using System;
using System.Collections.Generic;

/// <summary>
/// 「今回の出撃でエレベーターへ積み込めた特殊NPC」を
/// 洞窟Scene → Result Sceneの間だけ保持します。
///
/// 永久保存はRescueProgressionControllerがGameSessionManagerへ確定します。
/// 新しい出撃開始時にはGameSessionManager.BeginCurrentExpedition()からClearされます。
/// </summary>
public static class RescueProgressExpeditionSession
{
    [Serializable]
    public sealed class PendingSpecialRescue
    {
        public string SpecialNpcId = string.Empty;
        public string DisplayName = string.Empty;
        public string UnlockStoryFlagId = string.Empty;
        public string FacilityId = string.Empty;
        public int FacilityLevel = 1;
    }

    private static readonly List<PendingSpecialRescue> pendingSpecialRescues =
        new List<PendingSpecialRescue>();

    public static IReadOnlyList<PendingSpecialRescue> PendingSpecialRescues =>
        pendingSpecialRescues;

    public static int PendingSpecialRescueCount =>
        pendingSpecialRescues.Count;

    /// <summary>
    /// 同じ特殊NPC IDは1出撃につき1件だけ登録します。
    /// </summary>
    public static bool Register(
        RescuePersonUnlockProfile2D profile)
    {
        if (profile == null ||
            !profile.HasValidId)
        {
            return false;
        }

        string id =
            profile.SpecialNpcId;

        foreach (PendingSpecialRescue data in
                 pendingSpecialRescues)
        {
            if (data != null &&
                string.Equals(
                    data.SpecialNpcId,
                    id,
                    StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        pendingSpecialRescues.Add(
            new PendingSpecialRescue
            {
                SpecialNpcId =
                    id,
                DisplayName =
                    profile.DisplayName,
                UnlockStoryFlagId =
                    profile.UnlockStoryFlagId,
                FacilityId =
                    profile.FacilityId,
                FacilityLevel =
                    profile.FacilityLevel
            }
        );

        return true;
    }

    public static void Clear()
    {
        pendingSpecialRescues.Clear();
    }
}
