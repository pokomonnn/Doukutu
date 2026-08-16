using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// エレベーター前でPlayerがCarryableObject2Dを下ろした時、
/// 対象をエレベーター内部の空いているCargo Slotへ自動収納します。
///
/// 推奨構成：
/// Elevator
/// ├ ReceiverZone (Trigger + このComponent)
/// ├ CargoSlot_01
/// ├ CargoSlot_02
/// └ CargoSlot_03
///
/// PlayerCarryController2D.AnyCarryableDroppedを監視するため、
/// NPC・ItemBoxなどCarryableObject2Dを持つ物体を共通で扱えます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ElevatorCarryReceiver2D : MonoBehaviour
{
    [Header("受取範囲")]
    [Tooltip("エレベーター前の受取Trigger。未設定なら同じObjectのCollider2Dを使います。")]
    [SerializeField] private Collider2D receiverTrigger;

    [Tooltip("受け付けるCarryableObject2DのLayer。例：Carry。0なら全Layerを許可します。")]
    [SerializeField] private LayerMask acceptedLayers;

    [Tooltip("ONならFキーで手動で下ろした時だけ反応します。死亡などによる強制Dropでは収納しません。")]
    [SerializeField] private bool requireManualDrop = true;

    [Tooltip("ONなら背負い状態から下ろした物だけを収納します。")]
    [SerializeField] private bool acceptOnlyBackpackDrops = true;

    [Header("収納位置")]
    [Tooltip("ONなら子ObjectのElevatorCargoSlot2Dを自動取得します。")]
    [SerializeField] private bool autoFindCargoSlotsInChildren = true;

    [SerializeField] private List<ElevatorCargoSlot2D> cargoSlots =
        new List<ElevatorCargoSlot2D>();

    [Header("自動移動")]
    [Tooltip("下ろした位置からCargo Slotまで移動する秒数。0なら即座に入ります。")]
    [SerializeField, Min(0f)] private float moveDuration = 0.55f;

    [SerializeField] private AnimationCurve moveCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("自動移動中だけColliderを一時的に無効化します。")]
    [SerializeField] private bool disableCollidersWhileMoving = true;

    [Tooltip("収納後はRigidbody2D.simulatedをOFFにして、エレベーターと完全に一緒に動かします。")]
    [SerializeField] private bool disableSimulationWhileStored = true;

    [Header("解放")]
    [Tooltip("ONならこのReceiverが無効になった時、収納中の荷物を物理状態へ戻します。通常はOFF推奨です。")]
    [SerializeField] private bool releaseCargoWhenDisabled;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public int StoredCargoCount => cargoRecords.Count;
    public int FreeSlotCount => CountFreeSlots();

    /// <summary>
    /// 現在エレベーターが受け取っているCarryableObject2Dの一覧を返します。
    /// Result集計では、収納完了済みだけでなく自動移動中の荷物も対象にできます。
    /// </summary>
    public List<CarryableObject2D> GetCargoSnapshot(
        bool includeMovingTargets = true)
    {
        List<CarryableObject2D> result =
            new List<CarryableObject2D>();

        foreach (CarryableObject2D target in cargoRecords.Keys)
        {
            if (target != null && !result.Contains(target))
            {
                result.Add(target);
            }
        }

        if (includeMovingTargets)
        {
            foreach (CarryableObject2D target in movingTargets)
            {
                if (target != null && !result.Contains(target))
                {
                    result.Add(target);
                }
            }
        }

        return result;
    }

    private sealed class ColliderState
    {
        public Collider2D Collider;
        public bool WasEnabled;
    }

    private sealed class CargoRecord
    {
        public CarryableObject2D Target;
        public ElevatorCargoSlot2D Slot;
        public Transform OriginalParent;

        public Rigidbody2D Rigidbody;
        public RigidbodyType2D BodyType;
        public float GravityScale;
        public float LinearDamping;
        public float AngularDamping;
        public RigidbodyConstraints2D Constraints;
        public RigidbodyInterpolation2D Interpolation;
        public CollisionDetectionMode2D CollisionDetectionMode;
        public bool Simulated;

        public readonly List<ColliderState> Colliders =
            new List<ColliderState>();
    }

    private readonly Dictionary<CarryableObject2D, CargoRecord> cargoRecords =
        new Dictionary<CarryableObject2D, CargoRecord>();

    private readonly HashSet<CarryableObject2D> movingTargets =
        new HashSet<CarryableObject2D>();

    private void Awake()
    {
        FindReferences();
        RefreshCargoSlots();
    }

    private void OnEnable()
    {
        FindReferences();
        RefreshCargoSlots();
        PlayerCarryController2D.AnyCarryableDropped += HandleAnyCarryableDropped;
    }

    private void OnDisable()
    {
        PlayerCarryController2D.AnyCarryableDropped -= HandleAnyCarryableDropped;

        if (releaseCargoWhenDisabled)
        {
            ReleaseAllCargo();
        }
    }

    private void HandleAnyCarryableDropped(
        PlayerCarryController2D player,
        CarryableObject2D target,
        PlayerCarryState2D droppedFromState,
        bool manualDrop)
    {
        if (target == null)
        {
            return;
        }

        if (requireManualDrop && !manualDrop)
        {
            return;
        }

        if (acceptOnlyBackpackDrops &&
            droppedFromState != PlayerCarryState2D.Backpack)
        {
            return;
        }

        if (!IsLayerAccepted(target))
        {
            Log($"受取対象外Layer: {target.name} / Layer={LayerMask.LayerToName(target.gameObject.layer)}");
            return;
        }

        if (!IsTargetInsideReceiver(target))
        {
            return;
        }

        TryStoreCargo(target);
    }

    /// <summary>
    /// 外部から明示的に収納させたい時にも使えます。
    /// </summary>
    public bool TryStoreCargo(CarryableObject2D target)
    {
        if (target == null || target.IsCarried ||
            cargoRecords.ContainsKey(target) || movingTargets.Contains(target))
        {
            return false;
        }

        ElevatorCargoSlot2D slot = FindFirstFreeSlot();

        if (slot == null)
        {
            LogWarning($"空いているCargo Slotがありません。{target.name} はその場に残します。");
            return false;
        }

        if (!target.TryReserveForExternalSystem(this))
        {
            LogWarning($"{target.name} をエレベーター用に確保できませんでした。");
            return false;
        }

        if (!slot.TryAssign(target))
        {
            target.ReleaseExternalReservation(this);
            return false;
        }

        CargoRecord record = CaptureCargoState(target, slot);
        movingTargets.Add(target);
        StartCoroutine(MoveCargoRoutine(record));

        Log($"収納開始: {target.name} → {slot.name}");
        return true;
    }

    private IEnumerator MoveCargoRoutine(CargoRecord record)
    {
        if (record == null || record.Target == null || record.Slot == null)
        {
            yield break;
        }

        CarryableObject2D target = record.Target;
        Transform targetTransform = target.transform;
        Rigidbody2D rb = record.Rigidbody;

        PreparePhysicsForMove(record);

        Vector3 startPosition = targetTransform.position;
        float startRotation = targetTransform.eulerAngles.z;
        Vector3 endPosition = record.Slot.WorldPosition;
        float endRotation = record.Slot.WorldRotation;

        float duration = Mathf.Max(0f, moveDuration);

        if (duration > 0f)
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                if (target == null || record.Slot == null)
                {
                    movingTargets.Remove(target);
                    yield break;
                }

                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float eased = moveCurve != null
                    ? Mathf.Clamp01(moveCurve.Evaluate(t))
                    : t;

                Vector3 position = Vector3.Lerp(startPosition, endPosition, eased);
                float rotation = Mathf.LerpAngle(startRotation, endRotation, eased);

                targetTransform.position = position;
                targetTransform.rotation = Quaternion.Euler(0f, 0f, rotation);

                if (rb != null)
                {
                    rb.position = (Vector2)position;
                    rb.rotation = rotation;
                    rb.linearVelocity = Vector2.zero;
                    rb.angularVelocity = 0f;
                }

                yield return null;
            }
        }

        targetTransform.SetParent(record.Slot.transform, true);
        targetTransform.position = record.Slot.WorldPosition;
        targetTransform.rotation = Quaternion.Euler(0f, 0f, record.Slot.WorldRotation);

        if (rb != null)
        {
            rb.position = (Vector2)record.Slot.WorldPosition;
            rb.rotation = record.Slot.WorldRotation;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;

            if (disableSimulationWhileStored)
            {
                rb.simulated = false;
            }
            else
            {
                rb.simulated = true;
                rb.bodyType = RigidbodyType2D.Kinematic;
                rb.gravityScale = 0f;
                rb.constraints = RigidbodyConstraints2D.FreezeAll;
            }
        }

        RestoreColliderEnabledStates(record);

        movingTargets.Remove(target);
        cargoRecords[target] = record;

        Log($"収納完了: {target.name} → {record.Slot.name}");
    }

    /// <summary>
    /// 収納中の1個を解放し、収納前のRigidbody2D状態へ戻します。
    /// エレベーター到着時などに呼べます。
    /// </summary>
    public bool ReleaseCargo(CarryableObject2D target)
    {
        if (target == null || !cargoRecords.TryGetValue(target, out CargoRecord record))
        {
            return false;
        }

        cargoRecords.Remove(target);
        movingTargets.Remove(target);

        if (record.Slot != null)
        {
            record.Slot.Clear(target);
        }

        target.transform.SetParent(record.OriginalParent, true);
        RestoreCargoPhysics(record);
        target.ReleaseExternalReservation(this);

        Log($"収納解除: {target.name}");
        return true;
    }

    /// <summary>
    /// エレベーター到着後などに、全Cargoを再び拾える状態へ戻します。
    /// </summary>
    [ContextMenu("Release All Cargo")]
    public void ReleaseAllCargo()
    {
        List<CarryableObject2D> targets =
            new List<CarryableObject2D>(cargoRecords.Keys);

        foreach (CarryableObject2D target in targets)
        {
            ReleaseCargo(target);
        }
    }

    private CargoRecord CaptureCargoState(
        CarryableObject2D target,
        ElevatorCargoSlot2D slot)
    {
        CargoRecord record = new CargoRecord
        {
            Target = target,
            Slot = slot,
            OriginalParent = target.transform.parent,
            Rigidbody = target.TargetRigidbody
        };

        Rigidbody2D rb = record.Rigidbody;

        if (rb != null)
        {
            record.BodyType = rb.bodyType;
            record.GravityScale = rb.gravityScale;
            record.LinearDamping = rb.linearDamping;
            record.AngularDamping = rb.angularDamping;
            record.Constraints = rb.constraints;
            record.Interpolation = rb.interpolation;
            record.CollisionDetectionMode = rb.collisionDetectionMode;
            record.Simulated = rb.simulated;
        }

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider in colliders)
        {
            if (collider == null)
            {
                continue;
            }

            record.Colliders.Add(new ColliderState
            {
                Collider = collider,
                WasEnabled = collider.enabled
            });
        }

        return record;
    }

    private void PreparePhysicsForMove(CargoRecord record)
    {
        Rigidbody2D rb = record.Rigidbody;

        if (rb != null)
        {
            rb.simulated = true;
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
            rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        if (!disableCollidersWhileMoving)
        {
            return;
        }

        foreach (ColliderState state in record.Colliders)
        {
            if (state.Collider != null)
            {
                state.Collider.enabled = false;
            }
        }
    }

    private void RestoreColliderEnabledStates(CargoRecord record)
    {
        foreach (ColliderState state in record.Colliders)
        {
            if (state.Collider != null)
            {
                state.Collider.enabled = state.WasEnabled;
            }
        }
    }

    private void RestoreCargoPhysics(CargoRecord record)
    {
        RestoreColliderEnabledStates(record);

        Rigidbody2D rb = record.Rigidbody;

        if (rb == null)
        {
            return;
        }

        rb.simulated = record.Simulated;
        rb.bodyType = record.BodyType;
        rb.gravityScale = record.GravityScale;
        rb.linearDamping = record.LinearDamping;
        rb.angularDamping = record.AngularDamping;
        rb.constraints = record.Constraints;
        rb.interpolation = record.Interpolation;
        rb.collisionDetectionMode = record.CollisionDetectionMode;
        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;
    }

    private bool IsTargetInsideReceiver(CarryableObject2D target)
    {
        if (receiverTrigger == null || target == null)
        {
            return false;
        }

        if (receiverTrigger.OverlapPoint(target.transform.position) ||
            receiverTrigger.OverlapPoint(target.InteractionWorldPosition))
        {
            return true;
        }

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider in colliders)
        {
            if (collider == null || !collider.enabled)
            {
                continue;
            }

            if (receiverTrigger.bounds.Intersects(collider.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private bool IsLayerAccepted(CarryableObject2D target)
    {
        if (target == null || acceptedLayers.value == 0)
        {
            return target != null;
        }

        if ((acceptedLayers.value & (1 << target.gameObject.layer)) != 0)
        {
            return true;
        }

        Collider2D[] colliders = target.GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider in colliders)
        {
            if (collider != null &&
                (acceptedLayers.value & (1 << collider.gameObject.layer)) != 0)
            {
                return true;
            }
        }

        return false;
    }

    private ElevatorCargoSlot2D FindFirstFreeSlot()
    {
        RefreshCargoSlots();

        foreach (ElevatorCargoSlot2D slot in cargoSlots)
        {
            if (slot != null && slot.CanUse && !slot.IsOccupied)
            {
                return slot;
            }
        }

        return null;
    }

    private int CountFreeSlots()
    {
        int count = 0;

        if (cargoSlots == null)
        {
            return 0;
        }

        foreach (ElevatorCargoSlot2D slot in cargoSlots)
        {
            if (slot != null && slot.CanUse && !slot.IsOccupied)
            {
                count++;
            }
        }

        return count;
    }

    public void RefreshCargoSlots()
    {
        if (!autoFindCargoSlotsInChildren)
        {
            return;
        }

        ElevatorCargoSlot2D[] found =
            GetComponentsInChildren<ElevatorCargoSlot2D>(true);

        cargoSlots.Clear();
        cargoSlots.AddRange(found);
    }

    private void FindReferences()
    {
        if (receiverTrigger == null)
        {
            receiverTrigger = GetComponent<Collider2D>();
        }

        if (receiverTrigger != null && !receiverTrigger.isTrigger)
        {
            LogWarning("Receiver ColliderはIs TriggerをONにしてください。");
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ElevatorCarryReceiver2D] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[ElevatorCarryReceiver2D] {message}", this);
    }

    private void OnValidate()
    {
        moveDuration = Mathf.Max(0f, moveDuration);
        FindReferences();
        RefreshCargoSlots();
    }
}
