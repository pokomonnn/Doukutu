using System;
using System.Linq;
using UnityEngine;

[Serializable] public class CampaignData
{
    public int version;
    public CampaignNode[] nodes;
    public CampaignStep[] steps;
    public string[] offers;
    public CampaignNode Node(string id) => nodes.First(n => n.id == id);
}
[Serializable] public class CampaignNode { public string id, source; public string[] lines; public CampaignChoice[] choices; }
[Serializable] public class CampaignChoice { public string label, target; }
[Serializable] public class CampaignStep
{
    public string id, quest, questTitle, title;
    public int stage, reward;
    public float x;
    public string[] requires, nodes, flags;
}

// Pure rules are shared by runtime and editor tests; rewards are applied only on commit.
public static class CampaignRules
{
    public static bool Ready(CampaignStep step, Func<string, bool> has) =>
        !has("step_" + step.id) && !has(step.quest + "_Completed") && step.requires.All(has);
    public static bool GardenOpen(Func<string, bool> has) =>
        has("S1M01_Completed") && has("S2M01_Completed") && has("S3M01_Completed");
    public static bool CanEnd(string ending, Func<string, bool> has) =>
        (ending == "A" || ending == "B" || ending == "C") &&
        has("S4M01_StorySeen") && !has("EndingCommitted") &&
        (ending != "C" || (has("faction_adventurer_support") &&
        has("faction_bandit_support") && has("faction_dwarf_support")));
}
