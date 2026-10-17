#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// ItemDataの作成・編集・検索・データベース登録を一つのウィンドウで行う。
/// Runtimeスクリプトには手を加えない。Editorフォルダに設置すること。
/// </summary>
public sealed class ItemStudioWindow2D : EditorWindow
{
    private static readonly Color HeaderColor = new Color(0.15f, 0.19f, 0.22f);
    private static readonly Color AccentColor = new Color(0.40f, 0.76f, 0.67f);
    private static readonly Color SoftColor = new Color(0.69f, 0.76f, 0.79f);
    private static readonly Color PreviewColor = new Color(0.105f, 0.12f, 0.14f);

    [SerializeField] private ItemData selected;
    [SerializeField] private ItemDataDatabase database;
    [SerializeField] private string targetFolder = "Assets/ScriptableObjects/Items";
    [SerializeField] private string searchText = "";
    [SerializeField] private int filterType = 0;
    [SerializeField] private int createType = 0;
    [SerializeField] private string newName = "新しいアイテム";
    [SerializeField] private Sprite newIcon;
    [SerializeField] private bool autoRegister = true;
    [SerializeField] private bool showLocalization;
    [SerializeField] private bool showShape = true;
    [SerializeField] private bool showTrade = true;
    [SerializeField] private bool showAdvanced = true;
    [SerializeField] private bool showDatabaseSettings = true;
    [SerializeField] private int previewRotation;

    private readonly List<Type> availableTypes = new List<Type>();
    private readonly List<ItemData> allItems = new List<ItemData>();
    private readonly Dictionary<string, int> idCounts = new Dictionary<string, int>(StringComparer.Ordinal);
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private bool dirtyList = true;
    private GUIStyle headerStyle;
    private GUIStyle headingStyle;
    private GUIStyle mutedStyle;

    [MenuItem("Tools/2D Shooter/Item Studio - アイテム統合管理")]
    public static void OpenWindow()
    {
        ItemStudioWindow2D window = GetWindow<ItemStudioWindow2D>("Item Studio 2D");
        window.minSize = new Vector2(920f, 620f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += OnProjectChanged;
        RebuildTypes();
        FindSoleDatabase();
        dirtyList = true;
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= OnProjectChanged;
    }

    private void OnProjectChanged()
    {
        dirtyList = true;
        Repaint();
    }

    private void EnsureStyles()
    {
        if (headerStyle != null) return;
        headerStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 19 };
        headerStyle.normal.textColor = Color.white;
        headingStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 12 };
        headingStyle.normal.textColor = AccentColor;
        mutedStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
        mutedStyle.normal.textColor = SoftColor;
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (availableTypes.Count == 0) RebuildTypes();
        if (dirtyList) ReloadItems();

