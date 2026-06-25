using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "ItemDataDatabase",
    menuName = "Inventory/Item Data Database"
)]
public class ItemDataDatabase : ScriptableObject
{
    [Header("ゲーム内で使用する全ItemData")]
    [SerializeField]
    private List<ItemData> itemDataList =
        new List<ItemData>();

    private Dictionary<string, ItemData> itemLookup =
        new Dictionary<string, ItemData>();

    private bool isLookupBuilt;

    public bool TryGetItemData(
        string itemId,
        out ItemData itemData)
    {
        itemData = null;

        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        BuildLookupIfNeeded();

        return itemLookup.TryGetValue(
            itemId,
            out itemData
        );
    }

    [ContextMenu("Validate Item IDs")]
    public void ValidateItemIds()
    {
        BuildLookup(true);
    }

    private void OnEnable()
    {
        BuildLookup(false);
    }

    private void OnValidate()
    {
        isLookupBuilt = false;
    }

    private void BuildLookupIfNeeded()
    {
        if (isLookupBuilt)
        {
            return;
        }

        BuildLookup(false);
    }

    private void BuildLookup(bool logWarnings)
    {
        itemLookup.Clear();

        foreach (ItemData itemData in itemDataList)
        {
            if (itemData == null)
            {
                continue;
            }

            string itemId = itemData.ItemId;

            if (string.IsNullOrWhiteSpace(itemId))
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"ItemDataDatabase: " +
                        $"{itemData.name} の Item Id が空です。",
                        this
                    );
                }

                continue;
            }

            if (itemLookup.ContainsKey(itemId))
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"ItemDataDatabase: Item Id が重複しています：" +
                        $"{itemId}",
                        this
                    );
                }

                continue;
            }

            itemLookup.Add(itemId, itemData);
        }

        isLookupBuilt = true;
    }
}