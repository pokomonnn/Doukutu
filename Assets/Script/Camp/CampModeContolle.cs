using System.Collections;
using UnityEngine;

/// <summary>
/// キャンプへの入場・退出、暗転、プレイヤー移動、カメラズーム、
/// 武器操作停止、睡眠によるSAN・食料・水分の変化をまとめて管理します。
/// シーンに1つだけ置いて使います。
/// </summary>
[DisallowMultipleComponent]
public class CampModeController : MonoBehaviour
{
    [Header("プレイヤー参照")]
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private Rigidbody2D playerRigidbody;

    [SerializeField]
    private PlayerEquipmentVisualController equipmentVisualController;

    [SerializeField] private PlayerDeathHandler playerDeathHandler;
    [SerializeField] private Animator playerAnimator;

    [Header("SAN・サバイバル参照")]
    [Tooltip("未設定ならPlayerから自動取得します")]
    [SerializeField] private PlayerSanityController sanityController;

    [Tooltip("未設定ならPlayerから自動取得します")]
    [SerializeField] private PlayerSurvivalController survivalController;

    [Header("カメラ")]
    [Tooltip("未設定ならMain Cameraを使います。通常の2D Camera用です")]
    [SerializeField] private Camera targetCamera;

    [Tooltip("キャンプ中のOrthographic Size。小さいほどズームします")]
    [SerializeField, Min(0.1f)] private float campOrthographicSize = 3.8f;

    [SerializeField] private bool zoomCameraWhileCamping = true;

