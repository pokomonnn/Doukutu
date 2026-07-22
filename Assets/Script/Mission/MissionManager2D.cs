using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// MissionManager2D のInspectorで並べる、シーン内ミッション設定です。
/// ScriptableObjectのMissionDefinition2Dに対し、コンパスの目的地や対象Enemyを紐づけます。
/// </summary>
[Serializable]
public class MissionEntry2D
{
    [Tooltip("ミッションの基本データ")]
    [SerializeField] private MissionDefinition2D mission;

    [Header("コンパスの行き先")]
    [Tooltip("回収地点・アイテム箱など、コンパスが指すTransformです。討伐ミッションで空欄ならTarget Enemyの位置を使います")]
    [SerializeField] private Transform compassTarget;

    [Header("指定敵の討伐ミッション")]
    [Tooltip("Defeat Target Enemyの時に設定する、MissionEnemyTarget2D付きのEnemyです")]
    [SerializeField] private MissionEnemyTarget2D targetEnemy;

    public MissionDefinition2D Mission => mission;
    public MissionEnemyTarget2D TargetEnemy => targetEnemy;

    public Transform GetCompassTarget()
    {
        if (compassTarget != null)
        {
            return compassTarget;
        }

        return targetEnemy != null
            ? targetEnemy.CompassAnchor
            : null;
    }
}

/// <summary>
/// ミッションメニューで使う現在の状態です。
/// InProgress のミッションだけ、コンパス追跡対象に選べます。
/// </summary>
public enum MissionProgressState2D
{
    Inactive,
    InProgress,
    Completed
}

/// <summary>
/// 複数の収集・指定敵討伐ミッションの進行を同時に管理します。
///
/// ・開始済みのミッションはすべて並行して進みます。
/// ・その中から1件だけを「追跡中」に選び、コンパスと既存MissionHUDへ表示します。
/// ・既存の ActiveMission 系プロパティは、追跡中ミッションを返す互換用APIです。
/// </summary>
[DisallowMultipleComponent]
public class MissionManager2D : MonoBehaviour
{
    [Header("プレイヤー参照")]
    [Tooltip("未設定ならシーン内から自動取得します")]
    [SerializeField] private InventoryController inventoryController;

    [Header("ミッション一覧")]
    [SerializeField]
    private List<MissionEntry2D> missions =
        new List<MissionEntry2D>();

    [Header("開始設定")]
    [Tooltip("オンならゲーム開始時に、一覧内の有効な未達成ミッションをすべて進行中にします")]
    [FormerlySerializedAs("startFirstMissionOnStart")]
    [SerializeField] private bool startAllMissionsOnStart = true;

    [Tooltip("進行中のミッションがまだ1件もない時、最初に開始したミッションを自動でコンパス追跡にします")]
    [SerializeField] private bool automaticallyTrackFirstStartedMission = true;

    [Tooltip("Start All Missions On Startがオフの時だけ有効です。達成したら、一覧の次の未開始ミッションを自動で開始します")]
    [FormerlySerializedAs("autoStartNextMission")]
    [SerializeField] private bool autoStartNextMissionWhenNotUsingStartAll;

    [Tooltip("達成済みミッションをStartMissionで再開できるようにします")]
    [SerializeField] private bool allowRestartCompletedMissions;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    // ---------------------------------------------------------------------
    // 既存スクリプトとの互換用API
    // Active = 現在コンパスで追跡しているミッション
    // ---------------------------------------------------------------------

    public MissionDefinition2D ActiveMission => TrackedMission;
    public bool HasActiveMission => HasTrackedMission;
    public int ActiveMissionIndex => trackedMissionIndex;
    public int ActiveProgress => TrackedProgress;
    public int ActiveRequiredAmount => TrackedRequiredAmount;
    public Transform ActiveCompassTarget => TrackedCompassTarget;

    // ---------------------------------------------------------------------
    // 新しい複数ミッション用API
    // ---------------------------------------------------------------------

    public int MissionCount => missions != null ? missions.Count : 0;

