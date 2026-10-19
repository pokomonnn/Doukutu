using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>STEP2: 既存の派閥/カルマ/性格データを読み取るだけ。値を変更しません。</summary>
public enum DialogueConditionKind2D
{
    [InspectorName("カルマが指定値以上")] KarmaAtLeast = 0,
    [InspectorName("カルマが指定値以下")] KarmaAtMost = 1,
    [InspectorName("派閥友好度が指定値以上")] ReputationAtLeast = 2,
    [InspectorName("派閥友好度が指定値以下")] ReputationAtMost = 3,
    [InspectorName("話者の所属派閥が一致")] ActorFactionIs = 4,
    [InspectorName("話者の性格が一致")] ActorPersonalityIs = 5,
    [InspectorName("話者がPlayerに敵対")] ActorHostileToPlayer = 6,
    [InspectorName("話者がPlayerに敵対していない")] ActorNotHostileToPlayer = 7
}

[Serializable]
public sealed class DialogueCondition2D
{
    public DialogueConditionKind2D kind = DialogueConditionKind2D.KarmaAtLeast;
    [Tooltip("カルマ・友好度の判定に使用。-100～100")]
    [Range(-100, 100)] public int threshold;
    [Tooltip("友好度条件なら対象派閥、所属条件なら話者の派閥。例: bandits")]
    public string factionId = "bandits";
    public CharacterAIPersonalityType2D personality = CharacterAIPersonalityType2D.Balanced;
}

[Serializable]
public sealed class DialogueConditionalRoute2D
{
    [Tooltip("エディター上のメモ。評価には使いません")]
    public string label = "条件分岐";
    [Tooltip("条件が成立した場合のジャンプ先ノード")]
    public string targetNodeId = "";
    [Tooltip("すべて成立した場合に分岐。0件なら常に成立するので注意")]
    public List<DialogueCondition2D> conditions = new List<DialogueCondition2D>();
}

public static class DialogueConditions2D
{
    /// <summary>
    /// 同じリスト内の条件はAND、ルートは上から優先。参照コンポーネントがない場合は一致しません。
    /// 条件が0件なら常に成立（選択肢の従来互換）。
    /// </summary>
    public static bool AllMatch(IReadOnlyList<DialogueCondition2D> conditions, DialogueActor2D actor)
    {
        if (conditions == null || conditions.Count == 0) return true;
        for (int i = 0; i < conditions.Count; i++)
            if (conditions[i] == null || !Matches(conditions[i], actor)) return false;
        return true;
    }

    public static bool Matches(DialogueCondition2D condition, DialogueActor2D actor)
    {
        if (condition == null) return false;
        switch (condition.kind)
        {
            case DialogueConditionKind2D.KarmaAtLeast:
            case DialogueConditionKind2D.KarmaAtMost:
            case DialogueConditionKind2D.ReputationAtLeast:
            case DialogueConditionKind2D.ReputationAtMost:
            {
                PlayerKarmaReputation2D state = PlayerKarmaReputation2D.GetCurrent();
                if (state == null) return false;
                if (condition.kind == DialogueConditionKind2D.KarmaAtLeast)
                    return state.Karma >= condition.threshold;
                if (condition.kind == DialogueConditionKind2D.KarmaAtMost)
                    return state.Karma <= condition.threshold;
                if (string.IsNullOrWhiteSpace(condition.factionId)) return false;
                int reputation = state.GetReputation(condition.factionId);
                return condition.kind == DialogueConditionKind2D.ReputationAtLeast
                    ? reputation >= condition.threshold : reputation <= condition.threshold;
            }
            case DialogueConditionKind2D.ActorFactionIs:
            {
                FactionMember2D faction = FindFaction(actor);
                return faction != null && !string.IsNullOrWhiteSpace(condition.factionId) &&
                    FactionId2D.Same(faction.FactionId, condition.factionId);
            }
            case DialogueConditionKind2D.ActorPersonalityIs:
            {
                CharacterAIPersonality2D personality = actor != null
                    ? actor.GetComponentInParent<CharacterAIPersonality2D>() : null;
                if (personality == null && actor != null)
                    personality = actor.GetComponentInChildren<CharacterAIPersonality2D>();
                return personality != null && personality.PersonalityType == condition.personality;
            }
            case DialogueConditionKind2D.ActorHostileToPlayer:
            case DialogueConditionKind2D.ActorNotHostileToPlayer:
            {
                FactionMember2D faction = FindFaction(actor);
                if (faction == null) return false;
                bool hostile = FactionHostility2D.IsHostileToPlayer(faction);
                return condition.kind == DialogueConditionKind2D.ActorHostileToPlayer ? hostile : !hostile;
            }
        }
        return false;
    }

    public static string FirstMatchingTarget(IReadOnlyList<DialogueConditionalRoute2D> routes,
        DialogueActor2D actor, string fallback)
    {
        if (routes != null)
        {
            for (int i = 0; i < routes.Count; i++)
            {
                DialogueConditionalRoute2D route = routes[i];
                if (route != null && AllMatch(route.conditions, actor))
                    return route.targetNodeId ?? ""; // 空文字=その時点で会話終了
            }
        }
        return fallback ?? "";
    }

    private static FactionMember2D FindFaction(DialogueActor2D actor)
    {
        if (actor == null) return null;
        FactionMember2D faction = actor.GetComponentInParent<FactionMember2D>();
        return faction != null ? faction : actor.GetComponentInChildren<FactionMember2D>();
    }
}
