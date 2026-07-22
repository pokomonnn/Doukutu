using System;

/// <summary>
/// セーブ一覧へ表示する軽量な概要データです。
/// JSONの内容をゲームへ適用せずに読み取ります。
/// </summary>
[Serializable]
public class SaveSlotInfo
{
    /// <summary>手動スロットは1～20、オートセーブは0です。</summary>
    public int SlotNumber;
    public bool IsAutoSave;
    public string DisplayName = string.Empty;

    public bool HasSaveData;
    public bool IsCompatible;

    public int SaveVersion;
    public string SavedSceneName = string.Empty;
    public string SavedAtUtc = string.Empty;
    public int Money;

    public int InventoryItemCount;
    public int MissionCount;
    public bool HasPrimaryWeapon;
    public string PrimaryWeaponItemId = string.Empty;

    public long FileSizeBytes;
    public DateTime FileModifiedUtc = DateTime.MinValue;
    public string SavePath = string.Empty;
    public string ReadError = string.Empty;

    public bool IsEmpty => !HasSaveData;
    public bool CanLoad => HasSaveData && IsCompatible;

    public string SlotLabel =>
        IsAutoSave
            ? "オートセーブ"
            : $"セーブ {SlotNumber:00}";
}
