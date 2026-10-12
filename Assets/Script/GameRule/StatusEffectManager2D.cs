using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>現在有効な1つの効果。DurationSecondsが0なら治療・解除まで継続。</summary>
public sealed class ActiveStatusEffect2D
{
    public StatusEffectData2D Data { get; private set; }
    public float DurationSeconds { get; private set; }
    public float RemainingSeconds { get; private set; }
    internal bool OwnsLinkedCondition { get; set; }

    public bool IsTimed => DurationSeconds > 0f;
    public float Remaining01 => IsTimed
        ? Mathf.Clamp01(RemainingSeconds / DurationSeconds)
        : 1f;

    internal ActiveStatusEffect2D(StatusEffectData2D data, float duration)
    {
        Data = data;
        Refresh(duration);
    }

    internal void Refresh(float duration)
    {
        DurationSeconds = Mathf.Max(0f, duration);
        RemainingSeconds = DurationSeconds;
    }

    internal void Tick(float deltaTime)
    {
        if (IsTimed)
            RemainingSeconds = Mathf.Max(0f, RemainingSeconds - deltaTime);
    }
}

/// <summary>
/// Playerの状態効果とHUDを結びつけます。
/// PlayerStatusConditionControllerの骨折等を自動同期。
/// 登録漏れのConditionは暫定アイコン（!）として表示します。
/// </summary>
[DisallowMultipleComponent]
public class StatusEffectManager2D : MonoBehaviour
{
    [Header("既存状態異常への接続（PlayerのController）")]
    [SerializeField] private PlayerStatusConditionController conditionController;

    [Header("表示用データ（骨折ならLinked Condition=Fracture）")]
    [SerializeField] private List<StatusEffectData2D> registeredEffects =
        new List<StatusEffectData2D>();

    [Header("設定漏れ対策")]
    [Tooltip("状態異常のデータ登録が漏れていても仮アイコンで知らせます。正式なアイコンを設定したら自動でそちらを使います。")]
    [SerializeField] private bool showFallbackForUnregisteredConditions = true;
    [SerializeField] private bool showDiagnosticWarnings = true;

    private static readonly StatusConditionType[] SupportedConditions =
    {
        StatusConditionType.Bleeding,
        StatusConditionType.Fracture,
        StatusConditionType.Poison,
        StatusConditionType.Burn
    };

    private readonly List<ActiveStatusEffect2D> activeEffects =
        new List<ActiveStatusEffect2D>();
    private readonly Dictionary<StatusConditionType, StatusEffectData2D> fallbackData =
        new Dictionary<StatusConditionType, StatusEffectData2D>();
    private readonly HashSet<StatusConditionType> warnedAboutFallbacks =
        new HashSet<StatusConditionType>();

    private PlayerStatusConditionController subscribedController;
    private float nextReferenceCheckTime;
    private StatusEffectData2D uiTestData;
    private int testIndex;

    public IReadOnlyList<ActiveStatusEffect2D> ActiveEffects => activeEffects;
    public event Action EffectsChanged;

    private void Awake()
    {
        ResolveConditionController();
    }

    private void OnEnable()
    {
        ConnectConditionController();
    }

    private void Start()
    {
        ConnectConditionController();
        if (subscribedController == null && showDiagnosticWarnings)
            Debug.LogWarning(
                "[StatusEffectManager2D] PlayerStatusConditionControllerが見つかりません。" +
                " ManagerのCondition Controller欄にPlayerのコンポーネントを指定してください。", this);
        else
            SyncFromConditions();
    }

    private void OnDisable()
    {
        DisconnectConditionController();
    }

    private void OnDestroy()
    {
        foreach (var entry in fallbackData)
            if (entry.Value != null) Destroy(entry.Value);
        fallbackData.Clear();
        if (uiTestData != null) Destroy(uiTestData);
    }

