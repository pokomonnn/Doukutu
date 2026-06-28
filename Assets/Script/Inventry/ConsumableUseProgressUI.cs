using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class ConsumableUseProgressUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private PlayerWeightController playerWeightController;

    [SerializeField] private Slider progressSlider;

    [SerializeField] private TMP_Text labelText;

    [SerializeField] private TMP_Text remainingTimeText;

    [Header("表示設定")]
    [SerializeField] private bool showRemainingTime = true;

    [Header("翻訳")]
    [Tooltip("GameText の hud.healing などを設定")]
    [SerializeField]
    private LocalizedString useProgressLabel =
        new LocalizedString();

    [SerializeField]
    private string fallbackLabel = "回復中";

    private CanvasGroup canvasGroup;

    private string localizedLabel = "回復中";
    private bool isLabelSubscribed;

    private void Awake()
    {
        EnsureReferences();
        EnsureLocalizedString();
        SetupSlider();

        SetVisible(false);
    }

    private void OnEnable()
    {
        EnsureReferences();
        EnsureLocalizedString();
        SetupSlider();

        SubscribeLabel();
        RefreshVisual();
    }

    private void OnDisable()
    {
        UnsubscribeLabel();
    }

    private void Update()
    {
        RefreshVisual();
    }

    private void RefreshVisual()
    {
        FindPlayerWeightController();

        bool isUsingConsumable =
            playerWeightController != null &&
            playerWeightController.IsUsingConsumable;

        SetVisible(isUsingConsumable);

        if (!isUsingConsumable)
        {
            if (progressSlider != null)
            {
                progressSlider.value = 0f;
            }

            return;
        }

        if (progressSlider != null)
        {
            progressSlider.value =
                playerWeightController.ConsumableUseProgress;
        }

        if (labelText != null)
        {
            labelText.text = localizedLabel;
        }

        if (remainingTimeText != null)
        {
            remainingTimeText.text =
                $"{playerWeightController.ConsumableUseRemainingTime:0.0}s";
        }
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup == null)
        {
            return;
        }

        canvasGroup.alpha = visible ? 1f : 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        if (remainingTimeText != null)
        {
            remainingTimeText.gameObject.SetActive(
                visible && showRemainingTime
            );
        }
    }

    private void SetupSlider()
    {
        if (progressSlider == null)
        {
            return;
        }

        progressSlider.minValue = 0f;
        progressSlider.maxValue = 1f;
        progressSlider.value = 0f;
    }

    private void EnsureReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (progressSlider == null)
        {
            progressSlider =
                GetComponentInChildren<Slider>(true);
        }
    }

    private void FindPlayerWeightController()
    {
        if (playerWeightController != null)
        {
            return;
        }

        playerWeightController =
            FindAnyObjectByType<PlayerWeightController>();
    }

    private void EnsureLocalizedString()
    {
        if (useProgressLabel == null)
        {
            useProgressLabel = new LocalizedString();
        }

        if (string.IsNullOrWhiteSpace(localizedLabel))
        {
            localizedLabel = fallbackLabel;
        }
    }

    private void SubscribeLabel()
    {
        if (isLabelSubscribed)
        {
            return;
        }

        useProgressLabel.StringChanged += HandleLabelChanged;
        useProgressLabel.RefreshString();

        isLabelSubscribed = true;
    }

    private void UnsubscribeLabel()
    {
        if (!isLabelSubscribed)
        {
            return;
        }

        useProgressLabel.StringChanged -= HandleLabelChanged;

        isLabelSubscribed = false;
    }

    private void HandleLabelChanged(string localizedText)
    {
        localizedLabel = string.IsNullOrWhiteSpace(localizedText)
            ? fallbackLabel
            : localizedText;
    }
}