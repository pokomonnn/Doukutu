using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HealthBarUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterHealth targetHealth;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private TMP_Text healthText;

    private void Awake()
    {
        // 同じオブジェクトにSliderが付いていれば自動で取得
        if (healthSlider == null)
        {
            healthSlider = GetComponent<Slider>();
        }
    }

    private void OnEnable()
    {
        if (targetHealth != null)
        {
            targetHealth.HealthChanged += UpdateHealthBar;
        }
    }

    private void Start()
    {
        RefreshHealthBar();
    }

    private void OnDisable()
    {
        if (targetHealth != null)
        {
            targetHealth.HealthChanged -= UpdateHealthBar;
        }
    }

    private void RefreshHealthBar()
    {
        if (targetHealth == null)
        {
            return;
        }

        UpdateHealthBar(targetHealth.CurrentHealth, targetHealth.MaxHealth);
    }

    private void UpdateHealthBar(int currentHealth, int maxHealth)
    {
        if (maxHealth <= 0)
        {
            return;
        }

        float healthPercent = (float)currentHealth / maxHealth;

        if (healthSlider != null)
        {
            healthSlider.minValue = 0f;
            healthSlider.maxValue = 1f;
            healthSlider.value = healthPercent;
        }

        if (healthText != null)
        {
            healthText.text = $"{currentHealth} / {maxHealth}";
        }
    }
}