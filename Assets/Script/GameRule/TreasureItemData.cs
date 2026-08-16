using UnityEngine;

/// <summary>
/// リザルト画面で「お宝Item」として集計されるItemDataです。
/// </summary>
[CreateAssetMenu(
    fileName = "NewTreasureItemData",
    menuName = "Inventory/Items/Treasure Item Data"
)]
public class TreasureItemData : ItemData
{
    public override InventoryItemType ItemType =>
        InventoryItemType.Treasure;
}
