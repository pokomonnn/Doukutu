using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 生存状態で町へ持ち帰ると施設・商品・機能などを解放する
/// 特殊救出NPCに付けます。
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonSpecialUnlock2D : MonoBehaviour
{
    [Header("特殊NPC")]
    [InspectorName("特殊NPC ID")]
    [SerializeField]
    private string specialNpcId = "special_survivor";

    [InspectorName("救出済みFlagを自動作成")]
    [SerializeField]
    private bool addAutomaticRescuedFlag = true;

    [Header("救出時の解放")]
    [InspectorName("解放Story Flags")]
    [SerializeField]
    private List<string> unlockStoryFlags = new List<string>();

    [Header("Inspector用メモ")]
    [TextArea(2, 5)]
    [InspectorName("解放内容メモ")]
    [SerializeField]
    private string unlockNote = "例：武器修理機能を解放";

    public string SpecialNpcId =>
        string.IsNullOrWhiteSpace(specialNpcId)
            ? "special_survivor"
            : specialNpcId.Trim();

    public string UnlockNote => unlockNote ?? string.Empty;

    public List<string> GetUnlockStoryFlags()
    {
        List<string> result = new List<string>();

        if (addAutomaticRescuedFlag)
        {
            AddUnique(result, $"rescued_special_{SpecialNpcId}");
        }

        if (unlockStoryFlags != null)
        {
            foreach (string flag in unlockStoryFlags)
            {
                AddUnique(result, flag);
            }
        }

        return result;
    }

    private static void AddUnique(List<string> target, string value)
    {
        if (target == null || string.IsNullOrWhiteSpace(value)) return;

        string normalized = value.Trim();

        foreach (string existing in target)
        {
            if (string.Equals(
                    existing,
                    normalized,
                    System.StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
        }

        target.Add(normalized);
    }

    private void OnValidate()
    {
        specialNpcId = specialNpcId?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(specialNpcId))
        {
            specialNpcId = "special_survivor";
        }

        if (unlockStoryFlags == null)
        {
            unlockStoryFlags = new List<string>();
        }

        for (int i = 0; i < unlockStoryFlags.Count; i++)
        {
            unlockStoryFlags[i] =
                unlockStoryFlags[i]?.Trim() ?? string.Empty;
        }
    }
}
