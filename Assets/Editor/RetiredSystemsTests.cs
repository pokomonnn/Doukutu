using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class RetiredSystemsTests
{
    public static void Run()
    {
        try {
            var database = AssetDatabase.LoadAssetAtPath<ItemDataDatabase>("Assets/Prefab/ItemPrefab/ItemDataDatabase.asset");
            foreach (string id in new[] { "Water_001", "Skill_player006", "Skill_player007", "Skill_player008", "Skill_player009" })
                Require(!database.TryGetItemData(id, out _), "retired item still available: " + id);
            var restore = typeof(GameSessionManager).GetMethod("CreateSessionInventoryItem", BindingFlags.NonPublic | BindingFlags.Static);
            object[] args = { new SavedInventoryItemData { ItemId = "Water_001", Amount = 1 }, database, null };
            Require(restore.Invoke(null, args) == null, "old water inventory entry must be skipped safely");
            var antidote = AssetDatabase.LoadAssetAtPath<ConsumableItemData>("Assets/Prefab/ItemPrefab/Test_Item/Kaihuku/解毒剤.asset");
            Require(antidote != null && antidote.CanCure(StatusConditionType.Poison), "antidote must remain");
            args = new object[] { new SavedInventoryItemData { ItemId = antidote.ItemId, Amount = 1 }, database, null };
            Require(restore.Invoke(null, args) != null, "remaining inventory item must still restore");
            var previous = PlayerStatusSessionStore.CreateSnapshot();
            var player = new GameObject("Retirement compatibility test");
            try {
                var health = player.AddComponent<CharacterHealth>();
                var food = player.AddComponent<PlayerSurvivalController>();
                typeof(PlayerSurvivalController).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(food, null);
                var bridge = player.AddComponent<PlayerStatusSaveBridge>();
                // Water/SAN at zero in an old save must not affect health or food restoration.
                PlayerStatusSessionStore.SetData(JsonUtility.FromJson<SavedPlayerStatusData>(
                    "{\"HasHealth\":true,\"CurrentHealth\":61,\"HasSurvival\":true,\"CurrentFood\":37,\"CurrentWater\":0,\"HasSanity\":true,\"CurrentSanity\":0}"));
                Require(bridge.ReloadFromSession(), "legacy status restore");
                Require(health.CurrentHealth == 61 && Mathf.Approximately(food.CurrentFood, 37), "health and food preserved");
                food.DrainFood(7); food.RestoreFood(5);
                Require(Mathf.Approximately(food.CurrentFood, 35), "food depletion and recovery preserved");
                Require(typeof(PlayerSurvivalController).GetProperty("CurrentWater") == null, "water runtime API removed");
            } finally {
                UnityEngine.Object.DestroyImmediate(player);
                PlayerStatusSessionStore.SetData(previous);
            }
            foreach (string path in StoryIntegrationBuilder.Scenes) {
                var scene = EditorSceneManager.OpenScene(path);
                var components = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<MonoBehaviour>(true)).Where(c => c != null).ToArray();
                Require(!components.Any(c => RetiredSystemsMigration.RemovedTypes.Contains(c.GetType().Name)), "retired component in " + path);
                if (!path.Contains("Town_Main")) {
                    Require(components.Any(c => c is PlayerMove), "movement preserved");
                    Require(components.Any(c => c is PlayerCarryController2D), "carrying preserved");
                    // Finn's authored treatment route is in stage1; other scenes have different UI setups.
                    if (path.EndsWith("/stage1.unity"))
                        Require(components.Any(c => c is MedicalKitTreatment2D),
                            "Finn medical treatment preserved");
                }
            }
            SaveCompatibilitySelfTest.Run();
            Debug.Log("RETIRED SYSTEMS TEST PASS: legacy status and items, food, antidote, 5 scenes, existing save compatibility.");
            StoryIntegrationTests.RunPlay();
        } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static void Require(bool condition, string label)
    {
        if (!condition) throw new Exception("Retirement test failed: " + label);
    }
}
