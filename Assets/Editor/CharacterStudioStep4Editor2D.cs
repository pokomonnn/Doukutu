#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>Character Studio 2D - STEP4 性格・士気。</summary>
public sealed partial class CharacterStudioWindow2D
{
    private void DrawCharacterAIStep4Settings()
    {
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("STEP4  性格・士気・戦闘スタイル", EditorStyles.boldLabel);
        CharacterAIPersonality2D personality = draft.GetComponent<CharacterAIPersonality2D>();
        bool current = personality != null && personality.enabled;
        EditorGUI.BeginChangeCheck();
        bool want = EditorGUILayout.ToggleLeft("性格・士気AIを使用（STEP4）", current, EditorStyles.boldLabel);
        if (EditorGUI.EndChangeCheck())
        {
            if (want && personality == null)
                personality = draft.AddComponent<CharacterAIPersonality2D>();
            if (personality != null)
            {
                Undo.RecordObject(personality, "STEP4 Personality toggle");
                personality.enabled = want;
            }
        }
        if (personality == null || !personality.enabled)
        {
            EditorGUILayout.HelpBox("OFFならSTEP1～3と派閥戦闘は従来どおりに動きます。", MessageType.None);
            return;
        }

        DrawStep4PersonalityJapanesePopup(personality);
        EditorGUILayout.HelpBox(
            "性格を選んだ後に下の『プリセットを適用』を押すと、戦闘速度・逃走ライン・" +
            "仲間への警報などを初期設定します。既存値を書き換えるため任意操作です。", MessageType.Info);
        if (GUILayout.Button("選択した性格のプリセットを適用（既存AI設定も調整）", GUILayout.Height(28)))
        {
            ApplyStep4PersonalityPreset(personality);
        }

        DrawFields(personality, "攻撃と戦闘中の移動倍率", "combatMoveMultiplier", "attackIntervalMultiplier");
        DrawFields(personality, "周囲の仲間・敵の数による士気", "enableGroupMorale",
            "baseFleeRatioAdjustment", "outnumberedFleeBonus", "allyConfidenceBonus",
            "moraleScanRadius", "moraleTargetLayers", "moraleScanInterval");
        DrawFields(personality, "確認・デバッグ", "drawMoraleRange", "logMoraleChanges");
        CharacterAIFearFlee2D fear = draft.GetComponent<CharacterAIFearFlee2D>();
        if (fear == null || !fear.enabled)
        {
            EditorGUILayout.HelpBox("HP低下での士気退却にはSTEP3の『逃走・恐怖AI』が必要です。" +
                "ただし戦闘速度・攻撃間隔の性格効果は単独でも使えます。", MessageType.Warning);
            if (GUILayout.Button("STEP3 逃走・恐怖AIを追加"))
            {
                if (fear == null) fear = draft.AddComponent<CharacterAIFearFlee2D>();
                fear.enabled = true;
            }
        }
        EditorGUILayout.HelpBox("士気はSTEP3の Flee When Low Health をONにした場合のみ、" +
            "HP逃走ラインを補正します。敵対判定・索敵相手・運搬・弾の当たり判定は変更しません。", MessageType.None);
    }

    // 性格の内部 enum 値と並び順は変更せず、Character Studio の表示だけ日本語化します。
    private static readonly string[] Step4PersonalityJapaneseLabels =
    {
        "標準", "攻撃的", "勇敢", "慎重", "臆病", "仲間思い"
    };

    private static void DrawStep4PersonalityJapanesePopup(CharacterAIPersonality2D personality)
    {
        EditorGUILayout.LabelField("性格の種類", EditorStyles.boldLabel);
        SerializedObject so = new SerializedObject(personality);
        so.Update();
        SerializedProperty property = so.FindProperty("personalityType");
        if (property == null || property.propertyType != SerializedPropertyType.Enum)
        {
            EditorGUILayout.HelpBox("性格の設定が見つかりません。", MessageType.Warning);
            return;
        }

        int current = Mathf.Clamp(property.enumValueIndex, 0, Step4PersonalityJapaneseLabels.Length - 1);
        EditorGUI.BeginChangeCheck();
        int selected = EditorGUILayout.Popup("性格", current, Step4PersonalityJapaneseLabels);
        if (EditorGUI.EndChangeCheck())
        {
            property.enumValueIndex = selected;
            so.ApplyModifiedProperties(); // Unity の Undo に対応
            EditorUtility.SetDirty(personality);
        }
    }

