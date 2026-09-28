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

    [Header("壁登り連携")]
    [Tooltip(
        "壁登り中は落下距離を計測しません。" +
        "未設定ならPlayer本体から自動取得します。"
    )]
    [SerializeField] private WallClimbController wallClimbController;

    [Header("デバッグ")]
    [Tooltip("壁登りによる落下計測リセットをConsoleへ表示します")]
    [SerializeField] private bool showWallFallDebugLogs = false;

    [Header("ワープ・リスポーン連携")]
    [Tooltip(
        "チェックポイント復帰やワープ直後、この秒数だけ落下ダメージ・骨折判定を無効にします。" +
        "GameManagerからResetAfterTeleport()が呼ばれた時に使用します。"
    )]
    [SerializeField, Min(0f)]
    private float teleportFallIgnoreDuration = 0.35f;

    [Tooltip("ワープ後の落下計測リセットをConsoleへ表示します")]
    [SerializeField] private bool showTeleportResetDebugLogs = false;

    private Rigidbody2D rb;
    private PlayerMove playerMove;
    private CharacterHealth health;
    private PlayerStatusConditionController statusConditions;

    private bool wasGrounded;
    private bool isTrackingFall;
    private float highestY;
    private bool wasWallClimbing;
    private float ignoreFallUntilTime;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        playerMove = GetComponent<PlayerMove>();
        health = GetComponent<CharacterHealth>();
        statusConditions = GetComponent<PlayerStatusConditionController>();
        FindWallClimbController();
    }

    private void Start()
    {
        wasGrounded = playerMove.IsGrounded;

        bool isWallClimbing = IsWallClimbingNow();
        wasWallClimbing = isWallClimbing;

        if (isWallClimbing)
        {
            ResetFallTrackingToCurrentPosition();
            return;
        }

        if (!wasGrounded)
        {
            StartFallTrackingFromCurrentPosition();
        }
    }

    private void FixedUpdate()
    {
        if (playerMove == null || rb == null)
        {
            return;
        }

        FindWallClimbController();

        bool isGrounded = playerMove.IsGrounded;
        bool isWallClimbing = IsWallClimbingNow();

        // チェックポイント復帰・ワープ直後は、
        // ワープ前のY座標を落下開始地点として扱わないようにする。
        if (Time.time < ignoreFallUntilTime)
        {
            ResetFallTrackingToCurrentPosition();
            wasGrounded = isGrounded;
            wasWallClimbing = isWallClimbing;
            return;
        }

        // 壁を伝っている間は落下ではない。
        // 壁を掴む前に溜まっていた落下距離もここで破棄する。
        if (isWallClimbing)
        {
            if (!wasWallClimbing && showWallFallDebugLogs)
            {
                Debug.Log(
                    "[FallDamage] 壁登り開始：落下距離の計測をリセットします。",
                    this
                );
            }

            ResetFallTrackingToCurrentPosition();
            wasGrounded = isGrounded;
            wasWallClimbing = true;
            return;
        }

        // 壁から離れた地点を、新しい自由落下の開始地点にする。
        if (wasWallClimbing && !isWallClimbing)
        {
            if (!isGrounded)
            {
                StartFallTrackingFromCurrentPosition();

                if (showWallFallDebugLogs)
                {
                    Debug.Log(
                        $"[FallDamage] 壁を離れました：" +
                        $"Y={rb.position.y:F2} から自由落下を再計測します。",
                        this
                    );
                }
            }
            else
            {
                ResetFallTrackingToCurrentPosition();
            }
        }

        // 通常のジャンプ・崖落下。
        if (wasGrounded && !isGrounded)
        {
            StartFallTrackingFromCurrentPosition();
        }

        // 壁離脱など、wasGroundedがfalseのまま空中へ移ったケースの保険。
        if (!isGrounded && !isTrackingFall)
        {
            StartFallTrackingFromCurrentPosition();
        }

        // 上昇があれば最高地点を更新する。
        if (isTrackingFall && !isGrounded)
        {
            highestY = Mathf.Max(highestY, rb.position.y);
        }

        // 空中 → 接地した瞬間。
        if (!wasGrounded && isGrounded && isTrackingFall)
        {
            float fallDistance = Mathf.Max(
                0f,
                highestY - rb.position.y
            );

            ApplyLandingEffects(fallDistance);
            isTrackingFall = false;
        }

        wasGrounded = isGrounded;
        wasWallClimbing = false;
    }

    /// <summary>
    /// チェックポイント復帰・ワープ・強制位置変更の直後に呼びます。
    /// ワープ前に記録していた最高Yを破棄し、一定時間だけ
    /// 落下ダメージ／骨折判定を無効にします。
    /// </summary>
    public void ResetAfterTeleport()
    {
        ResetAfterTeleport(teleportFallIgnoreDuration);
    }

    public void ResetAfterTeleport(float ignoreDuration)
    {
        float safeDuration = Mathf.Max(0f, ignoreDuration);

        ignoreFallUntilTime = Mathf.Max(
            ignoreFallUntilTime,
            Time.time + safeDuration
        );

        ResetFallTrackingToCurrentPosition();

        wasGrounded =
            playerMove != null && playerMove.IsGrounded;

        FindWallClimbController();
        wasWallClimbing = IsWallClimbingNow();

        if (showTeleportResetDebugLogs)
        {
            float currentY = rb != null
                ? rb.position.y
                : transform.position.y;

            Debug.Log(
                $"[FallDamage] ワープ後の落下計測をリセットしました。" +
                $" Y={currentY:F2} / 無効時間={safeDuration:F2}s",
                this
            );
        }
    }

    private void StartFallTrackingFromCurrentPosition()
    {
        isTrackingFall = true;
        highestY = rb != null
            ? rb.position.y
            : transform.position.y;
    }

    private void ResetFallTrackingToCurrentPosition()
    {
        isTrackingFall = false;
        highestY = rb != null
            ? rb.position.y
            : transform.position.y;
    }

    private bool IsWallClimbingNow()
    {
        return wallClimbController != null &&
               wallClimbController.IsWallClimbing;
    }

    private void FindWallClimbController()
    {
        if (wallClimbController != null)
        {
            return;
        }

        wallClimbController = GetComponent<WallClimbController>();

        if (wallClimbController == null)
        {
            wallClimbController = GetComponentInParent<WallClimbController>();
        }
    }

    private void ApplyLandingEffects(float fallDistance)
    {
        if (health == null || health.IsDead)
        {
            return;
        }

        if (!health.IsInvincible)
        {
            ApplyFallDamage(fallDistance);
        }

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

        fractureFallDistance = Mathf.Max(
            safeFallDistance,
            fractureFallDistance
        );

        teleportFallIgnoreDuration = Mathf.Max(
            0f,
            teleportFallIgnoreDuration
        );
    }
}
