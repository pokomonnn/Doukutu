#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>STEP2: Character Studioの右側AI欄へ聴覚・探索・警報設定を統合。</summary>
public sealed partial class CharacterStudioWindow2D
{
    private void DrawInvestigationAISettings()
    {
        EditorGUILayout.Space(9);
        EditorGUILayout.LabelField("STEP2  音への反応・見失い探索・仲間への警報", EditorStyles.boldLabel);
        CharacterAIInvestigation2D senses = draft.GetComponent<CharacterAIInvestigation2D>();
        bool enabledNow = senses != null && senses.enabled;
        EditorGUI.BeginChangeCheck();
        bool wanted = EditorGUILayout.ToggleLeft("警戒・探索AIを使用（STEP2）", enabledNow, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (wanted && senses == null) senses = draft.AddComponent<CharacterAIInvestigation2D>();
            if (senses != null)
            {
                Undo.RecordObject(senses, "STEP2 AI toggle");
                senses.enabled = wanted;
            }
        }
        if (senses == null || !senses.enabled)
        {
            EditorGUILayout.HelpBox("OFFならSTEP1の徘徊・派閥戦闘は従来どおり動きます。", MessageType.None);
            return;
        }

        DrawFields(senses, "聴覚と敵対判定", "hearFootsteps", "hearGunshots",
            "onlyInvestigateHostileSources", "investigateUnknownSources");
        DrawFields(senses, "調査・探索", "searchAfterLosingTarget", "investigationMoveSpeed",
            "arrivalDistanceX", "searchRadius", "searchPointCount", "searchPauseSeconds",
            "maxInvestigationSeconds");
        DrawFields(senses, "同派閥への警報", "reportHostileToAllies", "respondToAllyAlerts",
            "allyAlertRadius", "alertCooldownSeconds");
        DrawFields(senses, "表示・ログ", "showGizmo", "debugLogs");

        if (draft.GetComponent<FactionMember2D>() == null)
            EditorGUILayout.HelpBox("FactionMember2Dなし：派閥に応じた音の反応・仲間への警報が利用できません。派閥欄で所属を設定してください。", MessageType.Warning);
        bool chaser = draft.GetComponent<EnemyChaser2D>() != null &&
            draft.GetComponent<EnemyChaser2D>().enabled;
        bool wander = draft.GetComponent<CharacterWander2D>() != null &&
            draft.GetComponent<CharacterWander2D>().enabled;
        if (!chaser && !wander)
        {
            EditorGUILayout.HelpBox("警戒AIは移動しません。調査地点へ移動するには、追跡AIか徘徊／巡回AIが必要です。", MessageType.Warning);
            if (GUILayout.Button("徘徊／巡回AIを追加（移動担当）"))
            {
                if (draft.GetComponent<Rigidbody2D>() == null)
                    draft.AddComponent<Rigidbody2D>();
                if (draft.GetComponent<Collider2D>() == null)
                    draft.AddComponent<CapsuleCollider2D>();
                CharacterWander2D component = draft.GetComponent<CharacterWander2D>();
                if (component == null) component = draft.AddComponent<CharacterWander2D>();
                component.enabled = true;
            }
        }
        EditorGUILayout.HelpBox(
            "音の発信側（Player）にはCharacterAINoiseEmitter2DをInspectorで追加してね。" +
            "足音は横移動から自動通知。銃声はAudioSourceを割り当てるか、発射処理からEmitGunshot()を呼びます。" +
            "遠くの音に気づいても、その時点では攻撃対象として確定しません。",
            MessageType.Info);
    }
}
#endif
