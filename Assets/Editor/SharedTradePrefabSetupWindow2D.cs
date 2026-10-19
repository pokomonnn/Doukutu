#if UNITY_EDITOR
using System;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 町の完成済み売買Canvasの内部構造を編集しないでPrefab化するツール。
/// 既存UIのGrid、商品アイコンPrefab、Slider/Button、SE参照をコピーします。
/// </summary>
public sealed class SharedTradePrefabSetupWindow2D : EditorWindow
{
    private const string PrefabPath = "Assets/DialogueStudio2D/SharedTrade/Prefabs/SharedTownTradeUI.prefab";
    private TownUnifiedTradeScreen2D townScreen;
    private Canvas targetCanvas;
    private GameObject sharedPrefab;
    private DungeonNpcShop2D targetNpc;

    [MenuItem("Tools/2D Shooter/Dialogue Studio/町と洞窟の共通売買UIセットアップ")]
    private static void ShowWindow()
    {
        GetWindow<SharedTradePrefabSetupWindow2D>("共通売買UI").Show();
    }

    private void OnEnable()
    {
        FindTownScreen();
        targetCanvas = UnityEngine.Object.FindAnyObjectByType<Canvas>(FindObjectsInactive.Include);
        sharedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        if (Selection.activeGameObject != null)
            targetNpc = Selection.activeGameObject.GetComponent<DungeonNpcShop2D>();
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("町・洞窟の売買画面を共通化", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "最初は必ずTown_Mainをバックアップしてください。Prefabの元はTown_Mainの実際のUIです。" +
            "旧の洞窟用簡易UIは消しません。", MessageType.Info);
        EditorGUILayout.Space(8);
        townScreen = (TownUnifiedTradeScreen2D)EditorGUILayout.ObjectField(
            "町の売買Screen", townScreen, typeof(TownUnifiedTradeScreen2D), true);
        if (GUILayout.Button("開いているシーンから町のScreenを検索")) FindTownScreen();
        using (new EditorGUI.DisabledScope(townScreen == null || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("1. 町の現UIを共通Prefabとして保存（町シーンは変更しない）", GUILayout.Height(38)))
                CreateSharedPrefab();
        }
        EditorGUILayout.Space(12);
        sharedPrefab = (GameObject)EditorGUILayout.ObjectField(
            "共通UI Prefab", sharedPrefab, typeof(GameObject), false);
        targetCanvas = (Canvas)EditorGUILayout.ObjectField("洞窟Canvas", targetCanvas, typeof(Canvas), true);
        using (new EditorGUI.DisabledScope(sharedPrefab == null || targetCanvas == null || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("2. 開いている洞窟シーンへ共通Prefabを配置", GUILayout.Height(34)))
                InstallPrefabInCanvas();
        }
        EditorGUILayout.Space(12);
        targetNpc = (DungeonNpcShop2D)EditorGUILayout.ObjectField(
            "設定するNPC", targetNpc, typeof(DungeonNpcShop2D), true);
        if (GUILayout.Button("選択中のGameObjectからNPCを取得") && Selection.activeGameObject != null)
            targetNpc = Selection.activeGameObject.GetComponent<DungeonNpcShop2D>();
        using (new EditorGUI.DisabledScope(targetNpc == null || EditorApplication.isPlaying))
        {
            if (GUILayout.Button("3. NPCの商人在庫を作成して共通UIを接続", GUILayout.Height(38)))
                ConfigureNpc();
        }
        EditorGUILayout.Space(14);
        EditorGUILayout.LabelField("町をPrefabインスタンスに切り替える（任意）", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "上の1〜3だけで、洞窟では町と同一の画面・コントローラーを使用できます。" +
            "下の操作はTown_Mainの既存オブジェクトを壊さないように再親子化しPrefab接続しますが、" +
            "町の参照を多く使うため、別途バックアップしたシーンでのみ実行してください。", MessageType.Warning);
        using (new EditorGUI.DisabledScope(townScreen == null || EditorApplication.isPlaying || sharedPrefab == null))
        {
            if (GUILayout.Button("4. 町の既存UIを同一Prefabへ接続（バックアップ必須）", GUILayout.Height(34)))
            {
                if (EditorUtility.DisplayDialog("既存の町UIをPrefab化しますか？",
                    "Town_Mainの保存済みバックアップがありますか？この操作は町シーンを変更します。", "実行", "キャンセル"))
                    ConvertTownToPrefab();
            }
        }
    }

