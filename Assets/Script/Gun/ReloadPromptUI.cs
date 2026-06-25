using TMPro;
using UnityEngine;

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

    private GunShooter currentGunShooter;

    private Color originalColor;
    private float currentAlpha;
    private bool isSubscribed;

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

        originalColor = promptText.color;

        currentAlpha = 0f;
        SetTextAlpha(0f);
    }

    private void OnEnable()
    {
        SubscribeToEquipmentVisualController();
    }

    private void Start()
    {
        // 実行順の違いでOnEnable時に見つからなかった場合の保険
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
                ? "Reload \"R\""
                : "NoAmmo";
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

        // 銃を外した瞬間は、表示を消す
        if (currentGunShooter == null)
        {
            currentAlpha = 0f;
            SetTextAlpha(0f);
        }
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
}