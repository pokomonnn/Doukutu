#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;

/// <summary>
/// STEP4でNPC StudioとEnemy Studioから使う設定値。
/// NPC Studioは編集中のフォームに保存し、Enemy Studioは仮Prefabへ直接適用する。
/// </summary>
[Serializable]
public sealed class FactionStudioStep4Settings2D
{
    public bool UseFaction;
    public string FactionId = FactionId2D.Adventurers;
    public FactionMonsterType2D MonsterType = FactionMonsterType2D.None;
    public bool RetaliateWhenAttacked = true;
    public bool EnableTargeting;
    public bool EnableGuard;
    public int TargetLayers = ~0;
    public float ScanInterval = 0.35f;
    public float RetaliationDuration = 9f;
    public float GuardDetectionDistance = 6f;
    public float GuardLoseDistance = 8f;
    public float GuardAttackDistance = 1.1f;
    public int GuardAttackDamage = 5;
    public float GuardAttackInterval = 1.5f;
    public bool GuardRequireLineOfSight;
    public int GuardObstacleLayers;
}

/// <summary>
/// STEP2のFactionMember2DとSTEP3のTargeting/Guardを、2つのStudioに共通実装する。
/// Existing components remain; "off" deactivates AI rather than deleting components.
/// </summary>
public static class FactionStudioStep4Panel2D
{
    private static bool enemyFold = true;
    private static bool npcFold = true;
    private static readonly string[] Labels =
        { "モンスター（スライム・スケルトン）", "盗賊", "冒険者", "ドワーフ", "カスタムID" };
    private static readonly string[] KnownIds =
        { FactionId2D.Monsters, FactionId2D.Bandits, FactionId2D.Adventurers, FactionId2D.Dwarves };

    /// <summary>NPC Studio: フォームを描画。Prefabへの反映はNPC側のSave時。</summary>
    public static void DrawNpcSettings(FactionStudioStep4Settings2D settings)
    {
        if (settings == null) return;
        npcFold = EditorGUILayout.Foldout(npcFold, "派閥・戦闘 (STEP4)", true, EditorStyles.foldoutHeader);
        if (!npcFold) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        try
        {
            EditorGUILayout.HelpBox("所属するだけなら『派閥を使用』だけON。自動索敵させるときはSTEP3の派閥索敵をONにしてください。", MessageType.None);
            DrawFields(settings, false, false);
            EditorGUILayout.HelpBox("NPC Studioで『既存Prefabに設定を保存』を押すまで、Prefabには反映されません。", MessageType.Info);
        }
        finally { EditorGUILayout.EndVertical(); }
    }

    /// <summary>Enemy Studio: 保存前のdraftに即時反映。元Prefabは保存ボタンまで無変更。</summary>
    public static void DrawEnemyDraft(GameObject draft)
    {
        if (draft == null) return;
        enemyFold = EditorGUILayout.Foldout(enemyFold, "派閥・敵対AI (STEP4)", true, EditorStyles.foldoutHeader);
        if (!enemyFold) return;
        EditorGUILayout.BeginVertical(EditorStyles.helpBox);
        try
        {
            FactionStudioStep4Settings2D settings = ReadFrom(draft);
            FactionMember2D assignedFaction = draft.GetComponent<FactionMember2D>();
            if (assignedFaction == null || string.IsNullOrEmpty(assignedFaction.FactionId))
            {
                // Enemy Studioの新規Enemyはモンスター派閥を初期選択にする。
                settings.FactionId = FactionId2D.Monsters;
                settings.MonsterType = FactionMonsterType2D.Other;
            }
            EditorGUI.BeginChangeCheck();
            DrawFields(settings, true, draft.GetComponent<EnemyChaser2D>() != null);
            if (EditorGUI.EndChangeCheck())
            {
                ApplyTo(draft, settings);
                EditorUtility.SetDirty(draft);
            }
            EnemyChaser2D chaser = draft.GetComponent<EnemyChaser2D>();
            if (chaser != null && settings.UseFaction)
                DrawChaserDistances(chaser);
            if (draft.GetComponent<EnemyRangedAttack2D>() != null &&
                draft.GetComponent<EnemyRangedAttack2D>().enabled)
                EditorGUILayout.HelpBox("EnemyRangedAttack2Dの飛び道具はSTEP3の派閥索敵に未連携です。別途射撃対象の連携が必要です。", MessageType.Warning);
        }
        finally { EditorGUILayout.EndVertical(); }
    }

