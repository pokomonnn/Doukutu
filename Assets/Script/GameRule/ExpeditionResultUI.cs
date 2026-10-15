using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Result SceneのUIへExpeditionResultSessionの内容を表示します。
/// 救出報酬に加えて、出撃パネルから受注したMissionの報酬も処理します。
/// </summary>
[DisallowMultipleComponent]
public class ExpeditionResultUI : MonoBehaviour
{
    [Header("結果表示")]
    [SerializeField] private TMP_Text rescuedNpcCountText;
    [SerializeField] private TMP_Text deadNpcCountText;
    [SerializeField] private TMP_Text recoveredItemBoxCountText;
    [SerializeField] private TMP_Text treasureListText;

    [Header("救出報酬")]
    [Tooltip("救出報酬の合計を表示するTextです。未設定でも報酬加算自体は行われます。")]
    [SerializeField] private TMP_Text rescueRewardText;

    [Tooltip("生存している救出者1人あたりの報酬です。")]
    [SerializeField, Min(0)] private int aliveRescueRewardPerPerson = 500;

    [Tooltip("死亡している救出者1人あたりの報酬です。")]
    [SerializeField, Min(0)] private int deadRescueRewardPerPerson = 50;

    [Tooltip("Result Sceneを開いた時に救出報酬を所持金へ自動加算します。")]
    [SerializeField] private bool grantRescueRewardOnStart = true;

    [Tooltip("救出報酬の表示形式です。{0}=合計報酬")]
    [SerializeField] private string rescueRewardFormat = "{0:N0} G";

    [Header("出撃ミッション報酬")]
    [Tooltip(
        "今回、出撃パネルから受注したMissionの達成状況を表示するTextです。" +
        "未設定でも報酬処理は行えます。"
    )]
    [SerializeField] private TMP_Text expeditionMissionListText;

    [Tooltip("達成した出撃MissionのMoney報酬合計を表示します。")]
    [SerializeField] private TMP_Text expeditionMissionRewardText;

    [Tooltip("Result Sceneを開いた時に、達成済みの出撃Mission報酬を自動受取します。")]
    [SerializeField] private bool grantExpeditionMissionRewardsOnStart = true;

    [Tooltip("出撃MissionのMoney報酬合計表示です。{0}=合計")]
    [SerializeField]
    private string expeditionMissionRewardFormat =
        "{0:N0} G";

    [Header("洞窟で回収した現金")]
    [Tooltip("プレイヤーのバッグから持ち帰り、町の所持金へ移した現金の表示欄です。")]
    [SerializeField] private TMP_Text recoveredMoneyText;

    [Tooltip("{0}=今回持ち帰ったGの合計です。")]
    [SerializeField] private string recoveredMoneyFormat = "{0:N0} G";

    [Header("合計報酬")]
    [Tooltip(
        "回収現金 + 救出報酬 + 達成した出撃MissionのMoney報酬を表示するTextです。" +
        "未設定でも集計自体は行われます。"
    )]
    [SerializeField] private TMP_Text totalRewardText;

    [Tooltip("合計報酬の表示形式です。{0}=回収現金と救出報酬とMission報酬を足した合計")]
    [SerializeField]
    private string totalRewardFormat = "{0:N0} G";

    [Tooltip("今回の出撃Missionが1件も無い時の表示です。")]
    [SerializeField]
    private string noExpeditionMissionMessage =
        "出撃ミッションなし";

    [Tooltip("Mission達成時の先頭記号です。")]
    [SerializeField] private string missionCompletedMark = "✓";

    [Tooltip("Mission未達成時の先頭記号です。")]
    [SerializeField] private string missionFailedMark = "✗";

    [Tooltip("報酬受取済みの表示です。")]
    [SerializeField] private string missionRewardClaimedLabel = "報酬受取済み";

    [Tooltip("達成したが報酬をまだ受け取れていない時の表示です。")]
    [SerializeField] private string missionRewardPendingLabel = "報酬未受取";

