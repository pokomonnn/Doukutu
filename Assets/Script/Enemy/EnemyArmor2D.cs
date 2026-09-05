using UnityEngine;

/// <summary>
/// 敵の装甲値を管理します。
/// DamageDealerからRaw DamageとAmmoのArmor Penetrationを受け取り、
/// 装甲軽減後のダメージを返します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class EnemyArmor2D : MonoBehaviour
{
    [Header("装甲")]
    [Tooltip("敵の装甲値です。0なら装甲なし。徹甲値によって差し引かれます")]
    [SerializeField, Min(0f)] private float armorRating;

    [Tooltip("実効Armor 1につき何%ダメージを軽減するか。0.01=1%です")]
    [SerializeField, Range(0f, 0.1f)]
    private float damageReductionPerArmorPoint = 0.01f;

    [Tooltip("装甲による最大軽減率です。0.8なら最大80%軽減です")]
    [SerializeField, Range(0f, 0.95f)]
    private float maximumDamageReduction = 0.8f;

    [Header("デバッグ")]
    [SerializeField] private bool showArmorLogs;

    public float ArmorRating => Mathf.Max(0f, armorRating);
    public float DamageReductionPerArmorPoint =>
        Mathf.Max(0f, damageReductionPerArmorPoint);
    public float MaximumDamageReduction =>
        Mathf.Clamp(maximumDamageReduction, 0f, 0.95f);

    /// <summary>
    /// Raw Damageへ装甲を適用した最終ダメージを返します。
    /// Armor PenetrationはArmor Ratingから直接差し引きます。
    /// </summary>
    public int CalculateDamageAfterArmor(
        int rawDamage,
        float armorPenetration)
    {
        int safeRawDamage = Mathf.Max(0, rawDamage);

        if (safeRawDamage <= 0)
        {
            return 0;
        }

        float safePenetration = Mathf.Max(0f, armorPenetration);
        float effectiveArmor = Mathf.Max(
            0f,
            ArmorRating - safePenetration
        );

        float reduction = Mathf.Clamp(
            effectiveArmor * DamageReductionPerArmorPoint,
            0f,
            MaximumDamageReduction
        );

        int finalDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(safeRawDamage * (1f - reduction))
        );

        if (showArmorLogs)
        {
            Debug.Log(
                $"[EnemyArmor2D] {name}: " +
                $"Raw={safeRawDamage} / Armor={ArmorRating:0.##} / " +
                $"Pen={safePenetration:0.##} / Effective={effectiveArmor:0.##} / " +
                $"Reduction={reduction * 100f:0.#}% / Final={finalDamage}",
                this
            );
        }

        return finalDamage;
    }

    [ContextMenu("Log Armor Preview")]
    private void LogArmorPreview()
    {
        int previewDamage = 100;
        float previewPenetration = 0f;
        int finalDamage = CalculateDamageAfterArmor(
            previewDamage,
            previewPenetration
        );

        Debug.Log(
            $"[EnemyArmor2D] Preview: Damage {previewDamage} -> {finalDamage}",
            this
        );
    }

    private void OnValidate()
    {
        armorRating = Mathf.Max(0f, armorRating);
        damageReductionPerArmorPoint = Mathf.Clamp(
            damageReductionPerArmorPoint,
            0f,
            0.1f
        );
        maximumDamageReduction = Mathf.Clamp(
            maximumDamageReduction,
            0f,
            0.95f
        );
    }
}
