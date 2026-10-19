#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem.UI;
#endif

/// <summary>実行確認用uGUIをSceneに生成。既存Canvasや既存会話UIは変更しない。</summary>
public static class DialogueStudioUICreator2D
{
    [MenuItem("Tools/2D Shooter/Dialogue Studio/テスト用会話UIをシーンに作成")]
    public static void CreateInScene()
    {
        if (Application.isPlaying)
        {
            EditorUtility.DisplayDialog("停止してください", "UI作成はPlayモードを終了してから行ってください。", "OK");
            return;
        }
        DialoguePresenter2D existing = Object.FindFirstObjectByType<DialoguePresenter2D>();
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing);
            EditorUtility.DisplayDialog("既にあります", "SceneにDialoguePresenter2Dが存在します。既存UIを選択しました。", "OK");
            return;
        }

        GameObject canvasGo = new GameObject("DialogueStudio2D_Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Undo.RegisterCreatedObjectUndo(canvasGo, "会話UIを作成");
        Canvas canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 150;
        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelGo = CreateUIObject("会話パネル", canvasGo.transform);
        RectTransform panelRect = panelGo.GetComponent<RectTransform>();
        SetRect(panelRect, new Vector2(0.14f, 0.045f), new Vector2(0.86f, 0.47f), Vector2.zero, Vector2.zero);
        Image panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0.075f, 0.10f, 0.13f, 0.94f);
        CanvasGroup cg = panelGo.AddComponent<CanvasGroup>();

        TMP_Text speaker = CreateText("話者名", panelGo.transform, "NPC", 30, FontStyles.Bold);
        SetRect((RectTransform)speaker.transform, new Vector2(0.035f, 0.81f), new Vector2(0.96f, 0.95f), Vector2.zero, Vector2.zero);
        TMP_Text body = CreateText("会話本文", panelGo.transform, "会話の本文がここに表示されます。", 27);
        SetRect((RectTransform)body.transform, new Vector2(0.035f, 0.49f), new Vector2(0.96f, 0.80f), Vector2.zero, Vector2.zero);
        body.enableWordWrapping = true;

        GameObject choiceRoot = CreateUIObject("選択肢一覧", panelGo.transform);
        RectTransform choiceRect = choiceRoot.GetComponent<RectTransform>();
        SetRect(choiceRect, new Vector2(0.04f, 0.06f), new Vector2(0.77f, 0.46f), Vector2.zero, Vector2.zero);
        VerticalLayoutGroup layout = choiceRoot.AddComponent<VerticalLayoutGroup>();
        layout.childAlignment = TextAnchor.UpperLeft;
        layout.spacing = 6f;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;

        Button choiceButton = CreateButton("選択肢テンプレート", choiceRoot.transform, "選択肢", 24);
        LayoutElement choiceLayout = choiceButton.gameObject.AddComponent<LayoutElement>();
        choiceLayout.minHeight = 40f;
        choiceLayout.preferredHeight = 45f;
        choiceButton.gameObject.SetActive(false);

        Button nextButton = CreateButton("次へ", panelGo.transform, "次へ ▶", 24);
        SetRect((RectTransform)nextButton.transform, new Vector2(0.80f, 0.08f), new Vector2(0.96f, 0.27f), Vector2.zero, Vector2.zero);

        DialoguePresenter2D presenter = canvasGo.AddComponent<DialoguePresenter2D>();
        SerializedObject so = new SerializedObject(presenter);
        so.FindProperty("panel").objectReferenceValue = cg;
        so.FindProperty("speakerText").objectReferenceValue = speaker;
        so.FindProperty("bodyText").objectReferenceValue = body;
        so.FindProperty("choicesRoot").objectReferenceValue = choiceRoot.transform;
        so.FindProperty("choiceButtonTemplate").objectReferenceValue = choiceButton;
        so.FindProperty("nextButton").objectReferenceValue = nextButton;
        so.ApplyModifiedPropertiesWithoutUndo();

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject eventGo = new GameObject("DialogueStudio2D_EventSystem", typeof(EventSystem));
            Undo.RegisterCreatedObjectUndo(eventGo, "EventSystemを作成");
#if ENABLE_INPUT_SYSTEM
            eventGo.AddComponent<InputSystemUIInputModule>();
#else
            eventGo.AddComponent<StandaloneInputModule>();
#endif
        }
        Selection.activeGameObject = canvasGo;
        EditorSceneManager.MarkSceneDirty(canvasGo.scene);
        EditorUtility.DisplayDialog("会話UIを作成", "テストUIをシーンに作成しました。文字が□になる場合は各TMP_Textに日本語対応のFont Assetを設定してください。", "OK");
    }

    private static GameObject CreateUIObject(string name, Transform parent)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    private static TMP_Text CreateText(string name, Transform parent, string content, int fontSize, FontStyles style = FontStyles.Normal)
    {
        GameObject go = CreateUIObject(name, parent);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        if (TMP_Settings.defaultFontAsset != null) text.font = TMP_Settings.defaultFontAsset;
        text.text = content;
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = true;
        text.raycastTarget = false;
        return text;
    }

    private static Button CreateButton(string name, Transform parent, string content, int fontSize)
    {
        GameObject go = CreateUIObject(name, parent);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0.17f, 0.34f, 0.38f, 0.96f);
        Button button = go.AddComponent<Button>();
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.50f, 0.88f, 0.79f);
        button.colors = colors;
        TMP_Text label = CreateText("文字", go.transform, content, fontSize);
        label.alignment = TextAlignmentOptions.Center;
        SetRect((RectTransform)label.transform, Vector2.zero, Vector2.one, new Vector2(7, 2), new Vector2(-7, -2));
        return button;
    }
}
#endif
