using System;
using System.Collections.Generic;
using UnityEngine;

public enum ItemUseResult
{
    Success,
    InvalidItem,
    NotConsumable,
    PlayerNotFound,
    PlayerIsDead,

    HealthIsFull,
    FoodIsFull,
    WaterIsFull,
    FoodAndWaterAreFull,

    NoUsableEffect,

    StatusConditionControllerNotFound,
    SurvivalControllerNotFound
}

public class InventoryController : MonoBehaviour
{
    [Serializable]
    private class StartingItem
    {
        [SerializeField] private ItemData itemData;
        [SerializeField, Min(1)] private int amount = 1;

        public ItemData ItemData => itemData;
        public int Amount => amount;
    }

    [Header("インベントリサイズ")]
    [SerializeField, Min(1)] private int gridWidth = 7;
    [SerializeField, Min(1)] private int gridHeight = 10;

    [Header("開始時に入れるアイテム（テスト用）")]
    [SerializeField] private bool addStartingItemsOnAwake = true;

    [SerializeField]
    private List<StartingItem> startingItems =
        new List<StartingItem>();

    [Header("使用対象")]
    [Tooltip(
        "プレイヤーのCharacterHealth。" +
        "未設定ならPlayerタグから自動取得します。"
    )]
    [SerializeField]
    private CharacterHealth playerHealth;

    [Tooltip(
        "プレイヤーの状態異常管理。" +
        "未設定ならPlayerタグから自動取得します。"
    )]
    [SerializeField]
    private PlayerStatusConditionController
        playerStatusConditions;

    [Tooltip(
        "プレイヤーの食料・水分管理。" +
        "未設定ならPlayerタグから自動取得します。"
    )]
    [SerializeField]
    private PlayerSurvivalController
        playerSurvivalController;

    [SerializeField] private string playerTag = "Player";

    [Header("インベントリ本体")]
    [SerializeField]
    private InventoryGrid inventoryGrid =
        new InventoryGrid();

    public InventoryGrid Grid => inventoryGrid;

    public event Action OnInventoryChanged;

    private bool isInitialized;

