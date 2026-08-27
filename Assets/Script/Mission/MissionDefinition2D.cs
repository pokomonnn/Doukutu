using System.Collections.Generic;
using UnityEngine;

public enum MissionObjectiveType2D
{
    // 既存Assetのシリアライズ値を壊さないため数値を固定します。
    CollectItem = 0,
    DefeatTargetEnemy = 1,
    DeliverItem = 2
}

/// <summary>
/// ミッションの基本データです。
/// 収集ミッション、NPCなどへの納品ミッション、特定の敵を倒すミッションに対応します。
/// 実際のコンパスの行き先や対象敵は、MissionManager2D側でシーンごとに設定します。
/// </summary>
[CreateAssetMenu(
    fileName = "NewMissionDefinition",
    menuName = "Mission/Mission Definition 2D"
)]
public class MissionDefinition2D : ScriptableObject
{
    [Header("基本情報")]
    [Tooltip("重複しない管理用IDです。空欄でも動作しますが、設定しておくと分かりやすいです")]
    [SerializeField] private string missionId = "mission_id";

    [Tooltip("HUDやデバッグ表示に使うミッション名です")]
    [SerializeField] private string displayName = "新しいミッション";

    [SerializeField, TextArea(2, 4)]
    private string description;

    [Header("目的")]
    [SerializeField]
    private MissionObjectiveType2D objectiveType =
        MissionObjectiveType2D.CollectItem;

    [Header("アイテム回収・納品ミッション")]
    [Tooltip("Collect Item / Deliver Item の時に必要なItemDataです")]
    [SerializeField] private ItemData requiredItem;

    [Tooltip("必要な個数です")]
    [SerializeField, Min(1)] private int requiredAmount = 1;

    [Tooltip("Collect Item専用です。オンならミッション開始時にすでに持っている必要アイテムも進捗に数えます。Deliver Itemでは使用しません")]
    [SerializeField] private bool countItemsAlreadyHeldWhenMissionStarts;

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

    public MissionObjectiveType2D ObjectiveType => objectiveType;

    public ItemData RequiredItem => requiredItem;
    public int RequiredAmount => Mathf.Max(1, requiredAmount);

    public bool CountItemsAlreadyHeldWhenMissionStarts =>
        countItemsAlreadyHeldWhenMissionStarts;

    public IReadOnlyList<SkillCardData> SkillCardRewards =>
        skillCardRewards;

    public int SkillSlotUnlockAmount =>
        Mathf.Clamp(skillSlotUnlockAmount, 0, 4);

    private void OnValidate()
    {
        requiredAmount = Mathf.Max(1, requiredAmount);
        missionId = missionId?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        skillSlotUnlockAmount = Mathf.Clamp(skillSlotUnlockAmount, 0, 4);

        if (skillCardRewards == null)
        {
            skillCardRewards = new List<SkillCardData>();
        }
    }
}
