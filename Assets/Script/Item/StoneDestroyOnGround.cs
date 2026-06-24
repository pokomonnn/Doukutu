using UnityEngine;

public class StoneDestroyOnGround : MonoBehaviour
{
    [Header("消える設定")]
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float destroyDelay = 10f;

    private bool hasHitGround = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasHitGround) return;

        // 当たった相手がGroundレイヤーか確認
        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            hasHitGround = true;
            Destroy(gameObject, destroyDelay);
        }
    }
}