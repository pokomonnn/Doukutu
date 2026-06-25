using System;
using UnityEngine;

public enum EquipmentResult
{
    Success,
    InvalidItem,
    InventoryNotFound,
    ItemNotInInventory,
    UnsupportedItem,
    InvalidSlot,
    SlotOccupied,
    InventoryFull,
    NothingEquipped,
    InvalidStackAmount
}

[DisallowMultipleComponent]
public class EquipmentController : MonoBehaviour
{
    [Header("通常インベントリ")]
    [SerializeField] private InventoryController inventoryController;

    [Header("現在の装備")]
    [SerializeField] private InventoryItem primaryWeaponItem;
    [SerializeField] private InventoryItem helmetItem;

    public InventoryController InventoryController
    {
        get
        {
            FindInventoryController();
            return inventoryController;
        }
    }

    public event Action OnEquipmentChanged;

    public InventoryItem PrimaryWeaponItem => primaryWeaponItem;
    public InventoryItem HelmetItem => helmetItem;

    public WeaponItemData EquippedWeaponData =>
        primaryWeaponItem != null
            ? primaryWeaponItem.ItemData as WeaponItemData
            : null;

    public ArmorItemData EquippedHelmetData =>
        helmetItem != null
            ? helmetItem.ItemData as ArmorItemData
            : null;

    private void Awake()
    {
        FindInventoryController();
    }

    // アイテムがどの装備枠に入るかを返す
    public bool TryGetEquipmentSlot(
        ItemData itemData,
        out EquipmentSlotType slotType)
    {
        slotType = EquipmentSlotType.None;

        if (itemData == null)
        {
            return false;
        }

        if (itemData is WeaponItemData)
        {
            slotType = EquipmentSlotType.PrimaryWeapon;
            return true;
        }

        if (itemData is ArmorItemData armorData)
        {
            slotType = armorData.EquipmentSlot;

            return slotType != EquipmentSlotType.None;
        }

        return false;
    }

    // 通常インベントリにあるアイテムを装備する
    public bool TryEquipItem(
        InventoryItem item,
        out EquipmentResult result)
    {
        result = EquipmentResult.InvalidItem;

        if (item == null || item.ItemData == null)
        {
            return false;
        }

        if (!FindInventoryController())
        {
            result = EquipmentResult.InventoryNotFound;
            return false;
        }

        if (!inventoryController.Grid.ContainsItem(item))
        {
            result = EquipmentResult.ItemNotInInventory;
            return false;
        }

        // 装備品は必ず1個ずつ扱う
        if (item.Amount != 1)
        {
            result = EquipmentResult.InvalidStackAmount;
            return false;
        }

        if (!TryGetEquipmentSlot(
                item.ItemData,
                out EquipmentSlotType slotType))
        {
            result = EquipmentResult.UnsupportedItem;
            return false;
        }

        if (slotType == EquipmentSlotType.None)
        {
            result = EquipmentResult.InvalidSlot;
            return false;
        }

        // 今回は、すでに装備がある場合は交換せず失敗にする
        if (HasEquippedItem(slotType))
        {
            result = EquipmentResult.SlotOccupied;
            return false;
        }

        // 通常インベントリから消す
        if (!inventoryController.RemoveItem(item))
        {
            result = EquipmentResult.ItemNotInInventory;
            return false;
        }

        SetEquippedItem(slotType, item);

        result = EquipmentResult.Success;
        OnEquipmentChanged?.Invoke();

        return true;
    }

    // 装備枠のアイテムを通常インベントリへ戻す
    public bool TryUnequip(
        EquipmentSlotType slotType,
        out EquipmentResult result)
    {
        result = EquipmentResult.InvalidSlot;

        if (!FindInventoryController())
        {
            result = EquipmentResult.InventoryNotFound;
            return false;
        }

        InventoryItem equippedItem = GetEquippedItem(slotType);

        if (equippedItem == null || equippedItem.ItemData == null)
        {
            result = EquipmentResult.NothingEquipped;
            return false;
        }

        // 武器・ヘルメットはMax Stackを1にして使う前提
        if (equippedItem.Amount != 1)
        {
            result = EquipmentResult.InvalidStackAmount;
            return false;
        }

        // 新しいInventoryItemを作らず、今装備している同じ個体を戻す。
        // これで武器に保存しているStoredMagazineAmmoも失われない。
        if (!inventoryController.Grid.FindSpaceForItem(
                equippedItem.ItemData,
                out Vector2Int position,
                out bool isRotated))
        {
            result = EquipmentResult.InventoryFull;
            return false;
        }

        bool moved = inventoryController.TryMoveItem(
            equippedItem,
            position.x,
            position.y,
            isRotated
        );

        if (!moved)
        {
            result = EquipmentResult.InventoryFull;
            return false;
        }

        SetEquippedItem(slotType, null);

        result = EquipmentResult.Success;
        OnEquipmentChanged?.Invoke();

        return true;
    }


    // 装備中のアイテムを、指定したインベントリ座標へ戻す
    public bool TryUnequipToPosition(
        EquipmentSlotType slotType,
        int targetX,
        int targetY,
        bool isRotated,
        out EquipmentResult result)
    {
        result = EquipmentResult.InvalidSlot;

        if (!FindInventoryController())
        {
            result = EquipmentResult.InventoryNotFound;
            return false;
        }

        InventoryItem equippedItem = GetEquippedItem(slotType);

        if (equippedItem == null ||
            equippedItem.ItemData == null)
        {
            result = EquipmentResult.NothingEquipped;
            return false;
        }

        if (equippedItem.Amount != 1)
        {
            result = EquipmentResult.InvalidStackAmount;
            return false;
        }

        bool finalRotation =
            equippedItem.CanRotate && isRotated;

        // 指定位置が空いている時だけ、同じInventoryItemを戻す
        bool moved = inventoryController.TryMoveItem(
            equippedItem,
            targetX,
            targetY,
            finalRotation
        );

        if (!moved)
        {
            // 重なり・グリッド外・サイズ不足を含めて失敗扱い
            result = EquipmentResult.InventoryFull;
            return false;
        }

        // 通常インベントリに戻せた時だけ装備枠を空にする
        SetEquippedItem(slotType, null);

        result = EquipmentResult.Success;
        OnEquipmentChanged?.Invoke();

        return true;
    }

    public InventoryItem GetEquippedItem(
        EquipmentSlotType slotType)
    {
        switch (slotType)
        {
            case EquipmentSlotType.PrimaryWeapon:
                return primaryWeaponItem;

            case EquipmentSlotType.Helmet:
                return helmetItem;

            default:
                return null;
        }
    }

    private bool HasEquippedItem(EquipmentSlotType slotType)
    {
        InventoryItem equippedItem = GetEquippedItem(slotType);

        return equippedItem != null &&
               equippedItem.ItemData != null;
    }

    public bool IsSlotOccupied(
    EquipmentSlotType slotType)
    {
        return HasEquippedItem(slotType);
    }

    private void SetEquippedItem(
        EquipmentSlotType slotType,
        InventoryItem item)
    {
        switch (slotType)
        {
            case EquipmentSlotType.PrimaryWeapon:
                primaryWeaponItem = item;
                break;

            case EquipmentSlotType.Helmet:
                helmetItem = item;
                break;
        }
    }

    private bool FindInventoryController()
    {
        if (inventoryController != null)
        {
            return true;
        }

        inventoryController =
            GetComponent<InventoryController>();

        if (inventoryController != null)
        {
            return true;
        }

        inventoryController =
            FindAnyObjectByType<InventoryController>();

        return inventoryController != null;
    }
}