using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 最初のドラッグからEquipmentSlotUI.CanPreviewEquipの結果を可視化する。
/// 他のアイテム移動や売買判定は変えない。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(EquipmentSlotUI))]
public sealed class TownEquipmentSlotPreview2D : MonoBehaviour
{
    [SerializeField] private Color allowedColor = new Color(0.20f, 0.60f, 0.34f, 0.32f);
    [SerializeField] private Color blockedColor = new Color(0.68f, 0.20f, 0.19f, 0.30f);
    private EquipmentSlotUI slot;
    private Image overlay;

    private void Awake() { EnsureOverlay(); }
    private void OnEnable() { EnsureOverlay(); }
    private void LateUpdate()
    {
        if (slot == null || overlay == null) EnsureOverlay();
        if (slot == null || overlay == null) return;
        InventoryItemUI drag = InventoryItemUI.ActiveDragUI;
        bool hover = drag != null && drag.DragSourceGrid != null;
        if (hover)
        {
            RectTransform rect = slot.transform as RectTransform;
            Canvas canvas = slot.GetComponentInParent<Canvas>();
            Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera : null;
            hover = rect != null && RectTransformUtility.RectangleContainsScreenPoint(
                rect, Input.mousePosition, cam);
        }
        if (hover)
        {
            bool valid = slot.CanPreviewEquip(drag.Item, drag.DragRotationSteps, drag.DragSourceGrid);
            overlay.color = valid ? allowedColor : blockedColor;
            overlay.enabled = true;
            overlay.transform.SetAsLastSibling();
        }
        else overlay.enabled = false;
    }
    private void OnDisable() { if (overlay != null) overlay.enabled = false; }
    private void EnsureOverlay()
    {
        if (slot == null) slot = GetComponent<EquipmentSlotUI>();
        if (overlay != null) return;
        Transform existing = transform.Find("TownEquipmentDropPreview");
        GameObject go;
        if (existing != null) go = existing.gameObject;
        else
        {
            go = new GameObject("TownEquipmentDropPreview", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
        }
        RectTransform r = go.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        r.localScale = Vector3.one;
        overlay = go.GetComponent<Image>();
        overlay.raycastTarget = false;
        overlay.enabled = false;
    }
}
