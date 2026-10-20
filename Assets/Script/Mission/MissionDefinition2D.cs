using System;
using System.Collections.Generic;
using UnityEngine;

public enum MissionObjectiveType2D
{
    // 既存Assetのシリアライズ値を壊さないため数値を固定します。
    CollectItem = 0,
    DefeatTargetEnemy = 1,
    DeliverItem = 2,
    RescueTargetNpc = 3,
    StoryFlag = 4
}

/// <summary>
/// 出撃ミッション用のアイテム報酬1件分です。
/// NPC会話ミッションでは従来どおりTownConversationData側の報酬を使用できます。
/// </summary>
[Serializable]
public class MissionRewardItem
{
    [SerializeField] private ItemData itemData;
    [SerializeField, Min(1)] private int amount = 1;

    public ItemData ItemData => itemData;
    public int Amount => Mathf.Max(1, amount);
}

/// <summary>
/// ミッションの基本データです。
/// 収集・納品・指定敵討伐・指定NPC救出ミッションに対応します。
/// Enemy / Rescue NPCはMissionTargetIdentity2DのTarget Idで種類や個体を識別できます。
/// </summary>
[CreateAssetMenu(
    fileName = "NewMissionDefinition",
    menuName = "Mission/Mission Definition 2D"
)]
public class MissionDefinition2D : ScriptableObject
{
    [Header("共通の目標表示（空欄なら従来の進捗表示）")]
    [SerializeField] private List<MissionObjectiveDisplay2D> displayObjectives = new List<MissionObjectiveDisplay2D>();
    [SerializeField] private string objectiveFinishFlag;
    public IReadOnlyList<MissionObjectiveDisplay2D> DisplayObjectives => displayObjectives;
    public bool HasDisplayObjectives => displayObjectives != null && displayObjectives.Count > 0;
    public string ObjectiveFinishFlag => objectiveFinishFlag;
    [SerializeField] private string completionStoryFlag;
    public string CompletionStoryFlag => completionStoryFlag;
    [SerializeField] private string activationStoryFlag;
    public string ActivationStoryFlag => string.IsNullOrEmpty(activationStoryFlag)
        ? completionStoryFlag?.Replace("_Completed", "_Started") : activationStoryFlag;
    [Header("ストーリー現地報告報酬（結果画面では付与しません）")]
    [SerializeField, Min(0)] private int storyMoneyReward;
    public int StoryMoneyReward => Mathf.Max(0, storyMoneyReward);
    [Header("基本情報")]
    [Tooltip("重複しない管理用IDです。空欄でも動作しますが、設定しておくと分かりやすいです")]
    [SerializeField] private string missionId = "mission_id";

    [Tooltip("HUDやデバッグ表示に使うミッション名です")]
    [SerializeField] private string displayName = "新しいミッション";

    [SerializeField, TextArea(2, 4)]
    private string description;

    [Tooltip("町の出撃パネルなど、ミッション詳細表示に使用するイラストです。")]
    [SerializeField]
    private Sprite missionImage;

    [Header("目的")]
    [SerializeField]
    private MissionObjectiveType2D objectiveType =
        MissionObjectiveType2D.CollectItem;

    [Header("必要数")]
    [Tooltip(
        "ミッション達成に必要な数です。\n" +
        "Collect Item / Deliver Item = 必要Item数\n" +
        "Defeat Target Enemy = 討伐数\n" +
        "Rescue Target Npc = 救出数"
    )]
    [SerializeField, Min(1)] private int requiredAmount = 1;

    [Header("アイテム回収・納品ミッション")]
    [Tooltip("Collect Item / Deliver Item の時に必要なItemDataです")]
    [SerializeField] private ItemData requiredItem;

    [Tooltip("Collect Item専用です。オンならミッション開始時にすでに持っている必要アイテムも進捗に数えます。Deliver Itemでは使用しません")]
    [SerializeField] private bool countItemsAlreadyHeldWhenMissionStarts;

