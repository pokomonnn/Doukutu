using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Editor-only export: source scenes and campaign data are never saved or rebuilt.
public static class StoryPlacementCatalogBuilder
{
    const string Folder = "Assets/Prefabs/StoryPlacement";
    const string ScenePath = "Assets/Scenes/QuestPlacementCatalog.unity";
    static readonly string[] Names = { "Town_受付", "Stage1_日誌とフィンと設計図", "Stage2_昇降路と補給と水路", "Stage3_巡礼路と停戦と交易", "Stage4_庭と結末" };
    static TMP_FontAsset Font => AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/NotoSansJP-VariableFont_wght SDF.asset");

    [MenuItem("Tools/Story/Create placement catalog")]
    public static void Build()
    {
        if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) != null)
            throw new InvalidOperationException("確認シーンは作成済みです。手動配置を保護するため自動上書きしません。");
        try {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            var hashes = StoryIntegrationBuilder.Scenes.Select(Hash).ToArray();
            var db = AssetDatabase.LoadAssetAtPath<StoryCampaignDatabase>("Assets/Resources/StoryCampaign/Database.asset");
            var manifest = new List<string> {
                "クエスト配置見本（本編の配置は変更していません）", "",
                "開くシーン: " + ScenePath,
                "Sceneビューで2Dを選択し、Hierarchyの各区画／配置単位を選択してFで寄ってください。",
                "確認シーンは静的な見本です。セーブへの干渉を避けるため、見本インスタンスの動作スクリプトと物理を停止しています。",
                "本編で使用する場合は Project の " + Folder + " にあるプレハブを使用してください。",
                "Townは既存の町長・執事・後日談ボタン、ダンジョンはEキーの会話／調査地点です。",
                "本編には同じ対象が既にあります。追加で置くと二重になるため、通常は本編の既存対象を移動してください。",
                "プレハブで置換する場合は既存対象を無効化し、MissionManager2DのcompassTargetを新しい対象へ付け替えてください。",
                "TownプレハブにはCanvasを含みます。TownConversationControllerは既存シーンのものを使います。",
                "ダンジョンには既存プレイヤー、会話UI、MissionManager、進行・セーブの仕組みが必要です。",
                "進行処理は本編のシーン名を判定します。確認シーンや別名シーンでクエストは進行しません。",
                "Stage1はフィンとバルドを同じプレハブに保持しています。個別に複製せず子の配置単位を動かしてください。",
                "Stage4には庭の会話切り替え用StoryCampaignSceneを保持しています。",
                "配置単位ごと動かすとラベル・交渉範囲も一緒に動きます。地形に合わせた高さ調整は本編側で必要です。",
                "アイテムは元の会話処理で取得します。下記ItemDataは別の拾得物として重ねて配置しないでください。",
                "救急キットは既存のプレイヤー所持品を使用します。治療所の薬は追加していません。", ""
            };
            var paths = new List<string>();
            for (int stage = 0; stage < Names.Length; stage++) {
                var source = EditorSceneManager.OpenScene(StoryIntegrationBuilder.Scenes[stage]);
                var original = source.GetRootGameObjects().Single(g => g.name == "StoryIntegration");
                var clone = UnityEngine.Object.Instantiate(original);
                clone.name = Names[stage];
                // Remove managers/HUD and obsolete points; keep shared in-root references intact.
                foreach (Transform child in clone.transform.Cast<Transform>().ToArray()) {
                    bool keep = stage == 0 ? child.GetComponent<StoryTownLink>() != null :
                        child.GetComponent<StoryInteractionPoint>() != null ||
                        child.GetComponent<StoryNegotiationZone>() != null ||
                        child.GetComponent<TextMeshPro>() != null;
                    if (!keep || !child.gameObject.activeSelf) UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
                foreach (var component in clone.GetComponents<Component>()) {
                    if (component is Transform) continue;
                    if (stage == 0 && (component is Canvas || component is UnityEngine.UI.CanvasScaler ||
                                      component is UnityEngine.UI.GraphicRaycaster)) continue;
                    if (stage > 0 && (component is StoryCampaignScene || component is StoryConversationGuard)) continue;
                    UnityEngine.Object.DestroyImmediate(component);
                }
                manifest.Add("■ " + Names[stage] + " / 配置先 " + StoryIntegrationBuilder.Scenes[stage]);
                if (stage > 0) Arrange(clone, db, manifest);
                else manifest.Add("町長: S1M01・S2M01・S3M01の受注／報告、S2S02の入町許可。執事: 物語の補足。後日談: 結末後。");
                clone.transform.position = Vector3.zero;
                string path = Folder + "/" + Names[stage] + ".prefab";
                if (AssetDatabase.LoadAssetAtPath<GameObject>(path) != null)
                    throw new InvalidOperationException("既存プレハブを保護しました: " + path);
                PrefabUtility.SaveAsPrefabAsset(clone, path);
                ValidatePrefab(path, stage);
                paths.Add(path);
                manifest.Add("");
            }
            var catalog = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var camera = new GameObject("Overview camera").AddComponent<Camera>();
            camera.orthographic = true; camera.orthographicSize = 58;
            camera.transform.position = new Vector3(35, -44, -100);
            camera.backgroundColor = new Color(.055f, .065f, .09f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            Label(null, "クエスト配置見本 — Sceneビューで区画を選択して F\n本編の配置は保持 / このシーンはプレイ検証用ではありません", new Vector3(35, 11), 120, 20);
            for (int stage = 0; stage < Names.Length; stage++) {
                var section = new GameObject(Names[stage] + "【確認用】");
                section.transform.position = new Vector3(stage == 2 || stage == 4 ? 76 : 0, stage == 0 ? 0 : stage <= 2 ? -24 : -65);
                Label(section.transform, Names[stage], new Vector3(27, 5), 70, 17);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(paths[stage]));
                instance.transform.SetParent(section.transform, false);
                if (stage == 0) {
                    var canvas = instance.GetComponent<Canvas>();
                    canvas.renderMode = RenderMode.WorldSpace;
                    var rect = instance.GetComponent<RectTransform>();
                    rect.sizeDelta = new Vector2(900, 80);
                    rect.localScale = Vector3.one * .06f;
                    rect.localPosition = new Vector3(27, 0);
                }
                foreach (var script in instance.GetComponentsInChildren<MonoBehaviour>(true))
                    if (!(script is TMP_Text)) { script.enabled = false; PrefabUtility.RecordPrefabInstancePropertyModifications(script); }
                foreach (var body in instance.GetComponentsInChildren<Rigidbody2D>(true)) {
                    body.simulated = false; PrefabUtility.RecordPrefabInstancePropertyModifications(body);
                }
            }
            EditorSceneManager.SaveScene(catalog, ScenePath);
            AssetDatabase.SaveAssets();
            for (int i = 0; i < hashes.Length; i++)
                if (hashes[i] != Hash(StoryIntegrationBuilder.Scenes[i])) throw new Exception("Source scene changed");
            File.WriteAllLines("Docs/QuestPlacementCatalog.txt", manifest, new System.Text.UTF8Encoding(false));
            Debug.Log("QUEST PLACEMENT CATALOG PASS: 5 prefabs, source scene hashes unchanged, Finn and garden references intact.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        } catch (Exception e) {
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1);
            else throw;
        }
    }

    static void Arrange(GameObject root, StoryCampaignDatabase db, List<string> manifest)
    {
        var points = root.GetComponentsInChildren<StoryInteractionPoint>().OrderBy(p => p.stepId.StartsWith("hub_") ? 0 : 1).ThenBy(p => p.stepId).ToArray();
        var extras = root.transform.Cast<Transform>().Where(t => t.GetComponent<StoryInteractionPoint>() == null).ToList();
        for (int i = 0; i < points.Length; i++) {
            var point = points[i];
            Vector3 old = point.transform.position;
            var step = db.steps.FirstOrDefault(s => s.id == point.stepId);
            string title = step != null ? step.quest + " / " + step.title : HubTitle(point.stepId);
            var unit = new GameObject(title);
            unit.transform.SetParent(root.transform, false);
            unit.transform.position = old;
            point.transform.SetParent(unit.transform, true);
            foreach (var extra in extras.ToArray()) {
                Vector3 expected = extra.GetComponent<TextMeshPro>() != null ? old + Vector3.up * 2 : old;
                if (Vector3.Distance(extra.position, expected) > .15f) continue;
                extra.SetParent(unit.transform, true); extras.Remove(extra);
            }
            // Existing labels remain with the object but use one clear catalog label.
            foreach (var label in unit.GetComponentsInChildren<TextMeshPro>()) label.gameObject.SetActive(false);
            Label(unit.transform, title, new Vector3(0, 3), 17, 12);
            unit.transform.localPosition = new Vector3(i % 4 * 18, -(i / 4) * 10);
            string item = step?.grantItem != null ? AssetDatabase.GetAssetPath(step.grantItem) : "なし（会話・フラグ条件）";
            manifest.Add("  " + title + " / 対象 " + point.name + " / 入手ItemData: " + item);
            if (step?.consumeItems != null)
                foreach (var required in step.consumeItems.Where(x => x != null)) manifest.Add("    必要ItemData: " + AssetDatabase.GetAssetPath(required));
        }
        foreach (var extra in extras) UnityEngine.Object.DestroyImmediate(extra.gameObject);
    }

    static void Label(Transform parent, string text, Vector3 position, float width, float size)
    {
        var label = new GameObject("案内 " + text.Split('\n')[0], typeof(TextMeshPro)).GetComponent<TextMeshPro>();
        label.transform.SetParent(parent, false); label.transform.localPosition = position;
        label.font = Font; label.text = text; label.fontSize = size;
        label.alignment = TextAlignmentOptions.Center;
        label.rectTransform.sizeDelta = new Vector2(width, 4);
        label.color = new Color(.91f, .89f, .76f);
    }

    static void ValidatePrefab(string path, int stage)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try {
            foreach (var component in root.GetComponentsInChildren<Component>(true)) {
                if (component == null) throw new Exception("Missing script: " + path);
                var iterator = new SerializedObject(component).GetIterator();
                while (iterator.Next(true)) {
                    if (iterator.propertyType != SerializedPropertyType.ObjectReference) continue;
                    var reference = iterator.objectReferenceValue;
                    if (reference == null || EditorUtility.IsPersistent(reference)) continue;
                    Transform target = reference is Component c ? c.transform : reference is GameObject g ? g.transform : null;
                    if (target != null && target != root.transform && !target.IsChildOf(root.transform))
                        throw new Exception("External scene reference: " + path + " " + iterator.propertyPath);
                }
            }
            if (stage == 1) {
                var delivery = root.GetComponentInChildren<StoryFinnDelivery>(true);
                if (delivery == null || delivery.baldo == null || !delivery.baldo.IsChildOf(root.transform))
                    throw new Exception("Finn/Baldo reference lost");
            }
            if (stage == 4 && root.GetComponent<StoryCampaignScene>().gardenActor == null)
                throw new Exception("Garden reference lost");
        } finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static string Hash(string path)
    {
        using (var algorithm = SHA256.Create()) return Convert.ToBase64String(algorithm.ComputeHash(File.ReadAllBytes(path)));
    }

    static string HubTitle(string id)
    {
        string[] parts = id.Split('_');
        string role = parts[2] == "Baldo" ? "バルド" : parts[2] == "Glen" ? "グレン" : "ロアン";
        string quest = parts[1] == "1" && parts[2] != "Glen" ? "S1S01" :
            "S" + parts[1] + (parts[2] == "Baldo" ? "S02" : parts[2] == "Glen" ? "S03" : "S01");
        return quest + " / " + role + (parts[1] == "1" && parts[2] == "Roan" ? "（受注のみ）" : "（受注・報告）");
    }

    // Finishes the generated catalog and captures its static layout, without entering Play mode.
    public static void ValidateAndCapture()
    {
        try {
            var hashes = StoryIntegrationBuilder.Scenes.Select(Hash).ToArray();
            var db = AssetDatabase.LoadAssetAtPath<StoryCampaignDatabase>("Assets/Resources/StoryCampaign/Database.asset");
            var covered = new HashSet<string> { "S1M01", "S2M01", "S3M01" };
            for (int stage = 0; stage < Names.Length; stage++) {
                string path = Folder + "/" + Names[stage] + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try {
                    foreach (var point in root.GetComponentsInChildren<StoryInteractionPoint>(true)) {
                        if (point.stepId.StartsWith("hub_")) {
                            string title = HubTitle(point.stepId);
                            point.transform.parent.name = title;
                            var label = point.transform.parent.GetComponentsInChildren<TextMeshPro>(true).Single(t => t.name.StartsWith("案内 "));
                            label.text = title; label.name = "案内 " + title;
                        } else {
                            var step = db.steps.Single(s => s.id == point.stepId);
                            covered.Add(step.quest);
                        }
                    }
                    foreach (var label in root.GetComponentsInChildren<TextMeshPro>(true).Where(t => t.name.StartsWith("案内 ")))
                        label.fontSize = 12;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                } finally { PrefabUtility.UnloadPrefabContents(root); }
                ValidatePrefab(path, stage);
            }
            if (covered.Count != 12 || db.quests.Any(q => !covered.Contains(q.id))) throw new Exception("Quest coverage incomplete");
            string doc = File.ReadAllText("Docs/QuestPlacementCatalog.txt");
            for (int stage = 1; stage <= 3; stage++)
                foreach (string role in new[] { "Baldo", "Glen", "Roan" })
                    doc = doc.Replace("共有依頼人 " + stage + "_" + role, HubTitle("hub_" + stage + "_" + role));
            int index = doc.IndexOf("\n■ クエスト名の索引", StringComparison.Ordinal);
            if (index >= 0) doc = doc.Substring(0, index);
            doc += "\n■ クエスト名の索引（4メイン / 8サブ）\n";
            foreach (var quest in db.quests) doc += quest.id + " / " + quest.mission.DisplayName + "\n";
            File.WriteAllText("Docs/QuestPlacementCatalog.txt", doc, new System.Text.UTF8Encoding(false));
            EditorSceneManager.OpenScene(ScenePath);
            string materialPath = Folder + "/CatalogPreview.mat";
            var previewMaterial = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (previewMaterial == null) {
                previewMaterial = new Material(Shader.Find("Sprites/Default"));
                AssetDatabase.CreateAsset(previewMaterial, materialPath);
            }
            foreach (var sprite in UnityEngine.Object.FindObjectsByType<SpriteRenderer>(FindObjectsInactive.Include)) {
                sprite.sharedMaterial = previewMaterial;
                sprite.SetPropertyBlock(null);
                PrefabUtility.RecordPrefabInstancePropertyModifications(sprite);
            }
            foreach (var text in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Exclude)) {
                if (text is TextMeshPro && !PrefabUtility.IsPartOfPrefabInstance(text))
                    text.fontSize = text.transform.parent == null ? 20 : 17;
                text.ForceMeshUpdate();
            }
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
            var camera = UnityEngine.Object.FindFirstObjectByType<Camera>();
            Directory.CreateDirectory("Logs/QuestPlacementCatalog");
            for (int capture = 0; capture < Names.Length * 2; capture++) {
                int stage = capture % Names.Length;
                camera.transform.position = new Vector3(stage == 2 || stage == 4 ? 103 : 27, stage == 0 ? 2 : stage <= 2 ? -32 : -72, -100);
                camera.orthographicSize = 20;
                var target = new RenderTexture(1920, 1080, 24);
                target.Create(); camera.targetTexture = target;
                var previous = RenderTexture.active;
                try {
                    camera.Render(); RenderTexture.active = target;
                    var image = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
                    image.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0); image.Apply();
                    File.WriteAllBytes("Logs/QuestPlacementCatalog/Stage" + stage + ".png", image.EncodeToPNG());
                    UnityEngine.Object.DestroyImmediate(image);
                } finally {
                    RenderTexture.active = previous; camera.targetTexture = null;
                    target.Release(); UnityEngine.Object.DestroyImmediate(target);
                }
            }
            // Camera changes above are temporary and deliberately not saved.
            for (int i = 0; i < hashes.Length; i++)
                if (hashes[i] != Hash(StoryIntegrationBuilder.Scenes[i])) throw new Exception("Source scene changed");
            AssetDatabase.SaveAssets();
            Debug.Log("QUEST CATALOG VALIDATION PASS: all 12 quests covered, 5 prefab reference checks, 5 captures, original scenes unchanged.");
            if (Application.isBatchMode) EditorApplication.Exit(0);
        } catch (Exception e) {
            Debug.LogException(e);
            if (Application.isBatchMode) EditorApplication.Exit(1); else throw;
        }
    }
}
