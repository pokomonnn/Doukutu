using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲームで使用するストーリークエストをまとめて登録するデータベースです。
/// StoryProgressManager に1つ設定してください。
/// </summary>
[CreateAssetMenu(
    fileName = "StoryQuestDatabase",
    menuName = "ストーリー/ストーリークエストデータベース"
)]
public class StoryQuestDatabase : ScriptableObject
{
    [Header("ストーリークエスト一覧")]
    [Tooltip("メイン・サブを含め、ゲームで使用するStoryQuestDefinitionをすべて登録してください。")]
    [InspectorName("登録ストーリー一覧")]
    [SerializeField] private List<StoryQuestDefinition> stories =
        new List<StoryQuestDefinition>();

    public IReadOnlyList<StoryQuestDefinition> Stories => stories;

    public StoryQuestDefinition FindById(string storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId) || stories == null)
        {
            return null;
        }

        string target = storyId.Trim();

        foreach (StoryQuestDefinition story in stories)
        {
            if (story != null &&
                string.Equals(
                    story.StoryId,
                    target,
                    StringComparison.OrdinalIgnoreCase))
            {
                return story;
            }
        }

        return null;
    }

    public StoryQuestDefinition FindByLinkedMission(
        MissionDefinition2D mission)
    {
        if (mission == null || stories == null)
        {
            return null;
        }

        string missionId = mission.MissionId?.Trim() ?? string.Empty;

        foreach (StoryQuestDefinition story in stories)
        {
            if (story == null || story.LinkedMission == null)
            {
                continue;
            }

            if (story.LinkedMission == mission)
            {
                return story;
            }

            if (!string.IsNullOrWhiteSpace(missionId) &&
                string.Equals(
                    story.LinkedMission.MissionId,
                    missionId,
                    StringComparison.OrdinalIgnoreCase))
            {
                return story;
            }
        }

        return null;
    }

    private void OnValidate()
    {
        if (stories == null)
        {
            stories = new List<StoryQuestDefinition>();
            return;
        }

        HashSet<StoryQuestDefinition> seen =
            new HashSet<StoryQuestDefinition>();

        for (int i = stories.Count - 1; i >= 0; i--)
        {
            StoryQuestDefinition story = stories[i];

            // Inspectorで「＋」を押した直後はnull要素が作られます。
            // ここで削除すると、追加した枠が即座に消えてしまうため、
            // nullはそのまま残してInspectorから設定できるようにします。
            if (story == null)
            {
                continue;
            }

            // 同じStoryQuestDefinitionの重複登録だけを整理します。
            if (!seen.Add(story))
            {
                stories.RemoveAt(i);
            }
        }
    }
}
