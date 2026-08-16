using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 複数のItemBoxSpawnPoint2Dから4～5個などをランダム選択し、
/// ItemBoxを生成します。
///
/// 既存WorldStateSessionStoreにこのManagerが生成した箱の保存データがある場合は、
/// PersistentIdから同じSpawnPoint・同じBox Profileを復元して生成します。
/// その後の「中身・開封状態」は既存WorldStateSaveBridgeが復元します。
/// </summary>
[DefaultExecutionOrder(-600)]
[DisallowMultipleComponent]
public class ItemBoxSpawnManager2D : MonoBehaviour
{
    private const string PersistentPrefix = "RIB2D";
    private const char Separator = '|';

    [Serializable]
    public class ItemBoxSpawnProfile
    {
        [Tooltip("保存復元にも使う箱タイプIDです。後から変更しないでください。例: common / medical")]
        [SerializeField] private string profileId = "default";

        [SerializeField] private GameObject itemBoxPrefab;
        [SerializeField] private ItemBoxLootTable lootTable;

        [Tooltip("複数Profileがある場合の選ばれやすさです。")]
        [SerializeField, Min(0f)] private float selectionWeight = 1f;

        public string ProfileId => profileId?.Trim() ?? string.Empty;
        public GameObject ItemBoxPrefab => itemBoxPrefab;
        public ItemBoxLootTable LootTable => lootTable;
        public float SelectionWeight => Mathf.Max(0f, selectionWeight);

        public void Validate(int index)
        {
            profileId = NormalizeToken(
                string.IsNullOrWhiteSpace(profileId)
                    ? $"profile_{index + 1}"
                    : profileId
            );

            selectionWeight = Mathf.Max(0f, selectionWeight);
        }
    }

    [Header("保存グループ")]
    [Tooltip("同じSceneに複数SpawnManagerがある場合は、必ず別IDにします。後から変更しないでください。")]
    [SerializeField] private string randomGroupId = "main_item_boxes";

    [Header("Spawn Point")]
    [Tooltip("ONなら、このManagerの子にあるItemBoxSpawnPoint2Dを自動取得します。")]
    [SerializeField] private bool autoFindSpawnPointsInChildren = true;

    [SerializeField]
    private List<ItemBoxSpawnPoint2D> spawnPoints =
        new List<ItemBoxSpawnPoint2D>();

    [Header("生成数")]
    [SerializeField, Min(0)] private int minimumSpawnCount = 4;
    [SerializeField, Min(0)] private int maximumSpawnCount = 5;

    [Header("生成するItemBox")]
    [SerializeField]
    private List<ItemBoxSpawnProfile> boxProfiles =
        new List<ItemBoxSpawnProfile>();

    [Tooltip("生成した箱をまとめる親Transform。未設定ならこのManagerの子にします。")]
    [SerializeField] private Transform spawnedBoxRoot;

    [Header("初回抽選")]
    [Tooltip("初回生成直後にWorldStateSessionStoreへ箱配置と中身を保存します。同じ探索のロード時に再抽選されないため、通常はON推奨です。")]
    [SerializeField] private bool saveInitialRollImmediately = true;

    [Header("デバッグ")]
    [SerializeField] private bool useFixedSeedForDebug;
    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private bool showDebugLogs = true;

    private readonly List<GameObject> spawnedBoxes =
        new List<GameObject>();

    public string RandomGroupId => NormalizeToken(randomGroupId);
    public IReadOnlyList<GameObject> SpawnedBoxes => spawnedBoxes;

    private void Awake()
    {
        RefreshSpawnPoints();
        SpawnFromSaveOrCreateNew();
    }

    [ContextMenu("Respawn Fresh Random Item Boxes")]
    public void RespawnFreshRandomItemBoxes()
    {
        ClearSavedBoxesForThisGroup();
        DestroySpawnedBoxes();
        RefreshSpawnPoints();
        SpawnFresh();

        if (saveInitialRollImmediately)
        {
            SaveCurrentRandomBoxesToSession();
        }
    }

