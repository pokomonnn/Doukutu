using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(PlayerMove))]
[RequireComponent(typeof(Collider2D))]
public class PlayerItemDropper : MonoBehaviour
{
    [Header("落とすPrefab")]
    [Tooltip("WorldItemPickup が付いた DroppedItem Prefab を設定")]
    [SerializeField] private WorldItemPickup droppedItemPrefab;

    [Header("参照")]
    [SerializeField] private InventoryController inventoryController;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private Collider2D playerCollider;

    [Header("落とす位置")]
    [SerializeField, Min(0f)]
    private float dropForwardDistance = 0.25f;

    [SerializeField, Min(0f)]
    private float dropHeight = 0.35f;

    [Header("落とした時の動き")]
    [Tooltip("Xはプレイヤーの向きに合わせて自動反転します")]
    [SerializeField]
    private Vector2 dropVelocity =
        new Vector2(2.5f, 2.5f);

    private void Awake()
    {
        FindPlayerReferences();
        FindInventoryController();
    }

    // 通常インベントリ内のアイテムを落とす
    public bool TryDropItem(InventoryItem item)
    {
        if (!CanDropItem(item))
        {
            return false;
        }

        if (!FindPlayerReferences() ||
            !FindInventoryController())
        {
            Debug.LogWarning(
                "PlayerItemDropper: 必要な参照が見つかりません。",
                this
            );
            return false;
        }

        if (!inventoryController.Grid.ContainsItem(item))
        {
            Debug.LogWarning(
                "PlayerItemDropper: 指定アイテムはインベントリ内にありません。",
                this
            );
            return false;
        }

        WorldItemPickup droppedPickup =
            CreateDroppedPickup();

        if (droppedPickup == null)
        {
            return false;
        }

        // 地面に生成できた時だけインベントリから外す
        if (!inventoryController.RemoveItem(item))
        {
            Destroy(droppedPickup.gameObject);
            return false;
        }

        SetupDroppedPickup(droppedPickup, item);

        return true;
    }

    // 装備枠内のアイテムを地面へ落とす
    public bool TryDropEquippedItem(
        EquipmentController equipmentController,
        EquipmentSlotType slotType)
    {
        if (equipmentController == null ||
            !FindPlayerReferences())
        {
            Debug.LogWarning(
                "PlayerItemDropper: EquipmentController または " +
                "Player参照が見つかりません。",
                this
            );
            return false;
        }

        InventoryItem item =
            equipmentController.GetEquippedItem(slotType);

        if (!CanDropItem(item))
        {
            return false;
        }

        WorldItemPickup droppedPickup =
            CreateDroppedPickup();

        if (droppedPickup == null)
        {
            return false;
        }

        // 地面への生成後、装備枠からだけ外す
        if (!equipmentController.TryRemoveEquippedItem(
                slotType,
                item,
                out EquipmentResult result))
        {
            Destroy(droppedPickup.gameObject);

            Debug.Log(
                $"装備中アイテムを落とせません：{result}",
                this
            );

            return false;
        }

        // 同じInventoryItemを渡すため、武器残弾も維持される
        SetupDroppedPickup(droppedPickup, item);

        return true;
    }

    private bool CanDropItem(InventoryItem item)
    {
        if (item == null || item.ItemData == null)
        {
            return false;
        }

        if (!item.ItemData.CanDiscard)
        {
            return false;
        }

        return item.ItemData.ItemType !=
               InventoryItemType.Quest;
    }

    private WorldItemPickup CreateDroppedPickup()
    {
        if (droppedItemPrefab == null)
        {
            Debug.LogWarning(
                "PlayerItemDropper: Dropped Item Prefab が設定されていません。",
                this
            );
            return null;
        }

        return Instantiate(
            droppedItemPrefab,
            GetDropPosition(),
            Quaternion.identity
        );
    }

    private void SetupDroppedPickup(
        WorldItemPickup droppedPickup,
        InventoryItem item)
    {
        if (droppedPickup == null)
        {
            return;
        }

        droppedPickup.Setup(item);

        float direction =
            playerMove.IsFacingRight ? 1f : -1f;

        droppedPickup.SetVelocity(
            new Vector2(
                dropVelocity.x * direction,
                dropVelocity.y
            )
        );
    }

    private Vector3 GetDropPosition()
    {
        float direction =
            playerMove.IsFacingRight ? 1f : -1f;

        if (playerCollider == null)
        {
            return transform.position + new Vector3(
                direction * dropForwardDistance,
                dropHeight,
                0f
            );
        }

        Bounds bounds = playerCollider.bounds;

        return new Vector3(
            bounds.center.x +
            direction * (
                bounds.extents.x +
                dropForwardDistance
            ),
            bounds.min.y + dropHeight,
            transform.position.z
        );
    }

    private bool FindPlayerReferences()
    {
        if (playerMove == null)
        {
            playerMove = GetComponent<PlayerMove>();
        }

        if (playerCollider == null)
        {
            playerCollider = GetComponent<Collider2D>();
        }

        return playerMove != null &&
               playerCollider != null;
    }

    private bool FindInventoryController()
    {
        if (inventoryController != null)
        {
            return true;
        }

        inventoryController =
            GetComponent<InventoryController>();

        if (inventoryController == null)
        {
            inventoryController =
                FindAnyObjectByType<InventoryController>();
        }

        return inventoryController != null;
    }

    private void OnValidate()
    {
        dropForwardDistance =
            Mathf.Max(0f, dropForwardDistance);

        dropHeight =
            Mathf.Max(0f, dropHeight);
    }
}