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
    private const string QuickSellDiagnosticVersion =
        "SellClickFix_v3_2026-09-01";

    public static PawnShopUIController ActiveInstance
    {
        get;
        private set;
    }

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
    [SerializeField] private TownPlayerInventoryController townPlayerInventory;

    [Header("購入システム")]
    [SerializeField] private MerchantPurchaseController merchantPurchaseController;

    [Header("武器修理システム")]
    [Tooltip("武器修理Panelを管理するControllerです。未設定ならシーン内から自動検索します。")]
    [SerializeField] private MerchantWeaponRepairController weaponRepairController;

    [Header("売却側のGrid UI")]
    [Tooltip("TownPlayerInventoryのInventoryControllerを表示するGrid UIです。")]
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

    [Header("クリック売却診断")]
    [Tooltip("Player Inventory左クリック売却の各段階をConsoleへ詳しく出します。原因特定後はOFFにできます。")]
    [SerializeField] private bool showQuickSellDiagnostics = true;

    public bool IsOpen => isOpen;
    public bool IsPurchaseAvailable => purchaseAvailable;
    public bool IsRepairMode => isRepairMode;

    public bool IsSellPanelActuallyVisible =>
        sellPanel != null &&
        sellPanel.activeInHierarchy;

    public bool IsSellCartGridActuallyVisible =>
        sellCartGridUI != null &&
        sellCartGridUI.gameObject.activeInHierarchy;

    /// <summary>
    /// 現在、通常取引の「売却」タブを表示しているか。
    /// Player Inventoryの左クリック簡単売却で使用します。
    /// </summary>
    public bool IsSellTabActive
    {
        get
        {
            if (!isOpen || isRepairMode)
            {
                return false;
            }

            if (isSellTabActive)
            {
                return true;
            }

            // 内部フラグが更新されていなくても、
            // 実際にSellPanel、またはSellCart Gridが表示中なら
            // 売却画面として扱う。
            //
            // UI Button側でPanelのSetActiveだけを切り替えていて
            // ShowSellTab()が呼ばれていない構成にも対応します。
            return
                IsSellPanelActuallyVisible ||
                IsSellCartGridActuallyVisible;
        }
    }

    private bool isOpen;
    private bool purchaseAvailable;
    private bool isRepairMode;
    private bool isSellTabActive;
    private bool isApplicationQuitting;

    private void Awake()
    {
        ActiveInstance = this;

        QuickSellDiagnostic(
            $"Awake / Version={QuickSellDiagnosticVersion} / " +
            $"ActiveInstance設定 / Object={GetTransformPath(transform)}"
        );

        PawnShopUIController[] pawnControllers =
            FindObjectsByType<PawnShopUIController>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (pawnControllers.Length > 1)
        {
            QuickSellDiagnostic(
                $"注意：PawnShopUIControllerがScene内に{pawnControllers.Length}個あります。"
            );
        }

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

        if (pawnShopPanel == null)
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

        ReturnOldSellCartIfNeeded();

        // 売買を開く時は修理画面を必ず閉じる。
        weaponRepairController?.CloseRepairShop();

        isRepairMode = false;
        isSellTabActive = false;
        SetTradeNavigationVisible(true);
        SetPanelVisible(true);
        isOpen = true;

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

        // 前回、売却カートへItemを残したまま別画面へ進んだ場合は先に戻す。
        ReturnOldSellCartIfNeeded();

        // 修理モードでは通常の購入・売却処理を停止する。
        merchantPurchaseController?.CloseShop();
        transactionController?.SetMerchantStock(null);
        sellCart?.ClearMerchantStock();

        purchaseAvailable = false;
        isRepairMode = true;
        isSellTabActive = false;
        isOpen = true;

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
            isSellTabActive = false;
            isOpen = false;
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
        if (isRepairMode)
        {
            return;
        }

        if (!purchaseAvailable)
        {
            ShowSellTab();
            return;
        }

        isSellTabActive = false;

        QuickSellDiagnostic(
            $"ShowPurchaseTab / IsOpen={isOpen} / " +
            $"RepairMode={isRepairMode} / SellTabActive={IsSellTabActive}"
        );

        SetTabPanels(true);
        merchantPurchaseController?.RefreshUI();
    }

    public void ShowSellTab()
    {
        if (isRepairMode)
        {
            return;
        }

        isSellTabActive = true;

        QuickSellDiagnostic(
            $"ShowSellTab / IsOpen={isOpen} / " +
            $"RepairMode={isRepairMode} / " +
            $"InternalSellTab={isSellTabActive} / " +
            $"PublicSellTab={IsSellTabActive}"
        );

        SetTabPanels(false);
        BindAndRefreshSellGridUIs();
        transactionController?.RefreshUI();

        QuickSellDiagnostic(
            "ShowSellTab完了 / " +
            $"SellPanelVisible={IsSellPanelActuallyVisible} / " +
            $"SellCartGridVisible={IsSellCartGridActuallyVisible} / " +
            $"PublicSellTab={IsSellTabActive} / " +
            $"PlayerGrid={(playerInventoryGridUI != null ? GetTransformPath(playerInventoryGridUI.transform) : "null")} / " +
            $"SellCartGrid={(sellCartGridUI != null ? GetTransformPath(sellCartGridUI.transform) : "null")} / " +
            $"SellCart={(sellCart != null ? GetTransformPath(sellCart.transform) : "null")} / " +
            $"Transaction={(transactionController != null ? GetTransformPath(transactionController.transform) : "null")}"
        );
    }

    /// <summary>
    /// 売却タブ中、Player InventoryのItemを左クリックした時に呼ばれます。
    /// SellCartの空き位置を自動検索し、既存のTryTransferItemTo()経由で
    /// 商人の買取条件を確認しながらカートへ移動します。
    /// </summary>
    public bool TryQuickAddPlayerItemToSellCart(
        InventoryItem item)
    {
        FindReferences();

        string itemName =
            item != null &&
            item.ItemData != null
                ? item.ItemData.DisplayName
                : "null";

        QuickSellDiagnostic(
            $"[STEP 1] TryQuickAdd開始 / Item={itemName} / " +
            $"IsOpen={isOpen} / RepairMode={isRepairMode} / " +
            $"InternalSellTab={isSellTabActive} / " +
            $"SellPanelVisible={IsSellPanelActuallyVisible} / " +
            $"SellCartGridVisible={IsSellCartGridActuallyVisible} / " +
            $"PublicSellTab={IsSellTabActive}"
        );

        if (!IsSellTabActive)
        {
            QuickSellDiagnostic(
                "[STOP] 売却タブ判定NG。 " +
                $"isOpen={isOpen}, isRepairMode={isRepairMode}, " +
                $"isSellTabActive={isSellTabActive}, " +
                $"SellPanelVisible={IsSellPanelActuallyVisible}, " +
                $"SellCartGridVisible={IsSellCartGridActuallyVisible}"
            );
            return false;
        }

        if (item == null)
        {
            QuickSellDiagnostic("[STOP] item=null");
            return false;
        }

        if (item.ItemData == null)
        {
            QuickSellDiagnostic("[STOP] item.ItemData=null");
            return false;
        }

        QuickSellDiagnostic(
            $"[STEP 2] 参照確認 / " +
            $"PlayerGrid={(playerInventoryGridUI != null ? GetTransformPath(playerInventoryGridUI.transform) : "null")} / " +
            $"SellCartGrid={(sellCartGridUI != null ? GetTransformPath(sellCartGridUI.transform) : "null")} / " +
            $"SellCart={(sellCart != null ? GetTransformPath(sellCart.transform) : "null")} / " +
            $"CartInventory={(sellCart != null && sellCart.CartInventory != null ? GetTransformPath(sellCart.CartInventory.transform) : "null")}"
        );

        if (playerInventoryGridUI == null)
        {
            QuickSellDiagnostic("[STOP] Player Inventory Grid UI = null");
            LogWarning("クリック売却：Player Inventory Grid UIが未設定です。");
            return false;
        }

        if (sellCartGridUI == null)
        {
            QuickSellDiagnostic("[STOP] Sell Cart Grid UI = null");
            LogWarning("クリック売却：Sell Cart Grid UIが未設定です。");
            return false;
        }

        if (sellCart == null)
        {
            QuickSellDiagnostic("[STOP] SellCartInventory = null");
            LogWarning("クリック売却：SellCartInventoryが見つかりません。");
            return false;
        }

        if (sellCart.CartInventory == null)
        {
            QuickSellDiagnostic("[STOP] SellCart.CartInventory = null");
            LogWarning("クリック売却：SellCartのItemBoxInventoryが見つかりません。");
            return false;
        }

        if (sellCart.CartInventory.Grid == null)
        {
            QuickSellDiagnostic("[STOP] SellCart.CartInventory.Grid = null");
            LogWarning("クリック売却：SellCart Gridが初期化されていません。");
            return false;
        }

        bool playerContainsItem =
            playerInventoryGridUI.ContainsItem(item);

        QuickSellDiagnostic(
            $"[STEP 3] PlayerGrid.ContainsItem={playerContainsItem} / " +
            $"GridIsPlayer={playerInventoryGridUI.IsPlayerInventory}"
        );

        if (!playerContainsItem)
        {
            QuickSellDiagnostic(
                "[STOP] PawnShopUIControllerに設定されているPlayer Inventory Grid UIが、" +
                "クリックしたInventoryItemを保持していません。別のGrid UIを参照している可能性があります。"
            );
            return false;
        }

        bool canAccept =
            sellCart.CanAcceptItem(
                item,
                out string rejectionReason
            );

        QuickSellDiagnostic(
            $"[STEP 4] SellCart.CanAcceptItem={canAccept} / " +
            $"Reason={(string.IsNullOrWhiteSpace(rejectionReason) ? "(なし)" : rejectionReason)} / " +
            $"Merchant={(sellCart.CurrentMerchantStock != null ? sellCart.CurrentMerchantStock.ShopName : "null")}"
        );

        if (!canAccept)
        {
            sellCart.ReportRejectedTransfer(
                item,
                rejectionReason
            );

            QuickSellDiagnostic(
                "[STOP] 商人の買取条件または共通売却条件で拒否されました。"
            );
            return false;
        }

        bool foundSpace =
            sellCart.CartInventory.Grid.FindSpaceForItem(
                item.ItemData,
                out Vector2Int position,
                out bool isRotated
            );

        QuickSellDiagnostic(
            $"[STEP 5] FindSpaceForItem={foundSpace} / " +
            $"Position={position.x},{position.y} / Rotated={isRotated}"
        );

        if (!foundSpace)
        {
            QuickSellDiagnostic(
                "[STOP] SellCartに配置可能な空きマスがありません。"
            );

            LogWarning(
                $"売却カートに空きがありません。Item={item.ItemData.DisplayName}"
            );
            return false;
        }

        QuickSellDiagnostic(
            $"[STEP 6] TryTransferItemTo実行 / " +
            $"SourceGrid={GetTransformPath(playerInventoryGridUI.transform)} / " +
            $"TargetGrid={GetTransformPath(sellCartGridUI.transform)}"
        );

        bool moved =
            playerInventoryGridUI.TryTransferItemTo(
                item,
                sellCartGridUI,
                position.x,
                position.y,
                isRotated
            );

        QuickSellDiagnostic(
            $"[STEP 7] TryTransferItemTo結果={moved} / " +
            $"PlayerContainsAfter={playerInventoryGridUI.ContainsItem(item)} / " +
            $"SellCartContainsAfter={sellCartGridUI.ContainsItem(item)}"
        );

        if (!moved)
        {
            QuickSellDiagnostic(
                "[STOP] TryTransferItemTo=false。 " +
                "AllowsDirectTransfer、移動先GridのBinding、CanPlaceItem、またはRemove/Move処理を確認してください。"
            );

            LogWarning(
                $"クリックで売却カートへ移動できませんでした。" +
                $"Item={item.ItemData.DisplayName}"
            );
            return false;
        }

        InventoryItemTooltipUI.HideFor(item);

        BindAndRefreshSellGridUIs();
        transactionController?.RefreshUI();

        int total =
            transactionController != null
                ? transactionController.CurrentCheckoutTotal
                : -1;

        QuickSellDiagnostic(
            $"[SUCCESS] クリック売却カート追加成功 / " +
            $"Item={item.ItemData.DisplayName} / " +
            $"位置={position.x},{position.y} / " +
            $"売却合計={total}"
        );

        Log(
            $"クリックで売却カートへ追加：" +
            $"{item.ItemData.DisplayName} / " +
            $"位置={position.x},{position.y}"
        );

        return true;
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

        CaptureTownInventory();

        merchantPurchaseController?.CloseShop();
        weaponRepairController?.CloseRepairShop();
        transactionController?.SetMerchantStock(null);
        sellCart?.ClearMerchantStock();
        MerchantShopConversationContext.Clear();

        purchaseAvailable = false;
        isRepairMode = false;
        isSellTabActive = false;
        isOpen = false;
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
        if (!captureInventoryWhenClosing ||
            townPlayerInventory == null)
        {
            return;
        }

        PlayerInventorySessionBridge bridge =
            townPlayerInventory.SessionBridge;

        bridge?.CaptureToSession();
    }

    private void BindAndRefreshSellGridUIs()
    {
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
                FindAnyObjectByType<TownPlayerInventoryController>(
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

    private void QuickSellDiagnostic(
        string message)
    {
        if (!showQuickSellDiagnostics)
        {
            return;
        }

        Debug.Log(
            $"[クリック売却診断][PawnShop] {message}",
            this
        );
    }

    private static string GetTransformPath(
        Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
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
