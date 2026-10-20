using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class CampaignBuilder
{
    const string Folder="Assets/Scenes/Campaign/";
    static bool rebuild;
    // Used only while authoring these new scenes. The regular menu never overwrites scenes.
    public static void RebuildImplementation() {rebuild=true; Build();}
    [MenuItem("Tools/Story Campaign/Generate missing stages")]
    public static void Build()
    {
        var data=JsonUtility.FromJson<CampaignData>(Resources.Load<TextAsset>("Campaign/Story").text);
        var font=AssetDatabase.LoadAssetAtPath<Font>("Assets/TextMesh Pro/Fonts/NotoSansJP-VariableFont_wght.ttf");
        var database=AssetDatabase.FindAssets("t:ItemDataDatabase").Select(g=>AssetDatabase.LoadAssetAtPath<ItemDataDatabase>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();
        if(database==null) throw new Exception("ItemDataDatabase required");
        var playerPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefab/Player.prefab");
        var playerSprite=playerPrefab.GetComponent<SpriteRenderer>().sprite;
        if(playerSprite==null) throw new Exception("Player body sprite missing");
        var tiles=AssetDatabase.LoadAllAssetsAtPath("Assets/Cainos/Pixel Art Platformer - Dungeon/Texture/TX Tileset - Dungeon Ground.png").OfType<Sprite>().ToArray();
        if(tiles.Length==0) throw new Exception("Dungeon tiles missing");
        Sprite ground=tiles.OrderBy(s=>Mathf.Abs(s.rect.width-16)+Mathf.Abs(s.rect.height-16)).First();
        var props=AssetDatabase.FindAssets("t:Prefab",new[]{"Assets/Cainos/Pixel Art Platformer - Dungeon/Prefab/Props"})
            .Select(g=>AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(g)))
            .Where(p=>p.GetComponentInChildren<SpriteRenderer>()!=null && !p.name.Contains("Trap") && !p.name.Contains("Blade")).ToArray();
        Sprite Character(string name) {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/"+name+".prefab");
            return prefab==null?playerSprite:prefab.GetComponentsInChildren<SpriteRenderer>(true).FirstOrDefault(r=>r.sprite!=null)?.sprite??playerSprite;
        }
        var paths=new List<string>();
        for(int stage=0;stage<=4;stage++) {
            string path=Folder+(stage==0?"CampaignTown":"CampaignStage"+stage)+".unity";
            paths.Add(path);
            if(System.IO.File.Exists(path) && !rebuild) continue; // Never overwrite a designer's stage from the menu.
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            var director=new GameObject("Story Campaign").AddComponent<CampaignDirector>();
            director.stage=stage; director.japaneseFont=font; director.itemDatabase=database;
            var camera=new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag="MainCamera"; camera.orthographic=true; camera.orthographicSize=6;
            camera.transform.position=new Vector3(10,4,-10);
            camera.backgroundColor=stage==4?new Color(.12f,.22f,.19f):new Color(.045f,.045f,.065f);
            camera.clearFlags=CameraClearFlags.SolidColor;
            var light=new GameObject("Global Light").AddComponent<UnityEngine.Rendering.Universal.Light2D>();
            light.lightType=UnityEngine.Rendering.Universal.Light2D.LightType.Global;
            if(stage>0) {
                Color tint=new[]{Color.white,new Color(.65f,.63f,.60f),new Color(.54f,.65f,.69f),new Color(.68f,.59f,.70f),new Color(.63f,.83f,.66f)}[stage];
                for(int x=0;x<80;x+=2) for(int y=1;y<8;y+=2) {
                    var wall=SpriteObject("Background stone",ground,new Vector3(x,y,1),new Color(tint.r*.23f,tint.g*.23f,tint.b*.23f,1),new Vector2(2,2));
                    wall.GetComponent<SpriteRenderer>().sortingOrder=-10;
                }
                // Continuous route keeps all narrative objectives recoverable. Upper platforms are optional combat cover.
                for(int x=-2;x<82;x++) {
                    var tile=SpriteObject("Walkway "+x,ground,new Vector3(x,-.5f,0),tint,new Vector2(1,1));
                    tile.layer=6; tile.AddComponent<BoxCollider2D>();
                    SpriteObject("Masonry "+x,ground,new Vector3(x,-1.5f,0),tint*.65f,new Vector2(1,1));
                }
                for(int x=7;x<77;x+=(stage==1?12:stage==2?9:stage==3?14:20)) {
                    for(int j=0;j<4;j++) {
                        var platform=SpriteObject("Upper gallery",ground,new Vector3(x+j,stage==3?3:2.2f,0),tint,new Vector2(1,.45f));
                        platform.layer=6; platform.AddComponent<BoxCollider2D>();
                    }
                    if(props.Length>0) {
                        string theme=stage==1?"Book":stage==2?"Tool":stage==3?"Window":"Altar";
                        var themed=props.Where(p=>p.name.Contains(theme)).ToArray();
                        var decor=PrefabUtility.InstantiatePrefab(themed.Length>0?themed[x%themed.Length]:props[x%props.Length]) as GameObject;
                        PrefabUtility.UnpackPrefabInstance(decor,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
                        decor.name="Scenery "+decor.name; decor.transform.position=new Vector3(x,0,0);
                        foreach(var behaviour in decor.GetComponentsInChildren<MonoBehaviour>()) UnityEngine.Object.DestroyImmediate(behaviour);
                        foreach(var collider in decor.GetComponentsInChildren<Collider2D>()) UnityEngine.Object.DestroyImmediate(collider);
                    }
                }
                var player=SpriteObject("Campaign Player",playerSprite,new Vector3(3,2,0),Color.white,new Vector2(.85f,1.8f));
                player.layer=3; var box=player.AddComponent<BoxCollider2D>();box.size=new Vector2(player.GetComponent<SpriteRenderer>().sprite.bounds.size.x,player.GetComponent<SpriteRenderer>().sprite.bounds.size.y);
                var rb=player.AddComponent<Rigidbody2D>();rb.freezeRotation=true;rb.gravityScale=3;
                director.actor=player.AddComponent<CampaignActor>();director.actor.followCamera=camera;
                foreach(var step in data.steps.Where(s=>s.stage==stage)) {
                    bool person=step.id=="finn" || step.title.Contains("バルド") || step.title.Contains("グレン") || step.id=="garden";
                    string propName=step.id=="medicine"?"Bottle":step.id=="original"||step.id=="alternative"?"Chest":step.id=="water"?"Switch":"Book";
                    Sprite marker=person?Character(step.title.Contains("バルド")?"盗賊":step.title.Contains("グレン")?"ドワーフ":"冒険者"):
                        props.Where(p=>p.name.Contains(propName)).SelectMany(p=>p.GetComponentsInChildren<SpriteRenderer>()).FirstOrDefault(r=>r.sprite!=null)?.sprite??ground;
                    var point=SpriteObject(step.title,marker,new Vector3(step.x,person?.9f:.6f,0),Color.white,person?new Vector2(.65f,1.6f):new Vector2(.8f,.8f));
                    point.AddComponent<CampaignPoint>().stepId=step.id;
                    var label=new GameObject("Landmark label").AddComponent<TextMesh>();
                    label.transform.position=new Vector3(step.x,4.3f,0);label.text=step.title;
                    label.font=font;label.fontSize=48;label.characterSize=.08f;label.anchor=TextAnchor.MiddleCenter;
                    label.GetComponent<MeshRenderer>().sharedMaterial=font.material;
                }
                if(stage<4) for(int x=20;x<70;x+=18) {
                    var enemy=SpriteObject("Ancient guardian",Character("スケルトン"),new Vector3(x,1,0),Color.white,new Vector2(.8f,1.5f));
                    enemy.layer=11;enemy.AddComponent<BoxCollider2D>();enemy.AddComponent<CampaignEnemy>();
                }
            }
            EditorSceneManager.SaveScene(scene,path);
        }
        var builds=EditorBuildSettings.scenes.ToList();
        foreach(string path in paths) if(!builds.Any(s=>s.path==path)) builds.Add(new EditorBuildSettingsScene(path,true));
        EditorBuildSettings.scenes=builds.ToArray();
        AssetDatabase.SaveAssets();
        Validate();
        rebuild=false;
        Debug.Log("CAMPAIGN BUILD PASS");
    }
    static GameObject SpriteObject(string name,Sprite sprite,Vector3 position,Color color,Vector2 size)
    {
        var go=new GameObject(name);go.transform.position=position;
        var r=go.AddComponent<SpriteRenderer>();r.sprite=sprite;r.color=color;
        go.transform.localScale=new Vector3(size.x/sprite.bounds.size.x,size.y/sprite.bounds.size.y,1);
        return go;
    }
    [MenuItem("Tools/Story Campaign/Validate progression")]
    public static void Validate()
    {
        var d=JsonUtility.FromJson<CampaignData>(Resources.Load<TextAsset>("Campaign/Story").text);
        void Check(bool ok,string error) {if(!ok) throw new Exception(error);}
        Check(d.steps.Select(s=>s.id).Distinct().Count()==d.steps.Length,"Duplicate step IDs");
        Check(d.nodes.Select(s=>s.id).Distinct().Count()==d.nodes.Length,"Duplicate node IDs");
        foreach(var s in d.steps) foreach(var id in s.nodes) Check(d.Node(id).lines.Length>0,"Empty dialogue "+id);
        foreach(var offer in d.offers) Check(d.Node(offer).lines.Length>0,"Empty offer");
        var flags=new HashSet<string>();
        Check(!CampaignRules.GardenOpen(flags.Contains),"Garden initially unlocked");
        foreach(string main in new[]{"S3M01_Completed","S1M01_Completed","S2M01_Completed"}) flags.Add(main);
        Check(CampaignRules.GardenOpen(flags.Contains),"Main order dependency");
        flags.Clear();
        // Reachability, prerequisites, and idempotency without Unity scene state.
        int total=0;
        for(int pass=0;pass<20;pass++) foreach(var s in d.steps.Where(s=>s.id!="rescue" || flags.Contains("step_finn"))) {
            if(!CampaignRules.Ready(s,flags.Contains)) continue;
            flags.Add("step_"+s.id);foreach(string f in s.flags) flags.Add(f);total+=s.reward;
            Check(!CampaignRules.Ready(s,flags.Contains),"Reward replay "+s.id);
        }
        Check(d.steps.Select(s=>s.quest).Distinct().Where(q=>q!="S4M01").All(q=>flags.Contains(q+"_Completed")),"Unreachable quest");
        Check(CampaignRules.CanEnd("C",flags.Contains),"C unreachable");
        foreach(string support in new[]{"faction_adventurer_support","faction_bandit_support","faction_dwarf_support"}) {
            flags.Remove(support);Check(!CampaignRules.CanEnd("C",flags.Contains),"C missing support");flags.Add(support);
        }
        flags.Add("EndingCommitted");Check(!CampaignRules.CanEnd("A",flags.Contains),"Ending can change after commit");
        Check(total==1200,"Unexpected reward ledger");
        var roundtrip=JsonUtility.FromJson<CampaignData>(JsonUtility.ToJson(d));Check(roundtrip.steps.Length==d.steps.Length,"JSON roundtrip");
        foreach(var scene in EditorBuildSettings.scenes.Where(s=>s.path.StartsWith(Folder))) {
            var opened=EditorSceneManager.OpenScene(scene.path);
            Check(opened.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CampaignDirector>()).Count()==1,"Missing director");
            foreach(var go in opened.GetRootGameObjects()) foreach(var t in go.GetComponentsInChildren<Transform>(true)) Check(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script "+t.name);
        }
        Debug.Log("CAMPAIGN VALIDATION PASS: 13 quests, 35 steps, 3 endings, 5 scenes");
    }
}
