using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 救出NPCの会話パネル・生存者詳細パネル・「T 治療する」案内の重なりを調整します。
/// 優先順位は 会話（固定） > 詳細（必要なら移動） > T案内（必要なら移動）。
/// すべてのUIが各自のLateUpdateで追従した後に実行するため、位置を奪い合いません。
/// MedicalKitNPCInteraction2DがPlayer上に自動追加します。
/// </summary>
[DefaultExecutionOrder(30000)]
[DisallowMultipleComponent]
public sealed class RescueNpcOverheadLayout2D : MonoBehaviour
{
    [Header("参照（通常は自動取得）")]
    [SerializeField] private MedicalKitNPCInteraction2D medicalKitNpcInteraction;
    [SerializeField] private CaveDialogueAnchorFollower dialogueFollower;

    [Header("重なり回避")]
    [Tooltip("UI同士の間隔（スクリーン上のピクセル）")]
    [SerializeField, Range(0f, 60f)] private float panelGapPixels = 14f;
    [Tooltip("画面の端に残す余白（スクリーン上のピクセル）")]
    [SerializeField, Range(0f, 60f)] private float screenMarginPixels = 10f;
    [Tooltip("自動検出した会話パネルを情報・治療案内より優先します")]
    [SerializeField] private bool prioritizeDialogue = true;
    [Tooltip("会話がない場合も生存者詳細と治療案内の重なりを避けます")]
    [SerializeField] private bool separateDetailAndTreatmentPrompt = true;

    [Header("診断")]
    [SerializeField] private bool logReposition;
    [SerializeField, Min(0.5f)] private float logInterval = 3f;

    private readonly List<Rect> obstacles = new List<Rect>(4);
    private readonly Vector3[] corners = new Vector3[4];
    private CaveDialogueAnchorFollower[] followers = new CaveDialogueAnchorFollower[0];
    private float nextFollowerScan;
    private float nextLogTime;

    private void Awake()
    {
        if (medicalKitNpcInteraction == null)
            medicalKitNpcInteraction = GetComponent<MedicalKitNPCInteraction2D>();
    }

    private void LateUpdate()
    {
        // MedicalKitNPCInteraction2DのUpdate(-220)とは別に、すべての追従UIの
        // LateUpdateが終わった最後に、画面座標で位置を確定する。
        LayoutNow(false);
    }

    [ContextMenu("診断 / NPC頭上3種UIと重なり状況を表示")]
    private void PrintDiagnostics()
    {
        LayoutNow(true);
    }

    private void LayoutNow(bool diagnostic)
    {
        if (medicalKitNpcInteraction == null)
            medicalKitNpcInteraction = GetComponent<MedicalKitNPCInteraction2D>();

        Transform npcRoot = medicalKitNpcInteraction != null &&
            medicalKitNpcInteraction.CurrentPromptNpc != null
                ? medicalKitNpcInteraction.CurrentPromptNpc.transform
                : null;

        RescuePersonInfo2D visibleInfo = RescuePersonDetailTooltipUI.VisibleInfo;
        if (npcRoot == null && visibleInfo != null)
            npcRoot = visibleInfo.transform;

        if (npcRoot == null)
        {
            if (diagnostic)
                Debug.Log("[NpcOverheadLayout][診断] 対象NPCなし。T案内が表示される距離か確認してください。", this);
            return;
        }

        RectTransform dialogue = GetDialoguePanel(npcRoot);
        RectTransform detail = RescuePersonDetailTooltipUI.GetVisiblePanelFor(npcRoot);
        RectTransform treatmentPrompt = medicalKitNpcInteraction != null &&
             medicalKitNpcInteraction.CurrentPromptNpc != null &&
             SameNpc(npcRoot, medicalKitNpcInteraction.CurrentPromptNpc.transform)
                ? medicalKitNpcInteraction.VisiblePromptPanelRect : null;

        // 1: 最優先の会話パネルは動かさない。
        obstacles.Clear();
        if (prioritizeDialogue && TryScreenRect(dialogue, out Rect dialogueRect))
            obstacles.Add(dialogueRect);

        // 2: 生存者詳細全体（タイトル + 背景 + 情報）を会話から離す。
        bool detailMoved = false;
        if (TryScreenRect(detail, out Rect detailRect))
        {
            if (obstacles.Count > 0 &&
                TryFindFreeSpot(detailRect, obstacles, out Vector2 detailDelta))
            {
                detailMoved = MoveScreenPixels(detail, detailDelta);
            }

            // 移動後の詳細領域を追加する。T案内は詳細パネル全体を避ける。
            if (separateDetailAndTreatmentPrompt &&
                TryScreenRect(detail, out Rect placedDetail))
                obstacles.Add(placedDetail);
        }

        // 3: T案内は会話・詳細の両方から離す。
        bool promptMoved = false;
        if (obstacles.Count > 0 &&
            TryScreenRect(treatmentPrompt, out Rect promptRect) &&
            TryFindFreeSpot(promptRect, obstacles, out Vector2 promptDelta))
        {
            promptMoved = MoveScreenPixels(treatmentPrompt, promptDelta);
        }

        if (diagnostic || ((detailMoved || promptMoved) && logReposition &&
            Time.unscaledTime >= nextLogTime))
        {
            nextLogTime = Time.unscaledTime + Mathf.Max(0.5f, logInterval);
            Debug.Log(
                "[NpcOverheadLayout][配置] NPC=" + npcRoot.name +
                " / Dialogue=" + Describe(dialogue) +
                " / Detail=" + Describe(detail) +
                " / TPrompt=" + Describe(treatmentPrompt) +
                " / DetailMoved=" + detailMoved +
                " / PromptMoved=" + promptMoved +
                " / ObstacleCount=" + obstacles.Count +
                " / Gap=" + panelGapPixels,
                this);
        }
    }

