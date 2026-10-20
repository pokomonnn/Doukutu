using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class MissionMinimapDataInstaller
{
    // Scene + stable StoryInteractionPoint IDs. Positions remain authored in the scenes.
    public static void Configure(StoryCampaignDatabase db)
    {
        foreach (var q in db.quests) foreach (var g in q.mission.DisplayObjectives) {
            int stage = q.id[1] - '0';
            string point = g.id;
            string dependency = null;
            switch (q.id + "/" + g.id) {
                case "S1M01/report": case "S2M01/report": case "S3M01/report": stage=0;point="";break;
                case "S1S01/find": case "S1S01/treat": point="finn";break;
                case "S1S01/deliver": case "S1S01/report": point="hub_1_Baldo";break;
                case "S1S03/report": point="hub_1_Glen";break;
                case "S2S01/goods": point="original|alternative";break;
                case "S2S01/report": point="hub_2_Roan";break;
                case "S2S02/truce": dependency="story_S1S01";break;
                case "S2S02/permit": stage=0;point="";break;
                case "S2S02/report": point="hub_2_Baldo";break;
                case "S2S03/install": case "S2S03/report": point="hub_2_Glen";break;
                case "S3S01/proposal": case "S3S01/report": point="hub_3_Roan";break;
                case "S3S01/truce": point="hub_3_Baldo";break;
                case "S3S02/route": point="trade";break;
                case "S3S02/permit": dependency="story_S2S02";break;
                case "S3S02/report": point="hub_3_Baldo";break;
                case "S3S03/ledger": point="castleLedger";break;
                case "S3S03/water": dependency="story_S2S03";break;
                case "S3S03/report": point="hub_3_Glen";break;
                case "S4M01/ending": point="garden";break;
            }
            g.minimapScene=db.stageScenes[stage];
            g.minimapPointIds=string.IsNullOrEmpty(point)?Array.Empty<string>():point.Split('|');
            g.minimapFollowMissionId=dependency;
            EditorUtility.SetDirty(q.mission);
        }
    }
    [MenuItem("Tools/Story Campaign/Update minimap objective destinations")]
    public static void Apply()
    {
        Configure(StoryCampaignRuntime.Database);
        AssetDatabase.SaveAssets();
        Debug.Log("MINIMAP DATA PASS");
    }
}
