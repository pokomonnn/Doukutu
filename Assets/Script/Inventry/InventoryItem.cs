using System;
using UnityEngine;

[Serializable]
public class InventoryItem
{
    [SerializeField] private ItemData itemData;

    [Header("インベントリ内の位置")]
    [SerializeField] private int gridX;
    [SerializeField] private int gridY;

    [Header("状態")]
    [SerializeField] private bool isRotated;
    [SerializeField, Min(1)] private int amount = 1;

    [Header("武器の残弾")]
    [SerializeField] private bool hasStoredMagazineAmmo;

    [SerializeField, Min(0)]
    private int storedMagazineAmmo;

    [Header("武器の装填弾種")]
    [SerializeField] private bool hasStoredMagazineAmmoType;
    [SerializeField] private AmmoItemData storedMagazineAmmoType;

    [Header("武器の耐久度")]
    [SerializeField] private bool hasStoredWeaponDurability;

    [SerializeField, Min(0f)]
    private float storedWeaponDurability;

    [Header("武器のジャム状態")]
    [SerializeField] private bool storedWeaponJammed;

    public bool HasStoredMagazineAmmo =>
        hasStoredMagazineAmmo;

    public int StoredMagazineAmmo =>
        storedMagazineAmmo;

    public bool HasStoredMagazineAmmoType =>
        hasStoredMagazineAmmoType &&
        storedMagazineAmmoType != null;

    public AmmoItemData StoredMagazineAmmoType =>
        HasStoredMagazineAmmoType
            ? storedMagazineAmmoType
            : null;

    public bool HasStoredWeaponDurability =>
        hasStoredWeaponDurability;

    public bool StoredWeaponJammed =>
        itemData is WeaponItemData && storedWeaponJammed;

    public float StoredWeaponDurability
    {
        get
        {
            if (hasStoredWeaponDurability)
            {
                return Mathf.Max(0f, storedWeaponDurability);
            }

            WeaponItemData weaponData = itemData as WeaponItemData;
            return weaponData != null
                ? weaponData.MaxDurability
                : 0f;
        }
    }

    public float WeaponDurabilityPercent
    {
        get
        {
            WeaponItemData weaponData = itemData as WeaponItemData;

            if (weaponData == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(
                StoredWeaponDurability / weaponData.MaxDurability
            );
        }
    }

    public float WeaponDamagePercent =>
        itemData is WeaponItemData
            ? 1f - WeaponDurabilityPercent
            : 0f;

    public bool IsWeaponBroken =>
        itemData is WeaponItemData &&
        StoredWeaponDurability <= 0f;

    public void SetStoredWeaponDurability(float durability)
    {
        WeaponItemData weaponData = itemData as WeaponItemData;

        if (weaponData == null)
        {
            storedWeaponDurability = 0f;
            hasStoredWeaponDurability = false;
            storedWeaponJammed = false;
            return;
        }

        storedWeaponDurability = Mathf.Clamp(
            durability,
            0f,
            weaponData.MaxDurability
        );

        hasStoredWeaponDurability = true;
    }

    public void EnsureWeaponDurabilityInitialized()
    {
        WeaponItemData weaponData = itemData as WeaponItemData;

        if (weaponData == null || hasStoredWeaponDurability)
        {
            return;
        }

        SetStoredWeaponDurability(weaponData.MaxDurability);
    }

    public float RepairWeaponDurability(float repairAmount)
    {
        WeaponItemData weaponData = itemData as WeaponItemData;

        if (weaponData == null || repairAmount <= 0f)
        {
            return 0f;
        }

        EnsureWeaponDurabilityInitialized();

        float before = StoredWeaponDurability;
        float after = Mathf.Clamp(
            before + repairAmount,
            0f,
            weaponData.MaxDurability
        );

        SetStoredWeaponDurability(after);
        return Mathf.Max(0f, after - before);
    }

    public void RepairWeaponToFull()
    {
        WeaponItemData weaponData = itemData as WeaponItemData;

        if (weaponData == null)
        {
            return;
        }

        SetStoredWeaponDurability(weaponData.MaxDurability);
        SetStoredWeaponJammed(false);
    }

    public void SetStoredWeaponJammed(bool jammed)
    {
        if (!(itemData is WeaponItemData))
        {
            storedWeaponJammed = false;
            return;
        }

        storedWeaponJammed = jammed;
    }

    public void SetStoredMagazineAmmo(int ammo)
    {
        storedMagazineAmmo = Mathf.Max(0, ammo);
        hasStoredMagazineAmmo = true;
    }

    public void SetStoredMagazineAmmoType(AmmoItemData ammoType)
    {
        storedMagazineAmmoType = ammoType;
        hasStoredMagazineAmmoType = ammoType != null;
    }

    public void SetStoredMagazineAmmoState(
        int ammo,
        AmmoItemData ammoType)
    {
        SetStoredMagazineAmmo(ammo);
        SetStoredMagazineAmmoType(ammoType);
    }

    public ItemData ItemData => itemData;

    public int GridX => gridX;
    public int GridY => gridY;

    public Vector2Int GridPosition => new Vector2Int(gridX, gridY);

    public bool IsRotated => isRotated;
    public int Amount => amount;

    public Vector2Int Size
    {
        get
        {
            if (itemData == null)
            {
                return Vector2Int.one;
            }

            return itemData.GetSize(isRotated);
        }
    }

    public int Width => Size.x;
    public int Height => Size.y;

    public bool CanRotate => itemData != null && itemData.CanRotate;

    public bool CanStack =>
        itemData != null &&
        itemData.CanStack;

    public bool IsStackFull =>
        itemData != null &&
        amount >= itemData.MaxStack;

    public InventoryItem(
        ItemData newItemData,
        int x = 0,
        int y = 0,
        int initialAmount = 1)
    {
        itemData = newItemData;
        gridX = x;
        gridY = y;

        if (itemData != null)
        {
            amount = Mathf.Clamp(
                initialAmount,
                1,
                itemData.MaxStack
            );
        }
        else
        {
            amount = 1;
        }

        EnsureWeaponDurabilityInitialized();
    }

    public void SetGridPosition(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    public bool TryRotate()
    {
        if (!CanRotate)
        {
            return false;
        }

        isRotated = !isRotated;
        return true;
    }

    public bool IsSameItem(ItemData otherItemData)
    {
        return itemData == otherItemData;
    }

    public int AddAmount(int addAmount)
    {
        if (itemData == null || !CanStack || addAmount <= 0)
        {
            return addAmount;
        }

        int freeSpace = itemData.MaxStack - amount;
        int addedAmount = Mathf.Min(addAmount, freeSpace);

        amount += addedAmount;

        return addAmount - addedAmount;
    }

    public int RemoveAmount(int removeAmount)
    {
        if (removeAmount <= 0)
        {
            return 0;
        }

        int removedAmount = Mathf.Min(removeAmount, amount);
        amount -= removedAmount;

        return removedAmount;
    }

    public bool IsEmpty()
    {
        return amount <= 0;
    }
}
