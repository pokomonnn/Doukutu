using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemySpawnTable2DからEnemyを定期生成します。
/// Stageごとの敵構成・レア強敵の低確率出現テスト用の基本Spawnerです。
/// </summary>
[DisallowMultipleComponent]
public class EnemySpawnManager2D : MonoBehaviour
{
    [Header("テーブル")]
    [SerializeField] private EnemySpawnTable2D spawnTable;
    [SerializeField, Min(1)] private int stageNumber = 1;

    [Header("Spawn Point")]
    [Tooltip("空欄なら、このObjectの子TransformをSpawn Pointとして使います")]
    [SerializeField] private Transform[] spawnPoints;

    [Header("生成")]
    [SerializeField] private bool spawnOnStart = true;
    [SerializeField, Min(0)] private int maximumAliveEnemies = 6;
    [SerializeField, Min(0.1f)] private float spawnInterval = 5f;
    [SerializeField, Min(0f)] private float minimumDistanceFromPlayer = 6f;

    [Header("デバッグ")]
    [SerializeField] private bool showLogs;

    private readonly List<GameObject> spawnedEnemies = new List<GameObject>();
    private readonly List<Transform> cachedSpawnPoints = new List<Transform>();
    private float nextSpawnTime;

    private void Awake()
    {
        CacheSpawnPoints();
    }

    private void Start()
    {
        nextSpawnTime = spawnOnStart
            ? Time.time
            : Time.time + spawnInterval;
    }

    private void Update()
    {
        RemoveDestroyedEnemies();

        if (Time.time < nextSpawnTime ||
            maximumAliveEnemies <= 0 ||
            spawnedEnemies.Count >= maximumAliveEnemies)
        {
            return;
        }

        TrySpawnEnemy();
        nextSpawnTime = Time.time + spawnInterval;
    }

    [ContextMenu("Spawn Enemy Now")]
    public void SpawnEnemyNow()
    {
        TrySpawnEnemy();
    }

    private void TrySpawnEnemy()
    {
        if (spawnTable == null)
        {
            return;
        }

        CacheSpawnPoints();
        Transform point = GetRandomValidSpawnPoint();

        if (point == null)
        {
            return;
        }

        GameObject prefab = spawnTable.Roll(stageNumber);

        if (prefab == null)
        {
            return;
        }

        GameObject enemy = Instantiate(
            prefab,
            point.position,
            point.rotation
        );

        spawnedEnemies.Add(enemy);

        if (showLogs)
        {
            Debug.Log(
                $"[EnemySpawnManager2D] Stage{stageNumber}: {prefab.name} を生成しました。",
                this
            );
        }
    }

    private Transform GetRandomValidSpawnPoint()
    {
        if (cachedSpawnPoints.Count == 0)
        {
            return null;
        }

        PlayerMove player = FindAnyObjectByType<PlayerMove>();
        Transform playerTransform = player != null ? player.transform : null;
        List<Transform> valid = new List<Transform>();

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

            valid.Add(point);
        }

        if (valid.Count == 0)
        {
            return null;
        }

        return valid[UnityEngine.Random.Range(0, valid.Count)];
    }

    private void CacheSpawnPoints()
    {
        cachedSpawnPoints.Clear();

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

        foreach (Transform child in transform)
        {
            if (child != null)
            {
                cachedSpawnPoints.Add(child);
            }
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

    private void OnValidate()
    {
        stageNumber = Mathf.Max(1, stageNumber);
        maximumAliveEnemies = Mathf.Max(0, maximumAliveEnemies);
        spawnInterval = Mathf.Max(0.1f, spawnInterval);
        minimumDistanceFromPlayer = Mathf.Max(0f, minimumDistanceFromPlayer);
    }
}
