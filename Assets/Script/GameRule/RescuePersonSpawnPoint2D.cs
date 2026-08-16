using System;
using UnityEngine;

/// <summary>
/// 救出対象NPCをランダム生成してよい候補地点です。
/// SpawnPointIdは識別用なので、配置後は重複・変更しないことを推奨します。
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonSpawnPoint2D : MonoBehaviour
{
    [Header("識別")]
    [Tooltip("このSpawnPoint固有のIDです。同じManager内で重複させないでください。")]
    [SerializeField] private string spawnPointId = string.Empty;

    [Header("生成位置")]
    [Tooltip("Transform原点から少しずらして生成したい場合に使います。")]
    [SerializeField] private Vector3 localSpawnOffset = Vector3.zero;

    [Tooltip("ONならSpawnPoint自身の回転をNPCへ適用します。OFFなら回転なしで生成します。")]
    [SerializeField] private bool useSpawnPointRotation = true;

    [Header("使用設定")]
    [SerializeField] private bool canSpawnHere = true;

    public string SpawnPointId => spawnPointId?.Trim() ?? string.Empty;
    public bool CanSpawnHere => canSpawnHere;

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
    private void OnDrawGizmos()
    {
        Gizmos.DrawWireSphere(SpawnPosition, 0.22f);
        Gizmos.DrawLine(transform.position, SpawnPosition);
    }
#endif
}
