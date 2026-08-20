using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 町の施設1つ分のアップグレード設定です。
/// 例：武器屋 / facilityId=weapon_shop / Lv1→Lv2の必要素材と解禁商品。
/// </summary>
[CreateAssetMenu(
    fileName = "NewTownFacilityUpgrade",
    menuName = "Town/Facility/Upgrade Data"
)]
public class TownFacilityUpgradeData : ScriptableObject
{
    [Header("施設情報")]
    [Tooltip("セーブデータで施設を識別する重複しないIDです。公開後は変更しないでください。例：weapon_shop")]
    [SerializeField] private string facilityId = "weapon_shop";

    [SerializeField] private string facilityName = "武器屋";

    [Tooltip("ニューゲーム開始時の施設レベルです。通常は1です。")]
    [SerializeField, Min(1)] private int startingLevel = 1;

    [Header("レベルアップ設定")]
    [Tooltip("各要素は『このレベルへ上げる時』の条件です。Lv2、Lv3…の順に設定してください。")]
    [SerializeField]
    private List<TownFacilityUpgradeLevel> upgradeLevels =
        new List<TownFacilityUpgradeLevel>();

    public string FacilityId => string.IsNullOrWhiteSpace(facilityId)
        ? name
        : facilityId.Trim();

    public string FacilityName => string.IsNullOrWhiteSpace(facilityName)
        ? name
        : facilityName.Trim();

    public int StartingLevel => Mathf.Max(1, startingLevel);

    public int MaxLevel
    {
        get
        {
            int maxLevel = StartingLevel;

            if (upgradeLevels == null)
            {
                return maxLevel;
            }

            foreach (TownFacilityUpgradeLevel level in upgradeLevels)
            {
                if (level == null)
                {
                    continue;
                }

                maxLevel = Mathf.Max(maxLevel, level.TargetLevel);
            }

            return maxLevel;
        }
    }

    public IReadOnlyList<TownFacilityUpgradeLevel> UpgradeLevels =>
        upgradeLevels;

    public TownFacilityUpgradeLevel GetUpgradeLevel(int targetLevel)
    {
        if (upgradeLevels == null || targetLevel <= StartingLevel)
        {
            return null;
        }

        foreach (TownFacilityUpgradeLevel level in upgradeLevels)
        {
            if (level != null && level.TargetLevel == targetLevel)
            {
                return level;
            }
        }

        return null;
    }

    public IEnumerable<TownFacilityShopUnlockItem> GetShopUnlockItemsForLevel(
        int level)
    {
        TownFacilityUpgradeLevel upgrade = GetUpgradeLevel(level);

        if (upgrade == null || upgrade.UnlockedShopItems == null)
        {
            yield break;
        }

        foreach (TownFacilityShopUnlockItem item in upgrade.UnlockedShopItems)
        {
            if (item != null && item.ItemData != null && item.Amount > 0)
            {
                yield return item;
            }
        }
    }

    private void OnValidate()
    {
        facilityId = facilityId?.Trim() ?? string.Empty;
        facilityName = facilityName?.Trim() ?? string.Empty;
        startingLevel = Mathf.Max(1, startingLevel);

        if (upgradeLevels == null)
        {
            upgradeLevels = new List<TownFacilityUpgradeLevel>();
        }

        HashSet<int> usedLevels = new HashSet<int>();

        foreach (TownFacilityUpgradeLevel level in upgradeLevels)
        {
            if (level == null)
            {
                continue;
            }

            level.Validate(startingLevel);

            if (!usedLevels.Add(level.TargetLevel))
            {
                Debug.LogWarning(
                    $"[TownFacilityUpgradeData] {name}: " +
                    $"Target Level {level.TargetLevel} が重複しています。",
                    this
                );
            }
        }
    }
}

[Serializable]
public class TownFacilityUpgradeLevel
{
    [Header("このレベルへアップグレード")]
    [SerializeField, Min(2)] private int targetLevel = 2;

    [Header("必要素材")]
    [SerializeField]
    private List<TownFacilityUpgradeRequirement> requiredItems =
        new List<TownFacilityUpgradeRequirement>();

    [Header("このレベルで店に追加する商品")]
    [Tooltip("ItemBoxInventoryのStarting Itemsとは別に、このレベルへ到達したら商品棚へ追加されます。")]
    [SerializeField]
    private List<TownFacilityShopUnlockItem> unlockedShopItems =
        new List<TownFacilityShopUnlockItem>();

    public int TargetLevel => Mathf.Max(1, targetLevel);
    public IReadOnlyList<TownFacilityUpgradeRequirement> RequiredItems =>
        requiredItems;
    public IReadOnlyList<TownFacilityShopUnlockItem> UnlockedShopItems =>
        unlockedShopItems;

    public void Validate(int startingLevel)
    {
        targetLevel = Mathf.Max(startingLevel + 1, targetLevel);

        if (requiredItems == null)
        {
            requiredItems = new List<TownFacilityUpgradeRequirement>();
        }

        if (unlockedShopItems == null)
        {
            unlockedShopItems = new List<TownFacilityShopUnlockItem>();
        }

        foreach (TownFacilityUpgradeRequirement requirement in requiredItems)
        {
            requirement?.Validate();
        }

        foreach (TownFacilityShopUnlockItem item in unlockedShopItems)
        {
            item?.Validate();
        }
    }
}

[Serializable]
public class TownFacilityUpgradeRequirement
{
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int amount = 1;

    public ItemData ItemData => itemData;
    public int Amount => Mathf.Max(1, amount);

    public void Validate()
    {
        amount = Mathf.Max(1, amount);
    }
}

[Serializable]
public class TownFacilityShopUnlockItem
{
    [SerializeField] private ItemData itemData;

    [Tooltip("商品棚へ追加する初期在庫数です。")]
    [SerializeField, Min(1)] private int amount = 1;

    public ItemData ItemData => itemData;
    public int Amount => Mathf.Max(1, amount);

    public void Validate()
    {
        amount = Mathf.Max(1, amount);
    }
}
