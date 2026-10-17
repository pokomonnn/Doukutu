using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// STEP1: プレイヤーのカルマと各派閥との友好度だけを管理する。
/// 敵対判定・自動的なカルマ加減算・攻撃AIは STEP2/3 で実装する。
/// GameSessionManager と同じ GameObject に保持し、シーンを跨いで維持する。
/// </summary>
[DisallowMultipleComponent]
public sealed class PlayerKarmaReputation2D : MonoBehaviour
{
    public const int MinValue = -100;
    public const int MaxValue = 100;

    // セーブ・クエスト・NPCとの連携時は日本語表示名ではなく固定IDを使用してください。
    public const string MonstersId = "monsters";       // スライム・スケルトン
    public const string BanditsId = "bandits";         // 盗賊
    public const string AdventurersId = "adventurers"; // 冒険者
    public const string DwarvesId = "dwarves";         // ドワーフ

    [Serializable]
    public sealed class FactionEntry
    {
        public string factionId;
        public int reputation;

        public FactionEntry(string id, int value)
        {
            factionId = id;
            reputation = value;
        }
    }

    [Header("カルマ（全体的な善悪）")]
    [Range(MinValue, MaxValue)]
    [SerializeField] private int karma;

    [Header("派閥ごとの友好度：敵対判定は STEP2 で導入")]
    [SerializeField] private List<FactionEntry> factionReputations = new List<FactionEntry>();

    public int Karma => karma;
    public IReadOnlyList<FactionEntry> FactionReputations => factionReputations;

    // 値が実際に変化したときだけ通知する。
    public event Action<int> KarmaChanged;
    public event Action<string, int> ReputationChanged;
    public event Action AllValuesRestored;

    private void Awake()
    {
        EnsureDefaults();
    }

    /// <summary>
    /// 永続管理者の GameObject から取得・作成する。
    /// Player 本体に追加するとシーン遷移で消え得るため、この方法を推奨。
    /// </summary>
    public static PlayerKarmaReputation2D GetOrCreate(GameSessionManager session)
    {
        if (session == null)
        {
            Debug.LogError("[Karma STEP1] GameSessionManager が存在しません。");
            return null;
        }

        PlayerKarmaReputation2D state = session.GetComponent<PlayerKarmaReputation2D>();
        if (state == null)
            state = session.gameObject.AddComponent<PlayerKarmaReputation2D>();
        state.EnsureDefaults();
        return state;
    }

    public static PlayerKarmaReputation2D GetCurrent()
    {
        return GetOrCreate(GameSessionManager.Instance);
    }

    /// <summary>カルマの値をそのまま設定。範囲外は -100～100 へ丸める。</summary>
    public void SetKarma(int value)
    {
        int next = Mathf.Clamp(value, MinValue, MaxValue);
        if (next == karma) return;
        karma = next;
        KarmaChanged?.Invoke(karma);
    }

    /// <summary>カルマを増減。増減後も -100～100 に丸める。</summary>
    public void ChangeKarma(int delta)
    {
        long next = (long)karma + delta;
        SetKarma(next < MinValue ? MinValue : next > MaxValue ? MaxValue : (int)next);
    }

    public int GetReputation(string factionId)
    {
        string id = NormalizeId(factionId);
        FactionEntry entry = FindEntry(id);
        return entry != null ? entry.reputation : 0;
    }

    /// <summary>派閥IDが空の場合は登録しない。未知のIDは新規追加する。</summary>
    public bool SetReputation(string factionId, int value)
    {
        string id = NormalizeId(factionId);
        if (string.IsNullOrEmpty(id)) return false;
        EnsureDefaults();

        int next = Mathf.Clamp(value, MinValue, MaxValue);
        FactionEntry entry = FindEntry(id);
        if (entry == null)
        {
            factionReputations.Add(new FactionEntry(id, next));
            ReputationChanged?.Invoke(id, next);
            return true;
        }

        if (entry.reputation != next)
        {
            entry.reputation = next;
            ReputationChanged?.Invoke(id, next);
        }
        return true;
    }

