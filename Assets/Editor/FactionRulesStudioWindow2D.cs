#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>STEP2 の専用Editor。既存のEnemy/NPC Studioのコードは変更しない。</summary>
public sealed class FactionRulesStudioWindow2D : EditorWindow
{
    private const string DefaultAssetPath = "Assets/Resources/FactionRules/DefaultFactionRules2D.asset";
    private static readonly string[] FactionIds =
    {
        FactionId2D.Monsters, FactionId2D.Bandits, FactionId2D.Adventurers, FactionId2D.Dwarves
    };
    private static readonly string[] FactionLabels =
    {
        "スライム・スケルトン", "盗賊", "冒険者", "ドワーフ"
    };
    private FactionRules2D ruleAsset;
    private Vector2 scroll;
    private bool editPlayerRules = true;
    private bool editNpcRules = true;
    private bool showMultiTarget = true;
    private int multiAttackerIndex = 0;
    private string multiCustomAttackerId = string.Empty;
    private string multiCustomTargetIds = string.Empty;
    private readonly bool[] multiTargetChecks = new bool[4];
    private FactionMonsterType2D multiMonsterType = FactionMonsterType2D.Any;
    private static readonly string[] MultiAttackerLabels =
    {
        "スライム・スケルトン (monsters)", "盗賊 (bandits)",
        "冒険者 (adventurers)", "ドワーフ (dwarves)", "カスタム派閥ID"
    };
    private bool showTest = true;
    private bool showBulk = true;
    private int previewAttacker = 1;
    private int previewTarget = 0;
    private FactionMonsterType2D previewSpecies = FactionMonsterType2D.Slime;
    private int previewKarma;
    private int previewReputation;
    private int bulkFactionIndex = 1;
    private FactionMonsterType2D bulkMonsterSpecies = FactionMonsterType2D.Slime;
    private bool bulkPreserveExisting = true;
    private string status = "未設定";

    [MenuItem("Tools/2D Shooter/STEP2 - 派閥ルールエディター")]
    public static void Open() => GetWindow<FactionRulesStudioWindow2D>("派閥STEP2");

    private void OnEnable()
    {
        ruleAsset = AssetDatabase.LoadAssetAtPath<FactionRules2D>(DefaultAssetPath);
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("派閥・敵対判定 STEP2", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "STEP1のカルマ・派閥友好度を参照して敵対判定するルールを管理します。\n" +
            "STEP2単体では既存の敵AIや攻撃対象は変化しません（STEP3で連携）。", MessageType.Info);
        EditorGUILayout.Space(7);
        DrawAssetHeader();
        if (ruleAsset == null)
        {
            EditorGUILayout.EndScrollView();
            return;
        }

        DrawRulesEditor();
        DrawMultiTargetEditor();
        DrawTestPreview();
        DrawBulkAssignment();
        GUILayout.Space(8);
        EditorGUILayout.HelpBox(status, MessageType.None);
        EditorGUILayout.EndScrollView();
    }

