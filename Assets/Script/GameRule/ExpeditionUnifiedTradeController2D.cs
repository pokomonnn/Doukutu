using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 出撃前の売買を1画面・1決済に統合する独立コンポーネント。
/// 商人Grid左クリック=購入候補 / PlayerGrid左クリック=売却候補。
/// スタック選択中はSliderで個数変更。選択段階では所持金・品物を移動しない。
/// 既存PawnShopUI/MerchantPurchaseControllerとは同時に開かない。
/// </summary>
[DisallowMultipleComponent]
public class ExpeditionUnifiedTradeController2D : MonoBehaviour
{
    [Header("既存データ")] 
    [SerializeField] private MerchantStockInventory merchantStock;
    [SerializeField] private InventoryGridUI merchantGridUI;
    [SerializeField] private InventoryGridUI playerGridUI;
    [SerializeField] private InventoryController playerInventory;
    [SerializeField] private SellCartInventory sellRules;
    [SerializeField] private PlayerTradeInventoryBridge townInventoryBridge;
    [SerializeField] private GameSessionManager sessionManager;

    [Header("金額表示")] 
    [SerializeField] private TMP_Text buyAmountText;
    [SerializeField] private TMP_Text sellAmountText;
    [SerializeField] private TMP_Text totalAmountText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text statusText;

    [Header("操作")] 
    [SerializeField] private Button checkoutButton;
    [SerializeField] private Slider quantitySlider;
    [SerializeField] private GameObject sliderRoot;
    [SerializeField] private TMP_Text quantityText;
    [SerializeField] private TMP_Text selectedItemText;
    [SerializeField] private bool allowSellSlider = true;
    [SerializeField] private bool disableCheckoutWhenEmpty = true;

    [Header("色/サウンド")] 
    [SerializeField] private Color buySelectedColor = new Color(0.16f, 0.65f, 0.97f, 0.38f);
    [SerializeField] private Color sellSelectedColor = new Color(0.96f, 0.59f, 0.19f, 0.38f);
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip selectionClip;
    [SerializeField] private AudioClip checkoutClip;
    [SerializeField] private AudioClip failClip;
    [SerializeField, Range(0f, 1f)] private float volume = 0.85f;

    // InventoryItemインスタンスは、InventoryGrid更新時に置き換わる場合がある。
    // 会計直前に必ず再検証する。
    private readonly Dictionary<InventoryItem, int> buying = new Dictionary<InventoryItem, int>();
    private readonly Dictionary<InventoryItem, int> selling = new Dictionary<InventoryItem, int>();
    private readonly List<RaycastResult> raycast = new List<RaycastResult>();
    private readonly List<InventoryItem> cleanup = new List<InventoryItem>();
    private bool isOpen;
    private bool isCheckingOut;
    private InventoryItem editingItem;
    private bool editingIsBuy;
    private bool ignoreSliderChange;
    private float nextVisualRefresh;
    private float nextValidation;

    public bool IsOpen => isOpen;
    public bool HasPendingSelections => buying.Count > 0 || selling.Count > 0;

    private void Awake()
    {
        checkoutButton?.onClick.AddListener(Checkout);
        quantitySlider?.onValueChanged.AddListener(OnQuantityChanged);
        if (sliderRoot != null) sliderRoot.SetActive(false);
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
    }

    private void OnDestroy()
    {
        checkoutButton?.onClick.RemoveListener(Checkout);
        quantitySlider?.onValueChanged.RemoveListener(OnQuantityChanged);
    }

    private void OnDisable()
    {
        isOpen = false;
        editingItem = null;
    }

