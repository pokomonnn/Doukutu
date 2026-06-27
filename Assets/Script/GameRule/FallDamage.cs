using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(PlayerStatusConditionController))]
public class FallDamage : MonoBehaviour
{
    [Header("落下ダメージ設定")]
    [Tooltip("この距離まではダメージなし")]
    [SerializeField, Min(0f)] private float safeFallDistance = 8f;

    [Tooltip("安全距離を超えた1ユニットごとのダメージ")]
    [SerializeField, Min(0)] private int damagePerUnit = 10;

    [Tooltip("1回で受ける最大ダメージ")]
    [SerializeField, Min(1)] private int maxDamage = 80;

    [Header("骨折設定")]
    [Tooltip("オンなら、一定以上の落下時に骨折します")]
    [SerializeField] private bool canCauseFracture = true;

    [Tooltip("この距離以上を落下すると骨折します")]
    [SerializeField, Min(0f)] private float fractureFallDistance = 13f;

    private Rigidbody2D rb;
    private PlayerMove playerMove;
    private CharacterHealth health;
    private PlayerStatusConditionController statusConditions;

    private bool wasGrounded;
    private bool isTrackingFall;
    private float highestY;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<PlayerMove>();
        health = GetComponent<CharacterHealth>();
        statusConditions =
            GetComponent<PlayerStatusConditionController>();
    }

    private void Start()
    {
        wasGrounded = playerMove.IsGrounded;

        // 空中からゲーム開始した場合にも対応
        if (!wasGrounded)
        {
            isTrackingFall = true;
            highestY = rb.position.y;
        }
    }

    private void FixedUpdate()
    {
        bool isGrounded = playerMove.IsGrounded;

        // 地面から離れた瞬間に落下距離の計測を開始
        if (wasGrounded && !isGrounded)
        {
            isTrackingFall = true;
            highestY = rb.position.y;
        }

        // 空中で一番高かった位置を保存
        if (isTrackingFall && !isGrounded)
        {
            highestY = Mathf.Max(highestY, rb.position.y);
        }

        // 空中 → 接地した瞬間
        if (!wasGrounded && isGrounded && isTrackingFall)
        {
            float fallDistance = highestY - rb.position.y;

            ApplyLandingEffects(fallDistance);

            isTrackingFall = false;
        }

        wasGrounded = isGrounded;
    }

    private void ApplyLandingEffects(float fallDistance)
    {
        // すでに死亡している場合は処理しない
        if (health == null || health.IsDead)
        {
            return;
        }

        // 無敵中は従来どおり落下ダメージを受けない
        if (!health.IsInvincible)
        {
            ApplyFallDamage(fallDistance);
        }

        // 致命傷になった落下では骨折状態を付けない
        if (!health.IsDead)
        {
            TryCauseFracture(fallDistance);
        }
    }

    private void ApplyFallDamage(float fallDistance)
    {
        float dangerousDistance = fallDistance - safeFallDistance;

        if (dangerousDistance <= 0f)
        {
            return;
        }

        int damage = Mathf.CeilToInt(
            dangerousDistance * damagePerUnit
        );

        damage = Mathf.Clamp(damage, 1, maxDamage);

        Debug.Log(
            $"落下距離 {fallDistance:F2}：落下ダメージ {damage}",
            this
        );

        health.TakeDamage(damage);
    }

    private void TryCauseFracture(float fallDistance)
    {
        if (!canCauseFracture ||
            statusConditions == null ||
            fallDistance < fractureFallDistance)
        {
            return;
        }

        bool fractured = statusConditions.AddConditions(
            StatusConditionType.Fracture
        );

        if (fractured)
        {
            Debug.Log(
                $"落下距離 {fallDistance:F2}：骨折しました",
                this
            );
        }
    }

    private void OnValidate()
    {
        safeFallDistance = Mathf.Max(0f, safeFallDistance);
        damagePerUnit = Mathf.Max(0, damagePerUnit);
        maxDamage = Mathf.Max(1, maxDamage);

        // 骨折の閾値が安全距離を下回らないようにする
        fractureFallDistance = Mathf.Max(
            safeFallDistance,
            fractureFallDistance
        );
    }
}
