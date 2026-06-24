using System.Collections;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class InventoryToastUI : MonoBehaviour
{
    [Header("表示するText")]
    [SerializeField] private TMP_Text messageText;

    [Header("アニメーション")]
    [SerializeField, Min(0.1f)] private float displayDuration = 1.2f;
    [SerializeField, Min(0f)] private float riseDistance = 70f;

    [Tooltip("この割合から徐々に透明にする")]
    [SerializeField, Range(0f, 1f)]
    private float fadeStartNormalizedTime = 0.35f;

    private RectTransform toastRect;
    private CanvasGroup canvasGroup;
    private Coroutine showCoroutine;

    private Vector2 startPosition;
    private bool hasStartPosition;

    private void Awake()
    {
        EnsureReferences();
        CaptureStartPosition();
        HideImmediately();
    }

    public void Show(string message)
    {
        EnsureReferences();

        if (messageText == null)
        {
            Debug.LogWarning(
                "InventoryToastUI: Message Text が設定されていません。",
                this
            );
            return;
        }

        CaptureStartPosition();

        if (showCoroutine != null)
        {
            StopCoroutine(showCoroutine);
        }

        messageText.text = message;

        toastRect.anchoredPosition = startPosition;
        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        showCoroutine = StartCoroutine(ShowRoutine());
    }

    private IEnumerator ShowRoutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < displayDuration)
        {
            float normalizedTime = Mathf.Clamp01(
                elapsedTime / displayDuration
            );

            toastRect.anchoredPosition =
                startPosition +
                Vector2.up * riseDistance * normalizedTime;

            float fadeTime = Mathf.InverseLerp(
                fadeStartNormalizedTime,
                1f,
                normalizedTime
            );

            canvasGroup.alpha = 1f - fadeTime;

            elapsedTime += Time.unscaledDeltaTime;

            yield return null;
        }

        HideImmediately();
        showCoroutine = null;
    }

    private void HideImmediately()
    {
        if (toastRect != null)
        {
            toastRect.anchoredPosition = startPosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    private void EnsureReferences()
    {
        if (toastRect == null)
        {
            toastRect = GetComponent<RectTransform>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (messageText == null)
        {
            messageText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void CaptureStartPosition()
    {
        if (toastRect == null || hasStartPosition)
        {
            return;
        }

        startPosition = toastRect.anchoredPosition;
        hasStartPosition = true;
    }

    private void OnValidate()
    {
        displayDuration = Mathf.Max(0.1f, displayDuration);
        riseDistance = Mathf.Max(0f, riseDistance);
        fadeStartNormalizedTime =
            Mathf.Clamp01(fadeStartNormalizedTime);
    }
}