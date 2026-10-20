using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Player-centred ground map. No second world camera, lights or gameplay objects are duplicated.</summary>
[DefaultExecutionOrder(110)]
public sealed class DungeonMinimap2D : MonoBehaviour
{
    public LayerMask groundLayers = 1 << 6;
    [Min(.2f)] public float cellSize = .75f;
    public Vector2 displaySize = new Vector2(320, 228);
    public float topInset = 42;
    public TMP_FontAsset font;
    const int Columns = 64, Rows = 40;
    readonly Dictionary<Vector2Int, bool> terrain = new Dictionary<Vector2Int, bool>();
    readonly Color32[] pixels = new Color32[Columns * Rows];
    readonly Collider2D[] hits = new Collider2D[1];
    Texture2D texture;
    RectTransform frame, mapRect;
    Transform player;
    Canvas canvas;
    MissionHUDSafeArea2D hudLayout;
    Vector2Int lastCell = new Vector2Int(int.MinValue, int.MinValue);
    float nextUpdate;
    public RectTransform Frame => frame;
    public RectTransform TerrainRect => mapRect;
    public Transform Player => player;
    public Vector2 WorldToMap(Vector3 world)
    {
        return new Vector2(((world.x / cellSize) - lastCell.x + Columns / 2f) / Columns,
            ((world.y / cellSize) - lastCell.y + Rows / 2f) / Rows);
    }
    public int CachedCellCount => terrain.Count;
    public float ReservedTop => topInset + displaySize.y + 18;

    void Awake()
    {
        canvas = GetComponent<Canvas>();
        hudLayout = GetComponent<MissionHUDSafeArea2D>();
        frame = Rect("Minimap", transform);
        var background = frame.gameObject.AddComponent<Image>();
        background.color = new Color(.025f, .035f, .05f, .88f); background.raycastTarget = false;
        mapRect = Rect("Terrain", frame);
        mapRect.anchorMin = Vector2.zero; mapRect.anchorMax = Vector2.one;
        mapRect.offsetMin = new Vector2(8, 47); mapRect.offsetMax = new Vector2(-8, -32);
        texture = new Texture2D(Columns, Rows, TextureFormat.RGBA32, false) {
            name = "Runtime minimap terrain", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp
        };
        var image = mapRect.gameObject.AddComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
        Text("Title", "ミニマップ", new Vector2(8, -4), new Vector2(300, 26), new Vector2(0, 1));
        Text("Legend", "<color=#5DFFE0>◆現在地</color> <color=#7FBFFF>●NPC</color>\n<color=#96FFA0>■帰還口</color> <color=#FFD15C>◇目標</color>",
            new Vector2(8, 3), new Vector2(304, 43), Vector2.zero);
        frame.Find("Legend").GetComponent<TMP_Text>().fontSize = 17;
        gameObject.AddComponent<DungeonMinimapMarkers2D>();
        Layout();
    }
    static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false); return rect;
    }
    void Text(string name, string value, Vector2 position, Vector2 size, Vector2 anchor)
    {
        var rect = Rect(name, frame); rect.anchorMin = rect.anchorMax = anchor; rect.pivot = anchor;
        rect.anchoredPosition = position; rect.sizeDelta = size;
        var text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        text.font = font; text.text = value; text.fontSize = 20; text.raycastTarget = false;
        text.color = name == "Legend" ? new Color(.4f, .95f, .9f) : new Color(.9f, .87f, .75f);
    }
    void Layout()
    {
        if (frame == null || Screen.width <= 0 || Screen.height <= 0) return;
        Rect safe = Screen.safeArea;
        frame.anchorMin = frame.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
        frame.pivot = Vector2.one; frame.sizeDelta = displaySize;
        frame.anchoredPosition = new Vector2(-20, -topInset);
    }
    void LateUpdate()
    {
        Layout();
        if (hudLayout != null && hudLayout.IsObscured) return;
        if (Time.unscaledTime < nextUpdate) return;
        nextUpdate = Time.unscaledTime + .2f;
        if (player == null) {
            foreach (var candidate in FindObjectsByType<PlayerMove>(FindObjectsInactive.Exclude))
                if (candidate.gameObject.scene == gameObject.scene) { player = candidate.transform; break; }
        }
        if (player == null) return;
        var centre = new Vector2Int(Mathf.FloorToInt(player.position.x / cellSize), Mathf.FloorToInt(player.position.y / cellSize));
        if (centre == lastCell) return;
        lastCell = centre;
        if (terrain.Count > 65536) terrain.Clear();
        var filter = new ContactFilter2D { useLayerMask = true, layerMask = groundLayers, useTriggers = false };
        for (int y = 0; y < Rows; y++) for (int x = 0; x < Columns; x++) {
            var cell = centre + new Vector2Int(x - Columns / 2, y - Rows / 2);
            if (!terrain.TryGetValue(cell, out bool solid)) {
                Vector2 position = ((Vector2)cell + Vector2.one * .5f) * cellSize;
                solid = Physics2D.OverlapBox(position, Vector2.one * cellSize * .9f, 0, filter, hits) > 0;
                terrain[cell] = solid;
            }
            pixels[y * Columns + x] = solid ? new Color32(119, 130, 144, 255) : new Color32(18, 24, 32, 255);
        }
        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
            if (Mathf.Abs(dx) + Mathf.Abs(dy) <= 1)
                pixels[(Rows / 2 + dy) * Columns + Columns / 2 + dx] = new Color32(93, 255, 224, 255);
        texture.SetPixels32(pixels); texture.Apply(false, false);
    }
    // Call after runtime terrain edits. Static terrain is otherwise cached for the lifetime of the scene.
    public void InvalidateTerrain() { terrain.Clear(); lastCell = new Vector2Int(int.MinValue, int.MinValue); }
    void OnDestroy() { if (texture != null) Destroy(texture); }
}
