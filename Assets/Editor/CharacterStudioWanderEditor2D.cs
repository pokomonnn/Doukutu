#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Character Studio 2Dの既存UIへ通常行動（徘徊・巡回）設定を追加するpartial。</summary>
public sealed partial class CharacterStudioWindow2D
{
    private void DrawWanderAISettings()
    {
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("通常行動AI（待機・徘徊・巡回・帰還）", EditorStyles.boldLabel);

        CharacterWander2D wander = draft.GetComponent<CharacterWander2D>();
        bool active = wander != null && wander.enabled;
        EditorGUI.BeginChangeCheck();
        bool desired = EditorGUILayout.ToggleLeft("徘徊 / 巡回AIを使用", active, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (desired && wander == null)
            {
                // 既存NPC/Enemyの不要なコンポーネントは自動的に消さない。
                if (draft.GetComponent<Rigidbody2D>() == null)
                    draft.AddComponent<Rigidbody2D>();
                if (draft.GetComponent<Collider2D>() == null)
                    draft.AddComponent<CapsuleCollider2D>();
                wander = draft.AddComponent<CharacterWander2D>();
            }
            if (wander != null) wander.enabled = desired;
        }

        if (wander == null || !wander.enabled)
        {
            EditorGUILayout.HelpBox("OFFなら今までのEnemy/NPCの動作は変わりません。", MessageType.None);
            return;
        }

        EditorGUILayout.HelpBox(
            "EnemyChaser2Dあり：追跡がない時だけ徘徊し、移動処理はChaser側が担当。\n" +
            "EnemyChaser2Dなし：このAIが通常移動を担当。Guard戦闘中や運搬中は停止します。",
            MessageType.Info);

        DrawFields(wander, "移動モード・待機", "mode", "moveSpeed", "wanderRadius",
            "minWaitSeconds", "maxWaitSeconds", "arrivalDistance", "returnHomeAfterCombat");

        SerializedObject so = new SerializedObject(wander);
        SerializedProperty modeProp = so.FindProperty("mode");
        if (modeProp != null && modeProp.enumValueIndex == (int)CharacterWanderMode2D.Patrol)
        {
            EditorGUILayout.Space(2);
            EditorGUILayout.LabelField("巡回地点（Prefab内の子オブジェクト）", EditorStyles.boldLabel);
            SerializedProperty points = so.FindProperty("patrolPoints");
            if (points != null) EditorGUILayout.PropertyField(points, true);
            SerializedProperty pingPong = so.FindProperty("pingPongPatrol");
            if (pingPong != null) EditorGUILayout.PropertyField(pingPong, new GUIContent("往復巡回"));
            so.ApplyModifiedProperties();

            if (GUILayout.Button("巡回ポイントを子Objectとして追加"))
            {
                Transform group = draft.transform.Find("AI_PatrolPoints");
                if (group == null)
                {
                    GameObject holder = new GameObject("AI_PatrolPoints");
                    holder.transform.SetParent(draft.transform, false);
                    group = holder.transform;
                }
                SerializedObject refreshed = new SerializedObject(wander);
                SerializedProperty refreshedPoints = refreshed.FindProperty("patrolPoints");
                int index = refreshedPoints != null ? refreshedPoints.arraySize : 0;
                GameObject child = new GameObject("Patrol_" + (index + 1).ToString("00"));
                child.transform.SetParent(group, false);
                child.transform.localPosition = new Vector3(index % 2 == 0 ? -2f : 2f, 0f, 0f);
                if (refreshedPoints != null)
                {
                    refreshedPoints.InsertArrayElementAtIndex(index);
                    refreshedPoints.GetArrayElementAtIndex(index).objectReferenceValue = child.transform;
                    refreshed.ApplyModifiedPropertiesWithoutUndo();
                }
                EditorGUIUtility.PingObject(child);
            }
            EditorGUILayout.HelpBox("巡回地点のX座標へ移動します。Y方向は地形に沿って移動するため、壁の向こう・別フロアは指定しないでね。", MessageType.None);
        }

        DrawFields(wander, "崖・壁安全設定（ChaserなしNPC向け）", "avoidDeepCliffs",
            "groundLayers", "obstacleLayers", "forwardProbe", "cliffProbeDistance",
            "maxSafeDrop", "turnAroundAtWalls");
        if (GetMask(wander, "groundLayers") == 0 && draft.GetComponent<EnemyChaser2D>() == null)
            EditorGUILayout.HelpBox("Ground Layersを指定してください。未設定では崖を検出できません（Groundという名前のLayerがある場合は実行時に自動補完します）。", MessageType.Warning);

        if (draft.GetComponent<FactionGuardCombat2D>() != null)
        {
            DrawFields(wander, "近接Guard NPCの敵対相手への接近", "approachHostilesWithGuard", "guardApproachSpeed");
            EditorGUILayout.HelpBox("Guard+徘徊の場合、敵対対象を発見したら攻撃距離へ近づきます。Guard自身の攻撃範囲より遠ければ接近し、接近不能な崖や壁の前では停止します。", MessageType.Info);
        }

        DrawFields(wander, "救出NPC・運搬時の復帰", "useDropPositionAsHome", "waitAfterDrop");
        DrawFields(wander, "向き・デバッグ", "spriteRenderer", "spriteFacesRightByDefault",
            "drawGizmos", "printStateChanges");
        if (draft.GetComponent<EnemyChaser2D>() != null &&
            draft.GetComponent<FactionGuardCombat2D>() != null)
            EditorGUILayout.HelpBox("EnemyChaser2DとFactionGuardCombat2Dが両方あるとGuardが停止します。巡回する戦闘NPCはChaserの攻撃を使ってください。", MessageType.Warning);
    }
}
#endif
