#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// EnemyChaser2Dの既存ロジックをなるべく保持して、
/// FindPlayer() と TakeDamage() の最小差分だけを追加するためのEditor。
/// </summary>
public sealed class FactionStep3EditorWindow : EditorWindow
{
    private const string TargetStart = "FACTION_STEP3_TARGET_START";
    private const string TargetEnd = "FACTION_STEP3_TARGET_END";
    private const string DamageStart = "FACTION_STEP3_DAMAGE_START";
    private const string DamageEnd = "FACTION_STEP3_DAMAGE_END";

    private string enemyChaserAssetPath = string.Empty;
    private bool guardMode;
    private Vector2 scroll;
    private string status = "STEP1とSTEP2を先に導入してください。";

    [MenuItem("Tools/2D Shooter/STEP3 - 派閥AI連携")]
    public static void Open() => GetWindow<FactionStep3EditorWindow>("派閥STEP3");

    private void OnEnable() { enemyChaserAssetPath = FindEnemyChaserScript(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("STEP3 - 派閥別 索敵・戦闘AI", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("STEP1(カルマ・友好度)、STEP2(派閥ルール)を使用します。" +
            "既存のEnemyChaser2Dを最小限パッチすることで、ジャンプ・崖回避・攻撃アニメーションを維持します。", MessageType.Info);

        GUILayout.Space(8);
        GUILayout.Label("① 既存 EnemyChaser2D.cs に連携コードを追加", EditorStyles.boldLabel);
        enemyChaserAssetPath = EditorGUILayout.TextField("EnemyChaser2D.cs のパス", enemyChaserAssetPath);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("スクリプトを自動検出"))
            enemyChaserAssetPath = FindEnemyChaserScript();
        if (GUILayout.Button("検査"))
        {
            status = InspectPatch(out string _);
            Repaint();
        }
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("導入時に元ファイルをプロジェクト直下の Library/FactionStep3Backups/ へ保存します。" +
            "Assets内にバックアップ用.csを作らないため、クラス重複エラーを防げます。", MessageType.None);
        if (GUILayout.Button("バックアップして EnemyChaser2D へ連携コードを追加", GUILayout.Height(35)))
        {
            if (EditorUtility.DisplayDialog("STEP3のスクリプト更新", "EnemyChaser2D.csの対象メソッドに差分を追加します。バックアップを作成しますか？", "実行する", "キャンセル"))
                status = ApplyPatch();
        }

        GUILayout.Space(12);
        GUILayout.Label("② 派閥AIを選択中のNPC/Enemy Prefabへ一括追加", EditorStyles.boldLabel);
        guardMode = EditorGUILayout.ToggleLeft("移動しないNPC用：FactionGuardCombat2Dも追加する", guardMode);
        EditorGUILayout.HelpBox("対象はHierarchyで選択したシーンObject、またはProjectで選択したPrefab。" +
            "先にSTEP2でFactionMember2Dと派閥IDを設定してください。" +
            "EnemyChaser2Dが付いている場合は、Guardを同時追加できません。", MessageType.Info);
        EditorGUILayout.LabelField("選択数", Selection.gameObjects.Length.ToString());
        if (GUILayout.Button("選択したPrefab/オブジェクトへ派閥索敵を一括設定", GUILayout.Height(32)))
            status = ApplyComponentsToSelection();

        GUILayout.Space(12);
        GUILayout.Label("③ 現在のSceneとスクリプトの確認", EditorStyles.boldLabel);
        if (GUILayout.Button("Sceneの派閥AIと既存コードの診断", GUILayout.Height(28)))
            status = CheckScene();
        EditorGUILayout.HelpBox("プレイヤーにもFactionMember2D(player)が必要です。" +
            "STEP3はEnemyChaser2D / FactionGuardCombat2Dの近接攻撃に対応。" +
            "蜘蛛や独自飛び道具AIなど、別の攻撃スクリプトは個別の連携が必要です。", MessageType.Warning);

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private static string FindEnemyChaserScript()
    {
        string[] guids = AssetDatabase.FindAssets("EnemyChaser2D t:MonoScript");
        List<string> matches = new List<string>();
        foreach (string guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(p).Equals("EnemyChaser2D.cs", StringComparison.OrdinalIgnoreCase))
                matches.Add(p);
        }
        return matches.Count == 1 ? matches[0] : string.Empty;
    }

    private string InspectPatch(out string original)
    {
        original = null;
        string path = enemyChaserAssetPath.Replace('\\', '/').Trim();
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) ||
            !Path.GetFileName(path).Equals("EnemyChaser2D.cs", StringComparison.OrdinalIgnoreCase))
            return "エラー: Assets/.../EnemyChaser2D.cs のパスを指定してください。";

