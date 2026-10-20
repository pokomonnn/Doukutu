#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 旧TownFacilityUpgradeData（例：WeaponShopUpgradeData）の設定を、
/// 統合後のMerchantShopDataへコピーするためのEditor移行ツールです。
///
/// Projectで
/// 1. 統合先MerchantShopData
/// 2. 旧TownFacilityUpgradeData
/// の2つをCtrl選択してメニューを実行してください。
/// </summary>
public static class MerchantShopUpgradeMigrationEditor
{
    private const string MenuPath =
        "Tools/Town/店舗データ統合/選択したUpgradeDataをMerchantShopDataへコピー";

    [MenuItem(MenuPath)]
    private static void MigrateSelectedAssets()
    {
        MerchantShopData targetShop = null;
        TownFacilityUpgradeData sourceUpgrade = null;

        foreach (Object selected in Selection.objects)
        {
            if (selected is MerchantShopData merchant && targetShop == null)
            {
                targetShop = merchant;
                continue;
            }

            if (selected is TownFacilityUpgradeData upgrade &&
                !(selected is MerchantShopData) &&
                sourceUpgrade == null)
            {
                sourceUpgrade = upgrade;
            }
        }

        if (targetShop == null || sourceUpgrade == null)
        {
            EditorUtility.DisplayDialog(
                "店舗データ統合",
                "Projectで次の2つを同時に選択してください。\n\n" +
                "・統合先 MerchantShopData\n" +
                "・旧 TownFacilityUpgradeData（WeaponShopUpgradeDataなど）",
                "OK"
            );
            return;
        }

        string targetPath = AssetDatabase.GetAssetPath(targetShop);
        string sourcePath = AssetDatabase.GetAssetPath(sourceUpgrade);

        bool proceed = EditorUtility.DisplayDialog(
            "店舗データ統合",
            "旧アップグレードデータをMerchantShopDataへコピーします。\n\n" +
            $"統合先: {targetPath}\n" +
            $"コピー元: {sourcePath}\n\n" +
            "コピーされる内容:\n" +
            "・Facility Id / Name\n" +
            "・Starting Level\n" +
            "・Upgrade Levels\n" +
            "・必要ミッション / 素材 / 解禁商品\n\n" +
            "MerchantShopData固有の購入設定は維持されます。",
            "コピーする",
            "キャンセル"
        );

        if (!proceed)
        {
            return;
        }

        Undo.RecordObject(targetShop, "Merge Facility Upgrade Into Merchant Shop");

        SerializedObject source = new SerializedObject(sourceUpgrade);
        SerializedObject target = new SerializedObject(targetShop);

        CopyProperty(source, target, "facilityId");
        CopyProperty(source, target, "facilityName");
        CopyProperty(source, target, "startingLevel");
        CopyProperty(source, target, "upgradeLevels");

        target.ApplyModifiedProperties();
        EditorUtility.SetDirty(targetShop);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        EditorUtility.DisplayDialog(
            "店舗データ統合",
            "コピーが完了しました。\n\n" +
            "MerchantShopDataのInspectorでLv設定を確認し、\n" +
            "会話のUpgrade Facility参照をこのMerchantShopDataへ変更してください。\n" +
            "動作確認後、旧WeaponShopUpgradeDataは削除できます。",
            "OK"
        );

        Selection.activeObject = targetShop;
        EditorGUIUtility.PingObject(targetShop);
    }

    private static void CopyProperty(
        SerializedObject source,
        SerializedObject target,
        string propertyName)
    {
        SerializedProperty sourceProperty =
            source.FindProperty(propertyName);

        SerializedProperty targetProperty =
            target.FindProperty(propertyName);

        if (sourceProperty == null || targetProperty == null)
        {
            Debug.LogWarning(
                $"[MerchantShopUpgradeMigrationEditor] " +
                $"Propertyが見つかりません: {propertyName}"
            );
            return;
        }

        switch (sourceProperty.propertyType)
        {
            case SerializedPropertyType.String:
                targetProperty.stringValue = sourceProperty.stringValue;
                break;

            case SerializedPropertyType.Integer:
                targetProperty.intValue = sourceProperty.intValue;
                break;

            default:
                // Upgrade Levelsのような配列・複合データはJSON経由ではなく
                // SerializedProperty単位でコピーします。
                target.CopyFromSerializedProperty(sourceProperty);
                break;
        }
    }
}
#endif