    private static void DrawFields(FactionStudioStep4Settings2D settings, bool enemy, bool hasChaser)
    {
        settings.UseFaction = EditorGUILayout.ToggleLeft("派閥を設定する", settings.UseFaction);
        if (!settings.UseFaction)
        {
            EditorGUILayout.HelpBox("オフにすると所属IDを空にし、派閥索敵・Guardを無効化します（コンポーネントは残す）。", MessageType.None);
            return;
        }

        int selection = Array.FindIndex(KnownIds, id => FactionId2D.Same(id, settings.FactionId));
        if (selection < 0) selection = KnownIds.Length;
        int chosen = EditorGUILayout.Popup("所属派閥", selection, Labels);
        if (chosen != selection && chosen < KnownIds.Length) settings.FactionId = KnownIds[chosen];
        if (chosen == KnownIds.Length)
            settings.FactionId = EditorGUILayout.TextField("派閥ID", settings.FactionId);

        settings.FactionId = FactionId2D.Normalize(settings.FactionId);
        if (FactionId2D.Same(settings.FactionId, FactionId2D.Monsters))
        {
            settings.MonsterType = (FactionMonsterType2D)EditorGUILayout.EnumPopup("モンスターの種類", settings.MonsterType);
            if (settings.MonsterType == FactionMonsterType2D.Any)
                EditorGUILayout.HelpBox("Any はルール用の値。個体には Slime / Skeleton / Other を指定してね。", MessageType.Warning);
        }
        settings.RetaliateWhenAttacked = EditorGUILayout.Toggle("攻撃されたら反撃", settings.RetaliateWhenAttacked);
        EditorGUILayout.Space(4);
        settings.EnableTargeting = EditorGUILayout.ToggleLeft("派閥索敵 (FactionEnemyTargeting2D)", settings.EnableTargeting);
        if (settings.EnableTargeting)
        {
            settings.TargetLayers = DrawLayerMask("索敵対象レイヤー", settings.TargetLayers);
            settings.ScanInterval = EditorGUILayout.Slider("索敵間隔（秒）", settings.ScanInterval, 0.1f, 2f);
            settings.RetaliationDuration = EditorGUILayout.FloatField("反撃継続時間（秒）", settings.RetaliationDuration);
            settings.RetaliationDuration = Mathf.Max(0f, settings.RetaliationDuration);
        }
        if (!hasChaser)
        {
            settings.EnableGuard = EditorGUILayout.ToggleLeft("移動しないNPCの近接戦闘 (FactionGuardCombat2D)", settings.EnableGuard);
            if (settings.EnableGuard)
            {
                settings.EnableTargeting = true;
                settings.GuardDetectionDistance = Mathf.Max(0f, EditorGUILayout.FloatField("Guard索敵距離", settings.GuardDetectionDistance));
                settings.GuardLoseDistance = Mathf.Max(settings.GuardDetectionDistance, EditorGUILayout.FloatField("Guard追跡解除距離", settings.GuardLoseDistance));
                settings.GuardAttackDistance = Mathf.Max(0.1f, EditorGUILayout.FloatField("Guard攻撃距離", settings.GuardAttackDistance));
                settings.GuardAttackDamage = Mathf.Max(1, EditorGUILayout.IntField("Guard攻撃力", settings.GuardAttackDamage));
                settings.GuardAttackInterval = Mathf.Max(0.1f, EditorGUILayout.FloatField("Guard攻撃間隔", settings.GuardAttackInterval));
                settings.GuardRequireLineOfSight = EditorGUILayout.Toggle("Guard視線チェック", settings.GuardRequireLineOfSight);
                if (settings.GuardRequireLineOfSight)
                    settings.GuardObstacleLayers = DrawLayerMask("視線を遮るレイヤー", settings.GuardObstacleLayers);
                EditorGUILayout.HelpBox("Guardは移動しません。自分のCharacterHealthとCollider2D、相手のCollider2Dが必要。移動AI付きには使用しないでください。", MessageType.Info);
            }
        }
        else
        {
            if (settings.EnableGuard)
                EditorGUILayout.HelpBox("EnemyChaser2Dとの二重攻撃を避けるためGuardは無効化されます。", MessageType.Warning);
            EditorGUILayout.LabelField("戦闘", "EnemyChaser2Dの攻撃処理を使用");
        }

        FactionRules2D asset = AssetDatabase.LoadAssetAtPath<FactionRules2D>(
            "Assets/Resources/FactionRules/DefaultFactionRules2D.asset");
        if (asset == null)
            EditorGUILayout.HelpBox("STEP2のルールassetが見つかりません。STEP2エディターから作成してください（未作成時は内蔵ルールで判定）。", MessageType.Warning);
        if (GUILayout.Button("STEP2 派閥ルールエディターを開く"))
            FactionRulesStudioWindow2D.Open();
    }

