using UnityEngine;

/// <summary>
/// 飲酒1本ごとに効果時間を2分に更新。重量・運搬のバフと空腹のデバフは同時に有効。
/// 通常の飲酒効果はHUD1アイコン。飲み過ぎ中だけ、別のデバフアイコンを60秒表示する。
/// InventoryController.TryUseItemからTryDrinkを呼び、消費処理はInventoryControllerが行う。
/// </summary>
[DisallowMultipleComponent]
public class PlayerAlcoholEffect2D : MonoBehaviour
{
    [Header("参照（通常は自動取得）")]
    [SerializeField] private CharacterHealth characterHealth;
    [SerializeField] private PlayerWeightController weightController;
    [SerializeField] private PlayerSurvivalController survivalController;
    [SerializeField] private StatusEffectManager2D statusEffectManager;

    [Header("効果時間")]
    [Tooltip("飲んだ後にバフと空腹デバフが続く時間（秒）。追加で飲むと120秒から再開始。")]
    [SerializeField, Min(1f)] private float effectDurationSeconds = 120f;

    [Header("蓄積値・飲み過ぎ")]
    [Tooltip("150以上になると3杯×50で飲み過ぎを発動。")]
    [SerializeField, Min(1f)] private float alcoholTolerance = 150f;
    [Tooltip("1秒あたりのアルコール蓄積値の減衰量。次の飲酒がある場合の飲み過ぎ判定に使用。")]
    [SerializeField, Min(0.01f)] private float alcoholRecoveryPerSecond = 0.5f;
    [Tooltip("飲酒直後のこの秒数は蓄積値が減らない。短時間の3杯で150に到達させるため。")]
    [SerializeField, Min(0f)] private float recoveryDelayAfterDrinkSeconds = 12f;
    [SerializeField, Min(1f)] private float maximumAlcoholPoints = 300f;
    [SerializeField, Min(1f)] private float overLimitShakeSeconds = 60f;

    [Header("飲み過ぎ：目で分かる大きなゆっくり横揺れ")]
    [Tooltip("横揺れ幅（ワールド単位）。v3の小さな初期値0.045とは別フィールド。推奨0.55。強すぎれば0.35程度。")]
    [SerializeField, Range(0f, 2f)] private float overdoseSwayHorizontalAmplitude = 0.55f;
    [Tooltip("1秒あたりの往復回数。0.20なら約5秒で1往復。")]
    [SerializeField, Range(0.05f, 0.8f)] private float swayCyclesPerSecond = 0.20f;
    [Tooltip("最大傾斜角。目に見えるがガタガタしない初期値2.5度。")]
    [SerializeField, Range(0f, 8f)] private float overdoseSwayRollDegrees = 2.5f;

    // v3で保存済みの弱い揺れ設定値はこの2フィールドには復元されない。
    // フィールド名変更によりv4の視認可能な初期値が既存Prefabでも使われる。

    [Header("右上HUD（通常1アイコン＋飲み過ぎ中のみ別デバフ）")]
    [Tooltip("Tools > Alcohol > v3 を接続 で自動生成。空ならResourcesから自動読込。")]
    [SerializeField] private StatusEffectData2D combinedAlcoholEffect;

    // v2のInspector参照を保持。重量/空腹の旧アイコンは除去し、飲み過ぎのDataは再利用する。
    [SerializeField, HideInInspector] private StatusEffectData2D weightAndCarryBuff;
    [SerializeField, HideInInspector] private StatusEffectData2D hungerDebuff;
    [SerializeField, HideInInspector] private StatusEffectData2D overLimitDebuff;

    [Header("診断")]
    [SerializeField] private bool showDebugLogs = true;

    [System.Serializable]
    private sealed class DescriptionOverwrite
    {
        public string description;
    }

