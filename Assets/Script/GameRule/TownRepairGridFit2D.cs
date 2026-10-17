using UnityEngine;

/// <summary>
/// Player Inventoryの縦横サイズに応じて、修理画面の表示枠に収める。
/// 実際のGrid座標/CellSizeを変更せずRectTransformだけを等倍以下に縮める。
/// ドラッグ判定はRectTransformUtility経由なので縮小後も座標が一致する。
/// </summary>
[DisallowMultipleComponent]
public sealed class TownRepairGridFit2D : MonoBehaviour
{
    [SerializeField] private InventoryGridUI playerGrid;
    [SerializeField] private RectTransform frame;
    [SerializeField, Min(0f)] private float padding = 14f;
    private int lastWidth = -1;
    private int lastHeight = -1;
    private Vector2 lastFrame;
    private float lastCell = -1f;

    public void Configure(InventoryGridUI grid, RectTransform targetFrame)
    {
        playerGrid = grid;
        frame = targetFrame;
        lastWidth = -1;
    }

    private void OnEnable() { lastWidth = -1; }

    private void LateUpdate()
    {
        if (playerGrid == null || frame == null || playerGrid.Grid == null) return;
        int w = playerGrid.Grid.Width, h = playerGrid.Grid.Height;
        if (w <= 0 || h <= 0) return;
        Vector2 size = frame.rect.size;
        float cell = playerGrid.CellSize;
        if (w == lastWidth && h == lastHeight && size == lastFrame && Mathf.Approximately(cell, lastCell)) return;
        playerGrid.RefreshInventoryUI();
        float step = cell + playerGrid.CellSpacing;
        float gridW = w * cell + Mathf.Max(0, w - 1) * playerGrid.CellSpacing;
        float gridH = h * cell + Mathf.Max(0, h - 1) * playerGrid.CellSpacing;
        if (gridW <= 0f || gridH <= 0f) return;
        float usableW = Mathf.Max(1f, size.x - padding * 2f);
        float usableH = Mathf.Max(1f, size.y - padding * 2f);
        float scale = Mathf.Min(1f, usableW / gridW, usableH / gridH);
        RectTransform rect = playerGrid.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
        rect.pivot = new Vector2(0f, 1f);
        rect.localScale = new Vector3(scale, scale, 1f);
        rect.anchoredPosition = new Vector2(
            (size.x - gridW * scale) * 0.5f,
            -(size.y - gridH * scale) * 0.5f);
        lastWidth = w;
        lastHeight = h;
        lastFrame = size;
        lastCell = cell;
    }
}
