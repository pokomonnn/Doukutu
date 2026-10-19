#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>元の600件のID/本文をそのまま保持してv1.5.0のScriptableObjectへ変換。既存データは上書きしない。</summary>
internal static class CharacterPackDialogueBuilder2D
{
    [Serializable] private sealed class Catalog { public Entry[] records; }
    [Serializable] private sealed class Entry
    {
        public string id, title, kind, group, situation, speakerInfo, prerequisites, personalityHint, playbackHint;
        public int stage;
        public Line[] lines;
    }
    [Serializable] private sealed class Line { public string speaker, text; }

    internal const string JsonPath = "Assets/CharacterPrefabPack2D/Editor/Dialogue600Catalog2D.json";
    internal const string ExistingPath = "Assets/DialogueStudio2D/Dialogue600/Generated";
    internal const string CustomPath = "Assets/CharacterPrefabPack2D/Generated/Dialogue";

    internal static bool PrepareAssets(out string report)
    {
        report = "";
        TextAsset json = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (json == null) { report = "同梱の600会話JSONがありません。"; return false; }
        Catalog catalog;
        try { catalog = JsonUtility.FromJson<Catalog>(json.text); }
        catch (Exception e) { report = e.Message; return false; }
        if (catalog == null || catalog.records == null || catalog.records.Length != 600)
        { report = "台本の件数が600件ではありません。"; return false; }
        var ids = new HashSet<string>();
        var amb = new Dictionary<string,List<Entry>>();
        var solo = new Dictionary<string,List<Entry>>();
        foreach (string k in new[]{"ADV","BND","DWF","CIV_GENERAL","CIV_NONCOMBAT","CIV_MEDICAL"}) amb[k] = new List<Entry>();
        foreach (string k in new[]{"ADV","BND","DWF","CIV","HOST_ADV","HOST_BND","HOST_GENERIC"}) solo[k] = new List<Entry>();
        int nAmbient=0,nSolo=0;
        foreach (Entry r in catalog.records)
        {
            if (r == null || string.IsNullOrEmpty(r.id) || !ids.Add(r.id) || r.lines == null ||
                r.lines.Length != (r.kind == "ambient" ? 3 : 1))
            { report = "ID重複またはセリフ欠落: " + (r != null ? r.id : "null"); return false; }
            foreach (Line l in r.lines)
                if (l == null || string.IsNullOrWhiteSpace(l.text)) { report = "空セリフ: " + r.id; return false; }
            if (r.kind == "ambient")
            {
                nAmbient++;
                if (r.group == "CROSS")
                {
                    if (r.speakerInfo.Contains("冒険者")) amb["ADV"].Add(r);
                    if (r.speakerInfo.Contains("盗賊")) amb["BND"].Add(r);
                    if (r.speakerInfo.Contains("ドワーフ")) amb["DWF"].Add(r);
                }
                else if (r.group == "CIV")
                {
                    string k = r.speakerInfo.Contains("医者") ? "CIV_MEDICAL" :
                        r.speakerInfo.Contains("非戦闘員") ? "CIV_NONCOMBAT" : "CIV_GENERAL";
                    amb[k].Add(r);
                }
                else amb[r.group].Add(r);
            }
            else
            {
                nSolo++;
                if (r.group != "HOST") solo[r.group].Add(r);
                else if (r.speakerInfo.Contains("盗賊")) solo["HOST_BND"].Add(r);
                else if (r.speakerInfo.Contains("冒険者")) solo["HOST_ADV"].Add(r);
                else solo["HOST_GENERIC"].Add(r);
            }
        }
        if (nAmbient != 350 || nSolo != 250) { report = "掛け合い/独り言の数が想定外"; return false; }
        solo["HOST_BND"].AddRange(solo["HOST_GENERIC"]);
        solo["HOST_ADV"].AddRange(solo["HOST_GENERIC"]);
        CharacterPrefabPackWindow2D.MakeFolders(ExistingPath);
        CharacterPrefabPackWindow2D.MakeFolders(CustomPath);
        int newCount = 0;
        foreach (var kv in amb)
        {
            string path = ExistingPath + "/AMB600_" + kv.Key + ".asset";
            if (AssetDatabase.LoadAssetAtPath<DialogueAmbientTalkData2D>(path) != null) continue;
            DialogueAmbientTalkData2D asset = ScriptableObject.CreateInstance<DialogueAmbientTalkData2D>();
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            so.FindProperty("displayName").stringValue = "600会話集・掛け合い " + kv.Key;
            SerializedProperty a = so.FindProperty("exchanges"); a.ClearArray();
            foreach (Entry r in kv.Value) AppendAmbient(a,r);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
            newCount++;
        }
        foreach (var kv in solo)
        {
            string path = ExistingPath + "/SOLO600_" + kv.Key + ".asset";
            if (AssetDatabase.LoadAssetAtPath<Dialogue600SoloLibrary2D>(path) != null) continue;
            Dialogue600SoloLibrary2D asset = ScriptableObject.CreateInstance<Dialogue600SoloLibrary2D>();
            AssetDatabase.CreateAsset(asset, path);
            var so = new SerializedObject(asset);
            so.FindProperty("displayName").stringValue = "600会話集・独り言 " + kv.Key;
            SerializedProperty a = so.FindProperty("entries"); a.ClearArray();
            foreach (Entry r in kv.Value) AppendSolo(a,r);
            so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
            newCount++;
        }
        MakeMonsterExtras();
        AssetDatabase.SaveAssets();
        report = "元台本600件(掛け合い350・独り言250)を検査。新規会話アセット="+newCount+
            "（既存データは維持）。モンスター専用の別台本も用意。";
        return true;
    }

