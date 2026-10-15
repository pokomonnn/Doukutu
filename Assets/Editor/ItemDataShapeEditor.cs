#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// ItemDataおよび武器・弾薬など派生ItemDataのInspectorにセル編集UIを追加。
/// Width/Heightは既存ItemDataの値を使い、7x10グリッドは変更しません。
/// </summary>
[CustomEditor(typeof(ItemData), true)]
[CanEditMultipleObjects]
public sealed class ItemDataShapeEditor : Editor
{
    private SerializedProperty enabledProperty;
    private SerializedProperty cellsProperty;
    private SerializedProperty widthProperty;
    private SerializedProperty heightProperty;

    private void OnEnable()
    {
        enabledProperty = serializedObject.FindProperty("useCustomShape");
        cellsProperty = serializedObject.FindProperty("customShapeCells");
        widthProperty = serializedObject.FindProperty("width");
        heightProperty = serializedObject.FindProperty("height");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();

        EditorGUILayout.Space(12f);
        EditorGUILayout.LabelField("アイテム形状の編集", EditorStyles.boldLabel);

        // 既存のItemDataがまだパッチ前の場合も、Inspectorエラーにしない。
        if (enabledProperty == null || cellsProperty == null)
        {
            EditorGUILayout.HelpBox(
                "Tools > Inventory > Install Irregular Item Shapes から導入してください。",
                MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        if (serializedObject.isEditingMultipleObjects)
        {
            EditorGUILayout.HelpBox("カスタム形状の編集は1つのItemDataずつ行ってください。",
                MessageType.Info);
            serializedObject.ApplyModifiedProperties();
            return;
        }

        bool wasEnabled = enabledProperty.boolValue;
        bool nowEnabled = EditorGUILayout.ToggleLeft("カスタム形状を使う", wasEnabled);
        if (wasEnabled != nowEnabled)
        {
            enabledProperty.boolValue = nowEnabled;
            if (nowEnabled && cellsProperty.arraySize == 0)
                FillRectangle();
        }

        if (nowEnabled)
        {
            int w = Mathf.Max(1, widthProperty.intValue);
            int h = Mathf.Max(1, heightProperty.intValue);
            EditorGUILayout.HelpBox(
                "■=占有するマス、□=空きマス。クリックで切り替えできます。" +
                "\n左上が (0,0)。通常の四角形は『カスタム形状を使う』をOFFにしてください。",
                MessageType.None);

            if (w > 12 || h > 12)
                EditorGUILayout.HelpBox("表示は最大12×12マスまでです。幅・高さを調整してください。", MessageType.Warning);

            int visibleW = Mathf.Min(w, 12);
            int visibleH = Mathf.Min(h, 12);
            Color before = GUI.backgroundColor;
            for (int y = 0; y < visibleH; y++)
            {
                EditorGUILayout.BeginHorizontal();
                for (int x = 0; x < visibleW; x++)
                {
                    var cell = new Vector2Int(x, y);
                    int index = IndexOf(cell);
                    bool active = index >= 0;
                    GUI.backgroundColor = active
                        ? new Color(0.25f, 0.65f, 0.82f)
                        : new Color(0.55f, 0.55f, 0.55f);
                    if (GUILayout.Button(active ? "■" : " ", GUILayout.Width(29), GUILayout.Height(27)))
                    {
                        if (active && cellsProperty.arraySize > 1)
                            cellsProperty.DeleteArrayElementAtIndex(index);
                        else if (!active)
                            AddCell(cell);
                    }
                }
                EditorGUILayout.EndHorizontal();
            }
            GUI.backgroundColor = before;

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("すべて埋める")) FillRectangle();
            if (GUILayout.Button("形をクリア（左上1マス）"))
            {
                cellsProperty.ClearArray();
                AddCell(Vector2Int.zero);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.LabelField("占有マス数", cellsProperty.arraySize.ToString());
        }

        serializedObject.ApplyModifiedProperties();
    }

    private int IndexOf(Vector2Int cell)
    {
        for (int i = 0; i < cellsProperty.arraySize; i++)
            if (cellsProperty.GetArrayElementAtIndex(i).vector2IntValue == cell)
                return i;
        return -1;
    }

    private void AddCell(Vector2Int cell)
    {
        int index = cellsProperty.arraySize;
        cellsProperty.InsertArrayElementAtIndex(index);
        cellsProperty.GetArrayElementAtIndex(index).vector2IntValue = cell;
    }

    private void FillRectangle()
    {
        int w = Mathf.Max(1, widthProperty.intValue);
        int h = Mathf.Max(1, heightProperty.intValue);
        cellsProperty.ClearArray();
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                AddCell(new Vector2Int(x, y));
    }
}
#endif
