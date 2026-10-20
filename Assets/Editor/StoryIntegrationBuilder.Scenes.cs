using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static partial class StoryIntegrationBuilder
{
    [MenuItem("Tools/Story/Install into existing game")]
    public static void Install()
    {
        if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        BuildData();
        for(int stage=0;stage<Scenes.Length;stage++) {
            var scene=EditorSceneManager.OpenScene(Scenes[stage]);
            if(scene.GetRootGameObjects().Any(g=>g.name=="StoryIntegration")) {Debug.Log("STORY existing placement preserved "+scene.path);continue;}
            var root=new GameObject("StoryIntegration");var integration=root.AddComponent<StoryCampaignScene>();integration.stage=stage;
            InstallMissions(root);
            if(stage==0) InstallTown(root); else InstallDungeon(root,stage,integration);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        // Keep experimental campaign assets for reference, but remove them from the game's enabled build scenes.
        var builds=EditorBuildSettings.scenes;
        foreach(var b in builds) if(b.path.StartsWith("Assets/Scenes/Campaign/"))b.enabled=false;
        EditorBuildSettings.scenes=builds;AssetDatabase.SaveAssets();
        ReconcileQuestLocations();
        Debug.Log("STORY INSTALL PASS");
    }
    static void InstallMissions(GameObject root)
    {
        var managers=UnityEngine.Object.FindObjectsByType<MissionManager2D>(FindObjectsInactive.Include);
        foreach(var manager in managers) {
            var so=new SerializedObject(manager);var entries=so.FindProperty("missions");
            foreach(var q in database.quests) {
                bool exists=false;for(int i=0;i<entries.arraySize;i++) if(entries.GetArrayElementAtIndex(i).FindPropertyRelative("mission").objectReferenceValue==q.mission) exists=true;
                if(exists)continue;
                int n=entries.arraySize;entries.InsertArrayElementAtIndex(n);var e=entries.GetArrayElementAtIndex(n);
                e.FindPropertyRelative("mission").objectReferenceValue=q.mission;
                e.FindPropertyRelative("compassTarget").objectReferenceValue=null;e.FindPropertyRelative("targetEnemy").objectReferenceValue=null;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        if(UnityEngine.Object.FindFirstObjectByType<StoryProgressManager>()==null) {
            var managerObject=new GameObject("Story progress fallback");managerObject.transform.SetParent(root.transform);
            var manager=managerObject.AddComponent<StoryProgressManager>();
            var db=AssetDatabase.LoadAssetAtPath<StoryQuestDatabase>("Assets/Mission/StoryQuestDatabase.asset");
            Put(manager,"storyDatabase",db);Put(manager,"dontDestroyOnLoad",false);
        }
    }
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NotoSansJP-VariableFont_wght SDF.asset");
    static Button Button(Transform parent,string title,Vector2 position)
    {
        var go=new GameObject(title,typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);
        var rect=go.GetComponent<RectTransform>();rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.anchoredPosition=position;rect.sizeDelta=new Vector2(160,42);
        go.GetComponent<Image>().color=new Color(.10f,.12f,.16f,.96f);
        var label=new GameObject("Label",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(go.transform,false);
        var lr=label.GetComponent<RectTransform>();lr.anchorMin=Vector2.zero;lr.anchorMax=Vector2.one;lr.offsetMin=lr.offsetMax=Vector2.zero;
        var text=label.GetComponent<TextMeshProUGUI>();text.text=title;text.font=Font;text.fontSize=21;text.alignment=TextAlignmentOptions.Center;text.raycastTarget=false;
        return go.GetComponent<Button>();
    }
    static void InstallTown(GameObject root)
    {
        var canvas=root.AddComponent<Canvas>();canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.sortingOrder=1;
        root.AddComponent<GraphicRaycaster>();var scaler=root.AddComponent<CanvasScaler>();scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1920,1080);
        string[] roles={"Mayor","Roan","Glen","Butler","Epilogue"};string[] labels={"町長・物語","ロアン・依頼","グレン・依頼","執事","後日談"};
        for(int i=0;i<roles.Length;i++) {
            var button=Button(root.transform,labels[i],new Vector2(12+i*166,-12));var link=button.gameObject.AddComponent<StoryTownLink>();
            link.conversation=Asset<TownConversationData>("Town_"+roles[i]);link.butler=roles[i]=="Butler";
        }
        // Existing departure panels, stores and tavern are retained.
    }
    static void InstallDungeon(GameObject root,int stage,StoryCampaignScene scene)
    {
        if(UnityEngine.Object.FindFirstObjectByType<DialoguePresenter2D>()==null) DialogueStudioUICreator2D.CreateInScene();
        root.AddComponent<StoryConversationGuard>();
        var player=UnityEngine.Object.FindFirstObjectByType<PlayerMove>();
        if(player==null) throw new Exception("Existing PlayerMove missing "+Scenes[stage]);
        var spawns=UnityEngine.Object.FindObjectsByType<StageExpeditionSpawnPoint>(FindObjectsInactive.Exclude).OrderBy(s=>s.EntryPointId).ToArray();
        var spawn=spawns.Length>0?spawns[0].transform.position:player.transform.position;
        // Place next to existing reachable loot/carry locations, on their actual terrain rather than an invented floor.
        var anchors=UnityEngine.Object.FindObjectsByType<ItemBoxInteractable>(FindObjectsInactive.Exclude)
            .Where(b=>b.transform.position.x<0).Select(b=>b.transform.position).OrderBy(p=>(p-spawn).sqrMagnitude).ToArray();
        if(anchors.Length==0) anchors=new[]{spawn};
        Physics2D.SyncTransforms();
        var steps=database.steps.Where(s=>s.stage==stage).ToArray();
        var used=new List<Vector3>();int i=0;
        foreach(var step in steps) {
            Vector3 anchor=(step.id=="garden"?spawn:anchors[Mathf.Min(anchors.Length-1,(i+1)*Mathf.Max(1,anchors.Length/(steps.Length+1)))]);
            bool representative=step.title.Contains("バルド")||step.title.Contains("グレン")||step.id=="finn";
            if(representative) anchor=spawn+Vector3.right*(5+i%3*4);
            Vector3 pos=Ground(anchor+Vector3.right*2);
            while(used.Any(p=>Vector2.Distance(p,pos)<4)) pos=Ground(pos+Vector3.right*4);
            used.Add(pos);
            GameObject go;
            if(step.id=="finn") {
                var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/NPC/探検家.prefab");
                go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);go.transform.SetParent(root.transform,true);go.transform.position=pos;
                var id=go.AddComponent<StoryRescueIdentity>();id.rescuedFlag="S1S01_Rescued";
                var oldConversation=go.GetComponent<CaveConversationNPC>();if(oldConversation!=null)oldConversation.enabled=false;
                if(go.GetComponent<RescuePersonTarget2D>()==null)go.AddComponent<RescuePersonTarget2D>();
                if(go.GetComponent<MissionTargetIdentity2D>()==null)go.AddComponent<MissionTargetIdentity2D>();
                var identity=go.GetComponent<MissionTargetIdentity2D>();Put(identity,"targetId","story_finn");Put(identity,"targetType",MissionTargetType2D.RescueNpc);
            } else {
                go=new GameObject(step.title);go.transform.SetParent(root.transform);go.transform.position=pos;
                var render=go.AddComponent<SpriteRenderer>();render.sprite=MarkerSprite(step);render.sortingOrder=5;
                if(render.sprite!=null && render.sprite.bounds.size.y>0) go.transform.localScale=Vector3.one*(representative?1.5f:1f)/render.sprite.bounds.size.y;
            }
            go.name="Story_"+step.id;
            var marker=go.AddComponent<StoryInteractionPoint>();marker.stepId=step.id;
            var actor=go.GetComponent<DialogueActor2D>();if(actor==null)actor=go.AddComponent<DialogueActor2D>();
            Put(actor,"dialogue",step.dialogue);Put(actor,"useEKeyInteraction",true);Put(actor,"interactionDistance",2.5f);
            Put(actor,"preventWhenHostileToPlayer",false);
            var label=new GameObject("Story label",typeof(TextMeshPro));label.transform.SetParent(root.transform);label.transform.position=pos+Vector3.up*2;
            var text=label.GetComponent<TextMeshPro>();text.font=Font;text.fontSize=3;text.text=step.title+"\n[E]";text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(8,2);
            if(representative && step.id!="finn") {var zone=new GameObject("交渉場所").AddComponent<StoryNegotiationZone>();zone.transform.SetParent(root.transform);zone.transform.position=pos;}
            if(step.id=="garden") {
                scene.gardenActor=actor;scene.gardenIntroduction=step.dialogue;scene.gardenChoices=Asset<DialogueData2D>("Dialogue_GardenChoices");
                scene.endingA=Asset<DialogueData2D>("Dialogue_EndingA");scene.endingB=Asset<DialogueData2D>("Dialogue_EndingB");scene.endingC=Asset<DialogueData2D>("Dialogue_EndingC");
            }
            // Retain existing mission entries, add a compass destination for each authored quest in this scene.
            foreach(var manager in UnityEngine.Object.FindObjectsByType<MissionManager2D>(FindObjectsInactive.Include)) {
                var so=new SerializedObject(manager);var entries=so.FindProperty("missions");
                for(int k=0;k<entries.arraySize;k++) {
                    var e=entries.GetArrayElementAtIndex(k);
                    if(e.FindPropertyRelative("mission").objectReferenceValue==database.quests.First(q=>q.id==step.quest).mission)
                        e.FindPropertyRelative("compassTarget").objectReferenceValue=go.transform;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }
            Debug.Log("STORY PLACED stage="+stage+" "+step.id+"@"+pos);i++;
        }
        if(stage==3) {
            var zone=new GameObject("指定回廊の停戦区域").AddComponent<StoryNegotiationZone>();zone.transform.SetParent(root.transform);
            zone.transform.position=root.GetComponentsInChildren<StoryInteractionPoint>().First(p=>p.stepId=="route").transform.position;
            zone.size=new Vector2(24,12);zone.requiredFlag="S3S01_Completed";
        }
        if(stage==4) {
            // The garden is peaceful. Preserve source prefab assets; deactivate their combat instances only here.
            foreach(var hp in UnityEngine.Object.FindObjectsByType<CharacterHealth>(FindObjectsInactive.Exclude))
                if(hp.GetComponentInParent<PlayerMove>()==null && !hp.transform.IsChildOf(root.transform)) hp.gameObject.SetActive(false);
            foreach(var damage in UnityEngine.Object.FindObjectsByType<DamageDealer>(FindObjectsInactive.Include)) damage.enabled=false;
        }
    }
    static Vector3 Ground(Vector3 position)
    {
        var hit=Physics2D.Raycast(position+Vector3.up*4,Vector2.down,16,1<<6);
        return hit.collider!=null?(Vector3)hit.point+Vector3.up*.8f:position;
    }
    static Sprite MarkerSprite(StoryCampaignStep s)
    {
        string character=s.title.Contains("バルド")?"盗賊":s.title.Contains("グレン")?"ドワーフ":s.id=="garden"?"町人":null;
        if(character!=null) return AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/"+character+".prefab").GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.sprite!=null).sprite;
        string folder="Assets/Cainos/Pixel Art Platformer - Dungeon/Prefab/Props";
        string term=s.id=="medicine"?"Bottle":s.id=="water"?"Switch":s.id=="original"||s.id=="alternative"?"Chest":"Book";
        return AssetDatabase.FindAssets("t:Prefab",new[]{folder}).Select(AssetDatabase.GUIDToAssetPath).Where(p=>p.Contains(term))
            .Select(AssetDatabase.LoadAssetAtPath<GameObject>).SelectMany(p=>p.GetComponentsInChildren<SpriteRenderer>(true)).First(r=>r.sprite!=null).sprite;
    }
}
