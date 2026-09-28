using UnityEngine;

/// <summary>
/// 洞窟商人の出現候補地点です。
/// Weightを大きくすると選ばれやすくなります。
/// </summary>
public class CaveMerchantSpawnPoint2D : MonoBehaviour
{
    [SerializeField, Min(0f)] private float weight = 1f;
    [SerializeField] private bool allowSpawn = true;

    public float Weight => Mathf.Max(0f, weight);
    public bool AllowSpawn => allowSpawn;

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, 0.35f);
        Gizmos.DrawLine(
            transform.position + Vector3.left * 0.5f,
            transform.position + Vector3.right * 0.5f
        );
    }
}
