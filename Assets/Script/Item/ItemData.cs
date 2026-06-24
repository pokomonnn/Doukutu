using UnityEngine;

public enum InventoryItemType
{
    Misc,       // その他
    Consumable, // 回復アイテム
    Ammo,       // 弾薬
    Weapon,     // 銃
    Armor,      // 防具
    Equipment,  // ライト・かぎ爪など
    Treasure,   // 売却用お宝
    Quest       // クエスト用
}

[CreateAssetMenu(
    fileName = "NewMiscItemData",
    menuName = "Inventory/Items/Misc Item Data"
)]
public class ItemData : ScriptableObject
{
    [Header("基本情報")]
    [SerializeField] private string itemId = "item_id";
    [SerializeField] private string displayName = "新しいアイテム";
    [SerializeField, TextArea(2, 4)] private string description;
    [SerializeField] private Sprite icon;

    [Header("インベントリ内のサイズ")]
    [SerializeField, Min(1)] private int width = 1;
    [SerializeField, Min(1)] private int height = 1;
    [SerializeField] private bool canRotate = true;

    [Header("スタック設定")]
    [SerializeField, Min(1)] private int maxStack = 1;

    [Header("取引設定")]
    [Tooltip("プレイヤーが店から購入する時の価格")]
    [SerializeField, Min(0)] private int purchasePrice = 0;

    [Tooltip("プレイヤーが店へ売却した時にもらえる価格")]
    [SerializeField, Min(0)] private int sellPrice = 0;

    [Tooltip("オフの場合、このアイテムは店に売却できません")]
    [SerializeField] private bool canSellToShop = true;

    [Header("その他")]
    [SerializeField, Min(0f)] private float weight = 0f;

    [Header("捨てる設定")]
    [Tooltip("オフの場合、このアイテムはインベントリから捨てられません")]
    [SerializeField] private bool canDiscard = true;

    public bool CanDiscard => canDiscard;

    public string ItemId => itemId;
    public string DisplayName => displayName;
    public string Description => description;
    public Sprite Icon => icon;

    public int Width => width;
    public int Height => height;
    public bool CanRotate => canRotate;
    public int MaxStack => maxStack;

    public int PurchasePrice => purchasePrice;
    public int SellPrice => sellPrice;
    public bool CanSellToShop => canSellToShop;

    public float Weight => weight;

    public bool CanStack => maxStack > 1;

    // 子クラス側で Weapon / Ammo などへ上書きする
    public virtual InventoryItemType ItemType =>
        InventoryItemType.Misc;

    // 回転状態を含めた、現在必要なマス数を返す
    public Vector2Int GetSize(bool isRotated)
    {
        if (isRotated && canRotate)
        {
            return new Vector2Int(height, width);
        }

        return new Vector2Int(width, height);
    }

    protected virtual void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        maxStack = Mathf.Max(1, maxStack);

        purchasePrice = Mathf.Max(0, purchasePrice);
        sellPrice = Mathf.Max(0, sellPrice);

        weight = Mathf.Max(0f, weight);
    }
}