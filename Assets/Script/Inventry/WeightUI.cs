using TMPro;
using UnityEngine;
using UnityEngine.Localization;

[DisallowMultipleComponent]
public class WeightUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private PlayerWeightController playerWeightController;

    [Header("表示Text")]
    [SerializeField] private TMP_Text weightText;
    [SerializeField] private TMP_Text stateText;

    [Header("表示設定")]
    [SerializeField] private bool showMaxWeight = true;
    [SerializeField] private bool showStateText = true;

    [Header("翻訳")]
    [Tooltip("GameText の weight.value.with_max を設定")]
    [SerializeField]
    private LocalizedString weightWithMaxFormat =
        new LocalizedString();

    [Tooltip("GameText の weight.value.current を設定")]
    [SerializeField]
    private LocalizedString weightCurrentFormat =
        new LocalizedString();

    [Tooltip("GameText の weight.state.slightly_slow を設定")]
    [SerializeField]
    private LocalizedString slightlySlowMessage =
        new LocalizedString();

    [Tooltip("GameText の weight.state.slow を設定")]
    [SerializeField]
    private LocalizedString slowMessage =
        new LocalizedString();

    [Tooltip("GameText の weight.state.very_slow を設定")]
    [SerializeField]
    private LocalizedString verySlowMessage =
        new LocalizedString();

    [Tooltip("GameText の weight.state.immobilized を設定")]
    [SerializeField]
    private LocalizedString immobilizedMessage =
        new LocalizedString();

    [Header("状態ごとの色")]
    [SerializeField] private Color normalColor = Color.white;

    [SerializeField]
    private Color slightlySlowColor =
        new Color(0.95f, 0.9f, 0.3f, 1f);

    [SerializeField]
    private Color slowColor =
        new Color(1f, 0.65f, 0.2f, 1f);

    [SerializeField]
    private Color verySlowColor =
        new Color(1f, 0.35f, 0.15f, 1f);

    [SerializeField]
    private Color immobilizedColor =
        new Color(1f, 0.2f, 0.2f, 1f);

    private bool isSubscribed;
    private bool isLocalizedStringsSubscribed;

    private string localizedWeightWithMaxFormat =
        "重量：{current} / {max} kg";

    private string localizedWeightCurrentFormat =
        "重量：{current} kg";

    private string localizedSlightlySlowMessage =
        "少し重い：移動速度低下";

    private string localizedSlowMessage =
        "重量超過：移動速度低下";

    private string localizedVerySlowMessage =
        "かなり重い：移動速度大幅低下";

    private string localizedImmobilizedMessage =
        "重量限界：移動できません";

    private void Awake()
    {
        EnsureLocalizedStrings();
        FindReferences();
    }

    private void OnEnable()
    {
        EnsureLocalizedStrings();
        FindReferences();

        SubscribeEvents();
        SubscribeLocalizedStrings();

        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        UnsubscribeLocalizedStrings();
    }

    private void HandleWeightChanged(float currentWeight)
    {
        RefreshUI();
    }

    private void HandleWeightStateChanged(
        PlayerWeightState state)
    {
        RefreshUI();
    }

    public void RefreshUI()
    {
        FindReferences();

        if (playerWeightController == null)
        {
            return;
        }

        float currentWeight =
            playerWeightController.CurrentWeight;

        float maxWeight =
            playerWeightController.ImmobilizedWeightLimit;

        PlayerWeightState state =
            playerWeightController.CurrentState;

        Color stateColor = GetStateColor(state);

        if (weightText != null)
        {
            weightText.text = GetWeightText(
                currentWeight,
                maxWeight
            );

            weightText.color = stateColor;
        }

        if (stateText != null)
        {
            bool shouldShow =
                showStateText &&
                state != PlayerWeightState.Normal;

            stateText.gameObject.SetActive(shouldShow);

            if (shouldShow)
            {
                stateText.text = GetStateMessage(state);
                stateText.color = stateColor;
            }
        }
    }

    private string GetWeightText(
        float currentWeight,
        float maxWeight)
    {
        string format = showMaxWeight
            ? localizedWeightWithMaxFormat
            : localizedWeightCurrentFormat;

        string currentValue = currentWeight.ToString("0.0");
        string maxValue = maxWeight.ToString("0.0");

        return format
            .Replace("{current}", currentValue)
            .Replace("{max}", maxValue);
    }

    private void SubscribeEvents()
    {
        if (playerWeightController == null)
        {
            return;
        }

        playerWeightController.OnWeightChanged -=
            HandleWeightChanged;

        playerWeightController.OnWeightChanged +=
            HandleWeightChanged;

        playerWeightController.OnWeightStateChanged -=
            HandleWeightStateChanged;

        playerWeightController.OnWeightStateChanged +=
            HandleWeightStateChanged;

        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed ||
            playerWeightController == null)
        {
            return;
        }

        playerWeightController.OnWeightChanged -=
            HandleWeightChanged;

        playerWeightController.OnWeightStateChanged -=
            HandleWeightStateChanged;

        isSubscribed = false;
    }

    private void SubscribeLocalizedStrings()
    {
        if (isLocalizedStringsSubscribed)
        {
            return;
        }

        weightWithMaxFormat.StringChanged +=
            HandleWeightWithMaxFormatChanged;

        weightCurrentFormat.StringChanged +=
            HandleWeightCurrentFormatChanged;

        slightlySlowMessage.StringChanged +=
            HandleSlightlySlowMessageChanged;

        slowMessage.StringChanged +=
            HandleSlowMessageChanged;

        verySlowMessage.StringChanged +=
            HandleVerySlowMessageChanged;

        immobilizedMessage.StringChanged +=
            HandleImmobilizedMessageChanged;

        isLocalizedStringsSubscribed = true;
    }

    private void UnsubscribeLocalizedStrings()
    {
        if (!isLocalizedStringsSubscribed)
        {
            return;
        }

        weightWithMaxFormat.StringChanged -=
            HandleWeightWithMaxFormatChanged;

        weightCurrentFormat.StringChanged -=
            HandleWeightCurrentFormatChanged;

        slightlySlowMessage.StringChanged -=
            HandleSlightlySlowMessageChanged;

        slowMessage.StringChanged -=
            HandleSlowMessageChanged;

        verySlowMessage.StringChanged -=
            HandleVerySlowMessageChanged;

        immobilizedMessage.StringChanged -=
            HandleImmobilizedMessageChanged;

        isLocalizedStringsSubscribed = false;
    }

    private void HandleWeightWithMaxFormatChanged(
        string localizedText)
    {
        localizedWeightWithMaxFormat =
            GetLocalizedOrFallback(
                localizedText,
                "重量：{current} / {max} kg"
            );

        RefreshUI();
    }

    private void HandleWeightCurrentFormatChanged(
        string localizedText)
    {
        localizedWeightCurrentFormat =
            GetLocalizedOrFallback(
                localizedText,
                "重量：{current} kg"
            );

        RefreshUI();
    }

    private void HandleSlightlySlowMessageChanged(
        string localizedText)
    {
        localizedSlightlySlowMessage =
            GetLocalizedOrFallback(
                localizedText,
                "少し重い：移動速度低下"
            );

        RefreshUI();
    }

    private void HandleSlowMessageChanged(
        string localizedText)
    {
        localizedSlowMessage =
            GetLocalizedOrFallback(
                localizedText,
                "重量超過：移動速度低下"
            );

        RefreshUI();
    }

    private void HandleVerySlowMessageChanged(
        string localizedText)
    {
        localizedVerySlowMessage =
            GetLocalizedOrFallback(
                localizedText,
                "かなり重い：移動速度大幅低下"
            );

        RefreshUI();
    }

    private void HandleImmobilizedMessageChanged(
        string localizedText)
    {
        localizedImmobilizedMessage =
            GetLocalizedOrFallback(
                localizedText,
                "重量限界：移動できません"
            );

        RefreshUI();
    }

    private string GetLocalizedOrFallback(
        string localizedText,
        string fallbackText)
    {
        return string.IsNullOrWhiteSpace(localizedText)
            ? fallbackText
            : localizedText;
    }

    private Color GetStateColor(
        PlayerWeightState state)
    {
        switch (state)
        {
            case PlayerWeightState.SlightlySlow:
                return slightlySlowColor;

            case PlayerWeightState.Slow:
                return slowColor;

            case PlayerWeightState.VerySlow:
                return verySlowColor;

            case PlayerWeightState.Immobilized:
                return immobilizedColor;

            default:
                return normalColor;
        }
    }

    private string GetStateMessage(
        PlayerWeightState state)
    {
        switch (state)
        {
            case PlayerWeightState.SlightlySlow:
                return localizedSlightlySlowMessage;

            case PlayerWeightState.Slow:
                return localizedSlowMessage;

            case PlayerWeightState.VerySlow:
                return localizedVerySlowMessage;

            case PlayerWeightState.Immobilized:
                return localizedImmobilizedMessage;

            default:
                return string.Empty;
        }
    }

    private void FindReferences()
    {
        if (playerWeightController == null)
        {
            playerWeightController =
                FindAnyObjectByType<
                    PlayerWeightController
                >();
        }

        if (weightText == null)
        {
            weightText = GetComponent<TMP_Text>();
        }
    }

    private void EnsureLocalizedStrings()
    {
        if (weightWithMaxFormat == null)
        {
            weightWithMaxFormat =
                new LocalizedString();
        }

        if (weightCurrentFormat == null)
        {
            weightCurrentFormat =
                new LocalizedString();
        }

        if (slightlySlowMessage == null)
        {
            slightlySlowMessage =
                new LocalizedString();
        }

        if (slowMessage == null)
        {
            slowMessage =
                new LocalizedString();
        }

        if (verySlowMessage == null)
        {
            verySlowMessage =
                new LocalizedString();
        }

        if (immobilizedMessage == null)
        {
            immobilizedMessage =
                new LocalizedString();
        }
    }
}