    public MissionDefinition2D TrackedMission =>
        HasTrackedMission
            ? GetMissionDefinition(trackedMissionIndex)
            : null;

    public bool HasTrackedMission =>
        IsValidMissionIndex(trackedMissionIndex) &&
        GetMissionState(trackedMissionIndex) ==
            MissionProgressState2D.InProgress;

    public int TrackedMissionIndex => trackedMissionIndex;

    public int TrackedProgress =>
        HasTrackedMission
            ? GetMissionProgress(trackedMissionIndex)
            : 0;

    public int TrackedRequiredAmount =>
        HasTrackedMission
            ? GetMissionRequiredAmount(trackedMissionIndex)
            : 0;

    public Transform TrackedCompassTarget
    {
        get
        {
            if (!HasTrackedMission)
            {
                return null;
            }

            Transform target = GetMissionCompassTarget(
                trackedMissionIndex
            );

            return target != null && target.gameObject.activeInHierarchy
                ? target
                : null;
        }
    }

    // 既存のHUD・コンパスが購読しているイベント
    public event Action<MissionDefinition2D> ActiveMissionChanged;
    public event Action<MissionDefinition2D, int, int>
        ActiveMissionProgressChanged;

    // 新しいメニュー用イベント
    public event Action<MissionDefinition2D> TrackedMissionChanged;
    public event Action<MissionDefinition2D, int, int>
        MissionProgressChanged;
    public event Action<MissionDefinition2D, MissionProgressState2D>
        MissionStatusChanged;
    public event Action<MissionDefinition2D> MissionCompleted;
    public event Action MissionStateChanged;

    private sealed class MissionRuntimeData
    {
        public int Progress;
        public int LastObservedItemAmount;
        public bool IsInitialized;
    }

    private readonly HashSet<int> inProgressMissionIndices =
        new HashSet<int>();

    private readonly HashSet<int> completedMissionIndices =
        new HashSet<int>();

    private readonly Dictionary<int, MissionRuntimeData>
        missionRuntimeData =
            new Dictionary<int, MissionRuntimeData>();

    private readonly HashSet<MissionEnemyTarget2D>
        subscribedEnemyTargets =
            new HashSet<MissionEnemyTarget2D>();

    private int trackedMissionIndex = -1;
    private bool inventorySubscribed;

    private void Awake()
    {
        FindInventoryController();
    }

    private void OnEnable()
    {
        FindInventoryController();
        SubscribeInventory();
        SubscribeEnemyTargets();
    }

    private void Start()
    {
        FindInventoryController();
        SubscribeInventory();
        SubscribeEnemyTargets();

        if (startAllMissionsOnStart &&
            inProgressMissionIndices.Count == 0)
        {
            StartAllAvailableMissions();
        }
    }

    private void OnDisable()
    {
        UnsubscribeInventory();
        UnsubscribeEnemyTargets();
    }

    /// <summary>
    /// 指定した1件を進行中にします。
    /// すでに別ミッションが進行中でも停止しません。
    /// 初めて開始したミッションは、自動追跡設定がオンならコンパス対象にもなります。
    /// </summary>
    public bool StartMission(int missionIndex)
    {
        FindInventoryController();
        SubscribeInventory();
        SubscribeEnemyTargets();

        if (!TryGetValidEntry(missionIndex, out MissionEntry2D entry))
        {
            LogWarning($"Mission Index が範囲外、またはMission Definition未設定です: {missionIndex}");
            return false;
        }

        if (!allowRestartCompletedMissions &&
            completedMissionIndices.Contains(missionIndex))
        {
            LogWarning(
                $"{entry.Mission.DisplayName} はすでに達成済みです。"
            );
            return false;
        }

        // 再開を許可している時は、達成済み状態を外して最初から進行を作り直す。
        if (allowRestartCompletedMissions)
        {
            completedMissionIndices.Remove(missionIndex);
        }

        bool wasAlreadyInProgress =
            inProgressMissionIndices.Contains(missionIndex);

        if (!wasAlreadyInProgress)
        {
            inProgressMissionIndices.Add(missionIndex);
            InitializeMissionProgress(missionIndex);

            MissionStatusChanged?.Invoke(
                entry.Mission,
                MissionProgressState2D.InProgress
            );

            Log($"ミッション開始: {entry.Mission.DisplayName}");
        }

        if (!HasTrackedMission &&
            automaticallyTrackFirstStartedMission)
        {
            SetTrackedMission(missionIndex);
        }

        NotifyMissionProgress(missionIndex);
        MissionStateChanged?.Invoke();

        if (IsMissionComplete(missionIndex))
        {
            CompleteMission(missionIndex);
        }

        return true;
    }

