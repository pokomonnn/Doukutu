using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class DungeonUIInstaller
{
    [MenuItem("Tools/Story Campaign/Apply text UI and minimap")]
    public static void Apply()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        foreach (string path in StoryIntegrationBuilder.Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path);
            var hud = Object.FindObjectsByType<MissionHUDSafeArea2D>(FindObjectsInactive.Include)
                .First(h => h.gameObject.scene == scene);
            foreach (var image in hud.panel.GetComponentsInChildren<Image>(true))
                image.color = Color.clear;
            if (scene.name != "Town_Main")
            {
                var map = hud.GetComponent<DungeonMinimap2D>();
                if (map == null) map = hud.gameObject.AddComponent<DungeonMinimap2D>();
                map.font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(
                    "Assets/TextMesh Pro/Fonts/NotoSansJP-VariableFont_wght SDF.asset");
                hud.panel.anchoredPosition = new Vector2(-20, -map.ReservedTop);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();
        Debug.Log("DUNGEON UI INSTALL PASS: five existing scenes, four minimaps");
    }
}
