using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 納品ミッション用の納品地点です。
/// PlayerがTrigger内に入り、指定キーを押すとMissionManager2Dへ納品を要求します。
/// 必要アイテムはInventoryControllerから実際に消費され、必要数に達するとミッション達成になります。
/// </summary>
[DisallowMultipleComponent]
public class MissionDeliveryPoint2D : MonoBehaviour
{
    [Header("対象ミッション")]
    [Tooltip("Objective TypeをDeliver ItemにしたMissionDefinition2Dを設定します")]
    [SerializeField] private MissionDefinition2D mission;

    [Tooltip("未設定なら同じシーンのMissionManager2Dを自動取得します")]
    [SerializeField] private MissionManager2D missionManager;

    [Header("操作")]
    [SerializeField] private KeyCode deliveryKey = KeyCode.E;

    [Tooltip("OFFなら残り必要数を全部持っている時だけ納品します。ONなら持っている個数だけ部分納品できます")]
    [SerializeField] private bool allowPartialDelivery;

    [Header("プレイヤー判定")]
    [SerializeField] private string playerTag = "Player";

    [Header("表示")]
    [Tooltip("納品地点の子などに置いたTextMeshProを設定します。不要なら空欄でも動作します")]
    [SerializeField] private TMP_Text promptText;

    [SerializeField] private string deliveryLabel = "納品";
    [SerializeField] private string completedLabel = "納品完了";

    [Tooltip("納品結果を一時表示するTextです。Prompt Textと同じTextを設定してもOKです")]
    [SerializeField] private TMP_Text resultText;

    [SerializeField, Min(0f)] private float resultVisibleDuration = 2.5f;

    [Header("セッション同期")]
    [Tooltip("納品直後にMissionSessionBridgeへ保存し、同じシーン内の会話システムからも達成状態を確認しやすくします")]
    [SerializeField] private bool captureMissionSessionAfterDelivery = true;

    [SerializeField] private MissionSessionBridge missionSessionBridge;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private readonly HashSet<Collider2D> playerColliders =
        new HashSet<Collider2D>();

    private Coroutine resultCoroutine;

    public MissionDefinition2D Mission => mission;
    public bool IsPlayerInRange => playerColliders.Count > 0;

    private void Awake()
    {
        FindReferences();
        RefreshPrompt();
    }

    private void OnEnable()
    {
        FindReferences();
        RefreshPrompt();
    }

    private void Update()
    {
        RefreshPrompt();

        if (!IsPlayerInRange || !CanAttemptDelivery())
        {
            return;
        }

        if (Input.GetKeyDown(deliveryKey))
        {
            TryDeliver();
        }
    }

    public bool TryDeliver()
    {
        FindReferences();

        if (missionManager == null)
        {
            ShowResult("MissionManager2Dが見つかりません。", true);
            return false;
        }

        if (mission == null)
        {
            ShowResult("納品対象のMission Definitionが未設定です。", true);
            return false;
        }

        bool delivered = missionManager.TryDeliverMissionItems(
            mission,
            allowPartialDelivery,
            out int deliveredAmount,
            out string resultMessage
        );

        ShowResult(resultMessage, !delivered);

        if (!delivered)
        {
            RefreshPrompt();
            return false;
        }

        if (captureMissionSessionAfterDelivery)
        {
            CaptureMissionSession();
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[MissionDeliveryPoint2D: {name}] " +
                $"納品成功 Mission={mission.DisplayName} / " +
                $"Delivered={deliveredAmount}",
                this
            );
        }

