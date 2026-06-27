using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SurvivalBarUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private PlayerSurvivalController survivalController;

    [Header("食料UI")]
    [SerializeField] private Slider foodSlider;
    [SerializeField] private TMP_Text foodText;
    [SerializeField] private Image foodFillImage;

    [Header("水分UI")]
    [SerializeField] private Slider waterSlider;
    [SerializeField] private TMP_Text waterText;
    [SerializeField] private Image waterFillImage;

    [Header("バーの色")]
    [SerializeField]
    private Color normalColor =
        new Color(0.25f, 0.85f, 0.35f, 1f);

    [SerializeField]
    private Color warningColor =
        new Color(0.95f, 0.8f, 0.2f, 1f);

    [SerializeField]
    private Color lowColor =
        new Color(1f, 0.5f, 0.15f, 1f);

    [SerializeField]
    private Color criticalColor =
        new Color(0.95f, 0.2f, 0.2f, 1f);

    private bool isSubscribed;

    private void Awake()
    {
        FindReferences();
        SetupSliders();
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeEvents();
        RefreshAll();
    }

    private void Start()
    {
        FindReferences();
        SubscribeEvents();
        RefreshAll();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void RefreshAll()
    {
        if (survivalController == null)
        {
            return;
        }

        UpdateFood(
            survivalController.CurrentFood,
            survivalController.MaxFood
        );

        UpdateWater(
            survivalController.CurrentWater,
            survivalController.MaxWater
        );

        UpdateFoodColor(survivalController.FoodState);
        UpdateWaterColor(survivalController.WaterState);
    }

    private void UpdateFood(float currentFood, float maxFood)
    {
        float percent = maxFood <= 0f
            ? 0f
            : Mathf.Clamp01(currentFood / maxFood);

        if (foodSlider != null)
        {
            foodSlider.value = percent;
        }

        if (foodText != null)
        {
            foodText.text =
                $"食料 {Mathf.CeilToInt(currentFood)} / " +
                $"{Mathf.CeilToInt(maxFood)}";
        }
    }

    private void UpdateWater(float currentWater, float maxWater)
    {
        float percent = maxWater <= 0f
            ? 0f
            : Mathf.Clamp01(currentWater / maxWater);

        if (waterSlider != null)
        {
            waterSlider.value = percent;
        }

        if (waterText != null)
        {
            waterText.text =
                $"水分 {Mathf.CeilToInt(currentWater)} / " +
                $"{Mathf.CeilToInt(maxWater)}";
        }
    }

    private void HandleFoodStateChanged(
        SurvivalNeedState foodState)
    {
        UpdateFoodColor(foodState);
    }

    private void HandleWaterStateChanged(
        SurvivalNeedState waterState)
    {
        UpdateWaterColor(waterState);
    }

    private void UpdateFoodColor(SurvivalNeedState state)
    {
        if (foodFillImage == null)
        {
            return;
        }

        foodFillImage.color = GetStateColor(state);
    }

    private void UpdateWaterColor(SurvivalNeedState state)
    {
        if (waterFillImage == null)
        {
            return;
        }

        waterFillImage.color = GetStateColor(state);
    }

    private Color GetStateColor(SurvivalNeedState state)
    {
        switch (state)
        {
            case SurvivalNeedState.Warning:
                return warningColor;

            case SurvivalNeedState.Low:
                return lowColor;

            case SurvivalNeedState.Critical:
            case SurvivalNeedState.Empty:
                return criticalColor;

            default:
                return normalColor;
        }
    }

    private void SetupSliders()
    {
        SetupSlider(foodSlider);
        SetupSlider(waterSlider);
    }

    private void SetupSlider(Slider slider)
    {
        if (slider == null)
        {
            return;
        }

        slider.minValue = 0f;
        slider.maxValue = 1f;
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || survivalController == null)
        {
            return;
        }

        survivalController.FoodChanged += UpdateFood;
        survivalController.WaterChanged += UpdateWater;

        survivalController.FoodStateChanged +=
            HandleFoodStateChanged;

        survivalController.WaterStateChanged +=
            HandleWaterStateChanged;

        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || survivalController == null)
        {
            return;
        }

        survivalController.FoodChanged -= UpdateFood;
        survivalController.WaterChanged -= UpdateWater;

        survivalController.FoodStateChanged -=
            HandleFoodStateChanged;

        survivalController.WaterStateChanged -=
            HandleWaterStateChanged;

        isSubscribed = false;
    }

    private void FindReferences()
    {
        if (survivalController == null)
        {
            survivalController =
                FindAnyObjectByType<PlayerSurvivalController>();
        }

        if (foodFillImage == null &&
            foodSlider != null &&
            foodSlider.fillRect != null)
        {
            foodFillImage =
                foodSlider.fillRect.GetComponent<Image>();
        }

        if (waterFillImage == null &&
            waterSlider != null &&
            waterSlider.fillRect != null)
        {
            waterFillImage =
                waterSlider.fillRect.GetComponent<Image>();
        }
    }
}