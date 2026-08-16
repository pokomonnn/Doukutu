using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 洞窟内NPCへ近づいてEキーでTownConversationDataの会話を開始します。
/// 会話内容・選択肢・ミッション処理は既存TownConversationControllerをそのまま使用し、
/// CaveDialogueAnchorFollowerへNPC頭上のDialogueAnchorを渡します。
/// </summary>
[DisallowMultipleComponent]
public class CaveConversationNPC : MonoBehaviour
{
    [Header("既存の会話システム")]
    [Tooltip("洞窟Canvasに置いた既存TownConversationControllerです。未設定なら自動検索します。")]
    [SerializeField] private TownConversationController conversationController;

    [Tooltip("町と共通で使えるTownConversationDataです。")]
    [SerializeField] private TownConversationData conversationData;

    [Header("NPC頭上への表示")]
    [Tooltip("Dialogue Panelに付けたCaveDialogueAnchorFollowerです。未設定なら自動検索します。")]
    [SerializeField] private CaveDialogueAnchorFollower anchorFollower;

    [Tooltip("NPCの頭上に作成した空GameObjectを設定します。未設定ならこのNPC自身のTransformを使います。")]
    [SerializeField] private Transform dialogueAnchor;

    [Header("話しかける操作")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;

    [Tooltip("Player判定に使うTagです。")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("プレイヤーが範囲内の時だけEキーで会話を開始します。")]
    [SerializeField] private bool requirePlayerInTrigger = true;

    [Tooltip("会話中にもう一度Eを押しても再Openしないようにします。")]
    [SerializeField] private bool preventReopenWhileConversationOpen = true;

    [Header("任意：話す表示")]
    [Tooltip("NPC頭上などに置いたTMP_Text。不要なら空欄でOKです。")]
    [SerializeField] private TMP_Text interactionPromptText;

    [SerializeField] private string interactionPrompt = "E：話す";

    [Header("商人を洞窟に置く場合のみ")]
    [Tooltip("Conversation TypeがMerchantの場合だけ設定します。通常NPCなら空欄でOKです。")]
    [SerializeField] private MerchantStockInventory merchantStockInventory;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private readonly HashSet<Collider2D> playerColliders =
        new HashSet<Collider2D>();

    public bool IsPlayerInRange =>
        !requirePlayerInTrigger || playerColliders.Count > 0;

    public TownConversationData ConversationData => conversationData;
    public Transform DialogueAnchor => dialogueAnchor != null
        ? dialogueAnchor
        : transform;

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

    private void OnDisable()
    {
        playerColliders.Clear();
        RefreshPrompt();
    }

    private void Update()
    {
        if (!IsPlayerInRange)
        {
            return;
        }

        if (preventReopenWhileConversationOpen &&
            conversationController != null &&
            conversationController.IsOpen)
        {
            RefreshPrompt();
            return;
        }

        if (Input.GetKeyDown(interactionKey))
        {
            OpenConversation();
        }
    }

    /// <summary>
    /// 他のInteractシステムやButtonから呼びたい場合にも使用できます。
    /// </summary>
    public void OpenConversation()
    {
        FindReferences();

        if (requirePlayerInTrigger && !IsPlayerInRange)
        {
            Log("プレイヤーが会話範囲外なので開始しません。");
            return;
        }

        if (conversationController == null)
        {
            LogWarning(
                "TownConversationControllerが見つかりません。" +
                "洞窟Canvasに既存TownConversationControllerを配置してください。"
            );
            return;
        }

        if (conversationData == null)
        {
            LogWarning("Conversation Dataが未設定です。");
            return;
        }

        if (anchorFollower == null)
        {
            LogWarning(
                "CaveDialogueAnchorFollowerが見つかりません。" +
                "Dialogue Panelへ追加してください。"
            );
            return;
        }

        SetupMerchantContextIfNeeded();

        // 会話を開く前に追従先を設定しておくことで、
        // Panelが表示された最初のフレームからNPC頭上へ出せます。
        anchorFollower.SetTarget(DialogueAnchor);
        conversationController.OpenConversation(conversationData);
        anchorFollower.UpdatePositionNow();
        RefreshPrompt();

        Log(
            $"会話開始: Resident={conversationData.ResidentName} / " +
            $"Anchor={DialogueAnchor.name}"
        );
    }

    private void SetupMerchantContextIfNeeded()
    {
        if (conversationData == null ||
            conversationData.ConversationType !=
            TownConversationType.Merchant)
        {
            MerchantShopConversationContext.Clear();
            return;
        }

        if (merchantStockInventory == null)
        {
            merchantStockInventory =
                GetComponentInParent<MerchantStockInventory>();
        }

        if (merchantStockInventory == null)
        {
            merchantStockInventory =
                GetComponentInChildren<MerchantStockInventory>(true);
        }

        MerchantShopConversationContext.SetMerchant(
            conversationData,
            merchantStockInventory
        );

        if (merchantStockInventory == null)
        {
            LogWarning(
                "Merchant会話ですがMerchant Stock Inventoryが未設定です。"
            );
        }
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
        if (other == null)
        {
            return;
        }

        playerColliders.Remove(other);
        RefreshPrompt();
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            if (other.CompareTag(playerTag))
            {
                return true;
            }

            Transform root = other.transform.root;

            if (root != null && root.CompareTag(playerTag))
            {
                return true;
            }
        }

        return other.GetComponentInParent<PlayerMove>() != null;
    }

    private void RefreshPrompt()
    {
        if (interactionPromptText == null)
        {
            return;
        }

        bool conversationIsOpen =
            conversationController != null &&
            conversationController.IsOpen;

        bool shouldShow =
            IsPlayerInRange &&
            (!preventReopenWhileConversationOpen ||
             !conversationIsOpen);

        interactionPromptText.text = interactionPrompt ?? string.Empty;
        interactionPromptText.gameObject.SetActive(shouldShow);
    }

    private void FindReferences()
    {
        if (conversationController == null)
        {
            conversationController =
                FindAnyObjectByType<TownConversationController>(
                    FindObjectsInactive.Include
                );
        }

        if (anchorFollower == null)
        {
            anchorFollower =
                FindAnyObjectByType<CaveDialogueAnchorFollower>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"[CaveConversationNPC: {name}] {message}",
            this
        );
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[CaveConversationNPC: {name}] {message}",
            this
        );
    }

    private void OnValidate()
    {
        playerTag = playerTag?.Trim() ?? string.Empty;
        interactionPrompt = interactionPrompt ?? string.Empty;
    }
}
