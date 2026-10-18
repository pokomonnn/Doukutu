using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 敵・NPC共通の通常行動。EnemyChaser2Dが有効な場合は移動指示だけを返し、
/// 移動自体はEnemyChaser2Dが一元管理します。
/// EnemyChaser2Dが無いNPCでは、このコンポーネントが横方向の移動を担当します。
/// </summary>
public enum CharacterWanderMode2D { Idle, RandomWander, Patrol }
public enum CharacterWanderState2D { Waiting, Wandering, Patrolling, Returning, Combat, Carried, Dead, Investigating, Searching, Fleeing, Cowering }

[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public sealed class CharacterWander2D : MonoBehaviour
{
    [Header("通常行動")]
    [SerializeField] private CharacterWanderMode2D mode = CharacterWanderMode2D.RandomWander;
    [SerializeField, Min(0.1f)] private float moveSpeed = 1.2f;
    [SerializeField, Min(0.1f)] private float wanderRadius = 4f;
    [SerializeField, Min(0f)] private float minWaitSeconds = 1f;
    [SerializeField, Min(0f)] private float maxWaitSeconds = 3f;
    [SerializeField, Min(0.05f)] private float arrivalDistance = 0.25f;
    [SerializeField] private bool returnHomeAfterCombat = true;

    [Header("巡回：同じPrefab内の子Transformを並べて登録")]
    [SerializeField] private List<Transform> patrolPoints = new List<Transform>();
    [SerializeField] private bool pingPongPatrol;

    [Header("単独移動NPC用の地形安全設定")]
    [Tooltip("EnemyChaser2Dが有効なら、その崖・壁判定を優先します")]
    [SerializeField] private bool avoidDeepCliffs = true;
    [SerializeField] private LayerMask groundLayers;
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField, Min(.05f)] private float forwardProbe = .14f;
    [SerializeField, Min(.1f)] private float cliffProbeDistance = 6f;
    [SerializeField, Min(0f)] private float maxSafeDrop = 4.5f;
    [SerializeField] private bool turnAroundAtWalls = true;

    [Header("派閥戦闘：FactionGuardCombat2Dを使うNPC")]
    [Tooltip("移動しないGuardに徘徊AIを追加した際、発見した敵対相手へ攻撃距離まで近づきます。")]
    [SerializeField] private bool approachHostilesWithGuard = true;
    [Tooltip("Guard近接NPCの敵対対象への接近速度。通常の徘徊速度とは別に調整できます。")]
    [SerializeField, Min(.1f)] private float guardApproachSpeed = 1.6f;

    [Header("運搬NPC用")]
    [Tooltip("下ろされた場所を新しい徘徊の中心にします。救出NPCではON推奨")]
    [SerializeField] private bool useDropPositionAsHome = true;
    [SerializeField, Min(0f)] private float waitAfterDrop = 2f;

    [Header("表示・デバッグ")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool spriteFacesRightByDefault = true;
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool printStateChanges;

    private Rigidbody2D body;
    private Collider2D bodyCollider;
    private CharacterHealth health;
    private CarryableObject2D carry;
    private EnemyChaser2D chaser;
    private FactionGuardCombat2D guard;
    private FactionEnemyTargeting2D targeting;
    private CharacterAIInvestigation2D investigationAI;
    private CharacterAIFearFlee2D fearAI;
    private CharacterAIPersonality2D personalityAI;

    private Vector2 home;
    private bool homeCaptured;
    private float destinationX;
    private bool hasDestination;
    private float waitUntil;
    private float blockedUntil;
    private float lastRequestedSpeed;
    private bool combatActive;
    private bool wasCarried;
    private float resumeFromCarryAt;
    private int patrolIndex;
    private int patrolStep = 1;
    private int consecutiveReturnBlocks;
    private CharacterWanderState2D state = CharacterWanderState2D.Waiting;

    public CharacterWanderMode2D Mode => mode;
    public CharacterWanderState2D CurrentState => state;
    public bool IsActivelyMoving => isActiveAndEnabled &&
        (state == CharacterWanderState2D.Wandering ||
         state == CharacterWanderState2D.Patrolling ||
         state == CharacterWanderState2D.Returning ||
         state == CharacterWanderState2D.Investigating ||
         state == CharacterWanderState2D.Searching ||
         state == CharacterWanderState2D.Fleeing) &&
        Mathf.Abs(lastRequestedSpeed) > .01f;
    public float CurrentDesiredSpeed => lastRequestedSpeed;
    public float MoveSpeed => moveSpeed;
    public bool IsRecoveringFromCarry => wasCarried || Time.time < resumeFromCarryAt ||
        (carry != null && carry.IsReserved);

    private void Awake()
    {
        CacheReferences();
        CaptureHomeIfNeeded();
    }

    private void Start()
    {
        if (!hasDestination && state == CharacterWanderState2D.Waiting)
            BeginWaiting(RandomWait());
    }

    private void OnEnable()
    {
        CacheReferences();
        CaptureHomeIfNeeded();
    }

    private void CacheReferences()
    {
        if (body == null) body = GetComponent<Rigidbody2D>();
        if (bodyCollider == null) bodyCollider = GetComponent<Collider2D>();
        if (health == null) health = GetComponent<CharacterHealth>();
        if (health == null) health = GetComponentInChildren<CharacterHealth>();
        if (carry == null) carry = GetComponent<CarryableObject2D>();
        if (chaser == null) chaser = GetComponent<EnemyChaser2D>();
        if (guard == null) guard = GetComponent<FactionGuardCombat2D>();
        if (targeting == null) targeting = GetComponent<FactionEnemyTargeting2D>();
        if (investigationAI == null) investigationAI = GetComponent<CharacterAIInvestigation2D>();
        if (fearAI == null) fearAI = GetComponent<CharacterAIFearFlee2D>();
        if (personalityAI == null) personalityAI = GetComponent<CharacterAIPersonality2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (groundLayers.value == 0)
        {
            int ground = LayerMask.NameToLayer("Ground");
            if (ground >= 0) groundLayers = 1 << ground;
        }
    }

    private void CaptureHomeIfNeeded()
    {
        if (homeCaptured) return;
        home = transform.position;
        homeCaptured = true;
    }

    /// <summary>EnemyChaser2Dから呼ぶ。戦闘への出入りを通知する。</summary>
    public void SetCombatActive(bool active)
    {
        if (active)
        {
            if (!combatActive)
            {
                combatActive = true;
                hasDestination = false;
            }
            lastRequestedSpeed = 0f;
            ChangeState(CharacterWanderState2D.Combat);
            return;
        }

        if (!combatActive) return;
        combatActive = false;
        hasDestination = false;
        consecutiveReturnBlocks = 0;
        if (returnHomeAfterCombat &&
            Mathf.Abs(transform.position.x - home.x) > arrivalDistance)
            ChangeState(CharacterWanderState2D.Returning);
        else
            BeginWaiting(RandomWait());
    }

    /// <summary>EnemyChaser2DのUpdateから取得する、通常行動の希望X速度。</summary>
    public float GetDesiredSpeed()
    {
        CacheReferences();
        CaptureHomeIfNeeded();
        if (IsUnavailable()) return 0f;
        if (combatActive)
        {
            lastRequestedSpeed = 0f;
            ChangeState(CharacterWanderState2D.Combat);
            return 0f;
        }

        if (mode == CharacterWanderMode2D.Idle && state != CharacterWanderState2D.Returning)
        {
            lastRequestedSpeed = 0f;
            ChangeState(CharacterWanderState2D.Waiting);
            return 0f;
        }

        if (Time.time < blockedUntil)
        {
            lastRequestedSpeed = 0f;
            return 0f;
        }

        if (state == CharacterWanderState2D.Waiting)
        {
            if (Time.time < waitUntil)
            {
                lastRequestedSpeed = 0f;
                return 0f;
            }
            BeginNextMovement();
        }

        if (state == CharacterWanderState2D.Returning)
        {
            destinationX = home.x;
            hasDestination = true;
        }

        if (!hasDestination)
        {
            BeginWaiting(RandomWait());
            return 0f;
        }

        float difference = destinationX - transform.position.x;
        if (Mathf.Abs(difference) <= arrivalDistance)
        {
            if (state == CharacterWanderState2D.Patrolling) AdvancePatrolIndex();
            consecutiveReturnBlocks = 0;
            BeginWaiting(RandomWait());
            return 0f;
        }

        lastRequestedSpeed = Mathf.Sign(difference) * moveSpeed;
        return lastRequestedSpeed;
    }

    private bool IsUnavailable()
    {
        if (health != null && health.IsDead)
        {
            lastRequestedSpeed = 0f;
            ChangeState(CharacterWanderState2D.Dead);
            return true;
        }

        if (carry != null && carry.IsReserved)
        {
            wasCarried = true;
            lastRequestedSpeed = 0f;
            ChangeState(CharacterWanderState2D.Carried);
            return true;
        }

        if (wasCarried)
        {
            wasCarried = false;
            combatActive = false;
            hasDestination = false;
            if (useDropPositionAsHome) home = transform.position;
            resumeFromCarryAt = Time.time + waitAfterDrop;
            BeginWaiting(waitAfterDrop);
            lastRequestedSpeed = 0f;
            return true;
        }
        return false;
    }

    private void BeginNextMovement()
    {
        hasDestination = false;
        if (mode == CharacterWanderMode2D.RandomWander)
        {
            float radius = Mathf.Max(.1f, wanderRadius);
            // 微小な目的地を引くと停止を繰り返すため、ある程度離れた位置を選ぶ。
            for (int i = 0; i < 6; i++)
            {
                float candidate = home.x + Random.Range(-radius, radius);
                if (Mathf.Abs(candidate - transform.position.x) < Mathf.Min(radius * .3f, arrivalDistance * 2f))
                    continue;
                destinationX = candidate;
                hasDestination = true;
                break;
            }
            if (!hasDestination) destinationX = home.x + (Random.value > .5f ? radius : -radius);
            hasDestination = true;
            ChangeState(CharacterWanderState2D.Wandering);
        }
        else if (mode == CharacterWanderMode2D.Patrol)
        {
            if (patrolPoints == null || patrolPoints.Count == 0)
            {
                BeginWaiting(1f);
                return;
            }
            for (int j = 0; j < patrolPoints.Count; j++)
            {
                int index = Mathf.Clamp(patrolIndex, 0, patrolPoints.Count - 1);
                if (patrolPoints[index] != null)
                {
                    destinationX = patrolPoints[index].position.x;
                    hasDestination = true;
                    ChangeState(CharacterWanderState2D.Patrolling);
                    return;
                }
                AdvancePatrolIndex();
            }
            BeginWaiting(1f);
        }
        else
        {
            BeginWaiting(1f);
        }
    }

    private void AdvancePatrolIndex()
    {
        int count = patrolPoints != null ? patrolPoints.Count : 0;
        if (count <= 1) { patrolIndex = 0; return; }
        if (!pingPongPatrol) { patrolIndex = (patrolIndex + 1) % count; return; }
        if (patrolIndex + patrolStep >= count || patrolIndex + patrolStep < 0)
            patrolStep = -patrolStep;
        patrolIndex = Mathf.Clamp(patrolIndex + patrolStep, 0, count - 1);
    }

    private float RandomWait()
    {
        return Random.Range(Mathf.Max(0f, minWaitSeconds),
            Mathf.Max(minWaitSeconds, maxWaitSeconds));
    }

    private void BeginWaiting(float seconds)
    {
        hasDestination = false;
        lastRequestedSpeed = 0f;
        waitUntil = Time.time + Mathf.Max(0f, seconds);
        ChangeState(CharacterWanderState2D.Waiting);
    }

    public void NotifyMovementBlocked(float direction)
    {
        if (Mathf.Abs(direction) < .01f || combatActive) return;
        lastRequestedSpeed = 0f;
        blockedUntil = Time.time + Random.Range(.3f, .65f);
        if (state == CharacterWanderState2D.Returning)
        {
            // 進入不能な壁の向こうへ永久に戻ろうとしない。
            consecutiveReturnBlocks++;
            if (consecutiveReturnBlocks >= 3) BeginWaiting(2f);
            return;
        }
        if (state == CharacterWanderState2D.Patrolling) AdvancePatrolIndex();
        // ぶつかった向きの逆側を次の移動候補にする。
        BeginWaiting(Random.Range(.25f, .65f));
    }

    private void ChangeState(CharacterWanderState2D next)
    {
        if (state == next) return;
        state = next;
        if (printStateChanges) Debug.Log("[CharacterWander2D] " + name + " -> " + state, this);
    }

    private void Update()
    {
        // EnemyChaser2Dがアクティブな場合は、移動と戦闘判断を任せる。
        if (chaser != null && chaser.isActiveAndEnabled) return;
        // STEP3: 逃走・うずくまる恐怖反応はGuard戦闘／音調査／徘徊より優先。
        // 運搬中のRigidbody2D操作は禁止したままにする。
        if (fearAI != null && fearAI.isActiveAndEnabled &&
            fearAI.TryGetDesiredSpeed(out float reactionSpeed))
        {
            if (IsUnavailable()) return;
            SetCombatActive(true);
            lastRequestedSpeed = reactionSpeed;
            ChangeState(fearAI.IsFleeing
                ? CharacterWanderState2D.Fleeing : CharacterWanderState2D.Cowering);
            if (Mathf.Abs(reactionSpeed) > .01f)
            {
                float fleeDirection = Mathf.Sign(reactionSpeed);
                if ((avoidDeepCliffs && IsCliffAhead(fleeDirection)) ||
                    (turnAroundAtWalls && IsWallAhead(fleeDirection)))
                {
                    lastRequestedSpeed = 0f;
                    fearAI.NotifyMovementBlocked();
                }
                else UpdateFacing(fleeDirection);
            }
            return;
        }
        // Guardの索敵はGuard自身が更新する。Targeting.CurrentTargetだけを見て
        // その場に止めると、敵が攻撃範囲に入る前に両者が立ち止まる。
        // Guardはダメージのみを担当し、Wanderが攻撃距離まで接近する。
        Transform guardTarget = guard != null && guard.isActiveAndEnabled
            ? guard.CurrentTarget : null;
        if (guardTarget != null && guardTarget.gameObject.activeInHierarchy)
        {
            SetCombatActive(true);
            // carried / dead をGetDesiredSpeed同様に処理する。
            if (IsUnavailable()) return;
            if (!approachHostilesWithGuard)
            {
                lastRequestedSpeed = 0f;
                return;
            }

            float attackRange = guard.AttackDistance;
            float distance = Vector2.Distance(transform.position, guardTarget.position);
            float horizontal = guardTarget.position.x - transform.position.x;
            // Guardの半径内へ確実に入るまで接近する。X成分がほぼ0なら移動不要。
            float stopRange = Mathf.Max(.05f, attackRange - .08f);
            if (distance <= stopRange || Mathf.Abs(horizontal) <= .03f)
            {
                lastRequestedSpeed = 0f;
                return;
            }
            float directionToHostile = Mathf.Sign(horizontal);
            if ((avoidDeepCliffs && IsCliffAhead(directionToHostile)) ||
                (turnAroundAtWalls && IsWallAhead(directionToHostile)))
            {
                lastRequestedSpeed = 0f;
                return;
            }
            lastRequestedSpeed = directionToHostile * guardApproachSpeed *
                (personalityAI != null ? personalityAI.CombatMoveMultiplier : 1f);
            UpdateFacing(directionToHostile);
            return;
        }

        // 敵対対象がいなくなったら、STEP2の調査を優先。なければ通常徘徊・帰還へ。
        SetCombatActive(false);
        if (investigationAI != null && investigationAI.isActiveAndEnabled &&
            investigationAI.TryGetDesiredSpeed(out float inspectSpeed))
        {
            lastRequestedSpeed = inspectSpeed;
            ChangeState(investigationAI.CurrentState == CharacterAIInvestigationState2D.Searching
                ? CharacterWanderState2D.Searching : CharacterWanderState2D.Investigating);
            if (Mathf.Abs(inspectSpeed) > .01f)
            {
                float inspectDirection = Mathf.Sign(inspectSpeed);
                if ((avoidDeepCliffs && IsCliffAhead(inspectDirection)) ||
                    (turnAroundAtWalls && IsWallAhead(inspectDirection)))
                {
                    lastRequestedSpeed = 0f;
                    investigationAI.NotifyMovementBlocked();
                }
                else UpdateFacing(inspectDirection);
            }
            return;
        }
        // 調査終了時に残った専用Stateを通常の待機に戻す。
        if (state == CharacterWanderState2D.Investigating ||
            state == CharacterWanderState2D.Searching)
        {
            if (returnHomeAfterCombat &&
                Mathf.Abs(transform.position.x - home.x) > arrivalDistance)
                ChangeState(CharacterWanderState2D.Returning);
            else
                BeginWaiting(RandomWait());
        }
        float speed = GetDesiredSpeed();
        if (Mathf.Abs(speed) <= .01f) return;
        float direction = Mathf.Sign(speed);
        if ((avoidDeepCliffs && IsCliffAhead(direction)) ||
            (turnAroundAtWalls && IsWallAhead(direction)))
        {
            NotifyMovementBlocked(direction);
            return;
        }
        UpdateFacing(direction);
    }

    private void FixedUpdate()
    {
        if (chaser != null && chaser.isActiveAndEnabled) return;
        if (body == null || body.bodyType != RigidbodyType2D.Dynamic || !body.simulated) return;
        if ((health != null && health.IsDead) || (carry != null && carry.IsReserved)) return;
        Vector2 v = body.linearVelocity;
        v.x = lastRequestedSpeed;
        body.linearVelocity = v;
    }

    private bool IsCliffAhead(float direction)
    {
        if (bodyCollider == null || groundLayers.value == 0) return false;
        Bounds b = bodyCollider.bounds;
        Vector2 origin = new Vector2(b.center.x + direction * (b.extents.x + forwardProbe), b.min.y + .07f);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, Vector2.down, cliffProbeDistance, groundLayers);
        float nearestDrop = float.PositiveInfinity;
        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null || hit.collider.transform.IsChildOf(transform) || hit.normal.y < .2f) continue;
            nearestDrop = Mathf.Min(nearestDrop, Mathf.Max(0f, origin.y - hit.point.y));
        }
        return nearestDrop > maxSafeDrop;
    }

    private bool IsWallAhead(float direction)
    {
        if (bodyCollider == null) return false;
        int mask = obstacleLayers.value != 0 ? obstacleLayers.value : groundLayers.value;
        if (mask == 0) return false;
        Bounds b = bodyCollider.bounds;
        Vector2 origin = new Vector2(b.center.x + direction * b.extents.x, b.min.y + b.size.y * .45f);
        RaycastHit2D[] hits = Physics2D.RaycastAll(origin, new Vector2(direction, 0f), forwardProbe + .08f, mask);
        foreach (RaycastHit2D hit in hits)
            if (hit.collider != null && !hit.collider.transform.IsChildOf(transform)) return true;
        return false;
    }

    private void UpdateFacing(float direction)
    {
        if (spriteRenderer == null || Mathf.Abs(direction) < .01f) return;
        bool right = direction > 0f;
        spriteRenderer.flipX = spriteFacesRightByDefault ? !right : right;
    }

    private void OnDisable()
    {
        lastRequestedSpeed = 0f;
        // 追跡AI所有時はRigidbody2Dに手を出さない。
        if (chaser != null && chaser.isActiveAndEnabled) return;
        if (body != null && body.bodyType == RigidbodyType2D.Dynamic &&
            body.simulated && (carry == null || !carry.IsReserved))
        {
            Vector2 velocity = body.linearVelocity;
            velocity.x = 0f;
            body.linearVelocity = velocity;
        }
    }

    private void OnValidate()
    {
        moveSpeed = Mathf.Max(.1f, moveSpeed);
        wanderRadius = Mathf.Max(.1f, wanderRadius);
        minWaitSeconds = Mathf.Max(0f, minWaitSeconds);
        maxWaitSeconds = Mathf.Max(minWaitSeconds, maxWaitSeconds);
        arrivalDistance = Mathf.Max(.05f, arrivalDistance);
        forwardProbe = Mathf.Max(.05f, forwardProbe);
        cliffProbeDistance = Mathf.Max(.1f, cliffProbeDistance);
        maxSafeDrop = Mathf.Max(0f, maxSafeDrop);
        guardApproachSpeed = Mathf.Max(.1f, guardApproachSpeed);
        waitAfterDrop = Mathf.Max(0f, waitAfterDrop);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Vector3 h = Application.isPlaying && homeCaptured ? (Vector3)home : transform.position;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(h, .14f);
        Gizmos.DrawLine(new Vector3(h.x - wanderRadius, h.y, 0f),
            new Vector3(h.x + wanderRadius, h.y, 0f));
        if (Application.isPlaying && hasDestination)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position,
                new Vector3(destinationX, transform.position.y, transform.position.z));
        }
        if (patrolPoints != null)
        {
            Gizmos.color = Color.green;
            for (int i = 0; i < patrolPoints.Count; i++)
            {
                if (patrolPoints[i] == null) continue;
                Gizmos.DrawWireSphere(patrolPoints[i].position, .12f);
                if (i > 0 && patrolPoints[i - 1] != null)
                    Gizmos.DrawLine(patrolPoints[i - 1].position, patrolPoints[i].position);
            }
        }
    }
}