    public bool OpenTrade(MerchantStockInventory stock)
    {
        if (stock != null) merchantStock = stock;
        if (sessionManager == null) sessionManager = GameSessionManager.Instance;
        if (playerInventory == null && playerGridUI != null) playerInventory = playerGridUI.Controller;
        if (playerInventory == null && townInventoryBridge != null)
            playerInventory = townInventoryBridge.InventoryController;
        if (playerInventory == null)
            playerInventory = FindAnyObjectByType<InventoryController>(FindObjectsInactive.Include);
        if (merchantStock == null || merchantGridUI == null || playerGridUI == null ||
            playerInventory == null || sessionManager == null)
        {
            SetStatus("売買に必要な参照が不足しています（店舗・2つのGrid・Inventory・GameSessionManager）。", true);
            return false;
        }
        if (merchantStock.ShopData == null)
        {
            SetStatus("MerchantStockInventoryのShopDataを設定してください。", true);
            return false;
        }
        merchantStock.PrepareForOpen();
        ItemBoxInventory stockInventory = merchantStock.StockInventory;
        if (stockInventory == null || stockInventory.BoxKind != ItemBoxKind.Shop || stockInventory.Grid == null)
        {
            SetStatus("商品棚のItemBoxInventoryのBox KindはShopにしてください。", true);
            return false;
        }
        if (playerInventory.Grid == null)
        {
            SetStatus("プレイヤーInventoryのGridが見つかりません。", true);
            return false;
        }
        if (sellRules != null) sellRules.SetMerchantStock(merchantStock);
        merchantGridUI.BindItemBoxInventory(stockInventory);
        playerGridUI.BindPlayerInventory(playerInventory);
        buying.Clear();
        selling.Clear();
        editingItem = null;
        isOpen = true;
        isCheckingOut = false;
        if (sliderRoot != null) sliderRoot.SetActive(false);
        SetStatus("商人の商品をクリックで購入選択／プレイヤーの商品をクリックで売却選択", false);
        RefreshTotals();
        RefreshSelectedVisuals();
        return true;
    }

    public void CloseTrade()
    {
        isOpen = false;
        buying.Clear();
        selling.Clear();
        editingItem = null;
        if (sliderRoot != null) sliderRoot.SetActive(false);
        RefreshSelectedVisuals();
        RefreshTotals();
    }

    private void Update()
    {
        if (!isOpen || isCheckingOut) return;
        if (Time.unscaledTime >= nextValidation)
        {
            nextValidation = Time.unscaledTime + 0.23f;
            PruneSelections();
            RefreshTotals();
        }
        if (Time.unscaledTime >= nextVisualRefresh)
        {
            nextVisualRefresh = Time.unscaledTime + 0.12f;
            RefreshSelectedVisuals();
        }
        if (!Input.GetMouseButtonUp(0) || EventSystem.current == null) return;
        raycast.Clear();
        PointerEventData pointer = new PointerEventData(EventSystem.current) { position = Input.mousePosition };
        EventSystem.current.RaycastAll(pointer, raycast);
        if (raycast.Count == 0) return;
        // 先頭のUIだけを見る（Slider/Buttonの奥の商品を選択しない）。
        InventoryItemUI itemUI = raycast[0].gameObject != null
            ? raycast[0].gameObject.GetComponentInParent<InventoryItemUI>() : null;
        if (itemUI == null || itemUI.Item == null) return;
        if (merchantGridUI.ContainsItem(itemUI.Item)) Toggle(itemUI.Item, true);
        else if (playerGridUI.ContainsItem(itemUI.Item)) Toggle(itemUI.Item, false);
    }

    private void Toggle(InventoryItem item, bool buy)
    {
        Dictionary<InventoryItem, int> selected = buy ? buying : selling;
        if (selected.Remove(item))
        {
            if (editingItem == item)
            {
                editingItem = null;
                if (sliderRoot != null) sliderRoot.SetActive(false);
            }
            SetStatus("選択を解除しました。", false);
        }
        else
        {
            if (buy && (merchantStock == null || merchantStock.GetUnitBuyPrice(item.ItemData) < 0))
            {
                SetStatus("この商品の価格が設定されていません。", true);
                return;
            }
            if (!buy && !CanSell(item, out string reason))
            {
                SetStatus(reason, true);
                Play(failClip);
                return;
            }
            // はじめは1個、スタック不可は全部（通常1個）。
            selected[item] = 1;
            editingItem = item;
            editingIsBuy = buy;
            ConfigureSlider(item, buy);
            SetStatus((buy ? "購入：" : "売却：") + item.ItemData.DisplayName + " を選択", false);
        }
        Play(selectionClip);
        RefreshTotals();
        RefreshSelectedVisuals();
    }