    private static string RoleFaction(string text)
    {
        if (text.Contains("冒険者")) return "adventurers";
        if (text.Contains("盗賊")) return "bandits";
        if (text.Contains("ドワーフ")) return "dwarves";
        return "";
    }
    private static string Faction(string group)
    {
        switch(group) { case "ADV": return "adventurers"; case "BND":return "bandits";case "DWF":return "dwarves"; default:return ""; }
    }
    private static void AppendAmbient(SerializedProperty array, Entry r)
    {
        int at=array.arraySize;array.InsertArrayElementAtIndex(at);
        SerializedProperty e=array.GetArrayElementAtIndex(at);
        SetStr(e,"title",r.id+"　"+r.title);SetStr(e,"sourceDialogueId",r.id);
        SetInt(e,"requiredStage",r.stage);SetStr(e,"sourceSituation",r.situation);
        SetStr(e,"sourcePrerequisites",r.prerequisites);SetStr(e,"sourcePersonalityHint",r.personalityHint);
        SetStr(e,"sourcePlaybackHint",r.playbackHint);SetInt(e,"weight",1);SetFloat(e,"probability",1);
        SetBool(e,"requireInitiatorPersonality",false);SetBool(e,"requirePartnerPersonality",false);
        e.FindPropertyRelative("conditions").ClearArray();
        string first=Faction(r.group), second=first;
        string[] names=r.speakerInfo.Split(new[]{'＋'},StringSplitOptions.None);
        if(r.group=="CROSS" && names.Length==2)
        { first=RoleFaction(names[0]);second=RoleFaction(names[1]); }
        SetStr(e,"requiredInitiatorFactionId",first);SetStr(e,"requiredPartnerFactionId",second);
        SerializedProperty lines=e.FindPropertyRelative("lines");lines.ClearArray();
        for(int i=0;i<r.lines.Length;i++)
        {
            lines.InsertArrayElementAtIndex(i);
            SerializedProperty line=lines.GetArrayElementAtIndex(i);
            line.FindPropertyRelative("speaker").enumValueIndex=(names.Length==2 && r.lines[i].speaker.Trim()==names[1].Trim()) ? 1 : 0;
            SetStr(line,"text",r.lines[i].text);
            SetFloat(line,"displaySeconds",Mathf.Max(2.4f,r.lines[i].text.Length*.045f+.85f));
            SetFloat(line,"pauseAfterSeconds",.35f);
        }
    }
    private static void AppendSolo(SerializedProperty arr,Entry r)
    {
        int at=arr.arraySize;arr.InsertArrayElementAtIndex(at);SerializedProperty e=arr.GetArrayElementAtIndex(at);
        SetStr(e,"sourceDialogueId",r.id);SetStr(e,"title",r.title);
        SetInt(e,"requiredStage",r.stage);SetStr(e,"text",r.lines[0].text);
        SetStr(e,"speakerInfo",r.speakerInfo);SetStr(e,"sourceSituation",r.situation);
        SetStr(e,"sourcePrerequisites",r.prerequisites);SetStr(e,"sourcePersonalityHint",r.personalityHint);
        SetStr(e,"sourcePlaybackHint",r.playbackHint);SetInt(e,"weight",1);
    }

