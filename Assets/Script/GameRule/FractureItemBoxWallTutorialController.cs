using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Serialization;

/// <summary>
/// 「落下で骨折 → ItemBox紹介Timeline → ItemBoxを開く →
/// 治療アイテムで骨折を治す → 壁登り」の順番を管理します。
///
/// Timelineはカメラ演出だけに使用し、プレイヤーの操作達成待ちは
/// 各コンポーネントのイベントで進行します。
/// </summary>
[DisallowMultipleComponent]
public class FractureItemBoxWallTutorialController : MonoBehaviour
{
    [Serializable]
    public class TutorialMessageEntry
    {
        [TextArea(2, 5)]
        [Tooltip("表示する文章です。空欄の要素はスキップします。")]
        public string message;

        public TutorialMessageEntry()
        {
        }

        public TutorialMessageEntry(string text)
        {
            message = text;
        }
    }

    [Serializable]
    public class TutorialMessageSequence
    {
        [Tooltip("上から順番に表示される文章Listです。")]
        public List<TutorialMessageEntry> messages =
            new List<TutorialMessageEntry>();

        [Tooltip("次の文章へ切り替えるまでの秒数です。最後の文章は次の操作が完了するまで残ります。")]
        [Min(0f)]
        public float intervalSeconds = 3f;
    }

    public enum TutorialStep
    {
        WaitingForFracture,
        PlayingItemBoxTimeline,
        MoveToItemBox,
        UseFractureCureItem,
        ClimbWall,
        Completed
    }

    [Header("進行に必要な参照")]
    [Tooltip("PlayerのPlayerStatusConditionControllerです。")]
    [SerializeField]
    private PlayerStatusConditionController statusConditionController;

    [Tooltip("チュートリアルで開かせるItemBoxです。")]
    [SerializeField]
    private ItemBoxInteractable tutorialItemBox;

    [Tooltip("チュートリアルで登らせるPlayer側のWallClimbControllerです。")]
    [SerializeField]
    private WallClimbController wallClimbController;

    [Tooltip("説明文と矢印を表示するTutorialPromptUIです。")]
    [SerializeField]
    private TutorialPromptUI tutorialPromptUI;

    [Header("Timeline")]
    [Tooltip("骨折した時にItemBoxを映すTimelineのPlayableDirectorです。")]
    [SerializeField]
    private PlayableDirector itemBoxCameraDirector;

    [Tooltip("Timeline中はPlayerMoveを停止します。")]
    [SerializeField]
    private PlayerMove playerMove;

    [Tooltip("Timeline中の照準・射撃・リロードを止めます。")]
    [SerializeField]
    private PlayerEquipmentVisualController equipmentVisualController;

    [Tooltip("Timeline中に追加で停止するスクリプトです。StoneThrower、PlayerProneController、InventoryPanelToggleなどを必要に応じて設定します。")]
    [SerializeField]
    private Behaviour[] additionalScriptsToDisableDuringTimeline;

    [Tooltip("Timeline開始時にPlayerの速度を0へ戻します。")]
    [SerializeField]
    private bool stopPlayerVelocityDuringTimeline = true;

    [Header("矢印の位置")]
    [Tooltip("矢印を表示する基準位置です。未設定ならPlayerMoveのTransformを使います。")]
    [SerializeField]
    private Transform arrowOrigin;

    [Tooltip("ItemBoxの上などに置いた矢印ターゲットです。未設定ならItemBox本体を使います。")]
    [SerializeField]
    private Transform itemBoxArrowTarget;

    [Tooltip("壁の近くに置いた矢印ターゲットです。未設定なら壁登り説明は文章だけになります。")]
    [SerializeField]
    private Transform wallArrowTarget;

    [Header("表示する文章List")]
    [Tooltip("Timeline終了後、ItemBoxへ移動させる段階の文章です。上から順番に表示されます。")]
    [SerializeField]
    private TutorialMessageSequence moveToItemBoxMessages =
        new TutorialMessageSequence
        {
            intervalSeconds = 3f,
            messages = new List<TutorialMessageEntry>
            {
                new TutorialMessageEntry("骨折してしまったようだ。"),
                new TutorialMessageEntry("アイテムボックスを確認しよう。")
            }
        };

