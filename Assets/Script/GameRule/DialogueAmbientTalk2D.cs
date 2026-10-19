using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// STEP5: 近くの非敵対NPC同士が交互に会話。戦闘/被弾/恐怖/調査/運搬で即中断。
/// CharacterWander2Dの通常移動だけを一時停止し、派閥戦闘・移動AIは変更しない。
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueAmbientTalk2D : MonoBehaviour
{
    [Header("STEP5 NPC同士の会話")]
    [SerializeField] private DialogueAmbientTalkData2D talkData;
    [Tooltip("既存のSTEP5会話に加え、600会話集のパターンを抽選に追加（以前の設定はそのまま）。")]
    [SerializeField] private DialogueAmbientTalkData2D additionalDialogue600TalkData;
    [SerializeField] private DialogueActor2D dialogueActor;
    [SerializeField] private bool allowDifferentNonHostileFactions;

    [Header("開始条件")]
    [SerializeField, Min(.4f)] private float talkRadius = 2.8f;
    [SerializeField, Min(0f)] private float maxHeightDifference = 1.7f;
    [SerializeField, Range(0f, 1f)] private float startProbability = .42f;
    [SerializeField, Min(.15f)] private float scanInterval = 3.5f;
    [SerializeField, Min(1f)] private float minimumRestSeconds = 15f;
    [SerializeField, Min(1f)] private float maximumRestSeconds = 28f;
    [SerializeField, Min(.3f)] private float maxPartnerDrift = 3.7f;

    [Header("600会話集: 聴取範囲")]
    [Tooltip("会話集から読み込んだ立ち話はPlayerの聴取範囲内で開始する。従来の会話データには影響しません。")]
    [SerializeField] private bool limitImportedTalkToPlayerHearingRange = true;
    [SerializeField, Min(1f)] private float importedTalkHearingRadius = 12f;

    [Header("文字送り（NPC・Enemy同士の立ち話）")]
    [Tooltip("立ち話の吹き出しを1文字ずつ表示します。")]
    [SerializeField] private bool enableTypewriter = true;
    [Tooltip("1文字を表示する間隔（秒）。0.04～0.07がおすすめ。")]
    [SerializeField, Min(0.005f)] private float secondsPerCharacter = 0.045f;
    [Tooltip("最後の文字を表示したあと、最低限全文を残す時間（秒）。")]
    [SerializeField, Min(0.15f)] private float minimumFullyVisibleSeconds = 0.7f;

    [Header("吹き出し：STEP3と同じ表示方式")]
    [SerializeField] private TMP_FontAsset japaneseFont;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, 0f);
    [SerializeField, Min(1f)] private float widthInWorldUnits = 3.5f;
    [SerializeField] private int sortingOrder = 120;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(.08f, .1f, .14f, .9f);
    [SerializeField] private bool debugLogs;

    private static readonly List<DialogueAmbientTalk2D> Agents = new List<DialogueAmbientTalk2D>();
    private readonly List<DialogueAmbientExchange2D> candidates = new List<DialogueAmbientExchange2D>();
    private DialogueAmbientTalk2D partner;
    private DialogueAmbientTalk2D owner;
    private DialogueAmbientExchange2D currentExchange;
    private DialogueBattleBarkBubble2D activeBubble;
    private float nextScan;
    private float nextTalkAllowed;
    private float nextLineTime;
    private int lineIndex;
    private bool waitingForLineFinish;
    private bool reverseDialogue600Roles;
    private Dialogue600SoloSpeaker2D soloSpeaker;
    private Transform playerForDialogue600;
    private CharacterHealth health;
    private CarryableObject2D carry;
    private CharacterWander2D wander;
    private EnemyChaser2D chaser;
    private FactionEnemyTargeting2D targeting;
    private FactionGuardCombat2D guard;
    private CharacterAIInvestigation2D investigation;
    private CharacterAIFearFlee2D fear;
    private CharacterAIPersonality2D personality;
    private FactionMember2D faction;

    public bool IsTalking => partner != null;
    public DialogueAmbientTalkData2D TalkData => talkData;
    public TMP_FontAsset JapaneseFont => japaneseFont;

    private void Awake() { CacheReferences(); }
    private void OnEnable()
    {
        CacheReferences();
        if (!Agents.Contains(this)) Agents.Add(this);
        // 同時発火を防ぐためスキャン時刻をばらす
        nextScan = Time.time + UnityEngine.Random.Range(.5f, 3f);
        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
    }

    private void OnDisable()
    {
        Agents.Remove(this);
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
        CancelConversation("無効化");
    }

    private void OnDestroy() { CancelConversation("破棄"); }

    private void CacheReferences()
    {
        if (soloSpeaker == null) soloSpeaker = GetComponent<Dialogue600SoloSpeaker2D>();
        if (health == null) health = GetComponent<CharacterHealth>();
        if (health == null) health = GetComponentInChildren<CharacterHealth>();
        if (carry == null) carry = GetComponent<CarryableObject2D>();
        if (wander == null) wander = GetComponent<CharacterWander2D>();
        if (chaser == null) chaser = GetComponent<EnemyChaser2D>();
        if (targeting == null) targeting = GetComponent<FactionEnemyTargeting2D>();
        if (guard == null) guard = GetComponent<FactionGuardCombat2D>();
        if (investigation == null) investigation = GetComponent<CharacterAIInvestigation2D>();
        if (fear == null) fear = GetComponent<CharacterAIFearFlee2D>();
        if (personality == null) personality = GetComponent<CharacterAIPersonality2D>();
        if (faction == null) faction = GetComponent<FactionMember2D>();
        if (dialogueActor == null) dialogueActor = GetComponent<DialogueActor2D>();
    }

    private bool IsInterrupted()
    {
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy) return true;
        if (soloSpeaker != null && soloSpeaker.IsSpeaking) return true;
        if (health != null && health.IsDead) return true;
        if (carry != null && (carry.IsReserved || carry.IsCarried)) return true;
        if (wander != null && wander.IsRecoveringFromCarry) return true;
        // Chaser単独で移動している個体は立ち話中の移動停止を保証できないため除外。
        if (chaser != null && chaser.isActiveAndEnabled && (wander == null || !wander.isActiveAndEnabled)) return true;
        if (chaser != null && chaser.isActiveAndEnabled && chaser.HasDetectedPlayer) return true;
        if (guard != null && guard.isActiveAndEnabled && guard.CurrentTarget != null) return true;
        if (targeting != null && targeting.isActiveAndEnabled && targeting.CurrentTarget != null) return true;
        if (fear != null && fear.isActiveAndEnabled && fear.IsReacting) return true;
        if (investigation != null && investigation.isActiveAndEnabled && investigation.IsInvestigating) return true;
        if (wander != null && (wander.CurrentState == CharacterWanderState2D.Combat ||
            wander.CurrentState == CharacterWanderState2D.Carried ||
            wander.CurrentState == CharacterWanderState2D.Dead ||
            wander.CurrentState == CharacterWanderState2D.Fleeing ||
            wander.CurrentState == CharacterWanderState2D.Cowering)) return true;
        if (DialoguePresenter2D.Active != null && DialoguePresenter2D.Active.IsPlaying) return true;
        return false;
    }

    private void Update()
    {
        // 相手がシーン切替などで破棄された場合、残りの参照と吹き出しを清掃。
        if (partner == null && owner != null) CancelConversation("相手を失った");
        if (partner != null)
        {
            if (partner == null || partner.partner != this || IsInterrupted() || partner.IsInterrupted() ||
                Vector2.Distance(transform.position, partner.transform.position) > maxPartnerDrift ||
                !CanTalkTo(partner))
            {
                CancelConversation("戦闘・距離・状態変化");
                return;
            }
            // 会話進行は開始側の1個体のみで処理（二重セリフ防止）
            if (owner == this) UpdateConversation();
            return;
        }

        if ((talkData == null && additionalDialogue600TalkData == null) ||
            Time.time < nextScan || Time.time < nextTalkAllowed) return;
        nextScan = Time.time + Mathf.Max(.15f, scanInterval) + UnityEngine.Random.Range(0f, .75f);
        if (IsInterrupted() || UnityEngine.Random.value > startProbability) return;
        DialogueAmbientTalk2D nearby = FindPartner();
        if (nearby != null) TryBegin(nearby);
    }

    private DialogueAmbientTalk2D FindPartner()
    {
        DialogueAmbientTalk2D best = null;
        float bestDistance = talkRadius * talkRadius;
        for (int i = 0; i < Agents.Count; i++)
        {
            DialogueAmbientTalk2D other = Agents[i];
            if (!CanTalkTo(other) || other.IsInterrupted() || other.partner != null ||
                Time.time < other.nextTalkAllowed) continue;
            Vector3 delta = other.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > maxHeightDifference) continue;
            float distance = delta.x * delta.x + delta.y * delta.y;
            if (distance < bestDistance && distance <= other.talkRadius * other.talkRadius)
            {
                bestDistance = distance;
                best = other;
            }
        }
        return best;
    }

    private bool CanTalkTo(DialogueAmbientTalk2D other)
    {
        if (other == null || other == this || !other.isActiveAndEnabled ||
            other.gameObject == gameObject || faction == null || other.faction == null ||
            faction.IsPlayer || other.faction.IsPlayer) return false;
        // パック生成のスライムとスケルトンは、商人でも相手の種族を限定する。
        CharacterPackRaceIdentity2D selfRace = GetComponent<CharacterPackRaceIdentity2D>();
        CharacterPackRaceIdentity2D otherRace = other.GetComponent<CharacterPackRaceIdentity2D>();
        if ((selfRace != null && selfRace.IsMonster) || (otherRace != null && otherRace.IsMonster))
        {
            if (selfRace == null || otherRace == null || selfRace.RaceCode != otherRace.RaceCode)
                return false;
        }
        if (FactionHostility2D.IsHostile(faction, other.faction) ||
            FactionHostility2D.IsHostile(other.faction, faction)) return false;
        if (!FactionId2D.Same(faction.FactionId, other.faction.FactionId) &&
            (!allowDifferentNonHostileFactions || !other.allowDifferentNonHostileFactions)) return false;
        return true;
    }

    private bool TryBegin(DialogueAmbientTalk2D other)
    {
        if (other == null || partner != null || other.partner != null ||
            IsInterrupted() || other.IsInterrupted() || !CanTalkTo(other)) return false;
        DialogueAmbientExchange2D selected = ChooseExchange(other, out bool reverseRoles);
        if (selected == null) return false;
        partner = other;
        owner = this;
        currentExchange = selected;
        reverseDialogue600Roles = reverseRoles;
        lineIndex = 0;
        waitingForLineFinish = false;
        nextLineTime = Time.time + .15f;
        other.partner = this;
        other.owner = this;
        if (debugLogs) Debug.Log($"[Dialogue STEP5] {name} ↔ {other.name}: {selected.title}", this);
        return true;
    }

    private DialogueAmbientExchange2D ChooseExchange(DialogueAmbientTalk2D other, out bool selectedReversed)
    {
        selectedReversed = false;
        candidates.Clear();
        // リバース判定は最終候補で計算し直す（候補リストは従来のまま）。
        int total = 0;
        IReadOnlyList<DialogueAmbientExchange2D>[] libraries =
        {
            talkData != null ? talkData.Exchanges : null,
            additionalDialogue600TalkData != null ? additionalDialogue600TalkData.Exchanges : null
        };
        foreach (IReadOnlyList<DialogueAmbientExchange2D> all in libraries)
        {
            if (all == null) continue;
            foreach (DialogueAmbientExchange2D e in all)
        {
            if (e == null || e.lines == null || e.lines.Count == 0 ||
                UnityEngine.Random.value > e.probability ||
                !DialogueConditions2D.AllMatch(e.conditions, dialogueActor)) continue;
            if (!Dialogue600Stage2D.Matches(e.requiredStage)) continue;
            if (!string.IsNullOrEmpty(e.sourceDialogueId) && !CanBeHeardByPlayer600(other)) continue;
            if (!Dialogue600PairMatches(e, other, out _)) continue;
            if (e.requireInitiatorPersonality &&
                (personality == null || personality.PersonalityType != e.initiatorPersonality)) continue;
            if (e.requirePartnerPersonality &&
                (other.personality == null || other.personality.PersonalityType != e.partnerPersonality)) continue;
            bool anyText = false;
            foreach (DialogueAmbientLine2D line in e.lines)
                if (line != null && !string.IsNullOrWhiteSpace(line.text)) { anyText = true; break; }
            if (!anyText) continue;
            candidates.Add(e);
            total += Mathf.Max(1, e.weight);
        }
        }
        if (total <= 0) return null;
        int pick = UnityEngine.Random.Range(0, total);
        foreach (DialogueAmbientExchange2D e in candidates)
        {
            pick -= Mathf.Max(1, e.weight);
            if (pick < 0)
            {
                Dialogue600PairMatches(e, other, out selectedReversed);
                return e;
            }
        }
        DialogueAmbientExchange2D last = candidates[candidates.Count - 1];
        Dialogue600PairMatches(last, other, out selectedReversed);
        return last;
    }

    // 600会話集は視聴される距離でのみ抽選（元のSTEP5会話は従来のまま）。
    private bool CanBeHeardByPlayer600(DialogueAmbientTalk2D other)
    {
        if (!limitImportedTalkToPlayerHearingRange) return true;
        if (playerForDialogue600 == null)
        {
            GameObject found = GameObject.FindGameObjectWithTag("Player");
            playerForDialogue600 = found != null ? found.transform : null;
        }
        if (playerForDialogue600 == null || other == null) return false;
        float sqr = importedTalkHearingRadius * importedTalkHearingRadius;
        return ((Vector2)(transform.position - playerForDialogue600.position)).sqrMagnitude <= sqr &&
            ((Vector2)(other.transform.position - playerForDialogue600.position)).sqrMagnitude <= sqr;
    }

    // 固定の役名を持つ異派閥会話は、近づいた側が逆でも役割を正しく表示する。
    private bool Dialogue600PairMatches(DialogueAmbientExchange2D e,
        DialogueAmbientTalk2D other, out bool reverse)
    {
        reverse = false;
        string first = e.requiredInitiatorFactionId;
        string second = e.requiredPartnerFactionId;
        if (string.IsNullOrEmpty(first) && string.IsNullOrEmpty(second)) return true;
        if (faction == null || other == null || other.faction == null) return false;
        string a = faction.FactionId;
        string b = other.faction.FactionId;
        bool forward = (string.IsNullOrEmpty(first) || FactionId2D.Same(first, a)) &&
                       (string.IsNullOrEmpty(second) || FactionId2D.Same(second, b));
        if (forward) return true;
        bool backward = (string.IsNullOrEmpty(first) || FactionId2D.Same(first, b)) &&
                        (string.IsNullOrEmpty(second) || FactionId2D.Same(second, a));
        reverse = backward;
        return backward;
    }

    private void UpdateConversation()
    {
        if (currentExchange == null || currentExchange.lines == null)
        {
            CancelConversation("会話データなし");
            return;
        }
        if (Time.time < nextLineTime) return;
        if (waitingForLineFinish)
        {
            waitingForLineFinish = false;
            lineIndex++;
        }
        if (lineIndex >= currentExchange.lines.Count)
        {
            CancelConversation("終了");
            return;
        }
        DialogueAmbientLine2D line = currentExchange.lines[lineIndex];
        if (line == null || string.IsNullOrWhiteSpace(line.text))
        {
            lineIndex++;
            nextLineTime = Time.time;
            return;
        }
        bool startSide = line.speaker == DialogueAmbientSpeaker2D.Initiator;
        if (reverseDialogue600Roles) startSide = !startSide;
        DialogueAmbientTalk2D speaker = startSide ? this : partner;
        if (speaker == null) { CancelConversation("相手がいない"); return; }
        // 一つ前の吹き出しをすぐ消してから、次のキャラクターが話す
        DestroyBubble();
        partner.DestroyBubble();
        speaker.activeBubble = DialogueBattleBarkBubble2D.Show(speaker.transform, line.text,
            speaker.japaneseFont != null ? speaker.japaneseFont : japaneseFont,
            speaker.worldOffset, Mathf.Max(.25f, line.displaySeconds),
            speaker.sortingOrder, speaker.widthInWorldUnits, speaker.textColor, speaker.backgroundColor,
            speaker.enableTypewriter ? speaker.secondsPerCharacter : 0f,
            speaker.minimumFullyVisibleSeconds);
        // 文字送りの途中で次のセリフへ進まないよう、実際の吹き出し寿命で同期する。
        float visibleDuration = speaker.activeBubble != null
            ? speaker.activeBubble.TotalDuration : Mathf.Max(.25f, line.displaySeconds);
        nextLineTime = Time.time + visibleDuration + Mathf.Max(0f, line.pauseAfterSeconds);
        waitingForLineFinish = true;
    }

    private void OnDamaged(int damage, int groupId, Vector3 position)
    {
        if (damage > 0) CancelConversation("被弾");
    }
    private void OnDied() { CancelConversation("死亡"); }

    private void DestroyBubble()
    {
        if (activeBubble != null) Destroy(activeBubble.gameObject);
        activeBubble = null;
    }

    private void CancelConversation(string reason)
    {
        if (partner == null)
        {
            DestroyBubble();
            owner = null;
            currentExchange = null;
            return;
        }
        DialogueAmbientTalk2D other = partner;
        partner = null;
        owner = null;
        currentExchange = null;
        DestroyBubble();
        nextTalkAllowed = Time.time + UnityEngine.Random.Range(
            Mathf.Max(1f, minimumRestSeconds), Mathf.Max(minimumRestSeconds, maximumRestSeconds));
        if (other != null)
        {
            other.partner = null;
            other.owner = null;
            other.currentExchange = null;
            other.DestroyBubble();
            other.nextTalkAllowed = Time.time + UnityEngine.Random.Range(
                Mathf.Max(1f, other.minimumRestSeconds), Mathf.Max(other.minimumRestSeconds, other.maximumRestSeconds));
        }
        if (debugLogs) Debug.Log("[Dialogue STEP5] 会話終了: " + reason, this);
    }

    [ContextMenu("STEP5 / 隣の相手と会話テスト（Play中）")]
    private void DebugStartConversation()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Play中のみ実行できます。", this); return; }
        CacheReferences();
        DialogueAmbientTalk2D other = FindPartner();
        if (other == null || !TryBegin(other)) Debug.LogWarning("[Dialogue STEP5] 相手/条件/会話データを確認してください。", this);
    }

    private void OnValidate()
    {
        talkRadius = Mathf.Max(.4f, talkRadius);
        scanInterval = Mathf.Max(.15f, scanInterval);
        maximumRestSeconds = Mathf.Max(minimumRestSeconds, maximumRestSeconds);
        maxPartnerDrift = Mathf.Max(.3f, maxPartnerDrift);
        secondsPerCharacter = Mathf.Max(0.005f, secondsPerCharacter);
        minimumFullyVisibleSeconds = Mathf.Max(0.15f, minimumFullyVisibleSeconds);
        importedTalkHearingRadius = Mathf.Max(1f, importedTalkHearingRadius);
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(.2f, .85f, .9f, .65f);
        Gizmos.DrawWireSphere(transform.position, talkRadius);
    }
}
