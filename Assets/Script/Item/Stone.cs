using UnityEngine;

public class Stone : MonoBehaviour
{
    [SerializeField] private LayerMask groundLayer;
    [SerializeField] private float destroyDelay = 20f;

    private bool hasHitGround = false;

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (hasHitGround) return;

        if (((1 << collision.gameObject.layer) & groundLayer) != 0)
        {
            hasHitGround = true;
            Destroy(gameObject, destroyDelay);
        }
    }
}