using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ItemBoxへ入る候補Itemと、それぞれの出現確率・個数を設定するLootTableです。
/// 各Entryは基本的に独立抽選されます。
/// </summary>
[CreateAssetMenu(
    fileName = "NewItemBoxLootTable",
    menuName = "Inventory/Item Box/Loot Table"
)]
public class ItemBoxLootTable : ScriptableObject
{
    [Serializable]
    public class LootEntry
    {
        [SerializeField] private ItemData itemData;

        [Tooltip("このItemが箱に入る確率です。0=出ない、100=必ず出ます。")]
        [SerializeField, Range(0f, 100f)] private float chancePercent = 50f;

        [SerializeField, Min(1)] private int minAmount = 1;
        [SerializeField, Min(1)] private int maxAmount = 1;

        public ItemData ItemData => itemData;
        public float ChancePercent => Mathf.Clamp(chancePercent, 0f, 100f);
        public int MinAmount => Mathf.Max(1, minAmount);
        public int MaxAmount => Mathf.Max(MinAmount, maxAmount);

        public int RollAmount(System.Random random)
        {
            int min = MinAmount;
            int max = MaxAmount;

            if (random == null || min >= max)
            {
                return min;
            }

            // System.Random.Next の上限は含まれないので +1。
            return random.Next(min, max + 1);
        }

        public bool RollChance(System.Random random)
        {
            if (itemData == null || ChancePercent <= 0f)
            {
                return false;
            }

            if (ChancePercent >= 100f)
            {
                return true;
            }

            double value = random != null
                ? random.NextDouble() * 100.0
                : UnityEngine.Random.value * 100.0;

            return value < ChancePercent;
        }

        public void Validate()
        {
            chancePercent = Mathf.Clamp(chancePercent, 0f, 100f);
            minAmount = Mathf.Max(1, minAmount);
            maxAmount = Mathf.Max(minAmount, maxAmount);
        }
    }

    public readonly struct LootRollResult
    {
        public LootRollResult(ItemData itemData, int amount)
        {
            ItemData = itemData;
            Amount = Mathf.Max(1, amount);
        }

        public ItemData ItemData { get; }
        public int Amount { get; }
    }

    [Header("抽選候補")]
    [SerializeField]
    private List<LootEntry> entries = new List<LootEntry>();

    [Header("抽選数の補助")]
    [Tooltip("通常抽選の当選数がこれ未満だった場合、外れた候補から追加抽選して最低数を保証します。0なら保証なしです。")]
    [SerializeField, Min(0)] private int minimumSuccessfulEntries;

    [Tooltip("0なら上限なし。1以上なら、当選したItem種類数をこの数までに制限します。")]
    [SerializeField, Min(0)] private int maximumSuccessfulEntries;

    public IReadOnlyList<LootEntry> Entries => entries;

    public List<LootRollResult> Roll(System.Random random)
    {
        List<LootEntry> selected = new List<LootEntry>();
        List<LootEntry> missed = new List<LootEntry>();

        if (entries == null || entries.Count == 0)
        {
            return new List<LootRollResult>();
        }

        foreach (LootEntry entry in entries)
        {
            if (entry == null || entry.ItemData == null)
            {
                continue;
            }

            if (entry.RollChance(random))
            {
                selected.Add(entry);
            }
            else
            {
                missed.Add(entry);
            }
        }

        int validEntryCount = selected.Count + missed.Count;
        int minimum = Mathf.Clamp(
            minimumSuccessfulEntries,
            0,
            validEntryCount
        );

        // 最低当選数を保証したい場合のみ、外れた候補からランダムで補います。
        while (selected.Count < minimum && missed.Count > 0)
        {
            int index = random != null
                ? random.Next(0, missed.Count)
                : UnityEngine.Random.Range(0, missed.Count);

            selected.Add(missed[index]);
            missed.RemoveAt(index);
        }

        int maximum = maximumSuccessfulEntries;
        if (maximum > 0 && selected.Count > maximum)
        {
            Shuffle(selected, random);
            selected.RemoveRange(maximum, selected.Count - maximum);
        }

        List<LootRollResult> results =
            new List<LootRollResult>(selected.Count);

        foreach (LootEntry entry in selected)
        {
            results.Add(
                new LootRollResult(
                    entry.ItemData,
                    entry.RollAmount(random)
                )
            );
        }

        return results;
    }


    /// <summary>
    /// 「漁り屋」などの追加抽選用です。
    /// 通常Rollとは別枠で、ChancePercentを重みとして候補を1種類だけ選びます。
    /// そのため、レアItem（ChancePercentが低いもの）は追加抽選でも選ばれにくくなります。
    /// </summary>
    public bool TryRollBonusItem(
        System.Random random,
        out LootRollResult result)
    {
        result = default;

        if (entries == null || entries.Count == 0)
        {
            return false;
        }

        float totalWeight = 0f;

        foreach (LootEntry entry in entries)
        {
            if (entry == null ||
                entry.ItemData == null ||
                entry.ChancePercent <= 0f)
            {
                continue;
            }

            totalWeight += entry.ChancePercent;
        }

        if (totalWeight <= 0f)
        {
            return false;
        }

        double roll01 = random != null
            ? random.NextDouble()
            : UnityEngine.Random.value;

        float roll = (float)(roll01 * totalWeight);
        float accumulated = 0f;
        LootEntry fallback = null;

        foreach (LootEntry entry in entries)
        {
            if (entry == null ||
                entry.ItemData == null ||
                entry.ChancePercent <= 0f)
            {
                continue;
            }

            fallback = entry;
            accumulated += entry.ChancePercent;

            if (roll <= accumulated)
            {
                result = new LootRollResult(
                    entry.ItemData,
                    entry.RollAmount(random)
                );
                return true;
            }
        }

        if (fallback != null)
        {
            result = new LootRollResult(
                fallback.ItemData,
                fallback.RollAmount(random)
            );
            return true;
        }

        return false;
    }

    private static void Shuffle<T>(
        IList<T> list,
        System.Random random)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = random != null
                ? random.Next(0, i + 1)
                : UnityEngine.Random.Range(0, i + 1);

            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    private void OnValidate()
    {
        minimumSuccessfulEntries = Mathf.Max(0, minimumSuccessfulEntries);
        maximumSuccessfulEntries = Mathf.Max(0, maximumSuccessfulEntries);

        if (maximumSuccessfulEntries > 0)
        {
            minimumSuccessfulEntries = Mathf.Min(
                minimumSuccessfulEntries,
                maximumSuccessfulEntries
            );
        }

        if (entries == null)
        {
            entries = new List<LootEntry>();
            return;
        }

        foreach (LootEntry entry in entries)
        {
            entry?.Validate();
        }
    }
}
