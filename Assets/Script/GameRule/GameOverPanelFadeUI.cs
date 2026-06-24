using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(CanvasGroup))]
public class GameOverPanelFadeUI : MonoBehaviour
{
    [Header("フェード設定")]
    [SerializeField, Min(0.01f)]
    private float fadeDuration = 0.6f;

    [SerializeField]
    private bool useUnscaledTime = true;

    private CanvasGroup canvasGroup;
    private Coroutine fadeCoroutine;

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
    }

    private void OnEnable()
    {
        StartFadeIn();
    }

    private void OnDisable()
    {
        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
            fadeCoroutine = null;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void StartFadeIn()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (fadeCoroutine != null)
        {
            StopCoroutine(fadeCoroutine);
        }

        fadeCoroutine = StartCoroutine(FadeInRoutine());
    }

    private IEnumerator FadeInRoutine()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += useUnscaledTime
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            canvasGroup.alpha = Mathf.Clamp01(
                elapsedTime / fadeDuration
            );

            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;
        canvasGroup.interactable = true;

        fadeCoroutine = null;
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
    }
}