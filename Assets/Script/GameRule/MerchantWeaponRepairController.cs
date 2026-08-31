using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Serialization;
using UnityEngine.UI;

/// <summary>
/// 武器商人の「武器修理」専用画面を管理します。
///
/// 【新しい修理方式】
/// ・Player Inventory内の損傷した武器を左クリックで複数選択
/// ・選択済み武器をもう一度クリックすると解除
/// ・「修理する」で選択中の武器をまとめて新品まで修理
/// ・「すべて修理する」でPlayer Inventory内の損傷武器をすべて修理
/// ・選択中の合計修理費 / 全修理の合計修理費を別Textへ表示
/// ・StatusTextへ出る全メッセージを下から上へ移動しながらフェードアウト
///
/// 修理対象は、Player Inventory内とPrimaryWeapon装備スロットのWeaponItemDataです。
/// 耐久100%の武器は修理対象外です。
/// </summary>
[DisallowMultipleComponent]
public class MerchantWeaponRepairController : MonoBehaviour
{
    public static MerchantWeaponRepairController ActiveInstance
    {
        get;
        private set;
    }

    [Header("修理サービス表示")]
    [Tooltip("武器修理UI全体です。通常の購入・売却Panelとは分けてください。")]
    [SerializeField] private GameObject repairRoot;

    [Tooltip("通常はオン。商品棚にWeaponItemDataがある店だけ修理サービスを有効にします。")]
    [SerializeField] private bool requireShopToSellWeapons = true;

    [Tooltip("オンなら商品内容に関係なく、この店舗で修理サービスを有効にします。")]
    [SerializeField] private bool forceEnableRepairService;

    [Header("プレイヤー参照")]
    [Tooltip("武器修理画面に表示するPlayer InventoryのInventoryGridUIです。")]
    [SerializeField] private InventoryGridUI playerInventoryGridUI;

    [SerializeField] private TownPlayerInventoryController townPlayerInventory;
    [SerializeField] private PlayerEquipmentVisualController equipmentVisualController;
    [SerializeField] private GameSessionManager gameSessionManager;

    [Tooltip("「戻る」を押した時に商人画面を閉じるためのControllerです。未設定なら自動検索します。")]
    [SerializeField] private PawnShopUIController pawnShopUIController;

    [Header("修理金額Text")]
    [Tooltip("例：選択した武器の修理合計：¥3,500")]
    [SerializeField] private TMP_Text selectedRepairTotalText;

    [Tooltip("例：すべて修理する場合の合計：¥7,800")]
    [SerializeField] private TMP_Text allRepairTotalText;

    [Tooltip("現在の所持金を表示するTextです。任意です。")]
    [SerializeField] private TMP_Text moneyText;

    [Tooltip("「お金が足りない」などを表示するTextです。")]
    [SerializeField] private TMP_Text statusText;

    [Header("操作Button")]
    [Tooltip("現在選択している武器だけをまとめて修理します。")]
    [SerializeField] private Button repairSelectedButton;

    [Tooltip("Player Inventory内の損傷した武器をすべて修理します。")]
    [SerializeField] private Button repairAllButton;

    [Tooltip("武器修理画面を閉じます。")]
    [SerializeField] private Button backButton;

    [Header("修理価格")]
    [Tooltip("WeaponItemDataで計算した修理費へ掛ける店舗倍率。1=標準、1.2=20%割増。")]
    [SerializeField, Min(0f)] private float repairPriceMultiplier = 1f;