    /// <summary>
    /// セーブデータをロードする前に、現在のランタイム状態をすべて消去します。
    /// MissionDefinition2Dの登録一覧自体は変更しません。
    /// </summary>
    public void ClearAllMissionRuntimeStateForLoad()
    {
        inProgressMissionIndices.Clear();
        completedMissionIndices.Clear();
        missionRuntimeData.Clear();
        trackedMissionIndex = -1;

        ActiveMissionChanged?.Invoke(null);
        TrackedMissionChanged?.Invoke(null);
        MissionStateChanged?.Invoke();
    }

    /// <summary>
    /// セーブデータから1件のミッション状態と進捗を復元します。
    /// 通常のStartMissionと異なり、保存済み進捗を初期化で上書きしません。
    /// </summary>
    public bool RestoreMissionState(
        int missionIndex,
        MissionProgressState2D state,
        int savedProgress,
        bool trackAfterRestore)
    {
        FindInventoryController();
        SubscribeInventory();
        SubscribeEnemyTargets();

        if (!TryGetValidEntry(missionIndex, out MissionEntry2D entry))
        {
            LogWarning(
                $"ミッション復元失敗：Mission Indexが無効です: {missionIndex}"
            );
            return false;
        }

        int requiredAmount = Mathf.Max(
            1,
            GetMissionRequiredAmount(missionIndex)
        );

        MissionRuntimeData data = GetOrCreateRuntimeData(missionIndex);
        data.Progress = Mathf.Clamp(savedProgress, 0, requiredAmount);
        data.LastObservedItemAmount = GetCurrentRequiredItemAmount(entry.Mission);
        data.IsInitialized = true;

        inProgressMissionIndices.Remove(missionIndex);
        completedMissionIndices.Remove(missionIndex);

        switch (state)
        {
            case MissionProgressState2D.InProgress:
                inProgressMissionIndices.Add(missionIndex);
                MissionStatusChanged?.Invoke(
                    entry.Mission,
                    MissionProgressState2D.InProgress
                );
                break;

            case MissionProgressState2D.Completed:
                data.Progress = requiredAmount;
                completedMissionIndices.Add(missionIndex);
                MissionStatusChanged?.Invoke(
                    entry.Mission,
                    MissionProgressState2D.Completed
                );
                break;

            default:
                data.Progress = 0;
                break;
        }

        if (trackedMissionIndex == missionIndex &&
            state != MissionProgressState2D.InProgress)
        {
            trackedMissionIndex = -1;
        }

        if (state == MissionProgressState2D.InProgress)
        {
            NotifyMissionProgress(missionIndex);

            if (trackAfterRestore)
            {
                SetTrackedMission(missionIndex);
            }
        }

        MissionStateChanged?.Invoke();

        Log(
            $"ミッション状態を復元: {entry.Mission.DisplayName} / " +
            $"状態={state} / 進捗={data.Progress}/{requiredAmount} / " +
            $"追跡={trackAfterRestore}"
        );

        return true;
    }

    /// <summary>
    /// 一覧内の有効な未達成ミッションをすべて開始します。
    /// 進捗は並行して進みます。
    /// </summary>
    public int StartAllAvailableMissions()
    {
        int startedCount = 0;

        for (int i = 0; i < MissionCount; i++)
        {
            if (!TryGetValidEntry(i, out _) ||
                (!allowRestartCompletedMissions &&
                 completedMissionIndices.Contains(i)))
            {
                continue;
            }

            bool wasInProgress =
                inProgressMissionIndices.Contains(i);

            if (StartMission(i) && !wasInProgress)
            {
                startedCount++;
            }
        }

        // 何か開始されているのに追跡先がない場合の保険。
        if (!HasTrackedMission)
        {
            SelectFirstInProgressMission();
        }

        MissionStateChanged?.Invoke();
        return startedCount;
    }

