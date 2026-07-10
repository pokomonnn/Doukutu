using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 住人会話パネルの表示、文章送り、選択肢の生成、質屋・ミッション受注への遷移を管理します。
/// TownCanvasなど、ResidentDialoguePanelとは別の常時有効Objectへ付けてください。
/// </summary>
[DisallowMultipleComponent]
public class TownDialogueController : MonoBehaviour
{
    [Header("会話パネル")]
    [Tooltip("会話表示全体のPanelです。TownDialogueController自身とは別Objectを設定してください")]
    [SerializeField] private GameObject dialoguePanel;

    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text residentNameText;
    [SerializeField] private TMP_Text dialogueText;

    [Header("通常会話のボタン")]
    [SerializeField] private Button nextButton;
    [SerializeField] private TMP_Text nextButtonText;
    [SerializeField] private Button closeButton;

    [Header("選択肢")]
    [Tooltip("選択肢Buttonを並べる親です。Vertical Layout Groupを付けるのがおすすめです")]
    [SerializeField] private Transform choiceButtonsRoot;

    [Tooltip("Buttonコンポーネントと子TMP_Textを持つPrefabを設定します")]
    [SerializeField] private Button choiceButtonPrefab;

    [Header("ほかの機能への接続")]
    [Tooltip("Open Pawn Shopの選択肢で開く既存の質屋UIです")]
    [SerializeField] private PawnShopUIController pawnShopUIController;

    [Tooltip("Start Missionの選択肢で使う町用の受注Controllerです。町シーンではこちらを使います。")]
    [SerializeField] private TownMissionAcceptController missionAcceptController;

    [Tooltip("Claim Mission Rewardの選択肢で使う報酬Controllerです。町シーンではこちらを使います。")]
    [SerializeField] private TownMissionRewardController missionRewardController;

    [Tooltip("探索シーン内で直接会話を使う場合の予備です。町シーンでは通常不要です")]
    [SerializeField] private MissionManager2D missionManager;

    [Header("任意メッセージ")]
    [Tooltip("ミッション受注失敗などを表示するText。不要なら空欄でOKです")]
    [SerializeField] private TMP_Text statusText;

    [SerializeField] private string nextLabel = "次へ";
    [SerializeField] private string closeLabel = "閉じる";

    [Header("動作")]
    [Tooltip("開始時に会話パネルを隠します")]
    [SerializeField] private bool hidePanelOnAwake = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public bool IsOpen => isOpen;
    public TownResidentDialogueData CurrentDialogue => currentDialogue;
    public int CurrentNodeIndex => currentNodeIndex;

    private readonly List<Button> spawnedChoiceButtons =
        new List<Button>();

    private TownResidentDialogueData currentDialogue;
    private int currentNodeIndex = -1;
    private bool isOpen;

    private void Awake()
    {
        FindReferences();
        SetupButtons();

        if (hidePanelOnAwake)
        {
            SetDialoguePanelVisible(false);
        }
    }

    private void OnDestroy()
    {
        RemoveButtonListeners();
        ClearChoiceButtons();
    }

    /// <summary>
    /// TownResidentBuildingButtonから呼びます。
    /// Resident Dialogue Dataの開始条件を見て、自動で開始Nodeを切り替えます。
    /// </summary>
    public void OpenDialogue(TownResidentDialogueData dialogueData)
    {
        if (dialogueData == null)
        {
            OpenDialogue(null, 0);
            return;
        }

        int startNodeIndex = ResolveConditionalStartNode(dialogueData);
        OpenDialogue(dialogueData, startNodeIndex);
    }

    private int ResolveConditionalStartNode(
        TownResidentDialogueData dialogueData)
    {
        GameSessionManager session = GameSessionManager.Instance;

        if (session == null)
        {
            session = FindAnyObjectByType<GameSessionManager>();
        }

        int startNodeIndex = dialogueData.GetStartNodeIndexForSession(
            session,
            out string debugReason
        );

        Log(
            $"会話開始Node判定: {dialogueData.ResidentName} / " +
            $"StartNode={startNodeIndex} / {debugReason}"
        );

        if (session == null)
        {
            LogWarning(
                "GameSessionManagerが見つからないため、" +
                "ミッション状態による会話分岐はDefault Nodeになります。" +
                "Town_Mainを単体再生していないか確認してください。"
            );
        }

        return startNodeIndex;
    }