    private float alcoholAmount;
    private float effectRemainingSeconds;
    private float overLimitRemainingSeconds;
    private float lastDrinkTime = float.NegativeInfinity;
    private float currentWeightBonusKg;
    private float currentRescueCarrySpeedMultiplier = 1f;
    private float currentFoodDrainMultiplier = 1f;
    private bool warnedMissingHud;
    private bool warnedMissingEffect;
    private StatusEffectData2D runtimeCombinedEffect;
    private StatusEffectData2D runtimeOverLimitEffect;
    private StatusEffectManager2D registeredManager;
    private AlcoholCameraShake2D shake;
    private float nextDetailRefreshTime;

    public float CurrentAlcoholAmount => alcoholAmount;
    public float AlcoholTolerance => alcoholTolerance;
    public float EffectRemainingSeconds => effectRemainingSeconds;
    public float OverLimitRemainingSeconds => overLimitRemainingSeconds;
    public bool IsOverLimit => overLimitRemainingSeconds > 0f;
    public bool HasAlcoholEffect => effectRemainingSeconds > 0f;
    public float CurrentWeightBonusKg => HasAlcoholEffect ? currentWeightBonusKg : 0f;
    public float CurrentRescueCarrySpeedMultiplier => HasAlcoholEffect ? currentRescueCarrySpeedMultiplier : 1f;
    public float CurrentFoodDrainMultiplier => HasAlcoholEffect ? currentFoodDrainMultiplier : 1f;

    private void Awake()
    {
        ResolveReferences();
        if (weightController != null)
            weightController.SendMessage("SetAlcoholMoveSpeedMultiplier", 1f,
                SendMessageOptions.DontRequireReceiver);
    }

    private void OnEnable()
    {
        ResolveReferences();
        SyncHud(false);
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        bool wasOverLimit = IsOverLimit;

        if (HasAlcoholEffect)
        {
            effectRemainingSeconds = Mathf.Max(0f, effectRemainingSeconds - dt);
            if (Time.time - lastDrinkTime >= recoveryDelayAfterDrinkSeconds)
                alcoholAmount = Mathf.Max(0f, alcoholAmount - alcoholRecoveryPerSecond * dt);

            if (!HasAlcoholEffect)
            {
                alcoholAmount = 0f;
                currentWeightBonusKg = 0f;
                currentRescueCarrySpeedMultiplier = 1f;
                currentFoodDrainMultiplier = 1f;
                RefreshWeight();
                Log("飲酒効果120秒終了。重量・NPC運搬バフと空腹デバフを解除しました。");
            }
        }

        // 飲み過ぎデバフは通常飲酒バフから独立した60秒タイマー。
        if (wasOverLimit)
            overLimitRemainingSeconds = Mathf.Max(0f, overLimitRemainingSeconds - dt);

        if (wasOverLimit && !IsOverLimit)
        {
            SyncHud(false);
            Log("飲み過ぎデバフ60秒終了。専用HUDアイコンを解除しました。");
        }
        else if (!HasAlcoholEffect)
        {
            // 通常効果が切れても飲み過ぎの時間だけ残りうるため、専用アイコンは保持。
            SyncHud(false);
        }

        if (!HasAlcoholEffect && !IsOverLimit) return;

        // Managerが遅れて生成／切り替わった場合、片方だけHUDから消えた場合に修復。
        if (statusEffectManager == null ||
            (HasAlcoholEffect && runtimeCombinedEffect != null &&
             !statusEffectManager.HasEffect(runtimeCombinedEffect)) ||
            (IsOverLimit && runtimeOverLimitEffect != null &&
             !statusEffectManager.HasEffect(runtimeOverLimitEffect)))
            SyncHud(false);

        if (Time.unscaledTime >= nextDetailRefreshTime)
        {
            nextDetailRefreshTime = Time.unscaledTime + 1f;
            UpdateRuntimeDescriptions();
        }
    }

