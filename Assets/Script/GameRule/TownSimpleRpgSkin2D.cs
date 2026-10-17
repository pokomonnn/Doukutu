using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 既存の町UIのHierarchy・座標・ButtonのOnClick・売買/修理/収納データを変更せず、
/// 見た目だけを共通のシンプルRPG配色にするコンポーネント。
/// 4種類のメニュー（希少品店、武器商人、武器修理、自宅収納）で再利用できる。
///
/// InventoryGridUI は表示中にセルを再生成するため、生成後の CellRoot も追従する。
/// InventoryItemUI のアイテム選択色、ドラッグ処理、アイコンには触れない。
/// EquipmentSlotUI は既存コンポーネントをそのまま残し、背後の装飾だけ追加する。
/// </summary>
[DisallowMultipleComponent]
public sealed class TownSimpleRpgSkin2D : MonoBehaviour
{
    public enum TownScreenKind
    {
        RareGoodsShop,
        WeaponMerchant,
        WeaponRepair,
        HomeStorage
    }

    [Header("対象画面")]
    [SerializeField] private TownScreenKind screenKind = TownScreenKind.RareGoodsShop;
    [SerializeField] private RectTransform screenRoot;

    [Header("見た目")]
    [SerializeField] private bool recolorExistingPanels = true;
    [SerializeField] private bool recolorExistingButtons = true;
    [SerializeField] private bool recolorExistingText = true;
    [SerializeField] private bool recolorInventoryCells = true;
    [SerializeField] private bool decorateEquipmentSlots = true;
    [SerializeField, Min(0.1f)] private float cellRefreshInterval = 0.5f;

    [Header("日本語フォント（任意）")]
    [Tooltip("TMP Font Assetを指定すると、この画面内のTextMeshPro文字に適用します。日本語グリフ入りのFont Assetを指定してください。")]
    [SerializeField] private TMP_FontAsset japaneseTmpFont;
    [Tooltip("従来のUI.Textも使っている画面向け。TMPだけなら未設定でOKです。")]
    [SerializeField] private Font japaneseLegacyFont;

    private static readonly Color Backdrop = H("#191D1E");
    private static readonly Color Panel = H("#24292A");
    private static readonly Color InnerPanel = H("#2C3132");
    private static readonly Color Cell = H("#303536");
    private static readonly Color Line = H("#555B59");
    private static readonly Color ButtonNormal = H("#343A3B");
    private static readonly Color ButtonHover = H("#474D4D");
    private static readonly Color ButtonPressed = H("#222729");
    private static readonly Color ActionButton = H("#62584A");
    private static readonly Color ActionHover = H("#796C59");
    private static readonly Color TextMain = H("#E1DFD6");
    private static readonly Color TextDim = H("#A8ADA9");
    private static readonly Color Accent = H("#CAB48B");

    private float nextCellRefresh;

    public TownScreenKind ScreenKind => screenKind;
    public RectTransform ScreenRoot => Root;
    public TMP_FontAsset JapaneseTmpFont => japaneseTmpFont;
    public Font JapaneseLegacyFont => japaneseLegacyFont;

    /// <summary>日本語フォント設定を保存します。nullは変更しないので既存設定が残ります。</summary>
    public void ConfigureJapaneseFonts(TMP_FontAsset tmpFont, Font legacyFont)
    {
        if (tmpFont != null) japaneseTmpFont = tmpFont;
        if (legacyFont != null) japaneseLegacyFont = legacyFont;
    }

    /// <summary>
    /// 配色・配置・イベントは触らずに、画面内の文字のフォントだけ変更します。
    /// Play中に生成されたTMP_Textにも、次回開いた時に適用されます。
    /// </summary>
    [ContextMenu("日本語フォントだけ適用")]
    public void ApplyJapaneseFonts()
    {
        ApplyFontsToHierarchy(Root, japaneseTmpFont, japaneseLegacyFont);
    }

