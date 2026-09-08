using UnityEngine;

[CreateAssetMenu(fileName = "NewStageExpeditionData", menuName = "Game/Stage Expedition Data")]
public class StageExpeditionData : ScriptableObject
{
    [Header("ステージ識別")]
    [SerializeField] private string stageId = "stage_01";
    [SerializeField] private string stageName = "第一洞窟";

    [Header("危険度（1～5）")]
    [SerializeField, Range(1, 5)] private int combatDanger = 1;
    [SerializeField, Range(1, 5)] private int caveDanger = 1;
    [SerializeField, Range(1, 5)] private int requiredSupplies = 1;

    public string StageId => stageId;
    public string StageName => stageName;
    public int CombatDanger => Mathf.Clamp(combatDanger, 1, 5);
    public int CaveDanger => Mathf.Clamp(caveDanger, 1, 5);
    public int RequiredSupplies => Mathf.Clamp(requiredSupplies, 1, 5);

    private void OnValidate()
    {
        stageId = stageId?.Trim() ?? string.Empty;
        stageName = stageName?.Trim() ?? string.Empty;
        combatDanger = Mathf.Clamp(combatDanger, 1, 5);
        caveDanger = Mathf.Clamp(caveDanger, 1, 5);
        requiredSupplies = Mathf.Clamp(requiredSupplies, 1, 5);
    }
}
