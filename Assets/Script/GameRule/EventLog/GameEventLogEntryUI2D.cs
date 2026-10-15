using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// イベントログ1件の描画と、スライド・フェードのアニメーションを担当します。
/// GameEventLogManager2Dがランタイムで自動生成します。手動アタッチ不要です。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public sealed class GameEventLogEntryUI2D : MonoBehaviour
{
    private RectTransform rect;
    private CanvasGroup group;
    private float targetY;
    private float velocityY;
    private float birthTime;
    private bool initialized;

    public bool IsExpired { get; private set; }

    public void Initialize(GameEventLogType type, string message, float width, float height,
        TMP_FontAsset font, float textSize, Color panelColor, Color textColor, Color accentColor)
    {
        rect = GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.sizeDelta = new Vector2(width, height);
        rect.anchoredPosition = new Vector2(0f, height + 30f);

        Image background = gameObject.AddComponent<Image>();
        background.color = panelColor;
        background.raycastTarget = false;
        group = gameObject.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject stripeObject = new GameObject("Accent", typeof(RectTransform), typeof(Image));
        RectTransform stripe = stripeObject.GetComponent<RectTransform>();
        stripe.SetParent(rect, false);
        stripe.anchorMin = new Vector2(0f, 0f);
        stripe.anchorMax = new Vector2(0f, 1f);
        stripe.pivot = new Vector2(0f, 0.5f);
        stripe.sizeDelta = new Vector2(6f, 0f);
        stripe.anchoredPosition = Vector2.zero;
        Image stripeImage = stripeObject.GetComponent<Image>();
        stripeImage.color = accentColor;
        stripeImage.raycastTarget = false;

        var typeText = MakeText("Category", GetCategory(type), font, 16f, accentColor);
        RectTransform typeRect = typeText.rectTransform;
        typeRect.anchorMin = new Vector2(0f, 0f);
        typeRect.anchorMax = new Vector2(0f, 1f);
        typeRect.offsetMin = new Vector2(17f, 2f);
        typeRect.offsetMax = new Vector2(90f, -2f);

        var bodyText = MakeText("Message", message, font, textSize, textColor);
        RectTransform bodyRect = bodyText.rectTransform;
        bodyRect.anchorMin = new Vector2(0f, 0f);
        bodyRect.anchorMax = new Vector2(1f, 1f);
        bodyRect.offsetMin = new Vector2(90f, 3f);
        bodyRect.offsetMax = new Vector2(-10f, -3f);

        birthTime = Time.unscaledTime;
        targetY = 0f;
        velocityY = 0f;
        IsExpired = false;
        initialized = true;
    }

    private TextMeshProUGUI MakeText(string childName, string content,
        TMP_FontAsset font, float size, Color color)
    {
        var textObject = new GameObject(childName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(rect, false);
        var text = textObject.GetComponent<TextMeshProUGUI>();
        text.text = content;
        text.font = font != null ? font : TMP_Settings.defaultFontAsset;
        text.fontSize = size;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        text.raycastTarget = false;
        return text;
    }

    private static string GetCategory(GameEventLogType type)
    {
        switch (type)
        {
            case GameEventLogType.Money: return "入手";
            case GameEventLogType.Treatment: return "治療";
            case GameEventLogType.WeaponBroken: return "破損";
            case GameEventLogType.SkillCard: return "スキル";
            case GameEventLogType.Item: return "アイテム";
            case GameEventLogType.Rescue: return "救出";
            case GameEventLogType.Mission: return "達成";
            default: return "通知";
        }
    }

    public void SetTargetY(float y) => targetY = y;

    public void Animate(float unscaledDeltaTime, float slideSeconds,
        float visibleSeconds, float fadeInSeconds, float fadeOutSeconds)
    {
        if (!initialized || IsExpired || rect == null || group == null)
            return;

        float delta = Mathf.Max(0f, unscaledDeltaTime);
        Vector2 position = rect.anchoredPosition;
        position.y = Mathf.SmoothDamp(position.y, targetY, ref velocityY,
            Mathf.Max(0.02f, slideSeconds), Mathf.Infinity, delta);
        rect.anchoredPosition = position;

        float elapsed = Time.unscaledTime - birthTime;
        float appear = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, fadeInSeconds));
        float disappear = Mathf.Clamp01((elapsed - visibleSeconds) / Mathf.Max(0.01f, fadeOutSeconds));
        group.alpha = Mathf.Min(appear, 1f - disappear);
        IsExpired = elapsed >= visibleSeconds + fadeOutSeconds;
    }
}
