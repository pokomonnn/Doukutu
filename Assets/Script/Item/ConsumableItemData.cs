using System;
using UnityEngine;

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
    [SerializeField, Min(0)] private int healAmount = 0;

    [Header("状態異常回復設定")]
    [Tooltip("このアイテムで治療できる状態異常")]
    [SerializeField]
    private StatusConditionType curedConditions =
        StatusConditionType.None;

    [Header("使用設定")]
    [Tooltip("使用にかかる秒数。0なら即時使用")]
    [SerializeField, Min(0f)] private float useDuration = 0f;

    [Tooltip("使用成功時にアイテムを1個消費する")]
    [SerializeField] private bool consumeOnUse = true;

    [Tooltip("使用時に鳴らす音")]
    [SerializeField] private AudioClip useSound;

    public override InventoryItemType ItemType =>
        InventoryItemType.Consumable;

    public int HealAmount => healAmount;
    public StatusConditionType CuredConditions => curedConditions;
    public float UseDuration => useDuration;
    public bool ConsumeOnUse => consumeOnUse;
    public AudioClip UseSound => useSound;

    public bool CanHealHealth => healAmount > 0;

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
        useDuration = Mathf.Max(0f, useDuration);
    }
}