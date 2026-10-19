using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// STEP4 v1.3.5: 洞窟のPlayer本体にInventoryControllerを付けず、
/// 既存のTabインベントリUIが表示する実データを共通売買UIへ渡します。
/// 新しいInventoryControllerは絶対に作成しません。
/// </summary>
public static class SharedTradePlayerInventoryResolver2D
{
    public static bool TryResolve(
        Component requester,
        InventoryGridUI preferredTabInventoryGrid,
        InventoryController explicitlyAssignedInventory,
        out InventoryController result,
        out string diagnostic)
    {
        result = null;
        diagnostic = string.Empty;

        if (Valid(preferredTabInventoryGrid != null ? preferredTabInventoryGrid.Controller : null))
        {
            result = preferredTabInventoryGrid.Controller;
            diagnostic = "明示指定した通常インベントリGridUI: " + result.name;
            return true;
        }

        // 必ずゲーム内で使われている通常InventoryGridUIを優先します。
        // 共通売買PrefabのPlayer用Grid（起動前は空）を参照してはいけません。
        var grids = Object.FindObjectsByType<InventoryGridUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        var scores = new Dictionary<InventoryController, int>();
        Scene scene = requester.gameObject.scene;
        foreach (InventoryGridUI ui in grids)
        {
            if (ui == null || ui.gameObject.scene != scene) continue;
            if (ui.GetComponentInParent<SharedTradePrefabAnchor2D>(true) != null) continue;
            if (!Valid(ui.Controller) || ui.ItemBox != null) continue;
            if (IsTradeOrMerchantUI(ui.transform)) continue;

            InventoryController candidate = ui.Controller;
            int score = 100;
            if (ui.gameObject.activeInHierarchy) score += 10;
            if (candidate.gameObject.activeInHierarchy) score += 5;
            if (HasPlayerParent(candidate.transform)) score += 35;
            if (HasPlayerParent(ui.transform)) score += 15;
            if (candidate.Grid.Items.Count > 0) score += 25;
            if (explicitlyAssignedInventory == candidate) score += 10;
            if (IsMainInventoryUI(ui.transform)) score += 25;
            if (!scores.TryGetValue(candidate, out int old) || old < score)
                scores[candidate] = score;
        }

        int highest = int.MinValue;
        bool tied = false;
        foreach (KeyValuePair<InventoryController, int> pair in scores)
        {
            if (pair.Value > highest)
            {
                highest = pair.Value;
                result = pair.Key;
                tied = false;
            }
            else if (pair.Value == highest && result != pair.Key)
            {
                tied = true;
            }
        }
        if (result != null && !tied)
        {
            diagnostic = "通常インベントリGridUIから自動検出: " + result.name +
                         " / 所持スタック=" + result.Grid.Items.Count;
            return true;
        }
        if (tied)
        {
            result = null;
            diagnostic = "通常インベントリUIの候補が複数あります。" +
                "商人NPCの『通常インベントリのGrid UI』へ、Tabで使うInventoryGridUIを設定してください。";
            return false;
        }

        // Tab側GridUIが見つからない特殊な構成への後方互換。
        if (Valid(explicitlyAssignedInventory))
        {
            result = explicitlyAssignedInventory;
            diagnostic = "NPCに手動設定されたInventoryController: " + result.name;
            return true;
        }
        GameObject player = FindPlayerObject();
        if (player != null)
        {
            PlayerInventorySessionBridge sessionBridge =
                player.GetComponent<PlayerInventorySessionBridge>() ??
                player.GetComponentInChildren<PlayerInventorySessionBridge>(true);
            if (sessionBridge != null && Valid(sessionBridge.InventoryController))
            {
                result = sessionBridge.InventoryController;
                diagnostic = "PlayerInventorySessionBridge: " + result.name;
                return true;
            }
            InventoryController child = player.GetComponentInChildren<InventoryController>(true);
            if (Valid(child))
            {
                result = child;
                diagnostic = "Player階層内: " + result.name;
                return true;
            }
        }

        // シーン内に候補が1個しかない場合のみ採用。適当な空Inventoryを選ばない。
        InventoryController unique = null;
        foreach (InventoryController inv in Object.FindObjectsByType<InventoryController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (inv == null || inv.gameObject.scene != scene || !Valid(inv)) continue;
            if (unique != null && unique != inv)
            {
                diagnostic = "InventoryControllerが複数あり、どれがPlayer所持品か判別できません。" +
                    "NPCの『通常インベントリのGrid UI』にTab用Gridを指定してください。";
                return false;
            }
            unique = inv;
        }
        if (unique != null)
        {
            result = unique;
            diagnostic = "シーン内で唯一のInventoryController: " + result.name;
            return true;
        }

        diagnostic = "実際のPlayer所持品を管理しているInventoryControllerを取得できません。" +
            "Playerへ新規追加せず、NPCの『通常インベントリのGrid UI』へ" +
            "普段のインベントリのInventoryGridUIを割り当ててください。";
        return false;
    }

    public static Transform FindPlayerTransform()
    {
        GameObject player = FindPlayerObject();
        return player != null ? player.transform : null;
    }

    public static EquipmentController FindEquipment(InventoryController inventory,
        PlayerTradeInventoryBridge bridge)
    {
        if (bridge != null && bridge.InventoryController == inventory &&
            bridge.EquipmentController != null) return bridge.EquipmentController;

        GameObject player = FindPlayerObject();
        if (player != null)
        {
            PlayerInventorySessionBridge sessionBridge =
                player.GetComponent<PlayerInventorySessionBridge>() ??
                player.GetComponentInChildren<PlayerInventorySessionBridge>(true);
            if (sessionBridge != null && sessionBridge.EquipmentController != null)
                return sessionBridge.EquipmentController;
            EquipmentController equipment = player.GetComponent<EquipmentController>() ??
                player.GetComponentInChildren<EquipmentController>(true);
            if (equipment != null) return equipment;
        }
        if (inventory != null)
        {
            PlayerInventorySessionBridge sessionBridge =
                inventory.GetComponent<PlayerInventorySessionBridge>() ??
                inventory.GetComponentInParent<PlayerInventorySessionBridge>();
            if (sessionBridge != null && sessionBridge.EquipmentController != null)
                return sessionBridge.EquipmentController;
            return inventory.GetComponent<EquipmentController>() ??
                   inventory.GetComponentInParent<EquipmentController>();
        }
        return null;
    }

    private static bool Valid(InventoryController inv) =>
        inv != null && inv.Grid != null;

    private static GameObject FindPlayerObject()
    {
        // Tag未登録プロジェクトでも例外にせず、探索失敗として返す。
        try { return GameObject.FindGameObjectWithTag("Player"); }
        catch (UnityException) { return null; }
    }

    private static bool HasPlayerParent(Transform transform)
    {
        for (Transform t = transform; t != null; t = t.parent)
        {
            if (t.CompareTag("Player")) return true;
        }
        return false;
    }

    private static bool IsTradeOrMerchantUI(Transform transform)
    {
        for (Transform t = transform; t != null; t = t.parent)
        {
            string name = t.name.ToLowerInvariant();
            if (name.Contains("trade") || name.Contains("shop") ||
                name.Contains("merchant") || name.Contains("売買") ||
                name.Contains("商人") || name.Contains("店舗")) return true;
        }
        return false;
    }

    private static bool IsMainInventoryUI(Transform transform)
    {
        for (Transform t = transform; t != null; t = t.parent)
        {
            string n = t.name.ToLowerInvariant();
            if (n.Contains("inventory") || n.Contains("インベントリ") ||
                n.Contains("backpack")) return true;
        }
        return false;
    }
}
