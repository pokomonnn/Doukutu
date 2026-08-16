using UnityEngine;

/// <summary>
/// RescuePersonSpawnManager2Dから生成されたNPCへ自動で付与される識別用コンポーネントです。
/// 会話・救出処理・デバッグから「どの候補地点に生成されたか」を取得できます。
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonSpawnedMarker2D : MonoBehaviour
{
    public RescuePersonSpawnManager2D SpawnManager { get; private set; }
    public string SpawnPointId { get; private set; } = string.Empty;
    public string ProfileId { get; private set; } = string.Empty;

    public void Initialize(
        RescuePersonSpawnManager2D manager,
        string spawnPointId,
        string profileId)
    {
        SpawnManager = manager;
        SpawnPointId = spawnPointId?.Trim() ?? string.Empty;
        ProfileId = profileId?.Trim() ?? string.Empty;
    }
}
