#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// 旧MerchantStockInventory / ItemBoxInventoryに分散している設定を、
/// MerchantShopDataへ移すための補助Editorです。
/// </summary>
public static class MerchantShopDataMigrationEditor
{
    [MenuItem(
        "Tools/Town/Merchant/選択した商人設定をMerchantShopDataへ移行",
        true
    )]
    private static bool ValidateMigrateSelectedMerchant()
    {
        return GetSelectedMerchant() != null;
    }

    [MenuItem(
        "Tools/Town/Merchant/選択した商人設定をMerchantShopDataへ移行"
    )]
    private static void MigrateSelectedMerchant()
    {
        MerchantStockInventory merchant = GetSelectedMerchant();

        if (merchant == null)
        {
            EditorUtility.DisplayDialog(
                "商人設定移行",
                "MerchantStockInventoryが付いたGameObjectを選択してください。",
                "OK"
            );
            return;
        }

        MerchantShopData shopData = merchant.ShopData;
        ItemBoxInventory itemBox = merchant.StockInventory;

        if (shopData == null)
        {
            EditorUtility.DisplayDialog(
                "商人設定移行",
                "MerchantStockInventoryのShop DataにMerchantShopDataを設定してください。",
                "OK"
            );
            return;
        }

        Undo.RecordObject(shopData, "Migrate Merchant Shop Data");

        SerializedObject merchantSO = new SerializedObject(merchant);
        SerializedObject shopSO = new SerializedObject(shopData);

        merchantSO.Update();
        shopSO.Update();

        CopyBool(
            merchantSO,
            "allowAllSellItemTypes",
            shopSO,
            "allowAllSellItemTypes"
        );

        CopyEnumArray(
            merchantSO,
            "acceptedSellItemTypes",
            shopSO,
            "acceptedSellItemTypes"
        );

        CopyObjectArray(
            merchantSO,
            "acceptedSpecificItems",
            shopSO,
            "acceptedSpecificItems"
        );

        CopyObjectArray(
            merchantSO,
            "rejectedSpecificItems",
            shopSO,
            "rejectedSpecificItems"
        );

        CopyString(
            merchantSO,
            "rejectedItemMessageFormat",
            shopSO,
            "rejectedItemMessageFormat"
        );

        if (itemBox != null)
        {
            CopyItemBoxStartingItems(itemBox, shopSO);
        }

        shopSO.ApplyModifiedProperties();
        EditorUtility.SetDirty(shopData);
        AssetDatabase.SaveAssets();

        EditorUtility.DisplayDialog(
            "商人設定移行 完了",
            "旧MerchantStockInventoryの買取条件と、ItemBoxInventoryのStarting ItemsをMerchantShopDataへコピーしました。\n\n" +
            "コピー後はMerchantShopDataを確認してください。\n" +
            "ItemBoxInventoryのStarting Itemsは残していますが、新方式の商人では使用されません。",
            "OK"
        );
    }

    private static MerchantStockInventory GetSelectedMerchant()
    {
        if (Selection.activeGameObject == null)
        {
            return null;
        }

        return Selection.activeGameObject.GetComponent<MerchantStockInventory>();
    }

    private static void CopyBool(
        SerializedObject sourceSO,
        string sourceName,
        SerializedObject targetSO,
        string targetName)
    {
        SerializedProperty source = sourceSO.FindProperty(sourceName);
        SerializedProperty target = targetSO.FindProperty(targetName);

        if (source != null && target != null)
        {
            target.boolValue = source.boolValue;
        }
    }

    private static void CopyString(
        SerializedObject sourceSO,
        string sourceName,
        SerializedObject targetSO,
        string targetName)
    {
        SerializedProperty source = sourceSO.FindProperty(sourceName);
        SerializedProperty target = targetSO.FindProperty(targetName);

        if (source != null && target != null)
        {
            target.stringValue = source.stringValue;
        }
    }

    private static void CopyEnumArray(
        SerializedObject sourceSO,
        string sourceName,
        SerializedObject targetSO,
        string targetName)
    {
        SerializedProperty source = sourceSO.FindProperty(sourceName);
        SerializedProperty target = targetSO.FindProperty(targetName);

        if (source == null || target == null || !source.isArray || !target.isArray)
        {
            return;
        }

        target.arraySize = source.arraySize;

        for (int i = 0; i < source.arraySize; i++)
        {
            target.GetArrayElementAtIndex(i).enumValueIndex =
                source.GetArrayElementAtIndex(i).enumValueIndex;
        }
    }

    private static void CopyObjectArray(
        SerializedObject sourceSO,
        string sourceName,
        SerializedObject targetSO,
        string targetName)
    {
        SerializedProperty source = sourceSO.FindProperty(sourceName);
        SerializedProperty target = targetSO.FindProperty(targetName);

        if (source == null || target == null || !source.isArray || !target.isArray)
        {
            return;
        }

        target.arraySize = source.arraySize;

        for (int i = 0; i < source.arraySize; i++)
        {
            target.GetArrayElementAtIndex(i).objectReferenceValue =
                source.GetArrayElementAtIndex(i).objectReferenceValue;
        }
    }

    private static void CopyItemBoxStartingItems(
        ItemBoxInventory itemBox,
        SerializedObject shopSO)
    {
        SerializedObject itemBoxSO = new SerializedObject(itemBox);
        itemBoxSO.Update();

        SerializedProperty sourceItems =
            itemBoxSO.FindProperty("startingItems");
        SerializedProperty targetItems =
            shopSO.FindProperty("items");

        if (sourceItems == null ||
            targetItems == null ||
            !sourceItems.isArray ||
            !targetItems.isArray)
        {
            return;
        }

        for (int i = 0; i < sourceItems.arraySize; i++)
        {
            SerializedProperty sourceEntry =
                sourceItems.GetArrayElementAtIndex(i);

            ItemData itemData = sourceEntry
                .FindPropertyRelative("itemData")
                .objectReferenceValue as ItemData;

            int amount = Mathf.Max(
                1,
                sourceEntry
                    .FindPropertyRelative("amount")
                    .intValue
            );

            if (itemData == null)
            {
                continue;
            }

            int targetIndex = FindShopItemIndex(
                targetItems,
                itemData
            );

            if (targetIndex < 0)
            {
                targetIndex = targetItems.arraySize;
                targetItems.InsertArrayElementAtIndex(targetIndex);
            }

            SerializedProperty targetEntry =
                targetItems.GetArrayElementAtIndex(targetIndex);

            targetEntry
                .FindPropertyRelative("itemData")
                .objectReferenceValue = itemData;

            targetEntry
                .FindPropertyRelative("quantityPerPurchase")
                .intValue = 1;

            targetEntry
                .FindPropertyRelative("initialStock")
                .intValue = amount;
        }
    }

    private static int FindShopItemIndex(
        SerializedProperty items,
        ItemData itemData)
    {
        for (int i = 0; i < items.arraySize; i++)
        {
            SerializedProperty entry = items.GetArrayElementAtIndex(i);
            SerializedProperty itemProperty =
                entry.FindPropertyRelative("itemData");

            if (itemProperty != null &&
                itemProperty.objectReferenceValue == itemData)
            {
                return i;
            }
        }

        return -1;
    }
}
#endif
