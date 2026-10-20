using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Text-only dialogue: speaker/body below the world, choices above them on the right.</summary>
[DefaultExecutionOrder(120)]
public sealed class DialoguePresentationLayout2D : MonoBehaviour
{
    RectTransform panel, choices, viewport;
    TMP_Text speaker, body;
    Button template, next;
    Canvas canvas;
    ScrollRect scroll;
    string signature;
    Vector2 lastSize;
    Rect lastSafe;
    GameObject lastSelection;
    Button[] outsideButtons;
    public void Bind(CanvasGroup panelGroup, TMP_Text nameText, TMP_Text bodyText, Transform choicesRoot, Button choiceTemplate, Button nextButton)
    {
        panel = (RectTransform)panelGroup.transform; speaker = nameText; body = bodyText;
        choices = (RectTransform)choicesRoot; template = choiceTemplate; next = nextButton;
        canvas = GetComponent<Canvas>();
        outsideButtons = FindObjectsByType<Button>(FindObjectsInactive.Include);
        var background = panel.GetComponent<Image>();
        if (background != null) { background.color = Color.clear; background.raycastTarget = true; }
        viewport = new GameObject("ChoicesViewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image), typeof(ScrollRect)).GetComponent<RectTransform>();
        viewport.SetParent(panel, false);
        viewport.GetComponent<Image>().color = Color.clear;
        choices.SetParent(viewport, false);
        scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = choices;
        scroll.horizontal = false; scroll.vertical = true; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 28;
        var group = choices.GetComponent<VerticalLayoutGroup>();
        if (group != null) group.enabled = false;
        StyleButton(template); StyleButton(next);
        Rebuild();
    }
    void LateUpdate()
    {
        Rebuild();
        var events = UnityEngine.EventSystems.EventSystem.current;
        var selected = events != null ? events.currentSelectedGameObject : null;
        if (selected == lastSelection) return;
        lastSelection = selected;
        if (selected == null || choices == null || scroll == null || !scroll.vertical ||
            !selected.transform.IsChildOf(choices)) return;
        var rect = selected.transform as RectTransform;
        if (rect == null) return;
        float top = -rect.anchoredPosition.y, bottom = top + rect.rect.height;
        float offset = choices.anchoredPosition.y, height = viewport.rect.height;
        if (top < offset) offset = top;
        else if (bottom > offset + height) offset = bottom - height;
        scroll.StopMovement();
        choices.anchoredPosition = new Vector2(0, Mathf.Clamp(offset, 0, Mathf.Max(0, choices.rect.height - height)));
    }
    static void StyleText(TMP_Text text)
    {
        text.color = Color.white; text.raycastTarget = false;
        TextOnlyUIStyle2D.Apply(text);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.overflowMode = TextOverflowModes.Overflow;
    }
    static void StyleButton(Button button)
    {
        var image = button.GetComponent<Image>();
        if (image != null) image.color = Color.clear; // Keep the transparent pointer target.
        var text = button.GetComponentInChildren<TMP_Text>(true);
        if (text == null) return;
        StyleText(text);
        text.rectTransform.anchorMin = Vector2.zero; text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.offsetMin = new Vector2(10, 4); text.rectTransform.offsetMax = new Vector2(-10, -4);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        button.targetGraphic = text;
        var colors = button.colors;
        colors.normalColor = Color.white; colors.highlightedColor = new Color(1, .86f, .48f);
        colors.selectedColor = colors.highlightedColor; colors.pressedColor = new Color(1, .65f, .2f);
        colors.disabledColor = new Color(.65f, .65f, .65f); colors.colorMultiplier = 1;
        button.colors = colors;
    }
    static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchorMin = rect.anchorMax = Vector2.zero; rect.pivot = Vector2.zero;
        rect.anchoredPosition = new Vector2(x, y); rect.sizeDelta = new Vector2(width, height);
    }
    public void Rebuild()
    {
        if (panel == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        panel.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
        panel.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        panel.offsetMin = panel.offsetMax = Vector2.zero;
        string content = speaker.text + "\n" + body.text;
        foreach (var text in choices.GetComponentsInChildren<TMP_Text>()) content += "\n" + text.text;
        if (signature == content && lastSize == panel.rect.size && lastSafe == safe) return;
        signature = content; lastSize = panel.rect.size; lastSafe = safe;
        float w = panel.rect.width, h = panel.rect.height;
        float scale = canvas != null ? Mathf.Max(.1f, canvas.rootCanvas.scaleFactor) : 1;
        float fontSize = Mathf.Max(27, 17 / scale);
        StyleText(body); StyleText(speaker);
        body.fontSize = fontSize; body.enableAutoSizing = false; body.alignment = TextAlignmentOptions.TopLeft;
        float bottom = h * .055f;
        float bodyX = w * .20f;
        // Some existing canvases use fixed pixels, so Town can extend into the dialogue at wide resolutions.
        foreach (var button in outsideButtons)
        {
            if (button == null || !button.isActiveAndEnabled || button.transform.IsChildOf(panel)) continue;
            bool hidden = false;
            foreach (var group in button.GetComponentsInParent<CanvasGroup>()) if (group.alpha < .01f) hidden = true;
            if (hidden) continue;
            var root = button.GetComponentInParent<Canvas>();
            if (root == null) continue;
            root = root.rootCanvas;
            var corners = new Vector3[4]; ((RectTransform)button.transform).GetWorldCorners(corners);
            var sourceCamera = root.renderMode == RenderMode.ScreenSpaceOverlay ? null : root.worldCamera;
            var targetCamera = canvas == null || canvas.rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.rootCanvas.worldCamera;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[0]), targetCamera, out var min);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(panel, RectTransformUtility.WorldToScreenPoint(sourceCamera, corners[2]), targetCamera, out var max);
            min += panel.pivot * panel.rect.size; max += panel.pivot * panel.rect.size;
            if (min.y < h * .35f && max.y > bottom && max.x < w * .40f)
                bodyX = Mathf.Max(bodyX, max.x + 16);
        }
        float bodyWidth = w * .96f - bodyX;
        float bodyHeight = Mathf.Max(fontSize * 1.5f, body.GetPreferredValues(body.text, bodyWidth, 0).y + 8);
        if (bodyHeight > h * .3f) {
            body.enableAutoSizing = true; body.fontSizeMin = 15 / scale; body.fontSizeMax = fontSize;
            bodyHeight = h * .3f;
        }
        float nameY = Mathf.Max(bottom + bodyHeight + 14, h * .17f);
        Place(body.rectTransform, bodyX, bottom, bodyWidth, bodyHeight);
        speaker.fontSize = fontSize + 2; speaker.alignment = TextAlignmentOptions.Center;
        Place(speaker.rectTransform, w * .34f, nameY, w * .30f, fontSize * 1.6f);
        float choiceBottom = nameY + fontSize * 1.6f + 18;
        float choiceWidth = w * .35f;
        float available = Mathf.Max(80, h * .84f - choiceBottom);
        float total = 0;
        foreach (var button in choices.GetComponentsInChildren<Button>(true)) {
            if (button == template || !button.gameObject.activeSelf) continue;
            StyleButton(button);
            var text = button.GetComponentInChildren<TMP_Text>(true); text.fontSize = Mathf.Max(24, 16 / scale);
            float height = Mathf.Max(46, text.GetPreferredValues(text.text, choiceWidth - 20, 0).y + 14);
            var rect = (RectTransform)button.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0, 1); rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0, -total); rect.sizeDelta = new Vector2(choiceWidth, height);
            total += height + 6;
        }
        float visible = Mathf.Min(available, Mathf.Max(48, total));
        Place(viewport, w * .61f, choiceBottom, choiceWidth, visible);
        choices.anchorMin = new Vector2(0, 1); choices.anchorMax = Vector2.one; choices.pivot = new Vector2(.5f, 1);
        choices.sizeDelta = new Vector2(0, total); choices.anchoredPosition = Vector2.zero;
        scroll.vertical = total > visible;
        scroll.verticalNormalizedPosition = 1;
        Place((RectTransform)next.transform, w * .80f, nameY, w * .16f, fontSize * 1.6f);
        next.GetComponentInChildren<TMP_Text>(true).fontSize = Mathf.Max(24, 16 / scale);
    }
}
