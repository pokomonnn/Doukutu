using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// Stage1～4で共通利用する出撃パネルです。
/// 選択されたStageExpeditionDataを表示し、
/// 「出発する」で既存SceneTransitionButtonの安全な遷移処理を呼びます。
///
/// 開く時は、小さい状態から広がるポップアップ演出と開く効果音を再生できます。
/// 閉じる時は閉じる効果音を再生できます。
/// 「出発する」押下時は出撃音を鳴らし、指定秒数待機しながら画面全体を
/// 徐々にフェードアウトしてから既存SceneTransitionButtonへ遷移を渡します。
/// </summary>
[DisallowMultipleComponent]
public class StageExpeditionPanelController : MonoBehaviour
{
    [Header("パネル")]
    [Tooltip("表示・非表示を切り替えるPanel本体。未設定ならこのGameObjectを使用します。")]
    [SerializeField]
    private GameObject panelRoot;

    [Tooltip("ゲーム開始時は出撃パネルを閉じます。")]
    [SerializeField]
    private bool startHidden = true;

    [Header("表示Text")]
    [SerializeField]
    private TMP_Text stageNameText;

    [SerializeField]
    private TMP_Text combatDangerText;

    [SerializeField]
    private TMP_Text caveDangerText;

    [SerializeField]
    private TMP_Text requiredSuppliesText;

    [Header("出撃地点選択")]
    [Tooltip("出撃地点選択UI全体です。出撃地点が未設定のステージでは自動で非表示にできます。")]
    [InspectorName("出撃地点選択エリア")]
    [SerializeField]
    private GameObject entryPointSelectionRoot;

    [Tooltip("選択中の出撃地点に設定されたプレビュー画像を表示します。")]
    [InspectorName("出撃地点プレビュー画像")]
    [SerializeField]
    private Image entryPointPreviewImage;

    [Tooltip("選択中の出撃地点名を表示します。不要なら未設定でOKです。")]
    [InspectorName("選択中の出撃地点名")]
    [SerializeField]
    private TMP_Text entryPointNameText;

    [Tooltip(
        "出撃地点を選択するButtonを順番に登録します。" +
        "Stage1で4か所ならSize=4にしてButton1～4を設定してください。"
    )]
    [InspectorName("出撃地点ボタン")]
    [SerializeField]
    private List<Button> entryPointButtons =
        new List<Button>();

    [Tooltip("オンならButton内のTextMeshProへ出撃地点の表示名を自動反映します。")]
    [InspectorName("ボタン名を自動反映")]
    [SerializeField]
    private bool autoApplyEntryPointNamesToButtons = true;

    [Tooltip("出撃地点の選択・出撃時保存をConsoleへ表示します。")]
    [InspectorName("出撃地点ログを表示")]
    [SerializeField]
    private bool showEntryPointDebugLogs = true;

    [Header("開催中ミッション一覧")]
    [Tooltip("MissionButtonを自動生成する親です。Scroll View > Viewport > Content を設定してください。")]
    [SerializeField]
    private Transform missionListContent;

    [Tooltip("StageExpeditionMissionButtonUIを付けたミッションButton Prefabです。")]
    [SerializeField]
    private StageExpeditionMissionButtonUI missionButtonPrefab;

    [Tooltip("ContentにVerticalLayoutGroup / ContentSizeFitterが無い場合、実行時に補助設定します。")]
    [SerializeField]
    private bool autoConfigureMissionListLayout = true;

    [SerializeField, Min(1f)]
    private float missionButtonPreferredHeight = 56f;

    [SerializeField, Min(0f)]
    private float missionButtonSpacing = 8f;

    [SerializeField, Min(0f)]
    private float missionListPadding = 4f;

    [Header("ミッション詳細")]
    [Tooltip("右側のミッション詳細エリア全体。未設定でもText/Imageだけ設定すれば動作します。")]
    [SerializeField]
    private GameObject missionDetailRoot;

    [SerializeField]
    private Image missionImage;

    [SerializeField]
    private TMP_Text missionTitleText;

    [Tooltip("Scroll ViewのContent内に置いた説明Textを設定します。")]
    [SerializeField]
    private TMP_Text missionDescriptionText;

    [Tooltip("説明欄のScrollRectです。ミッション切替時に一番上へ戻します。")]
    [SerializeField]
    private ScrollRect missionDescriptionScrollRect;

    [SerializeField]
    private string noMissionTitle = "開催中ミッションなし";

    [SerializeField, TextArea(2, 4)]
    private string noMissionDescription =
        "この洞窟では現在開催中のミッションはありません。";

    [Header("出撃時のミッション自動受注")]
    [Tooltip("ONなら「出発する」を押した時、この洞窟の開催中ミッションをGameSessionManagerへ自動受注します。")]
    [SerializeField]
    private bool autoAcceptStageMissionsOnDepart = true;

    [Tooltip("開催中ミッションのうち、最初に受注できたミッションを自動追跡します。")]
    [SerializeField]
    private bool trackFirstAcceptedMission = true;

    [SerializeField]
    private bool showMissionDebugLogs = true;

    [Header("Button")]
    [SerializeField]
    private Button departButton;

    [SerializeField]
    private Button closeButton;

    [Header("出撃前補充画面（新規）")]
    [Tooltip("ON: 出撃パネルのボタンは『次へ』となり、補充画面を経由します。設定が不完全なら誤出撃しないよう停止します。")]
    [SerializeField] private bool useSupplyPreparationScreen = true;

    [Tooltip("別階層（出撃パネルの子ではない）に置いた出撃補充画面Controllerを指定します。")]
    [SerializeField] private ExpeditionSupplyPanelController2D supplyPanelController;

    [Tooltip("ONなら出撃選択パネルのDepart Buttonに表示される文字を『次へ』に変更します。")]
    [SerializeField] private bool autoSetNextButtonText = true;
    [SerializeField] private string nextButtonLabel = "次へ";

    [Header("パネルを開く演出")]
    [Tooltip("オンなら、パネルを小さい状態から広げて表示します。")]
    [SerializeField]
    private bool useOpenAnimation = true;

    [Tooltip("パネルが広がり切るまでの秒数です。")]
    [SerializeField, Min(0.01f)]
    private float openAnimationDuration = 0.22f;

    [Tooltip("開き始めの大きさです。0.75なら75%の大きさから始まります。")]
    [SerializeField, Range(0.1f, 1f)]
    private float openStartScale = 0.75f;

    [Tooltip("最後に少しだけ勢いよく広がる強さです。1.15～1.5程度がおすすめです。")]
    [SerializeField, Range(0f, 3f)]
    private float openOvershoot = 1.25f;

