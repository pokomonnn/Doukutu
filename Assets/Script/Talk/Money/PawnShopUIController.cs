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

    [Header("売却システム")]
    [SerializeField] private SellCartInventory sellCart;
    [SerializeField] private ShopSellTransactionController transactionController;
    [SerializeField] private TownPlayerInventoryController townPlayerInventory;

    [Header("購入システム")]
    [SerializeField] private MerchantPurchaseController merchantPurchaseController;

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

    public bool IsOpen => isOpen;
    public bool IsPurchaseAvailable => purchaseAvailable;

    private bool isOpen;
    private bool purchaseAvailable;
    private bool isApplicationQuitting;

    private void Awake()
    {
        FindReferences();
        SetupTabButtons();

        if (hidePanelOnAwake)
        {
            merchantPurchaseController?.CloseShop();
            SetPanelVisible(false);
        }
    }

    private void OnDestroy()
    {
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

        ReturnOldSellCartIfNeeded();

        SetPanelVisible(true);
        isOpen = true;

        MerchantStockInventory stock =
            MerchantShopConversationContext.CurrentStock;

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

    public void ShowPurchaseTab()
    {
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

        CaptureTownInventory();

        merchantPurchaseController?.CloseShop();
        transactionController?.SetMerchantStock(null);
        sellCart?.ClearMerchantStock();
        MerchantShopConversationContext.Clear();

        purchaseAvailable = false;
        isOpen = false;
        SetPanelVisible(false);

        Log("購入・売却画面を閉じました。");
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
