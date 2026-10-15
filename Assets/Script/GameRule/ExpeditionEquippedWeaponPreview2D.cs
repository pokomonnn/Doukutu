using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>出撃画面の武器枠に、現在装備中のPrimary Weaponを表示。装備変更は従来Inventory UIで行う。</summary>
public class ExpeditionEquippedWeaponPreview2D : MonoBehaviour
{
    [SerializeField] private EquipmentController equipment;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TMP_Text weaponNameText;
    private float nextRefresh;

    private void OnEnable() { Refresh(); }
    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + 0.25f;
        Refresh();
    }
    public void Refresh()
    {
        if (equipment == null)
        {
            TownPlayerInventoryController town = FindAnyObjectByType<TownPlayerInventoryController>(FindObjectsInactive.Include);
            if (town != null) equipment = town.EquipmentController;
        }
        if (equipment == null) equipment = FindAnyObjectByType<EquipmentController>(FindObjectsInactive.Include);
        InventoryItem equipped = equipment != null ? equipment.PrimaryWeaponItem : null;
        ItemData item = equipped != null ? equipped.ItemData : null;
        if (weaponNameText != null) weaponNameText.text = item != null ? item.DisplayName : "武器：未装備";
        if (weaponIcon != null)
        {
            weaponIcon.sprite = item != null ? item.Icon : null;
            weaponIcon.enabled = weaponIcon.sprite != null;
            weaponIcon.color = Color.white;
            weaponIcon.preserveAspect = true;
        }
    }
}
