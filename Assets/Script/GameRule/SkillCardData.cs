using System;
using System.Collections.Generic;
using UnityEngine;

public enum SkillCardRarity
{
    Common,
    Uncommon,
    Rare,
    Epic
}

/// <summary>
/// スキルカードが変更できるプレイヤー能力です。
/// Percentage系は Value=0.20 で+20%、Value=-0.20で-20%として扱います。
/// CarryWeightLimitAddKgだけは割合ではなくkgを直接加算します。
///
/// 重要：既存AssetのEnum値を壊さないため、既存項目の順番は変更せず、
/// 新しい効果は末尾へ追加してください。
/// </summary>
public enum SkillEffectType
{
    WeaponDamage,
    ReloadDuration,
    WeaponSpread,
    JamChance,
    WeaponDurabilityLoss,
    MoveSpeed,
    CarryWeightLimitAddKg,
    FoodDrain,
    WaterDrain,
    SanityDrain,
    TorchDrain,
    DamageTaken,
    HealingReceived,

    // 2026-09 Alpha skill expansion
    SanityRecovery,
    ItemBoxExtraLootChance,
    RescueCarryPenaltyReduction,
    JumpPower
}

/// <summary>
/// 1つのSkillCardEffectが発動する条件です。
/// Conditionsが空なら常時発動します。
/// 複数設定した場合は「すべて満たした時」に発動します。
/// </summary>
public enum SkillConditionType
{
    Always,
    HealthPercentAtOrBelow,
    HealthPercentAtOrAbove,
    SanityPercentAtOrBelow,
    SanityPercentAtOrAbove,
    WeightAtOrBelowKg,
    WeightAtOrAboveKg,
    EquippedWeapon,
    CarryingRescuePerson
}

[Serializable]
public class SkillCardCondition
{
    [SerializeField] private SkillConditionType conditionType = SkillConditionType.Always;

    [Tooltip(
        "HP/SAN条件で使用します。0.30=30%、0.50=50%です。" +
        "この条件を使わない場合は無視されます。"
    )]
    [SerializeField, Range(0f, 1f)] private float percentThreshold = 0.5f;

    [Tooltip(
        "重量条件で使用します。例：20なら20kg以下/以上です。" +
        "この条件を使わない場合は無視されます。"
    )]
    [SerializeField, Min(0f)] private float weightThresholdKg = 20f;

    [Tooltip(
        "Equipped Weapon条件で使用します。" +
        "ここへ指定したWeaponItemDataを装備中だけ条件成立します。"
    )]
    [SerializeField] private WeaponItemData requiredWeapon;

    [Tooltip("ONなら、この条件の成立/不成立を反転します。")]
    [SerializeField] private bool invert;

    public SkillConditionType ConditionType => conditionType;
    public float PercentThreshold => Mathf.Clamp01(percentThreshold);
    public float WeightThresholdKg => Mathf.Max(0f, weightThresholdKg);
    public WeaponItemData RequiredWeapon => requiredWeapon;
    public bool Invert => invert;

    public bool IsMet()
    {
        bool result = SkillCardEffectUtility.EvaluateCondition(this);
        return invert ? !result : result;
    }
}

[Serializable]
public class SkillCardEffect
{
    [SerializeField] private SkillEffectType effectType;

    [Tooltip(
        "通常は 0.20 = +20%、-0.20 = -20% です。" +
        "CarryWeightLimitAddKgだけは 10 = +10kg として扱います。" +
        "ItemBoxExtraLootChanceは 0.15 = 追加抽選15%です。" +
        "RescueCarryPenaltyReductionは 0.40 = 運搬速度ペナルティ40%軽減です。"
    )]
    [SerializeField] private float value;

    [Header("発動条件（空なら常時）")]
    [Tooltip("複数設定した場合、すべての条件を満たした時だけこのEffectが発動します。")]
    [SerializeField]
    private List<SkillCardCondition> conditions = new List<SkillCardCondition>();

    public SkillEffectType EffectType => effectType;
    public float Value => value;
    public IReadOnlyList<SkillCardCondition> Conditions => conditions;

    public bool AreConditionsMet()
    {
        if (conditions == null || conditions.Count == 0)
        {
            return true;
        }

        foreach (SkillCardCondition condition in conditions)
        {
            if (condition == null)
            {
                continue;
            }

            if (!condition.IsMet())
            {
                return false;
            }
        }

        return true;
    }
}

/// <summary>
/// Fallout系の装備式スキルカードです。
/// 通常インベントリへは入らず、拾った瞬間にスキルコレクションへ永久登録されます。
/// </summary>
[CreateAssetMenu(
    fileName = "NewSkillCardData",
    menuName = "Inventory/Items/Skill Card Data"
)]
public class SkillCardData : ItemData
{
    [Header("カード情報")]
    [SerializeField] private SkillCardRarity rarity = SkillCardRarity.Common;

