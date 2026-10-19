using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

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
    [Header("STEP4 v1.3.5：Player所持品の自動参照")]
    [Tooltip("任意。普段Tabで開くインベントリ画面のInventoryGridUIを指定。Player本体にInventoryControllerは不要です。")]
    [SerializeField] private InventoryGridUI usualPlayerInventoryGridUI;
    [Tooltip("任意。Playerの位置。未指定ならPlayerタグから取得します。InventoryControllerの位置で距離判定しません。")]
    [SerializeField] private Transform playerPositionOverride;
    [Tooltip("任意。装備ControllerもPlayerと別Objectにある場合に割り当てます。未指定ならPlayer/SessionBridgeから探索します。")]
    [SerializeField] private EquipmentController playerEquipmentOverride;
    [SerializeField, Min(0.5f)] private float interactionRadius = 2.5f;
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private GUISkin optionalGuiSkin;
    [SerializeField] private bool showDebugLogs;
    [Header("町と同じインベントリ形式の売買UI")]
    [Tooltip("ONで左右グリッドの売買UIを表示。OFFなら従来の簡易IMGUI画面")]
    [SerializeField] private bool useInventoryGridTradeUI = true;
    [Tooltip("日本語表示が四角になる場合に日本語TMP FontAssetを設定")]
    [SerializeField] private TMP_FontAsset japaneseTradeFont;

    [Header("町と共通の売買Prefab（STEP4 v1.3.4）")]
    [Tooltip("ONなら町と同じPrefab/一括会計を使用。既存の簡易UIは使いません。")]
    [SerializeField] private bool useSharedTownTradeUI;
    [Tooltip("町UIから生成したPrefabのTownUnifiedTradeScreen2D。洞窟Canvas内に配置します。")]
    [SerializeField] private TownUnifiedTradeScreen2D sharedTownTradeScreen;
    [Tooltip("NPCの子に設けたMerchantStockInventory。DungeonNpcTradeProfileを自動読み込みします。")]
    [SerializeField] private MerchantStockInventory sharedMerchantStock;
    [Tooltip("任意。未指定ならPlayerのInventoryから検索します。")]
    [SerializeField] private PlayerTradeInventoryBridge sharedPlayerBridge;

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
    public InventoryController TradePlayerInventory { get { FindPlayer(); return playerInventory; } }
    public string TradeDisplayName => identity != null ? identity.DisplayName : name;
    public List<ItemData> TradeAllProducts() => new List<ItemData>(stock.Keys);
    public int TradeGetStock(ItemData item) => GetStock(item);
    public int TradeGetBuyPrice(ItemData item) => GetPrice(item);
    public int TradeGetSellPrice(ItemData item) => tradeProfile != null && item != null ? tradeProfile.GetSellPrice(item) : 0;
    public bool TradeCanSell(ItemData item) => tradeProfile != null && tradeProfile.WillBuy(item) && item != null && item.CanSellToShop;

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
            if (useSharedTownTradeUI && sharedTownTradeScreen != null &&
                !sharedTownTradeScreen.IsOpen)
            {
                // UIの閉じるボタンで閉じた場合もNPCの状態を同期。
                CloseShop();
                return;
            }
            if (!IsNpcAvailable() || Input.GetKeyDown(closeKey)) CloseShop();
            return;
        }
        if (!IsNpcAvailable() || !IsPlayerNearby()) return;
        if (Input.GetKeyDown(interactionKey)) OpenShop();
    }

    private bool IsNpcAvailable() => tradeProfile != null && (health == null || !health.IsDead) &&
        (carryable == null || (!carryable.IsCarried && !carryable.IsExternallyReserved));

    /// <summary>
    /// Player本体にInventoryControllerを増設しないこと。
    /// 普段のTabインベントリUIが使っているControllerを優先して取得する。
    /// </summary>
    private bool FindPlayer()
    {
        if (SharedTradePlayerInventoryResolver2D.TryResolve(
                this, usualPlayerInventoryGridUI, playerInventory,
                out InventoryController resolved, out string reason))
        {
            if (playerInventory != resolved && showDebugLogs)
                Debug.Log("[DungeonNpcShop2D] Player所持品の参照を接続: " + reason, this);
            playerInventory = resolved;
            return true;
        }
        if (showDebugLogs) Debug.LogWarning("[DungeonNpcShop2D] " + reason, this);
        return false;
    }

    /// <summary>InventoryのあるObjectではなく、Playerキャラクターの位置で距離判定。</summary>
    private Transform GetPlayerPositionTransform()
    {
        if (playerPositionOverride != null) return playerPositionOverride;
        Transform taggedPlayer = SharedTradePlayerInventoryResolver2D.FindPlayerTransform();
        if (taggedPlayer != null) return taggedPlayer;
        if (playerInventory == null) return null;
        for (Transform t = playerInventory.transform; t != null; t = t.parent)
            if (t.CompareTag("Player")) return t;
        // Playerタグが未設定なら位置を推測せず、取引距離エラーを明示する。
        return null;
    }

    private bool IsPlayerNearby()
    {
        Transform playerTransform = GetPlayerPositionTransform();
        if (playerTransform == null) return false;
        return ((Vector2)(playerTransform.position - transform.position)).sqrMagnitude <=
            interactionRadius * interactionRadius;
    }

    // 既存のUnityEventなどが使うvoid版はそのまま残します。
    public void OpenShop()
    {
        TryOpenShop(out _);
    }

    /// <summary>
    /// Dialogue Studio STEP4から呼ぶ詳細エラー付き版。
    /// 一般的なEキー操作でも、従来の条件を維持します。
    /// </summary>
    public bool TryOpenShop(out string resultMessage)
    {
        resultMessage = "";
        if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
        {
            resultMessage = "ショップ用コンポーネントまたはNPCが無効になっています";
            return false;
        }
        if (tradeProfile == null)
        {
            resultMessage = "商人のTrade Profileが未設定です（DungeonNpcShop2D）";
            return false;
        }
        if (health != null && health.IsDead)
        {
            resultMessage = "NPCが死亡しているため取引できません";
            return false;
        }
        if (carryable != null && (carryable.IsCarried || carryable.IsExternallyReserved))
        {
            resultMessage = "NPCが運搬中または予約中のため取引できません";
            return false;
        }
        if (!FindPlayer())
        {
            SharedTradePlayerInventoryResolver2D.TryResolve(this, usualPlayerInventoryGridUI,
                playerInventory, out _, out string inventoryError);
            resultMessage = inventoryError;
            return false;
        }
        if (GetPlayerPositionTransform() == null)
        {
            resultMessage = "Playerの位置を取得できません。PlayerタグまたはPlayer Position Overrideを設定してください。";
            return false;
        }
        if (!IsPlayerNearby())
        {
            Transform playerPosition = GetPlayerPositionTransform();
            float distance = playerPosition != null
                ? Vector2.Distance(playerPosition.position, transform.position)
                : float.PositiveInfinity;
            resultMessage = $"ショップの取引距離外です（現在{distance:0.00} / 許可{interactionRadius:0.00}）。Interaction Radiusを確認";
            return false;
        }
        var session = GameSessionManager.Instance;
        if (session == null)
        {
            resultMessage = "GameSessionManager.Instanceが見つかりません（シーンのSession初期化を確認）";
            return false;
        }
        session.EnsureMoneyInitialized();
        // 町型のPrefabを使う場合は、旧ショップUIや旧在庫を開かない。
        if (useSharedTownTradeUI)
        {
            if (sharedTownTradeScreen == null || sharedMerchantStock == null)
            {
                resultMessage = "共通売買UIの参照が未設定です（Trade Screen / Merchant Stock）";
                return false;
            }
            sharedMerchantStock.ConfigureDungeonProfile(tradeProfile);
            // Bridgeは任意。追加するとInventoryControllerが自動生成される旧設定を避ける。
            // 今ゲーム内で使っているTabインベントリのControllerを必ず渡す。
            if (sharedPlayerBridge == null)
                sharedPlayerBridge = playerInventory.GetComponent<PlayerTradeInventoryBridge>() ??
                    playerInventory.GetComponentInParent<PlayerTradeInventoryBridge>();
            if (sharedPlayerBridge != null &&
                sharedPlayerBridge.InventoryController != playerInventory)
            {
                // 別の空Inventoryを持つBridgeは絶対に採用しない。
                sharedPlayerBridge = null;
            }
            EquipmentController playerEquipment = playerEquipmentOverride != null
                ? playerEquipmentOverride
                : SharedTradePlayerInventoryResolver2D.FindEquipment(playerInventory, sharedPlayerBridge);
            sharedTownTradeScreen.BindDungeonPlayer(playerInventory, sharedPlayerBridge, session,
                playerEquipment);
            if (!sharedTownTradeScreen.Open(sharedMerchantStock))
            {
                resultMessage = "町の共通売買UIを開けません。GameSession・Grid・Shop在庫を確認してください。";
                return false;
            }
            isOpen = true;
            if (showDebugLogs)
                Debug.Log("[DungeonNpcShop2D] 売買UIが参照するPlayer所持品: " +
                    playerInventory.name + " / スタック=" + playerInventory.Grid.Items.Count, this);
            resultMessage = "共通の売買画面を開きました";
            return true;
        }
        isOpen = true;
        viewingBuy = true;
        statusMessage = "";
        resultMessage = "ショップを開きました";
        if (useInventoryGridTradeUI)
        {
            if (!DungeonNpcInventoryTradeUI2D.OpenFor(this, japaneseTradeFont, out resultMessage))
            {
                isOpen = false;
                return false;
            }
        }
        return true;
    }

    public void CloseShop()
    {
        isOpen = false;
        statusMessage = "";
        if (useSharedTownTradeUI)
        {
            if (sharedTownTradeScreen != null && sharedTownTradeScreen.IsOpen)
                sharedTownTradeScreen.Close();
        }
        else
        {
            DungeonNpcInventoryTradeUI2D.CloseFor(this);
        }
    }
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
        if (!Application.isPlaying || !isActiveAndEnabled || !IsNpcAvailable() || useInventoryGridTradeUI || useSharedTownTradeUI) return;
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
