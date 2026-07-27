using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 質屋へ売る予定のアイテムを一時的に置くためのカートです。
/// 同じGameObjectに ItemBoxInventory を付け、Box Kind を Storage、
/// Allow Direct Item Transfer を ON にして使用します。
///
/// ここへ入れた時点では売却は成立しません。
/// ShopSellTransactionController の会計処理が完了した時だけ、
/// アイテムがカートから取り除かれ、所持金が増えます。
///
/// v2.3では、現在開いているMerchantStockInventoryの買取条件も判定します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemBoxInventory))]
public class SellCartInventory : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("同じObjectのItemBoxInventoryを使います。未設定なら自動取得します")]
    [SerializeField] private ItemBoxInventory cartInventory;

    [Header("売却ルール")]
    [Tooltip("Sell Priceが0のアイテムも会計対象にするかどうか。通常はオフがおすすめです")]
    [SerializeField] private bool allowZeroValueItems;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public ItemBoxInventory CartInventory => cartInventory;
    public MerchantStockInventory CurrentMerchantStock =>
        currentMerchantStock;

    public bool HasItems => cartInventory != null &&
                            cartInventory.Grid != null &&
                            cartInventory.Grid.Items.Count > 0;

    public int ItemEntryCount => cartInventory != null &&
                                 cartInventory.Grid != null
        ? cartInventory.Grid.Items.Count
        : 0;

    /// <summary>
    /// カートの内容が変わった時に通知します。
    /// </summary>
    public event Action CartChanged;

    /// <summary>
    /// 商人の買取条件によって、カートへの移動を拒否した時に通知します。
    /// </summary>
    public event Action<InventoryItem, string> SellTransferRejected;

    private bool isSubscribed;
    private MerchantStockInventory currentMerchantStock;

    private void Awake()
    {
        FindReferences();
        cartInventory?.InitializeInventory();
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    /// <summary>
    /// 店を開いた時に、その商人の買取条件をカートへ設定します。
    /// nullの場合は商人固有のカテゴリー制限を行いません。
    /// </summary>
    public void SetMerchantStock(
        MerchantStockInventory merchantStock)
    {
        currentMerchantStock = merchantStock;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[SellCartInventory] 現在の商人=" +
                $"{(currentMerchantStock != null ? currentMerchantStock.ShopName : "未設定")}",
                this
            );
        }

        CartChanged?.Invoke();
    }

    public void ClearMerchantStock()
    {
        SetMerchantStock(null);
    }

    /// <summary>
    /// カートの内容を安全にコピーして返します。
    /// 会計・返却処理では、このリストを使ってください。
    /// </summary>
    public List<InventoryItem> GetItemsSnapshot()
    {
        FindReferences();
        cartInventory?.InitializeInventory();

        return cartInventory != null && cartInventory.Grid != null
            ? new List<InventoryItem>(cartInventory.Grid.Items)
            : new List<InventoryItem>();
    }

    /// <summary>
    /// プレイヤーInventoryから売却カートへ入れてよいか確認します。
    /// 共通売却条件と、現在の商人の買取条件を両方確認します。
    /// </summary>
    public bool CanAcceptItem(
        InventoryItem item,
        out string reason)
    {
        if (!CanSellByCommonRules(item, out reason))
        {
            return false;
        }

        if (currentMerchantStock != null &&
            !currentMerchantStock.CanBuyFromPlayer(
                item.ItemData,
                out reason))
        {
            return false;
        }

        return true;
    }

    /// <summary>
    /// このアイテムを会計できるか確認します。
    /// 売れない場合は reason に理由が入ります。
    /// </summary>
    public bool CanSell(
        InventoryItem item,
        out string reason)
    {
        return CanAcceptItem(item, out reason);
    }

    /// <summary>
    /// InventoryGridUIが、カートへの移動を拒否した理由をUIへ通知するために使います。
    /// </summary>
    public void ReportRejectedTransfer(
        InventoryItem item,
        string reason)
    {
        string resolvedReason = string.IsNullOrWhiteSpace(reason)
            ? "このアイテムは、この商人には売却できません。"
            : reason;

        if (showDebugLogs)
        {
            string itemName = item != null && item.ItemData != null
                ? item.ItemData.DisplayName
                : "不明なアイテム";

            Debug.LogWarning(
                $"[SellCartInventory] 売却カートへの移動を拒否: " +
                $"Item={itemName} / Reason={resolvedReason}",
                this
            );
        }

        SellTransferRejected?.Invoke(item, resolvedReason);
    }

    /// <summary>
    /// ItemDataのSell PriceとItemBoxInventoryのSell Price Multiplierを反映した、
    /// 1個あたりの売却価格を返します。
    /// </summary>
    public int GetUnitSellPrice(InventoryItem item)
    {
        if (item == null || item.ItemData == null)
        {
            return 0;
        }

        FindReferences();

        return cartInventory != null
            ? Mathf.Max(0, cartInventory.GetSellPrice(item.ItemData))
            : Mathf.Max(0, item.ItemData.SellPrice);
    }

    /// <summary>
    /// スタック数を含めた、そのアイテム枠全体の売却価格を返します。
    /// </summary>
    public int GetTotalSellPrice(InventoryItem item)
    {
        if (item == null || item.ItemData == null || item.Amount <= 0)
        {
            return 0;
        }

        long total = (long)GetUnitSellPrice(item) * item.Amount;

        return total > int.MaxValue
            ? int.MaxValue
            : Mathf.Max(0, (int)total);
    }

    /// <summary>
    /// 現在のカートで会計可能な合計金額を返します。
    /// blockedEntryCount は売却不可のアイテム枠数です。
    /// </summary>
    public int GetCheckoutTotal(
        out int sellableEntryCount,
        out int blockedEntryCount)
    {
        sellableEntryCount = 0;
        blockedEntryCount = 0;

        long total = 0;

        foreach (InventoryItem item in GetItemsSnapshot())
        {
            if (!CanSell(item, out _))
            {
                blockedEntryCount++;
                continue;
            }

            sellableEntryCount++;
            total += GetTotalSellPrice(item);

            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return Mathf.Max(0, (int)total);
    }

    /// <summary>
    /// カートから指定アイテムを取り除きます。
    /// 通常はShopSellTransactionControllerの会計・返却処理から使います。
    /// </summary>
    public bool RemoveFromCart(InventoryItem item)
    {
        FindReferences();

        return cartInventory != null &&
               cartInventory.RemoveItem(item);
    }

    private bool CanSellByCommonRules(
        InventoryItem item,
        out string reason)
    {
        reason = string.Empty;

        if (item == null || item.ItemData == null || item.Amount <= 0)
        {
            reason = "アイテムデータが無効です。";
            return false;
        }

        if (item.ItemData.ItemType == InventoryItemType.Quest)
        {
            reason = "クエストアイテムは売却できません。";
            return false;
        }

        if (!item.ItemData.CanSellToShop)
        {
            reason = "このアイテムは売却できません。";
            return false;
        }

        int unitPrice = GetUnitSellPrice(item);

        if (!allowZeroValueItems && unitPrice <= 0)
        {
            reason = "このアイテムには売却価格が設定されていません。";
            return false;
        }

        return true;
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || cartInventory == null)
        {
            return;
        }

        cartInventory.OnInventoryChanged += HandleCartChanged;
        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || cartInventory == null)
        {
            return;
        }

        cartInventory.OnInventoryChanged -= HandleCartChanged;
        isSubscribed = false;
    }

    private void HandleCartChanged()
    {
        CartChanged?.Invoke();

        if (showDebugLogs)
        {
            Debug.Log(
                $"[SellCartInventory] カート更新。現在={ItemEntryCount}枠",
                this
            );
        }
    }

    private void FindReferences()
    {
        if (cartInventory == null)
        {
            cartInventory = GetComponent<ItemBoxInventory>();
        }
    }
}
