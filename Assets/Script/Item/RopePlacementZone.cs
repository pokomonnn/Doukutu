using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 崖上のロープ設置地点を管理します。
/// プレイヤーが範囲内にいて、十分な高低差とロープアイテムがある時だけ
/// アイコンを表示し、Tキーでロープを1個消費して設置します。
/// この段階ではロープの見た目を残すところまでを担当します。
/// 上り下りの操作は次の RopeClimbController で追加します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class RopePlacementZone : MonoBehaviour
{
    [Header("必要なロープ")]
    [Tooltip("設置に使うRopeItemDataを設定します")]
    [SerializeField] private RopeItemData requiredRopeItem;

    [Tooltip("オンなら、設置時にロープアイテムを消費します")]
    [SerializeField] private bool consumeRopeOnPlacement = true;

    [Header("設置地点")]
    [Tooltip("ロープの上端。未設定なら子の RopeAnchorPoint を探します")]
    [SerializeField] private Transform ropeAnchorPoint;

    [Tooltip("ロープの下端。未設定なら子の RopeBottomPoint を探します")]
    [SerializeField] private Transform ropeBottomPoint;

    [Tooltip("この高さ以上の崖だけ、設置候補として表示します。1マス=1unitなら3で3マスです")]
    [SerializeField, Min(0.01f)] private float minimumDropHeight = 3f;

    [Header("設置後に残すロープ")]
    [Tooltip("最初は非表示にしておくロープの見た目。設置成功時に表示されます")]
    [SerializeField] private GameObject ropeVisual;

    [Tooltip("RopeVisualに付けたSpriteRenderer。設定すると上端・下端の間に自動配置します")]
    [SerializeField] private SpriteRenderer ropeSpriteRenderer;

    [Tooltip("オンならSprite RendererをTiled表示にして、高さを自動調整します")]
    [SerializeField] private bool fitTiledSpriteToPoints = true;

    [SerializeField, Min(0.01f)] private float ropeVisualWidth = 0.22f;

    [Tooltip("テスト用。オンならゲーム開始時から設置済みにします")]
    [SerializeField] private bool startPlaced;

    [Header("操作")]
    [SerializeField] private KeyCode placementKey = KeyCode.T;
    [SerializeField] private string playerTag = "Player";

    [Header("プレイヤーのインベントリ")]
    [Tooltip("Playerの持ち物を管理している InventoryController を設定します。空欄でも自動検索しますが、設定しておくと確実です。")]
    [SerializeField] private InventoryController playerInventoryController;

    [Header("接近時アイコン")]
    [Tooltip("プレイヤーの頭上へ表示するアイコン。SpriteRendererでもWorld Space Canvasでも使えます")]
    [SerializeField] private GameObject nearbyIcon;

    [SerializeField] private Vector3 iconWorldOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private bool animateIcon = true;
    [SerializeField, Min(0f)] private float iconFloatHeight = 0.08f;
    [SerializeField, Min(0.01f)] private float iconFloatSpeed = 2f;

    [Header("接近時テキスト")]
    [Tooltip("ワールド空間のTextMeshProを設定します。不要なら空欄でOKです")]
    [SerializeField] private TMP_Text interactionPromptText;

    [SerializeField] private string interactionPrompt = "T: ロープを設置";
    [SerializeField] private Vector3 promptWorldOffset = new Vector3(0f, 1.85f, 0f);

    [Header("設置サウンド")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip placeRopeSound;
    [SerializeField, Range(0f, 1f)] private float placeRopeSoundVolume = 0.9f;

    [Header("デバッグ")]
    [Tooltip("Consoleに、プレイヤー検出・Tキー・設置失敗理由を表示します")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("プレイヤーがこのTriggerに入った/出た時にもConsoleへ表示します")]
    [SerializeField] private bool logTriggerEvents = true;

    [Tooltip("オンの場合、プレイヤーがこのTriggerの外にいる状態でTを押した時もConsoleへ表示します。設置地点が多い場合は通常オフがおすすめです")]
    [SerializeField] private bool logKeyPressOutsideTrigger;

    public bool IsRopePlaced => isRopePlaced;
    public float DropHeight => GetDropHeight();

    public event Action<RopePlacementZone> RopePlaced;

    private readonly HashSet<Collider2D> playerColliders =
        new HashSet<Collider2D>();

    private Transform currentPlayerTransform;
    private InventoryController currentPlayerInventory;
    private bool isRopePlaced;

    private void Awake()
    {
        FindChildReferences();
        SetupTriggerCollider();
        SetupAudioSource();

        isRopePlaced = startPlaced;
        ApplyRopePlacedState();
        SetInteractionVisualsVisible(false);

        LogCurrentSetup("Awake");
    }

    private void OnEnable()
    {
        RefreshInteractionVisuals();
    }

    private void OnDisable()
    {
        SetInteractionVisualsVisible(false);
    }

    private void Update()
    {
        RefreshInteractionVisuals();
        UpdateInteractionVisualPositions();

        if (!Input.GetKeyDown(placementKey))
        {
            return;
        }

        if (IsPlayerInRange())
        {
            TryPlaceRope();
        }
        else if (showDebugLogs && logKeyPressOutsideTrigger)
        {
            Log(
                $"{placementKey}キーを検出しましたが、Playerはこの設置地点のTrigger内にいません。"
            );
        }
    }

    private void Reset()
    {
        SetupTriggerCollider();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayerReferences(
                other,
                out Transform playerTransform,
                out InventoryController inventoryController,
                out string failureReason))
        {
            if (showDebugLogs && logTriggerEvents)
            {
                string objectName = other != null
                    ? other.gameObject.name
                    : "null";

                Log(
                    $"Triggerに {objectName} が入りましたが、Playerとして認識しませんでした。理由：{failureReason}"
                );
            }

            return;
        }

        playerColliders.Add(other);
        currentPlayerTransform = playerTransform;
        currentPlayerInventory = inventoryController;

        if (showDebugLogs && logTriggerEvents)
        {
            Log(
                $"Playerを検出しました。所持ロープ数={GetCurrentRopeAmount()} / 必要数={GetRequiredRopeAmount()}、高低差={GetDropHeight():0.00}、必要高低差={minimumDropHeight:0.00}"
            );
        }

        RefreshInteractionVisuals();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        bool removed = playerColliders.Remove(other);

        if (playerColliders.Count == 0)
        {
            currentPlayerTransform = null;
            currentPlayerInventory = null;
        }

        if (showDebugLogs && logTriggerEvents && removed)
        {
            Log("Playerが設置地点のTriggerから離れました。");
        }

        RefreshInteractionVisuals();
    }

    /// <summary>
    /// ロープを設置します。UIボタンなどからも呼べます。
    /// </summary>
    public bool TryPlaceRope()
    {
        if (!TryGetPlacementFailureReason(out string failureReason))
        {
            Log("ロープ設置を開始します。");

            int consumeAmount = GetRequiredRopeAmount();

            if (consumeRopeOnPlacement)
            {
                int removed = currentPlayerInventory.RemoveAmountByItemData(
                    requiredRopeItem,
                    consumeAmount
                );

                // 念のため、途中で失敗した場合は消費済み分を戻します。
                if (removed != consumeAmount)
                {
                    if (removed > 0)
                    {
                        currentPlayerInventory.TryAddItem(
                            requiredRopeItem,
                            removed
                        );
                    }

                    LogWarning(
                        $"ロープの消費に失敗しました。必要={consumeAmount} / 実際に削除={removed}。消費済み分は戻しました。"
                    );

                    return false;
                }

                Log($"ロープを {consumeAmount} 個消費しました。残り={GetCurrentRopeAmount()}。");
            }

            isRopePlaced = true;
            ApplyRopePlacedState();

            if (audioSource != null && placeRopeSound != null)
            {
                audioSource.PlayOneShot(
                    placeRopeSound,
                    placeRopeSoundVolume
                );
            }

            RopePlaced?.Invoke(this);
            RefreshInteractionVisuals();

            Log("ロープを設置しました。RopeVisualが表示されているか確認してください。");
            return true;
        }

        LogWarning($"ロープを設置できません。理由：{failureReason}");
        return false;
    }

    [ContextMenu("Log Rope Placement Status")]
    private void LogRopePlacementStatus()
    {
        LogCurrentSetup("手動チェック");

        if (TryGetPlacementFailureReason(out string failureReason))
        {
            LogWarning($"現在は設置できません。理由：{failureReason}");
        }
        else
        {
            Log("現在は設置可能です。PlayerがTrigger内にいる状態でTを押してください。");
        }
    }

    [ContextMenu("Place Rope (Debug)")]
    private void PlaceRopeForDebug()
    {
        isRopePlaced = true;
        ApplyRopePlacedState();
        RefreshInteractionVisuals();
        Log("デバッグ用にロープを設置済みにしました。");
    }

    [ContextMenu("Remove Rope (Debug)")]
    private void RemoveRopeForDebug()
    {
        isRopePlaced = false;
        ApplyRopePlacedState();
        RefreshInteractionVisuals();
        Log("デバッグ用にロープ設置状態を解除しました。");
    }

    [ContextMenu("Fit Rope Visual To Points")]
    public void FitRopeVisualToPoints()
    {
        if (ropeVisual == null ||
            ropeAnchorPoint == null ||
            ropeBottomPoint == null)
        {
            return;
        }

        Vector3 top = ropeAnchorPoint.position;
        Vector3 bottom = ropeBottomPoint.position;

        Vector3 center = (top + bottom) * 0.5f;
        center.z = ropeVisual.transform.position.z;

        ropeVisual.transform.position = center;
        ropeVisual.transform.rotation = Quaternion.identity;

        if (!fitTiledSpriteToPoints ||
            ropeSpriteRenderer == null)
        {
            return;
        }

        float visualHeight = Mathf.Max(
            0.01f,
            Vector2.Distance(top, bottom)
        );

        ropeSpriteRenderer.drawMode = SpriteDrawMode.Tiled;
        ropeSpriteRenderer.size = new Vector2(
            ropeVisualWidth,
            visualHeight
        );
    }

    private bool CanPlaceRopeNow()
    {
        return !TryGetPlacementFailureReason(out _);
    }

    private bool TryGetPlacementFailureReason(out string failureReason)
    {
        if (isRopePlaced)
        {
            failureReason = "この場所にはすでにロープが設置されています。";
            return true;
        }

        if (!IsPlayerInRange())
        {
            failureReason = "Playerがこの設置地点のTrigger内にいません。BoxCollider2DのIs Triggerと範囲を確認してください。";
            return true;
        }

        if (requiredRopeItem == null)
        {
            failureReason = "Required Rope Item が未設定です。Rope_BasicなどのRopeItemDataを設定してください。";
            return true;
        }

        if (currentPlayerInventory == null)
        {
            failureReason = "PlayerのInventoryControllerが見つかりません。Playerに付いているInventoryControllerを確認してください。";
            return true;
        }

        if (ropeAnchorPoint == null)
        {
            failureReason = "Rope Anchor Point が未設定です。子にRopeAnchorPointを作るか、Inspectorで設定してください。";
            return true;
        }

        if (ropeBottomPoint == null)
        {
            failureReason = "Rope Bottom Point が未設定です。子にRopeBottomPointを作るか、Inspectorで設定してください。";
            return true;
        }

        if (!HasValidDropHeight())
        {
            failureReason =
                $"高低差が不足しています。現在={GetDropHeight():0.00} / 必要={minimumDropHeight:0.00}。RopeBottomPointをもっと下へ移動するか、Minimum Drop Heightを下げてください。";
            return true;
        }

        if (!consumeRopeOnPlacement)
        {
            failureReason = string.Empty;
            return false;
        }

        int currentAmount = GetCurrentRopeAmount();
        int requiredAmount = GetRequiredRopeAmount();

        if (currentAmount < requiredAmount)
        {
            string displayName = !string.IsNullOrWhiteSpace(requiredRopeItem.DisplayName)
                ? requiredRopeItem.DisplayName
                : requiredRopeItem.name;

            failureReason =
                $"ロープが足りません。{displayName} 所持={currentAmount} / 必要={requiredAmount}。PlayerのInventoryControllerのStarting ItemsへRope_Basicを追加してください。";
            return true;
        }

        failureReason = string.Empty;
        return false;
    }

    private bool HasValidDropHeight()
    {
        return GetDropHeight() >= minimumDropHeight;
    }

    private float GetDropHeight()
    {
        if (ropeAnchorPoint == null || ropeBottomPoint == null)
        {
            return 0f;
        }

        // RopeBottomPoint が上端より下にある場合だけ有効です。
        return Mathf.Max(
            0f,
            ropeAnchorPoint.position.y - ropeBottomPoint.position.y
        );
    }

    private bool IsPlayerInRange()
    {
        return playerColliders.Count > 0 &&
            currentPlayerTransform != null;
    }

    private int GetCurrentRopeAmount()
    {
        if (currentPlayerInventory == null || requiredRopeItem == null)
        {
            return 0;
        }

        return currentPlayerInventory.GetTotalAmount(requiredRopeItem);
    }

    private int GetRequiredRopeAmount()
    {
        return requiredRopeItem != null
            ? requiredRopeItem.ConsumeAmountPerPlacement
            : 0;
    }

    private void ApplyRopePlacedState()
    {
        FitRopeVisualToPoints();

        if (ropeVisual != null &&
            ropeVisual.activeSelf != isRopePlaced)
        {
            ropeVisual.SetActive(isRopePlaced);
        }
    }

    private void RefreshInteractionVisuals()
    {
        SetInteractionVisualsVisible(CanPlaceRopeNow());
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
        if (!CanPlaceRopeNow() || currentPlayerTransform == null)
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

    private bool TryGetPlayerReferences(
        Collider2D other,
        out Transform playerTransform,
        out InventoryController inventoryController,
        out string failureReason)
    {
        playerTransform = null;
        inventoryController = null;
        failureReason = string.Empty;

        if (other == null)
        {
            failureReason = "Colliderがnullです。";
            return false;
        }

        bool isPlayer = other.CompareTag(playerTag) ||
            other.transform.root.CompareTag(playerTag) ||
            other.GetComponentInParent<PlayerMove>() != null;

        if (!isPlayer)
        {
            failureReason =
                $"Tagが{playerTag}ではなく、親にもPlayerMoveが見つかりません。";
            return false;
        }

        PlayerMove playerMove = other.GetComponentInParent<PlayerMove>();

        playerTransform = playerMove != null
            ? playerMove.transform
            : other.transform.root;

        // まずInspectorで直接指定したPlayer用InventoryControllerを使う。
        // このプロジェクトではInventoryControllerがPlayer本体ではなく
        // 別の管理オブジェクトに付いている場合があるためです。
        inventoryController = playerInventoryController;

        // 未設定なら、Triggerに入ったColliderの親・Player本体・子を順に探す。
        if (inventoryController == null)
        {
            inventoryController =
                other.GetComponentInParent<InventoryController>();
        }

        if (inventoryController == null && playerTransform != null)
        {
            inventoryController =
                playerTransform.GetComponentInParent<InventoryController>();
        }

        if (inventoryController == null && playerTransform != null)
        {
            inventoryController =
                playerTransform.GetComponentInChildren<InventoryController>(true);
        }

        // 最後にシーン内の唯一のPlayer用InventoryControllerを自動検索する。
        if (inventoryController == null)
        {
            inventoryController =
                FindAnyObjectByType<InventoryController>();
        }

        if (inventoryController == null)
        {
            failureReason =
                $"Playerとして認識しましたが、{playerTransform.name} に紐づくInventoryControllerが見つかりません。" +
                " RopePlacementZoneの『Player Inventory Controller』へ、プレイヤー用InventoryControllerを直接設定してください。";
            return false;
        }

        return true;
    }

    private void FindChildReferences()
    {
        if (ropeAnchorPoint == null)
        {
            ropeAnchorPoint = transform.Find("RopeAnchorPoint");
        }

        if (ropeBottomPoint == null)
        {
            ropeBottomPoint = transform.Find("RopeBottomPoint");
        }

        if (ropeVisual == null)
        {
            Transform visual = transform.Find("RopeVisual");

            if (visual != null)
            {
                ropeVisual = visual.gameObject;
            }
        }

        if (ropeSpriteRenderer == null && ropeVisual != null)
        {
            ropeSpriteRenderer = ropeVisual.GetComponentInChildren<SpriteRenderer>(true);
        }

        if (nearbyIcon == null)
        {
            Transform icon = transform.Find("RopeIcon");

            if (icon != null)
            {
                nearbyIcon = icon.gameObject;
            }
        }

        if (interactionPromptText == null)
        {
            Transform prompt = transform.Find("RopePrompt");

            if (prompt != null)
            {
                interactionPromptText = prompt.GetComponent<TMP_Text>();
            }
        }
    }

    private void SetupTriggerCollider()
    {
        Collider2D triggerCollider = GetComponent<Collider2D>();

        if (triggerCollider != null)
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void SetupAudioSource()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 1f;
    }

    private void LogCurrentSetup(string source)
    {
        if (!showDebugLogs)
        {
            return;
        }

        string ropeName = requiredRopeItem != null
            ? requiredRopeItem.name
            : "未設定";

        Log(
            $"[{source}] 初期確認: Required Rope Item={ropeName}, " +
            $"Anchor={(ropeAnchorPoint != null ? ropeAnchorPoint.name : "未設定")}, " +
            $"Bottom={(ropeBottomPoint != null ? ropeBottomPoint.name : "未設定")}, " +
            $"高低差={GetDropHeight():0.00}, 必要高低差={minimumDropHeight:0.00}, " +
            $"RopeVisual={(ropeVisual != null ? ropeVisual.name : "未設定")}。"
        );
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[RopePlacementZone: {name}] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.LogWarning($"[RopePlacementZone: {name}] {message}", this);
    }

    private void OnValidate()
    {
        minimumDropHeight = Mathf.Max(0.01f, minimumDropHeight);
        ropeVisualWidth = Mathf.Max(0.01f, ropeVisualWidth);
        iconFloatHeight = Mathf.Max(0f, iconFloatHeight);
        iconFloatSpeed = Mathf.Max(0.01f, iconFloatSpeed);
        placeRopeSoundVolume = Mathf.Clamp01(placeRopeSoundVolume);

        FindChildReferences();
        FitRopeVisualToPoints();
    }
}
    