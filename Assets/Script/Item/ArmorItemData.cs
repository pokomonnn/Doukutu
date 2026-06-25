using UnityEngine;

[CreateAssetMenu(
    fileName = "NewArmorItemData",
    menuName = "Inventory/Items/Armor Item Data"
)]
public class ArmorItemData : ItemData
{
    [Header("装備設定")]
    [SerializeField]
    private EquipmentSlotType equipmentSlot =
        EquipmentSlotType.Helmet;

    [Header("防御性能")]
    [Tooltip("受けるダメージを何％減らすか。例：20なら20％軽減")]
    [SerializeField, Range(0f, 100f)]
    private float damageReductionPercent = 0f;

    [Header("見た目")]
    [Tooltip("装備時にプレイヤーへ表示するPrefab。今は空でもOK")]
    [SerializeField] private GameObject equippedVisualPrefab;

    public override InventoryItemType ItemType =>
        InventoryItemType.Armor;

    public EquipmentSlotType EquipmentSlot => equipmentSlot;

    public float DamageReductionPercent =>
        damageReductionPercent;

    public GameObject EquippedVisualPrefab =>
        equippedVisualPrefab;

    protected override void OnValidate()
    {
        base.OnValidate();

        // 防具データでWeapon枠を選べないようにする
        if (equipmentSlot != EquipmentSlotType.Helmet)
        {
            equipmentSlot = EquipmentSlotType.Helmet;
        }

        damageReductionPercent = Mathf.Clamp(
            damageReductionPercent,
            0f,
            100f
        );
    }
}