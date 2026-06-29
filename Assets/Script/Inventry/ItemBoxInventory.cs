using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemBoxKind
{
    Storage,
    Shop
}

[DisallowMultipleComponent]
public class ItemBoxInventory : MonoBehaviour
{
    [Serializable]
    private class StartingItem
    {
        [SerializeField] private ItemData itemData;
        [SerializeField, Min(1)] private int amount = 1;

        public ItemData ItemData => itemData;
        public int Amount => amount;
    }

    [Header("基本設定")]
    [SerializeField] private string boxDisplayName = "アイテムボックス";

    [Tooltip("Storage は無料で出し入れできる通常箱。Shop は将来の売買在庫用です。")]
    [SerializeField] private ItemBoxKind boxKind = ItemBoxKind.Storage;

    [Tooltip("Storage の時だけ有効。オフなら中身の閲覧専用になります。")]
    [SerializeField] private bool allowDirectItemTransfer = true;

    [Header("インベントリサイズ")]
    [SerializeField, Min(1)] private int gridWidth = 7;
    [SerializeField, Min(1)] private int gridHeight = 10;

    [Header("開始時に入れるアイテム")]
    [SerializeField] private bool addStartingItemsOnAwake = true;

    [SerializeField]
    private List<StartingItem> startingItems =
        new List<StartingItem>();

    [Header("ショップ用の価格設定（将来用）")]
    [Tooltip("購入価格に掛ける倍率。1なら ItemData の Purchase Price そのままです。")]
    [SerializeField, Min(0f)] private float buyPriceMultiplier = 1f;

    [Tooltip("売却価格に掛ける倍率。1なら ItemData の Sell Price そのままです。")]
    [SerializeField, Min(0f)] private float sellPriceMultiplier = 1f;

    [SerializeField]
    private InventoryGrid inventoryGrid =
        new InventoryGrid();

    public InventoryGrid Grid => inventoryGrid;
    public string BoxDisplayName => boxDisplayName;
    public ItemBoxKind BoxKind => boxKind;

    // Shop は、売買処理を作るまでは無料移動できないようにします。
    public bool AllowsDirectItemTransfer =>
        boxKind == ItemBoxKind.Storage &&
        allowDirectItemTransfer;

    public event Action OnInventoryChanged;

    private bool isInitialized;

    private void Awake()
    {
        InitializeInventory();
    }

    public void InitializeInventory()
    {
        if (isInitialized)
        {
            return;
        }

        if (inventoryGrid == null)
        {
            inventoryGrid = new InventoryGrid();
        }

        inventoryGrid.Initialize(gridWidth, gridHeight, true);

        if (addStartingItemsOnAwake)
        {
            AddStartingItems();
        }

        isInitialized = true;
        NotifyInventoryChanged();
    }

    public bool ContainsItem(InventoryItem item)
    {
        return inventoryGrid != null &&
               inventoryGrid.ContainsItem(item);
    }

    public bool TryAddItem(ItemData itemData, int amount = 1)
    {
        return TryAddItem(itemData, amount, out _);
    }

    public bool TryAddItem(
        ItemData itemData,
        int amount,
        out int remainingAmount)
    {
        EnsureInitialized();

        if (itemData == null || amount <= 0)
        {
            remainingAmount = Mathf.Max(0, amount);
            return false;
        }

        bool addedAll = inventoryGrid.TryAddItem(
            itemData,
            amount,
            out remainingAmount
        );

        if (remainingAmount < amount)
        {
            NotifyInventoryChanged();
        }

        return addedAll;
    }

    public bool TryMoveItem(
        InventoryItem item,
        int targetX,
        int targetY)
    {
        return TryMoveItem(
            item,
            targetX,
            targetY,
            item != null && item.IsRotated
        );
    }

    public bool TryMoveItem(
        InventoryItem item,
        int targetX,
        int targetY,
        bool isRotated)
    {
        EnsureInitialized();

        if (item == null)
        {
            return false;
        }

        bool moved = inventoryGrid.TryPlaceItem(
            item,
            targetX,
            targetY,
            isRotated
        );

        if (moved)
        {
            NotifyInventoryChanged();
        }

        return moved;
    }

    public bool CanMoveItemTo(
        InventoryItem item,
        int targetX,
        int targetY,
        bool isRotated)
    {
        EnsureInitialized();

        return item != null &&
               inventoryGrid.CanPlaceItem(
                   item,
                   targetX,
                   targetY,
                   isRotated
               );
    }

    public bool RemoveItem(InventoryItem item)
    {
        EnsureInitialized();

        if (item == null)
        {
            return false;
        }

        bool removed = inventoryGrid.RemoveItem(item);

        if (removed)
        {
            NotifyInventoryChanged();
        }

        return removed;
    }

    public int RemoveItemAmount(
        InventoryItem item,
        int amount)
    {
        EnsureInitialized();

        if (item == null || amount <= 0)
        {
            return 0;
        }

        int removedAmount = inventoryGrid.RemoveAmount(
            item,
            amount
        );

        if (removedAmount > 0)
        {
            NotifyInventoryChanged();
        }

        return removedAmount;
    }

    public int GetTotalAmount(ItemData itemData)
    {
        EnsureInitialized();

        if (itemData == null || inventoryGrid == null)
        {
            return 0;
        }

        int total = 0;

        foreach (InventoryItem item in inventoryGrid.Items)
        {
            if (item != null && item.ItemData == itemData)
            {
                total += item.Amount;
            }
        }

        return total;
    }

    public int GetBuyPrice(ItemData itemData)
    {
        if (itemData == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            Mathf.CeilToInt(
                itemData.PurchasePrice * buyPriceMultiplier
            )
        );
    }

    public int GetSellPrice(ItemData itemData)
    {
        if (itemData == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            Mathf.FloorToInt(
                itemData.SellPrice * sellPriceMultiplier
            )
        );
    }

    [ContextMenu("Reset Item Box Inventory")]
    public void ResetInventory()
    {
        if (inventoryGrid == null)
        {
            inventoryGrid = new InventoryGrid();
        }

        inventoryGrid.Initialize(gridWidth, gridHeight, true);
        AddStartingItems();

        isInitialized = true;
        NotifyInventoryChanged();
    }

    private void EnsureInitialized()
    {
        if (!isInitialized)
        {
            InitializeInventory();
        }
    }

    private void AddStartingItems()
    {
        foreach (StartingItem startingItem in startingItems)
        {
            if (startingItem == null ||
                startingItem.ItemData == null)
            {
                continue;
            }

            bool addedAll = inventoryGrid.TryAddItem(
                startingItem.ItemData,
                startingItem.Amount,
                out int remainingAmount
            );

            if (!addedAll)
            {
                Debug.LogWarning(
                    $"ItemBoxInventory: " +
                    $"{startingItem.ItemData.DisplayName} を " +
                    $"{remainingAmount} 個入れられませんでした。",
                    this
                );
            }
        }
    }

    private void NotifyInventoryChanged()
    {
        OnInventoryChanged?.Invoke();
    }

    private void OnValidate()
    {
        gridWidth = Mathf.Max(1, gridWidth);
        gridHeight = Mathf.Max(1, gridHeight);

        buyPriceMultiplier = Mathf.Max(0f, buyPriceMultiplier);
        sellPriceMultiplier = Mathf.Max(0f, sellPriceMultiplier);

        boxDisplayName = string.IsNullOrWhiteSpace(boxDisplayName)
            ? "アイテムボックス"
            : boxDisplayName.Trim();
    }
}
