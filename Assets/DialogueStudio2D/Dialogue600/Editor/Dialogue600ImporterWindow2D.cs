#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>ユーザー台本TXTを検査して作成したJSONから、STEP5/独り言用ScriptableObjectを生成します。</summary>
public sealed class Dialogue600ImporterWindow2D : EditorWindow
{
    [Serializable] private sealed class Catalog { public DialogueRecord[] records; }
    [Serializable] private sealed class DialogueRecord
    {
        public string id, title, kind, group, stageDescription, situation, speakerInfo;
        public string prerequisites, personalityHint, playbackHint;
        public int stage;
        public DialogueLine[] lines;
    }
    [Serializable] private sealed class DialogueLine { public string speaker, text; }

    private const string JsonPath = "Assets/DialogueStudio2D/Dialogue600/Editor/Dialogue600Catalog2D.json";
    private const string Generated = "Assets/DialogueStudio2D/Dialogue600/Generated";
    private static readonly string[] GroupCodes = { "ADV", "BND", "DWF", "CIV_GENERAL", "CIV_NONCOMBAT", "CIV_MEDICAL" };
    private static readonly string[] GroupLabels = { "冒険者", "盗賊", "ドワーフ", "救出NPC・一般人", "救出NPC・運搬人", "救出NPC・医者と助手" };
    private int groupIndex;
    private bool enableCrossFactionTalk;
    private bool replaceExistingReferences;
    private Vector2 scroll;

