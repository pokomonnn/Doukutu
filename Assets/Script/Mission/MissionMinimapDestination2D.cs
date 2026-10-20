using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Read-only projection of the same objective flags used by the tracker.</summary>
public static class MissionMinimapDestination2D
{
    public static MissionObjectiveDisplay2D Current(MissionDefinition2D mission, int depth = 0)
    {
        if (mission == null || depth > 8) return null;
        var done = MissionObjectiveDisplayState.Snapshot(mission);
        for (int i = 0; i < done.Length; i++) {
            if (done[i]) continue;
            var goal = mission.DisplayObjectives[i];
            if (!string.IsNullOrEmpty(goal.minimapFollowMissionId)) {
                var db = StoryCampaignRuntime.Database;
                var dependency = db != null ? db.quests.FirstOrDefault(q => q.mission.MissionId == goal.minimapFollowMissionId) : null;
                return dependency != null ? Current(dependency.mission, depth + 1) : null;
            }
            return goal;
        }
        return null;
    }

    public static void Resolve(MissionManager2D manager, Scene scene, Vector3 origin,
        IList<StoryInteractionPoint> points, IList<CaveExitElevator2D> exits,
        List<Transform> targets, out string caption)
    {
        targets.Clear(); caption = "";
        if (manager == null || !manager.HasTrackedMission) return;
        var mission = manager.TrackedMission;
        if (!mission.HasDisplayObjectives) {
            var target = manager.TrackedCompassTarget;
            if (target != null && target.gameObject.activeInHierarchy && target.gameObject.scene == scene) targets.Add(target);
            return;
        }
        var goal = Current(mission);
        if (goal == null || string.IsNullOrEmpty(goal.minimapScene)) return;
        if (goal.minimapScene != scene.name) {
            string destination = goal.minimapScene == "Town_Main" ? "町" : goal.minimapScene;
            var db = StoryCampaignRuntime.Database;
            int stage = db != null ? System.Array.IndexOf(db.stageScenes, goal.minimapScene) : -1;
            if (stage > 0) destination = "ステージ" + stage;
            caption = "目標：" + destination + "（帰還口へ）";
            var exit = exits.Where(e => e != null && e.isActiveAndEnabled && e.gameObject.scene == scene)
                .OrderBy(e => (e.transform.position - origin).sqrMagnitude).FirstOrDefault();
            if (exit != null) targets.Add(exit.transform);
            return;
        }
        foreach (var point in points)
            if (point != null && point.isActiveAndEnabled && point.gameObject.scene == scene &&
                goal.minimapPointIds != null && goal.minimapPointIds.Contains(point.stepId)) targets.Add(point.transform);
        if (targets.Count == 0) caption = "目的地の配置を確認してください";
    }
}
