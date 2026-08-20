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
    public InventoryItem CurrentWeaponItem => currentWeaponItem;

    public event Action<GunShooter> OnActiveGunChanged;

    private GameObject activeWeaponObject;
    private InventoryItem currentWeaponItem;

    private GunShooter currentGunShooter;
    private WeaponAim currentWeaponAim;
    private WeaponItemData currentWeaponData;

    private bool isSubscribed;
    private bool weaponControlsEnabled = true;
    private bool isWeaponHiddenForConsumableUse;

    private readonly HashSet<object> weaponControlLocks =
        new HashSet<object>();

    private readonly HashSet<object> weaponVisibilityLocks =
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
        SaveCurrentWeaponState();
        UnsubscribeFromEquipment();
    }

    private void OnDestroy()
    {
        SaveCurrentWeaponState();
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

    public void SetWeaponControlsEnabled(bool enabled)
    {
        weaponControlsEnabled = enabled;
        ApplyWeaponControlState();
    }

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

    public void SetWeaponVisibilityLock(object owner, bool hidden)
    {
        if (owner == null)
        {
            return;
        }

        bool changed = hidden
            ? weaponVisibilityLocks.Add(owner)
            : weaponVisibilityLocks.Remove(owner);

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
            activeWeaponObject.GetComponentInChildren<GunShooter>(true);

        currentWeaponAim =
            activeWeaponObject.GetComponentInChildren<WeaponAim>(true);

        if (currentGunShooter == null)
        {
            Debug.LogWarning(
                $"{weaponData.DisplayName} のPrefabに GunShooter がありません。",
                activeWeaponObject
            );
        }
        else
        {
            currentGunShooter.SetInventoryPanel(inventoryPanel);

            currentGunShooter.ConfigureAmmoSystem(
                currentWeaponData,
                equipmentController.InventoryController
            );

            currentWeaponItem.EnsureWeaponDurabilityInitialized();

            currentGunShooter.ConfigureDurabilitySystem(
                currentWeaponData,
                currentWeaponItem.StoredWeaponDurability,
                currentWeaponItem.HasStoredWeaponDurability,
                currentWeaponItem.StoredWeaponJammed
            );

            currentGunShooter.OnMagazineAmmoChanged +=
                HandleMagazineAmmoChanged;

            currentGunShooter.OnLoadedAmmoChanged +=
                HandleLoadedAmmoChanged;

            currentGunShooter.OnDurabilityChanged +=
                HandleDurabilityChanged;

            currentGunShooter.OnJamStateChanged +=
                HandleJamStateChanged;

            RestoreWeaponAmmo();
            SaveCurrentWeaponDurability();
        }

        if (currentWeaponAim == null)
        {
            Debug.LogWarning(
                $"{weaponData.DisplayName} のPrefabに WeaponAim がありません。",
                activeWeaponObject
            );
        }
        else
        {
            currentWeaponAim.SetInventoryPanel(inventoryPanel);
        }

        ApplyWeaponControlState();
        OnActiveGunChanged?.Invoke(currentGunShooter);

        Log($"{weaponData.DisplayName} を装備しました。");
    }

    private void RestoreWeaponAmmo()
    {
        if (currentWeaponItem == null ||
            currentGunShooter == null)
        {
            return;
        }

        if (currentWeaponItem.HasStoredMagazineAmmo)
        {
            AmmoItemData storedType =
                currentWeaponItem.HasStoredMagazineAmmoType
                    ? currentWeaponItem.StoredMagazineAmmoType
                    : currentWeaponData?.PreferredAmmo;

            currentGunShooter.SetMagazineState(
                currentWeaponItem.StoredMagazineAmmo,
                storedType
            );

            // 旧セーブで弾種が無かった場合も、ここで補完して以降保存する。
            currentWeaponItem.SetStoredMagazineAmmoState(
                currentGunShooter.CurrentAmmo,
                currentGunShooter.LoadedAmmo
            );

            return;
        }

        currentGunShooter.SetMagazineState(
            0,
            currentWeaponData?.PreferredAmmo
        );

        currentWeaponItem.SetStoredMagazineAmmoState(
            0,
            currentWeaponData?.PreferredAmmo
        );
    }

    private void HandleMagazineAmmoChanged(int ammo)
    {
        if (currentWeaponItem == null)
        {
            return;
        }

        currentWeaponItem.SetStoredMagazineAmmoState(
            ammo,
            currentGunShooter != null
                ? currentGunShooter.LoadedAmmo
                : currentWeaponItem.StoredMagazineAmmoType
        );
    }

    private void HandleLoadedAmmoChanged(AmmoItemData ammoType)
    {
        if (currentWeaponItem == null)
        {
            return;
        }

        currentWeaponItem.SetStoredMagazineAmmoState(
            currentGunShooter != null
                ? currentGunShooter.CurrentAmmo
                : currentWeaponItem.StoredMagazineAmmo,
            ammoType
        );
    }

    private void HandleDurabilityChanged(
        float currentDurability,
        float maximumDurability)
    {
        if (currentWeaponItem == null)
        {
            return;
        }

        currentWeaponItem.SetStoredWeaponDurability(
            currentDurability
        );
    }

    private void HandleJamStateChanged(bool jammed)
    {
        if (currentWeaponItem == null)
        {
            return;
        }

        currentWeaponItem.SetStoredWeaponJammed(jammed);
    }

    /// <summary>
    /// 修理キット・武器屋などでInventoryItem側の耐久度を変更した後、
    /// 現在装備中の実体GunShooterへ即時反映します。
    /// </summary>
    public void SynchronizeWeaponConditionFromItem(InventoryItem weaponItem)
    {
        if (weaponItem == null ||
            weaponItem != currentWeaponItem ||
            currentGunShooter == null)
        {
            return;
        }

        weaponItem.EnsureWeaponDurabilityInitialized();

        currentGunShooter.SetCurrentDurability(
            weaponItem.StoredWeaponDurability
        );

        currentGunShooter.SetJammedState(
            weaponItem.StoredWeaponJammed
        );
    }

    /// <summary>
    /// 指定銃を修理します。装備中ならGunShooterにも即時反映します。
    /// 修理に成功した場合はジャムも解除します。
    /// </summary>
    public bool TryRepairWeapon(
        InventoryItem weaponItem,
        float repairAmount,
        out float repairedAmount)
    {
        repairedAmount = 0f;

        if (weaponItem == null ||
            !(weaponItem.ItemData is WeaponItemData) ||
            repairAmount <= 0f)
        {
            return false;
        }

        repairedAmount = weaponItem.RepairWeaponDurability(
            repairAmount
        );

        if (repairedAmount <= 0f)
        {
            return false;
        }

        weaponItem.SetStoredWeaponJammed(false);
        SynchronizeWeaponConditionFromItem(weaponItem);
        return true;
    }

    public bool TryRepairWeaponToFull(
        InventoryItem weaponItem,
        out float repairedAmount)
    {
        repairedAmount = 0f;

        if (weaponItem == null ||
            !(weaponItem.ItemData is WeaponItemData weaponData))
        {
            return false;
        }

        weaponItem.EnsureWeaponDurabilityInitialized();
        float before = weaponItem.StoredWeaponDurability;

        if (before >= weaponData.MaxDurability)
        {
            return false;
        }

        weaponItem.RepairWeaponToFull();
        repairedAmount = Mathf.Max(
            0f,
            weaponData.MaxDurability - before
        );

        SynchronizeWeaponConditionFromItem(weaponItem);
        return repairedAmount > 0f;
    }

    private void SaveCurrentWeaponDurability()
    {
        if (currentWeaponItem == null ||
            currentGunShooter == null)
        {
            return;
        }

        currentWeaponItem.SetStoredWeaponDurability(
            currentGunShooter.CurrentDurability
        );
    }

    private void SaveCurrentWeaponState()
    {
        SaveCurrentWeaponAmmo();
        SaveCurrentWeaponDurability();
        SaveCurrentWeaponJamState();
    }

    private void SaveCurrentWeaponJamState()
    {
        if (currentWeaponItem == null ||
            currentGunShooter == null)
        {
            return;
        }

        currentWeaponItem.SetStoredWeaponJammed(
            currentGunShooter.IsJammed
        );
    }

    private void SaveCurrentWeaponAmmo()
    {
        if (currentWeaponItem == null ||
            currentGunShooter == null)
        {
            return;
        }

        currentWeaponItem.SetStoredMagazineAmmoState(
            currentGunShooter.CurrentAmmo,
            currentGunShooter.LoadedAmmo
        );
    }

    private void ClearActiveWeapon()
    {
        SaveCurrentWeaponState();

        if (currentGunShooter != null)
        {
            currentGunShooter.OnMagazineAmmoChanged -=
                HandleMagazineAmmoChanged;

            currentGunShooter.OnLoadedAmmoChanged -=
                HandleLoadedAmmoChanged;

            currentGunShooter.OnDurabilityChanged -=
                HandleDurabilityChanged;

            currentGunShooter.OnJamStateChanged -=
                HandleJamStateChanged;
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
        bool weaponIsVisible =
            !isWeaponHiddenForConsumableUse &&
            weaponVisibilityLocks.Count == 0;

        bool canUseWeapon =
            currentWeaponData != null &&
            weaponControlsEnabled &&
            weaponControlLocks.Count == 0 &&
            weaponIsVisible;

        currentGunShooter?.SetGunEquipped(canUseWeapon);
        currentWeaponAim?.SetGunEquipped(canUseWeapon);

        ApplyWeaponVisualState();
    }

    private void ApplyWeaponVisualState()
    {
        if (activeWeaponObject == null)
        {
            return;
        }

        bool shouldShowWeapon =
            !isWeaponHiddenForConsumableUse &&
            weaponVisibilityLocks.Count == 0;

        if (activeWeaponObject.activeSelf != shouldShowWeapon)
        {
            activeWeaponObject.SetActive(shouldShowWeapon);
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
        if (!isSubscribed || equipmentController == null)
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

        equipmentController = GetComponent<EquipmentController>();

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
