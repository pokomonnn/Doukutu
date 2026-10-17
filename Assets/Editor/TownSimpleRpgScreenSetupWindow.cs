#if UNITY_EDITOR
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// 町に「すでにある」4画面の参照と配置を変えず、シンプルUIを一括適用。
/// 実際の売買/修理/自宅の操作は元のController/ボタンに任せる。
/// </summary>
public sealed class TownSimpleRpgScreenSetupWindow : EditorWindow
{
    private TMP_FontAsset japaneseTmpFont;
    private Font japaneseLegacyFont;
    private RectTransform rareShop;
    private RectTransform weaponShop;
    private RectTransform weaponRepair;
    private RectTransform homeStorage;
    private Vector2 scroll;

    [MenuItem("Tools/2D Shooter/町UI/4画面をシンプルUIに統一")]
    public static void ShowWindow()
    {
        GetWindow<TownSimpleRpgScreenSetupWindow>("町の4画面・シンプルUI").Show();
    }

    private void OnGUI()
    {
        scroll = EditorGUILayout.BeginScrollView(scroll);
        EditorGUILayout.LabelField("町の4画面を同じデザインへ", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "すでに存在する画面のPanel Rootを割り当てます。既存Hierarchyの配置、Button.OnClick、InventoryGridUI、EquipmentSlotUI、" +
            "商人の在庫やお金の処理は変更しません。Sceneをバックアップしてから実行してください。", MessageType.Info);

        rareShop = (RectTransform)EditorGUILayout.ObjectField("希少品の販売店", rareShop, typeof(RectTransform), true);
        weaponShop = (RectTransform)EditorGUILayout.ObjectField("武器商人", weaponShop, typeof(RectTransform), true);
        weaponRepair = (RectTransform)EditorGUILayout.ObjectField("武器の修理画面", weaponRepair, typeof(RectTransform), true);
        homeStorage = (RectTransform)EditorGUILayout.ObjectField("Playerの家のInventory", homeStorage, typeof(RectTransform), true);

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("日本語フォント（任意）", EditorStyles.boldLabel);
        japaneseTmpFont = (TMP_FontAsset)EditorGUILayout.ObjectField(
            "日本語 TMP Font Asset", japaneseTmpFont, typeof(TMP_FontAsset), false);
        japaneseLegacyFont = (Font)EditorGUILayout.ObjectField(
            "従来 UI.Text 用 Font（任意）", japaneseLegacyFont, typeof(Font), false);
        EditorGUILayout.HelpBox("未指定なら既存の文字フォントを維持します。日本語グリフ入りのTMP Font Assetを使ってください。", MessageType.None);
        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode))
        {
            DrawApplyButton("希少品の販売店だけ適用", rareShop, TownSimpleRpgSkin2D.TownScreenKind.RareGoodsShop);
            DrawApplyButton("武器商人だけ適用", weaponShop, TownSimpleRpgSkin2D.TownScreenKind.WeaponMerchant);
            DrawApplyButton("武器修理だけ適用", weaponRepair, TownSimpleRpgSkin2D.TownScreenKind.WeaponRepair);
            DrawApplyButton("自宅Inventoryだけ適用", homeStorage, TownSimpleRpgSkin2D.TownScreenKind.HomeStorage);
            EditorGUILayout.Space(10);
            using (new EditorGUI.DisabledScope(rareShop == null || weaponShop == null || weaponRepair == null || homeStorage == null))
            {
                if (GUILayout.Button("4画面すべてに適用", GUILayout.Height(44)))
                {
                    Apply(rareShop, TownSimpleRpgSkin2D.TownScreenKind.RareGoodsShop);
                    Apply(weaponShop, TownSimpleRpgSkin2D.TownScreenKind.WeaponMerchant);
                    Apply(weaponRepair, TownSimpleRpgSkin2D.TownScreenKind.WeaponRepair);
                    Apply(homeStorage, TownSimpleRpgSkin2D.TownScreenKind.HomeStorage);
                    EditorUtility.DisplayDialog("町UIの統一", "4画面にシンプルな配色を適用しました。Sceneを保存してください。", "OK");
                }
            }
        }
        EditorGUILayout.Space(10);
        using (new EditorGUI.DisabledScope(EditorApplication.isPlayingOrWillChangePlaymode ||
            (japaneseTmpFont == null && japaneseLegacyFont == null)))
        {
            if (GUILayout.Button("指定した画面のフォントだけ変更", GUILayout.Height(32)))
            {
                int count = 0;
                foreach (RectTransform root in new[] { rareShop, weaponShop, weaponRepair, homeStorage })
                {
                    if (root == null) continue;
                    TownSimpleRpgSkin2D skin = root.GetComponent<TownSimpleRpgSkin2D>();
                    if (skin != null)
                    {
                        Undo.RecordObject(skin, "日本語フォント設定");
                        skin.ConfigureJapaneseFonts(japaneseTmpFont, japaneseLegacyFont);
                        EditorUtility.SetDirty(skin);
                    }
                    count += TownSimpleRpgSkin2D.ApplyFontsToHierarchy(root, japaneseTmpFont, japaneseLegacyFont);
                    if (root.gameObject.scene.IsValid()) EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
                }
                EditorUtility.DisplayDialog("日本語フォント", $"反映した文字：{count}個。Sceneを保存してください。", "OK");
            }
        }
        EditorGUILayout.Space(8);
        EditorGUILayout.HelpBox(
            "注意：これは『既存の町画面を装飾・統一する』安全な先行版です。" +
            "出撃補充と同じ一体型購入・売却ロジックへの画面再構成、修理・自宅箱の自動配線は行いません。" +
            "その作業には現在使用中の各Controllerスクリプトが必要です。", MessageType.Warning);
        EditorGUILayout.EndScrollView();
    }

    private void DrawApplyButton(string label, RectTransform root, TownSimpleRpgSkin2D.TownScreenKind kind)
    {
        using (new EditorGUI.DisabledScope(root == null))
        {
            if (GUILayout.Button(label)) Apply(root, kind);
        }
    }

    private void Apply(RectTransform root, TownSimpleRpgSkin2D.TownScreenKind kind)
    {
        if (root == null) return;
        if (!root.gameObject.scene.IsValid())
        {
            EditorUtility.DisplayDialog("対象を確認", "Hierarchy上のPanel Rootを指定してください（Prefab Assetではありません）。", "OK");
            return;
        }
        Undo.RegisterFullObjectHierarchyUndo(root.gameObject, "町UIをシンプルRPG風に統一");
        TownSimpleRpgSkin2D skin = root.GetComponent<TownSimpleRpgSkin2D>();
        if (skin == null) skin = Undo.AddComponent<TownSimpleRpgSkin2D>(root.gameObject);
        Undo.RecordObject(skin, "町UI設定を保存");
        skin.Configure(kind, root);
        skin.ConfigureJapaneseFonts(japaneseTmpFont, japaneseLegacyFont);
        skin.ApplyTheme();
        EditorUtility.SetDirty(skin);
        EditorSceneManager.MarkSceneDirty(root.gameObject.scene);
    }
}
#endif