    [Tooltip("ItemBoxを開いた後、骨折治療を案内する段階の文章です。上から順番に表示されます。")]
    [SerializeField]
    private TutorialMessageSequence useCureItemMessages =
        new TutorialMessageSequence
        {
            intervalSeconds = 3f,
            messages = new List<TutorialMessageEntry>
            {
                new TutorialMessageEntry("アイテムを回収しよう。"),
                new TutorialMessageEntry("naegiを使用して骨折を治そう。")
            }
        };

    [Tooltip("骨折治療後、壁登りを案内する段階の文章です。Listを増やせます。")]
    [SerializeField]
    private TutorialMessageSequence climbWallMessages =
        new TutorialMessageSequence
        {
            intervalSeconds = 3f,
            messages = new List<TutorialMessageEntry>
            {
                new TutorialMessageEntry(
                    "壁に向かってA＋W、またはD＋Wを押して壁を登ろう。"
                )
            }
        };

    [Tooltip("完了時に表示する文章です。Listを増やせます。")]
    [SerializeField]
    private TutorialMessageSequence completedMessages =
        new TutorialMessageSequence
        {
            intervalSeconds = 3f,
            messages = new List<TutorialMessageEntry>
            {
                new TutorialMessageEntry("チュートリアル完了！")
            }
        };

    [Tooltip("チュートリアル完了Listの最後の文章を表示する秒数です。0なら最後の文章を表示し続けます。")]
    [SerializeField, Min(0f)]
    private float completedMessageDuration = 2.5f;

    [Tooltip("文章切り替えの待機時間にTime.timeScaleの影響を受けないようにします。")]
    [SerializeField]
    private bool useUnscaledMessageTime = true;

    [Header("文章表示サウンド")]
    [Tooltip("文章が表示・切り替えされた時に共通SEを鳴らします。")]
    [SerializeField]
    private bool playMessageSound = true;

    [Tooltip("すべてのチュートリアル文章で共通して使用するSEです。")]
    [SerializeField]
    private AudioClip messageSound;

    [Tooltip("文章表示SEを再生するAudioSourceです。未設定ならこのControllerのGameObjectから取得し、必要なら自動追加します。")]
    [SerializeField]
    private AudioSource messageAudioSource;

    [SerializeField, Range(0f, 1f)]
    private float messageSoundVolume = 1f;

    [Tooltip("AudioSourceが未設定でMessage Soundが設定されている場合、このControllerのGameObjectへ自動追加します。")]
    [SerializeField]
    private bool autoCreateMessageAudioSource = true;

    // 旧バージョンの単一文章設定を自動移行するために保持します。
    [FormerlySerializedAs("moveToItemBoxMessage")]
    [SerializeField, HideInInspector]
    private string legacyMoveToItemBoxMessage;

    [FormerlySerializedAs("useCureItemMessage")]
    [SerializeField, HideInInspector]
    private string legacyUseCureItemMessage;

    [FormerlySerializedAs("climbWallMessage")]
    [SerializeField, HideInInspector]
    private string legacyClimbWallMessage;

    [FormerlySerializedAs("completedMessage")]
    [SerializeField, HideInInspector]
    private string legacyCompletedMessage;

    [Header("開始・復帰設定")]
    [Tooltip("Controllerが有効になった時点ですでに骨折している場合も開始します。")]
    [SerializeField]
    private bool startIfAlreadyFractured = true;

    [Tooltip("ItemBox案内段階へ進んだ時、対象の箱がすでに開いていれば治療説明へ進みます。")]
    [SerializeField]
    private bool skipMoveStepIfBoxAlreadyOpened = true;

    [Header("デバッグ")]
    [SerializeField]
    private bool showDebugLogs = true;

    [SerializeField]
    private TutorialStep currentStep = TutorialStep.WaitingForFracture;

