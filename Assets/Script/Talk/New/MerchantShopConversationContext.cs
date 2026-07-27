/// <summary>
/// TownConversationButtonで選ばれた商人と、
/// その商人の実在庫ItemBoxInventoryをPawnShopUIControllerへ渡します。
/// </summary>
public static class MerchantShopConversationContext
{
    public static TownConversationData CurrentConversation
    {
        get;
        private set;
    }

    public static MerchantStockInventory CurrentStock
    {
        get;
        private set;
    }

    public static MerchantShopData CurrentShopData
    {
        get
        {
            if (CurrentStock != null &&
                CurrentStock.ShopData != null)
            {
                return CurrentStock.ShopData;
            }

            return CurrentConversation != null &&
                   CurrentConversation.ConversationType ==
                   TownConversationType.Merchant
                ? CurrentConversation.MerchantShopData
                : null;
        }
    }

    public static void SetMerchant(
        TownConversationData conversationData,
        MerchantStockInventory stockInventory)
    {
        CurrentConversation = conversationData;
        CurrentStock = stockInventory;
    }

    /// <summary>
    /// 旧コード互換用です。実在庫は未設定になります。
    /// </summary>
    public static void SetConversation(
        TownConversationData conversationData)
    {
        SetMerchant(conversationData, null);
    }

    public static void Clear()
    {
        CurrentConversation = null;
        CurrentStock = null;
    }
}
