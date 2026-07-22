using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// ESCキーでポーズメニューを開閉します。
/// ポーズ中はTime.timeScaleを0にしてゲーム進行を停止し、
/// PlayerMove・武器操作・任意の入力Behaviourも一時停止します。
///
/// このコンポーネントは、非表示にするPauseMenuPanel自身ではなく、
/// Canvasまたは常に有効な管理Objectへ追加してください。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-1000)]
public class PauseMenuController : MonoBehaviour
{
    public static PauseMenuController Instance { get; private set; }
    public static bool IsPaused { get; private set; }

    [Header("表示パネル")]
    [Tooltip("ESCで表示・非表示にするポーズメニュー本体です。")]
    [SerializeField] private GameObject pauseMenuPanel;

    [Tooltip("音量・画面設定などを置く子パネルです。未使用なら空で構いません。")]
    [SerializeField] private GameObject settingsPanel;

    [Header("操作キー")]
    [SerializeField] private KeyCode pauseKey = KeyCode.Escape;
    [SerializeField] private bool allowPause = true;

    [Header("時間停止")]
    [SerializeField] private bool pauseTimeScale = true;

    [Tooltip("ポーズ中にゲーム音も停止します。UI音を鳴らすAudioSourceはIgnore Listener PauseをONにしてください。")]
    [SerializeField] private bool pauseAudioListener;

    [Header("カーソル")]
    [SerializeField] private bool showCursorWhilePaused = true;

    [Header("プレイヤー操作停止")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private PlayerEquipmentVisualController equipmentVisualController;

    [Tooltip("ポーズ中に無効化する入力スクリプトを設定します。例：GunShooter、WeaponAim、StoneThrowerなど。")]
    [SerializeField] private Behaviour[] behavioursToDisableWhilePaused;

    [Header("ESC競合対策")]
    [Tooltip("ミッションメニューが開いている時は、最初のESCでミッションメニューだけを閉じます。")]
    [SerializeField] private MissionMenuUI missionMenuUI;

    [Tooltip("インベントリが開いている時は、最初のESCでインベントリだけを閉じます。")]
    [SerializeField] private InventoryPanelToggle inventoryPanelToggle;

    [Tooltip("インベントリが開いているか判定するPanelです。")]
    [SerializeField] private GameObject inventoryPanel;

    [Tooltip("セーブスロット画面が開いている時は、最初のESCでセーブ画面だけを閉じます。")]
    [SerializeField] private SaveSlotMenuController saveSlotMenuController;

    [Tooltip("会話・アイテム箱など、開いている間はポーズを新しく開かせたくないPanelを設定します。")]
    [SerializeField] private GameObject[] panelsThatBlockPause;

    [Header("ボタン（任意）")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button settingsBackButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button returnToTitleButton;
    [SerializeField] private Button quitButton;

    [Header("セーブ画面")]
    [Tooltip("ポーズメニューのセーブボタンから開く20スロット画面です。")]
    [SerializeField] private bool openSaveMenuFromSaveButton = true;

    [Header("タイトルへ戻る")]
    [SerializeField] private string titleSceneName = "TitleScene";

    [Tooltip("タイトルへ戻る直前にオートセーブします。将来使用する場合のみONにしてください。")]
    [SerializeField] private bool autoSaveBeforeReturningToTitle;

    [Header("開始設定")]
    [SerializeField] private bool startClosed = true;
    [SerializeField] private bool wireButtonsAutomatically = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private readonly List<Behaviour> disabledBehaviours =
        new List<Behaviour>();

    private float timeScaleBeforePause = 1f;
    private bool audioListenerWasPaused;

    private bool hasSavedCursorState;
    private bool cursorWasVisible;
    private CursorLockMode cursorLockModeBeforePause;

    private bool playerMoveWasEnabled;
    private bool hasDisabledPlayerMove;
    private bool hasLockedWeaponControls;
    private bool ownsPauseState;

    public bool IsPauseMenuOpen => ownsPauseState && IsPaused;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[PauseMenuController] 複数存在するため、後から生成された方を無効にします。",
                this
            );
            enabled = false;
            return;
        }

        Instance = this;
        FindReferences();

        if (wireButtonsAutomatically)
        {
            AddButtonListeners();
        }