    private void Update()
    {
        // Playerが後から生成されるシーンでも自動接続する。
        if (Time.unscaledTime >= nextReferenceCheckTime)
        {
            nextReferenceCheckTime = Time.unscaledTime + 1f;
            if (conditionController == null || conditionController != subscribedController)
                ConnectConditionController();
            // 既存ControllerがAddConditionsを使用していればイベントだけで即時反映。
            // スクリプト有効化順などによる取りこぼしはここでも回収する。
            SyncFromConditions();
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        for (int i = activeEffects.Count - 1; i >= 0; i--)
        {
            ActiveStatusEffect2D effect = activeEffects[i];
            if (!effect.IsTimed) continue;
            effect.Tick(dt);
            if (effect.RemainingSeconds > 0f) continue;

            bool shouldCure = effect.OwnsLinkedCondition;
            StatusConditionType linked = effect.Data.LinkedCondition;
            RemoveAt(i);
            if (shouldCure && conditionController != null &&
                linked != StatusConditionType.None)
                conditionController.RemoveConditions(linked);
        }
    }

    public bool ApplyEffect(StatusEffectData2D data, float durationOverride = -1f)
    {
        if (data == null) return false;
        float duration = Mathf.Max(0f, durationOverride >= 0f
            ? durationOverride : data.DurationSeconds);

        ActiveStatusEffect2D existing = FindActive(data);
        bool addedCondition = false;
        bool externallyOwned = false;
        if (data.LinkedCondition != StatusConditionType.None)
        {
            ConnectConditionController();
            if (conditionController != null)
            {
                bool hasCondition = conditionController.HasCondition(data.LinkedCondition);
                externallyOwned = hasCondition &&
                    (existing == null || !existing.OwnsLinkedCondition);
                if (!hasCondition)
                    addedCondition = conditionController.AddConditions(data.LinkedCondition);
            }
        }
        if (externallyOwned) duration = 0f;

        // AddConditions発火時にSyncFromConditions側で既に生成された場合がある。
        existing = FindActive(data);
        if (existing != null)
        {
            existing.Refresh(duration);
            existing.OwnsLinkedCondition |= addedCondition;
            EffectsChanged?.Invoke();
            return true;
        }

        var effect = new ActiveStatusEffect2D(data, duration);
        effect.OwnsLinkedCondition = addedCondition;
        activeEffects.Insert(0, effect);
        EffectsChanged?.Invoke();
        return true;
    }

    public bool RemoveEffect(StatusEffectData2D data)
    {
        if (data == null) return false;
        int index = activeEffects.FindIndex(e => e.Data == data);
        if (index < 0) return false;
        RemoveAt(index);
        if (data.LinkedCondition != StatusConditionType.None && conditionController != null)
            conditionController.RemoveConditions(data.LinkedCondition);
        return true;
    }

    public bool HasEffect(StatusEffectData2D data) => FindActive(data) != null;

    private ActiveStatusEffect2D FindActive(StatusEffectData2D data)
    {
        return activeEffects.Find(e => e.Data == data);
    }

    private void RemoveAt(int index)
    {
        if (index < 0 || index >= activeEffects.Count) return;
        activeEffects.RemoveAt(index);
        EffectsChanged?.Invoke();
    }

    private void OnConditionsChanged(StatusConditionType conditions)
    {
        SyncFromConditions();
    }

    /// <summary>
    /// 既存の骨折・出血・毒・火傷をHUDの表示効果へ同期します。
    /// 未登録の一時表示データも解除時に必ず取り除きます。
    /// </summary>
    private void SyncFromConditions()
    {
        if (conditionController == null) return;

        foreach (StatusConditionType condition in SupportedConditions)
        {
            bool present = conditionController.HasCondition(condition);
            StatusEffectData2D configured = FindConfiguredData(condition);
            fallbackData.TryGetValue(condition, out StatusEffectData2D fallback);

            if (!present)
            {
                // 治療後は正式アイコンだけでなく、未登録時の仮アイコンも消す。
                RemoveActiveData(configured);
                RemoveActiveData(fallback);
                continue;
            }

            if (configured != null)
            {
                // 後から正式Dataを登録した場合、仮アイコンと二重表示しない。
                RemoveActiveData(fallback);
            }
            else if (!showFallbackForUnregisteredConditions)
            {
                // 仮表示を無効化した場合は、既存の仮アイコンも消す。
                RemoveActiveData(fallback);
                continue;
            }

            StatusEffectData2D data = configured;
            if (data == null)
            {
                data = GetFallbackData(condition);
                if (showDiagnosticWarnings && warnedAboutFallbacks.Add(condition))
                {
                    Debug.LogWarning(
                        $"[StatusEffectManager2D] {condition} の表示Dataが未登録です。" +
                        " 仮アイコンを表示します。Linked ConditionとRegistered Effectsを確認してください。",
                        this);
                }
            }

            if (FindActive(data) != null) continue;

            // 外部で付与された骨折等は、治療されるまで継続する（タイマー無し）。
            activeEffects.Insert(0, new ActiveStatusEffect2D(data, 0f));
            EffectsChanged?.Invoke();
        }
    }

    private void RemoveActiveData(StatusEffectData2D data)
    {
        if (data == null) return;
        int index = activeEffects.FindIndex(effect => effect.Data == data);
        if (index >= 0) RemoveAt(index);
    }

    private StatusEffectData2D FindConfiguredData(StatusConditionType condition)
    {
        if (registeredEffects == null) return null;
        foreach (StatusEffectData2D data in registeredEffects)
            if (data != null && data.LinkedCondition == condition)
                return data;
        return null;
    }

    private StatusEffectData2D GetFallbackData(StatusConditionType condition)
    {
        if (fallbackData.TryGetValue(condition, out StatusEffectData2D data) && data != null)
            return data;

        data = ScriptableObject.CreateInstance<StatusEffectData2D>();
        data.hideFlags = HideFlags.HideAndDontSave;
        string label = condition == StatusConditionType.Fracture ? "骨折" :
            condition == StatusConditionType.Bleeding ? "出血" :
            condition == StatusConditionType.Poison ? "毒" : "火傷";
        data.SetRuntimeFallback(label, "状態異常が発生しています。", condition);
        fallbackData[condition] = data;
        return data;
    }

    private void ResolveConditionController()
    {
        if (conditionController != null) return;
        conditionController = GetComponent<PlayerStatusConditionController>();
        if (conditionController == null)
            conditionController = GetComponentInParent<PlayerStatusConditionController>();
        if (conditionController == null)
            conditionController = GetComponentInChildren<PlayerStatusConditionController>(true);
        if (conditionController != null) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            conditionController = player.GetComponent<PlayerStatusConditionController>();
            if (conditionController == null)
                conditionController = player.GetComponentInChildren<PlayerStatusConditionController>(true);
        }
    }

