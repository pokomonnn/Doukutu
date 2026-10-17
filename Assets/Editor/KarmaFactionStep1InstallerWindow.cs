#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 既存の GameSessionManager / SaveGameData に安全な最小限の処理を追記する。
/// 元ソースの完全上書きはしない。バックアップは Unity プロジェクト直下の Assets 外に作る。
/// 文字列アンカーが見つからない場合は、一切ファイルを書き換えず中止する。
/// </summary>
public sealed class KarmaFactionStep1InstallerWindow : EditorWindow
{
    private const string ResetMark = "// KARMA_STEP1_RESET";
    private const string SaveMark = "// KARMA_STEP1_SAVE";
    private const string LoadMark = "// KARMA_STEP1_LOAD";
    private const string DataMark = "// KARMA_STEP1_FIELD";

    private string lastMessage = "未実行";

    [MenuItem("Tools/2D Shooter/STEP1 - カルマ・友好度導入")]
    public static void Open() => GetWindow<KarmaFactionStep1InstallerWindow>("カルマSTEP1導入");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("カルマ・派閥友好度 STEP1", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "既存の GameSessionManager.cs と SaveGameData.cs に、\n" +
            "ニューゲーム初期化・JSON保存・JSON読込の最小限の処理を追加します。\n" +
            "元ファイルは Assets 外にバックアップします。Unityでの実行前にGit等でもコミットしておくと安全です。",
            MessageType.Info);

        GUILayout.Space(8);
        if (GUILayout.Button("① セーブ連携を検査", GUILayout.Height(34)))
        {
            try { lastMessage = Inspect(); }
            catch (Exception ex) { lastMessage = "検査できません: " + ex.Message; }
        }

        if (GUILayout.Button("② バックアップしてセーブ連携を追加", GUILayout.Height(42)))
        {
            try
            {
                string report = Apply();
                lastMessage = report;
                Debug.Log("[Karma STEP1] " + report);
            }
            catch (Exception ex)
            {
                lastMessage = "適用を中止しました: " + ex.Message;
                Debug.LogError("[Karma STEP1] " + lastMessage);
            }
        }

