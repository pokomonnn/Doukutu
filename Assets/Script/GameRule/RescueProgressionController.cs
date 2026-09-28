using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

/// <summary>
/// Result Sceneで今回の「生存救出」を永久進捗へ確定します。
///
/// ・生存救出人数を累計へ加算
/// ・累計3 / 6 / 10 / 15人などの節目でStory Flagや施設Lvを解放
/// ・特殊NPCを初めて救出した時に個別のStory Flag / 施設Lvを解放
///
/// 救出時のお金は既存ExpeditionResultUIが担当するため、
/// このComponentでは二重に加算しません。
/// </summary>
[DisallowMultipleComponent]
public class RescueProgressionController : MonoBehaviour
{
    [Serializable]
    public class RescueCountMilestone
    {
        [Tooltip("この累計救出人数以上になったら解放します。")]
        [InspectorName("必要救出人数")]
        [Min(1)]
        public int RequiredRescueCount = 3;

        [Tooltip("Resultの解放通知に表示する名前です。")]
        [InspectorName("表示名")]
        public string DisplayName = "救出3人達成";

        [Tooltip(
            "達成時にONにするStory Flagです。" +
            "町の機能解放やストーリー条件に利用できます。"
        )]
        [InspectorName("解放Story Flag ID")]
        public string UnlockStoryFlagId =
            "rescue_total_3";

        [Tooltip(
            "達成時に施設Lvを上げる場合のFacility IDです。" +
            "不要なら空欄にしてください。"
        )]
        [InspectorName("施設ID（任意）")]
        public string FacilityId = string.Empty;

        [Tooltip("施設IDを設定した場合、このレベル以上へ引き上げます。")]
        [InspectorName("解放後施設レベル")]
        [Min(1)]
        public int FacilityLevel = 1;

        public RescueCountMilestone()
        {
        }

        public RescueCountMilestone(
            int requiredRescueCount,
            string displayName,
            string unlockStoryFlagId)
        {
            RequiredRescueCount =
                Mathf.Max(
                    1,
                    requiredRescueCount
                );

            DisplayName =
                displayName ?? string.Empty;

            UnlockStoryFlagId =
                unlockStoryFlagId ??
                string.Empty;
        }
    }

    [Header("処理")]
    [Tooltip("ONならResult Scene開始時に今回の救出進捗を自動確定します。")]
    [InspectorName("開始時に自動確定")]
    [SerializeField] private bool processOnStart = true;

    [Tooltip(
        "初めて1人以上を生存救出した時にONにするStory Flagです。" +
        "不要なら空欄にしてください。"
    )]
    [InspectorName("初救出Story Flag ID")]
    [SerializeField] private string firstRescueStoryFlagId =
        "rescue_first_completed";

    [Header("累計救出人数の解放")]
    [SerializeField]
    private List<RescueCountMilestone> milestones =
        new List<RescueCountMilestone>
        {
            new RescueCountMilestone(
                3,
                "累計3人救出",
                "rescue_total_3"
            ),
            new RescueCountMilestone(
                6,
                "累計6人救出",
                "rescue_total_6"
            ),
            new RescueCountMilestone(
                10,
                "累計10人救出",
                "rescue_total_10"
            ),
            new RescueCountMilestone(
                15,
                "累計15人救出",
                "rescue_total_15"
            )
        };

    [Header("Result表示（任意）")]
    [Tooltip("現在の累計救出人数を表示するTextです。未設定でも進捗処理は動作します。")]
    [InspectorName("累計救出人数Text")]
    [SerializeField] private TMP_Text totalRescuedNpcCountText;

    [Tooltip("今回新しく解放された内容を表示するTextです。未設定でも進捗処理は動作します。")]
    [InspectorName("新規解放Text")]
    [SerializeField] private TMP_Text unlockNotificationText;

    [InspectorName("累計表示形式")]
    [SerializeField] private string totalRescueFormat =
        "累計救出人数：{0}人";

    [InspectorName("新規解放なし表示")]
    [SerializeField] private string noNewUnlockMessage =
        "新しい解放はありません";

    [Header("デバッグ")]
    [InspectorName("救出進捗ログを表示")]
    [SerializeField] private bool showDebugLogs = true;

    // 同じExpeditionResultDataを使ってResult Sceneを再初期化しても、
    // 同一ランタイム中に累計人数を二重加算しないためのガードです。
    private static ExpeditionResultData lastProcessedResult;

    public bool ProgressProcessed { get; private set; }
    public int AddedRescueCount { get; private set; }
    public int CurrentTotalRescuedNpcCount { get; private set; }

    private readonly List<string> newlyUnlockedLabels =
        new List<string>();

