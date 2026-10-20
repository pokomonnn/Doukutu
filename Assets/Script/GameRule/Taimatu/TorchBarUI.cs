using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// TorchControllerの残量を下半円ゲージへ表示します。旧Slider/Text参照はシーン互換用に保持。
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
    private TorchHalfRingGraphic halfRing;

    private void SetupHalfRing()
    {
        if (halfRing == null)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;
            var root = new GameObject("Torch lower half ring", typeof(RectTransform), typeof(CanvasRenderer));
            var rect = root.GetComponent<RectTransform>();
            rect.SetParent(canvas.transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, 1);
            rect.pivot = new Vector2(.5f, 1);
            rect.sizeDelta = new Vector2(160, 82);
            rect.anchoredPosition = new Vector2(0, -24);
            halfRing = root.AddComponent<TorchHalfRingGraphic>();
            halfRing.raycastTarget = false;
        }
        halfRing.gameObject.SetActive(isActiveAndEnabled);
        if (torchSlider != null)
        {
            torchSlider.interactable = false;
            foreach (var graphic in torchSlider.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
        }
        if (torchText != null) { torchText.text = string.Empty; torchText.enabled = false; }
    }

    private void Awake()
    {
        FindReferences();
        SetupSlider();
        SetupHalfRing();
    }

    private void OnEnable()
    {
        FindReferences();
        SetupSlider();
        SetupHalfRing();
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
        if (halfRing != null) halfRing.gameObject.SetActive(false);
    }

    private void OnDestroy() { if (halfRing != null) Destroy(halfRing.gameObject); }

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

        if (halfRing != null)
        {
            halfRing.SetRemaining(percent);
            if (torchText != null) torchText.text = string.Empty;
            return;
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
