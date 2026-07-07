using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Mキーなどで開くミッションメニューです。
/// 
/// ・一覧からミッションを選択して詳細を表示します。
/// ・未開始ミッションは「受注する」、進行中ミッションは「追跡する」を押せます。
/// ・Contentの縦レイアウト・ボタン参照・クリックを妨げるDetailPanel背景を自動補助します。
/// </summary>
[DisallowMultipleComponent]
public class MissionMenuUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private MissionManager2D missionManager;

    [Tooltip("MissionMenuUI自身は常に有効にして、表示・非表示にするパネルを別Objectで設定するのがおすすめです")]
    [SerializeField] private GameObject missionMenuPanel;

    [Tooltip("Scroll View の Content を設定します。通常は MissionScrollView > Viewport > Content です")]
    [SerializeField] private Transform missionListContent;

    [Tooltip("MissionListItemUIを付けた一覧用Prefabを設定します")]
    [SerializeField] private MissionListItemUI missionListItemPrefab;

    [Header("詳細パネル")]
    [Tooltip("一覧を選ぶまでは非表示にするDetailPanel本体を設定します")]
    [SerializeField] private GameObject detailPanel;

    [SerializeField] private TMP_Text missionTitleText;
    [SerializeField] private TMP_Text missionDescriptionText;
    [SerializeField] private TMP_Text missionProgressText;
    [SerializeField] private TMP_Text missionStatusText;

    [SerializeField] private Button trackButton;
    [SerializeField] private TMP_Text trackButtonText;
    [SerializeField] private Button closeButton;

    [Header("操作")]
    [SerializeField] private KeyCode toggleKey = KeyCode.M;
    [SerializeField] private bool closeWithEscape = true;

    [Tooltip("メニューを開き直した時、前回の一覧選択を残すかどうか")]
    [SerializeField] private bool keepSelectionWhenReopening;

    [Header("受注・追跡")]
    [Tooltip("未開始ミッションを、DetailPanelのボタンから受注できるようにします")]
    [SerializeField] private bool acceptInactiveMissionsFromMenu = true;

    [SerializeField] private string acceptLabel = "受注する";
    [SerializeField] private string trackLabel = "追跡する";
    [SerializeField] private string trackingLabel = "追跡中";
    [SerializeField] private string completedActionLabel = "達成済み";
    [SerializeField] private string cannotTrackLabel = "追跡不可";

    [Header("一覧レイアウトの自動補助")]
    [Tooltip("ContentにVertical Layout GroupとContent Size Fitterが無い時、実行中に自動追加します")]
    [SerializeField] private bool autoConfigureMissionListLayout = true;

    [SerializeField, Min(1f)] private float listItemPreferredHeight = 85f;
    [SerializeField, Min(0f)] private float listItemSpacing = 8f;
    [SerializeField, Min(0f)] private float listPadding = 8f;

    [Header("メニュー表示中の操作制限")]
    [SerializeField] private bool lockPlayerMovementWhileOpen = true;
    [SerializeField] private PlayerMove playerMove;

    [SerializeField] private bool lockWeaponControlsWhileOpen = true;
    [SerializeField]
    private PlayerEquipmentVisualController equipmentVisualController;

    [Tooltip("石投げやロープ登りなど、メニュー中に停止したい任意のスクリプトを設定します")]
    [SerializeField] private Behaviour[] behavioursToDisableWhileOpen;

    [Header("表示文")]
    [SerializeField] private string collectFormat = "{0}  {1} / {2}";
    [SerializeField] private string defeatFormat = "討伐  {0} / {1}";
    [SerializeField] private string inactiveLabel = "未開始";
    [SerializeField] private string inProgressLabel = "進行中";
    [SerializeField] private string completedLabel = "達成済み";

    [Header("開始設定")]
    [SerializeField] private bool openOnStart;

    [Header("デバッグ")]
    [Tooltip("一覧構築・受注・追跡・閉じるボタンの状態をConsoleへ出します")]
    [SerializeField] private bool showDebugLogs = true;

    public bool IsOpen => isOpen;
    public int SelectedMissionIndex => selectedMissionIndex;

    private readonly List<MissionListItemUI> listItems =
        new List<MissionListItemUI>();

    private readonly List<int> listItemMissionIndices =
        new List<int>();

    private readonly List<Behaviour> disabledBehaviours =
        new List<Behaviour>();

    private bool isOpen;
    private bool isSubscribed;
    private bool refreshRequested;
    private int selectedMissionIndex = -1;

    private bool hasDisabledPlayerMove;
    private bool playerMoveWasEnabledBeforeOpen;
    private bool hasLockedWeaponControls;

    private bool hasSavedCursorState;
    private bool cursorWasVisible;
    private CursorLockMode cursorLockModeBeforeOpen;

    private CanvasGroup selfPanelCanvasGroup;
    private int lastLoggedMissionCount = -1;
    private int lastLoggedValidMissionCount = -1;

    private void Awake()
    {
        FindReferences();
        SetupButtons();
        ConfigureDetailPanelRaycasts();
        SetPanelVisible(false);
        SetDetailPanelVisible(false);
    }

    private void OnEnable()
    {
        FindReferences();
        SetupButtons();
        ConfigureDetailPanelRaycasts();
        SubscribeEvents();
        RequestRefresh();
    }

    private void Start()
    {
        if (openOnStart)
        {
            OpenMenu();
        }
        else
        {
            SetPanelVisible(false);
            SetDetailPanelVisible(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            ToggleMenu();
        }

        if (isOpen && closeWithEscape && Input.GetKeyDown(KeyCode.Escape))
        {
            CloseMenu();
        }

        if (refreshRequested)
        {
            refreshRequested = false;
            RefreshUI();
        }
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        RestorePlayerControls();
        RestoreCursorState();
        isOpen = false;
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
    }

    public void ToggleMenu()
    {
        if (isOpen)
        {
            CloseMenu();
        }
        else
        {
            OpenMenu();
        }
    }

    public void OpenMenu()
    {
        FindReferences();
        SetupButtons();
        ConfigureDetailPanelRaycasts();

        if (isOpen)
        {
            RequestRefresh();
            return;
        }

        if (!keepSelectionWhenReopening)
        {
            selectedMissionIndex = -1;
        }
        else
        {
            ClearSelectionIfInvalid();
        }

        isOpen = true;
        SetPanelVisible(true);
        SaveAndShowCursor();
        LockPlayerControls();
        LogMenuDiagnostics();
        RefreshUI();

        Log("ミッションメニューを開きました。");
    }

    public void CloseMenu()
    {
        Log("閉じる処理が呼ばれました。");

        if (!isOpen)
        {
            SetPanelVisible(false);
            SetDetailPanelVisible(false);
            return;
        }

        isOpen = false;
        SetPanelVisible(false);
        SetDetailPanelVisible(false);
        RestorePlayerControls();
        RestoreCursorState();
    }

    public void SelectMission(int missionIndex)
    {
        if (missionManager == null ||
            missionManager.GetMissionDefinition(missionIndex) == null)
        {
            LogWarning($"一覧選択に失敗しました。Mission Index={missionIndex}");
            return;
        }

        selectedMissionIndex = missionIndex;
        Log($"一覧を選択しました。Mission Index={missionIndex}");
        RefreshUI();
    }

    /// <summary>
    /// 未開始なら受注して追跡、進行中なら追跡先だけを切り替えます。
    /// 既存のTrackButtonのOnClickにもそのまま使えます。
    /// </summary>
    public void TrackSelectedMission()
    {
        if (missionManager == null || selectedMissionIndex < 0)
        {
            LogWarning("操作できません。ミッションが選択されていません。");
            return;
        }

        MissionDefinition2D mission =
            missionManager.GetMissionDefinition(selectedMissionIndex);

        if (mission == null)
        {
            LogWarning("操作できません。選択中のMission Definitionがありません。");
            return;
        }

        MissionProgressState2D state =
            missionManager.GetMissionState(selectedMissionIndex);

        if (state == MissionProgressState2D.Inactive)
        {
            if (!acceptInactiveMissionsFromMenu)
            {
                LogWarning(
                    "このメニューからの受注が無効です。" +
                    "Accept Inactive Missions From MenuをONにしてください。"
                );
                return;
            }

            if (!missionManager.StartMission(selectedMissionIndex))
            {
                LogWarning($"ミッション受注に失敗しました：{mission.DisplayName}");
                return;
            }

            missionManager.SetTrackedMission(selectedMissionIndex);
            Log($"ミッションを受注しました：{mission.DisplayName}");
        }
        else if (state == MissionProgressState2D.InProgress)
        {
            if (missionManager.IsTrackedMission(selectedMissionIndex))
            {
                Log($"すでに追跡中です：{mission.DisplayName}");
                RefreshUI();
                return;
            }

            if (!missionManager.SetTrackedMission(selectedMissionIndex))
            {
                LogWarning("追跡ミッションの切替に失敗しました。");
                return;
            }

            Log($"追跡対象を切り替えました：{mission.DisplayName}");
        }
        else
        {
            LogWarning($"達成済みミッションは受注・追跡できません：{mission.DisplayName}");
            RefreshUI();
            return;
        }

        Transform compassTarget = missionManager.TrackedCompassTarget;

        if (compassTarget == null)
        {
            LogWarning(
                "ミッション状態は切り替わりましたが、Compass Targetがありません。" +
                "MissionManager2D > Missions > 該当Missionの Compass Target、" +
                "または Target Enemy を設定してください。"
            );
        }
        else
        {
            Log($"コンパス対象={compassTarget.name}");
        }

        RefreshUI();
    }

    public void RefreshUI()
    {
        if (!isOpen)
        {
            return;
        }

        FindReferences();
        ClearSelectionIfInvalid();
        RefreshMissionList();
        RefreshDetailPanel();
    }

    [ContextMenu("Log Mission Menu Diagnostics")]
    public void LogMissionMenuDiagnosticsFromContextMenu()
    {
        FindReferences();
        LogMenuDiagnostics();
    }

    private void RequestRefresh()
    {
        refreshRequested = true;
    }

    private void RefreshMissionList()
    {
        if (missionManager == null)
        {
            LogWarning("MissionManager2D が見つかりません。ミッション一覧を作れません。");
            return;
        }

        if (missionListContent == null)
        {
            LogWarning(
                "Mission List Content が未設定です。" +
                "MissionScrollView > Viewport > Content を設定してください。"
            );
            return;
        }

        if (missionListItemPrefab == null)
        {
            LogWarning("Mission List Item Prefab が未設定です。");
            return;
        }

        EnsureMissionListLayout();

        List<int> validMissionIndices = new List<int>();
        int invalidMissionCount = 0;

        for (int i = 0; i < missionManager.MissionCount; i++)
        {
            MissionDefinition2D mission =
                missionManager.GetMissionDefinition(i);

            if (mission == null)
            {
                invalidMissionCount++;
                continue;
            }

            validMissionIndices.Add(i);
        }

        if (missionManager.MissionCount != lastLoggedMissionCount ||
            validMissionIndices.Count != lastLoggedValidMissionCount)
        {
            Log(
                $"一覧確認：MissionManager登録={missionManager.MissionCount}件 / " +
                $"表示可能={validMissionIndices.Count}件 / " +
                $"Mission未設定={invalidMissionCount}件 / " +
                $"Content={GetHierarchyPath(missionListContent)}"
            );

            if (invalidMissionCount > 0)
            {
                LogWarning(
                    "MissionManager2DのMissions内に、Mission Definitionが未設定のEntryがあります。" +
                    "未設定Entryは一覧に表示されません。"
                );
            }

            lastLoggedMissionCount = missionManager.MissionCount;
            lastLoggedValidMissionCount = validMissionIndices.Count;
        }

        if (!DoesListMatch(validMissionIndices))
        {
            RebuildMissionList(validMissionIndices);
        }

        for (int i = 0; i < listItems.Count; i++)
        {
            MissionListItemUI item = listItems[i];
            int missionIndex = listItemMissionIndices[i];

            if (item == null)
            {
                continue;
            }

            MissionDefinition2D mission =
                missionManager.GetMissionDefinition(missionIndex);

            item.Setup(
                missionIndex,
                mission,
                missionManager.GetMissionState(missionIndex),
                missionManager.GetMissionProgress(missionIndex),
                missionManager.GetMissionRequiredAmount(missionIndex),
                missionIndex == selectedMissionIndex,
                missionManager.IsTrackedMission(missionIndex)
            );
        }

        ForceRebuildListLayout();
    }

    private bool DoesListMatch(List<int> validMissionIndices)
    {
        if (listItems.Count != validMissionIndices.Count ||
            listItemMissionIndices.Count != validMissionIndices.Count)
        {
            return false;
        }

        for (int i = 0; i < validMissionIndices.Count; i++)
        {
            if (listItems[i] == null ||
                listItemMissionIndices[i] != validMissionIndices[i])
            {
                return false;
            }
        }

        return true;
    }

    private void RebuildMissionList(List<int> validMissionIndices)
    {
        ClearMissionList();

        foreach (int missionIndex in validMissionIndices)
        {
            MissionListItemUI item = Instantiate(
                missionListItemPrefab,
                missionListContent
            );

            item.name = $"MissionListItem_{missionIndex}";
            item.Clicked += HandleListItemClicked;

            PrepareListItemLayout(item);

            listItems.Add(item);
            listItemMissionIndices.Add(missionIndex);
        }

        Log(
            $"ミッション一覧を再構築しました。生成数={listItems.Count} / " +
            $"親={GetHierarchyPath(missionListContent)}"
        );
    }

    private void ClearMissionList()
    {
        foreach (MissionListItemUI item in listItems)
        {
            if (item == null)
            {
                continue;
            }

            item.Clicked -= HandleListItemClicked;
            Destroy(item.gameObject);
        }

        listItems.Clear();
        listItemMissionIndices.Clear();
    }

    private void HandleListItemClicked(MissionListItemUI item)
    {
        if (item == null)
        {
            return;
        }

        Log($"MissionListItemのクリックを受信しました。Index={item.MissionIndex}");
        SelectMission(item.MissionIndex);
    }

    private void RefreshDetailPanel()
    {
        MissionDefinition2D mission = missionManager != null &&
            selectedMissionIndex >= 0
            ? missionManager.GetMissionDefinition(selectedMissionIndex)
            : null;

        bool hasSelection = mission != null;
        SetDetailPanelVisible(hasSelection);

        if (!hasSelection)
        {
            return;
        }

        MissionProgressState2D state =
            missionManager.GetMissionState(selectedMissionIndex);

        int progress = missionManager.GetMissionProgress(selectedMissionIndex);
        int required = missionManager.GetMissionRequiredAmount(selectedMissionIndex);

        bool isTracked = missionManager.IsTrackedMission(selectedMissionIndex);

        SetText(missionTitleText, mission.DisplayName);
        SetText(missionDescriptionText, mission.Description);
        SetText(missionProgressText, BuildProgressText(mission, progress, required));
        SetText(missionStatusText, BuildStatusText(state));

        bool canUseActionButton;
        string actionLabel;

        switch (state)
        {
            case MissionProgressState2D.Inactive:
                canUseActionButton = acceptInactiveMissionsFromMenu;
                actionLabel = canUseActionButton
                    ? acceptLabel
                    : cannotTrackLabel;
                break;

            case MissionProgressState2D.InProgress:
                canUseActionButton = !isTracked;
                actionLabel = isTracked
                    ? trackingLabel
                    : trackLabel;
                break;

            default:
                canUseActionButton = false;
                actionLabel = completedActionLabel;
                break;
        }

        if (trackButton != null)
        {
            trackButton.interactable = canUseActionButton;
        }

        SetText(trackButtonText, actionLabel);
    }

    private string BuildProgressText(
        MissionDefinition2D mission,
        int progress,
        int required)
    {
        if (mission == null)
        {
            return string.Empty;
        }

        if (mission.ObjectiveType == MissionObjectiveType2D.CollectItem)
        {
            string itemName = mission.RequiredItem != null
                ? mission.RequiredItem.DisplayName
                : "アイテム";

            return string.Format(
                collectFormat,
                itemName,
                Mathf.Max(0, progress),
                Mathf.Max(0, required)
            );
        }

        return string.Format(
            defeatFormat,
            Mathf.Max(0, progress),
            Mathf.Max(0, required)
        );
    }

    private string BuildStatusText(MissionProgressState2D state)
    {
        switch (state)
        {
            case MissionProgressState2D.InProgress:
                return inProgressLabel;

            case MissionProgressState2D.Completed:
                return completedLabel;

            default:
                return inactiveLabel;
        }
    }

    private void ClearSelectionIfInvalid()
    {
        if (selectedMissionIndex < 0)
        {
            return;
        }

        if (missionManager == null ||
            missionManager.GetMissionDefinition(selectedMissionIndex) == null)
        {
            selectedMissionIndex = -1;
        }
    }

    private void HandleMissionStateChanged()
    {
        RequestRefresh();
    }

    private void HandleTrackedMissionChanged(MissionDefinition2D mission)
    {
        RequestRefresh();
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || missionManager == null)
        {
            return;
        }

        missionManager.MissionStateChanged += HandleMissionStateChanged;
        missionManager.TrackedMissionChanged += HandleTrackedMissionChanged;
        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || missionManager == null)
        {
            return;
        }

        missionManager.MissionStateChanged -= HandleMissionStateChanged;
        missionManager.TrackedMissionChanged -= HandleTrackedMissionChanged;
        isSubscribed = false;
    }

    private void SetupButtons()
    {
        if (trackButton != null)
        {
            trackButton.onClick.RemoveListener(TrackSelectedMission);
            trackButton.onClick.AddListener(TrackSelectedMission);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseMenu);
            closeButton.onClick.AddListener(CloseMenu);
        }
    }

    private void RemoveButtonListeners()
    {
        if (trackButton != null)
        {
            trackButton.onClick.RemoveListener(TrackSelectedMission);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseMenu);
        }
    }

    private void LockPlayerControls()
    {
        if (lockPlayerMovementWhileOpen && playerMove != null &&
            !hasDisabledPlayerMove)
        {
            playerMoveWasEnabledBeforeOpen = playerMove.enabled;
            hasDisabledPlayerMove = true;
            playerMove.enabled = false;
        }

        if (lockWeaponControlsWhileOpen && equipmentVisualController != null &&
            !hasLockedWeaponControls)
        {
            equipmentVisualController.SetWeaponControlLock(this, true);
            hasLockedWeaponControls = true;
        }

        foreach (Behaviour behaviour in behavioursToDisableWhileOpen)
        {
            if (behaviour == null || behaviour == this || !behaviour.enabled)
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
            if (playerMove != null && playerMoveWasEnabledBeforeOpen)
            {
                playerMove.enabled = true;
            }

            playerMoveWasEnabledBeforeOpen = false;
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
            cursorLockModeBeforeOpen = Cursor.lockState;
        }

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    private void RestoreCursorState()
    {
        if (!hasSavedCursorState)
        {
            return;
        }

        Cursor.visible = cursorWasVisible;
        Cursor.lockState = cursorLockModeBeforeOpen;
        hasSavedCursorState = false;
    }

    private void SetPanelVisible(bool visible)
    {
        if (missionMenuPanel == null)
        {
            return;
        }

        if (missionMenuPanel == gameObject)
        {
            if (selfPanelCanvasGroup == null)
            {
                selfPanelCanvasGroup = GetComponent<CanvasGroup>();

                if (selfPanelCanvasGroup == null)
                {
                    selfPanelCanvasGroup = gameObject.AddComponent<CanvasGroup>();
                }
            }

            selfPanelCanvasGroup.alpha = visible ? 1f : 0f;
            selfPanelCanvasGroup.interactable = visible;
            selfPanelCanvasGroup.blocksRaycasts = visible;
            return;
        }

        if (missionMenuPanel.activeSelf != visible)
        {
            missionMenuPanel.SetActive(visible);
        }
    }

    private void SetDetailPanelVisible(bool visible)
    {
        if (detailPanel != null && detailPanel.activeSelf != visible)
        {
            detailPanel.SetActive(visible);
        }
    }

    private void FindReferences()
    {
        if (missionManager == null)
        {
            missionManager = FindAnyObjectByType<MissionManager2D>();
        }

        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>();
        }

        if (equipmentVisualController == null)
        {
            equipmentVisualController =
                FindAnyObjectByType<PlayerEquipmentVisualController>();
        }

        FindUiReferences();
    }

    private void FindUiReferences()
    {
        if (missionMenuPanel == null)
        {
            if (gameObject.name == "MissionMenuPanel")
            {
                missionMenuPanel = gameObject;
            }
            else
            {
                Transform panelTransform = FindChildRecursive(
                    transform,
                    "MissionMenuPanel"
                );

                if (panelTransform != null)
                {
                    missionMenuPanel = panelTransform.gameObject;
                }
            }
        }

        // MissionMenuUIをManager側へ付けた場合でも、Canvas内の
        // MissionMenuPanelを名前から補助検索する。
        if (missionMenuPanel == null)
        {
            RectTransform[] allRectTransforms =
                FindObjectsByType<RectTransform>(
                    FindObjectsInactive.Include
                );

            foreach (RectTransform rectTransform in allRectTransforms)
            {
                if (rectTransform != null &&
                    rectTransform.name == "MissionMenuPanel")
                {
                    missionMenuPanel = rectTransform.gameObject;
                    break;
                }
            }
        }

        if (missionMenuPanel == null)
        {
            return;
        }

        Transform panelTransformRoot = missionMenuPanel.transform;

        if (detailPanel == null)
        {
            Transform detailTransform = FindChildRecursive(
                panelTransformRoot,
                "DetailPanel"
            );

            if (detailTransform != null)
            {
                detailPanel = detailTransform.gameObject;
            }
        }

        if (missionListContent == null ||
            missionListContent == panelTransformRoot)
        {
            Transform scrollView = FindChildRecursive(
                panelTransformRoot,
                "MissionScrollView"
            );

            Transform discoveredContent = scrollView != null
                ? scrollView.Find("Viewport/Content")
                : null;

            if (discoveredContent != null)
            {
                if (missionListContent == panelTransformRoot)
                {
                    LogWarning(
                        "Mission List ContentがMissionMenuPanelになっていました。" +
                        "自動で MissionScrollView > Viewport > Content に修正しました。"
                    );
                }

                missionListContent = discoveredContent;
            }
        }

        if (closeButton == null)
        {
            Transform closeTransform = FindChildRecursive(
                panelTransformRoot,
                "CloseButton"
            );

            if (closeTransform != null)
            {
                closeButton = closeTransform.GetComponent<Button>();
            }
        }

        if (detailPanel == null)
        {
            return;
        }

        Transform detailTransformRoot = detailPanel.transform;

        if (trackButton == null)
        {
            Transform trackTransform = FindChildRecursive(
                detailTransformRoot,
                "TrackButton"
            );

            if (trackTransform != null)
            {
                trackButton = trackTransform.GetComponent<Button>();
            }
        }

        if (trackButtonText == null && trackButton != null)
        {
            trackButtonText = trackButton.GetComponentInChildren<TMP_Text>(true);
        }

        if (missionTitleText == null)
        {
            missionTitleText = FindTextByObjectName(
                detailTransformRoot,
                "MissionTitleText"
            );
        }

        if (missionDescriptionText == null)
        {
            missionDescriptionText = FindTextByObjectName(
                detailTransformRoot,
                "DescriptionText"
            );
        }

        if (missionProgressText == null)
        {
            missionProgressText = FindTextByObjectName(
                detailTransformRoot,
                "ProgressText"
            );
        }

        if (missionStatusText == null)
        {
            missionStatusText = FindTextByObjectName(
                detailTransformRoot,
                "StatusText"
            );
        }
    }

    private void EnsureMissionListLayout()
    {
        if (!autoConfigureMissionListLayout || missionListContent == null)
        {
            return;
        }

        VerticalLayoutGroup layoutGroup =
            missionListContent.GetComponent<VerticalLayoutGroup>();

        if (layoutGroup == null)
        {
            layoutGroup = missionListContent.gameObject
                .AddComponent<VerticalLayoutGroup>();

            Log("Mission List ContentにVertical Layout Groupを自動追加しました。");
        }

        layoutGroup.childAlignment = TextAnchor.UpperCenter;
        layoutGroup.padding = new RectOffset(
            Mathf.RoundToInt(listPadding),
            Mathf.RoundToInt(listPadding),
            Mathf.RoundToInt(listPadding),
            Mathf.RoundToInt(listPadding)
        );
        layoutGroup.spacing = listItemSpacing;
        layoutGroup.childControlWidth = true;
        layoutGroup.childControlHeight = false;
        layoutGroup.childForceExpandWidth = true;
        layoutGroup.childForceExpandHeight = false;

        ContentSizeFitter sizeFitter =
            missionListContent.GetComponent<ContentSizeFitter>();

        if (sizeFitter == null)
        {
            sizeFitter = missionListContent.gameObject
                .AddComponent<ContentSizeFitter>();

            Log("Mission List ContentにContent Size Fitterを自動追加しました。");
        }

        sizeFitter.horizontalFit =
            ContentSizeFitter.FitMode.Unconstrained;
        sizeFitter.verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;
    }

    private void PrepareListItemLayout(MissionListItemUI item)
    {
        if (item == null)
        {
            return;
        }

        LayoutElement layoutElement =
            item.GetComponent<LayoutElement>();

        if (layoutElement == null)
        {
            layoutElement = item.gameObject.AddComponent<LayoutElement>();
        }

        if (layoutElement.preferredHeight <= 0f)
        {
            layoutElement.preferredHeight = listItemPreferredHeight;
        }

        if (layoutElement.minHeight <= 0f)
        {
            layoutElement.minHeight = listItemPreferredHeight;
        }
    }

    private void ForceRebuildListLayout()
    {
        RectTransform rectTransform =
            missionListContent as RectTransform;

        if (rectTransform == null)
        {
            return;
        }

        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
    }

    private void ConfigureDetailPanelRaycasts()
    {
        if (detailPanel == null)
        {
            return;
        }

        Graphic[] graphics = detailPanel.GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic == null)
            {
                continue;
            }

            // ButtonやScrollbarそのものはクリックできるように残す。
            if (graphic.GetComponent<Selectable>() != null)
            {
                continue;
            }

            // DetailPanelの背景やTextが、CloseButton・一覧のクリックを
            // 横取りしないようにする。
            graphic.raycastTarget = false;
        }
    }

    private void LogMenuDiagnostics()
    {
        if (!showDebugLogs)
        {
            return;
        }

        Log("--- Mission Menu Diagnostics ---");
        Log($"MissionManager={GetObjectName(missionManager)}");
        Log($"MissionMenuPanel={GetObjectName(missionMenuPanel)}");
        Log($"MissionListContent={GetHierarchyPath(missionListContent)}");
        Log($"MissionListItemPrefab={GetObjectName(missionListItemPrefab)}");
        Log($"DetailPanel={GetObjectName(detailPanel)}");
        Log($"CloseButton={GetButtonState(closeButton)}");
        Log($"TrackButton={GetButtonState(trackButton)}");
        Log($"EventSystem={(EventSystem.current != null ? EventSystem.current.name : "なし")}");

        if (missionManager != null)
        {
            int validCount = 0;

            for (int i = 0; i < missionManager.MissionCount; i++)
            {
                MissionDefinition2D mission =
                    missionManager.GetMissionDefinition(i);

                if (mission != null)
                {
                    validCount++;
                    Log(
                        $"Mission[{i}]={mission.DisplayName} / " +
                        $"状態={missionManager.GetMissionState(i)}"
                    );
                }
                else
                {
                    LogWarning($"Mission[{i}] はMission Definition未設定です。");
                }
            }

            Log($"MissionCount={missionManager.MissionCount} / 有効={validCount}");
        }

        WarnIfUiCannotReceiveClicks();
    }

    private void WarnIfUiCannotReceiveClicks()
    {
        if (EventSystem.current == null)
        {
            LogWarning(
                "EventSystem がシーンにありません。UIボタンはクリックできません。" +
                "Hierarchyで UI > Event System を作成してください。"
            );
        }

        Canvas canvas = missionMenuPanel != null
            ? missionMenuPanel.GetComponentInParent<Canvas>(true)
            : GetComponentInParent<Canvas>(true);

        if (canvas != null && canvas.GetComponent<GraphicRaycaster>() == null)
        {
            LogWarning(
                "CanvasにGraphic Raycasterがありません。UIボタンはクリックできません。"
            );
        }

        if (missionListContent == null)
        {
            LogWarning(
                "Mission List Contentが未設定です。" +
                "MissionScrollView > Viewport > Content を入れてください。"
            );
        }

        if (missionListItemPrefab == null)
        {
            LogWarning("Mission List Item Prefabが未設定です。");
        }

        if (closeButton == null)
        {
            LogWarning(
                "Close Buttonが未設定です。CloseButtonのButtonコンポーネントを設定してください。"
            );
        }

        if (trackButton == null)
        {
            LogWarning(
                "Track Buttonが未設定です。DetailPanel内のTrackButtonを設定してください。"
            );
        }

        if (missionMenuPanel != null &&
            missionListContent == missionMenuPanel.transform)
        {
            LogWarning(
                "Mission List ContentがMissionMenuPanel本体になっています。" +
                "MissionScrollView > Viewport > Contentへ変更してください。"
            );
        }
    }

    private static Transform FindChildRecursive(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == objectName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    private static TMP_Text FindTextByObjectName(
        Transform root,
        string objectName)
    {
        if (root == null)
        {
            return null;
        }

        TMP_Text[] texts = root.GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null && text.gameObject.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private static string GetObjectName(Object target)
    {
        return target != null ? target.name : "未設定";
    }

    private static string GetButtonState(Button button)
    {
        if (button == null)
        {
            return "未設定";
        }

        return $"{button.name} / active={button.gameObject.activeInHierarchy} / interactable={button.interactable}";
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null)
        {
            return "未設定";
        }

        List<string> names = new List<string>();
        Transform current = target;

        while (current != null)
        {
            names.Add(current.name);
            current = current.parent;
        }

        names.Reverse();
        return string.Join(" > ", names);
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[MissionMenuUI] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[MissionMenuUI] {message}", this);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }

    private void OnValidate()
    {
        listItemPreferredHeight = Mathf.Max(1f, listItemPreferredHeight);
        listItemSpacing = Mathf.Max(0f, listItemSpacing);
        listPadding = Mathf.Max(0f, listPadding);
    }
}
