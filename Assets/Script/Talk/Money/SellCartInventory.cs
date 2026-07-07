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

    private bool isSubscribed;

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
    /// このアイテムを売却できるか確認します。
    /// 売れない場合は reason に理由が入ります。
    /// </summary>
    public bool CanSell(
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
