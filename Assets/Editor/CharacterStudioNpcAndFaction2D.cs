#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Character Studio 2D additions to the Enemy-Studio-style prefab editor.
/// Only touched controls change a loaded prefab. Other components remain intact.
/// </summary>
public sealed partial class CharacterStudioWindow2D
{
    private static readonly HashSet<string> backedUpPaths = new HashSet<string>();
    private static readonly string[] rescueExtras =
    {
        "RescuePersonSymptoms2D", "RescuePersonAmbientSpeech2D",
        "RescuePersonFallDamage2D", "RescuePersonSpecialUnlock2D",
        "RescuePersonCorpseLoot2D"
    };

    private bool showNpcTradeProfile = true;

    private void CreateNewNpcDraftContents()
    {
        // 既存プロジェクトで使われているRescueNPCを優先する。
        int npcLayer = LayerMask.NameToLayer("RescueNPC");
        if (npcLayer < 0) npcLayer = LayerMask.NameToLayer("NPC");
        if (npcLayer >= 0) draft.layer = npcLayer;
        Rigidbody2D body = draft.AddComponent<Rigidbody2D>();
        body.bodyType = RigidbodyType2D.Dynamic;
        body.gravityScale = 2.5f;
        body.freezeRotation = true;
        CapsuleCollider2D capsule = draft.AddComponent<CapsuleCollider2D>();
        capsule.size = new Vector2(.85f, 1.55f);
        draft.AddComponent<CharacterHealth>();
        GameObject visual = new GameObject("Visual", typeof(SpriteRenderer));
        visual.transform.SetParent(draft.transform, false);
        visualRenderer = visual.GetComponent<SpriteRenderer>();
        DungeonNpcIdentity2D identity = draft.AddComponent<DungeonNpcIdentity2D>();
        SetValue(identity, "npcId", p => p.stringValue = NewNpcId());
        SetValue(identity, "displayName", p => p.stringValue = "洞窟の旅人");
        FactionMember2D faction = draft.AddComponent<FactionMember2D>();
        faction.Configure(FactionId2D.Adventurers);
        RepairReferences();
        Repaint();
    }

    private static string NewNpcId()
    {
        return "cave_npc_" + Guid.NewGuid().ToString("N").Substring(0, 8);
    }

    private void AssignFreshNpcIdIfPresent()
    {
        DungeonNpcIdentity2D identity = draft != null ? draft.GetComponent<DungeonNpcIdentity2D>() : null;
        if (identity == null) return;
        string newId = NewNpcId();
        SetValue(identity, "npcId", p => p.stringValue = newId);
        SetValue(identity, "displayName", p => p.stringValue = newName);
        Component special = FindComponentByName(draft, "RescuePersonSpecialUnlock2D");
        if (special != null)
            SetValue(special, "specialNpcId", p => p.stringValue = newId);
        MissionTargetIdentity2D mission = draft.GetComponent<MissionTargetIdentity2D>();
        if (mission != null)
            SetValue(mission, "targetId", p => p.stringValue = newId);
    }

