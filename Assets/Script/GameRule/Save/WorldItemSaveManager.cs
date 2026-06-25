using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class WorldItemSaveManager : MonoBehaviour
{
    [Header("復元に必要な参照")]
    [SerializeField]
    private WorldItemPickup droppedItemPrefab;

    [SerializeField]
    private ItemDataDatabase itemDataDatabase;

    [Header("ロード時の設定")]
    [Tooltip("ロード前に、現在シーンにある地面アイテムを消します")]
    [SerializeField]
    private bool clearExistingWorldItemsBeforeRestore = true;

    public WorldItemSaveCollection CaptureAllLoadedWorldItems()
    {
        WorldItemSaveCollection collection =
            new WorldItemSaveCollection();

        WorldItemPickup[] pickups =
            Object.FindObjectsByType<WorldItemPickup>(
                FindObjectsInactive.Exclude
            );

        foreach (WorldItemPickup pickup in pickups)
        {
            if (pickup == null ||
                !pickup.HasValidDroppedItem)
            {
                continue;
            }

            WorldItemSaveData itemData =
                pickup.CreateSaveData();

            if (itemData == null)
            {
                continue;
            }

            itemData.sceneName =
                pickup.gameObject.scene.name;

            collection.items.Add(itemData);
        }

        return collection;
    }

    public void RestoreActiveSceneWorldItems(
        WorldItemSaveCollection collection)
    {
        if (collection == null ||
            collection.items == null)
        {
            return;
        }

        if (droppedItemPrefab == null ||
            itemDataDatabase == null)
        {
            Debug.LogWarning(
                "WorldItemSaveManager: DroppedItem Prefab または " +
                "ItemDataDatabase が設定されていません。",
                this
            );

            return;
        }

        Scene activeScene =
            SceneManager.GetActiveScene();

        if (clearExistingWorldItemsBeforeRestore)
        {
            ClearWorldItemsInScene(activeScene);
        }

        foreach (WorldItemSaveData savedItem in collection.items)
        {
            if (savedItem == null ||
                savedItem.sceneName != activeScene.name)
            {
                continue;
            }

            if (!itemDataDatabase.TryGetItemData(
                    savedItem.itemId,
                    out ItemData itemData))
            {
                Debug.LogWarning(
                    $"WorldItemSaveManager: " +
                    $"Item Id が見つかりません：" +
                    $"{savedItem.itemId}",
                    this
                );

                continue;
            }

            Vector3 position = new Vector3(
                savedItem.positionX,
                savedItem.positionY,
                savedItem.positionZ
            );

            WorldItemPickup pickup = Instantiate(
                droppedItemPrefab,
                position,
                Quaternion.identity
            );

            if (!pickup.RestoreFromSaveData(
                    savedItem,
                    itemData))
            {
                Destroy(pickup.gameObject);
            }
        }
    }

    public void ClearActiveSceneWorldItems()
    {
        ClearWorldItemsInScene(
            SceneManager.GetActiveScene()
        );
    }

    private void ClearWorldItemsInScene(Scene targetScene)
    {
        WorldItemPickup[] pickups =
            Object.FindObjectsByType<WorldItemPickup>(
                FindObjectsInactive.Exclude
            );

        foreach (WorldItemPickup pickup in pickups)
        {
            if (pickup == null ||
                pickup.gameObject.scene != targetScene)
            {
                continue;
            }

            pickup.gameObject.SetActive(false);
            Destroy(pickup.gameObject);
        }
    }
}