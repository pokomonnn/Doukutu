using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// メイン・サブストーリーの解放、受注、完了、排他クエスト、Story Flagを管理します。
/// GameSessionManagerと同じObjectへ付けることを推奨します。
/// </summary>
[DisallowMultipleComponent]
public class StoryProgressManager : MonoBehaviour
{
    [Header("ストーリー設定")]
    [Tooltip("ゲームで使用する全StoryQuestDefinitionを登録したデータベースです。")]
    [InspectorName("ストーリークエストデータベース")]
    [SerializeField] private StoryQuestDatabase storyDatabase;

    [Header("参照")]
    [Tooltip("未設定ならGameSessionManager.Instance、またはシーン内から自動取得します。")]
    [InspectorName("ゲームセッション管理")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("動作")]
    [Tooltip("ONならこのManagerをシーン移動で破棄しません。GameSessionManagerと同じObjectに付ける場合はON推奨です。")]
    [InspectorName("シーン移動で保持する")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [Tooltip("Start時に未登録ストーリーを初期化し、前提条件を満たしたクエストを受注可能にします。")]
    [InspectorName("開始時に自動初期化する")]
    [SerializeField] private bool initializeOnStart = true;

    [Header("デバッグ")]
    [InspectorName("デバッグログを表示")]
    [SerializeField] private bool showDebugLogs = true;

    public static StoryProgressManager Instance { get; private set; }

    public StoryQuestDatabase StoryDatabase => storyDatabase;

    public event Action<StoryQuestDefinition, StoryQuestState, StoryQuestState>
        StoryStateChanged;
    public event Action<StoryQuestDefinition> StoryAvailable;
    public event Action<StoryQuestDefinition> StoryStarted;
    public event Action<StoryQuestDefinition> StoryCompleted;
    public event Action<StoryQuestDefinition> StoryLockedOut;

    private bool isEvaluating;
    private bool isHandlingSessionChange;
    private bool sessionSubscribed;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (dontDestroyOnLoad)
        {
            DontDestroyOnLoad(gameObject);
        }

        FindReferences();
        SubscribeSession();
    }

    private void Start()
    {
        if (initializeOnStart)
        {
            InitializeAndEvaluate();
        }
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeSession();
    }

    private void OnDisable()
    {
        UnsubscribeSession();
    }

