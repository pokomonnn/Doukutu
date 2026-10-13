using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 救出NPCの通常会話とは別に、洞窟内で短いセリフをランダム表示します。
///
/// ・運搬開始時に1回しゃべる
/// ・運搬中は一定間隔でランダムにしゃべる
/// ・高所から落下し始めた時に1回しゃべる
/// ・待機中のランダム会話は任意（初期値OFF）
/// ・死亡中はしゃべらない
/// ・エレベーター等の外部システムがNPCを確保中はしゃべらない
///
/// 吹き出しUIはWorld Space Canvasとして自動生成するため、
/// NPCのTransform反転の影響を受けにくい構成です。
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonAmbientSpeech2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("救出NPCのCarryableObject2D。未設定なら同じObject/親子から自動取得します。")]
    [InspectorName("運搬対象")]
    [SerializeField] private CarryableObject2D carryableObject;

    [Tooltip("NPCのCharacterHealth。未設定なら同じObject/親子から自動取得します。")]
    [InspectorName("体力")]
    [SerializeField] private CharacterHealth characterHealth;

    [Tooltip("NPCのRigidbody2D。未設定なら同じObject/親子から自動取得します。")]
    [InspectorName("Rigidbody2D")]
    [SerializeField] private Rigidbody2D targetRigidbody;

    [Tooltip("吹き出しを表示する基準位置です。未設定ならNPCのTransformを使います。")]
    [InspectorName("吹き出し基準位置（任意）")]
    [SerializeField] private Transform speechAnchor;

    [Header("運搬中のセリフ")]
    [Tooltip("NPCを持ち上げた直後にも、ここから1つランダム表示します。")]
    [InspectorName("持ち上げ直後にしゃべる")]
    [SerializeField] private bool speakImmediatelyWhenPickedUp = true;

    [InspectorName("運搬中セリフ")]
    [SerializeField] private string[] carriedLines =
    {
        "ありがとう",
        "そっと運んでくれ～",
        "早く助けてくれよ"
    };

    [Tooltip("運搬中に次のセリフを話すまでの最短秒数です。")]
    [InspectorName("運搬中 最短間隔")]
    [SerializeField, Min(0.1f)] private float carriedSpeechMinInterval = 4f;

    [Tooltip("運搬中に次のセリフを話すまでの最長秒数です。")]
    [InspectorName("運搬中 最長間隔")]
    [SerializeField, Min(0.1f)] private float carriedSpeechMaxInterval = 8f;

    [Header("落下中のセリフ")]
    [InspectorName("落下中セリフ")]
    [SerializeField] private string[] fallingLines =
    {
        "うわ～！",
        "落ちるー！",
        "助けてー！"
    };

    [Tooltip("これより速く下方向へ移動した時に落下中と判定します。")]
    [InspectorName("落下判定速度")]
    [SerializeField, Min(0.1f)] private float fallingVelocityThreshold = 1.5f;

    [Tooltip("短時間に落下判定を繰り返して連続発言しないための待ち時間です。")]
    [InspectorName("落下セリフ再発言待ち時間")]
    [SerializeField, Min(0f)] private float fallingSpeechCooldown = 2f;

    [Header("待機中のランダム会話（任意）")]
    [Tooltip("ONにすると、地面で待っている間にもランダム会話します。")]
    [InspectorName("待機中もしゃべる")]
    [SerializeField] private bool enableIdleSpeech;

    [InspectorName("待機中セリフ")]
    [SerializeField] private string[] idleLines;

    [InspectorName("待機中 最短間隔")]
    [SerializeField, Min(0.1f)] private float idleSpeechMinInterval = 9f;

    [InspectorName("待機中 最長間隔")]
    [SerializeField, Min(0.1f)] private float idleSpeechMaxInterval = 16f;

    [Header("吹き出し表示")]
    [Tooltip("InventoryやNPC詳細表示と同じ日本語対応TMP Font Assetを指定してください。")]
    [InspectorName("日本語フォント")]
    [SerializeField] private TMP_FontAsset japaneseFontAsset;

    [Tooltip("NPCから見た吹き出しの位置です。Speech Anchorを設定した場合も、この値を追加します。")]
    [InspectorName("吹き出し位置")]
    [SerializeField] private Vector3 bubbleWorldOffset = new Vector3(0f, 1.45f, 0f);

    [InspectorName("吹き出し表示時間")]
    [SerializeField, Min(0.1f)] private float bubbleDuration = 2.2f;

    [InspectorName("吹き出し幅")]
    [SerializeField, Min(80f)] private float bubbleWidth = 280f;

    [InspectorName("吹き出し高さ")]
    [SerializeField, Min(40f)] private float bubbleHeight = 82f;

    [InspectorName("文字サイズ")]
    [SerializeField, Min(8f)] private float fontSize = 24f;

    [Tooltip("World Space Canvasの縮尺です。2Dゲームでは0.005～0.015程度が目安です。")]
    [InspectorName("吹き出し縮尺")]
    [SerializeField, Min(0.0001f)] private float bubbleWorldScale = 0.008f;

    [InspectorName("背景色")]
    [SerializeField] private Color backgroundColor =
        new Color(1f, 1f, 1f, 0.92f);

    [InspectorName("文字色")]
    [SerializeField] private Color textColor =
        new Color(0.08f, 0.08f, 0.08f, 1f);

    [Tooltip("任意の吹き出しSpriteを指定できます。9-slice用Spriteなら枠をきれいに伸ばせます。")]
    [InspectorName("吹き出し背景Sprite（任意）")]
    [SerializeField] private Sprite bubbleBackgroundSprite;

    [Tooltip("他のWorld Space Canvasとの表示順です。")]
    [InspectorName("吹き出しSorting Order")]
    [SerializeField] private int bubbleSortingOrder = 50;

    [Header("吹き出しアニメーション")]
    [Tooltip("ONなら表示開始時にポンッと拡大しながらフェードインします。")]
    [InspectorName("出現アニメーション")]
    [SerializeField] private bool enableAppearAnimation = true;

    [Tooltip("吹き出しが出現するまでの秒数です。")]
    [InspectorName("出現時間")]
    [SerializeField, Min(0.01f)] private float appearDuration = 0.16f;

    [Tooltip("出現開始時の大きさです。1 = 通常サイズ。")]
    [InspectorName("出現開始サイズ")]
    [SerializeField, Range(0.1f, 1.5f)] private float appearStartScale = 0.8f;

    [Tooltip("通常会話の出現途中で一度だけ少し大きくする倍率です。")]
    [InspectorName("出現時の最大サイズ")]
    [SerializeField, Range(1f, 1.5f)] private float appearOvershootScale = 1.08f;

    [Tooltip("ONなら表示中に吹き出しが少し上下します。")]
    [InspectorName("表示中のふわふわ")]
    [SerializeField] private bool enableFloating = true;

    [Tooltip("ふわふわする上下幅です。World座標単位です。")]
    [InspectorName("ふわふわ幅")]
    [SerializeField, Min(0f)] private float floatingAmplitude = 0.035f;

    [Tooltip("ふわふわする速さです。")]
    [InspectorName("ふわふわ速度")]
    [SerializeField, Min(0f)] private float floatingSpeed = 2.4f;

    [Tooltip("ONなら落下中のセリフだけ左右にブルブル揺れます。")]
    [InspectorName("落下セリフのShake")]
    [SerializeField] private bool enableFallingShake = true;

    [Tooltip("落下セリフの左右揺れ幅です。World座標単位です。")]
    [InspectorName("Shake強さ")]
    [SerializeField, Min(0f)] private float fallingShakeStrength = 0.055f;

    [Tooltip("落下セリフを揺らす時間です。")]
    [InspectorName("Shake時間")]
    [SerializeField, Min(0f)] private float fallingShakeDuration = 0.45f;

    [Tooltip("落下セリフの表示サイズです。1 = 通常。")]
    [InspectorName("落下セリフ基本サイズ")]
    [SerializeField, Range(1f, 1.5f)] private float fallingBaseScale = 1.10f;

    [Tooltip("落下セリフが出た瞬間の最大サイズです。")]
    [InspectorName("落下セリフ最大サイズ")]
    [SerializeField, Range(1f, 1.6f)] private float fallingOvershootScale = 1.18f;

    [Tooltip("ONなら消える時にフェードアウトしながら少し縮みます。")]
    [InspectorName("消えるアニメーション")]
    [SerializeField] private bool enableDisappearAnimation = true;

    [Tooltip("消えるアニメーションの秒数です。")]
    [InspectorName("消える時間")]
    [SerializeField, Min(0.01f)] private float disappearDuration = 0.18f;

    [Tooltip("消える直前に縮む大きさです。1 = 通常サイズ。")]
    [InspectorName("消える時のサイズ")]
    [SerializeField, Range(0.1f, 1f)] private float disappearEndScale = 0.9f;

    [Header("ランダム設定")]
    [Tooltip("ONなら直前と同じセリフをできるだけ避けます。")]
    [InspectorName("同じセリフの連続を避ける")]
    [SerializeField] private bool avoidImmediateRepeat = true;

    [Header("デバッグ")]
    [InspectorName("会話ログを表示")]
    [SerializeField] private bool showDebugLogs;

    private Canvas bubbleCanvas;
    private RectTransform bubbleCanvasRect;
    private CanvasGroup bubbleCanvasGroup;
    private Image bubbleBackground;
    private TextMeshProUGUI bubbleText;

    private Coroutine bubbleAnimationCoroutine;

    private bool wasCarried;
    private bool wasFalling;

    private float nextCarriedSpeechTime;
    private float nextIdleSpeechTime;
    private float nextFallingSpeechAllowedTime;

    private int lastCarriedLineIndex = -1;
    private int lastFallingLineIndex = -1;
    private int lastIdleLineIndex = -1;

    private bool currentBubbleIsFalling;
    private float bubbleShownAtTime;
    private float currentScaleMultiplier = 1f;

    private bool IsDead =>
        characterHealth != null &&
        characterHealth.IsDead;

    private bool IsCarried =>
        carryableObject != null &&
        carryableObject.IsCarried;

    private bool IsExternallyReserved =>
        carryableObject != null &&
        carryableObject.IsExternallyReserved;

    private void Awake()
    {
        FindReferences();
        ScheduleNextCarriedSpeech();
        ScheduleNextIdleSpeech();
    }

    private void OnEnable()
    {
        FindReferences();

        wasCarried = IsCarried;
        wasFalling = false;

        ScheduleNextCarriedSpeech();
        ScheduleNextIdleSpeech();
    }

    private void Update()
    {
        FindReferences();

        if (IsDead)
        {
            HideBubble();
            wasCarried = false;
            wasFalling = false;
            return;
        }

        bool carried = IsCarried;

        if (carried)
        {
            HandleCarriedSpeech();

            wasCarried = true;
            wasFalling = false;
            return;
        }

        if (wasCarried)
        {
            // 地面へ下ろした直後に、待機会話が即発生しないよう再抽選します。
            ScheduleNextIdleSpeech();
        }

        wasCarried = false;

        if (IsExternallyReserved)
        {
            wasFalling = false;
            return;
        }

        bool falling = IsCurrentlyFalling();
        HandleFallingSpeech(falling);

        if (!falling)
        {
            HandleIdleSpeech();
        }

        wasFalling = falling;
    }

    private void LateUpdate()
    {
        UpdateBubbleWorldPosition();
    }

    private void OnDisable()
    {
        NpcSpeechCoordinator2D.Release(this);
        StopBubbleAnimationCoroutine();

        if (bubbleCanvas != null)
        {
            Destroy(bubbleCanvas.gameObject);
        }

        bubbleCanvas = null;
        bubbleCanvasRect = null;
        bubbleCanvasGroup = null;
        bubbleBackground = null;
        bubbleText = null;
    }

    private void HandleCarriedSpeech()
    {
        if (!wasCarried)
        {
            if (speakImmediatelyWhenPickedUp)
            {
                SpeakRandom(
                    carriedLines,
                    ref lastCarriedLineIndex,
                    "持ち上げ"
                );
            }

            ScheduleNextCarriedSpeech();
            return;
        }

        if (Time.time < nextCarriedSpeechTime)
        {
            return;
        }

        SpeakRandom(
            carriedLines,
            ref lastCarriedLineIndex,
            "運搬中"
        );

        ScheduleNextCarriedSpeech();
    }

    private void HandleFallingSpeech(bool falling)
    {
        if (!falling)
        {
            return;
        }

        if (wasFalling ||
            Time.time < nextFallingSpeechAllowedTime)
        {
            return;
        }

        SpeakRandom(
            fallingLines,
            ref lastFallingLineIndex,
            "落下",
            true
        );

        nextFallingSpeechAllowedTime =
            Time.time + fallingSpeechCooldown;
    }

    private void HandleIdleSpeech()
    {
        if (!enableIdleSpeech ||
            Time.time < nextIdleSpeechTime)
        {
            return;
        }

        SpeakRandom(
            idleLines,
            ref lastIdleLineIndex,
            "待機"
        );

        ScheduleNextIdleSpeech();
    }

    private bool IsCurrentlyFalling()
    {
        if (targetRigidbody == null ||
            targetRigidbody.bodyType != RigidbodyType2D.Dynamic ||
            !targetRigidbody.simulated)
        {
            return false;
        }

        return targetRigidbody.linearVelocity.y <=
            -Mathf.Abs(fallingVelocityThreshold);
    }

    private void SpeakRandom(
        string[] lines,
        ref int lastIndex,
        string reason,
        bool fallingStyle = false)
    {
        if (lines == null || lines.Length == 0)
        {
            return;
        }

        int validCount = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                validCount++;
            }
        }

        if (validCount == 0)
        {
            return;
        }

        int selectedIndex = PickRandomValidLineIndex(
            lines,
            lastIndex
        );

        if (selectedIndex < 0)
        {
            return;
        }

        // 複数のNPCが同時にしゃべらないよう共通Managerへ表示を申請。
        // 落下 > 持ち上げ > 運搬中 > 待機中 の優先度。
        int speechPriority = fallingStyle ? 80 :
            reason == "持ち上げ" ? 60 :
            reason == "運搬中" ? 35 : 10;

        if (!ShowBubble(lines[selectedIndex].Trim(), fallingStyle, speechPriority))
            return;

        lastIndex = selectedIndex;

        if (showDebugLogs)
        {
            Debug.Log(
                $"[RescuePersonAmbientSpeech2D] {name}: " +
                $"{reason}『{lines[selectedIndex].Trim()}』",
                this
            );
        }
    }

    private int PickRandomValidLineIndex(
        string[] lines,
        int lastIndex)
    {
        if (lines == null || lines.Length == 0)
        {
            return -1;
        }

        int[] validIndices = new int[lines.Length];
        int count = 0;

        for (int i = 0; i < lines.Length; i++)
        {
            if (!string.IsNullOrWhiteSpace(lines[i]))
            {
                validIndices[count++] = i;
            }
        }

        if (count <= 0)
        {
            return -1;
        }

        if (count == 1)
        {
            return validIndices[0];
        }

        int selected = validIndices[
            Random.Range(0, count)
        ];

        if (avoidImmediateRepeat &&
            selected == lastIndex)
        {
            int guard = 0;

            while (selected == lastIndex &&
                   guard < 12)
            {
                selected = validIndices[
                    Random.Range(0, count)
                ];

                guard++;
            }

            // 乱数の偏りで同じままなら、確実に次の候補へ進めます。
            if (selected == lastIndex)
            {
                for (int i = 0; i < count; i++)
                {
                    if (validIndices[i] != lastIndex)
                    {
                        selected = validIndices[i];
                        break;
                    }
                }
            }
        }

        return selected;
    }

    private bool ShowBubble(
        string message,
        bool fallingStyle = false,
        int speechPriority = 60)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        EnsureBubbleVisuals();

        if (bubbleCanvas == null ||
            bubbleCanvasGroup == null ||
            bubbleText == null)
        {
            return false;
        }

        if (!NpcSpeechCoordinator2D.TryShow(
            this, speechPriority, bubbleDuration + 0.05f, HandleSpeechInterrupted))
            return false;

        bubbleText.text = message;
        currentBubbleIsFalling = fallingStyle;
        bubbleShownAtTime = Time.time;
        currentScaleMultiplier = 1f;

        ApplyVisualSettings();
        UpdateBubbleWorldPosition();

        bubbleCanvas.gameObject.SetActive(true);

        StopBubbleAnimationCoroutine();

        bubbleAnimationCoroutine = StartCoroutine(
            BubbleAnimationRoutine(
                fallingStyle
            )
        );
        return true;
    }

    // 優先度の高い別NPCの発言が始まった時だけ呼ばれます。
    private void HandleSpeechInterrupted()
    {
        StopBubbleAnimationCoroutine();
        HideBubbleImmediate();
    }

    private IEnumerator BubbleAnimationRoutine(
        bool fallingStyle)
    {
        float targetScale =
            fallingStyle
                ? Mathf.Max(1f, fallingBaseScale)
                : 1f;

        float overshootScale =
            fallingStyle
                ? Mathf.Max(
                    targetScale,
                    fallingOvershootScale
                )
                : Mathf.Max(
                    1f,
                    NpcSpeechUnifiedBubbleVisual2D.AppearOvershoot
                );

        if (enableAppearAnimation)
        {
            float duration =
                NpcSpeechUnifiedBubbleVisual2D.AppearSeconds;

            float firstPhase =
                duration * 0.62f;

            float secondPhase =
                Mathf.Max(
                    0.01f,
                    duration - firstPhase
                );

            float startScale =
                Mathf.Max(
                    0.1f,
                    NpcSpeechUnifiedBubbleVisual2D.AppearStartScale
                );

            float elapsed = 0f;

            while (elapsed < firstPhase)
            {
                elapsed += Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        elapsed /
                        Mathf.Max(
                            0.01f,
                            firstPhase
                        )
                    );

                float eased =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t
                    );

                currentScaleMultiplier =
                    Mathf.Lerp(
                        startScale,
                        overshootScale,
                        eased
                    );

                if (bubbleCanvasGroup != null)
                {
                    bubbleCanvasGroup.alpha =
                        Mathf.Lerp(
                            0f,
                            1f,
                            eased
                        );
                }

                ApplyAnimatedScale();
                yield return null;
            }

            elapsed = 0f;

            while (elapsed < secondPhase)
            {
                elapsed += Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        elapsed /
                        secondPhase
                    );

                float eased =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t
                    );

                currentScaleMultiplier =
                    Mathf.Lerp(
                        overshootScale,
                        targetScale,
                        eased
                    );

                if (bubbleCanvasGroup != null)
                {
                    bubbleCanvasGroup.alpha = 1f;
                }

                ApplyAnimatedScale();
                yield return null;
            }
        }
        else
        {
            currentScaleMultiplier =
                targetScale;

            if (bubbleCanvasGroup != null)
            {
                bubbleCanvasGroup.alpha = 1f;
            }

            ApplyAnimatedScale();
        }

        float fadeDuration =
            enableDisappearAnimation
                ? Mathf.Max(
                    0.01f,
                    NpcSpeechUnifiedBubbleVisual2D.DisappearSeconds
                )
                : 0f;

        float holdDuration =
            Mathf.Max(
                0f,
                bubbleDuration -
                (enableAppearAnimation
                    ? NpcSpeechUnifiedBubbleVisual2D.AppearSeconds
                    : 0f) -
                fadeDuration
            );

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(
                holdDuration
            );
        }

        if (enableDisappearAnimation)
        {
            float startScale =
                currentScaleMultiplier;

            float endScale =
                targetScale *
                Mathf.Clamp(
                    disappearEndScale,
                    0.1f,
                    1f
                );

            float elapsed = 0f;

            while (elapsed < fadeDuration)
            {
                elapsed += Time.deltaTime;

                float t =
                    Mathf.Clamp01(
                        elapsed /
                        fadeDuration
                    );

                float eased =
                    Mathf.SmoothStep(
                        0f,
                        1f,
                        t
                    );

                currentScaleMultiplier =
                    Mathf.Lerp(
                        startScale,
                        endScale,
                        eased
                    );

                if (bubbleCanvasGroup != null)
                {
                    bubbleCanvasGroup.alpha =
                        Mathf.Lerp(
                            1f,
                            0f,
                            eased
                        );
                }

                ApplyAnimatedScale();
                yield return null;
            }
        }

        bubbleAnimationCoroutine = null;
        HideBubbleImmediate();
    }

    private void HideBubble()
    {
        StopBubbleAnimationCoroutine();
        HideBubbleImmediate();
    }

    private void HideBubbleImmediate()
    {
        NpcSpeechCoordinator2D.Release(this);
        currentScaleMultiplier = 1f;
        currentBubbleIsFalling = false;

        if (bubbleCanvasGroup != null)
        {
            bubbleCanvasGroup.alpha = 0f;
        }

        if (bubbleCanvas != null)
        {
            bubbleCanvas.gameObject.SetActive(false);
        }
    }

    private void StopBubbleAnimationCoroutine()
    {
        if (bubbleAnimationCoroutine == null)
        {
            return;
        }

        StopCoroutine(
            bubbleAnimationCoroutine
        );

        bubbleAnimationCoroutine = null;
    }

    private void EnsureBubbleVisuals()
    {
        if (bubbleCanvas != null)
        {
            ApplyVisualSettings();
            return;
        }

        GameObject canvasObject = new GameObject(
            $"{name}_AmbientSpeechBubble",
            typeof(RectTransform),
            typeof(Canvas),
            typeof(CanvasScaler),
            typeof(CanvasGroup)
        );

        bubbleCanvas =
            canvasObject.GetComponent<Canvas>();

        bubbleCanvasRect =
            canvasObject.GetComponent<RectTransform>();

        bubbleCanvasGroup =
            canvasObject.GetComponent<CanvasGroup>();

        bubbleCanvas.renderMode =
            RenderMode.WorldSpace;

        bubbleCanvas.overrideSorting = true;
        bubbleCanvas.sortingOrder =
            bubbleSortingOrder;

        bubbleCanvasGroup.interactable = false;
        bubbleCanvasGroup.blocksRaycasts = false;
        bubbleCanvasGroup.alpha = 0f;

        GameObject backgroundObject = new GameObject(
            "Background",
            typeof(RectTransform),
            typeof(Image)
        );

        backgroundObject.transform.SetParent(
            canvasObject.transform,
            false
        );

        bubbleBackground =
            backgroundObject.GetComponent<Image>();

        RectTransform backgroundRect =
            backgroundObject.GetComponent<RectTransform>();

        backgroundRect.anchorMin = Vector2.zero;
        backgroundRect.anchorMax = Vector2.one;
        backgroundRect.offsetMin = Vector2.zero;
        backgroundRect.offsetMax = Vector2.zero;

        bubbleBackground.raycastTarget = false;

        GameObject textObject = new GameObject(
            "Text",
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );

        textObject.transform.SetParent(
            backgroundObject.transform,
            false
        );

        bubbleText =
            textObject.GetComponent<TextMeshProUGUI>();

        RectTransform textRect =
            textObject.GetComponent<RectTransform>();

        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin =
            new Vector2(14f, 8f);
        textRect.offsetMax =
            new Vector2(-14f, -8f);

        bubbleText.alignment =
            TextAlignmentOptions.Center;

        bubbleText.enableWordWrapping = true;
        bubbleText.overflowMode =
            TextOverflowModes.Overflow;

        bubbleText.raycastTarget = false;

        ApplyVisualSettings();
        UpdateBubbleWorldPosition();

        canvasObject.SetActive(false);
    }

    private void ApplyVisualSettings()
    {
        // 新しい共通Managerから背景画像・枠・日本語フォント・大きさを受け取る。
        // 旧Inspectorの個別見た目設定は削除せず残し、将来の移行を安全にする。
        NpcSpeechUnifiedBubbleVisual2D.Apply(
            bubbleCanvas, bubbleCanvasRect, bubbleBackground, bubbleText, japaneseFontAsset);
        ApplyAnimatedScale();
    }

    private void ApplyAnimatedScale()
    {
        if (bubbleCanvasRect == null) return;
        bubbleCanvasRect.localScale = Vector3.one *
            NpcSpeechUnifiedBubbleVisual2D.WorldScale *
            Mathf.Max(0.01f, currentScaleMultiplier);
    }

    private void UpdateBubbleWorldPosition()
    {
        if (bubbleCanvasRect == null)
        {
            return;
        }

        Transform anchor =
            speechAnchor != null
                ? speechAnchor
                : transform;

        Vector3 animationOffset =
            Vector3.zero;

        if (bubbleCanvas != null &&
            bubbleCanvas.gameObject.activeSelf)
        {
            if (NpcSpeechUnifiedBubbleVisual2D.FloatWhileVisible &&
                NpcSpeechUnifiedBubbleVisual2D.FloatAmplitude > 0f &&
                NpcSpeechUnifiedBubbleVisual2D.FloatSpeed > 0f)
            {
                animationOffset.y +=
                    Mathf.Sin(
                        Time.time *
                        NpcSpeechUnifiedBubbleVisual2D.FloatSpeed *
                        Mathf.PI *
                        2f
                    ) *
                    NpcSpeechUnifiedBubbleVisual2D.FloatAmplitude;
            }

            if (currentBubbleIsFalling &&
                enableFallingShake &&
                fallingShakeStrength > 0f &&
                Time.time - bubbleShownAtTime <=
                    fallingShakeDuration)
            {
                float elapsed =
                    Time.time -
                    bubbleShownAtTime;

                float normalized =
                    fallingShakeDuration > 0f
                        ? Mathf.Clamp01(
                            elapsed /
                            fallingShakeDuration
                        )
                        : 1f;

                float strength =
                    fallingShakeStrength *
                    (1f - normalized);

                animationOffset.x +=
                    Mathf.Sin(
                        elapsed *
                        72f
                    ) *
                    strength;

                animationOffset.y +=
                    Mathf.Sin(
                        elapsed *
                        53f
                    ) *
                    strength *
                    0.35f;
            }
        }

        bubbleCanvasRect.position =
            anchor.position +
            bubbleWorldOffset +
            animationOffset;

        // NPCの左右反転や回転に巻き込まれないよう、
        // 吹き出し自体は常に正位置を保ちます。
        bubbleCanvasRect.rotation =
            Quaternion.identity;
    }

    private void ScheduleNextCarriedSpeech()
    {
        nextCarriedSpeechTime =
            Time.time +
            Random.Range(
                Mathf.Min(
                    carriedSpeechMinInterval,
                    carriedSpeechMaxInterval
                ),
                Mathf.Max(
                    carriedSpeechMinInterval,
                    carriedSpeechMaxInterval
                )
            );
    }

    private void ScheduleNextIdleSpeech()
    {
        nextIdleSpeechTime =
            Time.time +
            Random.Range(
                Mathf.Min(
                    idleSpeechMinInterval,
                    idleSpeechMaxInterval
                ),
                Mathf.Max(
                    idleSpeechMinInterval,
                    idleSpeechMaxInterval
                )
            );
    }

    private void FindReferences()
    {
        if (carryableObject == null)
        {
            carryableObject =
                GetComponent<CarryableObject2D>();

            if (carryableObject == null)
            {
                carryableObject =
                    GetComponentInParent<
                        CarryableObject2D
                    >();
            }

            if (carryableObject == null)
            {
                carryableObject =
                    GetComponentInChildren<
                        CarryableObject2D
                    >(true);
            }
        }

        if (characterHealth == null)
        {
            characterHealth =
                GetComponent<CharacterHealth>();

            if (characterHealth == null)
            {
                characterHealth =
                    GetComponentInParent<
                        CharacterHealth
                    >();
            }

            if (characterHealth == null)
            {
                characterHealth =
                    GetComponentInChildren<
                        CharacterHealth
                    >(true);
            }
        }

        if (targetRigidbody == null)
        {
            targetRigidbody =
                carryableObject != null
                    ? carryableObject.TargetRigidbody
                    : null;
        }

        if (targetRigidbody == null)
        {
            targetRigidbody =
                GetComponent<Rigidbody2D>();

            if (targetRigidbody == null)
            {
                targetRigidbody =
                    GetComponentInParent<
                        Rigidbody2D
                    >();
            }

            if (targetRigidbody == null)
            {
                targetRigidbody =
                    GetComponentInChildren<
                        Rigidbody2D
                    >(true);
            }
        }
    }

    [ContextMenu("吹き出しテスト")]
    private void TestSpeechBubble()
    {
        ShowBubble(
            "ありがとう！",
            false
        );
    }

    [ContextMenu("落下吹き出しテスト")]
    private void TestFallingSpeechBubble()
    {
        ShowBubble(
            "うわ～！",
            true
        );
    }

    private void OnValidate()
    {
        carriedSpeechMinInterval =
            Mathf.Max(
                0.1f,
                carriedSpeechMinInterval
            );

        carriedSpeechMaxInterval =
            Mathf.Max(
                carriedSpeechMinInterval,
                carriedSpeechMaxInterval
            );

        fallingVelocityThreshold =
            Mathf.Max(
                0.1f,
                fallingVelocityThreshold
            );

        fallingSpeechCooldown =
            Mathf.Max(
                0f,
                fallingSpeechCooldown
            );

        idleSpeechMinInterval =
            Mathf.Max(
                0.1f,
                idleSpeechMinInterval
            );

        idleSpeechMaxInterval =
            Mathf.Max(
                idleSpeechMinInterval,
                idleSpeechMaxInterval
            );

        bubbleDuration =
            Mathf.Max(
                0.1f,
                bubbleDuration
            );

        bubbleWidth =
            Mathf.Max(
                80f,
                bubbleWidth
            );

        bubbleHeight =
            Mathf.Max(
                40f,
                bubbleHeight
            );

        fontSize =
            Mathf.Max(
                8f,
                fontSize
            );

        bubbleWorldScale =
            Mathf.Max(
                0.0001f,
                bubbleWorldScale
            );

        appearDuration =
            Mathf.Max(
                0.01f,
                appearDuration
            );

        appearStartScale =
            Mathf.Clamp(
                appearStartScale,
                0.1f,
                1.5f
            );

        appearOvershootScale =
            Mathf.Clamp(
                appearOvershootScale,
                1f,
                1.5f
            );

        floatingAmplitude =
            Mathf.Max(
                0f,
                floatingAmplitude
            );

        floatingSpeed =
            Mathf.Max(
                0f,
                floatingSpeed
            );

        fallingShakeStrength =
            Mathf.Max(
                0f,
                fallingShakeStrength
            );

        fallingShakeDuration =
            Mathf.Max(
                0f,
                fallingShakeDuration
            );

        fallingBaseScale =
            Mathf.Clamp(
                fallingBaseScale,
                1f,
                1.5f
            );

        fallingOvershootScale =
            Mathf.Max(
                fallingBaseScale,
                Mathf.Clamp(
                    fallingOvershootScale,
                    1f,
                    1.6f
                )
            );

        disappearDuration =
            Mathf.Max(
                0.01f,
                disappearDuration
            );

        disappearEndScale =
            Mathf.Clamp(
                disappearEndScale,
                0.1f,
                1f
            );

        if (Application.isPlaying)
        {
            ApplyVisualSettings();
        }
    }
}
