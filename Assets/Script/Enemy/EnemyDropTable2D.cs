using System;
using UnityEngine;

/// <summary>
/// 敵が倒れた時に、設定したアイテムを確率で地面へ落とすドロップテーブルです。
/// EnemyDeathHandler2D から SpawnDrops() を呼んで使います。
/// </summary>
[DisallowMultipleComponent]
public class EnemyDropTable2D : MonoBehaviour
{
    [Serializable]
    private class DropEntry
    {
        [Tooltip("落とすアイテムデータ。Ammo / Consumable / Rope など、ItemData派生なら設定できます")]
        [SerializeField] private ItemData itemData;

        [Tooltip("このアイテムを落とす確率。1 = 100%、0.25 = 25%")]
        [SerializeField, Range(0f, 1f)] private float dropChance = 1f;

        [Tooltip("抽選に成功した時に落とす最小個数")]
        [SerializeField, Min(1)] private int minimumAmount = 1;

        [Tooltip("抽選に成功した時に落とす最大個数")]
        [SerializeField, Min(1)] private int maximumAmount = 1;

        public ItemData ItemData => itemData;
        public float DropChance => dropChance;
        public int MinimumAmount => Mathf.Max(1, minimumAmount);
        public int MaximumAmount => Mathf.Max(MinimumAmount, maximumAmount);

        public void Validate()
        {
            dropChance = Mathf.Clamp01(dropChance);
            minimumAmount = Mathf.Max(1, minimumAmount);
            maximumAmount = Mathf.Max(minimumAmount, maximumAmount);
        }
    }

    [Header("落とすPrefab")]
    [Tooltip("PlayerItemDropper に設定している DroppedItem Prefab と同じものを設定します。WorldItemPickup が付いている必要があります")]
    [SerializeField] private WorldItemPickup droppedItemPrefab;

    [Header("ドロップ内容")]
    [SerializeField] private DropEntry[] dropEntries = Array.Empty<DropEntry>();

    [Header("生成位置")]
    [Tooltip("未設定なら、この敵のColliderの中心付近から落とします")]
    [SerializeField] private Transform dropSpawnPoint;

    [SerializeField] private Vector3 dropPositionOffset = new Vector3(0f, 0.15f, 0f);

    [Tooltip("複数ドロップ時に左右へばらける幅")]
    [SerializeField, Min(0f)] private float horizontalScatter = 0.32f;

    [Header("落とした時の動き")]
    [SerializeField] private Vector2 initialDropVelocity = new Vector2(1.2f, 2.1f);

    [Tooltip("上向き速度に加えるランダム幅")]
    [SerializeField, Min(0f)] private float verticalVelocityRandomness = 0.45f;

    [Header("動作設定")]
    [Tooltip("1体の敵から同じドロップを二重に出さないための保護です")]
    [SerializeField] private bool preventDuplicateSpawn = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDropLogs;

    private Collider2D enemyCollider;
    private bool hasSpawnedDrops;

    private void Awake()
    {
        enemyCollider = GetComponent<Collider2D>();
    }

    private void OnEnable()
    {
        // 将来オブジェクトプールで敵を再利用する場合にも、次の死亡時に再ドロップできます。
        hasSpawnedDrops = false;
    }

    /// <summary>
    /// EnemyDeathHandler2Dから呼ばれます。
    /// 手動で呼んでも構いませんが、通常はEnemyDeathHandler2Dに任せてください。
    /// </summary>
    public bool SpawnDrops()
    {
        if (preventDuplicateSpawn && hasSpawnedDrops)
        {
            return false;
        }

        hasSpawnedDrops = true;

        if (droppedItemPrefab == null)
        {
            if (showDropLogs && HasAnyConfiguredEntry())
            {
                Debug.LogWarning(
                    "[EnemyDropTable2D] Dropped Item Prefab が未設定のため、ドロップを生成できません。",
                    this
                );
            }

            return false;
        }

        bool spawnedAny = false;

        foreach (DropEntry entry in dropEntries)
        {
            if (entry == null || entry.ItemData == null)
            {
                continue;
            }

            if (UnityEngine.Random.value > entry.DropChance)
            {
                if (showDropLogs)
                {
                    Debug.Log(
                        $"[EnemyDropTable2D] {entry.ItemData.DisplayName}: ドロップ抽選失敗。",
                        this
                    );
                }

                continue;
            }

            int amount = UnityEngine.Random.Range(
                entry.MinimumAmount,
                entry.MaximumAmount + 1
            );

            spawnedAny |= SpawnItemStacks(entry.ItemData, amount);
        }

        return spawnedAny;
    }

    private bool SpawnItemStacks(ItemData itemData, int amount)
    {
        if (itemData == null || amount <= 0)
        {
            return false;
        }

        int remainingAmount = amount;
        int maxStack = Mathf.Max(1, itemData.MaxStack);
        bool spawnedAny = false;

        while (remainingAmount > 0)
        {
            int stackAmount = Mathf.Min(remainingAmount, maxStack);
            remainingAmount -= stackAmount;

            WorldItemPickup droppedPickup = Instantiate(
                droppedItemPrefab,
                GetRandomDropPosition(),
                Quaternion.identity
            );

            InventoryItem droppedItem = new InventoryItem(
                itemData,
                0,
                0,
                stackAmount
            );

            droppedPickup.Setup(droppedItem);
            droppedPickup.SetVelocity(GetRandomDropVelocity());

            spawnedAny = true;

            if (showDropLogs)
            {
                Debug.Log(
                    $"[EnemyDropTable2D] {itemData.DisplayName} x{stackAmount} をドロップしました。",
                    this
                );
            }
        }

        return spawnedAny;
    }

    private Vector3 GetRandomDropPosition()
    {
        Vector3 basePosition;

        if (dropSpawnPoint != null)
        {
            basePosition = dropSpawnPoint.position;
        }
        else if (enemyCollider != null)
        {
            Bounds bounds = enemyCollider.bounds;
            basePosition = new Vector3(
                bounds.center.x,
                bounds.center.y,
                transform.position.z
            );
        }
        else
        {
            basePosition = transform.position;
        }

        basePosition += dropPositionOffset;
        basePosition.x += UnityEngine.Random.Range(
            -horizontalScatter,
            horizontalScatter
        );

        return basePosition;
    }

    private Vector2 GetRandomDropVelocity()
    {
        return new Vector2(
            UnityEngine.Random.Range(
                -initialDropVelocity.x,
                initialDropVelocity.x
            ),
            initialDropVelocity.y + UnityEngine.Random.Range(
                -verticalVelocityRandomness,
                verticalVelocityRandomness
            )
        );
    }

    private bool HasAnyConfiguredEntry()
    {
        foreach (DropEntry entry in dropEntries)
        {
            if (entry != null && entry.ItemData != null)
            {
                return true;
            }
        }

        return false;
    }

    private void OnValidate()
    {
        horizontalScatter = Mathf.Max(0f, horizontalScatter);
        verticalVelocityRandomness = Mathf.Max(0f, verticalVelocityRandomness);

        if (dropEntries == null)
        {
            dropEntries = Array.Empty<DropEntry>();
            return;
        }

        foreach (DropEntry entry in dropEntries)
        {
            entry?.Validate();
        }
    }
}
