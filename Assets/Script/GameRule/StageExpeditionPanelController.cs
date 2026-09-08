using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage1～4で共通利用する出撃パネルです。
/// 選択されたStageExpeditionDataを表示し、
/// 「出発する」で既存SceneTransitionButtonの安全な遷移処理を呼びます。
///
/// 開く時は、小さい状態から広がるポップアップ演出と開く効果音を再生できます。
/// 閉じる時は閉じる効果音を再生できます。
/// 「出発する」押下時は出撃音を鳴らし、指定秒数待機しながら画面全体を
/// 徐々にフェードアウトしてから既存SceneTransitionButtonへ遷移を渡します。
/// </summary>
[DisallowMultipleComponent]
public class StageExpeditionPanelController : MonoBehaviour
{
    [Header("パネル")]
    [Tooltip("表示・非表示を切り替えるPanel本体。未設定ならこのGameObjectを使用します。")]
    [SerializeField]
    private GameObject panelRoot;

    [Tooltip("ゲーム開始時は出撃パネルを閉じます。")]
    [SerializeField]
    private bool startHidden = true;

    [Header("表示Text")]
    [SerializeField]
    private TMP_Text stageNameText;

    [SerializeField]
    private TMP_Text combatDangerText;

    [SerializeField]
    private TMP_Text caveDangerText;

    [SerializeField]
    private TMP_Text requiredSuppliesText;

    [Header("Button")]
    [SerializeField]
    private Button departButton;

    [SerializeField]
    private Button closeButton;

    [Header("パネルを開く演出")]
    [Tooltip("オンなら、パネルを小さい状態から広げて表示します。")]
    [SerializeField]
    private bool useOpenAnimation = true;

    [Tooltip("パネルが広がり切るまでの秒数です。")]
    [SerializeField, Min(0.01f)]
    private float openAnimationDuration = 0.22f;

    [Tooltip("開き始めの大きさです。0.75なら75%の大きさから始まります。")]
    [SerializeField, Range(0.1f, 1f)]
    private float openStartScale = 0.75f;

    [Tooltip("最後に少しだけ勢いよく広がる強さです。1.15～1.5程度がおすすめです。")]
    [SerializeField, Range(0f, 3f)]
    private float openOvershoot = 1.25f;

    [Header("パネルを開く音")]
    [Tooltip("未設定なら、このGameObjectまたはPanel RootからAudioSourceを探します。")]
    [SerializeField]
    private AudioSource audioSource;

    [Tooltip("出撃パネルを開いた瞬間に鳴らす効果音です。")]
    [SerializeField]
    private AudioClip openSound;

    [SerializeField, Range(0f, 1f)]
    private float openSoundVolume = 1f;

    [Header("パネルを閉じる音")]
    [SerializeField] private AudioClip closeSound;
    [SerializeField, Range(0f, 1f)] private float closeSoundVolume = 1f;

    [Header("出撃時の演出")]
    [SerializeField] private AudioClip departSound;
    [SerializeField, Range(0f, 1f)] private float departSoundVolume = 1f;
    [SerializeField, Min(0f)] private float departureDelaySeconds = 2f;
    [SerializeField, Min(0.01f)] private float departureFadeDuration = 2f;
    [SerializeField] private Color departureFadeColor = Color.black;

    [Header("出撃フェードUI")]
    [SerializeField] private CanvasGroup departureFadeCanvasGroup;
    [SerializeField] private Image departureFadeImage;
    [SerializeField] private bool autoCreateDepartureFade = true;

    private StageExpeditionData selectedStageData;
    private SceneTransitionButton selectedTransitionButton;

    private bool initialized;
    private Coroutine openAnimationCoroutine;
    private Coroutine departureCoroutine;
    private Vector3 panelBaseScale = Vector3.one;
    private bool isDeparting;

    /// <summary>
    /// 現在パネルで選択されているステージ。
    /// 将来、ミッション一覧を表示する時にも利用できます。
    /// </summary>
    public StageExpeditionData CurrentStageData =>
        selectedStageData;

    public bool IsOpen =>
        panelRoot != null &&
        panelRoot.activeSelf &&
        selectedStageData != null;

