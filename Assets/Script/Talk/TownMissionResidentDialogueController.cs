using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ミッションをくれる住人専用の簡単会話Controllerです。
/// ミッション状態に応じて、未受注・受注中・報告可能・報酬後の会話を自動で切り替えます。
/// </summary>
[DisallowMultipleComponent]
public class TownMissionResidentDialogueController : MonoBehaviour
{
    [Header("会話パネル")]
    [Tooltip("会話表示全体のPanelです。開始時に非表示にしてOKです。")]
    [SerializeField] private GameObject dialoguePanel;

    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text residentNameText;
    [SerializeField] private TMP_Text dialogueText;

    [Header("ボタン")]
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonLabel;

    [SerializeField] private Button acceptButton;
    [SerializeField] private TMP_Text acceptButtonLabel;

    [SerializeField] private Button claimRewardButton;
    [SerializeField] private TMP_Text claimRewardButtonLabel;

    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text closeButtonLabel;

    [Header("報酬アイテムの入れ先")]
    [Tooltip("アイテム報酬を入れる町用プレイヤーInventoryControllerです。お金だけなら空欄でもOKです。")]
    [SerializeField] private InventoryController rewardInventoryController;

    [Header("任意メッセージ")]
    [Tooltip("受注成功・報酬受取・設定ミスなどを表示するTextです。不要なら空欄でOKです。")]
    [SerializeField] private TMP_Text statusText;

    [Header("動作")]
    [SerializeField] private bool hidePanelOnAwake = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool logEveryOpen = true;

    public bool IsOpen => isOpen;
    public TownMissionResidentDialogueData CurrentDialogue => currentDialogue;
    public TownMissionResidentState CurrentState => currentState;

    private TownMissionResidentDialogueData currentDialogue;
    private TownMissionResidentState currentState = TownMissionResidentState.Invalid;
    private IReadOnlyList<string> currentLines = Array.Empty<string>();
    private int currentLineIndex;
    private bool isOpen;

