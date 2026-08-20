using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MerchantShopDataと、実際の商品在庫であるItemBoxInventoryを結び付けます。
/// 商人ごとに1つ作り、ItemBoxInventoryと同じGameObjectへ付けてください。
///
/// v2.3では、商人ごとの買取可能カテゴリー・個別許可・個別拒否も管理します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemBoxInventory))]
public class MerchantStockInventory : MonoBehaviour
{
    [Header("店舗")]
    [SerializeField] private MerchantShopData shopData;

    [Header("商品在庫")]
    [Tooltip("同じGameObjectのItemBoxInventoryです。未設定なら自動取得します。")]
    [SerializeField] private ItemBoxInventory stockInventory;

    [Header("町施設アップグレード連動")]
    [Tooltip("この店舗が施設アップグレードと連動する場合に設定します。武器屋ならWeaponShop用Dataを設定します。")]
    [SerializeField] private TownFacilityUpgradeData facilityUpgradeData;

    [Tooltip("未設定ならGameSessionManager.Instanceを使用します。")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("プレイヤーからの買取設定")]
    [Tooltip("オンなら、ItemData側で売却可能な全カテゴリーを買い取ります。既存店舗との互換用に初期値はオンです。")]
    [SerializeField] private bool allowAllSellItemTypes = true;

    [Tooltip("Allow All Sell Item Typesがオフの時に、この商人が買い取るカテゴリーです。")]
    [SerializeField]
    private List<InventoryItemType> acceptedSellItemTypes =
        new List<InventoryItemType>();

    [Tooltip("カテゴリーに関係なく、この商人が特別に買い取るItemDataです。Rejected Specific Itemsの方が優先されます。")]
    [SerializeField]
    private List<ItemData> acceptedSpecificItems =
        new List<ItemData>();

    [Tooltip("カテゴリーや個別許可に関係なく、この商人が買い取らないItemDataです。")]
    [SerializeField]
    private List<ItemData> rejectedSpecificItems =
        new List<ItemData>();

    [Tooltip("対象カテゴリー外のアイテムを拒否した時の基本文言です。{0}=店舗名、{1}=アイテム名、{2}=カテゴリー名")]
    [SerializeField]
    private string rejectedItemMessageFormat =
        "{0}では「{1}」を買い取りません。";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public MerchantShopData ShopData => shopData;
    public ItemBoxInventory StockInventory => stockInventory;
    public TownFacilityUpgradeData FacilityUpgradeData => facilityUpgradeData;

    public bool AllowAllSellItemTypes => allowAllSellItemTypes;
    public IReadOnlyList<InventoryItemType> AcceptedSellItemTypes =>
        acceptedSellItemTypes;
    public IReadOnlyList<ItemData> AcceptedSpecificItems =>
        acceptedSpecificItems;
    public IReadOnlyList<ItemData> RejectedSpecificItems =>
        rejectedSpecificItems;

    public string ShopId => shopData != null
        ? shopData.ShopId
        : string.Empty;

    public string ShopName
    {
        get
        {
            if (shopData != null)
            {
                return shopData.ShopName;
            }

            return stockInventory != null
                ? stockInventory.BoxDisplayName
                : name;
        }
    }

    public bool IsReady =>
        shopData != null &&
        stockInventory != null &&
        stockInventory.Grid != null;

    private int lastAppliedFacilityLevel = -1;

    private void Awake()
    {
        FindReferences();
        stockInventory?.InitializeInventory();
    }

    /// <summary>
    /// 店を開く直前の在庫準備です。
    /// Reset Stock Each OpenがオンならStarting Itemsへ戻します。
    /// </summary>
    public void PrepareForOpen()
    {
        FindReferences();

        if (stockInventory == null)
        {
            LogWarning("ItemBoxInventoryが見つかりません。");
            return;
        }

        bool resetEachOpen =
            shopData != null && shopData.ResetStockEachOpen;

        if (resetEachOpen)
        {
            stockInventory.ResetInventory();
            Log("店を開いたため在庫をStarting Itemsへ戻しました。");
        }
        else
        {
            stockInventory.InitializeInventory();
        }

        ApplyFacilityUnlockedStock(resetEachOpen);
    }

