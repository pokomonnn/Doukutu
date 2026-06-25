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

    [Header("弾薬設定")]
    [Tooltip("この銃がリロード時に消費するAmmo Item Data")]
    [SerializeField] private AmmoItemData compatibleAmmo;

    public override InventoryItemType ItemType =>
        InventoryItemType.Weapon;

    public EquipmentSlotType EquipmentSlot =>
        EquipmentSlotType.PrimaryWeapon;

    public GameObject WeaponPrefab => weaponPrefab;
    public AmmoItemData CompatibleAmmo => compatibleAmmo;
}
