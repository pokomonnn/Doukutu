using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName="Story/Integrated campaign")]
public sealed class StoryCampaignDatabase : ScriptableObject
{
    public List<StoryCampaignQuest> quests = new List<StoryCampaignQuest>();
    public List<StoryCampaignStep> steps = new List<StoryCampaignStep>();
    public string[] stageScenes = {"Town_Main", "stage1", "Doukutu_2", "Doukutu_3", "Doukutu_4"};
    public int endingReward = 500;
}
[Serializable] public sealed class StoryCampaignQuest
{
    public string id;
    public StoryQuestDefinition story;
    public MissionDefinition2D mission;
}
[Serializable] public sealed class StoryCampaignStep
{
    public string id, quest, title;
    public int stage, reward;
    public string[] requires, flags;
    public string[] blockedBy;
    public ItemData grantItem;
    public ItemData[] consumeItems;
    public DialogueData2D dialogue;
}
