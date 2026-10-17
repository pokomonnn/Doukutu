#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 既存の出撃補充UIの商人Gridだけを縦スクロール化する。
/// Player側・町の売買画面・実際のInventory容量は変更しない。
/// 何度実行してもScrollRectは二重生成されない。
/// </summary>
public static class ExpeditionMerchantScrollSetup
{
    private const string SupplyRootName = "ExpeditionSupplyPanel_LAYOUT_V2";
    private const string MerchantGridName = "MerchantGridUI";
    private const string ScrollHostName = "ExpeditionMerchantGridScrollArea";
    private const float DefaultViewportWidth = 330f;
    private const float DefaultViewportHeight = 458f;

    [MenuItem("Tools/2D Shooter/出撃補充/商人Gridを縦スクロール化")]
    public static void AddMerchantScroll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Playモードを終了", "Playモードを終了してから実行してください。", "OK");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        InventoryGridUI merchantGrid = FindMerchantGrid(scene);
        if (merchantGrid == null)
        {
            EditorUtility.DisplayDialog("商人Gridが見つかりません",
                "現在のシーンに出撃補充画面の MerchantGridUI がありません。Town_Main の ExpeditionSupplyPanel_LAYOUT_V2 を確認してください。", "OK");
            return;
        }

        ScrollRect scroll = EnsureMerchantScroll(merchantGrid);
        if (scroll == null)
        {
            EditorUtility.DisplayDialog("スクロール追加失敗", "商人Gridの親RectTransformを確認してください。", "OK");
            return;
        }

