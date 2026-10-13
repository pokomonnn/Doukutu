#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 既存の治療スクリプト・リザルト集計スクリプトへ最小限の通知処理だけ追記します。
/// 元のファイルを丸ごと置換せず、適用前にプロジェクト直下へバックアップします。
/// </summary>
public static class RescueNpcTreatmentResultPatcher
{
    private const string TreatmentFilename = "MedicalKitTreatment2D.cs";
    private const string CollectorFilename = "ExpeditionResultCollector2D.cs";

    [MenuItem("Tools/RescueNPC/NPC治療結果をリザルトへ接続")]
    public static void Apply()
    {
        try
        {
            string treatmentPath = FindScriptAsset(TreatmentFilename);
            string collectorPath = FindScriptAsset(CollectorFilename);
            if (string.IsNullOrEmpty(treatmentPath) || string.IsNullOrEmpty(collectorPath))
                throw new InvalidOperationException("MedicalKitTreatment2D.cs または ExpeditionResultCollector2D.cs が見つかりません。");

            string originalTreatment = File.ReadAllText(treatmentPath, Encoding.UTF8);
            string originalCollector = File.ReadAllText(collectorPath, Encoding.UTF8);
            string patchedTreatment = originalTreatment;
            string patchedCollector = originalCollector;

            const string beforeMarker = "RescueNpcTreatmentResultSession2D.RecordSuccessfulTreatment";
            const string captureMarker = "RescueNpcTreatmentResultSession2D.CaptureFromElevator";
            int added = 0;

            if (!patchedTreatment.Contains(beforeMarker))
            {
                // 元々ある「使用できる時だけ効果を発生させる」コメントの直前。
                patchedTreatment = InsertBeforeUnique(patchedTreatment,
                    @"^[ \t]*//[ \t]*使えることが確認できたときだけ効果を発生させる。[ \t]*$",
                    "        // NPC治療結果：今回新たに処置した症状だけを記録する。\n" +
                    "        StatusConditionType treatmentBeforeFlags = npc != null\n" +
                    "            ? npc.StabilizedSymptoms : StatusConditionType.None;\n\n",
                    "MedicalKitTreatment2D: 治療前フラグ");
                added++;

                // 治療が成功し、アイテム消費も問題なく完了した時だけ記録。
                patchedTreatment = InsertBeforeUnique(patchedTreatment,
                    @"^[ \t]*if[ \t]*\([ \t]*useAnimation[ \t]*!=[ \t]*null[ \t]*\)[ \t]*useAnimation\.BeginConsumableUse\(data\);[ \t]*$",
                    "        // NPC治療結果：処置完了時だけ集計（途中キャンセル・失敗は対象外）。\n" +
                    "        if (npc != null)\n" +
                    "        {\n" +
                    "            StatusConditionType newlyStabilized =\n" +
                    "                npc.StabilizedSymptoms & ~treatmentBeforeFlags;\n" +
                    "            RescueNpcTreatmentResultSession2D.RecordSuccessfulTreatment(npc, newlyStabilized);\n" +
                    "        }\n\n",
                    "MedicalKitTreatment2D: 処置確定の位置");
                added++;
            }

            if (!patchedCollector.Contains(captureMarker))
            {
                patchedCollector = InsertBeforeUnique(patchedCollector,
                    @"^[ \t]*LastCollectedResult[ \t]*=[ \t]*result[ \t]*;[ \t]*$",
                    "        // エレベーター回収NPCの『今回行った治療』だけResultシーンへ引き継ぐ。\n" +
                    "        RescueNpcTreatmentResultSession2D.CaptureFromElevator(elevatorCarryReceiver, result);\n\n",
                    "ExpeditionResultCollector2D: リザルト確定の位置");
                added++;
            }

            if (added == 0)
            {
                Debug.Log("[RescueTreatment][PATCH] すでに接続されています。");
                EditorUtility.DisplayDialog("NPC治療リザルト", "すでに接続済みです。変更はありません。", "OK");
                return;
            }

            string root = Directory.GetParent(Application.dataPath).FullName;
            string backupDir = Path.Combine(root,
                "RescueTreatment_Backup_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(backupDir);
            File.WriteAllText(Path.Combine(backupDir, TreatmentFilename), originalTreatment, new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(backupDir, CollectorFilename), originalCollector, new UTF8Encoding(false));

            try
            {
                if (patchedTreatment != originalTreatment)
                    File.WriteAllText(treatmentPath, patchedTreatment, new UTF8Encoding(false));
                if (patchedCollector != originalCollector)
                    File.WriteAllText(collectorPath, patchedCollector, new UTF8Encoding(false));
            }
            catch
            {
                File.WriteAllText(treatmentPath, originalTreatment, new UTF8Encoding(false));
                File.WriteAllText(collectorPath, originalCollector, new UTF8Encoding(false));
                throw;
            }

            AssetDatabase.Refresh();
            Debug.Log("[RescueTreatment][PATCH SUCCESS] " + added +
                      "箇所を追加 / Backup=" + backupDir);
            EditorUtility.DisplayDialog("NPC治療リザルト",
                "既存の治療・リザルトに接続しました。\n追加箇所：" + added +
                "\nバックアップ：" + backupDir +
                "\n\nResultシーンに治療結果用TMP_Textを作り、RescueNpcTreatmentResultUI2Dを追加してください。", "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[RescueTreatment][PATCH FAILED] " + ex);
            EditorUtility.DisplayDialog("NPC治療リザルト",
                "自動接続できませんでした。\nConsoleに詳細を表示しています。\n" +
                "失敗時は元のスクリプトに変更を加えません。", "OK");
        }
    }

    private static string FindScriptAsset(string filename)
    {
        string[] matches = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(filename) + " t:MonoScript");
        string found = null;
        foreach (string guid in matches)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!string.Equals(Path.GetFileName(path), filename, StringComparison.Ordinal)) continue;
            if (found != null)
                throw new InvalidOperationException(filename + " がAssets内に複数あります。重複ファイルを整理してください。");
            found = path;
        }
        return found;
    }

    private static string InsertBeforeUnique(string source, string pattern, string addition, string label)
    {
        MatchCollection matches = Regex.Matches(source, pattern, RegexOptions.Multiline);
        if (matches.Count != 1)
            throw new InvalidOperationException(label + "の位置が特定できません（" + matches.Count + "件）。" +
                " ご利用中のスクリプト版に合わせた手動接続が必要です。");
        return source.Insert(matches[0].Index, addition);
    }
}
#endif