    [Tooltip("未達成Missionの表示です。")]
    [SerializeField] private string missionIncompleteLabel = "未達成";

    [Tooltip(
        "Collect ItemミッションでResult時に回収品を消費した場合の表示です。" +
        "{0}=Item名、{1}=消費数"
    )]
    [SerializeField]
    private string collectedItemSubmittedFormat =
        "回収品提出: {0} × {1}";

    [Header("表示形式")]
    [SerializeField] private string rescuedNpcFormat = "{0} 人";
    [SerializeField] private string deadNpcFormat = "{0} 人";
    [SerializeField] private string recoveredItemBoxFormat = "{0} 個";

    [Tooltip("お宝が1つも無い時の表示です。")]
    [SerializeField] private string noTreasureMessage = "お宝なし";

    [Tooltip("お宝1種類ごとの表示形式です。{0}=名前、{1}=個数")]
    [SerializeField] private string treasureLineFormat = "{0} × {1}";

    [Header("墓場への死亡者記録")]
    [Tooltip(
        "ONならTown_Mainへ戻る直前に、今回の死亡NPC数を墓場の累計へ加算します。" +
        "通常はONで使用してください。"
    )]
    [SerializeField] private bool recordDeadNpcCountWhenReturningTown = true;

    [Header("町へ戻る")]
    [SerializeField] private Button returnTownButton;

    [Tooltip("町Scene名です。")]
    [SerializeField] private string townSceneName = "Town_Main";

    [Tooltip(
        "町へ移動する直前にResultの一時データを破棄します。" +
        "通常はONでOKです。"
    )]
    [SerializeField] private bool clearResultWhenReturningTown = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    // 同じExpeditionResultDataを使ってResult Sceneを再読込した場合に、
    // 救出報酬が二重加算されるのを防ぎます。
    private static ExpeditionResultData lastRewardedResult;

    public int CurrentRescueReward { get; private set; }
    public int CurrentExpeditionMissionMoneyReward { get; private set; }
    public int CurrentTotalReward { get; private set; }
    public int CurrentRecoveredMoney { get; private set; }
    public int GrantedExpeditionMissionMoneyReward { get; private set; }

    public bool RescueRewardGranted { get; private set; }
    public bool ExpeditionMissionRewardsProcessed { get; private set; }
    public bool DeadNpcCountRecordedToGraveyard { get; private set; }

    private bool returnToTownStarted;

    private void Awake()
    {
        SetupButton();
    }

    private void Start()
    {
        if (grantRescueRewardOnStart)
        {
            TryGrantRescueReward();
        }

        if (grantExpeditionMissionRewardsOnStart)
        {
            TryGrantExpeditionMissionRewards();
        }

        RefreshUI();
    }

    private void OnDestroy()
    {
        if (returnTownButton != null)
        {
            returnTownButton.onClick.RemoveListener(ReturnToTown);
        }
    }