    /// <summary>
    /// 指定したノードから会話を開始します。
    /// </summary>
    public void OpenDialogue(
        TownResidentDialogueData dialogueData,
        int startNodeIndex)
    {
        FindReferences();

        if (dialogueData == null)
        {
            LogWarning("Resident Dialogue Data が未設定です。");
            return;
        }

        if (!dialogueData.IsValidNodeIndex(startNodeIndex))
        {
            LogWarning(
                $"{dialogueData.name} のStart Node Index={startNodeIndex} は無効です。" +
                "Nodesに会話を追加して、0以上の番号を設定してください。"
            );
            return;
        }

        if (dialoguePanel == null)
        {
            LogWarning("Dialogue Panel が未設定です。");
            return;
        }

        currentDialogue = dialogueData;
        isOpen = true;
        SetDialoguePanelVisible(true);
        ClearStatusMessage();

        ShowNode(startNodeIndex);

        Log($"会話開始: {dialogueData.ResidentName}");
    }

    /// <summary>
    /// NextButtonのOnClickにも使えます。
    /// </summary>
    public void AdvanceDialogue()
    {
        if (!isOpen || currentDialogue == null)
        {
            return;
        }

        TownDialogueNode node = currentDialogue.GetNode(
            currentNodeIndex
        );

        if (node == null)
        {
            CloseDialogue();
            return;
        }

        // 選択肢があるノードでは、NextButtonを表示しない設計です。
        if (node.ChoiceCount > 0)
        {
            return;
        }

        if (currentDialogue.IsValidNodeIndex(node.NextNodeIndex))
        {
            ShowNode(node.NextNodeIndex);
        }
        else
        {
            CloseDialogue();
        }
    }

    /// <summary>
    /// CloseButtonのOnClickにも使えます。
    /// </summary>
    public void CloseDialogue()
    {
        if (!isOpen && currentDialogue == null)
        {
            SetDialoguePanelVisible(false);
            return;
        }

        ClearChoiceButtons();
        ClearStatusMessage();
        SetDialoguePanelVisible(false);

        string dialogueName = currentDialogue != null
            ? currentDialogue.ResidentName
            : "未設定";

        currentDialogue = null;
        currentNodeIndex = -1;
        isOpen = false;

        Log($"会話終了: {dialogueName}");
    }

    /// <summary>
    /// UIボタンのOnClickから番号指定で選択肢を実行したい時にも使えます。
    /// 通常は自動生成した選択肢Buttonから呼ばれます。
    /// </summary>
    public void SelectChoice(int choiceIndex)
    {
        if (!isOpen || currentDialogue == null)
        {
            return;
        }

        TownDialogueNode node = currentDialogue.GetNode(
            currentNodeIndex
        );

        TownDialogueChoice choice = node != null
            ? node.GetChoice(choiceIndex)
            : null;

        if (choice == null)
        {
            LogWarning($"Choice Index={choiceIndex} が無効です。");
            return;
        }

        ClearStatusMessage();

        switch (choice.Action)
        {
            case TownDialogueChoiceAction.GoToNode:
                GoToChoiceNextNodeOrClose(choice);
                break;

            case TownDialogueChoiceAction.OpenPawnShop:
                OpenPawnShopFromDialogue();
                break;

            case TownDialogueChoiceAction.StartMission:
                StartMissionFromDialogue(choice);
                break;

            case TownDialogueChoiceAction.ClaimMissionReward:
                ClaimMissionRewardFromDialogue(choice);
                break;

            case TownDialogueChoiceAction.CloseDialogue:
                CloseDialogue();
                break;

            default:
                LogWarning("未対応の会話選択肢Actionです。");
                break;
        }
    }

    private void ShowNode(int nodeIndex)
    {
        if (currentDialogue == null ||
            !currentDialogue.IsValidNodeIndex(nodeIndex))
        {
            CloseDialogue();
            return;
        }

        currentNodeIndex = nodeIndex;

        TownDialogueNode node = currentDialogue.GetNode(nodeIndex);
        if (node == null)
        {
            CloseDialogue();
            return;
        }

        string speakerName = string.IsNullOrWhiteSpace(
            node.SpeakerNameOverride
        )
            ? currentDialogue.ResidentName
            : node.SpeakerNameOverride;

        if (residentNameText != null)
        {
            residentNameText.text = speakerName;
        }

        if (dialogueText != null)
        {
            dialogueText.text = node.Message;
        }

        Sprite portrait = node.PortraitOverride != null
            ? node.PortraitOverride
            : currentDialogue.DefaultPortrait;

        ApplyPortrait(portrait);
        BuildChoiceButtons(node);
        RefreshNextButton(node);
    }