        RefreshPrompt();
        return true;
    }

    private bool CanAttemptDelivery()
    {
        if (missionManager == null || mission == null)
        {
            return false;
        }

        if (mission.ObjectiveType != MissionObjectiveType2D.DeliverItem)
        {
            return false;
        }

        int missionIndex = missionManager.FindMissionIndex(mission);

        return missionIndex >= 0 &&
            missionManager.GetMissionState(missionIndex) ==
            MissionProgressState2D.InProgress;
    }

    private void RefreshPrompt()
    {
        if (promptText == null)
        {
            return;
        }

        bool shouldShow =
            IsPlayerInRange &&
            missionManager != null &&
            mission != null &&
            mission.ObjectiveType == MissionObjectiveType2D.DeliverItem;

        if (!shouldShow)
        {
            promptText.enabled = false;
            return;
        }

        int missionIndex = missionManager.FindMissionIndex(mission);

        if (missionIndex < 0)
        {
            promptText.enabled = false;
            return;
        }

        MissionProgressState2D state =
            missionManager.GetMissionState(missionIndex);

        if (state == MissionProgressState2D.Completed)
        {
            promptText.text = completedLabel;
            promptText.enabled = true;
            return;
        }

        if (state != MissionProgressState2D.InProgress)
        {
            promptText.enabled = false;
            return;
        }

        int progress = missionManager.GetMissionProgress(missionIndex);
        int required = missionManager.GetMissionRequiredAmount(missionIndex);
        int remaining = Mathf.Max(0, required - progress);

        string itemName = mission.RequiredItem != null
            ? mission.RequiredItem.DisplayName
            : "アイテム";

        promptText.text =
            $"{deliveryKey}:{deliveryLabel}  {itemName} {remaining}個";

        promptText.enabled = true;
    }

    private void ShowResult(string message, bool warning)
    {
        if (showDebugLogs && !string.IsNullOrWhiteSpace(message))
        {
            if (warning)
            {
                Debug.LogWarning(
                    $"[MissionDeliveryPoint2D: {name}] {message}",
                    this
                );
            }
            else
            {
                Debug.Log(
                    $"[MissionDeliveryPoint2D: {name}] {message}",
                    this
                );
            }
        }

        if (resultText == null)
        {
            return;
        }

        if (resultCoroutine != null)
        {
            StopCoroutine(resultCoroutine);
        }

        resultCoroutine = StartCoroutine(
            ShowResultRoutine(message)
        );
    }

    private IEnumerator ShowResultRoutine(string message)
    {
        resultText.text = message ?? string.Empty;
        resultText.enabled = !string.IsNullOrWhiteSpace(message);

        if (resultVisibleDuration > 0f)
        {
            yield return new WaitForSeconds(resultVisibleDuration);
        }

        if (resultText != promptText)
        {
            resultText.text = string.Empty;
            resultText.enabled = false;
        }

        resultCoroutine = null;
        RefreshPrompt();
    }

    private void CaptureMissionSession()
    {
        if (missionSessionBridge == null)
        {
            missionSessionBridge =
                FindAnyObjectByType<MissionSessionBridge>(
                    FindObjectsInactive.Include
                );
        }

        missionSessionBridge?.CaptureToSession();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!IsPlayerCollider(other))
        {
            return;
        }

        playerColliders.Add(other);
        RefreshPrompt();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        playerColliders.Remove(other);
        RefreshPrompt();
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            return true;
        }

        if (other.transform.root != null &&
            other.transform.root.CompareTag(playerTag))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerMove>() != null;
    }

    private void FindReferences()
    {
        if (missionManager == null)
        {
            missionManager =
                FindAnyObjectByType<MissionManager2D>(
                    FindObjectsInactive.Include
                );
        }

        if (captureMissionSessionAfterDelivery &&
            missionSessionBridge == null)
        {
            missionSessionBridge =
                FindAnyObjectByType<MissionSessionBridge>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void OnValidate()
    {
        resultVisibleDuration = Mathf.Max(
            0f,
            resultVisibleDuration
        );

        playerTag = playerTag?.Trim() ?? string.Empty;
        deliveryLabel = string.IsNullOrWhiteSpace(deliveryLabel)
            ? "納品"
            : deliveryLabel.Trim();

        completedLabel = string.IsNullOrWhiteSpace(completedLabel)
            ? "納品完了"
            : completedLabel.Trim();
    }
}
