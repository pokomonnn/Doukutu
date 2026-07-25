using System;
using System.Text;
using UnityEngine;

/// <summary>
/// Altキーでプレイヤーのほふく状態を切り替えます。
/// ほふく中は向きとジャンプを固定し、移動速度を下げます。
/// また、ほふく中はBody Colliderを縮小し、頭上に障害物がある場合は
/// 立ち上がれないようにします。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(PlayerWeightController))]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerProneController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerWeightController weightController;
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private CharacterHealth playerHealth;

    [Tooltip("ほふく時に縮小する、Is TriggerがOFFのBoxCollider2DまたはCapsuleCollider2Dです。未設定なら自動取得します")]
    [SerializeField] private Collider2D bodyCollider;

    [Tooltip("ほふく中は壁登りを開始できないようにします。未設定なら自動取得します")]
    [SerializeField] private WallClimbController wallClimbController;

    [Tooltip("ほふく中はロープ登りを開始できないようにします。未設定なら自動取得します")]
    [SerializeField] private RopeClimbController ropeClimbController;

    [Header("入力")]
    [Tooltip("ほふくの開始・解除に使うキーです")]
    [SerializeField] private KeyCode proneKey = KeyCode.LeftAlt;

    [Tooltip("右Altでもほふくを切り替えられるようにします")]
    [SerializeField] private bool allowRightAlt = true;

    [Header("ほふく設定")]
    [Tooltip("通常速度に掛ける倍率です。0.7なら通常の70%になります")]
    [SerializeField, Range(0.05f, 1f)]
    private float proneMoveSpeedMultiplier = 0.7f;

    [Tooltip("ほふく中に銃を動かせる合計角度です。90なら正面から上下45度ずつです")]
    [SerializeField, Range(1f, 180f)]
    private float proneAimAngle = 90f;

    [Tooltip("オンなら、地面にいる時だけほふくを開始できます")]
    [SerializeField] private bool requireGroundedToEnter = true;

    [Tooltip("足場から落ちて空中になった時、自動でほふくを解除します")]
    [SerializeField] private bool exitProneWhenAirborne = true;

    [Tooltip("空中解除を行う下向き速度です。小さな接地揺れでは解除されません")]
    [SerializeField, Min(0f)]
    private float airborneExitVelocity = 0.1f;

    [Header("ほふく時のCollider")]
    [Tooltip("オンなら、ほふく中にBody Colliderのサイズを縮小します")]
    [SerializeField] private bool resizeColliderWhileProne = true;

    [Tooltip("通常時の高さに掛ける倍率です。0.5なら高さが半分になります")]
    [SerializeField, Range(0.1f, 1f)]
    private float proneColliderHeightMultiplier = 0.5f;

    [Tooltip("通常時の横幅に掛ける倍率です。基本は1のままでOKです")]
    [SerializeField, Range(0.1f, 1f)]
    private float proneColliderWidthMultiplier = 1f;

    [Tooltip("オンなら、Colliderの底面位置を変えずに上側だけを縮めます")]
    [SerializeField] private bool preserveColliderBottom = true;

    [Header("立ち上がり判定")]
    [Tooltip("立ち上がりを妨げる地面・壁・天井のLayerです。空欄ならPlayerMoveのGround Layerを使用します")]
    [SerializeField] private LayerMask standUpObstacleLayers;

    [Tooltip("通常Colliderの上端より、さらにこの距離だけ余裕がある時に立ち上がれます")]
    [SerializeField, Min(0f)]
    private float standUpExtraClearance = 0.02f;

    [Tooltip("頭上判定Boxの左右を少しだけ内側にします。壁との接触を天井と誤判定する時に少し上げます")]
    [SerializeField, Min(0f)]
    private float standUpCheckSideInset = 0.01f;

    [Tooltip("Trigger Colliderも立ち上がりを妨げる障害物として扱うかどうか")]
    [SerializeField] private bool includeTriggerObstacles;

    [Tooltip("Sceneビューへ頭上判定範囲を表示します")]
    [SerializeField] private bool showStandUpCheckGizmo = true;

    [Header("入力を禁止する画面（任意）")]
    [Tooltip("インベントリやミッション画面など、開いている間はAlt入力を受け付けないPanelを設定できます")]
    [SerializeField] private GameObject[] panelsThatBlockProne;

    [Header("診断ログ")]
    [Tooltip("Alt入力、開始、解除、立ち上がれない理由をConsoleへ表示します")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("開始時に参照状態とCollider設定を詳しく表示します")]
    [SerializeField] private bool showDetailedDiagnostics = true;

    public bool IsProne { get; private set; }
    public bool LockedFacingRight { get; private set; } = true;
    public float ProneAimAngle => proneAimAngle;
    public float ProneMoveSpeedMultiplier => proneMoveSpeedMultiplier;
    public Collider2D BodyCollider => bodyCollider;

    public event Action<bool> ProneStateChanged;
    public event Action<Collider2D> StandUpBlocked;

    private enum SupportedColliderType
    {
        None,
        Box,
        Capsule
    }

    private SupportedColliderType supportedColliderType;
    private BoxCollider2D boxBodyCollider;
    private CapsuleCollider2D capsuleBodyCollider;

    private Vector2 standingColliderSize;
    private Vector2 standingColliderOffset;
    private Vector2 proneColliderSize;
    private Vector2 proneColliderOffset;
    private bool hasCachedStandingCollider;

    private Vector2 lastStandCheckCenter;
    private Vector2 lastStandCheckSize;
    private float lastStandCheckAngle;
    private bool hasStandCheckShape;
    private bool lastStandCheckWasBlocked;

    private void Awake()
    {
        FindReferences();
        CacheStandingColliderValues();

        if (showDetailedDiagnostics)
        {
            LogDiagnostics("Awake");
        }
    }

    private void OnEnable()
    {
        FindReferences();

        if (!IsProne)
        {
            CacheStandingColliderValues();
        }

        if (showDetailedDiagnostics)
        {
            LogDiagnostics("OnEnable");
        }
    }

    private void Start()
    {
        if (showDetailedDiagnostics)
        {
            LogDiagnostics("Start");
        }
    }

    private void Update()
    {
        FindReferences();

        if (IsProne)
        {
            MaintainProneState();
        }

        if (!WasProneKeyPressed())
        {
            return;
        }

        Log(
            $"ほふくキー入力を検出しました。Key={proneKey}, " +
            $"現在IsProne={IsProne}"
        );

        if (TryGetOpenBlockingPanel(out GameObject blockingPanel))
        {
            LogWarning(
                "ほふく入力は受け取りましたが、入力禁止Panelが開いています: " +
                GetTransformPath(blockingPanel.transform)
            );
            return;
        }

        if (IsProne)
        {
            TryExitProneInternal(
                false,
                "Altキーで解除しました。"
            );
        }
        else
        {
            TryEnterProne();
        }
    }

    private void OnDisable()
    {
        TryExitProneInternal(
            true,
            "PlayerProneControllerが無効になりました。"
        );
    }

    private void OnDestroy()
    {
        TryExitProneInternal(
            true,
            "PlayerProneControllerが破棄されました。"
        );
    }

    public bool TryEnterProne()
    {
        FindReferences();

        if (IsProne)
        {
            Log("すでにほふく中です。");
            return true;
        }

        if (!CanEnterProne(out string reason))
        {
            LogWarning("ほふくを開始できません: " + reason);
            LogDiagnostics("開始失敗時");
            return false;
        }

        // ほふく開始前のCollider値を保存します。
        // Inspectorから通常Colliderを変更した場合も、次回開始時に反映されます。
        CacheStandingColliderValues();

        if (resizeColliderWhileProne &&
            !ApplyProneCollider())
        {
            LogWarning(
                "ほふく用Colliderへ変更できなかったため、ほふくを開始しません。"
            );
            return false;
        }

        LockedFacingRight = playerMove.IsFacingRight;
        IsProne = true;

        playerMove.SetFacingDirection(
            LockedFacingRight ? 1f : -1f
        );

        playerMove.SetFacingLocked(true);
        playerMove.SetJumpLocked(true);

        weightController.SetProneState(
            true,
            proneMoveSpeedMultiplier
        );

        StopClimbingIfNeeded();
        ProneStateChanged?.Invoke(true);

        Log(
            "ほふく開始成功: " +
            $"LockedFacingRight={LockedFacingRight}, " +
            $"SpeedMultiplier={proneMoveSpeedMultiplier:0.###}, " +
            $"AimAngle={proneAimAngle:0.###}, " +
            $"Grounded={playerMove.IsGrounded}, " +
            $"Collider={GetColliderSummary()}"
        );

        return true;
    }

    /// <summary>
    /// 頭上に空きがある場合だけ、ほふくを解除します。
    /// 解除できた場合はtrueを返します。
    /// </summary>
    public bool TryExitProne()
    {
        return TryExitProneInternal(
            false,
            "外部処理から解除しました。"
        );
    }

    /// <summary>
    /// 既存のButtonや外部スクリプトとの互換用です。
    /// 頭上に障害物がある場合は解除しません。
    /// </summary>
    public void ExitProne()
    {
        TryExitProne();
    }

    public void SetProne(bool prone)
    {
        if (prone)
        {
            TryEnterProne();
        }
        else
        {
            TryExitProneInternal(
                false,
                "SetProne(false)で解除しました。"
            );
        }
    }

    /// <summary>
    /// 現在、通常の立ちColliderへ戻せるか確認します。
    /// 頭上にある最初の障害物をblockingColliderへ返します。
    /// </summary>
    public bool CanStandUp(out Collider2D blockingCollider)
    {
        blockingCollider = null;
        lastStandCheckWasBlocked = false;

        if (!resizeColliderWhileProne ||
            !IsProne ||
            bodyCollider == null ||
            !hasCachedStandingCollider)
        {
            return true;
        }

        if (!TryBuildStandUpCheckShape(
                out Vector2 center,
                out Vector2 size,
                out float angle))
        {
            return true;
        }

        lastStandCheckCenter = center;
        lastStandCheckSize = size;
        lastStandCheckAngle = angle;
        hasStandCheckShape = true;

        LayerMask obstacleMask = GetStandUpObstacleMask();

        if (obstacleMask.value == 0)
        {
            LogWarning(
                "Stand Up Obstacle LayersとPlayerMoveのGround Layerが空です。" +
                "頭上障害物を判定できません。"
            );
            return true;
        }

        Collider2D[] overlaps = Physics2D.OverlapBoxAll(
            center,
            size,
            angle,
            obstacleMask
        );

        foreach (Collider2D overlap in overlaps)
        {
            if (!IsValidStandUpObstacle(overlap))
            {
                continue;
            }

            blockingCollider = overlap;
            lastStandCheckWasBlocked = true;
            return false;
        }

        return true;
    }

    [ContextMenu("Log Stand Up Check")]
    public void LogStandUpCheck()
    {
        if (CanStandUp(out Collider2D blockingCollider))
        {
            Log(
                "立ち上がり判定: 頭上に障害物はありません。立ち上がれます。"
            );
            return;
        }

        LogWarning(
            "立ち上がり判定: 頭上に障害物があります。" +
            GetObstacleSummary(blockingCollider)
        );
    }

    [ContextMenu("Recache Standing Collider")]
    public void RecacheStandingCollider()
    {
        if (IsProne)
        {
            LogWarning(
                "ほふく中は通常Collider値を再保存できません。" +
                "一度、障害物のない場所で立ち上がってください。"
            );
            return;
        }

        FindReferences();
        CacheStandingColliderValues();
        Log("通常Collider値を再保存しました: " + GetColliderSummary());
    }

    private bool CanEnterProne(out string reason)
    {
        reason = string.Empty;

        if (playerMove == null)
        {
            reason = "PlayerMove参照がありません。";
            return false;
        }

        if (weightController == null)
        {
            reason = "PlayerWeightController参照がありません。";
            return false;
        }

        if (playerRigidbody == null)
        {
            reason = "Rigidbody2D参照がありません。";
            return false;
        }

        if (!playerMove.enabled)
        {
            reason = "PlayerMoveが無効です。";
            return false;
        }

        if (playerHealth != null && playerHealth.IsDead)
        {
            reason = "プレイヤーが死亡中です。";
            return false;
        }

        if (requireGroundedToEnter && !playerMove.IsGrounded)
        {
            reason =
                "接地判定がfalseです。PlayerMoveのGround Layer、" +
                "Ground Check Distance、Collider位置を確認してください。";
            return false;
        }

        if (wallClimbController != null &&
            wallClimbController.IsWallClimbing)
        {
            reason = "壁登り中です。";
            return false;
        }

        if (ropeClimbController != null &&
            ropeClimbController.IsClimbing)
        {
            reason = "ロープ登り中です。";
            return false;
        }

        if (resizeColliderWhileProne)
        {
            if (bodyCollider == null)
            {
                reason = "縮小対象のBody Colliderが見つかりません。";
                return false;
            }

            if (bodyCollider.isTrigger)
            {
                reason = "Body ColliderにIs TriggerがONになっています。";
                return false;
            }

            if (supportedColliderType == SupportedColliderType.None)
            {
                reason =
                    "Body ColliderはBoxCollider2DまたはCapsuleCollider2Dにしてください。";
                return false;
            }
        }

        reason = "OK";
        return true;
    }

    private void MaintainProneState()
    {
        if (playerHealth != null && playerHealth.IsDead)
        {
            TryExitProneInternal(
                true,
                "死亡したため強制解除しました。"
            );
            return;
        }

        if (playerMove == null)
        {
            TryExitProneInternal(
                true,
                "PlayerMove参照を失ったため強制解除しました。"
            );
            return;
        }

        playerMove.SetFacingDirection(
            LockedFacingRight ? 1f : -1f
        );

        playerMove.SetFacingLocked(true);
        playerMove.SetJumpLocked(true);

        StopClimbingIfNeeded();

        if (!exitProneWhenAirborne ||
            playerRigidbody == null ||
            playerMove.IsGrounded)
        {
            return;
        }

        if (playerRigidbody.linearVelocity.y <
            -airborneExitVelocity)
        {
            TryExitProneInternal(
                true,
                "空中へ落下したため強制解除しました。" +
                $" VerticalVelocity={playerRigidbody.linearVelocity.y:0.###}"
            );
        }
    }

    private bool TryExitProneInternal(
        bool forceRestore,
        string reason)
    {
        bool wasProne = IsProne;

        if (!wasProne && !forceRestore)
        {
            return true;
        }

        if (wasProne && !forceRestore &&
            !CanStandUp(out Collider2D blockingCollider))
        {
            string obstacleSummary = GetObstacleSummary(
                blockingCollider
            );

            LogWarning(
                "頭上に障害物があるため立ち上がれません。" +
                obstacleSummary
            );

            StandUpBlocked?.Invoke(blockingCollider);
            return false;
        }

        // Colliderを先に戻し、その後に移動・向き・Animator状態を解除します。
        RestoreStandingCollider();
        IsProne = false;

        if (playerMove != null)
        {
            playerMove.SetFacingLocked(false);
            playerMove.SetJumpLocked(false);
        }

        if (weightController != null)
        {
            weightController.SetProneState(false, 1f);
        }

        if (wasProne)
        {
            ProneStateChanged?.Invoke(false);
            Log("ほふく解除: " + reason);
        }

        return true;
    }

    private bool ApplyProneCollider()
    {
        if (!resizeColliderWhileProne)
        {
            return true;
        }

        if (!hasCachedStandingCollider)
        {
            CacheStandingColliderValues();
        }

        if (!hasCachedStandingCollider)
        {
            return false;
        }

        switch (supportedColliderType)
        {
            case SupportedColliderType.Box:
                if (boxBodyCollider == null)
                {
                    return false;
                }

                boxBodyCollider.size = proneColliderSize;
                boxBodyCollider.offset = proneColliderOffset;
                break;

            case SupportedColliderType.Capsule:
                if (capsuleBodyCollider == null)
                {
                    return false;
                }

                capsuleBodyCollider.size = proneColliderSize;
                capsuleBodyCollider.offset = proneColliderOffset;
                break;

            default:
                return false;
        }

        Physics2D.SyncTransforms();
        return true;
    }

    private void RestoreStandingCollider()
    {
        if (!resizeColliderWhileProne ||
            !hasCachedStandingCollider)
        {
            return;
        }

        switch (supportedColliderType)
        {
            case SupportedColliderType.Box:
                if (boxBodyCollider != null)
                {
                    boxBodyCollider.size = standingColliderSize;
                    boxBodyCollider.offset = standingColliderOffset;
                }
                break;

            case SupportedColliderType.Capsule:
                if (capsuleBodyCollider != null)
                {
                    capsuleBodyCollider.size = standingColliderSize;
                    capsuleBodyCollider.offset = standingColliderOffset;
                }
                break;
        }

        Physics2D.SyncTransforms();
    }

    private void CacheStandingColliderValues()
    {
        FindReferences();
        ResolveSupportedCollider();

        if (bodyCollider == null ||
            supportedColliderType == SupportedColliderType.None)
        {
            hasCachedStandingCollider = false;
            return;
        }

        switch (supportedColliderType)
        {
            case SupportedColliderType.Box:
                standingColliderSize = boxBodyCollider.size;
                standingColliderOffset = boxBodyCollider.offset;
                break;

            case SupportedColliderType.Capsule:
                standingColliderSize = capsuleBodyCollider.size;
                standingColliderOffset = capsuleBodyCollider.offset;
                break;

            default:
                hasCachedStandingCollider = false;
                return;
        }

        standingColliderSize.x = Mathf.Max(
            0.01f,
            standingColliderSize.x
        );

        standingColliderSize.y = Mathf.Max(
            0.01f,
            standingColliderSize.y
        );

        proneColliderSize = new Vector2(
            Mathf.Max(
                0.01f,
                standingColliderSize.x *
                proneColliderWidthMultiplier
            ),
            Mathf.Max(
                0.01f,
                standingColliderSize.y *
                proneColliderHeightMultiplier
            )
        );

        proneColliderOffset = standingColliderOffset;

        if (preserveColliderBottom)
        {
            float removedHeight =
                standingColliderSize.y -
                proneColliderSize.y;

            proneColliderOffset.y -= removedHeight * 0.5f;
        }

        hasCachedStandingCollider = true;
    }

    private void ResolveSupportedCollider()
    {
        boxBodyCollider = bodyCollider as BoxCollider2D;
        capsuleBodyCollider = bodyCollider as CapsuleCollider2D;

        if (boxBodyCollider != null)
        {
            supportedColliderType = SupportedColliderType.Box;
            return;
        }

        if (capsuleBodyCollider != null)
        {
            supportedColliderType = SupportedColliderType.Capsule;
            return;
        }

        supportedColliderType = SupportedColliderType.None;
    }

    private bool TryBuildStandUpCheckShape(
        out Vector2 worldCenter,
        out Vector2 worldSize,
        out float worldAngle)
    {
        worldCenter = Vector2.zero;
        worldSize = Vector2.zero;
        worldAngle = 0f;
        hasStandCheckShape = false;

        if (bodyCollider == null ||
            !hasCachedStandingCollider)
        {
            return false;
        }

        float proneTop =
            proneColliderOffset.y +
            proneColliderSize.y * 0.5f;

        float standingTop =
            standingColliderOffset.y +
            standingColliderSize.y * 0.5f +
            standUpExtraClearance;

        float localCheckHeight = standingTop - proneTop;

        if (localCheckHeight <= 0.001f)
        {
            return false;
        }

        float localCheckWidth = Mathf.Max(
            0.01f,
            standingColliderSize.x -
            standUpCheckSideInset * 2f
        );

        Vector2 localCenter = new Vector2(
            standingColliderOffset.x,
            (proneTop + standingTop) * 0.5f
        );

        Transform colliderTransform = bodyCollider.transform;
        Vector3 lossyScale = colliderTransform.lossyScale;

        worldCenter = colliderTransform.TransformPoint(
            localCenter
        );

        worldSize = new Vector2(
            localCheckWidth * Mathf.Abs(lossyScale.x),
            localCheckHeight * Mathf.Abs(lossyScale.y)
        );

        worldAngle = colliderTransform.eulerAngles.z;
        hasStandCheckShape = true;
        return true;
    }

    private bool IsValidStandUpObstacle(Collider2D obstacle)
    {
        if (obstacle == null ||
            obstacle == bodyCollider)
        {
            return false;
        }

        Transform obstacleTransform = obstacle.transform;

        if (obstacleTransform == transform ||
            obstacleTransform.IsChildOf(transform))
        {
            return false;
        }

        if (!includeTriggerObstacles && obstacle.isTrigger)
        {
            return false;
        }

        return true;
    }

    private LayerMask GetStandUpObstacleMask()
    {
        if (standUpObstacleLayers.value != 0)
        {
            return standUpObstacleLayers;
        }

        return playerMove != null
            ? playerMove.groundLayer
            : default;
    }

    private void StopClimbingIfNeeded()
    {
        if (wallClimbController != null &&
            wallClimbController.IsWallClimbing)
        {
            Log("壁登りを終了してほふく状態を維持します。");
            wallClimbController.StopWallClimbNow();
        }

        if (ropeClimbController != null &&
            ropeClimbController.IsClimbing)
        {
            Log("ロープ登りを終了してほふく状態を維持します。");
            ropeClimbController.StopClimbingNow();
        }
    }

    private bool WasProneKeyPressed()
    {
        if (Input.GetKeyDown(proneKey))
        {
            return true;
        }

        return allowRightAlt &&
            proneKey != KeyCode.RightAlt &&
            Input.GetKeyDown(KeyCode.RightAlt);
    }

    private bool TryGetOpenBlockingPanel(
        out GameObject openPanel)
    {
        openPanel = null;

        if (panelsThatBlockProne == null)
        {
            return false;
        }

        foreach (GameObject panel in panelsThatBlockProne)
        {
            if (panel != null && panel.activeInHierarchy)
            {
                openPanel = panel;
                return true;
            }
        }

        return false;
    }

    private void FindReferences()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        if (weightController == null)
        {
            weightController =
                GetComponent<PlayerWeightController>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<CharacterHealth>();
        }

        if (wallClimbController == null)
        {
            wallClimbController =
                GetComponent<WallClimbController>();
        }

        if (ropeClimbController == null)
        {
            ropeClimbController =
                GetComponent<RopeClimbController>();
        }

        if (bodyCollider == null)
        {
            bodyCollider = FindPreferredBodyCollider();
        }

        ResolveSupportedCollider();
    }

    private Collider2D FindPreferredBodyCollider()
    {
        Collider2D[] rootColliders =
            GetComponents<Collider2D>();

        Collider2D preferred = FindPreferredColliderInArray(
            rootColliders
        );

        if (preferred != null)
        {
            return preferred;
        }

        Collider2D[] childColliders =
            GetComponentsInChildren<Collider2D>(true);

        return FindPreferredColliderInArray(childColliders);
    }

    private Collider2D FindPreferredColliderInArray(
        Collider2D[] colliders)
    {
        if (colliders == null)
        {
            return null;
        }

        foreach (Collider2D collider in colliders)
        {
            if (collider == null ||
                collider.isTrigger)
            {
                continue;
            }

            if (collider is BoxCollider2D ||
                collider is CapsuleCollider2D)
            {
                return collider;
            }
        }

        return null;
    }

    [ContextMenu("Log Prone Diagnostics")]
    public void LogProneDiagnostics()
    {
        FindReferences();
        LogDiagnostics("手動診断");
    }

    private void LogDiagnostics(string phase)
    {
        if (!showDebugLogs)
        {
            return;
        }

        StringBuilder builder = new StringBuilder();
        builder.AppendLine(
            $"========== ほふく診断: {phase} =========="
        );
        builder.AppendLine(
            $"Object={GetTransformPath(transform)} / " +
            $"enabled={enabled} / active={gameObject.activeInHierarchy}"
        );
        builder.AppendLine(
            $"IsProne={IsProne} / LockedFacingRight={LockedFacingRight} / " +
            $"ProneKey={proneKey} / AllowRightAlt={allowRightAlt}"
        );
        builder.AppendLine(
            $"PlayerMove={(playerMove != null ? "OK" : "未取得")} / " +
            $"enabled={(playerMove != null ? playerMove.enabled.ToString() : "不明")} / " +
            $"Grounded={(playerMove != null ? playerMove.IsGrounded.ToString() : "不明")} / " +
            $"FacingRight={(playerMove != null ? playerMove.IsFacingRight.ToString() : "不明")}"
        );
        builder.AppendLine(
            $"PlayerWeightController={(weightController != null ? "OK" : "未取得")} / " +
            $"Rigidbody2D={(playerRigidbody != null ? "OK" : "未取得")} / " +
            $"Velocity={(playerRigidbody != null ? playerRigidbody.linearVelocity.ToString() : "不明")}"
        );
        builder.AppendLine(
            $"CharacterHealth={(playerHealth != null ? "OK" : "未取得")} / " +
            $"IsDead={(playerHealth != null ? playerHealth.IsDead.ToString() : "不明")}"
        );
        builder.AppendLine(
            $"WallClimb={(wallClimbController != null ? wallClimbController.IsWallClimbing.ToString() : "未取得")} / " +
            $"RopeClimb={(ropeClimbController != null ? ropeClimbController.IsClimbing.ToString() : "未取得")}"
        );
        builder.AppendLine(
            $"ResizeCollider={resizeColliderWhileProne} / " +
            $"BodyCollider={(bodyCollider != null ? GetTransformPath(bodyCollider.transform) : "未取得")} / " +
            $"Type={supportedColliderType} / Cached={hasCachedStandingCollider}"
        );
        builder.AppendLine(
            $"StandingSize={standingColliderSize} / StandingOffset={standingColliderOffset} / " +
            $"ProneSize={proneColliderSize} / ProneOffset={proneColliderOffset}"
        );
        builder.AppendLine(
            $"StandUpMask={GetStandUpObstacleMask().value} / " +
            $"ExtraClearance={standUpExtraClearance:0.###} / " +
            $"SideInset={standUpCheckSideInset:0.###}"
        );

        if (TryGetOpenBlockingPanel(out GameObject openPanel))
        {
            builder.AppendLine(
                "BlockingPanel=" +
                GetTransformPath(openPanel.transform)
            );
        }
        else
        {
            builder.AppendLine("BlockingPanel=なし");
        }

        Debug.Log(builder.ToString(), this);
    }

    private string GetColliderSummary()
    {
        if (bodyCollider == null)
        {
            return "未設定";
        }

        return
            $"{bodyCollider.GetType().Name} / " +
            $"StandingSize={standingColliderSize} / " +
            $"ProneSize={proneColliderSize} / " +
            $"StandingOffset={standingColliderOffset} / " +
            $"ProneOffset={proneColliderOffset}";
    }

    private static string GetObstacleSummary(
        Collider2D obstacle)
    {
        if (obstacle == null)
        {
            return " 障害物Colliderは取得できませんでした。";
        }

        return
            $" Object={GetTransformPath(obstacle.transform)}, " +
            $"Collider={obstacle.GetType().Name}, " +
            $"Layer={LayerMask.LayerToName(obstacle.gameObject.layer)}, " +
            $"IsTrigger={obstacle.isTrigger}";
    }

    private static string GetTransformPath(Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private void Log(string message)
    {
        if (!showDebugLogs ||
            string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.Log(
            $"[PlayerProneController] {message}",
            this
        );
    }

    private void LogWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.LogWarning(
            $"[PlayerProneController] {message}",
            this
        );
    }

    private void OnValidate()
    {
        proneMoveSpeedMultiplier = Mathf.Clamp(
            proneMoveSpeedMultiplier,
            0.05f,
            1f
        );

        proneAimAngle = Mathf.Clamp(
            proneAimAngle,
            1f,
            180f
        );

        airborneExitVelocity = Mathf.Max(
            0f,
            airborneExitVelocity
        );

        proneColliderHeightMultiplier = Mathf.Clamp(
            proneColliderHeightMultiplier,
            0.1f,
            1f
        );

        proneColliderWidthMultiplier = Mathf.Clamp(
            proneColliderWidthMultiplier,
            0.1f,
            1f
        );

        standUpExtraClearance = Mathf.Max(
            0f,
            standUpExtraClearance
        );

        standUpCheckSideInset = Mathf.Max(
            0f,
            standUpCheckSideInset
        );

        if (standUpObstacleLayers.value == 0 &&
            playerMove != null)
        {
            standUpObstacleLayers = playerMove.groundLayer;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!showStandUpCheckGizmo)
        {
            return;
        }

        if (Application.isPlaying)
        {
            if (!hasStandCheckShape && IsProne)
            {
                TryBuildStandUpCheckShape(
                    out lastStandCheckCenter,
                    out lastStandCheckSize,
                    out lastStandCheckAngle
                );
            }
        }
        else
        {
            FindReferences();

            if (!hasCachedStandingCollider)
            {
                CacheStandingColliderValues();
            }

            TryBuildStandUpCheckShape(
                out lastStandCheckCenter,
                out lastStandCheckSize,
                out lastStandCheckAngle
            );
        }

        if (!hasStandCheckShape)
        {
            return;
        }

        Gizmos.color = lastStandCheckWasBlocked
            ? Color.red
            : Color.yellow;

        Matrix4x4 previousMatrix = Gizmos.matrix;

        Gizmos.matrix = Matrix4x4.TRS(
            lastStandCheckCenter,
            Quaternion.Euler(0f, 0f, lastStandCheckAngle),
            Vector3.one
        );

        Gizmos.DrawWireCube(
            Vector3.zero,
            lastStandCheckSize
        );

        Gizmos.matrix = previousMatrix;
    }
}
