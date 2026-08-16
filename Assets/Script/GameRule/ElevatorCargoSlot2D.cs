using UnityEngine;

/// <summary>
/// エレベーター内でCarryableObject2Dを固定する1枠分の位置です。
/// 空いているSlotへ順番に荷物が収納されます。
/// </summary>
[DisallowMultipleComponent]
public class ElevatorCargoSlot2D : MonoBehaviour
{
    [Header("設定")]
    [Tooltip("OFFにすると、このSlotは自動収納先として使われません。")]
    [SerializeField] private bool canUse = true;

    [Tooltip("Sceneビューで収納位置を表示します。")]
    [SerializeField] private bool showGizmo = true;

    public bool CanUse => canUse;
    public bool IsOccupied => occupant != null;
    public CarryableObject2D Occupant => occupant;
    public Vector3 WorldPosition => transform.position;
    public float WorldRotation => transform.eulerAngles.z;

    private CarryableObject2D occupant;

    public bool TryAssign(CarryableObject2D target)
    {
        if (!canUse || target == null || occupant != null)
        {
            return false;
        }

        occupant = target;
        return true;
    }

    public void Clear(CarryableObject2D target = null)
    {
        if (target != null && occupant != target)
        {
            return;
        }

        occupant = null;
    }

    private void OnDrawGizmos()
    {
        if (!showGizmo)
        {
            return;
        }

        Gizmos.DrawWireCube(transform.position, new Vector3(0.45f, 0.65f, 0f));
    }
}
