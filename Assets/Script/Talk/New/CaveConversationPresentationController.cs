using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 洞窟会話専用の見せ方を管理します。
/// ・文章を左から右へ1文字ずつ表示
/// ・BlockごとのFear Shake
/// ・重要会話：Player停止 + 会話Panelクリックで次へ
/// ・通常会話：Player移動可能 + 自動送り
/// ・通常会話の最後のBlockだけ表示時間を個別指定
///
/// 会話内容・ミッション・選択肢そのものはTownConversationControllerを使用します。
/// </summary>
[DisallowMultipleComponent]
public class CaveConversationPresentationController : MonoBehaviour, IPointerClickHandler
{
    [Header("既存の会話システム")]
    [SerializeField] private TownConversationController conversationController;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private CaveDialogueAnchorFollower anchorFollower;

    [Header("1文字ずつ表示")]
    [Tooltip("オンなら会話本文を左から右へ1文字ずつ表示します。")]
    [SerializeField] private bool useTypewriter = true;

    [Tooltip("1秒間に表示する文字数です。")]
    [SerializeField, Min(1f)] private float charactersPerSecond = 32f;

    [Tooltip("重要会話で文章表示途中にクリックした時、まず文章を最後まで即表示します。もう一度クリックすると次へ進みます。")]
    [SerializeField] private bool firstClickCompletesTyping = true;

    [Tooltip("Time.timeScaleが0でも文字表示・自動送り・揺れを動かします。")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("おびえた揺れ")]
    [Tooltip("TownConversationDataのBlockでFear ShakeがONの時に使用する揺れ時間です。")]
    [SerializeField, Min(0f)] private float fearShakeDuration = 0.5f;

    [Tooltip("画面上で揺らす強さです。Screen Space Canvasならpx相当です。")]
    [SerializeField, Min(0f)] private float fearShakeStrength = 7f;

    [Tooltip("揺れ位置を更新する間隔です。小さいほど細かく震えます。")]
    [SerializeField, Min(0.01f)] private float fearShakeStep = 0.035f;

    [Header("重要会話中のPlayer停止")]
    [SerializeField] private bool lockPlayerMovementOnImportantConversation = true;

    [Tooltip("未設定ならシーン内のPlayerMoveを自動取得します。")]
    [SerializeField] private PlayerMove playerMove;

    [Tooltip("重要会話へ入った瞬間、直前の横移動速度を0にします。")]
    [SerializeField] private bool stopHorizontalVelocityWhenLocked = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private Coroutine presentationCoroutine;
    private Coroutine shakeCoroutine;

    private bool isTyping;
    private bool isPlayerLockedByThis;
    private bool playerMoveWasEnabledBeforeLock;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeConversationEvents();

        if (conversationController != null)
        {
            conversationController.SetExternalNavigationMode(true);
        }

