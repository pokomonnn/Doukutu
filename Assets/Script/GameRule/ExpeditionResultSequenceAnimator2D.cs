using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Result画面の表示「だけ」を演出する追加コンポーネントです。
/// NPC/ミッション/所持金の計算や受け取りには一切触れません。
/// ExpeditionResultUI と同じGameObjectに付けてください。
/// </summary>
[DisallowMultipleComponent]
public sealed class ExpeditionResultSequenceAnimator2D : MonoBehaviour
{
    [Serializable]
    public sealed class RevealRow
    {
        [Tooltip("1行全体のRectTransformを指定。テキストだけでなくラベルや背景もまとめて動かせます。")]
        public RectTransform target;

        [Tooltip("この行だけで再生する音。未設定なら共通の表示音を使います。")]
        public AudioClip soundOverride;

        [Tooltip("ONなら共通の表示音ではなく報酬音を使用します（音の個別指定がない場合）。")]
        public bool useRewardSound;
    }

    [Header("参照（同じGameObjectにExpeditionResultUIを推奨）")]
    [SerializeField] private ExpeditionResultUI expeditionResultUI;
    [Tooltip("NPC治療結果のUI。未設定なら同じResultシーン内から検索します。")]
    [SerializeField] private RescueNpcTreatmentResultUI2D npcTreatmentResultUI;

    [Header("表示する項目と順序")]
    [Tooltip("設定するとこの並び順が優先。1行ずつ上から下へ登録してください。空なら既存Textを画面の上から自動収集します。")]
    [SerializeField] private List<RevealRow> customRows = new List<RevealRow>();
    [SerializeField] private bool includeNpcTreatmentInAutoRows = true;
    [Tooltip("カスタム未指定時は画面上のY座標が高い順に表示します。")]
    [SerializeField] private bool sortAutoRowsByScreenY = true;
    [Tooltip("ONなら町へ戻るButtonを最後に表示し、演出完了まで押せないようにします。")]
    [SerializeField] private bool revealReturnTownButtonLast = true;

    [Header("演出（秒 / UI座標）")]
    [SerializeField] private bool playOnStart = true;
    [SerializeField, Min(0f)] private float initialDelay = 0.30f;
    [SerializeField, Min(0.01f)] private float enterSeconds = 0.40f;
    [SerializeField, Min(0f)] private float gapSeconds = 0.18f;
    [Tooltip("正の値を指定すると、各行が少し上から下へ滑り込んできます。")]
    [SerializeField, Min(0f)] private float slideDownDistance = 35f;

    [Header("効果音")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("項目が登場した時に再生する共通音。")]
    [SerializeField] private AudioClip itemAppearSound;
    [Tooltip("合計報酬などUse Reward SoundがONの行の音。")]
    [SerializeField] private AudioClip rewardAppearSound;
    [Tooltip("全項目が揃った時の音（任意）。")]
    [SerializeField] private AudioClip sequenceFinishedSound;
    [SerializeField, Range(0f, 1f)] private float volume = 0.85f;

    [Header("デバッグ")]
    [SerializeField] private bool showWarnings = true;

    private sealed class RowState
    {
        public RectTransform rect;
        public CanvasGroup group;
        public Vector2 originalPosition;
        public float originalAlpha;
        public bool originalInteractable;
        public bool originalBlocksRaycasts;
        public AudioClip overrideClip;
        public bool isReward;
        public Button button;
        public bool originalButtonInteractable;
    }

    private readonly List<RowState> rows = new List<RowState>();
    private Coroutine sequenceCoroutine;
    private bool prepared;

    private void Awake()
    {
        ResolveReferences();
        BuildRows();
        PrepareHidden();
    }

    private IEnumerator Start()
    {
        if (!playOnStart)
        {
            RevealEverything();
            yield break;
        }

        // 他コンポーネントのStartでResultの内容と報酬処理が確定してから再生する。
        // 表示する子Object自体は無効化しないので、既存UIのStartも正常に呼ばれます。
        yield return null;
        Play();
    }

