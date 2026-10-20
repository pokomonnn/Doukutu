#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class StorageIdentityAudit
{
    public static void AddPlacementKeys()
    {
        var report=new List<string>{"旧persistentIdは変更せず、重複した配置に保存専用キーを追加。旧セーブの読み取り照合は維持。"};
        foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled)) {
            var scene=EditorSceneManager.OpenScene(entry.path);
            var boxes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ItemBoxSaveIdentity>(true)).ToArray();
            bool changed=false;
            foreach(var group in boxes.GroupBy(b=>b.PersistentId).Where(g=>g.Count()>1))
                foreach(var box in group) {
                    var data=new SerializedObject(box);
                    var key=data.FindProperty("placementSaveKey");
                    if(!string.IsNullOrEmpty(key.stringValue))continue;
                    key.stringValue="placed:"+GlobalObjectId.GetGlobalObjectIdSlow(box);
                    data.ApplyModifiedPropertiesWithoutUndo();
                    PrefabUtility.RecordPrefabInstancePropertyModifications(box);
                    report.Add(entry.path+" | "+Path(box.transform)+" | old="+box.PersistentId+" | key="+box.SaveKey);
                    changed=true;
                }
            if(boxes.Select(b=>b.SaveKey).Distinct().Count()!=boxes.Length)throw new Exception("Duplicate save key: "+entry.path);
            if(changed)EditorSceneManager.SaveScene(scene);
        }
        System.IO.File.WriteAllLines("Docs/StorageIdentity_Migration_20261020.txt",report);
        Debug.Log("STORAGE KEYS PASS: legacy IDs unchanged; placement keys unique");
    }
    public static void Run(){foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled)){
        var scene=EditorSceneManager.OpenScene(entry.path);var boxes=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<ItemBoxSaveIdentity>(true)).ToArray();
        foreach(var group in boxes.GroupBy(b=>b.PersistentId).Where(g=>g.Count()>1))foreach(var b in group)Debug.Log("BOX DUP "+entry.path+" | "+Path(b.transform)+" | "+b.PersistentId+" | "+GlobalObjectId.GetGlobalObjectIdSlow(b));
        foreach(var bridge in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<PlayerInventorySessionBridge>(true)))Debug.Log("INVENTORY BRIDGE "+entry.path+" "+Path(bridge.transform)+" => "+(bridge.InventoryController!=null?Path(bridge.InventoryController.transform):"null"));
        foreach(var box in scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<TownPlayerItemBoxSessionBridge>(true)))Debug.Log("TOWN BOX "+entry.path+" "+Path(box.transform));
    }}
    static string Path(Transform t)=>t.parent==null?t.name:Path(t.parent)+"/"+t.name;
}
#endif