        SyncCurrentConversationState();
    }

    private void OnDisable()
    {
        UnsubscribeConversationEvents();
        StopPresentationRoutines();
        RestoreFullTextVisibility();
        SetPlayerLocked(false);

        if (anchorFollower != null)
        {
            anchorFollower.ClearRuntimeAnimationOffset();
        }

        if (conversationController != null)
        {
            conversationController.SetExternalNavigationMode(false);
        }
    }

    private void OnDestroy()
    {
        UnsubscribeConversationEvents();
        SetPlayerLocked(false);
    }

    /// <summary>
    /// 重要会話中はNext Buttonの代わりに、会話Panel自体のクリックで進めます。
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (conversationController == null ||
            !conversationController.IsOpen)
        {
            return;
        }

        TownConversationBlock block = conversationController.CurrentBlock;

        if (block == null || !block.ImportantConversation)
        {
            // 通常会話は自動送りなのでクリックでは進めません。
            return;
        }

        bool isLastPage =
            conversationController.CurrentPageIndex >= block.PageCount - 1;

        // 選択肢が出ている時は会話Panelクリックでは進めません。
        // 選択肢Buttonそのものをクリックしてください。
        if (isLastPage && block.ChoiceCount > 0)
        {
            return;
        }

        if (isTyping && firstClickCompletesTyping)
        {
            CompleteTypingImmediately();
            return;
        }

        conversationController.AdvanceConversation();
    }

    private void HandleBlockChanged(
        TownConversationController controller,
        TownConversationBlock block)
    {
        bool shouldLock = block != null && block.ImportantConversation;
        SetPlayerLocked(shouldLock);
    }

    private void HandlePageShown(
        TownConversationController controller,
        TownConversationBlock block,
        TownConversationPage page,
        int pageIndex)
    {
        if (block == null || page == null)
        {
            return;
        }

        SetPlayerLocked(block.ImportantConversation);
        StartPagePresentation(block, page, pageIndex);
    }

    private void HandleConversationClosed(
        TownConversationController controller)
    {
        StopPresentationRoutines();
        RestoreFullTextVisibility();
        SetPlayerLocked(false);

        if (anchorFollower != null)
        {
            anchorFollower.ClearRuntimeAnimationOffset();
        }
    }

    private void StartPagePresentation(
        TownConversationBlock block,
        TownConversationPage page,
        int pageIndex)
    {
        StopPresentationCoroutine();
        RestoreFullTextVisibility();

        if (block.FearShake)
        {
            StartFearShake();
        }
        else if (anchorFollower != null)
        {
            anchorFollower.ClearRuntimeAnimationOffset();
        }

        presentationCoroutine = StartCoroutine(
            PresentPageRoutine(block, page, pageIndex)
        );
    }

    private IEnumerator PresentPageRoutine(
        TownConversationBlock block,
        TownConversationPage page,
        int pageIndex)
    {
        if (dialogueText == null)
        {
            yield break;
        }

        if (useTypewriter)
        {
            dialogueText.ForceMeshUpdate();

            int characterCount = dialogueText.textInfo.characterCount;
            dialogueText.maxVisibleCharacters = 0;
            isTyping = characterCount > 0;

            float shownCharacters = 0f;

            while (isTyping &&
                   shownCharacters < characterCount)
            {
                shownCharacters +=
                    Mathf.Max(1f, charactersPerSecond) * GetDeltaTime();

                dialogueText.maxVisibleCharacters = Mathf.Clamp(
                    Mathf.FloorToInt(shownCharacters),
                    0,
                    characterCount
                );

                yield return null;
            }

            dialogueText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;
        }
        else
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
            isTyping = false;
        }

        if (block.ImportantConversation)
        {
            // 重要会話はここで待機。次はPlayerのクリックで進みます。
            presentationCoroutine = null;
            yield break;
        }

        // 選択肢がある最後のページは、自動で選択できないので待機します。
        bool isLastPage = pageIndex >= block.PageCount - 1;

        if (isLastPage && block.ChoiceCount > 0)
        {
            presentationCoroutine = null;
            yield break;
        }

        float waitSeconds = GetAutoAdvanceDelay(block, isLastPage);
        float elapsed = 0f;

        while (elapsed < waitSeconds)
        {
            elapsed += GetDeltaTime();
            yield return null;
        }

        if (conversationController == null ||
            !conversationController.IsOpen ||
            conversationController.CurrentBlock != block ||
            conversationController.CurrentPageIndex != pageIndex)
        {
            presentationCoroutine = null;
            yield break;
        }

        presentationCoroutine = null;
        conversationController.AdvanceConversation();
    }

    private float GetAutoAdvanceDelay(
        TownConversationBlock block,
        bool isLastPage)
    {
        if (block == null)
        {
            return 0f;
        }

        bool isFinalBlock =
            isLastPage &&
            !block.CheckMissionDeliveryOnBlockEnd &&
            string.IsNullOrWhiteSpace(block.NextBlockId) &&
            block.ChoiceCount <= 0;

        return isFinalBlock
            ? block.FinalBlockDisplayDuration
            : block.AutoAdvanceDelay;
    }

    private void CompleteTypingImmediately()
    {
        if (!isTyping)
        {
            return;
        }

        StopPresentationCoroutine();
        RestoreFullTextVisibility();
        isTyping = false;
    }

    private void StartFearShake()
    {
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }

        if (anchorFollower == null ||
            fearShakeDuration <= 0f ||
            fearShakeStrength <= 0f)
        {
            anchorFollower?.ClearRuntimeAnimationOffset();
            return;
        }

        shakeCoroutine = StartCoroutine(FearShakeRoutine());
    }

    private IEnumerator FearShakeRoutine()
    {
        float elapsed = 0f;
        float stepElapsed = fearShakeStep;

        while (elapsed < fearShakeDuration)
        {
            float dt = GetDeltaTime();
            elapsed += dt;
            stepElapsed += dt;

            if (stepElapsed >= fearShakeStep)
            {
                stepElapsed = 0f;

                float remaining = 1f - Mathf.Clamp01(
                    elapsed / Mathf.Max(0.01f, fearShakeDuration)
                );

                Vector2 randomOffset =
                    Random.insideUnitCircle *
                    fearShakeStrength *
                    Mathf.Lerp(0.35f, 1f, remaining);

                anchorFollower.SetRuntimeAnimationOffset(randomOffset);
            }

            yield return null;
        }

        anchorFollower.ClearRuntimeAnimationOffset();
        shakeCoroutine = null;
    }

    private void SetPlayerLocked(bool shouldLock)
    {
        if (!lockPlayerMovementOnImportantConversation)
        {
            shouldLock = false;
        }

        FindPlayerReferences();

        if (shouldLock)
        {
            if (isPlayerLockedByThis || playerMove == null)
            {
                return;
            }

            playerMoveWasEnabledBeforeLock = playerMove.enabled;
            isPlayerLockedByThis = true;

            if (playerMove.enabled)
            {
                playerMove.enabled = false;
            }

            if (stopHorizontalVelocityWhenLocked)
            {
                Rigidbody2D body = playerMove.GetComponent<Rigidbody2D>();

                if (body != null)
                {
                    body.linearVelocity = new Vector2(
                        0f,
                        body.linearVelocity.y
                    );
                }
            }

            Log("重要会話：Player移動を停止しました。");
            return;
        }

        if (!isPlayerLockedByThis)
        {
            return;
        }

        if (playerMove != null && playerMoveWasEnabledBeforeLock)
        {
            playerMove.enabled = true;
        }

        isPlayerLockedByThis = false;
        playerMoveWasEnabledBeforeLock = false;
        Log("重要会話終了：Player移動を復元しました。");
    }

    private void SyncCurrentConversationState()
    {
        if (conversationController == null ||
            !conversationController.IsOpen)
        {
            return;
        }

        TownConversationBlock block = conversationController.CurrentBlock;
        TownConversationPage page = conversationController.CurrentPage;

        if (block != null)
        {
            SetPlayerLocked(block.ImportantConversation);
        }

        if (block != null && page != null)
        {
            StartPagePresentation(
                block,
                page,
                conversationController.CurrentPageIndex
            );
        }
    }

    private void SubscribeConversationEvents()
    {
        if (conversationController == null)
        {
            return;
        }

        UnsubscribeConversationEvents();

        conversationController.BlockChanged +=
            HandleBlockChanged;
        conversationController.PageShown +=
            HandlePageShown;
        conversationController.ConversationClosed +=
            HandleConversationClosed;
    }

    private void UnsubscribeConversationEvents()
    {
        if (conversationController == null)
        {
            return;
        }

        conversationController.BlockChanged -=
            HandleBlockChanged;
        conversationController.PageShown -=
            HandlePageShown;
        conversationController.ConversationClosed -=
            HandleConversationClosed;
    }

    private void StopPresentationRoutines()
    {
        StopPresentationCoroutine();

        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
    }

    private void StopPresentationCoroutine()
    {
        if (presentationCoroutine != null)
        {
            StopCoroutine(presentationCoroutine);
            presentationCoroutine = null;
        }
    }

    private void RestoreFullTextVisibility()
    {
        isTyping = false;

        if (dialogueText != null)
        {
            dialogueText.maxVisibleCharacters = int.MaxValue;
        }
    }

    private float GetDeltaTime()
    {
        return useUnscaledTime
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
    }

    private void FindReferences()
    {
        if (conversationController == null)
        {
            conversationController =
                FindAnyObjectByType<TownConversationController>(
                    FindObjectsInactive.Include
                );
        }

        if (anchorFollower == null)
        {
            anchorFollower = GetComponent<CaveDialogueAnchorFollower>();
        }

        if (anchorFollower == null)
        {
            anchorFollower =
                FindAnyObjectByType<CaveDialogueAnchorFollower>(
                    FindObjectsInactive.Include
                );
        }

        if (dialogueText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);

            foreach (TMP_Text text in texts)
            {
                if (text != null &&
                    text.gameObject.name.ToLowerInvariant().Contains("dialogue"))
                {
                    dialogueText = text;
                    break;
                }
            }
        }

        FindPlayerReferences();
    }

    private void FindPlayerReferences()
    {
        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>(
                FindObjectsInactive.Include
            );
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"[CaveConversationPresentation] {message}",
            this
        );
    }

    private void OnValidate()
    {
        charactersPerSecond = Mathf.Max(1f, charactersPerSecond);
        fearShakeDuration = Mathf.Max(0f, fearShakeDuration);
        fearShakeStrength = Mathf.Max(0f, fearShakeStrength);
        fearShakeStep = Mathf.Max(0.01f, fearShakeStep);
    }
}
