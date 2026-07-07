using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 町の一枚絵に重ねた透明Buttonへ付けます。
/// クリックすると、指定した住人会話を開始します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class TownResidentBuildingButton : MonoBehaviour
{
    [Header("会話")]
    [Tooltip("未設定ならシーン内から探します")]
    [SerializeField] private TownDialogueController dialogueController;

    [SerializeField] private TownResidentDialogueData residentDialogue;

    [Tooltip("通常は0。特定の会話ページから始めたい場合だけ変更します")]
    [SerializeField, Min(0)] private int startNodeIndex;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private Button buildingButton;

    private void Awake()
    {
        FindReferences();

        if (buildingButton != null)
        {
            buildingButton.onClick.RemoveListener(OpenResidentDialogue);
            buildingButton.onClick.AddListener(OpenResidentDialogue);
        }
    }

    private void OnDestroy()
    {
        if (buildingButton != null)
        {
            buildingButton.onClick.RemoveListener(OpenResidentDialogue);
        }
    }

    /// <summary>
    /// ButtonのOnClickへ手動登録したい場合にも使えます。
    /// </summary>
    public void OpenResidentDialogue()
    {
        FindReferences();

        if (dialogueController == null)
        {
            LogWarning(
                "TownDialogueController が見つかりません。" +
                "TownCanvasのTownDialogueSystemへ付けてください。"
            );
            return;
        }

        if (residentDialogue == null)
        {
            LogWarning(
                "Resident Dialogue が未設定です。"
            );
            return;
        }

        dialogueController.OpenDialogue(
            residentDialogue,
            startNodeIndex
        );

        Log($"建物クリック: {residentDialogue.ResidentName}");
    }

    private void FindReferences()
    {
        if (buildingButton == null)
        {
            buildingButton = GetComponent<Button>();
        }

        if (dialogueController == null)
        {
            dialogueController =
                FindAnyObjectByType<TownDialogueController>();
        }
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log($"[TownResidentBuildingButton] {message}", this);
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[TownResidentBuildingButton] {message}", this);
    }
}
