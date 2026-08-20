using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通常住人・ミッション住人・商人の会話を1つのUIで表示します。
/// 会話ブロック、選択肢、ミッション受注・報酬、既存質屋UIへの遷移を管理します。
/// </summary>
[DisallowMultipleComponent]
public class TownConversationController : MonoBehaviour
{
    [Header("会話パネル")]
    [Tooltip("Controller自身とは別の子Panelを設定してください。")]
    [SerializeField] private GameObject dialoguePanel;
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text residentNameText;
    [SerializeField] private TMP_Text dialogueText;

    [Header("操作ボタン")]
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonText;
    [SerializeField] private Button closeButton;
    [SerializeField] private TMP_Text closeButtonText;

    [Header("選択肢")]
    [SerializeField] private Transform choiceButtonsRoot;
    [SerializeField] private Button choiceButtonPrefab;

    [Header("既存システムへの接続")]
    [SerializeField] private TownMissionAcceptController missionAcceptController;

    [Tooltip("探索シーンで納品ミッションをNPCへ渡す時に使います。未設定ならシーン内から自動取得します。")]
    [SerializeField] private MissionManager2D missionManager;

    [SerializeField] private PawnShopUIController pawnShopUIController;

    [Tooltip("ミッションのアイテム報酬を入れるTownPlayerInventoryのInventoryControllerです。")]
    [SerializeField] private InventoryController rewardInventoryController;

    [SerializeField] private GameSessionManager gameSessionManager;
    [SerializeField] private TownConversationHistoryManager historyManager;

    [Tooltip("町の施設アップグレード処理です。未設定ならシーン内から自動取得します。")]
    [SerializeField] private TownFacilityUpgradeManager facilityUpgradeManager;

    [Header("住人立ち絵のアニメーション")]
    [Tooltip("未設定ならPortrait ImageへCanvasGroupを自動追加します。")]
    [SerializeField] private CanvasGroup portraitCanvasGroup;

    [Tooltip("立ち絵が透明から完全表示になるまでの時間です。")]
    [SerializeField, Min(0.01f)] private float portraitFadeInDuration = 0.35f;

    [Tooltip("会話を開始した時に立ち絵をフェードインします。")]
    [SerializeField] private bool fadePortraitOnConversationStart = true;

    [Tooltip("ページ途中で別の立ち絵へ切り替わった時にもフェードインします。")]
    [SerializeField] private bool fadePortraitWhenChanged = true;

    [Tooltip("Time.timeScaleが0でも立ち絵のフェードを動かします。")]
    [SerializeField] private bool useUnscaledTimeForPortrait = true;

    [Header("状態メッセージ")]
    [Tooltip("会話終了後も表示を続けるため、Dialogue Panelの外側に置いてください。")]
    [SerializeField] private TMP_Text statusText;

    [Header("状態メッセージのアニメーション")]
    [Tooltip("未設定ならStatus TextのRectTransformを自動取得します。")]
    [SerializeField] private RectTransform statusTextRectTransform;

    [Tooltip("未設定ならStatus TextへCanvasGroupを自動追加します。")]
    [SerializeField] private CanvasGroup statusCanvasGroup;

    [Tooltip("上から下へ移動しながら表示される時間です。")]
    [SerializeField, Min(0.01f)] private float statusFadeInDuration = 0.35f;

    [Tooltip("完全に表示された後、そのまま表示しておく時間です。")]
    [SerializeField, Min(0f)] private float statusVisibleDuration = 5f;

    [Tooltip("5秒後に徐々に消える時間です。")]
    [SerializeField, Min(0.01f)] private float statusFadeOutDuration = 0.45f;

    [Tooltip("表示開始時に、通常位置より上へずらす距離です。")]
    [SerializeField, Min(0f)] private float statusStartYOffset = 45f;

    [Tooltip("Time.timeScaleが0でも通知を動かします。")]
    [SerializeField] private bool useUnscaledTimeForStatus = true;

    [Header("会話音")]
    [Tooltip("未設定なら、このObjectのAudioSourceを使用または自動追加します。")]
    [SerializeField] private AudioSource dialogueAudioSource;
    [SerializeField] private AudioClip nextSound;
    [SerializeField] private AudioClip choiceSelectSound;
    [SerializeField] private AudioClip closeSound;

    [SerializeField, Range(0f, 1f)] private float nextSoundVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float choiceSoundVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float closeSoundVolume = 0.8f;

    [Header("動作")]
    [SerializeField] private bool hidePanelOnAwake = true;

    [Tooltip("通常住人の会話を閉じた時に、会話済みとして記録します。")]
    [SerializeField] private bool recordNormalConversationOnClose = true;

    [Header("報酬メッセージ")]
    [SerializeField]
    private string rewardClaimedFormat =
        "{0} の報酬を受け取りました。";

    [SerializeField]
    private string moneyRewardFormat =
        "所持金 +¥{0}";

