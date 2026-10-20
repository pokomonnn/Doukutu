using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CampaignPlayTest
{
    const string Key="Campaign.PlayTest";
    static double started;
    static int phase;
    static CampaignPlayTest()
    {
        if(!SessionState.GetBool(Key,false)) return;
        CampaignDirector.ValidationMode=true;
        started=EditorApplication.timeSinceStartup;
        EditorApplication.update+=Tick;
    }
    public static void Run()
    {
        SessionState.SetBool(Key,true);
        EditorSceneManager.OpenScene("Assets/Scenes/Campaign/CampaignStage1.unity");
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool condition,string text) {if(!condition) throw new Exception(text);}
    static void Tick()
    {
        if(!EditorApplication.isPlaying || EditorApplication.timeSinceStartup-started<4) return;
        try {
            var d=CampaignDirector.Current;
            if(phase==0) {
                Check(d!=null && d.actor!=null,"Missing runtime actor/director");
                Check(d.actor.transform.position.y>.5f && d.actor.transform.position.y<2,"Player did not settle on ground");
                var main=d.Data.steps.First(s=>s.id=="diary");
                d.Interact(main);Check(!d.Talking,"Remote investigation allowed");
                d.actor.transform.position=new Vector3(main.x,1,0);
                d.Interact(main);Check(d.Talking,"Interaction failed");
                var next=typeof(CampaignDirector).GetMethod("Next",BindingFlags.NonPublic|BindingFlags.Instance);
                for(int i=0;d.Talking && i<500;i++) next.Invoke(d,null);
                Check(d.Has("S1M01_ClueFound"),"Clue not committed");
                d.Interact(main);Check(!d.Talking,"Duplicate interaction");
                d.stage=0;
                var report=d.Data.steps.First(s=>s.id=="report1");
                int gold=GameSessionManager.Instance.CurrentMoney;
                d.Interact(report);
                for(int i=0;d.Talking && i<500;i++) next.Invoke(d,null);
                Check(GameSessionManager.Instance.CurrentMoney==gold+100,"Report reward missing");
                d.Interact(report);
                Check(!d.Talking && GameSessionManager.Instance.CurrentMoney==gold+100,"Duplicate report reward");
                d.stage=1;
                var snapshot=GameSessionManager.Instance.CreateSaveGameData("CampaignStage1");
                var json=JsonUtility.ToJson(snapshot);
                Check(GameSessionManager.Instance.ApplySaveGameData(JsonUtility.FromJson<SaveGameData>(json),d.itemDatabase,out string error),error);
                Check(d.Has("S1M01_ClueFound"),"Story flags lost on save roundtrip");
                var enemy=UnityEngine.Object.FindFirstObjectByType<CampaignEnemy>();
                Check(enemy!=null,"No combat enemy");enemy.Hit();enemy.Hit();enemy.Hit();
                d.actor.Damage(5);Check(d.actor.health==5 && d.actor.transform.position.x==3,"Death recovery failed");
                d.actor.transform.position=new Vector3(28,1,0);
                phase=1; started=EditorApplication.timeSinceStartup;
            } else if(phase==1) {
                Check(!d.Talking && Time.timeScale==1,"Dialogue did not unpause");
                var camera=d.actor.followCamera;
                var target=new RenderTexture(1280,720,24);
                var old=RenderTexture.active;
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
                image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();
                System.IO.File.WriteAllBytes(System.IO.Path.GetFullPath("Docs/CampaignStage1-preview.png"),image.EncodeToPNG());
                camera.targetTexture=null;RenderTexture.active=old;
                UnityEngine.Object.Destroy(image);UnityEngine.Object.Destroy(target);
                phase=2;started=EditorApplication.timeSinceStartup;
            } else {
                SessionState.SetBool(Key,false);
                Debug.Log("CAMPAIGN PLAYTEST PASS: ground collision, proximity, dialogue, idempotency, save roundtrip, combat, respawn");
                EditorApplication.Exit(0);
            }
        } catch(Exception e) {
            SessionState.SetBool(Key,false);Debug.LogException(e);EditorApplication.Exit(1);
        }
    }
}
