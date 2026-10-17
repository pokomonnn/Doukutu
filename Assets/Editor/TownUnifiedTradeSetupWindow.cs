#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 町の希少品店・武器屋が共用する売買画面を生成し、既存の会話/商人Controllerへ接続する。
/// 既存Shop/Repair/Homeのオブジェクトやデータは削除しない。
/// </summary>
public sealed class TownUnifiedTradeSetupWindow : EditorWindow
{
    private Canvas canvas;
    private PawnShopUIController pawnShop;
    private PlayerTradeInventoryBridge playerBridge;
    private SellCartInventory sellRules;
    private ExpeditionUnifiedTradeController2D soundTemplate;
    private RectTransform repairPanel;
    private RectTransform homePanel;
    private RectTransform repairEquipmentMount;
    private RectTransform homeEquipmentMount;
    private MerchantWeaponRepairController repairController;
    private InventoryGridUI homeItemBoxGrid;
    private InventoryGridUI homePlayerGrid;
    private ItemBoxInventory homeStorageBox;
    private AudioClip repairOpenSound;
    private AudioClip repairCloseSound;
    private AudioClip repairSelectionSound;
    private AudioClip repairSuccessSound;
    private bool setupHomeScroll = true;
    private TMP_FontAsset japaneseTmpFont;
    private Font japaneseLegacyFont;
    private bool createTrade = true;
    private bool styleRepairAndHome = true;
    private Vector2 scroll;

    private static readonly Color Backdrop = ColorOf("#171B1D");
    private static readonly Color Panel = ColorOf("#242A2C");
    private static readonly Color Cell = ColorOf("#303638");
    private static readonly Color Border = ColorOf("#525A58");
    private static readonly Color Paper = ColorOf("#E5E1D6");
    private static readonly Color Dim = ColorOf("#ABB0AB");
    private static readonly Color Accent = ColorOf("#A79A83");

    [MenuItem("Tools/2D Shooter/町UI/共通売買画面 v3.4 調整・修正")]
    [MenuItem("Tools/2D Shooter/町UI/共通売買画面 v3.3 修正・生成")] 
    [MenuItem("Tools/2D Shooter/町UI/共通売買画面 v3.2 を生成・接続")]
    [MenuItem("Tools/2D Shooter/町UI/共通売買画面 v3.1 を生成・接続")]
    private static void Open()
    {
        GetWindow<TownUnifiedTradeSetupWindow>("町の共通UI v3.4").Show();
    }

