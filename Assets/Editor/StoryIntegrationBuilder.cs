using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static partial class StoryIntegrationBuilder
{
    public static readonly string[] Scenes={"Assets/Scenes/Town_Main.unity","Assets/Scenes/stage/stage1.unity","Assets/Scenes/Doukutu_2.unity","Assets/Scenes/Doukutu_3.unity","Assets/Scenes/Doukutu_4.unity"};
    public static void Inspect()
    {
        foreach(string path in Scenes) {
            var scene=EditorSceneManager.OpenScene(path);
            var roots=scene.GetRootGameObjects();
            var players=roots.SelectMany(r=>r.GetComponentsInChildren<PlayerMove>(true)).ToArray();
            Debug.Log("STORY INSPECT "+path+" player="+string.Join(",",players.Select(p=>p.name+"@"+p.transform.position))+
                " missions="+roots.SelectMany(r=>r.GetComponentsInChildren<MissionManager2D>(true)).Count()+
                " dialogue="+roots.SelectMany(r=>r.GetComponentsInChildren<DialoguePresenter2D>(true)).Count()+
                " elevator="+string.Join(",",roots.SelectMany(r=>r.GetComponentsInChildren<CaveExitElevator2D>(true)).Select(e=>e.name+"@"+e.transform.position)));
            foreach(var p in roots.SelectMany(r=>r.GetComponentsInChildren<StageExpeditionSpawnPoint>(true))) Debug.Log("STORY SPAWN "+p.name+"@"+p.transform.position);
            foreach(var p in roots.SelectMany(r=>r.GetComponentsInChildren<FactionMember2D>(true)).Take(12)) Debug.Log("STORY NPC "+p.name+" "+p.FactionId+"@"+p.transform.position);
        }
    }
}