    public TutorialStep CurrentStep => currentStep;
    public bool IsCompleted => currentStep == TutorialStep.Completed;

    private readonly List<Behaviour> disabledDuringTimeline =
        new List<Behaviour>();

    private Rigidbody2D playerRigidbody;
    private bool playerMoveWasEnabled;
    private bool playerMoveStateCached;
    private bool controlsAreLocked;
    private bool eventsSubscribed;
    private bool timelinePlaybackStarted;
    private Coroutine messageSequenceCoroutine;

    private void Awake()
    {
        EnsureMessageSequences();
        FindReferences();
        EnsureMessageAudioSource();
    }

    private void OnEnable()
    {
        EnsureMessageSequences();
        FindReferences();
        EnsureMessageAudioSource();
        SubscribeEvents();
    }

    private void Start()
    {
        FindReferences();
        SubscribeEvents();

        if (startIfAlreadyFractured &&
            currentStep == TutorialStep.WaitingForFracture &&
            IsPlayerFractured())
        {
            Log("開始時点ですでに骨折していたため、チュートリアルを開始します。");
            StartItemBoxTimeline();
        }
    }

    private void Update()
    {
        if (currentStep != TutorialStep.PlayingItemBoxTimeline ||
            !timelinePlaybackStarted ||
            itemBoxCameraDirector == null)
        {
            return;
        }

        double duration = itemBoxCameraDirector.duration;
        bool reachedEnd =
            duration > 0d &&
            !double.IsInfinity(duration) &&
            itemBoxCameraDirector.time >= duration - 0.01d;

        bool stoppedAfterStarting =
            itemBoxCameraDirector.state != PlayState.Playing &&
            itemBoxCameraDirector.time > 0d;

        // DirectorのWrap ModeがHoldの場合でも、最後まで到達したら
        // チュートリアルを次へ進められるようにします。
        if (reachedEnd || stoppedAfterStarting)
        {
            FinishItemBoxTimeline();
        }
    }

    private void OnDisable()
    {
        StopMessageSequence();
        UnsubscribeEvents();
        timelinePlaybackStarted = false;
        UnsubscribeDirectorStopped();
        RestorePlayerControls();
    }

    private void OnDestroy()
    {
        StopMessageSequence();
        UnsubscribeEvents();
        timelinePlaybackStarted = false;
        UnsubscribeDirectorStopped();
        RestorePlayerControls();
    }

    private void HandleConditionsAdded(
        StatusConditionType addedConditions)
    {
        if (currentStep != TutorialStep.WaitingForFracture ||
            (addedConditions & StatusConditionType.Fracture) == 0)
        {
            return;
        }

        Log("骨折追加イベントを検知しました。");
        StartItemBoxTimeline();
    }

    private void HandleConditionsRemoved(
        StatusConditionType removedConditions)
    {
        if ((removedConditions & StatusConditionType.Fracture) == 0)
        {
            return;
        }

        Log($"骨折解除イベントを検知しました。現在Step={currentStep}");

        if (currentStep == TutorialStep.UseFractureCureItem)
        {
            StartWallClimbStep();
        }
    }

    private void HandleItemBoxOpened(
        ItemBoxInteractable openedItemBox)
    {
        if (currentStep != TutorialStep.MoveToItemBox ||
            openedItemBox == null ||
            openedItemBox != tutorialItemBox)
        {
            return;
        }

        Log("チュートリアル対象のItemBoxが開かれました。");
        StartUseCureItemStep();
    }

    private void HandleWallClimbStarted(
        WallClimbController source)
    {
        if (currentStep != TutorialStep.ClimbWall ||
            source == null ||
            source != wallClimbController)
        {
            return;
        }

        CompleteTutorial();
    }

