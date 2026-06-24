using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class InventoryGrid
{
    [Header("グリッドサイズ")]
    [SerializeField, Min(1)] private int width = 7;
    [SerializeField, Min(1)] private int height = 10;

    [Header("入っているアイテム")]
    [SerializeField] private List<InventoryItem> items = new List<InventoryItem>();

    // 実行中だけ使う、各マスがどのアイテムに使われているかの一覧
    [NonSerialized] private InventoryItem[,] slotMap;

    public int Width => width;
    public int Height => height;

    public IReadOnlyList<InventoryItem> Items => items;

    public InventoryGrid()
    {
    }

    public InventoryGrid(int gridWidth, int gridHeight)
    {
        width = Mathf.Max(1, gridWidth);
        height = Mathf.Max(1, gridHeight);

        RebuildSlotMap();
    }

    // グリッドのサイズを設定する
    public void Initialize(int gridWidth, int gridHeight, bool clearItems = true)
    {
        width = Mathf.Max(1, gridWidth);
        height = Mathf.Max(1, gridHeight);

        if (clearItems)
        {
            items.Clear();
        }

        RebuildSlotMap();
    }

    // 指定マスがグリッド内か確認する
    public bool IsInsideGrid(int x, int y)
    {
        return x >= 0 && x < width &&
               y >= 0 && y < height;
    }

    // 指定マスを使用しているアイテムを返す
    public InventoryItem GetItemAt(int x, int y)
    {
        EnsureSlotMap();

        if (!IsInsideGrid(x, y))
        {
            return null;
        }

        return slotMap[x, y];
    }

    // 指定アイテムが、このインベントリに入っているか
    public bool ContainsItem(InventoryItem item)
    {
        return item != null && items.Contains(item);
    }

    // 現在の向きのまま、その位置へ置けるか確認する
    public bool CanPlaceItem(InventoryItem item, int targetX, int targetY)
    {
        if (item == null)
        {
            return false;
        }

        return CanPlaceItem(item, targetX, targetY, item.IsRotated);
    }

    // 指定した向きで、その位置へ置けるか確認する
    public bool CanPlaceItem(
        InventoryItem item,
        int targetX,
        int targetY,
        bool isRotated)
    {
        EnsureSlotMap();

        if (item == null || item.ItemData == null)
        {
            return false;
        }

        // 回転不可アイテムなら、常に通常向きとして扱う
        bool finalRotated = item.CanRotate && isRotated;
        Vector2Int itemSize = item.ItemData.GetSize(finalRotated);

        for (int y = targetY; y < targetY + itemSize.y; y++)
        {
            for (int x = targetX; x < targetX + itemSize.x; x++)
            {
                // グリッド外にはみ出していたら置けない
                if (!IsInsideGrid(x, y))
                {
                    return false;
                }

                InventoryItem occupiedItem = slotMap[x, y];

                // 自分自身以外のアイテムが入っていたら置けない
                if (occupiedItem != null && occupiedItem != item)
                {
                    return false;
                }
            }
        }

        return true;
    }

    // アイテムを現在の向きのまま配置・移動する
    public bool TryPlaceItem(InventoryItem item, int targetX, int targetY)
    {
        if (item == null)
        {
            return false;
        }

        return TryPlaceItem(item, targetX, targetY, item.IsRotated);
    }

    // アイテムを指定位置・指定方向で配置・移動する
    public bool TryPlaceItem(
        InventoryItem item,
        int targetX,
        int targetY,
        bool isRotated)
    {
        EnsureSlotMap();

        if (item == null || item.ItemData == null)
        {
            return false;
        }

        bool finalRotated = item.CanRotate && isRotated;

        if (!CanPlaceItem(item, targetX, targetY, finalRotated))
        {
            return false;
        }

        // 初めて置くアイテムなら一覧へ追加
        if (!items.Contains(item))
        {
            items.Add(item);
        }

        // 前の場所を空ける
        ClearItemFromSlots(item);

        // 必要なら回転状態を変更
        if (item.IsRotated != finalRotated)
        {
            item.TryRotate();
        }

        item.SetGridPosition(targetX, targetY);

        // 新しい場所を使用済みにする
        FillItemSlots(item);

        return true;
    }

    // アイテムをその場で回転できるか試す
    public bool TryRotateItem(InventoryItem item)
    {
        if (item == null || !item.CanRotate || !items.Contains(item))
        {
            return false;
        }

        return TryPlaceItem(
            item,
            item.GridX,
            item.GridY,
            !item.IsRotated
        );
    }

    // アイテムをインベントリから取り除く
    public bool RemoveItem(InventoryItem item)
    {
        EnsureSlotMap();

        if (item == null || !items.Contains(item))
        {
            return false;
        }

        ClearItemFromSlots(item);
        items.Remove(item);

        return true;
    }

    // 指定したアイテムから数を減らす
    public int RemoveAmount(InventoryItem item, int amount)
    {
        if (item == null || !items.Contains(item) || amount <= 0)
        {
            return 0;
        }

        int removedAmount = item.RemoveAmount(amount);

        if (item.IsEmpty())
        {
            RemoveItem(item);
        }

        return removedAmount;
    }

    // アイテムを自動配置する。入らなかった個数を remainingAmount に返す
    public bool TryAddItem(
        ItemData itemData,
        int amount,
        out int remainingAmount)
    {
        remainingAmount = Mathf.Max(0, amount);

        if (itemData == null)
        {
            return false;
        }

        if (remainingAmount == 0)
        {
            return true;
        }

        // 先に、既にある同じアイテムのスタックへ追加する
        if (itemData.CanStack)
        {
            foreach (InventoryItem existingItem in items)
            {
                if (!existingItem.IsSameItem(itemData) || existingItem.IsStackFull)
                {
                    continue;
                }

                remainingAmount = existingItem.AddAmount(remainingAmount);

                if (remainingAmount <= 0)
                {
                    return true;
                }
            }
        }

        // 残りを新しいスタックとして配置する
        while (remainingAmount > 0)
        {
            if (!FindSpaceForItem(itemData, out Vector2Int position, out bool isRotated))
            {
                return false;
            }

            int stackAmount = Mathf.Min(remainingAmount, itemData.MaxStack);

            InventoryItem newItem = new InventoryItem(
                itemData,
                position.x,
                position.y,
                stackAmount
            );

            if (!TryPlaceItem(newItem, position.x, position.y, isRotated))
            {
                return false;
            }

            remainingAmount -= stackAmount;
        }

        return true;
    }

    // 空いている配置場所を探す
    public bool FindSpaceForItem(
        ItemData itemData,
        out Vector2Int position,
        out bool isRotated)
    {
        position = Vector2Int.zero;
        isRotated = false;

        if (itemData == null)
        {
            return false;
        }

        InventoryItem testItem = new InventoryItem(itemData);

        // まず通常向きで探す
        if (FindSpace(testItem, false, out position))
        {
            isRotated = false;
            return true;
        }

        // 入らなければ回転して探す
        if (itemData.CanRotate && FindSpace(testItem, true, out position))
        {
            isRotated = true;
            return true;
        }

        return false;
    }

    // セーブ・ロード後などに、使用済みマスの情報を作り直す
    public void RebuildSlotMap()
    {
        width = Mathf.Max(1, width);
        height = Mathf.Max(1, height);

        slotMap = new InventoryItem[width, height];

        foreach (InventoryItem item in items)
        {
            if (item == null || item.ItemData == null)
            {
                continue;
            }

            if (!CanPlaceItem(item, item.GridX, item.GridY, item.IsRotated))
            {
                Debug.LogWarning(
                    $"InventoryGrid: {item.ItemData.DisplayName} の配置データが不正です。"
                );
                continue;
            }

            FillItemSlots(item);
        }
    }

    private bool FindSpace(
        InventoryItem item,
        bool isRotated,
        out Vector2Int position)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (CanPlaceItem(item, x, y, isRotated))
                {
                    position = new Vector2Int(x, y);
                    return true;
                }
            }
        }

        position = Vector2Int.zero;
        return false;
    }

    private void EnsureSlotMap()
    {
        if (slotMap == null ||
            slotMap.GetLength(0) != width ||
            slotMap.GetLength(1) != height)
        {
            RebuildSlotMap();
        }
    }

    private void FillItemSlots(InventoryItem item)
    {
        Vector2Int itemSize = item.Size;

        for (int y = item.GridY; y < item.GridY + itemSize.y; y++)
        {
            for (int x = item.GridX; x < item.GridX + itemSize.x; x++)
            {
                if (IsInsideGrid(x, y))
                {
                    slotMap[x, y] = item;
                }
            }
        }
    }

    private void ClearItemFromSlots(InventoryItem item)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (slotMap[x, y] == item)
                {
                    slotMap[x, y] = null;
                }
            }
        }
    }
}