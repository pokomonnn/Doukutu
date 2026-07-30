using TMPro;
using UnityEngine;

/// <summary>
/// ロープで引っ張れる物体へ付けます。
/// Rigidbody2DはDynamicで動作し、重力・衝突・落下の影響を受けます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Rigidbody2D))]
public class RopePullTarget : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Rigidbody2D targetRigidbody;

    [Tooltip("ロープを結ぶ位置です。未設定ならこのObjectの中心を使います")]
    [SerializeField] private Transform ropeAttachmentPoint;

    [Tooltip("プレイヤーとの距離判定に使う位置です。未設定ならロープ接続位置を使います")]
    [SerializeField] private Transform interactionPoint;

    [Header("Fボタン表示")]
    [Tooltip("Objectの近くに表示するWorld SpaceのTMP Textです")]
    [SerializeField] private TMP_Text interactionText;

    [SerializeField] private bool applyPromptLocalPosition = true;
    [SerializeField] private Vector3 promptLocalPosition = new Vector3(0f, 1.1f, 0f);

    [Header("物理設定")]
    [Tooltip("オンなら開始時にRigidbody2DをDynamicへ変更します")]
    [SerializeField] private bool forceDynamicBodyOnAwake = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public Rigidbody2D TargetRigidbody => targetRigidbody;

    public Vector2 RopeAttachmentWorldPosition =>
        ropeAttachmentPoint != null
            ? ropeAttachmentPoint.position
            : transform.position;

    public Vector2 InteractionWorldPosition =>
        interactionPoint != null
            ? interactionPoint.position
            : RopeAttachmentWorldPosition;

    public bool IsReserved => currentController != null;
    public PlayerRopePullController CurrentController => currentController;

    private PlayerRopePullController currentController;

    private void Awake()
    {
        FindReferences();
        ApplyPhysicsSettings();
        HidePrompt();
    }

    private void OnEnable()
    {
        FindReferences();
        ApplyPhysicsSettings();
        HidePrompt();
    }

    private void OnDisable()
    {
        HidePrompt();

        if (currentController != null)
        {
            PlayerRopePullController controller = currentController;
            currentController = null;
            controller.NotifyTargetBecameUnavailable(this);
        }
    }

    public bool TryReserve(PlayerRopePullController controller)
    {
        if (controller == null)
        {
            return false;
        }

        if (currentController != null && currentController != controller)
        {
            return false;
        }

        currentController = controller;
        return true;
    }

    public void Release(PlayerRopePullController controller)
    {
        if (currentController == controller)
        {
            currentController = null;
        }
    }

    public void ShowPrompt(string message)
    {
        FindPromptText();

        if (interactionText == null)
        {
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

    private void ApplyPhysicsSettings()
    {
        if (targetRigidbody == null)
        {
            return;
        }

        if (forceDynamicBodyOnAwake)
        {
            targetRigidbody.bodyType = RigidbodyType2D.Dynamic;
            targetRigidbody.simulated = true;
        }

        if (targetRigidbody.bodyType != RigidbodyType2D.Dynamic)
        {
            Debug.LogWarning(
                $"[RopePullTarget] {name} のRigidbody2DをDynamicにしてください。",
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

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[RopePullTarget: {name}] {message}", this);
        }
    }

    private void OnValidate()
    {
        FindReferences();
    }
}