    private void StartItemBoxTimeline()
    {
        if (currentStep != TutorialStep.WaitingForFracture)
        {
            return;
        }

        SetStep(TutorialStep.PlayingItemBoxTimeline);
        tutorialPromptUI?.HideImmediately();
        LockPlayerControls();

        if (itemBoxCameraDirector == null ||
            itemBoxCameraDirector.playableAsset == null)
        {
            LogWarning(
                "ItemBox Camera DirectorまたはPlayable Assetが未設定です。" +
                "Timelineを省略してItemBox案内へ進みます。"
            );

            timelinePlaybackStarted = false;
            RestorePlayerControls();
            StartMoveToItemBoxStep();
            return;
        }

        UnsubscribeDirectorStopped();
        itemBoxCameraDirector.stopped += HandleTimelineStopped;

        itemBoxCameraDirector.time = 0d;
        itemBoxCameraDirector.Evaluate();
        itemBoxCameraDirector.Play();
        timelinePlaybackStarted = true;

        Log(
            $"ItemBox紹介Timelineを再生しました。" +
            $"Asset={itemBoxCameraDirector.playableAsset.name}"
        );
    }

    private void HandleTimelineStopped(PlayableDirector director)
    {
        if (director != itemBoxCameraDirector)
        {
            return;
        }

        FinishItemBoxTimeline();
    }

    private void FinishItemBoxTimeline()
    {
        if (currentStep != TutorialStep.PlayingItemBoxTimeline)
        {
            return;
        }

        timelinePlaybackStarted = false;
        UnsubscribeDirectorStopped();
        RestorePlayerControls();

        Log("ItemBox紹介Timelineが終了しました。");
        StartMoveToItemBoxStep();
    }

    private void StartMoveToItemBoxStep()
    {
        SetStep(TutorialStep.MoveToItemBox);

        if (skipMoveStepIfBoxAlreadyOpened &&
            tutorialItemBox != null &&
            tutorialItemBox.WasOpened)
        {
            Log("対象ItemBoxはすでに開封済みのため、治療説明へ進みます。");
            StartUseCureItemStep();
            return;
        }

        Transform target = itemBoxArrowTarget;

        if (target == null && tutorialItemBox != null)
        {
            target = tutorialItemBox.transform;
        }

        ShowTutorialMessageSequence(moveToItemBoxMessages, target, 0f);
    }

    private void StartUseCureItemStep()
    {
        SetStep(TutorialStep.UseFractureCureItem);
        ShowTutorialMessageSequence(useCureItemMessages, null, 0f);

        // 別の処理ですでに骨折が解除されていた場合も停止しない。
        if (!IsPlayerFractured())
        {
            Log("ItemBoxを開いた時点で骨折が解除済みのため、壁登り説明へ進みます。");
            StartWallClimbStep();
        }
    }

    private void StartWallClimbStep()
    {
        SetStep(TutorialStep.ClimbWall);
        ShowTutorialMessageSequence(climbWallMessages, wallArrowTarget, 0f);
    }

    private void CompleteTutorial()
    {
        SetStep(TutorialStep.Completed);

        if (HasValidMessages(completedMessages))
        {
            ShowTutorialMessageSequence(
                completedMessages,
                null,
                completedMessageDuration
            );
        }
        else
        {
            StopMessageSequence();
            tutorialPromptUI?.Hide();
        }

        Log("壁登り開始を検知しました。チュートリアル完了です。");
    }

    private void ShowTutorialMessageSequence(
        TutorialMessageSequence sequence,
        Transform target,
        float finalMessageDuration)
    {
        StopMessageSequence();

        if (!HasValidMessages(sequence))
        {
            LogWarning(
                $"Step={currentStep} の文章Listが空です。" +
                "InspectorでMessagesへ文章を追加してください。"
            );
            tutorialPromptUI?.Hide();
            return;
        }

        messageSequenceCoroutine = StartCoroutine(
            MessageSequenceRoutine(
                sequence,
                target,
                finalMessageDuration
            )
        );
    }

