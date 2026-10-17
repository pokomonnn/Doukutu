using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ダンジョンNPC専用の購入・売却窓。Eで開く。
/// 実装初版のUIはUnityのIMGUIを利用。後からuGUI/TMPへ差し替え可能。
/// </summary>
[DisallowMultipleComponent]
public sealed class DungeonNpcShop2D : MonoBehaviour
{
    [Header("商人")]
    [SerializeField] private DungeonNpcTradeProfile2D tradeProfile;
    [SerializeField] private InventoryController playerInventory;
    [SerializeField, Min(0.5f)] private float interactionRadius = 2.5f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private GUISkin optionalGuiSkin;
    [SerializeField] private bool showDebugLogs;

    private readonly Dictionary<ItemData, int> stock = new Dictionary<ItemData, int>();
    private bool isOpen;
    private bool viewingBuy = true;
    private Vector2 buyScroll;
    private Vector2 sellScroll;
    private int npcMoney = -1;
    private Rect windowRect = new Rect(20, 20, 600, 450);
    private string statusMessage = "";
    private CharacterHealth health;
    private CarryableObject2D carryable;
    private DungeonNpcIdentity2D identity;
    private GUIStyle headerStyle;
    private GUIStyle smallStyle;
    private GUIStyle statusStyle;
    private int cachedStyleFontSize;
    private GUISkin cachedStyleSkin;

    public bool IsOpen => isOpen;

    private void Awake()
    {
        health = GetComponent<CharacterHealth>();
        carryable = GetComponent<CarryableObject2D>();
        identity = GetComponent<DungeonNpcIdentity2D>();
        RebuildStock();
    }

    public void RebuildStock()
    {
        stock.Clear();
        npcMoney = tradeProfile != null ? tradeProfile.startingNpcMoney : -1;
        if (tradeProfile == null) return;
        foreach (var entry in tradeProfile.products)
        {
            if (entry != null && entry.item != null && entry.stock > 0)
            {
                stock.TryGetValue(entry.item, out int existing);
                stock[entry.item] = Mathf.Min(int.MaxValue, (int)Math.Min((long)existing + entry.stock, int.MaxValue));
            }
        }
    }

    private void Update()
    {
        if (isOpen)
        {
            if (!IsNpcAvailable() || Input.GetKeyDown(closeKey)) CloseShop();
            return;
        }
        if (!IsNpcAvailable() || !IsPlayerNearby()) return;
        if (Input.GetKeyDown(interactionKey)) OpenShop();
    }

    private bool IsNpcAvailable() => tradeProfile != null && (health == null || !health.IsDead) &&
        (carryable == null || (!carryable.IsCarried && !carryable.IsExternallyReserved));

    private bool FindPlayer()
    {
        if (playerInventory != null && playerInventory.gameObject.activeInHierarchy) return true;
        var all = FindObjectsByType<InventoryController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (var inv in all)
        {
            if (inv == null) continue;
            Transform current = inv.transform;
            while (current != null)
            {
                if (current.CompareTag("Player")) { playerInventory = inv; return true; }
                current = current.parent;
            }
        }
        return false;
    }

    private bool IsPlayerNearby()
    {
        if (!FindPlayer()) return false;
        return ((Vector2)(playerInventory.transform.position - transform.position)).sqrMagnitude <=
            interactionRadius * interactionRadius;
    }

    public void OpenShop()
    {
        if (!IsNpcAvailable() || !IsPlayerNearby()) return;
        var session = GameSessionManager.Instance;
        if (session == null) { statusMessage = "GameSessionManager が見つかりません"; return; }
        session.EnsureMoneyInitialized();
        isOpen = true;
        viewingBuy = true;
        statusMessage = "";
    }

    public void CloseShop() { isOpen = false; statusMessage = ""; }
    private void OnDisable() => CloseShop();

    private int GetStock(ItemData item) => item != null && stock.TryGetValue(item, out int n) ? n : 0;

