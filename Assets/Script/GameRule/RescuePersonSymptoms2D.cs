using System;
using UnityEngine;

/// <summary>
/// 救出NPC専用の症状管理。
/// RescuePersonInfo2D が管理する重症度（健康/軽傷/負傷/重傷/死亡）は変更しません。
/// 既存の ConsumableItemData.cs にある StatusConditionType を共通利用します。
///
/// 洞窟での処置 = StabilizeSymptoms（症状を維持して進行を抑える）
/// 町での本格治療 = CureSymptoms（症状そのものを解除する）
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class RescuePersonSymptoms2D : MonoBehaviour
{
    private const StatusConditionType SupportedSymptoms =
        StatusConditionType.Bleeding |
        StatusConditionType.Poison |
        StatusConditionType.Fracture;

    [Header("参照")]
    [SerializeField] private CharacterHealth characterHealth;

    [Header("開始時の症状（複数選択可）")]
    [SerializeField] private StatusConditionType initialSymptoms =
        StatusConditionType.None;
    [Tooltip("開始時から処置済みにする症状。Initial Symptomsに含まれる症状のみ有効です。")]
    [SerializeField] private StatusConditionType initiallyStabilized =
        StatusConditionType.None;

    [Header("スポーン時に症状をランダム付与（任意）")]
    [SerializeField] private bool useRandomSymptoms = false;
    [SerializeField, Range(0f, 100f)] private float bleedingChancePercent = 35f;
    [SerializeField, Range(0f, 100f)] private float poisonChancePercent = 15f;
    [SerializeField, Range(0f, 100f)] private float fractureChancePercent = 20f;

    [Header("出血：未処置中の継続ダメージ")]
    [SerializeField, Min(1)] private int bleedingDamagePerTick = 1;
    [SerializeField, Min(0.5f)] private float bleedingTickSeconds = 3f;

    [Header("毒：未処置中の継続ダメージ")]
    [SerializeField, Min(1)] private int poisonDamagePerTick = 1;
    [SerializeField, Min(0.5f)] private float poisonTickSeconds = 4f;
    [Tooltip("応急処置後は毒のダメージ間隔をこの倍率だけ延ばします（5なら5倍）。")]
    [SerializeField, Min(1f)] private float stabilizedPoisonIntervalMultiplier = 5f;

    [Header("診断")]
    [SerializeField] private bool showDebugLogs = false;

    public StatusConditionType ActiveSymptoms { get; private set; }
    public StatusConditionType StabilizedSymptoms { get; private set; }
    public bool HasAnySymptoms => ActiveSymptoms != StatusConditionType.None;
    public bool IsDead => characterHealth != null && characterHealth.IsDead;
    public CharacterHealth Health => characterHealth;

    /// <summary>引数：現在の症状 / 応急処置済みの症状</summary>
    public event Action<StatusConditionType, StatusConditionType> SymptomsChanged;
    public event Action<StatusConditionType> SymptomsAdded;
    public event Action<StatusConditionType> SymptomsCured;
    public event Action<StatusConditionType> SymptomsStabilized;

    private bool initialized;
    private float bleedingElapsed;
    private float poisonElapsed;

    private void Awake()
    {
        if (characterHealth == null)
            characterHealth = GetComponent<CharacterHealth>();

        InitializeOnce();
    }

    private void OnEnable()
    {
        if (characterHealth == null)
            characterHealth = GetComponent<CharacterHealth>();

        if (characterHealth != null)
        {
            characterHealth.Died -= HandleDied;
            characterHealth.Died += HandleDied;
        }
    }

    private void OnDisable()
    {
        if (characterHealth != null)
            characterHealth.Died -= HandleDied;
    }

    private void InitializeOnce()
    {
        if (initialized) return;
        initialized = true;

        ActiveSymptoms = initialSymptoms & SupportedSymptoms;
        StabilizedSymptoms = initiallyStabilized & ActiveSymptoms;

        if (useRandomSymptoms)
        {
            if (UnityEngine.Random.value * 100f < bleedingChancePercent)
                ActiveSymptoms |= StatusConditionType.Bleeding;
            if (UnityEngine.Random.value * 100f < poisonChancePercent)
                ActiveSymptoms |= StatusConditionType.Poison;
            if (UnityEngine.Random.value * 100f < fractureChancePercent)
                ActiveSymptoms |= StatusConditionType.Fracture;
        }

        Log("初期症状: " + GetSymptomsSummary());
        NotifyChanged();
    }

    private void Update()
    {
        if (!initialized || characterHealth == null || characterHealth.IsDead)
            return;

        if (!HasAnySymptoms) return;

        float delta = Time.deltaTime;
        if (delta <= 0f) return;

        // 1フレーム内の出血と毒ダメージを合算して一度だけ適用。
        // CharacterHealthの無敵時間で片方が意図せず消える問題を避けます。
        int damage = 0;

        if (HasSymptom(StatusConditionType.Bleeding) &&
            !IsStabilized(StatusConditionType.Bleeding))
        {
            bleedingElapsed += delta;
            damage += ConsumePeriodicDamage(
                ref bleedingElapsed, bleedingTickSeconds, bleedingDamagePerTick);
        }
        else
        {
            bleedingElapsed = 0f;
        }

        if (HasSymptom(StatusConditionType.Poison))
        {
            float interval = poisonTickSeconds;
            if (IsStabilized(StatusConditionType.Poison))
                interval *= stabilizedPoisonIntervalMultiplier;

            poisonElapsed += delta;
            damage += ConsumePeriodicDamage(
                ref poisonElapsed, interval, poisonDamagePerTick);
        }
        else
        {
            poisonElapsed = 0f;
        }

        // 骨折はこの段階では継続ダメージなし。
        // 搬送・落下ダメージとの連携は既存の落下処理を確認してから追加します。
        if (damage > 0)
        {
            int applied = Mathf.Min(damage, characterHealth.CurrentHealth);
            if (applied > 0)
            {
                characterHealth.TakeDamage(applied);
                Log("症状による継続ダメージ要求=" + applied +
                    ", HP=" + characterHealth.CurrentHealth);
            }
        }
    }

    private static int ConsumePeriodicDamage(ref float elapsed, float interval, int damagePerTick)
    {
        interval = Mathf.Max(0.5f, interval);
        if (elapsed < interval) return 0;

        int ticks = Mathf.FloorToInt(elapsed / interval);
        elapsed -= ticks * interval;
        return ticks * Mathf.Max(0, damagePerTick);
    }

    public bool HasSymptom(StatusConditionType symptom)
    {
        symptom &= SupportedSymptoms;
        return symptom != StatusConditionType.None &&
               (ActiveSymptoms & symptom) == symptom;
    }

    public bool IsStabilized(StatusConditionType symptom)
    {
        symptom &= SupportedSymptoms;
        return symptom != StatusConditionType.None &&
               (ActiveSymptoms & symptom) == symptom &&
               (StabilizedSymptoms & symptom) == symptom;
    }

    /// <summary>罠・攻撃・落下などから症状を追加。複数同時指定可能。</summary>
    public bool AddSymptoms(StatusConditionType symptoms)
    {
        InitializeOnce();
        if (IsDead) return false;

        StatusConditionType added = (symptoms & SupportedSymptoms) & ~ActiveSymptoms;
        if (added == StatusConditionType.None) return false;

        ActiveSymptoms |= added;
        if ((added & StatusConditionType.Bleeding) != 0) bleedingElapsed = 0f;
        if ((added & StatusConditionType.Poison) != 0) poisonElapsed = 0f;

        SymptomsAdded?.Invoke(added);
        NotifyChanged();
        Log("症状追加: " + added);
        return true;
    }

    /// <summary>
    /// 洞窟での応急処置。症状自体は残します。
    /// 出血は停止、毒はダメージ間隔延長、骨折は固定済み扱い。
    /// </summary>
    public bool StabilizeSymptoms(StatusConditionType symptoms)
    {
        InitializeOnce();
        if (IsDead) return false;

        StatusConditionType target =
            (symptoms & SupportedSymptoms) & ActiveSymptoms & ~StabilizedSymptoms;
        if (target == StatusConditionType.None) return false;

        StabilizedSymptoms |= target;
        if ((target & StatusConditionType.Bleeding) != 0) bleedingElapsed = 0f;
        if ((target & StatusConditionType.Poison) != 0) poisonElapsed = 0f;

        SymptomsStabilized?.Invoke(target);
        NotifyChanged();
        Log("応急処置完了: " + target);
        return true;
    }

    /// <summary>町での本格治療用。症状と処置済みフラグを解除。</summary>
    public bool CureSymptoms(StatusConditionType symptoms)
    {
        InitializeOnce();
        if (IsDead) return false;

        StatusConditionType removed = (symptoms & SupportedSymptoms) & ActiveSymptoms;
        if (removed == StatusConditionType.None) return false;

        ActiveSymptoms &= ~removed;
        StabilizedSymptoms &= ~removed;
        if ((removed & StatusConditionType.Bleeding) != 0) bleedingElapsed = 0f;
        if ((removed & StatusConditionType.Poison) != 0) poisonElapsed = 0f;

        SymptomsCured?.Invoke(removed);
        NotifyChanged();
        Log("完治: " + removed);
        return true;
    }

    public string GetSymptomsSummary()
    {
        if (!HasAnySymptoms) return "症状なし";
        string text = "";
        AppendName(StatusConditionType.Bleeding, "出血", ref text);
        AppendName(StatusConditionType.Poison, "毒", ref text);
        AppendName(StatusConditionType.Fracture, "骨折", ref text);
        return text;
    }

    private void AppendName(StatusConditionType flag, string label, ref string result)
    {
        if (!HasSymptom(flag)) return;
        if (result.Length > 0) result += " / ";
        result += label;
        if (IsStabilized(flag)) result += "（処置済み）";
    }

    /// <summary>セーブシステムで使用するためのスナップショット。</summary>
    public RescuePersonSymptomsSaveData2D GetSaveData()
    {
        InitializeOnce();
        return new RescuePersonSymptomsSaveData2D
        {
            activeSymptoms = ActiveSymptoms,
            stabilizedSymptoms = StabilizedSymptoms,
            bleedingElapsed = bleedingElapsed,
            poisonElapsed = poisonElapsed
        };
    }

    /// <summary>ロード後の状態復元。ランダム抽選の結果を上書きします。</summary>
    public void RestoreSaveData(RescuePersonSymptomsSaveData2D data)
    {
        InitializeOnce();
        ActiveSymptoms = data.activeSymptoms & SupportedSymptoms;
        StabilizedSymptoms = data.stabilizedSymptoms & ActiveSymptoms;
        bleedingElapsed = Mathf.Max(0f, data.bleedingElapsed);
        poisonElapsed = Mathf.Max(0f, data.poisonElapsed);
        NotifyChanged();
    }

    private void HandleDied()
    {
        // 死亡後も症状の記録は保持（将来の救出リザルト用）。
        // Update側で継続ダメージだけ停止します。
        NotifyChanged();
    }

    private void NotifyChanged()
    {
        SymptomsChanged?.Invoke(ActiveSymptoms, StabilizedSymptoms);
    }

    private void Log(string message)
    {
        if (showDebugLogs)
            Debug.Log("[RescuePersonSymptoms2D] " + name + " / " + message, this);
    }

    [ContextMenu("TEST / 出血を付与")]
    private void TestAddBleeding() { if (Application.isPlaying) AddSymptoms(StatusConditionType.Bleeding); }

    [ContextMenu("TEST / 毒を付与")]
    private void TestAddPoison() { if (Application.isPlaying) AddSymptoms(StatusConditionType.Poison); }

    [ContextMenu("TEST / 骨折を付与")]
    private void TestAddFracture() { if (Application.isPlaying) AddSymptoms(StatusConditionType.Fracture); }

    [ContextMenu("TEST / 3種類すべて付与")]
    private void TestAddAll() { if (Application.isPlaying) AddSymptoms(SupportedSymptoms); }

    [ContextMenu("TEST / 全症状を応急処置")]
    private void TestStabilizeAll() { if (Application.isPlaying) StabilizeSymptoms(SupportedSymptoms); }

    [ContextMenu("TEST / 全症状を完治")]
    private void TestCureAll() { if (Application.isPlaying) CureSymptoms(SupportedSymptoms); }

    [ContextMenu("TEST / 現在の症状をConsoleへ表示")]
    private void TestPrint()
    {
        if (!Application.isPlaying) return;
        Debug.Log("[RescuePersonSymptoms2D] " + name +
                  " / " + GetSymptomsSummary() +
                  " / HP=" + (characterHealth != null ? characterHealth.CurrentHealth.ToString() : "なし"), this);
    }

    private void OnValidate()
    {
        initialSymptoms &= SupportedSymptoms;
        initiallyStabilized &= SupportedSymptoms;
        bleedingTickSeconds = Mathf.Max(0.5f, bleedingTickSeconds);
        poisonTickSeconds = Mathf.Max(0.5f, poisonTickSeconds);
        stabilizedPoisonIntervalMultiplier = Mathf.Max(1f, stabilizedPoisonIntervalMultiplier);
        bleedingDamagePerTick = Mathf.Max(1, bleedingDamagePerTick);
        poisonDamagePerTick = Mathf.Max(1, poisonDamagePerTick);
    }
}

[Serializable]
public struct RescuePersonSymptomsSaveData2D
{
    public StatusConditionType activeSymptoms;
    public StatusConditionType stabilizedSymptoms;
    public float bleedingElapsed;
    public float poisonElapsed;
}
