using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class StoryIntegrationBuilder
{
    static int AuthoredStage(string id,int fallback)
    {
        switch(id) {
            case "reportFinn": case "reportBlueprint": return 1;
            case "reportSupply": case "reportWater": return 2;
            case "proposal": case "reportTruce": case "reportCastle": return 3;
            default: return fallback;
        }
    }
    static string[] HubSteps(int stage,string role)
    {
        if(role=="Roan")return stage==1?Array.Empty<string>():stage==2?new[]{"reportSupply"}:new[]{"proposal","reportTruce"};
        if(role=="Glen")return stage==1?new[]{"reportBlueprint"}:stage==2?new[]{"install","reportWater"}:new[]{"reportCastle"};
        return stage==1?new[]{"reportFinn"}:stage==2?new[]{"reportPermit"}:new[]{"truce","reportTrade"};
    }
    static string RoleName(string role)=>role=="Roan"?"ロアン":role=="Glen"?"グレン":"バルド";
    static void BuildQuestHubs()
    {
        for(int stage=1;stage<=3;stage++)foreach(string role in new[]{"Roan","Glen","Baldo"}) {
            string quest="S"+stage+(role=="Roan"?"S01":role=="Glen"?"S03":"S02");
            if(stage==1 && role=="Baldo")quest="S1S01";
            var menu=new DialogueNode2D{id="menu",speakerName=RoleName(role),body="依頼について話す。"};
            var nodes=new List<DialogueNode2D>{menu};var actions=new List<DialogueAction2D>();
            string offer=source.offers.First(o=>o.StartsWith(quest+"/"));
            var offerConditions=new List<DialogueCondition2D>{Flag(quest+"_Completed",false),Flag(quest+"_Started",false)};
            if(quest=="S2S02")offerConditions.Add(Flag("PlayerBanditTruce"));
            menu.choices.Add(new DialogueChoice2D{label="依頼を聞く",nextNodeId="offer0",conditions=offerConditions});
            var offerNodes=DialogueLines(Lines(new[]{offer}),"offer");
            offerNodes.Last().choices.Add(new DialogueChoice2D{label="引き受ける",eventId="accept/"+quest,nextNodeId="menu",conditions=offerConditions});
            offerNodes.Last().choices.Add(new DialogueChoice2D{label="今はやめておく"});
            nodes.AddRange(offerNodes);actions.Add(Action("accept/"+quest));
            // The request remains readable after acceptance without accepting or rewarding twice.
            menu.choices.Add(new DialogueChoice2D{label="依頼内容を確認する",nextNodeId="reminder0",conditions=new List<DialogueCondition2D>{Flag(quest+"_Started"),Flag(quest+"_Completed",false)}});
            nodes.AddRange(DialogueLines(Lines(new[]{offer}),"reminder"));
            foreach(string id in HubSteps(stage,role)) {
                var step=database.steps.First(s=>s.id==id);
                string prefix=id+"_";
                menu.choices.Add(new DialogueChoice2D{label=step.title,nextNodeId=prefix+"0",eventId="step/"+id,conditions=Conditions(step)});
                menu.choices.Add(new DialogueChoice2D{label="前の報告を振り返る："+step.title,nextNodeId=prefix+"0",conditions=new List<DialogueCondition2D>{Flag("step_"+id)}});
                nodes.AddRange(DialogueLines(Lines(source.steps.First(s=>s.id==id).nodes),prefix));
                actions.Add(Action("step/"+id));
            }
            menu.choices.Add(new DialogueChoice2D{label="話を終える"});
            MakeDialogue("Hub_"+stage+"_"+role,RoleName(role),nodes,actions);
        }
    }
    public static void ReconcileAndRunTests()
    {
        BuildData();ReconcileQuestLocations();StoryIntegrationTests.RunPlay();
    }
    static void ReconcileQuestLocations()
    {
        for(int stage=0;stage<Scenes.Length;stage++) {
            var scene=EditorSceneManager.OpenScene(Scenes[stage]);
            var root=scene.GetRootGameObjects().First(g=>g.name=="StoryIntegration");
            RemoveRetiredMissionEntries();
            if(stage==0) {
                foreach(var link in root.GetComponentsInChildren<StoryTownLink>(true))
                    if(link.conversation!=null && (link.conversation.name=="Town_Roan" || link.conversation.name=="Town_Glen"))link.gameObject.SetActive(false);
            } else if(stage<=3) {
                Physics2D.SyncTransforms();
                var allPoints=root.GetComponentsInChildren<StoryInteractionPoint>(true);
                var occupied=allPoints.Where(p=>p.gameObject.activeSelf).Select(p=>p.transform.position).ToList();
                foreach(string role in new[]{"Roan","Glen","Baldo"}) {
                    string name="StoryHub_"+role;
                    var existing=root.transform.Find(name);
                    GameObject hub;
                    if(existing!=null)hub=existing.gameObject;
                    else {
                        var replaced=allPoints.FirstOrDefault(p=>HubSteps(stage,role).Contains(p.stepId));
                        Vector3 position;
                        if(replaced!=null)position=replaced.transform.position;
                        else {
                            var anchor=allPoints.First(p=>p.gameObject.activeSelf).transform.position;
                            position=FindHubGround(anchor,occupied);
                        }
                        hub=new GameObject(name);hub.transform.SetParent(root.transform);hub.transform.position=position;occupied.Add(position);
                        var render=hub.AddComponent<SpriteRenderer>();
                        string character=role=="Roan"?"冒険者":role=="Glen"?"ドワーフ":"盗賊";
                        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Characters/"+character+".prefab");
                        if(prefab==null)throw new Exception("Missing NPC art: "+character);
                        render.sprite=prefab.GetComponentsInChildren<SpriteRenderer>(true).First(r=>r.sprite!=null).sprite;render.sortingOrder=5;
                        hub.transform.localScale=Vector3.one*1.5f/render.sprite.bounds.size.y;
                        var point=hub.AddComponent<StoryInteractionPoint>();point.stepId="hub_"+stage+"_"+role;
                        var actor=hub.AddComponent<DialogueActor2D>();Put(actor,"useEKeyInteraction",true);Put(actor,"interactionDistance",2.5f);Put(actor,"preventWhenHostileToPlayer",false);
                        var label=new GameObject(name+" label",typeof(TextMeshPro));label.transform.SetParent(root.transform);label.transform.position=position+Vector3.up*2;
                        var text=label.GetComponent<TextMeshPro>();text.font=Font;text.fontSize=3;text.text=RoleName(role)+"\n[E] 依頼・報告";text.alignment=TextAlignmentOptions.Center;text.rectTransform.sizeDelta=new Vector2(8,2);
                        var zone=new GameObject(name+" negotiation").AddComponent<StoryNegotiationZone>();zone.transform.SetParent(root.transform);zone.transform.position=position;
                    }
                    Put(hub.GetComponent<DialogueActor2D>(),"dialogue",Asset<DialogueData2D>("Dialogue_Hub_"+stage+"_"+role));
                    string quest="S"+stage+(role=="Roan"?"S01":role=="Glen"?"S03":"S02");
                    if(stage==1 && role=="Baldo")quest="S1S01";
                    foreach(var manager in UnityEngine.Object.FindObjectsByType<MissionManager2D>(FindObjectsInactive.Include)) {
                        var serialized=new SerializedObject(manager);var entries=serialized.FindProperty("missions");
                        for(int i=0;i<entries.arraySize;i++) {
                            var entry=entries.GetArrayElementAtIndex(i);
                            if(entry.FindPropertyRelative("mission").objectReferenceValue==database.quests.First(q=>q.id==quest).mission)
                                entry.FindPropertyRelative("compassTarget").objectReferenceValue=hub.transform;
                        }
                        serialized.ApplyModifiedPropertiesWithoutUndo();
                    }
                    foreach(var point in allPoints.Where(p=>HubSteps(stage,role).Contains(p.stepId))) {
                        point.gameObject.SetActive(false);
                        foreach(var label in root.GetComponentsInChildren<TextMeshPro>())
                            if(label.name=="Story label" && Vector3.Distance(label.transform.position,point.transform.position+Vector3.up*2)<.1f)label.gameObject.SetActive(false);
                    }
                }
                // Finn's rescue route must retain the ordinary risk of transport.
                var finn=root.GetComponentInChildren<StoryRescueIdentity>(true);
                if(stage==1)ConfigureFinnScene(root);
                if(finn!=null) {
                    var info=finn.GetComponent<RescuePersonInfo2D>();
                    if(info!=null) {
                        Put(info,"displayName","フィン");Put(info,"occupation","冒険者（斥候）");Put(info,"condition",RescuePersonCondition2D.Injured);
                        PrefabUtility.RecordPrefabInstancePropertyModifications(info);
                    }
                }
                if(finn!=null)foreach(var zone in root.GetComponentsInChildren<StoryNegotiationZone>())
                    if(string.IsNullOrEmpty(zone.requiredFlag) && Vector3.Distance(zone.transform.position,finn.transform.position)<.1f)zone.gameObject.SetActive(false);
            }
            InstallObjectiveHUD(root);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        }
        AssetDatabase.SaveAssets();Debug.Log("STORY SPEC RECONCILIATION PASS: dungeon quest hubs, town-only mayor, original rescue mechanics");
    }
    static Vector3 FindHubGround(Vector3 anchor,List<Vector3> occupied)
    {
        foreach(var candidate in UnityEngine.Object.FindObjectsByType<ItemBoxInteractable>(FindObjectsInactive.Exclude)
            .Where(b=>b.transform.position.x<0).SelectMany(b=>new[]{0f,-6f,6f,-12f,12f}.Select(dx=>b.transform.position+Vector3.right*dx)).OrderBy(p=>(p-anchor).sqrMagnitude)) {
            var hit=Physics2D.Raycast(candidate+Vector3.up*4,Vector2.down,16,1<<6);
            if(hit.collider==null)continue;
            Vector3 position=(Vector3)hit.point+Vector3.up*.8f;
            if(occupied.All(p=>Vector2.Distance(p,position)>=5))return position;
        }
        throw new Exception("No grounded space for quest NPC");
    }
}
