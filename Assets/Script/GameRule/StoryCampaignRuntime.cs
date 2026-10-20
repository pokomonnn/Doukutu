using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adapter for existing session, inventory, mission and conversation systems. No separate save file or controls.
public static class StoryCampaignRuntime
{
    public const string Prefix = "story/";
    public static StoryCampaignDatabase Database => Resources.Load<StoryCampaignDatabase>("StoryCampaign/Database");
    static GameSessionManager Session => GameSessionManager.Instance;
    public static bool Has(string flag) => Session != null && Session.HasStoryFlag(Prefix+flag);
    public static void Set(string flag) => Session.SetStoryFlag(Prefix+flag);
    static bool busy;
    // Match MissionSessionBridge: persistent town objects can coexist with dungeon managers.
    public static MissionManager2D FindMissionManager()
    {
        var scene=SceneManager.GetActiveScene();
        var managers=UnityEngine.Object.FindObjectsByType<MissionManager2D>(FindObjectsInactive.Exclude);
        return managers.FirstOrDefault(m=>m.gameObject.scene==scene) ?? managers.FirstOrDefault();
    }
    public static bool Ready(StoryCampaignStep s) => s != null && !Has(s.quest+"_Completed") &&
        (s.requires == null || s.requires.All(Has)) &&
        (s.blockedBy == null || !s.blockedBy.Any(Has)) && (!Has("step_"+s.id) || s.grantItem != null);
    public static bool GardenOpen => Has("S1M01_Completed") && Has("S2M01_Completed") && Has("S3M01_Completed");
    public static bool SupportsCastle => Has("faction_adventurer_support") && Has("faction_bandit_support") && Has("faction_dwarf_support");
    public static bool TryExecute(string command, out string message)
    {
        message="進行条件を満たしていません。";
        if (busy || Session == null || Database == null || string.IsNullOrEmpty(command)) return false;
        busy=true;
        try {
            if(command.StartsWith("accept/")) {
                string id=command.Substring(7);
                if(id=="S2S02" && !Has("PlayerBanditTruce")) return false;
                if(!Database.quests.Any(q=>q.id==id)) return false;
                int stage=id.Contains("M01")?(id=="S4M01"?4:0):id[1]-'0';
                if(SceneManager.GetActiveScene().name!=Database.stageScenes[stage])return false;
                Begin(id); message="依頼を記録しました。"; return true;
            }
            if(command=="butler") {Set("ButlerAppeared");message="";return true;}
            if(command.StartsWith("ending/")) {
                string ending=command.Substring(7);
                if(SceneManager.GetActiveScene().name!=Database.stageScenes[4] ||
                    !Has("S4M01_StorySeen") || Has("EndingCommitted") ||
                    (ending!="A" && ending!="B" && ending!="C") || (ending=="C" && !SupportsCastle)) return false;
                Set("EndingCommitted");Set("EndingType_"+ending);Set("S4M01_Completed");
                if(ending=="B") Set("NobleReturnedToTown");
                if(ending=="C") Set("CastleRebuildPlanned");
                Set("RewardDelivered");Session.AddMoney(Database.endingReward);Begin("S4M01");Sync();
                message="結末を確定しました。";return true;
            }
            if(!command.StartsWith("step/")) return false;
            var step=Database.steps.FirstOrDefault(s=>s.id==command.Substring(5));
            if(!Ready(step) || SceneManager.GetActiveScene().name!=Database.stageScenes[step.stage]) return false;
            // Finn can only be rescued by the existing extraction collector, never a dialogue choice.
            if(step.id=="rescue") return false;
            if(step.id=="reportFinn") {
                var mission=Database.quests.First(q=>q.id=="S1S01").mission;
                var manager=StoryCampaignRuntime.FindMissionManager();
                if(manager==null)return false;
                manager.RefreshAllMissionProgress();Session.CaptureMissionsFromManager(manager);
                if(!Session.CanClaimMissionReward(mission,true,out message) ||
                   !Session.MarkMissionRewardClaimed(mission,out message))return false;
                Session.AddMoney(mission.StoryMoneyReward);
            }
            var inventory=PlayerInventory(out var inventoryBridge);
            var consume=step.consumeItems ?? Array.Empty<ItemData>();
            if(consume.Any(i=>i==null || inventory==null || inventory.GetTotalAmount(i)<1)) {
                message="必要なクエスト用品を所持品に入れてください。紛失した用品は調査地点で再取得できます。";return false;
            }
            if(step.grantItem!=null) {
                if(inventory==null) {message="インベントリが見つかりません。";return false;}
                if(inventory.GetTotalAmount(step.grantItem)>0) {message="この用品はすでに持っています。";return false;}
                if(!inventory.TryAddItem(step.grantItem,1)) {message="所持品が満杯です。空きを作ってから再度調べてください。";return false;}
            }
            foreach(var item in consume) inventory.RemoveAmountByItemData(item,1);
            if(step.grantItem!=null || consume.Length>0) inventoryBridge?.CaptureToSession();
            Set("step_"+step.id);
            foreach(string flag in step.flags ?? Array.Empty<string>()) Set(flag);
            if(step.reward>0 && step.id!="reportFinn") Session.AddMoney(step.reward);
            if (Has(step.quest+"_Started") || Has(step.quest+"_Completed")) Begin(step.quest);
            if(step.quest.Contains("M01") && step.stage==0) Set("MayorIntroduced");
            if(GardenOpen) Set("STAGE4_UNLOCKED");
            Sync();message="進行を記録しました。";return true;
        } finally {busy=false;}
    }
    static void Begin(string questId)
    {
        var quest=Database.quests.First(q=>q.id==questId);
        Set(questId+"_Started");
        if(questId.Contains("M01") && SceneManager.GetActiveScene().name=="Town_Main") Set("MayorIntroduced");
        var stories=StoryProgressManager.Instance;
        if(stories!=null) stories.TryStartQuest(quest.story,out _);
        var missions=StoryCampaignRuntime.FindMissionManager();
        if(missions!=null) {int index=missions.FindMissionIndex(quest.mission); if(index>=0 && !missions.IsMissionInProgress(index) && !missions.IsMissionCompleted(index)) missions.StartMission(index);}
    }
    public static InventoryController PlayerInventory(out PlayerInventorySessionBridge bridge)
    {
        // Town's usual inventory may be inactive while its panel is closed. Use the same
        // explicitly bound inventory as the existing trade and scene-transfer systems.
        var town=UnityEngine.Object.FindFirstObjectByType<TownPlayerInventoryController>(FindObjectsInactive.Include);
        if(town!=null) {
            town.RefreshReferences();bridge=town.SessionBridge;
            town.InventoryController?.InitializeInventory();bridge?.RestoreFromSession();
            return town.InventoryController;
        }
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerMove>();
        bridge=player!=null?player.GetComponent<PlayerInventorySessionBridge>():null;
        return bridge!=null?bridge.InventoryController:player!=null?player.GetComponent<InventoryController>():null;
    }
    public static void Sync()
    {
        var missions=StoryCampaignRuntime.FindMissionManager();
        if(missions!=null) missions.RefreshAllMissionProgress();
        // MissionSessionBridge already captures these states through the normal save path.
    }
}