    private IEnumerator MessageSequenceRoutine(
        TutorialMessageSequence sequence,
        Transform target,
        float finalMessageDuration)
    {
        List<TutorialMessageEntry> validMessages =
            GetValidMessages(sequence);

        for (int index = 0; index < validMessages.Count; index++)
        {
            TutorialMessageEntry entry = validMessages[index];
            bool isLastMessage = index == validMessages.Count - 1;

            ShowTutorialMessage(
                entry.message,
                target,
                isLastMessage ? finalMessageDuration : 0f
            );

            Log(
                $"文章表示: Step={currentStep} / " +
                $"{index + 1}/{validMessages.Count} / " +
                $"Message={entry.message}"
            );

            if (isLastMessage)
            {
                break;
            }

            float waitSeconds = Mathf.Max(
                0f,
                sequence.intervalSeconds
            );

            if (waitSeconds <= 0f)
            {
                yield return null;
                continue;
            }

            float elapsed = 0f;

            while (elapsed < waitSeconds)
            {
                elapsed += useUnscaledMessageTime
                    ? Time.unscaledDeltaTime
                    : Time.deltaTime;

                yield return null;
            }
        }

        messageSequenceCoroutine = null;
    }

    private void ShowTutorialMessage(
        string message,
        Transform target,
        float duration)
    {
        if (tutorialPromptUI == null)
        {
            FindReferences();
        }

        if (tutorialPromptUI == null)
        {
            LogWarning(
                "TutorialPromptUIが見つからないため、文章と矢印を表示できません。"
            );
            return;
        }

        tutorialPromptUI.Show(
            message,
            GetArrowOrigin(),
            target,
            duration
        );

        PlayMessageSound();
    }

    private void PlayMessageSound()
    {
        if (!playMessageSound || messageSound == null)
        {
            return;
        }

        EnsureMessageAudioSource();

        if (messageAudioSource == null)
        {
            LogWarning(
                "Message Soundは設定されていますが、AudioSourceを準備できませんでした。"
            );
            return;
        }

        messageAudioSource.PlayOneShot(
            messageSound,
            messageSoundVolume
        );
    }

    private void EnsureMessageAudioSource()
    {
        if (messageAudioSource != null)
        {
            return;
        }

        messageAudioSource = GetComponent<AudioSource>();

        if (messageAudioSource == null &&
            autoCreateMessageAudioSource &&
            messageSound != null)
        {
            messageAudioSource = gameObject.AddComponent<AudioSource>();
            messageAudioSource.playOnAwake = false;
            messageAudioSource.loop = false;
            messageAudioSource.spatialBlend = 0f;
        }
    }

    private void StopMessageSequence()
    {
        if (messageSequenceCoroutine == null)
        {
            return;
        }

        StopCoroutine(messageSequenceCoroutine);
        messageSequenceCoroutine = null;
    }

    private static bool HasValidMessages(
        TutorialMessageSequence sequence)
    {
        if (sequence == null || sequence.messages == null)
        {
            return false;
        }

        foreach (TutorialMessageEntry entry in sequence.messages)
        {
            if (entry != null &&
                !string.IsNullOrWhiteSpace(entry.message))
            {
                return true;
            }
        }

        return false;
    }

    private static List<TutorialMessageEntry> GetValidMessages(
        TutorialMessageSequence sequence)
    {
        List<TutorialMessageEntry> validMessages =
            new List<TutorialMessageEntry>();

        if (sequence == null || sequence.messages == null)
        {
            return validMessages;
        }

        foreach (TutorialMessageEntry entry in sequence.messages)
        {
            if (entry != null &&
                !string.IsNullOrWhiteSpace(entry.message))
            {
                validMessages.Add(entry);
            }
        }

        return validMessages;
    }

    private void EnsureMessageSequences()
    {
        moveToItemBoxMessages = EnsureSequence(
            moveToItemBoxMessages,
            legacyMoveToItemBoxMessage,
            "骨折してしまったようだ。",
            "アイテムボックスを確認しよう。"
        );

        useCureItemMessages = EnsureSequence(
            useCureItemMessages,
            legacyUseCureItemMessage,
            "アイテムを回収しよう。",
            "naegiを使用して骨折を治そう。"
        );

        climbWallMessages = EnsureSequence(
            climbWallMessages,
            legacyClimbWallMessage,
            "壁に向かってA＋W、またはD＋Wを押して壁を登ろう。"
        );

        completedMessages = EnsureSequence(
            completedMessages,
            legacyCompletedMessage,
            "チュートリアル完了！"
        );
    }

