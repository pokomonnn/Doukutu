#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Enemy 2D Prefab management in one window. Editor-only; does not modify gameplay scripts.
/// Creates prefab assets and edits existing prefabs in isolated PrefabUtility contents.
/// </summary>
public sealed partial class CharacterStudioWindow2D : EditorWindow
{
    private enum Template { CaveNpc, Melee, RangedStraight, RangedArc }
    private enum Section { Base, Health, Ai, Attack, Animation, Hit, Death, Drop, Mission, Identity, Rescue, Trade, Faction, Advanced }

    private static readonly Color Accent = new Color(0.39f, 0.75f, 0.66f);
    private static readonly Color Panel = new Color(0.15f, 0.175f, 0.20f);
    private static readonly Color Muted = new Color(0.67f, 0.72f, 0.77f);

    [SerializeField] private string newName = "Character_New";
    [SerializeField] private string prefabFolder = "Assets/Prefabs/Characters";
    [SerializeField] private string search = "";
    [SerializeField] private Template selectedTemplate = Template.CaveNpc;
    [SerializeField] private bool autoFitCollider = true;
    [SerializeField] private bool showOnlyEnemyPrefabs = true;
    [SerializeField] private bool[] expanded = { true, true, true, true, false, false, false, false, false, true, true, true, true, false };

    private Vector2 listScroll;
    private Vector2 settingsScroll;
    private readonly List<string> prefabPaths = new List<string>();
    private GameObject draft;
    private bool editingExisting;
    private string loadedPath;
    private SpriteRenderer visualRenderer;
    private bool listDirty = true;
    private GUIStyle titleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle descriptionStyle;
    private GUIStyle previewStyle;

    [MenuItem("Tools/2D Shooter/Character Studio - NPC・Enemy統合管理")]
    public static void Open()
    {
        CharacterStudioWindow2D window = GetWindow<CharacterStudioWindow2D>("Character Studio 2D");
        window.minSize = new Vector2(960f, 640f);
        window.Show();
    }

    private void OnEnable()
    {
        EditorApplication.projectChanged += MarkListDirty;
        if (draft == null) CreateNewDraft(false);
    }

    private void OnDisable()
    {
        EditorApplication.projectChanged -= MarkListDirty;
        DiscardDraft();
    }

    private void MarkListDirty() { listDirty = true; Repaint(); }

    private void OnGUI()
    {
        if (draft == null) CreateNewDraft(false);
        InitStyles();
        if (listDirty) RefreshList();
        DrawHeader();

        EditorGUILayout.BeginHorizontal();
        DrawLeftPane(Mathf.Clamp(position.width * .32f, 290f, 390f));
        GUILayout.Space(8);
        DrawRightPane();
        EditorGUILayout.EndHorizontal();
    }