    /// <summary>
    /// 将来のミッションUIなどが、ステージ選択を検知するためのイベントです。
    /// </summary>
    public event Action<StageExpeditionData> PanelOpened;

    public event Action PanelClosed;

    private void Awake()
    {
        EnsureInitialized();

        if (panelRoot != null)
        {
            panelBaseScale = panelRoot.transform.localScale;
        }

        FindAudioSource();

        if (startHidden)
        {
            HideImmediate();
        }
    }

    private void OnDisable()
    {
        if (openAnimationCoroutine != null)
        {
            StopCoroutine(openAnimationCoroutine);
            openAnimationCoroutine = null;
        }

        if (departureCoroutine != null)
        {
            StopCoroutine(departureCoroutine);
            departureCoroutine = null;
        }

        isDeparting = false;
    }

    private void OnDestroy()
    {
        if (departButton != null)
        {
            departButton.onClick.RemoveListener(Depart);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(Close);
        }
    }

    /// <summary>
    /// StageExpeditionButtonから呼びます。
    /// </summary>
    public void Open(
        StageExpeditionData stageData,
        SceneTransitionButton transitionButton)
    {
        EnsureInitialized();

        if (stageData == null)
        {
            Debug.LogWarning(
                "[StageExpeditionPanelController] StageExpeditionDataがnullです。",
                this
            );
            return;
        }

        if (transitionButton == null)
        {
            Debug.LogWarning(
                "[StageExpeditionPanelController] SceneTransitionButtonがnullです。",
                this
            );
            return;
        }

        selectedStageData = stageData;
        selectedTransitionButton = transitionButton;

        RefreshDisplay();

        if (panelRoot != null)
        {
            panelRoot.SetActive(true);
        }

        isDeparting = false;
        ResetDepartureFade();

        if (departButton != null)
        {
            departButton.interactable = true;
        }

        if (closeButton != null)
        {
            closeButton.interactable = true;
        }

        PlayOpenSound();
        PlayOpenAnimation();

        PanelOpened?.Invoke(selectedStageData);
    }

    /// <summary>
    /// 出撃パネルの「出発する」Buttonから呼ばれます。
    /// 既存SceneTransitionButtonを通すため、
    /// 今までのシーン移動前保存処理を維持できます。
    /// </summary>
    public void Depart()
    {
        if (isDeparting) return;

        if (selectedStageData == null)
        {
            Debug.LogWarning("[StageExpeditionPanelController] 出発先ステージが選択されていません。", this);
            return;
        }

        if (selectedTransitionButton == null)
        {
            Debug.LogWarning("[StageExpeditionPanelController] 出発用SceneTransitionButtonが見つかりません。", this);
            return;
        }

        departureCoroutine = StartCoroutine(DepartureRoutine());
    }

    public void Close()
    {
        if (isDeparting) return;

        StopOpenAnimationAndRestoreScale();
        PlayCloseSound();

        selectedStageData = null;
        selectedTransitionButton = null;

        if (panelRoot != null) panelRoot.SetActive(false);

        PanelClosed?.Invoke();
    }

    private void HideImmediate()
    {
        StopOpenAnimationAndRestoreScale();

        selectedStageData = null;
        selectedTransitionButton = null;

        if (panelRoot != null)
        {
            panelRoot.SetActive(false);
        }
    }

    private IEnumerator DepartureRoutine()
    {
        isDeparting = true;

        if (departButton != null) departButton.interactable = false;
        if (closeButton != null) closeButton.interactable = false;

        PlayDepartSound();
        EnsureDepartureFadeVisual();

        if (departureFadeCanvasGroup != null)
        {
            departureFadeCanvasGroup.alpha = 0f;
            departureFadeCanvasGroup.blocksRaycasts = true;
            departureFadeCanvasGroup.interactable = true;
            departureFadeCanvasGroup.transform.SetAsLastSibling();
        }

        float delay = Mathf.Max(0f, departureDelaySeconds);
        float fadeDuration = Mathf.Max(0.01f, departureFadeDuration);
        float totalDuration = Mathf.Max(delay, fadeDuration);
        float elapsed = 0f;

        while (elapsed < totalDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            if (departureFadeCanvasGroup != null)
            {
                float fadeT = Mathf.Clamp01(elapsed / fadeDuration);
                departureFadeCanvasGroup.alpha = Mathf.SmoothStep(0f, 1f, fadeT);
            }

            yield return null;
        }

        if (departureFadeCanvasGroup != null) departureFadeCanvasGroup.alpha = 1f;

        SceneTransitionButton transitionButton = selectedTransitionButton;
        departureCoroutine = null;

        if (transitionButton != null)
        {
            transitionButton.LoadConfiguredScene();
        }
        else
        {
            isDeparting = false;
            if (departButton != null) departButton.interactable = true;
            if (closeButton != null) closeButton.interactable = true;
            ResetDepartureFade();
        }
    }

