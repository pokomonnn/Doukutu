using UnityEngine;

/// <summary>
/// STEP3: EnemyChaser2Dなどに追加する攻撃対象選択コンポーネント。
/// STEP2の「攻撃者 -> 対象」一方向ルールを使う。中立相手は自動索敵しない。
/// EnemyChaser2Dとの連携は付属のEditorパッチを適用する。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FactionMember2D))]
public sealed class FactionEnemyTargeting2D : MonoBehaviour
{
    [Header("索敵範囲に含めるLayer")]
    [SerializeField] private LayerMask targetLayers = ~0;

    [Header("候補検索の間隔")]
    [SerializeField, Range(0.10f, 2f)] private float scanInterval = 0.35f;

    [Header("反撃")]
    [Tooltip("FactionMember2Dで反撃許可の場合、実際に攻撃された相手を一定秒数優先します。")]
    [SerializeField, Min(0f)] private float retaliationDuration = 9f;

    [Header("デバッグ")]
    [SerializeField] private bool printTargetChanges;
    [SerializeField] private bool drawRangeGizmo = true;

    private FactionMember2D self;
    private CharacterHealth selfHealth;
    private Transform rememberedTarget;
    private CharacterHealth rememberedHealth;
    private Transform provoker;
    private float provokerExpiresAt;
    private float nextScanTime;
    private float gizmoRange = 8f;

    public Transform CurrentTarget => rememberedTarget;
    public CharacterHealth CurrentTargetHealth => rememberedHealth;

    private void Awake()
    {
        self = GetComponent<FactionMember2D>();
        selfHealth = GetComponent<CharacterHealth>();
    }

    /// <summary>
    /// EnemyChaser2Dに組み込んだFindPlayer()から呼び出す。
    /// 検索は間引き、現在の相手のカルマ/友好度変化だけは毎回判定する。
    /// </summary>
    public void ResolveTarget(Transform oldTarget, float detectionDistance,
        float loseTargetDistance, out Transform target, out CharacterHealth targetHealth)
    {
        if (self == null) self = GetComponent<FactionMember2D>();
        if (selfHealth == null) selfHealth = GetComponent<CharacterHealth>();
        float acquire = Mathf.Max(0f, detectionDistance);
        float retain = Mathf.Max(acquire, loseTargetDistance);
        gizmoRange = acquire;

        if (self == null || string.IsNullOrEmpty(self.FactionId) || self.IsPlayer ||
            (selfHealth != null && selfHealth.IsDead))
        {
            ChangeTarget(null, null);
            target = null;
            targetHealth = null;
            return;
        }

        if (rememberedTarget == null && oldTarget != null)
        {
            if (TryValidateTarget(oldTarget, retain, out CharacterHealth previousHealth))
                ChangeTarget(oldTarget, previousHealth);
        }

        // 短絡評価の外で値を使わないように、対象が存在する場合だけ検証する。
        // これにより CS0165 (stillAlive の未代入使用) を防ぐ。
        if (rememberedTarget != null)
        {
            if (TryValidateTarget(rememberedTarget, retain,
                    out CharacterHealth stillAlive))
            {
                rememberedHealth = stillAlive;
            }
            else
            {
                ChangeTarget(null, null);
            }
        }

        // 反撃対象が有効なら最優先。中立→反撃の切り替えを即時に反映する。
        if (provoker != null && Time.time < provokerExpiresAt &&
            TryValidateTarget(provoker, retain, out CharacterHealth attackerHealth))
        {
            ChangeTarget(provoker, attackerHealth);
        }

        if (rememberedTarget == null && Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + Mathf.Max(0.1f, scanInterval);
            FindClosestHostile(acquire);
        }

        target = rememberedTarget;
        targetHealth = rememberedHealth;
    }

    public void NotifyAttackedBy(Transform attackerTransform)
    {
        if (self == null) self = GetComponent<FactionMember2D>();
        if (self == null || !self.RetaliateWhenAttacked || retaliationDuration <= 0f ||
            attackerTransform == null) return;

        FactionMember2D aggressor = attackerTransform.GetComponentInParent<FactionMember2D>();
        if (aggressor == null) aggressor = attackerTransform.GetComponentInChildren<FactionMember2D>();
        if (aggressor == null || aggressor == self || aggressor.IsPlayer && self.IsPlayer ||
            FactionId2D.Same(self.FactionId, aggressor.FactionId)) return;

        provoker = aggressor.transform;
        provokerExpiresAt = Time.time + retaliationDuration;
        nextScanTime = 0f;
    }

    private void FindClosestHostile(float radius)
    {
        Collider2D[] nearby = Physics2D.OverlapCircleAll(transform.position, radius, targetLayers.value);
        float bestSq = float.PositiveInfinity;
        Transform best = null;
        CharacterHealth bestHealth = null;
        foreach (Collider2D col in nearby)
        {
            if (col == null || !col.enabled) continue;
            FactionMember2D other = col.GetComponentInParent<FactionMember2D>();
            if (other == null || other == self) continue;
            Transform t = other.transform;
            float sq = (t.position - transform.position).sqrMagnitude;
            if (sq >= bestSq || !TryValidateTarget(t, radius, out CharacterHealth health)) continue;
            bestSq = sq;
            best = t;
            bestHealth = health;
        }
        if (best != null) ChangeTarget(best, bestHealth);
    }

    private bool TryValidateTarget(Transform candidate, float maxDistance, out CharacterHealth health)
    {
        health = null;
        if (candidate == null || !candidate.gameObject.activeInHierarchy) return false;
        FactionMember2D targetFaction = candidate.GetComponent<FactionMember2D>();
        if (targetFaction == null) targetFaction = candidate.GetComponentInParent<FactionMember2D>();
        if (targetFaction == null || targetFaction == self ||
            (targetFaction.transform.position - transform.position).sqrMagnitude > maxDistance * maxDistance) return false;

        bool provoked = targetFaction.transform == provoker && Time.time < provokerExpiresAt &&
            self.RetaliateWhenAttacked && !FactionId2D.Same(self.FactionId, targetFaction.FactionId);
        if (!provoked && !FactionHostility2D.IsHostile(self, targetFaction)) return false;

        health = targetFaction.GetComponent<CharacterHealth>();
        if (health == null) health = targetFaction.GetComponentInChildren<CharacterHealth>();
        return health != null && health.isActiveAndEnabled && !health.IsDead;
    }

    private void ChangeTarget(Transform next, CharacterHealth health)
    {
        if (printTargetChanges && rememberedTarget != next)
            Debug.Log("[Faction STEP3] " + name + " target: " +
                (next != null ? next.name : "なし"), this);
        rememberedTarget = next;
        rememberedHealth = health;
        if (next == null) nextScanTime = Mathf.Min(nextScanTime, Time.time);
    }

    private void OnDisable()
    {
        rememberedTarget = null;
        rememberedHealth = null;
        provoker = null;
        nextScanTime = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawRangeGizmo) return;
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, gizmoRange);
        if (rememberedTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position, rememberedTarget.position);
        }
    }
}