    [Header("暗転UI")]
    [Tooltip("Canvas内の黒い全面Imageに付けたCanvasGroupを設定します")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [SerializeField, Min(0.01f)] private float fadeDuration = 0.35f;

    [SerializeField, Min(0f)]
    private float blackScreenHoldDuration = 0.05f;

    [Header("キャンプUI")]
    [Tooltip("キャンプ中だけ表示するPanel")]
    [SerializeField] private GameObject campPanel;

    [Header("睡眠時間選択UI")]
    [Tooltip("SleepTimePanelに付けたCampSleepTimeSelectorUIを設定します")]
    [SerializeField]
    private CampSleepTimeSelectorUI sleepTimeSelectorUI;

    [Tooltip("選べる最短の睡眠時間（ゲーム内時間）")]
    [SerializeField, Min(1f)] private float minimumSleepHours = 1f;

    [Tooltip("選べる最長の睡眠時間（ゲーム内時間）")]
    [SerializeField, Min(1f)] private float maximumSleepHours = 12f;

    [Tooltip("睡眠時間選択を開いた時に最初から選ばれている時間")]
    [SerializeField, Min(1f)] private float defaultSleepHours = 8f;

    [Header("入力")]
    [SerializeField] private KeyCode returnKey = KeyCode.G;

    [Tooltip("キャンプ中に睡眠時間の選択画面を開くキー")]
    [SerializeField] private KeyCode sleepKey = KeyCode.Q;

    [Header("睡眠：ゲーム内時間ごとの効果")]
    [Tooltip("ゲーム内で1時間眠るごとに回復するSAN値")]
    [SerializeField, Min(0f)] private float sanityRestorePerGameHour = 10f;

    [Tooltip("ゲーム内で1時間眠るごとに減る食料値")]
    [SerializeField, Min(0f)] private float foodDecreasePerGameHour = 1f;

    [Tooltip("ゲーム内で1時間眠るごとに減る水分値")]
    [SerializeField, Min(0f)] private float waterDecreasePerGameHour = 2f;

    [Tooltip("ゲーム内1時間あたりの、実際の暗転待機時間（秒）。例：0.4なら8時間で3.2秒")]
    [SerializeField, Min(0f)] private float sleepSecondsPerGameHour = 0.4f;

    [Tooltip("睡眠中はキャンプ用UIを一時的に隠します")]
    [SerializeField] private bool hideCampPanelWhileSleeping = true;

    [Tooltip("眠るポーズへ切り替えるTrigger名。不要なら空欄でOKです")]
    [SerializeField] private string sleepTriggerName = "";

    [Tooltip("起きるポーズへ切り替えるTrigger名。不要なら空欄でOKです")]
    [SerializeField] private string wakeTriggerName = "";

    [Header("アニメーション")]
    [Tooltip("キャンプへ入った時の休憩ポーズTrigger名。不要なら空欄でOKです")]
    [SerializeField] private string restTriggerName = "";

    [Tooltip("通常ポーズへ戻すTrigger名。不要なら空欄でOKです")]
    [SerializeField] private string returnTriggerName = "";

    public bool IsCamping { get; private set; }
    public bool IsBusy { get; private set; }
    public bool IsSleeping { get; private set; }
    public bool IsSleepTimeSelectionOpen { get; private set; }

    public CampSiteInteractable CurrentCampSite { get; private set; }

    // 睡眠演出用。必要になった時、UIから進捗表示にも使えます。
    public float SleepProgress { get; private set; }
    public float LastSleepHours { get; private set; }

    // CampSleepTimeSelectorUI がSlider設定に使います。
    public float MinimumSleepHours => minimumSleepHours;
    public float MaximumSleepHours => maximumSleepHours;
    public float DefaultSleepHours => defaultSleepHours;

    private bool wasPlayerMoveEnabledBeforeCamp;
    private bool playerPositionWasSaved;
    private Vector3 savedPlayerPosition;
    private Quaternion savedPlayerRotation;
    private float savedCameraOrthographicSize;
    private bool cameraSizeWasSaved;

    private void Awake()
    {
        FindReferences();
        SetupFadeCanvas();

        if (campPanel != null)
        {
            campPanel.SetActive(false);
        }

        sleepTimeSelectorUI?.HideSelectionImmediately();
    }

    private void Update()
    {
        if (!IsCamping || IsBusy)
        {
            return;
        }

        // 睡眠時間を選んでいる最中は、G / Escで選択画面だけ閉じます。
        if (IsSleepTimeSelectionOpen)
        {
            if (Input.GetKeyDown(returnKey) ||
                Input.GetKeyDown(KeyCode.Escape))
            {
                CloseSleepTimeSelection();
            }

            return;
        }

        if (Input.GetKeyDown(returnKey))
        {
            ExitCamp();
            return;
        }

        if (Input.GetKeyDown(sleepKey))
        {
            OpenSleepTimeSelection();
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (IsCamping || IsBusy)
        {
            ForceRestorePlayerState();
        }

        SetFadeAlpha(0f);
    }

    public void EnterCamp(CampSiteInteractable campSite)
    {
        if (campSite == null ||
            campSite.CampRestPoint == null ||
            IsCamping ||
            IsBusy)
        {
            return;
        }

        FindReferences();

        if (playerMove == null)
        {
            Debug.LogWarning(
                "CampModeController: PlayerMove が見つかりません。",
                this
            );
            return;
        }

        if (playerDeathHandler != null &&
            playerDeathHandler.IsDead)
        {
            return;
        }

        CurrentCampSite = campSite;
        savedPlayerPosition = playerMove.transform.position;
        savedPlayerRotation = playerMove.transform.rotation;
        playerPositionWasSaved = true;

        SaveCameraSize();
        LockPlayerForCamp();

        // キャンプ開始直後から射撃・照準・リロードを止める
        equipmentVisualController?.SetWeaponControlsEnabled(false);

        StartCoroutine(EnterRoutine());
    }

    public void ExitCamp()
    {
        if (!IsCamping || IsBusy)
        {
            return;
        }

        // 睡眠時間選択中のGは、キャンプ終了ではなく選択のキャンセル。
        if (IsSleepTimeSelectionOpen)
        {
            CloseSleepTimeSelection();
            return;
        }

        if (playerDeathHandler != null &&
            playerDeathHandler.IsDead)
        {
            return;
        }

        StartCoroutine(ExitRoutine());
    }

    /// <summary>
    /// 旧Sleepボタンとの互換用です。即時に眠らず、睡眠時間の選択画面を開きます。
    /// </summary>
    public void Sleep()
    {
        OpenSleepTimeSelection();
    }

    /// <summary>
    /// キャンプ中に睡眠時間の選択画面を開きます。QキーまたはUIボタンから呼べます。
    /// </summary>
    public void OpenSleepTimeSelection()
    {
        if (!IsCamping ||
            IsBusy ||
            IsSleeping ||
            IsSleepTimeSelectionOpen)
        {
            return;
        }

        if (playerDeathHandler != null &&
            playerDeathHandler.IsDead)
        {
            return;
        }

        FindReferences();

        if (sleepTimeSelectorUI == null)
        {
            Debug.LogWarning(
                "CampModeController: Sleep Time Selector UI が設定されていません。",
                this
            );
            return;
        }

        IsSleepTimeSelectionOpen = true;
        sleepTimeSelectorUI.ShowSelection();
    }

    /// <summary>
    /// 睡眠時間の選択画面を閉じます。G / Escやキャンセルボタンから呼べます。
    /// </summary>
    public void CloseSleepTimeSelection()
    {
        IsSleepTimeSelectionOpen = false;
        sleepTimeSelectorUI?.HideSelection();
    }

    /// <summary>
    /// 指定したゲーム内時間だけ眠ります。
    /// CampSleepTimeSelectorUI の決定ボタンから呼ばれます。
    /// </summary>
    public void SleepForHours(float gameHours)
    {
        if (!IsCamping || IsBusy || IsSleeping)
        {
            return;
        }

        if (playerDeathHandler != null &&
            playerDeathHandler.IsDead)
        {
            return;
        }

        gameHours = Mathf.Clamp(
            Mathf.Round(gameHours),
            minimumSleepHours,
            maximumSleepHours
        );

        CloseSleepTimeSelection();
        LastSleepHours = gameHours;

        FindReferences();
        StartCoroutine(SleepRoutine(gameHours));
    }

    // ButtonのOnClickから使いやすいプリセットです。
    public void SleepFor1Hour() => SleepForHours(1f);
    public void SleepFor3Hours() => SleepForHours(3f);
    public void SleepFor6Hours() => SleepForHours(6f);
    public void SleepFor8Hours() => SleepForHours(8f);

    private IEnumerator EnterRoutine()
    {
        IsBusy = true;

        yield return FadeTo(1f);
        yield return WaitBlackScreenHold();

        if (CurrentCampSite != null &&
            CurrentCampSite.CampRestPoint != null &&
            playerMove != null)
        {
            Transform restPoint = CurrentCampSite.CampRestPoint;

            playerMove.transform.position = restPoint.position;
            playerMove.transform.rotation = restPoint.rotation;

            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector2.zero;
                playerRigidbody.angularVelocity = 0f;
            }
        }

        ApplyCampCameraZoom();
        PlayAnimatorTrigger(restTriggerName);

        if (campPanel != null)
        {
            campPanel.SetActive(true);
        }

        yield return FadeTo(0f);

        IsCamping = true;
        IsBusy = false;
    }

    private IEnumerator SleepRoutine(float gameHours)
    {
        IsBusy = true;
        IsSleeping = true;
        SleepProgress = 0f;

        if (hideCampPanelWhileSleeping && campPanel != null)
        {
            campPanel.SetActive(false);
        }

        PlayAnimatorTrigger(sleepTriggerName);

        yield return FadeTo(1f);

        float visualSleepDuration =
            gameHours * sleepSecondsPerGameHour;

        if (visualSleepDuration > 0f)
        {
            float elapsed = 0f;

            while (elapsed < visualSleepDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                SleepProgress = Mathf.Clamp01(
                    elapsed / visualSleepDuration
                );
                yield return null;
            }
        }

        SleepProgress = 1f;

        ApplySleepEffects(gameHours);

        PlayAnimatorTrigger(wakeTriggerName);

        if (campPanel != null)
        {
            campPanel.SetActive(true);
        }

        yield return FadeTo(0f);

        SleepProgress = 0f;
        IsSleeping = false;
        IsBusy = false;
    }

    private void ApplySleepEffects(float gameHours)
    {
        float sanityRestoreAmount =
            sanityRestorePerGameHour * gameHours;

        float foodDecreaseAmount =
            foodDecreasePerGameHour * gameHours;

        float waterDecreaseAmount =
            waterDecreasePerGameHour * gameHours;

        // 各Controller側で0〜最大値の範囲に自動調整されます。
        sanityController?.RestoreSanity(sanityRestoreAmount);
        survivalController?.DrainFood(foodDecreaseAmount);
        survivalController?.DrainWater(waterDecreaseAmount);
    }

    private IEnumerator ExitRoutine()
    {
        IsBusy = true;

        CloseSleepTimeSelection();

        if (campPanel != null)
        {
            campPanel.SetActive(false);
        }

        yield return FadeTo(1f);
        yield return WaitBlackScreenHold();

        if (playerMove != null && playerPositionWasSaved)
        {
            playerMove.transform.position = savedPlayerPosition;
            playerMove.transform.rotation = savedPlayerRotation;

            if (playerRigidbody != null)
            {
                playerRigidbody.linearVelocity = Vector2.zero;
                playerRigidbody.angularVelocity = 0f;
            }
        }

        RestoreCameraZoom();
        PlayAnimatorTrigger(returnTriggerName);

        // 死亡していない時だけ、キャンプ前の操作状態へ戻す
        if (playerDeathHandler == null ||
            !playerDeathHandler.IsDead)
        {
            equipmentVisualController?.SetWeaponControlsEnabled(true);
            UnlockPlayerAfterCamp();
        }

        CurrentCampSite = null;
        playerPositionWasSaved = false;
        IsCamping = false;

        yield return FadeTo(0f);

        IsBusy = false;
    }

    private void LockPlayerForCamp()
    {
        if (playerMove == null)
        {
            return;
        }

        wasPlayerMoveEnabledBeforeCamp = playerMove.enabled;
        playerMove.enabled = false;

        if (playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }
    }

    private void UnlockPlayerAfterCamp()
    {
        if (playerMove != null &&
            wasPlayerMoveEnabledBeforeCamp)
        {
            playerMove.enabled = true;
        }

        wasPlayerMoveEnabledBeforeCamp = false;
    }

    private void ForceRestorePlayerState()
    {
        CloseSleepTimeSelection();

        if (campPanel != null)
        {
            campPanel.SetActive(false);
        }

        RestoreCameraZoom();

        if (playerMove != null && playerPositionWasSaved)
        {
            playerMove.transform.position = savedPlayerPosition;
            playerMove.transform.rotation = savedPlayerRotation;
        }

        if (playerDeathHandler == null ||
            !playerDeathHandler.IsDead)
        {
            equipmentVisualController?.SetWeaponControlsEnabled(true);
            UnlockPlayerAfterCamp();
        }

        CurrentCampSite = null;
        playerPositionWasSaved = false;
        IsCamping = false;
        IsBusy = false;
        IsSleeping = false;
        IsSleepTimeSelectionOpen = false;
        SleepProgress = 0f;
    }

    private IEnumerator FadeTo(float targetAlpha)
    {
        if (fadeCanvasGroup == null)
        {
            yield break;
        }

        float startAlpha = fadeCanvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;

            float progress = Mathf.Clamp01(elapsed / fadeDuration);

            SetFadeAlpha(
                Mathf.Lerp(startAlpha, targetAlpha, progress)
            );

            yield return null;
        }

        SetFadeAlpha(targetAlpha);
    }

