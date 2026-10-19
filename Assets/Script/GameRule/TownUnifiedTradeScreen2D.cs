using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 町の希少品店・武器店が共用する、一体型売買パネルの開閉と軽いアニメーション。
/// 実際の一括決済は既存ExpeditionUnifiedTradeController2Dに委譲する。
/// このコンポーネントは非表示になるTradeRootの外にある、常時有効なManagerへ置く。
/// </summary>
[DisallowMultipleComponent]
public sealed class TownUnifiedTradeScreen2D : MonoBehaviour
{
    [Header("共通売買画面")]
    [SerializeField] private GameObject tradeRoot;
    [SerializeField] private RectTransform windowRect;
    [SerializeField] private CanvasGroup windowCanvasGroup;
    [SerializeField] private TMP_Text shopNameText;
    [SerializeField] private Button closeButton;

    [Header("連携（非表示パネルの外に配置）")]
    [SerializeField] private ExpeditionUnifiedTradeController2D tradeController;
    [SerializeField] private PawnShopUIController pawnShopController;
    [SerializeField] private PlayerTradeInventoryBridge playerTradeInventory;

    [Header("ポップアップ演出")]
    [SerializeField, Min(0f)] private float openDuration = 0.2f;
    [SerializeField, Min(0f)] private float closeDuration = 0.24f;
    [SerializeField, Range(0.5f, 1f)] private float startingScale = 0.95f;

    private Coroutine animation;
    private bool isClosing;
    private bool isOpen;
    private Vector3 originalScale = Vector3.one;

    public bool IsOpen => isOpen;

    /// <summary>洞窟用Prefabで呼ぶ。町の画面には影響しない。</summary>
    public void BindDungeonPlayer(InventoryController inventory, PlayerTradeInventoryBridge bridge,
        GameSessionManager session, EquipmentController equipment = null)
    {
        playerTradeInventory = bridge;
        pawnShopController = null; // 町の質屋の閉じる判定は洞窟では使わない
        if (tradeController != null) tradeController.BindDungeonPlayer(inventory, bridge, session, equipment);
    }

    private void Awake()
    {
        if (windowRect != null) originalScale = windowRect.localScale;
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(OnCloseClicked);
            closeButton.onClick.AddListener(OnCloseClicked);
        }
        if (tradeRoot != null) tradeRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (closeButton != null) closeButton.onClick.RemoveListener(OnCloseClicked);
    }

    public bool Open(MerchantStockInventory stock)
    {
        if (stock == null || tradeRoot == null || tradeController == null ||
            windowRect == null || windowCanvasGroup == null)
        {
            Debug.LogWarning("[TownUnifiedTradeScreen2D] 売買画面・Controller・Windowの参照が不足しています。", this);
            return false;
        }

        StopAnimation();
        isClosing = false;
        isOpen = false;
        ResetVisuals();
        tradeRoot.SetActive(true);

        // 成功するまで画面の操作を無効にし、初期化失敗時は元の状態へ戻す。
        windowCanvasGroup.interactable = false;
        windowCanvasGroup.blocksRaycasts = false;
        if (!tradeController.OpenTrade(stock))
        {
            tradeRoot.SetActive(false);
            ResetVisuals();
            return false;
        }

        if (shopNameText != null) shopNameText.text = stock.ShopName;
        isOpen = true;
        animation = StartCoroutine(AnimateOpen());
        return true;
    }

    /// <summary>共通売買が扱った同じPlayer Inventory/Equipmentを保存します。</summary>
    public void CapturePlayerSession()
    {
        playerTradeInventory?.CaptureToSession();
    }

    /// <summary>Controller側から即時終了。キャンセルされた商品選択は破棄し、Inventoryは移動させない。</summary>
    public void Close()
    {
        StopAnimation();
        tradeController?.CloseTrade();
        isClosing = false;
        isOpen = false;
        ResetVisuals();
        if (tradeRoot != null) tradeRoot.SetActive(false);
    }

    private void OnCloseClicked()
    {
        if (!isOpen || isClosing) return;
        isClosing = true;
        if (windowCanvasGroup != null)
        {
            windowCanvasGroup.interactable = false;
            windowCanvasGroup.blocksRaycasts = false;
        }
        StopAnimation();
        animation = StartCoroutine(AnimateClose());
    }

    private IEnumerator AnimateOpen()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.001f, openDuration);
        if (windowCanvasGroup != null) windowCanvasGroup.alpha = 0f;
        if (windowRect != null) windowRect.localScale = originalScale * startingScale;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (windowRect != null) windowRect.localScale = originalScale * Mathf.Lerp(startingScale, 1f, t);
            if (windowCanvasGroup != null) windowCanvasGroup.alpha = t;
            yield return null;
        }
        ResetVisuals();
        animation = null;
    }

    private IEnumerator AnimateClose()
    {
        float elapsed = 0f;
        float duration = Mathf.Max(0.001f, closeDuration);
        Vector3 fromScale = windowRect != null ? windowRect.localScale : originalScale;
        float fromAlpha = windowCanvasGroup != null ? windowCanvasGroup.alpha : 1f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            if (windowRect != null) windowRect.localScale = Vector3.Lerp(fromScale, originalScale * startingScale, t);
            if (windowCanvasGroup != null) windowCanvasGroup.alpha = Mathf.Lerp(fromAlpha, 0f, t);
            yield return null;
        }
        animation = null;

        // 在庫不足などでControllerが閉鎖を拒んだ場合、UIを元の状態へ戻す。
        bool closed = pawnShopController == null || pawnShopController.TryClosePawnShop();
        if (closed)
        {
            if (pawnShopController == null) Close();
        }
        else
        {
            isClosing = false;
            ResetVisuals();
        }
    }

    private void ResetVisuals()
    {
        if (windowRect != null) windowRect.localScale = originalScale;
        if (windowCanvasGroup != null)
        {
            windowCanvasGroup.alpha = 1f;
            windowCanvasGroup.interactable = true;
            windowCanvasGroup.blocksRaycasts = true;
        }
    }

    private void StopAnimation()
    {
        if (animation == null) return;
        StopCoroutine(animation);
        animation = null;
    }
}