    private void DrawAssetHeader()
    {
        EditorGUILayout.LabelField("1. 派閥ルールアセット", EditorStyles.boldLabel);
        EditorGUILayout.LabelField("ゲームで使用するアセットの固定パス", DefaultAssetPath, EditorStyles.wordWrappedMiniLabel);
        FactionRules2D assigned = (FactionRules2D)EditorGUILayout.ObjectField("表示・編集するアセット", ruleAsset, typeof(FactionRules2D), false);
        if (assigned != ruleAsset) ruleAsset = assigned;

        if (GUILayout.Button("初期設定のルールアセットを作成 / 選択", GUILayout.Height(34)))
            CreateOrSelectDefaultAsset();
        if (ruleAsset == null) return;

        if (AssetDatabase.GetAssetPath(ruleAsset) != DefaultAssetPath)
            EditorGUILayout.HelpBox("現在のアセットは閲覧・編集できますが、ゲーム本体が自動読み込みするのは上記固定パスです。", MessageType.Warning);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("アセットを選択")) Selection.activeObject = ruleAsset;
        if (GUILayout.Button("推奨ルールへ初期化（上書き）"))
        {
            if (EditorUtility.DisplayDialog("派閥ルールの初期化", "このルールアセット内の既存のカスタム設定をすべて削除します。よろしいですか？", "初期化する", "戻る"))
            {
                Undo.RecordObject(ruleAsset, "Reset Faction Rules");
                ruleAsset.ResetToSuggestedDefaults();
                EditorUtility.SetDirty(ruleAsset);
                AssetDatabase.SaveAssets();
                status = "推奨初期ルールへ戻しました。";
            }
        }
        EditorGUILayout.EndHorizontal();
    }

    private void DrawRulesEditor()
    {
        EditorGUILayout.Space(12);
        EditorGUILayout.LabelField("2. ルールの編集", EditorStyles.boldLabel);
        SerializedObject so = new SerializedObject(ruleAsset);
        so.Update();
        editPlayerRules = EditorGUILayout.Foldout(editPlayerRules, "プレイヤーへの態度（カルマ/友好度）", true);
        if (editPlayerRules)
        {
            EditorGUILayout.HelpBox("盗賊=友好度40未満、冒険者=カルマ-30以下が初期値。" +
                "カルマと友好度を混ぜず、派閥ごとに別条件を選択できます。", MessageType.None);
            EditorGUILayout.PropertyField(so.FindProperty("playerRules"), new GUIContent("プレイヤー判定ルール"), true);
        }
        EditorGUILayout.Space(6);
        editNpcRules = EditorGUILayout.Foldout(editNpcRules, "NPC同士の敵対ルール（向きあり）", true);
        if (editNpcRules)
        {
            EditorGUILayout.HelpBox("攻撃者 → 被攻撃者の一方向ルールです。" +
                "ドワーフ→monsters/Slime を指定するとスケルトンには適用されません。" +
                "リストに無い組み合わせは中立です。", MessageType.None);
            EditorGUILayout.PropertyField(so.FindProperty("npcRules"), new GUIContent("NPC対NPCルール"), true);
        }
        if (so.ApplyModifiedProperties())
        {
            EditorUtility.SetDirty(ruleAsset);
            status = "ルールを編集しました。『アセットを保存』で確定してください。";
        }
        if (GUILayout.Button("アセットを保存", GUILayout.Height(26)))
        {
            AssetDatabase.SaveAssets();
            status = "派閥ルールアセットを保存しました。";
        }
    }

    /// <summary>
    /// STEP2 v1.1: チェックした複数の対象を、既存形式の「1対象1ルール」に展開する。
    /// Runtime/STEP3/STEP4 の形式・APIには一切変更を加えない。
    /// </summary>
    private void DrawMultiTargetEditor()
    {
        EditorGUILayout.Space(10);
        showMultiTarget = EditorGUILayout.Foldout(
            showMultiTarget, "2-B. 攻撃対象の複数選択（STEP2 v1.1）", true);
        if (!showMultiTarget) return;

        EditorGUILayout.HelpBox(
            "攻撃する側を選び、攻撃される側を複数チェックしてください。" +
            "『攻撃対象にする』を押すと、チェックしたIDをそれぞれ個別のルールとして登録します。\n" +
            "チェックを外しただけでは既存ルールは削除されません。" +
            "解除したい対象はチェックして『非攻撃対象にする』か『ルールを削除』を実行してください。",
            MessageType.Info);

        EditorGUI.BeginChangeCheck();
        multiAttackerIndex = EditorGUILayout.Popup(
            "攻撃する側のID", multiAttackerIndex, MultiAttackerLabels);
        if (EditorGUI.EndChangeCheck()) ClearMultiSelection();
        if (multiAttackerIndex == FactionIds.Length)
            multiCustomAttackerId = EditorGUILayout.TextField(
                "カスタム攻撃側ID", multiCustomAttackerId);

        string attackerId = GetMultiAttackerId();
        EditorGUILayout.LabelField("攻撃される側のID（複数選択可）", EditorStyles.boldLabel);
        for (int i = 0; i < FactionIds.Length; i++)
        {
            bool self = FactionId2D.Same(attackerId, FactionIds[i]);
            using (new EditorGUI.DisabledScope(self))
            {
                multiTargetChecks[i] = EditorGUILayout.ToggleLeft(
                    FactionLabels[i] + "  [" + FactionIds[i] + "]" +
                    (self ? "（同じ派閥は指定不可）" : string.Empty), multiTargetChecks[i]);
            }
            if (self) multiTargetChecks[i] = false;
        }

        multiMonsterType = (FactionMonsterType2D)EditorGUILayout.EnumPopup(
            new GUIContent("対象がmonstersの場合の種類",
                "Anyなら全モンスター。Slimeならスライムだけ。Skeletonならスケルトンだけ。"),
            multiMonsterType);
        EditorGUILayout.HelpBox(
            "例：ドワーフ → monsters/Slime を設定すると、スケルトンには適用されません。" +
            " 既存の『全種類』ルールより個別モンスター指定が優先されます。",
            MessageType.None);

        multiCustomTargetIds = EditorGUILayout.TextField(
            new GUIContent("追加の対象ID（任意）", "複数IDをカンマ区切りで入力。例: knights, traders"),
            multiCustomTargetIds);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("対象を全選択"))
        {
            for (int i = 0; i < multiTargetChecks.Length; i++)
                multiTargetChecks[i] = !FactionId2D.Same(attackerId, FactionIds[i]);
        }
        if (GUILayout.Button("選択解除"))
        {
            ClearMultiSelection();
            multiCustomTargetIds = string.Empty;
        }
        if (GUILayout.Button("既存の攻撃対象を選択"))
            LoadExistingHostileTargets(attackerId);
        EditorGUILayout.EndHorizontal();

        List<string> selected;
        string error;
        bool valid = FactionMultiTargetBatch2D.TryCollectTargets(
            attackerId, FactionIds, multiTargetChecks, multiCustomTargetIds,
            out selected, out error);
        if (!valid)
            EditorGUILayout.HelpBox(error, MessageType.Warning);
        else
            EditorGUILayout.LabelField("選択対象: " + selected.Count + "件  /  " +
                string.Join(", ", selected.ToArray()), EditorStyles.wordWrappedMiniLabel);

        using (new EditorGUI.DisabledScope(!valid || selected.Count == 0 || ruleAsset == null))
        {
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("選択した対象を攻撃対象にする", GUILayout.Height(33)))
                ApplyMultiTargets(attackerId, selected, FactionBatchOperation2D.Hostile);
            if (GUILayout.Button("選択した対象を非攻撃対象にする", GUILayout.Height(33)))
                ApplyMultiTargets(attackerId, selected, FactionBatchOperation2D.Neutral);
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("選択した対象のルールを削除（未指定へ戻す）"))
            {
                if (EditorUtility.DisplayDialog("NPC敵対ルールを削除",
                    "選択した " + selected.Count + "件のルールを削除します。\n" +
                    "全種類(Any)のルールが残る場合、削除後にそちらが適用されることがあります。",
                    "削除する", "キャンセル"))
                    ApplyMultiTargets(attackerId, selected, FactionBatchOperation2D.Delete);
            }
        }
        EditorGUILayout.HelpBox(
            "変更結果は上の『NPC対NPCルール』一覧にも反映されます。" +
            "保存するのは従来のTargetFactionId 1件ずつなので、STEP3/STEP4の更新は不要です。",
            MessageType.None);
    }

    private void ApplyMultiTargets(string attackerId, List<string> targets,
        FactionBatchOperation2D operation)
    {
        try
        {
            int changed = FactionMultiTargetBatch2D.Apply(
                ruleAsset, attackerId, targets, multiMonsterType, operation);
            FactionHostility2D.ClearDefaultRulesCache();
            status = "STEP2 v1.1：" + changed + "件の派閥ルールを" +
                (operation == FactionBatchOperation2D.Delete ? "削除" : "登録・更新") +
                "しました。自動保存済みです。";
            Repaint();
        }
        catch (ArgumentException ex)
        {
            status = "設定できませんでした: " + ex.Message;
            Debug.LogWarning("[派閥STEP2 v1.1] " + status);
        }
    }

    private string GetMultiAttackerId() => multiAttackerIndex < FactionIds.Length
        ? FactionIds[multiAttackerIndex]
        : multiCustomAttackerId;

    private void ClearMultiSelection()
    {
        for (int i = 0; i < multiTargetChecks.Length; i++) multiTargetChecks[i] = false;
    }

    private void LoadExistingHostileTargets(string attackerId)
    {
        ClearMultiSelection();
        if (ruleAsset == null) return;
        List<string> custom = new List<string>();
        foreach (FactionNpcRule2D rule in ruleAsset.NpcRules)
        {
            if (rule == null || !rule.IsHostile ||
                !FactionId2D.Same(rule.AttackerFactionId, attackerId)) continue;
            bool isMonster = FactionId2D.Same(rule.TargetFactionId, FactionId2D.Monsters);
            if (isMonster && rule.TargetMonsterType != multiMonsterType) continue;
            if (!isMonster && rule.TargetMonsterType != FactionMonsterType2D.Any) continue;
            int i = Array.FindIndex(FactionIds, x => FactionId2D.Same(x, rule.TargetFactionId));
            if (i >= 0)
            {
                if (!FactionId2D.Same(attackerId, rule.TargetFactionId)) multiTargetChecks[i] = true;
            }
            else if (!FactionId2D.Same(rule.TargetFactionId, attackerId) &&
                     !string.IsNullOrWhiteSpace(rule.TargetFactionId) &&
                     !custom.Contains(FactionId2D.Normalize(rule.TargetFactionId)))
                custom.Add(FactionId2D.Normalize(rule.TargetFactionId));
        }
        multiCustomTargetIds = string.Join(", ", custom.ToArray());
        status = "現在の攻撃ルールをチェック欄に読み込みました。";
    }

    private void DrawTestPreview()
    {
        EditorGUILayout.Space(12);
        showTest = EditorGUILayout.Foldout(showTest, "3. 敵対判定シミュレーター", true);
        if (!showTest) return;
        EditorGUILayout.HelpBox("ここでの数値変更はテストのみ。STEP1の実際のカルマ・友好度は変更されません。", MessageType.None);
        previewAttacker = EditorGUILayout.Popup("判定するNPCの派閥", previewAttacker, FactionLabels);
        previewKarma = EditorGUILayout.IntSlider("仮カルマ", previewKarma, -100, 100);
        previewReputation = EditorGUILayout.IntSlider("仮の対象派閥友好度", previewReputation, -100, 100);
        FactionDecision2D playerDecision = FactionHostility2D.EvaluateToPlayer(
            FactionIds[previewAttacker], previewKarma, previewReputation, ruleAsset);
        EditorGUILayout.HelpBox("対プレイヤー: " + (playerDecision.IsHostile ? "敵対" : "中立") +
            " / " + playerDecision.Reason, playerDecision.IsHostile ? MessageType.Warning : MessageType.Info);

        EditorGUILayout.Space(5);
        previewTarget = EditorGUILayout.Popup("ターゲット派閥", previewTarget, FactionLabels);
        if (previewTarget == 0)
            previewSpecies = (FactionMonsterType2D)EditorGUILayout.EnumPopup("対象モンスター種", previewSpecies);
        FactionDecision2D npcDecision = FactionHostility2D.EvaluateNpcToNpc(
            FactionIds[previewAttacker], FactionIds[previewTarget], previewSpecies, ruleAsset);
        EditorGUILayout.HelpBox("対NPC: " + (npcDecision.IsHostile ? "攻撃対象" : "非攻撃対象") +
            " / " + npcDecision.Reason, npcDecision.IsHostile ? MessageType.Warning : MessageType.Info);

        GUILayout.Space(5);
        EditorGUILayout.LabelField("初期ルールの確認（簡易診断）", EditorStyles.boldLabel);
        if (GUILayout.Button("スライム/スケルトン・盗賊・冒険者・ドワーフの判定をConsoleへ出力"))
            PrintTestSummary();
    }

    private void DrawBulkAssignment()
    {
        EditorGUILayout.Space(12);
        showBulk = EditorGUILayout.Foldout(showBulk, "4. 選択したGameObject/Prefabへ派閥を一括設定", true);
        if (!showBulk) return;
        EditorGUILayout.HelpBox(
            "HierarchyのNPC/Enemy（ルートObject）、またはProjectのPrefabを複数選択して実行。" +
            "プレイヤーには『Player』設定を使ってください。" +
            "既存の攻撃AIは変更しません。Prefabは元のAssetに保存されます。", MessageType.Info);
        bulkFactionIndex = EditorGUILayout.Popup("設定する派閥", bulkFactionIndex,
            new[] { "プレイヤー", "スライム・スケルトン", "盗賊", "冒険者", "ドワーフ" });
        if (bulkFactionIndex == 1)
            bulkMonsterSpecies = (FactionMonsterType2D)EditorGUILayout.EnumPopup("モンスターの種類", bulkMonsterSpecies);
        bulkPreserveExisting = EditorGUILayout.ToggleLeft("既存FactionMember2Dの設定は上書きしない", bulkPreserveExisting);
        EditorGUILayout.LabelField("現在の選択数: " + Selection.gameObjects.Length + " GameObject", EditorStyles.miniLabel);
        if (GUILayout.Button("選択オブジェクトにFactionMember2Dを一括設定", GUILayout.Height(34)))
            ApplyToSelection();
        EditorGUILayout.HelpBox("Playerは1体だけ所属ID=playerに設定してください。" +
            "スライム・スケルトンを同じプレハブで使い回す場合も、種類は個別に設定してください。", MessageType.None);
    }

    private void CreateOrSelectDefaultAsset()
    {
        FactionRules2D existing = AssetDatabase.LoadAssetAtPath<FactionRules2D>(DefaultAssetPath);
        if (existing != null)
        {
            ruleAsset = existing;
            status = "既存のルールアセットを選択しました（初期化していません）。";
        }
        else
        {
            EnsureFolder("Assets/Resources");
            EnsureFolder("Assets/Resources/FactionRules");
            FactionRules2D created = CreateInstance<FactionRules2D>();
            created.ResetToSuggestedDefaults();
            AssetDatabase.CreateAsset(created, DefaultAssetPath);
            AssetDatabase.SaveAssets();
            ruleAsset = created;
            status = "初期ルールアセットを作成しました。";
        }
        FactionHostility2D.ClearDefaultRulesCache();
        Selection.activeObject = ruleAsset;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    private void PrintTestSummary()
    {
        string[] sources = { FactionId2D.Monsters, FactionId2D.Bandits, FactionId2D.Adventurers, FactionId2D.Dwarves };
        foreach (string source in sources)
        {
            FactionDecision2D p = FactionHostility2D.EvaluateToPlayer(source, 0, 0, ruleAsset);
            Debug.Log("[STEP2] " + FactionId2D.DisplayName(source) + " → Player: " + (p.IsHostile ? "敵対" : "中立") + " / " + p.Reason);
            foreach (string target in sources)
            {
                if (target == FactionId2D.Monsters)
                {
                    PrintNpcPair(source, target, FactionMonsterType2D.Slime);
                    PrintNpcPair(source, target, FactionMonsterType2D.Skeleton);
                }
                else PrintNpcPair(source, target, FactionMonsterType2D.None);
            }
        }
        status = "判定結果をUnity Consoleへ出力しました。";
    }

    private void PrintNpcPair(string source, string target, FactionMonsterType2D species)
    {
        FactionDecision2D d = FactionHostility2D.EvaluateNpcToNpc(source, target, species, ruleAsset);
        Debug.Log("[STEP2] " + FactionId2D.DisplayName(source) + " → " +
            FactionId2D.DisplayName(target) + " [" + species + "]: " +
            (d.IsHostile ? "攻撃" : "中立"));
    }

    private void ApplyToSelection()
    {
        GameObject[] selected = Selection.gameObjects;
        if (selected == null || selected.Length == 0)
        {
            status = "HierarchyまたはProjectからGameObject/Prefabを選択してください。";
            return;
        }
        string id;
        switch (bulkFactionIndex)
        {
            case 0: id = FactionId2D.Player; break;
            case 1: id = FactionId2D.Monsters; break;
            case 2: id = FactionId2D.Bandits; break;
            case 3: id = FactionId2D.Adventurers; break;
            default: id = FactionId2D.Dwarves; break;
        }
        FactionMonsterType2D species = bulkFactionIndex == 1 ? bulkMonsterSpecies : FactionMonsterType2D.None;
        int applied = 0, skipped = 0, failed = 0;
        HashSet<string> processedAssets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (GameObject obj in selected)
        {
            if (obj == null) continue;
            string path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path))
            {
                if (!path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase) ||
                    !processedAssets.Add(path)) { skipped++; continue; }
                GameObject root = null;
                try
                {
                    root = PrefabUtility.LoadPrefabContents(path);
                    FactionMember2D existing = root.GetComponent<FactionMember2D>();
                    if (existing != null && bulkPreserveExisting) { skipped++; continue; }
                    FactionMember2D component = existing != null ? existing : root.AddComponent<FactionMember2D>();
                    component.Configure(id, species);
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    applied++;
                }
                catch (Exception ex)
                {
                    failed++;
                    Debug.LogWarning("[派閥STEP2] Prefabの設定に失敗: " + path + " / " + ex.Message);
                }
                finally
                {
                    if (root != null) PrefabUtility.UnloadPrefabContents(root);
                }
            }
            else if (obj.scene.IsValid())
            {
                FactionMember2D existing = obj.GetComponent<FactionMember2D>();
                if (existing != null && bulkPreserveExisting) { skipped++; continue; }
                FactionMember2D component = existing != null ? existing : Undo.AddComponent<FactionMember2D>(obj);
                Undo.RecordObject(component, "Change faction");
                component.Configure(id, species);
                EditorUtility.SetDirty(component);
                EditorSceneManager.MarkSceneDirty(obj.scene);
                applied++;
            }
            else skipped++;
        }
        AssetDatabase.SaveAssets();
        status = "一括設定: " + applied + "件 / スキップ: " + skipped + "件 / 失敗: " + failed + "件。Sceneを保存してください。";
    }
}
#endif
