using UnityEngine;

/// <summary>Inventoryで使用する酒。基底クラスConsumableItemDataのアイテム設定をそのまま利用する。</summary>
[CreateAssetMenu(fileName = "NewAlcoholItemData", menuName = "Inventory/Items/Alcohol Item Data")]
public class AlcoholItemData : ConsumableItemData
{
    [Header("アルコール蓄積")]
    [Tooltip("1本飲むたびに加算。標準50、許容量150なので素早く3本で飲み過ぎ。")]
    [SerializeField, Min(1f)] private float alcoholPoints = 50f;

    [Header("バフ：酔いが残っている間")]
    [Tooltip("所持重量の各段階の上限を増加（kg）。標準+5kg")]
    [SerializeField, Min(0f)] private float weightLimitBonusKg = 5f;
    [Tooltip("救出NPC運搬中の移動速度倍率。標準1.15")]
    [SerializeField, Range(1f, 2f)] private float rescueCarrySpeedMultiplier = 1.15f;

    [Header("デバフ：酔いが残っている間")]
    [Tooltip("食料の時間減少速度の倍率。標準1.25 = 25%増し")]
    [SerializeField, Range(1f, 4f)] private float foodDrainMultiplier = 1.25f;

    // v1.0で作ったSOとのデータ互換性用。新仕様では効果を発動しない。
    [SerializeField, HideInInspector] private float drunkDurationSeconds = 45f;
    [SerializeField, HideInInspector] private float drunkMoveSpeedMultiplier = 0.8f;

    public float AlcoholPoints => alcoholPoints;
    public float WeightLimitBonusKg => weightLimitBonusKg;
    public float RescueCarrySpeedMultiplier => rescueCarrySpeedMultiplier;
    public float FoodDrainMultiplier => foodDrainMultiplier;

    protected override void OnValidate()
    {
        base.OnValidate();
        alcoholPoints = Mathf.Max(1f, alcoholPoints);
        weightLimitBonusKg = Mathf.Max(0f, weightLimitBonusKg);
        rescueCarrySpeedMultiplier = Mathf.Clamp(rescueCarrySpeedMultiplier, 1f, 2f);
        foodDrainMultiplier = Mathf.Clamp(foodDrainMultiplier, 1f, 4f);
    }
}
