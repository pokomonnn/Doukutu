using UnityEngine;

/// <summary>
/// Town_Mainでプレイヤー本体を置かずに、プレイヤーの持ち物と装備を扱うための入れ物です。
/// 同じGameObjectに InventoryController / EquipmentController /
/// PlayerInventorySessionBridge を付けて使用します。
///
/// このObjectをInventoryGridUIや、後で作る質屋の売却画面の参照先にします。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(InventoryController))]
[RequireComponent(typeof(EquipmentController))]
[RequireComponent(typeof(PlayerInventorySessionBridge))]
public class TownPlayerInventoryController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private InventoryController inventoryController;
    [SerializeField] private EquipmentController equipmentController;
    [SerializeField] private PlayerInventorySessionBridge sessionBridge;

    public InventoryController InventoryController => inventoryController;
    public EquipmentController EquipmentController => equipmentController;
    public PlayerInventorySessionBridge SessionBridge => sessionBridge;

    private void Awake()
    {
        FindReferences();
    }

    [ContextMenu("Restore Town Inventory From Session")]
    private void RestoreTownInventoryFromSession()
    {
        FindReferences();
        sessionBridge?.RestoreFromSession();
    }

    private void FindReferences()
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
}
