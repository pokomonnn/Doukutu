using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Playerに付けて使うロープ上り下り操作です。
/// ロープのTrigger内でW/Sを押すとつかまり、W/Sで上下します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(Rigidbody2D))]
public class RopeClimbController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private Rigidbody2D playerRigidbody;

    [Tooltip("ロープ中だけ無効化する、Player本体の物理Colliderです。未設定ならPlayer直下のCollider2Dを自動取得します")]
    [SerializeField] private Collider2D playerBodyCollider;

    [Tooltip("ロープ中の射撃・照準・リロードを止めるために使います")]
    [SerializeField]
    private PlayerEquipmentVisualController
        equipmentVisualController;

    [Tooltip("ロープ中は石投げも止めたい場合に設定します。未設定なら自動取得します")]
    [SerializeField] private StoneThrower stoneThrower;

    [Tooltip("任意。AnimatorにBoolを作った時だけ名前を設定します。例：IsClimbing")]
    [SerializeField] private Animator playerAnimator;

    [SerializeField] private string climbingBoolName = "";

    [Header("操作")]
    [SerializeField] private KeyCode climbUpKey = KeyCode.W;
    [SerializeField] private KeyCode climbDownKey = KeyCode.S;

    [Tooltip("W/Sを押した瞬間にロープへつかまります")]
    [SerializeField] private bool requireVerticalKeyToStart = true;

    [Header("移動設定")]
    [SerializeField, Min(0.01f)] private float climbSpeed = 2.5f;

    [Tooltip("上端・下端にこの距離まで近づいた状態で、さらにW/Sを押すとロープから降ります")]
    [SerializeField, Min(0f)] private float exitThreshold = 0.06f;

    [Tooltip("ロープへつかまる時、X座標をロープ中心へ寄せる速さ。0ならすぐ中心へ寄せます")]
    [SerializeField, Min(0f)] private float horizontalSnapSpeed = 18f;

    [Header("角で止まらないための設定")]
    [Tooltip("オンなら、ロープ中だけPlayer本体の物理Colliderを無効にします。崖の角・天井に引っかからず、ロープを上下できます")]
    [SerializeField] private bool disablePlayerBodyColliderWhileClimbing = true;

    [Header("ロープから離れた直後の再つかまり防止")]
    [Tooltip("上端・下端から降りた直後、同じロープを再びつかめるまでの最短秒数です")]
    [SerializeField, Min(0f)] private float regrabDelayAfterExit = 0.35f;

    [Header("制限")]
    [SerializeField] private bool lockWeaponControlsWhileClimbing = true;
    [SerializeField] private bool disableStoneThrowWhileClimbing = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public bool IsClimbing => isClimbing;
    public RopeClimbZone CurrentRopeZone => currentRopeZone;

    private readonly HashSet<RopeClimbZone> ropesInRange =
        new HashSet<RopeClimbZone>();

    private RopeClimbZone currentRopeZone;
    private RopeClimbZone lastExitedRopeZone;

    private bool isClimbing;
    private float verticalInput;
    private float regrabAllowedTime;

    private float originalGravityScale;
    private bool hasCachedPhysics;

    private bool wasPlayerMoveEnabledBeforeClimb;
    private bool hasDisabledPlayerMove;

    private bool wasStoneThrowerEnabledBeforeClimb;
    private bool hasDisabledStoneThrower;

    private bool wasPlayerBodyColliderEnabledBeforeClimb;
    private bool hasDisabledPlayerBodyCollider;

    private void Awake()
    {
        FindReferences();
    }

    private void OnDisable()
    {
        StopClimbing(true);
        ropesInRange.Clear();
        lastExitedRopeZone = null;
    }

    private void OnDestroy()
    {
        StopClimbing(true);
    }

    private void Update()
    {
        FindReferences();
        RemoveInvalidRopes();

        verticalInput = GetVerticalInput();

        if (!isClimbing)
        {
            TryStartClimbing();
            return;
        }

        if (currentRopeZone == null ||
            !currentRopeZone.IsClimbAvailable)
        {
            StopClimbing(true);
        }
    }

    private void FixedUpdate()
    {
        if (!isClimbing ||
            currentRopeZone == null ||
            playerRigidbody == null)
        {
            return;
        }

        MoveAlongRope();
    }

    /// <summary>
    /// RopeClimbZoneから呼ばれます。直接呼ぶ必要はありません。
    /// </summary>
    public void EnterRopeRange(RopeClimbZone ropeZone)
    {
        if (ropeZone == null)
        {
            return;
        }

        ropesInRange.Add(ropeZone);
    }

    /// <summary>
    /// RopeClimbZoneから呼ばれます。直接呼ぶ必要はありません。
    /// </summary>
    public void ExitRopeRange(RopeClimbZone ropeZone)
    {
        if (ropeZone == null)
        {
            return;
        }

        ropesInRange.Remove(ropeZone);

        // ロープ中はPlayer本体Colliderを一時的に無効化するため、
        // Trigger Exitが発生してもここでロープ移動を止めない。
        // RopeVisualが消えた場合などはUpdate側で安全に終了する。
        if (!isClimbing && ropeZone == lastExitedRopeZone)
        {
            lastExitedRopeZone = null;
            regrabAllowedTime = 0f;
        }
    }

    /// <summary>
    /// 外部からロープを離したい時に呼べます。
    /// </summary>
    public void StopClimbingNow()
    {
        StopClimbing(true);
    }

    private void TryStartClimbing()
    {
        RopeClimbZone ropeZone = GetNearestAvailableRope();

        if (ropeZone == null)
        {
            return;
        }

        if (requireVerticalKeyToStart &&
            Mathf.Abs(verticalInput) < 0.01f)
        {
            return;
        }

        StartClimbing(ropeZone);
    }

    private void StartClimbing(RopeClimbZone ropeZone)
    {
        if (isClimbing ||
            ropeZone == null ||
            playerRigidbody == null)
        {
            return;
        }

        currentRopeZone = ropeZone;
        isClimbing = true;

        originalGravityScale = playerRigidbody.gravityScale;
        hasCachedPhysics = true;

        playerRigidbody.gravityScale = 0f;
        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;

        LockPlayerMovement();
        LockOtherActions();
        DisablePlayerBodyCollider();
        SetClimbingAnimation(true);

        Vector2 position = playerRigidbody.position;
        position.x = ropeZone.RopeX;
        position.y = Mathf.Clamp(
            position.y,
            ropeZone.BottomClimbY,
            ropeZone.TopClimbY
        );

        playerRigidbody.position = position;

        Log("ロープにつかまりました。");
    }

    private void MoveAlongRope()
    {
        Vector2 position = playerRigidbody.position;

        float topY = currentRopeZone.TopClimbY;
        float bottomY = currentRopeZone.BottomClimbY;

        // 端に到着した次のFixedUpdateで、さらに同方向を押していた時だけ降りる。
        // 到着した瞬間に意図せずロープから離れるのを防ぎます。
        if (verticalInput > 0f &&
            position.y >= topY - exitThreshold)
        {
            ExitFromRope(true);
            return;
        }

        if (verticalInput < 0f &&
            position.y <= bottomY + exitThreshold)
        {
            ExitFromRope(false);
            return;
        }

        float targetX = currentRopeZone.RopeX;
        float newX = horizontalSnapSpeed <= 0f
            ? targetX
            : Mathf.MoveTowards(
                position.x,
                targetX,
                horizontalSnapSpeed * Time.fixedDeltaTime
            );

        float nextY = position.y +
            verticalInput * climbSpeed * Time.fixedDeltaTime;

        nextY = Mathf.Clamp(nextY, bottomY, topY);

        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.MovePosition(new Vector2(newX, nextY));
    }

    private void ExitFromRope(bool fromTop)
    {
        if (currentRopeZone == null ||
            playerRigidbody == null)
        {
            StopClimbing(true);
            return;
        }

        RopeClimbZone exitingRopeZone = currentRopeZone;

        Vector2 exitPosition = fromTop
            ? exitingRopeZone.GetTopExitPosition(
                playerRigidbody.position
            )
            : exitingRopeZone.GetBottomExitPosition(
                playerRigidbody.position
            );

        // 同じロープのTrigger内に残っていても、離れた直後に自動で
        // つかみ直さないように候補リストから外します。
        MarkRopeAsJustExited(exitingRopeZone);

        // Colliderを無効のまま出口位置へ移動してから、最後に元へ戻す。
        // これにより、崖の角・床の端にぶつかって登れなくなる問題を防ぎます。
        StopClimbing(false);

        playerRigidbody.position = exitPosition;
        playerRigidbody.linearVelocity = Vector2.zero;
        playerRigidbody.angularVelocity = 0f;

        RestorePlayerBodyCollider();

        Log(fromTop
            ? "ロープ上端から離れました。"
            : "ロープ下端から離れました。"
        );
    }

    private void MarkRopeAsJustExited(RopeClimbZone ropeZone)
    {
        if (ropeZone == null)
        {
            return;
        }

        ropesInRange.Remove(ropeZone);
        lastExitedRopeZone = ropeZone;
        regrabAllowedTime = Time.time + regrabDelayAfterExit;
    }

    private void StopClimbing(bool restorePlayerBodyCollider)
    {
        bool wasUsingRope =
            isClimbing ||
            hasDisabledPlayerMove ||
            hasDisabledStoneThrower ||
            hasDisabledPlayerBodyCollider;

        if (!wasUsingRope)
        {
            return;
        }

        if (playerRigidbody != null)
        {
            if (hasCachedPhysics)
            {
                playerRigidbody.gravityScale = originalGravityScale;
            }

            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }

        UnlockOtherActions();
        UnlockPlayerMovement();
        SetClimbingAnimation(false);

        currentRopeZone = null;
        isClimbing = false;
        hasCachedPhysics = false;

        if (restorePlayerBodyCollider)
        {
            RestorePlayerBodyCollider();
        }
    }

    private void LockPlayerMovement()
    {
        if (playerMove == null || hasDisabledPlayerMove)
        {
            return;
        }

        // 箱・キャンプ・死亡などで元から無効なら、
        // ロープ終了時に勝手に有効化しない。
        wasPlayerMoveEnabledBeforeClimb = playerMove.enabled;
        hasDisabledPlayerMove = true;
        playerMove.enabled = false;
    }

    private void UnlockPlayerMovement()
    {
        if (!hasDisabledPlayerMove)
        {
            return;
        }

        if (playerMove != null &&
            wasPlayerMoveEnabledBeforeClimb)
        {
            playerMove.enabled = true;
        }

        wasPlayerMoveEnabledBeforeClimb = false;
        hasDisabledPlayerMove = false;
    }

    private void DisablePlayerBodyCollider()
    {
        if (!disablePlayerBodyColliderWhileClimbing ||
            playerBodyCollider == null ||
            hasDisabledPlayerBodyCollider)
        {
            return;
        }

        wasPlayerBodyColliderEnabledBeforeClimb =
            playerBodyCollider.enabled;

        hasDisabledPlayerBodyCollider = true;
        playerBodyCollider.enabled = false;
    }

    private void RestorePlayerBodyCollider()
    {
        if (!hasDisabledPlayerBodyCollider)
        {
            return;
        }

        if (playerBodyCollider != null)
        {
            playerBodyCollider.enabled =
                wasPlayerBodyColliderEnabledBeforeClimb;
        }

        wasPlayerBodyColliderEnabledBeforeClimb = false;
        hasDisabledPlayerBodyCollider = false;
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

        wasStoneThrowerEnabledBeforeClimb =
            stoneThrower.enabled;

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

        if (stoneThrower != null &&
            wasStoneThrowerEnabledBeforeClimb)
        {
            stoneThrower.enabled = true;
        }

        wasStoneThrowerEnabledBeforeClimb = false;
        hasDisabledStoneThrower = false;
    }

    private void SetClimbingAnimation(bool climbing)
    {
        if (playerAnimator == null ||
            string.IsNullOrWhiteSpace(climbingBoolName))
        {
            return;
        }

        playerAnimator.SetBool(climbingBoolName, climbing);
    }

    private float GetVerticalInput()
    {
        bool up = Input.GetKey(climbUpKey);
        bool down = Input.GetKey(climbDownKey);

        if (up == down)
        {
            return 0f;
        }

        return up ? 1f : -1f;
    }

    private RopeClimbZone GetNearestAvailableRope()
    {
        RopeClimbZone nearest = null;
        float nearestDistance = float.PositiveInfinity;

        foreach (RopeClimbZone ropeZone in ropesInRange)
        {
            if (ropeZone == null ||
                !ropeZone.IsClimbAvailable)
            {
                continue;
            }

            if (ropeZone == lastExitedRopeZone &&
                Time.time < regrabAllowedTime)
            {
                continue;
            }

            float distance = Mathf.Abs(
                transform.position.x - ropeZone.RopeX
            );

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = ropeZone;
            }
        }

        return nearest;
    }

    private void RemoveInvalidRopes()
    {
        ropesInRange.RemoveWhere(
            ropeZone => ropeZone == null ||
                        !ropeZone.IsClimbAvailable
        );

        if (lastExitedRopeZone == null)
        {
            regrabAllowedTime = 0f;
        }
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
            playerBodyCollider = GetComponent<Collider2D>();
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

        if (playerAnimator == null)
        {
            playerAnimator = GetComponent<Animator>();
        }

        if (playerAnimator == null)
        {
            playerAnimator =
                GetComponentInChildren<Animator>(true);
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"[RopeClimbController: {name}] {message}",
            this
        );
    }

    private void OnValidate()
    {
        climbSpeed = Mathf.Max(0.01f, climbSpeed);
        exitThreshold = Mathf.Max(0f, exitThreshold);
        horizontalSnapSpeed = Mathf.Max(0f, horizontalSnapSpeed);
        regrabDelayAfterExit = Mathf.Max(0f, regrabDelayAfterExit);
    }
}
