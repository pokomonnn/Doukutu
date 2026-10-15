#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 出撃補充UIのEditor用1クリック自動セットアップ。
/// 既存のStageExpeditionPanelController・PawnShopUIController・MerchantStockInventoryを利用し、
/// 新規のCanvasパネル、医療品3種の直接購入UI、各ボタンの参照接続を自動生成します。
/// Runtimeでは動作せず、Unity Editor上でのみ使用します。
/// </summary>
public class ExpeditionSupplyAutoSetupWindow : EditorWindow
{
    private StageExpeditionPanelController stagePanel;
    private PawnShopUIController pawnShop;
    private MerchantStockInventory merchantStock;
    private MedicalKitInventory2D sceneKit;
    private Canvas targetCanvas;
    private TMP_FontAsset japaneseFont;
    private ConsumableItemData antidote;
    private ConsumableItemData splint;
    private ConsumableItemData bandage;
    private int antidotePrice = 150;
    private int splintPrice = 150;
    private int bandagePrice = 100;
    private int fallbackKitCapacity = 3;
    private AudioClip successSound;
    private AudioClip failSound;
    private Vector2 scroll;

    private const string GeneratedPanelName = "ExpeditionSupplyPanel_AUTO";
    private const string GeneratedManagerName = "ExpeditionSupplyManager_AUTO";
    private static readonly Color Bg = Hex("#191E29");
    private static readonly Color CardColor = Hex("#293242");
    private static readonly Color Accent = Hex("#8CBFE5");
    private static readonly Color SecondaryText = Hex("#B9C5D8");

    [MenuItem("Tools/2D Shooter/出撃用アイテム補充画面を自動生成")]
    public static void ShowWindow()
    {
        GetWindow<ExpeditionSupplyAutoSetupWindow>("出撃補充 UI 自動作成").Show();
    }

    private void OnEnable() { AutoFind(); }

