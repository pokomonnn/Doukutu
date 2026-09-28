using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 洞窟内の商人に近づいてキー入力すると、既存の購入・売却UIを開きます。
/// 売買中はPlayerMoveを停止し、PlayerEquipmentVisualControllerのOwner Lockで
/// 射撃・照準をロックします。
///
/// v2.6 Merchant Stock Resolve:
/// ・v2.5のPlayer検出補助を維持
/// ・MerchantStockInventoryがPrefab階層外にある場合、Scene内から自動補完
/// ・Scene内に複数Stockがある場合は、この商人に最も近いStockを選択可能
/// ・Stock自動補完時は選択理由をConsoleへ表示
/// ・v2.4の距離診断を維持
/// ・Player Layer判定に加え、PlayerTradeInventoryBridgeを補助判定として使用可能
/// ・Triggerイベントを取り逃しても、商人TriggerとPlayer Colliderの実重なりから範囲内状態を復旧
/// ・CaveMerchant2DとMerchantStockInventoryがPrefab内の兄弟階層でも自動検索
/// ・PlayerMove / PlayerEquipmentVisualControllerがBridgeと別階層でも検索
/// ・Trigger受信
/// ・PlayerTradeInventoryBridgeは売買用参照としてのみ取得
/// ・Eキー入力
/// ・MerchantStockInventory / PawnShopUIController検出
/// ・OpenPawnShopの成否
/// ・Collider2D / Rigidbody2D構成
/// をConsoleへ段階的に出力できます。
/// </summary>
[DisallowMultipleComponent]
public class CaveMerchant2D : MonoBehaviour
{
    [Header("商人")]
    [SerializeField] private MerchantStockInventory merchantStockInventory;
    [SerializeField] private PawnShopUIController pawnShopUIController;

    [Header("MerchantStock自動補完")]
    [Tooltip("Prefabの親・子・RootからMerchantStockInventoryが見つからない場合、Scene内のMerchantStockInventoryを検索します。洞窟商人ではON推奨です。")]
    [SerializeField] private bool allowSceneMerchantStockFallback = true;

    [Tooltip("Scene内にMerchantStockInventoryが複数ある場合、この商人Transformに最も近いStockを自動選択します。OFFなら複数候補時は自動選択しません。")]
    [SerializeField] private bool chooseNearestSceneStockWhenMultiple = true;

    [Header("Player判定")]
    [Tooltip("このLayer Maskに含まれるLayerをPlayerとして認識します。通常は Player Layerだけを選択してください。")]
    [SerializeField] private LayerMask playerLayerMask;

    [Tooltip("Collider自身がPlayer Layerでなくても、親階層にPlayer LayerがあればPlayerとして認識します。")]
    [SerializeField] private bool checkParentLayers = true;

    [Tooltip("Player Layer判定に失敗しても、同じPlayer階層にPlayerTradeInventoryBridgeがあればPlayerとして補助認識します。洞窟PlayerではON推奨です。")]
    [SerializeField] private bool allowPlayerBridgeFallback = true;

    [Header("Trigger補助判定")]
    [Tooltip("OnTriggerEnter2D / Stayを取り逃した場合でも、商人TriggerとPlayer Colliderの実際の重なりから範囲内状態を復旧します。ON推奨です。")]
    [SerializeField] private bool usePhysicsOverlapFallback = true;

    [Tooltip("Trigger補助判定を行う間隔（秒）です。0.1秒程度を推奨します。")]
    [SerializeField, Min(0.02f)] private float overlapFallbackInterval = 0.1f;

    [Tooltip("補助判定でPlayer範囲内状態を復旧した時にConsoleへログを出します。")]
    [SerializeField] private bool logOverlapFallbackRecovery = true;

    [Header("操作")]
    [SerializeField] private KeyCode interactionKey = KeyCode.E;
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private bool allowInteractionKeyToClose = true;

    [Header("案内UI（任意）")]
    [SerializeField] private GameObject interactionPromptRoot;
    [SerializeField] private TMP_Text interactionPromptText;
    [SerializeField] private string interactionPromptFormat = "[{0}] 売買";

    [Header("売買中のPlayer制御")]
    [SerializeField] private bool disablePlayerMoveWhileTrading = true;
    [SerializeField] private bool lockWeaponWhileTrading = true;
    [Tooltip("ロープ・壁登りなど、売買中に個別で止めたいBehaviourがあれば追加します。")]
    [SerializeField] private List<Behaviour> extraBehavioursToDisable = new List<Behaviour>();

    [Header("カーソル")]
    [SerializeField] private bool showCursorWhileTrading = true;

    [Header("デバッグ・診断")]
    [Tooltip("オン推奨。商人との接触・入力・参照取得・売買開始までをConsoleへ出します。")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("OnTriggerEnter2D / Stay / Exitで受け取ったCollider情報を出します。")]
    [SerializeField] private bool logTriggerEvents = true;

    [Tooltip("Eキー等のInteraction Keyを押した時、範囲内判定を出します。")]
    [SerializeField] private bool logInteractionInput = true;

    [Tooltip("MerchantStockInventoryやPawnShopUIControllerの自動検索結果を出します。")]
    [SerializeField] private bool logReferenceSearch = true;

    [Tooltip("TriggerEnterを取り逃した場合、TriggerStayからPlayer判定を復旧します。")]
    [SerializeField] private bool recoverPlayerWithTriggerStay = true;

    [Tooltip("オンにすると一定間隔で現在状態を出します。通常はオフでOKです。")]
    [SerializeField] private bool periodicStatusLog;

    [SerializeField, Min(0.5f)] private float periodicStatusInterval = 3f;

