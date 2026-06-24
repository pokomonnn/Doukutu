using UnityEngine;

public class BulletDestroyOnLayer : MonoBehaviour
{
    [Header("弾が当たると消えるレイヤー")]
    [SerializeField] private LayerMask destroyLayers;

    [Header("着弾エフェクト")]
    [Tooltip("壁・地面に当たった時に出すParticle SystemやアニメーションPrefab")]
    [SerializeField] private GameObject hitEffectPrefab;

    private Rigidbody2D bulletRb;
    private bool hasHit;

    private void Awake()
    {
        bulletRb = GetComponent<Rigidbody2D>();
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasHit)
        {
            return;
        }

        if (!IsDestroyTarget(collision.collider.gameObject))
        {
            return;
        }

        ContactPoint2D contact = collision.GetContact(0);

        CreateHitEffect(
            contact.point,
            contact.normal
        );

        DestroyBullet();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (hasHit)
        {
            return;
        }

        if (!IsDestroyTarget(other.gameObject))
        {
            return;
        }

        Vector2 hitPoint = other.ClosestPoint(transform.position);

        Vector2 hitNormal = GetHitNormal(hitPoint);

        CreateHitEffect(hitPoint, hitNormal);

        DestroyBullet();
    }

    private bool IsDestroyTarget(GameObject hitObject)
    {
        return (destroyLayers.value & (1 << hitObject.layer)) != 0;
    }

    private Vector2 GetHitNormal(Vector2 hitPoint)
    {
        Vector2 normal = (Vector2)transform.position - hitPoint;

        if (normal.sqrMagnitude > 0.001f)
        {
            return normal.normalized;
        }

        if (bulletRb != null && bulletRb.linearVelocity.sqrMagnitude > 0.001f)
        {
            return -bulletRb.linearVelocity.normalized;
        }

        return Vector2.up;
    }

    private void CreateHitEffect(Vector2 hitPosition, Vector2 hitNormal)
    {
        if (hitEffectPrefab == null)
        {
            return;
        }

        // エフェクトPrefabが右向きを基準に作られている場合、
        // 壁や地面の向きに合わせて回転する
        float angle = Mathf.Atan2(hitNormal.y, hitNormal.x) * Mathf.Rad2Deg;

        Instantiate(
            hitEffectPrefab,
            hitPosition,
            Quaternion.Euler(0f, 0f, angle)
        );
    }

    private void DestroyBullet()
    {
        hasHit = true;
        Destroy(gameObject);
    }
}