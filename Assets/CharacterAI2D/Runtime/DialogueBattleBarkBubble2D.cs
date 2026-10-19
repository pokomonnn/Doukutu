using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// ScreenSpace UIとは別の頭上専用WorldSpace Canvas。
/// 生成されたバブルは話者がDestroyされても最後の位置で消える。
/// </summary>
[DisallowMultipleComponent]
public sealed class DialogueBattleBarkBubble2D : MonoBehaviour
{
    private Transform target;
    private Vector3 offset;
    private float remaining;
    private float duration;
    private CanvasGroup group;
    private TextMeshProUGUI body;
    private int totalCharacters;
    private float secondsPerCharacter;
    private float elapsedSeconds;

    /// <summary>文字送り＋全文表示時間を含めた実際の表示時間（STEP5の進行同期用）。</summary>
    public float TotalDuration => duration;
    public bool IsFullyRevealed => secondsPerCharacter <= 0f ||
        body == null || body.maxVisibleCharacters >= totalCharacters;

    public static DialogueBattleBarkBubble2D Show(Transform speaker, string message, TMP_FontAsset font,
        Vector3 worldOffset, float seconds, int order, float widthUnits,
        Color textColor, Color backgroundColor,
        float characterInterval = 0.045f,
        float minimumFullyVisibleSeconds = 0.7f)
    {
        if (speaker == null || string.IsNullOrWhiteSpace(message)) return null;
        GameObject root = new GameObject("BattleBark_" + speaker.name,
            typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup),
            typeof(DialogueBattleBarkBubble2D));
        RectTransform rootRect = (RectTransform)root.transform;
        rootRect.sizeDelta = new Vector2(500f, 105f);
        rootRect.localScale = Vector3.one * (Mathf.Max(.5f, widthUnits) / 500f);

        Canvas canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingOrder = order;
        CanvasGroup alpha = root.GetComponent<CanvasGroup>();
        alpha.blocksRaycasts = false;
        alpha.interactable = false;

        GameObject background = new GameObject("背景", typeof(RectTransform), typeof(Image));
        RectTransform bgRect = (RectTransform)background.transform;
        bgRect.SetParent(root.transform, false);
        bgRect.anchorMin = Vector2.zero;
        bgRect.anchorMax = Vector2.one;
        bgRect.offsetMin = Vector2.zero;
        bgRect.offsetMax = Vector2.zero;
        Image bg = background.GetComponent<Image>();
        bg.color = backgroundColor;
        bg.raycastTarget = false;

        GameObject textObject = new GameObject("セリフ", typeof(RectTransform), typeof(TextMeshProUGUI));
        RectTransform textRect = (RectTransform)textObject.transform;
        textRect.SetParent(root.transform, false);
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(13f, 6f);
        textRect.offsetMax = new Vector2(-13f, -6f);
        TextMeshProUGUI tmp = textObject.GetComponent<TextMeshProUGUI>();
        if (font != null) tmp.font = font;
        else if (TMP_Settings.defaultFontAsset != null) tmp.font = TMP_Settings.defaultFontAsset;
        tmp.text = message;
        tmp.fontSize = 30;
        tmp.color = textColor;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;

        DialogueBattleBarkBubble2D bubble = root.GetComponent<DialogueBattleBarkBubble2D>();
        bubble.target = speaker;
        bubble.offset = worldOffset;
        bubble.group = alpha;
        bubble.body = tmp;
        bubble.secondsPerCharacter = Mathf.Max(0f, characterInterval);
        // まず全文を組版し、実際に描画する文字数だけ0にする。
        // タグ部分を数えないため、色指定などがあっても1文字ずつ表示できる。
        tmp.maxVisibleCharacters = 0;
        tmp.ForceMeshUpdate();
        bubble.totalCharacters = tmp.textInfo.characterCount;
        float revealSeconds = bubble.totalCharacters * bubble.secondsPerCharacter;
        bubble.duration = bubble.remaining = bubble.secondsPerCharacter > 0f
            ? Mathf.Max(Mathf.Max(.1f, seconds), revealSeconds + Mathf.Max(0.15f, minimumFullyVisibleSeconds))
            : Mathf.Max(.1f, seconds);
        if (bubble.secondsPerCharacter <= 0f || bubble.totalCharacters == 0)
            tmp.maxVisibleCharacters = int.MaxValue;
        bubble.Follow();
        return bubble;
    }

    private void LateUpdate()
    {
        Follow();
        float delta = Time.deltaTime;
        elapsedSeconds += delta;
        remaining = Mathf.Max(0f, duration - elapsedSeconds);
        if (body != null && secondsPerCharacter > 0f && totalCharacters > 0)
        {
            int visible = Mathf.Min(totalCharacters,
                Mathf.FloorToInt(elapsedSeconds / secondsPerCharacter));
            if (body.maxVisibleCharacters != visible) body.maxVisibleCharacters = visible;
        }
        if (remaining <= 0f) { Destroy(gameObject); return; }
        if (group != null) group.alpha = Mathf.Clamp01(remaining / Mathf.Min(.35f, duration));
    }

    private void Follow()
    {
        if (target != null)
        {
            Vector3 pos = target.position + offset;
            pos.z = -2f;
            transform.position = pos;
        }
    }
}
