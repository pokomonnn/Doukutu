#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Text;
using UnityEditor;
using UnityEngine;

// Patched v2.1 installer for projects already using Irregular Item Shapes v1.
// This installer deliberately does not replace inventory layout or existing .meta files.
public static class InventoryShapeV2Installer
{
    [MenuItem("Tools/Inventory/Upgrade Shape Preview and 4-Way Rotation")]
    public static void Install()
    {
        string[] names = {
            "ItemData.cs", "InventoryItem.cs", "InventoryGrid.cs",
            "InventoryController.cs", "ItemBoxInventory.cs", "InventoryGridUI.cs",
            "InventoryItemUI.cs", "InventoryDragPlacementFeedback.cs", "EquipmentSlotUI.cs"
        };
        var originals = new Dictionary<string, string>();
        var updates = new Dictionary<string, string>();
        try
        {
            foreach (string name in names)
            {
                string path = FindUnique(name);
                string original = File.ReadAllText(path);
                string normalized = original.Replace("\r\n", "\n");
                if (normalized.Contains("// IrregularShapesV2 "))
                    throw new InvalidOperationException(name + " は既にv2導入済みです。二度実行しないでください。");
                string updated = Patch(name, normalized);
                if (updated == normalized) throw new InvalidOperationException(name + " に変更がありません。");
                originals.Add(path, original);
                updates.Add(path, original.Contains("\r\n") ? updated.Replace("\n", "\r\n") : updated);
            }
            if (!EditorUtility.DisplayDialog("Item Shape v2", "白/緑/赤の案内を使用マスだけに限定し、4方向回転と回転演出を追加します。\n9つのスクリプトのバックアップ後に更新します。\nシーン構成・7×10マスは変更しません。", "バックアップして更新", "中止")) return;

            string root = Directory.GetParent(Application.dataPath).FullName;
            string backup = Path.Combine(root, "Library", "IrregularShapeV2Backups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
            foreach (var kv in originals)
            {
                string relative = kv.Key.Substring(Application.dataPath.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                string outPath = Path.Combine(backup, relative);
                Directory.CreateDirectory(Path.GetDirectoryName(outPath));
                File.WriteAllText(outPath, kv.Value, new UTF8Encoding(false));
            }
            var written = new List<string>();
            try
            {
                foreach (var kv in updates)
                {
                    File.WriteAllText(kv.Key, kv.Value, new UTF8Encoding(false));
                    written.Add(kv.Key);
                }
            }
            catch
            {
                foreach (string path in written) File.WriteAllText(path, originals[path], new UTF8Encoding(false));
                throw;
            }
            AssetDatabase.Refresh();
            Debug.Log("[IrregularShapesV2] Updated 9 scripts. Backup: " + backup);
            EditorUtility.DisplayDialog("更新完了", "回転はRキーで 0→90→180→270→0。\n緑/赤の配置案内も占有セルのみです。\nバックアップ: " + backup, "OK");
        }
        catch (Exception ex)
        {
            Debug.LogError("[IrregularShapesV2] " + ex);
            EditorUtility.DisplayDialog("安全のため更新を中止しました", "前回の不規則形状v1が未適用、またはコードが異なる可能性があります。\n対象ファイルは変更していません（書き込み途中の失敗時は復元を試みます）。\n\n" + ex.Message, "OK");
        }
    }

    private static string FindUnique(string name)
    {
        string[] paths = Directory.GetFiles(Application.dataPath, name, SearchOption.AllDirectories)
            .Where(p => Path.GetFileName(p) == name).ToArray();
        if (paths.Length != 1) throw new InvalidOperationException(name + " が " + paths.Length + " 個あります。Assets内に1つ必要です。");
        return paths[0];
    }

    private static string Once(string text, string oldValue, string replacement, string label)
    {
        int first = text.IndexOf(oldValue, StringComparison.Ordinal);
        if (first < 0 || text.IndexOf(oldValue, first + oldValue.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("コード位置が見つからない/複数存在: " + label);
        return text.Substring(0, first) + replacement + text.Substring(first + oldValue.Length);
    }

    private static string Method(string text, string signature, string replacement)
    {
        int start = text.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0 || text.IndexOf(signature, start + signature.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("対象メソッドが見つからない/重複: " + signature);
        int open = text.IndexOf('{', start + signature.Length);
        if (open < 0) throw new InvalidOperationException(signature + " の開始がありません。");
        int depth = 0, end = -1;
        for (int i = open; i < text.Length; i++)
        {
            if (text[i] == '{') ++depth;
            else if (text[i] == '}' && --depth == 0) { end = i + 1; break; }
        }
        if (end < 0) throw new InvalidOperationException(signature + " の終端がありません。");
        return text.Substring(0, start) + replacement.Trim('\n', '\r') + text.Substring(end);
    }

    // Replace inside a specific method; whitespace in a user's UI code may differ.
    private static string InMethod(string source, string signature, Func<string, string> apply, string label)
    {
        int start = source.IndexOf(signature, StringComparison.Ordinal);
        if (start < 0 || source.IndexOf(signature, start + signature.Length, StringComparison.Ordinal) >= 0)
            throw new InvalidOperationException("対象メソッドが見つからない/複数存在: " + label);
        int open = source.IndexOf('{', start + signature.Length);
        if (open < 0) throw new InvalidOperationException("開始位置がありません: " + label);
        int depth = 0, end = -1;
        for (int i = open; i < source.Length; ++i)
        {
            if (source[i] == '{') ++depth;
            else if (source[i] == '}' && --depth == 0) { end = i + 1; break; }
        }
        if (end < 0) throw new InvalidOperationException("終了位置がありません: " + label);
        string original = source.Substring(start, end - start);
        string result = apply(original);
        if (result == original)
            throw new InvalidOperationException("置き換え対象なし: " + label + "。現在の InventoryItemUI.cs が必要です。");
        return source.Substring(0, start) + result + source.Substring(end);
    }

    private static string Patch(string name, string text)
    {
        switch (name)
        {
            case "ItemData.cs": return PatchItemData(text);
            case "InventoryItem.cs": return PatchInventoryItem(text);
            case "InventoryGrid.cs": return PatchGrid(text);
            case "InventoryController.cs": return PatchController(text);
            case "ItemBoxInventory.cs": return PatchBox(text);
            case "InventoryGridUI.cs": return PatchGridUI(text);
            case "InventoryItemUI.cs": return PatchItemUI(text);
            case "InventoryDragPlacementFeedback.cs": return PatchFeedback(text);
            case "EquipmentSlotUI.cs": return PatchEquipmentUI(text);
            default: throw new InvalidOperationException(name);
        }
    }

    private static string PatchItemData(string s)
    {
        if (!s.Contains("HasCustomShape") || !s.Contains("GetOccupiedCells(bool isRotated)"))
            throw new InvalidOperationException("ItemData にv1の形状変更機能が見つかりません。");
        s = Method(s,"    public IEnumerable<Vector2Int> GetOccupiedCells(bool isRotated)", @"
    // IrregularShapesV2 ItemData : 4-way rotation, y axis goes downward.
    public IEnumerable<Vector2Int> GetOccupiedCells(bool isRotated)
    {
        return GetOccupiedCells(isRotated ? 1 : 0);
    }

    public IEnumerable<Vector2Int> GetOccupiedCells(int rotationSteps)
    {
        int w = Mathf.Max(1, width);
        int h = Mathf.Max(1, height);
        int steps = canRotate ? ((rotationSteps % 4) + 4) % 4 : 0;
        if (HasCustomShape)
        {
            foreach (Vector2Int cell in customShapeCells)
            {
                if (cell.x < 0 || cell.y < 0 || cell.x >= w || cell.y >= h) continue;
                int x = cell.x, y = cell.y, cw = w, ch = h;
                for (int i = 0; i < steps; i++)
                {
                    int nx = ch - 1 - y;
                    int ny = x;
                    x = nx; y = ny;
                    int tmp = cw; cw = ch; ch = tmp;
                }
                yield return new Vector2Int(x, y);
            }
            yield break;
        }
        Vector2Int size = GetSize(steps);
        for (int y = 0; y < size.y; y++)
            for (int x = 0; x < size.x; x++)
                yield return new Vector2Int(x, y);
    }");
        s = Once(s,"    private void SubscribeLocalizedText()",@"    public Vector2Int GetSize(int rotationSteps)
    {
        int steps = canRotate ? ((rotationSteps % 4) + 4) % 4 : 0;
        return (steps & 1) != 0 ? new Vector2Int(height, width) : new Vector2Int(width, height);
    }

    private void SubscribeLocalizedText()","ItemData GetSize(int)");
        return s;
    }

    private static string PatchInventoryItem(string s)
    {
        s=Once(s,"    [SerializeField] private bool isRotated;", @"    [SerializeField] private bool isRotated;
    // IrregularShapesV2: old items do not have hasFourWayRotation, so use their saved bool.
    [SerializeField, HideInInspector] private int rotationSteps;
    [SerializeField, HideInInspector] private bool hasFourWayRotation;","Item rotation field");
        s=Once(s,"    public bool IsRotated => isRotated;", @"    public int RotationSteps => hasFourWayRotation ? ((rotationSteps % 4) + 4) % 4 : (isRotated ? 1 : 0);
    public bool IsRotated => (RotationSteps & 1) != 0;

    // bool API compatibility. Do not discard a valid 180/270 orientation when its parity is unchanged.
    public int ResolveLegacyRotation(bool legacyRotated) =>
        IsRotated == legacyRotated ? RotationSteps : (legacyRotated ? 1 : 0);

    public void SetRotationSteps(int steps)
    {
        rotationSteps = CanRotate ? ((steps % 4) + 4) % 4 : 0;
        hasFourWayRotation = true;
        isRotated = (rotationSteps & 1) != 0;
    }","Item rotation accessors");
        s=Once(s,"return itemData.GetSize(isRotated);","return itemData.GetSize(RotationSteps);","Item Size");
        s=Method(s,"    public bool TryRotate()",@"
    public bool TryRotate()
    {
        if (!CanRotate) return false;
        SetRotationSteps(RotationSteps + 1);
        return true;
    }");
        return s;
    }

    private static string PatchGrid(string s)
    {
        if (!s.Contains("GetOccupiedCells(finalRotated)"))
            throw new InvalidOperationException("InventoryGrid にv1のカスタム配置判定が見つかりません。");
        s=Method(s,"    public bool CanPlaceItem(\n        InventoryItem item,\n        int targetX,\n        int targetY,\n        bool isRotated)",@"
    public bool CanPlaceItem(InventoryItem item, int targetX, int targetY, bool isRotated)
    {
        return CanPlaceItem(item, targetX, targetY,
            item != null ? item.ResolveLegacyRotation(isRotated) : 0);
    }

    // IrregularShapesV2: 0,1,2,3 = up,right,down,left.
    public bool CanPlaceItem(InventoryItem item, int targetX, int targetY, int rotationSteps)
    {
        EnsureSlotMap();
        if (item == null || item.ItemData == null) return false;
        int steps = item.CanRotate ? ((rotationSteps % 4) + 4) % 4 : 0;
        Vector2Int size = item.ItemData.GetSize(steps);
        if (targetX < 0 || targetY < 0 || targetX + size.x > width || targetY + size.y > height)
            return false;
        foreach (Vector2Int cell in item.ItemData.GetOccupiedCells(steps))
        {
            int x = targetX + cell.x;
            int y = targetY + cell.y;
            if (!IsInsideGrid(x, y)) return false;
            InventoryItem occupied = slotMap[x, y];
            if (occupied != null && occupied != item) return false;
        }
        return true;
    }");
        s=Method(s,"    public bool TryPlaceItem(\n        InventoryItem item,\n        int targetX,\n        int targetY,\n        bool isRotated)", @"
    public bool TryPlaceItem(InventoryItem item, int targetX, int targetY, bool isRotated)
    {
        return TryPlaceItem(item, targetX, targetY,
            item != null ? item.ResolveLegacyRotation(isRotated) : 0);
    }

    public bool TryPlaceItem(InventoryItem item, int targetX, int targetY, int rotationSteps)
    {
        EnsureSlotMap();
        if (item == null || item.ItemData == null) return false;
        int steps = item.CanRotate ? ((rotationSteps % 4) + 4) % 4 : 0;
        if (!CanPlaceItem(item, targetX, targetY, steps)) return false;
        if (!items.Contains(item)) items.Add(item);
        ClearItemFromSlots(item);
        item.SetRotationSteps(steps);
        item.SetGridPosition(targetX, targetY);
        FillItemSlots(item);
        return true;
    }");
        s=s.Replace("            !item.IsRotated\n        );","            (item.RotationSteps + 1) % 4\n        );");
        if (!s.Contains("(item.RotationSteps + 1) % 4"))throw new InvalidOperationException("InventoryGrid TryRotateItem 更新失敗");
        s=Once(s,"CanPlaceItem(item, item.GridX, item.GridY, item.IsRotated)","CanPlaceItem(item, item.GridX, item.GridY, item.RotationSteps)","Grid rebuild");
        s=Once(s,"item.ItemData.GetOccupiedCells(item.IsRotated)","item.ItemData.GetOccupiedCells(item.RotationSteps)","Grid Fill slots");
        return s;
    }

    private static string PatchController(string s)
    {
        s=Once(s,"    public bool CanMoveItemTo(",@"
    // IrregularShapesV2: preserves all 4 orientations through the usual notification path.
    public bool TryMoveItem(InventoryItem item, int targetX, int targetY, int rotationSteps)
    {
        if (item == null) return false;
        bool moved = inventoryGrid.TryPlaceItem(item, targetX, targetY, rotationSteps);
        if (moved) NotifyInventoryChanged();
        return moved;
    }

    public bool CanMoveItemTo(","InventoryController int Move");
        return s;
    }

    private static string PatchBox(string s)
    {
        s=Once(s,"    public bool CanMoveItemTo(",@"
    // IrregularShapesV2: same as standard move, but accepts quarter-turns.
    public bool TryMoveItem(InventoryItem item, int targetX, int targetY, int rotationSteps)
    {
        EnsureInitialized();
        if (item == null) return false;
        bool moved = inventoryGrid.TryPlaceItem(item, targetX, targetY, rotationSteps);
        if (moved) NotifyInventoryChanged();
        return moved;
    }

    public bool CanMoveItemTo(","ItemBoxInventory int Move");
        return s;
    }

    private static string PatchGridUI(string s)
    {
        s=Once(s,"    /// <summary>\n    /// 別のGrid UIへ、同じInventoryItemを移動します。",@"
    // IrregularShapesV2: full four-way orientation for player inventory and boxes.
    public bool TryMoveItem(InventoryItem item, int targetX, int targetY, int rotationSteps)
    {
        if (item == null) return false;
        if (itemBoxInventory != null)
            return itemBoxInventory.TryMoveItem(item, targetX, targetY, rotationSteps);
        return inventoryController != null &&
            inventoryController.TryMoveItem(item, targetX, targetY, rotationSteps);
    }

    public bool TryTransferItemTo(
        InventoryItem item, InventoryGridUI targetGridUI,
        int targetX, int targetY, int rotationSteps)
    {
        if (item == null || targetGridUI == null) return false;
        if (targetGridUI == this)
            return TryMoveItem(item, targetX, targetY, rotationSteps);
        if (!AllowsDirectTransfer || !targetGridUI.AllowsDirectTransfer)
            return false;
        if (!TryGetInventoryGrid(out InventoryGrid sourceGrid) ||
            !targetGridUI.TryGetInventoryGrid(out InventoryGrid targetGrid) ||
            !sourceGrid.ContainsItem(item)) return false;
        int steps = item.CanRotate ? ((rotationSteps % 4) + 4) % 4 : 0;
        if (!targetGrid.CanPlaceItem(item, targetX, targetY, steps)) return false;
        int sourceX = item.GridX, sourceY = item.GridY, sourceSteps = item.RotationSteps;
        if (!RemoveItemFromThisGrid(item)) return false;
        if (targetGridUI.TryMoveItem(item, targetX, targetY, steps)) return true;
        TryMoveItem(item, sourceX, sourceY, sourceSteps);
        return false;
    }

    /// <summary>
    /// 別のGrid UIへ、同じInventoryItemを移動します。","GridUI int transfer");
        return s;
    }

    private static string PatchItemUI(string s)
    {
        if (!s.Contains("UpdateCustomShapeBackground(bool isRotated)"))
            throw new InvalidOperationException("InventoryItemUI にv1のカスタム表示処理が見つかりません。");
        s=Once(s,"using System.Collections.Generic;","using System.Collections;\nusing System.Collections.Generic;","UI using");
        s=Once(s,"    private bool dragIsRotated;", @"    private bool dragIsRotated;
    // IrregularShapesV2: R increments 0 -> 1 -> 2 -> 3 -> 0.
    private int dragRotationSteps;
    [SerializeField, Range(0.06f, 0.5f)] private float rotationAnimationSeconds = 0.15f;
    private Coroutine rotationAnimation;
    private float visualIconAngle;","UI fields");
        // v1 and user-edited UI files may differ in whitespace / argument expression.
        s = InMethod(s, "    public void Refresh()", body =>
        {
            const string pattern = @"\bApplyVisuals\s*\(\s*[^()\r\n;]*\)\s*;";
            MatchCollection matches = Regex.Matches(body, pattern);
            if (matches.Count != 1)
                throw new InvalidOperationException(
                    "UI refresh: Refresh()内のApplyVisualsが" + matches.Count +
                    "か所です。InventoryItemUI.csの最新版を共有してください。");
            return Regex.Replace(body, pattern, "ApplyVisuals(inventoryItem.RotationSteps);");
        }, "UI refresh");
        s=Once(s,"        dragIsRotated = inventoryItem.IsRotated;", @"        dragIsRotated = inventoryItem.IsRotated;
        dragRotationSteps = inventoryItem.RotationSteps;
        if (rotationAnimation != null) { StopCoroutine(rotationAnimation); rotationAnimation = null; }
        visualIconAngle = -90f * dragRotationSteps;","UI begin drag");
        s=Once(s,"        ApplyVisuals(dragIsRotated);\n\n        transform.SetAsLastSibling();","        ApplyVisuals(dragRotationSteps);\n\n        transform.SetAsLastSibling();","UI drag start");
        // There are two (identical) placements in OnEndDrag; patch both at once.
        s = InMethod(s, "    public void OnEndDrag(PointerEventData eventData)", body =>
        {
            int count = Regex.Matches(body, @"\bdragIsRotated\b").Count;
            if (count < 1) throw new InvalidOperationException("UI drop: 回転引数なし");
            return Regex.Replace(body, @"\bdragIsRotated\b", "dragRotationSteps");
        }, "UI drag/drop movement + transfer");
        s=Once(s,"                    dragIsRotated,\n                    out EquipmentResult result", "                    dragRotationSteps,\n                    out EquipmentResult result", "UI equip direction");
        s=Method(s,"    private void TryRotateDuringDrag()", @"
    private void TryRotateDuringDrag()
    {
        if (inventoryItem == null || inventoryItem.ItemData == null) return;
        if (!inventoryItem.CanRotate) { soundPlayer?.PlayFailed(); return; }
        Vector2Int oldSize = GetSize(dragRotationSteps);
        int nextSteps = (dragRotationSteps + 1) & 3;
        // R rotates clockwise: pointer keeps holding the same occupied cell.
        dragCellOffset = new Vector2Int(oldSize.y - 1 - dragCellOffset.y, dragCellOffset.x);
        dragRotationSteps = nextSteps;
        dragIsRotated = (nextSteps & 1) != 0;
        Vector2Int size = GetSize(nextSteps);
        dragCellOffset.x = Mathf.Clamp(dragCellOffset.x, 0, size.x - 1);
        dragCellOffset.y = Mathf.Clamp(dragCellOffset.y, 0, size.y - 1);
        float fromAngle = visualIconAngle;
        ApplyVisuals(nextSteps); // cells snap immediately so placement guidance is accurate.
        if (rotationAnimation != null) StopCoroutine(rotationAnimation);
        rotationAnimation = StartCoroutine(AnimateIconRotation(fromAngle, fromAngle - 90f, nextSteps));
        UpdateDragPosition(Input.mousePosition);
        soundPlayer?.PlayRotate();
    }

    private IEnumerator AnimateIconRotation(float from, float to, int finalSteps)
    {
        float duration = Mathf.Max(0.01f, rotationAnimationSeconds);
        float time = 0f;
        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / duration);
            float eased = t * t * (3f - 2f * t);
            visualIconAngle = Mathf.LerpUnclamped(from, to, eased);
            if (iconImage != null)
            {
                iconImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, visualIconAngle);
                float pop = 1f + 0.09f * Mathf.Sin(t * Mathf.PI);
                iconImage.rectTransform.localScale = new Vector3(pop, pop, 1f);
            }
            yield return null;
        }
        visualIconAngle = -90f * finalSteps;
        if (iconImage != null)
        {
            iconImage.rectTransform.localRotation = Quaternion.Euler(0f, 0f, visualIconAngle);
            iconImage.rectTransform.localScale = Vector3.one;
        }
        rotationAnimation = null;
    }");
        s=Once(s,"        isDragging = false;\n\n        canvasGroup.alpha", @"        isDragging = false;
        if (rotationAnimation != null) { StopCoroutine(rotationAnimation); rotationAnimation = null; }
        if (iconImage != null) iconImage.rectTransform.localScale = Vector3.one;

        canvasGroup.alpha","UI FinishDrag");
        s=Once(s,"    private void ApplyVisuals(bool isRotated)",@"    private Vector2Int GetSize(int rotationSteps) =>
        inventoryItem == null || inventoryItem.ItemData == null
            ? Vector2Int.one : inventoryItem.ItemData.GetSize(rotationSteps);

    private void ApplyVisuals(int rotationSteps)","UI ApplyVisuals signature");
        s=Once(s,"        Vector2Int itemSize = GetSize(isRotated);\n\n        itemRect.sizeDelta", "        Vector2Int itemSize = GetSize(rotationSteps);\n\n        itemRect.sizeDelta", "UI ApplyVisuals size");
        s=Once(s,"        UpdateCustomShapeBackground(isRotated);","        UpdateCustomShapeBackground(rotationSteps);","UI shape visuals call");
        s=Once(s,"            isRotated ? -90f : 0f", "            -90f * rotationSteps", "UI icon angle");
        s=Once(s,"    private void UpdateCustomShapeBackground(bool isRotated)","    private void UpdateCustomShapeBackground(int rotationSteps)","UI shape method");
        s=Once(s,"inventoryItem.ItemData.GetOccupiedCells(isRotated)","inventoryItem.ItemData.GetOccupiedCells(rotationSteps)","UI shape cells");
        return s;
    }

    private static string PatchFeedback(string s)
    {
        s=Once(s,"using System.Reflection;","using System.Reflection;\nusing System.Collections.Generic;","feedback using");
        s=Once(s,"    private Image tintedImage;",@"    // IrregularShapesV2: highlights only occupied ShapeCell images, never the rectangular root.
    private class HighlightState
    {
        public Image Image;
        public Color Color;
        public Outline Outline;
        public bool HadOutline;
        public bool OutlineEnabled;
        public Color OutlineColor;
        public Vector2 OutlineDistance;
    }
    private RectTransform highlightedOwner;
    private readonly List<HighlightState> highlightStates = new List<HighlightState>();
    private readonly List<Image> desiredImages = new List<Image>();

    private Image tintedImage;","feedback highlight fields");
        s=Once(s,"GetPrivateValue(itemUI, \"dragIsRotated\", item.IsRotated)","GetPrivateValue(itemUI, \"dragRotationSteps\", item.RotationSteps)","feedback UI rotation");
        s=Once(s,"GetPrivateValue(handler, \"dragIsRotated\", item.IsRotated)","(GetPrivateValue(handler, \"dragIsRotated\", item.IsRotated) ? 1 : 0)","feedback equipment rotation");
        s=Once(s,"            drag.IsRotated\n        );","            drag.RotationSteps\n        );","feedback place step");
        s=Once(s,"        bool isRotated)\n    {\n        if (targetGridUI", "        int rotationSteps)\n    {\n        if (targetGridUI", "feedback CanPlace signature");
        s=Once(s,"            isRotated\n        );\n    }\n\n    private void ApplyPlacementVisual", "            rotationSteps\n        );\n    }\n\n    private void ApplyPlacementVisual", "feedback can place call");
        s=Once(s,"        public readonly bool IsRotated;", "        public readonly int RotationSteps;", "feedback struct field");
        s=Once(s,"            bool isRotated)\n        {\n            Source = source;", "            int rotationSteps)\n        {\n            Source = source;", "feedback struct ctor");
        s=Once(s,"            IsRotated = isRotated;", "            RotationSteps = rotationSteps;", "feedback struct assignment");
        s=Method(s,"    private void ApplyPlacementVisual(\n        RectTransform itemRect,\n        bool canPlace)",@"
    private void ApplyPlacementVisual(RectTransform itemRect, bool canPlace)
    {
        if (itemRect == null) return;
        desiredImages.Clear();
        var itemUI = itemRect.GetComponent<InventoryItemUI>();
        bool custom = itemUI != null && itemUI.Item != null &&
            itemUI.Item.ItemData != null && itemUI.Item.ItemData.HasCustomShape;
        if (custom)
        {
            Transform root = itemRect.Find(""CustomShapeCells"");
            if (root != null)
            {
                foreach (Image image in root.GetComponentsInChildren<Image>(false))
                    if (image != null && image.isActiveAndEnabled) desiredImages.Add(image);
            }
        }
        else
        {
            Image background = itemRect.GetComponent<Image>();
            if (background != null) desiredImages.Add(background);
        }

        bool changed = highlightedOwner != itemRect || highlightStates.Count != desiredImages.Count;
        if (!changed)
            for (int i = 0; i < desiredImages.Count; i++)
                if (highlightStates[i].Image != desiredImages[i]) { changed = true; break; }
        if (changed)
        {
            RestoreItemVisualOnly();
            highlightedOwner = itemRect;
            foreach (Image image in desiredImages)
            {
                HighlightState state = new HighlightState();
                state.Image = image;
                state.Color = image.color;
                state.Outline = image.GetComponent<Outline>();
                state.HadOutline = state.Outline != null;
                if (state.Outline == null) state.Outline = image.gameObject.AddComponent<Outline>();
                else
                {
                    state.OutlineEnabled = state.Outline.enabled;
                    state.OutlineColor = state.Outline.effectColor;
                    state.OutlineDistance = state.Outline.effectDistance;
                }
                highlightStates.Add(state);
            }
        }
        Color target = canPlace ? validPlacementColor : invalidPlacementColor;
        foreach (HighlightState state in highlightStates)
        {
            if (state.Image == null) continue;
            state.Image.color = Color.Lerp(state.Color, target, tintStrength);
            if (state.Outline != null)
            {
                state.Outline.enabled = outlineThickness > 0f;
                state.Outline.effectColor = target;
                state.Outline.effectDistance = new Vector2(outlineThickness, -outlineThickness);
            }
        }
    }");
        s=Method(s,"    private void RestoreItemVisualOnly()",@"
    private void RestoreItemVisualOnly()
    {
        foreach (HighlightState state in highlightStates)
        {
            if (state.Image != null) state.Image.color = state.Color;
            if (state.Outline != null)
            {
                if (state.HadOutline)
                {
                    state.Outline.enabled = state.OutlineEnabled;
                    state.Outline.effectColor = state.OutlineColor;
                    state.Outline.effectDistance = state.OutlineDistance;
                }
                else
                {
                    Destroy(state.Outline);
                }
            }
        }
        highlightStates.Clear();
        highlightedOwner = null;
    }");
        return s;
    }

    private static string PatchEquipmentUI(string s)
    {
        // Add overload. Legacy bool method retained for any other caller.
        s=Once(s,"    private void RefreshVisual()",@"
    // IrregularShapesV2: retain 180/270 rotations on successful equip.
    public bool TryEquipDroppedItem(InventoryItem item, int desiredRotation, out EquipmentResult result)
    {
        result = EquipmentResult.InvalidItem;
        if (item == null || item.ItemData == null) return false;
        if (!FindEquipmentController()) { result = EquipmentResult.InventoryNotFound; return false; }
        if (!equipmentController.TryGetEquipmentSlot(item.ItemData, out EquipmentSlotType itemSlot))
        { result = EquipmentResult.UnsupportedItem; return false; }
        if (itemSlot != slotType) { result = EquipmentResult.InvalidSlot; return false; }
        int steps = item.CanRotate ? ((desiredRotation % 4) + 4) % 4 : 0;
        Vector2Int size = item.ItemData.GetSize(steps);
        if (size.x > slotWidth || size.y > slotHeight)
        { result = EquipmentResult.InvalidSlot; return false; }
        if (!equipmentController.TryEquipItem(item, out result)) return false;
        item.SetRotationSteps(steps);
        RefreshVisual();
        return true;
    }

    private void RefreshVisual()","EquipmentUI 4-way equip");
        s=Once(s,"equippedItem.IsRotated ? -90f : 0f","-90f * equippedItem.RotationSteps","EquipmentUI stored icon angle");
        return s;
    }
}
#endif
