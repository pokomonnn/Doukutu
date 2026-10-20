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
public static class ResultReturnIntegrationTests
{
    const string Key="ResultReturn.Tests";
    static IEnumerator flow;static int frame=-1;static double started;
    static ResultReturnIntegrationTests(){if(SessionState.GetBool(Key,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate(){if(SessionState.GetBool(Key,false))SaveManager.EditorTestSaveFolder=Path.GetFullPath("Logs/ResultReturnSaves/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/stage/stage1.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static void Check(bool yes,string message){if(!yes)throw new Exception("RETURN TEST: "+message);}
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
    static void Tick(){try{if(EditorApplication.timeSinceStartup-started>180)throw new Exception("Return test timeout");if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;if(flow==null)flow=Flow();if(flow.MoveNext())return;SessionState.SetBool(Key,false);Debug.Log("RESULT RETURN PASS: stage1 elevator, rewards, reveal order, reload idempotence, town button, isolated save/load");EditorApplication.Exit(0);}catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}}
    static IEnumerator Flow()
    {
        for(int i=0;i<60;i++)yield return null;
        Check(!string.IsNullOrEmpty(SaveManager.EditorTestSaveFolder),"save isolation absent");
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyChaser2D>(FindObjectsInactive.Include))enemy.enabled=false;
        var session=GameSessionManager.Instance;Check(session!=null,"session absent");session.SetMoney(1000);session.BeginCurrentExpedition("stage1");
        var exit=UnityEngine.Object.FindFirstObjectByType<CaveExitElevator2D>();var collector=Field<ExpeditionResultCollector2D>(exit,"expeditionResultCollector");
        var inventory=Field<InventoryController>(UnityEngine.Object.FindFirstObjectByType<MissionManager2D>(),"inventoryController");inventory.InitializeInventory();
        var cash=Field<ItemData>(collector,"expeditionMoneyItem");Check(cash!=null&&inventory.TryAddItem(cash,137),"cash fixture");
        int cashReward=137*Field<int>(collector,"goldPerMoneyItem");
        var manager=UnityEngine.Object.FindFirstObjectByType<MissionManager2D>();
        var mission=Enumerable.Range(0,manager.MissionCount).Select(manager.GetMissionDefinition).First(m=>m!=null && m.ObjectiveType==MissionObjectiveType2D.CollectItem && m.ExpeditionMoneyReward>0);
        Check(session.AcceptMission(mission,false,MissionAcceptSource.Expedition,out var message),"accept: "+message);session.RegisterCurrentExpeditionMission(mission);manager.StartMission(manager.FindMissionIndex(mission));
        Check(inventory.TryAddItem(mission.RequiredItem,mission.RequiredAmount),"mission items fit");manager.RefreshAllMissionProgress();Check(manager.IsMissionCompleted(manager.FindMissionIndex(mission)),"native collection completion: "+mission.DisplayName+" amount="+inventory.GetTotalAmount(mission.RequiredItem)+" progress="+manager.GetMissionProgress(manager.FindMissionIndex(mission)));
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
        int expected=1000+cashReward+ui.CalculateRescueReward(data)+mission.ExpeditionMoneyReward+medical.CalculateTreatmentReward(RescueNpcTreatmentResultSession2D.Current);
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
        var save=SaveManager.Instance;Check(save!=null&&save.GetSaveFolderPath()==SaveManager.EditorTestSaveFolder,"isolated manager");Check(save.SaveSlot(1),"save town");session.SetMoney(0);Check(save.LoadSlot(1),"load town");for(int i=0;i<60;i++)yield return null;Check(GameSessionManager.Instance.CurrentMoney==expected,"saved reward restore");
        Debug.Log("RETURN VERIFIED money="+expected+" rescue="+data.RescuedNpcCount+" mission="+mission.DisplayName+" isolated saves="+SaveManager.EditorTestSaveFolder);
    }
}
#endif