    private void FindTownScreen()
    {
        foreach (TownUnifiedTradeScreen2D screen in Resources.FindObjectsOfTypeAll<TownUnifiedTradeScreen2D>())
        {
            if (screen != null && screen.gameObject.scene.IsValid() &&
                screen.gameObject.name == "TownUnifiedTradeManager")
            {
                townScreen = screen;
                return;
            }
        }
    }

    private static UnityEngine.Object GetRef(UnityEngine.Object owner, string property)
    {
        SerializedObject serialized = new SerializedObject(owner);
        SerializedProperty prop = serialized.FindProperty(property);
        return prop != null ? prop.objectReferenceValue : null;
    }

    private static void SetRef(UnityEngine.Object owner, string property, UnityEngine.Object value)
    {
        SerializedObject serialized = new SerializedObject(owner);
        SerializedProperty prop = serialized.FindProperty(property);
        if (prop == null) throw new InvalidOperationException(owner.GetType().Name + " の " + property + " が見つかりません。");
        prop.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(owner);
    }

    private static UnityEngine.Object FindCloneReference(UnityEngine.Object source, Transform sourceRoot,
        Transform cloneRoot)
    {
        if (source == null) return null;
        Transform transform = source as Transform;
        Component component = source as Component;
        GameObject go = source as GameObject;
        if (component != null) transform = component.transform;
        else if (go != null) transform = go.transform;
        if (transform == null || (transform != sourceRoot && !transform.IsChildOf(sourceRoot)))
            return null;
        string path = AnimationUtility.CalculateTransformPath(transform, sourceRoot);
        Transform target = string.IsNullOrEmpty(path) ? cloneRoot : cloneRoot.Find(path);
        if (target == null) return null;
        return go != null ? (UnityEngine.Object)target.gameObject :
            component != null ? target.GetComponent(component.GetType()) : (UnityEngine.Object)target;
    }

    private static void CopyMappedField(UnityEngine.Object from, UnityEngine.Object to,
        string name, Transform fromRoot, Transform toRoot)
    {
        SetRef(to, name, FindCloneReference(GetRef(from, name), fromRoot, toRoot));
    }

