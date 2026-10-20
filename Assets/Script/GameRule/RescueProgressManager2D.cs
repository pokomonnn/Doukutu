using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生存状態で町へ持ち帰った救出NPCの累計人数と、
/// 節目／特殊NPC解放をGameSessionManagerへ保存します。
///
/// 新しいSaveGameData項目を追加せず、既存の
/// FacilityLevels と StoryFlags を利用します。
/// </summary>
[DisallowMultipleComponent]
public class RescueProgressManager2D : MonoBehaviour
{
    [Serializable]
    public class RescueMilestone
    {
        [InspectorName("必要救出人数")]
        [Min(1)]
        public int RequiredRescueCount = 3;

        [InspectorName("解放Story Flag")]
        public string StoryFlagId = "rescue_total_3";

        [InspectorName("メモ")]
        public string Note = "累計3人救出";
    }

    [Header("累計救出人数の保存")]
    [InspectorName("累計救出人数 保存ID")]
    [SerializeField]
    private string rescueCountStorageId = "__rescue_total_count";

    [Header("初回救出")]
    [InspectorName("初回救出Story Flag")]
    [SerializeField]
    private string firstRescueStoryFlag = "rescue_first_success";

    [Header("累計人数による町の発展")]
    [SerializeField]
    private List<RescueMilestone> milestones =
        new List<RescueMilestone>
        {
            new RescueMilestone
            {
                RequiredRescueCount = 3,
                StoryFlagId = "rescue_total_3",
                Note = "累計3人"
            },
            new RescueMilestone
            {
                RequiredRescueCount = 6,
                StoryFlagId = "rescue_total_6",
                Note = "累計6人"
            },
            new RescueMilestone
            {
                RequiredRescueCount = 10,
                StoryFlagId = "rescue_total_10",
                Note = "累計10人"
            },
            new RescueMilestone
            {
                RequiredRescueCount = 15,
                StoryFlagId = "rescue_total_15",
                Note = "累計15人"
            }
        };

    [Header("デバッグ")]
    [InspectorName("救出進捗ログを表示")]
    [SerializeField] private bool showDebugLogs = true;

    public int TotalRescuedCount => GetTotalRescuedCount();

    public bool RecordRescuedPeople(
        IReadOnlyList<CarryableObject2D> rescuedTargets)
    {
        GameSessionManager session = GetSession();

        if (session == null)
        {
            LogWarning("GameSessionManagerが見つかりません。");
            return false;
        }

        int rescuedThisExpedition = 0;
        HashSet<int> counted = new HashSet<int>();

        if (rescuedTargets != null)
        {
            for (int i = 0; i < rescuedTargets.Count; i++)
            {
                CarryableObject2D target = rescuedTargets[i];

                if (target == null) continue;
                if (target.GetComponent<StoryFinnDelivery>() != null) continue;

                if (!counted.Add(target.GetInstanceID()))
                {
                    continue;
                }

                StoryRescueIdentity identity = target.GetComponent<StoryRescueIdentity>();
                if (identity != null)
                {
                    var health = target.GetComponent<CharacterHealth>();
                    if (identity.AlreadyRescued || (health != null && health.IsDead)) continue;
                }
                rescuedThisExpedition++;

                ApplySpecialNpcUnlocks(target, session);
            }
        }

        if (rescuedThisExpedition <= 0)
        {
            return true;
        }

        int previousTotal = GetTotalRescuedCount(session);

        long nextLong = (long)previousTotal + rescuedThisExpedition;

        int nextTotal =
            nextLong > int.MaxValue - 1
                ? int.MaxValue - 1
                : (int)nextLong;

        bool saved =
            session.SetFacilityLevel(
                GetStorageId(),
                nextTotal + 1,
                1
            );

        if (!saved)
        {
            LogWarning("累計救出人数の保存に失敗しました。");
            return false;
        }

        if (nextTotal > 0 &&
            !string.IsNullOrWhiteSpace(firstRescueStoryFlag))
        {
            session.SetStoryFlag(firstRescueStoryFlag.Trim(), true);
        }

        ApplyMilestones(previousTotal, nextTotal, session);

        Log(
            $"救出進捗: 今回={rescuedThisExpedition}人 / " +
            $"累計={nextTotal}人"
        );

        return true;
    }

