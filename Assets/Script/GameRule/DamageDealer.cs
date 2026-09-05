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

    [Header("高速弾すり抜け対策")]
    [Tooltip(
        "オンの場合、Rigidbody2Dの次の物理フレーム移動区間を先読みして、" +
        "CircleCastでも命中確認します。高速弾のEnemyすり抜け対策です。"
    )]
    [SerializeField] private bool enableContinuousHitSweep = true;

    [Tooltip(
        "オンの場合、この弾に使われているRigidbody2DのCollision Detectionを" +
        "Continuousへ自動変更します。"
    )]
    [SerializeField] private bool forceContinuousCollisionDetection = true;

    [Tooltip(
        "先読みCircleCastの太さ倍率です。" +
        "Bullet Colliderの短い辺を基準にします。通常は0.8～1.0でOKです。"
    )]
    [SerializeField, Range(0.1f, 2f)]
    private float sweepRadiusScale = 0.9f;

    [Tooltip(
        "次フレームの移動距離へ追加する余裕距離です。" +
        "薄いColliderをより確実に拾いたい場合に少し増やします。"
    )]
    [SerializeField, Min(0f)]
    private float sweepExtraDistance = 0.02f;

    [Header("高速弾すり抜け診断")]
    [Tooltip(
        "オンにすると、Triggerではなく先読みCastでEnemyを拾った時だけログを出します。"
    )]
    [SerializeField] private bool logSweepHits = false;

    private readonly HashSet<CharacterHealth> damagedTargets = new();

    private AmmoItemData runtimeAmmoData;
    private int runtimeDamage = -1;
    private float runtimeSkillDamageMultiplier = 1f;

    // 0 = 通常攻撃。
    // 0以外 = 同じ1回のショットに属する弾/Pelletを識別するID。
    private int runtimeDamageGroupId;

    private Collider2D damageCollider;
    private Rigidbody2D bulletRigidbody;

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

    private void Awake()
    {
        CachePhysicsReferences();
        ApplyContinuousCollisionDetection();
    }

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col != null)
        {
            col.isTrigger = true;
        }

        CachePhysicsReferences();
        ApplyContinuousCollisionDetection();
    }

    private void OnEnable()
    {
        damagedTargets.Clear();
        runtimeDamageGroupId = 0;

        CachePhysicsReferences();
        ApplyContinuousCollisionDetection();
    }

    private void FixedUpdate()
    {
        if (!enableContinuousHitSweep)
        {
            return;
        }

        CachePhysicsReferences();

        if (damageCollider == null ||
            bulletRigidbody == null ||
            !bulletRigidbody.simulated)
        {
            return;
        }

        Vector2 velocity = bulletRigidbody.linearVelocity;
        float speed = velocity.magnitude;

        if (speed <= 0.001f)
        {
            return;
        }

        float castDistance =
            (speed * Time.fixedDeltaTime) +
            Mathf.Max(0f, sweepExtraDistance);

        if (castDistance <= 0.001f)
        {
            return;
        }

        Bounds bounds = damageCollider.bounds;

        float minimumExtent = Mathf.Min(
            Mathf.Abs(bounds.extents.x),
            Mathf.Abs(bounds.extents.y)
        );

        float radius = Mathf.Max(
            0.01f,
            minimumExtent *
            Mathf.Clamp(sweepRadiusScale, 0.1f, 2f)
        );

        Vector2 origin = bounds.center;
        Vector2 direction = velocity / speed;

        RaycastHit2D[] hits = Physics2D.CircleCastAll(
            origin,
            radius,
            direction,
            castDistance
        );

        if (hits == null || hits.Length == 0)
        {
            return;
        }

        System.Array.Sort(
            hits,
            (a, b) => a.distance.CompareTo(b.distance)
        );

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D hitCollider = hit.collider;

            if (hitCollider == null ||
                IsOwnCollider(hitCollider))
            {
                continue;
            }

            if (TryDealDamage(
                    hitCollider,
                    true,
                    hit.point
                ))
            {
                if (logSweepHits)
                {
                    Debug.Log(
                        "[DamageDealer][Sweep命中] " +
                        $"Bullet={name} / " +
                        $"Target={hitCollider.name} / " +
                        $"Distance={hit.distance:F3} / " +
                        $"Speed={speed:F2}",
                        this
                    );
                }

                if (destroyOnHit)
                {
                    return;
                }
            }
        }
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
        TryDealDamage(other, false, null);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        TryDealDamage(other, false, null);
    }

    /// <summary>
    /// 有効なDamage Targetへ処理を行えた場合trueを返します。
    /// Triggerと高速弾Sweepの両方から共通利用します。
    /// </summary>
    private bool TryDealDamage(
        Collider2D other,
        bool fromSweep,
        Vector2? explicitHitPoint)
    {
        if (other == null || IsOwnCollider(other))
        {
            return false;
        }

        CharacterHealth targetHealth =
            other.GetComponentInParent<CharacterHealth>();

        if (targetHealth == null || targetHealth.IsDead)
        {
            return false;
        }

        int targetLayer = targetHealth.gameObject.layer;

        if ((targetLayers.value & (1 << targetLayer)) == 0)
        {
            return false;
        }

        if (hitOnlyOncePerTarget &&
            damagedTargets.Contains(targetHealth))
        {
            return false;
        }

        EnemyHitReaction2D hitReaction =
            targetHealth.GetComponent<EnemyHitReaction2D>();

        Vector3 hitWorldPosition;

        if (explicitHitPoint.HasValue)
        {
            hitWorldPosition = explicitHitPoint.Value;
        }
        else
        {
            Vector2 sourcePosition = damageCollider != null
                ? (Vector2)damageCollider.bounds.center
                : (Vector2)transform.position;

            hitWorldPosition = other.ClosestPoint(sourcePosition);
        }

        if (!targetHealth.IsInvincible)
        {
            Vector3 hitSourcePosition =
                fromSweep && bulletRigidbody != null
                    ? (Vector3)bulletRigidbody.position
                    : transform.position;

            hitReaction?.NotifyHitSource(hitSourcePosition);
        }

        int finalDamage = CurrentDamage;

        EnemyArmor2D enemyArmor =
            targetHealth.GetComponent<EnemyArmor2D>();

        if (enemyArmor == null)
        {
            enemyArmor = targetHealth.GetComponentInChildren<EnemyArmor2D>(true);
        }

        if (enemyArmor != null)
        {
            finalDamage = enemyArmor.CalculateDamageAfterArmor(
                CurrentDamage,
                ArmorPenetration
            );
        }

        targetHealth.TakeDamage(
            finalDamage,
            runtimeDamageGroupId,
            hitWorldPosition
        );

        damagedTargets.Add(targetHealth);

        if (destroyOnHit)
        {
            if (damageCollider != null)
            {
                damageCollider.enabled = false;
            }

            if (bulletRigidbody != null)
            {
                bulletRigidbody.linearVelocity = Vector2.zero;
            }

            Destroy(gameObject);
        }

        return true;
    }

    private bool IsOwnCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (damageCollider != null &&
            other == damageCollider)
        {
            return true;
        }

        Transform otherTransform = other.transform;

        return otherTransform == transform ||
               otherTransform.IsChildOf(transform) ||
               transform.IsChildOf(otherTransform);
    }

    private void CachePhysicsReferences()
    {
        if (damageCollider == null)
        {
            damageCollider = GetComponent<Collider2D>();
        }

        if (bulletRigidbody == null)
        {
            bulletRigidbody = GetComponent<Rigidbody2D>();

            if (bulletRigidbody == null)
            {
                bulletRigidbody =
                    GetComponentInParent<Rigidbody2D>();
            }
        }
    }

    private void ApplyContinuousCollisionDetection()
    {
        if (!forceContinuousCollisionDetection)
        {
            return;
        }

        CachePhysicsReferences();

        if (bulletRigidbody == null)
        {
            return;
        }

        bulletRigidbody.collisionDetectionMode =
            CollisionDetectionMode2D.Continuous;
    }

    private void OnValidate()
    {
        sweepRadiusScale =
            Mathf.Clamp(sweepRadiusScale, 0.1f, 2f);

        sweepExtraDistance =
            Mathf.Max(0f, sweepExtraDistance);
    }
}
