using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Playerから物体へロープを接続して引っ張ります。
/// 1キーで武器モード、2キーでロープモードへ切り替え、
/// ロープモード中はEで短く、Rで長くします。
/// </summary>
[DefaultExecutionOrder(-120)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerRopePullController : MonoBehaviour
{
    [Header("Player参照")]
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private CharacterHealth playerHealth;
    [SerializeField] private PlayerEquipmentVisualController equipmentVisualController;
    [SerializeField] private StoneThrower stoneThrower;
    [SerializeField] private PlayerProneController proneController;
    [SerializeField] private RopeClimbController ropeClimbController;
    [SerializeField] private WallClimbController wallClimbController;

    [Tooltip("Player側でロープを持つ位置です。未設定ならPlayer中心＋Offsetを使います")]
    [SerializeField] private Transform playerRopeHoldPoint;
    [SerializeField] private Vector2 fallbackHoldOffset = new Vector2(0f, 0.45f);

    [Header("入力")]
    [SerializeField] private KeyCode weaponModeKey = KeyCode.Alpha1;
    [SerializeField] private KeyCode ropeModeKey = KeyCode.Alpha2;
    [SerializeField] private KeyCode interactKey = KeyCode.F;
    [SerializeField] private KeyCode shortenKey = KeyCode.E;
    [SerializeField] private KeyCode lengthenKey = KeyCode.R;

    [Header("接続対象")]
    [Tooltip("空欄なら全LayerからRopePullTargetを探します")]
    [SerializeField] private LayerMask pullableObjectLayers;

    [SerializeField, Min(0.1f)] private float interactionRadius = 2f;

    [Header("ロープ長")]
    [SerializeField, Min(0.1f)] private float minimumRopeLength = 0.8f;
    [SerializeField, Min(0.1f)] private float maximumRopeLength = 14f;
    [SerializeField, Min(0f)] private float initialSlack = 0.35f;
    [SerializeField, Min(0.01f)] private float ropeLengthChangeSpeed = 3f;

    [Header("ロープ物理")]
    [Tooltip("オンなら物体からPlayerにも力が伝わります。通常はオフの方が操作が安定します")]
    [SerializeField] private bool allowObjectToPullPlayer;

    [Tooltip("ロープに掛かる力がこの値を超えると切れます。0以下なら切れません")]
    [SerializeField, Min(0f)] private float ropeBreakForce = 5000f;

    [SerializeField] private bool enableCollisionBetweenBodies;

    [Header("ロープとGroundの当たり判定")]
    [Tooltip("オンなら、ロープを物理セグメントに分割してGround・壁・段差へ衝突させます")]
    [SerializeField] private bool usePhysicalCollisionRope = true;

    [Tooltip("ロープと衝突させたいGround系Layerです。空欄ならPlayerMoveのGround Layerを使います")]
    [SerializeField] private LayerMask ropeGroundLayers;

    [Tooltip("推奨は専用のRope Layerです。存在しない場合はPlayerと同じLayerを使います")]
    [SerializeField] private string ropeSegmentLayerName = "Rope";

    [Tooltip("Physics 2DのLayer Collision MatrixでRopeとGroundの衝突がOFFでも、実行中だけ自動的にONへします")]
    [SerializeField] private bool forceEnableGroundCollisionAtRuntime = true;

    [Tooltip("短いほど滑らかに地面へ沿いますが、生成される物理Objectが増えます")]
    [SerializeField, Min(0.08f)] private float preferredPhysicalSegmentLength = 0.35f;

    [SerializeField, Range(2, 64)] private int minimumPhysicalSegmentCount = 3;
    [SerializeField, Range(2, 64)] private int maximumPhysicalSegmentCount = 40;

    [Tooltip("物理ロープの当たり判定の太さです")]
    [SerializeField, Min(0.01f)] private float physicalRopeThickness = 0.08f;

    [SerializeField, Min(0.001f)] private float physicalSegmentMass = 0.04f;
    [SerializeField, Min(0f)] private float physicalSegmentGravityScale = 1f;
    [SerializeField, Min(0f)] private float physicalSegmentLinearDamping = 1.5f;
    [SerializeField, Min(0f)] private float physicalSegmentAngularDamping = 0.5f;
    [SerializeField] private PhysicsMaterial2D physicalRopeMaterial;

    [Tooltip("Player自身と物理ロープの衝突を無視して、手元で暴れにくくします")]
    [SerializeField] private bool ignorePhysicalRopeCollisionWithPlayer = true;

    [Tooltip("引っ張る物体自身と物理ロープの衝突を無視して、接続部分を安定させます")]
    [SerializeField] private bool ignorePhysicalRopeCollisionWithTarget = true;

    [Header("Line Renderer")]
    [SerializeField] private LineRenderer ropeLineRenderer;
    [SerializeField, Range(4, 40)] private int ropeVisualPointCount = 18;
    [SerializeField, Min(0.001f)] private float ropeWidth = 0.06f;
    [SerializeField] private Color ropeColor = new Color(0.35f, 0.2f, 0.08f, 1f);
    [SerializeField, Min(0f)] private float sagMultiplier = 0.75f;
    [SerializeField, Min(0f)] private float maximumVisualSag = 3f;
    [SerializeField, Min(0.01f)] private float visualFollowSpeed = 18f;
    [SerializeField] private string ropeSortingLayerName = "Default";
    [SerializeField] private int ropeSortingOrder = 10;

    [Header("Text表示")]
    [SerializeField] private TMP_Text modeText;
    [SerializeField] private string attachPrompt = "F：ロープをつなぐ";
    [SerializeField] private string detachPrompt = "F：切り離す";

    [Tooltip("同じ物がCarryableObject2Dにも対応している時の表示です")]
    [SerializeField] private string carryableAttachPrompt =
        "E：持つ　F：ロープをつなぐ";

    [Tooltip("持てる物へロープが接続済みの時の表示です")]
    [SerializeField] private string carryableDetachPrompt =
        "E：持つ　F：切り離す";
    [SerializeField] private string weaponModeLabel = "1：武器モード　2：ロープモード";
    [SerializeField] private string ropeModeLabel = "ロープモード　E：短くする　R：長くする　1：武器";

    [Header("入力を止めるPanel（任意）")]
    [SerializeField] private GameObject[] panelsThatBlockInput;

    [Header("安全設定")]
    [SerializeField] private bool detachWhenPlayerDies = true;

    [Tooltip("別の設置ロープを登り始めた時、引っ張り用ロープを切り離します")]
    [SerializeField] private bool detachWhenClimbing = true;

    [Tooltip("オンにした場合だけ、壁登り開始時に引っ張り用ロープを切り離します。通常はオフにしてください")]
    [SerializeField] private bool detachWhenWallClimbing = false;

    [SerializeField] private bool blockRopeModeWhileProne = true;

    [Header("ロープサウンド")]
    [Tooltip("ロープを物体へ接続した時の音です")]
    [SerializeField] private AudioClip attachSound;

    [Tooltip("Fキーでロープを切り離した時の音です")]
    [SerializeField] private AudioClip detachSound;

    [Tooltip("物体を地面で引きずっている間にループ再生する音です")]
    [SerializeField] private AudioClip draggingLoopSound;

    [Tooltip("未設定なら専用AudioSourceを自動生成します")]
    [SerializeField] private AudioSource oneShotAudioSource;

    [Tooltip("未設定なら専用AudioSourceを自動生成します")]
    [SerializeField] private AudioSource draggingAudioSource;

    [SerializeField, Range(0f, 1f)] private float attachSoundVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float detachSoundVolume = 0.9f;
    [SerializeField, Range(0f, 1f)] private float draggingSoundVolume = 0.75f;
    [SerializeField, Range(0f, 1f)] private float soundSpatialBlend = 0.7f;

    [Header("引きずり音の判定")]
    [Tooltip("床や地面に使用しているLayerを設定します。空欄ならPlayerMoveのGround Layerを使用します")]
    [SerializeField] private LayerMask dragSurfaceLayers;

    [Tooltip("この速度以上で物体が動いた時に引きずり音を開始します")]
    [SerializeField, Min(0.01f)] private float minimumDraggingSpeed = 0.2f;

    [Tooltip("この速度で引きずり音が最大音量になります")]
    [SerializeField, Min(0.01f)] private float maximumDraggingSpeed = 4f;

    [Tooltip("ロープが張ったと判断する距離の余裕です")]
    [SerializeField, Min(0f)] private float ropeTensionTolerance = 0.15f;

    [Tooltip("物体の下方向へ地面を確認する距離です")]
    [SerializeField, Min(0.01f)] private float dragSurfaceCheckDistance = 0.12f;

    [Tooltip("引きずり音のフェード速度です")]
    [SerializeField, Min(0.01f)] private float draggingSoundFadeSpeed = 6f;

    [SerializeField, Range(0.1f, 3f)] private float minimumDraggingPitch = 0.85f;
    [SerializeField, Range(0.1f, 3f)] private float maximumDraggingPitch = 1.15f;
    [SerializeField] private bool requireTautRopeForDraggingSound = true;
    [SerializeField] private bool requireSurfaceContactForDraggingSound = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;
    [SerializeField] private bool showInteractionGizmo = true;

    public bool IsRopeMode => isRopeMode;
    public bool IsRopeControlLocked => ropeControlLocks.Count > 0;
    public bool IsRopeAttached =>
        attachedTarget != null && HasActiveRopeConstraint();
    public float CurrentRopeLength => currentRopeLength;
    public RopePullTarget AttachedTarget => attachedTarget;

    private RopePullTarget attachedTarget;
    private RopePullTarget promptTarget;
    private DistanceJoint2D ropeJoint;

    private GameObject physicalRopeObject;
    private RopePullPhysicalRope2D physicalRope;

    private GameObject anchorObject;
    private Rigidbody2D anchorRigidbody;

    private bool isRopeMode;
    private float currentRopeLength;

    // 物を持っている時など、外部機能からロープ操作を止めるowner方式のロックです。
    private readonly HashSet<object> ropeControlLocks =
        new HashSet<object>();

    private Vector3[] displayedRopePoints;
    private Material runtimeLineMaterial;

    private GameObject runtimeOneShotAudioObject;
    private GameObject runtimeDraggingAudioObject;

    private void Awake()
    {
        FindReferences();
        CreatePlayerAnchorIfNeeded();
        CreatePhysicalRopeIfNeeded();
        SetupLineRenderer();
        SetupAudioSources();
        SetRopeMode(false, true);
        RefreshModeText();
    }

    private void OnEnable()
    {
        FindReferences();
        CreatePlayerAnchorIfNeeded();
        CreatePhysicalRopeIfNeeded();
        SetupLineRenderer();
        SetupAudioSources();
        RefreshModeText();
    }

    private void Update()
    {
        FindReferences();

        if (attachedTarget != null &&
            (!attachedTarget.isActiveAndEnabled ||
             !HasActiveRopeConstraint()))
        {
            DetachRope(false, false);
        }

        UpdateDraggingSound();

        if (IsRopeControlLocked)
        {
            if (isRopeMode)
            {
                SetRopeMode(false, true);
            }

            ClearPromptTarget();
            UpdateStoneThrowLock();
            RefreshModeText();
            return;
        }

        if (HandleForcedStopConditions())
        {
            ClearPromptTarget();
            UpdateStoneThrowLock();
            return;
        }

        bool inputBlocked = IsInputBlocked();

        if (!inputBlocked)
        {
            HandleModeInput();
            RefreshPromptTarget();
            HandleInteractionInput();
            HandleRopeLengthInput();
        }
        else
        {
            ClearPromptTarget();
        }

        UpdateStoneThrowLock();
        RefreshModeText();
    }

    private void FixedUpdate()
    {
        Vector2 holdPosition = GetPlayerHoldPosition();

        if (anchorRigidbody != null && !allowObjectToPullPlayer)
        {
            anchorRigidbody.MovePosition(holdPosition);
            anchorRigidbody.linearVelocity = Vector2.zero;
            anchorRigidbody.angularVelocity = 0f;
        }

        if (physicalRope != null &&
            physicalRope.IsActive &&
            attachedTarget != null)
        {
            physicalRope.UpdateAnchors(
                holdPosition,
                attachedTarget.RopeAttachmentWorldPosition
            );
        }
    }

    private void LateUpdate()
    {
        UpdateRopeVisual();
    }

    private void OnDisable()
    {
        ClearPromptTarget();
        DetachRope(false, false);
        StopDraggingSoundImmediately();
        SetRopeMode(false, true);
        stoneThrower?.SetThrowControlLock(this, false);
    }

    private void OnDestroy()
    {
        if (anchorObject != null)
        {
            Destroy(anchorObject);
        }

        if (physicalRopeObject != null)
        {
            Destroy(physicalRopeObject);
        }

        if (runtimeLineMaterial != null)
        {
            Destroy(runtimeLineMaterial);
        }

        if (runtimeOneShotAudioObject != null)
        {
            Destroy(runtimeOneShotAudioObject);
        }

        if (runtimeDraggingAudioObject != null)
        {
            Destroy(runtimeDraggingAudioObject);
        }
    }

    public void SetWeaponMode()
    {
        SetRopeMode(false, false);
    }

    public void SetRopeMode()
    {
        if (IsRopeControlLocked)
        {
            Log("別の行動中のためロープモードへ切り替えられません。");
            return;
        }

        if (blockRopeModeWhileProne &&
            proneController != null &&
            proneController.IsProne)
        {
            Log("ほふく中はロープモードへ切り替えられません。");
            return;
        }

        SetRopeMode(true, false);
    }

    /// <summary>
    /// 物を持つ機能などから、ロープ接続・モード切替・長さ変更を一時停止します。
    /// ownerごとに管理するため、別のロックが残っている間は解除されません。
    /// </summary>
    public void SetRopeControlLock(object owner, bool locked)
    {
        if (owner == null)
        {
            return;
        }

        bool changed = locked
            ? ropeControlLocks.Add(owner)
            : ropeControlLocks.Remove(owner);

        if (!changed)
        {
            return;
        }

        if (locked)
        {
            if (IsRopeAttached)
            {
                DetachRope(false, false);
            }

            SetRopeMode(false, true);
            ClearPromptTarget();
        }

        UpdateStoneThrowLock();
        RefreshModeText();
    }

    public void ToggleRopeMode(bool ropeMode)
    {
        if (ropeMode)
        {
            SetRopeMode();
        }
        else
        {
            SetWeaponMode();
        }
    }

    public bool AttachRope(RopePullTarget target)
    {
        if (IsRopeControlLocked)
        {
            return false;
        }

        if (target == null || !target.isActiveAndEnabled)
        {
            return false;
        }

        if (IsRopeAttached)
        {
            return attachedTarget == target;
        }

        if (!target.TryReserve(this))
        {
            Log($"{target.name} は別のロープで使用中です。");
            return false;
        }

        Rigidbody2D targetBody = target.TargetRigidbody;

        if (targetBody == null)
        {
            target.Release(this);
            Debug.LogWarning(
                $"[PlayerRopePullController] {target.name} にRigidbody2Dがありません。",
                target
            );
            return false;
        }

        if (targetBody.bodyType != RigidbodyType2D.Dynamic)
        {
            targetBody.bodyType = RigidbodyType2D.Dynamic;
        }

        CreatePlayerAnchorIfNeeded();

        attachedTarget = target;
        currentRopeLength = Mathf.Clamp(
            Vector2.Distance(
                GetPlayerHoldPosition(),
                target.RopeAttachmentWorldPosition
            ) + initialSlack,
            minimumRopeLength,
            maximumRopeLength
        );

        bool created = usePhysicalCollisionRope
            ? CreatePhysicalRopeConstraint(targetBody)
            : CreateLegacyDistanceJoint(targetBody);

        if (!created)
        {
            attachedTarget = null;
            currentRopeLength = 0f;
            target.Release(this);

            Debug.LogWarning(
                $"[PlayerRopePullController] {target.name} へのロープ生成に失敗しました。",
                this
            );

            return false;
        }

        if (ropeLineRenderer != null)
        {
            ropeLineRenderer.enabled = true;
        }

        InitializeRopeVisualPoints();
        RefreshPromptTarget();
        UpdateStoneThrowLock();
        PlayOneShotAtTarget(attachSound, attachSoundVolume, target);

        Log($"ロープ接続: {target.name} / 長さ={currentRopeLength:0.00}");
        return true;
    }

    private bool CreateLegacyDistanceJoint(
        Rigidbody2D targetBody)
    {
        if (targetBody == null)
        {
            return false;
        }

        ropeJoint =
            targetBody.gameObject.AddComponent<DistanceJoint2D>();

        ropeJoint.autoConfigureDistance = false;
        ropeJoint.autoConfigureConnectedAnchor = false;
        ropeJoint.enableCollision = enableCollisionBetweenBodies;
        ropeJoint.maxDistanceOnly = true;
        ropeJoint.distance = currentRopeLength;
        ropeJoint.anchor =
            ropeJoint.transform.InverseTransformPoint(
                attachedTarget.RopeAttachmentWorldPosition
            );

        if (allowObjectToPullPlayer)
        {
            ropeJoint.connectedBody = playerRigidbody;
            ropeJoint.connectedAnchor =
                playerRigidbody.transform.InverseTransformPoint(
                    GetPlayerHoldPosition()
                );
        }
        else
        {
            ropeJoint.connectedBody = anchorRigidbody;
            ropeJoint.connectedAnchor = Vector2.zero;
        }

        ropeJoint.breakForce = ropeBreakForce > 0f
            ? ropeBreakForce
            : Mathf.Infinity;

        ropeJoint.breakTorque = Mathf.Infinity;
        return ropeJoint != null;
    }

    private bool CreatePhysicalRopeConstraint(
        Rigidbody2D targetBody)
    {
        CreatePhysicalRopeIfNeeded();

        if (physicalRope == null ||
            targetBody == null ||
            attachedTarget == null)
        {
            return false;
        }

        Rigidbody2D startBody = allowObjectToPullPlayer
            ? playerRigidbody
            : anchorRigidbody;

        if (startBody == null)
        {
            return false;
        }

        int segmentLayer = ResolvePhysicalRopeLayer();
        ValidatePhysicalRopeLayerCollision(segmentLayer);

        RopePullPhysicalRope2D.Settings settings =
            new RopePullPhysicalRope2D.Settings
            {
                SegmentLayer = segmentLayer,
                PreferredSegmentLength =
                    preferredPhysicalSegmentLength,
                MinimumSegmentCount =
                    minimumPhysicalSegmentCount,
                MaximumSegmentCount =
                    maximumPhysicalSegmentCount,
                Thickness = physicalRopeThickness,
                SegmentMass = physicalSegmentMass,
                GravityScale =
                    physicalSegmentGravityScale,
                LinearDamping =
                    physicalSegmentLinearDamping,
                AngularDamping =
                    physicalSegmentAngularDamping,
                BreakForce = ropeBreakForce,
                PhysicsMaterial = physicalRopeMaterial,
                IgnoredColliders =
                    BuildPhysicalRopeIgnoredColliders()
            };

        ropeJoint = null;

        return physicalRope.Build(
            startBody,
            GetPlayerHoldPosition(),
            targetBody,
            attachedTarget.RopeAttachmentWorldPosition,
            currentRopeLength,
            settings
        );
    }

    private Collider2D[] BuildPhysicalRopeIgnoredColliders()
    {
        List<Collider2D> ignored =
            new List<Collider2D>();

        if (ignorePhysicalRopeCollisionWithPlayer)
        {
            Collider2D[] playerColliders =
                GetComponentsInChildren<Collider2D>(true);

            foreach (Collider2D collider in playerColliders)
            {
                if (collider != null &&
                    !ignored.Contains(collider))
                {
                    ignored.Add(collider);
                }
            }
        }

        if (ignorePhysicalRopeCollisionWithTarget &&
            attachedTarget != null)
        {
            Collider2D[] targetColliders =
                attachedTarget.GetComponentsInChildren<
                    Collider2D
                >(true);

            foreach (Collider2D collider in targetColliders)
            {
                if (collider != null &&
                    !ignored.Contains(collider))
                {
                    ignored.Add(collider);
                }
            }
        }

        return ignored.ToArray();
    }

    private void CreatePhysicalRopeIfNeeded()
    {
        if (physicalRope != null)
        {
            return;
        }

        physicalRopeObject = new GameObject(
            "RopePull_PhysicalRope"
        );

        physicalRopeObject.hideFlags =
            HideFlags.HideInHierarchy;

        physicalRopeObject.transform.position = Vector3.zero;
        physicalRope =
            physicalRopeObject.AddComponent<
                RopePullPhysicalRope2D
            >();
    }

    private int ResolvePhysicalRopeLayer()
    {
        int configuredLayer = string.IsNullOrWhiteSpace(
            ropeSegmentLayerName
        )
            ? -1
            : LayerMask.NameToLayer(
                ropeSegmentLayerName.Trim()
            );

        if (configuredLayer >= 0)
        {
            return configuredLayer;
        }

        if (!string.IsNullOrWhiteSpace(ropeSegmentLayerName))
        {
            Debug.LogWarning(
                $"[PlayerRopePullController] Layer『{ropeSegmentLayerName}』が見つかりません。" +
                $"物理ロープにはPlayerと同じLayer『{LayerMask.LayerToName(gameObject.layer)}』を使用します。",
                this
            );
        }

        return gameObject.layer;
    }

    private void ValidatePhysicalRopeLayerCollision(
        int segmentLayer)
    {
        int groundMask = ropeGroundLayers.value;

        if (groundMask == 0 && playerMove != null)
        {
            groundMask = playerMove.groundLayer.value;
        }

        if (groundMask == 0)
        {
            Debug.LogWarning(
                "[PlayerRopePullController] Rope Ground Layersが空です。" +
                "Groundレイヤーを設定してください。",
                this
            );
            return;
        }

        for (int layer = 0; layer < 32; layer++)
        {
            if ((groundMask & (1 << layer)) == 0)
            {
                continue;
            }

            if (!Physics2D.GetIgnoreLayerCollision(
                    segmentLayer,
                    layer))
            {
                continue;
            }

            if (forceEnableGroundCollisionAtRuntime)
            {
                Physics2D.IgnoreLayerCollision(
                    segmentLayer,
                    layer,
                    false
                );

                Log(
                    $"実行中のLayer衝突をONにしました: " +
                    $"{LayerMask.LayerToName(segmentLayer)} × " +
                    $"{LayerMask.LayerToName(layer)}"
                );
            }
            else
            {
                Debug.LogWarning(
                    $"[PlayerRopePullController] Physics 2DのLayer Collision Matrixで、" +
                    $"『{LayerMask.LayerToName(segmentLayer)}』と" +
                    $"『{LayerMask.LayerToName(layer)}』の衝突がOFFです。" +
                    "この2つが衝突するようにチェックを入れてください。",
                    this
                );
            }
        }
    }

    private bool HasActiveRopeConstraint()
    {
        return ropeJoint != null ||
               (physicalRope != null &&
                physicalRope.IsValid);
    }

    public void DetachRope()
    {
        DetachRope(true, true);
    }

    public void NotifyTargetBecameUnavailable(RopePullTarget target)
    {
        if (attachedTarget == target)
        {
            DetachRope(false, false);
        }
    }

    private void DetachRope(bool refreshPrompt, bool playDetachSound)
    {
        RopePullTarget previousTarget = attachedTarget;

        if (playDetachSound && previousTarget != null)
        {
            PlayOneShotAtTarget(detachSound, detachSoundVolume, previousTarget);
        }

        StopDraggingSoundImmediately();

        if (ropeJoint != null)
        {
            ropeJoint.enabled = false;
            Destroy(ropeJoint);
        }

        physicalRope?.DestroyRope();
        ropeJoint = null;
        attachedTarget = null;
        currentRopeLength = 0f;

        previousTarget?.Release(this);
        previousTarget?.HidePrompt();

        if (ropeLineRenderer != null)
        {
            ropeLineRenderer.enabled = false;
        }

        displayedRopePoints = null;

        if (refreshPrompt && isActiveAndEnabled)
        {
            RefreshPromptTarget();
        }

        UpdateStoneThrowLock();

        if (previousTarget != null)
        {
            Log($"ロープ切り離し: {previousTarget.name}");
        }
    }

    private void HandleModeInput()
    {
        if (Input.GetKeyDown(weaponModeKey))
        {
            SetWeaponMode();
        }

        if (Input.GetKeyDown(ropeModeKey))
        {
            SetRopeMode();
        }
    }

    private void HandleInteractionInput()
    {
        if (!Input.GetKeyDown(interactKey) || promptTarget == null)
        {
            return;
        }

        if (attachedTarget == promptTarget)
        {
            DetachRope(true, true);
            return;
        }

        if (!IsRopeAttached)
        {
            AttachRope(promptTarget);
        }
    }

    private void HandleRopeLengthInput()
    {
        if (!isRopeMode || !IsRopeAttached)
        {
            return;
        }

        bool shortening = Input.GetKey(shortenKey);
        bool lengthening = Input.GetKey(lengthenKey);

        if (shortening == lengthening)
        {
            return;
        }

        float direction = shortening ? -1f : 1f;
        currentRopeLength = Mathf.Clamp(
            currentRopeLength +
            direction * ropeLengthChangeSpeed * Time.deltaTime,
            minimumRopeLength,
            maximumRopeLength
        );

        if (physicalRope != null &&
            physicalRope.IsActive)
        {
            physicalRope.SetLength(currentRopeLength);
        }
        else if (ropeJoint != null)
        {
            ropeJoint.distance = currentRopeLength;
        }
    }

    private void SetRopeMode(bool ropeMode, bool force)
    {
        if (ropeMode && IsRopeControlLocked)
        {
            return;
        }

        if (!force && isRopeMode == ropeMode)
        {
            return;
        }

        isRopeMode = ropeMode;

        if (equipmentVisualController != null)
        {
            equipmentVisualController.SetWeaponControlLock(this, ropeMode);
            equipmentVisualController.SetWeaponVisibilityLock(this, ropeMode);
        }

        UpdateStoneThrowLock();
        RefreshModeText();

        Log(ropeMode ? "ロープモードへ切り替えました。" : "武器モードへ切り替えました。");
    }

    private void RefreshPromptTarget()
    {
        RopePullTarget nextTarget = null;

        if (attachedTarget != null)
        {
            float distance = Vector2.Distance(
                transform.position,
                attachedTarget.InteractionWorldPosition
            );

            if (distance <= interactionRadius)
            {
                nextTarget = attachedTarget;
            }
        }
        else
        {
            int layerMask = pullableObjectLayers.value == 0
                ? Physics2D.AllLayers
                : pullableObjectLayers.value;

            Collider2D[] colliders = Physics2D.OverlapCircleAll(
                transform.position,
                interactionRadius,
                layerMask
            );

            float nearestDistance = float.PositiveInfinity;

            foreach (Collider2D collider in colliders)
            {
                if (collider == null)
                {
                    continue;
                }

                RopePullTarget candidate =
                    collider.GetComponentInParent<RopePullTarget>();

                if (candidate == null ||
                    !candidate.isActiveAndEnabled ||
                    (candidate.IsReserved && candidate.CurrentController != this))
                {
                    continue;
                }

                float distance = Vector2.Distance(
                    transform.position,
                    candidate.InteractionWorldPosition
                );

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nextTarget = candidate;
                }
            }
        }

        SetPromptTarget(nextTarget);
    }

    private void SetPromptTarget(RopePullTarget nextTarget)
    {
        if (promptTarget != nextTarget)
        {
            promptTarget?.HidePrompt();
            promptTarget = nextTarget;
        }

        if (promptTarget == null)
        {
            return;
        }

        CarryableObject2D carryable =
            promptTarget.GetComponent<CarryableObject2D>();

        bool canCarry = carryable != null && carryable.CanBePickedUp;
        bool isAttachedPrompt = attachedTarget == promptTarget;

        promptTarget.ShowPrompt(
            canCarry
                ? (isAttachedPrompt
                    ? carryableDetachPrompt
                    : carryableAttachPrompt)
                : (isAttachedPrompt
                    ? detachPrompt
                    : attachPrompt)
        );
    }

    private void ClearPromptTarget()
    {
        promptTarget?.HidePrompt();
        promptTarget = null;
    }

    private bool HandleForcedStopConditions()
    {
        bool isDead = playerHealth != null && playerHealth.IsDead;
        bool isRopeClimbing =
            ropeClimbController != null && ropeClimbController.IsClimbing;
        bool isWallClimbing =
            wallClimbController != null && wallClimbController.IsWallClimbing;

        if (isDead && detachWhenPlayerDies)
        {
            if (IsRopeAttached)
            {
                DetachRope(false, false);
            }

            if (isRopeMode)
            {
                SetRopeMode(false, false);
            }

            return true;
        }

        // 設置したロープを登る機能とは同時操作させないため、
        // 必要なら従来どおり引っ張り用ロープを切り離します。
        if (isRopeClimbing && detachWhenClimbing)
        {
            if (IsRopeAttached)
            {
                DetachRope(false, false);
            }

            if (isRopeMode)
            {
                SetRopeMode(false, false);
            }

            return true;
        }

        // 壁登り中は入力だけ一時停止し、引っ張り用ロープは維持します。
        // InspectorでDetach When Wall Climbingをオンにした場合だけ切り離します。
        if (isWallClimbing && detachWhenWallClimbing)
        {
            if (IsRopeAttached)
            {
                DetachRope(false, false);
            }

            if (isRopeMode)
            {
                SetRopeMode(false, false);
            }

            return true;
        }

        return isDead || isRopeClimbing || isWallClimbing;
    }

    private bool IsInputBlocked()
    {
        if (panelsThatBlockInput == null)
        {
            return false;
        }

        foreach (GameObject panel in panelsThatBlockInput)
        {
            if (panel != null && panel.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private void UpdateStoneThrowLock()
    {
        if (stoneThrower == null)
        {
            return;
        }

        bool shouldBlock = isRopeMode || promptTarget != null;
        stoneThrower.SetThrowControlLock(this, shouldBlock);
    }

    private Vector2 GetPlayerHoldPosition()
    {
        if (playerRopeHoldPoint != null)
        {
            return playerRopeHoldPoint.position;
        }

        return (Vector2)transform.position + fallbackHoldOffset;
    }

    private void CreatePlayerAnchorIfNeeded()
    {
        if (allowObjectToPullPlayer || anchorRigidbody != null)
        {
            return;
        }

        anchorObject = new GameObject("RopePull_PlayerAnchor");
        anchorObject.hideFlags = HideFlags.HideInHierarchy;
        anchorObject.transform.position = GetPlayerHoldPosition();

        anchorRigidbody = anchorObject.AddComponent<Rigidbody2D>();
        anchorRigidbody.bodyType = RigidbodyType2D.Kinematic;
        anchorRigidbody.gravityScale = 0f;
        anchorRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;
        anchorRigidbody.simulated = true;
    }

    private void SetupLineRenderer()
    {
        if (ropeLineRenderer == null)
        {
            GameObject visualObject = new GameObject("RopePullVisual");
            visualObject.transform.SetParent(transform, false);
            ropeLineRenderer = visualObject.AddComponent<LineRenderer>();
        }

        ropeLineRenderer.useWorldSpace = true;
        ropeLineRenderer.positionCount = ropeVisualPointCount;
        ropeLineRenderer.widthMultiplier = ropeWidth;
        ropeLineRenderer.startColor = ropeColor;
        ropeLineRenderer.endColor = ropeColor;
        ropeLineRenderer.numCapVertices = 4;
        ropeLineRenderer.numCornerVertices = 3;
        ropeLineRenderer.textureMode = LineTextureMode.Stretch;
        ropeLineRenderer.alignment = LineAlignment.View;
        ropeLineRenderer.sortingLayerName = ropeSortingLayerName;
        ropeLineRenderer.sortingOrder = ropeSortingOrder;

        if (ropeLineRenderer.sharedMaterial == null)
        {
            Shader shader = Shader.Find(
                "Universal Render Pipeline/2D/Sprite-Unlit-Default"
            );

            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }

            if (shader != null)
            {
                runtimeLineMaterial = new Material(shader);
                ropeLineRenderer.sharedMaterial = runtimeLineMaterial;
            }
        }

        ropeLineRenderer.enabled = false;
    }

    private void InitializeRopeVisualPoints()
    {
        if (!IsRopeAttached || ropeLineRenderer == null)
        {
            return;
        }

        if (physicalRope != null &&
            physicalRope.IsValid)
        {
            int physicalPointCount =
                Mathf.Max(2, physicalRope.VisualPointCount);

            displayedRopePoints =
                new Vector3[physicalPointCount];

            int copied = physicalRope.CopyVisualPoints(
                displayedRopePoints
            );

            ropeLineRenderer.positionCount = copied;

            if (copied > 0)
            {
                ropeLineRenderer.SetPositions(
                    displayedRopePoints
                );
            }

            return;
        }

        int count = Mathf.Max(4, ropeVisualPointCount);
        displayedRopePoints = new Vector3[count];

        Vector3 start = GetPlayerHoldPosition();
        Vector3 end = attachedTarget.RopeAttachmentWorldPosition;

        for (int i = 0; i < count; i++)
        {
            displayedRopePoints[i] = Vector3.Lerp(
                start,
                end,
                i / (float)(count - 1)
            );
        }

        ropeLineRenderer.positionCount = count;
        ropeLineRenderer.SetPositions(displayedRopePoints);
    }

    private void UpdateRopeVisual()
    {
        if (!IsRopeAttached || ropeLineRenderer == null)
        {
            if (ropeLineRenderer != null)
            {
                ropeLineRenderer.enabled = false;
            }

            return;
        }

        if (physicalRope != null &&
            physicalRope.IsValid)
        {
            int count = physicalRope.VisualPointCount;

            if (displayedRopePoints == null ||
                displayedRopePoints.Length != count)
            {
                displayedRopePoints = new Vector3[count];
            }

            int copied = physicalRope.CopyVisualPoints(
                displayedRopePoints
            );

            ropeLineRenderer.positionCount = copied;

            if (copied > 0)
            {
                ropeLineRenderer.SetPositions(
                    displayedRopePoints
                );
                ropeLineRenderer.enabled = true;
            }
            else
            {
                ropeLineRenderer.enabled = false;
            }

            return;
        }

        int visualCount = Mathf.Max(4, ropeVisualPointCount);

        if (displayedRopePoints == null ||
            displayedRopePoints.Length != visualCount)
        {
            InitializeRopeVisualPoints();
        }

        Vector3 start = GetPlayerHoldPosition();
        Vector3 end = attachedTarget.RopeAttachmentWorldPosition;
        float straightDistance = Vector2.Distance(start, end);
        float slack = Mathf.Max(0f, currentRopeLength - straightDistance);
        float sag = Mathf.Min(maximumVisualSag, slack * sagMultiplier);

        float blend = 1f - Mathf.Exp(
            -visualFollowSpeed * Mathf.Max(0f, Time.deltaTime)
        );

        for (int i = 0; i < visualCount; i++)
        {
            float t = i / (float)(visualCount - 1);
            Vector3 targetPoint = Vector3.Lerp(start, end, t);
            targetPoint += Vector3.down *
                (Mathf.Sin(Mathf.PI * t) * sag);

            if (i == 0 || i == visualCount - 1)
            {
                displayedRopePoints[i] = targetPoint;
            }
            else
            {
                displayedRopePoints[i] = Vector3.Lerp(
                    displayedRopePoints[i],
                    targetPoint,
                    blend
                );
            }
        }

        ropeLineRenderer.positionCount = visualCount;
        ropeLineRenderer.SetPositions(displayedRopePoints);
        ropeLineRenderer.enabled = true;
    }

    private void SetupAudioSources()
    {
        if (oneShotAudioSource == null)
        {
            runtimeOneShotAudioObject = new GameObject(
                "RopePull_OneShotAudio"
            );

            runtimeOneShotAudioObject.hideFlags =
                HideFlags.HideInHierarchy;

            oneShotAudioSource =
                runtimeOneShotAudioObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(oneShotAudioSource, false);

        if (draggingAudioSource == null)
        {
            runtimeDraggingAudioObject = new GameObject(
                "RopePull_DraggingAudio"
            );

            runtimeDraggingAudioObject.hideFlags =
                HideFlags.HideInHierarchy;

            draggingAudioSource =
                runtimeDraggingAudioObject.AddComponent<AudioSource>();
        }

        ConfigureAudioSource(draggingAudioSource, true);
    }

    private void ConfigureAudioSource(
        AudioSource source,
        bool loop)
    {
        if (source == null)
        {
            return;
        }

        source.playOnAwake = false;
        source.loop = loop;
        source.spatialBlend = soundSpatialBlend;
    }

    private void PlayOneShotAtTarget(
        AudioClip clip,
        float volume,
        RopePullTarget target)
    {
        if (clip == null)
        {
            return;
        }

        SetupAudioSources();

        if (oneShotAudioSource == null)
        {
            return;
        }

        oneShotAudioSource.transform.position = target != null
            ? (Vector3)target.RopeAttachmentWorldPosition
            : transform.position;

        oneShotAudioSource.PlayOneShot(
            clip,
            Mathf.Clamp01(volume)
        );
    }

    private void UpdateDraggingSound()
    {
        SetupAudioSources();

        if (draggingAudioSource == null)
        {
            return;
        }

        float targetVolume = 0f;
        float targetPitch = minimumDraggingPitch;

        if (IsRopeAttached &&
            attachedTarget != null &&
            draggingLoopSound != null)
        {
            draggingAudioSource.transform.position =
                attachedTarget.RopeAttachmentWorldPosition;

            Rigidbody2D targetBody = attachedTarget.TargetRigidbody;
            float speed = targetBody != null
                ? targetBody.linearVelocity.magnitude
                : 0f;

            float ropePathLength =
                physicalRope != null &&
                physicalRope.IsValid
                    ? physicalRope.GetApproximatePathLength()
                    : Vector2.Distance(
                        GetPlayerHoldPosition(),
                        attachedTarget.RopeAttachmentWorldPosition
                    );

            bool ropeIsTaut = !requireTautRopeForDraggingSound ||
                ropePathLength >=
                currentRopeLength - ropeTensionTolerance;

            bool touchesSurface =
                !requireSurfaceContactForDraggingSound ||
                IsAttachedTargetNearDragSurface();

            if (speed >= minimumDraggingSpeed &&
                ropeIsTaut &&
                touchesSurface)
            {
                float speedRate = Mathf.InverseLerp(
                    minimumDraggingSpeed,
                    Mathf.Max(
                        minimumDraggingSpeed + 0.01f,
                        maximumDraggingSpeed
                    ),
                    speed
                );

                targetVolume =
                    draggingSoundVolume * speedRate;

                targetPitch = Mathf.Lerp(
                    minimumDraggingPitch,
                    maximumDraggingPitch,
                    speedRate
                );
            }
        }

        if (draggingAudioSource.clip != draggingLoopSound)
        {
            draggingAudioSource.Stop();
            draggingAudioSource.clip = draggingLoopSound;
        }

        draggingAudioSource.pitch = targetPitch;
        draggingAudioSource.volume = Mathf.MoveTowards(
            draggingAudioSource.volume,
            targetVolume,
            draggingSoundFadeSpeed * Time.unscaledDeltaTime
        );

        if (targetVolume > 0.001f)
        {
            if (!draggingAudioSource.isPlaying)
            {
                draggingAudioSource.Play();
            }
        }
        else if (draggingAudioSource.isPlaying &&
                 draggingAudioSource.volume <= 0.001f)
        {
            draggingAudioSource.Stop();
        }
    }

    private bool IsAttachedTargetNearDragSurface()
    {
        if (attachedTarget == null)
        {
            return false;
        }

        int layerMask = dragSurfaceLayers.value;

        if (layerMask == 0 && playerMove != null)
        {
            layerMask = playerMove.groundLayer.value;
        }

        if (layerMask == 0)
        {
            return true;
        }

        Collider2D[] targetColliders =
            attachedTarget.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D targetCollider in targetColliders)
        {
            if (targetCollider == null || targetCollider.isTrigger)
            {
                continue;
            }

            Bounds bounds = targetCollider.bounds;
            Vector2 castOrigin = new Vector2(
                bounds.center.x,
                bounds.min.y + 0.02f
            );

            Vector2 castSize = new Vector2(
                Mathf.Max(0.05f, bounds.size.x * 0.8f),
                0.04f
            );

            RaycastHit2D[] hits = Physics2D.BoxCastAll(
                castOrigin,
                castSize,
                0f,
                Vector2.down,
                dragSurfaceCheckDistance,
                layerMask
            );

            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider == null ||
                    hit.collider.transform.IsChildOf(
                        attachedTarget.transform
                    ))
                {
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    private void StopDraggingSoundImmediately()
    {
        if (draggingAudioSource == null)
        {
            return;
        }

        draggingAudioSource.Stop();
        draggingAudioSource.volume = 0f;
    }

    private void RefreshModeText()
    {
        if (modeText == null)
        {
            return;
        }

        modeText.text = isRopeMode
            ? ropeModeLabel
            : weaponModeLabel;
    }

    private void FindReferences()
    {
        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody2D>();
        }

        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<CharacterHealth>();
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

        if (proneController == null)
        {
            proneController = GetComponent<PlayerProneController>();
        }

        if (ropeClimbController == null)
        {
            ropeClimbController = GetComponent<RopeClimbController>();
        }

        if (wallClimbController == null)
        {
            wallClimbController = GetComponent<WallClimbController>();
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerRopePullController] {message}", this);
        }
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.1f, interactionRadius);
        minimumRopeLength = Mathf.Max(0.1f, minimumRopeLength);
        maximumRopeLength = Mathf.Max(minimumRopeLength, maximumRopeLength);
        initialSlack = Mathf.Max(0f, initialSlack);
        ropeLengthChangeSpeed = Mathf.Max(0.01f, ropeLengthChangeSpeed);
        ropeBreakForce = Mathf.Max(0f, ropeBreakForce);

        preferredPhysicalSegmentLength = Mathf.Max(
            0.08f,
            preferredPhysicalSegmentLength
        );
        minimumPhysicalSegmentCount = Mathf.Clamp(
            minimumPhysicalSegmentCount,
            2,
            64
        );
        maximumPhysicalSegmentCount = Mathf.Clamp(
            maximumPhysicalSegmentCount,
            minimumPhysicalSegmentCount,
            64
        );
        physicalRopeThickness = Mathf.Max(
            0.01f,
            physicalRopeThickness
        );
        physicalSegmentMass = Mathf.Max(
            0.001f,
            physicalSegmentMass
        );
        physicalSegmentGravityScale = Mathf.Max(
            0f,
            physicalSegmentGravityScale
        );
        physicalSegmentLinearDamping = Mathf.Max(
            0f,
            physicalSegmentLinearDamping
        );
        physicalSegmentAngularDamping = Mathf.Max(
            0f,
            physicalSegmentAngularDamping
        );
        ropeSegmentLayerName =
            ropeSegmentLayerName?.Trim() ?? string.Empty;

        if (ropeGroundLayers.value == 0 &&
            playerMove != null)
        {
            ropeGroundLayers = playerMove.groundLayer;
        }

        ropeVisualPointCount = Mathf.Clamp(ropeVisualPointCount, 4, 40);
        ropeWidth = Mathf.Max(0.001f, ropeWidth);
        sagMultiplier = Mathf.Max(0f, sagMultiplier);
        maximumVisualSag = Mathf.Max(0f, maximumVisualSag);
        visualFollowSpeed = Mathf.Max(0.01f, visualFollowSpeed);

        attachSoundVolume = Mathf.Clamp01(attachSoundVolume);
        detachSoundVolume = Mathf.Clamp01(detachSoundVolume);
        draggingSoundVolume = Mathf.Clamp01(draggingSoundVolume);
        soundSpatialBlend = Mathf.Clamp01(soundSpatialBlend);
        minimumDraggingSpeed = Mathf.Max(0.01f, minimumDraggingSpeed);
        maximumDraggingSpeed = Mathf.Max(
            minimumDraggingSpeed + 0.01f,
            maximumDraggingSpeed
        );
        ropeTensionTolerance = Mathf.Max(0f, ropeTensionTolerance);
        dragSurfaceCheckDistance = Mathf.Max(
            0.01f,
            dragSurfaceCheckDistance
        );
        draggingSoundFadeSpeed = Mathf.Max(
            0.01f,
            draggingSoundFadeSpeed
        );
        minimumDraggingPitch = Mathf.Clamp(
            minimumDraggingPitch,
            0.1f,
            3f
        );
        maximumDraggingPitch = Mathf.Clamp(
            maximumDraggingPitch,
            minimumDraggingPitch,
            3f
        );
    }

    private void OnDrawGizmosSelected()
    {
        if (!showInteractionGizmo)
        {
            return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);

        if (attachedTarget != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(
                GetPlayerHoldPosition(),
                attachedTarget.RopeAttachmentWorldPosition
            );
        }
    }
}
