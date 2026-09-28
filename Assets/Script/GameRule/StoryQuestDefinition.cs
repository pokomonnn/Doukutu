using System;
using System.Collections.Generic;
using UnityEngine;

public enum StoryQuestType
{
    [InspectorName("メインストーリー")]
    Main = 0,

    [InspectorName("サブストーリー")]
    Sub = 1
}

public enum StoryExclusiveMode
{
    [InspectorName("排他なし")]
    None = 0,

    [InspectorName("受注中だけ他を受注不可")]
    WhileInProgress = 1,

    [InspectorName("受注したら他を永久に受注不可")]
    PermanentOnAccept = 2
}

/// <summary>
/// ストーリー進行の定義データです。
/// 実際の進行状態は StoryProgressManager / GameSessionManager が保持します。
/// </summary>
[CreateAssetMenu(
    fileName = "NewStoryQuest",
    menuName = "ストーリー/ストーリークエスト"
)]
public class StoryQuestDefinition : ScriptableObject
{
    [Header("基本情報")]
    [Tooltip("重複しない管理用IDです。例: main_01 / sub_mine_a")]
    [InspectorName("ストーリーID")]
    [SerializeField] private string storyId = "story_id";

    [Tooltip("ゲーム内やデバッグ表示で使うクエスト名です。")]
    [InspectorName("表示名")]
    [SerializeField] private string displayName = "新しいストーリークエスト";

    [Tooltip("メインストーリーかサブストーリーかを選びます。")]
    [InspectorName("ストーリー種類")]
    [SerializeField] private StoryQuestType storyType = StoryQuestType.Main;

    [TextArea(2, 6)]
    [Tooltip("ストーリークエストの説明です。")]
    [InspectorName("説明")]
    [SerializeField] private string description;

    [Tooltip("ストーリー一覧などで使う画像です。未設定でも動作します。")]
    [InspectorName("ストーリー画像")]
    [SerializeField] private Sprite storyImage;

    [Header("解放条件")]
    [Tooltip("ONなら、前提条件を満たした時点で受注可能になります。前提が何もない場合はゲーム開始時から受注可能になります。")]
    [InspectorName("条件達成時に自動解放")]
    [SerializeField] private bool automaticallyBecomeAvailable = true;

    [Tooltip("このストーリーを受注可能にするため、先に完了している必要があるストーリーです。複数設定した場合は全部の完了が必要です。")]
    [InspectorName("前提ストーリー")]
    [SerializeField] private List<StoryQuestDefinition> prerequisiteStories =
        new List<StoryQuestDefinition>();

    [Tooltip("このストーリーを受注可能にするために必要なStory Flagです。文字列がすべてONの時だけ解放されます。")]
    [InspectorName("必要Story Flag")]
    [SerializeField] private List<string> requiredStoryFlags =
        new List<string>();

    [Header("既存ミッションとの連携")]
    [Tooltip("このストーリーで使うMissionDefinition2Dです。NPC会話からこのMissionを受注すると、ストーリーも自動で進行中になります。")]
    [InspectorName("連携ミッション")]
    [SerializeField] private MissionDefinition2D linkedMission;

    [Tooltip("ONならLinked Missionが達成された時、このストーリーも自動で完了します。")]
    [InspectorName("連携ミッション達成で自動完了")]
    [SerializeField] private bool completeWhenLinkedMissionCompleted = true;

    [Header("排他サブクエスト")]
    [Tooltip("同じ文字列を持つクエストを同じ排他グループとして扱います。例: mine_choice_01。空欄なら排他なしです。")]
    [InspectorName("排他グループID")]
    [SerializeField] private string exclusiveGroupId;

    [Tooltip("同じ排他グループのクエストをどう扱うか選びます。サブクエスト分岐では『受注したら他を永久に受注不可』がおすすめです。")]
    [InspectorName("排他モード")]
    [SerializeField] private StoryExclusiveMode exclusiveMode =
        StoryExclusiveMode.None;

    [Header("完了時Story Flag")]
    [Tooltip("このストーリー完了時にONにするStory Flagです。ステージ解放・会話分岐・別クエスト解放などに利用できます。")]
    [InspectorName("完了時にONにするStory Flag")]
    [SerializeField] private List<string> storyFlagsSetOnComplete =
        new List<string>();

    public string StoryId => storyId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName.Trim();
    public StoryQuestType StoryType => storyType;
    public string Description => description ?? string.Empty;
    public Sprite StoryImage => storyImage;
    public bool AutomaticallyBecomeAvailable => automaticallyBecomeAvailable;
    public IReadOnlyList<StoryQuestDefinition> PrerequisiteStories => prerequisiteStories;
    public IReadOnlyList<string> RequiredStoryFlags => requiredStoryFlags;
    public MissionDefinition2D LinkedMission => linkedMission;
    public bool CompleteWhenLinkedMissionCompleted => completeWhenLinkedMissionCompleted;
    public string ExclusiveGroupId => exclusiveGroupId?.Trim() ?? string.Empty;
    public StoryExclusiveMode ExclusiveMode => exclusiveMode;
    public IReadOnlyList<string> StoryFlagsSetOnComplete => storyFlagsSetOnComplete;

    private void OnValidate()
    {
        storyId = storyId?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
        exclusiveGroupId = exclusiveGroupId?.Trim() ?? string.Empty;

        // Inspectorの「+」で追加した直後は、
        // Object参照はnull、文字列は空文字になります。
        // ここで削除してしまうと「+を押しても増えない」ように見えるため、
        // 空の要素は残したままにします。
        if (prerequisiteStories == null)
        {
            prerequisiteStories = new List<StoryQuestDefinition>();
        }

        // 自分自身を前提にはできません。
        // 要素自体は削除せずnullへ戻すことで、Inspector上の枠を維持します。
        for (int i = 0; i < prerequisiteStories.Count; i++)
        {
            if (prerequisiteStories[i] == this)
            {
                prerequisiteStories[i] = null;
            }
        }

        if (requiredStoryFlags == null)
        {
            requiredStoryFlags = new List<string>();
        }

        TrimStringListWithoutRemovingEmptyEntries(requiredStoryFlags);

        if (storyFlagsSetOnComplete == null)
        {
            storyFlagsSetOnComplete = new List<string>();
        }

        TrimStringListWithoutRemovingEmptyEntries(storyFlagsSetOnComplete);

        if (string.IsNullOrWhiteSpace(exclusiveGroupId))
        {
            exclusiveMode = StoryExclusiveMode.None;
        }
    }

    /// <summary>
    /// Inspectorで追加した空欄を削除せず、入力済み文字列だけ前後の空白を除去します。
    /// StoryProgressManager側ではnull/空文字を安全に無視するため、
    /// 未入力の枠が残っていてもゲーム進行には影響しません。
    /// </summary>
    private static void TrimStringListWithoutRemovingEmptyEntries(
        List<string> values)
    {
        if (values == null)
        {
            return;
        }

        for (int i = 0; i < values.Count; i++)
        {
            values[i] = values[i]?.Trim() ?? string.Empty;
        }
    }
}
