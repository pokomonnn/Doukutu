#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 画像で指定された配置に基づき、既存のItemBoxInventory / InventoryControllerで動くUIを生成する。
/// Tools > 2D Shooter > 出撃補給・一体型売買UIを生成
/// </summary>
public class ExpeditionTradeAutoLayoutWindow : EditorWindow
{
    private StageExpeditionPanelController stagePanel;
    private MerchantStockInventory merchantStock;
    private InventoryController playerInventory;
    private PlayerTradeInventoryBridge townBridge;
    private SellCartInventory sellRules;
    private MedicalKitInventory2D kit;
    private TMP_FontAsset japaneseFont;
    private ConsumableItemData[] medicalItems = new ConsumableItemData[3];
    private int[] prices = {150, 150, 100};
    private AudioClip openSfx, clickSfx, checkoutSfx, failSfx;
    
    private Vector2 scroll;

    private const string CanvasName = "ExpeditionTradeCanvas_LAYOUT_V2";
    private const string ManagerName = "ExpeditionTradeManager_LAYOUT_V2";
    private static readonly Color Blue = Hex("#175E80");
    private static readonly Color Orange = Hex("#EB722E");
    private static readonly Color White = Color.white;

    [MenuItem("Tools/2D Shooter/出撃補給・一体型売買UIを図の配置で生成")]
    public static void OpenWindow()
    {
        GetWindow<ExpeditionTradeAutoLayoutWindow>("出撃補給：一体型売買").Show();
    }

    private void OnEnable() { FindReferences(); }

