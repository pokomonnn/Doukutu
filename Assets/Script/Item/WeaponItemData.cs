using System;
using UnityEngine;

public enum WeaponFireMode
{
    [Tooltip("1クリックにつき1回だけ射撃します。ハンドガン・ショットガン向けです。")]
    SemiAuto,

    [Tooltip("左クリックを押している間、Fire Intervalごとに連射します。自動小銃向けです。")]
    FullAuto
}

[CreateAssetMenu(
    fileName = "NewWeaponItemData",
    menuName = "Inventory/Items/Weapon Item Data"
)]
public class WeaponItemData : ItemData
{
    [Header("装備設定")]
    [Tooltip("この武器を装備した時に使う武器Prefab")]
    [SerializeField] private GameObject weaponPrefab;

    [Header("射撃方式")]
    [Tooltip(
        "Semi Auto：1クリックで1発だけ射撃します。ハンドガン・ショットガン向け。" +
        "Full Auto：左クリック長押しでFire Intervalごとに連射します。"
    )]
    [SerializeField] private WeaponFireMode fireMode =
        WeaponFireMode.SemiAuto;

    [Tooltip(
        "1発撃ってから次の1発を撃てるまでの最短時間（秒）です。" +
        "例：0.2なら最大5発/秒、0.1なら最大10発/秒です。"
    )]
    [SerializeField, Min(0f)] private float fireInterval = 0.15f;

    [Header("散弾設定")]
    [Tooltip(
        "1回の射撃で生成する弾丸数です。" +
        "通常の銃は1、ショットガンは6～10程度を目安にしてください。"
    )]
    [SerializeField, Min(1)] private int pelletCount = 1;

    [Tooltip(
        "散弾全体の広がり角度です。" +
        "例：16なら、照準中心から左右に最大約8度ずつ広がります。" +
        "Pellet Countが1の場合は基本的に0のままでOKです。"
    )]
    [SerializeField, Min(0f)] private float pelletSpreadAngle = 0f;

    [Header("弾薬設定")]
    [Tooltip(
        "この銃の基準・優先Ammoです。既存データとの互換用にも使用します。" +
        "同じCaliber Idを持つ別弾種も使用できます。"
    )]
    [SerializeField] private AmmoItemData compatibleAmmo;

    [Tooltip(
        "この銃が使用する口径IDです。例：9mm、556、12Gauge。" +
        "空欄ならCompatible AmmoのCaliber Idを自動使用します。"
    )]
    [SerializeField] private string compatibleAmmoCaliberId;

    [Header("耐久度設定")]
    [Tooltip("新品時の最大耐久度です。通常は100のままでOKです。")]
    [SerializeField, Min(1f)] private float maxDurability = 100f;

    [Tooltip("1発撃つごとに減る耐久度です。例：0.1なら1000発で100→0になります。")]
    [SerializeField, Min(0f)] private float durabilityLossPerShot = 0.1f;

    [Header("低耐久：ジャム")]
    [Tooltip("この耐久割合以下でジャム判定を開始します。0.5なら耐久50%以下。")]
    [SerializeField, Range(0.01f, 1f)]
    private float jamStartDurabilityPercent = 0.5f;

    [Tooltip("ジャム判定開始地点での1発あたりジャム確率。0.01なら1%。")]
    [SerializeField, Range(0f, 1f)]
    private float jamChanceAtStart = 0.01f;

    [Tooltip("耐久度がほぼ0の時の最大ジャム確率。0.25なら25%。")]
    [SerializeField, Range(0f, 1f)]
    private float maxJamChanceAtZeroDurability = 0.25f;

    [Tooltip("ジャム中にRキーを押して解除するまでの秒数。")]
    [SerializeField, Min(0f)] private float jamClearDuration = 1.25f;

    [Header("低耐久：命中精度")]
    [Tooltip("この耐久割合以下で武器劣化による弾のブレを開始します。")]
    [SerializeField, Range(0.01f, 1f)]
    private float accuracyPenaltyStartDurabilityPercent = 0.6f;

