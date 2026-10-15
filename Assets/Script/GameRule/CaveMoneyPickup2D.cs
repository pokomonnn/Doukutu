using UnityEngine;

/// <summary>
/// 洞窟内のお金回収オブジェクト。
/// Eキーで、プレイヤーの通常インベントリへ設定したItemDataを追加します。
/// 全部収納できなかった場合は残額がその場に残ります。
/// 接近中の案内と、取得／失敗メッセージはInspectorで自由に変更できます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CircleCollider2D))]
public sealed class CaveMoneyPickup2D : CavePickupInteractable2D
{
    [Header("お金アイテム（町の換金対象と同じItemData）")]
    [SerializeField] private ItemData moneyItem;

    [Tooltip("このオブジェクトに置かれているお金の個数です。1個=1G設定ならそのまま金額です。")]
    [SerializeField, Min(1)] private int amount = 100;

    [Header("画面左上イベントログ")]
    [Tooltip("お金アイテム1個を何Gと表記するか。所持金を直接増やす設定ではありません。")]
    [SerializeField, Min(1)] private int goldPerMoneyItem = 1;

    [Header("Inspectorで設定する表示メッセージ")]
    [Tooltip("近づいたときの案内です。前に[E]が自動で付きます。{name}=アイテム名、{amount}=残っている個数、{remaining}=残っている個数。")]
    [SerializeField, TextArea(1, 3)]
    private string nearbyPromptMessage = "お金を回収 ({amount}個)";

    [Tooltip("回収成功時の通知です。{name}=アイテム名、{amount}=今回拾った個数、{remaining}=地面に残った個数。")]
    [SerializeField, TextArea(1, 3)]
    private string collectedMessage = "{name} ×{amount} を回収";

    [Tooltip("バッグが満杯で1個も拾えなかった時の通知です。")]
    [SerializeField, TextArea(1, 3)]
    private string inventoryFullMessage = "バッグに空きがないため、お金を拾えません";

    [Tooltip("PlayerのInventoryControllerが見つからない場合の通知です。")]
    [SerializeField, TextArea(1, 3)]
    private string inventoryMissingMessage = "回収できません：プレイヤーのInventoryが見つかりません";

    [Header("Player Inventory参照（間違ったInventoryへ入るのを防ぐ）")]
    [Tooltip("通常はPlayerのInventoryControllerを指定してください。設定時は最優先で使用します。")]
    [SerializeField] private InventoryController playerInventoryOverride;

    [Tooltip("バッグ画面のInventoryGridUIを設定すると、そのUIが表示しているInventoryを使用します。Player Inventoryが未設定の場合の優先候補です。")]
    [SerializeField] private InventoryGridUI playerInventoryGridUI;

    [Header("回収診断")]
    [Tooltip("ONなら実際の追加先Inventory・追加数・格納後の数をConsoleに表示します。")]
    [SerializeField] private bool showPickupDiagnostics = true;

    private int remainingAmount;

    public int RemainingAmount => remainingAmount;

    protected override void Awake()
    {
        remainingAmount = Mathf.Max(1, amount);
        base.Awake();
    }

    protected override bool IsReady()
    {
        return moneyItem != null && remainingAmount > 0;
    }

    protected override string GetPromptLabel()
    {
        return FormatMessage(nearbyPromptMessage, remainingAmount);
    }

    protected override void Collect(Transform player)
    {
        if (moneyItem == null || remainingAmount <= 0 || player == null)
        {
            return;
        }

        // 参照を明示した場合はそのInventoryへ追加します。
        // 無指定なら取得したPlayerに属するものだけを探します。
        InventoryController inventory = ResolvePlayerInventory(player);

        if (inventory == null)
        {
            Debug.LogWarning(
                "[CaveMoneyPickup2D] PlayerのInventoryControllerが見つかりません。",
                this);
            ShowMessage(FormatMessage(inventoryMissingMessage, remainingAmount));
            return;
        }

        int before = remainingAmount;
        int storedBefore = CountMoneyInInventory(inventory);
        bool acceptedAll = inventory.TryAddItem(moneyItem, before, out int notAdded);
        notAdded = Mathf.Clamp(notAdded, 0, before);
        int accepted = before - notAdded;
        int storedAfter = CountMoneyInInventory(inventory);

        if (showPickupDiagnostics)
        {
            Debug.Log(
                $"[CaveMoneyPickup2D] 回収診断: " +
                $"Item={moneyItem.DisplayName} (ID={moneyItem.ItemId}) / " +
                $"対象Inventory={GetHierarchyPath(inventory.transform)} " +
                $"(instance={inventory.GetInstanceID()}) / " +
                $"要求={before}, 追加={accepted}, 残り={notAdded}, 全部成功={acceptedAll} / " +
                $"バッグ内同Item数={storedBefore}→{storedAfter}",
                this);
            WarnIfUiUsesDifferentInventory(inventory);
        }

        if (accepted <= 0)
        {
            ShowMessage(FormatMessage(inventoryFullMessage, 0));
            return;
        }

        remainingAmount = notAdded;
        PlayPickupSound();
        ShowMessage(FormatMessage(collectedMessage, accepted));

        // 正しくInventoryへ収納できた分だけ通知（満杯で残った分は含めない）。
        long gold = (long)accepted * Mathf.Max(1, goldPerMoneyItem);
        GameEventLogManager2D.MoneyGained((int)System.Math.Min(gold, int.MaxValue));

        if (remainingAmount <= 0)
        {
            FinishCollection();
        }
    }