    private void FindReferences()
    {
        if (stagePanel == null) stagePanel = FindUnique<StageExpeditionPanelController>();
        if (merchantStock == null) merchantStock = FindUnique<MerchantStockInventory>();
        if (townBridge == null) townBridge = FindUnique<PlayerTradeInventoryBridge>();
        if (playerInventory == null && townBridge != null) playerInventory = townBridge.InventoryController;
        if (playerInventory == null) playerInventory = FindUnique<InventoryController>();
        if (sellRules == null) sellRules = FindUnique<SellCartInventory>();
        if (kit == null) kit = FindUnique<MedicalKitInventory2D>();
        for (int i = 0; i < 3; i++)
            if (medicalItems[i] == null && kit != null) medicalItems[i] = kit.GetItemAtSlot(i);
        if (japaneseFont == null) japaneseFont = TMP_Settings.defaultFontAsset;
        if (openSfx == null) openSfx = FindSfx("ExpeditionSupplyOpen");
        if (clickSfx == null) clickSfx = FindSfx("ExpeditionTradeSelect");
        if (checkoutSfx == null) checkoutSfx = FindSfx("ExpeditionTradeCheckout");
        if (failSfx == null) failSfx = FindSfx("ExpeditionTradeFail");

    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("出撃用売買画面・シンプルRPG UI v2.3", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Town_Mainを開いてから実行。古い出撃選択・ミッション/セーブ機能はそのままです。生成した新UI内だけで購入＋売却の差額を一括決済します。", MessageType.Info);
        if (GUILayout.Button("シーンの参照を再検索")) FindReferences();
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            if (GUILayout.Button("既存画面をシンプルUIに変更（再生成しない）", GUILayout.Height(38))) ThemeExistingMenu();
        }
        stagePanel = (StageExpeditionPanelController)EditorGUILayout.ObjectField("出撃選択Panel（必須）", stagePanel, typeof(StageExpeditionPanelController), true);
        merchantStock = (MerchantStockInventory)EditorGUILayout.ObjectField("商人Stock（必須）", merchantStock, typeof(MerchantStockInventory), true);
        playerInventory = (InventoryController)EditorGUILayout.ObjectField("Player Inventory（必須）", playerInventory, typeof(InventoryController), true);
        townBridge = (PlayerTradeInventoryBridge)EditorGUILayout.ObjectField("Town Inventory Bridge", townBridge, typeof(PlayerTradeInventoryBridge), true);
        sellRules = (SellCartInventory)EditorGUILayout.ObjectField("既存売却規則（任意）", sellRules, typeof(SellCartInventory), true);
        kit = (MedicalKitInventory2D)EditorGUILayout.ObjectField("医療Kit（任意）", kit, typeof(MedicalKitInventory2D), true);
        japaneseFont = (TMP_FontAsset)EditorGUILayout.ObjectField("日本語TMP FontAsset", japaneseFont, typeof(TMP_FontAsset), false);
        EditorGUILayout.Space(8);
        string[] names = {"解毒剤", "副木", "包帯"};
        for (int i = 0; i < 3; i++)
        {
            medicalItems[i] = (ConsumableItemData)EditorGUILayout.ObjectField(names[i] + " ItemData", medicalItems[i], typeof(ConsumableItemData), false);
            prices[i] = Mathf.Max(0, EditorGUILayout.IntField(names[i] + "価格(G)", prices[i]));
        }
        EditorGUILayout.Space(8);
        openSfx = (AudioClip)EditorGUILayout.ObjectField("補充画面が開くSE", openSfx, typeof(AudioClip), false);
        clickSfx = (AudioClip)EditorGUILayout.ObjectField("Item選択SE", clickSfx, typeof(AudioClip), false);
        checkoutSfx = (AudioClip)EditorGUILayout.ObjectField("決済SE", checkoutSfx, typeof(AudioClip), false);
        failSfx = (AudioClip)EditorGUILayout.ObjectField("失敗SE", failSfx, typeof(AudioClip), false);
        if (stagePanel == null || merchantStock == null || playerInventory == null)
            EditorGUILayout.HelpBox("出撃Panel・MerchantStockInventory・Player Inventoryの3つを設定してください。", MessageType.Error);
        if (Array.Exists(medicalItems, item => item == null))
            EditorGUILayout.HelpBox("医療品ItemDataが未設定のものは、生成後に購入できません。", MessageType.Warning);
        EditorGUILayout.Space(12);
        using (new EditorGUI.DisabledScope(stagePanel == null || merchantStock == null || playerInventory == null || Application.isPlaying))
        {
            if (GUILayout.Button("同じ配置でシンプルUIを新規生成", GUILayout.Height(48))) Generate();
        }
        EditorGUILayout.HelpBox("既存のUIがある場合は Tools > 2D Shooter > 出撃補充 > 既存画面をシンプルUIに変更 を推奨します（参照や配置を維持）。新規生成は旧v2.0のUIを削除して作り直します。", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void Generate()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (stagePanel.gameObject.scene != scene || merchantStock.gameObject.scene != scene || playerInventory.gameObject.scene != scene)
        {
            EditorUtility.DisplayDialog("対象シーンを確認", "出撃Panel / Stock / Inventory は現在開いているTown_Mainに配置してください。", "OK");
            return;
        }
        GameObject oldCanvas = Named(scene, CanvasName);
        GameObject oldManager = Named(scene, ManagerName);
        if ((oldCanvas != null || oldManager != null) &&
            !EditorUtility.DisplayDialog("v2.0再生成", "前回のv2.0自動生成UIを作り直しますか？（既存の子オブジェクトは削除）", "生成し直す", "やめる")) return;
        if (oldCanvas != null) Undo.DestroyObjectImmediate(oldCanvas);
        if (oldManager != null) Undo.DestroyObjectImmediate(oldManager);
        GameObject previous = Named(scene, "ExpeditionSupplyPanel_AUTO");
        if (previous != null) { Undo.RecordObject(previous, "旧補充パネルを非表示"); previous.SetActive(false); }
        GameObject oldController = Named(scene, "ExpeditionSupplyManager_AUTO");
        if (oldController != null)
        {
            ExpeditionSupplyPanelController2D controller = oldController.GetComponent<ExpeditionSupplyPanelController2D>();
            if (controller != null) { Undo.RecordObject(controller, "旧補充Managerを停止"); controller.enabled = false; }
        }

        GameObject canvasGo = new GameObject(CanvasName, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(canvasGo, "出撃補充Canvas生成");
        SceneManager.MoveGameObjectToScene(canvasGo, scene);
        Canvas canvas = Undo.AddComponent<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 105;
        CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1328f, 738f);
        scaler.matchWidthOrHeight = 0.5f;
        Undo.AddComponent<GraphicRaycaster>(canvasGo);
        EnsureEventSystem(scene);

        GameObject root = RectImage("ExpeditionSupplyPanel_LAYOUT_V2", canvasGo.transform, Color.white, true);
        Stretch(root.GetComponent<RectTransform>());
        GameObject managerGo = new GameObject(ManagerName);
        Undo.RegisterCreatedObjectUndo(managerGo, "売買管理オブジェクト生成");
        SceneManager.MoveGameObjectToScene(managerGo, scene);
        ExpeditionSupplyPanelController2D supply = Undo.AddComponent<ExpeditionSupplyPanelController2D>(managerGo);
        ExpeditionUnifiedTradeController2D trade = Undo.AddComponent<ExpeditionUnifiedTradeController2D>(managerGo);
        MedicalKitSupplyShop2D med = Undo.AddComponent<MedicalKitSupplyShop2D>(managerGo);
        AudioSource audio = Undo.AddComponent<AudioSource>(managerGo);
        audio.playOnAwake = false;
        audio.spatialBlend = 0;

        TMP_Text stageText = Label("StageInfo", root.transform, "出撃先", 17, new Color(.14f,.22f,.3f), 1032, 304, 275, 24);
        TMP_Text entryText = Label("EntryInfo", root.transform, "出撃地点", 14, new Color(.2f,.28f,.36f), 1032, 335, 275, 22);
        TMP_Text moneyText = Label("Money", root.transform, "所持金：0G", 20, new Color(.09f,.23f,.32f), 384, 164, 240, 32);

        Panel("MerchantGridBG", root.transform, 15, 35, 345, 477, Blue);
        Label("MerchantTitle", root.transform, "商人grid", 22, White, 70, 237, 232, 36, TextAlignmentOptions.Center);
        InventoryGridUI merchantGrid = Grid("MerchantGridUI", root.transform, 25, 45, 330, 458);

        GameObject weaponPanel = Panel("WeaponSlot", root.transform, 378, 35, 241, 101, Blue);
        Label("WeaponGridLabel", weaponPanel.transform, "武器grid", 22, White, 14, 5, 210, 30, TextAlignmentOptions.Center);
        Image weaponIcon = RectImage("EquippedWeaponIcon", weaponPanel.transform, Color.white, false).GetComponent<Image>();
        Place(weaponIcon.rectTransform, 14, 42, 57, 53);
        weaponIcon.enabled = false;
        TMP_Text weaponName = Label("EquippedWeaponName", weaponPanel.transform, "武器：未装備", 17, White, 81, 55, 151, 28);
        ExpeditionEquippedWeaponPreview2D preview = Undo.AddComponent<ExpeditionEquippedWeaponPreview2D>(weaponPanel);
        SetObjects(preview, ("weaponIcon", weaponIcon), ("weaponNameText", weaponName));

        GameObject sliderPanel = Panel("QuantitySliderArea", root.transform, 376, 143, 250, 52, new Color(.91f,.95f,.96f,1f));
        TMP_Text itemName = Label("QuantityName", sliderPanel.transform, "数量調整", 12, new Color(.12f,.23f,.3f), 6, 1, 235, 18);
        Slider qtySlider = MakeSlider("StackQuantitySlider", sliderPanel.transform, 11, 23, 140, 21);
        TMP_Text qtyText = Label("QuantityValue", sliderPanel.transform, "数量：―", 12, new Color(.11f,.24f,.35f), 159, 22, 87, 23);

        TMP_Text sellTotal = OnPanelLabel(root.transform, "SellValue", "売り金額  0 G", 22, 424, 201, 165, 52);
        TMP_Text buyTotal = OnPanelLabel(root.transform, "BuyValue", "買い金額  0 G", 22, 424, 266, 165, 52);
        TMP_Text netTotal = OnPanelLabel(root.transform, "NetValue", "差引支払  0 G", 20, 369, 342, 279, 79);
        Button checkout = MakeButton("CheckoutButton", root.transform, "決済ボタン", 22, Blue, 424, 444, 161, 57);

        Panel("PlayerGridBG", root.transform, 657, 35, 345, 477, Blue);
        Label("PlayerTitle", root.transform, "プレイヤーgrid", 22, White, 703, 239, 250, 40, TextAlignmentOptions.Center);
        InventoryGridUI playerGrid = Grid("PlayerGridUI", root.transform, 668, 46, 325, 456);

        GameObject medicineBackground = Panel("MedicalKitItems", root.transform, 1022, 43, 286, 159, Blue);
        string[] fallbacks = {"解毒剤", "副木", "包帯"};
        Button[] medButtons = new Button[3];
        Image[] icons = new Image[3];
        TMP_Text[] medNames = new TMP_Text[3];
        TMP_Text[] medPrices = new TMP_Text[3];
        TMP_Text[] medStock = new TMP_Text[3];
        for (int i = 0; i < 3; i++)
        {
            float x = 7 + i * 92;
            GameObject card = Panel("MedicalSlot" + i, medicineBackground.transform, x, 21, 81, 96, Orange);
            medNames[i] = Label("MedicalItemName", card.transform, medicalItems[i] != null ? medicalItems[i].DisplayName : fallbacks[i], 14, White, 3, 10, 75, 31, TextAlignmentOptions.Center);
            icons[i] = RectImage("MedicalItemIcon", card.transform, Color.white, false).GetComponent<Image>();
            Place(icons[i].rectTransform, 20, 44, 42, 38);
            icons[i].sprite = medicalItems[i] != null ? medicalItems[i].Icon : null;
            icons[i].enabled = icons[i].sprite != null;
            icons[i].preserveAspect = true;
            medStock[i] = Label("MedicalStock", card.transform, "0/3", 13, White, 16, 82, 65, 14, TextAlignmentOptions.Center);
            medButtons[i] = MakeButton("MedicalBuy" + i, root.transform, "買う", 18, Blue, 1029 + i * 104, 214, 65, 36);
            medPrices[i] = Label("MedicalPrice", root.transform, prices[i] + " G", 14, new Color(.11f,.28f,.36f), 1029 + i * 104, 257, 65, 22, TextAlignmentOptions.Center);
        }
        TMP_Text status = Label("StatusText", root.transform, "アイテムをクリックして購入・売却を選択してください", 16, new Color(.16f,.31f,.40f), 15, 548, 1290, 33, TextAlignmentOptions.Center);
        Button back = MakeButton("BackButton", root.transform, "戻る", 18, Blue, 15, 642, 170, 55);
        Button depart = MakeButton("FinalDepartButton", root.transform, "出撃ボタン", 23, Blue, 442, 626, 433, 80);

        SerializedObject tradeSO = new SerializedObject(trade);
        Obj(tradeSO,"merchantStock",merchantStock);
        Obj(tradeSO,"merchantGridUI",merchantGrid);
        Obj(tradeSO,"playerGridUI",playerGrid);
        Obj(tradeSO,"playerInventory",playerInventory);
        Obj(tradeSO,"sellRules",sellRules);
        Obj(tradeSO,"townInventoryBridge",townBridge);
        Obj(tradeSO,"buyAmountText",buyTotal);
        Obj(tradeSO,"sellAmountText",sellTotal);
        Obj(tradeSO,"totalAmountText",netTotal);
        Obj(tradeSO,"moneyText",moneyText);
        Obj(tradeSO,"statusText",status);
        Obj(tradeSO,"checkoutButton",checkout);
        Obj(tradeSO,"quantitySlider",qtySlider);
        Obj(tradeSO,"sliderRoot",sliderPanel);
        Obj(tradeSO,"quantityText",qtyText);
        Obj(tradeSO,"selectedItemText",itemName);
        Obj(tradeSO,"audioSource",audio);
        Obj(tradeSO,"selectionClip",clickSfx);
        Obj(tradeSO,"checkoutClip",checkoutSfx);
        Obj(tradeSO,"failClip",failSfx);
        tradeSO.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject supplySO = new SerializedObject(supply);
        Obj(supplySO,"supplyPanelRoot",root);
        Obj(supplySO,"stageNameText",stageText);
        Obj(supplySO,"entryPointNameText",entryText);
        Obj(supplySO,"statusText",status);
        Obj(supplySO,"backButton",back);
        Obj(supplySO,"finalDepartButton",depart);
        Obj(supplySO,"unifiedTrade",trade);
        Obj(supplySO,"supplyMerchantStock",merchantStock);
        Obj(supplySO,"medicalKitSupplyShop",med);
        Obj(supplySO,"audioSource",audio);
        Obj(supplySO,"openClip",openSfx);
        Obj(supplySO,"closeClip",clickSfx);
        Obj(supplySO,"departClip",checkoutSfx);
        supplySO.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject medSO = new SerializedObject(med);
        Obj(medSO,"medicalKit",kit);
        Obj(medSO,"moneyText",moneyText);
        Obj(medSO,"statusText",status);
        Obj(medSO,"audioSource",audio);
        Obj(medSO,"successSound",checkoutSfx);
        Obj(medSO,"failedSound",failSfx);
        SerializedProperty list = medSO.FindProperty("supplies");
        list.arraySize = 3;
        for (int i = 0; i < 3; i++)
        {
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("slotIndex").intValue = i;
            element.FindPropertyRelative("itemData").objectReferenceValue = medicalItems[i];
            element.FindPropertyRelative("priceG").intValue = prices[i];
            element.FindPropertyRelative("buyOneButton").objectReferenceValue = medButtons[i];
            element.FindPropertyRelative("itemIcon").objectReferenceValue = icons[i];
            element.FindPropertyRelative("itemNameText").objectReferenceValue = medNames[i];
            element.FindPropertyRelative("priceText").objectReferenceValue = medPrices[i];
            element.FindPropertyRelative("stockText").objectReferenceValue = medStock[i];
        }
        medSO.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject stageSO = new SerializedObject(stagePanel);
        Obj(stageSO,"supplyPanelController",supply);
        SerializedProperty useSupply = stageSO.FindProperty("useSupplyPreparationScreen");
        if (useSupply != null) useSupply.boolValue = true;
        stageSO.ApplyModifiedPropertiesWithoutUndo();
        ApplyGameTheme(root);
        // ランタイム側が自動で「次へ」に置き換える（従来Stage脚本）。
        root.SetActive(false);
        Undo.RecordObject(root, "補充パネル初期表示をOFFにする");
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = managerGo;
        EditorUtility.DisplayDialog("出撃補充・シンプルUIを生成しました", "従来の配置と参照を維持してシンプルUIスキンを適用しました。Playして購入・売却・決済・応急キット補充・出撃の一連の動作を確認してください。", "OK");
    }

    private InventoryGridUI Grid(string name, Transform parent, float x, float y, float w, float h)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Grid生成");
        go.transform.SetParent(parent,false);
        Place(go.GetComponent<RectTransform>(),x,y,w,h);
        InventoryGridUI grid = Undo.AddComponent<InventoryGridUI>(go);
        SerializedObject so = new SerializedObject(grid);
        SerializedProperty cell = so.FindProperty("cellSize");
        if (cell != null) cell.floatValue = 44f;
        SerializedProperty gap = so.FindProperty("cellSpacing");
        if (gap != null) gap.floatValue = 2f;
        so.ApplyModifiedPropertiesWithoutUndo();
        return grid;
    }

