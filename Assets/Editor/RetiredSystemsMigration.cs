using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// One-time, explicit editor migration. Does not run automatically on import.
public static class RetiredSystemsMigration
{
    public static readonly string[] RemovedTypes = {
        "WaterVignetteUI", "WaterEnemyVisibilityController", "PlayerSanityController",
        "SanityBarUI", "SanityVignetteUI", "SanityEnemySpawnController", "EnemySanityAura2D",
        "RopePlacementZone", "RopeClimbController", "RopeClimbZone",
        "PlayerRopePullController", "RopePullTarget", "RopePullPhysicalRope2D"
    };
    static readonly string[] Items = {
        "Assets/Prefab/ItemPrefab/Test_Item/Kaihuku/Water.asset",
        "Assets/Prefab/ItemPrefab/Test_Item/RopeItemData.asset",
        "Assets/Prefab/ItemPrefab/SkillCardData/節水.asset",
        "Assets/Prefab/ItemPrefab/SkillCardData/強靭な精神.asset",
        "Assets/Prefab/ItemPrefab/SkillCardData/狂気との共存.asset",
        "Assets/Prefab/ItemPrefab/SkillCardData/闇に慣れた者.asset",
        "Assets/Mission/ステージクエスト/2飲み水の確保.asset"
    };
    static readonly HashSet<UnityEngine.Object> retiredItems = new HashSet<UnityEngine.Object>();
    static readonly List<string> report = new List<string>();

    public static void Run()
    {
        try {
            foreach (string path in Items) {
                var item = AssetDatabase.LoadMainAssetAtPath(path);
                if (item != null) retiredItems.Add(item);
            }
            var guids = AssetDatabase.FindAssets("t:MonoScript", new[] { "Assets" })
                .Where(g => RemovedTypes.Contains(Path.GetFileNameWithoutExtension(AssetDatabase.GUIDToAssetPath(g))))
                .Concat(Items.Select(AssetDatabase.AssetPathToGUID))
                .Concat(new[] { "SurvivalBarUI" }.SelectMany(n => AssetDatabase.FindAssets(n + " t:MonoScript", new[] { "Assets" })))
                .Where(g => !string.IsNullOrEmpty(g)).ToArray();
            var candidates = AssetDatabase.GetAllAssetPaths().Where(p => p.StartsWith("Assets/") &&
                (p.EndsWith(".prefab") || p.EndsWith(".unity") || p.EndsWith(".asset")) &&
                !Items.Contains(p) && File.Exists(p) && guids.Any(g => File.ReadAllText(p).Contains(g))).ToArray();
            // Base prefabs first; scene instances then inherit removed components.
            foreach (string path in candidates.Where(p => p.EndsWith(".prefab"))) {
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    if (Clean(root)) { PrefabUtility.SaveAsPrefabAsset(root, path); report.Add(path); }
                } finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            foreach (string path in candidates.Where(p => p.EndsWith(".unity"))) {
                var scene = EditorSceneManager.OpenScene(path);
                bool changed = false;
                foreach (var root in scene.GetRootGameObjects()) changed |= Clean(root);
                if (changed) { EditorSceneManager.SaveScene(scene); report.Add(path); }
            }
            foreach (string path in candidates.Where(p => p.EndsWith(".asset"))) {
                bool changed = false;
                foreach (var obj in AssetDatabase.LoadAllAssetsAtPath(path).OfType<ScriptableObject>())
                    changed |= ClearItemReferences(obj);
                if (changed) report.Add(path);
            }
            AssetDatabase.SaveAssets();
            foreach (string guid in AssetDatabase.FindAssets("t:ConsumableItemData", new[] { "Assets" })) {
                var item = AssetDatabase.LoadAssetAtPath<ConsumableItemData>(AssetDatabase.GUIDToAssetPath(guid));
                if (item == null || retiredItems.Contains(item)) continue;
                EditorUtility.SetDirty(item);
                AssetDatabase.SaveAssetIfDirty(item);
            }
            // Remove item definitions only after all serialized references have been cleared.
            foreach (string path in Items) if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            Directory.CreateDirectory("Logs");
            File.AppendAllLines("Logs/RetiredSystemsMigration.txt", report);
            Debug.Log("RETIRED SYSTEMS MIGRATION PASS: " + report.Count + " assets updated");
            EditorApplication.Exit(0);
        } catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
    }

