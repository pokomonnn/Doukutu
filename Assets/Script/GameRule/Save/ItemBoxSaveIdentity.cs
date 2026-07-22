using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(ItemBoxInventory))]
public class ItemBoxSaveIdentity : MonoBehaviour
{
    [SerializeField] private string persistentId = string.Empty;
    [SerializeField] private ItemBoxInventory itemBoxInventory;
    [SerializeField] private bool wasOpened;
    [SerializeField] private GameObject closedVisual;
    [SerializeField] private GameObject openedVisual;

    public string PersistentId => persistentId?.Trim() ?? string.Empty;
    public bool WasOpened => wasOpened;

    private void Awake()
    {
        if (itemBoxInventory == null) itemBoxInventory = GetComponent<ItemBoxInventory>();
        ApplyVisual();
    }

    public void MarkOpened()
    {
        wasOpened = true;
        ApplyVisual();
    }

    public SavedItemBoxData CreateSaveData()
    {
        if (itemBoxInventory == null) itemBoxInventory = GetComponent<ItemBoxInventory>();
        if (itemBoxInventory == null || string.IsNullOrWhiteSpace(PersistentId)) return null;

        SavedItemBoxData result = new SavedItemBoxData
        {
            SceneName = gameObject.scene.name,
            PersistentId = PersistentId,
            WasOpened = wasOpened,
            GridWidth = itemBoxInventory.Grid != null ? itemBoxInventory.Grid.Width : 1,
            GridHeight = itemBoxInventory.Grid != null ? itemBoxInventory.Grid.Height : 1
        };

        if (itemBoxInventory.Grid?.Items != null)
        {
            foreach (InventoryItem item in itemBoxInventory.Grid.Items)
            {
                if (item?.ItemData == null) continue;
                result.Items.Add(new SavedInventoryItemData
                {
                    ItemId = item.ItemData.ItemId,
                    GridX = item.GridX,
                    GridY = item.GridY,
                    IsRotated = item.IsRotated,
                    Amount = item.Amount,
                    HasStoredMagazineAmmo = item.HasStoredMagazineAmmo,
                    StoredMagazineAmmo = item.StoredMagazineAmmo
                });
            }
        }

        return result;
    }

    public bool RestoreFromSaveData(SavedItemBoxData saved, ItemDataDatabase database)
    {
        if (saved == null || database == null) return false;
        if (itemBoxInventory == null) itemBoxInventory = GetComponent<ItemBoxInventory>();
        if (itemBoxInventory == null) return false;

        List<InventoryItem> restored = new List<InventoryItem>();
        if (saved.Items != null)
        {
            foreach (SavedInventoryItemData itemData in saved.Items)
            {
                if (itemData == null || !database.TryGetItemData(itemData.ItemId, out ItemData definition)) continue;
                InventoryItem item = new InventoryItem(definition, itemData.GridX, itemData.GridY, Mathf.Clamp(itemData.Amount, 1, definition.MaxStack));
                if (itemData.IsRotated && item.CanRotate) item.TryRotate();
                if (itemData.HasStoredMagazineAmmo) item.SetStoredMagazineAmmo(itemData.StoredMagazineAmmo);
                restored.Add(item);
            }
        }

        itemBoxInventory.RestoreInventoryFromSave(saved.GridWidth, saved.GridHeight, restored);
        wasOpened = saved.WasOpened;
        ApplyVisual();
        return true;
    }

    [ContextMenu("Generate New Persistent Id")]
    public void GenerateNewPersistentId()
    {
        persistentId = Guid.NewGuid().ToString("N");
#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void ApplyVisual()
    {
        if (closedVisual != null) closedVisual.SetActive(!wasOpened);
        if (openedVisual != null) openedVisual.SetActive(wasOpened);
    }

    private void OnValidate()
    {
        if (itemBoxInventory == null) itemBoxInventory = GetComponent<ItemBoxInventory>();
        if (string.IsNullOrWhiteSpace(persistentId)) persistentId = Guid.NewGuid().ToString("N");
        persistentId = persistentId.Trim();
    }
}
