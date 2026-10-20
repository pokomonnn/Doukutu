using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static partial class StoryIntegrationBuilder
{
    const string Root="Assets/Resources/StoryCampaign";
    static CampaignData source;
    static StoryCampaignDatabase database;
    static void Put(object target,string name,object value)
    {
        var field=target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
        if(field==null) throw new Exception(target.GetType().Name+" missing "+name);
        field.SetValue(target,value);
        if(target is UnityEngine.Object o) EditorUtility.SetDirty(o);
    }
    static T Asset<T>(string name) where T:ScriptableObject
    {
        string path=Root+"/"+name+".asset";
        var asset=AssetDatabase.LoadAssetAtPath<T>(path);
        if(asset==null) {asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);}
        return asset;
    }
    static DialogueCondition2D Flag(string name,bool set=true) => new DialogueCondition2D {
        kind=set?DialogueConditionKind2D.StoryFlagSet:DialogueConditionKind2D.StoryFlagUnset, storyFlag="story/"+name};
    static List<DialogueCondition2D> Conditions(StoryCampaignStep s)
    {
        var result=(s.requires??Array.Empty<string>()).Select(f=>Flag(f)).ToList();
        result.Add(Flag(s.quest+"_Completed",false));
        result.AddRange((s.blockedBy??Array.Empty<string>()).Select(f=>Flag(f,false)));
        if(s.grantItem==null) result.Add(Flag("step_"+s.id,false));
        return result;
    }
    static IEnumerable<string> Lines(IEnumerable<string> ids) => ids.SelectMany(id=>source.Node(id).lines);
    static void ParseLine(string text,out string speaker,out string body)
    {
        int sep=text.IndexOf("：「",StringComparison.Ordinal);
        speaker=sep>=0?text.Substring(0,sep):"";
        body=sep>=0?text.Substring(sep+2).TrimEnd('」'):text;
    }
    static List<DialogueNode2D> DialogueLines(IEnumerable<string> lines,string prefix,string next="")
    {
        var texts=lines.ToArray();var nodes=new List<DialogueNode2D>();
        for(int i=0;i<texts.Length;i++) {
            ParseLine(texts[i],out string speaker,out string body);
            nodes.Add(new DialogueNode2D{id=prefix+i,speakerName=speaker,body=body,nextNodeId=i==texts.Length-1?next:prefix+(i+1)});
        }
        return nodes;
    }
    static DialogueData2D MakeDialogue(string id,string title,List<DialogueNode2D> nodes,List<DialogueAction2D> actions=null)
    {
        var data=Asset<DialogueData2D>("Dialogue_"+id);
        Put(data,"dialogueId","story_"+id);Put(data,"displayName",title);Put(data,"startNodeId",nodes[0].id);
        Put(data,"nodes",nodes);Put(data,"actions",actions??new List<DialogueAction2D>());
        Put(data,"startRoutes",new List<DialogueConditionalRoute2D>());return data;
    }
    static DialogueAction2D Action(string id)=>new DialogueAction2D{eventId=id,kind=DialogueActionKind2D.StoryCampaign,oncePerRuntime=false};
    static void BuildData()
    {
        System.IO.Directory.CreateDirectory(Root);AssetDatabase.Refresh();
        source=JsonUtility.FromJson<CampaignData>(AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/Campaign/Story.json").text);
        ApplyFinnRevision();
        database=Asset<StoryCampaignDatabase>("Database");database.quests.Clear();database.steps.Clear();
        foreach(var group in source.steps.GroupBy(s=>s.quest)) {
            string id=group.Key;
            var mission=Asset<MissionDefinition2D>("Mission_"+id);
            Put(mission,"missionId","story_"+id);Put(mission,"displayName",group.First().questTitle);
            Put(mission,"description",string.Join("\n",group.Select(s=>s.title)));
            Put(mission,"objectiveType",MissionObjectiveType2D.StoryFlag);Put(mission,"requiredAmount",1);
            Put(mission,"completionStoryFlag","story/"+id+"_Completed");
            Put(mission,"activationStoryFlag","story/"+id+"_Started");
            if(id=="S1S01") {
                Put(mission,"completionStoryFlag","story/S1S01_Delivered");
                Put(mission,"storyMoneyReward",100);
            }
            var story=Asset<StoryQuestDefinition>("Quest_"+id);
            Put(story,"storyId","story_"+id);Put(story,"displayName",group.First().questTitle);
            Put(story,"description",string.Join(" → ",group.Select(s=>s.title)));
            Put(story,"storyType",id.Contains("M01")?StoryQuestType.Main:StoryQuestType.Sub);
            Put(story,"linkedMission",mission);
            database.quests.Add(new StoryCampaignQuest{id=id,mission=mission,story=story});
        }
        var items=new Dictionary<string,ItemData>();
        foreach(string id in new[]{"blueprint","original","alternative","bearing","valve","gear"}) {
            string itemId=id=="alternative"?"original":id;
            if(items.ContainsKey(itemId)) {items[id]=items[itemId];continue;}
            var item=Asset<ItemData>("Item_"+itemId);
            Put(item,"itemId","story_"+itemId);
            Put(item,"icon",MarkerSprite(new StoryCampaignStep{id=id,title="クエスト用品"}));
            Put(item,"canSellToShop",false);Put(item,"width",1);Put(item,"height",1);
            Put(item,"weight",.2f);Put(item,"displayName",source.steps.First(s=>s.id==id).title);
            // Existing non-localized fallback is used for authored quest-item names.
            items[id]=item;
        }
        foreach(var s in source.steps) {
            if(s.id=="rescue") continue; // Real elevator extraction owns this event.
            var step=new StoryCampaignStep{id=s.id,quest=s.quest,title=s.title,stage=s.stage,reward=s.reward,requires=s.requires,flags=s.flags};
            step.stage=AuthoredStage(s.id,s.stage);
            if(s.id=="bearing" || s.id=="valve" || s.id=="gear")step.blockedBy=new[]{"S2S03_Installed"};
            if(items.TryGetValue(s.id,out var item)) step.grantItem=item;
            string[] consumed=s.id=="deliverMedicine"?new[]{"medicine"}:s.id=="reportBlueprint"?new[]{"blueprint"}:
                s.id=="reportSupply"?new[]{"original"}:s.id=="install"?new[]{"bearing","valve","gear"}:Array.Empty<string>();
            step.consumeItems=consumed.Select(i=>items[i]).ToArray();
            var action=Action("step/"+s.id);
            var nodes=DialogueLines(Lines(s.nodes),"line");
            var actions=new List<DialogueAction2D>{action};
            if(s.id=="garden") {
                nodes.Last().choices.Add(new DialogueChoice2D{label="町のことを伝え、これからについて話す",eventId=action.eventId});
            } else {
                nodes.Insert(0,new DialogueNode2D{id="start",body=s.title,choices=new List<DialogueChoice2D>{
                    new DialogueChoice2D{label=step.grantItem!=null?"回収する":"調べる・話す",nextNodeId="line0",eventId=action.eventId,conditions=Conditions(step)},
                    new DialogueChoice2D{label="今はやめておく"}}});
                if(s.id=="deliverMedicine" || s.id=="reportPermit" || s.id=="reportTrade") {
                    string offer=source.offers.First(o=>o.StartsWith(s.quest+"/"));
                    var offerNodes=DialogueLines(Lines(new[]{offer}),"offer");
                    string accept="accept/"+s.quest;actions.Add(Action(accept));
                    offerNodes.Last().choices.Add(new DialogueChoice2D{label="引き受ける",eventId=accept});
                    offerNodes.Last().choices.Add(new DialogueChoice2D{label="今はやめておく"});
                    nodes[0].choices.Insert(0,new DialogueChoice2D{label="依頼について聞く",nextNodeId="offer0",conditions=s.id=="reportPermit"?new List<DialogueCondition2D>{Flag("PlayerBanditTruce")}:new List<DialogueCondition2D>()});
                    nodes.AddRange(offerNodes);
                }
            }
            step.dialogue=MakeDialogue(s.id,s.title,nodes,actions);
            database.steps.Add(step);
        }
        var allItems=AssetDatabase.FindAssets("t:ItemDataDatabase").Select(g=>AssetDatabase.LoadAssetAtPath<ItemDataDatabase>(AssetDatabase.GUIDToAssetPath(g)));
        foreach(var db in allItems) {
            var so=new SerializedObject(db);var list=so.FindProperty("itemDataList");
            foreach(var item in items.Values.Distinct()) AppendReference(list,item);
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        foreach(string id in AssetDatabase.FindAssets("t:StoryQuestDatabase")) {
            var db=AssetDatabase.LoadAssetAtPath<StoryQuestDatabase>(AssetDatabase.GUIDToAssetPath(id));
            var so=new SerializedObject(db);foreach(var q in database.quests) AppendReference(so.FindProperty("stories"),q.story);so.ApplyModifiedPropertiesWithoutUndo();
            var entries=so.FindProperty("stories");
            for(int i=entries.arraySize-1;i>=0;i--) {
                var old=entries.GetArrayElementAtIndex(i).objectReferenceValue as StoryQuestDefinition;
                if(old!=null && old.StoryId=="story_S1S02") {
                    entries.GetArrayElementAtIndex(i).objectReferenceValue=null;entries.DeleteArrayElementAtIndex(i);
                }
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        EditorUtility.SetDirty(database);
        BuildTownData();BuildEndings();BuildQuestHubs();ConfigureObjectiveData();MissionMinimapDataInstaller.Configure(database);AssetDatabase.SaveAssets();
    }
    static void AppendReference(SerializedProperty list,UnityEngine.Object value)
    {
        for(int i=0;i<list.arraySize;i++) if(list.GetArrayElementAtIndex(i).objectReferenceValue==value)return;
        list.InsertArrayElementAtIndex(list.arraySize);list.GetArrayElementAtIndex(list.arraySize-1).objectReferenceValue=value;
    }
    static TownConversationChoice Choice(string text,string next,string action=null,List<DialogueCondition2D> conditions=null)
    {
        var c=new TownConversationChoice{storyEventId=action,storyConditions=conditions??new List<DialogueCondition2D>()};
        Put(c,"choiceText",text);Put(c,"nextBlockId",next);Put(c,"action",action==null?TownConversationChoiceAction.GoToBlock:TownConversationChoiceAction.StoryCampaign);return c;
    }
    static TownConversationBlock Block(string id,IEnumerable<string> lines,List<TownConversationChoice> choices,string next="")
    {
        var b=new TownConversationBlock();Put(b,"blockId",id);Put(b,"importantConversation",true);Put(b,"nextBlockId",next);
        var pages=new List<TownConversationPage>();
        foreach(string line in lines) {ParseLine(line,out string speaker,out string body);var page=new TownConversationPage();Put(page,"speakerNameOverride",speaker);Put(page,"message",body);pages.Add(page);}
        Put(b,"pages",pages);Put(b,"choices",choices);return b;
    }
    static TownConversationData TownAsset(string id,string name,List<TownConversationBlock> blocks)
    {
        var d=Asset<TownConversationData>("Town_"+id);Put(d,"residentId","story_"+id);Put(d,"residentName",name);
        Put(d,"firstConversationBlockId","menu");Put(d,"repeatConversationBlockId","menu");Put(d,"blocks",blocks);return d;
    }
    static void BuildTownData()
    {
        foreach(string role in new[]{"Mayor","Roan","Glen"}) {
            string name=role=="Mayor"?"町長":role=="Roan"?"ロアン":"グレン";
            if(role!="Mayor") {
                TownAsset(role,name,new List<TownConversationBlock>{Block("menu",new[]{"依頼の相談・報告は、各ダンジョンの野営地・作業場で行えます。"},new List<TownConversationChoice>{Choice("閉じる","")})});
                continue;
            }
            var blocks=new List<TownConversationBlock>();var menu=new List<TownConversationChoice>();
            var quests=database.quests.Where(q=>q.id.StartsWith("S4")?false:role=="Mayor"?q.id.Contains("M01"):q.id.EndsWith(role=="Roan"?"S01":"S03"));
            foreach(var q in quests) {
                string offer=source.offers.First(o=>o.StartsWith(q.id+"/"));
                menu.Add(Choice(q.story.DisplayName,q.id));
                var choices=new List<TownConversationChoice>{Choice("依頼を聞く",q.id+"offer"),Choice("戻る","menu")};
                foreach(var step in database.steps.Where(s=>s.quest==q.id && s.stage==0)) {
                    choices.Insert(0,Choice(step.title,"done_"+step.id,"step/"+step.id,Conditions(step)));
                    blocks.Add(Block("done_"+step.id,Lines(source.steps.First(s=>s.id==step.id).nodes),new List<TownConversationChoice>{Choice("戻る","menu")}));
                }
                blocks.Add(Block(q.id,new[]{q.story.DisplayName},choices));
                blocks.Add(Block(q.id+"offer",Lines(new[]{offer}),new List<TownConversationChoice>{
                    Choice("引き受ける","menu","accept/"+q.id,new List<DialogueCondition2D>{Flag(q.id+"_Completed",false)}),Choice("今はやめる","menu")}));
            }
            // Permit belongs to the mayor, even though Baldo requests the work in the dungeon.
            if(role=="Mayor") {
                var permit=database.steps.First(s=>s.id=="permit");
                menu.Add(Choice("搬入許可の相談","permit"));
                blocks.Add(Block("permit",new[]{"倉庫の目録と、盗賊たちとの停戦が必要です。"},new List<TownConversationChoice>{
                    Choice("目録を示して交渉する","permitDone","step/permit",Conditions(permit)),Choice("戻る","menu")}));
                blocks.Add(Block("permitDone",Lines(source.steps.First(s=>s.id=="permit").nodes),new List<TownConversationChoice>{Choice("戻る","menu")}));
            }
            menu.Add(Choice("話を終える",""));
            blocks.Insert(0,Block("menu",new[]{"何について話しますか？"},menu));TownAsset(role,name,blocks);
        }
        TownAsset("Butler","執事",new List<TownConversationBlock>{Block("menu",new[]{"執事がこちらを見ている。"},new List<TownConversationChoice>{
            Choice("話を聞く","first","butler",new List<DialogueCondition2D>{Flag("ButlerAppeared",false)}),
            Choice("その後について聞く","repeat",null,new List<DialogueCondition2D>{Flag("ButlerAppeared")}),Choice("立ち去る","")}),
            Block("first",Lines(new[]{"S1M01/BUTLER_FIRST"}),new List<TownConversationChoice>()),Block("repeat",Lines(new[]{"S1M01/BUTLER_REPEAT"}),new List<TownConversationChoice>())});
        var epilogueBlocks=new List<TownConversationBlock>();var endingMenu=new List<TownConversationChoice>();
        foreach(string kind in new[]{"A","B","C"}) {
            endingMenu.Add(Choice("帰還後の人々の話を聞く",kind,null,new List<DialogueCondition2D>{Flag("EndingType_"+kind)}));
            epilogueBlocks.Add(Block(kind,source.nodes.Where(n=>n.id.StartsWith("S4M01/END_"+kind+"_") && !n.id.Contains("REPEAT") && !n.id.Contains("OPTIONAL")).SelectMany(n=>n.lines),new List<TownConversationChoice>()));
        }
        endingMenu.Add(Choice("閉じる",""));epilogueBlocks.Insert(0,Block("menu",new[]{"捜索を終えた町の人々。"},endingMenu));TownAsset("Epilogue","町の人々",epilogueBlocks);
    }
    static void BuildEndings()
    {
        var menu=new DialogueNode2D{id="menu",body="貴族と町のこれからについて、どう伝えようか。"};
        var nodes=new List<DialogueNode2D>{menu};var actions=new List<DialogueAction2D>();
        foreach(string kind in new[]{"A","B","C"}) {
            var conditions=new List<DialogueCondition2D>{Flag("S4M01_StorySeen"),Flag("EndingCommitted",false)};
            if(kind=="C") conditions.AddRange(new[]{"adventurer","bandit","dwarf"}.Select(f=>Flag("faction_"+f+"_support")));
            menu.choices.Add(new DialogueChoice2D{label=kind=="A"?"ふたりの時間を尊重する":kind=="B"?"町へ戻ってほしい":"三派閥と古城を再建する",nextNodeId="confirm"+kind,conditions=conditions});
            var endingLines=source.nodes.Where(n=>n.id.StartsWith("S4M01/S4_END_"+kind+"_")&&!n.id.EndsWith("CONFIRM")).SelectMany(n=>n.lines);
            nodes.AddRange(DialogueLines(endingLines,kind));
            nodes.Add(new DialogueNode2D{id="confirm"+kind,body=string.Join("\n",source.Node("S4M01/S4_END_"+kind+"_CONFIRM").lines),choices=new List<DialogueChoice2D>{
                new DialogueChoice2D{label="この結末を選ぶ",eventId="ending/"+kind,nextNodeId=kind+"0",conditions=conditions},new DialogueChoice2D{label="もう少し考える",nextNodeId="menu"}}});
            actions.Add(Action("ending/"+kind));
            MakeDialogue("Ending"+kind,"庭での結末",DialogueLines(endingLines,kind));
        }
        menu.choices.Add(new DialogueChoice2D{label="今は保留する（帰還・再訪できます）"});
        MakeDialogue("GardenChoices","消えた貴族",nodes,actions);
    }
}
