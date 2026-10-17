using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 既存のItemBoxセーブ対応。DungeonLootStudio用にResetOpenedForNewDungeonVisitのみ追加。
/// すでにプロジェクト内にある同名スクリプトに上書きしてください（二重配置禁止）。
/// </summary>
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

    /// <summary>
    /// ステージに再入場し新しいLootを抽選する時、開封状態を初期化する。
    /// PersistentIdは変更しない（セーブとの対応を維持）。
    /// </summary>
    public void ResetOpenedForNewDungeonVisit()
    {
        wasOpened = false;
        ApplyVisual();
    }

    /// <summary>ランタイム生成されたItemBoxへSpawnPointの固定IDを設定。</summary>
    public void AssignPersistentId(string newPersistentId)
    {
        if (string.IsNullOrWhiteSpace(newPersistentId))
        {
            Debug.LogWarning("[ItemBoxSaveIdentity] 空のPersistent Idは設定できません。",this);
            return;
        }
        persistentId = newPersistentId.Trim();
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
                    StoredMagazineAmmo = item.StoredMagazineAmmo,
                    StoredMagazineAmmoItemId = item.StoredMagazineAmmoType != null ? item.StoredMagazineAmmoType.ItemId : string.Empty,
                    HasStoredWeaponDurability = item.HasStoredWeaponDurability,
                    StoredWeaponDurability = item.StoredWeaponDurability
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
                InventoryItem item = new InventoryItem(definition,itemData.GridX,itemData.GridY,
                    Mathf.Clamp(itemData.Amount,1,definition.MaxStack));
                if (itemData.IsRotated && item.CanRotate) item.TryRotate();
                if (itemData.HasStoredMagazineAmmo)
                {
                    AmmoItemData ammoType = null;
                    if (!string.IsNullOrWhiteSpace(itemData.StoredMagazineAmmoItemId) &&
                        database.TryGetItemData(itemData.StoredMagazineAmmoItemId, out ItemData ammoDefinition))
                        ammoType = ammoDefinition as AmmoItemData;
                    item.SetStoredMagazineAmmoState(itemData.StoredMagazineAmmo, ammoType);
                }
                if (itemData.HasStoredWeaponDurability)
                    item.SetStoredWeaponDurability(itemData.StoredWeaponDurability);
                else
                    item.EnsureWeaponDurabilityInitialized();
                restored.Add(item);
            }
        }
        itemBoxInventory.RestoreInventoryFromSave(saved.GridWidth,saved.GridHeight,restored);
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
