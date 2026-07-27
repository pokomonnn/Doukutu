using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 商人を識別するための店舗データです。
///
/// v2では実際の商品・在庫・購入価格倍率は ItemBoxInventory 側で管理します。
/// 旧方式の商品リストは移行確認と後方互換のため残していますが、
/// MerchantPurchaseControllerからは使用しません。
/// </summary>
[CreateAssetMenu(
    fileName = "NewMerchantShop",
    menuName = "Town/Merchant/Shop Data"
)]
public class MerchantShopData : ScriptableObject
{
    [Header("店舗情報")]
    [SerializeField] private string shopId = "merchant_shop";
    [SerializeField] private string shopName = "商店";

    [Header("ItemBoxInventory方式")]
    [Tooltip("オンなら店を開くたびに、MerchantStockInventoryのItemBoxInventoryをStarting Itemsの内容へ戻します。")]
    [SerializeField] private bool resetStockEachOpen;

    [Tooltip("購入画面で一度に指定できる最大個数です。在庫数より多くは指定できません。")]
    [SerializeField, Min(1)] private int maxPurchaseAmount = 999;

    [Header("旧方式との互換用（v2では未使用）")]
    [Tooltip("旧MerchantShopUIController用の価格倍率です。v2ではItemBoxInventoryのBuy Price Multiplierを使用します。")]
    [SerializeField, Min(0f)] private float purchasePriceMultiplier = 1f;

    [Tooltip("旧MerchantShopUIController用の商品リストです。v2ではItemBoxInventoryのStarting Itemsを使用します。")]
    [SerializeField]
    private List<MerchantShopItemEntry> items =
        new List<MerchantShopItemEntry>();

    public string ShopId => string.IsNullOrWhiteSpace(shopId)
        ? name
        : shopId.Trim();

    public string ShopName => string.IsNullOrWhiteSpace(shopName)
        ? name
        : shopName.Trim();

    public bool ResetStockEachOpen => resetStockEachOpen;
    public int MaxPurchaseAmount => Mathf.Max(1, maxPurchaseAmount);

    // 以下は旧方式とのコンパイル互換用です。
    public float PurchasePriceMultiplier =>
        Mathf.Max(0f, purchasePriceMultiplier);

    public IReadOnlyList<MerchantShopItemEntry> Items => items;

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

    private void OnValidate()
    {
        shopId = shopId?.Trim() ?? string.Empty;
        shopName = shopName?.Trim() ?? string.Empty;
        maxPurchaseAmount = Mathf.Max(1, maxPurchaseAmount);
        purchasePriceMultiplier = Mathf.Max(
            0f,
            purchasePriceMultiplier
        );

        if (items == null)
        {
            items = new List<MerchantShopItemEntry>();
        }

        foreach (MerchantShopItemEntry entry in items)
        {
            entry?.Validate();
        }
    }
}

/// <summary>
/// 旧リスト方式とのコンパイル互換用です。
/// v2のItemBoxInventory方式では使用しません。
/// </summary>
[Serializable]
public class MerchantShopItemEntry
{
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int quantityPerPurchase = 1;
    [SerializeField] private int unitPriceOverride = -1;
    [SerializeField] private int initialStock = -1;

    public ItemData ItemData => itemData;
    public int QuantityPerPurchase => Mathf.Max(1, quantityPerPurchase);
    public int UnitPriceOverride => Mathf.Max(-1, unitPriceOverride);
    public int InitialStock => Mathf.Max(-1, initialStock);
    public bool HasLimitedStock => InitialStock >= 0;

    public void Validate()
    {
        quantityPerPurchase = Mathf.Max(1, quantityPerPurchase);
        unitPriceOverride = Mathf.Max(-1, unitPriceOverride);
        initialStock = Mathf.Max(-1, initialStock);
    }
}
