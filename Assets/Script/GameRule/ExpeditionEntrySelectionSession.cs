using UnityEngine;

/// <summary>
/// 町の出撃パネルで最後に選択した出撃地点を、
/// Town Scene → 探索Stage SceneのScene移動中に保持します。
///
/// セーブデータではなく「現在の出撃」の一時データです。
/// StageExpeditionPanelControllerが出撃直前に設定し、
/// GameManagerがStage開始位置の決定に使用します。
/// </summary>
public static class ExpeditionEntrySelectionSession
{
    private static string selectedStageId = string.Empty;
    private static string selectedEntryPointId = string.Empty;
    private static string selectedDisplayName = string.Empty;

    public static bool HasSelection =>
        !string.IsNullOrWhiteSpace(selectedStageId) &&
        !string.IsNullOrWhiteSpace(selectedEntryPointId);

    public static string SelectedStageId =>
        selectedStageId;

    public static string SelectedEntryPointId =>
        selectedEntryPointId;

    public static string SelectedDisplayName =>
        selectedDisplayName;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        Clear();
    }

    public static bool SetSelection(
        string stageId,
        string entryPointId,
        string displayName = "")
    {
        string normalizedStageId =
            stageId?.Trim() ?? string.Empty;

        string normalizedEntryPointId =
            entryPointId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(normalizedStageId) ||
            string.IsNullOrWhiteSpace(normalizedEntryPointId))
        {
            Clear();
            return false;
        }

        selectedStageId = normalizedStageId;
        selectedEntryPointId = normalizedEntryPointId;
        selectedDisplayName =
            displayName?.Trim() ?? string.Empty;

        return true;
    }

    public static bool TryGetSelection(
        out string stageId,
        out string entryPointId,
        out string displayName)
    {
        stageId = selectedStageId;
        entryPointId = selectedEntryPointId;
        displayName = selectedDisplayName;

        return HasSelection;
    }

    public static void Clear()
    {
        selectedStageId = string.Empty;
        selectedEntryPointId = string.Empty;
        selectedDisplayName = string.Empty;
    }
}
