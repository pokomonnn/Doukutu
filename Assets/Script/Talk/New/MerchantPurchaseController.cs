using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// ItemBoxInventoryを商人の商品棚として表示し、
/// 選択した複数の商品をまとめて購入します。
///
/// 【複数選択】
/// ・Merchant InventoryのItemを左クリックすると購入選択
/// ・選択したItemはInventoryItemUI側で色変更
/// ・別Itemをクリックしても前の選択を保持
/// ・選択済みItemをもう一度クリックすると選択解除
///
/// 【数量Slider】
/// ・弾薬などCanStack=trueで複数購入可能なItemを選択すると、
///   クリック位置の近くへSliderを表示
/// ・SliderはそのItemの購入数だけを変更
/// ・Slider最大値は在庫数と店舗MaxPurchaseAmountを基準にする
///
/// 【一括購入】
/// ・Total Price Textは選択中すべての合計金額
/// ・購入ボタンで選択中すべてを一括購入
/// ・購入前に所持金とPlayer Inventory全体の空き容量をまとめて検証
/// ・空き不足なら「スペースがない」
/// ・所持金不足なら「所持金が足りない」
///
/// 武器修理画面の開閉はPawnShopUIController側で独立して管理します。
/// </summary>
[DisallowMultipleComponent]
public class MerchantPurchaseController : MonoBehaviour
{
    public static MerchantPurchaseController ActiveInstance
    {
        get;
        private set;
    }

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

    [Header("最後に操作した商品表示")]
    [SerializeField] private Image selectedItemIcon;
    [SerializeField] private TMP_Text selectedItemNameText;
    [SerializeField] private TMP_Text unitPriceText;
    [SerializeField] private TMP_Text stockText;
    [SerializeField] private TMP_Text purchaseAmountText;

    [Tooltip("選択中すべての商品を合計した購入予定金額を表示します。")]
    [SerializeField] private TMP_Text totalPriceText;

    [SerializeField] private TMP_Text statusText;

    [Header("購入エラーStatus演出")]
    [Tooltip("所持金不足メッセージを下から上へ移動しながらフェードアウトさせます。")]
    [SerializeField] private bool animateInsufficientMoneyStatus = true;

    [Tooltip("所持金不足の演出時間です。")]
    [SerializeField, Min(0.1f)]
    private float insufficientMoneyStatusDuration = 1.2f;

    [Tooltip("所持金不足の開始位置を元のStatusText位置から何px下げるか。")]
    [SerializeField]
    private float insufficientMoneyStatusStartYOffset = 36f;

    [Tooltip("所持金不足の終了時に元の位置から何px上へ移動するか。")]
    [SerializeField]
    private float insufficientMoneyStatusEndYOffset = 28f;

    [Tooltip("所持金不足の開始時透明度です。1で完全表示。")]
    [SerializeField, Range(0f, 1f)]
    private float insufficientMoneyStatusStartAlpha = 1f;

    [Tooltip("所持金不足の終了時透明度です。通常は0。")]
    [SerializeField, Range(0f, 1f)]
    private float insufficientMoneyStatusEndAlpha = 0f;

    [Space(8f)]
    [Tooltip("Inventory空き不足メッセージを下から上へ移動しながらフェードアウトさせます。")]
    [SerializeField] private bool animateInventoryFullStatus = true;

    [Tooltip("Inventory空き不足の演出時間です。")]
    [SerializeField, Min(0.1f)]
    private float inventoryFullStatusDuration = 1.2f;

    [Tooltip("Inventory空き不足の開始位置を元のStatusText位置から何px下げるか。")]
    [SerializeField]
    private float inventoryFullStatusStartYOffset = 36f;

    [Tooltip("Inventory空き不足の終了時に元の位置から何px上へ移動するか。")]
    [SerializeField]
    private float inventoryFullStatusEndYOffset = 28f;

    [Tooltip("Inventory空き不足の開始時透明度です。1で完全表示。")]
    [SerializeField, Range(0f, 1f)]
    private float inventoryFullStatusStartAlpha = 1f;

    [Tooltip("Inventory空き不足の終了時透明度です。通常は0。")]
    [SerializeField, Range(0f, 1f)]
    private float inventoryFullStatusEndAlpha = 0f;

    [Header("購入操作")]
    [SerializeField] private Button decreaseAmountButton;
    [SerializeField] private Button increaseAmountButton;
    [SerializeField] private Button purchaseButton;

    [Header("購入数Slider")]
    [Tooltip("最後に選択したスタック可能Itemの購入数を変更するSliderです。")]
    [SerializeField] private Slider purchaseAmountSlider;

    [Tooltip("Slider全体の親Object。カーソル付近へ移動させる対象です。未設定ならSlider自身を使います。")]
    [SerializeField] private GameObject purchaseAmountSliderRoot;

    [Tooltip("オンなら、スタック不可または1個しか買えないItemではSliderを隠します。")]
    [SerializeField] private bool hideSliderForSinglePurchaseItems = true;

    [Tooltip(
        "オンならSlider最大値は在庫数と店舗購入上限のみを使用します。" +
        "所持金・Inventory空きは購入ボタンを押した時にまとめて判定するので、通常はON推奨です。"
    )]
    [SerializeField] private bool selectionSliderUsesStockOnly = true;

    [Header("Sliderの表示位置")]
    [Tooltip(
        "Itemをクリックした時、そのクリック位置の近くへSliderを表示します。" +
        "Slider操作中に動いてしまわないよう、クリック後はその場所に固定します。"
    )]
    [SerializeField] private bool placeSliderNearClickedItem = true;

    [SerializeField]
    private Vector2 sliderCursorOffset = new Vector2(24f, -16f);

    [Header("購入サウンド")]
    [Tooltip("購入関連の効果音を再生するAudioSourceです。未設定なら同じGameObjectから探します。")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("一括購入が正常に完了した時だけ再生する効果音です。")]
    [SerializeField] private AudioClip purchaseSuccessClip;

    [SerializeField, Range(0f, 1f)]
    private float purchaseSoundVolume = 1f;

    [Tooltip("Itemを選択した時・選択解除した時に共通で鳴らす効果音です。")]
    [SerializeField] private AudioClip itemSelectionToggleClip;

    [SerializeField, Range(0f, 1f)]
    private float itemSelectionToggleVolume = 1f;

    [Tooltip("購入ボタンを押したが購入できなかった時に鳴らす効果音です。")]
    [SerializeField] private AudioClip purchaseFailedClip;