    public bool TryDrink(AlcoholItemData alcohol, out string reason)
    {
        reason = string.Empty;
        if (alcohol == null) { reason = "AlcoholItemDataがありません。"; return false; }
        ResolveReferences();
        if (characterHealth == null || characterHealth.IsDead)
        { reason = "PlayerのCharacterHealthが見つからない、または死亡しています。"; return false; }
        if (weightController == null || survivalController == null)
        { reason = "PlayerWeightControllerまたはPlayerSurvivalControllerが見つかりません。"; return false; }
        if (alcohol.AlcoholPoints <= 0f)
        { reason = "Alcohol Pointsが0以下です。"; return false; }

        float before = alcoholAmount;
        alcoholAmount = Mathf.Min(maximumAlcoholPoints, alcoholAmount + alcohol.AlcoholPoints);
        lastDrinkTime = Time.time;
        effectRemainingSeconds = effectDurationSeconds;

        // 異なる酒を続けて飲んだ時は強い方の効果を採用。スタック加算はしない。
        currentWeightBonusKg = Mathf.Max(currentWeightBonusKg, alcohol.WeightLimitBonusKg);
        currentRescueCarrySpeedMultiplier = Mathf.Max(currentRescueCarrySpeedMultiplier,
            alcohol.RescueCarrySpeedMultiplier);
        currentFoodDrainMultiplier = Mathf.Max(currentFoodDrainMultiplier,
            alcohol.FoodDrainMultiplier);

        if (before < alcoholTolerance && alcoholAmount >= alcoholTolerance)
        {
            overLimitRemainingSeconds = overLimitShakeSeconds;
            StartOverLimitSway();
            Log($"飲み過ぎ発動 / 蓄積 {before:0.#}→{alcoholAmount:0.#}/{alcoholTolerance:0.#} / " +
                $"横揺れ={overdoseSwayHorizontalAmplitude:0.##}m / 傾斜={overdoseSwayRollDegrees:0.#}° / {overLimitShakeSeconds:0}秒");
        }

        RefreshWeight();
        SyncHud(true); // 通常は1アイコン。閾値到達中だけ飲み過ぎデバフを追加。
        Log($"飲酒成功 {alcohol.DisplayName} / {effectDurationSeconds:0}秒 / 酒量 {before:0.#}→{alcoholAmount:0.#} / 重量+{CurrentWeightBonusKg:0.#}kg / NPC運搬x{CurrentRescueCarrySpeedMultiplier:0.##} / 空腹x{CurrentFoodDrainMultiplier:0.##}");
        return true;
    }

