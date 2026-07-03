using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// MissionManager2DのInspectorで並べる、シーン内ミッション設定です。
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
/// 収集・指定敵討伐ミッションの進行を管理します。
/// ItemDataの収集はPlayerのInventoryController内の所持数変化を監視します。
/// 指定敵の討伐はMissionEnemyTarget2Dから通知を受けます。
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

    [Header("開始・進行")]
    [Tooltip("ゲーム開始時に先頭の未完了ミッションを開始します")]
    [SerializeField] private bool startFirstMissionOnStart = true;

    [Tooltip("ミッション達成直後に、次の未完了ミッションを自動で開始します")]
    [SerializeField] private bool autoStartNextMission = true;

    [Tooltip("達成済みミッションをStartMissionで再開できるようにします")]
    [SerializeField] private bool allowRestartCompletedMissions;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public MissionDefinition2D ActiveMission => HasActiveMission
        ? ActiveEntry.Mission
        : null;

    public bool HasActiveMission =>
        activeMissionIndex >= 0 &&
        activeMissionIndex < missions.Count &&
        ActiveEntry != null &&
        ActiveEntry.Mission != null &&
        !completedMissionIndices.Contains(activeMissionIndex);

    public int ActiveMissionIndex => activeMissionIndex;
    public int ActiveProgress => activeProgress;
    public int ActiveRequiredAmount => GetActiveRequiredAmount();

    public Transform ActiveCompassTarget
    {
        get
        {
            if (!HasActiveMission)
            {
                return null;
            }

            Transform target = ActiveEntry.GetCompassTarget();

            return target != null && target.gameObject.activeInHierarchy
                ? target
                : null;
        }
    }

    public event Action<MissionDefinition2D> ActiveMissionChanged;
    public event Action<MissionDefinition2D, int, int>
        ActiveMissionProgressChanged;
    public event Action<MissionDefinition2D> MissionCompleted;
    public event Action MissionStateChanged;

    private readonly HashSet<int> completedMissionIndices =
        new HashSet<int>();

    private readonly HashSet<MissionEnemyTarget2D>
        subscribedEnemyTargets =
        new HashSet<MissionEnemyTarget2D>();

    private int activeMissionIndex = -1;
    private int lastCompletedMissionIndex = -1;
    private int activeProgress;
    private int lastObservedItemAmount;

    private bool inventorySubscribed;

    private MissionEntry2D ActiveEntry =>
        activeMissionIndex >= 0 &&
        activeMissionIndex < missions.Count
            ? missions[activeMissionIndex]
            : null;

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

        if (startFirstMissionOnStart &&
            activeMissionIndex < 0)
        {
            StartFirstAvailableMission();
        }
    }

    private void OnDisable()
    {
        UnsubscribeInventory();
        UnsubscribeEnemyTargets();
    }

    /// <summary>
    /// Inspectorの一覧番号を指定してミッションを開始します。
    /// 会話・イベント・UIボタンからも呼べます。
    /// </summary>
    public bool StartMission(int missionIndex)
    {
        FindInventoryController();
        SubscribeInventory();
        SubscribeEnemyTargets();

        if (missionIndex < 0 || missionIndex >= missions.Count)
        {
            LogWarning($"Mission Index が範囲外です: {missionIndex}");
            return false;
        }

        MissionEntry2D entry = missions[missionIndex];

        if (entry == null || entry.Mission == null)
        {
            LogWarning($"Mission {missionIndex} にMission Definitionが設定されていません。");
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

        activeMissionIndex = missionIndex;
        activeProgress = 0;
        lastObservedItemAmount = 0;

        InitializeActiveMissionProgress();

        ActiveMissionChanged?.Invoke(entry.Mission);
        NotifyProgressChanged();
        MissionStateChanged?.Invoke();

        Log($"ミッション開始: {entry.Mission.DisplayName}");

        if (IsActiveMissionComplete())
        {
            CompleteActiveMission();
        }

        return true;
    }

    /// <summary>
    /// 次の未完了ミッションを開始します。
    /// </summary>
    public bool StartNextMission()
    {
        int startIndex = activeMissionIndex >= 0
            ? activeMissionIndex + 1
            : lastCompletedMissionIndex + 1;

        return StartFirstAvailableMission(startIndex);
    }

    /// <summary>
    /// 一番先頭の未完了ミッションを開始します。
    /// </summary>
    public bool StartFirstAvailableMission()
    {
        return StartFirstAvailableMission(0);
    }

    /// <summary>
    /// 進行を再計算します。外部処理で直接Inventoryを変更した直後などに使えます。
    /// </summary>
    public void RefreshActiveMissionProgress()
    {
        if (!HasActiveMission)
        {
            return;
        }

        MissionDefinition2D mission = ActiveMission;

        if (mission.ObjectiveType ==
            MissionObjectiveType2D.CollectItem)
        {
            RefreshCollectItemProgress();
        }

        if (IsActiveMissionComplete())
        {
            CompleteActiveMission();
        }
    }

    [ContextMenu("Start First Available Mission")]
    private void StartFirstAvailableMissionFromContextMenu()
    {
        StartFirstAvailableMission();
    }

    [ContextMenu("Complete Active Mission")]
    private void CompleteActiveMissionFromContextMenu()
    {
        CompleteActiveMission();
    }

    private bool StartFirstAvailableMission(int startIndex)
    {
        for (int i = Mathf.Max(0, startIndex);
             i < missions.Count;
             i++)
        {
            if (missions[i] == null || missions[i].Mission == null)
            {
                continue;
            }

            if (!allowRestartCompletedMissions &&
                completedMissionIndices.Contains(i))
            {
                continue;
            }

            return StartMission(i);
        }

        ClearActiveMission();
        return false;
    }

    private void InitializeActiveMissionProgress()
    {
        MissionDefinition2D mission = ActiveMission;

        if (mission == null)
        {
            return;
        }

        switch (mission.ObjectiveType)
        {
            case MissionObjectiveType2D.CollectItem:
                InitializeCollectItemProgress(mission);
                break;

            case MissionObjectiveType2D.DefeatTargetEnemy:
                MissionEnemyTarget2D targetEnemy =
                    ActiveEntry.TargetEnemy;

                activeProgress = targetEnemy != null &&
                    targetEnemy.IsDefeated
                    ? 1
                    : 0;
                break;
        }
    }

    private void InitializeCollectItemProgress(
        MissionDefinition2D mission)
    {
        if (mission.RequiredItem == null ||
            inventoryController == null)
        {
            activeProgress = 0;
            lastObservedItemAmount = 0;
            return;
        }

        lastObservedItemAmount = Mathf.Max(
            0,
            inventoryController.GetTotalAmount(
                mission.RequiredItem
            )
        );

        activeProgress =
            mission.CountItemsAlreadyHeldWhenMissionStarts
                ? Mathf.Min(
                    mission.RequiredAmount,
                    lastObservedItemAmount
                )
                : 0;
    }

    private void HandleInventoryChanged()
    {
        if (!HasActiveMission ||
            ActiveMission.ObjectiveType !=
            MissionObjectiveType2D.CollectItem)
        {
            return;
        }

        RefreshCollectItemProgress();

        if (IsActiveMissionComplete())
        {
            CompleteActiveMission();
        }
    }

    private void RefreshCollectItemProgress()
    {
        MissionDefinition2D mission = ActiveMission;

        if (mission == null ||
            mission.RequiredItem == null ||
            inventoryController == null)
        {
            return;
        }

        int currentAmount = Mathf.Max(
            0,
            inventoryController.GetTotalAmount(
                mission.RequiredItem
            )
        );

        if (mission.CountItemsAlreadyHeldWhenMissionStarts)
        {
            activeProgress = Mathf.Min(
                mission.RequiredAmount,
                currentAmount
            );
        }
        else
        {
            int newlyAddedAmount = Mathf.Max(
                0,
                currentAmount - lastObservedItemAmount
            );

            if (newlyAddedAmount > 0)
            {
                activeProgress = Mathf.Min(
                    mission.RequiredAmount,
                    activeProgress + newlyAddedAmount
                );
            }
        }

        lastObservedItemAmount = currentAmount;

        NotifyProgressChanged();
        MissionStateChanged?.Invoke();
    }

    private void HandleTargetEnemyDefeated(
        MissionEnemyTarget2D defeatedEnemy)
    {
        if (!HasActiveMission ||
            ActiveMission.ObjectiveType !=
            MissionObjectiveType2D.DefeatTargetEnemy ||
            ActiveEntry.TargetEnemy != defeatedEnemy)
        {
            return;
        }

        activeProgress = 1;
        NotifyProgressChanged();
        MissionStateChanged?.Invoke();

        CompleteActiveMission();
    }

    private bool IsActiveMissionComplete()
    {
        return HasActiveMission &&
            activeProgress >= GetActiveRequiredAmount();
    }

    private int GetActiveRequiredAmount()
    {
        if (!HasActiveMission)
        {
            return 0;
        }

        return ActiveMission.ObjectiveType ==
            MissionObjectiveType2D.CollectItem
            ? ActiveMission.RequiredAmount
            : 1;
    }

    private void CompleteActiveMission()
    {
        if (!HasActiveMission)
        {
            return;
        }

        int completedIndex = activeMissionIndex;
        MissionDefinition2D completedMission = ActiveMission;

        completedMissionIndices.Add(completedIndex);
        lastCompletedMissionIndex = completedIndex;

        Log($"ミッション達成: {completedMission.DisplayName}");

        MissionCompleted?.Invoke(completedMission);
        MissionStateChanged?.Invoke();

        if (autoStartNextMission)
        {
            ClearActiveMission(false);
            StartFirstAvailableMission(completedIndex + 1);
            return;
        }

        ClearActiveMission();
    }

    private void ClearActiveMission(bool notify = true)
    {
        activeMissionIndex = -1;
        activeProgress = 0;
        lastObservedItemAmount = 0;

        if (notify)
        {
            ActiveMissionChanged?.Invoke(null);
            MissionStateChanged?.Invoke();
        }
    }

    private void NotifyProgressChanged()
    {
        if (!HasActiveMission)
        {
            return;
        }

        ActiveMissionProgressChanged?.Invoke(
            ActiveMission,
            activeProgress,
            GetActiveRequiredAmount()
        );
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