    private IEnumerator WaitBlackScreenHold()
    {
        if (blackScreenHoldDuration <= 0f)
        {
            yield break;
        }

        yield return new WaitForSecondsRealtime(
            blackScreenHoldDuration
        );
    }

    private void SetupFadeCanvas()
    {
        if (fadeCanvasGroup == null)
        {
            return;
        }

        fadeCanvasGroup.alpha = 0f;
        fadeCanvasGroup.blocksRaycasts = false;
        fadeCanvasGroup.interactable = false;
    }

    private void SetFadeAlpha(float alpha)
    {
        if (fadeCanvasGroup == null)
        {
            return;
        }

        fadeCanvasGroup.alpha = Mathf.Clamp01(alpha);
        fadeCanvasGroup.blocksRaycasts = alpha > 0.01f;
    }

    private void ApplyCampCameraZoom()
    {
        if (!zoomCameraWhileCamping ||
            targetCamera == null ||
            !targetCamera.orthographic)
        {
            return;
        }

        targetCamera.orthographicSize = campOrthographicSize;
    }

    private void SaveCameraSize()
    {
        if (!zoomCameraWhileCamping ||
            targetCamera == null ||
            !targetCamera.orthographic)
        {
            cameraSizeWasSaved = false;
            return;
        }

        savedCameraOrthographicSize =
            targetCamera.orthographicSize;

        cameraSizeWasSaved = true;
    }