    [ContextMenu("Refresh Result UI")]
    public void RefreshUI()
    {
        ExpeditionResultData result =
            ExpeditionResultSession.Current ??
            new ExpeditionResultData();

        SetText(
            rescuedNpcCountText,
            string.Format(
                rescuedNpcFormat,
                Mathf.Max(0, result.RescuedNpcCount)
            )
        );

        SetText(
            deadNpcCountText,
            string.Format(
                deadNpcFormat,
                Mathf.Max(0, result.DeadNpcCount)
            )
        );

        SetText(
            recoveredItemBoxCountText,
            string.Format(
                recoveredItemBoxFormat,
                Mathf.Max(0, result.RecoveredItemBoxCount)
            )
        );

        SetText(
            treasureListText,
            BuildTreasureText(result)
        );

        CurrentRescueReward = CalculateRescueReward(result);

        SetText(
            rescueRewardText,
            string.Format(
                rescueRewardFormat,
                CurrentRescueReward
            )
        );

        CurrentExpeditionMissionMoneyReward =
            CalculateExpeditionMissionMoneyReward();

        SetText(
            expeditionMissionRewardText,
            string.Format(
                expeditionMissionRewardFormat,
                CurrentExpeditionMissionMoneyReward
            )
        );

        CurrentRecoveredMoney = ExpeditionRecoveredMoneySession.GetAmount(
            ExpeditionResultSession.Current
        );

        SetText(
            recoveredMoneyText,
            string.Format(recoveredMoneyFormat, CurrentRecoveredMoney)
        );

        CurrentTotalReward = CalculateTotalReward(
            CurrentRescueReward,
            CurrentExpeditionMissionMoneyReward,
            CurrentRecoveredMoney
        );

        SetText(
            totalRewardText,
            string.Format(
                totalRewardFormat,
                CurrentTotalReward
            )
        );

        SetText(
            expeditionMissionListText,
            BuildExpeditionMissionResultText()
        );

        if (showDebugLogs)
        {
            Debug.Log(
                $"[ExpeditionResultUI] 表示更新: " +
                $"救出={result.RescuedNpcCount}, " +
                $"死亡={result.DeadNpcCount}, " +
                $"ItemBox={result.RecoveredItemBoxCount}, " +
                $"Treasure={result.TreasureItems?.Count ?? 0}, " +
                $"救出報酬={CurrentRescueReward:N0}G, " +
                $"出撃MissionMoney={CurrentExpeditionMissionMoneyReward:N0}G, " +
                $"回収現金={CurrentRecoveredMoney:N0}G, " +
                $"合計報酬={CurrentTotalReward:N0}G",
                this
            );
        }
    }

    /// <summary>
    /// 現在の探索結果から救出報酬を計算します。
    /// 生存者 × aliveRescueRewardPerPerson + 死亡者 × deadRescueRewardPerPerson。
    /// </summary>
    public int CalculateRescueReward(ExpeditionResultData result)
    {
        if (result == null)
        {
            return 0;
        }

        long aliveReward =
            (long)Mathf.Max(0, result.RescuedNpcCount) *
            Mathf.Max(0, aliveRescueRewardPerPerson);

        long deadReward =
            (long)Mathf.Max(0, result.DeadNpcCount) *
            Mathf.Max(0, deadRescueRewardPerPerson);

        long total = aliveReward + deadReward;

        return total > int.MaxValue
            ? int.MaxValue
            : (int)total;
    }


    /// <summary>
    /// Result画面に表示するMoney報酬の総合計です。
    /// 救出報酬 + 達成した出撃MissionのMoney報酬を合算します。
    /// この値は表示用で、所持金へ再加算はしません。
    /// </summary>
    // 従来の2引数版は互換用として残します。
    public int CalculateTotalReward(
        int rescueReward,
        int expeditionMissionMoneyReward)
    {
        return CalculateTotalReward(rescueReward, expeditionMissionMoneyReward, 0);
    }

    public int CalculateTotalReward(
        int rescueReward,
        int expeditionMissionMoneyReward,
        int recoveredMoney)
    {
        long total =
            (long)Mathf.Max(0, rescueReward) +
            Mathf.Max(0, expeditionMissionMoneyReward) +
            Mathf.Max(0, recoveredMoney);

        return total > int.MaxValue
            ? int.MaxValue
            : (int)total;
    }