    [Header("パネルを開く音")]
    [Tooltip("未設定なら、このGameObjectまたはPanel RootからAudioSourceを探します。")]
    [SerializeField]
    private AudioSource audioSource;

    [Tooltip("出撃パネルを開いた瞬間に鳴らす効果音です。")]
    [SerializeField]
    private AudioClip openSound;

    [SerializeField, Range(0f, 1f)]
    private float openSoundVolume = 1f;

    [Header("パネルを閉じる音")]
    [SerializeField] private AudioClip closeSound;
    [SerializeField, Range(0f, 1f)] private float closeSoundVolume = 1f;

    [Header("出撃時の演出")]
    [SerializeField] private AudioClip departSound;
    [SerializeField, Range(0f, 1f)] private float departSoundVolume = 1f;
    [SerializeField, Min(0f)] private float departureDelaySeconds = 2f;
    [SerializeField, Min(0.01f)] private float departureFadeDuration = 2f;
    [SerializeField] private Color departureFadeColor = Color.black;

    [Header("出撃フェードUI")]
    [SerializeField] private CanvasGroup departureFadeCanvasGroup;
    [SerializeField] private Image departureFadeImage;
    [SerializeField] private bool autoCreateDepartureFade = true;

    private StageExpeditionData selectedStageData;
    private SceneTransitionButton selectedTransitionButton;

    private bool initialized;
    private Coroutine openAnimationCoroutine;
    private Coroutine departureCoroutine;
    private Vector3 panelBaseScale = Vector3.one;
    private bool isDeparting;
    // 補充画面からの出撃SEを押下時点で先行再生したか。
    // Stage側のDepartureRoutineで重複再生しないためのフラグ。
    private bool departureSoundPlayedEarlyForSupply;
    private bool showingSupplyScreen;
    private CanvasGroup supplyHiddenCanvasGroup;
    private float savedPanelAlpha = 1f;
    private bool savedPanelInteractable = true;
    private bool savedPanelBlocksRaycasts = true;

    private int selectedEntryPointIndex = -1;

    private readonly List<UnityAction> entryPointButtonCallbacks =
        new List<UnityAction>();

    private readonly List<StageExpeditionMissionButtonUI> missionButtons =
        new List<StageExpeditionMissionButtonUI>();

    private int selectedMissionIndex = -1;

    // 今回の出撃パネルに実際に表示しているランダム抽選結果。
    private readonly List<MissionDefinition2D> currentStageMissions =
        new List<MissionDefinition2D>();

    // 同じ町シーン内では、パネルを閉じて開き直しても再抽選しないためのキャッシュ。
    private readonly Dictionary<StageExpeditionData, List<MissionDefinition2D>>
        stageMissionSelectionCache =
            new Dictionary<StageExpeditionData, List<MissionDefinition2D>>();

    /// <summary>
    /// 現在パネルで選択されているステージ。
    /// 将来、ミッション一覧を表示する時にも利用できます。
    /// </summary>
    public StageExpeditionData CurrentStageData =>
        selectedStageData;

    public int SelectedEntryPointIndex =>
        selectedEntryPointIndex;

    public StageExpeditionEntryPointData SelectedEntryPoint =>
        selectedStageData != null
            ? selectedStageData.GetEntryPoint(selectedEntryPointIndex)
            : null;

    public int SelectedMissionIndex =>
        selectedMissionIndex;

    public MissionDefinition2D SelectedMission =>
        GetCurrentMission(selectedMissionIndex);

    public IReadOnlyList<MissionDefinition2D> CurrentStageMissions =>
        currentStageMissions;

    public bool IsOpen =>
        panelRoot != null &&
        panelRoot.activeSelf &&
        selectedStageData != null &&
        !showingSupplyScreen;

    public GameObject PanelRoot => panelRoot;
    public bool IsShowingSupplyScreen => showingSupplyScreen;

    /// <summary>
    /// 将来のミッションUIなどが、ステージ選択を検知するためのイベントです。
    /// </summary>
    public event Action<StageExpeditionData> PanelOpened;

    public event Action PanelClosed;

    private void Awake()
    {
        EnsureInitialized();
        SceneManager.activeSceneChanged += HandleActiveSceneChanged;

        if (panelRoot != null)
        {
            panelBaseScale = panelRoot.transform.localScale;
        }

        FindAudioSource();

        if (startHidden)
        {
            HideImmediate();
        }
    }

    private void OnDisable()
    {
        if (openAnimationCoroutine != null)
        {
            StopCoroutine(openAnimationCoroutine);
            openAnimationCoroutine = null;
        }

        if (departureCoroutine != null)
        {
            StopCoroutine(departureCoroutine);
            departureCoroutine = null;
        }

        isDeparting = false;
        departureSoundPlayedEarlyForSupply = false;
    }

    private void OnDestroy()
    {
        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;

        if (departButton != null)
        {
            departButton.onClick.RemoveListener(Depart);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }

        RemoveEntryPointButtonListeners();
        ClearMissionButtons();
    }

    /// <summary>
    /// StageExpeditionButtonから呼びます。
    /// </summary>
    public void Open(
        StageExpeditionData stageData,
        SceneTransitionButton transitionButton)
    {
        EnsureInitialized();

        if (stageData == null)
        {
            Debug.LogWarning(
                "[StageExpeditionPanelController] StageExpeditionDataがnullです。",
                this
            );
            return;
        }

        if (transitionButton == null)
        {
            Debug.LogWarning(
                "[StageExpeditionPanelController] SceneTransitionButtonがnullです。",
                this
            );
            return;
        }

        RestorePanelAfterSupply();
        selectedStageData = stageData;
        selectedTransitionButton = transitionButton;
        RefreshDepartButtonLabel();

        // 前回出撃時の選択が残っていても、新しい出撃パネルを開いた時点で破棄します。
        ExpeditionEntrySelectionSession.Clear();

        RefreshDisplay();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        RefreshEntryPointSection();
        RefreshMissionSection();

        isDeparting = false;
        departureSoundPlayedEarlyForSupply = false;
        ResetDepartureFade();

        if (departButton != null)
        {
            departButton.interactable = !StoryDestinationLocked();
        }

        if (closeButton != null)
        {
            closeButton.interactable = true;
        }

        SetMissionButtonsInteractable(true);

        PlayOpenSound();
        PlayOpenAnimation();

        PanelOpened?.Invoke(selectedStageData);
    }

