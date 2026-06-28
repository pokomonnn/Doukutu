using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// SAN値に応じて、このControllerが追加で出す敵の数と
/// 奇形敵の出現率を変えるスポーナーです。
/// </summary>
[DisallowMultipleComponent]
public class SanityEnemySpawnController : MonoBehaviour
{
    [Serializable]
    private class SpawnRule
    {
        [Tooltip("このSAN段階で、このスポーナーが維持する追加敵の最大数")]
        [Min(0)] public int targetAliveEnemyCount = 2;

        [Tooltip("次の1体を出すまでの最短秒数")]
        [Min(0.05f)] public float spawnInterval = 3f;

        [Tooltip("0〜1。奇形敵Prefabが設定されている時だけ使われます")]
        [Range(0f, 1f)] public float mutantSpawnChance = 0f;
    }

    [Header("参照")]
    [SerializeField] private PlayerSanityController sanityController;

    [Tooltip("未設定ならSAN ControllerのTransformを使います")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("未設定ならSAN Controllerと同じGameObjectから自動取得します")]
    [SerializeField] private CharacterHealth playerHealth;

    [Header("通常敵Prefab")]
    [SerializeField] private GameObject[] normalEnemyPrefabs;

    [Header("奇形敵Prefab")]
    [Tooltip("SAN低下時に追加で抽選される敵Prefabを設定")]
    [SerializeField] private GameObject[] mutantEnemyPrefabs;

    [Header("出現場所")]
    [Tooltip("敵を出したい地点を入れます。空なら、このGameObjectの子Transformを出現地点として使います")]
    [SerializeField] private Transform[] spawnPoints;

    [Tooltip("プレイヤーに近すぎるSpawn Pointからは出しません")]
    [SerializeField, Min(0f)] private float minimumDistanceFromPlayer = 8f;

    [Header("SAN段階ごとの追加敵")]
    [SerializeField]
    private SpawnRule normalRule = new SpawnRule
    {
        targetAliveEnemyCount = 2,
        spawnInterval = 3.5f,
        mutantSpawnChance = 0f
    };

    [SerializeField]
    private SpawnRule warningRule = new SpawnRule
    {
        targetAliveEnemyCount = 3,
        spawnInterval = 3f,
        mutantSpawnChance = 0f
    };

    [SerializeField]
    private SpawnRule lowRule = new SpawnRule
    {
        targetAliveEnemyCount = 5,
        spawnInterval = 2.2f,
        mutantSpawnChance = 0.2f
    };

    [SerializeField]
    private SpawnRule criticalRule = new SpawnRule
    {
        targetAliveEnemyCount = 7,
        spawnInterval = 1.6f,
        mutantSpawnChance = 0.5f
    };

    [SerializeField]
    private SpawnRule emptyRule = new SpawnRule
    {
        targetAliveEnemyCount = 9,
        spawnInterval = 1f,
        mutantSpawnChance = 0.8f
    };

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    private readonly List<GameObject> spawnedEnemies =
        new List<GameObject>();

    private readonly List<Transform> cachedSpawnPoints =
        new List<Transform>();

    private float nextSpawnTime;

    private void Awake()
    {
        FindReferences();
        CacheChildSpawnPointsIfNeeded();
    }

    private void OnEnable()
    {
        FindReferences();
        CacheChildSpawnPointsIfNeeded();
        nextSpawnTime = Time.time;
    }

    private void Update()
    {
        FindReferences();
        RemoveDestroyedEnemies();

        if (sanityController == null ||
            (playerHealth != null && playerHealth.IsDead) ||
            Time.time < nextSpawnTime)
        {
            return;
        }

        SpawnRule rule = GetCurrentRule(sanityController.CurrentState);

        if (rule == null ||
            spawnedEnemies.Count >= rule.targetAliveEnemyCount)
        {
            return;
        }

        TrySpawnEnemy(rule);
    }

    [ContextMenu("Clear Spawned SAN Enemies")]
    public void ClearSpawnedEnemies()
    {
        foreach (GameObject enemy in spawnedEnemies)
        {
            if (enemy != null)
            {
                Destroy(enemy);
            }
        }

        spawnedEnemies.Clear();
    }

