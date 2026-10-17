using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 出撃前補充画面の「装備中の武器」専用プレビュー。
/// 装備変更はしない。町のEquipmentControllerからPrimaryWeaponItemを表示する。
/// 古いSceneで枠・アイコン・テキストの参照が未設定でも復旧する。
/// </summary>
[DisallowMultipleComponent]
public class ExpeditionEquippedWeaponPreview2D : MonoBehaviour
{
    [SerializeField] private EquipmentController equipment;
    [SerializeField] private Image weaponIcon;
    [SerializeField] private TMP_Text weaponNameText;
    [SerializeField] private float refreshInterval = 0.25f;

    private float nextRefresh;

    private void OnEnable()
    {
        nextRefresh = 0f;
        Refresh();
    }

    private void Update()
    {
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        Refresh();
    }

    public void Refresh()
    {
        EnsureWeaponSlotVisuals();
        FindEquipmentIfNeeded();

        InventoryItem equipped = equipment != null
            ? equipment.PrimaryWeaponItem : null;
        ItemData item = equipped != null ? equipped.ItemData : null;

        if (weaponNameText != null)
        {
            weaponNameText.gameObject.SetActive(true);
            weaponNameText.enabled = true;
            weaponNameText.text = item != null ? item.DisplayName : "武器：未装備";
        }

        if (weaponIcon != null)
        {
            weaponIcon.gameObject.SetActive(true);
            weaponIcon.sprite = item != null ? item.Icon : null;
            weaponIcon.enabled = weaponIcon.sprite != null;
            weaponIcon.color = Color.white;
            weaponIcon.preserveAspect = true;
            weaponIcon.raycastTarget = false;
        }
    }

    private void FindEquipmentIfNeeded()
    {
        if (equipment != null) return;

        // Inactiveも含む検索だけだと、別PlayerのEquipmentを拾う場合がある。
        // まず町の有効なPlayerを優先して探す。
        TownPlayerInventoryController town =
            FindAnyObjectByType<TownPlayerInventoryController>(FindObjectsInactive.Exclude);
        if (town == null)
            town = FindAnyObjectByType<TownPlayerInventoryController>(FindObjectsInactive.Include);

        if (town != null) equipment = town.EquipmentController;

        if (equipment == null)
            equipment = FindAnyObjectByType<EquipmentController>(FindObjectsInactive.Exclude);
        if (equipment == null)
            equipment = FindAnyObjectByType<EquipmentController>(FindObjectsInactive.Include);
    }

    private void EnsureWeaponSlotVisuals()
    {
        // この補充画面のWeaponSlotのみを対象にする（他の装備プレビューは変更しない）。
        if (gameObject.name != "WeaponSlot") return;

        Transform frame = transform.Find("WeaponGridFrame");
        if (frame == null)
        {
            GameObject go = new GameObject("WeaponGridFrame", typeof(RectTransform), typeof(Image), typeof(Outline));
            go.transform.SetParent(transform, false);
            RectTransform r = go.GetComponent<RectTransform>();
            SetTopLeft(r, 11f, 37f, 67f, 59f);
            Image bg = go.GetComponent<Image>();
            bg.color = new Color32(48, 52, 53, 255);
            bg.raycastTarget = false;
            Outline border = go.GetComponent<Outline>();
            border.effectColor = new Color32(119, 118, 112, 255);
            border.effectDistance = new Vector2(1f, -1f);
            frame = go.transform;
        }
        if (frame.GetSiblingIndex() != 0) frame.SetAsFirstSibling();
        if (!frame.gameObject.activeSelf) frame.gameObject.SetActive(true);

        if (weaponIcon == null)
        {
            Transform found = transform.Find("EquippedWeaponIcon");
            if (found != null) weaponIcon = found.GetComponent<Image>();
        }
        if (weaponIcon == null)
        {
            GameObject go = new GameObject("EquippedWeaponIcon", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            weaponIcon = go.GetComponent<Image>();
            SetTopLeft(weaponIcon.rectTransform, 16f, 42f, 57f, 49f);
            weaponIcon.enabled = false;
        }
        if (weaponNameText == null)
        {
            Transform found = transform.Find("EquippedWeaponName");
            if (found != null) weaponNameText = found.GetComponent<TMP_Text>();
        }
        if (weaponNameText == null)
        {
            GameObject go = new GameObject("EquippedWeaponName", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            TMP_Text text = go.GetComponent<TMP_Text>();
            text.text = "武器：未装備";
            text.fontSize = 16f;
            text.color = new Color32(230, 227, 218, 255);
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            SetTopLeft(text.rectTransform, 84f, 51f, 147f, 37f);
            weaponNameText = text;
        }
        // アイコン・武器名が背景に隠れないようにする。
        if (weaponIcon.transform.parent == transform) weaponIcon.transform.SetAsLastSibling();
        if (weaponNameText.transform.parent == transform) weaponNameText.transform.SetAsLastSibling();
    }

    private static void SetTopLeft(RectTransform r, float x, float y, float w, float h)
    {
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
    }
}
