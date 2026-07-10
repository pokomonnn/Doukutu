using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 町の住人との会話内容を保存するScriptableObjectです。
/// 住人ごとにアセットを1つ作り、ノードと選択肢をInspectorで設定します。
/// </summary>
[CreateAssetMenu(
    fileName = "NewTownResidentDialogue",
    menuName = "Town/Dialogue/Resident Dialogue Data"
)]
public class TownResidentDialogueData : ScriptableObject
{
    [Header("住人の基本情報")]
    [Tooltip("重複しない管理用IDです。空欄でも会話は動きます")]
    [SerializeField] private string residentId = "resident_id";

    [SerializeField] private string residentName = "住人";

    [Tooltip("各ノードで個別指定しない時に使う立ち絵です")]
    [SerializeField] private Sprite defaultPortrait;

    [Header("会話開始ノード")]
    [Tooltip("下の条件ルールに当てはまらない時に開始するNode番号です。通常は0です。")]
    [SerializeField, Min(0)] private int defaultStartNodeIndex = 0;

    [Tooltip("上から順番に判定します。例：報酬受取済み→達成済み未報告→受注中→未受注 の順に並べるのがおすすめです。")]
    [SerializeField]
    private List<TownDialogueStartRule> startRules =
        new List<TownDialogueStartRule>();

    [Header("会話ノード")]
    [SerializeField]
    private List<TownDialogueNode> nodes =
        new List<TownDialogueNode>();

    public string ResidentId => residentId;

    public string ResidentName => string.IsNullOrWhiteSpace(residentName)
        ? name
        : residentName;

    public Sprite DefaultPortrait => defaultPortrait;

    public int DefaultStartNodeIndex => defaultStartNodeIndex;

    public int NodeCount => nodes != null ? nodes.Count : 0;

    public int StartRuleCount => startRules != null ? startRules.Count : 0;

    public bool IsValidNodeIndex(int index)
    {
        return nodes != null &&
               index >= 0 &&
               index < nodes.Count &&
               nodes[index] != null;
    }

    public TownDialogueNode GetNode(int index)
    {
        return IsValidNodeIndex(index) ? nodes[index] : null;
    }

    /// <summary>
    /// 現在のミッション状態に応じて、どのNodeから会話を始めるか決めます。
    /// TownDialogueController.OpenDialogue(dialogueData)から自動で使われます。
    /// </summary>
    public int GetStartNodeIndexForSession(
        GameSessionManager session,
        out string debugReason)
    {
        debugReason = string.Empty;

        if (startRules != null)
        {
            for (int i = 0; i < startRules.Count; i++)
            {
                TownDialogueStartRule rule = startRules[i];

                if (rule == null || !rule.Enabled)
                {
                    continue;
                }

                if (!rule.IsMatch(session, out string ruleReason))
                {
                    continue;
                }

                if (!IsValidNodeIndex(rule.StartNodeIndex))
                {
                    debugReason =
                        $"StartRule[{i}] は一致しましたが、Start Node Index={rule.StartNodeIndex} が無効です。" +
                        $" 理由={ruleReason}";
                    break;
                }

                debugReason =
                    $"StartRule[{i}] に一致しました。Node={rule.StartNodeIndex} / 理由={ruleReason}";
                return rule.StartNodeIndex;
            }
        }

        int fallbackIndex = IsValidNodeIndex(defaultStartNodeIndex)
            ? defaultStartNodeIndex
            : 0;

        debugReason =
            $"一致するStartRuleがないためDefault Node={fallbackIndex}から開始します。";

        return fallbackIndex;
    }

    private void OnValidate()
    {
        residentId = residentId?.Trim() ?? string.Empty;
        residentName = residentName?.Trim() ?? string.Empty;
        defaultStartNodeIndex = Mathf.Max(0, defaultStartNodeIndex);
    }
}

/// <summary>
/// 会話開始位置を切り替える条件です。
/// </summary>
public enum TownDialogueStartCondition
{
    Always,

    [Tooltip("GameSessionManagerにこのMissionIdがまだ保存されていない、またはInactiveの時")]
    MissionNotAccepted,

    [Tooltip("受注済みなら、進行中・達成済み・報酬済みを問わず一致")]
    MissionAccepted,

    [Tooltip("進行中で、まだ必要進捗に到達していない時")]
    MissionInProgress,

    [Tooltip("受注済みだが、まだ必要進捗に到達していない時")]
    MissionAcceptedNotComplete,

    [Tooltip("必要進捗に到達済みで、まだ報酬を受け取っていない時")]
    MissionObjectiveCompleteRewardUnclaimed,

    [Tooltip("ミッション状態がCompletedの時。報酬受取済みも含む場合があります")]
    MissionCompleted,