    private Slider MakeSlider(string name, Transform parent, float x, float y, float w, float h)
    {
        GameObject container = RectImage(name,parent,new Color(.66f,.74f,.78f,1f),true);
        Place(container.GetComponent<RectTransform>(),x,y,w,h);
        Slider slider = Undo.AddComponent<Slider>(container);
        GameObject fill = RectImage("Fill", container.transform, new Color(.16f,.46f,.64f,1f),false);
        Stretch(fill.GetComponent<RectTransform>());
        GameObject handle = RectImage("Handle",container.transform,White,true);
        Place(handle.GetComponent<RectTransform>(),0,-3,16,h+6);
        slider.fillRect = fill.GetComponent<RectTransform>();
        slider.handleRect = handle.GetComponent<RectTransform>();
        slider.targetGraphic = handle.GetComponent<Image>();
        slider.direction = Slider.Direction.LeftToRight;
        slider.wholeNumbers = true;
        slider.minValue = 1;
        slider.maxValue = 90;
        slider.value = 1;
        return slider;
    }

    private TMP_Text OnPanelLabel(Transform parent, string name, string label, int size, float x, float y, float w, float h)
    {
        Panel(name + "BG",parent,x,y,w,h,Blue);
        return Label(name,parent,label,size,White,x+6,y+4,w-12,h-8,TextAlignmentOptions.Center);
    }

