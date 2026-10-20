using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーの所持品、NPCのShop、既存MissionManager2Dへ接続するSTEP4の実処理。
/// 命令は必ずユーザーが選択肢をクリックした時だけ実行する。
/// UnityEventの任意イベントIDとの後方互換性はPresenter側が管理。
/// </summary>
public static class DialogueActionRunner2D
{
    // Scene再読込までの簡易ガード。同じActor,イベントIDに対する成功は繰り返さない。
    // セーブデータへの保存は別途対応が必要（リロードで再度実行可能になる）。
    private static readonly HashSet<string> completed = new HashSet<string>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCache() => completed.Clear();

    public static bool TryExecute(DialogueAction2D action, DialogueActor2D actor, out string message, out bool closeDialogue)
    {
        message = string.Empty;
        closeDialogue = false;
        if (action == null || actor == null)
        {
            message = "会話アクションの設定がありません";
            return false;
        }

        // Story transactions use the saved ledger, not the per-actor runtime guard.
        if (action.kind == DialogueActionKind2D.StoryCampaign)
            return StoryCampaignRuntime.TryExecute(action.eventId, out message);
        string key = actor.GetInstanceID() + ":" + action.eventId;
        bool applyOnceGuard = action.oncePerRuntime && action.kind != DialogueActionKind2D.OpenShop;
        if (applyOnceGuard && completed.Contains(key))
        {
            message = "この操作はすでに実行しています";
            return false;
        }

        DialogueActionContext2D context = actor.GetComponent<DialogueActionContext2D>();
        try
        {
            bool success = false;
            switch (action.kind)
            {
                case DialogueActionKind2D.OpenShop:
                    DungeonNpcShop2D shop = context != null ? context.shop : null;
                    if (shop == null) shop = actor.GetComponent<DungeonNpcShop2D>();
                    if (shop == null) shop = actor.GetComponentInChildren<DungeonNpcShop2D>(true);
                    if (shop == null) { message = "このNPCにDungeonNpcShop2Dがありません"; return false; }
                    shop.OpenShop();
                    success = shop.IsOpen;
                    closeDialogue = success;
                    message = success ? "ショップを開きました" : "ショップを開けません（距離・商人設定・Sessionを確認）";
                    break;

                case DialogueActionKind2D.StartMission:
                case DialogueActionKind2D.DeliverMissionItems:
                    MissionManager2D mission = context != null ? context.missionManager : null;
                    if (mission == null) mission = UnityEngine.Object.FindFirstObjectByType<MissionManager2D>();
                    if (mission == null) { message = "MissionManager2Dがシーンにありません"; return false; }
                    if (action.missionIndex < 0 || mission.GetMissionDefinition(action.missionIndex) == null)
                    { message = "ミッションIndexが無効です"; return false; }
                    if (action.kind == DialogueActionKind2D.StartMission)
                    {
                        success = mission.StartMission(action.missionIndex);
                        message = success ? "ミッションを受注しました" : "ミッションを受注できません（状態・受注条件を確認）";
                    }
                    else
                    {
                        InventoryController explicitInventory = ResolveInventory(context);
                        if (explicitInventory != null)
                        {
                            success = mission.TryDeliverMission(action.missionIndex, explicitInventory, out _, out message);
                        }
                        else
                        {
                            success = mission.TryDeliverMission(action.missionIndex, out message);
                        }
                    }
                    break;

                case DialogueActionKind2D.GiveItemToPlayer:
                case DialogueActionKind2D.TakeItemFromPlayer:
                    InventoryController inventory = ResolveInventory(context);
                    if (inventory == null) { message = "PlayerのInventoryControllerが見つかりません"; return false; }
                    if (action.item == null || action.amount < 1) { message = "アイテムまたは数量が未設定です"; return false; }
                    int count = action.amount;
                    if (action.kind == DialogueActionKind2D.TakeItemFromPlayer)
                    {
                        if (inventory.GetTotalAmount(action.item) < count)
                        { message = "必要なアイテムが足りません"; return false; }
                        int removed = inventory.RemoveAmountByItemData(action.item, count);
                        success = removed == count;
                        // 事前判定後の予期しない部分削除には補償を試みる。
                        if (!success && removed > 0) inventory.TryAddItem(action.item, removed, out _);
                        message = success ? action.item.DisplayName + " ×" + count + " を渡しました" : "アイテムを取り出せませんでした";
                    }
                    else
                    {
                        // 既存APIが部分追加を行う場合があるので、失敗時は追加分を取り消す。
                        int before = inventory.GetTotalAmount(action.item);
                        bool allAdded = inventory.TryAddItem(action.item, count, out int remainder);
                        int added = Mathf.Max(0, inventory.GetTotalAmount(action.item) - before);
                        success = allAdded && remainder == 0;
                        if (!success && added > 0)
                        {
                            int rolledBack = inventory.RemoveAmountByItemData(action.item, added);
                            if (rolledBack != added)
                                Debug.LogError("[Dialogue STEP4] Item追加失敗後のロールバックに失敗", actor);
                        }
                        message = success ? action.item.DisplayName + " ×" + count + " を受け取りました" : "Inventoryの空きが不足しています";
                    }
                    break;
                default:
                    message = "未対応の会話アクションです";
                    return false;
            }
            if (success && applyOnceGuard) completed.Add(key);
            return success;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception, actor);
            message = "会話アクションでエラーが発生しました（Consoleを確認）";
            return false;
        }
    }

    private static InventoryController ResolveInventory(DialogueActionContext2D context)
    {
        if (context != null && context.playerInventory != null) return context.playerInventory;
        GameObject player = null;
        try { player = GameObject.FindGameObjectWithTag("Player"); }
        catch (UnityException) { }
        if (player == null) return null;
        InventoryController inventory = player.GetComponent<InventoryController>();
        if (inventory == null) inventory = player.GetComponentInChildren<InventoryController>(true);
        if (inventory == null) inventory = player.GetComponentInParent<InventoryController>();
        return inventory;
    }
}
