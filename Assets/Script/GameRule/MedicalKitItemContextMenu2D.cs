using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 応急キット専用のアイテムメニュー（使用する・情報のみ）。
/// 通常InventoryContextMenuUIはInventoryGrid内のItemを前提とするため、
/// 固定枠専用のメニューを別管理します。
/// 自動生成のほか、既存コンテキストメニューと同じ外見の複製UIを任意に指定可能です。
/// </summary>
[DisallowMultipleComponent]
public sealed class MedicalKitItemContextMenu2D : MonoBehaviour
{
    [Header("任意：既存メニューを複製したUIを割り当てられます")]
    [SerializeField] private RectTransform menuRoot;
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private Button useButton;
    [SerializeField] private Button takeButton;
    [SerializeField] private Button informationButton;
    [SerializeField] private Button closeButton;
    [Header("動作")]
    [SerializeField] private bool closeWhenClickOutside = true;

    private RectTransform panelRoot;
    private TMP_FontAsset japaneseFont;
    private Action useAction, infoAction;
    private int openedFrame;
    private bool listenersAttached;
    private const float MenuWidth = 188f;
    private const float MenuHeight = 132f;

    public void Configure(Transform panel, TMP_FontAsset font)
    {
        if (panel is RectTransform r) panelRoot = r;
        japaneseFont = font;
        if (menuRoot == null) BuildAutomaticMenu();
        if (itemNameText != null && japaneseFont != null) itemNameText.font = japaneseFont;
        // 旧メニュー（3項目）をPrefabで使っている場合も、中央の操作を削除する。
        // 通常Inventoryとは独立しているため、ほかの右クリックメニューは変更しない。
        ResolveOptionalButtons();
        ApplyTwoActionLayout();
        if (!listenersAttached)
        {
            if (useButton != null) useButton.onClick.AddListener(ClickUse);
            // 取り出す/取り消す のボタンは廃止。旧Prefabに残っていても登録しない。
            if (informationButton != null) informationButton.onClick.AddListener(ClickInfo);
            // 旧Prefabのキャンセルボタンはメニューに表示しない。
            listenersAttached = true;
        }
        Hide();
    }

    public void Show(ConsumableItemData data, int amount, Vector2 screenPosition,
        Action onUse, Action onTake, Action onInfo, bool canUse = true)
    {
        if (data == null || menuRoot == null) return;
        useAction = onUse;
        // onTake引数はMedicalKitInventoryUI2Dとの既存API互換のため維持。
        // メニューからの「取り出す」操作は行わない。
        infoAction = onInfo;
        if (itemNameText != null) itemNameText.text = data.DisplayName + " ×" + amount;
        if (useButton != null) useButton.interactable = amount > 0 && canUse;
        ApplyTwoActionLayout();
        menuRoot.gameObject.SetActive(true);
        menuRoot.SetAsLastSibling();
        Position(screenPosition);
        openedFrame = Time.frameCount;
    }

    public void Hide()
    {
        if (menuRoot != null) menuRoot.gameObject.SetActive(false);
        useAction = null;
        infoAction = null;
    }

