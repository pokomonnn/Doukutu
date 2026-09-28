using UnityEngine;

/// <summary>
/// 「このNPCを初めて生存救出した時に何を解放するか」を設定します。
/// 通常の救出NPCには付ける必要はありません。
///
/// 例:
/// 特殊NPC ID = mechanic_01
/// 表示名 = 整備士
/// 解放Story Flag ID = unlock_weapon_repair
/// 施設ID = weapon_shop
/// 解放後施設レベル = 2
/// </summary>
[DisallowMultipleComponent]
public class RescuePersonUnlockProfile2D : MonoBehaviour
{
    [Header("特殊NPC")]
    [Tooltip("セーブに残す一意のIDです。同じNPC種類では同じIDを使ってください。")]
    [InspectorName("特殊NPC ID")]
    [SerializeField] private string specialNpcId = string.Empty;

    [Tooltip("Resultの解放通知やログで使う表示名です。")]
    [InspectorName("表示名")]
    [SerializeField] private string displayName = "特殊な生存者";

    [Header("初回救出時の解放")]
    [Tooltip(
        "初めて生存状態で持ち帰った時にONにするStory Flagです。" +
        "空欄ならStory Flagは変更しません。"
    )]
    [InspectorName("解放Story Flag ID")]
    [SerializeField] private string unlockStoryFlagId = string.Empty;

    [Tooltip(
        "初回救出時に施設レベルを上げたい場合のFacility IDです。" +
        "空欄なら施設レベルは変更しません。"
    )]
    [InspectorName("施設ID（任意）")]
    [SerializeField] private string facilityId = string.Empty;

    [Tooltip("施設IDを設定した場合、このレベル以上へ引き上げます。")]
    [InspectorName("解放後施設レベル")]
    [SerializeField, Min(1)] private int facilityLevel = 1;

    public string SpecialNpcId =>
        specialNpcId?.Trim() ?? string.Empty;

    public string DisplayName =>
        string.IsNullOrWhiteSpace(displayName)
            ? SpecialNpcId
            : displayName.Trim();

    public string UnlockStoryFlagId =>
        unlockStoryFlagId?.Trim() ??
        string.Empty;

    public string FacilityId =>
        facilityId?.Trim() ??
        string.Empty;

    public int FacilityLevel =>
        Mathf.Max(1, facilityLevel);

    public bool HasValidId =>
        !string.IsNullOrWhiteSpace(
            SpecialNpcId
        );

    private void OnValidate()
    {
        specialNpcId =
            specialNpcId?.Trim() ??
            string.Empty;

        displayName =
            displayName?.Trim() ??
            string.Empty;

        unlockStoryFlagId =
            unlockStoryFlagId?.Trim() ??
            string.Empty;

        facilityId =
            facilityId?.Trim() ??
            string.Empty;

        facilityLevel =
            Mathf.Max(1, facilityLevel);
    }
}
