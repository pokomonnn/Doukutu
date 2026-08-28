using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField, Min(1)] private int damage = 10;

    [Tooltip("ダメージを与えられる対象のLayer")]
    [SerializeField] private LayerMask targetLayers;

    [Header("Hit Settings")]
    [Tooltip("同じ相手に1回だけダメージを与える")]
    [SerializeField] private bool hitOnlyOncePerTarget = true;

    [Tooltip("ダメージを与えたら、このオブジェクトを消す（弾用）")]
    [SerializeField] private bool destroyOnHit = true;

    private readonly HashSet<CharacterHealth> damagedTargets = new();

    private AmmoItemData runtimeAmmoData;
    private int runtimeDamage = -1;
    private float runtimeSkillDamageMultiplier = 1f;

    // 0 = 通常攻撃。
    // 0以外 = 同じ1回のショットに属する弾/Pelletを識別するID。
    private int runtimeDamageGroupId;

    public int BaseDamage => Mathf.Max(1, damage);
    public int CurrentDamage => runtimeDamage >= 0
        ? runtimeDamage
        : BaseDamage;

    public AmmoItemData RuntimeAmmoData => runtimeAmmoData;

    /// <summary>
    /// 現在の弾が持つ徹甲値です。
    /// 敵側に装甲システムを追加した時、この値を利用できます。
    /// </summary>
    public float ArmorPenetration => runtimeAmmoData != null
        ? runtimeAmmoData.ArmorPenetration
        : 0f;

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnEnable()
    {
        damagedTargets.Clear();
        runtimeDamageGroupId = 0;
    }

    /// <summary>
    /// GunShooterが弾を生成した直後に呼びます。
    /// Bullet Prefabの基礎DamageへAmmoItemDataの倍率を反映します。
    /// </summary>
    public void ConfigureAmmo(AmmoItemData ammoData)
    {
        runtimeAmmoData = ammoData;
        RecalculateRuntimeDamage();
    }

    /// <summary>
    /// GunShooterから、装備中スキルカードの武器ダメージ倍率を渡します。
    /// </summary>
    public void ConfigureSkillDamageMultiplier(float multiplier)
    {
        runtimeSkillDamageMultiplier = Mathf.Max(0f, multiplier);
        RecalculateRuntimeDamage();
    }

    /// <summary>
    /// 同じ1回のショットから生成されたBullet/Pelletへ共通IDを設定します。
    /// 0の場合は従来どおりの通常ダメージです。
    /// </summary>
    public void ConfigureDamageGroup(int damageGroupId)
    {
        runtimeDamageGroupId = Mathf.Max(0, damageGroupId);
    }

    private void RecalculateRuntimeDamage()
    {
        float ammoMultiplier = runtimeAmmoData != null
            ? runtimeAmmoData.DamageMultiplier
            : 1f;

        runtimeDamage = Mathf.Max(
            1,
            Mathf.RoundToInt(
                BaseDamage *
                ammoMultiplier *
                Mathf.Max(0f, runtimeSkillDamageMultiplier)
            )
        );
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void TryDealDamage(Collider2D other)
    {
        CharacterHealth targetHealth =
            other.GetComponentInParent<CharacterHealth>();

        if (targetHealth == null || targetHealth.IsDead)
        {
            return;
        }

        int targetLayer = targetHealth.gameObject.layer;

        if ((targetLayers.value & (1 << targetLayer)) == 0)
        {
            return;
        }

        if (hitOnlyOncePerTarget && damagedTargets.Contains(targetHealth))
        {
            return;
        }

        EnemyHitReaction2D hitReaction =
            targetHealth.GetComponent<EnemyHitReaction2D>();

        if (!targetHealth.IsInvincible)
        {
            hitReaction?.NotifyHitSource(transform.position);
        }

        targetHealth.TakeDamage(
            CurrentDamage,
            runtimeDamageGroupId
        );

        damagedTargets.Add(targetHealth);

        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }
}
