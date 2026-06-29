using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class InventoryGridUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("プレイヤー用のGrid UIでは設定します")]
    [SerializeField] private InventoryController inventoryController;

    [Tooltip("アイテムボックス用のGrid UIでは、ItemBoxUIControllerが実行中に設定します")]
    [SerializeField] private ItemBoxInventory itemBoxInventory;

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

    // 既存コード互換用。箱側Grid UIでは null です。
    public InventoryController Controller =>
        itemBoxInventory == null
            ? inventoryController
            : null;

    public ItemBoxInventory ItemBox => itemBoxInventory;

    public bool IsPlayerInventory =>
        itemBoxInventory == null &&
        inventoryController != null;

    public bool IsItemBoxInventory => itemBoxInventory != null;

    // Shop は売買機能を作るまで無料ドラッグ移動を禁止します。
    public bool AllowsDirectTransfer =>
        itemBoxInventory == null ||
        itemBoxInventory.AllowsDirectItemTransfer;

    public InventoryGrid Grid
    {
        get
        {
            TryGetInventoryGrid(out InventoryGrid grid);
            return grid;
        }
    }

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

        // Tabでインベントリを開いた瞬間に、
        // 現在の所持数・残弾表示などを最新状態へ更新する
        RefreshInventoryUI();
    }

    private void Start()
    {
        RefreshInventoryUI();
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

    /// <summary>
    /// 箱側のGrid UIに、開いたItemBoxInventoryを接続します。
    /// プレイヤー用Grid UIでは呼ばないでください。
    /// </summary>
    public void BindItemBoxInventory(ItemBoxInventory newItemBoxInventory)
    {
        if (itemBoxInventory == newItemBoxInventory)
        {
            RefreshInventoryUI();
            return;
        }

        UnsubscribeFromInventory();

        itemBoxInventory = newItemBoxInventory;
        builtWidth = -1;
        builtHeight = -1;

        SubscribeToInventory();
        RefreshInventoryUI();
    }

    [ContextMenu("Rebuild Grid UI")]
    public void RebuildGridUI()
    {
        if (!TryGetInventoryGrid(out InventoryGrid grid))
        {
            return;
        }

        EnsureRoots();

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

    public void RefreshInventoryUI()
    {
        if (!TryGetInventoryGrid(out InventoryGrid grid))
        {
            return;
        }

        // 初回表示時、またはグリッドサイズが変わっている時は
        // マス目ごと作り直す
        if (grid.Width != builtWidth ||
            grid.Height != builtHeight)
        {
            RebuildGridUI();
            return;
        }

        // アイテム数、弾数などの表示だけ最新化する
        RefreshItemsUI();
    }

    public bool ContainsItem(InventoryItem item)
    {
        return TryGetInventoryGrid(out InventoryGrid grid) &&
               grid.ContainsItem(item);
    }

    public bool TryMoveItem(
        InventoryItem item,
        int targetX,
        int targetY,
        bool isRotated)
    {
        if (item == null)
        {
            return false;
        }

        if (itemBoxInventory != null)
        {
            return itemBoxInventory.TryMoveItem(
                item,
                targetX,
                targetY,
                isRotated
            );
        }

        return inventoryController != null &&
               inventoryController.TryMoveItem(
                   item,
                   targetX,
                   targetY,
                   isRotated
               );
    }

    /// <summary>
    /// 別のGrid UIへ、同じInventoryItemを移動します。
    /// 武器のStoredMagazineAmmoなど個別情報も維持されます。
    /// </summary>
    public bool TryTransferItemTo(
        InventoryItem item,
        InventoryGridUI targetGridUI,
        int targetX,
        int targetY,
        bool isRotated)
    {
        if (item == null ||
            targetGridUI == null)
        {
            return false;
        }

        // 同じGridなら通常の並べ替えとして扱う
        if (targetGridUI == this)
        {
            return TryMoveItem(
                item,
                targetX,
                targetY,
                isRotated
            );
        }

        // Shopは、売買実装前に無料で取れてしまわないようにする
        if (!AllowsDirectTransfer ||
            !targetGridUI.AllowsDirectTransfer)
        {
            return false;
        }

        if (!TryGetInventoryGrid(out InventoryGrid sourceGrid) ||
            !targetGridUI.TryGetInventoryGrid(
                out InventoryGrid targetGrid) ||
            !sourceGrid.ContainsItem(item))
        {
            return false;
        }

        bool finalRotation = item.CanRotate && isRotated;

        if (!targetGrid.CanPlaceItem(
                item,
                targetX,
                targetY,
                finalRotation))
        {
            return false;
        }

        int sourceX = item.GridX;
        int sourceY = item.GridY;
        bool sourceRotation = item.IsRotated;

        if (!RemoveItemFromThisGrid(item))
        {
            return false;
        }

        bool moved = targetGridUI.TryMoveItem(
            item,
            targetX,
            targetY,
            finalRotation
        );

        if (moved)
        {
            return true;
        }

        // 万一の失敗時は、元のGridへ戻す
        TryMoveItem(
            item,
            sourceX,
            sourceY,
            sourceRotation
        );

        return false;
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

        if (!TryGetInventoryGrid(out InventoryGrid grid))
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
        if (!TryGetInventoryGrid(out InventoryGrid grid))
        {
            return;
        }

        EnsureRoots();

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
        if (isSubscribed)
        {
            return;
        }

        if (itemBoxInventory != null)
        {
            itemBoxInventory.OnInventoryChanged +=
                HandleInventoryChanged;

            isSubscribed = true;
            return;
        }

        if (inventoryController == null)
        {
            inventoryController = GetComponent<InventoryController>();
        }

        if (inventoryController == null)
        {
            return;
        }

        inventoryController.OnInventoryChanged +=
            HandleInventoryChanged;

        isSubscribed = true;
    }

    private void UnsubscribeFromInventory()
    {
        if (!isSubscribed)
        {
            return;
        }

        if (itemBoxInventory != null)
        {
            itemBoxInventory.OnInventoryChanged -=
                HandleInventoryChanged;
        }
        else if (inventoryController != null)
        {
            inventoryController.OnInventoryChanged -=
                HandleInventoryChanged;
        }

        isSubscribed = false;
    }

    private void HandleInventoryChanged()
    {
        if (!TryGetInventoryGrid(out InventoryGrid grid))
        {
            return;
        }

        if (grid.Width != builtWidth || grid.Height != builtHeight)
        {
            RebuildGridUI();
            return;
        }

        RefreshItemsUI();
    }

    private bool RemoveItemFromThisGrid(InventoryItem item)
    {
        if (itemBoxInventory != null)
        {
            return itemBoxInventory.RemoveItem(item);
        }

        return inventoryController != null &&
               inventoryController.RemoveItem(item);
    }

    private bool TryGetInventoryGrid(out InventoryGrid grid)
    {
        grid = null;

        if (itemBoxInventory != null)
        {
            grid = itemBoxInventory.Grid;
            return grid != null;
        }

        if (inventoryController == null)
        {
            inventoryController = GetComponent<InventoryController>();
        }

        if (inventoryController == null)
        {
            return false;
        }

        grid = inventoryController.Grid;
        return grid != null;
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

        if (contextMenuUI == null)
        {
            contextMenuUI =
                FindAnyObjectByType<InventoryContextMenuUI>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void EnsureRoots()
    {
        // 非アクティブなPanel内のGrid UIでも、
        // ItemBoxUIControllerから先に接続できるようにする
        if (gridRect == null)
        {
            gridRect = GetComponent<RectTransform>();
        }

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
