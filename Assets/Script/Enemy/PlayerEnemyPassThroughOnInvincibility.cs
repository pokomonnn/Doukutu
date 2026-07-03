using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Player と Enemy の物理衝突を制御するコンポーネントです。
/// 
/// 初期設定では、Enemy と常に物理衝突しないため、敵が攻撃していない時でも
/// Player が押され続けることはありません。
/// 敵の攻撃は EnemyChaser2D / CeilingSpiderEnemy2D の距離判定で行われるため、
/// Player と Enemy のColliderを衝突させる必要はありません。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class PlayerEnemyPassThroughOnInvincibility : MonoBehaviour
{
    public enum EnemyCollisionMode
    {
        // 既存コンポーネントを置き換えた時にも、この値が初期値になります。
        AlwaysIgnoreEnemyCollisions = 0,
        IgnoreOnlyWhileInvincible = 1
    }

    [Header("参照")]
    [Tooltip("未設定なら同じGameObjectから自動取得します")]
    [SerializeField] private CharacterHealth playerHealth;

    [Header("対象")]
    [Tooltip("敵のルートObject、または子Colliderの親に設定しているEnemyレイヤーを指定します")]
    [SerializeField] private LayerMask enemyLayers;

    [Header("衝突ルール")]
    [Tooltip("通常は Always Ignore Enemy Collisions のまま使います。敵は距離で攻撃するため、常にすり抜けにした方がPlayerが押されません。")]
    [SerializeField]
    private EnemyCollisionMode collisionMode =
        EnemyCollisionMode.AlwaysIgnoreEnemyCollisions;

    [Tooltip("敵Prefabが途中で生成された時にも無視対象へ加える間隔です")]
    [SerializeField, Min(0.02f)] private float refreshInterval = 0.15f;

    [Tooltip("通常はオフのままでOKです。Enemy側のTriggerイベントまで無視したい場合だけオンにします")]
    [SerializeField] private bool includeEnemyTriggerColliders;

    [Tooltip("通常はオフのままでOKです。Player側のTrigger Colliderも無視対象へ含めたい場合だけオンにします")]
    [SerializeField] private bool includePlayerTriggerColliders;

    private readonly HashSet<Collider2D> ignoredEnemyColliders =
        new HashSet<Collider2D>();

    private Collider2D[] playerColliders;
    private bool isIgnoringEnemyCollisions;
    private float nextRefreshTime;

    public bool IsPassingThroughEnemies =>
        isIgnoringEnemyCollisions;

    public bool IsAlwaysIgnoringEnemyCollisions =>
        collisionMode ==
        EnemyCollisionMode.AlwaysIgnoreEnemyCollisions;

    private void Awake()
    {
        FindReferences();
        RefreshPlayerColliders();
    }

    private void OnEnable()
    {
        RefreshPassThroughState();
    }

    private void Update()
    {
        RefreshPassThroughState();
    }

    private void OnDisable()
    {
        RestoreEnemyCollisions();
    }

    private void OnDestroy()
    {
        RestoreEnemyCollisions();
    }

    /// <summary>
    /// Enemy側の攻撃直後にも呼べる、互換性維持用の公開メソッドです。
    /// 常時すり抜け設定では、攻撃前でも自動的に有効になります。
    /// </summary>
    public void RefreshPassThroughState()
    {
        FindReferences();

        if (!ShouldIgnoreEnemyCollisions())
        {
            RestoreEnemyCollisions();
            return;
        }

        if (!isIgnoringEnemyCollisions)
        {
            BeginIgnoringEnemyCollisions();
            return;
        }

        if (Time.time >= nextRefreshTime)
        {
            IgnoreAllEnemyCollisions();
            nextRefreshTime = Time.time + refreshInterval;
        }
    }

    private bool ShouldIgnoreEnemyCollisions()
    {
        if (collisionMode ==
            EnemyCollisionMode.AlwaysIgnoreEnemyCollisions)
        {
            return true;
        }

        return playerHealth != null &&
               !playerHealth.IsDead &&
               playerHealth.IsInvincible;
    }

    private void BeginIgnoringEnemyCollisions()
    {
        RefreshPlayerColliders();

        if (playerColliders == null || playerColliders.Length == 0)
        {
            return;
        }

        isIgnoringEnemyCollisions = true;
        IgnoreAllEnemyCollisions();
        nextRefreshTime = Time.time + refreshInterval;
    }

    private void RestoreEnemyCollisions()
    {
        if (!isIgnoringEnemyCollisions &&
            ignoredEnemyColliders.Count == 0)
        {
            return;
        }

        RefreshPlayerColliders();

        foreach (Collider2D enemyCollider in ignoredEnemyColliders)
        {
            if (enemyCollider != null)
            {
                SetCollisionIgnored(enemyCollider, false);
            }
        }

        ignoredEnemyColliders.Clear();
        isIgnoringEnemyCollisions = false;
        nextRefreshTime = 0f;
    }

    private void IgnoreAllEnemyCollisions()
    {
        RefreshPlayerColliders();

        if (playerColliders == null || playerColliders.Length == 0)
        {
            return;
        }

        Collider2D[] allColliders =
            FindObjectsByType<Collider2D>(
                FindObjectsInactive.Exclude
            );

        foreach (Collider2D enemyCollider in allColliders)
        {
            if (!IsEnemyCollider(enemyCollider))
            {
                continue;
            }

            SetCollisionIgnored(enemyCollider, true);
            ignoredEnemyColliders.Add(enemyCollider);
        }
    }

    private void SetCollisionIgnored(
        Collider2D enemyCollider,
        bool ignored)
    {
        if (enemyCollider == null || playerColliders == null)
        {
            return;
        }

        foreach (Collider2D playerCollider in playerColliders)
        {
            if (playerCollider == null ||
                playerCollider == enemyCollider)
            {
                continue;
            }

            Physics2D.IgnoreCollision(
                playerCollider,
                enemyCollider,
                ignored
            );
        }
    }

    private bool IsEnemyCollider(Collider2D targetCollider)
    {
        if (targetCollider == null ||
            targetCollider.transform.IsChildOf(transform))
        {
            return false;
        }

        if (!includeEnemyTriggerColliders &&
            targetCollider.isTrigger)
        {
            return false;
        }

        return HasEnemyLayerInHierarchy(
            targetCollider.transform
        );
    }

    private bool HasEnemyLayerInHierarchy(Transform target)
    {
        Transform current = target;

        while (current != null)
        {
            if ((enemyLayers.value &
                 (1 << current.gameObject.layer)) != 0)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void RefreshPlayerColliders()
    {
        Collider2D[] allPlayerColliders =
            GetComponentsInChildren<Collider2D>(true);

        List<Collider2D> usableColliders =
            new List<Collider2D>();

        foreach (Collider2D playerCollider in allPlayerColliders)
        {
            if (playerCollider == null)
            {
                continue;
            }

            if (!includePlayerTriggerColliders &&
                playerCollider.isTrigger)
            {
                continue;
            }

            usableColliders.Add(playerCollider);
        }

        playerColliders = usableColliders.ToArray();
    }

    private void FindReferences()
    {
        if (playerHealth == null)
        {
            playerHealth = GetComponent<CharacterHealth>();
        }
    }

    private void OnValidate()
    {
        refreshInterval = Mathf.Max(0.02f, refreshInterval);
    }
}
