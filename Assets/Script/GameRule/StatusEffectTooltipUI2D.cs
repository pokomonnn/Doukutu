using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// InventoryItemTooltipUIと同じように、カーソルの右側へ詳細を表示。
/// 右端では左側へ、上下端では画面内へ位置補正。既存InventoryUIは変更しません。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(Image))]
public class StatusEffectTooltipUI2D : MonoBehaviour
{
    [SerializeField] private Vector2 cursorOffset = new Vector2(20f, 12f);
    [SerializeField] private float panelWidth = 340f;

    private RectTransform panel;
    private RectTransform canvasRect;
    private Canvas rootCanvas;
    private CanvasGroup canvasGroup;
    private TMP_Text nameText;
    private TMP_Text categoryText;
    private TMP_Text descriptionText;
    private ActiveStatusEffect2D showingEffect;
    private bool visible;

    public void Initialize(Canvas canvas, TMP_FontAsset japaneseFont)
    {
        rootCanvas = canvas.rootCanvas;
        canvasRect = rootCanvas.transform as RectTransform;
        panel = GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0.5f);
        panel.anchorMax = new Vector2(0.5f, 0.5f);
        panel.pivot = new Vector2(0f, 1f);
        panel.sizeDelta = new Vector2(panelWidth, 175f);

        Image background = GetComponent<Image>();
        background.color = new Color(0.06f, 0.07f, 0.08f, 0.96f);
        background.raycastTarget = false;

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        nameText = MakeText("EffectName", 23f, FontStyles.Bold,
            Color.white, japaneseFont);
        categoryText = MakeText("Category", 14f, FontStyles.Bold,
            new Color(0.70f, 0.82f, 0.89f, 1f), japaneseFont);
        descriptionText = MakeText("EffectDescription", 17f, FontStyles.Normal,
            new Color(0.88f, 0.88f, 0.88f, 1f), japaneseFont);

        SetRect(nameText.rectTransform, new Vector2(16f, -15f),
            new Vector2(panelWidth - 32f, 35f));
        SetRect(categoryText.rectTransform, new Vector2(16f, -56f),
            new Vector2(panelWidth - 32f, 24f));
        SetRect(descriptionText.rectTransform, new Vector2(16f, -90f),
            new Vector2(panelWidth - 32f, 90f));
        Hide();
    }

    public void Show(ActiveStatusEffect2D effect)
    {
        if (effect == null || effect.Data == null || canvasRect == null) return;
        showingEffect = effect;
        visible = true;
        nameText.text = effect.Data.DisplayName;
        categoryText.text = effect.Data.Category == StatusEffectCategory2D.Buff
            ? "バフ" : "デバフ";
        categoryText.color = effect.Data.Category == StatusEffectCategory2D.Buff
            ? new Color(0.47f, 0.9f, 0.7f, 1f)
            : new Color(1f, 0.50f, 0.52f, 1f);
        descriptionText.text = string.IsNullOrWhiteSpace(effect.Data.Description)
            ? "説明なし" : effect.Data.Description;

        // 長めの日本語説明にも対応。
        float textHeight = descriptionText.GetPreferredValues(
            descriptionText.text, panelWidth - 32f, 0f).y;
        float height = Mathf.Clamp(108f + textHeight, 155f, 380f);
        panel.sizeDelta = new Vector2(panelWidth, height);
        SetRect(descriptionText.rectTransform, new Vector2(16f, -90f),
            new Vector2(panelWidth - 32f, height - 104f));

        canvasGroup.alpha = 1f;
        transform.SetAsLastSibling();
        UpdatePosition();
    }

    public void HideIf(ActiveStatusEffect2D effect)
    {
        if (effect == showingEffect) Hide();
    }

    public void Hide()
    {
        visible = false;
        showingEffect = null;
        if (canvasGroup != null) canvasGroup.alpha = 0f;
    }

    private void LateUpdate()
    {
        if (visible) UpdatePosition();
    }

    private void UpdatePosition()
    {
        if (canvasRect == null || rootCanvas == null || panel == null) return;

        Camera uiCamera = rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null : rootCanvas.worldCamera;
        float scale = Mathf.Max(rootCanvas.scaleFactor, 0.0001f);
        bool placeLeft = Input.mousePosition.x + cursorOffset.x + panel.rect.width * scale
            > Screen.width;

        panel.pivot = new Vector2(placeLeft ? 1f : 0f, 1f);
        Vector2 cursor = Input.mousePosition;
        Vector2 screenPosition = new Vector2(
            cursor.x + (placeLeft ? -Mathf.Abs(cursorOffset.x) : Mathf.Abs(cursorOffset.x)),
            cursor.y + cursorOffset.y);

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPosition, uiCamera, out Vector2 local)) return;

        Rect bounds = canvasRect.rect;
        float minX = placeLeft ? bounds.xMin + panel.rect.width + 5f : bounds.xMin + 5f;
        float maxX = placeLeft ? bounds.xMax - 5f : bounds.xMax - panel.rect.width - 5f;
        float minY = bounds.yMin + panel.rect.height + 5f;
        float maxY = bounds.yMax - 5f;
        local.x = Mathf.Clamp(local.x, Mathf.Min(minX, maxX), Mathf.Max(minX, maxX));
        local.y = Mathf.Clamp(local.y, Mathf.Min(minY, maxY), Mathf.Max(minY, maxY));
        panel.anchoredPosition = local;
    }

    private TMP_Text MakeText(string childName, float fontSize, FontStyles style,
        Color color, TMP_FontAsset japaneseFont)
    {
        var go = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(transform, false);
        var text = go.GetComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = color;
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Truncate;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.raycastTarget = false;
        if (japaneseFont != null) text.font = japaneseFont;
        return text;
    }

    private static void SetRect(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
