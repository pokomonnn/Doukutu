using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// NPC治療専用の3秒プログレス。
/// 治療効果/アイテム消費は完了時の MedicalKitTreatment2D.TryUse のみ。
/// プレイヤー用の使用は従来通り即時。
/// UIを閉じた・移動した・対象から離れた・NPCが死亡/消失した場合は無消費で中断。
/// </summary>
[DisallowMultipleComponent]
public sealed class MedicalKitNPCTreatmentProgress2D : MonoBehaviour
{
    [Header("NPC治療時間")]
    [SerializeField, Min(0.1f)] private float treatmentSeconds = 1f;
    [Tooltip("開始地点からこれ以上動くと処置キャンセル。物理挙動の微小な揺れは許容します。")]
    [SerializeField, Min(0.01f)] private float cancelMovementDistance = 0.22f;
    [Tooltip("NPCとの最大距離。Tキーの探索距離より少し大きめにしてください。")]
    [SerializeField, Min(0.5f)] private float maximumNpcDistance = 2.3f;
    [Tooltip("Time.timeScale=0でInventoryを開くゲームでも3秒ゲージが進むようにする")]
    [SerializeField] private bool useUnscaledTime = true;

    [Header("ゲージUI（指定しなければ自動生成）")]
    [SerializeField] private RectTransform progressRoot;
    [SerializeField] private RectTransform progressFill;
    [SerializeField] private TMP_Text progressLabel;
    [SerializeField] private TMP_FontAsset japaneseFont;
    [SerializeField] private Vector2 panelOffset = Vector2.zero;
    [Header("診断")]
    [SerializeField] private bool showDebugLogs = true;

    private MedicalKitInventoryUI2D ui;
    private MedicalKitInventory2D kit;
    private MedicalKitTreatment2D treatment;
    private ConsumableItemData selectedData;
    private RescuePersonSymptoms2D targetNpc;
    private Transform playerTransform;
    private Vector2 startPosition;
    private float elapsed;
    private Action<bool, bool, string, ConsumableItemData> completion;
    private bool busy;

    public bool IsTreating => busy;
    public float Progress01 => busy ? Mathf.Clamp01(elapsed / Mathf.Max(0.1f, treatmentSeconds)) : 0f;

    public void Configure(MedicalKitInventoryUI2D controller, GameObject kitPanel,
        TMP_FontAsset font)
    {
        ui = controller;
        if (font != null) japaneseFont = font;
        if (progressRoot == null && kitPanel != null)
            CreateProgressUI(kitPanel.transform);
        if (progressLabel != null && japaneseFont != null)
            progressLabel.font = japaneseFont;
        if (!busy && progressRoot != null && progressRoot.gameObject.activeSelf)
            progressRoot.gameObject.SetActive(false);
    }

    public bool Begin(MedicalKitInventory2D inventory, MedicalKitTreatment2D treatmentController,
        RescuePersonSymptoms2D npc, ConsumableItemData data,
        Action<bool, bool, string, ConsumableItemData> onCompleted,
        out string message)
    {
        if (busy)
        {
            message = "現在NPCを治療中です";
            return false;
        }
        if (ui == null || !ui.IsMedicalPanelOpen || !ui.IsTreatingNpc ||
            npc == null || ui.CurrentNpcTarget != npc)
        {
            message = "NPC治療画面が開いていません";
            return false;
        }
        if (inventory == null || treatmentController == null || data == null)
        {
            message = "医療用品または治療スクリプトが見つかりません";
            return false;
        }
        if (!treatmentController.CanTreat(inventory, data, npc, out message))
            return false;

        Transform player = inventory.transform;
        if (player == null || Vector2.Distance(player.position, npc.transform.position) > maximumNpcDistance)
        {
            message = "NPCから離れすぎています";
            return false;
        }
        if (progressRoot == null)
        {
            message = "処置ゲージが作成されていません。MedicalKitPanelの参照を確認してください";
            return false;
        }

        kit = inventory;
        treatment = treatmentController;
        targetNpc = npc;
        selectedData = data;
        playerTransform = player;
        startPosition = player.position;
        elapsed = 0f;
        completion = onCompleted;
        busy = true;
        progressRoot.gameObject.SetActive(true);
        progressRoot.SetAsLastSibling();
        RefreshVisual();
        message = npc.name + "に" + data.DisplayName + "の応急処置を開始（" + treatmentSeconds.ToString("0.#") + "秒）";
        if (showDebugLogs) Debug.Log("[MedicalKitProgress][START] " + message, this);
        return true;
    }

    private void Update()
    {
        if (!busy) return;
        if (ui == null || !ui.isActiveAndEnabled || !ui.IsMedicalPanelOpen ||
            !ui.IsTreatingNpc || ui.CurrentNpcTarget != targetNpc)
        {
            Cancel("Inventoryまたは応急キットを閉じたためキャンセル");
            return;
        }
        if (targetNpc == null || !targetNpc.gameObject.activeInHierarchy || targetNpc.IsDead)
        {
            Cancel("治療対象NPCが死亡、または消失したためキャンセル");
            return;
        }
        if (playerTransform == null ||
            Vector2.Distance(playerTransform.position, startPosition) > cancelMovementDistance)
        {
            Cancel("プレイヤーが動いたため治療を中断");
            return;
        }
        if (Vector2.Distance(playerTransform.position, targetNpc.transform.position) > maximumNpcDistance)
        {
            Cancel("NPCから離れたため治療を中断");
            return;
        }
        if (kit == null || selectedData == null || kit.GetAmount(selectedData) <= 0)
        {
            Cancel("医療用品がなくなったため治療を中断");
            return;
        }
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cancel("Escキーで処置をキャンセル");
            return;
        }

