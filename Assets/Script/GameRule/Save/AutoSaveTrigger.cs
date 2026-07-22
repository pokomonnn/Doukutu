using UnityEngine;

/// <summary>
/// 将来のオートセーブ実装で使用する呼び出し口です。
/// 現時点では自動実行せず、町への到着・睡眠完了・ミッション報酬受取などから
/// RequestAutoSave()を呼ぶと、手動20枠とは別のautosave.jsonへ保存します。
/// </summary>
[DisallowMultipleComponent]
public class AutoSaveTrigger : MonoBehaviour
{
    [SerializeField] private bool showDebugLogs = true;

    public void RequestAutoSave()
    {
        SaveManager manager = SaveManager.Instance;
        if (manager == null)
        {
            manager = FindAnyObjectByType<SaveManager>(
                FindObjectsInactive.Include
            );
        }

        if (manager == null)
        {
            Debug.LogWarning(
                "[AutoSaveTrigger] SaveManagerが見つかりません。",
                this
            );
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log("[AutoSaveTrigger] オートセーブを実行します。", this);
        }

        manager.SaveAutoGame();
    }
}
