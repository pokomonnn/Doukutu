using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// プレイヤーが手前に持つ、または背負うことができる物体です。
/// 持ち運び中は物理挙動を一時停止し、落とした時に元の設定へ戻します。
/// RopePullTargetが付いている場合、持ち上げる前に引っ張りロープを切り離します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class CarryableObject2D : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Rigidbody2D targetRigidbody;

    [Tooltip("E表示の距離判定に使う位置です。未設定ならObject中心を使います")]
    [SerializeField] private Transform interactionPoint;

    [Tooltip("Objectの近くや運搬中に表示するWorld Space TMP Textです")]
    [SerializeField] private TMP_Text interactionText;

    [Header("物理設定")]
    [Tooltip("オンなら開始時にRigidbody2DをDynamicへ変更します")]
    [SerializeField] private bool forceDynamicBodyOnAwake = true;

    [Tooltip("持ち運び中は、この物体のRigidbody2Dに属するColliderをすべて無効にします。通常はオンがおすすめです")]
    [SerializeField] private bool disableCollidersWhileCarried = true;

    [Tooltip("落とした直後、Playerとの衝突を一時的に無視する秒数です")]
    [SerializeField, Min(0f)] private float defaultPlayerCollisionIgnoreDuration = 0.25f;

    [Tooltip("落とした直後に、すぐ持ち直さないための待ち時間です")]
    [SerializeField, Min(0f)] private float repickupDelay = 0.2f;

    [Header("運搬重量")]
    [Tooltip("この物体を持つ／背負う時にプレイヤーへかかる重量です。kg単位で設定します")]
    [SerializeField, Min(0f)] private float carryWeightKg = 10f;

    [Header("表示")]
    [SerializeField] private bool applyPromptLocalPosition = true;
    [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 1.1f, 0f);

    [Header("デバッグ診断")]
    [Tooltip("持ち上げ・物理切替・落下の通常ログを表示します")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("参照、Collider、Rigidbody2D、取得不可理由を詳しく表示します")]
    [SerializeField] private bool showDetailedDiagnostics = true;

    public Rigidbody2D TargetRigidbody => targetRigidbody;
    public float CarryWeightKg => Mathf.Max(0f, carryWeightKg);

    public Vector2 InteractionWorldPosition =>
        interactionPoint != null
            ? interactionPoint.position
            : transform.position;

    public bool IsCarried => currentCarrier != null;
    public bool IsReserved => currentCarrier != null;
    public bool CanBePickedUp =>
        isActiveAndEnabled &&
        targetRigidbody != null &&
        !IsCarried &&
        Time.time >= nextPickupAllowedTime;

    public float RemainingRepickupDelay => Mathf.Max(
        0f,
        nextPickupAllowedTime - Time.time
    );

    public PlayerCarryController2D CurrentCarrier => currentCarrier;

    private PlayerCarryController2D currentCarrier;
    private float nextPickupAllowedTime;

    private Transform originalParent;
    private RigidbodyType2D originalBodyType;
    private float originalGravityScale;
    private float originalLinearDamping;
    private float originalAngularDamping;
    private RigidbodyConstraints2D originalConstraints;
    private RigidbodyInterpolation2D originalInterpolation;
    private CollisionDetectionMode2D originalCollisionDetectionMode;
    private bool originalSimulated;
    private bool hasCachedPhysics;

    private readonly List<ColliderState> colliderStates =
        new List<ColliderState>();

    private readonly List<CollisionPair> ignoredPlayerCollisionPairs =
        new List<CollisionPair>();

    private Coroutine restorePlayerCollisionCoroutine;

    private sealed class ColliderState
    {
        public Collider2D Collider;
        public bool WasEnabled;
    }

    private sealed class CollisionPair
    {
        public Collider2D ObjectCollider;
        public Collider2D PlayerCollider;
    }

    private void Awake()
    {
        FindReferences();
        ApplyInitialPhysicsSettings();
        HidePrompt();
        LogCarryableDiagnostics("Awake");
    }

    private void OnEnable()
    {
        FindReferences();
        ApplyInitialPhysicsSettings();
        LogCarryableDiagnostics("OnEnable");

        if (!IsCarried)
        {
            HidePrompt();
        }
    }

    private void Start()
    {
        if (!showDetailedDiagnostics)
        {
            return;
        }

        PlayerCarryController2D[] controllers =
            FindObjectsByType<PlayerCarryController2D>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (controllers == null || controllers.Length == 0)
        {
            LogWarning(
                "[Carry診断][連携不足] Scene内にPlayerCarryController2Dがありません。" +
                "Playerへ追加してください。"
            );
            return;
        }

        if (controllers.Length > 1)
        {
            LogWarning(
                $"[Carry診断][重複] PlayerCarryController2Dが{controllers.Length}個あります。" +
                "通常はPlayerに1個だけ配置してください。"
            );
        }

        PlayerCarryController2D controller = controllers[0];
        Collider2D[] colliders = GetComponentsInChildren<Collider2D>(true);
        bool hasAllowedCollider = false;

        foreach (Collider2D collider in colliders)
        {
            if (collider != null &&
                collider.enabled &&
                controller.IsLayerAllowedForCarry(collider.gameObject.layer))
            {
                hasAllowedCollider = true;
                break;
            }
        }

        if (!hasAllowedCollider)
        {
            LogWarning(
                $"[Carry診断][Layer不一致] このObjectの有効Colliderが、PlayerのCarryable Object Layersに含まれていません。" +
                $"ObjectLayer={LayerMask.LayerToName(gameObject.layer)} / " +
                $"PlayerMask={controller.CarryableObjectLayers.value}"
            );
        }

        float distance = Vector2.Distance(
            controller.transform.position,
            InteractionWorldPosition
        );

        Log(
            $"[Carry診断][Player連携] Player={controller.name} / " +
            $"CurrentDistance={distance:0.00} / RequiredRadius={controller.InteractionRadius:0.00} / " +
            $"LayerAllowed={hasAllowedCollider}"
        );
    }

    private void OnDisable()
    {
        HidePrompt();
        RestoreIgnoredPlayerCollisions();

        if (currentCarrier != null)
        {
            PlayerCarryController2D carrier = currentCarrier;
            currentCarrier = null;
            RestorePhysicsAndColliders();
            carrier.NotifyCarryTargetUnavailable(this);
        }
    }

    public string GetPickupBlockReason()
    {
        if (!isActiveAndEnabled)
        {
            return "CarryableObject2Dが無効";
        }

        if (targetRigidbody == null)
        {
            return "Rigidbody2Dがない";
        }

        if (IsCarried)
        {
            return $"運搬中（Carrier={currentCarrier.name}）";
        }

        if (RemainingRepickupDelay > 0f)
        {
            return $"再取得待ち {RemainingRepickupDelay:0.00}秒";
        }

        return "取得可能";
    }

    public bool TryBeginCarry(PlayerCarryController2D carrier)
    {
        Log(
            $"[Carry診断][TryBeginCarry開始] Carrier={(carrier != null ? carrier.name : "null")} / " +
            $"Active={isActiveAndEnabled} / CanBePickedUp={CanBePickedUp} / " +
            $"CurrentCarrier={(currentCarrier != null ? currentCarrier.name : "なし")}"
        );

        if (carrier == null)
        {
            LogWarning("[Carry診断][TryBeginCarry失敗] Carrierがnullです。");
            return false;
        }

        if (!isActiveAndEnabled)
        {
            LogWarning("[Carry診断][TryBeginCarry失敗] CarryableObject2Dが無効です。");
            return false;
        }

        FindReferences();

        if (!CanBePickedUp)
        {
            LogWarning(
                $"[Carry診断][TryBeginCarry失敗] 現在持てません。理由={GetPickupBlockReason()}"
            );
            return false;
        }

        if (currentCarrier != null && currentCarrier != carrier)
        {
            LogWarning(
                $"[Carry診断][TryBeginCarry失敗] 別のPlayerが予約中です。Carrier={currentCarrier.name}"
            );
            return false;
        }

        if (targetRigidbody == null)
        {
            LogWarning("[Carry診断][TryBeginCarry失敗] Rigidbody2Dが見つかりません。");
            return false;
        }

        RopePullTarget ropeTarget = GetComponent<RopePullTarget>();
        if (ropeTarget != null && ropeTarget.CurrentController != null)
        {
            ropeTarget.CurrentController.DetachRope();
        }

        RestoreIgnoredPlayerCollisions();
        CachePhysicsAndColliders();

        if (!hasCachedPhysics)
        {
            LogWarning("[Carry診断][TryBeginCarry失敗] 物理設定を保存できませんでした。");
            return false;
        }

        currentCarrier = carrier;
        originalParent = transform.parent;

        // 持ち運び中は必ずPlayerとの衝突を無視します。
        // Colliderの無効化設定に依存しない二重の安全対策です。
        EnsurePlayerCollisionsIgnored(
            carrier.GetComponentsInChildren<Collider2D>(true)
        );

        targetRigidbody.simulated = true;
        targetRigidbody.bodyType = RigidbodyType2D.Kinematic;
        targetRigidbody.gravityScale = 0f;
        targetRigidbody.linearVelocity = Vector2.zero;
        targetRigidbody.angularVelocity = 0f;
        targetRigidbody.constraints = RigidbodyConstraints2D.FreezeRotation;
        targetRigidbody.interpolation = RigidbodyInterpolation2D.Interpolate;

        if (disableCollidersWhileCarried)
        {
            foreach (ColliderState state in colliderStates)
            {
                if (state.Collider != null)
                {
                    state.Collider.enabled = false;
                }
            }
        }

        Log(
            $"[Carry診断][TryBeginCarry成功] BodyType={targetRigidbody.bodyType} / " +
            $"Gravity={targetRigidbody.gravityScale:0.###} / " +
            $"ColliderCount={colliderStates.Count} / " +
            $"DisableColliders={disableCollidersWhileCarried} / " +
            $"IgnoredPlayerCollisionPairs={ignoredPlayerCollisionPairs.Count}"
        );
        return true;
    }

    public void SetCarryPose(Vector2 worldPosition, float worldRotation)
    {
        if (!IsCarried || targetRigidbody == null)
        {
            return;
        }

        targetRigidbody.MovePosition(worldPosition);
        targetRigidbody.MoveRotation(worldRotation);
        targetRigidbody.linearVelocity = Vector2.zero;
        targetRigidbody.angularVelocity = 0f;
    }

    public void SnapCarryPose(Vector2 worldPosition, float worldRotation)
    {
        if (!IsCarried || targetRigidbody == null)
        {
            return;
        }

        targetRigidbody.position = worldPosition;
        targetRigidbody.rotation = worldRotation;
        transform.position = worldPosition;
        transform.rotation = Quaternion.Euler(0f, 0f, worldRotation);
        targetRigidbody.linearVelocity = Vector2.zero;
        targetRigidbody.angularVelocity = 0f;
    }

    public void ReleaseFromCarry(
        PlayerCarryController2D carrier,
        Vector2 worldPosition,
        float worldRotation,
        Vector2 releaseVelocity,
        Collider2D[] playerColliders,
        float playerCollisionIgnoreDuration = -1f)
    {
        if (carrier == null)
        {
            LogWarning("[Carry診断][Release失敗] Carrierがnullです。");
            return;
        }

        if (currentCarrier != carrier)
        {
            LogWarning(
                $"[Carry診断][Release失敗] Carrierが一致しません。" +
                $"Current={(currentCarrier != null ? currentCarrier.name : "なし")} / Requested={carrier.name}"
            );
            return;
        }

        currentCarrier = null;
        nextPickupAllowedTime = Time.time + repickupDelay;

        transform.SetParent(originalParent, true);
        transform.position = worldPosition;
        transform.rotation = Quaternion.Euler(0f, 0f, worldRotation);

        RestorePhysicsAndColliders();

        if (targetRigidbody != null)
        {
            targetRigidbody.position = worldPosition;
            targetRigidbody.rotation = worldRotation;
            targetRigidbody.linearVelocity = releaseVelocity;
            targetRigidbody.angularVelocity = 0f;
        }

        float ignoreDuration = playerCollisionIgnoreDuration >= 0f
            ? playerCollisionIgnoreDuration
            : defaultPlayerCollisionIgnoreDuration;

        if (playerColliders != null)
        {
            // 運搬中から無視していた衝突設定を維持したままColliderを復元し、
            // 落とした後にだけ遅延して元へ戻します。
            EnsurePlayerCollisionsIgnored(playerColliders);

            if (ignoreDuration > 0f)
            {
                SchedulePlayerCollisionRestore(ignoreDuration);
            }
            else
            {
                RestoreIgnoredPlayerCollisions();
            }
        }
        else
        {
            RestoreIgnoredPlayerCollisions();
        }

        HidePrompt();
        Log(
            $"[Carry診断][Release成功] Position={worldPosition} / Velocity={releaseVelocity} / " +
            $"BodyType={(targetRigidbody != null ? targetRigidbody.bodyType.ToString() : "なし")} / " +
            $"Gravity={(targetRigidbody != null ? targetRigidbody.gravityScale.ToString("0.###") : "なし")}"
        );
    }

    public void ForceReleaseWithoutPhysicsRestore(PlayerCarryController2D carrier)
    {
        if (currentCarrier != carrier)
        {
            return;
        }

        currentCarrier = null;
        nextPickupAllowedTime = Time.time + repickupDelay;
        RestorePhysicsAndColliders();
        HidePrompt();
    }

    public void ShowPrompt(string message)
    {
        FindPromptText();

        if (interactionText == null)
        {
            if (showDetailedDiagnostics)
            {
                LogWarning(
                    "[Carry診断][表示不可] Interaction Textが見つからないため、E/F表示を出せません。" +
                    "CarryableObject2Dの子にWorld Space TMP Textを置いてください。"
                );
            }
            return;
        }

        interactionText.text = message ?? string.Empty;
        interactionText.gameObject.SetActive(true);
        interactionText.enabled = true;
    }

    public void HidePrompt()
    {
        FindPromptText();

        if (interactionText == null)
        {
            return;
        }

        interactionText.text = string.Empty;
        interactionText.enabled = false;
    }

    private void CachePhysicsAndColliders()
    {
        if (hasCachedPhysics)
        {
            return;
        }

        if (targetRigidbody == null)
        {
            LogWarning("[Carry診断][物理保存失敗] Rigidbody2Dがありません。");
            return;
        }

        originalBodyType = targetRigidbody.bodyType;
        originalGravityScale = targetRigidbody.gravityScale;
        originalLinearDamping = targetRigidbody.linearDamping;
        originalAngularDamping = targetRigidbody.angularDamping;
        originalConstraints = targetRigidbody.constraints;
        originalInterpolation = targetRigidbody.interpolation;
        originalCollisionDetectionMode = targetRigidbody.collisionDetectionMode;
        originalSimulated = targetRigidbody.simulated;

        colliderStates.Clear();
        Collider2D[] colliders = GetOwnedColliders();

        foreach (Collider2D collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            colliderStates.Add(new ColliderState
            {
                Collider = collider,
                WasEnabled = collider.enabled
            });
        }

        hasCachedPhysics = true;

        Log(
            $"[Carry診断][物理保存] BodyType={originalBodyType} / Gravity={originalGravityScale:0.###} / " +
            $"Simulated={originalSimulated} / Constraints={originalConstraints} / " +
            $"ColliderCount={colliderStates.Count}"
        );
    }

    private void RestorePhysicsAndColliders()
    {
        if (!hasCachedPhysics)
        {
            return;
        }

        foreach (ColliderState state in colliderStates)
        {
            if (state.Collider != null)
            {
                state.Collider.enabled = state.WasEnabled;
            }
        }

        if (targetRigidbody != null)
        {
            targetRigidbody.simulated = originalSimulated;
            targetRigidbody.bodyType = originalBodyType;
            targetRigidbody.gravityScale = originalGravityScale;
            targetRigidbody.linearDamping = originalLinearDamping;
            targetRigidbody.angularDamping = originalAngularDamping;
            targetRigidbody.constraints = originalConstraints;
            targetRigidbody.interpolation = originalInterpolation;
            targetRigidbody.collisionDetectionMode = originalCollisionDetectionMode;
        }

        Log(
            $"[Carry診断][物理復元] BodyType={(targetRigidbody != null ? targetRigidbody.bodyType.ToString() : "なし")} / " +
            $"Gravity={(targetRigidbody != null ? targetRigidbody.gravityScale.ToString("0.###") : "なし")} / " +
            $"ColliderCount={colliderStates.Count}"
        );

        hasCachedPhysics = false;
        colliderStates.Clear();
    }

    /// <summary>
    /// CarryableObject2Dの配置階層だけでなく、Target Rigidbodyに実際に接続されている
    /// Colliderも収集します。Colliderが親・兄弟ObjectにあるPrefab構成にも対応します。
    /// </summary>
    private Collider2D[] GetOwnedColliders()
    {
        HashSet<Collider2D> result = new HashSet<Collider2D>();

        Collider2D[] localColliders =
            GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider in localColliders)
        {
            if (collider != null)
            {
                result.Add(collider);
            }
        }

        if (targetRigidbody != null)
        {
            Transform searchRoot = targetRigidbody.transform.root;
            Collider2D[] rootColliders =
                searchRoot.GetComponentsInChildren<Collider2D>(true);

            foreach (Collider2D collider in rootColliders)
            {
                if (collider == null)
                {
                    continue;
                }

                // Rigidbody2Dへ接続されているColliderだけを追加し、
                // 同じPrefab内の無関係なColliderは巻き込まないようにします。
                if (collider.attachedRigidbody == targetRigidbody)
                {
                    result.Add(collider);
                }
            }
        }

        Collider2D[] array = new Collider2D[result.Count];
        result.CopyTo(array);
        return array;
    }

    /// <summary>
    /// 既に無視中の組み合わせは維持しながら、不足しているPlayerとの
    /// Collider組み合わせだけを追加します。
    /// </summary>
    private void EnsurePlayerCollisionsIgnored(
        Collider2D[] playerColliders)
    {
        if (playerColliders == null)
        {
            return;
        }

        Collider2D[] objectColliders = GetOwnedColliders();

        foreach (Collider2D objectCollider in objectColliders)
        {
            if (objectCollider == null)
            {
                continue;
            }

            foreach (Collider2D playerCollider in playerColliders)
            {
                if (playerCollider == null ||
                    objectCollider == playerCollider)
                {
                    continue;
                }

                bool alreadyRegistered = false;

                foreach (CollisionPair pair in ignoredPlayerCollisionPairs)
                {
                    if (pair.ObjectCollider == objectCollider &&
                        pair.PlayerCollider == playerCollider)
                    {
                        alreadyRegistered = true;
                        break;
                    }
                }

                if (alreadyRegistered)
                {
                    continue;
                }

                Physics2D.IgnoreCollision(
                    objectCollider,
                    playerCollider,
                    true
                );

                ignoredPlayerCollisionPairs.Add(new CollisionPair
                {
                    ObjectCollider = objectCollider,
                    PlayerCollider = playerCollider
                });
            }
        }

        Log(
            $"[Carry診断][衝突無視] PlayerとのCollider組み合わせ=" +
            ignoredPlayerCollisionPairs.Count
        );
    }

    private void SchedulePlayerCollisionRestore(float duration)
    {
        if (restorePlayerCollisionCoroutine != null)
        {
            StopCoroutine(restorePlayerCollisionCoroutine);
        }

        restorePlayerCollisionCoroutine = StartCoroutine(
            RestorePlayerCollisionsAfterDelay(duration)
        );
    }

    private IEnumerator RestorePlayerCollisionsAfterDelay(float duration)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, duration));
        RestoreIgnoredPlayerCollisions();
    }

    private void RestoreIgnoredPlayerCollisions()
    {
        if (restorePlayerCollisionCoroutine != null)
        {
            StopCoroutine(restorePlayerCollisionCoroutine);
            restorePlayerCollisionCoroutine = null;
        }

        foreach (CollisionPair pair in ignoredPlayerCollisionPairs)
        {
            if (pair.ObjectCollider != null && pair.PlayerCollider != null)
            {
                Physics2D.IgnoreCollision(
                    pair.ObjectCollider,
                    pair.PlayerCollider,
                    false
                );
            }
        }

        if (ignoredPlayerCollisionPairs.Count > 0)
        {
            Log(
                $"[Carry診断][衝突復元] PlayerとのCollider組み合わせ=" +
                ignoredPlayerCollisionPairs.Count
            );
        }

        ignoredPlayerCollisionPairs.Clear();
    }

    private void ApplyInitialPhysicsSettings()
    {
        if (targetRigidbody == null)
        {
            return;
        }

        if (forceDynamicBodyOnAwake && !IsCarried)
        {
            targetRigidbody.bodyType = RigidbodyType2D.Dynamic;
            targetRigidbody.simulated = true;
        }

        if (!IsCarried && targetRigidbody.bodyType != RigidbodyType2D.Dynamic)
        {
            Debug.LogWarning(
                $"[CarryableObject2D] {name} のRigidbody2DはDynamicを推奨します。",
                this
            );
        }
    }

    private void FindReferences()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody2D>();
        }

        // Prefab階層の都合でRigidbody2Dが親や子に置かれている場合にも対応します。
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponentInParent<Rigidbody2D>();
        }

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponentInChildren<Rigidbody2D>(true);
        }

        FindPromptText();

        if (interactionText != null && applyPromptLocalPosition)
        {
            interactionText.transform.localPosition = promptLocalPosition;
        }
    }

    private void FindPromptText()
    {
        if (interactionText == null)
        {
            interactionText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    [ContextMenu("Log Carryable Diagnostics")]
    public void LogCarryableDiagnosticsFromContextMenu()
    {
        FindReferences();
        LogCarryableDiagnostics("ContextMenu");
    }

    private void LogCarryableDiagnostics(string phase)
    {
        if (!showDetailedDiagnostics)
        {
            return;
        }

        Collider2D[] allColliders = GetComponentsInChildren<Collider2D>(true);
        int enabledColliderCount = 0;
        int triggerColliderCount = 0;

        foreach (Collider2D collider in allColliders)
        {
            if (collider == null)
            {
                continue;
            }

            if (collider.enabled)
            {
                enabledColliderCount++;
            }

            if (collider.isTrigger)
            {
                triggerColliderCount++;
            }
        }

        Debug.Log(
            $"[Carry診断][対象][{phase}] Object={name} / Active={isActiveAndEnabled} / " +
            $"CanBePickedUp={CanBePickedUp} / Reason={GetPickupBlockReason()} / " +
            $"Layer={LayerMask.LayerToName(gameObject.layer)} / " +
            $"InteractionPoint={(interactionPoint != null ? interactionPoint.name : "Object中心")} / " +
            $"InteractionText={(interactionText != null ? interactionText.name : "未設定")}\n" +
            $"Rigidbody={(targetRigidbody != null ? "OK" : "未設定")} / " +
            $"BodyType={(targetRigidbody != null ? targetRigidbody.bodyType.ToString() : "なし")} / " +
            $"Simulated={(targetRigidbody != null ? targetRigidbody.simulated.ToString() : "なし")} / " +
            $"Colliders={allColliders.Length}（Enabled={enabledColliderCount}, Trigger={triggerColliderCount}）",
            this
        );

        if (targetRigidbody == null)
        {
            LogWarning("[Carry診断][設定不足] Rigidbody2Dがありません。");
        }

        if (allColliders.Length == 0)
        {
            LogWarning(
                "[Carry診断][設定不足] Collider2Dがありません。Playerの探索に検出されません。"
            );
        }

        if (enabledColliderCount == 0)
        {
            LogWarning(
                "[Carry診断][設定不足] 有効なCollider2Dがありません。Playerの探索に検出されません。"
            );
        }

        if (interactionText == null)
        {
            LogWarning(
                "[Carry診断][設定不足] Interaction Textがありません。E/F案内は表示されません。"
            );
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[CarryableObject2D: {name}] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[CarryableObject2D: {name}] {message}", this);
    }

    private void OnValidate()
    {
        carryWeightKg = Mathf.Max(0f, carryWeightKg);
        defaultPlayerCollisionIgnoreDuration = Mathf.Max(
            0f,
            defaultPlayerCollisionIgnoreDuration
        );
        repickupDelay = Mathf.Max(0f, repickupDelay);
        FindReferences();
    }
}