        elapsed += useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        RefreshVisual();
        if (elapsed < Mathf.Max(0.1f, treatmentSeconds)) return;

        // ここで初めて治療と消費を確定。治療直前に状態と在庫を再検証する。
        string result = "治療コンポーネントがなくなりました";
        bool success = treatment != null &&
            treatment.TryUse(kit, selectedData, targetNpc, out result);
        if (showDebugLogs)
            Debug.Log("[MedicalKitProgress][FINISH] success=" + success + " / " + result, this);
        Finish(success, false, result);
    }

    private void RefreshVisual()
    {
        float ratio = Progress01;
        if (progressFill != null)
        {
            Vector2 a = progressFill.anchorMax;
            a.x = ratio;
            progressFill.anchorMax = a;
        }
        if (progressLabel != null)
        {
            string targetName = targetNpc != null ? targetNpc.name : "NPC";
            progressLabel.text = targetName + " を治療中... " + Mathf.CeilToInt(
                Mathf.Max(0f, treatmentSeconds - elapsed)) + "秒";
        }
    }

    public void Cancel(string reason)
    {
        if (!busy) return;
        if (showDebugLogs) Debug.Log("[MedicalKitProgress][CANCEL] " + reason + " / 未消費", this);
        Finish(false, true, reason);
    }

    private void Finish(bool success, bool cancelled, string result)
    {
        if (!busy) return;
        ConsumableItemData data = selectedData;
        Action<bool, bool, string, ConsumableItemData> callback = completion;
        busy = false;
        completion = null;
        targetNpc = null;
        selectedData = null;
        kit = null;
        treatment = null;
        playerTransform = null;
        elapsed = 0f;
        if (progressRoot != null) progressRoot.gameObject.SetActive(false);
        callback?.Invoke(success, cancelled, result, data);
    }

    private void OnDisable()
    {
        Cancel("UIControllerが無効になったため治療中断");
    }

    private void CreateProgressUI(Transform panel)
    {
        GameObject bg = new GameObject("NPCTreatmentProgressOverlay",
            typeof(RectTransform), typeof(Image));
        bg.transform.SetParent(panel, false);
        progressRoot = bg.GetComponent<RectTransform>();
        progressRoot.anchorMin = progressRoot.anchorMax = new Vector2(0.5f, 0.5f);
        progressRoot.pivot = new Vector2(0.5f, 0.5f);
        progressRoot.sizeDelta = new Vector2(330f, 94f);
        progressRoot.anchoredPosition = panelOffset;
        Image bgImage = bg.GetComponent<Image>();
        bgImage.color = new Color(0.07f, 0.11f, 0.14f, 0.97f);
        bgImage.raycastTarget = false;
        Outline border = bg.AddComponent<Outline>();
        border.effectColor = new Color(0.48f, 0.65f, 0.68f, 0.9f);
        border.effectDistance = new Vector2(1f, -1f);

        GameObject title = new GameObject("ProgressLabel", typeof(RectTransform),
            typeof(TextMeshProUGUI));
        title.transform.SetParent(progressRoot, false);
        RectTransform textRect = title.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.offsetMin = new Vector2(12f, -47f);
        textRect.offsetMax = new Vector2(-12f, -12f);
        TextMeshProUGUI label = title.GetComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 17f;
        label.color = Color.white;
        label.raycastTarget = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        if (japaneseFont != null) label.font = japaneseFont;
        progressLabel = label;

        GameObject bar = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(progressRoot, false);
        RectTransform barRect = bar.GetComponent<RectTransform>();
        barRect.anchorMin = new Vector2(0f, 0f);
        barRect.anchorMax = new Vector2(1f, 0f);
        barRect.pivot = new Vector2(0.5f, 0f);
        barRect.offsetMin = new Vector2(14f, 18f);
        barRect.offsetMax = new Vector2(-14f, 41f);
        bar.GetComponent<Image>().color = new Color(0.18f, 0.22f, 0.24f, 1f);
        bar.GetComponent<Image>().raycastTarget = false;

        GameObject fill = new GameObject("ProgressFill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(barRect, false);
        progressFill = fill.GetComponent<RectTransform>();
        progressFill.anchorMin = new Vector2(0f, 0f);
        progressFill.anchorMax = new Vector2(0f, 1f);
        progressFill.offsetMin = Vector2.zero;
        progressFill.offsetMax = Vector2.zero;
        fill.GetComponent<Image>().color = new Color(0.37f, 0.84f, 0.67f, 1f);
        fill.GetComponent<Image>().raycastTarget = false;
        bg.SetActive(false);
    }
}
