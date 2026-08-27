using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// プレイヤーがエレベーター内部のTriggerへ入った瞬間に、
/// 現在のインベントリ・装備をGameSessionManagerへ明示的に保存します。
///
/// 目的：
/// ゲーム開始直後などにすぐエレベーターでシーン移動した場合でも、
/// PlayerInventorySessionBridge.OnDisableだけに保存を任せず、
/// シーン遷移より前に確実にInventoryをSessionへ同期します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class ElevatorInventoryCaptureZone2D : MonoBehaviour
{
    [Header("プレイヤー判定")]
    [SerializeField] private string playerTag = "Player";

    [Tooltip("ONなら、一度エレベーターから完全に出て再び入った時にも再保存します。")]
    [SerializeField] private bool captureAgainAfterExit = true;

    [Header("保存方法")]
    [Tooltip("PlayerにPlayerInventorySessionBridgeがある場合は、まずBridge経由で保存します。通常はON推奨です。")]
    [SerializeField] private bool preferInventorySessionBridge = true;

    [Tooltip("Bridgeが見つからない場合、InventoryControllerとEquipmentControllerを探してGameSessionManagerへ直接保存します。通常はON推奨です。")]
    [SerializeField] private bool useDirectCaptureFallback = true;

    [Header("診断ログ")]
    [Tooltip("エレベーター侵入時の保存結果をConsoleへ表示します。")]
    [SerializeField] private bool showDebugLogs = true;

    private Collider2D captureTrigger;

    private readonly HashSet<Collider2D> playerCollidersInside =
        new HashSet<Collider2D>();

    private bool hasCapturedThisEntry;

    private void Awake()
    {
        FindReferences();
    }

    private void Reset()
    {
        FindReferences();

        if (captureTrigger != null)
        {
            captureTrigger.isTrigger = true;
        }
    }

    private void OnEnable()
    {
        FindReferences();
        playerCollidersInside.Clear();
        hasCapturedThisEntry = false;
    }

    private void OnDisable()
    {
        playerCollidersInside.Clear();
        hasCapturedThisEntry = false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!TryGetPlayerRoot(other, out Transform playerRoot))
        {
            return;
        }

        bool wasOutside = playerCollidersInside.Count == 0;
        playerCollidersInside.Add(other);

        if (!wasOutside || hasCapturedThisEntry)
        {
            return;
        }

        bool captured = CapturePlayerInventory(playerRoot);

        if (captured)
        {
            hasCapturedThisEntry = true;

            Log(
                $"保存成功: Player={playerRoot.name} / " +
                $"Zone={name} / InsideColliders={playerCollidersInside.Count}"
            );
        }
        else
        {
            LogWarning(
                $"保存失敗: Player={playerRoot.name} / Zone={name}。"
            );
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other == null)
        {
            return;
        }

        playerCollidersInside.Remove(other);

        if (playerCollidersInside.Count > 0)
        {
            return;
        }

        if (captureAgainAfterExit)
        {
            hasCapturedThisEntry = false;
            Log("Playerがエレベーターから完全に出たため、次回入室時に再保存できます。");
        }
    }

    [ContextMenu("Capture Player Inventory Now")]
    public void CapturePlayerInventoryNow()
    {
        PlayerMove playerMove =
            FindAnyObjectByType<PlayerMove>(
                FindObjectsInactive.Include
            );

        Transform playerRoot =
            playerMove != null
                ? playerMove.transform.root
                : null;

        if (playerRoot == null)
        {
            GameObject playerObject = null;

            try
            {
                playerObject =
                    GameObject.FindGameObjectWithTag(playerTag);
            }
            catch (UnityException)
            {
            }

            if (playerObject != null)
            {
                playerRoot = playerObject.transform.root;
            }
        }

        if (playerRoot == null)
        {
            LogWarning("Playerが見つからないため手動保存できません。");
            return;
        }

        CapturePlayerInventory(playerRoot);
    }

    private bool CapturePlayerInventory(Transform playerRoot)
    {
        if (playerRoot == null)
        {
            return false;
        }

        if (preferInventorySessionBridge)
        {
            PlayerInventorySessionBridge bridge =
                playerRoot.GetComponentInChildren<PlayerInventorySessionBridge>(true);

            if (bridge != null)
            {
                bool captured = bridge.CaptureToSession();

                if (captured)
                {
                    Log(
                        "PlayerInventorySessionBridge.CaptureToSession() " +
                        "で保存しました。"
                    );

                    return true;
                }

                LogWarning(
                    "PlayerInventorySessionBridgeは見つかりましたが、" +
                    "CaptureToSession()が失敗しました。"
                );
            }
        }

        if (!useDirectCaptureFallback)
        {
            return false;
        }

        InventoryController inventoryController =
            playerRoot.GetComponentInChildren<InventoryController>(true);

        EquipmentController equipmentController =
            playerRoot.GetComponentInChildren<EquipmentController>(true);

        if (inventoryController == null)
        {
            LogWarning(
                "Player内にInventoryControllerが見つかりません。"
            );

            return false;
        }

        GameSessionManager session =
            GameSessionManager.Instance;

        if (session == null)
        {
            session =
                FindAnyObjectByType<GameSessionManager>(
                    FindObjectsInactive.Include
                );
        }

        if (session == null)
        {
            LogWarning(
                "GameSessionManagerが見つかりません。"
            );

            return false;
        }

        bool result = session.CapturePlayerInventory(
            inventoryController,
            equipmentController
        );

        if (result)
        {
            int itemCount =
                inventoryController.Grid != null
                    ? inventoryController.Grid.Items.Count
                    : 0;

            Log(
                "GameSessionManagerへ直接保存しました。" +
                $"通常Inventory={itemCount}件 / " +
                $"Equipment={(equipmentController != null ? "あり" : "なし")}"
            );
        }

        return result;
    }

    private bool TryGetPlayerRoot(
        Collider2D other,
        out Transform playerRoot)
    {
        playerRoot = null;

        if (other == null)
        {
            return false;
        }

        Transform root = other.transform.root;

        if (IsPlayerTag(other.gameObject) ||
            (root != null && IsPlayerTag(root.gameObject)))
        {
            playerRoot = root != null
                ? root
                : other.transform;

            return true;
        }

        PlayerMove playerMove =
            other.GetComponentInParent<PlayerMove>();

        if (playerMove == null)
        {
            return false;
        }

        playerRoot = playerMove.transform.root;
        return true;
    }

    private bool IsPlayerTag(GameObject target)
    {
        if (target == null ||
            string.IsNullOrWhiteSpace(playerTag))
        {
            return false;
        }

        try
        {
            return target.CompareTag(playerTag);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private void FindReferences()
    {
        if (captureTrigger == null)
        {
            captureTrigger = GetComponent<Collider2D>();
        }

        if (captureTrigger != null &&
            !captureTrigger.isTrigger)
        {
            LogWarning(
                "このCollider2DはIs TriggerをONにしてください。"
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
            $"[ElevatorInventoryCaptureZone2D] {message}",
            this
        );
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[ElevatorInventoryCaptureZone2D] {message}",
            this
        );
    }

    private void OnValidate()
    {
        playerTag = playerTag?.Trim() ?? string.Empty;
        FindReferences();
    }
}