    private void OnDestroy()
    {
        UnsubscribeSession();

        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// 未登録クエストをLockedで作成し、現在の前提条件から受注可能状態を再計算します。
    /// </summary>
    public void InitializeAndEvaluate()
    {
        FindReferences();

        if (gameSessionManager == null || storyDatabase == null)
        {
            LogWarning(
                "初期化できません。GameSessionManagerまたはStory Quest Databaseが未設定です。"
            );
            return;
        }

        IReadOnlyList<StoryQuestDefinition> stories = storyDatabase.Stories;

        if (stories != null)
        {
            foreach (StoryQuestDefinition story in stories)
            {
                if (story == null || string.IsNullOrWhiteSpace(story.StoryId))
                {
                    continue;
                }

                gameSessionManager.EnsureStorySessionData(story);
            }
        }

        ReconcileWithLinkedMissionSessions();
        EvaluateAvailability();
    }

    /// <summary>
    /// 前提ストーリー・Story Flag・排他状態を見てLockedからAvailableへ更新します。
    /// </summary>
    public void EvaluateAvailability()
    {
        if (isEvaluating)
        {
            return;
        }

        FindReferences();

        if (gameSessionManager == null || storyDatabase == null)
        {
            return;
        }

        isEvaluating = true;

        try
        {
            IReadOnlyList<StoryQuestDefinition> stories = storyDatabase.Stories;

            if (stories == null)
            {
                return;
            }

            // 先にPermanent排他グループの既選択ルートを確定させます。
            ApplyPermanentExclusiveHistory(stories);

            foreach (StoryQuestDefinition story in stories)
            {
                if (story == null || string.IsNullOrWhiteSpace(story.StoryId))
                {
                    continue;
                }

                StoryQuestState state = GetState(story);

                if (state == StoryQuestState.InProgress ||
                    state == StoryQuestState.Completed ||
                    state == StoryQuestState.LockedOut ||
                    (state == StoryQuestState.Available &&
                     !story.AutomaticallyBecomeAvailable))
                {
                    continue;
                }

                bool canBecomeAvailable =
                    story.AutomaticallyBecomeAvailable &&
                    ArePrerequisitesCompleted(story) &&
                    AreRequiredFlagsEnabled(story) &&
                    !HasPermanentExclusiveSelection(story);

                StoryQuestState desired = canBecomeAvailable
                    ? StoryQuestState.Available
                    : StoryQuestState.Locked;

                SetStateInternal(story, desired);
            }
        }
        finally
        {
            isEvaluating = false;
        }
    }

    public StoryQuestState GetState(StoryQuestDefinition story)
    {
        if (story == null)
        {
            return StoryQuestState.Locked;
        }

        FindReferences();

        if (gameSessionManager == null)
        {
            return StoryQuestState.Locked;
        }

        return gameSessionManager.GetStoryState(story.StoryId);
    }

    public StoryQuestState GetState(string storyId)
    {
        FindReferences();
        return gameSessionManager != null
            ? gameSessionManager.GetStoryState(storyId)
            : StoryQuestState.Locked;
    }

    public bool CanStartQuest(
        StoryQuestDefinition story,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (story == null)
        {
            resultMessage = "ストーリークエストが未設定です。";
            return false;
        }

        InitializeStoryIfNeeded(story);
        EvaluateAvailability();

        StoryQuestState state = GetState(story);

        switch (state)
        {
            case StoryQuestState.InProgress:
                resultMessage = $"『{story.DisplayName}』はすでに進行中です。";
                return false;

            case StoryQuestState.Completed:
                resultMessage = $"『{story.DisplayName}』はすでに完了しています。";
                return false;

            case StoryQuestState.LockedOut:
                resultMessage = $"『{story.DisplayName}』は別ルートを選択したため受注できません。";
                return false;

            case StoryQuestState.Locked:
                resultMessage = $"『{story.DisplayName}』はまだ解放されていません。";
                return false;
        }

        StoryQuestDefinition conflict = FindActiveExclusiveConflict(story);

        if (conflict != null)
        {
            resultMessage =
                $"『{conflict.DisplayName}』を進行中のため、" +
                $"『{story.DisplayName}』は受注できません。";
            return false;
        }

        resultMessage = $"『{story.DisplayName}』を受注できます。";
        return true;
    }

    public bool TryStartQuest(
        StoryQuestDefinition story,
        out string resultMessage)
    {
        if (!CanStartQuest(story, out resultMessage))
        {
            return false;
        }

        StoryQuestState oldState = GetState(story);
        SetStateInternal(story, StoryQuestState.InProgress);

        if (GetState(story) != StoryQuestState.InProgress)
        {
            resultMessage = $"『{story.DisplayName}』の開始状態を保存できませんでした。";
            return false;
        }

        if (story.ExclusiveMode == StoryExclusiveMode.PermanentOnAccept &&
            !string.IsNullOrWhiteSpace(story.ExclusiveGroupId))
        {
            LockOutOtherQuestsInGroup(story);
        }

        StoryStarted?.Invoke(story);
        resultMessage = $"『{story.DisplayName}』を開始しました。";
        Log(resultMessage + $" / 旧状態={oldState}");
        return true;
    }

    public bool TryCompleteQuest(
        StoryQuestDefinition story,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (story == null)
        {
            resultMessage = "ストーリークエストが未設定です。";
            return false;
        }

        InitializeStoryIfNeeded(story);

        StoryQuestState state = GetState(story);

        if (state == StoryQuestState.Completed)
        {
            resultMessage = $"『{story.DisplayName}』はすでに完了しています。";
            return true;
        }

        if (state != StoryQuestState.InProgress)
        {
            resultMessage = $"『{story.DisplayName}』は進行中ではないため完了できません。";
            return false;
        }

        SetStateInternal(story, StoryQuestState.Completed);
        ApplyCompletionFlags(story);

        StoryCompleted?.Invoke(story);
        resultMessage = $"『{story.DisplayName}』を完了しました。";
        Log(resultMessage);

        EvaluateAvailability();
        return true;
    }

    /// <summary>
    /// Linked Missionが達成された時にMissionManager2Dから呼ばれます。
    /// </summary>
    public void ReportLinkedMissionCompleted(MissionDefinition2D mission)
    {
        if (mission == null || storyDatabase == null)
        {
            return;
        }

        IReadOnlyList<StoryQuestDefinition> stories = storyDatabase.Stories;

        if (stories == null)
        {
            return;
        }

        foreach (StoryQuestDefinition story in stories)
        {
            if (story == null ||
                story.LinkedMission == null ||
                !story.CompleteWhenLinkedMissionCompleted ||
                !IsSameMission(story.LinkedMission, mission))
            {
                continue;
            }

            if (GetState(story) == StoryQuestState.InProgress)
            {
                TryCompleteQuest(story, out _);
            }
        }
    }

    /// <summary>
    /// NPC会話でMissionを受注する前に、そのMissionへ紐づくStoryの受注可否を確認します。
    /// Storyに紐づいていないMissionは常にtrueです。
    /// </summary>
    public bool CanAcceptLinkedMission(
        MissionDefinition2D mission,
        out StoryQuestDefinition linkedStory,
        out string resultMessage)
    {
        linkedStory = FindStoryByLinkedMission(mission);

        if (linkedStory == null)
        {
            resultMessage = string.Empty;
            return true;
        }

        return CanStartQuest(linkedStory, out resultMessage);
    }

    /// <summary>
    /// NPC会話でMission受注に成功した直後、紐づくStoryを進行中へします。
    /// Storyに紐づいていないMissionはtrueを返します。
    /// </summary>
    public bool StartLinkedStoryForMission(
        MissionDefinition2D mission,
        out string resultMessage)
    {
        StoryQuestDefinition story = FindStoryByLinkedMission(mission);

        if (story == null)
        {
            resultMessage = string.Empty;
            return true;
        }

        if (GetState(story) == StoryQuestState.InProgress)
        {
            resultMessage = $"『{story.DisplayName}』は進行中です。";
            return true;
        }

        return TryStartQuest(story, out resultMessage);
    }

    public StoryQuestDefinition FindStoryByLinkedMission(
        MissionDefinition2D mission)
    {
        return storyDatabase != null
            ? storyDatabase.FindByLinkedMission(mission)
            : null;
    }

    public bool UnlockQuest(
        StoryQuestDefinition story,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (story == null)
        {
            resultMessage = "ストーリークエストが未設定です。";
            return false;
        }

        InitializeStoryIfNeeded(story);
        StoryQuestState state = GetState(story);

        if (state == StoryQuestState.LockedOut ||
            state == StoryQuestState.Completed ||
            state == StoryQuestState.InProgress)
        {
            resultMessage = $"『{story.DisplayName}』は現在の状態では解放できません。";
            return false;
        }

        SetStateInternal(story, StoryQuestState.Available);
        resultMessage = $"『{story.DisplayName}』を受注可能にしました。";
        return true;
    }

    public bool SetStoryFlag(string flagId, bool enabled = true)
    {
        FindReferences();

        if (gameSessionManager == null)
        {
            return false;
        }

        bool changed = gameSessionManager.SetStoryFlag(flagId, enabled);

        if (changed)
        {
            EvaluateAvailability();
        }

        return changed;
    }

    public bool HasStoryFlag(string flagId)
    {
        FindReferences();
        return gameSessionManager != null &&
            gameSessionManager.HasStoryFlag(flagId);
    }

    [ContextMenu("ストーリー状態を再評価")]
    private void EvaluateFromContextMenu()
    {
        InitializeAndEvaluate();
    }

    [ContextMenu("ストーリー状態をConsoleへ表示")]
    private void LogStorySummaryFromContextMenu()
    {
        FindReferences();

        if (gameSessionManager == null || storyDatabase == null)
        {
            LogWarning("GameSessionManagerまたはDatabaseがありません。");
            return;
        }

        foreach (StoryQuestDefinition story in storyDatabase.Stories)
        {
            if (story != null)
            {
                Debug.Log(
                    $"[Story] {story.DisplayName} / ID={story.StoryId} / 状態={GetState(story)}",
                    story
                );
            }
        }
    }

    /// <summary>
    /// 旧セーブや導入前から進行中の会話Missionがある場合、
    /// その状態をLinked Storyへ引き継ぎます。
    /// </summary>
    private void ReconcileWithLinkedMissionSessions()
    {
        if (gameSessionManager == null || storyDatabase == null)
        {
            return;
        }

        foreach (StoryQuestDefinition story in storyDatabase.Stories)
        {
            if (story == null || story.LinkedMission == null ||
                string.IsNullOrWhiteSpace(story.LinkedMission.MissionId))
            {
                continue;
            }

            StoryQuestState storyState = GetState(story);

            if (storyState == StoryQuestState.Completed ||
                storyState == StoryQuestState.LockedOut ||
                storyState == StoryQuestState.InProgress)
            {
                continue;
            }

            if (!gameSessionManager.TryGetMissionSession(
                    story.LinkedMission.MissionId,
                    out MissionSessionData missionData) ||
                missionData == null)
            {
                continue;
            }

            // Storyと自動同期するのはNPC会話由来のMissionを基本とします。
            // 旧セーブのUnknownは互換のため会話Missionとして扱います。
            if (missionData.AcceptSource != MissionAcceptSource.Conversation &&
                missionData.AcceptSource != MissionAcceptSource.Unknown)
            {
                continue;
            }

            if (missionData.State == MissionSessionState.Completed &&
                story.CompleteWhenLinkedMissionCompleted)
            {
                SetStateInternal(story, StoryQuestState.Completed);
                ApplyCompletionFlags(story);
                continue;
            }

            if (missionData.State == MissionSessionState.InProgress)
            {
                SetStateInternal(story, StoryQuestState.InProgress);
            }
        }
    }

    private void InitializeStoryIfNeeded(StoryQuestDefinition story)
    {
        FindReferences();
        gameSessionManager?.EnsureStorySessionData(story);
    }

    private bool ArePrerequisitesCompleted(StoryQuestDefinition story)
    {
        IReadOnlyList<StoryQuestDefinition> prerequisites =
            story.PrerequisiteStories;

        if (prerequisites == null || prerequisites.Count == 0)
        {
            return true;
        }

        foreach (StoryQuestDefinition prerequisite in prerequisites)
        {
            if (prerequisite == null)
            {
                continue;
            }

            if (GetState(prerequisite) != StoryQuestState.Completed)
            {
                return false;
            }
        }

        return true;
    }

    private bool AreRequiredFlagsEnabled(StoryQuestDefinition story)
    {
        IReadOnlyList<string> flags = story.RequiredStoryFlags;

        if (flags == null || flags.Count == 0)
        {
            return true;
        }

        foreach (string flag in flags)
        {
            if (!string.IsNullOrWhiteSpace(flag) &&
                !HasStoryFlag(flag))
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyCompletionFlags(StoryQuestDefinition story)
    {
        IReadOnlyList<string> flags = story.StoryFlagsSetOnComplete;

        if (flags == null)
        {
            return;
        }

        foreach (string flag in flags)
        {
            if (!string.IsNullOrWhiteSpace(flag))
            {
                gameSessionManager?.SetStoryFlag(flag, true);
            }
        }
    }

    private StoryQuestDefinition FindActiveExclusiveConflict(
        StoryQuestDefinition target)
    {
        if (target == null ||
            target.ExclusiveMode == StoryExclusiveMode.None ||
            string.IsNullOrWhiteSpace(target.ExclusiveGroupId) ||
            storyDatabase == null)
        {
            return null;
        }

        foreach (StoryQuestDefinition other in storyDatabase.Stories)
        {
            if (other == null || other == target ||
                !string.Equals(
                    other.ExclusiveGroupId,
                    target.ExclusiveGroupId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            StoryQuestState otherState = GetState(other);

            if (otherState == StoryQuestState.InProgress)
            {
                return other;
            }

            if ((target.ExclusiveMode == StoryExclusiveMode.PermanentOnAccept ||
                 other.ExclusiveMode == StoryExclusiveMode.PermanentOnAccept) &&
                otherState == StoryQuestState.Completed)
            {
                return other;
            }
        }

        return null;
    }

    private bool HasPermanentExclusiveSelection(
        StoryQuestDefinition target)
    {
        return FindPermanentExclusiveSelection(target) != null;
    }

    private StoryQuestDefinition FindPermanentExclusiveSelection(
        StoryQuestDefinition target)
    {
        if (target == null ||
            string.IsNullOrWhiteSpace(target.ExclusiveGroupId) ||
            storyDatabase == null)
        {
            return null;
        }

        foreach (StoryQuestDefinition other in storyDatabase.Stories)
        {
            if (other == null || other == target ||
                !string.Equals(
                    other.ExclusiveGroupId,
                    target.ExclusiveGroupId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            StoryQuestState state = GetState(other);

            if (other.ExclusiveMode == StoryExclusiveMode.PermanentOnAccept &&
                (state == StoryQuestState.InProgress ||
                 state == StoryQuestState.Completed))
            {
                return other;
            }
        }

        return null;
    }

    private void ApplyPermanentExclusiveHistory(
        IReadOnlyList<StoryQuestDefinition> stories)
    {
        if (stories == null)
        {
            return;
        }

        foreach (StoryQuestDefinition selected in stories)
        {
            if (selected == null ||
                selected.ExclusiveMode != StoryExclusiveMode.PermanentOnAccept ||
                string.IsNullOrWhiteSpace(selected.ExclusiveGroupId))
            {
                continue;
            }

            StoryQuestState selectedState = GetState(selected);

            if (selectedState != StoryQuestState.InProgress &&
                selectedState != StoryQuestState.Completed)
            {
                continue;
            }

            LockOutOtherQuestsInGroup(selected);
        }
    }

    private void LockOutOtherQuestsInGroup(StoryQuestDefinition selected)
    {
        if (selected == null ||
            storyDatabase == null ||
            string.IsNullOrWhiteSpace(selected.ExclusiveGroupId))
        {
            return;
        }

        foreach (StoryQuestDefinition other in storyDatabase.Stories)
        {
            if (other == null || other == selected ||
                !string.Equals(
                    other.ExclusiveGroupId,
                    selected.ExclusiveGroupId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            StoryQuestState state = GetState(other);

            if (state == StoryQuestState.Completed ||
                state == StoryQuestState.LockedOut)
            {
                continue;
            }

            SetStateInternal(other, StoryQuestState.LockedOut);
        }
    }

    private void SetStateInternal(
        StoryQuestDefinition story,
        StoryQuestState newState)
    {
        if (story == null)
        {
            return;
        }

        FindReferences();

        if (gameSessionManager == null)
        {
            return;
        }

        StoryQuestState oldState = GetState(story);

        if (oldState == newState)
        {
            return;
        }

        if (!gameSessionManager.SetStoryState(story, newState))
        {
            return;
        }

        StoryStateChanged?.Invoke(story, oldState, newState);

        switch (newState)
        {
            case StoryQuestState.Available:
                StoryAvailable?.Invoke(story);
                break;

            case StoryQuestState.LockedOut:
                StoryLockedOut?.Invoke(story);
                break;
        }

        Log(
            $"状態変更: {story.DisplayName} / {oldState} → {newState}"
        );
    }

    private void HandleStorySessionChanged()
    {
        if (isEvaluating || isHandlingSessionChange)
        {
            return;
        }

        isHandlingSessionChange = true;

        try
        {
            ReconcileWithLinkedMissionSessions();
            EvaluateAvailability();
        }
        finally
        {
            isHandlingSessionChange = false;
        }
    }

    private void FindReferences()
    {
        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void SubscribeSession()
    {
        FindReferences();

        if (sessionSubscribed || gameSessionManager == null)
        {
            return;
        }

        gameSessionManager.StorySessionChanged +=
            HandleStorySessionChanged;
        sessionSubscribed = true;
    }

    private void UnsubscribeSession()
    {
        if (!sessionSubscribed || gameSessionManager == null)
        {
            sessionSubscribed = false;
            return;
        }

        gameSessionManager.StorySessionChanged -=
            HandleStorySessionChanged;
        sessionSubscribed = false;
    }

    private static bool IsSameMission(
        MissionDefinition2D a,
        MissionDefinition2D b)
    {
        if (a == b)
        {
            return true;
        }

        if (a == null || b == null)
        {
            return false;
        }

        string aId = a.MissionId?.Trim() ?? string.Empty;
        string bId = b.MissionId?.Trim() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(aId) &&
            string.Equals(aId, bId, StringComparison.OrdinalIgnoreCase);
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[StoryProgressManager] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[StoryProgressManager] {message}", this);
    }
}
