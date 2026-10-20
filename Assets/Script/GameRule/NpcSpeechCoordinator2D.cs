using System;
using UnityEngine;
using TMPro;

/// <summary>
/// 洞窟NPCの短い会話を一括管理します。
/// RescuePersonAmbientSpeech2D / CaveOldManSpeechBubble2D が使用する共通の表示調停役。
/// どちらのセリフ／UI設定も維持し、優先度によって同時表示を防ぎます。
///
/// 洞窟シーンの Manager などに付けると Inspector から設定可能。
/// 付け忘れても最初の会話時に自動生成します。
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-250)]
public sealed class NpcSpeechCoordinator2D : MonoBehaviour
{
    [Header("NPCの吹き出し共通管理")]
    [Tooltip("ON：救出NPCと素材おじさんを合わせて同時に吹き出しは1個だけ。")]
    [SerializeField] private bool oneBubbleAtATime = true;

    [Tooltip("重要会話（優先度80以上）が同時に発生したら、新しい会話を優先します。")]
    [SerializeField] private bool latestImportantWinsWhenEqual = true;

    [Tooltip("Consoleで発話元と優先度の切り替わりを確認します。")]
    [SerializeField] private bool showDebugLogs = false;

    [Header("救出NPCと素材おじさんの共通デザイン")]
    [Tooltip("両方の吹き出しで同じ画像・文字・サイズ・アニメーションを使用します。")]
    [SerializeField] private NpcSpeechUnifiedBubbleVisual2D.Settings sharedAppearance =
        new NpcSpeechUnifiedBubbleVisual2D.Settings();

    // 会話側が見た目だけを参照する。セリフの優先度管理は従来どおり。
    public static NpcSpeechUnifiedBubbleVisual2D.Settings SharedAppearance
    {
        get { return GetOrCreate().sharedAppearance; }
    }

    private static NpcSpeechCoordinator2D instance;

    private MonoBehaviour activeSpeaker;
    private int activePriority;
    private float activeUntil;
    private Action activeInterrupt;

    public static string CurrentSpeakerName
    {
        get { return instance != null && instance.activeSpeaker != null
            ? instance.activeSpeaker.name : "なし"; }
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticReference()
    {
        instance = null;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Debug.LogWarning("[NpcSpeechCoordinator2D] 複数存在します。先に有効になったManagerを使用します。", this);
            enabled = false;
            return;
        }
        instance = this;
    }

    private void OnDestroy()
    {
        if (instance != this) return;
        ClearActive(true);
        instance = null;
    }

    private static NpcSpeechCoordinator2D GetOrCreate()
    {
        if (instance != null) return instance;

        // シーンに設置済みのManagerを優先（無ければ自動生成）。
        NpcSpeechCoordinator2D existing = FindAnyObjectByType<NpcSpeechCoordinator2D>();
        if (existing != null && existing.enabled)
        {
            instance = existing;
            return instance;
        }

        GameObject go = new GameObject("NPCSpeechCoordinator_Auto");
        instance = go.AddComponent<NpcSpeechCoordinator2D>();
        return instance;
    }

    /// <summary>
    /// 吹き出し表示を要求します。許可されたら呼び出し元が自分のUIを表示します。
    /// 優先度：素材おじさん交換100 / 救出NPC落下80 / 持ち上げ60 / 運搬35 / 待機10。
    /// </summary>
    public static bool TryShow(
        MonoBehaviour speaker, int priority, float duration, Action onInterrupted)
    {
        if (speaker == null || !speaker.isActiveAndEnabled) return false;
        if (DialoguePresenter2D.Active != null && DialoguePresenter2D.Active.IsPlaying) return false;
        return GetOrCreate().TryShowInternal(
            speaker, priority, duration, onInterrupted);
    }

    public static void Silence()
    {
        if (instance != null) instance.ClearActive(true);
    }

    /// <summary>自然終了やGameObject無効化時の解放。</summary>
    public static void Release(MonoBehaviour speaker)
    {
        if (speaker == null || instance == null) return;
        if (instance.activeSpeaker == speaker)
            instance.ClearActive(false);
    }

    private bool TryShowInternal(
        MonoBehaviour speaker, int priority, float duration, Action onInterrupted)
    {
        priority = Mathf.Clamp(priority, 0, 1000);
        duration = Mathf.Max(0.1f, duration);

        if (!oneBubbleAtATime)
            return true;

        if (activeSpeaker != null && activeSpeaker != speaker)
        {
            bool expired = Time.time >= activeUntil;
            bool moreImportant = priority > activePriority;
            bool latestImportant = latestImportantWinsWhenEqual &&
                priority >= 80 && priority == activePriority;

            if (!expired && !moreImportant && !latestImportant)
                return false;

            ClearActive(true);
        }
        else if (activeSpeaker == null)
        {
            // Unity側で既存の発話者がDestroyされていた場合の古い参照を破棄。
            ClearActive(false);
        }

        activeSpeaker = speaker;
        activePriority = priority;
        activeUntil = Time.time + duration;
        activeInterrupt = onInterrupted;

        if (showDebugLogs)
            Debug.Log($"[NPCSpeech][表示許可] {speaker.name} / Priority={priority} / {duration:0.0}秒", speaker);

        return true;
    }

    private void ClearActive(bool interrupt)
    {
        MonoBehaviour oldSpeaker = activeSpeaker;
        Action oldInterrupt = activeInterrupt;
        activeSpeaker = null;
        activeInterrupt = null;
        activePriority = 0;
        activeUntil = 0f;

        if (interrupt && oldSpeaker != null && oldInterrupt != null)
        {
            try { oldInterrupt(); }
            catch (Exception e) { Debug.LogException(e, oldSpeaker); }
        }
    }
}