    private void OnEnable()
    {
        if (canvas == null) canvas = Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        if (pawnShop == null) pawnShop = Object.FindAnyObjectByType<PawnShopUIController>(FindObjectsInactive.Include);
        if (playerBridge == null) playerBridge = Object.FindAnyObjectByType<PlayerTradeInventoryBridge>(FindObjectsInactive.Include);
        if (sellRules == null) sellRules = Object.FindAnyObjectByType<SellCartInventory>(FindObjectsInactive.Include);
        if (repairController == null) repairController = Object.FindAnyObjectByType<MerchantWeaponRepairController>(FindObjectsInactive.Include);
        if (soundTemplate == null) soundTemplate = Object.FindAnyObjectByType<ExpeditionUnifiedTradeController2D>(FindObjectsInactive.Include);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("町の4画面・シンプルRPG UI v3.4", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "希少品店と武器屋は同じ新規売買画面を使用します。\n" +
            "NPC会話で選択されたMerchantStockInventoryに応じて品物・買取条件が変わります。" +
            "修理画面と自宅保管画面の既存操作は変更しません。", MessageType.Info);

        canvas = (Canvas)EditorGUILayout.ObjectField("町のCanvas", canvas, typeof(Canvas), true);
        pawnShop = (PawnShopUIController)EditorGUILayout.ObjectField("既存PawnShopUIController", pawnShop, typeof(PawnShopUIController), true);
        playerBridge = (PlayerTradeInventoryBridge)EditorGUILayout.ObjectField("PlayerTradeInventoryBridge", playerBridge, typeof(PlayerTradeInventoryBridge), true);
        sellRules = (SellCartInventory)EditorGUILayout.ObjectField("SellCartInventory（推奨）", sellRules, typeof(SellCartInventory), true);
        soundTemplate = (ExpeditionUnifiedTradeController2D)EditorGUILayout.ObjectField("出撃補充のSE設定（任意）", soundTemplate, typeof(ExpeditionUnifiedTradeController2D), true);
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("日本語フォント設定", EditorStyles.boldLabel);
        japaneseTmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "日本語 TMP Font Asset", japaneseTmpFont, typeof(TMP_FontAsset), false);
        japaneseLegacyFont = (Font)EditorGUILayout.ObjectField(
            "従来 UI.Text 用 Font（任意）", japaneseLegacyFont, typeof(Font), false);
        EditorGUILayout.HelpBox(
            "日本語を含むTMP Font Assetを指定してください。空欄なら今のフォントを変更しません。" +
            "下の『既存画面へフォントのみ適用』は新しい売買UIを作り直しません。", MessageType.Info);
        EditorGUILayout.Space(8);
        createTrade = EditorGUILayout.ToggleLeft("希少品店・武器屋の一体型売買画面を作る", createTrade);
        styleRepairAndHome = EditorGUILayout.ToggleLeft("修理・自宅画面の既存UIをシンプル配色に統一", styleRepairAndHome);
        if (styleRepairAndHome)
        {
            repairPanel = (RectTransform)EditorGUILayout.ObjectField("既存の武器修理Panel Root", repairPanel, typeof(RectTransform), true);
            homePanel = (RectTransform)EditorGUILayout.ObjectField("既存の自宅ItemBox Panel Root", homePanel, typeof(RectTransform), true);
            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("装備Slot追加（任意・配置先だけ指定）", EditorStyles.miniBoldLabel);
            repairEquipmentMount = (RectTransform)EditorGUILayout.ObjectField("修理Slotの配置先", repairEquipmentMount, typeof(RectTransform), true);
            homeEquipmentMount = (RectTransform)EditorGUILayout.ObjectField("自宅Slotの配置先", homeEquipmentMount, typeof(RectTransform), true);
        }
        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("v3.4 町UIのGrid・容量・サウンド調整", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "売買Player Gridのホイール操作を修復。商人在庫と自宅保管箱を1回だけ約2倍に拡張。" +
            "自宅Player側は売買Playerと同じCell Size=60にします。出撃補充画面には適用しません。", MessageType.Info);
        setupHomeScroll = EditorGUILayout.ToggleLeft("自宅ItemBoxを縦スクロール対応にする", setupHomeScroll);
        homeItemBoxGrid = (InventoryGridUI)EditorGUILayout.ObjectField("自宅ItemBoxのGridUI", homeItemBoxGrid, typeof(InventoryGridUI), true);
        homePlayerGrid = (InventoryGridUI)EditorGUILayout.ObjectField("自宅PlayerのGridUI", homePlayerGrid, typeof(InventoryGridUI), true);
        homeStorageBox = (ItemBoxInventory)EditorGUILayout.ObjectField("自宅の実ItemBoxInventory【必須】", homeStorageBox, typeof(ItemBoxInventory), true);
        repairController = (MerchantWeaponRepairController)EditorGUILayout.ObjectField("既存の修理Controller", repairController, typeof(MerchantWeaponRepairController), true);
        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("修理画面のSE（任意・出撃のSE設定からも自動補完）", EditorStyles.miniBoldLabel);
        repairOpenSound = (AudioClip)EditorGUILayout.ObjectField("修理画面を開くSE", repairOpenSound, typeof(AudioClip), false);
        repairCloseSound = (AudioClip)EditorGUILayout.ObjectField("修理画面を閉じるSE", repairCloseSound, typeof(AudioClip), false);
        repairSelectionSound = (AudioClip)EditorGUILayout.ObjectField("修理する武器を選択するSE", repairSelectionSound, typeof(AudioClip), false);
        repairSuccessSound = (AudioClip)EditorGUILayout.ObjectField("修理成功SE", repairSuccessSound, typeof(AudioClip), false);
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            if (GUILayout.Button("既存売買画面をセル60＋スクロールに更新", GUILayout.Height(34)))
            {
                int n = UpgradeExistingTradeGrids();
                EditorUtility.DisplayDialog("町の売買画面", n > 0 ? $"{n}個のGridを更新しました。Sceneを保存してください。" : "TownUnifiedTradeRoot内にGridが見つかりません。v3.1/v3.2で先に生成してください。", "OK");
            }
            if (GUILayout.Button("自宅Player小型化＋箱の2倍拡張＋縦スクロール", GUILayout.Height(34)))
            {
                bool done = UpgradeHomeItemBox();
                EditorUtility.DisplayDialog("自宅保管箱", done
                    ? "自宅のPlayerセル60、箱の縦スクロール・容量拡張を適用しました。Sceneを保存してください。"
                    : "自宅ItemBoxのGridUIと実ItemBoxInventory、Player GridUI（または自宅Panel）を設定してください。", "OK");
            }
            if (GUILayout.Button("商人2店舗の在庫Grid縦幅を約2倍に拡張", GUILayout.Height(34)))
            {
                int n = UpgradeMerchantStockRows();
                EditorUtility.DisplayDialog("商人の在庫行数", n > 0
                    ? $"{n}店舗の実Grid縦幅を拡張しました（再実行しても倍増しません）。Sceneを保存してください。"
                    : "同じSceneにMerchantStockInventoryが見つかりません。", "OK");
            }
            using (new EditorGUI.DisabledScope(canvas == null || pawnShop == null || playerBridge == null || repairController == null))
            {
                if (GUILayout.Button("武器修理画面を新UIで生成・接続", GUILayout.Height(38)))
                {
                    bool done = CreateNewRepairScreen() || UpgradeExistingRepairScreen();
                    EditorUtility.DisplayDialog("武器修理画面", done ? "修理画面の表示領域にGridを合わせ、SE参照を更新しました。Sceneを保存してください。" : "修理画面または修理Controllerの配置を確認してください。", "OK");
                }
            }
            if (GUILayout.Button("v3.4の6項目を既存町UIへ一括適用", GUILayout.Height(38)))
            {
                int trade = UpgradeExistingTradeGrids();
                int shops = UpgradeMerchantStockRows();
                bool home = UpgradeHomeItemBox();
                bool repair = CreateNewRepairScreen() || UpgradeExistingRepairScreen();
                EditorUtility.DisplayDialog("町UI v3.4", $"売買Grid={trade} / 商人在庫={shops} / 自宅={(home ? "適用" : "要参照設定")} / 修理={(repair ? "適用" : "要参照設定")}。Sceneを保存してください。", "OK");
            }
            if (GUILayout.Button("町の装備Slotの緑/赤プレビューを更新", GUILayout.Height(32)))
            {
                int n = ApplyEquipmentDropPreviewFix();
                EditorUtility.DisplayDialog("装備プレビュー", $"{n}個の装備Slotに事前判定コンポーネントを設定しました。", "OK");
            }
        }
        EditorGUILayout.Space(12);
        bool cannotBuild = EditorApplication.isPlayingOrWillChangePlaymode ||
                           (createTrade && (canvas == null || pawnShop == null || playerBridge == null));
        using (new EditorGUI.DisabledScope(cannotBuild))
        {
            if (GUILayout.Button("共通UIを生成・接続する", GUILayout.Height(42)))
            {
                if (createTrade && Object.FindAnyObjectByType<TownUnifiedTradeScreen2D>(FindObjectsInactive.Include) != null)
                {
                    EditorUtility.DisplayDialog("既存UIあり", "TownUnifiedTradeScreen2Dがすでにあります。重複を防ぐため新規生成を中止しました。", "OK");
                }
                else
                {
                    if (createTrade) BuildSharedTrade();
                    UpgradeExistingTradeGrids();
                    UpgradeMerchantStockRows();
                    if (styleRepairAndHome) StyleExtraPanels();
                    if (setupHomeScroll) UpgradeHomeItemBox();
                    if (repairController != null && canvas != null && pawnShop != null && playerBridge != null)
                    {
                        if (!CreateNewRepairScreen()) UpgradeExistingRepairScreen();
                    }
                    ApplyEquipmentDropPreviewFix();
                    if (canvas != null && canvas.gameObject.scene.IsValid())
                        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
                    EditorUtility.DisplayDialog("町UI", "共通UIを生成・接続しました。Sceneを保存してPlayモードで確認してください。", "OK");
                }
            }
        }
        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
                  (japaneseTmpFont == null && japaneseLegacyFont == null)))
        {
            if (GUILayout.Button("既存の町画面へ日本語フォントのみ適用", GUILayout.Height(34)))
            {
                int count = ApplyFontsToExistingScreens();
                EditorUtility.DisplayDialog("日本語フォント", 
                    count > 0
                        ? $"日本語フォントを反映しました（変更した文字：{count}個）。Sceneを保存してください。"
                        : "対象画面を見つけられなかったか、すでに指定フォントでした。", "OK");
            }
        }
        EditorGUILayout.HelpBox(
            "実行前にTown_MainのSceneとScriptsをバックアップしてください。" +
            "既存画面の変更は上のv3.4一括適用ボタンから行えます。" +
            "自宅の実ItemBoxInventoryは商人在庫と間違えずに指定してください。" +
            "日本語Fontは以前と同じ設定を使用できます。", MessageType.Warning);
        EditorGUILayout.EndScrollView();
    }

    private int ApplyFontsToExistingScreens()
    {
        int changed = 0;
        var seen = new System.Collections.Generic.HashSet<int>();
        var roots = new System.Collections.Generic.List<Transform>();
        // v3.0/v3.1で既にスキンを付けた画面も、非アクティブを含めて取得する。
        TownSimpleRpgSkin2D[] skins = Object.FindObjectsByType<TownSimpleRpgSkin2D>(FindObjectsInactive.Include);
        foreach (TownSimpleRpgSkin2D skin in skins)
        {
            if (skin == null || skin.ScreenRoot == null) continue;
            if (canvas != null && skin.gameObject.scene != canvas.gameObject.scene) continue;
            Undo.RecordObject(skin, "町UIの日本語フォント設定");
            skin.ConfigureJapaneseFonts(japaneseTmpFont, japaneseLegacyFont);
            EditorUtility.SetDirty(skin);
            Transform root = skin.ScreenRoot;
            if (seen.Add(root.GetInstanceID())) roots.Add(root);
        }
        // Skinコンポーネントがまだ無い既存の修理・自宅Panelにも反映する。
        foreach (RectTransform panel in new[] { repairPanel, homePanel })
        {
            if (panel == null) continue;
            if (seen.Add(panel.GetInstanceID())) roots.Add(panel);
        }
        // Trade画面があるがSkinが外れている場合も、名前から判定して補完する。
        if (canvas != null)
        {
            foreach (RectTransform rect in canvas.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == null || rect.name != "TownUnifiedTradeRoot") continue;
                if (seen.Add(rect.GetInstanceID())) roots.Add(rect);
            }
        }
        foreach (Transform root in roots)
        {
            changed += TownSimpleRpgSkin2D.ApplyFontsToHierarchy(
                root, japaneseTmpFont, japaneseLegacyFont);
            if (root.gameObject.scene.IsValid())
                EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        }
        return changed;
    }

    private void BuildSharedTrade()
    {
        // ManagerはRootの外に配置。Root.SetActive(false)でも出入口は生き続ける。
        GameObject manager = New("TownUnifiedTradeManager", canvas.transform, typeof(TownUnifiedTradeScreen2D),
            typeof(ExpeditionUnifiedTradeController2D), typeof(AudioSource));
        AudioSource audioSource = manager.GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0;

        GameObject root = New("TownUnifiedTradeRoot", canvas.transform, typeof(RectTransform), typeof(Image));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;
        Image overlay = root.GetComponent<Image>();
        overlay.color = ColorOf("#0B1011E8");
        overlay.raycastTarget = true;

        GameObject window = New("TownTradeWindow", root.transform,
            typeof(RectTransform), typeof(Image), typeof(Outline), typeof(CanvasGroup));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(1180, 770);
        window.GetComponent<Image>().color = Backdrop;
        window.GetComponent<Outline>().effectColor = Border;
        window.GetComponent<Outline>().effectDistance = new Vector2(1, -1);

        TextMeshProUGUI title = Label("ScreenTitle", window.transform, "取引", 28, 24, 14, 260, 46, Paper);
        TextMeshProUGUI shopName = Label("ShopName", window.transform, "店舗名", 20, 302, 23, 610, 34, Accent);
        Label("LeftHeading", window.transform, "商品の在庫  /  購入", 21, 26, 83, 500, 32, Paper);
        Label("RightHeading", window.transform, "所持品  /  売却", 21, 613, 83, 200, 32, Paper);
        RectTransform left = Plate("MerchantStockPanel", window.transform, 24, 124, 550, 480);
        RectTransform right = Plate("PlayerStockPanel", window.transform, 605, 124, 550, 480);
        Label("EquipmentHeading", window.transform, "装備中の武器", 15, 696, 42, 135, 26, Dim);

        InventoryGridUI merchantGrid = MakeGrid("TownMerchantGridUI", left, 18, 16, 520, 462);
        InventoryGridUI playerGrid = MakeGrid("TownPlayerGridUI", right, 18, 16, 520, 462);
        TownGridScrollEditorUtility.EnsureScroll(merchantGrid, 520, 462);
        TownGridScrollEditorUtility.EnsureScroll(playerGrid, 520, 462);
        EquipmentSlotUI equipmentSlot = MakeEquipmentSlot("TownEquipmentSlotUI", window.transform, 825, 7, 310, 107);

        RectTransform totals = Plate("TradeTotalsPanel", window.transform, 24, 632, 1130, 116);
        TextMeshProUGUI buyText = Label("BuyTotalText", totals, "買い金額  0 G", 17, 18, 8, 290, 26, Paper);
        TextMeshProUGUI sellText = Label("SellTotalText", totals, "売り金額  0 G", 17, 18, 37, 290, 26, Paper);
        TextMeshProUGUI moneyText = Label("MoneyText", totals, "所持金  ― G", 17, 330, 8, 270, 26, Accent);
        TextMeshProUGUI netText = Label("NetTotalText", totals, "差引  0 G", 19, 330, 42, 320, 32, Accent);
        TextMeshProUGUI statusText = Label("TradeStatusText", window.transform, "商品をクリックで購入、所持品をクリックで売却選択", 14, 28, 606, 1030, 22, Dim);
        Button closeButton = MakeButton("CloseTradeButton", totals, "戻る", 785, 18, 143, 75, false);
        Button checkoutButton = MakeButton("CheckoutButton", totals, "決済する", 944, 18, 168, 75, true);

        GameObject sliderRoot = New("TradeQuantityRoot", window.transform, typeof(RectTransform));
        Place(sliderRoot.GetComponent<RectTransform>(), 605, 554, 550, 45);
        TextMeshProUGUI selectedText = Label("SelectedItemText", sliderRoot.transform, "選択中のアイテム", 14, 0, 0, 180, 24, Paper);
        TextMeshProUGUI quantityText = Label("SelectedQuantityText", sliderRoot.transform, "数量：―", 14, 192, 0, 100, 24, Paper);
        Slider slider = MakeSlider("QuantitySlider", sliderRoot.transform, 304, 3, 230, 22);

        // Controller はUI自体の非アクティブ化で停止させない。
        ExpeditionUnifiedTradeController2D unified = manager.GetComponent<ExpeditionUnifiedTradeController2D>();
        SerializedObject unifiedSO = new SerializedObject(unified);
        SetRef(unifiedSO, "merchantGridUI", merchantGrid);
        SetRef(unifiedSO, "playerGridUI", playerGrid);
        SetRef(unifiedSO, "playerInventory", playerBridge.InventoryController);
        SetRef(unifiedSO, "townInventoryBridge", playerBridge);
        SetRef(unifiedSO, "sellRules", sellRules);
        SetRef(unifiedSO, "buyAmountText", buyText);
        SetRef(unifiedSO, "sellAmountText", sellText);
        SetRef(unifiedSO, "totalAmountText", netText);
        SetRef(unifiedSO, "moneyText", moneyText);
        SetRef(unifiedSO, "statusText", statusText);
        SetRef(unifiedSO, "checkoutButton", checkoutButton);
        SetRef(unifiedSO, "quantitySlider", slider);
        SetRef(unifiedSO, "sliderRoot", sliderRoot);
        SetRef(unifiedSO, "quantityText", quantityText);
        SetRef(unifiedSO, "selectedItemText", selectedText);
        SetRef(unifiedSO, "audioSource", audioSource);
        if (soundTemplate != null)
        {
            SerializedObject source = new SerializedObject(soundTemplate);
            foreach (string clipName in new[] { "selectionClip", "checkoutClip", "failClip" })
            {
                SerializedProperty sourceClip = source.FindProperty(clipName);
                if (sourceClip != null) SetRef(unifiedSO, clipName, sourceClip.objectReferenceValue);
            }
        }
        SetColor(unifiedSO, "buySelectedColor", ColorOf("#B8A68677"));
        SetColor(unifiedSO, "sellSelectedColor", ColorOf("#965D4B88"));
        unifiedSO.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject slotSO = new SerializedObject(equipmentSlot);
        SetRef(slotSO, "equipmentController", playerBridge.EquipmentController);
        slotSO.ApplyModifiedPropertiesWithoutUndo();
        TownUnifiedTradeScreen2D screen = manager.GetComponent<TownUnifiedTradeScreen2D>();
        SerializedObject screenSO = new SerializedObject(screen);
        SetRef(screenSO, "tradeRoot", root);
        SetRef(screenSO, "windowRect", windowRect);
        SetRef(screenSO, "windowCanvasGroup", window.GetComponent<CanvasGroup>());
        SetRef(screenSO, "shopNameText", shopName);
        SetRef(screenSO, "closeButton", closeButton);
        SetRef(screenSO, "tradeController", unified);
        SetRef(screenSO, "pawnShopController", pawnShop);
        SetRef(screenSO, "playerTradeInventory", playerBridge);
        screenSO.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject shopSO = new SerializedObject(pawnShop);
        SetRef(shopSO, "unifiedTownTradeScreen", screen);
        SerializedProperty enabled = shopSO.FindProperty("useUnifiedTownTrade");
        if (enabled != null) enabled.boolValue = true;
        shopSO.ApplyModifiedPropertiesWithoutUndo();

        // ルートを直接装飾。Item / Equipmentの操作コンポーネントは変更しない。
        TownSimpleRpgSkin2D skin = Undo.AddComponent<TownSimpleRpgSkin2D>(root);
        skin.Configure(TownSimpleRpgSkin2D.TownScreenKind.RareGoodsShop, rootRect);
        skin.ConfigureJapaneseFonts(japaneseTmpFont, japaneseLegacyFont);
        skin.ApplyTheme();
        sliderRoot.SetActive(false);
        root.SetActive(false);
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
    }

    private void StyleExtraPanels()
    {
        StyleExisting(repairPanel, TownSimpleRpgSkin2D.TownScreenKind.WeaponRepair);
        StyleExisting(homePanel, TownSimpleRpgSkin2D.TownScreenKind.HomeStorage);
        if (playerBridge == null) return;
        if (repairEquipmentMount != null) EnsureSlotForMount(repairEquipmentMount);
        if (homeEquipmentMount != null) EnsureSlotForMount(homeEquipmentMount);
    }

    private void StyleExisting(RectTransform root, TownSimpleRpgSkin2D.TownScreenKind kind)
    {
        if (root == null) return;
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "町UIのシンプルな配色");
        TownSimpleRpgSkin2D skin = root.GetComponent<TownSimpleRpgSkin2D>();
        if (skin == null) skin = Undo.AddComponent<TownSimpleRpgSkin2D>(root.gameObject);
        skin.Configure(kind, root);
        skin.ConfigureJapaneseFonts(japaneseTmpFont, japaneseLegacyFont);
        skin.ApplyTheme();
        EditorUtility.SetDirty(skin);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }

    private void EnsureSlotForMount(RectTransform mount)
    {
        // すでにEquipmentSlotUIが配置されていれば重複生成しない。
        if (mount.GetComponentInChildren<EquipmentSlotUI>(true) != null) return;
        EquipmentSlotUI slot = MakeEquipmentSlot("TownEquipmentSlotUI", mount, 0, 0, 300, 115);
        SerializedObject obj = new SerializedObject(slot);
        SetRef(obj, "equipmentController", playerBridge.EquipmentController);
        obj.ApplyModifiedPropertiesWithoutUndo();
    }


    /// <summary>町の共通売買Root内のGridだけに適用。出撃補充Rootは検索しない。</summary>
    private int UpgradeExistingTradeGrids()
    {
        if (canvas == null) return 0;
        int count = 0;
        foreach (TownUnifiedTradeScreen2D screen in
            Object.FindObjectsByType<TownUnifiedTradeScreen2D>(FindObjectsInactive.Include))
        {
            if (screen == null || screen.gameObject.scene != canvas.gameObject.scene) continue;
            // 過去の生成名に限定し、他のInventory画面は変更しない。
            Transform manager = screen.transform;
            Transform root = canvas.transform.Find("TownUnifiedTradeRoot");
            if (root == null) continue;
            foreach (InventoryGridUI grid in root.GetComponentsInChildren<InventoryGridUI>(true))
            {
                if (grid.name != "TownMerchantGridUI" && grid.name != "TownPlayerGridUI") continue;
                TownGridScrollEditorUtility.SetCellSize(grid, 60f);
                if (TownGridScrollEditorUtility.EnsureScroll(grid, 520f, 462f) != null) count++;
            }
            if (count > 0) EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            break;
        }
        return count;
    }

    private ItemBoxUIController FindHomeUIController()
    {
        foreach (ItemBoxUIController boxUI in Object.FindObjectsByType<ItemBoxUIController>(FindObjectsInactive.Include))
        {
            if (boxUI == null || (canvas != null && boxUI.gameObject.scene != canvas.gameObject.scene)) continue;
            SerializedObject so = new SerializedObject(boxUI);
            var panel = so.FindProperty("itemBoxPanel");
            GameObject panelObj = panel != null ? panel.objectReferenceValue as GameObject : null;
            if (homePanel != null && panelObj != null &&
                (homePanel.gameObject == panelObj || homePanel.IsChildOf(panelObj.transform) || panelObj.transform.IsChildOf(homePanel)))
                return boxUI;
        }
        return null;
    }

    private bool UpgradeHomeItemBox()
    {
        ItemBoxUIController boxUI = FindHomeUIController();
        if (boxUI != null)
        {
            SerializedObject so = new SerializedObject(boxUI);
            var bp = so.FindProperty("itemBoxGridUI");
            var pp = so.FindProperty("playerGridUI");
            if (homeItemBoxGrid == null && bp != null)
                homeItemBoxGrid = bp.objectReferenceValue as InventoryGridUI;
            if (homePlayerGrid == null && pp != null)
                homePlayerGrid = pp.objectReferenceValue as InventoryGridUI;
        }
        if (homePanel != null)
        {
            foreach (InventoryGridUI candidate in homePanel.GetComponentsInChildren<InventoryGridUI>(true))
            {
                if (candidate == homeItemBoxGrid || candidate == homePlayerGrid) continue;
                if (homePlayerGrid == null && candidate.name.ToLowerInvariant().Contains("player"))
                    homePlayerGrid = candidate;
                else if (homeItemBoxGrid == null && (candidate.name.ToLowerInvariant().Contains("box") || candidate.name.ToLowerInvariant().Contains("storage")))
                    homeItemBoxGrid = candidate;
            }
        }
        if (homeItemBoxGrid == null || homePlayerGrid == null || homeItemBoxGrid == homePlayerGrid || homeStorageBox == null)
            return false;
        if (homeStorageBox.BoxKind != ItemBoxKind.Storage ||
            homeStorageBox.GetComponent<MerchantStockInventory>() != null || homeStorageBox.GetComponent<SellCartInventory>() != null ||
            (canvas != null && (homeItemBoxGrid.gameObject.scene != canvas.gameObject.scene || homeStorageBox.gameObject.scene != canvas.gameObject.scene)))
            return false;

        // 見た目のセルサイズだけを販売画面のPlayer Gridと統一する。
        TownGridScrollEditorUtility.SetCellSize(homePlayerGrid, 60f);
        if (setupHomeScroll && TownGridScrollEditorUtility.EnsureScroll(homeItemBoxGrid) == null)
            return false;

        EnlargeItemBoxOnce(homeStorageBox);
        EditorSceneManager.MarkSceneDirty(homeItemBoxGrid.gameObject.scene);
        return true;
    }

    private int UpgradeMerchantStockRows()
    {
        if (canvas == null) return 0;
        int changed = 0;
        foreach (MerchantStockInventory merchant in Object.FindObjectsByType<MerchantStockInventory>(FindObjectsInactive.Include))
        {
            if (merchant == null || merchant.gameObject.scene != canvas.gameObject.scene) continue;
            ItemBoxInventory box = merchant.StockInventory != null ? merchant.StockInventory : merchant.GetComponent<ItemBoxInventory>();
            if (box == null) continue;
            EnlargeItemBoxOnce(box);
            changed++;
        }
        return changed;
    }

    /// <summary>1回目だけ元の行数から2倍へ拡張。設定の繰り返しでは追加拡張しない。</summary>
    private void EnlargeItemBoxOnce(ItemBoxInventory box)
    {
        if (box == null) return;
        SerializedObject data = new SerializedObject(box);
        SerializedProperty prop = data.FindProperty("gridHeight");
        if (prop == null) return;
        TownItemBoxMinRows2D capacity = box.GetComponent<TownItemBoxMinRows2D>();
        if (capacity == null)
        {
            int original = Mathf.Max(1, prop.intValue);
            capacity = Undo.AddComponent<TownItemBoxMinRows2D>(box.gameObject);
            Undo.RecordObject(capacity, "町用の箱の行数を記録");
            capacity.Configure(Mathf.Min(80, original * 2));
        }
        // 保存されている設定も増やす。実行中/ロード後の古い寸法はTownItemBoxMinRows2Dが補正する。
        prop.intValue = Mathf.Max(prop.intValue, capacity.MinimumRows);
        data.ApplyModifiedProperties();
        EditorUtility.SetDirty(box);
        EditorUtility.SetDirty(capacity);
        EditorSceneManager.MarkSceneDirty(box.gameObject.scene);
    }

    /// <summary>既に生成済みの修理画面にも、枠への自動フィットとSEを設定。</summary>
    private bool UpgradeExistingRepairScreen()
    {
        if (canvas == null || repairController == null) return false;
        Transform root = canvas.transform.Find("TownWeaponRepairRoot");
        if (root == null) return false;
        Transform panel = root.Find("TownRepairWindow/RepairInventoryPanel");
        if (panel == null) return false;
        InventoryGridUI grid = panel.GetComponentInChildren<InventoryGridUI>(true);
        if (grid == null) return false;
        TownRepairGridFit2D fitter = panel.GetComponent<TownRepairGridFit2D>();
        if (fitter == null) fitter = Undo.AddComponent<TownRepairGridFit2D>(panel.gameObject);
        Undo.RecordObject(fitter, "修理Grid位置補正");
        fitter.Configure(grid, panel as RectTransform);
        EditorUtility.SetDirty(fitter);
        ConfigureRepairSounds(root.GetComponent<TownWeaponRepairScreen2D>());
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
        return true;
    }

    private static AudioClip GetClip(ExpeditionUnifiedTradeController2D template, string key)
    {
        if (template == null) return null;
        SerializedProperty prop = new SerializedObject(template).FindProperty(key);
        return prop != null ? prop.objectReferenceValue as AudioClip : null;
    }

    private void ConfigureRepairSounds(TownWeaponRepairScreen2D screen)
    {
        if (repairController == null || screen == null) return;
        AudioSource source = repairController.GetComponent<AudioSource>();
        if (source == null) source = Undo.AddComponent<AudioSource>(repairController.gameObject);
        Undo.RecordObject(source, "修理画面サウンド");
        source.playOnAwake = false;
        source.spatialBlend = 0f;
        EditorUtility.SetDirty(source);

        AudioClip defaultSelection = GetClip(soundTemplate, "selectionClip");
        AudioClip defaultCheckout = GetClip(soundTemplate, "checkoutClip");
        SerializedObject repairSO = new SerializedObject(repairController);
        SetRef(repairSO, "audioSource", source);
        var selected = repairSO.FindProperty("weaponSelectSound");
        // 開いた瞬間の自動選択でOpenSEと重なりやすいため、
        // 武器選択音はユーザーが明示設定した時だけ上書きする。
        if (selected != null && repairSelectionSound != null)
            selected.objectReferenceValue = repairSelectionSound;
        var success = repairSO.FindProperty("repairSuccessSound");
        if (success != null && (success.objectReferenceValue == null || repairSuccessSound != null))
            success.objectReferenceValue = repairSuccessSound != null ? repairSuccessSound : defaultCheckout;
        repairSO.ApplyModifiedProperties();

        SerializedObject screenSO = new SerializedObject(screen);
        SetRef(screenSO, "soundSource", source);
        var open = screenSO.FindProperty("openSound");
        var close = screenSO.FindProperty("closeSound");
        if (open != null && (open.objectReferenceValue == null || repairOpenSound != null))
            open.objectReferenceValue = repairOpenSound != null ? repairOpenSound : defaultSelection;
        if (close != null && (close.objectReferenceValue == null || repairCloseSound != null))
            close.objectReferenceValue = repairCloseSound != null ? repairCloseSound : defaultSelection;
        screenSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(screen);
    }

    private int ApplyEquipmentDropPreviewFix()
    {
        if (canvas == null) return 0;
        int count = 0;
        foreach (EquipmentSlotUI slot in Object.FindObjectsByType<EquipmentSlotUI>(FindObjectsInactive.Include))
        {
            if (slot == null || slot.gameObject.scene != canvas.gameObject.scene) continue;
            if (slot.GetComponent<TownEquipmentSlotPreview2D>() != null) continue;
            Undo.AddComponent<TownEquipmentSlotPreview2D>(slot.gameObject);
            count++;
        }
        if (count > 0) EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        return count;
    }

    private bool CreateNewRepairScreen()
    {
        if (canvas == null || pawnShop == null || playerBridge == null || repairController == null) return false;
        // Controllerを旧repairRoot直下に置いている場合、旧Panelを閉じるとControllerまで停止する。
        // Unityの場面構成に合わせ、常時有効なManagerへ移動してから生成してもらう。
        SerializedProperty oldRepairRoot = new SerializedObject(repairController).FindProperty("repairRoot");
        GameObject oldRoot = oldRepairRoot != null ? oldRepairRoot.objectReferenceValue as GameObject : null;
        if (oldRoot != null && repairController.transform.IsChildOf(oldRoot.transform))
        {
            EditorUtility.DisplayDialog("武器修理Controllerの配置", 
                "MerchantWeaponRepairControllerが旧修理Panelの子にあります。" +
                "Panelを閉じるとControllerも停止するため、先にPawnShopManagerなど常時有効なObjectへ移してください。" +
                "移動後にもう一度実行してください。", "OK");
            return false;
        }
        foreach (Transform child in canvas.transform)
            if (child.name == "TownWeaponRepairRoot") return false;

        GameObject root = New("TownWeaponRepairRoot", canvas.transform,
            typeof(RectTransform), typeof(Image), typeof(TownWeaponRepairScreen2D));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;
        rootRect.localScale = Vector3.one;
        Image fade = root.GetComponent<Image>();
        fade.color = ColorOf("#111719FA");
        fade.raycastTarget = true;

        GameObject window = New("TownRepairWindow", root.transform,
            typeof(RectTransform), typeof(Image), typeof(Outline), typeof(CanvasGroup));
        RectTransform windowRect = window.GetComponent<RectTransform>();
        windowRect.anchorMin = windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.anchoredPosition = Vector2.zero;
        windowRect.sizeDelta = new Vector2(1180, 770);
        window.GetComponent<Image>().color = Backdrop;
        window.GetComponent<Outline>().effectColor = Border;
        Label("RepairTitle", window.transform, "武器の修理", 28, 25, 20, 520, 45, Paper);
        Label("RepairInventoryLabel", window.transform, "所持品から修理する武器を選択", 21, 25, 94, 530, 34, Paper);
        Label("RepairEquipLabel", window.transform, "装備中の武器（クリックで選択）", 19, 632, 94, 500, 34, Paper);

        RectTransform inventoryPanel = Plate("RepairInventoryPanel", window.transform, 22, 139, 556, 480);
        RectTransform detailPanel = Plate("RepairInfoPanel", window.transform, 600, 139, 555, 480);
        InventoryGridUI playerGrid = MakeGrid("TownRepairPlayerGrid", inventoryPanel, 18, 15, 516, 455);
        TownGridScrollEditorUtility.SetCellSize(playerGrid, 52f);
        // 修理画面のGridは専用の小さいセル。セル60指定は町の売買画面に限定する。
        EquipmentSlotUI equip = MakeEquipmentSlot("TownRepairEquipmentSlot", detailPanel, 22, 17, 306, 109);
        var equipSO = new SerializedObject(equip);
        SetRef(equipSO, "equipmentController", playerBridge.EquipmentController);
        equipSO.ApplyModifiedPropertiesWithoutUndo();
        Button pickEquippedButton = MakeButton("SelectEquippedRepairButton", detailPanel, "装備武器を選択", 337, 42, 194, 54, false);
        Image selectedIcon = New("RepairSelectedIcon", detailPanel,
            typeof(RectTransform), typeof(Image)).GetComponent<Image>();
        Place(selectedIcon.rectTransform, 21, 148, 102, 102);
        selectedIcon.preserveAspect = true;
        selectedIcon.enabled = false;
        selectedIcon.raycastTarget = false;
        TextMeshProUGUI weaponName = Label("RepairWeaponName", detailPanel, "武器を選択", 20, 140, 155, 393, 42, Paper);
        TextMeshProUGUI durability = Label("RepairDurability", detailPanel, "耐久度：―", 18, 24, 283, 507, 34, Paper);
        TextMeshProUGUI cost = Label("RepairCost", detailPanel, "修理費：―", 21, 24, 337, 507, 38, Accent);
        TextMeshProUGUI money = Label("RepairMoney", detailPanel, "所持金：―", 18, 24, 389, 507, 32, Dim);
        TextMeshProUGUI status = Label("RepairStatus", window.transform,
            "修理する武器を選択してください。", 16, 31, 630, 1030, 29, Dim);
        RectTransform footer = Plate("RepairFooter", window.transform, 24, 678, 1130, 70);
        Button back = MakeButton("CloseRepairButton", footer, "戻る", 788, 9, 140, 52, false);
        Button repair = MakeButton("RepairSelectedButton", footer, "修理する", 941, 9, 162, 52, true);

        SerializedObject so = new SerializedObject(repairController);
        SetRef(so, "repairRoot", root);
        SetRef(so, "playerInventoryGridUI", playerGrid);
        SetRef(so, "townPlayerInventory", playerBridge);
        SetRef(so, "selectedWeaponIcon", selectedIcon);
        SetRef(so, "selectedWeaponNameText", weaponName);
        SetRef(so, "durabilityText", durability);
        SetRef(so, "repairCostText", cost);
        SetRef(so, "moneyText", money);
        SetRef(so, "statusText", status);
        SetRef(so, "repairButton", repair);
        SetRef(so, "selectEquippedWeaponButton", pickEquippedButton);
        so.ApplyModifiedPropertiesWithoutUndo();

        TownWeaponRepairScreen2D screen = root.GetComponent<TownWeaponRepairScreen2D>();
        SerializedObject sc = new SerializedObject(screen);
        SetRef(sc, "pawnShop", pawnShop);
        SetRef(sc, "closeButton", back);
        SetRef(sc, "window", windowRect);
        SetRef(sc, "windowGroup", window.GetComponent<CanvasGroup>());
        sc.ApplyModifiedPropertiesWithoutUndo();
        TownRepairGridFit2D fit = Undo.AddComponent<TownRepairGridFit2D>(inventoryPanel.gameObject);
        fit.Configure(playerGrid, inventoryPanel);
        ConfigureRepairSounds(screen);

        TownSimpleRpgSkin2D skin = Undo.AddComponent<TownSimpleRpgSkin2D>(root);
        skin.Configure(TownSimpleRpgSkin2D.TownScreenKind.WeaponRepair, rootRect);
        skin.ConfigureJapaneseFonts(japaneseTmpFont, japaneseLegacyFont);
        skin.ApplyTheme();
        root.SetActive(false);
        root.transform.SetAsLastSibling();
        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        return true;
    }

    private static EquipmentSlotUI MakeEquipmentSlot(string name, Transform parent,
        float x, float y, float w, float h)
    {
        GameObject go = New(name, parent, typeof(RectTransform), typeof(Image), typeof(EquipmentSlotUI));
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        go.GetComponent<Image>().color = Cell;
        SerializedObject so = new SerializedObject(go.GetComponent<EquipmentSlotUI>());
        Int(so, "slotWidth", 5);
        Int(so, "slotHeight", 2);
        Float(so, "cellSize", 50f);
        Float(so, "cellSpacing", 2f);
        SetColor(so, "slotBackgroundColor", Panel);
        SetColor(so, "cellColor", Cell);
        SetColor(so, "itemBackgroundColor", ColorOf("#4B5050"));
        so.ApplyModifiedPropertiesWithoutUndo();
        return go.GetComponent<EquipmentSlotUI>();
    }

    private static InventoryGridUI MakeGrid(string name, Transform parent,
        float x, float y, float w, float h)
    {
        GameObject go = New(name, parent, typeof(RectTransform), typeof(InventoryGridUI));
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        SerializedObject so = new SerializedObject(go.GetComponent<InventoryGridUI>());
        Float(so, "cellSize", 60f);
        Float(so, "cellSpacing", 2f);
        SetColor(so, "cellColor", Cell);
        so.ApplyModifiedPropertiesWithoutUndo();
        return go.GetComponent<InventoryGridUI>();
    }

    private static RectTransform Plate(string name, Transform parent,
        float x, float y, float w, float h)
    {
        GameObject go = New(name, parent, typeof(RectTransform), typeof(Image), typeof(Outline));
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        go.GetComponent<Image>().color = Panel;
        go.GetComponent<Outline>().effectColor = Border;
        go.GetComponent<Outline>().effectDistance = new Vector2(1, -1);
        return go.GetComponent<RectTransform>();
    }

    private Button MakeButton(string name, Transform parent, string text,
        float x, float y, float w, float h, bool primary)
    {
        GameObject go = New(name, parent, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(Button));
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        Image image = go.GetComponent<Image>();
        image.color = primary ? ColorOf("#575044") : ColorOf("#353B3D");
        go.GetComponent<Outline>().effectColor = Border;
        Button button = go.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = ColorOf("#DDD7CD");
        colors.pressedColor = ColorOf("#A7A6A1");
        colors.selectedColor = Color.white;
        colors.fadeDuration = 0.08f;
        button.colors = colors;
        TextMeshProUGUI label = Label("Label", go.transform, text, 19, 0, 0, w, h, Paper);
        label.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private static Slider MakeSlider(string name, Transform parent, float x, float y, float w, float h)
    {
        GameObject go = New(name, parent, typeof(RectTransform), typeof(Slider));
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        GameObject track = New("Track", go.transform, typeof(RectTransform), typeof(Image));
        RectTransform trackRect = track.GetComponent<RectTransform>();
        FillStretch(trackRect, 2, 2, 2, 2);
        track.GetComponent<Image>().color = ColorOf("#3A4243");
        GameObject fill = New("Fill", go.transform, typeof(RectTransform), typeof(Image));
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        FillStretch(fillRect, 3, 3, 3, 3);
        fill.GetComponent<Image>().color = Accent;
        GameObject handle = New("Handle", go.transform, typeof(RectTransform), typeof(Image));
        RectTransform handleRect = handle.GetComponent<RectTransform>();
        handleRect.anchorMin = handleRect.anchorMax = new Vector2(0, 0.5f);
        handleRect.pivot = new Vector2(0.5f, 0.5f);
        handleRect.sizeDelta = new Vector2(14, 27);
        handle.GetComponent<Image>().color = Paper;
        Slider slider = go.GetComponent<Slider>();
        slider.fillRect = fillRect;
        slider.handleRect = handleRect;
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.minValue = 1;
        slider.maxValue = 10;
        slider.wholeNumbers = true;
        return slider;
    }

    private TextMeshProUGUI Label(string name, Transform parent, string text,
        float fontSize, float x, float y, float w, float h, Color color)
    {
        GameObject go = New(name, parent, typeof(RectTransform), typeof(TextMeshProUGUI));
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        label.text = text;
        if (japaneseTmpFont != null) label.font = japaneseTmpFont;
        label.fontSize = fontSize;
        label.enableAutoSizing = false;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject New(string name, Transform parent, params System.Type[] comps)
    {
        GameObject go = new GameObject(name, comps);
        go.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(go, "町UI v3.1");
        return go;
    }

    private static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0, 1);
        rect.pivot = new Vector2(0, 1);
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(w, h);
        rect.localScale = Vector3.one;
    }

    private static void FillStretch(RectTransform rect, float l, float t, float r, float b)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(l, b);
        rect.offsetMax = new Vector2(-r, -t);
    }

    private static void SetRef(SerializedObject so, string property, Object value)
    {
        SerializedProperty found = so.FindProperty(property);
        if (found == null)
            Debug.LogWarning("[町UI Editor] SerializedPropertyなし: " + property + " on " + so.targetObject.name);
        else found.objectReferenceValue = value;
    }
    private static void Float(SerializedObject so, string property, float value)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p != null) p.floatValue = value;
    }
    private static void Int(SerializedObject so, string property, int value)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p != null) p.intValue = value;
    }
    private static void SetColor(SerializedObject so, string property, Color value)
    {
        SerializedProperty p = so.FindProperty(property);
        if (p != null) p.colorValue = value;
    }
    private static Color ColorOf(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color c);
        return c;
    }
}
#endif