    private static TutorialMessageSequence EnsureSequence(
        TutorialMessageSequence sequence,
        string legacyMessage,
        params string[] defaultMessages)
    {
        if (sequence == null)
        {
            sequence = new TutorialMessageSequence();
        }

        if (sequence.messages == null)
        {
            sequence.messages = new List<TutorialMessageEntry>();
        }

        if (HasValidMessages(sequence))
        {
            return sequence;
        }

        if (!string.IsNullOrWhiteSpace(legacyMessage))
        {
            sequence.messages.Add(
                new TutorialMessageEntry(legacyMessage)
            );
            return sequence;
        }

        if (defaultMessages != null)
        {
            foreach (string defaultMessage in defaultMessages)
            {
                if (!string.IsNullOrWhiteSpace(defaultMessage))
                {
                    sequence.messages.Add(
                        new TutorialMessageEntry(defaultMessage)
                    );
                }
            }
        }

        return sequence;
    }

    private Transform GetArrowOrigin()
    {
        if (arrowOrigin != null)
        {
            return arrowOrigin;
        }

        return playerMove != null
            ? playerMove.transform
            : transform;
    }

    private bool IsPlayerFractured()
    {
        return statusConditionController != null &&
            statusConditionController.HasCondition(
                StatusConditionType.Fracture
            );
    }

    private void LockPlayerControls()
    {
        if (controlsAreLocked)
        {
            return;
        }

        controlsAreLocked = true;
        disabledDuringTimeline.Clear();

        if (playerMove != null)
        {
            playerMoveWasEnabled = playerMove.enabled;
            playerMoveStateCached = true;
            playerMove.enabled = false;

            if (playerRigidbody == null)
            {
                playerRigidbody =
                    playerMove.GetComponent<Rigidbody2D>();
            }
        }

        if (stopPlayerVelocityDuringTimeline &&
            playerRigidbody != null)
        {
            playerRigidbody.linearVelocity = Vector2.zero;
            playerRigidbody.angularVelocity = 0f;
        }

        if (additionalScriptsToDisableDuringTimeline != null)
        {
            foreach (Behaviour behaviour in
                     additionalScriptsToDisableDuringTimeline)
            {
                if (behaviour == null ||
                    behaviour == this ||
                    behaviour == playerMove ||
                    disabledDuringTimeline.Contains(behaviour))
                {
                    continue;
                }

                if (behaviour.enabled)
                {
                    behaviour.enabled = false;
                    disabledDuringTimeline.Add(behaviour);
                }
            }
        }

        equipmentVisualController?.SetWeaponControlLock(
            this,
            true
        );

        Log("Timeline中のプレイヤー操作を停止しました。");
    }

    private void RestorePlayerControls()
    {
        if (!controlsAreLocked)
        {
            return;
        }

        equipmentVisualController?.SetWeaponControlLock(
            this,
            false
        );

        foreach (Behaviour behaviour in disabledDuringTimeline)
        {
            if (behaviour != null)
            {
                behaviour.enabled = true;
            }
        }

        disabledDuringTimeline.Clear();

        if (playerMoveStateCached && playerMove != null)
        {
            playerMove.enabled = playerMoveWasEnabled;
        }

        playerMoveWasEnabled = false;
        playerMoveStateCached = false;
        controlsAreLocked = false;

        Log("Timeline終了後のプレイヤー操作を復元しました。");
    }

    private void SubscribeEvents()
    {
        if (eventsSubscribed)
        {
            return;
        }

        if (statusConditionController != null)
        {
            statusConditionController.ConditionsAdded +=
                HandleConditionsAdded;

            statusConditionController.ConditionsRemoved +=
                HandleConditionsRemoved;
        }

        if (tutorialItemBox != null)
        {
            tutorialItemBox.Opened += HandleItemBoxOpened;
        }

        if (wallClimbController != null)
        {
            wallClimbController.WallClimbStarted +=
                HandleWallClimbStarted;
        }

        eventsSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed)
        {
            return;
        }

