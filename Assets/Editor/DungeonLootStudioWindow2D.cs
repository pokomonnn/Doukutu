#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Scene単位のItemBox抽選設定、Prefabへの識別マーカー設置を1タブに統合。</summary>
public sealed class DungeonLootStudioWindow2D : EditorWindow
{
    private const string ProfileFolder = "Assets/Resources/DungeonLootProfiles";
    private SceneAsset selectedScene;
    private DungeonLootStageProfile2D profile;
    private int selectedBoxIndex = -1;
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string warning = "";
    private DefaultAsset itemFolder;
    private DefaultAsset boxFolder;
    private int newItemWeight = 10;
    private int newItemMinAmount = 1;
    private int newItemMaxAmount = 1;

    private readonly Color accent = new Color(0.27f, 0.58f, 0.49f);

    [MenuItem("Tools/2D Shooter/Dungeon Loot Studio - ItemBoxランダム生成")]
    private static void OpenWindow()
    {
        var window = GetWindow<DungeonLootStudioWindow2D>("Dungeon Loot Studio");
        window.minSize = new Vector2(940f, 550f);
        window.Show();
    }

    private void OnEnable()
    {
        if (selectedScene == null)
        {
            string path = SceneManager.GetActiveScene().path;
            if (!string.IsNullOrEmpty(path))
                selectedScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
        }
        FindExistingProfile();
    }