    [Tooltip("カード左側などに表示するメリット文章です。空欄でも構いません。")]
    [SerializeField, TextArea(2, 5)]
    private string benefitText;

    [Tooltip("カード右側などに表示するデメリット文章です。デメリットなしなら空欄でOKです。")]
    [SerializeField, TextArea(2, 5)]
    private string drawbackText;

    [Header("実際に適用する効果")]
    [SerializeField]
    private List<SkillCardEffect> effects = new List<SkillCardEffect>();

    public override InventoryItemType ItemType => InventoryItemType.SkillCard;

    public SkillCardRarity Rarity => rarity;
    public string BenefitText => benefitText ?? string.Empty;
    public string DrawbackText => drawbackText ?? string.Empty;
    public IReadOnlyList<SkillCardEffect> Effects => effects;

    /// <summary>
    /// 現在のPlayer状態で条件成立しているEffectだけを合計します。
    /// 既存のGameSessionManager.GetSkillEffectAdditiveValue()から呼ばれても
    /// 条件付きカードが正しく反映されます。
    /// </summary>
    public float GetTotalValue(SkillEffectType effectType)
    {
        if (effects == null)
        {
            return 0f;
        }

        float total = 0f;

        foreach (SkillCardEffect effect in effects)
        {
            if (effect == null ||
                effect.EffectType != effectType ||
                !effect.AreConditionsMet())
            {
                continue;
            }

            total += effect.Value;
        }

        return total;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        if (effects == null)
        {
            effects = new List<SkillCardEffect>();
        }
    }
}

/// <summary>
/// 現在装備中のカード効果をゲーム側から簡単に取得する共通APIです。
/// GameSessionManagerを正として扱うため、シーンをまたいでも同じビルドが使われます。
///
/// Player側の参照は必要になった時だけ検索し、その後キャッシュします。
/// Scene切替でObjectがDestroyされた場合、Unityのnull判定で自動的に再検索します。
/// </summary>
public static class SkillCardEffectUtility
{
    private static PlayerWeightController cachedWeightController;
    private static PlayerSanityController cachedSanityController;
    private static PlayerSurvivalController cachedSurvivalController;
    private static PlayerCarryController2D cachedCarryController;
    private static EquipmentController cachedEquipmentController;

    public static float GetAdditiveValue(SkillEffectType effectType)
    {
        GameSessionManager session = GameSessionManager.Instance;

        if (session == null)
        {
            return 0f;
        }

        float total = 0f;
        int slotCount = session.UnlockedSkillSlotCount;

        for (int i = 0; i < slotCount; i++)
        {
            SkillCardData card = session.GetEquippedSkillCard(i);
            if (card == null)
            {
                continue;
            }

            total += card.GetTotalValue(effectType);
        }

        return total;
    }

    public static float GetMultiplier(SkillEffectType effectType)
    {
        return Mathf.Max(0f, 1f + GetAdditiveValue(effectType));
    }

    /// <summary>
    /// 0～1の割合として使うEffect用です。
    /// 例：ItemBoxExtraLootChance 0.15、RescueCarryPenaltyReduction 0.40。
    /// </summary>
    public static float GetClamped01Value(SkillEffectType effectType)
    {
        return Mathf.Clamp01(GetAdditiveValue(effectType));
    }

    public static bool EvaluateCondition(SkillCardCondition condition)
    {
        if (condition == null)
        {
            return true;
        }

        switch (condition.ConditionType)
        {
            case SkillConditionType.Always:
                return true;

            case SkillConditionType.HealthPercentAtOrBelow:
            {
                CharacterHealth health = GetPlayerHealth();
                return health != null &&
                       GetHealthPercent(health) <= condition.PercentThreshold;
            }

            case SkillConditionType.HealthPercentAtOrAbove:
            {
                CharacterHealth health = GetPlayerHealth();
                return health != null &&
                       GetHealthPercent(health) >= condition.PercentThreshold;
            }

            case SkillConditionType.SanityPercentAtOrBelow:
            {
                PlayerSanityController sanity = GetSanityController();
                return sanity != null &&
                       sanity.SanityPercent <= condition.PercentThreshold;
            }

            case SkillConditionType.SanityPercentAtOrAbove:
            {
                PlayerSanityController sanity = GetSanityController();
                return sanity != null &&
                       sanity.SanityPercent >= condition.PercentThreshold;
            }

            case SkillConditionType.WeightAtOrBelowKg:
            {
                PlayerWeightController weight = GetWeightController();
                return weight != null &&
                       weight.CurrentWeight <= condition.WeightThresholdKg;
            }

            case SkillConditionType.WeightAtOrAboveKg:
            {
                PlayerWeightController weight = GetWeightController();
                return weight != null &&
                       weight.CurrentWeight >= condition.WeightThresholdKg;
            }

            case SkillConditionType.EquippedWeapon:
                return IsRequiredWeaponEquipped(condition.RequiredWeapon);

            case SkillConditionType.CarryingRescuePerson:
                return IsCarryingRescuePerson();

            default:
                return true;
        }
    }