        if (statusConditionController != null)
        {
            statusConditionController.ConditionsAdded -=
                HandleConditionsAdded;

            statusConditionController.ConditionsRemoved -=
                HandleConditionsRemoved;
        }

        if (tutorialItemBox != null)
        {
            tutorialItemBox.Opened -= HandleItemBoxOpened;
        }

        if (wallClimbController != null)
        {
            wallClimbController.WallClimbStarted -=
                HandleWallClimbStarted;
        }

        eventsSubscribed = false;
    }

    private void UnsubscribeDirectorStopped()
    {
        if (itemBoxCameraDirector != null)
        {
            itemBoxCameraDirector.stopped -=
                HandleTimelineStopped;
        }
    }

    private void FindReferences()
    {
        if (statusConditionController == null)
        {
            statusConditionController =
                FindAnyObjectByType<PlayerStatusConditionController>(
                    FindObjectsInactive.Include
                );
        }

        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>(
                FindObjectsInactive.Include
            );
        }

        if (playerMove != null && playerRigidbody == null)
        {
            playerRigidbody =
                playerMove.GetComponent<Rigidbody2D>();
        }

        if (equipmentVisualController == null &&
            playerMove != null)
        {
            equipmentVisualController =
                playerMove.GetComponent<
                    PlayerEquipmentVisualController
                >();
        }

        if (wallClimbController == null && playerMove != null)
        {
            wallClimbController =
                playerMove.GetComponent<WallClimbController>();
        }

        if (tutorialItemBox == null)
        {
            tutorialItemBox =
                FindAnyObjectByType<ItemBoxInteractable>(
                    FindObjectsInactive.Include
                );
        }

        if (tutorialPromptUI == null)
        {
            tutorialPromptUI =
                FindAnyObjectByType<TutorialPromptUI>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void SetStep(TutorialStep nextStep)
    {
        if (currentStep == nextStep)
        {
            return;
        }

        TutorialStep previousStep = currentStep;
        currentStep = nextStep;

        Log($"Step変更: {previousStep} → {currentStep}");
    }

    [ContextMenu("Reset Tutorial")]
    public void ResetTutorial()
    {
        StopMessageSequence();
        UnsubscribeDirectorStopped();

        if (itemBoxCameraDirector != null &&
            itemBoxCameraDirector.state == PlayState.Playing)
        {
            itemBoxCameraDirector.Stop();
        }

        timelinePlaybackStarted = false;
        RestorePlayerControls();
        tutorialPromptUI?.HideImmediately();
        currentStep = TutorialStep.WaitingForFracture;

        Log("チュートリアル進行を最初へ戻しました。");
    }

    [ContextMenu("Start Tutorial From Current Fracture")]
    public void StartTutorialFromCurrentFracture()
    {
        if (currentStep != TutorialStep.WaitingForFracture)
        {
            LogWarning(
                $"現在Step={currentStep}のため開始できません。" +
                "先にReset Tutorialを実行してください。"
            );
            return;
        }

        if (!IsPlayerFractured())
        {
            LogWarning("Playerが骨折していないため開始できません。");
            return;
        }

        StartItemBoxTimeline();
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"[FractureItemBoxWallTutorial] {message}",
            this
        );
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[FractureItemBoxWallTutorial] {message}",
            this
        );
    }

    private void OnValidate()
    {
        completedMessageDuration = Mathf.Max(
            0f,
            completedMessageDuration
        );

        messageSoundVolume = Mathf.Clamp01(
            messageSoundVolume
        );

        ClampSequenceInterval(moveToItemBoxMessages);
        ClampSequenceInterval(useCureItemMessages);
        ClampSequenceInterval(climbWallMessages);
        ClampSequenceInterval(completedMessages);
    }

    private static void ClampSequenceInterval(
        TutorialMessageSequence sequence)
    {
        if (sequence != null)
        {
            sequence.intervalSeconds = Mathf.Max(
                0f,
                sequence.intervalSeconds
            );
        }
    }
}
