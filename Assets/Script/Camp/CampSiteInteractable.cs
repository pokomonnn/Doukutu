using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// キャンプ地の接近判定と、Eキーによるキャンプ開始を担当します。
/// Trigger Collider 付きの CampSite に付けて使います。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CampSiteInteractable : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("シーン内のCampModeControllerを設定します")]
    [SerializeField] private CampModeController campModeController;

    [Tooltip("休憩中のプレイヤーを置く位置。未設定なら子の CampRestPoint を自動で探します")]
    [SerializeField] private Transform campRestPoint;

    [Header("操作")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private string playerTag = "Player";

    [Header("接近時アイコン")]
    [Tooltip("プレイヤーの上に表示するアイコン用GameObject。SpriteRendererでもWorld Space Canvasでも使えます")]
    [SerializeField] private GameObject nearbyIcon;

    [Tooltip("プレイヤーから見たアイコンの位置")]
    [SerializeField] private Vector3 iconWorldOffset = new Vector3(0f, 1.35f, 0f);

    [Tooltip("接近中のアイコンを上下にゆっくり動かします")]
    [SerializeField] private bool animateIcon = true;

    [SerializeField, Min(0f)] private float iconFloatHeight = 0.08f;
    [SerializeField, Min(0.01f)] private float iconFloatSpeed = 2f;

    [Header("接近時テキスト")]
    [Tooltip("ワールド空間のTextMeshProを設定します。不要なら空欄でOKです")]
    [SerializeField] private TMP_Text interactionPromptText;

    [SerializeField] private string interactionPrompt = "E: キャンプ";

    [Tooltip("プレイヤーから見たテキストの位置")]
    [SerializeField] private Vector3 promptWorldOffset = new Vector3(0f, 1.85f, 0f);

    private readonly HashSet<Collider2D> playerColliders =
        new HashSet<Collider2D>();

    private Transform currentPlayerTransform;

    public Transform CampRestPoint => campRestPoint;
    public bool IsPlayerInRange => playerColliders.Count > 0;

    private void Awake()
    {
        FindCampModeController();
        FindCampRestPoint();

        SetInteractionVisualsVisible(false);
    }

    private void OnEnable()
    {
        RefreshInteractionVisuals();
    }

    private void OnDisable()
    {
        SetInteractionVisualsVisible(false);
    }

    private void Reset()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void Update()
    {
        RefreshInteractionVisuals();
        UpdateInteractionVisualPositions();

        if (!IsPlayerInRange ||
            !FindCampModeController() ||
            campModeController.IsBusy ||
            campModeController.IsCamping)
        {
            return;
        }

        if (Input.GetKeyDown(interactKey))
        {
            campModeController.EnterCamp(this);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayerTransform(other, out Transform playerTransform))
        {
            return;
        }

        playerColliders.Add(other);
        currentPlayerTransform = playerTransform;

        RefreshInteractionVisuals();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        playerColliders.Remove(other);

        if (playerColliders.Count == 0)
        {
            currentPlayerTransform = null;
        }

        RefreshInteractionVisuals();
    }

    private bool TryGetPlayerTransform(
        Collider2D other,
        out Transform playerTransform)
    {
        playerTransform = null;

        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag) ||
            other.transform.root.CompareTag(playerTag))
        {
            playerTransform = other.transform.root;
            return true;
        }

        PlayerMove playerMove =
            other.GetComponentInParent<PlayerMove>();

        if (playerMove == null)
        {
            return false;
        }

        playerTransform = playerMove.transform;
        return true;
    }

    private bool FindCampModeController()
    {
        if (campModeController != null)
        {
            return true;
        }

        campModeController =
            FindAnyObjectByType<CampModeController>();

        return campModeController != null;
    }

    private void FindCampRestPoint()
    {
        if (campRestPoint != null)
        {
            return;
        }

        Transform child = transform.Find("CampRestPoint");

        if (child != null)
        {
            campRestPoint = child;
        }
    }

    private void RefreshInteractionVisuals()
    {
        bool shouldShow =
            IsPlayerInRange &&
            (campModeController == null ||
             (!campModeController.IsBusy &&
              !campModeController.IsCamping));

        SetInteractionVisualsVisible(shouldShow);
    }

    private void SetInteractionVisualsVisible(bool visible)
    {
        if (nearbyIcon != null &&
            nearbyIcon.activeSelf != visible)
        {
            nearbyIcon.SetActive(visible);
        }

        if (interactionPromptText != null)
        {
            interactionPromptText.enabled = visible;

            if (visible)
            {
                interactionPromptText.text = interactionPrompt;
            }
        }
    }

    private void UpdateInteractionVisualPositions()
    {
        if (!IsPlayerInRange || currentPlayerTransform == null)
        {
            return;
        }

        float floatOffset = animateIcon
            ? Mathf.Sin(Time.time * iconFloatSpeed) * iconFloatHeight
            : 0f;

        if (nearbyIcon != null)
        {
            nearbyIcon.transform.position =
                currentPlayerTransform.position +
                iconWorldOffset +
                Vector3.up * floatOffset;
        }

        if (interactionPromptText != null)
        {
            interactionPromptText.transform.position =
                currentPlayerTransform.position +
                promptWorldOffset +
                Vector3.up * floatOffset;
        }
    }

    private void OnValidate()
    {
        iconFloatHeight = Mathf.Max(0f, iconFloatHeight);
        iconFloatSpeed = Mathf.Max(0.01f, iconFloatSpeed);
    }
}
