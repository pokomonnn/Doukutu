using UnityEngine;

/// <summary>
/// STEP3: HP低下時の逃走／危険な派閥を見つけたNPCの恐怖反応。
/// Rigidbody2Dを直接動かさず、EnemyChaser2D / CharacterWander2D に速度指示を渡します。
/// 攻撃対象の選択と派閥ルールは変更しません。
/// </summary>
public enum CharacterAIFearResponse2D { None, Flee, Cower }
public enum CharacterAIFearState2D { Calm, Fleeing, Cowering, Recovering }

[DisallowMultipleComponent]
public sealed class CharacterAIFearFlee2D : MonoBehaviour
{
    [Header("HP低下による退避（戦闘する敵にも使用可）")]
    [SerializeField] private bool fleeWhenLowHealth;
    [SerializeField, Range(0.01f, 1f)] private float fleeHealthRatio = .25f;
    [SerializeField, Range(0.01f, 1f)] private float stopFleeHealthRatio = .5f;

    [Header("敵対勢力への恐怖（救出NPCなど）")]
    [SerializeField] private CharacterAIFearResponse2D fearResponse = CharacterAIFearResponse2D.None;
    [SerializeField, Min(.25f)] private float fearStartDistance = 4.5f;
    [SerializeField, Min(.25f)] private float threatScanDistance = 7f;
    [Tooltip("自分が攻撃しない相手でも、相手が自分を攻撃するなら危険として扱います。")]
    [SerializeField] private bool reactToIncomingHostility = true;
    [Tooltip("避難先を探す時のColliderレイヤー。Everything推奨。")]
    [SerializeField] private LayerMask threatLayers = ~0;

    [Header("反応中の移動・復帰")]
    [SerializeField, Min(.1f)] private float fleeSpeed = 3.4f;
    [Tooltip("敵がいなくなってもこの秒数は逃走・恐怖を継続します。")]
    [SerializeField, Min(0f)] private float panicHoldSeconds = 2.5f;
    [SerializeField, Min(0f)] private float calmDownSeconds = 1.2f;
    [SerializeField, Min(.05f)] private float rescanInterval = .18f;
    [Header("アニメーション連携（任意）")]
    [SerializeField] private Animator characterAnimator;
    [Tooltip("Animatorに同名のBoolがある時だけ切り替えます。空欄なら何もしません。")]
    [SerializeField] private string fleeingBoolName = "IsFleeing";
    [SerializeField] private string coweringBoolName = "IsCowering";

    [Header("デバッグ")]
    [SerializeField] private bool debugLogs;
    [SerializeField] private bool showGizmo = true;

    public CharacterAIFearState2D CurrentState { get; private set; }
    public bool IsReacting => isActiveAndEnabled && CurrentState != CharacterAIFearState2D.Calm;
    public bool IsFleeing => isActiveAndEnabled && CurrentState == CharacterAIFearState2D.Fleeing;
    public bool IsCowering => isActiveAndEnabled && CurrentState == CharacterAIFearState2D.Cowering;

    private CharacterHealth health;
    private FactionMember2D faction;
    private CarryableObject2D carry;
    private CharacterWander2D wander;
    private CharacterAIPersonality2D personalityAI;
    private Transform threat;
    private float nextScanTime;
    private float panicUntil;
    private float calmUntil;
    private float blockedUntil;
    private float lastFleeDirection = 1f;
    private bool healthPanicLatched;
    private int lastEvaluatedFrame = -1;

    private void Awake() { CacheReferences(); }
    private void OnEnable() { CacheReferences(); }
    private void OnDisable()
    {
        ApplyAnimation(CharacterAIFearState2D.Calm);
        CurrentState = CharacterAIFearState2D.Calm;
        healthPanicLatched = false;
        threat = null;
        lastEvaluatedFrame = -1;
    }

    private void CacheReferences()
    {
        if (health == null) health = GetComponent<CharacterHealth>();
        if (health == null) health = GetComponentInChildren<CharacterHealth>();
        if (faction == null) faction = GetComponent<FactionMember2D>();
        if (carry == null) carry = GetComponent<CarryableObject2D>();
        if (wander == null) wander = GetComponent<CharacterWander2D>();
        if (personalityAI == null) personalityAI = GetComponent<CharacterAIPersonality2D>();
        if (characterAnimator == null) characterAnimator = GetComponentInChildren<Animator>(true);
    }

    private void Update() { Evaluate(); }

    /// <summary>trueなら移動を専有する。0でも「しゃがむ／立ちすくむ」状態なのでtrueを返す。</summary>
    public bool TryGetDesiredSpeed(out float horizontalSpeed)
    {
        Evaluate();
        horizontalSpeed = 0f;
        if (!IsReacting) return false;
        if (CurrentState == CharacterAIFearState2D.Fleeing && Time.time >= blockedUntil)
            horizontalSpeed = lastFleeDirection * fleeSpeed;
        return true;
    }

    public void NotifyMovementBlocked()
    {
        // 崖や壁の先へ逃げようとせず、その場で一時停止して安全を優先します。
        blockedUntil = Time.time + .7f;
    }