    /// <summary>
    /// 以前の「最初のミッションを開始」と同じ用途の互換メソッドです。
    /// すでに進行中の別ミッションは停止しません。
    /// </summary>
    public bool StartFirstAvailableMission()
    {
        return StartFirstAvailableMission(0);
    }

    /// <summary>
    /// 指定インデックス以降で、最初の未開始ミッションを開始します。
    /// </summary>
    public bool StartNextMission()
    {
        int startIndex = trackedMissionIndex >= 0
            ? trackedMissionIndex + 1
            : 0;

        return StartFirstAvailableMission(startIndex);
    }

    /// <summary>
    /// コンパス・既存MissionHUDが表示する追跡ミッションを切り替えます。
    /// 達成済み・未開始ミッションは追跡できません。
    /// </summary>
    public bool SetTrackedMission(int missionIndex)
    {
        if (!TryGetValidEntry(missionIndex, out MissionEntry2D entry))
        {
            return false;
        }

        if (GetMissionState(missionIndex) !=
            MissionProgressState2D.InProgress)
        {
            LogWarning(
                $"{entry.Mission.DisplayName} は進行中ではないため追跡できません。"
            );
            return false;
        }

        if (trackedMissionIndex == missionIndex)
        {
            return true;
        }

        trackedMissionIndex = missionIndex;

        ActiveMissionChanged?.Invoke(entry.Mission);
        TrackedMissionChanged?.Invoke(entry.Mission);
        NotifyTrackedMissionProgress();
        MissionStateChanged?.Invoke();

        Log($"追跡ミッション変更: {entry.Mission.DisplayName}");
        return true;
    }

    // UIボタンなどから読みやすい別名
    public bool SelectTrackedMission(int missionIndex)
    {
        return SetTrackedMission(missionIndex);
    }

    /// <summary>
    /// 現在追跡しているミッションを解除します。
    /// ミッションの進行自体は止まりません。
    /// </summary>
    public void ClearTrackedMission()
    {
        if (trackedMissionIndex < 0)
        {
            return;
        }

        trackedMissionIndex = -1;
        ActiveMissionChanged?.Invoke(null);
        TrackedMissionChanged?.Invoke(null);
        MissionStateChanged?.Invoke();
    }

    /// <summary>
    /// すべての進行中ミッションの収集進捗を再計算します。
    /// 外部処理で直接Inventoryを変更した直後などに使えます。
    /// </summary>
    public void RefreshAllMissionProgress()
    {
        List<int> completionCandidates = new List<int>();

        foreach (int missionIndex in inProgressMissionIndices)
        {
            MissionDefinition2D mission =
                GetMissionDefinition(missionIndex);

            if (mission != null &&
                mission.ObjectiveType ==
                MissionObjectiveType2D.CollectItem)
            {
                RefreshCollectItemProgress(missionIndex);
            }

            if (IsMissionComplete(missionIndex))
            {
                completionCandidates.Add(missionIndex);
            }
        }

        foreach (int missionIndex in completionCandidates)
        {
            CompleteMission(missionIndex);
        }
    }

    // 既存ボタン・イベントとの互換用
    public void RefreshActiveMissionProgress()
    {
        RefreshAllMissionProgress();
    }

    public MissionDefinition2D GetMissionDefinition(int missionIndex)
    {
        return TryGetValidEntry(missionIndex, out MissionEntry2D entry)
            ? entry.Mission
            : null;
    }

    public MissionEntry2D GetMissionEntry(int missionIndex)
    {
        return TryGetValidEntry(missionIndex, out MissionEntry2D entry)
            ? entry
            : null;
    }

