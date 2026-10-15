using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Stage出撃地点選択 -> 出撃前の一体型売買・医療補充画面 -> Dungeon。
/// 出撃確定/ミッション受注/シーン保存は既存StageExpeditionPanelControllerに任せる。
/// このComponentは表示パネルの子ではなく、常時ActiveなManagerへ付ける。
/// </summary>
[DisallowMultipleComponent]
public class ExpeditionSupplyPanelController2D : MonoBehaviour
{
    [Header("表示パネル")]
    [SerializeField] private GameObject supplyPanelRoot;
    [SerializeField] private TMP_Text stageNameText;
    [SerializeField] private TMP_Text entryPointNameText;
    [SerializeField] private TMP_Text statusText;

    [Header("ボタン")]
    [SerializeField] private Button backButton;
    [SerializeField] private Button finalDepartButton;
    [SerializeField] private Button openNormalItemShopButton;

    [Header("一体型売買")]
    [SerializeField] private ExpeditionUnifiedTradeController2D unifiedTrade;
    [SerializeField] private MerchantStockInventory supplyMerchantStock;
    [SerializeField] private MedicalKitSupplyShop2D medicalKitSupplyShop;

    [Header("旧売買UIとの互換（任意）")]
    [SerializeField] private PawnShopUIController existingPawnShop;

