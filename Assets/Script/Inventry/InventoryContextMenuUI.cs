using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Localization;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class InventoryContextMenuUI : MonoBehaviour
{
    [Header("メニューUI")]
    [SerializeField] private TMP_Text itemNameText;
    [SerializeField] private Button equipButton;
    [SerializeField] private Button useButton;
    [SerializeField] private Button informationButton;
    [SerializeField] private Button repairButton;
    [SerializeField] private Button trashButton;

    [Header("Information表示（後で作成してもOK）")]
    [SerializeField] private GameObject informationPanel;
    [SerializeField] private TMP_Text informationTitleText;
    [SerializeField] private TMP_Text informationDescriptionText;

    [Header("装備")]
    [SerializeField] private EquipmentController equipmentController;
    [SerializeField] private PlayerEquipmentVisualController equipmentVisualController;

    [Header("武器修理キット")]
    [SerializeField] private string repairKitMissingMessage = "修理キットを持っていません";
    [SerializeField] private string weaponAlreadyFullMessage = "この武器は修理する必要がありません";
    [SerializeField] private string weaponRepairSuccessFormat = "{0} を修理しました（+{1:0.#}）";

    [Header("アイテムを捨てる処理")]
    [Tooltip("Playerに付けたPlayerItemDropper。未設定なら自動検索します。")]
    [SerializeField] private PlayerItemDropper playerItemDropper;

    [Header("表示位置")]
    [SerializeField]
    private Vector2 cursorOffset = new Vector2(12f, -12f);

    [SerializeField] private bool closeWhenClickOutside = true;

    private RectTransform menuRect;
    private Canvas rootCanvas;

    private InventoryItem selectedItem;
    private InventoryController inventoryController;
    private EquipmentSlotUI selectedEquipmentSlotUI;

    // 箱・ショップ在庫を右クリックした時は、詳細だけ閲覧できる状態にする
    private bool selectedItemIsReadOnly;

    [SerializeField] private InventorySoundPlayer soundPlayer;

    [Header("通知UI")]
    [SerializeField] private InventoryToastUI healthFullToastUI;

    [Header("回復アイテム使用中の制限")]
    [Tooltip("未設定なら、PlayerにあるPlayerWeightControllerを自動取得します")]
    [SerializeField] private PlayerWeightController playerWeightController;

    [Tooltip("別の回復アイテムを使おうとした時に表示するメッセージ")]
    [SerializeField]
    private string usingConsumableMessage =
        "回復アイテムを使用中です";

    [Tooltip("松明が満タンの時に表示するメッセージ")]
    [SerializeField]
    private string torchFullMessage =
        "松明は十分に燃えています";

    [Tooltip("PlayerにTorchControllerが無い時に表示するメッセージ")]
    [SerializeField]
    private string torchControllerNotFoundMessage =
        "松明システムが見つかりません";

    [Header("通知UIの翻訳")]
    [Tooltip("GameText の toast.health_full を設定")]
    [SerializeField]
    private LocalizedString healthFullMessage =
        new LocalizedString();

    private string localizedHealthFullMessage =
        "体力満タンです";

    private bool isHealthFullMessageSubscribed;

    // Information Panelを開いているアイテム。
    // コンテキストメニューを閉じた後も言語切替で更新するため保持する。
    private ItemData informationItemData;
    private InventoryItem informationInventoryItem;
    private bool isItemTextChangeSubscribed;

    private bool buttonsRegistered;
    private int openedFrame = -1;

    public bool IsOpen =>
        gameObject.activeInHierarchy &&
        selectedItem != null;

    private void Awake()
    {
        EnsureLocalizedStrings();
        EnsureReferences();
        FindSoundPlayer();
        FindEquipmentController();
        FindEquipmentVisualController();
        FindPlayerItemDropper();
        FindHealthFullToastUI();
        RegisterButtons();
        SubscribeItemTextChanges();
    }

    private void OnEnable()
    {
        SubscribeHealthFullMessage();

        if (selectedItem == null && Time.frameCount > 0)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        UnsubscribeHealthFullMessage();

        selectedItem = null;
        inventoryController = null;
        selectedEquipmentSlotUI = null;
        selectedItemIsReadOnly = false;
    }

    private void OnDestroy()
    {
        UnregisterButtons();
        UnsubscribeItemTextChanges();
    }

    private void Update()
    {
        if (!IsOpen || !closeWhenClickOutside)
        {
            return;
        }

        // 開いた瞬間のクリックでは閉じない
        if (Time.frameCount == openedFrame)
        {
            return;
        }

        bool clicked =
            Input.GetMouseButtonDown(0) ||
            Input.GetMouseButtonDown(1);

        if (!clicked)
        {
            return;
        }

        if (!IsPointerOverMenuOrChild(Input.mousePosition))
        {
            Hide();
        }
    }

    // InventoryItemUI の右クリックから呼ぶ
    public void Show(
        InventoryItem item,
        InventoryController controller,
        Vector2 screenPosition)
    {
        if (item == null ||
            item.ItemData == null ||
            controller == null)
        {
            return;
        }

        bool wasOpen = IsOpen;

        selectedEquipmentSlotUI = null;
        selectedItem = item;
        inventoryController = controller;
        selectedItemIsReadOnly = false;

        FindEquipmentController();
        FindEquipmentVisualController();
        FindPlayerItemDropper();

        gameObject.SetActive(true);

        RefreshMenu();
        SetMenuPosition(screenPosition);

        openedFrame = Time.frameCount;

        // 閉じている状態から開いた時だけ鳴らす
        if (!wasOpen)
        {
            soundPlayer?.PlayContextMenuOpen();
        }
    }

    public void ShowEquippedItem(
        InventoryItem item,
        EquipmentSlotUI equipmentSlotUI,
        Vector2 screenPosition)
    {
        if (item == null ||
            item.ItemData == null ||
            equipmentSlotUI == null ||
            equipmentSlotUI.GetEquippedItem() != item)
        {
            return;
        }

        bool wasOpen = IsOpen;

        selectedItem = item;
        inventoryController = equipmentSlotUI.InventoryController;
        selectedEquipmentSlotUI = equipmentSlotUI;
        selectedItemIsReadOnly = false;

        FindEquipmentController();
        FindEquipmentVisualController();
        FindPlayerItemDropper();

        gameObject.SetActive(true);

        RefreshMenu();
        SetMenuPosition(screenPosition);

        openedFrame = Time.frameCount;

        if (!wasOpen)
        {
            soundPlayer?.PlayContextMenuOpen();
        }
    }

    /// <summary>
    /// アイテムボックス・ショップ側のアイテム用。
    /// 現段階では詳細表示だけ可能にし、使用・装備・捨てるは表示しません。
    /// </summary>
    public void ShowReadOnlyItem(
        InventoryItem item,
        Vector2 screenPosition)
    {
        if (item == null || item.ItemData == null)
        {
            return;
        }

        bool wasOpen = IsOpen;

        selectedItem = item;
        inventoryController = null;
        selectedEquipmentSlotUI = null;
        selectedItemIsReadOnly = true;

        gameObject.SetActive(true);

        RefreshMenu();
        SetMenuPosition(screenPosition);

        openedFrame = Time.frameCount;

        if (!wasOpen)
        {
            soundPlayer?.PlayContextMenuOpen();
        }
    }

    public void Hide()
    {
        // 実際に開いていた場合だけ閉じる音を鳴らす
        bool wasOpen = IsOpen;

        if (wasOpen)
        {
            soundPlayer?.PlayContextMenuClose();
        }

        selectedEquipmentSlotUI = null;
        selectedItem = null;
        inventoryController = null;
        selectedItemIsReadOnly = false;

        gameObject.SetActive(false);
    }

    // Equipボタンから呼ぶ
    public void EquipSelectedItem()
    {
        if (selectedItemIsReadOnly ||
            selectedItem == null ||
            inventoryController == null)
        {
            Hide();
            return;
        }

        if (!FindEquipmentController())
        {
            soundPlayer?.PlayFailed();

            Debug.LogWarning(
                "EquipmentController が見つかりません。",
                this
            );

            return;
        }

        bool equipped = equipmentController.TryEquipItem(
            selectedItem,
            out EquipmentResult result
        );

        if (equipped)
        {
            soundPlayer?.PlayPlace();
            Hide();
            return;
        }

        soundPlayer?.PlayFailed();

        Debug.Log(
            $"アイテムを装備できません：{result}",
            this
        );
    }

    public void UseSelectedItem()
    {
        if (selectedItemIsReadOnly ||
            selectedItem == null ||
            inventoryController == null)
        {
            Hide();
            return;
        }

        // 回復アイテムの使用演出中は、効果・消費・SEが
        // 二重に発生しないよう、次の回復アイテムを使わせない
        if (IsUsingConsumable())
        {
            soundPlayer?.PlayFailed();
            healthFullToastUI?.Show(usingConsumableMessage);
            Hide();
            return;
        }

        ConsumableItemData consumableData =
            selectedItem.ItemData as ConsumableItemData;

        AudioClip useClip = consumableData != null
            ? consumableData.UseSound
            : null;

        bool used = inventoryController.TryUseItem(
            selectedItem,
            out ItemUseResult result
        );

        if (used)
        {
            soundPlayer?.PlayUseSound(useClip);
            Hide();
            return;
        }

        if (result == ItemUseResult.HealthIsFull)
        {
            soundPlayer?.PlayHealthFull();
            healthFullToastUI?.Show(localizedHealthFullMessage);
            return;
        }

        if (result == ItemUseResult.TorchIsFull)
        {
            soundPlayer?.PlayFailed();
            healthFullToastUI?.Show(torchFullMessage);
            return;
        }

        if (result == ItemUseResult.TorchControllerNotFound)
        {
            soundPlayer?.PlayFailed();
            healthFullToastUI?.Show(
                torchControllerNotFoundMessage
            );
            return;
        }

        Debug.Log(
            $"アイテムを使用できません：{result}",
            this
        );
    }

    public void RepairSelectedWeapon()
    {
        if (selectedItemIsReadOnly ||
            selectedItem == null ||
            inventoryController == null ||
            !(selectedItem.ItemData is WeaponItemData weaponData))
        {
            soundPlayer?.PlayFailed();
            return;
        }

        selectedItem.EnsureWeaponDurabilityInitialized();

        if (selectedItem.StoredWeaponDurability >=
            weaponData.MaxDurability - 0.0001f)
        {
            soundPlayer?.PlayFailed();
            healthFullToastUI?.Show(weaponAlreadyFullMessage);
            return;
        }

        InventoryItem repairKitItem = FindBestRepairKit(
            inventoryController,
            weaponData.MaxDurability - selectedItem.StoredWeaponDurability
        );

        WeaponRepairItemData repairKitData =
            repairKitItem?.ItemData as WeaponRepairItemData;

        if (repairKitItem == null || repairKitData == null)
        {
            soundPlayer?.PlayFailed();
            healthFullToastUI?.Show(repairKitMissingMessage);
            return;
        }

        FindEquipmentVisualController();

        float repairedAmount;
        bool repaired;

        if (equipmentVisualController != null)
        {
            repaired = equipmentVisualController.TryRepairWeapon(
                selectedItem,
                repairKitData.RepairAmount,
                out repairedAmount
            );
        }
        else
        {
            repairedAmount = selectedItem.RepairWeaponDurability(
                repairKitData.RepairAmount
            );

            if (repairedAmount > 0f)
            {
                selectedItem.SetStoredWeaponJammed(false);
            }

            repaired = repairedAmount > 0f;
        }

        if (!repaired)
        {
            soundPlayer?.PlayFailed();
            return;
        }

        if (repairKitData.ConsumeOnUse)
        {
            inventoryController.RemoveItemAmount(
                repairKitItem,
                1
            );
        }

        soundPlayer?.PlayUseSound(repairKitData.UseSound);

        healthFullToastUI?.Show(
            string.Format(
                weaponRepairSuccessFormat,
                weaponData.DisplayName,
                repairedAmount
            )
        );

        Hide();
    }

    private InventoryItem FindBestRepairKit(
        InventoryController controller,
        float missingDurability)
    {
        if (controller == null || controller.Grid == null)
        {
            return null;
        }

        InventoryItem bestCoveringKit = null;
        float bestCoveringAmount = float.MaxValue;

        InventoryItem strongestFallback = null;
        float strongestAmount = 0f;

        foreach (InventoryItem item in controller.Grid.Items)
        {
            WeaponRepairItemData repairData =
                item?.ItemData as WeaponRepairItemData;

            if (repairData == null || item.Amount <= 0)
            {
                continue;
            }

            float amount = repairData.RepairAmount;

            if (amount >= missingDurability &&
                amount < bestCoveringAmount)
            {
                bestCoveringKit = item;
                bestCoveringAmount = amount;
            }

            if (amount > strongestAmount)
            {
                strongestFallback = item;
                strongestAmount = amount;
            }
        }

        return bestCoveringKit != null
            ? bestCoveringKit
            : strongestFallback;
    }

    public void ShowInformation()
    {
        if (selectedItem == null || selectedItem.ItemData == null)
        {
            Hide();
            return;
        }

        ItemData itemData = selectedItem.ItemData;

        if (informationPanel != null)
        {
            informationItemData = itemData;
            informationInventoryItem = selectedItem;
            RefreshInformationPanel();

            informationPanel.SetActive(true);
            soundPlayer?.PlayInformation();
        }
        else
        {
            Debug.Log(
                $"【{itemData.DisplayName}】\n{itemData.Description}",
                this
            );
        }

        Hide();
    }

    public void CloseInformation()
    {
        if (informationPanel != null)
        {
            informationPanel.SetActive(false);
        }

        informationItemData = null;
        informationInventoryItem = null;
        soundPlayer?.PlayClose();
    }

    public void CloseContextMenu()
    {
        Hide();
    }

    // Trashボタンから呼ばれる
    public void TrashSelectedItem()
    {
        if (selectedItemIsReadOnly ||
            selectedItem == null ||
            selectedItem.ItemData == null)
        {
            Hide();
            return;
        }

        if (!CanDiscard(selectedItem.ItemData))
        {
            soundPlayer?.PlayFailed();
            return;
        }

        if (!FindPlayerItemDropper())
        {
            soundPlayer?.PlayFailed();

            Debug.LogWarning(
                "PlayerItemDropper が見つかりません。",
                this
            );

            return;
        }

        bool dropped;

        // 装備枠から開いたメニューの場合
        if (selectedEquipmentSlotUI != null)
        {
            EquipmentController controller =
                selectedEquipmentSlotUI.EquipmentControllerRef;

            if (controller == null ||
                selectedEquipmentSlotUI.GetEquippedItem() !=
                    selectedItem)
            {
                soundPlayer?.PlayFailed();
                Hide();
                return;
            }

            dropped =
                playerItemDropper.TryDropEquippedItem(
                    controller,
                    selectedEquipmentSlotUI.SlotType
                );
        }
        else
        {
            // 通常インベントリから開いたメニューの場合
            if (inventoryController == null)
            {
                Hide();
                return;
            }

            dropped =
                playerItemDropper.TryDropItem(selectedItem);
        }

        if (!dropped)
        {
            soundPlayer?.PlayFailed();
            return;
        }

        soundPlayer?.PlayTrash();
        Hide();
    }

    private void SubscribeItemTextChanges()
    {
        if (isItemTextChangeSubscribed)
        {
            return;
        }

        ItemData.OnLocalizedTextChanged +=
            HandleLocalizedItemTextChanged;

        isItemTextChangeSubscribed = true;
    }

    private void UnsubscribeItemTextChanges()
    {
        if (!isItemTextChangeSubscribed)
        {
            return;
        }

        ItemData.OnLocalizedTextChanged -=
            HandleLocalizedItemTextChanged;

        isItemTextChangeSubscribed = false;
    }

    private void HandleLocalizedItemTextChanged(
        ItemData changedItemData)
    {
        if (changedItemData == null)
        {
            return;
        }

        if (IsOpen &&
            selectedItem != null &&
            selectedItem.ItemData == changedItemData)
        {
            RefreshMenu();
        }

        if (informationItemData == changedItemData &&
            informationPanel != null &&
            informationPanel.activeInHierarchy)
        {
            RefreshInformationPanel();
        }
    }

    private void RefreshInformationPanel()
    {
        if (informationItemData == null)
        {
            return;
        }

        if (informationTitleText != null)
        {
            informationTitleText.text =
                informationItemData.DisplayName;
        }

        if (informationDescriptionText != null)
        {
            string description = informationItemData.Description;

            if (informationInventoryItem != null &&
                informationInventoryItem.ItemData == informationItemData &&
                informationItemData is WeaponItemData weaponData)
            {
                informationInventoryItem.EnsureWeaponDurabilityInitialized();

                float durabilityPercent = Mathf.Clamp01(
                    informationInventoryItem.StoredWeaponDurability /
                    weaponData.MaxDurability
                );

                float damagePercent =
                    (1f - durabilityPercent) * 100f;

                float jamChance = weaponData.GetJamChance(
                    durabilityPercent
                ) * 100f;

                float spread = weaponData.GetDurabilitySpreadAngle(
                    durabilityPercent
                );

                float reloadMultiplier =
                    weaponData.GetReloadDurationMultiplier(
                        durabilityPercent
                    );

                int repairCost = weaponData.CalculateFullRepairCost(
                    informationInventoryItem.StoredWeaponDurability
                );

                description +=
                    $"\n\n損傷度：{damagePercent:0.#}%" +
                    $"\n耐久度：{durabilityPercent * 100f:0.#}%" +
                    $"\n状態：{(informationInventoryItem.StoredWeaponJammed ? "ジャム中" : (informationInventoryItem.IsWeaponBroken ? "故障" : "使用可能"))}" +
                    $"\n現在ジャム率：{jamChance:0.#}%" +
                    $"\n耐久による最大ブレ：±{spread:0.#}°" +
                    $"\nリロード時間倍率：×{reloadMultiplier:0.##}" +
                    $"\n武器屋修理費：¥{repairCost:N0}";
            }

            informationDescriptionText.text = description;
        }
    }

    private void RefreshMenu()
    {
        if (selectedItem == null || selectedItem.ItemData == null)
        {
            Hide();
            return;
        }

        ItemData itemData = selectedItem.ItemData;

        if (itemNameText != null)
        {
            itemNameText.text = itemData.DisplayName;
        }

        bool isEquippedItem =
            !selectedItemIsReadOnly &&
            selectedEquipmentSlotUI != null &&
            selectedEquipmentSlotUI.GetEquippedItem() ==
                selectedItem;

        bool canEquip =
            !selectedItemIsReadOnly &&
            !isEquippedItem &&
            CanEquip(itemData);

        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(canEquip);
        }

        bool canUse =
            !selectedItemIsReadOnly &&
            itemData is ConsumableItemData;

        if (useButton != null)
        {
            useButton.gameObject.SetActive(canUse);
        }

        if (informationButton != null)
        {
            informationButton.gameObject.SetActive(true);
        }

        if (repairButton != null)
        {
            bool canRepair =
                !selectedItemIsReadOnly &&
                itemData is WeaponItemData repairWeaponData &&
                selectedItem.StoredWeaponDurability <
                    repairWeaponData.MaxDurability - 0.0001f;

            repairButton.gameObject.SetActive(canRepair);
        }

        if (trashButton != null)
        {
            trashButton.gameObject.SetActive(
                !selectedItemIsReadOnly &&
                CanDiscard(itemData)
            );
        }
    }

    private bool CanEquip(ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        if (equipmentController != null)
        {
            return equipmentController.TryGetEquipmentSlot(
                itemData,
                out _
            );
        }

        return itemData is WeaponItemData ||
               itemData is ArmorItemData;
    }

    private bool CanDiscard(ItemData itemData)
    {
        if (itemData == null)
        {
            return false;
        }

        // ItemData側でCan Discardがオフなら捨てられない
        if (!itemData.CanDiscard)
        {
            return false;
        }

        // Questアイテムも捨てられない
        return itemData.ItemType != InventoryItemType.Quest;
    }

    private void SetMenuPosition(Vector2 screenPosition)
    {
        if (menuRect == null)
        {
            return;
        }

        RectTransform parentRect =
            menuRect.parent as RectTransform;

        if (parentRect == null)
        {
            return;
        }

        Camera uiCamera = null;

        if (rootCanvas != null &&
            rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = rootCanvas.worldCamera;
        }

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                parentRect,
                screenPosition,
                uiCamera,
                out Vector2 localPosition))
        {
            return;
        }

        menuRect.anchorMin = new Vector2(0.5f, 0.5f);
        menuRect.anchorMax = new Vector2(0.5f, 0.5f);
        menuRect.pivot = new Vector2(0f, 1f);

        menuRect.anchoredPosition =
            localPosition + cursorOffset;
    }

    private bool IsPointerInsideMenu(Vector2 screenPosition)
    {
        Camera uiCamera = null;

        if (rootCanvas != null &&
            rootCanvas.renderMode != RenderMode.ScreenSpaceOverlay)
        {
            uiCamera = rootCanvas.worldCamera;
        }

        return RectTransformUtility.RectangleContainsScreenPoint(
            menuRect,
            screenPosition,
            uiCamera
        );
    }

    private bool IsPointerOverMenuOrChild(Vector2 screenPosition)
    {
        if (EventSystem.current == null)
        {
            return IsPointerInsideMenu(screenPosition);
        }

        PointerEventData pointerData =
            new PointerEventData(EventSystem.current);

        pointerData.position = screenPosition;

        List<RaycastResult> results =
            new List<RaycastResult>();

        EventSystem.current.RaycastAll(pointerData, results);

        foreach (RaycastResult result in results)
        {
            if (result.gameObject != null &&
                result.gameObject.transform.IsChildOf(transform))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureReferences()
    {
        if (menuRect == null)
        {
            menuRect = GetComponent<RectTransform>();
        }

        if (rootCanvas == null)
        {
            rootCanvas = GetComponentInParent<Canvas>()?.rootCanvas;
        }
    }

    private void RegisterButtons()
    {
        if (buttonsRegistered)
        {
            return;
        }

        if (equipButton != null)
        {
            equipButton.onClick.AddListener(EquipSelectedItem);
        }

        if (useButton != null)
        {
            useButton.onClick.AddListener(UseSelectedItem);
        }

        if (informationButton != null)
        {
            informationButton.onClick.AddListener(ShowInformation);
        }

        if (repairButton != null)
        {
            repairButton.onClick.AddListener(RepairSelectedWeapon);
        }

        if (trashButton != null)
        {
            trashButton.onClick.AddListener(TrashSelectedItem);
        }

        buttonsRegistered = true;
    }

    private void UnregisterButtons()
    {
        if (!buttonsRegistered)
        {
            return;
        }

        if (equipButton != null)
        {
            equipButton.onClick.RemoveListener(EquipSelectedItem);
        }

        if (useButton != null)
        {
            useButton.onClick.RemoveListener(UseSelectedItem);
        }

        if (informationButton != null)
        {
            informationButton.onClick.RemoveListener(ShowInformation);
        }

        if (repairButton != null)
        {
            repairButton.onClick.RemoveListener(RepairSelectedWeapon);
        }

        if (trashButton != null)
        {
            trashButton.onClick.RemoveListener(TrashSelectedItem);
        }

        buttonsRegistered = false;
    }

    private void FindSoundPlayer()
    {
        if (soundPlayer != null)
        {
            return;
        }

        soundPlayer = GetComponentInParent<InventorySoundPlayer>();

        if (soundPlayer == null)
        {
            soundPlayer = FindAnyObjectByType<InventorySoundPlayer>(
                FindObjectsInactive.Include
            );
        }
    }

    private bool FindEquipmentController()
    {
        if (equipmentController != null)
        {
            return true;
        }

        if (inventoryController != null)
        {
            equipmentController =
                inventoryController.GetComponent<EquipmentController>();
        }

        if (equipmentController == null)
        {
            equipmentController =
                FindAnyObjectByType<EquipmentController>(
                    FindObjectsInactive.Include
                );
        }

        return equipmentController != null;
    }

    private bool IsUsingConsumable()
    {
        return FindPlayerWeightController() &&
               playerWeightController.IsUsingConsumable;
    }

    private bool FindPlayerWeightController()
    {
        if (playerWeightController != null)
        {
            return true;
        }

        // InventoryControllerと同じPlayerに付いている場合
        if (inventoryController != null)
        {
            playerWeightController =
                inventoryController.GetComponent<
                    PlayerWeightController
                >();
        }

        if (playerWeightController == null)
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                playerWeightController =
                    player.GetComponent<PlayerWeightController>();
            }
        }

        if (playerWeightController == null)
        {
            playerWeightController =
                FindAnyObjectByType<PlayerWeightController>();
        }

        return playerWeightController != null;
    }

    private bool FindEquipmentVisualController()
    {
        if (equipmentVisualController != null)
        {
            return true;
        }

        if (inventoryController != null)
        {
            equipmentVisualController =
                inventoryController.GetComponent<
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

        return equipmentVisualController != null;
    }

    private bool FindPlayerItemDropper()
    {
        if (playerItemDropper != null)
        {
            return true;
        }

        // InventoryControllerとPlayerItemDropperが
        // 同じPlayerに付いている場合
        if (inventoryController != null)
        {
            playerItemDropper =
                inventoryController.GetComponent<PlayerItemDropper>();
        }

        if (playerItemDropper == null)
        {
            GameObject player =
                GameObject.FindGameObjectWithTag("Player");

            if (player != null)
            {
                playerItemDropper =
                    player.GetComponent<PlayerItemDropper>();
            }
        }

        if (playerItemDropper == null)
        {
            playerItemDropper =
                FindAnyObjectByType<PlayerItemDropper>(
                    FindObjectsInactive.Include
                );
        }

        return playerItemDropper != null;
    }

    private void FindHealthFullToastUI()
    {
        if (healthFullToastUI != null)
        {
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>()?.rootCanvas;

        if (canvas != null)
        {
            healthFullToastUI =
                canvas.GetComponentInChildren<InventoryToastUI>(true);
        }
    }

    private void EnsureLocalizedStrings()
    {
        if (healthFullMessage == null)
        {
            healthFullMessage = new LocalizedString();
        }
    }

    private void SubscribeHealthFullMessage()
    {
        EnsureLocalizedStrings();

        if (isHealthFullMessageSubscribed)
        {
            return;
        }

        healthFullMessage.StringChanged +=
            HandleHealthFullMessageChanged;

        isHealthFullMessageSubscribed = true;
    }

    private void UnsubscribeHealthFullMessage()
    {
        if (!isHealthFullMessageSubscribed)
        {
            return;
        }

        healthFullMessage.StringChanged -=
            HandleHealthFullMessageChanged;

        isHealthFullMessageSubscribed = false;
    }

    private void HandleHealthFullMessageChanged(
        string localizedText)
    {
        localizedHealthFullMessage =
            string.IsNullOrWhiteSpace(localizedText)
                ? "体力満タンです"
                : localizedText;
    }
}
