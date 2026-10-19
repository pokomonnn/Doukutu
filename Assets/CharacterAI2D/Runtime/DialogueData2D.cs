using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>NPCとEnemyの共通会話データ。既存の洞窟会話データは変更しません。</summary>
[CreateAssetMenu(menuName = "2D Shooter/Dialogue Studio/会話データ", fileName = "Dialogue_New")]
public sealed class DialogueData2D : ScriptableObject
{
    [SerializeField] private string dialogueId = "dialogue_new";
    [SerializeField] private string displayName = "新しい会話";
    [SerializeField] private string startNodeId = "start";
    [SerializeField] private List<DialogueNode2D> nodes = new List<DialogueNode2D>();

    [Tooltip("上から順に評価。最初に成立した条件の開始ノードを使います。未成立なら通常の開始ノード") ]
    [SerializeField] private List<DialogueConditionalRoute2D> startRoutes = new List<DialogueConditionalRoute2D>();

    [Header("STEP4 会話アクション（選択肢のイベントIDと一致させます）")]
    [SerializeField] private List<DialogueAction2D> actions = new List<DialogueAction2D>();

    public IReadOnlyList<DialogueAction2D> Actions => actions;

    public DialogueAction2D FindAction(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId) || actions == null) return null;
        for (int i = 0; i < actions.Count; i++)
            if (actions[i] != null && string.Equals(actions[i].eventId, eventId, StringComparison.Ordinal))
                return actions[i];
        return null;
    }

    public string DialogueId => dialogueId;
    public string DisplayName => displayName;
    public string StartNodeId => startNodeId;
    public IReadOnlyList<DialogueNode2D> Nodes => nodes;
    public IReadOnlyList<DialogueConditionalRoute2D> StartRoutes => startRoutes;

    public string ResolveStartNodeId(DialogueActor2D actor) =>
        DialogueConditions2D.FirstMatchingTarget(startRoutes, actor, startNodeId);

    public DialogueNode2D FindNode(string nodeId)
    {
        if (string.IsNullOrEmpty(nodeId) || nodes == null) return null;
        for (int i = 0; i < nodes.Count; i++)
            if (nodes[i] != null && string.Equals(nodes[i].Id, nodeId, StringComparison.Ordinal))
                return nodes[i];
        return null;
    }

#if UNITY_EDITOR
    public void EditorInitialize(string newId, string title)
    {
        dialogueId = newId;
        displayName = title;
        startNodeId = "start";
        nodes = new List<DialogueNode2D>
        {
            new DialogueNode2D { id = "start", speakerName = "NPC", body = "こんにちは。", nextNodeId = "" }
        };
    }
#endif
}

[Serializable]
public sealed class DialogueNode2D
{
    public string id = "node";
    public string speakerName = "";
    [TextArea(3, 8)] public string body = "";
    [Tooltip("このノードを表示した際に発行する任意のイベントID（イベントを使わなければ空欄）")]
    public string onEnterEventId = "";
    [Tooltip("選択肢がない場合のみ使用。空欄なら会話終了")]
    public string nextNodeId = "";
    public List<DialogueChoice2D> choices = new List<DialogueChoice2D>();
    [Tooltip("選択肢がない場合の条件付き次ノード。上から優先、未成立なら次ノード") ]
    public List<DialogueConditionalRoute2D> nextRoutes = new List<DialogueConditionalRoute2D>();

    public string Id => id;
    public string SpeakerName => speakerName;
    public string Body => body;
    public string NextNodeId => nextNodeId;
    public string OnEnterEventId => onEnterEventId;
    public IReadOnlyList<DialogueChoice2D> Choices => choices;
    public IReadOnlyList<DialogueConditionalRoute2D> NextRoutes => nextRoutes;

    public string ResolveNextNodeId(DialogueActor2D actor) =>
        DialogueConditions2D.FirstMatchingTarget(nextRoutes, actor, nextNodeId);
}

[Serializable]
public sealed class DialogueChoice2D
{
    public string label = "選択肢";
    [Tooltip("空欄なら選択した時点で会話終了")]
    public string nextNodeId = "";
    [Tooltip("選んだ時に発行する任意のイベントID")]
    public string eventId = "";
    [Tooltip("すべて成立したときだけ、この選択肢を表示。0件なら常に表示") ]
    public List<DialogueCondition2D> conditions = new List<DialogueCondition2D>();

    public string Label => label;
    public string NextNodeId => nextNodeId;
    public string EventId => eventId;
    public IReadOnlyList<DialogueCondition2D> Conditions => conditions;
}