    private void Awake()
    {
        InitializeInventory();

        FindPlayerHealth();
        FindPlayerStatusConditions();
        FindPlayerSurvivalController();
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

    public bool TryAddItem(ItemData itemData, int amount = 1)
    {
        return TryAddItem(itemData, amount, out _);
    }

    public bool TryAddItem(
        ItemData itemData,
        int amount,
        out int remainingAmount)
    {
        if (!isInitialized)
        {
            InitializeInventory();
        }

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

    // 通常の向きのまま移動
    public bool TryMoveItem(
        InventoryItem item,
        int targetX,
        int targetY)
    {
        if (item == null)
        {
            return false;
        }

        bool moved = inventoryGrid.TryPlaceItem(
            item,
            targetX,
            targetY
        );

        if (moved)
        {
            NotifyInventoryChanged();
        }

        return moved;
    }

    // ドラッグ中の仮の向きを、ドロップ時に確定して移動する
    public bool TryMoveItem(
        InventoryItem item,
        int targetX,
        int targetY,
        bool isRotated)
    {
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
        int targetY)
    {
        if (item == null)
        {
            return false;
        }

        return inventoryGrid.CanPlaceItem(
            item,
            targetX,
            targetY
        );
    }

    public bool TryRotateItem(InventoryItem item)
    {
        if (item == null)
        {
            return false;
        }

        bool rotated = inventoryGrid.TryRotateItem(item);

        if (rotated)
        {
            NotifyInventoryChanged();
        }

        return rotated;
    }

    public bool RemoveItem(InventoryItem item)
    {
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

    public InventoryItem GetItemAt(int x, int y)
    {
        return inventoryGrid.GetItemAt(x, y);
    }

    // 指定ItemDataの所持数を、全スタック合計で返す
    public int GetTotalAmount(ItemData itemData)
    {
        if (itemData == null || inventoryGrid == null)
        {
            return 0;
        }

        int totalAmount = 0;

        foreach (InventoryItem item in inventoryGrid.Items)
        {
            if (item == null || item.ItemData != itemData)
            {
                continue;
            }

            totalAmount += item.Amount;
        }

        return totalAmount;
    }

    // 指定ItemDataを複数スタックにまたがって消費し、
    // 実際に消費した数を返す
    public int RemoveAmountByItemData(
        ItemData itemData,
        int amount)
    {
        if (itemData == null ||
            amount <= 0 ||
            inventoryGrid == null)
        {
            return 0;
        }

        List<InventoryItem> matchingItems =
            new List<InventoryItem>();

        foreach (InventoryItem item in inventoryGrid.Items)
        {
            if (item != null && item.ItemData == itemData)
            {
                matchingItems.Add(item);
            }
        }

        int remainingAmount = amount;
        int removedTotal = 0;

        foreach (InventoryItem item in matchingItems)
        {
            if (remainingAmount <= 0)
            {
                break;
            }

            int removed = inventoryGrid.RemoveAmount(
                item,
                remainingAmount
            );

            removedTotal += removed;
            remainingAmount -= removed;
        }

        if (removedTotal > 0)
        {
            NotifyInventoryChanged();
        }

        return removedTotal;
    }

    // 消耗品を使用する
    public bool TryUseItem(
        InventoryItem item,
        out ItemUseResult result)
    {
        result = ItemUseResult.InvalidItem;

        if (item == null ||
            item.ItemData == null ||
            !inventoryGrid.ContainsItem(item))
        {
            return false;
        }

        ConsumableItemData consumableData =
            item.ItemData as ConsumableItemData;

        if (consumableData == null)
        {
            result = ItemUseResult.NotConsumable;
            return false;
        }

        if (!FindPlayerHealth())
        {
            result = ItemUseResult.PlayerNotFound;
            return false;
        }

        if (playerHealth.IsDead)
        {
            result = ItemUseResult.PlayerIsDead;
            return false;
        }

        bool needsStatusConditionController =
            consumableData.CuredConditions !=
            StatusConditionType.None;

        bool needsSurvivalController =
            consumableData.CanRestoreFood ||
            consumableData.CanRestoreWater;

        // 状態異常回復アイテムなのにControllerが無い場合、
        // アイテムが消費されないようにする
        if (needsStatusConditionController &&
            !FindPlayerStatusConditions())
        {
            result =
                ItemUseResult.StatusConditionControllerNotFound;

            Debug.LogWarning(
                "InventoryController：" +
                "PlayerStatusConditionControllerが" +
                "Playerに見つかりません。",
                this
            );

            return false;
        }

        // 食料・水分を回復するアイテムなのにControllerが無い場合も、
        // アイテムだけ消費されないようにする
        if (needsSurvivalController &&
            !FindPlayerSurvivalController())
        {
            result =
                ItemUseResult.SurvivalControllerNotFound;

            Debug.LogWarning(
                "InventoryController：" +
                "PlayerSurvivalControllerが" +
                "Playerに見つかりません。",
                this
            );

            return false;
        }

        bool usedEffect = false;

        bool healthIsFull =
            consumableData.HealAmount > 0 &&
            playerHealth.CurrentHealth >=
            playerHealth.MaxHealth;

        bool foodIsFull =
            consumableData.CanRestoreFood &&
            playerSurvivalController.IsFoodFull;

        bool waterIsFull =
            consumableData.CanRestoreWater &&
            playerSurvivalController.IsWaterFull;

        // HP回復
        if (consumableData.HealAmount > 0 &&
            !healthIsFull)
        {
            playerHealth.Heal(consumableData.HealAmount);
            usedEffect = true;
        }

        // 食料回復
        if (consumableData.CanRestoreFood &&
            !foodIsFull)
        {
            float restoredFood =
                playerSurvivalController.RestoreFood(
                    consumableData.FoodRestoreAmount
                );

            if (restoredFood > 0f)
            {
                usedEffect = true;
            }
        }

        // 水分回復
        if (consumableData.CanRestoreWater &&
            !waterIsFull)
        {
            float restoredWater =
                playerSurvivalController.RestoreWater(
                    consumableData.WaterRestoreAmount
                );

            if (restoredWater > 0f)
            {
                usedEffect = true;
            }
        }

        // 骨折・出血などの状態異常回復
        if (needsStatusConditionController &&
            playerStatusConditions.CureConditions(
                consumableData.CuredConditions
            ))
        {
            usedEffect = true;
        }

        // 回復できるものが何も無い場合は消費しない
        if (!usedEffect)
        {
            result = GetNoUsableEffectResult(
                consumableData,
                healthIsFull,
                foodIsFull,
                waterIsFull
            );

            return false;
        }

        // 使用時に消費する設定なら1個減らす
        if (consumableData.ConsumeOnUse)
        {
            RemoveItemAmount(item, 1);
        }

        result = ItemUseResult.Success;
        return true;
    }

    [ContextMenu("Reset Inventory")]
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

    private ItemUseResult GetNoUsableEffectResult(
        ConsumableItemData consumableData,
        bool healthIsFull,
        bool foodIsFull,
        bool waterIsFull)
    {
        bool restoresFood =
            consumableData.CanRestoreFood;

        bool restoresWater =
            consumableData.CanRestoreWater;

        if (restoresFood &&
            restoresWater &&
            foodIsFull &&
            waterIsFull)
        {
            return ItemUseResult.FoodAndWaterAreFull;
        }

        if (restoresWater && waterIsFull)
        {
            return ItemUseResult.WaterIsFull;
        }

        if (restoresFood && foodIsFull)
        {
            return ItemUseResult.FoodIsFull;
        }

        if (consumableData.HealAmount > 0 &&
            healthIsFull)
        {
            return ItemUseResult.HealthIsFull;
        }

        return ItemUseResult.NoUsableEffect;
    }

    private bool FindPlayerHealth()
    {
        if (playerHealth != null)
        {
            return true;
        }

        GameObject player =
            GameObject.FindGameObjectWithTag(playerTag);

        if (player == null)
        {
            return false;
        }

        playerHealth = player.GetComponent<CharacterHealth>();

        return playerHealth != null;
    }

    private bool FindPlayerStatusConditions()
    {
        if (playerStatusConditions != null)
        {
            return true;
        }

        GameObject player =
            GameObject.FindGameObjectWithTag(playerTag);

        if (player == null)
        {
            return false;
        }

        playerStatusConditions =
            player.GetComponent<
                PlayerStatusConditionController
            >();

        return playerStatusConditions != null;
    }

    private bool FindPlayerSurvivalController()
    {
        if (playerSurvivalController != null)
        {
            return true;
        }

        GameObject player =
            GameObject.FindGameObjectWithTag(playerTag);

        if (player == null)
        {
            return false;
        }

        playerSurvivalController =
            player.GetComponent<PlayerSurvivalController>();

        return playerSurvivalController != null;
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
                    $"InventoryController: " +
                    $"{startingItem.ItemData.DisplayName} を " +
                    $"{remainingAmount} 個入れられませんでした。"
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
    }
}