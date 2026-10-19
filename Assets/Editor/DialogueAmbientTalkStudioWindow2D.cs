#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>STEP5 専用データ管理画面。通常会話・戦闘セリフを改変しません。</summary>
public sealed class DialogueAmbientTalkStudioWindow2D : EditorWindow
{
    [SerializeField] private DialogueAmbientTalkData2D selected;
    [SerializeField] private string nameInput = "AmbientTalk_New";
    [SerializeField] private string folder = "Assets/DialogueStudio2D/AmbientTalkData";
    [SerializeField] private string search = "";
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private readonly List<DialogueAmbientTalkData2D> assets = new List<DialogueAmbientTalkData2D>();
    private bool needRefresh = true;

    [MenuItem("Tools/2D Shooter/Dialogue Studio/STEP5 NPC・Enemy同士の会話")]
    public static void Open()
    {
        DialogueAmbientTalkStudioWindow2D window = GetWindow<DialogueAmbientTalkStudioWindow2D>("STEP5 立ち話管理");
        window.minSize = new Vector2(920f, 580f);
        window.Show();
    }

    public static void OpenWithData(DialogueAmbientTalkData2D data)
    {
        Open();
        DialogueAmbientTalkStudioWindow2D window = GetWindow<DialogueAmbientTalkStudioWindow2D>();
        if (data != null) window.selected = data;
        window.Repaint();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += OnProjectChanged;
        needRefresh = true;
    }
    private void OnDisable() { EditorApplication.projectChanged -= OnProjectChanged; }
    private void OnProjectChanged() { needRefresh = true; Repaint(); }

