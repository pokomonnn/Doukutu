using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>STEP5: NPC同士の立ち話。通常会話/戦闘セリフとは独立したデータ。</summary>
public enum DialogueAmbientSpeaker2D
{
    [InspectorName("会話を始めたキャラ")] Initiator = 0,
    [InspectorName("話しかけられたキャラ")] Partner = 1
}

[Serializable]
public sealed class DialogueAmbientLine2D
{
    [Tooltip("どちらが話すか")]
    public DialogueAmbientSpeaker2D speaker = DialogueAmbientSpeaker2D.Initiator;
    [TextArea(2, 4)] public string text = "こんにちは。";
    [Min(0.25f)] public float displaySeconds = 2.4f;
    [Min(0f)] public float pauseAfterSeconds = .35f;
}

[Serializable]
public sealed class DialogueAmbientExchange2D
{
    [Header("600会話集（未設定なら旧STEP5と同じ動作）")]
    [Tooltip("元TXTの会話ID。読み直しても同じ会話を識別できます。")]
    public string sourceDialogueId;
    [Tooltip("0=全ステージ、1=旧居住区、2=工房区、3=聖堂区")]
    [Range(0, 3)] public int requiredStage;
    [Tooltip("異派閥会話の話者側。空欄なら派閥は限定しません。")]
    public string requiredInitiatorFactionId;
    [Tooltip("異派閥会話の相手側。空欄なら派閥は限定しません。")]
    public string requiredPartnerFactionId;
    [Tooltip("出典の場面。現段階では編集用メモで、実際の行動状態の自動判定はしません。")]
    public string sourceSituation;
    [Tooltip("出典の前提条件（参考メモ）")]
    public string sourcePrerequisites;
    [Tooltip("出典の性格目安。強制フィルタにはしません。")]
    public string sourcePersonalityHint;
    [Tooltip("出典の再生目安（参考メモ）")]
    public string sourcePlaybackHint;
    public string title = "日常の立ち話";
    [Min(1)] public int weight = 1;
    [Range(0f, 1f)] public float probability = 1f;
    [Tooltip("STEP2と同じ条件。判定対象は会話を開始したキャラ。条件0件は無条件。")]
    public List<DialogueCondition2D> conditions = new List<DialogueCondition2D>();
    [Tooltip("会話したい性格を限定（オフならどの性格でも可）")]
    public bool requireInitiatorPersonality;
    public CharacterAIPersonalityType2D initiatorPersonality;
    public bool requirePartnerPersonality;
    public CharacterAIPersonalityType2D partnerPersonality;
    public List<DialogueAmbientLine2D> lines = new List<DialogueAmbientLine2D>();
}

[CreateAssetMenu(menuName = "2D Shooter/Dialogue Studio/STEP5 NPC同士の会話データ", fileName = "AmbientTalk_New")]
public sealed class DialogueAmbientTalkData2D : ScriptableObject
{
    [SerializeField] private string displayName = "新しい立ち話";
    [SerializeField] private List<DialogueAmbientExchange2D> exchanges = new List<DialogueAmbientExchange2D>();
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? name : displayName;
    public IReadOnlyList<DialogueAmbientExchange2D> Exchanges => exchanges;

#if UNITY_EDITOR
    public void EditorCreateDefault(string title)
    {
        displayName = title;
        exchanges = new List<DialogueAmbientExchange2D>
        {
            new DialogueAmbientExchange2D
            {
                title = "近況を話す",
                lines = new List<DialogueAmbientLine2D>
                {
                    new DialogueAmbientLine2D { speaker = DialogueAmbientSpeaker2D.Initiator, text = "最近、洞窟の様子が変だな。", displaySeconds = 2.5f },
                    new DialogueAmbientLine2D { speaker = DialogueAmbientSpeaker2D.Partner, text = "ああ。気をつけたほうがいい。", displaySeconds = 2.6f },
                    new DialogueAmbientLine2D { speaker = DialogueAmbientSpeaker2D.Initiator, text = "分かった。またな。", displaySeconds = 2.1f }
                }
            }
        };
    }
#endif
}
