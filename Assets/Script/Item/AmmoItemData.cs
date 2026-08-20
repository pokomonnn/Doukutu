using UnityEngine;

public enum AmmoVariant
{
    Standard,
    ArmorPiercing,
    HollowPoint,
    Subsonic,
    Incendiary,
    Custom
}

[CreateAssetMenu(
    fileName = "NewAmmoItemData",
    menuName = "Inventory/Items/Ammo Item Data"
)]
public class AmmoItemData : ItemData
{
    [Header("弾薬設定")]
    [Tooltip("表示・管理用の名前です。既存データとの互換用に残しています。")]
    [SerializeField] private string ammoTypeName = "9mm";

    [Tooltip(
        "銃との互換性判定に使う口径IDです。" +
        "例：9mm、556、12Gauge。空欄ならAmmo Type Nameを使用します。"
    )]
    [SerializeField] private string ammoCaliberId = "9mm";

    [Tooltip("弾種です。通常弾・徹甲弾・ホローポイントなどを設定します。")]
    [SerializeField] private AmmoVariant ammoVariant = AmmoVariant.Standard;

    [Header("弾種性能")]
    [Tooltip(
        "Bullet PrefabのDamageDealerに設定されている基礎Damageへ掛ける倍率です。" +
        "1.0なら通常、1.2なら20%増加です。"
    )]
    [SerializeField, Min(0f)] private float damageMultiplier = 1f;

    [Tooltip(
        "将来の装甲システムへ渡す徹甲値です。現在はDamageDealerへ保持されますが、" +
        "装甲値を持つ敵側の処理が無いため、この数値だけではダメージ計算は変化しません。"
    )]
    [SerializeField, Min(0f)] private float armorPenetration;

    public override InventoryItemType ItemType =>
        InventoryItemType.Ammo;

    public string AmmoTypeName => ammoTypeName;

    public string CaliberId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ammoCaliberId))
            {
                return ammoCaliberId.Trim();
            }

            return ammoTypeName?.Trim() ?? string.Empty;
        }
    }

    public AmmoVariant Variant => ammoVariant;
    public float DamageMultiplier => Mathf.Max(0f, damageMultiplier);
    public float ArmorPenetration => Mathf.Max(0f, armorPenetration);

    protected override void OnValidate()
    {
        base.OnValidate();

        ammoTypeName = ammoTypeName?.Trim() ?? string.Empty;
        ammoCaliberId = ammoCaliberId?.Trim() ?? string.Empty;
        damageMultiplier = Mathf.Max(0f, damageMultiplier);
        armorPenetration = Mathf.Max(0f, armorPenetration);
    }
}
