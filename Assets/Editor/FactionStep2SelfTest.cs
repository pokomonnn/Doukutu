#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

/// <summary>STEP2判定APIの主要シナリオを確認。ゲームや本セーブを変更しない。</summary>
public static class FactionStep2SelfTest
{
    [MenuItem("Tools/2D Shooter/STEP2 - ルールの自己テスト")]
    public static void Run()
    {
        FactionRules2D rules = ScriptableObject.CreateInstance<FactionRules2D>();
        rules.ResetToSuggestedDefaults();
        int passed = 0;
        int failed = 0;
        try
        {
            Check("MonsterはPlayerに常に敵対", true,
                FactionHostility2D.EvaluateToPlayer(FactionId2D.Monsters, 100, 100, rules).IsHostile,
                ref passed, ref failed);
            Check("盗賊rep=0敵対", true,
                FactionHostility2D.EvaluateToPlayer(FactionId2D.Bandits, 100, 0, rules).IsHostile,
                ref passed, ref failed);
            Check("盗賊rep=39敵対", true,
                FactionHostility2D.EvaluateToPlayer(FactionId2D.Bandits, -100, 39, rules).IsHostile,
                ref passed, ref failed);
            Check("盗賊rep=40中立、カルマは無視", false,
                FactionHostility2D.EvaluateToPlayer(FactionId2D.Bandits, -100, 40, rules).IsHostile,
                ref passed, ref failed);
            Check("冒険者カルマ-30敵対", true,
                FactionHostility2D.EvaluateToPlayer(FactionId2D.Adventurers, -30, 100, rules).IsHostile,
                ref passed, ref failed);
            Check("冒険者カルマ-29中立", false,
                FactionHostility2D.EvaluateToPlayer(FactionId2D.Adventurers, -29, -100, rules).IsHostile,
                ref passed, ref failed);
            Check("ドワーフはPlayerに中立", false,
                FactionHostility2D.EvaluateToPlayer(FactionId2D.Dwarves, -100, -100, rules).IsHostile,
                ref passed, ref failed);
            Check("モンスターは盗賊を攻撃", true,
                FactionHostility2D.EvaluateNpcToNpc(FactionId2D.Monsters, FactionId2D.Bandits,
                    FactionMonsterType2D.None, rules).IsHostile, ref passed, ref failed);
            Check("モンスターは冒険者を攻撃", true,
                FactionHostility2D.EvaluateNpcToNpc(FactionId2D.Monsters, FactionId2D.Adventurers,
                    FactionMonsterType2D.None, rules).IsHostile, ref passed, ref failed);
            Check("モンスターはドワーフを標準では攻撃しない", false,
                FactionHostility2D.EvaluateNpcToNpc(FactionId2D.Monsters, FactionId2D.Dwarves,
                    FactionMonsterType2D.None, rules).IsHostile, ref passed, ref failed);
            Check("ドワーフはスライムを攻撃", true,
                FactionHostility2D.EvaluateNpcToNpc(FactionId2D.Dwarves, FactionId2D.Monsters,
                    FactionMonsterType2D.Slime, rules).IsHostile, ref passed, ref failed);
            Check("ドワーフはスケルトンを攻撃しない", false,
                FactionHostility2D.EvaluateNpcToNpc(FactionId2D.Dwarves, FactionId2D.Monsters,
                    FactionMonsterType2D.Skeleton, rules).IsHostile, ref passed, ref failed);
            Check("ドワーフは盗賊を攻撃", true,
                FactionHostility2D.EvaluateNpcToNpc(FactionId2D.Dwarves, FactionId2D.Bandits,
                    FactionMonsterType2D.None, rules).IsHostile, ref passed, ref failed);
            Check("盗賊はデフォルトでモンスターを攻撃しない（非対称）", false,
                FactionHostility2D.EvaluateNpcToNpc(FactionId2D.Bandits, FactionId2D.Monsters,
                    FactionMonsterType2D.Slime, rules).IsHostile, ref passed, ref failed);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(rules);
        }
        if (failed == 0) Debug.Log("[派閥STEP2] 判定テスト全件成功: " + passed + "件 / 0件失敗");
        else Debug.LogError("[派閥STEP2] 判定テスト失敗: " + passed + "件成功 / " + failed + "件失敗");
    }

    private static void Check(string label, bool expected, bool actual, ref int passed, ref int failed)
    {
        if (expected == actual) passed++;
        else
        {
            failed++;
            Debug.LogError("[派閥STEP2] " + label + " 期待=" + expected + " 実際=" + actual);
        }
    }
}
#endif