    private void Awake()
    {
        SetupButtons();

        if (hidePanelOnAwake)
        {
            SetPanelVisible(false);
        }
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    public void OpenDialogue(TownMissionResidentDialogueData dialogueData)
    {
        if (dialogueData == null)
        {
            LogWarning("TownMissionResidentDialogueDataが未設定です。");
            return;
        }

        if (dialoguePanel == null)
        {
            LogWarning("Dialogue Panelが未設定です。");
            return;
        }

        currentDialogue = dialogueData;
        currentState = EvaluateState(dialogueData);
        currentLines = dialogueData.GetLines(currentState);
        currentLineIndex = 0;
        isOpen = true;

        SetPanelVisible(true);
        ClearStatusMessage();
        ApplyStaticUI(dialogueData);
        ShowCurrentLine();

        if (logEveryOpen)
        {
            LogState("会話開始");
        }
    }

    public void CloseDialogue()
    {
        ClearStatusMessage();
        SetPanelVisible(false);
        isOpen = false;
        currentDialogue = null;
        currentState = TownMissionResidentState.Invalid;
        currentLines = Array.Empty<string>();
        currentLineIndex = 0;
    }

    public void NextLine()
    {
        if (!isOpen || currentDialogue == null)
        {
            return;
        }

        if (currentLines == null || currentLines.Count == 0)
        {
            CloseDialogue();
            return;
        }

        if (currentLineIndex < currentLines.Count - 1)
        {
            currentLineIndex++;
            ShowCurrentLine();
        }
        else
        {
            CloseDialogue();
        }
    }

    public void AcceptMission()
    {
        if (!isOpen || currentDialogue == null)
        {
            return;
        }

        MissionDefinition2D mission = currentDialogue.Mission;
        GameSessionManager session = FindSessionManager();

        if (session == null)
        {
            SetStatusMessage(
                "GameSessionManagerが見つかりません。探索シーンからTown_Mainへ移動しているか確認してください。",
                true
            );
            return;
        }

        bool accepted = session.AcceptMission(
            mission,
            currentDialogue.TrackMissionAfterAccept,
            out string resultMessage
        );

        SetStatusMessage(resultMessage, !accepted);

        if (!accepted)
        {
            LogState("受注失敗");
            return;
        }

        currentState = TownMissionResidentState.AcceptedJustNow;
        currentLines = currentDialogue.GetLines(currentState);
        currentLineIndex = 0;
        ShowCurrentLine();
        LogState("受注成功");
    }

    public void ClaimReward()
    {
        if (!isOpen || currentDialogue == null)
        {
            return;
        }

        GameSessionManager session = FindSessionManager();

        if (session == null)
        {
            SetStatusMessage(
                "GameSessionManagerが見つかりません。探索シーンからTown_Mainへ移動しているか確認してください。",
                true
            );
            return;
        }

        MissionDefinition2D mission = currentDialogue.Mission;

        if (!session.CanClaimMissionReward(
                mission,
                currentDialogue.RequireObjectiveCompleted,
                out string resultMessage))
        {
            SetStatusMessage(resultMessage, true);
            LogState("報酬確認失敗");
            return;
        }

        if (!CanFitItemRewards(currentDialogue.ItemRewards, out resultMessage))
        {
            SetStatusMessage(resultMessage, true);
            return;
        }

        if (!GrantItemRewards(currentDialogue.ItemRewards, out resultMessage))
        {
            SetStatusMessage(resultMessage, true);
            return;
        }

        int moneyReward = currentDialogue.MoneyReward;

        if (moneyReward > 0)
        {
            session.AddMoney(moneyReward);
        }

        if (!session.MarkMissionRewardClaimed(mission, out resultMessage))
        {
            SetStatusMessage(resultMessage, true);
            return;
        }

        string message = BuildRewardMessage(currentDialogue, moneyReward);
        SetStatusMessage(message, false);

        currentState = TownMissionResidentState.RewardClaimed;
        currentLines = currentDialogue.GetLines(currentState);
        currentLineIndex = 0;
        ShowCurrentLine();
        LogState("報酬受取成功");
    }

    private TownMissionResidentState EvaluateState(
        TownMissionResidentDialogueData dialogueData)
    {
        if (dialogueData == null || dialogueData.Mission == null)
        {
            return TownMissionResidentState.Invalid;
        }

        GameSessionManager session = FindSessionManager();

        if (session == null)
        {
            return TownMissionResidentState.NotAccepted;
        }

        string missionId = GetMissionId(dialogueData.Mission);

        if (string.IsNullOrWhiteSpace(missionId))
        {
            return TownMissionResidentState.Invalid;
        }

        if (!session.TryGetMissionSession(
                missionId,
                out MissionSessionData data) ||
            data == null ||
            data.State == MissionSessionState.Inactive)
        {
            return TownMissionResidentState.NotAccepted;
        }

        if (data.RewardClaimed)
        {
            return TownMissionResidentState.RewardClaimed;
        }

        if (!dialogueData.RequireObjectiveCompleted)
        {
            return TownMissionResidentState.ReadyToReport;
        }

        if (data.State == MissionSessionState.Completed ||
            data.Progress >= Mathf.Max(1, data.RequiredAmount))
        {
            return TownMissionResidentState.ReadyToReport;
        }

        return TownMissionResidentState.InProgress;
    }

    private void ShowCurrentLine()
    {
        if (currentDialogue == null)
        {
            return;
        }

        string line = string.Empty;

        if (currentLines != null && currentLines.Count > 0)
        {
            int safeIndex = Mathf.Clamp(currentLineIndex, 0, currentLines.Count - 1);
            line = currentLines[safeIndex] ?? string.Empty;
        }

        if (dialogueText != null)
        {
            dialogueText.text = line;
        }

        bool hasNextLine = currentLines != null &&
                           currentLineIndex < currentLines.Count - 1;

        bool isLastLine = !hasNextLine;

        SetButtonVisible(nextButton, hasNextLine);
        SetButtonVisible(
            acceptButton,
            isLastLine && currentState == TownMissionResidentState.NotAccepted
        );
        SetButtonVisible(
            claimRewardButton,
            isLastLine && currentState == TownMissionResidentState.ReadyToReport
        );
        SetButtonVisible(closeButton, true);
    }

    private void ApplyStaticUI(TownMissionResidentDialogueData dialogueData)
    {
        if (residentNameText != null)
        {
            residentNameText.text = dialogueData.ResidentName;
        }

        if (portraitImage != null)
        {
            portraitImage.sprite = dialogueData.Portrait;
            portraitImage.enabled = dialogueData.Portrait != null;
        }

        SetButtonText(nextButtonLabel, dialogueData.NextButtonText);
        SetButtonText(acceptButtonLabel, dialogueData.AcceptButtonText);
        SetButtonText(claimRewardButtonLabel, dialogueData.ClaimRewardButtonText);
        SetButtonText(closeButtonLabel, dialogueData.CloseButtonText);
    }

    private bool CanFitItemRewards(
        IReadOnlyList<TownMissionResidentRewardItem> itemRewards,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (!HasValidItemReward(itemRewards))
        {
            return true;
        }

        if (rewardInventoryController == null ||
            rewardInventoryController.Grid == null)
        {
            resultMessage =
                "アイテム報酬の入れ先InventoryControllerが設定されていません。TownMissionResidentDialogueControllerのReward Inventory Controllerを設定してください。";
            return false;
        }

        InventoryGrid grid = rewardInventoryController.Grid;
        bool[,] occupied = BuildOccupiedMap(grid);

        foreach (TownMissionResidentRewardItem reward in itemRewards)
        {
            if (reward == null || reward.ItemData == null || reward.Amount <= 0)
            {
                continue;
            }

            ItemData itemData = reward.ItemData;
            int remainingAmount = reward.Amount;
            int maxStack = Mathf.Max(1, itemData.MaxStack);

            if (itemData.CanStack)
            {
                int stackSpace = CountExistingStackSpace(grid, itemData);
                int stacked = Mathf.Min(stackSpace, remainingAmount);
                remainingAmount -= stacked;
            }

            while (remainingAmount > 0)
            {
                if (!TryReserveRewardItemSpace(grid, occupied, itemData))
                {
                    resultMessage =
                        $"報酬アイテム {itemData.DisplayName} を入れる空きがありません。インベントリを空けてから報告してください。";
                    return false;
                }

                remainingAmount -= Mathf.Min(maxStack, remainingAmount);
            }
        }

        return true;
    }

    private bool GrantItemRewards(
        IReadOnlyList<TownMissionResidentRewardItem> itemRewards,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (!HasValidItemReward(itemRewards))
        {
            return true;
        }

        if (rewardInventoryController == null)
        {
            resultMessage = "報酬アイテムの入れ先InventoryControllerが見つかりません。";
            return false;
        }

        foreach (TownMissionResidentRewardItem reward in itemRewards)
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
        }

        return true;
    }

