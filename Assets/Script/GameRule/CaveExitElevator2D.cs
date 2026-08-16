using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// エレベーター前で「Tで洞窟を出る」を表示し、
/// Tキーで確認Panelを開いてYES/NOを選ばせるControllerです。
///
/// YES：指定したリザルトSceneへ移動
/// NO ：確認Panelを閉じてゲームへ戻る
///
/// このComponentを、Is TriggerをONにしたCollider2Dと同じObjectへ付けてください。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class CaveExitElevator2D : MonoBehaviour
{
    [Header("プレイヤー判定")]
    [SerializeField] private string playerTag = "Player";

    [Header("エレベーター前の案内")]
    [Tooltip("「Tで洞窟を出る」を表示するRoot。空欄ならPrompt TextのGameObjectを使用します")]
    [SerializeField] private GameObject exitPromptRoot;

    [SerializeField] private TMP_Text exitPromptText;

    [SerializeField] private string exitPromptMessage = "Tで洞窟を出る";

    [Header("確認画面")]
    [Tooltip("「洞窟を出ますか？」とYES/NOをまとめたPanelです")]
    [SerializeField] private GameObject confirmationPanel;

    [SerializeField] private TMP_Text confirmationText;

    [SerializeField] private string confirmationMessage = "洞窟を出ますか？";

    [SerializeField] private Button yesButton;
    [SerializeField] private Button noButton;

    [Header("操作")]
    [SerializeField] private KeyCode openConfirmationKey = KeyCode.T;

    [Tooltip("確認画面をEscapeでも閉じられるようにします")]
    [SerializeField] private bool closeConfirmationWithEscape = true;

    [Header("YESを押した後")]
    [Tooltip("移動先のリザルトScene名です。Build Settings / Build Profilesへ登録してください")]
    [SerializeField] private string resultSceneName = "Result";

    [Tooltip("YES押下時にSceneManager.LoadSceneで移動します")]
    [SerializeField] private bool loadResultSceneOnYes = true;

    [Tooltip(
        "YESを押した時に、Result Sceneへ移動する前に探索結果を集計します。" +
        "未設定なら自動取得します。"
    )]
    [SerializeField]
    private ExpeditionResultCollector2D expeditionResultCollector;

    [Header("確認画面中のプレイヤー操作停止")]
    [SerializeField] private bool lockPlayerMovement = true;
    [SerializeField] private bool lockWeaponControls = true;

    [Tooltip("確認画面中に追加で停止したいBehaviour。StoneThrowerやPlayerRopePullControllerなどを任意で入れられます")]
    [SerializeField] private Behaviour[] behavioursToDisableWhileConfirming;

    [Header("カーソル")]
    [SerializeField] private bool showCursorWhileConfirming = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public bool IsPlayerInside => playerInside;
    public bool IsConfirmationOpen => confirmationOpen;

    private bool playerInside;
    private bool confirmationOpen;
    private bool transitionStarted;

    private PlayerMove currentPlayerMove;
    private PlayerEquipmentVisualController currentEquipmentVisualController;

    private bool playerMoveWasEnabled;
    private bool playerMoveWasChanged;
    private bool weaponLockApplied;

    private readonly List<Behaviour> disabledBehaviours =
        new List<Behaviour>();

    private bool cursorStateSaved;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;

    private void Awake()
    {
        EnsureTrigger();
        SetupUIReferences();
        SetupButtons();

        SetPromptVisible(false);
        SetConfirmationVisible(false);
    }

    private void OnEnable()
    {
        SetupButtons();
        SetPromptVisible(false);
        SetConfirmationVisible(false);
    }

    private void OnDisable()
    {
        RemoveButtonListeners();
        RestorePlayerControls();
        RestoreCursor();

        playerInside = false;
        confirmationOpen = false;
        transitionStarted = false;
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
        RestorePlayerControls();
        RestoreCursor();
    }

    private void Update()
    {
        if (transitionStarted)
        {
            return;
        }

        if (confirmationOpen)
        {
            if (closeConfirmationWithEscape &&
                Input.GetKeyDown(KeyCode.Escape))
            {
                CloseConfirmation();
            }

            return;
        }

        if (!playerInside)
        {
            return;
        }

        if (Input.GetKeyDown(openConfirmationKey))
        {
            OpenConfirmation();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryResolvePlayer(other))
        {
            return;
        }

        playerInside = true;

        if (!confirmationOpen && !transitionStarted)
        {
            SetPromptVisible(true);
        }

        Log("プレイヤーが洞窟出口エリアへ入りました。");
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // 子Colliderだけが先に入った場合などの保険。
        if (!playerInside)
        {
            TryResolvePlayer(other);
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!IsSamePlayer(other))
        {
            return;
        }

        playerInside = false;

        if (!confirmationOpen)
        {
            SetPromptVisible(false);
        }

        Log("プレイヤーが洞窟出口エリアから離れました。");
    }

    public void OpenConfirmation()
    {
        if (!playerInside ||
            confirmationOpen ||
            transitionStarted)
        {
            return;
        }

        confirmationOpen = true;

        SetPromptVisible(false);
        SetConfirmationVisible(true);

        LockPlayerControls();
        SaveAndShowCursor();

        Log("洞窟退出の確認画面を開きました。");
    }

    public void CloseConfirmation()
    {
        if (!confirmationOpen || transitionStarted)
        {
            return;
        }

        confirmationOpen = false;

        SetConfirmationVisible(false);
        RestorePlayerControls();
        RestoreCursor();

        SetPromptVisible(playerInside);

        Log("洞窟退出をキャンセルしました。");
    }

    public void ConfirmExit()
    {
        if (!confirmationOpen || transitionStarted)
        {
            return;
        }

        transitionStarted = true;
        confirmationOpen = false;

        SetPromptVisible(false);
        SetConfirmationVisible(false);

        RestorePlayerControls();
        RestoreCursor();

        if (!loadResultSceneOnYes)
        {
            transitionStarted = false;
            Log("YESが押されました。Load Result Scene On YesがOFFのためScene移動は行いません。");
            return;
        }

        if (string.IsNullOrWhiteSpace(resultSceneName))
        {
            transitionStarted = false;
            Debug.LogWarning(
                "[CaveExitElevator2D] Result Scene Nameが空です。",
                this
            );
            return;
        }

        CollectExpeditionResult();

        Log($"リザルトSceneへ移動します: {resultSceneName}");
        SceneManager.LoadScene(resultSceneName);
    }

    private void CollectExpeditionResult()
    {
        if (expeditionResultCollector == null)
        {
            expeditionResultCollector =
                FindAnyObjectByType<ExpeditionResultCollector2D>(
                    FindObjectsInactive.Include
                );
        }

        if (expeditionResultCollector == null)
        {
            Debug.LogWarning(
                "[CaveExitElevator2D] ExpeditionResultCollector2Dが見つかりません。" +
                "Result Sceneには0件の結果を渡します。",
                this
            );

            ExpeditionResultSession.SetResult(
                new ExpeditionResultData()
            );

            return;
        }

        expeditionResultCollector.CollectAndStoreResult();
    }

    private bool TryResolvePlayer(Collider2D source)
    {
        if (source == null)
        {
            return false;
        }

        Transform sourceTransform = source.transform;

        GameObject taggedPlayer = null;

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            Transform current = sourceTransform;

            while (current != null)
            {
                if (current.CompareTag(playerTag))
                {
                    taggedPlayer = current.gameObject;
                    break;
                }

                current = current.parent;
            }
        }

        PlayerMove playerMove =
            source.GetComponentInParent<PlayerMove>();

        if (taggedPlayer == null && playerMove == null)
        {
            return false;
        }

        GameObject playerObject = taggedPlayer != null
            ? taggedPlayer
            : playerMove.gameObject;

        currentPlayerMove = playerObject.GetComponent<PlayerMove>();

        if (currentPlayerMove == null)
        {
            currentPlayerMove =
                playerObject.GetComponentInChildren<PlayerMove>(true);
        }

        currentEquipmentVisualController =
            playerObject.GetComponent<PlayerEquipmentVisualController>();

        if (currentEquipmentVisualController == null)
        {
            currentEquipmentVisualController =
                playerObject.GetComponentInChildren<
                    PlayerEquipmentVisualController
                >(true);
        }

        return true;
    }

    private bool IsSamePlayer(Collider2D source)
    {
        if (source == null)
        {
            return false;
        }

        if (currentPlayerMove != null)
        {
            Transform playerRoot = currentPlayerMove.transform;

            if (source.transform == playerRoot ||
                source.transform.IsChildOf(playerRoot))
            {
                return true;
            }
        }

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            Transform current = source.transform;

            while (current != null)
            {
                if (current.CompareTag(playerTag))
                {
                    return true;
                }

                current = current.parent;
            }
        }

        return false;
    }

    private void LockPlayerControls()
    {
        if (lockPlayerMovement &&
            currentPlayerMove != null &&
            !playerMoveWasChanged)
        {
            playerMoveWasEnabled = currentPlayerMove.enabled;
            playerMoveWasChanged = true;
            currentPlayerMove.enabled = false;
        }

        if (lockWeaponControls &&
            currentEquipmentVisualController != null &&
            !weaponLockApplied)
        {
            currentEquipmentVisualController.SetWeaponControlLock(
                this,
                true
            );

            weaponLockApplied = true;
        }

        disabledBehaviours.Clear();

        if (behavioursToDisableWhileConfirming == null)
        {
            return;
        }

        foreach (Behaviour behaviour
                 in behavioursToDisableWhileConfirming)
        {
            if (behaviour == null ||
                behaviour == this ||
                !behaviour.enabled)
            {
                continue;
            }

            behaviour.enabled = false;
            disabledBehaviours.Add(behaviour);
        }
    }

    private void RestorePlayerControls()
    {
        if (playerMoveWasChanged)
        {
            if (currentPlayerMove != null &&
                playerMoveWasEnabled)
            {
                currentPlayerMove.enabled = true;
            }

            playerMoveWasEnabled = false;
            playerMoveWasChanged = false;
        }

        if (weaponLockApplied)
        {
            currentEquipmentVisualController?.SetWeaponControlLock(
                this,
                false
            );

            weaponLockApplied = false;
        }

        foreach (Behaviour behaviour in disabledBehaviours)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledBehaviours.Clear();
    }

    private void SaveAndShowCursor()
    {
        if (!showCursorWhileConfirming)
        {
            return;
        }

        if (!cursorStateSaved)
        {
            previousCursorVisible = Cursor.visible;
            previousCursorLockMode = Cursor.lockState;
            cursorStateSaved = true;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreCursor()
    {
        if (!cursorStateSaved)
        {
            return;
        }

        Cursor.visible = previousCursorVisible;
        Cursor.lockState = previousCursorLockMode;
        cursorStateSaved = false;
    }

    private void SetPromptVisible(bool visible)
    {
        if (exitPromptText != null)
        {
            exitPromptText.text = exitPromptMessage ?? string.Empty;
        }

        GameObject root = exitPromptRoot != null
            ? exitPromptRoot
            : exitPromptText != null
                ? exitPromptText.gameObject
                : null;

        if (root != null && root.activeSelf != visible)
        {
            root.SetActive(visible);
        }
    }

    private void SetConfirmationVisible(bool visible)
    {
        if (confirmationText != null)
        {
            confirmationText.text =
                confirmationMessage ?? string.Empty;
        }

        if (confirmationPanel != null &&
            confirmationPanel.activeSelf != visible)
        {
            confirmationPanel.SetActive(visible);
        }
    }

    private void SetupUIReferences()
    {
        if (exitPromptRoot == null && exitPromptText != null)
        {
            exitPromptRoot = exitPromptText.gameObject;
        }
    }

    private void SetupButtons()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(ConfirmExit);
            yesButton.onClick.AddListener(ConfirmExit);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(CloseConfirmation);
            noButton.onClick.AddListener(CloseConfirmation);
        }
    }

    private void RemoveButtonListeners()
    {
        if (yesButton != null)
        {
            yesButton.onClick.RemoveListener(ConfirmExit);
        }

        if (noButton != null)
        {
            noButton.onClick.RemoveListener(CloseConfirmation);
        }
    }

    private void EnsureTrigger()
    {
        Collider2D trigger = GetComponent<Collider2D>();

        if (trigger != null && !trigger.isTrigger)
        {
            Debug.LogWarning(
                "[CaveExitElevator2D] Collider2DのIs TriggerをONにしてください。",
                this
            );
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[CaveExitElevator2D] {message}", this);
        }
    }

    private void OnValidate()
    {
        SetupUIReferences();
    }
}
