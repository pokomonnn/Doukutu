#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// STEP2 v1.1: 複数のターゲット指定を、既存のFactionNpcRule2D (1対1)へ変換する。
/// ランタイム形式と既存ルールアセットは変更しない。
/// </summary>
public enum FactionBatchOperation2D
{
    Hostile,
    Neutral,
    Delete
}

public static class FactionMultiTargetBatch2D
{
    public static bool TryCollectTargets(string attacker, string[] builtinIds,
        bool[] checks, string extraTargets, out List<string> targets, out string error)
    {
        targets = new List<string>();
        error = string.Empty;
        string source = FactionId2D.Normalize(attacker);
        if (!ValidId(source))
        {
            error = "攻撃する側のIDは英小文字・数字・_・-で入力してください。";
            return false;
        }
        if (source == FactionId2D.Player)
        {
            error = "プレイヤーへのルールはNPC間とは別に設定してください。";
            return false;
        }
        if (builtinIds == null || checks == null || builtinIds.Length != checks.Length)
        {
            error = "対象チェック欄を読み込めません。";
            return false;
        }
        HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < builtinIds.Length; i++)
        {
            if (!checks[i]) continue;
            if (!TryAppend(builtinIds[i], source, unique, targets, out error))
                return false;
        }
        if (!string.IsNullOrWhiteSpace(extraTargets))
        {
            string[] parts = Regex.Split(extraTargets, @"[,，;；\r\n\t\s]+");
            foreach (string part in parts)
            {
                if (string.IsNullOrWhiteSpace(part)) continue;
                if (!TryAppend(part, source, unique, targets, out error))
                    return false;
            }
        }
        return true;
    }

    private static bool TryAppend(string raw, string attacker, HashSet<string> unique,
        List<string> targets, out string error)
    {
        error = string.Empty;
        string id = FactionId2D.Normalize(raw);
        if (!ValidId(id))
        {
            error = "無効な対象派閥ID: " + raw + "。英小文字・数字・_・-を使ってください。";
            return false;
        }
        if (id == FactionId2D.Player)
        {
            error = "playerはNPC対NPCルールでは選べません。対プレイヤールールを使用してください。";
            return false;
        }
        if (id == attacker)
        {
            error = "自分自身の派閥(" + id + ")は攻撃対象にできません。";
            return false;
        }
        if (unique.Add(id)) targets.Add(id);
        return true;
    }

    private static bool ValidId(string id) =>
        !string.IsNullOrEmpty(id) && Regex.IsMatch(id, @"^[a-z0-9_-]+$");

    /// <summary>
    /// 選択された各ペアの同じモンスター種別に限って既存エントリを削除し、
    /// Hostile/Neutral時は1件ずつ追加する。その他の派閥・種類は保持する。
    /// Undo対応、アセットの場合のみ自動保存。
    /// </summary>
    public static int Apply(FactionRules2D rules, string attackerId,
        IList<string> targetIds, FactionMonsterType2D monsterType,
        FactionBatchOperation2D operation)
    {
        if (rules == null) throw new ArgumentException("ルールアセットを選んでください。");
        if (targetIds == null || targetIds.Count == 0)
            throw new ArgumentException("対象派閥を1件以上選んでください。");
        string attacker = FactionId2D.Normalize(attackerId);
        if (!ValidId(attacker) || attacker == FactionId2D.Player)
            throw new ArgumentException("攻撃側IDが無効です。");
        if (monsterType == FactionMonsterType2D.None)
            throw new ArgumentException("対象モンスター種類は Any / Slime / Skeleton / Other から選んでください。");

        HashSet<string> unique = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> targets = new List<string>();
        foreach (string raw in targetIds)
        {
            string id = FactionId2D.Normalize(raw);
            if (!ValidId(id) || id == FactionId2D.Player || id == attacker)
                throw new ArgumentException("無効な攻撃対象ID: " + raw);
            if (unique.Add(id)) targets.Add(id);
        }

        Undo.RecordObject(rules, "STEP2 v1.1 - Multi Target Faction Rules");
        SerializedObject so = new SerializedObject(rules);
        so.Update();
        SerializedProperty array = so.FindProperty("npcRules");
        if (array == null || !array.isArray)
            throw new ArgumentException("FactionRules2D の npcRules が見つかりません。");

        foreach (string target in targets)
        {
            FactionMonsterType2D scopedType = FactionId2D.Same(target, FactionId2D.Monsters)
                ? monsterType : FactionMonsterType2D.Any;
            int typeIndex = EnumIndex(scopedType);
            // 同じペア+同じモンスター種類の旧ルールだけ消去。重複を残さない。
            for (int i = array.arraySize - 1; i >= 0; i--)
            {
                SerializedProperty rule = array.GetArrayElementAtIndex(i);
                if (FactionId2D.Same(rule.FindPropertyRelative("AttackerFactionId").stringValue, attacker) &&
                    FactionId2D.Same(rule.FindPropertyRelative("TargetFactionId").stringValue, target) &&
                    rule.FindPropertyRelative("TargetMonsterType").enumValueIndex == typeIndex)
                    array.DeleteArrayElementAtIndex(i);
            }
            if (operation == FactionBatchOperation2D.Delete) continue;
            int next = array.arraySize;
            array.InsertArrayElementAtIndex(next);
            SerializedProperty newRule = array.GetArrayElementAtIndex(next);
            newRule.FindPropertyRelative("AttackerFactionId").stringValue = attacker;
            newRule.FindPropertyRelative("TargetFactionId").stringValue = target;
            newRule.FindPropertyRelative("TargetMonsterType").enumValueIndex = typeIndex;
            newRule.FindPropertyRelative("IsHostile").boolValue =
                operation == FactionBatchOperation2D.Hostile;
        }

        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(rules);
        if (AssetDatabase.Contains(rules)) AssetDatabase.SaveAssets();
        return targets.Count;
    }

    private static int EnumIndex(FactionMonsterType2D value)
    {
        Array all = Enum.GetValues(typeof(FactionMonsterType2D));
        for (int i = 0; i < all.Length; i++)
            if ((FactionMonsterType2D)all.GetValue(i) == value) return i;
        throw new ArgumentException("モンスター種類が不正です: " + value);
    }
}
#endif
