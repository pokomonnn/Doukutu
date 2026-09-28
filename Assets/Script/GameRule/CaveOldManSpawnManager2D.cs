using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CaveOldManSpawnPoint2Dから1地点を抽選して、お爺さんPrefabを生成します。
/// 救出NPC Managerとは独立しているため、救出人数には影響しません。
/// </summary>
[DefaultExecutionOrder(-300)]
[DisallowMultipleComponent]
public class CaveOldManSpawnManager2D : MonoBehaviour
{
    [Header("Spawn Point")]
    [SerializeField] private bool autoFindSpawnPointsInChildren = true;
    [SerializeField] private List<CaveOldManSpawnPoint2D> spawnPoints = new List<CaveOldManSpawnPoint2D>();

    [Header("お爺さん")]
    [SerializeField] private GameObject oldManPrefab;
    [Tooltip("この出撃でお爺さん自体が出現する確率。1=100%、0.5=50%。")]
    [SerializeField, Range(0f, 1f)] private float spawnChance = 1f;
    [SerializeField] private Transform spawnedOldManRoot;

    [Header("生成タイミング")]
    [SerializeField] private bool spawnOnAwake = true;

    [Header("デバッグ")]
    [SerializeField] private bool useFixedSeedForDebug;
    [SerializeField] private int fixedSeed = 12345;
    [SerializeField] private bool showDebugLogs = true;

    private GameObject spawnedOldMan;
    public GameObject SpawnedOldMan => spawnedOldMan;

    private void Awake()
    {
        RefreshSpawnPoints();
        if (spawnOnAwake) SpawnOldMan();
    }

    [ContextMenu("Spawn Old Man")]
    public GameObject SpawnOldMan()
    {
        DestroySpawnedOldMan();
        RefreshSpawnPoints();

        if (oldManPrefab == null)
        {
            LogWarning("Old Man Prefabが未設定です。");
            return null;
        }

        List<CaveOldManSpawnPoint2D> valid = GetValidSpawnPoints();
        if (valid.Count == 0)
        {
            LogWarning("有効なCaveOldManSpawnPoint2Dがありません。");
            return null;
        }

        System.Random random = CreateRandom();
        if (random.NextDouble() > spawnChance)
        {
            Log($"今回はお爺さんが出現しませんでした。SpawnChance={spawnChance:0.00}");
            return null;
        }

        CaveOldManSpawnPoint2D point = valid[random.Next(valid.Count)];
        Transform parent = spawnedOldManRoot != null ? spawnedOldManRoot : transform;
        spawnedOldMan = Instantiate(oldManPrefab, point.SpawnPosition, point.SpawnRotation, parent);
        spawnedOldMan.name = $"CaveOldMan_{point.SpawnPointId}";
        Log($"お爺さん生成: Point={point.name}({point.SpawnPointId}) / Prefab={oldManPrefab.name}");
        return spawnedOldMan;
    }

    [ContextMenu("Destroy Spawned Old Man")]
    public void DestroySpawnedOldMan()
    {
        if (spawnedOldMan == null) return;
        if (Application.isPlaying) Destroy(spawnedOldMan); else DestroyImmediate(spawnedOldMan);
        spawnedOldMan = null;
    }

    public void RefreshSpawnPoints()
    {
        if (!autoFindSpawnPointsInChildren) return;
        CaveOldManSpawnPoint2D[] found = GetComponentsInChildren<CaveOldManSpawnPoint2D>(true);
        spawnPoints.Clear();
        foreach (var p in found) if (p != null) spawnPoints.Add(p);
    }

    private List<CaveOldManSpawnPoint2D> GetValidSpawnPoints()
    {
        var result = new List<CaveOldManSpawnPoint2D>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (spawnPoints == null) return result;
        foreach (var p in spawnPoints)
        {
            if (p == null || !p.CanSpawnHere) continue;
            if (string.IsNullOrWhiteSpace(p.SpawnPointId)) { LogWarning($"SpawnPoint『{p.name}』のIDが空です。"); continue; }
            if (!ids.Add(p.SpawnPointId)) { LogWarning($"SpawnPoint IDが重複しています: {p.SpawnPointId}"); continue; }
            result.Add(p);
        }
        return result;
    }

    private System.Random CreateRandom()
    {
        if (useFixedSeedForDebug) return new System.Random(fixedSeed);
        int seed = unchecked(Environment.TickCount ^ GetInstanceID() ^ DateTime.UtcNow.Millisecond);
        return new System.Random(seed);
    }

    private void Log(string m) { if (showDebugLogs) Debug.Log($"[CaveOldManSpawnManager2D] {m}", this); }
    private void LogWarning(string m) { Debug.LogWarning($"[CaveOldManSpawnManager2D] {m}", this); }
    private void OnValidate() { spawnChance = Mathf.Clamp01(spawnChance); }
}