    private void Evaluate()
    {
        if (!isActiveAndEnabled || lastEvaluatedFrame == Time.frameCount) return;
        lastEvaluatedFrame = Time.frameCount;
        CacheReferences();

        if ((health != null && health.IsDead) ||
            (carry != null && carry.IsReserved) ||
            (wander != null && wander.IsRecoveringFromCarry))
        {
            healthPanicLatched = false;
            threat = null;
            ChangeState(CharacterAIFearState2D.Calm);
            return;
        }

        if (fleeWhenLowHealth && health != null && health.MaxHealth > 0)
        {
            float hpRatio = (float)health.CurrentHealth / health.MaxHealth;
            float fleeLimit = personalityAI != null
                ? personalityAI.AdjustFleeHealthRatio(fleeHealthRatio)
                : fleeHealthRatio;
            // 元のヒステリシス幅を保持し、逃走/回復の細かな往復を防ぎます。
            float recoverLimit = Mathf.Clamp(
                fleeLimit + Mathf.Max(0.01f, stopFleeHealthRatio - fleeHealthRatio),
                fleeLimit + 0.01f, 1f);
            if (hpRatio <= fleeLimit) healthPanicLatched = true;
            else if (hpRatio >= recoverLimit) healthPanicLatched = false;
        }
        else healthPanicLatched = false;

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + rescanInterval;
            ScanForHostile();
        }

        bool threatInRange = threat != null && threat.gameObject.activeInHierarchy &&
            Vector2.Distance(transform.position, threat.position) <= threatScanDistance;
        float threatDistance = threatInRange
            ? Vector2.Distance(transform.position, threat.position)
            : float.PositiveInfinity;
        bool shouldFlee = healthPanicLatched && threatInRange;
        bool shouldFear = fearResponse != CharacterAIFearResponse2D.None &&
            threatInRange && threatDistance <= fearStartDistance;

        if (shouldFlee || shouldFear)
        {
            float deltaX = transform.position.x - threat.position.x;
            if (Mathf.Abs(deltaX) >= .05f) lastFleeDirection = Mathf.Sign(deltaX);
            panicUntil = Time.time + panicHoldSeconds;
            ChangeState(shouldFlee || fearResponse == CharacterAIFearResponse2D.Flee
                ? CharacterAIFearState2D.Fleeing
                : CharacterAIFearState2D.Cowering);
            return;
        }

        if ((CurrentState == CharacterAIFearState2D.Fleeing ||
             CurrentState == CharacterAIFearState2D.Cowering) && Time.time < panicUntil)
            return;

        if (CurrentState == CharacterAIFearState2D.Fleeing ||
            CurrentState == CharacterAIFearState2D.Cowering)
        {
            calmUntil = Time.time + calmDownSeconds;
            ChangeState(CharacterAIFearState2D.Recovering);
        }
        else if (CurrentState == CharacterAIFearState2D.Recovering && Time.time >= calmUntil)
            ChangeState(CharacterAIFearState2D.Calm);
    }

    private void ScanForHostile()
    {
        threat = null;
        if (faction == null || (!fleeWhenLowHealth && fearResponse == CharacterAIFearResponse2D.None)) return;
        Collider2D[] overlaps = Physics2D.OverlapCircleAll(transform.position,
            threatScanDistance, threatLayers);
        float bestDistance = float.PositiveInfinity;
        foreach (Collider2D collider in overlaps)
        {
            if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
            FactionMember2D other = collider.GetComponentInParent<FactionMember2D>();
            if (other == null || other == faction || other.transform.IsChildOf(transform)) continue;
            CharacterHealth otherHp = other.GetComponent<CharacterHealth>();
            if (otherHp == null) otherHp = other.GetComponentInChildren<CharacterHealth>();
            if (otherHp != null && otherHp.IsDead) continue;
            if (!FactionHostility2D.IsHostile(faction, other) &&
                !(reactToIncomingHostility && FactionHostility2D.IsHostile(other, faction))) continue;
            float d = ((Vector2)other.transform.position - (Vector2)transform.position).sqrMagnitude;
            if (d < bestDistance)
            {
                bestDistance = d;
                threat = other.transform;
            }
        }
    }

    private void ChangeState(CharacterAIFearState2D next)
    {
        if (CurrentState == next) return;
        CurrentState = next;
        ApplyAnimation(next);
        if (debugLogs) Debug.Log("[CharacterAI STEP3] " + name + " -> " + next, this);
    }

    private void ApplyAnimation(CharacterAIFearState2D current)
    {
        if (characterAnimator == null || !characterAnimator.isActiveAndEnabled) return;
        TrySetBool(fleeingBoolName, current == CharacterAIFearState2D.Fleeing);
        TrySetBool(coweringBoolName, current == CharacterAIFearState2D.Cowering);
    }

    private void TrySetBool(string parameterName, bool value)
    {
        if (string.IsNullOrWhiteSpace(parameterName) || characterAnimator == null) return;
        foreach (AnimatorControllerParameter parameter in characterAnimator.parameters)
        {
            if (parameter.name == parameterName && parameter.type == AnimatorControllerParameterType.Bool)
            {
                characterAnimator.SetBool(parameterName, value);
                break;
            }
        }
    }

    private void OnValidate()
    {
        stopFleeHealthRatio = Mathf.Max(fleeHealthRatio, stopFleeHealthRatio);
        threatScanDistance = Mathf.Max(fearStartDistance, threatScanDistance);
        fleeSpeed = Mathf.Max(.1f, fleeSpeed);
        rescanInterval = Mathf.Max(.05f, rescanInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showGizmo) return;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, fearStartDistance);
        if (Application.isPlaying && threat != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, threat.position);
        }
    }
}
