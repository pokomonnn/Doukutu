using UnityEngine;

/// <summary>
/// 設置済みロープの登り判定です。
/// RopeVisual に付けて使用します。
/// RopePlacementZone がロープを表示した時だけ Trigger を有効にします。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D))]
public class RopeClimbZone : MonoBehaviour
{
    private enum ExitSide
    {
        Left,
        Right
    }

    [Header("参照")]
    [Tooltip("未設定なら親の RopePlacementZone を自動取得します")]
    [SerializeField] private RopePlacementZone ropePlacementZone;

    [Tooltip("ロープ上端から降りた時にプレイヤーを置く位置。未設定なら RopeAnchorPoint を基準にします")]
    [SerializeField] private Transform topExitPoint;

    [Tooltip("ロープ下端から降りた時にプレイヤーを置く位置。未設定なら RopeBottomPoint を基準にします")]
    [SerializeField] private Transform bottomExitPoint;

    [Header("登り判定の大きさ")]
    [Tooltip("ロープに触れるために必要な横幅。PlayerのColliderより少し広めがおすすめです")]
    [SerializeField, Min(0.05f)] private float triggerWidth = 0.7f;

    [Tooltip("上端・下端より少しだけ外側まで判定を伸ばす距離です")]
    [SerializeField, Min(0f)] private float triggerEndPadding = 0.12f;

    [Header("出口の補助設定")]
    [Tooltip("Top Exit Point が未設定の時、RopeAnchorPointからこのYだけ上へ出します")]
    [SerializeField] private float fallbackTopExitYOffset = 0.45f;

    [Tooltip("Bottom Exit Point が未設定の時、RopeBottomPointからこのYだけ上へ出します")]
    [SerializeField] private float fallbackBottomExitYOffset = 0.45f;

    [Tooltip("Exit Pointが未設定の時、ロープ中心から横へ逃がす距離です。角に埋まる・再びつかむ問題を防ぎます")]
    [SerializeField, Min(0f)] private float fallbackTopExitHorizontalOffset = 0.55f;

    [Tooltip("Exit Pointが未設定の時、ロープ中心から横へ逃がす距離です。角に埋まる・再びつかむ問題を防ぎます")]
    [SerializeField, Min(0f)] private float fallbackBottomExitHorizontalOffset = 0.55f;

    [SerializeField] private ExitSide fallbackTopExitSide = ExitSide.Right;
    [SerializeField] private ExitSide fallbackBottomExitSide = ExitSide.Right;

    private BoxCollider2D ropeTrigger;

    public bool IsClimbAvailable =>
        ropePlacementZone != null &&
        ropePlacementZone.IsRopePlaced &&
        gameObject.activeInHierarchy &&
        ropeTrigger != null &&
        ropeTrigger.enabled;

    public float RopeX => transform.position.x;

    public float TopClimbY => ropeTrigger != null
        ? ropeTrigger.bounds.max.y - triggerEndPadding
        : transform.position.y;

    public float BottomClimbY => ropeTrigger != null
        ? ropeTrigger.bounds.min.y + triggerEndPadding
        : transform.position.y;

    private void Awake()
    {
        FindReferences();
        SetupTrigger();
        RefreshTrigger();
    }

    private void OnEnable()
    {
        FindReferences();
        SetupTrigger();
        RefreshTrigger();
    }

    private void Update()
    {
        // RopePlacementZone が設置済み・未設置を切り替えた時に、
        // 追加設定なしで登り判定も連動させる。
        RefreshTrigger();
    }

    private void OnDisable()
    {
        if (ropeTrigger != null)
        {
            ropeTrigger.enabled = false;
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsClimbAvailable || other == null)
        {
            return;
        }

        RopeClimbController controller =
            other.GetComponentInParent<RopeClimbController>();

        controller?.EnterRopeRange(this);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        RopeClimbController controller =
            other.GetComponentInParent<RopeClimbController>();

        controller?.ExitRopeRange(this);
    }

    /// <summary>
    /// RopeVisual の大きさをAnchor/Bottom Pointに合わせ、Triggerを更新します。
    /// RopePlacementZone の Fit Rope Visual To Points を使っている構成に対応します。
    /// </summary>
    public void RefreshTrigger()
    {
        FindReferences();

        if (ropeTrigger == null)
        {
            return;
        }

        float ropeHeight = GetRopeHeight();

        ropeTrigger.isTrigger = true;
        ropeTrigger.offset = Vector2.zero;
        ropeTrigger.size = new Vector2(
            triggerWidth,
            Mathf.Max(
                0.05f,
                ropeHeight + triggerEndPadding * 2f
            )
        );

        ropeTrigger.enabled =
            ropePlacementZone != null &&
            ropePlacementZone.IsRopePlaced &&
            gameObject.activeInHierarchy;
    }