    private void BuildChoiceButtons(TownDialogueNode node)
    {
        ClearChoiceButtons();

        if (node == null || node.ChoiceCount <= 0)
        {
            return;
        }

        if (choiceButtonsRoot == null || choiceButtonPrefab == null)
        {
            LogWarning(
                "選択肢がありますが、Choice Buttons Root または " +
                "Choice Button Prefab が未設定です。"
            );
            return;
        }

        for (int i = 0; i < node.ChoiceCount; i++)
        {
            TownDialogueChoice choice = node.GetChoice(i);
            if (choice == null)
            {
                continue;
            }

            int capturedIndex = i;

            Button choiceButton = Instantiate(
                choiceButtonPrefab,
                choiceButtonsRoot
            );

            choiceButton.name = $"ChoiceButton_{capturedIndex}";
            choiceButton.onClick.RemoveAllListeners();
            choiceButton.onClick.AddListener(
                () => SelectChoice(capturedIndex)
            );

            TMP_Text choiceText =
                choiceButton.GetComponentInChildren<TMP_Text>(true);

            if (choiceText != null)
            {
                choiceText.text = choice.ChoiceText;
            }
            else
            {
                LogWarning(
                    $"Choice Button Prefab {choiceButtonPrefab.name} の子にTMP_Textがありません。"
                );
            }

            spawnedChoiceButtons.Add(choiceButton);
        }
    }

    private void ClearChoiceButtons()
    {
        foreach (Button choiceButton in spawnedChoiceButtons)
        {
            if (choiceButton != null)
            {
                Destroy(choiceButton.gameObject);
            }
        }

        spawnedChoiceButtons.Clear();
    }

    private void RefreshNextButton(TownDialogueNode node)
    {
        if (nextButton == null)
        {
            return;
        }

        bool hasChoices = node != null && node.ChoiceCount > 0;
        nextButton.gameObject.SetActive(!hasChoices);

        if (hasChoices)
        {
            return;
        }

        bool hasNextNode = currentDialogue != null &&
            currentDialogue.IsValidNodeIndex(
                node != null ? node.NextNodeIndex : -1
            );

        if (nextButtonText != null)
        {
            nextButtonText.text = hasNextNode
                ? nextLabel
                : closeLabel;
        }
    }

    private void GoToChoiceNextNodeOrClose(
        TownDialogueChoice choice)
    {
        if (currentDialogue != null &&
            currentDialogue.IsValidNodeIndex(choice.NextNodeIndex))
        {
            ShowNode(choice.NextNodeIndex);
            return;
        }

        CloseDialogue();
    }

    private void OpenPawnShopFromDialogue()
    {
        if (pawnShopUIController == null)
        {
            FindReferences();
        }

        if (pawnShopUIController == null)
        {
            SetStatusMessage(
                "質屋画面が設定されていません。" +
                "TownDialogueControllerのPawn Shop UI Controllerを設定してください。"
            );
            LogWarning("PawnShopUIController が見つかりません。");
            return;
        }

        // DialogPanelとPawnShopPanelは兄弟Objectとして配置する想定です。
        // 先に会話を閉じてから質屋を開きます。
        CloseDialogue();
        pawnShopUIController.OpenPawnShop();
    }

    private void StartMissionFromDialogue(
        TownDialogueChoice choice)
    {
        MissionDefinition2D mission = choice.MissionToStart;

        if (mission == null)
        {
            SetStatusMessage(
                "受注するミッションが設定されていません。"
            );
            LogWarning("Start Missionの選択肢にMission Definitionが未設定です。");
            return;
        }

        if (missionAcceptController == null)
        {
            missionAcceptController =
                FindAnyObjectByType<TownMissionAcceptController>();
        }

        // Town_MainのようにMissionManager2Dが存在しないシーンでは、
        // GameSessionManagerへ受注状態だけを保存する。
        if (missionAcceptController != null)
        {
            bool accepted = missionAcceptController.AcceptMission(
                mission,
                choice.TrackMissionAfterStarting,
                out string resultMessage
            );

            if (!accepted)
            {
                SetStatusMessage(resultMessage);
                return;
            }

            Log($"ミッション受注を保存: {mission.DisplayName}");
            GoToChoiceNextNodeOrClose(choice);
            return;
        }

        // 予備：探索シーンなど、同じシーンにMissionManager2Dがある場合は従来どおり直接開始する。
        if (missionManager == null)
        {
            missionManager = FindAnyObjectByType<MissionManager2D>();
        }

        if (missionManager == null)
        {
            SetStatusMessage(
                "TownMissionAcceptController、またはMissionManager2Dがこのシーンにありません。"
            );
            LogWarning(
                "Start Missionを実行できません。TownMissionAcceptControllerをTownCanvasなどへ追加してください。"
            );
            return;
        }

        int missionIndex = FindMissionIndex(mission);

        if (missionIndex < 0)
        {
            SetStatusMessage(
                $"{mission.DisplayName} がMissionManager2DのMissionsへ登録されていません。"
            );
            LogWarning(
                $"MissionManager2Dに {mission.DisplayName} が登録されていません。"
            );
            return;
        }

        bool started = missionManager.StartMission(missionIndex);

        if (!started && !missionManager.IsMissionInProgress(missionIndex))
        {
            SetStatusMessage(
                $"{mission.DisplayName} を受注できませんでした。"
            );
            return;
        }

        if (choice.TrackMissionAfterStarting)
        {
            missionManager.SetTrackedMission(missionIndex);
        }

        Log($"ミッション受注: {mission.DisplayName}");
        GoToChoiceNextNodeOrClose(choice);
    }

