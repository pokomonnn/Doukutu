using System;
using UnityEngine;

/// <summary>
/// 移動AIを持たないNPC用の簡易近接戦闘。
/// EnemyChaser2Dを使うNPCには付けない（二重攻撃になる）。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(FactionMember2D))]
[RequireComponent(typeof(FactionEnemyTargeting2D))]
[RequireComponent(typeof(CharacterHealth))]
public sealed class FactionGuardCombat2D : MonoBehaviour
{
    [Header("索敵・攻撃")]
    [SerializeField, Min(0f)] private float detectionDistance = 6f;
    [SerializeField, Min(0f)] private float loseDistance = 8f;
    [SerializeField, Min(0.1f)] private float attackDistance = 1.1f;
    [SerializeField, Min(1)] private int attackDamage = 5;
    [SerializeField, Min(0.1f)] private float attackInterval = 1.5f;
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask obstacleLayers;
    [Header("攻撃アニメーション（任意）")]
    [SerializeField] private Animator animator;
    [SerializeField] private string attackTriggerName = "Attack";
    [Header("向き")]
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private bool spriteFacesRightByDefault = true;

    public event Action AttackPerformed;

    private FactionEnemyTargeting2D targeting;
    private CharacterHealth ownHealth;
    private Transform selected;
    private float nextAttackTime;

    private void Awake()
    {
        targeting = GetComponent<FactionEnemyTargeting2D>();
        ownHealth = GetComponent<CharacterHealth>();
        if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
        if (animator == null) animator = GetComponentInChildren<Animator>(true);
    }

    private void Start()
    {
        // リフレクションによる曖昧な判別ではなく、コンポーネントで二重実行を防ぐ。
        if (GetComponent<EnemyChaser2D>() != null)
        {
            Debug.LogWarning("[Faction STEP3] EnemyChaser2DとFactionGuardCombat2Dを同じNPCに追加しないでください。Guard側を停止します。", this);
            enabled = false;
        }
    }

    private void Update()
    {
        if (ownHealth == null || ownHealth.IsDead || targeting == null) return;
        targeting.ResolveTarget(selected, detectionDistance, loseDistance,
            out Transform target, out CharacterHealth health);
        selected = target;
        if (target == null || health == null) return;

        Vector2 diff = target.position - transform.position;
        if (spriteRenderer != null && Mathf.Abs(diff.x) > 0.03f)
        {
            bool right = diff.x > 0f;
            spriteRenderer.flipX = spriteFacesRightByDefault ? !right : right;
        }
        if (diff.sqrMagnitude > attackDistance * attackDistance || Time.time < nextAttackTime) return;

        if (requireLineOfSight && obstacleLayers.value != 0)
        {
            RaycastHit2D wall = Physics2D.Raycast(transform.position, diff.normalized,
                diff.magnitude, obstacleLayers);
            if (wall.collider != null) return;
        }

        nextAttackTime = Time.time + attackInterval;
        FactionCombatDamage2D.DealDamage(health, attackDamage, transform);
        if (animator != null && !string.IsNullOrEmpty(attackTriggerName))
        {
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                if (parameter.type == AnimatorControllerParameterType.Trigger &&
                    parameter.name == attackTriggerName)
                {
                    animator.SetTrigger(attackTriggerName);
                    break;
                }
            }
        }
        AttackPerformed?.Invoke();
    }

    private void OnValidate()
    {
        detectionDistance = Mathf.Max(0f, detectionDistance);
        loseDistance = Mathf.Max(detectionDistance, loseDistance);
        attackDistance = Mathf.Max(0.1f, attackDistance);
        attackDamage = Mathf.Max(1, attackDamage);
        attackInterval = Mathf.Max(0.1f, attackInterval);
    }
}
