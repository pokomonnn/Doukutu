using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 町の透明ボタンに付ける、ミッション住人専用ボタンです。
/// 既存のTownResidentBuildingButtonではなく、ミッションをくれる住人にはこちらを使います。
/// </summary>
[RequireComponent(typeof(Button))]
[DisallowMultipleComponent]
public class TownMissionResidentButton : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private TownMissionResidentDialogueController dialogueController;
    [SerializeField] private TownMissionResidentDialogueData dialogueData;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();

        if (dialogueController == null)
        {
            dialogueController = FindAnyObjectByType<TownMissionResidentDialogueController>();
        }
    }

    private void OnEnable()
    {
        if (button == null)
        {
            button = GetComponent<Button>();
        }

        button.onClick.AddListener(OpenResidentDialogue);
    }

    private void OnDisable()
    {
        if (button != null)
        {
            button.onClick.RemoveListener(OpenResidentDialogue);
        }
    }

    public void OpenResidentDialogue()
    {
        if (dialogueController == null)
        {
            dialogueController = FindAnyObjectByType<TownMissionResidentDialogueController>();
        }

        if (dialogueController == null)
        {
            Debug.LogWarning(
                "[TownMissionResidentButton] TownMissionResidentDialogueControllerが見つかりません。TownCanvasなどに追加してください。",
                this
            );
            return;
        }

        if (dialogueData == null)
        {
            Debug.LogWarning(
                "[TownMissionResidentButton] Dialogue Dataが未設定です。村人用のTownMissionResidentDialogueDataを設定してください。",
                this
            );
            return;
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TownMissionResidentButton] ミッション住人会話を開きます: {dialogueData.ResidentName}",
                this
            );
        }

        dialogueController.OpenDialogue(dialogueData);
    }
}
