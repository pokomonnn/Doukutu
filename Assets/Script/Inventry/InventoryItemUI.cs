using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class InventoryItemUI : MonoBehaviour,
    IBeginDragHandler,
    IDragHandler,
    IEndDragHandler,
    IPointerClickHandler
{
    [Header("見た目")]
    [SerializeField]
    private Color backgroundColor =
        new Color(0.18f, 0.30f, 0.38f, 0.92f);

    [SerializeField, Min(0f)] private float iconPadding = 5f;
    [SerializeField] private bool showStackAmount = true;

    [Header("ドラッグ中の見た目")]
    [SerializeField, Range(0.1f, 1f)] private float dragAlpha = 0.75f;

    [Header("入力設定")]
    [SerializeField] private KeyCode rotateKey = KeyCode.R;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private InventoryItem inventoryItem;
    private InventoryGridUI gridUI;
    private InventorySoundPlayer soundPlayer;

    private RectTransform itemRect;
    private Image backgroundImage;
    private Image iconImage;
    private Text amountText;
    private CanvasGroup canvasGroup;

    private Canvas rootCanvas;
    private Transform originalParent;

    // 掴んだ場所がアイテム内の何マス目か
    private Vector2Int dragCellOffset;

    // マウス位置とアイテム左上のズレ
    private Vector2 dragPointerOffset;

    private bool isDragging;

    // ドラッグ中だけ使用する「仮の向き」
    // ドロップ成功時だけ、本当のInventoryItemへ反映する
    private bool dragIsRotated;

    private static Sprite defaultSprite;

    public InventoryItem Item => inventoryItem;

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

    public void Setup(InventoryItem item, InventoryGridUI ownerGridUI)
    {
        inventoryItem = item;
        gridUI = ownerGridUI;

        FindSoundPlayer();
        EnsureVisuals();

        // ドラッグ中に更新イベントが来ても
        // 位置を元の場所へ戻さない
        if (!isDragging)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        if (inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null ||
            !gridUI.ContainsItem(inventoryItem))
        {
            gameObject.SetActive(false);
            return;
        }

        gameObject.SetActive(true);

        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(0f, 1f);
        itemRect.pivot = new Vector2(0f, 1f);

        itemRect.anchoredPosition = gridUI.GetCellPosition(
            inventoryItem.GridX,
            inventoryItem.GridY
        );

        ApplyVisuals(inventoryItem.IsRotated);

        gameObject.name = $"ItemUI_{inventoryItem.ItemData.ItemId}";
    }

    // 右クリックでコンテキストメニューを開く
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Right ||
            isDragging ||
            inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null)
        {
            return;
        }

        InventoryContextMenuUI contextMenuUI =
            gridUI.ContextMenuUI;

        if (contextMenuUI == null)
        {
            Debug.LogWarning(
                "InventoryItemUI: ContextMenuUI が設定されていません。",
                this
            );

            soundPlayer?.PlayFailed();
            return;
        }

        // プレイヤーインベントリでは従来どおり装備・使用・捨てる。
        // 箱・ショップ在庫側は、誤操作防止のため詳細表示だけにする。
        if (gridUI.IsPlayerInventory &&
            gridUI.Controller != null)
        {
            contextMenuUI.Show(
                inventoryItem,
                gridUI.Controller,
                eventData.position
            );
        }
        else
        {
            contextMenuUI.ShowReadOnlyItem(
                inventoryItem,
                eventData.position
            );
        }

        Log(
            $"右クリックメニューを開きました：" +
            $"{inventoryItem.ItemData.DisplayName}"
        );
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left ||
            inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null ||
            !gridUI.ContainsItem(inventoryItem))
        {
            return;
        }

        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        if (rootCanvas == null)
        {
            Debug.LogWarning(
                "InventoryItemUI: 親階層にCanvasが見つかりません。",
                this
            );
            return;
        }

        isDragging = true;
        originalParent = transform.parent;

        // ドラッグ開始時の向きを保存
        dragIsRotated = inventoryItem.IsRotated;

        // 掴んだマスを保存
        if (gridUI.TryGetGridPosition(
                eventData.position,
                eventData.pressEventCamera,
                out Vector2Int clickedGridPosition))
        {
            dragCellOffset =
                clickedGridPosition - inventoryItem.GridPosition;

            Vector2Int itemSize = GetSize(dragIsRotated);

            dragCellOffset.x = Mathf.Clamp(
                dragCellOffset.x,
                0,
                itemSize.x - 1
            );

            dragCellOffset.y = Mathf.Clamp(
                dragCellOffset.y,
                0,
                itemSize.y - 1
            );
        }
        else
        {
            dragCellOffset = Vector2Int.zero;
        }

        // マウスで掴んだ位置を保存
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                itemRect,
                eventData.position,
                eventData.pressEventCamera,
                out dragPointerOffset))
        {
            dragPointerOffset = Vector2.zero;
        }

        canvasGroup.alpha = dragAlpha;
        canvasGroup.blocksRaycasts = false;

        // ドラッグ中はCanvas直下へ出して最前面にする
        transform.SetParent(rootCanvas.transform, false);

        itemRect.anchorMin = new Vector2(0.5f, 0.5f);
        itemRect.anchorMax = new Vector2(0.5f, 0.5f);
        itemRect.pivot = new Vector2(0f, 1f);

        ApplyVisuals(dragIsRotated);

        transform.SetAsLastSibling();

        UpdateDragPosition(eventData.position);

        soundPlayer?.PlayPickUp();

        Log(
            $"ドラッグ開始：{inventoryItem.ItemData.DisplayName} / " +
            $"向き={(dragIsRotated ? "回転" : "通常")} / " +
            $"サイズ={GetSize(dragIsRotated).x}×" +
            $"{GetSize(dragIsRotated).y}"
        );
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

        // 先に装備スロットへのドロップを判定する
        if (TryDropToEquipmentSlot(
                eventData,
                out bool wasOverEquipmentSlot))
        {
            soundPlayer?.PlayPlace();

            Log(
                $"装備成功：{inventoryItem.ItemData.DisplayName}"
            );

            FinishDrag();
            return;
        }

        // 装備スロットの上にはいたが、
        // 種類違い・枠が埋まっているなどで失敗した場合
        if (wasOverEquipmentSlot)
        {
            soundPlayer?.PlayFailed();
            FinishDrag();
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

            if (targetGridUI == gridUI)
            {
                moved = gridUI.TryMoveItem(
                    inventoryItem,
                    targetPosition.x,
                    targetPosition.y,
                    dragIsRotated
                );
            }
            else
            {
                moved = gridUI.TryTransferItemTo(
                    inventoryItem,
                    targetGridUI,
                    targetPosition.x,
                    targetPosition.y,
                    dragIsRotated
                );
            }

            if (moved)
            {
                soundPlayer?.PlayPlace();

                Log(
                    $"ドロップ成功：{inventoryItem.ItemData.DisplayName} / " +
                    $"位置={targetPosition.x},{targetPosition.y}"
                );
            }
            else
            {
                soundPlayer?.PlayFailed();

                Log(
                    $"ドロップ失敗：{inventoryItem.ItemData.DisplayName}"
                );
            }
        }
        else
        {
            soundPlayer?.PlayFailed();
            Log("ドロップ失敗：グリッド外です。");
        }

        FinishDrag();
    }

    private bool TryDropToEquipmentSlot(
        PointerEventData eventData,
        out bool wasOverEquipmentSlot)
    {
        wasOverEquipmentSlot = false;

        // 箱から直接装備はさせず、いったんプレイヤー側へ移してから装備する。
        if (gridUI == null || !gridUI.IsPlayerInventory ||
            EventSystem.current == null)
        {
            return false;
        }

        List<RaycastResult> results =
            new List<RaycastResult>();

        EventSystem.current.RaycastAll(
            eventData,
            results
        );

        foreach (RaycastResult raycastResult in results)
        {
            if (raycastResult.gameObject == null)
            {
                continue;
            }

            EquipmentSlotUI equipmentSlotUI =
                raycastResult.gameObject
                    .GetComponentInParent<EquipmentSlotUI>();

            if (equipmentSlotUI == null)
            {
                continue;
            }

            wasOverEquipmentSlot = true;

            bool equipped =
                equipmentSlotUI.TryEquipDroppedItem(
                    inventoryItem,
                    dragIsRotated,
                    out EquipmentResult result
                );

            if (!equipped)
            {
                Log(
                    $"装備スロットへのドロップ失敗：" +
                    $"{inventoryItem.ItemData.DisplayName} / {result}"
                );
            }

            return equipped;
        }

        return false;
    }

    private bool TryFindTargetGrid(
        PointerEventData eventData,
        out InventoryGridUI targetGridUI,
        out Vector2Int pointerGridPosition)
    {
        targetGridUI = null;
        pointerGridPosition = Vector2Int.zero;

        InventoryGridUI[] allGridUIs =
            Object.FindObjectsByType<InventoryGridUI>(
                FindObjectsInactive.Exclude
            );

        foreach (InventoryGridUI candidate in allGridUIs)
        {
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.TryGetGridPosition(
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2Int gridPosition))
            {
                continue;
            }

            targetGridUI = candidate;
            pointerGridPosition = gridPosition;
            return true;
        }

        return false;
    }

    private void TryRotateDuringDrag()
    {
        if (inventoryItem == null ||
            inventoryItem.ItemData == null)
        {
            return;
        }

        if (!inventoryItem.CanRotate)
        {
            soundPlayer?.PlayFailed();

            Log(
                $"Rキー検知：{inventoryItem.ItemData.DisplayName} は " +
                "Can Rotate がオフです。"
            );

            return;
        }

        bool previousRotation = dragIsRotated;
        Vector2Int previousSize = GetSize(previousRotation);

        dragIsRotated = !dragIsRotated;

        Vector2Int newSize = GetSize(dragIsRotated);

        // 掴んでいるマスの位置を、回転後の形に合わせる
        if (!previousRotation && dragIsRotated)
        {
            // 通常 → 90度回転
            dragCellOffset = new Vector2Int(
                previousSize.y - 1 - dragCellOffset.y,
                dragCellOffset.x
            );
        }
        else
        {
            // 90度回転 → 通常
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

        // 表示だけを回転する
        // InventoryItem本体はドロップ成功まで変更しない
        ApplyVisuals(dragIsRotated);

        Vector2 mousePosition = new Vector2(
            Input.mousePosition.x,
            Input.mousePosition.y
        );

        UpdateDragPosition(mousePosition);

        soundPlayer?.PlayRotate();

        Log(
            $"Rキー回転成功：{inventoryItem.ItemData.DisplayName} / " +
            $"サイズ {previousSize.x}×{previousSize.y} → " +
            $"{newSize.x}×{newSize.y}"
        );
    }

    private void UpdateDragPosition(Vector2 screenPosition)
    {
        if (rootCanvas == null)
        {
            return;
        }

        Camera canvasCamera = rootCanvas.renderMode ==
            RenderMode.ScreenSpaceOverlay
            ? null
            : rootCanvas.worldCamera;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
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
        else if (gridUI != null)
        {
            transform.SetParent(gridUI.ItemRoot, false);
        }

        transform.SetAsLastSibling();

        Refresh();
    }

    private Vector2Int GetSize(bool isRotated)
    {
        if (inventoryItem == null || inventoryItem.ItemData == null)
        {
            return Vector2Int.one;
        }

        return inventoryItem.ItemData.GetSize(isRotated);
    }

    private void ApplyVisuals(bool isRotated)
    {
        if (inventoryItem == null ||
            inventoryItem.ItemData == null ||
            gridUI == null)
        {
            return;
        }

        Vector2Int itemSize = GetSize(isRotated);

        itemRect.sizeDelta = gridUI.GetItemPixelSize(
            itemSize.x,
            itemSize.y
        );

        backgroundImage.color = backgroundColor;

        Sprite icon = inventoryItem.ItemData.Icon;

        iconImage.sprite = icon != null
            ? icon
            : GetDefaultSprite();

        iconImage.color = icon != null
            ? Color.white
            : new Color(0.65f, 0.65f, 0.65f, 1f);

        iconImage.preserveAspect = true;

        iconImage.rectTransform.localRotation = Quaternion.Euler(
            0f,
            0f,
            isRotated ? -90f : 0f
        );

        bool shouldShowAmount =
            showStackAmount &&
            inventoryItem.CanStack;

        amountText.gameObject.SetActive(shouldShowAmount);

        if (shouldShowAmount)
        {
            amountText.text = inventoryItem.Amount.ToString();
        }
    }

    private void FindSoundPlayer()
    {
        if (soundPlayer != null)
        {
            return;
        }

        if (gridUI != null)
        {
            soundPlayer = gridUI.GetComponent<InventorySoundPlayer>();
        }

        if (soundPlayer == null)
        {
            soundPlayer = GetComponentInParent<InventorySoundPlayer>();
        }

        if (soundPlayer == null)
        {
            soundPlayer = FindAnyObjectByType<InventorySoundPlayer>(
                FindObjectsInactive.Include
            );
        }
    }

    private void EnsureVisuals()
    {
        itemRect = GetComponent<RectTransform>();

        backgroundImage = GetComponent<Image>();

        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();

            if (canvasGroup == null)
            {
                canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        backgroundImage.sprite = GetDefaultSprite();
        backgroundImage.raycastTarget = true;

        if (iconImage == null)
        {
            GameObject iconObject = new GameObject(
                "Icon",
                typeof(RectTransform),
                typeof(Image)
            );

            iconObject.transform.SetParent(transform, false);

            RectTransform iconRect =
                iconObject.GetComponent<RectTransform>();

            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.offsetMin = new Vector2(iconPadding, iconPadding);
            iconRect.offsetMax = new Vector2(-iconPadding, -iconPadding);

            iconImage = iconObject.GetComponent<Image>();
            iconImage.raycastTarget = false;
        }

        if (amountText == null)
        {
            GameObject amountObject = new GameObject(
                "AmountText",
                typeof(RectTransform),
                typeof(Text)
            );

            amountObject.transform.SetParent(transform, false);

            RectTransform amountRect =
                amountObject.GetComponent<RectTransform>();

            amountRect.anchorMin = Vector2.zero;
            amountRect.anchorMax = Vector2.one;
            amountRect.offsetMin = new Vector2(4f, 2f);
            amountRect.offsetMax = new Vector2(-4f, -2f);

            amountText = amountObject.GetComponent<Text>();
            amountText.font =
                Resources.GetBuiltinResource<Font>(
                    "LegacyRuntime.ttf"
                );

            amountText.fontSize = 22;
            amountText.alignment = TextAnchor.LowerRight;
            amountText.color = Color.white;
            amountText.raycastTarget = false;
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[InventoryItemUI] {message}", this);
        }
    }

    private static Sprite GetDefaultSprite()
    {
        if (defaultSprite != null)
        {
            return defaultSprite;
        }

        defaultSprite = Sprite.Create(
            Texture2D.whiteTexture,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f)
        );

        defaultSprite.name = "InventoryItemUI_DefaultSprite";

        return defaultSprite;
    }
}