    private void Refresh()
    {
        assets.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:DialogueAmbientTalkData2D"))
        {
            DialogueAmbientTalkData2D asset = AssetDatabase.LoadAssetAtPath<DialogueAmbientTalkData2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) assets.Add(asset);
        }
        assets.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        needRefresh = false;
    }

    private void OnGUI()
    {
        if (needRefresh) Refresh();
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("DIALOGUE STUDIO 2D / STEP5  NPC・Enemy同士の会話", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("立ち話は戦闘中ではなく、非敵対のキャラクター同士が近くにいる時に始まります。戦闘・被弾・逃走・運搬で中断します。", MessageType.Info);
        EditorGUILayout.BeginHorizontal();
        DrawList();
        GUILayout.Space(8);
        DrawDetails();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawList()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(280));
        GUILayout.Label("01  会話データ一覧", EditorStyles.boldLabel);
        search = EditorGUILayout.TextField("検索", search);
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
        foreach (DialogueAmbientTalkData2D data in assets)
        {
            if (!string.IsNullOrEmpty(search) &&
                data.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                data.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            Color before = GUI.backgroundColor;
            if (selected == data) GUI.backgroundColor = new Color(.54f, .86f, .75f);
            if (GUILayout.Button(data.DisplayName + "\n" + data.name, GUILayout.Height(45))) selected = data;
            GUI.backgroundColor = before;
        }
        EditorGUILayout.EndScrollView();
        GUILayout.Label("02  新規作成・複製", EditorStyles.boldLabel);
        nameInput = EditorGUILayout.TextField("ファイル名", nameInput);
        folder = EditorGUILayout.TextField("保存フォルダ", folder);
        if (GUILayout.Button("新規データを作成", GUILayout.Height(29))) Create();
        using (new EditorGUI.DisabledScope(selected == null))
        {
            if (GUILayout.Button("選択データを複製")) Duplicate();
            if (GUILayout.Button("Projectで表示")) EditorGUIUtility.PingObject(selected);
            if (GUILayout.Button("削除（確認あり）")) Delete();
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawDetails()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Label("03  会話内容・条件", EditorStyles.boldLabel);
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        if (selected == null)
        {
            EditorGUILayout.HelpBox("左側で会話データを作成、または選択してください。", MessageType.Info);
        }
        else
        {
            SerializedObject so = new SerializedObject(selected);
            so.Update();
            EditorGUILayout.PropertyField(so.FindProperty("displayName"), new GUIContent("表示名"));
            SerializedProperty exchanges = so.FindProperty("exchanges");
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("会話パターン（複数ある場合は重み付きランダム）", EditorStyles.boldLabel);
            for (int i = 0; i < exchanges.arraySize; i++)
            {
                SerializedProperty e = exchanges.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                e.isExpanded = EditorGUILayout.Foldout(e.isExpanded,
                    "パターン " + (i + 1) + "：" + e.FindPropertyRelative("title").stringValue, true);
                if (GUILayout.Button("削除", GUILayout.Width(53)))
                {
                    exchanges.DeleteArrayElementAtIndex(i);
                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();
                    break;
                }
                EditorGUILayout.EndHorizontal();
                if (e.isExpanded)
                {
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("title"), new GUIContent("名前"));
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("weight"), new GUIContent("選択される重み"));
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("probability"), new GUIContent("抽選に参加する確率"));
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("requireInitiatorPersonality"), new GUIContent("開始側の性格を限定"));
                    if (e.FindPropertyRelative("requireInitiatorPersonality").boolValue)
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("initiatorPersonality"), new GUIContent("開始側の性格"));
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("requirePartnerPersonality"), new GUIContent("相手側の性格を限定"));
                    if (e.FindPropertyRelative("requirePartnerPersonality").boolValue)
                        EditorGUILayout.PropertyField(e.FindPropertyRelative("partnerPersonality"), new GUIContent("相手側の性格"));
                    EditorGUILayout.PropertyField(e.FindPropertyRelative("conditions"), new GUIContent("STEP2 条件（開始側）"), true);
                    SerializedProperty lines = e.FindPropertyRelative("lines");
                    EditorGUILayout.Space(5);
                    EditorGUILayout.LabelField("交互に表示するセリフ", EditorStyles.boldLabel);
                    for (int k = 0; k < lines.arraySize; k++)
                    {
                        SerializedProperty line = lines.GetArrayElementAtIndex(k);
                        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                        EditorGUILayout.BeginHorizontal();
                        EditorGUILayout.LabelField("セリフ " + (k + 1), GUILayout.Width(85));
                        if (GUILayout.Button("削除", GUILayout.Width(50)))
                        {
                            lines.DeleteArrayElementAtIndex(k);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();
                            break;
                        }
                        EditorGUILayout.EndHorizontal();
                        EditorGUILayout.PropertyField(line.FindPropertyRelative("speaker"), new GUIContent("話すキャラ"));
                        EditorGUILayout.PropertyField(line.FindPropertyRelative("text"), new GUIContent("セリフ"));
                        EditorGUILayout.PropertyField(line.FindPropertyRelative("displaySeconds"), new GUIContent("表示秒数"));
                        EditorGUILayout.PropertyField(line.FindPropertyRelative("pauseAfterSeconds"), new GUIContent("次のセリフまでの間"));
                        EditorGUILayout.EndVertical();
                    }
                    if (GUILayout.Button("＋ セリフを追加"))
                    {
                        lines.InsertArrayElementAtIndex(lines.arraySize);
                        SerializedProperty line = lines.GetArrayElementAtIndex(lines.arraySize - 1);
                        line.FindPropertyRelative("text").stringValue = "新しいセリフ";
                        line.FindPropertyRelative("displaySeconds").floatValue = 2.4f;
                        line.FindPropertyRelative("pauseAfterSeconds").floatValue = .35f;
                        line.FindPropertyRelative("speaker").enumValueIndex = lines.arraySize % 2 == 0 ? 1 : 0;
                    }
                }
                EditorGUILayout.EndVertical();
            }
            if (GUILayout.Button("＋ 会話パターンを追加", GUILayout.Height(29)))
            {
                exchanges.InsertArrayElementAtIndex(exchanges.arraySize);
                SerializedProperty e = exchanges.GetArrayElementAtIndex(exchanges.arraySize - 1);
                e.FindPropertyRelative("title").stringValue = "新しい会話";
                e.FindPropertyRelative("weight").intValue = 1;
                e.FindPropertyRelative("probability").floatValue = 1f;
                e.FindPropertyRelative("requireInitiatorPersonality").boolValue = false;
                e.FindPropertyRelative("requirePartnerPersonality").boolValue = false;
                e.FindPropertyRelative("conditions").ClearArray();
                e.FindPropertyRelative("lines").ClearArray();
                e.isExpanded = true;
            }
            if (so.ApplyModifiedProperties()) EditorUtility.SetDirty(selected);
            EditorGUILayout.Space(10);
            EditorGUILayout.HelpBox("Character Studio 2D > NPC / Enemy 会話 > STEP5 からPrefabへ割り当てます。まず近くの同じ派閥2体に設定して試してください。", MessageType.Info);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private string EnsureFolder()
    {
        string target = string.IsNullOrWhiteSpace(folder) || !folder.StartsWith("Assets")
            ? "Assets/DialogueStudio2D/AmbientTalkData" : folder.TrimEnd('/');
        if (!AssetDatabase.IsValidFolder(target))
        {
            string current = "Assets";
            foreach (string part in target.Substring("Assets".Length).Split('/'))
            {
                if (string.IsNullOrEmpty(part)) continue;
                string next = current + "/" + part;
                if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, part);
                current = next;
            }
        }
        return target;
    }

    private void Create()
    {
        string title = string.IsNullOrWhiteSpace(nameInput) ? "AmbientTalk_New" : nameInput.Trim();
        DialogueAmbientTalkData2D data = CreateInstance<DialogueAmbientTalkData2D>();
        data.EditorCreateDefault(title);
        string path = AssetDatabase.GenerateUniqueAssetPath(EnsureFolder() + "/" + title.Replace('/', '_') + ".asset");
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        selected = data;
        needRefresh = true;
        EditorGUIUtility.PingObject(data);
    }

    private void Duplicate()
    {
        if (selected == null) return;
        string source = AssetDatabase.GetAssetPath(selected);
        string dest = AssetDatabase.GenerateUniqueAssetPath(EnsureFolder() + "/" + selected.name + "_Copy.asset");
        if (!AssetDatabase.CopyAsset(source, dest)) return;
        AssetDatabase.Refresh();
        selected = AssetDatabase.LoadAssetAtPath<DialogueAmbientTalkData2D>(dest);
        needRefresh = true;
    }

    private void Delete()
    {
        if (selected == null || !EditorUtility.DisplayDialog("会話データを削除", "「" + selected.name + "」を削除しますか？Prefabの参照が失われる可能性があります。", "削除", "キャンセル")) return;
        string path = AssetDatabase.GetAssetPath(selected);
        selected = null;
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.Refresh();
        needRefresh = true;
    }
}
#endif
