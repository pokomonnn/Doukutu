#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 財宝50種類インポーター。既存ItemData / DBの構造に合わせるためRuntimeコードを変更しない。
/// 一括生成時、同一パスの既存ItemDataは上書きせずスキップする。
/// </summary>
public sealed class Treasure50BatchImportWindow : EditorWindow
{
    private const string DefaultIconFolder = "Assets/Treasure50Pack/Icons";
    private const string DefaultOutput = "Assets/ScriptableObjects/Items/Treasure50";
    private static readonly Color Header = new Color(0.16f, 0.22f, 0.24f);
    private static readonly Color Mint = new Color(0.51f, 0.84f, 0.73f);

    private enum PriceConversion
    {
        RoundUpToWholeG,
        TenthsOfGAsIntegerUnit
    }

    private sealed class Spec
    {
        public readonly int No;
        public readonly string Name;
        public readonly string OriginalPrice;
        public readonly float WeightKg;
        public readonly string Description;
        public readonly int Width, Height, Stack;
        public readonly bool CanRotate;

        public Spec(int no, string name, string price, float weightKg, string description,
            int width, int height, int stack, bool canRotate)
        {
            No = no; Name = name; OriginalPrice = price; WeightKg = weightKg;
            Description = description; Width = width; Height = height;
            Stack = stack; CanRotate = canRotate;
        }
    }