    private void ApplyFacilityUnlockedStock(bool stockWasReset)
    {
        if (stockInventory == null || facilityUpgradeData == null)
        {
            return;
        }

        FindReferences();

        int currentLevel = gameSessionManager != null
            ? gameSessionManager.GetFacilityLevel(facilityUpgradeData)
            : facilityUpgradeData.StartingLevel;

        currentLevel = Mathf.Clamp(
            currentLevel,
            facilityUpgradeData.StartingLevel,
            facilityUpgradeData.MaxLevel
        );

        // Reset Stock Each OpenならStarting Itemsへ戻った直後なので、
        // 到達済みレベルの商品をすべて再追加します。
        if (stockWasReset)
        {
            AddUnlockedItemsInRange(
                facilityUpgradeData.StartingLevel + 1,
                currentLevel
            );

            lastAppliedFacilityLevel = currentLevel;
            return;
        }

        // 初回準備時は現在レベルまでの解禁商品を追加します。
        if (lastAppliedFacilityLevel < 0)
        {
            AddUnlockedItemsInRange(
                facilityUpgradeData.StartingLevel + 1,
                currentLevel
            );

            lastAppliedFacilityLevel = currentLevel;
            return;
        }

        // 同一Scene中に施設Lvが上がった場合は、新しく到達したLv分だけ追加します。
        if (currentLevel > lastAppliedFacilityLevel)
        {
            AddUnlockedItemsInRange(
                lastAppliedFacilityLevel + 1,
                currentLevel
            );

            lastAppliedFacilityLevel = currentLevel;
            return;
        }

        // 同じSceneで古いセーブをロードして施設Lvが下がった場合は、
        // 高Lv商品が残らないようStarting Itemsから組み直します。
        if (currentLevel < lastAppliedFacilityLevel)
        {
            stockInventory.ResetInventory();

            AddUnlockedItemsInRange(
                facilityUpgradeData.StartingLevel + 1,
                currentLevel
            );

            lastAppliedFacilityLevel = currentLevel;
        }
    }

    private void AddUnlockedItemsInRange(int fromLevel, int toLevel)
    {
        if (facilityUpgradeData == null ||
            stockInventory == null ||
            toLevel < fromLevel)
        {
            return;
        }

        for (int level = fromLevel; level <= toLevel; level++)
        {
            foreach (TownFacilityShopUnlockItem unlockItem in
                     facilityUpgradeData.GetShopUnlockItemsForLevel(level))
            {
                if (unlockItem == null ||
                    unlockItem.ItemData == null ||
                    unlockItem.Amount <= 0)
                {
                    continue;
                }

                int existingAmount = stockInventory.GetTotalAmount(
                    unlockItem.ItemData
                );

                int amountToAdd = Mathf.Max(
                    0,
                    unlockItem.Amount - existingAmount
                );

                if (amountToAdd <= 0)
                {
                    continue;
                }

                bool addedAll = stockInventory.TryAddItem(
                    unlockItem.ItemData,
                    amountToAdd,
                    out int remainingAmount
                );

                if (!addedAll || remainingAmount > 0)
                {
                    LogWarning(
                        $"Lv{level}解禁商品 {unlockItem.ItemData.DisplayName} を " +
                        $"{remainingAmount}個、商品棚へ追加できませんでした。"
                    );
                    continue;
                }

                Log(
                    $"施設Lv{level}の商品を解禁: " +
                    $"{unlockItem.ItemData.DisplayName} ×{amountToAdd}"
                );
            }
        }
    }

    public int GetUnitBuyPrice(ItemData itemData)
    {
        FindReferences();

        return stockInventory != null
            ? stockInventory.GetBuyPrice(itemData)
            : 0;
    }

    /// <summary>
    /// この商人がプレイヤーから指定ItemDataを買い取るか判定します。
    /// ItemData共通のCanSellToShop、Quest、売却価格判定はSellCartInventory側で行います。
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
        catch (System.FormatException)
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

    private void FindReferences()
    {
        if (stockInventory == null)
        {
            stockInventory = GetComponent<ItemBoxInventory>();
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[MerchantStockInventory: {name}] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[MerchantStockInventory: {name}] {message}",
            this
        );
    }

    private void OnValidate()
    {
        FindReferences();

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

        

        if (stockInventory != null &&
            stockInventory.BoxKind != ItemBoxKind.Shop)
        {
            Debug.LogWarning(
                $"[MerchantStockInventory: {name}] " +
                "ItemBoxInventoryのBox KindをShopにしてください。",
                this
            );
        }
    }

}
