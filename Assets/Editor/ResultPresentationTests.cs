#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

[InitializeOnLoad]
public static class ResultPresentationTests
{
    const string Key="ResultPresentation.Tests";
    static IEnumerator flow;static int frame=-1;static double start;
    static ResultPresentationTests(){if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/Result.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static object Visual(string method,params object[] args)=>typeof(QuestHUDVisualTests).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,args);
    static void Tick(){try{if(EditorApplication.timeSinceStartup-start>180)throw new Exception("Result test timeout");if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;if(flow==null)flow=Flow();if(flow.MoveNext())return;Visual("CleanupSizes");SessionState.SetBool(Key,false);Debug.Log("RESULT PRESENTATION PASS: sequence, return button, 5 resolutions, long scroll lists, distinct reward references");EditorApplication.Exit(0);}catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}}
    static IEnumerator Flow()
    {
        Directory.CreateDirectory("Logs/ResultUI");
        var ui=UnityEngine.Object.FindFirstObjectByType<ExpeditionResultUI>();var seq=ui.GetComponent<ExpeditionResultSequenceAnimator2D>();var so=new SerializedObject(ui);
        var button=(Button)so.FindProperty("returnTownButton").objectReferenceValue;
        var root=button.transform.parent;
        var groups=root.GetComponentsInChildren<CanvasGroup>();
        if(button.interactable)throw new Exception("Return enabled before sequence");
        float deadline=Time.realtimeSinceStartup+8;
        while(!button.interactable && Time.realtimeSinceStartup<deadline)yield return null;
        if(!button.interactable||groups.Any(g=>g.alpha<.99f))throw new Exception("Sequence incomplete");
        var rescue=so.FindProperty("rescueRewardText").objectReferenceValue;var total=so.FindProperty("totalRewardText").objectReferenceValue;
        if(rescue==total||rescue==null||total==null)throw new Exception("Reward fields aliased");
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1920,1080),new Vector2Int(2560,1440),new Vector2Int(2560,1080),new Vector2Int(1024,768)}) {
            Visual("SetResolution",size.x,size.y);for(int i=0;i<30;i++)yield return null;
            foreach(bool populated in new[]{false,true}) {
                ui.RefreshUI();
                var mission=(TMP_Text)so.FindProperty("expeditionMissionListText").objectReferenceValue;
                var treasure=(TMP_Text)so.FindProperty("treasureListText").objectReferenceValue;
                if(populated){mission.text=string.Join("\n",Enumerable.Range(1,16).Select(i=>"達成：旧居住区で取り残された仲間を救出する "+i+"　報酬受取済み"));treasure.text=string.Join("\n",Enumerable.Range(1,10).Select(i=>"古城で見つけた貴重な調査資料 × "+i));}
                // 表示用の集計のみを用意し、所持金・セーブには触れない。
                var reportType=typeof(RescueNpcTreatmentResultSession2D);
                reportType.GetField("associatedResult",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,ExpeditionResultSession.Current);
                reportType.GetField("report",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,populated?new RescueNpcTreatmentResultSession2D.Report{TreatedNpcCount=12,BleedingTreated=3,PoisonTreated=8,FractureTreated=1,TreatedAndRescued=9,TreatedNotRecovered=2,TreatedButDeadRecovered=1,UnstabilizedRescued=1,TreatmentRewardPaid=true,TreatmentRewardPaidAmount=1200}:null);
                UnityEngine.Object.FindFirstObjectByType<RescueNpcTreatmentResultUI2D>().Refresh();
                Canvas.ForceUpdateCanvases();for(int i=0;i<4;i++)yield return null;
                foreach(var t in root.GetComponentsInChildren<TMP_Text>()) {t.ForceMeshUpdate();if(t.isTextOverflowing)throw new Exception("Text overflow: "+t.name+" at "+size);}
                foreach(var s in root.GetComponentsInChildren<ScrollRect>()) {s.verticalNormalizedPosition=1;if(populated&&s.content.childCount>0&&s.content.GetChild(0)==mission.transform&&s.content.rect.height<=s.viewport.rect.height)throw new Exception("Long list did not expand");}
                var image=(Texture2D)Visual("CaptureGameView");if(image.width!=size.x||image.height!=size.y)throw new Exception("Capture size mismatch");File.WriteAllBytes("Logs/ResultUI/"+size.x+"x"+size.y+(populated?"-long":"-empty")+".png",image.EncodeToPNG());UnityEngine.Object.Destroy(image);
            }
        }
    }
}
#endif

