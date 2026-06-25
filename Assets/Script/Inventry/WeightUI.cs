using TMPro;
using UnityEngine;

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
    [SerializeField] private string weightLabel = "重量";
    [SerializeField] private bool showMaxWeight = true;
    [SerializeField] private bool showStateText = true;

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

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
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
            if (showMaxWeight)
            {
                weightText.text =
                    $"{weightLabel}：{currentWeight:0.0} / " +
                    $"{maxWeight:0.0} kg";
            }
            else
            {
                weightText.text =
                    $"{weightLabel}：{currentWeight:0.0} kg";
            }

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
    }

    private void UnsubscribeEvents()
    {
        if (playerWeightController == null)
        {
            return;
        }

        playerWeightController.OnWeightChanged -=
            HandleWeightChanged;

        playerWeightController.OnWeightStateChanged -=
            HandleWeightStateChanged;
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
                return "少し重い：移動速度低下";

            case PlayerWeightState.Slow:
                return "重量超過：移動速度低下";

            case PlayerWeightState.VerySlow:
                return "かなり重い：移動速度大幅低下";

            case PlayerWeightState.Immobilized:
                return "重量限界：移動できません";

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
}