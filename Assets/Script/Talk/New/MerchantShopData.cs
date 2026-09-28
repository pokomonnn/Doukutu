using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商人1店舗分のマスターデータです。
///
/// このAssetだけで、
/// ・店舗ID / 店舗名
/// ・Lv1販売商品
/// ・施設レベル / レベルアップ条件
/// ・Lv2以降の解禁商品
/// ・プレイヤーから買い取るItemType / 個別許可 / 個別拒否
/// ・購入画面の最大購入数
/// ・店を開いた時の在庫リセット
/// をまとめて管理します。
///
/// 実際の商品を格納するInventoryGridはMerchantStockInventory + ItemBoxInventoryが担当します。
/// </summary>
[CreateAssetMenu(
    fileName = "NewMerchantShop",
    menuName = "Town/Merchant/Shop Data"
)]
public class MerchantShopData : TownFacilityUpgradeData
{
    [Header("Lv1：最初から販売する商品")]
    [Tooltip(
        "この店がLv1から販売する商品です。\n" +
        "MerchantStockInventoryが店を準備する時に、ItemBoxInventoryへ自動で反映します。\n" +
        "ItemBoxInventory側のStarting Itemsへ同じ商品を設定する必要はありません。"
    )]
    [SerializeField]
    private List<MerchantShopItemEntry> items =
        new List<MerchantShopItemEntry>();

    [Header("在庫・購入設定")]
    [Tooltip(
        "ONなら店を開くたびにMerchantShopDataから在庫を作り直します。\n" +
        "OFFなら同じScene中は購入後の残り在庫を維持します。"
    )]
    [SerializeField] private bool resetStockEachOpen;

    [Tooltip("購入画面で一度に指定できる最大個数です。在庫数より多くは指定できません。")]
    [SerializeField, Min(1)] private int maxPurchaseAmount = 999;

    [Header("プレイヤーからの買取設定")]
    [Tooltip(
        "ONなら、ItemData側で売却可能な全カテゴリーをこの商人が買い取ります。\n" +
        "OFFならAccepted Sell Item TypesまたはAccepted Specific Itemsに登録した物だけを買い取ります。"
    )]
    [SerializeField] private bool allowAllSellItemTypes = true;

    [Tooltip("Allow All Sell Item TypesがOFFの時、この商人が買い取るItemTypeです。複数指定できます。")]
    [SerializeField]
    private List<InventoryItemType> acceptedSellItemTypes =
        new List<InventoryItemType>();

    [Tooltip("ItemTypeに関係なく、この商人が特別に買い取るItemDataです。Rejected Specific Itemsの方が優先されます。")]
    [SerializeField]
    private List<ItemData> acceptedSpecificItems =
        new List<ItemData>();

    [Tooltip("カテゴリーや個別許可に関係なく、この商人が絶対に買い取らないItemDataです。")]
    [SerializeField]
    private List<ItemData> rejectedSpecificItems =
        new List<ItemData>();

    [Tooltip("買取不可時の基本文言です。{0}=店舗名、{1}=アイテム名、{2}=ItemType")]
    [SerializeField]
    private string rejectedItemMessageFormat =
        "{0}では「{1}」を買い取りません。";

    [Header("旧方式との互換用")]
    [Tooltip(
        "旧MerchantShopUIController用です。現在のItemBoxInventory方式では通常使用しません。"
    )]
    [SerializeField, Min(0f)] private float purchasePriceMultiplier = 1f;

    // 店舗ID・店舗名は施設ID・施設名と完全共通です。
    public string ShopId => FacilityId;
    public string ShopName => FacilityName;

    public bool ResetStockEachOpen => resetStockEachOpen;
    public int MaxPurchaseAmount => Mathf.Max(1, maxPurchaseAmount);

    public bool AllowAllSellItemTypes => allowAllSellItemTypes;
    public IReadOnlyList<InventoryItemType> AcceptedSellItemTypes =>
        acceptedSellItemTypes;
    public IReadOnlyList<ItemData> AcceptedSpecificItems =>
        acceptedSpecificItems;
    public IReadOnlyList<ItemData> RejectedSpecificItems =>
        rejectedSpecificItems;

    public IReadOnlyList<MerchantShopItemEntry> Items => items;

    // 旧コード互換用。
    public float PurchasePriceMultiplier =>
        Mathf.Max(0f, purchasePriceMultiplier);

