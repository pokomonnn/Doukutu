using System;

/// <summary>
/// ストーリークエストの進行状態です。
/// LockedOut は排他クエストを選んだ結果、永久に受注不可になった状態です。
/// </summary>
public enum StoryQuestState
{
    Locked = 0,
    Available = 1,
    InProgress = 2,
    Completed = 3,
    LockedOut = 4
}

/// <summary>
/// シーンをまたいで保持するストーリークエスト1件分の状態です。
/// </summary>
[Serializable]
public class StorySessionData
{
    public StoryQuestDefinition Story;
    public string StoryId = string.Empty;
    public string DisplayName = string.Empty;
    public StoryQuestState State = StoryQuestState.Locked;
}