    [Tooltip("報酬を受け取り済みの時")]
    MissionRewardClaimed
}

/// <summary>
/// 住人に話しかけた時、現在のミッション状態に応じて開始ノードを切り替えるルールです。
/// </summary>
[Serializable]
public class TownDialogueStartRule
{
    [Header("説明")]
    [Tooltip("Inspectorで分かりやすくするためのメモです。処理には使いません。例：達成後の報告会話")]
    [SerializeField] private string label = "開始条件";

    [SerializeField] private bool enabled = true;

    [Header("条件")]
    [SerializeField]
    private TownDialogueStartCondition condition =
        TownDialogueStartCondition.Always;

    [Tooltip("ミッション状態で分岐する場合に設定します。Always以外では基本的に必要です。")]
    [SerializeField] private MissionDefinition2D mission;

    [Header("開始先")]
    [Tooltip("条件に一致した時に開始するNode番号です。")]
    [SerializeField, Min(0)] private int startNodeIndex;

    public string Label => label;
    public bool Enabled => enabled;
    public TownDialogueStartCondition Condition => condition;
    public MissionDefinition2D Mission => mission;
    public int StartNodeIndex => startNodeIndex;

    public bool IsMatch(
        GameSessionManager session,
        out string debugReason)
    {
        debugReason = string.Empty;

        if (!enabled)
        {
            debugReason = "Ruleが無効です。";
            return false;
        }

        if (condition == TownDialogueStartCondition.Always)
        {
            debugReason = string.IsNullOrWhiteSpace(label)
                ? "Always"
                : $"Always / {label}";
            return true;
        }

        if (mission == null)
        {
            debugReason =
                $"{condition}: Missionが未設定です。";
            return false;
        }

        string missionId = mission.MissionId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(missionId))
        {
            debugReason =
                $"{mission.DisplayName} のMission Idが空です。";
            return false;
        }

        MissionSessionData data = null;
        bool hasSession = session != null &&
            session.TryGetMissionSession(missionId, out data) &&
            data != null;

        bool accepted = hasSession &&
            data.State != MissionSessionState.Inactive;

        bool inProgress = hasSession &&
            data.State == MissionSessionState.InProgress;

        bool completed = hasSession &&
            data.State == MissionSessionState.Completed;

        bool rewardClaimed = hasSession && data.RewardClaimed;

        int progress = hasSession ? Mathf.Max(0, data.Progress) : 0;
        int required = hasSession
            ? Mathf.Max(1, data.RequiredAmount)
            : Mathf.Max(1, mission.RequiredAmount);

        bool objectiveComplete = completed ||
            (accepted && progress >= required);

        switch (condition)
        {
            case TownDialogueStartCondition.MissionNotAccepted:
                debugReason = BuildReason(
                    mission,
                    data,
                    !accepted,
                    "未受注"
                );
                return !accepted;

            case TownDialogueStartCondition.MissionAccepted:
                debugReason = BuildReason(
                    mission,
                    data,
                    accepted,
                    "受注済み"
                );
                return accepted;

            case TownDialogueStartCondition.MissionInProgress:
                debugReason = BuildReason(
                    mission,
                    data,
                    inProgress && !objectiveComplete,
                    "進行中・未達成"
                );
                return inProgress && !objectiveComplete;

            case TownDialogueStartCondition.MissionAcceptedNotComplete:
                debugReason = BuildReason(
                    mission,
                    data,
                    accepted && !objectiveComplete,
                    "受注済み・未達成"
                );
                return accepted && !objectiveComplete;

            case TownDialogueStartCondition.MissionObjectiveCompleteRewardUnclaimed:
                debugReason = BuildReason(
                    mission,
                    data,
                    objectiveComplete && !rewardClaimed,
                    "達成済み・報酬未受取"
                );
                return objectiveComplete && !rewardClaimed;

            case TownDialogueStartCondition.MissionCompleted:
                debugReason = BuildReason(
                    mission,
                    data,
                    completed,
                    "Completed"
                );
                return completed;

            case TownDialogueStartCondition.MissionRewardClaimed:
                debugReason = BuildReason(
                    mission,
                    data,
                    rewardClaimed,
                    "報酬受取済み"
                );
                return rewardClaimed;

            default:
                debugReason = "未対応の条件です。";
                return false;
        }
    }

    private static string BuildReason(
        MissionDefinition2D mission,
        MissionSessionData data,
        bool matched,
        string conditionLabel)
    {
        string missionName = mission != null
            ? mission.DisplayName
            : "Mission未設定";

        if (data == null)
        {
            return
                $"{missionName}: 保存データなし / 条件={conditionLabel} / 一致={matched}";
        }

        return
            $"{missionName}: State={data.State}, Progress={data.Progress}/{Mathf.Max(1, data.RequiredAmount)}, " +
            $"RewardClaimed={data.RewardClaimed} / 条件={conditionLabel} / 一致={matched}";
    }
}

