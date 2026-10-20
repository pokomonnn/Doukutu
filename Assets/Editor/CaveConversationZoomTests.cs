#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class CaveConversationZoomTests
{
    const string Key = "CaveZoom.Test";
    static IEnumerator flow;
    static int frame = -1;
    static double started;
    static CaveConversationZoomTests()
    {
        if (!SessionState.GetBool(Key, false)) return;
        started = EditorApplication.timeSinceStartup;
        EditorApplication.update += Tick;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Isolate()
    {
        if (SessionState.GetBool(Key, false)) SaveManager.EditorTestSaveFolder = Path.GetFullPath("Logs/CaveZoomSaves");
    }
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/stage/stage1.unity");
        SessionState.SetBool(Key, true);
        EditorApplication.EnterPlaymode();
    }
    static void Check(bool ok, string message) { if (!ok) throw new Exception("CAVE ZOOM: " + message); }
    static void Tick()
    {
        try
        {
            if (EditorApplication.timeSinceStartup - started > 120) throw new Exception("Cave zoom timeout");
            if (!EditorApplication.isPlaying || frame == Time.frameCount) return;
            frame = Time.frameCount;
            if (flow == null) flow = Flow();
            if (flow.MoveNext()) return;
            Time.timeScale = 1;
            SessionState.SetBool(Key, false);
            Debug.Log("CAVE ZOOM PASS: real story and legacy NPC dialogue, smooth zoom, close restore, disable restore, unscaled time, no cumulative zoom");
            EditorApplication.Exit(0);
        }
        catch (Exception e) { Time.timeScale = 1; SessionState.SetBool(Key, false); Debug.LogException(e); EditorApplication.Exit(1); }
    }
    static IEnumerator Flow()
    {
        for (int i = 0; i < 70; i++) yield return null;
        foreach (var enemy in UnityEngine.Object.FindObjectsByType<EnemyChaser2D>(FindObjectsInactive.Include)) enemy.enabled = false;
        var camera = Camera.main;
        var cm = camera.GetComponent<CinemachineBrain>().ActiveVirtualCamera as CinemachineCamera;
        Check(cm != null, "live Cinemachine camera");
        float original = cm.Lens.OrthographicSize;
        var player = UnityEngine.Object.FindFirstObjectByType<PlayerMove>();
        var actor = UnityEngine.Object.FindObjectsByType<StoryInteractionPoint>(FindObjectsInactive.Exclude)
            .First(p => p.stepId == "hub_1_Baldo").GetComponent<DialogueActor2D>();
        actor.transform.position = player.transform.position + Vector3.right;
        var presenter = DialoguePresenter2D.Active;
        Time.timeScale = 1;
        for (int n = 0; n < 2; n++)
        {
            Check(actor.TryStartDialogue(), "story dialogue starts");
            float end = Time.realtimeSinceStartup + .55f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Check(Mathf.Abs(cm.Lens.OrthographicSize - original * .67f) < .01f, "zoom size");
            Check(Mathf.Abs(camera.orthographicSize - cm.Lens.OrthographicSize) < .02f,
                "output zoom " + camera.orthographicSize + " lens " + cm.Lens.OrthographicSize);
            presenter.EndDialogue();
            end = Time.realtimeSinceStartup + .55f;
            while (Time.realtimeSinceStartup < end) yield return null;
            Check(Mathf.Abs(cm.Lens.OrthographicSize - original) < .01f, "restore size");
        }
        Check(actor.TryStartDialogue(), "start before disable");
        Time.timeScale = 0;
        for (int i = 0; i < 5; i++) yield return null;
        var zoom = camera.GetComponent<CaveConversationZoom2D>();
        zoom.enabled = false;
        Check(Mathf.Abs(cm.Lens.OrthographicSize - original) < .01f, "disable restores");
        presenter.EndDialogue();
        Time.timeScale = 1;
        zoom.enabled = true;
        var npc = UnityEngine.Object.FindObjectsByType<CaveConversationNPC>(FindObjectsInactive.Exclude)
            .First(n => n.ConversationData != null);
        typeof(CaveConversationNPC).GetField("requirePlayerInTrigger", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(npc, false);
        npc.OpenConversation();
        float finish = Time.realtimeSinceStartup + .55f;
        while (Time.realtimeSinceStartup < finish) yield return null;
        Check(cm.Lens.OrthographicSize < original - .1f, "legacy NPC zoom");
        UnityEngine.Object.FindFirstObjectByType<TownConversationController>(FindObjectsInactive.Include).CloseConversation();
        finish = Time.realtimeSinceStartup + .55f;
        while (Time.realtimeSinceStartup < finish) yield return null;
        Check(Mathf.Abs(cm.Lens.OrthographicSize - original) < .01f, "legacy close restore");
    }
}
#endif
