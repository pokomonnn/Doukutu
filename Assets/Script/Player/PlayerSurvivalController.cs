using System;
using UnityEngine;

public enum SurvivalNeedState
{
    Normal,
    Warning,
    Low,
    Critical,
    Empty
}

[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class PlayerSurvivalController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら同じPlayerから自動取得します")]
    [SerializeField] private CharacterHealth playerHealth;

    [Header("最大値")]
    [SerializeField, Min(1f)] private float maxFood = 100f;

    [Header("開始時の値")]
    [Tooltip("オンなら、開始時に食料を最大値にします")]
    [SerializeField] private bool startWithFullValues = true;

    [SerializeField, Min(0f)] private float startingFood = 100f;

    [Header("時間経過による減少")]
    [Tooltip("1分ごとに減る食料値。1.67なら約60分で0になります")]
    [SerializeField, Min(0f)] private float foodDecreasePerMinute = 1.67f;

    [Header("残量による段階")]
    [Tooltip("この割合以下で警告状態になります")]
    [SerializeField, Range(0f, 1f)] private float warningThreshold = 0.6f;

    [Tooltip("この割合以下で低下状態になります")]
    [SerializeField, Range(0f, 1f)] private float lowThreshold = 0.3f;

    [Tooltip("この割合以下で危険状態になります")]
    [SerializeField, Range(0f, 1f)] private float criticalThreshold = 0.1f;

    [Header("食料不足：移動速度倍率")]
    [SerializeField, Range(0.05f, 1f)]
    private float foodLowMoveSpeedMultiplier = 0.9f;

    [SerializeField, Range(0.05f, 1f)]
    private float foodCriticalMoveSpeedMultiplier = 0.8f;

    [SerializeField, Range(0.05f, 1f)]
    private float foodEmptyMoveSpeedMultiplier = 0.7f;

    [Header("0になった時のダメージ")]
    [SerializeField] private bool enableDamageAtZero = true;

    [Tooltip("ダメージを受ける間隔（秒）")]
    [SerializeField, Min(0.1f)] private float emptyDamageInterval = 5f;

    [Tooltip("食料が0の時に受けるダメージ")]
    [SerializeField, Min(0)] private int foodEmptyDamage = 1;

    public float CurrentFood => currentFood;

    public float MaxFood => maxFood;

    public float FoodPercent =>
        maxFood <= 0f ? 0f : currentFood / maxFood;

    public bool IsFoodFull =>
        currentFood >= maxFood - 0.01f;

    public bool IsFoodEmpty => currentFood <= 0.01f;

    public SurvivalNeedState FoodState => foodState;

    // 後でPlayerWeightControllerから使う速度倍率
    public float FoodMoveSpeedMultiplier =>
        GetMoveSpeedMultiplier(
            foodState,
            foodLowMoveSpeedMultiplier,
            foodCriticalMoveSpeedMultiplier,
            foodEmptyMoveSpeedMultiplier
        );

    public float TotalMoveSpeedMultiplier =>
        FoodMoveSpeedMultiplier;

    // UI用イベント
    public event Action<float, float> FoodChanged;

    public event Action<SurvivalNeedState> FoodStateChanged;

    private float currentFood;

    private SurvivalNeedState foodState;

    private float emptyDamageTimer;

    private void Awake()
    {
        FindPlayerHealth();

        if (startWithFullValues)
        {
            currentFood = maxFood;
        }
        else
        {
            currentFood = Mathf.Clamp(
                startingFood,
                0f,
                maxFood
            );

        }

        foodState = GetState(currentFood, maxFood);
    }

    private void Start()
    {
        NotifyAll();
    }

    private void Update()
    {
        if (!FindPlayerHealth() || playerHealth.IsDead)
        {
            return;
        }

        float deltaTime = Time.deltaTime;

        if (deltaTime <= 0f)
        {
            return;
        }

        float foodDecrease =
            foodDecreasePerMinute / 60f * deltaTime *
            SkillCardEffectUtility.GetMultiplier(
                SkillEffectType.FoodDrain
            ) * GetAlcoholFoodDrainMultiplier();

        SetFoodInternal(currentFood - foodDecrease, true);

        UpdateEmptyDamage(deltaTime);
    }

    // [AlcoholSystem v2.0 / HungerDebuff]
    private float GetAlcoholFoodDrainMultiplier()
    {
        PlayerAlcoholEffect2D effect = GetComponent<PlayerAlcoholEffect2D>();
        return effect != null ? effect.CurrentFoodDrainMultiplier : 1f;
    }

    // 食料を回復し、実際に回復した量を返す
    public float RestoreFood(float amount)
    {
        if (amount <= 0f ||
            (playerHealth != null && playerHealth.IsDead))
        {
            return 0f;
        }

        float previousFood = currentFood;

        SetFoodInternal(currentFood + amount, true);

        return currentFood - previousFood;
    }

    // 将来、走る・暑い場所・毒などで使える減少用メソッド
    public float DrainFood(float amount)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        float previousFood = currentFood;

        SetFoodInternal(currentFood - amount, true);

        return previousFood - currentFood;
    }

    [ContextMenu("Refill Food")]
    public void RefillFood()
    {
        SetFoodInternal(maxFood, true);
    }

    public void RefillAll()
    {
        SetFoodInternal(maxFood, true);
    }

    [ContextMenu("Empty Food")]
    public void EmptyFood()
    {
        SetFoodInternal(0f, true);
    }

    private void UpdateEmptyDamage(float deltaTime)
    {
        if (!enableDamageAtZero)
        {
            emptyDamageTimer = 0f;
            return;
        }

        bool shouldTakeDamage =
            IsFoodEmpty;

        if (!shouldTakeDamage)
        {
            emptyDamageTimer = 0f;
            return;
        }

        emptyDamageTimer += deltaTime;

        if (emptyDamageTimer < emptyDamageInterval)
        {
            return;
        }

        emptyDamageTimer = 0f;

        if (IsFoodEmpty && foodEmptyDamage > 0)
        {
            playerHealth.TakeDamage(foodEmptyDamage);
        }
    }

    private void SetFoodInternal(
        float value,
        bool notify)
    {
        float previousFood = currentFood;
        SurvivalNeedState previousState = foodState;

        currentFood = Mathf.Clamp(value, 0f, maxFood);
        foodState = GetState(currentFood, maxFood);

        if (notify &&
            !Mathf.Approximately(previousFood, currentFood))
        {
            FoodChanged?.Invoke(currentFood, maxFood);
        }

        if (notify && previousState != foodState)
        {
            FoodStateChanged?.Invoke(foodState);
        }
    }

    private SurvivalNeedState GetState(
        float currentValue,
        float maxValue)
    {
        if (maxValue <= 0f || currentValue <= 0.01f)
        {
            return SurvivalNeedState.Empty;
        }

        float percent = currentValue / maxValue;

        if (percent <= criticalThreshold)
        {
            return SurvivalNeedState.Critical;
        }

        if (percent <= lowThreshold)
        {
            return SurvivalNeedState.Low;
        }

        if (percent <= warningThreshold)
        {
            return SurvivalNeedState.Warning;
        }

        return SurvivalNeedState.Normal;
    }

    private float GetMoveSpeedMultiplier(
        SurvivalNeedState state,
        float lowMultiplier,
        float criticalMultiplier,
        float emptyMultiplier)
    {
        switch (state)
        {
            case SurvivalNeedState.Low:
                return lowMultiplier;

            case SurvivalNeedState.Critical:
                return criticalMultiplier;

            case SurvivalNeedState.Empty:
                return emptyMultiplier;

            default:
                return 1f;
        }
    }

    private void NotifyAll()
    {
        FoodChanged?.Invoke(currentFood, maxFood);

        FoodStateChanged?.Invoke(foodState);
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
        maxFood = Mathf.Max(1f, maxFood);

        startingFood = Mathf.Clamp(
            startingFood,
            0f,
            maxFood
        );

        foodDecreasePerMinute = Mathf.Max(
            0f,
            foodDecreasePerMinute
        );

        warningThreshold = Mathf.Clamp01(warningThreshold);

        lowThreshold = Mathf.Clamp(
            lowThreshold,
            0f,
            warningThreshold
        );

        criticalThreshold = Mathf.Clamp(
            criticalThreshold,
            0f,
            lowThreshold
        );

        foodLowMoveSpeedMultiplier = Mathf.Clamp(
            foodLowMoveSpeedMultiplier,
            0.05f,
            1f
        );

        foodCriticalMoveSpeedMultiplier = Mathf.Clamp(
            foodCriticalMoveSpeedMultiplier,
            0.05f,
            1f
        );

        foodEmptyMoveSpeedMultiplier = Mathf.Clamp(
            foodEmptyMoveSpeedMultiplier,
            0.05f,
            1f
        );

        emptyDamageInterval = Mathf.Max(
            0.1f,
            emptyDamageInterval
        );

        foodEmptyDamage = Mathf.Max(0, foodEmptyDamage);
    }
}