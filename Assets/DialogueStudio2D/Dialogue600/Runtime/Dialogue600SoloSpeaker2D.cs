using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 600会話集の独り言250件を、既存のSTEP3頭上吹き出し（タイプライター）で再生。
/// AI移動・ターゲット・取引・通常会話には干渉しない。
/// </summary>
[DisallowMultipleComponent]
public sealed class Dialogue600SoloSpeaker2D : MonoBehaviour
{
    [Header("600会話集：独り言")]
    [SerializeField] private Dialogue600SoloLibrary2D regularLibrary;
    [SerializeField] private Dialogue600SoloLibrary2D hostileLibrary;
    [SerializeField] private bool enableHostileSoliloquies = true;
    [SerializeField, Min(1f)] private float hearingRadius = 11f;
    [SerializeField, Min(1f)] private float firstTalkDelayMin = 12f;
    [SerializeField, Min(1f)] private float firstTalkDelayMax = 24f;
    [SerializeField, Min(1f)] private float cooldownMin = 90f;
    [SerializeField, Min(1f)] private float cooldownMax = 180f;
    [SerializeField, Range(0f, 1f)] private float chancePerAttempt = 0.8f;

    [Header("文字送り・頭上吹き出し")]
    [SerializeField] private TMP_FontAsset japaneseFont;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField, Range(1f, 8f)] private float bubbleWidth = 3.5f;
    [SerializeField, Min(.005f)] private float secondsPerCharacter = .045f;
    [SerializeField, Min(.15f)] private float minimumFullyVisibleSeconds = .85f;
    [SerializeField] private int sortingOrder = 119;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(.08f, .1f, .14f, .9f);
    [SerializeField] private bool debugLogs;

    private static Dialogue600SoloSpeaker2D currentSpeaker;
    private readonly List<Dialogue600SoloEntry2D> choices = new List<Dialogue600SoloEntry2D>();
    private DialogueBattleBarkBubble2D bubble;
    private float nextAllowed;
    private string lastDialogueId;
    private CharacterHealth health;
    private CarryableObject2D carry;
    private CharacterWander2D wander;
    private EnemyChaser2D chaser;
    private FactionGuardCombat2D guard;
    private FactionEnemyTargeting2D targeting;
    private CharacterAIFearFlee2D fear;
    private DialogueAmbientTalk2D ambient;
    private DialogueBattleBarks2D battle;
    private FactionMember2D faction;
    private Transform player;
    private FactionMember2D playerFaction;
    public bool IsSpeaking => bubble != null;

    private void Awake()
    {
        health = GetComponent<CharacterHealth>();
        if (health == null) health = GetComponentInChildren<CharacterHealth>();
        carry = GetComponent<CarryableObject2D>();
        wander = GetComponent<CharacterWander2D>();
        chaser = GetComponent<EnemyChaser2D>();
        guard = GetComponent<FactionGuardCombat2D>();
        targeting = GetComponent<FactionEnemyTargeting2D>();
        fear = GetComponent<CharacterAIFearFlee2D>();
        ambient = GetComponent<DialogueAmbientTalk2D>();
        battle = GetComponent<DialogueBattleBarks2D>();
        faction = GetComponent<FactionMember2D>();
    }

    private void OnEnable()
    {
        nextAllowed = Time.time + Random.Range(firstTalkDelayMin, Mathf.Max(firstTalkDelayMin, firstTalkDelayMax));
    }

    private void OnDisable() { StopTalking(); }
    private void OnDestroy() { StopTalking(); }

    private bool TryFindPlayer()
    {
        if (player != null && player.gameObject.activeInHierarchy) return true;
        GameObject obj = GameObject.FindGameObjectWithTag("Player");
        player = obj != null ? obj.transform : null;
        playerFaction = player != null ? player.GetComponent<FactionMember2D>() : null;
        return player != null;
    }

    private bool HostileToPlayer()
    {
        if (!enableHostileSoliloquies || faction == null || !TryFindPlayer() || playerFaction == null) return false;
        return FactionHostility2D.IsHostile(faction, playerFaction);
    }

    private bool IsBlocked()
    {
        if (!isActiveAndEnabled || (health != null && health.IsDead)) return true;
        if (!TryFindPlayer() || Vector2.Distance(transform.position, player.position) > hearingRadius) return true;
        if (carry != null && (carry.IsReserved || carry.IsCarried)) return true;
        if (wander != null && wander.IsRecoveringFromCarry) return true;
        if (ambient != null && ambient.IsTalking) return true;
        if (battle != null && battle.IsBarkVisible) return true;
        if (DialoguePresenter2D.Active != null && DialoguePresenter2D.Active.IsPlaying) return true;
        if (chaser != null && chaser.isActiveAndEnabled && chaser.HasDetectedPlayer) return true;
        if (guard != null && guard.isActiveAndEnabled && guard.CurrentTarget != null) return true;
        if (targeting != null && targeting.isActiveAndEnabled && targeting.CurrentTarget != null) return true;
        if (fear != null && fear.isActiveAndEnabled && fear.IsReacting) return true;
        if (wander != null && (wander.CurrentState == CharacterWanderState2D.Combat ||
            wander.CurrentState == CharacterWanderState2D.Carried ||
            wander.CurrentState == CharacterWanderState2D.Dead ||
            wander.CurrentState == CharacterWanderState2D.Fleeing ||
            wander.CurrentState == CharacterWanderState2D.Cowering)) return true;
        return false;
    }

    private void Update()
    {
        if (bubble != null)
        {
            if (IsBlocked()) StopTalking();
            return;
        }
        if (currentSpeaker == this) currentSpeaker = null;
        if (Time.time < nextAllowed || currentSpeaker != null || IsBlocked()) return;
        nextAllowed = Time.time + Random.Range(cooldownMin, Mathf.Max(cooldownMin, cooldownMax));
        if (Random.value > chancePerAttempt) return;
        Dialogue600SoloEntry2D chosen = ChooseLine();
        if (chosen == null) return;
        Speak(chosen);
    }

    private Dialogue600SoloEntry2D ChooseLine()
    {
        choices.Clear();
        AddChoices(regularLibrary);
        if (HostileToPlayer()) AddChoices(hostileLibrary);
        if (choices.Count == 0) return null;
        // 直前と同じIDを避けられるなら避ける。
        int total = 0;
        foreach (Dialogue600SoloEntry2D e in choices)
            if (e.sourceDialogueId != lastDialogueId || choices.Count == 1) total += Mathf.Max(1, e.weight);
        if (total <= 0) return null;
        int pick = Random.Range(0, total);
        foreach (Dialogue600SoloEntry2D e in choices)
        {
            if (e.sourceDialogueId == lastDialogueId && choices.Count > 1) continue;
            pick -= Mathf.Max(1, e.weight);
            if (pick < 0) return e;
        }
        return choices[choices.Count - 1];
    }

    private void AddChoices(Dialogue600SoloLibrary2D data)
    {
        if (data == null || data.Entries == null) return;
        foreach (Dialogue600SoloEntry2D e in data.Entries)
            if (e != null && !string.IsNullOrWhiteSpace(e.text) && Dialogue600Stage2D.Matches(e.requiredStage))
                choices.Add(e);
    }

    private void Speak(Dialogue600SoloEntry2D entry)
    {
        if (IsBlocked() || currentSpeaker != null || entry == null) return;
        TMP_FontAsset font = japaneseFont != null ? japaneseFont :
            (ambient != null ? ambient.JapaneseFont : null);
        bubble = DialogueBattleBarkBubble2D.Show(transform, entry.text, font,
            worldOffset, 2.4f, sortingOrder, bubbleWidth, textColor, backgroundColor,
            secondsPerCharacter, minimumFullyVisibleSeconds);
        if (bubble == null) return;
        lastDialogueId = entry.sourceDialogueId;
        currentSpeaker = this;
        if (debugLogs) Debug.Log("[Dialogue600 独り言] " + name + " / " + entry.sourceDialogueId, this);
    }

    private void StopTalking()
    {
        if (bubble != null) Destroy(bubble.gameObject);
        bubble = null;
        if (currentSpeaker == this) currentSpeaker = null;
    }

    [ContextMenu("600会話集 / 独り言をすぐに試す（Play中）")]
    private void TestSolo()
    {
        if (!Application.isPlaying) return;
        if (currentSpeaker != null && currentSpeaker != this) return;
        Dialogue600SoloEntry2D entry = ChooseLine();
        if (entry != null) Speak(entry);
        else Debug.LogWarning("[Dialogue600] 使用できる独り言がないか、戦闘・距離条件で停止しています。", this);
    }

    private void OnValidate()
    {
        firstTalkDelayMax = Mathf.Max(firstTalkDelayMin, firstTalkDelayMax);
        cooldownMax = Mathf.Max(cooldownMin, cooldownMax);
        hearingRadius = Mathf.Max(1f, hearingRadius);
    }
}
