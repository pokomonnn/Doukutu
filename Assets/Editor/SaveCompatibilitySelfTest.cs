#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>一時オブジェクトだけで保存互換性を検証する。シーンやアセットを保存しない。</summary>
public static class SaveCompatibilitySelfTest
{
    private const BindingFlags PrivateStatic = BindingFlags.NonPublic | BindingFlags.Static;

    [MenuItem("Tools/Project Maintenance/保存互換性テスト")]
    public static void Run()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("保存互換性テストはEdit Modeで実行してください。");

        var definition = ScriptableObject.CreateInstance<ItemData>();
        var database = ScriptableObject.CreateInstance<ItemDataDatabase>();
        var owner = new GameObject("SaveCompatibilitySelfTest") { hideFlags = HideFlags.HideAndDontSave };
        try
        {
            // 非対称なL字は180/270度の情報欠落を検出できる。
            Set(definition, "itemId", "save_test_l_shape");
            Set(definition, "width", 2);
            Set(definition, "height", 2);
            Set(definition, "useCustomShape", true);
            Set(definition, "customShapeCells", new List<Vector2Int>
            {
                new Vector2Int(0, 0), new Vector2Int(0, 1), new Vector2Int(1, 1)
            });
            Set(database, "itemDataList", new List<ItemData> { definition });
            Set(database, "isLookupBuilt", false);
            var session = owner.AddComponent<GameSessionManager>();

            for (int rotation = 0; rotation < 4; rotation++)
            {
                var item = new InventoryItem(definition, 1, 1, 1);
                item.SetRotationSteps(rotation);
                var snapshot = (SessionInventoryItemData)InvokeInstance(session, "CreateItemSnapshot", item);
                var clone = (SessionInventoryItemData)InvokeStatic("CloneSessionInventoryItem", snapshot);
                var saved = (SavedInventoryItemData)InvokeStatic("CreateSavedInventoryItem", clone);
                saved = JsonUtility.FromJson<SavedInventoryItemData>(JsonUtility.ToJson(saved));
                object[] args = { saved, database, null };
                var restoredSession = (SessionInventoryItemData)InvokeStatic("CreateSessionInventoryItem", args);
                var restored = (InventoryItem)InvokeInstance(session, "CreateRuntimeInventoryItem", restoredSession);
                Require(restored.RotationSteps == rotation, "セッション/JSONの回転: " + rotation);
                Require(new HashSet<Vector2Int>(definition.GetOccupiedCells(rotation)).SetEquals(
                    definition.GetOccupiedCells(restored.RotationSteps)), "占有マス: " + rotation);

                var ground = owner.GetComponent<WorldItemPickup>() ?? owner.AddComponent<WorldItemPickup>();
                Set(ground, "droppedItem", item);
                WorldItemSaveData groundSaved = JsonUtility.FromJson<WorldItemSaveData>(
                    JsonUtility.ToJson(ground.CreateSaveData()));
                Require(groundSaved.ResolvedRotationSteps == rotation, "地面アイテム保存: " + rotation);

                var box = owner.GetComponent<ItemBoxInventory>() ?? owner.AddComponent<ItemBoxInventory>();
                box.RestoreInventoryFromSave(4, 4, new List<InventoryItem> { item });
                var identity = owner.GetComponent<ItemBoxSaveIdentity>() ?? owner.AddComponent<ItemBoxSaveIdentity>();
                identity.AssignPersistentId("save_test_box");
                SavedItemBoxData boxSaved = JsonUtility.FromJson<SavedItemBoxData>(
                    JsonUtility.ToJson(identity.CreateSaveData()));
                Require(identity.RestoreFromSaveData(boxSaved, database), "箱の復元成功");
                Require(box.Grid.Items.Count == 1 && box.Grid.Items[0].RotationSteps == rotation,
                    "箱の回転復元: " + rotation);
            }

            foreach (bool rotated in new[] { false, true })
            {
                var legacy = JsonUtility.FromJson<SavedInventoryItemData>(
                    "{\"ItemId\":\"save_test_l_shape\",\"Amount\":1,\"IsRotated\":" + rotated.ToString().ToLowerInvariant() + "}");
                object[] args = { legacy, database, null };
                var restored = (SessionInventoryItemData)InvokeStatic("CreateSessionInventoryItem", args);
                Require(restored.ResolvedRotationSteps == (rotated ? 1 : 0), "旧セーブの回転互換");
                var world = JsonUtility.FromJson<WorldItemSaveData>(
                    "{\"isRotated\":" + rotated.ToString().ToLowerInvariant() + "}");
                Require(world.ResolvedRotationSteps == (rotated ? 1 : 0), "旧地面データの回転互換");
            }

            // L字の空き1マスへ報酬を配置できることを通常Gridの判定と共に検証。
            var small = ScriptableObject.CreateInstance<ItemData>();
            try
            {
                var inventory = new PlayerInventorySessionData { GridWidth = 2, GridHeight = 2 };
                inventory.InventoryItems.Add(new SessionInventoryItemData
                {
                    ItemData = definition, Amount = 1, HasFourWayRotation = true, RotationSteps = 2
                });
                object[] args = { inventory, small, 0, 0, 0 };
                Require((bool)InvokeStatic("TryFindFreeSessionGridPosition", args), "不規則形状の空きへの報酬配置");
                var grid = new InventoryGrid(2, 2);
                grid.TryPlaceItem(new InventoryItem(definition, 0, 0, 1), 0, 0, 2);
                Require(grid.CanPlaceItem(new InventoryItem(small, 0, 0, 1), (int)args[2], (int)args[3], (int)args[4]),
                    "報酬配置と通常Gridの一致");
            }
            finally { UnityEngine.Object.DestroyImmediate(small); }

            var data = new SaveGameData
            {
                MedicalKit = new SavedMedicalKitData { HasData = true, Slot0Amount = 1, Slot1Amount = 2, Slot2Amount = 3 }
            };
            var roundTrip = JsonUtility.FromJson<SaveGameData>(JsonUtility.ToJson(data));
            Require(roundTrip.SaveVersion == GameSessionManager.CurrentSaveVersion, "保存バージョン一致");
            Require(roundTrip.MedicalKit.HasData && roundTrip.MedicalKit.Slot0Amount == 1 &&
                roundTrip.MedicalKit.Slot1Amount == 2 && roundTrip.MedicalKit.Slot2Amount == 3, "医療品JSON保存");
            var oldSave = JsonUtility.FromJson<SaveGameData>("{\"SaveVersion\":13}");
            Require(oldSave.MedicalKit == null || !oldSave.MedicalKit.HasData, "旧セーブは医療品未保存");
            Debug.Log("[SaveCompatibilitySelfTest] PASS: 4方向回転、箱、地面保存、旧形式、報酬配置、医療品JSON");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(owner);
            UnityEngine.Object.DestroyImmediate(database);
            UnityEngine.Object.DestroyImmediate(definition);
        }
    }

    private static void Set(object target, string field, object value) =>
        target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
    private static object InvokeStatic(string name, params object[] args) =>
        typeof(GameSessionManager).GetMethod(name, PrivateStatic).Invoke(null, args);
    private static object InvokeInstance(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
    private static void Require(bool valid, string label)
    {
        if (!valid) throw new InvalidOperationException("[SaveCompatibilitySelfTest] FAIL: " + label);
    }
}
#endif
