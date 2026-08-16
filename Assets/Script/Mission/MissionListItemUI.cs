using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MissionMenuUI の一覧へ並べる、ミッション1件分の表示です。
/// ボタンPrefabへ付けて使います。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class MissionListItemUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Button selectButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text statusText;

    [Tooltip("選択中の時だけ表示する任意のマークです")]
    [SerializeField] private GameObject selectedMarker;

    [Tooltip("コンパス追跡中の時だけ表示する任意のマークです")]
    [SerializeField] private GameObject trackedMarker;

    [Header("文言")]
    [SerializeField] private string collectFormat = "{0}  {1} / {2}";
    [SerializeField] private string deliverFormat = "納品 {0}  {1} / {2}";
    [SerializeField] private string defeatFormat = "討伐  {0} / {1}";
    [SerializeField] private string inactiveLabel = "未開始";
    [SerializeField] private string inProgressLabel = "進行中";
    [SerializeField] private string completedLabel = "達成済み";

    [Header("背景色")]
    [SerializeField] private bool tintBackground = true;
    [SerializeField]
    private Color normalBackgroundColor =
        new Color(0.15f, 0.15f, 0.18f, 0.9f);
    [SerializeField]
    private Color selectedBackgroundColor =
        new Color(0.16f, 0.36f, 0.58f, 0.95f);
    [SerializeField]
    private Color completedBackgroundColor =
        new Color(0.16f, 0.45f, 0.25f, 0.9f);

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public int MissionIndex { get; private set; } = -1;

    public event Action<MissionListItemUI> Clicked;

    private MissionDefinition2D mission;
    private MissionProgressState2D state;
    private bool isSelected;
    private bool isTracked;

    private void Awake()
    {
        FindReferences();
        ConfigureDecorativeRaycasts();

        if (selectButton != null)
        {
            selectButton.onClick.AddListener(HandleClicked);
        }
    }

    private void OnDestroy()
    {
        if (selectButton != null)
        {
            selectButton.onClick.RemoveListener(HandleClicked);
        }
    }

    /// <summary>
    /// MissionMenuUI から表示内容を更新します。
    /// </summary>
    public void Setup(
        int missionIndex,
        MissionDefinition2D missionDefinition,
        MissionProgressState2D missionState,
        int progress,
        int requiredAmount,
        bool selected,
        bool tracked)
    {
        FindReferences();

        MissionIndex = missionIndex;
        mission = missionDefinition;
        state = missionState;
        isSelected = selected;
        isTracked = tracked;

        if (titleText != null)
        {
            titleText.text = mission != null
                ? mission.DisplayName
                : "ミッション未設定";
        }

        if (progressText != null)
        {
            progressText.text = BuildProgressText(
                mission,
                progress,
                requiredAmount
            );
        }

        if (statusText != null)
        {
            statusText.text = BuildStatusText(missionState);
        }

        if (selectedMarker != null)
        {
            selectedMarker.SetActive(selected);
        }

        if (trackedMarker != null)
        {
            trackedMarker.SetActive(tracked);
        }

        ApplyBackgroundColor();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;

        if (selectedMarker != null)
        {
            selectedMarker.SetActive(selected);
        }

        ApplyBackgroundColor();
    }

    private string BuildProgressText(
        MissionDefinition2D missionDefinition,
        int progress,
        int requiredAmount)
    {
        if (missionDefinition == null)
        {
            return string.Empty;
        }

        int safeProgress = Mathf.Max(0, progress);
        int safeRequired = Mathf.Max(0, requiredAmount);

        if (missionDefinition.ObjectiveType ==
            MissionObjectiveType2D.CollectItem)
        {
            string itemName = missionDefinition.RequiredItem != null
                ? missionDefinition.RequiredItem.DisplayName
                : "アイテム";

            return string.Format(
                collectFormat,
                itemName,
                safeProgress,
                safeRequired
            );
        }

        if (missionDefinition.ObjectiveType ==
            MissionObjectiveType2D.DeliverItem)
        {
            string itemName = missionDefinition.RequiredItem != null
                ? missionDefinition.RequiredItem.DisplayName
                : "アイテム";

            return string.Format(
                deliverFormat,
                itemName,
                safeProgress,
                safeRequired
            );
        }

        return string.Format(
            defeatFormat,
            safeProgress,
            safeRequired
        );
    }

    private string BuildStatusText(MissionProgressState2D missionState)
    {
        switch (missionState)
        {
            case MissionProgressState2D.InProgress:
                return inProgressLabel;

            case MissionProgressState2D.Completed:
                return completedLabel;

            default:
                return inactiveLabel;
        }
    }

    private void ApplyBackgroundColor()
    {
        if (!tintBackground || backgroundImage == null)
        {
            return;
        }

        if (state == MissionProgressState2D.Completed)
        {
            backgroundImage.color = completedBackgroundColor;
            return;
        }

        backgroundImage.color = isSelected
            ? selectedBackgroundColor
            : normalBackgroundColor;
    }

    private void HandleClicked()
    {
        if (showDebugLogs)
        {
            Debug.Log(
                $"[MissionListItemUI] {name}: クリック。MissionIndex={MissionIndex}",
                this
            );
        }

        Clicked?.Invoke(this);
    }

    private void FindReferences()
    {
        if (selectButton == null)
        {
            selectButton = GetComponent<Button>();
        }

        if (backgroundImage == null)
        {
            backgroundImage = GetComponent<Image>();
        }
    }

    private void ConfigureDecorativeRaycasts()
    {
        Graphic[] graphics = GetComponentsInChildren<Graphic>(true);

        foreach (Graphic graphic in graphics)
        {
            if (graphic == null)
            {
                continue;
            }

            // Button本体のImageだけはクリック判定を残し、文字やマークは
            // ボタンのクリックを横取りしないようにする。
            if (graphic.GetComponent<Button>() != null)
            {
                continue;
            }

            graphic.raycastTarget = false;
        }
    }
}