    public Vector2 GetTopExitPosition(Vector2 fallbackPosition)
    {
        if (topExitPoint != null)
        {
            return topExitPoint.position;
        }

        Transform anchorPoint = GetAnchorPoint();

        float baseY = anchorPoint != null
            ? anchorPoint.position.y
            : TopClimbY;

        return new Vector2(
            RopeX + GetSideMultiplier(fallbackTopExitSide) *
            fallbackTopExitHorizontalOffset,
            baseY + fallbackTopExitYOffset
        );
    }

    public Vector2 GetBottomExitPosition(Vector2 fallbackPosition)
    {
        if (bottomExitPoint != null)
        {
            return bottomExitPoint.position;
        }

        Transform bottomPoint = GetBottomPoint();

        float baseY = bottomPoint != null
            ? bottomPoint.position.y
            : BottomClimbY;

        return new Vector2(
            RopeX + GetSideMultiplier(fallbackBottomExitSide) *
            fallbackBottomExitHorizontalOffset,
            baseY + fallbackBottomExitYOffset
        );
    }

    private float GetSideMultiplier(ExitSide side)
    {
        return side == ExitSide.Right ? 1f : -1f;
    }

    private float GetRopeHeight()
    {
        Transform anchorPoint = GetAnchorPoint();
        Transform bottomPoint = GetBottomPoint();

        if (anchorPoint != null && bottomPoint != null)
        {
            return Mathf.Abs(
                anchorPoint.position.y -
                bottomPoint.position.y
            );
        }

        SpriteRenderer spriteRenderer =
            GetComponent<SpriteRenderer>();

        return Mathf.Max(
            0.05f,
            spriteRenderer != null
                ? spriteRenderer.bounds.size.y
                : 1f
        );
    }

    private Transform GetAnchorPoint()
    {
        if (ropePlacementZone == null)
        {
            return null;
        }

        return ropePlacementZone.transform.Find(
            "RopeAnchorPoint"
        );
    }

    private Transform GetBottomPoint()
    {
        if (ropePlacementZone == null)
        {
            return null;
        }

        return ropePlacementZone.transform.Find(
            "RopeBottomPoint"
        );
    }

    private void FindReferences()
    {
        if (ropeTrigger == null)
        {
            ropeTrigger = GetComponent<BoxCollider2D>();
        }

        if (ropePlacementZone == null)
        {
            ropePlacementZone =
                GetComponentInParent<RopePlacementZone>();
        }

        if (ropePlacementZone == null)
        {
            return;
        }

        Transform placementRoot = ropePlacementZone.transform;

        if (topExitPoint == null)
        {
            topExitPoint = placementRoot.Find(
                "TopExitPoint"
            );
        }

        if (bottomExitPoint == null)
        {
            bottomExitPoint = placementRoot.Find(
                "BottomExitPoint"
            );
        }
    }

    private void SetupTrigger()
    {
        if (ropeTrigger == null)
        {
            return;
        }

        ropeTrigger.isTrigger = true;
    }

    private void OnValidate()
    {
        triggerWidth = Mathf.Max(0.05f, triggerWidth);
        triggerEndPadding = Mathf.Max(0f, triggerEndPadding);
        fallbackTopExitHorizontalOffset = Mathf.Max(
            0f,
            fallbackTopExitHorizontalOffset
        );
        fallbackBottomExitHorizontalOffset = Mathf.Max(
            0f,
            fallbackBottomExitHorizontalOffset
        );

        FindReferences();
        SetupTrigger();

        if (!Application.isPlaying && ropeTrigger != null)
        {
            // 編集中でも判定範囲を見やすくする。
            // 実行時の有効/無効はRopePlacementZoneが管理します。
            float ropeHeight = GetRopeHeight();
            ropeTrigger.offset = Vector2.zero;
            ropeTrigger.size = new Vector2(
                triggerWidth,
                Mathf.Max(
                    0.05f,
                    ropeHeight + triggerEndPadding * 2f
                )
            );
        }
    }

    private void OnDrawGizmosSelected()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();

        if (collider == null)
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawWireCube(collider.offset, collider.size);
    }
}
