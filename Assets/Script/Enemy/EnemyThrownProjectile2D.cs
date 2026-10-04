using UnityEngine;

/// <summary>
/// 槍・矢・投げナイフなど、Rigidbody2Dで飛ぶProjectileの見た目を
/// 現在の飛行方向へ向け続けます。
///
/// Spriteが右向きで描かれている場合はRotation Offset=0。
/// 上向きなら-90または90など、Prefabに合わせて調整してください。
/// </summary>
[DisallowMultipleComponent]
public class EnemyThrownProjectile2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら親子から自動検索します。")]
    [SerializeField] private Rigidbody2D targetRigidbody;

    [Tooltip("回転させるTransform。未設定ならこのGameObject自身を回転します。")]
    [SerializeField] private Transform visualTransform;

    [Header("飛行方向へ回転")]
    [SerializeField] private bool rotateAlongVelocity = true;

    [Tooltip("Spriteの元向き補正。Spriteが右向きなら0、上向き素材なら±90付近を調整します。")]
    [SerializeField] private float rotationOffsetDegrees;

    [Tooltip("これ未満の速度では向きを更新しません。着地直前などの回転暴れ防止用です。")]
    [SerializeField, Min(0f)] private float minimumSpeedToRotate = 0.05f;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
    }

    private void LateUpdate()
    {
        if (!rotateAlongVelocity)
        {
            return;
        }

        FindReferences();

        if (targetRigidbody == null)
        {
            return;
        }

        ApplyRotationImmediately(targetRigidbody.linearVelocity);
    }

    /// <summary>
    /// 発射直後にも正しい向きへ合わせるため、EnemyRangedAttack2Dから呼び出せます。
    /// </summary>
    public void ApplyRotationImmediately(Vector2 velocity)
    {
        if (!rotateAlongVelocity ||
            velocity.sqrMagnitude < minimumSpeedToRotate * minimumSpeedToRotate)
        {
            return;
        }

        Transform target = visualTransform != null
            ? visualTransform
            : transform;

        float angle =
            Mathf.Atan2(velocity.y, velocity.x) *
            Mathf.Rad2Deg +
            rotationOffsetDegrees;

        target.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void FindReferences()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody2D>();
        }

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponentInParent<Rigidbody2D>();
        }

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponentInChildren<Rigidbody2D>();
        }
    }

    private void OnValidate()
    {
        minimumSpeedToRotate = Mathf.Max(0f, minimumSpeedToRotate);
    }
}
