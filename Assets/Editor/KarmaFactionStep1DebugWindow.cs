#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public sealed class KarmaFactionStep1DebugWindow : EditorWindow
{
    private string customFactionId = "";
    private int customFactionValue;
    private Vector2 scroll;

    [MenuItem("Tools/2D Shooter/STEP1 - カルマ・友好度テスト")]
    public static void Open() => GetWindow<KarmaFactionStep1DebugWindow>("カルマSTEP1テスト");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("カルマ・派閥友好度（実行中テスト）", EditorStyles.boldLabel);
        if (!EditorApplication.isPlaying)
        {
            EditorGUILayout.HelpBox("Playモード中に利用してください。変更した数値は本セーブまで維持され、ロード時に復元されます。", MessageType.Info);
            return;
        }

        PlayerKarmaReputation2D state = PlayerKarmaReputation2D.GetCurrent();
        if (state == null)
        {
            EditorGUILayout.HelpBox("GameSessionManagerが見つかりません。初期Sceneに配置してください。", MessageType.Warning);
            return;
        }

        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("カルマ", EditorStyles.boldLabel);
        int karma = EditorGUILayout.IntSlider("プレイヤー", state.Karma,
            PlayerKarmaReputation2D.MinValue, PlayerKarmaReputation2D.MaxValue);
        if (karma != state.Karma) state.SetKarma(karma);
        GUILayout.Space(5);
        EditorGUILayout.LabelField("派閥友好度", EditorStyles.boldLabel);
        DrawRep(state, PlayerKarmaReputation2D.MonstersId, "スライム・スケルトン");
        DrawRep(state, PlayerKarmaReputation2D.BanditsId, "盗賊");
        DrawRep(state, PlayerKarmaReputation2D.AdventurersId, "冒険者");
        DrawRep(state, PlayerKarmaReputation2D.DwarvesId, "ドワーフ");

        GUILayout.Space(8);
        EditorGUILayout.LabelField("将来の新派閥（任意）", EditorStyles.boldLabel);
        customFactionId = EditorGUILayout.TextField("派閥ID（英数字推奨）", customFactionId);
        customFactionValue = EditorGUILayout.IntSlider("友好度", customFactionValue, -100, 100);
        if (GUILayout.Button("この派閥IDを登録 / 更新"))
            state.SetReputation(customFactionId, customFactionValue);

        GUILayout.Space(8);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("全部0にリセット"))
        {
            if (EditorUtility.DisplayDialog("確認", "カルマと全友好度を0に戻しますか？", "リセット", "戻る"))
                state.ResetToDefaults();
        }
        if (GUILayout.Button("保存データをConsoleに表示"))
            Debug.Log("[Karma STEP1] " + JsonUtility.ToJson(state.CreateSaveData(), true));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.HelpBox("数値を変更しても攻撃AIは変わりません（STEP2/3で連携）。ニューゲームは0、ロードはセーブ値に復元されます。", MessageType.Info);
        EditorGUILayout.EndScrollView();
        if (Event.current.type == EventType.Repaint) Repaint();
    }

    private static void DrawRep(PlayerKarmaReputation2D state, string id, string label)
    {
        int value = state.GetReputation(id);
        int next = EditorGUILayout.IntSlider(label, value, -100, 100);
        if (next != value) state.SetReputation(id, next);
    }
}
#endif
