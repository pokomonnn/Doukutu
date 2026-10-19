#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Dialogue StudioのSTEP3専用画面。通常会話データは変更しません。</summary>
public sealed class DialogueBattleBarkStudioWindow2D : EditorWindow
{
    private static readonly Color Accent = new Color(.36f, .77f, .68f);
    private readonly List<DialogueBattleBarkData2D> assets = new List<DialogueBattleBarkData2D>();
    [SerializeField] private DialogueBattleBarkData2D selected;
    [SerializeField] private string search = "";
    [SerializeField] private string newName = "BattleBarks_New";
    [SerializeField] private string saveFolder = "Assets/DialogueStudio2D/BattleBarkData";
    [SerializeField] private int triggerFilter = -1;
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private bool refreshNeeded = true;

    [MenuItem("Tools/2D Shooter/Dialogue Studio/STEP3 戦闘セリフ管理")]
    public static void Open()
    {
        var window = GetWindow<DialogueBattleBarkStudioWindow2D>("Dialogue STEP3");
        window.minSize = new Vector2(940, 610);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += NeedRefresh;
        NeedRefresh();
    }

    private void OnDisable() { EditorApplication.projectChanged -= NeedRefresh; }
    private void NeedRefresh() { refreshNeeded = true; Repaint(); }

