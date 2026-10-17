using UnityEngine;

/// <summary>
/// 指定したStorage/商人在庫の高さを下限行数以上に維持する。
/// 保存済みの箱が旧サイズで復元された後も、アイテムを保持したまま拡張する。
/// 対象ItemBoxInventoryと同じGameObjectにのみ付けること。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemBoxInventory))]
public sealed class TownItemBoxMinRows2D : MonoBehaviour
{
    [SerializeField, Min(1)] private int minimumRows = 20;
    private ItemBoxInventory box;
    private bool expanding;

    public int MinimumRows => minimumRows;

    public void Configure(int rows)
    {
        minimumRows = Mathf.Max(1, rows);
    }

    private void Awake()
    {
        box = GetComponent<ItemBoxInventory>();
    }

    private void OnEnable()
    {
        if (box == null) box = GetComponent<ItemBoxInventory>();
        if (box != null) box.OnInventoryChanged += EnsureRows;
        EnsureRows();
    }

    private void Start() { EnsureRows(); }

    private void OnDisable()
    {
        if (box != null) box.OnInventoryChanged -= EnsureRows;
    }

    private void EnsureRows()
    {
        if (expanding || box == null || !Application.isPlaying) return;
        expanding = true;
        try { box.EnsureGridHeightAtLeast(minimumRows); }
        finally { expanding = false; }
    }
}