    private void OnDisable()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
        RevealEverything();
    }

    private void ResolveReferences()
    {
        if (expeditionResultUI == null)
            expeditionResultUI = GetComponent<ExpeditionResultUI>();
        if (expeditionResultUI == null)
            expeditionResultUI = FindAnyObjectByType<ExpeditionResultUI>(FindObjectsInactive.Include);

        if (npcTreatmentResultUI == null && includeNpcTreatmentInAutoRows)
            npcTreatmentResultUI = FindAnyObjectByType<RescueNpcTreatmentResultUI2D>(FindObjectsInactive.Include);

        if (audioSource == null)
        {
            // 既存のBGM/SE AudioSourceを上書きせず、専用の2D音声Sourceを作る。
            Transform child = transform.Find("ResultSequenceAudio2D");
            if (child == null)
            {
                GameObject holder = new GameObject("ResultSequenceAudio2D");
                child = holder.transform;
                child.SetParent(transform, false);
            }

            audioSource = child.GetComponent<AudioSource>();
            if (audioSource == null) audioSource = child.gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.loop = false;
        }
    }

    private void BuildRows()
    {
        rows.Clear();
        Canvas.ForceUpdateCanvases();

        if (customRows != null && HasCustomRows())
        {
            foreach (RevealRow configured in customRows)
            {
                if (configured != null && configured.target != null)
                    AddRow(configured.target, configured.soundOverride, configured.useRewardSound);
            }
        }
        else
        {
            BuildAutomaticRows();
        }

        // 旧版/新版のExpeditionResultUIどちらでも動くよう、既存SerializeFieldを参照。
        // v1.0の演出用追加プロパティには依存しません。
        Button returnButton = ReadSerializedField<Button>(
            expeditionResultUI, "returnTownButton");

        if (revealReturnTownButtonLast && returnButton != null)
        {
            RectTransform buttonRect = returnButton.GetComponent<RectTransform>();
            if (buttonRect != null)
            {
                // カスタムリストの中にButtonが入っていても最後へ移す。
                RowState buttonRow = null;
                for (int i = 0; i < rows.Count; i++)
                {
                    if (rows[i].rect == buttonRect)
                    {
                        buttonRow = rows[i];
                        rows.RemoveAt(i);
                        break;
                    }
                }

                if (buttonRow == null)
                    buttonRow = MakeRow(buttonRect, null, false);
                if (buttonRow != null)
                    rows.Add(buttonRow);
            }
        }

        if (rows.Count == 0 && showWarnings)
        {
            Debug.LogWarning("[ResultSequence] 表示対象がありません。Custom Rowsを登録するか、ExpeditionResultUIへの参照を設定してください。", this);
        }
    }

    private bool HasCustomRows()
    {
        foreach (RevealRow row in customRows)
            if (row != null && row.target != null) return true;
        return false;
    }

    // ExpeditionResultUIの元から存在するSerializeField名。
    // UI本体のコードを更新しなくても、各欄を自動収集できます。
    private static readonly string[] RevealTextFieldNames =
    {
        "rescuedNpcCountText",
        "deadNpcCountText",
        "recoveredItemBoxCountText",
        "treasureListText",
        "expeditionMissionListText",
        "rescueRewardText",
        "recoveredMoneyText",
        "expeditionMissionRewardText",
        "totalRewardText"
    };

    private void BuildAutomaticRows()
    {
        if (expeditionResultUI == null) return;

        TMP_Text total = ReadSerializedField<TMP_Text>(
            expeditionResultUI, "totalRewardText");

        foreach (string fieldName in RevealTextFieldNames)
        {
            TMP_Text text = ReadSerializedField<TMP_Text>(
                expeditionResultUI, fieldName);
            if (text == null || text.rectTransform == null) continue;
            AddRow(text.rectTransform, null, text == total);
        }

        if (includeNpcTreatmentInAutoRows && npcTreatmentResultUI != null)
        {
            TMP_Text text = ReadSerializedField<TMP_Text>(
                npcTreatmentResultUI, "resultText");
            if (text == null) text = npcTreatmentResultUI.GetComponent<TMP_Text>();
            if (text != null && IsOnSameRootCanvas(text))
                AddRow(text.rectTransform, null, false);
        }

        if (sortAutoRowsByScreenY)
            rows.Sort((a, b) => GetTopWorldY(b.rect).CompareTo(GetTopWorldY(a.rect)));
    }

    // UIロジックを更新せず、既存のInspector割り当てを読み取るだけ。
    // 非公開フィールドが見つからない場合はnullを返し、演出対象から除外します。
    private static T ReadSerializedField<T>(Component source, string fieldName)
        where T : UnityEngine.Object
    {
        if (source == null) return null;
        FieldInfo field = source.GetType().GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        if (field == null) return null;
        return field.GetValue(source) as T;
    }

    private bool IsOnSameRootCanvas(Component candidate)
    {
        Canvas root = expeditionResultUI != null
            ? expeditionResultUI.GetComponentInParent<Canvas>()
            : null;
        Canvas other = candidate.GetComponentInParent<Canvas>();
        return root == null || other == null || root.rootCanvas == other.rootCanvas;
    }

    private static float GetTopWorldY(RectTransform rect)
    {
        if (rect == null) return float.MinValue;
        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        return Mathf.Max(corners[1].y, corners[2].y);
    }

    private void AddRow(RectTransform target, AudioClip soundOverride, bool reward)
    {
        if (target == null) return;
        foreach (RowState existing in rows)
        {
            if (existing.rect == target) return;
            if (target.IsChildOf(existing.rect) || existing.rect.IsChildOf(target))
            {
                if (showWarnings)
                    Debug.LogWarning("[ResultSequence] 親子で重複する対象を検出しました。" +
                                     "各行は別のRectTransformを指定してください: " + target.name, target);
                return;
            }
        }

        RowState row = MakeRow(target, soundOverride, reward);
        if (row != null) rows.Add(row);
    }

    private RowState MakeRow(RectTransform rect, AudioClip overrideClip, bool reward)
    {
        if (rect == null) return null;
        CanvasGroup group = rect.GetComponent<CanvasGroup>();
        if (group == null) group = rect.gameObject.AddComponent<CanvasGroup>();

        Button button = rect.GetComponent<Button>();
        return new RowState
        {
            rect = rect,
            group = group,
            originalPosition = rect.anchoredPosition,
            originalAlpha = group.alpha,
            originalInteractable = group.interactable,
            originalBlocksRaycasts = group.blocksRaycasts,
            overrideClip = overrideClip,
            isReward = reward || ContainsTotalRewardText(rect),
            button = button,
            originalButtonInteractable = button != null && button.interactable
        };
    }

    private bool ContainsTotalRewardText(Transform target)
    {
        TMP_Text total = ReadSerializedField<TMP_Text>(
            expeditionResultUI, "totalRewardText");
        return total != null && total.transform.IsChildOf(target);
    }

    private void PrepareHidden()
    {
        prepared = true;
        foreach (RowState row in rows)
        {
            if (row.rect == null || row.group == null) continue;
            row.rect.anchoredPosition = row.originalPosition + Vector2.up * slideDownDistance;
            row.group.alpha = 0f;
            row.group.interactable = false;
            row.group.blocksRaycasts = false;
            if (row.button != null) row.button.interactable = false;
        }
    }

    [ContextMenu("Play中: 最初から演出を再生")]
    public void Play()
    {
        if (!Application.isPlaying) return;
        if (!prepared)
        {
            ResolveReferences();
            BuildRows();
        }
        if (sequenceCoroutine != null) StopCoroutine(sequenceCoroutine);
        PrepareHidden();
        sequenceCoroutine = StartCoroutine(PlayRoutine());
    }

    private IEnumerator PlayRoutine()
    {
        if (initialDelay > 0f)
            yield return new WaitForSecondsRealtime(initialDelay);

        foreach (RowState row in rows)
        {
            if (row.rect == null || row.group == null) continue;

            AudioClip clip = row.overrideClip != null
                ? row.overrideClip
                : (row.isReward && rewardAppearSound != null ? rewardAppearSound : itemAppearSound);
            PlaySound(clip);

            float elapsed = 0f;
            while (elapsed < enterSeconds)
            {
                if (row.rect == null || row.group == null) break;
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / enterSeconds);
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                row.rect.anchoredPosition = row.originalPosition + Vector2.up * slideDownDistance * (1f - eased);
                row.group.alpha = row.originalAlpha * eased;
                yield return null;
            }

            ShowRow(row);
            if (gapSeconds > 0f)
                yield return new WaitForSecondsRealtime(gapSeconds);
        }

        PlaySound(sequenceFinishedSound);
        sequenceCoroutine = null;
    }

    private void ShowRow(RowState row)
    {
        if (row.rect != null) row.rect.anchoredPosition = row.originalPosition;
        if (row.group != null)
        {
            row.group.alpha = row.originalAlpha;
            row.group.interactable = row.originalInteractable;
            row.group.blocksRaycasts = row.originalBlocksRaycasts;
        }
        if (row.button != null) row.button.interactable = row.originalButtonInteractable;
    }

    [ContextMenu("Play中: 演出をスキップして全表示")]
    public void Skip()
    {
        if (sequenceCoroutine != null)
        {
            StopCoroutine(sequenceCoroutine);
            sequenceCoroutine = null;
        }
        RevealEverything();
    }

    private void RevealEverything()
    {
        foreach (RowState row in rows) ShowRow(row);
    }

    private void PlaySound(AudioClip clip)
    {
        if (clip != null && audioSource != null && volume > 0f)
            audioSource.PlayOneShot(clip, volume);
    }

    private void OnValidate()
    {
        initialDelay = Mathf.Max(0f, initialDelay);
        enterSeconds = Mathf.Max(0.01f, enterSeconds);
        gapSeconds = Mathf.Max(0f, gapSeconds);
        slideDownDistance = Mathf.Max(0f, slideDownDistance);
    }
}
