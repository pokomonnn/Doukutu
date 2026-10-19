using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>STEP3: 戦闘中の短い頭上セリフ。通常の会話ノードとは独立したアセット。</summary>
public enum DialogueBattleBarkTrigger2D
{
    [InspectorName("敵を発見")] Spotted = 0,
    [InspectorName("攻撃")] Attack = 1,
    [InspectorName("被弾")] Damaged = 2,
    [InspectorName("HP低下")] LowHealth = 3,
    [InspectorName("逃走開始")] Flee = 4,
    [InspectorName("身をすくめる")] Cower = 5,
    [InspectorName("仲間へ警報")] AllyAlert = 6,
    [InspectorName("敵を見失う")] LostTarget = 7,
    [InspectorName("死亡")] Death = 8
}

[Serializable]
public sealed class DialogueBattleBarkLine2D
{
    public DialogueBattleBarkTrigger2D trigger = DialogueBattleBarkTrigger2D.Spotted;
    [TextArea(2, 4)] public string text = "敵だ！";
    [Min(1)] public int weight = 1;
    [Range(0f, 1f)] public float probability = 1f;
    [Tooltip("STEP2の会話条件を流用。0件なら常に候補になります。")]
    public List<DialogueCondition2D> conditions = new List<DialogueCondition2D>();
}

[CreateAssetMenu(menuName = "2D Shooter/Dialogue Studio/戦闘セリフデータ", fileName = "BattleBarks_New")]
public sealed class DialogueBattleBarkData2D : ScriptableObject
{
    [SerializeField] private string displayName = "新しい戦闘セリフ";
    [SerializeField] private List<DialogueBattleBarkLine2D> lines = new List<DialogueBattleBarkLine2D>();
    public string DisplayName => displayName;
    public IReadOnlyList<DialogueBattleBarkLine2D> Lines => lines;

#if UNITY_EDITOR
    public void EditorInitialize(string title)
    {
        displayName = title;
        lines = new List<DialogueBattleBarkLine2D>
        {
            new DialogueBattleBarkLine2D { trigger = DialogueBattleBarkTrigger2D.Spotted, text = "誰だ！" },
            new DialogueBattleBarkLine2D { trigger = DialogueBattleBarkTrigger2D.Damaged, text = "ぐっ……！" },
            new DialogueBattleBarkLine2D { trigger = DialogueBattleBarkTrigger2D.Flee, text = "いったん退くぞ！" }
        };
    }
#endif
}
