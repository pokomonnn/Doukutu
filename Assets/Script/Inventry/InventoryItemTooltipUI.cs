using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InventoryItemUIへマウスを乗せた時に、カーソル付近へItem情報を表示します。
///
/// ・通常はカーソル右側へ表示
/// ・画面右端では自動で左側へ切り替え
/// ・画面上下からはみ出さないよう位置補正
/// ・通常Inventory / ItemBox Inventoryの両方で共通利用
/// ・ItemDataの言語切替にも追従
///
/// Sceneに手動配置しなくても、最初のHover時にRoot Canvas直下へ自動生成します。
/// 手動で配置したい場合は、Canvas配下のGameObjectへこのComponentを付けても構いません。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class InventoryItemTooltipUI : MonoBehaviour
{
    private static InventoryItemTooltipUI instance;

    [Header("サイズ")]
    [SerializeField, Min(200f)]
    private float tooltipWidth = 340f;

    [SerializeField, Min(120f)]
    private float minimumHeight = 210f;

    [SerializeField, Min(150f)]
    private float maximumHeight = 420f;

    [Header("カーソルからの距離")]
    [Tooltip("通常はカーソルの右上寄りへ表示します")]
    [SerializeField]
    private Vector2 cursorOffset = new Vector2(20f, 12f);

    [Header("表示設定")]
    [SerializeField] private bool showIcon = true;
    [SerializeField] private bool showItemType = true;
    [SerializeField] private bool showAmount = true;
    [SerializeField] private bool showWeight = true;
    [SerializeField] private bool showTotalWeightForStack = true;
    [SerializeField] private bool showSellPrice = true;

    [Tooltip(
        "商人の商品Inventoryにカーソルを乗せた時だけ、" +
        "その商人での実際の購入金額を表示します。" +
        "Player Inventoryや通常ItemBoxでは表示されません。"
    )]
    [SerializeField] private bool showMerchantPurchasePrice = true;

    [Header("武器の耐久状態")]
    [Tooltip(
        "武器ItemのTooltip最上段に、耐久度Textと読み取り専用Sliderを表示します。100%=新品、0%=完全破損です。"
    )]
    [SerializeField] private bool showWeaponDamage = true;

    [Tooltip("武器の損傷度表示エリアの高さです。")]
    [SerializeField, Min(44f)]
    private float weaponDamageSectionHeight = 56f;

    [Tooltip("損傷度Textの文字サイズです。")]
    [SerializeField, Min(8f)]
    private float weaponDamageFontSize = 15f;

    [Tooltip("損傷度Sliderの高さです。")]
    [SerializeField, Min(4f)]
    private float weaponDamageSliderHeight = 12f;

    [Tooltip("耐久度100%付近の色です。")]
    [SerializeField]
    private Color weaponDurabilityHighColor =
        Color.white;

    [Tooltip("耐久度50%の色です。")]
    [SerializeField]
    private Color weaponDurabilityMiddleColor =
        new Color(1f, 0.9f, 0.05f, 1f);

    [Tooltip("耐久度10%以下の色です。")]
    [SerializeField]
    private Color weaponDurabilityLowColor =
        new Color(1f, 0.12f, 0.08f, 1f);

    [SerializeField]
    private Color weaponDamageSliderBackgroundColor =
        new Color(0.18f, 0.18f, 0.18f, 0.95f);

    [Header("文字サイズ")]
    [SerializeField, Min(8f)] private float itemNameFontSize = 24f;
    [SerializeField, Min(8f)] private float descriptionFontSize = 17f;
    [SerializeField, Min(8f)] private float detailFontSize = 16f;

    [Header("日本語Font")]
    [Tooltip(
        "Tooltip内のItemName / Description / Detailsに使用するTextMeshPro Font Assetです。" +
        "日本語対応のTMP_FontAssetを設定してください。未設定ならTMPのデフォルトFontを使用します。"
    )]
    [SerializeField] private TMP_FontAsset japaneseFont;

    [Header("見た目")]
    [SerializeField]
    private Color backgroundColor =
        new Color(0.06f, 0.07f, 0.08f, 0.96f);

    [SerializeField]
    private Color itemNameColor = Color.white;

    [SerializeField]
    private Color descriptionColor =
        new Color(0.88f, 0.88f, 0.88f, 1f);

    [SerializeField]
    private Color detailColor =
        new Color(0.76f, 0.80f, 0.84f, 1f);

    private RectTransform panelRect;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;
    private Image backgroundImage;

    private Image iconImage;
    private TMP_Text itemNameText;
    private TMP_Text descriptionText;
    private TMP_Text detailsText;

    private RectTransform weaponDamageRoot;
    private TMP_Text weaponDamageText;
    private Slider weaponDamageSlider;
    private Image weaponDamageSliderBackground;
    private Image weaponDamageSliderFill;

    private InventoryItem currentItem;

    // 現在HoverしているItemが商人在庫に属する時だけ設定されます。
    // Player Inventory / 通常ItemBoxではnullです。
    private MerchantStockInventory currentMerchantStock;

    private bool isVisible;

    private static Sprite whiteSprite;

    public static bool IsVisible =>
        instance != null &&
        instance.isVisible;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;

        panelRect = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        EnsureVisuals();
        HideImmediate();
    }

    private void OnEnable()
    {
        ItemData.OnLocalizedTextChanged +=
            HandleLocalizedTextChanged;
    }

    private void OnDisable()
    {
        ItemData.OnLocalizedTextChanged -=
            HandleLocalizedTextChanged;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    private void LateUpdate()
    {
        if (!isVisible ||
            currentItem == null ||
            currentItem.ItemData == null)
        {
            return;
        }

        UpdatePosition(Input.mousePosition);
    }

    /// <summary>
    /// Item情報を表示します。
    /// TooltipがSceneに無い場合はRoot Canvas直下へ自動生成します。
    /// </summary>
    public static void Show(
        InventoryItem item,
        Canvas canvas,
        MerchantStockInventory merchantStock = null)
    {
        if (item == null ||
            item.ItemData == null ||
            canvas == null)
        {
            return;
        }

        InventoryItemTooltipUI tooltip =
            GetOrCreate(canvas.rootCanvas);

        if (tooltip == null)
        {
            return;
        }

        tooltip.ShowInternal(
            item,
            merchantStock
        );
    }

    /// <summary>
    /// 指定Itemを表示中の場合だけTooltipを閉じます。
    /// </summary>
    public static void HideFor(InventoryItem item)
    {
        if (instance == null)
        {
            return;
        }

        if (item == null ||
            instance.currentItem == item)
        {
            instance.HideInternal();
        }
    }

    /// <summary>
    /// 表示中のTooltipを無条件で閉じます。
    /// </summary>
    public static void Hide()
    {
        instance?.HideInternal();
    }

    private static InventoryItemTooltipUI GetOrCreate(
        Canvas canvas)
    {
        if (canvas == null)
        {
            return null;
        }

        if (instance != null)
        {
            instance.AttachToCanvas(canvas);
            return instance;
        }

        InventoryItemTooltipUI existing =
            canvas.GetComponentInChildren<
                InventoryItemTooltipUI
            >(true);

        if (existing != null)
        {
            instance = existing;
            instance.AttachToCanvas(canvas);
            instance.EnsureVisuals();
            return instance;
        }

        GameObject tooltipObject =
            new GameObject(
                "InventoryItemTooltip",
                typeof(RectTransform),
                typeof(CanvasGroup),
                typeof(Image),
                typeof(InventoryItemTooltipUI)
            );

        tooltipObject.transform.SetParent(
            canvas.transform,
            false
        );

        instance =
            tooltipObject.GetComponent<
                InventoryItemTooltipUI
            >();

        instance.AttachToCanvas(canvas);
        instance.EnsureVisuals();
        instance.HideImmediate();

        return instance;
    }

    private void AttachToCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        rootCanvas = canvas.rootCanvas;

        if (transform.parent != rootCanvas.transform)
        {
            transform.SetParent(
                rootCanvas.transform,
                false
            );
        }

        transform.SetAsLastSibling();
    }

    private void ShowInternal(
        InventoryItem item,
        MerchantStockInventory merchantStock)
    {
        currentItem = item;
        currentMerchantStock = merchantStock;

        EnsureVisuals();
        RefreshContent();

        gameObject.SetActive(true);

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        isVisible = true;

        transform.SetAsLastSibling();

        UpdatePosition(Input.mousePosition);
    }

    private void HideInternal()
    {
        currentItem = null;
        currentMerchantStock = null;
        isVisible = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void HideImmediate()
    {
        currentItem = null;
        currentMerchantStock = null;
        isVisible = false;

        if (canvasGroup == null)
        {
            canvasGroup =
                GetComponent<CanvasGroup>();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void RefreshContent()
    {
        if (currentItem == null ||
            currentItem.ItemData == null)
        {
            HideInternal();
            return;
        }

        ItemData data = currentItem.ItemData;

        itemNameText.text = data.DisplayName;

        descriptionText.text =
            string.IsNullOrWhiteSpace(data.Description)
                ? "説明なし"
                : data.Description;

        iconImage.gameObject.SetActive(
            showIcon &&
            data.Icon != null
        );

        if (data.Icon != null)
        {
            iconImage.sprite = data.Icon;
            iconImage.color = Color.white;
        }

        RefreshWeaponDamageDisplay(data);

        System.Text.StringBuilder details =
            new System.Text.StringBuilder();

        if (showItemType)
        {
            AppendDetailLine(
                details,
                $"種類：{GetItemTypeLabel(data.ItemType)}"
            );
        }

        if (showAmount)
        {
            AppendDetailLine(
                details,
                $"数量：{Mathf.Max(1, currentItem.Amount)}"
            );
        }

        if (showWeight)
        {
            AppendDetailLine(
                details,
                $"重量：{data.Weight:0.##} kg"
            );

            if (showTotalWeightForStack &&
                currentItem.Amount > 1)
            {
                AppendDetailLine(
                    details,
                    $"合計重量：" +
                    $"{data.Weight * currentItem.Amount:0.##} kg"
                );
            }
        }

        if (showSellPrice)
        {
            string sellText =
                data.CanSellToShop
                    ? $"{data.SellPrice} G"
                    : "売却不可";

            AppendDetailLine(
                details,
                $"売値：{sellText}"
            );
        }

        // 購入金額は商人の商品InventoryでHoverした時だけ表示する。
        // MerchantStockInventory.GetUnitBuyPriceを使用することで、
        // 店舗側の価格倍率なども反映した実際の購入単価と一致させます。
        if (showMerchantPurchasePrice &&
            currentMerchantStock != null)
        {
            int purchasePrice =
                Mathf.Max(
                    0,
                    currentMerchantStock.GetUnitBuyPrice(data)
                );

            AppendDetailLine(
                details,
                $"購入金額：{purchasePrice} G"
            );
        }

        detailsText.text =
            details.ToString();

        ResizeToContent();
    }

    /// <summary>
    /// 武器だけTooltip最上段へ耐久度を表示します。
    ///
    /// Sliderは残り耐久度を表すので、
    /// 1 = 新品 / 0 = 完全破損です。
    /// </summary>
    private void RefreshWeaponDamageDisplay(ItemData data)
    {
        bool isWeapon =
            showWeaponDamage &&
            data is WeaponItemData;

        if (weaponDamageRoot != null)
        {
            weaponDamageRoot.gameObject.SetActive(
                isWeapon
            );
        }

        if (!isWeapon ||
            currentItem == null)
        {
            return;
        }

        // 武器の現在耐久がまだ初期化されていない場合は、
        // WeaponItemData.MaxDurabilityを初期値として保存します。
        currentItem.EnsureWeaponDurabilityInitialized();

        float durabilityPercent =
            Mathf.Clamp01(
                currentItem.WeaponDurabilityPercent
            );

        string conditionLabel =
            GetWeaponDurabilityConditionLabel(
                durabilityPercent
            );

        string jamText =
            currentItem.StoredWeaponJammed
                ? " / ジャム中"
                : string.Empty;

        Color durabilityColor =
            GetWeaponDurabilityColor(
                durabilityPercent
            );

        if (weaponDamageText != null)
        {
            weaponDamageText.text =
                $"耐久度：{durabilityPercent * 100f:0.#}% " +
                $"（{conditionLabel}）" +
                jamText;

            weaponDamageText.color =
                durabilityColor;
        }

        if (weaponDamageSliderFill != null)
        {
            weaponDamageSliderFill.color =
                durabilityColor;
        }

        if (weaponDamageSlider != null)
        {
            weaponDamageSlider.minValue = 0f;
            weaponDamageSlider.maxValue = 1f;
            weaponDamageSlider.wholeNumbers = false;
            weaponDamageSlider.interactable = false;

            weaponDamageSlider.SetValueWithoutNotify(
                durabilityPercent
            );
        }
    }

    /// <summary>
    /// 耐久度に応じて
    /// 100%=白 / 50%=黄 / 10%以下=赤
    /// になるよう補間します。
    /// </summary>
    private Color GetWeaponDurabilityColor(
        float durabilityPercent)
    {
        float value =
            Mathf.Clamp01(durabilityPercent);

        // 50% ～ 100% : 黄 → 白
        if (value >= 0.5f)
        {
            float t =
                Mathf.InverseLerp(
                    0.5f,
                    1f,
                    value
                );

            return Color.Lerp(
                weaponDurabilityMiddleColor,
                weaponDurabilityHighColor,
                t
            );
        }

        // 10% ～ 50% : 赤 → 黄
        if (value > 0.1f)
        {
            float t =
                Mathf.InverseLerp(
                    0.1f,
                    0.5f,
                    value
                );

            return Color.Lerp(
                weaponDurabilityLowColor,
                weaponDurabilityMiddleColor,
                t
            );
        }

        // 10%以下は赤固定。
        return weaponDurabilityLowColor;
    }

    private static string GetWeaponDurabilityConditionLabel(
        float durabilityPercent)
    {
        float value =
            Mathf.Clamp01(durabilityPercent);

        if (value >= 0.999f)
        {
            return "新品";
        }

        if (value >= 0.75f)
        {
            return "良好";
        }

        if (value >= 0.50f)
        {
            return "軽度損傷";
        }

        if (value >= 0.25f)
        {
            return "摩耗";
        }

        if (value > 0f)
        {
            return "重度損傷";
        }

        return "完全破損";
    }

    private static void AppendDetailLine(
        System.Text.StringBuilder builder,
        string line)
    {
        if (builder.Length > 0)
        {
            builder.AppendLine();
        }

        builder.Append(line);
    }

    private static string GetItemTypeLabel(
        InventoryItemType itemType)
    {
        return itemType switch
        {
            InventoryItemType.Consumable => "消耗品",
            InventoryItemType.Ammo => "弾薬",
            InventoryItemType.Weapon => "武器",
            InventoryItemType.Armor => "防具",
            InventoryItemType.Equipment => "装備品",
            InventoryItemType.Treasure => "お宝",
            InventoryItemType.Quest => "クエスト",
            _ => "その他"
        };
    }

    private void ResizeToContent()
    {
        float contentWidth =
            tooltipWidth - 32f;

        float descriptionWidth =
            showIcon
                ? tooltipWidth - 116f
                : contentWidth;

        Vector2 descriptionPreferred =
            descriptionText.GetPreferredValues(
                descriptionText.text,
                Mathf.Max(80f, descriptionWidth),
                0f
            );

        Vector2 detailPreferred =
            detailsText.GetPreferredValues(
                detailsText.text,
                contentWidth,
                0f
            );

        float descriptionHeight =
            Mathf.Max(48f, descriptionPreferred.y);

        bool hasWeaponDamage =
            IsCurrentItemWeaponDamageVisible();

        float weaponDamageExtra =
            hasWeaponDamage
                ? weaponDamageSectionHeight + 10f
                : 0f;

        float desiredHeight =
            112f +
            descriptionHeight +
            Mathf.Max(40f, detailPreferred.y) +
            weaponDamageExtra;

        // 既存InspectorのMaximum Heightを変えなくても、
        // 武器の損傷度エリア分だけTooltipが上限より伸びられるようにする。
        float effectiveMaximumHeight =
            maximumHeight +
            weaponDamageExtra;

        desiredHeight =
            Mathf.Clamp(
                desiredHeight,
                minimumHeight,
                effectiveMaximumHeight
            );

        panelRect.sizeDelta =
            new Vector2(
                tooltipWidth,
                desiredHeight
            );

        LayoutVisuals();
    }

    private void UpdatePosition(Vector2 screenPosition)
    {
        if (rootCanvas == null ||
            panelRect == null)
        {
            return;
        }

        float scale =
            Mathf.Max(
                0.0001f,
                rootCanvas.scaleFactor
            );

        float widthPixels =
            panelRect.rect.width * scale;

        float heightPixels =
            panelRect.rect.height * scale;

        bool placeLeft =
            screenPosition.x +
            cursorOffset.x +
            widthPixels >
            Screen.width;

        Vector2 finalScreenPosition;

        if (placeLeft)
        {
            panelRect.pivot =
                new Vector2(1f, 1f);

            finalScreenPosition =
                new Vector2(
                    screenPosition.x -
                    Mathf.Abs(cursorOffset.x),
                    screenPosition.y +
                    cursorOffset.y
                );
        }
        else
        {
            panelRect.pivot =
                new Vector2(0f, 1f);

            finalScreenPosition =
                new Vector2(
                    screenPosition.x +
                    Mathf.Abs(cursorOffset.x),
                    screenPosition.y +
                    cursorOffset.y
                );
        }

        // 上にはみ出さない
        finalScreenPosition.y =
            Mathf.Min(
                finalScreenPosition.y,
                Screen.height - 4f
            );

        // 下にはみ出さない
        if (finalScreenPosition.y - heightPixels < 4f)
        {
            finalScreenPosition.y =
                heightPixels + 4f;
        }

        RectTransform canvasRect =
            rootCanvas.transform as RectTransform;

        if (canvasRect == null)
        {
            return;
        }

        Camera uiCamera =
            rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

        if (RectTransformUtility
            .ScreenPointToLocalPointInRectangle(
                canvasRect,
                finalScreenPosition,
                uiCamera,
                out Vector2 localPosition))
        {
            panelRect.anchoredPosition =
                localPosition;
        }
    }

    private void HandleLocalizedTextChanged(
        ItemData changedItem)
    {
        if (!isVisible ||
            currentItem == null ||
            currentItem.ItemData != changedItem)
        {
            return;
        }

        RefreshContent();
    }

    private void EnsureVisuals()
    {
        panelRect =
            GetComponent<RectTransform>();

        canvasGroup =
            GetComponent<CanvasGroup>();

        if (canvasGroup == null)
        {
            canvasGroup =
                gameObject.AddComponent<CanvasGroup>();
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        backgroundImage =
            GetComponent<Image>();

        if (backgroundImage == null)
        {
            backgroundImage =
                gameObject.AddComponent<Image>();
        }

        backgroundImage.color =
            backgroundColor;

        backgroundImage.raycastTarget =
            false;

        panelRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        panelRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        panelRect.pivot =
            new Vector2(0f, 1f);

        panelRect.sizeDelta =
            new Vector2(
                tooltipWidth,
                minimumHeight
            );

        EnsureWeaponDamageVisuals();
        EnsureIcon();
        EnsureNameText();
        EnsureDescriptionText();
        EnsureDetailsText();

        ApplyFontToAllTexts();
        LayoutVisuals();
    }

    private void EnsureWeaponDamageVisuals()
    {
        if (weaponDamageRoot == null)
        {
            Transform existingRoot =
                transform.Find("WeaponDamage");

            if (existingRoot != null)
            {
                weaponDamageRoot =
                    existingRoot as RectTransform;
            }
        }

        if (weaponDamageRoot == null)
        {
            GameObject rootObject =
                new GameObject(
                    "WeaponDamage",
                    typeof(RectTransform)
                );

            rootObject.transform.SetParent(
                transform,
                false
            );

            weaponDamageRoot =
                rootObject.GetComponent<RectTransform>();
        }

        if (weaponDamageText == null)
        {
            Transform existingText =
                weaponDamageRoot.Find("DamageText");

            TextMeshProUGUI text =
                existingText != null
                    ? existingText.GetComponent<
                        TextMeshProUGUI
                    >()
                    : null;

            if (text == null)
            {
                GameObject textObject =
                    new GameObject(
                        "DamageText",
                        typeof(RectTransform),
                        typeof(TextMeshProUGUI)
                    );

                textObject.transform.SetParent(
                    weaponDamageRoot,
                    false
                );

                text =
                    textObject.GetComponent<
                        TextMeshProUGUI
                    >();
            }

            weaponDamageText = text;
        }

        ApplyConfiguredFont(
            weaponDamageText
        );

        if (weaponDamageText != null)
        {
            weaponDamageText.fontSize =
                weaponDamageFontSize;

            weaponDamageText.color =
                weaponDurabilityHighColor;

            weaponDamageText.fontStyle =
                FontStyles.Bold;

            weaponDamageText.alignment =
                TextAlignmentOptions.TopLeft;

            weaponDamageText.raycastTarget =
                false;

            weaponDamageText.margin =
                Vector4.zero;
        }

        if (weaponDamageSlider == null)
        {
            Transform existingSlider =
                weaponDamageRoot.Find(
                    "DamageSlider"
                );

            if (existingSlider != null)
            {
                weaponDamageSlider =
                    existingSlider.GetComponent<
                        Slider
                    >();
            }
        }

        if (weaponDamageSlider == null)
        {
            GameObject sliderObject =
                new GameObject(
                    "DamageSlider",
                    typeof(RectTransform),
                    typeof(Slider)
                );

            sliderObject.transform.SetParent(
                weaponDamageRoot,
                false
            );

            weaponDamageSlider =
                sliderObject.GetComponent<Slider>();
        }

        weaponDamageSlider.direction =
            Slider.Direction.LeftToRight;

        weaponDamageSlider.interactable =
            false;

        weaponDamageSlider.transition =
            Selectable.Transition.None;

        Navigation navigation =
            weaponDamageSlider.navigation;

        navigation.mode =
            Navigation.Mode.None;

        weaponDamageSlider.navigation =
            navigation;

        RectTransform sliderRect =
            weaponDamageSlider.transform
                as RectTransform;

        if (sliderRect != null)
        {
            sliderRect.anchorMin =
                new Vector2(0f, 1f);

            sliderRect.anchorMax =
                new Vector2(1f, 1f);

            sliderRect.pivot =
                new Vector2(0.5f, 1f);
        }

        Transform backgroundTransform =
            weaponDamageSlider.transform.Find(
                "Background"
            );

        if (backgroundTransform != null)
        {
            weaponDamageSliderBackground =
                backgroundTransform.GetComponent<
                    Image
                >();
        }

        if (weaponDamageSliderBackground == null)
        {
            GameObject backgroundObject =
                new GameObject(
                    "Background",
                    typeof(RectTransform),
                    typeof(Image)
                );

            backgroundObject.transform.SetParent(
                weaponDamageSlider.transform,
                false
            );

            weaponDamageSliderBackground =
                backgroundObject.GetComponent<Image>();
        }

        RectTransform backgroundRect =
            weaponDamageSliderBackground
                .rectTransform;

        backgroundRect.anchorMin =
            Vector2.zero;

        backgroundRect.anchorMax =
            Vector2.one;

        backgroundRect.offsetMin =
            Vector2.zero;

        backgroundRect.offsetMax =
            Vector2.zero;

        weaponDamageSliderBackground.sprite =
            GetWhiteSprite();

        weaponDamageSliderBackground.color =
            weaponDamageSliderBackgroundColor;

        weaponDamageSliderBackground.raycastTarget =
            false;

        Transform fillAreaTransform =
            weaponDamageSlider.transform.Find(
                "FillArea"
            );

        RectTransform fillAreaRect =
            fillAreaTransform != null
                ? fillAreaTransform
                    .GetComponent<RectTransform>()
                : null;

        if (fillAreaRect == null)
        {
            GameObject fillAreaObject =
                new GameObject(
                    "FillArea",
                    typeof(RectTransform)
                );

            fillAreaObject.transform.SetParent(
                weaponDamageSlider.transform,
                false
            );

            fillAreaRect =
                fillAreaObject.GetComponent<
                    RectTransform
                >();
        }

        fillAreaRect.anchorMin =
            Vector2.zero;

        fillAreaRect.anchorMax =
            Vector2.one;

        fillAreaRect.offsetMin =
            Vector2.zero;

        fillAreaRect.offsetMax =
            Vector2.zero;

        Transform fillTransform =
            fillAreaRect.Find("Fill");

        if (fillTransform != null)
        {
            weaponDamageSliderFill =
                fillTransform.GetComponent<Image>();
        }

        if (weaponDamageSliderFill == null)
        {
            GameObject fillObject =
                new GameObject(
                    "Fill",
                    typeof(RectTransform),
                    typeof(Image)
                );

            fillObject.transform.SetParent(
                fillAreaRect,
                false
            );

            weaponDamageSliderFill =
                fillObject.GetComponent<Image>();
        }

        RectTransform fillRect =
            weaponDamageSliderFill.rectTransform;

        fillRect.anchorMin =
            Vector2.zero;

        fillRect.anchorMax =
            Vector2.one;

        fillRect.offsetMin =
            Vector2.zero;

        fillRect.offsetMax =
            Vector2.zero;

        weaponDamageSliderFill.sprite =
            GetWhiteSprite();

        weaponDamageSliderFill.color =
            weaponDurabilityHighColor;

        weaponDamageSliderFill.raycastTarget =
            false;

        weaponDamageSlider.fillRect =
            fillRect;

        weaponDamageSlider.handleRect =
            null;

        weaponDamageSlider.targetGraphic =
            weaponDamageSliderBackground;

        weaponDamageSlider.minValue = 0f;
        weaponDamageSlider.maxValue = 1f;

        if (weaponDamageRoot != null)
        {
            weaponDamageRoot.gameObject.SetActive(
                false
            );
        }
    }

    private bool IsCurrentItemWeaponDamageVisible()
    {
        return
            showWeaponDamage &&
            currentItem != null &&
            currentItem.ItemData is WeaponItemData;
    }

    private void EnsureIcon()
    {
        if (iconImage != null)
        {
            return;
        }

        Transform existing =
            transform.Find("Icon");

        if (existing != null)
        {
            iconImage =
                existing.GetComponent<Image>();
        }

        if (iconImage != null)
        {
            iconImage.raycastTarget = false;
            return;
        }

        GameObject iconObject =
            new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(Image)
            );

        iconObject.transform.SetParent(
            transform,
            false
        );

        iconImage =
            iconObject.GetComponent<Image>();

        iconImage.preserveAspect = true;
        iconImage.raycastTarget = false;
    }

    private void EnsureNameText()
    {
        if (itemNameText != null)
        {
            return;
        }

        itemNameText =
            FindOrCreateText(
                "ItemName",
                itemNameFontSize,
                itemNameColor,
                FontStyles.Bold
            );
    }

    private void EnsureDescriptionText()
    {
        if (descriptionText != null)
        {
            return;
        }

        descriptionText =
            FindOrCreateText(
                "Description",
                descriptionFontSize,
                descriptionColor,
                FontStyles.Normal
            );

        descriptionText.enableWordWrapping = true;
        descriptionText.overflowMode =
            TextOverflowModes.Truncate;
    }

    private void EnsureDetailsText()
    {
        if (detailsText != null)
        {
            return;
        }

        detailsText =
            FindOrCreateText(
                "Details",
                detailFontSize,
                detailColor,
                FontStyles.Normal
            );

        detailsText.enableWordWrapping = true;
    }

    private TMP_Text FindOrCreateText(
        string childName,
        float fontSize,
        Color color,
        FontStyles style)
    {
        Transform existing =
            transform.Find(childName);

        TextMeshProUGUI text =
            existing != null
                ? existing.GetComponent<TextMeshProUGUI>()
                : null;

        if (text == null)
        {
            GameObject textObject =
                new GameObject(
                    childName,
                    typeof(RectTransform),
                    typeof(TextMeshProUGUI)
                );

            textObject.transform.SetParent(
                transform,
                false
            );

            text =
                textObject.GetComponent<
                    TextMeshProUGUI
                >();
        }

        ApplyConfiguredFont(text);

        text.fontSize = fontSize;
        text.color = color;
        text.fontStyle = style;
        text.alignment =
            TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        text.margin = Vector4.zero;

        return text;
    }

    /// <summary>
    /// InspectorでJapanese Fontが設定されている場合、
    /// Tooltip内のTMP TextへそのFont Assetを適用します。
    /// </summary>
    private void ApplyConfiguredFont(TMP_Text target)
    {
        if (target == null ||
            japaneseFont == null)
        {
            return;
        }

        target.font = japaneseFont;
    }

    /// <summary>
    /// InspectorでFont Assetを変更した時などに、
    /// 既に生成済みの全Textへ再適用します。
    /// </summary>
    private void ApplyFontToAllTexts()
    {
        ApplyConfiguredFont(weaponDamageText);
        ApplyConfiguredFont(itemNameText);
        ApplyConfiguredFont(descriptionText);
        ApplyConfiguredFont(detailsText);
    }

    private void LayoutVisuals()
    {
        if (panelRect == null)
        {
            return;
        }

        float width =
            panelRect.rect.width > 0f
                ? panelRect.rect.width
                : tooltipWidth;

        float height =
            panelRect.rect.height > 0f
                ? panelRect.rect.height
                : minimumHeight;

        float padding = 16f;
        float iconSize = 68f;
        float top = -padding;

        bool hasWeaponDamage =
            IsCurrentItemWeaponDamageVisible();

        if (weaponDamageRoot != null)
        {
            weaponDamageRoot.gameObject.SetActive(
                hasWeaponDamage
            );

            RectTransform rootRect =
                weaponDamageRoot;

            rootRect.anchorMin =
                new Vector2(0f, 1f);

            rootRect.anchorMax =
                new Vector2(1f, 1f);

            rootRect.pivot =
                new Vector2(0.5f, 1f);

            rootRect.anchoredPosition =
                new Vector2(
                    0f,
                    top
                );

            rootRect.sizeDelta =
                new Vector2(
                    -(padding * 2f),
                    weaponDamageSectionHeight
                );

            if (weaponDamageText != null)
            {
                RectTransform damageTextRect =
                    weaponDamageText.rectTransform;

                damageTextRect.anchorMin =
                    new Vector2(0f, 1f);

                damageTextRect.anchorMax =
                    new Vector2(1f, 1f);

                damageTextRect.pivot =
                    new Vector2(0.5f, 1f);

                damageTextRect.anchoredPosition =
                    Vector2.zero;

                damageTextRect.sizeDelta =
                    new Vector2(
                        0f,
                        24f
                    );
            }

            if (weaponDamageSlider != null)
            {
                RectTransform damageSliderRect =
                    weaponDamageSlider.transform
                        as RectTransform;

                if (damageSliderRect != null)
                {
                    damageSliderRect.anchorMin =
                        new Vector2(0f, 1f);

                    damageSliderRect.anchorMax =
                        new Vector2(1f, 1f);

                    damageSliderRect.pivot =
                        new Vector2(0.5f, 1f);

                    damageSliderRect.anchoredPosition =
                        new Vector2(
                            0f,
                            -30f
                        );

                    damageSliderRect.sizeDelta =
                        new Vector2(
                            0f,
                            weaponDamageSliderHeight
                        );
                }
            }
        }

        float contentTop =
            hasWeaponDamage
                ? top -
                  weaponDamageSectionHeight -
                  10f
                : top;

        if (iconImage != null)
        {
            RectTransform iconRect =
                iconImage.rectTransform;

            iconRect.anchorMin =
                new Vector2(0f, 1f);
            iconRect.anchorMax =
                new Vector2(0f, 1f);
            iconRect.pivot =
                new Vector2(0f, 1f);

            iconRect.anchoredPosition =
                new Vector2(
                    padding,
                    contentTop
                );

            iconRect.sizeDelta =
                new Vector2(
                    iconSize,
                    iconSize
                );
        }

        float textLeft =
            showIcon
                ? padding + iconSize + 12f
                : padding;

        if (itemNameText != null)
        {
            RectTransform rect =
                itemNameText.rectTransform;

            rect.anchorMin =
                new Vector2(0f, 1f);
            rect.anchorMax =
                new Vector2(0f, 1f);
            rect.pivot =
                new Vector2(0f, 1f);

            rect.anchoredPosition =
                new Vector2(
                    textLeft,
                    contentTop
                );

            rect.sizeDelta =
                new Vector2(
                    width - textLeft - padding,
                    34f
                );
        }

        if (descriptionText != null)
        {
            float descriptionTop =
                contentTop - 42f;

            RectTransform rect =
                descriptionText.rectTransform;

            rect.anchorMin =
                new Vector2(0f, 1f);
            rect.anchorMax =
                new Vector2(0f, 1f);
            rect.pivot =
                new Vector2(0f, 1f);

            rect.anchoredPosition =
                new Vector2(
                    textLeft,
                    descriptionTop
                );

            float descriptionHeight =
                Mathf.Max(
                    60f,
                    height - 120f
                );

            rect.sizeDelta =
                new Vector2(
                    width - textLeft - padding,
                    descriptionHeight
                );
        }

        if (detailsText != null)
        {
            RectTransform rect =
                detailsText.rectTransform;

            rect.anchorMin =
                new Vector2(0f, 0f);
            rect.anchorMax =
                new Vector2(1f, 0f);
            rect.pivot =
                new Vector2(0.5f, 0f);

            rect.anchoredPosition =
                new Vector2(
                    0f,
                    padding
                );

            rect.sizeDelta =
                new Vector2(
                    -(padding * 2f),
                    Mathf.Min(
                        100f,
                        height * 0.42f
                    )
                );
        }
    }

    private static Sprite GetWhiteSprite()
    {
        if (whiteSprite != null)
        {
            return whiteSprite;
        }

        whiteSprite =
            Sprite.Create(
                Texture2D.whiteTexture,
                new Rect(0f, 0f, 1f, 1f),
                new Vector2(0.5f, 0.5f)
            );

        whiteSprite.name =
            "InventoryItemTooltipUI_WhiteSprite";

        return whiteSprite;
    }

    private void OnValidate()
    {
        tooltipWidth =
            Mathf.Max(200f, tooltipWidth);

        minimumHeight =
            Mathf.Max(120f, minimumHeight);

        maximumHeight =
            Mathf.Max(
                minimumHeight,
                maximumHeight
            );

        itemNameFontSize =
            Mathf.Max(8f, itemNameFontSize);

        descriptionFontSize =
            Mathf.Max(8f, descriptionFontSize);

        detailFontSize =
            Mathf.Max(8f, detailFontSize);

        weaponDamageSectionHeight =
            Mathf.Max(
                44f,
                weaponDamageSectionHeight
            );

        weaponDamageFontSize =
            Mathf.Max(
                8f,
                weaponDamageFontSize
            );

        weaponDamageSliderHeight =
            Mathf.Max(
                4f,
                weaponDamageSliderHeight
            );

        if (Application.isPlaying)
        {
            EnsureVisuals();
            ApplyFontToAllTexts();

            if (isVisible)
            {
                RefreshContent();
            }
        }
    }
}