    public bool ChangeReputation(string factionId, int delta)
    {
        string id = NormalizeId(factionId);
        if (string.IsNullOrEmpty(id)) return false;
        long next = (long)GetReputation(id) + delta;
        return SetReputation(id, next < MinValue ? MinValue : next > MaxValue ? MaxValue : (int)next);
    }

    /// <summary>新規ゲーム開始時にカルマと全派閥友好度を0へ初期化。</summary>
    public void ResetToDefaults()
    {
        karma = 0;
        factionReputations = new List<FactionEntry>();
        EnsureDefaults();
        KarmaChanged?.Invoke(karma);
        foreach (FactionEntry e in factionReputations)
            ReputationChanged?.Invoke(e.factionId, e.reputation);
        AllValuesRestored?.Invoke();
    }

    /// <summary>セーブ用データを複製して返す（ScriptableObjectなどの参照を保持しない）。</summary>
    public SavedKarmaReputationData CreateSaveData()
    {
        EnsureDefaults();
        SavedKarmaReputationData save = new SavedKarmaReputationData
        {
            Karma = karma,
            Reputations = new List<SavedFactionReputationData>()
        };
        foreach (FactionEntry e in factionReputations)
        {
            if (e == null || string.IsNullOrWhiteSpace(e.factionId)) continue;
            save.Reputations.Add(new SavedFactionReputationData
            {
                FactionId = NormalizeId(e.factionId),
                Value = Mathf.Clamp(e.reputation, MinValue, MaxValue)
            });
        }
        return save;
    }

    /// <summary>
    /// ロード時は値を丸ごと入れ替える。旧セーブ(null)なら初期値0。
    /// 重複IDは先に現れたものを採用し、無効IDは無視する。
    /// </summary>
    public void RestoreFromSaveData(SavedKarmaReputationData saved)
    {
        karma = Mathf.Clamp(saved != null ? saved.Karma : 0, MinValue, MaxValue);
        factionReputations = new List<FactionEntry>();

        if (saved != null && saved.Reputations != null)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (SavedFactionReputationData entry in saved.Reputations)
            {
                if (entry == null) continue;
                string id = NormalizeId(entry.FactionId);
                if (string.IsNullOrEmpty(id) || !seen.Add(id)) continue;
                factionReputations.Add(new FactionEntry(id,
                    Mathf.Clamp(entry.Value, MinValue, MaxValue)));
            }
        }
        EnsureDefaults();
        KarmaChanged?.Invoke(karma);
        foreach (FactionEntry e in factionReputations)
            ReputationChanged?.Invoke(e.factionId, e.reputation);
        AllValuesRestored?.Invoke();
    }

    private static string NormalizeId(string raw)
    {
        return string.IsNullOrWhiteSpace(raw) ? string.Empty : raw.Trim().ToLowerInvariant();
    }

    private FactionEntry FindEntry(string normalizedId)
    {
        if (string.IsNullOrEmpty(normalizedId) || factionReputations == null) return null;
        foreach (FactionEntry entry in factionReputations)
        {
            if (entry != null && string.Equals(entry.factionId, normalizedId,
                StringComparison.OrdinalIgnoreCase)) return entry;
        }
        return null;
    }

    private void EnsureDefaults()
    {
        if (factionReputations == null) factionReputations = new List<FactionEntry>();
        AddDefault(MonstersId);
        AddDefault(BanditsId);
        AddDefault(AdventurersId);
        AddDefault(DwarvesId);
    }

    private void AddDefault(string id)
    {
        if (FindEntry(id) == null) factionReputations.Add(new FactionEntry(id, 0));
    }

    private void OnValidate()
    {
        karma = Mathf.Clamp(karma, MinValue, MaxValue);
        if (factionReputations == null) return;
        foreach (FactionEntry entry in factionReputations)
            if (entry != null) entry.reputation = Mathf.Clamp(entry.reputation, MinValue, MaxValue);
    }
}

[Serializable]
public sealed class SavedKarmaReputationData
{
    public int Karma;
    public List<SavedFactionReputationData> Reputations = new List<SavedFactionReputationData>();
}

[Serializable]
public sealed class SavedFactionReputationData
{
    public string FactionId = string.Empty;
    public int Value;
}
