#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>既存のコードは上書きせず、必要な部分だけをバックアップ付きで更新する。</summary>
public static class AlcoholSystemIntegrationPatcher
{
    private const string InvMarkerV1 = "// [AlcoholSystem v1.0 / InventoryController]";
    private const string InvMarkerV2 = "// [AlcoholSystem v2.0 / InventoryController]";
    private const string WeightMarker = "// [AlcoholSystem v2.0 / WeightBuff]";
    private const string FoodMarker = "// [AlcoholSystem v2.0 / HungerDebuff]";

    [MenuItem("Tools/Alcohol/お酒システム v4 を接続")]
    public static void Apply()
    {
        string invPath = FindUniqueScript("InventoryController.cs");
        string weightPath = FindUniqueScript("PlayerWeightController.cs");
        string survivalPath = FindUniqueScript("PlayerSurvivalController.cs");
        if (invPath == null || weightPath == null || survivalPath == null)
        {
            EditorUtility.DisplayDialog("お酒システム v4", "必要なスクリプトが見つからないため中止しました。Consoleを確認してください。", "OK");
            return;
        }

        string oldInventory = File.ReadAllText(invPath);
        string oldWeight = File.ReadAllText(weightPath);
        string oldSurvival = File.ReadAllText(survivalPath);
        string newInventory = oldInventory, newWeight = oldWeight, newSurvival = oldSurvival;
        string error;
        if (!PatchInventory(ref newInventory, out error) || !PatchWeight(ref newWeight, out error) ||
            !PatchSurvival(ref newSurvival, out error))
        {
            Debug.LogError("[AlcoholPatcher v4] 安全のため何も変更しませんでした: " + error);
            EditorUtility.DisplayDialog("お酒システム v4", "コードが想定と異なったため中止。\n" + error, "OK");
            return;
        }

        string root = Directory.GetParent(Application.dataPath).FullName;
        string backupDir = Path.Combine(root, "AlcoholPatcherBackups", DateTime.Now.ToString("yyyyMMdd_HHmmss_fff"));
        bool changed = oldInventory != newInventory || oldWeight != newWeight || oldSurvival != newSurvival;
        if (changed)
        {
            Directory.CreateDirectory(backupDir);
            BackupAndWrite(invPath, oldInventory, newInventory, backupDir);
            BackupAndWrite(weightPath, oldWeight, newWeight, backupDir);
            BackupAndWrite(survivalPath, oldSurvival, newSurvival, backupDir);
            Debug.Log("[AlcoholPatcher v4] Inventory・重量・空腹処理を更新 / バックアップ=" + backupDir);
        }
        else Debug.Log("[AlcoholPatcher v4] 既にコード接続済み（変更なし）");

        GenerateStatusEffectAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("お酒システム v4", "接続完了。PlayerのPlayerAlcoholEffect2DとStatusEffectManager2Dを確認してください。右上は通常1アイコン＋飲みすぎ中のみ専用デバフを表示します。\n" +
            (changed ? "バックアップ=" + backupDir : "コードは接続済み"), "OK");
    }

    private static void BackupAndWrite(string path, string original, string updated, string backupDir)
    {
        if (original == updated) return;
        File.Copy(path, Path.Combine(backupDir, Path.GetFileName(path) + ".txt"), true);
        File.WriteAllText(path, updated, new UTF8Encoding(false));
    }