    [Header("距離診断")]
    [Tooltip("オンにすると、Trigger判定に関係なくScene内のPlayer Layer Colliderを探し、商人との距離をConsoleへ表示します。診断中はオン推奨です。")]
    [SerializeField] private bool logPlayerDistance = true;

    [Tooltip("Playerとの距離を表示する間隔（秒）。診断中は1秒がおすすめです。")]
    [SerializeField, Min(0.2f)] private float playerDistanceLogInterval = 1f;

    [Tooltip("距離ログに商人TriggerとPlayer ColliderのBounds重なり、Layer Collision Matrixも表示します。")]
    [SerializeField] private bool logDistancePhysicsDetails = true;

    private PlayerTradeInventoryBridge playerTradeInventory;
    private PlayerMove playerMove;
    private PlayerEquipmentVisualController equipmentVisualController;

    private readonly Dictionary<Behaviour, bool> extraBehaviourStates =
        new Dictionary<Behaviour, bool>();

    // Playerに複数Colliderがある場合でも、1個抜けただけで範囲外扱いしないため保持します。
    private readonly HashSet<Collider2D> playerCollidersInside =
        new HashSet<Collider2D>();

    private bool playerInside;
    private bool fallbackPlayerInside;
    private bool isTrading;
    private bool playerMoveWasEnabled;
    private bool previousCursorVisible;
    private CursorLockMode previousCursorLockMode;
    private float nextPeriodicStatusTime;
    private float nextPlayerDistanceLogTime;
    private float nextOverlapFallbackTime;

    private void Awake()
    {
        EnsureDefaultPlayerLayer();
        FindReferences();
        SetPromptVisible(false);

        Log(
            $"Awake: GameObject={name} / Active={gameObject.activeInHierarchy} / " +
            $"Enabled={enabled} / InteractionKey={interactionKey}"
        );
    }

    private void Start()
    {
        PrintDiagnosticSnapshot();
    }

    private void OnEnable()
    {
        Log("OnEnable: CaveMerchant2Dが有効になりました。");
    }

    private void OnValidate()
    {
        periodicStatusInterval = Mathf.Max(0.5f, periodicStatusInterval);
        playerDistanceLogInterval = Mathf.Max(0.2f, playerDistanceLogInterval);
        overlapFallbackInterval = Mathf.Max(0.02f, overlapFallbackInterval);
        EnsureDefaultPlayerLayer();

        if (merchantStockInventory == null)
        {
            merchantStockInventory =
                GetComponentInParent<MerchantStockInventory>();

            if (merchantStockInventory == null)
            {
                merchantStockInventory =
                    GetComponentInChildren<MerchantStockInventory>(true);
            }

            if (merchantStockInventory == null && transform.root != null)
            {
                merchantStockInventory =
                    transform.root.GetComponentInChildren<MerchantStockInventory>(true);
            }
        }
    }