    private void InitStyles()
    {
        if (titleStyle != null) return;
        titleStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 20, alignment = TextAnchor.MiddleLeft };
        titleStyle.normal.textColor = Color.white;
        sectionStyle = new GUIStyle(EditorStyles.boldLabel) { fontSize = 13 };
        sectionStyle.normal.textColor = Accent;
        descriptionStyle = new GUIStyle(EditorStyles.wordWrappedMiniLabel);
        descriptionStyle.normal.textColor = Muted;
        previewStyle = new GUIStyle(EditorStyles.centeredGreyMiniLabel);
        previewStyle.normal.textColor = Muted;
    }

    private void DrawHeader()
    {
        Rect header = GUILayoutUtility.GetRect(0f, 69f, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(header, Panel);
        GUI.Label(new Rect(header.x + 18, header.y + 8, header.width - 30, 26), "CHARACTER STUDIO 2D", titleStyle);
        GUI.Label(new Rect(header.x + 19, header.y + 37, header.width - 30, 22),
            "NPC + ENEMY  /  洞窟用キャラクターをまとめて作成・編集", descriptionStyle);
        GUILayout.Space(9);
        DrawTopShortcuts();
    }

    private void DrawLeftPane(float width)
    {
        EditorGUILayout.BeginVertical(GUILayout.Width(width));
        GUILayout.Label("01  CHARACTER PREVIEW", sectionStyle);
        GUILayout.Space(5);
        DrawPreview(width);
        GUILayout.Space(7);

        if (editingExisting)
        {
            EditorGUILayout.HelpBox("既存Prefabを編集中。『変更をPrefabに保存』を押すまでは元のPrefabは変更されません。", MessageType.Info);
        }
        else
        {
            newName = EditorGUILayout.TextField("Prefab名", newName);
            selectedTemplate = (Template)EditorGUILayout.EnumPopup("テンプレート", selectedTemplate);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("テンプレート設定を適用", GUILayout.Height(24)))
            {
                if (selectedTemplate == Template.CaveNpc && draft.GetComponent<EnemyChaser2D>() != null)
                    EditorUtility.DisplayDialog("テンプレート切替", "敵AIを安全に削除しないため、NPCに切り替える場合は「新規作成に戻す」を押してください。", "OK");
                else if (selectedTemplate != Template.CaveNpc && draft.GetComponent<EnemyChaser2D>() == null)
                    EditorUtility.DisplayDialog("テンプレート切替", "Enemyテンプレートに切り替える場合は「新規作成に戻す」を押してください。", "OK");
                else ApplyTemplate(selectedTemplate);
            }
            if (GUILayout.Button("新規作成に戻す", GUILayout.Height(24))) CreateNewDraft(true);
            EditorGUILayout.EndHorizontal();
        }

        GUILayout.Space(8);
        EditorGUILayout.LabelField("保存先", EditorStyles.boldLabel);
        EditorGUILayout.BeginHorizontal();
        prefabFolder = EditorGUILayout.TextField(prefabFolder);
        if (GUILayout.Button("...", GUILayout.Width(32))) ChooseFolder();
        EditorGUILayout.EndHorizontal();

        GUILayout.Space(7);
        if (editingExisting)
        {
            GUI.backgroundColor = Accent;
            if (GUILayout.Button("変更をPrefabに保存", GUILayout.Height(38))) SaveExistingPrefab();
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button("このPrefabを複製して新規作成", GUILayout.Height(28))) DuplicateSelectedAsNew();
            if (GUILayout.Button("新しいCharacterを作成", GUILayout.Height(26))) CreateNewDraft(true);
            if (GUILayout.Button("ProjectでPrefabを選択", GUILayout.Height(24)))
            {
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(loadedPath);
                EditorGUIUtility.PingObject(Selection.activeObject);
            }
        }
        else
        {
            GUI.backgroundColor = Accent;
            if (GUILayout.Button("Character Prefabを作成", GUILayout.Height(42))) SaveNewPrefab();
            GUI.backgroundColor = Color.white;
        }

        GUILayout.Space(15);
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("02  CHARACTER LIBRARY", sectionStyle);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("更新", GUILayout.Width(47))) { listDirty = true; RefreshList(); }
        EditorGUILayout.EndHorizontal();
        search = EditorGUILayout.TextField("検索", search);
        EditorGUI.BeginChangeCheck();
        showOnlyEnemyPrefabs = EditorGUILayout.ToggleLeft("NPC / Enemy系コンポーネントがあるPrefabのみ", showOnlyEnemyPrefabs);
        if (EditorGUI.EndChangeCheck()) { listDirty = true; RefreshList(); }
        GUILayout.Label($"{prefabPaths.Count} Prefabs", descriptionStyle);
        listScroll = EditorGUILayout.BeginScrollView(listScroll, GUILayout.MinHeight(135));
        int drawn = 0;
        foreach (string path in prefabPaths)
        {
            if (path == null ||
                (!string.IsNullOrEmpty(search) &&
                 Path.GetFileNameWithoutExtension(path).IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)) continue;
            drawn++;
            bool selected = editingExisting && path == loadedPath;
            string label = (selected ? "● " : "  ") + Path.GetFileNameWithoutExtension(path);
            if (GUILayout.Button(label, selected ? EditorStyles.toolbarButton : EditorStyles.miniButton, GUILayout.Height(26)))
                LoadExisting(path);
        }
        if (drawn == 0) GUILayout.Label("一致するPrefabはありません", descriptionStyle);
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawPreview(float width)
    {
        Rect r = GUILayoutUtility.GetRect(width, 225f, GUILayout.Width(width));
        EditorGUI.DrawRect(r, new Color(.09f, .11f, .135f));
        Rect center = new Rect(r.x + 12, r.y + 10, r.width - 24, r.height - 36);
        Sprite sprite = visualRenderer != null ? visualRenderer.sprite : null;
        if (sprite != null && sprite.texture != null)
        {
            try
            {
                Rect source = sprite.textureRect;
                Texture2D tex = sprite.texture;
                Rect uv = new Rect(source.x / tex.width, source.y / tex.height,
                    source.width / tex.width, source.height / tex.height);
                float aspect = Mathf.Max(.05f, source.width / Mathf.Max(1f, source.height));
                float targetWidth = Mathf.Min(center.width, center.height * aspect);
                float targetHeight = Mathf.Min(center.height, center.width / aspect);
                Rect imageRect = new Rect(center.center.x - targetWidth * .5f,
                    center.center.y - targetHeight * .5f, targetWidth, targetHeight);
                GUI.DrawTextureWithTexCoords(imageRect, tex, uv, true);
            }
            catch (Exception) { GUI.Label(center, "Sprite Preview unavailable", previewStyle); }
        }
        else GUI.Label(center, "ここにSpriteを表示\n\n右の『外観・物理』からSpriteを設定", previewStyle);
        GUI.Label(new Rect(r.x + 8, r.yMax - 25, r.width - 16, 18),
            editingExisting ? "EDITING  " + Path.GetFileNameWithoutExtension(loadedPath) : "NEW CHARACTER PREVIEW", previewStyle);
    }

    private void DrawRightPane()
    {
        EditorGUILayout.BeginVertical(GUILayout.ExpandWidth(true));
        GUILayout.Label("03  CHARACTER SETTINGS", sectionStyle);
        EditorGUILayout.LabelField("既存Prefabを直接上書きせず作業用コピーで編集します。保存するまで反映されません。", descriptionStyle);
        GUILayout.Space(5);
        settingsScroll = EditorGUILayout.BeginScrollView(settingsScroll);
        if (draft == null) { EditorGUILayout.HelpBox("Enemyを読み込めません。", MessageType.Error); }
        else
        {
            DrawSection(Section.Base, "外観・物理", DrawBase);
            DrawSection(Section.Health, "HP・防御力", DrawHealth);
            DrawSection(Section.Ai, "索敵・移動AI・ジャンプ", DrawAI);
            DrawSection(Section.Attack, "近接 / 遠距離攻撃", DrawAttack);
            DrawSection(Section.Animation, "Animator・アニメーション連携", DrawAnimation);
            DrawSection(Section.Hit, "被弾・ノックバック", DrawHit);
            DrawSection(Section.Death, "死亡・エフェクト・SE", DrawDeath);
            DrawSection(Section.Drop, "ドロップアイテム", DrawDrops);
            DrawSection(Section.Mission, "ミッション対象・ダメージ表示", DrawMission);
            DrawSection(Section.Identity, "洞窟NPC・基本情報", DrawCharacterIdentity);
            DrawSection(Section.Rescue, "救出・運搬・症状・支援", DrawCharacterRescue);
            DrawSection(Section.Trade, "購入・売却・物々交換", DrawCharacterTrade);
            DrawCharacterDialogueSettings();
            DrawSection(Section.Faction, "派閥・友好度連携・戦闘", DrawCharacterFaction);
            DrawSection(Section.Advanced, "全コンポーネントをInspector形式で表示", DrawAdvanced);
            GUILayout.Space(15);
        }
        EditorGUILayout.EndScrollView();
        EditorGUILayout.EndVertical();
    }

    private void DrawSection(Section section, string title, Action draw)
    {
        int i = (int)section;
        while (expanded == null || expanded.Length <= i)
            Array.Resize(ref expanded, 14);
        GUILayout.Space(5);
        Rect background = GUILayoutUtility.GetRect(0, 27, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(background, new Color(.17f, .20f, .23f));
        expanded[i] = EditorGUI.Foldout(new Rect(background.x + 9, background.y + 3, background.width - 10, 20),
            expanded[i], title, true, EditorStyles.foldoutHeader);
        if (!expanded[i]) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        try { draw(); }
        catch (Exception e) { EditorGUILayout.HelpBox("設定欄エラー: " + e.Message, MessageType.Error); }
        EditorGUILayout.EndVertical();
    }

    private void DrawBase()
    {
        EditorGUI.BeginChangeCheck();
        string tag = EditorGUILayout.TagField("タグ", draft.tag);
        int layer = EditorGUILayout.LayerField("Layer", draft.layer);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(draft, "Enemy Tag/Layer");
            draft.tag = tag;
            draft.layer = layer;
        }
        if (visualRenderer == null) { EditorGUILayout.HelpBox("SpriteRendererが見つかりません。下の修復ボタンで追加できます。", MessageType.Warning); }
        else
        {
            SerializedObject so = new SerializedObject(visualRenderer);
            SerializedProperty sprite = so.FindProperty("m_Sprite");
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(sprite, new GUIContent("外観 Sprite"));
            bool changed = EditorGUI.EndChangeCheck();
            Property(so, "m_Color", "色");
            Property(so, "m_SortingOrder", "描画順序");
            so.ApplyModifiedProperties();
            SortingLayer[] layers = SortingLayer.layers;
            if (layers.Length > 0)
            {
                string[] labels = Array.ConvertAll(layers, x => x.name);
                int current = Array.FindIndex(layers, x => x.id == visualRenderer.sortingLayerID);
                int next = EditorGUILayout.Popup("Sorting Layer", Mathf.Max(0, current), labels);
                if (next != current) { Undo.RecordObject(visualRenderer, "Enemy sorting layer"); visualRenderer.sortingLayerID = layers[next].id; }
            }
            if (changed && autoFitCollider) FitColliderToSprite();
        }
        Animator animator = draft.GetComponentInChildren<Animator>(true);
        if (animator != null)
        {
            SerializedObject so = new SerializedObject(animator);
            Property(so, "m_Controller", "Animator Controller");
            so.ApplyModifiedProperties();
        }
        else EditorGUILayout.HelpBox("Animatorなし。『参照を修復』で追加できます。", MessageType.Warning);
        Transform visual = draft.transform.Find("Visual");
        if (visual != null)
        {
            EditorGUI.BeginChangeCheck();
            Vector3 scale = EditorGUILayout.Vector3Field("Visual Scale", visual.localScale);
            if (EditorGUI.EndChangeCheck()) { Undo.RecordObject(visual, "Enemy Visual Scale"); visual.localScale = scale; }
        }

        Rigidbody2D rb = draft.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            SerializedObject so = new SerializedObject(rb);
            Property(so, "m_GravityScale", "重力 Scale");
            Property(so, "m_Mass", "質量");
            Property(so, "m_CollisionDetection", "衝突判定");
            so.ApplyModifiedProperties();
        }
        Collider2D collider = draft.GetComponent<Collider2D>();
        if (collider != null)
        {
            SerializedObject so = new SerializedObject(collider);
            Property(so, "m_Size", "Collider Size");
            Property(so, "m_Offset", "Collider Offset");
            so.ApplyModifiedProperties();
        }
        autoFitCollider = EditorGUILayout.ToggleLeft("Spriteを変更したらColliderを自動調整", autoFitCollider);
        if (GUILayout.Button("SpriteからCollider Sizeを再計算")) FitColliderToSprite();
        if (GUILayout.Button("物理・SpriteRendererを補い、既存の参照を修復"))
        {
            if (draft.GetComponent<Rigidbody2D>() == null) draft.AddComponent<Rigidbody2D>();
            if (draft.GetComponent<Collider2D>() == null) draft.AddComponent<CapsuleCollider2D>();
            if (draft.GetComponentInChildren<SpriteRenderer>(true) == null)
            {
                GameObject v = new GameObject("Visual", typeof(SpriteRenderer));
                v.transform.SetParent(draft.transform, false);
            }
            RepairReferences();
        }
    }

    private void DrawHealth()
    {
        if (draft.GetComponent<CharacterHealth>() == null)
        {
            if (GUILayout.Button("CharacterHealthを追加")) draft.AddComponent<CharacterHealth>();
        }
        if (draft.GetComponent<CharacterHealth>() != null)
            DrawFields<CharacterHealth>("最大HP・無敵時間・被ダメージSE", "maxHealth", "invincibilityDuration", "damageSound", "damageSoundVolume");
        DrawOptional<EnemyArmor2D>("装甲を使用", "armorRating", "damageReductionPerArmorPoint", "maximumDamageReduction");
    }

    private void DrawAI()
    {
        DrawWanderAISettings();
        DrawInvestigationAISettings();
        DrawCharacterAIStep3Settings();
        DrawCharacterAIStep4Settings();
        EditorGUILayout.Space(8);
        EnemyChaser2D chaser = draft.GetComponent<EnemyChaser2D>();
        if (chaser == null)
        {
            EditorGUILayout.HelpBox("移動AIは未追加です。会話NPC・商人などはAIなしでOKです。", MessageType.Info);
            if (GUILayout.Button("EnemyChaser2D（追跡・ジャンプAI）を追加"))
            {
                EnsurePhysicsForCombat();
                chaser = draft.AddComponent<EnemyChaser2D>();
                RepairReferences();
            }
            if (chaser == null) return;
        }
        EditorGUI.BeginChangeCheck();
        bool active = EditorGUILayout.ToggleLeft("追跡AIを有効化", chaser.enabled);
        if (EditorGUI.EndChangeCheck()) chaser.enabled = active;
        if (chaser == null) { EditorGUILayout.HelpBox("EnemyChaser2Dなし", MessageType.Warning); return; }
        DrawFields(chaser, "発見・追跡", "detectionDistance", "loseTargetDistance", "requireLineOfSight", "obstacleLayers");
        DrawFields(chaser, "移動と間合い", "moveSpeed", "combatMovementStyle", "stopDistance", "keepDistanceMinimum", "keepDistanceMaximum");
        DrawFields(chaser, "崖・段差", "groundLayers", "avoidDeepCliffs", "cliffCheckLayers", "cliffCheckDistance", "maxSafeDropHeight");
        DrawFields(chaser, "ジャンプ", "enableJump", "jumpPower", "jumpCooldown", "jumpObstacleLayers", "groundCheckDistance", "wallCheckDistance", "jumpWhenPlayerIsAbove");
        DrawFields(chaser, "見た目", "spriteFacesRightByDefault");
        if (GetMask(chaser, "groundLayers") == 0)
            EditorGUILayout.HelpBox("Ground Layersが空です。地面判定・ジャンプ・崖判定を使う場合は指定してください。", MessageType.Warning);
    }

    private void DrawAttack()
    {
        EnemyChaser2D chaser = draft.GetComponent<EnemyChaser2D>();
        if (chaser != null) DrawFields(chaser, "近接攻撃", "enableBuiltInMeleeAttack", "attackDistance", "attackDamage", "attackInterval", "stopWhileAttacking");
        DrawOptional<EnemyRangedAttack2D>("遠距離攻撃を使用", "projectilePrefab", "minimumAttackDistance", "maximumAttackDistance", "attackInterval", "projectileSpeed", "projectileLifeTime", "requireDetectedPlayer", "useBallisticArc", "arcHeight", "projectileGravityScale", "requireLineOfSight", "obstacleLayers", "targetOffset");
        EnemyRangedAttack2D ranged = draft.GetComponent<EnemyRangedAttack2D>();
        if (ranged != null && ranged.enabled)
        {
            if (chaser != null && GetBool(chaser, "enableBuiltInMeleeAttack"))
                EditorGUILayout.HelpBox("近接攻撃と遠距離攻撃が両方有効です。両方使う敵以外は片方をOFFにしてください。", MessageType.Info);
            SerializedObject so = new SerializedObject(ranged);
            SerializedProperty p = so.FindProperty("projectilePrefab");
            if (p != null && p.objectReferenceValue == null)
                EditorGUILayout.HelpBox("遠距離攻撃にはProjectile Prefabが必須です。弾にはDamageDealerとRigidbody2Dを設定してください。", MessageType.Warning);
        }
    }

    private void DrawAnimation()
    {
        DrawOptional<EnemyAnimator2D>("Animator連携を使用", "isMovingBoolName", "hasTargetBoolName", "moveSpeedFloatName", "attackTriggerName", "rangedAttackTriggerName", "specialAttackTriggerName", "hitTriggerName", "deathTriggerName");
        EditorGUILayout.LabelField("AnimatorのParameter名はControllerの設定と一致させてください。", descriptionStyle);
    }

    private void DrawHit()
    {
        DrawOptional<EnemyHitReaction2D>("被弾リアクションを使用", "knockbackSpeed", "upwardKnockbackSpeed", "hitStunDuration", "knockbackResistance", "hitStunResistance", "enableHitFlash", "flashDuration", "flashCount", "flashColor", "flashIntensity");
    }

    private void DrawDeath()
    {
        DrawOptional<EnemyDeathHandler2D>("死亡処理を使用", "deathSound", "deathSoundVolume", "deathEffectPrefab", "deathEffectOffset", "deathEffectDestroyDelay", "enableDeathSlide", "deathSlideDistance", "deathSlideDuration", "destroyAfterDeath", "destroyDelay");
        EditorGUILayout.HelpBox("EnemyAnimator2DにもDie Triggerがあります。死亡Triggerを二重発火させないため、DeathHandlerのdeathTriggerNameは通常空欄のままでOK。", MessageType.None);
    }

    private void DrawDrops()
    {
        DrawOptional<EnemyDropTable2D>("アイテムドロップを使用", "droppedItemPrefab", "dropEntries", "dropPositionOffset", "horizontalScatter", "initialDropVelocity", "verticalVelocityRandomness", "preventDuplicateSpawn");
        EditorGUILayout.LabelField("Dropped Item PrefabにはWorldItemPickupが必要です。", descriptionStyle);
    }

    private void DrawMission()
    {
        DrawOptional<MissionTargetIdentity2D>("ミッション対象IDを付ける", "targetType", "targetId", "displayName");
        EditorGUILayout.LabelField("対象外にしたい場合は Target Type を None にしてください。", descriptionStyle);
        DrawOptional<EnemyDamagePopupSpawner>("ダメージ数字を使用", "popupPrefab", "worldOffset", "randomOffset", "combineSameDamageGroup", "combineWindow");
    }

    private void DrawAdvanced()
    {
        EditorGUILayout.HelpBox("ここでは未掲載の設定も確認できます。TransformとColliderなど、Unity標準のコンポーネントも含みます。", MessageType.Info);
        Component[] components = draft.GetComponentsInChildren<Component>(true);
        foreach (Component c in components)
        {
            if (c == null || c is Transform) continue;
            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField(c.GetType().Name, EditorStyles.boldLabel);
            using (EditorGUI.IndentLevelScope indent = new EditorGUI.IndentLevelScope())
            {
                SerializedObject so = new SerializedObject(c);
                SerializedProperty iterator = so.GetIterator();
                if (iterator.NextVisible(true))
                {
                    do
                    {
                        if (iterator.propertyPath == "m_Script")
                        {
                            using (new EditorGUI.DisabledScope(true)) EditorGUILayout.PropertyField(iterator, true);
                        }
                        else EditorGUILayout.PropertyField(iterator, true);
                    }
                    while (iterator.NextVisible(false));
                }
                so.ApplyModifiedProperties();
            }
        }
    }

    private void DrawOptionalByClassName(string typeName, string label)
    {
        // The project may have additional enemy behaviours which were not uploaded.
        // Resolve by name to avoid adding compile-time dependencies for optional scripts.
        Type type = null;
        foreach (Type candidate in TypeCache.GetTypesDerivedFrom<MonoBehaviour>())
        {
            if (candidate.Name == typeName && !candidate.IsAbstract) { type = candidate; break; }
        }
        if (type == null)
        {
            EditorGUILayout.LabelField(label + " : スクリプト未検出", descriptionStyle);
            return;
        }
        Behaviour component = draft.GetComponent(type) as Behaviour;
        bool current = component != null && component.enabled;
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUILayout.ToggleLeft(label, current, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (enabled && component == null) component = draft.AddComponent(type) as Behaviour;
            if (component != null)
            {
                Undo.RecordObject(component, "Enemy optional feature");
                component.enabled = enabled;
                if (enabled) RepairReferences();
            }
        }
        if (component == null || !component.enabled) return;
        SerializedObject so = new SerializedObject(component);
        SerializedProperty iterator = so.GetIterator();
        EditorGUI.indentLevel++;
        if (iterator.NextVisible(true))
        {
            do
            {
                if (iterator.propertyPath != "m_Script")
                    EditorGUILayout.PropertyField(iterator, true);
            }
            while (iterator.NextVisible(false));
        }
        so.ApplyModifiedProperties();
        EditorGUI.indentLevel--;
    }

    private void DrawFields<T>(string heading, params string[] names) where T : Component
    {
        DrawFields(draft.GetComponent<T>(), heading, names);
    }
    private void DrawFields(Component target, string heading, params string[] names)
    {
        if (target == null) { EditorGUILayout.HelpBox(heading + ": component not found", MessageType.Warning); return; }
        if (!string.IsNullOrEmpty(heading)) EditorGUILayout.LabelField(heading, EditorStyles.boldLabel);
        SerializedObject so = new SerializedObject(target);
        so.Update();
        foreach (string name in names)
        {
            SerializedProperty prop = so.FindProperty(name);
            if (prop != null) EditorGUILayout.PropertyField(prop, new GUIContent(ObjectNames.NicifyVariableName(name)), true);
            else EditorGUILayout.LabelField("未対応: " + name, descriptionStyle);
        }
        so.ApplyModifiedProperties();
        GUILayout.Space(4);
    }

    private void DrawOptional<T>(string label, params string[] properties) where T : Behaviour
    {
        T component = draft.GetComponent<T>();
        bool current = component != null && component.enabled;
        EditorGUI.BeginChangeCheck();
        bool enabled = EditorGUILayout.ToggleLeft(label, current, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (component == null && enabled) component = draft.AddComponent<T>();
            if (component != null) { Undo.RecordObject(component, "Enemy feature"); component.enabled = enabled; }
            // EnemyDeathHandler2D invokes SpawnDrops directly, even if the drop component is disabled.
            if (component is EnemyDropTable2D)
            {
                EnemyDeathHandler2D death = draft.GetComponent<EnemyDeathHandler2D>();
                SetValue(death, "dropTable", p => p.objectReferenceValue = enabled ? component : null);
            }
            if (enabled) RepairReferences();
        }
        if (component != null && component.enabled)
        {
            EditorGUI.indentLevel++;
            DrawFields(component, null, properties);
            EditorGUI.indentLevel--;
        }
    }

    private static void Property(SerializedObject so, string path, string label)
    {
        SerializedProperty p = so.FindProperty(path);
        if (p != null) EditorGUILayout.PropertyField(p, new GUIContent(label), true);
    }

    private static bool GetBool(Component component, string name)
    {
        SerializedProperty p = new SerializedObject(component).FindProperty(name);
        return p != null && p.boolValue;
    }
    private static int GetMask(Component component, string name)
    {
        SerializedProperty p = new SerializedObject(component).FindProperty(name);
        return p != null ? p.intValue : 0;
    }
    private static void SetValue(Component c, string name, Action<SerializedProperty> set)
    {
        if (c == null) return;
        SerializedObject so = new SerializedObject(c);
        SerializedProperty p = so.FindProperty(name);
        if (p != null) { set(p); so.ApplyModifiedPropertiesWithoutUndo(); }
    }
    private static void Ref(Component c, string name, UnityEngine.Object val)
    {
        // Existing enemies may use custom child references; never overwrite them on "repair".
        if (c == null || val == null) return;
        SetValue(c, name, p =>
        {
            if (p.objectReferenceValue == null) p.objectReferenceValue = val;
        });
    }

    private void CreateNewDraft(bool ask)
    {
        if (ask && editingExisting && !EditorUtility.DisplayDialog("未保存の変更", "現在のPrefabの未保存変更を破棄して新規作成しますか？", "破棄", "キャンセル")) return;
        DiscardDraft();
        editingExisting = false;
        loadedPath = null;
        draft = new GameObject("CharacterDraft_EditorOnly");
        draft.hideFlags = HideFlags.HideAndDontSave;
        if (selectedTemplate == Template.CaveNpc)
        {
            CreateNewNpcDraftContents();
            return;
        }
        int enemyLayer = LayerMask.NameToLayer("Enemy");
        if (enemyLayer >= 0) draft.layer = enemyLayer;
        Rigidbody2D rb = draft.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 2.5f;
        rb.freezeRotation = true;
        CapsuleCollider2D col = draft.AddComponent<CapsuleCollider2D>();
        col.size = new Vector2(.85f, 1.55f);
        draft.AddComponent<CharacterHealth>();
        draft.AddComponent<EnemyChaser2D>();
        draft.AddComponent<EnemyArmor2D>();
        draft.AddComponent<EnemyHitReaction2D>();
        draft.AddComponent<EnemyDeathHandler2D>();
        draft.AddComponent<EnemyDropTable2D>();
        draft.AddComponent<EnemyAnimator2D>();
        MissionTargetIdentity2D mission = draft.AddComponent<MissionTargetIdentity2D>();
        SetValue(mission, "targetType", p => p.enumValueIndex = 1); // Enemy
        SetValue(mission, "targetId", p => p.stringValue = "enemy_new");
        GameObject visual = new GameObject("Visual", typeof(SpriteRenderer), typeof(Animator));
        visual.transform.SetParent(draft.transform, false);
        GameObject muzzle = new GameObject("ProjectileSpawnPoint");
        muzzle.transform.SetParent(draft.transform, false);
        muzzle.transform.localPosition = new Vector3(.5f, .2f, 0f);
        GameObject drops = new GameObject("DropSpawnPoint");
        drops.transform.SetParent(draft.transform, false);
        drops.transform.localPosition = new Vector3(0f, .15f, 0f);
        visualRenderer = visual.GetComponent<SpriteRenderer>();
        ApplyTemplate(selectedTemplate);
        RepairReferences(true);
        Repaint();
    }

    private void ApplyTemplate(Template template)
    {
        if (draft == null) return;
        EnemyChaser2D chaser = draft.GetComponent<EnemyChaser2D>();
        if (chaser == null) return;
        if (template == Template.CaveNpc) return; // 新規NPCへの切替は「新規作成」ボタンから実行
        bool ranged = template != Template.Melee;
        SetValue(chaser, "combatMovementStyle", p => p.enumValueIndex = ranged ? 1 : 0);
        SetValue(chaser, "enableBuiltInMeleeAttack", p => p.boolValue = !ranged);
        SetValue(chaser, "moveSpeed", p => p.floatValue = ranged ? 1.65f : 2.2f);
        SetValue(chaser, "keepDistanceMinimum", p => p.floatValue = 3f);
        SetValue(chaser, "keepDistanceMaximum", p => p.floatValue = 6f);
        EnemyRangedAttack2D attack = draft.GetComponent<EnemyRangedAttack2D>();
        if (ranged && attack == null) attack = draft.AddComponent<EnemyRangedAttack2D>();
        if (attack != null)
        {
            attack.enabled = ranged;
            SetValue(attack, "useBallisticArc", p => p.boolValue = template == Template.RangedArc);
        }
        RepairReferences();
    }

    private void RepairReferences(bool addMissing = false)
    {
        if (draft == null) return;
        CharacterHealth health = draft.GetComponent<CharacterHealth>();
        Rigidbody2D rb = draft.GetComponent<Rigidbody2D>();
        EnemyChaser2D chaser = draft.GetComponent<EnemyChaser2D>();
        EnemyRangedAttack2D ranged = draft.GetComponent<EnemyRangedAttack2D>();
        EnemyHitReaction2D hit = draft.GetComponent<EnemyHitReaction2D>();
        EnemyDeathHandler2D death = draft.GetComponent<EnemyDeathHandler2D>();
        EnemyDropTable2D drop = draft.GetComponent<EnemyDropTable2D>();
        EnemyAnimator2D animate = draft.GetComponent<EnemyAnimator2D>();
        EnemyDamagePopupSpawner popup = draft.GetComponent<EnemyDamagePopupSpawner>();
        // Existing prefabs may intentionally be stationary. Do not add AI / physics
        // automatically on Save. Missing components are added only by the repair button.
        if (health == null && addMissing) health = draft.AddComponent<CharacterHealth>();
        if (rb == null && addMissing) rb = draft.AddComponent<Rigidbody2D>();
        if (draft.GetComponent<Collider2D>() == null && addMissing) draft.AddComponent<CapsuleCollider2D>();
        if (chaser == null && addMissing) chaser = draft.AddComponent<EnemyChaser2D>();
        Transform visual = draft.transform.Find("Visual");
        if (visual == null)
        {
            SpriteRenderer sr = draft.GetComponentInChildren<SpriteRenderer>(true);
            if (sr != null) visual = sr.transform;
            else if (addMissing)
            {
                GameObject v = new GameObject("Visual", typeof(SpriteRenderer), typeof(Animator));
                v.transform.SetParent(draft.transform, false);
                visual = v.transform;
            }
        }
        visualRenderer = visual != null ? visual.GetComponent<SpriteRenderer>() : null;
        if (visualRenderer == null && visual != null && addMissing)
            visualRenderer = visual.gameObject.AddComponent<SpriteRenderer>();
        Animator animator = draft.GetComponentInChildren<Animator>(true);
        if (animator == null && visual != null && addMissing)
            animator = visual.gameObject.AddComponent<Animator>();
        Ref(chaser, "spriteRenderer", visualRenderer);
        Ref(ranged, "enemyChaser", chaser);
        Ref(ranged, "ownHealth", health);
        Transform muzzle = draft.transform.Find("ProjectileSpawnPoint");
        if (muzzle == null && ranged != null && addMissing) { GameObject m = new GameObject("ProjectileSpawnPoint"); m.transform.SetParent(draft.transform, false); m.transform.localPosition = new Vector3(.5f, .2f, 0f); muzzle = m.transform; }
        Ref(ranged, "projectileSpawnPoint", muzzle);

        Ref(hit, "health", health); Ref(hit, "enemyRigidbody", rb); Ref(hit, "enemyChaser", chaser);
        Ref(death, "health", health); Ref(death, "enemyRigidbody", rb); Ref(death, "enemyChaser", chaser);
        Ref(death, "hitReaction", hit);
        if (drop == null || !drop.enabled)
            SetValue(death, "dropTable", p => p.objectReferenceValue = null);
        else Ref(death, "dropTable", drop);
        Ref(death, "rangedAttack", ranged); Ref(death, "animator", animator);
        Ref(animate, "animator", animator); Ref(animate, "health", health); Ref(animate, "enemyRigidbody", rb);
        Ref(animate, "enemyChaser", chaser); Ref(animate, "rangedAttack", ranged);
        Transform dropPoint = draft.transform.Find("DropSpawnPoint");
        if (drop != null && dropPoint == null && addMissing) { GameObject p = new GameObject("DropSpawnPoint"); p.transform.SetParent(draft.transform, false); p.transform.localPosition = new Vector3(0f, .15f, 0f); dropPoint = p.transform; }
        Ref(drop, "dropSpawnPoint", dropPoint);
        Ref(popup, "characterHealth", health);
        EditorUtility.SetDirty(draft);
    }

    private void FitColliderToSprite()
    {
        if (draft == null || visualRenderer == null || visualRenderer.sprite == null) return;
        Vector2 size = visualRenderer.sprite.bounds.size;
        Vector3 s = visualRenderer.transform.localScale;
        size.x = Mathf.Max(.15f, Mathf.Abs(size.x * s.x) * .86f);
        size.y = Mathf.Max(.15f, Mathf.Abs(size.y * s.y) * .92f);
        CapsuleCollider2D capsule = draft.GetComponent<CapsuleCollider2D>();
        if (capsule != null) { Undo.RecordObject(capsule, "Fit enemy collider"); capsule.size = size; }
        BoxCollider2D box = draft.GetComponent<BoxCollider2D>();
        if (box != null) { Undo.RecordObject(box, "Fit enemy collider"); box.size = size; }
    }

    private void RefreshList()
    {
        listDirty = false;
        prefabPaths.Clear();
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) continue;
            if (path.Contains("/CharacterStudio2D/Backups/")) continue;
            if (showOnlyEnemyPrefabs)
            {
                GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null || !IsKnownCharacterPrefab(prefab)) continue;
            }
            prefabPaths.Add(path);
        }
        prefabPaths.Sort(StringComparer.OrdinalIgnoreCase);
    }

    private void LoadExisting(string path)
    {
        if (editingExisting && loadedPath == path) return;
        if (draft != null && !EditorUtility.DisplayDialog("Prefabを切り替え", "保存していない変更がある場合は破棄されます。読み込みますか？", "読み込む", "キャンセル")) return;
        DiscardDraft();
        try
        {
            draft = PrefabUtility.LoadPrefabContents(path);
            editingExisting = true;
            loadedPath = path;
            newName = Path.GetFileNameWithoutExtension(path);
            visualRenderer = draft.GetComponentInChildren<SpriteRenderer>(true);
            settingsScroll = Vector2.zero;
        }
        catch (Exception e)
        {
            Debug.LogException(e); EditorUtility.DisplayDialog("読込エラー", e.Message, "OK");
            CreateNewDraft(false);
        }
    }

    private void SaveNewPrefab()
    {
        if (draft == null) return;
        string name = SanitizeName(newName);
        if (string.IsNullOrWhiteSpace(name)) { EditorUtility.DisplayDialog("名前が必要", "Prefab名を入力してください。", "OK"); return; }
        if (!EnsureFolder(prefabFolder)) return;
        RepairReferences();
        if (!ValidateForSave()) return;
        if (visualRenderer == null || visualRenderer.sprite == null)
        {
            if (!EditorUtility.DisplayDialog("Spriteが未設定", "Spriteが設定されていません。外観なしのPrefabを作成しますか？", "作成", "キャンセル")) return;
        }
        MissionTargetIdentity2D mission = draft.GetComponent<MissionTargetIdentity2D>();
        if (mission != null)
        {
            SerializedObject m = new SerializedObject(mission);
            SerializedProperty id = m.FindProperty("targetId");
            if (id != null && (id.stringValue == "target_id" || id.stringValue == "enemy_new"))
            {
                id.stringValue = "enemy_" + name.ToLowerInvariant().Replace(' ', '_');
                m.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        string path = AssetDatabase.GenerateUniqueAssetPath(prefabFolder.TrimEnd('/') + "/" + name + ".prefab");
        try
        {
            GameObject instance = Instantiate(draft);
            instance.name = name;
            ClearHideFlags(instance);
            bool success;
            try
            {
                GameObject result = PrefabUtility.SaveAsPrefabAsset(instance, path, out success);
                if (!success || result == null) throw new IOException("Prefabの保存に失敗しました。Consoleも確認してください。");
            }
            finally { DestroyImmediate(instance); }
            AssetDatabase.SaveAssets();
            listDirty = true;
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            EditorGUIUtility.PingObject(Selection.activeObject);
            EditorUtility.DisplayDialog("Prefabを作成しました", path, "OK");
            LoadExistingWithoutConfirm(path);
        }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("作成失敗", e.Message, "OK"); }
    }

    private void SaveExistingPrefab()
    {
        if (!editingExisting || string.IsNullOrEmpty(loadedPath) || draft == null) return;
        if (!EditorUtility.DisplayDialog("Prefab保存", "次のPrefabを上書き保存します。\n" + loadedPath, "保存する", "キャンセル")) return;
        try
        {
            RepairReferences();
            if (!ValidateForSave()) return;
            BackupExistingPrefab(loadedPath);
            bool success;
            GameObject asset = PrefabUtility.SaveAsPrefabAsset(draft, loadedPath, out success);
            if (!success || asset == null) throw new IOException("Prefabを保存できませんでした。");
            AssetDatabase.SaveAssets();
            Selection.activeObject = asset;
            listDirty = true;
            EditorUtility.DisplayDialog("保存完了", "Prefabを更新しました。", "OK");
        }
        catch (Exception e) { Debug.LogException(e); EditorUtility.DisplayDialog("保存失敗", e.Message, "OK"); }
    }

    private void MoveExistingToTrash()
    {
        if (!editingExisting || string.IsNullOrEmpty(loadedPath)) return;
        string oldPath = loadedPath;
        if (!EditorUtility.DisplayDialog("Character Prefabの削除",
            "次のPrefabをゴミ箱へ移動します。\n" + oldPath +
            "\n\nシーンや他のPrefabから参照されている場合は参照切れになります。", "ゴミ箱へ移動", "キャンセル")) return;
        // The asset is not loaded from a scene, so unload isolated editing contents first.
        DiscardDraft();
        if (!AssetDatabase.MoveAssetToTrash(oldPath))
            EditorUtility.DisplayDialog("削除できませんでした", oldPath, "OK");
        CreateNewDraft(false);
        listDirty = true;
    }

    private void DuplicateSelectedAsNew()
    {
        if (draft == null) return;
        if (!editingExisting) return;
        GameObject clone = Instantiate(draft);
        clone.name = "CharacterDraft_EditorOnly";
        DiscardDraft();
        draft = clone;
        draft.hideFlags = HideFlags.HideAndDontSave;
        editingExisting = false;
        loadedPath = null;
        newName += "_Copy";
        AssignFreshNpcIdIfPresent();
        visualRenderer = draft.GetComponentInChildren<SpriteRenderer>(true);
        RepairReferences();
    }

    private void LoadExistingWithoutConfirm(string path)
    {
        DiscardDraft();
        try
        {
            draft = PrefabUtility.LoadPrefabContents(path);
            editingExisting = true;
            loadedPath = path;
            newName = Path.GetFileNameWithoutExtension(path);
            visualRenderer = draft.GetComponentInChildren<SpriteRenderer>(true);
        }
        catch (Exception e) { Debug.LogException(e); CreateNewDraft(false); }
    }

    private void DiscardDraft()
    {
        if (draft == null) return;
        if (editingExisting) PrefabUtility.UnloadPrefabContents(draft);
        else DestroyImmediate(draft);
        draft = null;
        visualRenderer = null;
        editingExisting = false;
        loadedPath = null;
    }

    private void ChooseFolder()
    {
        string baseDir = Directory.GetParent(Application.dataPath).FullName;
        string picked = EditorUtility.OpenFolderPanel("Character Prefabの保存先", baseDir, "");
        if (string.IsNullOrEmpty(picked)) return;
        picked = picked.Replace('\\', '/');
        baseDir = baseDir.Replace('\\', '/').TrimEnd('/');
        if (!picked.StartsWith(baseDir + "/Assets", StringComparison.OrdinalIgnoreCase))
        {
            EditorUtility.DisplayDialog("保存先", "プロジェクト内のAssetsフォルダを選択してください。", "OK");
            return;
        }
        prefabFolder = picked.Substring(baseDir.Length + 1);
    }

    private static bool EnsureFolder(string path)
    {
        path = path.TrimEnd('/').Replace('\\', '/');
        if (!path.StartsWith("Assets/", StringComparison.Ordinal) && path != "Assets")
        {
            EditorUtility.DisplayDialog("保存先エラー", "保存先は Assets/ から始まる必要があります。", "OK");
            return false;
        }
        string[] parts = path.Split('/');
        string current = "Assets";
        for (int i = 1; i < parts.Length; i++)
        {
            if (parts[i] == "" || parts[i] == "." || parts[i] == "..") return false;
            string next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
        return true;
    }

    private static string SanitizeName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        foreach (char c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name.Replace('/', '_').Replace('\\', '_').Trim();
    }

    private static void ClearHideFlags(GameObject root)
    {
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            t.gameObject.hideFlags = HideFlags.None;
            foreach (Component component in t.GetComponents<Component>())
                if (component != null) component.hideFlags = HideFlags.None;
        }
    }
}
#endif
