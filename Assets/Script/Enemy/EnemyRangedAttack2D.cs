using System;
using UnityEngine;

/// <summary>
/// 遠距離Enemy用のProjectile攻撃です。
///
/// 通常の直線射撃に加えて、槍投げのような放物線（Ballistic Arc）に対応します。
/// Projectile Prefab側にDamageDealerとRigidbody2Dを付け、
/// DamageDealerのTarget LayersはPlayerを指定してください。
///
/// 放物線モードではProjectileのRigidbody2DをDynamicにし、
/// Gravity Scaleを使用してPlayer位置へ山なりに投げます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class EnemyRangedAttack2D : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private EnemyChaser2D enemyChaser;
    [SerializeField] private CharacterHealth ownHealth;
    [SerializeField] private Transform projectileSpawnPoint;
    [SerializeField] private GameObject projectilePrefab;

    [Header("攻撃")]
    [SerializeField, Min(0f)] private float minimumAttackDistance = 0f;
    [SerializeField, Min(0f)] private float maximumAttackDistance = 8f;
    [SerializeField, Min(0.01f)] private float attackInterval = 1.5f;

    [Tooltip("直線射撃時のProjectile速度です。放物線モードでは自動計算した初速を使用します。")]
    [SerializeField, Min(0f)] private float projectileSpeed = 10f;

    [SerializeField, Min(0.05f)] private float projectileLifeTime = 5f;

    [Tooltip("プレイヤーを発見している時だけ射撃します")]
    [SerializeField] private bool requireDetectedPlayer = true;

    [Header("槍投げ・放物線")]
    [Tooltip("ONで槍投げのような放物線軌道、OFFで従来の直線射撃になります。")]
    [SerializeField] private bool useBallisticArc = true;

    [Tooltip("Playerより何m高い位置を頂点として投げるか。大きいほど高い山なりになります。")]
    [SerializeField, Min(0.05f)] private float arcHeight = 2f;

    [Tooltip("放物線Projectileへ設定するGravity Scaleです。通常は1前後。大きいほど素早く落下します。")]
    [SerializeField, Min(0.01f)] private float projectileGravityScale = 1f;

    [Tooltip("ONなら放物線モード時にProjectileのRigidbody2DをDynamicへ変更します。")]
    [SerializeField] private bool forceDynamicBodyForBallistic = true;

    [Tooltip("放物線計算ができない場合、従来の直線射撃へフォールバックします。")]
    [SerializeField] private bool fallbackToStraightShot = true;

    [Header("射線")]
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("照準")]
    [Tooltip("Player Transform位置へ加える狙い位置の補正です。胸や足元などを狙わせたい場合に調整します。")]
    [SerializeField] private Vector2 targetOffset;

    [Header("デバッグ")]
    [SerializeField] private bool showAttackRangeGizmo = true;
    [SerializeField] private bool showTrajectoryGizmo = true;
    [SerializeField, Range(6, 40)] private int trajectoryGizmoSegments = 16;
    [SerializeField] private bool showAttackLogs;

    public event Action RangedAttackPerformed;

    public bool CanCurrentlyAttack => CanAttackNow(false);

    private float nextAttackTime;
    private CharacterAIPersonality2D personalityAI;

    private void Awake()
    {
        FindReferences();
        personalityAI = GetComponent<CharacterAIPersonality2D>();
    }

    private void OnEnable()
    {
        FindReferences();
        nextAttackTime = Time.time;
    }

    private void Update()
    {
        FindReferences();

        if (Time.time < nextAttackTime || !CanAttackNow(true))
        {
            return;
        }

        FireProjectile();
    }

    private bool CanAttackNow(bool checkCooldown)
    {
        if (checkCooldown && Time.time < nextAttackTime)
        {
            return false;
        }

        if (ownHealth == null || ownHealth.IsDead ||
            enemyChaser == null ||
            enemyChaser.PlayerTransform == null ||
            projectilePrefab == null)
        {
            return false;
        }

        if (requireDetectedPlayer && !enemyChaser.HasDetectedPlayer)
        {
            return false;
        }

        Transform player = enemyChaser.PlayerTransform;
        float distance = Vector2.Distance(
            transform.position,
            player.position
        );

        if (distance < minimumAttackDistance ||
            distance > maximumAttackDistance)
        {
            return false;
        }

        return !requireLineOfSight || HasLineOfSight(player);
    }

    private bool HasLineOfSight(Transform player)
    {
        if (player == null || obstacleLayers.value == 0)
        {
            return true;
        }

        Vector2 origin = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position;

        Vector2 target = (Vector2)player.position + targetOffset;
        Vector2 delta = target - origin;
        float distance = delta.magnitude;

        if (distance <= 0.001f)
        {
            return true;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            delta / distance,
            distance,
            obstacleLayers
        );

        return hit.collider == null;
    }

    private void FireProjectile()
    {
        Transform player = enemyChaser != null
            ? enemyChaser.PlayerTransform
            : null;

        if (player == null || projectilePrefab == null)
        {
            return;
        }

        Vector2 spawnPosition = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position;

        Vector2 targetPosition = (Vector2)player.position + targetOffset;

        bool ballisticSucceeded = false;
        Vector2 launchVelocity;

        if (useBallisticArc)
        {
            ballisticSucceeded = TryCalculateBallisticVelocity(
                spawnPosition,
                targetPosition,
                out launchVelocity,
                out _
            );

            if (!ballisticSucceeded && !fallbackToStraightShot)
            {
                if (showAttackLogs)
                {
                    Debug.LogWarning(
                        $"[EnemyRangedAttack2D] {name}: 放物線初速を計算できなかったため発射を中止しました。",
                        this
                    );
                }

                nextAttackTime = Time.time + attackInterval *
                    (personalityAI != null ? personalityAI.AttackIntervalMultiplier : 1f);
                return;
            }
        }
        else
        {
            launchVelocity = CalculateStraightVelocity(
                spawnPosition,
                targetPosition
            );
        }

        if (useBallisticArc && !ballisticSucceeded)
        {
            launchVelocity = CalculateStraightVelocity(
                spawnPosition,
                targetPosition
            );
        }

        if (launchVelocity.sqrMagnitude <= 0.0001f)
        {
            launchVelocity = Vector2.right * Mathf.Max(0.01f, projectileSpeed);
        }

        float angle = Mathf.Atan2(
            launchVelocity.y,
            launchVelocity.x
        ) * Mathf.Rad2Deg;

        GameObject projectile = Instantiate(
            projectilePrefab,
            spawnPosition,
            Quaternion.Euler(0f, 0f, angle)
        );

        // 発射者を弾に通知。自分への命中・物理接触・同派閥への誤射を防ぐ。
        // Rigidbody2Dに初速を与えるより前に設定し、発射直後の自傷を防止。
        DamageDealer[] projectileDamageDealers =
            projectile.GetComponentsInChildren<DamageDealer>(true);
        foreach (DamageDealer dealer in projectileDamageDealers)
        {
            if (dealer != null)
            {
                dealer.ConfigureShooter(transform);
            }
        }

        if (projectileDamageDealers.Length == 0 && showAttackLogs)
        {
            Debug.LogWarning(
                $"[EnemyRangedAttack2D] {projectile.name}: DamageDealerが見つかりません。" +
                " 弾Prefabの親または子に追加してください。",
                this
            );
        }

        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            rb = projectile.GetComponentInChildren<Rigidbody2D>();
        }

        if (rb != null)
        {
            if (useBallisticArc && ballisticSucceeded)
            {
                if (forceDynamicBodyForBallistic &&
                    rb.bodyType != RigidbodyType2D.Dynamic)
                {
                    rb.bodyType = RigidbodyType2D.Dynamic;
                }

                rb.gravityScale = projectileGravityScale;
            }
            else
            {
                // 従来の直線Projectileと同じ挙動を保証する。
                rb.gravityScale = 0f;
            }

            rb.linearVelocity = launchVelocity;

            EnemyThrownProjectile2D thrownProjectile =
                projectile.GetComponent<EnemyThrownProjectile2D>();

            if (thrownProjectile == null)
            {
                thrownProjectile =
                    projectile.GetComponentInChildren<EnemyThrownProjectile2D>();
            }

            thrownProjectile?.ApplyRotationImmediately(launchVelocity);
        }
        else if (showAttackLogs)
        {
            Debug.LogWarning(
                $"[EnemyRangedAttack2D] {projectile.name}: Rigidbody2Dがありません。",
                this
            );
        }

        Destroy(projectile, projectileLifeTime);

        nextAttackTime = Time.time + attackInterval *
                    (personalityAI != null ? personalityAI.AttackIntervalMultiplier : 1f);
        RangedAttackPerformed?.Invoke();

        if (showAttackLogs)
        {
            string mode =
                useBallisticArc && ballisticSucceeded
                    ? "Ballistic/槍投げ"
                    : "Straight/直線";

            Debug.Log(
                $"[EnemyRangedAttack2D] {name}: 遠距離攻撃。" +
                $"Mode={mode} / " +
                $"Distance={Vector2.Distance(transform.position, player.position):0.00} / " +
                $"LaunchVelocity={launchVelocity} / Speed={launchVelocity.magnitude:0.00}",
                this
            );
        }
    }

    /// <summary>
    /// 現在位置と目標位置から、指定したArc Heightを通る放物線の初速を計算します。
    /// Playerが静止していれば理論上targetPositionへ到達します。
    /// </summary>
    private bool TryCalculateBallisticVelocity(
        Vector2 start,
        Vector2 target,
        out Vector2 velocity,
        out float flightTime)
    {
        velocity = Vector2.zero;
        flightTime = 0f;

        float gravity =
            Mathf.Abs(Physics2D.gravity.y) *
            Mathf.Max(0.01f, projectileGravityScale);

        if (gravity <= 0.0001f)
        {
            return false;
        }

        float apexY =
            Mathf.Max(start.y, target.y) +
            Mathf.Max(0.05f, arcHeight);

        float riseHeight = Mathf.Max(0f, apexY - start.y);
        float fallHeight = Mathf.Max(0f, apexY - target.y);

        float verticalSpeed =
            Mathf.Sqrt(2f * gravity * riseHeight);

        float timeUp = verticalSpeed / gravity;
        float timeDown =
            Mathf.Sqrt(2f * fallHeight / gravity);

        flightTime = timeUp + timeDown;

        if (flightTime <= 0.01f ||
            float.IsNaN(flightTime) ||
            float.IsInfinity(flightTime))
        {
            velocity = Vector2.zero;
            flightTime = 0f;
            return false;
        }

        float horizontalSpeed =
            (target.x - start.x) / flightTime;

        velocity = new Vector2(
            horizontalSpeed,
            verticalSpeed
        );

        return !float.IsNaN(velocity.x) &&
               !float.IsNaN(velocity.y) &&
               !float.IsInfinity(velocity.x) &&
               !float.IsInfinity(velocity.y);
    }

    private Vector2 CalculateStraightVelocity(
        Vector2 start,
        Vector2 target)
    {
        Vector2 direction = (target - start).normalized;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.right;
        }

        return direction * projectileSpeed;
    }

    private void FindReferences()
    {
        if (enemyChaser == null)
        {
            enemyChaser = GetComponent<EnemyChaser2D>();
        }

        if (ownHealth == null)
        {
            ownHealth = GetComponent<CharacterHealth>();
        }
    }

    private void OnValidate()
    {
        minimumAttackDistance = Mathf.Max(0f, minimumAttackDistance);
        maximumAttackDistance = Mathf.Max(
            minimumAttackDistance,
            maximumAttackDistance
        );
        attackInterval = Mathf.Max(0.01f, attackInterval);
        projectileSpeed = Mathf.Max(0f, projectileSpeed);
        projectileLifeTime = Mathf.Max(0.05f, projectileLifeTime);
        arcHeight = Mathf.Max(0.05f, arcHeight);
        projectileGravityScale = Mathf.Max(0.01f, projectileGravityScale);
        trajectoryGizmoSegments = Mathf.Clamp(
            trajectoryGizmoSegments,
            6,
            40
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (showAttackRangeGizmo)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, maximumAttackDistance);

            if (minimumAttackDistance > 0f)
            {
                Gizmos.color = Color.gray;
                Gizmos.DrawWireSphere(transform.position, minimumAttackDistance);
            }
        }

        if (!showTrajectoryGizmo ||
            !useBallisticArc ||
            !Application.isPlaying ||
            enemyChaser == null ||
            enemyChaser.PlayerTransform == null)
        {
            return;
        }

        Vector2 start = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position;

        Vector2 target =
            (Vector2)enemyChaser.PlayerTransform.position +
            targetOffset;

        if (!TryCalculateBallisticVelocity(
                start,
                target,
                out Vector2 launchVelocity,
                out float flightTime))
        {
            return;
        }

        float gravityY =
            Physics2D.gravity.y *
            projectileGravityScale;

        Gizmos.color = Color.yellow;

        Vector3 previous = start;
        int segments = Mathf.Clamp(trajectoryGizmoSegments, 6, 40);

        for (int i = 1; i <= segments; i++)
        {
            float t = flightTime * i / segments;

            Vector2 point =
                start +
                launchVelocity * t +
                0.5f * new Vector2(0f, gravityY) * t * t;

            Gizmos.DrawLine(previous, point);
            previous = point;
        }
    }
}