    private static readonly Spec[] Catalog =
    {
            new Spec(1, "錆びた釘の束", "0.1", 0.3f, "赤錆に覆われた鉄釘の束。溶かせば鉄材として再利用できる。", 1, 1, 10, false),
            new Spec(2, "ひび割れた木椀", "0.3", 0.22f, "庶民が使用していた粗末な木椀。底には大きなひびが入っている。", 1, 1, 1, false),
            new Spec(3, "欠けた陶器の杯", "0.6", 0.35f, "縁が欠けた安物の酒杯。かつては酒場などで使われていたようだ。", 1, 1, 1, false),
            new Spec(4, "摩耗した馬蹄", "1", 0.45f, "長年使われていた鉄製の馬蹄。表面には無数の擦り傷がある。", 1, 1, 3, false),
            new Spec(5, "錆びた鎖の切れ端", "1.5", 1.8f, "地下牢で使用されていた鉄鎖。錆びついているが金属としての価値はある。", 1, 2, 1, true),
            new Spec(6, "割れた土鍋", "2.5", 1.5f, "大きくひび割れた調理用の土鍋。内部には古い焦げ跡が残っている。", 2, 2, 1, false),
            new Spec(7, "破れた麻袋", "3", 0.45f, "穀物の運搬に使われていた麻袋。何度も補修された跡がある。", 2, 2, 1, false),
            new Spec(8, "古びた鉄錠", "4", 0.9f, "鉄製の古い南京錠。鍵穴が詰まっており、開閉することはできない。", 1, 1, 2, false),
            new Spec(9, "朽ちた木盾", "6", 2.2f, "湿気によって傷んだ木製の盾。表面にはかすかに紋章が残っている。", 2, 3, 1, true),
            new Spec(10, "欠けた手斧", "8", 1.4f, "刃が大きく欠けた作業用の手斧。修理すれば再利用できそうだ。", 2, 2, 1, true),
            new Spec(11, "革の水袋", "12", 0.3f, "厚手の革で作られた水袋。使い込まれているが、まだ十分に使用できる。", 1, 2, 1, true),
            new Spec(12, "蜜蝋の蝋燭束", "18", 0.45f, "上質な蜜蝋で作られた蝋燭の束。燃焼時間が長く、貴族の間で好まれる。", 1, 2, 4, true),
            new Spec(13, "羊皮紙の束", "24", 0.6f, "文字を書くために加工された羊皮紙。修道士や書記官に需要がある。", 2, 1, 5, true),
            new Spec(14, "香辛料の小袋", "30", 0.2f, "遠方の国から運ばれた希少な香辛料。独特の強い香りがする。", 1, 1, 5, false),
            new Spec(15, "錫の食器揃い", "38", 1.5f, "錫で作られた皿や杯の一式。比較的裕福な家庭で使用されていた。", 2, 2, 1, false),
            new Spec(16, "鍛冶道具の袋", "46", 3f, "金槌や鉄鋏などが詰め込まれた革袋。どの道具も使い込まれている。", 2, 2, 1, true),
            new Spec(17, "羊毛の反物", "55", 3.5f, "厚手の羊毛を織り上げた布。防寒着などの材料として重宝される。", 2, 3, 1, true),
            new Spec(18, "銀の指輪", "65", 0.012f, "銀で作られた簡素な指輪。目立った装飾はないが、丁寧に磨かれている。", 1, 1, 4, false),
            new Spec(19, "金の指輪", "100", 0.008f, "細身の金製指輪。小ぶりながらも美しい輝きを放っている。", 1, 1, 4, false),
            new Spec(20, "塩漬け肉の小樽", "110", 6f, "保存用の塩漬け肉が詰まった小樽。封が保たれており、保存食として価値がある。", 2, 3, 1, false),
            new Spec(21, "細工入りの革鞄", "140", 1.2f, "熟練の革職人が仕立てた丈夫な鞄。留め具には美しい模様が刻まれている。", 2, 2, 1, false),
            new Spec(22, "貴族の銀製留め具", "180", 0.09f, "外套を留めるために使われていた銀製の装飾品。細かな彫刻が施されている。", 1, 1, 3, false),
            new Spec(23, "アメジスト", "230", 0.009f, "淡い紫色に輝く宝石。美しい色合いから装飾品として人気がある。", 1, 1, 5, false),
            new Spec(24, "ガーネット", "290", 0.007f, "深紅の輝きを持つ宝石。古くから旅人のお守りとして大切にされてきた。", 1, 1, 5, false),
            new Spec(25, "修道院の写本", "310", 1.1f, "修道士によって手書きされた古い書物。宗教的な教えが丁寧に記されている。", 2, 2, 1, true),
            new Spec(26, "装飾された短剣", "430", 0.7f, "柄に銀細工が施された短剣。貴族が護身用として携えていたもの。", 1, 2, 1, true),
            new Spec(27, "サファイア", "520", 0.006f, "澄んだ青色の美しい宝石。高貴な人物が身につける装飾品に用いられる。", 1, 1, 5, false),
            new Spec(28, "騎士の鉄兜", "650", 2.4f, "熟練の鍛冶職人によって作られた兜。表面には戦闘による傷が残っている。", 2, 2, 1, false),
            new Spec(29, "エメラルド", "720", 0.005f, "鮮やかな緑色をした希少な宝石。内部には自然に生じた小さな模様が見える。", 1, 1, 5, false),
            new Spec(30, "石彫りの聖人像", "730", 3.8f, "修道院に安置されていた小さな石像。細部まで丁寧に彫り込まれている。", 2, 3, 1, true),
            new Spec(31, "銀の香炉", "820", 1.6f, "礼拝の際に香を焚くための銀製香炉。表面には宗教的な模様が刻まれている。", 2, 2, 1, false),
            new Spec(32, "ルビー", "850", 0.005f, "燃えるような赤色に輝く宝石。王侯貴族の装飾品として珍重されている。", 1, 1, 5, false),
            new Spec(33, "騎士の胸甲", "920", 5f, "熟練の職人が鍛えた鉄製の胸甲。頑丈な造りで、保存状態も良好である。", 3, 3, 1, true),
            new Spec(34, "ダイヤモンド", "980", 0.003f, "透き通った輝きを放つ極めて硬い宝石。希少性が高く、宝石商の間で高値で取引される。", 1, 1, 5, false),
            new Spec(35, "貴族の宝石箱", "1140", 1.4f, "精巧な木細工と銀の装飾が施された小箱。内部には小さな装身具が残されている。", 2, 2, 1, false),
            new Spec(36, "騎士団の紋章旗", "1260", 2.1f, "名誉ある騎士団が掲げていた旗。色鮮やかな刺繍が施されている。", 2, 3, 1, true),
            new Spec(37, "金糸の祭壇布", "1390", 1.8f, "教会の祭壇を覆っていた豪華な織物。金糸による精巧な刺繍が美しい。", 2, 2, 1, true),
            new Spec(38, "司祭の装飾十字架", "1520", 1.5f, "高位聖職者が儀式で使用していた十字架。銀細工と宝石で飾られている。", 1, 3, 1, true),
            new Spec(39, "鍛造の儀礼剣", "1720", 1.7f, "騎士の叙任式に使われていた儀礼用の長剣。刃には古い祈りの言葉が刻まれている。", 1, 4, 1, true),
            new Spec(40, "小型の青銅鐘", "1950", 6.5f, "礼拝の合図に使用されていた青銅製の鐘。美しい音色を響かせる。", 2, 3, 1, true),
            new Spec(41, "貴族家の銀食器箱", "2250", 5.5f, "銀製の食器が収められた箱。すべての食器に同じ家紋が刻まれている。", 3, 2, 1, true),
            new Spec(42, "宝石装飾の聖遺物箱", "2750", 3.2f, "聖人ゆかりの品を保管するために作られた箱。金銀細工と宝石で豪華に飾られている。", 2, 3, 1, true),
            new Spec(43, "公爵の紋章盾", "3300", 3.5f, "公爵家の紋章が施された儀礼用の盾。金銀の装飾がふんだんに使用されている。", 3, 3, 1, true),
            new Spec(44, "宝石細工の金飾り帯", "3900", 0.85f, "高位貴族が身につけていた装飾帯。黄金の留め具には希少な宝石が埋め込まれている。", 3, 1, 1, true),
            new Spec(45, "修道院の彩飾写本", "4500", 1.8f, "貴重な顔料と金箔を使って描かれた豪華な写本。完成までに数十年を要したとされる。", 2, 2, 1, true),
            new Spec(46, "銀象嵌の甲冑", "5100", 13f, "銀の装飾が施された高級甲冑。かつて名高い騎士団長が所有していたと伝わる。", 3, 4, 1, true),
            new Spec(47, "黄金の聖杯", "5700", 1.3f, "黄金で作られた礼拝用の聖杯。側面には色鮮やかな宝石が埋め込まれている。", 2, 2, 1, false),
            new Spec(48, "金細工の祭壇像", "6400", 7.5f, "大聖堂の祭壇に祀られていた聖人像。金箔と繊細な金細工が施されている。", 3, 4, 1, true),
            new Spec(49, "王家の宝物櫃", "7200", 9.5f, "王族の財宝を保管するために作られた小型の櫃。内部には古い金貨や装飾品が収められている。", 3, 3, 1, true),
            new Spec(50, "大聖堂の宝石聖像", "7900", 8f, "大聖堂の奥に安置されていた貴重な聖像。黄金と数多くの宝石で飾られている。", 3, 4, 1, true),
    };


    // 財宝50種類のうち12種類だけをカスタム形状にする。
    // 行は上から下。# が占有マス、. が空きマス。座標系は ItemData の (0,0)=左上。
    private sealed class PuzzleShape
    {
        public readonly int No;
        public readonly string[] Rows;
        public int Width => Rows[0].Length;
        public int Height => Rows.Length;