    private void ConnectConditionController()
    {
        ResolveConditionController();
        if (subscribedController == conditionController) return;

        DisconnectConditionController();
        if (conditionController == null) return;
        conditionController.ConditionsChanged += OnConditionsChanged;
        subscribedController = conditionController;
        SyncFromConditions();
    }

    private void DisconnectConditionController()
    {
        if (subscribedController != null)
            subscribedController.ConditionsChanged -= OnConditionsChanged;
        subscribedController = null;
    }

    [ContextMenu("診断 / 接続状態をConsoleに表示")]
    private void LogConnectionDiagnostics()
    {
        ConnectConditionController();
        Debug.Log(
            $"[StatusEffectManager2D 診断] " +
            $"Manager={name} / " +
            $"Controller={(conditionController == null ? "未接続" : conditionController.name)} / " +
            $"骨折={(conditionController != null && conditionController.IsFractured)} / " +
            $"登録数={(registeredEffects == null ? 0 : registeredEffects.Count)} / " +
            $"骨折Data={(FindConfiguredData(StatusConditionType.Fracture) == null ? "未登録" : FindConfiguredData(StatusConditionType.Fracture).name)} / " +
            $"表示中={activeEffects.Count}", this);
    }

    [ContextMenu("TEST / 骨折を付与")]
    private void TestAddFracture()
    {
        if (!Application.isPlaying) return;
        ConnectConditionController();
        if (conditionController == null)
        {
            Debug.LogError("[StatusEffectManager2D] 骨折テスト失敗: Condition Controllerが未接続です。", this);
            return;
        }
        conditionController.AddConditions(StatusConditionType.Fracture);
        SyncFromConditions();
        LogConnectionDiagnostics();
    }

    [ContextMenu("TEST / 骨折を解除")]
    private void TestRemoveFracture()
    {
        if (!Application.isPlaying || conditionController == null) return;
        conditionController.RemoveConditions(StatusConditionType.Fracture);
    }

    [ContextMenu("TEST / UI表示だけを10秒間テスト")]
    private void TestHudWithoutConditions()
    {
        if (!Application.isPlaying) return;
        if (uiTestData == null)
        {
            uiTestData = ScriptableObject.CreateInstance<StatusEffectData2D>();
            uiTestData.hideFlags = HideFlags.HideAndDontSave;
            uiTestData.SetRuntimeFallback("UIテスト", "10秒間だけ表示するテストアイコンです。",
                StatusConditionType.None);
        }
        ApplyEffect(uiTestData, 10f);
        Debug.Log("[StatusEffectManager2D] UIテストを発行しました。HUDにアイコンが出るか確認してください。", this);
    }

    [ContextMenu("TEST / 次の登録効果を15秒間付与")]
    private void TestApplyNextEffect()
    {
        if (!Application.isPlaying || registeredEffects == null || registeredEffects.Count == 0)
            return;
        for (int i = 0; i < registeredEffects.Count; i++)
        {
            StatusEffectData2D data = registeredEffects[(testIndex++) % registeredEffects.Count];
            if (data == null) continue;
            ApplyEffect(data, 15f);
            return;
        }
    }

    [ContextMenu("TEST / 上から3番目の効果を解除")]
    private void TestRemoveThirdEffect()
    {
        if (!Application.isPlaying || activeEffects.Count < 3) return;
        RemoveEffect(activeEffects[2].Data);
    }
}
