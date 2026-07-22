using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// シーン再読込後も、その時点で生存しているSaveManagerを探して
/// セーブ・ロード・削除を実行するButton用コンポーネントです。
///
/// ButtonのInspectorにSaveManagerを直接登録しないため、
/// シーン移動後のMissing参照や古いScene Object参照を防げます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class SaveLoadButton : MonoBehaviour
{
    public enum SaveButtonAction
    {
        Save,
        Load,
        Delete
    }

    [Header("実行内容")]
    [SerializeField] private SaveButtonAction action = SaveButtonAction.Save;

    [Tooltip("オンならSaveManagerのDefault Slot Numberを使います。")]
    [SerializeField] private bool useDefaultSlot = true;

    [Tooltip("Use Default Slotがオフの時に使用します。")]
    [SerializeField, Min(1)] private int slotNumber = 1;

    [Header("参照")]
    [Tooltip("未設定なら同じGameObjectのButtonを取得します。")]
    [SerializeField] private Button targetButton;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private bool listenerRegistered;

    private void Awake()
    {
        FindButton();
    }

    private void OnEnable()
    {
        RegisterListener();
    }

    private void OnDisable()
    {
        UnregisterListener();
    }

    private void OnDestroy()
    {
        UnregisterListener();
    }

    public void Execute()
    {
        SaveManager manager = FindCurrentSaveManager();

        if (manager == null)
        {
            Debug.LogError(
                "[SaveLoadButton] SaveManagerが見つかりません。" +
                "最初のシーンにSaveManagerを置き、Dont Destroy On Loadをオンにしてください。",
                this
            );
            return;
        }

        int resolvedSlot = useDefaultSlot
            ? manager.DefaultSlotNumber
            : Mathf.Max(1, slotNumber);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[SaveLoadButton] Button実行：Action={action} / " +
                $"Slot={resolvedSlot} / Scene={gameObject.scene.name} / " +
                $"SaveManager={manager.name}",
                this
            );
        }

        switch (action)
        {
            case SaveButtonAction.Save:
                manager.SaveSlot(resolvedSlot);
                break;

            case SaveButtonAction.Load:
                manager.LoadSlot(resolvedSlot);
                break;

            case SaveButtonAction.Delete:
                manager.DeleteSlot(resolvedSlot);
                break;
        }
    }

    private SaveManager FindCurrentSaveManager()
    {
        if (SaveManager.Instance != null)
        {
            return SaveManager.Instance;
        }

        return FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include);
    }

    private void FindButton()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }
    }

    private void RegisterListener()
    {
        FindButton();

        if (targetButton == null || listenerRegistered)
        {
            return;
        }

        targetButton.onClick.AddListener(Execute);
        listenerRegistered = true;
    }

    private void UnregisterListener()
    {
        if (targetButton == null || !listenerRegistered)
        {
            return;
        }

        targetButton.onClick.RemoveListener(Execute);
        listenerRegistered = false;
    }

    private void OnValidate()
    {
        slotNumber = Mathf.Max(1, slotNumber);

        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }
    }
}
