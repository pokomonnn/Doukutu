using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// EnemyのCharacterHealthを監視して、頭上のHPバーを更新します。
/// World Space Canvasの子に付ける想定です。
/// </summary>
public class EnemyHealthBarUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら親から自動取得します。")]
    [SerializeField] private CharacterHealth characterHealth;

    [Tooltip("HP表示用Slider。Min=0 / Max=1として自動制御します。")]
    [SerializeField] private Slider healthSlider;

    [Tooltip("任意。設定すると 75 / 100 のように数値も表示します。")]
    [SerializeField] private TMP_Text healthText;

    [Tooltip("表示・非表示を切り替えるRoot。未設定ならこのGameObjectを使用します。")]
    [SerializeField] private GameObject visualRoot;

    [Header("表示設定")]
    [Tooltip("ONならHP満タン時は非表示になります。攻撃を受けると表示します。")]
    [SerializeField] private bool hideAtFullHealth = false;

    [Tooltip("ONなら死亡時にHPバーを非表示にします。")]
    [SerializeField] private bool hideWhenDead = true;

    [Tooltip("EnemyがScale X=-1で左右反転しても、HPバーを反転させません。")]
    [SerializeField] private bool keepUnflipped = true;

    [Header("アニメーション")]
    [Tooltip("HP減少を少し滑らかに表示します。")]
    [SerializeField] private bool smoothValue = true;

    [SerializeField, Min(0.01f)] private float smoothSpeed = 10f;

    private float targetValue = 1f;
    private Vector3 initialLocalScale;

    private void Awake()
    {
        initialLocalScale = transform.localScale;
        FindReferences();
        ConfigureSlider();
    }

    private void OnEnable()
    {
        FindReferences();
        Subscribe();
        RefreshImmediate();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (healthSlider == null || !smoothValue)
        {
            return;
        }

        healthSlider.value = Mathf.MoveTowards(
            healthSlider.value,
            targetValue,
            smoothSpeed * Time.deltaTime
        );
    }

    private void LateUpdate()
    {
        if (!keepUnflipped || transform.parent == null)
        {
            return;
        }

        float parentWorldSign = transform.parent.lossyScale.x < 0f
            ? -1f
            : 1f;

        Vector3 scale = transform.localScale;
        scale.x = Mathf.Abs(initialLocalScale.x) * parentWorldSign;
        scale.y = initialLocalScale.y;
        scale.z = initialLocalScale.z;
        transform.localScale = scale;
    }

    private void FindReferences()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponentInParent<CharacterHealth>();
        }

        if (healthSlider == null)
        {
            healthSlider = GetComponentInChildren<Slider>(true);
        }

        if (visualRoot == null && healthSlider != null)
        {
            // Script自身を無効化するとHealthChangedを受け取れなくなるため、
            // 未設定時はSlider側だけを表示Rootとして使用します。
            visualRoot = healthSlider.gameObject;
        }
    }

    private void ConfigureSlider()
    {
        if (healthSlider == null)
        {
            return;
        }

        healthSlider.minValue = 0f;
        healthSlider.maxValue = 1f;
        healthSlider.wholeNumbers = false;
    }

    private void Subscribe()
    {
        if (characterHealth == null)
        {
            return;
        }

        characterHealth.HealthChanged -= HandleHealthChanged;
        characterHealth.HealthChanged += HandleHealthChanged;

        characterHealth.Died -= HandleDied;
        characterHealth.Died += HandleDied;
    }

    private void Unsubscribe()
    {
        if (characterHealth == null)
        {
            return;
        }

        characterHealth.HealthChanged -= HandleHealthChanged;
        characterHealth.Died -= HandleDied;
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        int safeMaxHealth = Mathf.Max(1, maxHealth);
        targetValue = Mathf.Clamp01(
            (float)Mathf.Max(0, currentHealth) / safeMaxHealth
        );

        if (healthSlider != null && !smoothValue)
        {
            healthSlider.value = targetValue;
        }

        if (healthText != null)
        {
            healthText.text =
                $"{Mathf.Max(0, currentHealth)} / {safeMaxHealth}";
        }

        UpdateVisibility(currentHealth, safeMaxHealth);
    }

    private void HandleDied()
    {
        if (hideWhenDead && visualRoot != null)
        {
            visualRoot.SetActive(false);
        }
    }

    private void RefreshImmediate()
    {
        if (characterHealth == null)
        {
            return;
        }

        ConfigureSlider();

        int maxHealth = Mathf.Max(1, characterHealth.MaxHealth);
        targetValue = Mathf.Clamp01(
            (float)Mathf.Max(0, characterHealth.CurrentHealth) / maxHealth
        );

        if (healthSlider != null)
        {
            healthSlider.value = targetValue;
        }

        if (healthText != null)
        {
            healthText.text =
                $"{Mathf.Max(0, characterHealth.CurrentHealth)} / {maxHealth}";
        }

        UpdateVisibility(characterHealth.CurrentHealth, maxHealth);
    }

    private void UpdateVisibility(int currentHealth, int maxHealth)
    {
        if (visualRoot == null)
        {
            return;
        }

        bool shouldShow = true;

        if (hideWhenDead && currentHealth <= 0)
        {
            shouldShow = false;
        }
        else if (hideAtFullHealth && currentHealth >= maxHealth)
        {
            shouldShow = false;
        }

        if (visualRoot.activeSelf != shouldShow)
        {
            visualRoot.SetActive(shouldShow);
        }
    }

    private void OnValidate()
    {
        smoothSpeed = Mathf.Max(0.01f, smoothSpeed);
    }
}
