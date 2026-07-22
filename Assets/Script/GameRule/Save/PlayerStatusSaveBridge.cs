using UnityEngine;

/// <summary>
/// PlayerのHP・食料・水分・SAN・状態異常・松明残量と、
/// PlayerStatusSessionStoreを橋渡しします。
///
/// 探索シーンのPlayerへ追加してください。
/// Town側にも同じPlayerが存在する場合は追加して構いません。
/// 存在しないControllerの項目は、以前の保存値を上書きしません。
/// </summary>
[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
public class PlayerStatusSaveBridge : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら同じPlayerから自動取得します")]
    [SerializeField] private CharacterHealth characterHealth;

    [SerializeField] private PlayerSurvivalController survivalController;
    [SerializeField] private PlayerSanityController sanityController;
    [SerializeField]
    private PlayerStatusConditionController statusConditionController;
    [SerializeField] private TorchController torchController;

    [Header("自動処理")]
    [Tooltip("シーン開始時に、セッションへ保存されている状態を復元します")]
    [SerializeField] private bool restoreOnStart = true;

    [Tooltip("シーンを離れる時に現在状態をセッションへ保存します")]
    [SerializeField] private bool captureOnDisable = true;

    [Tooltip("セッション値がない最初の開始時に、現在の初期値を保存します")]
    [SerializeField] private bool captureDefaultsWhenNoSession = true;

    [Header("HPロード設定")]
    [Tooltip("死亡状態で再開しないよう、ロード時HPの最低値を指定します。0なら死亡状態も許可します")]
    [SerializeField, Min(0)] private int minimumRestoredHealth = 1;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private bool hasStarted;
    private bool isQuitting;

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        hasStarted = true;
        FindReferences();

        if (restoreOnStart && PlayerStatusSessionStore.HasData)
        {
            ReloadFromSession();
            return;
        }

        if (captureDefaultsWhenNoSession &&
            !PlayerStatusSessionStore.HasData)
        {
            CaptureToSession();
        }
    }

    private void OnDisable()
    {
        if (!captureOnDisable || !hasStarted || isQuitting)
        {
            return;
        }

        CaptureToSession();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
        CaptureToSession();
    }

    /// <summary>
    /// 現在のPlayer状態を、シーン間保持ストアへ保存します。
    /// 存在しないControllerの項目は以前の値を維持します。
    /// </summary>
    public bool CaptureToSession()
    {
        FindReferences();

        SavedPlayerStatusData data =
            PlayerStatusSessionStore.GetOrCreateMutableData();

        bool capturedAnything = false;

        if (characterHealth != null)
        {
            data.HasHealth = true;
            data.CurrentHealth = characterHealth.CurrentHealth;
            data.MaximumHealthAtSave = characterHealth.MaxHealth;
            capturedAnything = true;
        }

        if (survivalController != null)
        {
            data.HasSurvival = true;
            data.CurrentFood = survivalController.CurrentFood;
            data.MaximumFoodAtSave = survivalController.MaxFood;
            data.CurrentWater = survivalController.CurrentWater;
            data.MaximumWaterAtSave = survivalController.MaxWater;
            capturedAnything = true;
        }

        if (sanityController != null)
        {
            data.HasSanity = true;
            data.CurrentSanity = sanityController.CurrentSanity;
            data.MaximumSanityAtSave = sanityController.MaxSanity;
            capturedAnything = true;
        }

        if (statusConditionController != null)
        {
            data.HasStatusConditions = true;
            data.ActiveStatusConditions =
                statusConditionController.ActiveConditions;
            capturedAnything = true;
        }

        if (torchController != null)
        {
            data.HasTorch = true;
            data.CurrentTorch = torchController.CurrentTorch;
            data.MaximumTorchAtSave = torchController.MaximumTorch;
            capturedAnything = true;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                capturedAnything
                    ? $"[PlayerStatusSaveBridge] 状態を保存しました：{Describe(data)}"
                    : "[PlayerStatusSaveBridge] 保存対象のPlayer Controllerが見つかりません。",
                this
            );
        }

        return capturedAnything;
    }

    /// <summary>
    /// セッションへ保存されているPlayer状態を、現在のPlayerへ反映します。
    /// </summary>
    public bool ReloadFromSession()
    {
        FindReferences();

        SavedPlayerStatusData data =
            PlayerStatusSessionStore.CreateSnapshot();

        if (data == null || !data.HasAnyData)
        {
            Log("復元できるPlayer状態がありません。");
            return false;
        }

        bool restoredAnything = false;

        // 食料・水分の回復APIは死亡中に動かないため、HPを先に戻します。
        if (data.HasHealth && characterHealth != null)
        {
            int restoredHealth = data.CurrentHealth;

            if (minimumRestoredHealth > 0)
            {
                restoredHealth = Mathf.Max(
                    minimumRestoredHealth,
                    restoredHealth
                );
            }

            characterHealth.RestoreHealth(restoredHealth);
            restoredAnything = true;
        }

        if (data.HasSurvival && survivalController != null)
        {
            SetFoodValue(data.CurrentFood);
            SetWaterValue(data.CurrentWater);
            restoredAnything = true;
        }

        if (data.HasSanity && sanityController != null)
        {
            sanityController.SetSanity(data.CurrentSanity);
            restoredAnything = true;
        }

        if (data.HasStatusConditions &&
            statusConditionController != null)
        {
            RestoreStatusConditions(
                data.ActiveStatusConditions
            );
            restoredAnything = true;
        }

        if (data.HasTorch && torchController != null)
        {
            torchController.SetTorch(data.CurrentTorch);
            restoredAnything = true;
        }

        if (restoredAnything)
        {
            // 現在の各最大値でClampされた結果を、セッションにも戻します。
            CaptureToSession();
            Log($"Player状態を復元しました：{Describe(data)}");
        }
        else
        {
            Log("Player状態データはありますが、対応するControllerが現在シーンにありません。");
        }

        return restoredAnything;
    }

    [ContextMenu("Capture Player Status To Session")]
    private void CaptureFromContextMenu()
    {
        CaptureToSession();
    }

    [ContextMenu("Reload Player Status From Session")]
    private void ReloadFromContextMenu()
    {
        ReloadFromSession();
    }

    private void SetFoodValue(float targetValue)
    {
        float difference = targetValue -
            survivalController.CurrentFood;

        if (difference > 0f)
        {
            survivalController.RestoreFood(difference);
        }
        else if (difference < 0f)
        {
            survivalController.DrainFood(-difference);
        }
    }

    private void SetWaterValue(float targetValue)
    {
        float difference = targetValue -
            survivalController.CurrentWater;

        if (difference > 0f)
        {
            survivalController.RestoreWater(difference);
        }
        else if (difference < 0f)
        {
            survivalController.DrainWater(-difference);
        }
    }

    private void RestoreStatusConditions(
        StatusConditionType conditions)
    {
        AudioSource statusAudio =
            statusConditionController.GetComponent<AudioSource>();

        bool previousMute = statusAudio != null && statusAudio.mute;

        if (statusAudio != null)
        {
            // 骨折状態の復元時に骨折SEが鳴らないよう一時的にミュートします。
            statusAudio.mute = true;
        }

        statusConditionController.ClearAllConditions();

        if (conditions != StatusConditionType.None)
        {
            statusConditionController.AddConditions(conditions);
        }

        if (statusAudio != null)
        {
            statusAudio.mute = previousMute;
        }
    }

    private void FindReferences()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponentInParent<CharacterHealth>();
        }

        if (survivalController == null)
        {
            survivalController =
                GetComponent<PlayerSurvivalController>();
        }

        if (survivalController == null)
        {
            survivalController =
                GetComponentInParent<PlayerSurvivalController>();
        }

        if (sanityController == null)
        {
            sanityController =
                GetComponent<PlayerSanityController>();
        }

        if (sanityController == null)
        {
            sanityController =
                GetComponentInParent<PlayerSanityController>();
        }

        if (statusConditionController == null)
        {
            statusConditionController =
                GetComponent<PlayerStatusConditionController>();
        }

        if (statusConditionController == null)
        {
            statusConditionController =
                GetComponentInParent<
                    PlayerStatusConditionController>();
        }

        if (torchController == null)
        {
            torchController = GetComponent<TorchController>();
        }

        if (torchController == null)
        {
            torchController = GetComponentInParent<TorchController>();
        }
    }

    private static string Describe(SavedPlayerStatusData data)
    {
        if (data == null)
        {
            return "null";
        }

        return
            $"HP={data.CurrentHealth}, " +
            $"Food={data.CurrentFood:0.0}, " +
            $"Water={data.CurrentWater:0.0}, " +
            $"SAN={data.CurrentSanity:0.0}, " +
            $"Conditions={data.ActiveStatusConditions}, " +
            $"Torch={data.CurrentTorch:0.0}";
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log(
                $"[PlayerStatusSaveBridge] {message}",
                this
            );
        }
    }

    private void OnValidate()
    {
        minimumRestoredHealth =
            Mathf.Max(0, minimumRestoredHealth);
    }
}
