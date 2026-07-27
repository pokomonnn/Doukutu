using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 質屋の売却予定カートを会計し、プレイヤーの所持金へ反映します。
/// 会計前に閉じた場合は、カート内のアイテムをプレイヤーのインベントリへ戻せます。
/// </summary>
[DisallowMultipleComponent]
public class ShopSellTransactionController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private SellCartInventory sellCart;

    [Tooltip("Town_MainのTownPlayerInventoryを設定します。未設定ならシーン内から探します")]
    [SerializeField] private TownPlayerInventoryController townPlayerInventory;

    [Tooltip("未設定ならGameSessionManager.Instanceを使います")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("UI")]
    [SerializeField] private TMP_Text totalPriceText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button checkoutButton;

    [Tooltip("{0}へ売却合計金額が入ります")]
    [SerializeField] private string totalPriceFormat = "売却合計：¥{0:N0}";

    [Tooltip("売却可能なアイテムがない時に表示する文言")]
    [SerializeField] private string emptyCartMessage = "売却するアイテムを入れてください。";

    [Header("売却成功サウンド")]
    [Tooltip("売却成功時の効果音を再生するAudioSourceです。未設定なら同じGameObjectから探します。")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("会計が正常に完了した時だけ再生する効果音です。")]
    [SerializeField] private AudioClip sellSuccessClip;

    [SerializeField, Range(0f, 1f)]
    private float sellSoundVolume = 1f;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public int CurrentCheckoutTotal { get; private set; }
    public bool HasSellableItems => CurrentCheckoutTotal > 0;
    public string LastMessage { get; private set; } = string.Empty;
    public MerchantStockInventory CurrentMerchantStock =>
        currentMerchantStock;

    public event Action<int> CheckoutCompleted;
    public event Action CartReturned;

    private bool isSubscribed;
    private MerchantStockInventory currentMerchantStock;

    private readonly struct SellEntry
    {
        public readonly InventoryItem Item;
        public readonly int Price;

        public SellEntry(InventoryItem item, int price)
        {
            Item = item;
            Price = price;
        }
    }

    private void Awake()
    {
        FindReferences();
        SetupButton();
        RefreshUI();
    }

    private void OnEnable()
    {
        FindReferences();
        SetupButton();
        SubscribeEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        RemoveButtonListener();
    }

    /// <summary>
    /// 店を開いた時に、現在の商人を設定します。
    /// SellCartInventoryにも同じ商人を渡し、ドラッグ時と会計時の両方で
    /// 商人ごとの買取条件を確認できるようにします。
    /// </summary>
    public void SetMerchantStock(
        MerchantStockInventory merchantStock)
    {
        FindReferences();

        currentMerchantStock = merchantStock;
        sellCart?.SetMerchantStock(merchantStock);

        LastMessage = string.Empty;
        RefreshUI();

        if (showDebugLogs)
        {
            Log(
                "現在の買取商人を設定しました: " +
                (currentMerchantStock != null
                    ? currentMerchantStock.ShopName
                    : "未設定")
            );
        }
    }

    /// <summary>
    /// CheckoutButtonのOnClickへ登録する会計処理です。
    /// 売却できないアイテムはカートに残し、売却可能なアイテムだけを会計します。
    /// </summary>
    public bool Checkout()
    {
        FindReferences();

        if (sellCart == null || sellCart.CartInventory == null)
        {
            SetMessage("売却カートが見つかりません。");
            return false;
        }

        if (gameSessionManager == null)
        {
            SetMessage("GameSessionManagerが見つかりません。所持金を更新できません。");
            return false;
        }

        List<SellEntry> sellEntries = BuildSellEntries(
            out int blockedEntryCount
        );

        if (sellEntries.Count == 0)
        {
            SetMessage(
                blockedEntryCount > 0
                    ? "売却できるアイテムがありません。"
                    : emptyCartMessage
            );
            return false;
        }

        long totalLong = 0;

        foreach (SellEntry entry in sellEntries)
        {
            totalLong += entry.Price;

            if (totalLong >= int.MaxValue)
            {
                totalLong = int.MaxValue;
                break;
            }
        }

        int total = Mathf.Max(0, (int)totalLong);

        if (total <= 0)
        {
            SetMessage("売却額が0円のため、会計できません。");
            return false;
        }

        int removedEntryCount = 0;
        int removedItemCount = 0;
        int removedPrice = 0;

        foreach (SellEntry entry in sellEntries)
        {
            InventoryItem item = entry.Item;

            if (item == null || !sellCart.RemoveFromCart(item))
            {
                LogWarning(
                    "会計中に売却予定アイテムをカートから取り除けませんでした。" +
                    "取り除けた分だけを会計します。"
                );
                continue;
            }

            removedEntryCount++;
            removedItemCount += Mathf.Max(0, item.Amount);

            long nextPrice = (long)removedPrice + entry.Price;
            removedPrice = nextPrice > int.MaxValue
                ? int.MaxValue
                : (int)nextPrice;
        }

        if (removedEntryCount == 0 || removedPrice <= 0)
        {
            SetMessage("会計に失敗しました。売却予定アイテムはカートに残っています。");
            return false;
        }

        if (!gameSessionManager.AddMoney(removedPrice))
        {
            SetMessage("所持金の更新に失敗しました。Consoleを確認してください。");
            return false;
        }

        CapturePlayerInventoryToSession();
        PlaySellSuccessSound();

        string message =
            $"{removedItemCount}個を売却しました。+¥{removedPrice:N0}";

        if (blockedEntryCount > 0)
        {
            message +=
                $" 売却できないアイテム{blockedEntryCount}枠はカートに残っています。";
        }

        SetMessage(message);
        CheckoutCompleted?.Invoke(removedPrice);

        Log(message);
        RefreshUI();
        return true;
    }

    /// <summary>
    /// カート内のアイテムを、プレイヤーの通常インベントリへ安全に戻します。
    /// 空きが足りない場合は戻せないアイテムをカートに残し、falseを返します。
    /// </summary>
    public bool ReturnAllItemsToPlayer()
    {
        FindReferences();

        if (sellCart == null || sellCart.CartInventory == null)
        {
            SetMessage("売却カートが見つかりません。");
            return false;
        }

        if (townPlayerInventory == null ||
            townPlayerInventory.InventoryController == null)
        {
            SetMessage("プレイヤーインベントリが見つかりません。アイテムを戻せません。");
            return false;
        }

        List<InventoryItem> items = sellCart.GetItemsSnapshot();

        if (items.Count == 0)
        {
            LastMessage = string.Empty;
            RefreshUI();
            return true;
        }

        InventoryController playerInventory =
            townPlayerInventory.InventoryController;

        int returnedEntryCount = 0;
        int failedEntryCount = 0;

        foreach (InventoryItem item in items)
        {
            if (item == null || item.ItemData == null)
            {
                failedEntryCount++;
                continue;
            }

            int sourceX = item.GridX;
            int sourceY = item.GridY;
            bool sourceRotation = item.IsRotated;

            if (!playerInventory.Grid.FindSpaceForItem(
                    item.ItemData,
                    out Vector2Int position,
                    out bool isRotated))
            {
                failedEntryCount++;
                continue;
            }

            if (!sellCart.RemoveFromCart(item))
            {
                failedEntryCount++;
                continue;
            }

            bool moved = playerInventory.TryMoveItem(
                item,
                position.x,
                position.y,
                isRotated
            );

            if (moved)
            {
                returnedEntryCount++;
                continue;
            }

            bool restoredToCart = sellCart.CartInventory.TryMoveItem(
                item,
                sourceX,
                sourceY,
                sourceRotation
            );

            if (!restoredToCart)
            {
                LogWarning(
                    $"{item.ItemData.DisplayName} をプレイヤーにもカートにも戻せませんでした。" +
                    "Consoleを確認してください。"
                );
            }

            failedEntryCount++;
        }

        if (returnedEntryCount > 0)
        {
            CapturePlayerInventoryToSession();
            CartReturned?.Invoke();
        }

        if (failedEntryCount > 0)
        {
            SetMessage(
                $"{failedEntryCount}枠を戻せませんでした。" +
                "プレイヤーインベントリの空きを作ってから、もう一度閉じてください。"
            );
            return false;
        }

        SetMessage(returnedEntryCount > 0
            ? "売却予定のアイテムをすべて戻しました。"
            : string.Empty
        );

        RefreshUI();
        return true;
    }

    public bool CancelAndReturnAllItems()
    {
        return ReturnAllItemsToPlayer();
    }

    public void RefreshUI()
    {
        FindReferences();

        if (sellCart == null)
        {
            CurrentCheckoutTotal = 0;
            SetTotalText(0);

            if (checkoutButton != null)
            {
                checkoutButton.interactable = false;
            }

            return;
        }

        CurrentCheckoutTotal = sellCart.GetCheckoutTotal(
            out int sellableEntryCount,
            out int blockedEntryCount
        );

        SetTotalText(CurrentCheckoutTotal);

        if (checkoutButton != null)
        {
            checkoutButton.interactable =
                sellableEntryCount > 0 &&
                CurrentCheckoutTotal > 0;
        }

        if (string.IsNullOrWhiteSpace(LastMessage) &&
            statusText != null &&
            blockedEntryCount > 0)
        {
            string merchantName = currentMerchantStock != null
                ? currentMerchantStock.ShopName
                : "この商人";

            statusText.text =
                $"{merchantName}に売却できないアイテムが" +
                $"{blockedEntryCount}枠あります。";
        }
        else if (string.IsNullOrWhiteSpace(LastMessage) &&
                 statusText != null &&
                 sellableEntryCount == 0)
        {
            statusText.text = emptyCartMessage;
        }
    }

    private List<SellEntry> BuildSellEntries(
        out int blockedEntryCount)
    {
        blockedEntryCount = 0;
        List<SellEntry> entries = new List<SellEntry>();

        if (sellCart == null)
        {
            return entries;
        }

        foreach (InventoryItem item in sellCart.GetItemsSnapshot())
        {
            if (!sellCart.CanSell(item, out _))
            {
                blockedEntryCount++;
                continue;
            }

            int price = sellCart.GetTotalSellPrice(item);

            if (price <= 0)
            {
                blockedEntryCount++;
                continue;
            }

            entries.Add(new SellEntry(item, price));
        }

        return entries;
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || sellCart == null)
        {
            return;
        }

        sellCart.CartChanged += HandleCartChanged;
        sellCart.SellTransferRejected += HandleSellTransferRejected;
        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || sellCart == null)
        {
            return;
        }

        sellCart.CartChanged -= HandleCartChanged;
        sellCart.SellTransferRejected -= HandleSellTransferRejected;
        isSubscribed = false;
    }

    private void HandleCartChanged()
    {
        LastMessage = string.Empty;
        RefreshUI();
    }

    private void HandleSellTransferRejected(
        InventoryItem item,
        string reason)
    {
        string resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? "このアイテムは、この商人には売却できません。"
            : reason;

        SetMessage(resolvedReason);
    }

    private void SetupButton()
    {
        if (checkoutButton == null)
        {
            return;
        }

        checkoutButton.onClick.RemoveListener(HandleCheckoutButtonClicked);
        checkoutButton.onClick.AddListener(HandleCheckoutButtonClicked);
    }

    private void RemoveButtonListener()
    {
        if (checkoutButton != null)
        {
            checkoutButton.onClick.RemoveListener(HandleCheckoutButtonClicked);
        }
    }

    private void HandleCheckoutButtonClicked()
    {
        Checkout();
    }

    private void SetTotalText(int total)
    {
        if (totalPriceText != null)
        {
            totalPriceText.text = string.Format(
                totalPriceFormat,
                Mathf.Max(0, total)
            );
        }
    }

    private void SetMessage(string message)
    {
        LastMessage = message ?? string.Empty;

        if (statusText != null)
        {
            statusText.text = LastMessage;
        }

        if (!string.IsNullOrWhiteSpace(LastMessage))
        {
            Log(LastMessage);
        }

        RefreshUI();
    }

    private void PlaySellSuccessSound()
    {
        if (sellSuccessClip == null)
        {
            return;
        }

        if (audioSource == null)
        {
            LogWarning(
                "Sell Success Clipは設定されていますが、" +
                "AudioSourceが見つからないため再生できません。"
            );
            return;
        }

        audioSource.PlayOneShot(
            sellSuccessClip,
            Mathf.Clamp01(sellSoundVolume)
        );
    }

    private void CapturePlayerInventoryToSession()
    {
        if (townPlayerInventory == null)
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

        if (sellCart == null)
        {
            sellCart = FindAnyObjectByType<SellCartInventory>();
        }

        if (townPlayerInventory == null)
        {
            townPlayerInventory =
                FindAnyObjectByType<TownPlayerInventoryController>();
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>();
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[ShopSellTransactionController] {message}", this);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[ShopSellTransactionController] {message}", this);
    }
}
