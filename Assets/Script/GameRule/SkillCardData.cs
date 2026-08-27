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
    HealingReceived
}

[Serializable]
public class SkillCardEffect
{
    [SerializeField] private SkillEffectType effectType;

    [Tooltip(
        "通常は 0.20 = +20%、-0.20 = -20% です。" +
        "CarryWeightLimitAddKgだけは 10 = +10kg として扱います。"
    )]
    [SerializeField] private float value;

    public SkillEffectType EffectType => effectType;
    public float Value => value;
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

    public float GetTotalValue(SkillEffectType effectType)
    {
        if (effects == null)
        {
            return 0f;
        }

        float total = 0f;

        foreach (SkillCardEffect effect in effects)
        {
            if (effect != null && effect.EffectType == effectType)
            {
                total += effect.Value;
            }
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
/// </summary>
public static class SkillCardEffectUtility
{
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
}