    /// <summary>
    /// 救出報酬をGameSessionManagerの所持金へ1回だけ加算します。
    /// </summary>
    public bool TryGrantRescueReward()
    {
        ExpeditionResultData result = ExpeditionResultSession.Current;

        if (result == null)
        {
            LogWarning("探索結果が無いため、救出報酬を加算できませんでした。");
            return false;
        }

        CurrentRescueReward = CalculateRescueReward(result);

        if (lastRewardedResult == result)
        {
            RescueRewardGranted = true;
            Log("この探索結果の救出報酬はすでに加算済みです。二重加算を防止しました。");
            return false;
        }

        if (CurrentRescueReward <= 0)
        {
            lastRewardedResult = result;
            RescueRewardGranted = true;
            Log("救出報酬は0Gです。所持金への加算はありません。");
            return true;
        }

        GameSessionManager session = FindSessionManager();

        if (session == null)
        {
            LogWarning(
                "GameSessionManager.Instanceが見つからないため、" +
                $"救出報酬 {CurrentRescueReward:N0}G を加算できませんでした。"
            );
            return false;
        }

        if (!session.AddMoney(CurrentRescueReward))
        {
            LogWarning(
                $"救出報酬 {CurrentRescueReward:N0}G の加算に失敗しました。"
            );
            return false;
        }

        lastRewardedResult = result;
        RescueRewardGranted = true;

        Log(
            $"救出報酬を加算しました。" +
            $"生存={Mathf.Max(0, result.RescuedNpcCount)}人 × {aliveRescueRewardPerPerson:N0}G / " +
            $"死亡={Mathf.Max(0, result.DeadNpcCount)}人 × {deadRescueRewardPerPerson:N0}G / " +
            $"合計={CurrentRescueReward:N0}G / " +
            $"現在所持金={session.CurrentMoney:N0}G"
        );

        return true;
    }

    /// <summary>
    /// 今回の出撃で達成したExpedition Missionだけ報酬を受け取ります。
    /// RewardClaimedで二重受取を防止します。
    /// </summary>
    [ContextMenu("Grant Expedition Mission Rewards")]
    public bool TryGrantExpeditionMissionRewards()
    {
        GameSessionManager session = FindSessionManager();

        if (session == null)
        {
            LogWarning(
                "GameSessionManagerが見つからないため、出撃Mission報酬を処理できません。"
            );
            return false;
        }

        IReadOnlyList<string> missionIds =
            session.CurrentExpeditionMissionIds;

        GrantedExpeditionMissionMoneyReward = 0;

        if (missionIds == null || missionIds.Count == 0)
        {
            ExpeditionMissionRewardsProcessed = true;
            Log("今回の出撃Missionはありません。");
            return true;
        }

        bool allProcessableRewardsGranted = true;

        // 処理中にSessionイベントが発火しても列挙が崩れないようIDをコピーします。
        List<string> ids =
            new List<string>(missionIds);

        foreach (string missionId in ids)
        {
            if (!session.TryGetMissionSession(
                    missionId,
                    out MissionSessionData data) ||
                data == null)
            {
                LogWarning(
                    $"Result対象MissionのSessionDataが見つかりません: {missionId}"
                );
                continue;
            }

            if (data.AcceptSource != MissionAcceptSource.Expedition)
            {
                // 会話受注MissionはResult報酬の対象外です。
                continue;
            }

            bool completed =
                data.State == MissionSessionState.Completed ||
                data.Progress >= Mathf.Max(1, data.RequiredAmount);

            if (!completed || data.RewardClaimed)
            {
                continue;
            }

            MissionDefinition2D mission = data.Mission;

            if (mission == null)
            {
                allProcessableRewardsGranted = false;
                LogWarning(
                    $"MissionDefinition2D参照が無いため報酬を付与できません: {data.DisplayName}"
                );
                continue;
            }

            if (session.TryClaimExpeditionMissionReward(
                    mission,
                    out int grantedMoney,
                    out string resultMessage))
            {
                GrantedExpeditionMissionMoneyReward +=
                    Mathf.Max(0, grantedMoney);

                Log(resultMessage);
            }
            else
            {
                allProcessableRewardsGranted = false;
                LogWarning(resultMessage);
            }
        }

        ExpeditionMissionRewardsProcessed = true;
        RefreshUI();

        return allProcessableRewardsGranted;
    }