    private void RestoreCameraZoom()
    {
        if (!cameraSizeWasSaved ||
            targetCamera == null ||
            !targetCamera.orthographic)
        {
            return;
        }

        targetCamera.orthographicSize =
            savedCameraOrthographicSize;

        cameraSizeWasSaved = false;
    }

    private void PlayAnimatorTrigger(string triggerName)
    {
        if (playerAnimator == null ||
            string.IsNullOrWhiteSpace(triggerName))
        {
            return;
        }

        playerAnimator.SetTrigger(triggerName);
    }

    private void FindReferences()
    {
        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>();
        }

        if (playerRigidbody == null && playerMove != null)
        {
            playerRigidbody = playerMove.GetComponent<Rigidbody2D>();
        }

        if (equipmentVisualController == null && playerMove != null)
        {
            equipmentVisualController =
                playerMove.GetComponent<PlayerEquipmentVisualController>();
        }

        if (playerDeathHandler == null && playerMove != null)
        {
            playerDeathHandler =
                playerMove.GetComponent<PlayerDeathHandler>();
        }

        if (playerAnimator == null && playerMove != null)
        {
            playerAnimator =
                playerMove.GetComponentInChildren<Animator>(true);
        }

        if (sanityController == null && playerMove != null)
        {
            sanityController =
                playerMove.GetComponent<PlayerSanityController>();
        }

