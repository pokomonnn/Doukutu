using System;
using System.Collections;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[InitializeOnLoad]
public static class QuestHUDVisualTests
{
    const string Key="QuestHUD.VisualTests";
    static IEnumerator flow;
    static int frame=-1;
    static double started;
    static QuestHUDVisualTests()
    {
        if(SessionState.GetBool(Key,false)){started=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}
    }
    public static void Run()
    {
        SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene(StoryIntegrationBuilder.Scenes[0]);
        EditorApplication.EnterPlaymode();
    }
    static void Tick()
    {
        try {
            if(EditorApplication.timeSinceStartup-started>240)throw new Exception("Visual test timeout");
            if(!EditorApplication.isPlaying || frame==Time.frameCount)return;
            frame=Time.frameCount;
            if(flow==null)flow=Flow();
            if(flow.MoveNext())return;
            CleanupSizes();SessionState.SetBool(Key,false);Debug.Log("QUEST HUD VISUAL PASS: text, bounds, button overlap, modal visibility, 5 resolutions");EditorApplication.Exit(0);
        } catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static IEnumerator Flow()
    {
        Directory.CreateDirectory("Logs/QuestHUD");
        foreach(string scene in new[]{"Town_Main","stage1"}) {
            if(SceneManager.GetActiveScene().name!=scene)SceneManager.LoadScene(scene);
            for(int i=0;i<80;i++)yield return null;
            foreach(var auto in UnityEngine.Object.FindObjectsByType<AutoSaveTrigger>(FindObjectsInactive.Include))auto.enabled=false;
            foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyChaser2D>(FindObjectsInactive.Include))enemy.enabled=false;
            var hud=UnityEngine.Object.FindObjectsByType<MissionHUD2D>(FindObjectsInactive.Include).First(h=>h.enabled && h.gameObject.scene==SceneManager.GetActiveScene());
            hud.enabled=false;
            var layout=hud.GetComponent<MissionHUDSafeArea2D>();layout.enabled=true;
            var panel=layout.panel;panel.gameObject.SetActive(true);
            var texts=hud.GetComponentsInChildren<TMP_Text>(true);
            var title=texts.First(t=>t.name=="QuestName");
            var body=texts.First(t=>t.name=="Objective");
            var notice=texts.First(t=>t.name=="CompletedObjective");
            foreach(var group in hud.GetComponentsInChildren<CanvasGroup>(true))group.alpha=1;
            foreach(var t in texts)t.enabled=true;
            title.text="【古城の設計図】";
            var objectives=StoryCampaignRuntime.Database.quests.SelectMany(q=>q.mission.DisplayObjectives).ToArray();
            string longest=objectives.OrderByDescending(o=>o.text.Length).First().text;
            body.text="□ "+longest;notice.text="完了："+longest;
            foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(2560,1080),new Vector2Int(1024,768)}) {
                SetResolution(size.x,size.y);
                for(int i=0;i<30;i++)yield return null;
                Canvas.ForceUpdateCanvases();
                panel.anchorMin=panel.anchorMax=Vector2.one;
                layout.RebuildLayout();
                var minimap=hud.GetComponent<DungeonMinimap2D>();
                if(scene=="stage1") {
                    if(minimap==null || minimap.CachedCellCount==0)throw new Exception("Minimap did not sample terrain");
                    if(size.x==1280)DungeonMinimapMarkerTests.Check(minimap);
                    CheckBounds(minimap.Frame,size);
                    if(ScreenRect(minimap.Frame).Overlaps(ScreenRect(panel)))throw new Exception("Minimap overlaps tracker");
                }
                foreach(var objective in objectives) {
                    body.text="□ "+objective.text;notice.text="完了："+objective.text;
                    layout.RebuildLayout(); Canvas.ForceUpdateCanvases(); foreach(var t in texts)t.ForceMeshUpdate();
                    if(body.isTextOverflowing || notice.isTextOverflowing)
                        throw new Exception("QUEST HUD OVERFLOW "+scene+" "+size+" "+objective.text);
                    CheckBounds(panel,size);CheckBounds((RectTransform)notice.transform.parent,size);
                    CheckButtons(panel);CheckButtons((RectTransform)notice.transform.parent);
                }
                body.text="□ "+longest;notice.text="完了："+longest;
                Canvas.ForceUpdateCanvases();
                for(int i=0;i<3;i++)yield return null;
                var image=CaptureGameView();
                if(image.width!=size.x || image.height!=size.y)throw new Exception("Resolution mismatch");
                File.WriteAllBytes("Logs/QuestHUD/"+scene+"-"+size.x+"x"+size.y+".png",image.EncodeToPNG());
                Debug.Log("QUEST HUD CAPTURE "+scene+" requested="+size+" actual="+image.width+"x"+image.height);
                UnityEngine.Object.Destroy(image);
                if(scene=="stage1") {
                    if(size.x==1280) {
                        var interactions=InteractionPolishTests.Check();
                        while(interactions.MoveNext())yield return interactions.Current;
                    }
                    var dialogueChecks=DungeonUIVisualChecks.Check(size);
                    while(dialogueChecks.MoveNext())yield return dialogueChecks.Current;
                }
            }
            if(scene=="Town_Main") {
                var talk=UnityEngine.Object.FindFirstObjectByType<TownConversationController>();
                talk.OpenConversation(Resources.Load<TownConversationData>("StoryCampaign/Town_Mayor"));
                for(int i=0;i<10;i++)yield return null;
                if(!layout.IsObscured || hud.GetComponent<CanvasGroup>().alpha!=0)throw new Exception("HUD overlaps town conversation");
                talk.CloseConversation();
            } else {
                var menu=UnityEngine.Object.FindObjectsByType<MissionMenuUI>(FindObjectsInactive.Include).First(m=>m.gameObject.scene==SceneManager.GetActiveScene());
                menu.OpenMenu();for(int i=0;i<20;i++)yield return null;
                if(!layout.IsObscured || hud.GetComponent<CanvasGroup>().alpha!=0)throw new Exception("HUD overlaps mission menu");
                var capture=CaptureGameView();File.WriteAllBytes("Logs/QuestHUD/stage1-menu-1024x768.png",capture.EncodeToPNG());UnityEngine.Object.Destroy(capture);
                menu.CloseMenu();
            }
            for(int i=0;i<10;i++)yield return null;
            if(layout.IsObscured || hud.GetComponent<CanvasGroup>().alpha!=1)throw new Exception("HUD did not return after modal closed");
        }
    }
    static Rect ScreenRect(RectTransform transform)
    {
        var canvas=transform.GetComponentInParent<Canvas>().rootCanvas;
        var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
        var corners=new Vector3[4];transform.GetWorldCorners(corners);
        var min=RectTransformUtility.WorldToScreenPoint(camera,corners[0]);
        var max=RectTransformUtility.WorldToScreenPoint(camera,corners[2]);
        return Rect.MinMaxRect(min.x,min.y,max.x,max.y);
    }
    static void CheckBounds(RectTransform panel,Vector2Int size)
    {
        var rect=ScreenRect(panel);
        if(rect.xMin<0 || rect.yMin<0 || rect.xMax>size.x+1 || rect.yMax>size.y+1)
            throw new Exception("HUD outside screen "+size+" "+rect);
    }
    static void CheckButtons(RectTransform panel)
    {
        var rect=ScreenRect(panel);
        foreach(var button in UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Exclude)) {
            if(button.GetComponentInParent<Canvas>()==null || button.GetComponentsInParent<CanvasGroup>().Any(g=>g.alpha<.01f))continue;
            if(ScreenRect((RectTransform)button.transform).Overlaps(rect))
                throw new Exception("HUD overlaps button "+button.name+" "+rect);
        }
    }
    static void CleanupSizes()
    {
        var assembly=typeof(Editor).Assembly;
        var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static;
        var type=assembly.GetType("UnityEditor.GameViewSizes");
        var sizes=type.BaseType.GetProperty("instance",flags).GetValue(null);
        var group=type.GetMethod("GetGroup",flags).Invoke(sizes,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
        var groupType=group.GetType();
        int builtins=(int)groupType.GetMethod("GetBuiltinCount",flags).Invoke(group,null);
        int total=(int)groupType.GetMethod("GetTotalCount",flags).Invoke(group,null);
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",flags).SetValue(view,0);
        for(int i=total-1;i>=builtins;i--){
            var size=groupType.GetMethod("GetGameViewSize",flags).Invoke(group,new object[]{i});
            string label=(string)size.GetType().GetProperty("baseText",flags).GetValue(size);
            if(label.StartsWith("HUD test "))groupType.GetMethod("RemoveCustomSize",flags).Invoke(group,new object[]{i-builtins});
        }
    }
    static void SetResolution(int width,int height)
    {
        var assembly=typeof(Editor).Assembly;
        var sizesType=assembly.GetType("UnityEditor.GameViewSizes");
        var flags=System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.Static;
        var sizes=sizesType.BaseType.GetProperty("instance",flags).GetValue(null);
        var group=sizesType.GetMethod("GetGroup",flags).Invoke(sizes,new[]{Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeGroupType"),"Standalone")});
        var sizeType=assembly.GetType("UnityEditor.GameViewSize");
        var mode=Enum.Parse(assembly.GetType("UnityEditor.GameViewSizeType"),"FixedResolution");
        var size=Activator.CreateInstance(sizeType,flags,null,new object[]{mode,width,height,"HUD test "+width+"x"+height},null);
        group.GetType().GetMethod("AddCustomSize",flags).Invoke(group,new[]{size});
        int count=(int)group.GetType().GetMethod("GetTotalCount",flags).Invoke(group,null);
        var view=EditorWindow.GetWindow(assembly.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",flags).SetValue(view,count-1);
        view.Repaint();
    }    static Texture2D CaptureGameView()
    {
        var view=EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView"));
        RenderTexture target=null;
        for(var type=view.GetType();type!=null;type=type.BaseType) {
            foreach(var field in type.GetFields(System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.DeclaredOnly))
                if(typeof(RenderTexture).IsAssignableFrom(field.FieldType)) {var value=field.GetValue(view) as RenderTexture;if(value!=null){target=value;Debug.Log("VIEW TEXTURE "+field.Name+" "+value.width+"x"+value.height);}}
        }
        if(target==null)throw new Exception("GameView render texture unavailable");
        var previous=RenderTexture.active;RenderTexture.active=target;
        var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);
        image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();RenderTexture.active=previous; if(SystemInfo.graphicsUVStartsAtTop){var pixels=image.GetPixels32();var row=new Color32[target.width];for(int y=0;y<target.height/2;y++){int a=y*target.width,b=(target.height-1-y)*target.width;Array.Copy(pixels,a,row,0,target.width);Array.Copy(pixels,b,pixels,a,target.width);Array.Copy(row,0,pixels,b,target.width);}image.SetPixels32(pixels);image.Apply();}
        return image;
    }}