        GUILayout.Space(8);
        EditorGUILayout.HelpBox(lastMessage, MessageType.None);
        GUILayout.Space(8);
        EditorGUILayout.LabelField("重要", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "適用は必ず Edit モードで行ってください。2つの元スクリプトが見つからないか、\n" +
            "バージョン違いで安全な挿入位置を判断できない場合は変更せず停止します。\n" +
            "STEP1 は敵対AI・自動カルマ増減を変更しません。",
            MessageType.Warning);
    }

    private static string Inspect()
    {
        string game = FindUniqueScript("GameSessionManager.cs");
        string save = FindUniqueScript("SaveGameData.cs");
        string gameText = File.ReadAllText(game);
        string saveText = File.ReadAllText(save);
        bool g = HasAllGameMarkers(gameText);
        bool d = saveText.Contains(DataMark);
        if (g && d) return "STEP1導入済みです。次は Play モードで『カルマ・友好度テスト』を開いてください。";
        if (g != d || ContainsAnyGameMarker(gameText) || (saveText.Contains("KarmaReputation") && !d))
            return "部分適用または独自改造の可能性があります。自動追加は行わず、手動差分を確認してください。";
        // 安全に挿入可能かを実際に試す（結果は書き込まない）。
        PatchGameText(gameText);
        PatchSaveText(saveText);
        return "未導入。既存ソース2本を認識し、挿入位置も確認できました。";
    }

    private static string Apply()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Playモードを停止してから導入してください。");

        string gamePath = FindUniqueScript("GameSessionManager.cs");
        string savePath = FindUniqueScript("SaveGameData.cs");
        string gameText = File.ReadAllText(gamePath);
        string saveText = File.ReadAllText(savePath);

        if (HasAllGameMarkers(gameText) && saveText.Contains(DataMark))
            return "すでに導入済みです。二重挿入はしません。";

        if (ContainsAnyGameMarker(gameText) || saveText.Contains(DataMark) ||
            saveText.Contains("KarmaReputation"))
            throw new InvalidOperationException("既存ソースに部分適用・独自実装の跡があります。自動上書きせず停止しました。");

        // 全変更をメモリ上で検証してからバックアップ・書き込みする。
        string patchedGame = PatchGameText(gameText);
        string patchedSave = PatchSaveText(saveText);
        if (!HasAllGameMarkers(patchedGame) || !patchedSave.Contains(DataMark))
            throw new InvalidOperationException("変更後の検査に失敗しました。");

        string projectRoot = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        string backupDir = Path.Combine(projectRoot, "KarmaFactionStep1_Backups",
            DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        Directory.CreateDirectory(backupDir);
        string gameBackup = Path.Combine(backupDir, "GameSessionManager.cs.bak.txt");
        string saveBackup = Path.Combine(backupDir, "SaveGameData.cs.bak.txt");
        File.Copy(gamePath, gameBackup, false);
        File.Copy(savePath, saveBackup, false);

        // 元ファイルのBOMを維持。改行は元ソースから検出した文字を使用。
        try
        {
            WritePreservingBom(gamePath, patchedGame);
            WritePreservingBom(savePath, patchedSave);
        }
        catch
        {
            // 片方しか書けなかった状態で残さないよう復元。
            File.Copy(gameBackup, gamePath, true);
            File.Copy(saveBackup, savePath, true);
            AssetDatabase.Refresh();
            throw;
        }

        AssetDatabase.Refresh();
        return "STEP1のセーブ連携を追加しました。バックアップ: " + backupDir;
    }

    private static string PatchGameText(string input)
    {
        if (ContainsAnyGameMarker(input))
            throw new InvalidOperationException("GameSessionManager.cs にSTEP1のマーカーが既に存在します。");

        string nl = DetectNewline(input);
        const string resetSignature = "public void ResetForNewGame()";
        int reset = RequireUnique(input, resetSignature);
        int resetBrace = input.IndexOf('{', reset + resetSignature.Length);
        if (resetBrace < 0 || resetBrace - reset > 160)
            throw new InvalidOperationException("ResetForNewGameの開始位置が見つかりません。");

        const string saveSignature = "public SaveGameData CreateSaveGameData(";
        const string loadSignature = "public bool ApplySaveGameData(";
        int saveStart = RequireUnique(input, saveSignature);
        int loadStart = RequireUnique(input, loadSignature);
        if (saveStart >= loadStart)
            throw new InvalidOperationException("GameSessionManagerの保存・復元メソッド位置が不正です。");

        const string ret = "return saveData;";
        int saveReturn = input.LastIndexOf(ret, loadStart, loadStart - saveStart);
        if (saveReturn < saveStart)
            throw new InvalidOperationException("CreateSaveGameDataのreturnが見つかりません。");

        int loadNext = input.IndexOf("private SavedSkillCardData CreateSavedSkillCardData(", loadStart);
        if (loadNext < 0) throw new InvalidOperationException("ApplySaveGameDataの終端が認識できません。");
        int notify = input.IndexOf("NotifyMoneyChanged();", loadStart);
        if (notify < loadStart || notify > loadNext)
            throw new InvalidOperationException("ApplySaveGameData内の通知位置を特定できません。");

        string result = input;
        // 末尾から挿入してオフセットがずれないようにする。
        var inserts = new List<KeyValuePair<int, string>>
        {
            new KeyValuePair<int, string>(resetBrace + 1,
                nl + "        " + ResetMark + nl +
                "        PlayerKarmaReputation2D.GetOrCreate(this).ResetToDefaults();"),
            new KeyValuePair<int, string>(saveReturn,
                SaveMark + nl + "        saveData.KarmaReputation = " +
                "PlayerKarmaReputation2D.GetOrCreate(this).CreateSaveData();" + nl + nl + "        "),
            new KeyValuePair<int, string>(notify,
                LoadMark + nl + "        PlayerKarmaReputation2D.GetOrCreate(this)" +
                ".RestoreFromSaveData(saveData.KarmaReputation);" + nl + nl + "        ")
        };
        foreach (KeyValuePair<int, string> insertion in inserts.OrderByDescending(x => x.Key))
            result = result.Insert(insertion.Key, insertion.Value);
        return result;
    }

    private static string PatchSaveText(string input)
    {
        if (input.Contains(DataMark) || input.Contains("KarmaReputation"))
            throw new InvalidOperationException("SaveGameData.csに同様のフィールドが存在します。");
        int location = RequireUnique(input, "public int Money;");
        int after = location + "public int Money;".Length;
        string nl = DetectNewline(input);
        return input.Insert(after,
            nl + nl + "    " + DataMark + nl +
            "    // 旧セーブにない場合はロード時に初期値0へ復元する。" + nl +
            "    public SavedKarmaReputationData KarmaReputation = " +
            "new SavedKarmaReputationData();");
    }

    private static bool ContainsAnyGameMarker(string input) =>
        input.Contains(ResetMark) || input.Contains(SaveMark) || input.Contains(LoadMark);

    private static bool HasAllGameMarkers(string input) =>
        input.Contains(ResetMark) && input.Contains(SaveMark) && input.Contains(LoadMark);

    private static int RequireUnique(string text, string anchor)
    {
        int first = text.IndexOf(anchor, StringComparison.Ordinal);
        if (first < 0) throw new InvalidOperationException("必要なコードが見つかりません: " + anchor);
        int second = text.IndexOf(anchor, first + anchor.Length, StringComparison.Ordinal);
        if (second >= 0) throw new InvalidOperationException("候補が複数あります: " + anchor);
        return first;
    }

    private static string FindUniqueScript(string filename)
    {
        string[] found = Directory.GetFiles(Application.dataPath, filename, SearchOption.AllDirectories)
            .Where(p => !p.Contains("/PackageCache/") && !p.Contains("\\PackageCache\\"))
            .ToArray();
        if (found.Length != 1)
            throw new InvalidOperationException(filename + " をAssets内で " + found.Length +
                " 件検出しました。1件だけになるよう確認してください。");
        return found[0];
    }

    private static string DetectNewline(string text) => text.Contains("\r\n") ? "\r\n" : "\n";

    private static void WritePreservingBom(string path, string text)
    {
        byte[] oldBytes = File.ReadAllBytes(path);
        bool hasUtf8Bom = oldBytes.Length >= 3 && oldBytes[0] == 0xEF &&
            oldBytes[1] == 0xBB && oldBytes[2] == 0xBF;
        File.WriteAllText(path, text, new UTF8Encoding(hasUtf8Bom));
    }
}
#endif