    private void DrawTopShortcuts()
    {
        GUILayout.Space(5);
        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("旧 Enemy Studio", EditorStyles.miniButton, GUILayout.Width(116)))
            EditorApplication.ExecuteMenuItem("Tools/2D Shooter/Enemy Studio - 敵プレハブ管理");
        if (GUILayout.Button("旧 NPC Studio", EditorStyles.miniButton, GUILayout.Width(116)))
            EditorApplication.ExecuteMenuItem("Tools/2D Shooter/NPC Studio - 洞窟NPC統合管理");
        if (GUILayout.Button("Prefab構成チェック", EditorStyles.miniButton, GUILayout.Width(122)))
            CheckCharacterDraft();
        GUILayout.Space(12);
        EditorGUILayout.EndHorizontal();
    }

    private static bool IsKnownCharacterPrefab(GameObject prefab)
    {
        if (prefab == null || prefab.CompareTag("Player")) return false;
        if (prefab.GetComponent<EnemyChaser2D>() != null ||
            prefab.GetComponent<EnemyDeathHandler2D>() != null ||
            prefab.GetComponent<EnemyRangedAttack2D>() != null ||
            prefab.GetComponent<CharacterHealth>() != null ||
            prefab.GetComponent<DungeonNpcIdentity2D>() != null ||
            prefab.GetComponent<FactionMember2D>() != null) return true;
        return HasComponentByName(prefab, "RescuePersonTarget2D") ||
               HasComponentByName(prefab, "CaveOldManAlcoholExchange2D") ||
               HasComponentByName(prefab, "DungeonNpcShop2D");
    }

    private static bool HasComponentByName(GameObject root, string name)
    {
        return FindComponentByName(root, name) != null;
    }

    private static Component FindComponentByName(GameObject root, string typeName)
    {
        if (root == null) return null;
        foreach (Component c in root.GetComponents<Component>())
            if (c != null && c.GetType().Name == typeName) return c;
        return null;
    }

    private static Type FindBehaviourType(string typeName)
    {
        foreach (Type type in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
            if (type.Name == typeName && !type.IsAbstract && !type.IsGenericType)
                return type;
        return null;
    }

    private static Component AddByName(GameObject root, string typeName)
    {
        Component existing = FindComponentByName(root, typeName);
        if (existing != null) return existing;
        Type type = FindBehaviourType(typeName);
        return type == null ? null : root.AddComponent(type);
    }

    private void EnsurePhysicsForCombat()
    {
        if (draft.GetComponent<Rigidbody2D>() == null)
        {
            Rigidbody2D body = draft.AddComponent<Rigidbody2D>();
            body.freezeRotation = true;
        }
        if (draft.GetComponent<Collider2D>() == null)
            draft.AddComponent<CapsuleCollider2D>();
        if (draft.GetComponent<CharacterHealth>() == null)
            draft.AddComponent<CharacterHealth>();
    }

    private void DrawCharacterIdentity()
    {
        DungeonNpcIdentity2D identity = draft.GetComponent<DungeonNpcIdentity2D>();
        if (identity == null)
        {
            EditorGUILayout.HelpBox("NPC専用情報は未追加。敵だけのPrefabには不要です。", MessageType.Info);
            if (GUILayout.Button("洞窟NPCの基本情報を追加"))
            {
                identity = draft.AddComponent<DungeonNpcIdentity2D>();
                SetValue(identity, "npcId", p => p.stringValue = NewNpcId());
                SetValue(identity, "displayName", p => p.stringValue = newName);
            }
        }
        if (identity == null) return;
        DrawFields(identity, "名前・職業・識別ID", "npcId", "displayName", "occupation", "description", "tradeProfile");
        if (GUILayout.Button("Npc IDを新規発行（複製時など）"))
            if (EditorUtility.DisplayDialog("NPC IDの再発行", "既存ミッションや救出履歴がこのIDを参照している場合、変更で紐付けが切れます。再発行しますか？", "再発行", "キャンセル"))
                SetValue(identity, "npcId", p => p.stringValue = NewNpcId());
        if (GUILayout.Button("表示名と職業をRescuePersonInfo2Dへ同期"))
        {
            Component info = FindComponentByName(draft, "RescuePersonInfo2D");
            if (info != null)
            {
                var type = info.GetType();
                var nameFn = type.GetMethod("SetDisplayName", new[] { typeof(string) });
                var jobFn = type.GetMethod("SetOccupation", new[] { typeof(string) });
                if (nameFn != null) nameFn.Invoke(info, new object[] { identity.DisplayName });
                if (jobFn != null) jobFn.Invoke(info, new object[] { identity.Occupation });
                EditorUtility.SetDirty(info);
            }
            else EditorUtility.DisplayDialog("救出情報", "RescuePersonInfo2Dが見つかりません。救出設定から基本セットを追加してください。", "OK");
        }
    }

    private void DrawCharacterRescue()
    {
        EditorGUILayout.HelpBox("既存NPCに設定されたコンポーネントは自動削除しません。必要な機能だけON/OFFしてください。", MessageType.None);
        bool target = IsComponentEnabled(draft, "RescuePersonTarget2D");
        EditorGUI.BeginChangeCheck();
        bool desired = EditorGUILayout.ToggleLeft("救出・運搬NPCとして扱う", target, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (desired)
            {
                EnsurePhysicsForCombat();
                SetEnabled(AddByName(draft, "CarryableObject2D"), true);
                SetEnabled(AddByName(draft, "RescuePersonInfo2D"), true);
                SetEnabled(AddByName(draft, "RescuePersonTarget2D"), true);
                SetEnabled(AddByName(draft, "RescuePersonFallDamage2D"), true);
                DungeonNpcIdentity2D id = draft.GetComponent<DungeonNpcIdentity2D>();
                Component info = FindComponentByName(draft, "RescuePersonInfo2D");
                if (id != null && info != null)
                {
                    var type = info.GetType();
                    var nameMethod = type.GetMethod("SetDisplayName", new[] { typeof(string) });
                    var occupationMethod = type.GetMethod("SetOccupation", new[] { typeof(string) });
                    if (nameMethod != null) nameMethod.Invoke(info, new object[] { id.DisplayName });
                    if (occupationMethod != null) occupationMethod.Invoke(info, new object[] { id.Occupation });
                }
                MissionTargetIdentity2D mission = draft.GetComponent<MissionTargetIdentity2D>();
                if (mission == null) mission = draft.AddComponent<MissionTargetIdentity2D>();
                SetValue(mission, "targetType", property =>
                {
                    if (property.propertyType != SerializedPropertyType.Enum) return;
                    int index = Array.IndexOf(property.enumNames, "RescueNpc");
                    if (index >= 0) property.enumValueIndex = index;
                });
                if (id != null)
                {
                    SetValue(mission, "targetId", property => property.stringValue = id.NpcId);
                    SetValue(mission, "displayName", property => property.stringValue = id.DisplayName);
                }
            }
            else
            {
                SetEnabled(FindComponentByName(draft, "RescuePersonTarget2D"), false);
                SetEnabled(FindComponentByName(draft, "CarryableObject2D"), false);
            }
        }
        if (desired)
        {
            DrawRootOptional("CarryableObject2D", "持ち運び・背負う", "carryWeightKg");
            DrawRootOptional("RescuePersonInfo2D", "救出者の名前・職業・怪我");
            foreach (string extra in rescueExtras)
            {
                string title = extra == "RescuePersonSymptoms2D" ? "症状・出血・毒" :
                    extra == "RescuePersonAmbientSpeech2D" ? "独り言・吹き出し" :
                    extra == "RescuePersonFallDamage2D" ? "落下ダメージ" :
                    extra == "RescuePersonSpecialUnlock2D" ? "救出時の特別解放" :
                    "死亡時ItemBox";
                DrawRootOptional(extra, title);
            }
            if (IsComponentEnabled(draft, "RescuePersonCorpseLoot2D"))
            {
                Component corpse = FindComponentByName(draft, "RescuePersonCorpseLoot2D");
                EditorGUILayout.HelpBox("死体Lootを初めて追加する場合、下のボタンで子ItemBoxと必須参照を作ってね。", MessageType.Info);
                if (GUILayout.Button("死体Lootの子ItemBox・参照をセットアップ"))
                    SetupRescueCorpseLoot();
            }
        }
        GUILayout.Space(4);
        DrawRootOptional("CaveSupplyOldMan2D", "接近時の支援物資配布");
        if (GUILayout.Button("旧NPC Studioで救出詳細を開く"))
            EditorApplication.ExecuteMenuItem("Tools/2D Shooter/NPC Studio - 洞窟NPC統合管理");
    }

    private static bool IsComponentEnabled(GameObject root, string typeName)
    {
        Behaviour b = FindComponentByName(root, typeName) as Behaviour;
        return b != null && b.enabled;
    }

    private static void SetEnabled(Component component, bool enabled)
    {
        Behaviour b = component as Behaviour;
        if (b != null) { Undo.RecordObject(b, "Character Studio feature toggle"); b.enabled = enabled; }
    }

    private void DrawRootOptional(string typeName, string title, params string[] propertyNames)
    {
        Type type = FindBehaviourType(typeName);
        if (type == null)
        {
            EditorGUILayout.LabelField(title + "（未導入）", EditorStyles.miniLabel);
            return;
        }
        Component component = FindComponentByName(draft, typeName);
        bool current = (component as Behaviour) != null && ((Behaviour)component).enabled;
        EditorGUI.BeginChangeCheck();
        bool enable = EditorGUILayout.ToggleLeft(title, current);
        if (EditorGUI.EndChangeCheck())
        {
            if (enable && component == null) component = draft.AddComponent(type);
            SetEnabled(component, enable);
        }
        if (component == null || !enable) return;
        if (propertyNames != null && propertyNames.Length > 0)
            DrawFields(component, null, propertyNames);
        else
        {
            EditorGUI.indentLevel++;
            DrawAllSerializedProperties(component);
            EditorGUI.indentLevel--;
        }
    }

    private static void DrawAllSerializedProperties(Component component)
    {
        SerializedObject so = new SerializedObject(component);
        so.Update();
        SerializedProperty iterator = so.GetIterator();
        if (iterator.NextVisible(true))
        {
            do
            {
                if (iterator.propertyPath == "m_Script") continue;
                EditorGUILayout.PropertyField(iterator, true);
            } while (iterator.NextVisible(false));
        }
        so.ApplyModifiedProperties();
    }

    private void DrawCharacterTrade()
    {
        DungeonNpcIdentity2D identity = draft.GetComponent<DungeonNpcIdentity2D>();
        DungeonNpcShop2D shop = draft.GetComponent<DungeonNpcShop2D>();
        DungeonNpcBarter2D barter = draft.GetComponent<DungeonNpcBarter2D>();
        bool activeShop = shop != null && shop.enabled;
        bool activeBarter = barter != null && barter.enabled;
        EditorGUI.BeginChangeCheck();
        bool nextShop = EditorGUILayout.ToggleLeft("購入・売却NPC（Eキー）", activeShop, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (nextShop && shop == null) shop = draft.AddComponent<DungeonNpcShop2D>();
            SetEnabled(shop, nextShop);
        }
        EditorGUI.BeginChangeCheck();
        bool nextBarter = EditorGUILayout.ToggleLeft("物々交換NPC（地面にItemを落とす）", activeBarter, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (nextBarter && barter == null) barter = draft.AddComponent<DungeonNpcBarter2D>();
            SetEnabled(barter, nextBarter);
        }
        if (shop == null && barter == null)
        {
            EditorGUILayout.HelpBox("NPC売買・交換を使う場合は上の機能をON。", MessageType.None);
            return;
        }
        if (identity == null)
            EditorGUILayout.HelpBox("NPC基本情報（DungeonNpcIdentity2D）がありません。NPCの名前表示に利用します。", MessageType.Warning);
        DungeonNpcTradeProfile2D profile = null;
        if (shop != null)
        {
            SerializedProperty property = new SerializedObject(shop).FindProperty("tradeProfile");
            if (property != null) profile = property.objectReferenceValue as DungeonNpcTradeProfile2D;
        }
        if (profile == null && barter != null)
        {
            SerializedProperty property = new SerializedObject(barter).FindProperty("tradeProfile");
            if (property != null) profile = property.objectReferenceValue as DungeonNpcTradeProfile2D;
        }
        if (profile == null && identity != null) profile = identity.TradeProfile;
        EditorGUI.BeginChangeCheck();
        profile = (DungeonNpcTradeProfile2D)EditorGUILayout.ObjectField("取引Profile", profile, typeof(DungeonNpcTradeProfile2D), false);
        if (EditorGUI.EndChangeCheck()) BindTradeProfile(identity, shop, barter, profile);
        if (GUILayout.Button("新規取引Profileを作成（購入・売却・交換共通）"))
        {
            string directory = "Assets/CharacterStudio2D/TradeProfiles";
            if (EnsureFolder(directory))
            {
                string path = AssetDatabase.GenerateUniqueAssetPath(directory + "/" + SanitizeName(editingExisting ? draft.name : newName) + "_Trade.asset");
                DungeonNpcTradeProfile2D created = ScriptableObject.CreateInstance<DungeonNpcTradeProfile2D>();
                AssetDatabase.CreateAsset(created, path);
                AssetDatabase.SaveAssets();
                BindTradeProfile(identity, shop, barter, created);
                profile = created;
            }
        }
        if (shop != null && shop.enabled)
            DrawFields(shop, "ショップのキー・距離・UI", "interactionKey", "interactionRadius", "closeKey", "optionalGuiSkin", "showDebugLogs");
        if (barter != null && barter.enabled)
            DrawFields(barter, "物々交換の報酬Prefab・距離", "rewardPickupPrefab", "detectionRadius", "settleSeconds", "exchangeCooldown");
        if (profile != null)
        {
            showNpcTradeProfile = EditorGUILayout.Foldout(showNpcTradeProfile, "商品・買取条件・交換レシピ", true);
            if (showNpcTradeProfile)
            {
                EditorGUILayout.HelpBox("取引Profileは共有ScriptableObjectです。ここで編集すると同じProfileを使うすべてのNPCに即時反映されます。", MessageType.Warning);
                Editor editor = Editor.CreateEditor(profile);
                try { if (editor != null) editor.OnInspectorGUI(); }
                finally { if (editor != null) UnityEngine.Object.DestroyImmediate(editor); }
                DrawBulkProductDrop(profile);
            }
        }
        if (shop != null && shop.enabled && IsComponentEnabled(draft, "RescuePersonTarget2D"))
            EditorGUILayout.HelpBox("救出操作とショップのEキーが競合する場合があります。Interaction Keyを変更してね。", MessageType.Warning);
        if (barter != null && barter.enabled && HasComponentByName(draft, "CaveOldManAlcoholExchange2D"))
            EditorGUILayout.HelpBox("旧お酒交換スクリプトと交換が二重発動しないよう注意。", MessageType.Warning);
    }

    private static void BindTradeProfile(DungeonNpcIdentity2D identity, DungeonNpcShop2D shop,
        DungeonNpcBarter2D barter, DungeonNpcTradeProfile2D profile)
    {
        if (shop != null) SetValue(shop, "tradeProfile", p => p.objectReferenceValue = profile);
        if (barter != null) SetValue(barter, "tradeProfile", p => p.objectReferenceValue = profile);
        if (identity != null) SetValue(identity, "tradeProfile", p => p.objectReferenceValue = profile);
    }

    private void DrawBulkProductDrop(DungeonNpcTradeProfile2D profile)
    {
        Rect box = GUILayoutUtility.GetRect(0, 46, GUILayout.ExpandWidth(true));
        GUI.Box(box, "ItemData.assetをここに複数ドロップして商品追加\n（重複商品はスキップ・価格と在庫は保持）");
        Event e = Event.current;
        if (!box.Contains(e.mousePosition) ||
            (e.type != EventType.DragUpdated && e.type != EventType.DragPerform)) return;
        bool any = false;
        foreach (UnityEngine.Object o in DragAndDrop.objectReferences)
            if (o is ItemData || (o is DefaultAsset && AssetDatabase.IsValidFolder(AssetDatabase.GetAssetPath(o)))) any = true;
        if (!any) return;
        DragAndDrop.visualMode = DragAndDropVisualMode.Copy;
        if (e.type == EventType.DragPerform)
        {
            DragAndDrop.AcceptDrag();
            HashSet<ItemData> items = new HashSet<ItemData>();
            foreach (UnityEngine.Object o in DragAndDrop.objectReferences)
            {
                if (o is ItemData item) items.Add(item);
                else if (o is DefaultAsset)
                {
                    string folder = AssetDatabase.GetAssetPath(o);
                    if (!AssetDatabase.IsValidFolder(folder)) continue;
                    foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder }))
                    {
                        ItemData found = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));
                        if (found != null) items.Add(found);
                    }
                }
            }
            Undo.RecordObject(profile, "Character Studio add products");
            int count = 0;
            foreach (ItemData item in items)
            {
                if (profile.products.Exists(p => p != null && p.item == item)) continue;
                profile.products.Add(new DungeonNpcTradeProfile2D.Product { item = item, stock = 3 });
                count++;
            }
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
            Debug.Log("[CharacterStudio2D] 商品登録: " + count + " 件", profile);
        }
        e.Use();
    }

    private void DrawCharacterFaction()
    {
        EditorGUILayout.HelpBox("STEP2で敵対ルールを設定、STEP3で索敵・近接戦闘へ接続。ここではPrefabの所属とAIを編集します。", MessageType.None);
        FactionStudioStep4Panel2D.DrawEnemyDraft(draft);
        FactionMember2D member = draft.GetComponent<FactionMember2D>();
        EnemyChaser2D chaser = draft.GetComponent<EnemyChaser2D>();
        if (member != null && chaser != null && chaser.enabled)
        {
            SerializedObject so = new SerializedObject(chaser);
            SerializedProperty attack = so.FindProperty("attackDistance");
            SerializedProperty stop = so.FindProperty("stopDistance");
            if (attack != null && stop != null && stop.floatValue >= attack.floatValue)
                EditorGUILayout.HelpBox("Stop Distance が Attack Distance 以上です。近づいても攻撃が届かない可能性があります。", MessageType.Warning);
        }
        if (GUILayout.Button("STEP2 派閥ルールを開く"))
            EditorApplication.ExecuteMenuItem("Tools/2D Shooter/STEP2 - 派閥ルールエディター");
        EditorGUILayout.HelpBox("遠距離攻撃AI / 特殊AIは、現行STEP3では派閥ターゲットへ未連携の場合があります。", MessageType.Warning);
    }

    private void SetupRescueCorpseLoot()
    {
        Component corpse = FindComponentByName(draft, "RescuePersonCorpseLoot2D");
        if (corpse == null) return;
        Transform loot = draft.transform.Find("CorpseLoot");
        if (loot == null)
        {
            GameObject child = new GameObject("CorpseLoot");
            child.transform.SetParent(draft.transform, false);
            loot = child.transform;
        }
        CircleCollider2D circle = loot.GetComponent<CircleCollider2D>();
        if (circle == null) circle = loot.gameObject.AddComponent<CircleCollider2D>();
        circle.isTrigger = true;
        ItemBoxInventory box = loot.GetComponent<ItemBoxInventory>();
        if (box == null) box = loot.gameObject.AddComponent<ItemBoxInventory>();
        SetValue(box, "addStartingItemsOnAwake", p => p.boolValue = false);
        ItemBoxInteractable interactable = loot.GetComponent<ItemBoxInteractable>();
        if (interactable == null) interactable = loot.gameObject.AddComponent<ItemBoxInteractable>();
        ItemBoxRandomLootInitializer initializer = loot.GetComponent<ItemBoxRandomLootInitializer>();
        if (initializer == null) initializer = loot.gameObject.AddComponent<ItemBoxRandomLootInitializer>();
        SetValue(initializer, "rollAutomaticallyOnStart", p => p.boolValue = false);
        SerializedProperty lootTable = new SerializedObject(corpse).FindProperty("corpseLootTable");
        UnityEngine.Object table = lootTable != null ? lootTable.objectReferenceValue : null;
        if (table != null) SetValue(initializer, "lootTable", p => p.objectReferenceValue = table);
        SetValue(corpse, "corpseInventory", p => p.objectReferenceValue = box);
        SetValue(corpse, "corpseItemBoxInteractable", p => p.objectReferenceValue = interactable);
        SetValue(corpse, "corpseLootTrigger", p => p.objectReferenceValue = circle);
        SetValue(corpse, "lootInitializer", p => p.objectReferenceValue = initializer);
        interactable.enabled = false;
        circle.enabled = false;
        EditorUtility.DisplayDialog("死体Loot設定", "子ItemBoxと参照を作成しました。LootTableと死亡処理はInspectorでも確認してください。", "OK");
    }

    private void CheckCharacterDraft()
    {
        if (draft == null) return;
        List<string> problems = new List<string>();
        FactionMember2D faction = draft.GetComponent<FactionMember2D>();
        EnemyChaser2D chaser = draft.GetComponent<EnemyChaser2D>();
        FactionEnemyTargeting2D factionAI = draft.GetComponent<FactionEnemyTargeting2D>();
        if (faction != null && !string.IsNullOrEmpty(faction.FactionId) && chaser != null &&
            chaser.enabled && (factionAI == null || !factionAI.enabled))
            problems.Add("派閥付きの追跡Enemyなのに FactionEnemyTargeting2D が有効になっていません。");
        if (chaser != null && chaser.enabled)
        {
            SerializedObject so = new SerializedObject(chaser);
            SerializedProperty stop = so.FindProperty("stopDistance");
            SerializedProperty attack = so.FindProperty("attackDistance");
            if (stop != null && attack != null && stop.floatValue >= attack.floatValue)
                problems.Add("Stop Distance >= Attack Distance。接近しても殴れない可能性があります。");
            SerializedProperty ground = so.FindProperty("groundLayers");
            if (ground != null && ground.intValue == 0)
                problems.Add("Ground Layers が未設定。崖・接地判定を確認してください。");
        }
        if (IsComponentEnabled(draft, "RescuePersonTarget2D") &&
            !IsComponentEnabled(draft, "CarryableObject2D"))
            problems.Add("救出NPCですが CarryableObject2D が無効です。");
        DungeonNpcShop2D shop = draft.GetComponent<DungeonNpcShop2D>();
        DungeonNpcBarter2D barter = draft.GetComponent<DungeonNpcBarter2D>();
        if (shop != null && shop.enabled && !HasTradeProfile(shop)) problems.Add("ショップのTradeProfileがありません。");
        if (barter != null && barter.enabled)
        {
            if (!HasTradeProfile(barter)) problems.Add("物々交換のTradeProfileがありません。");
            SerializedProperty reward = new SerializedObject(barter).FindProperty("rewardPickupPrefab");
            if (reward == null || reward.objectReferenceValue == null)
                problems.Add("物々交換の報酬Pickup Prefabがありません。");
        }
        if (problems.Count == 0)
            EditorUtility.DisplayDialog("Character Studio 構成チェック", "基本的な構成上の警告は見つかりませんでした。実際のPlay動作は別途確認してね。", "OK");
        else
            EditorUtility.DisplayDialog("Character Studio 構成チェック", string.Join("\n\n", problems), "OK");
    }

    private bool ValidateForSave()
    {
        DungeonNpcShop2D shop = draft.GetComponent<DungeonNpcShop2D>();
        DungeonNpcBarter2D barter = draft.GetComponent<DungeonNpcBarter2D>();
        if (shop != null && shop.enabled && !HasTradeProfile(shop))
        {
            EditorUtility.DisplayDialog("取引Profileが必要", "ショップをONにした場合は、購入・売却のTradeProfileを割り当ててください。", "OK");
            return false;
        }
        if (barter != null && barter.enabled)
        {
            if (!HasTradeProfile(barter))
            {
                EditorUtility.DisplayDialog("取引Profileが必要", "物々交換をONにした場合はTradeProfileを割り当ててください。", "OK");
                return false;
            }
            SerializedProperty p = new SerializedObject(barter).FindProperty("rewardPickupPrefab");
            if (p == null || p.objectReferenceValue == null)
            {
                EditorUtility.DisplayDialog("報酬Prefabが必要", "物々交換をONにした場合はReward Pickup Prefabを割り当ててください。", "OK");
                return false;
            }
        }
        return true;
    }

    private static bool HasTradeProfile(Component component)
    {
        SerializedProperty p = new SerializedObject(component).FindProperty("tradeProfile");
        return p != null && p.objectReferenceValue != null;
    }

    private static void BackupExistingPrefab(string path)
    {
        if (string.IsNullOrEmpty(path) || backedUpPaths.Contains(path)) return;
        string folder = "Assets/CharacterStudio2D/Backups";
        if (!EnsureFolder(folder)) throw new IOException("バックアップフォルダを作れません。");
        string destination = AssetDatabase.GenerateUniqueAssetPath(folder + "/" +
            Path.GetFileNameWithoutExtension(path) + "_BeforeCharacterStudio.prefab");
        if (!AssetDatabase.CopyAsset(path, destination))
            throw new IOException("Prefabのバックアップ作成に失敗しました。変更は保存しません。");
        backedUpPaths.Add(path);
        AssetDatabase.SaveAssets();
    }
}
#endif