    private int GetPrice(ItemData item)
    {
        if (tradeProfile == null || item == null) return 0;
        foreach (var e in tradeProfile.products)
            if (e != null && e.item == item)
                return tradeProfile.GetBuyPrice(item, e.priceOverride);
        return tradeProfile.GetBuyPrice(item);
    }

    /// <summary>1個購入。支払い成功後にInventoryへ入らなければ同額を返金。</summary>
    public bool TryBuy(ItemData item, out string result)
    {
        result = "購入できません";
        var session = GameSessionManager.Instance;
        if (session == null || !FindPlayer() || !IsNpcAvailable() || item == null) return false;
        if (GetStock(item) < 1) { result = "売り切れです"; return false; }
        int price = GetPrice(item);
        if (!session.CanAfford(price)) { result = "所持金が足りません"; return false; }
        if (!session.TrySpendMoney(price)) { result = "支払いに失敗しました"; return false; }

        try
        {
            bool added = playerInventory.TryAddItem(item, 1, out int remaining);
            if (!added || remaining > 0)
            {
                if (price > 0) session.AddMoney(price);
                result = "Inventoryの空きが足りません（返金済み）";
                return false;
            }
        }
        catch (Exception ex)
        {
            if (price > 0) session.AddMoney(price);
            Debug.LogException(ex, this);
            result = "収納中にエラーが発生しました（返金済み）";
            return false;
        }

        stock[item] = GetStock(item) - 1;
        if (npcMoney >= 0) npcMoney = (int)Math.Min((long)npcMoney + price, int.MaxValue);
        result = item.DisplayName + " を購入しました (" + price + "G)";
        if (showDebugLogs) Debug.Log("[DungeonNpcShop2D] " + result, this);
        return true;
    }

    /// <summary>1個売却。対象のInventoryItemが無ければ出金しません。</summary>
    public bool TrySell(ItemData item, out string result)
    {
        result = "売却できません";
        var session = GameSessionManager.Instance;
        if (session == null || !FindPlayer() || !IsNpcAvailable() || item == null) return false;
        if (!tradeProfile.WillBuy(item)) { result = "このNPCはそのItemを買い取りません"; return false; }
        int price = tradeProfile.GetSellPrice(item);
        if (npcMoney >= 0 && npcMoney < price) { result = "NPCの買取資金が足りません"; return false; }
        if (price > 0 && session.CurrentMoney > int.MaxValue - price) { result = "所持金の上限に達します"; return false; }

        InventoryItem owned = null;
        foreach (var entry in playerInventory.Grid.Items)
        {
            if (entry != null && entry.ItemData == item && entry.Amount > 0) { owned = entry; break; }
        }
        if (owned == null) { result = "所持していません"; return false; }
        int removed = playerInventory.RemoveItemAmount(owned, 1);
        if (removed != 1) { result = "Itemを取り出せませんでした"; return false; }
        if (price > 0 && !session.AddMoney(price))
        {
            playerInventory.TryAddItem(item, 1, out _);
            result = "入金に失敗したため売却を取り消しました";
            return false;
        }
        if (npcMoney >= 0) npcMoney -= price;
        if (tradeProfile.addSoldItemsToStock)
            stock[item] = Mathf.Min(int.MaxValue, GetStock(item) + 1);
        result = item.DisplayName + " を売却しました (+" + price + "G)";
        if (showDebugLogs) Debug.Log("[DungeonNpcShop2D] " + result, this);
        return true;
    }