    [SerializeField, Range(0f, 1f)]
    private float purchaseFailedVolume = 1f;

    [Tooltip("購入数Sliderの値が1段階変わった時に鳴らす効果音です。")]
    [SerializeField] private AudioClip sliderMoveClip;

    [SerializeField, Range(0f, 1f)]
    private float sliderMoveVolume = 1f;

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

    [Header("一括購入の表示文言")]
    [SerializeField] private string cartInsufficientMoneyMessage =
        "所持金が足りない";

    [SerializeField] private string cartInventoryFullMessage =
        "スペースがない";

    [SerializeField] private string multiPurchaseSuccessFormat =
        "{0}種類 / 合計 ¥{1:N0} を購入しました。";

    [Header("動作")]
    [Tooltip(
        "新しい複数選択方式では、Shopを開いた直後は未選択がおすすめです。" +
        "ONなら必ず未選択で開始します。"
    )]
    [SerializeField] private bool startWithNoSelection = true;

    [Tooltip("旧仕様互換。Start With No SelectionがOFFの時だけ最初の商品を自動選択します。")]
    [SerializeField] private bool selectFirstItemWhenOpened = true;

    [SerializeField] private bool captureInventoryAfterPurchase = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public bool IsOpen => isOpen;
    public MerchantStockInventory CurrentStock => currentStock;

    /// <summary>
    /// 旧コード互換。
    /// 複数選択中のうち、最後にクリックして数量操作対象になっているItemです。
    /// </summary>
    public InventoryItem SelectedItem => selectedItem;

    /// <summary>
    /// 旧コード互換。
    /// 最後に操作しているItemの購入数です。
    /// </summary>
    public int PurchaseAmount => purchaseAmount;

    public int SelectedPurchaseEntryCount => selectedPurchases.Count;

    private MerchantStockInventory currentStock;

    // 最後にクリックして数量Sliderの操作対象になっているItem。
    private InventoryItem selectedItem;
    private int purchaseAmount = 1;

    // 複数選択の本体。InventoryItemごとに購入予定数を保持します。
    private readonly Dictionary<InventoryItem, int> selectedPurchases =
        new Dictionary<InventoryItem, int>();

    private bool isOpen;
    private bool isStockSubscribed;
    private bool isMoneySubscribed;
    private bool isProcessingPurchase;

    // Slider音の連打防止。
    // 同じ整数値でonValueChangedが複数回飛んでも1回だけ鳴らします。
    private int lastSliderSoundValue = int.MinValue;

    private Coroutine statusAnimationCoroutine;
    private RectTransform statusRectTransform;
    private Vector2 statusBaseAnchoredPosition;
    private Color statusBaseColor = Color.white;
    private bool hasCapturedStatusBaseState;

    private Vector2 lastSliderAnchorScreenPosition;
    private bool hasSliderAnchor;

    private readonly struct PurchaseSnapshot
    {
        public readonly InventoryItem StockItem;
        public readonly ItemData ItemData;
        public readonly int Amount;
        public readonly int UnitPrice;
        public readonly int TotalPrice;

        public PurchaseSnapshot(
            InventoryItem stockItem,
            ItemData itemData,
            int amount,
            int unitPrice)
        {
            StockItem = stockItem;
            ItemData = itemData;
            Amount = Mathf.Max(0, amount);
            UnitPrice = Mathf.Max(0, unitPrice);
            TotalPrice = MultiplyPrice(UnitPrice, Amount);
        }
    }

    private readonly struct RemovedStockEntry
    {
        public readonly ItemData ItemData;
        public readonly int Amount;

        public RemovedStockEntry(ItemData itemData, int amount)
        {
            ItemData = itemData;
            Amount = Mathf.Max(0, amount);
        }
    }

    private readonly struct PurchaseGroup
    {
        public readonly ItemData ItemData;
        public readonly int Amount;

        public PurchaseGroup(ItemData itemData, int amount)
        {
            ItemData = itemData;
            Amount = Mathf.Max(0, amount);
        }
    }

    private void Awake()
    {
        FindReferences();
        CaptureStatusBaseState();
        SetupButtons();
        SetupSlider();
        RefreshUI();
    }

    private void OnEnable()
    {
        FindReferences();
        CaptureStatusBaseState();
        SetupButtons();
        SetupSlider();
        SubscribeMoney();
        RefreshUI();
    }

    private void OnDisable()
    {
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        UnsubscribeStock();
        UnsubscribeMoney();
        HidePurchaseSlider();
        StopStatusAnimation(true);
    }

    private void OnDestroy()
    {
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

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

        TryToggleItemUnderPointer();
    }