        if (sanityController == null)
        {
            sanityController =
                FindAnyObjectByType<PlayerSanityController>();
        }

        if (survivalController == null && playerMove != null)
        {
            survivalController =
                playerMove.GetComponent<PlayerSurvivalController>();
        }

        if (survivalController == null)
        {
            survivalController =
                FindAnyObjectByType<PlayerSurvivalController>();
        }

        if (sleepTimeSelectorUI == null)
        {
            sleepTimeSelectorUI =
                FindAnyObjectByType<CampSleepTimeSelectorUI>(
                    FindObjectsInactive.Include
                );
        }

        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }
    }

    private void OnValidate()
    {
        campOrthographicSize = Mathf.Max(0.1f, campOrthographicSize);
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
        blackScreenHoldDuration = Mathf.Max(0f, blackScreenHoldDuration);

        minimumSleepHours = Mathf.Max(1f, minimumSleepHours);
        maximumSleepHours = Mathf.Max(
            minimumSleepHours,
            maximumSleepHours
        );
        defaultSleepHours = Mathf.Clamp(
            defaultSleepHours,
            minimumSleepHours,
            maximumSleepHours
        );

        sanityRestorePerGameHour = Mathf.Max(0f, sanityRestorePerGameHour);
        foodDecreasePerGameHour = Mathf.Max(0f, foodDecreasePerGameHour);
        waterDecreasePerGameHour = Mathf.Max(0f, waterDecreasePerGameHour);
        sleepSecondsPerGameHour = Mathf.Max(0f, sleepSecondsPerGameHour);
    }
}
