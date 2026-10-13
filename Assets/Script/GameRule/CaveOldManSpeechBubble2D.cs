using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 洞窟の素材おじさん専用の吹き出し。近づいた時のひと言と、
/// お酒受取／素材お返しのひと言を表示する。
/// 
/// UIを指定しなければワールド空間に自動生成する。
/// 日本語が□になる場合は Japanese Font Asset に日本語対応TMPフォントを設定する。
/// </summary>
[DisallowMultipleComponent]
public class CaveOldManSpeechBubble2D : MonoBehaviour
{
    [Header("吹き出しUI（両方未設定なら自動生成）")]
    [SerializeField] private GameObject bubbleRoot;
    [SerializeField] private TMP_Text bubbleText;
    [SerializeField] private TMP_FontAsset japaneseFontAsset;
    [SerializeField] private Vector3 worldOffset = new Vector3(0f, 2f, -0.2f);
    [SerializeField, Range(0.004f, 0.03f)] private float autoCanvasScale = 0.01f;
    [SerializeField] private int sortingOrder = 150;

    [Header("プレイヤーが近くにいる時")]
    [SerializeField, Min(0.1f)] private float greetingRadius = 3.2f;
    [Tooltip("空欄ならPlayerレイヤーで検知。Playerレイヤーが無い時はPlayerタグも探します")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private LayerMask playerLayer;
    [SerializeField, Min(1f)] private float idleLineChangeSeconds = 7f;

    [Header("普段のセリフ（ランダム）")]
    [SerializeField] private List<string> idleLines = new List<string>
    {
        "酒を持っておらんかのう……",
        "ここで飲む酒は格別じゃ",
        "一杯わけてくれんか？"
    };

    [Header("お酒を受け取った時のセリフ（ランダム）")]
    [SerializeField] private List<string> acceptLines = new List<string>
    {
        "おおっ！酒じゃないか！",
        "ありがたい！一杯いただこう！",
        "ほっほっ、いい酒じゃのう！"
    };

    [Header("素材を渡す時のセリフ（ランダム）")]
    [SerializeField] private List<string> rewardLines = new List<string>
    {
        "礼じゃ！これを持っていけ！",
        "こいつを持っていくといい！",
        "わしからの礼じゃ、受け取れ！"
    };

    [Header("表示時間")]
    [SerializeField, Min(0.2f)] private float acceptMessageSeconds = 2.2f;
    [SerializeField, Min(0.2f)] private float rewardMessageSeconds = 3f;

    [Header("ログ")]
    [SerializeField] private bool showDebugLogs = false;

    private enum DisplayState { Hidden, Idle, Temporary }
    private DisplayState state;
    private float temporaryUntil;
    private float nextIdleLineAt;
    private GameObject generatedRoot;
    private bool hasSetupUi;
    private int lastIdleIndex = -1;
    private int lastAcceptIndex = -1;
    private int lastRewardIndex = -1;
    private GameObject legacyBubbleRoot;
    private Canvas sharedCanvas;
    private RectTransform sharedCanvasRect;
    private CanvasGroup sharedCanvasGroup;
    private Image sharedBackground;
    private Coroutine bubbleVisualCoroutine;
    private float visualScale = 1f;

    private void Awake()
    {
        if (playerLayer.value == 0)
            playerLayer = LayerMask.GetMask("Player");

        EnsureBubble();
        Hide();
    }

    private void Update()
    {
        if (!hasSetupUi) return;

        if (state == DisplayState.Temporary)
        {
            if (Time.time < temporaryUntil) return;
            Hide();
            nextIdleLineAt = Time.time;
        }

        bool near = IsPlayerNear();
        if (!near)
        {
            Hide();
            return;
        }

        // 他のNPCの発話に遮られた時は、短い間隔で再表示を試みます。
        if (Time.time >= nextIdleLineAt)
            ShowIdle();
    }

    private void LateUpdate()
    {
        if (generatedRoot != null)
        {
            Vector3 offset = worldOffset;
            if (generatedRoot.activeSelf && NpcSpeechUnifiedBubbleVisual2D.FloatWhileVisible)
            {
                offset.y += Mathf.Sin(Time.time * NpcSpeechUnifiedBubbleVisual2D.FloatSpeed *
                    Mathf.PI * 2f) * NpcSpeechUnifiedBubbleVisual2D.FloatAmplitude;
            }
            generatedRoot.transform.position = transform.position + offset;
            generatedRoot.transform.rotation = Quaternion.identity;
        }
    }

    private void OnDisable()
    {
        Hide();
    }

    private void OnDestroy()
    {
        NpcSpeechCoordinator2D.Release(this);
        StopVisualAnimation();
        if (generatedRoot != null)
            Destroy(generatedRoot);
    }

    public void SayAccept(string offeredItemName = "")
    {
        string line = PickLine(acceptLines, ref lastAcceptIndex);
        ShowTemporary(line.Replace("{item}", offeredItemName ?? ""), acceptMessageSeconds);
    }

    public void SayReward(string rewardItemName = "")
    {
        string line = PickLine(rewardLines, ref lastRewardIndex);
        ShowTemporary(line.Replace("{item}", rewardItemName ?? ""), rewardMessageSeconds);
    }

    public void SayExchangeFailed()
    {
        ShowTemporary("すまん、うまく渡せんかった。酒は返すぞ", rewardMessageSeconds);
    }

    private void ShowIdle()
    {
        string line = PickLine(idleLines, ref lastIdleIndex);
        if (string.IsNullOrWhiteSpace(line))
        {
            Hide();
            return;
        }
        if (!SetBubble(line, true, 10, idleLineChangeSeconds))
        {
            state = DisplayState.Hidden;
            nextIdleLineAt = Time.time + 0.6f;
            return;
        }
        state = DisplayState.Idle;
        nextIdleLineAt = Time.time + idleLineChangeSeconds;
    }

    private void ShowTemporary(string line, float duration)
    {
        if (string.IsNullOrWhiteSpace(line)) return;
        EnsureBubble();
        if (!hasSetupUi) return;
        // お酒の受取り・お礼は通常のランダム会話より最優先。
        if (!SetBubble(line, true, 100, duration))
            return;
        state = DisplayState.Temporary;
        temporaryUntil = Time.time + Mathf.Max(0.2f, duration);
        if (showDebugLogs) Debug.Log("[OldManBubble] " + line, this);
    }

    private void Hide()
    {
        StopVisualAnimation();
        NpcSpeechCoordinator2D.Release(this);
        state = DisplayState.Hidden;
        if (hasSetupUi && bubbleRoot != null && bubbleRoot.activeSelf)
            bubbleRoot.SetActive(false);
    }

    private void HandleSpeechInterrupted()
    {
        StopVisualAnimation();
        state = DisplayState.Hidden;
        nextIdleLineAt = Time.time + 0.6f;
        if (hasSetupUi && bubbleRoot != null && bubbleRoot.activeSelf)
            bubbleRoot.SetActive(false);
    }

    private bool SetBubble(string message, bool visible, int priority, float duration)
    {
        if (bubbleRoot == null || bubbleText == null) return false;
        if (visible && !NpcSpeechCoordinator2D.TryShow(
            this, priority, duration + 0.05f, HandleSpeechInterrupted))
            return false;
        bubbleText.text = message;
        if (visible)
        {
            NpcSpeechUnifiedBubbleVisual2D.Apply(
                sharedCanvas, sharedCanvasRect, sharedBackground, bubbleText, japaneseFontAsset);
            if (!bubbleRoot.activeSelf) bubbleRoot.SetActive(true);
            StartVisualAnimation(duration);
        }
        else if (bubbleRoot.activeSelf)
        {
            Hide();
        }
        return true;
    }

    private bool IsPlayerNear()
    {
        if (playerTarget != null)
            return ((Vector2)(playerTarget.position - transform.position)).sqrMagnitude <= greetingRadius * greetingRadius;

        if (playerLayer.value != 0)
        {
            Collider2D hit = Physics2D.OverlapCircle((Vector2)transform.position, greetingRadius, playerLayer);
            if (hit != null) return true;
        }

        // Playerタグ未設定のプロジェクトでも例外で停止しない。
        try
        {
            GameObject taggedPlayer = GameObject.FindGameObjectWithTag("Player");
            if (taggedPlayer != null)
                return ((Vector2)(taggedPlayer.transform.position - transform.position)).sqrMagnitude <= greetingRadius * greetingRadius;
        }
        catch (UnityException) { }
        return false;
    }

    private static string PickLine(List<string> lines, ref int previousIndex)
    {
        if (lines == null || lines.Count == 0) return string.Empty;
        List<int> valid = new List<int>();
        for (int i = 0; i < lines.Count; i++)
            if (!string.IsNullOrWhiteSpace(lines[i])) valid.Add(i);
        if (valid.Count == 0) return string.Empty;
        int choice = valid[Random.Range(0, valid.Count)];
        if (valid.Count > 1 && choice == previousIndex)
        {
            int next = valid.IndexOf(choice) + 1;
            choice = valid[next % valid.Count];
        }
        previousIndex = choice;
        return lines[choice];
    }

    private void EnsureBubble()
    {
        if (hasSetupUi) return;

        // 以前の素材おじさん専用CanvasがInspectorに登録されていても使用しない。
        // 救出NPCと同じ背景・枠・尻尾・テキストを共通描画するため。
        if (bubbleRoot != null)
        {
            legacyBubbleRoot = bubbleRoot;
            // 誤ってNPC本体をBubble Rootに指定していても停止させない。
            if (legacyBubbleRoot != gameObject &&
                !transform.IsChildOf(legacyBubbleRoot.transform))
            {
                legacyBubbleRoot.SetActive(false);
            }
            else
            {
                Debug.LogWarning(
                    "[OldManBubble] 旧Bubble RootにNPC本体または親が指定されています。" +
                    " 本体は無効化せず共通UIを作ります。", this);
            }
        }

        GameObject root = new GameObject(
            "OldManSpeechBubble_Unified", typeof(RectTransform), typeof(Canvas),
            typeof(CanvasScaler), typeof(CanvasGroup));
        generatedRoot = root;
        sharedCanvas = root.GetComponent<Canvas>();
        sharedCanvasRect = root.GetComponent<RectTransform>();
        sharedCanvasGroup = root.GetComponent<CanvasGroup>();
        sharedCanvasGroup.interactable = false;
        sharedCanvasGroup.blocksRaycasts = false;
        sharedCanvasGroup.alpha = 0f;
        root.transform.position = transform.position + worldOffset;

        GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(root.transform, false);
        sharedBackground = bg.GetComponent<Image>();
        sharedBackground.raycastTarget = false;

        GameObject textObj = new GameObject(
            "Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObj.transform.SetParent(bg.transform, false);
        TextMeshProUGUI tmp = textObj.GetComponent<TextMeshProUGUI>();
        tmp.overflowMode = TextOverflowModes.Overflow;
        tmp.raycastTarget = false;
        tmp.text = string.Empty;

        bubbleRoot = root;
        bubbleText = tmp;
        hasSetupUi = true;
        NpcSpeechUnifiedBubbleVisual2D.Apply(
            sharedCanvas, sharedCanvasRect, sharedBackground, bubbleText, japaneseFontAsset);
        ApplyVisualScale();
        root.SetActive(false);
    }

    private void ApplyVisualScale()
    {
        if (sharedCanvasRect == null) return;
        sharedCanvasRect.localScale = Vector3.one *
            NpcSpeechUnifiedBubbleVisual2D.WorldScale *
            Mathf.Max(0.01f, visualScale);
    }

    private void StartVisualAnimation(float duration)
    {
        StopVisualAnimation();
        bubbleVisualCoroutine = StartCoroutine(BubbleVisualRoutine(Mathf.Max(0.2f, duration)));
    }

    private void StopVisualAnimation()
    {
        if (bubbleVisualCoroutine != null)
        {
            StopCoroutine(bubbleVisualCoroutine);
            bubbleVisualCoroutine = null;
        }
        if (sharedCanvasGroup != null) sharedCanvasGroup.alpha = 0f;
        visualScale = 1f;
    }

    // 救出NPCと同じポンッと出るフェード＆ゆっくり浮遊する共通アニメーション。
    private IEnumerator BubbleVisualRoutine(float duration)
    {
        float appear = Mathf.Min(NpcSpeechUnifiedBubbleVisual2D.AppearSeconds,
            Mathf.Max(0.02f, duration * 0.3f));
        float disappear = Mathf.Min(NpcSpeechUnifiedBubbleVisual2D.DisappearSeconds,
            Mathf.Max(0.02f, duration * 0.3f));
        float overshoot = NpcSpeechUnifiedBubbleVisual2D.AppearOvershoot;
        float start = NpcSpeechUnifiedBubbleVisual2D.AppearStartScale;
        float elapsed = 0f;
        if (sharedCanvasGroup != null) sharedCanvasGroup.alpha = 0f;
        visualScale = start;
        ApplyVisualScale();

        while (elapsed < appear)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / appear));
            // 少しオーバーしてから通常の大きさへ戻る。
            visualScale = t < 0.65f
                ? Mathf.Lerp(start, overshoot, t / 0.65f)
                : Mathf.Lerp(overshoot, 1f, (t - 0.65f) / 0.35f);
            if (sharedCanvasGroup != null) sharedCanvasGroup.alpha = t;
            ApplyVisualScale();
            yield return null;
        }

        visualScale = 1f;
        if (sharedCanvasGroup != null) sharedCanvasGroup.alpha = 1f;
        ApplyVisualScale();
        float hold = Mathf.Max(0f, duration - appear - disappear);
        if (hold > 0f) yield return new WaitForSeconds(hold);

        elapsed = 0f;
        while (elapsed < disappear)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / disappear));
            if (sharedCanvasGroup != null) sharedCanvasGroup.alpha = 1f - t;
            visualScale = Mathf.Lerp(1f, 0.9f, t);
            ApplyVisualScale();
            yield return null;
        }
        bubbleVisualCoroutine = null;
        if (sharedCanvasGroup != null) sharedCanvasGroup.alpha = 0f;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.7f, 0.9f, 0.4f, 0.55f);
        Gizmos.DrawWireSphere(transform.position, greetingRadius);
    }
}