    private static void Step4SetValue(Component component, string key, System.Action<SerializedProperty> set)
    {
        if (component == null) return;
        SerializedObject so = new SerializedObject(component);
        SerializedProperty prop = so.FindProperty(key);
        if (prop == null) return;
        Undo.RecordObject(component, "STEP4 Personality preset");
        set(prop);
        so.ApplyModifiedProperties();
        EditorUtility.SetDirty(component);
    }
    private static void Step4Float(Component c, string key, float value)
        => Step4SetValue(c, key, p => p.floatValue = value);
    private static void Step4Bool(Component c, string key, bool value)
        => Step4SetValue(c, key, p => p.boolValue = value);

    private void ApplyStep4PersonalityPreset(CharacterAIPersonality2D personality)
    {
        var kind = personality.PersonalityType;
        float move = 1f, cooldown = 1f, baseFlee = 0f;
        float outnumbered = 0.10f, confidence = 0.06f;
        float fleeAt = 0.25f, recoverAt = 0.5f;
        float alertRange = 10f, repositionChance = 0.35f;
        bool fleeOnLowHp = true;
        switch (kind)
        {
            case CharacterAIPersonalityType2D.Aggressive:
                move = 1.2f; cooldown = 0.78f; baseFlee = -0.08f;
                fleeAt = 0.20f; recoverAt = 0.45f; outnumbered = 0.05f;
                alertRange = 11f; repositionChance = 0.55f;
                break;
            case CharacterAIPersonalityType2D.Brave:
                move = 1.05f; cooldown = 0.92f; baseFlee = -0.12f;
                fleeAt = 0.12f; recoverAt = 0.36f; outnumbered = 0.03f;
                confidence = 0.10f; alertRange = 12f;
                break;
            case CharacterAIPersonalityType2D.Cautious:
                move = 0.9f; cooldown = 1.08f; baseFlee = 0.08f;
                fleeAt = 0.35f; recoverAt = 0.60f; outnumbered = 0.15f;
                alertRange = 9f; repositionChance = 0.60f;
                break;
            case CharacterAIPersonalityType2D.Cowardly:
                move = 0.9f; cooldown = 1.18f; baseFlee = 0.18f;
                fleeAt = 0.47f; recoverAt = 0.70f; outnumbered = 0.22f;
                alertRange = 7f; repositionChance = 0.60f;
                break;
            case CharacterAIPersonalityType2D.Guardian:
                move = 1.05f; cooldown = 0.95f; baseFlee = -0.06f;
                fleeAt = 0.25f; recoverAt = 0.50f; outnumbered = 0.06f;
                confidence = 0.12f; alertRange = 18f;
                break;
        }
        Step4Float(personality, "combatMoveMultiplier", move);
        Step4Float(personality, "attackIntervalMultiplier", cooldown);
        Step4Float(personality, "baseFleeRatioAdjustment", baseFlee);
        Step4Float(personality, "outnumberedFleeBonus", outnumbered);
        Step4Float(personality, "allyConfidenceBonus", confidence);
        Step4Bool(personality, "enableGroupMorale", true);

        CharacterAIFearFlee2D fear = draft.GetComponent<CharacterAIFearFlee2D>();
        if (fear != null)
        {
            Step4Bool(fear, "fleeWhenLowHealth", fleeOnLowHp);
            Step4Float(fear, "fleeHealthRatio", fleeAt);
            Step4Float(fear, "stopFleeHealthRatio", recoverAt);
        }
        CharacterAIInvestigation2D investigation = draft.GetComponent<CharacterAIInvestigation2D>();
        if (investigation != null)
        {
            Step4Bool(investigation, "reportHostileToAllies", true);
            Step4Bool(investigation, "respondToAllyAlerts", true);
            Step4Float(investigation, "allyAlertRadius", alertRange);
        }
        CharacterAIRangedTactics2D tactics = draft.GetComponent<CharacterAIRangedTactics2D>();
        if (tactics != null)
        {
            Step4Bool(tactics, "allowSmallReposition", true);
            Step4Float(tactics, "repositionChance", repositionChance);
        }
        Debug.Log("[Character Studio STEP4] " + draft.name + " / " + kind +
            " の性格プリセットを作業用Prefabに適用しました。保存で確定します。", draft);
    }
}
#endif
