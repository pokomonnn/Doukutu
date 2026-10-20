using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class StoryIntegrationTests
{
    const string Key="StoryIntegration.Tests";
    static IEnumerator test;
    static double start;
    static int lastPlayFrame=-1;
    static StoryIntegrationTests()
    {
        if(SessionState.GetBool(Key,false)) {start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}
    }
    static void Assert(bool condition,string message){if(!condition)throw new Exception("STORY TEST: "+message);}
    static void Command(string id)
    {
        Assert(StoryCampaignRuntime.TryExecute(id,out string message),id+": "+message);
        switch(id) {
            case "step/diary": CheckGoal("S1M01","report");break;
            case "step/machine": CheckGoal("S2M01","report");break;
            case "step/map": CheckGoal("S3M01","report");break;
            case "step/alternative": CheckGoal("S2S01","report");break;
            case "step/ledger": CheckGoal("S2S02","permit");break;
            case "step/install": CheckGoal("S2S03","water");break;
            case "step/water": CheckGoal("S2S03","report");break;
            case "step/route": CheckGoal("S3S01","proposal");break;
            case "step/trade": CheckGoal("S3S02","permit");break;
            case "step/structure": CheckGoal("S3S03","water");break;
            case "step/garden": CheckGoal("S4M01","ending");break;
        }
    }
    static void CheckGoal(string quest,string expected)
    {
        var mission=StoryCampaignRuntime.Database.quests.First(q=>q.id==quest).mission;
        var snapshot=MissionObjectiveDisplayState.Snapshot(mission);
        int index=Array.FindIndex(snapshot,done=>!done);
        Assert(index>=0 && mission.DisplayObjectives[index].id==expected,"next objective "+quest+" expected "+expected);
    }
    static void Fail(string id){Assert(!StoryCampaignRuntime.TryExecute(id,out _),"unexpected success: "+id);}
    public static void Validate()
    {
        var db=StoryCampaignRuntime.Database;
        Assert(db!=null && db.quests.Count==12 && db.steps.Count==32,"definition counts");
        foreach(var quest in db.quests) {
            Assert(quest.mission.HasDisplayObjectives,"missing objectives "+quest.id);
            Assert(quest.mission.DisplayObjectives.All(o=>!string.IsNullOrWhiteSpace(o.text)),"empty objective text");
            Assert(quest.mission.DisplayObjectives.Select(o=>o.id).Distinct().Count()==quest.mission.DisplayObjectives.Count,"duplicate objective id");
        }
        var waterGoals=db.quests.First(q=>q.id=="S2S03").mission.DisplayObjectives;
        Assert(!waterGoals[0].IsComplete(f=>f=="story/S2S03_GearFound") &&
            waterGoals[2].IsComplete(f=>f=="story/S2S03_GearFound"),"out-of-order component collection");
        Assert(waterGoals.Take(4).All(o=>o.IsComplete(f=>f=="story/S2S03_Installed")),"installed components must remain complete");
        var condition=new MissionObjectiveDisplay2D{anyOfFlags=new[]{"item","rescue"},allOfFlags=new[]{"return"}};
        Assert(!condition.IsComplete(f=>f=="item"),"objective all condition ignored");
        Assert(condition.IsComplete(f=>f=="rescue" || f=="return"),"objective out-of-order condition");
        Assert(!new MissionObjectiveDisplay2D().IsComplete(f=>true),"empty objective auto-completed");
        var finnDialogue=db.steps.First(s=>s.id=="finn").dialogue;
        Assert(string.IsNullOrEmpty(finnDialogue.FindNode("start").choices[0].eventId),"opening Finn dialogue must not complete discovery");
        Assert(finnDialogue.Nodes.Last().choices.Any(c=>c.eventId=="step/finn"),"Finn agreement missing");
        Assert((int)MissionObjectiveType2D.RescueTargetNpc==3 && (int)DialogueActionKind2D.TakeItemFromPlayer==4,"serialized enums changed");
        for(int stage=1;stage<=3;stage++)foreach(string role in new[]{"Roan","Glen","Baldo"}) {
            var hub=AssetDatabase.LoadAssetAtPath<DialogueData2D>("Assets/Resources/StoryCampaign/Dialogue_Hub_"+stage+"_"+role+".asset");
            Assert(hub!=null,"hub dialogue absent");
            foreach(var node in hub.Nodes) {
                if(!string.IsNullOrEmpty(node.nextNodeId))Assert(hub.FindNode(node.nextNodeId)!=null,"hub node target");
                foreach(var choice in node.choices)if(!string.IsNullOrEmpty(choice.nextNodeId))Assert(hub.FindNode(choice.nextNodeId)!=null,"hub choice target");
            }
        }
        foreach(var s in db.steps) {
            Assert(s.dialogue!=null,"dialogue missing "+s.id);
            foreach(var node in s.dialogue.Nodes) {
                if(!string.IsNullOrEmpty(node.nextNodeId))Assert(s.dialogue.FindNode(node.nextNodeId)!=null,"node target "+node.nextNodeId);
                foreach(var c in node.choices) if(!string.IsNullOrEmpty(c.nextNodeId))Assert(s.dialogue.FindNode(c.nextNodeId)!=null,"choice target "+c.nextNodeId);
            }
        }
        foreach(string scenePath in StoryIntegrationBuilder.Scenes) {
            var scene=EditorSceneManager.OpenScene(scenePath);var roots=scene.GetRootGameObjects();
            var huds=roots.SelectMany(g=>g.GetComponentsInChildren<MissionHUD2D>(true)).Where(h=>h.enabled).ToArray();
            Assert(huds.Length==1,"expected one active objective HUD: "+scenePath);
            var hudData=new SerializedObject(huds[0]);
            foreach(string field in new[]{"missionManager","hudRoot","missionTitleText","missionDescriptionText","completionText"})
                Assert(hudData.FindProperty(field).objectReferenceValue!=null,"HUD reference "+field+" "+scenePath);
            Assert(roots.Count(g=>g.name=="StoryIntegration")==1,"integration root "+scenePath);
            foreach(var t in roots.SelectMany(g=>g.GetComponentsInChildren<Transform>(true))) {
                int missing=GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject);
                if(t.root.name=="StoryIntegration") Assert(missing==0,"story missing script "+t.name);
                else if(missing>0) Debug.LogWarning("STORY AUDIT existing missing script: "+scenePath+" / "+t.name);
            }
            foreach(var mm in roots.SelectMany(g=>g.GetComponentsInChildren<MissionManager2D>(true)))
                foreach(var q in db.quests)Assert(mm.FindMissionIndex(q.mission)>=0,"mission missing "+q.id+" "+scenePath);
        }
        Debug.Log("STORY VALIDATION PASS: 12 native quests; 32 authored steps + treatment/delivery; 5 existing scenes");
    }
    public static void RunPlay()
    {
        Validate();SessionState.SetBool(Key,true);EditorSceneManager.OpenScene(StoryIntegrationBuilder.Scenes[0]);EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        try {
            if(EditorApplication.timeSinceStartup-start>240)throw new Exception("STORY TEST timeout");
            if(!EditorApplication.isPlaying)return;
            // Editor updates can run several times per player frame in batch mode.
            if(lastPlayFrame==Time.frameCount)return;
            lastPlayFrame=Time.frameCount;
            if(test==null)test=Flow();
            if(test.MoveNext())return;
            SessionState.SetBool(Key,false);Debug.Log("STORY PLAYTEST PASS: native town dialogue, items/full inventory, rescue cargo, all 12 quests, save roundtrip, endings/idempotence");EditorApplication.Exit(0);
        } catch(Exception e) {SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator WaitFrames(int count=30){for(int i=0;i<count;i++)yield return null;}
    static IEnumerator Flow()
    {
        for(int i=0;i<100;i++)yield return null;
        // Suppress only test-run disk writes. Production SaveManager remains unchanged.
        foreach(var autosave in UnityEngine.Object.FindObjectsByType<AutoSaveTrigger>(FindObjectsInactive.Include))autosave.enabled=false;
        var session=GameSessionManager.Instance;Assert(session!=null,"native session absent");
        typeof(GameSessionManager).GetField("alwaysLogSessionTransfer",BindingFlags.NonPublic|BindingFlags.Instance)?.SetValue(session,true);
        var db=StoryCampaignRuntime.Database;
        foreach(string flag in session.StoryFlags.Where(f=>f.StartsWith("story/")).ToArray())session.SetStoryFlag(flag,false);
        foreach(string order in new[]{"123","132","213","231","312","321"}) {
            foreach(int n in new[]{1,2,3})session.SetStoryFlag("story/S"+n+"M01_Completed",false);
            for(int i=0;i<3;i++){StoryCampaignRuntime.Set("S"+order[i]+"M01_Completed");Assert(StoryCampaignRuntime.GardenOpen==(i==2),"main report order "+order);}
        }
        foreach(int n in new[]{1,2,3})session.SetStoryFlag("story/S"+n+"M01_Completed",false);
        var controller=UnityEngine.Object.FindFirstObjectByType<TownConversationController>();
        controller.OpenConversation(Resources.Load<TownConversationData>("StoryCampaign/Town_Mayor"));
        Assert(controller.IsOpen && controller.CurrentBlockId=="menu","native town dialogue did not open");controller.CloseConversation();
        Fail("step/report1");Fail("accept/S2S02");
        Fail("accept/S1S01");
        foreach(var q in db.quests.Where(q=>q.id.Contains("M01") && q.id!="S4M01"))Command("accept/"+q.id);
        int initialMoney=session.CurrentMoney;
        // Explore stage 3 before its prerequisite side quests and before the other main clues.
        yield return null;SceneManager.LoadScene("Doukutu_3");for(int i=0;i<100;i++)yield return null;
        DisableTestDangers();AcceptSideQuests(3);Command("step/map");Command("step/route");Command("step/proposal");Command("step/truce");Command("step/reportTruce");
        Command("step/trade");Command("step/castleLedger");Command("step/structure");Fail("step/reportTrade");Fail("step/reportCastle");CheckNegotiationZones();
        Capture();SceneManager.LoadScene("Town_Main");for(int i=0;i<80;i++)yield return null;
        Command("step/report3");Fail("step/proposal");Fail("step/reportCastle");Assert(!StoryCampaignRuntime.GardenOpen,"garden early");
        Capture();SceneManager.LoadScene("stage1");for(int i=0;i<100;i++)yield return null;
        DisableTestDangers();AcceptSideQuests(1);
        var inventory=StoryCampaignRuntime.PlayerInventory(out _);
        var original=inventory.Grid;
        var field=typeof(InventoryController).GetField("inventoryGrid",BindingFlags.NonPublic|BindingFlags.Instance);
        var full=new InventoryGrid(1,1);
        var blueprint=db.steps.First(s=>s.id=="blueprint").grantItem;full.TryAddItem(blueprint,1,out _);field.SetValue(inventory,full);
        Fail("step/blueprint");field.SetValue(inventory,original);
        Command("step/diary");Fail("step/medicine");Fail("step/deliverMedicine");Fail("accept/S1S02");Command("step/blueprint");
        Command("step/reportBlueprint");Fail("step/reportFinn");
        var tracking=StoryCampaignRuntime.FindMissionManager();
        var finnMission=db.quests.First(q=>q.id=="S1S01").mission;
        Assert(tracking.SetTrackedMission(tracking.FindMissionIndex(finnMission)),"track Finn");
        var hud=UnityEngine.Object.FindObjectsByType<MissionHUD2D>(FindObjectsInactive.Include).First(h=>h.enabled);
        float hudWait=Time.unscaledTime+.2f;while(Time.unscaledTime<hudWait)yield return null;
        Assert(hud.CurrentObjectiveText.Contains("捜し"),"initial objective: text="+hud.CurrentObjectiveText+" active="+hud.isActiveAndEnabled+" tracked="+tracking.TrackedMission?.name+" frame="+Time.frameCount);
        int notices=hud.CompletionNoticeCount;
        Command("step/finn");hud.RefreshUI();for(int i=0;i<10;i++)yield return null;
        Assert(hud.CurrentObjectiveText.Contains("解毒剤") && hud.CompletionNoticeCount==notices+1,"agreement objective update");
        StoryCampaignRuntime.Set("step_finn");hud.RefreshUI();for(int i=0;i<10;i++)yield return null;
        Assert(hud.CompletionNoticeCount==notices+1,"duplicate completion notice");
        var finn=UnityEngine.Object.FindFirstObjectByType<StoryFinnDelivery>();Assert(finn!=null,"Finn missing");
        Assert(finn.GetComponent<RescuePersonInfo2D>().DisplayName=="フィン","Finn identity mismatch");
        var cargo=finn.GetComponent<CarryableObject2D>();Assert(cargo!=null,"native carrying missing");
        var receiver=UnityEngine.Object.FindObjectsByType<ElevatorCarryReceiver2D>(FindObjectsInactive.Exclude).First();
        var collector=UnityEngine.Object.FindObjectsByType<ExpeditionResultCollector2D>(FindObjectsInactive.Exclude).First();
        typeof(ExpeditionResultCollector2D).GetField("elevatorCarryReceiver",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(collector,receiver);
        Assert(!receiver.TryStoreCargo(cargo),"Finn accepted by return elevator");
        var symptoms=finn.GetComponent<RescuePersonSymptoms2D>();
        Assert(symptoms.HasSymptom(StatusConditionType.Poison),"Finn not poisoned");
        var hp=finn.GetComponent<CharacterHealth>();hp.RestoreHealth(1);hp.TakeDamage(99999);
        Assert(!hp.IsDead && hp.CurrentHealth>=1,"Finn can die");
        Assert(!finn.TryDeliver(),"untreated Finn delivered");
        var kit=UnityEngine.Object.FindFirstObjectByType<MedicalKitInventory2D>(FindObjectsInactive.Include);
        var treatment=kit.GetComponent<MedicalKitTreatment2D>();
        var antidote=kit.GetItemAtSlot(0);var bandage=kit.GetItemAtSlot(2);
        kit.RestoreSavedAmounts(0,1,1);
        Assert(!treatment.TryUse(kit,antidote,symptoms,out _),"empty kit treated Finn");
        Assert(!treatment.TryUse(kit,bandage,symptoms,out _),"wrong item treated poison");
        kit.RestoreSavedAmounts(2,1,1);
        Assert(treatment.TryUse(kit,antidote,symptoms,out _),"antidote failed");
        Assert(finn.IsTreated && !symptoms.HasSymptom(StatusConditionType.Poison),"poison not cured");
        Assert(kit.GetAmount(antidote)==1,"antidote consumption");
        hud.RefreshUI();for(int i=0;i<10;i++)yield return null;
        Assert(hud.CurrentObjectiveText.Contains("バルドのもとへ運ぶ"),"treatment objective update");
        Assert(!treatment.TryUse(kit,antidote,symptoms,out _) && kit.GetAmount(antidote)==1,"repeat treatment consumed item");
        Assert(!finn.TryDeliver(),"distant Finn delivered");
        Capture();SceneManager.LoadScene("stage1");for(int i=0;i<80;i++)yield return null;
        DisableTestDangers();finn=UnityEngine.Object.FindFirstObjectByType<StoryFinnDelivery>();
        Assert(finn.IsTreated && !finn.GetComponent<RescuePersonSymptoms2D>().HasSymptom(StatusConditionType.Poison),"treatment lost after revisit");
        var carrier=UnityEngine.Object.FindFirstObjectByType<PlayerCarryController2D>();
        var originalPosition=finn.transform.position;
        carrier.transform.position=finn.baldo.position;finn.transform.position=finn.baldo.position;
        Assert(!finn.TryDeliver(),"uncarried proximity completed delivery");
        finn.transform.position=originalPosition;
        carrier.transform.position=finn.transform.position;Physics2D.SyncTransforms();
        Assert(carrier.TryPickUp(finn.GetComponent<CarryableObject2D>()),"native pickup failed");
        carrier.transform.position=finn.baldo.position;finn.transform.position=finn.baldo.position;
        Assert(finn.TryDeliver(),"delivery to Baldo failed");
        var manager=StoryCampaignRuntime.FindMissionManager();
        Assert(manager.IsMissionCompleted(manager.FindMissionIndex(db.quests.First(q=>q.id=="S1S01").mission)),"native mission not complete on arrival");
        Assert(!carrier.IsCarrying,"delivery left carry lock");
        hud=UnityEngine.Object.FindObjectsByType<MissionHUD2D>(FindObjectsInactive.Include).First(h=>h.enabled);
        hud.RefreshUI();for(int i=0;i<10;i++)yield return null;
        Assert(manager.TrackedMission==finnMission && hud.CurrentObjectiveText.Contains("報告"),"completed delivery must remain tracked until report");
        Capture();var pendingSave=session.CreateSaveGameData("stage1");notices=hud.CompletionNoticeCount;
        Assert(session.ApplySaveGameData(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(pendingSave)),SaveManager.Instance.ItemDataDatabase,out _),"pending report save load");
        session.RestoreMissionsToManager(manager);hud.RefreshUI();for(int i=0;i<10;i++)yield return null;
        Assert(manager.TrackedMission==finnMission && hud.CurrentObjectiveText.Contains("報告") && hud.CompletionNoticeCount==notices,"pending report restored without animation: tracked="+manager.TrackedMission?.name+" body="+hud.CurrentObjectiveText+" notices="+hud.CompletionNoticeCount+" expected="+notices);
        int beforeReward=session.CurrentMoney;Command("step/reportFinn");Fail("step/reportFinn");
        Assert(session.CurrentMoney==beforeReward+100 && StoryCampaignRuntime.Has("PlayerBanditTruce"),"reward/truce missing");
        Assert(session.IsMissionRewardClaimed("story_S1S01"),"native reward claim not recorded");
        hud.RefreshUI();for(int i=0;i<10;i++)yield return null;
        Assert(manager.TrackedMission!=finnMission && hud.CompletionNoticeCount==notices+2,"report and quest completion notifications");
        Debug.Log("OBJECTIVE HUD PASS: agreement, treatment, delivery, pending-report save/load, report, notification idempotence");
        Capture();SceneManager.LoadScene("Town_Main");for(int i=0;i<80;i++)yield return null;
        var townInventory=StoryCampaignRuntime.PlayerInventory(out var townBridge);
        Debug.Log("STORY INVENTORY town="+(townInventory!=null?townInventory.name:"null")+" bridge="+(townBridge!=null?townBridge.name:"null")+" blueprint="+(townInventory!=null?townInventory.GetTotalAmount(blueprint):-1)+" session="+session.GetInventorySessionSummary());
        Command("step/report1");Fail("step/reportFinn");Fail("step/reportBlueprint");
        Assert(!StoryCampaignRuntime.GardenOpen,"garden before 3rd report");
        Capture();SceneManager.LoadScene("stage1");for(int i=0;i<80;i++)yield return null;
        DisableTestDangers();Assert(UnityEngine.Object.FindFirstObjectByType<StoryRescueIdentity>()==null,"rescued Finn respawned");Fail("step/reportFinn");
        Capture();SceneManager.LoadScene("Doukutu_2");for(int i=0;i<100;i++)yield return null;
        DisableTestDangers();AcceptSideQuests(2);foreach(string id in new[]{"machine","alternative","ledger","bearing","valve","gear","install"})Command("step/"+id);
        foreach(string id in new[]{"bearing","valve","gear"})Fail("step/"+id);
        foreach(string id in new[]{"water","reportWater","reportSupply"})Command("step/"+id);
        foreach(string id in new[]{"bearing","valve","gear"})Fail("step/"+id);
        Capture();SceneManager.LoadScene("Town_Main");for(int i=0;i<80;i++)yield return null;
        foreach(string id in new[]{"report2","permit"})Command("step/"+id);
        Fail("step/reportWater");Fail("step/reportSupply");
        Assert(StoryCampaignRuntime.GardenOpen,"garden never unlocked");
        Capture();SceneManager.LoadScene("Doukutu_2");for(int i=0;i<80;i++)yield return null;DisableTestDangers();Command("step/reportPermit");
        Capture();SceneManager.LoadScene("Doukutu_3");for(int i=0;i<80;i++)yield return null;DisableTestDangers();Command("step/reportTrade");Command("step/reportCastle");
        Capture();SceneManager.LoadScene("Town_Main");for(int i=0;i<80;i++)yield return null;
        Assert(session.CurrentMoney==initialMoney+1100,"quest reward ledger mismatch");
        Capture();var save=session.CreateSaveGameData("Town_Main");
        var itemDb=SaveManager.Instance.ItemDataDatabase;
        Assert(session.ApplySaveGameData(JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(save)),itemDb,out string msg),"save restore "+msg);
        Assert(StoryCampaignRuntime.SupportsCastle,"support flags lost");Fail("step/report1");
        Assert(session.IsMissionRewardClaimed("story_S1S01"),"native reward claim lost after save");
        Capture();SceneManager.LoadScene("Doukutu_4");for(int i=0;i<80;i++)yield return null;
        Fail("ending/C");Command("step/garden");
        foreach(string support in new[]{"adventurer","bandit","dwarf"}) {session.SetStoryFlag("story/faction_"+support+"_support",false);Fail("ending/C");StoryCampaignRuntime.Set("faction_"+support+"_support");}
        string beforeEnding=JsonUtility.ToJson(session.CreateSaveGameData("Doukutu_4"));
        foreach(string kind in new[]{"A","B"}) {
            Command("ending/"+kind);Fail("ending/"+kind);Assert(session.CurrentMoney==initialMoney+1600,"ending reward "+kind);
            Assert(session.ApplySaveGameData(JsonUtility.FromJson<SaveGameData>(beforeEnding),itemDb,out _),"ending test reset");
        }
        Command("ending/C");Fail("ending/A");Fail("ending/C");Assert(session.CurrentMoney==initialMoney+1600,"ending duplicated rewards");
        Assert(db.quests.All(q=>StoryCampaignRuntime.Has(q.id+"_Completed")),"not all 12 quests complete");
        Assert(db.quests.All(q=>MissionObjectiveDisplayState.Snapshot(q.mission).All(done=>done)),"completed quest objective history");
        Debug.Log("ALL QUEST OBJECTIVES PASS: 12 definitions, event-driven next goals, cross-quest prerequisites, alternate supplies, unordered components, final histories");
        string completed=JsonUtility.ToJson(session.CreateSaveGameData("Doukutu_4"));
        Assert(session.ApplySaveGameData(JsonUtility.FromJson<SaveGameData>(completed),itemDb,out _),"completed save restore");Fail("ending/C");
    }
    static void Capture()
    {
        var inv=StoryCampaignRuntime.PlayerInventory(out var ownerBridge);
        Debug.Log("STORY INVENTORY capture scene="+SceneManager.GetActiveScene().name+" owner="+(inv!=null?inv.name:"null")+" bridge="+(ownerBridge!=null?ownerBridge.name:"null")+" items="+(inv!=null&&inv.Grid!=null?string.Join(",",inv.Grid.Items.Select(i=>i.ItemData.name)):"none"));
        foreach(var bridge in UnityEngine.Object.FindObjectsByType<PlayerInventorySessionBridge>(FindObjectsInactive.Exclude))bridge.CaptureToSession();
        foreach(var bridge in UnityEngine.Object.FindObjectsByType<MissionSessionBridge>(FindObjectsInactive.Exclude).Where(b=>b.gameObject.scene==SceneManager.GetActiveScene()))bridge.CaptureToSession();
    }
    static void AcceptSideQuests(int stage)
    {
        foreach(string suffix in new[]{"S01","S02","S03"})if(stage!=1 || suffix!="S02")Command("accept/S"+stage+suffix);
    }
    static void CheckNegotiationZones()
    {
        var zone=UnityEngine.Object.FindObjectsByType<StoryNegotiationZone>(FindObjectsInactive.Exclude).First(z=>z.requiredFlag=="S3S01_Completed");
        var a=new GameObject("Story test adventurer").AddComponent<FactionMember2D>();a.Configure(FactionId2D.Adventurers);
        var b=new GameObject("Story test bandit").AddComponent<FactionMember2D>();b.Configure(FactionId2D.Bandits);
        a.transform.position=b.transform.position=zone.transform.position;
        Assert(StoryNegotiationZone.Protects(a,b),"corridor truce missing");
        b.Configure(FactionId2D.Monsters);Assert(!StoryNegotiationZone.Protects(a,b),"monster made peaceful by negotiation");
        b.Configure(FactionId2D.Bandits);a.transform.position=b.transform.position=Vector3.one*10000;
        Assert(!StoryNegotiationZone.Protects(a,b),"truce extends beyond local corridor");
        UnityEngine.Object.Destroy(a.gameObject);UnityEngine.Object.Destroy(b.gameObject);
    }
    static void DisableTestDangers()
    {
        Assert(UnityEngine.Object.FindFirstObjectByType<StoryCampaignScene>()!=null,"story objects lost on scene transition");
        foreach(var auto in UnityEngine.Object.FindObjectsByType<AutoSaveTrigger>(FindObjectsInactive.Include))auto.enabled=false;
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyChaser2D>(FindObjectsInactive.Include))enemy.enabled=false;
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerMove>();
        if(player!=null){player.enabled=false;player.GetComponent<Rigidbody2D>().bodyType=RigidbodyType2D.Static;}
    }
}









