#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>洞窟NPCのPrefab新規作成・既存編集・売買/交換プロフィール管理。</summary>
public sealed class DungeonNpcStudioWindow2D : EditorWindow
{
    private const string RootFolder = "Assets/DungeonNpcStudio";
    private readonly List<GameObject> npcPrefabs = new List<GameObject>();
    private Vector2 leftScroll;
    private Vector2 rightScroll;
    private string searchText = "";
    private GameObject editingPrefab;
    private FactionStudioStep4Settings2D factionStep4Settings = new FactionStudioStep4Settings2D(); // FACTION_STEP4_NPC_PANEL
    private GameObject visualTemplate;
    private Sprite sprite;
    private string npcId = "cave_npc_";
    private string displayName = "洞窟の旅人";
    private string occupation = "旅人";
    private string description = "";
    private int maxHealth = 100;
    private float carryWeight = 60f;
    private bool rescue = true;
    private bool symptoms = true;
    private bool ambientSpeech = true;
    private bool corpseLoot;
    private bool specialUnlock;
    private bool supply;
    private bool shop;
    private bool barter;
    private ItemBoxLootTable corpseLootTable;
    private DungeonNpcTradeProfile2D tradeProfile;
    private WorldItemPickup droppedItemPrefab;
    private bool foldRescue = true;
    private bool foldTrade = true;
    private bool foldOptional;
    private string message = "";
    private Editor cachedProfileEditor;
    private DungeonNpcTradeProfile2D cachedProfile;

    [MenuItem("Tools/2D Shooter/NPC Studio - 洞窟NPC統合管理")]
    public static void Open()
    {
        var window = GetWindow<DungeonNpcStudioWindow2D>("NPC Studio 2D");
        window.minSize = new Vector2(930, 590);
        window.Refresh();
    }

    private void OnEnable() { Refresh(); }
    private void OnDisable() { if (cachedProfileEditor != null) DestroyImmediate(cachedProfileEditor); }

