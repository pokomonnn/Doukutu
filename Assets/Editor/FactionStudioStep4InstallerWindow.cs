#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 既存のStudioを置換せず、最小限の呼び出しを差し込むインストーラー。
/// 元C#はAssets外のLibraryにバックアップする（重複クラス防止）。
/// </summary>
public sealed class FactionStudioStep4InstallerWindow : EditorWindow
{
    private const string NpcName = "DungeonNpcStudioWindow2D.cs";
    private const string EnemyName = "EnemyStudioWindow2D.cs";
    private const string NpcToken = "FACTION_STEP4_NPC_PANEL";
    private const string EnemyToken = "FACTION_STEP4_ENEMY_PANEL";

    private string npcPath = string.Empty;
    private string enemyPath = string.Empty;
    private string status = "『スクリプトを探す』→『検査』→『バックアップして追加』の順に操作してください。";
    private Vector2 scroll;

    [MenuItem("Tools/2D Shooter/STEP4 - NPC・Enemy Studio派閥統合")]
    public static void Open() => GetWindow<FactionStudioStep4InstallerWindow>("派閥STEP4");

    private void OnEnable() { Discover(); }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        GUILayout.Label("STEP4 - 派閥設定を2つのStudioへ統合", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("STEP1〜STEP3を導入済みのプロジェクト用です。\n" +
            "既存のNPC Studio / Enemy Studioを丸ごと上書きせず、派閥編集欄への呼び出しだけを追加します。", MessageType.Info);

        EditorGUILayout.Space(10);
        GUILayout.Label("対象スクリプト", EditorStyles.boldLabel);
        npcPath = EditorGUILayout.TextField("NPC Studio", npcPath);
        enemyPath = EditorGUILayout.TextField("Enemy Studio", enemyPath);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("① スクリプトを探す")) Discover();
        if (GUILayout.Button("② 検査")) status = Inspect();
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(8);
        if (GUILayout.Button("③ バックアップして派閥設定欄を追加", GUILayout.Height(36)))
        {
            if (EditorUtility.DisplayDialog("STEP4を導入", "2つのStudioに派閥欄を追加します。\n更新前のC#をLibrary/FactionStep4Backupsに保管します。\n続行しますか？", "追加する", "戻る"))
                Install();
        }
        EditorGUILayout.HelpBox(status, MessageType.None);

        EditorGUILayout.Space(12);
        GUILayout.Label("導入後", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("NPC Studio: 新規作成・既存編集時に『派閥・戦闘』が表示されます。保存ボタンで反映。\n" +
            "Enemy Studio: 『ミッション』と『全コンポーネント』の間に派閥設定を表示。Draftを編集し、既存の保存ボタンで反映。", MessageType.Info);
        if (GUILayout.Button("STEP2 ルールエディターを開く")) FactionRulesStudioWindow2D.Open();
        EditorGUILayout.EndScrollView();
    }

    private void Discover()
    {
        npcPath = FindOne(NpcName);
        enemyPath = FindOne(EnemyName);
        status = (string.IsNullOrEmpty(npcPath) || string.IsNullOrEmpty(enemyPath))
            ? "スクリプトの検出に失敗しました。2つの.csがAssets内にあり、名前が一致するか確認してください。"
            : "検出しました。次に『② 検査』を押してください。";
    }

