using System;
using System.Text;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Playerへ追加：NPC用Collider2DのLayerMaskとOverlapCircleで治療対象を検出し、
/// 接近時は「T　治療する」をNPC頭上へ表示、Tで既存MedicalKitInventoryUI2DをNPCモードで開きます。
/// Eキーは従来の運搬処理に任せます。
/// 
/// ★Collider2Dの付いたGameObject（子Colliderの場合は子の方）をInspectorのNpc Layersに指定したLayerへ。
/// ★RescuePersonSymptoms2DはCollider2D自身またはその親に付けてください。
/// ★このクラスはPlayerに1個だけ追加してください。前の同名スクリプトと置き換えます。
/// </summary>
[DefaultExecutionOrder(-220)]
[DisallowMultipleComponent]
public sealed class MedicalKitNPCInteraction2D : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private MedicalKitInventoryUI2D medicalKitUI;
    [SerializeField] private Camera worldCamera;
    [SerializeField] private CharacterHealth playerHealth;

    [Header("NPC探索：Layerと距離")]
    [Tooltip("治療できるNPCのCollider2Dが所属するLayerをチェックします。初期値はEverything。設定後はNPC用Layerのみ推奨。")]
    [SerializeField] private LayerMask npcLayers = ~0;
    [Tooltip("プレイヤー中心から、この半径内のCollider2Dを探索します（Unityワールド単位）。")]
    [SerializeField, Min(0.2f)] private float interactionDistance = 1.8f;
    [SerializeField, Min(0.05f)] private float scanInterval = 0.10f;
    [SerializeField] private bool requireNpcInFront;
    [SerializeField] private KeyCode interactKey = KeyCode.T;

    [Header("応急処置が必要なNPCだけ案内する（v3.0）")]
    [Tooltip("ON: 出血・毒・骨折のうち未処置の症状があるNPCにだけ『T 治療する』を表示します。すべて処置済み／無症状ならTでは開きません。")]
    [SerializeField] private bool promptOnlyWhenTreatmentNeeded = true;

    [Header("出血表示と応急処置フラグの食い違い対策（v3.1）")]
    [Tooltip("ON：救出NPCの詳細情報に『出血中』『骨折』『毒』と明記されているのに症状フラグが無い場合、症状管理へ1度だけ反映して治療可能にします。既に処置済みの症状には触れません。")]
    [SerializeField] private bool synchronizeExplicitNpcInfoSymptoms = true;
    [Tooltip("ON：近くのNPCを治療不要と判断した場合、実際の症状フラグ・表示中の健康状態を3秒に1回Consoleへ出力します。確認後はOFFで構いません。")]
    [SerializeField] private bool logTreatmentNeedDiagnosis = true;

    [Header("画面表示を停止する条件")]
    [Tooltip("本当の通常Inventoryのルートが正しく設定されている時だけON推奨。誤参照で案内が全く表示されないことを防ぐため、初期値はOFFです。")]
    [SerializeField] private bool hidePromptWhenMainInventoryOpen = false;

    [Header("案内の表示制御（Inventory表示中だけ非表示）")]
    [Tooltip("ON: Tab用Inventoryが実際に開いている間だけ、T治療するの案内を非表示にします。NPC検出やEキー運搬には影響しません。")]
    [SerializeField] private bool hidePromptBehindInventory = true;
    [Tooltip("自動生成する案内Canvasの描画順。通常は1100。Inventoryより背面化するのは描画順ではなく開いている間の非表示処理で対応します。")]
    [SerializeField] private int promptSortingOrder = 1100;
    [Tooltip("実際にTabでON/OFFされる通常Inventoryのパネル。空欄ならInventoryPanelToggleのInventory Panel参照を自動取得します。Canvasや常時ONのInventory親を設定しないでください。")]
    [SerializeField] private GameObject actualTabInventoryPanel;

    [Header("近接案内『T　治療する』")]
    [Tooltip("未指定なら通常Inventoryとは別の専用Overlay CanvasとTMPを自動生成します。")]
    [SerializeField] private TMP_Text interactionPromptText;
    [Tooltip("日本語が□になる場合は日本語グリフを含むTMP Font Assetを明示設定してください。")]
    [SerializeField] private TMP_FontAsset japaneseFont;
    [SerializeField] private string promptMessage = "治療する";
    [SerializeField] private Vector3 promptWorldOffset = new Vector3(0f, 1.35f, 0f);
    [SerializeField] private Vector2 promptScreenOffset;
    [SerializeField, Range(12, 42)] private int promptFontSize = 21;
    [SerializeField] private Color promptBackground = new Color(0.08f, 0.12f, 0.15f, 0.86f);

    [Header("他のNPCテキストとの重なり防止（v2.8）")]
    [Tooltip("ON：生存者情報・会話など、表示中のTextを避けて『T 治療する』の位置を自動調整します。")]
    [SerializeField] private bool avoidOverlappingNpcText = true;
    [Tooltip("ON：対象NPCの子にあるTMPテキストを自動検出します。NPCの外にある会話UIは下のAdditional Textsへ指定してください。")]
    [SerializeField] private bool autoFindNpcChildTexts = true;
    [Tooltip("会話UIなどがNPCの子ではない場合に登録します。空欄でも動作します。")]
    [SerializeField] private TMP_Text[] additionalTextsToAvoid;
    [Tooltip("他のテキストと離す画面上の距離（ピクセル）。")]
    [SerializeField, Range(0f, 60f)] private float textGapPixels = 14f;
    [Tooltip("テキストを避けるためにずらす段数。")]
    [SerializeField, Range(1, 16)] private int maximumAvoidanceSteps = 12;
    [Tooltip("画面端との余白（ピクセル）。")]
    [SerializeField, Range(0f, 60f)] private float screenMarginPixels = 8f;
    [Tooltip("ONにすると、他のテキストを避けて移動したときにConsoleへ簡易ログを表示します。")]
    [SerializeField] private bool logPromptReposition;

    [Header("Console診断")]
    [SerializeField] private bool showDebugLogs = true;
    [Tooltip("ONなら設定した秒間隔でNPC探索結果も表示。通常はOFFでOK。")]
    [SerializeField] private bool verboseScanLogs;
    [SerializeField, Min(0.5f)] private float diagnosticLogInterval = 3f;

    [Header("自動原因診断（案内が表示されない場合）")]
    [Tooltip("ON: 案内が表示されない時に、Layer・Collider・NPC状態・Canvas・Fontを自動でConsoleへ報告します。")]
    [SerializeField] private bool autoDiagnoseMissingPrompt = true;
    [Tooltip("自動原因診断の間隔。出力過多を防ぐため、初期値5秒。")]
    [SerializeField, Min(1f)] private float autoDiagnosticInterval = 5f;
    [Tooltip("詳細診断に全Layerの近隣ColliderとScene内NPC候補を含めます。")]
    [SerializeField] private bool inspectAllLayersAndNpcs = true;

    // Eキー運搬パッチを以前導入した環境とのコンパイル互換を維持。
    public static bool WasEConsumedThisFrame => false;

    public RescuePersonSymptoms2D NearestNpc => nearestNpc;
    public float InteractionDistance => interactionDistance;
    public LayerMask NpcLayerMask => npcLayers;


    // 頭上の3種UIをまとめて配置する専用スクリプト用の読み取り専用API。
    // NPC検出・Tキー入力・治療処理は変更しません。
    public RescuePersonSymptoms2D CurrentPromptNpc => nearestNpc;
    public RectTransform VisiblePromptPanelRect =>
        promptPositionRect != null &&
        promptPositionRect.gameObject.activeInHierarchy &&
        interactionPromptText != null && interactionPromptText.isActiveAndEnabled
            ? promptPositionRect : null;

    private RescuePersonSymptoms2D nearestNpc;
    private Collider2D nearestCollider;
    private Collider2D[] colliderBuffer = new Collider2D[64];
    private Canvas generatedCanvas;
    private InventoryPanelToggle inventoryPanelToggle;
    private bool warnedMissingActualInventoryPanel;
    private bool warnedLegacySortingOrder;
    private bool wasHiddenByInventory;
    private RectTransform promptPositionRect;
    private RectTransform promptParentRect;
    private bool ownsPrompt;
    private bool wasShowing;
    private bool warnedFont;
    private bool warnedCamera;
    private bool warnedMissingUi;
    private bool warnedBadLayers;
    private bool warnedMissingPromptParent;
    private float nextScanTime;
    private float nextUiResolveTime;
    private float nextDiagnosticLog;
    private int lastPhysicsCount;
    private int lastEligibleCount;
    private bool foundNearbyNpcWithoutTreatmentNeed;
    private float nextTreatmentNeedDiagnosticTime;
    // 情報UIの古い文字列で完治後の症状を再付与しないよう、1度同期した種類を記録します。
    private readonly System.Collections.Generic.Dictionary<int, StatusConditionType>
        synchronizedInfoSymptomsByNpc =
            new System.Collections.Generic.Dictionary<int, StatusConditionType>();
    private float nextAutomaticDiagnosticTime;
    private bool hasReportedStartupDiagnostics;

    private RescuePersonSymptoms2D cachedAvoidTarget;
    private TMP_Text[] cachedNpcTexts = Array.Empty<TMP_Text>();
    private float nextAvoidCacheRefresh;
    private readonly System.Collections.Generic.List<Rect> avoidanceRects =
        new System.Collections.Generic.List<Rect>(16);
    private readonly Vector3[] promptCorners = new Vector3[4];
    private float nextRepositionLogTime;

    private void Awake()
    {
        MigrateLegacyEKey();
        MigrateLegacyPromptOrder();
        ResolveReferences();
        EnsurePrompt();
        HidePrompt();
        EnsureSharedOverheadLayout();
        if (showDebugLogs)
            Debug.Log("[MedicalKitNPC][起動] Layer検索 / Key=" + interactKey +
                      " / LayerMask=" + FormatMask(npcLayers) +
                      " / Radius=" + interactionDistance.ToString("0.00") +
                      " / UI=" + (medicalKitUI != null ? medicalKitUI.name : "未設定"), this);
    }

    private void OnEnable()
    {
        MigrateLegacyEKey();
        MigrateLegacyPromptOrder();
        ResolveReferences();
        EnsurePrompt();
        HidePrompt();
        EnsureSharedOverheadLayout();
        nextScanTime = 0f;
        nextDiagnosticLog = 0f;
        wasHiddenByInventory = false;
        nextAutomaticDiagnosticTime = Time.unscaledTime + 1f;
        hasReportedStartupDiagnostics = false;
        if (showDebugLogs)
            Debug.Log("[MedicalKitNPC][有効化] Player=" + PathOf(transform) +
                      " / ComponentEnabled=" + enabled +
                      " / GameObjectActive=" + gameObject.activeInHierarchy +
                      " / PromptText=" + (interactionPromptText != null ? PathOf(interactionPromptText.transform) : "未生成") +
                      " / LayerMask=" + FormatMask(npcLayers), this);
    }

    private void Update()
    {
        bool pressed = Input.GetKeyDown(interactKey);
        if (medicalKitUI == null && Time.unscaledTime >= nextUiResolveTime)
        {
            nextUiResolveTime = Time.unscaledTime + 1f;
            ResolveReferences();
        }

        // Canvasを無理に背面へ落とさず、Tabで開く実際のInventoryが表示中のときだけ消す。
        // Inventorys/Canvasなど常時Activeな親を可視判定に使うと案内が永久に消えるので使わない。
        bool hiddenByInventory = hidePromptBehindInventory && IsActualInventoryVisible();
        if (hiddenByInventory != wasHiddenByInventory && showDebugLogs)
        {
            Debug.Log("[MedicalKitNPC][案内表示制御] " +
                      (hiddenByInventory ? "Tab Inventory表示中なので案内を隠す" : "Tab Inventoryが閉じたので案内を再開") +
                      " / TabPanel=" +
                      (actualTabInventoryPanel != null ? PathOf(actualTabInventoryPanel.transform) : "未検出") +
                      " / TabPanelActive=" + (actualTabInventoryPanel != null && actualTabInventoryPanel.activeInHierarchy), this);
        }
        wasHiddenByInventory = hiddenByInventory;
        if (hiddenByInventory)
        {
            nearestNpc = null;
            nearestCollider = null;
            HidePrompt();
            return;
        }

        string blockedReason = GetBlockingReason();
        if (blockedReason != null)
        {
            nearestNpc = null;
            nearestCollider = null;
            HidePrompt();
            if (pressed && showDebugLogs)
                Debug.Log("[MedicalKitNPC][T停止] " + blockedReason, this);
            MaybeAutoDiagnose("入力・案内がブロックされています: " + blockedReason, pressed);
            return;
        }

        if (pressed || Time.unscaledTime >= nextScanTime)
        {
            nextScanTime = Time.unscaledTime + scanInterval;
            ScanNearbyNpc();

            if (verboseScanLogs && Time.unscaledTime >= nextDiagnosticLog)
            {
                nextDiagnosticLog = Time.unscaledTime + diagnosticLogInterval;
                Debug.Log("[MedicalKitNPC][探索] Hits=" + lastPhysicsCount +
                          " / Valid=" + lastEligibleCount +
                          " / Nearest=" + (nearestNpc != null ? nearestNpc.name : "なし"), this);
            }
        }

        if (nearestNpc == null)
        {
            HidePrompt();
            // 治療不要のNPCが近くにいるだけなら異常ではない。診断スパムは出さない。
            if (foundNearbyNpcWithoutTreatmentNeed)
            {
                if (pressed && showDebugLogs)
                    Debug.Log("[MedicalKitNPC][T対象なし] 近くのNPCは無症状または応急処置済みです。", this);
                return;
            }
            if (pressed && showDebugLogs)
                Debug.LogWarning("[MedicalKitNPC][T対象なし] 範囲内に有効なNPCがありません。詳細診断を確認してください。", this);
            MaybeAutoDiagnose("NPCを検出できません", pressed);
            return;
        }

        EnsurePrompt();
        UpdatePromptPosition(nearestNpc);
        string displayIssue = GetPromptVisibilityIssue();
        if (displayIssue != null)
            MaybeAutoDiagnose("NPC検出済み・案内UIが不可視: " + displayIssue, pressed);
        else if (!hasReportedStartupDiagnostics && showDebugLogs)
        {
            hasReportedStartupDiagnostics = true;
            Debug.Log("[MedicalKitNPC][正常表示確認] " + nearestNpc.name +
                      " / Prompt=" + PathOf(promptPositionRect.transform) +
                      " / Text=" + interactionPromptText.text, this);
        }

        if (!pressed)
            return;

        if (showDebugLogs)
            Debug.Log("[MedicalKitNPC][T入力] Target=" + nearestNpc.name +
                      " / Collider=" + (nearestCollider != null ? nearestCollider.name : "なし") +
                      " / Radius=" + interactionDistance, this);

        if (medicalKitUI == null)
        {
            if (!warnedMissingUi)
            {
                warnedMissingUi = true;
                Debug.LogWarning("[MedicalKitNPC] Medical Kit UIの参照がありません。" +
                                 "PlayerのMedicalKitNPCInteraction2DにMedicalKitUIControllerを設定してください。", this);
            }
            return;
        }

        if (medicalKitUI.OpenForNpc(nearestNpc))
        {
            if (showDebugLogs)
                Debug.Log("[MedicalKitNPC][T成功] NPC「" + nearestNpc.name +
                          "」向けの応急キットを開きました。", this);
            HidePrompt();
        }
        else
        {
            Debug.LogWarning("[MedicalKitNPC][T失敗] NPC用の応急キットを開けませんでした。" +
                             " MedicalKitInventoryUI2DのConsoleログも確認してください。", this);
        }
    }

    /// <summary>
    /// Tab用のInventoryPanelToggleが実際にON/OFFするGameObjectだけを判定します。
    /// 常時ActiveのCanvasやInventorysコンテナは判定に利用しません。
    /// </summary>
    private bool IsActualInventoryVisible()
    {
        if (actualTabInventoryPanel == null)
            ResolveActualInventoryPanel();

        if (actualTabInventoryPanel != null)
            return actualTabInventoryPanel.activeInHierarchy;

        // 自動取得に失敗しても、NPC治療メニューが開いている間は案内を隠す。
        // 不明なMain Inventory親のactive状態は絶対に使わない。
        return medicalKitUI != null &&
               (medicalKitUI.IsMedicalPanelOpen || medicalKitUI.IsTreatingNpc);
    }

    private void ResolveActualInventoryPanel()
    {
        if (actualTabInventoryPanel != null) return;
        if (inventoryPanelToggle == null)
            inventoryPanelToggle = FindAnyObjectByType<InventoryPanelToggle>(FindObjectsInactive.Include);
        if (inventoryPanelToggle == null) return;

        // 既存InventoryPanelToggle.csのprivate inventoryPanelを読み取るだけ。
        // 元のTab開閉処理は書き換えず、別のInventoryオブジェクトも起動しません。
        FieldInfo field = typeof(InventoryPanelToggle).GetField(
            "inventoryPanel", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (field == null) return;

        actualTabInventoryPanel = field.GetValue(inventoryPanelToggle) as GameObject;
        if (actualTabInventoryPanel != null && showDebugLogs)
            Debug.Log("[MedicalKitNPC][Inventory検出] Tabで開閉するパネル=" +
                      PathOf(actualTabInventoryPanel.transform) +
                      " / 現在表示=" + actualTabInventoryPanel.activeInHierarchy, this);
    }

    private void MigrateLegacyPromptOrder()
    {
        // v2.5では-100が保存され、通常HUDや背景UIの背面に隠れる場合がありました。
        // 以前の設定値だけを安全な値へ戻します。
        if (promptSortingOrder != -100) return;
        promptSortingOrder = 1100;
        if (!warnedLegacySortingOrder)
        {
            warnedLegacySortingOrder = true;
            Debug.LogWarning("[MedicalKitNPC][v2.6自動修正] v2.5のPrompt Sorting Order=-100を1100に戻しました。" +
                             " Inventory表示中は実際のTabパネルの状態で案内を消します。", this);
        }
    }

    private string GetBlockingReason()
    {
        if (playerHealth != null && playerHealth.IsDead)
            return "プレイヤー死亡";
        if (medicalKitUI != null && medicalKitUI.IsMedicalPanelOpen)
            return "応急キット画面を表示中";
        if (hidePromptWhenMainInventoryOpen && IsActualInventoryVisible())
            return "Tab Inventoryが表示中";
        return null;
    }

    private void ScanNearbyNpc()
    {
        nearestNpc = null;
        nearestCollider = null;
        lastPhysicsCount = 0;
        lastEligibleCount = 0;
        foundNearbyNpcWithoutTreatmentNeed = false;

        if (npcLayers.value == 0)
        {
            if (!warnedBadLayers)
            {
                warnedBadLayers = true;
                Debug.LogWarning("[MedicalKitNPC][Layer設定ミス] Npc Layers が Nothing です。" +
                                 " NPC用Layerを選択してください。", this);
            }
            return;
        }

        Vector2 center = transform.position;
        ContactFilter2D filter = new ContactFilter2D();
        filter.SetLayerMask(npcLayers);
        filter.useTriggers = true; // Trigger Collider2Dにも対応

        // Colliderが多数あるSceneでも検索結果の切り捨てでNPCを見落とさないようにする。
        int count;
        do
        {
            count = Physics2D.OverlapCircle(center, interactionDistance, filter, colliderBuffer);
            if (count < colliderBuffer.Length || colliderBuffer.Length >= 1024) break;
            colliderBuffer = new Collider2D[Mathf.Min(1024, colliderBuffer.Length * 2)];
        } while (true);

        lastPhysicsCount = count;
        float closestSqr = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            Collider2D hit = colliderBuffer[i];
            if (hit == null) continue;
            RescuePersonSymptoms2D npc = ResolveNpcFromCollider(hit);
            if (npc == null || !npc.isActiveAndEnabled || npc.IsDead) continue;

            Vector2 delta = (Vector2)npc.transform.position - center;
            if (requireNpcInFront && delta.x * transform.right.x < 0f) continue;

            if (promptOnlyWhenTreatmentNeeded)
            {
                // NPC詳細情報に明示される「出血中」と実際の症状フラグが
                // 別々に設定されているPrefabsでも、処置対象が一致するようにします。
                if (synchronizeExplicitNpcInfoSymptoms)
                    SynchronizeExplicitSymptomsFromNpcInfo(npc);

                if (!NeedsFirstAid(npc))
                {
                    foundNearbyNpcWithoutTreatmentNeed = true;
                    LogTreatmentNeedDiagnosis(npc);
                    continue;
                }
            }
            lastEligibleCount++;

            Vector2 closestPoint = hit.ClosestPoint(center);
            float sqrDistance = (closestPoint - center).sqrMagnitude;
            if (sqrDistance >= closestSqr) continue;
            nearestNpc = npc;
            nearestCollider = hit;
            closestSqr = sqrDistance;
        }
    }

    // 現在の症状フラグを直接見ます。HPの少なさや「軽傷/重傷」だけでは
    // 応急処置が必要とみなしません（処置できないUIを表示しないため）。
    private static bool NeedsFirstAid(RescuePersonSymptoms2D npc)
    {
        if (npc == null || npc.IsDead || !npc.isActiveAndEnabled) return false;
        return (npc.HasSymptom(StatusConditionType.Bleeding) &&
                !npc.IsStabilized(StatusConditionType.Bleeding)) ||
               (npc.HasSymptom(StatusConditionType.Poison) &&
                !npc.IsStabilized(StatusConditionType.Poison)) ||
               (npc.HasSymptom(StatusConditionType.Fracture) &&
                !npc.IsStabilized(StatusConditionType.Fracture));
    }

    // NPC詳細の状態欄に「出血中」などの具体的な症状が書かれていても、
    // 旧PrefabではRescuePersonSymptoms2DのInitial SymptomsがNoneのままの
    // 場合がありました。症状の明示がある時だけ、治療可能な症状へ同期します。
    // 「軽傷」「重傷」だけから症状を推測することはしません。
    private void SynchronizeExplicitSymptomsFromNpcInfo(RescuePersonSymptoms2D npc)
    {
        if (npc == null || npc.IsDead) return;

        RescuePersonInfo2D info = FindNpcDetailInfo(npc);
        if (info == null) return;
        string condition = Convert.ToString(info.ConditionLabel);
        if (string.IsNullOrEmpty(condition)) return;

        StatusConditionType explicitSymptoms = StatusConditionType.None;
        if ((condition.Contains("出血中") || condition.Contains("出血あり") ||
             condition.Contains("出血して") || condition == "出血") &&
            !condition.Contains("止血") && !condition.Contains("処置済"))
            explicitSymptoms |= StatusConditionType.Bleeding;
        if ((condition.Contains("骨折中") || condition.Contains("骨折あり") ||
             condition == "骨折") && !condition.Contains("固定済") &&
            !condition.Contains("処置済"))
            explicitSymptoms |= StatusConditionType.Fracture;
        if ((condition.Contains("毒状態") || condition.Contains("毒状態中") ||
             condition.Contains("中毒中") || condition == "毒") &&
            !condition.Contains("処置済"))
            explicitSymptoms |= StatusConditionType.Poison;

        if (explicitSymptoms == StatusConditionType.None) return;

        int id = npc.GetInstanceID();
        StatusConditionType alreadySynchronized;
        if (!synchronizedInfoSymptomsByNpc.TryGetValue(id, out alreadySynchronized))
            alreadySynchronized = StatusConditionType.None;
        // いったん治療・完治してから元の情報ラベルが残っても再付与しません。
        StatusConditionType missing = explicitSymptoms &
            ~npc.ActiveSymptoms & ~alreadySynchronized;
        // 既に症状がある場合も登録し、完治後に古い詳細表示から再付与しない。
        synchronizedInfoSymptomsByNpc[id] = alreadySynchronized | explicitSymptoms;
        if (missing == StatusConditionType.None) return;

        if (npc.AddSymptoms(missing) && showDebugLogs)
            Debug.Log("[MedicalKitNPC][症状同期] NPC=" + npc.name +
                      " / 詳細表示='" + condition + "'" +
                      " / 追加した未処置症状=" + missing +
                      " / 既存の処置済み状態は維持", npc);
    }

    private static RescuePersonInfo2D FindNpcDetailInfo(RescuePersonSymptoms2D npc)
    {
        if (npc == null) return null;
        RescuePersonInfo2D info = npc.GetComponent<RescuePersonInfo2D>();
        if (info == null) info = npc.GetComponentInParent<RescuePersonInfo2D>();
        if (info == null) info = npc.GetComponentInChildren<RescuePersonInfo2D>(true);
        return info;
    }

    private void LogTreatmentNeedDiagnosis(RescuePersonSymptoms2D npc)
    {
        if (!logTreatmentNeedDiagnosis || npc == null ||
            Time.unscaledTime < nextTreatmentNeedDiagnosticTime) return;
        nextTreatmentNeedDiagnosticTime = Time.unscaledTime + 3f;
        RescuePersonInfo2D info = FindNpcDetailInfo(npc);
        Debug.Log("[MedicalKitNPC][治療条件診断] NPC=" + PathOf(npc.transform) +
                  " / ActiveSymptoms=" + npc.ActiveSymptoms +
                  " / StabilizedSymptoms=" + npc.StabilizedSymptoms +
                  " / 未処置=" + (npc.ActiveSymptoms & ~npc.StabilizedSymptoms) +
                  " / 詳細ConditionLabel=" + (info != null ? Convert.ToString(info.ConditionLabel) : "情報なし") +
                  " / NPC死亡=" + npc.IsDead +
                  " / 結果=未処置症状なしのためT案内非表示。" +
                  " 出血中のはずならActiveSymptomsにBleedingがあるか確認してください。", npc);
    }

    private static RescuePersonSymptoms2D ResolveNpcFromCollider(Collider2D hit)
    {
        if (hit == null) return null;
        RescuePersonSymptoms2D npc = hit.GetComponent<RescuePersonSymptoms2D>();
        if (npc == null) npc = hit.GetComponentInParent<RescuePersonSymptoms2D>();
        if (npc == null && hit.attachedRigidbody != null)
        {
            npc = hit.attachedRigidbody.GetComponent<RescuePersonSymptoms2D>();
            if (npc == null) npc = hit.attachedRigidbody.GetComponentInParent<RescuePersonSymptoms2D>();
        }
        return npc;
    }

    private void EnsureSharedOverheadLayout()
    {
        if (!Application.isPlaying)
            return;

        // レイアウトだけをLateUpdateで処理。T入力の実行順序は変更しない。
        if (GetComponent<RescueNpcOverheadLayout2D>() == null)
            gameObject.AddComponent<RescueNpcOverheadLayout2D>();
    }

    private void ResolveReferences()
    {
        if (medicalKitUI == null)
            medicalKitUI = FindAnyObjectByType<MedicalKitInventoryUI2D>(FindObjectsInactive.Include);
        if (worldCamera == null) worldCamera = Camera.main;
        if (playerHealth == null) playerHealth = GetComponent<CharacterHealth>();
        ResolveActualInventoryPanel();

        if (japaneseFont == null && medicalKitUI != null)
        {
            TMP_Text[] labels = medicalKitUI.GetComponentsInChildren<TMP_Text>(true);
            foreach (TMP_Text label in labels)
            {
                if (label != null && label.font != null && label.font.HasCharacter('治', true, false))
                {
                    japaneseFont = label.font;
                    break;
                }
            }
        }
        if (interactionPromptText != null && japaneseFont != null)
            interactionPromptText.font = japaneseFont;
    }

    private void MigrateLegacyEKey()
    {
        if (interactKey != KeyCode.E) return;
        interactKey = KeyCode.T;
        if (showDebugLogs)
            Debug.Log("[MedicalKitNPC] 以前のEキー設定をTキーへ変更しました（Eは運搬専用）。", this);
    }

    private void EnsurePrompt()
    {
        if (generatedCanvas != null && ownsPrompt &&
            generatedCanvas.sortingOrder != promptSortingOrder)
            generatedCanvas.sortingOrder = promptSortingOrder;

        if (interactionPromptText != null)
        {
            if (promptPositionRect == null)
            {
                promptPositionRect = interactionPromptText.rectTransform;
                if (!ownsPrompt && interactionPromptText.transform.parent is RectTransform rect)
                    promptParentRect = rect;
            }
            if (japaneseFont != null && interactionPromptText.font != japaneseFont)
                interactionPromptText.font = japaneseFont;
            SetPromptText();
            CheckFont();
            return;
        }

        // Tab Inventoryの下には置かない。独立Canvasで常時表示可能にする。
        GameObject canvasObject = new GameObject(
            "MedicalKit_NPC_TreatmentPromptCanvas", typeof(RectTransform),
            typeof(Canvas), typeof(CanvasScaler));
        generatedCanvas = canvasObject.GetComponent<Canvas>();
        generatedCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        generatedCanvas.overrideSorting = true;
        // v2.6: 通常時に確実に見えるCanvas描画順に戻す。
        // Inventoryが開いている間は実際のTabパネル判定で非表示にする。
        generatedCanvas.sortingOrder = promptSortingOrder;
        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject backgroundObject = new GameObject("Prompt_T_Treat", typeof(RectTransform), typeof(Image));
        backgroundObject.transform.SetParent(canvasObject.transform, false);
        RectTransform backgroundRect = backgroundObject.GetComponent<RectTransform>();
        backgroundRect.anchorMin = backgroundRect.anchorMax = new Vector2(0.5f, 0.5f);
        backgroundRect.pivot = new Vector2(0.5f, 0.5f);
        backgroundRect.sizeDelta = new Vector2(210f, 42f);
        Image backgroundImage = backgroundObject.GetComponent<Image>();
        backgroundImage.color = promptBackground;
        backgroundImage.raycastTarget = false;

        GameObject textObject = new GameObject("PromptLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(backgroundObject.transform, false);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 2f);
        textRect.offsetMax = new Vector2(-8f, -2f);
        TextMeshProUGUI label = textObject.GetComponent<TextMeshProUGUI>();
        label.fontSize = promptFontSize;
        label.alignment = TextAlignmentOptions.Center;
        label.color = Color.white;
        label.enableWordWrapping = false;
        label.raycastTarget = false;
        if (japaneseFont != null) label.font = japaneseFont;

        interactionPromptText = label;
        promptPositionRect = backgroundRect;
        promptParentRect = canvasObject.GetComponent<RectTransform>();
        ownsPrompt = true;
        SetPromptText();
        CheckFont();
    }

    private void SetPromptText()
    {
        if (interactionPromptText != null)
            interactionPromptText.text = interactKey + "　" + promptMessage;
    }

    private void CheckFont()
    {
        if (warnedFont || interactionPromptText == null) return;
        TMP_FontAsset font = interactionPromptText.font;
        if (font != null && font.HasCharacter('治', true, false)) return;
        warnedFont = true;
        Debug.LogWarning("[MedicalKitNPC][フォント] 『治』のグリフがありません。" +
                         " Japanese Fontへ日本語対応TMP Font Assetを割り当ててください。", this);
    }

    private void UpdatePromptPosition(RescuePersonSymptoms2D npc)
    {
        if (npc == null || promptPositionRect == null) return;
        if (worldCamera == null) worldCamera = Camera.main;
        if (worldCamera == null)
        {
            if (!warnedCamera)
            {
                warnedCamera = true;
                Debug.LogWarning("[MedicalKitNPC][Camera] Main Cameraがありません。World Cameraを指定してください。", this);
            }
            HidePrompt();
            return;
        }
        Vector3 world = npc.transform.position + promptWorldOffset;
        Vector3 screen = worldCamera.WorldToScreenPoint(world);
        if (screen.z <= 0f)
        {
            HidePrompt();
            if (verboseScanLogs && Time.unscaledTime >= nextDiagnosticLog)
                Debug.LogWarning("[MedicalKitNPC][画面外] NPCがCameraの後ろです。Camera / World Offsetを確認。", this);
            return;
        }
        if (promptParentRect == null)
        {
            if (!warnedMissingPromptParent)
            {
                warnedMissingPromptParent = true;
                Debug.LogWarning("[MedicalKitNPC][UI] Promptの親RectTransformがありません。" +
                                 " Interaction Prompt Textを一度空欄にして自動生成を試してください。", this);
            }
            HidePrompt();
            return;
        }
        Canvas canvas = promptPositionRect.GetComponentInParent<Canvas>();
        Camera canvasCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? canvas.worldCamera : null;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                promptParentRect, screen, canvasCamera, out Vector2 local))
        {
            HidePrompt();
            return;
        }
        promptPositionRect.anchoredPosition = local + promptScreenOffset;
        if (avoidOverlappingNpcText)
            RepositionPromptAwayFromOtherTexts(npc);
        if (!wasShowing || !promptPositionRect.gameObject.activeSelf)
        {
            promptPositionRect.gameObject.SetActive(true);
            wasShowing = true;
            if (showDebugLogs)
                Debug.Log("[MedicalKitNPC][案内表示] " + npc.name + " / Layer=" +
                          LayerMask.LayerToName(nearestCollider != null ? nearestCollider.gameObject.layer : npc.gameObject.layer), this);
        }
    }

    /// <summary>
    /// NPC情報・会話のUIに干渉せず、治療案内の位置だけを調整します。
    /// 他のテキストが表示中でない場合は、これまでのPrompt World Offsetをそのまま使用します。
    /// </summary>
    private void RepositionPromptAwayFromOtherTexts(RescuePersonSymptoms2D npc)
    {
        if (npc == null || promptPositionRect == null || promptParentRect == null)
            return;

        RefreshNpcTextCache(npc);
        avoidanceRects.Clear();
        for (int i = 0; i < cachedNpcTexts.Length; i++)
            TryAddAvoidanceRect(cachedNpcTexts[i]);
        if (additionalTextsToAvoid != null)
        {
            for (int i = 0; i < additionalTextsToAvoid.Length; i++)
                TryAddAvoidanceRect(additionalTextsToAvoid[i]);
        }

        if (avoidanceRects.Count == 0) return;
        if (!TryGetPromptScreenRect(out Rect original)) return;
        if (IsValidPromptPosition(original)) return;

        float stepY = Mathf.Max(28f, original.height + textGapPixels);
        float stepX = Mathf.Max(28f, original.width + textGapPixels);
        int steps = Mathf.Clamp(maximumAvoidanceSteps, 1, 16);
        Vector2 offset = Vector2.zero;
        bool found = false;

        // 会話・情報の下側を優先し、画面外になる場合は上側へ移動します。
        // 左右の移動は、上下だけでは空きが見つからないときの最終手段です。
        for (int pass = 0; pass < 4 && !found; pass++)
        {
            for (int n = 1; n <= steps; n++)
            {
                float shift = pass < 2 ? n * stepY : n * stepX;
                float dx = pass == 2 ? shift : pass == 3 ? -shift : 0f;
                float dy = pass == 0 ? -shift : pass == 1 ? shift : 0f;
                Rect proposed = new Rect(original.x + dx, original.y + dy,
                                         original.width, original.height);
                if (!IsValidPromptPosition(proposed)) continue;
                offset = new Vector2(dx, dy);
                found = true;
                break;
            }
        }
        if (!found) return;

        Canvas canvas = promptPositionRect.GetComponentInParent<Canvas>();
        Camera canvasCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? (canvas.worldCamera != null ? canvas.worldCamera : worldCamera) : null;
        Vector2 before, after;
        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                promptParentRect, original.center, canvasCam, out before) &&
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                promptParentRect, original.center + offset, canvasCam, out after))
        {
            promptPositionRect.anchoredPosition += after - before;
            if (logPromptReposition && Time.unscaledTime >= nextRepositionLogTime)
            {
                nextRepositionLogTime = Time.unscaledTime + 2f;
                Debug.Log("[MedicalKitNPC][重なり回避] Target=" + npc.name +
                          " / AvoidTexts=" + avoidanceRects.Count +
                          " / ScreenShift=" + offset, this);
            }
        }
    }

    private void RefreshNpcTextCache(RescuePersonSymptoms2D npc)
    {
        if (!autoFindNpcChildTexts)
        {
            cachedNpcTexts = Array.Empty<TMP_Text>();
            cachedAvoidTarget = null;
            return;
        }
        if (npc != cachedAvoidTarget || Time.unscaledTime >= nextAvoidCacheRefresh)
        {
            cachedAvoidTarget = npc;
            nextAvoidCacheRefresh = Time.unscaledTime + 0.7f;
            cachedNpcTexts = npc.GetComponentsInChildren<TMP_Text>(true);
        }
    }

    private void TryAddAvoidanceRect(TMP_Text label)
    {
        if (label == null || label == interactionPromptText ||
            !label.isActiveAndEnabled || string.IsNullOrWhiteSpace(label.text) ||
            label.color.a <= 0.01f) return;
        Canvas c = label.GetComponentInParent<Canvas>();
        if (c != null && !c.enabled) return;
        CanvasGroup[] groups = label.GetComponentsInParent<CanvasGroup>(true);
        foreach (CanvasGroup group in groups)
            if (group != null && group.alpha <= 0.01f) return;

        Rect screenRect;
        if (label is TextMeshProUGUI && label.rectTransform != null)
        {
            RectTransform rect = label.rectTransform;
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Camera cameraForUI = c != null && c.renderMode != RenderMode.ScreenSpaceOverlay
                ? (c.worldCamera != null ? c.worldCamera : worldCamera) : null;
            if (!CornersToScreenRect(corners, cameraForUI, out screenRect)) return;
        }
        else
        {
            Renderer renderer = label.GetComponent<Renderer>();
            if (renderer == null || worldCamera == null) return;
            Bounds b = renderer.bounds;
            Vector3[] corners = new Vector3[8];
            int index = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                corners[index++] = b.center + Vector3.Scale(b.extents,
                                          new Vector3(x, y, z));
            if (!CornersToScreenRect(corners, worldCamera, out screenRect)) return;
        }
        if (screenRect.width < 1f || screenRect.height < 1f) return;
        avoidanceRects.Add(screenRect);
    }

    private bool TryGetPromptScreenRect(out Rect screenRect)
    {
        promptPositionRect.GetWorldCorners(promptCorners);
        Canvas canvas = promptPositionRect.GetComponentInParent<Canvas>();
        Camera cameraForUI = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? (canvas.worldCamera != null ? canvas.worldCamera : worldCamera) : null;
        return CornersToScreenRect(promptCorners, cameraForUI, out screenRect);
    }

    private static bool CornersToScreenRect(Vector3[] worldCorners,
                                            Camera camera, out Rect result)
    {
        float minX = float.PositiveInfinity, minY = float.PositiveInfinity;
        float maxX = float.NegativeInfinity, maxY = float.NegativeInfinity;
        for (int i = 0; i < worldCorners.Length; i++)
        {
            Vector3 position = RectTransformUtility.WorldToScreenPoint(camera, worldCorners[i]);
            if (float.IsNaN(position.x) || float.IsNaN(position.y))
            {
                result = default(Rect);
                return false;
            }
            minX = Mathf.Min(minX, position.x);
            minY = Mathf.Min(minY, position.y);
            maxX = Mathf.Max(maxX, position.x);
            maxY = Mathf.Max(maxY, position.y);
        }
        result = Rect.MinMaxRect(minX, minY, maxX, maxY);
        return true;
    }

    private bool IsValidPromptPosition(Rect box)
    {
        float edge = screenMarginPixels;
        if (box.xMin < edge || box.xMax > Screen.width - edge ||
            box.yMin < edge || box.yMax > Screen.height - edge)
            return false;
        for (int i = 0; i < avoidanceRects.Count; i++)
        {
            Rect obstacle = avoidanceRects[i];
            obstacle.xMin -= textGapPixels;
            obstacle.xMax += textGapPixels;
            obstacle.yMin -= textGapPixels;
            obstacle.yMax += textGapPixels;
            if (box.Overlaps(obstacle)) return false;
        }
        return true;
    }

    private void HidePrompt()
    {
        if (promptPositionRect != null)
            promptPositionRect.gameObject.SetActive(false);
        wasShowing = false;
    }

    private void OnDisable()
    {
        HidePrompt();
    }

    private void OnDestroy()
    {
        if (ownsPrompt && generatedCanvas != null)
            Destroy(generatedCanvas.gameObject);
    }

    [ContextMenu("診断 / 近くのNPCの出血・治療必要判定を表示")]
    public void DiagnoseNearbyNpcTreatmentNeed()
    {
        Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position,
                                                         interactionDistance);
        var visited = new System.Collections.Generic.HashSet<int>();
        int count = 0;
        foreach (Collider2D hit in hits)
        {
            if (hit == null) continue;
            RescuePersonSymptoms2D npc = ResolveNpcFromCollider(hit);
            if (npc == null || !visited.Add(npc.GetInstanceID())) continue;
            RescuePersonInfo2D info = FindNpcDetailInfo(npc);
            bool layerOk = (npcLayers.value & (1 << hit.gameObject.layer)) != 0;
            Debug.Log("[MedicalKitNPC][出血確認] NPC=" + PathOf(npc.transform) +
                      " / ColliderLayer=" + LayerMask.LayerToName(hit.gameObject.layer) +
                      " / LayerOK=" + layerOk +
                      " / Active=" + npc.ActiveSymptoms +
                      " / Stabilized=" + npc.StabilizedSymptoms +
                      " / NeedsFirstAid=" + NeedsFirstAid(npc) +
                      " / 詳細ConditionLabel=" + (info != null ? Convert.ToString(info.ConditionLabel) : "なし") +
                      " / NPCDead=" + npc.IsDead +
                      " / InventoryVisible=" + IsActualInventoryVisible(), npc);
            count++;
        }
        if (count == 0)
            Debug.LogWarning("[MedicalKitNPC][出血確認] 範囲内にRescuePersonSymptoms2Dを持つNPCがいません。" +
                             " Npc LayersとInteraction DistanceおよびCollider2Dを確認。", this);
    }

    [ContextMenu("診断 / 近くのColliderとLayerをConsoleに表示")]
    public void PrintNearbyColliderDiagnostics()
    {
        ScanNearbyNpc();
        StringBuilder sb = new StringBuilder();
        sb.Append("[MedicalKitNPC][Collider診断] Player=").Append(name)
          .Append(" / PlayerPosition=").Append(transform.position)
          .Append(" / Key=").Append(interactKey)
          .Append(" / Radius=").Append(interactionDistance)
          .Append(" / LayerMask=").Append(FormatMask(npcLayers))
          .Append(" / FilteredHits=").Append(lastPhysicsCount)
          .Append(" / ValidNPC=").Append(lastEligibleCount)
          .Append(" / Selected=").Append(nearestNpc != null ? nearestNpc.name : "なし")
          .Append(" / BlockReason=").Append(GetBlockingReason() ?? "なし")
          .Append(" / Camera=").Append(worldCamera != null ? worldCamera.name : "NULL")
          .Append(" / Prompt=").Append(interactionPromptText != null ? interactionPromptText.name : "NULL")
          .Append("\n");

        Collider2D[] all = Physics2D.OverlapCircleAll(transform.position, interactionDistance);
        sb.Append("AllLayersHits=").Append(all.Length).Append(" （最大15件表示）\n");
        for (int i = 0; i < all.Length && i < 15; i++)
        {
            Collider2D hit = all[i];
            if (hit == null) continue;
            bool layerAllowed = (npcLayers.value & (1 << hit.gameObject.layer)) != 0;
            RescuePersonSymptoms2D npc = ResolveNpcFromCollider(hit);
            sb.Append(" - ").Append(PathOf(hit.transform))
              .Append(" / Collider=").Append(hit.GetType().Name)
              .Append(" / Layer=").Append(LayerMask.LayerToName(hit.gameObject.layer))
              .Append(" / LayerOK=").Append(layerAllowed)
              .Append(" / Symptoms=").Append(npc != null ? npc.name : "なし")
              .Append(" / Dead=").Append(npc != null && npc.IsDead)
              .Append("\n");
        }
        if (all.Length == 0)
            sb.Append("近くに有効なCollider2Dがありません。NPCのCollider2Dがenabledか確認してください。\n");

        RescuePersonSymptoms2D[] npcs = FindObjectsByType<RescuePersonSymptoms2D>(FindObjectsSortMode.None);
        sb.Append("Sceneで有効なRescuePersonSymptoms2D数=").Append(npcs.Length);
        if (npcs.Length > 0)
            sb.Append(" / 最初のNPC=").Append(PathOf(npcs[0].transform));
        Debug.Log(sb.ToString(), this);
    }

    [ContextMenu("診断 / Tキーと案内状態をConsoleに表示")]
    private void PrintDiagnostics()
    {
        Debug.Log("[MedicalKitNPC][状態診断] Key=" + interactKey +
                  " / UI=" + (medicalKitUI != null ? medicalKitUI.name : "NULL") +
                  " / Mask=" + FormatMask(npcLayers) +
                  " / Radius=" + interactionDistance +
                  " / Nearest=" + (nearestNpc != null ? nearestNpc.name : "なし") +
                  " / Prompt=" + (interactionPromptText != null ? interactionPromptText.name : "NULL") +
                  " / PromptActive=" + (promptPositionRect != null && promptPositionRect.gameObject.activeInHierarchy) +
                  " / Camera=" + (worldCamera != null ? worldCamera.name : "NULL") +
                  " / Font=" + (interactionPromptText != null && interactionPromptText.font != null ? interactionPromptText.font.name : "NULL") +
                  " / BlockReason=" + (GetBlockingReason() ?? "なし"), this);
    }

    /// <summary>
    /// 表示されない間だけ、既定5秒間隔で原因を自動表示。
    /// Tキーを押した時はクールダウンを無視して即診断する。
    /// </summary>
    private void MaybeAutoDiagnose(string reason, bool force)
    {
        if (!autoDiagnoseMissingPrompt && !force) return;
        if (!force && Time.unscaledTime < nextAutomaticDiagnosticTime) return;
        nextAutomaticDiagnosticTime = Time.unscaledTime + Mathf.Max(1f, autoDiagnosticInterval);
        PrintFullDiagnostics(reason);
    }

    private string GetPromptVisibilityIssue()
    {
        if (interactionPromptText == null) return "Interaction Prompt TextがNULL";
        if (promptPositionRect == null) return "Prompt用RectTransformがNULL";
        if (!promptPositionRect.gameObject.activeSelf) return "表示Root.activeSelf=false";
        if (!promptPositionRect.gameObject.activeInHierarchy)
            return "表示Root.activeInHierarchy=false（親Canvas/親Panelが非表示の可能性）";
        if (!interactionPromptText.enabled) return "TMP_Text.enabled=false";
        if (!interactionPromptText.gameObject.activeInHierarchy)
            return "TMP_Text.activeInHierarchy=false（Textまたは親が非表示）";
        if (interactionPromptText.color.a < 0.01f) return "TMP_Text.color alphaが0";
        Canvas parentCanvas = interactionPromptText.GetComponentInParent<Canvas>();
        if (parentCanvas == null) return "Canvasが見つかりません";
        if (!parentCanvas.enabled) return "親Canvas.enabled=false";
        CanvasGroup[] groups = interactionPromptText.GetComponentsInParent<CanvasGroup>(true);
        foreach (CanvasGroup group in groups)
            if (group != null && group.alpha < 0.01f)
                return "親CanvasGroup.alpha=0: " + PathOf(group.transform);
        if (interactionPromptText.font == null) return "TMP Font AssetがNULL";
        if (!interactionPromptText.font.HasCharacter('治', true, false))
            return "日本語フォントに「治」がありません（□表示の可能性）";
        if (worldCamera == null) return "World CameraがNULL";
        if (nearestNpc != null)
        {
            Vector3 viewport = worldCamera.WorldToViewportPoint(nearestNpc.transform.position + promptWorldOffset);
            if (viewport.z <= 0f) return "NPCがCameraより後方にあります";
            if (viewport.x < 0f || viewport.x > 1f || viewport.y < 0f || viewport.y > 1f)
                return "NPCの案内位置が画面外（Viewport=" + viewport.ToString("F2") + "）";
        }
        return null;
    }

    [ContextMenu("診断 / 全項目（Layer・Collider・Canvas・Font）をConsoleに表示")]
    public void DiagnoseEverything()
    {
        ResolveReferences();
        EnsurePrompt();
        ScanNearbyNpc();
        PrintFullDiagnostics("Inspectorから手動実行");
    }

    private void PrintFullDiagnostics(string reason)
    {
        StringBuilder sb = new StringBuilder(2500);
        sb.AppendLine("[MedicalKitNPC][詳細原因診断] " + reason);
        sb.Append("[1: Player] Path=").Append(PathOf(transform))
          .Append(" / Component.enabled=").Append(enabled)
          .Append(" / ActiveSelf=").Append(gameObject.activeSelf)
          .Append(" / ActiveInHierarchy=").Append(gameObject.activeInHierarchy)
          .Append(" / WorldPos=").Append(transform.position)
          .Append(" / PlayerDead=").Append(playerHealth != null && playerHealth.IsDead)
          .Append(" / BlockReason=").Append(GetBlockingReason() ?? "なし").AppendLine();

        sb.Append("[2: Physics] LayerMask=").Append(FormatMask(npcLayers))
          .Append(" / MaskValue=").Append(npcLayers.value)
          .Append(" / Radius=").Append(interactionDistance.ToString("F2"))
          .Append(" / FilteredHits=").Append(lastPhysicsCount)
          .Append(" / ValidNPC=").Append(lastEligibleCount)
          .Append(" / RequireInFront=").Append(requireNpcInFront)
          .Append(" / Selected=").Append(nearestNpc != null ? PathOf(nearestNpc.transform) : "なし")
          .AppendLine();

        if (inspectAllLayersAndNpcs)
        {
            // フィルタなしの物理検索で「ColliderはあるがLayerが違う」を判別。
            Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, interactionDistance);
            sb.Append("[3: All-Layer Overlap] Hits=").Append(hits.Length).AppendLine();
            int printed = 0;
            for (int i = 0; i < hits.Length && printed < 16; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null) continue;
                RescuePersonSymptoms2D npc = ResolveNpcFromCollider(hit);
                sb.Append("  - ").Append(PathOf(hit.transform))
                  .Append(" | Collider=").Append(hit.GetType().Name)
                  .Append(" | Layer=").Append(LayerMask.LayerToName(hit.gameObject.layer))
                  .Append(" | LayerOK=").Append((npcLayers.value & (1 << hit.gameObject.layer)) != 0)
                  .Append(" | NPCComponent=").Append(npc != null ? npc.name : "なし")
                  .Append(" | NPCdead=").Append(npc != null && npc.IsDead)
                  .AppendLine();
                printed++;
            }
            if (hits.Length > 16) sb.AppendLine("  ...16件まで表示");

            RescuePersonSymptoms2D[] candidates = FindObjectsByType<RescuePersonSymptoms2D>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            sb.Append("[4: Scene NPC Components] Count=").Append(candidates.Length).AppendLine();
            for (int i = 0; i < candidates.Length && i < 8; i++)
            {
                RescuePersonSymptoms2D npc = candidates[i];
                if (npc == null) continue;
                Collider2D[] parts = npc.GetComponentsInChildren<Collider2D>(true);
                sb.Append("  - ").Append(PathOf(npc.transform))
                  .Append(" | NPCenabled=").Append(npc.enabled)
                  .Append(" | NPCactive=").Append(npc.gameObject.activeInHierarchy)
                  .Append(" | IsDead=").Append(npc.IsDead)
                  .Append(" | Distance=").Append(Vector2.Distance(transform.position, npc.transform.position).ToString("F2"))
                  .Append(" | ChildColliders=").Append(parts.Length).AppendLine();
                for (int j = 0; j < parts.Length && j < 3; j++)
                {
                    Collider2D c = parts[j];
                    if (c == null) continue;
                    sb.Append("      Collider=").Append(PathOf(c.transform))
                      .Append(" | Enabled=").Append(c.enabled)
                      .Append(" | Active=").Append(c.gameObject.activeInHierarchy)
                      .Append(" | Layer=").Append(LayerMask.LayerToName(c.gameObject.layer))
                      .Append(" | LayerOK=").Append((npcLayers.value & (1 << c.gameObject.layer)) != 0)
                      .Append(" | ClosestDistance=").Append(Vector2.Distance(transform.position, c.ClosestPoint(transform.position)).ToString("F2"))
                      .AppendLine();
                }
            }
            if (candidates.Length == 0)
                sb.AppendLine("  ※SceneにRescuePersonSymptoms2Dがありません。救出NPCのPrefabへの追加を確認してください。");
        }

        sb.Append("[5: Inventory表示制御] HideWhenOpen=").Append(hidePromptBehindInventory)
          .Append(" | TabPanel=").Append(actualTabInventoryPanel != null ? PathOf(actualTabInventoryPanel.transform) : "未検出")
          .Append(" | TabPanelActive=").Append(actualTabInventoryPanel != null && actualTabInventoryPanel.activeInHierarchy)
          .Append(" | MainRoot(判定には使わない)=").Append(medicalKitUI != null && medicalKitUI.DiagnosticMainInventoryPanel != null ? PathOf(medicalKitUI.DiagnosticMainInventoryPanel.transform) : "NULL")
          .Append(" | CanvasOrder=").Append(generatedCanvas != null ? generatedCanvas.sortingOrder : promptSortingOrder)
          .AppendLine();
        sb.Append("[6: UI] MedicalKitUI=").Append(medicalKitUI != null ? PathOf(medicalKitUI.transform) : "NULL")
          .Append(" | PanelOpen=").Append(medicalKitUI != null && medicalKitUI.IsMedicalPanelOpen)
          .Append(" | WorldCamera=").Append(worldCamera != null ? PathOf(worldCamera.transform) : "NULL")
          .Append(" | CameraEnabled=").Append(worldCamera != null && worldCamera.enabled)
          .Append(" | PromptText=").Append(interactionPromptText != null ? PathOf(interactionPromptText.transform) : "NULL")
          .AppendLine();
        if (interactionPromptText != null)
        {
            sb.Append("[6: Text] Content=").Append(interactionPromptText.text)
              .Append(" | Enabled=").Append(interactionPromptText.enabled)
              .Append(" | ActiveSelf=").Append(interactionPromptText.gameObject.activeSelf)
              .Append(" | ActiveInHierarchy=").Append(interactionPromptText.gameObject.activeInHierarchy)
              .Append(" | Font=").Append(interactionPromptText.font != null ? interactionPromptText.font.name : "NULL")
              .Append(" | Has治=").Append(interactionPromptText.font != null && interactionPromptText.font.HasCharacter('治', true, false))
              .Append(" | FontSize=").Append(interactionPromptText.fontSize)
              .Append(" | ColorAlpha=").Append(interactionPromptText.color.a.ToString("F2"))
              .AppendLine();
        }
        if (promptPositionRect != null)
        {
            sb.Append("[7: PromptRect] Path=").Append(PathOf(promptPositionRect.transform))
              .Append(" | ActiveSelf=").Append(promptPositionRect.gameObject.activeSelf)
              .Append(" | ActiveInHierarchy=").Append(promptPositionRect.gameObject.activeInHierarchy)
              .Append(" | Position=").Append(promptPositionRect.anchoredPosition)
              .Append(" | Size=").Append(promptPositionRect.rect.size)
              .Append(" | Generated=").Append(ownsPrompt)
              .AppendLine();
        }
        if (nearestNpc != null && worldCamera != null)
        {
            Vector3 world = nearestNpc.transform.position + promptWorldOffset;
            sb.Append("[8: Projection] World=").Append(world)
              .Append(" | Screen=").Append(worldCamera.WorldToScreenPoint(world))
              .Append(" | Viewport=").Append(worldCamera.WorldToViewportPoint(world)).AppendLine();
        }
        sb.Append("[9: Verdict] PromptIssue=").Append(GetPromptVisibilityIssue() ?? "可視条件は満たしています（他UIの重なり／描画順も確認）").AppendLine();
        Debug.LogWarning(sb.ToString(), this);
    }

    private static string PathOf(Transform transform)
    {
        if (transform == null) return "NULL";
        string result = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            result = transform.name + "/" + result;
        }
        return result;
    }

    private static string FormatMask(LayerMask mask)
    {
        if (mask.value == 0) return "Nothing";
        if (mask.value == -1) return "Everything";
        StringBuilder sb = new StringBuilder();
        for (int layer = 0; layer < 32; layer++)
        {
            if ((mask.value & (1 << layer)) == 0) continue;
            if (sb.Length > 0) sb.Append(", ");
            string layerName = LayerMask.LayerToName(layer);
            sb.Append(!string.IsNullOrEmpty(layerName) ? layerName : "Layer" + layer);
        }
        return sb.ToString();
    }

    private void OnValidate()
    {
        if (interactKey == KeyCode.E) interactKey = KeyCode.T;
        // v2.5で保存された-100は、自動生成Canvasが背面に隠れるため移行します。
        if (promptSortingOrder == -100) promptSortingOrder = 1100;
        promptSortingOrder = Mathf.Clamp(promptSortingOrder, -32768, 32767);
        interactionDistance = Mathf.Max(0.2f, interactionDistance);
        scanInterval = Mathf.Max(0.05f, scanInterval);
        diagnosticLogInterval = Mathf.Max(0.5f, diagnosticLogInterval);
        autoDiagnosticInterval = Mathf.Max(1f, autoDiagnosticInterval);
        promptFontSize = Mathf.Clamp(promptFontSize, 12, 42);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactionDistance);
    }
}
