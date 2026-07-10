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

    [Header("会話ノード")]
    [SerializeField]
    private List<TownDialogueNode> nodes =
        new List<TownDialogueNode>();

    public string ResidentId => residentId;

    public string ResidentName => string.IsNullOrWhiteSpace(residentName)
        ? name
        : residentName;

    public Sprite DefaultPortrait => defaultPortrait;

    public int NodeCount => nodes != null ? nodes.Count : 0;

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

    private void OnValidate()
    {
        residentId = residentId?.Trim() ?? string.Empty;
        residentName = residentName?.Trim() ?? string.Empty;
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
