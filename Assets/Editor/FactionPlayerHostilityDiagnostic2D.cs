#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// STEP2 v1.1.1: Playerへの敵対が失われたときの診断ツール。
/// NPC対NPCルールは変更しない。Playerルールの復元も明示的な確認後のみ。
/// </summary>
public sealed class FactionPlayerHostilityDiagnostic2D : EditorWindow
{
    private const string RulesAssetPath = "Assets/Resources/FactionRules/DefaultFactionRules2D.asset";
    private static readonly string[] FactionIds =
    {
        FactionId2D.Monsters,
        FactionId2D.Bandits,
        FactionId2D.Adventurers,
        FactionId2D.Dwarves
    };
    private static readonly FactionPlayerAttitude2D[] SuggestedModes =
    {
        FactionPlayerAttitude2D.AlwaysHostile,
        FactionPlayerAttitude2D.HostileWhenReputationBelow,
        FactionPlayerAttitude2D.HostileWhenKarmaAtOrBelow,
        FactionPlayerAttitude2D.Neutral
    };
    private static readonly int[] SuggestedThresholds = { 0, 40, -30, 0 };

    private int testKarma;
    private int testReputation;
    private Vector2 scroll;
    private string status = "診断待ち";

    [MenuItem("Tools/2D Shooter/STEP2 - Player敵対診断・復旧")]
    private static void Open()
    {
        GetWindow<FactionPlayerHostilityDiagnostic2D>("Player敵対診断");
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("STEP2 v1.1.1 - Player敵対診断", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "NPC対NPCルールの複数選択と、Playerへの敵対判定は別管理です。" +
            "このツールはPlayer用ルールを点検します。" +
            "『復元』を明示的に押すまでは既存のルールアセットを変更しません。",
            MessageType.Info);

        FactionRules2D rules = AssetDatabase.LoadAssetAtPath<FactionRules2D>(RulesAssetPath);
        if (rules == null)
        {
            EditorGUILayout.HelpBox(
                "指定パスにルールアセットがありません。STEP2のルールエディターで" +
                "初期設定のルールアセットを作成 / 選択してください。\n" + RulesAssetPath,
                MessageType.Warning);
            EditorGUILayout.EndScrollView();
            return;
        }

        EditorGUILayout.ObjectField("ゲームが読むルール", rules, typeof(FactionRules2D), false);
        EditorGUILayout.LabelField("NPC対NPCルール", rules.NpcRules.Count + " 件（このツールでは変更しません）");

        int runtimeKarma = testKarma;
        PlayerKarmaReputation2D state = Application.isPlaying
            ? PlayerKarmaReputation2D.GetCurrent()
            : null;
        if (state != null)
        {
            runtimeKarma = state.Karma;
            EditorGUILayout.HelpBox("Playモードの実際のカルマ・友好度で判定しています。", MessageType.None);
        }
        else
        {
            EditorGUILayout.HelpBox("Editモードでは仮の数値で判定します。", MessageType.None);
            testKarma = EditorGUILayout.IntSlider("仮カルマ", testKarma, -100, 100);
            testReputation = EditorGUILayout.IntSlider("仮友好度（各派閥共通）", testReputation, -100, 100);
        }

        EditorGUILayout.Space(6);
        EditorGUILayout.LabelField("対プレイヤー判定結果", EditorStyles.boldLabel);
        int missingOrDuplicated = 0;
        foreach (string id in FactionIds)
        {
            int count = CountPlayerRuleEntries(rules, id);
            int rep = state != null ? state.GetReputation(id) : testReputation;
            FactionDecision2D d = FactionHostility2D.EvaluateToPlayer(id, runtimeKarma, rep, rules);
            string title = FactionId2D.DisplayName(id) + " (" + id + ")";
            EditorGUILayout.BeginVertical(EditorStyles.helpBox);
            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            EditorGUILayout.LabelField("Playerへの判定", d.IsHostile ? "敵対する" : "中立（攻撃しない）");
            EditorGUILayout.LabelField("根拠", d.Reason, EditorStyles.wordWrappedLabel);
            EditorGUILayout.LabelField("Player用ルール件数", count.ToString());
            if (count == 0)
            {
                missingOrDuplicated++;
                EditorGUILayout.HelpBox("Player用の設定がありません。アセットがあると未設定は中立扱いです。", MessageType.Warning);
            }
            else if (count > 1)
            {
                missingOrDuplicated++;
                EditorGUILayout.HelpBox("同じIDの設定が重複しています。最後のルールが採用されます。", MessageType.Warning);
            }
            EditorGUILayout.EndVertical();
        }

        if (Application.isPlaying)
            DrawSceneReferences(rules);

        EditorGUILayout.Space(7);
        if (GUILayout.Button("Consoleへ診断結果を出力", GUILayout.Height(28)))
        {
            foreach (string id in FactionIds)
            {
                int rep = state != null ? state.GetReputation(id) : testReputation;
                FactionDecision2D d = FactionHostility2D.EvaluateToPlayer(id, runtimeKarma, rep, rules);
                Debug.Log("[Player敵対診断] " + id + " -> player: " +
                    (d.IsHostile ? "敵対" : "中立") + ", " + d.Reason +
                    ", PlayerRule件数=" + CountPlayerRuleEntries(rules, id));
            }
            status = "Consoleへ出力しました。";
        }

        if (missingOrDuplicated > 0)
            EditorGUILayout.HelpBox("Playerルールの欠落・重複が " + missingOrDuplicated + " 派閥に見つかりました。", MessageType.Warning);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("復旧（任意・既存設定に注意）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "以下のボタンはPlayerへの4派閥の態度のみ推奨値へ戻します。" +
            "変更済みのカルマ閾値・盗賊友好度も初期値に戻るため、実行前に確認してください。" +
            "NPC対NPCルールは残ります。バックアップを作成してから変更します。",
            MessageType.Warning);
        if (GUILayout.Button("Player判定ルールだけ推奨値に戻す（バックアップあり）", GUILayout.Height(32)))
        {
            if (EditorUtility.DisplayDialog("Playerルールのみを復旧",
                "既存のPlayer判定ルール（カルマ・友好度の閾値を含む）を初期値へ戻します。\n" +
                "NPC対NPCルールは一切変更しません。事前にバックアップを作ります。\n実行しますか？",
                "バックアップして復旧", "キャンセル"))
            {
                RestorePlayerRulesOnly(rules);
            }
        }
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private static int CountPlayerRuleEntries(FactionRules2D rules, string id)
    {
        int count = 0;
        foreach (FactionPlayerRule2D rule in rules.PlayerRules)
        {
            if (rule != null && FactionId2D.Same(rule.FactionId, id)) count++;
        }
        return count;
    }

    private static void DrawSceneReferences(FactionRules2D rules)
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Playモード：Scene参照チェック", EditorStyles.boldLabel);
        FactionMember2D[] members = UnityEngine.Object.FindObjectsByType<FactionMember2D>(FindObjectsSortMode.None);
        int playerCount = 0;
        foreach (FactionMember2D member in members)
        {
            if (member == null || !member.IsPlayer) continue;
            playerCount++;
            CharacterHealth health = member.GetComponent<CharacterHealth>();
            if (health == null) health = member.GetComponentInChildren<CharacterHealth>();
            Collider2D collider = member.GetComponentInChildren<Collider2D>();
            EditorGUILayout.ObjectField("Player FactionMember2D", member, typeof(FactionMember2D), true);
            EditorGUILayout.LabelField("CharacterHealth", health != null ? "あり（HP=" + health.CurrentHealth + ")" : "なし");
            EditorGUILayout.LabelField("Collider2D", collider != null ? "あり" : "なし");
        }
        if (playerCount == 0)
            EditorGUILayout.HelpBox("FactionId が player のFactionMember2DがSceneで見つかりません。", MessageType.Warning);
        if (playerCount > 1)
            EditorGUILayout.HelpBox("player所属のオブジェクトが複数あります。Prefabと子Objectへの二重付与を確認してください。", MessageType.Warning);

        FactionEnemyTargeting2D[] targetings = UnityEngine.Object.FindObjectsByType<FactionEnemyTargeting2D>(FindObjectsSortMode.None);
        foreach (FactionEnemyTargeting2D targeting in targetings)
        {
            FactionMember2D faction = targeting.GetComponent<FactionMember2D>();
            if (faction == null) continue;
            FactionDecision2D toPlayer = FactionHostility2D.EvaluateToPlayer(faction);
            string current = targeting.CurrentTarget != null ? targeting.CurrentTarget.name : "対象なし";
            EditorGUILayout.LabelField(targeting.name + " (" + faction.FactionId + ")",
                "Player:" + (toPlayer.IsHostile ? "敵対" : "中立") + ", 現在の索敵:" + current);
        }
        if (targetings.Length == 0)
            EditorGUILayout.HelpBox("FactionEnemyTargeting2DがSceneで見つかりません。STEP3の付与を確認してください。", MessageType.Info);
    }

