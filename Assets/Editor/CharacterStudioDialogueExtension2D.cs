#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>既存Character Studioの作業用Prefabへ会話データを設定する拡張部分。</summary>
public sealed partial class CharacterStudioWindow2D
{
    [SerializeField] private bool dialogueSectionExpanded = true;

    private void DrawCharacterDialogueSettings()
    {
        GUILayout.Space(6);
        dialogueSectionExpanded = EditorGUILayout.Foldout(dialogueSectionExpanded, "NPC / Enemy 会話（Dialogue Studio 2D）", true, EditorStyles.foldoutHeader);
        if (!dialogueSectionExpanded || draft == null) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        DialogueActor2D actor = draft.GetComponent<DialogueActor2D>();
        if (actor == null)
        {
            EditorGUILayout.HelpBox("このPrefabにはDialogueActor2Dがありません。追加すると会話データを割り当てられます。既存の会話スクリプトは変更されません。", MessageType.None);
            if (GUILayout.Button("会話コンポーネントを追加")) actor = draft.AddComponent<DialogueActor2D>();
        }
        if (actor != null)
        {
            SerializedObject so = new SerializedObject(actor);
            so.Update();
            SerializedProperty dataProp = so.FindProperty("dialogue");
            EditorGUILayout.PropertyField(dataProp, new GUIContent("会話データ"));
            EditorGUILayout.PropertyField(so.FindProperty("useEKeyInteraction"), new GUIContent("Eキーで会話（既存会話と重複注意）"));
            if (so.FindProperty("useEKeyInteraction").boolValue)
                EditorGUILayout.PropertyField(so.FindProperty("interactionDistance"), new GUIContent("会話できる距離"));
            EditorGUILayout.PropertyField(so.FindProperty("preventWhenDead"), new GUIContent("死亡中は会話禁止"));
            EditorGUILayout.PropertyField(so.FindProperty("preventWhenCarried"), new GUIContent("運搬中は会話禁止"));
            EditorGUILayout.PropertyField(so.FindProperty("preventWhenHostileToPlayer"), new GUIContent("プレイヤー敵対中は会話禁止"));
            so.ApplyModifiedProperties();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dialogue Studioを開く"))
                DialogueStudioWindow2D.OpenWithData(dataProp.objectReferenceValue as DialogueData2D);
            if (GUILayout.Button("設定した会話データを選択") && dataProp.objectReferenceValue != null)
                EditorGUIUtility.PingObject(dataProp.objectReferenceValue);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("STEP4 取引・ミッション・アイテム連携", EditorStyles.boldLabel);
            DialogueActionContext2D context = draft.GetComponent<DialogueActionContext2D>();
            if (context == null)
            {
                EditorGUILayout.HelpBox("必要な参照は自動検索します。複数のMissionManagerやPlayer用Inventoryがある場合はSTEP4参照を追加し手動指定できます。", MessageType.Info);
                if (GUILayout.Button("STEP4参照コンポーネントを追加"))
                    context = draft.AddComponent<DialogueActionContext2D>();
            }
            if (context != null)
            {
                SerializedObject contextSO = new SerializedObject(context);
                contextSO.Update();
                EditorGUILayout.PropertyField(contextSO.FindProperty("shop"), new GUIContent("ショップ（任意）"));
                EditorGUILayout.PropertyField(contextSO.FindProperty("missionManager"), new GUIContent("MissionManager（任意）"));
                EditorGUILayout.PropertyField(contextSO.FindProperty("playerInventory"), new GUIContent("Player Inventory（任意）"));
                contextSO.ApplyModifiedProperties();
            }
            EditorGUILayout.HelpBox("ショップが同じEキーで自動起動してしまう場合はDungeonNpcShop2DのInteraction KeyをNoneへ変更してください。", MessageType.Warning);
            EditorGUILayout.HelpBox("会話の実行にはシーンにDialoguePresenter2Dが必要です。既存会話のEキーを使っているNPCは、このスクリプトのEキーをOFFにしたままにしてください。", MessageType.Info);
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(5);
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        EditorGUILayout.LabelField("STEP5 NPC・Enemy同士の会話", EditorStyles.boldLabel);
        DialogueAmbientTalk2D ambient = draft.GetComponent<DialogueAmbientTalk2D>();
        if (ambient == null)
        {
            EditorGUILayout.HelpBox("近くのNPC同士で立ち話させる場合に追加します。既存の会話・派閥AI・ショップは変更しません。", MessageType.Info);
            if (GUILayout.Button("STEP5 立ち話コンポーネントを追加"))
                ambient = draft.AddComponent<DialogueAmbientTalk2D>();
        }
        if (ambient != null)
        {
            SerializedObject ambientSO = new SerializedObject(ambient);
            ambientSO.Update();
            SerializedProperty talkData = ambientSO.FindProperty("talkData");
            EditorGUILayout.PropertyField(talkData, new GUIContent("立ち話データ"));
            EditorGUILayout.PropertyField(ambientSO.FindProperty("allowDifferentNonHostileFactions"), new GUIContent("異なる非敵対派閥とも会話"));
            EditorGUILayout.PropertyField(ambientSO.FindProperty("talkRadius"), new GUIContent("相手との会話開始距離"));
            EditorGUILayout.PropertyField(ambientSO.FindProperty("startProbability"), new GUIContent("発生確率"));
            EditorGUILayout.PropertyField(ambientSO.FindProperty("scanInterval"), new GUIContent("相手を探す間隔（秒）"));
            EditorGUILayout.PropertyField(ambientSO.FindProperty("minimumRestSeconds"), new GUIContent("次の会話まで最小休憩秒数"));
            EditorGUILayout.PropertyField(ambientSO.FindProperty("maximumRestSeconds"), new GUIContent("次の会話まで最大休憩秒数"));
            EditorGUILayout.PropertyField(ambientSO.FindProperty("japaneseFont"), new GUIContent("日本語フォント（TMP）"));
            ambientSO.ApplyModifiedProperties();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("STEP5 会話管理を開く"))
                DialogueAmbientTalkStudioWindow2D.OpenWithData(talkData.objectReferenceValue as DialogueAmbientTalkData2D);
            if (GUILayout.Button("設定データを選択") && talkData.objectReferenceValue != null)
                EditorGUIUtility.PingObject(talkData.objectReferenceValue);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox("双方のキャラにDialogueAmbientTalk2Dを追加してください。同派閥なら会話可能です。", MessageType.None);
        }
        EditorGUILayout.EndVertical();
    }
}
#endif
