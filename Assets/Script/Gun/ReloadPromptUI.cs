using TMPro;
using UnityEngine;
using UnityEngine.Localization;

[RequireComponent(typeof(TextMeshPro))]
public class ReloadPromptUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private PlayerEquipmentVisualController
        equipmentVisualController;

    [SerializeField] private TextMeshPro promptText;

    [Header("フェード設定")]
    [Tooltip("表示・非表示が切り替わるまでの秒数")]
    [SerializeField, Min(0.01f)]
    private float fadeDuration = 0.25f;

    [Header("翻訳")]
    [Tooltip("GameText の hud.reload を設定")]
    [SerializeField]
    private LocalizedString reloadPromptText =
        new LocalizedString();

    [Tooltip("GameText の hud.no_ammo を設定")]
    [SerializeField]
    private LocalizedString noAmmoPromptText =
        new LocalizedString();

    private GunShooter currentGunShooter;

    private Color originalColor;
    private float currentAlpha;
    private bool isSubscribed;
    private bool isPromptTextSubscribed;

    private string localizedReloadPrompt = "リロード「R」";
    private string localizedNoAmmoPrompt = "弾薬がありません";

    private void Awake()
    {
        EnsureLocalizedStrings();

        if (promptText == null)
        {
            promptText = GetComponent<TextMeshPro>();
        }

        if (promptText == null)
        {
            enabled = false;
            return;
        }

        originalColor = promptText.color;

        currentAlpha = 0f;
        SetTextAlpha(0f);
    }

    private void OnEnable()
    {
        SubscribePromptTexts();
        SubscribeToEquipmentVisualController();
    }

    private void Start()
    {
        SubscribePromptTexts();
        SubscribeToEquipmentVisualController();

        if (equipmentVisualController != null)
        {
            SetCurrentGun(
                equipmentVisualController.CurrentGunShooter
            );
        }
    }

    private void OnDisable()
    {
        UnsubscribeFromEquipmentVisualController();
        UnsubscribePromptTexts();
    }

    private void Update()
    {
        bool shouldShow =
            currentGunShooter != null &&
            currentGunShooter.isActiveAndEnabled &&
            currentGunShooter.IsGunEquipped &&
            currentGunShooter.IsEmpty &&
            !currentGunShooter.IsReloading;

        if (shouldShow && promptText != null)
        {
            promptText.text = currentGunShooter.HasReserveAmmo
                ? localizedReloadPrompt
                : localizedNoAmmoPrompt;
        }

        float targetAlpha = shouldShow ? 1f : 0f;

        currentAlpha = Mathf.MoveTowards(
            currentAlpha,
            targetAlpha,
            Time.deltaTime / fadeDuration
        );

        SetTextAlpha(currentAlpha);
    }

    private void HandleActiveGunChanged(
        GunShooter newGunShooter)
    {
        SetCurrentGun(newGunShooter);
    }

    private void SetCurrentGun(GunShooter newGunShooter)
    {
        currentGunShooter = newGunShooter;

        if (currentGunShooter == null)
        {
            currentAlpha = 0f;
            SetTextAlpha(0f);
        }
    }

    private void SubscribePromptTexts()
    {
        EnsureLocalizedStrings();

        if (isPromptTextSubscribed)
        {
            return;
        }

        reloadPromptText.StringChanged +=
            HandleReloadPromptChanged;

        noAmmoPromptText.StringChanged +=
            HandleNoAmmoPromptChanged;

        isPromptTextSubscribed = true;
    }

    private void UnsubscribePromptTexts()
    {
        if (!isPromptTextSubscribed)
        {
            return;
        }

        reloadPromptText.StringChanged -=
            HandleReloadPromptChanged;

        noAmmoPromptText.StringChanged -=
            HandleNoAmmoPromptChanged;

        isPromptTextSubscribed = false;
    }

    private void HandleReloadPromptChanged(
        string localizedText)
    {
        localizedReloadPrompt =
            string.IsNullOrWhiteSpace(localizedText)
                ? "リロード「R」"
                : localizedText;
    }

    private void HandleNoAmmoPromptChanged(
        string localizedText)
    {
        localizedNoAmmoPrompt =
            string.IsNullOrWhiteSpace(localizedText)
                ? "弾薬がありません"
                : localizedText;
    }

    private void SubscribeToEquipmentVisualController()
    {
        if (isSubscribed)
        {
            return;
        }

        if (!FindEquipmentVisualController())
        {
            return;
        }

        equipmentVisualController.OnActiveGunChanged +=
            HandleActiveGunChanged;

        isSubscribed = true;

        SetCurrentGun(
            equipmentVisualController.CurrentGunShooter
        );
    }

    private void UnsubscribeFromEquipmentVisualController()
    {
        if (!isSubscribed ||
            equipmentVisualController == null)
        {
            return;
        }

        equipmentVisualController.OnActiveGunChanged -=
            HandleActiveGunChanged;

        isSubscribed = false;
    }

    private bool FindEquipmentVisualController()
    {
        if (equipmentVisualController != null)
        {
            return true;
        }

        equipmentVisualController =
            FindAnyObjectByType<
                PlayerEquipmentVisualController
            >(FindObjectsInactive.Include);

        return equipmentVisualController != null;
    }

    private void SetTextAlpha(float alpha)
    {
        if (promptText == null)
        {
            return;
        }

        Color color = originalColor;
        color.a = originalColor.a * alpha;

        promptText.color = color;
    }

    private void EnsureLocalizedStrings()
    {
        if (reloadPromptText == null)
        {
            reloadPromptText = new LocalizedString();
        }

        if (noAmmoPromptText == null)
        {
            noAmmoPromptText = new LocalizedString();
        }
    }

    private void OnValidate()
    {
        fadeDuration = Mathf.Max(0.01f, fadeDuration);
    }
}