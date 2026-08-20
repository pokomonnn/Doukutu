using UnityEngine;

/// <summary>
/// 町の施設アップグレードに使用する専用素材です。
/// 古代の剣・古代の盾・古代の矛などに使用します。
/// </summary>
[CreateAssetMenu(
    fileName = "NewTownUpgradeMaterial",
    menuName = "Inventory/Items/Town Upgrade Material Data"
)]
public class TownUpgradeMaterialItemData : ItemData
{
    public override InventoryItemType ItemType =>
        InventoryItemType.TownUpgradeMaterial;
}
