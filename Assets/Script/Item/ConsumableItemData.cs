using System;
using UnityEngine;
using UnityEngine.Serialization;

[Flags]
public enum StatusConditionType
{
    None = 0,

    Bleeding = 1 << 0, // 出血
    Fracture = 1 << 1, // 骨折
    Poison = 1 << 2,   // 毒（将来用）
    Burn = 1 << 3      // 火傷（将来用）
}

[CreateAssetMenu(
    fileName = "NewConsumableItemData",
    menuName = "Inventory/Items/Consumable Item Data"
)]
public class ConsumableItemData : ItemData
{
    [Header("HP回復設定")]
    [Tooltip("使用時に回復するHP。回復しない場合は0")]
    [SerializeField, Min(0)]
    private int healAmount = 0;

    [Header("食料・水分回復設定")]
    [Tooltip("使用時に回復する食料値。回復しない場合は0")]
    [SerializeField, Min(0f)]
    private float foodRestoreAmount = 0f;

    [Tooltip("使用時に回復する水分値。回復しない場合は0")]
    [SerializeField, Min(0f)]
    private float waterRestoreAmount = 0f;

    [Header("状態異常回復設定")]
    [Tooltip("このアイテムで治療できる状態異常")]
    [SerializeField]
    private StatusConditionType curedConditions =
        StatusConditionType.None;

    [Header("使用中の移動速度低下")]
    [Tooltip("回復アイテムを使った後、移動速度が低下する秒数。0なら速度低下なし")]
    [FormerlySerializedAs("useDuration")]
    [SerializeField, Min(0f)]
    private float slowdownDuration = 3f;

    [Tooltip("移動速度低下中の速度倍率。0.5なら通常速度の半分")]
    [SerializeField, Range(0.05f, 1f)]
    private float useMoveSpeedMultiplier = 0.5f;

    [Tooltip("使用成功時にアイテムを1個消費する")]
    [SerializeField]
    private bool consumeOnUse = true;

    [Tooltip("使用時に鳴らす音")]
    [SerializeField]
    private AudioClip useSound;

    [Header("使用アニメーション")]
    [Tooltip(
    "Player Animatorに作成したTrigger名。" +
    "例：UseBandage、DrinkWater、EatFood。" +
    "空欄ならアニメーションは再生しません。"
)]
    [SerializeField]
    private string useAnimationTrigger = "UseItem";

    public override InventoryItemType ItemType =>
        InventoryItemType.Consumable;

    public int HealAmount => healAmount;

    public float FoodRestoreAmount => foodRestoreAmount;

    public float WaterRestoreAmount => waterRestoreAmount;

    public StatusConditionType CuredConditions => curedConditions;

    public float SlowdownDuration => slowdownDuration;

    public float UseMoveSpeedMultiplier =>
        useMoveSpeedMultiplier;

    public bool ConsumeOnUse => consumeOnUse;

    public AudioClip UseSound => useSound;

    public string UseAnimationTrigger =>
    useAnimationTrigger;

    public bool CanHealHealth => healAmount > 0;

    public bool CanRestoreFood => foodRestoreAmount > 0f;

    public bool CanRestoreWater => waterRestoreAmount > 0f;

    public bool CanCure(StatusConditionType condition)
    {
        if (condition == StatusConditionType.None)
        {
            return false;
        }

        return (curedConditions & condition) == condition;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        healAmount = Mathf.Max(0, healAmount);

        foodRestoreAmount = Mathf.Max(
            0f,
            foodRestoreAmount
        );

        waterRestoreAmount = Mathf.Max(
            0f,
            waterRestoreAmount
        );

        slowdownDuration = Mathf.Max(0f, slowdownDuration);

        useMoveSpeedMultiplier = Mathf.Clamp(
            useMoveSpeedMultiplier,
            0.05f,
            1f
        );
        useAnimationTrigger =
    useAnimationTrigger?.Trim() ?? string.Empty;
    }
}
