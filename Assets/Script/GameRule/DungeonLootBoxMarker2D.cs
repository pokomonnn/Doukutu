using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 対象ItemBoxプレハブに付く識別コンポーネント。Sceneにロードされるたびに新しいLootを抽選。
/// WorldStateSaveBridge.Startで前回の箱データが復元された後に上書きするため、フレーム末に生成します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemBoxInventory))]
public sealed class DungeonLootBoxMarker2D : MonoBehaviour
{
    [SerializeField, HideInInspector] private string prefabKey = "";
    [SerializeField] private bool debugLog = false;
    private bool generatedThisInstance;

    public string PrefabKey => prefabKey;

#if UNITY_EDITOR
    public void SetKeyForEditor(string key) { prefabKey = key; }
#endif

    private IEnumerator Start()
    {
        // ItemBoxInventory.Awake と WorldStateSaveBridge.Start の両方が終わるのを待つ。
        yield return new WaitForEndOfFrame();
        bool didGenerate = GenerateOnce();
        if (!didGenerate) yield break;
        // 現在の訪問分の結果を、既存のセッション保存管理に反映する。
        yield return new WaitForEndOfFrame();
        if (gameObject.scene == UnityEngine.SceneManagement.SceneManager.GetActiveScene())
        {
            WorldStateSaveBridge bridge = FindAnyObjectByType<WorldStateSaveBridge>(FindObjectsInactive.Include);
            if (bridge != null) bridge.CaptureToSession();
        }
    }

    private bool GenerateOnce()
    {
        if (generatedThisInstance) return false;
        generatedThisInstance = true;

        ItemBoxInventory box = GetComponent<ItemBoxInventory>();
        if (box == null) return false;
        if (box.BoxKind != ItemBoxKind.Storage)
        {
            Debug.LogWarning("[DungeonLoot] Shop型ItemBoxは生成対象外です: " + name, this);
            return false;
        }

        string scene = gameObject.scene.name;
        DungeonLootStageProfile2D[] profiles = Resources.LoadAll<DungeonLootStageProfile2D>("DungeonLootProfiles");
        DungeonLootStageProfile2D profile = null;
        foreach (DungeonLootStageProfile2D candidate in profiles)
        {
            if (candidate != null && string.Equals(candidate.sceneName, scene, StringComparison.OrdinalIgnoreCase))
            {
                profile = candidate;
                break;
            }
        }
        if (profile == null) return false;  // 町など、ルールの無いSceneでは触れない。
        DungeonLootStageProfile2D.BoxRule rule = profile.FindBoxRule(prefabKey);
        if (rule == null) return false;   // このステージで指定されていないBoxは触れない。

        // 必ず元のGridサイズを維持。前回の訪問の中身と「開いた」フラグを初期化。
        box.InitializeInventory();
        int width = box.Grid != null ? box.Grid.Width : 7;
        int height = box.Grid != null ? box.Grid.Height : 10;
        box.RestoreInventoryFromSave(width, height, new List<InventoryItem>());
        ItemBoxSaveIdentity save = GetComponent<ItemBoxSaveIdentity>();
        if (save != null) save.ResetOpenedForNewDungeonVisit();

        int generated = 0;
        if (UnityEngine.Random.Range(0, 100) >= Mathf.Clamp(rule.emptyChancePercent, 0, 100))
        {
            List<DungeonLootStageProfile2D.ItemChance> entries = rule.useOwnItemTable
                ? rule.ownItemTable : profile.stageItems;
            int low = Mathf.Max(0, rule.minDraws);
            int high = Mathf.Max(low, rule.maxDraws);
            int draws = UnityEngine.Random.Range(low, high + 1);
            HashSet<ItemData> failedForSpace = new HashSet<ItemData>();
            for (int n = 0; n < draws; n++)
            {
                DungeonLootStageProfile2D.ItemChance selected = Choose(entries, failedForSpace);
                if (selected == null) break;
                int amount = UnityEngine.Random.Range(Mathf.Max(1, selected.minAmount),
                    Mathf.Max(Mathf.Max(1, selected.minAmount), selected.maxAmount) + 1);
                box.TryAddItem(selected.item, amount, out int remaining);
                generated += amount - remaining;
                if (remaining > 0) failedForSpace.Add(selected.item);
            }
        }
        if (debugLog) Debug.Log($"[DungeonLoot] {scene} / {name}: {generated}個をランダム生成", this);
        return true;
    }

    private static DungeonLootStageProfile2D.ItemChance Choose(
        List<DungeonLootStageProfile2D.ItemChance> entries, HashSet<ItemData> excluded)
    {
        if (entries == null) return null;
        long total = 0;
        foreach (DungeonLootStageProfile2D.ItemChance e in entries)
            if (e != null && e.item != null && e.weight > 0 && !excluded.Contains(e.item))
                total += e.weight;
        if (total <= 0) return null;
        double pick = UnityEngine.Random.value * total;
        foreach (DungeonLootStageProfile2D.ItemChance e in entries)
        {
            if (e == null || e.item == null || e.weight <= 0 || excluded.Contains(e.item)) continue;
            pick -= e.weight;
            if (pick <= 0) return e;
        }
        return null;
    }
}