    /// <summary>
    /// 指定された商人の商品在庫を購入画面へ接続します。
    /// </summary>
    public bool OpenShop(MerchantStockInventory stockInventory)
    {
        FindReferences();
        SetupSlider();
        UnsubscribeStock();

        currentStock = stockInventory;

        ClearAllSelections(false);

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

        if (currentStock.StockInventory == null ||
            currentStock.StockInventory.BoxKind != ItemBoxKind.Shop)
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
        ActiveInstance = this;

        if (!startWithNoSelection &&
            selectFirstItemWhenOpened)
        {
            SelectFirstAvailableItemForLegacyMode();
        }

        SetStatus(
            selectedPurchases.Count > 0
                ? string.Empty
                : selectItemMessage,
            false
        );

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
        ClearAllSelections(false);

        currentStock = null;

        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        SetStatus(string.Empty, false);
        HidePurchaseSlider();
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

    /// <summary>
    /// InventoryItemUIから、購入選択色を出すために参照します。
    /// </summary>
    public bool IsItemSelectedForPurchase(InventoryItem item)
    {
        return isOpen &&
               item != null &&
               selectedPurchases.ContainsKey(item);
    }

    public int GetSelectedPurchaseAmount(InventoryItem item)
    {
        if (item == null)
        {
            return 0;
        }

        return selectedPurchases.TryGetValue(
            item,
            out int amount)
            ? Mathf.Max(0, amount)
            : 0;
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
        int roundedValue =
            Mathf.RoundToInt(sliderValue);

        bool shouldPlaySliderSound =
            selectedItem != null &&
            selectedPurchases.ContainsKey(selectedItem) &&
            roundedValue != lastSliderSoundValue;

        SetPurchaseAmount(
            roundedValue
        );

        if (shouldPlaySliderSound)
        {
            lastSliderSoundValue =
                purchaseAmount;

            PlaySliderMoveSound();
        }
    }

    /// <summary>
    /// 現在選択中の商品をすべて一括購入します。
    /// </summary>
    public bool TryPurchaseSelectedItem()
    {
        FindReferences();

        if (!TryGetCartPurchaseContext(
                out ItemBoxInventory stockInventory,
                out InventoryController playerInventory))
        {
            PlayPurchaseFailedSound();
            return false;
        }

        PruneAndClampSelections();

        if (selectedPurchases.Count == 0)
        {
            SetStatus(selectItemMessage, true);
            PlayPurchaseFailedSound();
            RefreshUI();
            return false;
        }

        List<PurchaseSnapshot> snapshots =
            BuildPurchaseSnapshots();

        if (snapshots.Count == 0)
        {
            SetStatus(selectItemMessage, true);
            PlayPurchaseFailedSound();
            RefreshUI();
            return false;
        }

        int totalPrice = 0;

        foreach (PurchaseSnapshot snapshot in snapshots)
        {
            if (snapshot.StockItem == null ||
                snapshot.ItemData == null ||
                !stockInventory.ContainsItem(snapshot.StockItem) ||
                snapshot.Amount <= 0 ||
                snapshot.StockItem.Amount < snapshot.Amount)
            {
                SetStatus(soldOutMessage, true);
                PlayPurchaseFailedSound();
                HandleStockChanged();
                return false;
            }

            int maxForEntry =
                GetStockLimitedMaximumPurchaseAmount(
                    snapshot.StockItem
                );

            if (maxForEntry <= 0 ||
                snapshot.Amount > maxForEntry)
            {
                SetStatus(soldOutMessage, true);
                PlayPurchaseFailedSound();
                HandleStockChanged();
                return false;
            }

            totalPrice =
                SafeAddPrice(
                    totalPrice,
                    snapshot.TotalPrice
                );
        }

        if (gameSessionManager == null)
        {
            SetStatus(
                "GameSessionManagerが見つかりません。",
                true
            );
            PlayPurchaseFailedSound();
            return false;
        }

        // 購入ボタン自体は押せるままにして、
        // 押した時に希望どおり不足理由をText表示します。
        if (!gameSessionManager.CanAfford(totalPrice))
        {
            ShowInsufficientMoneyStatus();
            PlayPurchaseFailedSound();
            RefreshSelectedItemUI();
            return false;
        }

        Dictionary<ItemData, int> groupedAmounts =
            BuildGroupedPurchaseAmounts(snapshots);

        if (!CanFitPurchaseSet(
                playerInventory.Grid,
                groupedAmounts))
        {
            ShowInventoryFullStatus();
            PlayPurchaseFailedSound();
            RefreshSelectedItemUI();
            return false;
        }

        Dictionary<ItemData, int> playerAmountBefore =
            CapturePlayerAmountsBefore(
                playerInventory,
                groupedAmounts
            );

        List<RemovedStockEntry> removedStock =
            new List<RemovedStockEntry>();

        isProcessingPurchase = true;

        // 1) 商人在庫からすべて取り出す
        foreach (PurchaseSnapshot snapshot in snapshots)
        {
            int removed =
                stockInventory.RemoveItemAmount(
                    snapshot.StockItem,
                    snapshot.Amount
                );

            if (removed > 0)
            {
                removedStock.Add(
                    new RemovedStockEntry(
                        snapshot.ItemData,
                        removed
                    )
                );
            }

            if (removed != snapshot.Amount)
            {
                RestoreRemovedStock(
                    stockInventory,
                    removedStock
                );

                isProcessingPurchase = false;

                SetStatus(
                    "在庫の取り出しに失敗しました。もう一度お試しください。",
                    true
                );

                PlayPurchaseFailedSound();
                ClearAllSelections(false);
                RefreshUI();
                return false;
            }
        }

        // 2) 合計金額を支払う
        if (!gameSessionManager.TrySpendMoney(totalPrice))
        {
            RestoreRemovedStock(
                stockInventory,
                removedStock
            );

            isProcessingPurchase = false;

            ShowInsufficientMoneyStatus();
            PlayPurchaseFailedSound();

            RefreshUI();
            return false;
        }

        // 3) Player Inventoryへまとめて追加
        bool addSuccess = true;

        foreach (KeyValuePair<ItemData, int> pair
                 in groupedAmounts)
        {
            if (pair.Key == null ||
                pair.Value <= 0)
            {
                continue;
            }

            bool addedAll =
                playerInventory.TryAddItem(
                    pair.Key,
                    pair.Value,
                    out int remainingAmount
                );

            if (!addedAll || remainingAmount > 0)
            {
                addSuccess = false;
                break;
            }
        }

        if (!addSuccess)
        {
            // 念のため、途中まで追加されたItemを取り消す
            RollbackPlayerInventory(
                playerInventory,
                playerAmountBefore
            );

            if (totalPrice > 0)
            {
                gameSessionManager.AddMoney(totalPrice);
            }

            RestoreRemovedStock(
                stockInventory,
                removedStock
            );

            isProcessingPurchase = false;

            ShowInventoryFullStatus();
            PlayPurchaseFailedSound();

            ClearAllSelections(false);
            RefreshUI();
            return false;
        }

        isProcessingPurchase = false;

        int purchasedKindCount =
            groupedAmounts.Count;

        ClearAllSelections(false);

        CapturePlayerInventory();
        PlayPurchaseSuccessSound();

        SetStatus(
            string.Format(
                string.IsNullOrWhiteSpace(
                    multiPurchaseSuccessFormat)
                    ? "{0}種類 / 合計 ¥{1:N0} を購入しました。"
                    : multiPurchaseSuccessFormat,
                purchasedKindCount,
                totalPrice
            ),
            false
        );

        RefreshUI();

        Log(
            $"一括購入成功: 種類={purchasedKindCount} / " +
            $"選択枠={snapshots.Count} / 合計={totalPrice:N0}"
        );

        return true;
    }

    private bool TryGetCartPurchaseContext(
        out ItemBoxInventory stockInventory,
        out InventoryController playerInventory)
    {
        stockInventory = currentStock != null
            ? currentStock.StockInventory
            : null;

        playerInventory = townPlayerInventory != null
            ? townPlayerInventory.InventoryController
            : null;

        if (!isOpen || currentStock == null)
        {
            SetStatus(
                "購入画面が開かれていません。",
                true
            );
            return false;
        }

        if (stockInventory == null ||
            stockInventory.Grid == null)
        {
            SetStatus(
                "商人の商品在庫が見つかりません。",
                true
            );
            return false;
        }

        if (playerInventory == null ||
            playerInventory.Grid == null)
        {
            SetStatus(
                "プレイヤーインベントリが見つかりません。",
                true
            );
            return false;
        }

        return true;
    }

    /// <summary>
    /// Merchant InventoryのItemをクリックすると選択／解除します。
    /// </summary>
    private void TryToggleItemUnderPointer()
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

        EventSystem.current.RaycastAll(
            pointerData,
            results
        );

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            InventoryItemUI itemUI =
                result.gameObject
                    .GetComponentInParent<InventoryItemUI>();

            InventoryItem item =
                itemUI != null
                    ? itemUI.Item
                    : null;

            if (item == null ||
                !merchantInventoryGridUI.ContainsItem(item))
            {
                continue;
            }

            ToggleItemSelection(
                item,
                pointerData.position
            );
            return;
        }
    }

    private void ToggleItemSelection(
        InventoryItem item,
        Vector2 clickScreenPosition)
    {
        if (currentStock == null ||
            currentStock.StockInventory == null ||
            item == null ||
            item.ItemData == null ||
            !currentStock.StockInventory.ContainsItem(item))
        {
            return;
        }

        // 選択済みをもう一度クリック → 選択解除
        if (selectedPurchases.Remove(item))
        {
            PlayItemSelectionToggleSound();

            Log(
                $"商品選択解除: {item.ItemData.DisplayName}"
            );

            if (selectedItem == item)
            {
                selectedItem = null;
                purchaseAmount = 1;
                lastSliderSoundValue = int.MinValue;
                hasSliderAnchor = false;
                SelectAnotherFocusedItem();
            }

            SetStatus(
                selectedPurchases.Count > 0
                    ? string.Empty
                    : selectItemMessage,
                false
            );

            RefreshUI();
            return;
        }

        int maxAmount =
            GetStockLimitedMaximumPurchaseAmount(item);

        if (maxAmount <= 0)
        {
            SetStatus(soldOutMessage, true);
            return;
        }

        // 未選択Itemをクリック → 選択へ追加
        // 弾薬など複数購入可能なItemは、
        // Sliderの現在のMAX値から購入数を開始します。
        int initialPurchaseAmount =
            Mathf.Max(
                1,
                GetSliderMaximumPurchaseAmount(item)
            );

        selectedPurchases[item] =
            initialPurchaseAmount;

        PlayItemSelectionToggleSound();

        selectedItem = item;
        purchaseAmount =
            initialPurchaseAmount;

        lastSliderSoundValue =
            initialPurchaseAmount;

        lastSliderAnchorScreenPosition =
            clickScreenPosition;

        hasSliderAnchor = true;

        SetStatus(string.Empty, false);
        RefreshUI();

        Log(
            $"商品選択追加: {item.ItemData.DisplayName} / " +
            $"現在選択枠={selectedPurchases.Count}"
        );
    }

    private void SelectAnotherFocusedItem()
    {
        foreach (KeyValuePair<InventoryItem, int> pair
                 in selectedPurchases)
        {
            if (pair.Key == null)
            {
                continue;
            }

            selectedItem = pair.Key;
            purchaseAmount =
                Mathf.Max(1, pair.Value);

            // 自動フォーカス切替ではクリック位置が無いので、
            // Sliderは勝手に別場所へ出さない。
            hasSliderAnchor = false;
            return;
        }

        selectedItem = null;
        purchaseAmount = 1;
        hasSliderAnchor = false;
    }

    private void SelectFirstAvailableItemForLegacyMode()
    {
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

            int initialPurchaseAmount =
                Mathf.Max(
                    1,
                    GetSliderMaximumPurchaseAmount(item)
                );

            selectedPurchases[item] =
                initialPurchaseAmount;

            selectedItem = item;
            purchaseAmount =
                initialPurchaseAmount;

            lastSliderSoundValue =
                initialPurchaseAmount;

            hasSliderAnchor = false;
            return;
        }
    }

    private void SetPurchaseAmount(int value)
    {
        if (selectedItem == null ||
            !selectedPurchases.ContainsKey(selectedItem))
        {
            purchaseAmount = 1;
            RefreshSelectedItemUI();
            return;
        }

        int maxAmount =
            GetSliderMaximumPurchaseAmount(
                selectedItem
            );

        purchaseAmount =
            maxAmount <= 0
                ? 1
                : Mathf.Clamp(
                    value,
                    1,
                    maxAmount
                );

        selectedPurchases[selectedItem] =
            purchaseAmount;

        RefreshSelectedItemUI();
    }

    /// <summary>
    /// 在庫数と店舗側のMaxPurchaseAmountだけで決まる最大数。
    /// </summary>
    private int GetStockLimitedMaximumPurchaseAmount(
        InventoryItem item)
    {
        if (item == null ||
            item.ItemData == null ||
            item.Amount <= 0)
        {
            return 0;
        }

        int shopLimit =
            currentStock != null &&
            currentStock.ShopData != null
                ? currentStock.ShopData.MaxPurchaseAmount
                : 999;

        return Mathf.Max(
            0,
            Mathf.Min(
                item.Amount,
                shopLimit
            )
        );
    }

    private int GetSliderMaximumPurchaseAmount(
        InventoryItem item)
    {
        int stockLimited =
            GetStockLimitedMaximumPurchaseAmount(item);

        if (stockLimited <= 0 ||
            item == null ||
            item.ItemData == null)
        {
            return 0;
        }

        // 新しい買い物かご方式では、基本的にここで
        // 所持金・空き容量を制限せず、購入ボタン時に理由を表示します。
        if (selectionSliderUsesStockOnly)
        {
            return stockLimited;
        }

        int upperLimit = stockLimited;

        if (currentStock != null &&
            gameSessionManager != null)
        {
            int unitPrice =
                currentStock.GetUnitBuyPrice(
                    item.ItemData
                );

            if (unitPrice > 0)
            {
                int affordable =
                    Mathf.Max(
                        0,
                        gameSessionManager.CurrentMoney /
                        unitPrice
                    );

                upperLimit =
                    Mathf.Min(
                        upperLimit,
                        affordable
                    );
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
            upperLimit =
                GetMaximumFittableAmount(
                    playerInventory.Grid,
                    item.ItemData,
                    upperLimit
                );
        }

        return Mathf.Max(0, upperLimit);
    }

    private void RefreshSelectedItemUI()
    {
        bool hasFocus =
            isOpen &&
            currentStock != null &&
            currentStock.StockInventory != null &&
            selectedItem != null &&
            selectedItem.ItemData != null &&
            currentStock.StockInventory.ContainsItem(selectedItem) &&
            selectedPurchases.ContainsKey(selectedItem) &&
            selectedItem.Amount > 0;

        ItemData itemData =
            hasFocus
                ? selectedItem.ItemData
                : null;

        if (selectedItemIcon != null)
        {
            selectedItemIcon.sprite =
                itemData != null
                    ? itemData.Icon
                    : null;

            selectedItemIcon.enabled =
                selectedItemIcon.sprite != null;

            selectedItemIcon.preserveAspect = true;
        }

        if (selectedItemNameText != null)
        {
            selectedItemNameText.text =
                itemData != null
                    ? itemData.DisplayName
                    : "商品未選択";
        }

        int unitPrice =
            itemData != null &&
            currentStock != null
                ? currentStock.GetUnitBuyPrice(itemData)
                : 0;

        int stockLimitedMax =
            hasFocus
                ? GetStockLimitedMaximumPurchaseAmount(
                    selectedItem
                )
                : 0;

        int sliderMax =
            hasFocus
                ? GetSliderMaximumPurchaseAmount(
                    selectedItem
                )
                : 0;

        if (hasFocus)
        {
            int savedAmount =
                selectedPurchases.TryGetValue(
                    selectedItem,
                    out int currentAmount)
                    ? currentAmount
                    : 1;

            purchaseAmount =
                sliderMax > 0
                    ? Mathf.Clamp(
                        savedAmount,
                        1,
                        sliderMax
                    )
                    : 1;

            selectedPurchases[selectedItem] =
                purchaseAmount;
        }
        else
        {
            purchaseAmount = 1;
        }

        int cartTotal =
            CalculateCartTotal();

        SetFormattedText(
            unitPriceText,
            unitPriceFormat,
            unitPrice
        );

        SetFormattedText(
            stockText,
            stockFormat,
            hasFocus
                ? selectedItem.Amount
                : 0
        );

        SetFormattedText(
            purchaseAmountText,
            amountFormat,
            hasFocus
                ? purchaseAmount
                : 0
        );

        SetFormattedText(
            totalPriceText,
            totalPriceFormat,
            cartTotal
        );

        RefreshPurchaseSlider(
            hasFocus,
            itemData,
            stockLimitedMax,
            sliderMax
        );

        if (decreaseAmountButton != null)
        {
            decreaseAmountButton.interactable =
                hasFocus &&
                sliderMax > 0 &&
                purchaseAmount > 1;
        }

        if (increaseAmountButton != null)
        {
            increaseAmountButton.interactable =
                hasFocus &&
                sliderMax > 0 &&
                purchaseAmount < sliderMax;
        }

        if (purchaseButton != null)
        {
            // あえて所持金・空き容量では無効化しません。
            // 押した時に不足理由をStatus Textへ表示します。
            purchaseButton.interactable =
                isOpen &&
                currentStock != null &&
                selectedPurchases.Count > 0;
        }
    }

    private void RefreshPurchaseSlider(
        bool hasFocus,
        ItemData itemData,
        int stockLimitedMax,
        int sliderMax)
    {
        if (purchaseAmountSlider == null)
        {
            return;
        }

        bool supportsMultiple =
            hasFocus &&
            itemData != null &&
            itemData.CanStack &&
            stockLimitedMax > 1;

        bool shouldShow =
            (!hideSliderForSinglePurchaseItems ||
             supportsMultiple) &&
            (!placeSliderNearClickedItem ||
             hasSliderAnchor);

        GameObject sliderDisplayRoot =
            purchaseAmountSliderRoot != null
                ? purchaseAmountSliderRoot
                : purchaseAmountSlider.gameObject;

        if (sliderDisplayRoot != null &&
            sliderDisplayRoot.activeSelf != shouldShow)
        {
            sliderDisplayRoot.SetActive(shouldShow);
        }

        purchaseAmountSlider.wholeNumbers = true;
        purchaseAmountSlider.minValue = 1f;
        purchaseAmountSlider.maxValue =
            Mathf.Max(1, sliderMax);

        purchaseAmountSlider.interactable =
            shouldShow &&
            hasFocus &&
            sliderMax > 1;

        float sliderValue =
            sliderMax > 0
                ? Mathf.Clamp(
                    purchaseAmount,
                    1,
                    sliderMax
                )
                : 1f;

        purchaseAmountSlider.SetValueWithoutNotify(
            sliderValue
        );

        if (shouldShow &&
            placeSliderNearClickedItem &&
            hasSliderAnchor)
        {
            PositionSliderNearScreenPoint(
                lastSliderAnchorScreenPosition
            );
        }
    }

    private void PositionSliderNearScreenPoint(
        Vector2 screenPoint)
    {
        RectTransform sliderRect =
            GetSliderRootRect();

        if (sliderRect == null)
        {
            return;
        }

        Canvas canvas =
            sliderRect.GetComponentInParent<Canvas>()?.rootCanvas;

        if (canvas == null)
        {
            return;
        }

        RectTransform parentRect =
            sliderRect.parent as RectTransform;

        if (parentRect == null)
        {
            parentRect =
                canvas.transform as RectTransform;
        }

        if (parentRect == null)
        {
            return;
        }

        float scale =
            Mathf.Max(
                0.0001f,
                canvas.scaleFactor
            );

        float widthPixels =
            sliderRect.rect.width * scale;

        float heightPixels =
            sliderRect.rect.height * scale;

        float xOffset =
            Mathf.Abs(sliderCursorOffset.x);

        bool placeLeft =
            screenPoint.x +
            xOffset +
            widthPixels >
            Screen.width;

        Vector2 targetScreenPoint =
            screenPoint;

        if (placeLeft)
        {
            sliderRect.pivot =
                new Vector2(1f, 1f);

            targetScreenPoint.x -= xOffset;
        }
        else
        {
            sliderRect.pivot =
                new Vector2(0f, 1f);

            targetScreenPoint.x += xOffset;
        }

        targetScreenPoint.y +=
            sliderCursorOffset.y;

        targetScreenPoint.y =
            Mathf.Clamp(
                targetScreenPoint.y,
                heightPixels + 4f,
                Screen.height - 4f
            );

        Camera uiCamera =
            canvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : canvas.worldCamera;

        if (RectTransformUtility
            .ScreenPointToWorldPointInRectangle(
                parentRect,
                targetScreenPoint,
                uiCamera,
                out Vector3 worldPoint))
        {
            sliderRect.position = worldPoint;
        }
    }

    private RectTransform GetSliderRootRect()
    {
        GameObject root =
            purchaseAmountSliderRoot != null
                ? purchaseAmountSliderRoot
                : purchaseAmountSlider != null
                    ? purchaseAmountSlider.gameObject
                    : null;

        return root != null
            ? root.transform as RectTransform
            : null;
    }

    private void HidePurchaseSlider()
    {
        GameObject root =
            purchaseAmountSliderRoot != null
                ? purchaseAmountSliderRoot
                : purchaseAmountSlider != null
                    ? purchaseAmountSlider.gameObject
                    : null;

        if (root != null &&
            root.activeSelf)
        {
            root.SetActive(false);
        }
    }

    private int CalculateCartTotal()
    {
        if (currentStock == null)
        {
            return 0;
        }

        int total = 0;

        foreach (KeyValuePair<InventoryItem, int> pair
                 in selectedPurchases)
        {
            InventoryItem item = pair.Key;

            if (item == null ||
                item.ItemData == null ||
                pair.Value <= 0)
            {
                continue;
            }

            int unitPrice =
                currentStock.GetUnitBuyPrice(
                    item.ItemData
                );

            total =
                SafeAddPrice(
                    total,
                    MultiplyPrice(
                        unitPrice,
                        pair.Value
                    )
                );
        }

        return total;
    }

    private List<PurchaseSnapshot>
        BuildPurchaseSnapshots()
    {
        List<PurchaseSnapshot> snapshots =
            new List<PurchaseSnapshot>();

        if (currentStock == null)
        {
            return snapshots;
        }

        foreach (KeyValuePair<InventoryItem, int> pair
                 in selectedPurchases)
        {
            InventoryItem item = pair.Key;

            if (item == null ||
                item.ItemData == null ||
                pair.Value <= 0)
            {
                continue;
            }

            int unitPrice =
                currentStock.GetUnitBuyPrice(
                    item.ItemData
                );

            snapshots.Add(
                new PurchaseSnapshot(
                    item,
                    item.ItemData,
                    pair.Value,
                    unitPrice
                )
            );
        }

        return snapshots;
    }

    private static Dictionary<ItemData, int>
        BuildGroupedPurchaseAmounts(
            List<PurchaseSnapshot> snapshots)
    {
        Dictionary<ItemData, int> grouped =
            new Dictionary<ItemData, int>();

        foreach (PurchaseSnapshot snapshot in snapshots)
        {
            if (snapshot.ItemData == null ||
                snapshot.Amount <= 0)
            {
                continue;
            }

            grouped.TryGetValue(
                snapshot.ItemData,
                out int current
            );

            long next =
                (long)current +
                snapshot.Amount;

            grouped[snapshot.ItemData] =
                next > int.MaxValue
                    ? int.MaxValue
                    : (int)next;
        }

        return grouped;
    }

    private static Dictionary<ItemData, int>
        CapturePlayerAmountsBefore(
            InventoryController playerInventory,
            Dictionary<ItemData, int> groupedAmounts)
    {
        Dictionary<ItemData, int> before =
            new Dictionary<ItemData, int>();

        if (playerInventory == null)
        {
            return before;
        }

        foreach (KeyValuePair<ItemData, int> pair
                 in groupedAmounts)
        {
            if (pair.Key == null)
            {
                continue;
            }

            before[pair.Key] =
                playerInventory.GetTotalAmount(
                    pair.Key
                );
        }

        return before;
    }

    private static void RollbackPlayerInventory(
        InventoryController playerInventory,
        Dictionary<ItemData, int> beforeAmounts)
    {
        if (playerInventory == null)
        {
            return;
        }

        foreach (KeyValuePair<ItemData, int> pair
                 in beforeAmounts)
        {
            if (pair.Key == null)
            {
                continue;
            }

            int now =
                playerInventory.GetTotalAmount(
                    pair.Key
                );

            int added =
                Mathf.Max(
                    0,
                    now - pair.Value
                );

            if (added > 0)
            {
                playerInventory.RemoveAmountByItemData(
                    pair.Key,
                    added
                );
            }
        }
    }

    private void PruneAndClampSelections()
    {
        if (currentStock == null ||
            currentStock.StockInventory == null)
        {
            ClearAllSelections(false);
            return;
        }

        List<InventoryItem> removeList =
            new List<InventoryItem>();

        List<InventoryItem> keySnapshot =
            new List<InventoryItem>(
                selectedPurchases.Keys
            );

        foreach (InventoryItem item in keySnapshot)
        {
            if (item == null ||
                item.ItemData == null ||
                !currentStock.StockInventory.ContainsItem(item) ||
                item.Amount <= 0)
            {
                removeList.Add(item);
                continue;
            }

            int max =
                GetStockLimitedMaximumPurchaseAmount(item);

            if (max <= 0)
            {
                removeList.Add(item);
                continue;
            }

            selectedPurchases[item] =
                Mathf.Clamp(
                    selectedPurchases[item],
                    1,
                    max
                );
        }

        foreach (InventoryItem item in removeList)
        {
            selectedPurchases.Remove(item);
        }

        if (selectedItem == null ||
            !selectedPurchases.ContainsKey(selectedItem))
        {
            SelectAnotherFocusedItem();
        }
        else
        {
            purchaseAmount =
                selectedPurchases[selectedItem];
        }
    }

    private void ClearAllSelections(
        bool refreshUI)
    {
        selectedPurchases.Clear();

        selectedItem = null;
        purchaseAmount = 1;
        lastSliderSoundValue = int.MinValue;

        hasSliderAnchor = false;
        HidePurchaseSlider();

        if (refreshUI)
        {
            RefreshUI();
        }
    }

    private void RefreshMoneyText()
    {
        int money =
            gameSessionManager != null
                ? gameSessionManager.CurrentMoney
                : 0;

        SetFormattedText(
            moneyText,
            moneyFormat,
            money
        );
    }

    private void HandleStockChanged()
    {
        if (isProcessingPurchase)
        {
            return;
        }

        PruneAndClampSelections();
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
        if (isMoneySubscribed ||
            gameSessionManager == null)
        {
            return;
        }

        gameSessionManager.MoneyChanged +=
            HandleMoneyChanged;

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
            gameSessionManager.MoneyChanged -=
                HandleMoneyChanged;
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
                FindAnyObjectByType<
                    TownPlayerInventoryController
                >(FindObjectsInactive.Include);
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                GameSessionManager.Instance;
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

    private void PlayItemSelectionToggleSound()
    {
        PlayPurchaseUiSound(
            itemSelectionToggleClip,
            itemSelectionToggleVolume
        );
    }

    private void PlayPurchaseFailedSound()
    {
        PlayPurchaseUiSound(
            purchaseFailedClip,
            purchaseFailedVolume
        );
    }

    private void PlaySliderMoveSound()
    {
        PlayPurchaseUiSound(
            sliderMoveClip,
            sliderMoveVolume
        );
    }

    private void PlayPurchaseUiSound(
        AudioClip clip,
        float volume)
    {
        if (clip == null)
        {
            return;
        }

        if (audioSource == null)
        {
            FindReferences();
        }

        if (audioSource == null)
        {
            LogWarning(
                "購入UI効果音を再生したいですが、AudioSourceが見つかりません。"
            );
            return;
        }

        audioSource.PlayOneShot(
            clip,
            Mathf.Clamp01(volume)
        );
    }

    private static void RestoreRemovedStock(
        ItemBoxInventory stockInventory,
        List<RemovedStockEntry> removedStock)
    {
        if (stockInventory == null)
        {
            return;
        }

        foreach (RemovedStockEntry entry in removedStock)
        {
            if (entry.ItemData == null ||
                entry.Amount <= 0)
            {
                continue;
            }

            stockInventory.TryAddItem(
                entry.ItemData,
                entry.Amount,
                out _
            );
        }
    }

    /// <summary>
    /// 選択中すべてのItemを同時に追加した時、
    /// 現在のPlayer Inventoryへ収まるか仮配置して確認します。
    /// </summary>
    private static bool CanFitPurchaseSet(
        InventoryGrid grid,
        Dictionary<ItemData, int> groupedAmounts)
    {
        if (grid == null)
        {
            return false;
        }

        bool[,] occupied =
            BuildOccupiedMap(grid);

        List<PurchaseGroup> groups =
            new List<PurchaseGroup>();

        foreach (KeyValuePair<ItemData, int> pair
                 in groupedAmounts)
        {
            if (pair.Key == null ||
                pair.Value <= 0)
            {
                continue;
            }

            groups.Add(
                new PurchaseGroup(
                    pair.Key,
                    pair.Value
                )
            );
        }

        // 大きいItemから先に仮配置すると、
        // 小さいItemで大きな空間を先に埋める失敗を減らせます。
        groups.Sort(
            (a, b) =>
            {
                Vector2Int aSize =
                    a.ItemData.GetSize(false);

                Vector2Int bSize =
                    b.ItemData.GetSize(false);

                int aArea =
                    aSize.x * aSize.y;

                int bArea =
                    bSize.x * bSize.y;

                return bArea.CompareTo(aArea);
            }
        );

        foreach (PurchaseGroup group in groups)
        {
            ItemData itemData =
                group.ItemData;

            int remainingAmount =
                group.Amount;

            int maxStack =
                Mathf.Max(
                    1,
                    itemData.MaxStack
                );

            // 既存Stackの空きへ先に詰める
            if (itemData.CanStack)
            {
                foreach (InventoryItem existing
                         in grid.Items)
                {
                    if (existing == null ||
                        existing.ItemData != itemData)
                    {
                        continue;
                    }

                    int stackSpace =
                        Mathf.Max(
                            0,
                            maxStack -
                            existing.Amount
                        );

                    int fill =
                        Mathf.Min(
                            remainingAmount,
                            stackSpace
                        );

                    remainingAmount -= fill;

                    if (remainingAmount <= 0)
                    {
                        break;
                    }
                }
            }

            // 残りは新しい枠を仮予約する
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

                remainingAmount -=
                    Mathf.Min(
                        remainingAmount,
                        maxStack
                    );
            }
        }

        return true;
    }

    /// <summary>
    /// 単一Item用。旧Slider互換用。
    /// </summary>
    private static int GetMaximumFittableAmount(
        InventoryGrid grid,
        ItemData itemData,
        int upperLimit)
    {
        if (grid == null ||
            itemData == null ||
            upperLimit <= 0)
        {
            return 0;
        }

        int low = 0;
        int high = upperLimit;

        while (low < high)
        {
            int mid =
                low +
                (high - low + 1) / 2;

            Dictionary<ItemData, int> test =
                new Dictionary<ItemData, int>
                {
                    { itemData, mid }
                };

            if (CanFitPurchaseSet(grid, test))
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

    private static bool[,] BuildOccupiedMap(
        InventoryGrid grid)
    {
        bool[,] occupied =
            new bool[
                grid.Width,
                grid.Height
            ];

        foreach (InventoryItem item in grid.Items)
        {
            if (item == null ||
                item.ItemData == null)
            {
                continue;
            }

            Vector2Int size =
                item.ItemData.GetSize(
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
                    if (x >= 0 &&
                        x < grid.Width &&
                        y >= 0 &&
                        y < grid.Height)
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
        Vector2Int size =
            itemData.GetSize(isRotated);

        for (int y = 0;
             y < gridHeight;
             y++)
        {
            for (int x = 0;
                 x < gridWidth;
                 x++)
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
                        occupied[
                            reserveX,
                            reserveY
                        ] = true;
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

        for (int y = startY;
             y < startY + size.y;
             y++)
        {
            for (int x = startX;
                 x < startX + size.x;
                 x++)
            {
                if (occupied[x, y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static int MultiplyPrice(
        int unitPrice,
        int amount)
    {
        long total =
            (long)Mathf.Max(
                0,
                unitPrice
            ) *
            Mathf.Max(
                0,
                amount
            );

        return total > int.MaxValue
            ? int.MaxValue
            : Mathf.Max(
                0,
                (int)total
            );
    }

    private static int SafeAddPrice(
        int current,
        int add)
    {
        long total =
            (long)Mathf.Max(0, current) +
            Mathf.Max(0, add);

        return total > int.MaxValue
            ? int.MaxValue
            : (int)total;
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

        string safeFormat =
            string.IsNullOrWhiteSpace(format)
                ? "{0}"
                : format;

        target.text =
            string.Format(
                safeFormat,
                Mathf.Max(0, value)
            );
    }

    /// <summary>
    /// 「所持金が足りない」を下から上へ移動しながらフェードアウト表示します。
    /// </summary>
    private void ShowInsufficientMoneyStatus()
    {
        string message =
            string.IsNullOrWhiteSpace(
                cartInsufficientMoneyMessage)
                ? "所持金が足りない"
                : cartInsufficientMoneyMessage;

        if (!animateInsufficientMoneyStatus ||
            statusText == null)
        {
            SetStatus(message, true);
            return;
        }

        StartStatusAnimation(
            message,
            insufficientMoneyStatusDuration,
            insufficientMoneyStatusStartYOffset,
            insufficientMoneyStatusEndYOffset,
            insufficientMoneyStatusStartAlpha,
            insufficientMoneyStatusEndAlpha
        );
    }

    /// <summary>
    /// 「スペースがない」を下から上へ移動しながらフェードアウト表示します。
    /// </summary>
    private void ShowInventoryFullStatus()
    {
        string message =
            string.IsNullOrWhiteSpace(
                cartInventoryFullMessage)
                ? "スペースがない"
                : cartInventoryFullMessage;

        if (!animateInventoryFullStatus ||
            statusText == null)
        {
            SetStatus(message, true);
            return;
        }

        StartStatusAnimation(
            message,
            inventoryFullStatusDuration,
            inventoryFullStatusStartYOffset,
            inventoryFullStatusEndYOffset,
            inventoryFullStatusStartAlpha,
            inventoryFullStatusEndAlpha
        );
    }

    private void StartStatusAnimation(
        string message,
        float duration,
        float startYOffset,
        float endYOffset,
        float startAlpha,
        float endAlpha)
    {
        CaptureStatusBaseState();
        StopStatusAnimation(true);

        statusAnimationCoroutine =
            StartCoroutine(
                AnimateStatusMessage(
                    message,
                    duration,
                    startYOffset,
                    endYOffset,
                    startAlpha,
                    endAlpha
                )
            );

        LogWarning(message);
    }

    private IEnumerator AnimateStatusMessage(
        string message,
        float requestedDuration,
        float startYOffset,
        float endYOffset,
        float startAlpha,
        float endAlpha)
    {
        if (statusText == null)
        {
            statusAnimationCoroutine = null;
            yield break;
        }

        CaptureStatusBaseState();

        if (statusRectTransform == null)
        {
            statusText.text = message;
            statusAnimationCoroutine = null;
            yield break;
        }

        statusText.text = message;

        float duration =
            Mathf.Max(
                0.1f,
                requestedDuration
            );

        Vector2 startPosition =
            statusBaseAnchoredPosition +
            Vector2.down *
            Mathf.Abs(
                startYOffset
            );

        Vector2 endPosition =
            statusBaseAnchoredPosition +
            Vector2.up *
            Mathf.Abs(
                endYOffset
            );

        Color startColor =
            statusBaseColor;

        startColor.a =
            Mathf.Clamp01(startAlpha);

        Color endColor =
            statusBaseColor;

        endColor.a =
            Mathf.Clamp01(endAlpha);

        statusRectTransform.anchoredPosition =
            startPosition;

        statusText.color =
            startColor;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            // 少しだけ滑らかに動く
            float eased =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            statusRectTransform.anchoredPosition =
                Vector2.LerpUnclamped(
                    startPosition,
                    endPosition,
                    eased
                );

            statusText.color =
                Color.Lerp(
                    startColor,
                    endColor,
                    t
                );

            yield return null;
        }

        statusRectTransform.anchoredPosition =
            endPosition;

        statusText.color =
            endColor;

        // 完全に消えた後はTextを空にして、
        // 次の通常Status表示に影響しないよう元の状態へ戻す。
        if (Mathf.Clamp01(endAlpha) <= 0.001f)
        {
            statusText.text = string.Empty;
        }

        ResetStatusVisualState();

        statusAnimationCoroutine = null;
    }

    private void CaptureStatusBaseState()
    {
        if (statusText == null ||
            hasCapturedStatusBaseState)
        {
            return;
        }

        statusRectTransform =
            statusText.rectTransform;

        if (statusRectTransform != null)
        {
            statusBaseAnchoredPosition =
                statusRectTransform.anchoredPosition;
        }

        statusBaseColor =
            statusText.color;

        hasCapturedStatusBaseState = true;
    }

    private void StopStatusAnimation(
        bool resetVisual)
    {
        if (statusAnimationCoroutine != null)
        {
            StopCoroutine(
                statusAnimationCoroutine
            );

            statusAnimationCoroutine = null;
        }

        if (resetVisual)
        {
            ResetStatusVisualState();
        }
    }

    private void ResetStatusVisualState()
    {
        if (!hasCapturedStatusBaseState ||
            statusText == null)
        {
            return;
        }

        if (statusRectTransform == null)
        {
            statusRectTransform =
                statusText.rectTransform;
        }

        if (statusRectTransform != null)
        {
            statusRectTransform.anchoredPosition =
                statusBaseAnchoredPosition;
        }

        statusText.color =
            statusBaseColor;
    }

    private void SetStatus(
        string message,
        bool warning)
    {
        // 通常Statusを表示する時は、
        // 所持金不足アニメーションを止めて元の位置・色へ戻します。
        StopStatusAnimation(true);

        if (statusText != null)
        {
            statusText.text =
                message ?? string.Empty;
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
            Debug.Log(
                $"[MerchantPurchaseController] {message}",
                this
            );
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
        purchaseSoundVolume =
            Mathf.Clamp01(
                purchaseSoundVolume
            );

        itemSelectionToggleVolume =
            Mathf.Clamp01(
                itemSelectionToggleVolume
            );

        purchaseFailedVolume =
            Mathf.Clamp01(
                purchaseFailedVolume
            );

        sliderMoveVolume =
            Mathf.Clamp01(
                sliderMoveVolume
            );

        insufficientMoneyStatusDuration =
            Mathf.Max(
                0.1f,
                insufficientMoneyStatusDuration
            );

        insufficientMoneyStatusStartAlpha =
            Mathf.Clamp01(
                insufficientMoneyStatusStartAlpha
            );

        insufficientMoneyStatusEndAlpha =
            Mathf.Clamp01(
                insufficientMoneyStatusEndAlpha
            );

        inventoryFullStatusDuration =
            Mathf.Max(
                0.1f,
                inventoryFullStatusDuration
            );

        inventoryFullStatusStartAlpha =
            Mathf.Clamp01(
                inventoryFullStatusStartAlpha
            );

        inventoryFullStatusEndAlpha =
            Mathf.Clamp01(
                inventoryFullStatusEndAlpha
            );

        if (purchaseAmountSlider != null)
        {
            purchaseAmountSlider.wholeNumbers = true;
            purchaseAmountSlider.minValue = 1f;
        }
    }
}
