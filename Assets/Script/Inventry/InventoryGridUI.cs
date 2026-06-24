using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class InventoryGridUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InventoryController inventoryController;

    [Header("右クリックメニュー")]
    [SerializeField] private InventoryContextMenuUI contextMenuUI;

    [Header("マス目の見た目")]
    [SerializeField, Min(8f)] private float cellSize = 64f;
    [SerializeField, Min(0f)] private float cellSpacing = 2f;
    [SerializeField]
    private Color cellColor =
        new Color(0.16f, 0.16f, 0.16f, 1f);

    private RectTransform gridRect;
    private RectTransform cellRoot;
    private RectTransform itemRoot;

    private readonly Dictionary<InventoryItem, InventoryItemUI> itemUIs =
        new Dictionary<InventoryItem, InventoryItemUI>();

    private readonly List<InventoryItem> itemKeysToRemove =
        new List<InventoryItem>();

    private int builtWidth = -1;
    private int builtHeight = -1;
    private bool isSubscribed;

    private static Sprite defaultSprite;

    public RectTransform GridRect => gridRect;
    public RectTransform ItemRoot => itemRoot;

    public float CellSize => cellSize;
    public float CellSpacing => cellSpacing;

    public InventoryController Controller => inventoryController;

    // InventoryItemUI が右クリックメニューを開く時に使う
    public InventoryContextMenuUI ContextMenuUI => contextMenuUI;

    private void Awake()
    {
        gridRect = GetComponent<RectTransform>();
        EnsureRoots();
        FindContextMenuUI();
    }

    private void OnEnable()
    {
        SubscribeToInventory();
        FindContextMenuUI();
    }

    private void Start()
    {
        RebuildGridUI();
    }

    private void OnDisable()
    {
        UnsubscribeFromInventory();

        // インベントリを閉じた時にメニューも閉じる
        if (contextMenuUI != null)
        {
            contextMenuUI.Hide();
        }
    }

    [ContextMenu("Rebuild Grid UI")]
    public void RebuildGridUI()
    {
        if (!TryGetInventoryController())
        {
            return;
        }

        EnsureRoots();

        InventoryGrid grid = inventoryController.Grid;

        ClearChildren(cellRoot);

        Vector2 gridSize = GetGridPixelSize(grid.Width, grid.Height);

        gridRect.pivot = new Vector2(0f, 1f);

        gridRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            gridSize.x
        );

        gridRect.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            gridSize.y
        );

        ConfigureLayer(cellRoot, gridSize);
        ConfigureLayer(itemRoot, gridSize);

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                CreateCell(x, y);
            }
        }

        builtWidth = grid.Width;
        builtHeight = grid.Height;

        RefreshItemsUI();
    }

    public Vector2 GetCellPosition(int x, int y)
    {
        float step = cellSize + cellSpacing;

        return new Vector2(
            x * step,
            -y * step
        );
    }

    public Vector2 GetItemPixelSize(int itemWidth, int itemHeight)
    {
        float width =
            (itemWidth * cellSize) +
            (Mathf.Max(0, itemWidth - 1) * cellSpacing);

        float height =
            (itemHeight * cellSize) +
            (Mathf.Max(0, itemHeight - 1) * cellSpacing);

        return new Vector2(width, height);
    }

    public bool TryGetGridPosition(
        Vector2 screenPosition,
        Camera uiCamera,
        out Vector2Int gridPosition)
    {
        gridPosition = Vector2Int.zero;

        if (!TryGetInventoryController())
        {
            return false;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                gridRect,
                screenPosition,
                uiCamera,
                out Vector2 localPoint))
        {
            return false;
        }

        InventoryGrid grid = inventoryController.Grid;
        Vector2 gridSize = GetGridPixelSize(grid.Width, grid.Height);

        if (localPoint.x < 0f ||
            localPoint.x >= gridSize.x ||
            localPoint.y > 0f ||
            localPoint.y <= -gridSize.y)
        {
            return false;
        }

        float step = cellSize + cellSpacing;

        int x = Mathf.FloorToInt(localPoint.x / step);
        int y = Mathf.FloorToInt(-localPoint.y / step);

        float insideCellX = localPoint.x - (x * step);
        float insideCellY = -localPoint.y - (y * step);

        if (insideCellX >= cellSize || insideCellY >= cellSize)
        {
            return false;
        }

        if (!grid.IsInsideGrid(x, y))
        {
            return false;
        }

        gridPosition = new Vector2Int(x, y);
        return true;
    }

    private void RefreshItemsUI()
    {
        if (!TryGetInventoryController())
        {
            return;
        }

        EnsureRoots();

        InventoryGrid grid = inventoryController.Grid;

        HashSet<InventoryItem> currentItems =
            new HashSet<InventoryItem>();

        foreach (InventoryItem item in grid.Items)
        {
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            currentItems.Add(item);

            if (!itemUIs.TryGetValue(item, out InventoryItemUI itemUI) ||
                itemUI == null)
            {
                itemUI = CreateItemUI();
                itemUIs[item] = itemUI;
            }

            itemUI.Setup(item, this);
            itemUI.transform.SetAsLastSibling();
        }

        itemKeysToRemove.Clear();

        foreach (KeyValuePair<InventoryItem, InventoryItemUI> pair in itemUIs)
        {
            bool isMissing =
                !currentItems.Contains(pair.Key) ||
                pair.Value == null;

            if (!isMissing)
            {
                continue;
            }

            if (pair.Value != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(pair.Value.gameObject);
                }
                else
                {
                    DestroyImmediate(pair.Value.gameObject);
                }
            }

            itemKeysToRemove.Add(pair.Key);
        }

        foreach (InventoryItem item in itemKeysToRemove)
        {
            itemUIs.Remove(item);
        }
    }

    private InventoryItemUI CreateItemUI()
    {
        GameObject itemObject = new GameObject(
            "ItemUI",
            typeof(RectTransform),
            typeof(Image),
            typeof(InventoryItemUI)
        );

        itemObject.transform.SetParent(itemRoot, false);

        return itemObject.GetComponent<InventoryItemUI>();
    }

    private void SubscribeToInventory()
    {
        if (isSubscribed || !TryGetInventoryController())
        {
            return;
        }

        inventoryController.OnInventoryChanged += HandleInventoryChanged;
        isSubscribed = true;
    }

    private void UnsubscribeFromInventory()
    {
        if (!isSubscribed || inventoryController == null)
        {
            return;
        }

        inventoryController.OnInventoryChanged -= HandleInventoryChanged;
        isSubscribed = false;
    }

    private void HandleInventoryChanged()
    {
        if (inventoryController == null)
        {
            return;
        }

        InventoryGrid grid = inventoryController.Grid;

        if (grid.Width != builtWidth || grid.Height != builtHeight)
        {
            RebuildGridUI();
            return;
        }

        RefreshItemsUI();
    }

    private bool TryGetInventoryController()
    {
        if (inventoryController == null)
        {
            inventoryController = GetComponent<InventoryController>();
        }

        if (inventoryController != null)
        {
            return true;
        }

        Debug.LogError(
            "InventoryGridUI: InventoryController をInspectorで設定してください。"
        );

        return false;
    }

    private void FindContextMenuUI()
    {
        if (contextMenuUI != null)
        {
            return;
        }

        Transform parent = transform.parent;

        if (parent != null)
        {
            contextMenuUI =
                parent.GetComponentInChildren<InventoryContextMenuUI>(true);
        }
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

    private void ConfigureLayer(RectTransform layer, Vector2 size)
    {
        layer.anchorMin = new Vector2(0f, 1f);
        layer.anchorMax = new Vector2(0f, 1f);
        layer.pivot = new Vector2(0f, 1f);
        layer.anchoredPosition = Vector2.zero;
        layer.sizeDelta = size;
        layer.localScale = Vector3.one;
    }

    private void CreateCell(int x, int y)
    {
        GameObject cellObject = new GameObject(
            $"Cell_{x}_{y}",
            typeof(RectTransform),
            typeof(Image)
        );

        cellObject.transform.SetParent(cellRoot, false);

        RectTransform cellRect = cellObject.GetComponent<RectTransform>();

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

    private Vector2 GetGridPixelSize(int width, int height)
    {
        float totalWidth =
            (width * cellSize) +
            (Mathf.Max(0, width - 1) * cellSpacing);

        float totalHeight =
            (height * cellSize) +
            (Mathf.Max(0, height - 1) * cellSpacing);

        return new Vector2(totalWidth, totalHeight);
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

        defaultSprite.name = "InventoryGridUI_DefaultSprite";

        return defaultSprite;
    }
}