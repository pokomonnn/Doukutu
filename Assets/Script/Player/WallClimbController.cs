using System;
using UnityEngine;

/// <summary>
/// Groundレイヤーの垂直な壁に張り付きます。
/// 壁へ張り付く時だけ「壁方向 + W」を使い、
/// 張り付いた後はWだけで上昇、Sだけで下降できます。
/// ロープとは別機能ですが、同時操作にならないようRopeClimbControllerとも連携します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(Rigidbody2D))]
public class WallClimbController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Tooltip("未設定ならPlayer本体の、Is TriggerがオフのCollider2Dを使います")]
    [SerializeField] private Collider2D playerBodyCollider;

    [Tooltip("壁登り中の照準・射撃・リロードを止めます")]
    [SerializeField]
    private PlayerEquipmentVisualController equipmentVisualController;

    [Tooltip("壁登り中は石投げも止めたい場合に設定します。未設定なら自動取得します")]
    [SerializeField] private StoneThrower stoneThrower;

    [Tooltip("ロープ上り下りと同時に開始しないために使います。未設定なら自動取得します")]
    [SerializeField] private RopeClimbController ropeClimbController;

    [Tooltip("死亡中に壁登りを開始しないために使います。未設定なら自動取得します")]
    [SerializeField] private CharacterHealth playerHealth;

    [Tooltip("任意。AnimatorにBoolを作った時だけ名前を設定します。例：IsWallClimbing")]
    [SerializeField] private Animator playerAnimator;

    [SerializeField] private string wallClimbingBoolName = "";

    [Header("壁の判定")]
    [Tooltip("壁・床・天井に使っているGroundレイヤーを設定します。空欄ならPlayerMoveのGround Layerを使います")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("壁を探すBoxCastの横方向の余裕です。壁に近付いても開始しない時だけ少し上げます")]
    [SerializeField, Min(0.01f)] private float wallCheckDistance = 0.16f;

    [Tooltip("PlayerのCollider幅に対する、壁判定BoxCastの幅倍率です")]
    [SerializeField, Range(0.1f, 1f)] private float wallCheckWidthMultiplier = 0.45f;

    [Tooltip("PlayerのCollider高さに対する、壁判定BoxCastの高さ倍率です")]
    [SerializeField, Range(0.1f, 1f)] private float wallCheckHeightMultiplier = 0.72f;

    [Tooltip("この値以上、横を向いた法線だけを壁として扱います。0.7前後がおすすめです")]
    [SerializeField, Range(0.1f, 1f)] private float minimumWallNormalX = 0.7f;

    [Tooltip("張り付いている時に壁から空ける小さな隙間です")]
    [SerializeField, Min(0f)] private float wallContactOffset = 0.015f;

    [Tooltip("Trigger Colliderを壁として扱うかどうか。通常はオフのままでOKです")]
    [SerializeField] private bool allowTriggerWalls;

    [Header("操作")]
    [SerializeField] private KeyCode climbUpKey = KeyCode.W;
    [SerializeField] private KeyCode climbDownKey = KeyCode.S;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Tooltip("壁へ張り付く開始操作です。右壁はD+W、左壁はA+Wです。張り付いた後はW/Sだけで上下します")]
    [SerializeField] private bool requireWallDirectionToGrab = true;

    [Tooltip("壁登り中に壁と逆方向だけを押した時、Sなしでも壁から離れて落下します")]
    [SerializeField] private bool detachWhenMovingAway = true;

    [Header("移動設定")]
    [SerializeField, Min(0.01f)] private float climbUpSpeed = 2.4f;
    [SerializeField, Min(0.01f)] private float climbDownSpeed = 2.1f;

    [Tooltip("入力を離している間も壁に張り付くかどうか。オフの場合はすぐ落下します")]
    [SerializeField] private bool hangOnWallWhenNoInput = true;

    [Header("壁からジャンプ")]
    [SerializeField, Min(0f)] private float jumpOffHorizontalPower = 6f;
    [SerializeField, Min(0f)] private float jumpOffVerticalPower = 9f;
    [SerializeField, Min(0f)] private float jumpOffHorizontalOffset = 0.06f;

    [Tooltip("ジャンプ直後に同じ壁へ再び張り付かないための秒数です")]
    [SerializeField, Min(0f)] private float regrabDelayAfterJump = 0.18f;

    [Header("行動制限")]
    [SerializeField] private bool lockWeaponControlsWhileClimbing = true;
    [SerializeField] private bool disableStoneThrowWhileClimbing = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;
    [SerializeField] private bool showWallCheckGizmo = true;

    public bool IsWallClimbing => isWallClimbing;

    private struct WallContact
    {
        public Collider2D Collider;
        public Vector2 Point;
        public Vector2 Normal;
    }

    private bool isWallClimbing;
    private int wallDirection;
    private WallContact currentWall;

    private float originalGravityScale;
    private bool hasCachedPhysics;

    private bool wasPlayerMoveEnabledBeforeClimb;
    private bool hasDisabledPlayerMove;

    private bool wasStoneThrowerEnabledBeforeClimb;
    private bool hasDisabledStoneThrower;

    private bool wasRopeClimbEnabledBeforeWallClimb;
    private bool hasDisabledRopeClimb;

    private float regrabAllowedTime;
    private bool jumpOffRequested;

    private int horizontalInputDirection;
    private bool upHeld;
    private bool downHeld;

    private void Awake()
    {
        FindReferences();
    }

    private void OnDisable()
    {
        StopWallClimb(true);
    }

    private void OnDestroy()
    {
        StopWallClimb(true);
    }

    private void Update()
    {
        FindReferences();

        horizontalInputDirection = GetHorizontalInputDirection();
        upHeld = Input.GetKey(climbUpKey);
        downHeld = Input.GetKey(climbDownKey);

        if (playerHealth != null && playerHealth.IsDead)
        {
            StopWallClimb(true);
            return;
        }

        if (!isWallClimbing)
        {
            TryStartWallClimb();
            return;
        }

        if (Input.GetKeyDown(jumpKey))
        {
            jumpOffRequested = true;
        }
    }

    private void FixedUpdate()
    {
        if (!isWallClimbing || playerRigidbody == null)
        {
            return;
        }

        if (jumpOffRequested)
        {
            jumpOffRequested = false;
            JumpOffWall();
            return;
        }

        if (!TryGetWallContact(wallDirection, out WallContact wall))
        {
            Log("壁判定を見失ったため、壁登りを終了します。");
            StopWallClimb(true);
            return;
        }

        currentWall = wall;

        // 横方向だけで壁と逆へ離れる時は、壁から手を離して落下する。
        // W/Sで上下している最中は、横入力が残っていても上下操作を優先する。
        if (detachWhenMovingAway &&
            horizontalInputDirection == -wallDirection &&
            !upHeld &&
            !downHeld)
        {
            StopWallClimb(true);
            return;
        }

        float verticalSpeed = GetVerticalClimbSpeed();

        if (!hangOnWallWhenNoInput && Mathf.Approximately(verticalSpeed, 0f))
        {
            StopWallClimb(true);
            return;
        }

        MoveAlongWall(wall, verticalSpeed);
    }

    /// <summary>
    /// 外部処理から壁登りを中断したい時に呼べます。
    /// </summary>
    public void StopWallClimbNow()
    {
        StopWallClimb(true);
    }

    private void TryStartWallClimb()
    {
        if (Time.time < regrabAllowedTime ||
            playerRigidbody == null ||
            horizontalInputDirection == 0)
        {
            return;
        }

        if (ropeClimbController != null && ropeClimbController.IsClimbing)
        {
            return;
        }

        // 張り付き開始だけは、従来どおり壁方向 + W を使う。
        // 右側の壁ならD + W、左側の壁ならA + W。
        if (!upHeld)
        {
            return;
        }

        if (requireWallDirectionToGrab && horizontalInputDirection == 0)
        {
            return;
        }

        int directionToWall = horizontalInputDirection;

        if (directionToWall == 0)
        {
            return;
        }

        if (TryGetWallContact(directionToWall, out WallContact wall))
        {
            StartWallClimb(directionToWall, wall);
        }
    }

    private void StartWallClimb(int directionToWall, WallContact wall)
    {
        if (isWallClimbing || directionToWall == 0 || playerRigidbody == null)
        {
            return;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            return;
        }

        wallDirection = directionToWall;
        currentWall = wall;
        isWallClimbing = true;

        CacheAndDisableMovement();
        CacheAndDisableRopeClimb();
        CachePhysics();
        LockOtherActions();
        SetWallClimbAnimation(true);

        SnapToWall(currentWall);

        Log(wallDirection > 0
            ? "右側の壁へ張り付きました。"
            : "左側の壁へ張り付きました。"
        );
    }

    private void MoveAlongWall(WallContact wall, float verticalSpeed)
    {
        Vector2 position = playerRigidbody.position;
        float targetX = GetWallHoldX(wall);
        float targetY = position.y + verticalSpeed * Time.fixedDeltaTime;

        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;
        playerRigidbody.MovePosition(new Vector2(targetX, targetY));
    }

    private void SnapToWall(WallContact wall)
    {
        if (playerRigidbody == null)
        {
            return;
        }

        Vector2 position = playerRigidbody.position;
        position.x = GetWallHoldX(wall);

        playerRigidbody.position = position;
        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;
    }

    private float GetWallHoldX(WallContact wall)
    {
        float halfWidth = playerBodyCollider != null
            ? playerBodyCollider.bounds.extents.x
            : 0.25f;

        return wall.Point.x -
            wallDirection * (halfWidth + wallContactOffset);
    }

    private float GetVerticalClimbSpeed()
    {
        // 張り付いた後は、横入力なしでW/Sだけを使って上下する。
        // WとSを同時に押した時は、その場で停止する。
        if (upHeld == downHeld)
        {
            return 0f;
        }

        return upHeld
            ? climbUpSpeed
            : -climbDownSpeed;
    }

    private void JumpOffWall()
    {
        if (playerRigidbody == null)
        {
            StopWallClimb(true);
            return;
        }

        int jumpDirection = horizontalInputDirection == -wallDirection
            ? horizontalInputDirection
            : -wallDirection;

        // Colliderのめり込みを避けるため、先にほんの少し壁から離す。
        Vector2 jumpPosition = playerRigidbody.position;
        jumpPosition.x += jumpDirection * jumpOffHorizontalOffset;

        StopWallClimb(true);

        playerRigidbody.position = jumpPosition;
        playerRigidbody.linearVelocity = new Vector2(
            jumpDirection * jumpOffHorizontalPower,
            jumpOffVerticalPower
        );
        playerRigidbody.angularVelocity = 0f;

        regrabAllowedTime = Time.time + regrabDelayAfterJump;

        Log("壁からジャンプしました。");
    }

    private void StopWallClimb(bool restorePhysics)
    {
        bool wasUsingWall =
            isWallClimbing ||
            hasDisabledPlayerMove ||
            hasDisabledStoneThrower ||
            hasDisabledRopeClimb ||
            hasCachedPhysics;

        if (!wasUsingWall)
        {
            return;
        }

        if (restorePhysics && playerRigidbody != null && hasCachedPhysics)
        {
            playerRigidbody.gravityScale = originalGravityScale;
            playerRigidbody.angularVelocity = 0f;
        }

        UnlockOtherActions();
        RestoreRopeClimb();
        RestoreMovement();
        SetWallClimbAnimation(false);

        isWallClimbing = false;
        wallDirection = 0;
        currentWall = default;
        hasCachedPhysics = false;
        jumpOffRequested = false;
    }

    private bool TryGetWallContact(int directionToWall, out WallContact contact)
    {
        contact = default;

        if (playerBodyCollider == null || directionToWall == 0)
        {
            return false;
        }

        Bounds bounds = playerBodyCollider.bounds;

        Vector2 boxCenter = bounds.center;
        Vector2 boxSize = new Vector2(
            Mathf.Max(0.03f, bounds.size.x * wallCheckWidthMultiplier),
            Mathf.Max(0.03f, bounds.size.y * wallCheckHeightMultiplier)
        );

        float castDistance =
            bounds.extents.x + wallCheckDistance;

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            boxCenter,
            boxSize,
            0f,
            Vector2.right * directionToWall,
            castDistance,
            groundLayers
        );

        float bestDistance = float.PositiveInfinity;

        foreach (RaycastHit2D hit in hits)
        {
            Collider2D hitCollider = hit.collider;

            if (hitCollider == null ||
                hitCollider == playerBodyCollider ||
                hitCollider.transform.IsChildOf(transform))
            {
                continue;
            }

            if (!allowTriggerWalls && hitCollider.isTrigger)
            {
                continue;
            }

            // 右へCastした時はnormal.xが負、左へCastした時はnormal.xが正の面だけを壁として採用。
            if (hit.normal.x * directionToWall > -minimumWallNormalX)
            {
                continue;
            }

            if (hit.distance >= bestDistance)
            {
                continue;
            }

            bestDistance = hit.distance;
            contact = new WallContact
            {
                Collider = hitCollider,
                Point = hit.point,
                Normal = hit.normal
            };
        }

        return contact.Collider != null;
    }

    private void CachePhysics()
    {
        if (hasCachedPhysics || playerRigidbody == null)
        {
            return;
        }

        originalGravityScale = playerRigidbody.gravityScale;
        hasCachedPhysics = true;

        playerRigidbody.gravityScale = 0f;
        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;
    }

    private void CacheAndDisableMovement()
    {
        if (playerMove == null || hasDisabledPlayerMove)
        {
            return;
        }

        wasPlayerMoveEnabledBeforeClimb = playerMove.enabled;
        hasDisabledPlayerMove = true;
        playerMove.enabled = false;
    }

    private void RestoreMovement()
    {
        if (!hasDisabledPlayerMove)
        {
            return;
        }

        if (playerMove != null && wasPlayerMoveEnabledBeforeClimb)
        {
            playerMove.enabled = true;
        }

        wasPlayerMoveEnabledBeforeClimb = false;
        hasDisabledPlayerMove = false;
    }

    private void CacheAndDisableRopeClimb()
    {
        if (ropeClimbController == null || hasDisabledRopeClimb)
        {
            return;
        }

        // 念のため同時にロープを掴んでいた場合は、先にロープ処理を安全に終了する。
        if (ropeClimbController.IsClimbing)
        {
            ropeClimbController.StopClimbingNow();
        }

        wasRopeClimbEnabledBeforeWallClimb =
            ropeClimbController.enabled;

        hasDisabledRopeClimb = true;
        ropeClimbController.enabled = false;
    }

    private void RestoreRopeClimb()
    {
        if (!hasDisabledRopeClimb)
        {
            return;
        }

        if (ropeClimbController != null &&
            wasRopeClimbEnabledBeforeWallClimb)
        {
            ropeClimbController.enabled = true;
        }

        wasRopeClimbEnabledBeforeWallClimb = false;
        hasDisabledRopeClimb = false;
    }

    private void LockOtherActions()
    {
        if (lockWeaponControlsWhileClimbing &&
            equipmentVisualController != null)
        {
            equipmentVisualController.SetWeaponControlLock(this, true);
        }

        if (!disableStoneThrowWhileClimbing ||
            stoneThrower == null ||
            hasDisabledStoneThrower)
        {
            return;
        }

        wasStoneThrowerEnabledBeforeClimb = stoneThrower.enabled;
        hasDisabledStoneThrower = true;
        stoneThrower.enabled = false;
    }

    private void UnlockOtherActions()
    {
        if (equipmentVisualController != null)
        {
            equipmentVisualController.SetWeaponControlLock(this, false);
        }

        if (!hasDisabledStoneThrower)
        {
            return;
        }

        if (stoneThrower != null && wasStoneThrowerEnabledBeforeClimb)
        {
            stoneThrower.enabled = true;
        }

        wasStoneThrowerEnabledBeforeClimb = false;
        hasDisabledStoneThrower = false;
    }

    private void SetWallClimbAnimation(bool value)
    {
        if (playerAnimator == null ||
            string.IsNullOrWhiteSpace(wallClimbingBoolName))
        {
            return;
        }

        playerAnimator.SetBool(wallClimbingBoolName, value);
    }

    private int GetHorizontalInputDirection()
    {
        bool left = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
        bool right = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);

        if (left == right)
        {
            return 0;
        }

        return right ? 1 : -1;
    }

    private void FindReferences()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        if (playerBodyCollider == null)
        {
            Collider2D[] colliders = GetComponents<Collider2D>();

            foreach (Collider2D collider in colliders)
            {
                if (collider != null && !collider.isTrigger)
                {
                    playerBodyCollider = collider;
                    break;
                }
            }
        }

        if (equipmentVisualController == null)
        {
            equipmentVisualController =
                GetComponent<PlayerEquipmentVisualController>();
        }

        if (stoneThrower == null)
        {
            stoneThrower = GetComponent<StoneThrower>();
        }

        if (ropeClimbController == null)
        {
            ropeClimbController = GetComponent<RopeClimbController>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<CharacterHealth>();
        }

        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
        }

        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>(true);
        }

        if (groundLayers.value == 0 && playerMove != null)
        {
            groundLayers = playerMove.groundLayer;
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[WallClimbController: {name}] {message}", this);
    }

    private void OnValidate()
    {
        wallCheckDistance = Mathf.Max(0.01f, wallCheckDistance);
        wallCheckWidthMultiplier = Mathf.Clamp(wallCheckWidthMultiplier, 0.1f, 1f);
        wallCheckHeightMultiplier = Mathf.Clamp(wallCheckHeightMultiplier, 0.1f, 1f);
        minimumWallNormalX = Mathf.Clamp(minimumWallNormalX, 0.1f, 1f);
        wallContactOffset = Mathf.Max(0f, wallContactOffset);

        climbUpSpeed = Mathf.Max(0.01f, climbUpSpeed);
        climbDownSpeed = Mathf.Max(0.01f, climbDownSpeed);

        jumpOffHorizontalPower = Mathf.Max(0f, jumpOffHorizontalPower);
        jumpOffVerticalPower = Mathf.Max(0f, jumpOffVerticalPower);
        jumpOffHorizontalOffset = Mathf.Max(0f, jumpOffHorizontalOffset);
        regrabDelayAfterJump = Mathf.Max(0f, regrabDelayAfterJump);

        if (groundLayers.value == 0 && playerMove != null)
        {
            groundLayers = playerMove.groundLayer;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showWallCheckGizmo)
        {
            return;
        }

        Collider2D body = playerBodyCollider != null
            ? playerBodyCollider
            : GetComponent<Collider2D>();

        if (body == null)
        {
            return;
        }

        Bounds bounds = body.bounds;
        Vector2 boxSize = new Vector2(
            Mathf.Max(0.03f, bounds.size.x * wallCheckWidthMultiplier),
            Mathf.Max(0.03f, bounds.size.y * wallCheckHeightMultiplier)
        );

        float castDistance = bounds.extents.x + wallCheckDistance;

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireCube(
            bounds.center + Vector3.right * castDistance,
            boxSize
        );
        Gizmos.DrawWireCube(
            bounds.center + Vector3.left * castDistance,
            boxSize
        );

        if (isWallClimbing)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(
                transform.position,
                transform.position + Vector3.right * wallDirection * 0.8f
            );
        }
    }
}
