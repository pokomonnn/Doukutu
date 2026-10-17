using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 購入・売却パネル全体を管理します。
/// 購入側はMerchantPurchaseController + ItemBoxInventory、
/// 売却側は既存のSellCartInventoryを使用します。
///
/// このコンポーネントは非表示になるPawnShopPanel自身ではなく、
/// 常に有効なTownCanvas / PawnShopSystemへ付けてください。
/// </summary>
[DisallowMultipleComponent]
public class PawnShopUIController : MonoBehaviour
{
    [Header("メインパネル")]
    [SerializeField] private GameObject pawnShopPanel;

    [Header("購入・売却タブ（任意）")]
    [SerializeField] private GameObject purchasePanel;
    [SerializeField] private GameObject sellPanel;
    [SerializeField] private Button purchaseTabButton;
    [SerializeField] private Button sellTabButton;

    [Tooltip("購入/売却タブButtonをまとめている親Objectです。未設定なら各Buttonを直接表示/非表示にします。")]
    [SerializeField] private GameObject tradeTabsRoot;

    [Header("売却システム")]
    [SerializeField] private SellCartInventory sellCart;
    [SerializeField] private ShopSellTransactionController transactionController;
    [SerializeField] private PlayerTradeInventoryBridge townPlayerInventory;
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("購入システム")]
    [SerializeField] private MerchantPurchaseController merchantPurchaseController;

    [Header("町の一体型売買UI（v3.1 / 任意）")]
    [Tooltip("指定時は希少品店・武器商人を同じ一体型決済画面で開きます。未設定なら旧タブ式のままです。")]
    [SerializeField] private TownUnifiedTradeScreen2D unifiedTownTradeScreen;
    [SerializeField] private bool useUnifiedTownTrade = true;

    [Header("Player Inventory接続")]
    [Tooltip(
        "ONなら、Player Inventory Grid UIのInspectorに設定されているInventoryControllerを優先します。" +
        " Manager側のInventoryControllerを表示したい構成ではON推奨です。"
    )]
    [SerializeField] private bool preferAssignedPlayerGridInventory = true;

    [Tooltip(
        "Player Inventory Grid UIにInventoryControllerが未設定の時だけ、" +
        "PlayerTradeInventoryBridgeのInventoryControllerへ自動接続します。"
    )]
    [SerializeField] private bool fallbackToPlayerTradeInventoryBridge = true;

    [Header("武器修理システム")]
    [Tooltip("武器修理Panelを管理するControllerです。未設定ならシーン内から自動検索します。")]
    [SerializeField] private MerchantWeaponRepairController weaponRepairController;

    [Header("売却側のGrid UI")]
    [Tooltip("プレイヤー所持品を表示するGrid UIです。InspectorでManager側InventoryControllerを設定できます。")]
    [SerializeField] private InventoryGridUI playerInventoryGridUI;

    [Tooltip("SellCartInventoryのItemBoxInventoryを表示するGrid UIです。")]
    [SerializeField] private InventoryGridUI sellCartGridUI;

    [Header("動作")]
    [SerializeField] private bool hidePanelOnAwake = true;
    [SerializeField] private bool openPurchaseTabFirst = true;

    [Tooltip("閉じる時やTown_Mainを離れる時、会計前のアイテムをプレイヤーへ戻します。")]
    [SerializeField] private bool returnCartItemsWhenClosing = true;

    [Tooltip("閉じる時に現在のプレイヤーインベントリをGameSessionManagerへ保存します。")]
    [SerializeField] private bool captureInventoryWhenClosing = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    /// <summary>
    /// 現在開いている売買UIです。InventoryItemUIのクイック売却から参照されます。
    /// </summary>
    public static PawnShopUIController ActiveInstance { get; private set; }

    /// <summary>出撃補充画面から既存売買パネルを前面に移動するための参照。</summary>
    public GameObject TradePanelRoot => pawnShopPanel;

    public bool IsOpen => isOpen;
    public bool IsPurchaseAvailable => purchaseAvailable;
    public bool IsRepairMode => isRepairMode;

    /// <summary>
    /// 売却タブが実際に利用中かを返します。
    /// </summary>
    public bool IsSellTabActive =>
        isOpen &&
        !isRepairMode &&
        !isUnifiedTradeMode &&
        sellPanel != null &&
        sellPanel.activeInHierarchy;

    public bool IsSellPanelActuallyVisible =>
        sellPanel != null && sellPanel.activeInHierarchy;

    public bool IsSellCartGridActuallyVisible =>
        sellCartGridUI != null &&
        sellCartGridUI.gameObject.activeInHierarchy;

    /// <summary>
    /// Player Inventory上のItemを左クリックした時、
    /// 売却カートの最初に置ける位置へ自動移動します。
    /// </summary>
    public bool TryQuickAddPlayerItemToSellCart(InventoryItem item)
    {
        FindReferences();

        if (!IsSellTabActive ||
            item == null ||
            item.ItemData == null ||
            playerInventoryGridUI == null ||
            sellCartGridUI == null ||
            sellCart == null ||
            sellCart.CartInventory == null ||
            sellCart.CartInventory.Grid == null ||
            !playerInventoryGridUI.ContainsItem(item))
        {
            return false;
        }

        if (!sellCart.CanAcceptItem(item, out string rejectionReason))
        {
            sellCart.ReportRejectedTransfer(item, rejectionReason);
            return false;
        }

        InventoryGrid targetGrid = sellCart.CartInventory.Grid;
        bool firstRotation = item.IsRotated;

        if (TryQuickTransferAtRotation(item, targetGrid, firstRotation))
        {
            return true;
        }

        if (item.CanRotate &&
            TryQuickTransferAtRotation(item, targetGrid, !firstRotation))
        {
            return true;
        }

        return false;
    }

    private bool TryQuickTransferAtRotation(
        InventoryItem item,
        InventoryGrid targetGrid,
        bool isRotated)
    {
        if (item == null || targetGrid == null)
        {
            return false;
        }

        for (int y = 0; y < targetGrid.Height; y++)
        {
            for (int x = 0; x < targetGrid.Width; x++)
            {
                if (!targetGrid.CanPlaceItem(item, x, y, isRotated))
                {
                    continue;
                }

                bool moved = playerInventoryGridUI.TryTransferItemTo(
                    item,
                    sellCartGridUI,
                    x,
                    y,
                    isRotated
                );

                if (moved)
                {
                    BindAndRefreshSellGridUIs();
                    transactionController?.RefreshUI();
                    return true;
                }
            }
        }

        return false;
    }

    private bool isOpen;
    private bool purchaseAvailable;
    private bool isRepairMode;
    private bool isUnifiedTradeMode;
    private bool isApplicationQuitting;

    private void Awake()
    {
        FindReferences();
        SetupTabButtons();

        if (hidePanelOnAwake)
        {
            merchantPurchaseController?.CloseShop();
            weaponRepairController?.CloseRepairShop();
            SetPanelVisible(false);
        }
    }

    private void OnDestroy()
    {
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        RemoveTabButtonListeners();

        if (!isApplicationQuitting)
        {
            TryReturnCartBeforeLeaving();
        }
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    /// <summary>
    /// TownConversationControllerのOpenPawnShop選択肢から呼ばれます。
    /// TownConversationButtonが記録したMerchantStockInventoryを使用します。
    /// </summary>
    public void OpenPawnShop()
    {
        FindReferences();
        SetupTabButtons();

        if (pawnShopPanel == null && !(useUnifiedTownTrade && unifiedTownTradeScreen != null))
        {
            LogWarning("Pawn Shop Panelが未設定です。");
            return;
        }

        MerchantStockInventory stock =
            MerchantShopConversationContext.CurrentStock;

        if (stock == null)
        {
            LogWarning(
                "現在会話中のMerchantStockInventoryが見つからないため、" +
                "購入・売却画面を開けません。"
            );
            return;
        }

        // 町の一体型売買画面が設定されている場合は、従来タブ式の
        // Purchase/Sell Controllerを開かず、出撃補充と同じ差額決済を使用する。
        if (useUnifiedTownTrade && unifiedTownTradeScreen != null)
        {
            if (!TryReturnCartBeforeLeaving())
            {
                LogWarning("以前の売却カートをPlayerへ戻せないため画面を開きません。");
                return;
            }

            merchantPurchaseController?.CloseShop();
            weaponRepairController?.CloseRepairShop();
            SetPanelVisible(false);
            isRepairMode = false;
            isUnifiedTradeMode = false;

            bool opened = unifiedTownTradeScreen.Open(stock);
            if (!opened)
            {
                LogWarning("一体型売買画面を開けませんでした。UIのGrid/Bridge参照を確認してください。");
                return;
            }

            isUnifiedTradeMode = true;
            purchaseAvailable = true;
            isOpen = true;
            ActiveInstance = this;
            Log("一体型売買画面を開きました。店舗=" + stock.ShopName);
            return;
        }

        ReturnOldSellCartIfNeeded();

        // 売買を開く時は修理画面を必ず閉じる。
        weaponRepairController?.CloseRepairShop();

        isRepairMode = false;
        SetTradeNavigationVisible(true);
        SetPanelVisible(true);
        isOpen = true;
        ActiveInstance = this;

        // 購入側だけでなく、売却カートと会計処理にも
        // 現在の商人を渡し、商人ごとの買取カテゴリーを反映する。
        sellCart?.SetMerchantStock(stock);
        transactionController?.SetMerchantStock(stock);

        BindAndRefreshSellGridUIs();
        transactionController?.RefreshUI();

        purchaseAvailable =
            merchantPurchaseController != null &&
            merchantPurchaseController.OpenShop(stock);

        if (purchaseAvailable && openPurchaseTabFirst)
        {
            ShowPurchaseTab();
        }
        else
        {
            ShowSellTab();
        }

        if (purchaseAvailable)
        {
            Log(
                $"購入・売却画面を開きました。店舗={stock.ShopName}"
            );
        }
        else
        {
            LogWarning(
                "購入用のMerchantStockInventoryを開けなかったため、" +
                "売却画面を表示しました。"
            );
        }
    }

    /// <summary>
    /// 会話システムを経由しない洞窟商人などから、
    /// MerchantStockInventoryを直接指定して売買画面を開きます。
    /// </summary>
    public bool OpenPawnShop(MerchantStockInventory stockInventory)
    {
        if (stockInventory == null)
        {
            LogWarning("MerchantStockInventoryが未設定のため売買画面を開けません。");
            return false;
        }

        MerchantShopConversationContext.SetMerchant(
            null,
            stockInventory
        );

        OpenPawnShop();
        return isOpen;
    }

    /// <summary>
    /// TownConversationControllerのOpenWeaponRepair選択肢から呼ばれます。
    /// 通常の購入・売却Panelは表示せず、武器修理Panelだけを開きます。
    /// </summary>
    public void OpenWeaponRepair()
    {
        FindReferences();
        SetupTabButtons();

        if (pawnShopPanel == null)
        {
            LogWarning("Pawn Shop Panelが未設定です。");
            return;
        }

        if (weaponRepairController == null)
        {
            LogWarning(
                "MerchantWeaponRepairControllerが見つからないため、" +
                "武器修理画面を開けません。"
            );
            return;
        }

        MerchantStockInventory stock =
            MerchantShopConversationContext.CurrentStock;

        if (stock == null)
        {
            LogWarning(
                "現在会話中のMerchantStockInventoryが見つからないため、" +
                "武器修理画面を開けません。"
            );
            return;
        }

        // 先に一体型売買画面を安全に閉じる。
        if (isUnifiedTradeMode)
        {
            unifiedTownTradeScreen?.Close();
            isUnifiedTradeMode = false;
        }

        // 前回、売却カートへItemを残したまま別画面へ進んだ場合は先に戻す。
        ReturnOldSellCartIfNeeded();

        // 修理モードでは通常の購入・売却処理を停止する。
        merchantPurchaseController?.CloseShop();
        transactionController?.SetMerchantStock(null);
        sellCart?.ClearMerchantStock();

        purchaseAvailable = false;
        isRepairMode = true;
        isOpen = true;
        ActiveInstance = this;

        SetPanelVisible(true);
        SetTradePanelsVisible(false);
        SetTradeNavigationVisible(false);

        weaponRepairController.CloseRepairShop();

        bool repairOpened =
            weaponRepairController.OpenRepairShop(
                stock
            );

        if (!repairOpened)
        {
            isRepairMode = false;
            isOpen = false;
            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }
            SetTradeNavigationVisible(true);
            SetPanelVisible(false);

            LogWarning(
                "この商人では武器修理サービスを開けませんでした。"
            );
            return;
        }

        Log(
            $"武器修理画面を開きました。店舗={stock.ShopName}"
        );
    }

    /// <summary>
    /// MerchantWeaponRepairControllerの「戻る」から呼べます。
    /// 現在の武器修理画面を安全に閉じます。
    /// </summary>
    public void ReturnFromWeaponRepair()
    {
        TryClosePawnShop();
    }

    public void ShowPurchaseTab()
    {
        if (isRepairMode || isUnifiedTradeMode)
        {
            return;
        }

        if (!purchaseAvailable)
        {
            ShowSellTab();
            return;
        }

        SetTabPanels(true);
        merchantPurchaseController?.RefreshUI();
    }

    public void ShowSellTab()
    {
        if (isRepairMode || isUnifiedTradeMode)
        {
            return;
        }

        SetTabPanels(false);
        BindAndRefreshSellGridUIs();
        transactionController?.RefreshUI();
    }

    public void ClosePawnShop()
    {
        TryClosePawnShop();
    }

    public bool TryClosePawnShop()
    {
        if (!isOpen &&
            pawnShopPanel != null &&
            !pawnShopPanel.activeSelf)
        {
            return true;
        }

        if (!TryReturnCartBeforeLeaving())
        {
            return false;
        }

        // 統合UIと旧タブUIでは使用するPlayerGridが異なる場合がある。
        // 統合UIは「生成時に指定したPlayerTradeInventoryBridge」から保存し、
        // 古い売却タブのGridを保存して状態を上書きしない。
        if (isUnifiedTradeMode)
        {
            unifiedTownTradeScreen?.CapturePlayerSession();
        }
        else
        {
            CaptureTownInventory();
        }

        // 一体型売買画面の未決済選択を解除し、非表示へ戻す。
        if (isUnifiedTradeMode)
        {
            unifiedTownTradeScreen?.Close();
            isUnifiedTradeMode = false;
        }

        merchantPurchaseController?.CloseShop();
        weaponRepairController?.CloseRepairShop();
        transactionController?.SetMerchantStock(null);
        sellCart?.ClearMerchantStock();
        MerchantShopConversationContext.Clear();

        purchaseAvailable = false;
        isRepairMode = false;
        isOpen = false;
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }
        SetTradeNavigationVisible(true);
        SetPanelVisible(false);

        Log("商人画面を閉じました。");
        return true;
    }

    public void Checkout()
    {
        FindReferences();

        if (transactionController == null)
        {
            LogWarning("ShopSellTransactionControllerが未設定です。");
            return;
        }

        if (transactionController.Checkout())
        {
            CaptureTownInventory();
        }
    }

    private void ReturnOldSellCartIfNeeded()
    {
        if (sellCart == null ||
            !sellCart.HasItems ||
            transactionController == null)
        {
            return;
        }

        bool returned =
            transactionController.ReturnAllItemsToPlayer();

        if (!returned)
        {
            LogWarning(
                "前回の売却予定アイテムをすべて戻せませんでした。" +
                "プレイヤーインベントリの空きを作ってください。"
            );
        }
    }

    private bool TryReturnCartBeforeLeaving()
    {
        if (!returnCartItemsWhenClosing ||
            sellCart == null ||
            !sellCart.HasItems)
        {
            return true;
        }

        if (transactionController == null)
        {
            LogWarning(
                "売却予定のアイテムがありますが、" +
                "ShopSellTransactionControllerが未設定です。"
            );
            return false;
        }

        bool returned =
            transactionController.ReturnAllItemsToPlayer();

        if (!returned)
        {
            LogWarning(
                "売却予定アイテムをすべて戻せないため、" +
                "アイテム消失防止のため画面を閉じません。"
            );
        }

        return returned;
    }

    private void CaptureTownInventory()
    {
        if (!captureInventoryWhenClosing)
        {
            return;
        }

        InventoryController resolvedInventory =
            ResolvePlayerInventoryController(
                bindFallbackToGrid: false
            );

        if (resolvedInventory == null)
        {
            LogWarning(
                "閉じる時の保存対象InventoryControllerが見つかりません。"
            );
            return;
        }

        // Bridgeが同じInventoryを管理している時は従来のSessionBridgeを使用。
        if (townPlayerInventory != null &&
            townPlayerInventory.InventoryController == resolvedInventory &&
            townPlayerInventory.SessionBridge != null)
        {
            townPlayerInventory.SessionBridge.CaptureToSession();
            return;
        }

        // Manager側InventoryをGrid UIで指定している場合は、
        // そのInventoryControllerをGameSessionManagerへ直接保存する。
        if (gameSessionManager != null)
        {
            gameSessionManager.CapturePlayerInventory(
                resolvedInventory,
                townPlayerInventory != null
                    ? townPlayerInventory.EquipmentController
                    : null
            );
        }
    }

    private void BindAndRefreshSellGridUIs()
    {
        // Inspectorで設定済みのPlayer InventoryControllerを優先する。
        // 未設定時のみPlayerTradeInventoryBridgeへ自動接続する。
        PreparePlayerInventoryGrid();

        if (sellCartGridUI != null &&
            sellCart != null &&
            sellCart.CartInventory != null)
        {
            sellCartGridUI.BindItemBoxInventory(
                sellCart.CartInventory
            );
        }

        playerInventoryGridUI?.RefreshInventoryUI();
        sellCartGridUI?.RefreshInventoryUI();
    }

    /// <summary>
    /// Player Inventory Grid UIのInspector設定を優先し、
    /// 必要な時だけPlayerTradeInventoryBridgeへフォールバックします。
    /// </summary>
    private InventoryController ResolvePlayerInventoryController(
        bool bindFallbackToGrid)
    {
        InventoryController gridController =
            playerInventoryGridUI != null
                ? playerInventoryGridUI.Controller
                : null;

        if (preferAssignedPlayerGridInventory &&
            gridController != null)
        {
            return gridController;
        }

        InventoryController bridgeController =
            townPlayerInventory != null
                ? townPlayerInventory.InventoryController
                : null;

        if (fallbackToPlayerTradeInventoryBridge &&
            bridgeController != null)
        {
            if (bindFallbackToGrid &&
                playerInventoryGridUI != null &&
                playerInventoryGridUI.Controller != bridgeController)
            {
                playerInventoryGridUI.BindPlayerInventory(
                    bridgeController
                );
            }

            return bridgeController;
        }

        return gridController;
    }

    private void PreparePlayerInventoryGrid()
    {
        if (playerInventoryGridUI == null)
        {
            LogWarning("Player Inventory Grid UIが未設定です。");
            return;
        }

        InventoryController before =
            playerInventoryGridUI.Controller;

        InventoryController resolved =
            ResolvePlayerInventoryController(
                bindFallbackToGrid: true
            );

        if (resolved == null)
        {
            LogWarning(
                "Player Inventory用InventoryControllerを解決できませんでした。" +
                " InventoryGridUIのInventory Controller設定を確認してください。"
            );
            return;
        }

        playerInventoryGridUI.RefreshInventoryUI();

        if (showDebugLogs)
        {
            string mode =
                preferAssignedPlayerGridInventory && before != null
                    ? "Grid Inspector設定を維持"
                    : "PlayerTradeInventoryBridgeへフォールバック";

            Log(
                $"Player Inventory接続: Mode={mode} / " +
                $"Controller={resolved.name}"
            );
        }
    }

    private void SetTradePanelsVisible(bool visible)
    {
        if (!visible)
        {
            if (purchasePanel != null)
            {
                purchasePanel.SetActive(false);
            }

            if (sellPanel != null)
            {
                sellPanel.SetActive(false);
            }

            return;
        }

        if (purchaseAvailable && openPurchaseTabFirst)
        {
            SetTabPanels(true);
        }
        else
        {
            SetTabPanels(false);
        }
    }

    private void SetTradeNavigationVisible(bool visible)
    {
        if (tradeTabsRoot != null)
        {
            tradeTabsRoot.SetActive(visible);
            return;
        }

        if (purchaseTabButton != null)
        {
            purchaseTabButton.gameObject.SetActive(visible);
        }

        if (sellTabButton != null)
        {
            sellTabButton.gameObject.SetActive(visible);
        }
    }

    private void SetTabPanels(bool showPurchase)
    {
        if (purchasePanel != null)
        {
            purchasePanel.SetActive(
                showPurchase && purchaseAvailable
            );
        }

        if (sellPanel != null)
        {
            sellPanel.SetActive(!showPurchase);
        }

        if (purchaseTabButton != null)
        {
            purchaseTabButton.interactable =
                purchaseAvailable && !showPurchase;
        }

        if (sellTabButton != null)
        {
            sellTabButton.interactable = showPurchase;
        }
    }

    private void SetPanelVisible(bool visible)
    {
        if (pawnShopPanel != null &&
            pawnShopPanel.activeSelf != visible)
        {
            pawnShopPanel.SetActive(visible);
        }
    }

    private void SetupTabButtons()
    {
        RemoveTabButtonListeners();
        purchaseTabButton?.onClick.AddListener(ShowPurchaseTab);
        sellTabButton?.onClick.AddListener(ShowSellTab);
    }

    private void RemoveTabButtonListeners()
    {
        purchaseTabButton?.onClick.RemoveListener(ShowPurchaseTab);
        sellTabButton?.onClick.RemoveListener(ShowSellTab);
    }

    private void FindReferences()
    {
        if (sellCart == null)
        {
            sellCart = FindAnyObjectByType<SellCartInventory>(
                FindObjectsInactive.Include
            );
        }

        if (transactionController == null)
        {
            transactionController =
                FindAnyObjectByType<ShopSellTransactionController>(
                    FindObjectsInactive.Include
                );
        }

        if (townPlayerInventory == null)
        {
            townPlayerInventory =
                FindAnyObjectByType<PlayerTradeInventoryBridge>(
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

        if (merchantPurchaseController == null)
        {
            merchantPurchaseController =
                FindAnyObjectByType<MerchantPurchaseController>(
                    FindObjectsInactive.Include
                );
        }

        if (weaponRepairController == null)
        {
            weaponRepairController =
                FindAnyObjectByType<MerchantWeaponRepairController>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PawnShopUIController] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[PawnShopUIController] {message}",
            this
        );
    }
}