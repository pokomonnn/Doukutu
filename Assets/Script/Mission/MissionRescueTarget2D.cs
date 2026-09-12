using UnityEngine;

/// <summary>
/// 「指定NPC救出ミッション」で対象を識別するためのIDコンポーネントです。
/// 救出対象NPCのPrefab（またはその子）へ付け、MissionDefinition2Dの
/// Target Rescue Npc Idと同じIDを設定してください。
/// </summary>
[DisallowMultipleComponent]
public class MissionRescueTarget2D : MonoBehaviour
{
    [Header("指定NPC救出ミッション")]
    [Tooltip(
        "この救出NPCを識別するIDです。" +
        "MissionDefinition2DのTarget Rescue Npc Idと完全に同じ文字列を設定してください。"
    )]
    [SerializeField] private string rescueTargetId = "rescue_target_01";

    [Tooltip("Inspectorやログ確認用の名前です。判定自体には使用しません。")]
    [SerializeField] private string targetDisplayName;

    public string RescueTargetId =>
        rescueTargetId?.Trim() ?? string.Empty;

    public string TargetDisplayName =>
        string.IsNullOrWhiteSpace(targetDisplayName)
            ? gameObject.name
            : targetDisplayName.Trim();

    private void OnValidate()
    {
        rescueTargetId = rescueTargetId?.Trim() ?? string.Empty;
        targetDisplayName = targetDisplayName?.Trim() ?? string.Empty;
    }
}
