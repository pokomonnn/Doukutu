using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 通常住人との会話回数を、シーンをまたいで保持します。
/// 現在は同一プレイ中の一時データです。将来のセーブ機能ではEntriesを保存対象にできます。
/// </summary>
[DisallowMultipleComponent]
public class TownConversationHistoryManager : MonoBehaviour
{
    [SerializeField]
    private List<TownConversationHistoryEntry> entries =
        new List<TownConversationHistoryEntry>();

    public static TownConversationHistoryManager Instance { get; private set; }
    public IReadOnlyList<TownConversationHistoryEntry> Entries => entries;

    private readonly Dictionary<string, TownConversationHistoryEntry> lookup =
        new Dictionary<string, TownConversationHistoryEntry>(
            StringComparer.OrdinalIgnoreCase
        );

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        RebuildLookup();

        if (transform.parent == null)
        {
            DontDestroyOnLoad(gameObject);
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    public static TownConversationHistoryManager GetOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        TownConversationHistoryManager existing =
            FindAnyObjectByType<TownConversationHistoryManager>(
                FindObjectsInactive.Include
            );

        if (existing != null)
        {
            return existing;
        }

        GameObject host = GameSessionManager.Instance != null
            ? GameSessionManager.Instance.gameObject
            : new GameObject("TownConversationHistoryManager");

        TownConversationHistoryManager manager =
            host.GetComponent<TownConversationHistoryManager>();

        if (manager == null)
        {
            manager = host.AddComponent<TownConversationHistoryManager>();
        }

        return manager;
    }

    public int GetCompletedConversationCount(string residentId)
    {
        string normalizedId = NormalizeId(residentId);

        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return 0;
        }

        EnsureLookup();

        return lookup.TryGetValue(
            normalizedId,
            out TownConversationHistoryEntry entry)
                ? Mathf.Max(0, entry.CompletedConversationCount)
                : 0;
    }

    public bool HasTalkedToResident(string residentId)
    {
        return GetCompletedConversationCount(residentId) > 0;
    }

    public int RecordConversationCompleted(string residentId)
    {
        string normalizedId = NormalizeId(residentId);

        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return 0;
        }

        EnsureLookup();

        if (!lookup.TryGetValue(
                normalizedId,
                out TownConversationHistoryEntry entry))
        {
            entry = new TownConversationHistoryEntry(
                normalizedId,
                0
            );

            entries.Add(entry);
            lookup.Add(normalizedId, entry);
        }

        entry.SetCompletedConversationCount(
            entry.CompletedConversationCount + 1
        );

        return entry.CompletedConversationCount;
    }

    public void SetCompletedConversationCount(
        string residentId,
        int count)
    {
        string normalizedId = NormalizeId(residentId);

        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return;
        }

        EnsureLookup();

        if (!lookup.TryGetValue(
                normalizedId,
                out TownConversationHistoryEntry entry))
        {
            entry = new TownConversationHistoryEntry(
                normalizedId,
                count
            );

            entries.Add(entry);
            lookup.Add(normalizedId, entry);
            return;
        }

        entry.SetCompletedConversationCount(count);
    }

    public void ClearAllHistory()
    {
        entries.Clear();
        lookup.Clear();
    }

    [ContextMenu("Clear All Conversation History")]
    private void ClearAllHistoryFromContextMenu()
    {
        ClearAllHistory();
    }

    private void EnsureLookup()
    {
        if (lookup.Count == 0 && entries.Count > 0)
        {
            RebuildLookup();
        }
    }

    private void RebuildLookup()
    {
        lookup.Clear();

        if (entries == null)
        {
            entries = new List<TownConversationHistoryEntry>();
            return;
        }

        foreach (TownConversationHistoryEntry entry in entries)
        {
            if (entry == null)
            {
                continue;
            }

            string residentId = NormalizeId(entry.ResidentId);

            if (string.IsNullOrWhiteSpace(residentId) ||
                lookup.ContainsKey(residentId))
            {
                continue;
            }

            entry.SetResidentId(residentId);
            lookup.Add(residentId, entry);
        }
    }

    private static string NormalizeId(string residentId)
    {
        return residentId?.Trim() ?? string.Empty;
    }
}

[Serializable]
public class TownConversationHistoryEntry
{
    [SerializeField] private string residentId;
    [SerializeField, Min(0)] private int completedConversationCount;

    public string ResidentId => residentId;
    public int CompletedConversationCount => completedConversationCount;

    public TownConversationHistoryEntry(
        string residentId,
        int completedConversationCount)
    {
        this.residentId = residentId?.Trim() ?? string.Empty;
        this.completedConversationCount = Mathf.Max(
            0,
            completedConversationCount
        );
    }

    public void SetResidentId(string value)
    {
        residentId = value?.Trim() ?? string.Empty;
    }

    public void SetCompletedConversationCount(int value)
    {
        completedConversationCount = Mathf.Max(0, value);
    }
}
