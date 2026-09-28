using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 町の施設1つ分のアップグレード設定です。
/// 例：武器屋 / facilityId=weapon_shop / Lv1→Lv2の必要ミッション・必要素材・解禁商品。
///
/// MerchantShopDataはこのクラスを継承します。
/// </summary>
[CreateAssetMenu(
    fileName = "NewTownFacilityUpgrade",
    menuName = "Town/Facility/Upgrade Data"
)]
public class TownFacilityUpgradeData : ScriptableObject
{
    [Header("施設情報")]
    [Tooltip("セーブデータで施設を識別する重複しないIDです。公開後は変更しないでください。例：weapon_shop")]
    [FormerlySerializedAs("shopId")]
    [SerializeField] private string facilityId = "weapon_shop";

    [FormerlySerializedAs("shopName")]
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

    /// <summary>
    /// StartingLevelより上からcurrentLevelまでに解禁済みの商品を順番に返します。
    /// MerchantStockInventoryが店舗在庫をMerchantShopDataから再構築する時に使用します。
    /// </summary>
    public IEnumerable<TownFacilityShopUnlockItem> GetShopUnlockItemsUpToLevel(
        int currentLevel)
    {
        int safeLevel = Mathf.Clamp(
            currentLevel,
            StartingLevel,
            MaxLevel
        );

        for (int level = StartingLevel + 1;
             level <= safeLevel;
             level++)
        {
            foreach (TownFacilityShopUnlockItem item in
                     GetShopUnlockItemsForLevel(level))
            {
                yield return item;
            }
        }
    }

    protected virtual void OnValidate()
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

    [Header("必要町人ミッション")]
    [Tooltip(
        "このレベルへのアップグレードを解放する町人ミッションです。" +
        "不要ならNoneのままで、従来どおり素材だけでアップグレードできます。"
    )]
    [SerializeField] private MissionDefinition2D requiredMission;

    [Tooltip(
        "ONならRequired Missionを達成しただけでは足りず、NPCから報酬を受け取った後にアップグレード可能になります。" +
        "OFFならミッション達成時点でアップグレード可能です。"
    )]
    [SerializeField] private bool requireMissionRewardClaimed = true;

    [Tooltip(
        "ミッション条件を満たしていない時に表示する専用メッセージです。" +
        "空欄ならManagerが状況に応じた文章を自動生成します。"
    )]
    [SerializeField, TextArea(2, 4)] private string missionLockedMessage;

    [Header("必要素材")]
    [SerializeField]
    private List<TownFacilityUpgradeRequirement> requiredItems =
        new List<TownFacilityUpgradeRequirement>();

    [Header("このレベルで追加解禁する販売商品")]
    [Tooltip(
        "この施設レベルへ到達した時に販売解禁する商品です。\n" +
        "MerchantShopDataを使用する店舗では、Lv1商品はMerchantShopDataのItems、Lv2以降はここで設定します。"
    )]
    [SerializeField]
    private List<TownFacilityShopUnlockItem> unlockedShopItems =
        new List<TownFacilityShopUnlockItem>();

    public int TargetLevel => Mathf.Max(1, targetLevel);
    public MissionDefinition2D RequiredMission => requiredMission;
    public bool RequireMissionRewardClaimed => requireMissionRewardClaimed;
    public string MissionLockedMessage =>
        missionLockedMessage?.Trim() ?? string.Empty;

    public IReadOnlyList<TownFacilityUpgradeRequirement> RequiredItems =>
        requiredItems;

    public IReadOnlyList<TownFacilityShopUnlockItem> UnlockedShopItems =>
        unlockedShopItems;

    public void Validate(int startingLevel)
    {
        targetLevel = Mathf.Max(startingLevel + 1, targetLevel);
        missionLockedMessage = missionLockedMessage?.Trim() ?? string.Empty;

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

    [Tooltip("この商品が解禁された時の商品棚在庫数です。")]
    [SerializeField, Min(1)] private int amount = 1;

    public ItemData ItemData => itemData;
    public int Amount => Mathf.Max(1, amount);

    public void Validate()
    {
        amount = Mathf.Max(1, amount);
    }
}