    private void AutoFind()
    {
        if (stagePanel == null) stagePanel = FindUniqueInCurrentScene<StageExpeditionPanelController>();
        if (pawnShop == null) pawnShop = FindUniqueInCurrentScene<PawnShopUIController>();
        if (merchantStock == null) merchantStock = FindUniqueInCurrentScene<MerchantStockInventory>();
        if (sceneKit == null) sceneKit = FindUniqueInCurrentScene<MedicalKitInventory2D>();
        if (targetCanvas == null && stagePanel != null)
        {
            Canvas parent = stagePanel.GetComponentInParent<Canvas>(true);
            targetCanvas = parent != null ? parent.rootCanvas : null;
        }
        if (targetCanvas == null) targetCanvas = FindInCurrentScene<Canvas>();
        if (japaneseFont == null) japaneseFont = TMP_Settings.defaultFontAsset;
        if (sceneKit != null)
        {
            if (antidote == null) antidote = sceneKit.GetItemAtSlot(0);
            if (splint == null) splint = sceneKit.GetItemAtSlot(1);
            if (bandage == null) bandage = sceneKit.GetItemAtSlot(2);
            fallbackKitCapacity = sceneKit.MaxAmountPerSlot;
        }
        if (antidote == null) antidote = SearchItemByDisplayName(new[] { "解毒", "antidote" });
        if (splint == null) splint = SearchItemByDisplayName(new[] { "副木", "splint" });
        if (bandage == null) bandage = SearchItemByDisplayName(new[] { "包帯", "bandage" });
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("出撃用アイテム補充UI 自動生成", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Town_Mainシーンを開いて実行してください。既存の出撃選択と商人の購入画面を再利用して、医療品3種類の補充UIを自動で作成・接続します。", MessageType.Info);
        if (GUILayout.Button("シーンから参照を再検出")) AutoFind();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("既存コンポーネント", EditorStyles.boldLabel);
        stagePanel = (StageExpeditionPanelController)EditorGUILayout.ObjectField("出撃パネル（必須）", stagePanel, typeof(StageExpeditionPanelController), true);
        pawnShop = (PawnShopUIController)EditorGUILayout.ObjectField("既存の売買UI", pawnShop, typeof(PawnShopUIController), true);
        merchantStock = (MerchantStockInventory)EditorGUILayout.ObjectField("販売する商品棚", merchantStock, typeof(MerchantStockInventory), true);
        sceneKit = (MedicalKitInventory2D)EditorGUILayout.ObjectField("町の医療キット（任意）", sceneKit, typeof(MedicalKitInventory2D), true);
        targetCanvas = (Canvas)EditorGUILayout.ObjectField("親Canvas（任意）", targetCanvas, typeof(Canvas), true);
        japaneseFont = (TMP_FontAsset)EditorGUILayout.ObjectField("日本語対応TMPフォント", japaneseFont, typeof(TMP_FontAsset), false);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("応急キットの販売品（固定スロット順）", EditorStyles.boldLabel);
        antidote = (ConsumableItemData)EditorGUILayout.ObjectField("0：解毒剤", antidote, typeof(ConsumableItemData), false);
        antidotePrice = Mathf.Max(0, EditorGUILayout.IntField("解毒剤の価格(G)", antidotePrice));
        splint = (ConsumableItemData)EditorGUILayout.ObjectField("1：副木", splint, typeof(ConsumableItemData), false);
        splintPrice = Mathf.Max(0, EditorGUILayout.IntField("副木の価格(G)", splintPrice));
        bandage = (ConsumableItemData)EditorGUILayout.ObjectField("2：包帯", bandage, typeof(ConsumableItemData), false);
        bandagePrice = Mathf.Max(0, EditorGUILayout.IntField("包帯の価格(G)", bandagePrice));
        fallbackKitCapacity = Mathf.Max(1, EditorGUILayout.IntField("各品目の最大所持数", fallbackKitCapacity));
        successSound = (AudioClip)EditorGUILayout.ObjectField("購入成功SE（任意）", successSound, typeof(AudioClip), false);
        failSound = (AudioClip)EditorGUILayout.ObjectField("購入失敗SE（任意）", failSound, typeof(AudioClip), false);

        if (stagePanel == null)
            EditorGUILayout.HelpBox("出撃パネルが未設定です。Town_MainのStageExpeditionPanelControllerを割り当ててください。", MessageType.Error);
        if (pawnShop == null || merchantStock == null)
            EditorGUILayout.HelpBox("既存の売買UIまたは商人在庫が未設定です。医療品の購入画面は生成できますが、『通常アイテム購入』はこれらを登録するまで使用できません。", MessageType.Warning);
        if (antidote == null || splint == null || bandage == null)
            EditorGUILayout.HelpBox("未設定の医療品があります。作成はできますが、その品目はItemDataを設定するまで購入できません。", MessageType.Warning);
        if (japaneseFont == null || japaneseFont == TMP_Settings.defaultFontAsset)
            EditorGUILayout.HelpBox("日本語表示には、日本語グリフが収録されたTMP Font Assetの指定を推奨します。初期フォントでは文字化けすることがあります。", MessageType.Warning);
        EditorGUILayout.Space(14);

        using (new EditorGUI.DisabledScope(stagePanel == null || Application.isPlaying))
        {
            if (GUILayout.Button("補充画面をまとめて自動生成・接続", GUILayout.Height(44)))
                CreateAll();
        }
        EditorGUILayout.HelpBox(
            "生成済みの _AUTO オブジェクトがある場合は、確認ダイアログ後に置き換えます。手作業で編集した _AUTO 画面がある場合は事前に保存してください。", MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void CreateAll()
    {
        if (stagePanel == null || !stagePanel.gameObject.scene.IsValid() || !stagePanel.gameObject.scene.isLoaded)
        {
            EditorUtility.DisplayDialog("設定不足", "Town_Mainの出撃パネルを指定してください。", "OK");
            return;
        }
        Scene scene = stagePanel.gameObject.scene;
        if (scene != SceneManager.GetActiveScene())
        {
            EditorUtility.DisplayDialog("Sceneを確認", "出撃パネルがあるシーンをActiveにしてから実行してください。", "OK");
            return;
        }
        GameObject oldPanel = FindNamedObject(scene, GeneratedPanelName);
        GameObject oldManager = FindNamedObject(scene, GeneratedManagerName);
        if ((oldPanel != null || oldManager != null) &&
            !EditorUtility.DisplayDialog("既存の自動生成UI", "以前作成した _AUTO 画面を置き換えます。続けますか？", "置き換える", "キャンセル"))
            return;

        if (oldPanel != null) Undo.DestroyObjectImmediate(oldPanel);
        if (oldManager != null) Undo.DestroyObjectImmediate(oldManager);

        Canvas canvas = ResolveCanvas(scene);
        if (canvas == null) return;
        Transform parent = canvas.transform;
        // 元の出撃パネルの子に入れてしまうと、Stageを非表示にした時に補充画面も消える。
        if (stagePanel.PanelRoot != null &&
            (canvas.gameObject == stagePanel.PanelRoot || canvas.transform.IsChildOf(stagePanel.PanelRoot.transform)))
        {
            EditorUtility.DisplayDialog("Canvasの選択", "出撃パネルの外側にあるCanvasを指定してください。", "OK");
            return;
        }

        GameObject managerGo = CreateObject(GeneratedManagerName, parent);
        ExpeditionSupplyPanelController2D supplyManager = Undo.AddComponent<ExpeditionSupplyPanelController2D>(managerGo);
        GameObject uiRoot = CreateImage(GeneratedPanelName, parent, new Color(0.015f, 0.02f, 0.04f, 0.83f), true);
        FillParent(uiRoot.GetComponent<RectTransform>());
        uiRoot.transform.SetAsLastSibling();

        GameObject board = CreateImage("MainBoard", uiRoot.transform, Bg, false);
        Place(board.GetComponent<RectTransform>(), 0, 0, 1160, 805);
        GameObject trim = CreateImage("TopAccent", board.transform, Accent, false);
        Place(trim.GetComponent<RectTransform>(), 0, 399, 1160, 6);

        AddText("HeaderTag", board.transform, "PREPARE FOR EXPEDITION", 21, SecondaryText,
            0, 360, 960, 36, TextAlignmentOptions.Center);
        AddText("Title", board.transform, "出撃前の補給", 47, Color.white,
            0, 301, 980, 70, TextAlignmentOptions.Center);
        AddText("InfoTag", board.transform, "出撃先・地点を確認し、必要な医療品と装備を購入してください", 21, SecondaryText,
            0, 254, 960, 40, TextAlignmentOptions.Center);
        TMP_Text stageName = AddText("StageNameText", board.transform, "出撃先：未選択", 25, Color.white,
            -295, 204, 480, 42, TextAlignmentOptions.Left);
        TMP_Text entryName = AddText("EntryPointNameText", board.transform, "出撃地点：未選択", 23, SecondaryText,
            -295, 163, 480, 42, TextAlignmentOptions.Left);
        TMP_Text moneyText = AddText("MoneyText", board.transform, "所持金：--- G", 25, Hex("#FAD58A"),
            349, 184, 330, 54, TextAlignmentOptions.Right);

        GameObject shopArea = CreateImage("NormalItemShop", board.transform, CardColor, false);
        Place(shopArea.GetComponent<RectTransform>(), 0, 81, 1080, 99);
        AddText("NormalItemTitle", shopArea.transform, "通常アイテムの購入", 29, Color.white,
            -297, 18, 420, 41, TextAlignmentOptions.Left);
        AddText("NormalItemSub", shopArea.transform, "既存の商人ショップから購入できます", 19, SecondaryText,
            -297, -21, 420, 35, TextAlignmentOptions.Left);
        Button openShop = CreateButton("OpenNormalShopButton", shopArea.transform,
            "商品を見る", Hex("#466C92"), 390, 0, 245, 57, 24);

        AddText("MedicalSectionTitle", board.transform, "応急キットを補充する", 30, Color.white,
            -327, -19, 430, 46, TextAlignmentOptions.Left);
        AddText("MedicalSectionNote", board.transform, "購入ボタン1回で1個入手  /  直接キットに収納", 18, SecondaryText,
            265, -20, 500, 36, TextAlignmentOptions.Right);

        CardRefs[] cards = new CardRefs[3];
        cards[0] = MakeMedicalCard(board.transform, -360, -160, "解毒剤", "毒の応急処置", antidote);
        cards[1] = MakeMedicalCard(board.transform, 0, -160, "副木", "骨折の応急処置", splint);
        cards[2] = MakeMedicalCard(board.transform, 360, -160, "包帯", "出血の応急処置", bandage);

        TMP_Text statusText = AddText("StatusText", board.transform, "", 20, Hex("#F1DEA9"),
            -30, -301, 1000, 40, TextAlignmentOptions.Center);
        GameObject footer = CreateImage("FooterAccent", board.transform, Hex("#3D4758"), false);
        Place(footer.GetComponent<RectTransform>(), 0, -329, 1080, 2);
        Button back = CreateButton("BackButton", board.transform, "戻る", Hex("#3A4659"), -349, -364, 280, 62, 25);
        Button depart = CreateButton("FinalDepartButton", board.transform, "出撃する", Hex("#547A6C"), 350, -364, 360, 62, 26);

        MedicalKitSupplyShop2D medicineShop = Undo.AddComponent<MedicalKitSupplyShop2D>(uiRoot);
        AudioSource audio = Undo.AddComponent<AudioSource>(uiRoot);
        audio.playOnAwake = false;
        audio.spatialBlend = 0f;

        SerializedObject medSettings = new SerializedObject(medicineShop);
        SetObject(medSettings, "medicalKit", sceneKit);
        SetObject(medSettings, "gameSessionManager", FindInCurrentScene<GameSessionManager>());
        SetObject(medSettings, "moneyText", moneyText);
        SetObject(medSettings, "statusText", statusText);
        SetObject(medSettings, "audioSource", audio);
        SetObject(medSettings, "successSound", successSound);
        SetObject(medSettings, "failedSound", failSound);
        SetInt(medSettings, "fallbackMaxPerSlot", fallbackKitCapacity);
        SerializedProperty slots = medSettings.FindProperty("supplies");
        slots.arraySize = 3;
        ConsumableItemData[] items = { antidote, splint, bandage };
        int[] prices = { antidotePrice, splintPrice, bandagePrice };
        for (int i = 0; i < 3; i++)
        {
            SerializedProperty p = slots.GetArrayElementAtIndex(i);
            p.FindPropertyRelative("slotIndex").intValue = i;
            p.FindPropertyRelative("itemData").objectReferenceValue = items[i];
            p.FindPropertyRelative("priceG").intValue = prices[i];
            p.FindPropertyRelative("buyOneButton").objectReferenceValue = cards[i].BuyButton;
            p.FindPropertyRelative("itemIcon").objectReferenceValue = cards[i].Icon;
            p.FindPropertyRelative("itemNameText").objectReferenceValue = cards[i].Name;
            p.FindPropertyRelative("priceText").objectReferenceValue = cards[i].Price;
            p.FindPropertyRelative("stockText").objectReferenceValue = cards[i].Stock;
        }
        medSettings.ApplyModifiedProperties();

        SerializedObject managerSettings = new SerializedObject(supplyManager);
        SetObject(managerSettings, "supplyPanelRoot", uiRoot);
        SetObject(managerSettings, "stageNameText", stageName);
        SetObject(managerSettings, "entryPointNameText", entryName);
        SetObject(managerSettings, "statusText", statusText);
        SetObject(managerSettings, "backButton", back);
        SetObject(managerSettings, "finalDepartButton", depart);
        SetObject(managerSettings, "openNormalItemShopButton", openShop);
        SetObject(managerSettings, "existingPawnShop", pawnShop);
        SetObject(managerSettings, "supplyMerchantStock", merchantStock);
        SetObject(managerSettings, "medicalKitSupplyShop", medicineShop);
        managerSettings.ApplyModifiedProperties();

        SerializedObject stageSettings = new SerializedObject(stagePanel);
        SetObject(stageSettings, "supplyPanelController", supplyManager);
        SetBool(stageSettings, "useSupplyPreparationScreen", true);
        SetBool(stageSettings, "autoSetNextButtonText", true);
        SerializedProperty departButton = stageSettings.FindProperty("departButton");
        Button originalDepart = departButton != null ? departButton.objectReferenceValue as Button : null;
        stageSettings.ApplyModifiedProperties();
        int removed = RemoveUnsafeSceneTransition(originalDepart);
        SetNextLabelOnStage(originalDepart);

        uiRoot.SetActive(false);
        EnsureEventSystem(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = managerGo;
        EditorGUIUtility.PingObject(managerGo);
        Debug.Log($"[ExpeditionSupplyAutoSetup] 完了：補充画面・医療品ボタン×3・所持金・戻る・出撃・商人UIとの連携を生成しました。Scene={scene.name} / 直接Scene移動OnClick除去={removed}", managerGo);
        EditorUtility.DisplayDialog("作成完了", "出撃用アイテム補充画面を生成しました。\n\n・医療品のItemDataと販売価格を確認\n・既存の商人の商品棚を確認\n・Playして『次へ』から動作確認\n\n※ Ctrl+S でシーンを保存してください。", "OK");
    }

    private CardRefs MakeMedicalCard(Transform board, float x, float y, string fallbackName, string description, ConsumableItemData item)
    {
        GameObject card = CreateImage(fallbackName + "Card", board, CardColor, false);
        Place(card.GetComponent<RectTransform>(), x, y, 340, 246);
        TMP_Text name = AddText("ItemName", card.transform, item != null ? item.DisplayName : fallbackName,
            28, Color.white, 0, 76, 296, 40, TextAlignmentOptions.Center);
        AddText("ItemDescription", card.transform, description,
            18, SecondaryText, 0, 40, 295, 34, TextAlignmentOptions.Center);
        GameObject iconBg = CreateImage("IconBackground", card.transform, Hex("#1C2330"), false);
        Place(iconBg.GetComponent<RectTransform>(), -99, -19, 100, 100);
        Image icon = CreateImage("ItemIcon", iconBg.transform, Color.white, false).GetComponent<Image>();
        Place(icon.GetComponent<RectTransform>(), 0, 0, 82, 82);
        icon.sprite = item != null ? item.Icon : null;
        icon.enabled = icon.sprite != null;
        icon.preserveAspect = true;
        TMP_Text price = AddText("Price", card.transform, "--- G / 個", 23, Hex("#FAD58A"),
            45, -1, 163, 39, TextAlignmentOptions.Center);
        TMP_Text stock = AddText("Stock", card.transform, "所持 0 / 3", 19, SecondaryText,
            45, -38, 165, 35, TextAlignmentOptions.Center);
        Button buy = CreateButton("BuyOneButton", card.transform, "1個購入", Hex("#42745E"),
            0, -92, 296, 53, 23);
        return new CardRefs { BuyButton = buy, Icon = icon, Name = name, Price = price, Stock = stock };
    }

    private struct CardRefs
    {
        public Button BuyButton;
        public Image Icon;
        public TMP_Text Name;
        public TMP_Text Price;
        public TMP_Text Stock;
    }

    private Canvas ResolveCanvas(Scene scene)
    {
        Canvas canvas = targetCanvas;
        if (canvas != null && canvas.gameObject.scene != scene)
            canvas = null;
        if (canvas == null)
        {
            Canvas attached = stagePanel.GetComponentInParent<Canvas>(true);
            if (attached != null) canvas = attached.rootCanvas;
        }
        if (canvas != null) return canvas;
        GameObject go = new GameObject("ExpeditionSupplyCanvas_AUTO", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "生成：出撃準備Canvas");
        SceneManager.MoveGameObjectToScene(go, scene);
        canvas = Undo.AddComponent<Canvas>(go);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        CanvasScaler scaler = Undo.AddComponent<CanvasScaler>(go);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        Undo.AddComponent<GraphicRaycaster>(go);
        targetCanvas = canvas;
        return canvas;
    }

    private static void EnsureEventSystem(Scene scene)
    {
        // 既存のEventSystemを変更しない。シーンに無い時だけ作成。
        if (FindInCurrentScene<EventSystem>() != null) return;
        GameObject es = new GameObject("ExpeditionUIEventSystem_AUTO");
        Undo.RegisterCreatedObjectUndo(es, "出撃準備：EventSystem生成");
        SceneManager.MoveGameObjectToScene(es, scene);
        Undo.AddComponent<EventSystem>(es);

        // Input Systemパッケージがある場合は新Input System用UI Moduleを優先。
        Type inputModuleType = Type.GetType("UnityEngine.InputSystem.UI.InputSystemUIInputModule, Unity.InputSystem");
        if (inputModuleType != null && typeof(BaseInputModule).IsAssignableFrom(inputModuleType))
            Undo.AddComponent(es, inputModuleType);
        else
            Undo.AddComponent<StandaloneInputModule>(es);
    }

    private static GameObject CreateObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "生成：" + name);
        go.layer = LayerMask.NameToLayer("UI") < 0 ? 0 : LayerMask.NameToLayer("UI");
        if (parent != null) Undo.SetTransformParent(go.transform, parent, "UI親設定");
        return go;
    }

    private static GameObject CreateImage(string name, Transform parent, Color color, bool raycastTarget)
    {
        GameObject go = CreateObject(name, parent);
        Image image = Undo.AddComponent<Image>(go);
        image.color = color;
        image.raycastTarget = raycastTarget;
        return go;
    }

    private TMP_Text AddText(string name, Transform parent, string value, float size, Color color,
        float x, float y, float w, float h, TextAlignmentOptions alignment)
    {
        GameObject go = CreateObject(name, parent);
        TextMeshProUGUI tmp = Undo.AddComponent<TextMeshProUGUI>(go);
        if (japaneseFont != null) tmp.font = japaneseFont;
        tmp.text = value;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        tmp.overflowMode = TextOverflowModes.Ellipsis;
        Place(go.GetComponent<RectTransform>(), x, y, w, h);
        return tmp;
    }

    private Button CreateButton(string name, Transform parent, string caption, Color color,
        float x, float y, float w, float h, float fontSize)
    {
        GameObject bg = CreateImage(name, parent, color, true);
        Place(bg.GetComponent<RectTransform>(), x, y, w, h);
        Button button = Undo.AddComponent<Button>(bg);
        button.targetGraphic = bg.GetComponent<Image>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Hex("#DBE9FF");
        colors.pressedColor = Hex("#93B0D6");
        colors.selectedColor = Color.white;
        colors.disabledColor = Hex("#707780");
        colors.colorMultiplier = 1f;
        button.colors = colors;
        AddText(name + "Label", bg.transform, caption, fontSize, Color.white,
            0, 0, w - 14, h - 6, TextAlignmentOptions.Center);
        return button;
    }

    private static void FillParent(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Place(RectTransform rect, float x, float y, float w, float h)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(w, h);
        rect.anchoredPosition = new Vector2(x, y);
        rect.localScale = Vector3.one;
    }

    private static void SetObject(SerializedObject so, string name, UnityEngine.Object value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.objectReferenceValue = value;
        else Debug.LogWarning("[ExpeditionSupplyAutoSetup] Inspectorプロパティが見つかりません: " + name);
    }
    private static void SetInt(SerializedObject so, string name, int value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.intValue = value;
    }
    private static void SetBool(SerializedObject so, string name, bool value)
    {
        SerializedProperty p = so.FindProperty(name);
        if (p != null) p.boolValue = value;
    }

    private static void SetNextLabelOnStage(Button button)
    {
        if (button == null) return;
        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null)
        {
            Undo.RecordObject(tmp, "出撃Buttonを次へに変更");
            tmp.text = "次へ";
            EditorUtility.SetDirty(tmp);
            return;
        }
        Text legacy = button.GetComponentInChildren<Text>(true);
        if (legacy != null)
        {
            Undo.RecordObject(legacy, "出撃Buttonを次へに変更");
            legacy.text = "次へ";
            EditorUtility.SetDirty(legacy);
        }
    }

    // シーン内に1つだけ存在する場合に限り自動選択。複数のショップから間違って選ばない。
    private static T FindUniqueInCurrentScene<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        T unique = null;
        foreach (T obj in UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (obj == null || obj.gameObject.scene != scene) continue;
            if (unique != null) return null;
            unique = obj;
        }
        return unique;
    }

