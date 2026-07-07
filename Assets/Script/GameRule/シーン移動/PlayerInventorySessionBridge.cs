using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 現在のシーンにあるInventoryController / EquipmentController と
/// GameSessionManager の引き継ぎデータを接続します。
///
/// Playerに付ければ探索シーンを離れる時に保存し、戻った時に復元します。
/// Town_MainのTownPlayerInventoryにも付ければ、同じ持ち物を町のUIで表示できます。
/// </summary>
[DisallowMultipleComponent]
public class PlayerInventorySessionBridge : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら同じObjectまたはシーン内から自動取得します")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [SerializeField] private InventoryController inventoryController;
    [SerializeField] private EquipmentController equipmentController;

    [Header("動作")]
    [Tooltip("Start時に、GameSessionManagerの保存データをこのシーンのインベントリへ復元します")]
    [SerializeField] private bool restoreSessionDataOnStart = true;

    [Tooltip("このObjectが無効化・破棄される時に、現在のインベントリと装備をGameSessionManagerへ保存します")]
    [SerializeField] private bool captureSessionDataOnDisable = true;

    [Tooltip("復元後に装備の見た目・重量を更新します")]
    [SerializeField] private bool refreshPlayerSystemsAfterRestore = true;

    [Header("診断ログ")]
    [Tooltip("オンなら、保存・復元の重要ログをConsoleへ常に表示します。")]
    [SerializeField] private bool alwaysLogSessionTransfer = true;

    [Tooltip("通常の補助ログも表示します。")]
    [SerializeField] private bool showDebugLogs;

    public InventoryController InventoryController => inventoryController;
    public EquipmentController EquipmentController => equipmentController;
    public bool HasValidReferences => gameSessionManager != null && inventoryController != null;

    private bool hasRestoredForThisInstance;
    private bool isApplicationQuitting;

    private void Awake()
    {
        FindReferences();
        LogTransfer(
            $"Awake: Scene={SceneManager.GetActiveScene().name} / Object={name} / " +
            $"GameSessionManager={GetObjectLabel(gameSessionManager)} / " +
            $"InventoryController={GetObjectLabel(inventoryController)} / " +
            $"EquipmentController={GetObjectLabel(equipmentController)}"
        );
    }

    private IEnumerator Start()
    {
        // InventoryControllerなどのAwake/OnEnableが終わった直後に復元する。
        // 1フレーム待つことでTown_Main側のUIや参照探索の順番も安定させます。
        yield return null;

        if (restoreSessionDataOnStart)
        {
            RestoreFromSession();
        }
        else
        {
            LogTransfer("Start: Restore Session Data On Start がOFFのため自動復元しません。");
        }
    }

    private void OnDisable()
    {
        if (captureSessionDataOnDisable && !isApplicationQuitting)
        {
            LogTransfer("OnDisable: シーン破棄または無効化を検出。保存を実行します。");
            CaptureToSession();
        }
        else if (!isApplicationQuitting)
        {
            LogTransfer("OnDisable: Capture Session Data On Disable がOFFのため保存しません。");
        }
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    /// <summary>現在の持ち物をGameSessionManagerへ保存します。</summary>
    public bool CaptureToSession()
    {
        FindReferences();

        if (gameSessionManager == null || inventoryController == null)
        {
            LogTransferWarning(
                "保存失敗: GameSessionManagerまたはInventoryControllerが見つかりません。" +
                $" GameSessionManager={GetObjectLabel(gameSessionManager)} / " +
                $"InventoryController={GetObjectLabel(inventoryController)}"
            );
            return false;
        }

        int itemCount = inventoryController.Grid != null
            ? inventoryController.Grid.Items.Count
            : -1;

        LogTransfer(
            $"保存要求: Scene={SceneManager.GetActiveScene().name} / Object={name} / " +
            $"通常アイテム={itemCount}件"
        );

        bool captured = gameSessionManager.CapturePlayerInventory(
            inventoryController,
            equipmentController
        );

        if (!captured)
        {
            LogTransferWarning("保存要求は失敗しました。");
        }

        return captured;
    }

    /// <summary>GameSessionManagerに保存されている持ち物をこのシーンへ復元します。</summary>
    public bool RestoreFromSession()
    {
        FindReferences();

        if (hasRestoredForThisInstance)
        {
            LogTransfer("復元をスキップ: このBridgeではすでに復元済みです。");
            return false;
        }

        if (gameSessionManager == null)
        {
            LogTransferWarning(
                "復元失敗: GameSessionManagerが見つかりません。" +
                "開始シーンにGameSessionManagerが存在するか、Town_Mainを単体再生していないか確認してください。"
            );
            return false;
        }

        if (inventoryController == null)
        {
            LogTransferWarning(
                "復元失敗: InventoryControllerが見つかりません。" +
                "TownPlayerInventoryまたはPlayer本体へInventoryControllerを付けてください。"
            );
            return false;
        }

        if (!gameSessionManager.HasInventorySessionData)
        {
            LogTransferWarning(
                "復元しません: GameSessionManagerにインベントリ保存データがありません。" +
                "移動元SceneのPlayerInventorySessionBridgeとSceneTransitionButtonの設定を確認してください。"
            );
            return false;
        }

        LogTransfer(
            $"復元要求: Scene={SceneManager.GetActiveScene().name} / Object={name} / " +
            gameSessionManager.GetInventorySessionSummary()
        );

        bool restored = gameSessionManager.RestorePlayerInventory(
            inventoryController,
            equipmentController,
            out string resultMessage
        );

        // 配置に失敗したアイテムがあった場合も、二重に開始アイテムを
        // 入れないよう、このシーンでは復元済みとして扱います。
        hasRestoredForThisInstance = true;

        if (refreshPlayerSystemsAfterRestore)
        {
            RefreshPlayerSystems();
        }

        if (restored)
        {
            LogTransfer($"復元成功: {resultMessage}");
        }
        else
        {
            LogTransferWarning($"復元完了または失敗: {resultMessage}");
        }

        return restored;
    }

    [ContextMenu("Capture Inventory To Game Session")]
    private void CaptureFromContextMenu()
    {
        CaptureToSession();
    }

    [ContextMenu("Restore Inventory From Game Session")]
    private void RestoreFromContextMenu()
    {
        RestoreFromSession();
    }

    [ContextMenu("Log Inventory Session Bridge Diagnostics")]
    private void LogDiagnosticsFromContextMenu()
    {
        FindReferences();
        LogTransfer(
            $"診断: Scene={SceneManager.GetActiveScene().name} / Object={name} / " +
            $"enabled={enabled} / active={gameObject.activeInHierarchy} / " +
            $"restored={hasRestoredForThisInstance} / " +
            $"GameSessionManager={GetObjectLabel(gameSessionManager)} / " +
            $"InventoryController={GetObjectLabel(inventoryController)} / " +
            $"EquipmentController={GetObjectLabel(equipmentController)}"
        );

        if (gameSessionManager != null)
        {
            LogTransfer(gameSessionManager.GetInventorySessionSummary());
        }
    }

    private void RefreshPlayerSystems()
    {
        PlayerEquipmentVisualController equipmentVisualController =
            GetComponent<PlayerEquipmentVisualController>();

        equipmentVisualController?.RefreshEquipmentState();

        PlayerWeightController weightController =
            GetComponent<PlayerWeightController>();

        weightController?.RecalculateWeight();
    }

    private void FindReferences()
    {
        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>();
        }

        if (inventoryController == null)
        {
            inventoryController = GetComponent<InventoryController>();
        }

        if (inventoryController == null)
        {
            inventoryController =
                FindAnyObjectByType<InventoryController>();
        }

        if (equipmentController == null)
        {
            equipmentController = GetComponent<EquipmentController>();
        }

        if (equipmentController == null)
        {
            equipmentController =
                FindAnyObjectByType<EquipmentController>();
        }
    }

    private void LogTransfer(string message)
    {
        if (!alwaysLogSessionTransfer)
        {
            return;
        }

        Debug.Log($"[InventorySessionBridge] {message}", this);
    }

    private void LogTransferWarning(string message)
    {
        if (!alwaysLogSessionTransfer)
        {
            return;
        }

        Debug.LogWarning($"[InventorySessionBridge] {message}", this);
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[PlayerInventorySessionBridge] {message}", this);
    }

    private static string GetObjectLabel(Component component)
    {
        return component != null
            ? component.gameObject.name
            : "未設定";
    }
}
