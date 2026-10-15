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

    [Header("NPCのケガ・必要な医療用品")]
    [Tooltip("NPC詳細パネルに出血・毒・骨折と必要なItemの画像を表示します")]
    [InspectorName("ケガの情報を表示")]
    [SerializeField] private bool showInjuryDetails = true;

    [Tooltip("未設定ならシーン内のMedicalKitInventory2Dを自動取得します。")]
    [InspectorName("応急キット（任意）")]
    [SerializeField] private MedicalKitInventory2D medicalKitInventory;

    [InspectorName("ケガ1行の高さ")]
    [SerializeField, Min(38f)] private float injuryRowHeight = 48f;

    [InspectorName("医療用品アイコンの大きさ")]
    [SerializeField, Range(28f, 56f)] private float injuryIconSize = 38f;

    [InspectorName("ケガの文字サイズ")]
    [SerializeField, Range(11f, 20f)] private float injuryFontSize = 15f;

    private const float SymptomHeaderHeight = 29f;
    private const float SymptomRowGap = 5f;
    private const float SymptomBottomMargin = 12f;
    private const float MinBasePanelHeight = 132f;

    private static readonly StatusConditionType[] SymptomOrder =
    {
        StatusConditionType.Bleeding,
        StatusConditionType.Poison,
        StatusConditionType.Fracture
    };

    private sealed class SymptomRowUI
    {
        public RectTransform rect;
        public Image background;
        public Image itemIcon;
        public TextMeshProUGUI statusText;
        public TextMeshProUGUI itemText;
    }

    private RescuePersonSymptoms2D currentSymptoms;
    private TextMeshProUGUI symptomHeaderText;
    private TextMeshProUGUI noSymptomsText;
    private readonly SymptomRowUI[] symptomRows = new SymptomRowUI[3];
    private float nextKitSearchTime;

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
        ApplyConfiguredFont(symptomHeaderText);
        ApplyConfiguredFont(noSymptomsText);
        for (int i = 0; i < symptomRows.Length; i++)
        {
            SymptomRowUI row = symptomRows[i];
            if (row == null) continue;
            ApplyConfiguredFont(row.statusText);
            ApplyConfiguredFont(row.itemText);
        }
    }

    private void ShowInternal(RescuePersonInfo2D info)
    {
        bool changed = currentInfo != info;
        currentInfo = info;
        if (changed || !isVisible)
        {
            currentSymptoms = FindSymptoms(info);
            EnsureVisuals();
        }

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
        currentSymptoms = null;
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
        currentSymptoms = null;
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

        RefreshSymptomRows();
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

        float leftBound = placeLeft ? screenMargin + widthPixels : screenMargin;
        float rightBound = placeLeft
            ? Screen.width - screenMargin
            : Screen.width - screenMargin - widthPixels;
        x = leftBound <= rightBound
            ? Mathf.Clamp(x, leftBound, rightBound)
            : Screen.width * 0.5f;

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
        EnsureSymptomVisuals();
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
            // 既存の3行を常にタイトル直下へ固定。症状欄でパネルが伸びても崩れません。
            RectTransform rect = detailText.rectTransform;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -46f);
            rect.sizeDelta = new Vector2(-24f, 76f);
        }

        LayoutSymptomVisuals();
    }


    // ---------------- NPCケガ表示 ----------------

    private static RescuePersonSymptoms2D FindSymptoms(RescuePersonInfo2D info)
    {
        if (info == null) return null;

        RescuePersonSymptoms2D symptoms =
            info.GetComponent<RescuePersonSymptoms2D>();
        if (symptoms == null)
            symptoms = info.GetComponentInParent<RescuePersonSymptoms2D>();
        if (symptoms == null)
            symptoms = info.GetComponentInChildren<RescuePersonSymptoms2D>(true);

        // Infoと症状が、同じNPCルート直下の別の子Objectにある構成も対応。
        if (symptoms == null && info.transform.parent != null)
            symptoms = info.transform.parent.GetComponentInChildren<RescuePersonSymptoms2D>(true);

        return symptoms;
    }

    private void FindMedicalKit()
    {
        if (medicalKitInventory != null) return;
        if (Time.unscaledTime < nextKitSearchTime) return;

        nextKitSearchTime = Time.unscaledTime + 1f;
        medicalKitInventory = FindFirstObjectByType<MedicalKitInventory2D>(
            FindObjectsInactive.Include);
    }

    private static string SymptomName(StatusConditionType symptom)
    {
        switch (symptom)
        {
            case StatusConditionType.Bleeding: return "出血";
            case StatusConditionType.Poison: return "毒";
            case StatusConditionType.Fracture: return "骨折";
            default: return "不明";
        }
    }

    private static string DefaultItemName(StatusConditionType symptom)
    {
        switch (symptom)
        {
            case StatusConditionType.Bleeding: return "包帯";
            case StatusConditionType.Poison: return "解毒剤";
            case StatusConditionType.Fracture: return "副木";
            default: return "医療用品";
        }
    }

    private ConsumableItemData FindMedicalItem(StatusConditionType symptom)
    {
        if (medicalKitInventory == null) return null;

        for (int i = 0; i < medicalKitInventory.SlotCount; i++)
        {
            ConsumableItemData data = medicalKitInventory.GetItemAtSlot(i);
            if (data != null &&
                medicalKitInventory.GetNpcTreatmentCondition(data) == symptom)
            {
                return data;
            }
        }
        return null;
    }

    private static TextMeshProUGUI CreateLabel(
        Transform parent, string objectName, float fontSize, Color color)
    {
        Transform existing = parent.Find(objectName);
        TextMeshProUGUI label = existing != null
            ? existing.GetComponent<TextMeshProUGUI>() : null;

        if (label == null)
        {
            GameObject labelObject = new GameObject(
                objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);
            label = labelObject.GetComponent<TextMeshProUGUI>();
        }

        label.fontSize = fontSize;
        label.color = color;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        return label;
    }

    private void EnsureSymptomVisuals()
    {
        symptomHeaderText = CreateLabel(
            transform, "InjurySectionTitle", injuryFontSize, new Color(0.82f, 0.88f, 1f));
        symptomHeaderText.fontStyle = FontStyles.Bold;
        ApplyConfiguredFont(symptomHeaderText);

        noSymptomsText = CreateLabel(
            transform, "NoInjuryText", injuryFontSize, detailColor);
        ApplyConfiguredFont(noSymptomsText);

        for (int i = 0; i < symptomRows.Length; i++)
        {
            if (symptomRows[i] == null) symptomRows[i] = new SymptomRowUI();
            SymptomRowUI row = symptomRows[i];
            string objectName = "InjuryRow_" + i;
            Transform child = transform.Find(objectName);
            if (child == null)
            {
                GameObject rowObject = new GameObject(
                    objectName, typeof(RectTransform), typeof(Image));
                rowObject.transform.SetParent(transform, false);
                child = rowObject.transform;
            }

            row.rect = child as RectTransform;
            row.background = child.GetComponent<Image>();
            if (row.background == null) row.background = child.gameObject.AddComponent<Image>();
            row.background.raycastTarget = false;

            Transform iconChild = child.Find("RequiredItemIcon");
            if (iconChild == null)
            {
                GameObject iconObject = new GameObject(
                    "RequiredItemIcon", typeof(RectTransform), typeof(Image));
                iconObject.transform.SetParent(child, false);
                iconChild = iconObject.transform;
            }
            row.itemIcon = iconChild.GetComponent<Image>();
            if (row.itemIcon == null) row.itemIcon = iconChild.gameObject.AddComponent<Image>();
            row.itemIcon.preserveAspect = true;
            row.itemIcon.raycastTarget = false;

            row.statusText = CreateLabel(
                child, "InjuryStatus", injuryFontSize, detailColor);
            row.statusText.fontStyle = FontStyles.Bold;
            row.itemText = CreateLabel(
                child, "RequiredItemName", Mathf.Max(11f, injuryFontSize - 2f), detailColor);
            ApplyConfiguredFont(row.statusText);
            ApplyConfiguredFont(row.itemText);
        }
    }

    private static void PositionAtTop(
        RectTransform rect, float left, float top, float width, float height)
    {
        rect.anchorMin = new Vector2(0f, 1f);
        rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.anchoredPosition = new Vector2(left, -top);
        rect.sizeDelta = new Vector2(width, height);
    }

    private void LayoutSymptomVisuals()
    {
        if (symptomHeaderText == null) return;

        float baseHeight = Mathf.Max(MinBasePanelHeight, panelHeight);
        float sectionTop = baseHeight + 1f;
        float contentWidth = Mathf.Max(145f, panelWidth - 24f);
        PositionAtTop(symptomHeaderText.rectTransform, 12f, sectionTop,
            contentWidth, 24f);
        PositionAtTop(noSymptomsText.rectTransform, 12f,
            sectionTop + SymptomHeaderHeight, contentWidth, 24f);

        for (int i = 0; i < symptomRows.Length; i++)
        {
            SymptomRowUI row = symptomRows[i];
            if (row == null || row.rect == null) continue;
            PositionAtTop(row.rect, 12f, sectionTop + SymptomHeaderHeight +
                i * (injuryRowHeight + SymptomRowGap), contentWidth, injuryRowHeight);
            PositionAtTop(row.itemIcon.rectTransform, 6f,
                (injuryRowHeight - injuryIconSize) * 0.5f,
                injuryIconSize, injuryIconSize);
            float textLeft = injuryIconSize + 13f;
            float textWidth = Mathf.Max(40f, contentWidth - textLeft - 5f);
            PositionAtTop(row.statusText.rectTransform, textLeft, 4f,
                textWidth, (injuryRowHeight - 6f) * 0.52f);
            PositionAtTop(row.itemText.rectTransform, textLeft,
                injuryRowHeight * 0.51f, textWidth, injuryRowHeight * 0.44f);
        }
    }

    private void RefreshSymptomRows()
    {
        if (panelRect == null) return;

        float baseHeight = Mathf.Max(MinBasePanelHeight, panelHeight);
        bool visible = showInjuryDetails;

        if (symptomHeaderText != null)
            symptomHeaderText.gameObject.SetActive(visible);

        if (!visible)
        {
            if (noSymptomsText != null) noSymptomsText.gameObject.SetActive(false);
            foreach (SymptomRowUI row in symptomRows)
                if (row != null && row.rect != null) row.rect.gameObject.SetActive(false);
            panelRect.sizeDelta = new Vector2(panelWidth, panelHeight);
            return;
        }

        FindMedicalKit();
        if (currentSymptoms == null)
            currentSymptoms = FindSymptoms(currentInfo);

        StatusConditionType active = currentSymptoms != null
            ? currentSymptoms.ActiveSymptoms : StatusConditionType.None;
        int rowCount = 0;
        foreach (StatusConditionType symptom in SymptomOrder)
        {
            if ((active & symptom) == 0) continue;
            if (rowCount >= symptomRows.Length) break;
            SymptomRowUI row = symptomRows[rowCount];
            row.rect.gameObject.SetActive(true);
            bool treated = currentSymptoms != null && currentSymptoms.IsStabilized(symptom);
            ConsumableItemData data = FindMedicalItem(symptom);

            row.statusText.text = SymptomName(symptom) +
                (treated ? "（処置済み）" : "（未処置）");
            row.statusText.color = treated
                ? new Color(0.58f, 0.89f, 0.64f)
                : new Color(1f, 0.73f, 0.66f);
            row.background.color = treated
                ? new Color(0.12f, 0.26f, 0.17f, 0.82f)
                : new Color(0.25f, 0.18f, 0.18f, 0.82f);

            string itemName = data != null ? data.DisplayName : DefaultItemName(symptom);
            row.itemText.text = data != null
                ? "必要：" + itemName + "（残り" + medicalKitInventory.GetAmount(data) + "）"
                : "必要：" + itemName + "（未登録）";
            row.itemText.color = detailColor;
            Sprite itemSprite = data != null ? data.Icon : null;
            row.itemIcon.sprite = itemSprite;
            row.itemIcon.enabled = itemSprite != null;
            row.itemIcon.color = treated
                ? new Color(0.72f, 0.82f, 0.72f, 0.75f)
                : Color.white;

            // 非表示の行が間に挟まっても表示が詰まるよう位置を再設定。
            float contentWidth = Mathf.Max(145f, panelWidth - 24f);
            PositionAtTop(row.rect, 12f,
                baseHeight + 1f + SymptomHeaderHeight +
                rowCount * (injuryRowHeight + SymptomRowGap),
                contentWidth, injuryRowHeight);
            rowCount++;
        }

        for (int i = rowCount; i < symptomRows.Length; i++)
            if (symptomRows[i] != null && symptomRows[i].rect != null)
                symptomRows[i].rect.gameObject.SetActive(false);

        bool noSymptoms = rowCount == 0;
        if (noSymptomsText != null)
        {
            noSymptomsText.gameObject.SetActive(noSymptoms);
            noSymptomsText.text = currentSymptoms == null
                ? "ケガの情報がありません"
                : "治療が必要な症状はありません";
        }

        float rowsHeight = noSymptoms
            ? 26f
            : rowCount * injuryRowHeight + (rowCount - 1) * SymptomRowGap;
        panelRect.sizeDelta = new Vector2(panelWidth,
            baseHeight + 1f + SymptomHeaderHeight + rowsHeight + SymptomBottomMargin);
    }

    private void OnValidate()
    {
        panelWidth = Mathf.Max(180f, panelWidth);
        panelHeight = Mathf.Max(90f, panelHeight);
        screenMargin = Mathf.Max(0f, screenMargin);
        titleFontSize = Mathf.Max(8f, titleFontSize);
        detailFontSize = Mathf.Max(8f, detailFontSize);
        injuryRowHeight = Mathf.Max(38f, injuryRowHeight);
        injuryIconSize = Mathf.Clamp(injuryIconSize, 28f, 56f);
        injuryFontSize = Mathf.Clamp(injuryFontSize, 11f, 20f);

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