    static bool Clean(GameObject root)
    {
        bool changed = false;
        foreach (var component in root.GetComponentsInChildren<MonoBehaviour>(true)) {
            if (component == null) continue;
            string type = component.GetType().Name;
            var so = new SerializedObject(component);
            if (type == "SurvivalBarUI") {
                foreach (string field in new[] { "waterSlider", "waterText", "waterFillImage" })
                    changed |= HideReference(so, field);
            }
            if (RemovedTypes.Contains(type)) {
                foreach (string field in new[] { "sanitySlider", "sanityText", "vignetteImage", "ropeVisual", "ropeLineRenderer", "modeText" })
                    HideReference(so, field);
                if (type == "WaterVignetteUI" || type == "SanityVignetteUI") {
                    var image = component.GetComponent<UnityEngine.UI.Image>();
                    if (image != null) image.enabled = false;
                }
                if (type == "RopeClimbZone")
                    foreach (var collider in component.GetComponents<Collider2D>().Where(c => c.isTrigger)) collider.enabled = false;
                UnityEngine.Object.DestroyImmediate(component);
                changed = true;
                continue;
            }
            if (type == "WorldItemPickup") {
                var iterator = so.GetIterator();
                while (iterator.Next(true))
                    if (iterator.propertyType == SerializedPropertyType.ObjectReference && retiredItems.Contains(iterator.objectReferenceValue)) {
                        component.gameObject.SetActive(false); changed = true; break;
                    }
            }
            changed |= ClearItemReferences(component);
        }
        return changed;
    }

    static bool HideReference(SerializedObject so, string field)
    {
        var p = so.FindProperty(field);
        if (p == null || p.propertyType != SerializedPropertyType.ObjectReference) return false;
        var obj = p.objectReferenceValue;
        var go = obj is Component c ? c.gameObject : obj as GameObject;
        if (go != null) go.SetActive(false);
        p.objectReferenceValue = null;
        so.ApplyModifiedPropertiesWithoutUndo();
        return go != null;
    }

    static bool ClearItemReferences(UnityEngine.Object obj)
    {
        var so = new SerializedObject(obj);
        var it = so.GetIterator();
        bool changed = false;
        if (it.NextVisible(true)) do { changed |= ClearProperty(it); } while (it.NextVisible(false));
        if (changed) { so.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(obj); }
        return changed;
    }

    static bool ClearProperty(SerializedProperty p)
    {
        if (p.propertyType == SerializedPropertyType.ObjectReference && retiredItems.Contains(p.objectReferenceValue)) {
            p.objectReferenceValue = null; return true;
        }
        bool changed = false;
        if (p.isArray && p.propertyType != SerializedPropertyType.String) {
            for (int i = p.arraySize - 1; i >= 0; i--) {
                var element = p.GetArrayElementAtIndex(i);
                bool removeEntry = DirectRetiredReference(element);
                if (!ClearProperty(element)) continue;
                // Remove retired loot/stock/start-inventory entries, not merely their item reference.
                if (removeEntry) {
                    int size = p.arraySize; p.DeleteArrayElementAtIndex(i);
                    if (p.arraySize == size) p.DeleteArrayElementAtIndex(i);
                }
                changed = true;
            }
        } else if (p.propertyType == SerializedPropertyType.Generic) {
            var child = p.Copy(); var end = p.GetEndProperty();
            if (child.NextVisible(true)) do {
                if (SerializedProperty.EqualContents(child, end)) break;
                changed |= ClearProperty(child);
            } while (child.NextVisible(false));
        }
        return changed;
    }

    static bool DirectRetiredReference(SerializedProperty p)
    {
        if (p.propertyType == SerializedPropertyType.ObjectReference) return retiredItems.Contains(p.objectReferenceValue);
        if (p.isArray || p.propertyType != SerializedPropertyType.Generic) return false;
        var child = p.Copy(); var end = p.GetEndProperty();
        if (child.NextVisible(true)) do {
            if (SerializedProperty.EqualContents(child, end)) break;
            if (child.propertyType == SerializedPropertyType.ObjectReference && retiredItems.Contains(child.objectReferenceValue)) return true;
        } while (child.NextVisible(false));
        return false;
    }
}
