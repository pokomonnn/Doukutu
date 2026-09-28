using UnityEngine;

/// <summary>
/// 町・洞窟などシーンを問わず、売買システムから
/// プレイヤーのInventory / Equipment / SessionBridgeへアクセスする共通窓口です。
///
/// Town_Mainでは旧TownPlayerInventoryControllerを残しても構いません。
/// TownPlayerInventoryControllerはこのクラスを継承する互換クラスになっています。
/// 洞窟の実プレイヤーには、このPlayerTradeInventoryBridgeを直接追加してください。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InventoryController))]
[RequireComponent(typeof(EquipmentController))]
[RequireComponent(typeof(PlayerInventorySessionBridge))]
public class PlayerTradeInventoryBridge : MonoBehaviour
{
    [Header("共通プレイヤー参照")]
    [SerializeField] private InventoryController inventoryController;
    [SerializeField] private EquipmentController equipmentController;
    [SerializeField] private PlayerInventorySessionBridge sessionBridge;

    public InventoryController InventoryController => inventoryController;
    public EquipmentController EquipmentController => equipmentController;
    public PlayerInventorySessionBridge SessionBridge => sessionBridge;

    public bool IsReady =>
        inventoryController != null &&
        equipmentController != null &&
        sessionBridge != null;

    protected virtual void Awake()
    {
        RefreshReferences();
    }

    protected virtual void OnValidate()
    {
        RefreshReferences();
    }

    /// <summary>
    /// 同じGameObjectから必要な参照を取り直します。
    /// </summary>
    public void RefreshReferences()
    {
        if (inventoryController == null)
        {
            inventoryController = GetComponent<InventoryController>();
        }

        if (equipmentController == null)
        {
            equipmentController = GetComponent<EquipmentController>();
        }

        if (sessionBridge == null)
        {
            sessionBridge = GetComponent<PlayerInventorySessionBridge>();
        }
    }

    /// <summary>
    /// 現在のInventory / Equipment状態をGameSessionへ保存します。
    /// </summary>
    public void CaptureToSession()
    {
        RefreshReferences();
        sessionBridge?.CaptureToSession();
    }

    /// <summary>
    /// GameSessionからInventory / Equipment状態を復元します。
    /// </summary>
    public void RestoreFromSession()
    {
        RefreshReferences();
        sessionBridge?.RestoreFromSession();
    }

    /// <summary>
    /// 売買UIのPlayer側InventoryGridUIを、このプレイヤーへ接続します。
    /// </summary>
    public bool BindPlayerGridUI(InventoryGridUI gridUI, bool refresh = true)
    {
        RefreshReferences();

        if (gridUI == null || inventoryController == null)
        {
            return false;
        }

        gridUI.BindPlayerInventory(inventoryController);

        if (refresh)
        {
            gridUI.RefreshInventoryUI();
        }

        return true;
    }

    [ContextMenu("Trade Inventory / Capture To Session")]
    private void CaptureFromContextMenu()
    {
        CaptureToSession();
    }

    [ContextMenu("Trade Inventory / Restore From Session")]
    private void RestoreFromContextMenu()
    {
        RestoreFromSession();
    }
}
