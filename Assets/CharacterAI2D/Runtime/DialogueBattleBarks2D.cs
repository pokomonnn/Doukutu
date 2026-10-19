using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// STEP3: CharacterAI STEP1～4の読み取り専用イベント/状態に連動して、
/// 通常会話を止めずに短い頭上セリフを表示します。移動や攻撃は操作しません。
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueBattleBarks2D : MonoBehaviour
{
    [Header("戦闘セリフデータ")]
    [SerializeField] private DialogueBattleBarkData2D barkData;
    [SerializeField] private DialogueActor2D dialogueActor;

    [Header("表示頻度")]
    [SerializeField, Min(0f)] private float globalCooldown = 1.5f;
    [SerializeField, Min(0f)] private float sameTriggerCooldown = 6f;
    [SerializeField, Min(.1f)] private float displaySeconds = 2.4f;
    [SerializeField, Range(.01f, 1f)] private float lowHealthThreshold = .3f;
    [SerializeField] private bool muteWhileNormalDialogue = true;
    [SerializeField] private bool muteWhileCarried = true;
    [SerializeField] private bool replacePreviousBubble = true;

    [Header("文字送り（独り言・戦闘セリフ）")]
    [Tooltip("頭上セリフを1文字ずつ表示します。")]
    [SerializeField] private bool enableTypewriter = true;
    [Tooltip("1文字を表示する間隔（秒）。0.04～0.07がおすすめ。")]
    [SerializeField, Min(0.005f)] private float secondsPerCharacter = 0.045f;
    [Tooltip("最後の文字を表示したあと、最低限全文を残す時間（秒）。")]
    [SerializeField, Min(0.15f)] private float minimumFullyVisibleSeconds = 0.7f;

    [Header("吹き出し表示")]
    [SerializeField] private TMP_FontAsset japaneseFont;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2.0f, 0f);
    [SerializeField, Range(1f, 8f)] private float widthInWorldUnits = 3.5f;
    [SerializeField] private int sortingOrder = 120;
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color backgroundColor = new Color(.08f, .1f, .14f, .90f);

    [Header("診断")]
    [SerializeField] private bool debugLogs;

    private CharacterHealth health;
    private CarryableObject2D carry;
    private CharacterWander2D wander;
    private EnemyChaser2D chaser;
    private FactionGuardCombat2D guard;
    private EnemyRangedAttack2D ranged;
    private CharacterAIFearFlee2D fear;
    private Transform previousTarget;
    private CharacterAIFearState2D previousFear;
    private bool previousLowHealth;
    private bool initialized;
    private float nextGlobalTime;
    private readonly float[] nextTriggerTime = new float[9];
    private readonly List<DialogueBattleBarkLine2D> candidates = new List<DialogueBattleBarkLine2D>();
    private DialogueBattleBarkBubble2D currentBubble;

    /// <summary>STEP600独り言との吹き出し重複を避けるための参照専用プロパティ。</summary>
    public bool IsBarkVisible => currentBubble != null;

    private void Awake() { CacheReferences(); }

    private void OnEnable()
    {
        CacheReferences();
        initialized = false;
        previousTarget = null;
        previousFear = fear != null ? fear.CurrentState : CharacterAIFearState2D.Calm;
        previousLowHealth = IsLowHealth();
        nextGlobalTime = 0f;
        for (int i = 0; i < nextTriggerTime.Length; i++) nextTriggerTime[i] = 0f;

        if (health != null)
        {
            health.Damaged += OnDamaged;
            health.Died += OnDied;
        }
        if (chaser != null) chaser.AttackPerformed += OnAttack;
        if (guard != null) guard.AttackPerformed += OnAttack;
        if (ranged != null) ranged.RangedAttackPerformed += OnAttack;
        CharacterAIAllyAlert2D.Emitted += OnAllyAlert;
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Damaged -= OnDamaged;
            health.Died -= OnDied;
        }
        if (chaser != null) chaser.AttackPerformed -= OnAttack;
        if (guard != null) guard.AttackPerformed -= OnAttack;
        if (ranged != null) ranged.RangedAttackPerformed -= OnAttack;
        CharacterAIAllyAlert2D.Emitted -= OnAllyAlert;
        initialized = false;
    }

    private void CacheReferences()
    {
        if (health == null) health = GetComponent<CharacterHealth>();
        if (health == null) health = GetComponentInChildren<CharacterHealth>();
        if (carry == null) carry = GetComponent<CarryableObject2D>();
        if (wander == null) wander = GetComponent<CharacterWander2D>();
        if (chaser == null) chaser = GetComponent<EnemyChaser2D>();
        if (guard == null) guard = GetComponent<FactionGuardCombat2D>();
        if (ranged == null) ranged = GetComponent<EnemyRangedAttack2D>();
        if (fear == null) fear = GetComponent<CharacterAIFearFlee2D>();
        if (dialogueActor == null) dialogueActor = GetComponent<DialogueActor2D>();
    }

    private void LateUpdate()
    {
        if (health != null && health.IsDead) return;
        if (IsCarried())
        {
            // 運搬の前後で「発見」を再発火させない。
            previousTarget = GetDetectedTarget();
            previousFear = fear != null ? fear.CurrentState : CharacterAIFearState2D.Calm;
            previousLowHealth = IsLowHealth();
            initialized = true;
            return;
        }

        Transform currentTarget = GetDetectedTarget();
        CharacterAIFearState2D currentFear = fear != null && fear.isActiveAndEnabled
            ? fear.CurrentState : CharacterAIFearState2D.Calm;
        bool lowHealth = IsLowHealth();

        if (!initialized)
        {
            initialized = true;
            if (currentTarget != null) TryBark(DialogueBattleBarkTrigger2D.Spotted);
            previousTarget = currentTarget;
            previousFear = currentFear;
            previousLowHealth = lowHealth;
            return;
        }

        if (currentTarget != previousTarget)
        {
            if (currentTarget != null) TryBark(DialogueBattleBarkTrigger2D.Spotted);
            else if (previousTarget != null) TryBark(DialogueBattleBarkTrigger2D.LostTarget);
        }
        previousTarget = currentTarget;

        if (!previousLowHealth && lowHealth) TryBark(DialogueBattleBarkTrigger2D.LowHealth);
        previousLowHealth = lowHealth;

        if (currentFear != previousFear)
        {
            if (currentFear == CharacterAIFearState2D.Fleeing)
                TryBark(DialogueBattleBarkTrigger2D.Flee);
            else if (currentFear == CharacterAIFearState2D.Cowering)
                TryBark(DialogueBattleBarkTrigger2D.Cower);
        }
        previousFear = currentFear;
    }

    private Transform GetDetectedTarget()
    {
        if (chaser != null && chaser.isActiveAndEnabled && chaser.HasDetectedPlayer)
            return chaser.PlayerTransform;
        if (guard != null && guard.isActiveAndEnabled) return guard.CurrentTarget;
        return null;
    }

    private bool IsLowHealth() => health != null && health.MaxHealth > 0 &&
        (float)health.CurrentHealth / health.MaxHealth <= lowHealthThreshold;

    private bool IsCarried() => muteWhileCarried &&
        ((carry != null && carry.IsReserved) ||
         (wander != null && wander.IsRecoveringFromCarry));

    private void OnAttack() => TryBark(DialogueBattleBarkTrigger2D.Attack);
    private void OnDamaged(int amount, int groupId, Vector3 hitPoint)
    {
        if (amount > 0) TryBark(DialogueBattleBarkTrigger2D.Damaged);
    }
    private void OnDied() => TryBark(DialogueBattleBarkTrigger2D.Death, true);

    private void OnAllyAlert(CharacterAIAllyAlert2D.Alert alert)
    {
        if (alert.Sender == transform) TryBark(DialogueBattleBarkTrigger2D.AllyAlert);
    }

    /// <summary>任意のイベントから呼び出せる。Playモードでのテストにも使用。</summary>
    public bool TryBark(DialogueBattleBarkTrigger2D trigger, bool forceImportant = false)
    {
        if (barkData == null || !isActiveAndEnabled) return false;
        bool death = trigger == DialogueBattleBarkTrigger2D.Death;
        if (!death && (IsCarried() || (health != null && health.IsDead))) return false;
        if (muteWhileNormalDialogue && DialoguePresenter2D.Active != null &&
            DialoguePresenter2D.Active.IsPlaying) return false;

        int index = (int)trigger;
        if (index < 0 || index >= nextTriggerTime.Length) return false;
        if (!forceImportant &&
            (Time.time < nextGlobalTime || Time.time < nextTriggerTime[index])) return false;

        candidates.Clear();
        int totalWeight = 0;
        IReadOnlyList<DialogueBattleBarkLine2D> lines = barkData.Lines;
        if (lines == null) return false;
        foreach (DialogueBattleBarkLine2D line in lines)
        {
            if (line == null || line.trigger != trigger || string.IsNullOrWhiteSpace(line.text) ||
                Random.value > line.probability ||
                !DialogueConditions2D.AllMatch(line.conditions, dialogueActor)) continue;
            candidates.Add(line);
            totalWeight += Mathf.Max(1, line.weight);
        }
        if (totalWeight == 0) return false;
        int chosen = Random.Range(0, totalWeight);
        DialogueBattleBarkLine2D selected = null;
        foreach (DialogueBattleBarkLine2D item in candidates)
        {
            chosen -= Mathf.Max(1, item.weight);
            if (chosen < 0) { selected = item; break; }
        }
        if (selected == null) return false;
        nextGlobalTime = Time.time + Mathf.Max(0f, globalCooldown);
        nextTriggerTime[index] = Time.time + Mathf.Max(0f, sameTriggerCooldown);
        if (replacePreviousBubble && currentBubble != null) Destroy(currentBubble.gameObject);
        currentBubble = DialogueBattleBarkBubble2D.Show(transform, selected.text, japaneseFont,
            worldOffset, displaySeconds, sortingOrder, widthInWorldUnits, textColor, backgroundColor,
            enableTypewriter ? secondsPerCharacter : 0f, minimumFullyVisibleSeconds);
        if (debugLogs) Debug.Log($"[Dialogue STEP3] {name} / {trigger}: {selected.text}", this);
        return true;
    }

    [ContextMenu("STEP3 / 敵発見セリフをテスト（Play中）")]
    private void TestBark()
    {
        if (!Application.isPlaying) { Debug.LogWarning("Playモードで実行してください。", this); return; }
        TryBark(DialogueBattleBarkTrigger2D.Spotted, true);
    }

    private void OnValidate()
    {
        globalCooldown = Mathf.Max(0f, globalCooldown);
        sameTriggerCooldown = Mathf.Max(0f, sameTriggerCooldown);
        displaySeconds = Mathf.Max(.1f, displaySeconds);
        secondsPerCharacter = Mathf.Max(0.005f, secondsPerCharacter);
        minimumFullyVisibleSeconds = Mathf.Max(0.15f, minimumFullyVisibleSeconds);
        widthInWorldUnits = Mathf.Clamp(widthInWorldUnits, 1f, 8f);
    }
}
