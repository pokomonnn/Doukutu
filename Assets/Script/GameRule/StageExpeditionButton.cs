using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(Button))]
public class StageExpeditionButton : MonoBehaviour
{
    [Header("このButtonのステージ")]
    [SerializeField] private StageExpeditionData stageData;

    [Header("共通の出撃パネル")]
    [SerializeField] private StageExpeditionPanelController expeditionPanel;

    [Header("既存の安全なシーン遷移")]
    [Tooltip("現在このステージ移動に使っているSceneTransitionButtonを設定します。出発するButtonを押した時だけLoadConfiguredScene()を呼びます。")]
    [SerializeField] private SceneTransitionButton sceneTransitionButton;

    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        if (button != null) button.onClick.AddListener(HandleClicked);
    }

    private void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(HandleClicked);
    }

    private void HandleClicked()
    {
        if (stageData == null)
        {
            Debug.LogWarning($"[StageExpeditionButton] {name} にStageExpeditionDataが設定されていません。", this);
            return;
        }
        if (expeditionPanel == null)
        {
            Debug.LogWarning($"[StageExpeditionButton] {name} にStageExpeditionPanelControllerが設定されていません。", this);
            return;
        }
        if (sceneTransitionButton == null)
        {
            Debug.LogWarning($"[StageExpeditionButton] {name} にSceneTransitionButtonが設定されていません。", this);
            return;
        }

        expeditionPanel.Open(stageData, sceneTransitionButton);
    }

    private void Reset()
    {
        sceneTransitionButton = GetComponent<SceneTransitionButton>();
    }
}
