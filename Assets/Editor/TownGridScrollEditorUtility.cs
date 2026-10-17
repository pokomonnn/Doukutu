#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 既存のInventoryGridUIを壊さずScrollRectのContentへ移す。
/// Grid自身はサイズを動的更新するため、スクロール範囲も追従する。
/// 町UIからだけ呼び出すこと（出撃補充画面には適用しない）。
/// </summary>
internal static class TownGridScrollEditorUtility
{
    internal static ScrollRect EnsureScroll(InventoryGridUI grid, float viewWidth = 0f, float viewHeight = 0f)
    {
        if (grid == null) return null;
        ScrollRect existing = grid.GetComponentInParent<ScrollRect>(true);
        if (existing != null && existing.content == grid.transform)
        {
            existing.vertical = true;
            existing.horizontal = false;
            existing.movementType = ScrollRect.MovementType.Clamped;
            existing.scrollSensitivity = 42f;
            AttachWheelForwarder(grid, existing);
            EditorUtility.SetDirty(existing);
            return existing;
        }
        RectTransform gridRect = grid.GetComponent<RectTransform>();
        RectTransform oldParent = gridRect.parent as RectTransform;
        if (oldParent == null) return null;

        var scrollGo = new GameObject(grid.name + "_ScrollArea", typeof(RectTransform),
            typeof(Image), typeof(ScrollRect));
        Undo.RegisterCreatedObjectUndo(scrollGo, "町のGridスクロールを追加");
        Undo.SetTransformParent(scrollGo.transform, oldParent, "町のGridスクロールを追加");
        RectTransform host = scrollGo.GetComponent<RectTransform>();
        host.SetSiblingIndex(gridRect.GetSiblingIndex());
        host.anchorMin = gridRect.anchorMin;
        host.anchorMax = gridRect.anchorMax;
        host.pivot = gridRect.pivot;
        host.anchoredPosition = gridRect.anchoredPosition;
        host.sizeDelta = gridRect.sizeDelta;
        host.localScale = gridRect.localScale;
        host.localRotation = gridRect.localRotation;
        if (viewWidth > 0) host.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, viewWidth);
        if (viewHeight > 0) host.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, viewHeight);
        Image background = scrollGo.GetComponent<Image>();
        background.color = new Color(0.17f, 0.20f, 0.20f, 1f);
        background.raycastTarget = true;
        var sourceLayout = grid.GetComponent<LayoutElement>();
        if (sourceLayout != null)
        {
            var destLayout = Undo.AddComponent<LayoutElement>(scrollGo);
            destLayout.minWidth = sourceLayout.minWidth;
            destLayout.minHeight = sourceLayout.minHeight;
            destLayout.preferredWidth = sourceLayout.preferredWidth;
            destLayout.preferredHeight = sourceLayout.preferredHeight;
            destLayout.flexibleWidth = sourceLayout.flexibleWidth;
            destLayout.flexibleHeight = sourceLayout.flexibleHeight;
            Undo.RecordObject(sourceLayout, "GridのLayoutをスクロール窓へ委譲");
            sourceLayout.ignoreLayout = true;
        }
        var viewportGo = new GameObject("Viewport", typeof(RectTransform),
            typeof(Image), typeof(RectMask2D));
        Undo.RegisterCreatedObjectUndo(viewportGo, "町のGrid表示範囲を追加");
        Undo.SetTransformParent(viewportGo.transform, host, "町のGrid表示範囲を追加");
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(4, 4);
        viewport.offsetMax = new Vector2(-20, -4);
        viewport.localScale = Vector3.one;
        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(1, 1, 1, 0f);
        viewportImage.raycastTarget = true;

        Undo.SetTransformParent(gridRect, viewport, "GridをScroll Contentへ移動");
        Undo.RecordObject(gridRect, "GridをScroll Contentへ移動");
        gridRect.anchorMin = new Vector2(0, 1);
        gridRect.anchorMax = new Vector2(0, 1);
        gridRect.pivot = new Vector2(0, 1);
        gridRect.anchoredPosition = Vector2.zero;
        gridRect.localRotation = Quaternion.identity;
        gridRect.localScale = Vector3.one;

        // 縦スクロール可能であることを分かりやすくする細いScrollbar。
        var barGo = new GameObject("VerticalScrollbar", typeof(RectTransform),
            typeof(Image), typeof(Scrollbar));
        Undo.RegisterCreatedObjectUndo(barGo, "縦スクロールバー追加");
        Undo.SetTransformParent(barGo.transform, host, "縦スクロールバー追加");
        RectTransform barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1, 0);
        barRect.anchorMax = new Vector2(1, 1);
        barRect.pivot = new Vector2(1, 0.5f);
        barRect.offsetMin = new Vector2(-15, 6);
        barRect.offsetMax = new Vector2(-3, -6);
        barGo.GetComponent<Image>().color = new Color(0.18f, 0.20f, 0.20f, 0.95f);
        var slideGo = new GameObject("SlidingArea", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(slideGo, "スクロールつまみ領域追加");
        Undo.SetTransformParent(slideGo.transform, barRect, "スクロールつまみ領域追加");
        RectTransform slide = slideGo.GetComponent<RectTransform>();
        slide.anchorMin = Vector2.zero; slide.anchorMax = Vector2.one;
        slide.offsetMin = Vector2.zero; slide.offsetMax = Vector2.zero;
        var handleGo = new GameObject("Handle", typeof(RectTransform), typeof(Image));
        Undo.RegisterCreatedObjectUndo(handleGo, "スクロールつまみ追加");
        Undo.SetTransformParent(handleGo.transform, slide, "スクロールつまみ追加");
        RectTransform handle = handleGo.GetComponent<RectTransform>();
        handle.anchorMin = Vector2.zero; handle.anchorMax = Vector2.one;
        handle.offsetMin = Vector2.zero; handle.offsetMax = Vector2.zero;
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = new Color(0.62f, 0.59f, 0.51f, 0.9f);
        Scrollbar verticalBar = barGo.GetComponent<Scrollbar>();
        verticalBar.handleRect = handle;
        verticalBar.targetGraphic = handleImage;
        verticalBar.direction = Scrollbar.Direction.BottomToTop;
        ScrollRect scroll = scrollGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = gridRect;
        scroll.vertical = true;
        scroll.horizontal = false;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.verticalScrollbar = verticalBar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        scroll.verticalScrollbarSpacing = -2f;
        scroll.inertia = true;
        scroll.decelerationRate = 0.15f;
        scroll.scrollSensitivity = 42f;
        AttachWheelForwarder(grid, scroll);
        EditorUtility.SetDirty(scroll);
        return scroll;
    }

    private static void AttachWheelForwarder(InventoryGridUI grid, ScrollRect scroll)
    {
        TownGridScrollRuntime2D wheel = grid.GetComponent<TownGridScrollRuntime2D>();
        if (wheel == null) wheel = Undo.AddComponent<TownGridScrollRuntime2D>(grid.gameObject);
        Undo.RecordObject(wheel, "町のGridホイール処理");
        wheel.Configure(scroll);
        EditorUtility.SetDirty(wheel);
    }

    internal static void SetCellSize(InventoryGridUI grid, float size)
    {
        if (grid == null) return;
        Undo.RecordObject(grid, "町の売買Gridセルサイズ変更");
        SerializedObject so = new SerializedObject(grid);
        SerializedProperty prop = so.FindProperty("cellSize");
        if (prop != null) prop.floatValue = size;
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(grid);
    }
}
#endif