    [Tooltip("耐久度が0に近い時、左右どちらかへ最大何度ブレるか。")]
    [SerializeField, Min(0f)]
    private float maxSpreadAngleAtZeroDurability = 6f;

    [Header("低耐久：リロード")]
    [Tooltip("この耐久割合以下でリロード時間を長くします。")]
    [SerializeField, Range(0.01f, 1f)]
    private float reloadPenaltyStartDurabilityPercent = 0.6f;

    [Tooltip("耐久度が0に近い時の最大リロード時間倍率。1.75なら75%遅くなります。")]
    [SerializeField, Min(1f)]
    private float maxReloadDurationMultiplierAtZeroDurability = 1.75f;

    [Header("武器屋修理価格")]
    [Tooltip("損傷100%の銃を新品まで直す時の基本価格。")]
    [SerializeField, Min(0)] private int fullRepairCost = 5000;

    [Tooltip("少しでも損傷している場合に必要な最低修理価格。0なら最低料金なし。")]
    [SerializeField, Min(0)] private int minimumRepairCost = 100;

    public override InventoryItemType ItemType =>
        InventoryItemType.Weapon;

    public EquipmentSlotType EquipmentSlot =>
        EquipmentSlotType.PrimaryWeapon;

    public GameObject WeaponPrefab => weaponPrefab;
    public WeaponFireMode FireMode => fireMode;
    public float FireInterval => Mathf.Max(0f, fireInterval);
    public int PelletCount => Mathf.Max(1, pelletCount);
    public float PelletSpreadAngle => Mathf.Max(0f, pelletSpreadAngle);

    // 既存スクリプト互換用。今後はPreferredAmmoとして扱います。
    public AmmoItemData CompatibleAmmo => compatibleAmmo;
    public AmmoItemData PreferredAmmo => compatibleAmmo;

    public float MaxDurability => Mathf.Max(1f, maxDurability);
    public float DurabilityLossPerShot => Mathf.Max(0f, durabilityLossPerShot);

    public float JamStartDurabilityPercent =>
        Mathf.Clamp01(jamStartDurabilityPercent);

    public float JamClearDuration => Mathf.Max(0f, jamClearDuration);

    public float AccuracyPenaltyStartDurabilityPercent =>
        Mathf.Clamp01(accuracyPenaltyStartDurabilityPercent);

    public float MaxSpreadAngleAtZeroDurability =>
        Mathf.Max(0f, maxSpreadAngleAtZeroDurability);

    public float ReloadPenaltyStartDurabilityPercent =>
        Mathf.Clamp01(reloadPenaltyStartDurabilityPercent);

    public float MaxReloadDurationMultiplierAtZeroDurability =>
        Mathf.Max(1f, maxReloadDurationMultiplierAtZeroDurability);

    public int FullRepairCost => Mathf.Max(0, fullRepairCost);
    public int MinimumRepairCost => Mathf.Max(0, minimumRepairCost);