    private void OnGUI()
    {
        if (refreshNeeded) RefreshAssets();
        Rect header = GUILayoutUtility.GetRect(0, 62, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(header, new Color(.15f, .20f, .24f));
        GUIStyle title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 19 };
        title.normal.textColor = Color.white;
        GUI.Label(new Rect(header.x + 14, header.y + 7, header.width - 25, 30),
            "DIALOGUE STUDIO 2D / STEP3", title);
        GUI.Label(new Rect(header.x + 16, header.y + 38, header.width - 25, 18),
            "NPC / ENEMY  戦闘中の頭上セリフ・条件・クールダウン", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        DrawLeft();
        GUILayout.Space(7);
        DrawRight();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeft()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(315));
        GUILayout.Space(10);
        Heading("01 戦闘セリフデータ");
        search = EditorGUILayout.TextField("検索", search);
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUILayout.ExpandHeight(true));
        foreach (var data in assets)
        {
            if (data == null) continue;
            if (!string.IsNullOrEmpty(search) &&
                data.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                data.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            Color old = GUI.backgroundColor;
            if (selected == data) GUI.backgroundColor = Accent;
            if (GUILayout.Button(data.DisplayName + "\n" + data.name, GUILayout.Height(42)))
                selected = data;
            GUI.backgroundColor = old;
        }
        EditorGUILayout.EndScrollView();
        Heading("02 作成・管理");
        newName = EditorGUILayout.TextField("ファイル名", newName);
        saveFolder = EditorGUILayout.TextField("保存先", saveFolder);
        if (GUILayout.Button("＋ 新しい戦闘セリフデータを作成", GUILayout.Height(33))) CreateAsset();
        using (new EditorGUI.DisabledScope(selected == null))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("複製")) Duplicate();
            if (GUILayout.Button("Projectで表示")) EditorGUIUtility.PingObject(selected);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("選択したキャラに割り当て", GUILayout.Height(28))) AssignSelected();
            if (GUILayout.Button("削除（確認あり）")) DeleteAsset();
        }
        EditorGUILayout.HelpBox("キャラをHierarchyまたはPrefabモードで選択してから『割り当て』。敵・NPCへのAI変更や通常会話データの編集はしません。", MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private void DrawRight()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Space(10);
        Heading("03 セリフ・発生タイミング");
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        if (selected == null)
        {
            EditorGUILayout.HelpBox("左側で戦闘セリフデータを作成、または選択してください。", MessageType.Info);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
            return;
        }
        SerializedObject so = new SerializedObject(selected);
        so.Update();
        EditorGUILayout.ObjectField("データ", selected, typeof(DialogueBattleBarkData2D), false);
        EditorGUILayout.PropertyField(so.FindProperty("displayName"), new GUIContent("表示名"));
        EditorGUILayout.HelpBox("敵発見・攻撃・被弾・HP低下・逃走・身をすくめる・仲間への警報・見失い・死亡に対応。戦闘は止まらず、短い吹き出しだけ表示します。", MessageType.Info);
        string[] filterNames = { "すべて", "敵を発見", "攻撃", "被弾", "HP低下", "逃走開始", "身をすくめる", "仲間へ警報", "敵を見失う", "死亡" };
        triggerFilter = EditorGUILayout.Popup("種類で絞り込み", Mathf.Clamp(triggerFilter + 1, 0, 9), filterNames) - 1;
        SerializedProperty lines = so.FindProperty("lines");
        int removeAt = -1;
        for (int i = 0; i < lines.arraySize; i++)
        {
            SerializedProperty line = lines.GetArrayElementAtIndex(i);
            SerializedProperty trigger = line.FindPropertyRelative("trigger");
            if (triggerFilter >= 0 && trigger.enumValueIndex != triggerFilter) continue;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("セリフ " + (i + 1), EditorStyles.boldLabel);
            if (GUILayout.Button("削除", GUILayout.Width(50))) removeAt = i;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(trigger, new GUIContent("発生タイミング"));
            EditorGUILayout.PropertyField(line.FindPropertyRelative("text"), new GUIContent("セリフ本文"), true);
            EditorGUILayout.PropertyField(line.FindPropertyRelative("weight"), new GUIContent("抽選の重み"));
            EditorGUILayout.PropertyField(line.FindPropertyRelative("probability"), new GUIContent("発言確率"));
            EditorGUILayout.PropertyField(line.FindPropertyRelative("conditions"), new GUIContent("発言条件（STEP2）"), true);
            EditorGUILayout.EndVertical();
        }
        if (removeAt >= 0) lines.DeleteArrayElementAtIndex(removeAt);
        if (GUILayout.Button("＋ 戦闘セリフを追加", GUILayout.Height(30)))
        {
            lines.arraySize++;
            SerializedProperty added = lines.GetArrayElementAtIndex(lines.arraySize - 1);
            added.FindPropertyRelative("trigger").enumValueIndex = triggerFilter >= 0 ? triggerFilter : 0;
            added.FindPropertyRelative("text").stringValue = "新しいセリフ";
            added.FindPropertyRelative("weight").intValue = 1;
            added.FindPropertyRelative("probability").floatValue = 1f;
            added.FindPropertyRelative("conditions").ClearArray();
        }
        if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(selected);
        GUILayout.Space(10);
        Heading("04 設定のヒント");
        EditorGUILayout.HelpBox("確率が50%なら発生タイミングごとに約半分の確率で発言候補になります。抽選の重みは、同じタイミングで複数候補があるときに使います。連続表示はキャラ側DialogueBattleBarks2Dで抑制します。", MessageType.None);
        EditorGUILayout.HelpBox("性格・派閥などの条件を使用する場合、キャラにDialogueActor2Dも付けてください。Eキー操作はOFFのままでOKです。", MessageType.None);
        if (GUILayout.Button("通常会話のDialogue Studioへ戻る")) DialogueStudioWindow2D.Open();
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void AssignSelected()
    {
        if (selected == null) return;
        GameObject obj = Selection.activeGameObject;
        if (obj == null)
        {
            EditorUtility.DisplayDialog("キャラが未選択", "HierarchyのキャラクターかPrefabモード内のルートを選択してください。", "OK");
            return;
        }
        if (EditorUtility.IsPersistent(obj))
        {
            EditorUtility.DisplayDialog("Prefabを開いてください", "PrefabアセットをダブルクリックしてPrefabモードで開き、その中のキャラクターを選択してください。", "OK");
            return;
        }
        DialogueBattleBarks2D actor = obj.GetComponent<DialogueBattleBarks2D>();
        if (actor == null) actor = Undo.AddComponent<DialogueBattleBarks2D>(obj);
        Undo.RecordObject(actor, "戦闘セリフを割り当て");
        SerializedObject so = new SerializedObject(actor);
        so.FindProperty("barkData").objectReferenceValue = selected;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(actor);
        Selection.activeObject = actor.gameObject;
        Debug.Log("[Dialogue STEP3] " + obj.name + " に " + selected.name + " を割り当てました。", obj);
    }

    private void CreateAsset()
    {
        if (string.IsNullOrWhiteSpace(newName)) newName = "BattleBarks_New";
        if (saveFolder != "Assets" && !saveFolder.StartsWith("Assets/", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("保存先", "保存先はAssets以下にしてください。", "OK");
            return;
        }
        Directory.CreateDirectory(saveFolder);
        AssetDatabase.Refresh();
        string safe = string.Concat(newName.Split(Path.GetInvalidFileNameChars()));
        if (string.IsNullOrEmpty(safe)) safe = "BattleBarks_New";
        string path = AssetDatabase.GenerateUniqueAssetPath(saveFolder.TrimEnd('/') + "/" + safe + ".asset");
        DialogueBattleBarkData2D asset = CreateInstance<DialogueBattleBarkData2D>();
        asset.EditorInitialize(safe);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();
        selected = asset;
        NeedRefresh();
    }

    private void Duplicate()
    {
        if (selected == null) return;
        string oldPath = AssetDatabase.GetAssetPath(selected);
        string newPath = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(oldPath, null) + "_Copy.asset");
        if (!AssetDatabase.CopyAsset(oldPath, newPath)) return;
        AssetDatabase.SaveAssets();
        selected = AssetDatabase.LoadAssetAtPath<DialogueBattleBarkData2D>(newPath);
        NeedRefresh();
    }

    private void DeleteAsset()
    {
        if (selected == null) return;
        string path = AssetDatabase.GetAssetPath(selected);
        if (!EditorUtility.DisplayDialog("戦闘セリフデータ削除", path + "\nPrefabの参照が切れます。削除しますか？", "削除", "中止")) return;
        if (AssetDatabase.MoveAssetToTrash(path)) { selected = null; NeedRefresh(); }
    }

    private void RefreshAssets()
    {
        refreshNeeded = false;
        assets.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:DialogueBattleBarkData2D"))
        {
            var item = AssetDatabase.LoadAssetAtPath<DialogueBattleBarkData2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (item != null) assets.Add(item);
        }
        assets.Sort((a,b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
    }

    private static void Heading(string value)
    {
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        style.normal.textColor = Accent;
        GUILayout.Label(value, style);
    }
}
#endif
