using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public class ItemBoxUIController : MonoBehaviour
{
    [Header("パネル")]
    [Tooltip("プレイヤー用と箱用のInventoryGridUIを入れた親Panel")]
    [SerializeField] private GameObject itemBoxPanel;

    [Header("グリッドUI")]
    [Tooltip("このPanel内のプレイヤー用InventoryGridUI")]
    [SerializeField] private InventoryGridUI playerGridUI;

    [Tooltip("このPanel内の箱用InventoryGridUI。InspectorのInventory Controllerは空でOKです")]
    [SerializeField] private InventoryGridUI itemBoxGridUI;

    [Header("見出し")]
    [SerializeField] private TMP_Text titleText;

    [SerializeField] private string storageTitleFormat = "{0}";
    [SerializeField] private string shopTitleFormat = "ショップ：{0}";

    [Header("既存インベントリとの連携")]
    [Tooltip("通常Tabインベントリを閉じてから箱を開きたい時に設定")]
    [SerializeField] private InventoryPanelToggle inventoryPanelToggle;

    [Header("プレイヤー操作")]
    [Tooltip("オンの場合、アイテムボックスを開いている間は移動・ジャンプを止めます")]
    [SerializeField] private bool lockPlayerMovementWhileOpen = true;

    [Tooltip("Playerに付いているPlayerMove。未設定なら自動検索します")]
    [SerializeField] private PlayerMove playerMove;

    [Header("武器操作")]
    [Tooltip("オンの場合、アイテムボックスを開いている間は照準・射撃・リロードを止めます")]
    [SerializeField] private bool lockWeaponControlsWhileOpen = true;

    [Tooltip("Playerに付いているPlayerEquipmentVisualController。未設定なら自動検索します")]
    [SerializeField]
    private PlayerEquipmentVisualController equipmentVisualController;

    private Rigidbody2D playerRigidbody;
    private bool wasPlayerMoveEnabledBeforeOpen;
    private bool hasLockedPlayerMovement;
    private bool hasLockedWeaponControls;

    public bool IsOpen =>
        itemBoxPanel != null &&
        itemBoxPanel.activeInHierarchy &&
        currentItemBox != null;

    public ItemBoxInventory CurrentItemBox => currentItemBox;

    private ItemBoxInventory currentItemBox;

    private void Awake()
    {
        FindPlayerMove();
        FindEquipmentVisualController();

        if (itemBoxPanel != null)
        {
            itemBoxPanel.SetActive(false);
        }
    }

    private void OnDisable()
    {
        // CanvasやこのControllerが無効になった時も、
        // プレイヤーだけ操作不能のまま残らないようにする
        UnlockPlayerMovement();
        UnlockWeaponControls();
    }

    private void OnDestroy()
    {
        UnlockPlayerMovement();
        UnlockWeaponControls();
    }

    public void Open(ItemBoxInventory itemBox)
    {
        if (itemBox == null || IsOpen)
        {
            return;
        }

        if (playerGridUI == null || itemBoxGridUI == null)
        {
            Debug.LogWarning(
                "ItemBoxUIController: Player Grid UI または " +
                "Item Box Grid UI が設定されていません。",
                this
            );
            return;
        }

        // 通常のTabインベントリと二重表示にならないようにする
        inventoryPanelToggle?.CloseInventory();

        currentItemBox = itemBox;

        // 箱用Gridだけを、今回開いた箱へ動的に接続する
        itemBoxGridUI.BindItemBoxInventory(currentItemBox);

        RefreshTitle();

        if (itemBoxPanel != null)
        {
            itemBoxPanel.SetActive(true);
        }

        // パネル表示と同時に、移動・ジャンプと武器操作を止める
        LockPlayerMovement();
        LockWeaponControls();

        playerGridUI.RefreshInventoryUI();
        itemBoxGridUI.RefreshInventoryUI();
    }

    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }

        if (itemBoxPanel != null)
        {
            itemBoxPanel.SetActive(false);
        }

        currentItemBox = null;

        // Tabや閉じるボタンで箱を閉じたら、元の操作状態へ戻す
        UnlockPlayerMovement();
        UnlockWeaponControls();
    }

    private void LockPlayerMovement()
    {
        if (!lockPlayerMovementWhileOpen ||
            hasLockedPlayerMovement ||
            !FindPlayerMove())
        {
            return;
        }

        // 死亡などで元から無効だった場合は、閉じても勝手に有効化しない
        wasPlayerMoveEnabledBeforeOpen = playerMove.enabled;
        hasLockedPlayerMovement = true;

        playerMove.enabled = false;

        // 開く直前に歩いていた場合、そのまま滑り続けないように止める
        if (playerRigidbody != null)
        {
            Vector2 velocity = playerRigidbody.linearVelocity;
            velocity.x = 0f;
            playerRigidbody.linearVelocity = velocity;
        }
    }

    private void UnlockPlayerMovement()
    {
        if (!hasLockedPlayerMovement)
        {
            return;
        }

        if (playerMove != null &&
            wasPlayerMoveEnabledBeforeOpen)
        {
            playerMove.enabled = true;
        }

        hasLockedPlayerMovement = false;
        wasPlayerMoveEnabledBeforeOpen = false;
    }

    private void LockWeaponControls()
    {
        if (!lockWeaponControlsWhileOpen ||
            hasLockedWeaponControls ||
            !FindEquipmentVisualController())
        {
            return;
        }

        equipmentVisualController.SetWeaponControlLock(
            this,
            true
        );

        hasLockedWeaponControls = true;
    }

    private void UnlockWeaponControls()
    {
        if (!hasLockedWeaponControls)
        {
            return;
        }

        if (equipmentVisualController != null)
        {
            equipmentVisualController.SetWeaponControlLock(
                this,
                false
            );
        }

        hasLockedWeaponControls = false;
    }

    private bool FindEquipmentVisualController()
    {
        if (equipmentVisualController != null)
        {
            return true;
        }

        if (playerMove != null)
        {
            equipmentVisualController =
                playerMove.GetComponent<
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

        return equipmentVisualController != null;
    }

    private bool FindPlayerMove()
    {
        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>();
        }

        if (playerRigidbody == null && playerMove != null)
        {
            playerRigidbody = playerMove.GetComponent<Rigidbody2D>();
        }

        return playerMove != null;
    }

    private void RefreshTitle()
    {
        if (titleText == null || currentItemBox == null)
        {
            return;
        }

        string format = currentItemBox.BoxKind == ItemBoxKind.Shop
            ? shopTitleFormat
            : storageTitleFormat;

        titleText.text = string.Format(
            format,
            currentItemBox.BoxDisplayName
        );
    }
}
