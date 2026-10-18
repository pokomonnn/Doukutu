using UnityEngine;

/// <summary>
/// STEP2: 音の調査・目標を見失った後の捜索・同派閥への警報。
/// Rigidbody2Dには触れず、EnemyChaser2D / CharacterWander2D に移動希望値だけを返す。
/// 攻撃対象の選択と敵対判定は既存のFactionEnemyTargeting2Dに任せる。
/// </summary>
public enum CharacterAIInvestigationState2D { None, GoingToLastKnownPoint, Searching }

[DisallowMultipleComponent]
public sealed class CharacterAIInvestigation2D : MonoBehaviour
{
    [Header("調査・捜索")]
    [SerializeField] private bool hearFootsteps = true;
    [SerializeField] private bool hearGunshots = true;
    [SerializeField] private bool investigateUnknownSources = false;
    [SerializeField] private bool onlyInvestigateHostileSources = true;
    [SerializeField] private bool searchAfterLosingTarget = true;
    [SerializeField, Min(.1f)] private float investigationMoveSpeed = 1.7f;
    [SerializeField, Min(.05f)] private float arrivalDistanceX = .35f;
    [SerializeField, Min(0f)] private float searchRadius = 1.6f;
    [SerializeField, Range(0, 10)] private int searchPointCount = 3;
    [SerializeField, Min(0f)] private float searchPauseSeconds = 1.0f;
    [SerializeField, Min(1f)] private float maxInvestigationSeconds = 15f;

    [Header("仲間への警報")]
    [SerializeField] private bool reportHostileToAllies = true;
    [SerializeField] private bool respondToAllyAlerts = true;
    [SerializeField, Min(0f)] private float allyAlertRadius = 10f;
    [SerializeField, Min(0.1f)] private float alertCooldownSeconds = 5f;

    [Header("デバッグ")]
    [SerializeField] private bool showGizmo = true;
    [SerializeField] private bool debugLogs;

    public CharacterAIInvestigationState2D CurrentState => state;
    public bool IsInvestigating => state != CharacterAIInvestigationState2D.None;
    public Vector2 InvestigationPoint => primaryPoint;

    private CharacterAIInvestigationState2D state;
    private CharacterWander2D wander;
    private EnemyChaser2D chaser;
    private FactionGuardCombat2D guard;
    private FactionMember2D faction;
    private CharacterHealth health;
    private CarryableObject2D carry;
    private CharacterAIFearFlee2D fearAI;
    private Transform lastCombatTarget;
    private CharacterHealth lastCombatTargetHealth;
    private Vector2 lastSeenPosition;
    private Vector2 primaryPoint;
    private Vector2 nextPoint;
    private float deadline;
    private float pauseUntil;
    private float nextAlertTime;
    private int remainingSearchPoints;
    private int blockedCount;

    private void Awake() { CacheReferences(); }

    private void OnEnable()
    {
        CacheReferences();
        CharacterAINoise2D.Emitted += OnNoise;
        CharacterAIAllyAlert2D.Emitted += OnAllyAlert;
    }

    private void OnDisable()
    {
        CharacterAINoise2D.Emitted -= OnNoise;
        CharacterAIAllyAlert2D.Emitted -= OnAllyAlert;
        CancelInvestigation();
        lastCombatTarget = null;
        lastCombatTargetHealth = null;
    }

    private void CacheReferences()
    {
        if (wander == null) wander = GetComponent<CharacterWander2D>();
        if (chaser == null) chaser = GetComponent<EnemyChaser2D>();
        if (guard == null) guard = GetComponent<FactionGuardCombat2D>();
        if (faction == null) faction = GetComponent<FactionMember2D>();
        if (health == null) health = GetComponent<CharacterHealth>();
        if (carry == null) carry = GetComponent<CarryableObject2D>();
        if (fearAI == null) fearAI = GetComponent<CharacterAIFearFlee2D>();
    }

    private bool Unavailable()
    {
        return !isActiveAndEnabled || (health != null && health.IsDead) ||
               (carry != null && carry.IsReserved) ||
               (wander != null && wander.IsRecoveringFromCarry) ||
               (fearAI != null && fearAI.IsReacting);
    }

