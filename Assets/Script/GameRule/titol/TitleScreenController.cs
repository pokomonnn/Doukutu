using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// タイトル画面で、続きから・ニューゲーム・終了を管理します。
/// 最大20個の手動セーブとオートセーブを確認し、
/// 続きから・ニューゲーム・終了を管理します。
/// </summary>
[DisallowMultipleComponent]
public class TitleScreenController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定ならSaveManager.Instanceを自動取得します。")]
    [SerializeField] private SaveManager saveManager;

    [Header("画面パネル")]
    [SerializeField] private GameObject pressAnyKeyPanel;
    [SerializeField] private CanvasGroup pressAnyKeyCanvasGroup;
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject newGameConfirmationPanel;
    [SerializeField] private GameObject saveSlotLoadPanel;
    [SerializeField] private SaveSlotMenuController saveSlotLoadMenu;
    [SerializeField] private GameObject saveDataPanel;
    [SerializeField] private GameObject noSaveDataPanel;

    [Header("ボタン")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button newGameButton;
    [SerializeField] private Button quitButton;
    [SerializeField] private Button confirmNewGameButton;
    [SerializeField] private Button cancelNewGameButton;
    [SerializeField] private TMP_Text newGameConfirmationText;

    [Header("セーブ情報Text")]
    [SerializeField] private TMP_Text savedSceneText;
    [SerializeField] private TMP_Text savedDateText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text inventoryCountText;
    [SerializeField] private TMP_Text missionCountText;
    [SerializeField] private TMP_Text saveCountText;
    [SerializeField] private TMP_Text statusText;

    [Header("表示文")]
    [SerializeField] private string savedSceneFormat = "場所：{0}";
    [SerializeField] private string savedDateFormat = "保存日時：{0}";
    [SerializeField] private string moneyFormat = "所持金：{0:N0}";
    [SerializeField] private string inventoryCountFormat = "所持アイテム：{0}";
    [SerializeField] private string missionCountFormat = "ミッション：{0}";
    [SerializeField] private string saveCountFormat = "セーブデータ：{0} / 20";
    [SerializeField] private string noSaveMessage = "セーブデータがありません";
    [SerializeField] private string incompatibleSaveMessage = "このセーブデータは現在のバージョンでは読み込めません";
    [SerializeField] private string newGameConfirmationMessage =
        "ニューゲームを開始しますか？\n既存のセーブデータは残ります。";
    [SerializeField] private string newGameDeleteConfirmationMessage =
        "ニューゲームを開始しますか？\n既定スロットのセーブデータを削除します。";

    [Header("ニューゲーム")]
    [Tooltip("ニューゲームで最初に読み込むScene名です。")]
    [SerializeField] private string newGameSceneName = "Town_Main";

    [Tooltip("通常はオフにしてください。複数スロット制では、ニューゲームを始めても既存セーブを残します。")]
    [SerializeField] private bool deleteExistingSaveOnNewGame = false;

    [Tooltip("続きからの一覧へオートセーブを含めます。")]
    [SerializeField] private bool includeAutoSaveInContinue = true;

    [Tooltip("セーブデータがある場合に確認パネルを表示します。")]
    [SerializeField] private bool confirmWhenSaveExists = true;

    [Header("タイトル開始演出")]
    [SerializeField] private bool showPressAnyKeyFirst = true;
    [SerializeField] private bool acceptMouseClick = true;
    [SerializeField, Min(0f)] private float inputEnableDelay = 0.2f;

    [Tooltip("Press Any Keyを点滅させます。CanvasGroupが必要です。")]
    [SerializeField] private bool blinkPressAnyKey = true;
    [SerializeField, Min(0.01f)] private float blinkSpeed = 2f;
    [SerializeField, Range(0f, 1f)] private float minimumBlinkAlpha = 0.25f;

    [Header("動作")]
    [Tooltip("ButtonのOnClickをInspectorで設定せず、このスクリプトが自動接続します。")]
    [SerializeField] private bool wireButtonsAutomatically = true;
    [SerializeField] private bool showDebugLogs = true;

    private float openedAtUnscaledTime;
    private bool menuOpened;
    private bool operationInProgress;
    private SaveSlotInfo currentSlotInfo;

    private void Awake()
    {
        FindReferences();

        if (wireButtonsAutomatically)
        {
            AddButtonListeners();
        }

        SetInitialPanelState();
    }

    private void Start()
    {
        openedAtUnscaledTime = Time.unscaledTime;
        RefreshSaveDisplay();
    }

    private void OnDestroy()
    {
        if (wireButtonsAutomatically)
        {
            RemoveButtonListeners();
        }
    }

    private void Update()
    {
        UpdatePressAnyKeyBlink();

        if (menuOpened || operationInProgress || !showPressAnyKeyFirst)
        {
            return;
        }

        if (Time.unscaledTime - openedAtUnscaledTime < inputEnableDelay)
        {
            return;
        }

        bool pressed = Input.anyKeyDown;
        if (acceptMouseClick)
        {
            pressed |= Input.GetMouseButtonDown(0) ||
                       Input.GetMouseButtonDown(1) ||
                       Input.GetMouseButtonDown(2);
        }

        if (pressed)
        {
            OpenMainMenu();
        }
    }

    /// <summary>
    /// 「何かキーを押してください」からメニューへ進みます。
    /// </summary>
    public void OpenMainMenu()
    {
        menuOpened = true;

        SetActive(pressAnyKeyPanel, false);
        SetActive(mainMenuPanel, true);
        SetActive(newGameConfirmationPanel, false);
        SetActive(saveSlotLoadPanel, false);

        RefreshSaveDisplay();
        Log("メインメニューを表示しました。");
    }

    /// <summary>
    /// タイトルの最初の画面へ戻します。
    /// </summary>
    public void ReturnToPressAnyKey()
    {
        if (!showPressAnyKeyFirst)
        {
            return;
        }

        menuOpened = false;
        openedAtUnscaledTime = Time.unscaledTime;

        SetActive(pressAnyKeyPanel, true);
        SetActive(mainMenuPanel, false);
        SetActive(newGameConfirmationPanel, false);
        SetActive(saveSlotLoadPanel, false);
    }

    /// <summary>
    /// セーブデータの有無と概要表示を更新します。
    /// </summary>
    public void RefreshSaveDisplay()
    {
        FindReferences();

        if (saveManager == null)
        {
            currentSlotInfo = null;
            SetContinueAvailable(false);
            SetActive(saveDataPanel, false);
            SetActive(noSaveDataPanel, true);
            SetStatus("SaveManagerが見つかりません");
            Debug.LogWarning(
                "[TitleScreenController] SaveManagerが見つかりません。" +
                "Title SceneへSaveManagerを配置してください。",
                this
            );
            return;
        }

        int manualSaveCount = saveManager.CountManualSaveFiles();
        SetText(
            saveCountText,
            string.Format(saveCountFormat, manualSaveCount)
        );

        bool readable = saveManager.TryReadMostRecentSaveInfo(
            includeAutoSaveInContinue,
            out SaveSlotInfo slotInfo,
            out string resultMessage
        );

        currentSlotInfo = slotInfo;
        bool hasAnyFile = saveManager.HasAnySaveData(
            includeAutoSaveInContinue
        );
        bool canContinue =
            readable &&
            slotInfo != null &&
            slotInfo.IsCompatible;

        SetContinueAvailable(canContinue);
        SetActive(saveDataPanel, hasAnyFile);
        SetActive(noSaveDataPanel, !hasAnyFile);

        if (!hasAnyFile)
        {
            ClearSaveTexts();
            SetText(
                saveCountText,
                string.Format(saveCountFormat, manualSaveCount)
            );
            SetStatus(noSaveMessage);
            return;
        }

        if (slotInfo != null)
        {
            ApplySlotInfoToTexts(slotInfo);
        }
        else
        {
            ClearSaveTexts();
        }

        if (!canContinue)
        {
            SetStatus(incompatibleSaveMessage);
        }
        else
        {
            SetStatus(string.Empty);
        }

        Log(resultMessage);
    }

    /// <summary>
    /// セーブデータを読み込み、保存されたSceneから再開します。
    /// </summary>
    public void ContinueGame()
    {
        if (operationInProgress)
        {
            return;
        }

        FindReferences();
        RefreshSaveDisplay();

        if (saveManager == null ||
            !saveManager.HasAnyCompatibleSaveData(includeAutoSaveInContinue))
        {
            SetStatus(noSaveMessage);
            return;
        }

        if (saveSlotLoadMenu != null)
        {
            saveSlotLoadMenu.OpenLoadMenu();
            SetStatus(string.Empty);
            return;
        }

        // 一覧パネルが未設定の場合の安全なフォールバックです。
        operationInProgress = true;
        SetButtonsInteractable(false);
        SetStatus("最新のセーブデータを読み込んでいます…");

        bool success = saveManager.LoadMostRecentSave(
            includeAutoSaveInContinue
        );

        if (!success)
        {
            operationInProgress = false;
            SetButtonsInteractable(true);
            SetStatus(saveManager.LastOperationMessage);
        }
    }

    /// <summary>
    /// ニューゲームを要求します。
    /// セーブがある場合は確認パネルを表示します。
    /// </summary>
    public void RequestNewGame()
    {
        if (operationInProgress)
        {
            return;
        }

        FindReferences();
        RefreshSaveDisplay();

        bool hasSave = saveManager != null &&
                       saveManager.HasAnySaveData(includeAutoSaveInContinue);

        if (confirmWhenSaveExists &&
            hasSave &&
            newGameConfirmationPanel != null)
        {
            SetText(
                newGameConfirmationText,
                deleteExistingSaveOnNewGame
                    ? newGameDeleteConfirmationMessage
                    : newGameConfirmationMessage
            );
            SetActive(newGameConfirmationPanel, true);
            SetButtonsInteractable(false, keepConfirmationButtons: true);
            return;
        }

        ConfirmNewGame();
    }

    /// <summary>
    /// 確認後、既存セッションを初期化してニューゲームを開始します。
    /// </summary>
    public void ConfirmNewGame()
    {
        if (operationInProgress)
        {
            return;
        }

        FindReferences();

        if (saveManager == null)
        {
            SetStatus("SaveManagerが見つかりません");
            return;
        }

        if (string.IsNullOrWhiteSpace(newGameSceneName))
        {
            SetStatus("New Game Scene Nameが設定されていません");
            return;
        }

        operationInProgress = true;
        SetActive(newGameConfirmationPanel, false);
        SetButtonsInteractable(false);
        SetStatus("ニューゲームを開始しています…");

        bool success = saveManager.StartNewGame(
            newGameSceneName,
            deleteExistingSaveOnNewGame
        );

        if (!success)
        {
            operationInProgress = false;
            SetButtonsInteractable(true);
            SetStatus(saveManager.LastOperationMessage);
        }
    }

    public void CancelNewGame()
    {
        if (operationInProgress)
        {
            return;
        }

        SetActive(newGameConfirmationPanel, false);
        SetButtonsInteractable(true);
        RefreshSaveDisplay();
    }

    public void QuitGame()
    {
        if (operationInProgress)
        {
            return;
        }

        Log("ゲームを終了します。");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void SetInitialPanelState()
    {
        operationInProgress = false;
        menuOpened = !showPressAnyKeyFirst;

        SetActive(pressAnyKeyPanel, showPressAnyKeyFirst);
        SetActive(mainMenuPanel, !showPressAnyKeyFirst);
        SetActive(newGameConfirmationPanel, false);
        SetActive(saveSlotLoadPanel, false);

        SetButtonsInteractable(true);
    }

    private void ApplySlotInfoToTexts(SaveSlotInfo info)
    {
        if (info == null)
        {
            ClearSaveTexts();
            return;
        }

        SetText(
            savedSceneText,
            string.Format(
                savedSceneFormat,
                string.IsNullOrWhiteSpace(info.SavedSceneName)
                    ? "不明"
                    : $"{info.SlotLabel} / {info.SavedSceneName}"
            )
        );

        SetText(
            savedDateText,
            string.Format(
                savedDateFormat,
                FormatSavedDate(info.SavedAtUtc)
            )
        );

        SetText(moneyText, string.Format(moneyFormat, info.Money));
        SetText(
            inventoryCountText,
            string.Format(inventoryCountFormat, info.InventoryItemCount)
        );
        SetText(
            missionCountText,
            string.Format(missionCountFormat, info.MissionCount)
        );
    }

    private void ClearSaveTexts()
    {
        SetText(savedSceneText, string.Empty);
        SetText(savedDateText, string.Empty);
        SetText(moneyText, string.Empty);
        SetText(inventoryCountText, string.Empty);
        SetText(missionCountText, string.Empty);
    }

    private static string FormatSavedDate(string utcText)
    {
        if (string.IsNullOrWhiteSpace(utcText))
        {
            return "不明";
        }

        if (DateTime.TryParse(
                utcText,
                null,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out DateTime parsed))
        {
            return parsed.ToLocalTime().ToString("yyyy/MM/dd HH:mm");
        }

        return utcText;
    }

    private void UpdatePressAnyKeyBlink()
    {
        if (!blinkPressAnyKey ||
            pressAnyKeyCanvasGroup == null ||
            menuOpened)
        {
            return;
        }

        float wave = (Mathf.Sin(
            Time.unscaledTime * blinkSpeed
        ) + 1f) * 0.5f;

        pressAnyKeyCanvasGroup.alpha = Mathf.Lerp(
            minimumBlinkAlpha,
            1f,
            wave
        );
    }

    private void SetContinueAvailable(bool available)
    {
        if (continueButton != null)
        {
            continueButton.interactable =
                available && !operationInProgress;
        }
    }

    private void SetButtonsInteractable(
        bool interactable,
        bool keepConfirmationButtons = false)
    {
        if (continueButton != null)
        {
            continueButton.interactable =
                interactable &&
                currentSlotInfo != null &&
                currentSlotInfo.HasSaveData &&
                currentSlotInfo.IsCompatible;
        }

        if (newGameButton != null)
        {
            newGameButton.interactable = interactable;
        }

        if (quitButton != null)
        {
            quitButton.interactable = interactable;
        }

        bool confirmationInteractable =
            keepConfirmationButtons || interactable;

        if (confirmNewGameButton != null)
        {
            confirmNewGameButton.interactable =
                confirmationInteractable;
        }

        if (cancelNewGameButton != null)
        {
            cancelNewGameButton.interactable =
                confirmationInteractable;
        }
    }

    private void FindReferences()
    {
        if (saveManager == null)
        {
            saveManager = SaveManager.Instance;
        }

        if (saveManager == null)
        {
            saveManager = FindAnyObjectByType<SaveManager>(
                FindObjectsInactive.Include
            );
        }
    }

    private void AddButtonListeners()
    {
        continueButton?.onClick.AddListener(ContinueGame);
        newGameButton?.onClick.AddListener(RequestNewGame);
        quitButton?.onClick.AddListener(QuitGame);
        confirmNewGameButton?.onClick.AddListener(ConfirmNewGame);
        cancelNewGameButton?.onClick.AddListener(CancelNewGame);
    }

    private void RemoveButtonListeners()
    {
        continueButton?.onClick.RemoveListener(ContinueGame);
        newGameButton?.onClick.RemoveListener(RequestNewGame);
        quitButton?.onClick.RemoveListener(QuitGame);
        confirmNewGameButton?.onClick.RemoveListener(ConfirmNewGame);
        cancelNewGameButton?.onClick.RemoveListener(CancelNewGame);
    }

    private void SetStatus(string message)
    {
        SetText(statusText, message ?? string.Empty);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
        {
            target.SetActive(active);
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[TitleScreenController] {message}", this);
        }
    }

    private void OnValidate()
    {
        inputEnableDelay = Mathf.Max(0f, inputEnableDelay);
        blinkSpeed = Mathf.Max(0.01f, blinkSpeed);
        minimumBlinkAlpha = Mathf.Clamp01(minimumBlinkAlpha);
    }
}