        public PuzzleShape(int no, params string[] rows)
        {
            No = no;
            Rows = rows;
        }

        public bool Occupies(int x, int y) => Rows[y][x] == '#';
    }

    private static readonly PuzzleShape[] PuzzleShapes =
    {
        new PuzzleShape( 4, "#.#", "###"),                 // 馬蹄：U字
        new PuzzleShape( 5, "##.", ".##", "..#"),         // 鎖：ジグザグ
        new PuzzleShape( 9, "###", "###", ".#."),         // 木盾：盾形
        new PuzzleShape(10, "##", ".#", ".#"),            // 手斧：L字
        new PuzzleShape(26, ".#", "##", ".#"),            // 短剣：片側突起
        new PuzzleShape(28, "###", "#.#"),                // 鉄兜：逆U字
        new PuzzleShape(31, ".#.", "###", "#.#"),         // 香炉：脚付き
        new PuzzleShape(38, ".#.", "###", ".#."),         // 十字架：十字型
        new PuzzleShape(40, ".#.", "###", "###"),         // 青銅鐘：ベル形
        new PuzzleShape(43, ".#.", "###", "###", ".#."),  // 紋章盾：菱形風
        new PuzzleShape(46, ".#.", "###", "###", "#.#"),  // 甲冑：左右の脚
        new PuzzleShape(48, ".#.", "###", ".#.", "###")   // 祭壇像：台座付き
    };

    private static PuzzleShape FindPuzzleShape(int no)
    {
        for (int i = 0; i < PuzzleShapes.Length; i++)
            if (PuzzleShapes[i].No == no) return PuzzleShapes[i];
        return null;
    }

    private static bool ShapeMatches(SerializedObject so, PuzzleShape shape)
    {
        if (so.FindProperty("width").intValue != shape.Width ||
            so.FindProperty("height").intValue != shape.Height ||
            !so.FindProperty("canRotate").boolValue ||
            !so.FindProperty("useCustomShape").boolValue)
            return false;
        SerializedProperty cells = so.FindProperty("customShapeCells");
        int count = 0;
        for (int y = 0; y < shape.Height; y++)
            for (int x = 0; x < shape.Width; x++)
                if (shape.Occupies(x, y)) count++;
        if (cells.arraySize != count) return false;
        // 並び順の違いは許容する（Unity側で手動並べ替えしていても同じ形状）。
        HashSet<Vector2Int> have = new HashSet<Vector2Int>();
        for (int i = 0; i < cells.arraySize; i++)
            have.Add(cells.GetArrayElementAtIndex(i).vector2IntValue);
        if (have.Count != count) return false;
        for (int y = 0; y < shape.Height; y++)
            for (int x = 0; x < shape.Width; x++)
                if (shape.Occupies(x, y) && !have.Contains(new Vector2Int(x, y))) return false;
        return true;
    }

    private static void AssignPuzzleShape(SerializedObject so, PuzzleShape shape)
    {
        so.FindProperty("width").intValue = shape.Width;
        so.FindProperty("height").intValue = shape.Height;
        so.FindProperty("canRotate").boolValue = true;
        so.FindProperty("useCustomShape").boolValue = true;
        SerializedProperty cells = so.FindProperty("customShapeCells");
        cells.ClearArray();
        int index = 0;
        for (int y = 0; y < shape.Height; y++)
            for (int x = 0; x < shape.Width; x++)
                if (shape.Occupies(x, y))
                {
                    cells.InsertArrayElementAtIndex(index);
                    cells.GetArrayElementAtIndex(index).vector2IntValue = new Vector2Int(x, y);
                    index++;
                }
    }

