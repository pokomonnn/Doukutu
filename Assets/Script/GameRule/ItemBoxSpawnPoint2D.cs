using System;
using UnityEngine;

/// <summary>
/// ItemBoxをランダム生成してよい候補地点です。
/// SpawnPointIdはセーブ復元時に使うため、一度配置したら変更しないでください。
/// </summary>
[DisallowMultipleComponent]
public class ItemBoxSpawnPoint2D : MonoBehaviour
{
    [Header("識別")]
    [Tooltip("このSpawnPoint固有のIDです。重複しないようにしてください。")]
    [SerializeField] private string spawnPointId = string.Empty;

    [Header("生成位置")]
    [SerializeField] private Vector3 localSpawnOffset = Vector3.zero;
    [SerializeField] private bool useSpawnPointRotation = true;

    public string SpawnPointId => spawnPointId?.Trim() ?? string.Empty;

    public Vector3 SpawnPosition =>
        transform.TransformPoint(localSpawnOffset);

    public Quaternion SpawnRotation =>
        useSpawnPointRotation
            ? transform.rotation
            : Quaternion.identity;

    [ContextMenu("Generate New Spawn Point Id")]
    public void GenerateNewSpawnPointId()
    {
        spawnPointId = Guid.NewGuid().ToString("N");

#if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(this);
#endif
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(spawnPointId))
        {
            spawnPointId = Guid.NewGuid().ToString("N");
        }

        spawnPointId = spawnPointId.Trim();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(SpawnPosition, 0.18f);
        Gizmos.DrawLine(transform.position, SpawnPosition);
    }
#endif
}
