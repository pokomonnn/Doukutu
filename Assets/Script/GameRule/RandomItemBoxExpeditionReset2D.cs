using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 町から「新しい探索」を開始する時などに、
/// 指定Scene・指定RandomGroupのランダムItemBox保存だけを消します。
/// 次回その探索Sceneへ入るとItemBoxSpawnManager2Dが新しく場所と中身を抽選します。
/// </summary>
[DisallowMultipleComponent]
public class RandomItemBoxExpeditionReset2D : MonoBehaviour
{
    [Header("再抽選対象")]
    [Tooltip("ランダムItemBoxを再抽選したい探索Scene名です。")]
    [SerializeField] private string targetSceneName;

    [Tooltip("探索SceneのItemBoxSpawnManager2Dと同じRandom Group Idを指定します。")]
    [SerializeField] private string randomGroupId = "main_item_boxes";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    /// <summary>
    /// Townの「探索開始」Buttonなどから、シーン移動前に呼んでください。
    /// </summary>
    public void PrepareNewExpedition()
    {
        string sceneName = string.IsNullOrWhiteSpace(targetSceneName)
            ? SceneManager.GetActiveScene().name
            : targetSceneName.Trim();

        int removed = ItemBoxSpawnManager2D.ClearSavedBoxesForGroup(
            sceneName,
            randomGroupId
        );

        if (showDebugLogs)
        {
            Debug.Log(
                $"[RandomItemBoxExpeditionReset2D] " +
                $"新規探索用にランダム箱保存を削除しました。" +
                $"Scene={sceneName} / Group={randomGroupId} / Removed={removed}",
                this
            );
        }
    }

    private void OnValidate()
    {
        targetSceneName = targetSceneName?.Trim() ?? string.Empty;
        randomGroupId = ItemBoxSpawnManager2D.NormalizeToken(randomGroupId);

        if (string.IsNullOrWhiteSpace(randomGroupId))
        {
            randomGroupId = "main_item_boxes";
        }
    }
}
