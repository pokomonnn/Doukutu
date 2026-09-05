using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Stage番号とWeightから出現Enemyを抽選するテーブルです。
/// レア強敵を低確率で混ぜる用途にも使えます。
/// </summary>
[CreateAssetMenu(
    fileName = "NewEnemySpawnTable2D",
    menuName = "Enemy/Spawn Table 2D"
)]
public class EnemySpawnTable2D : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [SerializeField] private GameObject enemyPrefab;
        [SerializeField, Min(0f)] private float weight = 1f;
        [SerializeField, Min(1)] private int minimumStage = 1;
        [Tooltip("0なら上限なし")]
        [SerializeField, Min(0)] private int maximumStage;
        [SerializeField] private bool rareEnemy;

        public GameObject EnemyPrefab => enemyPrefab;
        public float Weight => Mathf.Max(0f, weight);
        public int MinimumStage => Mathf.Max(1, minimumStage);
        public int MaximumStage => Mathf.Max(0, maximumStage);
        public bool RareEnemy => rareEnemy;

        public bool IsAvailable(int stageNumber)
        {
            int stage = Mathf.Max(1, stageNumber);
            return enemyPrefab != null &&
                   Weight > 0f &&
                   stage >= MinimumStage &&
                   (MaximumStage <= 0 || stage <= MaximumStage);
        }
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

    public GameObject Roll(int stageNumber, System.Random random = null)
    {
        List<Entry> candidates = new List<Entry>();
        float totalWeight = 0f;

        foreach (Entry entry in entries)
        {
            if (entry == null || !entry.IsAvailable(stageNumber))
            {
                continue;
            }

            candidates.Add(entry);
            totalWeight += entry.Weight;
        }

        if (candidates.Count == 0 || totalWeight <= 0f)
        {
            return null;
        }

        double roll01 = random != null
            ? random.NextDouble()
            : UnityEngine.Random.value;

        float roll = (float)roll01 * totalWeight;
        float accumulated = 0f;

        foreach (Entry entry in candidates)
        {
            accumulated += entry.Weight;

            if (roll <= accumulated)
            {
                return entry.EnemyPrefab;
            }
        }

        return candidates[candidates.Count - 1].EnemyPrefab;
    }
}