    private void Update()
    {
        if (Unavailable())
        {
            CancelInvestigation();
            lastCombatTarget = null;
            lastCombatTargetHealth = null;
            return;
        }

        Transform target = null;
        if (chaser != null && chaser.isActiveAndEnabled && chaser.HasDetectedPlayer)
            target = chaser.PlayerTransform;
        else if (guard != null && guard.isActiveAndEnabled)
            target = guard.CurrentTarget;

        if (target != null)
        {
            bool newlySeen = target != lastCombatTarget;
            lastCombatTarget = target;
            lastCombatTargetHealth = target.GetComponent<CharacterHealth>();
            if (lastCombatTargetHealth == null)
                lastCombatTargetHealth = target.GetComponentInChildren<CharacterHealth>();
            lastSeenPosition = target.position;
            CancelInvestigation();
            if (newlySeen) AlertNearbyAllies(target);
        }
        else if (lastCombatTarget != null)
        {
            // 戦闘終了（撃破）時は捜索しない。逃した敵だけ捜す。
            bool alive = lastCombatTargetHealth == null || !lastCombatTargetHealth.IsDead;
            lastCombatTarget = null;
            lastCombatTargetHealth = null;
            if (searchAfterLosingTarget && alive)
                BeginInvestigation(lastSeenPosition, "見失った敵");
        }
        else if (state != CharacterAIInvestigationState2D.None && Time.time >= deadline)
        {
            CancelInvestigation();
        }
    }

    private void OnNoise(CharacterAINoise2D.Noise noise)
    {
        if (Unavailable() || noise.Radius <= 0f ||
            (noise.Kind == CharacterAINoiseKind2D.Footstep && !hearFootsteps) ||
            (noise.Kind == CharacterAINoiseKind2D.Gunshot && !hearGunshots)) return;
        if (((Vector2)transform.position - noise.Position).sqrMagnitude > noise.Radius * noise.Radius) return;
        if (!CanInvestigateSource(noise.Source)) return;
        if (IsInCombat()) return;
        BeginInvestigation(noise.Position, noise.Kind == CharacterAINoiseKind2D.Gunshot ? "銃声" : "足音");
    }

    private bool CanInvestigateSource(Transform source)
    {
        if (source == transform || (source != null && source.IsChildOf(transform))) return false;
        FactionMember2D other = source != null ? source.GetComponentInParent<FactionMember2D>() : null;
        if (other == null) return investigateUnknownSources;
        if (other == faction) return false;
        if (!onlyInvestigateHostileSources) return true;
        return faction != null && FactionHostility2D.IsHostile(faction, other);
    }

    private void OnAllyAlert(CharacterAIAllyAlert2D.Alert alert)
    {
        if (!respondToAllyAlerts || Unavailable() || faction == null ||
            alert.Sender == transform || alert.SenderFaction == null ||
            !FactionId2D.Same(faction.FactionId, alert.SenderFaction.FactionId)) return;
        if (((Vector2)transform.position - alert.SenderPosition).sqrMagnitude >
            alert.Radius * alert.Radius) return;
        // 派閥内であっても相手への敵対判定は個別に確認。友好的な種族へは向かわない。
        if (alert.TargetFaction == null || !FactionHostility2D.IsHostile(faction, alert.TargetFaction)) return;
        if (IsInCombat()) return;
        BeginInvestigation(alert.LastKnownPosition, "仲間の警報");
    }

    private bool IsInCombat()
    {
        return (chaser != null && chaser.isActiveAndEnabled && chaser.HasDetectedPlayer) ||
               (guard != null && guard.isActiveAndEnabled && guard.CurrentTarget != null);
    }

    private void AlertNearbyAllies(Transform target)
    {
        if (!reportHostileToAllies || Time.time < nextAlertTime ||
            allyAlertRadius <= 0f || faction == null) return;
        FactionMember2D other = target.GetComponent<FactionMember2D>();
        if (other == null) other = target.GetComponentInParent<FactionMember2D>();
        if (other == null || !FactionHostility2D.IsHostile(faction, other)) return;
        nextAlertTime = Time.time + alertCooldownSeconds;
        CharacterAIAllyAlert2D.Emit(new CharacterAIAllyAlert2D.Alert(
            transform, faction, other, transform.position, target.position, allyAlertRadius));
        Log("仲間に敵発見を通知");
    }