    private Button MakeButton(string name, Transform parent, string label, int fontSize, Color background, float x, float y, float w, float h)
    {
        GameObject go = Panel(name,parent,x,y,w,h,background);
        Button button = Undo.AddComponent<Button>(go);
        button.targetGraphic = go.GetComponent<Image>();
        ColorBlock block = button.colors;
        block.highlightedColor = new Color(.86f,.95f,1f,1f);
        block.pressedColor = new Color(.64f,.85f,.95f,1f);
        block.disabledColor = new Color(.44f,.48f,.52f,1f);
        button.colors = block;
        Label("Text",go.transform,label,fontSize,White,0,0,w,h,TextAlignmentOptions.Center);
        return button;
    }

    private GameObject Panel(string name,Transform parent,float x,float y,float w,float h,Color color)
    {
        GameObject go = RectImage(name,parent,color,true);
        Place(go.GetComponent<RectTransform>(),x,y,w,h);
        return go;
    }

    private TMP_Text Label(string name,Transform parent,string str,float size,Color color,float x,float y,float w,float h,
        TextAlignmentOptions align = TextAlignmentOptions.Left)
    {
        GameObject go = new GameObject(name,typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go,"Text生成");
        go.transform.SetParent(parent,false);
        TextMeshProUGUI text = Undo.AddComponent<TextMeshProUGUI>(go);
        if (japaneseFont != null) text.font = japaneseFont;
        text.text = str;
        text.fontSize = size;
        text.alignment = align;
        text.color = color;
        text.raycastTarget = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        Place(go.GetComponent<RectTransform>(),x,y,w,h);
        return text;
    }

