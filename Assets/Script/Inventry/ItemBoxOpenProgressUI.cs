using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// アイテムボックスを開くまでの進行状況を表示するUIです。
/// Canvas内の専用パネルに付けて使用します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class ItemBoxOpenProgressUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("子オブジェクトのSliderを設定します。未設定なら子から自動取得します")]
    [SerializeField] private Slider progressSlider;

    [Tooltip("任意。設定すると開く進行率を文字でも表示します")]
    [SerializeField] private TMP_Text progressText;

    [Header("表示")]
    [SerializeField] private bool showPercentText = false;

    [SerializeField] private string progressLabel = "開けています...";

    private void Awake()
    {
        EnsureReferences();
        SetupSlider();
        Hide();
    }

    /// <summary>
    /// 進行UIを表示し、0〜1の値を反映します。
    /// </summary>
    public void Show(float normalizedProgress = 0f)
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
        }

        EnsureReferences();
        SetupSlider();
        SetVisible(true);
        SetProgress(normalizedProgress);
    }

    /// <summary>
    /// 進行率を0〜1の範囲で設定します。
    /// </summary>
    public void SetProgress(float normalizedProgress)
    {
        normalizedProgress = Mathf.Clamp01(normalizedProgress);

        if (progressSlider != null)
        {
            progressSlider.value = normalizedProgress;
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(showPercentText);

            if (showPercentText)
            {
                int percent = Mathf.RoundToInt(normalizedProgress * 100f);
                progressText.text = $"{progressLabel} {percent}%";
            }
        }
    }

    /// <summary>
    /// UIを隠して、次回用に値を0へ戻します。
    /// </summary>
    public void Hide()
    {
        if (progressSlider != null)
        {
            progressSlider.value = 0f;
        }

        if (progressText != null)
        {
            progressText.gameObject.SetActive(false);
        }

        SetVisible(false);
    }

    private void EnsureReferences()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (progressSlider == null)
        {
            progressSlider = GetComponentInChildren<Slider>(true);
        }

        if (progressText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text text in texts)
            {
                if (text != null && text.gameObject.name == "ProgressText")
                {
                    progressText = text;
                    break;
                }
            }
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
        progressSlider.interactable = false;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(progressLabel))
        {
            progressLabel = "開けています...";
        }
    }
}
