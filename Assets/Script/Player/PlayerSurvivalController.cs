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
    [SerializeField, Min(1f)] private float maxWater = 100f;

    [Header("開始時の値")]
    [Tooltip("オンなら、開始時に食料・水分を最大値にします")]
    [SerializeField] private bool startWithFullValues = true;

    [SerializeField, Min(0f)] private float startingFood = 100f;
    [SerializeField, Min(0f)] private float startingWater = 100f;

    [Header("時間経過による減少")]
    [Tooltip("1分ごとに減る食料値。1.67なら約60分で0になります")]
    [SerializeField, Min(0f)] private float foodDecreasePerMinute = 1.67f;

    [Tooltip("1分ごとに減る水分値。5なら約20分で0になります")]
    [SerializeField, Min(0f)] private float waterDecreasePerMinute = 5f;

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

    [Tooltip("水分が0の時に受けるダメージ")]
    [SerializeField, Min(0)] private int waterEmptyDamage = 2;

    public float CurrentFood => currentFood;
    public float CurrentWater => currentWater;

    public float MaxFood => maxFood;
    public float MaxWater => maxWater;

    public float FoodPercent =>
        maxFood <= 0f ? 0f : currentFood / maxFood;

    public float WaterPercent =>
        maxWater <= 0f ? 0f : currentWater / maxWater;

    public bool IsFoodFull =>
        currentFood >= maxFood - 0.01f;

    public bool IsWaterFull =>
        currentWater >= maxWater - 0.01f;

    public bool IsFoodEmpty => currentFood <= 0.01f;
    public bool IsWaterEmpty => currentWater <= 0.01f;

    public SurvivalNeedState FoodState => foodState;
    public SurvivalNeedState WaterState => waterState;

    // 後でPlayerWeightControllerから使う速度倍率
    public float FoodMoveSpeedMultiplier =>
        GetMoveSpeedMultiplier(
            foodState,
            foodLowMoveSpeedMultiplier,
            foodCriticalMoveSpeedMultiplier,
            foodEmptyMoveSpeedMultiplier
        );

    // 水分不足による移動低下は使用しない。
    // 水分の視認距離低下は WaterEnemyVisibilityController が担当する。
    public float TotalMoveSpeedMultiplier =>
        FoodMoveSpeedMultiplier;

    // UI用イベント
    public event Action<float, float> FoodChanged;
    public event Action<float, float> WaterChanged;

    public event Action<SurvivalNeedState> FoodStateChanged;
    public event Action<SurvivalNeedState> WaterStateChanged;

    private float currentFood;
    private float currentWater;

    private SurvivalNeedState foodState;
    private SurvivalNeedState waterState;

    private float emptyDamageTimer;

    private void Awake()
    {
        FindPlayerHealth();

        if (startWithFullValues)
        {
            currentFood = maxFood;
            currentWater = maxWater;
        }
        else
        {
            currentFood = Mathf.Clamp(
                startingFood,
                0f,
                maxFood
            );

            currentWater = Mathf.Clamp(
                startingWater,
                0f,
                maxWater
            );
        }

        foodState = GetState(currentFood, maxFood);
        waterState = GetState(currentWater, maxWater);
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
            foodDecreasePerMinute / 60f * deltaTime;

        float waterDecrease =
            waterDecreasePerMinute / 60f * deltaTime;

        SetFoodInternal(currentFood - foodDecrease, true);
        SetWaterInternal(currentWater - waterDecrease, true);

        UpdateEmptyDamage(deltaTime);
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

    // 水分を回復し、実際に回復した量を返す
    public float RestoreWater(float amount)
    {
        if (amount <= 0f ||
            (playerHealth != null && playerHealth.IsDead))
        {
            return 0f;
        }

        float previousWater = currentWater;

        SetWaterInternal(currentWater + amount, true);

        return currentWater - previousWater;
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

    public float DrainWater(float amount)
    {
        if (amount <= 0f)
        {
            return 0f;
        }

        float previousWater = currentWater;

        SetWaterInternal(currentWater - amount, true);

        return previousWater - currentWater;
    }

    [ContextMenu("Refill Food")]
    public void RefillFood()
    {
        SetFoodInternal(maxFood, true);
    }

    [ContextMenu("Refill Water")]
    public void RefillWater()
    {
        SetWaterInternal(maxWater, true);
    }

    [ContextMenu("Refill Food And Water")]
    public void RefillAll()
    {
        SetFoodInternal(maxFood, true);
        SetWaterInternal(maxWater, true);
    }

    [ContextMenu("Empty Food")]
    public void EmptyFood()
    {
        SetFoodInternal(0f, true);
    }

    [ContextMenu("Empty Water")]
    public void EmptyWater()
    {
        SetWaterInternal(0f, true);
    }

    private void UpdateEmptyDamage(float deltaTime)
    {
        if (!enableDamageAtZero)
        {
            emptyDamageTimer = 0f;
            return;
        }

        bool shouldTakeDamage =
            IsFoodEmpty || IsWaterEmpty;

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

        // 水分0を優先する。
        // 食料・水分ともに0でも、同じタイミングで
        // ダメージが重複しないようにする。
        if (IsWaterEmpty && waterEmptyDamage > 0)
        {
            playerHealth.TakeDamage(waterEmptyDamage);
            return;
        }

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

    private void SetWaterInternal(
        float value,
        bool notify)
    {
        float previousWater = currentWater;
        SurvivalNeedState previousState = waterState;

        currentWater = Mathf.Clamp(value, 0f, maxWater);
        waterState = GetState(currentWater, maxWater);

        if (notify &&
            !Mathf.Approximately(previousWater, currentWater))
        {
            WaterChanged?.Invoke(currentWater, maxWater);
        }

        if (notify && previousState != waterState)
        {
            WaterStateChanged?.Invoke(waterState);
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
        WaterChanged?.Invoke(currentWater, maxWater);

        FoodStateChanged?.Invoke(foodState);
        WaterStateChanged?.Invoke(waterState);
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
        maxWater = Mathf.Max(1f, maxWater);

        startingFood = Mathf.Clamp(
            startingFood,
            0f,
            maxFood
        );

        startingWater = Mathf.Clamp(
            startingWater,
            0f,
            maxWater
        );

        foodDecreasePerMinute = Mathf.Max(
            0f,
            foodDecreasePerMinute
        );

        waterDecreasePerMinute = Mathf.Max(
            0f,
            waterDecreasePerMinute
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
        waterEmptyDamage = Mathf.Max(0, waterEmptyDamage);
    }
}