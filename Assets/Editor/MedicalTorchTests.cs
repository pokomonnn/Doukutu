#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
[InitializeOnLoad]
public static class MedicalTorchTests
{
    const string Key="MedicalTorch.Tests";static IEnumerator flow;static int frame=-1;static double start;
    static MedicalTorchTests(){if(SessionState.GetBool(Key,false)){start=EditorApplication.timeSinceStartup;EditorApplication.update+=Tick;}}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]static void Isolate(){if(SessionState.GetBool(Key,false))SaveManager.EditorTestSaveFolder=Path.GetFullPath("Logs/MedicalTorchSaves");}
    public static void Run(){EditorSceneManager.OpenScene("Assets/Scenes/stage/stage1.unity");SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();}
    static object Visual(string name,params object[] args)=>typeof(QuestHUDVisualTests).GetMethod(name,BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,args);
    static void Check(bool value,string message){if(!value)throw new Exception("MEDICAL TORCH: "+message);}
    static void Tick(){try{if(EditorApplication.timeSinceStartup-start>180)throw new Exception("timeout");if(!EditorApplication.isPlaying||frame==Time.frameCount)return;frame=Time.frameCount;if(flow==null)flow=Flow();if(flow.MoveNext())return;Visual("CleanupSizes");SessionState.SetBool(Key,false);Debug.Log("MEDICAL TORCH PASS: 3 treatments, consumption, saved quantities, half-ring at 3 resolutions");EditorApplication.Exit(0);}catch(Exception e){SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);}}
    static IEnumerator Flow(){
        for(int i=0;i<70;i++)yield return null;
        foreach(var enemy in UnityEngine.Object.FindObjectsByType<EnemyChaser2D>(FindObjectsInactive.Include))enemy.enabled=false;
        var kit=UnityEngine.Object.FindFirstObjectByType<MedicalKitInventory2D>();var items=MedicalKitSetup20261021.Items();var conditions=new[]{StatusConditionType.Poison,StatusConditionType.Fracture,StatusConditionType.Bleeding};
        var npc=UnityEngine.Object.FindObjectsByType<RescuePersonSymptoms2D>(FindObjectsInactive.Exclude).First(n=>!n.IsDead && n.GetComponent<StoryFinnDelivery>()==null);
        var treatment=UnityEngine.Object.FindFirstObjectByType<MedicalKitTreatment2D>(FindObjectsInactive.Include);
        for(int i=0;i<3;i++){
            Check(kit.GetItemAtSlot(i)==items[i],"item registration "+i);Check(kit.GetTreatmentConditions(items[i])==conditions[i],"player treatment "+i);Check(kit.GetNpcTreatmentCondition(items[i])==conditions[i],"NPC treatment "+i);
            kit.TrySetAmount(items[i],3);npc.AddSymptoms(conditions[i]);Check(treatment.TryUse(kit,items[i],npc,out string message),message);Check(kit.GetAmountAtSlot(i)==2&&npc.IsStabilized(conditions[i]),"treatment consumes one "+i);
        }
        var snapshot=MedicalKitSessionStore2D.CreateSnapshot();kit.RestoreSavedAmounts(0,0,0);MedicalKitSessionStore2D.RestoreSnapshot(snapshot);Check(Enumerable.Range(0,3).All(i=>kit.GetAmountAtSlot(i)==2),"quantity restore");
        var torch=UnityEngine.Object.FindFirstObjectByType<TorchController>();var hud=UnityEngine.Object.FindFirstObjectByType<TorchBarUI>();var ring=UnityEngine.Object.FindFirstObjectByType<TorchHalfRingGraphic>();Check(ring!=null,"ring created");
        Directory.CreateDirectory("Logs/MedicalTorchUI");
        foreach(var size in new[]{new Vector2Int(1280,720),new Vector2Int(1024,768),new Vector2Int(1920,1080)}) {
            Visual("SetResolution",size.x,size.y);for(int i=0;i<25;i++)yield return null;
            foreach(float value in new[]{100f,20f,0f}){
                torch.SetTorch(value);hud.RefreshUI();Canvas.ForceUpdateCanvases();yield return null;
                Check(Mathf.Abs(ring.remaining-value/100)<.02,"ring remaining");
                var text=(TMPro.TMP_Text)typeof(TorchBarUI).GetField("torchText",BindingFlags.NonPublic|BindingFlags.Instance).GetValue(hud);Check(text==null||(!text.enabled&&string.IsNullOrEmpty(text.text)),"numeric text hidden");
                var img=(Texture2D)Visual("CaptureGameView");File.WriteAllBytes("Logs/MedicalTorchUI/"+size.x+"x"+size.y+"-"+value+".png",img.EncodeToPNG());UnityEngine.Object.Destroy(img);
            }
        }
        var toggle=UnityEngine.Object.FindFirstObjectByType<InventoryPanelToggle>(FindObjectsInactive.Include);toggle.OpenInventory();for(int i=0;i<5;i++)yield return null;var ui=UnityEngine.Object.FindFirstObjectByType<MedicalKitInventoryUI2D>(FindObjectsInactive.Include);ui.OpenForPlayer();for(int i=0;i<10;i++)yield return null;
        Check(ui.IsMedicalPanelOpen,"medical UI visible");var panel=(Texture2D)Visual("CaptureGameView");File.WriteAllBytes("Logs/MedicalTorchUI/kit.png",panel.EncodeToPNG());UnityEngine.Object.Destroy(panel);
    }
}
#endif


