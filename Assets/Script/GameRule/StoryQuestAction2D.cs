using UnityEngine;

/// <summary>
/// ボタン・イベント・Timelineなどからストーリー状態を操作するための補助Componentです。
/// Missionに紐づかない会話イベント等で利用できます。
/// </summary>
[DisallowMultipleComponent]
public class StoryQuestAction2D : MonoBehaviour
{
    [Header("対象ストーリー")]
    [InspectorName("対象ストーリークエスト")]
    [SerializeField] private StoryQuestDefinition storyQuest;

    [Header("参照")]
    [Tooltip("未設定ならStoryProgressManager.Instance、またはシーン内から自動取得します。")]
    [InspectorName("ストーリー進行管理")]
    [SerializeField] private StoryProgressManager storyProgressManager;

    [Header("Story Flag（任意）")]
    [Tooltip("Set Story Flag Onを呼んだ時にONにするFlag IDです。")]
    [InspectorName("Story Flag ID")]
    [SerializeField] private string storyFlagId;

    [Header("デバッグ")]
    [InspectorName("デバッグログを表示")]
    [SerializeField] private bool showDebugLogs = true;

    public void StartStoryQuest()
    {
        FindManager();

        if (storyProgressManager == null)
        {
            LogWarning("StoryProgressManagerが見つかりません。");
            return;
        }

        bool success = storyProgressManager.TryStartQuest(
            storyQuest,
            out string message
        );

        LogResult(success, message);
    }

    public void CompleteStoryQuest()
    {
        FindManager();

        if (storyProgressManager == null)
        {
            LogWarning("StoryProgressManagerが見つかりません。");
            return;
        }

        bool success = storyProgressManager.TryCompleteQuest(
            storyQuest,
            out string message
        );

        LogResult(success, message);
    }

    public void UnlockStoryQuest()
    {
        FindManager();

        if (storyProgressManager == null)
        {
            LogWarning("StoryProgressManagerが見つかりません。");
            return;
        }

        bool success = storyProgressManager.UnlockQuest(
            storyQuest,
            out string message
        );

        LogResult(success, message);
    }

    public void SetStoryFlagOn()
    {
        FindManager();

        if (storyProgressManager == null)
        {
            LogWarning("StoryProgressManagerが見つかりません。");
            return;
        }

        bool changed = storyProgressManager.SetStoryFlag(
            storyFlagId,
            true
        );

        LogResult(
            changed,
            changed
                ? $"Story Flag『{storyFlagId}』をONにしました。"
                : $"Story Flag『{storyFlagId}』は変更されませんでした。"
        );
    }

    public void SetStoryFlagOff()
    {
        FindManager();

        if (storyProgressManager == null)
        {
            LogWarning("StoryProgressManagerが見つかりません。");
            return;
        }

        bool changed = storyProgressManager.SetStoryFlag(
            storyFlagId,
            false
        );

        LogResult(
            changed,
            changed
                ? $"Story Flag『{storyFlagId}』をOFFにしました。"
                : $"Story Flag『{storyFlagId}』は変更されませんでした。"
        );
    }

    private void FindManager()
    {
        if (storyProgressManager == null)
        {
            storyProgressManager = StoryProgressManager.Instance;
        }

        if (storyProgressManager == null)
        {
            storyProgressManager =
                FindAnyObjectByType<StoryProgressManager>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void LogResult(bool success, string message)
    {
        if (!showDebugLogs && success)
        {
            return;
        }

        if (success)
        {
            Debug.Log($"[StoryQuestAction2D] {message}", this);
        }
        else
        {
            LogWarning(message);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[StoryQuestAction2D] {message}", this);
    }

    private void OnValidate()
    {
        storyFlagId = storyFlagId?.Trim() ?? string.Empty;
    }
}
