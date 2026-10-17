using System.Collections.Generic;
using UnityEngine;

/// <summary>NPCの近くへ置いたWorldItemPickupをレシピに従って交換します。</summary>
[DisallowMultipleComponent]
public sealed class DungeonNpcBarter2D : MonoBehaviour
{
    [Header("交換設定")]
    [SerializeField] private DungeonNpcTradeProfile2D tradeProfile;
    [SerializeField] private WorldItemPickup rewardPickupPrefab;
    [SerializeField, Min(0.2f)] private float detectionRadius = 2f;
    [SerializeField, Min(0f)] private float settleSeconds = 0.5f;
    [SerializeField, Min(0.1f)] private float scanInterval = 0.2f;
    [SerializeField, Min(0f)] private float exchangeCooldown = 1.3f;
    [SerializeField] private Vector2 rewardDropOffset = new Vector2(1f, 0.6f);
    [SerializeField] private Vector2 rewardThrowVelocity = new Vector2(1.2f, 2f);
    [SerializeField] private bool logExchange;

    private readonly Dictionary<int, float> noticedAt = new Dictionary<int, float>();
    private readonly Dictionary<int, int> exchangedByRecipe = new Dictionary<int, int>();
    private readonly HashSet<int> producedRewardIds = new HashSet<int>();
    private float nextScan;
    private float nextExchange;

    private void Update()
    {
        if (tradeProfile == null || rewardPickupPrefab == null || Time.time < nextScan) return;
        nextScan = Time.time + scanInterval;
        if (Time.time < nextExchange) return;

        var pickups = FindObjectsByType<WorldItemPickup>(FindObjectsSortMode.None);
        foreach (var pickup in pickups)
        {
            if (pickup == null || !pickup.isActiveAndEnabled || !pickup.HasValidDroppedItem) continue;
            if (((Vector2)(pickup.transform.position - transform.position)).sqrMagnitude > detectionRadius * detectionRadius) continue;
            var offered = pickup.DroppedItem;
            if (offered == null || offered.ItemData == null) continue;
            int id = pickup.GetInstanceID();
            if (producedRewardIds.Contains(id)) continue;
            if (!noticedAt.TryGetValue(id, out float firstSeen))
            {
                noticedAt[id] = Time.time;
                continue;
            }
            if (Time.time - firstSeen < settleSeconds) continue;
            for (int i = 0; i < tradeProfile.exchanges.Count; i++)
            {
                var exchange = tradeProfile.exchanges[i];
                if (exchange == null || exchange.requestItem == null || exchange.rewardItem == null ||
                    offered.ItemData != exchange.requestItem || offered.Amount < exchange.requestAmount) continue;
                exchangedByRecipe.TryGetValue(i, out int used);
                if (exchange.exchangeLimit > 0 && used >= exchange.exchangeLimit) continue;
                if (TryExchange(pickup, exchange))
                {
                    exchangedByRecipe[i] = used + 1;
                    noticedAt.Remove(id);
                    nextExchange = Time.time + exchangeCooldown;
                    return;
                }
            }
        }
    }

    private bool TryExchange(WorldItemPickup pickup, DungeonNpcTradeProfile2D.Exchange entry)
    {
        // 報酬は1つのWorldItemPickupで表現するためMaxStackを超えるレシピは拒否。
        if (entry.rewardAmount > entry.rewardItem.MaxStack) { Debug.LogWarning("[DungeonNpcBarter] 報酬数がMaxStackを超えています", this); return false; }
        WorldItemPickup reward = null;
        try
        {
            reward = Instantiate(rewardPickupPrefab, transform.position + (Vector3)rewardDropOffset, Quaternion.identity);
            if (reward == null) return false;
            reward.Setup(new InventoryItem(entry.rewardItem, 0, 0, entry.rewardAmount), false);
            reward.SetVelocity(rewardThrowVelocity);
            producedRewardIds.Add(reward.GetInstanceID());
        }
        catch (System.Exception ex)
        {
            if (reward != null) Destroy(reward.gameObject);
            Debug.LogException(ex, this);
            return false;
        }

        InventoryItem offered = pickup.DroppedItem;
        int consumed = offered.RemoveAmount(entry.requestAmount);
        if (consumed != entry.requestAmount)
        {
            Destroy(reward.gameObject);
            Debug.LogWarning("[DungeonNpcBarter] 消費数が不一致のため報酬を取り消しました", this);
            return false;
        }
        if (offered.IsEmpty()) Destroy(pickup.gameObject);
        else pickup.Setup(offered, false);
        if (logExchange) Debug.Log($"[DungeonNpcBarter] {entry.requestItem.DisplayName} x{consumed} -> {entry.rewardItem.DisplayName} x{entry.rewardAmount}", this);
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