    private void ConfigureSlider(InventoryItem item, bool buy)
    {
        if (item == null || item.ItemData == null) return;
        int max = item.Amount;
        if (buy && merchantStock != null && merchantStock.ShopData != null)
            max = Mathf.Min(max, merchantStock.ShopData.MaxPurchaseAmount);
        max = Mathf.Max(1, max);
        bool show = quantitySlider != null && item.CanStack && (buy || allowSellSlider) && max > 1;
        if (sliderRoot != null) sliderRoot.SetActive(show);
        ignoreSliderChange = true;
        if (quantitySlider != null)
        {
            quantitySlider.wholeNumbers = true;
            quantitySlider.minValue = 1;
            quantitySlider.maxValue = max;
            quantitySlider.SetValueWithoutNotify(Mathf.Clamp((buy ? buying : selling)[item], 1, max));
        }
        ignoreSliderChange = false;
        if (selectedItemText != null) selectedItemText.text = item.ItemData.DisplayName;
        RefreshQuantityLabel();
    }

    private void OnQuantityChanged(float raw)
    {
        if (ignoreSliderChange || editingItem == null) return;
        Dictionary<InventoryItem, int> selected = editingIsBuy ? buying : selling;
        if (!selected.ContainsKey(editingItem)) return;
        int count = Mathf.Clamp(Mathf.RoundToInt(raw), 1, Mathf.Max(1, editingItem.Amount));
        selected[editingItem] = count;
        RefreshQuantityLabel();
        RefreshTotals();
    }

    private void RefreshQuantityLabel()
    {
        if (quantityText == null) return;
        if (editingItem == null)
        {
            quantityText.text = "数量：―";
            return;
        }
        Dictionary<InventoryItem, int> selected = editingIsBuy ? buying : selling;
        quantityText.text = selected.TryGetValue(editingItem, out int count)
            ? "選択数：" + count + " / " + editingItem.Amount : "数量：―";
    }

    private bool CanSell(InventoryItem item, out string reason)
    {
        reason = "";
        if (item == null || item.ItemData == null || item.Amount <= 0)
        {
            reason = "無効なアイテムです。";
            return false;
        }
        if (sellRules != null) return sellRules.CanSell(item, out reason);
        if (item.ItemData.ItemType == InventoryItemType.Quest || !item.ItemData.CanSellToShop)
        {
            reason = "このアイテムは売却できません。";
            return false;
        }
        return merchantStock == null || merchantStock.CanBuyFromPlayer(item.ItemData, out reason);
    }

    private int UnitSellPrice(InventoryItem item)
    {
        if (item == null || item.ItemData == null) return 0;
        return sellRules != null ? sellRules.GetUnitSellPrice(item)
            : Mathf.Max(0, item.ItemData.SellPrice);
    }

    private void PruneSelections()
    {
        RemoveInvalid(buying, true);
        RemoveInvalid(selling, false);
        if (editingItem != null && !(editingIsBuy ? buying : selling).ContainsKey(editingItem))
        {
            editingItem = null;
            if (sliderRoot != null) sliderRoot.SetActive(false);
        }
    }

    private void RemoveInvalid(Dictionary<InventoryItem, int> dict, bool buy)
    {
        cleanup.Clear();
        foreach (KeyValuePair<InventoryItem, int> pair in dict)
        {
            InventoryItem item = pair.Key;
            bool exists = item != null && item.ItemData != null &&
                (buy ? merchantGridUI.ContainsItem(item) : playerGridUI.ContainsItem(item));
            if (!exists || item.Amount <= 0 || (!buy && !CanSell(item, out _)))
                cleanup.Add(item);
            else if (pair.Value > item.Amount)
                cleanup.Add(item); // 在庫変動時は選択を解除し、誤会計しない
        }
        foreach (InventoryItem key in cleanup) dict.Remove(key);
    }

    private bool CalculateTotals(out long buyTotal, out long sellTotal)
    {
        buyTotal = 0;
        sellTotal = 0;
        foreach (KeyValuePair<InventoryItem, int> pair in buying)
        {
            if (pair.Key == null || pair.Key.ItemData == null || merchantStock == null) return false;
            buyTotal += (long)Mathf.Max(0, merchantStock.GetUnitBuyPrice(pair.Key.ItemData)) * pair.Value;
        }
        foreach (KeyValuePair<InventoryItem, int> pair in selling)
        {
            if (pair.Key == null || pair.Key.ItemData == null) return false;
            sellTotal += (long)UnitSellPrice(pair.Key) * pair.Value;
        }
        return buyTotal <= int.MaxValue && sellTotal <= int.MaxValue;
    }

