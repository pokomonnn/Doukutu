using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class PlayerEquipmentVisualController : MonoBehaviour
{
    [Header("装備管理")]
    [SerializeField] private EquipmentController equipmentController;

    [Header("銃の生成位置")]
    [Tooltip("Playerの子に作った WeaponHolder を設定")]
    [SerializeField] private Transform weaponHolder;

    [Tooltip("Tabで表示・非表示にしているInventory Panel")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("ヘルメット表示")]
    [SerializeField] private GameObject helmetObject;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    public GunShooter CurrentGunShooter => currentGunShooter;
    public WeaponAim CurrentWeaponAim => currentWeaponAim;
    public WeaponItemData CurrentWeaponData => currentWeaponData;

    // ReloadPromptUI が現在の銃を追従するために使う
    public event Action<GunShooter> OnActiveGunChanged;

    private GameObject activeWeaponObject;

    // 装備中の「個別の銃アイテム」
    // 残弾はこのInventoryItemに保存する
    private InventoryItem currentWeaponItem;

    private GunShooter currentGunShooter;
    private WeaponAim currentWeaponAim;
    private WeaponItemData currentWeaponData;

    private bool isSubscribed;
    private bool weaponControlsEnabled = true;
    private bool isWeaponHiddenForConsumableUse;

    // アイテムボックス・キャンプなど、複数の画面から
    // 同時に武器操作を止められるようにするロック一覧
    private readonly HashSet<object> weaponControlLocks =
        new HashSet<object>();

    private void Awake()
    {
        FindEquipmentController();
    }

    private void OnEnable()
    {
        SubscribeToEquipment();
    }

    private void Start()
    {
        SubscribeToEquipment();
        RefreshEquipmentState();
    }

    private void OnDisable()
    {
        SaveCurrentWeaponAmmo();
        UnsubscribeFromEquipment();
    }

    private void OnDestroy()
    {
        SaveCurrentWeaponAmmo();
    }

    [ContextMenu("Refresh Equipment State")]
    public void RefreshEquipmentState()
    {
        if (!FindEquipmentController())
        {
            ClearActiveWeapon();
            ApplyHelmetState(false);

            Debug.LogWarning(
                "PlayerEquipmentVisualController: " +
                "EquipmentController が見つかりません。",
                this
            );

            return;
        }

        SetActiveWeapon(
            equipmentController.PrimaryWeaponItem
        );

        ApplyHelmetState(
            equipmentController.EquippedHelmetData != null
        );
    }

    // 死亡中などに、見た目は残したまま
    // 銃の照準・射撃・リロードだけ止める
    public void SetWeaponControlsEnabled(bool enabled)
    {
        weaponControlsEnabled = enabled;
        ApplyWeaponControlState();
    }

    // アイテムボックス・キャンプなど、特定の画面を開いている間だけ
    // 銃の照準・射撃・リロードを止めるためのロックです。
    // 同時に別のロックが残っている場合は、解除しても武器は使えません。
    public void SetWeaponControlLock(object owner, bool locked)
    {
        if (owner == null)
        {
            return;
        }

        bool changed = locked
            ? weaponControlLocks.Add(owner)
            : weaponControlLocks.Remove(owner);

        if (changed)
        {
            ApplyWeaponControlState();
        }
    }

    public void SetWeaponHiddenForConsumableUse(bool hidden)
    {
        if (isWeaponHiddenForConsumableUse == hidden)
        {
            return;
        }

        isWeaponHiddenForConsumableUse = hidden;

        ApplyWeaponControlState();
    }

    private void HandleEquipmentChanged()
    {
        RefreshEquipmentState();
    }

    private void SetActiveWeapon(InventoryItem weaponItem)
    {
        WeaponItemData weaponData =
            weaponItem != null
                ? weaponItem.ItemData as WeaponItemData
                : null;

        // ヘルメット装備などでイベントが来ても、
        // 同じ銃なら作り直さない
        if (weaponItem == currentWeaponItem &&
            activeWeaponObject != null)
        {
            ApplyWeaponControlState();
            return;
        }

        ClearActiveWeapon();

        if (weaponData == null)
        {
            return;
        }

        if (weaponHolder == null)
        {
            Debug.LogWarning(
                "PlayerEquipmentVisualController: " +
                "Weapon Holder が設定されていません。",
                this
            );

            return;
        }

        if (weaponData.WeaponPrefab == null)
        {
            Debug.LogWarning(
                $"PlayerEquipmentVisualController: " +
                $"{weaponData.DisplayName} の Weapon Prefab が未設定です。",
                this
            );

            return;
        }

        activeWeaponObject = Instantiate(
            weaponData.WeaponPrefab,
            weaponHolder,
            false
        );

        activeWeaponObject.name =
            $"Equipped_{weaponData.DisplayName}";

        currentWeaponItem = weaponItem;
        currentWeaponData = weaponData;

        currentGunShooter =
            activeWeaponObject.GetComponentInChildren<GunShooter>(
                true
            );

        currentWeaponAim =
            activeWeaponObject.GetComponentInChildren<WeaponAim>(
                true
            );

        if (currentGunShooter == null)
        {
            Debug.LogWarning(
                $"{weaponData.DisplayName} のPrefabに " +
                "GunShooter がありません。",
                activeWeaponObject
            );
        }
        else
        {
            currentGunShooter.SetInventoryPanel(
                inventoryPanel
            );

            currentGunShooter.ConfigureAmmoSystem(
                currentWeaponData,
                equipmentController.InventoryController
            );

            currentGunShooter.OnMagazineAmmoChanged +=
                HandleMagazineAmmoChanged;

            RestoreWeaponAmmo();
        }

        if (currentWeaponAim == null)
        {
            Debug.LogWarning(
                $"{weaponData.DisplayName} のPrefabに " +
                "WeaponAim がありません。",
                activeWeaponObject
            );
        }
        else
        {
            currentWeaponAim.SetInventoryPanel(
                inventoryPanel
            );
        }

        ApplyWeaponControlState();

        OnActiveGunChanged?.Invoke(
            currentGunShooter
        );

        Log($"{weaponData.DisplayName} を装備しました。");
    }

    private void RestoreWeaponAmmo()
    {
        if (currentWeaponItem == null ||
            currentGunShooter == null)
        {
            return;
        }

        // 一度でも装備した銃なら、保存していた残弾を復元する
        if (currentWeaponItem.HasStoredMagazineAmmo)
        {
            currentGunShooter.SetCurrentAmmo(
                currentWeaponItem.StoredMagazineAmmo
            );

            return;
        }

        // 初めて装備した銃は空マガジンから開始。
        // これにより、最初の装填もインベントリ内の弾薬を消費する。
        currentGunShooter.SetCurrentAmmo(0);
        currentWeaponItem.SetStoredMagazineAmmo(0);
    }

    private void HandleMagazineAmmoChanged(int ammo)
    {
        if (currentWeaponItem == null)
        {
            return;
        }

        currentWeaponItem.SetStoredMagazineAmmo(ammo);
    }

    private void SaveCurrentWeaponAmmo()
    {
        if (currentWeaponItem == null ||
            currentGunShooter == null)
        {
            return;
        }

        currentWeaponItem.SetStoredMagazineAmmo(
            currentGunShooter.CurrentAmmo
        );
    }

    private void ClearActiveWeapon()
    {
        // 銃を消す直前に残弾を保存する
        SaveCurrentWeaponAmmo();

        if (currentGunShooter != null)
        {
            currentGunShooter.OnMagazineAmmoChanged -=
                HandleMagazineAmmoChanged;
        }

        bool hadWeapon =
            activeWeaponObject != null ||
            currentWeaponItem != null;

        currentGunShooter?.SetGunEquipped(false);
        currentWeaponAim?.SetGunEquipped(false);

        if (activeWeaponObject != null)
        {
            activeWeaponObject.SetActive(false);

            if (Application.isPlaying)
            {
                Destroy(activeWeaponObject);
            }
            else
            {
                DestroyImmediate(activeWeaponObject);
            }
        }

        activeWeaponObject = null;
        currentWeaponItem = null;
        currentGunShooter = null;
        currentWeaponAim = null;
        currentWeaponData = null;

        if (hadWeapon)
        {
            OnActiveGunChanged?.Invoke(null);
        }
    }

    private void ApplyWeaponControlState()
    {
        bool canUseWeapon =
            currentWeaponData != null &&
            weaponControlsEnabled &&
            weaponControlLocks.Count == 0 &&
            !isWeaponHiddenForConsumableUse;

        currentGunShooter?.SetGunEquipped(
            canUseWeapon
        );

        currentWeaponAim?.SetGunEquipped(
            canUseWeapon
        );

        ApplyWeaponVisualState();
    }

    private void ApplyWeaponVisualState()
    {
        if (activeWeaponObject == null)
        {
            return;
        }

        bool shouldShowWeapon =
            !isWeaponHiddenForConsumableUse;

        if (activeWeaponObject.activeSelf !=
            shouldShowWeapon)
        {
            activeWeaponObject.SetActive(
                shouldShowWeapon
            );
        }
    }

    private void ApplyHelmetState(bool equipped)
    {
        if (helmetObject == null)
        {
            return;
        }

        helmetObject.SetActive(equipped);
    }

    private void SubscribeToEquipment()
    {
        if (isSubscribed || !FindEquipmentController())
        {
            return;
        }

        equipmentController.OnEquipmentChanged +=
            HandleEquipmentChanged;

        isSubscribed = true;
    }

    private void UnsubscribeFromEquipment()
    {
        if (!isSubscribed ||
            equipmentController == null)
        {
            return;
        }

        equipmentController.OnEquipmentChanged -=
            HandleEquipmentChanged;

        isSubscribed = false;
    }

    private bool FindEquipmentController()
    {
        if (equipmentController != null)
        {
            return true;
        }

        equipmentController =
            GetComponent<EquipmentController>();

        if (equipmentController != null)
        {
            return true;
        }

        equipmentController =
            FindAnyObjectByType<EquipmentController>(
                FindObjectsInactive.Include
            );

        return equipmentController != null;
    }

    private void Log(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.Log(
            $"[PlayerEquipmentVisualController] {message}",
            this
        );
    }
}
