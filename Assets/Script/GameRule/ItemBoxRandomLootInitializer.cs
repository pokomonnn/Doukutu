using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ItemBoxLootTableを使い、ItemBoxInventoryへランダムな中身を入れます。
/// ItemBoxSpawnManager2Dから管理されている場合、Startでの自動抽選は行いません。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemBoxInventory))]
public class ItemBoxRandomLootInitializer : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private ItemBoxInventory itemBoxInventory;
    [SerializeField] private ItemBoxLootTable lootTable;

    [Header("単体使用時")]
    [Tooltip("SpawnManagerを使わず、シーンへ直接置いた箱で自動抽選したい時だけONにします。")]
    [SerializeField] private bool rollAutomaticallyOnStart;

    [Tooltip("抽選前に、ItemBoxInventoryのStarting Itemsを含む既存内容を空にします。")]
    [SerializeField] private bool clearExistingContentsBeforeRoll = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    private bool managedBySpawnManager;
    private bool hasRolled;

    public bool HasRolled => hasRolled;
    public ItemBoxLootTable LootTable => lootTable;

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        if (!managedBySpawnManager && rollAutomaticallyOnStart)
        {
            RollLoot(null, false);
        }
    }

    public void SetManagedBySpawnManager(bool managed)
    {
        managedBySpawnManager = managed;
    }

    public void SetLootTable(ItemBoxLootTable newLootTable)
    {
        lootTable = newLootTable;
    }

    [ContextMenu("Roll Loot Now")]
    public void RollLootNow()
    {
        RollLoot(null, true);
    }

    public bool RollLoot(
        System.Random random,
        bool forceReroll)
    {
        FindReferences();

        if (itemBoxInventory == null)
        {
            Debug.LogWarning(
                "[ItemBoxRandomLootInitializer] ItemBoxInventoryが見つかりません。",
                this
            );
            return false;
        }

        if (lootTable == null)
        {
            Debug.LogWarning(
                "[ItemBoxRandomLootInitializer] LootTableが未設定です。空箱のままにします。",
                this
            );
            return false;
        }

        if (hasRolled && !forceReroll)
        {
            return true;
        }

        itemBoxInventory.InitializeInventory();

        if (clearExistingContentsBeforeRoll)
        {
            int width = itemBoxInventory.Grid != null
                ? Mathf.Max(1, itemBoxInventory.Grid.Width)
                : 1;

            int height = itemBoxInventory.Grid != null
                ? Mathf.Max(1, itemBoxInventory.Grid.Height)
                : 1;

            itemBoxInventory.RestoreInventoryFromSave(
                width,
                height,
                new List<InventoryItem>()
            );
        }

        List<ItemBoxLootTable.LootRollResult> results =
            lootTable.Roll(random);

        int addedKinds = 0;

        foreach (ItemBoxLootTable.LootRollResult result in results)
        {
            if (result.ItemData == null || result.Amount <= 0)
            {
                continue;
            }

            itemBoxInventory.TryAddItem(
                result.ItemData,
                result.Amount,
                out int remainingAmount
            );

            int addedAmount = result.Amount - remainingAmount;

            if (addedAmount > 0)
            {
                addedKinds++;
            }

            if (remainingAmount > 0)
            {
                Debug.LogWarning(
                    $"[ItemBoxRandomLootInitializer] {result.ItemData.DisplayName} を " +
                    $"{remainingAmount}個、箱の空き不足で入れられませんでした。",
                    this
                );
            }

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[ItemBoxRandomLootInitializer] {name}: " +
                    $"{result.ItemData.DisplayName} {addedAmount}/{result.Amount}個を生成。",
                    this
                );
            }
        }

        hasRolled = true;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[ItemBoxRandomLootInitializer] {name}: 抽選完了。Item種類={addedKinds}",
                this
            );
        }

        return true;
    }

    /// <summary>
    /// セーブ復元される箱で、Start時の自動抽選を確実に抑止します。
    /// </summary>
    public void PrepareForSavedRestore()
    {
        managedBySpawnManager = true;
        hasRolled = true;
    }

    private void FindReferences()
    {
        if (itemBoxInventory == null)
        {
            itemBoxInventory = GetComponent<ItemBoxInventory>();
        }
    }

    private void OnValidate()
    {
        if (itemBoxInventory == null)
        {
            itemBoxInventory = GetComponent<ItemBoxInventory>();
        }
    }
}