    [ContextMenu("現在の累計人数から解放を再確認")]
    public void RefreshUnlocksFromCurrentTotal()
    {
        GameSessionManager session = GetSession();

        if (session == null)
        {
            LogWarning("GameSessionManagerが見つかりません。");
            return;
        }

        int total = GetTotalRescuedCount(session);

        if (total > 0 &&
            !string.IsNullOrWhiteSpace(firstRescueStoryFlag))
        {
            session.SetStoryFlag(firstRescueStoryFlag.Trim(), true);
        }

        ApplyMilestones(0, total, session);
        Log($"救出解放を再確認しました。累計={total}人");
    }

    public int GetTotalRescuedCount()
    {
        GameSessionManager session = GetSession();
        return session != null ? GetTotalRescuedCount(session) : 0;
    }

    public bool IsMilestoneUnlocked(string storyFlagId)
    {
        GameSessionManager session = GetSession();

        return session != null &&
               !string.IsNullOrWhiteSpace(storyFlagId) &&
               session.HasStoryFlag(storyFlagId.Trim());
    }

    private int GetTotalRescuedCount(GameSessionManager session)
    {
        if (session == null) return 0;

        int storedLevel =
            session.GetFacilityLevel(GetStorageId(), 1);

        return Mathf.Max(0, storedLevel - 1);
    }

    private void ApplyMilestones(
        int previousTotal,
        int currentTotal,
        GameSessionManager session)
    {
        if (session == null || milestones == null) return;

        foreach (RescueMilestone milestone in milestones)
        {
            if (milestone == null) continue;

            int required = Mathf.Max(1, milestone.RequiredRescueCount);
            string flag = milestone.StoryFlagId?.Trim() ?? string.Empty;

            if (currentTotal < required ||
                string.IsNullOrWhiteSpace(flag))
            {
                continue;
            }

            bool newlyUnlocked = session.SetStoryFlag(flag, true);

            if (newlyUnlocked)
            {
                Log(
                    $"救出人数Milestone解放: {required}人 / " +
                    $"Flag={flag} / {previousTotal}→{currentTotal}"
                );
            }
        }
    }

    private void ApplySpecialNpcUnlocks(
        CarryableObject2D target,
        GameSessionManager session)
    {
        RescuePersonSpecialUnlock2D special =
            FindOnCarryable<RescuePersonSpecialUnlock2D>(target);

        if (special == null) return;

        List<string> flags = special.GetUnlockStoryFlags();

        foreach (string flag in flags)
        {
            if (string.IsNullOrWhiteSpace(flag)) continue;

            bool newlyUnlocked =
                session.SetStoryFlag(flag.Trim(), true);

            if (newlyUnlocked)
            {
                Log(
                    $"特殊NPC解放: {special.SpecialNpcId} / " +
                    $"Flag={flag.Trim()}"
                );
            }
        }
    }

    private static T FindOnCarryable<T>(CarryableObject2D target)
        where T : Component
    {
        if (target == null) return null;

        T component = target.GetComponent<T>();
        if (component != null) return component;

        component = target.GetComponentInChildren<T>(true);
        if (component != null) return component;

        return target.GetComponentInParent<T>();
    }

    private GameSessionManager GetSession()
    {
        if (GameSessionManager.Instance != null)
        {
            return GameSessionManager.Instance;
        }

        return FindAnyObjectByType<GameSessionManager>(
            FindObjectsInactive.Include
        );
    }

    private string GetStorageId()
    {
        string id = rescueCountStorageId?.Trim() ?? string.Empty;

        return string.IsNullOrWhiteSpace(id)
            ? "__rescue_total_count"
            : id;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[RescueProgressManager2D] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[RescueProgressManager2D] {message}", this);
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(rescueCountStorageId))
        {
            rescueCountStorageId = "__rescue_total_count";
        }

        rescueCountStorageId = rescueCountStorageId.Trim();
        firstRescueStoryFlag = firstRescueStoryFlag?.Trim() ?? string.Empty;

        if (milestones == null)
        {
            milestones = new List<RescueMilestone>();
        }

        foreach (RescueMilestone milestone in milestones)
        {
            if (milestone == null) continue;

            milestone.RequiredRescueCount =
                Mathf.Max(1, milestone.RequiredRescueCount);

            milestone.StoryFlagId =
                milestone.StoryFlagId?.Trim() ?? string.Empty;
        }
    }
}