    /// <summary>
    /// これまでの『出撃』ボタンを『次へ』として使用する入口。
    /// 出撃先の保存・ミッションの自動受注・Scene遷移はここでは実行しません。
    /// </summary>
    public void Depart()
    {
        if (isDeparting || showingSupplyScreen || !CanStartDeparture()) return;

        if (!useSupplyPreparationScreen)
        {
            TryStartFinalDeparture();
            return;
        }

        if (supplyPanelController == null)
        {
            Debug.LogError("[StageExpeditionPanelController] 出撃用補充画面が未設定です。Supply Panel Controller を設定してください。誤出撃防止のため移動しません。", this);
            return;
        }

        if (!supplyPanelController.OpenForStage(this)) return;
        HidePanelForSupply();
    }

    /// <summary>補充画面から戻る。選択中のステージ・出撃地点・ミッションを維持します。</summary>
    public void ReturnFromSupply()
    {
        if (isDeparting) return;
        RestorePanelAfterSupply();
        departureSoundPlayedEarlyForSupply = false;
        if (departButton != null) departButton.interactable = true;
        if (closeButton != null) closeButton.interactable = true;
        SetEntryPointButtonsInteractable(true);
        SetMissionButtonsInteractable(true);
    }

    /// <summary>
    /// 補充画面の「出撃する」押下直後に呼ぶ。
    /// Stageで設定されている本来の出撃SEを今すぐ再生し、
    /// 暗転後にDepartureRoutineで同じ音が重複しないよう記録する。
    /// 引数はStageに出撃SEがない旧Sceneだけのフォールバック。
    /// </summary>
    public bool TryPlayDepartureSoundImmediatelyFromSupply(
        AudioClip fallbackClip = null,
        AudioSource fallbackSource = null,
        float fallbackVolume = 1f)
    {
        if (!useSupplyPreparationScreen || !showingSupplyScreen ||
            isDeparting || !isActiveAndEnabled || !CanStartDeparture())
            return false;

        if (departureSoundPlayedEarlyForSupply) return false;

        if (departSound != null)
        {
            PlayDepartSound();
        }
        else if (fallbackClip != null)
        {
            if (fallbackSource != null && fallbackSource.isActiveAndEnabled)
                fallbackSource.PlayOneShot(fallbackClip, Mathf.Clamp01(fallbackVolume));
            else
                PlayDetachedUISound(fallbackClip, fallbackVolume);
        }
        else
        {
            Debug.LogWarning(
                "[StageExpeditionPanelController] 出撃SEが未設定です。InspectorのDepart Soundを確認してください。",
                this);
        }

        departureSoundPlayedEarlyForSupply = true;
        return true;
    }

    /// <summary>補充画面で出撃開始を取り消したときの先行SEフラグ解除。</summary>
    public void CancelPreparedDepartureSoundFromSupply()
    {
        if (!isDeparting) departureSoundPlayedEarlyForSupply = false;
    }

    /// <summary>旧呼び出し元との互換性を維持します。</summary>
    public bool StartFinalDepartureFromSupply()
    {
        return StartFinalDepartureFromSupply(false);
    }

    /// <summary>
    /// 既に補充画面で全面暗転済みなら、Stage側の2回目の暗転と待機を省略。
    /// 出撃地点保存・ミッション受注・既存SceneTransitionはそのまま実行。
    /// </summary>
    public bool StartFinalDepartureFromSupply(bool supplyFadeAlreadyCompleted)
    {
        if (!showingSupplyScreen || !useSupplyPreparationScreen)
        {
            Debug.LogWarning("[StageExpeditionPanelController] 補充画面を経由していないため出撃を中止しました。", this);
            return false;
        }
        return TryStartFinalDeparture(supplyFadeAlreadyCompleted);
    }

    private bool CanStartDeparture()
    {
        if (StoryDestinationLocked()) return false;
        if (selectedStageData == null)
        {
            Debug.LogWarning("[StageExpeditionPanelController] 出発先ステージが選択されていません。", this);
            return false;
        }
        if (selectedTransitionButton == null)
        {
            Debug.LogWarning("[StageExpeditionPanelController] SceneTransitionButtonが設定されていません。", this);
            return false;
        }
        if (string.IsNullOrWhiteSpace(selectedTransitionButton.TargetSceneName) ||
            !Application.CanStreamedLevelBeLoaded(selectedTransitionButton.TargetSceneName))
        {
            Debug.LogWarning("[StageExpeditionPanelController] 移動先SceneがBuild Settingsに登録されていません：" + selectedTransitionButton.TargetSceneName, this);
            return false;
        }
        if (selectedStageData.EntryPointCount > 0 && !HasValidSelectedEntryPoint())
        {
            Debug.LogWarning("[StageExpeditionPanelController] 有効な出撃地点が選択されていません。", this);
            return false;
        }
        return true;
    }

    private bool StoryDestinationLocked()
    {
        var story = StoryCampaignRuntime.Database;
        return story != null && selectedTransitionButton != null &&
            selectedTransitionButton.TargetSceneName == story.stageScenes[4] && !StoryCampaignRuntime.GardenOpen;
    }

    private bool TryStartFinalDeparture(bool supplyFadeAlreadyCompleted = false)
    {
        if (isDeparting || !isActiveAndEnabled || !CanStartDeparture()) return false;
        departureCoroutine = StartCoroutine(DepartureRoutine(supplyFadeAlreadyCompleted));
        return true;
    }

    private void HidePanelForSupply()
    {
        if (panelRoot == null) return;
        supplyHiddenCanvasGroup = panelRoot.GetComponent<CanvasGroup>();
        if (supplyHiddenCanvasGroup == null)
        {
            supplyHiddenCanvasGroup = panelRoot.AddComponent<CanvasGroup>();
        }

        savedPanelAlpha = supplyHiddenCanvasGroup.alpha;
        savedPanelInteractable = supplyHiddenCanvasGroup.interactable;
        savedPanelBlocksRaycasts = supplyHiddenCanvasGroup.blocksRaycasts;
        showingSupplyScreen = true;
        supplyHiddenCanvasGroup.alpha = 0f;
        supplyHiddenCanvasGroup.interactable = false;
        supplyHiddenCanvasGroup.blocksRaycasts = false;
        // panelRoot.SetActive(false) はしない。Controller自身が無効化されると出撃Coroutineを実行できないため。
    }

