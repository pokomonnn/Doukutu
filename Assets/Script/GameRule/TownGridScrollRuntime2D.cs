using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// アイテム上でもホイールを確実にScrollRectへ渡す。
/// Gridの子オブジェクトがポインタを受け取る時のスクロール取りこぼし対策。
/// ScrollRectのContentになっているInventoryGridUIへ追加する。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InventoryGridUI))]
public sealed class TownGridScrollRuntime2D : MonoBehaviour, IScrollHandler
{
    [SerializeField] private ScrollRect scrollRect;
    private InventoryGridUI grid;
    private bool initialized;

    public void Configure(ScrollRect target) { scrollRect = target; }

    private void Awake()
    {
        grid = GetComponent<InventoryGridUI>();
        if (scrollRect == null) scrollRect = GetComponentInParent<ScrollRect>();
    }

    private void OnEnable() { initialized = false; }

    private void LateUpdate()
    {
        if (initialized || scrollRect == null || grid == null || grid.Grid == null) return;
        if (grid.Grid.Width <= 0 || grid.Grid.Height <= 0) return;
        grid.RefreshInventoryUI();
        Canvas.ForceUpdateCanvases();
        scrollRect.verticalNormalizedPosition = 1f;
        initialized = true;
    }

    public void OnScroll(PointerEventData eventData)
    {
        if (scrollRect == null || !scrollRect.isActiveAndEnabled) return;
        scrollRect.OnScroll(eventData);
    }
}
