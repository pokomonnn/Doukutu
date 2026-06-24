using System;
using System.Collections;
using UnityEngine;

public class CharacterHealth : MonoBehaviour
{
    [Header("Health Settings")]
    [SerializeField, Min(1)] private int maxHealth = 100;

    [Header("Invincibility Settings")]
    [SerializeField, Min(0f)] private float invincibilityDuration = 0.5f;

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

    private Coroutine invincibilityCoroutine;

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

    public void TakeDamage(int damage)
    {
        // 無敵中はダメージを受けず、音も鳴らない
        if (IsDead || IsInvincible || damage <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Max(CurrentHealth - damage, 0);

        // 実際にダメージが通った時だけ被ダメージ音を鳴らす
        PlayDamageSound();

        NotifyHealthChanged();

        if (CurrentHealth <= 0)
        {
            Die();
            return;
        }

        StartInvincibility();
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0)
        {
            return;
        }

        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        NotifyHealthChanged();
    }

    public void ResetHealth()
    {
        IsDead = false;
        IsInvincible = false;
        CurrentHealth = MaxHealth;
        NotifyHealthChanged();
    }

    private void PlayDamageSound()
    {
        if (audioSource == null || damageSound == null)
        {
            return;
        }

        audioSource.PlayOneShot(damageSound, damageSoundVolume);
    }

    private void StartInvincibility()
    {
        if (invincibilityDuration <= 0f)
        {
            return;
        }

        if (invincibilityCoroutine != null)
        {
            StopCoroutine(invincibilityCoroutine);
        }

        invincibilityCoroutine = StartCoroutine(InvincibilityRoutine());
    }

    private IEnumerator InvincibilityRoutine()
    {
        IsInvincible = true;

        yield return new WaitForSeconds(invincibilityDuration);

        IsInvincible = false;
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