#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CaveTutorialManager2D))]
public sealed class CaveTutorialManager2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        CaveTutorialManager2D manager =
            (CaveTutorialManager2D)target;

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField(
            "チュートリアル進行度 1～11（スキップ済みを含む）",
            EditorStyles.boldLabel
        );

        EditorGUILayout.HelpBox(
            Application.isPlaying
                ? $"現在Step: {manager.CurrentStep}  ※先の地点へ進むと途中Stepは自動スキップされます。"
                : "進行チェックはPlay中に自動更新されます。先の地点へ到達した場合、途中項目も通過扱いになります。",
            MessageType.Info
        );

        using (new EditorGUI.DisabledScope(true))
        {
            DrawCheck("1  A・Dで移動", manager.Progress01MoveCompleted);
            DrawCheck("2  SPACEでジャンプ", manager.Progress02JumpCompleted);
            DrawCheck("3  左ALTでほふく", manager.Progress03ProneCompleted);
            DrawCheck("4  壁登り", manager.Progress04WallClimbCompleted);
            DrawCheck("5  ItemBoxを開いてItem回収", manager.Progress05ItemBoxCompleted);
            DrawCheck("6  Sで壁を降りる", manager.Progress06WallDescendCompleted);
            DrawCheck("7  TABでInventoryを開く", manager.Progress07InventoryCompleted);
            DrawCheck("8  武器装備＋リロード", manager.Progress08WeaponCompleted);
            DrawCheck("9  敵を攻撃", manager.Progress09EnemyCompleted);
            DrawCheck("10 NPCを持つ＋背負う", manager.Progress10NpcCarryCompleted);
            DrawCheck("11 NPCをエレベーターへ救出", manager.Progress11NpcRescueCompleted);
        }

        if (manager.IsCompleted)
        {
            EditorGUILayout.HelpBox(
                "✓ チュートリアル完了",
                MessageType.Info
            );
        }

        if (Application.isPlaying)
        {
            Repaint();
        }
    }

    private static void DrawCheck(string label, bool value)
    {
        EditorGUILayout.ToggleLeft(label, value);
    }
}
#endif
