using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TorchControllerの残量をSliderとTextへ表示します。
/// </summary>
[DisallowMultipleComponent]
public class TorchBarUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private TorchController torchController;
    [SerializeField] private Slider torchSlider;
    [SerializeField] private TMP_Text torchText;

    [Header("表示")]
    [SerializeField] private string label = "松明";
    [SerializeField] private bool showMaximumValue = true;
    [SerializeField] private bool hideTextWhenEmpty;

    private bool isSubscribed;

    private void Awake()
    {
        FindReferences();
        SetupSlider();
    }

    private void OnEnable()
    {
        FindReferences();
        SetupSlider();
        Subscribe();
        RefreshUI();
    }

    private void Start()
    {
        FindReferences();
        Subscribe();
        RefreshUI();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    public void RefreshUI()
    {
        if (torchController == null)
        {
            return;
        }

        UpdateTorch(
            torchController.CurrentTorch,
            torchController.MaximumTorch
        );
    }

    private void UpdateTorch(float current, float maximum)
    {
        float percent = maximum <= 0f
            ? 0f
            : Mathf.Clamp01(current / maximum);

        if (torchSlider != null)
        {
            torchSlider.value = percent;
        }

        if (torchText == null)
        {
            return;
        }

        if (hideTextWhenEmpty && current <= 0.01f)
        {
            torchText.text = string.Empty;
            return;
        }

        int currentValue = Mathf.CeilToInt(current);
        int maximumValue = Mathf.CeilToInt(maximum);

        torchText.text = showMaximumValue
            ? $"{label} {currentValue} / {maximumValue}"
            : $"{label} {currentValue}";
    }

    private void SetupSlider()
    {
        if (torchSlider == null)
        {
            torchSlider = GetComponent<Slider>();
        }

        if (torchSlider == null)
        {
            return;
        }

        torchSlider.minValue = 0f;
        torchSlider.maxValue = 1f;
    }

    private void Subscribe()
    {
        if (isSubscribed || torchController == null)
        {
            return;
        }

        torchController.TorchChanged += UpdateTorch;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || torchController == null)
        {
            return;
        }

        torchController.TorchChanged -= UpdateTorch;
        isSubscribed = false;
    }

    private void FindReferences()
    {
        if (torchController == null)
        {
            torchController = FindAnyObjectByType<TorchController>();
        }

        if (torchSlider == null)
        {
            torchSlider = GetComponent<Slider>();
        }

        if (torchText == null)
        {
            torchText = GetComponentInChildren<TMP_Text>(true);
        }
    }
}
