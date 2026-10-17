#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>一時アセット上で複数対象の登録・上書き・削除・種類指定を確認する。</summary>
public static class FactionStep2MultiTargetSelfTest
{
    [MenuItem("Tools/2D Shooter/STEP2 v1.1 - 複数対象の自己テスト")]
    public static void Run()
    {
        FactionRules2D rules = ScriptableObject.CreateInstance<FactionRules2D>();
        int ok = 0, ng = 0;
        try
        {
            rules.ResetToSuggestedDefaults();
            Check("初期のスライム攻撃", true, Hostile(rules, FactionId2D.Dwarves,
                FactionId2D.Monsters, FactionMonsterType2D.Slime), ref ok, ref ng);
            Check("初期のスケルトン中立", false, Hostile(rules, FactionId2D.Dwarves,
                FactionId2D.Monsters, FactionMonsterType2D.Skeleton), ref ok, ref ng);

            FactionMultiTargetBatch2D.Apply(rules, FactionId2D.Bandits,
                new List<string> { FactionId2D.Adventurers, FactionId2D.Dwarves },
                FactionMonsterType2D.Any, FactionBatchOperation2D.Hostile);
            Check("盗賊→冒険者: 一括追加", true, Hostile(rules, FactionId2D.Bandits,
                FactionId2D.Adventurers), ref ok, ref ng);
            Check("盗賊→ドワーフ: 一括追加", true, Hostile(rules, FactionId2D.Bandits,
                FactionId2D.Dwarves), ref ok, ref ng);
            Check("逆向きは中立のまま", false, Hostile(rules, FactionId2D.Dwarves,
                FactionId2D.Adventurers), ref ok, ref ng);

            FactionMultiTargetBatch2D.Apply(rules, FactionId2D.Bandits,
                new List<string> { FactionId2D.Dwarves }, FactionMonsterType2D.Any,
                FactionBatchOperation2D.Neutral);
            Check("指定ペアだけ非攻撃に変更", false, Hostile(rules, FactionId2D.Bandits,
                FactionId2D.Dwarves), ref ok, ref ng);
            Check("他の対象はそのまま", true, Hostile(rules, FactionId2D.Bandits,
                FactionId2D.Adventurers), ref ok, ref ng);

            FactionMultiTargetBatch2D.Apply(rules, FactionId2D.Dwarves,
                new List<string> { FactionId2D.Monsters }, FactionMonsterType2D.Skeleton,
                FactionBatchOperation2D.Hostile);
            Check("ドワーフ→スケルトンのみ追加", true, Hostile(rules, FactionId2D.Dwarves,
                FactionId2D.Monsters, FactionMonsterType2D.Skeleton), ref ok, ref ng);
            Check("スライムの既存ルール維持", true, Hostile(rules, FactionId2D.Dwarves,
                FactionId2D.Monsters, FactionMonsterType2D.Slime), ref ok, ref ng);

            FactionMultiTargetBatch2D.Apply(rules, FactionId2D.Dwarves,
                new List<string> { FactionId2D.Monsters }, FactionMonsterType2D.Skeleton,
                FactionBatchOperation2D.Delete);
            Check("スケルトンルールだけ削除", false, Hostile(rules, FactionId2D.Dwarves,
                FactionId2D.Monsters, FactionMonsterType2D.Skeleton), ref ok, ref ng);
            Check("スライムの既存ルール再確認", true, Hostile(rules, FactionId2D.Dwarves,
                FactionId2D.Monsters, FactionMonsterType2D.Slime), ref ok, ref ng);

            FactionMultiTargetBatch2D.Apply(rules, FactionId2D.Bandits,
                new List<string> { FactionId2D.Adventurers, FactionId2D.Adventurers },
                FactionMonsterType2D.Any, FactionBatchOperation2D.Hostile);
            int duplicates = 0;
            foreach (FactionNpcRule2D rule in rules.NpcRules)
                if (FactionId2D.Same(rule.AttackerFactionId, FactionId2D.Bandits) &&
                    FactionId2D.Same(rule.TargetFactionId, FactionId2D.Adventurers) &&
                    rule.TargetMonsterType == FactionMonsterType2D.Any) duplicates++;
            Check("同一ペアの重複防止", true, duplicates == 1, ref ok, ref ng);

            FactionMultiTargetBatch2D.Apply(rules, FactionId2D.Bandits,
                new List<string> { "knights" }, FactionMonsterType2D.Any,
                FactionBatchOperation2D.Hostile);
            Check("カスタム派閥IDを登録", true, Hostile(rules,
                FactionId2D.Bandits, "knights"), ref ok, ref ng);

            Check("プレイヤーとの敵対設定は不変", true,
                FactionHostility2D.EvaluateToPlayer(FactionId2D.Bandits, 0, 0, rules).IsHostile,
                ref ok, ref ng);
        }
        catch (Exception ex)
        {
            ng++;
            Debug.LogError("[STEP2 v1.1] 例外: " + ex);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rules);
        }
        if (ng == 0) Debug.Log("[STEP2 v1.1] 複数選択テスト全件成功: " + ok + "件");
        else Debug.LogError("[STEP2 v1.1] 複数選択テスト失敗: " + ok + "件成功 / " + ng + "件失敗");
    }

    private static bool Hostile(FactionRules2D rules, string attacker, string target,
        FactionMonsterType2D type = FactionMonsterType2D.None) =>
        FactionHostility2D.EvaluateNpcToNpc(attacker, target, type, rules).IsHostile;

    private static void Check(string label, bool expected, bool actual, ref int ok, ref int ng)
    {
        if (expected == actual) ok++;
        else
        {
            ng++;
            Debug.LogError("[STEP2 v1.1] " + label + " / 期待=" + expected + " / 実際=" + actual);
        }
    }
}
#endif