    private void Update()
    {
        if (!closeWhenClickOutside || menuRoot == null || !menuRoot.gameObject.activeInHierarchy ||
            Time.frameCount == openedFrame) return;
        if (!(Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))) return;
        Canvas canvas = menuRoot.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.RectangleContainsScreenPoint(menuRoot, Input.mousePosition, cam)) Hide();
    }

    private void OnDisable() { Hide(); }
    private void OnDestroy()
    {
        if (useButton != null) useButton.onClick.RemoveListener(ClickUse);
        if (informationButton != null) informationButton.onClick.RemoveListener(ClickInfo);
        if (closeButton != null) closeButton.onClick.RemoveListener(Hide);
    }
    private void ClickUse() { Action action = useAction; Hide(); action?.Invoke(); }
    private void ClickInfo() { Action action = infoAction; Hide(); action?.Invoke(); }

    private void Position(Vector2 screenPoint)
    {
        RectTransform parentRect = menuRoot.parent as RectTransform;
        if (parentRect == null) return;
        Canvas canvas = menuRoot.GetComponentInParent<Canvas>();
        Camera cam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPoint, cam, out Vector2 local)) return;
        // 中心基準の親Rectにも対応。クリック位置からはみ出さないようにする。
        float minX = parentRect.rect.xMin;
        float maxX = parentRect.rect.xMax - menuRoot.rect.width;
        float minY = parentRect.rect.yMin + menuRoot.rect.height;
        float maxY = parentRect.rect.yMax;
        menuRoot.anchoredPosition = new Vector2(
            Mathf.Clamp(local.x, minX, Mathf.Max(minX, maxX)),
            Mathf.Clamp(local.y, Mathf.Min(minY, maxY), maxY));
    }

    private void BuildAutomaticMenu()
    {
        if (panelRoot == null) return;
        GameObject root = new GameObject("MedicalKitContextMenu", typeof(RectTransform), typeof(Image));
        root.transform.SetParent(panelRoot, false);
        menuRoot = root.GetComponent<RectTransform>();
        menuRoot.anchorMin = menuRoot.anchorMax = new Vector2(0.5f, 0.5f);
        menuRoot.pivot = new Vector2(0f, 1f);
        menuRoot.sizeDelta = new Vector2(MenuWidth, MenuHeight);
        Image image = root.GetComponent<Image>();
        image.color = new Color(0.115f, 0.135f, 0.16f, 0.99f);
        Outline outline = root.AddComponent<Outline>();
        outline.effectColor = new Color(0.55f, 0.62f, 0.67f, 0.8f);
        outline.effectDistance = new Vector2(1f,-1f);
        itemNameText = MakeText("ItemName", root.transform, new Vector2(7f,-8f),
            new Vector2(MenuWidth-14f,28f), 15f, TextAlignmentOptions.MidlineLeft);
        useButton = MakeButton("UseButton", "使用する", root.transform, 43f);
        informationButton = MakeButton("InformationButton", "情報", root.transform, 84f);
        root.SetActive(false);
    }

    /// <summary>
    /// 旧Prefabの「取り出す」「取り消す」等のボタンを、Inspectorで未指定でも検出します。
    /// 専用メニューの子だけを対象とし、ほかのInventoryには触れません。
    /// </summary>
    private void ResolveOptionalButtons()
    {
        if (menuRoot == null) return;
        Button[] buttons = menuRoot.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null) continue;
            string objectName = button.name.ToLowerInvariant();
            string caption = ReadButtonLabel(button);
            if (useButton == null &&
                (objectName.Contains("usebutton") || caption == "使用" || caption == "使用する"))
                useButton = button;
            if (informationButton == null &&
                (objectName.Contains("informationbutton") || objectName.Contains("infobutton") || caption == "情報"))
                informationButton = button;
            if (takeButton == null &&
                (objectName.Contains("takebutton") || objectName.Contains("cancelbutton") ||
                 caption == "取り出す" || caption == "取り消す" || caption == "取消す" || caption == "キャンセル"))
                takeButton = button;
            if (closeButton == null &&
                (objectName.Contains("closebutton") || caption == "閉じる"))
                closeButton = button;
        }
    }

    private static string ReadButtonLabel(Button button)
    {
        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) return tmp.text.Trim();
        Text legacy = button.GetComponentInChildren<Text>(true);
        return legacy != null ? legacy.text.Trim() : "";
    }

    /// <summary>
    /// 自動生成メニューとInspectorで指定した旧メニューの両方を2項目に統一します。
    /// レイアウトを自動管理するPrefabの場合はSetActiveだけを変更します。
    /// </summary>
    private void ApplyTwoActionLayout()
    {
        if (menuRoot == null) return;
        ResolveOptionalButtons();

        if (takeButton != null) takeButton.gameObject.SetActive(false);
        if (closeButton != null) closeButton.gameObject.SetActive(false);
        // 名前/文言だけで作られた旧ボタン（参照未設定）も残さない。
        Button[] buttons = menuRoot.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (button == null || button == useButton || button == informationButton) continue;
            string caption = ReadButtonLabel(button);
            string nameLower = button.name.ToLowerInvariant();
            if (caption == "取り出す" || caption == "取り消す" || caption == "取消す" ||
                caption == "キャンセル" || caption == "閉じる" ||
                nameLower.Contains("takebutton") || nameLower.Contains("cancelbutton") ||
                nameLower.Contains("closebutton"))
                button.gameObject.SetActive(false);
        }

        if (useButton != null)
        {
            useButton.gameObject.SetActive(true);
            TMP_Text label = useButton.GetComponentInChildren<TMP_Text>(true);
            if (label != null && label.text.Trim() == "使用") label.text = "使用する";
            Text legacy = useButton.GetComponentInChildren<Text>(true);
            if (legacy != null && legacy.text.Trim() == "使用") legacy.text = "使用する";
        }
        if (informationButton != null) informationButton.gameObject.SetActive(true);
        // 既存Prefabの独自デザインは維持し、必要な箇所のみ位置と高さを調整。
        RectTransform useRect = useButton != null ? useButton.GetComponent<RectTransform>() : null;
        RectTransform infoRect = informationButton != null ? informationButton.GetComponent<RectTransform>() : null;
        if (useRect == null || infoRect == null || useRect.parent != infoRect.parent) return;

        RectTransform actionParent = useRect.parent as RectTransform;
        if (actionParent == null) return;
        if (actionParent.GetComponent<LayoutGroup>() != null) return;
        if (actionParent.GetComponent<ContentSizeFitter>() != null) return;

        // 上下2段に並べる。Buttonのサイズ・色・フォントは既存のまま。
        Vector2 p = infoRect.anchoredPosition;
        p.y = useRect.anchoredPosition.y - useRect.rect.height - 6f;
        infoRect.anchoredPosition = p;

        // 既存メニューに残る3段目分の余白を縮める。
        if (actionParent == menuRoot)
        {
            float bottom = -infoRect.anchoredPosition.y + infoRect.rect.height + 7f;
            menuRoot.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,
                Mathf.Max(116f, bottom));
        }
    }

    private Button MakeButton(string objectName, string caption, Transform parent, float topOffset)
    {
        GameObject root = new GameObject(objectName, typeof(RectTransform), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f,1f);
        rect.pivot = new Vector2(0f,1f);
        rect.anchoredPosition = new Vector2(7f,-topOffset);
        rect.sizeDelta = new Vector2(MenuWidth-14f,35f);
        root.GetComponent<Image>().color = new Color(0.21f,0.25f,0.29f,1f);
        Button button = root.GetComponent<Button>();
        ColorBlock colors = button.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.8f,0.9f,1f);
        colors.disabledColor = new Color(0.4f,0.4f,0.4f);
        button.colors = colors;
        MakeText("Text", root.transform, new Vector2(0f,0f),new Vector2(MenuWidth-14f,35f),
            15f, TextAlignmentOptions.Center).text = caption;
        return button;
    }

    private TMP_Text MakeText(string objectName, Transform parent, Vector2 anchoredPos,
        Vector2 size, float fontSize, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f,1f);
        rect.pivot = new Vector2(0f,1f);
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = size;
        TextMeshProUGUI label = go.GetComponent<TextMeshProUGUI>();
        if (japaneseFont != null) label.font = japaneseFont;
        label.fontSize = fontSize;
        label.color = Color.white;
        label.alignment = alignment;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        return label;
    }
}