    [Header("修理サウンド")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip repairSuccessSound;
    [SerializeField, Range(0f, 1f)] private float repairSoundVolume = 1f;

    [Header("表示文言")]
    [SerializeField] private string noWeaponSelectedMessage =
        "修理する武器を選択してください。";

    [SerializeField] private string noRepairNeededMessage =
        "修理が必要な武器がありません。";

    [SerializeField] private string insufficientMoneyMessage =
        "お金が足りない";

    [SerializeField] private string selectedTotalFormat =
        "選択した武器の修理合計：¥{0:N0}";

    [SerializeField] private string allTotalFormat =
        "すべて修理する場合の合計：¥{0:N0}";

    [SerializeField] private string moneyFormat =
        "所持金：¥{0:N0}";

    [SerializeField] private string selectedRepairSuccessFormat =
        "{0}丁の武器を合計 ¥{1:N0} で修理しました。";

    [SerializeField] private string allRepairSuccessFormat =
        "すべての武器（{0}丁）を合計 ¥{1:N0} で修理しました。";

    [Header("Status Text 共通演出")]
    [Tooltip("Status Textへ表示するすべてのメッセージを、下から上へ移動しながらフェードアウトさせます。")]
    [FormerlySerializedAs("animateInsufficientMoneyStatus")]
    [SerializeField] private bool animateStatusMessages = true;

    [Tooltip("すべてのStatusメッセージに共通で使う演出時間です。")]
    [FormerlySerializedAs("statusMessageDuration")]
    [SerializeField, Min(0.1f)]
    private float statusMessageDuration = 1.2f;

    [Tooltip("元のStatusText位置より何px下から開始するか。")]
    [FormerlySerializedAs("statusMessageStartYOffset")]
    [SerializeField]
    private float statusMessageStartYOffset = 36f;

    [Tooltip("元のStatusText位置より何px上まで移動するか。")]
    [FormerlySerializedAs("statusMessageEndYOffset")]
    [SerializeField]
    private float statusMessageEndYOffset = 28f;

    [Tooltip("Statusメッセージの開始時透明度です。")]
    [FormerlySerializedAs("statusMessageStartAlpha")]
    [SerializeField, Range(0f, 1f)]
    private float statusMessageStartAlpha = 1f;

    [Tooltip("Statusメッセージの終了時透明度です。通常は0。")]
    [FormerlySerializedAs("statusMessageEndAlpha")]
    [SerializeField, Range(0f, 1f)]
    private float statusMessageEndAlpha = 0f;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public bool IsOpen => isOpen;
    public MerchantStockInventory CurrentStock => currentStock;
    public int SelectedWeaponCount => selectedWeapons.Count;
    public int SelectedRepairTotal => CalculateSelectedRepairTotal();
    public int AllRepairTotal => CalculateAllRepairTotal();

    private MerchantStockInventory currentStock;

    private readonly HashSet<InventoryItem> selectedWeapons =
        new HashSet<InventoryItem>();

    private bool isOpen;
    private bool isMoneySubscribed;
    private bool buttonsRegistered;
    private bool isProcessingRepair;

    private Coroutine statusAnimationCoroutine;
    private RectTransform statusRectTransform;
    private Vector2 statusBaseAnchoredPosition;
    private Color statusBaseColor = Color.white;
    private bool hasCapturedStatusBaseState;

    private void Awake()
    {
        FindReferences();
        RegisterButtons();
        CaptureStatusBaseState();
        RefreshUI();
    }

    private void OnEnable()
    {
        FindReferences();
        RegisterButtons();
        CaptureStatusBaseState();
        SubscribeMoney();
        RefreshUI();
    }

    private void OnDisable()
    {
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        UnsubscribeMoney();
        StopStatusAnimation(true);
    }

    private void OnDestroy()
    {
        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        UnregisterButtons();
    }

    private void Update()
    {
        if (!isOpen ||
            isProcessingRepair ||
            playerInventoryGridUI == null ||
            !playerInventoryGridUI.gameObject.activeInHierarchy ||
            !Input.GetMouseButtonUp(0))
        {
            return;
        }

        TryToggleWeaponUnderPointer();
    }

    public bool OpenRepairShop(
        MerchantStockInventory stockInventory)
    {
        FindReferences();
        RegisterButtons();

        currentStock = stockInventory;
        selectedWeapons.Clear();
        isOpen = false;

        bool serviceAvailable =
            forceEnableRepairService ||
            !requireShopToSellWeapons ||
            ShopSellsWeapons(stockInventory);

        if (stockInventory == null ||
            !serviceAvailable)
        {
            if (repairRoot != null)
            {
                repairRoot.SetActive(false);
            }

            if (ActiveInstance == this)
            {
                ActiveInstance = null;
            }

            RefreshUI();
            return false;
        }

        isOpen = true;
        ActiveInstance = this;

        if (repairRoot != null)
        {
            repairRoot.SetActive(true);
        }

        SubscribeMoney();

        playerInventoryGridUI?.RefreshInventoryUI();

        SetStatus(string.Empty);
        RefreshUI();

        Log(
            $"武器修理画面を開きました。店舗={stockInventory.ShopName} / " +
            $"修理可能武器={GetRepairableOwnedWeapons().Count}"
        );

        return true;
    }

    public void CloseRepairShop()
    {
        isOpen = false;
        isProcessingRepair = false;
        currentStock = null;
        selectedWeapons.Clear();

        if (ActiveInstance == this)
        {
            ActiveInstance = null;
        }

        if (repairRoot != null)
        {
            repairRoot.SetActive(false);
        }

        StopStatusAnimation(true);
        SetStatus(string.Empty);
        RefreshUI();
    }

    /// <summary>
    /// InventoryItemUIが修理選択色を出すために使用します。
    /// </summary>
    public bool IsItemSelectedForRepair(
        InventoryItem item)
    {
        return
            isOpen &&
            item != null &&
            selectedWeapons.Contains(item);
    }

    /// <summary>
    /// InventoryItemUIから左クリックされた時に直接呼ばれます。
    /// Update + EventSystem.RaycastAllだけに依存せず、
    /// 修理用Player Inventoryで確実に選択/解除できるようにします。
    /// </summary>
    public bool TryToggleRepairSelectionFromItemUI(
        InventoryItem item)
    {
        if (!isOpen ||
            isProcessingRepair ||
            item == null ||
            !(item.ItemData is WeaponItemData))
        {
            return false;
        }

        InventoryController inventory =
            townPlayerInventory != null
                ? townPlayerInventory.InventoryController
                : null;

        if (inventory?.Grid == null ||
            !inventory.Grid.ContainsItem(item))
        {
            return false;
        }

        ToggleWeaponSelection(item);
        return true;
    }

    /// <summary>
    /// EquipmentItemDragHandlerから、装備スロットの武器を
    /// 修理選択／解除するために呼ばれます。
    /// </summary>
    public bool TryToggleRepairSelectionFromEquipmentUI(
        InventoryItem item)
    {
        if (!isOpen ||
            isProcessingRepair ||
            item == null ||
            !(item.ItemData is WeaponItemData))
        {
            return false;
        }

        if (GetEquippedWeapon() != item)
        {
            return false;
        }

        ToggleWeaponSelection(item);
        return true;
    }

    /// <summary>
    /// 「修理する」Button。
    /// 現在選択している武器だけをまとめて修理します。
    /// </summary>
    public void RepairSelectedWeapons()
    {
        FindReferences();
        PruneSelectedWeapons();

        List<InventoryItem> targets =
            new List<InventoryItem>();

        foreach (InventoryItem item in selectedWeapons)
        {
            if (IsRepairableOwnedWeapon(item))
            {
                targets.Add(item);
            }
        }

        if (targets.Count <= 0)
        {
            SetStatus(noWeaponSelectedMessage);
            RefreshUI();
            return;
        }

        int totalCost =
            CalculateRepairTotal(targets);

        TryRepairWeapons(
            targets,
            totalCost,
            false
        );
    }

    /// <summary>
    /// 「すべて修理する」Button。
    /// Player Inventory内＋装備スロットの損傷した武器をすべて修理します。
    /// </summary>
    public void RepairAllWeapons()
    {
        FindReferences();

        List<InventoryItem> targets =
            GetRepairableOwnedWeapons();

        if (targets.Count <= 0)
        {
            SetStatus(noRepairNeededMessage);
            RefreshUI();
            return;
        }

        int totalCost =
            CalculateRepairTotal(targets);

        TryRepairWeapons(
            targets,
            totalCost,
            true
        );
    }

    /// <summary>
    /// 「戻る」Button。
    /// 現在の修理画面を閉じます。
    /// </summary>
    public void Back()
    {
        FindReferences();

        if (pawnShopUIController != null)
        {
            pawnShopUIController.ClosePawnShop();
            return;
        }

        CloseRepairShop();
    }

    public void RefreshUI()
    {
        FindReferences();
        PruneSelectedWeapons();

        int selectedTotal =
            CalculateSelectedRepairTotal();

        int allTotal =
            CalculateAllRepairTotal();

        if (selectedRepairTotalText != null)
        {
            selectedRepairTotalText.text =
                string.Format(
                    string.IsNullOrWhiteSpace(
                        selectedTotalFormat)
                        ? "選択した武器の修理合計：¥{0:N0}"
                        : selectedTotalFormat,
                    selectedTotal
                );
        }

        if (allRepairTotalText != null)
        {
            allRepairTotalText.text =
                string.Format(
                    string.IsNullOrWhiteSpace(
                        allTotalFormat)
                        ? "すべて修理する場合の合計：¥{0:N0}"
                        : allTotalFormat,
                    allTotal
                );
        }

        if (moneyText != null)
        {
            int money =
                gameSessionManager != null
                    ? gameSessionManager.CurrentMoney
                    : 0;

            moneyText.text =
                string.Format(
                    string.IsNullOrWhiteSpace(moneyFormat)
                        ? "所持金：¥{0:N0}"
                        : moneyFormat,
                    money
                );
        }

        // お金が足りない場合でもButtonは押せるようにする。
        // 押した時に不足Animationを出す。
        if (repairSelectedButton != null)
        {
            repairSelectedButton.interactable =
                isOpen &&
                !isProcessingRepair &&
                selectedWeapons.Count > 0 &&
                selectedTotal > 0;
        }

        if (repairAllButton != null)
        {
            repairAllButton.interactable =
                isOpen &&
                !isProcessingRepair &&
                allTotal > 0;
        }

        if (backButton != null)
        {
            backButton.interactable =
                isOpen &&
                !isProcessingRepair;
        }
    }

    private void TryToggleWeaponUnderPointer()
    {
        if (EventSystem.current == null ||
            playerInventoryGridUI == null)
        {
            return;
        }

        PointerEventData pointerData =
            new PointerEventData(
                EventSystem.current)
            {
                position = Input.mousePosition
            };

        List<RaycastResult> results =
            new List<RaycastResult>();

        EventSystem.current.RaycastAll(
            pointerData,
            results
        );

        foreach (RaycastResult result in results)
        {
            if (result.gameObject == null)
            {
                continue;
            }

            InventoryItemUI itemUI =
                result.gameObject
                    .GetComponentInParent<
                        InventoryItemUI
                    >();

            InventoryItem item =
                itemUI != null
                    ? itemUI.Item
                    : null;

            if (item == null ||
                !playerInventoryGridUI.ContainsItem(item))
            {
                continue;
            }

            ToggleWeaponSelection(item);
            return;
        }
    }

    private void ToggleWeaponSelection(
        InventoryItem weaponItem)
    {
        if (weaponItem == null ||
            !(weaponItem.ItemData is WeaponItemData))
        {
            return;
        }

        if (selectedWeapons.Remove(weaponItem))
        {
            SetStatus(string.Empty);
            RefreshUI();

            Log(
                $"修理選択解除: {weaponItem.ItemData.DisplayName}"
            );
            return;
        }

        if (!IsRepairableOwnedWeapon(weaponItem))
        {
            SetStatus(noRepairNeededMessage);
            RefreshUI();
            return;
        }

        selectedWeapons.Add(weaponItem);

        SetStatus(string.Empty);
        RefreshUI();

        Log(
            $"修理選択追加: {weaponItem.ItemData.DisplayName} / " +
            $"選択数={selectedWeapons.Count}"
        );
    }

    private bool TryRepairWeapons(
        List<InventoryItem> targets,
        int totalCost,
        bool isAllRepair)
    {
        if (!isOpen ||
            targets == null ||
            targets.Count <= 0 ||
            totalCost <= 0)
        {
            SetStatus(
                isAllRepair
                    ? noRepairNeededMessage
                    : noWeaponSelectedMessage
            );
            RefreshUI();
            return false;
        }

        if (gameSessionManager == null ||
            !gameSessionManager.CanAfford(totalCost))
        {
            ShowInsufficientMoneyStatus();
            RefreshUI();
            return false;
        }

        if (!gameSessionManager.TrySpendMoney(totalCost))
        {
            ShowInsufficientMoneyStatus();
            RefreshUI();
            return false;
        }

        isProcessingRepair = true;

        int repairedCount = 0;

        foreach (InventoryItem item in targets)
        {
            if (!IsRepairableOwnedWeapon(item))
            {
                continue;
            }

            item.EnsureWeaponDurabilityInitialized();

            float before =
                item.StoredWeaponDurability;

            item.RepairWeaponToFull();
            item.SetStoredWeaponJammed(false);

            if (item.StoredWeaponDurability >
                before + 0.0001f)
            {
                repairedCount++;
            }

            // 装備中の同一武器だった場合は、
            // 実際に表示・使用中のGunShooter側へも同期する。
            equipmentVisualController
                ?.SynchronizeWeaponConditionFromItem(item);

            InventoryItemTooltipUI.HideFor(item);
        }

        isProcessingRepair = false;

        if (repairedCount <= 0)
        {
            // 想定外に1つも直らなかった場合は料金を戻す。
            gameSessionManager.AddMoney(totalCost);

            SetStatus(noRepairNeededMessage);
            RefreshUI();
            return false;
        }

        selectedWeapons.Clear();

        CapturePlayerInventory();
        playerInventoryGridUI?.RefreshInventoryUI();
        PlayRepairSound();

        SetStatus(
            string.Format(
                isAllRepair
                    ? (
                        string.IsNullOrWhiteSpace(
                            allRepairSuccessFormat)
                            ? "すべての武器（{0}丁）を合計 ¥{1:N0} で修理しました。"
                            : allRepairSuccessFormat
                    )
                    : (
                        string.IsNullOrWhiteSpace(
                            selectedRepairSuccessFormat)
                            ? "{0}丁の武器を合計 ¥{1:N0} で修理しました。"
                            : selectedRepairSuccessFormat
                    ),
                repairedCount,
                totalCost
            )
        );

        RefreshUI();

        Log(
            $"一括修理成功: 種類={repairedCount} / " +
            $"料金={totalCost:N0} / All={isAllRepair}"
        );

        return true;
    }

    private void PruneSelectedWeapons()
    {
        if (selectedWeapons.Count <= 0)
        {
            return;
        }

        List<InventoryItem> removeList =
            new List<InventoryItem>();

        foreach (InventoryItem item in selectedWeapons)
        {
            if (!IsRepairableOwnedWeapon(item))
            {
                removeList.Add(item);
            }
        }

        foreach (InventoryItem item in removeList)
        {
            selectedWeapons.Remove(item);
        }
    }

    private bool IsRepairableOwnedWeapon(
        InventoryItem item)
    {
        if (item == null ||
            !(item.ItemData is WeaponItemData weaponData) ||
            !PlayerOwnsWeapon(item))
        {
            return false;
        }

        item.EnsureWeaponDurabilityInitialized();

        int repairCost =
            CalculateRepairCost(
                weaponData,
                item.StoredWeaponDurability
            );

        return repairCost > 0;
    }

    /// <summary>
    /// Player Inventory内の損傷武器に加えて、
    /// PrimaryWeapon装備スロットの武器も含めて返します。
    /// 同一InventoryItemはHashSetで重複防止します。
    /// </summary>
    private List<InventoryItem>
        GetRepairableOwnedWeapons()
    {
        List<InventoryItem> result =
            new List<InventoryItem>();

        HashSet<InventoryItem> added =
            new HashSet<InventoryItem>();

        InventoryController inventory =
            townPlayerInventory != null
                ? townPlayerInventory.InventoryController
                : null;

        if (inventory?.Grid != null)
        {
            foreach (InventoryItem item
                     in inventory.Grid.Items)
            {
                if (item != null &&
                    added.Add(item) &&
                    IsRepairableOwnedWeapon(item))
                {
                    result.Add(item);
                }
            }
        }

        InventoryItem equipped =
            GetEquippedWeapon();

        if (equipped != null &&
            added.Add(equipped) &&
            IsRepairableOwnedWeapon(equipped))
        {
            result.Add(equipped);
        }

        return result;
    }

    private bool PlayerOwnsWeapon(
        InventoryItem item)
    {
        if (item == null ||
            !(item.ItemData is WeaponItemData))
        {
            return false;
        }

        InventoryController inventory =
            townPlayerInventory != null
                ? townPlayerInventory.InventoryController
                : null;

        if (inventory?.Grid != null &&
            inventory.Grid.ContainsItem(item))
        {
            return true;
        }

        return GetEquippedWeapon() == item;
    }

    private InventoryItem GetEquippedWeapon()
    {
        if (townPlayerInventory != null &&
            townPlayerInventory.EquipmentController != null)
        {
            return townPlayerInventory
                .EquipmentController
                .PrimaryWeaponItem;
        }

        return equipmentVisualController != null
            ? equipmentVisualController.CurrentWeaponItem
            : null;
    }

    private int CalculateSelectedRepairTotal()
    {
        if (!isOpen)
        {
            return 0;
        }

        int total = 0;

        foreach (InventoryItem item
                 in selectedWeapons)
        {
            total =
                SafeAdd(
                    total,
                    CalculateItemRepairCost(item)
                );
        }

        return total;
    }

    private int CalculateAllRepairTotal()
    {
        if (!isOpen)
        {
            return 0;
        }

        return CalculateRepairTotal(
            GetRepairableOwnedWeapons()
        );
    }

    private int CalculateRepairTotal(
        List<InventoryItem> items)
    {
        if (items == null)
        {
            return 0;
        }

        int total = 0;

        foreach (InventoryItem item in items)
        {
            total =
                SafeAdd(
                    total,
                    CalculateItemRepairCost(item)
                );
        }

        return total;
    }

    private int CalculateItemRepairCost(
        InventoryItem item)
    {
        if (item == null ||
            !(item.ItemData is WeaponItemData weaponData))
        {
            return 0;
        }

        item.EnsureWeaponDurabilityInitialized();

        return CalculateRepairCost(
            weaponData,
            item.StoredWeaponDurability
        );
    }

    private int CalculateRepairCost(
        WeaponItemData weaponData,
        float currentDurability)
    {
        if (weaponData == null)
        {
            return 0;
        }

        int baseCost =
            weaponData.CalculateFullRepairCost(
                currentDurability
            );

        if (baseCost <= 0)
        {
            return 0;
        }

        double calculated =
            baseCost *
            Mathf.Max(
                0f,
                repairPriceMultiplier
            );

        if (calculated >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return Mathf.Max(
            0,
            Mathf.CeilToInt(
                (float)calculated
            )
        );
    }

    private bool ShopSellsWeapons(
        MerchantStockInventory stockInventory)
    {
        if (stockInventory
                ?.StockInventory
                ?.Grid == null)
        {
            return false;
        }

        foreach (InventoryItem item
                 in stockInventory.StockInventory.Grid.Items)
        {
            if (item?.ItemData is WeaponItemData)
            {
                return true;
            }
        }

        return false;
    }

    private void CapturePlayerInventory()
    {
        if (townPlayerInventory == null)
        {
            return;
        }

        PlayerInventorySessionBridge bridge =
            townPlayerInventory.SessionBridge;

        if (bridge != null)
        {
            bridge.CaptureToSession();
            return;
        }

        if (gameSessionManager != null &&
            townPlayerInventory.InventoryController != null)
        {
            gameSessionManager.CapturePlayerInventory(
                townPlayerInventory.InventoryController,
                townPlayerInventory.EquipmentController
            );
        }
    }

    private void HandleMoneyChanged(int money)
    {
        RefreshUI();
    }

    private void SubscribeMoney()
    {
        if (isMoneySubscribed ||
            gameSessionManager == null)
        {
            return;
        }

        gameSessionManager.MoneyChanged +=
            HandleMoneyChanged;

        isMoneySubscribed = true;
    }

    private void UnsubscribeMoney()
    {
        if (!isMoneySubscribed)
        {
            return;
        }

        if (gameSessionManager != null)
        {
            gameSessionManager.MoneyChanged -=
                HandleMoneyChanged;
        }

        isMoneySubscribed = false;
    }

    private void RegisterButtons()
    {
        if (buttonsRegistered)
        {
            return;
        }

        repairSelectedButton
            ?.onClick.AddListener(
                RepairSelectedWeapons
            );

        repairAllButton
            ?.onClick.AddListener(
                RepairAllWeapons
            );

        backButton
            ?.onClick.AddListener(
                Back
            );

        buttonsRegistered = true;
    }

    private void UnregisterButtons()
    {
        if (!buttonsRegistered)
        {
            return;
        }

        repairSelectedButton
            ?.onClick.RemoveListener(
                RepairSelectedWeapons
            );

        repairAllButton
            ?.onClick.RemoveListener(
                RepairAllWeapons
            );

        backButton
            ?.onClick.RemoveListener(
                Back
            );

        buttonsRegistered = false;
    }

    private void FindReferences()
    {
        if (audioSource == null)
        {
            audioSource =
                GetComponent<AudioSource>();
        }

        if (townPlayerInventory == null)
        {
            townPlayerInventory =
                FindAnyObjectByType<
                    TownPlayerInventoryController
                >(FindObjectsInactive.Include);
        }

        if (playerInventoryGridUI == null)
        {
            InventoryGridUI[] gridUIs =
                FindObjectsByType<InventoryGridUI>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                );

            foreach (InventoryGridUI candidate
                     in gridUIs)
            {
                if (candidate != null &&
                    candidate.IsPlayerInventory)
                {
                    playerInventoryGridUI =
                        candidate;
                    break;
                }
            }
        }

        if (equipmentVisualController == null &&
            townPlayerInventory != null)
        {
            equipmentVisualController =
                townPlayerInventory.GetComponent<
                    PlayerEquipmentVisualController
                >();
        }

        if (equipmentVisualController == null)
        {
            equipmentVisualController =
                FindAnyObjectByType<
                    PlayerEquipmentVisualController
                >(FindObjectsInactive.Include);
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<
                    GameSessionManager
                >(FindObjectsInactive.Include);
        }

        if (pawnShopUIController == null)
        {
            pawnShopUIController =
                FindAnyObjectByType<
                    PawnShopUIController
                >(FindObjectsInactive.Include);
        }
    }

    private void PlayRepairSound()
    {
        if (audioSource != null &&
            repairSuccessSound != null)
        {
            audioSource.PlayOneShot(
                repairSuccessSound,
                Mathf.Clamp01(
                    repairSoundVolume
                )
            );
        }
    }

    private void ShowInsufficientMoneyStatus()
    {
        string message =
            string.IsNullOrWhiteSpace(
                insufficientMoneyMessage)
                ? "お金が足りない"
                : insufficientMoneyMessage;

        // 所持金不足も、他のStatusと同じ共通演出を使用します。
        SetStatus(message);
    }

    private IEnumerator
        AnimateStatusMessage(
            string message)
    {
        if (statusText == null)
        {
            statusAnimationCoroutine = null;
            yield break;
        }

        CaptureStatusBaseState();

        if (statusRectTransform == null)
        {
            statusText.text = message;
            statusAnimationCoroutine = null;
            yield break;
        }

        statusText.text = message;

        float duration =
            Mathf.Max(
                0.1f,
                statusMessageDuration
            );

        Vector2 startPosition =
            statusBaseAnchoredPosition +
            Vector2.down *
            Mathf.Abs(
                statusMessageStartYOffset
            );

        Vector2 endPosition =
            statusBaseAnchoredPosition +
            Vector2.up *
            Mathf.Abs(
                statusMessageEndYOffset
            );

        Color startColor =
            statusBaseColor;

        startColor.a =
            Mathf.Clamp01(
                statusMessageStartAlpha
            );

        Color endColor =
            statusBaseColor;

        endColor.a =
            Mathf.Clamp01(
                statusMessageEndAlpha
            );

        statusRectTransform.anchoredPosition =
            startPosition;

        statusText.color =
            startColor;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;

            float t =
                Mathf.Clamp01(
                    elapsed / duration
                );

            float eased =
                1f -
                Mathf.Pow(
                    1f - t,
                    3f
                );

            statusRectTransform.anchoredPosition =
                Vector2.LerpUnclamped(
                    startPosition,
                    endPosition,
                    eased
                );

            statusText.color =
                Color.Lerp(
                    startColor,
                    endColor,
                    t
                );

            yield return null;
        }

        statusRectTransform.anchoredPosition =
            endPosition;

        statusText.color =
            endColor;

        if (statusMessageEndAlpha <=
            0.001f)
        {
            statusText.text =
                string.Empty;
        }

        ResetStatusVisualState();
        statusAnimationCoroutine = null;
    }

