using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class StoryIntegrationBuilder
{
    public static void CheckPlacement()
    {
        foreach(string path in Scenes.Skip(1)) {
            var scene=EditorSceneManager.OpenScene(path);
            var root=scene.GetRootGameObjects().First(g=>g.name=="StoryIntegration");
            Physics2D.SyncTransforms();
            var points=root.GetComponentsInChildren<StoryInteractionPoint>();
            foreach(var point in points) {
                var origin=point.transform.position;
                if(Physics2D.Raycast(origin+Vector3.up*.3f,Vector2.down,2,1<<6).collider!=null)continue;
                var candidates=Object.FindObjectsByType<ItemBoxInteractable>(FindObjectsInactive.Exclude)
                    .Where(b=>b.transform.position.x<0).Select(b=>b.transform.position)
                    .OrderBy(p=>(p-origin).sqrMagnitude);
                bool placed=false;
                foreach(var candidate in candidates) {
                    var hit=Physics2D.Raycast(candidate+Vector3.up*4,Vector2.down,16,1<<6);
                    if(hit.collider==null)continue;
                    Vector3 position=(Vector3)hit.point+Vector3.up*.8f;
                    if(points.Any(p=>p!=point && Vector2.Distance(p.transform.position,position)<4))continue;
                    Vector3 delta=position-origin;point.transform.position=position;
                    foreach(var label in root.GetComponentsInChildren<TMPro.TextMeshPro>())
                        if(Vector3.Distance(label.transform.position,origin+Vector3.up*2)<.1f)label.transform.position+=delta;
                    foreach(var zone in root.GetComponentsInChildren<StoryNegotiationZone>())
                        if(Vector3.Distance(zone.transform.position,origin)<.1f)zone.transform.position+=delta;
                    Debug.Log("STORY PLACEMENT grounded "+scene.name+" / "+point.stepId+" "+origin+" -> "+position);
                    placed=true;break;
                }
                if(!placed)throw new System.Exception("No grounded story placement: "+scene.name+" / "+point.stepId);
            }
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        StoryIntegrationTests.Validate();
        Debug.Log("STORY PLACEMENT PASS: all authored points have nearby terrain");
    }
    // Update only components owned by this integration; keep existing terrain and gameplay instances.
    public static void RefineAndTest()
    {
        foreach(string path in Scenes) {
            var scene=EditorSceneManager.OpenScene(path);
            var root=scene.GetRootGameObjects().First(g=>g.name=="StoryIntegration");
            var manager=root.GetComponent<StoryProgressManager>();
            if(manager!=null) {
                var child=new GameObject("Story progress fallback");child.transform.SetParent(root.transform);
                var replacement=child.AddComponent<StoryProgressManager>();
                EditorUtility.CopySerialized(manager,replacement);
                Object.DestroyImmediate(manager);
            }
            var route=root.GetComponentsInChildren<StoryInteractionPoint>().FirstOrDefault(p=>p.stepId=="route");
            foreach(var rescue in root.GetComponentsInChildren<StoryRescueIdentity>()) {
                var oldConversation=rescue.GetComponent<CaveConversationNPC>();
                if(oldConversation!=null)oldConversation.enabled=false;
            }
            foreach(var zone in root.GetComponentsInChildren<StoryNegotiationZone>())
                if(zone.requiredFlag=="S3S01_Completed" && route!=null)zone.transform.position=route.transform.position;
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();StoryIntegrationTests.RunPlay();
    }
}