    private void RestorePanelAfterSupply()
    {
        showingSupplyScreen = false;
        if (supplyHiddenCanvasGroup == null) return;
        supplyHiddenCanvasGroup.alpha = savedPanelAlpha;
        supplyHiddenCanvasGroup.interactable = savedPanelInteractable;
        supplyHiddenCanvasGroup.blocksRaycasts = savedPanelBlocksRaycasts;
        // 追加したCanvasGroupは残して再利用。Destroyの遅延で次回のOpenが壊れるのを防ぎます。
        supplyHiddenCanvasGroup = null;
    }

    public void Close()
    {
        if (isDeparting || showingSupplyScreen) return;

        RestorePanelAfterSupply();
        StopOpenAnimationAndRestoreScale();
        PlayCloseSound();

        selectedStageData = null;
        selectedTransitionButton = null;
        selectedEntryPointIndex = -1;
        selectedMissionIndex = -1;
        ResetEntryPointPreview();
        ExpeditionEntrySelectionSession.Clear();
        ClearMissionButtons();
        currentStageMissions.Clear();

        if (panelRoot != null) panelRoot.SetActive(false);

        PanelClosed?.Invoke();
    }

    private void HideImmediate()
    {
        RestorePanelAfterSupply();
        StopOpenAnimationAndRestoreScale();

        selectedStageData = null;
        selectedTransitionButton = null;
        selectedEntryPointIndex = -1;
        selectedMissionIndex = -1;
        ResetEntryPointPreview();
        ExpeditionEntrySelectionSession.Clear();
        ClearMissionButtons();
        currentStageMissions.Clear();

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private IEnumerator DepartureRoutine(bool supplyFadeAlreadyCompleted)
    {
        isDeparting = true;
        bool skipSound = departureSoundPlayedEarlyForSupply;
        departureSoundPlayedEarlyForSupply = false;

        PrepareEntryPointSelectionForDeparture();

        if (autoAcceptStageMissionsOnDepart)
        {
            AcceptCurrentStageMissions();
        }

        if (departButton != null) departButton.interactable = false;
        if (closeButton != null) closeButton.interactable = false;
        SetEntryPointButtonsInteractable(false);
        SetMissionButtonsInteractable(false);

        if (!skipSound) PlayDepartSound();

        // 補充画面側は既に最前面に黒いCanvasを持っているので、
        // Stageの別Canvasを生成して画面を二重に暗転させる必要はない。
        if (!supplyFadeAlreadyCompleted)
        {
            EnsureDepartureFadeVisual();

            if (departureFadeCanvasGroup != null)
            {
                departureFadeCanvasGroup.alpha = 0f;
                departureFadeCanvasGroup.blocksRaycasts = true;
                departureFadeCanvasGroup.interactable = true;
                departureFadeCanvasGroup.transform.SetAsLastSibling();
            }

            float delay = Mathf.Max(0f, departureDelaySeconds);
            float fadeDuration = Mathf.Max(0.01f, departureFadeDuration);
            float totalDuration = Mathf.Max(delay, fadeDuration);
            float elapsed = 0f;

            while (elapsed < totalDuration)
            {
                elapsed += Time.unscaledDeltaTime;

                if (departureFadeCanvasGroup != null)
                {
                    float fadeT = Mathf.Clamp01(elapsed / fadeDuration);
                    departureFadeCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, fadeT);
                }

                yield return null;
            }

            if (departureFadeCanvasGroup != null) departureFadeCanvasGroup.alpha = 1f;
        }
        else
        {
            // Stage側の処理を1フレーム分離し、ボタンのOnClick中に
            // シーンが即時破棄されることを防ぐ（補充画面の黒Canvasは維持）。
            yield return null;
        }

        SceneTransitionButton transitionButton = selectedTransitionButton;
        departureCoroutine = null;

        if (transitionButton != null)
        {
            transitionButton.LoadConfiguredScene();
        }
        else
        {
            isDeparting = false;
            if (departButton != null) departButton.interactable = true;
            if (closeButton != null) closeButton.interactable = true;
            SetEntryPointButtonsInteractable(true);
            SetMissionButtonsInteractable(true);
            ResetDepartureFade();
        }
    }

    private void EnsureDepartureFadeVisual()
    {
        if (departureFadeCanvasGroup != null)
        {
            if (departureFadeImage == null)
                departureFadeImage = departureFadeCanvasGroup.GetComponent<Image>();
            ApplyDepartureFadeVisualSettings();
            return;
        }

        if (!autoCreateDepartureFade) return;

        Canvas rootCanvas = null;
        if (panelRoot != null)
        {
            Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
            if (canvas != null) rootCanvas = canvas.rootCanvas;
        }
        if (rootCanvas == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) rootCanvas = canvas.rootCanvas;
        }
        if (rootCanvas == null)
        {
            Debug.LogWarning("[StageExpeditionPanelController] 出撃フェード用Root Canvasが見つかりません。", this);
            return;
        }

