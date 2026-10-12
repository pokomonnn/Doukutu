using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 通常Inventoryの右クリックメニューを、応急キットのポップアップ風にします。
/// 「見た目」だけを変更します。装備/使用/情報/修理/捨てるの処理は
/// 既存のInventoryContextMenuUIに任せるため、アイテム消費などは二重実行しません。
///
/// 使い方：InventoryContextMenuUIと同じGameObjectへ追加。
/// Japanese Fontを指定するだけでも、自動で子ボタンを検出します。
/// 自動検出できない場合は下記Button参照を明示的に設定してください。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InventoryContextMenuUI))]
[RequireComponent(typeof(RectTransform))]
public sealed class InventoryContextMenuCompactSkin2D : MonoBehaviour
{
    [Header("任意：文字の表示用フォント")]
    [SerializeField] private TMP_FontAsset japaneseFont;

    [Header("既存の右クリックメニューUI（未設定なら自動検索）")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button useButton;
    [SerializeField] private Button informationButton;
    [SerializeField] private Button repairButton;
    [SerializeField] private Button trashButton;

    [Header("応急キット風の見た目")]
    [SerializeField, Min(140f)] private float menuWidth = 188f;
    [SerializeField, Min(28f)] private float actionHeight = 35f;
    [SerializeField, Min(0f)] private float actionSpacing = 6f;
    [SerializeField, Min(0f)] private float edgePadding = 7f;
    [SerializeField, Min(20f)] private float titleHeight = 28f;
    [SerializeField] private Color panelColor = new Color(0.115f, 0.135f, 0.16f, 0.99f);
    [SerializeField] private Color actionColor = new Color(0.21f, 0.25f, 0.29f, 1f);
    [SerializeField] private Color frameColor = new Color(0.55f, 0.62f, 0.67f, 0.80f);
    [SerializeField] private bool keepInsideParent = true;
    [SerializeField] private bool showDebugLogs = false;

    private readonly List<Button> actionButtons = new List<Button>();
    private RectTransform menuRect;
    private RectTransform titleRect;
    private Image panelImage;
    private Outline panelOutline;
    private Image divider;
    private int lastVisibilityMask = -1;
    private bool hasAppliedInitialStyle;
    private bool warnedMissingButtons;

    private void Awake()
    {
        menuRect = GetComponent<RectTransform>();
        LocateReferences();
        ApplyBaseStyle();
    }

    private void OnEnable()
    {
        if (menuRect == null) menuRect = GetComponent<RectTransform>();
        LocateReferences();
        ApplyBaseStyle();
        lastVisibilityMask = -1;
        UpdateVisibleLayout();
    }

    private void LateUpdate()
    {
        if (menuRect == null || !gameObject.activeInHierarchy) return;

        // InventoryContextMenuUI.Show()がボタンの表示/非表示を決めるのは
        // OnEnable後の場合もあるため、LateUpdateで最終状態へ追従します。
        UpdateVisibleLayout();
        if (keepInsideParent) ClampInsideParent();
    }

    [ContextMenu("診断：右クリックメニューの設定を確認")]
    public void Diagnose()
    {
        LocateReferences();
        Debug.Log(
            "[InventoryPopupSkin] " +
            "Menu=" + GetPath(transform) +
            " / Title=" + (itemNameText != null ? itemNameText.name : "NULL") +
            " / Equip=" + NameOrNull(equipButton) +
            " / Use=" + NameOrNull(useButton) +
            " / Info=" + NameOrNull(informationButton) +
            " / Repair=" + NameOrNull(repairButton) +
            " / Trash=" + NameOrNull(trashButton) +
            " / Font=" + (japaneseFont != null ? japaneseFont.name : "未指定") +
            " / MenuActive=" + gameObject.activeInHierarchy,
            this);
    }

    private void LocateReferences()
    {
        if (menuRect == null) menuRect = GetComponent<RectTransform>();

        Button[] buttons = GetComponentsInChildren<Button>(true);
        if (equipButton == null) equipButton = FindButton(buttons, "equip", "装備");
        if (useButton == null) useButton = FindButton(buttons, "use", "使用");
        if (informationButton == null) informationButton = FindButton(buttons, "information", "情報", "info");
        if (repairButton == null) repairButton = FindButton(buttons, "repair", "修理");
        if (trashButton == null) trashButton = FindButton(buttons, "trash", "捨て", "discard");

        actionButtons.Clear();
        AddAction(equipButton);
        AddAction(useButton);
        AddAction(informationButton);
        AddAction(repairButton);
        AddAction(trashButton);

        if (actionButtons.Count == 0)
        {
            // オブジェクト名が独自の場合でも、メニュー直下または
            // 一段下にあるボタンを拾う。InformationPanel側は除外。
            foreach (Button b in buttons)
            {
                if (b == null || IsInDetailsPanel(b.transform)) continue;
                bool shallow = b.transform.parent == transform ||
                    (b.transform.parent != null && b.transform.parent.parent == transform);
                if (shallow) AddAction(b);
            }
        }

        if (itemNameText == null)
        {
            foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == null || IsInDetailsPanel(t.transform)) continue;
                string n = t.name.ToLowerInvariant();
                if (n.Contains("itemname") || n.Contains("itemtitle") || n == "name" || n == "title")
                {
                    itemNameText = t;
                    break;
                }
            }
        }
        if (itemNameText == null)
        {
            foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
            {
                if (t == null || IsInDetailsPanel(t.transform)) continue;
                if (t.GetComponentInParent<Button>() == null)
                {
                    itemNameText = t;
                    break;
                }
            }
        }

