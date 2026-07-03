using UnityEngine;

public enum MissionObjectiveType2D
{
    CollectItem,
    DefeatTargetEnemy
}

/// <summary>
/// ミッションの基本データです。
/// 収集ミッションと、特定の敵を倒すミッションに対応します。
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

    [Header("アイテム回収ミッション")]
    [Tooltip("Collect Itemの時に必要なItemDataです")]
    [SerializeField] private ItemData requiredItem;

    [Tooltip("必要な個数です")]
    [SerializeField, Min(1)] private int requiredAmount = 1;

    [Tooltip("オンならミッション開始時にすでに持っている必要アイテムも進捗に数えます。オフなら開始後に増えた個数だけを数えます")]
    [SerializeField] private bool countItemsAlreadyHeldWhenMissionStarts;

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

    private void OnValidate()
    {
        requiredAmount = Mathf.Max(1, requiredAmount);
        missionId = missionId?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
    }
}
