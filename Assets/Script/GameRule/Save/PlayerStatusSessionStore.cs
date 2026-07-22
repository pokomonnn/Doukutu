using UnityEngine;

/// <summary>
/// HP・食料・水分・SAN・状態異常・松明残量を、
/// シーンをまたいで一時保持する静的ストアです。
///
/// PlayerStatusSaveBridgeが探索シーンを離れる直前に値を保存し、
/// Townで本セーブを行う場合も最後に取得したプレイヤー状態を使用できます。
/// </summary>
public static class PlayerStatusSessionStore
{
    private static SavedPlayerStatusData currentData;

    public static bool HasData =>
        currentData != null && currentData.HasAnyData;

    [RuntimeInitializeOnLoadMethod(
        RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetSession()
    {
        currentData = null;
    }

    public static SavedPlayerStatusData GetOrCreateMutableData()
    {
        if (currentData == null)
        {
            currentData = new SavedPlayerStatusData();
        }

        return currentData;
    }

    public static void SetData(SavedPlayerStatusData data)
    {
        currentData = Clone(data);
    }

    public static SavedPlayerStatusData CreateSnapshot()
    {
        return Clone(currentData);
    }

    public static void Clear()
    {
        currentData = null;
    }

    public static SavedPlayerStatusData Clone(
        SavedPlayerStatusData source)
    {
        if (source == null)
        {
            return null;
        }

        return new SavedPlayerStatusData
        {
            HasHealth = source.HasHealth,
            CurrentHealth = source.CurrentHealth,
            MaximumHealthAtSave = source.MaximumHealthAtSave,

            HasSurvival = source.HasSurvival,
            CurrentFood = source.CurrentFood,
            MaximumFoodAtSave = source.MaximumFoodAtSave,
            CurrentWater = source.CurrentWater,
            MaximumWaterAtSave = source.MaximumWaterAtSave,

            HasSanity = source.HasSanity,
            CurrentSanity = source.CurrentSanity,
            MaximumSanityAtSave = source.MaximumSanityAtSave,

            HasStatusConditions = source.HasStatusConditions,
            ActiveStatusConditions = source.ActiveStatusConditions,

            HasTorch = source.HasTorch,
            CurrentTorch = source.CurrentTorch,
            MaximumTorchAtSave = source.MaximumTorchAtSave
        };
    }
}