    private void StartOverLimitSway()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            LogWarning("MainCameraが見つからないため画面の揺らぎを開始できません。Tagを確認してください。");
            return;
        }
        shake = cam.GetComponent<AlcoholCameraShake2D>();
        if (shake == null) shake = cam.gameObject.AddComponent<AlcoholCameraShake2D>();
        shake.Sway(overLimitShakeSeconds, overdoseSwayHorizontalAmplitude,
            swayCyclesPerSecond, overdoseSwayRollDegrees);
        Log($"画面ゆらゆら開始: Camera={cam.name} / {overLimitShakeSeconds:0}秒 / " +
            $"横幅={overdoseSwayHorizontalAmplitude:0.##}ワールド / 傾斜={overdoseSwayRollDegrees:0.#}度");
    }

    private void ResolveReferences()
    {
        if (characterHealth == null) characterHealth = GetComponent<CharacterHealth>();
        if (weightController == null) weightController = GetComponent<PlayerWeightController>();
        if (survivalController == null) survivalController = GetComponent<PlayerSurvivalController>();
        if (statusEffectManager == null) statusEffectManager = GetComponent<StatusEffectManager2D>();
        if (statusEffectManager == null) statusEffectManager = GetComponentInChildren<StatusEffectManager2D>(true);
        if (statusEffectManager == null)
            statusEffectManager = FindAnyObjectByType<StatusEffectManager2D>(FindObjectsInactive.Include);
        if (combinedAlcoholEffect == null)
            combinedAlcoholEffect = Resources.Load<StatusEffectData2D>("AlcoholEffects/AlcoholCombinedEffect");
        if (weightAndCarryBuff == null)
            weightAndCarryBuff = Resources.Load<StatusEffectData2D>("AlcoholEffects/AlcoholWeightBuff");
        if (hungerDebuff == null)
            hungerDebuff = Resources.Load<StatusEffectData2D>("AlcoholEffects/AlcoholHungerDebuff");
        if (overLimitDebuff == null)
            overLimitDebuff = Resources.Load<StatusEffectData2D>("AlcoholEffects/AlcoholOverdoseDebuff");
    }

    private StatusEffectData2D GetRuntimeCombinedEffect()
    {
        if (runtimeCombinedEffect != null) return runtimeCombinedEffect;
        if (combinedAlcoholEffect == null) return null;
        runtimeCombinedEffect = Instantiate(combinedAlcoholEffect);
        runtimeCombinedEffect.name = "AlcoholCombinedEffect_Runtime";
        runtimeCombinedEffect.hideFlags = HideFlags.DontSave;
        UpdateRuntimeDescriptions();
        return runtimeCombinedEffect;
    }

    private StatusEffectData2D GetRuntimeOverLimitEffect()
    {
        if (runtimeOverLimitEffect != null) return runtimeOverLimitEffect;
        if (overLimitDebuff == null) return null;
        runtimeOverLimitEffect = Instantiate(overLimitDebuff);
        runtimeOverLimitEffect.name = "AlcoholOverdoseDebuff_Runtime";
        runtimeOverLimitEffect.hideFlags = HideFlags.DontSave;
        UpdateRuntimeDescriptions();
        return runtimeOverLimitEffect;
    }

    private void UpdateRuntimeDescriptions()
    {
        if (runtimeCombinedEffect != null)
        {
            string details =
                "【バフ】\n" +
                $"・所持重量制限 +{CurrentWeightBonusKg:0.#} kg\n" +
                $"・NPC運搬速度 ×{CurrentRescueCarrySpeedMultiplier:0.##}（通常速度が上限）\n" +
                "【デバフ】\n" +
                $"・空腹の減少速度 ×{CurrentFoodDrainMultiplier:0.##}\n" +
                $"・アルコール蓄積 {alcoholAmount:0.#}/{alcoholTolerance:0.#}\n" +
                $"飲酒効果 残り {Mathf.CeilToInt(effectRemainingSeconds)}秒";
            JsonUtility.FromJsonOverwrite(
                JsonUtility.ToJson(new DescriptionOverwrite { description = details }), runtimeCombinedEffect);
        }
        if (runtimeOverLimitEffect != null)
        {
            string details =
                "【飲みすぎ（デバフ）】\n" +
                "・酔って画面が大きくゆっくり左右に揺れる\n" +
                $"・横揺れ幅 {overdoseSwayHorizontalAmplitude:0.##} ワールド\n" +
                $"・画面揺れ 残り {Mathf.CeilToInt(overLimitRemainingSeconds)}秒";
            JsonUtility.FromJsonOverwrite(
                JsonUtility.ToJson(new DescriptionOverwrite { description = details }), runtimeOverLimitEffect);
        }
        // SOは実行時の複製だけ変更。Project内の元アセットは書き換えない。
    }

    private void SyncHud(bool refreshTimer)
    {
        ResolveReferences();
        if (statusEffectManager == null)
        {
            if (!warnedMissingHud)
            {
                warnedMissingHud = true;
                LogWarning("StatusEffectManager2Dが見つかりません。右上HUDが表示されません。");
            }
            return;
        }

        // 古いManagerに以前登録したアイコンが残らないようにする。
        if (registeredManager != null && registeredManager != statusEffectManager)
        {
            if (runtimeCombinedEffect != null) registeredManager.RemoveEffect(runtimeCombinedEffect);
            if (runtimeOverLimitEffect != null) registeredManager.RemoveEffect(runtimeOverLimitEffect);
        }

        // v2の旧重量バフと旧空腹デバフは統合済み。元SOを直接表示しない。
        if (weightAndCarryBuff != null) statusEffectManager.RemoveEffect(weightAndCarryBuff);
        if (hungerDebuff != null) statusEffectManager.RemoveEffect(hungerDebuff);
        // v2時代の飲み過ぎSOの直登録があれば取り除き、専用Runtimeコピーへ置き換える。
        if (overLimitDebuff != null) statusEffectManager.RemoveEffect(overLimitDebuff);

        // 通常の飲酒：1つにまとめたHUD（バフ／空腹デバフは説明欄で見られる）。
        if (HasAlcoholEffect)
        {
            StatusEffectData2D data = GetRuntimeCombinedEffect();
            if (data == null)
            {
                if (!warnedMissingEffect)
                {
                    warnedMissingEffect = true;
                    LogWarning("飲酒効果SOが見つかりません。Tools > Alcohol > お酒システム v4 を接続 を実行してください。");
                }
            }
            else if (refreshTimer || !statusEffectManager.HasEffect(data) || registeredManager != statusEffectManager)
                statusEffectManager.ApplyEffect(data, effectRemainingSeconds);
        }
        else
        {
            if (runtimeCombinedEffect != null) statusEffectManager.RemoveEffect(runtimeCombinedEffect);
            if (combinedAlcoholEffect != null) statusEffectManager.RemoveEffect(combinedAlcoholEffect);
        }

        // 飲み過ぎ60秒の間だけ、通常の飲酒アイコンとは独立したデバフを表示。
        if (IsOverLimit)
        {
            StatusEffectData2D overdose = GetRuntimeOverLimitEffect();
            if (overdose == null)
                LogWarning("飲みすぎデバフSOが見つかりません。Tools > Alcohol > お酒システム v4 を接続 を実行してください。");
            else if (refreshTimer || !statusEffectManager.HasEffect(overdose) || registeredManager != statusEffectManager)
            {
                statusEffectManager.ApplyEffect(overdose, overLimitRemainingSeconds);
                Log($"右上HUD: 飲みすぎデバフを追加（残り{overLimitRemainingSeconds:0}秒）");
            }
        }
        else if (runtimeOverLimitEffect != null)
            statusEffectManager.RemoveEffect(runtimeOverLimitEffect);

        registeredManager = statusEffectManager;
        UpdateRuntimeDescriptions();
    }

    private void RefreshWeight()
    {
        if (weightController != null) weightController.RecalculateWeight();
    }

    private void OnDestroy()
    {
        if (statusEffectManager != null)
        {
            if (runtimeCombinedEffect != null) statusEffectManager.RemoveEffect(runtimeCombinedEffect);
            if (runtimeOverLimitEffect != null) statusEffectManager.RemoveEffect(runtimeOverLimitEffect);
        }
        if (runtimeCombinedEffect != null) Destroy(runtimeCombinedEffect);
        if (runtimeOverLimitEffect != null) Destroy(runtimeOverLimitEffect);
    }

    [ContextMenu("診断 / 飲酒状態とHUDをConsole表示")]
    private void Diagnose()
    {
        ResolveReferences();
        Debug.Log($"[Alcohol v4 診断] 効果残り={effectRemainingSeconds:0.#}秒 / 酒量={alcoholAmount:0.#}/{alcoholTolerance:0.#} / 重量+{CurrentWeightBonusKg:0.#}kg / NPC運搬x{CurrentRescueCarrySpeedMultiplier:0.##} / 空腹x{CurrentFoodDrainMultiplier:0.##} / 飲みすぎ残り={overLimitRemainingSeconds:0.#}秒 / Camera揺れ残り={(shake != null ? shake.RemainingSeconds : 0f):0.#}秒 / Camera名={(shake != null ? shake.name : "未設定")} / HUD={(statusEffectManager != null ? "あり" : "なし")} / 統合Data={(combinedAlcoholEffect != null ? "あり" : "なし")} / アイコン数={(statusEffectManager != null ? statusEffectManager.ActiveEffects.Count : 0)} / 飲み過ぎアイコン={(statusEffectManager != null && runtimeOverLimitEffect != null && statusEffectManager.HasEffect(runtimeOverLimitEffect) ? "表示中" : "なし")}", this);
    }

    private void Log(string message) { if (showDebugLogs) Debug.Log("[Alcohol] " + message, this); }
    private void LogWarning(string message) { Debug.LogWarning("[Alcohol] " + message, this); }
}
