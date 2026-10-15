using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

public enum InventoryItemType
{
    Misc,       // その他
    Consumable, // 回復アイテム
    Ammo,       // 弾薬
    Weapon,     // 銃
    Armor,      // 防具
    Equipment,  // ライト・かぎ爪など
    Treasure,   // 売却用お宝
    Quest,      // クエスト用
    TownUpgradeMaterial, // 町の施設アップグレード素材
    SkillCard   // スキルカード
}

[CreateAssetMenu(
    fileName = "NewMiscItemData",
    menuName = "Inventory/Items/Misc Item Data"
)]
public class ItemData : ScriptableObject
{
    [Header("基本情報")]
    [SerializeField] private string itemId = "item_id";

    [Tooltip(
        "翻訳が未設定・読み込み中の時に使う名前です。"
    )]
    [SerializeField]
    private string displayName = "新しいアイテム";

    [Tooltip(
        "翻訳が未設定・読み込み中の時に使う説明です。"
    )]
    [SerializeField, TextArea(2, 4)]
    private string description;

    [SerializeField] private Sprite icon;

    [Header("名前・説明の翻訳")]
    [Tooltip(
        "ItemText などのString Tableから、" +
        "アイテム名のEntryを設定します。"
    )]
    [SerializeField]
    private LocalizedString localizedDisplayName =
        new LocalizedString();

    [Tooltip(
        "ItemText などのString Tableから、" +
        "アイテム説明のEntryを設定します。"
    )]
    [SerializeField]
    private LocalizedString localizedDescription =
        new LocalizedString();

    [Header("インベントリ内のサイズ")]
    [SerializeField, Min(1)] private int width = 1;
    [SerializeField, Min(1)] private int height = 1;
    [SerializeField] private bool canRotate = true;

    // 四角形アイテムとの互換性のため、初期状態では無効。
    [SerializeField, HideInInspector] private bool useCustomShape = false;
    [SerializeField, HideInInspector] private List<Vector2Int> customShapeCells =
        new List<Vector2Int>();

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

    private string currentDisplayName;
    private string currentDescription;

    private bool isDisplayNameSubscribed;
    private bool isDescriptionSubscribed;

    // 言語切替時、表示中のUIを更新するためのイベント
    public static event Action<ItemData> OnLocalizedTextChanged;

    public bool CanDiscard => canDiscard;

    public string ItemId => itemId;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(currentDisplayName)
            ? GetFallbackDisplayName()
            : currentDisplayName;

    public string Description =>
        currentDescription ?? description ?? string.Empty;

    public Sprite Icon => icon;

    public LocalizedString LocalizedDisplayName =>
        localizedDisplayName;

    public LocalizedString LocalizedDescription =>
        localizedDescription;

    public int Width => width;
    public int Height => height;
    public bool CanRotate => canRotate;

    // Inspectorでカスタム形状を有効化したアイテムだけ、不規則なマスを使う。
    public bool HasCustomShape =>
        useCustomShape && customShapeCells != null && customShapeCells.Count > 0;

    /// <summary>
    /// (0,0)が左上。返すのは実際に占有するマスのみ。
    /// 通常アイテムは従来どおり幅×高さの長方形を返す。
    /// </summary>
    // IrregularShapesV2 ItemData : 4-way rotation, y axis goes downward.
    public IEnumerable<Vector2Int> GetOccupiedCells(bool isRotated)
    {
        return GetOccupiedCells(isRotated ? 1 : 0);
    }

