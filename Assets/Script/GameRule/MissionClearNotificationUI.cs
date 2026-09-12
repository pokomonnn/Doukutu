using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// MissionManager2D の MissionCompleted イベントを監視して、
/// 洞窟探索中に「ミッションクリア」通知を表示します。
///
/// 演出：
/// 1. 画面上側から下へ降りてくる
/// 2. 指定位置で停止する
/// 3. 一定時間後にフェードアウトする
///
/// 複数ミッションが同時に完了した場合は、キューに入れて1件ずつ順番に表示します。
/// </summary>
[DisallowMultipleComponent]
public class MissionClearNotificationUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("監視するMissionManager2D。未設定なら同じシーンから自動取得します。")]
    [SerializeField] private MissionManager2D missionManager;

    [Tooltip("実際に表示する通知UIのRoot。通常はこのコンポーネントの子Objectを設定します。")]
    [SerializeField] private GameObject visualRoot;

    [Tooltip("上下移動させるRectTransform。通常はVisual Root自身を設定します。")]
    [SerializeField] private RectTransform notificationRect;

    [Tooltip("透明度を制御するCanvasGroup。通常はVisual Rootに付けます。")]
    [SerializeField] private CanvasGroup canvasGroup;

    [Tooltip("MISSION CLEAR とミッション名を表示するTextMeshProUGUI。")]
    [SerializeField] private TMP_Text notificationText;

    [Header("表示文")]
    [SerializeField] private string clearLabel = "MISSION CLEAR";

    [Tooltip("{0}=Clear Label / {1}=Mission名")]
    [SerializeField, TextArea(2, 4)]
    private string messageFormat = "{0}\n{1}";

    [SerializeField] private bool showMissionName = true;

    [Header("降りてくる演出")]
    [Tooltip("停止位置より何px上から開始するか。")]
    [SerializeField, Min(0f)] private float startOffsetY = 180f;

    [Tooltip("上から停止位置まで降りてくる時間。")]
    [SerializeField, Min(0.01f)] private float dropDuration = 0.45f;

    [Tooltip("停止位置で表示し続ける時間。")]
    [SerializeField, Min(0f)] private float holdDuration = 1.4f;

    [Tooltip("停止後、透明になるまでの時間。")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.8f;

    [Tooltip("降下中に少しフェードインさせます。")]
    [SerializeField] private bool fadeInWhileDropping = true;

    [Header("クリア音")]
    [Tooltip("未設定ならこのGameObjectからAudioSourceを探します。")]
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip missionClearSound;

    [SerializeField, Range(0f, 1f)]
    private float missionClearVolume = 1f;

    [Header("時間")]
    [Tooltip("Time.timeScaleが0でも通知演出を動かします。通常はON推奨です。")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    private readonly Queue<MissionDefinition2D> pendingMissions =
        new Queue<MissionDefinition2D>();

    private Coroutine notificationCoroutine;
    private Vector2 restingAnchoredPosition;
    private bool initialized;
    private bool subscribed;

    private void Awake()
    {
        FindReferences();
        InitializeVisual();
        HideImmediate();
    }

    private void OnEnable()
    {
        FindReferences();
        Subscribe();
    }

    private void Start()
    {
        FindReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();

        if (notificationCoroutine != null)
        {
            StopCoroutine(notificationCoroutine);
            notificationCoroutine = null;
        }

        pendingMissions.Clear();
        HideImmediate();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void FindReferences()
    {
        if (missionManager == null)
        {
            missionManager =
                FindAnyObjectByType<MissionManager2D>(
                    FindObjectsInactive.Include
                );
        }

        if (visualRoot == null)
        {
            visualRoot = gameObject;
        }

        if (notificationRect == null && visualRoot != null)
        {
            notificationRect =
                visualRoot.GetComponent<RectTransform>();
        }

        if (canvasGroup == null && visualRoot != null)
        {
            canvasGroup =
                visualRoot.GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null && visualRoot != null)
        {
            canvasGroup =
                visualRoot.AddComponent<CanvasGroup>();
        }

        if (notificationText == null && visualRoot != null)
        {
            notificationText =
                visualRoot.GetComponentInChildren<TMP_Text>(true);
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
    }

    private void InitializeVisual()
    {
        if (initialized)
        {
            return;
        }

        if (notificationRect != null)
        {
            restingAnchoredPosition =
                notificationRect.anchoredPosition;
        }

        initialized = true;
    }

    private void Subscribe()
    {
        if (subscribed || missionManager == null)
        {
            return;
        }

        missionManager.MissionCompleted +=
            HandleMissionCompleted;

        subscribed = true;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[MissionClearNotificationUI] MissionManager2Dを監視開始: {missionManager.name}",
                this
            );
        }
    }

    private void Unsubscribe()
    {
        if (!subscribed)
        {
            return;
        }

        if (missionManager != null)
        {
            missionManager.MissionCompleted -=
                HandleMissionCompleted;
        }

        subscribed = false;
    }

    private void HandleMissionCompleted(MissionDefinition2D mission)
    {
        if (mission == null)
        {
            return;
        }

        pendingMissions.Enqueue(mission);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[MissionClearNotificationUI] クリア通知を追加: {mission.DisplayName}",
                this
            );
        }

        if (notificationCoroutine == null)
        {
            notificationCoroutine =
                StartCoroutine(ProcessNotificationQueue());
        }
    }

    private IEnumerator ProcessNotificationQueue()
    {
        while (pendingMissions.Count > 0)
        {
            MissionDefinition2D mission =
                pendingMissions.Dequeue();

            yield return ShowNotificationRoutine(mission);
        }

        notificationCoroutine = null;
    }

    private IEnumerator ShowNotificationRoutine(MissionDefinition2D mission)
    {
        FindReferences();
        InitializeVisual();

        if (visualRoot != null && !visualRoot.activeSelf)
        {
            visualRoot.SetActive(true);
        }

        RefreshText(mission);

        Vector2 startPosition =
            restingAnchoredPosition +
            Vector2.up * Mathf.Max(0f, startOffsetY);

        if (notificationRect != null)
        {
            notificationRect.anchoredPosition = startPosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = fadeInWhileDropping ? 0f : 1f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        PlayClearSound();

        float dropElapsed = 0f;
        float safeDropDuration = Mathf.Max(0.01f, dropDuration);

        while (dropElapsed < safeDropDuration)
        {
            dropElapsed += GetDeltaTime();
            float t = Mathf.Clamp01(dropElapsed / safeDropDuration);
            float easedT = EaseOutCubic(t);

            if (notificationRect != null)
            {
                notificationRect.anchoredPosition =
                    Vector2.LerpUnclamped(
                        startPosition,
                        restingAnchoredPosition,
                        easedT
                    );
            }

            if (canvasGroup != null && fadeInWhileDropping)
            {
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            }

            yield return null;
        }

        if (notificationRect != null)
        {
            notificationRect.anchoredPosition = restingAnchoredPosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        if (holdDuration > 0f)
        {
            float holdElapsed = 0f;
            while (holdElapsed < holdDuration)
            {
                holdElapsed += GetDeltaTime();
                yield return null;
            }
        }

        float fadeElapsed = 0f;
        float safeFadeDuration = Mathf.Max(0.01f, fadeDuration);

        while (fadeElapsed < safeFadeDuration)
        {
            fadeElapsed += GetDeltaTime();
            float t = Mathf.Clamp01(fadeElapsed / safeFadeDuration);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            }

            yield return null;
        }

        HideImmediate();
    }

    private void RefreshText(MissionDefinition2D mission)
    {
        if (notificationText == null)
        {
            return;
        }

        string missionName = mission != null
            ? mission.DisplayName
            : string.Empty;

        if (!showMissionName)
        {
            notificationText.text = clearLabel ?? string.Empty;
            return;
        }

        string format = string.IsNullOrWhiteSpace(messageFormat)
            ? "{0}\n{1}"
            : messageFormat;

        try
        {
            notificationText.text = string.Format(
                format,
                clearLabel ?? string.Empty,
                missionName
            );
        }
        catch
        {
            notificationText.text = $"{clearLabel}\n{missionName}";
        }
    }

    private void PlayClearSound()
    {
        if (missionClearSound == null)
        {
            return;
        }

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (audioSource != null)
        {
            audioSource.PlayOneShot(
                missionClearSound,
                Mathf.Clamp01(missionClearVolume)
            );
            return;
        }

        GameObject temp = new GameObject("MissionClearTemporaryAudio");
        AudioSource source = temp.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.volume = Mathf.Clamp01(missionClearVolume);
        source.clip = missionClearSound;
        source.Play();

        Destroy(temp, Mathf.Max(0.1f, missionClearSound.length + 0.1f));
    }

    private void HideImmediate()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (notificationRect != null)
        {
            notificationRect.anchoredPosition = restingAnchoredPosition;
        }

        if (visualRoot != null && visualRoot != gameObject)
        {
            visualRoot.SetActive(false);
        }
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
    }

    private static float EaseOutCubic(float t)
    {
        float x = 1f - Mathf.Clamp01(t);
        return 1f - x * x * x;
    }

    [ContextMenu("Test Mission Clear Animation")]
    private void TestAnimation()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning(
                "[MissionClearNotificationUI] テストはPlay Mode中に実行してください。",
                this
            );
            return;
        }

        if (notificationCoroutine != null)
        {
            return;
        }

        notificationCoroutine = StartCoroutine(TestNotificationRoutine());
    }

    private IEnumerator TestNotificationRoutine()
    {
        FindReferences();
        InitializeVisual();

        if (visualRoot != null && !visualRoot.activeSelf)
        {
            visualRoot.SetActive(true);
        }

        if (notificationText != null)
        {
            notificationText.text = $"{clearLabel}\nテストミッション";
        }

        Vector2 startPosition =
            restingAnchoredPosition +
            Vector2.up * Mathf.Max(0f, startOffsetY);

        if (notificationRect != null)
        {
            notificationRect.anchoredPosition = startPosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = fadeInWhileDropping ? 0f : 1f;
        }

        PlayClearSound();

        float elapsed = 0f;
        float duration = Mathf.Max(0.01f, dropDuration);

        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = EaseOutCubic(t);

            if (notificationRect != null)
            {
                notificationRect.anchoredPosition =
                    Vector2.LerpUnclamped(
                        startPosition,
                        restingAnchoredPosition,
                        easedT
                    );
            }

            if (canvasGroup != null && fadeInWhileDropping)
            {
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, t);
            }

            yield return null;
        }

        if (notificationRect != null)
        {
            notificationRect.anchoredPosition = restingAnchoredPosition;
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
        }

        float holdElapsed = 0f;
        while (holdElapsed < holdDuration)
        {
            holdElapsed += GetDeltaTime();
            yield return null;
        }

        elapsed = 0f;
        duration = Mathf.Max(0.01f, fadeDuration);

        while (elapsed < duration)
        {
            elapsed += GetDeltaTime();
            float t = Mathf.Clamp01(elapsed / duration);

            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f - Mathf.SmoothStep(0f, 1f, t);
            }

            yield return null;
        }

        HideImmediate();
        notificationCoroutine = null;
    }

    private void OnValidate()
    {
        startOffsetY = Mathf.Max(0f, startOffsetY);
        dropDuration = Mathf.Max(0.01f, dropDuration);
        holdDuration = Mathf.Max(0f, holdDuration);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        missionClearVolume = Mathf.Clamp01(missionClearVolume);
    }
}
