using System;
using System.Collections.Generic;

/// <summary>
/// シーン間で保持するインベントリ1件分のデータです。
/// ItemDataはScriptableObject参照なので、同一プレイ中は安全に復元できます。
/// </summary>
[Serializable]
public class SessionInventoryItemData
{
    public ItemData ItemData;
    public int GridX;
    public int GridY;
    public bool IsRotated;
    public bool HasFourWayRotation;
    public int RotationSteps;
    public int ResolvedRotationSteps => HasFourWayRotation
        ? ((RotationSteps % 4) + 4) % 4 : (IsRotated ? 1 : 0);
    public int Amount;
    public bool HasStoredMagazineAmmo;
    public int StoredMagazineAmmo;
    public AmmoItemData StoredMagazineAmmoType;
    public bool HasStoredWeaponDurability;
    public float StoredWeaponDurability;
    public bool StoredWeaponJammed;
}

/// <summary>
/// プレイヤーの通常インベントリと装備枠をまとめた、シーン間引き継ぎデータです。
/// </summary>
[Serializable]
public class PlayerInventorySessionData
{
    public int GridWidth = 7;
    public int GridHeight = 10;
    public List<SessionInventoryItemData> InventoryItems =
        new List<SessionInventoryItemData>();
    public SessionInventoryItemData PrimaryWeaponItem;
    public SessionInventoryItemData HelmetItem;
}

/// <summary>
/// 町のプレイヤー保管箱1個分をシーン間で保持するデータです。
/// </summary>
[Serializable]
public class PlayerItemBoxSessionData
{
    public int GridWidth = 10;
    public int GridHeight = 12;
    public List<SessionInventoryItemData> Items =
        new List<SessionInventoryItemData>();
}

[Serializable]
public class SkillCardSessionData
{
    public int UnlockedSlotCount = GameSessionManager.MinSkillSlots;
    public List<SkillCardData> OwnedCards = new List<SkillCardData>();
    public List<SkillCardData> EquippedCards = new List<SkillCardData>();
}

public enum MissionSessionState
{
    Inactive,
    InProgress,
    Completed
}

/// <summary>
/// ミッションをどこから受注したかを表します。
/// </summary>
public enum MissionAcceptSource
{
    Unknown = 0,
    Conversation = 1,
    Expedition = 2
}

/// <summary>
/// シーン間で保持するミッション1件分の状態です。
/// MissionDefinition2Dへの参照も残しますが、探索シーンではMissionIdを優先してMissionManager2D内の登録を探します。
/// </summary>
[Serializable]
public class MissionSessionData
{
    public MissionDefinition2D Mission;
    public string MissionId;
    public string DisplayName;
    public MissionSessionState State;
    public int Progress;
    public int RequiredAmount = 1;
    public bool RewardClaimed;
    public MissionAcceptSource AcceptSource = MissionAcceptSource.Unknown;
}

/// <summary>
/// シーン間で保持する町施設1件分のレベルです。
/// </summary>
[Serializable]
public class TownFacilitySessionData
{
    public string FacilityId;
    public int Level = 1;
}
