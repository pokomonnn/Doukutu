using UnityEngine;

[CreateAssetMenu(
    fileName = "NewWeaponItemData",
    menuName = "Inventory/Items/Weapon Item Data"
)]
public class WeaponItemData : ItemData
{
    [Header("装備設定")]
    [Tooltip("この武器を装備した時に使う武器Prefab")]
    [SerializeField] private GameObject weaponPrefab;

    public override InventoryItemType ItemType =>
        InventoryItemType.Weapon;

    public EquipmentSlotType EquipmentSlot =>
        EquipmentSlotType.PrimaryWeapon;

    public GameObject WeaponPrefab => weaponPrefab;
}