    public int CalculateExpeditionMissionMoneyReward()
    {
        GameSessionManager session = FindSessionManager();

        if (session == null ||
            session.CurrentExpeditionMissionIds == null)
        {
            return 0;
        }

        long total = 0;

        foreach (string missionId in
                 session.CurrentExpeditionMissionIds)
        {
            if (!session.TryGetMissionSession(
                    missionId,
                    out MissionSessionData data) ||
                data == null ||
                data.AcceptSource != MissionAcceptSource.Expedition)
            {
                continue;
            }

            bool completed =
                data.State == MissionSessionState.Completed ||
                data.Progress >= Mathf.Max(1, data.RequiredAmount);

            if (!completed || data.Mission == null)
            {
                continue;
            }

            total += Mathf.Max(
                0,
                data.Mission.ExpeditionMoneyReward
            );

            if (total >= int.MaxValue)
            {
                return int.MaxValue;
            }
        }

        return (int)total;
    }

    private string BuildExpeditionMissionResultText()
    {
        GameSessionManager session = FindSessionManager();

        if (session == null ||
            session.CurrentExpeditionMissionIds == null ||
            session.CurrentExpeditionMissionIds.Count == 0)
        {
            return noExpeditionMissionMessage;
        }

        StringBuilder builder = new StringBuilder();

        foreach (string missionId in
                 session.CurrentExpeditionMissionIds)
        {
            if (!session.TryGetMissionSession(
                    missionId,
                    out MissionSessionData data) ||
                data == null)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            bool completed =
                data.State == MissionSessionState.Completed ||
                data.Progress >= Mathf.Max(1, data.RequiredAmount);

            string mark = completed
                ? missionCompletedMark
                : missionFailedMark;

            string missionName =
                !string.IsNullOrWhiteSpace(data.DisplayName)
                    ? data.DisplayName
                    : missionId;

            builder.Append(mark);
            builder.Append(' ');
            builder.Append(missionName);
            builder.Append("  ");

            if (!completed)
            {
                builder.Append(missionIncompleteLabel);
                continue;
            }

            builder.Append(
                data.RewardClaimed
                    ? missionRewardClaimedLabel
                    : missionRewardPendingLabel
            );

            MissionDefinition2D mission = data.Mission;

            if (mission == null)
            {
                continue;
            }

            List<string> rewardTexts =
                new List<string>();

            if (data.RewardClaimed &&
                mission.ObjectiveType == MissionObjectiveType2D.CollectItem &&
                mission.ConsumeCollectedItemsOnResult &&
                mission.RequiredItem != null)
            {
                rewardTexts.Add(
                    string.Format(
                        collectedItemSubmittedFormat,
                        mission.RequiredItem.DisplayName,
                        mission.RequiredAmount
                    )
                );
            }

            if (mission.ExpeditionMoneyReward > 0)
            {
                rewardTexts.Add(
                    $"+{mission.ExpeditionMoneyReward:N0}G"
                );
            }

            if (mission.ExpeditionItemRewards != null)
            {
                foreach (MissionRewardItem reward in
                         mission.ExpeditionItemRewards)
                {
                    if (reward == null ||
                        reward.ItemData == null ||
                        reward.Amount <= 0)
                    {
                        continue;
                    }

                    rewardTexts.Add(
                        $"{reward.ItemData.DisplayName}×{reward.Amount}"
                    );
                }
            }

            if (rewardTexts.Count > 0)
            {
                builder.Append(" / ");
                builder.Append(string.Join(" / ", rewardTexts));
            }
        }

        return builder.Length > 0
            ? builder.ToString()
            : noExpeditionMissionMessage;
    }