    private void CreateSharedPrefab()
    {
        GameObject sourceUI = GetRef(townScreen, "tradeRoot") as GameObject;
        if (sourceUI == null)
        {
            EditorUtility.DisplayDialog("参照不足", "町のTownUnifiedTradeScreen2DのTrade Rootが未設定です。", "OK");
            return;
        }
        ExpeditionUnifiedTradeController2D sourceTrade = townScreen.GetComponent<ExpeditionUnifiedTradeController2D>();
        if (sourceTrade == null)
        {
            EditorUtility.DisplayDialog("参照不足", "TownUnifiedTradeManagerにExpeditionUnifiedTradeController2Dが必要です。", "OK");
            return;
        }
        GameObject prefabRoot = null;
        try
        {
            prefabRoot = new GameObject("SharedTownTradeUI", typeof(RectTransform), typeof(SharedTradePrefabAnchor2D));
            RectTransform rect = prefabRoot.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
            GameObject clonedUI = Instantiate(sourceUI, prefabRoot.transform, false);
            clonedUI.name = sourceUI.name;
            GameObject clonedManager = Instantiate(townScreen.gameObject, prefabRoot.transform, false);
            clonedManager.name = townScreen.gameObject.name;
            TownUnifiedTradeScreen2D copiedScreen = clonedManager.GetComponent<TownUnifiedTradeScreen2D>();
            ExpeditionUnifiedTradeController2D copiedTrade = clonedManager.GetComponent<ExpeditionUnifiedTradeController2D>();
            Transform srcRoot = sourceUI.transform;
            Transform dstRoot = clonedUI.transform;
            foreach (string field in new[] {"tradeRoot", "windowRect", "windowCanvasGroup", "shopNameText", "closeButton"})
                CopyMappedField(townScreen, copiedScreen, field, srcRoot, dstRoot);
            SetRef(copiedScreen, "tradeController", copiedTrade);
            SetRef(copiedScreen, "pawnShopController", null);
            SetRef(copiedScreen, "playerTradeInventory", null);
            foreach (string field in new[] { "merchantGridUI", "playerGridUI", "buyAmountText", "sellAmountText",
                "totalAmountText", "moneyText", "statusText", "checkoutButton", "quantitySlider", "sliderRoot",
                "quantityText", "selectedItemText" })
                CopyMappedField(sourceTrade, copiedTrade, field, srcRoot, dstRoot);
            foreach (string field in new[] {"merchantStock", "playerInventory", "sellRules", "townInventoryBridge", "sessionManager"})
                SetRef(copiedTrade, field, null);
            SetRef(copiedTrade, "audioSource", clonedManager.GetComponent<AudioSource>());
            prefabRoot.GetComponent<SharedTradePrefabAnchor2D>().Configure(copiedScreen, copiedTrade);
            string path = "Assets/DialogueStudio2D/SharedTrade/Prefabs";
            if (!AssetDatabase.IsValidFolder("Assets/DialogueStudio2D"))
                AssetDatabase.CreateFolder("Assets", "DialogueStudio2D");
            if (!AssetDatabase.IsValidFolder("Assets/DialogueStudio2D/SharedTrade"))
                AssetDatabase.CreateFolder("Assets/DialogueStudio2D", "SharedTrade");
            if (!AssetDatabase.IsValidFolder(path)) AssetDatabase.CreateFolder("Assets/DialogueStudio2D/SharedTrade", "Prefabs");
            bool ok;
            PrefabUtility.SaveAsPrefabAsset(prefabRoot, PrefabPath, out ok);
            if (!ok) throw new InvalidOperationException("Prefabの保存に失敗しました");
            sharedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
            EditorUtility.DisplayDialog("作成完了", "町の売買UIを実際のHierarchyからPrefabに保存しました。\n" + PrefabPath, "OK");
        }
        catch (Exception error)
        {
            Debug.LogException(error);
            EditorUtility.DisplayDialog("失敗", error.Message, "OK");
        }
        finally
        {
            if (prefabRoot != null) DestroyImmediate(prefabRoot);
        }
    }

    private void InstallPrefabInCanvas()
    {
        SharedTradePrefabAnchor2D existing = targetCanvas.GetComponentInChildren<SharedTradePrefabAnchor2D>(true);
        if (existing != null)
        {
            EditorUtility.DisplayDialog("配置済み", "このCanvasには共通売買UIがすでに配置されています。", "OK");
            Selection.activeObject = existing.gameObject;
            return;
        }
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(sharedPrefab, targetCanvas.gameObject.scene);
        Undo.RegisterCreatedObjectUndo(instance, "共通売買UI配置");
        instance.transform.SetParent(targetCanvas.transform, false);
        instance.name = "SharedTownTradeUI";
        if (instance.transform is RectTransform r)
        {
            r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }
        EditorSceneManager.MarkSceneDirty(targetCanvas.gameObject.scene);
        Selection.activeGameObject = instance;
    }

    private void ConfigureNpc()
    {
        if (targetNpc == null) return;
        Canvas canvas = targetNpc.GetComponentInParent<Canvas>();
        SharedTradePrefabAnchor2D shared = canvas != null
            ? canvas.GetComponentInChildren<SharedTradePrefabAnchor2D>(true)
            : FindActiveSceneTradePrefab(targetNpc.gameObject.scene);
        if (shared == null || shared.Screen == null)
        {
            EditorUtility.DisplayDialog("UI未配置", "先に洞窟Canvasへ共通売買Prefabを配置してください。", "OK");
            return;
        }
        MerchantStockInventory stock = targetNpc.GetComponentInChildren<MerchantStockInventory>(true);
        if (stock == null)
        {
            GameObject inventoryGO = new GameObject("NPC共通売買用_商品在庫");
            Undo.RegisterCreatedObjectUndo(inventoryGO, "NPC商品在庫の作成");
            inventoryGO.transform.SetParent(targetNpc.transform, false);
            ItemBoxInventory itemBox = Undo.AddComponent<ItemBoxInventory>(inventoryGO);
            SerializedObject serialized = new SerializedObject(itemBox);
            serialized.FindProperty("boxKind").enumValueIndex = (int)ItemBoxKind.Shop;
            serialized.FindProperty("gridWidth").intValue = 7;
            serialized.FindProperty("gridHeight").intValue = 20;
            serialized.FindProperty("addStartingItemsOnAwake").boolValue = false;
            serialized.ApplyModifiedProperties();
            stock = Undo.AddComponent<MerchantStockInventory>(inventoryGO);
        }
        Undo.RecordObject(targetNpc, "NPC共通売買UIの接続");
        SetRef(targetNpc, "sharedMerchantStock", stock);
        SetRef(targetNpc, "sharedTownTradeScreen", shared.Screen);
        SerializedObject npcProperties = new SerializedObject(targetNpc);
        npcProperties.FindProperty("useSharedTownTradeUI").boolValue = true;
        // 会話側のEキーと二重起動しないようNPCのショップキーを無効化。
        npcProperties.FindProperty("interactionKey").enumValueIndex = (int)KeyCode.None;
        npcProperties.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(targetNpc.gameObject.scene);
        EditorUtility.DisplayDialog("接続完了", "NPCに共通売買UIと商人在庫を接続しました。\nPlayerにPlayerTradeInventoryBridgeがあることも確認してください。", "OK");
    }