    public MissionProgressState2D GetMissionState(int missionIndex)
    {
        if (!IsValidMissionIndex(missionIndex) ||
            GetMissionDefinition(missionIndex) == null)
        {
            return MissionProgressState2D.Inactive;
        }

        if (completedMissionIndices.Contains(missionIndex))
        {
            return MissionProgressState2D.Completed;
        }

        return inProgressMissionIndices.Contains(missionIndex)
            ? MissionProgressState2D.InProgress
            : MissionProgressState2D.Inactive;
    }

    public bool IsMissionInProgress(int missionIndex)
    {
        return GetMissionState(missionIndex) ==
            MissionProgressState2D.InProgress;
    }

    public bool IsMissionCompleted(int missionIndex)
    {
        return GetMissionState(missionIndex) ==
            MissionProgressState2D.Completed;
    }

    public bool IsTrackedMission(int missionIndex)
    {
        return HasTrackedMission &&
            trackedMissionIndex == missionIndex;
    }

    public int GetMissionProgress(int missionIndex)
    {
        if (!missionRuntimeData.TryGetValue(
                missionIndex,
                out MissionRuntimeData data) ||
            data == null)
        {
            return 0;
        }

        return Mathf.Max(0, data.Progress);
    }

    public int GetMissionRequiredAmount(int missionIndex)
    {
        MissionDefinition2D mission =
            GetMissionDefinition(missionIndex);

        if (mission == null)
        {
            return 0;
        }

        return mission.ObjectiveType ==
            MissionObjectiveType2D.CollectItem
            ? mission.RequiredAmount
            : 1;
    }

    public Transform GetMissionCompassTarget(int missionIndex)
    {
        return TryGetValidEntry(missionIndex, out MissionEntry2D entry)
            ? entry.GetCompassTarget()
            : null;
    }

    [ContextMenu("Start All Available Missions")]
    private void StartAllAvailableMissionsFromContextMenu()
    {
        StartAllAvailableMissions();
    }

    [ContextMenu("Start First Available Mission")]
    private void StartFirstAvailableMissionFromContextMenu()
    {
        StartFirstAvailableMission();
    }

    [ContextMenu("Refresh All Mission Progress")]
    private void RefreshAllMissionProgressFromContextMenu()
    {
        RefreshAllMissionProgress();
    }

    [ContextMenu("Complete Tracked Mission")]
    private void CompleteTrackedMissionFromContextMenu()
    {
        if (HasTrackedMission)
        {
            CompleteMission(trackedMissionIndex);
        }
    }

    private bool StartFirstAvailableMission(int startIndex)
    {
        for (int i = Mathf.Max(0, startIndex);
             i < MissionCount;
             i++)
        {
            if (!TryGetValidEntry(i, out _) ||
                inProgressMissionIndices.Contains(i) ||
                (!allowRestartCompletedMissions &&
                 completedMissionIndices.Contains(i)))
            {
                continue;
            }

            return StartMission(i);
        }

        return false;
    }

    private void InitializeMissionProgress(int missionIndex)
    {
        MissionDefinition2D mission =
            GetMissionDefinition(missionIndex);

        if (mission == null)
        {
            return;
        }

        MissionRuntimeData data = GetOrCreateRuntimeData(
            missionIndex
        );

        data.Progress = 0;
        data.LastObservedItemAmount = 0;
        data.IsInitialized = true;

        switch (mission.ObjectiveType)
        {
            case MissionObjectiveType2D.CollectItem:
                InitializeCollectItemProgress(missionIndex, mission);
                break;

            case MissionObjectiveType2D.DefeatTargetEnemy:
                MissionEntry2D entry = GetMissionEntry(missionIndex);

                data.Progress = entry != null &&
                    entry.TargetEnemy != null &&
                    entry.TargetEnemy.IsDefeated
                    ? 1
                    : 0;
                break;
        }
    }

