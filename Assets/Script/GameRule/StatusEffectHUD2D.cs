using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// プレイヤーの状態効果を右上に縦表示。
/// 新しい効果は最上段、消滅時は自動詰め、フェード/スライド演出付き。
/// 必ずゲームプレイ中も有効なCanvas配下に配置してください。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class StatusEffectHUD2D : MonoBehaviour
{
    [Header("PlayerのStatusEffectManager2D")]
    [SerializeField] private StatusEffectManager2D manager;

    [Header("右上の位置・間隔")]
    [SerializeField, Min(24f)] private float iconSize = 58f;
    [SerializeField, Min(0f)] private float iconSpacing = 8f;
    [SerializeField, Min(0f)] private float rightMargin = 24f;
    [SerializeField, Min(0f)] private float topMargin = 24f;

    [Header("アニメーション（秒）")]
    [SerializeField, Min(0.05f)] private float fadeSeconds = 0.28f;
    [SerializeField, Min(0.05f)] private float shiftSmoothSeconds = 0.18f;

    [Header("説明ウィンドウ")]
    [SerializeField] private TMP_FontAsset japaneseFont;

    private RectTransform panel;
    private Canvas rootCanvas;
    private StatusEffectTooltipUI2D tooltip;
    private StatusEffectManager2D subscribedManager;
    private float nextLookupTime;

    private readonly Dictionary<StatusEffectData2D, StatusEffectIconUI2D> views =
        new Dictionary<StatusEffectData2D, StatusEffectIconUI2D>();

    private void Awake()
    {
        panel = GetComponent<RectTransform>();
        Canvas canvas = GetComponentInParent<Canvas>();
        rootCanvas = canvas != null ? canvas.rootCanvas : null;
        PositionPanel();
        ResolveManager();
    }

    private void OnEnable()
    {
        ConnectManager();
    }

    private void Start()
    {
        if (rootCanvas == null)
        {
            Debug.LogError("[StatusEffectHUD2D] 親のCanvasがありません。ゲーム用HUDのCanvasの子に配置してください。", this);
            return;
        }
        CreateTooltipIfNeeded();
        ConnectManager();
        RefreshEffects();
        if (manager == null)
            Debug.LogWarning("[StatusEffectHUD2D] StatusEffectManager2Dが見つかりません。Manager欄へPlayerのManagerを指定してください。", this);
    }

    private void Update()
    {
        if (Time.unscaledTime < nextLookupTime) return;
        nextLookupTime = Time.unscaledTime + 1f;
        if (manager == null || subscribedManager != manager)
            ConnectManager();
    }

    private void OnDisable()
    {
        DisconnectManager();
        if (tooltip != null) tooltip.Hide();
    }

    private void OnDestroy()
    {
        if (tooltip != null) Destroy(tooltip.gameObject);
    }

    private void ResolveManager()
    {
        if (manager != null) return;

        // シーンに複数Managerがある場合、まずPlayerのものを優先する。
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            manager = player.GetComponent<StatusEffectManager2D>();
            if (manager == null)
                manager = player.GetComponentInChildren<StatusEffectManager2D>(true);
        }
        if (manager == null)
            manager = FindAnyObjectByType<StatusEffectManager2D>();
    }

    private void ConnectManager()
    {
        ResolveManager();
        if (subscribedManager == manager) return;
        DisconnectManager();
        if (manager == null) return;

        manager.EffectsChanged += RefreshEffects;
        subscribedManager = manager;
        RefreshEffects();
    }

    private void DisconnectManager()
    {
        if (subscribedManager != null)
            subscribedManager.EffectsChanged -= RefreshEffects;
        subscribedManager = null;
    }

    private void PositionPanel()
    {
        if (panel == null) return;
        panel.anchorMin = new Vector2(1f, 1f);
        panel.anchorMax = new Vector2(1f, 1f);
        panel.pivot = new Vector2(1f, 1f);
        panel.anchoredPosition = new Vector2(-rightMargin, -topMargin);
        panel.sizeDelta = new Vector2(iconSize, 0f);
    }

    private void RefreshEffects()
    {
        if (manager == null || panel == null || !isActiveAndEnabled) return;

        IReadOnlyList<ActiveStatusEffect2D> list = manager.ActiveEffects;
        var present = new HashSet<StatusEffectData2D>();

        for (int i = 0; i < list.Count; i++)
        {
            ActiveStatusEffect2D entry = list[i];
            if (entry == null || entry.Data == null) continue;
            present.Add(entry.Data);

            bool isNew = !views.TryGetValue(entry.Data, out StatusEffectIconUI2D view)
                || view == null;
            if (isNew)
            {
                var go = new GameObject("StatusIcon_" + entry.Data.name,
                    typeof(RectTransform), typeof(CanvasGroup), typeof(Image),
                    typeof(StatusEffectIconUI2D));
                go.transform.SetParent(panel, false);
                view = go.GetComponent<StatusEffectIconUI2D>();
                view.Initialize(this, entry, iconSize, fadeSeconds,
                    shiftSmoothSeconds, japaneseFont);
                views[entry.Data] = view;
            }
            view.SetPositionIndex(i, iconSize, iconSpacing, isNew);
        }

        var removed = new List<StatusEffectData2D>();
        foreach (KeyValuePair<StatusEffectData2D, StatusEffectIconUI2D> pair in views)
        {
            if (present.Contains(pair.Key)) continue;
            if (pair.Value != null) pair.Value.BeginRemoval();
            removed.Add(pair.Key);
        }
        foreach (StatusEffectData2D data in removed) views.Remove(data);
    }

    public void ShowTooltipFor(ActiveStatusEffect2D entry)
    {
        CreateTooltipIfNeeded();
        if (tooltip != null) tooltip.Show(entry);
    }

    public void HideTooltipFor(ActiveStatusEffect2D entry)
    {
        if (tooltip != null) tooltip.HideIf(entry);
    }

    private void CreateTooltipIfNeeded()
    {
        if (tooltip != null || rootCanvas == null) return;
        var go = new GameObject("StatusEffectTooltip", typeof(RectTransform),
            typeof(CanvasGroup), typeof(Image), typeof(StatusEffectTooltipUI2D));
        go.transform.SetParent(rootCanvas.transform, false);
        tooltip = go.GetComponent<StatusEffectTooltipUI2D>();
        tooltip.Initialize(rootCanvas, japaneseFont);
    }

    [ContextMenu("診断 / HUD接続状態をConsoleに表示")]
    private void LogHudDiagnostics()
    {
        ConnectManager();
        Canvas canvas = GetComponentInParent<Canvas>();
        string canvasName = canvas == null ? "なし" : canvas.name;
        string managerName = manager == null ? "なし" : manager.name;
        int active = manager == null ? -1 : manager.ActiveEffects.Count;
        Debug.Log($"[StatusEffectHUD2D 診断] HUD={name} / " +
            $"Active={gameObject.activeInHierarchy} / Canvas={canvasName} / " +
            $"Manager={managerName} / Manager側の有効数={active} / " +
            $"生成済みアイコン数={views.Count}", this);

        Transform parent = transform.parent;
        while (parent != null)
        {
            CanvasGroup group = parent.GetComponent<CanvasGroup>();
            if (group != null && group.alpha < 0.01f)
                Debug.LogWarning($"[StatusEffectHUD2D] 親オブジェクト {parent.name} のCanvasGroup.alpha=0です。画面に表示されません。", this);
            parent = parent.parent;
        }
    }

    private void OnValidate()
    {
        iconSize = Mathf.Max(24f, iconSize);
        iconSpacing = Mathf.Max(0f, iconSpacing);
        rightMargin = Mathf.Max(0f, rightMargin);
        topMargin = Mathf.Max(0f, topMargin);
        fadeSeconds = Mathf.Max(0.05f, fadeSeconds);
        shiftSmoothSeconds = Mathf.Max(0.05f, shiftSmoothSeconds);
        if (!Application.isPlaying) return;
        panel = GetComponent<RectTransform>();
        PositionPanel();
    }
}
