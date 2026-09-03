using System;
using System.Collections;
using UnityEngine;

public class CharacterHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField, Min(1)] private int maxHealth = 100;

    [Header("Invincibility Settings")]
    [SerializeField, Min(0f)] private float invincibilityDuration = 0.5f;

    [Header("Damage Debug Log")]
    [Tooltip(
        "オンにすると、実際に減ったHP量をConsoleへ表示します。" +
        "Enemyの被ダメージ確認用です。"
    )]
    [SerializeField] private bool enableDamageDebugLog = true;

    [Tooltip(
        "オンの場合、Playerの被ダメージもログ表示します。" +
        "Enemyだけ確認したい場合はOFFのままでOKです。"
    )]
    [SerializeField] private bool includePlayerDamageInDebugLog = false;

    [Tooltip(
        "オンの場合、無敵時間・死亡済み・0以下Damageなどで" +
        "ダメージが通らなかった時もログ表示します。"
    )]
    [SerializeField] private bool logBlockedDamage = true;

    [Header("Damage Sound")]
    [Tooltip("未設定なら、このオブジェクトのAudioSourceを自動取得します")]
    [SerializeField] private AudioSource audioSource;

    [SerializeField] private AudioClip damageSound;

    [SerializeField, Range(0f, 1f)]
    private float damageSoundVolume = 0.8f;

    public int MaxHealth => maxHealth;
    public int CurrentHealth { get; private set; }
    public bool IsDead { get; private set; }
    public bool IsInvincible { get; private set; }

    public event Action<int, int> HealthChanged;
    public event Action Died;

    /// <summary>
    /// 実際にHPが減った時だけ通知します。
    /// actualDamage / damageGroupId / hitWorldPosition
    /// </summary>
    public event Action<int, int, Vector3> Damaged;

    private Coroutine invincibilityCoroutine;

    // 現在の無敵時間を発生させた「1回の射撃」のDamage Group ID。
    // 0は通常ダメージ（グループ指定なし）。
    private int activeInvincibilityDamageGroupId;

    private void Awake()
    {
        ResetHealth();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        // AudioSourceが無い場合は自動で追加
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }

        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    private void Start()
    {
        NotifyHealthChanged();
    }

    /// <summary>
    /// 通常ダメージ用。既存コード互換のため残します。
    /// Damage Groupを持たない攻撃は0として扱います。
    /// </summary>
    public void TakeDamage(int damage)
    {
        TakeDamage(damage, 0, transform.position);
    }

    /// <summary>
    /// Damage Group ID付きダメージ。
    ///
    /// ショットガンでは「同じ1回の射撃」から出た全Pelletへ
    /// 同じ0以外のIDを渡します。
    ///
    /// Enemyが1発目のPelletで無敵時間に入った後でも、
    /// 同じDamage Group IDのPelletだけはダメージを受けられます。
    ///
    /// PlayerはDamage Group IDがあっても無敵時間を無視しません。
    /// </summary>
    public void TakeDamage(
        int damage,
        int damageGroupId)
    {
        TakeDamage(
            damage,
            damageGroupId,
            transform.position
        );
    }

    /// <summary>
    /// Damage Group IDと命中したワールド座標付きダメージ。
    /// DamagePopupなど、命中位置を使うUI用です。
    /// 既存のTakeDamage呼び出しとの互換性は維持します。
    /// </summary>
    public void TakeDamage(
        int damage,
        int damageGroupId,
        Vector3 hitWorldPosition)
    {
        bool isPlayer = ShouldUsePlayerSkillModifiers();
        bool shouldLog =
            enableDamageDebugLog &&
            (includePlayerDamageInDebugLog || !isPlayer);

        bool canPassCurrentInvincibility =
            !isPlayer &&
            IsInvincible &&
            damageGroupId != 0 &&
            damageGroupId == activeInvincibilityDamageGroupId;

        if (IsDead)
        {
            if (shouldLog && logBlockedDamage)
            {
                Debug.Log(
                    $"[DamageLog][無効:死亡済み] " +
                    $"Target={name} / " +
                    $"RequestedDamage={damage} / " +
                    $"ActualDamage=0 / " +
                    $"DamageGroup={damageGroupId} / " +
                    $"HP={CurrentHealth}/{MaxHealth}",
                    this
                );
            }

            return;
        }

        // Enemyのみ、現在の無敵時間を作ったものと同じDamage Groupなら通す。
        // つまり同じショットのPelletだけが通り、別攻撃は今まで通り無効。
        if (IsInvincible && !canPassCurrentInvincibility)
        {
            if (shouldLog && logBlockedDamage)
            {
                Debug.Log(
                    $"[DamageLog][無効:無敵時間] " +
                    $"Target={name} / " +
                    $"RequestedDamage={damage} / " +
                    $"ActualDamage=0 / " +
                    $"DamageGroup={damageGroupId} / " +
                    $"ActiveGroup={activeInvincibilityDamageGroupId} / " +
                    $"HP={CurrentHealth}/{MaxHealth}",
                    this
                );
            }

            return;
        }

        if (damage <= 0)
        {
            if (shouldLog && logBlockedDamage)
            {
                Debug.Log(
                    $"[DamageLog][無効:Damage<=0] " +
                    $"Target={name} / " +
                    $"RequestedDamage={damage} / " +
                    $"ActualDamage=0 / " +
                    $"DamageGroup={damageGroupId} / " +
                    $"HP={CurrentHealth}/{MaxHealth}",
                    this
                );
            }

            return;
        }

        float damageMultiplier = isPlayer
            ? SkillCardEffectUtility.GetMultiplier(
                SkillEffectType.DamageTaken
            )
            : 1f;

        int finalDamage = Mathf.Max(
            0,
            Mathf.RoundToInt(damage * damageMultiplier)
        );

        if (finalDamage <= 0)
        {
            if (shouldLog && logBlockedDamage)
            {
                Debug.Log(
                    $"[DamageLog][無効:最終Damage=0] " +
                    $"Target={name} / " +
                    $"RequestedDamage={damage} / " +
                    $"Multiplier={damageMultiplier:F3} / " +
                    $"ActualDamage=0 / " +
                    $"DamageGroup={damageGroupId} / " +
                    $"HP={CurrentHealth}/{MaxHealth}",
                    this
                );
            }

            return;
        }

        // このHitが入る前から無敵だったかを保持。
        // 同じPellet Groupの2発目以降ではtrueになります。
        bool wasInvincibleBeforeHit = IsInvincible;

        int healthBeforeDamage = CurrentHealth;

        CurrentHealth = Mathf.Max(
            CurrentHealth - finalDamage,
            0
        );

        // HPが100の敵へ500Damageを与えた場合、
        // 実際に減ったHPは100なので ActualDamage=100 とする。
        int actualDamage =
            Mathf.Max(0, healthBeforeDamage - CurrentHealth);

        bool diedFromThisDamage = CurrentHealth <= 0;

        if (shouldLog)
        {
            Debug.Log(
                $"[DamageLog][被ダメージ] " +
                $"Target={name} / " +
                $"HP={healthBeforeDamage}->{CurrentHealth} / " +
                $"RequestedDamage={damage} / " +
                $"FinalDamage={finalDamage} / " +
                $"ActualDamage={actualDamage} / " +
                $"Multiplier={damageMultiplier:F3} / " +
                $"DamageGroup={damageGroupId} / " +
                $"PelletInvincibilityPass={canPassCurrentInvincibility} / " +
                $"Dead={diedFromThisDamage}",
                this
            );
        }

        // 実際にダメージが通った時だけ被ダメージ音を鳴らす
        PlayDamageSound();

        NotifyHealthChanged();

        // EnemyDamagePopupSpawnerなどへ、実際に減ったHP量と命中位置を通知。
        // 死亡イベントより先に通知することで、死亡処理でEnemyが無効化されても
        // 最後のダメージ数字を表示できるようにします。
        if (actualDamage > 0)
        {
            Damaged?.Invoke(
                actualDamage,
                damageGroupId,
                hitWorldPosition
            );
        }

        if (CurrentHealth <= 0)
        {
            Die();
            return;
        }

        // 同じショットの2発目以降では無敵時間を延長しない。
        // 最初にダメージが通ったPelletの時刻を基準にする。
        if (!wasInvincibleBeforeHit)
        {
            StartInvincibility(damageGroupId);
        }
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        int finalAmount = Mathf.Max(
            0,
            Mathf.RoundToInt(
                amount *
                (ShouldUsePlayerSkillModifiers()
                    ? SkillCardEffectUtility.GetMultiplier(
                        SkillEffectType.HealingReceived
                    )
                    : 1f)
            )
        );

        if (finalAmount <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Min(CurrentHealth + finalAmount, MaxHealth);
        NotifyHealthChanged();
    }

    public void ResetHealth()
    {
        IsDead = false;
        IsInvincible = false;
        activeInvincibilityDamageGroupId = 0;
        CurrentHealth = MaxHealth;
        NotifyHealthChanged();
    }

    /// <summary>
    /// セーブデータなどからHPを直接復元します。
    /// 被ダメージ音・無敵時間は発生させず、HealthChangedだけを通知します。
    /// 0を指定した場合は死亡状態になりますが、既定ではDiedイベントを発火しません。
    /// </summary>
    public void RestoreHealth(
        int healthValue,
        bool invokeDiedEvent = false)
    {
        if (invincibilityCoroutine != null)
        {
            StopCoroutine(invincibilityCoroutine);
            invincibilityCoroutine = null;
        }

        IsInvincible = false;
        activeInvincibilityDamageGroupId = 0;

        CurrentHealth = Mathf.Clamp(
            healthValue,
            0,
            MaxHealth
        );

        bool shouldBeDead = CurrentHealth <= 0;
        bool wasDead = IsDead;
        IsDead = shouldBeDead;

        NotifyHealthChanged();

        if (shouldBeDead && !wasDead && invokeDiedEvent)
        {
            Died?.Invoke();
        }
    }

    private bool ShouldUsePlayerSkillModifiers()
    {
        return CompareTag("Player") ||
               (transform.root != null &&
                transform.root.CompareTag("Player"));
    }

    private void PlayDamageSound()
    {
        if (audioSource == null || damageSound == null)
        {
            return;
        }

        audioSource.PlayOneShot(damageSound, damageSoundVolume);
    }

    private void StartInvincibility(int damageGroupId)
    {
        if (invincibilityDuration <= 0f)
        {
            IsInvincible = false;
            activeInvincibilityDamageGroupId = 0;
            return;
        }

        if (invincibilityCoroutine != null)
        {
            StopCoroutine(invincibilityCoroutine);
        }

        activeInvincibilityDamageGroupId =
            Mathf.Max(0, damageGroupId);

        invincibilityCoroutine =
            StartCoroutine(InvincibilityRoutine());
    }

    private IEnumerator InvincibilityRoutine()
    {
        IsInvincible = true;

        yield return new WaitForSeconds(invincibilityDuration);

        IsInvincible = false;
        activeInvincibilityDamageGroupId = 0;
        invincibilityCoroutine = null;
    }

    private void Die()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;
        Died?.Invoke();
    }

    private void NotifyHealthChanged()
    {
        HealthChanged?.Invoke(CurrentHealth, MaxHealth);
    }
}