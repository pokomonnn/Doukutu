using UnityEngine;

/// <summary>
/// Screen Space Canvas上の会話Panelを、ワールド内NPCのDialogueAnchorへ追従させます。
/// TownConversationControllerの会話処理自体は変更せず、表示位置だけを担当します。
/// </summary>
[DisallowMultipleComponent]
public class CaveDialogueAnchorFollower : MonoBehaviour
{
    [Header("追従させるUI")]
    [Tooltip("NPC頭上へ移動させるDialogue PanelのRectTransformです。")]
    [SerializeField] private RectTransform dialoguePanelRect;

    [Tooltip("Dialogue Panelが入っているCanvasです。未設定なら親から自動取得します。")]
    [SerializeField] private Canvas targetCanvas;

    [Header("カメラ")]
    [Tooltip("NPCのワールド座標を画面座標へ変換するCameraです。未設定ならCamera.mainを使います。")]
    [SerializeField] private Camera worldCamera;

    [Header("追従設定")]
    [Tooltip("NPC頭上のDialogueAnchorです。通常はCaveConversationNPCから自動設定されます。")]
    [SerializeField] private Transform targetAnchor;

    [Tooltip("アンカー位置から画面上で追加するずらし量です。必要ならYを少し上げてください。")]
    [SerializeField] private Vector2 screenOffset = Vector2.zero;

    [Tooltip("会話Panelが非表示中も位置を更新するか。通常はOFFでOKです。")]
    [SerializeField] private bool followWhilePanelHidden = false;

    [Tooltip("カメラより後ろにアンカーがある時は位置更新を止めます。")]
    [SerializeField] private bool ignoreAnchorBehindCamera = true;

    [Header("任意：会話状態")]
    [Tooltip("設定すると、会話が開いている間だけ追従します。未設定でも動作します。")]
    [SerializeField] private TownConversationController conversationController;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public Transform TargetAnchor => targetAnchor;
    public RectTransform DialoguePanelRect => dialoguePanelRect;

    private RectTransform parentRect;
    private Vector2 runtimeAnimationOffset;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        UpdatePositionNow();
    }

    private void LateUpdate()
    {
        if (targetAnchor == null || dialoguePanelRect == null)
        {
            return;
        }

        if (!followWhilePanelHidden &&
            !dialoguePanelRect.gameObject.activeInHierarchy)
        {
            return;
        }

        if (conversationController != null &&
            !conversationController.IsOpen)
        {
            return;
        }

        UpdatePositionNow();
    }

    /// <summary>
    /// 会話を表示するNPCの頭上アンカーを設定します。
    /// CaveConversationNPCから会話開始時に呼ばれます。
    /// </summary>
    public void SetTarget(Transform anchor)
    {
        targetAnchor = anchor;
        FindReferences();
        UpdatePositionNow();

        if (showDebugLogs)
        {
            Debug.Log(
                $"[CaveDialogueAnchorFollower] 追従先を設定: " +
                $"{(targetAnchor != null ? targetAnchor.name : "null")}",
                this
            );
        }
    }

    public void ClearTarget()
    {
        targetAnchor = null;
        runtimeAnimationOffset = Vector2.zero;
    }

    /// <summary>
    /// 揺れなどの演出用オフセットです。通常位置へ戻す時はVector2.zeroを渡します。
    /// </summary>
    public void SetRuntimeAnimationOffset(Vector2 offset)
    {
        runtimeAnimationOffset = offset;
        UpdatePositionNow();
    }

    public void ClearRuntimeAnimationOffset()
    {
        SetRuntimeAnimationOffset(Vector2.zero);
    }

    [ContextMenu("Update Dialogue Position Now")]
    public void UpdatePositionNow()
    {
        FindReferences();

        if (targetAnchor == null ||
            dialoguePanelRect == null ||
            targetCanvas == null ||
            worldCamera == null)
        {
            return;
        }

        // World Space Canvasの場合は、UIもワールド座標で扱います。
        if (targetCanvas.renderMode == RenderMode.WorldSpace)
        {
            Vector2 totalOffset = screenOffset + runtimeAnimationOffset;

            dialoguePanelRect.position =
                targetAnchor.position +
                new Vector3(totalOffset.x, totalOffset.y, 0f);

            return;
        }

        Vector3 screenPoint3 =
            worldCamera.WorldToScreenPoint(targetAnchor.position);

        if (ignoreAnchorBehindCamera && screenPoint3.z < 0f)
        {
            return;
        }

        Vector2 screenPoint =
            new Vector2(screenPoint3.x, screenPoint3.y) +
            screenOffset +
            runtimeAnimationOffset;

        if (parentRect == null)
        {
            parentRect = dialoguePanelRect.parent as RectTransform;
        }

        if (parentRect == null)
        {
            Debug.LogWarning(
                "[CaveDialogueAnchorFollower] Dialogue Panelの親にRectTransformがありません。",
                this
            );
            return;
        }

        Camera uiCamera = targetCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : targetCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPoint,
                uiCamera,
                out Vector2 localPoint))
        {
            Vector3 currentLocalPosition =
                dialoguePanelRect.localPosition;

            dialoguePanelRect.localPosition = new Vector3(
                localPoint.x,
                localPoint.y,
                currentLocalPosition.z
            );
        }
    }

    private void FindReferences()
    {
        if (dialoguePanelRect == null)
        {
            dialoguePanelRect = GetComponent<RectTransform>();
        }

        if (targetCanvas == null && dialoguePanelRect != null)
        {
            targetCanvas =
                dialoguePanelRect.GetComponentInParent<Canvas>(true);
        }

        if (worldCamera == null)
        {
            worldCamera = Camera.main;
        }

        if (conversationController == null)
        {
            conversationController =
                FindAnyObjectByType<TownConversationController>(
                    FindObjectsInactive.Include
                );
        }

        parentRect = dialoguePanelRect != null
            ? dialoguePanelRect.parent as RectTransform
            : null;
    }

    private void OnValidate()
    {
        FindReferences();
    }
}
