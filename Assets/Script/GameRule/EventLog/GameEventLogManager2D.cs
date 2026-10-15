using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 画面左上のイベント通知を一元管理します。
/// 新規通知は上から入り、表示中の通知は下へ押し出されます。
/// コントローラをシーンに一つだけ設置してください。
/// </summary>
public enum GameEventLogType
{
    Money,
    Treatment,
    WeaponBroken,
    SkillCard,
    Item,
    Rescue,
    Mission,
    Information
}

[DisallowMultipleComponent]
public sealed class GameEventLogManager2D : MonoBehaviour
{
    public static GameEventLogManager2D Instance { get; private set; }

    [Header("表示位置 (1920x1080基準)")]
    [SerializeField, Min(0f)] private float leftMargin = 24f;
    [SerializeField, Min(0f)] private float topMargin = 22f;
    [SerializeField, Min(200f)] private float entryWidth = 530f;
    [SerializeField, Min(35f)] private float entryHeight = 56f;
    [SerializeField, Min(0f)] private float entryGap = 9f;
    [SerializeField, Range(1, 12)] private int maxVisibleEntries = 5;
    [SerializeField] private int canvasSortingOrder = 500;

    [Header("アニメーション")]
    [Tooltip("新規ログが上から入る速さ／古いログが下へ移動する速さ")]
    [SerializeField, Min(0.02f)] private float slideSeconds = 0.35f;
    [Tooltip("表示開始からフェード開始までの秒数")]
    [SerializeField, Min(0.1f)] private float visibleSeconds = 5f;
    [SerializeField, Min(0.05f)] private float fadeOutSeconds = 0.75f;
    [SerializeField, Min(0.01f)] private float fadeInSeconds = 0.18f;

    [Header("共通デザイン")]
    [Tooltip("日本語対応TextMeshPro Font Asset。未指定ならTMPのデフォルトを使用")]
    [SerializeField] private TMP_FontAsset japaneseFont;
    [SerializeField, Range(12f, 40f)] private float fontSize = 22f;
    [SerializeField] private Color panelColor = new Color(0.10f, 0.12f, 0.15f, 0.89f);
    [SerializeField] private Color textColor = Color.white;
    [SerializeField] private Color moneyColor = new Color(1f, 0.78f, 0.34f);
    [SerializeField] private Color treatmentColor = new Color(0.40f, 0.92f, 0.61f);
    [SerializeField] private Color brokenColor = new Color(1f, 0.39f, 0.42f);
    [SerializeField] private Color skillColor = new Color(0.52f, 0.70f, 1f);
    [SerializeField] private Color otherColor = new Color(0.76f, 0.76f, 0.83f);

