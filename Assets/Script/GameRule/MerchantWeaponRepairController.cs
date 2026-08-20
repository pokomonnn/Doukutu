using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 武器屋でプレイヤー所持武器を新品まで修理するUIです。
/// MerchantPurchaseControllerからOpenRepairShop/CloseRepairShopを呼べます。
/// プレイヤーInventory Gridの武器を左クリックして選択します。
/// 装備中武器はSelectEquippedWeaponをButtonから呼ぶことで選択できます。
/// </summary>
[DisallowMultipleComponent]
public class MerchantWeaponRepairController : MonoBehaviour
{
    [Header("修理サービス表示")]
    [Tooltip("修理UI全体。武器屋でない場合は自動的に非表示になります。")]
    [SerializeField] private GameObject repairRoot;

    [Tooltip("通常はオン。商品棚にWeaponItemDataがある店だけ修理サービスを有効にします。")]
    [SerializeField] private bool requireShopToSellWeapons = true;

    [Tooltip("オンなら商品内容に関係なく、この店舗で修理サービスを有効にします。")]
    [SerializeField] private bool forceEnableRepairService;

    [Header("プレイヤー参照")]
    [SerializeField] private InventoryGridUI playerInventoryGridUI;
    [SerializeField] private TownPlayerInventoryController townPlayerInventory;
    [SerializeField] private PlayerEquipmentVisualController equipmentVisualController;
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("選択武器表示")]
    [SerializeField] private Image selectedWeaponIcon;
    [SerializeField] private TMP_Text selectedWeaponNameText;
    [SerializeField] private TMP_Text durabilityText;
    [SerializeField] private TMP_Text repairCostText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text statusText;

    [Header("操作")]
    [SerializeField] private Button repairButton;
    [SerializeField] private Button selectEquippedWeaponButton;

    [Tooltip("画面を開いた時、装備中の銃があれば最初に選択します。")]
    [SerializeField] private bool preferEquippedWeaponOnOpen = true;

    [Header("修理価格")]
    [Tooltip("WeaponItemDataで計算した修理費へ掛ける店舗倍率。1=標準、1.2=20%割増。")]
    [SerializeField, Min(0f)] private float repairPriceMultiplier = 1f;

    [Header("サウンド")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip repairSuccessSound;
    [SerializeField, Range(0f, 1f)] private float repairSoundVolume = 1f;

    [Header("表示文言")]
    [SerializeField] private string noWeaponSelectedMessage =
        "修理する武器を選択してください。";
    [SerializeField] private string alreadyFullMessage =
        "この武器は修理する必要がありません。";
    [SerializeField] private string insufficientMoneyMessage =
        "修理費が足りません。";
    [SerializeField] private string repairSuccessFormat =
        "{0} を ¥{1:N0} で修理しました。";
    [SerializeField] private string durabilityFormat =
        "耐久度：{0:0.#}% / 損傷度：{1:0.#}%";
    [SerializeField] private string repairCostFormat =
        "修理費：¥{0:N0}";
    [SerializeField] private string moneyFormat =
        "所持金：¥{0:N0}";

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public bool IsOpen => isOpen;
    public InventoryItem SelectedWeapon => selectedWeapon;

    private MerchantStockInventory currentStock;
    private InventoryItem selectedWeapon;
    private bool isOpen;
    private bool isMoneySubscribed;
    private bool buttonsRegistered;

    private void Awake()
    {
        FindReferences();
        RegisterButtons();
        RefreshUI();
    }

    private void OnEnable()
    {
        FindReferences();
        RegisterButtons();
        SubscribeMoney();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeMoney();
    }

    private void OnDestroy()
    {
        UnregisterButtons();
    }

    private void Update()
    {
        if (!isOpen ||
            playerInventoryGridUI == null ||
            !playerInventoryGridUI.gameObject.activeInHierarchy ||
            !Input.GetMouseButtonUp(0))
        {
            return;
        }

        TrySelectWeaponUnderPointer();
    }

    public bool OpenRepairShop(MerchantStockInventory stockInventory)
    {
        FindReferences();

        currentStock = stockInventory;
        selectedWeapon = null;

        bool serviceAvailable =
            forceEnableRepairService ||
            !requireShopToSellWeapons ||
            ShopSellsWeapons(stockInventory);

        isOpen = stockInventory != null && serviceAvailable;

        if (repairRoot != null)
        {
            repairRoot.SetActive(isOpen);
        }

        if (!isOpen)
        {
            RefreshUI();
            return false;
        }

        SubscribeMoney();

        if (preferEquippedWeaponOnOpen)
        {
            SelectEquippedWeapon();
        }

        if (selectedWeapon == null)
        {
            SelectFirstInventoryWeapon();
        }

        SetStatus(
            selectedWeapon == null
                ? noWeaponSelectedMessage
                : string.Empty
        );

        RefreshUI();
        return true;
    }

    public void CloseRepairShop()
    {
        isOpen = false;
        currentStock = null;
        selectedWeapon = null;

        if (repairRoot != null)
        {
            repairRoot.SetActive(false);
        }

        SetStatus(string.Empty);
        RefreshUI();
    }

    public void SelectEquippedWeapon()
    {
        FindReferences();

        InventoryItem equipped =
            townPlayerInventory != null &&
            townPlayerInventory.EquipmentController != null
                ? townPlayerInventory.EquipmentController.PrimaryWeaponItem
                : equipmentVisualController != null
                    ? equipmentVisualController.CurrentWeaponItem
                    : null;

        if (equipped == null ||
            !(equipped.ItemData is WeaponItemData))
        {
            return;
        }

        SelectWeapon(equipped);
    }

    public void RepairSelectedWeapon()
    {
        FindReferences();

        if (!isOpen ||
            selectedWeapon == null ||
            !(selectedWeapon.ItemData is WeaponItemData weaponData) ||
            !PlayerOwnsWeapon(selectedWeapon))
        {
            SetStatus(noWeaponSelectedMessage);
            return;
        }

        selectedWeapon.EnsureWeaponDurabilityInitialized();

        int repairCost = CalculateRepairCost(
            weaponData,
            selectedWeapon.StoredWeaponDurability
        );

        if (repairCost <= 0)
        {
            SetStatus(alreadyFullMessage);
            RefreshUI();
            return;
        }

        if (gameSessionManager == null ||
            !gameSessionManager.CanAfford(repairCost))
        {
            SetStatus(insufficientMoneyMessage);
            RefreshUI();
            return;
        }

        if (!gameSessionManager.TrySpendMoney(repairCost))
        {
            SetStatus(insufficientMoneyMessage);
            RefreshUI();
            return;
        }

        float repairedAmount;
        bool repaired;

        if (equipmentVisualController != null)
        {
            repaired = equipmentVisualController.TryRepairWeaponToFull(
                selectedWeapon,
                out repairedAmount
            );
        }
        else
        {
            float before = selectedWeapon.StoredWeaponDurability;
            selectedWeapon.RepairWeaponToFull();
            repairedAmount = Mathf.Max(
                0f,
                weaponData.MaxDurability - before
            );
            repaired = repairedAmount > 0f;
        }

        if (!repaired)
        {
            gameSessionManager.AddMoney(repairCost);
            SetStatus(alreadyFullMessage);
            RefreshUI();
            return;
        }

        selectedWeapon.SetStoredWeaponJammed(false);
        equipmentVisualController?.SynchronizeWeaponConditionFromItem(
            selectedWeapon
        );

        CapturePlayerInventory();
        PlayRepairSound();

        SetStatus(
            string.Format(
                repairSuccessFormat,
                weaponData.DisplayName,
                repairCost
            )
        );

        RefreshUI();

        Log(
            $"修理成功: {weaponData.DisplayName} / " +
            $"回復={repairedAmount:0.##} / 料金={repairCost:N0}"
        );
    }

    public void RefreshUI()
    {
        FindReferences();

        bool hasWeapon =
            isOpen &&
            selectedWeapon != null &&
            selectedWeapon.ItemData is WeaponItemData &&
            PlayerOwnsWeapon(selectedWeapon);

        WeaponItemData weaponData = hasWeapon
            ? selectedWeapon.ItemData as WeaponItemData
            : null;

        if (selectedWeaponIcon != null)
        {
            selectedWeaponIcon.sprite = weaponData != null
                ? weaponData.Icon
                : null;
            selectedWeaponIcon.enabled =
                selectedWeaponIcon.sprite != null;
            selectedWeaponIcon.preserveAspect = true;
        }

        if (selectedWeaponNameText != null)
        {
            selectedWeaponNameText.text = weaponData != null
                ? weaponData.DisplayName
                : "武器未選択";
        }

        int repairCost = 0;

        if (hasWeapon && weaponData != null)
        {
            selectedWeapon.EnsureWeaponDurabilityInitialized();

            float durabilityPercent =
                selectedWeapon.WeaponDurabilityPercent * 100f;
            float damagePercent =
                selectedWeapon.WeaponDamagePercent * 100f;

            if (durabilityText != null)
            {
                durabilityText.text = string.Format(
                    durabilityFormat,
                    durabilityPercent,
                    damagePercent
                );
            }

            repairCost = CalculateRepairCost(
                weaponData,
                selectedWeapon.StoredWeaponDurability
            );
        }
        else if (durabilityText != null)
        {
            durabilityText.text = string.Empty;
        }

        if (repairCostText != null)
        {
            repairCostText.text = string.Format(
                repairCostFormat,
                repairCost
            );
        }

        int currentMoney = gameSessionManager != null
            ? gameSessionManager.CurrentMoney
            : 0;

        if (moneyText != null)
        {
            moneyText.text = string.Format(
                moneyFormat,
                currentMoney
            );
        }

        if (repairButton != null)
        {
            repairButton.interactable =
                hasWeapon &&
                repairCost > 0 &&
                gameSessionManager != null &&
                gameSessionManager.CanAfford(repairCost);
        }

        if (selectEquippedWeaponButton != null)
        {
            selectEquippedWeaponButton.interactable =
                GetEquippedWeapon() != null;
        }
    }

    private void TrySelectWeaponUnderPointer()
    {
        if (EventSystem.current == null ||
            playerInventoryGridUI == null)
        {
            return;
        }

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current)
            {
                position = Input.mousePosition
            };

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            InventoryItemUI itemUI =
                result.gameObject != null
                    ? result.gameObject.GetComponentInParent<InventoryItemUI>()
                    : null;

            InventoryItem item = itemUI != null
                ? itemUI.Item
                : null;

            if (item == null ||
                !(item.ItemData is WeaponItemData) ||
                !playerInventoryGridUI.ContainsItem(item))
            {
                continue;
            }

            SelectWeapon(item);
            return;
        }
    }

    private void SelectWeapon(InventoryItem weaponItem)
    {
        if (weaponItem == null ||
            !(weaponItem.ItemData is WeaponItemData) ||
            !PlayerOwnsWeapon(weaponItem))
        {
            return;
        }

        selectedWeapon = weaponItem;
        SetStatus(string.Empty);
        RefreshUI();
    }

    private void SelectFirstInventoryWeapon()
    {
        InventoryController inventory =
            townPlayerInventory != null
                ? townPlayerInventory.InventoryController
                : null;

        if (inventory?.Grid == null)
        {
            return;
        }

        foreach (InventoryItem item in inventory.Grid.Items)
        {
            if (item?.ItemData is WeaponItemData)
            {
                SelectWeapon(item);
                return;
            }
        }
    }

    private InventoryItem GetEquippedWeapon()
    {
        if (townPlayerInventory != null &&
            townPlayerInventory.EquipmentController != null)
        {
            return townPlayerInventory.EquipmentController.PrimaryWeaponItem;
        }

        return equipmentVisualController != null
            ? equipmentVisualController.CurrentWeaponItem
            : null;
    }

    private bool PlayerOwnsWeapon(InventoryItem weaponItem)
    {
        if (weaponItem == null)
        {
            return false;
        }

        InventoryController inventory =
            townPlayerInventory != null
                ? townPlayerInventory.InventoryController
                : null;

        if (inventory?.Grid != null &&
            inventory.Grid.ContainsItem(weaponItem))
        {
            return true;
        }

        return GetEquippedWeapon() == weaponItem;
    }

    private bool ShopSellsWeapons(MerchantStockInventory stockInventory)
    {
        if (stockInventory?.StockInventory?.Grid == null)
        {
            return false;
        }

        foreach (InventoryItem item in stockInventory.StockInventory.Grid.Items)
        {
            if (item?.ItemData is WeaponItemData)
            {
                return true;
            }
        }

        return false;
    }

    private int CalculateRepairCost(
        WeaponItemData weaponData,
        float currentDurability)
    {
        if (weaponData == null)
        {
            return 0;
        }

        int baseCost = weaponData.CalculateFullRepairCost(
            currentDurability
        );

        if (baseCost <= 0)
        {
            return 0;
        }

        double calculated =
            baseCost * Mathf.Max(0f, repairPriceMultiplier);

        if (calculated >= int.MaxValue)
        {
            return int.MaxValue;
        }

        return Mathf.Max(0, Mathf.CeilToInt((float)calculated));
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
        if (isMoneySubscribed || gameSessionManager == null)
        {
            return;
        }

        gameSessionManager.MoneyChanged += HandleMoneyChanged;
        isMoneySubscribed = true;
    }

    private void UnsubscribeMoney()
    {
        if (!isMoneySubscribed || gameSessionManager == null)
        {
            return;
        }

        gameSessionManager.MoneyChanged -= HandleMoneyChanged;
        isMoneySubscribed = false;
    }

    private void RegisterButtons()
    {
        if (buttonsRegistered)
        {
            return;
        }

        repairButton?.onClick.AddListener(RepairSelectedWeapon);
        selectEquippedWeaponButton?.onClick.AddListener(
            SelectEquippedWeapon
        );

        buttonsRegistered = true;
    }

    private void UnregisterButtons()
    {
        if (!buttonsRegistered)
        {
            return;
        }

        repairButton?.onClick.RemoveListener(RepairSelectedWeapon);
        selectEquippedWeaponButton?.onClick.RemoveListener(
            SelectEquippedWeapon
        );

        buttonsRegistered = false;
    }

    private void FindReferences()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }

        if (townPlayerInventory == null)
        {
            townPlayerInventory =
                FindAnyObjectByType<TownPlayerInventoryController>(
                    FindObjectsInactive.Include
                );
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
                FindAnyObjectByType<PlayerEquipmentVisualController>(
                    FindObjectsInactive.Include
                );
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>(
                    FindObjectsInactive.Include
                );
        }
    }

    private void PlayRepairSound()
    {
        if (audioSource != null && repairSuccessSound != null)
        {
            audioSource.PlayOneShot(
                repairSuccessSound,
                Mathf.Clamp01(repairSoundVolume)
            );
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message ?? string.Empty;
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[MerchantWeaponRepair] {message}", this);
        }
    }

    private void OnValidate()
    {
        repairPriceMultiplier = Mathf.Max(0f, repairPriceMultiplier);
        repairSoundVolume = Mathf.Clamp01(repairSoundVolume);
    }
}