/// <summary>
/// 会話中の選択肢が実行する処理です。
/// </summary>
public enum TownDialogueChoiceAction
{
    GoToNode,
    OpenPawnShop,
    StartMission,
    ClaimMissionReward,
    CloseDialogue
}

/// <summary>
/// 会話の1ページ分です。
/// Next Node Index を設定した場合、選択肢がない時にNextButtonで次のページへ進みます。
/// 選択肢を追加した場合は、選択肢が優先されます。
/// </summary>
[Serializable]
public class TownDialogueNode
{
    [Header("表示")]
    [Tooltip("空欄ならResident Dialogue Dataの住人名を使います")]
    [SerializeField] private string speakerNameOverride;

    [Tooltip("空欄ならResident Dialogue DataのDefault Portraitを使います")]
    [SerializeField] private Sprite portraitOverride;

    [SerializeField, TextArea(3, 8)]
    private string message = "会話内容";

    [Header("次の会話")]
    [Tooltip("選択肢がない時、NextButtonで進む先です。-1ならNextButtonは閉じるボタンになります")]
    [SerializeField] private int nextNodeIndex = -1;

    [Header("選択肢")]
    [SerializeField]
    private List<TownDialogueChoice> choices =
        new List<TownDialogueChoice>();

    public string SpeakerNameOverride => speakerNameOverride;
    public Sprite PortraitOverride => portraitOverride;
    public string Message => message ?? string.Empty;
    public int NextNodeIndex => nextNodeIndex;
    public int ChoiceCount => choices != null ? choices.Count : 0;

    public TownDialogueChoice GetChoice(int index)
    {
        if (choices == null || index < 0 || index >= choices.Count)
        {
            return null;
        }

        return choices[index];
    }
}

/// <summary>
/// 報酬アイテム1種類分です。
/// </summary>
[Serializable]
public class TownMissionRewardItem
{
    [SerializeField] private ItemData itemData;

    [SerializeField, Min(1)] private int amount = 1;

    public ItemData ItemData => itemData;
    public int Amount => Mathf.Max(1, amount);
}

/// <summary>
/// 会話中に表示する選択肢1つ分の設定です。
/// </summary>
[Serializable]
public class TownDialogueChoice
{
    [Header("表示")]
    [SerializeField] private string choiceText = "選択肢";

    [Header("実行内容")]
    [SerializeField]
    private TownDialogueChoiceAction action =
        TownDialogueChoiceAction.GoToNode;

    [Tooltip("Go To Node、Start Mission成功後、Claim Mission Reward成功後に続きの会話を表示したい時に設定します。-1なら会話を閉じます")]
    [SerializeField] private int nextNodeIndex = -1;

    [Header("ミッション受注用")]
    [Tooltip("ActionがStart Missionの時だけ設定します")]
    [SerializeField] private MissionDefinition2D missionToStart;

    [Tooltip("受注成功時、コンパスの追跡対象にも設定します")]
    [SerializeField] private bool trackMissionAfterStarting = true;

    [Header("ミッション報告・報酬用")]
    [Tooltip("ActionがClaim Mission Rewardの時に設定します。空欄ならMission To Startを代わりに使います")]
    [SerializeField] private MissionDefinition2D missionToClaimReward;

    [Tooltip("オンなら、ミッション達成済み、または進捗が必要数に到達している時だけ報酬を渡します")]
    [SerializeField] private bool requireObjectiveCompleted = true;

    [Tooltip("報酬として渡す所持金です")]
    [SerializeField, Min(0)] private int moneyReward;

    [Tooltip("報酬として渡すアイテムです。不要なら空のままでOKです")]
    [SerializeField]
    private List<TownMissionRewardItem> itemRewards =
        new List<TownMissionRewardItem>();

    public string ChoiceText => string.IsNullOrWhiteSpace(choiceText)
        ? "選択肢"
        : choiceText;

    public TownDialogueChoiceAction Action => action;
    public int NextNodeIndex => nextNodeIndex;
    public MissionDefinition2D MissionToStart => missionToStart;
    public bool TrackMissionAfterStarting => trackMissionAfterStarting;

    public MissionDefinition2D MissionToClaimReward =>
        missionToClaimReward != null
            ? missionToClaimReward
            : missionToStart;

    public bool RequireObjectiveCompleted => requireObjectiveCompleted;
    public int MoneyReward => Mathf.Max(0, moneyReward);
    public IReadOnlyList<TownMissionRewardItem> ItemRewards => itemRewards;
}