    /// <summary>
    /// この商人がプレイヤーから指定ItemDataを買い取れるか判定します。
    /// ItemData共通のCanSellToShopや売却価格0判定は既存の売却側Controllerに任せます。
    /// </summary>
    public bool CanBuyFromPlayer(
        ItemData itemData,
        out string rejectionReason)
    {
        rejectionReason = string.Empty;

        if (itemData == null)
        {
            rejectionReason = "アイテムデータが無効です。";
            return false;
        }

        if (ContainsItem(rejectedSpecificItems, itemData))
        {
            rejectionReason = BuildRejectionMessage(itemData);
            return false;
        }

        if (ContainsItem(acceptedSpecificItems, itemData))
        {
            return true;
        }

        if (allowAllSellItemTypes)
        {
            return true;
        }

        if (acceptedSellItemTypes != null &&
            acceptedSellItemTypes.Contains(itemData.ItemType))
        {
            return true;
        }

        rejectionReason = BuildRejectionMessage(itemData);
        return false;
    }

    public int GetUnitPrice(MerchantShopItemEntry entry)
    {
        if (entry == null || entry.ItemData == null)
        {
            return 0;
        }

        if (entry.UnitPriceOverride >= 0)
        {
            return entry.UnitPriceOverride;
        }

        return Mathf.Max(
            0,
            Mathf.CeilToInt(
                entry.ItemData.PurchasePrice *
                PurchasePriceMultiplier
            )
        );
    }

    public int GetTotalPrice(
        MerchantShopItemEntry entry,
        int amount)
    {
        if (entry == null || amount <= 0)
        {
            return 0;
        }

        long total = (long)GetUnitPrice(entry) * amount;

        return total > int.MaxValue
            ? int.MaxValue
            : Mathf.Max(0, (int)total);
    }

    private string BuildRejectionMessage(ItemData itemData)
    {
        string itemName = itemData != null
            ? itemData.DisplayName
            : "不明なアイテム";

        string itemType = itemData != null
            ? itemData.ItemType.ToString()
            : "Unknown";

        if (string.IsNullOrWhiteSpace(rejectedItemMessageFormat))
        {
            return $"{ShopName}では「{itemName}」を買い取りません。";
        }

        try
        {
            return string.Format(
                rejectedItemMessageFormat,
                ShopName,
                itemName,
                itemType
            );
        }
        catch (FormatException)
        {
            return $"{ShopName}では「{itemName}」を買い取りません。";
        }
    }

    private static bool ContainsItem(
        List<ItemData> list,
        ItemData target)
    {
        if (list == null || target == null)
        {
            return false;
        }

        foreach (ItemData item in list)
        {
            if (item == target)
            {
                return true;
            }
        }

        return false;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        maxPurchaseAmount = Mathf.Max(1, maxPurchaseAmount);
        purchasePriceMultiplier = Mathf.Max(0f, purchasePriceMultiplier);
        rejectedItemMessageFormat =
            rejectedItemMessageFormat?.Trim() ?? string.Empty;

        if (items == null)
        {
            items = new List<MerchantShopItemEntry>();
        }

        if (acceptedSellItemTypes == null)
        {
            acceptedSellItemTypes = new List<InventoryItemType>();
        }

        if (acceptedSpecificItems == null)
        {
            acceptedSpecificItems = new List<ItemData>();
        }

        if (rejectedSpecificItems == null)
        {
            rejectedSpecificItems = new List<ItemData>();
        }

        foreach (MerchantShopItemEntry entry in items)
        {
            entry?.Validate();
        }
    }
}

/// <summary>
/// Lv1から販売する商品の設定です。
/// 既存MerchantShopDataのItemsとの互換性を維持しています。
/// </summary>
[Serializable]
public class MerchantShopItemEntry
{
    [SerializeField] private ItemData itemData;

    [Tooltip(
        "旧方式の販売単位数です。Initial Stockが-1の場合は、Lv1初期在庫数としてこの数を使用します。"
    )]
    [SerializeField, Min(1)] private int quantityPerPurchase = 1;

    [Tooltip("旧方式の個別価格です。現在のItemBoxInventory方式では通常使用しません。")]
    [SerializeField] private int unitPriceOverride = -1;

    [Tooltip(
        "Lv1で商品棚へ入れる在庫数です。\n" +
        "-1の場合はQuantity Per Purchaseを在庫数として使用します。"
    )]
    [SerializeField] private int initialStock = -1;

    public ItemData ItemData => itemData;
    public int QuantityPerPurchase => Mathf.Max(1, quantityPerPurchase);
    public int UnitPriceOverride => Mathf.Max(-1, unitPriceOverride);
    public int InitialStock => Mathf.Max(-1, initialStock);
    public bool HasLimitedStock => InitialStock >= 0;

    /// <summary>
    /// 現在のショップ在庫生成に使用する数です。
    /// </summary>
    public int StockAmount => InitialStock >= 0
        ? Mathf.Max(1, InitialStock)
        : QuantityPerPurchase;

    public void Validate()
    {
        quantityPerPurchase = Mathf.Max(1, quantityPerPurchase);
        unitPriceOverride = Mathf.Max(-1, unitPriceOverride);
        initialStock = Mathf.Max(-1, initialStock);
    }
}