    public IEnumerable<Vector2Int> GetOccupiedCells(int rotationSteps)
    {
        int w = Mathf.Max(1, width);
        int h = Mathf.Max(1, height);
        int steps = canRotate ? ((rotationSteps % 4) + 4) % 4 : 0;
        if (HasCustomShape)
        {
            foreach (Vector2Int cell in customShapeCells)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= w || cell.y >= h) continue;
                int x = cell.x, y = cell.y, cw = w, ch = h;
                for (int i = 0; i < steps; i++)
                {
                    int nx = ch - 1 - y;
                    int ny = x;
                    x = nx; y = ny;
                    int tmp = cw; cw = ch; ch = tmp;
                }
                yield return new Vector2Int(x, y);
            }
            yield break;
        }
        Vector2Int size = GetSize(steps);
        for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                yield return new Vector2Int(x, y);
    }
    public int MaxStack => maxStack;

    public int PurchasePrice => purchasePrice;
    public int SellPrice => sellPrice;
    public bool CanSellToShop => canSellToShop;

    public float Weight => weight;

    public bool CanStack => maxStack > 1;

    public virtual InventoryItemType ItemType =>
        InventoryItemType.Misc;

    private void OnEnable()
    {
        EnsureLocalizedStrings();

        currentDisplayName = GetFallbackDisplayName();
        currentDescription = description ?? string.Empty;

        SubscribeLocalizedText();
    }

    private void OnDisable()
    {
        UnsubscribeLocalizedText();
    }

    // 回転状態を含めた、現在必要なマス数を返す
    public Vector2Int GetSize(bool isRotated)
    {
        if (isRotated && canRotate)
        {
            return new Vector2Int(height, width);
        }

        return new Vector2Int(width, height);
    }

    public Vector2Int GetSize(int rotationSteps)
    {
        int steps = canRotate ? ((rotationSteps % 4) + 4) % 4 : 0;
        return (steps & 1) != 0 ? new Vector2Int(height, width) : new Vector2Int(width, height);
    }

    private void SubscribeLocalizedText()
    {
        if (!isDisplayNameSubscribed &&
            localizedDisplayName != null &&
            !localizedDisplayName.IsEmpty)
        {
            localizedDisplayName.StringChanged +=
                HandleDisplayNameChanged;

            isDisplayNameSubscribed = true;
            localizedDisplayName.RefreshString();
        }

        if (!isDescriptionSubscribed &&
            localizedDescription != null &&
            !localizedDescription.IsEmpty)
        {
            localizedDescription.StringChanged +=
                HandleDescriptionChanged;

            isDescriptionSubscribed = true;
            localizedDescription.RefreshString();
        }
    }

    private void UnsubscribeLocalizedText()
    {
        if (isDisplayNameSubscribed &&
            localizedDisplayName != null)
        {
            localizedDisplayName.StringChanged -=
                HandleDisplayNameChanged;

            isDisplayNameSubscribed = false;
        }

        if (isDescriptionSubscribed &&
            localizedDescription != null)
        {
            localizedDescription.StringChanged -=
                HandleDescriptionChanged;

            isDescriptionSubscribed = false;
        }
    }

    private void HandleDisplayNameChanged(
        string localizedText)
    {
        currentDisplayName =
            string.IsNullOrWhiteSpace(localizedText)
                ? GetFallbackDisplayName()
                : localizedText;

        NotifyLocalizedTextChanged();
    }

    private void HandleDescriptionChanged(
        string localizedText)
    {
        currentDescription =
            localizedText ?? description ?? string.Empty;

        NotifyLocalizedTextChanged();
    }

    private void NotifyLocalizedTextChanged()
    {
        OnLocalizedTextChanged?.Invoke(this);
    }

    private string GetFallbackDisplayName()
    {
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            return displayName;
        }

        return string.IsNullOrWhiteSpace(itemId)
            ? "Item"
            : itemId;
    }

    private void EnsureLocalizedStrings()
    {
        if (localizedDisplayName == null)
        {
            localizedDisplayName = new LocalizedString();
        }

        if (localizedDescription == null)
        {
            localizedDescription = new LocalizedString();
        }
    }

    protected virtual void OnValidate()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);
        maxStack = Mathf.Max(1, maxStack);

        if (customShapeCells == null)
            customShapeCells = new List<Vector2Int>();

        // サイズ変更後に範囲外になったセルを除去。
        customShapeCells.RemoveAll(c =>
            c.x < 0 || c.y < 0 || c.x >= width || c.y >= height);

        // 意図しない重複セルを除去。
        var unique = new HashSet<Vector2Int>(customShapeCells);
        if (unique.Count != customShapeCells.Count)
            customShapeCells = new List<Vector2Int>(unique);

        purchasePrice = Mathf.Max(0, purchasePrice);
        sellPrice = Mathf.Max(0, sellPrice);

        weight = Mathf.Max(0f, weight);

        EnsureLocalizedStrings();
    }
}