    private static GameObject RectImage(string name,Transform parent,Color color,bool hit)
    {
        GameObject go = new GameObject(name,typeof(RectTransform),typeof(Image));
        Undo.RegisterCreatedObjectUndo(go,"UI生成");
        go.transform.SetParent(parent,false);
        Image image = go.GetComponent<Image>();
        image.color = color;
        image.raycastTarget = hit;
        return go;
    }

    private static void Place(RectTransform rect,float x,float y,float w,float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0f,1f);
        rect.pivot = new Vector2(0f,1f);
        rect.anchoredPosition = new Vector2(x,-y);
        rect.sizeDelta = new Vector2(w,h);
        rect.localScale = Vector3.one;
    }
    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;
        rect.offsetMin=Vector2.zero;rect.offsetMax=Vector2.zero;
    }
    private static void Obj(SerializedObject so,string property,UnityEngine.Object value)
    {
        SerializedProperty found = so.FindProperty(property);
        if (found != null) found.objectReferenceValue = value;
        else Debug.LogWarning("[ExpeditionTradeSetup] Inspector欄が見つかりません："+property);
    }
    private static void SetObjects(UnityEngine.Object target,params (string key,UnityEngine.Object value)[] values)
    {
        SerializedObject so = new SerializedObject(target);
        foreach (var v in values) Obj(so,v.key,v.value);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static T FindUnique<T>() where T: Component
    {
        T result = null;
        Scene active = SceneManager.GetActiveScene();
        foreach (T value in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if (value == null || value.gameObject.scene != active) continue;
            if (result != null) return null;
            result = value;
        }
        return result;
    }
    private static GameObject Named(Scene scene,string name)
    {
        foreach (Transform tr in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None))
            if (tr.gameObject.scene == scene && tr.name == name) return tr.gameObject;
        return null;
    }
    private static AudioClip FindSfx(string name)
    {
        foreach (string guid in AssetDatabase.FindAssets(name+" t:AudioClip"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (System.IO.Path.GetFileNameWithoutExtension(path) == name)
                return AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        }
        return null;
    }
    private static void EnsureEventSystem(Scene scene)
    {
        foreach (EventSystem present in UnityEngine.Object.FindObjectsByType<EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (present != null && present.gameObject.scene == scene) return;
        GameObject go = new GameObject("ExpeditionEventSystemV2");
        Undo.RegisterCreatedObjectUndo(go,"EventSystem生成");
        SceneManager.MoveGameObjectToScene(go,scene);
        Undo.AddComponent<EventSystem>(go);
        Type module = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (module != null && typeof(BaseInputModule).IsAssignableFrom(module))
            Undo.AddComponent(go,module);
        else Undo.AddComponent<StandaloneInputModule>(go);
    }
    // ---- v2.3: DREDGEの簡潔さを参考にした、無地・細枠のUI ----
    // GameObjects/RectTransform/売買・出撃イベントへの参照は一切作り直さない。
    // v2.2から更新できるよう、InventoryGridUI内のserializedフィールド名は維持する。
    private const string ThemeAssets = "Assets/ExpeditionTradeV2/SimpleTheme";
    private static readonly Color Ink = Hex("#E6E3DA");
    private static readonly Color MutedInk = Hex("#ABA99F");
    private static readonly Color Accent = Hex("#D0BA9D");
    private static readonly Color Red = Hex("#CF8F86");

    [MenuItem("Tools/2D Shooter/出撃補充/既存画面をシンプルUIに変更")]
    private static void ThemeExistingMenu()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Playモードを終了", "UIの変更はPlayモードを終了してから行ってください。", "OK");
            return;
        }
        Scene scene = SceneManager.GetActiveScene();
        GameObject root = Named(scene, "ExpeditionSupplyPanel_LAYOUT_V2");
        if (root == null)
        {
            EditorUtility.DisplayDialog("出撃補充UIが見つかりません", "Town_Mainを開き、ExpeditionSupplyPanel_LAYOUT_V2 の存在を確認してください。", "OK");
            return;
        }
        ApplyGameTheme(root);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorUtility.DisplayDialog("シンプルUIを適用しました", "売買・出撃ロジックと画面配置はそのままです。シーンを保存し、Playモードで確認してください。", "OK");
    }

    private static void ApplyGameTheme(GameObject root)
    {
        // 単色＋1px系のラインだけ。木/革/錆/ノイズ/グラデーションは使わない。
        Sprite backdrop = BuildFlatSprite("Screen", Hex("#121719"), Hex("#121719"), false);
        Sprite panel = BuildFlatSprite("Panel", Hex("#1A2021"), Hex("#595A55"));
        Sprite inset = BuildFlatSprite("Inset", Hex("#161B1C"), Hex("#4D504D"));
        Sprite itemSlot = BuildFlatSprite("Item", Hex("#303435"), Hex("#777670"));
        Sprite selectedItem = BuildFlatSprite("Selected", Hex("#564C43"), Hex("#D0BA9D"));
        Sprite cell = BuildFlatSprite("Cell", Hex("#121617"), Hex("#404644"));
        Sprite normalButton = BuildFlatSprite("Button", Hex("#303739"), Hex("#85847A"));
        Sprite checkoutButton = BuildFlatSprite("Checkout", Hex("#4A4842"), Hex("#C2AF94"));
        Sprite departButton = BuildFlatSprite("Depart", Hex("#593C3F"), Hex("#B78A86"));
        if (backdrop == null || panel == null || inset == null || itemSlot == null ||
            selectedItem == null || cell == null || normalButton == null ||
            checkoutButton == null || departButton == null)
        {
            Debug.LogError("[出撃補充UI] シンプルUI素材の生成に失敗しました。適用は中断しました。");
            return;
        }

        // 前バージョンが追加したリベットや光沢、余分な区切り見出しを消す。
        RemoveOldOrnaments(root);
        SetThemeImage(root, backdrop);
        foreach (string n in new[] { "MerchantGridBG", "PlayerGridBG", "WeaponSlot", "MedicalKitItems" })
            SetThemeImage(FindThemeObject(root, n), panel);
        foreach (string n in new[] { "QuantitySliderArea", "SellValueBG", "BuyValueBG", "NetValueBG" })
            SetThemeImage(FindThemeObject(root, n), inset);
        for (int i = 0; i < 3; i++) SetThemeImage(FindThemeObject(root, "MedicalSlot" + i), inset);

        ThemeButton(root, "CheckoutButton", checkoutButton, "決済する", 18);
        ThemeButton(root, "BackButton", normalButton, "戻る", 18);
        ThemeButton(root, "FinalDepartButton", departButton, "出撃する", 24);
        for (int i = 0; i < 3; i++) ThemeButton(root, "MedicalBuy" + i, normalButton, "補充", 16);

        // 読みやすい日本語だけ。見出しも細字寄り、背景に対する文字のコントラストを確保。
        ThemeText(root, "MerchantTitle", "商人の品", 19, Ink, 25, 510, 330, 32);
        ThemeText(root, "PlayerTitle", "所持品", 19, Ink, 668, 510, 325, 32);
        ThemeText(root, "WeaponGridLabel", "装備中の武器", 15, Ink);
        ThemeText(root, "QuantityName", null, 12, MutedInk);
        ThemeText(root, "QuantityValue", null, 13, Ink);
        ThemeText(root, "EquippedWeaponName", null, 16, Ink);
        ThemeText(root, "StageInfo", null, 17, Ink);
        ThemeText(root, "EntryInfo", null, 14, MutedInk);
        ThemeText(root, "Money", null, 19, Accent, 1032, 424, 268, 32);
        ThemeText(root, "StatusText", null, 15, MutedInk);
        ThemeText(root, "BuyValue", null, 17, Ink);
        ThemeText(root, "SellValue", null, 17, Ink);
        ThemeText(root, "NetValue", null, 20, Accent);
        for (int i = 0; i < 3; i++)
        {
            GameObject slot = FindThemeObject(root, "MedicalSlot" + i);
            if (slot != null)
            {
                foreach (TMP_Text t in slot.GetComponentsInChildren<TMP_Text>(true))
                {
                    Undo.RecordObject(t, "応急道具文字の色");
                    StyleText(t, t.gameObject.name == "MedicalStock" ? MutedInk : Ink, 13, false);
                }
            }
            ThemeText(root, "MedicalPrice", null, 13, Accent,
                1029 + i * 104, 257, 65, 22, TextAlignmentOptions.Center, i);
        }

        // スライダーはゲージ・ハンドルの色だけ変更。Sliderや参照は残す。
        GameObject sliderGo = FindThemeObject(root, "StackQuantitySlider");
        if (sliderGo != null)
        {
            SetThemeImage(sliderGo, inset);
            Transform fill = sliderGo.transform.Find("Fill");
            Transform handle = sliderGo.transform.Find("Handle");
            if (fill != null && fill.TryGetComponent(out Image fi))
            {
                Undo.RecordObject(fi, "数量ゲージの外観");
                fi.sprite = null;
                fi.type = Image.Type.Simple;
                fi.color = Accent;
            }
            if (handle != null && handle.TryGetComponent(out Image hi))
            {
                Undo.RecordObject(hi, "数量ハンドルの外観");
                hi.sprite = normalButton;
                hi.type = Image.Type.Sliced;
                hi.color = Color.white;
            }
        }

        // 補充画面内のGridに限定してセルとItemUIの実行時生成スキンを変更。
        ApplyGridTheme(root, "MerchantGridUI", cell, itemSlot, selectedItem);
        ApplyGridTheme(root, "PlayerGridUI", cell, itemSlot, selectedItem);
        ApplyTradeSelectionColors(root);

        TMP_Text fontSource = FindThemeObject(root, "StatusText")?.GetComponent<TMP_Text>();
        // 画面上端に小さなタイトルだけを残す。日本語フォントは元のものを使用。
        EnsureThemeLabel(root, "ThemeHeading", "出撃アイテム補充", 18, Ink, 377, 3, 580, 26, fontSource);
        EnsureThemeLabel(root, "ThemeKits", "応急キット", 15, Ink, 1028, 16, 275, 22, fontSource);
        EnsureThemeLabel(root, "ThemeStageCaption", "出撃先", 14, MutedInk, 1031, 284, 270, 22, fontSource);

        // 情報欄を区切る必要最小限の枠。必ず背面へ配置してテキストを隠さない。
        ThemePlate(root, "ThemeStatusPlate", inset, 15, 540, 1290, 44).transform.SetAsFirstSibling();
        ThemePlate(root, "ThemeStagePlate", panel, 1017, 280, 299, 120).transform.SetAsFirstSibling();
        ThemePlate(root, "ThemeMoneyPlate", inset, 1018, 414, 298, 48).transform.SetAsFirstSibling();

        EditorUtility.SetDirty(root);
    }

    private static void RemoveOldOrnaments(GameObject root)
    {
        // 以前のスキンが作成した、機能を持たないオブジェクト名だけを対象にする。
        HashSet<string> obsolete = new HashSet<string>
        {
            "ThemeTopBar", "ThemeTradeDivider", "ThemeRivet0", "ThemeRivet1",
            "ThemeRivet2", "ThemeRivet3", "ThemeButtonShine"
        };
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform t in children)
        {
            if (t == null || t == root.transform || !obsolete.Contains(t.name)) continue;
            Undo.DestroyObjectImmediate(t.gameObject);
        }
    }

    private static void ApplyGridTheme(GameObject root, string name,
        Sprite cellSprite, Sprite itemSprite, Sprite selectedSprite)
    {
        GameObject go = FindThemeObject(root, name);
        InventoryGridUI grid = go != null ? go.GetComponent<InventoryGridUI>() : null;
        if (grid == null) return;
        SerializedObject so = new SerializedObject(grid);
        // v2.2のSerializedProperty名は維持。保存済みのPrefab / Scene値を失わない。
        SerializedProperty active = so.FindProperty("useExpeditionMedievalTheme");
        if (active == null)
        {
            Debug.LogWarning("[出撃補充UI] v2.2以上の InventoryGridUI.cs / InventoryItemUI.cs が必要です。", grid);
            return;
        }
        active.boolValue = true;
        Obj(so, "medievalCellSprite", cellSprite);
        Obj(so, "medievalItemSprite", itemSprite);
        Obj(so, "medievalSelectedItemSprite", selectedSprite);
        SetColor(so, "medievalCellTint", Color.white);
        SetColor(so, "medievalItemTint", Color.white);
        SetColor(so, "medievalSelectedTint", Color.white);
        SetColor(so, "medievalStackTextTint", Ink);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(grid);
    }

    private static void ApplyTradeSelectionColors(GameObject root)
    {
        GameObject manager = Named(root.scene, ManagerName);
        if (manager == null) return;
        ExpeditionUnifiedTradeController2D trade = manager.GetComponent<ExpeditionUnifiedTradeController2D>();
        if (trade == null) return;
        SerializedObject so = new SerializedObject(trade);
        SetColor(so, "buySelectedColor", new Color(.82f, .73f, .61f, .27f));
        SetColor(so, "sellSelectedColor", new Color(.70f, .35f, .32f, .27f));
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(trade);
    }

    private static void SetColor(SerializedObject so, string name, Color value)
    {
        SerializedProperty property = so.FindProperty(name);
        if (property != null) property.colorValue = value;
    }

    private static void ThemeButton(GameObject root, string name,
        Sprite sprite, string textContent, int fontSize)
    {
        GameObject go = FindThemeObject(root, name);
        if (go == null) return;
        SetThemeImage(go, sprite);
        Button button = go.GetComponent<Button>();
        if (button != null)
        {
            Undo.RecordObject(button, "ボタンの外観");
            button.transition = Selectable.Transition.ColorTint;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Hex("#F6EEE2");
            colors.pressedColor = Hex("#B5AAA0");
            colors.selectedColor = Hex("#E7DDCE");
            colors.disabledColor = new Color(.48f, .48f, .48f, .55f);
            colors.fadeDuration = .1f;
            button.colors = colors;
        }
        TMP_Text text = go.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            Undo.RecordObject(text, "ボタンの文字");
            text.text = textContent;
            StyleText(text, Ink, fontSize, name == "FinalDepartButton");
        }
    }

    private static void ThemeText(GameObject root, string objectName, string newText, int size, Color color,
        float x = -1, float y = -1, float w = -1, float h = -1,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left, int occurrence = 0)
    {
        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);
        TMP_Text target = null;
        foreach (TMP_Text t in texts)
        {
            if (t.gameObject.name != objectName) continue;
            if (occurrence-- == 0) { target = t; break; }
        }
        if (target == null) return;
        Undo.RecordObject(target, "UIテキスト外観");
        if (newText != null) target.text = newText;
        StyleText(target, color, size,
            objectName == "MerchantTitle" || objectName == "PlayerTitle" || objectName == "NetValue");
        if (x >= 0f)
        {
            Undo.RecordObject(target.rectTransform, "UIテキスト位置");
            Place(target.rectTransform, x, y, w, h);
            target.alignment = alignment;
        }
    }

    private static void StyleText(TMP_Text text, Color color, int size, bool bold)
    {
        text.color = color;
        text.fontSize = size;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(9, size - 3);
        text.fontSizeMax = size;
        text.raycastTarget = false;
    }

    private static GameObject FindThemeObject(GameObject root, string name)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            if (t.name == name) return t.gameObject;
        return null;
    }

    private static void SetThemeImage(GameObject go, Sprite sprite)
    {
        if (go == null || sprite == null) return;
        Image image = go.GetComponent<Image>();
        if (image == null) return;
        Undo.RecordObject(image, "シンプルUIスキン");
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        // raycastTarget はそのまま。Input / Button を妨害しない。
        EditorUtility.SetDirty(image);
    }

    private static GameObject ThemePlate(GameObject root, string name, Sprite sprite,
        float x, float y, float w, float h)
    {
        Image image = EnsureDecorImage(root.transform, name);
        Undo.RecordObject(image.rectTransform, "情報欄の位置");
        Place(image.rectTransform, x, y, w, h);
        Undo.RecordObject(image, "情報欄の外観");
        image.sprite = sprite;
        image.type = Image.Type.Sliced;
        image.color = Color.white;
        image.raycastTarget = false;
        return image.gameObject;
    }

    private static Image EnsureDecorImage(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null && existing.TryGetComponent(out Image found)) return found;
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(go, "UI情報欄追加");
        go.transform.SetParent(parent, false);
        return go.GetComponent<Image>();
    }

    private static void EnsureThemeLabel(GameObject root, string name, string content,
        int fontSize, Color color, float x, float y, float w, float h, TMP_Text fontSource)
    {
        GameObject go = FindThemeObject(root, name);
        if (go == null)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            Undo.RegisterCreatedObjectUndo(go, "UI見出し追加");
            go.transform.SetParent(root.transform, false);
        }
        TextMeshProUGUI text = go.GetComponent<TextMeshProUGUI>();
        if (text == null) return;
        Undo.RecordObject(text, "UI見出し");
        if (fontSource != null && fontSource.font != null) text.font = fontSource.font;
        text.text = content;
        text.alignment = TextAlignmentOptions.Left;
        text.overflowMode = TextOverflowModes.Ellipsis;
        StyleText(text, color, fontSize, false);
        Undo.RecordObject(text.rectTransform, "UI見出し位置");
        Place(text.rectTransform, x, y, w, h);
    }

    private static Sprite BuildFlatSprite(string filename, Color fill, Color border, bool hasBorder = true)
    {
        if (!AssetDatabase.IsValidFolder("Assets/ExpeditionTradeV2"))
            AssetDatabase.CreateFolder("Assets", "ExpeditionTradeV2");
        if (!AssetDatabase.IsValidFolder(ThemeAssets))
            AssetDatabase.CreateFolder("Assets/ExpeditionTradeV2", "SimpleTheme");

        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Clamp;
        Color32[] pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            int d = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
            pixels[y * size + x] = hasBorder && d < 2 ? border : fill;
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        string destination = Path.Combine(Application.dataPath,
            "ExpeditionTradeV2", "SimpleTheme", filename + ".png");
        File.WriteAllBytes(destination, texture.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(texture);
        string assetPath = ThemeAssets + "/" + filename + ".png";
        AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer != null)
        {
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spriteBorder = new Vector4(4, 4, 4, 4);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }
        return AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
    }

    private static Color Hex(string value)
    {
        ColorUtility.TryParseHtmlString(value, out Color color);
        return color;
    }
}
#endif
