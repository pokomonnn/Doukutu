using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>インベントリ既存のアイテム説明Tooltipを医療キットのセルでも使う。</summary>
public class MedicalKitItemHover2D : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private InventoryItem item;
    private Canvas rootCanvas;

    public void Bind(InventoryItem target, Canvas canvas)
    {
        item = target;
        rootCanvas = canvas != null ? canvas.rootCanvas : null;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item != null && rootCanvas != null)
            InventoryItemTooltipUI.Show(item, rootCanvas);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        InventoryItemTooltipUI.HideFor(item);
    }

    private void OnDisable() => InventoryItemTooltipUI.HideFor(item);
}