    private void OnGUI()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || !IsNpcAvailable()) return;
        if (!isOpen)
        {
            if (IsPlayerNearby())
            {
                Rect prompt = new Rect(Mathf.Max(10, Screen.width / 2 - 160), Screen.height - 115, 320, 35);
                GUI.Box(prompt, "[" + interactionKey + "] " + (identity != null ? identity.DisplayName : name) + " と売買");
            }
            return;
        }
        GUISkin previousSkin = GUI.skin;
        if (optionalGuiSkin != null) GUI.skin = optionalGuiSkin;
        windowRect.width = Mathf.Min(630, Screen.width - 20);
        windowRect.height = Mathf.Min(490, Screen.height - 20);
        windowRect.x = (Screen.width - windowRect.width) / 2f;
        windowRect.y = (Screen.height - windowRect.height) / 2f;
        windowRect = GUI.Window(GetInstanceID(), windowRect, DrawWindow, (identity != null ? identity.DisplayName : name) + " - 取引");
        GUI.skin = previousSkin;
    }

    private void DrawWindow(int windowId)
    {
        GUILayout.Space(10);
        var session = GameSessionManager.Instance;
        GUILayout.Label("所持金: " + (session != null ? session.CurrentMoney.ToString("N0") : "?") + " G  /  " +
                        (npcMoney < 0 ? "NPC資金: 無制限" : "NPC資金: " + npcMoney.ToString("N0") + " G"));
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("購入", GUILayout.Height(34))) viewingBuy = true;
        if (GUILayout.Button("売却", GUILayout.Height(34))) viewingBuy = false;
        if (GUILayout.Button("閉じる", GUILayout.Width(80), GUILayout.Height(34))) CloseShop();
        GUILayout.EndHorizontal();
        if (!string.IsNullOrEmpty(statusMessage)) GUILayout.Label("▶ " + statusMessage);
        if (viewingBuy) DrawBuy(); else DrawSell();
        GUI.DragWindow(new Rect(0, 0, windowRect.width, 24));
    }

    private void DrawBuy()
    {
        buyScroll = GUILayout.BeginScrollView(buyScroll, GUILayout.Height(Mathf.Max(50, windowRect.height - 145)));
        var items = new List<ItemData>(stock.Keys);
        items.Sort((a, b) => string.Compare(a != null ? a.DisplayName : "", b != null ? b.DisplayName : "", StringComparison.Ordinal));
        foreach (var item in items)
        {
            if (item == null) continue;
            int available = GetStock(item);
            GUILayout.BeginHorizontal("box");
            GUILayout.Label(item.DisplayName, GUILayout.MinWidth(100), GUILayout.ExpandWidth(true));
            GUILayout.Label(GetPrice(item) + "G / 在庫 " + available, GUILayout.Width(145));
            GUI.enabled = available > 0;
            if (GUILayout.Button("購入", GUILayout.Width(70))) TryBuy(item, out statusMessage);
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
    }

    private void DrawSell()
    {
        sellScroll = GUILayout.BeginScrollView(sellScroll, GUILayout.Height(Mathf.Max(50, windowRect.height - 145)));
        if (playerInventory != null && playerInventory.Grid != null)
        {
            var amounts = new Dictionary<ItemData, int>();
            foreach (var entry in playerInventory.Grid.Items)
            {
                if (entry == null || entry.ItemData == null) continue;
                amounts.TryGetValue(entry.ItemData, out int old);
                amounts[entry.ItemData] = old + entry.Amount;
            }
            var items = new List<ItemData>(amounts.Keys);
            items.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.Ordinal));
            foreach (var item in items)
            {
                bool accepted = tradeProfile != null && tradeProfile.WillBuy(item);
                GUILayout.BeginHorizontal("box");
                GUILayout.Label(item.DisplayName + " ×" + amounts[item], GUILayout.MinWidth(120), GUILayout.ExpandWidth(true));
                GUILayout.Label(accepted ? tradeProfile.GetSellPrice(item) + "G" : "買取不可", GUILayout.Width(100));
                GUI.enabled = accepted;
                if (GUILayout.Button("1個売却", GUILayout.Width(85))) TrySell(item, out statusMessage);
                GUI.enabled = true;
                GUILayout.EndHorizontal();
            }
        }
        GUILayout.EndScrollView();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
