using UnityEngine;

/// <summary>洞窟NPCの識別情報。NPC Studioから作成・編集します。</summary>
[DisallowMultipleComponent]
public sealed class DungeonNpcIdentity2D : MonoBehaviour
{
    [SerializeField] private string npcId = "cave_npc";
    [SerializeField] private string displayName = "洞窟の住人";
    [SerializeField] private string occupation = "旅人";
    [TextArea(2, 4)] [SerializeField] private string description;
    [SerializeField] private DungeonNpcTradeProfile2D tradeProfile;
    public string NpcId => npcId;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public string Occupation => occupation;
    public string Description => description;
    public DungeonNpcTradeProfile2D TradeProfile => tradeProfile;
}
