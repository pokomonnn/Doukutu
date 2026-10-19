#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Character Studioとは独立した会話データ用エディター。</summary>
public sealed class DialogueStudioWindow2D : EditorWindow
{
    private static readonly Color HeaderColor = new Color(0.15f, 0.20f, 0.24f);
    private static readonly Color Accent = new Color(0.36f, 0.77f, 0.68f);

    private readonly List<DialogueData2D> dataList = new List<DialogueData2D>();
    [SerializeField] private DialogueData2D current;
    [SerializeField] private string search = "";
    [SerializeField] private string newAssetName = "Dialogue_New";
    [SerializeField] private string saveFolder = "Assets/DialogueStudio2D/DialogueData";
    [SerializeField] private int selectedNodeIndex;
    [SerializeField] private string previewNodeId = "";
    [SerializeField] private bool showPreview = true;
    private Vector2 leftScroll, rightScroll;
    private bool dirtyList = true;
    [SerializeField] private DialogueActor2D previewActor;

    [MenuItem("Tools/2D Shooter/Dialogue Studio - 会話管理")]
    public static void Open()
    {
        DialogueStudioWindow2D window = GetWindow<DialogueStudioWindow2D>("Dialogue Studio 2D");
        window.minSize = new Vector2(980, 640);
        window.Show();
    }

    public static void OpenWithData(DialogueData2D data)
    {
        Open();
        DialogueStudioWindow2D window = GetWindow<DialogueStudioWindow2D>();
        if (data != null) { window.current = data; window.selectedNodeIndex = 0; window.previewNodeId = data.StartNodeId; }
        window.Repaint();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += MarkDirty;
        MarkDirty();
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= MarkDirty;
    }

    private void MarkDirty() { dirtyList = true; Repaint(); }