    private void EnsureDepartureFadeVisual()
    {
        if (departureFadeCanvasGroup != null)
        {
            if (departureFadeImage == null)
                departureFadeImage = departureFadeCanvasGroup.GetComponent<Image>();
            ApplyDepartureFadeVisualSettings();
            return;
        }

        if (!autoCreateDepartureFade) return;

        Canvas rootCanvas = null;
        if (panelRoot != null)
        {
            Canvas canvas = panelRoot.GetComponentInParent<Canvas>();
            if (canvas != null) rootCanvas = canvas.rootCanvas;
        }
        if (rootCanvas == null)
        {
            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas != null) rootCanvas = canvas.rootCanvas;
        }
        if (rootCanvas == null)
        {
            Debug.LogWarning("[StageExpeditionPanelController] 出撃フェード用Root Canvasが見つかりません。", this);
            return;
        }

        GameObject fadeObject = new GameObject(
            "ExpeditionDepartureFade",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image));

        fadeObject.transform.SetParent(rootCanvas.transform, false);

        RectTransform rect = fadeObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        departureFadeCanvasGroup = fadeObject.GetComponent<CanvasGroup>();
        departureFadeImage = fadeObject.GetComponent<Image>();
        ApplyDepartureFadeVisualSettings();
    }

    private void ApplyDepartureFadeVisualSettings()
    {
        if (departureFadeImage != null)
        {
            departureFadeImage.color = departureFadeColor;
            departureFadeImage.raycastTarget = true;
        }

        if (departureFadeCanvasGroup != null)
        {
            departureFadeCanvasGroup.alpha = 0f;
            departureFadeCanvasGroup.interactable = false;
            departureFadeCanvasGroup.blocksRaycasts = false;
        }
    }

    private void ResetDepartureFade()
    {
        if (departureFadeCanvasGroup == null) return;
        departureFadeCanvasGroup.alpha = 0f;
        departureFadeCanvasGroup.interactable = false;
        departureFadeCanvasGroup.blocksRaycasts = false;
    }

    private void PlayOpenAnimation()
    {
        if (panelRoot == null)
        {
            return;
        }

        if (openAnimationCoroutine != null)
        {
            StopCoroutine(openAnimationCoroutine);
            openAnimationCoroutine = null;
        }

        if (!useOpenAnimation || openAnimationDuration <= 0.001f)
        {
            panelRoot.transform.localScale = panelBaseScale;
            return;
        }

        openAnimationCoroutine =
            StartCoroutine(OpenAnimationRoutine());
    }

    private IEnumerator OpenAnimationRoutine()
    {
        if (panelRoot == null)
        {
            yield break;
        }

        Transform panelTransform = panelRoot.transform;

        Vector3 startScale =
            panelBaseScale * Mathf.Clamp(openStartScale, 0.1f, 1f);

        panelTransform.localScale = startScale;

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, openAnimationDuration);

        while (elapsed < duration)
        {
            if (panelRoot == null)
            {
                yield break;
            }

            elapsed += Time.unscaledDeltaTime;

            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = EaseOutBack(t, openOvershoot);

            panelTransform.localScale =
                Vector3.LerpUnclamped(
                    startScale,
                    panelBaseScale,
                    easedT
                );

            yield return null;
        }

        panelTransform.localScale = panelBaseScale;
        openAnimationCoroutine = null;
    }

    /// <summary>
    /// 0→1の途中で少しだけ1を超える、ポップアップ向け補間です。
    /// </summary>
    private static float EaseOutBack(float t, float overshoot)
    {
        float c1 = Mathf.Max(0f, overshoot);
        float c3 = c1 + 1f;
        float x = t - 1f;

        return 1f +
               c3 * x * x * x +
               c1 * x * x;
    }

    private void StopOpenAnimationAndRestoreScale()
    {
        if (openAnimationCoroutine != null)
        {
            StopCoroutine(openAnimationCoroutine);
            openAnimationCoroutine = null;
        }

        if (panelRoot != null)
        {
            panelRoot.transform.localScale = panelBaseScale;
        }
    }

    private void PlayCloseSound()
    {
        PlayUISound(closeSound, closeSoundVolume, true);
    }

    private void PlayDepartSound()
    {
        PlayUISound(departSound, departSoundVolume, false);
    }

    private void PlayUISound(AudioClip clip, float volume, bool survivePanelDisable)
    {
        if (clip == null) return;
        FindAudioSource();

        bool sourceWillBeDisabled =
            survivePanelDisable &&
            audioSource != null &&
            panelRoot != null &&
            (audioSource.gameObject == panelRoot || audioSource.transform.IsChildOf(panelRoot.transform));

        if (audioSource != null && !sourceWillBeDisabled)
        {
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
            return;
        }

        PlayDetachedUISound(clip, volume);
    }

    private static void PlayDetachedUISound(AudioClip clip, float volume)
    {
        if (clip == null) return;

        GameObject audioObject = new GameObject("TemporaryUISound");
        AudioSource source = audioObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(volume);
        source.clip = clip;
        source.Play();
        Destroy(audioObject, Mathf.Max(0.1f, clip.length + 0.1f));
    }

    private void PlayOpenSound()
    {
        if (openSound == null)
        {
            return;
        }

        FindAudioSource();

        if (audioSource == null)
        {
            return;
        }

        audioSource.PlayOneShot(
            openSound,
            Mathf.Clamp01(openSoundVolume)
        );
    }

    private void FindAudioSource()
    {
        if (audioSource != null)
        {
            return;
        }

        audioSource = GetComponent<AudioSource>();

        if (audioSource == null && panelRoot != null)
        {
            audioSource = panelRoot.GetComponent<AudioSource>();
        }
    }

    private void RefreshDisplay()
    {
        if (selectedStageData == null)
        {
            return;
        }

        if (stageNameText != null)
        {
            stageNameText.text =
                selectedStageData.StageName;
        }

        if (combatDangerText != null)
        {
            combatDangerText.text =
                BuildStars(
                    selectedStageData.CombatDanger
                );
        }

        if (caveDangerText != null)
        {
            caveDangerText.text =
                BuildStars(
                    selectedStageData.CaveDanger
                );
        }

        if (requiredSuppliesText != null)
        {
            requiredSuppliesText.text =
                BuildStars(
                    selectedStageData.RequiredSupplies
                );
        }
    }

    private static string BuildStars(int value)
    {
        const int maxStars = 5;

        int clamped =
            Mathf.Clamp(value, 1, maxStars);

        return
            new string('★', clamped) +
            new string('☆', maxStars - clamped);
    }

    private void EnsureInitialized()
    {
        if (initialized)
        {
            return;
        }

        if (panelRoot == null)
        {
            panelRoot = gameObject;
        }

        if (departButton != null)
        {
            departButton.onClick.AddListener(Depart);
        }

        if (closeButton != null)
        {
            closeButton.onClick.AddListener(Close);
        }

        initialized = true;
    }

    private void OnValidate()
    {
        openAnimationDuration =
            Mathf.Max(0.01f, openAnimationDuration);

        openStartScale =
            Mathf.Clamp(openStartScale, 0.1f, 1f);

        openOvershoot =
            Mathf.Clamp(openOvershoot, 0f, 3f);

        openSoundVolume =
            Mathf.Clamp01(openSoundVolume);

        closeSoundVolume = Mathf.Clamp01(closeSoundVolume);
        departSoundVolume = Mathf.Clamp01(departSoundVolume);
        departureDelaySeconds = Mathf.Max(0f, departureDelaySeconds);
        departureFadeDuration = Mathf.Max(0.01f, departureFadeDuration);
    }
}
