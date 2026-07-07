using UnityEngine;

/// <summary>
/// 質屋パネルの開閉と、売却予定カートの安全なキャンセル処理を管理します。
/// このコンポーネントは、最初から有効な TownCanvas / PawnShopSystem などへ付け、
/// PawnShopPanel 自体は別Objectとして参照してください。
/// </summary>
[DisallowMultipleComponent]
public class PawnShopUIController : MonoBehaviour
{
    [Header("パネル")]
    [Tooltip("質屋画面全体のPanel。開始時は非表示にしてOKです")]
    [SerializeField] private GameObject pawnShopPanel;

    [Header("参照")]
    [SerializeField] private SellCartInventory sellCart;
    [SerializeField] private ShopSellTransactionController transactionController;
    [SerializeField] private TownPlayerInventoryController townPlayerInventory;

    [Header("UIグリッド")]
    [Tooltip("TownPlayerInventoryのInventoryControllerを表示するGrid UI")]
    [SerializeField] private InventoryGridUI playerInventoryGridUI;

    [Tooltip("SellCartInventoryのItemBoxInventoryを表示するGrid UI")]
    [SerializeField] private InventoryGridUI sellCartGridUI;

    [Header("動作")]
    [SerializeField] private bool hidePanelOnAwake = true;

    [Tooltip("閉じる時やTown_Mainを離れる時、会計前のアイテムをプレイヤーへ戻します")]
    [SerializeField] private bool returnCartItemsWhenClosing = true;

    [Tooltip("閉じる時に、現在のプレイヤーインベントリをGameSessionManagerへ保存します")]
    [SerializeField] private bool captureInventoryWhenClosing = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public bool IsOpen => isOpen;

    private bool isOpen;
    private bool isApplicationQuitting;

    private void Awake()
    {
        FindReferences();

        if (hidePanelOnAwake)
        {
            SetPanelVisible(false);
        }
    }

    private void OnDestroy()
    {
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
    /// 建物クリック用ButtonのOnClickから呼びます。
    /// </summary>
    public void OpenPawnShop()
    {
        FindReferences();

        if (pawnShopPanel == null)
        {
            LogWarning("Pawn Shop Panel が未設定です。");
            return;
        }

        // 前回、何らかの理由でカートに残ったアイテムがある場合は、
        // 先に返却を試みます。返却できない時はパネルを開いて手動で戻せるようにします。
        if (sellCart != null && sellCart.HasItems &&
            transactionController != null)
        {
            bool returned = transactionController.ReturnAllItemsToPlayer();

            if (!returned)
            {
                LogWarning(
                    "前回の売却予定アイテムをすべて戻せませんでした。" +
                    "質屋画面を開くので、プレイヤーインベントリの空きを作ってください。"
                );
            }
        }

        SetPanelVisible(true);
        isOpen = true;

        BindAndRefreshGridUIs();
        transactionController?.RefreshUI();

        Log("質屋画面を開きました。");
    }

    /// <summary>
    /// CloseButtonのOnClickから呼びます。
    /// 会計前のカート内容をプレイヤーへ戻せない時は、アイテム消失防止のため閉じません。
    /// </summary>
    public void ClosePawnShop()
    {
        TryClosePawnShop();
    }

    /// <summary>
    /// 質屋画面を閉じられた時だけtrueを返します。
    /// SceneTransitionButtonなど、シーン移動前に安全確認したい処理から使います。
    /// </summary>
    public bool TryClosePawnShop()
    {
        if (!isOpen && pawnShopPanel != null && !pawnShopPanel.activeSelf)
        {
            return true;
        }

        if (!TryReturnCartBeforeLeaving())
        {
            return false;
        }

        CaptureTownInventory();

        isOpen = false;
        SetPanelVisible(false);

        Log("質屋画面を閉じました。");
        return true;
    }

    /// <summary>
    /// CheckoutButtonのOnClickに直接登録したい場合にも使える中継メソッドです。
    /// </summary>
    public void Checkout()
    {
        FindReferences();

        if (transactionController == null)
        {
            LogWarning("ShopSellTransactionController が未設定です。");
            return;
        }

        if (transactionController.Checkout())
        {
            CaptureTownInventory();
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
                "ShopSellTransactionController が未設定のため戻せません。"
            );
            return false;
        }

        bool returned = transactionController.ReturnAllItemsToPlayer();

        if (!returned)
        {
            LogWarning(
                "売却予定のアイテムをすべてプレイヤーへ戻せないため、" +
                "アイテム消失防止のため質屋画面を閉じません。"
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

        if (bridge != null)
        {
            bridge.CaptureToSession();
        }
    }

    private void BindAndRefreshGridUIs()
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

    private void SetPanelVisible(bool visible)
    {
        if (pawnShopPanel == null)
        {
            return;
        }

        if (pawnShopPanel.activeSelf != visible)
        {
            pawnShopPanel.SetActive(visible);
        }
    }

    private void FindReferences()
    {
        if (sellCart == null)
        {
            sellCart = FindAnyObjectByType<SellCartInventory>();
        }

        if (transactionController == null)
        {
            transactionController =
                FindAnyObjectByType<ShopSellTransactionController>();
        }

        if (townPlayerInventory == null)
        {
            townPlayerInventory =
                FindAnyObjectByType<TownPlayerInventoryController>();
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[PawnShopUIController] {message}", this);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[PawnShopUIController] {message}", this);
    }
}
