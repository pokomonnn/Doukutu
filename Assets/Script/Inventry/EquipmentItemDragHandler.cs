using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(CanvasGroup))]
public class EquipmentItemDragHandler : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("ドラッグ中の見た目")]
    [SerializeField, Range(0.1f, 1f)]
    private float dragAlpha = 0.75f;

    [Header("入力設定")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;

    [Header("装備Itemの外ドロップ")]
    [Tooltip(
        "オンの場合、装備中ItemをInventory系UIの外で離すと、" +
        "Playerの足元へ地面Itemとして捨てます。"
    )]
    [SerializeField] private bool dropEquippedItemOutsideInventory = true;

    [Tooltip(
        "装備Itemを地面へ生成するPlayerItemDropper。" +
        "未設定ならPlayerから自動取得します。"
    )]
    [SerializeField] private PlayerItemDropper playerItemDropper;

    [Header("武器修理：装備武器の選択色")]
    [SerializeField]
    private Color repairSelectedBackgroundColor =
        new Color(0.18f, 0.55f, 0.28f, 0.96f);

    [SerializeField]
    private Color repairSelectedIconColor = Color.white;

    [Header("デバッグ")]
    [SerializeField] private bool showDropDebugLogs = false;

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

    private Image itemBackgroundImage;
    private Image itemIconImage;
    private bool lastRepairModeActive;
    private bool lastRepairSelected;

    private void Awake()
    {
        itemRect = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();

        FindEquipmentSlotUI();
        FindSoundPlayer();
        FindPlayerItemDropper();
        FindRepairVisualReferences();
    }

    private void Update()
    {
        RefreshRepairSelectionVisual();

        if (!isDragging)
        {
            return;
        }

        if (Input.GetKeyDown(rotateKey))
        {
            TryRotateDuringDrag();
        }
    }

    /// <summary>
    /// 装備スロット上のItemへカーソルを乗せた時も、
    /// 通常Inventoryと同じTooltipを表示します。
    /// </summary>
    public void OnPointerEnter(
        PointerEventData eventData)
    {
        if (isDragging ||
            !FindEquipmentSlotUI())
        {
            return;
        }

        InventoryItem item =
            equipmentSlotUI.GetEquippedItem();

        if (item == null ||
            item.ItemData == null)
        {
            return;
        }

        Canvas canvas =
            GetComponentInParent<Canvas>()?.rootCanvas;

        if (canvas == null)
        {
            return;
        }

        // 装備Itemは商人在庫ではないので、
        // merchantStockはnullで表示します。
        InventoryItemTooltipUI.Show(
            item,
            canvas,
            null
        );
    }

    public void OnPointerExit(
        PointerEventData eventData)
    {
        InventoryItem item =
            equipmentSlotUI != null
                ? equipmentSlotUI.GetEquippedItem()
                : null;

        InventoryItemTooltipUI.HideFor(item);
    }

    private void OnDisable()
    {
        InventoryItem item =
            equipmentSlotUI != null
                ? equipmentSlotUI.GetEquippedItem()
                : null;

        InventoryItemTooltipUI.HideFor(item);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isDragging ||
            !FindEquipmentSlotUI())
        {
            return;
        }

        InventoryItem item =
            equipmentSlotUI.GetEquippedItem();

        if (item == null ||
            item.ItemData == null)
        {
            return;
        }

        // 武器修理画面では、装備中の武器も左クリックで
        // 通常Inventoryと同じ選択集合へ追加／解除します。
        if (eventData.button ==
                PointerEventData.InputButton.Left &&
            item.ItemData is WeaponItemData)
        {
            MerchantWeaponRepairController
                repairController =
                    MerchantWeaponRepairController
                        .ActiveInstance;

            if (repairController != null &&
                repairController.IsOpen &&
                repairController
                    .TryToggleRepairSelectionFromEquipmentUI(
                        item
                    ))
            {
                RefreshRepairSelectionVisual(true);
                return;
            }
        }

        if (eventData.button !=
            PointerEventData.InputButton.Right)
        {
            return;
        }

        // ContextMenuとTooltipが重ならないよう閉じる。
        InventoryItemTooltipUI.HideFor(item);

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

        MerchantWeaponRepairController
            repairController =
                MerchantWeaponRepairController
                    .ActiveInstance;

        // 修理画面では装備Itemを移動させず、
        // 左クリックで修理対象を選ぶ操作を優先します。
        if (repairController != null &&
            repairController.IsOpen)
        {
            return;
        }

        // ドラッグ開始時はTooltipを消す。
        InventoryItemTooltipUI.HideFor(item);

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

        // -----------------------------------------------------
        // 1. 通常Inventoryへ戻す
        // -----------------------------------------------------
        if (TryFindTargetGrid(
                eventData,
                out InventoryGridUI targetGridUI,
                out Vector2Int pointerGridPosition))
        {
            Vector2Int targetPosition =
                pointerGridPosition - dragCellOffset;

            bool moved =
                equipmentSlotUI.TryUnequipToInventoryPosition(
                    targetPosition.x,
                    targetPosition.y,
                    dragIsRotated,
                    out EquipmentResult result
                );

            if (moved)
            {
                soundPlayer?.PlayPlace();

                LogDrop(
                    $"装備ItemをInventoryへ戻しました：" +
                    $"{equipmentSlotUI.GetEquippedItem()?.ItemData?.DisplayName}"
                );
            }
            else
            {
                Debug.Log(
                    $"装備解除できません：{result}",
                    this
                );

                soundPlayer?.PlayFailed();
            }

            FinishDrag();
            return;
        }

        // -----------------------------------------------------
        // 2. 装備枠・ItemBox Gridなど、Inventory系UIの上
        // -----------------------------------------------------
        // 有効な通常Inventory位置ではないだけで、
        // Inventory系UI上にいる場合は誤って地面へ捨てない。
        if (IsPointerOverInventoryRelatedUI(eventData))
        {
            soundPlayer?.PlayFailed();

            LogDrop(
                "Inventory系UI上なので地面ドロップしません。"
            );

            FinishDrag();
            return;
        }

        // -----------------------------------------------------
        // 3. Inventory系UIの完全な外 → 地面へ捨てる
        // -----------------------------------------------------
        if (dropEquippedItemOutsideInventory &&
            TryDropEquippedItemToWorld())
        {
            soundPlayer?.PlayPlace();

            LogDrop(
                "装備中ItemをInventory外へドラッグして地面へ捨てました。"
            );

            FinishDrag();
            return;
        }

        // CanDiscard=false / Quest Item / Dropper未設定など
        // 地面へ捨てられなかった場合は装備枠へ戻す。
        soundPlayer?.PlayFailed();

        LogDrop(
            "装備Itemを地面へ捨てられなかったため、装備枠へ戻します。"
        );

        FinishDrag();
    }

    private bool TryDropEquippedItemToWorld()
    {
        if (!FindEquipmentSlotUI() ||
            !FindPlayerItemDropper())
        {
            return false;
        }

        EquipmentController equipmentController =
            equipmentSlotUI.EquipmentControllerRef;

        if (equipmentController == null)
        {
            return false;
        }

        InventoryItem item =
            equipmentSlotUI.GetEquippedItem();

        if (item == null || item.ItemData == null)
        {
            return false;
        }

        // PlayerItemDropper側で
        // CanDiscard=false / Quest Item を既存仕様どおり拒否します。
        return playerItemDropper.TryDropEquippedItem(
            equipmentController,
            equipmentSlotUI.SlotType
        );
    }

    private bool IsPointerOverInventoryRelatedUI(
        PointerEventData eventData)
    {
        if (eventData == null ||
            EventSystem.current == null)
        {
            return false;
        }

        System.Collections.Generic.List<RaycastResult> results =
            new System.Collections.Generic.List<RaycastResult>();

        EventSystem.current.RaycastAll(
            eventData,
            results
        );

        foreach (RaycastResult result in results)
        {
            GameObject target = result.gameObject;

            if (target == null)
            {
                continue;
            }

            // 通常Inventory / ItemBox Inventory
            if (target.GetComponentInParent<InventoryGridUI>() != null)
            {
                return true;
            }

            // PrimaryWeapon / Helmetなどの装備枠
            if (target.GetComponentInParent<EquipmentSlotUI>() != null)
            {
                return true;
            }

            // Context Menu上で離した時も誤廃棄しない。
            if (target.GetComponentInParent<InventoryContextMenuUI>() != null)
            {
                return true;
            }
        }

        return false;
    }

    private bool FindPlayerItemDropper()
    {
        if (playerItemDropper != null)
        {
            return true;
        }

        if (FindEquipmentSlotUI())
        {
            InventoryController inventoryController =
                equipmentSlotUI.InventoryController;

            if (inventoryController != null)
            {
                playerItemDropper =
                    inventoryController.GetComponent<PlayerItemDropper>();

                if (playerItemDropper == null)
                {
                    playerItemDropper =
                        inventoryController.GetComponentInParent<
                            PlayerItemDropper
                        >();
                }
            }
        }

        if (playerItemDropper == null)
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                playerItemDropper =
                    player.GetComponent<PlayerItemDropper>();
            }
        }

        if (playerItemDropper == null)
        {
            playerItemDropper =
                Object.FindAnyObjectByType<PlayerItemDropper>(
                    FindObjectsInactive.Include
                );
        }

        return playerItemDropper != null;
    }

    private void LogDrop(string message)
    {
        if (!showDropDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"[EquipmentItemDragHandler] {message}",
            this
        );
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

    private void RefreshRepairSelectionVisual(
        bool force = false)
    {
        if (!FindEquipmentSlotUI())
        {
            return;
        }

        InventoryItem item =
            equipmentSlotUI.GetEquippedItem();

        MerchantWeaponRepairController
            repairController =
                MerchantWeaponRepairController
                    .ActiveInstance;

        bool repairModeActive =
            repairController != null &&
            repairController.IsOpen;

        bool selected =
            repairModeActive &&
            item != null &&
            item.ItemData is WeaponItemData &&
            repairController
                .IsItemSelectedForRepair(item);

        // 選択中はEquipmentSlotUI側のRefreshで色が戻されても
        // 次のFrameに必ず選択色を再適用します。
        if (selected)
        {
            FindRepairVisualReferences();

            if (itemBackgroundImage != null)
            {
                itemBackgroundImage.color =
                    repairSelectedBackgroundColor;
            }

            if (itemIconImage != null)
            {
                itemIconImage.color =
                    repairSelectedIconColor;
            }
        }
        else if (force ||
                 lastRepairSelected ||
                 lastRepairModeActive != repairModeActive)
        {
            // 選択解除／修理画面Close時は
            // EquipmentSlotUIの通常色へ戻します。
            equipmentSlotUI.RefreshSlotVisual();
        }

        lastRepairModeActive =
            repairModeActive;

        lastRepairSelected =
            selected;
    }

    private void FindRepairVisualReferences()
    {
        if (itemBackgroundImage == null)
        {
            itemBackgroundImage =
                GetComponent<Image>();
        }

        if (itemIconImage == null)
        {
            Transform iconTransform =
                transform.Find("Icon");

            if (iconTransform != null)
            {
                itemIconImage =
                    iconTransform.GetComponent<Image>();
            }
        }
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