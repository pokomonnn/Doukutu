using TMPro;
using UnityEngine;

/// <summary>
/// 町シーンの会話やボタンからミッションを受注し、GameSessionManagerへ保存します。
/// 町シーンにMissionManager2Dが無くても受注でき、探索シーンへ戻った時にMissionSessionBridgeが反映します。
/// </summary>
[DisallowMultipleComponent]
public class TownMissionAcceptController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定ならGameSessionManager.Instance、またはシーン内から探します")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Tooltip("結果メッセージを出したいText。不要なら空欄でOKです")]
    [SerializeField] private TMP_Text statusText;

    [Header("ボタンから直接受注する場合")]
    [SerializeField] private MissionDefinition2D missionToAccept;
    [SerializeField] private bool trackAfterAccepting = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    /// <summary>
    /// ButtonのOnClickから直接使う場合のメソッドです。
    /// 会話選択肢から使う場合はAcceptMissionを呼びます。
    /// </summary>
    public void AcceptConfiguredMission()
    {
        AcceptMission(
            missionToAccept,
            trackAfterAccepting,
            out _
        );
    }

    public bool AcceptMission(
        MissionDefinition2D mission,
        bool trackMission,
        out string resultMessage)
    {
        resultMessage = string.Empty;
        FindReferences();

        if (gameSessionManager == null)
        {
            resultMessage =
                "GameSessionManagerが見つかりません。開始シーンからTown_Mainへ移動しているか確認してください。";
            SetStatusMessage(resultMessage, true);
            return false;
        }

        bool accepted = gameSessionManager.AcceptMission(
            mission,
            trackMission,
            out resultMessage
        );

        SetStatusMessage(resultMessage, !accepted);
        return accepted;
    }

    private void FindReferences()
    {
        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = FindAnyObjectByType<GameSessionManager>();
        }
    }

    private void SetStatusMessage(string message, bool warning)
    {
        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
            statusText.gameObject.SetActive(!string.IsNullOrWhiteSpace(message));
        }

        if (!showDebugLogs || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (warning)
        {
            Debug.LogWarning($"[TownMissionAcceptController] {message}", this);
        }
        else
        {
            Debug.Log($"[TownMissionAcceptController] {message}", this);
        }
    }
}