    private void CaptureStatusBaseState()
    {
        if (statusText == null ||
            hasCapturedStatusBaseState)
        {
            return;
        }

        statusRectTransform =
            statusText.rectTransform;

        if (statusRectTransform != null)
        {
            statusBaseAnchoredPosition =
                statusRectTransform.anchoredPosition;
        }

        statusBaseColor =
            statusText.color;

        hasCapturedStatusBaseState = true;
    }

    private void StopStatusAnimation(
        bool resetVisual)
    {
        if (statusAnimationCoroutine != null)
        {
            StopCoroutine(
                statusAnimationCoroutine
            );

            statusAnimationCoroutine = null;
        }

        if (resetVisual)
        {
            ResetStatusVisualState();
        }
    }

    private void ResetStatusVisualState()
    {
        if (!hasCapturedStatusBaseState ||
            statusText == null)
        {
            return;
        }

        if (statusRectTransform == null)
        {
            statusRectTransform =
                statusText.rectTransform;
        }

        if (statusRectTransform != null)
        {
            statusRectTransform.anchoredPosition =
                statusBaseAnchoredPosition;
        }

        statusText.color =
            statusBaseColor;
    }

    private void SetStatus(string message)
    {
        if (statusText == null)
        {
            return;
        }

        string resolvedMessage =
            message ?? string.Empty;

        StopStatusAnimation(true);

        // 空文字は画面を閉じる・選択時のクリア用途なので、
        // Animationを出さず即座に消します。
        if (string.IsNullOrEmpty(resolvedMessage))
        {
            statusText.text = string.Empty;
            return;
        }

        if (!animateStatusMessages)
        {
            statusText.text =
                resolvedMessage;
            return;
        }

        CaptureStatusBaseState();

        statusAnimationCoroutine =
            StartCoroutine(
                AnimateStatusMessage(
                    resolvedMessage
                )
            );
    }

    private static int SafeAdd(
        int current,
        int add)
    {
        long total =
            (long)Mathf.Max(0, current) +
            Mathf.Max(0, add);

        return total >= int.MaxValue
            ? int.MaxValue
            : (int)total;
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log(
                $"[MerchantWeaponRepair] {message}",
                this
            );
        }
    }

    private void OnValidate()
    {
        repairPriceMultiplier =
            Mathf.Max(
                0f,
                repairPriceMultiplier
            );

        repairSoundVolume =
            Mathf.Clamp01(
                repairSoundVolume
            );

        statusMessageDuration =
            Mathf.Max(
                0.1f,
                statusMessageDuration
            );

        statusMessageStartAlpha =
            Mathf.Clamp01(
                statusMessageStartAlpha
            );

        statusMessageEndAlpha =
            Mathf.Clamp01(
                statusMessageEndAlpha
            );
    }
}
