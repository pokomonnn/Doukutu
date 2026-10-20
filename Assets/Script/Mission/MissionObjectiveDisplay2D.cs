using System;
using System.Linq;
using System.Text;
using UnityEngine;

/// <summary>Presentation only. Gameplay events own the saved flags; this class never grants rewards.</summary>
[Serializable]
public class MissionObjectiveDisplay2D
{
    public string id;
    [Header("ミニマップ目的地（表示専用）")]
    public string minimapScene;
    public string[] minimapPointIds = Array.Empty<string>();
    [Tooltip("前提クエストの現在目標を案内する場合のMission ID")]
    public string minimapFollowMissionId;
    [TextArea(1,3)] public string text;
    [Tooltip("いずれかが成立すれば達成。受注前・順不同の進行も既存の保存フラグから判定します。")]
    public string[] anyOfFlags = Array.Empty<string>();
    [Tooltip("指定した場合はすべて必要です。Any Of Flagsとの併用可。")]
    public string[] allOfFlags = Array.Empty<string>();

    public bool IsComplete(Func<string,bool> has)
    {
        bool any=anyOfFlags!=null && anyOfFlags.Length>0;
        bool all=allOfFlags!=null && allOfFlags.Length>0;
        return (any||all) && (!any || anyOfFlags.Any(has)) && (!all || allOfFlags.All(has));
    }
}

public static class MissionObjectiveDisplayState
{
    static bool Has(string flag) => !string.IsNullOrWhiteSpace(flag) && GameSessionManager.Instance!=null && GameSessionManager.Instance.HasStoryFlag(flag);
    public static bool[] Snapshot(MissionDefinition2D mission)
    {
        if(mission==null || !mission.HasDisplayObjectives)return Array.Empty<bool>();
        bool finished=Has(mission.ObjectiveFinishFlag);
        return mission.DisplayObjectives.Select(o=>finished || o!=null && o.IsComplete(Has)).ToArray();
    }
    public static bool HasPending(MissionDefinition2D mission) => mission!=null && mission.HasDisplayObjectives && Snapshot(mission).Any(done=>!done);
    public static string Current(MissionDefinition2D mission)
    {
        var done=Snapshot(mission);
        for(int i=0;i<done.Length;i++)if(!done[i])return mission.DisplayObjectives[i]?.text ?? "目標が未設定です";
        return "すべての目標を達成しました";
    }
    public static string History(MissionDefinition2D mission)
    {
        if(!mission.HasDisplayObjectives)return mission.Description;
        var result=new StringBuilder(mission.Description);result.Append("\n\n目標\n");
        var done=Snapshot(mission);
        for(int i=0;i<done.Length;i++)result.Append(done[i]?"[完了] ":"[未達成] ").Append(mission.DisplayObjectives[i]?.text).Append('\n');
        return result.ToString();
    }
}