    /// <summary>STEP3で問題になったAttack Distance / Stop Distanceの調整欄。</summary>
    private static void DrawChaserDistances(EnemyChaser2D chaser)
    {
        SerializedObject so = new SerializedObject(chaser);
        so.Update();
        SerializedProperty attack = so.FindProperty("attackDistance");
        SerializedProperty stop = so.FindProperty("stopDistance");
        if (attack == null || stop == null) return;
        EditorGUILayout.Space(5);
        EditorGUILayout.LabelField("攻撃距離・停止距離の確認", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(attack, new GUIContent("攻撃距離 (Attack Distance)"));
        EditorGUILayout.PropertyField(stop, new GUIContent("停止距離 (Stop Distance)"));
        if (stop.floatValue >= attack.floatValue)
            EditorGUILayout.HelpBox("停止距離が攻撃距離以上です。接近後に攻撃が届かない場合は、Attack Distanceを大きめにしてください。", MessageType.Warning);
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static int DrawLayerMask(string label, int actualMask)
    {
        string[] layers = InternalEditorUtility.layers;
        int displayMask = 0;
        for (int i = 0; i < layers.Length; i++)
        {
            int layer = LayerMask.NameToLayer(layers[i]);
            if (layer >= 0 && (actualMask & (1 << layer)) != 0) displayMask |= 1 << i;
        }
        displayMask = EditorGUILayout.MaskField(label, displayMask, layers);
        int converted = 0;
        for (int i = 0; i < layers.Length; i++)
            if ((displayMask & (1 << i)) != 0)
            {
                int layer = LayerMask.NameToLayer(layers[i]);
                if (layer >= 0) converted |= 1 << layer;
            }
        return converted;
    }

    public static FactionStudioStep4Settings2D ReadFrom(GameObject root)
    {
        FactionStudioStep4Settings2D result = new FactionStudioStep4Settings2D();
        ReadFrom(root, result);
        return result;
    }

    public static void ReadFrom(GameObject root, FactionStudioStep4Settings2D settings)
    {
        if (settings == null) return;
        FactionStudioStep4Settings2D defaults = new FactionStudioStep4Settings2D();
        Copy(defaults, settings);
        if (root == null) return;
        FactionMember2D member = root.GetComponent<FactionMember2D>();
        if (member != null)
        {
            settings.UseFaction = !string.IsNullOrEmpty(member.FactionId);
            if (settings.UseFaction) settings.FactionId = member.FactionId;
            settings.MonsterType = member.MonsterType;
            settings.RetaliateWhenAttacked = member.RetaliateWhenAttacked;
        }
        FactionEnemyTargeting2D targeting = root.GetComponent<FactionEnemyTargeting2D>();
        if (targeting != null)
        {
            settings.EnableTargeting = targeting.enabled;
            SerializedObject so = new SerializedObject(targeting);
            settings.TargetLayers = ReadInt(so, "targetLayers", settings.TargetLayers);
            settings.ScanInterval = ReadFloat(so, "scanInterval", settings.ScanInterval);
            settings.RetaliationDuration = ReadFloat(so, "retaliationDuration", settings.RetaliationDuration);
        }
        FactionGuardCombat2D guard = root.GetComponent<FactionGuardCombat2D>();
        if (guard != null)
        {
            settings.EnableGuard = guard.enabled;
            SerializedObject so = new SerializedObject(guard);
            settings.GuardDetectionDistance = ReadFloat(so, "detectionDistance", settings.GuardDetectionDistance);
            settings.GuardLoseDistance = ReadFloat(so, "loseDistance", settings.GuardLoseDistance);
            settings.GuardAttackDistance = ReadFloat(so, "attackDistance", settings.GuardAttackDistance);
            settings.GuardAttackDamage = ReadInt(so, "attackDamage", settings.GuardAttackDamage);
            settings.GuardAttackInterval = ReadFloat(so, "attackInterval", settings.GuardAttackInterval);
            SerializedProperty p = so.FindProperty("requireLineOfSight");
            if (p != null) settings.GuardRequireLineOfSight = p.boolValue;
            settings.GuardObstacleLayers = ReadInt(so, "obstacleLayers", settings.GuardObstacleLayers);
        }
    }

    public static void ApplyTo(GameObject root, FactionStudioStep4Settings2D settings)
    {
        if (root == null || settings == null) return;
        FactionMember2D member = root.GetComponent<FactionMember2D>();
        FactionEnemyTargeting2D targeting = root.GetComponent<FactionEnemyTargeting2D>();
        FactionGuardCombat2D guard = root.GetComponent<FactionGuardCombat2D>();

        if (!settings.UseFaction || string.IsNullOrWhiteSpace(settings.FactionId))
        {
            if (member != null) member.Configure(string.Empty);
            if (targeting != null) targeting.enabled = false;
            if (guard != null) guard.enabled = false;
            return;
        }

        if (member == null) member = root.AddComponent<FactionMember2D>();
        FactionMonsterType2D type = FactionId2D.Same(settings.FactionId, FactionId2D.Monsters)
            ? (settings.MonsterType == FactionMonsterType2D.Any ? FactionMonsterType2D.Other : settings.MonsterType)
            : FactionMonsterType2D.None;
        member.Configure(settings.FactionId, type);
        WriteBool(member, "retaliateWhenAttacked", settings.RetaliateWhenAttacked);

        bool enableTargeting = settings.EnableTargeting || settings.EnableGuard;
        if (enableTargeting && targeting == null) targeting = root.AddComponent<FactionEnemyTargeting2D>();
        if (targeting != null)
        {
            targeting.enabled = enableTargeting;
            if (enableTargeting)
            {
                WriteInt(targeting, "targetLayers", settings.TargetLayers);
                WriteFloat(targeting, "scanInterval", Mathf.Clamp(settings.ScanInterval, .1f, 2f));
                WriteFloat(targeting, "retaliationDuration", Mathf.Max(0f, settings.RetaliationDuration));
            }
        }

        bool hasChaser = root.GetComponent<EnemyChaser2D>() != null;
        bool enableGuard = settings.EnableGuard && !hasChaser;
        if (enableGuard && guard == null) guard = root.AddComponent<FactionGuardCombat2D>();
        if (guard != null)
        {
            guard.enabled = enableGuard;
            if (enableGuard)
            {
                WriteFloat(guard, "detectionDistance", Mathf.Max(0, settings.GuardDetectionDistance));
                WriteFloat(guard, "loseDistance", Mathf.Max(settings.GuardDetectionDistance, settings.GuardLoseDistance));
                WriteFloat(guard, "attackDistance", Mathf.Max(.1f, settings.GuardAttackDistance));
                WriteInt(guard, "attackDamage", Mathf.Max(1, settings.GuardAttackDamage));
                WriteFloat(guard, "attackInterval", Mathf.Max(.1f, settings.GuardAttackInterval));
                WriteBool(guard, "requireLineOfSight", settings.GuardRequireLineOfSight);
                WriteInt(guard, "obstacleLayers", settings.GuardObstacleLayers);
            }
        }
        if (enableTargeting && root.GetComponent<Collider2D>() == null &&
            root.GetComponentInChildren<Collider2D>() == null)
            Debug.LogWarning("[Faction STEP4] " + root.name + ": Collider2Dがありません。他NPCから索敵される場合はColliderを追加してください。", root);
    }

    private static void Copy(FactionStudioStep4Settings2D a, FactionStudioStep4Settings2D b)
    {
        b.UseFaction = a.UseFaction; b.FactionId = a.FactionId; b.MonsterType = a.MonsterType;
        b.RetaliateWhenAttacked = a.RetaliateWhenAttacked;
        b.EnableTargeting = a.EnableTargeting; b.EnableGuard = a.EnableGuard;
        b.TargetLayers = a.TargetLayers; b.ScanInterval = a.ScanInterval;
        b.RetaliationDuration = a.RetaliationDuration;
        b.GuardDetectionDistance = a.GuardDetectionDistance;
        b.GuardLoseDistance = a.GuardLoseDistance;
        b.GuardAttackDistance = a.GuardAttackDistance;
        b.GuardAttackDamage = a.GuardAttackDamage;
        b.GuardAttackInterval = a.GuardAttackInterval;
        b.GuardRequireLineOfSight = a.GuardRequireLineOfSight;
        b.GuardObstacleLayers = a.GuardObstacleLayers;
    }

    private static int ReadInt(SerializedObject so, string path, int fallback)
    {
        SerializedProperty p = so.FindProperty(path);
        return p == null ? fallback : p.intValue;
    }
    private static float ReadFloat(SerializedObject so, string path, float fallback)
    {
        SerializedProperty p = so.FindProperty(path);
        return p == null ? fallback : p.floatValue;
    }
    private static void WriteInt(UnityEngine.Object obj, string field, int value)
    {
        SerializedObject so = new SerializedObject(obj);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(obj.GetType().Name + "." + field + " が存在しません。");
        p.intValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void WriteFloat(UnityEngine.Object obj, string field, float value)
    {
        SerializedObject so = new SerializedObject(obj);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(obj.GetType().Name + "." + field + " が存在しません。");
        p.floatValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
    private static void WriteBool(UnityEngine.Object obj, string field, bool value)
    {
        SerializedObject so = new SerializedObject(obj);
        SerializedProperty p = so.FindProperty(field);
        if (p == null) throw new InvalidOperationException(obj.GetType().Name + "." + field + " が存在しません。");
        p.boolValue = value;
        so.ApplyModifiedPropertiesWithoutUndo();
    }
}
#endif