    private void BeginInvestigation(Vector2 worldPoint, string cause)
    {
        CacheReferences();
        if (Unavailable() || IsInCombat()) return;
        primaryPoint = worldPoint;
        nextPoint = worldPoint;
        deadline = Time.time + maxInvestigationSeconds;
        remainingSearchPoints = searchPointCount;
        blockedCount = 0;
        pauseUntil = 0f;
        state = CharacterAIInvestigationState2D.GoingToLastKnownPoint;
        Log(cause + " → " + worldPoint);
    }

    /// <summary>
    /// 移動担当のEnemyChaser2D/CharacterWander2Dが通常行動中に呼ぶ。
    /// trueは調査が移動を専有することを示す（停止速度0でもtrue）。
    /// </summary>
    public bool TryGetDesiredSpeed(out float horizontalSpeed)
    {
        horizontalSpeed = 0f;
        if (Unavailable() || state == CharacterAIInvestigationState2D.None || IsInCombat()) return false;
        if (Time.time >= deadline) { CancelInvestigation(); return false; }
        if (Time.time < pauseUntil) return true;

        float dx = nextPoint.x - transform.position.x;
        if (Mathf.Abs(dx) <= arrivalDistanceX)
        {
            if (state == CharacterAIInvestigationState2D.GoingToLastKnownPoint)
            {
                state = CharacterAIInvestigationState2D.Searching;
                pauseUntil = Time.time + searchPauseSeconds;
                nextPoint = primaryPoint;
                Log("現場に到着 → 捜索開始");
                return true;
            }
            if (remainingSearchPoints <= 0)
            {
                CancelInvestigation();
                return false;
            }
            remainingSearchPoints--;
            float offset = searchRadius > .01f ? Random.Range(-searchRadius, searchRadius) : 0f;
            nextPoint = primaryPoint + Vector2.right * offset;
            pauseUntil = Time.time + searchPauseSeconds;
            return true;
        }
        horizontalSpeed = Mathf.Sign(dx) * investigationMoveSpeed;
        return true;
    }

    /// <summary>壁・深い崖により調査地点へ進めない場合のキャンセル。</summary>
    public void NotifyMovementBlocked()
    {
        if (state == CharacterAIInvestigationState2D.None) return;
        blockedCount++;
        if (blockedCount >= 2 || state == CharacterAIInvestigationState2D.GoingToLastKnownPoint)
        {
            CancelInvestigation();
            return;
        }
        nextPoint = primaryPoint + Vector2.right * Random.Range(-searchRadius, searchRadius);
        pauseUntil = Time.time + searchPauseSeconds;
    }

    private void CancelInvestigation()
    {
        if (state == CharacterAIInvestigationState2D.None) return;
        state = CharacterAIInvestigationState2D.None;
        Log("調査終了");
    }

    private void Log(string msg)
    {
        if (debugLogs) Debug.Log("[CharacterAI STEP2] " + name + ": " + msg, this);
    }

    private void OnValidate()
    {
        investigationMoveSpeed = Mathf.Max(.1f, investigationMoveSpeed);
        arrivalDistanceX = Mathf.Max(.05f, arrivalDistanceX);
        searchRadius = Mathf.Max(0f, searchRadius);
        searchPauseSeconds = Mathf.Max(0f, searchPauseSeconds);
        maxInvestigationSeconds = Mathf.Max(1f, maxInvestigationSeconds);
        allyAlertRadius = Mathf.Max(0f, allyAlertRadius);
        alertCooldownSeconds = Mathf.Max(.1f, alertCooldownSeconds);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo || !Application.isPlaying || state == CharacterAIInvestigationState2D.None) return;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(primaryPoint, .3f);
        Gizmos.DrawLine(transform.position, nextPoint);
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(primaryPoint, searchRadius);
    }
}
