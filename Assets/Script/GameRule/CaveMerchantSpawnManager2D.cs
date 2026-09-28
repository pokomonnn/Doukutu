using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 洞窟開始時に、候補地点から商人をランダム配置します。
/// 基本は1洞窟につき最大1人を想定しています。
/// </summary>
[DisallowMultipleComponent]
public class CaveMerchantSpawnManager2D : MonoBehaviour
{
    [Header("商人Prefab")]
    [SerializeField] private GameObject merchantPrefab;
    [SerializeField] private Transform spawnedMerchantParent;

    [Header("出現設定")]
    [SerializeField, Range(0f, 1f)] private float spawnChance = 0.55f;
    [SerializeField, Min(1)] private int maxMerchants = 1;
    [SerializeField, Min(0f)] private float minimumDistanceFromPlayer = 12f;

    [Header("出現候補")]
    [SerializeField] private List<CaveMerchantSpawnPoint2D> spawnPoints =
        new List<CaveMerchantSpawnPoint2D>();
    [SerializeField] private bool autoFindSpawnPoints = true;

    [Header("実行")]
    [SerializeField] private bool spawnOnStart = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    private readonly List<GameObject> spawnedMerchants =
        new List<GameObject>();
    private bool hasSpawned;

    private void Start()
    {
        if (spawnOnStart)
        {
            SpawnMerchants();
        }
    }

    [ContextMenu("Spawn Cave Merchants")]
    public void SpawnMerchants()
    {
        if (hasSpawned)
        {
            return;
        }

        hasSpawned = true;

        if (merchantPrefab == null)
        {
            LogWarning("Merchant Prefabが未設定です。");
            return;
        }

        if (Random.value > spawnChance)
        {
            Log("今回は商人を出現させませんでした。");
            return;
        }

        RefreshSpawnPointsIfNeeded();

        Transform player = FindPlayerTransform();
        List<CaveMerchantSpawnPoint2D> candidates =
            BuildCandidateList(player);

        int targetCount = Mathf.Min(
            Mathf.Max(1, maxMerchants),
            candidates.Count
        );

        for (int i = 0; i < targetCount; i++)
        {
            CaveMerchantSpawnPoint2D point =
                ChooseWeightedPoint(candidates);

            if (point == null)
            {
                break;
            }

            candidates.Remove(point);

            GameObject instance = Instantiate(
                merchantPrefab,
                point.transform.position,
                point.transform.rotation,
                spawnedMerchantParent
            );

            spawnedMerchants.Add(instance);
            Log($"商人を配置: {point.name}");
        }
    }

    private void RefreshSpawnPointsIfNeeded()
    {
        if (!autoFindSpawnPoints && spawnPoints.Count > 0)
        {
            return;
        }

        CaveMerchantSpawnPoint2D[] found =
            FindObjectsByType<CaveMerchantSpawnPoint2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        spawnPoints.Clear();
        spawnPoints.AddRange(found);
    }

    private List<CaveMerchantSpawnPoint2D> BuildCandidateList(
        Transform player)
    {
        List<CaveMerchantSpawnPoint2D> result =
            new List<CaveMerchantSpawnPoint2D>();

        foreach (CaveMerchantSpawnPoint2D point in spawnPoints)
        {
            if (point == null ||
                !point.AllowSpawn ||
                point.Weight <= 0f)
            {
                continue;
            }

            if (player != null &&
                minimumDistanceFromPlayer > 0f &&
                Vector2.Distance(
                    player.position,
                    point.transform.position
                ) < minimumDistanceFromPlayer)
            {
                continue;
            }

            result.Add(point);
        }

        return result;
    }

    private CaveMerchantSpawnPoint2D ChooseWeightedPoint(
        List<CaveMerchantSpawnPoint2D> candidates)
    {
        float totalWeight = 0f;

        foreach (CaveMerchantSpawnPoint2D point in candidates)
        {
            if (point != null)
            {
                totalWeight += point.Weight;
            }
        }

        if (totalWeight <= 0f)
        {
            return null;
        }

        float roll = Random.value * totalWeight;

        foreach (CaveMerchantSpawnPoint2D point in candidates)
        {
            if (point == null)
            {
                continue;
            }

            roll -= point.Weight;

            if (roll <= 0f)
            {
                return point;
            }
        }

        return candidates.Count > 0
            ? candidates[candidates.Count - 1]
            : null;
    }

    private Transform FindPlayerTransform()
    {
        PlayerTradeInventoryBridge bridge =
            FindAnyObjectByType<PlayerTradeInventoryBridge>();

        return bridge != null ? bridge.transform : null;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[CaveMerchantSpawnManager2D] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[CaveMerchantSpawnManager2D] {message}",
            this
        );
    }
}
