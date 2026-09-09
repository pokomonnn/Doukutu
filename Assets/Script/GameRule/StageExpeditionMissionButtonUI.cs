using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 出撃パネルの「開催中ミッション」1件分のButtonです。
/// StageExpeditionPanelControllerがPrefabから自動生成して使用します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class StageExpeditionMissionButtonUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Button button;
    [SerializeField] private TMP_Text titleText;

    [Tooltip("選択中のミッションだけ表示したい枠・矢印など。不要なら未設定でOKです。")]
    [SerializeField] private GameObject selectedMarker;

    [Header("Button表示")]
    [Tooltip(
        "ON：MissionDefinition2DのDisplayNameを表示します。\n" +
        "OFF：「ミッション 1」「ミッション 2」のように表示します。"
    )]
    [SerializeField] private bool showMissionDisplayName = true;

    [SerializeField] private string numberedMissionFormat = "ミッション {0}";

    public int MissionIndex { get; private set; } = -1;
    public MissionDefinition2D Mission { get; private set; }

    private Action<int> clickedCallback;

    private void Awake()
    {
        FindReferences();

        if (button != null)
        {
            button.onClick.AddListener(HandleClicked);
        }

        SetSelected(false);
    }

    private void OnDestroy()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(HandleClicked);
        }

        clickedCallback = null;
    }

    public void Setup(
        int missionIndex,
        MissionDefinition2D mission,
        Action<int> onClicked)
    {
        FindReferences();

        MissionIndex = missionIndex;
        Mission = mission;
        clickedCallback = onClicked;

        if (titleText != null)
        {
            if (showMissionDisplayName &&
                mission != null &&
                !string.IsNullOrWhiteSpace(mission.DisplayName))
            {
                titleText.text = mission.DisplayName;
            }
            else
            {
                titleText.text = string.Format(
                    numberedMissionFormat,
                    missionIndex + 1
                );
            }
        }

        SetSelected(false);
        SetInteractable(mission != null);
    }

    public void SetSelected(bool selected)
    {
        if (selectedMarker != null)
        {
            selectedMarker.SetActive(selected);
        }
    }

    public void SetInteractable(bool interactable)
    {
        FindReferences();

        if (button != null)
        {
            button.interactable = interactable;
        }
    }

    private void HandleClicked()
    {
        if (MissionIndex < 0 || Mission == null)
        {
            return;
        }

        clickedCallback?.Invoke(MissionIndex);
    }

    private void FindReferences()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        if (titleText == null)
        {
            titleText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(numberedMissionFormat))
        {
            numberedMissionFormat = "ミッション {0}";
        }

        if (button == null)
        {
            button = GetComponent<Button>();
        }
    }
}
