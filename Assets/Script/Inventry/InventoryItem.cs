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

    public bool HasStoredMagazineAmmo =>
        hasStoredMagazineAmmo;

    public int StoredMagazineAmmo =>
        storedMagazineAmmo;

    public void SetStoredMagazineAmmo(int ammo)
    {
        storedMagazineAmmo = Mathf.Max(0, ammo);
        hasStoredMagazineAmmo = true;
    }

    public ItemData ItemData => itemData;

    public int GridX => gridX;
    public int GridY => gridY;

    public Vector2Int GridPosition => new Vector2Int(gridX, gridY);

    public bool IsRotated => isRotated;
    public int Amount => amount;

    // 回転状態を反映した、現在の横幅・縦幅
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

    public InventoryItem(ItemData newItemData, int x = 0, int y = 0, int initialAmount = 1)
    {
        itemData = newItemData;
        gridX = x;
        gridY = y;

        if (itemData != null)
        {
            amount = Mathf.Clamp(initialAmount, 1, itemData.MaxStack);
        }
        else
        {
            amount = 1;
        }
    }

    // アイテムの配置位置を変更する
    public void SetGridPosition(int x, int y)
    {
        gridX = x;
        gridY = y;
    }

    // 回転できるアイテムなら回転する
    public bool TryRotate()
    {
        if (!CanRotate)
        {
            return false;
        }

        isRotated = !isRotated;
        return true;
    }

    // 同じ種類のアイテムか確認する
    public bool IsSameItem(ItemData otherItemData)
    {
        return itemData == otherItemData;
    }

    // スタックに追加し、入り切らなかった数を返す
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

    // 数を減らし、実際に減らした数を返す
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

    // 数が0なら、このInventoryItemは削除対象
    public bool IsEmpty()
    {
        return amount <= 0;
    }
}