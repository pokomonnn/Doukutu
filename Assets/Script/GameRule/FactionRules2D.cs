using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>STEP2: 4派閥で共有するモンスターの種類。ドワーフはSlimeだけを攻撃可能。</summary>
public enum FactionMonsterType2D
{
    [InspectorName("未指定")]
    None = 0,
    [InspectorName("スライム")]
    Slime = 1,
    [InspectorName("スケルトン")]
    Skeleton = 2,
    [InspectorName("その他")]
    Other = 3,
    [InspectorName("全種類（ルール指定用）")]
    Any = 99
}

public enum FactionPlayerAttitude2D
{
    [InspectorName("常に中立")]
    Neutral = 0,
    [InspectorName("常に敵対")]
    AlwaysHostile = 1,
    [InspectorName("派閥友好度が閾値未満なら敵対")]
    HostileWhenReputationBelow = 2,
    [InspectorName("カルマが閾値以下なら敵対")]
    HostileWhenKarmaAtOrBelow = 3
}

[Serializable]
public class FactionPlayerRule2D
{
    [Tooltip("STEP1と同じ固定ID: monsters / bandits / adventurers / dwarves")]
    public string FactionId = string.Empty;
    public FactionPlayerAttitude2D Attitude = FactionPlayerAttitude2D.Neutral;
    [Range(-100, 100)] public int Threshold = 0;
}

[Serializable]
public class FactionNpcRule2D
{
    [Tooltip("攻撃する側の派閥ID")]
    public string AttackerFactionId = string.Empty;
    [Tooltip("攻撃される側の派閥ID")]
    public string TargetFactionId = string.Empty;
    [Tooltip("ターゲットがmonstersの場合のみ有効。Anyは全種別に一致")]
    public FactionMonsterType2D TargetMonsterType = FactionMonsterType2D.Any;
    [Tooltip("オン: 積極的に攻撃する対象 / オフ: 攻撃しない（例外を明示）")]
    public bool IsHostile = true;
}

/// <summary>
/// プレイヤーへの態度とNPC間の非対称な敵対関係を定義する共有アセット。
/// 実際の攻撃/索敵コードにはSTEP3で接続。STEP2はルール判定のみ。
/// </summary>
[CreateAssetMenu(fileName = "DefaultFactionRules2D", menuName = "2D Shooter/派閥/派閥ルールセット")]
public sealed class FactionRules2D : ScriptableObject
{
    [SerializeField] private List<FactionPlayerRule2D> playerRules = new List<FactionPlayerRule2D>();
    [SerializeField] private List<FactionNpcRule2D> npcRules = new List<FactionNpcRule2D>();

    public IReadOnlyList<FactionPlayerRule2D> PlayerRules => playerRules;
    public IReadOnlyList<FactionNpcRule2D> NpcRules => npcRules;

    public bool TryGetPlayerRule(string factionId, out FactionPlayerRule2D rule)
    {
        rule = null;
        if (playerRules == null || string.IsNullOrWhiteSpace(factionId)) return false;
        for (int i = 0; i < playerRules.Count; i++)
        {
            FactionPlayerRule2D entry = playerRules[i];
            if (entry != null && FactionId2D.Same(entry.FactionId, factionId)) rule = entry;
        }
        return rule != null; // 重複があるときは最後のエントリを採用
    }

    public bool TryGetNpcRule(string attackerId, string targetId, FactionMonsterType2D targetMonsterType,
        out FactionNpcRule2D rule)
    {
        rule = null;
        if (npcRules == null) return false;
        int highestSpecificity = -1;
        foreach (FactionNpcRule2D entry in npcRules)
        {
            if (entry == null || !FactionId2D.Same(entry.AttackerFactionId, attackerId) ||
                !FactionId2D.Same(entry.TargetFactionId, targetId)) continue;

            int specificity = 0;
            if (FactionId2D.Same(targetId, FactionId2D.Monsters))
            {
                if (entry.TargetMonsterType == FactionMonsterType2D.Any)
                    specificity = 0;
                else if (entry.TargetMonsterType == targetMonsterType)
                    specificity = 1;
                else continue;
            }
            else if (entry.TargetMonsterType != FactionMonsterType2D.Any)
            {
                // モンスター以外に種類指定されたルールは無視する。
                continue;
            }

            if (specificity >= highestSpecificity)
            {
                rule = entry;
                highestSpecificity = specificity;
            }
        }
        return rule != null;
    }

    /// <summary>エディターから明示的に実行する。アセットを開いただけでは設定を上書きしない。</summary>
    public void ResetToSuggestedDefaults()
    {
        playerRules = new List<FactionPlayerRule2D>
        {
            new FactionPlayerRule2D { FactionId = FactionId2D.Monsters,
                Attitude = FactionPlayerAttitude2D.AlwaysHostile },
            new FactionPlayerRule2D { FactionId = FactionId2D.Bandits,
                Attitude = FactionPlayerAttitude2D.HostileWhenReputationBelow, Threshold = 40 },
            new FactionPlayerRule2D { FactionId = FactionId2D.Adventurers,
                Attitude = FactionPlayerAttitude2D.HostileWhenKarmaAtOrBelow, Threshold = -30 },
            new FactionPlayerRule2D { FactionId = FactionId2D.Dwarves,
                Attitude = FactionPlayerAttitude2D.Neutral }
        };
        npcRules = new List<FactionNpcRule2D>
        {
            new FactionNpcRule2D { AttackerFactionId = FactionId2D.Monsters,
                TargetFactionId = FactionId2D.Bandits, IsHostile = true },
            new FactionNpcRule2D { AttackerFactionId = FactionId2D.Monsters,
                TargetFactionId = FactionId2D.Adventurers, IsHostile = true },
            new FactionNpcRule2D { AttackerFactionId = FactionId2D.Dwarves,
                TargetFactionId = FactionId2D.Monsters, TargetMonsterType = FactionMonsterType2D.Slime,
                IsHostile = true },
            new FactionNpcRule2D { AttackerFactionId = FactionId2D.Dwarves,
                TargetFactionId = FactionId2D.Bandits, IsHostile = true }
        };
    }
}

/// <summary>セーブと共通の派閥ID。変更するとSTEP1の友好度との対応が崩れる。</summary>
public static class FactionId2D
{
    public const string Player = "player";
    public const string Monsters = PlayerKarmaReputation2D.MonstersId;
    public const string Bandits = PlayerKarmaReputation2D.BanditsId;
    public const string Adventurers = PlayerKarmaReputation2D.AdventurersId;
    public const string Dwarves = PlayerKarmaReputation2D.DwarvesId;

    public static string Normalize(string id) => string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim().ToLowerInvariant();
    public static bool Same(string a, string b) => string.Equals(Normalize(a), Normalize(b), StringComparison.Ordinal);
    public static string DisplayName(string id)
    {
        switch (Normalize(id))
        {
            case Player: return "プレイヤー";
            case Monsters: return "スライム・スケルトン";
            case Bandits: return "盗賊";
            case Adventurers: return "冒険者";
            case Dwarves: return "ドワーフ";
            default: return string.IsNullOrWhiteSpace(id) ? "未設定" : id;
        }
    }
}
