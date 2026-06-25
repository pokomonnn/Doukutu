using UnityEngine;

[CreateAssetMenu(
    fileName = "NewAmmoItemData",
    menuName = "Inventory/Items/Ammo Item Data"
)]
public class AmmoItemData : ItemData
{
    [Header("弾薬設定")]
    [Tooltip("表示・管理用の名前です。銃との互換性は、このAmmo Item Dataアセット自体で判定します。")]
    [SerializeField] private string ammoTypeName = "9mm";

    public override InventoryItemType ItemType =>
        InventoryItemType.Ammo;

    public string AmmoTypeName => ammoTypeName;
}
