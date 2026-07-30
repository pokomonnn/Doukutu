using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// チュートリアルの説明文と、指定したワールド座標へ向くUI矢印を表示します。
/// Screen Space - Overlay / Camera のCanvasに対応します。
/// </summary>
[DisallowMultipleComponent]
public class TutorialPromptUI : MonoBehaviour
{
    [Header("表示ルート")]
    [Tooltip("説明文と矢印をまとめたRootです。未設定ならこのGameObjectを使います。")]
    [SerializeField] private GameObject tutorialRoot;

    [Tooltip("未設定ならTutorial Rootから自動取得します。")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("説明文")]
    [SerializeField] private TMP_Text tutorialText;

    [Header("矢印")]
    [SerializeField] private Image arrowImage;

    [Tooltip("Arrow ImageのRectTransformです。未設定なら自動取得します。")]
    [SerializeField] private RectTransform arrowRectTransform;

    [Tooltip("矢印を配置する基準CanvasのRectTransformです。未設定なら親Canvasから取得します。")]
    [SerializeField] private RectTransform canvasRectTransform;

    [Tooltip("Screen Space - Cameraの場合に使います。未設定ならCanvasのWorld Camera、次にMain Cameraを使います。")]
    [SerializeField] private Camera worldCamera;

    [Tooltip("プレイヤーの画面位置から、矢印をずらす量です。")]
    [SerializeField] private Vector2 arrowScreenOffset = new Vector2(0f, 90f);

    [Tooltip("矢印画像が右向きなら0、上向きなら-90付近にします。")]
    [SerializeField] private float arrowRotationOffset;

    [Tooltip("ターゲットと矢印基準点が近すぎる時、矢印を隠す距離です。0なら常に表示します。")]
    [SerializeField, Min(0f)] private float hideArrowWithinWorldDistance;