    private void InitializeCollectItemProgress(
        int missionIndex,
        MissionDefinition2D mission)
    {
        MissionRuntimeData data = GetOrCreateRuntimeData(
            missionIndex
        );

        if (mission.RequiredItem == null ||
            inventoryController == null)
        {
            data.Progress = 0;
            data.LastObservedItemAmount = 0;
            return;
        }

        int currentAmount = Mathf.Max(
            0,
            inventoryController.GetTotalAmount(
                mission.RequiredItem
            )
        );

        data.LastObservedItemAmount = currentAmount;
        data.Progress = mission.CountItemsAlreadyHeldWhenMissionStarts
            ? Mathf.Min(mission.RequiredAmount, currentAmount)
            : 0;
    }

    private int GetCurrentRequiredItemAmount(
        MissionDefinition2D mission)
    {
        if (mission == null ||
            mission.ObjectiveType != MissionObjectiveType2D.CollectItem ||
            mission.RequiredItem == null ||
            inventoryController == null)
        {
            return 0;
        }

        return Mathf.Max(
            0,
            inventoryController.GetTotalAmount(mission.RequiredItem)
        );
    }

    private void HandleInventoryChanged()
    {
        RefreshAllMissionProgress();
    }

    private void RefreshCollectItemProgress(int missionIndex)
    {
        MissionDefinition2D mission =
            GetMissionDefinition(missionIndex);

        if (mission == null ||
            mission.RequiredItem == null ||
            inventoryController == null)
        {
            return;
        }

        MissionRuntimeData data = GetOrCreateRuntimeData(
            missionIndex
        );

        int currentAmount = Mathf.Max(
            0,
            inventoryController.GetTotalAmount(
                mission.RequiredItem
            )
        );

        int previousProgress = data.Progress;

        if (mission.CountItemsAlreadyHeldWhenMissionStarts)
        {
            data.Progress = Mathf.Min(
                mission.RequiredAmount,
                currentAmount
            );
        }
        else
        {
            int newlyAddedAmount = Mathf.Max(
                0,
                currentAmount - data.LastObservedItemAmount
            );

            if (newlyAddedAmount > 0)
            {
                data.Progress = Mathf.Min(
                    mission.RequiredAmount,
                    data.Progress + newlyAddedAmount
                );
            }
        }

        data.LastObservedItemAmount = currentAmount;

        if (previousProgress != data.Progress)
        {
            NotifyMissionProgress(missionIndex);
        }
    }

    private void HandleTargetEnemyDefeated(
        MissionEnemyTarget2D defeatedEnemy)
    {
        if (defeatedEnemy == null)
        {
            return;
        }

        List<int> completionCandidates = new List<int>();

        foreach (int missionIndex in inProgressMissionIndices)
        {
            MissionDefinition2D mission =
                GetMissionDefinition(missionIndex);

            MissionEntry2D entry = GetMissionEntry(missionIndex);

            if (mission == null ||
                entry == null ||
                mission.ObjectiveType !=
                MissionObjectiveType2D.DefeatTargetEnemy ||
                entry.TargetEnemy != defeatedEnemy)
            {
                continue;
            }

            MissionRuntimeData data = GetOrCreateRuntimeData(
                missionIndex
            );

            if (data.Progress < 1)
            {
                data.Progress = 1;
                NotifyMissionProgress(missionIndex);
            }

            completionCandidates.Add(missionIndex);
        }

        foreach (int missionIndex in completionCandidates)
        {
            CompleteMission(missionIndex);
        }
    }

    private bool IsMissionComplete(int missionIndex)
    {
        return GetMissionState(missionIndex) ==
            MissionProgressState2D.InProgress &&
            GetMissionProgress(missionIndex) >=
            GetMissionRequiredAmount(missionIndex);
    }

    private void CompleteMission(int missionIndex)
    {
        if (!TryGetValidEntry(missionIndex, out MissionEntry2D entry) ||
            !inProgressMissionIndices.Remove(missionIndex))
        {
            return;
        }

        completedMissionIndices.Add(missionIndex);

        Log($"ミッション達成: {entry.Mission.DisplayName}");

        MissionCompleted?.Invoke(entry.Mission);
        MissionStatusChanged?.Invoke(
            entry.Mission,
            MissionProgressState2D.Completed
        );

        bool wasTracked = trackedMissionIndex == missionIndex;

        if (wasTracked)
        {
            trackedMissionIndex = -1;
            SelectFirstInProgressMission();
        }

        if (!startAllMissionsOnStart &&
            autoStartNextMissionWhenNotUsingStartAll)
        {
            StartFirstAvailableMission(missionIndex + 1);
        }

        MissionStateChanged?.Invoke();
    }