    [MenuItem("Tools/2D Shooter/Dialogue Studio/600会話集・一括インポート")]
    public static void Open() => GetWindow<Dialogue600ImporterWindow2D>("600会話集インポート");

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("DIALOGUE STUDIO 2D  /  600会話集", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("オリジナルTXTを元にした600件。掛け合い350／独り言250。元のID、本文、ステージ、場面、性格目安を保持します。戦闘専用のセリフは含まれません。", MessageType.Info);
        TextAsset file = AssetDatabase.LoadAssetAtPath<TextAsset>(JsonPath);
        if (file == null) EditorGUILayout.HelpBox("データJSONが見つかりません。ZIPのAssets/DialogueStudio2D/Dialogue600をフォルダごとコピーしてください。", MessageType.Error);
        else EditorGUILayout.LabelField("変換元", JsonPath);
        if (GUILayout.Button("① 会話600件をScriptableObjectへ登録 / 再登録", GUILayout.Height(38)) && file != null)
            ImportAll(file);
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("② NPC・Enemyへの一括割り当て", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("HierarchyやProjectで、対象NPCを1体以上選択してください。Prefabアセットの選択にも対応。今までのSTEP5の会話は保持したまま、600会話集を追加枠に設定します。", MessageType.None);
        groupIndex = EditorGUILayout.Popup("キャラクター種別", groupIndex, GroupLabels);
        enableCrossFactionTalk = EditorGUILayout.ToggleLeft("異派閥（双方非敵対）の会話も許可する", enableCrossFactionTalk);
        replaceExistingReferences = EditorGUILayout.ToggleLeft("既存の600会話集の参照だけ上書きする", replaceExistingReferences);
        if (GUILayout.Button("選択キャラクターに会話集を割り当てる", GUILayout.Height(34))) BindSelection();
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("③ シーンのステージ設定", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("ステージ指定の台本を正しい階層だけで再生するには、シーン内にDialogue600Stage2Dを1つ配置して1～3を設定してください。未配置なら全ステージ共通台本だけが候補になります。", MessageType.Warning);
        if (GUILayout.Button("シーンにステージ設定オブジェクトを作る"))
        {
            GameObject go = new GameObject("Dialogue600Stage2D_ステージ設定");
            Undo.RegisterCreatedObjectUndo(go, "600会話集ステージ設定");
            Undo.AddComponent<Dialogue600Stage2D>(go);
            Selection.activeGameObject = go;
        }
        EditorGUILayout.Space(8);
        if (GUILayout.Button("Projectで生成済み会話を開く"))
        {
            UnityEngine.Object obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(Generated);
            if (obj != null) EditorGUIUtility.PingObject(obj);
        }
        EditorGUILayout.EndScrollView();
    }

    private static string Faction(string id)
    {
        switch (id)
        {
            case "ADV": return "adventurers";
            case "BND": return "bandits";
            case "DWF": return "dwarves";
            default: return string.Empty; // 救出NPCの派閥IDは実際のPrefab側設定を優先
        }
    }

    private static string RoleFaction(string speaker)
    {
        if (speaker.Contains("冒険者")) return "adventurers";
        if (speaker.Contains("盗賊")) return "bandits";
        if (speaker.Contains("ドワーフ")) return "dwarves";
        return string.Empty;
    }

    private static string AmbientDestination(DialogueRecord r)
    {
        if (r.group != "CIV") return r.group;
        if (r.speakerInfo.Contains("医者")) return "CIV_MEDICAL";
        if (r.speakerInfo.Contains("非戦闘員")) return "CIV_NONCOMBAT";
        return "CIV_GENERAL";
    }

    private static void AddAmbient(SerializedProperty array, DialogueRecord r)
    {
        int n = array.arraySize;
        array.InsertArrayElementAtIndex(n);
        SerializedProperty e = array.GetArrayElementAtIndex(n);
        e.FindPropertyRelative("title").stringValue = r.id + "　" + r.title;
        e.FindPropertyRelative("weight").intValue = 1;
        e.FindPropertyRelative("probability").floatValue = 1f;
        e.FindPropertyRelative("requireInitiatorPersonality").boolValue = false;
        e.FindPropertyRelative("requirePartnerPersonality").boolValue = false;
        e.FindPropertyRelative("conditions").ClearArray();
        e.FindPropertyRelative("sourceDialogueId").stringValue = r.id;
        e.FindPropertyRelative("requiredStage").intValue = r.stage;
        e.FindPropertyRelative("sourceSituation").stringValue = r.situation;
        e.FindPropertyRelative("sourcePrerequisites").stringValue = r.prerequisites;
        e.FindPropertyRelative("sourcePersonalityHint").stringValue = r.personalityHint;
        e.FindPropertyRelative("sourcePlaybackHint").stringValue = r.playbackHint;
        string first = Faction(r.group);
        string second = first;
        if (r.group == "CROSS")
        {
            string[] roles = r.speakerInfo.Split(new[] { '＋' }, StringSplitOptions.None);
            if (roles.Length == 2)
            {
                first = RoleFaction(roles[0]);
                second = RoleFaction(roles[1]);
            }
        }
        e.FindPropertyRelative("requiredInitiatorFactionId").stringValue = first;
        e.FindPropertyRelative("requiredPartnerFactionId").stringValue = second;

        string[] speakerNames = r.speakerInfo.Split(new[] { '＋' }, StringSplitOptions.None);
        string firstName = speakerNames.Length > 0 ? speakerNames[0].Trim() : string.Empty;
        string secondName = speakerNames.Length > 1 ? speakerNames[1].Trim() : string.Empty;
        SerializedProperty lines = e.FindPropertyRelative("lines");
        lines.ClearArray();
        for (int i = 0; i < r.lines.Length; i++)
        {
            DialogueLine line = r.lines[i];
            lines.InsertArrayElementAtIndex(i);
            SerializedProperty p = lines.GetArrayElementAtIndex(i);
            bool partner = !string.IsNullOrEmpty(secondName) && line.speaker.Trim() == secondName;
            p.FindPropertyRelative("speaker").enumValueIndex = partner ? 1 : 0;
            p.FindPropertyRelative("text").stringValue = line.text;
            p.FindPropertyRelative("displaySeconds").floatValue = Mathf.Max(2.4f, line.text.Length * .045f + .85f);
            p.FindPropertyRelative("pauseAfterSeconds").floatValue = .35f;
        }
    }

    private static void AddSolo(SerializedProperty array, DialogueRecord r)
    {
        int n = array.arraySize;
        array.InsertArrayElementAtIndex(n);
        SerializedProperty e = array.GetArrayElementAtIndex(n);
        e.FindPropertyRelative("sourceDialogueId").stringValue = r.id;
        e.FindPropertyRelative("title").stringValue = r.title;
        e.FindPropertyRelative("requiredStage").intValue = r.stage;
        e.FindPropertyRelative("text").stringValue = r.lines[0].text;
        e.FindPropertyRelative("speakerInfo").stringValue = r.speakerInfo;
        e.FindPropertyRelative("sourceSituation").stringValue = r.situation;
        e.FindPropertyRelative("sourcePrerequisites").stringValue = r.prerequisites;
        e.FindPropertyRelative("sourcePersonalityHint").stringValue = r.personalityHint;
        e.FindPropertyRelative("sourcePlaybackHint").stringValue = r.playbackHint;
        e.FindPropertyRelative("weight").intValue = 1;
    }

    private static void EnsureFolders(string path)
    {
        string[] parts = path.Split('/');
        string running = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            string next = running + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(running, parts[i]);
            running = next;
        }
    }

    private static T GetOrCreate<T>(string name) where T : ScriptableObject
    {
        string path = Generated + "/" + name + ".asset";
        T asset = AssetDatabase.LoadAssetAtPath<T>(path);
        if (asset != null) return asset;
        asset = CreateInstance<T>();
        AssetDatabase.CreateAsset(asset, path);
        return asset;
    }

    private void ImportAll(TextAsset file)
    {
        Catalog catalog;
        try { catalog = JsonUtility.FromJson<Catalog>(file.text); }
        catch (Exception e) { EditorUtility.DisplayDialog("読み込み失敗", e.Message, "閉じる"); return; }
        if (catalog == null || catalog.records == null || catalog.records.Length != 600)
        {
            EditorUtility.DisplayDialog("データ件数不一致", "600件のデータを読み込めません。JSONを確認してください。", "閉じる");
            return;
        }
        HashSet<string> ids = new HashSet<string>();
        foreach (DialogueRecord r in catalog.records)
            if (r == null || !ids.Add(r.id) || r.lines == null || r.lines.Length != (r.kind == "ambient" ? 3 : 1))
            {
                EditorUtility.DisplayDialog("会話データ異常", r != null ? r.id : "null", "閉じる"); return;
            }
        if (!EditorUtility.DisplayDialog("600会話集を登録", "Generatedフォルダに同名データが存在する場合は、内容を600会話集で再構成します。手動編集済みの生成データはバックアップしてから実行してください。\n既存のSTEP1～5データは変更しません。", "登録", "キャンセル")) return;
        EnsureFolders(Generated);
        Dictionary<string, List<DialogueRecord>> ambient = new Dictionary<string, List<DialogueRecord>>();
        Dictionary<string, List<DialogueRecord>> solos = new Dictionary<string, List<DialogueRecord>>();
        foreach (string g in GroupCodes) ambient[g] = new List<DialogueRecord>();
        foreach (string g in new[] { "ADV", "BND", "DWF", "CIV", "HOST_BND", "HOST_ADV", "HOST_GENERIC" }) solos[g] = new List<DialogueRecord>();
        foreach (DialogueRecord r in catalog.records)
        {
            if (r.kind == "ambient")
            {
                if (r.group != "CROSS") ambient[AmbientDestination(r)].Add(r);
                else
                {
                    // どちらが会話を始めても候補になるよう、両派閥のアセットに登録。
                    if (r.speakerInfo.Contains("冒険者")) ambient["ADV"].Add(r);
                    if (r.speakerInfo.Contains("盗賊")) ambient["BND"].Add(r);
                    if (r.speakerInfo.Contains("ドワーフ")) ambient["DWF"].Add(r);
                }
            }
            else if (r.group != "HOST") solos[r.group].Add(r);
            else if (r.speakerInfo.Contains("盗賊")) solos["HOST_BND"].Add(r);
            else if (r.speakerInfo.Contains("冒険者")) solos["HOST_ADV"].Add(r);
            else solos["HOST_GENERIC"].Add(r);
        }
        // 任意の知性ある敵対NPC向け汎用10件は冒険者・盗賊の敵対用にも追加（IDは維持）。
        solos["HOST_BND"].AddRange(solos["HOST_GENERIC"]);
        solos["HOST_ADV"].AddRange(solos["HOST_GENERIC"]);
        try
        {
            int index = 0;
            foreach (KeyValuePair<string, List<DialogueRecord>> pair in ambient)
            {
                EditorUtility.DisplayProgressBar("600会話集", "掛け合い " + pair.Key, ++index / 13f);
                DialogueAmbientTalkData2D asset = GetOrCreate<DialogueAmbientTalkData2D>("AMB600_" + pair.Key);
                SerializedObject so = new SerializedObject(asset);
                so.FindProperty("displayName").stringValue = "600会話集・掛け合い " + pair.Key;
                SerializedProperty array = so.FindProperty("exchanges");
                array.ClearArray();
                foreach (DialogueRecord r in pair.Value) AddAmbient(array, r);
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
            }
            foreach (KeyValuePair<string, List<DialogueRecord>> pair in solos)
            {
                EditorUtility.DisplayProgressBar("600会話集", "独り言 " + pair.Key, ++index / 13f);
                Dialogue600SoloLibrary2D asset = GetOrCreate<Dialogue600SoloLibrary2D>("SOLO600_" + pair.Key);
                SerializedObject so = new SerializedObject(asset);
                so.FindProperty("displayName").stringValue = "600会話集・独り言 " + pair.Key;
                SerializedProperty array = so.FindProperty("entries");
                array.ClearArray();
                foreach (DialogueRecord r in pair.Value) AddSolo(array, r);
                so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets(); AssetDatabase.Refresh();
        }
        finally { EditorUtility.ClearProgressBar(); }
        EditorUtility.DisplayDialog("登録完了", "台本600件を登録しました。掛け合い350件（異派閥60件は各派閥データに参照用に複製）・独り言250件。\n次にHierarchyやPrefabからNPCを選択して割り当ててください。", "OK");
    }

    private void BindSelection()
    {
        string group = GroupCodes[groupIndex];
        DialogueAmbientTalkData2D ambient = AssetDatabase.LoadAssetAtPath<DialogueAmbientTalkData2D>(Generated + "/AMB600_" + group + ".asset");
        string soloGroup = group.StartsWith("CIV") ? "CIV" : group;
        Dialogue600SoloLibrary2D solo = AssetDatabase.LoadAssetAtPath<Dialogue600SoloLibrary2D>(Generated + "/SOLO600_" + soloGroup + ".asset");
        Dialogue600SoloLibrary2D hostile = null;
        if (group == "ADV" || group == "BND") hostile = AssetDatabase.LoadAssetAtPath<Dialogue600SoloLibrary2D>(Generated + "/SOLO600_HOST_" + group + ".asset");
        if (ambient == null || solo == null)
        {
            EditorUtility.DisplayDialog("先にインポート", "まず①の一括登録を実行してください。", "OK"); return;
        }
        GameObject[] objects = Selection.gameObjects;
        if (objects == null || objects.Length == 0) { EditorUtility.DisplayDialog("選択がありません", "HierarchyまたはProjectでNPCを選択してください。", "OK"); return; }
        int applied = 0;
        foreach (GameObject obj in objects)
        {
            if (obj == null) continue;
            string path = AssetDatabase.GetAssetPath(obj);
            bool prefab = path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            GameObject target = prefab ? PrefabUtility.LoadPrefabContents(path) : obj;
            try
            {
                Apply(target, ambient, solo, hostile);
                if (prefab) PrefabUtility.SaveAsPrefabAsset(target, path);
                else EditorUtility.SetDirty(target);
                applied++;
            }
            finally { if (prefab) PrefabUtility.UnloadPrefabContents(target); }
        }
        EditorUtility.DisplayDialog("割り当て", applied + "体に会話集を割り当てました。\n実行時に必要なのはFactionMember2Dと話者側の会話可能状態です。", "OK");
    }

    private void Apply(GameObject actor, DialogueAmbientTalkData2D ambientData, Dialogue600SoloLibrary2D soloData, Dialogue600SoloLibrary2D hostileData)
    {
        DialogueAmbientTalk2D talk = actor.GetComponent<DialogueAmbientTalk2D>();
        bool isNewAmbient = talk == null;
        if (isNewAmbient) talk = Undo.AddComponent<DialogueAmbientTalk2D>(actor);
        SerializedObject so = new SerializedObject(talk);
        if (isNewAmbient)
        {
            so.FindProperty("minimumRestSeconds").floatValue = 120f;
            so.FindProperty("maximumRestSeconds").floatValue = 240f;
        }
        SerializedProperty talkProp = so.FindProperty("additionalDialogue600TalkData");
        if (replaceExistingReferences || talkProp.objectReferenceValue == null) talkProp.objectReferenceValue = ambientData;
        if (enableCrossFactionTalk) so.FindProperty("allowDifferentNonHostileFactions").boolValue = true;
        so.ApplyModifiedProperties();

        Dialogue600SoloSpeaker2D speech = actor.GetComponent<Dialogue600SoloSpeaker2D>();
        if (speech == null) speech = Undo.AddComponent<Dialogue600SoloSpeaker2D>(actor);
        SerializedObject soloObj = new SerializedObject(speech);
        SerializedProperty normal = soloObj.FindProperty("regularLibrary");
        if (replaceExistingReferences || normal.objectReferenceValue == null) normal.objectReferenceValue = soloData;
        SerializedProperty hostile = soloObj.FindProperty("hostileLibrary");
        if (replaceExistingReferences || hostile.objectReferenceValue == null) hostile.objectReferenceValue = hostileData;
        soloObj.ApplyModifiedProperties();
    }
}
#endif