    private void TrySpawnEnemy(SpawnRule rule)
    {
        Transform spawnPoint = GetRandomValidSpawnPoint();

        if (spawnPoint == null)
        {
            nextSpawnTime = Time.time + rule.spawnInterval;
            return;
        }

        GameObject enemyPrefab = GetEnemyPrefab(rule.mutantSpawnChance);

        if (enemyPrefab == null)
        {
            nextSpawnTime = Time.time + rule.spawnInterval;
            return;
        }

        GameObject enemy = Instantiate(
            enemyPrefab,
            spawnPoint.position,
            spawnPoint.rotation
        );

        spawnedEnemies.Add(enemy);
        nextSpawnTime = Time.time + rule.spawnInterval;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[SanityEnemySpawnController] " +
                $"{enemyPrefab.name} を出現させました。" +
                $" SAN状態={sanityController.CurrentState}",
                this
            );
        }
    }

    private GameObject GetEnemyPrefab(float mutantChance)
    {
        bool canSpawnMutant = HasValidPrefab(mutantEnemyPrefabs);
        bool shouldSpawnMutant =
            canSpawnMutant && UnityEngine.Random.value < mutantChance;

        if (shouldSpawnMutant)
        {
            GameObject mutantPrefab = GetRandomPrefab(mutantEnemyPrefabs);

            if (mutantPrefab != null)
            {
                return mutantPrefab;
            }
        }

        return GetRandomPrefab(normalEnemyPrefabs);
    }

    private Transform GetRandomValidSpawnPoint()
    {
        CacheChildSpawnPointsIfNeeded();

        List<Transform> validPoints = new List<Transform>();

        foreach (Transform point in cachedSpawnPoints)
        {
            if (point == null)
            {
                continue;
            }

            if (playerTransform != null &&
                Vector2.Distance(playerTransform.position, point.position) <
                minimumDistanceFromPlayer)
            {
                continue;
            }

            validPoints.Add(point);
        }

        if (validPoints.Count == 0)
        {
            return null;
        }

        return validPoints[UnityEngine.Random.Range(0, validPoints.Count)];
    }

    private SpawnRule GetCurrentRule(SanityState state)
    {
        switch (state)
        {
            case SanityState.Warning:
                return warningRule;

            case SanityState.Low:
                return lowRule;

            case SanityState.Critical:
                return criticalRule;

            case SanityState.Empty:
                return emptyRule;

            default:
                return normalRule;
        }
    }

    private void RemoveDestroyedEnemies()
    {
        for (int i = spawnedEnemies.Count - 1; i >= 0; i--)
        {
            if (spawnedEnemies[i] == null)
            {
                spawnedEnemies.RemoveAt(i);
            }
        }
    }

    private void CacheChildSpawnPointsIfNeeded()
    {
        if (cachedSpawnPoints.Count > 0)
        {
            return;
        }

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            foreach (Transform point in spawnPoints)
            {
                if (point != null)
                {
                    cachedSpawnPoints.Add(point);
                }
            }

            return;
        }

        for (int i = 0; i < transform.childCount; i++)
        {
            Transform child = transform.GetChild(i);

            if (child != null)
            {
                cachedSpawnPoints.Add(child);
            }
        }
    }

    private void FindReferences()
    {
        if (sanityController == null)
        {
            sanityController =
                FindAnyObjectByType<PlayerSanityController>();
        }

        if (playerTransform == null && sanityController != null)
        {
            playerTransform = sanityController.transform;
        }

        if (playerHealth == null && sanityController != null)
        {
            playerHealth = sanityController.GetComponent<CharacterHealth>();
        }
    }

    private static bool HasValidPrefab(GameObject[] prefabs)
    {
        if (prefabs == null || prefabs.Length == 0)
        {
            return false;
        }

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null)
            {
                return true;
            }
        }

        return false;
    }

    private static GameObject GetRandomPrefab(GameObject[] prefabs)
    {
        if (!HasValidPrefab(prefabs))
        {
            return null;
        }

        List<GameObject> validPrefabs = new List<GameObject>();

        foreach (GameObject prefab in prefabs)
        {
            if (prefab != null)
            {
                validPrefabs.Add(prefab);
            }
        }

        return validPrefabs[UnityEngine.Random.Range(0, validPrefabs.Count)];
    }

    private void OnValidate()
    {
        minimumDistanceFromPlayer = Mathf.Max(0f, minimumDistanceFromPlayer);
        ValidateRule(normalRule);
        ValidateRule(warningRule);
        ValidateRule(lowRule);
        ValidateRule(criticalRule);
        ValidateRule(emptyRule);
    }

    private static void ValidateRule(SpawnRule rule)
    {
        if (rule == null)
        {
            return;
        }

        rule.targetAliveEnemyCount = Mathf.Max(0, rule.targetAliveEnemyCount);
        rule.spawnInterval = Mathf.Max(0.05f, rule.spawnInterval);
        rule.mutantSpawnChance = Mathf.Clamp01(rule.mutantSpawnChance);
    }
}