    private static string FindOne(string filename)
    {
        List<string> paths = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.Equals(Path.GetFileName(path), filename, StringComparison.OrdinalIgnoreCase))
                paths.Add(path);
        }
        return paths.Count == 1 ? paths[0] : string.Empty;
    }

    private string Inspect()
    {
        try
        {
            string n = ReadScript(npcPath, NpcName);
            string e = ReadScript(enemyPath, EnemyName);
            string np = PatchNpc(n);
            string ep = PatchEnemy(e);
            string ns = n == np ? "導入済み" : "追加可能";
            string es = e == ep ? "導入済み" : "追加可能";
            return "NPC Studio: " + ns + "\nEnemy Studio: " + es +
                "\n両方が導入済みなら再実行は不要です。";
        }
        catch (Exception ex) { return "検査失敗: " + ex.Message; }
    }

    private void Install()
    {
        try
        {
            string originalNpc = ReadScript(npcPath, NpcName);
            string originalEnemy = ReadScript(enemyPath, EnemyName);
            // 先に両方の編集が成功するか検証。片方だけ書き換わる状態を避ける。
            string modifiedNpc = PatchNpc(originalNpc);
            string modifiedEnemy = PatchEnemy(originalEnemy);
            if (originalNpc == modifiedNpc && originalEnemy == modifiedEnemy)
            {
                status = "STEP4はすでに導入されています。変更していません。";
                return;
            }

            string projectRoot = Path.GetDirectoryName(Application.dataPath);
            string backupDir = Path.Combine(projectRoot, "Library", "FactionStep4Backups",
                DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            Directory.CreateDirectory(backupDir);
            if (originalNpc != modifiedNpc)
                File.WriteAllText(Path.Combine(backupDir, NpcName + ".before-step4.txt"), originalNpc, new UTF8Encoding(false));
            if (originalEnemy != modifiedEnemy)
                File.WriteAllText(Path.Combine(backupDir, EnemyName + ".before-step4.txt"), originalEnemy, new UTF8Encoding(false));

            if (originalNpc != modifiedNpc) WriteScript(npcPath, modifiedNpc);
            if (originalEnemy != modifiedEnemy) WriteScript(enemyPath, modifiedEnemy);
            status = "追加完了。Unityの再コンパイルを待って、両Studioを開いてください。\nバックアップ: " + backupDir;
            AssetDatabase.Refresh();
        }
        catch (Exception ex)
        {
            status = "追加できませんでした: " + ex.Message + "\nスクリプトを無理に更新せず、必要ならバックアップから戻してください。";
            Debug.LogException(ex);
        }
    }

    private static string ReadScript(string path, string filename)
    {
        if (string.IsNullOrWhiteSpace(path) || !path.Replace('\\', '/').StartsWith("Assets/", StringComparison.Ordinal) ||
            !string.Equals(Path.GetFileName(path), filename, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(filename + " のパスが不正です。");
        string absolute = GetAbsolutePath(path);
        if (!File.Exists(absolute)) throw new FileNotFoundException(filename + " が見つかりません。", absolute);
        string content = File.ReadAllText(absolute);
        if (content.IndexOf("class " + Path.GetFileNameWithoutExtension(filename), StringComparison.Ordinal) < 0)
            throw new InvalidOperationException(filename + " 内に対応するクラスがありません。");
        return content;
    }

    private static string GetAbsolutePath(string path)
    {
        string projectRoot = Path.GetFullPath(Path.GetDirectoryName(Application.dataPath));
        string abs = Path.GetFullPath(Path.Combine(projectRoot, path.Replace('/', Path.DirectorySeparatorChar)));
        string assetsRoot = Path.GetFullPath(Application.dataPath).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!abs.StartsWith(assetsRoot, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Assets外は編集できません: " + path);
        return abs;
    }

    private static void WriteScript(string path, string updated)
    {
        File.WriteAllText(GetAbsolutePath(path), updated, new UTF8Encoding(false));
    }

    private static string Nl(string value) => value.Contains("\r\n") ? "\r\n" : "\n";

    /// <summary>既存の各メソッドを一箇所ずつ見つけ、挿入だけ行う。</summary>
    private static string PatchNpc(string source)
    {
        if (source.Contains(NpcToken))
        {
            if (source.Contains("FACTION_STEP4_NPC_APPLY") && source.Contains("FACTION_STEP4_NPC_LOAD") &&
                source.Contains("FACTION_STEP4_NPC_RESET")) return source;
            throw new InvalidOperationException("NPC StudioにSTEP4が部分適用されています。手動確認が必要です。");
        }
        string nl = Nl(source);
        string updated = ReplaceOnce(source, "private GameObject editingPrefab;",
            "private GameObject editingPrefab;" + nl +
            "    private FactionStudioStep4Settings2D factionStep4Settings = new FactionStudioStep4Settings2D(); // " + NpcToken,
            "NPC settings field");
        updated = InsertAfterMethodOpening(updated, "NewNpc", "        factionStep4Settings = new FactionStudioStep4Settings2D(); // FACTION_STEP4_NPC_RESET" + nl);
        updated = InsertAfterMethodOpening(updated, "LoadPrefab", "        FactionStudioStep4Panel2D.ReadFrom(prefab, factionStep4Settings); // FACTION_STEP4_NPC_LOAD" + nl);

        Match saveUi = Regex.Match(updated, @"(?m)^ {8}if\s*\(GUILayout\.Button\(editingPrefab\s*!=\s*null\s*\?");
        if (!saveUi.Success) throw new InvalidOperationException("NPC Studioの保存ボタンが見つかりません。対応する版を確認してください。");
        updated = updated.Insert(saveUi.Index, "        EditorGUILayout.Space(8);" + nl +
            "        FactionStudioStep4Panel2D.DrawNpcSettings(factionStep4Settings);" + nl + nl);

        const string nextMethod = "    private void ConfigureCorpse(GameObject root)";
        int index = updated.IndexOf(nextMethod, StringComparison.Ordinal);
        if (index < 0) throw new InvalidOperationException("NPC Studio.ConfigureCorpse()が見つかりません。");
        int begin = updated.IndexOf("private void ConfigurePrefab(GameObject root)", StringComparison.Ordinal);
        int end = updated.LastIndexOf("    }" + nl + nl, index, StringComparison.Ordinal);
        if (begin < 0 || end < begin || end >= index)
            throw new InvalidOperationException("NPC Studio.ConfigurePrefab()の末尾が特定できません。");
        updated = updated.Insert(end, "        FactionStudioStep4Panel2D.ApplyTo(root, factionStep4Settings); // FACTION_STEP4_NPC_APPLY" + nl);
        return updated;
    }

    private static string PatchEnemy(string source)
    {
        if (source.Contains(EnemyToken)) return source;
        string nl = Nl(source);
        const string anchor = "DrawSection(Section.Advanced, \"全コンポーネントをInspector形式で表示\", DrawAdvanced);";
        string addition = "FactionStudioStep4Panel2D.DrawEnemyDraft(draft); // " + EnemyToken + nl + "            " + anchor;
        return ReplaceOnce(source, anchor, addition, "Enemy Studio advanced section");
    }

    private static string ReplaceOnce(string text, string needle, string replacement, string description)
    {
        int pos = text.IndexOf(needle, StringComparison.Ordinal);
        if (pos < 0 || text.IndexOf(needle, pos + needle.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException(description + " の変更箇所を1個に特定できません。");
        return text.Substring(0, pos) + replacement + text.Substring(pos + needle.Length);
    }

    private static string InsertAfterMethodOpening(string text, string method, string code)
    {
        Match match = Regex.Match(text, @"private\s+void\s+" + Regex.Escape(method) + @"\s*\([^)]*\)\s*\{");
        if (!match.Success) throw new InvalidOperationException("NPC Studio." + method + "() が見つかりません。");
        return text.Insert(match.Index + match.Length, Nl(text) + code);
    }
}
#endif