    [Tooltip(
        "Collect Item専用です。ONにすると、出撃パネルから受注したこのミッションを達成した時、" +
        "Result Sceneで報酬を受け取る際にRequired ItemをRequired Amount分だけ所持品から回収（削除）します。" +
        "既存ミッションへの影響を避けるため初期値はOFFです。"
    )]
    [SerializeField] private bool consumeCollectedItemsOnResult;

    [Header("指定敵討伐ミッション")]
    [Tooltip(
        "Defeat Target Enemy専用です。対象Enemy Prefabに付けるMissionTargetIdentity2Dの" +
        "Target TypeをEnemy、Target Idをここで設定したIDと同じにしてください。" +
        "空欄の場合はMissionManager2DのTarget Enemy直接参照を使う旧方式も利用できます。"
    )]
    [SerializeField] private string targetEnemyId;

    [Header("指定NPC救出ミッション")]
    [Tooltip(
        "Rescue Target Npc専用です。対象NPC Prefabに付けるMissionTargetIdentity2Dの" +
        "Target TypeをRescue Npc、Target Idをここで設定したIDと同じにしてください。" +
        "旧MissionRescueTarget2Dにも互換対応します。"
    )]
    [SerializeField] private string targetRescueNpcId;

    [Header("出撃ミッション報酬")]
    [Tooltip(
        "出撃パネルから受注した時に、Result Sceneで受け取る所持金報酬です。" +
        "NPC会話ミッションのMoney Rewardは従来どおりTownConversationData側を使用してください。"
    )]
    [SerializeField, Min(0)] private int expeditionMoneyReward;

    [Tooltip(
        "出撃パネルから受注した時に、Result Sceneで受け取るアイテム報酬です。" +
        "NPC会話ミッションのItem Rewardsとは別管理です。"
    )]
    [SerializeField]
    private List<MissionRewardItem> expeditionItemRewards =
        new List<MissionRewardItem>();

    [Header("スキル報酬")]
    [Tooltip("このミッションの報酬受取時に永久取得するスキルカードです。複数設定できます。")]
    [SerializeField]
    private List<SkillCardData> skillCardRewards =
        new List<SkillCardData>();

    [Tooltip("このミッションの報酬受取時に解放するスキル装備枠数です。0なら解放なし。最大7枠までです。")]
    [SerializeField, Range(0, 4)]
    private int skillSlotUnlockAmount;

    public string MissionId => missionId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName;
    public string Description => description ?? string.Empty;
    public Sprite MissionImage => missionImage;

    public MissionObjectiveType2D ObjectiveType => objectiveType;

    public ItemData RequiredItem => requiredItem;
    public int RequiredAmount => Mathf.Max(1, requiredAmount);

    public bool CountItemsAlreadyHeldWhenMissionStarts =>
        countItemsAlreadyHeldWhenMissionStarts;

    public bool ConsumeCollectedItemsOnResult =>
        consumeCollectedItemsOnResult;

    public string TargetEnemyId =>
        targetEnemyId?.Trim() ?? string.Empty;

    public string TargetRescueNpcId =>
        targetRescueNpcId?.Trim() ?? string.Empty;

    public int ExpeditionMoneyReward =>
        Mathf.Max(0, expeditionMoneyReward);

    public IReadOnlyList<MissionRewardItem> ExpeditionItemRewards =>
        expeditionItemRewards;

    public IReadOnlyList<SkillCardData> SkillCardRewards =>
        skillCardRewards;

    public int SkillSlotUnlockAmount =>
        Mathf.Clamp(skillSlotUnlockAmount, 0, 4);

    private void OnValidate()
    {
        requiredAmount = Mathf.Max(1, requiredAmount);
        missionId = missionId?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        targetEnemyId = targetEnemyId?.Trim() ?? string.Empty;
        targetRescueNpcId = targetRescueNpcId?.Trim() ?? string.Empty;
        expeditionMoneyReward = Mathf.Max(0, expeditionMoneyReward);
        skillSlotUnlockAmount = Mathf.Clamp(skillSlotUnlockAmount, 0, 4);

        if (expeditionItemRewards == null)
        {
            expeditionItemRewards = new List<MissionRewardItem>();
        }

        if (skillCardRewards == null)
        {
            skillCardRewards = new List<SkillCardData>();
        }
    }
}
