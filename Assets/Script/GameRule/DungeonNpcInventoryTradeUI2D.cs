using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// 洞窟NPCショップの見た目を、町の左右インベントリグリッド方式に統一する画面。
/// 商品・Player所持品を実際の占有マス数で表示。売買自体は既存DungeonNpcShop2Dに委譲。
/// 町のExpeditionUnifiedTradeController2Dとは別の決済処理です。
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonNpcInventoryTradeUI2D : MonoBehaviour
{
    private static DungeonNpcInventoryTradeUI2D instance;
    private static readonly Color Backdrop = Hex("#171B1D");
    private static readonly Color Panel = Hex("#242A2C");
    private static readonly Color Cell = Hex("#303638");
    private static readonly Color Border = Hex("#525A58");
    private static readonly Color Paper = Hex("#E5E1D6");
    private static readonly Color Accent = Hex("#A79A83");
    private static readonly Color SelectedColor = Hex("#9B8060");
    private const float CellSize = 53f;
    private const int StockColumns = 10;
    private const int StockRows = 24;

    private DungeonNpcShop2D shop;
    private Canvas canvas;
    private GameObject overlay;
    private RectTransform merchantItems;
    private RectTransform playerItems;
    private TMP_Text shopName;
    private TMP_Text selectionText;
    private TMP_Text moneyText;
    private TMP_Text statusText;
    private Button buyButton;
    private Button sellButton;
    private TMP_FontAsset japaneseFont;
    private ItemData selectedBuy;
    private ItemData selectedSell;
    private int playerColumns;
    private int playerRows;
    private readonly List<GameObject> merchantCells = new List<GameObject>();
    private readonly List<GameObject> playerCells = new List<GameObject>();

    public static bool OpenFor(DungeonNpcShop2D source, TMP_FontAsset font, out string message)
    {
        message = string.Empty;
        if (source == null) { message = "ショップ参照がありません"; return false; }
        if (instance == null)
        {
            GameObject manager = new GameObject("DungeonNpcInventoryTradeUI2D_Manager");
            instance = manager.AddComponent<DungeonNpcInventoryTradeUI2D>();
        }
        return instance.Open(source, font, out message);
    }

    public static void CloseFor(DungeonNpcShop2D source)
    {
        if (instance != null && instance.shop == source) instance.CloseVisualOnly();
    }

    private bool Open(DungeonNpcShop2D source, TMP_FontAsset font, out string message)
    {
        message = "";
        // 別の店が既に開いていれば閉じる。
        if (shop != null && shop != source) shop.CloseShop();
        shop = source;
        japaneseFont = font != null ? font : TMP_Settings.defaultFontAsset;
        InventoryController inv = shop.TradePlayerInventory;
        if (inv == null || inv.Grid == null)
        {
            message = "PlayerのInventoryGridを取得できません";
            shop = null;
            return false;
        }
        playerColumns = Mathf.Max(1, inv.Grid.Width);
        playerRows = Mathf.Max(1, inv.Grid.Height);
        try
        {
            if (overlay == null) BuildUI();
            overlay.SetActive(true);
            overlay.transform.SetAsLastSibling();
            selectedBuy = null;
            selectedSell = null;
            statusText.text = "商品を選んで購入、所持品を選んで売却します";
            RefreshAll();
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogException(ex, this);
            message = "グリッド型ショップUIの生成に失敗しました（Consoleを確認）";
            CloseVisualOnly();
            return false;
        }
    }

    private void Update()
    {
        if (shop != null && !shop.IsOpen) CloseVisualOnly();
        if (shop != null && overlay != null && overlay.activeSelf && Input.GetKeyDown(KeyCode.Escape)) shop.CloseShop();
    }

    private void CloseVisualOnly()
    {
        if (overlay != null) overlay.SetActive(false);
        shop = null;
        selectedBuy = null;
        selectedSell = null;
    }

    private static Color Hex(string code) { ColorUtility.TryParseHtmlString(code, out Color color); return color; }

    private static GameObject Node(string name, Transform parent, params Type[] components)
    {
        var obj = new GameObject(name, components);
        if (parent != null) obj.transform.SetParent(parent, false);
        return obj;
    }

    private static RectTransform Rect(string name, Transform parent, float x, float y, float w, float h)
    {
        var obj = Node(name, parent, typeof(RectTransform));
        var r = obj.GetComponent<RectTransform>();
        r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
        r.pivot = new Vector2(0f, 1f);
        r.anchoredPosition = new Vector2(x, -y);
        r.sizeDelta = new Vector2(w, h);
        return r;
    }

    private static Image Plate(RectTransform rect, Color color)
    {
        Image image = rect.gameObject.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = true;
        return image;
    }

    private TMP_Text Text(string name, Transform parent, string content, int size, float x, float y, float w, float h, Color color)
    {
        RectTransform rect = Rect(name, parent, x, y, w, h);
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.color = color;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        if (japaneseFont != null) text.font = japaneseFont;
        return text;
    }

    private Button MakeButton(string name, Transform parent, string label, float x, float y, float w, float h, Action onClick)
    {
        RectTransform rect = Rect(name, parent, x, y, w, h);
        Plate(rect, Panel);
        var button = rect.gameObject.AddComponent<Button>();
        var colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = Hex("#C0B19C");
        colors.pressedColor = Accent;
        button.colors = colors;
        if (onClick != null) button.onClick.AddListener(() => onClick());
        var t = Text("Label", rect, label, 17, 6, 3, w - 12, h - 6, Paper);
        t.alignment = TextAlignmentOptions.Center;
        return button;
    }

    private void BuildUI()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            if (c.isRootCanvas && c.renderMode != RenderMode.WorldSpace) { canvas = c; break; }
        }
        if (canvas == null)
        {
            var c = Node("DungeonNpcTradeCanvas", null, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = c.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = c.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
        }
        if (FindFirstObjectByType<EventSystem>() == null)
            Debug.LogWarning("[DungeonNpcTrade] EventSystemがありません。既存のInputSystem UI EventSystemをシーンに用意してください。");

        overlay = Node("DungeonNpcGridTradeRoot", canvas.transform, typeof(RectTransform), typeof(Image));
        var full = overlay.GetComponent<RectTransform>();
        full.anchorMin = Vector2.zero;
        full.anchorMax = Vector2.one;
        full.offsetMin = full.offsetMax = Vector2.zero;
        overlay.GetComponent<Image>().color = Hex("#0B1011E8");
        RectTransform window = Rect("DungeonNpcTradeWindow", full, 0, 0, 1180, 770);
        window.anchorMin = window.anchorMax = new Vector2(.5f, .5f);
        window.pivot = new Vector2(.5f, .5f);
        window.anchoredPosition = Vector2.zero;
        Plate(window, Backdrop);
        var outline = window.gameObject.AddComponent<Outline>();
        outline.effectColor = Border;
        outline.effectDistance = new Vector2(1, -1);
        Text("Title", window, "取引", 29, 24, 14, 250, 49, Paper);
        shopName = Text("MerchantName", window, "商人", 20, 300, 20, 640, 40, Accent);
        Text("LeftTitle", window, "商人の在庫  /  購入", 21, 27, 83, 525, 32, Paper);
        Text("RightTitle", window, "所持品  /  売却", 21, 612, 83, 520, 32, Paper);
        RectTransform left = Rect("MerchantStockPanel", window, 24, 124, 550, 474);
        Plate(left, Panel);
        RectTransform right = Rect("PlayerStockPanel", window, 605, 124, 550, 474);
        Plate(right, Panel);
        merchantItems = CreateScrollableGrid(left, "MerchantInventoryGrid", 10, StockRows);
        playerItems = CreateScrollableGrid(right, "PlayerInventoryGrid", playerColumns, playerRows);
        selectionText = Text("SelectionText", window, "", 15, 29, 610, 1100, 24, Accent);
        RectTransform bottom = Rect("TradeFooter", window, 24, 643, 1130, 105);
        Plate(bottom, Panel);
        moneyText = Text("Money", bottom, "所持金 0G", 18, 18, 13, 330, 30, Paper);
        statusText = Text("Status", bottom, "", 15, 18, 56, 720, 33, Paper);
        buyButton = MakeButton("BuySelected", bottom, "選択商品を購入", 510, 8, 188, 45, () => RunTrade(true));
        sellButton = MakeButton("SellSelected", bottom, "選択所持品を売却", 708, 8, 188, 45, () => RunTrade(false));
        MakeButton("Close", bottom, "戻る", 906, 8, 205, 45, () => { if (shop != null) shop.CloseShop(); });
        overlay.SetActive(false);
    }

    private RectTransform CreateScrollableGrid(RectTransform panel, string name, int columns, int rows)
    {
        RectTransform viewport = Rect(name + "_Viewport", panel, 10, 10, 528, 450);
        Plate(viewport, Hex("#202628"));
        viewport.gameObject.AddComponent<RectMask2D>();
        RectTransform content = Rect(name + "_Content", viewport, 0, 0, Mathf.Max(528, columns * CellSize), Mathf.Max(450, rows * CellSize));
        var scrollRect = viewport.gameObject.AddComponent<ScrollRect>();
        scrollRect.viewport = viewport;
        scrollRect.content = content;
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.scrollSensitivity = 26;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        return content;
    }

    private static void Clear(Transform root, List<GameObject> cache)
    {
        foreach (var item in cache) if (item != null) { item.SetActive(false); Destroy(item); }
        cache.Clear();
    }

    private void BackgroundCells(RectTransform root, int columns, int rows, List<GameObject> cache)
    {
        root.sizeDelta = new Vector2(Mathf.Max(columns * CellSize, 528), Mathf.Max(rows * CellSize, 450));
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            RectTransform cell = Rect("Cell", root, x * CellSize + 1, y * CellSize + 1, CellSize - 2, CellSize - 2);
            Plate(cell, Cell);
            cache.Add(cell.gameObject);
        }
    }

    private void DrawItem(RectTransform grid, List<GameObject> cache, ItemData data, int x, int y, int w, int h, int amount, int price, bool buy)
    {
        if (data == null) return;
        RectTransform rect = Rect("Item_" + data.name, grid, x * CellSize + 2, y * CellSize + 2,
            CellSize * w - 4, CellSize * h - 4);
        Plate(rect, (buy ? selectedBuy == data : selectedSell == data) ? SelectedColor : Hex("#58645D"));
        var border = rect.gameObject.AddComponent<Outline>();
        border.effectColor = Accent;
        border.effectDistance = new Vector2(1, -1);
        if (data.Icon != null)
        {
            RectTransform ir = Rect("Icon", rect, 4, 4, rect.sizeDelta.x - 8, rect.sizeDelta.y - 8);
            Image icon = ir.gameObject.AddComponent<Image>();
            icon.sprite = data.Icon;
            icon.preserveAspect = true;
            icon.raycastTarget = false;
        }
        TMP_Text count = Text("Count", rect, "×" + amount, 13, 3, rect.sizeDelta.y - 22, rect.sizeDelta.x - 8, 19, Paper);
        count.alignment = TextAlignmentOptions.BottomRight;
        // アイコンがなくても品名で識別できるようにする。
        if (data.Icon == null)
        {
            var nameLabel = Text("ItemName", rect, data.DisplayName, 12, 3, 3, rect.sizeDelta.x - 6, Mathf.Max(16, rect.sizeDelta.y - 23), Paper);
            nameLabel.enableWordWrapping = true;
            nameLabel.alignment = TextAlignmentOptions.Center;
        }
        Button button = rect.gameObject.AddComponent<Button>();
        button.onClick.AddListener(() => {
            if (buy) selectedBuy = data; else selectedSell = data;
            if (selectionText != null) selectionText.text = data.DisplayName + " / " + price + " G / " + (buy ? "購入" : "売却");
            RefreshAll();
        });
        cache.Add(rect.gameObject);
    }

    private void RefreshAll()
    {
        if (shop == null || overlay == null || !overlay.activeSelf) return;
        shopName.text = shop.TradeDisplayName;
        InventoryController inventory = shop.TradePlayerInventory;
        moneyText.text = "所持金  " + (GameSessionManager.Instance != null ? GameSessionManager.Instance.CurrentMoney.ToString("N0") : "-") + " G";
        buyButton.interactable = selectedBuy != null && shop.TradeGetStock(selectedBuy) > 0;
        sellButton.interactable = selectedSell != null && shop.TradeCanSell(selectedSell) && inventory != null && inventory.GetTotalAmount(selectedSell) > 0;
        Clear(merchantItems, merchantCells);
        Clear(playerItems, playerCells);
        BackgroundCells(merchantItems, StockColumns, StockRows, merchantCells);
        BackgroundCells(playerItems, playerColumns, playerRows, playerCells);

        bool[,] used = new bool[StockColumns, StockRows];
        foreach (ItemData data in shop.TradeAllProducts())
        {
            if (data == null || shop.TradeGetStock(data) < 1) continue;
            Vector2Int sz = data.GetSize(false);
            sz.x = Mathf.Clamp(sz.x, 1, StockColumns);
            sz.y = Mathf.Clamp(sz.y, 1, StockRows);
            bool placed = false;
            for (int y=0;y<=StockRows-sz.y && !placed;y++)
            for (int x=0;x<=StockColumns-sz.x && !placed;x++)
            {
                bool free=true;
                for(int iy=0;iy<sz.y;iy++) for(int ix=0;ix<sz.x;ix++) if(used[x+ix,y+iy]) free=false;
                if(!free) continue;
                for(int iy=0;iy<sz.y;iy++) for(int ix=0;ix<sz.x;ix++) used[x+ix,y+iy]=true;
                DrawItem(merchantItems,merchantCells,data,x,y,sz.x,sz.y,shop.TradeGetStock(data),shop.TradeGetBuyPrice(data),true);
                placed=true;
            }
            if(!placed) Debug.LogWarning("[DungeonNpcTrade] 商人在庫グリッドがいっぱいです: " + data.DisplayName, shop);
        }

        if (inventory != null && inventory.Grid != null)
        {
            foreach (InventoryItem item in inventory.Grid.Items)
            {
                if (item == null || item.ItemData == null || item.Amount <= 0) continue;
                int x = item.GridX, y = item.GridY;
                if (x < 0 || y < 0 || x + item.Width > playerColumns || y + item.Height > playerRows) continue;
                DrawItem(playerItems,playerCells,item.ItemData,x,y,item.Width,item.Height,item.Amount,
                    shop.TradeCanSell(item.ItemData) ? shop.TradeGetSellPrice(item.ItemData) : 0,false);
            }
        }
    }

    private void RunTrade(bool buy)
    {
        if (shop == null) return;
        ItemData item = buy ? selectedBuy : selectedSell;
        if (item == null) { statusText.text = "アイテムを選択してください"; return; }
        string message;
        bool success = buy ? shop.TryBuy(item, out message) : shop.TrySell(item, out message);
        statusText.text = message;
        if (success) RefreshAll();
    }
}
