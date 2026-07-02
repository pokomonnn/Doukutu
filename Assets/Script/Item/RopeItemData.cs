using UnityEngine;

/// <summary>
/// 崖などへ設置するロープ用のアイテムデータです。
/// 現段階では通常のMiscアイテムとしてインベントリに入り、
/// 次の工程でRopePlacementZoneから消費して使います。
/// </summary>
[CreateAssetMenu(
    fileName = "NewRopeItemData",
    menuName = "Inventory/Items/Rope Item Data"
)]
public class RopeItemData : ItemData
{
    [Header("ロープ設置設定")]
    [Tooltip("ロープを1回設置する時に消費する個数です。通常は1のまま使います")]
    [SerializeField, Min(1)]
    private int consumeAmountPerPlacement = 1;

    public override InventoryItemType ItemType =>
        InventoryItemType.Misc;

    public int ConsumeAmountPerPlacement =>
        Mathf.Max(1, consumeAmountPerPlacement);
}
