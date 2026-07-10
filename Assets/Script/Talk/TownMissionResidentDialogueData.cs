using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ミッションをくれる住人専用の、分かりやすい会話データです。
/// Node番号やStart Rulesを使わず、ミッション状態ごとの文章を直接設定します。
/// </summary>
[CreateAssetMenu(
    fileName = "NewTownMissionResidentDialogue",
    menuName = "Town/Mission Resident Dialogue Data"
)]
public class TownMissionResidentDialogueData : ScriptableObject
{
    [Header("住人の表示")]
    [SerializeField] private string residentName = "住人";
    [SerializeField] private Sprite portrait;

    [Header("対象ミッション")]
    [Tooltip("この住人が担当するミッションです。Mission Idを必ず設定してください。")]
    [SerializeField] private MissionDefinition2D mission;

    [Header("受注設定")]
    [SerializeField] private bool trackMissionAfterAccept = true;

    [Tooltip("受注ボタンを押した直後、ここに文章があれば表示します。空なら受注中の会話を表示します。")]
    [SerializeField]
    private List<string> acceptedJustNowLines = new List<string>
    {
        "よろしく頼む。"
    };

    [Header("会話：未受注")]
    [SerializeField]
    private List<string> beforeAcceptLines = new List<string>
    {
        "頼みたいことがあるんだ。引き受けてくれないか？"
    };

    [Header("会話：受注中・未達成")]
    [SerializeField]
    private List<string> inProgressLines = new List<string>
    {
        "まだ終わっていないようだな。引き続き頼む。"
    };

    [Header("会話：達成済み・報酬未受取")]
    [SerializeField]
    private List<string> readyToReportLines = new List<string>
    {
        "おお、やってくれたのか。報酬を渡そう。"
    };

    [Header("会話：報酬受取後")]
    [SerializeField]
    private List<string> rewardClaimedLines = new List<string>
    {
        "この前は助かったよ。また何かあったら頼む。"
    };

    [Header("ボタン表示")]
    [SerializeField] private string acceptButtonText = "引き受ける";
    [SerializeField] private string claimRewardButtonText = "報酬を受け取る";
    [SerializeField] private string nextButtonText = "次へ";
    [SerializeField] private string closeButtonText = "閉じる";

    [Header("報酬")]
    [Tooltip("オンなら、ミッション達成済み、または進捗が必要数に到達している時だけ報酬を渡します。")]
    [SerializeField] private bool requireObjectiveCompleted = true;

    [SerializeField, Min(0)] private int moneyReward;

    [Tooltip("報酬として渡すアイテムです。不要なら空のままでOKです。")]
    [SerializeField]
    private List<TownMissionResidentRewardItem> itemRewards =
        new List<TownMissionResidentRewardItem>();

    public string ResidentName => string.IsNullOrWhiteSpace(residentName)
        ? name
        : residentName;

    public Sprite Portrait => portrait;
    public MissionDefinition2D Mission => mission;
    public bool TrackMissionAfterAccept => trackMissionAfterAccept;
    public bool RequireObjectiveCompleted => requireObjectiveCompleted;
    public int MoneyReward => Mathf.Max(0, moneyReward);
    public IReadOnlyList<TownMissionResidentRewardItem> ItemRewards => itemRewards;

    public string AcceptButtonText => string.IsNullOrWhiteSpace(acceptButtonText)
        ? "引き受ける"
        : acceptButtonText;

    public string ClaimRewardButtonText => string.IsNullOrWhiteSpace(claimRewardButtonText)
        ? "報酬を受け取る"
        : claimRewardButtonText;

    public string NextButtonText => string.IsNullOrWhiteSpace(nextButtonText)
        ? "次へ"
        : nextButtonText;

    public string CloseButtonText => string.IsNullOrWhiteSpace(closeButtonText)
        ? "閉じる"
        : closeButtonText;

    public IReadOnlyList<string> GetLines(TownMissionResidentState state)
    {
        switch (state)
        {
            case TownMissionResidentState.NotAccepted:
                return GetSafeLines(beforeAcceptLines, "何か頼みたいことがあるんだ。");

            case TownMissionResidentState.AcceptedJustNow:
                if (acceptedJustNowLines != null && acceptedJustNowLines.Count > 0)
                {
                    return GetSafeLines(acceptedJustNowLines, "よろしく頼む。");
                }

                return GetSafeLines(inProgressLines, "引き続き頼む。");

            case TownMissionResidentState.InProgress:
                return GetSafeLines(inProgressLines, "まだ終わっていないようだな。引き続き頼む。");

            case TownMissionResidentState.ReadyToReport:
                return GetSafeLines(readyToReportLines, "おお、やってくれたのか。報酬を渡そう。");

            case TownMissionResidentState.RewardClaimed:
                return GetSafeLines(rewardClaimedLines, "この前は助かったよ。");

            default:
                return GetSafeLines(beforeAcceptLines, "会話データが正しく設定されていません。");
        }
    }

    private static IReadOnlyList<string> GetSafeLines(
        List<string> source,
        string fallback)
    {
        if (source == null || source.Count == 0)
        {
            return new List<string> { fallback };
        }

        return source;
    }

    private void OnValidate()
    {
        residentName = residentName?.Trim() ?? string.Empty;
        acceptButtonText = acceptButtonText?.Trim() ?? string.Empty;
        claimRewardButtonText = claimRewardButtonText?.Trim() ?? string.Empty;
        nextButtonText = nextButtonText?.Trim() ?? string.Empty;
        closeButtonText = closeButtonText?.Trim() ?? string.Empty;
    }
}

public enum TownMissionResidentState
{
    Invalid,
    NotAccepted,
    AcceptedJustNow,
    InProgress,
    ReadyToReport,
    RewardClaimed
}

/// <summary>
/// ミッション住人専用の報酬アイテム設定です。
/// 既存のTownMissionRewardItemとは別名なので競合しません。
/// </summary>
[Serializable]
public class TownMissionResidentRewardItem
{
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int amount = 1;

    public ItemData ItemData => itemData;
    public int Amount => Mathf.Max(1, amount);
}
