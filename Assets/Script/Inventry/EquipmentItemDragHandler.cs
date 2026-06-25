using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class EquipmentItemDragHandler : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler
{
    [Header("ドラッグ中の見た目")]
    [SerializeField, Range(0.1f, 1f)]
    private float dragAlpha = 0.75f;

    [Header("入力設定")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;

    private EquipmentSlotUI equipmentSlotUI;
    private InventorySoundPlayer soundPlayer;

    private RectTransform itemRect;
    private CanvasGroup canvasGroup;

    private Canvas rootCanvas;
    private Transform originalParent;

    private Vector2 dragPointerOffset;
    private Vector2Int dragCellOffset;

    private bool isDragging;
    private bool dragIsRotated;

    private InventoryContextMenuUI contextMenuUI;

    private void Awake()
    {
        itemRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        FindEquipmentSlotUI();
        FindSoundPlayer();
    }

    private void Update()
    {
        if (!isDragging)
        {
            return;
        }

        if (Input.GetKeyDown(rotateKey))
        {
            TryRotateDuringDrag();
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button !=
                PointerEventData.InputButton.Right ||
            isDragging)
        {
            return;
        }

        if (!FindEquipmentSlotUI())
        {
            return;
        }

        InventoryItem item =
            equipmentSlotUI.GetEquippedItem();

        if (item == null || item.ItemData == null)
        {
            return;
        }

        if (!FindContextMenuUI())
        {
            soundPlayer?.PlayFailed();

            Debug.LogWarning(
                "EquipmentItemDragHandler: " +
                "InventoryContextMenuUI が見つかりません。",
                this
            );

            return;
        }

        contextMenuUI.ShowEquippedItem(
            item,
            equipmentSlotUI,
            eventData.position
        );
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button !=
            PointerEventData.InputButton.Left)
        {
            return;
        }

        if (!FindEquipmentSlotUI())
        {
            return;
        }

        InventoryItem item =
            equipmentSlotUI.GetEquippedItem();

        if (item == null || item.ItemData == null)
        {
            return;
        }

        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        if (rootCanvas == null)
        {
            Debug.LogWarning(
                "EquipmentItemDragHandler: Canvas が見つかりません。",
                this
            );
            return;
        }

        FindSoundPlayer();

        isDragging = true;
        dragIsRotated = item.IsRotated;
        originalParent = transform.parent;

        CalculateDragCellOffset(eventData, item);

        if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    itemRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out dragPointerOffset))
        {
            dragPointerOffset = Vector2.zero;
        }

        canvasGroup.alpha = dragAlpha;
        canvasGroup.blocksRaycasts = false;

        transform.SetParent(rootCanvas.transform, false);

        itemRect.anchorMin = new Vector2(0.5f, 0.5f);
        itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0f, 1f);

        transform.SetAsLastSibling();

        UpdateDragPosition(eventData.position);

        soundPlayer?.PlayPickUp();
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        UpdateDragPosition(eventData.position);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!isDragging)
        {
            return;
        }

        bool moved = false;

        if (TryFindTargetGrid(
                eventData,
                out InventoryGridUI targetGridUI,
                out Vector2Int pointerGridPosition))
        {
            Vector2Int targetPosition =
                pointerGridPosition - dragCellOffset;

            moved =
                equipmentSlotUI.TryUnequipToInventoryPosition(
                    targetPosition.x,
                    targetPosition.y,
                    dragIsRotated,
                    out EquipmentResult result
                );

            if (moved)
            {
                soundPlayer?.PlayPlace();
            }
            else
            {
                Debug.Log(
                    $"装備解除できません：{result}",
                    this
                );

                soundPlayer?.PlayFailed();
            }
        }
        else
        {
            soundPlayer?.PlayFailed();
        }

        FinishDrag();
    }

    private void TryRotateDuringDrag()
    {
        if (!FindEquipmentSlotUI())
        {
            return;
        }

        InventoryItem item =
            equipmentSlotUI.GetEquippedItem();

        if (item == null ||
            item.ItemData == null ||
            !item.CanRotate)
        {
            soundPlayer?.PlayFailed();
            return;
        }

        bool previousRotation = dragIsRotated;
        Vector2Int previousSize =
            item.ItemData.GetSize(previousRotation);

        dragIsRotated = !dragIsRotated;

        Vector2Int newSize =
            item.ItemData.GetSize(dragIsRotated);

        if (!previousRotation && dragIsRotated)
        {
            dragCellOffset = new Vector2Int(
                previousSize.y - 1 - dragCellOffset.y,
                dragCellOffset.x
            );
        }
        else
        {
            dragCellOffset = new Vector2Int(
                dragCellOffset.y,
                previousSize.x - 1 - dragCellOffset.x
            );
        }

        dragCellOffset.x = Mathf.Clamp(
            dragCellOffset.x,
            0,
            newSize.x - 1
        );

        dragCellOffset.y = Mathf.Clamp(
            dragCellOffset.y,
            0,
            newSize.y - 1
        );

        equipmentSlotUI.SetDragVisualRotation(
            dragIsRotated
        );

        UpdateDragPosition(Input.mousePosition);

        soundPlayer?.PlayRotate();
    }

    private void CalculateDragCellOffset(
        PointerEventData eventData,
        InventoryItem item)
    {
        dragCellOffset = Vector2Int.zero;

        if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    itemRect,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 localPoint))
        {
            return;
        }

        float step =
            equipmentSlotUI.CellSize +
            equipmentSlotUI.CellSpacing;

        if (step <= 0f)
        {
            return;
        }

        int x = Mathf.FloorToInt(
            Mathf.Max(0f, localPoint.x) / step
        );

        int y = Mathf.FloorToInt(
            Mathf.Max(0f, -localPoint.y) / step
        );

        Vector2Int itemSize =
            item.ItemData.GetSize(item.IsRotated);

        dragCellOffset = new Vector2Int(
            Mathf.Clamp(x, 0, itemSize.x - 1),
            Mathf.Clamp(y, 0, itemSize.y - 1)
        );
    }

    private bool TryFindTargetGrid(
        PointerEventData eventData,
        out InventoryGridUI targetGridUI,
        out Vector2Int pointerGridPosition)
    {
        targetGridUI = null;
        pointerGridPosition = Vector2Int.zero;

        if (!FindEquipmentSlotUI())
        {
            return false;
        }

        InventoryController targetInventory =
            equipmentSlotUI.InventoryController;

        if (targetInventory == null)
        {
            return false;
        }

        InventoryGridUI[] gridUIs =
    Object.FindObjectsByType<InventoryGridUI>(
        FindObjectsInactive.Exclude
    );

        foreach (InventoryGridUI gridUI in gridUIs)
        {
            if (gridUI == null ||
                !gridUI.isActiveAndEnabled)
            {
                continue;
            }

            if (!gridUI.TryGetGridPosition(
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2Int gridPosition))
            {
                continue;
            }

            if (gridUI.Controller != targetInventory)
            {
                continue;
            }

            targetGridUI = gridUI;
            pointerGridPosition = gridPosition;

            return true;
        }

        return false;
    }

    private void UpdateDragPosition(Vector2 screenPosition)
    {
        if (rootCanvas == null)
        {
            return;
        }

        Camera canvasCamera =
            rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

        if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    rootCanvas.transform as RectTransform,
                    screenPosition,
                    canvasCamera,
                    out Vector2 canvasLocalPosition))
        {
            return;
        }

        itemRect.anchoredPosition =
            canvasLocalPosition - dragPointerOffset;
    }

    private void FinishDrag()
    {
        isDragging = false;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        if (originalParent != null)
        {
            transform.SetParent(originalParent, false);
        }

        equipmentSlotUI?.RefreshSlotVisual();
    }

    private bool FindEquipmentSlotUI()
    {
        if (equipmentSlotUI != null)
        {
            return true;
        }

        equipmentSlotUI =
            GetComponentInParent<EquipmentSlotUI>();

        return equipmentSlotUI != null;
    }

    private void FindSoundPlayer()
    {
        if (soundPlayer != null)
        {
            return;
        }

        soundPlayer =
            GetComponentInParent<InventorySoundPlayer>();

        if (soundPlayer == null)
        {
            soundPlayer =
                Object.FindAnyObjectByType<
                    InventorySoundPlayer
                >(FindObjectsInactive.Include);
        }
    }

    private bool FindContextMenuUI()
    {
        if (contextMenuUI != null)
        {
            return true;
        }

        Canvas canvas =
            GetComponentInParent<Canvas>()?.rootCanvas;

        if (canvas != null)
        {
            contextMenuUI =
                canvas.GetComponentInChildren<
                    InventoryContextMenuUI
                >(true);
        }

        if (contextMenuUI == null)
        {
            contextMenuUI =
                Object.FindAnyObjectByType<
                    InventoryContextMenuUI
                >(FindObjectsInactive.Include);
        }

        return contextMenuUI != null;
    }
}