using TMPro;
using UnityEngine;

/// <summary>
/// 1つのダメージ数字を上方向へ浮かせながらフェードアウトします。
/// TextMeshPro / TextMeshProUGUIの両方に対応します。
/// </summary>
public class DamagePopupUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private TMP_Text damageText;

    [Header("表示時間")]
    [SerializeField, Min(0.05f)] private float lifeTime = 0.8f;

    [Header("移動")]
    [SerializeField] private Vector3 moveDirection = Vector3.up;
    [SerializeField, Min(0f)] private float moveSpeed = 0.8f;

    [Header("拡大縮小")]
    [SerializeField] private AnimationCurve scaleCurve =
        AnimationCurve.EaseInOut(0f, 0.8f, 1f, 1f);

    [SerializeField, Min(0.01f)] private float baseScaleMultiplier = 1f;

    private float elapsed;
    private Vector3 initialScale;
    private Color initialColor;
    private bool initialized;

    private void Awake()
    {
        FindReferences();
        CacheInitialVisualState();
    }

    public void Initialize(int damage)
    {
        FindReferences();

        if (!initialized)
        {
            CacheInitialVisualState();
        }

        SetDamage(damage);
        elapsed = 0f;
        ApplyVisual(0f);
    }

    /// <summary>
    /// ショットガンのPellet合算表示など、表示中の数字だけ更新したい時に使います。
    /// </summary>
    public void SetDamage(int damage)
    {
        if (damageText != null)
        {
            damageText.text = Mathf.Max(0, damage).ToString();
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;

        float safeLifeTime = Mathf.Max(0.05f, lifeTime);
        float normalizedTime = Mathf.Clamp01(elapsed / safeLifeTime);

        Vector3 direction = moveDirection.sqrMagnitude > 0.0001f
            ? moveDirection.normalized
            : Vector3.up;

        transform.position +=
            direction * (moveSpeed * Time.deltaTime);

        ApplyVisual(normalizedTime);

        if (elapsed >= safeLifeTime)
        {
            Destroy(gameObject);
        }
    }

    private void ApplyVisual(float normalizedTime)
    {
        float curveScale = scaleCurve != null
            ? Mathf.Max(0f, scaleCurve.Evaluate(normalizedTime))
            : 1f;

        transform.localScale =
            initialScale * baseScaleMultiplier * curveScale;

        if (damageText != null)
        {
            Color color = initialColor;
            color.a = initialColor.a * (1f - normalizedTime);
            damageText.color = color;
        }
    }

    private void FindReferences()
    {
        if (damageText == null)
        {
            damageText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private void CacheInitialVisualState()
    {
        initialScale = transform.localScale;

        if (damageText != null)
        {
            initialColor = damageText.color;
        }
        else
        {
            initialColor = Color.white;
        }

        initialized = true;
    }

    private void OnValidate()
    {
        lifeTime = Mathf.Max(0.05f, lifeTime);
        moveSpeed = Mathf.Max(0f, moveSpeed);
        baseScaleMultiplier = Mathf.Max(0.01f, baseScaleMultiplier);
    }
}
