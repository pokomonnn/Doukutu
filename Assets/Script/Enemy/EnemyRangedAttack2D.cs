using System;
using UnityEngine;

/// <summary>
/// 遠距離Enemy用のProjectile攻撃です。
/// Projectile Prefab側にDamageDealerとRigidbody2Dを付け、
/// DamageDealerのTarget LayersはPlayerを指定してください。
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
    [SerializeField, Min(0f)] private float projectileSpeed = 10f;
    [SerializeField, Min(0.05f)] private float projectileLifeTime = 5f;

    [Tooltip("プレイヤーを発見している時だけ射撃します")]
    [SerializeField] private bool requireDetectedPlayer = true;

    [Header("射線")]
    [SerializeField] private bool requireLineOfSight = true;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("照準")]
    [Tooltip("Player Transform位置へそのまま狙います。必要ならSpawnPoint側で高さを調整してください")]
    [SerializeField] private Vector2 targetOffset;

    [Header("デバッグ")]
    [SerializeField] private bool showAttackRangeGizmo = true;
    [SerializeField] private bool showAttackLogs;

    public event Action RangedAttackPerformed;

    public bool CanCurrentlyAttack => CanAttackNow(false);

    private float nextAttackTime;

    private void Awake()
    {
        FindReferences();
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
        Transform player = enemyChaser.PlayerTransform;

        if (player == null || projectilePrefab == null)
        {
            return;
        }

        Vector2 spawnPosition = projectileSpawnPoint != null
            ? projectileSpawnPoint.position
            : transform.position;

        Vector2 targetPosition = (Vector2)player.position + targetOffset;
        Vector2 direction = (targetPosition - spawnPosition).normalized;

        if (direction.sqrMagnitude <= 0.0001f)
        {
            direction = Vector2.right;
        }

        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

        GameObject projectile = Instantiate(
            projectilePrefab,
            spawnPosition,
            Quaternion.Euler(0f, 0f, angle)
        );

        Rigidbody2D rb = projectile.GetComponent<Rigidbody2D>();

        if (rb == null)
        {
            rb = projectile.GetComponentInChildren<Rigidbody2D>();
        }

        if (rb != null)
        {
            rb.linearVelocity = direction * projectileSpeed;
        }
        else if (showAttackLogs)
        {
            Debug.LogWarning(
                $"[EnemyRangedAttack2D] {projectile.name}: Rigidbody2Dがありません。",
                this
            );
        }

        Destroy(projectile, projectileLifeTime);

        nextAttackTime = Time.time + attackInterval;
        RangedAttackPerformed?.Invoke();

        if (showAttackLogs)
        {
            Debug.Log(
                $"[EnemyRangedAttack2D] {name}: 遠距離攻撃。Distance=" +
                Vector2.Distance(transform.position, player.position).ToString("0.00"),
                this
            );
        }
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
    }

    private void OnDrawGizmosSelected()
    {
        if (!showAttackRangeGizmo)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, maximumAttackDistance);

        if (minimumAttackDistance > 0f)
        {
            Gizmos.color = Color.gray;
            Gizmos.DrawWireSphere(transform.position, minimumAttackDistance);
        }
    }
}
