using System;
using System.Collections;
using TMPro;
using UnityEngine;

public enum PlayerCarryState2D
{
    None,
    Front,
    Backpack
}

/// <summary>
/// CarryableObject2Dを手前に持つ／背負う／落とす操作を管理します。
/// E：拾う、手前持ちと背負いの切り替え
/// F：手前持ちは少し前へ、背負い中は真下へ落とす
/// </summary>
[DefaultExecutionOrder(-130)]
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class PlayerCarryController2D : MonoBehaviour
{
    [Header("Player参照")]
    [SerializeField] private Rigidbody2D playerRigidbody;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private CharacterHealth playerHealth;
    [SerializeField] private PlayerEquipmentVisualController equipmentVisualController;
    [SerializeField] private WallClimbController wallClimbController;
    [SerializeField] private PlayerRopePullController ropePullController;
    [SerializeField] private StoneThrower stoneThrower;
    [SerializeField] private PlayerProneController proneController;
    [SerializeField] private PlayerWeightController playerWeightController;

    [Header("持つ位置")]
    [Tooltip("右向き時に物を手前へ持つ位置です")]
    [SerializeField] private Transform frontCarryPointRight;

    [Tooltip("左向き時に物を手前へ持つ位置です")]
    [SerializeField] private Transform frontCarryPointLeft;

    [Tooltip("右向き時に物を背負う位置です")]
    [SerializeField] private Transform backpackPointRight;

    [Tooltip("左向き時に物を背負う位置です")]
    [SerializeField] private Transform backpackPointLeft;

    [Tooltip("持つ位置が未設定の場合の手前位置。Xは向きに合わせて反転します")]
    [SerializeField] private Vector2 fallbackFrontCarryOffset =
        new Vector2(0.75f, 0.25f);

    [Tooltip("背負う位置が未設定の場合の背中位置。Xは向きに合わせて反転します")]
    [SerializeField] private Vector2 fallbackBackpackOffset =
        new Vector2(-0.38f, 0.5f);

    [SerializeField] private bool keepCarriedObjectUpright = true;
    [SerializeField] private float frontCarryRotation;
    [SerializeField] private float backpackRotation;

    [Header("落とす位置")]
    [Tooltip("右向き時に手前持ちの物を落とす位置です")]
    [SerializeField] private Transform frontDropPointRight;

    [Tooltip("左向き時に手前持ちの物を落とす位置です")]
    [SerializeField] private Transform frontDropPointLeft;

    [Tooltip("背負った物を真下へ落とす位置です")]
    [SerializeField] private Transform backpackDropPoint;

    [Tooltip("Drop Point未設定時の、手前へ落とす位置です")]
    [SerializeField] private Vector2 fallbackFrontDropOffset =
        new Vector2(0.95f, -0.05f);

    [Tooltip("Drop Point未設定時の、背負った物を落とす位置です")]
    [SerializeField] private Vector2 fallbackBackpackDropOffset =
        new Vector2(0f, -0.85f);

    [SerializeField, Min(0f)] private float frontDropHorizontalVelocity = 1.5f;
    [SerializeField, Min(0f)] private float frontDropUpwardVelocity = 0.35f;
    [SerializeField, Min(0f)] private float backpackDropDownwardVelocity = 0.25f;
    [SerializeField, Range(0f, 1f)] private float inheritedPlayerVelocity = 0.35f;
    [SerializeField, Min(0f)] private float playerCollisionIgnoreDuration = 0.25f;

    [Header("入力")]
    [SerializeField] private KeyCode carryKey = KeyCode.E;
    [SerializeField] private KeyCode dropKey = KeyCode.F;

    [Header("持てる物の探索")]
    [Tooltip("空欄なら全LayerからCarryableObject2Dを探します")]
    [SerializeField] private LayerMask carryableObjectLayers;
    [SerializeField, Min(0.1f)] private float interactionRadius = 1.8f;

    [Header("Text表示")]
    [SerializeField] private TMP_Text carryModeText;
    [SerializeField] private string pickupPrompt = "E：持つ";
    [SerializeField] private string frontCarryPrompt = "E：背負う　F：前に置く";
    [SerializeField] private string backpackPrompt = "E：手前に持つ　F：真下に置く";
    [SerializeField] private string frontCarryLabel = "手前に持っています";
    [SerializeField] private string backpackLabel = "背負っています";

    [Header("入力を止めるPanel（任意）")]
    [SerializeField] private GameObject[] panelsThatBlockInput;

    [Header("安全設定")]
    [SerializeField] private bool dropWhenPlayerDies = true;
    [SerializeField] private bool dropWhenControllerDisabled = true;
    [SerializeField] private bool blockPickupWhileProne = true;
    [SerializeField] private bool blockPickupWhileWallClimbing = true;
    [SerializeField] private bool blockPickupWhileRopeMode = true;

    [Header("Animator（任意）")]
    [SerializeField] private Animator playerAnimator;
    [SerializeField] private string frontCarryBoolName = "";
    [SerializeField] private string backpackBoolName = "";

    [Tooltip("手前持ちから背負う時に再生するAnimator Trigger名です。例：CarryToBackpack")]
    [SerializeField] private string backpackTransitionTriggerName = "CarryToBackpack";

    [Tooltip("背負う動作にかける秒数です。AnimatorのClip長に合わせてください")]
    [SerializeField, Min(0f)] private float backpackTransitionDuration = 0.45f;

    [Tooltip("背負うアニメーション中、物体も手前から背中へ滑らかに移動させます")]
    [SerializeField] private bool animateObjectToBackpack = true;

    [SerializeField]
    private AnimationCurve backpackTransitionCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("運搬重量による速度低下")]
    [SerializeField] private bool enableCarryWeightSlowdown = true;

    [Tooltip("横軸=運搬物のkg、縦軸=移動速度倍率。例：0kg=1、20kg=0.85、40kg=0.65、60kg=0.45")]
    [SerializeField]
    private AnimationCurve carryWeightSpeedCurve = new AnimationCurve(
        new Keyframe(0f, 1f),
        new Keyframe(10f, 1f),
        new Keyframe(20f, 0.85f),
        new Keyframe(40f, 0.65f),
        new Keyframe(60f, 0.45f)
    );

    [Tooltip("どれだけ重くても、この倍率より遅くしません")]
    [SerializeField, Range(0.05f, 1f)] private float minimumCarrySpeedMultiplier = 0.25f;

    [Header("デバッグ診断")]
    [Tooltip("通常の状態変更ログを表示します")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("参照不足、入力停止理由、対象探索結果などの詳しい診断を表示します")]
    [SerializeField] private bool showDetailedDiagnostics = true;

    [Tooltip("E／Fが押された瞬間の状態を表示します")]
    [SerializeField] private bool logInputEvents = true;

    [Tooltip("周囲で検出したColliderと、対象から除外した理由をすべて表示します。ログが多いため原因調査時だけON推奨です")]
    [SerializeField] private bool logTargetScanDetails;

    [Tooltip("同じ警告を繰り返す最短間隔です")]
    [SerializeField, Min(0.1f)] private float repeatedDiagnosticInterval = 1.5f;

    [SerializeField] private bool showInteractionGizmo = true;

    public PlayerCarryState2D CurrentState => currentState;
    public bool IsCarrying => carriedTarget != null && currentState != PlayerCarryState2D.None;
    public bool IsFrontCarrying => IsCarrying && currentState == PlayerCarryState2D.Front;
    public bool IsBackpackCarrying => IsCarrying && currentState == PlayerCarryState2D.Backpack;
    public bool IsCarryTransitioning => isCarryTransitioning;
    public CarryableObject2D CarriedTarget => carriedTarget;
    public float InteractionRadius => interactionRadius;
    public LayerMask CarryableObjectLayers => carryableObjectLayers;

    /// <summary>
    /// CarryableObject2Dを下ろした直後に通知します。
    /// 第3引数はFキーによる手動Dropならtrueです。
    /// </summary>
    public event Action<CarryableObject2D, PlayerCarryState2D, bool> CarryableDropped;

    /// <summary>
    /// Scene内の受取システム（エレベーター等）が購読する共通Dropイベントです。
    /// </summary>
    public static event Action<PlayerCarryController2D, CarryableObject2D, PlayerCarryState2D, bool> AnyCarryableDropped;

    public bool IsLayerAllowedForCarry(int layer)
    {
        return carryableObjectLayers.value == 0 ||
               (carryableObjectLayers.value & (1 << layer)) != 0;
    }

    /// <summary>
    /// Eキーを運搬操作へ優先させる必要がある時に、WorldItemPickupが参照します。
    /// </summary>
    public bool BlocksWorldItemPickup =>
        IsCarrying || promptTarget != null || consumedCarryInputThisFrame;

    private CarryableObject2D carriedTarget;
    private CarryableObject2D promptTarget;
    private PlayerCarryState2D currentState;
    private bool consumedCarryInputThisFrame;
    private bool isCarryTransitioning;
    private Coroutine backpackTransitionCoroutine;

    private string lastDiagnosticState = string.Empty;
    private float nextRepeatedDiagnosticTime;

    private void Awake()
    {
        FindReferences();
        ApplyActionLocks();
        RefreshCarryText();
        LogCarryDiagnostics("Awake");
    }

    private void OnEnable()
    {
        FindReferences();
        ApplyActionLocks();
        RefreshCarryText();
        LogCarryDiagnostics("OnEnable");
    }

    private void Update()
    {
        consumedCarryInputThisFrame = false;
        FindReferences();

        if (logInputEvents && (Input.GetKeyDown(carryKey) || Input.GetKeyDown(dropKey)))
        {
            Log(
                $"[Carry診断][入力] " +
                $"Key={(Input.GetKeyDown(carryKey) ? carryKey.ToString() : dropKey.ToString())} / " +
                $"State={currentState} / IsCarrying={IsCarrying} / " +
                $"PromptTarget={(promptTarget != null ? promptTarget.name : "なし")} / " +
                $"InputBlocked={IsInputBlocked()} / PickupBlock={GetNewPickupBlockReason()}"
            );
        }

        if (dropWhenPlayerDies &&
            playerHealth != null &&
            playerHealth.IsDead)
        {
            if (IsCarrying)
            {
                DropCarriedObject(false);
            }

            ClearPromptTarget();
            return;
        }

        if (IsInputBlocked())
        {
            LogDiagnosticState(
                "InputBlocked:" + GetBlockingPanelName(),
                $"[Carry診断] 入力を停止しています。Active Panel={GetBlockingPanelName()}"
            );
            ClearPromptTarget();
            RefreshCarryPrompt();
            return;
        }

        if (IsCarrying)
        {
            ClearPromptTarget();
            HandleCarryingInput();
            RefreshCarryPrompt();
            return;
        }

        if (ShouldBlockNewPickup())
        {
            string reason = GetNewPickupBlockReason();
            LogDiagnosticState(
                "PickupBlocked:" + reason,
                $"[Carry診断] 新しく物を持てません。理由={reason}"
            );
            ClearPromptTarget();
            RefreshCarryText();
            return;
        }

        RefreshPromptTarget();

        if (Input.GetKeyDown(carryKey))
        {
            if (promptTarget == null)
            {
                LogWarning(
                    $"[Carry診断] {carryKey}を押しましたが、持てる対象が選択されていません。" +
                    $"Radius={interactionRadius:0.00} / LayerMask={FormatLayerMask(carryableObjectLayers)}"
                );
            }
            else
            {
                consumedCarryInputThisFrame = TryPickUp(promptTarget);
            }
        }

        RefreshCarryText();
    }

    private void FixedUpdate()
    {
        if (!IsCarrying || isCarryTransitioning)
        {
            return;
        }

        GetCurrentCarryPose(out Vector2 position, out float rotation);
        carriedTarget.SetCarryPose(position, rotation);
    }

    private void LateUpdate()
    {
        if (!IsCarrying || isCarryTransitioning)
        {
            return;
        }

        GetCurrentCarryPose(out Vector2 position, out float rotation);
        carriedTarget.SnapCarryPose(position, rotation);
    }

    private void OnDisable()
    {
        ClearPromptTarget();
        StopBackpackTransition();

        if (dropWhenControllerDisabled && IsCarrying)
        {
            DropCarriedObject(false);
        }
        else
        {
            ReleaseAllActionLocks();
        }
    }

    private void OnDestroy()
    {
        ReleaseAllActionLocks();
    }

    public bool TryPickUp(CarryableObject2D target)
    {
        Log(
            $"[Carry診断][TryPickUp開始] Target={(target != null ? target.name : "null")} / " +
            $"CurrentState={currentState} / IsCarrying={IsCarrying}"
        );

        if (target == null)
        {
            LogWarning("[Carry診断][TryPickUp失敗] Targetがnullです。");
            return false;
        }

        if (IsCarrying)
        {
            LogWarning(
                $"[Carry診断][TryPickUp失敗] すでに{carriedTarget.name}を運搬中です。"
            );
            return false;
        }

        if (!target.isActiveAndEnabled)
        {
            LogWarning(
                $"[Carry診断][TryPickUp失敗] {target.name} のCarryableObject2Dが無効です。"
            );
            return false;
        }

        if (!target.CanBePickedUp)
        {
            LogWarning(
                $"[Carry診断][TryPickUp失敗] {target.name} は現在持てません。" +
                $"理由={target.GetPickupBlockReason()}"
            );
            return false;
        }

        if (ShouldBlockNewPickup())
        {
            LogWarning(
                $"[Carry診断][TryPickUp失敗] Player側で持つ操作が禁止されています。" +
                $"理由={GetNewPickupBlockReason()}"
            );
            return false;
        }

        if (ropePullController != null && ropePullController.IsRopeAttached)
        {
            Log("[Carry診断] 引っ張りロープ接続中のため、持ち上げる前に切り離します。");
            ropePullController.DetachRope();
        }

        if (!target.TryBeginCarry(this))
        {
            LogWarning(
                $"[Carry診断][TryPickUp失敗] {target.name}.TryBeginCarryがfalseを返しました。" +
                "対象Object側の直前ログを確認してください。"
            );
            return false;
        }

        ClearPromptTarget();
        carriedTarget = target;
        currentState = PlayerCarryState2D.Front;

        ApplyActionLocks();
        ApplyCarryWeightSlowdown();
        SnapObjectToCurrentCarryPoint();
        RefreshCarryPrompt();
        RefreshCarryText();

        Log($"{target.name} を手前に持ちました。");
        return true;
    }

    public void SwitchToBackpack()
    {
        if (!IsCarrying || currentState == PlayerCarryState2D.Backpack || isCarryTransitioning)
        {
            return;
        }

        if (backpackTransitionCoroutine != null)
        {
            StopCoroutine(backpackTransitionCoroutine);
        }

        backpackTransitionCoroutine = StartCoroutine(
            BackpackTransitionRoutine()
        );
    }

    private IEnumerator BackpackTransitionRoutine()
    {
        if (!IsCarrying || carriedTarget == null)
        {
            yield break;
        }

        isCarryTransitioning = true;

        // アニメーション完了まではFront扱いのままにして、
        // 銃と壁登りが途中で有効にならないようにします。
        currentState = PlayerCarryState2D.Front;
        ApplyActionLocks();
        ApplyCarryWeightSlowdown();

        SetAnimatorTrigger(backpackTransitionTriggerName);

        float duration = Mathf.Max(0f, backpackTransitionDuration);

        if (duration <= 0f)
        {
            FinishBackpackTransition();
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            if (!IsCarrying || carriedTarget == null)
            {
                StopBackpackTransition();
                yield break;
            }

            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / duration);
            float eased = backpackTransitionCurve != null
                ? Mathf.Clamp01(backpackTransitionCurve.Evaluate(progress))
                : progress;

            if (animateObjectToBackpack)
            {
                GetCarryPoseForState(
                    PlayerCarryState2D.Front,
                    out Vector2 frontPosition,
                    out float frontRotation
                );

                GetCarryPoseForState(
                    PlayerCarryState2D.Backpack,
                    out Vector2 backpackPosition,
                    out float backpackRotationValue
                );

                Vector2 position = Vector2.Lerp(
                    frontPosition,
                    backpackPosition,
                    eased
                );

                float rotation = Mathf.LerpAngle(
                    frontRotation,
                    backpackRotationValue,
                    eased
                );

                carriedTarget.SnapCarryPose(position, rotation);
            }

            yield return null;
        }

        FinishBackpackTransition();
    }

    private void FinishBackpackTransition()
    {
        if (!IsCarrying || carriedTarget == null)
        {
            StopBackpackTransition();
            return;
        }

        currentState = PlayerCarryState2D.Backpack;
        isCarryTransitioning = false;
        backpackTransitionCoroutine = null;

        ApplyActionLocks();
        ApplyCarryWeightSlowdown();
        SnapObjectToCurrentCarryPoint();
        RefreshCarryPrompt();
        RefreshCarryText();

        Log($"{carriedTarget.name} を背負いました。Weight={carriedTarget.CarryWeightKg:0.0}kg / Speed×{GetCurrentCarrySpeedMultiplier():0.00}");
    }

    private void StopBackpackTransition()
    {
        if (backpackTransitionCoroutine != null)
        {
            StopCoroutine(backpackTransitionCoroutine);
            backpackTransitionCoroutine = null;
        }

        isCarryTransitioning = false;
    }

    public void SwitchToFrontCarry()
    {
        if (!IsCarrying || currentState == PlayerCarryState2D.Front || isCarryTransitioning)
        {
            return;
        }

        if (blockPickupWhileProne && proneController != null && proneController.IsProne)
        {
            Log("ほふく中は物を手前に持ち直せません。");
            return;
        }

        currentState = PlayerCarryState2D.Front;
        ApplyActionLocks();
        ApplyCarryWeightSlowdown();
        SnapObjectToCurrentCarryPoint();
        RefreshCarryPrompt();
        RefreshCarryText();

        Log($"{carriedTarget.name} を手前に持ち直しました。");
    }

    public void DropCarriedObject()
    {
        DropCarriedObject(false);
    }

    public void DropCarriedObject(bool manualDrop)
    {
        if (!IsCarrying)
        {
            return;
        }

        CarryableObject2D target = carriedTarget;
        PlayerCarryState2D droppedFromState = currentState;

        StopBackpackTransition();

        GetDropPose(
            droppedFromState,
            out Vector2 dropPosition,
            out float dropRotation,
            out Vector2 releaseVelocity
        );

        carriedTarget = null;
        currentState = PlayerCarryState2D.None;
        ClearCarryWeightSlowdown();

        target.HidePrompt();
        target.ReleaseFromCarry(
            this,
            dropPosition,
            dropRotation,
            releaseVelocity,
            GetPlayerColliders(),
            playerCollisionIgnoreDuration
        );

        ReleaseAllActionLocks();
        RefreshCarryText();

        CarryableDropped?.Invoke(target, droppedFromState, manualDrop);
        AnyCarryableDropped?.Invoke(this, target, droppedFromState, manualDrop);

        Log(droppedFromState == PlayerCarryState2D.Backpack
            ? $"{target.name} を真下へ落としました。"
            : $"{target.name} を少し前へ置きました。"
        );
    }

    public void NotifyCarryTargetUnavailable(CarryableObject2D target)
    {
        if (target == null || target != carriedTarget)
        {
            return;
        }

        carriedTarget = null;
        currentState = PlayerCarryState2D.None;
        StopBackpackTransition();
        ClearCarryWeightSlowdown();
        ReleaseAllActionLocks();
        RefreshCarryText();
    }

    private void HandleCarryingInput()
    {
        if (isCarryTransitioning)
        {
            // 背負うモーション中はE/Fの連打による状態破綻を防ぎます。
            return;
        }

        if (carriedTarget == null)
        {
            LogWarning("[Carry診断] 運搬StateですがCarried Targetがnullです。状態を解除します。");
            currentState = PlayerCarryState2D.None;
            ReleaseAllActionLocks();
            return;
        }

        if (Input.GetKeyDown(carryKey))
        {
            consumedCarryInputThisFrame = true;

            if (currentState == PlayerCarryState2D.Front)
            {
                SwitchToBackpack();
            }
            else
            {
                SwitchToFrontCarry();
            }
        }

        if (Input.GetKeyDown(dropKey))
        {
            consumedCarryInputThisFrame = true;
            DropCarriedObject(true);
        }
    }

    private bool ShouldBlockNewPickup()
    {
        return !string.IsNullOrEmpty(GetNewPickupBlockReason());
    }

    private string GetNewPickupBlockReason()
    {
        if (blockPickupWhileProne &&
            proneController != null &&
            proneController.IsProne)
        {
            return "ほふく中";
        }

        if (blockPickupWhileWallClimbing &&
            wallClimbController != null &&
            wallClimbController.IsWallClimbing)
        {
            return "壁登り中";
        }

        if (blockPickupWhileRopeMode &&
            ropePullController != null &&
            ropePullController.IsRopeMode)
        {
            return "引っ張りロープモード中";
        }

        return string.Empty;
    }

    private void ApplyActionLocks()
    {
        bool carrying = IsCarrying;
        bool frontCarry = IsFrontCarrying;

        if (equipmentVisualController != null)
        {
            equipmentVisualController.SetWeaponControlLock(this, frontCarry);
            equipmentVisualController.SetWeaponVisibilityLock(this, frontCarry);
        }

        wallClimbController?.SetWallClimbLock(this, frontCarry);
        ropePullController?.SetRopeControlLock(this, carrying);
        stoneThrower?.SetThrowControlLock(this, carrying);

        SetAnimatorBool(frontCarryBoolName, frontCarry);
        SetAnimatorBool(backpackBoolName, IsBackpackCarrying);
    }

    private void ReleaseAllActionLocks()
    {
        equipmentVisualController?.SetWeaponControlLock(this, false);
        equipmentVisualController?.SetWeaponVisibilityLock(this, false);
        wallClimbController?.SetWallClimbLock(this, false);
        ropePullController?.SetRopeControlLock(this, false);
        stoneThrower?.SetThrowControlLock(this, false);

        SetAnimatorBool(frontCarryBoolName, false);
        SetAnimatorBool(backpackBoolName, false);
    }

    private void RefreshPromptTarget()
    {
        CarryableObject2D nextTarget = null;
        int layerMask = carryableObjectLayers.value == 0
            ? Physics2D.AllLayers
            : carryableObjectLayers.value;

        Collider2D[] colliders = Physics2D.OverlapCircleAll(
            transform.position,
            interactionRadius,
            layerMask
        );

        if (logTargetScanDetails)
        {
            Log(
                $"[Carry診断][探索] Center={transform.position} / Radius={interactionRadius:0.00} / " +
                $"LayerMask={FormatLayerMask(carryableObjectLayers)} / ColliderCount={colliders.Length}"
            );
        }

        float nearestDistance = float.PositiveInfinity;
        int selfColliderCount = 0;
        int missingCarryableCount = 0;
        int disabledCarryableCount = 0;
        int unavailableCarryableCount = 0;
        int fartherCandidateCount = 0;
        string firstRejectDetail = string.Empty;

        foreach (Collider2D collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            if (collider.transform.IsChildOf(transform))
            {
                selfColliderCount++;
                RecordFirstReject(
                    ref firstRejectDetail,
                    $"{GetObjectPath(collider.transform)} はPlayer自身のCollider"
                );

                if (logTargetScanDetails)
                {
                    Log($"[Carry診断][探索除外] {GetObjectPath(collider.transform)}: Player自身のColliderです。");
                }
                continue;
            }

            CarryableObject2D candidate = ResolveCarryableObject(collider);

            if (candidate == null)
            {
                missingCarryableCount++;
                RecordFirstReject(
                    ref firstRejectDetail,
                    $"{GetObjectPath(collider.transform)} の親・Rigidbody・子階層にCarryableObject2Dがない"
                );

                if (logTargetScanDetails)
                {
                    Log(
                        $"[Carry診断][探索除外] {GetObjectPath(collider.transform)}: " +
                        "親・Attached Rigidbody・子階層にCarryableObject2Dがありません。"
                    );
                }
                continue;
            }

            if (!candidate.isActiveAndEnabled)
            {
                disabledCarryableCount++;
                RecordFirstReject(
                    ref firstRejectDetail,
                    $"{candidate.name} のCarryableObject2Dが無効"
                );

                if (logTargetScanDetails)
                {
                    Log($"[Carry診断][探索除外] {candidate.name}: CarryableObject2Dが無効です。");
                }
                continue;
            }

            if (!candidate.CanBePickedUp || candidate.IsReserved)
            {
                unavailableCarryableCount++;
                string blockReason = candidate.GetPickupBlockReason();
                RecordFirstReject(
                    ref firstRejectDetail,
                    $"{candidate.name} は取得不可（{blockReason}）"
                );

                if (logTargetScanDetails)
                {
                    Log($"[Carry診断][探索除外] {candidate.name}: {blockReason}");
                }
                continue;
            }

            // Interaction Pointが誤って遠い場所へ置かれていても取得できるよう、
            // 実際にOverlapCircleで検出したCollider表面までの距離を使います。
            Vector2 closestPoint = collider.ClosestPoint(transform.position);
            float distance = Vector2.Distance(
                transform.position,
                closestPoint
            );

            if (distance > interactionRadius || distance >= nearestDistance)
            {
                fartherCandidateCount++;
                RecordFirstReject(
                    ref firstRejectDetail,
                    $"{candidate.name} のCollider距離={distance:0.00}"
                );

                if (logTargetScanDetails)
                {
                    Log(
                        $"[Carry診断][探索除外] {candidate.name}: " +
                        $"ColliderDistance={distance:0.00}, Nearest={nearestDistance:0.00}, " +
                        $"Radius={interactionRadius:0.00}, InteractionPointDistance=" +
                        $"{Vector2.Distance(transform.position, candidate.InteractionWorldPosition):0.00}"
                    );
                }
                continue;
            }

            nearestDistance = distance;
            nextTarget = candidate;
        }

        if (nextTarget == null)
        {
            string rejectSummary =
                $"Self={selfColliderCount}, Carryableなし={missingCarryableCount}, " +
                $"無効={disabledCarryableCount}, 取得不可={unavailableCarryableCount}, " +
                $"距離除外={fartherCandidateCount}";

            string firstReject = string.IsNullOrWhiteSpace(firstRejectDetail)
                ? "なし"
                : firstRejectDetail;

            LogDiagnosticState(
                "NoPromptTarget:" + rejectSummary + ":" + firstRejectDetail,
                $"[Carry診断] 範囲内に持てる対象がありません。" +
                $"検出Collider={colliders.Length} / Radius={interactionRadius:0.00} / " +
                $"LayerMask={FormatLayerMask(carryableObjectLayers)} / " +
                $"除外内訳=[{rejectSummary}] / 最初の除外理由={firstReject}"
            );
        }
        else
        {
            LogDiagnosticState(
                "PromptTarget:" + nextTarget.GetInstanceID(),
                $"[Carry診断] 持てる対象を検出しました：{nextTarget.name} / " +
                $"ColliderDistance={nearestDistance:0.00} / " +
                $"InteractionPointDistance={Vector2.Distance(transform.position, nextTarget.InteractionWorldPosition):0.00}"
            );
        }

        SetPromptTarget(nextTarget);
    }

    private static CarryableObject2D ResolveCarryableObject(
        Collider2D sourceCollider)
    {
        if (sourceCollider == null)
        {
            return null;
        }

        CarryableObject2D candidate =
            sourceCollider.GetComponent<CarryableObject2D>();

        if (candidate != null)
        {
            return candidate;
        }

        candidate = sourceCollider.GetComponentInParent<CarryableObject2D>();

        if (candidate != null)
        {
            return candidate;
        }

        Rigidbody2D attachedBody = sourceCollider.attachedRigidbody;

        if (attachedBody != null)
        {
            candidate = attachedBody.GetComponent<CarryableObject2D>();

            if (candidate != null)
            {
                return candidate;
            }

            candidate = attachedBody.GetComponentInChildren<CarryableObject2D>(true);

            if (candidate != null)
            {
                return candidate;
            }
        }

        Transform root = sourceCollider.transform.root;

        return root != null
            ? root.GetComponentInChildren<CarryableObject2D>(true)
            : null;
    }

    private static void RecordFirstReject(
        ref string firstRejectDetail,
        string detail)
    {
        if (string.IsNullOrWhiteSpace(firstRejectDetail))
        {
            firstRejectDetail = detail;
        }
    }

    private static string GetObjectPath(Transform target)
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

    private void SetPromptTarget(CarryableObject2D nextTarget)
    {
        if (promptTarget == nextTarget)
        {
            if (promptTarget != null)
            {
                promptTarget.ShowPrompt(pickupPrompt);
            }

            return;
        }

        promptTarget?.HidePrompt();
        promptTarget = nextTarget;
        promptTarget?.ShowPrompt(pickupPrompt);
    }

    private void ClearPromptTarget()
    {
        promptTarget?.HidePrompt();
        promptTarget = null;
    }

    private void RefreshCarryPrompt()
    {
        if (!IsCarrying)
        {
            return;
        }

        carriedTarget.ShowPrompt(
            currentState == PlayerCarryState2D.Front
                ? frontCarryPrompt
                : backpackPrompt
        );
    }

    private void RefreshCarryText()
    {
        if (carryModeText == null)
        {
            return;
        }

        if (!IsCarrying)
        {
            carryModeText.text = string.Empty;
            carryModeText.gameObject.SetActive(false);
            return;
        }

        carryModeText.text = currentState == PlayerCarryState2D.Front
            ? frontCarryLabel
            : backpackLabel;

        carryModeText.gameObject.SetActive(true);
    }

    private void GetCurrentCarryPose(out Vector2 position, out float rotation)
    {
        GetCarryPoseForState(currentState, out position, out rotation);
    }

    private void GetCarryPoseForState(
        PlayerCarryState2D state,
        out Vector2 position,
        out float rotation)
    {
        bool facingRight = playerMove == null || playerMove.IsFacingRight;
        bool front = state != PlayerCarryState2D.Backpack;

        Transform point = front
            ? (facingRight ? frontCarryPointRight : frontCarryPointLeft)
            : (facingRight ? backpackPointRight : backpackPointLeft);

        float configuredRotation = front
            ? frontCarryRotation
            : backpackRotation;

        if (point != null)
        {
            position = point.position;
            rotation = keepCarriedObjectUpright
                ? configuredRotation
                : point.eulerAngles.z + configuredRotation;
            return;
        }

        Vector2 offset = front
            ? fallbackFrontCarryOffset
            : fallbackBackpackOffset;

        float direction = facingRight ? 1f : -1f;
        offset.x *= direction;

        Vector2 playerPosition = playerRigidbody != null
            ? playerRigidbody.position
            : (Vector2)transform.position;

        position = playerPosition + offset;
        rotation = configuredRotation;
    }

    private void GetDropPose(
        PlayerCarryState2D state,
        out Vector2 position,
        out float rotation,
        out Vector2 velocity)
    {
        bool facingRight = playerMove == null || playerMove.IsFacingRight;
        float direction = facingRight ? 1f : -1f;
        Transform point;

        if (state == PlayerCarryState2D.Backpack)
        {
            point = backpackDropPoint;

            position = point != null
                ? point.position
                : (Vector2)transform.position + fallbackBackpackDropOffset;

            velocity = Vector2.down * backpackDropDownwardVelocity;
        }
        else
        {
            point = facingRight ? frontDropPointRight : frontDropPointLeft;

            Vector2 offset = fallbackFrontDropOffset;
            offset.x *= direction;

            position = point != null
                ? point.position
                : (Vector2)transform.position + offset;

            velocity = new Vector2(
                direction * frontDropHorizontalVelocity,
                frontDropUpwardVelocity
            );
        }

        if (playerRigidbody != null)
        {
            velocity += playerRigidbody.linearVelocity * inheritedPlayerVelocity;
        }

        rotation = carriedTarget != null
            ? carriedTarget.transform.eulerAngles.z
            : 0f;
    }

    private void SnapObjectToCurrentCarryPoint()
    {
        if (!IsCarrying)
        {
            return;
        }

        GetCurrentCarryPose(out Vector2 position, out float rotation);
        carriedTarget.SnapCarryPose(position, rotation);
    }

    private Collider2D[] GetPlayerColliders()
    {
        return GetComponentsInChildren<Collider2D>(true);
    }

    private bool IsInputBlocked()
    {
        return !string.IsNullOrEmpty(GetBlockingPanelName());
    }

    private string GetBlockingPanelName()
    {
        if (panelsThatBlockInput == null)
        {
            return string.Empty;
        }

        foreach (GameObject panel in panelsThatBlockInput)
        {
            if (panel != null && panel.activeInHierarchy)
            {
                return panel.name;
            }
        }

        return string.Empty;
    }

    private void SetAnimatorBool(string parameterName, bool value)
    {
        if (playerAnimator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        playerAnimator.SetBool(parameterName, value);
    }

    private void SetAnimatorTrigger(string parameterName)
    {
        if (playerAnimator == null || string.IsNullOrWhiteSpace(parameterName))
        {
            return;
        }

        playerAnimator.ResetTrigger(parameterName);
        playerAnimator.SetTrigger(parameterName);
    }

    private float GetCurrentCarrySpeedMultiplier()
    {
        if (!enableCarryWeightSlowdown || carriedTarget == null)
        {
            return 1f;
        }

        float weight = Mathf.Max(0f, carriedTarget.CarryWeightKg);
        float evaluated = carryWeightSpeedCurve != null
            ? carryWeightSpeedCurve.Evaluate(weight)
            : 1f;

        return Mathf.Clamp(
            evaluated,
            minimumCarrySpeedMultiplier,
            1f
        );
    }

    private void ApplyCarryWeightSlowdown()
    {
        if (playerWeightController == null)
        {
            FindReferences();
        }

        if (playerWeightController == null)
        {
            if (enableCarryWeightSlowdown && IsCarrying)
            {
                LogWarning(
                    "[Carry診断][重量] PlayerWeightControllerが見つからないため、運搬重量による速度低下を反映できません。"
                );
            }
            return;
        }

        float multiplier = GetCurrentCarrySpeedMultiplier();
        playerWeightController.SetCarryLoadState(
            IsCarrying && enableCarryWeightSlowdown,
            multiplier
        );

        if (showDebugLogs && IsCarrying && carriedTarget != null)
        {
            Log(
                $"[Carry診断][重量] Object={carriedTarget.name} / " +
                $"Weight={carriedTarget.CarryWeightKg:0.0}kg / Speed×{multiplier:0.00}"
            );
        }
    }

    private void ClearCarryWeightSlowdown()
    {
        if (playerWeightController == null)
        {
            FindReferences();
        }

        playerWeightController?.SetCarryLoadState(false, 1f);
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
            equipmentVisualController = GetComponent<PlayerEquipmentVisualController>();
        }

        if (wallClimbController == null)
        {
            wallClimbController = GetComponent<WallClimbController>();
        }

        if (ropePullController == null)
        {
            ropePullController = GetComponent<PlayerRopePullController>();
        }

        if (stoneThrower == null)
        {
            stoneThrower = GetComponent<StoneThrower>();
        }

        if (proneController == null)
        {
            proneController = GetComponent<PlayerProneController>();
        }

        if (playerWeightController == null)
        {
            playerWeightController = GetComponent<PlayerWeightController>();
        }

        if (playerAnimator == null)
        {
            playerAnimator = GetComponentInChildren<Animator>(true);
        }
    }

    [ContextMenu("Log Carry Diagnostics")]
    public void LogCarryDiagnosticsFromContextMenu()
    {
        FindReferences();
        LogCarryDiagnostics("ContextMenu");
    }

    private void LogCarryDiagnostics(string phase)
    {
        if (!showDetailedDiagnostics)
        {
            return;
        }

        Debug.Log(
            $"[Carry診断][{phase}] Player={name} / Active={isActiveAndEnabled} / " +
            $"State={currentState} / IsCarrying={IsCarrying} / " +
            $"CarriedTarget={(carriedTarget != null ? carriedTarget.name : "なし")} / " +
            $"PromptTarget={(promptTarget != null ? promptTarget.name : "なし")} / " +
            $"Radius={interactionRadius:0.00} / CarryLayer={FormatLayerMask(carryableObjectLayers)}\n" +
            $"References: Rigidbody={(playerRigidbody != null ? "OK" : "未設定")}, " +
            $"PlayerMove={(playerMove != null ? "OK" : "未設定")}, " +
            $"Health={(playerHealth != null ? "OK" : "未設定")}, " +
            $"EquipmentVisual={(equipmentVisualController != null ? "OK" : "未設定")}, " +
            $"WallClimb={(wallClimbController != null ? "OK" : "未設定")}, " +
            $"RopePull={(ropePullController != null ? "OK" : "未設定")}, " +
            $"StoneThrower={(stoneThrower != null ? "OK" : "未設定")}, " +
            $"Prone={(proneController != null ? "OK" : "未設定")}, " +
            $"Weight={(playerWeightController != null ? "OK" : "未設定")}",
            this
        );

        if (playerRigidbody == null)
        {
            LogWarning("[Carry診断][設定不足] PlayerにRigidbody2Dがありません。");
        }

        if (playerMove == null)
        {
            LogWarning("[Carry診断][設定不足] PlayerMoveが見つかりません。左右位置の切替が正しく動かない可能性があります。");
        }

        if (equipmentVisualController == null)
        {
            LogWarning("[Carry診断][設定不足] PlayerEquipmentVisualControllerがありません。手前持ち中も銃を止められません。");
        }

        if (wallClimbController == null)
        {
            LogWarning("[Carry診断][設定不足] WallClimbControllerがありません。手前持ち中の壁登りを止められません。");
        }

        if (frontCarryPointRight == null || frontCarryPointLeft == null)
        {
            Log("[Carry診断][情報] Front Carry Pointが一部未設定です。Fallback Offsetを使用します。");
        }

        if (backpackPointRight == null || backpackPointLeft == null)
        {
            Log("[Carry診断][情報] Backpack Pointが一部未設定です。Fallback Offsetを使用します。");
        }
    }

    private void LogDiagnosticState(string stateKey, string message)
    {
        if (!showDetailedDiagnostics)
        {
            return;
        }

        if (lastDiagnosticState == stateKey && Time.unscaledTime < nextRepeatedDiagnosticTime)
        {
            return;
        }

        lastDiagnosticState = stateKey;
        nextRepeatedDiagnosticTime = Time.unscaledTime + repeatedDiagnosticInterval;
        Debug.Log(message, this);
    }

    private static string FormatLayerMask(LayerMask mask)
    {
        if (mask.value == 0)
        {
            return "Everything（未指定のため全Layer）";
        }

        System.Collections.Generic.List<string> names =
            new System.Collections.Generic.List<string>();

        for (int layer = 0; layer < 32; layer++)
        {
            if ((mask.value & (1 << layer)) == 0)
            {
                continue;
            }

            string layerName = LayerMask.LayerToName(layer);
            names.Add(string.IsNullOrWhiteSpace(layerName) ? layer.ToString() : layerName);
        }

        return names.Count > 0 ? string.Join(", ", names) : mask.value.ToString();
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PlayerCarryController2D] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[PlayerCarryController2D] {message}", this);
    }

    private void OnValidate()
    {
        interactionRadius = Mathf.Max(0.1f, interactionRadius);
        frontDropHorizontalVelocity = Mathf.Max(0f, frontDropHorizontalVelocity);
        frontDropUpwardVelocity = Mathf.Max(0f, frontDropUpwardVelocity);
        backpackDropDownwardVelocity = Mathf.Max(0f, backpackDropDownwardVelocity);
        inheritedPlayerVelocity = Mathf.Clamp01(inheritedPlayerVelocity);
        playerCollisionIgnoreDuration = Mathf.Max(0f, playerCollisionIgnoreDuration);
        backpackTransitionDuration = Mathf.Max(0f, backpackTransitionDuration);
        minimumCarrySpeedMultiplier = Mathf.Clamp(minimumCarrySpeedMultiplier, 0.05f, 1f);
        repeatedDiagnosticInterval = Mathf.Max(0.1f, repeatedDiagnosticInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showInteractionGizmo)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