    private bool HasValidItemReward(
        IReadOnlyList<TownMissionResidentRewardItem> itemRewards)
    {
        if (itemRewards == null)
        {
            return false;
        }

        foreach (TownMissionResidentRewardItem reward in itemRewards)
        {
            if (reward != null && reward.ItemData != null && reward.Amount > 0)
            {
                return true;
            }
        }

        return false;
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

    private int CountExistingStackSpace(InventoryGrid grid, ItemData itemData)
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
        ItemData itemData)
    {
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

    private string BuildRewardMessage(
        TownMissionResidentDialogueData dialogueData,
        int moneyReward)
    {
        if (dialogueData == null || dialogueData.Mission == null)
        {
            return "報酬を受け取りました。";
        }

        List<string> parts = new List<string>();

        if (moneyReward > 0)
        {
            parts.Add($"所持金 +¥{moneyReward}");
        }

        foreach (TownMissionResidentRewardItem reward in dialogueData.ItemRewards)
        {
            if (reward == null || reward.ItemData == null || reward.Amount <= 0)
            {
                continue;
            }

            parts.Add($"{reward.ItemData.DisplayName} ×{reward.Amount}");
        }

        if (parts.Count == 0)
        {
            return $"{dialogueData.Mission.DisplayName} を報告しました。";
        }

        return $"{dialogueData.Mission.DisplayName} の報酬を受け取りました。{string.Join(" / ", parts)}";
    }

    private void SetupButtons()
    {
        RemoveButtonListeners();

        if (nextButton != null)
        {
            nextButton.onClick.AddListener(NextLine);
        }

        if (acceptButton != null)
        {
            acceptButton.onClick.AddListener(AcceptMission);
        }

        if (claimRewardButton != null)
        {
            claimRewardButton.onClick.AddListener(ClaimReward);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(CloseDialogue);
        }
    }

    private void RemoveButtonListeners()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(NextLine);
        }

        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveListener(AcceptMission);
        }

        if (claimRewardButton != null)
        {
            claimRewardButton.onClick.RemoveListener(ClaimReward);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseDialogue);
        }
    }

    private GameSessionManager FindSessionManager()
    {
        if (GameSessionManager.Instance != null)
        {
            return GameSessionManager.Instance;
        }

        return FindAnyObjectByType<GameSessionManager>();
    }

    private static string GetMissionId(MissionDefinition2D mission)
    {
        return mission != null
            ? (mission.MissionId ?? string.Empty).Trim()
            : string.Empty;
    }

    private void SetPanelVisible(bool visible)
    {
        if (dialoguePanel != null)
        {
            dialoguePanel.SetActive(visible);
        }
    }

    private void SetButtonVisible(Button button, bool visible)
    {
        if (button != null)
        {
            button.gameObject.SetActive(visible);
        }
    }

    private void SetButtonText(TMP_Text label, string text)
    {
        if (label != null)
        {
            label.text = text ?? string.Empty;
        }
    }

    private void SetStatusMessage(string message, bool warning)
    {
        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (warning)
        {
            LogWarning(message);
        }
        else
        {
            Log(message);
        }
    }

    private void ClearStatusMessage()
    {
        if (statusText != null)
        {
            statusText.text = string.Empty;
            statusText.gameObject.SetActive(false);
        }
    }

    private void LogState(string header)
    {
        if (!showDebugLogs || currentDialogue == null)
        {
            return;
        }

        MissionDefinition2D mission = currentDialogue.Mission;
        string missionId = GetMissionId(mission);
        string sessionDetail = "Sessionなし";
        GameSessionManager session = FindSessionManager();

        if (session != null &&
            !string.IsNullOrWhiteSpace(missionId) &&
            session.TryGetMissionSession(missionId, out MissionSessionData data) &&
            data != null)
        {
            sessionDetail =
                $"SessionState={data.State}, Progress={data.Progress}/{Mathf.Max(1, data.RequiredAmount)}, RewardClaimed={data.RewardClaimed}";
        }
        else if (session != null)
        {
            sessionDetail = "Session内に対象MissionIdなし";
        }

        Debug.Log(
            $"[TownMissionResidentDialogue] {header}: " +
            $"Resident={currentDialogue.ResidentName}, " +
            $"Mission={(mission != null ? mission.DisplayName : "未設定")}, " +
            $"MissionId={missionId}, " +
            $"表示State={currentState}, " +
            sessionDetail,
            this
        );
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[TownMissionResidentDialogue] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[TownMissionResidentDialogue] {message}", this);
    }
}
