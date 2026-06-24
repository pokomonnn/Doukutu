using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class DamageDealer : MonoBehaviour
{
    [Header("Damage Settings")]
    [SerializeField, Min(1)] private int damage = 10;

    [Tooltip("ダメージを与えられる対象のLayer")]
    [SerializeField] private LayerMask targetLayers;

    [Header("Hit Settings")]
    [Tooltip("同じ相手に1回だけダメージを与える")]
    [SerializeField] private bool hitOnlyOncePerTarget = true;

    [Tooltip("ダメージを与えたら、このオブジェクトを消す（弾用）")]
    [SerializeField] private bool destroyOnHit = true;

    private readonly HashSet<CharacterHealth> damagedTargets = new();

    private void Reset()
    {
        Collider2D col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnEnable()
    {
        damagedTargets.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryDealDamage(other);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        // Trigger内に入った瞬間にHealthが見つからなかった場合などの保険
        TryDealDamage(other);
    }

    private void TryDealDamage(Collider2D other)
    {
        CharacterHealth targetHealth = other.GetComponentInParent<CharacterHealth>();

        if (targetHealth == null || targetHealth.IsDead)
        {
            return;
        }

        // CharacterHealthが付いている親オブジェクトのLayerで判定する
        int targetLayer = targetHealth.gameObject.layer;

        if ((targetLayers.value & (1 << targetLayer)) == 0)
        {
            return;
        }

        if (hitOnlyOncePerTarget && damagedTargets.Contains(targetHealth))
        {
            return;
        }

        targetHealth.TakeDamage(damage);
        damagedTargets.Add(targetHealth);

        if (destroyOnHit)
        {
            Destroy(gameObject);
        }
    }
}