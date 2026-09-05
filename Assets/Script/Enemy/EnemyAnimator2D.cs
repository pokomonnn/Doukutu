using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通常EnemyのIdle / Chase / Attack / Hit / Deathアニメーションを、
/// EnemyChaser2D・CharacterHealthと連動させるコンポーネントです。
///
/// 使い方：Enemyの親Object（CharacterHealth / Rigidbody2D / EnemyChaser2Dがある場所）へ追加します。
/// AnimatorのParameter名はInspectorから変更できます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class EnemyAnimator2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら自身または子Objectから自動取得します")]
    [SerializeField] private Animator animator;

    [SerializeField] private CharacterHealth health;
    [SerializeField] private Rigidbody2D enemyRigidbody;
    [SerializeField] private EnemyChaser2D enemyChaser;
    [SerializeField] private EnemyRangedAttack2D rangedAttack;
    [SerializeField] private EnemySanityAura2D sanityAura;

    [Header("移動パラメータ")]
    [Tooltip("空欄なら設定しません。IdleとChaseの切替に使うBoolです")]
    [SerializeField] private string isMovingBoolName = "IsMoving";

    [Tooltip("空欄なら設定しません。追跡対象を見つけているかを表すBoolです")]
    [SerializeField] private string hasTargetBoolName = "HasTarget";

    [Tooltip("空欄なら設定しません。Rigidbody2Dの横速度の絶対値を入れるFloatです")]
    [SerializeField] private string moveSpeedFloatName = "MoveSpeed";

    [Tooltip("この横速度未満なら停止扱いにします")]
    [SerializeField, Min(0f)] private float movingSpeedThreshold = 0.03f;

    [Header("イベントTrigger")]
    [Tooltip("空欄なら攻撃Triggerは使いません。EnemyChaser2Dが実際に攻撃を試みた瞬間に呼ばれます")]
    [SerializeField] private string attackTriggerName = "Attack";

    [Tooltip("空欄なら遠距離攻撃Triggerは使いません。EnemyRangedAttack2Dの発射時に呼ばれます")]
    [SerializeField] private string rangedAttackTriggerName = "RangedAttack";

    [Tooltip("空欄なら特殊攻撃Triggerは使いません。EnemySanityAura2Dの効果発生時に呼ばれます")]
    [SerializeField] private string specialAttackTriggerName = "SpecialAttack";

    [Tooltip("空欄なら被弾Triggerは使いません。実際にHPが減った時に呼ばれます")]
    [SerializeField] private string hitTriggerName = "Hit";

    [Tooltip("空欄なら死亡Triggerは使いません。CharacterHealth.Diedと連動します")]
    [SerializeField] private string deathTriggerName = "Die";

    [Header("動作設定")]
    [Tooltip("死亡時に移動・追跡Boolをfalseへ戻します")]
    [SerializeField] private bool resetMovementBoolsOnDeath = true;

    [Tooltip("Animator Controllerに存在しないParameter名が設定されていた時にConsoleへ警告を出します")]
    [SerializeField] private bool showMissingParameterWarnings = true;

    [Header("デバッグ")]
    [SerializeField] private bool showStateLogs;

    private readonly Dictionary<string, AnimatorControllerParameterType>
        animatorParameters =
            new Dictionary<string, AnimatorControllerParameterType>();

    private int lastHealth;
    private bool hasCachedHealth;
    private bool isSubscribed;
    private bool isDead;
    private bool pendingHitTrigger;

    private void Awake()
    {
        FindReferences();
        CacheAnimatorParameters();
        CacheCurrentHealth();
    }

    private void OnEnable()
    {
        FindReferences();
        CacheAnimatorParameters();
        SubscribeEvents();

        isDead = health != null && health.IsDead;

        if (isDead)
        {
            ApplyDeathAnimationState();
        }
    }

    private void Start()
    {
        // CharacterHealthのAwake順序がこのコンポーネントより後だった場合にも、
        // 開始時HPを正しく基準値として保存します。
        CacheCurrentHealth();
        RefreshMovementParameters();
    }

    private void Update()
    {
        if (health != null && health.IsDead)
        {
            if (!isDead)
            {
                HandleDied();
            }

            return;
        }

        if (!isDead)
        {
            RefreshMovementParameters();
        }
    }

    private void LateUpdate()
    {
        // CharacterHealthはHP変更後に死亡イベントも同じフレームで通知します。
        // LateUpdateまで待つことで、致死ダメージでHitよりDieが優先されます。
        if (!pendingHitTrigger)
        {
            return;
        }

        pendingHitTrigger = false;

        if (!isDead && health != null && !health.IsDead)
        {
            TriggerIfExists(hitTriggerName);

            if (showStateLogs)
            {
                Debug.Log(
                    $"[EnemyAnimator2D] {name}: Hit Trigger",
                    this
                );
            }
        }
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        pendingHitTrigger = false;
    }

    private void HandleAttackPerformed()
    {
        if (isDead)
        {
            return;
        }

        TriggerIfExists(attackTriggerName);

        if (showStateLogs)
        {
            Debug.Log(
                $"[EnemyAnimator2D] {name}: Attack Trigger",
                this
            );
        }
    }

    private void HandleRangedAttackPerformed()
    {
        if (isDead)
        {
            return;
        }

        TriggerIfExists(rangedAttackTriggerName);

        if (showStateLogs)
        {
            Debug.Log($"[EnemyAnimator2D] {name}: RangedAttack Trigger", this);
        }
    }

    private void HandleSpecialEffectPerformed()
    {
        if (isDead)
        {
            return;
        }

        TriggerIfExists(specialAttackTriggerName);

        if (showStateLogs)
        {
            Debug.Log($"[EnemyAnimator2D] {name}: SpecialAttack Trigger", this);
        }
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (!hasCachedHealth)
        {
            lastHealth = currentHealth;
            hasCachedHealth = true;
            return;
        }

        if (currentHealth < lastHealth)
        {
            pendingHitTrigger = true;
        }

        lastHealth = currentHealth;
    }

    private void HandleDied()
    {
        if (isDead)
        {
            return;
        }

        isDead = true;
        pendingHitTrigger = false;
        ApplyDeathAnimationState();

        if (showStateLogs)
        {
            Debug.Log(
                $"[EnemyAnimator2D] {name}: Die Trigger",
                this
            );
        }
    }

    private void ApplyDeathAnimationState()
    {
        if (resetMovementBoolsOnDeath)
        {
            SetBoolIfExists(isMovingBoolName, false);
            SetBoolIfExists(hasTargetBoolName, false);
            SetFloatIfExists(moveSpeedFloatName, 0f);
        }

        ResetTriggerIfExists(attackTriggerName);
        ResetTriggerIfExists(rangedAttackTriggerName);
        ResetTriggerIfExists(specialAttackTriggerName);
        ResetTriggerIfExists(hitTriggerName);
        TriggerIfExists(deathTriggerName);
    }

    private void RefreshMovementParameters()
    {
        float horizontalSpeed = enemyRigidbody != null
            ? Mathf.Abs(enemyRigidbody.linearVelocity.x)
            : 0f;

        bool isMoving = enemyChaser != null
            ? enemyChaser.IsActivelyMoving
            : horizontalSpeed > movingSpeedThreshold;

        // Rigidbodyの押し出しだけでChaseへ切り替わらないよう、
        // EnemyChaser2Dがある場合はその追跡状態を優先します。
        bool hasTarget = enemyChaser != null &&
            enemyChaser.HasDetectedPlayer;

        SetBoolIfExists(isMovingBoolName, isMoving);
        SetBoolIfExists(hasTargetBoolName, hasTarget);
        SetFloatIfExists(moveSpeedFloatName, horizontalSpeed);
    }

    private void SubscribeEvents()
    {
        if (isSubscribed)
        {
            return;
        }

        if (health != null)
        {
            health.HealthChanged += HandleHealthChanged;
            health.Died += HandleDied;
        }

        if (enemyChaser != null)
        {
            enemyChaser.AttackPerformed += HandleAttackPerformed;
        }

        if (rangedAttack != null)
        {
            rangedAttack.RangedAttackPerformed += HandleRangedAttackPerformed;
        }

        if (sanityAura != null)
        {
            sanityAura.SpecialEffectPerformed += HandleSpecialEffectPerformed;
        }

        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed)
        {
            return;
        }

        if (health != null)
        {
            health.HealthChanged -= HandleHealthChanged;
            health.Died -= HandleDied;
        }

        if (enemyChaser != null)
        {
            enemyChaser.AttackPerformed -= HandleAttackPerformed;
        }

        if (rangedAttack != null)
        {
            rangedAttack.RangedAttackPerformed -= HandleRangedAttackPerformed;
        }

        if (sanityAura != null)
        {
            sanityAura.SpecialEffectPerformed -= HandleSpecialEffectPerformed;
        }

        isSubscribed = false;
    }

    private void CacheCurrentHealth()
    {
        if (health == null)
        {
            return;
        }

        lastHealth = health.CurrentHealth;
        hasCachedHealth = true;
    }

    private void CacheAnimatorParameters()
    {
        animatorParameters.Clear();

        if (animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimatorControllerParameter parameter
                 in animator.parameters)
        {
            if (parameter == null ||
                string.IsNullOrWhiteSpace(parameter.name))
            {
                continue;
            }

            animatorParameters[parameter.name] = parameter.type;
        }

        WarnIfConfiguredParameterIsMissing(
            isMovingBoolName,
            AnimatorControllerParameterType.Bool
        );

        WarnIfConfiguredParameterIsMissing(
            hasTargetBoolName,
            AnimatorControllerParameterType.Bool
        );

        WarnIfConfiguredParameterIsMissing(
            moveSpeedFloatName,
            AnimatorControllerParameterType.Float
        );

        WarnIfConfiguredParameterIsMissing(
            attackTriggerName,
            AnimatorControllerParameterType.Trigger
        );

        WarnIfConfiguredParameterIsMissing(
            rangedAttackTriggerName,
            AnimatorControllerParameterType.Trigger
        );

        WarnIfConfiguredParameterIsMissing(
            specialAttackTriggerName,
            AnimatorControllerParameterType.Trigger
        );

        WarnIfConfiguredParameterIsMissing(
            hitTriggerName,
            AnimatorControllerParameterType.Trigger
        );

        WarnIfConfiguredParameterIsMissing(
            deathTriggerName,
            AnimatorControllerParameterType.Trigger
        );
    }

    private void WarnIfConfiguredParameterIsMissing(
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        if (!showMissingParameterWarnings ||
            string.IsNullOrWhiteSpace(parameterName) ||
            animator == null ||
            animator.runtimeAnimatorController == null)
        {
            return;
        }

        if (!animatorParameters.TryGetValue(
                parameterName,
                out AnimatorControllerParameterType actualType))
        {
            Debug.LogWarning(
                $"[EnemyAnimator2D] {name}: Animatorに " +
                $"'{parameterName}' Parameterがありません。" +
                "使わない場合は該当欄を空欄にしてください。",
                this
            );

            return;
        }

        if (actualType != expectedType)
        {
            Debug.LogWarning(
                $"[EnemyAnimator2D] {name}: '{parameterName}' は " +
                $"{expectedType} ではなく {actualType} です。",
                this
            );
        }
    }

    private void SetBoolIfExists(string parameterName, bool value)
    {
        if (!HasParameter(
                parameterName,
                AnimatorControllerParameterType.Bool))
        {
            return;
        }

        animator.SetBool(parameterName, value);
    }

    private void SetFloatIfExists(string parameterName, float value)
    {
        if (!HasParameter(
                parameterName,
                AnimatorControllerParameterType.Float))
        {
            return;
        }

        animator.SetFloat(parameterName, value);
    }

    private void TriggerIfExists(string parameterName)
    {
        if (!HasParameter(
                parameterName,
                AnimatorControllerParameterType.Trigger))
        {
            return;
        }

        animator.SetTrigger(parameterName);
    }

    private void ResetTriggerIfExists(string parameterName)
    {
        if (!HasParameter(
                parameterName,
                AnimatorControllerParameterType.Trigger))
        {
            return;
        }

        animator.ResetTrigger(parameterName);
    }

    private bool HasParameter(
        string parameterName,
        AnimatorControllerParameterType expectedType)
    {
        if (animator == null ||
            string.IsNullOrWhiteSpace(parameterName))
        {
            return false;
        }

        if (!animatorParameters.TryGetValue(
                parameterName,
                out AnimatorControllerParameterType actualType))
        {
            return false;
        }

        return actualType == expectedType;
    }

    private void FindReferences()
    {
        if (health == null)
        {
            health = GetComponent<CharacterHealth>();
        }

        if (enemyRigidbody == null)
        {
            enemyRigidbody = GetComponent<Rigidbody2D>();
        }

        if (enemyChaser == null)
        {
            enemyChaser = GetComponent<EnemyChaser2D>();
        }

        if (rangedAttack == null)
        {
            rangedAttack = GetComponent<EnemyRangedAttack2D>();
        }

        if (sanityAura == null)
        {
            sanityAura = GetComponent<EnemySanityAura2D>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }
    }

    private void OnValidate()
    {
        movingSpeedThreshold = Mathf.Max(0f, movingSpeedThreshold);
    }
}