        Rect header = GUILayoutUtility.GetRect(1, 65, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(header, HeaderColor);
        GUI.Label(new Rect(header.x + 15, header.y + 8, header.width - 24, 26), "ITEM STUDIO 2D", headerStyle);
        GUI.Label(new Rect(header.x + 16, header.y + 37, header.width - 24, 20),
            "ItemData 作成 / グリッド形状 / 武器・弾薬パラメータ / Database登録", mutedStyle);
        GUILayout.Space(8);

        EditorGUILayout.BeginHorizontal();
        float leftWidth = Mathf.Clamp(position.width * 0.33f, 286f, 380f);
        EditorGUILayout.BeginVertical(GUILayout.Width(leftWidth));
        DrawLeft(leftWidth);
        EditorGUILayout.EndVertical();
        GUILayout.Space(9);
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawRight();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawLeft(float width)
    {
        GUILayout.Label("01  ITEM PREVIEW", headingStyle);
        DrawSpritePreview(selected != null ? selected.Icon : newIcon, width - 5f, 154f);
        GUILayout.Space(5f);

        if (selected == null)
        {
            EditorGUILayout.HelpBox("新しいItemData(.asset)を作成します。", MessageType.Info);
            DrawCreateFields();
        }
        else
        {
            DrawSelectionCard();
        }

        GUILayout.Space(9f);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("02  ITEM LIBRARY", headingStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("更新", GUILayout.Width(48))) { dirtyList = true; ReloadItems(); }
        EditorGUILayout.EndHorizontal();
        searchText = EditorGUILayout.TextField("検索", searchText);

        var labels = new List<string> { "すべて" };
        labels.AddRange(availableTypes.Select(TypeLabel));
        filterType = Mathf.Clamp(filterType, 0, labels.Count - 1);
        filterType = EditorGUILayout.Popup("種類", filterType, labels.ToArray());

        leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUILayout.MinHeight(120f), GUILayout.ExpandHeight(true));
        int visible = 0;
        foreach (ItemData item in allItems)
        {
            if (item == null || !IsVisible(item)) continue;
            visible++;
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(42f));
            Texture icon = item.Icon != null ? AssetPreview.GetMiniThumbnail(item.Icon) : null;
            if (icon != null) GUILayout.Label(icon, GUILayout.Width(34), GUILayout.Height(34));
            else GUILayout.Box("◇", GUILayout.Width(34), GUILayout.Height(34));
            EditorGUILayout.BeginVertical();
            bool active = selected == item;
            string name = item.DisplayName;
            if (GUILayout.Button((active ? "▶ " : "") + name, EditorStyles.linkLabel, GUILayout.Height(21)))
            {
                selected = item;
                previewRotation = 0;
                GUI.FocusControl(null);
            }
            GUILayout.Label(item.ItemId + "   [" + item.ItemType + "]", EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            if (IsDuplicateId(item)) GUILayout.Label("!", GUILayout.Width(12));
            EditorGUILayout.EndHorizontal();
        }
        if (visible == 0) EditorGUILayout.HelpBox("該当するItemDataはありません。", MessageType.None);
        EditorGUILayout.EndScrollView();
        GUILayout.Label("登録済みAsset: " + allItems.Count + "件", EditorStyles.miniLabel);
    }