    public static int ApplyFontsToHierarchy(Transform root, TMP_FontAsset tmpFont, Font legacyFont)
    {
        if (root == null || (tmpFont == null && legacyFont == null)) return 0;
        int changed = 0;

        if (tmpFont != null)
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label == null || label.font == tmpFont) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.RecordObject(label, "日本語TMPフォントを設定");
#endif
                label.font = tmpFont;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(label);
#endif
                changed++;
            }
        }

        // 旧uGUI.TextはTMP_FontAssetを使用できないため、専用のFontを適用します。
        if (legacyFont != null)
        {
            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                if (label == null || label.font == legacyFont) continue;
#if UNITY_EDITOR
                if (!Application.isPlaying)
                    UnityEditor.Undo.RecordObject(label, "日本語uGUIフォントを設定");
#endif
                label.font = legacyFont;
#if UNITY_EDITOR
                if (!Application.isPlaying) UnityEditor.EditorUtility.SetDirty(label);
#endif
                changed++;
            }
        }
        return changed;
    }

    public void Configure(TownScreenKind kind, RectTransform root)
    {
        screenKind = kind;
        screenRoot = root;
    }

    private RectTransform Root
    {
        get
        {
            if (screenRoot != null) return screenRoot;
            screenRoot = GetComponent<RectTransform>();
            return screenRoot;
        }
    }

    private void OnEnable()
    {
        // 編集モードでは勝手にPrefab/Sceneを書き換えない。Editorの適用ボタンで反映する。
        if (!Application.isPlaying) return;
        ApplyTheme();
        nextCellRefresh = 0f;
    }

    private void LateUpdate()
    {
        if (!Application.isPlaying || Root == null) return;
        if (!recolorInventoryCells && japaneseTmpFont == null && japaneseLegacyFont == null) return;
        if (Time.unscaledTime < nextCellRefresh) return;
        nextCellRefresh = Time.unscaledTime + Mathf.Max(0.1f, cellRefreshInterval);
        if (recolorInventoryCells) RefreshGeneratedGridCells();
        // 店舗の開閉中に新しく生成されたTMPラベルにも追従する。
        if (japaneseTmpFont != null || japaneseLegacyFont != null) ApplyJapaneseFonts();
    }

    [ContextMenu("シンプルRPG UIを適用")]
    public void ApplyTheme()
    {
        RectTransform root = Root;
        if (root == null)
        {
            Debug.LogWarning("[TownSimpleRpgSkin2D] Screen RootにはRectTransformを指定してください。", this);
            return;
        }

        if (recolorExistingPanels || recolorExistingButtons)
        {
            Image[] images = root.GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image == null || IsInsideInventoryGrid(image.transform) ||
                    IsInsideEquipmentSlot(image.transform) ||
                    image.GetComponentInParent<InventoryItemUI>(true) != null)
                    continue;

                Button button = image.GetComponent<Button>();
                if (button != null && recolorExistingButtons)
                {
                    StyleButton(button, image);
                    continue;
                }

                if (recolorExistingPanels && ShouldStylePanel(image.gameObject, root.gameObject))
                {
                    bool backdrop = image.gameObject == root.gameObject;
                    image.color = backdrop ? Backdrop : IsInset(image.gameObject.name) ? InnerPanel : Panel;
                    // raycastTargetは変更しない。モーダル画面の入力遮断を維持する。
                    ApplyOutline(image, Line, backdrop ? new Vector2(1f, -1f) : new Vector2(1f, -1f));
                }
            }
        }

        if (recolorExistingText)
        {
            foreach (TMP_Text label in root.GetComponentsInChildren<TMP_Text>(true))
            {
                if (label == null || IsInsideInventoryGrid(label.transform) ||
                    IsInsideEquipmentSlot(label.transform) ||
                    label.GetComponentInParent<InventoryItemUI>(true) != null) continue;
                label.color = ResolveTextColor(label.gameObject);
            }

            foreach (Text label in root.GetComponentsInChildren<Text>(true))
            {
                if (label == null || IsInsideInventoryGrid(label.transform) ||
                    IsInsideEquipmentSlot(label.transform) ||
                    label.GetComponentInParent<InventoryItemUI>(true) != null) continue;
                label.color = ResolveTextColor(label.gameObject);
            }
        }

        // インベントリの配置やイベントを変更せず、フォントだけ全TMP_Textへ反映する。
        ApplyJapaneseFonts();

        if (decorateEquipmentSlots)
        {
            foreach (EquipmentSlotUI slot in root.GetComponentsInChildren<EquipmentSlotUI>(true))
            {
                if (slot != null) EnsureEquipmentSlotBackground(slot);
            }
        }

        if (recolorInventoryCells) RefreshGeneratedGridCells();
    }

    private void RefreshGeneratedGridCells()
    {
        RectTransform root = Root;
        if (root == null) return;
        foreach (InventoryGridUI grid in root.GetComponentsInChildren<InventoryGridUI>(true))
        {
            if (grid == null) continue;
            // 生成用プレハブの参照は触らず、実際に作られた CellRoot のImageだけ対象にする。
            Transform cellRoot = grid.transform.Find("CellRoot");
            if (cellRoot == null) continue;
            for (int i = 0; i < cellRoot.childCount; i++)
            {
                Image image = cellRoot.GetChild(i).GetComponent<Image>();
                if (image == null) continue;
                if (image.color != Cell) image.color = Cell;
                image.raycastTarget = false;
            }
        }
    }

    private static void StyleButton(Button button, Image image)
    {
        string key = (button.name + " " + ReadButtonText(button)).ToLowerInvariant();
        bool primary = HasAny(key, "checkout", "confirm", "buy", "sell", "repair", "trade", "purchase", "決済", "取引", "購入", "売却", "修理", "確定", "精算");
        bool secondary = HasAny(key, "close", "back", "return", "cancel", "戻る", "閉じる", "キャンセル");

        Color normal = primary && !secondary ? ActionButton : ButtonNormal;
        Color hover = primary && !secondary ? ActionHover : ButtonHover;
        image.color = Color.white;
        // Button の Image に既存 Sprite が設定されている場合も、イベントや画像参照は変更しない。
        ColorBlock cb = button.colors;
        cb.normalColor = normal;
        cb.highlightedColor = hover;
        cb.pressedColor = ButtonPressed;
        cb.selectedColor = hover;
        cb.disabledColor = H("#272C2D");
        cb.colorMultiplier = 1f;
        cb.fadeDuration = 0.12f;
        button.transition = Selectable.Transition.ColorTint;
        // 既存のtargetGraphicが指定されている場合は差し替えない。
        if (button.targetGraphic == null) button.targetGraphic = image;
        button.colors = cb;
        ApplyOutline(image, Line, new Vector2(1f, -1f));
    }

    private static string ReadButtonText(Button button)
    {
        TMP_Text tmp = button.GetComponentInChildren<TMP_Text>(true);
        if (tmp != null) return tmp.text;
        Text legacy = button.GetComponentInChildren<Text>(true);
        return legacy != null ? legacy.text : string.Empty;
    }

    private static Color ResolveTextColor(GameObject obj)
    {
        string key = obj.name.ToLowerInvariant();
        if (HasAny(key, "money", "price", "total", "gold", "cost", "金額", "合計", "所持金", "費用")) return Accent;
        if (HasAny(key, "hint", "caption", "description", "subtext", "detail", "説明", "注釈")) return TextDim;
        return TextMain;
    }

    private static bool ShouldStylePanel(GameObject obj, GameObject root)
    {
        if (obj == root) return true;
        if (obj.GetComponent<Button>() != null || obj.GetComponent<Mask>() != null ||
            obj.GetComponent<RectMask2D>() != null) return false;
        string name = obj.name.ToLowerInvariant();
        if (HasAny(name, "icon", "portrait", "preview", "sprite", "illustration", "thumbnail", "image", "logo", "item")) return false;
        return HasAny(name, "panel", "background", "backdrop", "border", "frame", "window", "plate", "dialog", "header", "footer", "board", "bg", "contentbg", "base");
    }

    private static bool IsInset(string name)
    {
        string s = name.ToLowerInvariant();
        return HasAny(s, "inner", "info", "details", "detailsbg", "detail", "slot", "content", "list", "price", "summary");
    }

    private static bool IsInsideInventoryGrid(Transform obj)
    {
        return obj.GetComponentInParent<InventoryGridUI>(true) != null;
    }

    private static bool IsInsideEquipmentSlot(Transform obj)
    {
        return obj.GetComponentInParent<EquipmentSlotUI>(true) != null;
    }

    private static void EnsureEquipmentSlotBackground(EquipmentSlotUI slot)
    {
        const string name = "TownSimpleEquipmentBackground";
        Transform found = slot.transform.Find(name);
        GameObject background;
        if (found == null)
        {
            background = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Outline), typeof(LayoutElement));
            background.transform.SetParent(slot.transform, false);
