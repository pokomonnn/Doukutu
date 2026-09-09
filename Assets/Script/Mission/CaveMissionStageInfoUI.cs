using TMPro;
using UnityEngine;

/// <summary>
/// 洞窟内のMissionMenuPanelへ、出撃パネルと同じステージ情報を表示します。
/// 各洞窟シーンで、その洞窟に対応するStageExpeditionDataを設定してください。
/// </summary>
[DisallowMultipleComponent]
public class CaveMissionStageInfoUI : MonoBehaviour
{
    [Header("この洞窟のデータ")]
    [SerializeField] private StageExpeditionData stageData;

    [Header("表示Text")]
    [SerializeField] private TMP_Text stageNameText;
    [SerializeField] private TMP_Text combatDangerText;
    [SerializeField] private TMP_Text caveDangerText;
    [SerializeField] private TMP_Text requiredSuppliesText;

    [Header("表示設定")]
    [Tooltip("ONならOnEnable時にも再表示します")]
    [SerializeField] private bool refreshOnEnable = true;

    public StageExpeditionData StageData => stageData;

    private void Awake()
    {
        Refresh();
    }

    private void OnEnable()
    {
        if (refreshOnEnable)
        {
            Refresh();
        }
    }

    public void SetStageData(StageExpeditionData data)
    {
        stageData = data;
        Refresh();
    }

    [ContextMenu("Refresh Stage Info")]
    public void Refresh()
    {
        if (stageData == null)
        {
            SetText(stageNameText, "洞窟未設定");
            SetText(combatDangerText, "☆☆☆☆☆");
            SetText(caveDangerText, "☆☆☆☆☆");
            SetText(requiredSuppliesText, "☆☆☆☆☆");
            return;
        }

        SetText(stageNameText, stageData.StageName);
        SetText(combatDangerText, BuildStars(stageData.CombatDanger));
        SetText(caveDangerText, BuildStars(stageData.CaveDanger));
        SetText(requiredSuppliesText, BuildStars(stageData.RequiredSupplies));
    }

    private static string BuildStars(int value)
    {
        const int maxStars = 5;
        int clamped = Mathf.Clamp(value, 1, maxStars);

        return new string('★', clamped) +
               new string('☆', maxStars - clamped);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value ?? string.Empty;
        }
    }
}