    private void Update()
    {
        if (usePhysicsOverlapFallback &&
            !isTrading &&
            Time.unscaledTime >= nextOverlapFallbackTime)
        {
            nextOverlapFallbackTime =
                Time.unscaledTime + overlapFallbackInterval;

            RefreshPhysicsOverlapFallback(false);
        }

        if (periodicStatusLog &&
            showDebugLogs &&
            Time.unscaledTime >= nextPeriodicStatusTime)
        {
            nextPeriodicStatusTime =
                Time.unscaledTime + periodicStatusInterval;

            LogCurrentState("定期状態");
        }

        if (logPlayerDistance &&
            showDebugLogs &&
            Time.unscaledTime >= nextPlayerDistanceLogTime)
        {
            nextPlayerDistanceLogTime =
                Time.unscaledTime + playerDistanceLogInterval;

            LogPlayerDistanceSnapshot();
        }

        bool interactionPressed = Input.GetKeyDown(interactionKey);

        if (interactionPressed && logInteractionInput)
        {
            Log(
                $"入力検出: {interactionKey} / playerInside={playerInside} / " +
                $"isTrading={isTrading} / PlayerBridge=" +
                $"{GetObjectName(playerTradeInventory)} / " +
                $"範囲内PlayerCollider数={playerCollidersInside.Count}"
            );

            if (!playerInside)
            {
                LogWarning(
                    $"{interactionKey} は検出しましたが、商人はPlayerを範囲内と認識していません。" +
                    " Trigger設定またはPlayer判定を確認してください。"
                );

                // Eを押した瞬間にも必ず距離診断を出す。
                LogPlayerDistanceSnapshot();
            }
        }

        // UI側のClose Buttonから閉じられた場合も、Player Lockを必ず解除する。
        if (isTrading &&
            (pawnShopUIController == null || !pawnShopUIController.IsOpen))
        {
            Log(
                "PawnShopUIController側が閉じたことを検出したため、Player Lockを解除します。"
            );
            FinishTradingState();
        }

        if (isTrading)
        {
            if (Input.GetKeyDown(closeKey) ||
                (allowInteractionKeyToClose &&
                 interactionKey != closeKey &&
                 interactionPressed))
            {
                Log("売買終了キーを検出しました。");
                TryCloseTrade();
            }

            return;
        }

        if (playerInside && interactionPressed)
        {
            Log("Player範囲内 + Interaction Key検出 → TryOpenTradeを実行します。");
            TryOpenTrade();
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (logTriggerEvents)
        {
            LogTrigger("OnTriggerEnter2D", other);
        }

        if (!TryRecognizePlayer(other, out Transform matchedLayerTransform))
        {
            Log(
                $"TriggerEnterは受信しましたがPlayerとして認識できないため無視します: " +
                $"{DescribeCollider(other)} / PlayerLayerMask={DescribeLayerMask(playerLayerMask)}"
            );
            return;
        }

        playerCollidersInside.Add(other);
        RecalculatePlayerInsideState();

        CachePlayerReferences(other, matchedLayerTransform, true);

        Log(
            $"Player Layerを検出して範囲内登録しました。" +
            $"MatchedObject={GetObjectName(matchedLayerTransform)} / " +
            $"Layer={LayerMask.LayerToName(matchedLayerTransform.gameObject.layer)}" +
            $"({matchedLayerTransform.gameObject.layer}) / " +
            $"Collider数={playerCollidersInside.Count} / " +
            $"PlayerBridge={GetObjectName(playerTradeInventory)}"
        );

        SetPromptVisible(!isTrading);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        if (!recoverPlayerWithTriggerStay || playerInside)
        {
            return;
        }

        if (!TryRecognizePlayer(other, out Transform matchedLayerTransform))
        {
            return;
        }

        playerCollidersInside.Add(other);
        RecalculatePlayerInsideState();

        CachePlayerReferences(other, matchedLayerTransform, false);

        LogWarning(
            "TriggerEnterではPlayer登録されていませんでしたが、" +
            "OnTriggerStay2DからPlayer Layer判定で復旧しました。 " +
            $"MatchedObject={GetObjectName(matchedLayerTransform)} / " +
            $"PlayerBridge={GetObjectName(playerTradeInventory)}"
        );

        SetPromptVisible(!isTrading);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (logTriggerEvents)
        {
            LogTrigger("OnTriggerExit2D", other);
        }

        if (!playerCollidersInside.Contains(other))
        {
            return;
        }

        playerCollidersInside.Remove(other);
        RecalculatePlayerInsideState();

        Log(
            $"Player Layer Collider退出: 残りCollider数={playerCollidersInside.Count} / " +
            $"playerInside={playerInside}"
        );

        if (!playerInside)
        {
            SetPromptVisible(false);
        }
    }

    private void OnDisable()
    {
        Log("OnDisable: CaveMerchant2Dが無効になりました。");

        SetPromptVisible(false);
        playerCollidersInside.Clear();
        fallbackPlayerInside = false;
        playerInside = false;

        if (isTrading)
        {
            FinishTradingState();
        }
    }

    public bool TryOpenTrade()
    {
        Log("TryOpenTrade開始");

        if (isTrading)
        {
            Log("すでに売買中のため成功扱いで終了します。");
            return true;
        }

        FindReferences();

        if (usePhysicsOverlapFallback)
        {
            RefreshPhysicsOverlapFallback(true);
        }

        Log(
            $"売買開始前チェック: playerInside={playerInside} / " +
            $"PlayerBridge={GetObjectName(playerTradeInventory)} / " +
            $"MerchantStock={GetObjectName(merchantStockInventory)} / " +
            $"PawnShopUI={GetObjectName(pawnShopUIController)}"
        );

        if (!playerInside || playerTradeInventory == null)
        {
            LogWarning(
                "売買開始失敗: Player Layerは範囲内にいますが、" +
                "PlayerTradeInventoryBridgeが見つかっていないか、Playerが範囲外です。"
            );
            return false;
        }

        playerTradeInventory.RefreshReferences();

        Log(
            "PlayerTradeInventoryBridge参照: " +
            $"Inventory={GetObjectName(playerTradeInventory.InventoryController)} / " +
            $"Equipment={GetObjectName(playerTradeInventory.EquipmentController)} / " +
            $"SessionBridge={GetObjectName(playerTradeInventory.SessionBridge)} / " +
            $"IsReady={playerTradeInventory.IsReady}"
        );

        if (!playerTradeInventory.IsReady)
        {
            LogWarning(
                "売買開始失敗: PlayerTradeInventoryBridgeのInventory / Equipment / " +
                "SessionBridge参照が不足しています。"
            );
            return false;
        }

        if (merchantStockInventory == null)
        {
            LogWarning(
                "売買開始失敗: MerchantStockInventoryが設定・検出されていません。" +
                " 商人PrefabにMerchantStockInventoryを追加するか、" +
                " Merchant Stock Inventoryへ直接設定してください。"
            );
            return false;
        }

        Log(
            $"MerchantStock確認: ShopName={merchantStockInventory.ShopName} / " +
            $"IsReady={merchantStockInventory.IsReady} / " +
            $"Object={merchantStockInventory.name}"
        );

        if (!merchantStockInventory.IsReady)
        {
            LogWarning(
                "MerchantStockInventory.IsReady=false です。" +
                " 同じGameObjectのItemBoxInventoryとMerchantShopData設定を確認してください。"
            );
        }

        if (pawnShopUIController == null)
        {
            LogWarning(
                "売買開始失敗: PawnShopUIControllerがシーン内に見つかりません。" +
                " 非アクティブPanel内でもController自体がSceneに存在する必要があります。"
            );
            return false;
        }

        Log(
            $"OpenPawnShop呼び出し直前: PawnShopUI={pawnShopUIController.name} / " +
            $"現在IsOpen={pawnShopUIController.IsOpen}"
        );

        bool opened =
            pawnShopUIController.OpenPawnShop(merchantStockInventory);

        Log(
            $"OpenPawnShop戻り値={opened} / " +
            $"呼び出し後IsOpen={pawnShopUIController.IsOpen}"
        );

        if (!opened)
        {
            LogWarning(
                "売買開始失敗: PawnShopUIController.OpenPawnShopがfalseを返しました。" +
                " Console内のPawnShopUIController / MerchantPurchaseController側ログも確認してください。"
            );
            return false;
        }

        BeginTradingState();
        Log($"売買開始成功: {merchantStockInventory.ShopName}");
        return true;
    }

    public bool TryCloseTrade()
    {
        Log("TryCloseTrade開始");

        if (!isTrading)
        {
            Log("売買中ではないため成功扱いで終了します。");
            return true;
        }

        if (pawnShopUIController != null &&
            pawnShopUIController.IsOpen &&
            !pawnShopUIController.TryClosePawnShop())
        {
            // 売却カート内のItemをInventoryへ戻せない場合などは、
            // Item消失防止のためPlayer Lockも解除しない。
            LogWarning(
                "売却予定アイテムを戻せないため売買画面を閉じませんでした。"
            );
            return false;
        }

        FinishTradingState();
        return true;
    }

    /// <summary>
    /// Playerかどうかの判定はLayerだけで行います。
    /// Bridge / PlayerMove / EquipmentVisualは、Playerと認識した後の売買用参照として取得します。
    /// </summary>
    /// <summary>
    /// Player判定はLayerを優先し、必要ならPlayerTradeInventoryBridgeを補助判定に使用します。
    /// ColliderがPlayer Layerの兄弟階層にあるPrefab構成でも認識しやすくします。
    /// </summary>
    private bool TryRecognizePlayer(
        Collider2D other,
        out Transform matchedLayerTransform)
    {
        if (IsPlayerLayer(other, out matchedLayerTransform))
        {
            return true;
        }

        if (!allowPlayerBridgeFallback || other == null)
        {
            return false;
        }

        PlayerTradeInventoryBridge bridge =
            other.GetComponentInParent<PlayerTradeInventoryBridge>();

        Transform root = other.transform.root;

        if (bridge == null && root != null)
        {
            bridge = root.GetComponentInChildren<PlayerTradeInventoryBridge>(true);
        }

        if (bridge == null)
        {
            return false;
        }

        matchedLayerTransform = bridge.transform;
        return true;
    }

    /// <summary>
    /// Triggerイベントが届かないPrefab階層でも、実際のCollider重なりからPlayer範囲内状態を復旧します。
    /// Collider2D.Distanceを使うため、CaveMerchant2DがTrigger Colliderと別GameObjectでも動作します。
    /// </summary>
    private void RefreshPhysicsOverlapFallback(bool forceLog)
    {
        if (!usePhysicsOverlapFallback)
        {
            return;
        }

        Collider2D merchantTrigger = FindInteractionTriggerCollider();

        if (merchantTrigger == null ||
            !merchantTrigger.enabled ||
            !merchantTrigger.gameObject.activeInHierarchy)
        {
            SetFallbackPlayerInside(false, null, forceLog);
            return;
        }

        PlayerTradeInventoryBridge bridge = playerTradeInventory;

        if (bridge == null || !bridge.gameObject.activeInHierarchy)
        {
            bridge = FindAnyObjectByType<PlayerTradeInventoryBridge>(
                FindObjectsInactive.Exclude
            );
        }

        if (bridge == null)
        {
            SetFallbackPlayerInside(false, null, forceLog);
            return;
        }

        Collider2D[] playerColliders =
            bridge.transform.root.GetComponentsInChildren<Collider2D>(false);

        Collider2D overlappedPlayerCollider = null;
        Transform matchedLayerTransform = null;

        foreach (Collider2D candidate in playerColliders)
        {
            if (candidate == null ||
                candidate == merchantTrigger ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!TryRecognizePlayer(candidate, out Transform matchedTransform))
            {
                continue;
            }

            ColliderDistance2D distance =
                merchantTrigger.Distance(candidate);

            if (!distance.isOverlapped && distance.distance > 0.001f)
            {
                continue;
            }

            overlappedPlayerCollider = candidate;
            matchedLayerTransform = matchedTransform;
            break;
        }

        bool isOverlapping = overlappedPlayerCollider != null;

        if (isOverlapping)
        {
            CachePlayerReferences(
                overlappedPlayerCollider,
                matchedLayerTransform,
                false
            );
        }

        SetFallbackPlayerInside(
            isOverlapping,
            overlappedPlayerCollider,
            forceLog
        );
    }

    private void SetFallbackPlayerInside(
        bool inside,
        Collider2D matchedCollider,
        bool forceLog)
    {
        bool previousFallback = fallbackPlayerInside;
        fallbackPlayerInside = inside;

        bool previousInside = playerInside;
        RecalculatePlayerInsideState();

        if (inside &&
            (forceLog || (!previousFallback && logOverlapFallbackRecovery)))
        {
            LogWarning(
                "Triggerイベントに依存しない補助判定でPlayer範囲内を検出しました。 " +
                $"Collider={GetObjectName(matchedCollider)} / " +
                $"PlayerBridge={GetObjectName(playerTradeInventory)} / " +
                $"playerInside={playerInside}"
            );
        }
        else if (!inside &&
                 previousFallback &&
                 previousInside &&
                 !playerInside &&
                 logOverlapFallbackRecovery)
        {
            Log("補助判定上、Playerが商人範囲外へ出ました。");
        }
    }

    private void RecalculatePlayerInsideState()
    {
        bool previous = playerInside;

        playerInside =
            playerCollidersInside.Count > 0 ||
            fallbackPlayerInside;

        if (previous != playerInside && !isTrading)
        {
            SetPromptVisible(playerInside);
        }
    }

    private bool IsPlayerLayer(
        Collider2D other,
        out Transform matchedLayerTransform)
    {
        matchedLayerTransform = null;

        if (other == null)
        {
            return false;
        }

        Transform current = other.transform;

        while (current != null)
        {
            int layerBit = 1 << current.gameObject.layer;

            if ((playerLayerMask.value & layerBit) != 0)
            {
                matchedLayerTransform = current;
                return true;
            }

            if (!checkParentLayers)
            {
                break;
            }

            current = current.parent;
        }

        return false;
    }

    private void CachePlayerReferences(
        Collider2D other,
        Transform matchedLayerTransform,
        bool verbose)
    {
        if (other == null)
        {
            return;
        }

        PlayerTradeInventoryBridge bridge =
            other.GetComponentInParent<PlayerTradeInventoryBridge>();

        Transform root = other.transform.root;

        if (bridge == null && root != null)
        {
            bridge = root.GetComponentInChildren<PlayerTradeInventoryBridge>(true);
        }

        if (bridge == null && matchedLayerTransform != null)
        {
            bridge = matchedLayerTransform.GetComponentInParent<PlayerTradeInventoryBridge>();

            if (bridge == null)
            {
                bridge = matchedLayerTransform.GetComponentInChildren<PlayerTradeInventoryBridge>(true);
            }
        }

        playerTradeInventory = bridge;

        if (bridge != null)
        {
            playerMove = bridge.GetComponent<PlayerMove>();
            equipmentVisualController =
                bridge.GetComponent<PlayerEquipmentVisualController>();

            if (playerMove == null)
            {
                playerMove = bridge.GetComponentInParent<PlayerMove>();
            }

            if (equipmentVisualController == null)
            {
                equipmentVisualController =
                    bridge.GetComponentInParent<PlayerEquipmentVisualController>();
            }

            Transform bridgeRoot = bridge.transform.root;

            if (bridgeRoot != null)
            {
                if (playerMove == null)
                {
                    playerMove = bridgeRoot.GetComponentInChildren<PlayerMove>(true);
                }

                if (equipmentVisualController == null)
                {
                    equipmentVisualController =
                        bridgeRoot.GetComponentInChildren<PlayerEquipmentVisualController>(true);
                }
            }
        }
        else
        {
            // LayerではPlayerと認識済み。Bridgeが別階層でも移動・武器制御だけは取得を試みる。
            playerMove = other.GetComponentInParent<PlayerMove>();
            equipmentVisualController =
                other.GetComponentInParent<PlayerEquipmentVisualController>();

            if (root != null)
            {
                if (playerMove == null)
                {
                    playerMove = root.GetComponentInChildren<PlayerMove>(true);
                }

                if (equipmentVisualController == null)
                {
                    equipmentVisualController =
                        root.GetComponentInChildren<PlayerEquipmentVisualController>(true);
                }
            }
        }

        if (verbose)
        {
            Log(
                $"Player Layer認識後の参照取得: " +
                $"Bridge={GetObjectName(playerTradeInventory)} / " +
                $"PlayerMove={GetObjectName(playerMove)} / " +
                $"EquipmentVisual={GetObjectName(equipmentVisualController)}"
            );
        }

        if (playerTradeInventory == null)
        {
            LogWarning(
                "Player Layerとしては正常に認識しましたが、" +
                "PlayerTradeInventoryBridgeが見つかりません。" +
                " 売買を開くにはPlayer本体へPlayerTradeInventoryBridgeを追加してください。"
            );
        }
    }

    private void EnsureDefaultPlayerLayer()
    {
        if (playerLayerMask.value != 0)
        {
            return;
        }

        int playerLayer = LayerMask.NameToLayer("Player");

        if (playerLayer >= 0)
        {
            playerLayerMask = 1 << playerLayer;
        }
    }

    private void BeginTradingState()
    {
        isTrading = true;
        SetPromptVisible(false);

        Log("BeginTradingState: Player操作をロックします。");

        if (disablePlayerMoveWhileTrading && playerMove != null)
        {
            playerMoveWasEnabled = playerMove.enabled;
            playerMove.enabled = false;
            Log($"PlayerMove.enabled: {playerMoveWasEnabled} → false");
        }

        if (lockWeaponWhileTrading && equipmentVisualController != null)
        {
            equipmentVisualController.SetWeaponControlLock(this, true);
            Log("WeaponControlLock=true");
        }

        extraBehaviourStates.Clear();

        foreach (Behaviour behaviour in extraBehavioursToDisable)
        {
            if (behaviour == null || behaviour == this)
            {
                continue;
            }

            extraBehaviourStates[behaviour] = behaviour.enabled;
            behaviour.enabled = false;
            Log($"追加Behaviour停止: {behaviour.GetType().Name} / {behaviour.name}");
        }

        previousCursorVisible = Cursor.visible;
        previousCursorLockMode = Cursor.lockState;

        if (showCursorWhileTrading)
        {
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
    }

    private void FinishTradingState()
    {
        if (!isTrading)
        {
            return;
        }

        isTrading = false;

        if (disablePlayerMoveWhileTrading &&
            playerMove != null &&
            playerMoveWasEnabled)
        {
            playerMove.enabled = true;
        }

        if (lockWeaponWhileTrading && equipmentVisualController != null)
        {
            equipmentVisualController.SetWeaponControlLock(this, false);
        }

        foreach (KeyValuePair<Behaviour, bool> pair in extraBehaviourStates)
        {
            if (pair.Key != null)
            {
                pair.Key.enabled = pair.Value;
            }
        }

        extraBehaviourStates.Clear();

        if (showCursorWhileTrading)
        {
            Cursor.visible = previousCursorVisible;
            Cursor.lockState = previousCursorLockMode;
        }

        SetPromptVisible(playerInside);
        Log("売買終了・Player操作を復元しました。");
    }

    private void FindReferences()
    {
        MerchantStockInventory previousStock = merchantStockInventory;
        PawnShopUIController previousUI = pawnShopUIController;

        if (merchantStockInventory == null)
        {
            merchantStockInventory =
                GetComponentInParent<MerchantStockInventory>();

            if (merchantStockInventory == null)
            {
                merchantStockInventory =
                    GetComponentInChildren<MerchantStockInventory>(true);
            }

            if (merchantStockInventory == null && transform.root != null)
            {
                merchantStockInventory =
                    transform.root.GetComponentInChildren<MerchantStockInventory>(true);
            }

            if (merchantStockInventory == null && allowSceneMerchantStockFallback)
            {
                merchantStockInventory = FindMerchantStockInventoryFromScene();
            }
        }

        if (pawnShopUIController == null)
        {
            pawnShopUIController =
                FindAnyObjectByType<PawnShopUIController>(
                    FindObjectsInactive.Include
                );
        }

        if (logReferenceSearch && showDebugLogs)
        {
            if (previousStock != merchantStockInventory || previousStock == null)
            {
                Log(
                    $"参照検索 MerchantStockInventory → {GetObjectName(merchantStockInventory)}"
                );
            }

            if (previousUI != pawnShopUIController || previousUI == null)
            {
                Log(
                    $"参照検索 PawnShopUIController → {GetObjectName(pawnShopUIController)}"
                );
            }
        }
    }

    private MerchantStockInventory FindMerchantStockInventoryFromScene()
    {
        MerchantStockInventory[] candidates =
            FindObjectsByType<MerchantStockInventory>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None
            );

        if (candidates == null || candidates.Length == 0)
        {
            if (showDebugLogs && logReferenceSearch)
            {
                LogWarning(
                    "MerchantStock自動補完: Scene内にもMerchantStockInventoryが見つかりません。"
                );
            }

            return null;
        }

        if (candidates.Length == 1)
        {
            MerchantStockInventory single = candidates[0];

            if (single != null)
            {
                LogWarning(
                    $"MerchantStock自動補完: Prefab階層外のStockを1件検出したため使用します。" +
                    $" Stock={single.name} / ShopName={single.ShopName}"
                );
            }

            return single;
        }

        if (!chooseNearestSceneStockWhenMultiple)
        {
            LogWarning(
                $"MerchantStock自動補完: Scene内に{candidates.Length}件のStockがあります。" +
                " Choose Nearest Scene Stock When MultipleがOFFのため自動選択しません。" +
                " CaveMerchant2DのMerchant Stock Inventoryへ対象を直接設定してください。"
            );
            return null;
        }

        MerchantStockInventory nearest = null;
        float nearestDistance = float.PositiveInfinity;
        Vector2 merchantPosition = transform.position;

        foreach (MerchantStockInventory candidate in candidates)
        {
            if (candidate == null)
            {
                continue;
            }

            float distance = Vector2.Distance(
                merchantPosition,
                candidate.transform.position
            );

            if (distance >= nearestDistance)
            {
                continue;
            }

            nearest = candidate;
            nearestDistance = distance;
        }

        if (nearest != null)
        {
            LogWarning(
                $"MerchantStock自動補完: Scene内に{candidates.Length}件の候補があるため、" +
                $"最も近いStockを使用します。 Stock={nearest.name} / " +
                $"ShopName={nearest.ShopName} / 距離={nearestDistance:0.00}"
            );
        }

        return nearest;
    }

    private void SetPromptVisible(bool visible)
    {
        if (interactionPromptText != null)
        {
            interactionPromptText.text = string.Format(
                interactionPromptFormat,
                interactionKey
            );
        }

        if (interactionPromptRoot != null &&
            interactionPromptRoot.activeSelf != visible)
        {
            interactionPromptRoot.SetActive(visible);
        }
    }

    /// <summary>
    /// InspectorのComponent右上メニューからも実行できます。
    /// 商人側のTrigger / Rigidbody / 主要参照を一括表示します。
    /// </summary>
    [ContextMenu("Cave Merchant / 診断ログを今すぐ出力")]
    public void PrintDiagnosticSnapshot()
    {
        FindReferences();

        Debug.Log(
            $"[CaveMerchant2D][診断] ===== {name} 診断開始 =====",
            this
        );

        Collider2D[] colliders =
            GetComponentsInChildren<Collider2D>(true);

        Rigidbody2D bodyInParent = GetComponentInParent<Rigidbody2D>();
        Rigidbody2D bodyInChildren = GetComponentInChildren<Rigidbody2D>(true);

        Debug.Log(
            $"[CaveMerchant2D][診断] Component: ActiveInHierarchy={gameObject.activeInHierarchy} / " +
            $"enabled={enabled} / interactionKey={interactionKey} / " +
            $"PlayerLayerMask={DescribeLayerMask(playerLayerMask)} / " +
            $"CheckParentLayers={checkParentLayers}",
            this
        );

        Debug.Log(
            $"[CaveMerchant2D][診断] 参照: MerchantStock={GetObjectName(merchantStockInventory)} / " +
            $"PawnShopUI={GetObjectName(pawnShopUIController)} / " +
            $"PromptRoot={GetObjectName(interactionPromptRoot)}",
            this
        );

        Debug.Log(
            $"[CaveMerchant2D][診断] Physics: 子を含むCollider2D数={colliders.Length} / " +
            $"Rigidbody2D(parent)={GetObjectName(bodyInParent)} / " +
            $"Rigidbody2D(children)={GetObjectName(bodyInChildren)}",
            this
        );

        bool hasTrigger = false;

        for (int i = 0; i < colliders.Length; i++)
        {
            Collider2D col = colliders[i];

            if (col == null)
            {
                continue;
            }

            hasTrigger |= col.isTrigger;

            Debug.Log(
                $"[CaveMerchant2D][診断] Collider[{i}]: " +
                $"Name={col.name} / Type={col.GetType().Name} / " +
                $"Enabled={col.enabled} / IsTrigger={col.isTrigger} / " +
                $"Layer={LayerMask.LayerToName(col.gameObject.layer)}({col.gameObject.layer}) / " +
                $"AttachedRB={GetObjectName(col.attachedRigidbody)}",
                col
            );
        }

        if (colliders.Length == 0)
        {
            Debug.LogWarning(
                "[CaveMerchant2D][診断] 商人オブジェクト配下にCollider2Dがありません。" +
                " Interaction用のCollider2Dを追加してください。",
                this
            );
        }
        else if (!hasTrigger)
        {
            Debug.LogWarning(
                "[CaveMerchant2D][診断] Is Trigger=true のCollider2Dがありません。" +
                " Interaction範囲用Collider2DのIs Triggerをオンにしてください。",
                this
            );
        }

        if (bodyInParent == null && bodyInChildren == null)
        {
            Debug.LogWarning(
                "[CaveMerchant2D][診断] 商人側にRigidbody2Dがありません。" +
                " Triggerイベントには通常、商人側またはPlayer側の少なくとも一方にRigidbody2Dが必要です。" +
                " Player側にRigidbody2Dがあるか確認してください。",
                this
            );
        }

        if (merchantStockInventory == null)
        {
            Debug.LogWarning(
                "[CaveMerchant2D][診断] MerchantStockInventoryが見つかりません。",
                this
            );
        }
        else
        {
            Debug.Log(
                $"[CaveMerchant2D][診断] MerchantStock: ShopName={merchantStockInventory.ShopName} / " +
                $"IsReady={merchantStockInventory.IsReady}",
                merchantStockInventory
            );
        }

        if (pawnShopUIController == null)
        {
            Debug.LogWarning(
                "[CaveMerchant2D][診断] PawnShopUIControllerがScene内に見つかりません。",
                this
            );
        }

        LogCurrentState("診断時状態");

        Debug.Log(
            $"[CaveMerchant2D][診断] ===== {name} 診断終了 =====",
            this
        );
    }


    /// <summary>
    /// Triggerイベントに依存せず、Scene内のCollider2DからPlayer Layer候補を探し、
    /// CaveMerchant2Dとの距離を表示します。
    /// </summary>
    [ContextMenu("Cave Merchant / Playerとの距離を今すぐ出力")]
    public void LogPlayerDistanceSnapshot()
    {
        Collider2D[] sceneColliders =
            FindObjectsByType<Collider2D>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        Collider2D nearestPlayerCollider = null;
        Transform nearestMatchedLayerTransform = null;
        float nearestDistanceToCollider = float.PositiveInfinity;
        float nearestTransformDistance = float.PositiveInfinity;

        Vector2 merchantPosition = transform.position;

        foreach (Collider2D candidate in sceneColliders)
        {
            if (candidate == null ||
                !candidate.enabled ||
                !candidate.gameObject.activeInHierarchy)
            {
                continue;
            }

            if (!IsPlayerLayer(candidate, out Transform matchedLayerTransform))
            {
                continue;
            }

            Vector2 closestPoint = candidate.ClosestPoint(merchantPosition);
            float colliderDistance = Vector2.Distance(merchantPosition, closestPoint);

            if (colliderDistance >= nearestDistanceToCollider)
            {
                continue;
            }

            nearestDistanceToCollider = colliderDistance;
            nearestPlayerCollider = candidate;
            nearestMatchedLayerTransform = matchedLayerTransform;
            nearestTransformDistance =
                matchedLayerTransform != null
                    ? Vector2.Distance(
                        merchantPosition,
                        matchedLayerTransform.position
                    )
                    : Vector2.Distance(
                        merchantPosition,
                        candidate.transform.position
                    );
        }

        // Player Layer Colliderが見つからない場合も、Bridgeが存在すれば位置だけ診断する。
        if (nearestPlayerCollider == null)
        {
            PlayerTradeInventoryBridge fallbackBridge =
                FindAnyObjectByType<PlayerTradeInventoryBridge>(
                    FindObjectsInactive.Exclude
                );

            if (fallbackBridge != null)
            {
                float fallbackDistance = Vector2.Distance(
                    merchantPosition,
                    fallbackBridge.transform.position
                );

                LogWarning(
                    $"[距離診断] Player Layerに一致するCollider2Dを発見できません。" +
                    $" ただしPlayerTradeInventoryBridge={fallbackBridge.name} は発見しました。" +
                    $" 商人位置={FormatVector2(merchantPosition)} / " +
                    $"Player位置={FormatVector2(fallbackBridge.transform.position)} / " +
                    $"Transform距離={fallbackDistance:0.00}。" +
                    " PlayerのCollider階層のLayer設定を確認してください。"
                );
                return;
            }

            LogWarning(
                $"[距離診断] Scene内にPlayer Layer Mask=" +
                $"{DescribeLayerMask(playerLayerMask)} に一致する有効なCollider2Dが見つかりません。"
            );
            return;
        }

        Vector2 playerClosestPoint =
            nearestPlayerCollider.ClosestPoint(merchantPosition);

        string matchedLayerName =
            nearestMatchedLayerTransform != null
                ? LayerMask.LayerToName(
                    nearestMatchedLayerTransform.gameObject.layer
                )
                : "null";

        int matchedLayer =
            nearestMatchedLayerTransform != null
                ? nearestMatchedLayerTransform.gameObject.layer
                : -1;

        string colliderLayerName =
            LayerMask.LayerToName(
                nearestPlayerCollider.gameObject.layer
            );

        string baseMessage =
            $"[距離診断] Player候補={nearestPlayerCollider.name} / " +
            $"Root={nearestPlayerCollider.transform.root.name} / " +
            $"判定LayerObject={GetObjectName(nearestMatchedLayerTransform)} / " +
            $"判定Layer={matchedLayerName}({matchedLayer}) / " +
            $"実ColliderLayer={colliderLayerName}({nearestPlayerCollider.gameObject.layer}) / " +
            $"商人位置={FormatVector2(merchantPosition)} / " +
            $"Player位置={FormatVector2(nearestMatchedLayerTransform != null ? nearestMatchedLayerTransform.position : nearestPlayerCollider.transform.position)} / " +
            $"Transform距離={nearestTransformDistance:0.00} / " +
            $"PlayerCollider最近点={FormatVector2(playerClosestPoint)} / " +
            $"Colliderまでの距離={nearestDistanceToCollider:0.00} / " +
            $"playerInside={playerInside} / 登録Collider数={playerCollidersInside.Count}";

        if (!logDistancePhysicsDetails)
        {
            Log(baseMessage);
            return;
        }

        Collider2D merchantTrigger = FindInteractionTriggerCollider();

        if (merchantTrigger == null)
        {
            LogWarning(
                baseMessage +
                " / 商人側の有効なIsTrigger Collider2Dが見つかりません。"
            );
            return;
        }

        bool boundsOverlap =
            merchantTrigger.bounds.Intersects(
                nearestPlayerCollider.bounds
            );

        int merchantColliderLayer = merchantTrigger.gameObject.layer;
        int playerColliderLayer = nearestPlayerCollider.gameObject.layer;
        bool layerCollisionIgnored =
            Physics2D.GetIgnoreLayerCollision(
                merchantColliderLayer,
                playerColliderLayer
            );

        string merchantLayerName =
            LayerMask.LayerToName(merchantColliderLayer);

        bool playerHasRigidbody =
            nearestPlayerCollider.attachedRigidbody != null;
        bool merchantHasRigidbody =
            merchantTrigger.attachedRigidbody != null;

        Log(
            baseMessage +
            $" / Trigger={merchantTrigger.name}" +
            $" / TriggerLayer={merchantLayerName}({merchantColliderLayer})" +
            $" / TriggerBoundsCenter={FormatVector2(merchantTrigger.bounds.center)}" +
            $" / TriggerBoundsSize={FormatVector2(merchantTrigger.bounds.size)}" +
            $" / PlayerBoundsCenter={FormatVector2(nearestPlayerCollider.bounds.center)}" +
            $" / PlayerBoundsSize={FormatVector2(nearestPlayerCollider.bounds.size)}" +
            $" / BoundsOverlap={boundsOverlap}" +
            $" / LayerCollisionIgnored={layerCollisionIgnored}" +
            $" / PlayerRB={playerHasRigidbody}" +
            $" / MerchantTriggerRB={merchantHasRigidbody}"
        );
    }

    private Collider2D FindInteractionTriggerCollider()
    {
        Collider2D ownCollider = GetComponent<Collider2D>();

        if (ownCollider != null &&
            ownCollider.enabled &&
            ownCollider.isTrigger)
        {
            return ownCollider;
        }

        Collider2D[] parentColliders =
            GetComponentsInParent<Collider2D>(true);

        foreach (Collider2D col in parentColliders)
        {
            if (col != null && col.enabled && col.isTrigger)
            {
                return col;
            }
        }

        Collider2D[] childColliders =
            GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D col in childColliders)
        {
            if (col != null && col.enabled && col.isTrigger)
            {
                return col;
            }
        }

        return null;
    }

