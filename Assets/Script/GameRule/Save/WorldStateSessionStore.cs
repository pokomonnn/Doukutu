using System;
using System.Collections.Generic;
using UnityEngine;

public static class WorldStateSessionStore
{
    private static SavedWorldStateData data = new SavedWorldStateData();

    public static bool HasAnyData => data != null && data.HasAnyData;

    public static void SetData(SavedWorldStateData source)
    {
        data = Clone(source) ?? new SavedWorldStateData();
    }

    public static SavedWorldStateData CreateSnapshot()
    {
        return Clone(data) ?? new SavedWorldStateData();
    }

    public static bool HasSceneData(string sceneName)
    {
        string target = Normalize(sceneName);
        if (string.IsNullOrEmpty(target) || data == null) return false;

        if (data.CapturedSceneNames != null)
        {
            foreach (string capturedScene in data.CapturedSceneNames)
            {
                if (string.Equals(capturedScene, target, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        if (data.WorldItems?.items != null)
        {
            foreach (WorldItemSaveData item in data.WorldItems.items)
            {
                if (item != null && string.Equals(item.sceneName, target, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        if (data.ItemBoxes != null)
        {
            foreach (SavedItemBoxData box in data.ItemBoxes)
            {
                if (box != null && string.Equals(box.SceneName, target, StringComparison.OrdinalIgnoreCase)) return true;
            }
        }

        return false;
    }

    public static void ReplaceSceneWorldItems(string sceneName, WorldItemSaveCollection collection)
    {
        EnsureData();
        string target = Normalize(sceneName);
        MarkSceneCaptured(target);
        data.WorldItems.items.RemoveAll(item => item != null && string.Equals(item.sceneName, target, StringComparison.OrdinalIgnoreCase));

        if (collection?.items == null) return;
        foreach (WorldItemSaveData item in collection.items)
        {
            if (item == null) continue;
            item.sceneName = target;
            data.WorldItems.items.Add(CloneItem(item));
        }
    }

    public static void ReplaceSceneItemBoxes(string sceneName, List<SavedItemBoxData> boxes)
    {
        EnsureData();
        string target = Normalize(sceneName);
        MarkSceneCaptured(target);
        data.ItemBoxes.RemoveAll(box => box != null && string.Equals(box.SceneName, target, StringComparison.OrdinalIgnoreCase));

        if (boxes == null) return;
        foreach (SavedItemBoxData box in boxes)
        {
            if (box == null) continue;
            SavedItemBoxData copy = CloneBox(box);
            copy.SceneName = target;
            data.ItemBoxes.Add(copy);
        }
    }

    public static SavedWorldStateData CreateSceneSnapshot(string sceneName)
    {
        string target = Normalize(sceneName);
        SavedWorldStateData result = new SavedWorldStateData();

        if (data?.WorldItems?.items != null)
        {
            foreach (WorldItemSaveData item in data.WorldItems.items)
            {
                if (item != null && string.Equals(item.sceneName, target, StringComparison.OrdinalIgnoreCase)) result.WorldItems.items.Add(CloneItem(item));
            }
        }

        if (data?.ItemBoxes != null)
        {
            foreach (SavedItemBoxData box in data.ItemBoxes)
            {
                if (box != null && string.Equals(box.SceneName, target, StringComparison.OrdinalIgnoreCase)) result.ItemBoxes.Add(CloneBox(box));
            }
        }

        return result;
    }

    public static void Clear() => data = new SavedWorldStateData();

    private static void EnsureData()
    {
        data ??= new SavedWorldStateData();
        data.CapturedSceneNames ??= new List<string>();
        data.WorldItems ??= new WorldItemSaveCollection();
        data.WorldItems.items ??= new List<WorldItemSaveData>();
        data.ItemBoxes ??= new List<SavedItemBoxData>();
    }

    private static void MarkSceneCaptured(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        EnsureData();
        foreach (string existing in data.CapturedSceneNames)
        {
            if (string.Equals(existing, sceneName, StringComparison.OrdinalIgnoreCase)) return;
        }
        data.CapturedSceneNames.Add(sceneName);
    }

    private static string Normalize(string value) => value?.Trim() ?? string.Empty;

    private static SavedWorldStateData Clone(SavedWorldStateData source)
    {
        if (source == null) return new SavedWorldStateData();
        return JsonUtility.FromJson<SavedWorldStateData>(JsonUtility.ToJson(source));
    }

    private static WorldItemSaveData CloneItem(WorldItemSaveData item)
    {
        return JsonUtility.FromJson<WorldItemSaveData>(JsonUtility.ToJson(item));
    }

    private static SavedItemBoxData CloneBox(SavedItemBoxData box)
    {
        return JsonUtility.FromJson<SavedItemBoxData>(JsonUtility.ToJson(box));
    }
}
