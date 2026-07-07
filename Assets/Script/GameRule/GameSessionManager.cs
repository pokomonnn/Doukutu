using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// シーンをまたいで残したいゲームデータを管理します。
/// 所持金に加え、プレイヤーのインベントリと装備の一時データを保持します。
/// このデータはプレイ中のシーン移動用です。ゲーム終了後まで残すセーブ機能は別途追加します。
/// </summary>
[DisallowMultipleComponent]
public class GameSessionManager : MonoBehaviour
{
    [Header("初回起動時の所持金")]
    [Tooltip("PlayerMoneyController が開始金額を引き継がない場合に使う初期所持金です。通常は0のままでOKです。")]
    [SerializeField, Min(0)] private int defaultStartingMoney = 0;

    [Header("通常デバッグ")]
    [SerializeField] private bool showDebugLogs;

    [Header("シーン間インベントリ診断")]
    [Tooltip("オンなら、保存・復元・シーン移動に関する重要なログを常にConsoleへ表示します。")]
    [SerializeField] private bool alwaysLogSessionTransfer = true;

    [Tooltip("オンなら、保存・復元したアイテム1件ごとの詳細をConsoleへ表示します。")]
    [SerializeField] private bool logEachInventoryItem = true;

    public static GameSessionManager Instance { get; private set; }

    /// <summary>現在の所持金です。負の値にはなりません。</summary>
    public int CurrentMoney => currentMoney;

    /// <summary>所持金の初期化が済んでいるかどうかです。</summary>
    public bool HasInitializedMoney => hasInitializedMoney;

    /// <summary>インベントリ・装備の引き継ぎデータがあるかどうかです。</summary>
    public bool HasInventorySessionData => hasInventorySessionData;

    /// <summary>所持金が変化した時に、現在の所持金を通知します。</summary>
    public event Action<int> MoneyChanged;

    /// <summary>インベントリ・装備の引き継ぎデータを保存または復元した時に通知します。</summary>
    public event Action InventorySessionChanged;

    private int currentMoney;
    private bool hasInitializedMoney;

    private PlayerInventorySessionData inventorySessionData =
        new PlayerInventorySessionData();