    private void ClaimMissionRewardFromDialogue(
        TownDialogueChoice choice)
    {
        MissionDefinition2D mission = choice.MissionToClaimReward;

        if (mission == null)
        {
            SetStatusMessage(
                "報告するミッションが設定されていません。"
            );
            LogWarning(
                "Claim Mission Rewardの選択肢にMission Definitionが未設定です。"
            );
            return;
        }

        if (missionRewardController == null)
        {
            missionRewardController =
                FindAnyObjectByType<TownMissionRewardController>();
        }

        if (missionRewardController == null)
        {
            SetStatusMessage(
                "TownMissionRewardControllerが見つかりません。TownCanvasなどへ追加してください。"
            );
            LogWarning(
                "Claim Mission Rewardを実行できません。TownMissionRewardControllerを追加してください。"
            );
            return;
        }

        bool claimed = missionRewardController.TryClaimReward(
            choice,
            out string resultMessage
        );

        if (!claimed)
        {
            SetStatusMessage(resultMessage);
            return;
        }

        Log($"ミッション報酬受け取り: {mission.DisplayName}");
        GoToChoiceNextNodeOrClose(choice);
    }

    private int FindMissionIndex(MissionDefinition2D mission)
    {
        if (missionManager == null || mission == null)
        {
            return -1;
        }

        for (int i = 0; i < missionManager.MissionCount; i++)
        {
            if (missionManager.GetMissionDefinition(i) == mission)
            {
                return i;
            }
        }

        return -1;
    }

    private void ApplyPortrait(Sprite portrait)
    {
        if (portraitImage == null)
        {
            return;
        }

        portraitImage.sprite = portrait;
        portraitImage.enabled = portrait != null;
        portraitImage.preserveAspect = true;
    }

    private void SetStatusMessage(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
            statusText.gameObject.SetActive(true);
        }

        LogWarning(message);
    }

    private void ClearStatusMessage()
    {
        if (statusText == null)
        {
            return;
        }

        statusText.text = string.Empty;
        statusText.gameObject.SetActive(false);
    }

    private void SetupButtons()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(AdvanceDialogue);
            nextButton.onClick.AddListener(AdvanceDialogue);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseDialogue);
            closeButton.onClick.AddListener(CloseDialogue);
        }
    }

    private void RemoveButtonListeners()
    {
        if (nextButton != null)
        {
            nextButton.onClick.RemoveListener(AdvanceDialogue);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseDialogue);
        }
    }

    private void SetDialoguePanelVisible(bool visible)
    {
        if (dialoguePanel == null)
        {
            return;
        }

        if (dialoguePanel.activeSelf != visible)
        {
            dialoguePanel.SetActive(visible);
        }
    }

    private void FindReferences()
    {
        if (pawnShopUIController == null)
        {
            pawnShopUIController =
                FindAnyObjectByType<PawnShopUIController>();
        }

        if (missionAcceptController == null)
        {
            missionAcceptController =
                FindAnyObjectByType<TownMissionAcceptController>();
        }

        if (missionRewardController == null)
        {
            missionRewardController =
                FindAnyObjectByType<TownMissionRewardController>();
        }

        if (missionManager == null)
        {
            missionManager = FindAnyObjectByType<MissionManager2D>();
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[TownDialogueController] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.LogWarning($"[TownDialogueController] {message}", this);
    }
}
