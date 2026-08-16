using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// マップ内に置いた複数のRescuePersonSpawnPoint2Dから、
/// 指定数の地点を重複なしでランダム選択し、救出対象NPCを生成します。
///
/// ・候補地点の中から毎回ランダム配置
/// ・複数の救出NPC Prefabを重み付きでランダム選択可能
/// ・特定Missionが進行中の時だけ生成する設定に対応
/// ・既存MissionManager2Dを変更せず使用可能
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public class RescuePersonSpawnManager2D : MonoBehaviour
{
    [Serializable]
    public class RescuePersonProfile
    {
        [Tooltip("識別用IDです。例: survivor_male / survivor_female")]
        [SerializeField] private string profileId = "default";

        [Tooltip("生成する救出対象NPCのPrefabです。")]
        [SerializeField] private GameObject rescuePersonPrefab;

        [Tooltip("複数Profileがある場合の選ばれやすさです。0は選ばれません。")]
        [SerializeField, Min(0f)] private float selectionWeight = 1f;

        public string ProfileId => profileId?.Trim() ?? string.Empty;
        public GameObject RescuePersonPrefab => rescuePersonPrefab;
        public float SelectionWeight => Mathf.Max(0f, selectionWeight);

        public void Validate(int index)
        {
            if (string.IsNullOrWhiteSpace(profileId))
            {
                profileId = $"profile_{index + 1}";
            }

            profileId = profileId.Trim();
            selectionWeight = Mathf.Max(0f, selectionWeight);
        }
    }

    [Header("Spawn Point")]
    [Tooltip("ONなら、このManagerの子にあるRescuePersonSpawnPoint2Dを自動取得します。")]
    [SerializeField] private bool autoFindSpawnPointsInChildren = true;

    [SerializeField]
    private List<RescuePersonSpawnPoint2D> spawnPoints =
        new List<RescuePersonSpawnPoint2D>();

    [Header("生成数")]
    [Tooltip("通常の救出ミッションなら1がおすすめです。")]
    [SerializeField, Min(0)] private int minimumSpawnCount = 1;

    [Tooltip("通常の救出ミッションなら1がおすすめです。複数人救出なら増やせます。")]
    [SerializeField, Min(0)] private int maximumSpawnCount = 1;

    [Header("救出対象NPC")]
    [SerializeField]
    private List<RescuePersonProfile> personProfiles =
        new List<RescuePersonProfile>();

    [Tooltip("生成したNPCをまとめる親Transform。未設定ならこのManagerの子にします。")]
    [SerializeField] private Transform spawnedPersonRoot;

    [Header("ミッション連動（任意）")]
    [Tooltip("ONなら、指定したMissionが進行中の時だけ救出NPCを生成します。")]
    [SerializeField] private bool spawnOnlyWhileMissionInProgress;

    [Tooltip("Spawn Only While Mission In ProgressがONの時に指定します。")]
    [SerializeField] private MissionDefinition2D requiredMission;

    [Tooltip("GameSessionManagerを優先して受注状態を確認します。町から探索シーンへ移動する構成ではON推奨です。")]
    [SerializeField] private bool preferGameSessionMissionState = true;

    [Header("生成タイミング")]
    [Tooltip("ONならAwakeで自動生成します。")]
    [SerializeField] private bool spawnOnAwake = true;

    [Header("デバッグ")]
    [Tooltip("ONにすると毎回同じ配置を再現できます。通常プレイではOFFにしてください。")]
    [SerializeField] private bool useFixedSeedForDebug;

    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private bool showDebugLogs = true;

    private readonly List<GameObject> spawnedPersons =
        new List<GameObject>();

    public IReadOnlyList<GameObject> SpawnedPersons => spawnedPersons;
    public int SpawnedCount => spawnedPersons.Count;

    private void Awake()
    {
        RefreshSpawnPoints();

        if (spawnOnAwake)
        {
            SpawnRescuePersons();
        }
    }

    /// <summary>
    /// 現在の設定で救出NPCを生成します。
    /// 既にこのManagerが生成したNPCがある場合は一度削除してから生成します。
    /// </summary>
    [ContextMenu("Spawn Rescue Persons")]
    public int SpawnRescuePersons()
    {
        DestroySpawnedPersons();
        RefreshSpawnPoints();

        if (!CanSpawnForMissionState())
        {
            Log("対象Missionが進行中ではないため、救出NPCを生成しませんでした。");
            return 0;
        }

        List<RescuePersonSpawnPoint2D> validPoints =
            GetValidSpawnPoints();

        List<RescuePersonProfile> validProfiles =
            GetValidProfiles();

        if (validPoints.Count == 0)
        {
            LogWarning("有効なRescuePersonSpawnPoint2Dがありません。");
            return 0;
        }

        if (validProfiles.Count == 0)
        {
            LogWarning("有効なRescue Person Profileがありません。Prefabを設定してください。");
            return 0;
        }

        int max = Mathf.Min(
            Mathf.Max(minimumSpawnCount, maximumSpawnCount),
            validPoints.Count
        );

        int min = Mathf.Min(
            Mathf.Max(0, minimumSpawnCount),
            max
        );

        System.Random random = CreateRandom();

        int spawnCount = min >= max
            ? min
            : random.Next(min, max + 1);

        Shuffle(validPoints, random);

        int spawned = 0;

        for (int i = 0; i < spawnCount; i++)
        {
            RescuePersonSpawnPoint2D point = validPoints[i];
            RescuePersonProfile profile =
                SelectWeightedProfile(validProfiles, random);

            if (point == null || profile == null)
            {
                continue;
            }

            if (SpawnPerson(point, profile) != null)
            {
                spawned++;
            }
        }

        Log(
            $"救出NPCランダム生成完了: " +
            $"候補地点={validPoints.Count} / 生成={spawned}"
        );

        return spawned;
    }

    /// <summary>
    /// このManagerが生成した救出NPCだけ削除します。
    /// </summary>
    [ContextMenu("Destroy Spawned Rescue Persons")]
    public void DestroySpawnedPersons()
    {
        for (int i = spawnedPersons.Count - 1; i >= 0; i--)
        {
            GameObject instance = spawnedPersons[i];

            if (instance == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(instance);
            }
            else
            {
                DestroyImmediate(instance);
            }
        }

        spawnedPersons.Clear();
    }

    public void RefreshSpawnPoints()
    {
        if (!autoFindSpawnPointsInChildren)
        {
            return;
        }

        RescuePersonSpawnPoint2D[] found =
            GetComponentsInChildren<RescuePersonSpawnPoint2D>(true);

        spawnPoints.Clear();

        foreach (RescuePersonSpawnPoint2D point in found)
        {
            if (point != null)
            {
                spawnPoints.Add(point);
            }
        }
    }

    private GameObject SpawnPerson(
        RescuePersonSpawnPoint2D point,
        RescuePersonProfile profile)
    {
        if (point == null ||
            profile == null ||
            profile.RescuePersonPrefab == null)
        {
            return null;
        }

        Transform parent = spawnedPersonRoot != null
            ? spawnedPersonRoot
            : transform;

        GameObject instance = Instantiate(
            profile.RescuePersonPrefab,
            point.SpawnPosition,
            point.SpawnRotation,
            parent
        );

        instance.name =
            $"RescuePerson_{point.SpawnPointId}_{profile.ProfileId}";

        RescuePersonSpawnedMarker2D marker =
            instance.GetComponent<RescuePersonSpawnedMarker2D>();

        if (marker == null)
        {
            marker = instance.AddComponent<RescuePersonSpawnedMarker2D>();
        }

        marker.Initialize(
            this,
            point.SpawnPointId,
            profile.ProfileId
        );

        spawnedPersons.Add(instance);

        Log(
            $"救出NPC生成: Point={point.name}({point.SpawnPointId}) / " +
            $"Profile={profile.ProfileId} / Prefab={profile.RescuePersonPrefab.name}"
        );

        return instance;
    }

    private bool CanSpawnForMissionState()
    {
        if (!spawnOnlyWhileMissionInProgress)
        {
            return true;
        }

        if (requiredMission == null)
        {
            LogWarning(
                "Spawn Only While Mission In ProgressがONですが、" +
                "Required Missionが未設定です。"
            );
            return false;
        }

        if (preferGameSessionMissionState &&
            TryGetSessionMissionInProgress(out bool sessionInProgress))
        {
            return sessionInProgress;
        }

        if (TryGetManagerMissionInProgress(out bool managerInProgress))
        {
            return managerInProgress;
        }

        // Managerが見つからなかった場合、Session側をまだ確認していなければ最後に確認する。
        if (!preferGameSessionMissionState &&
            TryGetSessionMissionInProgress(out sessionInProgress))
        {
            return sessionInProgress;
        }

        LogWarning(
            $"Mission状態を確認できませんでした: {requiredMission.DisplayName}"
        );
        return false;
    }

    private bool TryGetSessionMissionInProgress(out bool inProgress)
    {
        inProgress = false;

        GameSessionManager session = GameSessionManager.Instance;

        if (session == null)
        {
            session = FindAnyObjectByType<GameSessionManager>(
                FindObjectsInactive.Include
            );
        }

        if (session == null)
        {
            return false;
        }

        string missionId = requiredMission != null
            ? (requiredMission.MissionId ?? string.Empty).Trim()
            : string.Empty;

        if (string.IsNullOrWhiteSpace(missionId))
        {
            return false;
        }

        if (!session.TryGetMissionSession(
                missionId,
                out MissionSessionData data) ||
            data == null)
        {
            // SessionManager自体は存在するため、未受注として扱えます。
            inProgress = false;
            return true;
        }

        inProgress = data.State == MissionSessionState.InProgress;
        return true;
    }

    private bool TryGetManagerMissionInProgress(out bool inProgress)
    {
        inProgress = false;

        MissionManager2D manager =
            FindAnyObjectByType<MissionManager2D>(
                FindObjectsInactive.Include
            );

        if (manager == null || requiredMission == null)
        {
            return false;
        }

        int missionIndex = FindMissionIndex(manager, requiredMission);

        if (missionIndex < 0)
        {
            return false;
        }

        inProgress = manager.GetMissionState(missionIndex) ==
            MissionProgressState2D.InProgress;

        return true;
    }

    private static int FindMissionIndex(
        MissionManager2D manager,
        MissionDefinition2D mission)
    {
        if (manager == null || mission == null)
        {
            return -1;
        }

        string targetMissionId = mission.MissionId?.Trim() ?? string.Empty;

        for (int i = 0; i < manager.MissionCount; i++)
        {
            MissionDefinition2D registered =
                manager.GetMissionDefinition(i);

            if (registered == null)
            {
                continue;
            }

            if (registered == mission)
            {
                return i;
            }

            if (!string.IsNullOrWhiteSpace(targetMissionId) &&
                string.Equals(
                    registered.MissionId?.Trim(),
                    targetMissionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private List<RescuePersonSpawnPoint2D> GetValidSpawnPoints()
    {
        List<RescuePersonSpawnPoint2D> result =
            new List<RescuePersonSpawnPoint2D>();

        HashSet<string> ids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (spawnPoints == null)
        {
            return result;
        }

        foreach (RescuePersonSpawnPoint2D point in spawnPoints)
        {
            if (point == null || !point.CanSpawnHere)
            {
                continue;
            }

            string id = point.SpawnPointId;

            if (string.IsNullOrWhiteSpace(id))
            {
                LogWarning($"SpawnPoint『{point.name}』のIDが空です。");
                continue;
            }

            if (!ids.Add(id))
            {
                LogWarning($"SpawnPoint IDが重複しています: {id}");
                continue;
            }

            result.Add(point);
        }

        return result;
    }

    private List<RescuePersonProfile> GetValidProfiles()
    {
        List<RescuePersonProfile> result =
            new List<RescuePersonProfile>();

        HashSet<string> ids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (personProfiles == null)
        {
            return result;
        }

        for (int i = 0; i < personProfiles.Count; i++)
        {
            RescuePersonProfile profile = personProfiles[i];

            if (profile == null ||
                profile.RescuePersonPrefab == null ||
                profile.SelectionWeight <= 0f)
            {
                continue;
            }

            string id = profile.ProfileId;

            if (string.IsNullOrWhiteSpace(id))
            {
                LogWarning($"Person Profile[{i}] のProfile Idが空です。");
                continue;
            }

            if (!ids.Add(id))
            {
                LogWarning($"Person Profile IDが重複しています: {id}");
                continue;
            }

            result.Add(profile);
        }

        return result;
    }

    private RescuePersonProfile SelectWeightedProfile(
        List<RescuePersonProfile> profiles,
        System.Random random)
    {
        if (profiles == null || profiles.Count == 0)
        {
            return null;
        }

        if (profiles.Count == 1)
        {
            return profiles[0];
        }

        double totalWeight = 0d;

        foreach (RescuePersonProfile profile in profiles)
        {
            totalWeight += profile.SelectionWeight;
        }

        if (totalWeight <= 0d)
        {
            return profiles[0];
        }

        double roll = random.NextDouble() * totalWeight;
        double accumulated = 0d;

        foreach (RescuePersonProfile profile in profiles)
        {
            accumulated += profile.SelectionWeight;

            if (roll <= accumulated)
            {
                return profile;
            }
        }

        return profiles[profiles.Count - 1];
    }

    private System.Random CreateRandom()
    {
        if (useFixedSeedForDebug)
        {
            Log($"固定Seedで抽選します: {fixedSeed}");
            return new System.Random(fixedSeed);
        }

        int seed = unchecked(
            Environment.TickCount ^
            GetInstanceID() ^
            DateTime.UtcNow.Millisecond
        );

        return new System.Random(seed);
    }

    private static void Shuffle<T>(
        IList<T> list,
        System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[RescuePersonSpawnManager2D] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[RescuePersonSpawnManager2D] {message}",
            this
        );
    }

    private void OnValidate()
    {
        minimumSpawnCount = Mathf.Max(0, minimumSpawnCount);
        maximumSpawnCount = Mathf.Max(0, maximumSpawnCount);

        if (maximumSpawnCount < minimumSpawnCount)
        {
            maximumSpawnCount = minimumSpawnCount;
        }

        if (personProfiles == null)
        {
            personProfiles = new List<RescuePersonProfile>();
        }

        for (int i = 0; i < personProfiles.Count; i++)
        {
            personProfiles[i]?.Validate(i);
        }
    }
}