    private void OnGUI()
    {
        DrawHeader();
        EditorGUILayout.Space(8);
        using (new EditorGUILayout.HorizontalScope())
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Min(320, position.width * .35f))))
            {
                leftScroll = EditorGUILayout.BeginScrollView(leftScroll);
                DrawScenePanel();
                DrawBoxList();
                EditorGUILayout.EndScrollView();
            }
            using (new EditorGUILayout.VerticalScope(GUILayout.ExpandWidth(true)))
            {
                rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
                DrawSettings();
                EditorGUILayout.EndScrollView();
            }
        }
        EditorGUILayout.Space(5);
        using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
        {
            GUILayout.Label("Prefabは編集時に設定 / Itemの生成はPlay中にSceneへ入った時", EditorStyles.miniLabel);
            GUILayout.FlexibleSpace();
            GUILayout.Label("v1.1  |  Bulk Drag & Drop", EditorStyles.miniLabel);
        }
    }

    private void DrawHeader()
    {
        Rect rect = GUILayoutUtility.GetRect(1f, 57f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(.12f,.18f,.21f));
        Rect bar = new Rect(rect.x, rect.y, 5, rect.height);
        EditorGUI.DrawRect(bar, accent);
        var title = new GUIStyle(EditorStyles.boldLabel) { fontSize = 17, normal = { textColor = Color.white } };
        var sub = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(.75f,.83f,.84f) } };
        GUI.Label(new Rect(rect.x+16,rect.y+8,rect.width-32,26), "DUNGEON LOOT STUDIO 2D", title);
        GUI.Label(new Rect(rect.x+16,rect.y+35,rect.width-32,18), "ステージ / Item候補 / ItemBoxプレハブ / 再入場時の抽選", sub);
    }

    private void DrawScenePanel()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("01  ステージを選択", EditorStyles.boldLabel);
            EditorGUI.BeginChangeCheck();
            selectedScene = (SceneAsset)EditorGUILayout.ObjectField("対象Scene", selectedScene, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                selectedBoxIndex = -1;
                FindExistingProfile();
            }
            string sceneName = selectedScene != null ? selectedScene.name : "未選択";
            EditorGUILayout.HelpBox("Scene: " + sceneName, MessageType.None);
            if (profile == null)
            {
                using (new EditorGUI.DisabledScope(selectedScene == null))
                {
                    if (GUILayout.Button("＋ このScene用Loot設定を作成", GUILayout.Height(30))) CreateProfile();
                }
            }
            else
            {
                EditorGUILayout.ObjectField("設定Asset", profile, typeof(DungeonLootStageProfile2D), false);
                if (GUILayout.Button("設定AssetをProjectで表示")) EditorGUIUtility.PingObject(profile);
            }
        }
    }

    private void DrawBoxList()
    {
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("02  生成対象ItemBox", EditorStyles.boldLabel);
            if (profile == null)
            {
                EditorGUILayout.HelpBox("先にステージのLoot設定を作成してください。", MessageType.Info);
                return;
            }
            EditorGUILayout.LabelField("同じPrefabの複数の箱も、それぞれ別々に抽選します。", EditorStyles.wordWrappedMiniLabel);
            if (profile.boxes != null)
            {
                for (int i = 0; i < profile.boxes.Count; i++)
                {
                    DungeonLootStageProfile2D.BoxRule rule = profile.boxes[i];
                    if (rule == null) continue;
                    string name = rule.itemBoxPrefab != null ? rule.itemBoxPrefab.name : "Prefab未指定";
                    string caption = (rule.enabled ? "● " : "○ ") + name;
                    GUIStyle style = i == selectedBoxIndex ? EditorStyles.miniButtonMid : EditorStyles.miniButton;
                    if (GUILayout.Button(caption, style, GUILayout.Height(30))) selectedBoxIndex = i;
                }
            }
            EditorGUILayout.Space(5);
            DrawBulkBoxImportUI();
            EditorGUILayout.Space(5);
            if (GUILayout.Button("＋ ItemBoxプレハブを追加", GUILayout.Height(27)))
            {
                Undo.RecordObject(profile,"Add dungeon loot box");
                profile.boxes.Add(new DungeonLootStageProfile2D.BoxRule { prefabKey = Guid.NewGuid().ToString("N") });
                selectedBoxIndex = profile.boxes.Count - 1;
                MarkProfile();
            }
            EditorGUILayout.Space(5);
            if (GUILayout.Button("対象Prefabへマーカーを一括設定", GUILayout.Height(26))) ApplyMarkers();
            if (GUILayout.Button("現在のSceneで箱の保存ID重複を修復")) RepairDuplicatePersistentIds();
            if (!string.IsNullOrEmpty(warning)) EditorGUILayout.HelpBox(warning, MessageType.Info);
        }
    }

    private void DrawSettings()
    {
        if (profile == null)
        {
            EditorGUILayout.HelpBox("左側でステージを選び、Loot設定を作成してください。", MessageType.Info);
            return;
        }
        SerializedObject so = new SerializedObject(profile);
        so.Update();
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("03  ステージ共通のItem候補", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("登録Itemは抽選候補です。Weightが大きいほど出やすく、0なら出ません。",MessageType.None);
            EditorGUILayout.PropertyField(so.FindProperty("sceneName"), new GUIContent("Scene名"));
            DrawBulkItemImportUI(-1);
            DrawItemList(so.FindProperty("stageItems"));
        }
        if (selectedBoxIndex >= 0 && selectedBoxIndex < profile.boxes.Count)
        {
            SerializedProperty rule = so.FindProperty("boxes").GetArrayElementAtIndex(selectedBoxIndex);
            using (new EditorGUILayout.VerticalScope("box"))
            {
                EditorGUILayout.LabelField("04  選択中のItemBox設定", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("itemBoxPrefab"),new GUIContent("対象Prefab"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("enabled"),new GUIContent("このStageで生成する"));
                EditorGUILayout.Space(5);
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("emptyChancePercent"),new GUIContent("空箱になる確率(%)"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("minDraws"),new GUIContent("最小抽選回数"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("maxDraws"),new GUIContent("最大抽選回数"));
                EditorGUILayout.PropertyField(rule.FindPropertyRelative("useOwnItemTable"),new GUIContent("この箱だけ独自Item候補"));
                if (rule.FindPropertyRelative("useOwnItemTable").boolValue)
                {
                    EditorGUILayout.Space(6);
                    EditorGUILayout.LabelField("このItemBox専用のItem候補", EditorStyles.boldLabel);
                    DrawBulkItemImportUI(selectedBoxIndex);
                    DrawItemList(rule.FindPropertyRelative("ownItemTable"));
                }
                EditorGUILayout.Space(8);
                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button("このPrefabへマーカーを設定",GUILayout.Height(29)))
                    {
                        so.ApplyModifiedProperties();
                        ApplyMarker(profile.boxes[selectedBoxIndex]);
                        MarkProfile();
                        GUIUtility.ExitGUI();
                    }
                    if (GUILayout.Button("このBox設定を削除",GUILayout.Height(29)))
                    {
                        so.ApplyModifiedProperties();
                        if (EditorUtility.DisplayDialog("確認", "ステージの対象リストからこの箱を除外しますか？Prefab自体は削除されません。", "除外", "キャンセル"))
                        {
                            Undo.RecordObject(profile,"Remove loot box rule");
                            profile.boxes.RemoveAt(selectedBoxIndex);
                            selectedBoxIndex = -1;
                            MarkProfile();
                        }
                        GUIUtility.ExitGUI();
                    }
                }
            }
        }
        using (new EditorGUILayout.VerticalScope("box"))
        {
            EditorGUILayout.LabelField("05  確認と保存", EditorStyles.boldLabel);
            if (GUILayout.Button("設定を保存 ＋ Prefabへマーカーを適用",GUILayout.Height(36)))
            {
                so.ApplyModifiedProperties();
                NormalizeProfile();
                ApplyMarkers();
                MarkProfile();
                warning = "設定を保存しました。Play中にこのSceneへ入るとItemBoxへ新しいItemを抽選します。";
                GUIUtility.ExitGUI();
            }
            EditorGUILayout.HelpBox("通常の箱は操作しません。指定Prefabと、設定したScene名の両方が一致する場合だけ生成します。", MessageType.Info);
        }
        if (so.ApplyModifiedProperties()) MarkProfile();
    }

    private void DrawItemList(SerializedProperty list)
    {
        if (list == null) return;
        int removeAt = -1;
        for (int i = 0; i < list.arraySize; i++)
        {
            SerializedProperty row = list.GetArrayElementAtIndex(i);
            SerializedProperty item = row.FindPropertyRelative("item");
            SerializedProperty weight = row.FindPropertyRelative("weight");
            SerializedProperty min = row.FindPropertyRelative("minAmount");
            SerializedProperty max = row.FindPropertyRelative("maxAmount");
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    ItemData data = item.objectReferenceValue as ItemData;
                    GUILayout.Label(data != null && data.Icon != null ? AssetPreview.GetMiniThumbnail(data) : EditorGUIUtility.IconContent("Prefab Icon").image, GUILayout.Width(34), GUILayout.Height(34));
                    EditorGUILayout.PropertyField(item, GUIContent.none, GUILayout.MinWidth(80));
                    if (GUILayout.Button("×",GUILayout.Width(25))) removeAt = i;
                }
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(weight,new GUIContent("重み"));
                    EditorGUILayout.PropertyField(min,new GUIContent("個数最小"));
                    EditorGUILayout.PropertyField(max,new GUIContent("最大"));
                }
            }
        }
        if (removeAt >= 0) list.DeleteArrayElementAtIndex(removeAt);
        if (GUILayout.Button("＋ Item候補を追加"))
        {
            int i = list.arraySize;
            list.InsertArrayElementAtIndex(i);
            SerializedProperty row = list.GetArrayElementAtIndex(i);
            row.FindPropertyRelative("item").objectReferenceValue = null;
            row.FindPropertyRelative("weight").intValue = 10;
            row.FindPropertyRelative("minAmount").intValue = 1;
            row.FindPropertyRelative("maxAmount").intValue = 1;
        }
    }


    // ---------- 一括ドラッグ＆ドロップ ----------

    private void DrawBulkBoxImportUI()
    {
        DrawDropArea("ここにItemBoxプレハブ / フォルダをまとめてドロップ", true, -1);
        if (GUILayout.Button("Projectで選択中のItemBoxプレハブを一括追加", GUILayout.Height(25)))
        {
            ImportBoxObjects(Selection.objects);
            GUIUtility.ExitGUI();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            boxFolder = (DefaultAsset)EditorGUILayout.ObjectField("Prefabフォルダ", boxFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("一括登録", GUILayout.Width(80)))
            {
                ImportBoxFolder();
                GUIUtility.ExitGUI();
            }
        }
        EditorGUILayout.LabelField("重複は自動スキップ。登録後に『設定を保存＋Prefabへマーカーを適用』を押してください。", EditorStyles.wordWrappedMiniLabel);
    }

    private void DrawBulkItemImportUI(int boxIndex)
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("まとめてItem候補を登録", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("ItemData.assetを複数ドラッグ、またはそのフォルダをドロップできます。ItemData参照が1個だけあるPrefabも対応。重複Itemは既存の重みと個数を保持します。", MessageType.None);
        using (new EditorGUILayout.HorizontalScope())
        {
            newItemWeight = EditorGUILayout.IntField("新規Itemの重み", newItemWeight, GUILayout.MinWidth(90));
            newItemMinAmount = EditorGUILayout.IntField("最小個数", newItemMinAmount, GUILayout.MinWidth(80));
            newItemMaxAmount = EditorGUILayout.IntField("最大個数", newItemMaxAmount, GUILayout.MinWidth(80));
        }
        newItemWeight = Mathf.Max(0, newItemWeight);
        newItemMinAmount = Mathf.Max(1, newItemMinAmount);
        newItemMaxAmount = Mathf.Max(newItemMinAmount, newItemMaxAmount);
        DrawDropArea("ItemData / Itemプレハブ / フォルダをまとめてドロップ", false, boxIndex);
        if (GUILayout.Button("Projectで選択中のItemData / Prefabを一括追加", GUILayout.Height(25)))
        {
            ImportItemObjects(Selection.objects, boxIndex);
            GUIUtility.ExitGUI();
        }
        using (new EditorGUILayout.HorizontalScope())
        {
            itemFolder = (DefaultAsset)EditorGUILayout.ObjectField("Itemフォルダ", itemFolder, typeof(DefaultAsset), false);
            if (GUILayout.Button("一括登録", GUILayout.Width(80)))
            {
                ImportItemFolder(boxIndex);
                GUIUtility.ExitGUI();
            }
        }
    }

    private void DrawDropArea(string caption, bool forBoxes, int boxIndex)
    {
        Rect rect = GUILayoutUtility.GetRect(0, 55, GUILayout.ExpandWidth(true));
        GUI.Box(rect, caption + "\n（複数選択で一度に登録）", EditorStyles.helpBox);
        Event ev = Event.current;
        if (!rect.Contains(ev.mousePosition) ||
            (ev.type != EventType.DragUpdated && ev.type != EventType.DragPerform)) return;
        if (profile == null) return;
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (ev.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            UnityEngine.Object[] dropped = DragAndDrop.objectReferences;
            if (forBoxes) ImportBoxObjects(dropped);
            else ImportItemObjects(dropped, boxIndex);
            ev.Use();
            GUIUtility.ExitGUI();
        }
        else ev.Use();
    }

    private void ImportBoxFolder()
    {
        if (!IsFolder(boxFolder))
        {
            warning = "PrefabフォルダにはProject内のフォルダを指定してください。";
            return;
        }
        ImportBoxObjects(new UnityEngine.Object[] { boxFolder });
    }

    private void ImportItemFolder(int boxIndex)
    {
        if (!IsFolder(itemFolder))
        {
            warning = "ItemフォルダにはProject内のフォルダを指定してください。";
            return;
        }
        ImportItemObjects(new UnityEngine.Object[] { itemFolder }, boxIndex);
    }

    private static bool IsFolder(UnityEngine.Object asset)
    {
        return asset != null && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(asset));
    }

    private static IEnumerable<UnityEngine.Object> ExpandAssets(IEnumerable<UnityEngine.Object> input)
    {
        if (input == null) yield break;
        HashSet<string> visitedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (UnityEngine.Object raw in input)
        {
            if (raw == null) continue;
            string path = AssetDatabase.GetAssetPath(raw);
            if (AssetDatabase.IsValidFolder(path))
            {
                // 子フォルダも含めて探索する。
                foreach (string guid in AssetDatabase.FindAssets("", new[] { path }))
                {
                    string foundPath = AssetDatabase.GUIDToAssetPath(guid);
                    if (string.IsNullOrEmpty(foundPath) || !visitedPaths.Add(foundPath)) continue;
                    string extension = Path.GetExtension(foundPath).ToLowerInvariant();
                    if (extension != ".prefab" && extension != ".asset") continue;
                    UnityEngine.Object asset = AssetDatabase.LoadMainAssetAtPath(foundPath);
                    if (asset != null) yield return asset;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(path) || visitedPaths.Add(path))
                    yield return raw;
            }
        }
    }

    private static GameObject ToPrefab(UnityEngine.Object asset)
    {
        GameObject go = asset as GameObject;
        if (go == null) return null;
        if (!PrefabUtility.IsPartOfPrefabAsset(go))
        {
            // Hierarchyからドラッグしたプレハブインスタンスにも対応。
            GameObject instanceRoot = PrefabUtility.GetNearestPrefabInstanceRoot(go);
            if (instanceRoot == null) return null;
            go = PrefabUtility.GetCorrespondingObjectFromSource(instanceRoot);
        }
        if (go == null) return null;
        string path = AssetDatabase.GetAssetPath(go);
        return !string.IsNullOrEmpty(path) && path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase)
            ? AssetDatabase.LoadAssetAtPath<GameObject>(path) : null;
    }

    private void ImportBoxObjects(IEnumerable<UnityEngine.Object> objects)
    {
        if (profile == null) return;
        if (profile.boxes == null) profile.boxes = new List<DungeonLootStageProfile2D.BoxRule>();
        List<GameObject> toAdd = new List<GameObject>();
        HashSet<string> known = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (DungeonLootStageProfile2D.BoxRule entry in profile.boxes)
            if (entry != null && entry.itemBoxPrefab != null)
                known.Add(AssetDatabase.GetAssetPath(entry.itemBoxPrefab));
        int duplicate = 0, invalid = 0;
        foreach (UnityEngine.Object obj in ExpandAssets(objects))
        {
            GameObject prefab = ToPrefab(obj);
            if (prefab == null || prefab.GetComponentInChildren<ItemBoxInventory>(true) == null)
            {
                invalid++;
                continue;
            }
            if (!known.Add(AssetDatabase.GetAssetPath(prefab))) { duplicate++; continue; }
            toAdd.Add(prefab);
        }
        if (toAdd.Count > 0)
        {
            Undo.RecordObject(profile, "Bulk add ItemBox prefabs");
            foreach (GameObject prefab in toAdd)
            {
                DungeonLootBoxMarker2D old = prefab.GetComponentInChildren<DungeonLootBoxMarker2D>(true);
                string key = old != null && !string.IsNullOrWhiteSpace(old.PrefabKey) &&
                    !KeyUsedByOtherPrefab(old.PrefabKey, prefab) ? old.PrefabKey : GenerateUniqueKey();
                profile.boxes.Add(new DungeonLootStageProfile2D.BoxRule
                {
                    itemBoxPrefab = prefab, prefabKey = key, enabled = true,
                    emptyChancePercent = 10, minDraws = 2, maxDraws = 5
                });
            }
            selectedBoxIndex = profile.boxes.Count - 1;
            MarkProfile();
        }
        warning = $"ItemBox一括登録：追加 {toAdd.Count} / 重複 {duplicate} / 対象外 {invalid}。保存＋マーカー適用で確定してください。";
        ShowNotification(new GUIContent(warning));
        Repaint();
    }

    private void ImportItemObjects(IEnumerable<UnityEngine.Object> objects, int boxIndex)
    {
        if (profile == null) return;
        if (profile.stageItems == null) profile.stageItems = new List<DungeonLootStageProfile2D.ItemChance>();
        if (profile.boxes == null) profile.boxes = new List<DungeonLootStageProfile2D.BoxRule>();
        List<DungeonLootStageProfile2D.ItemChance> destination = boxIndex < 0 ? profile.stageItems :
            boxIndex < profile.boxes.Count && profile.boxes[boxIndex] != null
                ? profile.boxes[boxIndex].ownItemTable : null;
        if (destination == null) return;
        HashSet<ItemData> known = new HashSet<ItemData>();
        foreach (DungeonLootStageProfile2D.ItemChance old in destination)
            if (old != null && old.item != null) known.Add(old.item);
        List<ItemData> additions = new List<ItemData>();
        int duplicate = 0, invalid = 0;
        foreach (UnityEngine.Object obj in ExpandAssets(objects))
        {
            ItemData item = ExtractItemData(obj);
            if (item == null) { invalid++; continue; }
            if (!known.Add(item)) { duplicate++; continue; }
            additions.Add(item);
        }
        if (additions.Count > 0)
        {
            Undo.RecordObject(profile, "Bulk add dungeon loot items");
            foreach (ItemData data in additions)
                destination.Add(new DungeonLootStageProfile2D.ItemChance {
                    item = data, weight = newItemWeight,
                    minAmount = newItemMinAmount, maxAmount = newItemMaxAmount
                });
            MarkProfile();
        }
        warning = $"Item候補一括登録：追加 {additions.Count} / 重複 {duplicate} / 対象外 {invalid}。重複したItemの重み・個数は変更していません。";
        ShowNotification(new GUIContent(warning));
        Repaint();
    }

    private static ItemData ExtractItemData(UnityEngine.Object asset)
    {
        if (asset is ItemData direct) return direct;
        GameObject prefab = ToPrefab(asset);
        if (prefab == null) return null;
        // Prefab内にItemData参照がちょうど1個ある場合のみ採用。
        // 複数ある（店の在庫 / ドロップテーブル等）場合は誤登録を防ぐ。
        HashSet<ItemData> found = new HashSet<ItemData>();
        foreach (MonoBehaviour component in prefab.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component == null) continue;
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty it = serialized.GetIterator();
            bool enterChildren = true;
            while (it.NextVisible(enterChildren))
            {
                enterChildren = false;
                if (it.propertyType != SerializedPropertyType.ObjectReference ||
                    !IsItemDataProperty(it.name)) continue;
                ItemData data = it.objectReferenceValue as ItemData;
                if (data != null) found.Add(data);
            }
            if (found.Count > 1) return null;
        }
        foreach (ItemData item in found) return item;
        return null;
    }

    private static bool IsItemDataProperty(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName)) return false;
        switch (propertyName.ToLowerInvariant())
        {
            case "item": case "itemdata": case "itemdefinition":
            case "itemasset": case "itemtospawn": case "itemtopickup":
            case "iteminfo": case "itemtodrop": case "definition":
                return true;
            default: return false;
        }
    }

    private bool KeyUsedByOtherPrefab(string key, GameObject itemBoxPrefab)
    {
        if (profile == null || profile.boxes == null || string.IsNullOrWhiteSpace(key)) return false;
        foreach (DungeonLootStageProfile2D.BoxRule entry in profile.boxes)
            if (entry != null && entry.itemBoxPrefab != null && entry.itemBoxPrefab != itemBoxPrefab &&
                string.Equals(entry.prefabKey, key, StringComparison.Ordinal)) return true;
        return false;
    }

    private string GenerateUniqueKey()
    {
        string key;
        do { key = Guid.NewGuid().ToString("N"); }
        while (KeyUsedByOtherPrefab(key, null));
        return key;
    }

    private void FindExistingProfile()
    {
        profile = null;
        if (selectedScene == null) return;
        foreach (string guid in AssetDatabase.FindAssets("t:DungeonLootStageProfile2D"))
        {
            DungeonLootStageProfile2D p = AssetDatabase.LoadAssetAtPath<DungeonLootStageProfile2D>(AssetDatabase.GUIDToAssetPath(guid));
            if (p != null && string.Equals(p.sceneName, selectedScene.name, StringComparison.OrdinalIgnoreCase))
            {
                profile = p;
                break;
            }
        }
    }

    private void CreateProfile()
    {
        if (selectedScene == null) return;
        if (!Directory.Exists(ProfileFolder)) Directory.CreateDirectory(ProfileFolder);
        AssetDatabase.Refresh();
        string safe = string.Join("_", selectedScene.name.Split(Path.GetInvalidFileNameChars()));
        string path = AssetDatabase.GenerateUniqueAssetPath(ProfileFolder + "/" + safe + "_DungeonLoot.asset");
        profile = CreateInstance<DungeonLootStageProfile2D>();
        profile.sceneName = selectedScene.name;
        AssetDatabase.CreateAsset(profile, path);
        MarkProfile();
        selectedBoxIndex = -1;
        warning = "設定Assetを作成しました。候補Itemと対象Prefabを追加してください。";
    }

    private void NormalizeProfile()
    {
        if (profile == null) return;
        Undo.RecordObject(profile,"Normalize Dungeon Loot Profile");
        if (profile.stageItems != null) NormalizeEntries(profile.stageItems);
        if (profile.boxes == null) profile.boxes = new List<DungeonLootStageProfile2D.BoxRule>();
        foreach (var rule in profile.boxes)
        {
            if (rule == null) continue;
            rule.minDraws = Mathf.Max(0, rule.minDraws);
            rule.maxDraws = Mathf.Max(rule.minDraws, rule.maxDraws);
            if (rule.ownItemTable != null) NormalizeEntries(rule.ownItemTable);
        }
    }
    private static void NormalizeEntries(List<DungeonLootStageProfile2D.ItemChance> list)
    {
        foreach (var e in list)
        {
            if (e == null) continue;
            e.weight = Mathf.Max(0, e.weight);
            e.minAmount = Mathf.Max(1, e.minAmount);
            e.maxAmount = Mathf.Max(e.minAmount, e.maxAmount);
        }
    }

    private void ApplyMarkers()
    {
        if (profile == null) return;
        int ok = 0, failed = 0;
        if (profile.boxes == null) profile.boxes = new List<DungeonLootStageProfile2D.BoxRule>();
        foreach (var rule in profile.boxes)
        {
            if (rule == null || rule.itemBoxPrefab == null) continue;
            if (ApplyMarker(rule)) ok++; else failed++;
        }
        MarkProfile();
        warning = $"Prefabマーカー設置: 成功 {ok} / 失敗 {failed}。対象PrefabがScene内にあるか確認してください。";
    }

    private bool ApplyMarker(DungeonLootStageProfile2D.BoxRule rule)
    {
        if (rule == null || rule.itemBoxPrefab == null) return false;
        string path = AssetDatabase.GetAssetPath(rule.itemBoxPrefab);
        if (string.IsNullOrEmpty(path) || !path.EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))
        {
            Debug.LogWarning("[DungeonLootStudio] Project内のPrefab Assetを指定してください: " + rule.itemBoxPrefab);
            return false;
        }
        GameObject root = null;
        try
        {
            root = PrefabUtility.LoadPrefabContents(path);
            ItemBoxInventory box = root.GetComponentInChildren<ItemBoxInventory>(true);
            if (box == null)
            {
                Debug.LogWarning("[DungeonLootStudio] ItemBoxInventoryがPrefab内に見つかりません: " + path);
                return false;
            }
            DungeonLootBoxMarker2D marker = box.GetComponent<DungeonLootBoxMarker2D>();
            if (marker == null) marker = box.gameObject.AddComponent<DungeonLootBoxMarker2D>();
            // 既存のPrefabキーは、他ステージと共用している場合も尊重する。
            // Prefab複製でキーが被った場合は、新規キーにして片方のBoxを区別する。
            string key = marker.PrefabKey;
            if (string.IsNullOrWhiteSpace(key) || KeyUsedByOtherPrefab(key, rule.itemBoxPrefab))
            {
                key = !string.IsNullOrWhiteSpace(rule.prefabKey) &&
                    !KeyUsedByOtherPrefab(rule.prefabKey, rule.itemBoxPrefab)
                        ? rule.prefabKey : GenerateUniqueKey();
            }
            marker.SetKeyForEditor(key);
            rule.prefabKey = key;
            EditorUtility.SetDirty(marker);
            PrefabUtility.SaveAsPrefabAsset(root, path);
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            return false;
        }
        finally { if (root != null) PrefabUtility.UnloadPrefabContents(root); }
    }

    private void RepairDuplicatePersistentIds()
    {
        if (selectedScene == null || SceneManager.GetActiveScene().name != selectedScene.name)
        {
            EditorUtility.DisplayDialog("Sceneが違います", "指定したStage Sceneを開いてから実行してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("ItemBox ID重複チェック", "現在のScene内で重複したItemBoxSaveIdentityのPersistentIdを新規発行します。続行しますか？", "実行", "中止")) return;
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int count = 0;
        foreach (ItemBoxSaveIdentity identity in FindObjectsByType<ItemBoxSaveIdentity>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (identity == null || identity.gameObject.scene != SceneManager.GetActiveScene()) continue;
            if (!string.IsNullOrEmpty(identity.PersistentId) && ids.Add(identity.PersistentId)) continue;
            Undo.RecordObject(identity,"Fix ItemBox persistent ID");
            identity.GenerateNewPersistentId();
            PrefabUtility.RecordPrefabInstancePropertyModifications(identity);
            ids.Add(identity.PersistentId);
            count++;
        }
        if (count > 0) EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        warning = $"保存IDの重複を {count} 件修復しました。Sceneを保存してください。";
    }

    private void MarkProfile()
    {
        if (profile == null) return;
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
    }
}
#endif