    [ContextMenu("Save Current Random Item Boxes To Session")]
    public void SaveCurrentRandomBoxesToSession()
    {
        Scene scene = gameObject.scene.IsValid()
            ? gameObject.scene
            : SceneManager.GetActiveScene();

        SavedWorldStateData snapshot =
            WorldStateSessionStore.CreateSceneSnapshot(scene.name);

        List<SavedItemBoxData> merged =
            new List<SavedItemBoxData>();

        if (snapshot?.ItemBoxes != null)
        {
            foreach (SavedItemBoxData saved in snapshot.ItemBoxes)
            {
                if (saved == null ||
                    IsPersistentIdForGroup(saved.PersistentId, RandomGroupId))
                {
                    continue;
                }

                merged.Add(saved);
            }
        }

        int savedCount = 0;

        foreach (GameObject spawnedBox in spawnedBoxes)
        {
            if (spawnedBox == null || !spawnedBox.activeInHierarchy)
            {
                continue;
            }

            ItemBoxSaveIdentity identity =
                spawnedBox.GetComponent<ItemBoxSaveIdentity>();

            if (identity == null)
            {
                continue;
            }

            SavedItemBoxData data = identity.CreateSaveData();

            if (data == null)
            {
                continue;
            }

            merged.Add(data);
            savedCount++;
        }

        WorldStateSessionStore.ReplaceSceneItemBoxes(
            scene.name,
            merged
        );

        Log(
            $"初回ランダム箱状態をSessionへ保存: " +
            $"Scene={scene.name} / RandomBoxes={savedCount}"
        );
    }

    public void ClearSavedBoxesForThisGroup()
    {
        Scene scene = gameObject.scene.IsValid()
            ? gameObject.scene
            : SceneManager.GetActiveScene();

        ClearSavedBoxesForGroup(scene.name, RandomGroupId);
    }

    public static int ClearSavedBoxesForGroup(
        string sceneName,
        string groupId)
    {
        string targetScene = sceneName?.Trim() ?? string.Empty;
        string targetGroup = NormalizeToken(groupId);

        if (string.IsNullOrWhiteSpace(targetScene) ||
            string.IsNullOrWhiteSpace(targetGroup))
        {
            return 0;
        }

        SavedWorldStateData snapshot =
            WorldStateSessionStore.CreateSceneSnapshot(targetScene);

        List<SavedItemBoxData> kept =
            new List<SavedItemBoxData>();

        int removed = 0;

        if (snapshot?.ItemBoxes != null)
        {
            foreach (SavedItemBoxData saved in snapshot.ItemBoxes)
            {
                if (saved == null)
                {
                    continue;
                }

                if (IsPersistentIdForGroup(
                        saved.PersistentId,
                        targetGroup))
                {
                    removed++;
                    continue;
                }

                kept.Add(saved);
            }
        }

        WorldStateSessionStore.ReplaceSceneItemBoxes(
            targetScene,
            kept
        );

        return removed;
    }

    private void SpawnFromSaveOrCreateNew()
    {
        Scene scene = gameObject.scene.IsValid()
            ? gameObject.scene
            : SceneManager.GetActiveScene();

        SavedWorldStateData snapshot =
            WorldStateSessionStore.CreateSceneSnapshot(scene.name);

        List<SavedItemBoxData> savedForThisGroup =
            new List<SavedItemBoxData>();

        if (snapshot?.ItemBoxes != null)
        {
            foreach (SavedItemBoxData saved in snapshot.ItemBoxes)
            {
                if (saved != null &&
                    IsPersistentIdForGroup(
                        saved.PersistentId,
                        RandomGroupId))
                {
                    savedForThisGroup.Add(saved);
                }
            }
        }

        if (savedForThisGroup.Count > 0)
        {
            int restored = SpawnSavedPlacements(savedForThisGroup);

            Log(
                $"保存済みランダム箱配置を再生成: " +
                $"Scene={scene.name} / {restored}/{savedForThisGroup.Count}箱"
            );

            return;
        }

        SpawnFresh();

        if (saveInitialRollImmediately)
        {
            SaveCurrentRandomBoxesToSession();
        }
    }