    private InventoryController ResolvePlayerInventory(Transform player)
    {
        if (playerInventoryOverride != null)
        {
            return playerInventoryOverride;
        }

        if (playerInventoryGridUI != null && playerInventoryGridUI.Controller != null)
        {
            return playerInventoryGridUI.Controller;
        }

        InventoryController controller = player.GetComponent<InventoryController>();
        if (controller == null)
        {
            controller = player.GetComponentInChildren<InventoryController>(true);
        }
        if (controller == null)
        {
            controller = player.GetComponentInParent<InventoryController>();
        }
        if (controller != null)
        {
            return controller;
        }

        // InventoryControllerがPlayer直下にない構成では、
        // 同じシーンにあるPlayer用SessionBridgeを利用する。
        PlayerInventorySessionBridge[] bridges = FindObjectsByType<PlayerInventorySessionBridge>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        InventoryController unique = null;
        foreach (PlayerInventorySessionBridge bridge in bridges)
        {
            if (bridge == null || bridge.gameObject.scene != gameObject.scene ||
                bridge.InventoryController == null)
            {
                continue;
            }

            if (unique != null && unique != bridge.InventoryController)
            {
                // 同じシーンに候補が複数ある場合は誤追加を防ぐ。
                Debug.LogWarning(
                    "[CaveMoneyPickup2D] Player Inventory候補が複数あります。" +
                    "InspectorのPlayer Inventoryに正しいInventoryControllerを指定してください。",
                    this);
                return null;
            }

            unique = bridge.InventoryController;
        }
        return unique;
    }

    private int CountMoneyInInventory(InventoryController inventory)
    {
        if (inventory == null || inventory.Grid == null || inventory.Grid.Items == null)
        {
            return 0;
        }

        int count = 0;
        foreach (InventoryItem item in inventory.Grid.Items)
        {
            if (item != null && item.ItemData == moneyItem && item.Amount > 0)
            {
                count += item.Amount;
            }
        }
        return count;
    }

    private void WarnIfUiUsesDifferentInventory(InventoryController writtenInventory)
    {
        if (playerInventoryGridUI != null && playerInventoryGridUI.Controller != null &&
            playerInventoryGridUI.Controller != writtenInventory)
        {
            Debug.LogWarning(
                "[CaveMoneyPickup2D] 指定したPlayer InventoryとPlayer Inventory Grid UIが" +
                "別のInventoryControllerを参照しています。表示されない原因になります。",
                this);
        }

        InventoryGridUI[] gridUis = FindObjectsByType<InventoryGridUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (InventoryGridUI ui in gridUis)
        {
            if (ui == null || ui.gameObject.scene != gameObject.scene ||
                !ui.IsPlayerInventory || ui.Controller == writtenInventory)
            {
                continue;
            }

            Debug.LogWarning(
                $"[CaveMoneyPickup2D] Player用GridUI『{GetHierarchyPath(ui.transform)}』は" +
                $"別のInventoryController『{GetHierarchyPath(ui.Controller.transform)}』を表示しています。" +
                "実際の取得先と一致しているか確認してください。",
                this);
        }
    }

    private static string GetHierarchyPath(Transform value)
    {
        if (value == null) return "null";
        string result = value.name;
        for (Transform parent = value.parent; parent != null; parent = parent.parent)
        {
            result = parent.name + "/" + result;
        }
        return result;
    }

    private string FormatMessage(string template, int amountValue)
    {
        if (string.IsNullOrEmpty(template))
        {
            return string.Empty;
        }

        string itemName = moneyItem != null
            ? moneyItem.DisplayName
            : "お金";

        // string.Formatを使わずトークン置換することで、
        // Inspectorで入力した括弧・TMPリッチテキスト等がエラーにならない。
        return template
            .Replace("{name}", itemName)
            .Replace("{amount}", amountValue.ToString("N0"))
            .Replace("{remaining}", remainingAmount.ToString("N0"));
    }

    protected override void OnValidate()
    {
        base.OnValidate();
        amount = Mathf.Max(1, amount);
        goldPerMoneyItem = Mathf.Max(1, goldPerMoneyItem);
    }
}