    private RectTransform entryRoot;
    private GameObject createdCanvas;
    private readonly List<GameEventLogEntryUI2D> entries = new List<GameEventLogEntryUI2D>();
    private static bool warnedMissingManager;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning("[EventLog] GameEventLogManager2Dが複数あります。1つだけ設置してください。", this);
            enabled = false;
            return;
        }

        Instance = this;
        warnedMissingManager = false;
        EnsureCanvas();
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        Instance = null;
        if (createdCanvas != null)
            Destroy(createdCanvas);
    }

    private void Update()
    {
        if (Instance != this)
            return;

        // 寿命を過ぎたログだけ削除。残ったログは自動で上へ詰まります。
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            GameEventLogEntryUI2D entry = entries[i];
            if (entry == null || entry.IsExpired)
            {
                if (entry != null)
                    Destroy(entry.gameObject);
                entries.RemoveAt(i);
            }
        }

        float delta = Time.unscaledDeltaTime;
        for (int i = 0; i < entries.Count; i++)
        {
            entries[i].SetTargetY(-i * (entryHeight + entryGap));
            entries[i].Animate(delta, slideSeconds, visibleSeconds,
                fadeInSeconds, fadeOutSeconds);
        }
    }

    private void EnsureCanvas()
    {
        if (entryRoot != null)
            return;

        // シーンのCanvasには触らず、透過・入力非ブロックの専用Canvasを自動生成。
        createdCanvas = new GameObject("[Runtime] GameEventLogCanvas", typeof(RectTransform));
        createdCanvas.transform.SetParent(transform, false);
        Canvas canvas = createdCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = canvasSortingOrder;

        CanvasScaler scaler = createdCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject rootObject = new GameObject("TopLeftLogStack", typeof(RectTransform));
        entryRoot = rootObject.GetComponent<RectTransform>();
        entryRoot.SetParent(createdCanvas.transform, false);
        entryRoot.anchorMin = new Vector2(0f, 1f);
        entryRoot.anchorMax = new Vector2(0f, 1f);
        entryRoot.pivot = new Vector2(0f, 1f);
        entryRoot.anchoredPosition = new Vector2(leftMargin, -topMargin);
        entryRoot.sizeDelta = new Vector2(entryWidth, 1f);
    }

    public void AddLog(GameEventLogType type, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return;

        EnsureCanvas();
        if (entryRoot == null)
            return;

        var obj = new GameObject("Log_" + type, typeof(RectTransform));
        obj.transform.SetParent(entryRoot, false);
        GameEventLogEntryUI2D entry = obj.AddComponent<GameEventLogEntryUI2D>();
        entry.Initialize(type, message, entryWidth, entryHeight, japaneseFont,
            fontSize, panelColor, textColor, GetAccent(type));
        entry.transform.SetAsFirstSibling();
        entries.Insert(0, entry);

        // ログが大量発生しても画面を覆わない。
        while (entries.Count > Mathf.Max(1, maxVisibleEntries))
        {
            int last = entries.Count - 1;
            if (entries[last] != null)
                Destroy(entries[last].gameObject);
            entries.RemoveAt(last);
        }

        for (int i = 0; i < entries.Count; i++)
            entries[i].SetTargetY(-i * (entryHeight + entryGap));
    }

    private Color GetAccent(GameEventLogType type)
    {
        switch (type)
        {
            case GameEventLogType.Money: return moneyColor;
            case GameEventLogType.Treatment: return treatmentColor;
            case GameEventLogType.WeaponBroken: return brokenColor;
            case GameEventLogType.SkillCard: return skillColor;
            default: return otherColor;
        }
    }

    public static void Show(GameEventLogType type, string message)
    {
        if (!Application.isPlaying)
            return;

        if (Instance == null)
            Instance = FindAnyObjectByType<GameEventLogManager2D>();

        if (Instance == null)
        {
            if (!warnedMissingManager)
            {
                warnedMissingManager = true;
                Debug.LogWarning("[EventLog] シーンに GameEventLogManager2D を1つ配置してください。ログを表示できません。");
            }
            return;
        }

        Instance.AddLog(type, message);
    }

    public static void MoneyGained(int gold)
    {
        if (gold > 0)
            Show(GameEventLogType.Money, $"{gold:N0}G入手");
    }

    public static void NpcTreated(string itemName)
    {
        Show(GameEventLogType.Treatment,
            string.IsNullOrWhiteSpace(itemName) ? "NPCを治療しました" : $"NPCを治療しました（{itemName}）");
    }

    public static void WeaponBroken(string name)
    {
        Show(GameEventLogType.WeaponBroken,
            string.IsNullOrWhiteSpace(name) ? "武器が壊れました" : $"{name}が壊れました");
    }

    public static void SkillCardAcquired(string name)
    {
        Show(GameEventLogType.SkillCard,
            string.IsNullOrWhiteSpace(name) ? "スキルカードを入手" : $"スキルカード入手：{name}");
    }

    // 今後のNPC救出・ミッション報酬・重要アイテムなども、このAPIから追加できます。
    public static void ImportantItemAcquired(string name) => Show(GameEventLogType.Item, $"{name}を入手");
    public static void NpcRescued() => Show(GameEventLogType.Rescue, "NPCを救出しました");
    public static void MissionCompleted(string name) => Show(GameEventLogType.Mission, $"ミッション達成：{name}");

    [ContextMenu("Play中テスト：800G入手")]
    private void TestMoney() => MoneyGained(800);

    [ContextMenu("Play中テスト：NPC治療")]
    private void TestTreatment() => NpcTreated("包帯");

    [ContextMenu("Play中テスト：武器破損")]
    private void TestBroken() => WeaponBroken("ナイフ");

    [ContextMenu("Play中テスト：スキルカード")]
    private void TestSkill() => SkillCardAcquired("クイックリロード");

    private void OnValidate()
    {
        entryWidth = Mathf.Max(200f, entryWidth);
        entryHeight = Mathf.Max(35f, entryHeight);
        entryGap = Mathf.Max(0f, entryGap);
        maxVisibleEntries = Mathf.Clamp(maxVisibleEntries, 1, 12);
    }
}
