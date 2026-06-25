using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
    [SerializeField] private Button trashButton;

    [Header("Information表示（後で作成してもOK）")]
    [SerializeField] private GameObject informationPanel;
    [SerializeField] private TMP_Text informationTitleText;
    [SerializeField] private TMP_Text informationDescriptionText;

    [Header("装備")]
    [SerializeField] private EquipmentController equipmentController;

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

    [SerializeField] private InventorySoundPlayer soundPlayer;

    [Header("通知UI")]
    [SerializeField] private InventoryToastUI healthFullToastUI;

    

    private bool buttonsRegistered;
    private int openedFrame = -1;

    public bool IsOpen =>
        gameObject.activeInHierarchy &&
        selectedItem != null;

    private void Awake()
    {
        EnsureReferences();
        FindSoundPlayer();
        FindEquipmentController();
        FindPlayerItemDropper();
        FindHealthFullToastUI();
        RegisterButtons();
    }

    private void OnEnable()
    {
        // 親のInventoryPanelを閉じてから再度開いた時に、
        // 前回の空メニューが残らないようにする
        if (selectedItem == null && Time.frameCount > 0)
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        selectedItem = null;
        inventoryController = null;
        selectedEquipmentSlotUI = null;
    }

    private void OnDestroy()
    {
        UnregisterButtons();
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

        FindEquipmentController();
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
        inventoryController =
            equipmentSlotUI.InventoryController;

        selectedEquipmentSlotUI =
            equipmentSlotUI;

        FindEquipmentController();
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

        gameObject.SetActive(false);
    }

    // Equipボタンから呼ぶ
    public void EquipSelectedItem()
    {
        if (selectedItem == null || inventoryController == null)
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
        if (selectedItem == null || inventoryController == null)
        {
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

            healthFullToastUI?.Show("体力満タンです");

            return;
        }

        Debug.Log(
            $"アイテムを使用できません：{result}",
            this
        );
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
            if (informationTitleText != null)
            {
                informationTitleText.text = itemData.DisplayName;
            }

            if (informationDescriptionText != null)
            {
                informationDescriptionText.text = itemData.Description;
            }

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

        soundPlayer?.PlayClose();
    }

    public void CloseContextMenu()
    {
        Hide();
    }

    // Trashボタンから呼ばれる
    public void TrashSelectedItem()
    {
        if (selectedItem == null ||
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
    selectedEquipmentSlotUI != null &&
    selectedEquipmentSlotUI.GetEquippedItem() ==
        selectedItem;

        bool canEquip =
            !isEquippedItem &&
            CanEquip(itemData);

        if (equipButton != null)
        {
            equipButton.gameObject.SetActive(canEquip);
        }

        bool canUse = itemData is ConsumableItemData;

        if (useButton != null)
        {
            useButton.gameObject.SetActive(canUse);
        }

        if (informationButton != null)
        {
            informationButton.gameObject.SetActive(true);
        }

        if (trashButton != null)
        {
            trashButton.gameObject.SetActive(
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
}