using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 町の会話選択肢から、ミッション達成確認・報酬付与・報酬受取済み保存を行います。
/// TownCanvasなど、常時有効なObjectへ付けて使います。
/// </summary>
[DisallowMultipleComponent]
public class TownMissionRewardController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("報酬アイテムを入れる町用プレイヤーInventoryControllerです。TownPlayerInventoryのInventoryControllerを設定します")]
    [SerializeField] private InventoryController rewardInventoryController;

    [Tooltip("結果メッセージを表示するTextです。不要なら空欄でOKです")]
    [SerializeField] private TMP_Text statusText;

    [Header("表示文")]
    [SerializeField]
    private string rewardClaimedFormat =
        "{0} の報酬を受け取りました。";

    [SerializeField]
    private string moneyRewardFormat =
        "所持金 +¥{0}";

    [SerializeField]
    private string itemRewardFormat =
        "アイテム {0} ×{1}";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public bool TryClaimReward(
        TownDialogueChoice choice,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (choice == null)
        {
            resultMessage = "報酬用の選択肢が取得できません。";
            SetStatusMessage(resultMessage);
            LogWarning(resultMessage);
            return false;
        }

        return TryClaimReward(
            choice.MissionToClaimReward,
            choice.MoneyReward,
            choice.ItemRewards,
            choice.RequireObjectiveCompleted,
            out resultMessage
        );
    }

    public bool TryClaimReward(
        MissionDefinition2D mission,
        int moneyReward,
        IReadOnlyList<TownMissionRewardItem> itemRewards,
        bool requireObjectiveCompleted,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        GameSessionManager session = FindSessionManager();

        if (session == null)
        {
            resultMessage =
                "GameSessionManagerが見つかりません。探索シーンからTown_Mainへ移動しているか確認してください。";
            SetStatusMessage(resultMessage);
            LogWarning(resultMessage);
            return false;
        }

        if (!session.CanClaimMissionReward(
                mission,
                requireObjectiveCompleted,
                out resultMessage))
        {
            SetStatusMessage(resultMessage);
            return false;
        }

        if (!CanFitItemRewards(itemRewards, out resultMessage))
        {
            SetStatusMessage(resultMessage);
            return false;
        }

        int safeMoneyReward = Mathf.Max(0, moneyReward);

        if (safeMoneyReward > 0)
        {
            session.AddMoney(safeMoneyReward);
        }

        if (!GrantItemRewards(itemRewards, out resultMessage))
        {
            SetStatusMessage(resultMessage);
            LogWarning(resultMessage);
            return false;
        }

        if (!session.MarkMissionRewardClaimed(
                mission,
                out string claimMessage))
        {
            // 事前確認後にここで失敗するケースは基本的にありません。
            // 万一失敗した場合は、報酬の二重受け取りを避けるため原因を表示します。
            resultMessage = claimMessage;
            SetStatusMessage(resultMessage);
            return false;
        }

        resultMessage = BuildRewardResultMessage(
            mission,
            safeMoneyReward,
            itemRewards
        );

        SetStatusMessage(resultMessage);
        Log(resultMessage);
        return true;
    }

    private bool CanFitItemRewards(
        IReadOnlyList<TownMissionRewardItem> itemRewards,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (!HasAnyValidItemReward(itemRewards))
        {
            return true;
        }

        FindReferences();

        if (rewardInventoryController == null ||
            rewardInventoryController.Grid == null)
        {
            resultMessage =
                "報酬アイテムを入れるInventoryControllerが設定されていません。TownMissionRewardControllerのReward Inventory Controllerを設定してください。";
            LogWarning(resultMessage);
            return false;
        }

        InventoryGrid grid = rewardInventoryController.Grid;
        bool[,] occupied = BuildOccupiedMap(grid);

        foreach (TownMissionRewardItem reward in itemRewards)
        {
            if (reward == null || reward.ItemData == null || reward.Amount <= 0)
            {
                continue;
            }

            int remainingAmount = reward.Amount;
            ItemData itemData = reward.ItemData;
            int maxStack = Mathf.Max(1, itemData.MaxStack);

            if (itemData.CanStack)
            {
                int availableStackSpace = CountExistingStackSpace(
                    grid,
                    itemData
                );

                int filledAmount = Mathf.Min(
                    availableStackSpace,
                    remainingAmount
                );

                remainingAmount -= filledAmount;
            }

            while (remainingAmount > 0)
            {
                if (!TryReserveRewardItemSpace(
                        grid,
                        occupied,
                        itemData,
                        out bool _))
                {
                    resultMessage =
                        $"報酬アイテム {itemData.DisplayName} を入れる空きがありません。インベントリを空けてから報告してください。";
                    LogWarning(resultMessage);
                    return false;
                }

                remainingAmount -= Mathf.Min(maxStack, remainingAmount);
            }
        }

        return true;
    }

    private bool GrantItemRewards(
        IReadOnlyList<TownMissionRewardItem> itemRewards,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (!HasAnyValidItemReward(itemRewards))
        {
            return true;
        }

        FindReferences();

        if (rewardInventoryController == null)
        {
            resultMessage =
                "報酬アイテムを入れるInventoryControllerが見つかりません。";
            return false;
        }

        foreach (TownMissionRewardItem reward in itemRewards)
        {
            if (reward == null || reward.ItemData == null || reward.Amount <= 0)
            {
                continue;
            }

            rewardInventoryController.TryAddItem(
                reward.ItemData,
                reward.Amount,
                out int remainingAmount
            );

            if (remainingAmount > 0)
            {
                resultMessage =
                    $"報酬アイテム {reward.ItemData.DisplayName} を一部受け取れませんでした。残り={remainingAmount}";
                return false;
            }

            Log(
                $"報酬アイテム付与: {reward.ItemData.DisplayName} ×{reward.Amount}"
            );
        }

        return true;
    }

    private bool[,] BuildOccupiedMap(InventoryGrid grid)
    {
        bool[,] occupied = new bool[grid.Width, grid.Height];

        foreach (InventoryItem item in grid.Items)
        {
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            Vector2Int size = item.ItemData.GetSize(item.IsRotated);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int mapX = item.GridX + x;
                    int mapY = item.GridY + y;

                    if (mapX >= 0 && mapX < grid.Width &&
                        mapY >= 0 && mapY < grid.Height)
                    {
                        occupied[mapX, mapY] = true;
                    }
                }
            }
        }

        return occupied;
    }

    private int CountExistingStackSpace(
        InventoryGrid grid,
        ItemData itemData)
    {
        if (grid == null || itemData == null || !itemData.CanStack)
        {
            return 0;
        }

        int space = 0;
        int maxStack = Mathf.Max(1, itemData.MaxStack);

        foreach (InventoryItem item in grid.Items)
        {
            if (item == null || item.ItemData != itemData)
            {
                continue;
            }

            space += Mathf.Max(0, maxStack - item.Amount);
        }

        return space;
    }

    private bool TryReserveRewardItemSpace(
        InventoryGrid grid,
        bool[,] occupied,
        ItemData itemData,
        out bool usedRotation)
    {
        usedRotation = false;

        if (TryReserveRewardItemSpaceWithRotation(
                grid,
                occupied,
                itemData,
                false))
        {
            return true;
        }

        if (itemData.CanRotate &&
            TryReserveRewardItemSpaceWithRotation(
                grid,
                occupied,
                itemData,
                true))
        {
            usedRotation = true;
            return true;
        }

        return false;
    }

    private bool TryReserveRewardItemSpaceWithRotation(
        InventoryGrid grid,
        bool[,] occupied,
        ItemData itemData,
        bool isRotated)
    {
        Vector2Int size = itemData.GetSize(isRotated);

        for (int y = 0; y <= grid.Height - size.y; y++)
        {
            for (int x = 0; x <= grid.Width - size.x; x++)
            {
                if (!IsAreaFree(occupied, x, y, size))
                {
                    continue;
                }

                ReserveArea(occupied, x, y, size);
                return true;
            }
        }

        return false;
    }

    private bool IsAreaFree(
        bool[,] occupied,
        int startX,
        int startY,
        Vector2Int size)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (occupied[startX + x, startY + y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private void ReserveArea(
        bool[,] occupied,
        int startX,
        int startY,
        Vector2Int size)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                occupied[startX + x, startY + y] = true;
            }
        }
    }

    private bool HasAnyValidItemReward(
        IReadOnlyList<TownMissionRewardItem> itemRewards)
    {
        if (itemRewards == null)
        {
            return false;
        }

        foreach (TownMissionRewardItem reward in itemRewards)
        {
            if (reward != null && reward.ItemData != null && reward.Amount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private string BuildRewardResultMessage(
        MissionDefinition2D mission,
        int moneyReward,
        IReadOnlyList<TownMissionRewardItem> itemRewards)
    {
        string missionName = mission != null
            ? mission.DisplayName
            : "ミッション";

        List<string> rewardLines = new List<string>();

        if (moneyReward > 0)
        {
            rewardLines.Add(
                string.Format(moneyRewardFormat, moneyReward.ToString("N0"))
            );
        }

        if (itemRewards != null)
        {
            foreach (TownMissionRewardItem reward in itemRewards)
            {
                if (reward == null || reward.ItemData == null || reward.Amount <= 0)
                {
                    continue;
                }

                rewardLines.Add(
                    string.Format(
                        itemRewardFormat,
                        reward.ItemData.DisplayName,
                        reward.Amount
                    )
                );
            }
        }

        string baseMessage = string.Format(rewardClaimedFormat, missionName);

        if (rewardLines.Count <= 0)
        {
            return baseMessage;
        }

        return baseMessage + "\n" + string.Join("\n", rewardLines);
    }

    private GameSessionManager FindSessionManager()
    {
        if (GameSessionManager.Instance != null)
        {
            return GameSessionManager.Instance;
        }

        return FindAnyObjectByType<GameSessionManager>(
            FindObjectsInactive.Include
        );
    }

    private void FindReferences()
    {
        if (rewardInventoryController == null)
        {
            rewardInventoryController =
                FindAnyObjectByType<InventoryController>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void SetStatusMessage(string message)
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = message ?? string.Empty;
        statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[TownMissionRewardController] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.LogWarning(
            $"[TownMissionRewardController] {message}",
            this
        );
    }
}
