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

    [Header("洞窟のお金アイテム")]
    [Tooltip("洞窟内で拾うお金のItemDataを割り当てます。必須です。Max StackはItemData側で1500にしてください。")]
    [SerializeField] private ItemData expeditionMoneyItem;

    [Tooltip("お金アイテム1個あたりの金額です。通常は1Gです。")]
    [SerializeField, Min(1)] private int goldPerMoneyItem = 1;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public ExpeditionResultData LastCollectedResult { get; private set; }

    private readonly Dictionary<ItemData, int> treasureTotals =
        new Dictionary<ItemData, int>();

    private bool rescueProgressRecorded;
    private bool moneyConvertedForThisExpedition;

    /// <summary>
    /// CaveExitElevator2Dからのみ呼ぶ想定です。
    /// 先にCollectAndStoreResultで探索結果を記録し、町へ移る直前に現金を清算します。
    /// 成功時、プレイヤーのバッグから現金だけ除去し、GameSessionManagerの所持金へ加算します。
    /// 保存済みインベントリを上書きされないよう、シーン退出前のCaptureより先に実行してください。
    /// </summary>
    public bool TryConvertMoneyBeforeExit(out int recoveredMoney)
    {
        recoveredMoney = 0;
        if (moneyConvertedForThisExpedition)
        {
            recoveredMoney = ExpeditionRecoveredMoneySession.GetAmount(LastCollectedResult);
            return true;
        }

        FindReferences();
        if (expeditionMoneyItem == null)
        {
            LogWarning("Expedition Money Itemが未設定です。お金の取り逃しを防ぐため退出を中止します。Inspectorでお金用ItemDataを設定してください。");
            return false;
        }

        if (playerInventory == null || playerInventory.Grid == null)
        {
            LogWarning("プレイヤーのInventoryControllerを見つけられません。現金を安全に精算できないため退出を中止します。");
            return false;
        }

        if (LastCollectedResult == null)
        {
            LogWarning("探索結果が集計されていません。現金精算は実行できません。");
            return false;
        }

        var cashItems = new List<InventoryItem>();
        long moneyTotal = 0;
        foreach (InventoryItem item in playerInventory.Grid.Items)
        {
            if (item == null || item.ItemData == null || item.Amount <= 0 ||
                !IsMoneyItem(item.ItemData))
            {
                continue;
            }
            cashItems.Add(item);
            moneyTotal += (long)item.Amount * Mathf.Max(1, goldPerMoneyItem);
        }

        if (moneyTotal == 0)
        {
            ExpeditionRecoveredMoneySession.Record(LastCollectedResult, 0);
            moneyConvertedForThisExpedition = true;
            Log("現金回収は0Gでした。");
            return true;
        }

        GameSessionManager session = GameSessionManager.Instance;
        if (session == null)
        {
            session = FindAnyObjectByType<GameSessionManager>(FindObjectsInactive.Include);
        }
        if (session == null)
        {
            LogWarning("GameSessionManagerがないため、現金の加算ができません。退出を中止します。");
            return false;
        }

        session.EnsureMoneyInitialized();
        if (moneyTotal > (long)int.MaxValue - session.CurrentMoney)
        {
            LogWarning("所持金の整数上限を超えるため、現金の精算と退出を中止します。");
            return false;
        }

        // 元のItem本体と配置を維持したまま退避し、失敗したら戻せるようにします。
        var removed = new List<InventoryItem>();
        foreach (InventoryItem item in cashItems)
        {
            if (!playerInventory.RemoveItem(item))
            {
                RestoreRemovedMoneyItems(removed);
                LogWarning("お金アイテムの削除に失敗したため、精算と退出を中止しました。");
                return false;
            }
            removed.Add(item);
        }

        recoveredMoney = (int)moneyTotal;
        if (!session.AddMoney(recoveredMoney))
        {
            RestoreRemovedMoneyItems(removed);
            recoveredMoney = 0;
            LogWarning("現金の所持金加算に失敗しました。可能な限り元のアイテムに戻しました。");
            return false;
        }

        ExpeditionRecoveredMoneySession.Record(LastCollectedResult, recoveredMoney);
        moneyConvertedForThisExpedition = true;
        Log($"洞窟現金を精算しました。今回回収={recoveredMoney:N0}G / 町の所持金={session.CurrentMoney:N0}G / 消去スタック数={cashItems.Count}");
        return true;
    }

    private void RestoreRemovedMoneyItems(List<InventoryItem> removed)
    {
        for (int i = removed.Count - 1; i >= 0; i--)
        {
            InventoryItem item = removed[i];
            if (item != null &&
                !playerInventory.TryMoveItem(item, item.GridX, item.GridY, item.IsRotated))
            {
                LogWarning($"現金アイテムのロールバックに失敗しました: {item.ItemData.DisplayName} / {item.Amount}");
            }
        }
    }

    private bool IsMoneyItem(ItemData itemData)
    {
        if (itemData == null || expeditionMoneyItem == null) return false;
        if (itemData == expeditionMoneyItem) return true;

        string actualId = itemData.ItemId;
        string cashId = expeditionMoneyItem.ItemId;
        return !string.IsNullOrWhiteSpace(actualId) &&
            string.Equals(actualId.Trim(), cashId?.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

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

        // エレベーター回収NPCの『今回行った治療』だけResultシーンへ引き継ぐ。
        RescueNpcTreatmentResultSession2D.CaptureFromElevator(elevatorCarryReceiver, result);

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
        if (itemData == null || IsMoneyItem(itemData)) return false;

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
        goldPerMoneyItem = Mathf.Max(1, goldPerMoneyItem);
        if (additionalTreasureItems == null)
        {
            additionalTreasureItems = new List<ItemData>();
        }
    }
}