    public string CompatibleAmmoCaliberId
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(compatibleAmmoCaliberId))
            {
                return compatibleAmmoCaliberId.Trim();
            }

            return compatibleAmmo != null
                ? compatibleAmmo.CaliberId
                : string.Empty;
        }
    }

    public bool IsAmmoCompatible(AmmoItemData ammoData)
    {
        if (ammoData == null)
        {
            return false;
        }

        string requiredCaliber = CompatibleAmmoCaliberId;

        if (!string.IsNullOrWhiteSpace(requiredCaliber) &&
            !string.IsNullOrWhiteSpace(ammoData.CaliberId))
        {
            return string.Equals(
                requiredCaliber,
                ammoData.CaliberId,
                StringComparison.OrdinalIgnoreCase
            );
        }

        return compatibleAmmo == ammoData;
    }

    /// <summary>
    /// 現在の耐久割合から、1発ごとのジャム確率を返します。
    /// 耐久0ではGunShooter側で完全故障として射撃自体を止めます。
    /// </summary>
    public float GetJamChance(float durabilityPercent)
    {
        float percent = Mathf.Clamp01(durabilityPercent);
        float start = JamStartDurabilityPercent;

        if (percent > start || percent <= 0f)
        {
            return 0f;
        }

        float strength = Mathf.InverseLerp(start, 0f, percent);

        return Mathf.Clamp01(
            Mathf.Lerp(
                Mathf.Clamp01(jamChanceAtStart),
                Mathf.Clamp01(maxJamChanceAtZeroDurability),
                strength
            )
        );
    }

    /// <summary>現在耐久から追加される最大ブレ角度を返します。</summary>
    public float GetDurabilitySpreadAngle(float durabilityPercent)
    {
        float percent = Mathf.Clamp01(durabilityPercent);
        float start = AccuracyPenaltyStartDurabilityPercent;

        if (percent >= start || MaxSpreadAngleAtZeroDurability <= 0f)
        {
            return 0f;
        }

        float strength = Mathf.InverseLerp(start, 0f, percent);
        return MaxSpreadAngleAtZeroDurability * strength;
    }

    /// <summary>現在耐久からリロード時間倍率を返します。</summary>
    public float GetReloadDurationMultiplier(float durabilityPercent)
    {
        float percent = Mathf.Clamp01(durabilityPercent);
        float start = ReloadPenaltyStartDurabilityPercent;

        if (percent >= start)
        {
            return 1f;
        }

        float strength = Mathf.InverseLerp(start, 0f, percent);

        return Mathf.Lerp(
            1f,
            MaxReloadDurationMultiplierAtZeroDurability,
            strength
        );
    }

    /// <summary>現在耐久から新品まで修理する武器屋価格を返します。</summary>
    public int CalculateFullRepairCost(float currentDurability)
    {
        float max = MaxDurability;
        float current = Mathf.Clamp(currentDurability, 0f, max);
        float damagePercent = 1f - (current / max);

        if (damagePercent <= 0.0001f || FullRepairCost <= 0)
        {
            return 0;
        }

        long calculated = (long)Mathf.CeilToInt(
            FullRepairCost * damagePercent
        );

        int result = calculated > int.MaxValue
            ? int.MaxValue
            : Mathf.Max(0, (int)calculated);

        if (result > 0)
        {
            result = Mathf.Max(result, MinimumRepairCost);
        }

        return result;
    }

    protected override void OnValidate()
    {
        base.OnValidate();

        compatibleAmmoCaliberId =
            compatibleAmmoCaliberId?.Trim() ?? string.Empty;

        fireInterval = Mathf.Max(0f, fireInterval);
        pelletCount = Mathf.Max(1, pelletCount);
        pelletSpreadAngle = Mathf.Max(0f, pelletSpreadAngle);

        maxDurability = Mathf.Max(1f, maxDurability);
        durabilityLossPerShot = Mathf.Max(0f, durabilityLossPerShot);

        jamStartDurabilityPercent =
            Mathf.Clamp(jamStartDurabilityPercent, 0.01f, 1f);
        jamChanceAtStart = Mathf.Clamp01(jamChanceAtStart);
        maxJamChanceAtZeroDurability =
            Mathf.Clamp01(maxJamChanceAtZeroDurability);
        maxJamChanceAtZeroDurability = Mathf.Max(
            jamChanceAtStart,
            maxJamChanceAtZeroDurability
        );
        jamClearDuration = Mathf.Max(0f, jamClearDuration);

        accuracyPenaltyStartDurabilityPercent =
            Mathf.Clamp(
                accuracyPenaltyStartDurabilityPercent,
                0.01f,
                1f
            );
        maxSpreadAngleAtZeroDurability =
            Mathf.Max(0f, maxSpreadAngleAtZeroDurability);

        reloadPenaltyStartDurabilityPercent =
            Mathf.Clamp(
                reloadPenaltyStartDurabilityPercent,
                0.01f,
                1f
            );
        maxReloadDurationMultiplierAtZeroDurability =
            Mathf.Max(1f, maxReloadDurationMultiplierAtZeroDurability);

        fullRepairCost = Mathf.Max(0, fullRepairCost);
        minimumRepairCost = Mathf.Clamp(
            minimumRepairCost,
            0,
            fullRepairCost
        );
    }
}
