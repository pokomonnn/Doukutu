using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// キャンプ中の「何時間眠るか」をSliderで選ぶUIです。
/// SleepTimePanelに付けて使います。
/// </summary>
[DisallowMultipleComponent]
public class CampSleepTimeSelectorUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CampModeController campModeController;
    [SerializeField] private Slider hoursSlider;
    [SerializeField] private TMP_Text hoursText;
    [SerializeField] private Button confirmButton;
    [SerializeField] private Button cancelButton;

    [Header("表示")]
    [Tooltip("未設定なら、このGameObject自身を表示・非表示します")]
    [SerializeField] private GameObject selectionRoot;

    [Tooltip("例：{0}時間眠る。{0}の場所に選択中の時間が入ります")]
    [SerializeField] private string hoursTextFormat = "{0}時間眠る";

    private bool isSubscribed;

    private void Awake()
    {
        FindReferences();
        ConfigureSlider();
        SubscribeButtons();
        HideSelectionImmediately();
    }

    private void OnEnable()
    {
        FindReferences();
        ConfigureSlider();
        SubscribeSlider();
        RefreshText();
    }

    private void OnDisable()
    {
        UnsubscribeSlider();
    }

    private void OnDestroy()
    {
        UnsubscribeButtons();
    }

    /// <summary>
    /// CampModeControllerから呼ばれます。
    /// </summary>
    public void ShowSelection()
    {
        FindReferences();
        ConfigureSlider();
        SetSelectedHours(GetDefaultHours());
        SetSelectionVisible(true);
        RefreshText();
    }

    public void HideSelection()
    {
        SetSelectionVisible(false);
    }

    public void HideSelectionImmediately()
    {
        SetSelectionVisible(false);
    }

    public void ConfirmSleep()
    {
        if (campModeController == null)
        {
            return;
        }

        campModeController.SleepForHours(GetSelectedHours());
    }

    public void CancelSleep()
    {
        if (campModeController != null)
        {
            campModeController.CloseSleepTimeSelection();
            return;
        }

        HideSelection();
    }

    private void HandleHoursChanged(float value)
    {
        RefreshText();
    }

    private void ConfigureSlider()
    {
        if (hoursSlider == null)
        {
            return;
        }

        int minHours = Mathf.CeilToInt(GetMinimumHours());
        int maxHours = Mathf.FloorToInt(GetMaximumHours());

        maxHours = Mathf.Max(minHours, maxHours);

        hoursSlider.wholeNumbers = true;
        hoursSlider.minValue = minHours;
        hoursSlider.maxValue = maxHours;
    }

    private void SetSelectedHours(float hours)
    {
        if (hoursSlider == null)
        {
            return;
        }

        float clampedHours = Mathf.Clamp(
            Mathf.Round(hours),
            hoursSlider.minValue,
            hoursSlider.maxValue
        );

        hoursSlider.SetValueWithoutNotify(clampedHours);
    }

    private int GetSelectedHours()
    {
        if (hoursSlider == null)
        {
            return Mathf.RoundToInt(GetDefaultHours());
        }

        return Mathf.RoundToInt(hoursSlider.value);
    }

    private void RefreshText()
    {
        if (hoursText == null)
        {
            return;
        }

        int selectedHours = GetSelectedHours();
        hoursText.text = string.Format(
            hoursTextFormat,
            selectedHours
        );
    }

    private void SetSelectionVisible(bool visible)
    {
        GameObject root = GetSelectionRoot();

        if (root != null && root.activeSelf != visible)
        {
            root.SetActive(visible);
        }
    }

    private GameObject GetSelectionRoot()
    {
        return selectionRoot != null
            ? selectionRoot
            : gameObject;
    }

    private void SubscribeSlider()
    {
        if (isSubscribed || hoursSlider == null)
        {
            return;
        }

        hoursSlider.onValueChanged.AddListener(
            HandleHoursChanged
        );

        isSubscribed = true;
    }

    private void UnsubscribeSlider()
    {
        if (!isSubscribed || hoursSlider == null)
        {
            return;
        }

        hoursSlider.onValueChanged.RemoveListener(
            HandleHoursChanged
        );

        isSubscribed = false;
    }

    private void SubscribeButtons()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.AddListener(ConfirmSleep);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.AddListener(CancelSleep);
        }
    }

    private void UnsubscribeButtons()
    {
        if (confirmButton != null)
        {
            confirmButton.onClick.RemoveListener(ConfirmSleep);
        }

        if (cancelButton != null)
        {
            cancelButton.onClick.RemoveListener(CancelSleep);
        }
    }

    private void FindReferences()
    {
        if (campModeController == null)
        {
            campModeController =
                FindAnyObjectByType<CampModeController>(
                    FindObjectsInactive.Include
                );
        }

        if (hoursSlider == null)
        {
            hoursSlider = GetComponentInChildren<Slider>(true);
        }

        if (hoursText == null)
        {
            TMP_Text[] texts =
                GetComponentsInChildren<TMP_Text>(true);

            if (texts.Length > 0)
            {
                hoursText = texts[0];
            }
        }
    }

    private float GetMinimumHours()
    {
        return campModeController != null
            ? campModeController.MinimumSleepHours
            : 1f;
    }

    private float GetMaximumHours()
    {
        return campModeController != null
            ? campModeController.MaximumSleepHours
            : 12f;
    }

    private float GetDefaultHours()
    {
        return campModeController != null
            ? campModeController.DefaultSleepHours
            : 8f;
    }
}
