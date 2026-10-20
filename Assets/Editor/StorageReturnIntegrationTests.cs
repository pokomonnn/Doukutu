#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class StorageReturnIntegrationTests
{
    const string Key="StorageReturn.Tests";
    static IEnumerator flow;static int frame=-1;static double started;
    static StorageReturnIntegrationTests(){if(SessionState.GetBool(Key,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate(){if(SessionState.GetBool(Key,false))SaveManager.EditorTestSaveFolder=Path.GetFullPath("Logs/StorageReturnSaves/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Town_Main.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static void Check(bool yes,string message){if(!yes)throw new Exception("RETURN TEST: "+message);}
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
    static void Tick(){try{if(EditorApplication.timeSinceStartup-started>180)throw new Exception("Return test timeout");if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;if(flow==null)flow=Flow();if(flow.MoveNext())return;SessionState.SetBool(Key,false);Debug.Log("RESULT RETURN PASS: stage1 elevator, rewards, reveal order, reload idempotence, town button, isolated save/load");EditorApplication.Exit(0);}catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}}
    static void CheckBoxCompatibility(ItemData marker)
    {
        var boxes=UnityEngine.Object.FindObjectsByType<ItemBoxSaveIdentity>(FindObjectsInactive.Include).Where(b=>b.gameObject.scene==SceneManager.GetActiveScene()).ToArray();
        Check(boxes.Select(b=>b.SaveKey).Distinct().Count()==boxes.Length,"unique box save keys");
        var duplicates=boxes.GroupBy(b=>b.PersistentId).First(g=>g.Count()>1).ToArray();
        var world=UnityEngine.Object.FindFirstObjectByType<WorldStateSaveBridge>();
        var saved=duplicates[0].CreateSaveData();saved.PersistentId=duplicates[0].PersistentId;
        saved.Items.Clear();saved.Items.Add(new SavedInventoryItemData{ItemId=marker.ItemId,Amount=1,GridX=0,GridY=0});
        WorldStateSessionStore.ReplaceSceneItemBoxes(SceneManager.GetActiveScene().name,new System.Collections.Generic.List<SavedItemBoxData>{saved});
        world.ReloadFromSession();Check(duplicates.Last().GetComponent<ItemBoxInventory>().GetTotalAmount(marker)==1,"legacy last-wins restoration unchanged");
        world.CaptureToSession();var snapshot=WorldStateSessionStore.CreateSceneSnapshot(SceneManager.GetActiveScene().name);
        Check(snapshot.ItemBoxes.Select(b=>b.PersistentId).Distinct().Count()==snapshot.ItemBoxes.Count,"captured keys unique");
        var target=duplicates.Last().GetComponent<ItemBoxInventory>();target.RemoveItemAmount(target.Grid.Items.First(i=>i.ItemData==marker),1);world.ReloadFromSession();Check(target.GetTotalAmount(marker)==1,"new key restores contents");
    }
    static IEnumerator Flow()
    {
        for(int i=0;i<60;i++)yield return null;
        Check(!string.IsNullOrEmpty(SaveManager.EditorTestSaveFolder),"save isolation absent");
        var marker=StoryCampaignRuntime.Database.steps.First(s=>s.id=="blueprint").grantItem;
        var townBox=UnityEngine.Object.FindFirstObjectByType<TownPlayerItemBoxSessionBridge>(FindObjectsInactive.Include);
        Check(townBox!=null,"town storage absent");townBox.EnsureSessionReady();
        int storedBefore=townBox.ItemBoxInventory.GetTotalAmount(marker);
        Check(townBox.ItemBoxInventory.TryAddItem(marker,1),"storage add fixture");townBox.CaptureToSession();
        var transition=UnityEngine.Object.FindObjectsByType<SceneTransitionButton>(FindObjectsInactive.Include).First();
        transition.LoadSceneByName("stage1");for(int i=0;i<60;i++)yield return null;
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyChaser2D>(FindObjectsInactive.Include))enemy.enabled=false;
        var session=GameSessionManager.Instance;Check(session!=null,"session absent");session.SetMoney(1000);session.BeginCurrentExpedition("stage1");
        var exit=UnityEngine.Object.FindFirstObjectByType<CaveExitElevator2D>();var collector=Field<ExpeditionResultCollector2D>(exit,"expeditionResultCollector");
        var inventory=Field<InventoryController>(UnityEngine.Object.FindFirstObjectByType<MissionManager2D>(),"inventoryController");inventory.InitializeInventory();
        Check(inventory.TryAddItem(marker,1),"inventory marker fixture");
        var torch=UnityEngine.Object.FindFirstObjectByType<TorchController>();Check(torch.TorchPercent>.97f,"torch full on dungeon entry");torch.SetTorch(20);
        CheckBoxCompatibility(marker);
        var actualBridge=GameObject.FindGameObjectWithTag("Player").GetComponent<PlayerInventorySessionBridge>();
        Debug.Log("STORAGE BINDINGS missionInv="+inventory.name+"@"+inventory.gameObject.scene.name+"#"+inventory.GetInstanceID()+" collector="+Field<InventoryController>(collector,"playerInventory").name+"#"+Field<InventoryController>(collector,"playerInventory").GetInstanceID()+" bridge="+actualBridge.InventoryController.name+"#"+actualBridge.InventoryController.GetInstanceID());
        var cash=Field<ItemData>(collector,"expeditionMoneyItem");Check(cash!=null&&inventory.TryAddItem(cash,137),"cash fixture");
        int cashReward=137*Field<int>(collector,"goldPerMoneyItem");
        var manager=UnityEngine.Object.FindFirstObjectByType<MissionManager2D>();
        var receiver=Field<ElevatorCarryReceiver2D>(collector,"elevatorCarryReceiver");
        var npc=UnityEngine.Object.FindObjectsByType<RescuePersonTarget2D>(FindObjectsInactive.Exclude).First(n=>n.GetComponent<StoryFinnDelivery>()==null && n.GetComponent<CarryableObject2D>()!=null && n.GetComponent<RescuePersonSymptoms2D>()!=null);
        var symptoms=npc.GetComponent<RescuePersonSymptoms2D>();Check(symptoms!=null,"rescue symptoms");symptoms.AddSymptoms(StatusConditionType.Poison);symptoms.StabilizeSymptoms(StatusConditionType.Poison);RescueNpcTreatmentResultSession2D.RecordSuccessfulTreatment(symptoms,StatusConditionType.Poison);
        Check(receiver.TryStoreCargo(npc.GetComponent<CarryableObject2D>()),"native cargo store");
        float until=Time.realtimeSinceStartup+2;while(Time.realtimeSinceStartup<until)yield return null;
        var player=GameObject.FindGameObjectWithTag("Player");var rb=player.GetComponent<Rigidbody2D>();rb.position=exit.GetComponent<Collider2D>().bounds.center;rb.linearVelocity=Vector2.zero;rb.gravityScale=0;Physics2D.SyncTransforms();
        for(int i=0;i<15;i++)yield return null;
        cashReward=inventory.GetTotalAmount(cash)*Field<int>(collector,"goldPerMoneyItem");Check(exit.IsPlayerInside,"player must enter elevator trigger");exit.OpenConfirmation();Check(exit.IsConfirmationOpen,"exit confirmation");exit.CloseConfirmation();Check(!exit.IsConfirmationOpen&&SceneManager.GetActiveScene().name=="stage1","cancel keeps cave");exit.OpenConfirmation();Field<Button>(exit,"yesButton").onClick.Invoke();
        for(int i=0;i<3;i++)yield return null;
        Check(SceneManager.GetActiveScene().name=="Result","result scene transition");
        var ui=UnityEngine.Object.FindFirstObjectByType<ExpeditionResultUI>();var medical=UnityEngine.Object.FindFirstObjectByType<RescueNpcTreatmentResultUI2D>();var data=ExpeditionResultSession.Current;
        Check(data.RescuedNpcCount==1,"rescue count");Check(ui.CurrentRecoveredMoney==cashReward,"cash count");
        int expected=1000+cashReward+ui.CalculateRescueReward(data)+medical.CalculateTreatmentReward(RescueNpcTreatmentResultSession2D.Current);
        Check(session.CurrentMoney==expected,"payout expected "+expected+" actual "+session.CurrentMoney);
        Check(ui.CurrentTotalReward==expected-1000-medical.CurrentTreatmentReward,"displayed subtotal");
        var button=Field<Button>(ui,"returnTownButton");Check(!button.interactable,"return enabled too early");
        string[] order={"01 Exploration summary","02 Missions","03 Treasure","04 Medical","05 Rewards"};
        until=Time.realtimeSinceStartup+10;
        while(!button.interactable&&Time.realtimeSinceStartup<until){bool previous=true;foreach(var name in order){var g=button.transform.parent.Find(name).GetComponent<CanvasGroup>();if(g.alpha>0)Check(previous,"reveal order "+name);previous=g.alpha>=.999f;}yield return null;}
        Check(button.interactable,"return never enabled");
        ui.TryGrantRescueReward();ui.TryGrantExpeditionMissionRewards();medical.TryGrantTreatmentReward();Check(session.CurrentMoney==expected,"repeated reward grant");
        SceneManager.LoadScene("Result");for(int i=0;i<30;i++)yield return null;Check(session.CurrentMoney==expected,"reload duplicates rewards");
        ui=UnityEngine.Object.FindFirstObjectByType<ExpeditionResultUI>();ui.GetComponent<ExpeditionResultSequenceAnimator2D>().Skip();button=Field<Button>(ui,"returnTownButton");button.onClick.Invoke();
        for(int i=0;i<90;i++)yield return null;Check(SceneManager.GetActiveScene().name=="Town_Main","town button transition");Check(session.CurrentMoney==expected,"town money preserved");Check(session.CurrentExpeditionMissionIds.Count==0&&!ExpeditionResultSession.HasResult,"expedition cleanup");
        var townInventory=UnityEngine.Object.FindFirstObjectByType<PlayerInventorySessionBridge>(FindObjectsInactive.Include).InventoryController;
        Check(townInventory.GetTotalAmount(marker)==1,"player inventory survived elevator and town");
        townBox=UnityEngine.Object.FindFirstObjectByType<TownPlayerItemBoxSessionBridge>(FindObjectsInactive.Include);townBox.EnsureSessionReady();
        Check(townBox.ItemBoxInventory.GetTotalAmount(marker)==storedBefore+1,"town storage survived expedition");
        var save=SaveManager.Instance;Check(save!=null&&save.GetSaveFolderPath()==SaveManager.EditorTestSaveFolder,"isolated manager");Check(save.SaveSlot(1),"save town");townBox.ItemBoxInventory.RemoveItemAmount(townBox.ItemBoxInventory.Grid.Items.First(i=>i.ItemData==marker),1);session.SetMoney(0);Check(save.LoadSlot(1),"load town");for(int i=0;i<60;i++)yield return null;Check(GameSessionManager.Instance.CurrentMoney==expected,"saved reward restore");
        townBox=UnityEngine.Object.FindFirstObjectByType<TownPlayerItemBoxSessionBridge>(FindObjectsInactive.Include);
        Check(townBox.ItemBoxInventory.GetTotalAmount(marker)==storedBefore+1,"storage restored from disk");
        Check(UnityEngine.Object.FindFirstObjectByType<PlayerInventorySessionBridge>(FindObjectsInactive.Include).InventoryController.GetTotalAmount(marker)==1,"inventory restored from disk");
        transition=UnityEngine.Object.FindObjectsByType<SceneTransitionButton>(FindObjectsInactive.Include).First();transition.LoadSceneByName("stage1");for(int i=0;i<60;i++)yield return null;
        torch=UnityEngine.Object.FindFirstObjectByType<TorchController>();Check(torch.TorchPercent>.97f,"torch refilled after 20 percent return");
        Check(UnityEngine.Object.FindFirstObjectByType<MissionManager2D>()!=null,"dungeon reentry");
        Check(Field<InventoryController>(UnityEngine.Object.FindFirstObjectByType<MissionManager2D>(),"inventoryController").GetTotalAmount(marker)==1,"inventory survived reentry");
        torch.SetTorch(20);Check(save.SaveSlot(2),"dungeon save");torch.SetTorch(70);Check(save.LoadSlot(2),"dungeon load");for(int i=0;i<10;i++)yield return null;Check(Mathf.Abs(torch.CurrentTorch-20)<1,"same scene load preserves torch");
        Debug.Log("STORAGE RETURN PASS: legacy and unique box keys, player inventory, town storage, disk restore, torch scene refill and same-scene load");
        Debug.Log("RETURN VERIFIED money="+expected+" rescue="+data.RescuedNpcCount+" isolated saves="+SaveManager.EditorTestSaveFolder);
    }
}
#endif