        GameObject fadeObject = new GameObject(
            "ExpeditionDepartureFade",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));

        fadeObject.transform.SetParent(rootCanvas.transform, false);

        RectTransform rect = fadeObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        departureFadeCanvasGroup = fadeObject.GetComponent<CanvasGroup>();
        departureFadeImage = fadeObject.GetComponent<Image>();
        ApplyDepartureFadeVisualSettings();
    }

    private void ApplyDepartureFadeVisualSettings()
    {
        if (departureFadeImage != null)
        {
            departureFadeImage.color = departureFadeColor;
            departureFadeImage.raycastTarget = true;
        }

        if (departureFadeCanvasGroup != null)
        {
            departureFadeCanvasGroup.alpha = 0f;
            departureFadeCanvasGroup.interactable = false;
            departureFadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private void ResetDepartureFade()
    {
        if (departureFadeCanvasGroup == null) return;
        departureFadeCanvasGroup.alpha = 0f;
        departureFadeCanvasGroup.interactable = false;
        departureFadeCanvasGroup.blocksRaycasts = false;
    }

    private void PlayOpenAnimation()
    {
        if (panelRoot == null)
        {
            return;
        }

        if (openAnimationCoroutine != null)
        {
            StopCoroutine(openAnimationCoroutine);
            openAnimationCoroutine = null;
        }

        if (!useOpenAnimation || openAnimationDuration <= 0.001f)
        {
            panelRoot.transform.localScale = panelBaseScale;
            return;
        }

        openAnimationCoroutine =
            StartCoroutine(OpenAnimationRoutine());
    }

    private IEnumerator OpenAnimationRoutine()
    {
        if (panelRoot == null)
        {
            yield break;
        }

        Transform panelTransform = panelRoot.transform;

        Vector3 startScale =
            panelBaseScale * Mathf.Clamp(openStartScale, 0.1f, 1f);

        panelTransform.localScale = startScale;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, openAnimationDuration);

        while (elapsed < duration)
        {
            if (panelRoot == null)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = EaseOutBack(t, openOvershoot);

            panelTransform.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    panelBaseScale,
                    easedT
                );

            yield return null;
        }

        panelTransform.localScale = panelBaseScale;
        openAnimationCoroutine = null;
    }

    /// <summary>
    /// 0→1の途中で少しだけ1を超える、ポップアップ向け補間です。
    /// </summary>
    private static float EaseOutBack(float t, float overshoot)
    {
        float c1 = Mathf.Max(0f, overshoot);
        float c3 = c1 + 1f;
        float x = t - 1f;

        return 1f +
               c3 * x * x * x +
               c1 * x * x;
    }

    private void StopOpenAnimationAndRestoreScale()
    {
        if (openAnimationCoroutine != null)
        {
            StopCoroutine(openAnimationCoroutine);
            openAnimationCoroutine = null;
        }

        if (panelRoot != null)
        {
            panelRoot.transform.localScale = panelBaseScale;
        }
    }

    private void PlayCloseSound()
    {
        PlayUISound(closeSound, closeSoundVolume, true);
    }

    private void PlayDepartSound()
    {
        PlayUISound(departSound, departSoundVolume, false);
    }

    private void PlayUISound(AudioClip clip, float volume, bool survivePanelDisable)
    {
        if (clip == null) return;
        FindAudioSource();

        bool sourceWillBeDisabled =
            survivePanelDisable &&
            audioSource != null &&
            panelRoot != null &&
            (audioSource.gameObject == panelRoot || audioSource.transform.IsChildOf(panelRoot.transform));

        if (audioSource != null && !sourceWillBeDisabled)
        {
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            return;
        }

        PlayDetachedUISound(clip, volume);
    }

    private static void PlayDetachedUISound(AudioClip clip, float volume)
    {
        if (clip == null) return;

        GameObject audioObject = new GameObject("TemporaryUISound");
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(volume);
        source.clip = clip;
        source.Play();
        Destroy(audioObject, Mathf.Max(0.1f, clip.length + 0.1f));
    }

    private void PlayOpenSound()
    {
        if (openSound == null)
        {
            return;
        }

        FindAudioSource();

        if (audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            openSound,
            Mathf.Clamp01(openSoundVolume)
        );
    }

    private void FindAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null && panelRoot != null)
        {
            audioSource = panelRoot.GetComponent<AudioSource>();
        }
    }

    private void RefreshDisplay()
    {
        if (selectedStageData == null)
        {
            return;
        }

        if (stageNameText != null)
        {
            stageNameText.text =
                selectedStageData.StageName + (StoryDestinationLocked() ? "\n町長へのメイン報告3件で開放" : "");
        }

        if (combatDangerText != null)
        {
            combatDangerText.text =
                BuildStars(
                    selectedStageData.CombatDanger
                );
        }

        if (caveDangerText != null)
        {
            caveDangerText.text =
                BuildStars(
                    selectedStageData.CaveDanger
                );
        }

        if (requiredSuppliesText != null)
        {
            requiredSuppliesText.text =
                BuildStars(
                    selectedStageData.RequiredSupplies
                );
        }
    }


    // ---------------------------------------------------------------------
    // 出撃地点選択
    // ---------------------------------------------------------------------

    private void RefreshEntryPointSection()
    {
        selectedEntryPointIndex = -1;

        int entryCount =
            selectedStageData != null
                ? selectedStageData.EntryPointCount
                : 0;

        bool hasAnyEntryPoint = false;

        for (int i = 0; i < entryPointButtons.Count; i++)
        {
            Button button = entryPointButtons[i];

            if (button == null)
            {
                continue;
            }

            StageExpeditionEntryPointData entry =
                selectedStageData != null
                    ? selectedStageData.GetEntryPoint(i)
                    : null;

            bool valid =
                entry != null &&
                !string.IsNullOrWhiteSpace(entry.EntryPointId);

            button.gameObject.SetActive(valid);
            button.interactable = valid && !isDeparting;

            if (valid)
            {
                hasAnyEntryPoint = true;

                if (autoApplyEntryPointNamesToButtons)
                {
                    TMP_Text label =
                        button.GetComponentInChildren<TMP_Text>(true);

                    if (label != null)
                    {
                        label.text =
                            string.IsNullOrWhiteSpace(entry.DisplayName)
                                ? $"出撃地点{i + 1}"
                                : entry.DisplayName;
                    }
                }
            }
        }

        // Data側にButton数より多く設定されていても、
        // 少なくとも有効地点があるかはData全体から確認します。
        if (!hasAnyEntryPoint && selectedStageData != null)
        {
            for (int i = 0; i < selectedStageData.EntryPointCount; i++)
            {
                StageExpeditionEntryPointData entry =
                    selectedStageData.GetEntryPoint(i);

                if (entry != null &&
                    !string.IsNullOrWhiteSpace(entry.EntryPointId))
                {
                    hasAnyEntryPoint = true;
                    break;
                }
            }
        }

        if (entryPointSelectionRoot != null)
        {
            entryPointSelectionRoot.SetActive(hasAnyEntryPoint);
        }

        int firstIndex = FindFirstValidEntryPointIndex();

        if (firstIndex >= 0)
        {
            SelectEntryPoint(firstIndex);
        }
        else
        {
            ResetEntryPointPreview();
        }
    }

    public void SelectEntryPoint(int entryPointIndex)
    {
        if (isDeparting ||
            selectedStageData == null)
        {
            return;
        }

        StageExpeditionEntryPointData entry =
            selectedStageData.GetEntryPoint(entryPointIndex);

        if (entry == null ||
            string.IsNullOrWhiteSpace(entry.EntryPointId))
        {
            return;
        }

        selectedEntryPointIndex = entryPointIndex;
        RefreshEntryPointPreview(entry);

        if (showEntryPointDebugLogs)
        {
            Debug.Log(
                "[StageExpeditionPanelController] " +
                $"出撃地点を選択しました：" +
                $"Stage={selectedStageData.StageId} / " +
                $"Index={entryPointIndex} / " +
                $"EntryId={entry.EntryPointId} / " +
                $"Name={entry.DisplayName}",
                this
            );
        }
    }

    // InspectorのButton OnClickから直接呼びたい場合にも使えます。
    public void SelectEntryPoint1() => SelectEntryPoint(0);
    public void SelectEntryPoint2() => SelectEntryPoint(1);
    public void SelectEntryPoint3() => SelectEntryPoint(2);
    public void SelectEntryPoint4() => SelectEntryPoint(3);

    private void HandleEntryPointButtonClicked(int entryPointIndex)
    {
        SelectEntryPoint(entryPointIndex);
    }

    private void RefreshEntryPointPreview(
        StageExpeditionEntryPointData entry)
    {
        if (entryPointPreviewImage != null)
        {
            Sprite preview = entry != null
                ? entry.PreviewImage
                : null;

            entryPointPreviewImage.sprite = preview;
            entryPointPreviewImage.enabled = preview != null;
            entryPointPreviewImage.preserveAspect = true;
        }

        if (entryPointNameText != null)
        {
            entryPointNameText.text =
                entry != null
                    ? entry.DisplayName
                    : string.Empty;
        }
    }

    private void ResetEntryPointPreview()
    {
        if (entryPointPreviewImage != null)
        {
            entryPointPreviewImage.sprite = null;
            entryPointPreviewImage.enabled = false;
        }

        if (entryPointNameText != null)
        {
            entryPointNameText.text = string.Empty;
        }
    }

    private int FindFirstValidEntryPointIndex()
    {
        if (selectedStageData == null)
        {
            return -1;
        }

        int usableCount = Mathf.Min(
            selectedStageData.EntryPointCount,
            entryPointButtons != null
                ? entryPointButtons.Count
                : 0
        );

        for (int i = 0; i < usableCount; i++)
        {
            StageExpeditionEntryPointData entry =
                selectedStageData.GetEntryPoint(i);

            if (entry != null &&
                !string.IsNullOrWhiteSpace(entry.EntryPointId) &&
                entryPointButtons[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    private bool HasValidSelectedEntryPoint()
    {
        StageExpeditionEntryPointData entry =
            SelectedEntryPoint;

        return
            entry != null &&
            !string.IsNullOrWhiteSpace(entry.EntryPointId);
    }

    private void PrepareEntryPointSelectionForDeparture()
    {
        ExpeditionEntrySelectionSession.Clear();

        StageExpeditionEntryPointData entry =
            SelectedEntryPoint;

        if (selectedStageData == null ||
            entry == null ||
            string.IsNullOrWhiteSpace(entry.EntryPointId))
        {
            return;
        }

        // 新しい出撃を開始するので、前回同じStageで到達したCheckpointが
        // Staticに残っていても選択した出撃地点より優先されないように破棄します。
        GameManager.ApplyCheckpointSaveData(null);

        ExpeditionEntrySelectionSession.SetSelection(
            selectedStageData.StageId,
            entry.EntryPointId,
            entry.DisplayName
        );

        if (showEntryPointDebugLogs)
        {
            Debug.Log(
                "[StageExpeditionPanelController] " +
                $"出撃地点をScene移動用に保存しました：" +
                $"Stage={selectedStageData.StageId} / " +
                $"EntryId={entry.EntryPointId} / " +
                $"Name={entry.DisplayName}",
                this
            );
        }
    }

    private void SetEntryPointButtonsInteractable(bool interactable)
    {
        for (int i = 0; i < entryPointButtons.Count; i++)
        {
            Button button = entryPointButtons[i];

            if (button == null ||
                !button.gameObject.activeSelf)
            {
                continue;
            }

            button.interactable = interactable;
        }
    }

    private void RegisterEntryPointButtonListeners()
    {
        RemoveEntryPointButtonListeners();

        if (entryPointButtons == null)
        {
            entryPointButtons = new List<Button>();
            return;
        }

        for (int i = 0; i < entryPointButtons.Count; i++)
        {
            Button button = entryPointButtons[i];

            if (button == null)
            {
                entryPointButtonCallbacks.Add(null);
                continue;
            }

            int capturedIndex = i;

            UnityAction callback =
                () => HandleEntryPointButtonClicked(capturedIndex);

            entryPointButtonCallbacks.Add(callback);
            button.onClick.AddListener(callback);
        }
    }

    private void RemoveEntryPointButtonListeners()
    {
        if (entryPointButtons == null)
        {
            entryPointButtonCallbacks.Clear();
            return;
        }

        int count = Mathf.Min(
            entryPointButtons.Count,
            entryPointButtonCallbacks.Count
        );

        for (int i = 0; i < count; i++)
        {
            Button button = entryPointButtons[i];
            UnityAction callback =
                entryPointButtonCallbacks[i];

            if (button != null && callback != null)
            {
                button.onClick.RemoveListener(callback);
            }
        }

        entryPointButtonCallbacks.Clear();
    }

    // ---------------------------------------------------------------------
    // 出撃パネル内の開催中ミッション表示
    // ---------------------------------------------------------------------

    private void RefreshMissionSection()
    {
        PrepareCurrentStageMissions();
        RebuildMissionButtons();

        int firstMissionIndex = FindFirstValidMissionIndex();

        if (firstMissionIndex >= 0)
        {
            SelectMission(firstMissionIndex);
        }
        else
        {
            selectedMissionIndex = -1;
            RefreshMissionDetails(null);
        }
    }

    private void RebuildMissionButtons()
    {
        ClearMissionButtons();

        if (selectedStageData == null)
        {
            return;
        }

        EnsureMissionListLayout();

        if (missionListContent == null ||
            missionButtonPrefab == null)
        {
            if (showMissionDebugLogs)
            {
                Debug.LogWarning(
                    "[StageExpeditionPanelController] " +
                    "Mission List Content または Mission Button Prefab が未設定です。" +
                    "詳細表示だけは可能ですが、ミッション一覧Buttonは生成されません。",
                    this
                );
            }

            return;
        }

        IReadOnlyList<MissionDefinition2D> missions =
            currentStageMissions;

        if (missions == null)
        {
            return;
        }

        for (int i = 0; i < missions.Count; i++)
        {
            MissionDefinition2D mission = missions[i];

            if (mission == null)
            {
                continue;
            }

            StageExpeditionMissionButtonUI item =
                Instantiate(
                    missionButtonPrefab,
                    missionListContent
                );

            item.name =
                $"ExpeditionMissionButton_{i + 1}_{mission.name}";

            item.Setup(
                i,
                mission,
                HandleMissionButtonClicked
            );

            PrepareMissionButtonLayout(item);
            missionButtons.Add(item);
        }

        ForceRebuildMissionListLayout();
    }

    private void ClearMissionButtons()
    {
        for (int i = 0; i < missionButtons.Count; i++)
        {
            StageExpeditionMissionButtonUI item =
                missionButtons[i];

            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }

        missionButtons.Clear();
    }

    private void HandleMissionButtonClicked(
        int missionIndex)
    {
        if (isDeparting)
        {
            return;
        }

        SelectMission(missionIndex);
    }

    /// <summary>
    /// 出撃パネル上で説明を表示するミッションを切り替えます。
    /// これは受注選択ではなく、詳細を見るための選択です。
    /// </summary>
    public void SelectMission(int missionIndex)
    {
        if (selectedStageData == null)
        {
            return;
        }

        MissionDefinition2D mission =
            GetCurrentMission(missionIndex);

        if (mission == null)
        {
            return;
        }

        selectedMissionIndex = missionIndex;

        for (int i = 0; i < missionButtons.Count; i++)
        {
            StageExpeditionMissionButtonUI item =
                missionButtons[i];

            if (item != null)
            {
                item.SetSelected(
                    item.MissionIndex ==
                    selectedMissionIndex
                );
            }
        }

        RefreshMissionDetails(mission);
    }

    private void RefreshMissionDetails(
        MissionDefinition2D mission)
    {
        bool hasMission = mission != null;

        if (missionDetailRoot != null)
        {
            missionDetailRoot.SetActive(true);
        }

        if (missionTitleText != null)
        {
            missionTitleText.text =
                hasMission
                    ? mission.DisplayName
                    : noMissionTitle;
        }

        if (missionDescriptionText != null)
        {
            string description =
                hasMission
                    ? mission.Description
                    : noMissionDescription;

            missionDescriptionText.text =
                string.IsNullOrWhiteSpace(description)
                    ? "説明なし"
                    : description;
        }

        if (missionImage != null)
        {
            Sprite sprite =
                hasMission
                    ? mission.MissionImage
                    : null;

            missionImage.sprite = sprite;
            missionImage.enabled = sprite != null;
        }

        ResetMissionDescriptionScroll();
    }

    private void ResetMissionDescriptionScroll()
    {
        if (missionDescriptionScrollRect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        missionDescriptionScrollRect.StopMovement();
        missionDescriptionScrollRect.verticalNormalizedPosition = 1f;
    }

    private int FindFirstValidMissionIndex()
    {
        for (int i = 0; i < currentStageMissions.Count; i++)
        {
            if (currentStageMissions[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    private MissionDefinition2D GetCurrentMission(int index)
    {
        if (index < 0 || index >= currentStageMissions.Count)
        {
            return null;
        }

        return currentStageMissions[index];
    }

    /// <summary>
    /// この町シーン内でまだ抽選していなければMission Poolから重複なしで抽選します。
    /// 一度抽選したステージは、パネルを閉じて開き直しても同じ結果を使います。
    /// </summary>
    private void PrepareCurrentStageMissions()
    {
        currentStageMissions.Clear();

        if (selectedStageData == null)
        {
            return;
        }

        if (stageMissionSelectionCache.TryGetValue(
                selectedStageData,
                out List<MissionDefinition2D> cachedMissions))
        {
            currentStageMissions.AddRange(cachedMissions);

            if (showMissionDebugLogs)
            {
                Debug.Log(
                    "[StageExpeditionPanelController] " +
                    $"抽選済みミッションを再利用：Stage={selectedStageData.StageName} / " +
                    $"Count={currentStageMissions.Count}",
                    this
                );
            }

            return;
        }

        List<MissionDefinition2D> validPool =
            BuildUniqueValidMissionPool(selectedStageData);

        int drawCount = Mathf.Min(
            selectedStageData.RandomMissionCount,
            validPool.Count
        );

        // Fisher-YatesでPoolをシャッフルし、先頭から必要件数を採用します。
        for (int i = validPool.Count - 1; i > 0; i--)
        {
            int swapIndex = UnityEngine.Random.Range(0, i + 1);
            MissionDefinition2D temp = validPool[i];
            validPool[i] = validPool[swapIndex];
            validPool[swapIndex] = temp;
        }

        for (int i = 0; i < drawCount; i++)
        {
            currentStageMissions.Add(validPool[i]);
        }

        stageMissionSelectionCache[selectedStageData] =
            new List<MissionDefinition2D>(currentStageMissions);

        if (showMissionDebugLogs)
        {
            Debug.Log(
                "[StageExpeditionPanelController] " +
                $"ランダムミッション抽選：Stage={selectedStageData.StageName} / " +
                $"Pool={validPool.Count} / Draw={currentStageMissions.Count}",
                this
            );

            for (int i = 0; i < currentStageMissions.Count; i++)
            {
                MissionDefinition2D mission = currentStageMissions[i];
                Debug.Log(
                    "[StageExpeditionPanelController] " +
                    $"  Mission{i + 1}={mission.DisplayName} / MissionId={mission.MissionId}",
                    this
                );
            }
        }
    }

    private List<MissionDefinition2D> BuildUniqueValidMissionPool(
        StageExpeditionData stageData)
    {
        List<MissionDefinition2D> result =
            new List<MissionDefinition2D>();

        if (stageData == null || stageData.MissionPool == null)
        {
            return result;
        }

        HashSet<MissionDefinition2D> seenAssets =
            new HashSet<MissionDefinition2D>();

        HashSet<string> seenMissionIds =
            new HashSet<string>(StringComparer.Ordinal);

        IReadOnlyList<MissionDefinition2D> pool =
            stageData.MissionPool;

        for (int i = 0; i < pool.Count; i++)
        {
            MissionDefinition2D mission = pool[i];

            if (mission == null || !seenAssets.Add(mission))
            {
                continue;
            }

            string missionId = mission.MissionId?.Trim() ?? string.Empty;

            if (!string.IsNullOrEmpty(missionId) &&
                !seenMissionIds.Add(missionId))
            {
                if (showMissionDebugLogs)
                {
                    Debug.LogWarning(
                        "[StageExpeditionPanelController] " +
                        $"Mission Pool内に同じMissionId『{missionId}』が複数あるため、" +
                        $"{mission.DisplayName} は重複候補から除外しました。",
                        this
                    );
                }

                continue;
            }

            result.Add(mission);
        }

        return result;
    }

    private void HandleActiveSceneChanged(Scene oldScene, Scene newScene)
    {
        // 町シーンを離れたら抽選固定を解除します。
        // 次に町へ戻って出撃パネルを開いた時は新しく抽選されます。
        stageMissionSelectionCache.Clear();
        currentStageMissions.Clear();
    }

    private void SetMissionButtonsInteractable(
        bool interactable)
    {
        for (int i = 0; i < missionButtons.Count; i++)
        {
            if (missionButtons[i] != null)
            {
                missionButtons[i].SetInteractable(
                    interactable
                );
            }
        }
    }

    private void EnsureMissionListLayout()
    {
        if (!autoConfigureMissionListLayout ||
            missionListContent == null)
        {
            return;
        }

        VerticalLayoutGroup layout =
            missionListContent.GetComponent<
                VerticalLayoutGroup
            >();

        if (layout == null)
        {
            layout =
                missionListContent.gameObject
                    .AddComponent<VerticalLayoutGroup>();
        }

        int padding =
            Mathf.RoundToInt(
                Mathf.Max(0f, missionListPadding)
            );

        layout.childAlignment =
            TextAnchor.UpperCenter;

        layout.padding =
            new RectOffset(
                padding,
                padding,
                padding,
                padding
            );

        layout.spacing =
            Mathf.Max(0f, missionButtonSpacing);

        layout.childControlWidth = true;
        layout.childControlHeight = false;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter =
            missionListContent.GetComponent<
                ContentSizeFitter
            >();

        if (fitter == null)
        {
            fitter =
                missionListContent.gameObject
                    .AddComponent<ContentSizeFitter>();
        }

        fitter.horizontalFit =
            ContentSizeFitter.FitMode.Unconstrained;

        fitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
    }

    private void PrepareMissionButtonLayout(
        StageExpeditionMissionButtonUI item)
    {
        if (item == null)
        {
            return;
        }

        LayoutElement layout =
            item.GetComponent<LayoutElement>();

        if (layout == null)
        {
            layout =
                item.gameObject.AddComponent<
                    LayoutElement
                >();
        }

        float height =
            Mathf.Max(
                1f,
                missionButtonPreferredHeight
            );

        layout.minHeight = height;
        layout.preferredHeight = height;
    }

    private void ForceRebuildMissionListLayout()
    {
        RectTransform rect =
            missionListContent as RectTransform;

        if (rect == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(
            rect
        );
    }

    // ---------------------------------------------------------------------
    // 出撃時のミッション自動受注
    // ---------------------------------------------------------------------

    private void AcceptCurrentStageMissions()
    {
        if (selectedStageData == null ||
            currentStageMissions.Count <= 0)
        {
            return;
        }

        GameSessionManager session =
            GameSessionManager.Instance;

        if (session == null)
        {
            session =
                FindAnyObjectByType<GameSessionManager>(
                    FindObjectsInactive.Include
                );
        }

        if (session == null)
        {
            Debug.LogWarning(
                "[StageExpeditionPanelController] " +
                "GameSessionManagerが見つからないため、" +
                "開催中ミッションを自動受注できませんでした。",
                this
            );
            return;
        }

        session.BeginCurrentExpedition(
            selectedStageData.StageId
        );

        bool trackedMissionAssigned = false;
        int acceptedCount = 0;
        int registeredForResultCount = 0;

        IReadOnlyList<MissionDefinition2D> missions =
            currentStageMissions;

        for (int i = 0; i < missions.Count; i++)
        {
            MissionDefinition2D mission = missions[i];

            if (mission == null)
            {
                continue;
            }

            bool shouldTrack =
                trackFirstAcceptedMission &&
                !trackedMissionAssigned;

            bool accepted =
                session.AcceptMission(
                    mission,
                    shouldTrack,
                    MissionAcceptSource.Expedition,
                    out string resultMessage
                );

            if (accepted)
            {
                acceptedCount++;

                if (shouldTrack)
                {
                    trackedMissionAssigned = true;
                }

                if (session.RegisterCurrentExpeditionMission(mission))
                {
                    registeredForResultCount++;
                }

                if (showMissionDebugLogs)
                {
                    Debug.Log(
                        "[StageExpeditionPanelController] " +
                        resultMessage,
                        this
                    );
                }
            }
            else if (showMissionDebugLogs)
            {
                // 達成済みなどはGameSessionManager側が受注を拒否します。
                // 出撃自体は止めず、そのミッションだけスキップします。
                Debug.Log(
                    "[StageExpeditionPanelController] " +
                    $"自動受注スキップ：{resultMessage}",
                    this
                );
            }
        }

        if (showMissionDebugLogs)
        {
            Debug.Log(
                "[StageExpeditionPanelController] " +
                $"出撃時ミッション同期完了：" +
                $"{acceptedCount}件を受注状態にしました。" +
                $" Result対象={registeredForResultCount}件。" +
                $" Stage={selectedStageData.StageName}",
                this
            );
        }
    }

    private static string BuildStars(int value)
    {
        const int maxStars = 5;

        int clamped =
            Mathf.Clamp(value, 1, maxStars);

        return
            new string('★', clamped) +
            new string('☆', maxStars - clamped);
    }

    private void RefreshDepartButtonLabel()
    {
        if (!useSupplyPreparationScreen || !autoSetNextButtonText || departButton == null) return;
        string label = string.IsNullOrWhiteSpace(nextButtonLabel) ? "次へ" : nextButtonLabel;
        TMP_Text tmp = departButton.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) tmp.text = label;
        else
        {
            Text legacy = departButton.GetComponentInChildren<Text>(true);
            if (legacy != null) legacy.text = label;
        }
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (departButton != null)
        {
            departButton.onClick.AddListener(Depart);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        RegisterEntryPointButtonListeners();
        RefreshDepartButtonLabel();

        initialized = true;
    }

    private void OnValidate()
    {
        openAnimationDuration =
            Mathf.Max(0.01f, openAnimationDuration);

        openStartScale =
            Mathf.Clamp(openStartScale, 0.1f, 1f);

        openOvershoot =
            Mathf.Clamp(openOvershoot, 0f, 3f);

        openSoundVolume =
            Mathf.Clamp01(openSoundVolume);

        closeSoundVolume = Mathf.Clamp01(closeSoundVolume);
        departSoundVolume = Mathf.Clamp01(departSoundVolume);
        departureDelaySeconds = Mathf.Max(0f, departureDelaySeconds);
        departureFadeDuration = Mathf.Max(0.01f, departureFadeDuration);

        missionButtonPreferredHeight =
            Mathf.Max(1f, missionButtonPreferredHeight);

        missionButtonSpacing =
            Mathf.Max(0f, missionButtonSpacing);

        missionListPadding =
            Mathf.Max(0f, missionListPadding);

        if (string.IsNullOrWhiteSpace(noMissionTitle))
        {
            noMissionTitle = "開催中ミッションなし";
        }

        if (noMissionDescription == null)
        {
            noMissionDescription = string.Empty;
        }
    }
}