    // 出撃選択Buttonの旧OnClickに残っているSceneTransitionButtonの直接遷移のみ削除。
    // ほかの音声・演出・ユーザー独自のOnClickは消しません。
    private static int RemoveUnsafeSceneTransition(Button button)
    {
        if (button == null) return 0;
        int removed = 0;
        for (int i = button.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
        {
            if (!(button.onClick.GetPersistentTarget(i) is SceneTransitionButton)) continue;
            string method = button.onClick.GetPersistentMethodName(i);
            if (method != "LoadConfiguredScene" && method != "LoadSceneByName") continue;
            Undo.RecordObject(button, "直接Scene遷移を除去");
            UnityEventTools.RemovePersistentListener(button.onClick, i);
            EditorUtility.SetDirty(button);
            removed++;
        }
        return removed;
    }

    private static T FindInCurrentScene<T>() where T : Component
    {
        Scene scene = SceneManager.GetActiveScene();
        T[] all = UnityEngine.Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (T obj in all)
            if (obj != null && obj.gameObject.scene == scene) return obj;
        return null;
    }

    private static GameObject FindNamedObject(Scene scene, string targetName)
    {
        foreach (Transform t in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t != null && t.gameObject.scene == scene && t.name == targetName) return t.gameObject;
        }
        return null;
    }

    private static ConsumableItemData SearchItemByDisplayName(string[] candidates)
    {
        List<ConsumableItemData> found = new List<ConsumableItemData>();
        foreach (string guid in AssetDatabase.FindAssets("t:ConsumableItemData"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            ConsumableItemData item = AssetDatabase.LoadAssetAtPath<ConsumableItemData>(path);
            if (item == null) continue;
            string display = (item.DisplayName ?? string.Empty).ToLowerInvariant();
            string assetName = item.name.ToLowerInvariant();
            foreach (string candidate in candidates)
            {
                if (!display.Contains(candidate) && !assetName.Contains(candidate)) continue;
                found.Add(item);
                break;
            }
        }
        // 名前の候補が複数ある時は間違った商品を勝手にセットしない。
        return found.Count == 1 ? found[0] : null;
    }

    private static Color Hex(string code)
    {
        ColorUtility.TryParseHtmlString(code, out Color result);
        return result;
    }
}
#endif