    private void Start()
    {
        if (processOnStart)
        {
            ProcessCurrentResult();
        }
        else
        {
            RefreshDisplay();
        }
    }

    /// <summary>
    /// 現在のExpeditionResultSessionを救出進捗へ反映します。
    /// </summary>
    [ContextMenu("今回の救出進捗を確定")]
    public bool ProcessCurrentResult()
    {
        ExpeditionResultData result =
            ExpeditionResultSession.Current;

        if (result == null)
        {
            LogWarning(
                "ExpeditionResultSession.Currentが無いため、救出進捗を確定できません。"
            );

            RefreshDisplay();
            return false;
        }

        GameSessionManager session =
            FindSessionManager();

        if (session == null)
        {
            LogWarning(
                "GameSessionManagerが見つからないため、救出進捗を確定できません。"
            );

            RefreshDisplay();
            return false;
        }

        // 同じResultをもう一度処理しようとした場合は、
        // 表示だけ更新して二重加算を防ぎます。
        if (lastProcessedResult == result)
        {
            ProgressProcessed = true;
            AddedRescueCount = 0;
            CurrentTotalRescuedNpcCount =
                session.TotalRescuedNpcCount;

            RefreshDisplay();

            Log(
                "この探索結果の救出進捗はすでに確定済みです。二重加算しません。"
            );

            return true;
        }

        newlyUnlockedLabels.Clear();

        AddedRescueCount =
            Mathf.Max(
                0,
                result.RescuedNpcCount
            );

        if (AddedRescueCount > 0)
        {
            session.AddRescuedNpcCount(
                AddedRescueCount
            );
        }

        CurrentTotalRescuedNpcCount =
            session.TotalRescuedNpcCount;

        ProcessFirstRescueFlag(
            session
        );

        ProcessMilestones(
            session
        );

        ProcessSpecialRescues(
            session
        );

        lastProcessedResult =
            result;

        ProgressProcessed = true;

        // 特殊NPCの一時データは永久進捗へ確定したので破棄します。
        RescueProgressExpeditionSession.Clear();

        RefreshDisplay();

        Log(
            $"救出進捗確定: " +
            $"今回生存救出={AddedRescueCount}人 / " +
            $"累計={CurrentTotalRescuedNpcCount}人 / " +
            $"新規解放={newlyUnlockedLabels.Count}件"
        );

        return true;
    }

    private void ProcessFirstRescueFlag(
        GameSessionManager session)
    {
        if (session == null ||
            session.TotalRescuedNpcCount <= 0 ||
            string.IsNullOrWhiteSpace(
                firstRescueStoryFlagId))
        {
            return;
        }

        string flagId =
            firstRescueStoryFlagId.Trim();

        if (session.HasStoryFlag(flagId))
        {
            return;
        }

        if (session.SetStoryFlag(
                flagId,
                true))
        {
            AddUnlockLabel(
                "初めての生存者救出"
            );
        }
    }

    private void ProcessMilestones(
        GameSessionManager session)
    {
        if (session == null ||
            milestones == null)
        {
            return;
        }

        foreach (RescueCountMilestone milestone
                 in milestones)
        {
            if (milestone == null)
            {
                continue;
            }

            int required =
                Mathf.Max(
                    1,
                    milestone.RequiredRescueCount
                );

            if (session.TotalRescuedNpcCount <
                required)
            {
                continue;
            }

            bool newlyUnlocked =
                ApplyUnlock(
                    session,
                    milestone.UnlockStoryFlagId,
                    milestone.FacilityId,
                    milestone.FacilityLevel
                );

            if (newlyUnlocked)
            {
                AddUnlockLabel(
                    string.IsNullOrWhiteSpace(
                        milestone.DisplayName)
                        ? $"累計{required}人救出"
                        : milestone.DisplayName.Trim()
                );
            }
        }
    }

    private void ProcessSpecialRescues(
        GameSessionManager session)
    {
        if (session == null)
        {
            return;
        }

        IReadOnlyList<
            RescueProgressExpeditionSession.PendingSpecialRescue
        > pending =
            RescueProgressExpeditionSession
                .PendingSpecialRescues;

        if (pending == null)
        {
            return;
        }

        for (int i = 0; i < pending.Count; i++)
        {
            RescueProgressExpeditionSession
                .PendingSpecialRescue data =
                    pending[i];

            if (data == null ||
                string.IsNullOrWhiteSpace(
                    data.SpecialNpcId))
            {
                continue;
            }

            bool firstTime =
                session.RegisterRescuedSpecialNpc(
                    data.SpecialNpcId
                );

            // 既にID登録済みでも、設定変更でFlag/施設だけ後から追加できるよう
            // 解放処理そのものは安全に再評価します。
            bool unlockChanged =
                ApplyUnlock(
                    session,
                    data.UnlockStoryFlagId,
                    data.FacilityId,
                    data.FacilityLevel
                );

            if (firstTime || unlockChanged)
            {
                string label =
                    string.IsNullOrWhiteSpace(
                        data.DisplayName)
                        ? data.SpecialNpcId
                        : data.DisplayName.Trim();

                AddUnlockLabel(
                    $"特殊NPC救出：{label}"
                );
            }
        }
    }

