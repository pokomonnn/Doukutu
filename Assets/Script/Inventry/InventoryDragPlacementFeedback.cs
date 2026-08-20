using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// インベントリ内のアイテムをドラッグしている間、
/// マウスカーソルを隠し、実際に置かれるマス位置へアイテム表示をスナップします。
/// 緑 = 置ける、赤 = 置けない、という見た目のフィードバックも表示します。
///
/// 同一プレイヤーInventory内でスタック結合できる場所も「置ける」と判定し、
/// 緑で表示します。
///
/// 既存の InventoryItemUI / EquipmentItemDragHandler は置き換え不要です。
/// RuntimeInitializeOnLoadMethod により、シーン開始時に自動で1つ生成されます。
/// </summary>
[DefaultExecutionOrder(10000)]
public sealed class InventoryDragPlacementFeedback : MonoBehaviour
{
    [Header("カーソル")]
    [Tooltip("アイテムをドラッグしている間だけマウスカーソルを隠します")]
    [SerializeField] private bool hideCursorWhileDragging = true;

    [Header("アイテム表示")]
    [Tooltip("ドラッグ中のアイテムを、実際に置かれるグリッド位置へスナップします")]
    [SerializeField] private bool snapDraggedItemToGrid = true;

    [Tooltip("置ける場所・スタック結合できる場所に重ねた時の色")]
    [SerializeField]
    private Color validPlacementColor =
        new Color(0.28f, 0.95f, 0.42f, 1f);

    [Tooltip("置けない場所に重ねた時の色")]
    [SerializeField]
    private Color invalidPlacementColor =
        new Color(1f, 0.28f, 0.28f, 1f);

    [Tooltip("元の色と判定色を混ぜる割合")]
    [SerializeField, Range(0f, 1f)] private float tintStrength = 0.55f;

    [Tooltip("判定色の枠線の太さ")]
    [SerializeField, Range(0f, 12f)] private float outlineThickness = 3f;

    private static InventoryDragPlacementFeedback instance;

    private DragVisual activeDrag;

    private Image tintedImage;
    private Color tintedImageOriginalColor;

    private Outline activeOutline;
    private bool activeOutlineWasAlreadyPresent;
    private bool activeOutlineWasEnabled;
    private Color activeOutlineOriginalColor;
    private Vector2 activeOutlineOriginalDistance;

    private bool cursorWasHiddenByThis;
    private bool cursorVisibleBeforeDrag;