        string absolute = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), path));
        string assetRoot = Path.GetFullPath(Application.dataPath) + Path.DirectorySeparatorChar;
        if (!absolute.StartsWith(assetRoot, StringComparison.OrdinalIgnoreCase) || !File.Exists(absolute))
            return "エラー: Assets以下の該当C#が見つかりません。";
        original = File.ReadAllText(absolute);

        if (!Regex.IsMatch(original, @"\bclass\s+EnemyChaser2D\b"))
            return "エラー: EnemyChaser2Dクラスが見つかりません。";
        bool hasTarget = original.Contains(TargetStart) && original.Contains(TargetEnd);
        bool hasDamage = original.Contains(DamageStart) && original.Contains(DamageEnd);
        if (hasTarget != hasDamage || original.Contains(TargetStart) != original.Contains(TargetEnd) ||
            original.Contains(DamageStart) != original.Contains(DamageEnd))
            return "エラー: STEP3のコードが途中まで挿入されています。元ファイルと比較して手動修正が必要です。";
        if (hasTarget && hasDamage) return "導入済み: STEP3の連携コードが両方見つかりました。再適用は不要です。";
        if (Regex.Matches(original, @"\bprivate\s+void\s+FindPlayer\s*\(\s*\)\s*\{").Count != 1)
            return "エラー: FindPlayer()の構造が想定と異なります。EnemyChaser2D.csを確認してください。";
        if (Regex.Matches(original, @"\bplayerHealth\s*\.\s*TakeDamage\s*\(\s*attackDamage\s*\)\s*;").Count != 1)
            return "エラー: 攻撃ダメージ箇所を特定できません。既存スクリプトの仕様を確認してください。";
        foreach (string required in new[] { "playerTransform", "playerHealth", "hasDetectedPlayer", "detectionDistance", "loseTargetDistance", "ClearPathBlock", "failedJumpAttempts", "isRecoveringFromWallHit" })
            if (!original.Contains(required)) return "エラー: 必要なフィールドが見つかりません: " + required;
        return "連携可能: FindPlayer()とTakeDamage()の挿入位置を確認しました。";
    }

    private string ApplyPatch()
    {
        string check = InspectPatch(out string original);
        if (!check.StartsWith("連携可能")) return check;

        string nl = original.Contains("\r\n") ? "\r\n" : "\n";
        string targetCode = @"
        // FACTION_STEP3_TARGET_START
        FactionEnemyTargeting2D factionStep3 = GetComponent<FactionEnemyTargeting2D>();
        if (factionStep3 != null && factionStep3.isActiveAndEnabled)
        {
            Transform previousFactionTarget = playerTransform;
            factionStep3.ResolveTarget(playerTransform, detectionDistance,
                Mathf.Max(detectionDistance, loseTargetDistance),
                out playerTransform, out playerHealth);
            if (previousFactionTarget != playerTransform)
            {
                hasDetectedPlayer = false;
                ClearPathBlock();
                failedJumpAttempts = 0;
                isRecoveringFromWallHit = false;
            }
            return;
        }
        // FACTION_STEP3_TARGET_END".Replace("\n", nl);

        Match find = Regex.Match(original, @"\bprivate\s+void\s+FindPlayer\s*\(\s*\)\s*\{");
        string updated = original.Insert(find.Index + find.Length, targetCode);

        string damageCode = @"// FACTION_STEP3_DAMAGE_START
        FactionEnemyTargeting2D factionStep3Attacker = GetComponent<FactionEnemyTargeting2D>();
        if (factionStep3Attacker != null && factionStep3Attacker.isActiveAndEnabled)
            FactionCombatDamage2D.DealDamage(playerHealth, attackDamage, transform);
        else
            playerHealth.TakeDamage(attackDamage);
        // FACTION_STEP3_DAMAGE_END".Replace("\n", nl);
        updated = Regex.Replace(updated,
            @"\bplayerHealth\s*\.\s*TakeDamage\s*\(\s*attackDamage\s*\)\s*;",
            m => damageCode, RegexOptions.None, TimeSpan.FromSeconds(2));

        string relative = enemyChaserAssetPath.Replace('\\', '/');
        string absolute = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(Application.dataPath), relative));
        string backupDir = Path.Combine(Path.GetDirectoryName(Application.dataPath), "Library", "FactionStep3Backups");
        try
        {
            Directory.CreateDirectory(backupDir);
            string backupPath = Path.Combine(backupDir,
                "EnemyChaser2D_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".cs.bak");
            File.Copy(absolute, backupPath, false);
            // Unityのコンパイル対象にtmpを入れない。
            string temp = Path.Combine(backupDir, "EnemyChaser2D_edit.tmp");
            File.WriteAllText(temp, updated, new UTF8Encoding(false));
            File.Copy(temp, absolute, true);
            File.Delete(temp);
            AssetDatabase.ImportAsset(relative, ImportAssetOptions.ForceUpdate);
            return "成功: 追跡対象と攻撃元通知の差分を追加しました。バックアップ: " + backupPath;
        }
        catch (Exception ex)
        {
            return "更新に失敗しました。バックアップとConsoleを確認してください: " + ex.Message;
        }
    }

    private string ApplyComponentsToSelection()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected.Length == 0) return "HierarchyまたはProjectでPrefabを選択してください。";
        int updated = 0, skipped = 0;
        foreach (GameObject go in selected)
        {
            if (go == null) continue;
            string assetPath = AssetDatabase.GetAssetPath(go);
            bool isPrefab = !string.IsNullOrEmpty(assetPath) &&
                PrefabUtility.IsPartOfPrefabAsset(go) && assetPath.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase);
            GameObject editRoot = null;
            GameObject target = go;
            try
            {
                if (isPrefab)
                {
                    editRoot = PrefabUtility.LoadPrefabContents(assetPath);
                    target = editRoot;
                }
                FactionMember2D member = target.GetComponent<FactionMember2D>();
                if (member == null || string.IsNullOrEmpty(member.FactionId) || member.IsPlayer)
                {
                    Debug.LogWarning("[Faction STEP3] 派閥が未設定のためスキップ: " + go.name, go);
                    skipped++;
                    continue;
                }
                if (target.GetComponent<CharacterHealth>() == null)
                {
                    Debug.LogWarning("[Faction STEP3] CharacterHealthがないためスキップ: " + go.name, go);
                    skipped++;
                    continue;
                }
                EnemyChaser2D chaser = target.GetComponent<EnemyChaser2D>();
                if (guardMode && chaser != null)
                {
                    Debug.LogWarning("[Faction STEP3] EnemyChaser2DへGuardは同時追加できません: " + go.name, go);
                    skipped++;
                    continue;
                }
                if (chaser == null && !guardMode)
                {
                    Debug.LogWarning("[Faction STEP3] EnemyChaser2Dがない場合はGuardモードをONに: " + go.name, go);
                    skipped++;
                    continue;
                }
                bool changed = false;
                if (target.GetComponent<FactionEnemyTargeting2D>() == null)
                {
                    if (isPrefab) target.AddComponent<FactionEnemyTargeting2D>();
                    else Undo.AddComponent<FactionEnemyTargeting2D>(target);
                    changed = true;
                }
                if (guardMode && target.GetComponent<FactionGuardCombat2D>() == null)
                {
                    if (isPrefab) target.AddComponent<FactionGuardCombat2D>();
                    else Undo.AddComponent<FactionGuardCombat2D>(target);
                    changed = true;
                }
                if (!changed) { skipped++; continue; }
                if (isPrefab) PrefabUtility.SaveAsPrefabAsset(editRoot, assetPath);
                else { EditorUtility.SetDirty(target); EditorSceneManager.MarkSceneDirty(target.scene); }
                updated++;
            }
            catch (Exception e)
            {
                skipped++;
                Debug.LogError("[Faction STEP3] " + go.name + ": " + e.Message, go);
            }
            finally
            {
                if (editRoot != null) PrefabUtility.UnloadPrefabContents(editRoot);
            }
        }
        AssetDatabase.SaveAssets();
        return "一括設定完了: 追加 " + updated + " / スキップ " + skipped + "。Consoleも確認してください。";
    }

    private string CheckScene()
    {
        int seekers = 0, withoutFaction = 0, withoutHealth = 0, mismatch = 0;
        foreach (FactionEnemyTargeting2D s in FindObjectsByType<FactionEnemyTargeting2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (s.gameObject.scene != SceneManager.GetActiveScene()) continue;
            seekers++;
            FactionMember2D f = s.GetComponent<FactionMember2D>();
            if (f == null || string.IsNullOrEmpty(f.FactionId)) withoutFaction++;
            if (s.GetComponent<CharacterHealth>() == null) withoutHealth++;
            if (s.GetComponent<EnemyChaser2D>() == null && s.GetComponent<FactionGuardCombat2D>() == null) mismatch++;
        }
        int players = 0;
        foreach (FactionMember2D f in FindObjectsByType<FactionMember2D>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (f.IsPlayer && f.gameObject.scene == SceneManager.GetActiveScene()) players++;
        string patchState = InspectPatch(out string _);
        string report = "Scene派閥AI: " + seekers + "体 / 派閥未設定=" + withoutFaction +
            " / HP不足=" + withoutHealth + " / 攻撃AI不足=" + mismatch +
            " / Player派閥=" + players + "体\nEnemyChaser連携: " + patchState;
        Debug.Log("[Faction STEP3 診断] " + report);
        return report;
    }
}
#endif
