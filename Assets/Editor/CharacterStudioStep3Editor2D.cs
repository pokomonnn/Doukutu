#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Character Studio 2D - STEP3 恐怖・逃走・遠距離間合い調整。</summary>
public sealed partial class CharacterStudioWindow2D
{
    private void DrawCharacterAIStep3Settings()
    {
        EditorGUILayout.Space(9);
        EditorGUILayout.LabelField("STEP3  HP低下で逃走・NPCの恐怖・遠距離戦術", EditorStyles.boldLabel);

        CharacterAIFearFlee2D fear = draft.GetComponent<CharacterAIFearFlee2D>();
        bool fearActive = fear != null && fear.enabled;
        EditorGUI.BeginChangeCheck();
        bool wantFear = EditorGUILayout.ToggleLeft("逃走・恐怖AIを使用（STEP3）", fearActive, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (wantFear && fear == null) fear = draft.AddComponent<CharacterAIFearFlee2D>();
            if (fear != null)
            {
                Undo.RecordObject(fear, "STEP3 Fear toggle");
                fear.enabled = wantFear;
            }
        }
        if (fear != null && fear.enabled)
        {
            DrawFields(fear, "HPが減った時の逃走", "fleeWhenLowHealth",
                "fleeHealthRatio", "stopFleeHealthRatio");
            DrawFields(fear, "NPCが敵を怖がる", "fearResponse", "fearStartDistance",
                "threatScanDistance", "reactToIncomingHostility", "threatLayers");
            DrawFields(fear, "逃走速度・落ち着くまで", "fleeSpeed", "panicHoldSeconds",
                "calmDownSeconds", "rescanInterval");
            DrawFields(fear, "恐怖アニメーション（任意）", "characterAnimator", "fleeingBoolName", "coweringBoolName");
            DrawFields(fear, "確認・デバッグ", "debugLogs", "showGizmo");
            if (draft.GetComponent<FactionMember2D>() == null)
                EditorGUILayout.HelpBox("FactionMember2Dが必要です。派閥欄で所属を設定してね。", MessageType.Warning);
            if (draft.GetComponent<CharacterHealth>() == null)
                EditorGUILayout.HelpBox("HP低下による逃走にはCharacterHealthが必要です。", MessageType.Warning);
            if (draft.GetComponent<EnemyChaser2D>() == null && draft.GetComponent<CharacterWander2D>() == null)
                EditorGUILayout.HelpBox("逃走にはEnemyChaser2DかCharacterWander2Dによる移動担当が必要です。", MessageType.Warning);
            if (draft.GetComponent<CarryableObject2D>() != null)
                EditorGUILayout.HelpBox("救出NPC：持ち上げ中と下ろした直後の休止中は恐怖AIが停止します。", MessageType.None);
            EditorGUILayout.HelpBox("救出NPCには Fear Response = Flee（逃走）または Cower（身をすくめる）を選択。" +
                "戦闘する敵には Fear Response = None、Flee When Low Health = ONがおすすめ。" +
                "相手が自分に敵対する場合も危険として検出します。", MessageType.Info);
        }
        else
            EditorGUILayout.HelpBox("OFFならHP低下・恐怖による行動変更は行いません。", MessageType.None);

        EditorGUILayout.Space(7);
        CharacterAIRangedTactics2D tactics = draft.GetComponent<CharacterAIRangedTactics2D>();
        bool tacticsActive = tactics != null && tactics.enabled;
        EditorGUI.BeginChangeCheck();
        bool wantTactics = EditorGUILayout.ToggleLeft("遠距離型の位置取りAIを使用（STEP3）", tacticsActive, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (wantTactics && tactics == null) tactics = draft.AddComponent<CharacterAIRangedTactics2D>();
            if (tactics != null)
            {
                Undo.RecordObject(tactics, "STEP3 Ranged toggle");
                tactics.enabled = wantTactics;
            }
        }
        if (tactics != null && tactics.enabled)
        {
            DrawFields(tactics, "射撃距離", "preferredMinimumDistance",
                "preferredMaximumDistance", "distanceHysteresis");
            DrawFields(tactics, "適正距離内での位置調整", "allowSmallReposition",
                "repositionChance", "repositionInterval", "repositionDuration",
                "repositionSpeedMultiplier");
            EnemyChaser2D chaser = draft.GetComponent<EnemyChaser2D>();
            if (chaser == null)
                EditorGUILayout.HelpBox("遠距離位置取りにはEnemyChaser2Dが必要です。", MessageType.Warning);
            else if (chaser.CombatMovementStyle != EnemyCombatMovementStyle.KeepDistance)
            {
                EditorGUILayout.HelpBox("現在は近接追跡モードです。遠距離型に切り替えてください。", MessageType.Warning);
                if (GUILayout.Button("Combat Movement Style を KeepDistance に設定"))
                {
                    Undo.RecordObject(chaser, "STEP3 set KeepDistance");
                    SerializedObject so = new SerializedObject(chaser);
                    SerializedProperty style = so.FindProperty("combatMovementStyle");
                    if (style != null)
                    {
                        style.enumValueIndex = (int)EnemyCombatMovementStyle.KeepDistance;
                        so.ApplyModifiedProperties();
                    }
                }
            }
            EditorGUILayout.HelpBox("敵対対象の選択や実際の弾発射は既存の派閥・遠距離攻撃コンポーネントを使用。" +
                "STEP3は移動速度と方向だけを調整します。", MessageType.None);
        }
    }
}
#endif
