using System.Collections.Generic;
using UnityEngine.Localization;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class WorldItemPickup : MonoBehaviour
{
    // 同じフレーム内で、地面ItemのE入力を複数のInteractableが
    // 二重に処理しないための共有フラグ。
    private static int pickupInputConsumedFrame = -1;

    /// <summary>
    /// このフレームのE入力が、すでに地面Itemの拾得へ使われたか。
    /// ItemBoxなど、同じキーを使うInteractableから確認できます。
    /// </summary>
    public static bool WasPickupInputConsumedThisFrame =>
        pickupInputConsumedFrame == Time.frameCount;

    [Header("拾う設定")]
    [SerializeField] private KeyCode pickupKey = KeyCode.E;

    [Tooltip("捨てた直後に、すぐ拾い直さないための待ち時間")]
    [SerializeField, Min(0f)] private float pickupDelay = 0.25f;

    [Header("拾う表示")]
    [SerializeField] private TMP_Text pickupPromptText;

    [Header("拾う表示の翻訳")]
    [Tooltip("GameText の world.pickup を設定")]
    [SerializeField]
    private LocalizedString pickupPromptLabel =
        new LocalizedString();

    private string localizedPickupPromptLabel = "拾う";
    private bool isPickupPromptLabelSubscribed;

    [SerializeField]
    private Vector3 pickupPromptLocalPosition =
        new Vector3(0f, 0.85f, 0f);

    [SerializeField] private bool hidePromptDuringPickupDelay = true;

    [Header("スタック数表示")]
    [Tooltip("DroppedItemの子に置いた StackAmountText を設定")]
    [SerializeField] private TMP_Text stackAmountText;

    [SerializeField] private string stackAmountPrefix = "×";

    [SerializeField]
    private Vector3 stackAmountLocalPosition =
        new Vector3(0.35f, -0.25f, 0f);

    [Tooltip("1個だけの時は個数を表示しない")]
    [SerializeField]
    private bool showStackAmountOnlyWhenMoreThanOne = true;

    [Header("サウンド")]
    [SerializeField] private AudioClip dropSound;
    [SerializeField] private AudioClip pickupSound;

    [SerializeField, Range(0f, 1f)]
    private float dropSoundVolume = 0.8f;

    [SerializeField, Range(0f, 1f)]
    private float pickupSoundVolume = 0.8f;

    [SerializeField, Range(0f, 1f)]
    private float soundSpatialBlend = 0f;

    [Header("参照")]
    [SerializeField] private SpriteRenderer itemSpriteRenderer;
    [SerializeField] private InventoryController inventoryController;

    [Tooltip(
        "武器未装備時に拾った武器をPrimaryWeaponへ自動装備するための参照です。" +
        "未設定なら自動取得します。"
    )]
    [SerializeField] private EquipmentController equipmentController;

    [Tooltip("物を持つ操作へEキーを優先するための参照です。未設定なら自動取得します")]
    [SerializeField] private PlayerCarryController2D carryController;

    [Header("武器の自動装備")]
    [Tooltip(
        "オンの場合、PrimaryWeaponが空の時に地面の武器を拾うと、" +
        "通常Inventoryへ入れず直接PrimaryWeaponへ装備します。"
    )]
    [SerializeField] private bool autoEquipWeaponWhenEmpty = true;

    [Tooltip(
        "自動装備が成功した時にConsoleへログを表示します。"
    )]
    [SerializeField] private bool logAutoEquipWeapon = true;

    [Header("プレイヤー判定")]
    [Tooltip("プレイヤーがアイテムを拾える範囲として使用するCollider2Dです。DroppedItemの子に作成したPickupRangeを設定してください。PlayerとDroppedItemのLayer CollisionをOFFにしていても、この範囲をOverlap判定して拾得できます。")]
    [SerializeField] private Collider2D pickupRange;

    [SerializeField] private string playerTag = "Player";

    private readonly HashSet<Collider2D> playerColliders =
        new HashSet<Collider2D>();

    private readonly List<Collider2D> pickupRangeOverlapResults =
        new List<Collider2D>(16);

    private bool isPlayerInRange;

    // PickupRangeと重なっている実際のPlayerを保持し、
    // 同じシーンに別のInventoryControllerがあっても誤収納しない。
    private Collider2D playerColliderInRange;

    [Header("拾得の診断 v1.2")]
    [Tooltip("Eキー拾得の収納先、所持数変化、失敗理由をConsoleへ表示します")]
    [SerializeField] private bool showPickupDebugLogs = true;

    private InventoryItem droppedItem;
    private float canPickupAfterTime;
    private bool isPickingUp;

    private enum StackPickupResult
    {
        Failed,
        Partial,
        Complete
    }

    public InventoryItem DroppedItem => droppedItem;

    public bool IsPlayerInRange => isPlayerInRange;

    public bool HasValidDroppedItem =>
    droppedItem != null &&
    droppedItem.ItemData != null;

    public WorldItemSaveData CreateSaveData()
    {
        if (!HasValidDroppedItem)
        {
            return null;
        }

        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        Vector2 velocity = rb != null
            ? rb.linearVelocity
            : Vector2.zero;

        Vector3 position = transform.position;

        return new WorldItemSaveData
        {
            itemId = droppedItem.ItemData.ItemId,
            amount = droppedItem.Amount,
            isRotated = droppedItem.IsRotated,
            hasFourWayRotation = true,
            rotationSteps = droppedItem.RotationSteps,

            hasStoredMagazineAmmo =
                droppedItem.HasStoredMagazineAmmo,

            storedMagazineAmmo =
                droppedItem.StoredMagazineAmmo,

            positionX = position.x,
            positionY = position.y,
            positionZ = position.z,

            velocityX = velocity.x,
            velocityY = velocity.y
        };
    }

    public bool RestoreFromSaveData(
        WorldItemSaveData saveData,
        ItemData itemData)
    {
        if (saveData == null || itemData == null)
        {
            return false;
        }

        int amount = Mathf.Clamp(
            saveData.amount,
            1,
            itemData.MaxStack
        );

        InventoryItem restoredItem = new InventoryItem(
            itemData,
            0,
            0,
            amount
        );

        restoredItem.SetRotationSteps(saveData.ResolvedRotationSteps);

        if (saveData.hasStoredMagazineAmmo)
        {
            restoredItem.SetStoredMagazineAmmo(
                saveData.storedMagazineAmmo
            );
        }

        droppedItem = restoredItem;

        transform.position = new Vector3(
            saveData.positionX,
            saveData.positionY,
            saveData.positionZ
        );

        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = new Vector2(
                saveData.velocityX,
                saveData.velocityY
            );

            rb.angularVelocity = 0f;
        }

        // ロード後はすぐ拾える状態にする
        canPickupAfterTime = Time.time;
        isPickingUp = false;
        RefreshPlayerInRangeState();

        RefreshVisual();
        RefreshPickupPrompt();
        RefreshStackAmountText();

        return true;
    }

    private void Awake()
    {
        if (itemSpriteRenderer == null)
        {
            itemSpriteRenderer = GetComponent<SpriteRenderer>();
        }

        FindPickupPromptText();
        FindStackAmountText();
        FindPickupRange();

        ApplyPickupPromptPosition();
        ApplyStackAmountPosition();

        FindInventoryController();
        FindEquipmentController();
        RefreshPlayerInRangeState();

        RefreshVisual();
        RefreshPickupPrompt();
        RefreshStackAmountText();
    }

    private void OnEnable()
    {
        SubscribePickupPromptLabel();
    }

    private void OnDisable()
    {
        UnsubscribePickupPromptLabel();
    }

    private void SubscribePickupPromptLabel()
    {
        if (isPickupPromptLabelSubscribed ||
            pickupPromptLabel == null)
        {
            return;
        }

        pickupPromptLabel.StringChanged +=
            HandlePickupPromptLabelChanged;

        isPickupPromptLabelSubscribed = true;
    }

    private void UnsubscribePickupPromptLabel()
    {
        if (!isPickupPromptLabelSubscribed ||
            pickupPromptLabel == null)
        {
            return;
        }

        pickupPromptLabel.StringChanged -=
            HandlePickupPromptLabelChanged;

        isPickupPromptLabelSubscribed = false;
    }

    private void HandlePickupPromptLabelChanged(
        string localizedLabel)
    {
        localizedPickupPromptLabel =
            string.IsNullOrWhiteSpace(localizedLabel)
                ? "拾う"
                : localizedLabel;

        RefreshPickupPrompt();
    }

    private void Update()
    {
        RefreshPlayerInRangeState();
        RefreshPickupPrompt();

        // 持ち運び操作がEキーを使用している間は、
        // 地面アイテムの拾得入力を受け取りません。
        bool carryBlocked = IsCarryInteractionBlockingPickup();
        if (carryBlocked)
        {
            if (showPickupDebugLogs && isPlayerInRange && Input.GetKeyDown(pickupKey))
            {
                Debug.LogWarning(
                    $"[WorldPickup][P02] 拾得が別操作によりブロックされています。" +
                    $" Carry={carryBlocked} / Item={GetDroppedItemLabel()}",
                    this
                );
            }
            return;
        }

        if (droppedItem == null ||
            droppedItem.ItemData == null ||
            isPickingUp ||
            !IsPlayerInRange ||
            Time.time < canPickupAfterTime)
        {
            return;
        }

        if (Input.GetKeyDown(pickupKey) &&
            !WasPickupInputConsumedThisFrame)
        {
            // 複数Itemが重なっていても、
            // Playerに一番近い拾得可能Itemだけを1個処理する。
            TryHandleNearestPickupInput(pickupKey);
        }
    }

    /// <summary>
    /// 指定キーで拾える地面Itemが現在存在するか確認します。
    /// ItemBoxの「E:開ける」表示を、Item拾得が優先される間だけ
    /// 隠すためにも使用します。
    /// </summary>
    public static bool HasPickupCandidateForKey(KeyCode key)
    {
        return FindNearestPickupCandidate(key) != null;
    }

    /// <summary>
    /// 指定キーで拾えるItemの中からPlayerに一番近い1個を選び、
    /// そのItemへ入力を消費させます。
    ///
    /// true：
    /// 拾得対象が存在し、このフレームの入力をItem拾得へ使用した。
    ///
    /// Inventory満杯などで実際の拾得に失敗しても、
    /// ItemBoxを同じE入力で開かないよう入力自体は消費します。
    /// </summary>
    public static bool TryHandleNearestPickupInput(KeyCode key)
    {
        if (WasPickupInputConsumedThisFrame)
        {
            return true;
        }

        WorldItemPickup candidate =
            FindNearestPickupCandidate(key);

        if (candidate == null)
        {
            return false;
        }

        pickupInputConsumedFrame = Time.frameCount;

        candidate.TryPickup();
        return true;
    }

    /// <summary>
    /// ItemBoxなど外部Interactableから、
    /// このItemが「今Eで拾える状態か」を確認するための判定です。
    /// 運搬・拾得Delayも従来どおり尊重します。
    /// </summary>
    public bool CanReceivePickupInputNow()
    {
        RefreshPlayerInRangeState();

        return droppedItem != null &&
               droppedItem.ItemData != null &&
               !isPickingUp &&
               IsPlayerInRange &&
               Time.time >= canPickupAfterTime &&
               
               !IsCarryInteractionBlockingPickup();
    }

    private static WorldItemPickup FindNearestPickupCandidate(
        KeyCode key)
    {
        WorldItemPickup[] pickups =
            FindObjectsByType<WorldItemPickup>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None
            );

        if (pickups == null || pickups.Length == 0)
        {
            return null;
        }

        PlayerMove player =
            FindAnyObjectByType<PlayerMove>(
                FindObjectsInactive.Exclude
            );

        Vector3 playerPosition =
            player != null
                ? player.transform.position
                : Vector3.zero;

        WorldItemPickup nearest = null;
        float nearestSqrDistance = float.PositiveInfinity;

        foreach (WorldItemPickup pickup in pickups)
        {
            if (pickup == null ||
                pickup.pickupKey != key ||
                !pickup.CanReceivePickupInputNow())
            {
                continue;
            }

            float sqrDistance;

            if (player != null)
            {
                sqrDistance =
                    (pickup.transform.position - playerPosition)
                    .sqrMagnitude;
            }
            else
            {
                // PlayerMoveが見つからない場合でも、
                // 候補が1個なら正常に選べるようにする。
                sqrDistance = nearest == null
                    ? 0f
                    : 1f;
            }

            if (nearest != null &&
                sqrDistance >= nearestSqrDistance)
            {
                continue;
            }

            nearest = pickup;
            nearestSqrDistance = sqrDistance;
        }

        return nearest;
    }

    public void Setup(InventoryItem item)
    {
        Setup(item, true);
    }

    /// <summary>
    /// シーンに最初から置くアイテムなど、生成音を鳴らしたくない場合は
    /// playDropSound=falseで初期化できます。
    /// </summary>
    public void Setup(InventoryItem item, bool playDropSound)
    {
        droppedItem = item;
        canPickupAfterTime = Time.time + pickupDelay;
        RefreshPlayerInRangeState();

        RefreshVisual();
        RefreshPickupPrompt();
        RefreshStackAmountText();

        if (playDropSound)
        {
            PlayWorldSound(dropSound, dropSoundVolume);
        }
    }

    public void SetVelocity(Vector2 velocity)
    {
        Rigidbody2D rb = GetComponent<Rigidbody2D>();

        if (rb != null)
        {
            rb.linearVelocity = velocity;
        }
    }

    public bool TryPickup()
    {
        if (droppedItem == null ||
            droppedItem.ItemData == null ||
            isPickingUp)
        {
            return false;
        }

        // スキルカードは通常インベントリへ入れず、
        // GameSessionManagerのスキルコレクションへ永久登録する。
        if (droppedItem.ItemData is SkillCardData skillCardData)
        {
            return TryPickupSkillCard(skillCardData);
        }

        // 武器自動装備より先に、拾ったPlayerのInventoryを解決する。
        // Prefabに残った参照やFindAnyObjectByTypeの別Inventoryを優先しない。
        if (!FindInventoryController())
        {
            Debug.LogWarning(
                $"[WorldPickup][P03] InventoryControllerを特定できません。" +
                $" Item={GetDroppedItemLabel()} / PlayerCollider={GetPlayerColliderLabel()}",
                this
            );
            return false;
        }

        ItemData pickupItemData = droppedItem.ItemData;
        string pickupItemName = pickupItemData.DisplayName;
        int amountBefore = droppedItem.Amount;
        int inventoryAmountBefore = inventoryController.GetTotalAmount(pickupItemData);
        if (showPickupDebugLogs)
        {
            Debug.Log(
                $"[WorldPickup][P10] 拾得開始 Item={pickupItemName} x{amountBefore} " +
                $"/ Inventory={GetHierarchyPath(inventoryController.transform)} " +
                $"(ID={inventoryController.GetInstanceID()}) " +
                $"/ 収納前={inventoryAmountBefore} / PlayerCollider={GetPlayerColliderLabel()}",
                this
            );
            DiagnoseInventoryUiLink();
        }

        // PrimaryWeaponが空なら武器を自動装備（元の機能を維持）。
        if (TryAutoEquipPickedWeapon())
        {
            if (showPickupDebugLogs)
                Debug.Log($"[WorldPickup][P12] 武器を直接装備: {pickupItemName}", this);
            return true;
        }

        isPickingUp = true;

        StackPickupResult result;

        if (droppedItem.CanStack)
        {
            result = TryPickupStackableItem();
        }
        else
        {
            result = TryPickupUniqueItem()
                ? StackPickupResult.Complete
                : StackPickupResult.Failed;
        }

        if (result == StackPickupResult.Failed)
        {
            isPickingUp = false;

            RefreshPickupPrompt();
            RefreshStackAmountText();

            Debug.LogWarning(
                $"[WorldPickup][P20] 拾得失敗: {pickupItemName} " +
                $"/ 収納先={GetHierarchyPath(inventoryController.transform)} " +
                $"/ 空きマス・スタック上限・別Inventory参照を確認してください。",
                this
            );

            return false;
        }

        int inventoryAmountAfter = inventoryController.GetTotalAmount(pickupItemData);
        int actuallyAdded = inventoryAmountAfter - inventoryAmountBefore;
        if (actuallyAdded <= 0)
        {
            // 拾得が成功したのに追加先の数量が増えていなければ、
            // 別のシステムによる即時消費や参照違いなどの可能性を残す。
            Debug.LogWarning(
                $"[WorldPickup][P21] 要調査: 拾得成功判定だが個数変化がありません。" +
                $" Item={pickupItemName} / {inventoryAmountBefore} -> {inventoryAmountAfter}" +
                $" / Inventory={GetHierarchyPath(inventoryController.transform)}",
                this
            );
        }
        else if (showPickupDebugLogs)
        {
            Debug.Log(
                $"[WorldPickup][P11] 収納成功 Item={pickupItemName} +{actuallyAdded}" +
                $" / {inventoryAmountBefore} -> {inventoryAmountAfter}" +
                $" / Inventory={GetHierarchyPath(inventoryController.transform)}",
                this
            );
        }

        PlayWorldSound(pickupSound, pickupSoundVolume);

        // 一部しか拾えなかった時は、
        // 残りを地面に残して数字だけ更新する
        if (result == StackPickupResult.Partial)
        {
            isPickingUp = false;

            RefreshVisual();
            RefreshPickupPrompt();
            RefreshStackAmountText();

            return true;
        }

        if (pickupPromptText != null)
        {
            pickupPromptText.enabled = false;
        }

        if (stackAmountText != null)
        {
            stackAmountText.enabled = false;
        }

        Destroy(gameObject);
        return true;
    }

    /// <summary>
    /// 武器未装備時だけ、地面の武器を直接PrimaryWeaponへ装備します。
    ///
    /// falseの場合は失敗とは限りません。
    /// 「武器ではない」「すでに武器装備済み」などの場合もfalseを返し、
    /// 呼び出し元が従来の通常Inventory拾得へフォールバックします。
    /// </summary>
    private bool TryAutoEquipPickedWeapon()
    {
        if (!autoEquipWeaponWhenEmpty ||
            droppedItem == null ||
            droppedItem.ItemData == null ||
            droppedItem.ItemData is not WeaponItemData)
        {
            return false;
        }

        // 武器は1個単位で装備する前提。
        // 万一Stack状態なら従来Inventory処理へ回す。
        if (droppedItem.Amount != 1)
        {
            return false;
        }

        if (!FindEquipmentController())
        {
            return false;
        }

        // すでにPrimaryWeaponがある時は交換せず、
        // 今まで通り通常Inventoryへ拾う。
        if (equipmentController.IsSlotOccupied(
                EquipmentSlotType.PrimaryWeapon))
        {
            return false;
        }

        isPickingUp = true;

        // WorldItemが持っている同じInventoryItem個体を直接渡す。
        InventoryItem weaponItem = droppedItem;

        if (!equipmentController.TryEquipExternalItem(
                weaponItem,
                out EquipmentResult equipResult))
        {
            isPickingUp = false;

            if (logAutoEquipWeapon)
            {
                Debug.LogWarning(
                    $"WorldItemPickup: 武器の自動装備に失敗しました。" +
                    $" Item={weaponItem.ItemData.DisplayName}" +
                    $" / Result={equipResult}",
                    this
                );
            }

            return false;
        }

        // EquipmentControllerが同じ個体を保持したので、
        // WorldItem側から参照を外して地面Itemとして保存されないようにする。
        droppedItem = null;

        PlayWorldSound(
            pickupSound,
            pickupSoundVolume
        );

        if (pickupPromptText != null)
        {
            pickupPromptText.enabled = false;
        }

        if (stackAmountText != null)
        {
            stackAmountText.enabled = false;
        }

        if (logAutoEquipWeapon)
        {
            Debug.Log(
                $"武器を拾って自動装備しました：" +
                $"{weaponItem.ItemData.DisplayName}",
                this
            );
        }

        Destroy(gameObject);
        return true;
    }

    private bool TryPickupSkillCard(SkillCardData skillCardData)
    {
        if (skillCardData == null)
        {
            return false;
        }

        GameSessionManager session = GameSessionManager.Instance;

        if (session == null)
        {
            session = FindAnyObjectByType<GameSessionManager>(
                FindObjectsInactive.Include
            );
        }

        if (session == null)
        {
            Debug.LogWarning(
                "WorldItemPickup: SkillCardを取得できません。GameSessionManagerが見つかりません。",
                this
            );
            return false;
        }

        isPickingUp = true;

        if (!session.UnlockSkillCard(
                skillCardData,
                out bool wasNewlyUnlocked))
        {
            isPickingUp = false;
            return false;
        }

        PlayWorldSound(pickupSound, pickupSoundVolume);

        if (pickupPromptText != null)
        {
            pickupPromptText.enabled = false;
        }

        if (stackAmountText != null)
        {
            stackAmountText.enabled = false;
        }

        Debug.Log(
            wasNewlyUnlocked
                ? $"スキルカードを取得しました：{skillCardData.DisplayName}"
                : $"取得済みのスキルカードです：{skillCardData.DisplayName}",
            this
        );

        Destroy(gameObject);
        return true;
    }

    private StackPickupResult TryPickupStackableItem()
    {
        int amountBeforePickup = droppedItem.Amount;

        inventoryController.TryAddItem(
            droppedItem.ItemData,
            amountBeforePickup,
            out int remainingAmount
        );

        int pickedUpAmount =
            amountBeforePickup - remainingAmount;

        if (pickedUpAmount <= 0)
        {
            return StackPickupResult.Failed;
        }

        droppedItem.RemoveAmount(pickedUpAmount);

        if (droppedItem.IsEmpty())
        {
            return StackPickupResult.Complete;
        }

        return StackPickupResult.Partial;
    }

    private bool TryPickupUniqueItem()
    {
        InventoryGrid grid = inventoryController.Grid;

        bool currentRotation =
            droppedItem.CanRotate &&
            droppedItem.IsRotated;

        if (TryFindSpace(
                grid,
                droppedItem,
                currentRotation,
                out Vector2Int position))
        {
            return inventoryController.TryMoveItem(
                droppedItem,
                position.x,
                position.y,
                currentRotation
            );
        }

        if (droppedItem.CanRotate)
        {
            bool alternateRotation = !currentRotation;

            if (TryFindSpace(
                    grid,
                    droppedItem,
                    alternateRotation,
                    out position))
            {
                return inventoryController.TryMoveItem(
                    droppedItem,
                    position.x,
                    position.y,
                    alternateRotation
                );
            }
        }

        return false;
    }

    private bool TryFindSpace(
        InventoryGrid grid,
        InventoryItem item,
        bool isRotated,
        out Vector2Int position)
    {
        position = Vector2Int.zero;

        if (grid == null || item == null)
        {
            return false;
        }

        for (int y = 0; y < grid.Height; y++)
        {
            for (int x = 0; x < grid.Width; x++)
            {
                if (!grid.CanPlaceItem(
                        item,
                        x,
                        y,
                        isRotated))
                {
                    continue;
                }

                position = new Vector2Int(x, y);
                return true;
            }
        }

        return false;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // PickupRangeが未設定の場合だけ、従来のTrigger方式を予備として使用します。
        if (pickupRange != null || !IsPlayerCollider(other))
        {
            return;
        }

        playerColliders.Add(other);
        playerColliderInRange = other;
        isPlayerInRange = playerColliders.Count > 0;
        RefreshPickupPrompt();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (pickupRange != null)
        {
            return;
        }

        playerColliders.Remove(other);
        if (playerColliderInRange == other)
        {
            playerColliderInRange = null;
            foreach (Collider2D remaining in playerColliders)
            {
                if (remaining != null && IsPlayerCollider(remaining))
                {
                    playerColliderInRange = remaining;
                    break;
                }
            }
        }
        isPlayerInRange = playerColliders.Count > 0;
        RefreshPickupPrompt();
    }

    /// <summary>
    /// PickupRangeに重なっているColliderを直接検索します。
    /// Layer Collision MatrixでPlayerとDroppedItemの衝突をOFFにしていても、
    /// 物理的に押し合わずに拾得範囲だけ判定できます。
    /// </summary>
    private void RefreshPlayerInRangeState()
    {
        if (pickupRange == null)
        {
            isPlayerInRange = playerColliders.Count > 0;
            if (!isPlayerInRange)
                playerColliderInRange = null;
            return;
        }

        if (!pickupRange.enabled ||
            !pickupRange.gameObject.activeInHierarchy)
        {
            isPlayerInRange = false;
            playerColliderInRange = null;
            return;
        }

        pickupRangeOverlapResults.Clear();

        ContactFilter2D filter =
            new ContactFilter2D().NoFilter();

        Physics2D.OverlapCollider(
            pickupRange,
            filter,
            pickupRangeOverlapResults
        );

        isPlayerInRange = false;
        playerColliderInRange = null;
        float closestDistance = float.PositiveInfinity;

        foreach (Collider2D other in pickupRangeOverlapResults)
        {
            if (other == null || other == pickupRange || !IsPlayerCollider(other))
                continue;

            float distance = (other.ClosestPoint(transform.position) -
                              (Vector2)transform.position).sqrMagnitude;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                playerColliderInRange = other;
            }
            isPlayerInRange = true;
        }
    }

    private void FindPickupRange()
    {
        if (pickupRange != null)
        {
            return;
        }

        Collider2D[] colliders =
            GetComponentsInChildren<Collider2D>(true);

        foreach (Collider2D collider in colliders)
        {
            if (collider != null &&
                collider.gameObject.name == "PickupRange")
            {
                pickupRange = collider;
                return;
            }
        }
    }

    private bool IsPlayerCollider(Collider2D other)
    {
        if (other == null)
        {
            return false;
        }

        if (other.CompareTag(playerTag))
        {
            return true;
        }

        if (other.transform.root.CompareTag(playerTag))
        {
            return true;
        }

        return other.GetComponentInParent<PlayerMove>() != null;
    }

    private bool IsCarryInteractionBlockingPickup()
    {
        if (carryController == null)
        {
            carryController =
                FindAnyObjectByType<PlayerCarryController2D>();
        }

        return carryController != null &&
               carryController.BlocksWorldItemPickup;
    }

    private bool FindEquipmentController()
    {
        // 自動装備は拾得先Inventoryと同じプレイヤーの装備へ行う。
        if (inventoryController != null)
        {
            EquipmentController owned =
                inventoryController.GetComponent<EquipmentController>();
            if (owned == null)
                owned = inventoryController.GetComponentInParent<EquipmentController>();
            if (owned == null)
                owned = inventoryController.GetComponentInChildren<EquipmentController>(true);

            if (owned != null)
            {
                equipmentController = owned;
                return true;
            }
        }

        if (equipmentController != null)
            return true;

        equipmentController = FindAnyObjectByType<EquipmentController>(
            FindObjectsInactive.Include);
        return equipmentController != null;
    }

    private bool FindInventoryController()
    {
        // v1.3: インベントリはPlayerとは別のManagerにある構成が正しい。
        // 画面のInventoryGridUIが使用するControllerを「正」として最優先する。
        // Prefabに以前のPlayer参照が残っていても、毎回UIの参照で上書きする。
        InventoryController fromUi = GetPlayerGridUiInventory();
        if (fromUi != null)
        {
            if (inventoryController != fromUi &&
                showPickupDebugLogs && playerColliderInRange != null)
            {
                Debug.Log(
                    $"[WorldPickup][P26] UIに合わせて収納先を修正: " +
                    $"{(inventoryController != null ? GetHierarchyPath(inventoryController.transform) : "未設定")} " +
                    $"-> {GetHierarchyPath(fromUi.transform)}",
                    this);
            }

            inventoryController = fromUi;
            return true;
        }

        // UIがまだ生成されていない特殊なシーンのみ、Player側を予備として確認。
        InventoryController fromPlayer = GetPlayerInventoryFromRange();
        if (fromPlayer != null)
        {
            inventoryController = fromPlayer;
            if (showPickupDebugLogs && playerColliderInRange != null)
                Debug.LogWarning(
                    "[WorldPickup][P27] InventoryGridUIの参照を見つけられないため、" +
                    "Player側のInventoryControllerを予備として使用しています。",
                    this);
            return true;
        }

        if (inventoryController != null)
            return true;

        // 最終的な予備。対象が1個に絞れないときは警告を出す。
        InventoryController[] all = FindObjectsByType<InventoryController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (all.Length == 1)
        {
            inventoryController = all[0];
            return true;
        }

        if (all.Length > 1 && showPickupDebugLogs)
            Debug.LogWarning(
                $"[WorldPickup][P23] 複数のInventoryController({all.Length}件)があるため、" +
                "拾得先を一意に選べません。PlayerまたはInventoryGridUIの参照を確認してください。",
                this);
        return false;
    }

    private InventoryController GetPlayerInventoryFromRange()
    {
        Collider2D col = playerColliderInRange;
        if (col == null)
            return null;

        PlayerMove playerMove = col.GetComponentInParent<PlayerMove>();
        if (playerMove != null)
        {
            InventoryController owned = playerMove.GetComponent<InventoryController>();
            if (owned == null)
                owned = playerMove.GetComponentInChildren<InventoryController>(true);
            if (owned == null)
                owned = playerMove.GetComponentInParent<InventoryController>();
            if (owned != null)
                return owned;
        }

        InventoryController onCollider = col.GetComponentInParent<InventoryController>();
        if (onCollider != null)
            return onCollider;

        Transform root = col.transform.root;
        return root != null
            ? root.GetComponentInChildren<InventoryController>(true)
            : null;
    }

    private InventoryController GetPlayerGridUiInventory()
    {
        InventoryGridUI[] grids = FindObjectsByType<InventoryGridUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        InventoryController found = null;
        foreach (InventoryGridUI grid in grids)
        {
            if (grid == null || grid.ItemBox != null || grid.Controller == null)
                continue;

            if (found == null)
                found = grid.Controller;
            else if (found != grid.Controller)
            {
                // 画面内にプレイヤー用の別Inventoryが複数あるなら、
                // 先頭を任意に選ばず診断へ回す。
                if (showPickupDebugLogs)
                    Debug.LogWarning(
                        "[WorldPickup][P24] 複数のプレイヤー用InventoryGridUIで" +
                        "異なるInventoryControllerが設定されています。",
                        this);
                return null;
            }
        }
        return found;
    }

    private void DiagnoseInventoryUiLink()
    {
        InventoryGridUI[] grids = FindObjectsByType<InventoryGridUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (InventoryGridUI grid in grids)
        {
            if (grid == null || grid.ItemBox != null || grid.Controller == null)
                continue;

            if (grid.Controller != inventoryController)
                Debug.LogWarning(
                    $"[WorldPickup][P25] 画面Inventoryと収納先の参照が異なります。" +
                    $" UI={GetHierarchyPath(grid.Controller.transform)}" +
                    $" / 拾得先={GetHierarchyPath(inventoryController.transform)}",
                    this);
        }
    }

    private string GetDroppedItemLabel()
    {
        return droppedItem != null && droppedItem.ItemData != null
            ? droppedItem.ItemData.DisplayName
            : "(なし)";
    }

    private string GetPlayerColliderLabel()
    {
        return playerColliderInRange != null
            ? GetHierarchyPath(playerColliderInRange.transform)
            : "未検出";
    }

    private static string GetHierarchyPath(Transform node)
    {
        if (node == null)
            return "null";
        string path = node.name;
        while (node.parent != null)
        {
            node = node.parent;
            path = node.name + "/" + path;
        }
        return path;
    }

    private void FindPickupPromptText()
    {
        if (pickupPromptText != null)
        {
            return;
        }

        pickupPromptText =
            FindTextByObjectName("PickupPrompt");
    }

    private void FindStackAmountText()
    {
        if (stackAmountText != null)
        {
            return;
        }

        stackAmountText =
            FindTextByObjectName("StackAmountText");
    }

    private TMP_Text FindTextByObjectName(string objectName)
    {
        TMP_Text[] texts =
            GetComponentsInChildren<TMP_Text>(true);

        foreach (TMP_Text text in texts)
        {
            if (text != null &&
                text.gameObject.name == objectName)
            {
                return text;
            }
        }

        return null;
    }

    private void RefreshVisual()
    {
        if (itemSpriteRenderer == null)
        {
            return;
        }

        if (droppedItem == null ||
            droppedItem.ItemData == null)
        {
            itemSpriteRenderer.sprite = null;
            return;
        }

        itemSpriteRenderer.sprite = droppedItem.ItemData.Icon;
    }

    private void RefreshPickupPrompt()
    {
        if (pickupPromptText == null)
        {
            return;
        }

        bool canShowAfterDelay =
            !hidePromptDuringPickupDelay ||
            Time.time >= canPickupAfterTime;

        bool shouldShow =
            droppedItem != null &&
            droppedItem.ItemData != null &&
            !isPickingUp &&
            IsPlayerInRange &&
            canShowAfterDelay &&
            
            !IsCarryInteractionBlockingPickup();

        if (shouldShow)
        {
            pickupPromptText.text =
     $"{pickupKey}:{localizedPickupPromptLabel}";
        }

        pickupPromptText.enabled = shouldShow;
    }

    private void RefreshStackAmountText()
    {
        if (stackAmountText == null)
        {
            return;
        }

        bool isStackItem =
            droppedItem != null &&
            droppedItem.ItemData != null &&
            droppedItem.CanStack;

        bool shouldShow =
            isStackItem &&
            (!showStackAmountOnlyWhenMoreThanOne ||
             droppedItem.Amount > 1);

        if (shouldShow)
        {
            stackAmountText.text =
                $"{stackAmountPrefix}{droppedItem.Amount}";
        }

        stackAmountText.enabled = shouldShow;
    }

    private void ApplyPickupPromptPosition()
    {
        if (pickupPromptText == null)
        {
            return;
        }

        pickupPromptText.transform.localPosition =
            pickupPromptLocalPosition;
    }

    private void ApplyStackAmountPosition()
    {
        if (stackAmountText == null)
        {
            return;
        }

        stackAmountText.transform.localPosition =
            stackAmountLocalPosition;
    }

    private void PlayWorldSound(
        AudioClip clip,
        float volume)
    {
        if (clip == null)
        {
            return;
        }

        GameObject soundObject = new GameObject(
            $"OneShot_{clip.name}"
        );

        soundObject.transform.position = transform.position;

        AudioSource audioSource =
            soundObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.clip = clip;
        audioSource.volume = volume;
        audioSource.spatialBlend = soundSpatialBlend;

        audioSource.Play();

        Destroy(
            soundObject,
            Mathf.Max(0.1f, clip.length)
        );
    }

    private void OnValidate()
    {
        pickupDelay = Mathf.Max(0f, pickupDelay);

        dropSoundVolume = Mathf.Clamp01(dropSoundVolume);
        pickupSoundVolume = Mathf.Clamp01(pickupSoundVolume);
        soundSpatialBlend = Mathf.Clamp01(soundSpatialBlend);

        if (string.IsNullOrWhiteSpace(stackAmountPrefix))
        {
            stackAmountPrefix = "×";
        }

        FindPickupPromptText();
        FindStackAmountText();
        FindPickupRange();

        ApplyPickupPromptPosition();
        ApplyStackAmountPosition();
    }
}