    private void OnGUI()
    {
        if (dirtyList) Refresh();
        Rect header = GUILayoutUtility.GetRect(0, 65, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(header, HeaderColor);
        GUIStyle headline = new GUIStyle(EditorStyles.boldLabel) { fontSize = 21 };
        headline.normal.textColor = Color.white;
        GUI.Label(new Rect(header.x + 15, header.y + 6, header.width - 20, 32), "DIALOGUE STUDIO 2D", headline);
        GUI.Label(new Rect(header.x + 16, header.y + 39, header.width - 20, 19), "NPC / ENEMY  共通会話データ・条件分岐・選択肢", EditorStyles.miniLabel);
        EditorGUILayout.BeginHorizontal();
        DrawLeftPanel();
        GUILayout.Space(8);
        DrawRightPanel();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeftPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(310));
        GUILayout.Space(10);
        Heading("01  会話データ一覧");
        search = EditorGUILayout.TextField("検索", search);
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUILayout.ExpandHeight(true));
        foreach (DialogueData2D data in dataList)
        {
            if (data == null) continue;
            if (!string.IsNullOrWhiteSpace(search) &&
                data.name.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                data.DisplayName.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
            bool selected = current == data;
            Color old = GUI.backgroundColor;
            if (selected) GUI.backgroundColor = Accent;
            if (GUILayout.Button(data.DisplayName + "\n" + data.name, GUILayout.Height(44)))
            {
                current = data;
                selectedNodeIndex = 0;
                previewNodeId = data.StartNodeId;
            }
            GUI.backgroundColor = old;
        }
        EditorGUILayout.EndScrollView();
        GUILayout.Space(5);
        Heading("02  新規作成・管理");
        newAssetName = EditorGUILayout.TextField("ファイル名", newAssetName);
        saveFolder = EditorGUILayout.TextField("保存フォルダ", saveFolder);
        if (GUILayout.Button("新しい会話データを作成", GUILayout.Height(34))) CreateNew();
        using (new EditorGUI.DisabledScope(current == null))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("複製")) DuplicateCurrent();
            if (GUILayout.Button("Projectで表示")) EditorGUIUtility.PingObject(current);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("削除（確認あり）")) DeleteCurrent();
        }
        GUILayout.Space(7);
        GUILayout.Space(6);
        if (GUILayout.Button("STEP3 戦闘セリフ管理を開く", GUILayout.Height(31)))
            DialogueBattleBarkStudioWindow2D.Open();
        if (GUILayout.Button("STEP5 NPC・Enemy同士の会話管理を開く", GUILayout.Height(31)))
            DialogueAmbientTalkStudioWindow2D.Open();
        if (GUILayout.Button("テスト用の会話UIをシーンに作成")) DialogueStudioUICreator2D.CreateInScene();
        EditorGUILayout.HelpBox("既存の洞窟会話UIや会話データは変更しません。Eキー会話はPrefab側で有効にするまでOFFです。", MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private void DrawRightPanel()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Space(10);
        Heading("03  会話設定");
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        if (current == null)
        {
            EditorGUILayout.HelpBox("左側の会話データを選択するか、新規作成してください。", MessageType.Info);
        }
        else
        {
            DrawCurrentData();
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawCurrentData()
    {
        SerializedObject so = new SerializedObject(current);
        so.Update();
        EditorGUILayout.ObjectField("会話データ", current, typeof(DialogueData2D), false);
        EditorGUILayout.PropertyField(so.FindProperty("dialogueId"), new GUIContent("会話ID"));
        EditorGUILayout.PropertyField(so.FindProperty("displayName"), new GUIContent("表示名"));
        SerializedProperty nodes = so.FindProperty("nodes");
        string[] ids = GetIds(nodes);
        DrawNodeReference(so.FindProperty("startNodeId"), "通常の開始ノード（どの条件も成立しないとき）", ids);
        EditorGUILayout.HelpBox("条件付き開始ノードは上から順に評価します。カルマや友好度などが一致した最初の分岐を使用し、どれも一致しなければ通常開始ノードになります。", MessageType.Info);
        DrawRouteEditor(so.FindProperty("startRoutes"), "STEP2 条件による開始ノード分岐", ids);
        EditorGUILayout.Space(10);
        DrawStep4Actions(so.FindProperty("actions"));
        EditorGUILayout.Space(10);
        Heading("04  セリフ・選択肢");

        EditorGUILayout.BeginHorizontal();
        selectedNodeIndex = Mathf.Clamp(selectedNodeIndex, 0, Mathf.Max(0, nodes.arraySize - 1));
        string[] headers = new string[nodes.arraySize];
        for (int i = 0; i < headers.Length; i++)
        {
            SerializedProperty node = nodes.GetArrayElementAtIndex(i);
            headers[i] = (i + 1) + ": " + node.FindPropertyRelative("id").stringValue;
        }
        if (headers.Length > 0)
            selectedNodeIndex = EditorGUILayout.Popup("編集ノード", selectedNodeIndex, headers);
        else
            EditorGUILayout.LabelField("ノードがありません");
        if (GUILayout.Button("追加", GUILayout.Width(54)))
        {
            nodes.arraySize++;
            SerializedProperty node = nodes.GetArrayElementAtIndex(nodes.arraySize - 1);
            InitNode(node, UniqueId(ids));
            selectedNodeIndex = nodes.arraySize - 1;
        }
        EditorGUILayout.EndHorizontal();

        if (nodes.arraySize > 0)
        {
            selectedNodeIndex = Mathf.Clamp(selectedNodeIndex, 0, nodes.arraySize - 1);
            SerializedProperty node = nodes.GetArrayElementAtIndex(selectedNodeIndex);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("id"), new GUIContent("ノードID（重複不可）"));
            EditorGUILayout.PropertyField(node.FindPropertyRelative("speakerName"), new GUIContent("話者名（空欄なら会話表示名）"));
            EditorGUILayout.PropertyField(node.FindPropertyRelative("body"), new GUIContent("セリフ"), true);
            EditorGUILayout.PropertyField(node.FindPropertyRelative("onEnterEventId"), new GUIContent("表示時イベントID"));
            EditorGUILayout.Space(5);
            SerializedProperty choices = node.FindPropertyRelative("choices");
            if (choices.arraySize == 0)
            {
                DrawNodeReference(node.FindPropertyRelative("nextNodeId"), "通常の次ノード（空欄で終了）", ids);
                DrawRouteEditor(node.FindPropertyRelative("nextRoutes"), "STEP2 条件付き次ノード", ids);
            }
            else
                EditorGUILayout.HelpBox("通常の次ノードは、表示可能な選択肢が0件の場合だけ使用します。次ノードの条件分岐も同じ扱いです。", MessageType.None);
            Heading("選択肢 " + choices.arraySize + " 件");
            int remove = -1;
            for (int i = 0; i < choices.arraySize; i++)
            {
                SerializedProperty choice = choices.GetArrayElementAtIndex(i);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("選択肢 " + (i + 1), EditorStyles.boldLabel);
                if (GUILayout.Button("削除", GUILayout.Width(48))) remove = i;
                EditorGUILayout.EndHorizontal();
                EditorGUILayout.PropertyField(choice.FindPropertyRelative("label"), new GUIContent("表示文字"));
                DrawNodeReference(choice.FindPropertyRelative("nextNodeId"), "遷移先（空欄で終了）", ids);
                EditorGUILayout.PropertyField(choice.FindPropertyRelative("eventId"), new GUIContent("選択時イベントID"));
                DrawConditionsEditor(choice.FindPropertyRelative("conditions"), "表示する条件（0件なら常時表示）");
                EditorGUILayout.EndVertical();
            }
            if (remove >= 0) choices.DeleteArrayElementAtIndex(remove);
            if (GUILayout.Button("＋ 選択肢を追加"))
            {
                choices.arraySize++;
                SerializedProperty choice = choices.GetArrayElementAtIndex(choices.arraySize - 1);
                choice.FindPropertyRelative("label").stringValue = "新しい選択肢";
                choice.FindPropertyRelative("nextNodeId").stringValue = "";
                choice.FindPropertyRelative("eventId").stringValue = "";
                choice.FindPropertyRelative("conditions").ClearArray();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("このノードを複製"))
            {
                nodes.InsertArrayElementAtIndex(selectedNodeIndex);
                selectedNodeIndex++;
                nodes.GetArrayElementAtIndex(selectedNodeIndex).FindPropertyRelative("id").stringValue = UniqueId(ids);
            }
            using (new EditorGUI.DisabledScope(nodes.arraySize <= 1))
                if (GUILayout.Button("このノードを削除"))
                {
                    if (EditorUtility.DisplayDialog("ノード削除", "参照している遷移先が切れる可能性があります。削除しますか？", "削除", "中止"))
                    {
                        nodes.DeleteArrayElementAtIndex(selectedNodeIndex);
                        selectedNodeIndex = Mathf.Max(0, selectedNodeIndex - 1);
                    }
                }
            EditorGUILayout.EndHorizontal();
        }

        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(current);
            AssetDatabase.SaveAssets();
        }
        EditorGUILayout.Space(12);
        DrawValidation();
        EditorGUILayout.Space(10);
        showPreview = EditorGUILayout.Foldout(showPreview, "05  会話プレビュー（エディター内）", true);
        if (showPreview) DrawPreview();
    }

    private void DrawStep4Actions(SerializedProperty actions)
    {
        Heading("STEP4 取引・ミッション・アイテム受け渡し");
        EditorGUILayout.HelpBox("選択肢の『選択時イベントID』とアクションIDを一致させてください。アイテム付与や受注はノード表示時イベントから実行しません。", MessageType.Info);
        if (actions == null) return;
        string[] kinds = { "ショップを開く", "ミッションを受注", "納品ミッションを報告", "プレイヤーにアイテムを渡す", "プレイヤーからアイテムを受け取る" };
        int remove = -1;
        HashSet<string> seen = new HashSet<string>();
        for (int i = 0; i < actions.arraySize; i++)
        {
            SerializedProperty action = actions.GetArrayElementAtIndex(i);
            SerializedProperty id = action.FindPropertyRelative("eventId");
            SerializedProperty kind = action.FindPropertyRelative("kind");
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("アクション " + (i + 1), EditorStyles.boldLabel);
            if (GUILayout.Button("削除", GUILayout.Width(48))) remove = i;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(id, new GUIContent("イベントID（選択肢と一致）"));
            kind.enumValueIndex = EditorGUILayout.Popup("実行内容", kind.enumValueIndex, kinds);
            switch ((DialogueActionKind2D)kind.enumValueIndex)
            {
                case DialogueActionKind2D.StartMission:
                case DialogueActionKind2D.DeliverMissionItems:
                    EditorGUILayout.PropertyField(action.FindPropertyRelative("missionIndex"), new GUIContent("MissionManagerのIndex（0から）"));
                    break;
                case DialogueActionKind2D.GiveItemToPlayer:
                case DialogueActionKind2D.TakeItemFromPlayer:
                    EditorGUILayout.PropertyField(action.FindPropertyRelative("item"), new GUIContent("ItemData"));
                    EditorGUILayout.PropertyField(action.FindPropertyRelative("amount"), new GUIContent("個数"));
                    break;
            }
            if (kind.enumValueIndex != (int)DialogueActionKind2D.OpenShop)
                EditorGUILayout.PropertyField(action.FindPropertyRelative("oncePerRuntime"), new GUIContent("Play中の再実行を禁止"));
            else
                EditorGUILayout.HelpBox("ショップは何度でも開けます。", MessageType.None);
            if (string.IsNullOrWhiteSpace(id.stringValue) || !seen.Add(id.stringValue))
                EditorGUILayout.HelpBox("イベントIDが空欄または重複しています。", MessageType.Warning);
            if (kind.enumValueIndex == (int)DialogueActionKind2D.GiveItemToPlayer)
                EditorGUILayout.HelpBox("重要：報酬の再実行ガードはPlay中のみ有効です。シーン再読み込み/セーブロード後の重複配布対策は別途セーブ連携が必要です。", MessageType.Warning);
            EditorGUILayout.EndVertical();
        }
        if (remove >= 0) actions.DeleteArrayElementAtIndex(remove);
        if (GUILayout.Button("＋ STEP4 アクションを追加"))
        {
            actions.arraySize++;
            SerializedProperty action = actions.GetArrayElementAtIndex(actions.arraySize - 1);
            action.FindPropertyRelative("eventId").stringValue = "action_" + Guid.NewGuid().ToString("N").Substring(0, 6);
            action.FindPropertyRelative("kind").enumValueIndex = 0;
            action.FindPropertyRelative("missionIndex").intValue = 0;
            action.FindPropertyRelative("item").objectReferenceValue = null;
            action.FindPropertyRelative("amount").intValue = 1;
            action.FindPropertyRelative("oncePerRuntime").boolValue = true;
        }
    }

    private void DrawNodeReference(SerializedProperty property, string label, string[] ids)
    {
        // 未入力や削除済みのIDも表示し、誤って自動修正しません。
        string currentId = property.stringValue;
        string[] options = new string[ids.Length + 1];
        options[0] = "（終了 / なし）";
        Array.Copy(ids, 0, options, 1, ids.Length);
        int index = Array.IndexOf(options, currentId);
        if (string.IsNullOrEmpty(currentId)) index = 0;
        else index = Array.IndexOf(ids, currentId) + 1;
        if (!string.IsNullOrEmpty(currentId) && index == 0)
            EditorGUILayout.HelpBox("参照先IDがありません: " + currentId, MessageType.Warning);
        int chosen = EditorGUILayout.Popup(label, index, options);
        if (chosen != index) property.stringValue = chosen == 0 ? "" : options[chosen];
    }

    private static string[] GetIds(SerializedProperty nodes)
    {
        string[] ids = new string[nodes.arraySize];
        for (int i = 0; i < ids.Length; i++)
            ids[i] = nodes.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue;
        return ids;
    }

    private static string UniqueId(string[] existing)
    {
        string id;
        do { id = "node_" + Guid.NewGuid().ToString("N").Substring(0, 6); }
        while (Array.IndexOf(existing, id) >= 0);
        return id;
    }

    private static void InitNode(SerializedProperty node, string id)
    {
        node.FindPropertyRelative("id").stringValue = id;
        node.FindPropertyRelative("speakerName").stringValue = "NPC";
        node.FindPropertyRelative("body").stringValue = "新しいセリフ";
        node.FindPropertyRelative("nextNodeId").stringValue = "";
        node.FindPropertyRelative("onEnterEventId").stringValue = "";
        node.FindPropertyRelative("choices").ClearArray();
        node.FindPropertyRelative("nextRoutes").ClearArray();
    }

    private void DrawValidation()
    {
        HashSet<string> ids = new HashSet<string>();
        HashSet<string> duplicates = new HashSet<string>();
        foreach (DialogueNode2D node in current.Nodes)
            if (node != null && !ids.Add(node.Id)) duplicates.Add(node.Id);
        foreach (string id in duplicates)
            EditorGUILayout.HelpBox("重複したノードID: " + id, MessageType.Error);
        if (!ids.Contains(current.StartNodeId))
            EditorGUILayout.HelpBox("開始ノードが存在しません: " + current.StartNodeId, MessageType.Error);
        ValidateRoutes(current.StartRoutes, ids, "開始分岐");
        foreach (DialogueNode2D node in current.Nodes)
        {
            if (node == null) continue;
            ValidateRoutes(node.NextRoutes, ids, "ノード " + node.Id + " の次ノード分岐");
            if (string.IsNullOrWhiteSpace(node.Id))
                EditorGUILayout.HelpBox("空のノードIDがあります", MessageType.Error);
            if (!string.IsNullOrEmpty(node.NextNodeId) && !ids.Contains(node.NextNodeId))
                EditorGUILayout.HelpBox(node.Id + " の次ノードが見つかりません: " + node.NextNodeId, MessageType.Warning);
            if (node.Choices == null) continue;
            foreach (DialogueChoice2D choice in node.Choices)
            {
                if (choice == null) continue;
                if (!string.IsNullOrEmpty(choice.NextNodeId) && !ids.Contains(choice.NextNodeId))
                    EditorGUILayout.HelpBox(node.Id + " の選択肢が不明なIDを参照: " + choice.NextNodeId, MessageType.Warning);
                ValidateConditions(choice.Conditions, node.Id + " の選択肢");
            }
        }
    }

    private void DrawPreview()
    {
        previewActor = (DialogueActor2D)EditorGUILayout.ObjectField("判定するNPC / Enemy", previewActor, typeof(DialogueActor2D), true);
        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("カルマ・友好度などの実際の条件判定はPlayモードで行ってください。話者を指定しない場合、話者条件は不成立です。", MessageType.Info);
        if (GUILayout.Button("開始位置からプレビュー"))
            previewNodeId = current.ResolveStartNodeId(previewActor);

        DialogueNode2D node = current.FindNode(previewNodeId);
        if (node == null)
        {
            previewNodeId = current.ResolveStartNodeId(previewActor);
            node = current.FindNode(previewNodeId);
        }
        if (node == null) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("ノード: " + node.Id + " / 話者: " +
            (string.IsNullOrWhiteSpace(node.SpeakerName) ? current.DisplayName : node.SpeakerName), EditorStyles.boldLabel);
        EditorGUILayout.LabelField(node.Body, EditorStyles.wordWrappedLabel, GUILayout.MinHeight(36));
        int visible = 0;
        if (node.Choices != null)
        {
            foreach (DialogueChoice2D choice in node.Choices)
            {
                if (choice == null || !DialogueConditions2D.AllMatch(choice.Conditions, previewActor)) continue;
                visible++;
                if (GUILayout.Button("▶ " + choice.Label)) previewNodeId = choice.NextNodeId;
            }
        }
        if (visible == 0)
        {
            string next = node.ResolveNextNodeId(previewActor);
            if (GUILayout.Button(string.IsNullOrEmpty(next) ? "会話終了 / 最初から" : "次へ →"))
                previewNodeId = string.IsNullOrEmpty(next) ? current.ResolveStartNodeId(previewActor) : next;
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawRouteEditor(SerializedProperty routes, string title, string[] ids)
    {
        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        int remove = -1, move = -1, direction = 0;
        for (int i = 0; i < routes.arraySize; i++)
        {
            SerializedProperty route = routes.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("優先順位 " + (i + 1), EditorStyles.boldLabel);
            using (new EditorGUI.DisabledScope(i == 0))
                if (GUILayout.Button("↑", GUILayout.Width(30))) { move = i; direction = -1; }
            using (new EditorGUI.DisabledScope(i == routes.arraySize - 1))
                if (GUILayout.Button("↓", GUILayout.Width(30))) { move = i; direction = 1; }
            if (GUILayout.Button("削除", GUILayout.Width(48))) remove = i;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.PropertyField(route.FindPropertyRelative("label"), new GUIContent("メモ"));
            DrawNodeReference(route.FindPropertyRelative("targetNodeId"), "成立したときのノード", ids);
            DrawConditionsEditor(route.FindPropertyRelative("conditions"), "必要な条件（すべて成立）");
            EditorGUILayout.EndVertical();
        }
        if (move >= 0) routes.MoveArrayElement(move, move + direction);
        if (remove >= 0) routes.DeleteArrayElementAtIndex(remove);
        if (GUILayout.Button("＋ 条件分岐を追加"))
        {
            routes.arraySize++;
            SerializedProperty route = routes.GetArrayElementAtIndex(routes.arraySize - 1);
            route.FindPropertyRelative("label").stringValue = "新しい条件分岐";
            route.FindPropertyRelative("targetNodeId").stringValue = "";
            route.FindPropertyRelative("conditions").ClearArray();
        }
    }

    private void DrawConditionsEditor(SerializedProperty conditions, string title)
    {
        EditorGUILayout.LabelField(title, EditorStyles.miniBoldLabel);
        int remove = -1;
        for (int i = 0; i < conditions.arraySize; i++)
        {
            SerializedProperty condition = conditions.GetArrayElementAtIndex(i);
            SerializedProperty kind = condition.FindPropertyRelative("kind");
            DialogueConditionKind2D type = (DialogueConditionKind2D)kind.enumValueIndex;
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.PropertyField(kind, new GUIContent("条件 " + (i + 1)));
            if (GUILayout.Button("×", GUILayout.Width(26))) remove = i;
            EditorGUILayout.EndHorizontal();
            switch (type)
            {
                case DialogueConditionKind2D.KarmaAtLeast:
                case DialogueConditionKind2D.KarmaAtMost:
                    EditorGUILayout.PropertyField(condition.FindPropertyRelative("threshold"), new GUIContent("カルマ値"));
                    break;
                case DialogueConditionKind2D.ReputationAtLeast:
                case DialogueConditionKind2D.ReputationAtMost:
                    DrawFactionField(condition.FindPropertyRelative("factionId"), "対象の派閥ID");
                    EditorGUILayout.PropertyField(condition.FindPropertyRelative("threshold"), new GUIContent("友好度"));
                    break;
                case DialogueConditionKind2D.ActorFactionIs:
                    DrawFactionField(condition.FindPropertyRelative("factionId"), "話者の派閥ID");
                    break;
                case DialogueConditionKind2D.ActorPersonalityIs:
                    EditorGUILayout.PropertyField(condition.FindPropertyRelative("personality"), new GUIContent("話者の性格"));
                    break;
            }
            EditorGUILayout.EndVertical();
        }
        if (remove >= 0) conditions.DeleteArrayElementAtIndex(remove);
        if (GUILayout.Button("＋ 判定条件を追加", EditorStyles.miniButton))
        {
            conditions.arraySize++;
            SerializedProperty condition = conditions.GetArrayElementAtIndex(conditions.arraySize - 1);
            condition.FindPropertyRelative("kind").enumValueIndex = 0;
            condition.FindPropertyRelative("threshold").intValue = 0;
            condition.FindPropertyRelative("factionId").stringValue = "bandits";
            condition.FindPropertyRelative("personality").enumValueIndex = 0;
        }
        if (conditions.arraySize == 0)
            EditorGUILayout.LabelField("条件なし：常に成立", EditorStyles.miniLabel);
    }

    private static void DrawFactionField(SerializedProperty faction, string label)
    {
        EditorGUILayout.PropertyField(faction, new GUIContent(label));
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("クイック選択", GUILayout.Width(80));
        if (GUILayout.Button("モンスター", EditorStyles.miniButton)) faction.stringValue = "monsters";
        if (GUILayout.Button("盗賊", EditorStyles.miniButton)) faction.stringValue = "bandits";
        if (GUILayout.Button("冒険者", EditorStyles.miniButton)) faction.stringValue = "adventurers";
        if (GUILayout.Button("ドワーフ", EditorStyles.miniButton)) faction.stringValue = "dwarves";
        EditorGUILayout.EndHorizontal();
    }

    private void ValidateRoutes(IReadOnlyList<DialogueConditionalRoute2D> routes, HashSet<string> ids, string location)
    {
        if (routes == null) return;
        for (int i = 0; i < routes.Count; i++)
        {
            DialogueConditionalRoute2D route = routes[i];
            if (route == null) continue;
            if (string.IsNullOrEmpty(route.targetNodeId))
                EditorGUILayout.HelpBox(location + " の条件 " + (i + 1) + " の遷移先が空欄（会話終了）", MessageType.Warning);
            else if (!ids.Contains(route.targetNodeId))
                EditorGUILayout.HelpBox(location + " が存在しないノードを参照: " + route.targetNodeId, MessageType.Error);
            if (route.conditions == null || route.conditions.Count == 0)
                EditorGUILayout.HelpBox(location + " の条件 " + (i + 1) + " が0件のため常に成立します。", MessageType.Warning);
            ValidateConditions(route.conditions, location + " の分岐 " + (i + 1));
        }
    }

    private void ValidateConditions(IReadOnlyList<DialogueCondition2D> conditions, string location)
    {
        if (conditions == null) return;
        for (int i = 0; i < conditions.Count; i++)
        {
            DialogueCondition2D c = conditions[i];
            if (c == null) continue;
            if ((c.kind == DialogueConditionKind2D.ReputationAtLeast ||
                 c.kind == DialogueConditionKind2D.ReputationAtMost ||
                 c.kind == DialogueConditionKind2D.ActorFactionIs) &&
                string.IsNullOrWhiteSpace(c.factionId))
                EditorGUILayout.HelpBox(location + " の派閥IDが空欄です。", MessageType.Warning);
        }
    }

    private void CreateNew()
    {
        if (string.IsNullOrWhiteSpace(newAssetName)) newAssetName = "Dialogue_New";
        if (!saveFolder.StartsWith("Assets/", StringComparison.Ordinal) && saveFolder != "Assets")
        {
            EditorUtility.DisplayDialog("保存場所", "保存先はAssets以下にしてください。", "OK");
            return;
        }
        Directory.CreateDirectory(saveFolder);
        AssetDatabase.Refresh();
        string safeName = string.Concat(newAssetName.Split(Path.GetInvalidFileNameChars()));
        string path = AssetDatabase.GenerateUniqueAssetPath(saveFolder + "/" + safeName + ".asset");
        DialogueData2D data = CreateInstance<DialogueData2D>();
        data.EditorInitialize("dialogue_" + Guid.NewGuid().ToString("N").Substring(0, 10), safeName);
        AssetDatabase.CreateAsset(data, path);
        AssetDatabase.SaveAssets();
        current = data;
        selectedNodeIndex = 0;
        previewNodeId = data.StartNodeId;
        MarkDirty();
    }

    private void DuplicateCurrent()
    {
        if (current == null) return;
        string from = AssetDatabase.GetAssetPath(current);
        string to = AssetDatabase.GenerateUniqueAssetPath(Path.ChangeExtension(from, null) + "_Copy.asset");
        if (!AssetDatabase.CopyAsset(from, to)) { Debug.LogError("会話データ複製に失敗しました: " + to); return; }
        DialogueData2D copy = AssetDatabase.LoadAssetAtPath<DialogueData2D>(to);
        if (copy != null)
        {
            SerializedObject so = new SerializedObject(copy);
            so.FindProperty("dialogueId").stringValue = "dialogue_" + Guid.NewGuid().ToString("N").Substring(0, 10);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(copy);
        }
        AssetDatabase.SaveAssets();
        current = copy;
        previewNodeId = copy != null ? copy.StartNodeId : "";
        selectedNodeIndex = 0;
        MarkDirty();
    }

    private void DeleteCurrent()
    {
        if (current == null) return;
        string path = AssetDatabase.GetAssetPath(current);
        if (!EditorUtility.DisplayDialog("会話データ削除", path + "\n\nPrefabから参照されている場合、参照が切れます。削除しますか？", "削除", "中止")) return;
        if (AssetDatabase.MoveAssetToTrash(path))
        {
            current = null;
            MarkDirty();
        }
    }

    private void Refresh()
    {
        dirtyList = false;
        dataList.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:DialogueData2D"))
        {
            DialogueData2D data = AssetDatabase.LoadAssetAtPath<DialogueData2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (data != null) dataList.Add(data);
        }
        dataList.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
    }

    private static void Heading(string s)
    {
        GUIStyle style = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        style.normal.textColor = Accent;
        GUILayout.Label(s, style);
    }
}
#endif
