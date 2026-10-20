using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 救出NPCと洞窟素材おじさんの吹き出しを共通の見た目で描画する。
/// 実際の色・大きさ・背景Sprite・日本語Fontは NpcSpeechCoordinator2D の Inspector で設定する。
/// </summary>
public static class NpcSpeechUnifiedBubbleVisual2D
{
    [Serializable]
    public sealed class Settings
    {
        [Header("共通の吹き出しデザイン")]
        [Tooltip("ここに日本語対応のTMP_FontAssetを1つだけ割り当てます。空欄なら最初に渡されたNPC側のフォントを使用します。")]
        public TMP_FontAsset JapaneseFont;

        [Tooltip("オリジナルの吹き出し画像。未指定なら共通の紙色＋枠＋尻尾を表示します。")]
        public Sprite BubbleBackgroundSprite;

        [Tooltip("カスタムSpriteに枠・尻尾が含まれている場合はOFF。")]
        public bool DrawBorderAndTail = true;

        public Vector2 BubbleSize = new Vector2(300f, 92f);
        [Min(0.0001f)] public float WorldScale = 0.008f;
        [Min(8f)] public float FontSize = 24f;
        public Color BackgroundColor = new Color(1f, 0.97f, 0.88f, 0.97f);
        public Color BorderColor = new Color(0.23f, 0.18f, 0.14f, 0.96f);
        public Color TextColor = new Color(0.16f, 0.12f, 0.10f, 1f);
        [Min(0f)] public float BorderWidth = 4f;
        public int SortingOrder = 150;

        [Header("共通のアニメーション")]
        [Min(0.01f)] public float AppearSeconds = 0.16f;
        [Range(0.1f, 1f)] public float AppearStartScale = 0.8f;
        [Range(1f, 1.5f)] public float AppearOvershoot = 1.08f;
        [Min(0.01f)] public float DisappearSeconds = 0.18f;
        public bool FloatWhileVisible = true;
        [Min(0f)] public float FloatAmplitude = 0.035f;
        [Min(0f)] public float FloatSpeed = 2.4f;
    }

    private static Settings GetSettings() { return NpcSpeechCoordinator2D.SharedAppearance; }

    public static float WorldScale { get { return Mathf.Max(0.0001f, GetSettings().WorldScale); } }
    public static float AppearSeconds { get { return Mathf.Max(0.01f, GetSettings().AppearSeconds); } }
    public static float AppearStartScale { get { return GetSettings().AppearStartScale; } }
    public static float AppearOvershoot { get { return GetSettings().AppearOvershoot; } }
    public static float DisappearSeconds { get { return Mathf.Max(0.01f, GetSettings().DisappearSeconds); } }
    public static bool FloatWhileVisible { get { return GetSettings().FloatWhileVisible; } }
    public static float FloatAmplitude { get { return GetSettings().FloatAmplitude; } }
    public static float FloatSpeed { get { return GetSettings().FloatSpeed; } }

    public static void Apply(
        Canvas canvas, RectTransform root, Image background, TMP_Text messageText,
        TMP_FontAsset fallbackJapaneseFont)
    {
        if (root == null || canvas == null || background == null || messageText == null)
            return;

        Settings s = GetSettings();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = s.SortingOrder;
        root.sizeDelta = new Vector2(
            Mathf.Max(80f, s.BubbleSize.x), Mathf.Max(40f, s.BubbleSize.y));

        // 日本語Assetを共通Managerへ渡す。別々のUIが勝手に違うFontに戻さないため。
        if (s.JapaneseFont == null && fallbackJapaneseFont != null)
            s.JapaneseFont = fallbackJapaneseFont;
        if (s.JapaneseFont != null)
            messageText.font = s.JapaneseFont;

        messageText.fontSize = Mathf.Max(8f, s.FontSize);
        messageText.color = Color.white;
        TextOnlyUIStyle2D.Apply(messageText);
        messageText.alignment = TextAlignmentOptions.Center;
        messageText.enableAutoSizing = false;
        messageText.enableWordWrapping = true;
        messageText.raycastTarget = false;
        root.sizeDelta = new Vector2(root.sizeDelta.x, Mathf.Max(root.sizeDelta.y,
            messageText.GetPreferredValues(messageText.text, root.sizeDelta.x - 32, 0).y + 18));

        RectTransform bgRect = background.rectTransform;
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.anchoredPosition = Vector2.zero;
        float inset = s.DrawBorderAndTail ? Mathf.Max(0f, s.BorderWidth) : 0f;
        bgRect.offsetMin = new Vector2(inset, inset);
        bgRect.offsetMax = new Vector2(-inset, -inset);
        background.color = s.BackgroundColor;
        background.sprite = s.BubbleBackgroundSprite;
        background.type = s.BubbleBackgroundSprite != null &&
            s.BubbleBackgroundSprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        background.raycastTarget = false;
        background.enabled = false;

        RectTransform textRect = messageText.rectTransform;
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(16f, 9f);
        textRect.offsetMax = new Vector2(-16f, -9f);

        SetDecoration(root, background, s);
        root.rotation = Quaternion.identity;
    }

    private static void SetDecoration(RectTransform root, Image background, Settings s)
    {
        RectTransform border = GetOrCreateDecoration(root, "UnifiedBorder");
        RectTransform tailBorder = GetOrCreateDecoration(root, "UnifiedTailBorder");
        RectTransform tail = GetOrCreateDecoration(root, "UnifiedTail");
        bool visible = false; // NPC speech uses text only, including legacy serialized appearances.
        border.gameObject.SetActive(visible);
        tailBorder.gameObject.SetActive(visible);
        tail.gameObject.SetActive(visible);
        if (!visible) return;

        float width = Mathf.Max(80f, s.BubbleSize.x);
        float height = Mathf.Max(40f, s.BubbleSize.y);
        SetImage(border, s.BorderColor, new Vector2(width, height), Vector2.zero, 0f);
        SetImage(tailBorder, s.BorderColor, new Vector2(24f, 24f), new Vector2(0f, -height * 0.5f), 45f);
        SetImage(tail, s.BackgroundColor, new Vector2(19f, 19f), new Vector2(0f, -height * 0.5f + 2f), 45f);

        // 背面→尻尾→枠→背景→テキスト の順にする。
        tailBorder.SetSiblingIndex(0);
        tail.SetSiblingIndex(1);
        border.SetSiblingIndex(2);
        background.rectTransform.SetSiblingIndex(3);
    }

    private static RectTransform GetOrCreateDecoration(Transform root, string name)
    {
        Transform found = root.Find(name);
        if (found != null) return found as RectTransform;
        GameObject created = new GameObject(name, typeof(RectTransform), typeof(Image));
        created.transform.SetParent(root, false);
        Image image = created.GetComponent<Image>();
        image.raycastTarget = false;
        return created.GetComponent<RectTransform>();
    }

    private static void SetImage(RectTransform rect, Color color, Vector2 size, Vector2 pos, float rot)
    {
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = size;
        rect.anchoredPosition = pos;
        rect.localRotation = Quaternion.Euler(0f, 0f, rot);
        Image image = rect.GetComponent<Image>();
        image.color = color;
        image.sprite = null;
        image.type = Image.Type.Simple;
    }
}
