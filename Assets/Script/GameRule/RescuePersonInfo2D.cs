using UnityEngine;

public enum RescuePersonCondition2D
{
    [InspectorName("健康")]
    Healthy = 0,

    [InspectorName("軽傷")]
    LightInjury = 1,

    [InspectorName("負傷")]
    Injured = 2,

    [InspectorName("重傷")]
    SevereInjury = 3,

    [InspectorName("死亡")]
    Dead = 4
}

/// <summary>
/// 救出NPCの詳細表示に使う情報を保持します。
/// 重量はCarryableObject2DのCarry Weight Kgをそのまま参照します。
/// CharacterHealthの残り体力に応じて、状態を悪化方向へ自動同期できます。
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonInfo2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら同じObject・親・子から自動取得します。")]
    [InspectorName("運搬コンポーネント")]
    [SerializeField] private CarryableObject2D carryableObject;

    [Tooltip("死亡状態と負傷状態の自動判定に使います。未設定でも使用できます。")]
    [InspectorName("体力コンポーネント（任意）")]
    [SerializeField] private CharacterHealth characterHealth;

    [Tooltip("詳細UIを表示する基準位置です。未設定ならこのNPCのTransformを使います。")]
    [InspectorName("詳細表示位置（任意）")]
    [SerializeField] private Transform detailAnchor;

    [Header("NPC情報")]
    [InspectorName("表示名")]
    [SerializeField] private string displayName = "生存者";

    [InspectorName("職業")]
    [SerializeField] private string occupation = "一般人";

    [InspectorName("状態")]
    [SerializeField]
    private RescuePersonCondition2D condition =
        RescuePersonCondition2D.Healthy;

    [Tooltip("ONの場合、CharacterHealthが死亡していればInspectorの状態設定より優先して『死亡』と表示します。")]
    [InspectorName("死亡状態を自動反映")]
    [SerializeField] private bool overrideWithDeathState = true;

    [Header("体力と状態の連動")]
    [Tooltip("ONにすると、体力が減った時に状態を 健康→軽傷→負傷→重傷 の順に自動で悪化させます。Inspectorで最初から設定した状態より軽くなることはありません。")]
    [InspectorName("体力低下で状態を自動悪化")]
    [SerializeField] private bool worsenConditionFromHealth = true;

    [Tooltip("残りHPがこの割合以下になると最低でも軽傷になります。0.75 = 75%。")]
    [InspectorName("軽傷になるHP割合")]
    [SerializeField, Range(0.01f, 1f)] private float lightInjuryHealthPercent = 0.75f;

    [Tooltip("残りHPがこの割合以下になると最低でも負傷になります。0.50 = 50%。")]
    [InspectorName("負傷になるHP割合")]
    [SerializeField, Range(0.01f, 1f)] private float injuredHealthPercent = 0.50f;

    [Tooltip("残りHPがこの割合以下になると最低でも重傷になります。0.25 = 25%。")]
    [InspectorName("重傷になるHP割合")]
    [SerializeField, Range(0.01f, 1f)] private float severeInjuryHealthPercent = 0.25f;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? "生存者"
            : displayName.Trim();

    public string Occupation =>
        string.IsNullOrWhiteSpace(occupation)
            ? "不明"
            : occupation.Trim();

    public RescuePersonCondition2D Condition
    {
        get
        {
            if (overrideWithDeathState &&
                characterHealth != null &&
                characterHealth.IsDead)
            {
                return RescuePersonCondition2D.Dead;
            }

            return condition;
        }
    }

    public string ConditionLabel =>
        GetConditionLabel(Condition);

    public float WeightKg =>
        carryableObject != null
            ? Mathf.Max(0f, carryableObject.CarryWeightKg)
            : 0f;

    public Vector3 DetailWorldPosition =>
        detailAnchor != null
            ? detailAnchor.position
            : transform.position;

    private bool isHealthSubscribed;
    private bool hasStarted;

    private void Awake()
    {
        // Awake時点では、別GameObject上のCharacterHealthが
        // まだ初期化されていない可能性があります。
        // ここでは参照取得だけを行い、HPからの状態同期はStartで行います。
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeHealthEvents();

        // Start完了後の再有効化時だけ、現在HPから状態を再同期します。
        // 初回OnEnableではCharacterHealthのAwake前の可能性があるため同期しません。
        if (hasStarted)
        {
            RefreshConditionFromHealth();
        }
    }

    private void Start()
    {
        hasStarted = true;

        // 全コンポーネントのAwake完了後に初回同期することで、
        // CurrentHealthの初期値0を「死亡」と誤判定するのを防ぎます。
        RefreshConditionFromHealth();
    }

    private void OnDisable()
    {
        UnsubscribeHealthEvents();
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = "生存者";
        }

        if (string.IsNullOrWhiteSpace(occupation))
        {
            occupation = "不明";
        }

        lightInjuryHealthPercent = Mathf.Clamp01(lightInjuryHealthPercent);
        injuredHealthPercent = Mathf.Clamp(
            injuredHealthPercent,
            0.01f,
            lightInjuryHealthPercent
        );
        severeInjuryHealthPercent = Mathf.Clamp(
            severeInjuryHealthPercent,
            0.01f,
            injuredHealthPercent
        );

        FindReferences();
    }

    /// <summary>
    /// 落下ダメージなどからNPCの状態を更新するための共通APIです。
    /// </summary>
    public void SetCondition(RescuePersonCondition2D nextCondition)
    {
        condition = nextCondition;
    }

    /// <summary>
    /// 現在のCharacterHealthを確認し、状態を必要な段階まで悪化させます。
    /// 初期設定した状態より軽い状態へ勝手に戻すことはありません。
    /// </summary>
    public void RefreshConditionFromHealth()
    {
        FindReferences();

        if (characterHealth == null)
        {
            return;
        }

        // CharacterHealthの正式な死亡フラグだけを死亡判定に使います。
        // CurrentHealthはCharacterHealth.Awake前だと初期値0のため、
        // CurrentHealth <= 0だけで判定するとゲーム開始時に死亡扱いになります。
        if (characterHealth.IsDead)
        {
            condition = RescuePersonCondition2D.Dead;
            return;
        }

        if (!worsenConditionFromHealth || characterHealth.MaxHealth <= 0)
        {
            return;
        }

        float healthPercent = Mathf.Clamp01(
            (float)characterHealth.CurrentHealth / characterHealth.MaxHealth
        );

        RescuePersonCondition2D healthCondition =
            RescuePersonCondition2D.Healthy;

        if (healthPercent <= severeInjuryHealthPercent)
        {
            healthCondition = RescuePersonCondition2D.SevereInjury;
        }
        else if (healthPercent <= injuredHealthPercent)
        {
            healthCondition = RescuePersonCondition2D.Injured;
        }
        else if (healthPercent <= lightInjuryHealthPercent)
        {
            healthCondition = RescuePersonCondition2D.LightInjury;
        }

        // enumは Healthy=0 → Dead=4 の順で重症度が上がるため、
        // 現在より重い状態だけ反映します。
        if ((int)healthCondition > (int)condition)
        {
            condition = healthCondition;
        }
    }

    public void SetOccupation(string nextOccupation)
    {
        occupation = string.IsNullOrWhiteSpace(nextOccupation)
            ? "不明"
            : nextOccupation.Trim();
    }

    public void SetDisplayName(string nextDisplayName)
    {
        displayName = string.IsNullOrWhiteSpace(nextDisplayName)
            ? "生存者"
            : nextDisplayName.Trim();
    }

    public static string GetConditionLabel(
        RescuePersonCondition2D value)
    {
        switch (value)
        {
            case RescuePersonCondition2D.Healthy:
                return "健康";

            case RescuePersonCondition2D.LightInjury:
                return "軽傷";

            case RescuePersonCondition2D.Injured:
                return "負傷";

            case RescuePersonCondition2D.SevereInjury:
                return "重傷";

            case RescuePersonCondition2D.Dead:
                return "死亡";

            default:
                return "不明";
        }
    }

    private void SubscribeHealthEvents()
    {
        if (isHealthSubscribed || characterHealth == null)
        {
            return;
        }

        characterHealth.HealthChanged += HandleHealthChanged;
        characterHealth.Died += HandleDied;
        isHealthSubscribed = true;
    }

    private void UnsubscribeHealthEvents()
    {
        if (!isHealthSubscribed || characterHealth == null)
        {
            return;
        }

        characterHealth.HealthChanged -= HandleHealthChanged;
        characterHealth.Died -= HandleDied;
        isHealthSubscribed = false;
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        RefreshConditionFromHealth();
    }

    private void HandleDied()
    {
        condition = RescuePersonCondition2D.Dead;
    }

    private void FindReferences()
    {
        if (carryableObject == null)
        {
            carryableObject = GetComponent<CarryableObject2D>();
        }

        if (carryableObject == null)
        {
            carryableObject = GetComponentInParent<CarryableObject2D>();
        }

        if (carryableObject == null)
        {
            carryableObject = GetComponentInChildren<CarryableObject2D>(true);
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponentInParent<CharacterHealth>();
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponentInChildren<CharacterHealth>(true);
        }
    }
}