    [Header("画面登場アニメーション")]
    [SerializeField] private bool playOpenAnimation = true;
    [SerializeField, Min(0.05f)] private float openDuration = 0.36f;
    [SerializeField, Range(0.5f, 1f)] private float startScale = 0.93f;
    [SerializeField] private float startYOffset = -52f;
    [SerializeField] private AnimationCurve openEase = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 0f),
        new Keyframe(1f, 1f, 0f, 0f));
    [SerializeField] private bool hidePanelOnAwake = true;

    [Header("出撃時の暗転演出")]
    [Tooltip("出撃ボタンを押した後、画面全体を黒くしてから出撃処理を開始します。")]
    [SerializeField] private bool playDepartureFade = true;
    [Tooltip("黒くなるまでの時間（秒）。TimeScale=0でも動作します。")]
    [SerializeField, Min(0.05f)] private float departureFadeDuration = 0.9f;
    [Tooltip("暗転の進み方。0→1のカーブを指定します。")]
    [SerializeField] private AnimationCurve departureFadeEase = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("効果音")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip openClip;
    [SerializeField] private AudioClip closeClip;
    [Tooltip("補充画面側から鳴らす任意の出撃SE。旧UI生成時は決済SEが割り当てられています。")]
    [SerializeField] private AudioClip departClip;
    [Tooltip("通常はOFF。出撃先のStageExpeditionPanelController側で出撃SEを鳴らすため、補充画面側のSEは二重再生しません。専用SEを別途鳴らしたい場合だけONにしてください。")]
    [SerializeField] private bool playAdditionalDepartureClip = false;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.85f;

    private StageExpeditionPanelController stagePanel;
    private Coroutine openCoroutine;
    private CanvasGroup group;
    private RectTransform panelRect;
    private Vector2 originalPosition;
    private Vector3 originalScale;
    private bool departureInProgress;
    private GameObject fadeCanvasRoot;
    private CanvasGroup fadeGroup;
    private bool isOpen;
    public bool IsOpen => isOpen;

    private void Awake()
    {
        if (supplyPanelRoot == null) supplyPanelRoot = gameObject;
        backButton?.onClick.AddListener(BackToStage);
        finalDepartButton?.onClick.AddListener(DepartToDungeon);
        openNormalItemShopButton?.onClick.AddListener(OpenNormalItemShop);
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        if (supplyPanelRoot != null)
        {
            group = supplyPanelRoot.GetComponent<CanvasGroup>();
            if (group == null) group = supplyPanelRoot.AddComponent<CanvasGroup>();
            panelRect = supplyPanelRoot.GetComponent<RectTransform>();
            if (panelRect != null)
            {
                originalScale = panelRect.localScale;
                originalPosition = panelRect.anchoredPosition;
            }
            if (hidePanelOnAwake) supplyPanelRoot.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        backButton?.onClick.RemoveListener(BackToStage);
        finalDepartButton?.onClick.RemoveListener(DepartToDungeon);
        openNormalItemShopButton?.onClick.RemoveListener(OpenNormalItemShop);
        // このManagerだけを破棄したときも黒い画面を残さない。
        if (fadeCanvasRoot != null) Destroy(fadeCanvasRoot);
    }

    public bool OpenForStage(StageExpeditionPanelController origin)
    {
        if (isOpen || departureInProgress || origin == null || origin.CurrentStageData == null || supplyPanelRoot == null) return false;
        if (origin.PanelRoot != null &&
            (origin.PanelRoot == supplyPanelRoot || supplyPanelRoot.transform.IsChildOf(origin.PanelRoot.transform)))
        {
            Debug.LogError("[ExpeditionSupply] 補充画面は出撃選択パネルの子にしないでください。", this);
            return false;
        }
        stagePanel = origin;
        if (existingPawnShop != null && existingPawnShop.IsOpen && !existingPawnShop.TryClosePawnShop())
            return false;
        isOpen = true;
        supplyPanelRoot.SetActive(true);
        supplyPanelRoot.transform.SetAsLastSibling();
        if (stageNameText != null) stageNameText.text = origin.CurrentStageData.StageName;
        if (entryPointNameText != null)
            entryPointNameText.text = origin.SelectedEntryPoint != null
                ? "出撃地点：" + origin.SelectedEntryPoint.DisplayName : "出撃地点：指定なし";
        if (statusText != null) statusText.text = "";
        if (unifiedTrade == null) unifiedTrade = GetComponent<ExpeditionUnifiedTradeController2D>();
        if (unifiedTrade != null && !unifiedTrade.OpenTrade(supplyMerchantStock))
            Debug.LogWarning("[ExpeditionSupply] 通常売買の接続に失敗しました。Inventoryと商人Stockの参照を確認してください。", this);
        medicalKitSupplyShop?.RefreshUI();
        if (backButton != null) backButton.interactable = true;
        if (finalDepartButton != null) finalDepartButton.interactable = true;
        Play(openClip);
        if (openCoroutine != null) StopCoroutine(openCoroutine);
        if (group != null)
        {
            group.alpha = playOpenAnimation ? 0f : 1f;
            group.interactable = !playOpenAnimation;
            group.blocksRaycasts = !playOpenAnimation;
        }
        if (panelRect != null)
        {
            panelRect.localScale = playOpenAnimation ? originalScale * startScale : originalScale;
            panelRect.anchoredPosition = playOpenAnimation
                ? originalPosition + new Vector2(0f, startYOffset) : originalPosition;
        }
        if (playOpenAnimation) openCoroutine = StartCoroutine(OpenRoutine());
        return true;
    }

    private IEnumerator OpenRoutine()
    {
        float elapsed = 0;
        while (elapsed < openDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = openEase != null ? openEase.Evaluate(Mathf.Clamp01(elapsed / openDuration))
                : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / openDuration));
            if (group != null) group.alpha = Mathf.Clamp01(t);
            if (panelRect != null)
            {
                panelRect.localScale = Vector3.LerpUnclamped(originalScale * startScale, originalScale, t);
                panelRect.anchoredPosition = Vector2.LerpUnclamped(originalPosition + new Vector2(0f, startYOffset), originalPosition, t);
            }
            yield return null;
        }
        if (group != null)
        {
            group.alpha = 1f;
            group.interactable = true;
            group.blocksRaycasts = true;
        }
        if (panelRect != null)
        {
            panelRect.localScale = originalScale;
            panelRect.anchoredPosition = originalPosition;
        }
        openCoroutine = null;
    }

    public void BackToStage()
    {
        if (!isOpen || departureInProgress) return;
        if (existingPawnShop != null && existingPawnShop.IsOpen && !existingPawnShop.TryClosePawnShop())
        {
            SetStatus("売却カートが閉じられません。Inventoryの空きを確認してください。");
            return;
        }
        if (openCoroutine != null) StopCoroutine(openCoroutine);
        openCoroutine = null;
        unifiedTrade?.CloseTrade();
        Play(closeClip);
        isOpen = false;
        supplyPanelRoot.SetActive(false);
        stagePanel?.ReturnFromSupply();
    }

    public void DepartToDungeon()
    {
        // 多重クリックや別画面からの連続呼び出しを防止。
        if (!isOpen || stagePanel == null || departureInProgress) return;
        if (openCoroutine != null) return;

        // 暗転前に既存の出撃条件をチェック。未決済なら何も変更しない。
        if (unifiedTrade != null && unifiedTrade.HasPendingSelections)
        {
            SetStatus("未決済の選択があります。決済するか、各アイテムをもう一度クリックして解除してください。");
            return;
        }
        if (existingPawnShop != null &&
            existingPawnShop.IsOpen &&
            !existingPawnShop.TryClosePawnShop())
        {
            SetStatus("売却カートが閉じられません。Inventoryの空きを確認してください。");
            return;
        }

        departureInProgress = true;
        if (finalDepartButton != null) finalDepartButton.interactable = false;
        if (backButton != null) backButton.interactable = false;
        if (openNormalItemShopButton != null) openNormalItemShopButton.interactable = false;

        // 旧自動生成UIは departClip に決済SE（checkoutSfx）を設定していました。
        // StageExpeditionPanelController が鳴らす出撃SEと重ならないよう、
        // 補充画面からの追加SEは明示的にONにした場合だけ再生します。
        if (playAdditionalDepartureClip)
        {
            Play(departClip);
        }
        StartCoroutine(DepartureRoutine());
    }

    private IEnumerator DepartureRoutine()
    {
        // アニメーションをOFFにしても既存の出撃手順はそのまま実行できる。
        if (playDepartureFade)
        {
            EnsureFadeOverlay();
            if (fadeGroup != null)
            {
                fadeGroup.alpha = 0f;
                float elapsed = 0f;
                float duration = Mathf.Max(0.05f, departureFadeDuration);

                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    float t = Mathf.Clamp01(elapsed / duration);
                    fadeGroup.alpha = departureFadeEase != null
                        ? Mathf.Clamp01(departureFadeEase.Evaluate(t)) : t;
                    yield return null;
                }

                fadeGroup.alpha = 1f;
                // 完全に黒くなったフレームを描画してから出撃処理を呼ぶ。
                yield return null;
            }
        }

        // セーブ／ミッション受注／シーン切替は元のStage側へすべて委譲。
        // 元の関数が自前で暗転する場合は、画面が黒いままその処理を待つ。
        bool departed = false;
        if (stagePanel != null)
        {
            try
            {
                departed = stagePanel.StartFinalDepartureFromSupply();
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }
        }

        if (departed)
        {
            isOpen = false;
            // フェードCanvasはTownシーンに属するため、シーン切替時に破棄される。
            // 出撃処理による切替が非同期の場合も、切替完了まで黒画面を保つ。
            yield break;
        }

        // 出撃を開始できなかった場合は元の画面へ戻して再操作可能にする。
        SetStatus("出撃できません。ステージ・出撃地点を確認してください。");
        if (fadeGroup != null)
        {
            float elapsed = 0f;
            const float returnDuration = 0.25f;
            while (elapsed < returnDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                fadeGroup.alpha = 1f - Mathf.Clamp01(elapsed / returnDuration);
                yield return null;
            }
        }
        ClearFadeOverlay();

        if (finalDepartButton != null) finalDepartButton.interactable = true;
        if (backButton != null) backButton.interactable = true;
        if (openNormalItemShopButton != null) openNormalItemShopButton.interactable = true;
        departureInProgress = false;
    }

    /// <summary>
    /// TownのUI階層と独立した最前面Canvasを作成する。
    /// 補充PanelのCanvasGroupを使わないため、パネルだけでなく画面全体が暗くなる。
    /// インスペクタでFade用Imageを配置する必要はありません。
    /// </summary>
    private void EnsureFadeOverlay()
    {
        if (fadeCanvasRoot != null && fadeGroup != null) return;

        fadeCanvasRoot = new GameObject("ExpeditionDepartureFadeCanvas",
            typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        Canvas canvas = fadeCanvasRoot.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.overrideSorting = true;
        canvas.sortingOrder = 32000;

        GameObject overlay = new GameObject("FullscreenBlack",
            typeof(RectTransform), typeof(Image), typeof(CanvasGroup));
        overlay.transform.SetParent(fadeCanvasRoot.transform, false);
        RectTransform rect = overlay.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.localScale = Vector3.one;

        Image image = overlay.GetComponent<Image>();
        image.color = Color.black;
        image.raycastTarget = true; // フェード中に下のUIをクリックさせない。

        fadeGroup = overlay.GetComponent<CanvasGroup>();
        fadeGroup.alpha = 0f;
        fadeGroup.interactable = true;
        fadeGroup.blocksRaycasts = true;
    }

    private void ClearFadeOverlay()
    {
        fadeGroup = null;
        if (fadeCanvasRoot != null) Destroy(fadeCanvasRoot);
        fadeCanvasRoot = null;
    }

    public void OpenNormalItemShop()
    {
        if (!isOpen || departureInProgress || existingPawnShop == null || supplyMerchantStock == null) return;
        existingPawnShop.OpenPawnShop(supplyMerchantStock);
    }

    private void SetStatus(string message)
    {
        if (statusText != null) statusText.text = message;
        Debug.LogWarning("[ExpeditionSupply] " + message, this);
    }

    private void Play(AudioClip clip)
    {
        if (clip != null && audioSource != null) audioSource.PlayOneShot(clip, soundVolume);
    }
}
