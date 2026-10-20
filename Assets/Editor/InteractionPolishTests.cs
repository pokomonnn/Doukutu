using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public static class InteractionPolishTests
{
    static void Assert(bool ok,string message){if(!ok)throw new Exception("INTERACTION TEST: "+message);}
    public static IEnumerator Check()
    {
        var manager=StoryCampaignRuntime.FindMissionManager();
        var db=StoryCampaignRuntime.Database;
        var quest=db.quests.First(q=>q.id=="S1S03").mission;
        int previous=manager.TrackedMissionIndex, index=manager.FindMissionIndex(quest);
        GameSessionManager.Instance.SetStoryFlag(quest.ObjectiveFinishFlag,false);
        manager.RestoreMissionState(index,(MissionProgressState2D)0,0,false);
        Assert(StoryCampaignRuntime.TryExecute("accept/S1S03",out _),"new acceptance");
        Assert(manager.TrackedMission==quest,"new dungeon mission not tracked");
        manager.SetTrackedMission(previous);
        StoryCampaignRuntime.TryExecute("accept/S1S03",out _);
        Assert(manager.TrackedMissionIndex==previous,"repeat acceptance stole tracking");
        var actor=UnityEngine.Object.FindObjectsByType<StoryInteractionPoint>(FindObjectsInactive.Exclude)
            .First(p=>p.stepId=="hub_1_Baldo").GetComponent<DialogueActor2D>();
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerMove>();
        Vector3 original=actor.transform.position;
        actor.transform.position=player.transform.position+Vector3.right;
        for(int i=0;i<3;i++)yield return null;
        var outline=actor.GetComponentInChildren<InteractableOutline2D>();
        Assert(outline!=null && outline.IsHighlighted,"nearby talk NPC not outlined");
        Assert(!outline.GetComponent<Collider2D>().enabled,"outline changes physics");
        var capture=(Texture2D)typeof(QuestHUDVisualTests).GetMethod("CaptureGameView",
            System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.NonPublic).Invoke(null,null);
        System.IO.File.WriteAllBytes("Logs/QuestHUD/npc-outline.png",capture.EncodeToPNG());
        UnityEngine.Object.Destroy(capture);
        actor.transform.position=player.transform.position+Vector3.right*100;
        for(int i=0;i<2;i++)yield return null;
        Assert(!outline.IsHighlighted,"distant NPC still outlined");
        actor.transform.position=original;
        Debug.Log("INTERACTION POLISH PASS: new/repeated acceptance, near/far NPC outline");
    }
}
