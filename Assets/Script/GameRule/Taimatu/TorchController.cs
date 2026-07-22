using System;
using UnityEngine;

/// <summary>
/// ダーケストダンジョン風の松明残量を管理します。
/// 時間経過で残量が減り、完全に消えた後はSAN値を徐々に減らします。
/// 同一プレイ中は、シーンを移動してPlayerが作り直されても残量を保持します。
/// </summary>
[DisallowMultipleComponent]
public class TorchController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定ならPlayerから自動取得します")]
    [SerializeField] private PlayerSanityController sanityController;

    [Tooltip("未設定ならPlayerから自動取得します")]
    [SerializeField] private CharacterHealth playerHealth;

    [Tooltip("キャンプ中の減少を止めたい場合に使用します。未設定なら自動検索します")]
    [SerializeField] private CampModeController campModeController;

    [Header("松明残量")]
    [SerializeField, Min(1f)] private float maximumTorch = 100f;

    [Tooltip("セッション保存値がない時の開始残量")]
    [SerializeField, Min(0f)] private float startingTorch = 100f;

    [Tooltip("1秒ごとに減る松明値。1なら100秒で100から0になります")]
    [SerializeField, Min(0f)] private float decreasePerSecond = 0.15f;

    [Header("消灯後のSAN減少")]
    [Tooltip("松明が完全に消えてからSAN減少が始まるまでの猶予秒数")]
    [SerializeField, Min(0f)] private float sanityDrainDelay = 1f;

    [Tooltip("松明が消えている間、1秒ごとに減るSAN値")]
    [SerializeField, Min(0f)] private float sanityDrainPerSecond = 0.2f;

    [Header("停止条件")]
    [SerializeField] private bool pauseDecreaseWhilePlayerIsDead = true;
    [SerializeField] private bool pauseDecreaseWhileCamping = true;

    [Header("シーン間保持")]
    [Tooltip("オンなら、同一プレイ中にシーンを移動しても松明残量を引き継ぎます")]
    [SerializeField] private bool persistBetweenScenes = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public float CurrentTorch => currentTorch;
    public float MaximumTorch => maximumTorch;

    public float TorchPercent => maximumTorch <= 0f
        ? 0f
        : Mathf.Clamp01(currentTorch / maximumTorch);

    public bool IsFull => currentTorch >= maximumTorch - 0.01f;
    public bool IsExtinguished => currentTorch <= 0.01f;

    /// <summary>現在値と最大値を通知します。</summary>
    public event Action<float, float> TorchChanged;

    /// <summary>trueなら消灯、falseなら再点火です。</summary>
    public event Action<bool> ExtinguishedStateChanged;

    private float currentTorch;
    private float extinguishedDuration;
    private bool previousExtinguishedState;

    private static bool hasSessionValue;
    private static float sessionTorchValue;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticSession()
    {
        hasSessionValue = false;
        sessionTorchValue = 0f;
    }

    private void Awake()
    {
        FindReferences();

        float initialValue = persistBetweenScenes && hasSessionValue
            ? sessionTorchValue
            : startingTorch;

        currentTorch = Mathf.Clamp(initialValue, 0f, maximumTorch);
        previousExtinguishedState = IsExtinguished;

        SaveSessionValue();
    }

    private void Start()
    {
        NotifyTorchChanged();
        ExtinguishedStateChanged?.Invoke(IsExtinguished);
    }

    private void Update()
    {
        if (ShouldPause())
        {
            return;
        }

        if (!IsExtinguished)
        {
            extinguishedDuration = 0f;

            if (decreasePerSecond > 0f)
            {
                DrainTorch(decreasePerSecond * Time.deltaTime);
            }

            return;
        }

        extinguishedDuration += Time.deltaTime;

        if (extinguishedDuration < sanityDrainDelay ||
            sanityDrainPerSecond <= 0f ||
            sanityController == null)
        {
            return;
        }

        sanityController.DrainSanity(
            sanityDrainPerSecond * Time.deltaTime
        );
    }

    private void OnDisable()
    {
        SaveSessionValue();
    }

    /// <summary>
    /// 松明を回復し、実際に増えた量を返します。
    /// </summary>
    public float RestoreTorch(float amount)
    {
        if (amount <= 0f || IsFull)
        {
            return 0f;
        }

        float previousValue = currentTorch;
        SetTorchInternal(currentTorch + amount, true);

        return currentTorch - previousValue;
    }

    /// <summary>
    /// 松明を減らし、実際に減った量を返します。
    /// </summary>
    public float DrainTorch(float amount)
    {
        if (amount <= 0f || IsExtinguished)
        {
            return 0f;
        }

        float previousValue = currentTorch;
        SetTorchInternal(currentTorch - amount, true);

        return previousValue - currentTorch;
    }

    public void SetTorch(float value)
    {
        SetTorchInternal(value, true);
    }

    [ContextMenu("Refill Torch")]
    public void RefillTorch()
    {
        SetTorchInternal(maximumTorch, true);
    }

    [ContextMenu("Extinguish Torch")]
    public void ExtinguishTorch()
    {
        SetTorchInternal(0f, true);
    }

    /// <summary>
    /// タイトル画面のニューゲーム開始時などに、
    /// シーン間で保持している松明値を静的に消去します。
    /// </summary>
    public static void ClearStoredSessionValue()
    {
        hasSessionValue = false;
        sessionTorchValue = 0f;
    }

    [ContextMenu("Clear Torch Session Value")]
    public void ClearSessionValue()
    {
        ClearStoredSessionValue();

        if (showDebugLogs)
        {
            Debug.Log("[TorchController] シーン間の松明保存値を消去しました。", this);
        }
    }

    private void SetTorchInternal(float value, bool notify)
    {
        float nextValue = Mathf.Clamp(value, 0f, maximumTorch);

        if (Mathf.Approximately(currentTorch, nextValue))
        {
            return;
        }

        bool wasExtinguished = IsExtinguished;
        currentTorch = nextValue;
        bool isNowExtinguished = IsExtinguished;

        if (!isNowExtinguished)
        {
            extinguishedDuration = 0f;
        }

        SaveSessionValue();

        if (notify)
        {
            NotifyTorchChanged();
        }

        if (wasExtinguished != isNowExtinguished ||
            previousExtinguishedState != isNowExtinguished)
        {
            previousExtinguishedState = isNowExtinguished;
            ExtinguishedStateChanged?.Invoke(isNowExtinguished);

            if (showDebugLogs)
            {
                Debug.Log(
                    isNowExtinguished
                        ? "[TorchController] 松明が完全に消えました。SAN減少を開始します。"
                        : "[TorchController] 松明を再点火しました。",
                    this
                );
            }
        }
    }

    private bool ShouldPause()
    {
        if (pauseDecreaseWhilePlayerIsDead &&
            playerHealth != null &&
            playerHealth.IsDead)
        {
            return true;
        }

        if (pauseDecreaseWhileCamping &&
            campModeController != null &&
            (campModeController.IsCamping ||
             campModeController.IsBusy ||
             campModeController.IsSleeping))
        {
            return true;
        }

        return false;
    }

    private void NotifyTorchChanged()
    {
        TorchChanged?.Invoke(currentTorch, maximumTorch);
    }

    private void SaveSessionValue()
    {
        if (!persistBetweenScenes)
        {
            return;
        }

        hasSessionValue = true;
        sessionTorchValue = currentTorch;
    }

    private void FindReferences()
    {
        if (sanityController == null)
        {
            sanityController = GetComponent<PlayerSanityController>();
        }

        if (sanityController == null)
        {
            sanityController = GetComponentInParent<PlayerSanityController>();
        }

        if (sanityController == null)
        {
            sanityController = FindAnyObjectByType<PlayerSanityController>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponent<CharacterHealth>();
        }

        if (playerHealth == null)
        {
            playerHealth = GetComponentInParent<CharacterHealth>();
        }

        if (campModeController == null && pauseDecreaseWhileCamping)
        {
            campModeController = FindAnyObjectByType<CampModeController>();
        }
    }

    private void OnValidate()
    {
        maximumTorch = Mathf.Max(1f, maximumTorch);
        startingTorch = Mathf.Clamp(startingTorch, 0f, maximumTorch);
        decreasePerSecond = Mathf.Max(0f, decreasePerSecond);
        sanityDrainDelay = Mathf.Max(0f, sanityDrainDelay);
        sanityDrainPerSecond = Mathf.Max(0f, sanityDrainPerSecond);
    }
}
