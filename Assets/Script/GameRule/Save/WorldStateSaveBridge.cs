using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-450)]
[DisallowMultipleComponent]
public class WorldStateSaveBridge : MonoBehaviour
{
    [SerializeField] private WorldItemSaveManager worldItemSaveManager;
    [SerializeField] private ItemDataDatabase itemDataDatabase;
    [SerializeField] private bool restoreOnStart = true;
    [SerializeField] private bool captureOnDisable = true;
    [SerializeField] private bool showDebugLogs = true;

    private bool hasStarted;
    private bool isQuitting;
    private bool hasRestoredThisScene;

    private void Awake() => FindReferences();

    private void Start()
    {
        hasStarted = true;
        if (restoreOnStart && !hasRestoredThisScene) ReloadFromSession();
    }

    private void OnDisable()
    {
        if (captureOnDisable && hasStarted && !isQuitting) CaptureToSession();
    }

    private void OnApplicationQuit()
    {
        isQuitting = true;
        CaptureToSession();
    }

    public bool CaptureToSession()
    {
        FindReferences();
        Scene scene = SceneManager.GetActiveScene();
        bool captured = false;

        if (worldItemSaveManager != null)
        {
            WorldStateSessionStore.ReplaceSceneWorldItems(scene.name, worldItemSaveManager.CaptureAllLoadedWorldItems());
            captured = true;
        }

        ItemBoxSaveIdentity[] boxes = FindObjectsByType<ItemBoxSaveIdentity>(FindObjectsInactive.Include);
        List<SavedItemBoxData> savedBoxes = new List<SavedItemBoxData>();
        HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (ItemBoxSaveIdentity box in boxes)
        {
            if (box == null || box.gameObject.scene != scene) continue;
            if (string.IsNullOrWhiteSpace(box.PersistentId))
            {
                Debug.LogWarning($"[WorldStateSaveBridge] ItemBox『{box.name}』のPersistent Idが空です。", box);
                continue;
            }
            if (!ids.Add(box.PersistentId))
            {
                Debug.LogWarning($"[WorldStateSaveBridge] ItemBox Persistent Idが重複しています：{box.PersistentId}", box);
                continue;
            }
            SavedItemBoxData data = box.CreateSaveData();
            if (data != null) savedBoxes.Add(data);
        }

        WorldStateSessionStore.ReplaceSceneItemBoxes(scene.name, savedBoxes);
        if (savedBoxes.Count > 0) captured = true;

        Log($"ワールド状態を保持しました：Scene={scene.name} / Boxes={savedBoxes.Count}");
        return captured;
    }

    public bool ReloadFromSession()
    {
        FindReferences();
        Scene scene = SceneManager.GetActiveScene();
        if (!WorldStateSessionStore.HasSceneData(scene.name))
        {
            Log($"復元対象なし：Scene={scene.name}");
            return false;
        }

        SavedWorldStateData snapshot = WorldStateSessionStore.CreateSceneSnapshot(scene.name);
        if (worldItemSaveManager != null) worldItemSaveManager.RestoreActiveSceneWorldItems(snapshot.WorldItems);

        int restoredBoxes = 0;
        if (snapshot.ItemBoxes != null && itemDataDatabase != null)
        {
            ItemBoxSaveIdentity[] boxes = FindObjectsByType<ItemBoxSaveIdentity>(FindObjectsInactive.Include);
            Dictionary<string, ItemBoxSaveIdentity> lookup = new Dictionary<string, ItemBoxSaveIdentity>(StringComparer.OrdinalIgnoreCase);
            foreach (ItemBoxSaveIdentity box in boxes)
            {
                if (box != null && box.gameObject.scene == scene && !string.IsNullOrWhiteSpace(box.PersistentId)) lookup[box.PersistentId] = box;
            }

            foreach (SavedItemBoxData saved in snapshot.ItemBoxes)
            {
                if (saved != null && lookup.TryGetValue(saved.PersistentId, out ItemBoxSaveIdentity box) && box.RestoreFromSaveData(saved, itemDataDatabase)) restoredBoxes++;
            }
        }

        hasRestoredThisScene = true;
        Log($"ワールド状態を復元しました：Scene={scene.name} / Boxes={restoredBoxes}");
        return true;
    }

    private void FindReferences()
    {
        if (worldItemSaveManager == null) worldItemSaveManager = FindAnyObjectByType<WorldItemSaveManager>(FindObjectsInactive.Include);
        if (itemDataDatabase == null && SaveManager.Instance != null) itemDataDatabase = SaveManager.Instance.ItemDataDatabase;
        if (itemDataDatabase == null)
        {
            ItemDataDatabase[] databases = Resources.FindObjectsOfTypeAll<ItemDataDatabase>();
            if (databases != null && databases.Length == 1) itemDataDatabase = databases[0];
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs) Debug.Log($"[WorldStateSaveBridge] {message}", this);
    }
}