    public static bool IsCarryingRescuePerson()
    {
        PlayerCarryController2D carry = GetCarryController();

        if (carry == null || !carry.IsCarrying || carry.CarriedTarget == null)
        {
            return false;
        }

        CarryableObject2D target = carry.CarriedTarget;

        RescuePersonTarget2D rescue =
            target.GetComponent<RescuePersonTarget2D>();

        if (rescue == null)
        {
            rescue = target.GetComponentInChildren<RescuePersonTarget2D>(true);
        }

        if (rescue == null)
        {
            rescue = target.GetComponentInParent<RescuePersonTarget2D>();
        }

        return rescue != null;
    }

    public static WeaponItemData GetEquippedPrimaryWeaponData()
    {
        EquipmentController equipment = GetEquipmentController();

        if (equipment == null || equipment.PrimaryWeaponItem == null)
        {
            return null;
        }

        return equipment.PrimaryWeaponItem.ItemData as WeaponItemData;
    }

    private static bool IsRequiredWeaponEquipped(WeaponItemData requiredWeapon)
    {
        if (requiredWeapon == null)
        {
            return false;
        }

        WeaponItemData equipped = GetEquippedPrimaryWeaponData();

        if (equipped == null)
        {
            return false;
        }

        if (equipped == requiredWeapon)
        {
            return true;
        }

        string requiredId = requiredWeapon.ItemId?.Trim() ?? string.Empty;
        string equippedId = equipped.ItemId?.Trim() ?? string.Empty;

        return !string.IsNullOrWhiteSpace(requiredId) &&
               string.Equals(
                   requiredId,
                   equippedId,
                   StringComparison.OrdinalIgnoreCase
               );
    }

    private static float GetHealthPercent(CharacterHealth health)
    {
        if (health == null || health.MaxHealth <= 0)
        {
            return 0f;
        }

        return Mathf.Clamp01(
            (float)health.CurrentHealth / health.MaxHealth
        );
    }

    private static CharacterHealth GetPlayerHealth()
    {
        PlayerWeightController weight = GetWeightController();
        if (weight != null)
        {
            CharacterHealth health = weight.GetComponent<CharacterHealth>();
            if (health != null)
            {
                return health;
            }
        }

        PlayerSanityController sanity = GetSanityController();
        if (sanity != null)
        {
            CharacterHealth health = sanity.GetComponent<CharacterHealth>();
            if (health != null)
            {
                return health;
            }
        }

        PlayerSurvivalController survival = GetSurvivalController();
        return survival != null
            ? survival.GetComponent<CharacterHealth>()
            : null;
    }

    private static PlayerWeightController GetWeightController()
    {
        if (cachedWeightController == null)
        {
            cachedWeightController =
                UnityEngine.Object.FindAnyObjectByType<PlayerWeightController>();
        }

        return cachedWeightController;
    }

    private static PlayerSanityController GetSanityController()
    {
        if (cachedSanityController == null)
        {
            cachedSanityController =
                UnityEngine.Object.FindAnyObjectByType<PlayerSanityController>();
        }

        return cachedSanityController;
    }

    private static PlayerSurvivalController GetSurvivalController()
    {
        if (cachedSurvivalController == null)
        {
            cachedSurvivalController =
                UnityEngine.Object.FindAnyObjectByType<PlayerSurvivalController>();
        }

        return cachedSurvivalController;
    }

    private static PlayerCarryController2D GetCarryController()
    {
        if (cachedCarryController == null)
        {
            cachedCarryController =
                UnityEngine.Object.FindAnyObjectByType<PlayerCarryController2D>();
        }

        return cachedCarryController;
    }

    private static EquipmentController GetEquipmentController()
    {
        if (cachedEquipmentController == null)
        {
            cachedEquipmentController =
                UnityEngine.Object.FindAnyObjectByType<EquipmentController>();
        }

        return cachedEquipmentController;
    }

    /// <summary>
    /// デバッグや特殊なScene構成で手動再検索したい時に使えます。
    /// 通常は呼ぶ必要はありません。
    /// </summary>
    public static void ClearRuntimeReferenceCache()
    {
        cachedWeightController = null;
        cachedSanityController = null;
        cachedSurvivalController = null;
        cachedCarryController = null;
        cachedEquipmentController = null;
    }
}