    private void RestorePlayerRulesOnly(FactionRules2D rules)
    {
        string assetPath = AssetDatabase.GetAssetPath(rules);
        if (string.IsNullOrEmpty(assetPath))
        {
            status = "ルールアセットの保存先が確認できません。";
            return;
        }
        string backupPath;
        try
        {
            AssetDatabase.SaveAssets();
            string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string backupFolder = Path.Combine(projectRoot, "FactionRuleBackups");
            Directory.CreateDirectory(backupFolder);
            backupPath = Path.Combine(backupFolder,
                "DefaultFactionRules2D_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".asset.bak");
            File.Copy(Path.GetFullPath(Path.Combine(projectRoot, assetPath)), backupPath, false);
        }
        catch (Exception ex)
        {
            status = "バックアップに失敗したため中止: " + ex.Message;
            Debug.LogError("[Player敵対診断] " + status);
            return;
        }

        Undo.RecordObject(rules, "Restore Player faction rules only");
        SerializedObject so = new SerializedObject(rules);
        so.Update();
        SerializedProperty list = so.FindProperty("playerRules");
        if (list == null || !list.isArray)
        {
            status = "playerRulesが見つからないため中止しました。バックアップ: " + backupPath;
            return;
        }
        list.ClearArray();
        for (int i = 0; i < FactionIds.Length; i++)
        {
            list.InsertArrayElementAtIndex(i);
            SerializedProperty element = list.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("FactionId").stringValue = FactionIds[i];
            element.FindPropertyRelative("Attitude").enumValueIndex = (int)SuggestedModes[i];
            element.FindPropertyRelative("Threshold").intValue = SuggestedThresholds[i];
        }
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(rules);
        AssetDatabase.SaveAssets();
        FactionHostility2D.ClearDefaultRulesCache();
        status = "Player判定ルールだけ復旧しました。NPC対NPCルールは変更していません。\n" +
                 "バックアップ: " + backupPath;
        Debug.Log("[Player敵対診断] " + status);
    }
}
#endif