    private void Refresh()
    {
        npcPrefabs.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab != null && (prefab.GetComponent<DungeonNpcIdentity2D>() != null ||
                                  prefab.GetComponent<RescuePersonTarget2D>() != null ||
                                  prefab.GetComponent<CaveOldManAlcoholExchange2D>() != null ||
                                  prefab.GetComponent<DungeonNpcShop2D>() != null))
                npcPrefabs.Add(prefab);
        }
        npcPrefabs.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
        Repaint();
    }

    private void OnGUI()
    {
        EditorGUILayout.BeginHorizontal();
        DrawSidebar();
        GUILayout.Space(9);
        DrawMain();
        EditorGUILayout.EndHorizontal();
    }

    private void DrawSidebar()
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(238));
        GUILayout.Label("NPC LIBRARY", EditorStyles.boldLabel);
        searchText = EditorGUILayout.TextField("検索", searchText);
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("新規NPC")) NewNpc();
        if (GUILayout.Button("再検索")) Refresh();
        EditorGUILayout.EndHorizontal();
        GUILayout.Label("登録済みPrefab: " + npcPrefabs.Count, EditorStyles.miniLabel);
        leftScroll = EditorGUILayout.BeginScrollView(leftScroll, GUI.skin.box);
        foreach (GameObject prefab in npcPrefabs)
        {
            if (prefab == null || (!string.IsNullOrWhiteSpace(searchText) &&
                prefab.name.IndexOf(searchText, StringComparison.OrdinalIgnoreCase) < 0)) continue;
            if (GUILayout.Toggle(editingPrefab == prefab, prefab.name, "Button", GUILayout.Height(26)))
                if (editingPrefab != prefab) LoadPrefab(prefab);
        }
        EditorGUILayout.EndScrollView();
        if (editingPrefab != null)
        {
            if (GUILayout.Button("PrefabをProjectで選択"))
            {
                Selection.activeObject = editingPrefab;
                EditorGUIUtility.PingObject(editingPrefab);
            }
            if (GUILayout.Button("新しいPrefabに複製")) DuplicateToNew();
        }
        EditorGUILayout.HelpBox("既存Prefabの上書き時も、未指定のコンポーネントと既存の参照はできる限り維持します。", MessageType.Info);
        EditorGUILayout.EndVertical();
    }

    private void DrawMain()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Label("DUNGEON NPC STUDIO 2D", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("町の住人機能は含みません。救出・物々交換・売買・支援をNPCごとに組み合わせます。", MessageType.None);
        rightScroll = EditorGUILayout.BeginScrollView(rightScroll);
        GUILayout.Label(editingPrefab != null ? "編集中: " + editingPrefab.name : "新規NPC", EditorStyles.largeLabel);
        visualTemplate = (GameObject)EditorGUILayout.ObjectField("外見用Prefab (任意)", visualTemplate, typeof(GameObject), false);
        sprite = (Sprite)EditorGUILayout.ObjectField("メインSprite", sprite, typeof(Sprite), false);
        displayName = EditorGUILayout.TextField("NPC名", displayName);
        npcId = EditorGUILayout.TextField("NPC ID", npcId);
        occupation = EditorGUILayout.TextField("職業", occupation);
        GUILayout.Label("説明 / 備考");
        description = EditorGUILayout.TextArea(description, GUILayout.Height(48));

        EditorGUILayout.Space(8);
        foldRescue = EditorGUILayout.Foldout(foldRescue, "救出・健康・死亡", true, EditorStyles.foldoutHeader);
        if (foldRescue)
        {
            EditorGUI.indentLevel++;
            rescue = EditorGUILayout.ToggleLeft("救出NPC（運搬できる）", rescue);
            if (rescue)
            {
                maxHealth = EditorGUILayout.IntField("最大HP", maxHealth);
                carryWeight = EditorGUILayout.FloatField("運搬重量 kg", carryWeight);
                symptoms = EditorGUILayout.ToggleLeft("症状（出血・毒・骨折）", symptoms);
                ambientSpeech = EditorGUILayout.ToggleLeft("運搬中・落下中の独り言", ambientSpeech);
                corpseLoot = EditorGUILayout.ToggleLeft("死亡時ItemBox", corpseLoot);
                if (corpseLoot)
                    corpseLootTable = (ItemBoxLootTable)EditorGUILayout.ObjectField("死体LootTable", corpseLootTable, typeof(ItemBoxLootTable), false);
                specialUnlock = EditorGUILayout.ToggleLeft("救出時の特殊解放", specialUnlock);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);
        foldTrade = EditorGUILayout.Foldout(foldTrade, "購入・売却・物々交換", true, EditorStyles.foldoutHeader);
        if (foldTrade)
        {
            EditorGUI.indentLevel++;
            shop = EditorGUILayout.ToggleLeft("販売NPC（Eで購入・売却）", shop);
            barter = EditorGUILayout.ToggleLeft("物々交換NPC（地面にItemを落とす）", barter);
            if (barter)
                droppedItemPrefab = (WorldItemPickup)EditorGUILayout.ObjectField("報酬用DroppedItem Prefab", droppedItemPrefab, typeof(WorldItemPickup), false);
            if (shop || barter)
            {
                tradeProfile = (DungeonNpcTradeProfile2D)EditorGUILayout.ObjectField("NPC取引Profile", tradeProfile, typeof(DungeonNpcTradeProfile2D), false);
                if (tradeProfile == null)
                {
                    if (GUILayout.Button("新しい取引Profileを作成")) CreateTradeProfile();
                }
                else
                {
                    DrawProfileEditor();
                    EditorGUILayout.Space(5);
                    DrawBulkDrop();
                }
                EditorGUILayout.HelpBox("商品はItemData.asset。ItemプレハブではなくItemDataを登録してください。\n物々交換は1レシピにつき要求Item1種類です。", MessageType.Info);
            }
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);
        foldOptional = EditorGUILayout.Foldout(foldOptional, "支援NPC・運用上の注意", true, EditorStyles.foldoutHeader);
        if (foldOptional)
        {
            EditorGUI.indentLevel++;
            supply = EditorGUILayout.ToggleLeft("接近時に物資を配布する（CaveSupplyOldMan2D）", supply);
            EditorGUILayout.HelpBox("支援物資の候補、商人のUIフォント、NPCのアニメーションなどはPrefab Inspector側でも個別調整できます。\n救出NPCと販売NPCを兼用する場合はEキーの競合を避けるため、DungeonNpcShop2DのInteraction Keyを変更してください。", MessageType.Warning);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.Space(8);
        FactionStudioStep4Panel2D.DrawNpcSettings(factionStep4Settings);

        if (GUILayout.Button(editingPrefab != null ? "既存Prefabに設定を保存" : "新しいNPC Prefabを作成", GUILayout.Height(38))) SaveNpc();
        if (!string.IsNullOrEmpty(message)) EditorGUILayout.HelpBox(message, MessageType.Info);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawProfileEditor()
    {
        if (cachedProfile != tradeProfile)
        {
            if (cachedProfileEditor != null) DestroyImmediate(cachedProfileEditor);
            cachedProfile = tradeProfile;
            cachedProfileEditor = tradeProfile != null ? Editor.CreateEditor(tradeProfile) : null;
        }
        if (cachedProfileEditor == null) return;
        GUILayout.Label("商品・交換レシピ・買取条件", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        cachedProfileEditor.OnInspectorGUI();
        if (EditorGUI.EndChangeCheck())
        {
            EditorUtility.SetDirty(tradeProfile);
            AssetDatabase.SaveAssets();
        }
    }

    private void DrawBulkDrop()
    {
        GUILayout.Label("商品一括登録", EditorStyles.boldLabel);
        Rect area = GUILayoutUtility.GetRect(10, 47, GUILayout.ExpandWidth(true));
        GUI.Box(area, "ItemData.asset を複数、またはItemDataフォルダをドロップ\n（既存の商品・価格・在庫は保持）");
        Event ev = Event.current;
        if (!area.Contains(ev.mousePosition)) return;
        if (ev.type == EventType.DragUpdated || ev.type == EventType.DragPerform)
        {
            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
            if (ev.type == EventType.DragPerform)
            {
                DragAndDrop.AcceptDrag();
                var unique = new HashSet<ItemData>();
                foreach (UnityEngine.Object obj in DragAndDrop.objectReferences)
                {
                    if (obj is ItemData item) unique.Add(item);
                    else if (obj is DefaultAsset)
                    {
                        string folder = AssetDatabase.GetAssetPath(obj);
                        if (!AssetDatabase.IsValidFolder(folder)) continue;
                        foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new [] {folder}))
                        {
                            var candidate = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                            if (candidate != null) unique.Add(candidate);
                        }
                    }
                }
                Undo.RecordObject(tradeProfile, "一括商品登録");
                int count = 0;
                foreach (ItemData item in unique)
                {
                    bool exists = tradeProfile.products.Exists(p => p != null && p.item == item);
                    if (exists) continue;
                    tradeProfile.products.Add(new DungeonNpcTradeProfile2D.Product { item = item, stock = 3 });
                    count++;
                }
                EditorUtility.SetDirty(tradeProfile);
                AssetDatabase.SaveAssets();
                message = count + "種類を追加しました（重複除外）。";
            }
            ev.Use();
        }
    }

    private void NewNpc()
    {
        factionStep4Settings = new FactionStudioStep4Settings2D(); // FACTION_STEP4_NPC_RESET

        editingPrefab = null;
        visualTemplate = null;
        sprite = null;
        npcId = "cave_npc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        displayName = "洞窟の旅人";
        occupation = "旅人";
        description = "";
        maxHealth = 100;
        carryWeight = 60;
        rescue = true;
        symptoms = true;
        ambientSpeech = true;
        corpseLoot = specialUnlock = supply = shop = barter = false;
        corpseLootTable = null;
        tradeProfile = null;
        droppedItemPrefab = null;
        message = "";
    }

    private void LoadPrefab(GameObject prefab)
    {
        FactionStudioStep4Panel2D.ReadFrom(prefab, factionStep4Settings); // FACTION_STEP4_NPC_LOAD

        editingPrefab = prefab;
        visualTemplate = null;
        var identity = prefab.GetComponent<DungeonNpcIdentity2D>();
        npcId = identity != null ? identity.NpcId : "cave_npc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        displayName = identity != null ? identity.DisplayName : prefab.name;
        occupation = identity != null ? identity.Occupation : "旅人";
        description = identity != null ? identity.Description : "";
        SpriteRenderer rend = prefab.GetComponentInChildren<SpriteRenderer>(true);
        sprite = rend != null ? rend.sprite : null;
        rescue = prefab.GetComponent<RescuePersonTarget2D>() != null && prefab.GetComponent<RescuePersonTarget2D>().enabled;
        symptoms = prefab.GetComponent<RescuePersonSymptoms2D>() != null && prefab.GetComponent<RescuePersonSymptoms2D>().enabled;
        ambientSpeech = prefab.GetComponent<RescuePersonAmbientSpeech2D>() != null && prefab.GetComponent<RescuePersonAmbientSpeech2D>().enabled;
        corpseLoot = prefab.GetComponent<RescuePersonCorpseLoot2D>() != null && prefab.GetComponent<RescuePersonCorpseLoot2D>().enabled;
        specialUnlock = prefab.GetComponent<RescuePersonSpecialUnlock2D>() != null && prefab.GetComponent<RescuePersonSpecialUnlock2D>().enabled;
        supply = prefab.GetComponent<CaveSupplyOldMan2D>() != null && prefab.GetComponent<CaveSupplyOldMan2D>().enabled;
        shop = prefab.GetComponent<DungeonNpcShop2D>() != null && prefab.GetComponent<DungeonNpcShop2D>().enabled;
        barter = prefab.GetComponent<DungeonNpcBarter2D>() != null && prefab.GetComponent<DungeonNpcBarter2D>().enabled;
        tradeProfile = identity != null ? identity.TradeProfile : null;
        var shopComp = prefab.GetComponent<DungeonNpcShop2D>();
        if (shopComp != null)
        {
            var so = new SerializedObject(shopComp);
            if (tradeProfile == null) tradeProfile = so.FindProperty("tradeProfile").objectReferenceValue as DungeonNpcTradeProfile2D;
        }
        var barterComp = prefab.GetComponent<DungeonNpcBarter2D>();
        droppedItemPrefab = null;
        if (barterComp != null)
        {
            var so = new SerializedObject(barterComp);
            if (tradeProfile == null) tradeProfile = so.FindProperty("tradeProfile").objectReferenceValue as DungeonNpcTradeProfile2D;
            droppedItemPrefab = so.FindProperty("rewardPickupPrefab").objectReferenceValue as WorldItemPickup;
        }
        var hp = prefab.GetComponent<CharacterHealth>();
        if (hp != null) maxHealth = new SerializedObject(hp).FindProperty("maxHealth").intValue;
        var carry = prefab.GetComponent<CarryableObject2D>();
        if (carry != null) carryWeight = carry.CarryWeightKg;
        corpseLootTable = null;
        var corpse = prefab.GetComponent<RescuePersonCorpseLoot2D>();
        if (corpse != null) corpseLootTable = new SerializedObject(corpse).FindProperty("corpseLootTable").objectReferenceValue as ItemBoxLootTable;
        message = "";
    }

    private void DuplicateToNew()
    {
        if (editingPrefab == null) return;
        visualTemplate = editingPrefab;
        editingPrefab = null;
        npcId = "cave_npc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
        displayName += "_Copy";
        // 共通Trade Profileを変更しないよう、新しいプロフィールを作成することを促す。
        tradeProfile = null;
        message = "コピー元Prefabを外見用Prefabとして選択しました。新しい取引Profileを作ってから保存してください。";
    }

    private void CreateTradeProfile()
    {
        EnsureFolder(RootFolder);
        EnsureFolder(RootFolder + "/Profiles");
        string path = AssetDatabase.GenerateUniqueAssetPath(RootFolder + "/Profiles/" + SafeName(displayName) + "_Trade.asset");
        tradeProfile = CreateInstance<DungeonNpcTradeProfile2D>();
        AssetDatabase.CreateAsset(tradeProfile, path);
        AssetDatabase.SaveAssets();
        Selection.activeObject = tradeProfile;
    }

    private void SaveNpc()
    {
        if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(npcId))
        { EditorUtility.DisplayDialog("NPC Studio", "NPC名とNPC IDは必須です。", "OK"); return; }
        if ((shop || barter) && tradeProfile == null)
        { EditorUtility.DisplayDialog("NPC Studio", "購入・交換をONにした場合は取引Profileを作成してください。", "OK"); return; }
        if (barter && droppedItemPrefab == null)
        { EditorUtility.DisplayDialog("NPC Studio", "物々交換の報酬用DroppedItem Prefabが必要です。", "OK"); return; }
        if (shop && rescue && !EditorUtility.DisplayDialog("NPC Studio", "救出NPCと商人の両機能がONです。Eキーの競合を避けるため保存後に商人のInteraction Keyを変更してください。", "続ける", "中止")) return;
        if (editingPrefab != null && !EditorUtility.DisplayDialog("既存Prefabの更新", editingPrefab.name + " を上書き保存します。事前にバックアップを推奨します。", "保存", "戻る")) return;

        EnsureFolder(RootFolder);
        EnsureFolder(RootFolder + "/Prefabs");
        string outputPath = editingPrefab != null ? AssetDatabase.GetAssetPath(editingPrefab) :
            AssetDatabase.GenerateUniqueAssetPath(RootFolder + "/Prefabs/" + SafeName(displayName) + ".prefab");
        bool editing = editingPrefab != null;
        GameObject root = null;
        try
        {
            root = editing ? PrefabUtility.LoadPrefabContents(outputPath) :
                visualTemplate != null ? Instantiate(visualTemplate) : new GameObject(displayName);
            root.name = displayName;
            ConfigurePrefab(root);
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(root, outputPath);
            AssetDatabase.SaveAssets();
            message = "保存しました: " + outputPath;
            Refresh();
            if (saved != null) LoadPrefab(saved);
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
            EditorUtility.DisplayDialog("NPC Studio エラー", ex.Message, "OK");
        }
        finally
        {
            if (root != null)
            {
                if (editing) PrefabUtility.UnloadPrefabContents(root);
                else DestroyImmediate(root);
            }
        }
    }

    private void ConfigurePrefab(GameObject root)
    {
        DungeonNpcIdentity2D identity = GetOrAdd<DungeonNpcIdentity2D>(root);
        SerializedObject id = new SerializedObject(identity);
        id.FindProperty("npcId").stringValue = npcId.Trim();
        id.FindProperty("displayName").stringValue = displayName.Trim();
        id.FindProperty("occupation").stringValue = occupation.Trim();
        id.FindProperty("description").stringValue = description;
        id.FindProperty("tradeProfile").objectReferenceValue = tradeProfile;
        id.ApplyModifiedPropertiesWithoutUndo();

        if (sprite != null)
        {
            SpriteRenderer render = root.GetComponentInChildren<SpriteRenderer>(true);
            if (render == null) render = root.AddComponent<SpriteRenderer>();
            render.sprite = sprite;
        }
        if (rescue)
        {
            Rigidbody2D body = GetOrAdd<Rigidbody2D>(root);
            if (root.GetComponent<Collider2D>() == null) root.AddComponent<CapsuleCollider2D>();
            var health = GetOrAdd<CharacterHealth>(root);
            SetSerialized(health, "maxHealth", Mathf.Max(1, maxHealth));
            var carry = GetOrAdd<CarryableObject2D>(root);
            carry.enabled = true;
            SetSerialized(carry, "carryWeightKg", Mathf.Max(0, carryWeight));
            var info = GetOrAdd<RescuePersonInfo2D>(root);
            info.SetDisplayName(displayName);
            info.SetOccupation(occupation);
            GetOrAdd<RescuePersonTarget2D>(root).enabled = true;
            SetOptional<RescuePersonSymptoms2D>(root, symptoms);
            SetOptional<RescuePersonAmbientSpeech2D>(root, ambientSpeech);
            GetOrAdd<RescuePersonFallDamage2D>(root).enabled = true;
            if (specialUnlock)
            {
                var special = GetOrAdd<RescuePersonSpecialUnlock2D>(root);
                special.enabled = true;
                SetSerialized(special, "specialNpcId", npcId.Trim());
            }
            else SetOptional<RescuePersonSpecialUnlock2D>(root, false);
            if (corpseLoot) ConfigureCorpse(root);
            else SetOptional<RescuePersonCorpseLoot2D>(root, false);
            var mission = GetOrAdd<MissionTargetIdentity2D>(root);
            SetSerialized(mission, "targetId", npcId.Trim());
            var mso = new SerializedObject(mission);
            mso.FindProperty("targetType").enumValueIndex = (int)MissionTargetType2D.RescueNpc;
            mso.FindProperty("displayName").stringValue = displayName.Trim();
            mso.ApplyModifiedPropertiesWithoutUndo();
        }
        if (!rescue)
        {
            SetOptional<RescuePersonTarget2D>(root, false);
            SetOptional<CarryableObject2D>(root, false);
            SetOptional<RescuePersonSymptoms2D>(root, false);
            SetOptional<RescuePersonAmbientSpeech2D>(root, false);
            SetOptional<RescuePersonCorpseLoot2D>(root, false);
            SetOptional<RescuePersonFallDamage2D>(root, false);
            SetOptional<RescuePersonSpecialUnlock2D>(root, false);
        }
        SetOptional<CaveSupplyOldMan2D>(root, supply);
        if (shop)
        {
            var shopComp = GetOrAdd<DungeonNpcShop2D>(root);
            SetSerialized(shopComp, "tradeProfile", tradeProfile);
            shopComp.enabled = true;
        }
        else
        {
            var existing = root.GetComponent<DungeonNpcShop2D>();
            if (existing != null) existing.enabled = false;
        }
        if (barter)
        {
            var barterComp = GetOrAdd<DungeonNpcBarter2D>(root);
            SetSerialized(barterComp, "tradeProfile", tradeProfile);
            SetSerialized(barterComp, "rewardPickupPrefab", droppedItemPrefab);
            barterComp.enabled = true;
            if (root.GetComponent<CaveOldManAlcoholExchange2D>() != null)
                Debug.LogWarning("[NPC Studio] 旧酒交換と汎用物々交換が両方ONです。酒を二重処理しないよう片方をOFFにしてください。", root);
        }
        else
        {
            var existing = root.GetComponent<DungeonNpcBarter2D>();
            if (existing != null) existing.enabled = false;
        }
        FactionStudioStep4Panel2D.ApplyTo(root, factionStep4Settings); // FACTION_STEP4_NPC_APPLY
    }

    private void ConfigureCorpse(GameObject root)
    {
        var corpse = GetOrAdd<RescuePersonCorpseLoot2D>(root);
        corpse.enabled = true;
        ItemBoxInventory inventory = null;
        Transform lootChild = root.transform.Find("CorpseLoot");
        if (lootChild == null)
        {
            GameObject obj = new GameObject("CorpseLoot");
            obj.transform.SetParent(root.transform, false);
            lootChild = obj.transform;
        }
        var circle = lootChild.GetComponent<CircleCollider2D>();
        if (circle == null) circle = lootChild.gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        inventory = GetOrAdd<ItemBoxInventory>(lootChild.gameObject);
        SetSerialized(inventory, "addStartingItemsOnAwake", false);
        var interactable = GetOrAdd<ItemBoxInteractable>(lootChild.gameObject);
        interactable.enabled = false;
        circle.enabled = false;
        var initializer = GetOrAdd<ItemBoxRandomLootInitializer>(lootChild.gameObject);
        SetSerialized(initializer, "lootTable", corpseLootTable);
        SetSerialized(initializer, "rollAutomaticallyOnStart", false);
        SetSerialized(corpse, "corpseInventory", inventory);
        SetSerialized(corpse, "corpseItemBoxInteractable", interactable);
        SetSerialized(corpse, "corpseLootTrigger", circle);
        SetSerialized(corpse, "lootInitializer", initializer);
        SetSerialized(corpse, "corpseLootTable", corpseLootTable);
    }

    private static void SetOptional<T>(GameObject obj, bool active) where T : Behaviour
    {
        T existing = obj.GetComponent<T>();
        if (active) (existing != null ? existing : obj.AddComponent<T>()).enabled = true;
        else if (existing != null) existing.enabled = false;
    }

    private static T GetOrAdd<T>(GameObject obj) where T : Component
    {
        T component = obj.GetComponent<T>();
        return component != null ? component : obj.AddComponent<T>();
    }

    private static void SetSerialized(UnityEngine.Object obj, string field, UnityEngine.Object value)
    {
        var so = new SerializedObject(obj);
        var p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(obj.GetType().Name + "." + field + " が存在しません");
        p.objectReferenceValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerialized(UnityEngine.Object obj, string field, int value)
    {
        var so = new SerializedObject(obj);
        var p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(obj.GetType().Name + "." + field + " が存在しません");
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerialized(UnityEngine.Object obj, string field, float value)
    {
        var so = new SerializedObject(obj);
        var p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(obj.GetType().Name + "." + field + " が存在しません");
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerialized(UnityEngine.Object obj, string field, bool value)
    {
        var so = new SerializedObject(obj);
        var p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(obj.GetType().Name + "." + field + " が存在しません");
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetSerialized(UnityEngine.Object obj, string field, string value)
    {
        var so = new SerializedObject(obj);
        var p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(obj.GetType().Name + "." + field + " が存在しません");
        p.stringValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static string SafeName(string name)
    {
        string value = name;
        foreach (char ch in Path.GetInvalidFileNameChars()) value = value.Replace(ch, '_');
        value = value.Trim();
        return string.IsNullOrEmpty(value) ? "DungeonNPC" : value;
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = path.Substring(0, path.LastIndexOf('/'));
        string child = path.Substring(path.LastIndexOf('/') + 1);
        if (!AssetDatabase.IsValidFolder(parent)) EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, child);
    }
}
#endif
