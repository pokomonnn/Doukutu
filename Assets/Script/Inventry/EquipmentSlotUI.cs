using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Image))]
public class EquipmentSlotUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private EquipmentController equipmentController;

    [Header("この装備枠")]
    [SerializeField]
    private EquipmentSlotType slotType =
        EquipmentSlotType.PrimaryWeapon;

    [SerializeField, Min(1)] private int slotWidth = 5;
    [SerializeField, Min(1)] private int slotHeight = 2;

    [Header("見た目")]
    [SerializeField, Min(8f)] private float cellSize = 64f;
    [SerializeField, Min(0f)] private float cellSpacing = 2f;

    [SerializeField]
    private Color slotBackgroundColor =
        new Color(0.08f, 0.08f, 0.08f, 0.95f);

    [SerializeField]
    private Color cellColor =
        new Color(0.18f, 0.18f, 0.18f, 1f);

    [SerializeField]
    private Color itemBackgroundColor =
        new Color(0.18f, 0.30f, 0.38f, 0.92f);

    [SerializeField, Min(0f)] private float iconPadding = 5f;

    private RectTransform slotRect;
    private Image slotBackgroundImage;

    private RectTransform cellRoot;
    private RectTransform itemRoot;

    private Image itemBackgroundImage;
    private Image itemIconImage;

    private bool isSubscribed;

    private static Sprite defaultSprite;

    public EquipmentSlotType SlotType => slotType;

    public float CellSize => cellSize;
    public float CellSpacing => cellSpacing;

    public InventoryController InventoryController
    {
        get
        {
            if (!FindEquipmentController())
            {
                return null;
            }

            return equipmentController.InventoryController;
        }
    }

    public InventoryItem GetEquippedItem()
    {
        if (!FindEquipmentController())
        {
            return null;
        }

        return equipmentController.GetEquippedItem(slotType);
    }

    public bool TryUnequipToInventoryPosition(
        int targetX,
        int targetY,
        bool isRotated,
        out EquipmentResult result)
    {
        result = EquipmentResult.InvalidSlot;

        if (!FindEquipmentController())
        {
            result = EquipmentResult.InventoryNotFound;
            return false;
        }

        return equipmentController.TryUnequipToPosition(
            slotType,
            targetX,
            targetY,
            isRotated,
            out result
        );
    }

    public void RefreshSlotVisual()
    {
        RefreshVisual();
    }

    public void SetDragVisualRotation(bool isRotated)
    {
        if (itemBackgroundImage == null ||
            itemIconImage == null)
        {
            return;
        }

        InventoryItem equippedItem = GetEquippedItem();

        if (equippedItem == null ||
            equippedItem.ItemData == null)
        {
            return;
        }

        bool finalRotation =
            equippedItem.CanRotate && isRotated;

        Vector2Int itemSize =
            equippedItem.ItemData.GetSize(finalRotation);

        RectTransform itemRect =
            itemBackgroundImage.rectTransform;

        itemRect.sizeDelta = GetItemPixelSize(
            itemSize.x,
            itemSize.y
        );

        itemIconImage.rectTransform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                finalRotation ? -90f : 0f
            );
    }


    private void Awake()
    {
        RebuildSlotUI();
    }

    private void OnEnable()
    {
        SubscribeToEquipment();
        RefreshVisual();
    }

    private void OnDisable()
    {
        UnsubscribeFromEquipment();
    }

    [ContextMenu("Rebuild Equipment Slot UI")]
    public void RebuildSlotUI()
    {
        EnsureReferences();
        EnsureRoots();

        Vector2 slotSize = GetGridPixelSize(
            slotWidth,
            slotHeight
        );

        slotRect.pivot = new Vector2(0f, 1f);

        slotRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            slotSize.x
        );

        slotRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            slotSize.y
        );

        ConfigureRoot(cellRoot, slotSize);
        ConfigureRoot(itemRoot, slotSize);

        ClearChildren(cellRoot);

        for (int y = 0; y < slotHeight; y++)
        {
            for (int x = 0; x < slotWidth; x++)
            {
                CreateCell(x, y);
            }
        }

        EnsureItemVisual();
        RefreshVisual();
    }

    public bool TryEquipDroppedItem(
        InventoryItem item,
        bool desiredRotation,
        out EquipmentResult result)
    {
        result = EquipmentResult.InvalidItem;

        if (item == null || item.ItemData == null)
        {
            return false;
        }

        if (!FindEquipmentController())
        {
            result = EquipmentResult.InventoryNotFound;
            return false;
        }

        if (!equipmentController.TryGetEquipmentSlot(
                item.ItemData,
                out EquipmentSlotType itemSlot))
        {
            result = EquipmentResult.UnsupportedItem;
            return false;
        }

        // 銃をヘルメット枠へ、などを防ぐ
        if (itemSlot != slotType)
        {
            result = EquipmentResult.InvalidSlot;
            return false;
        }

        bool finalRotation =
            item.CanRotate && desiredRotation;

        Vector2Int itemSize =
            item.ItemData.GetSize(finalRotation);

        // 装備枠のサイズを超える物は置けない
        if (itemSize.x > slotWidth ||
            itemSize.y > slotHeight)
        {
            result = EquipmentResult.InvalidSlot;
            return false;
        }

        bool equipped = equipmentController.TryEquipItem(
            item,
            out result
        );

        if (!equipped)
        {
            return false;
        }

        // ドラッグ中にRで回転していた場合、
        // 装備成功後にその向きを反映する
        if (item.CanRotate &&
            item.IsRotated != finalRotation)
        {
            item.TryRotate();
        }

        RefreshVisual();

        return true;
    }

    private void RefreshVisual()
    {
        if (itemBackgroundImage == null ||
            itemIconImage == null)
        {
            return;
        }

        if (!FindEquipmentController())
        {
            itemBackgroundImage.gameObject.SetActive(false);
            return;
        }

        InventoryItem equippedItem =
            equipmentController.GetEquippedItem(slotType);

        bool hasItem =
            equippedItem != null &&
            equippedItem.ItemData != null;

        itemBackgroundImage.gameObject.SetActive(hasItem);

        if (!hasItem)
        {
            return;
        }

        Vector2Int itemSize = equippedItem.Size;

        RectTransform itemRect =
            itemBackgroundImage.rectTransform;

        itemRect.anchorMin = new Vector2(0f, 1f);
        itemRect.anchorMax = new Vector2(0f, 1f);
        itemRect.pivot = new Vector2(0f, 1f);
        itemRect.anchoredPosition = Vector2.zero;
        itemRect.sizeDelta = GetItemPixelSize(
            itemSize.x,
            itemSize.y
        );

        itemBackgroundImage.color = itemBackgroundColor;

        Sprite icon = equippedItem.ItemData.Icon;

        itemIconImage.sprite =
            icon != null ? icon : GetDefaultSprite();

        itemIconImage.color =
            icon != null
                ? Color.white
                : new Color(0.65f, 0.65f, 0.65f, 1f);

        itemIconImage.preserveAspect = true;

        itemIconImage.rectTransform.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                equippedItem.IsRotated ? -90f : 0f
            );
    }

    private void SubscribeToEquipment()
    {
        if (isSubscribed || !FindEquipmentController())
        {
            return;
        }

        equipmentController.OnEquipmentChanged += RefreshVisual;
        isSubscribed = true;
    }

    private void UnsubscribeFromEquipment()
    {
        if (!isSubscribed || equipmentController == null)
        {
            return;
        }

        equipmentController.OnEquipmentChanged -= RefreshVisual;
        isSubscribed = false;
    }

    private bool FindEquipmentController()
    {
        if (equipmentController != null)
        {
            return true;
        }

        equipmentController =
            GetComponentInParent<EquipmentController>();

        if (equipmentController != null)
        {
            return true;
        }

        equipmentController =
            FindAnyObjectByType<EquipmentController>(
                FindObjectsInactive.Include
            );

        return equipmentController != null;
    }

    private void EnsureReferences()
    {
        if (slotRect == null)
        {
            slotRect = GetComponent<RectTransform>();
        }

        if (slotBackgroundImage == null)
        {
            slotBackgroundImage = GetComponent<Image>();
        }

        slotBackgroundImage.sprite = GetDefaultSprite();
        slotBackgroundImage.color = slotBackgroundColor;
        slotBackgroundImage.raycastTarget = true;
    }

    private void EnsureRoots()
    {
        if (cellRoot == null)
        {
            cellRoot = CreateRoot("CellRoot");
        }

        if (itemRoot == null)
        {
            itemRoot = CreateRoot("ItemRoot");
        }

        itemRoot.SetAsLastSibling();
    }

    private RectTransform CreateRoot(string rootName)
    {
        GameObject rootObject = new GameObject(
            rootName,
            typeof(RectTransform)
        );

        rootObject.transform.SetParent(transform, false);

        return rootObject.GetComponent<RectTransform>();
    }

    private void ConfigureRoot(
        RectTransform root,
        Vector2 size)
    {
        root.anchorMin = new Vector2(0f, 1f);
        root.anchorMax = new Vector2(0f, 1f);
        root.pivot = new Vector2(0f, 1f);
        root.anchoredPosition = Vector2.zero;
        root.sizeDelta = size;
        root.localScale = Vector3.one;
    }

    private void CreateCell(int x, int y)
    {
        GameObject cellObject = new GameObject(
            $"Cell_{x}_{y}",
            typeof(RectTransform),
            typeof(Image)
        );

        cellObject.transform.SetParent(cellRoot, false);

        RectTransform cellRect =
            cellObject.GetComponent<RectTransform>();

        cellRect.anchorMin = new Vector2(0f, 1f);
        cellRect.anchorMax = new Vector2(0f, 1f);
        cellRect.pivot = new Vector2(0f, 1f);
        cellRect.sizeDelta = new Vector2(cellSize, cellSize);
        cellRect.anchoredPosition = GetCellPosition(x, y);

        Image cellImage = cellObject.GetComponent<Image>();
        cellImage.sprite = GetDefaultSprite();
        cellImage.color = cellColor;
        cellImage.raycastTarget = false;
    }

    private void EnsureItemVisual()
    {
        if (itemBackgroundImage != null)
        {
            return;
        }

        GameObject itemObject = new GameObject(
               "EquippedItemVisual",
               typeof(RectTransform),
                 typeof(Image),
                typeof(CanvasGroup),
                 typeof(EquipmentItemDragHandler)
);

        itemObject.transform.SetParent(itemRoot, false);

        itemBackgroundImage =
            itemObject.GetComponent<Image>();

        itemBackgroundImage.sprite = GetDefaultSprite();
        itemBackgroundImage.raycastTarget = true;

        GameObject iconObject = new GameObject(
            "Icon",
            typeof(RectTransform),
            typeof(Image)
        );

        iconObject.transform.SetParent(itemObject.transform, false);

        RectTransform iconRect =
            iconObject.GetComponent<RectTransform>();

        iconRect.anchorMin = Vector2.zero;
        iconRect.anchorMax = Vector2.one;
        iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.offsetMin = new Vector2(
            iconPadding,
            iconPadding
        );
        iconRect.offsetMax = new Vector2(
            -iconPadding,
            -iconPadding
        );

        itemIconImage = iconObject.GetComponent<Image>();
        itemIconImage.raycastTarget = false;
    }

    private Vector2 GetCellPosition(int x, int y)
    {
        float step = cellSize + cellSpacing;

        return new Vector2(
            x * step,
            -y * step
        );
    }

    private Vector2 GetItemPixelSize(
        int width,
        int height)
    {
        float pixelWidth =
            (width * cellSize) +
            (Mathf.Max(0, width - 1) * cellSpacing);

        float pixelHeight =
            (height * cellSize) +
            (Mathf.Max(0, height - 1) * cellSpacing);

        return new Vector2(pixelWidth, pixelHeight);
    }

    private Vector2 GetGridPixelSize(
        int width,
        int height)
    {
        return GetItemPixelSize(width, height);
    }

    private void ClearChildren(Transform target)
    {
        for (int i = target.childCount - 1; i >= 0; i--)
        {
            GameObject child = target.GetChild(i).gameObject;

            if (Application.isPlaying)
            {
                Destroy(child);
            }
            else
            {
                DestroyImmediate(child);
            }
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

        defaultSprite.name =
            "EquipmentSlotUI_DefaultSprite";

        return defaultSprite;
    }

    private void OnValidate()
    {
        slotWidth = Mathf.Max(1, slotWidth);
        slotHeight = Mathf.Max(1, slotHeight);

        cellSize = Mathf.Max(8f, cellSize);
        cellSpacing = Mathf.Max(0f, cellSpacing);
        iconPadding = Mathf.Max(0f, iconPadding);
    }
}