    // 既存Itemの ID/名前/説明/価格/重量/Icon/スタック数は一切書き換えない。
    // 重複した候補がある場合には誤変更を避けるためスキップする。
    private void ApplyPuzzleShapesToExisting()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Play中は実行不可", "Playモードを終了してから実行してください。", "OK");
            return;
        }
        if (!EditorUtility.DisplayDialog("12種類の形状を更新", 
            "既存の財宝ItemDataのうち12種類だけをカスタム形状に変更します。\n\n" +
            "ID・名前・価格・重さ・説明・アイコンは変更しません。\n" +
            "既存セーブの配置とマスが重なる可能性があるため、必ずバックアップしてください。\n\n" +
            (overwriteExistingCustomShapes ? "【注意】手動編集済み形状も上書きします。" : "手動編集済みの形状はスキップします。") +
            "\n続行しますか？", "12種類を更新", "キャンセル")) return;

        Dictionary<int, List<TreasureItemData>> candidates = new Dictionary<int, List<TreasureItemData>>();
        foreach (PuzzleShape preset in PuzzleShapes)
            candidates[preset.No] = new List<TreasureItemData>();

        foreach (string guid in AssetDatabase.FindAssets("t:TreasureItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string file = Path.GetFileNameWithoutExtension(path);
            TreasureItemData item = AssetDatabase.LoadAssetAtPath<TreasureItemData>(path);
            if (item == null) continue;
            SerializedObject so = new SerializedObject(item);
            string name = so.FindProperty("displayName").stringValue;
            foreach (PuzzleShape preset in PuzzleShapes)
            {
                string prefix = "Treasure_" + preset.No.ToString("D2", CultureInfo.InvariantCulture);
                if (!(file == prefix || file.StartsWith(prefix + "_", StringComparison.Ordinal))) continue;
                if (name != Catalog[preset.No - 1].Name) continue;
                candidates[preset.No].Add(item);
            }
        }

        int updated = 0, alreadyCorrect = 0, skippedManual = 0, missing = 0, duplicates = 0;
        List<string> problems = new List<string>();
        foreach (PuzzleShape preset in PuzzleShapes)
        {
            List<TreasureItemData> matches = candidates[preset.No];
            if (matches.Count == 0) { missing++; problems.Add(preset.No.ToString("D2") + ": 対象Assetが見つかりません"); continue; }
            if (matches.Count > 1) { duplicates++; problems.Add(preset.No.ToString("D2") + ": 同名候補が複数あるためスキップ"); continue; }
            TreasureItemData item = matches[0];
            SerializedObject so = new SerializedObject(item);
            if (ShapeMatches(so, preset)) { alreadyCorrect++; continue; }
            if (!overwriteExistingCustomShapes &&
                so.FindProperty("useCustomShape").boolValue &&
                so.FindProperty("customShapeCells").arraySize > 0)
            {
                skippedManual++;
                problems.Add(preset.No.ToString("D2") + ": 設定済みカスタム形状を保護");
                continue;
            }
            Undo.RecordObject(item, "財宝12種 カスタム形状を設定");
            AssignPuzzleShape(so, preset);
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(item);
            updated++;
        }
        AssetDatabase.SaveAssets();
        lastShapeReport = "更新: " + updated + "/12、設定済み: " + alreadyCorrect +
            "、手動形状保護: " + skippedManual + "、未検出: " + missing +
            "、重複: " + duplicates + (problems.Count > 0 ? "\n" + string.Join("\n", problems.ToArray()) : "");
        Debug.Log("[Treasure50BatchImport] " + lastShapeReport);
        EditorUtility.DisplayDialog("カスタム形状の更新結果", lastShapeReport, "OK");
        Repaint();
    }

    private void DrawShapePreview(Spec spec)
    {
        PuzzleShape shape = FindPuzzleShape(spec.No);
        if (shape == null) return;
        GUILayout.Space(6);
        EditorGUILayout.LabelField("パズル形状（12種類の対象）", EditorStyles.boldLabel);
        const float tile = 25f;
        Rect area = GUILayoutUtility.GetRect(1f, shape.Height * (tile + 3f) + 6f, GUILayout.ExpandWidth(true));
        for (int y = 0; y < shape.Height; y++)
            for (int x = 0; x < shape.Width; x++)
            {
                Rect r = new Rect(area.x + 6f + x * (tile + 3f), area.y + 2f + y * (tile + 3f), tile, tile);
                EditorGUI.DrawRect(r, shape.Occupies(x, y) ? Mint : new Color(0.23f, 0.27f, 0.28f));
            }
        EditorGUILayout.LabelField(shape.Width + "×" + shape.Height + " / 回転可能", EditorStyles.miniLabel);
    }

    [SerializeField] private ItemDataDatabase database;
    [SerializeField] private string iconFolderPath = DefaultIconFolder;
    private string lastIconReport = "";
    [SerializeField] private string outputFolder = DefaultOutput;
    [SerializeField] private PriceConversion priceConversion = PriceConversion.RoundUpToWholeG;
    [SerializeField] private bool autoRegister = true;
    [SerializeField] private bool fillMissingIconsOnExisting = false;
    [SerializeField] private bool overwriteExistingCustomShapes = false;
    private string lastShapeReport = "";
    [SerializeField] private int selectedNo = 1;
    private Vector2 listScroll;
    private Vector2 detailScroll;

    [MenuItem("Tools/2D Shooter/Item Studio - 財宝50種を一括生成")]
    public static void Open()
    {
        var window = GetWindow<Treasure50BatchImportWindow>("財宝50種 一括作成");
        window.minSize = new Vector2(850f, 600f);
        window.Show();
    }

    private void OnEnable()
    {
        // Unity Project内の画像の位置がZIP解凍時に変わった場合も復旧する。
        if (CountPngFiles(iconFolderPath) == 0)
            AutoDetectIconFolder(false);
        if (database == null)
        {
            string[] dbAssets = AssetDatabase.FindAssets("t:ItemDataDatabase");
            if (dbAssets.Length == 1)
                database = AssetDatabase.LoadAssetAtPath<ItemDataDatabase>(AssetDatabase.GUIDToAssetPath(dbAssets[0]));
        }
    }

    private void OnGUI()
    {
        Rect header = GUILayoutUtility.GetRect(1, 60, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(header, Header);
        GUIStyle headline = new GUIStyle(EditorStyles.boldLabel) { fontSize = 19 };
        headline.normal.textColor = Color.white;
        GUI.Label(new Rect(header.x + 16, header.y + 6, header.width - 28, 25), "TREASURE ITEM PACK — 50", headline);
        GUIStyle small = new GUIStyle(EditorStyles.miniLabel);
        small.normal.textColor = Mint;
        GUI.Label(new Rect(header.x + 17, header.y + 36, header.width - 30, 18),
            "ドット絵Icon / 7×10 Inventory想定 / ID自動発行 / DBへ一括登録", small);
        GUILayout.Space(10f);

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.BeginVertical(GUILayout.Width(335));
        DrawSettings();
        EditorGUILayout.EndVertical();
        GUILayout.Space(10);
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        DrawCatalog();
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSettings()
    {
        GUILayout.Label("01  一括生成の設定", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("元リストの名前・説明・重量(kg換算)・価格を使って、売却用の TreasureItemData を50個作成します。", MessageType.Info);
        database = (ItemDataDatabase)EditorGUILayout.ObjectField("ItemDataDatabase", database, typeof(ItemDataDatabase), false);
        outputFolder = EditorGUILayout.TextField(".assetの保存先", outputFolder);
        GUILayout.Space(5);
        GUILayout.Label("PNG / Sprite 読み込み先", EditorStyles.boldLabel);
        iconFolderPath = EditorGUILayout.TextField("PNGのフォルダ", iconFolderPath);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("画像フォルダを自動検出", GUILayout.Height(25)))
            AutoDetectIconFolder(true);
        if (GUILayout.Button("フォルダを選択", GUILayout.Height(25)))
        {
            UnityEngine.Object folder = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(iconFolderPath);
            if (folder != null) { Selection.activeObject = folder; EditorGUIUtility.PingObject(folder); }
        }
        EditorGUILayout.EndHorizontal();
        int pngCount = CountPngFiles(iconFolderPath);
        int spriteCount = CountSprites();
        EditorGUILayout.HelpBox("現在の認識: PNG " + pngCount + "/50、Sprite " + spriteCount + "/50\n" +
            "検索先: " + iconFolderPath +
            (pngCount == 0 ? "\n画像をZIPからAssets/Treasure50Pack/Iconsへコピーするか、自動検出を実行してください。" : ""),
            pngCount == 50 && spriteCount == 50 ? MessageType.Info : MessageType.Warning);
        if (!string.IsNullOrEmpty(lastIconReport))
            EditorGUILayout.HelpBox(lastIconReport, MessageType.Info);
        autoRegister = EditorGUILayout.Toggle("Databaseへ自動登録", autoRegister);
        fillMissingIconsOnExisting = EditorGUILayout.Toggle("既存の空Iconのみ補完", fillMissingIconsOnExisting);
        priceConversion = (PriceConversion)EditorGUILayout.EnumPopup("小数価格の換算", priceConversion);

        if (priceConversion == PriceConversion.RoundUpToWholeG)
            EditorGUILayout.HelpBox("通常モード：整数Gへ切り上げ。元リストの0.1/0.3/0.6G→1G、1.5G→2G、2.5G→3Gになります。元の正確な数値はCSVに保存しています。", MessageType.Warning);
        else
            EditorGUILayout.HelpBox("上級モード：ゲーム全体で『1整数=0.1G』を採用する場合のみ使用。全50件の価格を10倍の整数で保存します。既存の所持金・UI表示・売買ロジックも同単位で揃える必要があります。", MessageType.Warning);

        if (database == null && autoRegister)
            EditorGUILayout.HelpBox("Database未設定。この状態では登録せずAssetのみ作成されます。実際にゲームが使うDatabaseを指定してください。", MessageType.Error);

        GUILayout.Space(8);
        if (GUILayout.Button("① PNGをSprite / Pointに設定", GUILayout.Height(33)))
            ConfigureAllIcons(true);
        if (GUILayout.Button("② 財宝50種類を生成・登録", GUILayout.Height(40)))
            CreateAllItems();
        if (GUILayout.Button("③ 既存の財宝Itemの空Iconを補完", GUILayout.Height(32)))
            RepairExistingEmptyIcons();
        GUILayout.Space(8);
        GUILayout.Label("04  パズル形状（12種類のみ）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("既存50件を生成し直さずに、該当12件のWidth/Heightと占有マスだけを更新します。残り38件とID・アイコン・価格などには触れません。", MessageType.Info);
        overwriteExistingCustomShapes = EditorGUILayout.ToggleLeft("手動編集済みの形状も上書きする（通常OFF）", overwriteExistingCustomShapes);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("④ 既存の財宝12種類にカスタム形状を適用", GUILayout.Height(39)))
                ApplyPuzzleShapesToExisting();
        }
        if (!string.IsNullOrEmpty(lastShapeReport))
            EditorGUILayout.HelpBox(lastShapeReport, MessageType.Info);
        GUILayout.Space(6);
        GUILayout.Label("既存Assetがある場合は上書きしません。", EditorStyles.wordWrappedMiniLabel);
        GUILayout.Label("IDは既存Itemの最大 item_番号の次から発行。", EditorStyles.wordWrappedMiniLabel);
        GUILayout.Space(12);
        GUILayout.Label("02  選択アイテム", EditorStyles.boldLabel);
        Spec spec = Catalog[Mathf.Clamp(selectedNo - 1, 0, Catalog.Length - 1)];
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(spec.No));
        Rect rect = GUILayoutUtility.GetRect(270, 155, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(rect, new Color(0.10f, 0.14f, 0.16f));
        if (sprite != null && sprite.texture != null)
        {
            // AssetPreviewは非同期生成のため、プレビュー未完成時はPNG本体で代用する。
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath(spec.No));
            if (texture == null) texture = AssetPreview.GetAssetPreview(sprite);
            if (texture == null) texture = AssetPreview.GetMiniThumbnail(sprite);
            if (texture != null)
            {
                float size = Mathf.Min(130, rect.height - 16);
                GUI.DrawTexture(new Rect(rect.center.x - size/2, rect.center.y - size/2, size, size),
                    texture, ScaleMode.ScaleToFit, true);
            }
        }
        else GUI.Label(rect, "画像が見つからない場合は①を実行", EditorStyles.centeredGreyMiniLabel);
        EditorGUILayout.LabelField(spec.No.ToString("D2") + "  " + spec.Name, EditorStyles.boldLabel);
        EditorGUILayout.LabelField("元価格", spec.OriginalPrice + " G");
        EditorGUILayout.LabelField("重量", spec.WeightKg.ToString("0.###", CultureInfo.InvariantCulture) + " kg");
        PuzzleShape shape = FindPuzzleShape(spec.No);
        EditorGUILayout.LabelField("マスサイズ", shape != null ? shape.Width + " × " + shape.Height + "（カスタム）" : spec.Width + " × " + spec.Height);
        EditorGUILayout.LabelField("最大スタック", spec.Stack.ToString());
        EditorGUILayout.HelpBox(spec.Description, MessageType.None);
        DrawShapePreview(spec);
    }

    private void DrawCatalog()
    {
        GUILayout.Label("03  ITEM CATALOG  |  全50種類", EditorStyles.boldLabel);
        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.ExpandHeight(true));
        for (int i = 0; i < Catalog.Length; i++)
        {
            Spec spec = Catalog[i];
            EditorGUILayout.BeginHorizontal(EditorStyles.helpBox, GUILayout.Height(42));
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(spec.No));
            Texture thumbnail = sprite != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath(spec.No)) : null;
            if (thumbnail == null && sprite != null) thumbnail = AssetPreview.GetMiniThumbnail(sprite);
            if (thumbnail != null)
                GUILayout.Label(thumbnail, GUILayout.Width(36), GUILayout.Height(36));
            else GUILayout.Box("◇", GUILayout.Width(36), GUILayout.Height(36));
            EditorGUILayout.BeginVertical();
            if (GUILayout.Button((spec.No == selectedNo ? "▶ " : "") + spec.No.ToString("D2") + ". " + spec.Name,
                EditorStyles.linkLabel, GUILayout.Height(22)))
                selectedNo = spec.No;
            PuzzleShape shape = FindPuzzleShape(spec.No);
            GUILayout.Label((shape == null ? spec.Width + "×" + spec.Height : "◆ " + shape.Width + "×" + shape.Height) + "  /  " + spec.WeightKg.ToString("0.###", CultureInfo.InvariantCulture) + "kg  /  " + spec.OriginalPrice + "G",
                EditorStyles.miniLabel);
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();
    }

    private string IconPath(int no)
    {
        return iconFolderPath.TrimEnd('/', '\\') + "/Treasure_" + no.ToString("D2") + ".png";
    }

    private static string PathFor(string folder, int no)
    {
        return folder.TrimEnd('/', '\\') + "/Treasure_" + no.ToString("D2") + ".png";
    }

    private static int CountPngFiles(string folder)
    {
        if (string.IsNullOrWhiteSpace(folder)) return 0;
        int found = 0;
        for (int i = 1; i <= 50; i++)
        {
            // Sprite設定前はTexture2Dとしてインポートされる。
            if (AssetImporter.GetAtPath(PathFor(folder, i)) is TextureImporter) found++;
        }
        return found;
    }

    private int CountSprites()
    {
        int count = 0;
        for (int i = 1; i <= 50; i++)
            if (AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(i)) != null) count++;
        return count;
    }

    private void AutoDetectIconFolder(bool showDialog)
    {
        int bestCount = CountPngFiles(iconFolderPath);
        string bestPath = iconFolderPath;
        // ZIP内のAssetsをさらにAssetsへ入れてしまった場合等も検索できる。
        foreach (string guid in AssetDatabase.FindAssets("Treasure_01"))
        {
            string file = AssetDatabase.GUIDToAssetPath(guid);
            if (!file.EndsWith("/Treasure_01.png", StringComparison.OrdinalIgnoreCase)) continue;
            string folder = file.Substring(0, file.LastIndexOf('/'));
            int count = CountPngFiles(folder);
            if (count > bestCount)
            {
                bestCount = count;
                bestPath = folder;
            }
        }
        iconFolderPath = bestPath;
        lastIconReport = bestCount > 0
            ? "画像フォルダ: " + iconFolderPath + "（PNG " + bestCount + "/50）"
            : "UnityのAssets内に Treasure_01.png を見つけられませんでした。ZIPのIconsフォルダをAssets以下へコピーしてください。";
        if (showDialog)
            EditorUtility.DisplayDialog("財宝アイコンの検索", lastIconReport, "OK");
        Repaint();
    }

    private void ConfigureAllIcons(bool verbose)
    {
        AssetDatabase.Refresh();
        if (CountPngFiles(iconFolderPath) == 0)
            AutoDetectIconFolder(false);
        int configured = 0, missing = 0, spriteFailed = 0;
        for (int i = 1; i <= 50; i++)
        {
            string path = IconPath(i);
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) { missing++; continue; }
            bool changed = importer.textureType != TextureImporterType.Sprite ||
                importer.spriteImportMode != SpriteImportMode.Single ||
                importer.filterMode != FilterMode.Point || importer.mipmapEnabled ||
                importer.textureCompression != TextureImporterCompression.Uncompressed ||
                !Mathf.Approximately(importer.spritePixelsPerUnit, 48f) ||
                !importer.alphaIsTransparency;
            if (changed)
            {
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.filterMode = FilterMode.Point;
                importer.mipmapEnabled = false;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.spritePixelsPerUnit = 48f;
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }
            // 以前の設定にキャッシュが残った場合も、明示的に再インポート。
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) == null)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            if (AssetDatabase.LoadAssetAtPath<Sprite>(path) != null) configured++;
            else spriteFailed++;
        }
        lastIconReport = "Sprite成功: " + configured + "/50、PNG未検出: " + missing +
            "、インポート失敗: " + spriteFailed + "\n検索先: " + iconFolderPath;
        Debug.Log("[Treasure50BatchImport] " + lastIconReport);
        if (verbose)
            EditorUtility.DisplayDialog("ドット絵アイコン", lastIconReport +
                "\n既にItemDataを作成した場合は、③空Iconを補完 も押してください。", "OK");
        Repaint();
    }

    private void RepairExistingEmptyIcons()
    {
        if (CountSprites() == 0)
        {
            ConfigureAllIcons(false);
            if (CountSprites() == 0)
            {
                EditorUtility.DisplayDialog("Spriteが未設定", "認識できるSpriteがありません。画像フォルダを自動検出し、①を押してからやり直してください。", "OK");
                return;
            }
        }
        int restored = 0, alreadySet = 0, missingSprites = 0, candidates = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:TreasureItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string filename = Path.GetFileNameWithoutExtension(path);
            // この50種の自動生成アセット以外を誤って変更しない。
            if (filename == null || filename.Length != 11 ||
                !filename.StartsWith("Treasure_", StringComparison.Ordinal)) continue;
            if (!int.TryParse(filename.Substring(9), out int no) || no < 1 || no > 50) continue;
            candidates++;
            TreasureItemData item = AssetDatabase.LoadAssetAtPath<TreasureItemData>(path);
            if (item == null) continue;
            if (item.Icon != null) { alreadySet++; continue; }
            Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(no));
            if (icon == null) { missingSprites++; continue; }
            Undo.RecordObject(item, "財宝Itemの空Iconを補完");
            SerializedObject so = new SerializedObject(item);
            SerializedProperty iconProperty = so.FindProperty("icon");
            if (iconProperty == null) continue;
            iconProperty.objectReferenceValue = icon;
            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(item);
            restored++;
        }
        AssetDatabase.SaveAssets();
        lastIconReport = "既存財宝Item: " + candidates + "件、今回Icon補完: " + restored +
            "件、設定済み: " + alreadySet + "件、Sprite未検出: " + missingSprites + "件";
        Debug.Log("[Treasure50BatchImport] " + lastIconReport);
        EditorUtility.DisplayDialog("既存Itemのアイコン補完", lastIconReport, "OK");
        Repaint();
    }

    private void CreateAllItems()
    {
        if (string.IsNullOrWhiteSpace(outputFolder) || !outputFolder.StartsWith("Assets/", StringComparison.Ordinal))
        {
            EditorUtility.DisplayDialog("保存先エラー", "保存先は Assets/ から始まるフォルダを指定してください。", "OK");
            return;
        }
        if (autoRegister && database == null &&
            !EditorUtility.DisplayDialog("Database未設定", "ItemDataDatabaseが未指定です。DBへ登録せずにItemDataだけ作成しますか？", "作成", "キャンセル"))
            return;
        string amountLabel = priceConversion == PriceConversion.RoundUpToWholeG ? "整数G（小数は切り上げ）" : "全価格10倍の整数";
        if (!EditorUtility.DisplayDialog("財宝50種類を生成", "既存同名Assetは変更しません。新規ItemのIDだけを自動採番します。\n価格の設定: " + amountLabel + "\n続けますか？", "作成する", "キャンセル"))
            return;
        ConfigureAllIcons(false);
        int spritesReady = CountSprites();
        if (spritesReady != 50)
        {
            EditorUtility.DisplayDialog("アイコン不足", "Sprite " + spritesReady + "/50 しか認識されていません。\n\n" +
                "一旦作成を止めます。画像フォルダを自動検出し、① PNGをSprite / Pointに設定 を実行してください。\n" +
                "検索先: " + iconFolderPath, "OK");
            return;
        }
        CreateFolders(outputFolder);
        HashSet<string> used = CollectUsedIds();
        long nextNumber = GetNextNumber(used);
        int created = 0, skipped = 0, missingIcons = 0, registered = 0;
        List<string> issues = new List<string>();
        try
        {
            for (int i = 0; i < Catalog.Length; i++)
            {
                Spec spec = Catalog[i];
                EditorUtility.DisplayProgressBar("財宝50種類を生成", spec.Name, (i + 1f) / Catalog.Length);
                string path = outputFolder.TrimEnd('/') + "/Treasure_" + spec.No.ToString("D2") + ".asset";
                TreasureItemData asset = AssetDatabase.LoadAssetAtPath<TreasureItemData>(path);
                if (asset == null && AssetDatabase.LoadMainAssetAtPath(path) != null)
                {
                    issues.Add("別種類のAssetが存在: " + path);
                    continue;
                }
                Sprite icon = AssetDatabase.LoadAssetAtPath<Sprite>(IconPath(spec.No));
                if (icon == null) missingIcons++;
                if (asset != null)
                {
                    skipped++;
                    if (fillMissingIconsOnExisting && asset.Icon == null && icon != null)
                    {
                        SerializedObject existing = new SerializedObject(asset);
                        existing.FindProperty("icon").objectReferenceValue = icon;
                        existing.ApplyModifiedPropertiesWithoutUndo();
                        EditorUtility.SetDirty(asset);
                    }
                }
                else
                {
                    if (nextNumber <= 0 || nextNumber == long.MaxValue)
                    {
                        issues.Add("ID番号上限に到達。作成を停止しました。");
                        break;
                    }
                    string id = MakeNewId(used, ref nextNumber);
                    asset = CreateInstance<TreasureItemData>();
                    asset.name = "Treasure_" + spec.No.ToString("D2") + "_" + spec.Name;
                    SerializedObject so = new SerializedObject(asset);
                    so.FindProperty("itemId").stringValue = id;
                    so.FindProperty("displayName").stringValue = spec.Name;
                    so.FindProperty("description").stringValue = spec.Description;
                    so.FindProperty("icon").objectReferenceValue = icon;
                    PuzzleShape shape = FindPuzzleShape(spec.No);
                    if (shape != null) AssignPuzzleShape(so, shape);
                    else
                    {
                        so.FindProperty("width").intValue = spec.Width;
                        so.FindProperty("height").intValue = spec.Height;
                        so.FindProperty("canRotate").boolValue = spec.CanRotate;
                        so.FindProperty("useCustomShape").boolValue = false;
                        so.FindProperty("customShapeCells").ClearArray();
                    }
                    so.FindProperty("maxStack").intValue = spec.Stack;
                    int convertedPrice = PriceToInteger(spec.OriginalPrice, priceConversion);
                    // 価格の用途が明記されていないため購入/売却は同一値（倍率を創作しない）。
                    so.FindProperty("purchasePrice").intValue = convertedPrice;
                    so.FindProperty("sellPrice").intValue = convertedPrice;
                    so.FindProperty("canSellToShop").boolValue = true;
                    so.FindProperty("weight").floatValue = spec.WeightKg;
                    so.FindProperty("canDiscard").boolValue = true;
                    so.ApplyModifiedPropertiesWithoutUndo();
                    // ItemDataのローカライズ用キャッシュはOnEnableで初期化済みなので、
                    // 実行中のEditorでも作成した日本語名・説明がすぐ表示されるよう合わせる。
                    SyncFallbackCache(asset, spec);
                    AssetDatabase.CreateAsset(asset, path);
                    EditorUtility.SetDirty(asset);
                    created++;
                }
                if (autoRegister && database != null && RegisterIntoDatabase(database, asset))
                    registered++;
            }
        }
        catch (Exception ex)
        {
            issues.Add(ex.Message);
            Debug.LogException(ex);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }
        string report = "新規生成: " + created + " / 既存スキップ: " + skipped +
            "\nDBへ追加: " + registered + " / Icon読込不可: " + missingIcons +
            (issues.Count > 0 ? "\n注意:\n" + string.Join("\n", issues.ToArray()) : "");
        Debug.Log("[Treasure50BatchImport] " + report);
        EditorUtility.DisplayDialog("財宝50種類の生成結果", report, "OK");
    }

    private static void SyncFallbackCache(ItemData asset, Spec spec)
    {
        BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        Type baseType = typeof(ItemData);
        FieldInfo display = baseType.GetField("currentDisplayName", flags);
        FieldInfo description = baseType.GetField("currentDescription", flags);
        if (display != null) display.SetValue(asset, spec.Name);
        if (description != null) description.SetValue(asset, spec.Description);
    }

    private static int PriceToInteger(string original, PriceConversion mode)
    {
        decimal price = decimal.Parse(original, CultureInfo.InvariantCulture);
        decimal result = mode == PriceConversion.RoundUpToWholeG ? decimal.Ceiling(price) : price * 10m;
        return (int)Math.Min((decimal)int.MaxValue, result);
    }

    private static void CreateFolders(string folder)
    {
        string[] segments = folder.Replace('\\', '/').Split('/');
        string path = "Assets";
        for (int i = 1; i < segments.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(segments[i])) continue;
            string next = path + "/" + segments[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(path, segments[i]);
            path = next;
        }
    }

    private static HashSet<string> CollectUsedIds()
    {
        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
        List<Type> types = new List<Type> { typeof(ItemData) };
        foreach (Type type in TypeCache.GetTypesDerivedFrom<ItemData>())
            if (type != null && !type.IsAbstract && !type.IsGenericType) types.Add(type);
        foreach (Type type in types)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:" + type.Name))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Add(path)) continue;
                ItemData item = AssetDatabase.LoadAssetAtPath<ItemData>(path);
                if (item != null && !string.IsNullOrWhiteSpace(item.ItemId)) ids.Add(item.ItemId.Trim());
            }
        }
        return ids;
    }

    private static long GetNextNumber(HashSet<string> ids)
    {
        long highest = 0;
        foreach (string id in ids)
        {
            if (!id.StartsWith("item_", StringComparison.Ordinal)) continue;
            if (long.TryParse(id.Substring(5), NumberStyles.Integer, CultureInfo.InvariantCulture, out long n))
                highest = Math.Max(highest, n);
        }
        return highest == long.MaxValue ? -1 : highest + 1;
    }

    private static string MakeNewId(HashSet<string> used, ref long n)
    {
        while (n > 0 && n < long.MaxValue)
        {
            string id = "item_" + n.ToString("D4", CultureInfo.InvariantCulture);
            n++;
            if (used.Add(id)) return id;
        }
        throw new InvalidOperationException("Item IDを生成できませんでした。既存IDの上限を確認してください。");
    }

    private static bool RegisterIntoDatabase(ItemDataDatabase db, ItemData item)
    {
        if (db == null || item == null) return false;
        SerializedObject so = new SerializedObject(db);
        SerializedProperty list = so.FindProperty("itemDataList");
        if (list == null)
        {
            Debug.LogError("ItemDataDatabaseの itemDataList が見つかりません。");
            return false;
        }
        for (int i = 0; i < list.arraySize; i++)
        {
            ItemData other = list.GetArrayElementAtIndex(i).objectReferenceValue as ItemData;
            if (other == item) return false;
            if (other != null && other.ItemId == item.ItemId)
            {
                Debug.LogWarning("[Treasure50BatchImport] DB内に同じIDが存在するため登録しません: " + item.ItemId);
                return false;
            }
        }
        int index = list.arraySize;
        list.InsertArrayElementAtIndex(index);
        list.GetArrayElementAtIndex(index).objectReferenceValue = item;
        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(db);
        return true;
    }
}
#endif
