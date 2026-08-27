using System;
using System.Collections.Generic;

/// <summary>
/// セーブファイル全体のデータです。
/// 第1段階：所持金・インベントリ・装備・武器残弾・ミッション
/// 第2段階：HP・食料・水分・SAN・状態異常・松明
/// 第3段階：現在シーン・チェックポイント・地面アイテム・アイテム箱・会話履歴
/// 第4段階：墓場の死亡NPC累計人数
/// 第5段階：町の施設アップグレードレベル
/// 第6段階：武器マガジン内の装填弾種
/// 第7段階：武器ごとの耐久度
/// 第8段階：武器ごとのジャム状態
/// 第9段階：スキルカード所持・装備・解放スロット数
/// </summary>
[Serializable]
public class SaveGameData
{
    public int SaveVersion = 9;
    public string SavedAtUtc = string.Empty;
    public string SavedSceneName = string.Empty;
    public int Money;

    /// <summary>これまで町へ持ち帰った死亡NPCの累計人数です。</summary>
    public int TotalDeadNpcCount;

    /// <summary>町の施設IDごとの現在レベルです。</summary>
    public List<SavedTownFacilityLevelData> FacilityLevels =
        new List<SavedTownFacilityLevelData>();

    public SavedPlayerInventoryData PlayerInventory =
        new SavedPlayerInventoryData();

    public List<SavedMissionData> Missions =
        new List<SavedMissionData>();

    public string TrackedMissionId = string.Empty;

    public SavedSkillCardData SkillCards =
        new SavedSkillCardData();

    public SavedPlayerStatusData PlayerStatus =
        new SavedPlayerStatusData();

    public SavedCheckpointData Checkpoint =
        new SavedCheckpointData();

    public SavedWorldStateData WorldState =
        new SavedWorldStateData();

    public List<SavedConversationHistoryData> ConversationHistory =
        new List<SavedConversationHistoryData>();
}

[Serializable]
public class SavedTownFacilityLevelData
{
    public string FacilityId = string.Empty;
    public int Level = 1;
}

[Serializable]
public class SavedPlayerInventoryData
{
    public int GridWidth = 7;
    public int GridHeight = 10;

    public List<SavedInventoryItemData> InventoryItems =
        new List<SavedInventoryItemData>();

    public SavedInventoryItemData PrimaryWeapon;
    public SavedInventoryItemData Helmet;
}

/// <summary>
/// プレイヤーインベントリ・装備・アイテム箱で共通利用するアイテム保存データです。
/// ScriptableObject参照ではなくItemIdを保存します。
/// </summary>
[Serializable]
public class SavedInventoryItemData
{
    public string ItemId = string.Empty;
    public int GridX;
    public int GridY;
    public bool IsRotated;
    public int Amount = 1;
    public bool HasStoredMagazineAmmo;
    public int StoredMagazineAmmo;

    /// <summary>マガジンに装填されているAmmoItemDataのItemIdです。</summary>
    public string StoredMagazineAmmoItemId = string.Empty;

    /// <summary>武器個体の耐久度が保存されているか。</summary>
    public bool HasStoredWeaponDurability;

    /// <summary>武器個体の現在耐久度。旧セーブではHas=falseのため新品扱いになります。</summary>
    public float StoredWeaponDurability;

    /// <summary>武器個体がジャム中か。Version 7以前ではfalse扱いです。</summary>
    public bool StoredWeaponJammed;
}

[Serializable]
public class SavedSkillCardData
{
    public int UnlockedSlotCount = 3;

    /// <summary>取得済みSkillCardDataのItemId一覧です。</summary>
    public List<string> OwnedCardIds = new List<string>();

    /// <summary>装備枠0～6のSkillCardData ItemIdです。空文字は未装備です。</summary>
    public List<string> EquippedCardIds = new List<string>();
}

[Serializable]
public class SavedMissionData
{
    public string MissionId = string.Empty;
    public string DisplayName = string.Empty;
    public MissionSessionState State = MissionSessionState.Inactive;
    public int Progress;
    public int RequiredAmount = 1;
    public bool RewardClaimed;
}

/// <summary>
/// HP・食料・水分・SAN・状態異常・松明残量です。
/// 各Hasフラグにより、存在していたControllerの項目だけを復元します。
/// </summary>
[Serializable]
public class SavedPlayerStatusData
{
    public bool HasHealth;
    public int CurrentHealth;
    public int MaximumHealthAtSave;

    public bool HasSurvival;
    public float CurrentFood;
    public float MaximumFoodAtSave;
    public float CurrentWater;
    public float MaximumWaterAtSave;

    public bool HasSanity;
    public float CurrentSanity;
    public float MaximumSanityAtSave;

    public bool HasStatusConditions;
    public StatusConditionType ActiveStatusConditions =
        StatusConditionType.None;

    public bool HasTorch;
    public float CurrentTorch;
    public float MaximumTorchAtSave;

    public bool HasAnyData =>
        HasHealth ||
        HasSurvival ||
        HasSanity ||
        HasStatusConditions ||
        HasTorch;
}

/// <summary>
/// GameManagerが保持するチェックポイント情報です。
/// Scene名とBuild Indexの両方を保存し、Build Index変更時はScene名を優先します。
/// </summary>
[Serializable]
public class SavedCheckpointData
{
    public bool HasCheckpoint;
    public int CheckpointNumber;
    public string SceneName = string.Empty;
    public int SceneBuildIndex = -1;
    public float PositionX;
    public float PositionY;
    public float PositionZ;
}

/// <summary>
/// 地面アイテムとアイテム箱の状態を、複数シーン分まとめて保持します。
/// </summary>
[Serializable]
public class SavedWorldStateData
{
    public List<string> CapturedSceneNames =
        new List<string>();

    public WorldItemSaveCollection WorldItems =
        new WorldItemSaveCollection();

    public List<SavedItemBoxData> ItemBoxes =
        new List<SavedItemBoxData>();

    public bool HasAnyData =>
        (CapturedSceneNames != null && CapturedSceneNames.Count > 0) ||
        (WorldItems != null &&
         WorldItems.items != null &&
         WorldItems.items.Count > 0) ||
        (ItemBoxes != null && ItemBoxes.Count > 0);
}

/// <summary>
/// シーン内のアイテム箱1個分です。
/// PersistentIdは各箱で重複しない値にしてください。
/// </summary>
[Serializable]
public class SavedItemBoxData
{
    public string SceneName = string.Empty;
    public string PersistentId = string.Empty;
    public bool WasOpened;
    public int GridWidth = 7;
    public int GridHeight = 10;

    public List<SavedInventoryItemData> Items =
        new List<SavedInventoryItemData>();
}

/// <summary>
/// 通常住人との会話完了回数です。
/// ResidentIdを変更すると過去セーブと紐付かなくなるため、公開後は固定してください。
/// </summary>
[Serializable]
public class SavedConversationHistoryData
{
    public string ResidentId = string.Empty;
    public int CompletedConversationCount;
}
