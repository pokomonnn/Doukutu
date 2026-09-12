using UnityEngine;

public enum MissionTargetType2D
{
    None = 0,
    Enemy = 1,
    RescueNpc = 2
}

/// <summary>
/// ミッション対象となるPrefabへ付ける共通IDコンポーネントです。
/// Enemy / Rescue NPCの種類とTarget IdをInspectorから設定できます。
/// MissionDefinition2D側はこのTarget Idを使って対象を判定します。
/// </summary>
[DisallowMultipleComponent]
public class MissionTargetIdentity2D : MonoBehaviour
{
    [Header("ミッション対象ID")]
    [Tooltip("このPrefabをミッション上でどの種類の対象として扱うか設定します。")]
    [SerializeField] private MissionTargetType2D targetType =
        MissionTargetType2D.None;

    [Tooltip(
        "ミッション判定に使うIDです。\n" +
        "Enemy例: enemy_cave_spider\n" +
        "Rescue NPC例: npc_doctor_01\n" +
        "同じ種類として数えたいPrefabは同じIDを設定してください。"
    )]
    [SerializeField] private string targetId = "target_id";

    [Tooltip("Inspector・ログ表示用の名前です。ミッション判定そのものには使用しません。")]
    [SerializeField] private string displayName;

    public MissionTargetType2D TargetType => targetType;

    public string TargetId =>
        targetId?.Trim() ?? string.Empty;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? gameObject.name
            : displayName.Trim();

    public bool HasValidId =>
        !string.IsNullOrWhiteSpace(TargetId);

    public bool Matches(
        MissionTargetType2D requiredType,
        string requiredId)
    {
        if (targetType != requiredType ||
            string.IsNullOrWhiteSpace(requiredId))
        {
            return false;
        }

        return string.Equals(
            TargetId,
            requiredId.Trim(),
            System.StringComparison.OrdinalIgnoreCase
        );
    }

    private void OnValidate()
    {
        targetId = targetId?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
    }
}
