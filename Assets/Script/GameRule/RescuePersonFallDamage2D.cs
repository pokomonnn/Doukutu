using UnityEngine;

/// <summary>
/// 救出NPCの落下距離を監視し、着地時にCharacterHealthへ落下ダメージを与えます。
/// CarryableObject2Dで手前持ち／背負い中、またはエレベーター等の外部システムが
/// 確保している間は落下判定を行いません。
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonFallDamage2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("NPCのRigidbody2Dです。未設定なら同じObject・親・子から自動取得します。")]
    [InspectorName("Rigidbody2D")]
    [SerializeField] private Rigidbody2D targetRigidbody;

    [Tooltip("NPCの体力です。未設定なら同じObject・親・子から自動取得します。")]
    [InspectorName("体力コンポーネント")]
    [SerializeField] private CharacterHealth characterHealth;

    [Tooltip("持つ／背負う状態の判定に使います。未設定なら同じObject・親・子から自動取得します。")]
    [InspectorName("運搬コンポーネント")]
    [SerializeField] private CarryableObject2D carryableObject;

    [Tooltip("詳細UIの状態更新に使います。未設定なら同じObject・親・子から自動取得します。")]
    [InspectorName("NPC情報コンポーネント")]
    [SerializeField] private RescuePersonInfo2D rescuePersonInfo;

    [Header("着地判定")]
    [Tooltip("着地として扱うLayerです。0（Nothing）の場合はLayerを限定せず、上向きの接触面を地面として扱います。")]
    [InspectorName("地面レイヤー")]
    [SerializeField] private LayerMask groundLayers;

    [Tooltip("この値以上の上向きNormalを持つ接触だけを着地として扱います。壁への衝突による誤判定を防ぎます。")]
    [InspectorName("地面判定の上向き強さ")]
    [SerializeField, Range(0f, 1f)] private float minimumGroundNormalY = 0.45f;

    [Tooltip("下降速度がこの値を超えた時点から落下距離の計測を開始します。")]
    [InspectorName("落下計測開始速度")]
    [SerializeField, Min(0f)] private float fallingStartSpeed = 0.1f;

    [Header("落下距離とダメージ")]
    [Tooltip("この高さ未満ならダメージを受けません。")]
    [InspectorName("ダメージ開始距離")]
    [SerializeField, Min(0f)] private float lightDamageDistance = 2.5f;

    [Tooltip("この高さ以上で中ダメージになります。")]
    [InspectorName("中ダメージ距離")]
    [SerializeField, Min(0f)] private float mediumDamageDistance = 4f;

    [Tooltip("この高さ以上で大ダメージになります。")]
    [InspectorName("大ダメージ距離")]
    [SerializeField, Min(0f)] private float severeDamageDistance = 6f;

    [Tooltip("この高さ以上から落ちると現在HPに関係なく死亡させます。")]
    [InspectorName("致死落下距離")]
    [SerializeField, Min(0f)] private float fatalDamageDistance = 8f;

    [InspectorName("小ダメージ")]
    [SerializeField, Min(1)] private int lightDamage = 25;

    [InspectorName("中ダメージ")]
    [SerializeField, Min(1)] private int mediumDamage = 50;

    [InspectorName("大ダメージ")]
    [SerializeField, Min(1)] private int severeDamage = 75;

    [Header("安全設定")]
    [Tooltip("ONならプレイヤーが手前持ち／背負い中は落下距離を完全にリセットします。")]
    [InspectorName("運搬中は落下判定しない")]
    [SerializeField] private bool ignoreWhileCarried = true;

    [Tooltip("ONならエレベーター等の外部システムがNPCを確保中は落下距離を完全にリセットします。")]
    [InspectorName("外部移動中は落下判定しない")]
    [SerializeField] private bool ignoreWhileExternallyReserved = true;

    [Header("デバッグ")]
    [InspectorName("落下ダメージログを表示")]
    [SerializeField] private bool showDebugLogs;

    private bool isTrackingFall;
    private float fallStartY;
    private float lowestVelocityY;

    public bool IsTrackingFall => isTrackingFall;
    public float CurrentTrackedFallDistance =>
        isTrackingFall && targetRigidbody != null
            ? Mathf.Max(0f, fallStartY - targetRigidbody.position.y)
            : 0f;

    private void Awake()
    {
        FindReferences();
        ResetFallTracking();
    }

    private void OnEnable()
    {
        FindReferences();
        ResetFallTracking();
    }

    private void FixedUpdate()
    {
        FindReferences();

        if (!CanTrackFall())
        {
            ResetFallTracking();
            return;
        }

        float velocityY = targetRigidbody.linearVelocity.y;

        // 上昇中はまだ落下開始地点を確定しません。
        // 頂点を越えて下降し始めた位置を落下開始地点として使います。
        if (!isTrackingFall)
        {
            if (velocityY < -Mathf.Max(0f, fallingStartSpeed))
            {
                isTrackingFall = true;
                fallStartY = targetRigidbody.position.y;
                lowestVelocityY = velocityY;

                Log(
                    $"落下計測開始: Y={fallStartY:0.00} / " +
                    $"VelocityY={velocityY:0.00}"
                );
            }

            return;
        }

        if (velocityY < lowestVelocityY)
        {
            lowestVelocityY = velocityY;
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryHandleLanding(collision);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        // 高速移動やCollider構成によってEnter時に取りこぼした場合の保険です。
        TryHandleLanding(collision);
    }

    /// <summary>
    /// テレポートや特殊移動を行う側から呼ぶことで、直前の落下距離を破棄できます。
    /// </summary>
    public void ResetFallTracking()
    {
        isTrackingFall = false;
        fallStartY = targetRigidbody != null
            ? targetRigidbody.position.y
            : transform.position.y;
        lowestVelocityY = 0f;
    }

    private bool CanTrackFall()
    {
        if (targetRigidbody == null ||
            characterHealth == null ||
            characterHealth.IsDead ||
            !targetRigidbody.simulated ||
            targetRigidbody.bodyType != RigidbodyType2D.Dynamic)
        {
            return false;
        }

        if (carryableObject != null)
        {
            if (ignoreWhileCarried && carryableObject.IsCarried)
            {
                return false;
            }

            if (ignoreWhileExternallyReserved &&
                carryableObject.IsExternallyReserved)
            {
                return false;
            }
        }

        return true;
    }

    private void TryHandleLanding(Collision2D collision)
    {
        if (!isTrackingFall ||
            !CanTrackFall() ||
            collision == null ||
            collision.collider == null)
        {
            return;
        }

        if (!IsGroundLayerAllowed(collision.collider.gameObject.layer))
        {
            return;
        }

        bool hasGroundContact = false;

        for (int i = 0; i < collision.contactCount; i++)
        {
            ContactPoint2D contact = collision.GetContact(i);

            if (contact.normal.y >= minimumGroundNormalY)
            {
                hasGroundContact = true;
                break;
            }
        }

        if (!hasGroundContact)
        {
            return;
        }

        float landingY = targetRigidbody != null
            ? targetRigidbody.position.y
            : transform.position.y;

        float fallDistance = Mathf.Max(0f, fallStartY - landingY);
        float impactVelocityY = lowestVelocityY;

        ResetFallTracking();
        ApplyFallDamage(fallDistance, impactVelocityY);
    }

    private void ApplyFallDamage(float fallDistance, float impactVelocityY)
    {
        if (characterHealth == null || characterHealth.IsDead)
        {
            return;
        }

        int damage = 0;
        string tier = "無傷";

        if (fallDistance >= fatalDamageDistance)
        {
            damage = Mathf.Max(
                characterHealth.MaxHealth,
                characterHealth.CurrentHealth
            );
            tier = "致死";
        }
        else if (fallDistance >= severeDamageDistance)
        {
            damage = severeDamage;
            tier = "大";
        }
        else if (fallDistance >= mediumDamageDistance)
        {
            damage = mediumDamage;
            tier = "中";
        }
        else if (fallDistance >= lightDamageDistance)
        {
            damage = lightDamage;
            tier = "小";
        }

        if (damage <= 0)
        {
            Log(
                $"着地: 距離={fallDistance:0.00}m / " +
                $"最大下降速度={impactVelocityY:0.00} / ダメージなし"
            );
            return;
        }

        int healthBefore = characterHealth.CurrentHealth;
        characterHealth.TakeDamage(damage);

        // RescuePersonInfo2D側はHealthChangedも監視しますが、
        // 念のため着地直後にも同期を要求します。
        rescuePersonInfo?.RefreshConditionFromHealth();

        Log(
            $"落下ダメージ: 距離={fallDistance:0.00}m / " +
            $"最大下降速度={impactVelocityY:0.00} / " +
            $"段階={tier} / Damage={damage} / " +
            $"HP={healthBefore}→{characterHealth.CurrentHealth}"
        );
    }

    private bool IsGroundLayerAllowed(int layer)
    {
        // NothingのままならLayer制限なし。
        return groundLayers.value == 0 ||
               (groundLayers.value & (1 << layer)) != 0;
    }

    private void FindReferences()
    {
        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponent<Rigidbody2D>();
        }

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponentInParent<Rigidbody2D>();
        }

        if (targetRigidbody == null)
        {
            targetRigidbody = GetComponentInChildren<Rigidbody2D>(true);
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

        if (rescuePersonInfo == null)
        {
            rescuePersonInfo = GetComponent<RescuePersonInfo2D>();
        }

        if (rescuePersonInfo == null)
        {
            rescuePersonInfo = GetComponentInParent<RescuePersonInfo2D>();
        }

        if (rescuePersonInfo == null)
        {
            rescuePersonInfo = GetComponentInChildren<RescuePersonInfo2D>(true);
        }
    }

    private void OnValidate()
    {
        lightDamageDistance = Mathf.Max(0f, lightDamageDistance);
        mediumDamageDistance = Mathf.Max(lightDamageDistance, mediumDamageDistance);
        severeDamageDistance = Mathf.Max(mediumDamageDistance, severeDamageDistance);
        fatalDamageDistance = Mathf.Max(severeDamageDistance, fatalDamageDistance);

        lightDamage = Mathf.Max(1, lightDamage);
        mediumDamage = Mathf.Max(1, mediumDamage);
        severeDamage = Mathf.Max(1, severeDamage);
        fallingStartSpeed = Mathf.Max(0f, fallingStartSpeed);
        minimumGroundNormalY = Mathf.Clamp01(minimumGroundNormalY);

        FindReferences();
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"[RescuePersonFallDamage2D: {name}] {message}",
            this
        );
    }
}