        EditorUtility.SetDirty(merchantGrid);
        EditorUtility.SetDirty(scroll);
        EditorSceneManager.MarkSceneDirty(scene);
        Selection.activeGameObject = scroll.gameObject;
        EditorUtility.DisplayDialog("商人Gridをスクロール化しました",
            "出撃補充画面の商人Gridだけに縦スクロールを追加しました。\n" +
            "Player側・町の売買UI・在庫容量・セルサイズは変更していません。\n\n" +
            "Ctrl+Sでシーンを保存し、Playでマウスホイールとバーを確認してください。", "OK");
    }

    private static InventoryGridUI FindMerchantGrid(Scene scene)
    {
        InventoryGridUI[] allGrids = Object.FindObjectsByType<InventoryGridUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        foreach (InventoryGridUI grid in allGrids)
        {
            if (grid == null || grid.gameObject.scene != scene || grid.name != MerchantGridName)
                continue;

            for (Transform parent = grid.transform; parent != null; parent = parent.parent)
            {
                if (parent.name == SupplyRootName)
                    return grid;
            }
        }
        return null;
    }

    private static ScrollRect EnsureMerchantScroll(InventoryGridUI merchantGrid)
    {
        RectTransform content = merchantGrid.GetComponent<RectTransform>();
        if (content == null) return null;

        // 既に設定済みの時は破壊・再生成せず、縦スクロールの設定を補修する。
        ScrollRect existing = merchantGrid.GetComponentInParent<ScrollRect>(true);
        if (existing != null)
        {
            if (existing.content != content)
            {
                Debug.LogWarning("[出撃補充] 商人Gridは別のScrollRect内です。" +
                    " 既存のScrollRectを変更せず処理を中断します。", merchantGrid);
                return null;
            }
            ConfigureScroll(existing, merchantGrid);
            EnsureRuntime(existing, merchantGrid);
            return existing;
        }

        RectTransform oldParent = content.parent as RectTransform;
        if (oldParent == null) return null;

        int oldSiblingIndex = content.GetSiblingIndex();
        Vector2 oldAnchorsMin = content.anchorMin;
        Vector2 oldAnchorsMax = content.anchorMax;
        Vector2 oldPivot = content.pivot;
        Vector2 oldPosition = content.anchoredPosition;
        Vector3 oldScale = content.localScale;
        Quaternion oldRotation = content.localRotation;

        GameObject hostGo = NewUIObject(ScrollHostName, oldParent,
            "出撃商人Gridのスクロール枠を追加", typeof(Image), typeof(ScrollRect));
        RectTransform host = hostGo.GetComponent<RectTransform>();
        host.SetSiblingIndex(oldSiblingIndex);
        host.anchorMin = oldAnchorsMin;
        host.anchorMax = oldAnchorsMax;
        host.pivot = oldPivot;
        host.anchoredPosition = oldPosition;
        host.sizeDelta = new Vector2(DefaultViewportWidth, DefaultViewportHeight);
        host.localScale = oldScale;
        host.localRotation = oldRotation;

        // 透明のRaycast用Image。既存のMerchantGridBGの色をそのまま見せる。
        Image hostImage = hostGo.GetComponent<Image>();
        hostImage.color = new Color(1f, 1f, 1f, 0f);
        hostImage.raycastTarget = true;

        GameObject viewportGo = NewUIObject("Viewport", host, "出撃商人Gridの表示窓を追加",
            typeof(Image), typeof(RectMask2D));
        RectTransform viewport = viewportGo.GetComponent<RectTransform>();
        Stretch(viewport);
        Image viewportImage = viewportGo.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0f);
        viewportImage.raycastTarget = true;

        // Grid本体は ScrollRect.Content に配置。既存コンポーネントや参照はそのまま。
        Undo.SetTransformParent(content, viewport, "出撃商人Gridを表示窓へ移動");
        Undo.RecordObject(content, "出撃商人Gridのアンカーを設定");
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(0f, 1f);
        content.pivot = new Vector2(0f, 1f);
        content.anchoredPosition = Vector2.zero;
        content.localScale = Vector3.one;
        content.localRotation = Quaternion.identity;

        GameObject barGo = NewUIObject("VerticalScrollbar", host, "出撃商人Gridの縦バーを追加",
            typeof(Image), typeof(Scrollbar));
        RectTransform barRect = barGo.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(1f, 0f);
        barRect.anchorMax = new Vector2(1f, 1f);
        barRect.pivot = new Vector2(1f, 0.5f);
        barRect.offsetMin = new Vector2(-9f, 4f);
        barRect.offsetMax = new Vector2(-1f, -4f);
        Image barBg = barGo.GetComponent<Image>();
        barBg.color = new Color(0.18f, 0.20f, 0.20f, 0.88f);

        GameObject sliderGo = NewUIObject("SlidingArea", barRect, "出撃商人Gridのバー領域を追加");
        RectTransform slidingArea = sliderGo.GetComponent<RectTransform>();
        Stretch(slidingArea);

        GameObject handleGo = NewUIObject("Handle", slidingArea, "出撃商人Gridのつまみを追加",
            typeof(Image));
        RectTransform handle = handleGo.GetComponent<RectTransform>();
        Stretch(handle);
        Image handleImage = handleGo.GetComponent<Image>();
        handleImage.color = new Color(0.76f, 0.69f, 0.59f, 0.92f);

        Scrollbar scrollbar = barGo.GetComponent<Scrollbar>();
        scrollbar.handleRect = handle;
        scrollbar.targetGraphic = handleImage;
        scrollbar.direction = Scrollbar.Direction.BottomToTop;

        ScrollRect scroll = hostGo.GetComponent<ScrollRect>();
        scroll.viewport = viewport;
        scroll.content = content;
        scroll.verticalScrollbar = scrollbar;
        scroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
        ConfigureScroll(scroll, merchantGrid);
        EnsureRuntime(scroll, merchantGrid);
        return scroll;
    }

    private static void ConfigureScroll(ScrollRect scroll, InventoryGridUI merchantGrid)
    {
        Undo.RecordObject(scroll, "出撃商人Gridの縦スクロールを設定");
        scroll.content = merchantGrid.GetComponent<RectTransform>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.inertia = true;
        scroll.decelerationRate = 0.15f;
        scroll.scrollSensitivity = 45f;
        EditorUtility.SetDirty(scroll);
    }

    private static void EnsureRuntime(ScrollRect scroll, InventoryGridUI merchantGrid)
    {
        ExpeditionMerchantScroll2D runtime = scroll.GetComponent<ExpeditionMerchantScroll2D>();
        if (runtime == null)
            runtime = Undo.AddComponent<ExpeditionMerchantScroll2D>(scroll.gameObject);
        Undo.RecordObject(runtime, "出撃商人Gridの初期位置設定");
        runtime.Configure(scroll, merchantGrid);
        EditorUtility.SetDirty(runtime);
    }

    private static GameObject NewUIObject(string name, Transform parent, string undo,
        params System.Type[] additionalComponents)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, undo);
        Undo.SetTransformParent(go.transform, parent, undo);
        foreach (System.Type type in additionalComponents)
            Undo.AddComponent(go, type);
        return go;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
    }
}
#endif
