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

        UpdateFoodColor(survivalController.FoodState);
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

    private void HandleFoodStateChanged(
        SurvivalNeedState foodState)
    {
        UpdateFoodColor(foodState);
    }

    private void UpdateFoodColor(SurvivalNeedState state)
    {
        if (foodFillImage == null)
        {
            return;
        }

        foodFillImage.color = GetStateColor(state);
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

        survivalController.FoodStateChanged +=
            HandleFoodStateChanged;

        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || survivalController == null)
        {
            return;
        }

        survivalController.FoodChanged -= UpdateFood;

        survivalController.FoodStateChanged -=
            HandleFoodStateChanged;

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

    }
}