    private static readonly BindingFlags InstanceFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAutomatically()
    {
        if (FindAnyObjectByType<InventoryDragPlacementFeedback>() != null)
        {
            return;
        }

        GameObject feedbackObject = new GameObject(
            nameof(InventoryDragPlacementFeedback)
        );

        feedbackObject.AddComponent<InventoryDragPlacementFeedback>();
        DontDestroyOnLoad(feedbackObject);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDisable()
    {
        ClearFeedback();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        ClearFeedback();
    }

    private void LateUpdate()
    {
        // 左クリックを離した後は、必ずカーソルと色を元へ戻す。
        if (!Input.GetMouseButton(0))
        {
            ClearFeedback();
            return;
        }

        if (!TryFindActiveDrag(out DragVisual drag))
        {
            ClearFeedback();
            return;
        }

        activeDrag = drag;
        HideCursor();
        RefreshDragFeedback(drag);
    }

    private bool TryFindActiveDrag(out DragVisual drag)
    {
        drag = default;

        // 通常のインベントリアイテムを優先する。
        InventoryItemUI[] itemUIs =
            FindObjectsByType<InventoryItemUI>(
                FindObjectsInactive.Exclude
            );

        foreach (InventoryItemUI itemUI in itemUIs)
        {
            if (itemUI == null)
            {
                continue;
            }

            CanvasGroup canvasGroup =
                itemUI.GetComponent<CanvasGroup>();

            if (canvasGroup == null || canvasGroup.blocksRaycasts)
            {
                continue;
            }

            InventoryItem item = itemUI.Item;

            if (item == null || item.ItemData == null)
            {
                continue;
            }

            RectTransform itemRect =
                itemUI.GetComponent<RectTransform>();

            Canvas rootCanvas =
                itemUI.GetComponentInParent<Canvas>()?.rootCanvas;

            if (itemRect == null || rootCanvas == null)
            {
                continue;
            }

            drag = new DragVisual(
                itemUI,
                itemRect,
                rootCanvas,
                item,
                GetPrivateObject(itemUI, "gridUI") as InventoryGridUI,
                GetPrivateValue(itemUI, "dragCellOffset", Vector2Int.zero),
                GetPrivateValue(itemUI, "dragIsRotated", item.IsRotated)
            );

            return true;
        }

        // 装備スロットから通常インベントリへドラッグする場合にも対応する。
        EquipmentItemDragHandler[] equipmentDragHandlers =
            FindObjectsByType<EquipmentItemDragHandler>(
                FindObjectsInactive.Exclude
            );

        foreach (EquipmentItemDragHandler handler in equipmentDragHandlers)
        {
            if (handler == null)
            {
                continue;
            }

            CanvasGroup canvasGroup =
                handler.GetComponent<CanvasGroup>();

            if (canvasGroup == null || canvasGroup.blocksRaycasts)
            {
                continue;
            }

            object equipmentSlotUI = GetPrivateObject(
                handler,
                "equipmentSlotUI"
            );

            InventoryItem item = InvokeMethod<InventoryItem>(
                equipmentSlotUI,
                "GetEquippedItem"
            );

            if (item == null || item.ItemData == null)
            {
                continue;
            }

            RectTransform itemRect =
                handler.GetComponent<RectTransform>();

            Canvas rootCanvas =
                handler.GetComponentInParent<Canvas>()?.rootCanvas;

            if (itemRect == null || rootCanvas == null)
            {
                continue;
            }

            drag = new DragVisual(
                handler,
                itemRect,
                rootCanvas,
                item,
                null,
                GetPrivateValue(handler, "dragCellOffset", Vector2Int.zero),
                GetPrivateValue(handler, "dragIsRotated", item.IsRotated)
            );

            return true;
        }

        return false;
    }

    private void RefreshDragFeedback(DragVisual drag)
    {
        if (!TryGetTargetPlacement(
                drag,
                out InventoryGridUI targetGridUI,
                out Vector2Int targetPosition))
        {
            // グリッドの外では通常色へ戻す。
            RestoreItemVisualOnly();
            return;
        }

        if (snapDraggedItemToGrid)
        {
            SnapDraggedItemToGrid(
                drag,
                targetGridUI,
                targetPosition
            );
        }

        // 実際のドロップ処理でスタック判定に使っているのは
        // 「アイテム左上」ではなく、マウスが指しているセル。
        // targetPositionは pointer - dragCellOffset なので戻して取得する。
        Vector2Int pointerGridPosition =
            targetPosition + drag.DragCellOffset;

        bool canPlace = CanPlaceItemAt(
            targetGridUI,
            drag.Item,
            drag.SourceGridUI,
            targetPosition,
            pointerGridPosition,
            drag.IsRotated
        );

        ApplyPlacementVisual(
            drag.ItemRect,
            canPlace
        );
    }

    /// <summary>
    /// 既存スクリプトがドロップ時に使う
    /// 「ポインタのマス - 掴んだマス」の計算と同じ座標を作ります。
    /// その座標へアイテム表示をスナップするため、見えている位置と
    /// 実際のドロップ位置が一致します。
    /// </summary>
    private bool TryGetTargetPlacement(
        DragVisual drag,
        out InventoryGridUI targetGridUI,
        out Vector2Int targetPosition)
    {
        targetGridUI = null;
        targetPosition = Vector2Int.zero;

        InventoryGridUI[] gridUIs =
            FindObjectsByType<InventoryGridUI>(
                FindObjectsInactive.Exclude
            );

        foreach (InventoryGridUI candidate in gridUIs)
        {
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            Canvas gridCanvas =
                candidate.GetComponentInParent<Canvas>()?.rootCanvas;

            Camera gridCamera = GetCanvasCamera(gridCanvas);

            if (!candidate.TryGetGridPosition(
                    Input.mousePosition,
                    gridCamera,
                    out Vector2Int pointerGridPosition))
            {
                continue;
            }

            targetGridUI = candidate;
            targetPosition = pointerGridPosition -
                drag.DragCellOffset;

            return true;
        }

        return false;
    }

    private void SnapDraggedItemToGrid(
        DragVisual drag,
        InventoryGridUI targetGridUI,
        Vector2Int targetPosition)
    {
        if (drag.ItemRect == null ||
            drag.RootCanvas == null ||
            targetGridUI == null ||
            targetGridUI.ItemRoot == null)
        {
            return;
        }

        RectTransform targetItemRoot =
            targetGridUI.ItemRoot as RectTransform;

        if (targetItemRoot == null)
        {
            return;
        }

        Vector2 cellLocalPosition = targetGridUI.GetCellPosition(
            targetPosition.x,
            targetPosition.y
        );

        Vector3 worldPosition = targetItemRoot.TransformPoint(
            cellLocalPosition
        );

        Camera targetCanvasCamera = GetCanvasCamera(
            targetGridUI.GetComponentInParent<Canvas>()?.rootCanvas
        );

        Vector2 screenPosition =
            RectTransformUtility.WorldToScreenPoint(
                targetCanvasCamera,
                worldPosition
            );

        RectTransform rootRect =
            drag.RootCanvas.transform as RectTransform;

        if (rootRect == null)
        {
            return;
        }

        Camera rootCanvasCamera = GetCanvasCamera(
            drag.RootCanvas
        );

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                rootRect,
                screenPosition,
                rootCanvasCamera,
                out Vector2 rootLocalPosition))
        {
            return;
        }

