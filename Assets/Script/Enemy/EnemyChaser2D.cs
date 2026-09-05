using System;
using UnityEngine;

public enum EnemyCombatMovementStyle
{
    MeleeChase,
    KeepDistance
}

/// <summary>
/// 地上を左右に移動してプレイヤーを追跡・攻撃する2D敵です。
/// 深い崖を避け、ジャンプ中に壁へ当たった場合は横移動を止めて
/// 自然に落下・着地します。越えられない崖や壁の先は一度追跡を諦めますが、Playerが近づく・道が安全になると再追跡します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class EnemyChaser2D : MonoBehaviour
{
    private enum PathBlockReason
    {
        None,
        DeepCliff,
        UnclimbableWall
    }

    [Header("プレイヤー参照")]
    [Tooltip("未設定ならシーン内のPlayerMoveを自動取得します")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("未設定ならPlayerから自動取得します")]
    [SerializeField] private CharacterHealth playerHealth;

    [Header("視認設定")]
    [Tooltip("この距離以内でプレイヤーを見つけると追跡します")]
    [SerializeField, Min(0f)] private float detectionDistance = 8f;

    [Tooltip("追跡をやめる距離。Detection Distance以上にしてください")]
    [SerializeField, Min(0f)] private float loseTargetDistance = 10f;

    [Tooltip("オンの場合、Obstacle Layersに遮られている時は新規発見しません")]
    [SerializeField] private bool requireLineOfSight;

    [Tooltip("壁・地形など、視線を遮るレイヤー")]
    [SerializeField] private LayerMask obstacleLayers;

    [Header("移動設定")]
    [SerializeField, Min(0f)] private float moveSpeed = 2.2f;

    [Tooltip("近接型では、この距離以内でプレイヤーへさらに近づきません")]
    [SerializeField, Min(0f)] private float stopDistance = 0.55f;

    [Header("戦闘距離タイプ")]
    [Tooltip("Melee Chase=従来の近接追跡。Keep Distance=遠距離敵向けに一定距離を保ちます")]
    [SerializeField] private EnemyCombatMovementStyle combatMovementStyle =
        EnemyCombatMovementStyle.MeleeChase;

    [Tooltip("Keep Distance時、この距離より近いとプレイヤーから離れます")]
    [SerializeField, Min(0f)] private float keepDistanceMinimum = 3f;

    [Tooltip("Keep Distance時、この距離より遠いとプレイヤーへ近づきます")]
    [SerializeField, Min(0f)] private float keepDistanceMaximum = 6f;

    [Header("崖・落下防止")]
    [Tooltip("オンなら、進行方向に深い崖がある時は手前で止まります")]
    [SerializeField] private bool avoidDeepCliffs = true;

    [Tooltip("崖の確認に使うLayer。空欄ならGround Layersを使います")]
    [SerializeField] private LayerMask cliffCheckLayers;

    [Tooltip("敵Colliderの前方へ、崖確認Rayの開始位置をどれだけずらすか")]
    [SerializeField, Min(0f)] private float cliffCheckForwardOffset = 0.08f;

    [Tooltip("足元からどれだけ上にRayを開始するか。地面へ少しめり込む場合は上げます")]
    [SerializeField, Min(0f)] private float cliffCheckStartHeight = 0.06f;

    [Tooltip("下方向へ地面を探す距離。1マス=1unitなら、5マスの崖を判定したい時は6程度がおすすめです")]
    [SerializeField, Min(0.05f)] private float cliffCheckDistance = 6f;

    [Tooltip("この高さを超えて下へ落ちる地形は、危険な崖として扱います")]
    [SerializeField, Min(0f)] private float maxSafeDropHeight = 4.5f;

    [Tooltip("オンなら、深い崖の先にいるPlayerは追跡対象から外します。")]
    [SerializeField] private bool giveUpWhenDeepCliff = true;

    [Header("追跡の再開")]
    [Tooltip("崖や越えられない壁で追跡を諦めた後でも、Playerがこの距離まで近づいたら再び追跡を試みます。0なら近距離での再追跡は行いません。")]
    [SerializeField, Min(0f)]
    private float reacquireDistanceAfterPathBlock = 4f;

    [Tooltip("オンなら、以前に止まった方向の崖・壁がなくなった時も自動で追跡を再開します。")]
    [SerializeField]
    private bool reacquireWhenPathIsClear = true;

    [Header("ジャンプ設定")]
    [Tooltip("オンなら、追跡中に壁や段差を越えるためジャンプします")]
    [SerializeField] private bool enableJump = true;

    [Tooltip("地面・足場に使うLayer。Groundを設定してください")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("前方の壁を調べるLayer。未設定ならGround Layersを使います")]
    [SerializeField] private LayerMask jumpObstacleLayers;

    [Tooltip("ジャンプ時の上向き速度")]
    [SerializeField, Min(0f)] private float jumpPower = 7f;

    [Tooltip("ジャンプ後、次にジャンプできるまでの秒数")]
    [SerializeField, Min(0f)] private float jumpCooldown = 0.65f;

    [Tooltip("足元の接地チェック距離")]
    [SerializeField, Min(0.01f)] private float groundCheckDistance = 0.08f;

    [Tooltip("敵Collider幅に対する接地チェックの幅")]
    [SerializeField, Range(0.1f, 1f)] private float groundCheckWidth = 0.85f;

    [Tooltip("前方の壁を調べる距離")]
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.18f;

    [Tooltip("Colliderの下から、壁を調べる高さの割合")]
    [SerializeField, Range(0f, 1f)] private float wallCheckHeightPercent = 0.35f;

    [Tooltip("プレイヤーが敵の上側にいる時もジャンプする")]
    [SerializeField] private bool jumpWhenPlayerIsAbove = true;

    [Tooltip("この高さ以上にプレイヤーがいる時、ジャンプ候補にします")]
    [SerializeField, Min(0f)] private float playerAboveJumpThreshold = 0.45f;

    [Header("ジャンプ失敗・壁対策")]
    [Tooltip("ジャンプ中に壁の横面へ当たった時、横移動を止める秒数。壁へ押し続けるのを防ぎます")]
    [SerializeField, Min(0f)] private float wallHitRecoveryDuration = 0.25f;

    [Tooltip("ジャンプでこれ以上前進できたら、成功したジャンプとして失敗回数をリセットします")]
    [SerializeField, Min(0f)] private float successfulJumpForwardDistance = 0.35f;

    [Tooltip("オンなら、壁に当たるジャンプが続く時は、その壁の先のPlayerを追跡対象から外します")]
    [SerializeField] private bool giveUpAfterFailedJumps = true;

    [Tooltip("何回連続で壁に当たったら追跡を諦めるか。0なら壁で追跡を諦めません")]
    [SerializeField, Min(0)] private int maxFailedJumpAttemptsBeforeGivingUp = 2;

    [Header("攻撃設定")]
    [Tooltip("オンならEnemyChaser2D自身の近接攻撃を使用します。遠距離敵ではオフ推奨です")]
    [SerializeField] private bool enableBuiltInMeleeAttack = true;

    [SerializeField, Min(0f)] private float attackDistance = 0.8f;

    [SerializeField, Min(1)] private int attackDamage = 10;

    [Tooltip("攻撃を試みる間隔。PlayerのCharacterHealth側の無敵時間も併用されます")]
    [SerializeField, Min(0.01f)] private float attackInterval = 1f;

    [Tooltip("攻撃中はその場で止まる")]
    [SerializeField] private bool stopWhileAttacking = true;

    [Header("見た目")]
    [Tooltip("未設定なら子のSpriteRendererを自動取得します")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("元Spriteが右向きならオン。左向きならオフ")]
    [SerializeField] private bool spriteFacesRightByDefault = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDetectionGizmo = true;
    [SerializeField] private bool showJumpGizmo;
    [SerializeField] private bool showCliffGizmo = true;

    public bool HasDetectedPlayer => hasDetectedPlayer;
    public Transform PlayerTransform => playerTransform;
    public CharacterHealth PlayerHealth => playerHealth;
    public EnemyCombatMovementStyle CombatMovementStyle => combatMovementStyle;
    public float MoveSpeed => moveSpeed;

    // EnemyAnimator2D が移動アニメーションを判定するために使います。
    // 攻撃距離で停止している時、被弾中、崖・壁で諦めている時は false になります。
    public bool IsActivelyMoving =>
        !IsDead() &&
        !IsHitStunned &&
        hasDetectedPlayer &&
        !isTargetBlockedByPath &&
        !isRecoveringFromWallHit &&
        Mathf.Abs(desiredHorizontalSpeed) > 0.01f;

    // Animator のSpeed値などを作りたい場合に参照できます。
    public float DesiredHorizontalSpeed => desiredHorizontalSpeed;

    // 実際にプレイヤーへ攻撃を試みた瞬間に呼ばれます。
    // EnemyAnimator2D が Attack Trigger を鳴らすために利用します。
    public event Action AttackPerformed;

    // EnemyHitReaction2D から呼ばれます。
    // 被弾中は追跡・攻撃・ジャンプによる横移動を一時停止します。
    public bool IsHitStunned => Time.time < hitStunEndTime;

    public void ApplyHitStun(float duration)
    {
        if (duration <= 0f)
        {
            return;
        }

        hitStunEndTime = Mathf.Max(
            hitStunEndTime,
            Time.time + duration
        );

        desiredHorizontalSpeed = 0f;
    }

    private Rigidbody2D enemyRigidbody;
    private Collider2D enemyCollider;
    private CharacterHealth ownHealth;

    private bool hasDetectedPlayer;
    private float nextAttackTime;
    private float nextJumpTime;
    private float desiredHorizontalSpeed;

    // 崖・越えられない壁を見つけた時の追跡停止情報
    private bool isTargetBlockedByPath;
    private float blockedTargetSide;
    private PathBlockReason pathBlockReason;

    // ジャンプ中に壁へ当たった時の復帰情報
    private bool isJumping;
    private bool registeredWallHitThisJump;
    private float jumpStartX;
    private float jumpDirection;
    private float jumpStartTime;
    private int failedJumpAttempts;

    private bool isRecoveringFromWallHit;
    private float wallRecoveryEndTime;

    // 被弾ノックバック中の追跡停止時間
    private float hitStunEndTime;

    private void Awake()
    {
        enemyRigidbody = GetComponent<Rigidbody2D>();
        enemyCollider = GetComponent<Collider2D>();
        ownHealth = GetComponent<CharacterHealth>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        FindPlayer();
    }

    private void Update()
    {
        FindPlayer();

        if (IsDead())
        {
            desiredHorizontalSpeed = 0f;
            return;
        }

        // ノックバックがEnemyChaserの移動速度で即座に打ち消されないよう、
        // 被弾直後は追跡・攻撃を止める。
        if (IsHitStunned)
        {
            desiredHorizontalSpeed = 0f;
            return;
        }

        if (!HasValidPlayer())
        {
            hasDetectedPlayer = false;
            desiredHorizontalSpeed = 0f;
            ClearPathBlock();
            return;
        }

        float horizontalDifference =
            playerTransform.position.x - transform.position.x;

        float distanceToPlayer = Vector2.Distance(
            transform.position,
            playerTransform.position
        );

        // 深い崖・越えられない壁で一度追跡を諦めても、
        // Playerが近づいたり、道が安全になった時は再び追跡を試みる。
        if (ShouldKeepGivingUp(
                horizontalDifference,
                distanceToPlayer))
        {
            hasDetectedPlayer = false;
            desiredHorizontalSpeed = 0f;
            return;
        }

        if (!hasDetectedPlayer)
        {
            hasDetectedPlayer = CanDetectPlayer(distanceToPlayer);
        }
        else if (distanceToPlayer > Mathf.Max(
                     detectionDistance,
                     loseTargetDistance
                 ))
        {
            hasDetectedPlayer = false;
        }

        if (!hasDetectedPlayer)
        {
            desiredHorizontalSpeed = 0f;
            return;
        }

        float absoluteHorizontalDifference =
            Mathf.Abs(horizontalDifference);

        bool canAttack =
            enableBuiltInMeleeAttack &&
            distanceToPlayer <= attackDistance;

        if (canAttack)
        {
            TryAttackPlayer();

            if (stopWhileAttacking)
            {
                desiredHorizontalSpeed = 0f;
                UpdateSpriteDirection(Mathf.Sign(horizontalDifference));
                return;
            }
        }

        if (isRecoveringFromWallHit)
        {
            desiredHorizontalSpeed = 0f;
            return;
        }

        float direction;
        bool movingTowardPlayer;

        if (combatMovementStyle == EnemyCombatMovementStyle.KeepDistance)
        {
            if (distanceToPlayer < keepDistanceMinimum)
            {
                // 近すぎるので後退。
                direction = -Mathf.Sign(horizontalDifference);
                movingTowardPlayer = false;
            }
            else if (distanceToPlayer > keepDistanceMaximum)
            {
                // 遠すぎるので接近。
                direction = Mathf.Sign(horizontalDifference);
                movingTowardPlayer = true;
            }
            else
            {
                // 適正距離では停止し、Player方向だけ向きます。
                desiredHorizontalSpeed = 0f;
                UpdateSpriteDirection(Mathf.Sign(horizontalDifference));
                return;
            }
        }
        else
        {
            if (absoluteHorizontalDifference <= stopDistance)
            {
                desiredHorizontalSpeed = 0f;
                UpdateSpriteDirection(Mathf.Sign(horizontalDifference));
                return;
            }

            direction = Mathf.Sign(horizontalDifference);
            movingTowardPlayer = true;
        }

        if (Mathf.Abs(direction) <= 0.01f)
        {
            desiredHorizontalSpeed = 0f;
            return;
        }

        if (avoidDeepCliffs &&
            IsDeepCliffAhead(direction, out _))
        {
            desiredHorizontalSpeed = 0f;

            // Playerへ近づいている時だけ、従来どおり追跡断念を記録します。
            // 遠距離型の後退中に崖があった場合は、その場で止まるだけです。
            if (movingTowardPlayer && giveUpWhenDeepCliff)
            {
                GiveUpTargetAtPath(direction, PathBlockReason.DeepCliff);
            }

            return;
        }

        desiredHorizontalSpeed = direction * moveSpeed;
        UpdateSpriteDirection(direction);
    }

    private void FixedUpdate()
    {
        if (enemyRigidbody == null || IsDead())
        {
            return;
        }

        // EnemyHitReaction2D が入れたノックバック速度を維持する。
        if (IsHitStunned)
        {
            return;
        }

        bool grounded = IsGrounded();
        UpdateJumpLandingState(grounded);

        Vector2 velocity = enemyRigidbody.linearVelocity;

        // 壁にジャンプで当たった直後は、Player方向へX速度を出し続けない。
        // これにより壁に張り付いたまま止まる現象を防ぎ、自然に落下させる。
        if (isRecoveringFromWallHit)
        {
            velocity.x = 0f;

            if (grounded && Time.time >= wallRecoveryEndTime)
            {
                isRecoveringFromWallHit = false;
            }

            enemyRigidbody.linearVelocity = velocity;
            return;
        }

        velocity.x = desiredHorizontalSpeed;

        if (ShouldJump(grounded))
        {
            velocity.y = jumpPower;
            nextJumpTime = Time.time + jumpCooldown;
            BeginJump();
        }

        enemyRigidbody.linearVelocity = velocity;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleJumpWallCollision(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // 高速で壁へ当たった場合にEnterを取り逃しても、Stayで安全に補足する。
        TryHandleJumpWallCollision(collision);
    }

    private void OnDisable()
    {
        hitStunEndTime = 0f;
        StopHorizontalMovement();
    }

    private bool CanDetectPlayer(float distanceToPlayer)
    {
        if (distanceToPlayer > detectionDistance)
        {
            return false;
        }

        if (!requireLineOfSight)
        {
            return true;
        }

        Vector2 origin = enemyCollider != null
            ? enemyCollider.bounds.center
            : transform.position;

        Vector2 target = playerTransform.position;
        Vector2 direction = target - origin;
        float distance = direction.magnitude;

        if (distance <= 0.001f)
        {
            return true;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            direction.normalized,
            distance,
            obstacleLayers
        );

        return hit.collider == null;
    }

    private bool ShouldJump(bool grounded)
    {
        if (!enableJump ||
            enemyCollider == null ||
            playerTransform == null ||
            isRecoveringFromWallHit ||
            Mathf.Abs(desiredHorizontalSpeed) < 0.01f ||
            Time.time < nextJumpTime ||
            !grounded)
        {
            return false;
        }

        float direction = Mathf.Sign(desiredHorizontalSpeed);

        // 崖へ降りるためのジャンプを防ぐ。
        if (avoidDeepCliffs &&
            IsDeepCliffAhead(direction, out _))
        {
            return false;
        }

        bool hasWallAhead = IsWallAhead(direction);
        bool playerIsAbove = jumpWhenPlayerIsAbove &&
            playerTransform.position.y >
            enemyCollider.bounds.max.y + playerAboveJumpThreshold;

        return hasWallAhead || playerIsAbove;
    }

    private void BeginJump()
    {
        isJumping = true;
        registeredWallHitThisJump = false;
        jumpStartX = enemyRigidbody != null
            ? enemyRigidbody.position.x
            : transform.position.x;
        jumpDirection = Mathf.Sign(desiredHorizontalSpeed);
        jumpStartTime = Time.time;
    }

    private void UpdateJumpLandingState(bool grounded)
    {
        if (!isJumping || !grounded ||
            Time.time - jumpStartTime < 0.06f)
        {
            return;
        }

        float currentX = enemyRigidbody != null
            ? enemyRigidbody.position.x
            : transform.position.x;

        float forwardProgress =
            (currentX - jumpStartX) * jumpDirection;

        // 十分に前へ進めたジャンプなら、以前の失敗回数をリセットする。
        if (forwardProgress >= successfulJumpForwardDistance)
        {
            failedJumpAttempts = 0;
        }

        isJumping = false;
        registeredWallHitThisJump = false;
    }

    private void TryHandleJumpWallCollision(Collision2D collision)
    {
        if (!isJumping ||
            registeredWallHitThisJump ||
            collision == null ||
            collision.collider == null ||
            !IsOnObstacleLayer(collision.collider.gameObject))
        {
            return;
        }

        bool hitWallSide = false;

        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (Mathf.Abs(contact.normal.x) >= 0.65f)
            {
                hitWallSide = true;
                break;
            }
        }

        if (!hitWallSide)
        {
            return;
        }

        registeredWallHitThisJump = true;
        failedJumpAttempts++;

        desiredHorizontalSpeed = 0f;
        isRecoveringFromWallHit = true;
        wallRecoveryEndTime =
            Time.time + wallHitRecoveryDuration;

        if (enemyRigidbody != null)
        {
            Vector2 velocity = enemyRigidbody.linearVelocity;
            velocity.x = 0f;
            enemyRigidbody.linearVelocity = velocity;
        }

        if (giveUpAfterFailedJumps &&
            maxFailedJumpAttemptsBeforeGivingUp > 0 &&
            failedJumpAttempts >=
            maxFailedJumpAttemptsBeforeGivingUp)
        {
            GiveUpTargetAtPath(
                jumpDirection,
                PathBlockReason.UnclimbableWall
            );
        }
    }

    private bool IsGrounded()
    {
        if (enemyCollider == null || groundLayers.value == 0)
        {
            return false;
        }

        Bounds bounds = enemyCollider.bounds;

        Vector2 center = new Vector2(
            bounds.center.x,
            bounds.min.y + 0.02f
        );

        Vector2 size = new Vector2(
            Mathf.Max(0.02f, bounds.size.x * groundCheckWidth),
            0.04f
        );

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            center,
            size,
            0f,
            Vector2.down,
            groundCheckDistance,
            groundLayers
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null ||
                hit.collider == enemyCollider ||
                hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (hit.normal.y > 0.35f ||
                hit.collider.gameObject.layer != gameObject.layer)
            {
                return true;
            }
        }

        return false;
    }

    private bool IsWallAhead(float direction)
    {
        if (enemyCollider == null || Mathf.Abs(direction) < 0.01f)
        {
            return false;
        }

        LayerMask checkLayers = jumpObstacleLayers.value != 0
            ? jumpObstacleLayers
            : groundLayers;

        if (checkLayers.value == 0)
        {
            return false;
        }

        Bounds bounds = enemyCollider.bounds;

        Vector2 origin = new Vector2(
            bounds.center.x + direction * bounds.extents.x,
            bounds.min.y +
            bounds.size.y * wallCheckHeightPercent
        );

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            Vector2.right * direction,
            wallCheckDistance,
            checkLayers
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null ||
                hit.collider == enemyCollider ||
                hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    private bool IsDeepCliffAhead(
        float direction,
        out float dropHeight)
    {
        dropHeight = 0f;

        if (enemyCollider == null ||
            Mathf.Abs(direction) < 0.01f)
        {
            return false;
        }

        LayerMask checkLayers = cliffCheckLayers.value != 0
            ? cliffCheckLayers
            : groundLayers;

        if (checkLayers.value == 0)
        {
            return false;
        }

        Bounds bounds = enemyCollider.bounds;

        Vector2 origin = new Vector2(
            bounds.center.x +
            direction * (bounds.extents.x + cliffCheckForwardOffset),
            bounds.min.y + cliffCheckStartHeight
        );

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            Vector2.down,
            cliffCheckDistance,
            checkLayers
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null ||
                hit.collider == enemyCollider ||
                hit.collider.transform.IsChildOf(transform))
            {
                continue;
            }

            // 下向きRayで床・足場の上面だけを落下先に使う。
            if (hit.normal.y < 0.2f)
            {
                continue;
            }

            dropHeight = Mathf.Max(0f, origin.y - hit.point.y);
            return dropHeight > maxSafeDropHeight;
        }

        // 指定距離内に床がない場合も、深い崖として扱う。
        dropHeight = cliffCheckDistance;
        return true;
    }

    private void GiveUpTargetAtPath(
        float targetSide,
        PathBlockReason reason)
    {
        if (Mathf.Abs(targetSide) < 0.01f)
        {
            return;
        }

        hasDetectedPlayer = false;
        desiredHorizontalSpeed = 0f;

        isTargetBlockedByPath = true;
        blockedTargetSide = Mathf.Sign(targetSide);
        pathBlockReason = reason;
    }

    private bool ShouldKeepGivingUp(
        float currentHorizontalDifference,
        float distanceToPlayer)
    {
        if (!isTargetBlockedByPath)
        {
            return false;
        }

        // すでに攻撃できるほど近い場合は、以前の経路ブロックを残さない。
        // これにより、敵のすぐ近くまで戻ってきたPlayerが永久に無視されません。
        if (distanceToPlayer <= attackDistance)
        {
            ResumeChasingBlockedTarget();
            return false;
        }

        // Inspectorで指定した距離まで近づいたら、もう一度追跡を試みる。
        if (reacquireDistanceAfterPathBlock > 0f &&
            distanceToPlayer <= reacquireDistanceAfterPathBlock)
        {
            ResumeChasingBlockedTarget();
            return false;
        }

        float currentPlayerSide = Mathf.Sign(
            currentHorizontalDifference
        );

        // Playerが反対側へ戻った場合は、以前の危険地点とは別方向なので再追跡する。
        if (Mathf.Abs(currentPlayerSide) <= 0.01f ||
            !Mathf.Approximately(
                currentPlayerSide,
                blockedTargetSide))
        {
            ResumeChasingBlockedTarget();
            return false;
        }

        // Playerが同じ側にいても、現在の進行方向に深い崖や壁がなくなったなら再追跡する。
        if (reacquireWhenPathIsClear &&
            IsBlockedPathClear(currentPlayerSide))
        {
            ResumeChasingBlockedTarget();
            return false;
        }

        return true;
    }

    private bool IsBlockedPathClear(float directionToPlayer)
    {
        if (Mathf.Abs(directionToPlayer) <= 0.01f)
        {
            return true;
        }

        switch (pathBlockReason)
        {
            case PathBlockReason.DeepCliff:
                return !avoidDeepCliffs ||
                    !IsDeepCliffAhead(directionToPlayer, out _);

            case PathBlockReason.UnclimbableWall:
                return !IsWallAhead(directionToPlayer);

            default:
                return true;
        }
    }

    private void ResumeChasingBlockedTarget()
    {
        ClearPathBlock();
        failedJumpAttempts = 0;
        isRecoveringFromWallHit = false;
    }

    private void ClearPathBlock()
    {
        isTargetBlockedByPath = false;
        blockedTargetSide = 0f;
        pathBlockReason = PathBlockReason.None;
    }

    private void TryAttackPlayer()
    {
        if (playerHealth == null ||
            playerHealth.IsDead ||
            Time.time < nextAttackTime)
        {
            return;
        }

        bool wasInvincible = playerHealth.IsInvincible;

        playerHealth.TakeDamage(attackDamage);
        nextAttackTime = Time.time + attackInterval;

        // ダメージが無敵で無効化された場合も、敵が攻撃動作をした事実は残します。
        // これにより、攻撃アニメーションとAttack Intervalが一致します。
        AttackPerformed?.Invoke();

        // ダメージが通った直後に、Player側のすり抜け処理を即時反映する。
        if (!wasInvincible && playerHealth.IsInvincible)
        {
            PlayerEnemyPassThroughOnInvincibility passThrough =
                playerHealth.GetComponent<
                    PlayerEnemyPassThroughOnInvincibility
                >();

            passThrough?.RefreshPassThroughState();
        }
    }

    private bool HasValidPlayer()
    {
        return playerTransform != null &&
               playerHealth != null &&
               !playerHealth.IsDead;
    }

    private bool IsDead()
    {
        return ownHealth != null && ownHealth.IsDead;
    }

    private bool IsOnObstacleLayer(GameObject target)
    {
        if (target == null)
        {
            return false;
        }

        LayerMask checkLayers = jumpObstacleLayers.value != 0
            ? jumpObstacleLayers
            : groundLayers;

        return (checkLayers.value &
                (1 << target.layer)) != 0;
    }

    private void FindPlayer()
    {
        if (playerTransform == null)
        {
            PlayerMove playerMove =
                FindAnyObjectByType<PlayerMove>();

            if (playerMove != null)
            {
                playerTransform = playerMove.transform;
            }
        }

        if (playerHealth == null && playerTransform != null)
        {
            playerHealth =
                playerTransform.GetComponent<CharacterHealth>();

            if (playerHealth == null)
            {
                playerHealth =
                    playerTransform.GetComponentInParent<CharacterHealth>();
            }
        }
    }

    private void UpdateSpriteDirection(float direction)
    {
        if (spriteRenderer == null || Mathf.Abs(direction) < 0.01f)
        {
            return;
        }

        bool shouldFaceRight = direction > 0f;

        spriteRenderer.flipX = spriteFacesRightByDefault
            ? !shouldFaceRight
            : shouldFaceRight;
    }

    private void StopHorizontalMovement()
    {
        if (enemyRigidbody == null)
        {
            return;
        }

        Vector2 velocity = enemyRigidbody.linearVelocity;
        velocity.x = 0f;
        enemyRigidbody.linearVelocity = velocity;
    }

    private void OnValidate()
    {
        detectionDistance = Mathf.Max(0f, detectionDistance);
        loseTargetDistance = Mathf.Max(
            detectionDistance,
            loseTargetDistance
        );

        moveSpeed = Mathf.Max(0f, moveSpeed);
        stopDistance = Mathf.Max(0f, stopDistance);
        keepDistanceMinimum = Mathf.Max(0f, keepDistanceMinimum);
        keepDistanceMaximum = Mathf.Max(
            keepDistanceMinimum,
            keepDistanceMaximum
        );

        cliffCheckForwardOffset = Mathf.Max(0f, cliffCheckForwardOffset);
        cliffCheckStartHeight = Mathf.Max(0f, cliffCheckStartHeight);
        cliffCheckDistance = Mathf.Max(0.05f, cliffCheckDistance);
        maxSafeDropHeight = Mathf.Max(0f, maxSafeDropHeight);

        jumpPower = Mathf.Max(0f, jumpPower);
        jumpCooldown = Mathf.Max(0f, jumpCooldown);
        groundCheckDistance = Mathf.Max(0.01f, groundCheckDistance);
        wallCheckDistance = Mathf.Max(0.01f, wallCheckDistance);
        playerAboveJumpThreshold = Mathf.Max(
            0f,
            playerAboveJumpThreshold
        );

        wallHitRecoveryDuration = Mathf.Max(
            0f,
            wallHitRecoveryDuration
        );

        successfulJumpForwardDistance = Mathf.Max(
            0f,
            successfulJumpForwardDistance
        );

        maxFailedJumpAttemptsBeforeGivingUp = Mathf.Max(
            0,
            maxFailedJumpAttemptsBeforeGivingUp
        );

        reacquireDistanceAfterPathBlock = Mathf.Max(
            0f,
            reacquireDistanceAfterPathBlock
        );

        attackDistance = Mathf.Max(0f, attackDistance);
        attackDamage = Mathf.Max(1, attackDamage);
        attackInterval = Mathf.Max(0.01f, attackInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (showDetectionGizmo)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectionDistance);

            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackDistance);
        }

        if (showJumpGizmo && enemyCollider != null)
        {
            Bounds bounds = enemyCollider.bounds;
            Gizmos.color = Color.blue;
            Gizmos.DrawLine(
                new Vector3(
                    bounds.max.x,
                    bounds.min.y +
                    bounds.size.y * wallCheckHeightPercent,
                    transform.position.z
                ),
                new Vector3(
                    bounds.max.x + wallCheckDistance,
                    bounds.min.y +
                    bounds.size.y * wallCheckHeightPercent,
                    transform.position.z
                )
            );
        }
        if (showCliffGizmo && enemyCollider != null)
        {
            Bounds bounds = enemyCollider.bounds;
            float previewDirection =
                Mathf.Abs(desiredHorizontalSpeed) > 0.01f
                    ? Mathf.Sign(desiredHorizontalSpeed)
                    : 1f;

            Vector3 origin = new Vector3(
                bounds.center.x + previewDirection *
                (bounds.extents.x + cliffCheckForwardOffset),
                bounds.min.y + cliffCheckStartHeight,
                transform.position.z
            );

            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(
                origin,
                origin + Vector3.down * cliffCheckDistance
            );
        }
    }
}
