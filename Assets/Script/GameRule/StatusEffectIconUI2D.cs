using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 1個分の円形アイコン。
/// 上からの並び順変更を滑らかに追従し、追加時フェードイン、削除時フェードアウト。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform), typeof(CanvasGroup), typeof(Image))]
public class StatusEffectIconUI2D : MonoBehaviour,
    IPointerEnterHandler, IPointerExitHandler
{
    private static Sprite circleSprite;
    private static Sprite ringSprite;

    private StatusEffectHUD2D hud;
    private ActiveStatusEffect2D effect;
    private RectTransform rect;
    private CanvasGroup canvasGroup;
    private Image timerRing;
    private Image ringBackground;
    private Image iconImage;
    private TMP_Text fallbackText;

    private float targetY;
    private float yVelocity;
    private float animationSeconds;
    private float moveSmoothSeconds;
    private bool leaving;
    private bool initialized;
    private float scaleFactor;

    public StatusEffectData2D Data => effect != null ? effect.Data : null;
    public bool IsLeaving => leaving;

    public void Initialize(
        StatusEffectHUD2D owner,
        ActiveStatusEffect2D active,
        float size,
        float appearSeconds,
        float moveSmooth,
        TMP_FontAsset japaneseFont)
    {
        hud = owner;
        effect = active;
        animationSeconds = Mathf.Max(0.01f, appearSeconds);
        moveSmoothSeconds = Mathf.Max(0.02f, moveSmooth);

        rect = GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(1f, 1f);
        rect.anchorMax = new Vector2(1f, 1f);
        rect.pivot = new Vector2(1f, 1f);
        rect.sizeDelta = new Vector2(size, size);

        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = false;
        scaleFactor = 0.78f;
        rect.localScale = Vector3.one * scaleFactor;

        Image background = GetComponent<Image>();
        background.sprite = GetCircleSprite();
        background.color = new Color(0.07f, 0.08f, 0.1f, 0.90f);
        background.raycastTarget = true;

        ringBackground = MakeChildImage("RingBackground", 0f);
        ringBackground.sprite = GetRingSprite();
        ringBackground.color = new Color(1f, 1f, 1f, 0.18f);

        timerRing = MakeChildImage("RemainingTimeRing", 0f);
        timerRing.sprite = GetRingSprite();
        timerRing.color = active.Data.Category == StatusEffectCategory2D.Buff
            ? new Color(0.35f, 0.87f, 0.62f, 1f)
            : new Color(1f, 0.35f, 0.37f, 1f);
        timerRing.type = Image.Type.Filled;
        timerRing.fillMethod = Image.FillMethod.Radial360;
        timerRing.fillOrigin = (int)Image.Origin360.Top;
        timerRing.fillClockwise = true;

        // 永続効果は時間が減らないため円ゲージを表示しません。
        ringBackground.gameObject.SetActive(active.IsTimed);
        timerRing.gameObject.SetActive(active.IsTimed);

        iconImage = MakeChildImage("EffectIcon", size * 0.19f);
        iconImage.sprite = active.Data.Icon;
        iconImage.color = Color.white;
        iconImage.preserveAspect = true;
        iconImage.gameObject.SetActive(active.Data.Icon != null);

        if (active.Data.Icon == null)
        {
            var textObject = new GameObject("NoIconQuestion", typeof(RectTransform),
                typeof(TextMeshProUGUI));
            textObject.transform.SetParent(rect, false);
            fallbackText = textObject.GetComponent<TextMeshProUGUI>();
            fallbackText.text = japaneseFont != null && !string.IsNullOrEmpty(active.Data.DisplayName)
                ? active.Data.DisplayName.Substring(0, 1) : "!";
            fallbackText.fontSize = size * 0.55f;
            fallbackText.alignment = TextAlignmentOptions.Center;
            fallbackText.color = Color.white;
            fallbackText.raycastTarget = false;
            if (japaneseFont != null) fallbackText.font = japaneseFont;
            Stretch(fallbackText.rectTransform, 0f);
        }

        initialized = true;
        UpdateRing();
    }

    public void SetPositionIndex(int index, float size, float spacing, bool firstPlacement)
    {
        targetY = -index * (size + spacing);
        if (firstPlacement)
        {
            // 少し上から現れて指定位置へ下がる。
            rect.anchoredPosition = new Vector2(0f, targetY + 16f);
        }
    }

    public void BeginRemoval()
    {
        if (leaving) return;
        leaving = true;
        if (canvasGroup != null) canvasGroup.blocksRaycasts = false;
        if (hud != null) hud.HideTooltipFor(effect);
    }

    private void Update()
    {
        if (!initialized) return;

        float dt = Time.unscaledDeltaTime;
        float y = Mathf.SmoothDamp(rect.anchoredPosition.y, targetY,
            ref yVelocity, moveSmoothSeconds, Mathf.Infinity, dt);
        rect.anchoredPosition = new Vector2(0f, y);

        float targetAlpha = leaving ? 0f : 1f;
        canvasGroup.alpha = Mathf.MoveTowards(canvasGroup.alpha,
            targetAlpha, dt / animationSeconds);

        float targetScale = leaving ? 0.78f : 1f;
        scaleFactor = Mathf.MoveTowards(scaleFactor, targetScale,
            dt * (0.22f / animationSeconds + 0.9f));
        rect.localScale = Vector3.one * scaleFactor;
        canvasGroup.blocksRaycasts = !leaving && canvasGroup.alpha > 0.55f;

        if (leaving)
        {
            if (canvasGroup.alpha <= 0.001f) Destroy(gameObject);
            return;
        }

        UpdateRing();
    }

    private void UpdateRing()
    {
        if (effect == null) return;
        // 既存の効果が更新され、永続→時間付きに変化した場合も反映。
        if (ringBackground != null && ringBackground.gameObject.activeSelf != effect.IsTimed)
            ringBackground.gameObject.SetActive(effect.IsTimed);
        if (timerRing != null)
        {
            if (timerRing.gameObject.activeSelf != effect.IsTimed)
                timerRing.gameObject.SetActive(effect.IsTimed);
            if (effect.IsTimed) timerRing.fillAmount = effect.Remaining01;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!leaving && hud != null && effect != null)
            hud.ShowTooltipFor(effect);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (hud != null) hud.HideTooltipFor(effect);
    }

    private void OnDisable()
    {
        if (hud != null) hud.HideTooltipFor(effect);
    }

    private Image MakeChildImage(string childName, float padding)
    {
        var go = new GameObject(childName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(rect, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        Stretch(img.rectTransform, padding);
        return img;
    }

    private static void Stretch(RectTransform target, float padding)
    {
        target.anchorMin = Vector2.zero;
        target.anchorMax = Vector2.one;
        target.offsetMin = new Vector2(padding, padding);
        target.offsetMax = new Vector2(-padding, -padding);
    }

    // ランタイムで円と円環画像を1度だけ生成します。素材のインポートは不要です。
    private static Sprite GetCircleSprite()
    {
        if (circleSprite == null) circleSprite = MakeCircleSprite(false);
        return circleSprite;
    }

    private static Sprite GetRingSprite()
    {
        if (ringSprite == null) ringSprite = MakeCircleSprite(true);
        return ringSprite;
    }

    private static Sprite MakeCircleSprite(bool ring)
    {
        const int dimension = 128;
        var texture = new Texture2D(dimension, dimension, TextureFormat.RGBA32, false);
        texture.name = ring ? "StatusEffect_Ring_Runtime" : "StatusEffect_Circle_Runtime";
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        var pixels = new Color32[dimension * dimension];
        for (int y = 0; y < dimension; y++)
        {
            for (int x = 0; x < dimension; x++)
            {
                float dx = (x + 0.5f - dimension / 2f) / (dimension / 2f);
                float dy = (y + 0.5f - dimension / 2f) / (dimension / 2f);
                float radius = Mathf.Sqrt(dx * dx + dy * dy);
                float outer = Mathf.Clamp01((0.98f - radius) / 0.026f);
                float inner = ring ? Mathf.Clamp01((radius - 0.76f) / 0.026f) : 1f;
                byte alpha = (byte)Mathf.RoundToInt(255f * outer * inner);
                pixels[y * dimension + x] = new Color32(255, 255, 255, alpha);
            }
        }

        texture.SetPixels32(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0, 0, dimension, dimension),
            new Vector2(0.5f, 0.5f), 100f);
        sprite.name = texture.name;
        return sprite;
    }
}
