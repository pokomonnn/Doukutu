using System;
using UnityEngine;
using UnityEngine.Events;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

[Serializable]
public sealed class DialogueEventString2D : UnityEvent<string> { }

/// <summary>
/// NPC/EnemyのPrefabへ追加し、DialogueData2Dを割り当てます。
/// 既存のNPC会話と操作が重複しないよう、Eキー操作は初期状態ではOFFです。
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueActor2D : MonoBehaviour
{
    [Header("会話データ")]
    [SerializeField] private DialogueData2D dialogue;

    [Header("操作（既存会話との重複を避けるため初期OFF）")]
    [SerializeField] private bool useEKeyInteraction = false;
    [SerializeField, Min(0.1f)] private float interactionDistance = 2.5f;
    [SerializeField] private Transform playerTransform;
    [SerializeField] private bool preventWhenDead = true;
    [SerializeField] private bool preventWhenCarried = true;
    [SerializeField] private bool preventWhenHostileToPlayer = false;

    [Header("イベントID通知（ショップ等との連携用・任意）")]
    [SerializeField] private DialogueEventString2D onDialogueEvent = new DialogueEventString2D();
    public event Action<string> EventRaised;

    public DialogueData2D Dialogue => dialogue;
    public bool UseEKeyInteraction => useEKeyInteraction;

    private void Update()
    {
        if (!useEKeyInteraction) return;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current == null || !Keyboard.current.eKey.wasPressedThisFrame) return;
#else
        if (!Input.GetKeyDown(KeyCode.E)) return;
#endif
        if (DialoguePresenter2D.Active != null && DialoguePresenter2D.Active.IsPlaying) return;
        TryStartDialogue();
    }

    public bool TryStartDialogue()
    {
        if (dialogue == null || !isActiveAndEnabled) return false;
        CharacterHealth hp = GetComponent<CharacterHealth>();
        if (preventWhenDead && hp != null && hp.IsDead) return false;

        if (preventWhenCarried)
        {
            // 実際の運搬APIを変更せず、既存コンポーネントの読み取りのみ行う。
            var carried = GetComponent<CarryableObject2D>();
            if (carried != null && carried.IsCarried) return false;
        }

        if (preventWhenHostileToPlayer)
        {
            FactionMember2D faction = GetComponent<FactionMember2D>();
            if (faction != null && FactionHostility2D.IsHostileToPlayer(faction)) return false;
        }

        if (useEKeyInteraction)
        {
            if (playerTransform == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag("Player");
                if (player != null) playerTransform = player.transform;
            }
            if (playerTransform == null ||
                Vector2.Distance(playerTransform.position, transform.position) > interactionDistance)
                return false;
        }

        DialoguePresenter2D presenter = DialoguePresenter2D.Active;
        if (presenter == null)
            presenter = FindFirstObjectByType<DialoguePresenter2D>();
        if (presenter == null)
        {
            Debug.LogWarning("[Dialogue Studio] DialoguePresenter2Dがシーンにありません。Tools > 2D Shooter > Dialogue Studio > テスト用会話UIをシーンに作成 を実行してください。", this);
            return false;
        }
        return presenter.Play(dialogue, this);
    }

    internal void RaiseDialogueEvent(string eventId)
    {
        if (string.IsNullOrWhiteSpace(eventId)) return;
        onDialogueEvent?.Invoke(eventId);
        EventRaised?.Invoke(eventId);
    }
}
