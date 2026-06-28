using TMPro;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public class SanityBarUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerSanityController sanityController;
    [SerializeField] private Slider sanitySlider;
    [SerializeField] private TMP_Text sanityText;

    [Header("表示設定")]
    [SerializeField] private bool showMaxSanity = true;
    [SerializeField] private string label = "SAN";

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
        SubscribeEvents();
        RefreshUI();
    }

    private void Start()
    {
        FindReferences();
        SubscribeEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void RefreshUI()
    {
        if (sanityController == null)
        {
            return;
        }

        UpdateSanity(
            sanityController.CurrentSanity,
            sanityController.MaxSanity
        );
    }

    private void UpdateSanity(float currentSanity, float maxSanity)
    {
        float percent = maxSanity <= 0f
            ? 0f
            : Mathf.Clamp01(currentSanity / maxSanity);

        if (sanitySlider != null)
        {
            sanitySlider.value = percent;
        }

        if (sanityText != null)
        {
            int currentValue = Mathf.CeilToInt(currentSanity);
            int maximumValue = Mathf.CeilToInt(maxSanity);

            sanityText.text = showMaxSanity
                ? $"{label} {currentValue} / {maximumValue}"
                : $"{label} {currentValue}";
        }
    }

    private void SetupSlider()
    {
        if (sanitySlider == null)
        {
            sanitySlider = GetComponent<Slider>();
        }

        if (sanitySlider == null)
        {
            return;
        }

        sanitySlider.minValue = 0f;
        sanitySlider.maxValue = 1f;
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || sanityController == null)
        {
            return;
        }

        sanityController.SanityChanged += UpdateSanity;
        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || sanityController == null)
        {
            return;
        }

        sanityController.SanityChanged -= UpdateSanity;
        isSubscribed = false;
    }

    private void FindReferences()
    {
        if (sanityController == null)
        {
            sanityController =
                FindAnyObjectByType<PlayerSanityController>();
        }

        if (sanitySlider == null)
        {
            sanitySlider = GetComponent<Slider>();
        }

        if (sanityText == null)
        {
            sanityText = GetComponentInChildren<TMP_Text>(true);
        }
    }
}
