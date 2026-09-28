using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 洞窟を出る直前に今回の探索結果を集計します。
/// ・生存救出NPC数
/// ・死亡NPC数
/// ・回収ItemBox数
/// ・Treasure
/// ・累計救出人数／特殊NPC解放
/// </summary>
[DisallowMultipleComponent]
public class ExpeditionResultCollector2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("現在のエレベーター荷物を管理しているReceiverです。")]
    [SerializeField] private ElevatorCarryReceiver2D elevatorCarryReceiver;

    [Tooltip("PlayerのInventoryController。未設定なら自動取得します。")]
    [SerializeField] private InventoryController playerInventory;

    [Header("救出進捗")]
    [InspectorName("救出進捗Manager")]
    [SerializeField] private RescueProgressManager2D rescueProgressManager;

    [InspectorName("救出進捗を記録する")]
    [SerializeField] private bool recordRescueProgress = true;

    [Header("お宝集計")]
    [SerializeField] private bool includePlayerInventoryTreasure = true;
    [SerializeField] private bool includeRecoveredItemBoxTreasure = true;

    [SerializeField]
    private List<ItemData> additionalTreasureItems =
        new List<ItemData>();

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public ExpeditionResultData LastCollectedResult { get; private set; }

    private readonly Dictionary<ItemData, int> treasureTotals =
        new Dictionary<ItemData, int>();

    private bool rescueProgressRecorded;

    [ContextMenu("Collect Expedition Result")]
    public ExpeditionResultData CollectAndStoreResult()
    {
        FindReferences();

        ExpeditionResultData result = new ExpeditionResultData();
        treasureTotals.Clear();

        List<CarryableObject2D> aliveRescueTargets =
            new List<CarryableObject2D>();

        if (elevatorCarryReceiver == null)
        {
            LogWarning(
                "ElevatorCarryReceiver2Dが見つかりません。" +
                "NPC・ItemBoxの回収数は0として集計します。"
            );
        }
        else
        {
            List<CarryableObject2D> cargo =
                elevatorCarryReceiver.GetCargoSnapshot(true);

            foreach (CarryableObject2D target in cargo)
            {
                if (target == null) continue;

                RescuePersonTarget2D rescueTarget =
                    FindComponentOnCarryable<RescuePersonTarget2D>(target);

                if (rescueTarget != null)
                {
                    CharacterHealth health =
                        FindComponentOnCarryable<CharacterHealth>(target);

                    bool isDead = health != null && health.IsDead;

                    if (isDead)
                    {
                        result.DeadNpcCount++;
                    }
                    else
                    {
                        result.RescuedNpcCount++;

                        if (!aliveRescueTargets.Contains(target))
                        {
                            aliveRescueTargets.Add(target);
                        }
                    }

                    continue;
                }

                ItemBoxInventory itemBox =
                    FindComponentOnCarryable<ItemBoxInventory>(target);

                if (itemBox == null) continue;

                result.RecoveredItemBoxCount++;

                if (includeRecoveredItemBoxTreasure)
                {
                    AddTreasureFromGrid(itemBox.Grid);
                }
            }
        }

        if (includePlayerInventoryTreasure && playerInventory != null)
        {
            AddTreasureFromGrid(playerInventory.Grid);
        }

        foreach (KeyValuePair<ItemData, int> pair in treasureTotals)
        {
            if (pair.Key == null || pair.Value <= 0) continue;

            result.TreasureItems.Add(
                new ExpeditionTreasureResult
                {
                    ItemData = pair.Key,
                    Amount = pair.Value
                }
            );
        }

        result.TreasureItems.Sort(
            (a, b) => string.Compare(
                a != null ? a.DisplayName : string.Empty,
                b != null ? b.DisplayName : string.Empty,
                System.StringComparison.CurrentCulture
            )
        );

        RecordRescueProgressOnce(aliveRescueTargets);

        LastCollectedResult = result;
        ExpeditionResultSession.SetResult(result);

        Log(
            $"探索結果集計完了: 救出NPC={result.RescuedNpcCount} / " +
            $"死亡NPC={result.DeadNpcCount} / " +
            $"ItemBox={result.RecoveredItemBoxCount} / " +
            $"お宝種類={result.TreasureItems.Count}"
        );

        return result;
    }

    private void RecordRescueProgressOnce(List<CarryableObject2D> aliveTargets)
    {
        if (!recordRescueProgress || rescueProgressRecorded)
        {
            return;
        }

        rescueProgressRecorded = true;

        if (aliveTargets == null || aliveTargets.Count == 0)
        {
            Log("今回の生存救出NPCは0人です。");
            return;
        }

        if (rescueProgressManager == null)
        {
            LogWarning(
                "RescueProgressManager2Dが見つかりません。" +
                "累計救出人数・特殊NPC解放は記録されません。"
            );
            return;
        }

        rescueProgressManager.RecordRescuedPeople(aliveTargets);
    }

    private void AddTreasureFromGrid(InventoryGrid grid)
    {
        if (grid == null || grid.Items == null) return;

        foreach (InventoryItem item in grid.Items)
        {
            if (item == null ||
                item.ItemData == null ||
                item.Amount <= 0 ||
                !IsTreasure(item.ItemData))
            {
                continue;
            }

            if (!treasureTotals.TryGetValue(item.ItemData, out int current))
            {
                current = 0;
            }

            treasureTotals[item.ItemData] = current + item.Amount;
        }
    }

    private bool IsTreasure(ItemData itemData)
    {
        if (itemData == null) return false;

        if (itemData.ItemType == InventoryItemType.Treasure)
        {
            return true;
        }

        return additionalTreasureItems != null &&
               additionalTreasureItems.Contains(itemData);
    }

    private static T FindComponentOnCarryable<T>(CarryableObject2D target)
        where T : Component
    {
        if (target == null) return null;

        T component = target.GetComponent<T>();
        if (component != null) return component;

        component = target.GetComponentInChildren<T>(true);
        if (component != null) return component;

        return target.GetComponentInParent<T>();
    }

    private void FindReferences()
    {
        if (elevatorCarryReceiver == null)
        {
            elevatorCarryReceiver =
                FindAnyObjectByType<ElevatorCarryReceiver2D>(
                    FindObjectsInactive.Include
                );
        }

        if (playerInventory == null)
        {
            playerInventory =
                FindAnyObjectByType<InventoryController>(
                    FindObjectsInactive.Include
                );
        }

        if (rescueProgressManager == null)
        {
            rescueProgressManager =
                FindAnyObjectByType<RescueProgressManager2D>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ExpeditionResultCollector2D] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[ExpeditionResultCollector2D] {message}", this);
    }

    private void OnValidate()
    {
        if (additionalTreasureItems == null)
        {
            additionalTreasureItems = new List<ItemData>();
        }
    }
}
