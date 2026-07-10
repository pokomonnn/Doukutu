using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 町の建物やNPCに重ねたButtonから、統一会話を開きます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class TownConversationButton : MonoBehaviour
{
    [Header("会話")]
    [SerializeField] private TownConversationController conversationController;
    [SerializeField] private TownConversationData conversationData;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private Button targetButton;

    private void Awake()
    {
        FindReferences();

        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(OpenConversation);
            targetButton.onClick.AddListener(OpenConversation);
        }
    }

    private void OnDestroy()
    {
        if (targetButton != null)
        {
            targetButton.onClick.RemoveListener(OpenConversation);
        }
    }

    public void OpenConversation()
    {
        FindReferences();

        if (conversationController == null)
        {
            Debug.LogWarning(
                "[TownConversationButton] TownConversationControllerが見つかりません。",
                this
            );
            return;
        }

        if (conversationData == null)
        {
            Debug.LogWarning(
                "[TownConversationButton] Conversation Dataが未設定です。",
                this
            );
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TownConversationButton] 会話開始要求: Data={conversationData.name} / " +
                $"Resident={conversationData.ResidentName} / Controller={conversationController.name} / " +
                $"ControllerActive={conversationController.gameObject.activeInHierarchy}",
                this
            );
        }

        conversationController.OpenConversation(conversationData);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TownConversationButton] 会話開始結果: IsOpen={conversationController.IsOpen} / " +
                $"CurrentBlock={conversationController.CurrentBlockId}",
                this
            );

            if (!conversationController.IsOpen)
            {
                Debug.LogWarning(
                    "[TownConversationButton] ControllerのIsOpenがfalseです。" +
                    "直前のTownConversationController警告を確認してください。",
                    this
                );
            }
        }
    }

    private void FindReferences()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }

        if (conversationController == null)
        {
            conversationController =
                FindAnyObjectByType<TownConversationController>(
                    FindObjectsInactive.Include
                );
        }
    }
}
