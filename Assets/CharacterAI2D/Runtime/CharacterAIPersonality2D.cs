using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// STEP4: 個体ごとの戦闘性格・集団の士気。
/// ここから直接Rigidbody2D、攻撃対象、派閥ルールを変更しません。
/// 既存Chaser / Guard / RangedAttack / FearFleeが倍率・閾値だけを参照します。
/// </summary>
public enum CharacterAIPersonalityType2D
{
    // InspectorName は表示名だけを変更します。識別子・番号順は変えないでください。
    [InspectorName("標準")]
    Balanced,
    [InspectorName("攻撃的")]
    Aggressive,
    [InspectorName("勇敢")]
    Brave,
    [InspectorName("慎重")]
    Cautious,
    [InspectorName("臆病")]
    Cowardly,
    [InspectorName("仲間思い")]
    Guardian
}

[DisallowMultipleComponent]
public sealed class CharacterAIPersonality2D : MonoBehaviour
{
    [Header("性格：Character Studioの「プリセット適用」で初期値をセット")]
    [SerializeField] private CharacterAIPersonalityType2D personalityType =
        CharacterAIPersonalityType2D.Balanced;

    [Header("戦闘中だけ適用する倍率（1 = 変更なし）")]
    [Tooltip("戦闘中の移動速度倍率。徘徊・音の調査速度は変更しません。")]
    [SerializeField, Range(0.5f, 1.6f)] private float combatMoveMultiplier = 1f;
    [Tooltip("攻撃間隔の倍率。0.8なら攻撃が速くなり、1.2なら遅くなります。")]
    [SerializeField, Range(0.5f, 1.6f)] private float attackIntervalMultiplier = 1f;

    [Header("HP低下逃走への士気補正")]
    [Tooltip("STEP3の Flee When Low Health を有効にした個体に作用します。")]
    [SerializeField] private bool enableGroupMorale = true;
    [Tooltip("負なら粘り強く、正なら早めに逃げます。基本閾値に加算します。")]
    [SerializeField, Range(-0.25f, 0.25f)] private float baseFleeRatioAdjustment;
    [Tooltip("敵が味方より多い時、逃走HPラインをどこまで増加させるか。")]
    [SerializeField, Range(0f, 0.30f)] private float outnumberedFleeBonus = 0.10f;
    [Tooltip("味方が敵より多い時、逃走HPラインをどこまで減少させるか。")]
    [SerializeField, Range(0f, 0.30f)] private float allyConfidenceBonus = 0.06f;
    [SerializeField, Min(0.5f)] private float moraleScanRadius = 8f;
    [SerializeField] private LayerMask moraleTargetLayers = ~0;
    [SerializeField, Min(0.1f)] private float moraleScanInterval = 0.6f;

    [Header("デバッグ")]
    [SerializeField] private bool drawMoraleRange;
    [SerializeField] private bool logMoraleChanges;

    public CharacterAIPersonalityType2D PersonalityType => personalityType;
    public float CombatMoveMultiplier => isActiveAndEnabled
        ? Mathf.Clamp(combatMoveMultiplier, 0.5f, 1.6f) : 1f;
    public float AttackIntervalMultiplier => isActiveAndEnabled
        ? Mathf.Clamp(attackIntervalMultiplier, 0.5f, 1.6f) : 1f;
    public int NearbyAllies => nearbyAllies;
    public int NearbyHostiles => nearbyHostiles;

    private FactionMember2D selfFaction;
    private CharacterHealth ownHealth;
    private CarryableObject2D carry;
    private CharacterWander2D wander;
    private readonly HashSet<int> seenMembers = new HashSet<int>();
    private float nextScanTime;
    private int nearbyAllies;
    private int nearbyHostiles;

    private void Awake() { Cache(); }
    private void OnEnable() { Cache(); nextScanTime = 0f; }
    private void OnDisable()
    {
        nearbyAllies = 0;
        nearbyHostiles = 0;
        seenMembers.Clear();
    }

    private void Cache()
    {
        if (selfFaction == null) selfFaction = GetComponent<FactionMember2D>();
        if (ownHealth == null) ownHealth = GetComponent<CharacterHealth>();
        if (ownHealth == null) ownHealth = GetComponentInChildren<CharacterHealth>();
        if (carry == null) carry = GetComponent<CarryableObject2D>();
        if (wander == null) wander = GetComponent<CharacterWander2D>();
    }

    /// <summary>
    /// STEP3の基本的な逃走HP割合へ、性格と周囲の勢力差を加えます。
    /// 元のFear Response（Flee/Cower）や逃走ON/OFFは変更しません。
    /// </summary>
    public float AdjustFleeHealthRatio(float originalRatio)
    {
        if (!isActiveAndEnabled) return originalRatio;
        Cache();
        float result = originalRatio + baseFleeRatioAdjustment;
        if (enableGroupMorale && selfFaction != null &&
            (ownHealth == null || !ownHealth.IsDead) &&
            (carry == null || !carry.IsReserved) &&
            (wander == null || !wander.IsRecoveringFromCarry))
        {
            RefreshMorale();
            int total = Mathf.Max(1, nearbyHostiles + nearbyAllies);
            float advantage = (float)(nearbyHostiles - nearbyAllies) / total;
            if (advantage > 0f) result += outnumberedFleeBonus * advantage;
            else if (advantage < 0f) result += allyConfidenceBonus * advantage;
        }
        return Mathf.Clamp(result, 0.01f, 0.95f);
    }

    private void RefreshMorale()
    {
        if (Time.time < nextScanTime) return;
        nextScanTime = Time.time + Mathf.Max(.1f, moraleScanInterval);
        seenMembers.Clear();
        int allies = 0;
        int enemies = 0;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(transform.position,
            Mathf.Max(0.5f, moraleScanRadius), moraleTargetLayers.value);
        foreach (Collider2D collider in colliders)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy)
                continue;
            FactionMember2D member = collider.GetComponentInParent<FactionMember2D>();
            if (member == null || member == selfFaction ||
                !seenMembers.Add(member.GetInstanceID())) continue;
            CharacterHealth hp = member.GetComponent<CharacterHealth>();
            if (hp == null) hp = member.GetComponentInChildren<CharacterHealth>();
            if (hp == null || !hp.isActiveAndEnabled || hp.IsDead) continue;
            if (FactionId2D.Same(selfFaction.FactionId, member.FactionId))
                allies++;
            else if (FactionHostility2D.IsHostile(selfFaction, member) ||
                     FactionHostility2D.IsHostile(member, selfFaction))
                enemies++;
        }
        if (logMoraleChanges && (nearbyAllies != allies || nearbyHostiles != enemies))
            Debug.Log($"[CharacterAI STEP4] {name}: 味方{allies}体 / 脅威{enemies}体", this);
        nearbyAllies = allies;
        nearbyHostiles = enemies;
    }

    private void OnValidate()
    {
        combatMoveMultiplier = Mathf.Clamp(combatMoveMultiplier, .5f, 1.6f);
        attackIntervalMultiplier = Mathf.Clamp(attackIntervalMultiplier, .5f, 1.6f);
        moraleScanRadius = Mathf.Max(.5f, moraleScanRadius);
        moraleScanInterval = Mathf.Max(.1f, moraleScanInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawMoraleRange) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, moraleScanRadius);
    }
}