        drag.ItemRect.anchoredPosition = rootLocalPosition;
    }

    private bool CanPlaceItemAt(
        InventoryGridUI targetGridUI,
        InventoryItem item,
        InventoryGridUI sourceGridUI,
        Vector2Int targetPosition,
        Vector2Int pointerGridPosition,
        bool isRotated)
    {
        if (targetGridUI == null ||
            item == null ||
            item.ItemData == null)
        {
            return false;
        }

        // ショップなど、既存の移動ルールで禁止されている場所は赤く表示する。
        if (sourceGridUI != null &&
            sourceGridUI != targetGridUI &&
            (!sourceGridUI.AllowsDirectTransfer ||
             !targetGridUI.AllowsDirectTransfer))
        {
            return false;
        }

        if (sourceGridUI == null &&
            !targetGridUI.AllowsDirectTransfer)
        {
            return false;
        }

        InventoryGrid targetGrid = targetGridUI.Grid;

        if (targetGrid == null)
        {
            // 対象グリッドの実データが取得できない場合は、
            // 誤って「置ける」と表示しないよう赤扱いにします。
            return false;
        }

        // 同じプレイヤーInventory内で、マウス先のスタックへ
        // 実際に結合できる場合は「置ける」と同じ緑表示にする。
        // MaxStack到達済み・別ItemData・別弾種などはfalseのまま。
        if (sourceGridUI != null &&
            sourceGridUI == targetGridUI &&
            targetGridUI.CanMergeItemAt(
                item,
                pointerGridPosition.x,
                pointerGridPosition.y))
        {
            return true;
        }

        return targetGrid.CanPlaceItem(
            item,
            targetPosition.x,
            targetPosition.y,
            isRotated
        );
    }

    private void ApplyPlacementVisual(
        RectTransform itemRect,
        bool canPlace)
    {
        if (itemRect == null)
        {
            return;
        }

        Image image = itemRect.GetComponent<Image>();

        if (image == null)
        {
            return;
        }

        if (tintedImage != image)
        {
            RestoreItemVisualOnly();

            tintedImage = image;
            tintedImageOriginalColor = image.color;

            activeOutline = image.GetComponent<Outline>();
            activeOutlineWasAlreadyPresent = activeOutline != null;

            if (activeOutline == null)
            {
                activeOutline = image.gameObject.AddComponent<Outline>();
                activeOutlineWasEnabled = false;
                activeOutlineOriginalColor = Color.clear;
                activeOutlineOriginalDistance = Vector2.zero;
            }
            else
            {
                activeOutlineWasEnabled = activeOutline.enabled;
                activeOutlineOriginalColor = activeOutline.effectColor;
                activeOutlineOriginalDistance = activeOutline.effectDistance;
            }
        }

        Color targetColor = canPlace
            ? validPlacementColor
            : invalidPlacementColor;

        tintedImage.color = Color.Lerp(
            tintedImageOriginalColor,
            targetColor,
            tintStrength
        );

        if (activeOutline != null)
        {
            activeOutline.enabled = outlineThickness > 0f;
            activeOutline.effectColor = targetColor;
            activeOutline.effectDistance = new Vector2(
                outlineThickness,
                -outlineThickness
            );
        }
    }

    private void RestoreItemVisualOnly()
    {
        if (tintedImage != null)
        {
            tintedImage.color = tintedImageOriginalColor;
        }

        if (activeOutline != null)
        {
            if (activeOutlineWasAlreadyPresent)
            {
                activeOutline.enabled = activeOutlineWasEnabled;
                activeOutline.effectColor = activeOutlineOriginalColor;
                activeOutline.effectDistance =
                    activeOutlineOriginalDistance;
            }
            else
            {
                Destroy(activeOutline);
            }
        }

        tintedImage = null;
        activeOutline = null;
        activeOutlineWasAlreadyPresent = false;
        activeOutlineWasEnabled = false;
        activeOutlineOriginalColor = Color.clear;
        activeOutlineOriginalDistance = Vector2.zero;
    }

    private void HideCursor()
    {
        if (!hideCursorWhileDragging || cursorWasHiddenByThis)
        {
            return;
        }

        cursorVisibleBeforeDrag = Cursor.visible;
        Cursor.visible = false;
        cursorWasHiddenByThis = true;
    }

    private void RestoreCursor()
    {
        if (!cursorWasHiddenByThis)
        {
            return;
        }

        Cursor.visible = cursorVisibleBeforeDrag;
        cursorWasHiddenByThis = false;
    }

    private void ClearFeedback()
    {
        RestoreItemVisualOnly();
        RestoreCursor();
        activeDrag = default;
    }

    private static Camera GetCanvasCamera(Canvas canvas)
    {
        if (canvas == null ||
            canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private static object GetPrivateObject(
        object target,
        string memberName)
    {
        if (target == null || string.IsNullOrWhiteSpace(memberName))
        {
            return null;
        }

        Type type = target.GetType();

        while (type != null)
        {
            FieldInfo field = type.GetField(
                memberName,
                InstanceFlags
            );

            if (field != null)
            {
                return field.GetValue(target);
            }

            PropertyInfo property = type.GetProperty(
                memberName,
                InstanceFlags
            );

            if (property != null && property.CanRead)
            {
                return property.GetValue(target);
            }

            type = type.BaseType;
        }

        return null;
    }

    private static T GetPrivateValue<T>(
        object target,
        string memberName,
        T fallback)
    {
        object value = GetPrivateObject(target, memberName);

        return value is T typedValue
            ? typedValue
            : fallback;
    }

    private static T InvokeMethod<T>(
        object target,
        string methodName)
        where T : class
    {
        if (target == null || string.IsNullOrWhiteSpace(methodName))
        {
            return null;
        }

        MethodInfo method = target.GetType().GetMethod(
            methodName,
            InstanceFlags,
            null,
            Type.EmptyTypes,
            null
        );

        if (method == null)
        {
            return null;
        }

        return method.Invoke(target, null) as T;
    }

    private readonly struct DragVisual
    {
        public readonly Component Source;
        public readonly RectTransform ItemRect;
        public readonly Canvas RootCanvas;
        public readonly InventoryItem Item;
        public readonly InventoryGridUI SourceGridUI;
        public readonly Vector2Int DragCellOffset;
        public readonly bool IsRotated;

        public DragVisual(
            Component source,
            RectTransform itemRect,
            Canvas rootCanvas,
            InventoryItem item,
            InventoryGridUI sourceGridUI,
            Vector2Int dragCellOffset,
            bool isRotated)
        {
            Source = source;
            ItemRect = itemRect;
            RootCanvas = rootCanvas;
            Item = item;
            SourceGridUI = sourceGridUI;
            DragCellOffset = dragCellOffset;
            IsRotated = isRotated;
        }
    }

    private void OnValidate()
    {
        tintStrength = Mathf.Clamp01(tintStrength);
        outlineThickness = Mathf.Clamp(outlineThickness, 0f, 12f);
    }
}
