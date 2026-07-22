using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// シーンを往復しても古いSaveManager参照を保持しない、
/// セーブ／ロードメニューを開くためのButton補助です。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class SaveSlotMenuOpenButton : MonoBehaviour
{
    [SerializeField] private SaveSlotMenuController targetMenu;
    [SerializeField] private SaveSlotMenuMode openMode = SaveSlotMenuMode.Save;
    [SerializeField] private bool wireButtonAutomatically = true;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (wireButtonAutomatically)
        {
            button.onClick.AddListener(Open);
        }
    }

    private void OnDestroy()
    {
        if (wireButtonAutomatically && button != null)
        {
            button.onClick.RemoveListener(Open);
        }
    }

    public void Open()
    {
        if (targetMenu == null)
        {
            Debug.LogWarning(
                "[SaveSlotMenuOpenButton] Target Menuが設定されていません。",
                this
            );
            return;
        }

        targetMenu.Open(openMode);
    }
}