    private static void MakeMonsterExtras()
    {
        string[][] skeleton=
        {
            new[]{"骨が鳴る……まだ歩ける。","その腕、昨夜は右にあったか。","知らん。持ち主も覚えておらん。"},
            new[]{"門の向こうから鐘が聞こえる。","ここに鐘はない。","だから妙なのだ。"},
            new[]{"誰かの足音が近い。","生きた者か。","それとも、我らのような者か。"},
            new[]{"あの松明が眩しい。","目など持たぬだろう。","記憶が眩しがるのだ。"},
            new[]{"長くここにいるな。","何年経った？","数える指が足りぬ。"},
            new[]{"鎧の主を覚えているか。","名前だけは……いや。","名はもう石の下だ。"},
            new[]{"階段を数えてみた。","いくつだった？","降りるたび一段増えた。"},
            new[]{"喉が渇いた気がする。","喉などない。","それでも渇く。"},
            new[]{"誰かが祈りを捧げている。","耳を塞げ。","耳もないのに。"},
            new[]{"夜が来たら休もう。","ここはいつも夜だ。","なら、もう休めるはずだ。"}
        };
        string[][] slime=
        {
            new[]{"ぷる……ぷるる。","ぽよ？","ぷるん！"},
            new[]{"ぽこ、ぽこ。","ぷるるる……。","ぽこん。"},
            new[]{"……ぷち。","ぽよよ？","ぷるるっ。"},
            new[]{"ぽよん。","ぽよん！","ぷるっ。"},
            new[]{"ぷく、ぷく……。","ぷる……。","ぽん。"},
            new[]{"ぽよ……？","ぷるぷる。","ぽよっ！"},
            new[]{"ぷる……ぽこ。","ぽこぽこ。","ぷるる。"},
            new[]{"ぷちっ。","……ぷる？","ぷちぷち。"},
            new[]{"ぽよん、ぽよん。","ぷるるる。","ぽよ！"},
            new[]{"ぷく……。","ぽこ。","ぷるん……。"}
        };
        string[] skeletonSolo={"……誰の名を呼んでいた。","骨の軋みが、歌に聞こえる。","ここで何を守っていたのだ。","この剣だけは手放せぬ。","誰かの祈りが壁に残っている。","息がないのに、ため息が出る。","この廊下を何度巡った？","鎧の中を風が抜ける。","帰る場所など、もうない。","また鐘が鳴った……。"};
        string[] slimeSolo={"ぷるる……。","ぽよん！","ぷち……ぽこ。","ぽよ……？","ぷるぷる！","……ぽこん。","ぷく、ぷく。","ぽよよ。","ぷるんっ。","ぷちぷち……。"};
        MakeMonster("SKEL","スケルトン",skeleton,skeletonSolo);
        MakeMonster("SLIME","スライム",slime,slimeSolo);
        MakeMonster("SKEL_MERCHANT","スケルトン商人",skeleton,skeletonSolo);
        MakeMonster("SLIME_MERCHANT","スライム商人",slime,slimeSolo);
    }
    private static void MakeMonster(string code,string title,string[][] exchanges,string[] monologues)
    {
        string ap=CustomPath+"/AMB_NEW_"+code+".asset";
        if (AssetDatabase.LoadAssetAtPath<DialogueAmbientTalkData2D>(ap)==null)
        {
            var data=ScriptableObject.CreateInstance<DialogueAmbientTalkData2D>();
            AssetDatabase.CreateAsset(data,ap);
            var so=new SerializedObject(data);so.FindProperty("displayName").stringValue=title+"専用の掛け合い（新規・600件とは別）";
            var arr=so.FindProperty("exchanges");arr.ClearArray();
            for(int i=0;i<exchanges.Length;i++)
            {
                int at=arr.arraySize;arr.InsertArrayElementAtIndex(at);
                SerializedProperty e=arr.GetArrayElementAtIndex(at);
                SetStr(e,"title",title+"の反響 "+(i+1));SetStr(e,"sourceDialogueId","NEW_"+code+"_AMB_"+(i+1).ToString("000"));
                string f=code.EndsWith("_MERCHANT") ? "monster_traders" : "monsters";
                SetStr(e,"requiredInitiatorFactionId",f);SetStr(e,"requiredPartnerFactionId",f);
                SetInt(e,"weight",1);SetFloat(e,"probability",1f);
                e.FindPropertyRelative("conditions").ClearArray();
                SerializedProperty ls=e.FindPropertyRelative("lines");ls.ClearArray();
                for(int j=0;j<exchanges[i].Length;j++)
                {
                    ls.InsertArrayElementAtIndex(j);var l=ls.GetArrayElementAtIndex(j);
                    l.FindPropertyRelative("speaker").enumValueIndex=(j%2==1)?1:0;
                    SetStr(l,"text",exchanges[i][j]);SetFloat(l,"displaySeconds",2.5f);
                    SetFloat(l,"pauseAfterSeconds",.35f);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(data);
        }
        string sp=CustomPath+"/SOLO_NEW_"+code+".asset";
        if (AssetDatabase.LoadAssetAtPath<Dialogue600SoloLibrary2D>(sp)==null)
        {
            var data=ScriptableObject.CreateInstance<Dialogue600SoloLibrary2D>();AssetDatabase.CreateAsset(data,sp);
            var so=new SerializedObject(data);so.FindProperty("displayName").stringValue=title+"の独り言（新規・600件とは別）";
            var arr=so.FindProperty("entries");arr.ClearArray();
            for(int i=0;i<monologues.Length;i++)
            {
                arr.InsertArrayElementAtIndex(i);var e=arr.GetArrayElementAtIndex(i);
                SetStr(e,"sourceDialogueId","NEW_"+code+"_SOLO_"+(i+1).ToString("000"));
                SetStr(e,"title",title+"の独り言 "+(i+1));SetStr(e,"text",monologues[i]);
                SetInt(e,"weight",1);
            }
            so.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(data);
        }
    }

    private static void SetStr(SerializedProperty e,string n,string v){e.FindPropertyRelative(n).stringValue=v??"";}
    private static void SetInt(SerializedProperty e,string n,int v){e.FindPropertyRelative(n).intValue=v;}
    private static void SetFloat(SerializedProperty e,string n,float v){e.FindPropertyRelative(n).floatValue=v;}
    private static void SetBool(SerializedProperty e,string n,bool v){e.FindPropertyRelative(n).boolValue=v;}
}
#endif
