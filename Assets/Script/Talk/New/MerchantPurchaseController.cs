using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ItemBoxInventoryを商人の商品棚として表示し、
/// 選択した商品を所持金で購入します。
///
/// 商品の選択は、Merchant Inventory Grid上のアイテムを左クリックします。
/// InventoryGridUI / InventoryItemUI本体を変更せずに動作します。
///
/// 購入数は従来の +/- Button に加えてSliderでも変更できます。
/// Sliderの最大値は、在庫・店舗購入上限・所持金・インベントリ空き容量から
/// 「現在実際に購入できる最大数」を自動計算します。
/// </summary>
[DisallowMultipleComponent]
public class MerchantPurchaseController : MonoBehaviour
{
    [Header("商品・プレイヤーのGrid UI")]
    [Tooltip("商人のItemBoxInventoryを表示するInventoryGridUIです。Inventory Controllerは空欄にします。")]
    [SerializeField] private InventoryGridUI merchantInventoryGridUI;

    [Tooltip("TownPlayerInventoryのInventoryControllerを表示するInventoryGridUIです。")]
    [SerializeField] private InventoryGridUI playerInventoryGridUI;

    [Header("既存システム")]
    [SerializeField] private TownPlayerInventoryController townPlayerInventory;
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("店舗・所持金表示")]
    [SerializeField] private TMP_Text shopNameText;
    [SerializeField] private TMP_Text moneyText;

    [Header("選択商品表示")]
    [SerializeField] private Image selectedItemIcon;
    [SerializeField] private TMP_Text selectedItemNameText;
    [SerializeField] private TMP_Text unitPriceText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text purchaseAmountText;
    [SerializeField] private TMP_Text totalPriceText;
    [SerializeField] private TMP_Text statusText;

    [Header("購入操作")]
    [SerializeField] private Button decreaseAmountButton;
    [SerializeField] private Button increaseAmountButton;
    [SerializeField] private Button purchaseButton;

    [Header("購入数Slider")]
    [Tooltip("購入数を一気に変更するSliderです。未設定でも従来の+/-Buttonだけで動作します。")]
    [SerializeField] private Slider purchaseAmountSlider;

    [Tooltip("Slider全体をまとめた親Objectです。銃・防具など1個購入の商品では非表示にしたい場合に設定します。未設定ならSlider自身を表示/非表示にします。")]
    [SerializeField] private GameObject purchaseAmountSliderRoot;

    [Tooltip("オンなら、スタック不可または最大1個しか購入できない商品ではSliderを隠します。")]
    [SerializeField] private bool hideSliderForSinglePurchaseItems = true;

    [Tooltip("オンならSliderの最大値を、在庫だけでなく所持金とインベントリ空き容量にも合わせます。")]
    [SerializeField] private bool limitSliderByMoneyAndInventory = true;

    [Header("購入成功サウンド")]
    [Tooltip("購入成功時の効果音を再生するAudioSourceです。未設定なら同じGameObjectから探します。")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("購入が正常に完了した時だけ再生する効果音です。")]
    [SerializeField] private AudioClip purchaseSuccessClip;

    [SerializeField, Range(0f, 1f)]
    private float purchaseSoundVolume = 1f;

    [Header("表示文言")]
    [SerializeField] private string moneyFormat = "所持金：¥{0:N0}";
    [SerializeField] private string unitPriceFormat = "単価：¥{0:N0}";
    [SerializeField] private string stockFormat = "在庫：{0:N0}";
    [SerializeField] private string amountFormat = "購入数：{0:N0}";
    [SerializeField] private string totalPriceFormat = "合計：¥{0:N0}";
    [SerializeField] private string selectItemMessage =
        "商品を左クリックしてください。";
    [SerializeField] private string insufficientMoneyMessage =
        "所持金が足りません。";
    [SerializeField] private string inventoryFullMessage =
        "プレイヤーインベントリに空きがありません。";
    [SerializeField] private string soldOutMessage =
        "この商品は売り切れです。";
    [SerializeField] private string purchaseSuccessFormat =
        "{0} ×{1} を ¥{2:N0} で購入しました。";

    [Header("動作")]
    [SerializeField] private bool selectFirstItemWhenOpened = true;
    [SerializeField] private bool captureInventoryAfterPurchase = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public bool IsOpen => isOpen;
    public MerchantStockInventory CurrentStock => currentStock;
    public InventoryItem SelectedItem => selectedItem;
    public int PurchaseAmount => purchaseAmount;

    private MerchantStockInventory currentStock;
    private InventoryItem selectedItem;
    private int purchaseAmount = 1;
    private bool isOpen;
    private bool isStockSubscribed;
    private bool isMoneySubscribed;

    private void Awake()
    {
        FindReferences();
        SetupButtons();
        SetupSlider();
        RefreshUI();
    }

    private void OnEnable()
    {
        FindReferences();
        SetupButtons();
        SetupSlider();
        SubscribeMoney();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeStock();
        UnsubscribeMoney();
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
        RemoveSliderListener();
    }

    private void Update()
    {
        if (!isOpen ||
            merchantInventoryGridUI == null ||
            !merchantInventoryGridUI.gameObject.activeInHierarchy ||
            !Input.GetMouseButtonUp(0))
        {
            return;
        }

        TrySelectItemUnderPointer();
    }

    /// <summary>
    /// 指定された商人のItemBoxInventoryを商品Gridへ接続します。
    /// 開けた時だけtrueを返します。
    /// </summary>
    public bool OpenShop(MerchantStockInventory stockInventory)
    {
        FindReferences();
        SetupSlider();
        UnsubscribeStock();

        currentStock = stockInventory;
        selectedItem = null;
        purchaseAmount = 1;
        isOpen = false;

        if (currentStock == null)
        {
            SetStatus(
                "MerchantStockInventoryが設定されていません。",
                true
            );
            RefreshUI();
            return false;
        }

        currentStock.PrepareForOpen();

        if (!currentStock.IsReady)
        {
            SetStatus(
                "商人の商品在庫を初期化できませんでした。",
                true
            );
            RefreshUI();
            return false;
        }

        if (currentStock.StockInventory.BoxKind != ItemBoxKind.Shop)
        {
            SetStatus(
                "商人のItemBoxInventoryはBox KindをShopにしてください。",
                true
            );
            RefreshUI();
            return false;
        }

        if (merchantInventoryGridUI == null)
        {
            SetStatus(
                "Merchant Inventory Grid UIが未設定です。",
                true
            );
            RefreshUI();
            return false;
        }

        merchantInventoryGridUI.BindItemBoxInventory(
            currentStock.StockInventory
        );

        SubscribeStock();
        SubscribeMoney();

        isOpen = true;

        if (selectFirstItemWhenOpened)
        {
            SelectFirstAvailableItem();
        }

        if (selectedItem == null)
        {
            SetStatus(selectItemMessage, false);
        }
        else
        {
            SetStatus(string.Empty, false);
        }

        RefreshUI();

        Log(
            $"購入画面を開きました。店舗={currentStock.ShopName} / " +
            $"商品枠={currentStock.StockInventory.Grid.Items.Count}"
        );

        return true;
    }

    public void CloseShop()
    {
        UnsubscribeStock();
        isOpen = false;
        currentStock = null;
        selectedItem = null;
        purchaseAmount = 1;
        SetStatus(string.Empty, false);
        RefreshUI();
    }

    public void RefreshUI()
    {
        FindReferences();

        if (shopNameText != null)
        {
            shopNameText.text = currentStock != null
                ? currentStock.ShopName
                : "商店";
        }

        RefreshMoneyText();
        RefreshSelectedItemUI();

        merchantInventoryGridUI?.RefreshInventoryUI();
        playerInventoryGridUI?.RefreshInventoryUI();
    }

    public void DecreasePurchaseAmount()
    {
        SetPurchaseAmount(purchaseAmount - 1);
    }

    public void IncreasePurchaseAmount()
    {
        SetPurchaseAmount(purchaseAmount + 1);
    }

    public void PurchaseSelectedItem()
    {
        TryPurchaseSelectedItem();
    }

    public void SetPurchaseAmountFromSlider(float sliderValue)
    {
        SetPurchaseAmount(Mathf.RoundToInt(sliderValue));
    }

    public bool TryPurchaseSelectedItem()
    {
        FindReferences();

        if (!TryGetPurchaseContext(
                out ItemBoxInventory stockInventory,
                out InventoryController playerInventory,
                out ItemData itemData))
        {
            return false;
        }

        int stockLimitedMax = GetStockLimitedMaximumPurchaseAmount();
        int amount = Mathf.Max(1, purchaseAmount);

        if (stockLimitedMax <= 0 || selectedItem.Amount < amount)
        {
            SetStatus(soldOutMessage, true);
            HandleStockChanged();
            return false;
        }

        amount = Mathf.Min(amount, stockLimitedMax);

        int unitPrice = currentStock.GetUnitBuyPrice(itemData);
        int totalPrice = MultiplyPrice(unitPrice, amount);

        if (gameSessionManager == null)
        {
            SetStatus("GameSessionManagerが見つかりません。", true);
            return false;
        }

        if (!gameSessionManager.CanAfford(totalPrice))
        {
            SetStatus(insufficientMoneyMessage, true);
            RefreshUI();
            return false;
        }

        if (!CanFitItemAmount(
                playerInventory.Grid,
                itemData,
                amount))
        {
            SetStatus(inventoryFullMessage, true);
            RefreshUI();
            return false;
        }

        int playerAmountBefore =
            playerInventory.GetTotalAmount(itemData);

        int removedFromStock = stockInventory.RemoveItemAmount(
            selectedItem,
            amount
        );

        if (removedFromStock != amount)
        {
            if (removedFromStock > 0)
            {
                stockInventory.TryAddItem(
                    itemData,
                    removedFromStock,
                    out _
                );
            }

            SetStatus(
                "在庫の取り出しに失敗しました。もう一度お試しください。",
                true
            );
            HandleStockChanged();
            return false;
        }

        if (!gameSessionManager.TrySpendMoney(totalPrice))
        {
            RestoreStock(stockInventory, itemData, amount);
            SetStatus(insufficientMoneyMessage, true);
            RefreshUI();
            return false;
        }

        bool addedAll = playerInventory.TryAddItem(
            itemData,
            amount,
            out int remainingAmount
        );

        if (!addedAll || remainingAmount > 0)
        {
            int playerAmountAfter =
                playerInventory.GetTotalAmount(itemData);

            int unexpectedlyAdded = Mathf.Max(
                0,
                playerAmountAfter - playerAmountBefore
            );

            if (unexpectedlyAdded > 0)
            {
                playerInventory.RemoveAmountByItemData(
                    itemData,
                    unexpectedlyAdded
                );
            }

            if (totalPrice > 0)
            {
                gameSessionManager.AddMoney(totalPrice);
            }

            RestoreStock(stockInventory, itemData, amount);

            SetStatus(inventoryFullMessage, true);
            HandleStockChanged();
            return false;
        }

        CapturePlayerInventory();
        PlayPurchaseSuccessSound();

        SetStatus(
            string.Format(
                purchaseSuccessFormat,
                itemData.DisplayName,
                amount,
                totalPrice
            ),
            false
        );

        if (selectedItem == null ||
            !stockInventory.ContainsItem(selectedItem) ||
            selectedItem.Amount <= 0)
        {
            SelectFirstAvailableItem();
        }
        else
        {
            int selectableMax = GetSelectableMaximumPurchaseAmount();

            if (selectableMax > 0)
            {
                purchaseAmount = Mathf.Clamp(
                    purchaseAmount,
                    1,
                    selectableMax
                );
            }
            else
            {
                purchaseAmount = 1;
            }
        }

        RefreshUI();

        Log(
            $"購入成功: {itemData.DisplayName} ×{amount} / " +
            $"単価={unitPrice:N0} / 合計={totalPrice:N0}"
        );

        return true;
    }

    private bool TryGetPurchaseContext(
        out ItemBoxInventory stockInventory,
        out InventoryController playerInventory,
        out ItemData itemData)
    {
        stockInventory = currentStock != null
            ? currentStock.StockInventory
            : null;

        playerInventory = townPlayerInventory != null
            ? townPlayerInventory.InventoryController
            : null;

        itemData = selectedItem != null
            ? selectedItem.ItemData
            : null;

        if (!isOpen || currentStock == null)
        {
            SetStatus("購入画面が開かれていません。", true);
            return false;
        }

        if (stockInventory == null || stockInventory.Grid == null)
        {
            SetStatus("商人の商品在庫が見つかりません。", true);
            return false;
        }

        if (playerInventory == null || playerInventory.Grid == null)
        {
            SetStatus("プレイヤーインベントリが見つかりません。", true);
            return false;
        }

        if (selectedItem == null ||
            itemData == null ||
            !stockInventory.ContainsItem(selectedItem))
        {
            SetStatus(selectItemMessage, true);
            return false;
        }

        return true;
    }

    private void TrySelectItemUnderPointer()
    {
        if (EventSystem.current == null ||
            merchantInventoryGridUI == null)
        {
            return;
        }

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

        List<RaycastResult> results =
            new List<RaycastResult>();

        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            InventoryItemUI itemUI =
                result.gameObject.GetComponentInParent<InventoryItemUI>();

            InventoryItem item = itemUI != null
                ? itemUI.Item
                : null;

            if (item == null ||
                !merchantInventoryGridUI.ContainsItem(item))
            {
                continue;
            }

            SelectItem(item);
            return;
        }
    }

    private void SelectItem(InventoryItem item)
    {
        if (currentStock == null ||
            currentStock.StockInventory == null ||
            item == null ||
            item.ItemData == null ||
            !currentStock.StockInventory.ContainsItem(item))
        {
            return;
        }

        selectedItem = item;
        purchaseAmount = 1;
        SetStatus(string.Empty, false);
        RefreshSelectedItemUI();

        Log($"商品を選択しました: {item.ItemData.DisplayName}");
    }

    private void SelectFirstAvailableItem()
    {
        selectedItem = null;
        purchaseAmount = 1;

        if (currentStock == null ||
            currentStock.StockInventory == null ||
            currentStock.StockInventory.Grid == null)
        {
            return;
        }

        foreach (InventoryItem item in
                 currentStock.StockInventory.Grid.Items)
        {
            if (item == null ||
                item.ItemData == null ||
                item.Amount <= 0)
            {
                continue;
            }

            selectedItem = item;
            return;
        }
    }

    private void SetPurchaseAmount(int value)
    {
        int maxAmount = GetSelectableMaximumPurchaseAmount();

        purchaseAmount = maxAmount <= 0
            ? 1
            : Mathf.Clamp(value, 1, maxAmount);

        RefreshSelectedItemUI();
    }

    /// <summary>
    /// 在庫数と店舗側の購入上限だけで決まる最大値です。
    /// </summary>
    private int GetStockLimitedMaximumPurchaseAmount()
    {
        if (selectedItem == null ||
            selectedItem.ItemData == null ||
            selectedItem.Amount <= 0)
        {
            return 0;
        }

        int shopLimit = currentStock != null &&
                        currentStock.ShopData != null
            ? currentStock.ShopData.MaxPurchaseAmount
            : 999;

        return Mathf.Max(
            0,
            Mathf.Min(selectedItem.Amount, shopLimit)
        );
    }

    /// <summary>
    /// Sliderや+/-Buttonで現在選択できる最大購入数です。
    /// 必要に応じて所持金とインベントリ空きも考慮します。
    /// </summary>
    private int GetSelectableMaximumPurchaseAmount()
    {
        int upperLimit = GetStockLimitedMaximumPurchaseAmount();

        if (upperLimit <= 0 ||
            selectedItem == null ||
            selectedItem.ItemData == null)
        {
            return 0;
        }

        if (!limitSliderByMoneyAndInventory)
        {
            return upperLimit;
        }

        ItemData itemData = selectedItem.ItemData;

        if (currentStock != null && gameSessionManager != null)
        {
            int unitPrice = currentStock.GetUnitBuyPrice(itemData);

            if (unitPrice > 0)
            {
                int affordableAmount =
                    Mathf.Max(0, gameSessionManager.CurrentMoney / unitPrice);

                upperLimit = Mathf.Min(upperLimit, affordableAmount);
            }
        }

        InventoryController playerInventory =
            townPlayerInventory != null
                ? townPlayerInventory.InventoryController
                : null;

        if (upperLimit > 0 &&
            playerInventory != null &&
            playerInventory.Grid != null)
        {
            upperLimit = GetMaximumFittableAmount(
                playerInventory.Grid,
                itemData,
                upperLimit
            );
        }

        return Mathf.Max(0, upperLimit);
    }

    /// <summary>
    /// 指定上限までのうち、現在のGridへ実際に収まる最大個数を二分探索します。
    /// </summary>
    private static int GetMaximumFittableAmount(
        InventoryGrid grid,
        ItemData itemData,
        int upperLimit)
    {
        if (grid == null || itemData == null || upperLimit <= 0)
        {
            return 0;
        }

        int low = 0;
        int high = upperLimit;

        while (low < high)
        {
            int mid = low + (high - low + 1) / 2;

            if (CanFitItemAmount(grid, itemData, mid))
            {
                low = mid;
            }
            else
            {
                high = mid - 1;
            }
        }

        return low;
    }

    private void RefreshSelectedItemUI()
    {
        bool hasSelection =
            isOpen &&
            currentStock != null &&
            currentStock.StockInventory != null &&
            selectedItem != null &&
            selectedItem.ItemData != null &&
            currentStock.StockInventory.ContainsItem(selectedItem) &&
            selectedItem.Amount > 0;

        ItemData itemData = hasSelection
            ? selectedItem.ItemData
            : null;

        if (selectedItemIcon != null)
        {
            selectedItemIcon.sprite = itemData != null
                ? itemData.Icon
                : null;
            selectedItemIcon.enabled =
                selectedItemIcon.sprite != null;
            selectedItemIcon.preserveAspect = true;
        }

        if (selectedItemNameText != null)
        {
            selectedItemNameText.text = itemData != null
                ? itemData.DisplayName
                : "商品未選択";
        }

        int unitPrice = itemData != null && currentStock != null
            ? currentStock.GetUnitBuyPrice(itemData)
            : 0;

        int stockLimitedMax = GetStockLimitedMaximumPurchaseAmount();
        int selectableMax = GetSelectableMaximumPurchaseAmount();

        if (selectableMax > 0)
        {
            purchaseAmount = Mathf.Clamp(
                purchaseAmount,
                1,
                selectableMax
            );
        }
        else
        {
            purchaseAmount = 1;
        }

        int totalPrice = MultiplyPrice(
            unitPrice,
            hasSelection ? purchaseAmount : 0
        );

        SetFormattedText(unitPriceText, unitPriceFormat, unitPrice);
        SetFormattedText(
            stockText,
            stockFormat,
            hasSelection ? selectedItem.Amount : 0
        );
        SetFormattedText(
            purchaseAmountText,
            amountFormat,
            hasSelection ? purchaseAmount : 0
        );
        SetFormattedText(totalPriceText, totalPriceFormat, totalPrice);

        RefreshPurchaseSlider(
            hasSelection,
            itemData,
            stockLimitedMax,
            selectableMax
        );

        if (decreaseAmountButton != null)
        {
            decreaseAmountButton.interactable =
                hasSelection &&
                selectableMax > 0 &&
                purchaseAmount > 1;
        }

        if (increaseAmountButton != null)
        {
            increaseAmountButton.interactable =
                hasSelection &&
                selectableMax > 0 &&
                purchaseAmount < selectableMax;
        }

        if (purchaseButton != null)
        {
            purchaseButton.interactable =
                hasSelection &&
                selectableMax > 0 &&
                gameSessionManager != null &&
                gameSessionManager.CanAfford(totalPrice);
        }
    }

    private void RefreshPurchaseSlider(
        bool hasSelection,
        ItemData itemData,
        int stockLimitedMax,
        int selectableMax)
    {
        if (purchaseAmountSlider == null)
        {
            return;
        }

        bool supportsMultiple =
            hasSelection &&
            itemData != null &&
            itemData.CanStack &&
            stockLimitedMax > 1;

        bool shouldShow =
            !hideSliderForSinglePurchaseItems || supportsMultiple;

        GameObject sliderDisplayRoot = purchaseAmountSliderRoot != null
            ? purchaseAmountSliderRoot
            : purchaseAmountSlider.gameObject;

        if (sliderDisplayRoot != null &&
            sliderDisplayRoot.activeSelf != shouldShow)
        {
            sliderDisplayRoot.SetActive(shouldShow);
        }

        purchaseAmountSlider.wholeNumbers = true;
        purchaseAmountSlider.minValue = 1f;
        purchaseAmountSlider.maxValue = Mathf.Max(1, selectableMax);
        purchaseAmountSlider.interactable =
            shouldShow && hasSelection && selectableMax > 1;

        float sliderValue = selectableMax > 0
            ? Mathf.Clamp(purchaseAmount, 1, selectableMax)
            : 1f;

        purchaseAmountSlider.SetValueWithoutNotify(sliderValue);
    }

    private void RefreshMoneyText()
    {
        int money = gameSessionManager != null
            ? gameSessionManager.CurrentMoney
            : 0;

        SetFormattedText(moneyText, moneyFormat, money);
    }

    private void HandleStockChanged()
    {
        if (currentStock == null ||
            currentStock.StockInventory == null)
        {
            selectedItem = null;
            RefreshUI();
            return;
        }

        if (selectedItem == null ||
            !currentStock.StockInventory.ContainsItem(selectedItem) ||
            selectedItem.Amount <= 0)
        {
            SelectFirstAvailableItem();
        }

        RefreshUI();
    }

    private void HandleMoneyChanged(int currentMoney)
    {
        RefreshMoneyText();
        RefreshSelectedItemUI();
    }

    private void SubscribeStock()
    {
        if (isStockSubscribed ||
            currentStock == null ||
            currentStock.StockInventory == null)
        {
            return;
        }

        currentStock.StockInventory.OnInventoryChanged +=
            HandleStockChanged;

        isStockSubscribed = true;
    }

    private void UnsubscribeStock()
    {
        if (!isStockSubscribed)
        {
            return;
        }

        if (currentStock != null &&
            currentStock.StockInventory != null)
        {
            currentStock.StockInventory.OnInventoryChanged -=
                HandleStockChanged;
        }

        isStockSubscribed = false;
    }

    private void SubscribeMoney()
    {
        if (isMoneySubscribed || gameSessionManager == null)
        {
            return;
        }

        gameSessionManager.MoneyChanged += HandleMoneyChanged;
        isMoneySubscribed = true;
    }

    private void UnsubscribeMoney()
    {
        if (!isMoneySubscribed)
        {
            return;
        }

        if (gameSessionManager != null)
        {
            gameSessionManager.MoneyChanged -= HandleMoneyChanged;
        }

        isMoneySubscribed = false;
    }

    private void SetupButtons()
    {
        RemoveButtonListeners();

        decreaseAmountButton?.onClick.AddListener(
            DecreasePurchaseAmount
        );

        increaseAmountButton?.onClick.AddListener(
            IncreasePurchaseAmount
        );

        purchaseButton?.onClick.AddListener(
            PurchaseSelectedItem
        );
    }

    private void RemoveButtonListeners()
    {
        decreaseAmountButton?.onClick.RemoveListener(
            DecreasePurchaseAmount
        );

        increaseAmountButton?.onClick.RemoveListener(
            IncreasePurchaseAmount
        );

        purchaseButton?.onClick.RemoveListener(
            PurchaseSelectedItem
        );
    }

    private void SetupSlider()
    {
        if (purchaseAmountSlider == null)
        {
            return;
        }

        purchaseAmountSlider.wholeNumbers = true;
        purchaseAmountSlider.minValue = 1f;

        purchaseAmountSlider.onValueChanged.RemoveListener(
            SetPurchaseAmountFromSlider
        );

        purchaseAmountSlider.onValueChanged.AddListener(
            SetPurchaseAmountFromSlider
        );
    }

    private void RemoveSliderListener()
    {
        if (purchaseAmountSlider != null)
        {
            purchaseAmountSlider.onValueChanged.RemoveListener(
                SetPurchaseAmountFromSlider
            );
        }
    }

    private void CapturePlayerInventory()
    {
        if (!captureInventoryAfterPurchase ||
            townPlayerInventory == null)
        {
            return;
        }

        PlayerInventorySessionBridge bridge =
            townPlayerInventory.SessionBridge;

        if (bridge != null)
        {
            bridge.CaptureToSession();
            return;
        }

        if (gameSessionManager != null &&
            townPlayerInventory.InventoryController != null)
        {
            gameSessionManager.CapturePlayerInventory(
                townPlayerInventory.InventoryController,
                townPlayerInventory.EquipmentController
            );
        }
    }

    private void FindReferences()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (townPlayerInventory == null)
        {
            townPlayerInventory =
                FindAnyObjectByType<TownPlayerInventoryController>(
                    FindObjectsInactive.Include
                );
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void PlayPurchaseSuccessSound()
    {
        if (purchaseSuccessClip == null)
        {
            return;
        }

        if (audioSource == null)
        {
            LogWarning(
                "Purchase Success Clipは設定されていますが、" +
                "AudioSourceが見つからないため再生できません。"
            );
            return;
        }

        audioSource.PlayOneShot(
            purchaseSuccessClip,
            Mathf.Clamp01(purchaseSoundVolume)
        );
    }

    private void RestoreStock(
        ItemBoxInventory stockInventory,
        ItemData itemData,
        int amount)
    {
        if (stockInventory == null ||
            itemData == null ||
            amount <= 0)
        {
            return;
        }

        bool restoredAll = stockInventory.TryAddItem(
            itemData,
            amount,
            out int remainingAmount
        );

        if (!restoredAll || remainingAmount > 0)
        {
            LogWarning(
                $"購入失敗後に{itemData.DisplayName}を" +
                $"{remainingAmount}個在庫へ戻せませんでした。"
            );
        }
    }

    private static bool CanFitItemAmount(
        InventoryGrid grid,
        ItemData itemData,
        int amount)
    {
        if (grid == null ||
            itemData == null ||
            amount <= 0)
        {
            return false;
        }

        int remainingAmount = amount;
        int maxStack = Mathf.Max(1, itemData.MaxStack);

        if (itemData.CanStack)
        {
            foreach (InventoryItem existingItem in grid.Items)
            {
                if (existingItem == null ||
                    existingItem.ItemData != itemData)
                {
                    continue;
                }

                int stackSpace = Mathf.Max(
                    0,
                    maxStack - existingItem.Amount
                );

                remainingAmount -= Mathf.Min(
                    remainingAmount,
                    stackSpace
                );

                if (remainingAmount <= 0)
                {
                    return true;
                }
            }
        }

        bool[,] occupied = BuildOccupiedMap(grid);

        while (remainingAmount > 0)
        {
            if (!TryReserveItemSpace(
                    occupied,
                    grid.Width,
                    grid.Height,
                    itemData))
            {
                return false;
            }

            remainingAmount -= Mathf.Min(
                remainingAmount,
                maxStack
            );
        }

        return true;
    }

    private static bool[,] BuildOccupiedMap(InventoryGrid grid)
    {
        bool[,] occupied = new bool[grid.Width, grid.Height];

        foreach (InventoryItem item in grid.Items)
        {
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            Vector2Int size = item.ItemData.GetSize(
                item.IsRotated
            );

            for (int y = item.GridY;
                 y < item.GridY + size.y;
                 y++)
            {
                for (int x = item.GridX;
                     x < item.GridX + size.x;
                     x++)
                {
                    if (x >= 0 && x < grid.Width &&
                        y >= 0 && y < grid.Height)
                    {
                        occupied[x, y] = true;
                    }
                }
            }
        }

        return occupied;
    }

    private static bool TryReserveItemSpace(
        bool[,] occupied,
        int gridWidth,
        int gridHeight,
        ItemData itemData)
    {
        if (TryReserveWithRotation(
                occupied,
                gridWidth,
                gridHeight,
                itemData,
                false))
        {
            return true;
        }

        return itemData.CanRotate &&
               TryReserveWithRotation(
                   occupied,
                   gridWidth,
                   gridHeight,
                   itemData,
                   true
               );
    }

    private static bool TryReserveWithRotation(
        bool[,] occupied,
        int gridWidth,
        int gridHeight,
        ItemData itemData,
        bool isRotated)
    {
        Vector2Int size = itemData.GetSize(isRotated);

        for (int y = 0; y < gridHeight; y++)
        {
            for (int x = 0; x < gridWidth; x++)
            {
                if (!CanReserve(
                        occupied,
                        gridWidth,
                        gridHeight,
                        x,
                        y,
                        size))
                {
                    continue;
                }

                for (int reserveY = y;
                     reserveY < y + size.y;
                     reserveY++)
                {
                    for (int reserveX = x;
                         reserveX < x + size.x;
                         reserveX++)
                    {
                        occupied[reserveX, reserveY] = true;
                    }
                }

                return true;
            }
        }

        return false;
    }

    private static bool CanReserve(
        bool[,] occupied,
        int gridWidth,
        int gridHeight,
        int startX,
        int startY,
        Vector2Int size)
    {
        if (startX < 0 ||
            startY < 0 ||
            startX + size.x > gridWidth ||
            startY + size.y > gridHeight)
        {
            return false;
        }

        for (int y = startY; y < startY + size.y; y++)
        {
            for (int x = startX; x < startX + size.x; x++)
            {
                if (occupied[x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int MultiplyPrice(int unitPrice, int amount)
    {
        long total =
            (long)Mathf.Max(0, unitPrice) *
            Mathf.Max(0, amount);

        return total > int.MaxValue
            ? int.MaxValue
            : Mathf.Max(0, (int)total);
    }

    private static void SetFormattedText(
        TMP_Text target,
        string format,
        int value)
    {
        if (target == null)
        {
            return;
        }

        string safeFormat = string.IsNullOrWhiteSpace(format)
            ? "{0}"
            : format;

        target.text = string.Format(
            safeFormat,
            Mathf.Max(0, value)
        );
    }

    private void SetStatus(string message, bool warning)
    {
        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (warning)
        {
            LogWarning(message);
        }
        else
        {
            Log(message);
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[MerchantPurchaseController] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[MerchantPurchaseController] {message}",
            this
        );
    }

    private void OnValidate()
    {
        purchaseSoundVolume = Mathf.Clamp01(purchaseSoundVolume);

        if (purchaseAmountSlider != null)
        {
            purchaseAmountSlider.wholeNumbers = true;
            purchaseAmountSlider.minValue = 1f;
        }
    }
}
