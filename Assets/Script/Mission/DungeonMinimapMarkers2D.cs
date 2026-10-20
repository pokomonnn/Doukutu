using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Overlay markers refresh independently of the cached terrain and player movement.</summary>
[DefaultExecutionOrder(115)]
public sealed class DungeonMinimapMarkers2D : MonoBehaviour
{
    DungeonMinimap2D map;
    MissionManager2D manager;
    TMP_Text title;
    readonly List<TMP_Text> pool = new List<TMP_Text>();
    readonly List<Transform> npcs = new List<Transform>();
    readonly List<Transform> targets = new List<Transform>();
    StoryInteractionPoint[] points;
    CaveExitElevator2D[] exits;
    float nextScan, nextUpdate;
    int used;
    public IReadOnlyList<Transform> ObjectiveTargets => targets;
    public int VisibleMarkerCount => used;
    void Start()
    {
        map = GetComponent<DungeonMinimap2D>();
        title = map.Frame.Find("Title").GetComponent<TMP_Text>();
    }
    void LateUpdate()
    {
        if (map == null || map.Player == null || Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + .2f;
        Refresh();
    }
    public void Refresh()
    {
        if (map == null || map.Player == null) return;
        if (points == null || Time.unscaledTime >= nextScan) {
            nextScan = Time.unscaledTime + 1;
            points = FindObjectsByType<StoryInteractionPoint>(FindObjectsInactive.Exclude)
                .Where(p => p.gameObject.scene == gameObject.scene).ToArray();
            exits = FindObjectsByType<CaveExitElevator2D>(FindObjectsInactive.Exclude)
                .Where(e => e.gameObject.scene == gameObject.scene).ToArray();
            npcs.Clear();
            foreach (var p in points)
                if (p.stepId.StartsWith("hub_") || p.stepId == "finn" || p.stepId == "garden") npcs.Add(p.transform);
            AddNpcs(FindObjectsByType<RescuePersonTarget2D>(FindObjectsInactive.Exclude));
            AddNpcs(FindObjectsByType<CaveConversationNPC>(FindObjectsInactive.Exclude));
            AddNpcs(FindObjectsByType<CaveOldManSpeechBubble2D>(FindObjectsInactive.Exclude));
            manager = StoryCampaignRuntime.FindMissionManager();
        }
        used = 0;
        foreach (var npc in npcs)
            if (npc != null && npc.gameObject.activeInHierarchy && !(npc.GetComponent<CharacterHealth>()?.IsDead ?? false))
                Draw(npc, "●", new Color(.5f,.75f,1), false);
        foreach (var exit in exits)
            if (exit != null && exit.isActiveAndEnabled) Draw(exit.transform, "■", new Color(.58f,1,.63f), true);
        MissionMinimapDestination2D.Resolve(manager, gameObject.scene, map.Player.position, points, exits, targets, out string caption);
        foreach (var target in targets) Draw(target, "◇", new Color(1,.82f,.36f), true);
        title.text = string.IsNullOrEmpty(caption) ? "ミニマップ" : caption;
        title.enableAutoSizing = true; title.fontSizeMin = 13; title.fontSizeMax = 20;
        for (int i = used; i < pool.Count; i++) pool[i].gameObject.SetActive(false);
    }
    void AddNpcs<T>(T[] found) where T : Component
    {
        foreach (var item in found)
            if (item.gameObject.scene == gameObject.scene && !npcs.Contains(item.transform)) npcs.Add(item.transform);
    }
    void Draw(Transform target, string symbol, Color color, bool showOutside)
    {
        Vector2 uv = map.WorldToMap(target.position);
        bool outside = uv.x < .04f || uv.x > .96f || uv.y < .07f || uv.y > .93f;
        if (outside && !showOutside) return;
        if (outside) {
            var direction = uv - Vector2.one * .5f;
            symbol = Mathf.Abs(direction.x) > Mathf.Abs(direction.y)
                ? (direction.x > 0 ? "→" : "←") : (direction.y > 0 ? "↑" : "↓");
            float ratio = Mathf.Max(Mathf.Abs(direction.x)/.46f, Mathf.Abs(direction.y)/.43f);
            uv = Vector2.one * .5f + direction / ratio;
        }
        if (used == pool.Count) {
            var go = new GameObject("MapMarker", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(map.TerrainRect, false);
            var text = go.GetComponent<TextMeshProUGUI>(); text.font = map.font;
            text.fontSize = 23; text.alignment = TextAlignmentOptions.Center; text.raycastTarget = false;
            text.enableWordWrapping = false; TextOnlyUIStyle2D.Apply(text);
            pool.Add(text);
        }
        var marker = pool[used++]; marker.gameObject.SetActive(true);
        marker.text = symbol; marker.color = color;
        marker.rectTransform.anchorMin = marker.rectTransform.anchorMax = uv;
        marker.rectTransform.pivot = Vector2.one * .5f;
        marker.rectTransform.anchoredPosition = Vector2.zero; marker.rectTransform.sizeDelta = new Vector2(26,26);
    }
}