    private void DrawCreateFields()
    {
        if (availableTypes.Count == 0) return;
        createType = Mathf.Clamp(createType, 0, availableTypes.Count - 1);
        createType = EditorGUILayout.Popup("Itemの種類", createType, availableTypes.Select(TypeLabel).ToArray());
        newName = EditorGUILayout.TextField("Item名", newName);

        // 新規ItemのIDはユーザー入力不要。作成直前に最新の全Assetを再走査する。
        using (new EditorGUI.DisabledScope(true))
            EditorGUILayout.TextField("Item ID（自動発行）", MakeNewId());
        EditorGUILayout.HelpBox(
            "『作成』を押した時点で未使用のIDを自動割り当てします。既存ItemのIDは変更しません。" +
            " セーブデータとの互換性のため、作成後のID変更は避けてください。",
            MessageType.None);

        newIcon = (Sprite)EditorGUILayout.ObjectField("アイコン", newIcon, typeof(Sprite), false);
        using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(newName)))
        {
            if (GUILayout.Button("＋  ItemDataを作成（ID自動発行）", GUILayout.Height(34)))
                CreateItem();
        }
    }

    private void DrawSelectionCard()
    {
        if (selected == null) return;
        EditorGUILayout.LabelField(selected.DisplayName, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("種類", selected.GetType().Name);
        EditorGUILayout.LabelField("Item ID", selected.ItemId);
        if (IsDuplicateId(selected)) EditorGUILayout.HelpBox("Item IDが別のAssetと重複しています。", MessageType.Error);
        if (database != null && !IsRegistered(selected))
            EditorGUILayout.HelpBox("このItemは選択中のItemDataDatabaseに未登録です。", MessageType.Warning);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新規作成へ", GUILayout.Height(28)))
        {
            selected = null;
            previewRotation = 0;
        }
        if (GUILayout.Button("複製", GUILayout.Height(28))) DuplicateItem();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Projectで表示")) EditorGUIUtility.PingObject(selected);
        if (GUILayout.Button("保存"))
        {
            EditorUtility.SetDirty(selected);
            AssetDatabase.SaveAssets();
            dirtyList = true;
        }
        EditorGUILayout.EndHorizontal();
        if (GUILayout.Button("削除...")) DeleteSelected();
    }

    private void DrawSpritePreview(Sprite sprite, float width, float height)
    {
        Rect r = GUILayoutUtility.GetRect(Mathf.Max(50, width), height, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(r, PreviewColor);
        if (sprite == null)
        {
            GUI.Label(r, "SPRITE PREVIEW\nアイコンを設定してください", new GUIStyle(EditorStyles.centeredGreyMiniLabel)
            {
                alignment = TextAnchor.MiddleCenter, wordWrap = true
            });
            return;
        }
        Texture2D texture = AssetPreview.GetAssetPreview(sprite) ?? AssetPreview.GetMiniThumbnail(sprite);
        if (texture == null) return;
        float scale = Mathf.Min((r.width - 16f) / texture.width, (r.height - 16f) / texture.height);
        float w = texture.width * scale, h = texture.height * scale;
        GUI.DrawTexture(new Rect(r.center.x - w / 2, r.center.y - h / 2, w, h), texture, ScaleMode.ScaleToFit, true);
    }

    private void DrawRight()
    {
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("03  ITEM SETTINGS", headingStyle);
        GUILayout.FlexibleSpace();
        if (selected != null) GUILayout.Label(selected.GetType().Name, EditorStyles.miniLabel);
        EditorGUILayout.EndHorizontal();
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll, GUILayout.ExpandHeight(true));
        DrawDatabaseSection();
        GUILayout.Space(7);

        if (selected == null)
        {
            EditorGUILayout.HelpBox("左側で新規Itemを作成するか、一覧から既存のItemを選択してください。", MessageType.Info);
            DrawTypeSummary();
            EditorGUILayout.EndScrollView();
            return;
        }

        SerializedObject so = new SerializedObject(selected);
        so.Update();
        DrawSection("基本情報", true, () =>
        {
            DrawProperty(so, "itemId", "Item ID（セーブ互換注意）");
            DrawProperty(so, "displayName", "名前（未翻訳時）");
            DrawProperty(so, "description", "説明（未翻訳時）");
            DrawProperty(so, "icon", "アイコン");
        });

        showLocalization = EditorGUILayout.Foldout(showLocalization, "名前・説明のローカライズ（任意）", true);
        if (showLocalization)
        {
            EditorGUI.indentLevel++;
            DrawProperty(so, "localizedDisplayName", "名前のLocalizedString", true);
            DrawProperty(so, "localizedDescription", "説明のLocalizedString", true);
            EditorGUI.indentLevel--;
        }

        showShape = EditorGUILayout.Foldout(showShape, "Inventoryのサイズ・カスタム形状", true);
        if (showShape)
        {
            EditorGUI.indentLevel++;
            DrawProperty(so, "width", "幅（マス）");
            DrawProperty(so, "height", "高さ（マス）");
            DrawProperty(so, "canRotate", "回転可能");
            DrawCustomShape(so);
            EditorGUI.indentLevel--;
        }

        showTrade = EditorGUILayout.Foldout(showTrade, "所持・取引・重量", true);
        if (showTrade)
        {
            EditorGUI.indentLevel++;
            DrawProperty(so, "maxStack", "最大スタック数");
            DrawProperty(so, "purchasePrice", "購入価格（G）");
            DrawProperty(so, "sellPrice", "売却価格（G）");
            DrawProperty(so, "canSellToShop", "売却可能");
            DrawProperty(so, "weight", "重量（ゲーム内設定単位）");
            DrawProperty(so, "canDiscard", "捨てられる");
            EditorGUI.indentLevel--;
        }

        showAdvanced = EditorGUILayout.Foldout(showAdvanced, "専用パラメータ（武器・弾薬・その他）", true);
        if (showAdvanced)
        {
            EditorGUI.indentLevel++;
            DrawDerivedFields(so);
            EditorGUI.indentLevel--;
        }

        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(selected);
            dirtyList = true;
            if (IsDuplicateId(selected))
                EditorGUILayout.HelpBox("Item IDに重複があります。セーブ・ロードが正しく動作しない可能性があります。", MessageType.Error);
        }
        EditorGUILayout.Space(8);
        if (GUILayout.Button("変更を保存", GUILayout.Height(31)))
        {
            EditorUtility.SetDirty(selected);
            AssetDatabase.SaveAssets();
            ReloadItems();
        }
        EditorGUILayout.EndScrollView();
    }

    private void DrawTypeSummary()
    {
        EditorGUILayout.LabelField("認識しているItemDataの種類", EditorStyles.boldLabel);
        foreach (Type type in availableTypes)
            EditorGUILayout.LabelField("• " + TypeLabel(type) + "  (" + type.Name + ")");
        EditorGUILayout.HelpBox("ItemDataを継承する新しいスクリプトを追加すると、自動的に種類の一覧に表示されます。", MessageType.None);
    }

    private void DrawDatabaseSection()
    {
        showDatabaseSettings = EditorGUILayout.Foldout(showDatabaseSettings, "ItemDataDatabase・保存先", true);
        if (!showDatabaseSettings) return;
        EditorGUI.indentLevel++;
        database = (ItemDataDatabase)EditorGUILayout.ObjectField("Database", database, typeof(ItemDataDatabase), false);
        targetFolder = EditorGUILayout.TextField("作成先フォルダ", targetFolder);
        autoRegister = EditorGUILayout.Toggle("新規作成時にDBへ登録", autoRegister);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Databaseを自動検索")) FindSoleDatabase(true);
        if (selected != null && GUILayout.Button("選択ItemをDBへ登録")) RegisterInDatabase(selected, true);
        EditorGUILayout.EndHorizontal();
        if (database != null && GUILayout.Button("未登録Itemを一括登録（重複IDは除外）")) RegisterAllUniqueInDatabase();
        if (database == null)
            EditorGUILayout.HelpBox("Database未指定でもItemDataの作成は可能です。ただしゲーム側のItem ID検索には登録が必要です。", MessageType.Warning);
        else if (selected != null && IsRegistered(selected))
            EditorGUILayout.LabelField("Database登録済み", EditorStyles.miniLabel);
        EditorGUI.indentLevel--;
    }

    private static void DrawSection(string title, bool expanded, Action content)
    {
        if (!expanded) return;
        EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
        EditorGUI.indentLevel++;
        content();
        EditorGUI.indentLevel--;
        EditorGUILayout.Space(5);
    }

    private static void DrawProperty(SerializedObject so, string name, string title, bool children = false)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p == null) return;
        EditorGUILayout.PropertyField(p, new GUIContent(title, p.tooltip), children);
    }

    private static readonly HashSet<string> BaseFieldNames = new HashSet<string>
    {
        "m_Script", "itemId", "displayName", "description", "icon",
        "localizedDisplayName", "localizedDescription", "width", "height", "canRotate",
        "useCustomShape", "customShapeCells", "maxStack", "purchasePrice", "sellPrice",
        "canSellToShop", "weight", "canDiscard"
    };

    private void DrawDerivedFields(SerializedObject so)
    {
        bool any = false;
        SerializedProperty iterator = so.GetIterator();
        bool next = iterator.NextVisible(true);
        while (next)
        {
            // NextVisible(false) ならトップレベルのフィールドだけを巡回する。
            if (iterator.depth == 0 && !BaseFieldNames.Contains(iterator.name))
            {
                any = true;
                EditorGUILayout.PropertyField(iterator, true);
            }
            next = iterator.NextVisible(false);
        }
        if (!any) EditorGUILayout.LabelField("このItem種別には追加設定がありません。", EditorStyles.miniLabel);
    }

    private void DrawCustomShape(SerializedObject so)
    {
        SerializedProperty toggle = so.FindProperty("useCustomShape");
        SerializedProperty cells = so.FindProperty("customShapeCells");
        SerializedProperty widthProp = so.FindProperty("width");
        SerializedProperty heightProp = so.FindProperty("height");
        if (toggle == null || cells == null || widthProp == null || heightProp == null) return;

        int w = Mathf.Clamp(widthProp.intValue, 1, 40);
        int h = Mathf.Clamp(heightProp.intValue, 1, 40);
        bool previous = toggle.boolValue;
        bool enabledShape = EditorGUILayout.Toggle("不規則なマス形状を使う", previous);
        if (enabledShape != previous)
        {
            toggle.boolValue = enabledShape;
            if (enabledShape && cells.arraySize == 0)
            {
                // 空集合を避け、今の矩形すべてをONにして開始する。
                for (int y = 0; y < h; y++)
                    for (int x = 0; x < w; x++)
                        AppendShapeCell(cells, new Vector2Int(x, y));
            }
        }
        HashSet<Vector2Int> used = new HashSet<Vector2Int>();
        if (toggle.boolValue)
        {
            for (int i = 0; i < cells.arraySize; i++)
                used.Add(cells.GetArrayElementAtIndex(i).vector2IntValue);
        }
        else
        {
            for (int y = 0; y < h; y++)
                for (int x = 0; x < w; x++) used.Add(new Vector2Int(x, y));
        }

        GUILayout.Label("マス配置（左上が0,0）", EditorStyles.miniBoldLabel);
        float cellSize = Mathf.Clamp(385f / Mathf.Max(1, w), 15f, 30f);
        int drawnWidth = Mathf.Min(w, 25);
        int drawnHeight = Mathf.Min(h, 25);
        for (int y = 0; y < drawnHeight; y++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6);
            for (int x = 0; x < drawnWidth; x++)
            {
                Vector2Int coord = new Vector2Int(x, y);
                bool on = used.Contains(coord);
                Color oldBg = GUI.backgroundColor;
                GUI.backgroundColor = on ? new Color(0.38f, 0.75f, 0.64f) : new Color(0.33f, 0.35f, 0.39f);
                GUI.enabled = toggle.boolValue;
                if (GUILayout.Button(on ? "■" : " ", GUILayout.Width(cellSize), GUILayout.Height(cellSize)))
                {
                    if (on) used.Remove(coord); else used.Add(coord);
                    WriteShapeCells(cells, used);
                }
                GUI.enabled = true;
                GUI.backgroundColor = oldBg;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (w > 25 || h > 25)
            EditorGUILayout.HelpBox("プレビューは最大25×25までです。サイズは通常25以下をおすすめします。", MessageType.Info);

        if (toggle.boolValue)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全マス選択"))
            {
                used.Clear();
                for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) used.Add(new Vector2Int(x, y));
                WriteShapeCells(cells, used);
            }
            if (GUILayout.Button("クリア")) WriteShapeCells(cells, new HashSet<Vector2Int>());
            EditorGUILayout.EndHorizontal();
            if (used.Count == 0)
                EditorGUILayout.HelpBox("カスタム形状が空の場合、ItemDataは通常の矩形マスを使用します。", MessageType.Warning);
        }
        EditorGUILayout.LabelField("占有マス数", used.Count.ToString());
        if (selected != null && so.FindProperty("canRotate").boolValue)
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("↻ 回転プレビュー", GUILayout.Width(125))) previewRotation = (previewRotation + 1) % 4;
            EditorGUILayout.LabelField((previewRotation * 90).ToString() + "° （読み取り専用）", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            if (previewRotation != 0)
                DrawRotatedCells(used, w, h, previewRotation);
        }
    }

    private static void DrawRotatedCells(HashSet<Vector2Int> baseCells, int width, int height, int steps)
    {
        var occupied = new HashSet<Vector2Int>();
        int newWidth = width, newHeight = height;
        foreach (Vector2Int baseCell in baseCells)
        {
            int x = baseCell.x, y = baseCell.y;
            int cw = width, ch = height;
            for (int i = 0; i < steps; i++)
            {
                int nx = ch - 1 - y;
                int ny = x;
                x = nx;
                y = ny;
                int t = cw; cw = ch; ch = t;
            }
            occupied.Add(new Vector2Int(x, y));
            newWidth = cw; newHeight = ch;
        }
        // 空の形状でも回転後の矩形サイズは示す。
        if (baseCells.Count == 0 && (steps & 1) != 0) { newWidth = height; newHeight = width; }
        EditorGUILayout.LabelField("回転後: " + newWidth + " × " + newHeight, EditorStyles.miniBoldLabel);
        int rows = Mathf.Min(newHeight, 25), cols = Mathf.Min(newWidth, 25);
        for (int y = 0; y < rows; y++)
        {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(6);
            for (int x = 0; x < cols; x++)
            {
                Rect r = GUILayoutUtility.GetRect(17, 17, GUILayout.Width(17), GUILayout.Height(17));
                EditorGUI.DrawRect(r, occupied.Contains(new Vector2Int(x, y))
                    ? new Color(0.38f, 0.75f, 0.64f)
                    : new Color(0.27f, 0.29f, 0.32f));
            }
            EditorGUILayout.EndHorizontal();
        }
    }

    private static void AppendShapeCell(SerializedProperty list, Vector2Int cell)
    {
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).vector2IntValue = cell;
    }

    private static void WriteShapeCells(SerializedProperty list, HashSet<Vector2Int> set)
    {
        list.ClearArray();
        foreach (Vector2Int cell in set.OrderBy(v => v.y).ThenBy(v => v.x))
            AppendShapeCell(list, cell);
    }

    private bool IsVisible(ItemData item)
    {
        if (filterType > 0 && filterType - 1 < availableTypes.Count &&
            item.GetType() != availableTypes[filterType - 1]) return false;
        if (string.IsNullOrWhiteSpace(searchText)) return true;
        string text = searchText.Trim();
        return (item.DisplayName != null && item.DisplayName.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            || (item.ItemId != null && item.ItemId.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0)
            || (item.name != null && item.name.IndexOf(text, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private void RebuildTypes()
    {
        availableTypes.Clear();
        availableTypes.Add(typeof(ItemData));
        foreach (Type type in TypeCache.GetTypesDerivedFrom<ItemData>())
        {
            if (type == null || type.IsAbstract || type.IsGenericType || !typeof(ItemData).IsAssignableFrom(type)) continue;
            if (!availableTypes.Contains(type)) availableTypes.Add(type);
        }
        availableTypes.Sort((a, b) =>
        {
            if (a == typeof(ItemData)) return -1;
            if (b == typeof(ItemData)) return 1;
            return string.CompareOrdinal(a.Name, b.Name);
        });
    }

    private static string TypeLabel(Type t)
    {
        if (t == typeof(ItemData)) return "その他・一般Item";
        if (t == typeof(WeaponItemData)) return "武器";
        if (t == typeof(AmmoItemData)) return "弾薬";
        if (t == typeof(TreasureItemData)) return "財宝";
        return t.Name;
    }

    private void ReloadItems()
    {
        allItems.Clear();
        idCounts.Clear();
        var pathSet = new HashSet<string>();
        foreach (Type type in availableTypes)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!pathSet.Add(path)) continue;
                ItemData asset = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (asset != null) allItems.Add(asset);
            }
        }
        allItems.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.CurrentCulture));
        foreach (ItemData item in allItems)
        {
            if (string.IsNullOrWhiteSpace(item.ItemId)) continue;
            int count;
            idCounts.TryGetValue(item.ItemId, out count);
            idCounts[item.ItemId] = count + 1;
        }
        dirtyList = false;
    }

    private bool IsDuplicateId(ItemData item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.ItemId)) return false;
        return IsIdInUse(item.ItemId, item);
    }

    private bool IsIdInUse(string id, ItemData exclude)
    {
        if (string.IsNullOrWhiteSpace(id)) return false;
        foreach (ItemData item in allItems)
            if (item != null && item != exclude && string.Equals(id.Trim(), item.ItemId, StringComparison.Ordinal)) return true;
        return false;
    }

    /// <summary>
    /// すべての既存Itemを確認し、item_0001 形式の最大番号の次を割り当てる。
    /// 欠番を埋めないことで、古いIDをなるべく再利用しない。
    /// 異なる命名形式のIDも衝突検査には含める。
    /// </summary>
    private string MakeNewId()
    {
        const string prefix = "item_";
        long highest = 0;
        foreach (ItemData item in allItems)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.ItemId)) continue;
            string id = item.ItemId.Trim();
            if (!id.StartsWith(prefix, StringComparison.Ordinal)) continue;
            string suffix = id.Substring(prefix.Length);
            if (long.TryParse(suffix, out long number) && number > highest)
                highest = number;
        }

        // 全体の最大番号+1。値が不正または極端に大きい場合はランダムIDへ退避。
        if (highest < long.MaxValue)
        {
            long number = highest + 1;
            for (int attempts = 0; attempts < 100000; attempts++, number++)
            {
                if (number <= 0) break;
                string id = prefix + number.ToString("D4");
                if (!IsIdInUse(id, null)) return id;
                if (number == long.MaxValue) break;
            }
        }
        string uniqueId;
        do { uniqueId = prefix + Guid.NewGuid().ToString("N"); }
        while (IsIdInUse(uniqueId, null));
        return uniqueId;
    }

    private void CreateItem()
    {
        if (availableTypes.Count == 0 || string.IsNullOrWhiteSpace(newName)) return;
        // 表示中に別のItemが作成された場合でも、最新のIDを用いて衝突を防ぐ。
        ReloadItems();
        string id = MakeNewId();
        if (IsIdInUse(id, null))
        {
            EditorUtility.DisplayDialog("重複ID", "同じItem IDがすでにあります。", "OK");
            return;
        }
        if (!EnsureFolder(targetFolder)) return;

        ItemData asset = (ItemData)ScriptableObject.CreateInstance(availableTypes[Mathf.Clamp(createType, 0, availableTypes.Count - 1)]);
        SerializedObject so = new SerializedObject(asset);
        so.FindProperty("itemId").stringValue = id;
        so.FindProperty("displayName").stringValue = newName.Trim();
        so.FindProperty("icon").objectReferenceValue = newIcon;
        so.ApplyModifiedPropertiesWithoutUndo();
        asset.name = MakeSafeFileName(newName.Trim());
        string path = AssetDatabase.GenerateUniqueAssetPath(targetFolder.TrimEnd('/') + "/" + asset.name + ".asset");
        AssetDatabase.CreateAsset(asset, path);
        EditorUtility.SetDirty(asset);
        AssetDatabase.SaveAssets();
        selected = asset;
        if (autoRegister && database != null) RegisterInDatabase(asset, false);
        dirtyList = true;
        ReloadItems();
        EditorGUIUtility.PingObject(asset);
        previewRotation = 0;
        ShowNotification(new GUIContent("ItemDataを作成しました"));
    }

    private void DuplicateItem()
    {
        if (selected == null || !EnsureFolder(targetFolder)) return;
        // 複製も元Itemと同じIDにならないよう、最新の一覧を確認して採番。
        ReloadItems();
        ItemData duplicate = Instantiate(selected);
        duplicate.name = MakeSafeFileName(selected.name + "_Copy");
        SerializedObject so = new SerializedObject(duplicate);
        so.FindProperty("itemId").stringValue = MakeNewId();
        SerializedProperty fallbackName = so.FindProperty("displayName");
        if (fallbackName != null) fallbackName.stringValue += " コピー";
        so.ApplyModifiedPropertiesWithoutUndo();
        string path = AssetDatabase.GenerateUniqueAssetPath(targetFolder.TrimEnd('/') + "/" + duplicate.name + ".asset");
        AssetDatabase.CreateAsset(duplicate, path);
        EditorUtility.SetDirty(duplicate);
        AssetDatabase.SaveAssets();
        selected = duplicate;
        if (autoRegister && database != null) RegisterInDatabase(duplicate, false);
        dirtyList = true;
        ReloadItems();
        ShowNotification(new GUIContent("複製しました。翻訳の参照は確認してください。"));
    }

    private void DeleteSelected()
    {
        if (selected == null) return;
        string path = AssetDatabase.GetAssetPath(selected);
        if (string.IsNullOrEmpty(path)) return;
        if (!EditorUtility.DisplayDialog("Itemを削除", selected.DisplayName + " を完全に削除します。\nItem IDを使用している既存のセーブやアイテム参照は壊れる可能性があります。", "削除する", "やめる")) return;

        // ItemDataDatabaseの参照を先に解除し、Missingを残さない。
        foreach (string guid in AssetDatabase.FindAssets("t:ItemDataDatabase"))
        {
            ItemDataDatabase db = AssetDatabase.LoadAssetAtPath<ItemDataDatabase>(AssetDatabase.GUIDToAssetPath(guid));
            if (db == null) continue;
            SerializedObject dbSO = new SerializedObject(db);
            SerializedProperty list = dbSO.FindProperty("itemDataList");
            if (list == null) continue;
            bool changed = false;
            for (int i = list.arraySize - 1; i >= 0; i--)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue != selected) continue;
                // ObjectReference配列はDeleteArrayElementAtIndexを2度要する場合がある。
                list.GetArrayElementAtIndex(i).objectReferenceValue = null;
                list.DeleteArrayElementAtIndex(i);
                changed = true;
            }
            if (changed)
            {
                dbSO.ApplyModifiedProperties();
                EditorUtility.SetDirty(db);
                db.ValidateItemIds();
            }
        }
        selected = null;
        AssetDatabase.DeleteAsset(path);
        AssetDatabase.SaveAssets();
        dirtyList = true;
        ReloadItems();
    }

    private bool IsRegistered(ItemData item)
    {
        if (database == null || item == null) return false;
        SerializedObject so = new SerializedObject(database);
        SerializedProperty list = so.FindProperty("itemDataList");
        if (list == null) return false;
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == item) return true;
        return false;
    }

    private void RegisterInDatabase(ItemData item, bool showDialog)
    {
        if (database == null || item == null)
        {
            if (showDialog) EditorUtility.DisplayDialog("Database未設定", "ItemDataDatabaseを選択してください。", "OK");
            return;
        }
        if (IsRegistered(item))
        {
            if (showDialog) ShowNotification(new GUIContent("すでに登録済みです"));
            return;
        }
        SerializedObject so = new SerializedObject(database);
        SerializedProperty list = so.FindProperty("itemDataList");
        if (list == null)
        {
            Debug.LogError("[ItemStudio] ItemDataDatabase.itemDataListが見つかりません。", database);
            return;
        }
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = item;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        database.ValidateItemIds();
        AssetDatabase.SaveAssets();
        if (showDialog) ShowNotification(new GUIContent("Databaseに登録しました"));
    }

    private void RegisterAllUniqueInDatabase()
    {
        if (database == null) return;
        ReloadItems();
        List<ItemData> candidates = allItems.Where(item =>
            item != null && !string.IsNullOrWhiteSpace(item.ItemId) &&
            !IsDuplicateId(item) && !IsRegistered(item)).ToList();
        if (candidates.Count == 0)
        {
            EditorUtility.DisplayDialog("一括登録", "追加できる未登録のItemDataはありません。重複IDも確認してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("ItemDataの一括登録",
            candidates.Count + "件のItemDataを選択中のDatabaseへ登録します。\n実行しますか？", "登録する", "キャンセル")) return;
        SerializedObject so = new SerializedObject(database);
        SerializedProperty list = so.FindProperty("itemDataList");
        if (list == null) return;
        foreach (ItemData item in candidates)
        {
            int index = list.arraySize;
            list.InsertArrayElementAtIndex(index);
            list.GetArrayElementAtIndex(index).objectReferenceValue = item;
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(database);
        database.ValidateItemIds();
        AssetDatabase.SaveAssets();
        ShowNotification(new GUIContent(candidates.Count + "件を登録しました"));
    }

    private void FindSoleDatabase(bool report = false)
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemDataDatabase");
        if (guids.Length == 1)
        {
            database = AssetDatabase.LoadAssetAtPath<ItemDataDatabase>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }
        else if (report)
        {
            EditorUtility.DisplayDialog("Databaseの検索", guids.Length == 0
                ? "ItemDataDatabaseが見つかりません。Project内で作成し、画面に割り当ててください。"
                : "Databaseが複数存在します。ゲームで使う正しいDatabaseを画面から指定してください。", "OK");
        }
    }

    private static bool EnsureFolder(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder) || !(folder == "Assets" || folder.StartsWith("Assets/", StringComparison.Ordinal)) || folder.Contains(".."))
        {
            EditorUtility.DisplayDialog("保存先が不正", "Assets/ から始まるプロジェクト内のフォルダを指定してください。", "OK");
            return false;
        }
        string current = "Assets";
        string[] parts = folder.Split('/');
        for (int i = 1; i < parts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(parts[i])) continue;
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
            {
                string guid = AssetDatabase.CreateFolder(current, parts[i]);
                if (string.IsNullOrEmpty(guid))
                {
                    EditorUtility.DisplayDialog("フォルダ作成失敗", next + " を作成できません。", "OK");
                    return false;
                }
            }
            current = next;
        }
        return AssetDatabase.IsValidFolder(folder.TrimEnd('/'));
    }

    private static string MakeSafeFileName(string source)
    {
        foreach (char c in Path.GetInvalidFileNameChars()) source = source.Replace(c, '_');
        source = source.Replace('/', '_').Replace('\\', '_').Trim().Trim('.');
        return string.IsNullOrWhiteSpace(source) ? "NewItem" : source;
    }
}
#endif
