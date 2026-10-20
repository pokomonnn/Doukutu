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
public static class MedicalKitRoundTripTests
{
    const string Key="MedicalKit.RoundTrip";
    static IEnumerator flow;static double started;static int frame=-1;
    static MedicalKitRoundTripTests(){if(SessionState.GetBool(Key,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Isolate(){if(SessionState.GetBool(Key,false))SaveManager.EditorTestSaveFolder=Path.GetFullPath("Logs/MedicalRoundTripSaves/"+DateTime.Now.ToString("yyyyMMdd-HHmmss"));}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Town_Main.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static T Find<T>() where T:UnityEngine.Object=>UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
    static T Field<T>(object obj,string name)=>(T)obj.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(obj);
    static void Check(bool yes,string message){if(!yes)throw new Exception("MEDICAL ROUNDTRIP: "+message);}
    static void Counts(int a,int b,int c,string at){var s=MedicalKitSessionStore2D.CreateSnapshot();Check(s.Slot0Amount==a&&s.Slot1Amount==b&&s.Slot2Amount==c,at+" expected "+a+","+b+","+c+" actual "+MedicalKitSessionStore2D.Describe());var kit=Find<MedicalKitInventory2D>();if(kit!=null)Check(kit.GetAmountAtSlot(0)==a&&kit.GetAmountAtSlot(1)==b&&kit.GetAmountAtSlot(2)==c,at+" live kit mismatch");Debug.Log("MEDICAL COUNTS "+at+" "+a+","+b+","+c);}
    static void Tick(){try{if(EditorApplication.timeSinceStartup-started>240)throw new Exception("Medical roundtrip timeout");if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;if(flow==null)flow=Flow();if(flow.MoveNext())return;SessionState.SetBool(Key,false);Debug.Log("MEDICAL ROUNDTRIP PASS: shop buttons/payment/capacity, all three player and NPC treatments, dungeon/town disk saves, elevator return, refill and reentry");EditorApplication.Exit(0);}catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}}
    static MedicalKitSupplyShop2D OpenShop(){var shop=Find<MedicalKitSupplyShop2D>();Check(shop!=null,"town shop missing");for(Transform t=shop.transform;t!=null;t=t.parent)t.gameObject.SetActive(true);shop.enabled=true;shop.RefreshUI();return shop;}
    static void ChangeScene(string name){Find<SceneTransitionButton>().LoadSceneByName(name);}
    static IEnumerator Flow(){
        for(int i=0;i<60;i++)yield return null;
        Check(!string.IsNullOrEmpty(SaveManager.EditorTestSaveFolder),"save isolation");
        MedicalKitSessionStore2D.RestoreSnapshot(new SavedMedicalKitData{HasData=true});var session=GameSessionManager.Instance;session.SetMoney(10000);
        var shop=OpenShop();for(int i=0;i<5;i++)yield return null;
        var supplies=Field<MedicalKitSupplyShop2D.SupplySlot[]>(shop,"supplies");int spent=0;
        for(int i=0;i<3;i++)for(int n=0;n<3;n++){Check(supplies[i].buyOneButton!=null&&supplies[i].buyOneButton.interactable,"purchase button "+i);supplies[i].buyOneButton.onClick.Invoke();spent+=supplies[i].priceG;}
        Counts(3,3,3,"town purchase");Check(session.CurrentMoney==10000-spent,"purchase charged once");int before=session.CurrentMoney;for(int i=0;i<3;i++)Check(!shop.BuyOne(i),"overfill blocked");Check(session.CurrentMoney==before,"full purchase must not charge");
        var save=SaveManager.Instance;Check(save.SaveSlot(1),"town purchase save");MedicalKitSessionStore2D.RestoreSnapshot(new SavedMedicalKitData{HasData=true});Check(save.LoadSlot(1),"town purchase load");for(int i=0;i<20;i++)yield return null;Counts(3,3,3,"purchase disk restore");
        ChangeScene("stage1");for(int i=0;i<70;i++)yield return null;Check(SceneManager.GetActiveScene().name=="stage1","enter cave");Counts(3,3,3,"cave entry");
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyChaser2D>(FindObjectsInactive.Include))enemy.enabled=false;
        var kit=Find<MedicalKitInventory2D>();var treatment=Find<MedicalKitTreatment2D>();var player=GameObject.FindGameObjectWithTag("Player");var conditions=player.GetComponent<PlayerStatusConditionController>();Check(conditions!=null,"player status");
        var symptoms=new[]{StatusConditionType.Poison,StatusConditionType.Fracture,StatusConditionType.Bleeding};
        for(int i=0;i<3;i++){conditions.AddConditions(symptoms[i]);Check(treatment.TryUse(kit,kit.GetItemAtSlot(i),null,out var message),"player "+i+": "+message);Check(!conditions.HasCondition(symptoms[i]),"player cured "+i);}
        Counts(2,2,2,"player treatment");
        var npc=UnityEngine.Object.FindObjectsByType<RescuePersonSymptoms2D>(FindObjectsInactive.Exclude).First(n=>!n.IsDead&&n.GetComponent<StoryFinnDelivery>()==null);
        for(int i=0;i<3;i++){npc.AddSymptoms(symptoms[i]);Check(treatment.TryUse(kit,kit.GetItemAtSlot(i),npc,out var message),"NPC "+i+": "+message);Check(npc.IsStabilized(symptoms[i]),"NPC stabilized "+i);Check(!treatment.TryUse(kit,kit.GetItemAtSlot(i),npc,out _),"already treated must not consume "+i);}
        Counts(1,1,1,"NPC treatment and repeat rejection");
        Check(save.SaveSlot(2),"dungeon after-use save");kit.RestoreSavedAmounts(0,0,0);Check(save.LoadSlot(2),"dungeon after-use load");for(int i=0;i<20;i++)yield return null;Counts(1,1,1,"dungeon disk restore");
        var exit=Find<CaveExitElevator2D>();var rb=player.GetComponent<Rigidbody2D>();rb.position=exit.GetComponent<Collider2D>().bounds.center;rb.linearVelocity=Vector2.zero;rb.gravityScale=0;Physics2D.SyncTransforms();for(int i=0;i<15;i++)yield return null;
        Check(exit.IsPlayerInside,"elevator trigger");exit.OpenConfirmation();Check(exit.IsConfirmationOpen,"elevator confirm");Field<Button>(exit,"yesButton").onClick.Invoke();for(int i=0;i<30;i++)yield return null;Check(SceneManager.GetActiveScene().name=="Result","Result scene");Counts(1,1,1,"Result");
        var result=Find<ExpeditionResultUI>();result.GetComponent<ExpeditionResultSequenceAnimator2D>().Skip();Field<Button>(result,"returnTownButton").onClick.Invoke();for(int i=0;i<60;i++)yield return null;Check(SceneManager.GetActiveScene().name=="Town_Main","town return");Counts(1,1,1,"town return");
        Check(save.SaveSlot(3),"returned town save");MedicalKitSessionStore2D.RestoreSnapshot(new SavedMedicalKitData{HasData=true,Slot0Amount=3,Slot1Amount=0,Slot2Amount=2});Check(save.LoadSlot(3),"returned town load");for(int i=0;i<20;i++)yield return null;Counts(1,1,1,"returned town disk restore");
        shop=OpenShop();before=session.CurrentMoney;session.SetMoney(0);Check(!shop.BuyOne(0),"insufficient funds rejected");Counts(1,1,1,"failed purchase");session.SetMoney(before);Check(shop.BuyOne(0),"refill antidote");Counts(2,1,1,"asymmetric refill");
        ChangeScene("stage1");for(int i=0;i<60;i++)yield return null;Counts(2,1,1,"reentry");
        Debug.Log("MEDICAL ROUNDTRIP SAVES "+SaveManager.EditorTestSaveFolder);
    }
}
#endif