    private static string FormatVector2(Vector2 value)
    {
        return $"({value.x:0.00}, {value.y:0.00})";
    }

    private void LogCurrentState(string label)
    {
        Log(
            $"{label}: playerInside={playerInside} / fallbackInside={fallbackPlayerInside} / " +
            $"isTrading={isTrading} / PlayerBridge={GetObjectName(playerTradeInventory)} / " +
            $"PlayerColliderCount={playerCollidersInside.Count} / " +
            $"Stock={GetObjectName(merchantStockInventory)} / " +
            $"PawnShopUI={GetObjectName(pawnShopUIController)}"
        );
    }

    private void LogTrigger(string eventName, Collider2D other)
    {
        Log($"{eventName}: {DescribeCollider(other)}");
    }

    private static string DescribeLayerMask(LayerMask mask)
    {
        if (mask.value == 0)
        {
            return "None(0)";
        }

        List<string> names = new List<string>();

        for (int layer = 0; layer < 32; layer++)
        {
            if ((mask.value & (1 << layer)) == 0)
            {
                continue;
            }

            string layerName = LayerMask.LayerToName(layer);
            names.Add(
                string.IsNullOrWhiteSpace(layerName)
                    ? layer.ToString()
                    : $"{layerName}({layer})"
            );
        }

        return string.Join(", ", names);
    }

    private static string DescribeCollider(Collider2D col)
    {
        if (col == null)
        {
            return "Collider=null";
        }

        string layerName = LayerMask.LayerToName(col.gameObject.layer);

        return
            $"Name={col.name} / Root={col.transform.root.name} / " +
            $"Tag={col.tag} / Layer={layerName}({col.gameObject.layer}) / " +
            $"Type={col.GetType().Name} / IsTrigger={col.isTrigger} / " +
            $"AttachedRB={GetObjectName(col.attachedRigidbody)}";
    }

    private static string GetObjectName(Object obj)
    {
        return obj != null ? obj.name : "null";
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[CaveMerchant2D] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[CaveMerchant2D] {message}", this);
    }
}