        if (startClosed)
        {
            SetActive(settingsPanel, false);
            SetActive(pauseMenuPanel, false);
        }
    }

    private void Start()
    {
        // 前Sceneがポーズ中のまま読み込まれた場合の保険です。
        if (!IsPaused && pauseTimeScale && Mathf.Approximately(Time.timeScale, 0f))
        {
            Time.timeScale = 1f;
        }
    }

    private void Update()
    {
        if (!allowPause || !Input.GetKeyDown(pauseKey))
        {
            return;
        }

        HandleEscape();
    }

    private void OnDisable()
    {
        if (ownsPauseState)
        {
            ResumeGame();
        }
    }

    private void OnDestroy()
    {
        if (wireButtonsAutomatically)
        {
            RemoveButtonListeners();
        }

        if (Instance == this)
        {
            Instance = null;
        }

        if (ownsPauseState)
        {
            ResumeGame();
        }
    }

    /// <summary>
    /// ESCの優先順位を処理します。
    /// 1. セーブ画面を閉じる
    /// 2. 設定画面を閉じる
    /// 3. ポーズを解除
    /// 4. ミッション／インベントリを閉じる
    /// 5. 新しくポーズを開く
    /// </summary>
    public void HandleEscape()
    {
        if (saveSlotMenuController != null &&
            saveSlotMenuController.gameObject.activeInHierarchy)
        {
            saveSlotMenuController.CloseMenu();
            Log("セーブスロット画面を閉じました。");
            return;
        }

        if (settingsPanel != null && settingsPanel.activeInHierarchy)
        {
            CloseSettings();
            return;
        }

        if (IsPauseMenuOpen)
        {
            ResumeGame();
            return;
        }

        if (missionMenuUI != null && missionMenuUI.IsOpen)
        {
            missionMenuUI.CloseMenu();
            Log("ミッションメニューを閉じました。");
            return;
        }

        if (inventoryPanel != null && inventoryPanel.activeInHierarchy)
        {
            if (inventoryPanelToggle != null)
            {
                inventoryPanelToggle.CloseInventory();
            }
            else
            {
                inventoryPanel.SetActive(false);
            }

            Log("インベントリを閉じました。");
            return;
        }

        if (HasBlockingPanelOpen())
        {
            Log("別のUIが開いているため、ポーズメニューを開きませんでした。");
            return;
        }

        PauseGame();
    }

    public void TogglePause()
    {
        if (IsPauseMenuOpen)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (IsPauseMenuOpen || IsPaused)
        {
            return;
        }

        FindReferences();

        ownsPauseState = true;
        IsPaused = true;

        SaveAndShowCursor();
        LockPlayerControls();

        if (pauseAudioListener)
        {
            audioListenerWasPaused = AudioListener.pause;
            AudioListener.pause = true;
        }

        if (pauseTimeScale)
        {
            timeScaleBeforePause = Time.timeScale;
            if (timeScaleBeforePause <= 0f)
            {
                timeScaleBeforePause = 1f;
            }

            Time.timeScale = 0f;
        }

        SetActive(settingsPanel, false);
        SetActive(pauseMenuPanel, true);

        SelectFirstButton(resumeButton);
        Log("ゲームを一時停止しました。");
    }

    public void ResumeGame()
    {
        if (!ownsPauseState)
        {
            SetActive(settingsPanel, false);
            SetActive(pauseMenuPanel, false);
            return;
        }

        SetActive(settingsPanel, false);
        SetActive(pauseMenuPanel, false);

        if (pauseTimeScale)
        {
            Time.timeScale = timeScaleBeforePause > 0f
                ? timeScaleBeforePause
                : 1f;
        }

        if (pauseAudioListener)
        {
            AudioListener.pause = audioListenerWasPaused;
        }

        RestorePlayerControls();
        RestoreCursorState();

        ownsPauseState = false;
        IsPaused = false;
        Log("ゲームを再開しました。");
    }

    public void OpenSettings()
    {
        if (!IsPauseMenuOpen || settingsPanel == null)
        {
            return;
        }

        settingsPanel.SetActive(true);
        SelectFirstSelectableIn(settingsPanel);
        Log("設定パネルを開きました。");
    }

    public void CloseSettings()
    {
        SetActive(settingsPanel, false);
        SelectFirstButton(settingsButton != null ? settingsButton : resumeButton);
        Log("設定パネルを閉じました。");
    }

    public void OpenSaveMenu()
    {
        if (!IsPauseMenuOpen)
        {
            return;
        }

        if (!openSaveMenuFromSaveButton)
        {
            LogWarning("Open Save Menu From Save ButtonがOFFです。");
            return;
        }

        if (saveSlotMenuController == null)
        {
            LogWarning("SaveSlotMenuControllerが設定されていません。");
            return;
        }

        saveSlotMenuController.OpenSaveMenu();
        Log("手動セーブ画面を開きました。");
    }

    public void ReturnToTitle()
    {
        if (string.IsNullOrWhiteSpace(titleSceneName))
        {
            LogWarning("Title Scene Nameが空です。");
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(titleSceneName))
        {
            LogWarning(
                $"タイトルSceneを読み込めません：{titleSceneName}。Build ProfilesのScene Listを確認してください。"
            );
            return;
        }

        if (autoSaveBeforeReturningToTitle && SaveManager.Instance != null)
        {
            SaveManager.Instance.SaveAutoGame();
        }

        // Sceneを切り替える前に必ず時間停止を解除します。
        ResumeGame();
        SceneManager.LoadScene(titleSceneName);
    }

    public void QuitGame()
    {
        ResumeGame();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void SetAllowPause(bool value)
    {
        allowPause = value;

        if (!allowPause && IsPauseMenuOpen)
        {
            ResumeGame();
        }
    }

    private void LockPlayerControls()
    {
        if (playerMove != null && !hasDisabledPlayerMove)
        {
            playerMoveWasEnabled = playerMove.enabled;
            hasDisabledPlayerMove = true;
            playerMove.enabled = false;
        }

        if (equipmentVisualController != null && !hasLockedWeaponControls)
        {
            equipmentVisualController.SetWeaponControlLock(this, true);
            hasLockedWeaponControls = true;
        }

        disabledBehaviours.Clear();

        if (behavioursToDisableWhilePaused == null)
        {
            return;
        }

        foreach (Behaviour behaviour in behavioursToDisableWhilePaused)
        {
            if (behaviour == null ||
                behaviour == this ||
                behaviour == playerMove ||
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
        if (hasDisabledPlayerMove)
        {
            if (playerMove != null && playerMoveWasEnabled)
            {
                playerMove.enabled = true;
            }

            playerMoveWasEnabled = false;
            hasDisabledPlayerMove = false;
        }

        if (hasLockedWeaponControls)
        {
            equipmentVisualController?.SetWeaponControlLock(this, false);
            hasLockedWeaponControls = false;
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
        if (!hasSavedCursorState)
        {
            hasSavedCursorState = true;
            cursorWasVisible = Cursor.visible;
            cursorLockModeBeforePause = Cursor.lockState;
        }

        if (showCursorWhilePaused)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void RestoreCursorState()
    {
        if (!hasSavedCursorState)
        {
            return;
        }

        Cursor.visible = cursorWasVisible;
        Cursor.lockState = cursorLockModeBeforePause;
        hasSavedCursorState = false;
    }

    private bool HasBlockingPanelOpen()
    {
        if (panelsThatBlockPause == null)
        {
            return false;
        }

        foreach (GameObject panel in panelsThatBlockPause)
        {
            if (panel != null && panel.activeInHierarchy)
            {
                return true;
            }
        }

        return false;
    }

    private void FindReferences()
    {
        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>();
        }

        if (equipmentVisualController == null)
        {
            equipmentVisualController =
                FindAnyObjectByType<PlayerEquipmentVisualController>();
        }

        if (missionMenuUI == null)
        {
            missionMenuUI = FindAnyObjectByType<MissionMenuUI>(
                FindObjectsInactive.Include
            );
        }

        if (inventoryPanelToggle == null)
        {
            inventoryPanelToggle = FindAnyObjectByType<InventoryPanelToggle>(
                FindObjectsInactive.Include
            );
        }

        if (saveSlotMenuController == null)
        {
            saveSlotMenuController =
                FindAnyObjectByType<SaveSlotMenuController>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void AddButtonListeners()
    {
        resumeButton?.onClick.AddListener(ResumeGame);
        settingsButton?.onClick.AddListener(OpenSettings);
        settingsBackButton?.onClick.AddListener(CloseSettings);
        saveButton?.onClick.AddListener(OpenSaveMenu);
        returnToTitleButton?.onClick.AddListener(ReturnToTitle);
        quitButton?.onClick.AddListener(QuitGame);
    }

    private void RemoveButtonListeners()
    {
        resumeButton?.onClick.RemoveListener(ResumeGame);
        settingsButton?.onClick.RemoveListener(OpenSettings);
        settingsBackButton?.onClick.RemoveListener(CloseSettings);
        saveButton?.onClick.RemoveListener(OpenSaveMenu);
        returnToTitleButton?.onClick.RemoveListener(ReturnToTitle);
        quitButton?.onClick.RemoveListener(QuitGame);
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null && target.activeSelf != value)
        {
            target.SetActive(value);
        }
    }

    private static void SelectFirstButton(Button button)
    {
        if (button == null || EventSystem.current == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(button.gameObject);
    }

    private static void SelectFirstSelectableIn(GameObject root)
    {
        if (root == null || EventSystem.current == null)
        {
            return;
        }

        Selectable selectable = root.GetComponentInChildren<Selectable>(true);
        if (selectable != null)
        {
            EventSystem.current.SetSelectedGameObject(selectable.gameObject);
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[PauseMenuController] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[PauseMenuController] {message}", this);
    }
}