    private void SpawnFresh()
    {
        List<ItemBoxSpawnPoint2D> validPoints = GetValidSpawnPoints();
        List<ItemBoxSpawnProfile> validProfiles = GetValidProfiles();

        if (validPoints.Count == 0)
        {
            LogWarning("有効なItemBoxSpawnPoint2Dがありません。");
            return;
        }

        if (validProfiles.Count == 0)
        {
            LogWarning("有効なItemBox Spawn Profileがありません。Prefabを設定してください。");
            return;
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
            ItemBoxSpawnPoint2D point = validPoints[i];
            ItemBoxSpawnProfile profile =
                SelectWeightedProfile(validProfiles, random);

            if (point == null || profile == null)
            {
                continue;
            }

            string persistentId = BuildPersistentId(
                RandomGroupId,
                point.SpawnPointId,
                profile.ProfileId
            );

            if (SpawnBox(
                    point,
                    profile,
                    persistentId,
                    true,
                    random) != null)
            {
                spawned++;
            }
        }

        Log(
            $"新規ランダム生成完了: " +
            $"SpawnPoints={validPoints.Count} / Boxes={spawned}"
        );
    }

    private int SpawnSavedPlacements(
        IReadOnlyList<SavedItemBoxData> savedBoxes)
    {
        List<ItemBoxSpawnPoint2D> validPoints = GetValidSpawnPoints();
        List<ItemBoxSpawnProfile> validProfiles = GetValidProfiles();

        Dictionary<string, ItemBoxSpawnPoint2D> pointLookup =
            new Dictionary<string, ItemBoxSpawnPoint2D>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (ItemBoxSpawnPoint2D point in validPoints)
        {
            string id = NormalizeToken(point.SpawnPointId);

            if (!string.IsNullOrWhiteSpace(id) &&
                !pointLookup.ContainsKey(id))
            {
                pointLookup.Add(id, point);
            }
        }

        Dictionary<string, ItemBoxSpawnProfile> profileLookup =
            new Dictionary<string, ItemBoxSpawnProfile>(
                StringComparer.OrdinalIgnoreCase
            );

        foreach (ItemBoxSpawnProfile profile in validProfiles)
        {
            string id = NormalizeToken(profile.ProfileId);

            if (!string.IsNullOrWhiteSpace(id) &&
                !profileLookup.ContainsKey(id))
            {
                profileLookup.Add(id, profile);
            }
        }

        HashSet<string> usedPoints =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        int spawned = 0;

        foreach (SavedItemBoxData saved in savedBoxes)
        {
            if (saved == null ||
                !TryParsePersistentId(
                    saved.PersistentId,
                    out string groupId,
                    out string spawnPointId,
                    out string profileId) ||
                !string.Equals(
                    groupId,
                    RandomGroupId,
                    StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!pointLookup.TryGetValue(
                    spawnPointId,
                    out ItemBoxSpawnPoint2D point))
            {
                LogWarning(
                    $"保存データのSpawnPointが見つかりません: {spawnPointId}"
                );
                continue;
            }

            if (!usedPoints.Add(spawnPointId))
            {
                LogWarning(
                    $"同じSpawnPointの保存箱が重複しています: {spawnPointId}"
                );
                continue;
            }

            if (!profileLookup.TryGetValue(
                    profileId,
                    out ItemBoxSpawnProfile profile))
            {
                LogWarning(
                    $"保存データのProfile『{profileId}』が見つかりません。" +
                    "先頭の有効Profileで代替します。"
                );

                profile = validProfiles.Count > 0
                    ? validProfiles[0]
                    : null;
            }

            if (profile == null)
            {
                continue;
            }

            // 中身はここでは抽選しない。
            // この後WorldStateSaveBridgeがPersistentId一致で完全復元する。
            if (SpawnBox(
                    point,
                    profile,
                    saved.PersistentId,
                    false,
                    null) != null)
            {
                spawned++;
            }
        }

        return spawned;
    }

    private GameObject SpawnBox(
        ItemBoxSpawnPoint2D point,
        ItemBoxSpawnProfile profile,
        string persistentId,
        bool generateLoot,
        System.Random random)
    {
        if (point == null ||
            profile == null ||
            profile.ItemBoxPrefab == null)
        {
            return null;
        }

        Transform parent = spawnedBoxRoot != null
            ? spawnedBoxRoot
            : transform;

        GameObject instance = Instantiate(
            profile.ItemBoxPrefab,
            point.SpawnPosition,
            point.SpawnRotation,
            parent
        );

        instance.name =
            $"RandomItemBox_{NormalizeToken(point.SpawnPointId)}";

        ItemBoxInventory inventory =
            instance.GetComponent<ItemBoxInventory>();

        if (inventory == null)
        {
            LogWarning(
                $"Prefab『{profile.ItemBoxPrefab.name}』にItemBoxInventoryがありません。"
            );
            Destroy(instance);
            return null;
        }

        ItemBoxSaveIdentity identity =
            instance.GetComponent<ItemBoxSaveIdentity>();

        if (identity == null)
        {
            identity = instance.AddComponent<ItemBoxSaveIdentity>();
        }

        identity.AssignPersistentId(persistentId);

        ItemBoxRandomLootInitializer initializer =
            instance.GetComponent<ItemBoxRandomLootInitializer>();

        if (initializer == null)
        {
            initializer = instance.AddComponent<ItemBoxRandomLootInitializer>();
        }

        initializer.SetManagedBySpawnManager(true);
        initializer.SetLootTable(profile.LootTable);

        if (generateLoot)
        {
            initializer.RollLoot(random, true);
        }
        else
        {
            initializer.PrepareForSavedRestore();
        }

        spawnedBoxes.Add(instance);

        Log(
            $"箱生成: Point={point.name}({point.SpawnPointId}) / " +
            $"Profile={profile.ProfileId} / " +
            $"Mode={(generateLoot ? "新規抽選" : "セーブ復元待ち")}"
        );

        return instance;
    }

    private void RefreshSpawnPoints()
    {
        if (!autoFindSpawnPointsInChildren)
        {
            return;
        }

        ItemBoxSpawnPoint2D[] found =
            GetComponentsInChildren<ItemBoxSpawnPoint2D>(true);

        spawnPoints.Clear();

        foreach (ItemBoxSpawnPoint2D point in found)
        {
            if (point != null)
            {
                spawnPoints.Add(point);
            }
        }
    }

    private List<ItemBoxSpawnPoint2D> GetValidSpawnPoints()
    {
        List<ItemBoxSpawnPoint2D> result =
            new List<ItemBoxSpawnPoint2D>();

        HashSet<string> ids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (spawnPoints == null)
        {
            return result;
        }

        foreach (ItemBoxSpawnPoint2D point in spawnPoints)
        {
            if (point == null)
            {
                continue;
            }

            string id = NormalizeToken(point.SpawnPointId);

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

    private List<ItemBoxSpawnProfile> GetValidProfiles()
    {
        List<ItemBoxSpawnProfile> result =
            new List<ItemBoxSpawnProfile>();

        HashSet<string> ids =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (boxProfiles == null)
        {
            return result;
        }

        for (int i = 0; i < boxProfiles.Count; i++)
        {
            ItemBoxSpawnProfile profile = boxProfiles[i];

            if (profile == null || profile.ItemBoxPrefab == null)
            {
                continue;
            }

            string id = NormalizeToken(profile.ProfileId);

            if (string.IsNullOrWhiteSpace(id))
            {
                LogWarning($"Box Profile[{i}]のProfile Idが空です。");
                continue;
            }

            if (!ids.Add(id))
            {
                LogWarning($"Box Profile Idが重複しています: {id}");
                continue;
            }

            result.Add(profile);
        }

        return result;
    }

    private static ItemBoxSpawnProfile SelectWeightedProfile(
        IReadOnlyList<ItemBoxSpawnProfile> profiles,
        System.Random random)
    {
        if (profiles == null || profiles.Count == 0)
        {
            return null;
        }

        float totalWeight = 0f;

        foreach (ItemBoxSpawnProfile profile in profiles)
        {
            if (profile != null)
            {
                totalWeight += profile.SelectionWeight;
            }
        }

        if (totalWeight <= 0f)
        {
            return profiles[0];
        }

        double roll = random.NextDouble() * totalWeight;
        float accumulated = 0f;

        foreach (ItemBoxSpawnProfile profile in profiles)
        {
            if (profile == null)
            {
                continue;
            }

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
        int seed = useFixedSeedForDebug
            ? fixedSeed
            : unchecked(
                Environment.TickCount ^
                Guid.NewGuid().GetHashCode() ^
                gameObject.scene.name.GetHashCode()
            );

        Log($"Random Seed={seed}");
        return new System.Random(seed);
    }

    private void DestroySpawnedBoxes()
    {
        foreach (GameObject spawnedBox in spawnedBoxes)
        {
            if (spawnedBox == null)
            {
                continue;
            }

            spawnedBox.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(spawnedBox);
            }
            else
            {
                DestroyImmediate(spawnedBox);
            }
        }

        spawnedBoxes.Clear();
    }

    public static string BuildPersistentId(
        string groupId,
        string spawnPointId,
        string profileId)
    {
        return string.Join(
            Separator.ToString(),
            PersistentPrefix,
            NormalizeToken(groupId),
            NormalizeToken(spawnPointId),
            NormalizeToken(profileId)
        );
    }

    public static bool IsPersistentIdForGroup(
        string persistentId,
        string groupId)
    {
        return TryParsePersistentId(
                   persistentId,
                   out string parsedGroup,
                   out _,
                   out _
               ) &&
               string.Equals(
                   parsedGroup,
                   NormalizeToken(groupId),
                   StringComparison.OrdinalIgnoreCase
               );
    }

    public static bool TryParsePersistentId(
        string persistentId,
        out string groupId,
        out string spawnPointId,
        out string profileId)
    {
        groupId = string.Empty;
        spawnPointId = string.Empty;
        profileId = string.Empty;

        if (string.IsNullOrWhiteSpace(persistentId))
        {
            return false;
        }

        string[] parts = persistentId.Split(Separator);

        if (parts.Length != 4 ||
            !string.Equals(
                parts[0],
                PersistentPrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        groupId = NormalizeToken(parts[1]);
        spawnPointId = NormalizeToken(parts[2]);
        profileId = NormalizeToken(parts[3]);

        return !string.IsNullOrWhiteSpace(groupId) &&
               !string.IsNullOrWhiteSpace(spawnPointId) &&
               !string.IsNullOrWhiteSpace(profileId);
    }

    public static string NormalizeToken(string value)
    {
        string result = value?.Trim() ?? string.Empty;
        result = result.Replace(Separator, '_');
        return result;
    }

    private static void Shuffle<T>(
        IList<T> list,
        System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random.Next(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ItemBoxSpawnManager2D] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[ItemBoxSpawnManager2D] {message}", this);
    }

    private void OnValidate()
    {
        randomGroupId = NormalizeToken(randomGroupId);

        if (string.IsNullOrWhiteSpace(randomGroupId))
        {
            randomGroupId = "main_item_boxes";
        }

        minimumSpawnCount = Mathf.Max(0, minimumSpawnCount);
        maximumSpawnCount = Mathf.Max(minimumSpawnCount, maximumSpawnCount);

        if (boxProfiles == null)
        {
            boxProfiles = new List<ItemBoxSpawnProfile>();
        }

        for (int i = 0; i < boxProfiles.Count; i++)
        {
            boxProfiles[i]?.Validate(i);
        }
    }
}
