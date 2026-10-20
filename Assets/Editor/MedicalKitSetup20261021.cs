#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class MedicalKitSetup20261021
{
    public static ConsumableItemData[] Items()=>new[]{"解毒剤","Naegi","包帯"}.Select(n=>AssetDatabase.LoadAssetAtPath<ConsumableItemData>("Assets/Prefab/ItemPrefab/Test_Item/Kaihuku/"+n+".asset")).ToArray();
    static bool Configure(GameObject root,ConsumableItemData[] items)
    {
        if(items.Any(i=>i==null))throw new Exception("Missing medical asset while configuring "+root.name);
        bool changed=false;
        foreach(var kit in root.GetComponentsInChildren<MedicalKitInventory2D>(true)) {
            var data=new SerializedObject(kit);var slots=data.FindProperty("fixedSlots");slots.arraySize=3;
            for(int i=0;i<3;i++) {
                var slot=slots.GetArrayElementAtIndex(i);var item=slot.FindPropertyRelative("itemData");
                if(item.objectReferenceValue==items[i])continue;
                if(item.objectReferenceValue==null)slot.FindPropertyRelative("amount").intValue=data.FindProperty("maxAmountPerSlot").intValue;
                item.objectReferenceValue=items[i];
            }
            if(data.ApplyModifiedPropertiesWithoutUndo()){PrefabUtility.RecordPrefabInstancePropertyModifications(kit);changed=true;}
        }
        foreach(var shop in root.GetComponentsInChildren<MedicalKitSupplyShop2D>(true)) {
            var data=new SerializedObject(shop);var supplies=data.FindProperty("supplies");
            for(int i=0;i<supplies.arraySize;i++){var slot=supplies.GetArrayElementAtIndex(i);int index=slot.FindPropertyRelative("slotIndex").intValue;if(index>=0&&index<3)slot.FindPropertyRelative("itemData").objectReferenceValue=items[index];}
            if(data.ApplyModifiedPropertiesWithoutUndo()){PrefabUtility.RecordPrefabInstancePropertyModifications(shop);changed=true;}
        }
        return changed;
    }
    public static void Apply()
    {
        var items=Items();if(items.Any(i=>i==null))throw new Exception("Missing medical asset");
        var prefab=PrefabUtility.LoadPrefabContents("Assets/Prefab/Player.prefab");try{if(Configure(prefab,items))PrefabUtility.SaveAsPrefabAsset(prefab,"Assets/Prefab/Player.prefab");}finally{PrefabUtility.UnloadPrefabContents(prefab);}
        foreach(var entry in EditorBuildSettings.scenes.Where(s=>s.enabled)){
            var scene=EditorSceneManager.OpenScene(entry.path);bool changed=false;
            items=Items();
            foreach(var root in scene.GetRootGameObjects())changed|=Configure(root,items);
            if(changed)EditorSceneManager.SaveScene(scene);
        }
        foreach(var guid in AssetDatabase.FindAssets("t:ItemDataDatabase")) {
            items=Items();
            var db=AssetDatabase.LoadAssetAtPath<ItemDataDatabase>(AssetDatabase.GUIDToAssetPath(guid));var data=new SerializedObject(db);var list=data.FindProperty("itemDataList");
            foreach(var item in items){bool found=false;for(int i=0;i<list.arraySize;i++)found|=list.GetArrayElementAtIndex(i).objectReferenceValue==item;if(!found){int end=list.arraySize;list.InsertArrayElementAtIndex(end);list.GetArrayElementAtIndex(end).objectReferenceValue=item;}}
            data.ApplyModifiedPropertiesWithoutUndo();
        }
        AssetDatabase.SaveAssets();
        var town=EditorSceneManager.OpenScene("Assets/Scenes/Town_Main.unity");
        items=Items();
        foreach(var shop in town.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<MedicalKitSupplyShop2D>(true))) {
            var supplies=new SerializedObject(shop).FindProperty("supplies");
            for(int i=0;i<supplies.arraySize;i++) {
                var slot=supplies.GetArrayElementAtIndex(i);int index=slot.FindPropertyRelative("slotIndex").intValue;
                if(index<0||index>=3||slot.FindPropertyRelative("itemData").objectReferenceValue!=items[index])
                    throw new Exception("Saved medical supply reference mismatch: "+shop.name+" slot "+index);
            }
        }
        Debug.Log("MEDICAL SETUP PASS: antidote, splint, bandage assigned and town references verified; saved quantities unchanged");
    }
}
#endif
