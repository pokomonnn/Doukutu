using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 洞窟を出る直前に今回の探索結果を集計します。
///
/// 集計内容：
/// ・エレベーターへ積んだ生存救出NPC数
/// ・エレベーターへ積んだ死亡NPC数
/// ・エレベーターへ積んだItemBox数
/// ・Player Inventoryと回収ItemBox内のTreasure Item
/// </summary>
[DisallowMultipleComponent]
public class ExpeditionResultCollector2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("現在のエレベーター荷物を管理しているReceiverです。")]
    [SerializeField]
    private ElevatorCarryReceiver2D elevatorCarryReceiver;

    [Tooltip("PlayerのInventoryController。未設定なら自動取得します。")]
    [SerializeField]
    private InventoryController playerInventory;

    [Header("お宝集計")]
    [Tooltip("Playerが直接持っているTreasureをResultへ加えます。")]
    [SerializeField]
    private bool includePlayerInventoryTreasure = true;

    [Tooltip("エレベーターへ回収したItemBox内のTreasureをResultへ加えます。")]
    [SerializeField]
    private bool includeRecoveredItemBoxTreasure = true;

    [Tooltip(
        "ItemTypeがTreasureではない既存ItemDataを、" +
        "リザルト上だけお宝扱いしたい場合に登録します。"
    )]
    [SerializeField]
    private List<ItemData> additionalTreasureItems =
        new List<ItemData>();

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public ExpeditionResultData LastCollectedResult { get; private set; }

    private readonly Dictionary<ItemData, int> treasureTotals =
        new Dictionary<ItemData, int>();

    /// <summary>
    /// 現在の探索結果を集計し、ExpeditionResultSessionへ保存します。
    /// </summary>
    [ContextMenu("Collect Expedition Result")]
    public ExpeditionResultData CollectAndStoreResult()
    {
        FindReferences();

        ExpeditionResultData result =
            new ExpeditionResultData();

        treasureTotals.Clear();

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
                if (target == null)
                {
                    continue;
                }

                // RescuePersonTarget2Dを持つCarryableはNPCとして先に判定します。
                // 死体用の子ItemBoxInventoryを「回収ItemBox」に誤算入しないためです。
                RescuePersonTarget2D rescueTarget =
                    FindComponentOnCarryable<RescuePersonTarget2D>(
                        target
                    );

                if (rescueTarget != null)
                {
                    CharacterHealth health =
                        FindComponentOnCarryable<CharacterHealth>(
                            target
                        );

                    bool isDead =
                        health != null && health.IsDead;

                    if (isDead)
                    {
                        result.DeadNpcCount++;
                    }
                    else
                    {
                        result.RescuedNpcCount++;
                    }

                    continue;
                }

                ItemBoxInventory itemBox =
                    FindComponentOnCarryable<ItemBoxInventory>(
                        target
                    );

                if (itemBox == null)
                {
                    continue;
                }

                result.RecoveredItemBoxCount++;

                if (includeRecoveredItemBoxTreasure)
                {
                    AddTreasureFromGrid(itemBox.Grid);
                }
            }
        }

        if (includePlayerInventoryTreasure &&
            playerInventory != null)
        {
            AddTreasureFromGrid(playerInventory.Grid);
        }

        foreach (KeyValuePair<ItemData, int> pair in treasureTotals)
        {
            if (pair.Key == null || pair.Value <= 0)
            {
                continue;
            }

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

        LastCollectedResult = result;
        ExpeditionResultSession.SetResult(result);

        Log(
            $"探索結果集計完了: " +
            $"救出NPC={result.RescuedNpcCount} / " +
            $"死亡NPC={result.DeadNpcCount} / " +
            $"ItemBox={result.RecoveredItemBoxCount} / " +
            $"お宝種類={result.TreasureItems.Count}"
        );

        return result;
    }

    private void AddTreasureFromGrid(InventoryGrid grid)
    {
        if (grid == null || grid.Items == null)
        {
            return;
        }

        foreach (InventoryItem item in grid.Items)
        {
            if (item == null ||
                item.ItemData == null ||
                item.Amount <= 0 ||
                !IsTreasure(item.ItemData))
            {
                continue;
            }

            if (!treasureTotals.TryGetValue(
                    item.ItemData,
                    out int currentAmount))
            {
                currentAmount = 0;
            }

            treasureTotals[item.ItemData] =
                currentAmount + item.Amount;
        }
    }

    private bool IsTreasure(ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        if (itemData.ItemType == InventoryItemType.Treasure)
        {
            return true;
        }

        return additionalTreasureItems != null &&
            additionalTreasureItems.Contains(itemData);
    }

    private static T FindComponentOnCarryable<T>(
        CarryableObject2D target)
        where T : Component
    {
        if (target == null)
        {
            return null;
        }

        T component = target.GetComponent<T>();

        if (component != null)
        {
            return component;
        }

        component = target.GetComponentInChildren<T>(true);

        if (component != null)
        {
            return component;
        }

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
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log(
                $"[ExpeditionResultCollector2D] {message}",
                this
            );
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[ExpeditionResultCollector2D] {message}",
            this
        );
    }

    private void OnValidate()
    {
        if (additionalTreasureItems == null)
        {
            additionalTreasureItems = new List<ItemData>();
        }
    }
}