    private static bool ApplyUnlock(
        GameSessionManager session,
        string storyFlagId,
        string facilityId,
        int facilityLevel)
    {
        if (session == null)
        {
            return false;
        }

        bool changed = false;

        string normalizedFlag =
            storyFlagId?.Trim() ??
            string.Empty;

        if (!string.IsNullOrWhiteSpace(
                normalizedFlag) &&
            !session.HasStoryFlag(
                normalizedFlag))
        {
            changed |=
                session.SetStoryFlag(
                    normalizedFlag,
                    true
                );
        }

        string normalizedFacility =
            facilityId?.Trim() ??
            string.Empty;

        if (!string.IsNullOrWhiteSpace(
                normalizedFacility))
        {
            int targetLevel =
                Mathf.Max(
                    1,
                    facilityLevel
                );

            int currentLevel =
                session.GetFacilityLevel(
                    normalizedFacility,
                    1
                );

            if (currentLevel < targetLevel)
            {
                changed |=
                    session.SetFacilityLevel(
                        normalizedFacility,
                        targetLevel,
                        1
                    );
            }
        }

        return changed;
    }

    private void AddUnlockLabel(
        string label)
    {
        if (string.IsNullOrWhiteSpace(
                label))
        {
            return;
        }

        string normalized =
            label.Trim();

        if (!newlyUnlockedLabels.Contains(
                normalized))
        {
            newlyUnlockedLabels.Add(
                normalized
            );
        }
    }

    private void RefreshDisplay()
    {
        GameSessionManager session =
            FindSessionManager();

        int total =
            session != null
                ? session.TotalRescuedNpcCount
                : CurrentTotalRescuedNpcCount;

        CurrentTotalRescuedNpcCount =
            Mathf.Max(0, total);

        if (totalRescuedNpcCountText != null)
        {
            totalRescuedNpcCountText.text =
                string.Format(
                    string.IsNullOrWhiteSpace(
                        totalRescueFormat)
                        ? "累計救出人数：{0}人"
                        : totalRescueFormat,
                    CurrentTotalRescuedNpcCount
                );
        }

        if (unlockNotificationText != null)
        {
            if (newlyUnlockedLabels.Count == 0)
            {
                unlockNotificationText.text =
                    noNewUnlockMessage ??
                    string.Empty;
            }
            else
            {
                StringBuilder builder =
                    new StringBuilder();

                builder.AppendLine(
                    "新しく解放されました"
                );

                foreach (string label in
                         newlyUnlockedLabels)
                {
                    builder.Append("・");
                    builder.AppendLine(label);
                }

                unlockNotificationText.text =
                    builder.ToString().TrimEnd();
            }
        }
    }

    private static GameSessionManager FindSessionManager()
    {
        GameSessionManager session =
            GameSessionManager.Instance;

        if (session == null)
        {
            session =
                FindAnyObjectByType<
                    GameSessionManager
                >(
                    FindObjectsInactive.Include
                );
        }

        return session;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log(
                $"[RescueProgressionController] {message}",
                this
            );
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[RescueProgressionController] {message}",
            this
        );
    }

    private void OnValidate()
    {
        firstRescueStoryFlagId =
            firstRescueStoryFlagId?.Trim() ??
            string.Empty;

        if (string.IsNullOrWhiteSpace(
                totalRescueFormat))
        {
            totalRescueFormat =
                "累計救出人数：{0}人";
        }

        if (milestones == null)
        {
            milestones =
                new List<RescueCountMilestone>();
        }

        foreach (RescueCountMilestone milestone
                 in milestones)
        {
            if (milestone == null)
            {
                continue;
            }

            milestone.RequiredRescueCount =
                Mathf.Max(
                    1,
                    milestone.RequiredRescueCount
                );

            milestone.DisplayName =
                milestone.DisplayName?.Trim() ??
                string.Empty;

            milestone.UnlockStoryFlagId =
                milestone.UnlockStoryFlagId?.Trim() ??
                string.Empty;

            milestone.FacilityId =
                milestone.FacilityId?.Trim() ??
                string.Empty;

            milestone.FacilityLevel =
                Mathf.Max(
                    1,
                    milestone.FacilityLevel
                );
        }
    }
}