    /// <summary>
    /// 今回の探索で死亡状態のまま回収されたNPC数を、
    /// GameSessionManagerの墓場累計へ1回だけ加算します。
    /// </summary>
    public bool TryRecordDeadNpcCountToGraveyard()
    {
        if (DeadNpcCountRecordedToGraveyard)
        {
            return true;
        }

        ExpeditionResultData result = ExpeditionResultSession.Current;

        if (result == null)
        {
            LogWarning(
                "探索結果が無いため、墓場へ死亡NPC数を記録できませんでした。"
            );
            return false;
        }

        int deadNpcCount = Mathf.Max(0, result.DeadNpcCount);

        if (deadNpcCount <= 0)
        {
            DeadNpcCountRecordedToGraveyard = true;
            Log("今回の死亡NPCは0人のため、墓場の累計は変更しません。");
            return true;
        }

        GameSessionManager session = FindSessionManager();

        if (session == null)
        {
            LogWarning(
                "GameSessionManager.Instanceが見つからないため、" +
                $"死亡NPC {deadNpcCount:N0}人を墓場へ記録できませんでした。"
            );
            return false;
        }

        if (!session.AddDeadNpcCount(deadNpcCount))
        {
            LogWarning(
                $"死亡NPC {deadNpcCount:N0}人の墓場登録に失敗しました。"
            );
            return false;
        }

        DeadNpcCountRecordedToGraveyard = true;

        Log(
            $"墓場へ死亡NPCを記録しました。" +
            $"今回={deadNpcCount:N0}人 / " +
            $"累計={session.TotalDeadNpcCount:N0}人"
        );

        return true;
    }

    public void ReturnToTown()
    {
        if (returnToTownStarted)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(townSceneName))
        {
            Debug.LogWarning(
                "[ExpeditionResultUI] Town Scene Nameが空です。",
                this
            );
            return;
        }

        if (recordDeadNpcCountWhenReturningTown &&
            !TryRecordDeadNpcCountToGraveyard())
        {
            LogWarning(
                "墓場への死亡NPC記録が完了していない可能性がありますが、Town_Mainへの移動は続行します。"
            );
        }

        returnToTownStarted = true;

        GameSessionManager session = FindSessionManager();

        if (session != null)
        {
            session.EndCurrentExpedition();
        }

        if (clearResultWhenReturningTown)
        {
            ExpeditionResultSession.Clear();
        }

        ExpeditionRecoveredMoneySession.Clear();
        SceneManager.LoadScene(townSceneName);
    }

    private string BuildTreasureText(
        ExpeditionResultData result)
    {
        if (result == null ||
            result.TreasureItems == null ||
            result.TreasureItems.Count == 0)
        {
            return noTreasureMessage;
        }

        StringBuilder builder = new StringBuilder();

        foreach (ExpeditionTreasureResult treasure
                 in result.TreasureItems)
        {
            if (treasure == null ||
                treasure.ItemData == null ||
                treasure.Amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendFormat(
                treasureLineFormat,
                treasure.DisplayName,
                treasure.Amount
            );
        }

        return builder.Length > 0
            ? builder.ToString()
            : noTreasureMessage;
    }

    private void SetupButton()
    {
        if (returnTownButton == null)
        {
            return;
        }

        returnTownButton.onClick.RemoveListener(ReturnToTown);
        returnTownButton.onClick.AddListener(ReturnToTown);
    }

    private static GameSessionManager FindSessionManager()
    {
        GameSessionManager session =
            GameSessionManager.Instance;

        if (session == null)
        {
            session =
                FindAnyObjectByType<GameSessionManager>(
                    FindObjectsInactive.Include
                );
        }

        return session;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ExpeditionResultUI] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[ExpeditionResultUI] {message}", this);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private void OnValidate()
    {
        aliveRescueRewardPerPerson =
            Mathf.Max(0, aliveRescueRewardPerPerson);

        deadRescueRewardPerPerson =
            Mathf.Max(0, deadRescueRewardPerPerson);

        if (string.IsNullOrWhiteSpace(rescueRewardFormat))
        {
            rescueRewardFormat = "{0:N0} G";
        }

        if (string.IsNullOrWhiteSpace(
                expeditionMissionRewardFormat))
        {
            expeditionMissionRewardFormat = "{0:N0} G";
        }

        if (string.IsNullOrWhiteSpace(totalRewardFormat))
        {
            totalRewardFormat = "{0:N0} G";
        }

        if (string.IsNullOrWhiteSpace(recoveredMoneyFormat))
        {
            recoveredMoneyFormat = "{0:N0} G";
        }
    }
}
