using System;
using UnityEngine;

/// <summary>
/// 探索Stage Scene内に置く、出撃地点用のSpawnPointです。
/// StageExpeditionDataのStage IDと出撃地点IDを一致させて使用します。
/// </summary>
[DisallowMultipleComponent]
public class StageExpeditionSpawnPoint : MonoBehaviour
{
    [Header("出撃地点識別")]
    [Tooltip("対応するStageExpeditionDataのStage IDと同じ値にしてください。例：stage_01")]
    [InspectorName("ステージID")]
    [SerializeField]
    private string stageId = "stage_01";

    [Tooltip("StageExpeditionDataに設定した出撃地点IDと同じ値にしてください。例：entry_01")]
    [InspectorName("出撃地点ID")]
    [SerializeField]
    private string entryPointId = "entry_01";

    [Header("生成位置")]
    [Tooltip("実際にPlayerを置くTransformです。未設定ならこのGameObject自身の位置を使用します。")]
    [InspectorName("生成位置")]
    [SerializeField]
    private Transform spawnTransform;

    public string StageId =>
        stageId?.Trim() ?? string.Empty;

    public string EntryPointId =>
        entryPointId?.Trim() ?? string.Empty;

    public Transform SpawnTransform =>
        spawnTransform != null
            ? spawnTransform
            : transform;

    public Vector3 SpawnPosition =>
        SpawnTransform.position;

    public bool Matches(
        string targetStageId,
        string targetEntryPointId)
    {
        if (string.IsNullOrWhiteSpace(targetStageId) ||
            string.IsNullOrWhiteSpace(targetEntryPointId))
        {
            return false;
        }

        return
            string.Equals(
                StageId,
                targetStageId.Trim(),
                StringComparison.OrdinalIgnoreCase
            ) &&
            string.Equals(
                EntryPointId,
                targetEntryPointId.Trim(),
                StringComparison.OrdinalIgnoreCase
            );
    }

    private void OnValidate()
    {
        stageId = stageId?.Trim() ?? string.Empty;
        entryPointId = entryPointId?.Trim() ?? string.Empty;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Vector3 position =
            spawnTransform != null
                ? spawnTransform.position
                : transform.position;

        Gizmos.DrawWireSphere(position, 0.35f);
        Gizmos.DrawLine(
            position + Vector3.left * 0.5f,
            position + Vector3.right * 0.5f
        );
    }
#endif
}
