#if UNITY_EDITOR
using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 近接武器のテスト用Prefab+ItemDataをまとめて作成するEditor補助。
/// ビジュアル用のSpriteは、作成後にSwingVisualへ設定してください。
/// </summary>
public static class MeleeWeaponTemplateEditor
{
    private const string Folder = "Assets/MeleeWeaponSamples";

    [MenuItem("Tools/Melee/ナイフ・剣・斧のサンプルを作成")]
    public static void CreateSamples()
    {
        if (!AssetDatabase.IsValidFolder(Folder))
            AssetDatabase.CreateFolder("Assets", "MeleeWeaponSamples");
        CreateWeapon("Knife", "ナイフ", 14, 1.35f, 0.28f, 0.38f, true, 0.10f, 80f);
        CreateWeapon("Sword", "剣", 25, 1.8f, 0.39f, 0.55f, false, 0.16f, 100f);
        CreateWeapon("Axe", "斧", 42, 1.65f, 0.60f, 0.90f, false, 0.27f, 105f);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog("近接武器サンプル", "Assets/MeleeWeaponSamples に3種類作成しました。\n各PrefabのSwingVisualにSpriteを設定し、ItemDataDatabaseへItemIdを登録してください。", "OK");
    }

    private static void CreateWeapon(string code, string name, int damage, float reach,
        float duration, float interval, bool carry, float hitDelay, float arc)
    {
        string prefabPath = Folder + "/" + code + "Weapon.prefab";
        string dataPath = Folder + "/" + code + "WeaponItem.asset";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath) != null ||
            AssetDatabase.LoadAssetAtPath<MeleeWeaponItemData>(dataPath) != null)
        {
            Debug.LogWarning("[MeleeSample] 既存ファイルを上書きしません: " + code);
            return;
        }

        var root = new GameObject(code + "Weapon");
        var aim = root.AddComponent<WeaponAim>();
        var melee = root.AddComponent<PlayerMeleeWeapon2D>();
        var source = root.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.spatialBlend = 0f;

        var visual = new GameObject("SwingVisual");
        visual.transform.SetParent(root.transform, false);
        var sprite = visual.AddComponent<SpriteRenderer>();
        visual.transform.localPosition = new Vector3(0.25f, 0f, 0f);

        var aimSo = new SerializedObject(aim);
        aimSo.FindProperty("gunSpriteRenderer").objectReferenceValue = sprite;
        aimSo.ApplyModifiedPropertiesWithoutUndo();

        var meleeSo = new SerializedObject(melee);
        meleeSo.FindProperty("swingVisual").objectReferenceValue = visual.transform;
        meleeSo.FindProperty("attackOrigin").objectReferenceValue = root.transform;
        meleeSo.FindProperty("audioSource").objectReferenceValue = source;
        meleeSo.FindProperty("showDebugLogs").boolValue = true;
        meleeSo.ApplyModifiedPropertiesWithoutUndo();

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        UnityEngine.Object.DestroyImmediate(root);

        var data = ScriptableObject.CreateInstance<MeleeWeaponItemData>();
        var so = new SerializedObject(data);
        so.FindProperty("itemId").stringValue = "melee_" + code.ToLowerInvariant();
        so.FindProperty("displayName").stringValue = name;
        so.FindProperty("description").stringValue = name + "：左クリックで斬撃できる近接武器。";
        so.FindProperty("width").intValue = code == "Axe" ? 2 : 1;
        so.FindProperty("height").intValue = code == "Knife" ? 2 : 3;
        so.FindProperty("maxStack").intValue = 1;
        so.FindProperty("weight").floatValue = code == "Axe" ? 3.5f : code == "Sword" ? 2f : 0.7f;
        so.FindProperty("weaponPrefab").objectReferenceValue = prefab;
        so.FindProperty("attackDamage").intValue = damage;
        so.FindProperty("attackReach").floatValue = reach;
        so.FindProperty("attackArcDegrees").floatValue = arc;
        so.FindProperty("swingDuration").floatValue = duration;
        so.FindProperty("attackInterval").floatValue = interval;
        so.FindProperty("hitStartDelay").floatValue = hitDelay;
        so.FindProperty("hitWindowSeconds").floatValue = 0.15f;
        so.FindProperty("usableWhileCarryingNpc").boolValue = carry;
        so.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(data, dataPath);
    }

    [MenuItem("Tools/Melee/装備時のGunShooter警告を抑制")]
    public static void PatchVisualLogger()
    {
        string[] guids = AssetDatabase.FindAssets("PlayerEquipmentVisualController t:MonoScript");
        string path = null;
        foreach (string guid in guids)
        {
            string candidate = AssetDatabase.GUIDToAssetPath(guid);
            if (Path.GetFileName(candidate) == "PlayerEquipmentVisualController.cs")
            {
                path = candidate;
                break;
            }
        }
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogWarning("[MeleePatch] PlayerEquipmentVisualController.cs が見つかりません。");
            return;
        }
        string full = Path.GetFullPath(path);
        string content = File.ReadAllText(full);
        if (content.Contains("if (!(weaponData is MeleeWeaponItemData)) Debug.LogWarning"))
        {
            Debug.Log("[MeleePatch] 既に対応済みです。");
            return;
        }
        string regex = @"if\s*\(currentGunShooter\s*==\s*null\)\s*\{\s*(?=Debug\.LogWarning\s*\()";
        if (!Regex.IsMatch(content, regex))
        {
            Debug.LogWarning("[MeleePatch] 想定したログ部分が見つからないため、何も変更しませんでした。手動でGunShooter未設定の警告だけを近接武器の場合は除外してください。");
            return;
        }
        string backup = full + ".beforeMelee.bak";
        if (!File.Exists(backup)) File.Copy(full, backup);
        // ifのelse構造は変更せず、Debug.LogWarningのみ条件付きにする。
        string modified = Regex.Replace(content, regex,
            "if (currentGunShooter == null)\n        {\n            if (!(weaponData is MeleeWeaponItemData)) ",
            RegexOptions.None, TimeSpan.FromSeconds(1));
        File.WriteAllText(full, modified, new UTF8Encoding(false));
        AssetDatabase.Refresh();
        Debug.Log("[MeleePatch] 近接武器PrefabにGunShooterがなくても誤警告を出さないようにしました。バックアップ: " + backup);
    }
}
#endif
