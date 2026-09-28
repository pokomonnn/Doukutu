using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MerchantShopDataを読み、ItemBoxInventoryへ実際の商品在庫を構築します。
///
/// 販売商品・施設Lv・買取条件のマスターデータはMerchantShopData側へ集約し、
/// このComponentはScene上の実在庫との橋渡しだけを担当します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemBoxInventory))]
public class MerchantStockInventory : MonoBehaviour
{
    [Header("店舗マスターデータ")]
    [Tooltip(
        "この商人のMerchantShopDataです。\n" +
        "Lv1商品・Lv2以降の商品・買取条件・施設Lv設定をすべてここから読み取ります。"
    )]
    [SerializeField] private MerchantShopData shopData;

    [Header("実際の商品在庫")]
    [Tooltip("同じGameObjectのItemBoxInventoryです。未設定なら自動取得します。")]
    [SerializeField] private ItemBoxInventory stockInventory;

    [Tooltip("未設定ならGameSessionManager.Instanceを使用します。")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    // ---------------------------------------------------------------------
    // 旧Component設定を壊さないため保持します。
    // 新しい実行処理ではMerchantShopData側の設定を優先します。
    // Editor移行ツールでShopDataへコピーできます。
    // ---------------------------------------------------------------------
    [SerializeField, HideInInspector]
    private TownFacilityUpgradeData facilityUpgradeData;

    [SerializeField, HideInInspector]
    private bool allowAllSellItemTypes = true;

    [SerializeField, HideInInspector]
    private List<InventoryItemType> acceptedSellItemTypes =
        new List<InventoryItemType>();

    [SerializeField, HideInInspector]
    private List<ItemData> acceptedSpecificItems =
        new List<ItemData>();

    [SerializeField, HideInInspector]
    private List<ItemData> rejectedSpecificItems =
        new List<ItemData>();

    [SerializeField, HideInInspector]
    private string rejectedItemMessageFormat =
        "{0}では「{1}」を買い取りません。";

    public MerchantShopData ShopData => shopData;
    public ItemBoxInventory StockInventory => stockInventory;

    // 既存コード互換。統合後はShopData自身がTownFacilityUpgradeDataです。
    public TownFacilityUpgradeData FacilityUpgradeData =>
        shopData != null ? shopData : facilityUpgradeData;

    public bool AllowAllSellItemTypes => shopData != null
        ? shopData.AllowAllSellItemTypes
        : allowAllSellItemTypes;

    public IReadOnlyList<InventoryItemType> AcceptedSellItemTypes =>
        shopData != null
            ? shopData.AcceptedSellItemTypes
            : acceptedSellItemTypes;

    public IReadOnlyList<ItemData> AcceptedSpecificItems =>
        shopData != null
            ? shopData.AcceptedSpecificItems
            : acceptedSpecificItems;

    public IReadOnlyList<ItemData> RejectedSpecificItems =>
        shopData != null
            ? shopData.RejectedSpecificItems
            : rejectedSpecificItems;

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

    private bool hasPreparedStock;
    private int lastAppliedFacilityLevel = -1;

    private void Awake()
    {
        FindReferences();
        stockInventory?.InitializeInventory();
    }

    /// <summary>
    /// 店を開く直前に呼びます。
    /// 初回は必ずMerchantShopDataから商品棚を作り直します。
    /// </summary>
    public void PrepareForOpen()
    {
        FindReferences();

        if (stockInventory == null)
        {
            LogWarning("ItemBoxInventoryが見つかりません。");
            return;
        }

        if (shopData == null)
        {
            LogWarning("MerchantShopDataが設定されていません。");
            return;
        }

        int currentLevel = GetCurrentFacilityLevel();

        // 最初に店を開いた時は、ItemBoxInventoryのStarting Itemsではなく
        // MerchantShopDataを正として商品棚を構築します。
        if (!hasPreparedStock)
        {
            RebuildStockFromShopData(currentLevel);
            hasPreparedStock = true;
            lastAppliedFacilityLevel = currentLevel;
            return;
        }

        // 店を開くたび補充する設定。
        if (shopData.ResetStockEachOpen)
        {
            RebuildStockFromShopData(currentLevel);
            lastAppliedFacilityLevel = currentLevel;
            return;
        }

        // 施設Lvが上がった場合は、新しく解禁されたLvの商品だけ補充します。
        if (currentLevel > lastAppliedFacilityLevel)
        {
            AddUnlockedItemsInRange(
                Mathf.Max(
                    shopData.StartingLevel + 1,
                    lastAppliedFacilityLevel + 1
                ),
                currentLevel
            );

            lastAppliedFacilityLevel = currentLevel;
            return;
        }

        // 古いセーブをロードするなどしてLvが下がった時は、
        // 高Lv商品が残らないよう現在Lvの構成へ作り直します。
        if (currentLevel < lastAppliedFacilityLevel)
        {
            RebuildStockFromShopData(currentLevel);
            lastAppliedFacilityLevel = currentLevel;
        }
    }

    /// <summary>
    /// MerchantShopDataのLv1商品 + 現在Lvまでの解禁商品だけで商品棚を再構築します。
    /// ItemBoxInventory側のStarting Itemsは使用しません。
    /// </summary>
    public void RebuildStockFromShopData()
    {
        FindReferences();

        if (stockInventory == null || shopData == null)
        {
            return;
        }

        int currentLevel = GetCurrentFacilityLevel();
        RebuildStockFromShopData(currentLevel);

        hasPreparedStock = true;
        lastAppliedFacilityLevel = currentLevel;
    }

    private void RebuildStockFromShopData(int currentLevel)
    {
        stockInventory.ResetInventoryEmpty();

        AddLevel1Items();
        AddUnlockedItemsInRange(
            shopData.StartingLevel + 1,
            currentLevel
        );

        Log(
            $"MerchantShopDataから商品棚を構築しました。" +
            $" 店舗={shopData.ShopName} / Lv={currentLevel} / " +
            $"商品枠={stockInventory.Grid.Items.Count}"
        );
    }

    private void AddLevel1Items()
    {
        IReadOnlyList<MerchantShopItemEntry> level1Items =
            shopData != null ? shopData.Items : null;

        if (level1Items == null)
        {
            return;
        }

        foreach (MerchantShopItemEntry entry in level1Items)
        {
            if (entry == null || entry.ItemData == null)
            {
                continue;
            }

            EnsureTargetStock(
                entry.ItemData,
                entry.StockAmount,
                "Lv1商品"
            );
        }
    }

    private void AddUnlockedItemsInRange(int fromLevel, int toLevel)
    {
        if (shopData == null ||
            stockInventory == null ||
            toLevel < fromLevel)
        {
            return;
        }

        int safeFrom = Mathf.Max(
            shopData.StartingLevel + 1,
            fromLevel
        );
        int safeTo = Mathf.Min(shopData.MaxLevel, toLevel);

        for (int level = safeFrom; level <= safeTo; level++)
        {
            foreach (TownFacilityShopUnlockItem unlockItem in
                     shopData.GetShopUnlockItemsForLevel(level))
            {
                if (unlockItem == null ||
                    unlockItem.ItemData == null ||
                    unlockItem.Amount <= 0)
                {
                    continue;
                }

                EnsureTargetStock(
                    unlockItem.ItemData,
                    unlockItem.Amount,
                    $"Lv{level}解禁商品"
                );
            }
        }
    }

    /// <summary>
    /// 商品棚にtargetAmount以上ある状態へ補充します。
    /// 同一ItemDataをLv1と高Lvの両方に設定しても重複スタックを無駄に増やしません。
    /// </summary>
    private void EnsureTargetStock(
        ItemData itemData,
        int targetAmount,
        string sourceLabel)
    {
        if (itemData == null || stockInventory == null)
        {
            return;
        }

        int safeTargetAmount = Mathf.Max(1, targetAmount);
        int existingAmount = stockInventory.GetTotalAmount(itemData);
        int amountToAdd = Mathf.Max(
            0,
            safeTargetAmount - existingAmount
        );

        if (amountToAdd <= 0)
        {
            return;
        }

        bool addedAll = stockInventory.TryAddItem(
            itemData,
            amountToAdd,
            out int remainingAmount
        );

        if (!addedAll || remainingAmount > 0)
        {
            LogWarning(
                $"{sourceLabel} {itemData.DisplayName} を " +
                $"{remainingAmount}個、商品棚へ追加できませんでした。" +
                " ItemBoxInventoryのGridサイズを確認してください。"
            );
            return;
        }

        Log(
            $"{sourceLabel}: {itemData.DisplayName} ×{amountToAdd} を商品棚へ反映"
        );
    }

    private int GetCurrentFacilityLevel()
    {
        FindReferences();

        if (shopData == null)
        {
            return 1;
        }

        int currentLevel = gameSessionManager != null
            ? gameSessionManager.GetFacilityLevel(shopData)
            : shopData.StartingLevel;

        return Mathf.Clamp(
            currentLevel,
            shopData.StartingLevel,
            shopData.MaxLevel
        );
    }

    public int GetUnitBuyPrice(ItemData itemData)
    {
        FindReferences();

        return stockInventory != null
            ? stockInventory.GetBuyPrice(itemData)
            : 0;
    }

    /// <summary>
    /// プレイヤーから買い取れるか判定します。
    /// 新方式ではMerchantShopData内の買取設定を使用します。
    /// </summary>
    public bool CanBuyFromPlayer(
        ItemData itemData,
        out string rejectionReason)
    {
        if (shopData != null)
        {
            return shopData.CanBuyFromPlayer(
                itemData,
                out rejectionReason
            );
        }

        // MerchantShopData未設定時だけ旧Component設定へフォールバック。
        return CanBuyFromPlayerLegacy(
            itemData,
            out rejectionReason
        );
    }

    private bool CanBuyFromPlayerLegacy(
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
            rejectionReason = BuildLegacyRejectionMessage(itemData);
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

        rejectionReason = BuildLegacyRejectionMessage(itemData);
        return false;
    }

    private string BuildLegacyRejectionMessage(ItemData itemData)
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
            Debug.Log(
                $"[MerchantStockInventory: {name}] {message}",
                this
            );
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

        if (shopData == null && facilityUpgradeData != null)
        {
            Debug.LogWarning(
                $"[MerchantStockInventory: {name}] " +
                "旧Facility Upgrade Dataが残っています。" +
                "新方式ではMerchantShopDataへ統合してください。",
                this
            );
        }
    }
}
