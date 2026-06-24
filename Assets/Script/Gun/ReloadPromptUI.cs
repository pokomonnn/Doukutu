using TMPro;
using UnityEngine;

[RequireComponent(typeof(TextMeshPro))]
public class ReloadPromptUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private GunShooter gunShooter;
    [SerializeField] private TextMeshPro promptText;

    [Header("フェード設定")]
    [Tooltip("表示・非表示が切り替わるまでの秒数")]
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.25f;

    private Color originalColor;
    private float currentAlpha;

    private void Awake()
    {
        if (promptText == null)
        {
            promptText = GetComponent<TextMeshPro>();
        }

        if (promptText == null)
        {
            enabled = false;
            return;
        }

        // Inspectorで設定した文字色を保存
        originalColor = promptText.color;

        // 開始時は透明
        currentAlpha = 0f;
        SetTextAlpha(0f);
    }

    private void Update()
    {
        if (gunShooter == null || promptText == null)
        {
            return;
        }

        // 弾切れ・装備中・リロード中ではない時に表示
        bool shouldShow =
            gunShooter.IsGunEquipped &&
            gunShooter.IsEmpty &&
            !gunShooter.IsReloading;

        float targetAlpha = shouldShow ? 1f : 0f;

        currentAlpha = Mathf.MoveTowards(
            currentAlpha,
            targetAlpha,
            Time.deltaTime / fadeDuration
        );

        SetTextAlpha(currentAlpha);
    }

    private void SetTextAlpha(float alpha)
    {
        Color color = originalColor;
        color.a = originalColor.a * alpha;

        promptText.color = color;
    }
}