    [Header("フェード")]
    [SerializeField, Min(0f)] private float fadeInDuration = 0.2f;
    [SerializeField, Min(0f)] private float fadeOutDuration = 0.2f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("動作")]
    [SerializeField] private bool hideOnAwake = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public bool IsVisible { get; private set; }
    public Transform CurrentOrigin => currentOrigin;
    public Transform CurrentTarget => currentTarget;

    private Canvas parentCanvas;
    private Transform currentOrigin;
    private Transform currentTarget;
    private Coroutine visibilityCoroutine;
    private Coroutine autoHideCoroutine;

    private void Awake()
    {
        FindReferences();

        if (hideOnAwake)
        {
            HideImmediately();
        }
    }

    private void LateUpdate()
    {
        if (!IsVisible)
        {
            return;
        }

        UpdateArrow();
    }

    /// <summary>
    /// チュートリアルを表示します。
    /// durationが0以下なら、自動では消えません。
    /// </summary>
    public void Show(
        string message,
        Transform arrowOrigin,
        Transform arrowTarget,
        float duration = 0f)
    {
        FindReferences();

        currentOrigin = arrowOrigin;
        currentTarget = arrowTarget;

        if (tutorialText != null)
        {
            tutorialText.text = message ?? string.Empty;
        }


        if (tutorialRoot != null && !tutorialRoot.activeSelf)
        {
            tutorialRoot.SetActive(true);
        }

        IsVisible = true;

        if (arrowImage != null)
        {
            arrowImage.gameObject.SetActive(
                currentOrigin != null && currentTarget != null
            );
        }

        UpdateArrow();
        StartFade(1f, fadeInDuration, false);

        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        if (duration > 0f)
        {
            autoHideCoroutine = StartCoroutine(
                AutoHideRoutine(duration)
            );
        }

        Log(
            $"表示開始: Message={message} / " +
            $"Origin={(arrowOrigin != null ? arrowOrigin.name : "未設定")} / " +
            $"Target={(arrowTarget != null ? arrowTarget.name : "未設定")}"
        );
    }

    public void Hide()
    {
        if (!IsVisible &&
            (tutorialRoot == null || !tutorialRoot.activeSelf))
        {
            return;
        }

        IsVisible = false;

        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        StartFade(0f, fadeOutDuration, true);
    }

    public void HideImmediately()
    {
        if (visibilityCoroutine != null)
        {
            StopCoroutine(visibilityCoroutine);
            visibilityCoroutine = null;
        }

        if (autoHideCoroutine != null)
        {
            StopCoroutine(autoHideCoroutine);
            autoHideCoroutine = null;
        }

        IsVisible = false;
        currentOrigin = null;
        currentTarget = null;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (tutorialRoot != null)
        {
            tutorialRoot.SetActive(false);
        }
    }

    private void UpdateArrow()
    {
        if (arrowImage == null || arrowRectTransform == null)
        {
            return;
        }

        if (currentOrigin == null || currentTarget == null)
        {
            arrowImage.gameObject.SetActive(false);
            return;
        }

        if (hideArrowWithinWorldDistance > 0f &&
            Vector2.Distance(
                currentOrigin.position,
                currentTarget.position
            ) <= hideArrowWithinWorldDistance)
        {
            arrowImage.gameObject.SetActive(false);
            return;
        }

        arrowImage.gameObject.SetActive(true);

        Camera cameraForWorld = GetWorldCamera();

        Vector2 originScreen = RectTransformUtility.WorldToScreenPoint(
            cameraForWorld,
            currentOrigin.position
        );

        Vector2 targetScreen = RectTransformUtility.WorldToScreenPoint(
            cameraForWorld,
            currentTarget.position
        );

        Vector2 screenDirection = targetScreen - originScreen;

        if (screenDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        if (canvasRectTransform != null &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRectTransform,
                originScreen,
                GetCanvasEventCamera(),
                out Vector2 localOrigin))
        {
            arrowRectTransform.anchoredPosition =
                localOrigin + arrowScreenOffset;
        }

        float angle = Mathf.Atan2(
            screenDirection.y,
            screenDirection.x
        ) * Mathf.Rad2Deg;

        arrowRectTransform.localRotation = Quaternion.Euler(
            0f,
            0f,
            angle + arrowRotationOffset
        );
    }

    private IEnumerator AutoHideRoutine(float duration)
    {
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            yield return null;
        }

        autoHideCoroutine = null;
        Hide();
    }

    private void StartFade(
        float targetAlpha,
        float duration,
        bool disableRootAfterFade)
    {
        if (visibilityCoroutine != null)
        {
            StopCoroutine(visibilityCoroutine);
        }

        visibilityCoroutine = StartCoroutine(
            FadeRoutine(
                targetAlpha,
                duration,
                disableRootAfterFade
            )
        );
    }

    private IEnumerator FadeRoutine(
        float targetAlpha,
        float duration,
        bool disableRootAfterFade)
    {
        if (canvasGroup == null)
        {
            if (disableRootAfterFade && tutorialRoot != null)
            {
                tutorialRoot.SetActive(false);
            }

            visibilityCoroutine = null;
            yield break;
        }

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        float startAlpha = canvasGroup.alpha;

        if (duration <= 0f)
        {
            canvasGroup.alpha = targetAlpha;
        }
        else
        {
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += GetDeltaTime();
                float progress = Mathf.Clamp01(elapsed / duration);
                canvasGroup.alpha = Mathf.Lerp(
                    startAlpha,
                    targetAlpha,
                    progress
                );

                yield return null;
            }

            canvasGroup.alpha = targetAlpha;
        }

        if (disableRootAfterFade)
        {
            currentOrigin = null;
            currentTarget = null;

            if (tutorialRoot != null)
            {
                tutorialRoot.SetActive(false);
            }
        }

        visibilityCoroutine = null;
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
    }

    private Camera GetWorldCamera()
    {
        if (worldCamera != null)
        {
            return worldCamera;
        }

        if (parentCanvas != null && parentCanvas.worldCamera != null)
        {
            return parentCanvas.worldCamera;
        }

        return Camera.main;
    }

    private Camera GetCanvasEventCamera()
    {
        if (parentCanvas == null ||
            parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return parentCanvas.worldCamera != null
            ? parentCanvas.worldCamera
            : worldCamera;
    }

    private void FindReferences()
    {
        if (tutorialRoot == null)
        {
            tutorialRoot = gameObject;
        }

        if (canvasGroup == null && tutorialRoot != null)
        {
            canvasGroup = tutorialRoot.GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null && tutorialRoot != null)
        {
            canvasGroup = tutorialRoot.AddComponent<CanvasGroup>();
        }

        if (tutorialText == null && tutorialRoot != null)
        {
            tutorialText = tutorialRoot.GetComponentInChildren<TMP_Text>(true);
        }


        if (arrowImage != null && arrowRectTransform == null)
        {
            arrowRectTransform = arrowImage.rectTransform;
        }

        if (parentCanvas == null)
        {
            parentCanvas = GetComponentInParent<Canvas>(true);
        }

        if (canvasRectTransform == null && parentCanvas != null)
        {
            canvasRectTransform = parentCanvas.transform as RectTransform;
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[TutorialPromptUI] {message}", this);
    }

    private void OnValidate()
    {
        fadeInDuration = Mathf.Max(0f, fadeInDuration);
        fadeOutDuration = Mathf.Max(0f, fadeOutDuration);
        hideArrowWithinWorldDistance = Mathf.Max(
            0f,
            hideArrowWithinWorldDistance
        );
    }
}
