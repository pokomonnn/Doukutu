using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public class PlayerMove : MonoBehaviour
{
    [Header("移動設定")]
    public float moveSpeed = 5f;
    public float jumpPower = 12f;

    [Header("接地判定")]
    public LayerMask groundLayer;
    public float groundCheckDistance = 0.05f;
    public float groundCheckWidth = 0.85f;
    public float groundCheckHeight = 0.04f;

    private Rigidbody2D rb;
    private Collider2D playerCollider;

    private float moveInput;
    private bool jumpRequest;

    private bool isGrounded;
    public bool IsGrounded => isGrounded;

    // 足音用：現在プレイヤーが踏んでいる地面のCollider
    public Collider2D CurrentGroundCollider { get; private set; }

    private bool canJump = true;

    [Header("向き設定")]
    [SerializeField] private SpriteRenderer spriteRenderer;

    public bool IsFacingRight { get; private set; } = true;

    // ジャンプ直後、地面から離れるまでは再ジャンプを許可しない
    private bool waitingToLeaveGround = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerCollider = GetComponent<Collider2D>();

        if (spriteRenderer == null)
        {
            spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }
    }

    private void Update()
    {
        moveInput = Input.GetAxisRaw("Horizontal");

        UpdateFacing(moveInput);

        if (Input.GetKeyDown(KeyCode.Space))
        {
            jumpRequest = true;
        }
    }

    private void FixedUpdate()
    {
        bool rawGrounded = CheckGrounded();

        // ジャンプ直後、まだ足元判定が残っている間は
        // 「接地していない扱い」にする
        if (waitingToLeaveGround)
        {
            if (!rawGrounded)
            {
                waitingToLeaveGround = false;
            }

            isGrounded = false;

            // ジャンプ中に足音が鳴らないよう、地面情報も空にする
            CurrentGroundCollider = null;
        }
        else
        {
            isGrounded = rawGrounded;

            if (!isGrounded)
            {
                CurrentGroundCollider = null;
            }
        }

        Vector2 velocity = rb.linearVelocity;

        // 横移動
        velocity.x = moveInput * moveSpeed;

        // 着地したらジャンプ可能に戻す
        if (isGrounded && velocity.y <= 0.01f)
        {
            canJump = true;
        }

        // 地面にいて、ジャンプ可能な時だけジャンプ
        if (jumpRequest && isGrounded && canJump)
        {
            velocity.y = jumpPower;
            canJump = false;
            waitingToLeaveGround = true;

            // ジャンプ開始直後に足音が鳴らないようにする
            CurrentGroundCollider = null;
        }

        rb.linearVelocity = velocity;

        jumpRequest = false;
    }

    private void UpdateFacing(float horizontalInput)
    {
        if (horizontalInput > 0)
        {
            IsFacingRight = true;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = false;
            }
        }
        else if (horizontalInput < 0)
        {
            IsFacingRight = false;

            if (spriteRenderer != null)
            {
                spriteRenderer.flipX = true;
            }
        }
    }

    private bool CheckGrounded()
    {
        // 毎回いったん空にして、
        // 接地が見つかった場合だけColliderを保存する
        CurrentGroundCollider = null;

        Bounds bounds = playerCollider.bounds;

        Vector2 boxCenter = new Vector2(
            bounds.center.x,
            bounds.min.y + groundCheckHeight / 2f
        );

        Vector2 boxSize = new Vector2(
            bounds.size.x * groundCheckWidth,
            groundCheckHeight
        );

        RaycastHit2D[] hits = Physics2D.BoxCastAll(
            boxCenter,
            boxSize,
            0f,
            Vector2.down,
            groundCheckDistance,
            groundLayer
        );

        foreach (RaycastHit2D hit in hits)
        {
            if (hit.collider == null)
            {
                continue;
            }

            // 自分自身のColliderに当たっていたら無視
            if (hit.collider == playerCollider)
            {
                continue;
            }

            // 下から支えている面だけを地面扱いにする
            if (hit.normal.y > 0.5f)
            {
                CurrentGroundCollider = hit.collider;
                return true;
            }

            // Tilemapなどでnormalがうまく取れない場合用
            if (hit.collider.gameObject.layer != gameObject.layer)
            {
                CurrentGroundCollider = hit.collider;
                return true;
            }
        }

        return false;
    }

    private void OnDrawGizmosSelected()
    {
        Collider2D col = GetComponent<Collider2D>();

        if (col == null)
        {
            return;
        }

        Bounds bounds = col.bounds;

        Vector2 boxCenter = new Vector2(
            bounds.center.x,
            bounds.min.y + groundCheckHeight / 2f
        );

        Vector2 boxSize = new Vector2(
            bounds.size.x * groundCheckWidth,
            groundCheckHeight
        );

        Gizmos.color = Color.green;

        Gizmos.DrawWireCube(
            boxCenter + Vector2.down * groundCheckDistance,
            boxSize
        );
    }
}