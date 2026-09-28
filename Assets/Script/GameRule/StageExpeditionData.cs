using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;


[Serializable]
public class StageExpeditionEntryPointData
{
    [Tooltip(
        "出撃地点を識別するIDです。Stage側のStageExpeditionSpawnPointと同じIDにしてください。" +
        "例：entry_01"
    )]
    [InspectorName("出撃地点ID")]
    [SerializeField]
    private string entryPointId = "entry_01";

    [Tooltip("出撃パネルのButton名や選択中の地点名として表示します。")]
    [InspectorName("表示名")]
    [SerializeField]
    private string displayName = "出撃地点";

    [Tooltip("この出撃地点を選択した時に出撃パネルへ表示する画像です。")]
    [InspectorName("プレビュー画像")]
    [SerializeField]
    private Sprite previewImage;

    public string EntryPointId =>
        entryPointId?.Trim() ?? string.Empty;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? EntryPointId
            : displayName.Trim();

    public Sprite PreviewImage => previewImage;

    public void Validate()
    {
        entryPointId = entryPointId?.Trim() ?? string.Empty;
        displayName = displayName?.Trim() ?? string.Empty;
    }
}

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

    [Header("出撃地点")]
    [Tooltip(
        "このステージで選択できる出撃地点一覧です。" +
        "Stage1で4か所使う場合はSizeを4にして、entry_01～entry_04などを設定してください。"
    )]
    [InspectorName("出撃地点一覧")]
    [SerializeField]
    private List<StageExpeditionEntryPointData> entryPoints =
        new List<StageExpeditionEntryPointData>();

    [Header("ランダムミッション")]
    [Tooltip(
        "この洞窟で出現できるMissionDefinition2Dの候補一覧です。\n" +
        "出撃パネルを最初に開いた時、この中から重複なしでランダム抽選します。"
    )]
    [FormerlySerializedAs("activeMissions")]
    [SerializeField]
    private List<MissionDefinition2D> missionPool =
        new List<MissionDefinition2D>();

    [Tooltip(
        "1回の出撃候補として表示するミッション数です。\n" +
        "Mission Poolの有効な件数がこれより少ない場合は、存在する分だけ表示します。"
    )]
    [SerializeField, Min(1)]
    private int randomMissionCount = 4;

    public string StageId => stageId;
    public string StageName => stageName;
    public int CombatDanger => Mathf.Clamp(combatDanger, 1, 5);
    public int CaveDanger => Mathf.Clamp(caveDanger, 1, 5);
    public int RequiredSupplies => Mathf.Clamp(requiredSupplies, 1, 5);

    public IReadOnlyList<StageExpeditionEntryPointData> EntryPoints =>
        entryPoints;

    public int EntryPointCount =>
        entryPoints != null ? entryPoints.Count : 0;

    public StageExpeditionEntryPointData GetEntryPoint(int index)
    {
        if (entryPoints == null ||
            index < 0 ||
            index >= entryPoints.Count)
        {
            return null;
        }

        return entryPoints[index];
    }

    /// <summary>
    /// この洞窟で抽選対象になるミッション候補です。
    /// 実際に今回表示されるミッションはStageExpeditionPanelController側で抽選します。
    /// </summary>
    public IReadOnlyList<MissionDefinition2D> MissionPool => missionPool;

    public int MissionPoolCount =>
        missionPool != null ? missionPool.Count : 0;

    public int RandomMissionCount =>
        Mathf.Max(1, randomMissionCount);

    public MissionDefinition2D GetMissionPoolEntry(int index)
    {
        if (missionPool == null ||
            index < 0 ||
            index >= missionPool.Count)
        {
            return null;
        }

        return missionPool[index];
    }

    // -----------------------------------------------------------------
    // 前バージョンとの互換用API
    // 旧StageExpeditionPanelControllerなどが残っていてもコンパイルを壊しません。
    // ※これはMission Pool全体を返すため、ランダム抽選結果ではありません。
    // -----------------------------------------------------------------
    public IReadOnlyList<MissionDefinition2D> ActiveMissions => missionPool;

    public int ActiveMissionCount => MissionPoolCount;

    public MissionDefinition2D GetActiveMission(int index)
    {
        return GetMissionPoolEntry(index);
    }

    private void OnValidate()
    {
        stageId = stageId?.Trim() ?? string.Empty;
        stageName = stageName?.Trim() ?? string.Empty;

        combatDanger = Mathf.Clamp(combatDanger, 1, 5);
        caveDanger = Mathf.Clamp(caveDanger, 1, 5);
        requiredSupplies = Mathf.Clamp(requiredSupplies, 1, 5);
        randomMissionCount = Mathf.Max(1, randomMissionCount);

        if (entryPoints == null)
        {
            entryPoints = new List<StageExpeditionEntryPointData>();
        }

        for (int i = 0; i < entryPoints.Count; i++)
        {
            entryPoints[i]?.Validate();
        }

        if (missionPool == null)
        {
            missionPool = new List<MissionDefinition2D>();
        }
    }
}
