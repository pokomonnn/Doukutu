#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(FactionMember2D))]
[CanEditMultipleObjects]
public sealed class FactionMember2DEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        SerializedProperty id = serializedObject.FindProperty("factionId");
        SerializedProperty type = serializedObject.FindProperty("monsterType");
        SerializedProperty retaliate = serializedObject.FindProperty("retaliateWhenAttacked");
        EditorGUILayout.LabelField("NPC / Enemyの所属派閥", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(id, new GUIContent("派閥ID"));
        if (id.hasMultipleDifferentValues || FactionId2D.Same(id.stringValue, FactionId2D.Monsters))
            EditorGUILayout.PropertyField(type, new GUIContent("モンスター種類"));
        EditorGUILayout.PropertyField(retaliate, new GUIContent("攻撃されたら反撃（STEP3用）"));
        serializedObject.ApplyModifiedProperties();

        if (!serializedObject.isEditingMultipleObjects && target is FactionMember2D member)
        {
            if (string.IsNullOrEmpty(member.FactionId))
                EditorGUILayout.HelpBox("派閥IDが未設定です。STEP2エディターから一括設定できます。", MessageType.Warning);
            if (FactionId2D.Same(member.FactionId, FactionId2D.Monsters) &&
                member.MonsterType == FactionMonsterType2D.None)
                EditorGUILayout.HelpBox("スライム/スケルトンの種類を選ぶと、ドワーフの攻撃対象を区別できます。", MessageType.Info);
            if (Application.isPlaying && !member.IsPlayer)
            {
                FactionDecision2D d = FactionHostility2D.EvaluateToPlayer(member);
                EditorGUILayout.HelpBox("対プレイヤー: " + (d.IsHostile ? "敵対" : "中立") + "\n" + d.Reason,
                    d.IsHostile ? MessageType.Warning : MessageType.Info);
            }
        }
        EditorGUILayout.Space(5);
        EditorGUILayout.HelpBox("このコンポーネント単独では攻撃AIは切り替わりません。STEP3で敵の索敵・攻撃処理に接続します。", MessageType.None);
    }
}
#endif
