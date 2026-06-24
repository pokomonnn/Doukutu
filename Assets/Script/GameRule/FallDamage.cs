using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(CharacterHealth))]
public class FallDamage : MonoBehaviour
{
    [Header("落下ダメージ設定")]
    [Tooltip("この距離まではダメージなし")]
    [SerializeField] private float safeFallDistance = 8f;

    [Tooltip("安全距離を超えた1ユニットごとのダメージ")]
    [SerializeField] private int damagePerUnit = 10;

    [Tooltip("1回で受ける最大ダメージ")]
    [SerializeField] private int maxDamage = 80;

    private Rigidbody2D rb;
    private PlayerMove playerMove;
    private CharacterHealth health;

    private bool wasGrounded;
    private bool isTrackingFall;
    private float highestY;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<PlayerMove>();
        health = GetComponent<CharacterHealth>();
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

            ApplyFallDamage(fallDistance);

            isTrackingFall = false;
        }

        wasGrounded = isGrounded;
    }

    private void ApplyFallDamage(float fallDistance)
    {
        float dangerousDistance = fallDistance - safeFallDistance;

        if (dangerousDistance <= 0f)
        {
            
            return;
        }

        int damage = Mathf.CeilToInt(dangerousDistance * damagePerUnit);
        damage = Mathf.Clamp(damage, 1, maxDamage);

        Debug.Log($"落下距離 {fallDistance:F2}：落下ダメージ {damage}");

        health.TakeDamage(damage);
    }
}