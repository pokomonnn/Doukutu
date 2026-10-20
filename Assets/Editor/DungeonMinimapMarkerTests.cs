using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DungeonMinimapMarkerTests
{
    static void Assert(bool ok,string message) { if(!ok)throw new Exception("MINIMAP TEST: "+message); }
    public static void InstallAndValidate()
    {
        MissionMinimapDataInstaller.Apply();
        var db=StoryCampaignRuntime.Database;
        foreach(string path in StoryIntegrationBuilder.Scenes.Skip(1)) {
            var scene=EditorSceneManager.OpenScene(path);
            var points=UnityEngine.Object.FindObjectsByType<StoryInteractionPoint>(FindObjectsInactive.Exclude);
            foreach(var q in db.quests) foreach(var g in q.mission.DisplayObjectives) {
                Assert(!string.IsNullOrEmpty(g.minimapScene),"unmapped "+q.id+"/"+g.id);
                if(!string.IsNullOrEmpty(g.minimapFollowMissionId)) {
                    Assert(db.quests.Any(d=>d.mission.MissionId==g.minimapFollowMissionId),"missing dependency");continue;
                }
                if(g.minimapScene!=scene.name)continue;
                foreach(string id in g.minimapPointIds)
                    Assert(points.Any(p=>p.stepId==id),"missing active point "+scene.name+"/"+id);
            }
            Assert(UnityEngine.Object.FindObjectsByType<CaveExitElevator2D>(FindObjectsInactive.Exclude).Any(e=>e.enabled),
                "missing exit "+scene.name);
        }
        Debug.Log("MINIMAP PLACEMENT PASS: all objective destinations and exits");
    }
    public static void Check(DungeonMinimap2D map)
    {
        var db=StoryCampaignRuntime.Database;
        var manager=StoryCampaignRuntime.FindMissionManager();
        var session=GameSessionManager.Instance;
        foreach(string flag in new[]{"S1S01_Completed","S1S01_Treated","S1S01_Delivered","step_finn"})
            session.SetStoryFlag("story/"+flag,false);
        Assert(StoryCampaignRuntime.TryExecute("accept/S1S01",out _),"accept Finn");
        var mission=db.quests.First(q=>q.id=="S1S01").mission;
        int index=Enumerable.Range(0,manager.MissionCount).First(i=>manager.GetMissionDefinition(i)==mission);
        manager.SetTrackedMission(index);
        var markers=map.GetComponent<DungeonMinimapMarkers2D>();
        markers.Refresh();
        Assert(markers.ObjectiveTargets.Any(t=>t.GetComponent<StoryInteractionPoint>()?.stepId=="finn"),"find Finn");
        StoryCampaignRuntime.Set("step_finn");markers.Refresh();
        Assert(markers.ObjectiveTargets.Any(t=>t.GetComponent<StoryInteractionPoint>()?.stepId=="finn"),"treat Finn");
        StoryCampaignRuntime.Set("S1S01_Treated");markers.Refresh();
        Assert(markers.ObjectiveTargets.Any(t=>t.GetComponent<StoryInteractionPoint>()?.stepId=="hub_1_Baldo"),"delivery Baldo");
        StoryCampaignRuntime.Set("S1S01_Delivered");markers.Refresh();
        Assert(markers.ObjectiveTargets.Any(t=>t.GetComponent<StoryInteractionPoint>()?.stepId=="hub_1_Baldo"),"report Baldo");
        StoryCampaignRuntime.Set("S1S01_Completed");StoryCampaignRuntime.Sync();markers.Refresh();
        Assert(!markers.ObjectiveTargets.Any(t=>t.GetComponent<StoryInteractionPoint>()?.stepId=="finn"),"completed target remains");
        manager.ClearTrackedMission();markers.Refresh();
        Assert(markers.ObjectiveTargets.Count==0,"untracked target remains");
        Assert(markers.VisibleMarkerCount>0,"NPC/exit markers missing");
        // A stationary player still sees flag-driven updates. Cross-scene reporting points to a real exit.
        var main=db.quests.First(q=>q.id=="S1M01").mission;
        session.SetStoryFlag("story/S1M01_Completed",false);
        StoryCampaignRuntime.Set("S1M01_ClueFound");
        var goal=MissionMinimapDestination2D.Current(main);
        Assert(goal.minimapScene=="Town_Main","report destination");
        int mainIndex=Enumerable.Range(0,manager.MissionCount).First(i=>manager.GetMissionDefinition(i)==main);
        session.SetStoryFlag(main.ActivationStoryFlag,true);
        Assert(manager.RestoreMissionState(mainIndex,MissionProgressState2D.InProgress,0,true),"restore tracked main mission");
        markers.Refresh();
        Assert(markers.ObjectiveTargets.Count==1 && markers.ObjectiveTargets[0].GetComponent<CaveExitElevator2D>()!=null,
            "cross-scene objective did not guide to return elevator");
        var dependency=db.quests.First(q=>q.id=="S3S02").mission;
        StoryCampaignRuntime.Set("S3S02_RouteFound");
        session.SetStoryFlag("story/S2S02_Completed",false);
        Assert(MissionMinimapDestination2D.Current(dependency)!=null,"dependency destination");
        Debug.Log("MINIMAP LIVE PASS: stationary flag updates, Finn/Baldo, clear tracking, dependency");
    }
}
