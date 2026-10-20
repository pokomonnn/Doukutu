using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static partial class StoryIntegrationBuilder
{
    static void ConfigureObjectiveData()
    {
        ConfigureOtherObjectiveData();
        var mission=database.quests.First(q=>q.id=="S1S01").mission;
        Put(mission,"objectiveFinishFlag","story/S1S01_Completed");
        Put(mission,"displayObjectives",new List<MissionObjectiveDisplay2D>{
            new MissionObjectiveDisplay2D{id="find",text="旧居住区でフィンを捜し、治療を申し出る",anyOfFlags=new[]{"story/step_finn","story/S1S01_Treated","story/S1S01_Delivered"}},
            new MissionObjectiveDisplay2D{id="treat",text="救急キットの解毒剤でフィンを治療する",anyOfFlags=new[]{"story/S1S01_Treated","story/S1S01_Delivered"}},
            new MissionObjectiveDisplay2D{id="deliver",text="フィンをバルドのもとへ運ぶ",anyOfFlags=new[]{"story/S1S01_Delivered"}},
            new MissionObjectiveDisplay2D{id="report",text="バルドに報告して報酬を受け取る",anyOfFlags=new[]{"story/S1S01_Completed"}}
        });
        // Opening a conversation is not discovery confirmation; the player agrees after reading Finn's request.
        var dialogue=database.steps.First(s=>s.id=="finn").dialogue;
        var start=dialogue.FindNode("start");start.choices[0].eventId="";start.choices[0].label="フィンに話しかける";
        var last=dialogue.Nodes.Last();last.choices.Add(new DialogueChoice2D{label="解毒剤で治療することを伝える",eventId="step/finn"});
        last.choices.Add(new DialogueChoice2D{label="今は保留する"});EditorUtility.SetDirty(dialogue);
    }
    static MissionObjectiveDisplay2D Goal(string id,string text,params string[] flags) =>
        new MissionObjectiveDisplay2D{id=id,text=text,anyOfFlags=flags.Select(f=>"story/"+f).ToArray()};
    static void Goals(string quest,params MissionObjectiveDisplay2D[] goals)
    {
        var mission=database.quests.First(q=>q.id==quest).mission;
        Put(mission,"objectiveFinishFlag","story/"+quest+"_Completed");
        Put(mission,"displayObjectives",goals.ToList());
    }
    static void ConfigureOtherObjectiveData()
    {
        Goals("S1M01",
            Goal("diary","ステージ1の古い図書室で遠征隊の日誌を調べる","S1M01_ClueFound"),
            Goal("report","町へ戻り、町長に日誌を報告する","S1M01_Completed"));
        Goals("S2M01",
            Goal("machine","ステージ2の機械室で記録箱を調べる","S2M01_ClueFound"),
            Goal("report","町へ戻り、町長に起動手順を報告する","S2M01_Completed"));
        Goals("S3M01",
            Goal("map","ステージ3で巡礼路の経路図を調べる","S3M01_ClueFound"),
            Goal("report","町へ戻り、町長に経路図を報告する","S3M01_Completed"));
        Goals("S1S03",
            Goal("blueprint","ステージ1の資料室で古城の設計図を回収する","S1S03_BlueprintTaken"),
            Goal("report","設計図を所持品に入れ、ステージ1のグレンに渡す（紛失時は資料室で再取得）","S1S03_Completed"));
        Goals("S2S01",
            Goal("goods","ステージ2で奪われた補給箱か旧配給所の代替物資を回収する","S2S01_Goods"),
            Goal("report","補給物資を所持品に入れ、ステージ2のロアンに渡す（紛失時は再取得）","S2S01_Completed"));
        Goals("S2S02",
            Goal("ledger","ステージ2の倉庫で搬入記録を調べる","S2S02_LedgerFound","S2S02_PermitIssued"),
            Goal("truce","ステージ1でフィンを救助し、バルドへの報告を終える","PlayerBanditTruce","S2S02_PermitIssued"),
            Goal("permit","町へ戻り、町長と盗賊の入町許可を交渉する","S2S02_PermitIssued"),
            Goal("report","ステージ2のバルドに入町許可を伝える","S2S02_Completed"));
        Goals("S2S03",
            Goal("bearing","ステージ2で水車の軸受けを回収する","S2S03_BearingFound","S2S03_Installed"),
            Goal("valve","ステージ2で配管の弁を回収する","S2S03_ValveFound","S2S03_Installed"),
            Goal("gear","ステージ2で予備の歯車を回収する","S2S03_GearFound","S2S03_Installed"),
            Goal("install","部品3点を所持品に入れ、ステージ2のグレンに渡す（不足分は再取得）","S2S03_Installed"),
            Goal("water","ステージ2の修理済みの水門を調べ、水路を復旧する","S2S03_WaterRestored"),
            Goal("report","ステージ2のグレンに水路の復旧を報告する","S2S03_Completed"));
        Goals("S3S01",
            Goal("route","ステージ3で裏方用の通路を調べる","S3S01_RouteFound","S3S01_Proposal"),
            Goal("proposal","ステージ3のロアンに迂回路を提案する","S3S01_Proposal","S3S01_BaldoAgreed"),
            Goal("truce","ステージ3のバルドと通路の停戦を交渉する","S3S01_BaldoAgreed"),
            Goal("report","ステージ3のロアンに停戦の合意を報告する","S3S01_Completed"));
        Goals("S3S02",
            Goal("route","ステージ3で地上への交易路を調べる","S3S02_RouteFound"),
            Goal("permit","「新たな稼ぎ口」を完了し、ステージ2のバルドへ入町許可を伝える","S2S02_Completed"),
            Goal("report","ステージ3のバルドに交易路を報告する","S3S02_Completed"));
        Goals("S3S03",
            Goal("ledger","ステージ3で古城の修繕台帳を調べる","S3S03_LedgerFound"),
            Goal("structure","ステージ3で回廊の構造を調べる","S3S03_StructureChecked"),
            Goal("water","「止まった水路」を進め、ステージ2の水門で水路を復旧する","S2S03_WaterRestored"),
            Goal("report","ステージ3のグレンに古城の再建調査を報告する","S3S03_Completed"));
        Goals("S4M01",
            Goal("garden","ステージ4の庭で貴族と話し、町のことを伝える","S4M01_StorySeen"),
            Goal("ending","庭の貴族と再び話し、これからの結末を選ぶ（保留・再訪可能）","EndingCommitted"));
    }
    // Only update objective assets: preserve hand-edited scenes and dialogue.
    public static void UpdateAllObjectivesAndRunTests()
    {
        database=AssetDatabase.LoadAssetAtPath<StoryCampaignDatabase>(Root+"/Database.asset");
        ConfigureOtherObjectiveData();
        MissionMinimapDataInstaller.Configure(database);
        AssetDatabase.SaveAssets();
        StoryIntegrationTests.RunPlay();
    }
    static RectTransform HudRect(string name,Transform parent,Vector2 min,Vector2 max,Vector2 offsetMin,Vector2 offsetMax)
    {
        var go=new GameObject(name,typeof(RectTransform));var rect=(RectTransform)go.transform;
        rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.offsetMin=offsetMin;rect.offsetMax=offsetMax;return rect;
    }
    static TMP_Text HudText(string name,Transform parent,float top,float height,float size,Color color)
    {
        var rect=HudRect(name,parent,new Vector2(0,1),Vector2.one,new Vector2(18,-top-height),new Vector2(-18,-top));
        var text=rect.gameObject.AddComponent<TextMeshProUGUI>();text.font=Font;text.fontSize=size;text.color=color;
        text.fontStyle=FontStyles.Normal;text.textWrappingMode=TextWrappingModes.Normal;text.overflowMode=TextOverflowModes.Ellipsis;
        text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=size;
        text.alignment=TextAlignmentOptions.TopLeft;text.raycastTarget=false;return text;
    }
    static void InstallObjectiveHUD(GameObject storyRoot)
    {
        var existing=storyRoot.transform.Find("QuestTrackingHUD");
        if(existing!=null)return; // Preserve hand-tuned layout and sound assignments on regeneration.
        // Reuse the existing HUD behaviour, hiding its former text references before rebinding.
        var huds=Object.FindObjectsByType<MissionHUD2D>(FindObjectsInactive.Include);
        foreach(var old in huds) {
            var so=new SerializedObject(old);
            foreach(string field in new[]{"missionTitleText","missionDescriptionText","missionProgressText"}) {
                var text=so.FindProperty(field).objectReferenceValue as TMP_Text;if(text!=null)text.enabled=false;
            }
            old.enabled=false;
        }
        var canvasObject=new GameObject("QuestTrackingHUD",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));
        canvasObject.transform.SetParent(storyRoot.transform,false);
        var canvas=canvasObject.GetComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=30;
        var scaler=canvasObject.GetComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution=new Vector2(1920,1080);scaler.matchWidthOrHeight=.5f;
        var hud=canvasObject.AddComponent<MissionHUD2D>();
        var panel=HudRect("Panel",canvasObject.transform,Vector2.one,Vector2.one,Vector2.zero,Vector2.zero);
        panel.pivot=Vector2.one;panel.sizeDelta=new Vector2(440,150);panel.anchoredPosition=new Vector2(-24,-88);
        var background=panel.gameObject.AddComponent<Image>();background.color=new Color(.045f,.055f,.07f,.74f);background.raycastTarget=false;
        var objective=HudRect("Current",panel,Vector2.zero,Vector2.one,Vector2.zero,Vector2.zero).gameObject.AddComponent<CanvasGroup>();
        objective.blocksRaycasts=false;objective.interactable=false;
        var title=HudText("QuestName",objective.transform,14,38,24,new Color(.91f,.85f,.69f));
        var body=HudText("Objective",objective.transform,54,86,22,new Color(.95f,.95f,.92f));
        var noticeRect=HudRect("Completion",panel,new Vector2(0,0),new Vector2(1,0),new Vector2(0,-146),new Vector2(0,-8));
        var notice=noticeRect.gameObject.AddComponent<CanvasGroup>();notice.blocksRaycasts=false;notice.interactable=false;notice.alpha=0;
        var noticeBg=noticeRect.gameObject.AddComponent<Image>();noticeBg.color=new Color(.04f,.05f,.07f,.85f);noticeBg.raycastTarget=false;
        var completion=HudText("CompletedObjective",noticeRect,12,118,21,new Color(.91f,.85f,.69f));
        Put(hud,"missionManager",Object.FindFirstObjectByType<MissionManager2D>(FindObjectsInactive.Include));Put(hud,"hudRoot",panel.gameObject);
        Put(hud,"missionTitleText",title);Put(hud,"missionDescriptionText",body);Put(hud,"objectiveGroup",objective);
        Put(hud,"completionGroup",notice);Put(hud,"completionText",completion);
        var audio=canvasObject.AddComponent<AudioSource>();audio.playOnAwake=false;audio.spatialBlend=0;Put(hud,"audioSource",audio);
        canvasObject.AddComponent<MissionHUDSafeArea2D>().panel=panel;
        if(storyRoot.GetComponent<StoryCampaignScene>().stage>0)
            canvasObject.AddComponent<DungeonMinimap2D>().font=title.font;
    }
}