#if UNITY_EDITOR
            if (!Application.isPlaying) UnityEditor.Undo.RegisterCreatedObjectUndo(background, "装備スロット背景を追加");
#endif
        }
        else background = found.gameObject;

        RectTransform rect = background.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        background.transform.SetAsFirstSibling();
        Image image = background.GetComponent<Image>();
        image.color = Cell;
        image.raycastTarget = false;
        LayoutElement layout = background.GetComponent<LayoutElement>();
        if (layout != null) layout.ignoreLayout = true;
        ApplyOutline(image, Line, new Vector2(1f, -1f));
    }

    private static void ApplyOutline(Image image, Color color, Vector2 offset)
    {
        Outline border = image.GetComponent<Outline>();
        if (border == null)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying) border = UnityEditor.Undo.AddComponent<Outline>(image.gameObject);
            else border = image.gameObject.AddComponent<Outline>();
#else
            border = image.gameObject.AddComponent<Outline>();
#endif
        }
        border.effectColor = color;
        border.effectDistance = offset;
        border.useGraphicAlpha = true;
    }

    private static bool HasAny(string value, params string[] words)
    {
        foreach (string word in words)
            if (value.IndexOf(word, StringComparison.OrdinalIgnoreCase) >= 0) return true;
        return false;
    }

    private static Color H(string hex)
    {
        ColorUtility.TryParseHtmlString(hex, out Color value);
        return value;
    }
}
