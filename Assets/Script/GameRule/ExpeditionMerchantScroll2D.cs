using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 出撃アイテム補充画面の商人Grid専用。
/// 在庫の行数に合わせて InventoryGridUI が Content を拡張しても、
/// 表示枠（Viewport）を固定したまま上から縦スクロールできるようにする。
/// 在庫数・セルサイズ・売買処理には一切触れません。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ScrollRect))]
public sealed class ExpeditionMerchantScroll2D : MonoBehaviour
{
    [SerializeField] private ScrollRect scrollRect;
    [SerializeField] private InventoryGridUI merchantGrid;
    [SerializeField] private bool resetToTopOnOpen = true;

    private Coroutine resetCoroutine;

    public void Configure(ScrollRect targetScroll, InventoryGridUI targetGrid)
    {
        scrollRect = targetScroll;
        merchantGrid = targetGrid;
    }

    private void Awake()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        if (resetCoroutine != null)
            StopCoroutine(resetCoroutine);

        if (resetToTopOnOpen && Application.isPlaying)
            resetCoroutine = StartCoroutine(ResetToTopAfterRebuild());
    }

    private void OnDisable()
    {
        if (resetCoroutine == null) return;
        StopCoroutine(resetCoroutine);
        resetCoroutine = null;
    }

    private void ResolveReferences()
    {
        if (scrollRect == null)
            scrollRect = GetComponent<ScrollRect>();
        if (merchantGrid == null && scrollRect != null && scrollRect.content != null)
            merchantGrid = scrollRect.content.GetComponent<InventoryGridUI>();
    }

    private IEnumerator ResetToTopAfterRebuild()
    {
        // 商人在庫は補充画面が開く際に再バインドされることがある。
        // Grid の OnEnable / RebuildGridUI 後に高さを確定してから上端へ移動。
        yield return null;
        ResolveReferences();
        if (merchantGrid != null)
            merchantGrid.RefreshInventoryUI();

        yield return null;
        if (scrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            scrollRect.StopMovement();
            scrollRect.verticalNormalizedPosition = 1f;
        }
        resetCoroutine = null;
    }
}