    private void SelectFirstInProgressMission()
    {
        for (int i = 0; i < MissionCount; i++)
        {
            if (GetMissionState(i) ==
                MissionProgressState2D.InProgress)
            {
                SetTrackedMission(i);
                return;
            }
        }

        ActiveMissionChanged?.Invoke(null);
        TrackedMissionChanged?.Invoke(null);
    }

    private void NotifyMissionProgress(int missionIndex)
    {
        MissionDefinition2D mission =
            GetMissionDefinition(missionIndex);

        if (mission == null ||
            GetMissionState(missionIndex) !=
            MissionProgressState2D.InProgress)
        {
            return;
        }

        int progress = GetMissionProgress(missionIndex);
        int required = GetMissionRequiredAmount(missionIndex);

        MissionProgressChanged?.Invoke(
            mission,
            progress,
            required
        );

        if (missionIndex == trackedMissionIndex)
        {
            ActiveMissionProgressChanged?.Invoke(
                mission,
                progress,
                required
            );
        }

        MissionStateChanged?.Invoke();
    }

    private void NotifyTrackedMissionProgress()
    {
        if (!HasTrackedMission)
        {
            return;
        }

        ActiveMissionProgressChanged?.Invoke(
            TrackedMission,
            TrackedProgress,
            TrackedRequiredAmount
        );
    }

    private MissionRuntimeData GetOrCreateRuntimeData(
        int missionIndex)
    {
        if (!missionRuntimeData.TryGetValue(
                missionIndex,
                out MissionRuntimeData data) ||
            data == null)
        {
            data = new MissionRuntimeData();
            missionRuntimeData[missionIndex] = data;
        }

        return data;
    }

    private bool TryGetValidEntry(
        int missionIndex,
        out MissionEntry2D entry)
    {
        entry = null;

        if (!IsValidMissionIndex(missionIndex))
        {
            return false;
        }

        entry = missions[missionIndex];

        return entry != null && entry.Mission != null;
    }

    private bool IsValidMissionIndex(int missionIndex)
    {
        return missions != null &&
            missionIndex >= 0 &&
            missionIndex < missions.Count;
    }

    private void SubscribeInventory()
    {
        if (inventorySubscribed || inventoryController == null)
        {
            return;
        }

        inventoryController.OnInventoryChanged +=
            HandleInventoryChanged;

        inventorySubscribed = true;
    }

    private void UnsubscribeInventory()
    {
        if (!inventorySubscribed || inventoryController == null)
        {
            return;
        }

        inventoryController.OnInventoryChanged -=
            HandleInventoryChanged;

        inventorySubscribed = false;
    }

    private void SubscribeEnemyTargets()
    {
        foreach (MissionEntry2D entry in missions)
        {
            if (entry == null ||
                entry.TargetEnemy == null ||
                !subscribedEnemyTargets.Add(entry.TargetEnemy))
            {
                continue;
            }

            entry.TargetEnemy.Defeated +=
                HandleTargetEnemyDefeated;
        }
    }

    private void UnsubscribeEnemyTargets()
    {
        foreach (MissionEnemyTarget2D targetEnemy
                 in subscribedEnemyTargets)
        {
            if (targetEnemy != null)
            {
                targetEnemy.Defeated -=
                    HandleTargetEnemyDefeated;
            }
        }

        subscribedEnemyTargets.Clear();
    }

    private void FindInventoryController()
    {
        if (inventoryController != null)
        {
            return;
        }

        inventoryController =
            FindAnyObjectByType<InventoryController>();
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[MissionManager2D] {message}", this);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MissionManager2D] {message}", this);
    }
}