        if (actionButtons.Count == 0 && !warnedMissingButtons)
        {
            warnedMissingButtons = true;
            Debug.LogWarning("[InventoryPopupSkin] ボタンを自動取得できません。InspectorでEquip/Use/Information/Repair/Trash Buttonを割り当ててください。", this);
        }
    }

    private Button FindButton(Button[] all, params string[] tokens)
    {
        foreach (Button button in all)
        {
            if (button == null || IsInDetailsPanel(button.transform)) continue;
            string lower = button.name.ToLowerInvariant();
            foreach (string token in tokens)
            {
                if (lower.Contains(token.ToLowerInvariant())) return button;
            }
        }
        return null;
    }

    private bool IsInDetailsPanel(Transform target)
    {
        for (Transform p = target.parent; p != null && p != transform; p = p.parent)
        {
            string n = p.name.ToLowerInvariant();
            if (n.Contains("informationpanel") || n.Contains("infopanel") ||
                n.Contains("detailspanel")) return true;
        }
        return false;
    }

    private void AddAction(Button button)
    {
        if (button != null && button.transform.IsChildOf(transform) &&
            !IsInDetailsPanel(button.transform) && !actionButtons.Contains(button))
            actionButtons.Add(button);
    }

    private void ApplyBaseStyle()
    {
        if (menuRect == null) return;

        // レイアウトを全自動で並べるため、従来の親レイアウトと競合させない。
        LayoutGroup layout = GetComponent<LayoutGroup>();
        if (layout != null) layout.enabled = false;
        ContentSizeFitter sizeFitter = GetComponent<ContentSizeFitter>();
        if (sizeFitter != null) sizeFitter.enabled = false;

        if (panelImage == null) panelImage = GetComponent<Image>();
        if (panelImage == null) panelImage = gameObject.AddComponent<Image>();
        panelImage.color = panelColor;
        panelImage.raycastTarget = true; // メニュー内の空白をクリックしても外側扱いにしない。

        if (panelOutline == null) panelOutline = GetComponent<Outline>();
        if (panelOutline == null) panelOutline = gameObject.AddComponent<Outline>();
        panelOutline.effectColor = frameColor;
        panelOutline.effectDistance = new Vector2(1f, -1f);
        panelOutline.useGraphicAlpha = false;

        if (itemNameText != null)
        {
            titleRect = itemNameText.rectTransform;
            // 見た目を崩す旧LayoutGroupの影響を避け、メニュー直下へ配置。
            if (itemNameText.transform.parent != transform)
                itemNameText.transform.SetParent(transform, false);
            titleRect.anchorMin = titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 1f);
            titleRect.anchoredPosition = new Vector2(edgePadding, -edgePadding);
            titleRect.sizeDelta = new Vector2(menuWidth - edgePadding * 2f, titleHeight);
            itemNameText.fontSize = 15f;
            itemNameText.color = Color.white;
            itemNameText.alignment = TextAlignmentOptions.MidlineLeft;
            itemNameText.enableWordWrapping = false;
            itemNameText.overflowMode = TextOverflowModes.Ellipsis;
            itemNameText.raycastTarget = false;
            if (japaneseFont != null) itemNameText.font = japaneseFont;
        }

        if (divider == null)
        {
            Transform existing = transform.Find("_CompactSkinDivider");
            divider = existing != null ? existing.GetComponent<Image>() : null;
            if (divider == null)
            {
                GameObject g = new GameObject("_CompactSkinDivider", typeof(RectTransform), typeof(Image));
                g.transform.SetParent(transform, false);
                divider = g.GetComponent<Image>();
            }
        }
        divider.color = frameColor;
        divider.raycastTarget = false;
        RectTransform dividerRect = divider.rectTransform;
        dividerRect.anchorMin = dividerRect.anchorMax = new Vector2(0f, 1f);
        dividerRect.pivot = new Vector2(0f, 1f);
        dividerRect.anchoredPosition = new Vector2(edgePadding, -(edgePadding + titleHeight + 2f));
        dividerRect.sizeDelta = new Vector2(menuWidth - edgePadding * 2f, 1f);

        foreach (Button button in actionButtons) StyleAction(button);
        hasAppliedInitialStyle = true;
    }

    private void StyleAction(Button button)
    {
        if (button == null) return;
        if (button.transform.parent != transform)
            button.transform.SetParent(transform, false);

        Image bg = button.GetComponent<Image>();
        if (bg == null) bg = button.gameObject.AddComponent<Image>();
        bg.color = actionColor;
        bg.raycastTarget = true;
        button.targetGraphic = bg;
        button.transition = Selectable.Transition.ColorTint;
        ColorBlock cb = button.colors;
        cb.normalColor = Color.white;
        cb.highlightedColor = new Color(0.83f, 0.9f, 1f, 1f);
        cb.pressedColor = new Color(0.64f, 0.78f, 0.92f, 1f);
        cb.selectedColor = cb.highlightedColor;
        cb.disabledColor = new Color(0.45f, 0.45f, 0.45f, 0.75f);
        cb.fadeDuration = 0.07f;
        button.colors = cb;
        button.navigation = new Navigation { mode = Navigation.Mode.None };

        LayoutGroup g = button.GetComponent<LayoutGroup>();
        if (g != null) g.enabled = false;
        foreach (TMP_Text label in button.GetComponentsInChildren<TMP_Text>(true))
        {
            if (label == null) continue;
            if (japaneseFont != null) label.font = japaneseFont;
            label.fontSize = 15f;
            label.color = Color.white;
            label.alignment = TextAlignmentOptions.Center;
            label.enableWordWrapping = false;
            label.overflowMode = TextOverflowModes.Ellipsis;
            label.raycastTarget = false;
            RectTransform tr = label.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.pivot = new Vector2(0.5f, 0.5f);
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }

        // 古いUIでLegacy UI.Textが使われていてもボタンの文字を整列させる。
        foreach (Text label in button.GetComponentsInChildren<Text>(true))
        {
            if (label == null) continue;
            label.fontSize = 15;
            label.color = Color.white;
            label.alignment = TextAnchor.MiddleCenter;
            label.raycastTarget = false;
            RectTransform tr = label.rectTransform;
            tr.anchorMin = Vector2.zero;
            tr.anchorMax = Vector2.one;
            tr.offsetMin = Vector2.zero;
            tr.offsetMax = Vector2.zero;
        }
    }

    private void UpdateVisibleLayout()
    {
        if (actionButtons.Count == 0) return;
        int mask = 0;
        for (int i = 0; i < actionButtons.Count; i++)
        {
            Button button = actionButtons[i];
            if (button != null && button.gameObject.activeSelf) mask |= (1 << i);
        }
        if (hasAppliedInitialStyle && mask == lastVisibilityMask) return;
        lastVisibilityMask = mask;

        float top = edgePadding + titleHeight + 8f;
        int visible = 0;
        for (int i = 0; i < actionButtons.Count; i++)
        {
            Button b = actionButtons[i];
            if (b == null || !b.gameObject.activeSelf) continue;
            RectTransform r = b.transform as RectTransform;
            if (r == null) continue;
            r.anchorMin = r.anchorMax = new Vector2(0f, 1f);
            r.pivot = new Vector2(0f, 1f);
            r.anchoredPosition = new Vector2(edgePadding, -(top + visible * (actionHeight + actionSpacing)));
            r.sizeDelta = new Vector2(menuWidth - edgePadding * 2f, actionHeight);
            visible++;
        }
        float height = top + visible * actionHeight + Mathf.Max(0, visible - 1) * actionSpacing + edgePadding;
        menuRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, menuWidth);
        menuRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
        if (showDebugLogs)
            Debug.Log("[InventoryPopupSkin] メニュー更新 / 表示ボタン=" + visible + " / Size=" + menuWidth + "x" + height, this);
    }

    private void ClampInsideParent()
    {
        RectTransform parent = menuRect.parent as RectTransform;
        if (parent == null) return;
        Rect rect = parent.rect;
        // 既存のInventoryContextMenuUIはanchor=(0.5,0.5)、pivot=(0,1)。
        // 親のPivotが中央以外でも画面外にはみ出さないよう考慮します。
        Vector2 anchor = menuRect.anchorMin;
        Vector2 anchorPoint = new Vector2(
            rect.xMin + rect.width * anchor.x,
            rect.yMin + rect.height * anchor.y);
        float margin = 3f;
        float minX = rect.xMin + margin - anchorPoint.x + menuRect.rect.width * menuRect.pivot.x;
        float maxX = rect.xMax - margin - anchorPoint.x - menuRect.rect.width * (1f - menuRect.pivot.x);
        float minY = rect.yMin + margin - anchorPoint.y + menuRect.rect.height * menuRect.pivot.y;
        float maxY = rect.yMax - margin - anchorPoint.y - menuRect.rect.height * (1f - menuRect.pivot.y);
        if (maxX < minX || maxY < minY) return; // 親が小さい場合は既存位置を優先。
        Vector2 p = menuRect.anchoredPosition;
        Vector2 limited = new Vector2(Mathf.Clamp(p.x, minX, maxX), Mathf.Clamp(p.y, minY, maxY));
        if (limited != p) menuRect.anchoredPosition = limited;
    }

    private static string NameOrNull(Button b) { return b != null ? b.name : "NULL"; }
    private static string GetPath(Transform t)
    {
        string result = t.name;
        while (t.parent != null) { t = t.parent; result = t.name + "/" + result; }
        return result;
    }
}
