using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通常インベントリから装備スロットへアイテムをドラッグしている時に、
/// 装備欄へアイテム表示をスナップし、
/// 装備可能なら緑、装備不可なら赤で装備欄を表示します。
///
/// 実際の装備処理は既存の InventoryItemUI → EquipmentSlotUI.TryEquipDroppedItem
/// に任せます。そのため既存の装備処理を置き換えません。
///
/// シーンへ手動配置しなくても RuntimeInitializeOnLoadMethod で自動生成されます。
/// </summary>
[DefaultExecutionOrder(11000)]
public sealed class EquipmentSlotDragPlacementFeedback : MonoBehaviour
{
    [Header("装備欄への吸着")]
    [Tooltip("オンなら、装備欄の上へドラッグした時にアイテム表示を装備欄の左上へ吸着します。")]
    [SerializeField]
    private bool snapDraggedItemToEquipmentSlot = true;

    [Header("装備可否の色")]
    [Tooltip("装備できる時の装備欄の色です。")]
    [SerializeField]
    private Color validEquipmentColor =
        new Color(0.28f, 0.95f, 0.42f, 1f);

    [Tooltip("装備できない時の装備欄の色です。")]
    [SerializeField]
    private Color invalidEquipmentColor =
        new Color(1f, 0.28f, 0.28f, 1f);

    [Tooltip("元の装備欄の色と判定色を混ぜる割合です。")]
    [SerializeField, Range(0f, 1f)]
    private float tintStrength = 0.55f;

    [Header("枠線")]
    [SerializeField, Range(0f, 12f)]
    private float outlineThickness = 3f;

    private static EquipmentSlotDragPlacementFeedback instance;

    private EquipmentSlotUI highlightedSlot;
    private Image highlightedImage;
    private Color highlightedOriginalColor;

    private Outline highlightedOutline;
    private bool outlineAlreadyExisted;
    private bool outlineWasEnabled;
    private Color outlineOriginalColor;
    private Vector2 outlineOriginalDistance;

    private static readonly BindingFlags InstanceFlags =
        BindingFlags.Instance |
        BindingFlags.Public |
        BindingFlags.NonPublic;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreateAutomatically()
    {
        if (FindAnyObjectByType<
                EquipmentSlotDragPlacementFeedback>() != null)
        {
            return;
        }

        GameObject feedbackObject = new GameObject(
            nameof(EquipmentSlotDragPlacementFeedback)
        );

        feedbackObject.AddComponent<
            EquipmentSlotDragPlacementFeedback>();

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
        ClearSlotFeedback();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }

        ClearSlotFeedback();
    }

    private void LateUpdate()
    {
        if (!Input.GetMouseButton(0))
        {
            ClearSlotFeedback();
            return;
        }

        if (!TryFindDraggedInventoryItem(
                out InventoryItemUI itemUI,
                out InventoryItem item,
                out InventoryGridUI sourceGridUI,
                out RectTransform itemRect,
                out Canvas rootCanvas,
                out bool isRotated))
        {
            ClearSlotFeedback();
            return;
        }

        // ItemBoxやShopから直接装備させない。
        // 既存のInventoryItemUIのルールと同じです。
        if (sourceGridUI == null ||
            !sourceGridUI.IsPlayerInventory)
        {
            ClearSlotFeedback();
            return;
        }

        if (!TryFindEquipmentSlotUnderPointer(
                out EquipmentSlotUI equipmentSlot))
        {
            ClearSlotFeedback();
            return;
        }

        bool canEquip = CanEquipToSlot(
            equipmentSlot,
            sourceGridUI,
            item,
            isRotated
        );

        ApplySlotFeedback(
            equipmentSlot,
            canEquip
        );

        if (snapDraggedItemToEquipmentSlot)
        {
            SnapDraggedItemToEquipmentSlot(
                itemRect,
                rootCanvas,
                equipmentSlot
            );
        }
    }

    /// <summary>
    /// InventoryItemUIがドラッグ中かどうかを取得します。
    /// 既存InventoryItemUIではドラッグ中だけCanvasGroup.blocksRaycasts=false
    /// になるため、それを利用します。
    /// </summary>
    private bool TryFindDraggedInventoryItem(
        out InventoryItemUI itemUI,
        out InventoryItem item,
        out InventoryGridUI sourceGridUI,
        out RectTransform itemRect,
        out Canvas rootCanvas,
        out bool isRotated)
    {
        itemUI = null;
        item = null;
        sourceGridUI = null;
        itemRect = null;
        rootCanvas = null;
        isRotated = false;

        InventoryItemUI[] itemUIs =
            FindObjectsByType<InventoryItemUI>(
                FindObjectsInactive.Exclude
            );

        foreach (InventoryItemUI candidate in itemUIs)
        {
            if (candidate == null ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            CanvasGroup canvasGroup =
                candidate.GetComponent<CanvasGroup>();

            if (canvasGroup == null ||
                canvasGroup.blocksRaycasts)
            {
                continue;
            }

            InventoryItem candidateItem = candidate.Item;

            if (candidateItem == null ||
                candidateItem.ItemData == null)
            {
                continue;
            }

            RectTransform candidateRect =
                candidate.GetComponent<RectTransform>();

            Canvas candidateCanvas =
                candidate.GetComponentInParent<Canvas>()
                    ?.rootCanvas;

            if (candidateRect == null ||
                candidateCanvas == null)
            {
                continue;
            }

            itemUI = candidate;
            item = candidateItem;

            sourceGridUI =
                GetMemberObject(
                    candidate,
                    "gridUI"
                ) as InventoryGridUI;

            itemRect = candidateRect;
            rootCanvas = candidateCanvas;

            isRotated = GetMemberValue(
                candidate,
                "dragIsRotated",
                candidateItem.IsRotated
            );

            return true;
        }

        return false;
    }

    private bool TryFindEquipmentSlotUnderPointer(
        out EquipmentSlotUI equipmentSlot)
    {
        equipmentSlot = null;

        EquipmentSlotUI[] slots =
            FindObjectsByType<EquipmentSlotUI>(
                FindObjectsInactive.Exclude
            );

        foreach (EquipmentSlotUI candidate in slots)
        {
            if (candidate == null ||
                !candidate.isActiveAndEnabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            RectTransform slotRect =
                candidate.GetComponent<RectTransform>();

            if (slotRect == null)
            {
                continue;
            }

            Canvas canvas =
                candidate.GetComponentInParent<Canvas>()
                    ?.rootCanvas;

            Camera camera = GetCanvasCamera(canvas);

            if (!RectTransformUtility
                    .RectangleContainsScreenPoint(
                        slotRect,
                        Input.mousePosition,
                        camera))
            {
                continue;
            }

            equipmentSlot = candidate;
            return true;
        }

        return false;
    }

    /// <summary>
    /// 実際に装備する前のプレビュー判定です。
    /// EquipmentController / EquipmentSlotUIが実際の装備時に確認する条件と
    /// 同じ条件を使います。
    /// </summary>
    private bool CanEquipToSlot(
        EquipmentSlotUI slot,
        InventoryGridUI sourceGridUI,
        InventoryItem item,
        bool desiredRotation)
    {
        if (slot == null ||
            sourceGridUI == null ||
            item == null ||
            item.ItemData == null)
        {
            return false;
        }

        EquipmentController equipmentController =
            slot.EquipmentControllerRef;

        if (equipmentController == null)
        {
            return false;
        }

        InventoryController inventoryController =
            equipmentController.InventoryController;

        if (inventoryController == null ||
            inventoryController.Grid == null ||
            !inventoryController.Grid.ContainsItem(item))
        {
            return false;
        }

        // 装備品は1個単位。
        if (item.Amount != 1)
        {
            return false;
        }

        if (!equipmentController.TryGetEquipmentSlot(
                item.ItemData,
                out EquipmentSlotType itemSlot))
        {
            return false;
        }

        // 武器→PrimaryWeapon、Helmet→Helmetなど、
        // 正しい装備欄かを確認。
        if (itemSlot != slot.SlotType)
        {
            return false;
        }

        // 現行仕様では装備済みの欄へ直接交換はしない。
        if (slot.GetEquippedItem() != null)
        {
            return false;
        }

        bool finalRotation =
            item.CanRotate && desiredRotation;

        Vector2Int itemSize =
            item.ItemData.GetSize(finalRotation);

        int slotWidth = GetMemberValue(
            slot,
            "slotWidth",
            CalculateSlotCellCount(
                slot,
                true
            )
        );

        int slotHeight = GetMemberValue(
            slot,
            "slotHeight",
            CalculateSlotCellCount(
                slot,
                false
            )
        );

        if (itemSize.x > Mathf.Max(1, slotWidth) ||
            itemSize.y > Mathf.Max(1, slotHeight))
        {
            return false;
        }

        return true;
    }

    private int CalculateSlotCellCount(
        EquipmentSlotUI slot,
        bool horizontal)
    {
        if (slot == null)
        {
            return 1;
        }

        RectTransform rect =
            slot.GetComponent<RectTransform>();

        if (rect == null)
        {
            return 1;
        }

        float totalSize =
            horizontal
                ? rect.rect.width
                : rect.rect.height;

        float step =
            slot.CellSize + slot.CellSpacing;

        if (step <= 0f)
        {
            return 1;
        }

        return Mathf.Max(
            1,
            Mathf.RoundToInt(
                (totalSize + slot.CellSpacing) /
                step
            )
        );
    }

    private void SnapDraggedItemToEquipmentSlot(
        RectTransform itemRect,
        Canvas rootCanvas,
        EquipmentSlotUI slot)
    {
        if (itemRect == null ||
            rootCanvas == null ||
            slot == null)
        {
            return;
        }

        RectTransform slotRect =
            slot.GetComponent<RectTransform>();

        RectTransform rootRect =
            rootCanvas.transform as RectTransform;

        if (slotRect == null ||
            rootRect == null)
        {
            return;
        }

        // EquipmentSlotUIは左上基準で描画しているため、
        // 装備後の表示位置と同じ「装備欄の左上」へ吸着させます。
        Vector2 slotTopLeftLocal = new Vector2(
            slotRect.rect.xMin,
            slotRect.rect.yMax
        );

        Vector3 slotTopLeftWorld =
            slotRect.TransformPoint(
                slotTopLeftLocal
            );

        Canvas slotCanvas =
            slot.GetComponentInParent<Canvas>()
                ?.rootCanvas;

        Camera slotCamera =
            GetCanvasCamera(slotCanvas);

        Vector2 screenPosition =
            RectTransformUtility.WorldToScreenPoint(
                slotCamera,
                slotTopLeftWorld
            );

        Camera rootCamera =
            GetCanvasCamera(rootCanvas);

        if (!RectTransformUtility
                .ScreenPointToLocalPointInRectangle(
                    rootRect,
                    screenPosition,
                    rootCamera,
                    out Vector2 rootLocalPosition))
        {
            return;
        }

        itemRect.anchoredPosition =
            rootLocalPosition;
    }

    private void ApplySlotFeedback(
        EquipmentSlotUI slot,
        bool canEquip)
    {
        if (slot == null)
        {
            ClearSlotFeedback();
            return;
        }

        Image slotImage = slot.GetComponent<Image>();

        if (slotImage == null)
        {
            ClearSlotFeedback();
            return;
        }

        if (highlightedSlot != slot ||
            highlightedImage != slotImage)
        {
            ClearSlotFeedback();

            highlightedSlot = slot;
            highlightedImage = slotImage;
            highlightedOriginalColor = slotImage.color;

            highlightedOutline =
                slotImage.GetComponent<Outline>();

            outlineAlreadyExisted =
                highlightedOutline != null;

            if (highlightedOutline == null)
            {
                highlightedOutline =
                    slotImage.gameObject
                        .AddComponent<Outline>();

                outlineWasEnabled = false;
                outlineOriginalColor = Color.clear;
                outlineOriginalDistance =
                    Vector2.zero;
            }
            else
            {
                outlineWasEnabled =
                    highlightedOutline.enabled;

                outlineOriginalColor =
                    highlightedOutline.effectColor;

                outlineOriginalDistance =
                    highlightedOutline.effectDistance;
            }
        }

        Color targetColor =
            canEquip
                ? validEquipmentColor
                : invalidEquipmentColor;

        highlightedImage.color =
            Color.Lerp(
                highlightedOriginalColor,
                targetColor,
                tintStrength
            );

        if (highlightedOutline != null)
        {
            highlightedOutline.enabled =
                outlineThickness > 0f;

            highlightedOutline.effectColor =
                targetColor;

            highlightedOutline.effectDistance =
                new Vector2(
                    outlineThickness,
                    -outlineThickness
                );

            highlightedOutline.useGraphicAlpha =
                true;
        }
    }

    private void ClearSlotFeedback()
    {
        if (highlightedImage != null)
        {
            highlightedImage.color =
                highlightedOriginalColor;
        }

        if (highlightedOutline != null)
        {
            if (outlineAlreadyExisted)
            {
                highlightedOutline.enabled =
                    outlineWasEnabled;

                highlightedOutline.effectColor =
                    outlineOriginalColor;

                highlightedOutline.effectDistance =
                    outlineOriginalDistance;
            }
            else
            {
                if (Application.isPlaying)
                {
                    Destroy(highlightedOutline);
                }
                else
                {
                    DestroyImmediate(
                        highlightedOutline
                    );
                }
            }
        }

        highlightedSlot = null;
        highlightedImage = null;
        highlightedOutline = null;

        outlineAlreadyExisted = false;
        outlineWasEnabled = false;
        outlineOriginalColor = Color.clear;
        outlineOriginalDistance = Vector2.zero;
    }

    private static Camera GetCanvasCamera(
        Canvas canvas)
    {
        if (canvas == null ||
            canvas.renderMode ==
            RenderMode.ScreenSpaceOverlay)
        {
            return null;
        }

        return canvas.worldCamera;
    }

    private static object GetMemberObject(
        object target,
        string memberName)
    {
        if (target == null ||
            string.IsNullOrWhiteSpace(memberName))
        {
            return null;
        }

        System.Type type = target.GetType();

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

            if (property != null &&
                property.CanRead)
            {
                return property.GetValue(target);
            }

            type = type.BaseType;
        }

        return null;
    }

    private static T GetMemberValue<T>(
        object target,
        string memberName,
        T fallback)
    {
        object value =
            GetMemberObject(
                target,
                memberName
            );

        return value is T typedValue
            ? typedValue
            : fallback;
    }

    private void OnValidate()
    {
        tintStrength =
            Mathf.Clamp01(tintStrength);

        outlineThickness =
            Mathf.Clamp(
                outlineThickness,
                0f,
                12f
            );
    }
}