    private static string FindUniqueScript(string file)
    {
        string[] matches = AssetDatabase.FindAssets("t:MonoScript")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(p => p.StartsWith("Assets/", StringComparison.Ordinal) &&
                        string.Equals(Path.GetFileName(p), file, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1) Debug.LogError("[AlcoholPatcher v4] " + file + " の候補数=" + matches.Length + " / " + string.Join(", ", matches));
        return matches.Length == 1 ? matches[0] : null;
    }

    private static bool PatchInventory(ref string source, out string error)
    {
        error = null;
        if (source.Contains(InvMarkerV1) || source.Contains(InvMarkerV2)) return true;
        const string anchor = "        bool needsStatusConditionController =";
        if (Count(source, anchor) != 1) { error = "InventoryControllerの使用分岐が見つかりません。"; return false; }
        const string block =
@"        // [AlcoholSystem v2.0 / InventoryController]
        if (consumableData is AlcoholItemData alcoholItem)
        {
            PlayerAlcoholEffect2D effect = playerHealth.GetComponent<PlayerAlcoholEffect2D>();
            if (effect == null) effect = playerHealth.gameObject.AddComponent<PlayerAlcoholEffect2D>();
            if (!effect.TryDrink(alcoholItem, out string drinkFailure))
            {
                Debug.LogWarning(""[Alcohol] 飲酒失敗: "" + drinkFailure, this);
                result = ItemUseResult.NoUsableEffect;
                return false;
            }
            if (alcoholItem.ConsumeOnUse) RemoveItemAmount(item, 1);
            ApplyConsumableUseSlowdown(alcoholItem);
            result = ItemUseResult.Success;
            return true;
        }

";
        source = source.Replace(anchor, block + anchor);
        return true;
    }

    private static bool PatchWeight(ref string source, out string error)
    {
        error = null;
        if (source.Contains(WeightMarker)) return true;
        const string weightProperty = "verySlowWeightLimit + CurrentSkillWeightLimitBonusKg;";
        const string bonusVar = "float bonus = CurrentSkillWeightLimitBonusKg;";
        const string methodAnchor = "    // ConsumableItemData の Use Duration の間だけ、";
        const string carryStart = "    public float CurrentCarryLoadMoveSpeedMultiplier";
        const string carryEnd = "    // 回復中の合計時間";
        if (Count(source, weightProperty)!=1 || Count(source, bonusVar)!=1 || Count(source, methodAnchor)!=1 ||
            Count(source, carryStart)!=1 || Count(source, carryEnd)!=1)
        { error = "PlayerWeightControllerの重量・運搬アンカーが一致しません。"; return false; }

        int start = source.IndexOf(carryStart, StringComparison.Ordinal);
        int end = source.IndexOf(carryEnd, start, StringComparison.Ordinal);
        if (end <= start) { error = "運搬速度のブロックを認識できません。"; return false; }
        string block = source.Substring(start, end - start);
        if (Count(block, "return rawMultiplier;") != 1 ||
            Count(block, "return Mathf.Clamp01(1f - reducedPenalty);") != 1)
        { error = "NPC運搬速度の返却式が想定と異なります。"; return false; }
        block = block.Replace("return rawMultiplier;", "return ApplyAlcoholCarryBonus(rawMultiplier);")
            .Replace("return Mathf.Clamp01(1f - reducedPenalty);", "return ApplyAlcoholCarryBonus(Mathf.Clamp01(1f - reducedPenalty));");
        source = source.Substring(0, start) + block + source.Substring(end);
        source = source.Replace(weightProperty, "verySlowWeightLimit + CurrentSkillWeightLimitBonusKg + GetAlcoholWeightBonusKg();");
        source = source.Replace(bonusVar, "float bonus = CurrentSkillWeightLimitBonusKg + GetAlcoholWeightBonusKg();");
        const string methods =
@"    // [AlcoholSystem v2.0 / WeightBuff]
    private float GetAlcoholWeightBonusKg()
    {
        PlayerAlcoholEffect2D effect = GetComponent<PlayerAlcoholEffect2D>();
        return effect != null ? effect.CurrentWeightBonusKg : 0f;
    }

    private float ApplyAlcoholCarryBonus(float baseMultiplier)
    {
        if (!isCarryingExternalLoad || !SkillCardEffectUtility.IsCarryingRescuePerson())
            return baseMultiplier;
        PlayerAlcoholEffect2D effect = GetComponent<PlayerAlcoholEffect2D>();
        float bonus = effect != null ? effect.CurrentRescueCarrySpeedMultiplier : 1f;
        // 運搬の減速を少し緩和。通常速度以上へ加速はしない。
        return Mathf.Clamp01(baseMultiplier * bonus);
    }

";
        source = source.Replace(methodAnchor, methods + methodAnchor);
        return true;
    }

    private static bool PatchSurvival(ref string source, out string error)
    {
        error = null;
        if (source.Contains(FoodMarker)) return true;
        const string anchor = "SkillEffectType.FoodDrain\n            );";
        const string crlfAnchor = "SkillEffectType.FoodDrain\r\n            );";
        string actual = source.Contains(crlfAnchor) ? crlfAnchor : anchor;
        if (Count(source, actual) != 1) { error = "PlayerSurvivalControllerの食料減少式を見つけられません。"; return false; }
        const string insert =
@"    // [AlcoholSystem v2.0 / HungerDebuff]
    private float GetAlcoholFoodDrainMultiplier()
    {
        PlayerAlcoholEffect2D effect = GetComponent<PlayerAlcoholEffect2D>();
        return effect != null ? effect.CurrentFoodDrainMultiplier : 1f;
    }

";
        const string methodAnchor = "    // 食料を回復し、実際に回復した量を返す";
        if (Count(source, methodAnchor)!=1) { error = "空腹補正メソッドの挿入位置が見つかりません。"; return false; }
        source = source.Replace(actual, actual.Replace(");", ") * GetAlcoholFoodDrainMultiplier();"));
        source = source.Replace(methodAnchor, insert + methodAnchor);
        return true;
    }

    private static int Count(string source, string text)
    {
        int count=0, idx=0;
        while ((idx=source.IndexOf(text,idx,StringComparison.Ordinal))>=0) { count++; idx+=text.Length; }
        return count;
    }

    [MenuItem("Tools/Alcohol/v4 飲酒・飲みすぎHUD Dataを生成")]
    public static void GenerateStatusEffectAssets()
    {
        EnsureFolders("Assets/Resources/AlcoholEffects/Icons");
        // v2の元SOは削除しない。統合飲酒1つ、飲み過ぎのデバフ1つを使う。
        // TooltipはPlayerAlcoholEffect2Dが実行時に各効果の詳細を書き換える。
        CreateEffect("AlcoholCombinedEffect", "飲酒（バフ・デバフ）",
            "【バフ】\n重量制限 +5kg / NPC運搬速度 ×1.15\n【デバフ】\n空腹速度 ×1.25\n【飲み過ぎ】\n3杯目で60秒間ゆっくり画面が揺れる",
            StatusEffectCategory2D.Buff, 3);
        // 旧v2のものが存在するならそのまま利用、未作成なら新規生成する。
        CreateEffect("AlcoholOverdoseDebuff", "飲みすぎ", "飲み過ぎで60秒間ゆっくり大きく画面が揺れる",
            StatusEffectCategory2D.Debuff, 2);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[AlcoholPatcher v4] Assets/Resources/AlcoholEffects に右上HUD用の通常飲酒1アイコンと飲み過ぎデバフ1アイコンを用意しました。");
    }

    private static void EnsureFolders(string fullPath)
    {
        string[] parts = fullPath.Split('/');
        string parent=parts[0];
        for(int i=1;i<parts.Length;i++)
        {
            string next=parent+"/"+parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(parent, parts[i]);
            parent=next;
        }
    }

    private static void CreateEffect(string name, string title, string desc, StatusEffectCategory2D category, int style)
    {
        string folder="Assets/Resources/AlcoholEffects/";
        string path=folder+name+".asset";
        StatusEffectData2D effect=AssetDatabase.LoadAssetAtPath<StatusEffectData2D>(path);
        if(effect!=null) return; // ユーザーが変更した既存Dataは上書きしない
        Sprite icon=CreateIcon(name, style);
        effect=ScriptableObject.CreateInstance<StatusEffectData2D>();
        SerializedObject so=new SerializedObject(effect);
        so.FindProperty("displayName").stringValue=title;
        so.FindProperty("description").stringValue=desc;
        so.FindProperty("icon").objectReferenceValue=icon;
        so.FindProperty("category").enumValueIndex=(int)category;
        so.FindProperty("durationSeconds").floatValue=0f;
        so.FindProperty("linkedCondition").intValue=0;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(effect,path);
    }

    private static Sprite CreateIcon(string name, int style)
    {
        string path="Assets/Resources/AlcoholEffects/Icons/"+name+".png";
        Sprite existing=AssetDatabase.LoadAssetAtPath<Sprite>(path);
        if(existing!=null)return existing;
        const int size=64;
        Texture2D tex=new Texture2D(size,size,TextureFormat.RGBA32,false);
        Color background = style == 3 ? new Color(.56f,.32f,.12f,1f) :
            style==0 ? new Color(.11f,.55f,.33f,1f) :
            style==1 ? new Color(.73f,.38f,.12f,1f) : new Color(.64f,.18f,.29f,1f);
        for(int y=0;y<size;y++) for(int x=0;x<size;x++)
        {
            float dx=x-31.5f,dy=y-31.5f;
            float radius=Mathf.Sqrt(dx*dx+dy*dy);
            // style=3: 透明部分を持つビールジョッキ風の簡易アイコン。
            bool foreground = style == 3
                ? ((dx > -17 && dx < 9 && dy > -20 && dy < 14 &&
                    (Mathf.Abs(dx + 17) < 3 || Mathf.Abs(dx - 9) < 3 || dy < -16 || dy > 11)) ||
                   (dx > 9 && dx < 24 && dy > -9 && dy < 10 &&
                    (Mathf.Abs(dx - 23) < 3 || dy < -6 || dy > 7)) ||
                   (dy > 12 && dy < 20 && dx > -21 && dx < 12))
                : style==0 ?
                   (Mathf.Abs(dy)<3 && Mathf.Abs(dx)<22) ||
                   (Mathf.Abs(dx)>14 && Mathf.Abs(dx)<21 && Mathf.Abs(dy)<11)
                : style==1 ?
                   (Mathf.Abs(dx)<4 && dy>-22 && dy<12) ||
                   (dy>8 && dy<13 && Mathf.Abs(dx)<16)
                : ((Mathf.Abs(dx-13)<4 || Mathf.Abs(dx+13)<4) && dy<12 && dy>-12) ||
                   (dy<-9 && dy>-17 && Mathf.Abs(dx)<12);
            tex.SetPixel(x,y,radius>30 ? Color.clear : foreground ? Color.white : background);
        }
        tex.Apply();
        File.WriteAllBytes(path,tex.EncodeToPNG());
        UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);
        TextureImporter imp=AssetImporter.GetAtPath(path) as TextureImporter;
        if(imp!=null){imp.textureType=TextureImporterType.Sprite;imp.alphaIsTransparency=true;imp.SaveAndReimport();}
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
}
#endif