    private static SharedTradePrefabAnchor2D FindActiveSceneTradePrefab(Scene scene)
    {
        foreach (SharedTradePrefabAnchor2D a in Resources.FindObjectsOfTypeAll<SharedTradePrefabAnchor2D>())
            if (a != null && a.gameObject.scene == scene) return a;
        return null;
    }

    private void ConvertTownToPrefab()
    {
        GameObject sourceUI = GetRef(townScreen, "tradeRoot") as GameObject;
        GameObject originalManager = townScreen.gameObject;
        if (sourceUI == null || originalManager.scene != sourceUI.scene)
        {
            EditorUtility.DisplayDialog("確認", "町のManagerとRootが同一シーンにありません。", "OK");
            return;
        }
        // 既存オブジェクトを再親子化する方式。外部のTownConversationControllerなどからの参照先を保持。
        Transform parent = sourceUI.transform.parent;
        GameObject holder = new GameObject("SharedTownTradeUI", typeof(RectTransform), typeof(SharedTradePrefabAnchor2D));
        Undo.RegisterCreatedObjectUndo(holder, "町UIを共通Prefabに接続");
        holder.transform.SetParent(parent, false);
        if (holder.transform is RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
        Undo.SetTransformParent(sourceUI.transform, holder.transform, "町の売買UIを格納");
        Undo.SetTransformParent(originalManager.transform, holder.transform, "町の売買Managerを格納");
        // Scene-only references cannot be embedded in an asset, so keep them and restore as prefab overrides.
        ExpeditionUnifiedTradeController2D trade = originalManager.GetComponent<ExpeditionUnifiedTradeController2D>();
        string[] screenFields = { "pawnShopController", "playerTradeInventory" };
        string[] controllerFields = { "merchantStock", "playerInventory", "sellRules", "townInventoryBridge", "sessionManager" };
        UnityEngine.Object[] screenRefs = Array.ConvertAll(screenFields, n => GetRef(townScreen, n));
        UnityEngine.Object[] controllerRefs = Array.ConvertAll(controllerFields, n => GetRef(trade, n));
        holder.GetComponent<SharedTradePrefabAnchor2D>().Configure(townScreen, trade);
        bool ok;
        PrefabUtility.SaveAsPrefabAssetAndConnect(holder, PrefabPath, InteractionMode.UserAction, out ok);
        if (!ok)
        {
            Debug.LogError("[共通売買UI] 町のPrefab接続に失敗しました。Undoで元に戻してください。");
            return;
        }
        for (int i = 0; i < screenFields.Length; i++) SetRef(townScreen, screenFields[i], screenRefs[i]);
        for (int i = 0; i < controllerFields.Length; i++) SetRef(trade, controllerFields[i], controllerRefs[i]);
        PrefabUtility.RecordPrefabInstancePropertyModifications(townScreen);
        PrefabUtility.RecordPrefabInstancePropertyModifications(trade);
        EditorSceneManager.MarkSceneDirty(originalManager.scene);
        EditorUtility.DisplayDialog("町のPrefab化完了", "Town_Mainの既存オブジェクトを同じPrefabへ接続しました。\n保存前にInspectで参照を確認し、Playテストしてください。", "OK");
    }
}
#endif
