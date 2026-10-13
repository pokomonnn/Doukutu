using System;
using UnityEngine;

/// <summary>
/// 固定3枠の応急キット数量をシーン間で保持し、SaveManagerのJSONへ受け渡す。
/// MonoBehaviourではなく静的なプレイ中セッション。PlayerPrefsは使用しません。
/// </summary>
[Serializable]
public class SavedMedicalKitData
{
    public bool HasData;
    public int Slot0Amount;
    public int Slot1Amount;
    public int Slot2Amount;
}

public static class MedicalKitSessionStore2D
{
    private static SavedMedicalKitData sessionData;
    private static MedicalKitInventory2D boundKit;
    private static bool applyingSavedValues;

    public static bool HasData => sessionData != null && sessionData.HasData;

    // Enter Play Mode OptionsでDomain Reloadを無効にした環境でも、別のPlay開始時に
    // 前回のテスト用数量が残らないようにする。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetAtRuntimeStart()
    {
        sessionData = null;
        boundKit = null;
        applyingSavedValues = false;
    }

    /// <summary>シーン上のキットのStart()から登録。</summary>
    public static void Bind(MedicalKitInventory2D kit)
    {
        if (kit == null) return;
        if (boundKit != null && boundKit != kit)
            boundKit.Changed -= HandleKitChanged;

        boundKit = kit;
        kit.Changed -= HandleKitChanged;
        kit.Changed += HandleKitChanged;

        if (HasData)
        {
            ApplyToKit(kit);
            Debug.Log("[MedicalKitSave][SCENE RESTORE] シーン移動後の数量を復元: " + Describe(), kit);
        }
        else
        {
            CaptureFromKit(kit);
            Debug.Log("[MedicalKitSave][INIT] 応急キット初期数量をセッションへ登録: " + Describe(), kit);
        }
    }

    public static void Unbind(MedicalKitInventory2D kit)
    {
        if (kit == null) return;
        kit.Changed -= HandleKitChanged;
        if (boundKit == kit) boundKit = null;
    }

    private static void HandleKitChanged()
    {
        if (applyingSavedValues || boundKit == null) return;
        CaptureFromKit(boundKit);
    }

    private static void CaptureFromKit(MedicalKitInventory2D kit)
    {
        if (kit == null || applyingSavedValues) return;
        sessionData = new SavedMedicalKitData
        {
            HasData = true,
            Slot0Amount = kit.GetAmountAtSlot(0),
            Slot1Amount = kit.GetAmountAtSlot(1),
            Slot2Amount = kit.GetAmountAtSlot(2)
        };
    }

    /// <summary>
    /// セーブ直前に呼び出す。通常はChangedイベントで随時同期済み。
    /// キットが非ActiveでStart前でも、初回だけ初期数量を取り込める。
    /// 既存セッションをPrefab初期値で上書きしない。
    /// </summary>
    public static SavedMedicalKitData CreateSnapshot()
    {
        if (!HasData)
        {
            MedicalKitInventory2D kit = FindSceneKit();
            if (kit != null) CaptureFromKit(kit);
        }
        return Clone(sessionData) ?? new SavedMedicalKitData { HasData = true };
    }

    /// <summary>SaveManagerがロードした値を確定し、現在のKitへ即時反映。</summary>
    public static void RestoreSnapshot(SavedMedicalKitData data)
    {
        // 旧セーブは医療品フィールドが存在しないため、0/0/0で開始。
        sessionData = data != null && data.HasData
            ? Clone(data)
            : new SavedMedicalKitData { HasData = true };

        MedicalKitInventory2D kit = FindSceneKit();
        if (kit != null)
        {
            // Bind済みでない非ActiveなKitにも確実に反映。
            ApplyToKit(kit);
        }
        Debug.Log("[MedicalKitSave][LOAD] 復元値: " + Describe());
    }

    public static void ResetForNewGame()
    {
        if (boundKit != null)
            boundKit.Changed -= HandleKitChanged;
        boundKit = null;
        sessionData = null;
        applyingSavedValues = false;
        Debug.Log("[MedicalKitSave][NEW GAME] 応急キットのセッション値をリセットしました。");
    }

    private static void ApplyToKit(MedicalKitInventory2D kit)
    {
        if (kit == null || !HasData) return;
        applyingSavedValues = true;
        try
        {
            kit.RestoreSavedAmounts(
                sessionData.Slot0Amount,
                sessionData.Slot1Amount,
                sessionData.Slot2Amount);
        }
        finally
        {
            applyingSavedValues = false;
        }
    }

    private static MedicalKitInventory2D FindSceneKit()
    {
        if (boundKit != null) return boundKit;
        // 応急キットは閉じたInventory階層に置かれていることがある。
        MedicalKitInventory2D[] kits = UnityEngine.Object.FindObjectsByType<MedicalKitInventory2D>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (kits == null || kits.Length == 0) return null;
        // 複数シーンで同時に残った場合でも、有効なSceneに属するKitを優先。
        foreach (MedicalKitInventory2D kit in kits)
        {
            if (kit != null && kit.gameObject.scene.IsValid() && kit.gameObject.scene.isLoaded)
                return kit;
        }
        return kits[0];
    }

    private static SavedMedicalKitData Clone(SavedMedicalKitData src)
    {
        return src == null ? null : new SavedMedicalKitData
        {
            HasData = src.HasData,
            Slot0Amount = src.Slot0Amount,
            Slot1Amount = src.Slot1Amount,
            Slot2Amount = src.Slot2Amount
        };
    }

    public static string Describe()
    {
        return !HasData ? "データなし" :
            $"解毒剤={sessionData.Slot0Amount} / 副木={sessionData.Slot1Amount} / 包帯={sessionData.Slot2Amount}";
    }
}