    [SerializeField]
    private string itemRewardFormat =
        "アイテム {0} ×{1}";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("オンなら、会話開始時にUI参照・Canvas・CanvasGroup・RectTransform・表示文章を詳しく診断します。")]
    [SerializeField] private bool showDetailedDiagnostics = true;

    [Tooltip("オンなら、ページを表示するたびに現在のUI状態をConsoleへ出します。原因調査後はオフにできます。")]
    [SerializeField] private bool logUiStateOnEveryPage = true;

    public bool IsOpen => isOpen;
    public TownConversationData CurrentConversation => currentConversation;
    public TownConversationBlock CurrentBlock => currentBlock;
    public int CurrentPageIndex => currentPageIndex;
    public TownConversationPage CurrentPage =>
        currentBlock != null && currentPageIndex >= 0
            ? currentBlock.GetPage(currentPageIndex)
            : null;
    public bool ExternalNavigationMode => externalNavigationMode;
    public string CurrentBlockId => currentBlock != null
        ? currentBlock.BlockId
        : string.Empty;

    /// <summary>
    /// 洞窟側などの外部表示Controllerが、会話の開始・Block変更・Page表示・終了を受け取るための通知です。
    /// 町側では購読しなくても従来どおり動作します。
    /// </summary>
    public event Action<TownConversationController> ConversationOpened;
    public event Action<TownConversationController, TownConversationBlock> BlockChanged;
    public event Action<TownConversationController, TownConversationBlock, TownConversationPage, int> PageShown;
    public event Action<TownConversationController> ConversationClosed;

    private readonly List<Button> spawnedChoiceButtons =
        new List<Button>();

    private TownConversationData currentConversation;
    private TownConversationBlock currentBlock;
    private int currentPageIndex = -1;
    private bool isOpen;
    private bool externalNavigationMode;
    private bool hasRecordedCurrentNormalConversation;

    private Coroutine portraitFadeCoroutine;
    private bool hasDisplayedPortraitInCurrentConversation;

    private Coroutine statusMessageCoroutine;
    private Vector2 statusVisibleAnchoredPosition;
    private bool hasCachedStatusPosition;

    private void Awake()
    {
        FindReferences();
        SetupAudioSource();
        SetupPortraitUI();
        SetupStatusMessageUI();
        SubscribeButtons();

        if (showDetailedDiagnostics)
        {
            LogUiDiagnostics("Awake時");
        }

        if (hidePanelOnAwake)
        {
            SetDialoguePanelVisible(false);
        }

        ClearStatusMessage();
    }

    private void OnDestroy()
    {
        if (portraitFadeCoroutine != null)
        {
            StopCoroutine(portraitFadeCoroutine);
            portraitFadeCoroutine = null;
        }

        if (statusMessageCoroutine != null)
        {
            StopCoroutine(statusMessageCoroutine);
            statusMessageCoroutine = null;
        }

        UnsubscribeButtons();
        ClearChoiceButtons();
    }

    public void OpenConversation(TownConversationData conversationData)
    {
        FindReferences();

        if (showDetailedDiagnostics)
        {
            LogOpenRequestDiagnostics(conversationData);
        }

        if (conversationData == null)
        {
            LogWarning("Conversation Dataが未設定です。");
            return;
        }

        if (dialoguePanel == null)
        {
            LogWarning("Dialogue Panelが未設定です。");
            return;
        }

        if (isOpen)
        {
            CloseConversation(false, false);
        }

        currentConversation = conversationData;
        hasRecordedCurrentNormalConversation = false;
        hasDisplayedPortraitInCurrentConversation = false;
        isOpen = true;

        SetDialoguePanelVisible(true);
        ForceCanvasRefresh();
        ClearStatusMessage();
        ApplyCommonLabels();

        if (showDetailedDiagnostics)
        {
            LogUiDiagnostics("Dialogue Panelを表示した直後");
        }

        string startBlockId = ResolveStartBlockId(conversationData);

        if (!OpenBlock(startBlockId))
        {
            string fallbackBlockId = conversationData.GetFirstConfiguredBlockId();

            if (!OpenBlock(fallbackBlockId))
            {
                LogWarning(
                    $"{conversationData.name} に表示できる会話Blockがありません。"
                );
                CloseConversation(false, false);
                return;
            }
        }

        ConversationOpened?.Invoke(this);

        Log(
            $"会話開始: {conversationData.ResidentName} / " +
            $"Type={conversationData.ConversationType} / Block={CurrentBlockId}"
        );

        if (showDetailedDiagnostics)
        {
            LogUiDiagnostics("会話開始処理完了後");
        }
    }

    /// <summary>
    /// 洞窟会話のようにNext/Close Buttonを使わず、クリックや自動送りを外部で管理する時にONにします。
    /// 町ではOFFのままなので、既存UIの挙動は変わりません。
    /// </summary>
    public void SetExternalNavigationMode(bool enabled)
    {
        externalNavigationMode = enabled;

        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(!enabled);
        }

        if (enabled && nextButton != null)
        {
            nextButton.gameObject.SetActive(false);
        }

        if (isOpen)
        {
            RefreshNextButton();
        }
    }

    public void AdvanceConversation()
    {
        if (!isOpen || currentConversation == null || currentBlock == null)
        {
            return;
        }

        bool isAtLastPage =
            currentPageIndex >= currentBlock.PageCount - 1;

        // 納品判定Blockでは、最後のページから進む操作を
        // 選択肢より優先してNPCへの納品判定として扱う。
        if (currentBlock.CheckMissionDeliveryOnBlockEnd &&
            isAtLastPage)
        {
            PlaySound(nextSound, nextSoundVolume);
            HandleMissionDeliveryAtBlockEnd();
            return;
        }

        // 選択肢表示中はNextで進めない。
        if (currentBlock.ChoiceCount > 0 && isAtLastPage)
        {
            return;
        }

        PlaySound(nextSound, nextSoundVolume);

        if (currentPageIndex + 1 < currentBlock.PageCount)
        {
            ShowPage(currentPageIndex + 1);
            return;
        }

        if (!string.IsNullOrWhiteSpace(currentBlock.NextBlockId))
        {
            if (!OpenBlock(currentBlock.NextBlockId))
            {
                CloseConversation(true, false);
            }
            return;
        }

        CloseConversation(true, false);
    }

    public void CloseConversation()
    {
        CloseConversation(true, true);
    }

    public void SelectChoice(int choiceIndex)
    {
        if (!isOpen || currentConversation == null || currentBlock == null)
        {
            return;
        }

        TownConversationChoice choice = currentBlock.GetChoice(choiceIndex);

        if (choice == null)
        {
            LogWarning($"Choice Index={choiceIndex} が無効です。");
            return;
        }

        SetChoiceButtonsInteractable(false);

        // すでに表示中のStatusTextは、会話を閉じる選択肢を押しても
        // 5秒間の表示を継続させます。新しい状態メッセージが出た時は、
        // ShowStatusMessage側で前のアニメーションを置き換えます。
        AudioClip selectionClip = choice.SelectionSoundOverride != null
            ? choice.SelectionSoundOverride
            : choiceSelectSound;

        PlaySound(selectionClip, choiceSoundVolume);

        switch (choice.Action)
        {
            case TownConversationChoiceAction.GoToBlock:
                GoToBlockOrClose(choice.NextBlockId);
                break;

            case TownConversationChoiceAction.AcceptMission:
                AcceptCurrentMission(choice.NextBlockId);
                break;

            case TownConversationChoiceAction.ClaimMissionReward:
                ClaimCurrentMissionReward(choice.NextBlockId);
                break;

            case TownConversationChoiceAction.OpenPawnShop:
                OpenPawnShop();
                break;

            case TownConversationChoiceAction.CloseDialogue:
                CloseConversation(true, false);
                break;

            case TownConversationChoiceAction.UpgradeFacility:
                UpgradeFacilityFromChoice(choice);
                break;

            default:
                SetChoiceButtonsInteractable(true);
                LogWarning("未対応の選択肢Actionです。");
                break;
        }
    }

    private void UpgradeFacilityFromChoice(
        TownConversationChoice choice)
    {
        if (choice == null)
        {
            SetChoiceButtonsInteractable(true);
            return;
        }

        FindReferences();

        if (facilityUpgradeManager == null)
        {
            ShowStatusMessage(
                "TownFacilityUpgradeManagerが見つかりません。Town_Mainへ追加してください。",
                true
            );
            SetChoiceButtonsInteractable(true);
            return;
        }

        TownFacilityUpgradeData facilityData =
            choice.FacilityUpgradeData;

        bool upgraded = facilityUpgradeManager.TryUpgradeFacility(
            facilityData,
            out string resultMessage
        );

        ShowStatusMessage(resultMessage, !upgraded);

        if (upgraded)
        {
            GoToBlockOrClose(choice.NextBlockId);
            return;
        }

        string failureBlockId = choice.UpgradeFailureBlockId;

        if (!string.IsNullOrWhiteSpace(failureBlockId) &&
            OpenBlock(failureBlockId))
        {
            return;
        }

        SetChoiceButtonsInteractable(true);
    }

    /// <summary>
    /// 現在のBlock終了時に、Mission Residentへ納品アイテムを渡せるか判定します。
    /// 必要数がすべて揃っている時だけアイテムを消費し、ミッションを達成させてSuccess Blockへ進みます。
    /// 足りない場合は何も消費せずFailure Blockへ進みます。
    /// </summary>
    private void HandleMissionDeliveryAtBlockEnd()
    {
        TownConversationBlock deliveryBlock = currentBlock;

        if (currentConversation == null || deliveryBlock == null)
        {
            CloseConversation(true, false);
            return;
        }

        if (currentConversation.ConversationType !=
            TownConversationType.MissionResident)
        {
            LogWarning(
                $"Block『{deliveryBlock.BlockId}』で納品判定がONですが、" +
                "Conversation TypeがMission Residentではありません。"
            );
            GoToDeliveryFailureBlockOrClose(deliveryBlock);
            return;
        }

        MissionDefinition2D mission = currentConversation.Mission;

        if (mission == null)
        {
            LogWarning(
                $"Block『{deliveryBlock.BlockId}』で納品判定がONですが、" +
                "Conversation DataのMissionが未設定です。"
            );
            GoToDeliveryFailureBlockOrClose(deliveryBlock);
            return;
        }

        if (mission.ObjectiveType != MissionObjectiveType2D.DeliverItem)
        {
            LogWarning(
                $"{mission.DisplayName} はDeliver Itemミッションではありません。" +
                "Mission DefinitionのObjective TypeをDeliver Itemにしてください。"
            );
            GoToDeliveryFailureBlockOrClose(deliveryBlock);
            return;
        }

        FindReferences();

        if (missionManager == null)
        {
            LogWarning(
                "MissionManager2Dが見つからないためNPCへ納品できません。" +
                "探索シーンのTownConversationControllerへMissionManager2Dを設定してください。"
            );
            GoToDeliveryFailureBlockOrClose(deliveryBlock);
            return;
        }

        bool delivered = missionManager.TryDeliverMissionItems(
            mission,
            false,
            out int deliveredAmount,
            out string resultMessage
        );

        if (!delivered)
        {
            Log(
                $"NPC納品失敗: {mission.DisplayName} / {resultMessage}"
            );
            GoToDeliveryFailureBlockOrClose(deliveryBlock);
            return;
        }

        // 納品直後にGameSessionManagerへ同期しておくことで、
        // 同じNPCへすぐ話しかけ直した場合も達成済み状態から開始できる。
        FindSessionManager();

        if (gameSessionManager != null)
        {
            gameSessionManager.CaptureMissionsFromManager(
                missionManager
            );
        }

        Log(
            $"NPC納品成功: {mission.DisplayName} / " +
            $"納品数={deliveredAmount} / {resultMessage}"
        );

        string successBlockId =
            deliveryBlock.DeliverySuccessBlockId;

        if (string.IsNullOrWhiteSpace(successBlockId))
        {
            // Success Block未設定時だけ従来のNext Blockを予備として使う。
            successBlockId = deliveryBlock.NextBlockId;
        }

        if (!string.IsNullOrWhiteSpace(successBlockId) &&
            OpenBlock(successBlockId))
        {
            return;
        }

        LogWarning(
            $"納品成功後のBlockが設定されていません。" +
            $" Block={deliveryBlock.BlockId}"
        );
        CloseConversation(true, false);
    }

    private void GoToDeliveryFailureBlockOrClose(
        TownConversationBlock deliveryBlock)
    {
        if (deliveryBlock != null &&
            !string.IsNullOrWhiteSpace(
                deliveryBlock.DeliveryFailureBlockId) &&
            OpenBlock(deliveryBlock.DeliveryFailureBlockId))
        {
            return;
        }

        if (deliveryBlock != null)
        {
            LogWarning(
                $"納品失敗時のFailure Blockが設定されていません。" +
                $" Block={deliveryBlock.BlockId}"
            );
        }

        CloseConversation(true, false);
    }

    private string ResolveStartBlockId(TownConversationData data)
    {
        switch (data.ConversationType)
        {
            case TownConversationType.NormalResident:
                FindHistoryManager();

                bool hasTalked = historyManager != null &&
                    historyManager.HasTalkedToResident(data.ResidentId);

                return hasTalked
                    ? data.RepeatConversationBlockId
                    : data.FirstConversationBlockId;

            case TownConversationType.MissionResident:
                return ResolveMissionStartBlockId(data);

            case TownConversationType.Merchant:
                return data.MerchantStartBlockId;

            default:
                return data.GetFirstConfiguredBlockId();
        }
    }

    private string ResolveMissionStartBlockId(TownConversationData data)
    {
        MissionDefinition2D mission = data.Mission;

        if (mission == null)
        {
            LogWarning(
                $"{data.name}: Mission ResidentですがMissionが未設定です。"
            );
            return data.MissionNotAcceptedBlockId;
        }

        FindSessionManager();

        string missionId = mission.MissionId?.Trim() ?? string.Empty;

        if (gameSessionManager == null ||
            string.IsNullOrWhiteSpace(missionId) ||
            !gameSessionManager.TryGetMissionSession(
                missionId,
                out MissionSessionData sessionData) ||
            sessionData == null ||
            sessionData.State == MissionSessionState.Inactive)
        {
            return data.MissionNotAcceptedBlockId;
        }

        if (sessionData.RewardClaimed)
        {
            return data.MissionRewardClaimedBlockId;
        }

        int requiredAmount = Mathf.Max(
            1,
            sessionData.RequiredAmount > 0
                ? sessionData.RequiredAmount
                : mission.RequiredAmount
        );

        bool objectiveCompleted =
            sessionData.State == MissionSessionState.Completed ||
            sessionData.Progress >= requiredAmount;

        return objectiveCompleted
            ? data.MissionReadyToReportBlockId
            : data.MissionInProgressBlockId;
    }

    private bool OpenBlock(string blockId)
    {
        if (currentConversation == null ||
            string.IsNullOrWhiteSpace(blockId))
        {
            return false;
        }

        TownConversationBlock block = currentConversation.GetBlock(blockId);

        if (block == null)
        {
            LogWarning(
                $"{currentConversation.name}: Block ID『{blockId}』が見つかりません。"
            );
            return false;
        }

        currentBlock = block;
        currentPageIndex = -1;
        ClearChoiceButtons();

        BlockChanged?.Invoke(this, currentBlock);

        if (showDetailedDiagnostics)
        {
            Log(
                $"Blockを開きます: ID={block.BlockId} / " +
                $"Pages={block.PageCount} / Choices={block.ChoiceCount} / " +
                $"NextBlock={FormatValue(block.NextBlockId)}"
            );
        }

        if (block.PageCount <= 0 && block.ChoiceCount <= 0)
        {
            LogWarning(
                $"Block『{block.BlockId}』にはPagesもChoicesもありません。" +
                "Panelは開きますが、会話内容は何も表示されません。"
            );
        }

        if (block.PageCount > 0)
        {
            ShowPage(0);
        }
        else
        {
            ShowEmptyBlock(block);
        }

        return true;
    }

    private void ShowPage(int pageIndex)
    {
        if (currentConversation == null || currentBlock == null)
        {
            return;
        }

        TownConversationPage page = currentBlock.GetPage(pageIndex);

        if (page == null)
        {
            LogWarning(
                $"Block『{currentBlock.BlockId}』のPage {pageIndex} が無効です。"
            );
            return;
        }

        currentPageIndex = pageIndex;

        if (string.IsNullOrWhiteSpace(page.Message))
        {
            LogWarning(
                $"Block『{currentBlock.BlockId}』Page {pageIndex} のMessageが空です。"
            );
        }

        string speakerName = string.IsNullOrWhiteSpace(
            page.SpeakerNameOverride
        )
            ? currentConversation.ResidentName
            : page.SpeakerNameOverride;

        if (residentNameText != null)
        {
            residentNameText.text = speakerName;
        }
        else
        {
            LogWarning("Resident Name Textが未設定のため、NPC名を表示できません。");
        }

        string resolvedMessage = ResolveConversationVariables(page.Message);

        if (dialogueText != null)
        {
            dialogueText.text = resolvedMessage;
        }
        else
        {
            LogWarning("Dialogue Textが未設定のため、会話本文を表示できません。");
        }

        Sprite portrait = page.PortraitOverride != null
            ? page.PortraitOverride
            : currentConversation.DefaultPortrait;

        ApplyPortrait(portrait);

        bool isLastPage = pageIndex >= currentBlock.PageCount - 1;

        if (isLastPage && currentBlock.ChoiceCount > 0)
        {
            BuildChoiceButtons(currentBlock);
        }
        else
        {
            ClearChoiceButtons();
        }

        RefreshNextButton();
        ForceCanvasRefresh();

        PageShown?.Invoke(
            this,
            currentBlock,
            page,
            currentPageIndex
        );

        if (showDetailedDiagnostics)
        {
            Log(
                $"Page表示: Block={currentBlock.BlockId} / " +
                $"Page={pageIndex + 1}/{currentBlock.PageCount} / " +
                $"Speaker={FormatValue(speakerName)} / " +
                $"Message={GetTextPreview(resolvedMessage)}"
            );

            if (logUiStateOnEveryPage)
            {
                LogUiDiagnostics(
                    $"Page表示後 Block={currentBlock.BlockId}, Page={pageIndex}"
                );
            }
        }
    }

    private void ShowEmptyBlock(TownConversationBlock block)
    {
        currentPageIndex = -1;

        if (residentNameText != null)
        {
            residentNameText.text = currentConversation.ResidentName;
        }

        if (dialogueText != null)
        {
            dialogueText.text = string.Empty;
        }

        ApplyPortrait(currentConversation.DefaultPortrait);

        if (block.ChoiceCount > 0)
        {
            BuildChoiceButtons(block);
        }

        RefreshNextButton();
    }

    private void BuildChoiceButtons(TownConversationBlock block)
    {
        ClearChoiceButtons();

        if (block == null || block.ChoiceCount <= 0)
        {
            return;
        }

        if (choiceButtonsRoot == null || choiceButtonPrefab == null)
        {
            LogWarning(
                "選択肢がありますが、Choice Buttons RootまたはChoice Button Prefabが未設定です。"
            );
            return;
        }

        for (int i = 0; i < block.ChoiceCount; i++)
        {
            TownConversationChoice choice = block.GetChoice(i);

            if (choice == null)
            {
                continue;
            }

            int capturedIndex = i;
            Button button = Instantiate(
                choiceButtonPrefab,
                choiceButtonsRoot
            );

            TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);

            if (label != null)
            {
                label.text = ResolveConversationVariables(
                    choice.ChoiceText
                );
            }

            button.onClick.AddListener(
                () => SelectChoice(capturedIndex)
            );

            spawnedChoiceButtons.Add(button);
        }
    }

    private void RefreshNextButton()
    {
        if (closeButton != null)
        {
            closeButton.gameObject.SetActive(!externalNavigationMode);
        }

        if (nextButton == null || currentBlock == null)
        {
            return;
        }

        if (externalNavigationMode)
        {
            nextButton.gameObject.SetActive(false);
            return;
        }

        bool hasChoices = currentBlock.ChoiceCount > 0 &&
            currentPageIndex >= currentBlock.PageCount - 1;

        nextButton.gameObject.SetActive(!hasChoices);

        if (hasChoices || nextButtonText == null)
        {
            return;
        }

        bool hasMorePages =
            currentPageIndex + 1 < currentBlock.PageCount;

        bool hasNextBlock =
            !string.IsNullOrWhiteSpace(currentBlock.NextBlockId);

        nextButtonText.text = hasMorePages || hasNextBlock
            ? currentConversation.NextButtonText
            : currentConversation.CloseButtonText;
    }

    private void ApplyCommonLabels()
    {
        if (closeButtonText != null && currentConversation != null)
        {
            closeButtonText.text = currentConversation.CloseButtonText;
        }
    }

    private void GoToBlockOrClose(string blockId)
    {
        if (!string.IsNullOrWhiteSpace(blockId) && OpenBlock(blockId))
        {
            return;
        }

        CloseConversation(true, false);
    }

    private void AcceptCurrentMission(string choiceNextBlockId)
    {
        if (currentConversation == null ||
            currentConversation.ConversationType !=
            TownConversationType.MissionResident)
        {
            ShowStatusMessage(
                "この会話には受注するミッションが設定されていません。",
                true
            );
            SetChoiceButtonsInteractable(true);
            return;
        }

        FindReferences();

        if (missionAcceptController == null)
        {
            ShowStatusMessage(
                "TownMissionAcceptControllerが見つかりません。",
                true
            );
            SetChoiceButtonsInteractable(true);
            return;
        }

        bool accepted = missionAcceptController.AcceptMission(
            currentConversation.Mission,
            currentConversation.TrackMissionAfterAccept,
            out string resultMessage
        );

        ShowStatusMessage(resultMessage, !accepted);

        if (!accepted)
        {
            SetChoiceButtonsInteractable(true);
            return;
        }

        string nextBlockId = !string.IsNullOrWhiteSpace(choiceNextBlockId)
            ? choiceNextBlockId
            : currentConversation.MissionAcceptedJustNowBlockId;

        GoToBlockOrClose(nextBlockId);
    }

    private void ClaimCurrentMissionReward(string choiceNextBlockId)
    {
        if (currentConversation == null ||
            currentConversation.ConversationType !=
            TownConversationType.MissionResident)
        {
            ShowStatusMessage(
                "この会話には報酬対象のミッションが設定されていません。",
                true
            );
            SetChoiceButtonsInteractable(true);
            return;
        }

        bool claimed = TryClaimCurrentReward(out string resultMessage);
        ShowStatusMessage(resultMessage, !claimed);

        if (!claimed)
        {
            SetChoiceButtonsInteractable(true);
            return;
        }

        string nextBlockId = !string.IsNullOrWhiteSpace(choiceNextBlockId)
            ? choiceNextBlockId
            : currentConversation.MissionRewardClaimedBlockId;

        GoToBlockOrClose(nextBlockId);
    }

    private void OpenPawnShop()
    {
        FindReferences();

        if (pawnShopUIController == null)
        {
            ShowStatusMessage(
                "PawnShopUIControllerが見つかりません。",
                true
            );
            SetChoiceButtonsInteractable(true);
            return;
        }

        CloseConversation(false, false);
        pawnShopUIController.OpenPawnShop();
    }

    private bool TryClaimCurrentReward(out string resultMessage)
    {
        resultMessage = string.Empty;
        FindSessionManager();

        if (gameSessionManager == null)
        {
            resultMessage =
                "GameSessionManagerが見つかりません。開始シーンから町へ移動しているか確認してください。";
            return false;
        }

        MissionDefinition2D mission = currentConversation.Mission;

        if (!gameSessionManager.CanClaimMissionReward(
                mission,
                currentConversation.RequireObjectiveCompleted,
                out resultMessage))
        {
            return false;
        }

        if (!CanFitItemRewards(
                currentConversation.ItemRewards,
                out resultMessage))
        {
            return false;
        }

        if (!GrantItemRewards(
                currentConversation.ItemRewards,
                out resultMessage))
        {
            return false;
        }

        int moneyReward = currentConversation.MoneyReward;

        if (moneyReward > 0)
        {
            gameSessionManager.AddMoney(moneyReward);
        }

        if (!gameSessionManager.MarkMissionRewardClaimed(
                mission,
                out string claimMessage))
        {
            resultMessage = claimMessage;
            return false;
        }

        resultMessage = BuildRewardResultMessage(
            mission,
            moneyReward,
            currentConversation.ItemRewards
        );

        return true;
    }

    private bool CanFitItemRewards(
        IReadOnlyList<TownConversationRewardItem> itemRewards,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (!HasAnyValidItemReward(itemRewards))
        {
            return true;
        }

        FindReferences();

        if (rewardInventoryController == null ||
            rewardInventoryController.Grid == null)
        {
            resultMessage =
                "報酬アイテムを入れるInventoryControllerが設定されていません。";
            return false;
        }

        InventoryGrid grid = rewardInventoryController.Grid;
        bool[,] occupied = BuildOccupiedMap(grid);

        Dictionary<ItemData, int> stackSpace =
            BuildExistingStackSpace(grid);

        foreach (TownConversationRewardItem reward in itemRewards)
        {
            if (reward == null || reward.ItemData == null || reward.Amount <= 0)
            {
                continue;
            }

            ItemData itemData = reward.ItemData;
            int remainingAmount = reward.Amount;
            int maxStack = Mathf.Max(1, itemData.MaxStack);

            if (itemData.CanStack &&
                stackSpace.TryGetValue(itemData, out int availableSpace) &&
                availableSpace > 0)
            {
                int amountForExistingStacks = Mathf.Min(
                    availableSpace,
                    remainingAmount
                );

                remainingAmount -= amountForExistingStacks;
                stackSpace[itemData] = availableSpace - amountForExistingStacks;
            }

            while (remainingAmount > 0)
            {
                if (!TryReserveRewardItemSpace(
                        grid,
                        occupied,
                        itemData))
                {
                    resultMessage =
                        $"報酬アイテム {itemData.DisplayName} を入れる空きがありません。";
                    return false;
                }

                int placedInNewStack = Mathf.Min(
                    maxStack,
                    remainingAmount
                );

                remainingAmount -= placedInNewStack;

                if (itemData.CanStack && placedInNewStack < maxStack)
                {
                    int leftoverSpace = maxStack - placedInNewStack;

                    stackSpace.TryGetValue(itemData, out int currentSpace);
                    stackSpace[itemData] = currentSpace + leftoverSpace;
                }
            }
        }

        return true;
    }

    private bool GrantItemRewards(
        IReadOnlyList<TownConversationRewardItem> itemRewards,
        out string resultMessage)
    {
        resultMessage = string.Empty;

        if (!HasAnyValidItemReward(itemRewards))
        {
            return true;
        }

        if (rewardInventoryController == null)
        {
            resultMessage =
                "報酬アイテムを入れるInventoryControllerが見つかりません。";
            return false;
        }

        foreach (TownConversationRewardItem reward in itemRewards)
        {
            if (reward == null || reward.ItemData == null || reward.Amount <= 0)
            {
                continue;
            }

            rewardInventoryController.TryAddItem(
                reward.ItemData,
                reward.Amount,
                out int remainingAmount
            );

            if (remainingAmount > 0)
            {
                resultMessage =
                    $"報酬アイテム {reward.ItemData.DisplayName} を一部受け取れませんでした。残り={remainingAmount}";
                return false;
            }
        }

        return true;
    }

    private static bool[,] BuildOccupiedMap(InventoryGrid grid)
    {
        bool[,] occupied = new bool[grid.Width, grid.Height];

        foreach (InventoryItem item in grid.Items)
        {
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            Vector2Int size = item.ItemData.GetSize(item.IsRotated);

            for (int y = 0; y < size.y; y++)
            {
                for (int x = 0; x < size.x; x++)
                {
                    int mapX = item.GridX + x;
                    int mapY = item.GridY + y;

                    if (mapX >= 0 && mapX < grid.Width &&
                        mapY >= 0 && mapY < grid.Height)
                    {
                        occupied[mapX, mapY] = true;
                    }
                }
            }
        }

        return occupied;
    }

    private static Dictionary<ItemData, int> BuildExistingStackSpace(
        InventoryGrid grid)
    {
        Dictionary<ItemData, int> result =
            new Dictionary<ItemData, int>();

        foreach (InventoryItem item in grid.Items)
        {
            if (item == null ||
                item.ItemData == null ||
                !item.ItemData.CanStack)
            {
                continue;
            }

            int free = Mathf.Max(
                0,
                item.ItemData.MaxStack - item.Amount
            );

            result.TryGetValue(item.ItemData, out int current);
            result[item.ItemData] = current + free;
        }

        return result;
    }

    private static bool TryReserveRewardItemSpace(
        InventoryGrid grid,
        bool[,] occupied,
        ItemData itemData)
    {
        if (TryReserveRewardItemSpaceWithRotation(
                grid,
                occupied,
                itemData,
                false))
        {
            return true;
        }

        return itemData.CanRotate &&
            TryReserveRewardItemSpaceWithRotation(
                grid,
                occupied,
                itemData,
                true
            );
    }

    private static bool TryReserveRewardItemSpaceWithRotation(
        InventoryGrid grid,
        bool[,] occupied,
        ItemData itemData,
        bool isRotated)
    {
        Vector2Int size = itemData.GetSize(isRotated);

        for (int y = 0; y <= grid.Height - size.y; y++)
        {
            for (int x = 0; x <= grid.Width - size.x; x++)
            {
                if (!IsAreaFree(occupied, x, y, size))
                {
                    continue;
                }

                ReserveArea(occupied, x, y, size);
                return true;
            }
        }

        return false;
    }

    private static bool IsAreaFree(
        bool[,] occupied,
        int startX,
        int startY,
        Vector2Int size)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                if (occupied[startX + x, startY + y])
                {
                    return false;
                }
            }
        }

        return true;
    }

    private static void ReserveArea(
        bool[,] occupied,
        int startX,
        int startY,
        Vector2Int size)
    {
        for (int y = 0; y < size.y; y++)
        {
            for (int x = 0; x < size.x; x++)
            {
                occupied[startX + x, startY + y] = true;
            }
        }
    }

    private static bool HasAnyValidItemReward(
        IReadOnlyList<TownConversationRewardItem> itemRewards)
    {
        if (itemRewards == null)
        {
            return false;
        }

        foreach (TownConversationRewardItem reward in itemRewards)
        {
            if (reward != null &&
                reward.ItemData != null &&
                reward.Amount > 0)
            {
                return true;
            }
        }

        return false;
    }

    private string BuildRewardResultMessage(
        MissionDefinition2D mission,
        int moneyReward,
        IReadOnlyList<TownConversationRewardItem> itemRewards)
    {
        string missionName = mission != null
            ? mission.DisplayName
            : "ミッション";

        List<string> rewardLines = new List<string>();

        if (moneyReward > 0)
        {
            rewardLines.Add(
                string.Format(
                    moneyRewardFormat,
                    moneyReward.ToString("N0")
                )
            );
        }

        if (itemRewards != null)
        {
            foreach (TownConversationRewardItem reward in itemRewards)
            {
                if (reward == null ||
                    reward.ItemData == null ||
                    reward.Amount <= 0)
                {
                    continue;
                }

                rewardLines.Add(
                    string.Format(
                        itemRewardFormat,
                        reward.ItemData.DisplayName,
                        reward.Amount
                    )
                );
            }
        }

        string baseMessage = string.Format(
            rewardClaimedFormat,
            missionName
        );

        return rewardLines.Count > 0
            ? baseMessage + "\n" + string.Join("\n", rewardLines)
            : baseMessage;
    }

    private void CloseConversation(
        bool recordNormalConversation,
        bool playCloseSound)
    {
        if (playCloseSound)
        {
            PlaySound(closeSound, closeSoundVolume);
        }

        if (recordNormalConversation)
        {
            RecordNormalConversationIfNeeded();
        }

        ClearChoiceButtons();
        StopPortraitFade(false);
        hasDisplayedPortraitInCurrentConversation = false;

        // StatusTextは会話終了後も5秒間表示してから消すため、
        // ここでは消しません。
        SetDialoguePanelVisible(false);

        currentConversation = null;
        currentBlock = null;
        currentPageIndex = -1;
        isOpen = false;
        hasRecordedCurrentNormalConversation = false;

        ConversationClosed?.Invoke(this);
    }

    private void RecordNormalConversationIfNeeded()
    {
        if (!recordNormalConversationOnClose ||
            hasRecordedCurrentNormalConversation ||
            currentConversation == null ||
            currentConversation.ConversationType !=
            TownConversationType.NormalResident)
        {
            return;
        }

        FindHistoryManager();

        if (historyManager == null)
        {
            return;
        }

        historyManager.RecordConversationCompleted(
            currentConversation.ResidentId
        );

        hasRecordedCurrentNormalConversation = true;
    }

    private void ApplyPortrait(Sprite portrait)
    {
        if (portraitImage == null)
        {
            return;
        }

        SetupPortraitUI();

        bool portraitChanged =
            portraitImage.sprite != portrait;

        if (portrait == null)
        {
            StopPortraitFade(false);
            portraitImage.sprite = null;
            portraitImage.enabled = false;
            hasDisplayedPortraitInCurrentConversation = false;

            if (portraitCanvasGroup != null)
            {
                portraitCanvasGroup.alpha = 0f;
            }

            return;
        }

        bool isFirstPortraitInConversation =
            !hasDisplayedPortraitInCurrentConversation;

        portraitImage.sprite = portrait;
        portraitImage.enabled = true;

        bool shouldFade =
            (isFirstPortraitInConversation &&
             fadePortraitOnConversationStart) ||
            (!isFirstPortraitInConversation &&
             portraitChanged &&
             fadePortraitWhenChanged);

        hasDisplayedPortraitInCurrentConversation = true;

        if (shouldFade)
        {
            StartPortraitFadeIn();
        }
        else if (portraitCanvasGroup != null)
        {
            StopPortraitFade(true);
        }
    }

    private void SetupPortraitUI()
    {
        if (portraitImage == null)
        {
            return;
        }

        if (portraitCanvasGroup == null)
        {
            portraitCanvasGroup =
                portraitImage.GetComponent<CanvasGroup>();
        }

        if (portraitCanvasGroup == null)
        {
            portraitCanvasGroup =
                portraitImage.gameObject.AddComponent<CanvasGroup>();
        }

        portraitCanvasGroup.interactable = false;
        portraitCanvasGroup.blocksRaycasts = false;
    }

    private void StartPortraitFadeIn()
    {
        SetupPortraitUI();

        if (portraitImage == null ||
            portraitCanvasGroup == null ||
            !portraitImage.enabled ||
            portraitImage.sprite == null)
        {
            return;
        }

        if (portraitFadeCoroutine != null)
        {
            StopCoroutine(portraitFadeCoroutine);
        }

        portraitFadeCoroutine =
            StartCoroutine(PortraitFadeInRoutine());
    }

    private IEnumerator PortraitFadeInRoutine()
    {
        portraitCanvasGroup.alpha = 0f;

        float duration = Mathf.Max(
            0.01f,
            portraitFadeInDuration
        );

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += useUnscaledTimeForPortrait
                ? Time.unscaledDeltaTime
                : Time.deltaTime;

            float progress = Mathf.Clamp01(
                elapsed / duration
            );

            // 最初と最後が滑らかになる補間です。
            float smoothProgress =
                progress * progress * (3f - 2f * progress);

            portraitCanvasGroup.alpha = smoothProgress;
            yield return null;
        }

        portraitCanvasGroup.alpha = 1f;
        portraitFadeCoroutine = null;
    }

    private void StopPortraitFade(bool showImmediately)
    {
        if (portraitFadeCoroutine != null)
        {
            StopCoroutine(portraitFadeCoroutine);
            portraitFadeCoroutine = null;
        }

        if (portraitCanvasGroup != null)
        {
            portraitCanvasGroup.alpha =
                showImmediately ? 1f : 0f;
        }
    }

    private void ClearChoiceButtons()
    {
        foreach (Button button in spawnedChoiceButtons)
        {
            if (button != null)
            {
                Destroy(button.gameObject);
            }
        }

        spawnedChoiceButtons.Clear();
    }

    private void SetChoiceButtonsInteractable(bool interactable)
    {
        foreach (Button button in spawnedChoiceButtons)
        {
            if (button != null)
            {
                button.interactable = interactable;
            }
        }
    }

    private void SetDialoguePanelVisible(bool visible)
    {
        if (dialoguePanel == null)
        {
            LogWarning("Dialogue Panelが未設定のため、表示状態を変更できません。");
            return;
        }

        if (dialoguePanel == gameObject)
        {
            LogWarning(
                "Dialogue PanelにTownConversationController自身のGameObjectが設定されています。" +
                "Controllerとは別の子Panelを設定してください。自身を非表示にするとControllerも停止します。"
            );
        }

        bool beforeActiveSelf = dialoguePanel.activeSelf;
        bool beforeActiveInHierarchy = dialoguePanel.activeInHierarchy;

        if (beforeActiveSelf != visible)
        {
            dialoguePanel.SetActive(visible);
        }

        if (showDetailedDiagnostics)
        {
            Log(
                $"Dialogue Panel表示変更: requested={visible} / " +
                $"beforeSelf={beforeActiveSelf} / beforeHierarchy={beforeActiveInHierarchy} / " +
                $"afterSelf={dialoguePanel.activeSelf} / afterHierarchy={dialoguePanel.activeInHierarchy}"
            );
        }

        if (visible && !dialoguePanel.activeInHierarchy)
        {
            Transform inactiveParent = FindFirstInactiveTransform(dialoguePanel.transform);

            LogWarning(
                "Dialogue PanelのActive SelfはONですが、Active In HierarchyがOFFです。" +
                $"無効な親Object={GetTransformPath(inactiveParent)}。" +
                "Dialogue Panelより上の親Objectを有効にしてください。"
            );
        }
    }


    [ContextMenu("Log Conversation UI Diagnostics")]
    public void LogConversationUiDiagnostics()
    {
        FindReferences();
        ForceCanvasRefresh();
        LogUiDiagnostics("Context Menuによる手動診断");
    }

    private void LogOpenRequestDiagnostics(TownConversationData data)
    {
        Log("========== 会話開始診断 ==========");
        Log(
            $"Controller={GetTransformPath(transform)} / " +
            $"enabled={enabled} / activeSelf={gameObject.activeSelf} / " +
            $"activeInHierarchy={gameObject.activeInHierarchy}"
        );

        if (data == null)
        {
            LogWarning("OpenConversationへ渡されたConversation Dataがnullです。");
            return;
        }

        string startBlockId = ResolveStartBlockId(data);
        TownConversationBlock startBlock = data.GetBlock(startBlockId);

        Log(
            $"Data={data.name} / ResidentId={FormatValue(data.ResidentId)} / " +
            $"ResidentName={FormatValue(data.ResidentName)} / Type={data.ConversationType} / " +
            $"BlockCount={data.BlockCount} / ResolvedStartBlock={FormatValue(startBlockId)}"
        );

        if (string.IsNullOrWhiteSpace(startBlockId))
        {
            LogWarning("開始Block IDが空です。Conversation Data上部の開始Block IDを確認してください。");
        }
        else if (startBlock == null)
        {
            LogWarning(
                $"開始Block『{startBlockId}』がBlocks一覧に存在しません。" +
                "Block IDの大文字小文字・空白・入力ミスを確認してください。"
            );
        }
        else
        {
            Log(
                $"開始Block確認成功: ID={startBlock.BlockId} / " +
                $"Pages={startBlock.PageCount} / Choices={startBlock.ChoiceCount} / " +
                $"Next={FormatValue(startBlock.NextBlockId)}"
            );

            if (startBlock.PageCount > 0)
            {
                TownConversationPage firstPage = startBlock.GetPage(0);
                Log(
                    $"最初のPage: Message={GetTextPreview(firstPage != null ? firstPage.Message : null)}"
                );
            }
        }
    }

    private void LogUiDiagnostics(string phase)
    {
        if (!showDetailedDiagnostics)
        {
            return;
        }

        Log($"---------- UI診断: {phase} ----------");

        LogReferenceState("Dialogue Panel", dialoguePanel);
        LogTextState("Resident Name Text", residentNameText);
        LogTextState("Dialogue Text", dialogueText);
        LogImageState("Portrait Image", portraitImage);

        if (portraitCanvasGroup != null)
        {
            Log(
                $"Portrait CanvasGroup={GetTransformPath(portraitCanvasGroup.transform)} / " +
                $"alpha={portraitCanvasGroup.alpha:0.###} / enabled={portraitCanvasGroup.enabled}"
            );
        }

        LogButtonState("Next Button", nextButton);
        LogTextState("Next Button Text", nextButtonText);
        LogButtonState("Close Button", closeButton);
        LogTextState("Close Button Text", closeButtonText);
        LogReferenceState(
            "Choice Buttons Root",
            choiceButtonsRoot != null ? choiceButtonsRoot.gameObject : null
        );
        LogReferenceState(
            "Choice Button Prefab",
            choiceButtonPrefab != null ? choiceButtonPrefab.gameObject : null
        );

        if (dialoguePanel == null)
        {
            LogWarning("【表示不能】Dialogue Panelが未設定です。");
            return;
        }

        Canvas[] canvases = dialoguePanel.GetComponentsInParent<Canvas>(true);
        Canvas canvas = canvases != null && canvases.Length > 0
            ? canvases[0]
            : null;

        if (canvas == null)
        {
            LogWarning(
                "【表示不能】Dialogue Panelの親階層にCanvasがありません。" +
                "Dialogue PanelをCanvasの子に置いてください。"
            );
        }
        else
        {
            string cameraState = canvas.worldCamera != null
                ? GetTransformPath(canvas.worldCamera.transform)
                : "未設定";

            Log(
                $"Canvas={GetTransformPath(canvas.transform)} / enabled={canvas.enabled} / " +
                $"active={canvas.gameObject.activeInHierarchy} / renderMode={canvas.renderMode} / " +
                $"sortingOrder={canvas.sortingOrder} / worldCamera={cameraState}"
            );

            if (!canvas.enabled || !canvas.gameObject.activeInHierarchy)
            {
                LogWarning("【表示不能】親Canvasが無効です。");
            }

            if ((canvas.renderMode == RenderMode.ScreenSpaceCamera ||
                 canvas.renderMode == RenderMode.WorldSpace) &&
                canvas.worldCamera == null)
            {
                LogWarning(
                    "CanvasがScreen Space - CameraまたはWorld Spaceですが、World Cameraが未設定です。"
                );
            }
        }

        Transform inactiveTransform = FindFirstInactiveTransform(
            dialoguePanel.transform
        );

        if (inactiveTransform != null)
        {
            LogWarning(
                "【表示不能】Panelまたは親Objectが無効です: " +
                GetTransformPath(inactiveTransform)
            );
        }

        RectTransform panelRect = dialoguePanel.GetComponent<RectTransform>();

        if (panelRect == null)
        {
            LogWarning(
                "Dialogue PanelにRectTransformがありません。UI PanelではないObjectが設定されている可能性があります。"
            );
        }
        else
        {
            Rect rect = panelRect.rect;
            Vector3 scale = panelRect.lossyScale;

            Log(
                $"Panel RectTransform: rect={rect.width:0.##}x{rect.height:0.##} / " +
                $"anchoredPosition={panelRect.anchoredPosition} / localScale={panelRect.localScale} / " +
                $"lossyScale={scale} / siblingIndex={panelRect.GetSiblingIndex()}"
            );

            if (rect.width <= 1f || rect.height <= 1f)
            {
                LogWarning(
                    "【表示困難】Dialogue Panelの幅または高さがほぼ0です。RectTransformを確認してください。"
                );
            }

            if (Mathf.Abs(scale.x) <= 0.001f ||
                Mathf.Abs(scale.y) <= 0.001f ||
                Mathf.Abs(scale.z) <= 0.001f)
            {
                LogWarning(
                    "【表示不能】Dialogue Panelまたは親ObjectのScaleが0です。"
                );
            }
        }

        CanvasGroup[] canvasGroups =
            dialoguePanel.GetComponentsInParent<CanvasGroup>(true);

        if (canvasGroups != null && canvasGroups.Length > 0)
        {
            foreach (CanvasGroup group in canvasGroups)
            {
                if (group == null)
                {
                    continue;
                }

                Log(
                    $"CanvasGroup={GetTransformPath(group.transform)} / alpha={group.alpha:0.###} / " +
                    $"interactable={group.interactable} / blocksRaycasts={group.blocksRaycasts} / " +
                    $"ignoreParentGroups={group.ignoreParentGroups}"
                );

                if (group.alpha <= 0.001f)
                {
                    LogWarning(
                        "【表示不能】CanvasGroupのAlphaが0です: " +
                        GetTransformPath(group.transform)
                    );
                }
            }
        }

        if (dialogueText == null)
        {
            LogWarning("【本文表示不能】Dialogue Textが未設定です。");
        }

        if (residentNameText == null)
        {
            LogWarning("Resident Name Textが未設定です。NPC名は表示されません。");
        }

        if (nextButton == null)
        {
            LogWarning("Next Buttonが未設定です。文章送りができません。");
        }

        if (closeButton == null)
        {
            LogWarning("Close Buttonが未設定です。閉じる操作ができません。");
        }
    }

    private void LogReferenceState(string label, GameObject target)
    {
        if (target == null)
        {
            LogWarning($"{label}=未設定");
            return;
        }

        Log(
            $"{label}={GetTransformPath(target.transform)} / " +
            $"activeSelf={target.activeSelf} / activeInHierarchy={target.activeInHierarchy} / " +
            $"layer={LayerMask.LayerToName(target.layer)}"
        );
    }

    private void LogTextState(string label, TMP_Text text)
    {
        if (text == null)
        {
            LogWarning($"{label}=未設定");
            return;
        }

        RectTransform rect = text.rectTransform;
        string fontName = text.font != null ? text.font.name : "未設定";

        Log(
            $"{label}={GetTransformPath(text.transform)} / enabled={text.enabled} / " +
            $"active={text.gameObject.activeInHierarchy} / text={GetTextPreview(text.text)} / " +
            $"font={fontName} / fontSize={text.fontSize:0.##} / alpha={text.color.a:0.###} / " +
            $"rect={rect.rect.width:0.##}x{rect.rect.height:0.##}"
        );

        if (!text.enabled || !text.gameObject.activeInHierarchy)
        {
            LogWarning($"【表示不能】{label}が無効です。");
        }

        if (text.color.a <= 0.001f)
        {
            LogWarning($"【表示不能】{label}の文字色Alphaが0です。");
        }

        if (text.font == null)
        {
            LogWarning($"【表示不能】{label}のFont Assetが未設定です。");
        }

        if (rect.rect.width <= 1f || rect.rect.height <= 1f)
        {
            LogWarning($"【表示困難】{label}のRectTransformサイズがほぼ0です。");
        }
    }

    private void LogImageState(string label, Image image)
    {
        if (image == null)
        {
            LogWarning($"{label}=未設定");
            return;
        }

        string spriteName = image.sprite != null ? image.sprite.name : "未設定";
        Log(
            $"{label}={GetTransformPath(image.transform)} / enabled={image.enabled} / " +
            $"active={image.gameObject.activeInHierarchy} / sprite={spriteName} / " +
            $"alpha={image.color.a:0.###} / rect={image.rectTransform.rect.width:0.##}x{image.rectTransform.rect.height:0.##}"
        );
    }

    private void LogButtonState(string label, Button button)
    {
        if (button == null)
        {
            LogWarning($"{label}=未設定");
            return;
        }

        Log(
            $"{label}={GetTransformPath(button.transform)} / enabled={button.enabled} / " +
            $"active={button.gameObject.activeInHierarchy} / interactable={button.interactable}"
        );
    }

    private static Transform FindFirstInactiveTransform(Transform target)
    {
        Transform current = target;

        while (current != null)
        {
            if (!current.gameObject.activeSelf)
            {
                return current;
            }

            current = current.parent;
        }

        return null;
    }

    private static string GetTransformPath(Transform target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static string FormatValue(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "<空欄>"
            : value;
    }

    private static string GetTextPreview(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "<空>";
        }

        string singleLine = value
            .Replace("\r", " ")
            .Replace("\n", " ");

        const int maxLength = 80;

        return singleLine.Length <= maxLength
            ? $"『{singleLine}』"
            : $"『{singleLine.Substring(0, maxLength)}…』";
    }

    private static void ForceCanvasRefresh()
    {
        Canvas.ForceUpdateCanvases();
    }

    private void ShowStatusMessage(string message, bool warning)
    {
        if (warning)
        {
            LogWarning(message);
        }
        else
        {
            Log(message);
        }

        if (statusText == null || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        SetupStatusMessageUI();

        if (statusMessageCoroutine != null)
        {
            StopCoroutine(statusMessageCoroutine);
        }

        statusMessageCoroutine = StartCoroutine(
            StatusMessageRoutine(message)
        );
    }

    private IEnumerator StatusMessageRoutine(string message)
    {
        if (statusText == null)
        {
            yield break;
        }

        SetupStatusMessageUI();
        WarnIfStatusTextIsInsideDialoguePanel();

        statusText.text = message ?? string.Empty;
        statusText.gameObject.SetActive(true);

        if (statusCanvasGroup != null)
        {
            statusCanvasGroup.alpha = 0f;
            statusCanvasGroup.interactable = false;
            statusCanvasGroup.blocksRaycasts = false;
        }

        Vector2 visiblePosition = statusVisibleAnchoredPosition;
        Vector2 startPosition = visiblePosition +
            Vector2.up * statusStartYOffset;

        if (statusTextRectTransform != null)
        {
            statusTextRectTransform.anchoredPosition = startPosition;
        }

        float elapsed = 0f;
        float fadeInDuration = Mathf.Max(0.01f, statusFadeInDuration);

        while (elapsed < fadeInDuration)
        {
            elapsed += GetStatusDeltaTime();
            float progress = Mathf.Clamp01(elapsed / fadeInDuration);
            float smoothProgress = Mathf.SmoothStep(0f, 1f, progress);

            if (statusCanvasGroup != null)
            {
                statusCanvasGroup.alpha = smoothProgress;
            }

            if (statusTextRectTransform != null)
            {
                statusTextRectTransform.anchoredPosition = Vector2.Lerp(
                    startPosition,
                    visiblePosition,
                    smoothProgress
                );
            }

            yield return null;
        }

        if (statusCanvasGroup != null)
        {
            statusCanvasGroup.alpha = 1f;
        }

        if (statusTextRectTransform != null)
        {
            statusTextRectTransform.anchoredPosition = visiblePosition;
        }

        float visibleElapsed = 0f;

        while (visibleElapsed < statusVisibleDuration)
        {
            visibleElapsed += GetStatusDeltaTime();
            yield return null;
        }

        elapsed = 0f;
        float fadeOutDuration = Mathf.Max(0.01f, statusFadeOutDuration);

        while (elapsed < fadeOutDuration)
        {
            elapsed += GetStatusDeltaTime();
            float progress = Mathf.Clamp01(elapsed / fadeOutDuration);

            if (statusCanvasGroup != null)
            {
                statusCanvasGroup.alpha = 1f - progress;
            }

            yield return null;
        }

        HideStatusMessageImmediately();
        statusMessageCoroutine = null;
    }

    private void SetupStatusMessageUI()
    {
        if (statusText == null)
        {
            return;
        }

        if (statusTextRectTransform == null)
        {
            statusTextRectTransform =
                statusText.GetComponent<RectTransform>();
        }

        if (statusCanvasGroup == null)
        {
            statusCanvasGroup =
                statusText.GetComponent<CanvasGroup>();
        }

        if (statusCanvasGroup == null)
        {
            statusCanvasGroup =
                statusText.gameObject.AddComponent<CanvasGroup>();
        }

        if (!hasCachedStatusPosition &&
            statusTextRectTransform != null)
        {
            statusVisibleAnchoredPosition =
                statusTextRectTransform.anchoredPosition;

            hasCachedStatusPosition = true;
        }

        if (statusCanvasGroup != null)
        {
            statusCanvasGroup.interactable = false;
            statusCanvasGroup.blocksRaycasts = false;
        }
    }

    private void WarnIfStatusTextIsInsideDialoguePanel()
    {
        if (statusText == null || dialoguePanel == null)
        {
            return;
        }

        Transform statusTransform = statusText.transform;
        Transform panelTransform = dialoguePanel.transform;

        if (statusTransform == panelTransform ||
            statusTransform.IsChildOf(panelTransform))
        {
            LogWarning(
                "Status TextがDialogue Panelの子に入っています。" +
                "会話終了時にDialogue Panelが無効になると、5秒間の通知も一緒に消えます。" +
                "Status TextをCanvas直下など、Dialogue Panelの外側へ移動してください。"
            );
        }
    }

    private float GetStatusDeltaTime()
    {
        return useUnscaledTimeForStatus
            ? Time.unscaledDeltaTime
            : Time.deltaTime;
    }

    private void ClearStatusMessage()
    {
        if (statusMessageCoroutine != null)
        {
            StopCoroutine(statusMessageCoroutine);
            statusMessageCoroutine = null;
        }

        HideStatusMessageImmediately();
    }

    private void HideStatusMessageImmediately()
    {
        if (statusText == null)
        {
            return;
        }

        SetupStatusMessageUI();

        statusText.text = string.Empty;

        if (statusCanvasGroup != null)
        {
            statusCanvasGroup.alpha = 0f;
            statusCanvasGroup.interactable = false;
            statusCanvasGroup.blocksRaycasts = false;
        }

        if (statusTextRectTransform != null &&
            hasCachedStatusPosition)
        {
            statusTextRectTransform.anchoredPosition =
                statusVisibleAnchoredPosition;
        }

        statusText.gameObject.SetActive(false);
    }

    private void SubscribeButtons()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(AdvanceConversation);
            nextButton.onClick.AddListener(AdvanceConversation);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseConversation);
            closeButton.onClick.AddListener(CloseConversation);
        }
    }

    private void UnsubscribeButtons()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(AdvanceConversation);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseConversation);
        }
    }

    private void SetupAudioSource()
    {
        if (dialogueAudioSource == null)
        {
            dialogueAudioSource = GetComponent<AudioSource>();
        }

        if (dialogueAudioSource == null)
        {
            dialogueAudioSource = gameObject.AddComponent<AudioSource>();
        }

        dialogueAudioSource.playOnAwake = false;
        dialogueAudioSource.loop = false;
        dialogueAudioSource.spatialBlend = 0f;
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (clip == null)
        {
            return;
        }

        SetupAudioSource();
        dialogueAudioSource?.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    /// <summary>
    /// 会話文に含まれるゲーム状態のプレースホルダーを現在値へ置換します。
    /// 墓場管理人では {DeadNpcCount} を使うと、これまで町へ持ち帰った
    /// 死亡NPCの累計人数を表示できます。
    /// {TotalDeadNpcCount} も同じ意味で使用できます。
    /// </summary>
    public string ResolveConversationVariables(string sourceText)
    {
        if (string.IsNullOrEmpty(sourceText))
        {
            return sourceText ?? string.Empty;
        }

        if (!sourceText.Contains("{DeadNpcCount}") &&
            !sourceText.Contains("{TotalDeadNpcCount}"))
        {
            return sourceText;
        }

        FindSessionManager();

        int deadNpcCount = gameSessionManager != null
            ? Mathf.Max(0, gameSessionManager.TotalDeadNpcCount)
            : 0;

        string countText = deadNpcCount.ToString("N0");

        return sourceText
            .Replace("{DeadNpcCount}", countText)
            .Replace("{TotalDeadNpcCount}", countText);
    }

    private void FindReferences()
    {
        FindSessionManager();
        FindHistoryManager();

        if (missionAcceptController == null)
        {
            missionAcceptController =
                FindAnyObjectByType<TownMissionAcceptController>(
                    FindObjectsInactive.Include
                );
        }

        if (missionManager == null)
        {
            MissionManager2D[] managers =
                FindObjectsByType<MissionManager2D>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            // TownConversationControllerと同じSceneのManagerを優先する。
            foreach (MissionManager2D candidate in managers)
            {
                if (candidate != null &&
                    candidate.gameObject.scene == gameObject.scene)
                {
                    missionManager = candidate;
                    break;
                }
            }

            if (missionManager == null && managers.Length > 0)
            {
                missionManager = managers[0];
            }
        }

        if (pawnShopUIController == null)
        {
            pawnShopUIController =
                FindAnyObjectByType<PawnShopUIController>(
                    FindObjectsInactive.Include
                );
        }

        if (rewardInventoryController == null)
        {
            TownPlayerInventoryController townInventory =
                FindAnyObjectByType<TownPlayerInventoryController>(
                    FindObjectsInactive.Include
                );

            if (townInventory != null)
            {
                rewardInventoryController =
                    townInventory.InventoryController;
            }
        }

        if (facilityUpgradeManager == null)
        {
            facilityUpgradeManager =
                FindAnyObjectByType<TownFacilityUpgradeManager>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void FindSessionManager()
    {
        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void FindHistoryManager()
    {
        if (historyManager == null)
        {
            historyManager =
                TownConversationHistoryManager.GetOrCreate();
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.Log($"[TownConversationController] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        Debug.LogWarning(
            $"[TownConversationController] {message}",
            this
        );
    }

    private void OnValidate()
    {
        nextSoundVolume = Mathf.Clamp01(nextSoundVolume);
        choiceSoundVolume = Mathf.Clamp01(choiceSoundVolume);
        closeSoundVolume = Mathf.Clamp01(closeSoundVolume);

        portraitFadeInDuration = Mathf.Max(0.01f, portraitFadeInDuration);

        statusFadeInDuration = Mathf.Max(0.01f, statusFadeInDuration);
        statusVisibleDuration = Mathf.Max(0f, statusVisibleDuration);
        statusFadeOutDuration = Mathf.Max(0.01f, statusFadeOutDuration);
        statusStartYOffset = Mathf.Max(0f, statusStartYOffset);
    }
}
