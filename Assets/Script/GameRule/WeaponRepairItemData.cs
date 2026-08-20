using UnityEngine;

[CreateAssetMenu(
    fileName = "NewWeaponRepairItemData",
    menuName = "Inventory/Items/Weapon Repair Item Data"
)]
public class WeaponRepairItemData : ItemData
{
    [Header("武器修理設定")]
    [Tooltip("使用時に回復する武器耐久度。MaxDurability=100なら25で25ポイント回復します。")]
    [SerializeField, Min(0.01f)]
    private float repairAmount = 25f;

    [Tooltip("修理成功時にこのアイテムを1個消費します。")]
    [SerializeField]
    private bool consumeOnUse = true;

    [Tooltip("修理キット使用成功時に鳴らす音。")]
    [SerializeField]
    private AudioClip useSound;

    public override InventoryItemType ItemType =>
        InventoryItemType.Consumable;

    public float RepairAmount => Mathf.Max(0.01f, repairAmount);
    public bool ConsumeOnUse => consumeOnUse;
    public AudioClip UseSound => useSound;

    protected override void OnValidate()
    {
        base.OnValidate();
        repairAmount = Mathf.Max(0.01f, repairAmount);
    }
}
