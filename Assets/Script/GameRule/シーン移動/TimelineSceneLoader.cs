using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class TimelineSceneLoader : MonoBehaviour
{
    [Header("移動先")]
    [Tooltip("Timeline終了後に移動するシーン名")]
    [SerializeField]
    private string nextSceneName = "Town_Main";

    private bool isLoading;

    /// <summary>
    /// TimelineのSignalから呼び出します。
    /// </summary>
    public void LoadNextScene()
    {
        if (isLoading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(nextSceneName))
        {
            Debug.LogError(
                $"{nameof(TimelineSceneLoader)}: " +
                "移動先のシーン名が設定されていません。",
                this
            );

            return;
        }

        isLoading = true;

        SceneManager.LoadScene(
            nextSceneName,
            LoadSceneMode.Single
        );
    }
}