    private bool hasInventorySessionData;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LogTransfer(
            $"Awake: GameSessionManagerを作成しました。開始Scene={SceneManager.GetActiveScene().name}"
        );
    }

    private void Start()
    {
        // PlayerMoneyController が同じ開始シーンにある場合は、
        // そちらの Initial Money を優先して引き継げるように
        // Start 時点まで初期化を待ちます。
        EnsureMoneyInitialized();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    // ---------------------------------------------------------------------
    // 所持金
    // ---------------------------------------------------------------------

    /// <summary>
    /// 初期所持金がまだ未確定の時だけ設定します。
    /// PlayerMoneyController の旧 Starting Money を引き継ぐために使います。
    /// </summary>
    public bool TrySetInitialMoney(int amount)
    {
        if (hasInitializedMoney)
        {
            return false;
        }

        currentMoney = Mathf.Max(0, amount);
        hasInitializedMoney = true;
        NotifyMoneyChanged();

        Log($"初期所持金を {currentMoney:N0} に設定しました。");
        return true;
    }

    /// <summary>
    /// 初期化されていなければ、Default Starting Money を使って初期化します。
    /// </summary>
    public void EnsureMoneyInitialized()
    {
        if (hasInitializedMoney)
        {
            return;
        }

        TrySetInitialMoney(defaultStartingMoney);
    }

    /// <summary>指定金額を所持金へ加算します。</summary>
    public bool AddMoney(int amount)
    {
        EnsureMoneyInitialized();

        if (amount <= 0)
        {
            return false;
        }

        long nextMoney = (long)currentMoney + amount;

        SetMoneyInternal(
            nextMoney > int.MaxValue
                ? int.MaxValue
                : (int)nextMoney
        );

        Log($"所持金を {amount:N0} 増やしました。現在 {currentMoney:N0}");
        return true;
    }

    /// <summary>
    /// 指定金額を支払います。所持金が足りない場合は減らしません。
    /// </summary>
    public bool TrySpendMoney(int amount)
    {
        EnsureMoneyInitialized();

        if (amount < 0)
        {
            return false;
        }

        if (amount == 0)
        {
            return true;
        }

        if (currentMoney < amount)
        {
            Log($"所持金が不足しています。必要 {amount:N0} / 現在 {currentMoney:N0}");
            return false;
        }

        SetMoneyInternal(currentMoney - amount);

        Log($"{amount:N0} 支払いました。現在 {currentMoney:N0}");
        return true;
    }

    /// <summary>指定金額を所持しているか確認します。</summary>
    public bool CanAfford(int amount)
    {
        EnsureMoneyInitialized();
        return amount >= 0 && currentMoney >= amount;
    }

    /// <summary>所持金を直接設定します。ロード・デバッグ用です。</summary>
    public void SetMoney(int amount)
    {
        EnsureMoneyInitialized();
        SetMoneyInternal(Mathf.Max(0, amount));
    }

    // ---------------------------------------------------------------------
    // インベントリ・装備のシーン間引き継ぎ
    // ---------------------------------------------------------------------

    /// <summary>
    /// 現在のインベントリと装備中アイテムを、シーン移動用データとして保存します。
    /// ItemDataへの参照、マス位置、回転、個数、武器の保存残弾を保持します。
    /// </summary>
    public bool CapturePlayerInventory(
        InventoryController inventoryController,
        EquipmentController equipmentController)
    {
        string sceneName = SceneManager.GetActiveScene().name;

        if (inventoryController == null || inventoryController.Grid == null)
        {
            LogTransferWarning(
                $"保存失敗: Scene={sceneName}。InventoryControllerまたはGridが見つかりません。" +
                " PlayerInventorySessionBridgeの参照を確認してください。"
            );
            return false;
        }

        InventoryGrid sourceGrid = inventoryController.Grid;

        LogTransfer(
            $"保存開始: Scene={sceneName} / Object={inventoryController.gameObject.name} / " +
            $"Grid={sourceGrid.Width}x{sourceGrid.Height} / 通常アイテム={sourceGrid.Items.Count}件 / " +
            $"EquipmentController={(equipmentController != null ? equipmentController.gameObject.name : "未設定")}"
        );

        PlayerInventorySessionData nextData =
            new PlayerInventorySessionData
            {
                GridWidth = Mathf.Max(1, sourceGrid.Width),
                GridHeight = Mathf.Max(1, sourceGrid.Height)
            };

        int skippedCount = 0;

        foreach (InventoryItem item in sourceGrid.Items)
        {
            SessionInventoryItemData itemData = CreateItemSnapshot(item);

            if (itemData != null)
            {
                nextData.InventoryItems.Add(itemData);
                LogSessionItem("保存", itemData, "通常インベントリ");
            }
            else
            {
                skippedCount++;
                LogTransferWarning(
                    "保存時に無効なInventoryItemを1件スキップしました。" +
                    " ItemData未設定またはAmountが0以下の可能性があります。"
                );
            }
        }

        if (equipmentController != null)
        {
            nextData.PrimaryWeaponItem = CreateItemSnapshot(
                equipmentController.PrimaryWeaponItem
            );

            nextData.HelmetItem = CreateItemSnapshot(
                equipmentController.HelmetItem
            );

            if (nextData.PrimaryWeaponItem != null)
            {
                LogSessionItem("保存", nextData.PrimaryWeaponItem, "装備: PrimaryWeapon");
            }

            if (nextData.HelmetItem != null)
            {
                LogSessionItem("保存", nextData.HelmetItem, "装備: Helmet");
            }
        }
        else
        {
            LogTransferWarning(
                "保存時にEquipmentControllerが未設定です。装備中アイテムは保存されません。"
            );
        }

        inventorySessionData = nextData;
        hasInventorySessionData = true;

        InventorySessionChanged?.Invoke();

        LogTransfer(
            $"保存完了: 通常={nextData.InventoryItems.Count}件 / " +
            $"武器={(nextData.PrimaryWeaponItem != null ? "あり" : "なし")} / " +
            $"ヘルメット={(nextData.HelmetItem != null ? "あり" : "なし")} / " +
            $"スキップ={skippedCount}件"
        );

        return true;
    }

    /// <summary>
    /// 保存済みのインベントリと装備を、現在シーンのControllerへ復元します。
    /// 復元先のInventoryControllerに入っていた開始アイテムは置き換えられます。
    /// </summary>
    public bool RestorePlayerInventory(
        InventoryController inventoryController,
        EquipmentController equipmentController,
        out string resultMessage)
    {
        resultMessage = string.Empty;
        string sceneName = SceneManager.GetActiveScene().name;

        if (!hasInventorySessionData || inventorySessionData == null)
        {
            resultMessage = "引き継ぎ用のインベントリデータはまだありません。";
            LogTransferWarning(
                $"復元しません: Scene={sceneName}。{resultMessage} " +
                "移動元SceneにPlayerInventorySessionBridgeが付いているか、" +
                "SceneTransitionButtonのCapture Inventory Before Loadがオンか確認してください。"
            );
            return false;
        }

        if (inventoryController == null || inventoryController.Grid == null)
        {
            resultMessage = "復元先のInventoryControllerが見つかりません。";
            LogTransferWarning(
                $"復元失敗: Scene={sceneName}。{resultMessage} " +
                "TownPlayerInventoryにInventoryControllerとPlayerInventorySessionBridgeが付いているか確認してください。"
            );
            return false;
        }

        LogTransfer(
            $"復元開始: Scene={sceneName} / Object={inventoryController.gameObject.name} / " +
            $"保存Grid={inventorySessionData.GridWidth}x{inventorySessionData.GridHeight} / " +
            $"保存通常アイテム={inventorySessionData.InventoryItems.Count}件 / " +
            $"EquipmentController={(equipmentController != null ? equipmentController.gameObject.name : "未設定")}"
        );

        ClearCurrentInventoryAndEquipment(
            inventoryController,
            equipmentController
        );

        inventoryController.Grid.Initialize(
            Mathf.Max(1, inventorySessionData.GridWidth),
            Mathf.Max(1, inventorySessionData.GridHeight),
            true
        );

        int restoredCount = 0;
        int fallbackPlacedCount = 0;
        int failedCount = 0;

        foreach (SessionInventoryItemData savedItem in
                 inventorySessionData.InventoryItems)
        {
            RestorePlacementResult result = RestoreNormalInventoryItem(
                inventoryController,
                savedItem
            );

            switch (result)
            {
                case RestorePlacementResult.Exact:
                    restoredCount++;
                    break;

                case RestorePlacementResult.Fallback:
                    restoredCount++;
                    fallbackPlacedCount++;
                    break;

                default:
                    failedCount++;
                    break;
            }
        }

        RestoreEquipmentItem(
            inventoryController,
            equipmentController,
            EquipmentSlotType.PrimaryWeapon,
            inventorySessionData.PrimaryWeaponItem,
            ref restoredCount,
            ref fallbackPlacedCount,
            ref failedCount
        );

        RestoreEquipmentItem(
            inventoryController,
            equipmentController,
            EquipmentSlotType.Helmet,
            inventorySessionData.HelmetItem,
            ref restoredCount,
            ref fallbackPlacedCount,
            ref failedCount
        );

        InventorySessionChanged?.Invoke();

        resultMessage = failedCount == 0
            ? $"インベントリを復元しました。{restoredCount}件"
            : $"インベントリを復元しましたが、{failedCount}件を配置できませんでした。";

        if (fallbackPlacedCount > 0)
        {
            resultMessage +=
                $" {fallbackPlacedCount}件は空いている別のマスへ移動しました。";
        }

        int finalNormalCount = inventoryController.Grid.Items.Count;
        resultMessage += $" 復元先通常={finalNormalCount}件。";

        if (failedCount > 0)
        {
            LogTransferWarning(resultMessage);
        }
        else
        {
            LogTransfer(resultMessage);
        }

        return failedCount == 0;
    }

    /// <summary>
    /// シーン間引き継ぎ用のインベントリデータを消去します。
    /// デバッグや「最初から」に使います。
    /// </summary>
    public void ClearInventorySessionData()
    {
        inventorySessionData = new PlayerInventorySessionData();
        hasInventorySessionData = false;
        InventorySessionChanged?.Invoke();

        Log("インベントリ引き継ぎデータを消去しました。");
    }

    [ContextMenu("Add 100 Money")]
    private void Add100MoneyFromContextMenu()
    {
        AddMoney(100);
    }

    [ContextMenu("Reset Money To Default Starting Amount")]
    private void ResetMoneyToDefaultStartingAmount()
    {
        currentMoney = Mathf.Max(0, defaultStartingMoney);
        hasInitializedMoney = true;
        NotifyMoneyChanged();
    }

    [ContextMenu("Clear Inventory Session Data")]
    private void ClearInventorySessionDataFromContextMenu()
    {
        ClearInventorySessionData();
    }

    [ContextMenu("Log Inventory Session Report")]
    private void LogInventorySessionReportFromContextMenu()
    {
        LogTransfer(GetInventorySessionSummary());
    }

    /// <summary>
    /// 現在保存されている引き継ぎデータを、Console確認用の文字列として返します。
    /// </summary>
    public string GetInventorySessionSummary()
    {
        if (!hasInventorySessionData || inventorySessionData == null)
        {
            return "[InventorySession] 保存データなし";
        }

        return "[InventorySession] 保存データあり / " +
               $"Grid={inventorySessionData.GridWidth}x{inventorySessionData.GridHeight} / " +
               $"通常={inventorySessionData.InventoryItems.Count}件 / " +
               $"武器={(inventorySessionData.PrimaryWeaponItem != null ? DescribeItem(inventorySessionData.PrimaryWeaponItem) : "なし")} / " +
               $"ヘルメット={(inventorySessionData.HelmetItem != null ? DescribeItem(inventorySessionData.HelmetItem) : "なし")}";
    }

    private SessionInventoryItemData CreateItemSnapshot(
        InventoryItem item)
    {
        if (item == null || item.ItemData == null || item.Amount <= 0)
        {
            return null;
        }

        return new SessionInventoryItemData
        {
            ItemData = item.ItemData,
            GridX = item.GridX,
            GridY = item.GridY,
            IsRotated = item.IsRotated,
            Amount = Mathf.Clamp(item.Amount, 1, item.ItemData.MaxStack),
            HasStoredMagazineAmmo = item.HasStoredMagazineAmmo,
            StoredMagazineAmmo = Mathf.Max(0, item.StoredMagazineAmmo)
        };
    }

    private void ClearCurrentInventoryAndEquipment(
        InventoryController inventoryController,
        EquipmentController equipmentController)
    {
        if (equipmentController != null)
        {
            equipmentController.ClearAllEquippedItems();
        }

        List<InventoryItem> currentItems =
            new List<InventoryItem>(inventoryController.Grid.Items);

        foreach (InventoryItem item in currentItems)
        {
            inventoryController.RemoveItem(item);
        }
    }

    private RestorePlacementResult RestoreNormalInventoryItem(
        InventoryController inventoryController,
        SessionInventoryItemData savedItem)
    {
        InventoryItem item = CreateRuntimeInventoryItem(savedItem);

        if (item == null)
        {
            LogTransferWarning("通常アイテム復元失敗: 保存ItemDataがnullです。");
            return RestorePlacementResult.Failed;
        }

        if (inventoryController.TryMoveItem(
                item,
                savedItem.GridX,
                savedItem.GridY,
                savedItem.IsRotated))
        {
            LogSessionItem("復元", savedItem, "通常インベントリ: 元位置へ配置成功");
            return RestorePlacementResult.Exact;
        }

        LogTransferWarning(
            $"通常アイテムの元位置復元に失敗しました。{DescribeItem(savedItem)} / " +
            $"保存位置=({savedItem.GridX},{savedItem.GridY}) / 回転={savedItem.IsRotated}。空きマスを探します。"
        );

        if (TryPlaceAtFirstAvailableSpace(
                inventoryController,
                item,
                savedItem.IsRotated))
        {
            LogSessionItem("復元", savedItem, "通常インベントリ: 空きマスへ代替配置成功");
            return RestorePlacementResult.Fallback;
        }

        LogTransferWarning(
            $"通常アイテム復元失敗: 空きマスにも置けませんでした。{DescribeItem(savedItem)}"
        );
        return RestorePlacementResult.Failed;
    }

    private void RestoreEquipmentItem(
        InventoryController inventoryController,
        EquipmentController equipmentController,
        EquipmentSlotType slotType,
        SessionInventoryItemData savedItem,
        ref int restoredCount,
        ref int fallbackPlacedCount,
        ref int failedCount)
    {
        if (savedItem == null)
        {
            LogTransfer($"装備復元: {slotType}=保存データなし");
            return;
        }

        InventoryItem item = CreateRuntimeInventoryItem(savedItem);

        if (item == null)
        {
            failedCount++;
            LogTransferWarning($"装備復元失敗: {slotType}のItemDataがnullです。");
            return;
        }

        // equipmentController がnullの場合でも、下の失敗ログで
        // 結果を安全に参照できるよう、先に初期値を入れておきます。
        EquipmentResult equipmentResult = EquipmentResult.InvalidItem;

        bool restoredToEquipment =
            equipmentController != null &&
            equipmentController.RestoreEquippedItem(
                slotType,
                item,
                out equipmentResult);

        if (restoredToEquipment)
        {
            restoredCount++;
            LogSessionItem("復元", savedItem, $"装備: {slotType}へ配置成功");
            return;
        }

        string failure = equipmentController == null
            ? "EquipmentControllerが未設定"
            : $"RestoreEquippedItem失敗({equipmentResult})";

        LogTransferWarning(
            $"装備復元の直接配置に失敗しました。{slotType} / {DescribeItem(savedItem)} / {failure}。" +
            "通常インベントリへ退避を試みます。"
        );

        if (TryPlaceAtFirstAvailableSpace(
                inventoryController,
                item,
                savedItem.IsRotated))
        {
            restoredCount++;
            fallbackPlacedCount++;

            LogSessionItem("復元", savedItem, $"{slotType}: 通常インベントリへ退避成功");
            return;
        }

        failedCount++;
        LogTransferWarning(
            $"装備復元失敗: {slotType}を通常インベントリへも置けませんでした。{DescribeItem(savedItem)}"
        );
    }

    private InventoryItem CreateRuntimeInventoryItem(
        SessionInventoryItemData savedItem)
    {
        if (savedItem == null || savedItem.ItemData == null)
        {
            return null;
        }

        InventoryItem item = new InventoryItem(
            savedItem.ItemData,
            savedItem.GridX,
            savedItem.GridY,
            Mathf.Clamp(
                savedItem.Amount,
                1,
                savedItem.ItemData.MaxStack
            )
        );

        if (savedItem.IsRotated && item.CanRotate)
        {
            item.TryRotate();
        }

        if (savedItem.HasStoredMagazineAmmo)
        {
            item.SetStoredMagazineAmmo(
                savedItem.StoredMagazineAmmo
            );
        }

        return item;
    }

    private bool TryPlaceAtFirstAvailableSpace(
        InventoryController inventoryController,
        InventoryItem item,
        bool preferredRotation)
    {
        if (inventoryController == null ||
            inventoryController.Grid == null ||
            item == null)
        {
            return false;
        }

        if (TryPlaceWithRotation(
                inventoryController,
                item,
                preferredRotation))
        {
            return true;
        }

        if (item.CanRotate &&
            TryPlaceWithRotation(
                inventoryController,
                item,
                !preferredRotation))
        {
            return true;
        }

        return false;
    }

    private bool TryPlaceWithRotation(
        InventoryController inventoryController,
        InventoryItem item,
        bool isRotated)
    {
        InventoryGrid grid = inventoryController.Grid;

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (!grid.CanPlaceItem(item, x, y, isRotated))
                {
                    continue;
                }

                return inventoryController.TryMoveItem(
                    item,
                    x,
                    y,
                    isRotated
                );
            }
        }

        return false;
    }

    private void LogSessionItem(
        string phase,
        SessionInventoryItemData item,
        string location)
    {
        if (!logEachInventoryItem || item == null)
        {
            return;
        }

        LogTransfer($"{phase}アイテム: {location} / {DescribeItem(item)}");
    }

    private static string DescribeItem(SessionInventoryItemData item)
    {
        if (item == null)
        {
            return "Item=null";
        }

        string itemName = item.ItemData != null
            ? item.ItemData.DisplayName
            : "ItemData=null";

        string itemId = item.ItemData != null
            ? item.ItemData.ItemId
            : "null";

        string magazine = item.HasStoredMagazineAmmo
            ? $" / 残弾={item.StoredMagazineAmmo}"
            : string.Empty;

        return $"{itemName}(ID={itemId}, 数={item.Amount}, 座標={item.GridX},{item.GridY}, 回転={item.IsRotated}{magazine})";
    }

    private void SetMoneyInternal(int value)
    {
        int safeValue = Mathf.Max(0, value);

        if (currentMoney == safeValue)
        {
            return;
        }

        currentMoney = safeValue;
        NotifyMoneyChanged();
    }

    private void NotifyMoneyChanged()
    {
        MoneyChanged?.Invoke(currentMoney);
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[GameSessionManager] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.LogWarning($"[GameSessionManager] {message}", this);
    }

    private void LogTransfer(string message)
    {
        if (!alwaysLogSessionTransfer)
        {
            return;
        }

        Debug.Log($"[InventorySession] {message}", this);
    }

    private void LogTransferWarning(string message)
    {
        if (!alwaysLogSessionTransfer)
        {
            return;
        }

        Debug.LogWarning($"[InventorySession] {message}", this);
    }

    private void OnValidate()
    {
        defaultStartingMoney = Mathf.Max(0, defaultStartingMoney);
    }

    private enum RestorePlacementResult
    {
        Failed,
        Exact,
        Fallback
    }
}

/// <summary>
/// シーン間で保持するインベントリ1件分のデータです。
/// ItemDataはScriptableObject参照なので、同一プレイ中は安全に復元できます。
/// </summary>
[Serializable]
public class SessionInventoryItemData
{
    public ItemData ItemData;
    public int GridX;
    public int GridY;
    public bool IsRotated;
    public int Amount;
    public bool HasStoredMagazineAmmo;
    public int StoredMagazineAmmo;
}

/// <summary>
/// プレイヤーの通常インベントリと装備枠をまとめた、シーン間引き継ぎデータです。
/// </summary>
[Serializable]
public class PlayerInventorySessionData
{
    public int GridWidth = 7;
    public int GridHeight = 10;
    public List<SessionInventoryItemData> InventoryItems =
        new List<SessionInventoryItemData>();
    public SessionInventoryItemData PrimaryWeaponItem;
    public SessionInventoryItemData HelmetItem;
}