    public void RefreshTotals()
    {
        if (!CalculateTotals(out long buy, out long sell))
        {
            if (statusText != null) statusText.text = "金額が上限を超えています。選択数を減らしてください。";
            return;
        }
        if (buyAmountText != null) buyAmountText.text = "買い金額  " + buy.ToString("N0") + " G";
        if (sellAmountText != null) sellAmountText.text = "売り金額  " + sell.ToString("N0") + " G";
        long net = buy - sell;
        if (totalAmountText != null)
            totalAmountText.text = net > 0 ? "差引支払  " + net.ToString("N0") + " G" :
                net < 0 ? "差引受取  " + (-net).ToString("N0") + " G" : "差引  0 G";
        if (moneyText != null)
            moneyText.text = "所持金  " + (sessionManager != null ? sessionManager.CurrentMoney.ToString("N0") : "―") + " G";
        if (checkoutButton != null && disableCheckoutWhenEmpty)
            checkoutButton.interactable = !isCheckingOut && HasPendingSelections;
        RefreshQuantityLabel();
    }

    private void RefreshSelectedVisuals()
    {
        PaintGrid(merchantGridUI, buying, buySelectedColor);
        PaintGrid(playerGridUI, selling, sellSelectedColor);
    }

    private static void PaintGrid(InventoryGridUI grid, Dictionary<InventoryItem, int> selected, Color color)
    {
        if (grid == null) return;
        InventoryItemUI[] itemUis = grid.GetComponentsInChildren<InventoryItemUI>(true);
        foreach (InventoryItemUI view in itemUis)
        {
            if (view == null || view.Item == null) continue;
            Transform overlay = view.transform.Find("ExpeditionTradeSelectionOverlay");
            bool show = selected.ContainsKey(view.Item);
            if (overlay == null && show)
            {
                GameObject go = new GameObject("ExpeditionTradeSelectionOverlay", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(view.transform, false);
                go.transform.SetAsLastSibling(); // item画像の上へ半透明で表示
                RectTransform rt = go.GetComponent<RectTransform>();
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.one;
                rt.offsetMin = Vector2.zero;
                rt.offsetMax = Vector2.zero;
                Image image = go.GetComponent<Image>();
                image.raycastTarget = false;
                overlay = go.transform;
            }
            if (overlay != null)
            {
                if (show) overlay.GetComponent<Image>().color = color;
                if (overlay.gameObject.activeSelf != show) overlay.gameObject.SetActive(show);
            }
        }
    }

    /// <summary>試算。選択した売却品を引いた盤面に、購入品を仮配置できるか判定。</summary>
    private bool FitsAfterSales()
    {
        InventoryGrid grid = playerInventory != null ? playerInventory.Grid : null;
        if (grid == null) return false;
        bool[,] occupied = new bool[grid.Width, grid.Height];
        Dictionary<ItemData, long> stackSpace = new Dictionary<ItemData, long>();
        foreach (InventoryItem item in grid.Items)
        {
            if (item == null || item.ItemData == null) continue;
            int sold = selling.TryGetValue(item, out int soldCount) ? soldCount : 0;
            int remain = Mathf.Max(0, item.Amount - sold);
            if (remain == 0) continue;
            Vector2Int sz = item.ItemData.GetSize(item.IsRotated);
            for (int y = item.GridY; y < item.GridY + sz.y; y++)
                for (int x = item.GridX; x < item.GridX + sz.x; x++)
                    if (x >= 0 && y >= 0 && x < grid.Width && y < grid.Height) occupied[x, y] = true;
            if (item.ItemData.CanStack)
            {
                if (!stackSpace.ContainsKey(item.ItemData)) stackSpace[item.ItemData] = 0;
                stackSpace[item.ItemData] += Mathf.Max(0, item.ItemData.MaxStack - remain);
            }
        }
        // 広いアイテム優先で配置。実際のInventoryGrid.TryAddItemと近い予測。
        List<KeyValuePair<InventoryItem, int>> groups = new List<KeyValuePair<InventoryItem, int>>(buying);
        groups.Sort((a, b) =>
        {
            Vector2Int sa = a.Key.ItemData.GetSize(false);
            Vector2Int sb = b.Key.ItemData.GetSize(false);
            return (sb.x * sb.y).CompareTo(sa.x * sa.y);
        });
        foreach (KeyValuePair<InventoryItem, int> entry in groups)
        {
            ItemData data = entry.Key.ItemData;
            int remaining = entry.Value;
            if (data.CanStack && stackSpace.TryGetValue(data, out long available))
            {
                int put = (int)Math.Min(remaining, available);
                remaining -= put;
                stackSpace[data] = available - put;
            }
            while (remaining > 0)
            {
                if (!ReserveSpace(occupied, grid.Width, grid.Height, data)) return false;
                remaining -= Mathf.Min(remaining, Mathf.Max(1, data.MaxStack));
            }
        }
        return true;
    }

    private static bool ReserveSpace(bool[,] used, int width, int height, ItemData data)
    {
        for (int rot = 0; rot < (data.CanRotate ? 2 : 1); rot++)
        {
            Vector2Int size = data.GetSize(rot == 1);
            for (int y = 0; y + size.y <= height; y++)
                for (int x = 0; x + size.x <= width; x++)
                {
                    bool fits = true;
                    for (int dy = 0; dy < size.y && fits; dy++)
                        for (int dx = 0; dx < size.x; dx++)
                            if (used[x + dx, y + dy]) { fits = false; break; }
                    if (!fits) continue;
                    for (int dy = 0; dy < size.y; dy++)
                        for (int dx = 0; dx < size.x; dx++) used[x + dx, y + dy] = true;
                    return true;
                }
        }
        return false;
    }

    private struct RemovedEntry
    {
        public ItemData Data;
        public int Amount;
        public RemovedEntry(ItemData data, int amount) { Data = data; Amount = amount; }
    }

    public void Checkout()
    {
        TryCheckout();
    }

    public bool TryCheckout()
    {
        if (!isOpen || isCheckingOut) return false;
        PruneSelections();
        if (!HasPendingSelections) return Fail("購入・売却するアイテムを選択してください。");
        if (merchantStock == null || merchantStock.StockInventory == null ||
            playerInventory == null || sessionManager == null) return Fail("売買システムへの参照が不足しています。");
        if (!CalculateTotals(out long buy, out long sell)) return Fail("金額上限を超えました。");
        long net = buy - sell;
        if (net > sessionManager.CurrentMoney) return Fail("所持金が足りません。差額を確認してください。");
        if (net < 0 && -net > int.MaxValue - (long)sessionManager.CurrentMoney)
            return Fail("所持金上限を超えるため精算できません。");
        foreach (KeyValuePair<InventoryItem, int> entry in buying)
        {
            if (!merchantStock.StockInventory.ContainsItem(entry.Key) || entry.Key.Amount < entry.Value || entry.Value <= 0)
                return Fail("商品の在庫が変わりました。選択し直してください。");
            int cap = merchantStock.ShopData != null ? merchantStock.ShopData.MaxPurchaseAmount : 999;
            if (entry.Value > cap) return Fail("1商品の購入上限を超えました。");
        }
        foreach (KeyValuePair<InventoryItem, int> entry in selling)
        {
            if (!playerInventory.Grid.ContainsItem(entry.Key) || entry.Key.Amount < entry.Value ||
                entry.Value <= 0 || !CanSell(entry.Key, out _))
                return Fail("売却品を確認できません。選択し直してください。");
        }
        if (!FitsAfterSales()) return Fail("購入後のプレイヤーInventoryに空きがありません。");

        // 本決済。最終金額を固定し、変化イベントで選択が変更されないよう同期処理する。
        EquipmentController backupEquipment = townInventoryBridge != null
            ? townInventoryBridge.EquipmentController : null;
        // 完全なInventorySnapshotを先に保存。武器の耐久や残弾も保持して失敗時に復元できる。
        if (!sessionManager.CapturePlayerInventory(playerInventory, backupEquipment))
            return Fail("安全なInventoryバックアップを作れないため決済を中止しました。");
        int moneyBefore = sessionManager.CurrentMoney;
        isCheckingOut = true;
        List<RemovedEntry> removedSales = new List<RemovedEntry>();
        List<RemovedEntry> removedStock = new List<RemovedEntry>();
        Dictionary<ItemData, int> purchasedBefore = new Dictionary<ItemData, int>();
        Dictionary<ItemData, int> purchasedAmount = new Dictionary<ItemData, int>();
        foreach (KeyValuePair<InventoryItem, int> entry in buying)
        {
            ItemData data = entry.Key.ItemData;
            if (!purchasedBefore.ContainsKey(data))
            {
                purchasedBefore[data] = playerInventory.GetTotalAmount(data);
                purchasedAmount[data] = 0;
            }
            purchasedAmount[data] += entry.Value;
        }
        string failure = null;
        try
        {
            // 売却で空くマスを先に確保。途中失敗時は購入品を戻してから復元する。
            foreach (KeyValuePair<InventoryItem, int> entry in selling)
            {
                int removed = playerInventory.RemoveItemAmount(entry.Key, entry.Value);
                if (removed > 0) removedSales.Add(new RemovedEntry(entry.Key.ItemData, removed));
                if (removed != entry.Value) { failure = "売却品の取り出しに失敗"; break; }
            }
            if (failure == null)
                foreach (KeyValuePair<InventoryItem, int> entry in buying)
                {
                    int removed = merchantStock.StockInventory.RemoveItemAmount(entry.Key, entry.Value);
                    if (removed > 0) removedStock.Add(new RemovedEntry(entry.Key.ItemData, removed));
                    if (removed != entry.Value) { failure = "商品在庫の取り出しに失敗"; break; }
                }
            if (failure == null)
                foreach (KeyValuePair<ItemData, int> entry in purchasedAmount)
                {
                    bool added = playerInventory.TryAddItem(entry.Key, entry.Value, out int remaining);
                    if (!added || remaining > 0) { failure = "Inventoryへの購入品収納に失敗"; break; }
                }
            if (failure == null)
            {
                if (net > 0 && !sessionManager.TrySpendMoney((int)net))
                    failure = "所持金の支払いに失敗";
                else if (net < 0 && !sessionManager.AddMoney((int)(-net)))
                    failure = "売却代金の受け取りに失敗";
            }
        }
        catch (Exception e)
        {
            Debug.LogException(e, this);
            failure = "決済処理中に例外が発生";
        }
        if (failure != null)
        {
            // 先ほどのGameSessionSnapshotから位置・装備・武器耐久・残弾まで復元する。
            // 商品のstockは取り出し済み分を元へ戻す。
            sessionManager.SetMoney(moneyBefore);
            foreach (RemovedEntry removed in removedStock)
            {
                bool restored = merchantStock.StockInventory.TryAddItem(removed.Data, removed.Amount, out int missing);
                if (!restored || missing > 0)
                    Debug.LogError("[ExpeditionUnifiedTrade] 商人在庫の復元不足：" + removed.Data.DisplayName + " x" + missing, this);
            }
            bool restoredInventory = sessionManager.RestorePlayerInventory(
                playerInventory, backupEquipment, out string restoreMessage);
            if (!restoredInventory)
                Debug.LogError("[ExpeditionUnifiedTrade] 緊急復元に失敗しました：" + restoreMessage, this);
            isCheckingOut = false;
            buying.Clear();
            selling.Clear();
            editingItem = null;
            if (sliderRoot != null) sliderRoot.SetActive(false);
            RefreshGridAndTotals();
            return Fail(failure + "。Inventory復元ログも確認してください。");
        }
        isCheckingOut = false;
        buying.Clear();
        selling.Clear();
        editingItem = null;
        if (sliderRoot != null) sliderRoot.SetActive(false);
        CapturePlayerSession();
        RefreshGridAndTotals();
        SetStatus("決済完了：購入 " + buy.ToString("N0") + " G / 売却 " + sell.ToString("N0") + " G", false);
        Play(checkoutClip);
        return true;
    }

    private void CapturePlayerSession()
    {
        if (townInventoryBridge != null && townInventoryBridge.SessionBridge != null &&
            townInventoryBridge.InventoryController == playerInventory)
        {
            townInventoryBridge.SessionBridge.CaptureToSession();
            return;
        }
        if (sessionManager != null && playerInventory != null)
            sessionManager.CapturePlayerInventory(playerInventory,
                townInventoryBridge != null ? townInventoryBridge.EquipmentController : null);
    }

    private void RefreshGridAndTotals()
    {
        merchantGridUI?.RefreshInventoryUI();
        playerGridUI?.RefreshInventoryUI();
        RefreshTotals();
        RefreshSelectedVisuals();
    }

    private bool Fail(string reason)
    {
        isCheckingOut = false;
        SetStatus(reason, true);
        Play(failClip);
        RefreshTotals();
        return false;
    }

    private void SetStatus(string message, bool warning)
    {
        if (statusText != null) statusText.text = message;
        if (warning) Debug.LogWarning("[ExpeditionUnifiedTrade] " + message, this);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, volume);
    }
}
