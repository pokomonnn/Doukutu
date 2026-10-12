using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Playerが救出NPCへ近づいた時に、NPCの詳細を画面上へ表示します。
/// Sceneに手動配置しなくても、最初の表示時にCanvas直下へ自動生成できます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class RescuePersonDetailTooltipUI : MonoBehaviour
{
    private static RescuePersonDetailTooltipUI instance;

    [Header("サイズ")]
    [InspectorName("横幅")]
    [SerializeField, Min(180f)] private float panelWidth = 270f;

    [InspectorName("高さ")]
    [SerializeField, Min(90f)] private float panelHeight = 132f;

    [Header("NPCからの表示位置")]
    [InspectorName("画面上のずらし位置")]
    [SerializeField] private Vector2 screenOffset = new Vector2(34f, 92f);

    [InspectorName("画面端の余白")]
    [SerializeField, Min(0f)] private float screenMargin = 8f;

    [Header("文字")]
    [Tooltip("日本語を表示できるTextMeshPro Font Assetを設定してください。未設定の場合はTMPのDefault Font Assetを使用します。")]
    [InspectorName("日本語フォント")]
    [SerializeField] private TMP_FontAsset japaneseFontAsset;

    [InspectorName("タイトル文字サイズ")]
    [SerializeField, Min(8f)] private float titleFontSize = 22f;

    [InspectorName("詳細文字サイズ")]
    [SerializeField, Min(8f)] private float detailFontSize = 18f;

    [Header("表示色")]
    [InspectorName("背景色")]
    [SerializeField] private Color backgroundColor =
        new Color(0.04f, 0.04f, 0.04f, 0.92f);

    [InspectorName("タイトル色")]
    [SerializeField] private Color titleColor = Color.white;

    [InspectorName("詳細色")]
    [SerializeField] private Color detailColor = Color.white;

    private RectTransform panelRect;
    private CanvasGroup canvasGroup;
    private Image backgroundImage;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI detailText;

    private Canvas rootCanvas;
    private RescuePersonInfo2D currentInfo;
    private bool isVisible;
    private int displaySiblingIndex;

    public static bool IsVisible =>
        instance != null && instance.isVisible;

    /// <summary>現在表示している生存者情報。非表示中はnull。</summary>
    public static RescuePersonInfo2D VisibleInfo =>
        instance != null && instance.isVisible &&
        instance.currentInfo != null &&
        instance.gameObject.activeInHierarchy
            ? instance.currentInfo
            : null;

    /// <summary>
    /// 指定NPCについて現在表示されている詳細パネル全体を返します。
    /// 背景を含むRectTransformを返すため、文字だけでなくパネル全体の重なりを避けられます。
    /// </summary>
    public static RectTransform GetVisiblePanelFor(Transform npcRoot)
    {
        if (npcRoot == null || VisibleInfo == null)
            return null;

        Transform infoTransform = instance.currentInfo.transform;
        if (infoTransform != npcRoot &&
            !infoTransform.IsChildOf(npcRoot) &&
            !npcRoot.IsChildOf(infoTransform))
            return null;

        return instance.panelRect;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        panelRect = GetComponent<RectTransform>();
        rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;

        EnsureVisuals();
        HideImmediate();
    }

    private void LateUpdate()
    {
        if (!isVisible || currentInfo == null)
        {
            return;
        }

        if (!currentInfo.isActiveAndEnabled)
        {
            HideInternal();
            return;
        }

        RefreshContent();
        UpdatePosition();
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }

    public static void Show(
        RescuePersonInfo2D info,
        Canvas canvas,
        TMP_FontAsset fontAsset = null,
        int siblingIndex = 0)
    {
        if (info == null || canvas == null)
        {
            return;
        }

        RescuePersonDetailTooltipUI tooltip =
            GetOrCreate(canvas.rootCanvas);

        if (tooltip == null)
        {
            return;
        }

        tooltip.SetJapaneseFont(fontAsset);
        tooltip.SetDisplaySiblingIndex(siblingIndex);
        tooltip.ShowInternal(info);
    }

    public static void HideFor(RescuePersonInfo2D info)
    {
        if (instance == null)
        {
            return;
        }

        if (info == null || instance.currentInfo == info)
        {
            instance.HideInternal();
        }
    }

    public static void Hide()
    {
        instance?.HideInternal();
    }

    private static RescuePersonDetailTooltipUI GetOrCreate(
        Canvas canvas)
    {
        if (canvas == null)
        {
            return null;
        }

        if (instance != null)
        {
            instance.AttachToCanvas(canvas);
            return instance;
        }

        RescuePersonDetailTooltipUI existing =
            canvas.GetComponentInChildren<RescuePersonDetailTooltipUI>(true);

        if (existing != null)
        {
            instance = existing;
            instance.AttachToCanvas(canvas);
            instance.EnsureVisuals();
            return instance;
        }

        GameObject tooltipObject = new GameObject(
            "RescuePersonDetailTooltip",
            typeof(RectTransform),
            typeof(CanvasGroup),
            typeof(Image),
            typeof(RescuePersonDetailTooltipUI)
        );

        tooltipObject.transform.SetParent(canvas.transform, false);

        instance =
            tooltipObject.GetComponent<RescuePersonDetailTooltipUI>();

        instance.AttachToCanvas(canvas);
        instance.EnsureVisuals();
        instance.HideImmediate();

        return instance;
    }

    private void AttachToCanvas(Canvas canvas)
    {
        if (canvas == null)
        {
            return;
        }

        rootCanvas = canvas.rootCanvas;

        if (transform.parent != rootCanvas.transform)
        {
            transform.SetParent(rootCanvas.transform, false);
        }

        ApplyDisplayPriority();
    }

    /// <summary>
    /// NPC詳細UIのCanvas内での表示順を設定します。
    /// 0が最も奥側で、数値が大きいほど手前へ表示されます。
    /// </summary>
    public void SetDisplaySiblingIndex(int siblingIndex)
    {
        displaySiblingIndex = Mathf.Max(0, siblingIndex);
        ApplyDisplayPriority();
    }

    private void ApplyDisplayPriority()
    {
        if (transform.parent == null)
        {
            return;
        }

        int maximumIndex =
            Mathf.Max(0, transform.parent.childCount - 1);

        transform.SetSiblingIndex(
            Mathf.Clamp(
                displaySiblingIndex,
                0,
                maximumIndex
            )
        );
    }

    /// <summary>
    /// Player側などから日本語表示用のTMP Font Assetを設定します。
    /// nullの場合は現在の設定を維持し、未設定ならTMPのDefault Font Assetを使用します。
    /// </summary>
    public void SetJapaneseFont(TMP_FontAsset fontAsset)
    {
        if (fontAsset != null)
        {
            japaneseFontAsset = fontAsset;
        }

        ApplyConfiguredFont(titleText);
        ApplyConfiguredFont(detailText);
    }

    private void ShowInternal(RescuePersonInfo2D info)
    {
        currentInfo = info;

        EnsureVisuals();
        RefreshContent();

        gameObject.SetActive(true);

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        isVisible = true;
        ApplyDisplayPriority();

        UpdatePosition();
    }

    private void HideInternal()
    {
        currentInfo = null;
        isVisible = false;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void HideImmediate()
    {
        currentInfo = null;
        isVisible = false;

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }
    }

    private void RefreshContent()
    {
        if (currentInfo == null)
        {
            HideInternal();
            return;
        }

        if (titleText != null)
        {
            titleText.text = currentInfo.DisplayName;
        }

        if (detailText != null)
        {
            detailText.text =
                $"職業：{currentInfo.Occupation}\n" +
                $"状態：{currentInfo.ConditionLabel}\n" +
                $"重量：{currentInfo.WeightKg:0.##}kg";
        }
    }

    private void UpdatePosition()
    {
        if (rootCanvas == null ||
            panelRect == null ||
            currentInfo == null)
        {
            return;
        }

        Camera worldCamera = Camera.main;

        if (worldCamera == null)
        {
            return;
        }

        Vector3 screenPoint =
            worldCamera.WorldToScreenPoint(
                currentInfo.DetailWorldPosition
            );

        if (screenPoint.z < 0f)
        {
            return;
        }

        float scale = Mathf.Max(0.0001f, rootCanvas.scaleFactor);
        float widthPixels = panelRect.rect.width * scale;
        float heightPixels = panelRect.rect.height * scale;

        float x = screenPoint.x + screenOffset.x;
        float y = screenPoint.y + screenOffset.y;

        bool placeLeft =
            x + widthPixels > Screen.width - screenMargin;

        if (placeLeft)
        {
            panelRect.pivot = new Vector2(1f, 0f);
            x = screenPoint.x - Mathf.Abs(screenOffset.x);
        }
        else
        {
            panelRect.pivot = new Vector2(0f, 0f);
        }

        x = Mathf.Clamp(
            x,
            screenMargin,
            Screen.width - screenMargin
        );

        y = Mathf.Clamp(
            y,
            screenMargin,
            Mathf.Max(screenMargin, Screen.height - heightPixels - screenMargin)
        );

        RectTransform canvasRect =
            rootCanvas.transform as RectTransform;

        if (canvasRect == null)
        {
            return;
        }

        Camera uiCamera =
            rootCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : rootCanvas.worldCamera;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect,
                new Vector2(x, y),
                uiCamera,
                out Vector2 localPosition))
        {
            panelRect.anchoredPosition = localPosition;
        }
    }

    private void EnsureVisuals()
    {
        panelRect = GetComponent<RectTransform>();

        canvasGroup = GetComponent<CanvasGroup>();
        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }

        backgroundImage = GetComponent<Image>();
        if (backgroundImage == null)
        {
            backgroundImage = gameObject.AddComponent<Image>();
        }

        backgroundImage.color = backgroundColor;
        backgroundImage.raycastTarget = false;

        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0f, 0f);
        panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);

        EnsureTitleText();
        EnsureDetailText();
        LayoutVisuals();

        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
    }

    private void EnsureTitleText()
    {
        if (titleText == null)
        {
            Transform existing = transform.Find("TitleText");
            titleText = existing != null
                ? existing.GetComponent<TextMeshProUGUI>()
                : null;
        }

        if (titleText == null)
        {
            GameObject textObject = new GameObject(
                "TitleText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );

            textObject.transform.SetParent(transform, false);
            titleText = textObject.GetComponent<TextMeshProUGUI>();
        }

        titleText.fontSize = titleFontSize;
        titleText.fontStyle = FontStyles.Bold;
        titleText.color = titleColor;
        titleText.alignment = TextAlignmentOptions.TopLeft;
        titleText.raycastTarget = false;

        ApplyConfiguredFont(titleText);
    }

    private void EnsureDetailText()
    {
        if (detailText == null)
        {
            Transform existing = transform.Find("DetailText");
            detailText = existing != null
                ? existing.GetComponent<TextMeshProUGUI>()
                : null;
        }

        if (detailText == null)
        {
            GameObject textObject = new GameObject(
                "DetailText",
                typeof(RectTransform),
                typeof(TextMeshProUGUI)
            );

            textObject.transform.SetParent(transform, false);
            detailText = textObject.GetComponent<TextMeshProUGUI>();
        }

        detailText.fontSize = detailFontSize;
        detailText.color = detailColor;
        detailText.alignment = TextAlignmentOptions.TopLeft;
        detailText.raycastTarget = false;
        detailText.enableWordWrapping = false;

        ApplyConfiguredFont(detailText);
    }

    private void ApplyConfiguredFont(TextMeshProUGUI targetText)
    {
        if (targetText == null)
        {
            return;
        }

        if (japaneseFontAsset != null)
        {
            targetText.font = japaneseFontAsset;
            return;
        }

        if (targetText.font == null && TMP_Settings.defaultFontAsset != null)
        {
            targetText.font = TMP_Settings.defaultFontAsset;
        }
    }

    private void LayoutVisuals()
    {
        if (titleText != null)
        {
            RectTransform rect = titleText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -10f);
            rect.sizeDelta = new Vector2(-24f, 30f);
        }

        if (detailText != null)
        {
            RectTransform rect = detailText.rectTransform;
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = new Vector2(12f, 10f);
            rect.offsetMax = new Vector2(-12f, -46f);
        }
    }

    private void OnValidate()
    {
        panelWidth = Mathf.Max(180f, panelWidth);
        panelHeight = Mathf.Max(90f, panelHeight);
        screenMargin = Mathf.Max(0f, screenMargin);
        titleFontSize = Mathf.Max(8f, titleFontSize);
        detailFontSize = Mathf.Max(8f, detailFontSize);

        if (Application.isPlaying)
        {
            EnsureVisuals();
        }
        else
        {
            ApplyConfiguredFont(titleText);
            ApplyConfiguredFont(detailText);
        }
    }
}
