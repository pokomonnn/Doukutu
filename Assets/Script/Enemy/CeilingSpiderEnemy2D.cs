using System.Text;
using UnityEngine;

/// <summary>
/// 天井へ張り付き、プレイヤーを見つけると天井の下面に沿って移動するクモ敵です。
/// 天井の段差で壁にぶつかった時は、その壁を上または下へ歩いて反対側の天井へ回り込みます。
/// プレイヤーの真上まで来ると落下し、地上では近距離攻撃を行います。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class CeilingSpiderEnemy2D : MonoBehaviour
{
    public enum SpiderState
    {
        WaitingOnCeiling,
        MovingOnCeiling,
        MovingOnWall,
        Dropping,
        GroundChasing
    }

    [Header("プレイヤー参照")]
    [Tooltip("未設定ならシーン内のPlayerMoveを自動取得します")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("未設定ならPlayerから自動取得します")]
    [SerializeField] private CharacterHealth playerHealth;

    [Header("発見・落下設定")]
    [Tooltip("この距離以内に入ったプレイヤーを見つけます")]
    [SerializeField, Min(0f)] private float detectionDistance = 10f;

    [Tooltip("プレイヤーとのX方向の差がこの値以下になったら落下します")]
    [SerializeField, Min(0f)] private float dropAlignmentDistance = 0.15f;

    [Tooltip("天井を横移動する速度")]
    [SerializeField, Min(0f)] private float ceilingMoveSpeed = 3f;

    [Tooltip("落下中に使うGravity Scale")]
    [SerializeField, Min(0.01f)] private float dropGravityScale = 2.5f;

    [Tooltip("床・天井・壁に使っているGroundレイヤーを設定してください")]
    [SerializeField] private LayerMask groundLayers;

    [Header("天井の凹凸に沿う設定")]
    [Tooltip("天井を探すRayの開始位置を、現在位置よりどれだけ下げるか。最大の段差より少し大きめにします")]
    [SerializeField, Min(0.01f)] private float ceilingProbeStartBelow = 3f;

    [Tooltip("上向きRayで天井を探す距離。最大の天井高低差より十分大きくします")]
    [SerializeField, Min(0.1f)] private float ceilingProbeDistance = 7f;

    [Tooltip("天井下面からクモのColliderまでに空けるわずかな距離")]
    [SerializeField, Min(0f)] private float ceilingContactOffset = 0.02f;

    [Tooltip("進行先に天井がなく、壁を回り込む経路もない時に落下します")]
    [SerializeField] private bool dropWhenCeilingIsLost = true;

    [Header("壁を登って段差を越える設定")]
    [Tooltip("前方の壁を調べる距離。クモの横幅より少し大きい値がおすすめです")]
    [SerializeField, Min(0.02f)] private float wallProbeDistance = 0.45f;

    [Tooltip("壁を上り下りする速度")]
    [SerializeField, Min(0f)] private float wallMoveSpeed = 2.8f;

    [Tooltip("壁の反対側にある天井を探す最大の上下距離。最大段差より大きくします")]
    [SerializeField, Min(0.1f)] private float wallRouteSearchHeight = 7f;

    [Tooltip("壁の反対側に天井があるか探す時の上下間隔。小さいほど段差に強くなります")]
    [SerializeField, Min(0.02f)] private float wallRouteSearchStep = 0.12f;

    [Tooltip("壁を回り込んで反対側の天井へ移る時の横方向の余白")]
    [SerializeField, Min(0f)] private float wallCrossOverClearance = 0.08f;

    [Tooltip("この距離まで壁の目的Yへ近づいたら、反対側の天井へ移ります")]
    [SerializeField, Min(0.001f)] private float wallArrivalDistance = 0.04f;

    [Header("落下中の地面すり抜け対策")]
    [Tooltip("オンなら、落下中に下方向のRaycastで床を先読みして着地させます。高速落下やTilemapでCollisionが抜ける時も床を通り抜けません")]
    [SerializeField] private bool preventGroundPassThrough = true;

    [Tooltip("落下中に床を探すRayの余裕距離。高速で落ちる・床を抜ける場合は少し上げます")]
    [SerializeField, Min(0.01f)] private float landingProbePadding = 0.12f;

    [Tooltip("着地時に床の上へ少しだけ浮かせる距離。めり込み・震え対策です")]
    [SerializeField, Min(0f)] private float groundSnapOffset = 0.01f;

    [Tooltip("この値以上の上向き法線を床として扱います。通常は0.35のままでOKです")]
    [SerializeField, Range(0f, 1f)] private float landingNormalMinY = 0.35f;

    [Header("落下の確実な着地設定")]
    [Tooltip("オンなら、Dynamic物理へ任せずCollider Castで1FixedUpdateごとの移動先を確認してから落下します。床を貫通しにくい推奨設定です")]
    [SerializeField] private bool useKinematicSweepDrop = true;

    [Tooltip("落下開始時に天井から少しだけ下へ離す距離。天井に接触したまま落下を始める問題を防ぎます")]
    [SerializeField, Min(0f)] private float dropDetachDistance = 0.03f;

    [Tooltip("Collider Castで床へ着地する時に残すわずかな隙間。0.005〜0.02程度がおすすめです")]
    [SerializeField, Min(0f)] private float dropCollisionSkin = 0.01f;

    [Tooltip("Kinematic落下中の最大落下速度。高速マップでの通り抜けを防ぐため上限を持たせます")]
    [SerializeField, Min(0.1f)] private float maximumDropSpeed = 25f;

    [Header("落下前に着地点を予約する設定")]
    [Tooltip("オンなら、落下開始前に真下の床を長距離Raycastで見つけます。床が見つからない場所では落下を中止するため、地面の中へ落ち続けません")]
    [SerializeField] private bool requireLandingTargetBeforeDrop = true;

    [Tooltip("落下開始時に真下の床を探す最大距離。マップの天井から床までの最大高さより大きくします")]
    [SerializeField, Min(1f)] private float landingTargetSearchDistance = 30f;

    [Tooltip("着地点へ到達したと判定する余裕距離")]
    [SerializeField, Min(0f)] private float landingTargetArrivalDistance = 0.005f;

    [Header("落下後の追跡")]
    [SerializeField, Min(0f)] private float groundMoveSpeed = 2.2f;

    [SerializeField, Min(0f)] private float groundStopDistance = 0.55f;

    [Header("地上追跡中の床抜け保険")]
    [Tooltip("オンなら、着地後もKinematicで地面の高さを追従します。Groundとの物理衝突設定に問題があっても地面の中へ落ちません")]
    [SerializeField] private bool useKinematicGroundChasing = true;

    [Tooltip("地上追跡中、次のX座標の床を探し始める高さ")]
    [SerializeField, Min(0.01f)] private float groundFollowProbeStartAbove = 1.5f;

    [Tooltip("地上追跡中、床を探す下向きRayの長さ。小さな段差・坂より大きくします")]
    [SerializeField, Min(0.1f)] private float groundFollowProbeDistance = 3f;

    [Tooltip("地上追跡で壁へめり込まないために残す隙間")]
    [SerializeField, Min(0f)] private float groundMoveCollisionSkin = 0.01f;

    [Header("攻撃設定")]
    [SerializeField, Min(0f)] private float attackDistance = 0.85f;

    [SerializeField, Min(1)] private int attackDamage = 12;

    [Tooltip("攻撃を試みる間隔。PlayerのCharacterHealth側の無敵時間も併用されます")]
    [SerializeField, Min(0.01f)] private float attackInterval = 1f;

    [Header("見た目")]
    [Tooltip("未設定なら子のSpriteRendererを自動取得します")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    [Tooltip("元Spriteが右向きならオン。左向きならオフ")]
    [SerializeField] private bool spriteFacesRightByDefault = true;

    [Header("デバッグ")]
    [Tooltip("状態の切替をConsoleへ表示します")]
    [SerializeField] private bool showStateLogs;

    [Header("落下・着地デバッグ")]
    [Tooltip("落下を開始した理由、Rigidbody設定、Ground Layersの内容をConsoleへ表示します")]
    [SerializeField] private bool showDropStartLogs = true;

    [Tooltip("落下中に床を探すRaycastの結果をConsoleへ表示します。最初はオン推奨です")]
    [SerializeField] private bool showLandingProbeLogs = true;

    [Tooltip("各Raycastの全Hitと除外理由もConsoleへ表示します。ログ量が多いので、原因調査中だけオンにしてください")]
    [SerializeField] private bool verboseLandingHitLogs;

    [Tooltip("落下中の床チェックをConsoleへ出す最短間隔。小さくすると詳しくなりますがログ量が増えます")]
    [SerializeField, Min(0.02f)] private float landingProbeLogInterval = 0.25f;

    [Tooltip("この秒数を超えても着地できない時に、1回だけWarningを出します")]
    [SerializeField, Min(0.1f)] private float dropTimeoutWarningSeconds = 2f;

    [Tooltip("落下中にColliderへ衝突した時、接触面の法線やLayerをConsoleへ表示します")]
    [SerializeField] private bool showDropCollisionLogs = true;

    [Header("Scene表示")]
    [SerializeField] private bool showDetectionGizmo = true;
    [SerializeField] private bool showCeilingProbeGizmo;
    [SerializeField] private bool showWallRouteGizmo;
    [Tooltip("最後に実行した床検出RayをScene画面に表示します")]
    [SerializeField] private bool showLandingProbeGizmo = true;

    public SpiderState CurrentState => currentState;

    private Rigidbody2D spiderRigidbody;
    private Collider2D spiderCollider;
    private CharacterHealth ownHealth;

    private SpiderState currentState;
    private RigidbodyConstraints2D originalConstraints;
    private RigidbodyType2D originalBodyType;
    private float originalGravityScale;
    private CollisionDetectionMode2D originalCollisionDetectionMode;

    private float desiredHorizontalSpeed;
    private float nextAttackTime;

    // 天井を進む向き。壁へ移った後も、反対側の天井へ戻る向きとして使います。
    private float crawlDirectionX;

    // 壁移動中に使う、壁上の目的位置と反対側天井の到達位置です。
    private float wallAttachX;
    private float wallTargetY;
    private Vector2 pendingCeilingPosition;
    private bool hasPendingCeilingRoute;

    // 落下・着地の原因を追うために、最後に使ったRaycast情報を保存します。
    private float dropStartedAtTime = -1f;
    private float nextLandingProbeLogTime;
    private bool dropTimeoutWarningLogged;
    private readonly Vector2[] lastLandingProbeOrigins = new Vector2[3];
    private float lastLandingProbeDistance;
    private RaycastHit2D lastLandingBestHit;
    private bool lastLandingProbeFoundFloor;
    private string lastLandingProbeResult = string.Empty;

    // Kinematic Collider Cast落下で使う現在の下向き速度です。
    private float kinematicDropVelocityY;
    private readonly RaycastHit2D[] kinematicDropCastHits =
        new RaycastHit2D[24];

    // 落下前に長距離Raycastで予約した、最初に着地する床です。
    // 予約できた場所だけへ落下するため、床判定を見失って
    // 地面の中まで移動し続ける問題を防ぎます。
    private bool hasReservedLandingTarget;
    private RaycastHit2D reservedLandingHit;
    private float reservedLandingBodyY;

    // Kinematic地上追跡で保持する、床の上に立つ本体中心Yです。
    private float groundChaseBodyY;
    private readonly RaycastHit2D[] groundMoveCastHits =
        new RaycastHit2D[24];

    private void Awake()
    {
        spiderRigidbody = GetComponent<Rigidbody2D>();
        spiderCollider = GetComponent<Collider2D>();
        ownHealth = GetComponent<CharacterHealth>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        }

        originalConstraints = spiderRigidbody.constraints;
        originalBodyType = spiderRigidbody.bodyType;
        originalGravityScale = spiderRigidbody.gravityScale;
        originalCollisionDetectionMode = spiderRigidbody.collisionDetectionMode;

        FindPlayer();
        AttachToCeiling();
    }

    private void Update()
    {
        FindPlayer();

        if (IsDead())
        {
            desiredHorizontalSpeed = 0f;
            return;
        }

        if (!HasValidPlayer())
        {
            desiredHorizontalSpeed = 0f;
            return;
        }

        switch (currentState)
        {
            case SpiderState.WaitingOnCeiling:
                UpdateWaitingOnCeiling();
                break;

            case SpiderState.MovingOnCeiling:
                UpdateMovingOnCeiling();
                break;

            case SpiderState.MovingOnWall:
                UpdateMovingOnWall();
                break;

            case SpiderState.Dropping:
                UpdateDropping();
                break;

            case SpiderState.GroundChasing:
                UpdateGroundChasing();
                break;
        }
    }

    private void FixedUpdate()
    {
        if (spiderRigidbody == null || IsDead())
        {
            return;
        }

        switch (currentState)
        {
            case SpiderState.WaitingOnCeiling:
                spiderRigidbody.linearVelocity = Vector2.zero;
                break;

            case SpiderState.MovingOnCeiling:
                MoveAlongCeiling();
                break;

            case SpiderState.MovingOnWall:
                MoveAlongWall();
                break;

            case SpiderState.Dropping:
                {
                    // 推奨のKinematic Sweep方式では、移動前にCollider Castで
                    // 床を確認してから位置を更新します。これにより床の中へ
                    // 入り込んでからRaycastする問題を避けられます。
                    if (useKinematicSweepDrop)
                    {
                        SimulateKinematicSweepDrop();
                        break;
                    }

                    // 旧方式。必要な場合だけInspectorでKinematic Sweepをオフにして使えます。
                    if (preventGroundPassThrough &&
                        TryLandOnGroundAhead())
                    {
                        break;
                    }

                    Vector2 velocity = spiderRigidbody.linearVelocity;
                    velocity.x = 0f;
                    spiderRigidbody.linearVelocity = velocity;
                    break;
                }

            case SpiderState.GroundChasing:
                {
                    if (useKinematicGroundChasing)
                    {
                        MoveAlongGroundKinematic();
                        break;
                    }

                    Vector2 velocity = spiderRigidbody.linearVelocity;
                    velocity.x = desiredHorizontalSpeed;
                    spiderRigidbody.linearVelocity = velocity;
                    break;
                }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (currentState != SpiderState.Dropping ||
            collision == null ||
            collision.collider == null)
        {
            return;
        }

        LogDropCollision(collision);

        if (!IsOnGroundLayer(collision.collider.gameObject))
        {
            return;
        }

        // 壁・天井の横面に当たっただけでは地上追跡へ移らず、
        // 足元を支える面へ着地した時だけ切り替えます。
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (contact.normal.y >= landingNormalMinY)
            {
                SnapToGroundAndBeginChase(contact.point.y);
                return;
            }
        }
    }

    private void OnDisable()
    {
        RestoreOriginalPhysics();
    }

    private void UpdateWaitingOnCeiling()
    {
        float distance = Vector2.Distance(
            transform.position,
            playerTransform.position
        );

        if (distance > detectionDistance)
        {
            return;
        }

        crawlDirectionX = GetHorizontalDirectionToPlayer();

        if (Mathf.Abs(crawlDirectionX) > 0.01f)
        {
            SetState(SpiderState.MovingOnCeiling);
        }
    }

    private void UpdateMovingOnCeiling()
    {
        float horizontalDifference =
            playerTransform.position.x - transform.position.x;

        if (Mathf.Abs(horizontalDifference) <=
            dropAlignmentDistance)
        {
            BeginDrop("プレイヤーの真上に到達");
        }
    }

    private void UpdateMovingOnWall()
    {
        // 壁を回り込んでいる途中にプレイヤーが反対側へ移っても、
        // いったん現在の段差を越えてから天井で向きを再計算します。
        // 壁の途中で毎フレーム向きを変えないため、張り付き状態で止まりません。
        if (!hasPendingCeilingRoute)
        {
            HandleLostWallRoute();
        }
    }

    private void MoveAlongCeiling()
    {
        if (currentState != SpiderState.MovingOnCeiling ||
            playerTransform == null)
        {
            return;
        }

        float horizontalDifference =
            playerTransform.position.x - spiderRigidbody.position.x;

        if (Mathf.Abs(horizontalDifference) <=
            dropAlignmentDistance)
        {
            BeginDrop("プレイヤーの真上に到達");
            return;
        }

        float desiredDirection = Mathf.Sign(horizontalDifference);

        if (Mathf.Abs(desiredDirection) > 0.01f)
        {
            crawlDirectionX = desiredDirection;
        }

        float moveAmount = ceilingMoveSpeed * Time.fixedDeltaTime;

        // 段差の縦壁を先に見つけて壁歩行へ切り替える。
        // 先に天井Rayだけで探すと、壁に押し付けられて止まりやすいためです。
        if (TryFindBlockingWall(
                crawlDirectionX,
                moveAmount + wallProbeDistance,
                out RaycastHit2D wallHit) &&
            TryBeginWallTraversal(wallHit, crawlDirectionX))
        {
            return;
        }

        float nextX = Mathf.MoveTowards(
            spiderRigidbody.position.x,
            playerTransform.position.x,
            moveAmount
        );

        if (!TryGetCeilingPosition(
                nextX,
                spiderRigidbody.position.y,
                out Vector2 targetPosition))
        {
            // Rayの当たり方によっては、次のFixedUpdateで壁を見つけることがあるため
            // ここでも一度だけ近距離の壁を確認します。
            if (TryFindBlockingWall(
                    crawlDirectionX,
                    wallProbeDistance + 0.02f,
                    out wallHit) &&
                TryBeginWallTraversal(wallHit, crawlDirectionX))
            {
                return;
            }

            HandleLostCeiling();
            return;
        }

        spiderRigidbody.linearVelocity = Vector2.zero;
        spiderRigidbody.position = targetPosition;
        UpdateSpriteDirection(crawlDirectionX);
    }

    private void MoveAlongWall()
    {
        if (currentState != SpiderState.MovingOnWall)
        {
            return;
        }

        if (!hasPendingCeilingRoute)
        {
            HandleLostWallRoute();
            return;
        }

        Vector2 currentPosition = spiderRigidbody.position;

        float nextY = Mathf.MoveTowards(
            currentPosition.y,
            wallTargetY,
            wallMoveSpeed * Time.fixedDeltaTime
        );

        spiderRigidbody.linearVelocity = Vector2.zero;
        spiderRigidbody.position = new Vector2(wallAttachX, nextY);
        UpdateSpriteDirection(crawlDirectionX);

        if (Mathf.Abs(nextY - wallTargetY) > wallArrivalDistance)
        {
            return;
        }

        // 壁の反対側の天井の下面へ移動します。
        // Crawl中はKinematicにしているので、段差の角で物理的に引っかかりません。
        spiderRigidbody.position = pendingCeilingPosition;
        hasPendingCeilingRoute = false;
        SetState(SpiderState.MovingOnCeiling);
    }

    /// <summary>
    /// Collider Castを使った落下です。
    /// 落下する前に「このFixedUpdateで進む距離」に床があるか確認するため、
    /// 床の中へ入ってから着地判定を行うことがありません。
    /// </summary>
    private void SimulateKinematicSweepDrop()
    {
        if (spiderRigidbody == null || spiderCollider == null)
        {
            return;
        }

        // この方式では、落下開始時に予約した床より下へは絶対に移動しません。
        // 床が見つからない場所では落下を中止するので、地面の中へ落ち続けません。
        if (requireLandingTargetBeforeDrop &&
            !hasReservedLandingTarget)
        {
            AbortDropBecauseLandingTargetWasNotFound();
            return;
        }

        float deltaTime = Time.fixedDeltaTime;
        float gravityAcceleration = Physics2D.gravity.y * dropGravityScale;

        kinematicDropVelocityY += gravityAcceleration * deltaTime;
        kinematicDropVelocityY = Mathf.Max(
            -maximumDropSpeed,
            kinematicDropVelocityY
        );

        float moveDistance = Mathf.Max(
            0f,
            -kinematicDropVelocityY * deltaTime
        );

        if (hasReservedLandingTarget)
        {
            Vector2 position = spiderRigidbody.position;
            float nextY = Mathf.Max(
                reservedLandingBodyY,
                position.y - moveDistance
            );

            spiderRigidbody.MovePosition(
                new Vector2(position.x, nextY)
            );

            lastLandingBestHit = reservedLandingHit;
            lastLandingProbeFoundFloor = true;
            lastLandingProbeResult =
                $"予約着地点へ落下中: {reservedLandingHit.collider.name} / " +
                $"targetBodyY={reservedLandingBodyY:0.000} / currentY={position.y:0.000}";

            LogReservedLandingProgress(moveDistance, nextY);

            if (nextY <= reservedLandingBodyY +
                landingTargetArrivalDistance)
            {
                spiderRigidbody.position = new Vector2(
                    position.x,
                    reservedLandingBodyY
                );

                BeginGroundChase();
            }

            return;
        }

        // 着地点予約をオフにした場合だけ、従来のCollider Cast方式を使います。
        float castDistance = Mathf.Max(
            landingProbePadding,
            moveDistance + landingProbePadding
        );

        if (TryFindGroundWithColliderCast(
                castDistance,
                out RaycastHit2D groundHit,
                out int hitCount,
                out int validGroundCount))
        {
            lastLandingBestHit = groundHit;
            lastLandingProbeFoundFloor = true;
            lastLandingProbeResult =
                $"ColliderCast着地: {groundHit.collider.name} / " +
                $"distance={groundHit.distance:0.000} / " +
                $"point={groundHit.point} / normal={groundHit.normal}";

            LogKinematicSweepCheck(
                hitCount,
                validGroundCount,
                groundHit,
                castDistance
            );

            SnapToGroundAndBeginChase(groundHit.point.y);
            return;
        }

        lastLandingBestHit = default;
        lastLandingProbeFoundFloor = false;
        lastLandingProbeResult =
            $"ColliderCast床候補なし / Hit={hitCount}, 有効床={validGroundCount}";

        LogKinematicSweepCheck(
            hitCount,
            validGroundCount,
            default,
            castDistance
        );

        Vector2 nextPosition = spiderRigidbody.position +
            Vector2.down * moveDistance;

        spiderRigidbody.MovePosition(nextPosition);
    }

    /// <summary>
    /// クモ自身のCollider形状で下方向へSweepし、床として使える面だけを返します。
    /// Raycastと違ってCollider全体を使うため、狭い床・端・タイル境界も検出しやすくなります。
    /// </summary>
    private bool TryFindGroundWithColliderCast(
        float castDistance,
        out RaycastHit2D bestHit,
        out int totalHitCount,
        out int validGroundCount)
    {
        bestHit = default;
        totalHitCount = 0;
        validGroundCount = 0;

        if (spiderCollider == null || groundLayers.value == 0)
        {
            return false;
        }

        Bounds bounds = spiderCollider.bounds;
        Vector2 center = bounds.center;
        float halfWidth = bounds.extents.x;

        lastLandingProbeOrigins[0] = center;
        lastLandingProbeOrigins[1] =
            center + Vector2.left * halfWidth * 0.6f;
        lastLandingProbeOrigins[2] =
            center + Vector2.right * halfWidth * 0.6f;
        lastLandingProbeDistance = castDistance;

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = groundLayers;
        filter.useTriggers = false;

        int hitCount = spiderCollider.Cast(
            Vector2.down,
            filter,
            kinematicDropCastHits,
            castDistance
        );

        totalHitCount = hitCount;
        bool found = false;
        float nearestDistance = float.PositiveInfinity;

        for (int index = 0; index < hitCount; index++)
        {
            RaycastHit2D hit = kinematicDropCastHits[index];

            if (!IsValidGroundHit(hit) ||
                hit.normal.y < landingNormalMinY)
            {
                continue;
            }

            validGroundCount++;

            if (hit.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hit.distance;
            bestHit = hit;
            found = true;
        }

        return found;
    }

    private void LogKinematicSweepCheck(
        int totalHitCount,
        int validGroundCount,
        RaycastHit2D bestHit,
        float castDistance)
    {
        if (!showLandingProbeLogs ||
            Time.time < nextLandingProbeLogTime ||
            spiderRigidbody == null ||
            spiderCollider == null)
        {
            return;
        }

        nextLandingProbeLogTime = Time.time + landingProbeLogInterval;

        string best = bestHit.collider == null
            ? "なし"
            : $"{bestHit.collider.name} / distance={bestHit.distance:0.000} / " +
              $"point={bestHit.point} / normal={bestHit.normal}";

        Debug.Log(
            $"[CeilingSpiderEnemy2D] {name}: 落下ColliderCast / " +
            $"position={spiderRigidbody.position} / velocityY={kinematicDropVelocityY:0.00} / " +
            $"castDistance={castDistance:0.000} / Hit={totalHitCount} / " +
            $"有効床={validGroundCount} / best={best} / 結果={lastLandingProbeResult}",
            this
        );
    }

    /// <summary>
    /// 落下中に床を先読みして、床の上へ確実に着地させます。
    /// 通常のCollisionEnterだけに頼ると、KinematicからDynamicへ切り替えた直後や
    /// 高速落下時に薄いTilemapColliderを通り抜ける場合があるための安全策です。
    /// </summary>
    private bool TryLandOnGroundAhead()
    {
        if (spiderRigidbody == null ||
            spiderCollider == null ||
            groundLayers.value == 0)
        {
            if (showLandingProbeLogs && Time.time >= nextLandingProbeLogTime)
            {
                nextLandingProbeLogTime = Time.time + landingProbeLogInterval;
                Debug.LogWarning(
                    $"[CeilingSpiderEnemy2D] {name}: 床検出を実行できません。" +
                    $" Rigidbody={spiderRigidbody != null}, Collider={spiderCollider != null}, " +
                    $"Ground Layers={GetLayerMaskNames(groundLayers)}",
                    this
                );
            }

            return false;
        }

        Bounds bounds = spiderCollider.bounds;
        float halfHeight = bounds.extents.y;
        float halfWidth = bounds.extents.x;

        float downwardVelocity = Mathf.Max(
            0f,
            -spiderRigidbody.linearVelocity.y
        );

        // 次のFixedUpdateで重力によって増える落下量も少し加算します。
        float gravityDistance = Mathf.Abs(
            Physics2D.gravity.y * spiderRigidbody.gravityScale
        ) * Time.fixedDeltaTime * Time.fixedDeltaTime;

        float castDistance = Mathf.Max(
            halfHeight + landingProbePadding,
            halfHeight +
            downwardVelocity * Time.fixedDeltaTime +
            gravityDistance +
            landingProbePadding
        );

        float horizontalInset = Mathf.Min(
            halfWidth * 0.65f,
            Mathf.Max(0.01f, halfWidth - 0.01f)
        );

        Vector2[] origins =
        {
            bounds.center,
            new Vector2(bounds.center.x - horizontalInset, bounds.center.y),
            new Vector2(bounds.center.x + horizontalInset, bounds.center.y)
        };

        for (int i = 0; i < lastLandingProbeOrigins.Length; i++)
        {
            lastLandingProbeOrigins[i] = origins[i];
        }

        lastLandingProbeDistance = castDistance;
        lastLandingBestHit = default;
        lastLandingProbeFoundFloor = false;
        lastLandingProbeResult = string.Empty;

        bool shouldLogThisCheck =
            showLandingProbeLogs &&
            Time.time >= nextLandingProbeLogTime;

        if (shouldLogThisCheck)
        {
            nextLandingProbeLogTime = Time.time + landingProbeLogInterval;
        }

        StringBuilder verboseDetails =
            verboseLandingHitLogs && shouldLogThisCheck
                ? new StringBuilder()
                : null;

        bool found = false;
        float nearestDistance = float.PositiveInfinity;
        RaycastHit2D bestHit = default;
        int totalHitCount = 0;
        int validFloorHitCount = 0;

        for (int originIndex = 0; originIndex < origins.Length; originIndex++)
        {
            Vector2 origin = origins[originIndex];

            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                Vector2.down,
                castDistance,
                groundLayers
            );

            foreach (RaycastHit2D hit in hits)
            {
                totalHitCount++;

                string rejectionReason = null;

                if (!IsValidGroundHit(hit))
                {
                    rejectionReason = "自分自身またはGround Layers外";
                }
                else if (hit.normal.y < landingNormalMinY)
                {
                    rejectionReason =
                        $"床法線ではない normalY={hit.normal.y:0.00} < {landingNormalMinY:0.00}";
                }

                if (rejectionReason != null)
                {
                    if (verboseDetails != null)
                    {
                        AppendLandingHitLog(
                            verboseDetails,
                            originIndex,
                            hit,
                            rejectionReason
                        );
                    }

                    continue;
                }

                validFloorHitCount++;

                if (verboseDetails != null)
                {
                    AppendLandingHitLog(
                        verboseDetails,
                        originIndex,
                        hit,
                        "床候補"
                    );
                }

                if (hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        if (!found)
        {
            lastLandingProbeResult =
                $"床候補なし / RayHit={totalHitCount}, 有効床={validFloorHitCount}";

            LogLandingProbeCheck(
                shouldLogThisCheck,
                totalHitCount,
                validFloorHitCount,
                null,
                verboseDetails
            );

            return false;
        }

        // 既にクモの上半分にある壁・段差を床と誤認しないよう、
        // 着地予定位置が現在位置より上になりすぎる候補は除外します。
        float landingBodyY = GetBodyYForGroundTop(bestHit.point.y);

        if (landingBodyY > spiderRigidbody.position.y +
            landingProbePadding)
        {
            lastLandingBestHit = bestHit;
            lastLandingProbeResult =
                $"床候補を除外: 着地Y={landingBodyY:0.00} が現在Y={spiderRigidbody.position.y:0.00}より上";

            LogLandingProbeCheck(
                shouldLogThisCheck,
                totalHitCount,
                validFloorHitCount,
                bestHit,
                verboseDetails
            );

            return false;
        }

        lastLandingBestHit = bestHit;
        lastLandingProbeFoundFloor = true;
        lastLandingProbeResult =
            $"着地: {bestHit.collider.name} / point={bestHit.point} / normal={bestHit.normal}";

        LogLandingProbeCheck(
            shouldLogThisCheck,
            totalHitCount,
            validFloorHitCount,
            bestHit,
            verboseDetails
        );

        SnapToGroundAndBeginChase(bestHit.point.y);
        return true;
    }

    private void SnapToGroundAndBeginChase(float groundTopY)
    {
        if (spiderRigidbody == null || spiderCollider == null)
        {
            BeginGroundChase();
            return;
        }

        Vector2 position = spiderRigidbody.position;
        position.y = GetBodyYForGroundTop(groundTopY);

        spiderRigidbody.position = position;
        spiderRigidbody.linearVelocity = Vector2.zero;
        spiderRigidbody.angularVelocity = 0f;

        BeginGroundChase();
    }

    private float GetBodyYForGroundTop(float groundTopY)
    {
        if (spiderRigidbody == null || spiderCollider == null)
        {
            return groundTopY;
        }

        Bounds bounds = spiderCollider.bounds;

        // ColliderにOffsetが付いていても、床に対して正しいYへ置けるように補正します。
        float colliderCenterOffsetY =
            bounds.center.y - spiderRigidbody.position.y;

        return groundTopY +
            bounds.extents.y +
            groundSnapOffset +
            (useKinematicSweepDrop ? dropCollisionSkin : 0f) -
            colliderCenterOffsetY;
    }

    /// <summary>
    /// 落下開始時に、クモの真下にある最初の床を長距離Raycastで予約します。
    /// 天井の下面・横壁は法線で除外し、上を向いた面だけを床として選びます。
    /// </summary>
    private bool TryReserveLandingTarget(
        out RaycastHit2D bestHit,
        out float targetBodyY)
    {
        bestHit = default;
        targetBodyY = 0f;

        if (spiderRigidbody == null ||
            spiderCollider == null ||
            groundLayers.value == 0)
        {
            return false;
        }

        Physics2D.SyncTransforms();

        Bounds bounds = spiderCollider.bounds;
        float halfWidth = bounds.extents.x;
        float horizontalInset = Mathf.Min(
            halfWidth * 0.55f,
            Mathf.Max(0.01f, halfWidth - 0.01f)
        );

        // 現在張り付いている天井を拾わないよう、Collider下端より少し下から探します。
        float originY = bounds.min.y -
            Mathf.Max(dropDetachDistance, 0.01f);

        Vector2[] origins =
        {
            new Vector2(bounds.center.x, originY),
            new Vector2(bounds.center.x - horizontalInset, originY),
            new Vector2(bounds.center.x + horizontalInset, originY)
        };

        for (int i = 0; i < lastLandingProbeOrigins.Length; i++)
        {
            lastLandingProbeOrigins[i] = origins[i];
        }

        lastLandingProbeDistance = landingTargetSearchDistance;

        bool found = false;
        float nearestDistance = float.PositiveInfinity;

        foreach (Vector2 origin in origins)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                Vector2.down,
                landingTargetSearchDistance,
                groundLayers
            );

            foreach (RaycastHit2D hit in hits)
            {
                if (!IsValidGroundHit(hit) ||
                    hit.normal.y < landingNormalMinY)
                {
                    continue;
                }

                if (hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                bestHit = hit;
                found = true;
            }
        }

        if (!found)
        {
            lastLandingBestHit = default;
            lastLandingProbeFoundFloor = false;
            lastLandingProbeResult =
                $"予約着地点なし / SearchDistance={landingTargetSearchDistance:0.0}";
            return false;
        }

        targetBodyY = GetBodyYForGroundTop(bestHit.point.y);

        bool isUsable = targetBodyY <= spiderRigidbody.position.y +
                        landingProbePadding;

        lastLandingBestHit = bestHit;
        lastLandingProbeFoundFloor = isUsable;
        lastLandingProbeResult = isUsable
            ? $"予約着地点: {bestHit.collider.name} / " +
              $"point={bestHit.point} / targetBodyY={targetBodyY:0.000}"
            : $"予約候補を除外: targetBodyY={targetBodyY:0.000} / " +
              $"currentY={spiderRigidbody.position.y:0.000}";

        // すでに床の下にいる・Rayが裏面を拾ったなどの異常候補は使いません。
        return isUsable;
    }

    private void AbortDropBecauseLandingTargetWasNotFound()
    {
        hasReservedLandingTarget = false;
        kinematicDropVelocityY = 0f;
        desiredHorizontalSpeed = 0f;
        lastLandingProbeFoundFloor = false;

        EnterSurfaceCrawlPhysics();

        if (showDropStartLogs)
        {
            Debug.LogWarning(
                $"[CeilingSpiderEnemy2D] {name}: 真下の着地点を見つけられなかったため、落下を中止しました。" +
                $" Ground Layers={GetLayerMaskNames(groundLayers)} / " +
                $"SearchDistance={landingTargetSearchDistance:0.0} / " +
                $"position={spiderRigidbody.position}",
                this
            );
        }

        dropStartedAtTime = -1f;
        SetState(SpiderState.WaitingOnCeiling);
    }

    private void LogReservedLandingProgress(
        float moveDistance,
        float nextY)
    {
        if (!showLandingProbeLogs ||
            Time.time < nextLandingProbeLogTime ||
            spiderRigidbody == null)
        {
            return;
        }

        nextLandingProbeLogTime =
            Time.time + landingProbeLogInterval;

        string targetName = reservedLandingHit.collider != null
            ? reservedLandingHit.collider.name
            : "なし";

        Debug.Log(
            $"[CeilingSpiderEnemy2D] {name}: 予約落下 / " +
            $"target={targetName} / currentY={spiderRigidbody.position.y:0.000} / " +
            $"nextY={nextY:0.000} / targetY={reservedLandingBodyY:0.000} / " +
            $"move={moveDistance:0.000}",
            this
        );
    }

    private void MoveAlongGroundKinematic()
    {
        if (spiderRigidbody == null || spiderCollider == null)
        {
            return;
        }

        Vector2 position = spiderRigidbody.position;
        float moveDistance = Mathf.Abs(
            desiredHorizontalSpeed * Time.fixedDeltaTime
        );

        float direction = Mathf.Sign(desiredHorizontalSpeed);
        float allowedMoveDistance = moveDistance;

        if (moveDistance > 0.0001f &&
            TryFindGroundChaseWall(
                direction,
                moveDistance + groundMoveCollisionSkin,
                out RaycastHit2D wallHit))
        {
            allowedMoveDistance = Mathf.Max(
                0f,
                wallHit.distance - groundMoveCollisionSkin
            );
        }

        float nextX = position.x + direction * allowedMoveDistance;
        float nextY = groundChaseBodyY;

        if (TryGetGroundFollowBodyY(
                nextX,
                groundChaseBodyY,
                out float followedY))
        {
            nextY = followedY;
            groundChaseBodyY = followedY;
        }

        spiderRigidbody.MovePosition(new Vector2(nextX, nextY));
        spiderRigidbody.linearVelocity = Vector2.zero;
    }

    private bool TryFindGroundChaseWall(
        float direction,
        float distance,
        out RaycastHit2D bestHit)
    {
        bestHit = default;

        if (spiderCollider == null ||
            groundLayers.value == 0 ||
            Mathf.Abs(direction) < 0.01f)
        {
            return false;
        }

        ContactFilter2D filter = new ContactFilter2D();
        filter.useLayerMask = true;
        filter.layerMask = groundLayers;
        filter.useTriggers = false;

        int hitCount = spiderCollider.Cast(
            new Vector2(Mathf.Sign(direction), 0f),
            filter,
            groundMoveCastHits,
            Mathf.Max(0.01f, distance)
        );

        bool found = false;
        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hitCount; i++)
        {
            RaycastHit2D hit = groundMoveCastHits[i];

            if (!IsValidGroundHit(hit) ||
                Mathf.Abs(hit.normal.x) < 0.55f ||
                hit.normal.x * direction > -0.2f)
            {
                continue;
            }

            if (hit.distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = hit.distance;
            bestHit = hit;
            found = true;
        }

        return found;
    }

    private bool TryGetGroundFollowBodyY(
        float targetX,
        float preferredBodyY,
        out float bodyY)
    {
        bodyY = preferredBodyY;

        if (spiderCollider == null ||
            groundLayers.value == 0)
        {
            return false;
        }

        Bounds bounds = spiderCollider.bounds;
        float colliderCenterOffsetY =
            bounds.center.y - spiderRigidbody.position.y;

        float originY = preferredBodyY +
            colliderCenterOffsetY +
            groundFollowProbeStartAbove;

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            new Vector2(targetX, originY),
            Vector2.down,
            groundFollowProbeStartAbove +
            groundFollowProbeDistance,
            groundLayers
        );

        bool found = false;
        float bestDistance = float.PositiveInfinity;
        RaycastHit2D bestHit = default;

        foreach (RaycastHit2D hit in hits)
        {
            if (!IsValidGroundHit(hit) ||
                hit.normal.y < landingNormalMinY)
            {
                continue;
            }

            // 現在地から極端に下にある床へ、一気に吸い付かないようにします。
            float candidateBodyY = GetBodyYForGroundTop(hit.point.y);

            if (Mathf.Abs(candidateBodyY - preferredBodyY) >
                groundFollowProbeDistance)
            {
                continue;
            }

            if (hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            bestHit = hit;
            found = true;
        }

        if (!found)
        {
            return false;
        }

        bodyY = GetBodyYForGroundTop(bestHit.point.y);
        return true;
    }

    private void UpdateDropping()
    {
        LogDropTimeoutIfNeeded();

        if (IsPlayerWithinAttackDistance())
        {
            TryAttackPlayer();
        }
    }

    private void UpdateGroundChasing()
    {
        if (IsPlayerWithinAttackDistance())
        {
            desiredHorizontalSpeed = 0f;
            TryAttackPlayer();
            return;
        }

        float horizontalDifference =
            playerTransform.position.x - transform.position.x;

        if (Mathf.Abs(horizontalDifference) <= groundStopDistance)
        {
            desiredHorizontalSpeed = 0f;
            return;
        }

        float direction = Mathf.Sign(horizontalDifference);
        desiredHorizontalSpeed = direction * groundMoveSpeed;
        UpdateSpriteDirection(direction);
    }

    private void BeginDrop(string reason)
    {
        if (currentState == SpiderState.Dropping ||
            currentState == SpiderState.GroundChasing)
        {
            return;
        }

        hasPendingCeilingRoute = false;
        desiredHorizontalSpeed = 0f;

        if (useKinematicSweepDrop)
        {
            // 動的物理ではなく、Collider Castで移動先を先に確認する落下です。
            // 天井に接触したままDynamicへ切り替わると、天井の下面との接触が
            // 最初のCollisionになることがあるため、少しだけ下へ離して開始します。
            spiderRigidbody.bodyType = RigidbodyType2D.Kinematic;
            spiderRigidbody.constraints =
                (originalConstraints &
                 ~RigidbodyConstraints2D.FreezePositionY) |
                RigidbodyConstraints2D.FreezeRotation;
            spiderRigidbody.gravityScale = 0f;
            spiderRigidbody.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;
            spiderRigidbody.linearVelocity = Vector2.zero;
            spiderRigidbody.angularVelocity = 0f;
            kinematicDropVelocityY = 0f;
            hasReservedLandingTarget = false;
            reservedLandingHit = default;
            reservedLandingBodyY = 0f;

            if (dropDetachDistance > 0f)
            {
                spiderRigidbody.position +=
                    Vector2.down * dropDetachDistance;
            }

            Physics2D.SyncTransforms();

            if (requireLandingTargetBeforeDrop)
            {
                if (!TryReserveLandingTarget(
                        out reservedLandingHit,
                        out reservedLandingBodyY))
                {
                    AbortDropBecauseLandingTargetWasNotFound();
                    return;
                }

                hasReservedLandingTarget = true;
            }
        }
        else
        {
            spiderRigidbody.bodyType = RigidbodyType2D.Dynamic;
            spiderRigidbody.constraints =
                (originalConstraints &
                 ~RigidbodyConstraints2D.FreezePositionY) |
                RigidbodyConstraints2D.FreezeRotation;

            spiderRigidbody.gravityScale = dropGravityScale;
            spiderRigidbody.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;
            spiderRigidbody.linearVelocity = Vector2.zero;
            spiderRigidbody.angularVelocity = 0f;
            kinematicDropVelocityY = 0f;
            hasReservedLandingTarget = false;
            reservedLandingHit = default;
            reservedLandingBodyY = 0f;
        }

        dropStartedAtTime = Time.time;
        nextLandingProbeLogTime = Time.time;
        dropTimeoutWarningLogged = false;
        lastLandingProbeFoundFloor = false;
        lastLandingProbeResult = string.Empty;

        if (showDropStartLogs)
        {
            Debug.Log(
                $"[CeilingSpiderEnemy2D] {name}: 落下開始。理由={reason} / " +
                $"position={spiderRigidbody.position} / bodyType={spiderRigidbody.bodyType} / " +
                $"gravityScale={spiderRigidbody.gravityScale:0.00} / " +
                $"collision={spiderRigidbody.collisionDetectionMode} / " +
                $"dropMode={(useKinematicSweepDrop ? "Kinematic ColliderCast" : "Dynamic Physics")} / " +
                $"Ground Layers={GetLayerMaskNames(groundLayers)} / " +
                $"ColliderIsTrigger={spiderCollider.isTrigger}",
                this
            );
        }

        SetState(SpiderState.Dropping);
    }

    private void BeginGroundChase()
    {
        if (useKinematicGroundChasing)
        {
            spiderRigidbody.bodyType = RigidbodyType2D.Kinematic;
            spiderRigidbody.constraints =
                (originalConstraints &
                 ~RigidbodyConstraints2D.FreezePositionY) |
                RigidbodyConstraints2D.FreezeRotation;
            spiderRigidbody.gravityScale = 0f;
            spiderRigidbody.collisionDetectionMode =
                CollisionDetectionMode2D.Continuous;
            spiderRigidbody.linearVelocity = Vector2.zero;
            spiderRigidbody.angularVelocity = 0f;
            groundChaseBodyY = spiderRigidbody.position.y;
        }
        else
        {
            spiderRigidbody.bodyType = RigidbodyType2D.Dynamic;
            spiderRigidbody.constraints =
                (originalConstraints &
                 ~RigidbodyConstraints2D.FreezePositionY) |
                RigidbodyConstraints2D.FreezeRotation;

            spiderRigidbody.gravityScale = originalGravityScale;
            spiderRigidbody.collisionDetectionMode =
                originalCollisionDetectionMode;
        }

        desiredHorizontalSpeed = 0f;
        kinematicDropVelocityY = 0f;
        hasReservedLandingTarget = false;

        if (showDropStartLogs && dropStartedAtTime >= 0f)
        {
            Debug.Log(
                $"[CeilingSpiderEnemy2D] {name}: 着地して地上追跡へ移行。" +
                $"落下時間={Time.time - dropStartedAtTime:0.00}s / " +
                $"position={spiderRigidbody.position} / " +
                $"groundMode={(useKinematicGroundChasing ? "Kinematic Ground Follow" : "Dynamic Physics")} / " +
                $"最後の床判定={lastLandingProbeResult}",
                this
            );
        }

        dropStartedAtTime = -1f;
        SetState(SpiderState.GroundChasing);
    }

    private void AttachToCeiling()
    {
        if (spiderRigidbody == null)
        {
            return;
        }

        EnterSurfaceCrawlPhysics();

        // 初期配置が少しずれていても、真上の天井下面へ合わせます。
        if (TryGetCeilingPosition(
                spiderRigidbody.position.x,
                spiderRigidbody.position.y,
                out Vector2 ceilingPosition))
        {
            spiderRigidbody.position = ceilingPosition;
        }

        SetState(SpiderState.WaitingOnCeiling);
    }

    private void EnterSurfaceCrawlPhysics()
    {
        // Kinematic中は壁・天井の角で物理的に押し返されません。
        // 接地位置はRaycastで求めるため、段差の壁を確実に上り下りできます。
        spiderRigidbody.bodyType = RigidbodyType2D.Kinematic;
        spiderRigidbody.gravityScale = 0f;
        spiderRigidbody.constraints =
            (originalConstraints &
             ~RigidbodyConstraints2D.FreezePositionY) |
            RigidbodyConstraints2D.FreezeRotation;

        spiderRigidbody.linearVelocity = Vector2.zero;
        spiderRigidbody.angularVelocity = 0f;
    }

    /// <summary>
    /// 指定X座標で、近い天井の下面を探してクモ本体の中心位置を返します。
    /// 天井の上下凹凸には、このRaycastで追従します。
    /// </summary>
    private bool TryGetCeilingPosition(
        float targetX,
        float preferredY,
        out Vector2 targetPosition)
    {
        targetPosition = spiderRigidbody != null
            ? spiderRigidbody.position
            : (Vector2)transform.position;

        if (spiderRigidbody == null ||
            spiderCollider == null ||
            groundLayers.value == 0)
        {
            return false;
        }

        Vector2 origin = new Vector2(
            targetX,
            preferredY - ceilingProbeStartBelow
        );

        RaycastHit2D[] hits = Physics2D.RaycastAll(
            origin,
            Vector2.up,
            ceilingProbeDistance,
            groundLayers
        );

        bool found = false;
        float bestScore = float.PositiveInfinity;
        float halfHeight = spiderCollider.bounds.extents.y;

        foreach (RaycastHit2D hit in hits)
        {
            if (!IsValidGroundHit(hit))
            {
                continue;
            }

            // 上向きRayが天井の下面に当たった時は、法線が下向きです。
            // 横壁や床の上面を天井扱いしないために確認します。
            if (hit.normal.y > -0.1f)
            {
                continue;
            }

            Vector2 candidatePosition = new Vector2(
                targetX,
                hit.point.y - halfHeight - ceilingContactOffset
            );

            float score = Mathf.Abs(
                candidatePosition.y - preferredY
            );

            if (score >= bestScore)
            {
                continue;
            }

            bestScore = score;
            targetPosition = candidatePosition;
            found = true;
        }

        return found;
    }

    /// <summary>
    /// 進行方向にある縦壁を探します。天井の段差で壁にぶつかる直前に使います。
    /// </summary>
    private bool TryFindBlockingWall(
        float direction,
        float distance,
        out RaycastHit2D wallHit)
    {
        wallHit = default;

        if (spiderRigidbody == null ||
            spiderCollider == null ||
            groundLayers.value == 0 ||
            Mathf.Abs(direction) < 0.01f)
        {
            return false;
        }

        Bounds bounds = spiderCollider.bounds;
        float verticalOffset = Mathf.Max(
            0.01f,
            bounds.extents.y * 0.78f
        );

        Vector2 center = bounds.center;

        Vector2[] origins =
        {
            center,
            center + Vector2.up * verticalOffset,
            center - Vector2.up * verticalOffset
        };

        bool found = false;
        float nearestDistance = float.PositiveInfinity;
        Vector2 rayDirection = new Vector2(
            Mathf.Sign(direction),
            0f
        );

        foreach (Vector2 origin in origins)
        {
            RaycastHit2D[] hits = Physics2D.RaycastAll(
                origin,
                rayDirection,
                Mathf.Max(0.02f, distance),
                groundLayers
            );

            foreach (RaycastHit2D hit in hits)
            {
                if (!IsValidGroundHit(hit))
                {
                    continue;
                }

                // 進行方向へ立ちはだかる縦面だけを壁として扱います。
                if (Mathf.Abs(hit.normal.x) < 0.55f ||
                    hit.normal.x * rayDirection.x > -0.2f)
                {
                    continue;
                }

                if (hit.distance >= nearestDistance)
                {
                    continue;
                }

                nearestDistance = hit.distance;
                wallHit = hit;
                found = true;
            }
        }

        return found;
    }

    /// <summary>
    /// 段差の壁を上り下りして、反対側の天井へ出る経路を探します。
    /// </summary>
    private bool TryBeginWallTraversal(
        RaycastHit2D wallHit,
        float horizontalDirection)
    {
        if (!IsValidGroundHit(wallHit) ||
            Mathf.Abs(horizontalDirection) < 0.01f)
        {
            return false;
        }

        float direction = Mathf.Sign(horizontalDirection);
        float halfWidth = spiderCollider.bounds.extents.x;

        // 壁のこちら側に一度寄せ、そこから上下へ歩きます。
        wallAttachX = wallHit.point.x +
            wallHit.normal.x *
            (halfWidth + ceilingContactOffset);

        // 壁の向こう側で天井を探します。
        float farSideX = wallHit.point.x +
            direction *
            (halfWidth + wallCrossOverClearance);

        if (!TryFindCeilingRouteAcrossWall(
                farSideX,
                spiderRigidbody.position.y,
                out Vector2 ceilingRoute))
        {
            return false;
        }

        pendingCeilingPosition = ceilingRoute;
        wallTargetY = ceilingRoute.y;
        hasPendingCeilingRoute = true;

        // 進行方向は壁を越えた後もそのまま使います。
        crawlDirectionX = direction;

        Vector2 currentPosition = spiderRigidbody.position;
        spiderRigidbody.position = new Vector2(
            wallAttachX,
            currentPosition.y
        );
        spiderRigidbody.linearVelocity = Vector2.zero;

        SetState(SpiderState.MovingOnWall);
        return true;
    }

    /// <summary>
    /// 壁の反対側にある天井を、現在位置より上・下の近い順に探します。
    /// これにより上向き段差と下向き段差の両方へ対応します。
    /// </summary>
    private bool TryFindCeilingRouteAcrossWall(
        float farSideX,
        float fromY,
        out Vector2 ceilingRoute)
    {
        ceilingRoute = Vector2.zero;

        if (wallRouteSearchHeight <= 0f ||
            wallRouteSearchStep <= 0f)
        {
            return false;
        }

        int stepCount = Mathf.CeilToInt(
            wallRouteSearchHeight / wallRouteSearchStep
        );

        // まず同じ高さ、次に上と下を交互に確認します。
        // 最も近い段差へ回り込むため、不要に長い壁を歩きません。
        for (int index = 0; index <= stepCount; index++)
        {
            float offset = index * wallRouteSearchStep;

            if (TryFindCeilingRouteCandidate(
                    farSideX,
                    fromY + offset,
                    out ceilingRoute))
            {
                return true;
            }

            if (index == 0)
            {
                continue;
            }

            if (TryFindCeilingRouteCandidate(
                    farSideX,
                    fromY - offset,
                    out ceilingRoute))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindCeilingRouteCandidate(
        float farSideX,
        float expectedY,
        out Vector2 ceilingRoute)
    {
        if (!TryGetCeilingPosition(
                farSideX,
                expectedY,
                out ceilingRoute))
        {
            return false;
        }

        // Raycastの長さが長いため、探索中の高さから大きく外れた天井は
        // この段階では候補にしません。近い段差を正しく選ぶための制限です。
        float allowedDifference =
            wallRouteSearchStep * 1.75f + wallArrivalDistance;

        return Mathf.Abs(ceilingRoute.y - expectedY) <=
               allowedDifference;
    }

    private void HandleLostCeiling()
    {
        if (dropWhenCeilingIsLost)
        {
            BeginDrop("天井または壁の経路を見失った");
            return;
        }

        SetState(SpiderState.WaitingOnCeiling);
    }

    private void HandleLostWallRoute()
    {
        hasPendingCeilingRoute = false;

        if (dropWhenCeilingIsLost)
        {
            BeginDrop("天井または壁の経路を見失った");
            return;
        }

        SetState(SpiderState.WaitingOnCeiling);
    }

    private bool IsPlayerWithinAttackDistance()
    {
        return playerTransform != null &&
               Vector2.Distance(
                   transform.position,
                   playerTransform.position
               ) <= attackDistance;
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

    private float GetHorizontalDirectionToPlayer()
    {
        if (playerTransform == null)
        {
            return 0f;
        }

        float difference =
            playerTransform.position.x - transform.position.x;

        return Mathf.Abs(difference) <= 0.01f
            ? 0f
            : Mathf.Sign(difference);
    }

    private bool IsValidGroundHit(RaycastHit2D hit)
    {
        if (hit.collider == null ||
            hit.collider == spiderCollider)
        {
            return false;
        }

        if (hit.collider.transform == transform ||
            hit.collider.transform.IsChildOf(transform))
        {
            return false;
        }

        return IsOnGroundLayer(hit.collider.gameObject);
    }

    private bool IsOnGroundLayer(GameObject target)
    {
        return target != null &&
            (groundLayers.value & (1 << target.layer)) != 0;
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

    private void RestoreOriginalPhysics()
    {
        if (spiderRigidbody == null)
        {
            return;
        }

        spiderRigidbody.bodyType = originalBodyType;
        spiderRigidbody.constraints = originalConstraints;
        spiderRigidbody.gravityScale = originalGravityScale;
        spiderRigidbody.collisionDetectionMode =
            originalCollisionDetectionMode;
        kinematicDropVelocityY = 0f;
        hasReservedLandingTarget = false;
        reservedLandingHit = default;
        reservedLandingBodyY = 0f;
    }

    private void SetState(SpiderState nextState)
    {
        if (currentState == nextState)
        {
            return;
        }

        SpiderState previousState = currentState;
        currentState = nextState;

        if (showStateLogs)
        {
            Debug.Log(
                $"[CeilingSpiderEnemy2D] {name}: " +
                $"{previousState} → {currentState} / " +
                $"position={transform.position}",
                this
            );
        }
    }

    private void LogDropTimeoutIfNeeded()
    {
        if (!showLandingProbeLogs ||
            dropTimeoutWarningLogged ||
            dropStartedAtTime < 0f ||
            Time.time - dropStartedAtTime < dropTimeoutWarningSeconds ||
            spiderRigidbody == null)
        {
            return;
        }

        dropTimeoutWarningLogged = true;

        Debug.LogWarning(
            $"[CeilingSpiderEnemy2D] {name}: 落下開始から " +
            $"{Time.time - dropStartedAtTime:0.00}秒経過しても着地していません。" +
            $" position={spiderRigidbody.position} / velocity={spiderRigidbody.linearVelocity} / " +
            $"Ground Layers={GetLayerMaskNames(groundLayers)} / " +
            $"最後の床判定={lastLandingProbeResult}",
            this
        );
    }

    private void LogLandingProbeCheck(
        bool shouldLog,
        int totalHitCount,
        int validFloorHitCount,
        RaycastHit2D? bestHit,
        StringBuilder verboseDetails)
    {
        if (!shouldLog || spiderRigidbody == null || spiderCollider == null)
        {
            return;
        }

        string bestHitText = "なし";

        if (bestHit.HasValue && bestHit.Value.collider != null)
        {
            RaycastHit2D hit = bestHit.Value;
            bestHitText =
                $"{hit.collider.name} (layer={LayerMask.LayerToName(hit.collider.gameObject.layer)}) " +
                $"distance={hit.distance:0.000}, point={hit.point}, normal={hit.normal}";
        }

        string detailedHits = verboseDetails != null &&
                              verboseDetails.Length > 0
            ? $"\n{verboseDetails}"
            : string.Empty;

        Debug.Log(
            $"[CeilingSpiderEnemy2D] {name}: 落下床Raycast / " +
            $"position={spiderRigidbody.position} / velocity={spiderRigidbody.linearVelocity} / " +
            $"boundsBottom={spiderCollider.bounds.min.y:0.000} / castDistance={lastLandingProbeDistance:0.000} / " +
            $"RayHit={totalHitCount} / 有効床={validFloorHitCount} / " +
            $"best={bestHitText} / 結果={lastLandingProbeResult}" +
            detailedHits,
            this
        );
    }

    private void AppendLandingHitLog(
        StringBuilder builder,
        int originIndex,
        RaycastHit2D hit,
        string result)
    {
        if (builder == null)
        {
            return;
        }

        string colliderName = hit.collider != null
            ? hit.collider.name
            : "null";

        string layerName = hit.collider != null
            ? LayerMask.LayerToName(hit.collider.gameObject.layer)
            : "-";

        builder.Append("  Ray[")
            .Append(originIndex)
            .Append("] ")
            .Append(colliderName)
            .Append(" layer=")
            .Append(layerName)
            .Append(" distance=")
            .Append(hit.distance.ToString("0.000"))
            .Append(" point=")
            .Append(hit.point)
            .Append(" normal=")
            .Append(hit.normal)
            .Append(" → ")
            .Append(result)
            .Append('\n');
    }

    private void LogDropCollision(Collision2D collision)
    {
        if (!showDropCollisionLogs || collision == null || collision.collider == null)
        {
            return;
        }

        StringBuilder contacts = new StringBuilder();

        foreach (ContactPoint2D contact in collision.contacts)
        {
            contacts.Append(" point=")
                .Append(contact.point)
                .Append(" normal=")
                .Append(contact.normal);
        }

        Debug.Log(
            $"[CeilingSpiderEnemy2D] {name}: 落下中Collision。" +
            $"相手={collision.collider.name} / " +
            $"layer={LayerMask.LayerToName(collision.collider.gameObject.layer)} / " +
            $"Ground判定={IsOnGroundLayer(collision.collider.gameObject)} / " +
            $"contacts:{contacts}",
            this
        );
    }

    [ContextMenu("Log Spider Landing Diagnostics")]
    private void LogSpiderLandingDiagnostics()
    {
        string bodyInfo = spiderRigidbody != null
            ? $"bodyType={spiderRigidbody.bodyType}, gravity={spiderRigidbody.gravityScale:0.00}, " +
              $"velocity={spiderRigidbody.linearVelocity}, collision={spiderRigidbody.collisionDetectionMode}"
            : "Rigidbody2D未取得";

        string colliderInfo = spiderCollider != null
            ? $"Collider={spiderCollider.GetType().Name}, IsTrigger={spiderCollider.isTrigger}, bounds={spiderCollider.bounds}"
            : "Collider2D未取得";

        Debug.Log(
            $"[CeilingSpiderEnemy2D] {name}: 診断情報 / " +
            $"state={currentState} / {bodyInfo} / {colliderInfo} / " +
            $"Ground Layers={GetLayerMaskNames(groundLayers)} / " +
            $"preventGroundPassThrough={preventGroundPassThrough} / " +
            $"lastLanding={lastLandingProbeResult}",
            this
        );
    }

    private static string GetLayerMaskNames(LayerMask layerMask)
    {
        if (layerMask.value == 0)
        {
            return "Nothing (0)";
        }

        StringBuilder builder = new StringBuilder();

        for (int layer = 0; layer < 32; layer++)
        {
            if ((layerMask.value & (1 << layer)) == 0)
            {
                continue;
            }

            string layerName = LayerMask.LayerToName(layer);

            if (string.IsNullOrWhiteSpace(layerName))
            {
                layerName = $"Layer {layer}";
            }

            if (builder.Length > 0)
            {
                builder.Append(", ");
            }

            builder.Append(layerName);
        }

        return builder.ToString();
    }

    private void OnValidate()
    {
        detectionDistance = Mathf.Max(0f, detectionDistance);
        dropAlignmentDistance = Mathf.Max(0f, dropAlignmentDistance);
        ceilingMoveSpeed = Mathf.Max(0f, ceilingMoveSpeed);
        dropGravityScale = Mathf.Max(0.01f, dropGravityScale);
        ceilingProbeStartBelow = Mathf.Max(0.01f, ceilingProbeStartBelow);
        ceilingProbeDistance = Mathf.Max(0.1f, ceilingProbeDistance);
        ceilingContactOffset = Mathf.Max(0f, ceilingContactOffset);
        wallProbeDistance = Mathf.Max(0.02f, wallProbeDistance);
        wallMoveSpeed = Mathf.Max(0f, wallMoveSpeed);
        wallRouteSearchHeight = Mathf.Max(0.1f, wallRouteSearchHeight);
        wallRouteSearchStep = Mathf.Max(0.02f, wallRouteSearchStep);
        wallCrossOverClearance = Mathf.Max(0f, wallCrossOverClearance);
        wallArrivalDistance = Mathf.Max(0.001f, wallArrivalDistance);
        landingProbePadding = Mathf.Max(0.01f, landingProbePadding);
        groundSnapOffset = Mathf.Max(0f, groundSnapOffset);
        landingNormalMinY = Mathf.Clamp01(landingNormalMinY);
        dropDetachDistance = Mathf.Max(0f, dropDetachDistance);
        dropCollisionSkin = Mathf.Max(0f, dropCollisionSkin);
        maximumDropSpeed = Mathf.Max(0.1f, maximumDropSpeed);
        landingProbeLogInterval = Mathf.Max(0.02f, landingProbeLogInterval);
        dropTimeoutWarningSeconds = Mathf.Max(0.1f, dropTimeoutWarningSeconds);
        landingTargetSearchDistance = Mathf.Max(1f, landingTargetSearchDistance);
        landingTargetArrivalDistance = Mathf.Max(0f, landingTargetArrivalDistance);
        groundFollowProbeStartAbove = Mathf.Max(0.01f, groundFollowProbeStartAbove);
        groundFollowProbeDistance = Mathf.Max(0.1f, groundFollowProbeDistance);
        groundMoveCollisionSkin = Mathf.Max(0f, groundMoveCollisionSkin);
        groundMoveSpeed = Mathf.Max(0f, groundMoveSpeed);
        groundStopDistance = Mathf.Max(0f, groundStopDistance);
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

        if (showCeilingProbeGizmo)
        {
            Gizmos.color = Color.cyan;
            Vector3 origin = transform.position +
                Vector3.down * ceilingProbeStartBelow;

            Gizmos.DrawLine(
                origin,
                origin + Vector3.up * ceilingProbeDistance
            );
        }

        if (showWallRouteGizmo && hasPendingCeilingRoute)
        {
            Gizmos.color = Color.magenta;
            Gizmos.DrawLine(
                new Vector3(wallAttachX, transform.position.y, 0f),
                new Vector3(wallAttachX, wallTargetY, 0f)
            );

            Gizmos.DrawSphere(pendingCeilingPosition, 0.08f);
        }

        if (showLandingProbeGizmo && lastLandingProbeDistance > 0f)
        {
            Gizmos.color = lastLandingProbeFoundFloor
                ? Color.green
                : Color.red;

            foreach (Vector2 origin in lastLandingProbeOrigins)
            {
                Gizmos.DrawLine(
                    origin,
                    origin + Vector2.down * lastLandingProbeDistance
                );
            }

            if (lastLandingBestHit.collider != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawSphere(lastLandingBestHit.point, 0.07f);
            }
        }
    }
}
