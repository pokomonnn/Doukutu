using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>洞窟NPC専用の売買・物々交換データ。町のMerchantShopDataには影響しません。</summary>
[CreateAssetMenu(fileName = "DungeonNpcTrade", menuName = "2D Shooter/Dungeon NPC/Trade Profile")]
public sealed class DungeonNpcTradeProfile2D : ScriptableObject
{
    [Serializable]
    public class Product
    {
        public ItemData item;
        [Tooltip("-1ならItemDataの購入価格と倍率を利用します")]
        public int priceOverride = -1;
        [Min(0)] public int stock = 3;
    }

    [Serializable]
    public class Exchange
    {
        public ItemData requestItem;
        [Min(1)] public int requestAmount = 1;
        public ItemData rewardItem;
        [Min(1)] public int rewardAmount = 1;
        [Tooltip("0なら無制限。1以上はこの出撃での交換上限")]
        [Min(0)] public int exchangeLimit;
    }

    [Header("販売リスト")]
    public List<Product> products = new List<Product>();
    [Min(0)] public float purchasePriceMultiplier = 1f;
    [Tooltip("同じ出撃中は在庫が維持され、次のScene入場で初期在庫へ戻ります")]
    public bool addSoldItemsToStock = true;

    [Header("プレイヤーからの買取")]
    public bool buyAnyItem = true;
    public List<ItemData> acceptedOnly = new List<ItemData>();
    public List<ItemData> rejected = new List<ItemData>();
    [Tooltip("ItemData.SellPriceに掛ける倍率。0.5なら半額")]
    [Min(0)] public float sellPriceMultiplier = 1f;
    [Tooltip("-1はNPC所持金無制限。0以上で買取原資の上限")]
    public int startingNpcMoney = -1;

    [Header("地面に落として物々交換")]
    public List<Exchange> exchanges = new List<Exchange>();

    public bool WillBuy(ItemData item)
    {
        if (item == null || rejected.Contains(item)) return false;
        return buyAnyItem || acceptedOnly.Contains(item);
    }

    public int GetBuyPrice(ItemData item, int priceOverride = -1)
    {
        if (item == null) return 0;
        if (priceOverride >= 0) return priceOverride;
        return Mathf.Max(0, Mathf.CeilToInt(item.PurchasePrice * purchasePriceMultiplier));
    }

    public int GetSellPrice(ItemData item)
    {
        if (item == null) return 0;
        return Mathf.Max(0, Mathf.FloorToInt(item.SellPrice * sellPriceMultiplier));
    }

    private void OnValidate()
    {
        purchasePriceMultiplier = Mathf.Max(0f, purchasePriceMultiplier);
        sellPriceMultiplier = Mathf.Max(0f, sellPriceMultiplier);
        startingNpcMoney = Mathf.Max(-1, startingNpcMoney);
        foreach (var entry in products)
            if (entry != null) { entry.stock = Mathf.Max(0, entry.stock); entry.priceOverride = Mathf.Max(-1, entry.priceOverride); }
        foreach (var e in exchanges)
            if (e != null) { e.requestAmount = Mathf.Max(1, e.requestAmount); e.rewardAmount = Mathf.Max(1, e.rewardAmount); e.exchangeLimit = Mathf.Max(0, e.exchangeLimit); }
    }
}
