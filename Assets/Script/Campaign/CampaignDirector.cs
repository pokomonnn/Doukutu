using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public class CampaignDirector : MonoBehaviour
{
    public int stage;
    public Font japaneseFont;
    public ItemDataDatabase itemDatabase;
    public CampaignActor actor;
    public static CampaignDirector Current { get; private set; }
    public bool Talking => pages != null || choiceNode != null;
    public bool BlocksControls => Talking || journal || endingConfirm != null;
    public static bool ValidationMode;
    public CampaignData Data { get; private set; }
    public bool Has(string flag) => GameSessionManager.Instance.HasStoryFlag("campaign/" + flag);
    string SavePath => Path.Combine(Application.persistentDataPath, "story-campaign.json");
    string[] pages;
    CampaignNode choiceNode;
    int page;
    Action onRead;
    Vector2 scroll;
    string notice = "";
    string endingConfirm;
    bool journal;
    float previousTimeScale = 1;
    GameSessionManager Session => GameSessionManager.Instance;
    public void Set(string flag) => Session.SetStoryFlag("campaign/" + flag);

    void Awake()
    {
        Current = this;
        if (GameSessionManager.Instance == null) new GameObject("GameSessionManager").AddComponent<GameSessionManager>();
        Data = JsonUtility.FromJson<CampaignData>(Resources.Load<TextAsset>("Campaign/Story").text);
    }
    void Start()
    {
        if (stage == 4 && !CampaignRules.GardenOpen(Has)) { Travel(0); return; }
        if (stage == 4 && !Has("S4M01_Entered"))
            Speak(new[]{"S4M01/S4_ENTER", "S4M01/S4_SILENT_HALL"}, () => {Set("S4M01_Entered"); Save();});
    }
    void OnDestroy()
    {
        if (Talking) Time.timeScale = previousTimeScale;
        if (Current == this) Current = null;
    }
    public void Speak(IEnumerable<string> ids, Action done = null)
    {
        if (Talking) return;
        pages = ids.SelectMany(id => Data.Node(id).lines).ToArray();
        if (pages.Length == 0) { pages = null; done?.Invoke(); return; }
        page = 0; onRead = done; previousTimeScale = Time.timeScale; Time.timeScale = 0;
    }
    void Next()
    {
        if (++page < pages.Length) return;
        pages = null; Time.timeScale = previousTimeScale;
        var action = onRead; onRead = null; action?.Invoke();
    }
    public void BrowseOffer(string id)
    {
        if(Talking) return;
        if(id.Contains("/MAYOR_")) Set("MayorIntroduced");
        var node=Data.Node(id);
        Speak(new[]{id},()=>{
            if(id.Contains("_ACCEPT")) {Set(id.Split('/')[0]+"_Started");Save();return;}
            if(node.choices!=null && node.choices.Any(c=>c.target=="END" || Data.nodes.Any(n=>n.id==c.target))) {
                choiceNode=node;previousTimeScale=Time.timeScale;Time.timeScale=0;
            }
        });
    }
    public bool Ready(CampaignStep s) => CampaignRules.Ready(s, Has);
    public void Interact(CampaignStep s)
    {
        if (!Ready(s) || Talking || s.stage != stage) return;
        if (stage > 0 && (actor == null || Mathf.Abs(actor.transform.position.x-s.x)>2.3f)) return;
        if (s.id == "rescue" && (actor.escort == null || Vector2.Distance(actor.escort.position,actor.transform.position)>3)) return;
        Speak(s.nodes, () => {
            if (!Ready(s)) return;
            Set("step_" + s.id);
            foreach (string flag in s.flags) Set(flag);
            if(s.id=="report1" || s.id=="report2" || s.id=="report3") Set("MayorIntroduced");
            if (s.reward > 0) Session.AddMoney(s.reward);
            if (CampaignRules.GardenOpen(Has)) Set("STAGE4_UNLOCKED");
            Save();
            if (s.id == "rescue") Travel(0);
        });
    }
    public void Travel(int destination)
    {
        if (Talking || (destination == 4 && !CampaignRules.GardenOpen(Has))) return;
        if (!Save()) return;
        SceneManager.LoadScene(destination == 0 ? "CampaignTown" : "CampaignStage" + destination);
    }
    public bool Save()
    {
        if (ValidationMode) return true;
        try {
            var data = Session.CreateSaveGameData(SceneManager.GetActiveScene().name);
            string temp = SavePath + ".tmp";
            File.WriteAllText(temp, JsonUtility.ToJson(data, true));
            if (File.Exists(SavePath)) File.Replace(temp, SavePath, SavePath + ".bak");
            else File.Move(temp, SavePath);
            notice = "進行を保存しました"; return true;
        } catch (Exception e) { notice = "保存失敗：" + e.Message; Debug.LogError(notice); return false; }
    }
    void Load()
    {
        try {
            var data = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(SavePath));
            if (!Session.ApplySaveGameData(data, itemDatabase, out string error)) {notice=error; return;}
            // Always resume in town so a transient escort or a combat encounter cannot strand a save.
            SceneManager.LoadScene("CampaignTown");
        } catch (Exception e) {notice="読込失敗："+e.Message;}
    }
    public void Ending(string kind)
    {
        if (!CampaignRules.CanEnd(kind, Has)) return;
        Set("EndingCommitted"); Set("EndingType_" + kind); Set("S4M01_Completed");
        if (kind == "B") Set("NobleReturnedToTown");
        if (kind == "C") Set("CastleRebuildPlanned");
        if (!Has("RewardDelivered")) { Set("RewardDelivered"); Session.AddMoney(500); }
        Save(); endingConfirm = null;
        PlayEnding(kind);
    }
    void PlayEnding(string kind)
    {
        var ids = Data.nodes.Where(n => n.id.StartsWith("S4M01/S4_END_" + kind + "_") && !n.id.EndsWith("_CONFIRM")).Select(n => n.id);
        Speak(ids, () => { Set("EndingScenesRead"); Save(); Travel(0); });
    }
    void Update()
    {
        if (Talking) {if (pages!=null && Input.GetKeyDown(KeyCode.Return)) Next(); return;}
        if (Input.GetKeyDown(KeyCode.Tab)) journal = !journal;
        if (Input.GetKeyDown(KeyCode.F5)) Save();
        if (Input.GetKeyDown(KeyCode.F9)) Load();
    }
    void OnGUI()
    {
        var oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(Screen.width/1280f, Screen.height/720f, 1));
        GUI.skin.font = japaneseFont;
        GUI.skin.label.fontSize = GUI.skin.button.fontSize = 18;
        GUI.skin.label.wordWrap = true;
        if(choiceNode!=null) {
            GUI.Box(new Rect(140,210,1000,450),"どう答えますか？");
            int index=0;
            foreach(var choice in choiceNode.choices.Where(c=>c.target=="END" || Data.nodes.Any(n=>n.id==c.target))) {
                if(GUI.Button(new Rect(180,260+index++*60,920,50),choice.label)) {
                    choiceNode=null;Time.timeScale=previousTimeScale;
                    if(choice.target!="END") BrowseOffer(choice.target);
                    break;
                }
            }
            GUI.matrix=oldMatrix;return;
        }
        if (Talking) {
            GUI.Box(new Rect(70,420,1140,260), "");
            GUI.Label(new Rect(100,450,1080,160), pages[page]);
            if (GUI.Button(new Rect(930,625,240,40), $"次へ  {page+1}/{pages.Length}  Enter")) Next();
            GUI.matrix = oldMatrix; return;
        }
        GUI.Box(new Rect(10,10,1260,65), "");
        GUI.Label(new Rect(25,20,910,55), $"{new[]{"町", "旧居住区", "忘れられた工房区", "巡礼路と大聖堂", "女神の庭"}[stage]}　所持金 {Session.CurrentMoney} G\nA/D 移動・Space ジャンプ・左クリック 射撃・E 調べる・Tab 目的・F5 保存・F9 読込");
        if (GUI.Button(new Rect(1040,20,210,40), "セーブを読み込む")) Load();
        GUI.Label(new Rect(20,680,1200,35), notice);
        if (stage == 0 || journal) DrawJournal();
        if (stage > 0 && actor != null && actor.transform.position.x < 5 && GUI.Button(new Rect(25,90,220,40), "帰還する")) Travel(0);
        if (stage == 4 && actor != null && actor.transform.position.x > 36 && Has("S4M01_StorySeen")) DrawEndings();
        if (stage == 0 && Has("EndingCommitted")) {
            string kind = new[]{"A","B","C"}.First(k=>Has("EndingType_"+k));
            if (!Has("EndingScenesRead") && GUI.Button(new Rect(950,475,300,40), "結末の会話を再開")) PlayEnding(kind);
            if (GUI.Button(new Rect(950,525,300,40), "帰還後の人々と話す"))
                Speak(Data.nodes.Where(n=>n.id.StartsWith("S4M01/END_"+kind+"_") && !n.id.Contains("REPEAT") && !n.id.Contains("OPTIONAL")).Select(n=>n.id));
        }
    }
    void DrawJournal()
    {
        GUI.Box(new Rect(20,90,910,575), "");
        GUILayout.BeginArea(new Rect(35,100,880,550));
        scroll=GUILayout.BeginScrollView(scroll);
        foreach (var group in Data.steps.GroupBy(s=>s.quest)) {
            GUILayout.Label((Has(group.Key+"_Completed")?"✓ ":"○ ")+group.First().questTitle);
            if (stage == 0 && !Has(group.Key+"_Completed")) {
                var offer=Data.offers.FirstOrDefault(id=>id.StartsWith(group.Key+"/"));
                if (offer != null && !group.Key.EndsWith("S02") && GUILayout.Button("依頼を聞く")) BrowseOffer(offer);
            }
            foreach(var s in group.Where(s=>!Has("step_"+s.id) && !Has(s.quest+"_Completed"))) {
                GUILayout.Label($"  {(Ready(s)?"進行可":"条件待ち")}：{s.title}（{(s.stage==0?"町":"ステージ"+s.stage)}）");
                if (s.stage==0 && stage==0 && Ready(s) && GUILayout.Button("話す："+s.title)) Interact(s);
            }
        }
        GUILayout.EndScrollView(); GUILayout.EndArea();
        if(stage!=0) return;
        for(int i=1;i<=4;i++) {
            GUI.enabled=i!=4||CampaignRules.GardenOpen(Has);
            if(GUI.Button(new Rect(950,90+i*65,300,50),new[]{"","旧居住区","忘れられた工房区","巡礼路と大聖堂","女神の庭"}[i])) Travel(i);
        }
        GUI.enabled=true;
        if (Has("MayorIntroduced") && Data.steps.Any(s=>s.flags.Any(f=>f.EndsWith("ClueFound")) && Has("step_"+s.id)) && GUI.Button(new Rect(950,425,300,40),"執事に話す"))
            Speak(new[]{Has("ButlerAppeared")?"S1M01/BUTLER_REPEAT":"S1M01/BUTLER_FIRST"},()=>{Set("ButlerAppeared");Save();});
    }
    void DrawEndings()
    {
        if (Has("EndingCommitted")) {
            if(GUI.Button(new Rect(900,200,340,50),"選んだ結末を読む")) PlayEnding(new[]{"A","B","C"}.First(k=>Has("EndingType_"+k)));
            return;
        }
        if(endingConfirm!=null) {
            GUI.Box(new Rect(350,160,580,230), "この選択で結末を確定します");
            GUI.Label(new Rect(380,205,520,80), string.Join("\n",Data.Node("S4M01/S4_END_"+endingConfirm+"_CONFIRM").lines));
            if(GUI.Button(new Rect(380,320,230,45),"この結末を選ぶ")) Ending(endingConfirm);
            if(GUI.Button(new Rect(640,320,230,45),"もう少し考える")) endingConfirm=null;
            return;
        }
        string[] labels={"A：ふたりの時間","B：地上への帰還","C：古城再建（3派閥の協力）"};
        for(int i=0;i<3;i++) {string k=((char)('A'+i)).ToString(); GUI.enabled=CampaignRules.CanEnd(k,Has); if(GUI.Button(new Rect(850,180+i*65,400,50),labels[i])) endingConfirm=k;}
        GUI.enabled=true;
    }
}
