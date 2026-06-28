using System;
using UnityEngine;

public enum SanityState
{
    Normal,
    Warning,
    Low,
    Critical,
    Empty
}

/// <summary>
/// プレイヤーのSAN値を管理します。
/// 敵・イベント・暗闇などから DrainSanity() を呼ぶことで減少させられます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class PlayerSanityController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら同じPlayerから自動取得します")]
    [SerializeField] private CharacterHealth playerHealth;

    [Header("最大値")]
    [SerializeField, Min(1f)] private float maxSanity = 100f;

    [Header("開始時の値")]
    [Tooltip("オンなら開始時にSAN値を最大まで回復します")]
    [SerializeField] private bool startWithFullSanity = true;

    [SerializeField, Min(0f)] private float startingSanity = 100f;

    [Header("時間経過で減らす設定（テスト・常時減少用）")]
    [Tooltip("オンの場合、時間経過でSAN値が減少します")]
    [SerializeField] private bool decreaseOverTime;

    [Tooltip("1分ごとに減るSAN値。5なら約20分で100から0になります")]
    [SerializeField, Min(0f)] private float sanityDecreasePerMinute = 0f;

    [Header("SAN値の段階")]
    [Tooltip("この割合以下でWarningになります")]
    [SerializeField, Range(0f, 1f)] private float warningThreshold = 0.7f;

    [Tooltip("この割合以下でLowになります")]
    [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.4f;

    [Tooltip("この割合以下でCriticalになります")]
    [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.15f;

    public float CurrentSanity => currentSanity;
    public float MaxSanity => maxSanity;

    public float SanityPercent =>
        maxSanity <= 0f
            ? 0f
            : currentSanity / maxSanity;

    public bool IsSanityFull => currentSanity >= maxSanity - 0.01f;
    public bool IsSanityEmpty => currentSanity <= 0.01f;

    public SanityState CurrentState => currentState;

    // UI・敵スポーナー・画面効果などから購読します
    public event Action<float, float> SanityChanged;
    public event Action<SanityState> SanityStateChanged;

    private float currentSanity;
    private SanityState currentState;

    private void Awake()
    {
        FindPlayerHealth();

        currentSanity = startWithFullSanity
            ? maxSanity
            : Mathf.Clamp(startingSanity, 0f, maxSanity);

        currentState = GetState(currentSanity, maxSanity);
    }

    private void Start()
    {
        NotifyAll();
    }

    private void Update()
    {
        if (!decreaseOverTime || sanityDecreasePerMinute <= 0f)
        {
            return;
        }

        if (FindPlayerHealth() && playerHealth.IsDead)
        {
            return;
        }

        float decreaseAmount =
            sanityDecreasePerMinute / 60f * Time.deltaTime;

        DrainSanity(decreaseAmount);
    }

    /// <summary>
    /// SAN値を減らし、実際に減った量を返します。
    /// </summary>
    public float DrainSanity(float amount)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        float previousSanity = currentSanity;
        SetSanityInternal(currentSanity - amount, true);

        return previousSanity - currentSanity;
    }

    /// <summary>
    /// SAN値を回復し、実際に回復した量を返します。
    /// </summary>
    public float RestoreSanity(float amount)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        float previousSanity = currentSanity;
        SetSanityInternal(currentSanity + amount, true);

        return currentSanity - previousSanity;
    }

    /// <summary>
    /// SAN値を直接設定します。演出イベントは通常どおり発火します。
    /// </summary>
    public void SetSanity(float value)
    {
        SetSanityInternal(value, true);
    }

    [ContextMenu("Refill SAN")]
    public void RefillSanity()
    {
        SetSanityInternal(maxSanity, true);
    }

    [ContextMenu("Drain SAN By 10")]
    public void DrainSanityByTen()
    {
        DrainSanity(10f);
    }

    [ContextMenu("Empty SAN")]
    public void EmptySanity()
    {
        SetSanityInternal(0f, true);
    }

    private void SetSanityInternal(float value, bool notify)
    {
        float previousSanity = currentSanity;
        SanityState previousState = currentState;

        currentSanity = Mathf.Clamp(value, 0f, maxSanity);
        currentState = GetState(currentSanity, maxSanity);

        if (notify && !Mathf.Approximately(previousSanity, currentSanity))
        {
            SanityChanged?.Invoke(currentSanity, maxSanity);
        }

        if (notify && previousState != currentState)
        {
            SanityStateChanged?.Invoke(currentState);
        }
    }

    private SanityState GetState(float currentValue, float maximumValue)
    {
        if (maximumValue <= 0f || currentValue <= 0.01f)
        {
            return SanityState.Empty;
        }

        float percent = currentValue / maximumValue;

        if (percent <= criticalThreshold)
        {
            return SanityState.Critical;
        }

        if (percent <= lowThreshold)
        {
            return SanityState.Low;
        }

        if (percent <= warningThreshold)
        {
            return SanityState.Warning;
        }

        return SanityState.Normal;
    }

    private void NotifyAll()
    {
        SanityChanged?.Invoke(currentSanity, maxSanity);
        SanityStateChanged?.Invoke(currentState);
    }

    private bool FindPlayerHealth()
    {
        if (playerHealth != null)
        {
            return true;
        }

        playerHealth = GetComponent<CharacterHealth>();
        return playerHealth != null;
    }

    private void OnValidate()
    {
        maxSanity = Mathf.Max(1f, maxSanity);
        startingSanity = Mathf.Clamp(startingSanity, 0f, maxSanity);
        sanityDecreasePerMinute = Mathf.Max(0f, sanityDecreasePerMinute);

        warningThreshold = Mathf.Clamp01(warningThreshold);
        lowThreshold = Mathf.Clamp(lowThreshold, 0f, warningThreshold);
        criticalThreshold = Mathf.Clamp(criticalThreshold, 0f, lowThreshold);
    }
}