    private RectTransform GetDialoguePanel(Transform npcRoot)
    {
        if (IsMatchingFollower(dialogueFollower, npcRoot))
            return dialogueFollower.DialoguePanelRect;

        if (Time.unscaledTime >= nextFollowerScan)
        {
            nextFollowerScan = Time.unscaledTime + 0.6f;
            followers = FindObjectsByType<CaveDialogueAnchorFollower>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        foreach (CaveDialogueAnchorFollower follower in followers)
            if (IsMatchingFollower(follower, npcRoot))
                return follower.DialoguePanelRect;
        return null;
    }

    private static bool IsMatchingFollower(CaveDialogueAnchorFollower follower,
                                           Transform npcRoot)
    {
        if (follower == null || !follower.IsDialogueCurrentlyVisible ||
            follower.TargetAnchor == null || npcRoot == null)
            return false;
        return SameNpc(follower.TargetAnchor, npcRoot);
    }

    private static bool SameNpc(Transform a, Transform b)
    {
        return a != null && b != null &&
               (a == b || a.IsChildOf(b) || b.IsChildOf(a));
    }

    private bool TryScreenRect(RectTransform rt, out Rect result)
    {
        result = default(Rect);
        if (!IsActuallyVisible(rt)) return false;

        rt.GetWorldCorners(corners);
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;

        Vector2 p0 = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
        Vector2 p1 = RectTransformUtility.WorldToScreenPoint(camera, corners[1]);
        Vector2 p2 = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
        Vector2 p3 = RectTransformUtility.WorldToScreenPoint(camera, corners[3]);

        float minX = Mathf.Min(Mathf.Min(p0.x, p1.x), Mathf.Min(p2.x, p3.x));
        float maxX = Mathf.Max(Mathf.Max(p0.x, p1.x), Mathf.Max(p2.x, p3.x));
        float minY = Mathf.Min(Mathf.Min(p0.y, p1.y), Mathf.Min(p2.y, p3.y));
        float maxY = Mathf.Max(Mathf.Max(p0.y, p1.y), Mathf.Max(p2.y, p3.y));
        if (float.IsNaN(minX) || float.IsNaN(minY) ||
            float.IsInfinity(maxX) || float.IsInfinity(maxY) ||
            maxX - minX < 1f || maxY - minY < 1f)
            return false;

        result = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private static bool IsActuallyVisible(RectTransform rt)
    {
        if (rt == null || !rt.gameObject.activeInHierarchy) return false;
        Canvas canvas = rt.GetComponentInParent<Canvas>();
        if (canvas == null || !canvas.isActiveAndEnabled) return false;
        CanvasGroup[] groups = rt.GetComponentsInParent<CanvasGroup>(true);
        for (int i = 0; i < groups.Length; i++)
            if (groups[i] != null && groups[i].alpha <= 0.01f) return false;
        return true;
    }

    private bool TryFindFreeSpot(Rect moving, List<Rect> fixedRects,
                                 out Vector2 delta)
    {
        delta = Vector2.zero;
        bool overlaps = false;
        for (int i = 0; i < fixedRects.Count; i++)
            if (IntersectsWithGap(moving, fixedRects[i]))
            {
                overlaps = true;
                break;
            }
        if (!overlaps) return false;

        bool found = false;
        float bestScore = float.PositiveInfinity;
        Vector2 best = Vector2.zero;
        float gap = Mathf.Max(0f, panelGapPixels);

        // 「会話の下→上→右→左」の順で候補を探す。画面端や他パネルとの衝突を考慮。
        foreach (Rect obstacle in fixedRects)
        {
            Vector2[] candidates =
            {
                new Vector2(moving.x, obstacle.yMin - gap - moving.height),
                new Vector2(obstacle.center.x - moving.width * 0.5f,
                            obstacle.yMin - gap - moving.height),
                new Vector2(moving.x, obstacle.yMax + gap),
                new Vector2(obstacle.center.x - moving.width * 0.5f,
                            obstacle.yMax + gap),
                new Vector2(obstacle.xMax + gap, moving.y),
                new Vector2(obstacle.xMin - gap - moving.width, moving.y),
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                Rect possible = new Rect(candidates[i].x, candidates[i].y,
                                         moving.width, moving.height);
                if (!IsValid(possible, fixedRects)) continue;
                Vector2 diff = possible.position - moving.position;
                float sidePenalty = i < 2 ? 0f : (i < 4 ? 35f : 70f);
                float score = diff.magnitude + sidePenalty;
                if (score >= bestScore) continue;
                bestScore = score;
                best = diff;
                found = true;
            }
        }

        // 周囲に別のテキストが多い場合は、上下左右へ少しずつずらして探索。
        if (!found)
        {
            float unit = Mathf.Max(28f, panelGapPixels + Mathf.Min(moving.width, moving.height));
            for (int ring = 1; ring <= 14 && !found; ring++)
            {
                Vector2[] shifts = {
                    new Vector2(0, -unit * ring), new Vector2(0, unit * ring),
                    new Vector2(unit * ring, 0), new Vector2(-unit * ring, 0),
                    new Vector2(unit * ring, -unit * ring),
                    new Vector2(-unit * ring, -unit * ring),
                    new Vector2(unit * ring, unit * ring),
                    new Vector2(-unit * ring, unit * ring)
                };
                foreach (Vector2 shift in shifts)
                {
                    Rect possible = new Rect(moving.position + shift, moving.size);
                    if (!IsValid(possible, fixedRects)) continue;
                    delta = shift;
                    return true;
                }
            }
        }
        if (found) delta = best;
        return found;
    }

    private bool IsValid(Rect candidate, List<Rect> fixedRects)
    {
        float m = Mathf.Max(0f, screenMarginPixels);
        if (candidate.xMin < m || candidate.yMin < m ||
            candidate.xMax > Screen.width - m ||
            candidate.yMax > Screen.height - m)
            return false;
        for (int i = 0; i < fixedRects.Count; i++)
            if (IntersectsWithGap(candidate, fixedRects[i])) return false;
        return true;
    }

    private bool IntersectsWithGap(Rect a, Rect b)
    {
        float g = Mathf.Max(0f, panelGapPixels);
        Rect grown = Rect.MinMaxRect(b.xMin - g, b.yMin - g,
                                     b.xMax + g, b.yMax + g);
        return a.Overlaps(grown);
    }

    private static bool MoveScreenPixels(RectTransform moving,
                                         Vector2 deltaPixels)
    {
        if (moving == null || deltaPixels.sqrMagnitude < 0.001f) return false;
        RectTransform parent = moving.parent as RectTransform;
        if (parent == null) return false;
        Canvas canvas = moving.GetComponentInParent<Canvas>();
        Camera camera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        Vector2 oldScreen = RectTransformUtility.WorldToScreenPoint(camera, moving.position);
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, oldScreen, camera, out Vector2 oldLocal)) return false;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parent, oldScreen + deltaPixels, camera, out Vector2 newLocal)) return false;
        moving.anchoredPosition += newLocal - oldLocal;
        return true;
    }

    private string Describe(RectTransform rt)
    {
        return TryScreenRect(rt, out Rect r)
            ? "x=" + r.xMin.ToString("0") + " y=" + r.yMin.ToString("0") +
              " w=" + r.width.ToString("0") + " h=" + r.height.ToString("0")
            : "非表示/参照なし";
    }
}
