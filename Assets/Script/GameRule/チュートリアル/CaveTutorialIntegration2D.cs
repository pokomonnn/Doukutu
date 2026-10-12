using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 既存ゲームの「成功した結果」を CaveTutorialManager2D へ渡すアダプターです。
/// 通常Sceneには置かず、チュートリアルSceneだけに置いてください。
/// </summary>
[DisallowMultipleComponent]
public sealed class CaveTutorialIntegration2D : MonoBehaviour
{
    [Header("基本参照")]
    [SerializeField] private CaveTutorialManager2D manager;
    [SerializeField] private PlayerMove playerMove;
    [SerializeField] private Rigidbody2D playerBody;
    [SerializeField] private WallClimbController wallClimb;
    [SerializeField] private PlayerProneController prone;

    [Header("ItemBox")]
    [SerializeField] private ItemBoxInteractable tutorialItemBox;
    [SerializeField] private ItemBoxInventory tutorialItemBoxInventory;

    [Header("Inventory・装備")]
    [SerializeField] private InventoryPanelToggle inventoryToggle;
    [SerializeField] private EquipmentController equipment;
    [SerializeField] private PlayerEquipmentVisualController equipmentVisual;

    [Header("戦闘")]
    [Tooltip("未設定ならManagerのTutorial EnemyからCharacterHealthを自動取得します。")]
    [SerializeField] private CharacterHealth tutorialEnemyHealth;

    [Tooltip("発射からこの秒数以内のTutorial Enemy被ダメージを、Playerの射撃命中として扱います。")]
    [SerializeField, Min(0.1f)] private float playerShotHitWindow = 5f;

    [Header("救出")]
    [SerializeField] private PlayerCarryController2D carry;
    [SerializeField] private CarryableObject2D tutorialNpc;
    [SerializeField] private ElevatorCarryReceiver2D elevatorReceiver;

    [Header("松明・状態異常")]
    [SerializeField] private TorchController torch;

    [Tooltip("骨折状態を監視します。未設定ならPlayerまたはSceneから自動検索します。")]
    [SerializeField] private PlayerStatusConditionController statusConditions;

    [Header("壁下降の実移動判定")]
    [SerializeField, Min(0.001f)] private float minimumDownwardMove = 0.015f;

    [Header("診断")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("ONなら現在Step・参照・装備/ほふく/NPC状態を定期的にConsoleへ表示します。")]
    [SerializeField] private bool showDetailedDebugLogs = true;

    [Tooltip("詳細診断を何秒おきに表示するか。")]
    [SerializeField, Min(0.5f)] private float diagnosticInterval = 2f;

    private GunShooter subscribedGun;

    private bool previousWallPositionValid;
    private float previousWallY;

    private bool previousTorchValid;
    private float previousTorchPercent;

    private bool manualBackpackDropSeen;

    private bool tutorialBoxOpened;
    private int tutorialBoxAmountWhenOpened = -1;
    private bool pendingItemCollected;

    private bool pendingProneEntered;

    private int lastKnownGunAmmo = -1;
    private float lastPlayerShotTime = -999f;

    private bool reloadKeySeen;
    private bool reloadStartedSeen;
    private bool pendingReloadSucceeded;
    private int reloadStartAmmo = -1;

    private bool pendingEnemyHit;

    private float nextDiagnosticTime;
    private CaveTutorialStep2D lastDiagnosticStep = (CaveTutorialStep2D)(-1);
    private string lastReferenceSummary = string.Empty;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeEvents();
        BindCurrentGun();
    }

    private void Start()
    {
        FindReferences();
        SubscribeEvents();
        BindCurrentGun();

        if (torch != null)
        {
            previousTorchPercent = torch.TorchPercent;
            previousTorchValid = true;
            manager?.SetTorchLevel01(previousTorchPercent);
        }

        if (statusConditions != null)
        {
            manager?.SetFractured(statusConditions.IsFractured);
        }

        LogDetailedState("Start");
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
        UnbindGun();
    }

    private void FindReferences()
    {
        if (manager == null)
        {
            manager = FindAnyObjectByType<CaveTutorialManager2D>(
                FindObjectsInactive.Include
            );
        }

        if (playerMove == null)
        {
            playerMove = FindAnyObjectByType<PlayerMove>(
                FindObjectsInactive.Include
            );
        }

        if (playerMove != null)
        {
            if (playerBody == null)
            {
                playerBody = playerMove.GetComponent<Rigidbody2D>();
            }

            if (wallClimb == null)
            {
                wallClimb = playerMove.GetComponent<WallClimbController>();
            }

            if (prone == null)
            {
                prone = playerMove.GetComponent<PlayerProneController>();
            }

            if (carry == null)
            {
                carry = playerMove.GetComponent<PlayerCarryController2D>();
            }

            if (equipmentVisual == null)
            {
                equipmentVisual =
                    playerMove.GetComponent<PlayerEquipmentVisualController>();
            }
        }

        // EquipmentControllerはPlayer直下とは限らないのでScene全体からも探す。
        if (equipment == null)
        {
            equipment = FindAnyObjectByType<EquipmentController>(
                FindObjectsInactive.Include
            );
        }

        if (equipmentVisual == null)
        {
            equipmentVisual =
                FindAnyObjectByType<PlayerEquipmentVisualController>(
                    FindObjectsInactive.Include
                );
        }

        if (inventoryToggle == null)
        {
            inventoryToggle = FindAnyObjectByType<InventoryPanelToggle>(
                FindObjectsInactive.Include
            );
        }

        if (tutorialItemBox != null &&
            tutorialItemBoxInventory == null)
        {
            tutorialItemBoxInventory =
                tutorialItemBox.GetComponent<ItemBoxInventory>();
        }

        if (tutorialNpc == null &&
            manager != null &&
            manager.TutorialAdventurer != null)
        {
            GameObject npcObject = manager.TutorialAdventurer;

            tutorialNpc = npcObject.GetComponent<CarryableObject2D>();

            if (tutorialNpc == null)
            {
                tutorialNpc =
                    npcObject.GetComponentInChildren<CarryableObject2D>(true);
            }

            if (tutorialNpc == null)
            {
                tutorialNpc =
                    npcObject.GetComponentInParent<CarryableObject2D>();
            }
        }

        if (carry == null)
        {
            carry = FindAnyObjectByType<PlayerCarryController2D>(
                FindObjectsInactive.Include
            );
        }

        if (elevatorReceiver == null)
        {
            elevatorReceiver = FindAnyObjectByType<ElevatorCarryReceiver2D>(
                FindObjectsInactive.Include
            );
        }

        if (torch == null)
        {
            torch = FindAnyObjectByType<TorchController>(
                FindObjectsInactive.Include
            );
        }

        if (statusConditions == null && playerMove != null)
        {
            statusConditions =
                playerMove.GetComponent<PlayerStatusConditionController>();

            if (statusConditions == null)
            {
                statusConditions =
                    playerMove.GetComponentInParent<PlayerStatusConditionController>();
            }

            if (statusConditions == null)
            {
                statusConditions =
                    playerMove.GetComponentInChildren<PlayerStatusConditionController>(true);
            }
        }

        if (statusConditions == null)
        {
            statusConditions =
                FindAnyObjectByType<PlayerStatusConditionController>(
                    FindObjectsInactive.Include
                );
        }

        ResolveTutorialEnemyHealth();
    }

    private void ResolveTutorialEnemyHealth()
    {
        if (tutorialEnemyHealth != null ||
            manager == null ||
            manager.TutorialEnemy == null)
        {
            return;
        }

        GameObject enemy = manager.TutorialEnemy;

        tutorialEnemyHealth = enemy.GetComponent<CharacterHealth>();

        if (tutorialEnemyHealth == null)
        {
            tutorialEnemyHealth =
                enemy.GetComponentInChildren<CharacterHealth>(true);
        }

        if (tutorialEnemyHealth == null)
        {
            tutorialEnemyHealth =
                enemy.GetComponentInParent<CharacterHealth>();
        }
    }

    private bool eventsSubscribed;

    private void SubscribeEvents()
    {
        if (eventsSubscribed)
        {
            return;
        }

        if (manager == null)
        {
            return;
        }

        if (playerMove != null)
        {
            playerMove.JumpPerformed += HandleJumped;
        }

        if (wallClimb != null)
        {
            wallClimb.WallClimbStarted += HandleWallClimbed;
        }

        if (prone != null)
        {
            prone.ProneStateChanged += HandleProneStateChanged;
        }

        if (tutorialItemBox != null)
        {
            tutorialItemBox.Opened += HandleItemBoxOpened;
        }

        if (tutorialItemBoxInventory != null)
        {
            tutorialItemBoxInventory.OnInventoryChanged +=
                HandleTutorialItemBoxInventoryChanged;
        }

        if (inventoryToggle != null)
        {
            inventoryToggle.InventoryOpened += HandleInventoryOpened;
        }

        if (equipment != null)
        {
            equipment.OnEquipmentChanged += HandleEquipmentChanged;
        }

        if (equipmentVisual != null)
        {
            equipmentVisual.OnActiveGunChanged += HandleActiveGunChanged;
        }

        if (tutorialEnemyHealth != null)
        {
            tutorialEnemyHealth.Damaged += HandleTutorialEnemyDamaged;
        }

        if (carry != null)
        {
            carry.CarryableDropped += HandleCarryableDropped;
        }

        if (statusConditions != null)
        {
            statusConditions.ConditionsChanged += HandleConditionsChanged;
        }

        manager.StepChanged += HandleTutorialStepChanged;

        eventsSubscribed = true;

        LogReferenceWarnings();
        LogReferenceSummary("SubscribeEvents");
    }

    private void UnsubscribeEvents()
    {
        if (!eventsSubscribed)
        {
            return;
        }

        if (playerMove != null)
        {
            playerMove.JumpPerformed -= HandleJumped;
        }

        if (wallClimb != null)
        {
            wallClimb.WallClimbStarted -= HandleWallClimbed;
        }

        if (prone != null)
        {
            prone.ProneStateChanged -= HandleProneStateChanged;
        }

        if (tutorialItemBox != null)
        {
            tutorialItemBox.Opened -= HandleItemBoxOpened;
        }

        if (tutorialItemBoxInventory != null)
        {
            tutorialItemBoxInventory.OnInventoryChanged -=
                HandleTutorialItemBoxInventoryChanged;
        }

        if (inventoryToggle != null)
        {
            inventoryToggle.InventoryOpened -= HandleInventoryOpened;
        }

        if (equipment != null)
        {
            equipment.OnEquipmentChanged -= HandleEquipmentChanged;
        }

        if (equipmentVisual != null)
        {
            equipmentVisual.OnActiveGunChanged -= HandleActiveGunChanged;
        }

        if (tutorialEnemyHealth != null)
        {
            tutorialEnemyHealth.Damaged -= HandleTutorialEnemyDamaged;
        }

        if (carry != null)
        {
            carry.CarryableDropped -= HandleCarryableDropped;
        }

        if (statusConditions != null)
        {
            statusConditions.ConditionsChanged -= HandleConditionsChanged;
        }

        if (manager != null)
        {
            manager.StepChanged -= HandleTutorialStepChanged;
        }

        eventsSubscribed = false;
    }

    private void LogReferenceWarnings()
    {
        if (!showDebugLogs)
        {
            return;
        }

        if (prone == null)
        {
            Debug.LogWarning(
                "[CaveTutorialIntegration] PlayerProneController が見つかりません。ほふくチュートリアルを完了できません。",
                this
            );
        }

        if (tutorialItemBox == null)
        {
            Debug.LogWarning(
                "[CaveTutorialIntegration] Tutorial Item Box が未設定です。",
                this
            );
        }

        if (tutorialItemBoxInventory == null)
        {
            Debug.LogWarning(
                "[CaveTutorialIntegration] Tutorial Item Box Inventory が見つかりません。Item回収を判定できません。",
                this
            );
        }

        if (equipment == null && equipmentVisual == null)
        {
            Debug.LogWarning(
                "[CaveTutorialIntegration] EquipmentController / PlayerEquipmentVisualController が見つかりません。",
                this
            );
        }

        if (tutorialEnemyHealth == null)
        {
            Debug.LogWarning(
                "[CaveTutorialIntegration] Tutorial Enemy の CharacterHealth が見つかりません。攻撃命中を判定できません。",
                this
            );
        }

        if (tutorialNpc == null)
        {
            Debug.LogWarning(
                "[CaveTutorialIntegration] Tutorial NPC の CarryableObject2D が見つかりません。",
                this
            );
        }

        if (statusConditions == null)
        {
            Debug.LogWarning(
                "[CaveTutorialIntegration] PlayerStatusConditionController が見つかりません。骨折Textを自動表示できません。",
                this
            );
        }
    }

    private void HandleJumped()
    {
        manager?.NotifyJumped();
    }

    private void HandleWallClimbed(WallClimbController source)
    {
        if (source == wallClimb)
        {
            manager?.NotifyWallClimbed();
        }
    }

    private void HandleProneStateChanged(bool isProne)
    {
        Diag($"[EVENT][PRONE] ProneStateChanged({isProne}) / CurrentStep={manager?.CurrentStep}");

        if (!isProne)
        {
            return;
        }

        pendingProneEntered = true;
        TryConsumePendingProne();
    }

    private void HandleItemBoxOpened(ItemBoxInteractable source)
    {
        if (source != tutorialItemBox)
        {
            return;
        }

        tutorialBoxOpened = true;
        tutorialBoxAmountWhenOpened = GetTutorialBoxTotalAmount();
        pendingItemCollected = false;

        if (tutorialBoxAmountWhenOpened <= 0 && showDebugLogs)
        {
            Debug.LogWarning(
                "[CaveTutorialIntegration] Tutorial ItemBox が空です。回収チュートリアルを完了できないため、箱に最低1個Itemを入れてください。",
                tutorialItemBox
            );
        }

        manager?.NotifyItemBoxOpened();
    }

    private void HandleTutorialItemBoxInventoryChanged()
    {
        if (!tutorialBoxOpened ||
            tutorialBoxAmountWhenOpened < 0)
        {
            return;
        }

        int currentAmount = GetTutorialBoxTotalAmount();

        if (currentAmount < tutorialBoxAmountWhenOpened)
        {
            pendingItemCollected = true;
        }
    }

    private int GetTutorialBoxTotalAmount()
    {
        if (tutorialItemBoxInventory == null ||
            tutorialItemBoxInventory.Grid == null ||
            tutorialItemBoxInventory.Grid.Items == null)
        {
            return -1;
        }

        int total = 0;

        foreach (InventoryItem item in tutorialItemBoxInventory.Grid.Items)
        {
            if (item != null && item.ItemData != null)
            {
                total += Mathf.Max(0, item.Amount);
            }
        }

        return total;
    }

    private void HandleInventoryOpened()
    {
        Diag($"[EVENT][INVENTORY] InventoryOpened / CurrentStep={manager?.CurrentStep}");
        manager?.NotifyInventoryOpened();
    }

    private void HandleEquipmentChanged()
    {
        bool equippedNow = IsWeaponActuallyEquipped();
        Diag(
            $"[EVENT][EQUIP] OnEquipmentChanged / CurrentStep={manager?.CurrentStep} / " +
            $"EquippedNow={equippedNow} / EquipmentWeapon={(equipment != null && equipment.EquippedWeaponData != null ? equipment.EquippedWeaponData.name : "NULL")}" 
        );

        if (equippedNow)
        {
            manager?.NotifyWeaponEquipped();
        }

        BindCurrentGun();
    }

    private void HandleActiveGunChanged(GunShooter gun)
    {
        Diag(
            $"[EVENT][GUN] OnActiveGunChanged / Gun={(gun != null ? gun.name : "NULL")} / CurrentStep={manager?.CurrentStep}"
        );

        BindGun(gun);

        if (gun != null)
        {
            manager?.NotifyWeaponEquipped();
        }
    }

    private bool IsWeaponActuallyEquipped()
    {
        if (equipment != null &&
            equipment.EquippedWeaponData != null)
        {
            return true;
        }

        return equipmentVisual != null &&
               equipmentVisual.CurrentGunShooter != null;
    }

    private void BindCurrentGun()
    {
        GunShooter candidate = null;

        if (equipmentVisual != null)
        {
            candidate = equipmentVisual.CurrentGunShooter;
        }

        if (candidate == null && playerMove != null)
        {
            GunShooter[] guns =
                playerMove.GetComponentsInChildren<GunShooter>(true);

            foreach (GunShooter gun in guns)
            {
                if (gun != null && gun.IsGunEquipped)
                {
                    candidate = gun;
                    break;
                }
            }
        }

        BindGun(candidate);
    }

    private void BindGun(GunShooter candidate)
    {
        if (candidate == subscribedGun)
        {
            return;
        }

        UnbindGun();

        subscribedGun = candidate;

        if (subscribedGun == null)
        {
            Diag("[GUN-BIND] 接続できるGunShooterがありません。");
            return;
        }

        lastKnownGunAmmo = subscribedGun.CurrentAmmo;
        subscribedGun.OnMagazineAmmoChanged += HandleMagazineAmmoChanged;
        Diag($"[GUN-BIND] Gun={subscribedGun.name} / Ammo={lastKnownGunAmmo} / Equipped={subscribedGun.IsGunEquipped}");
    }

    private void UnbindGun()
    {
        if (subscribedGun != null)
        {
            subscribedGun.OnMagazineAmmoChanged -= HandleMagazineAmmoChanged;
        }

        subscribedGun = null;
        lastKnownGunAmmo = -1;
    }

    private void HandleMagazineAmmoChanged(int newAmmo)
    {
        int previousAmmo = lastKnownGunAmmo;

        if (lastKnownGunAmmo < 0)
        {
            lastKnownGunAmmo = newAmmo;
            Diag($"[EVENT][AMMO] 初期Ammo={newAmmo}");
            return;
        }

        if (newAmmo < lastKnownGunAmmo)
        {
            lastPlayerShotTime = Time.unscaledTime;
            Diag($"[EVENT][SHOT] Ammo {lastKnownGunAmmo} → {newAmmo} / 発砲時刻を記録");
        }
        else if (newAmmo > lastKnownGunAmmo && reloadKeySeen)
        {
            pendingReloadSucceeded = true;
            Diag($"[EVENT][RELOAD] Ammo {lastKnownGunAmmo} → {newAmmo} / Reload成功候補=True");
        }
        else
        {
            Diag($"[EVENT][AMMO] Ammo {previousAmmo} → {newAmmo} / ReloadKeySeen={reloadKeySeen}");
        }

        lastKnownGunAmmo = newAmmo;
    }

    private void HandleTutorialEnemyDamaged(
        int actualDamage,
        int damageGroupId,
        Vector3 hitWorldPosition)
    {
        float elapsed = Time.unscaledTime - lastPlayerShotTime;
        bool recentPlayerShot =
            subscribedGun != null &&
            elapsed <= playerShotHitWindow;

        Diag(
            $"[EVENT][ENEMY-DAMAGE] Damage={actualDamage} / Group={damageGroupId} / " +
            $"CurrentStep={manager?.CurrentStep} / Gun={(subscribedGun != null ? subscribedGun.name : "NULL")} / " +
            $"LastShotAgo={elapsed:0.000}s / Window={playerShotHitWindow:0.00}s / Accept={actualDamage > 0 && recentPlayerShot}"
        );

        if (actualDamage <= 0 || !recentPlayerShot)
        {
            return;
        }

        pendingEnemyHit = true;
        TryConsumePendingEnemyHit();
    }

    private void HandleCarryableDropped(
        CarryableObject2D target,
        PlayerCarryState2D fromState,
        bool manual)
    {
        Diag(
            $"[EVENT][CARRY-DROP] Target={(target != null ? target.name : "NULL")} / " +
            $"TutorialNpc={(tutorialNpc != null ? tutorialNpc.name : "NULL")} / From={fromState} / Manual={manual} / Step={manager?.CurrentStep}"
        );

        if (target == null ||
            target != tutorialNpc ||
            !manual ||
            fromState != PlayerCarryState2D.Backpack)
        {
            return;
        }

        if (manager == null)
        {
            return;
        }

        manager.NotifyAdventurerDropped(target.gameObject);

        manualBackpackDropSeen =
            manager.CurrentStep == CaveTutorialStep2D.RescueAdventurer ||
            manager.CurrentStep == CaveTutorialStep2D.DropAdventurer;
    }

    private void Update()
    {
        if (manager == null)
        {
            return;
        }

        FindReferences();
        BindCurrentGun();

        // 状態異常イベントを何らかの理由で取りこぼしても、現在値から同期します。
        if (statusConditions != null)
        {
            manager.SetFractured(statusConditions.IsFractured);
        }

        UpdateDiagnostics();

        // ItemBoxを開いた後、箱の総Item数が減ったら「回収成功」。
        if (manager.CurrentStep == CaveTutorialStep2D.CollectItem)
        {
            if (tutorialBoxOpened &&
                tutorialBoxAmountWhenOpened >= 0 &&
                GetTutorialBoxTotalAmount() < tutorialBoxAmountWhenOpened)
            {
                pendingItemCollected = true;
            }

            if (pendingItemCollected)
            {
                manager.NotifyItemCollected();
            }
        }

        TryConsumePendingProne();

        // 装備イベントをフェード中に取りこぼしても、実際に武器が装備されていれば進む。
        if (manager.CurrentStep == CaveTutorialStep2D.Equip &&
            IsWeaponActuallyEquipped())
        {
            manager.NotifyWeaponEquipped();
        }

        HandleReloadTutorial();
        TryConsumePendingEnemyHit();

        if (carry != null &&
            tutorialNpc != null &&
            carry.CarriedTarget == tutorialNpc)
        {
            if (carry.IsFrontCarrying &&
                !carry.IsCarryTransitioning)
            {
                manager.NotifyAdventurerPickedUp(
                    tutorialNpc.gameObject
                );
            }

            if (carry.IsBackpackCarrying &&
                !carry.IsCarryTransitioning)
            {
                manager.NotifyAdventurerShouldered(
                    tutorialNpc.gameObject
                );
            }
        }

        // NPCのDropイベントを取りこぼしても、Elevator側の実際のCargo状態から救出完了を判定します。
        // Rescue以降だけ監視するので、通常の探索中に誤判定しません。
        if (tutorialNpc != null &&
            elevatorReceiver != null &&
            (manager.CurrentStep == CaveTutorialStep2D.RescueAdventurer ||
             manager.CurrentStep == CaveTutorialStep2D.DropAdventurer ||
             manager.CurrentStep == CaveTutorialStep2D.EnterElevator))
        {
            List<CarryableObject2D> accepted =
                elevatorReceiver.GetCargoSnapshot(true);

            bool npcStored = accepted.Contains(tutorialNpc);

            if (npcStored)
            {
                Diag(
                    $"[EVENT][ELEVATOR-NPC-STORED] TutorialNpc={tutorialNpc.name} / " +
                    $"CargoCount={accepted.Count} / Step={manager.CurrentStep}"
                );

                manager.NotifyAdventurerStored(
                    tutorialNpc.gameObject
                );

                manualBackpackDropSeen = false;
            }
            else if (manualBackpackDropSeen)
            {
                Diag(
                    $"[EVENT][ELEVATOR-WAIT] Drop済みですがCargoにはまだ入りません。" +
                    $" TutorialNpc={tutorialNpc.name} / CargoCount={accepted.Count} / Step={manager.CurrentStep}"
                );
            }
        }

        if (torch != null)
        {
            float percent = torch.TorchPercent;
            manager.SetTorchLevel01(percent);

            if (previousTorchValid &&
                percent > previousTorchPercent + 0.0001f)
            {
                manager.NotifyTorchBrightened();
            }

            previousTorchPercent = percent;
            previousTorchValid = true;
        }
    }

    private void TryConsumePendingProne()
    {
        if (!pendingProneEntered || manager == null)
        {
            return;
        }

        if (manager.CurrentStep == CaveTutorialStep2D.Prone)
        {
            Diag("[PRONE] pendingProneEntered=True → Manager.NotifyProneEntered()を試行します。");
            // 地点外ならManager側で拒否理由がログ表示されます。
            manager.NotifyProneEntered();
            return;
        }

        // Proneより後へ進んだら消費済み。
        if ((int)manager.CurrentStep > (int)CaveTutorialStep2D.Prone)
        {
            pendingProneEntered = false;
        }
    }

    private void HandleReloadTutorial()
    {
        if (manager.CurrentStep != CaveTutorialStep2D.Reload)
        {
            reloadKeySeen = false;
            reloadStartedSeen = false;
            pendingReloadSucceeded = false;
            reloadStartAmmo = -1;
            return;
        }

        if (subscribedGun == null)
        {
            DiagOncePerInterval("[RELOAD] Step=Reloadですが subscribedGun=NULL のためR判定できません。");
            return;
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            reloadKeySeen = true;
            reloadStartAmmo = subscribedGun.CurrentAmmo;
            Diag($"[EVENT][RELOAD-KEY] R入力 / StartAmmo={reloadStartAmmo} / IsReloading={subscribedGun.IsReloading}");
        }

        if (reloadKeySeen && subscribedGun.IsReloading)
        {
            reloadStartedSeen = true;
        }

        if (pendingReloadSucceeded)
        {
            manager.NotifyReloadCompleted();
            return;
        }

        // AmmoChangedを取りこぼした場合の保険。
        if (reloadKeySeen &&
            reloadStartedSeen &&
            !subscribedGun.IsReloading &&
            reloadStartAmmo >= 0 &&
            subscribedGun.CurrentAmmo > reloadStartAmmo)
        {
            pendingReloadSucceeded = true;
            manager.NotifyReloadCompleted();
        }
    }

    private void TryConsumePendingEnemyHit()
    {
        if (!pendingEnemyHit ||
            manager == null ||
            manager.CurrentStep != CaveTutorialStep2D.ShootEnemy)
        {
            return;
        }

        pendingEnemyHit = false;
        Diag("[COMBAT] Tutorial Enemyへの有効ダメージを確認 → NotifyEnemyHitへ進みます。");

        GameObject enemyObject = tutorialEnemyHealth != null
            ? tutorialEnemyHealth.gameObject
            : manager.TutorialEnemy;

        manager.NotifyEnemyHit(enemyObject);
    }


    private void HandleConditionsChanged(StatusConditionType conditions)
    {
        bool fracturedNow =
            statusConditions != null && statusConditions.IsFractured;

        Diag(
            $"[EVENT][STATUS] ConditionsChanged={conditions} / IsFractured={fracturedNow}"
        );

        manager?.SetFractured(fracturedNow);
    }

    private void HandleTutorialStepChanged(CaveTutorialStep2D step)
    {
        Diag($"[STEP-RECEIVED] Manager StepChanged → {step}");
        LogDetailedState("StepChanged");
    }

    private void UpdateDiagnostics()
    {
        if (!showDetailedDebugLogs || manager == null)
        {
            return;
        }

        if (manager.CurrentStep != lastDiagnosticStep)
        {
            lastDiagnosticStep = manager.CurrentStep;
            LogDetailedState("StepChangeDetected");
            nextDiagnosticTime =
                Time.unscaledTime + Mathf.Max(0.5f, diagnosticInterval);
            return;
        }

        if (Time.unscaledTime >= nextDiagnosticTime)
        {
            LogDetailedState("Heartbeat");
            nextDiagnosticTime =
                Time.unscaledTime + Mathf.Max(0.5f, diagnosticInterval);
        }

        string summary = BuildReferenceSummary();
        if (summary != lastReferenceSummary)
        {
            lastReferenceSummary = summary;
            Debug.Log(
                "[CaveTutorialDiag][REF-CHANGE] " + summary,
                this
            );
        }
    }

    private void LogDetailedState(string reason)
    {
        if (!showDetailedDebugLogs)
        {
            return;
        }

        string carryState = "NULL";
        if (carry != null)
        {
            carryState =
                $"Target={(carry.CarriedTarget != null ? carry.CarriedTarget.name : "NULL")}," +
                $"Front={carry.IsFrontCarrying},Backpack={carry.IsBackpackCarrying},Transition={carry.IsCarryTransitioning}";
        }

        Debug.Log(
            $"[CaveTutorialDiag][STATE][{reason}] " +
            $"Step={(manager != null ? manager.CurrentStep.ToString() : "ManagerNULL")} / " +
            $"Prone={(prone != null ? prone.IsProne.ToString() : "NULL")} / " +
            $"WeaponEquipped={IsWeaponActuallyEquipped()} / " +
            $"Gun={(subscribedGun != null ? subscribedGun.name : "NULL")} / " +
            $"Ammo={(subscribedGun != null ? subscribedGun.CurrentAmmo.ToString() : "-")} / " +
            $"EnemyHealth={(tutorialEnemyHealth != null ? tutorialEnemyHealth.CurrentHealth.ToString() : "NULL")} / " +
            $"Npc={(tutorialNpc != null ? tutorialNpc.name : "NULL")} / Carry={carryState} / " +
            $"Fractured={(statusConditions != null ? statusConditions.IsFractured.ToString() : "StatusNULL")}",
            this
        );
    }

    private void LogReferenceSummary(string reason)
    {
        if (!showDetailedDebugLogs)
        {
            return;
        }

        lastReferenceSummary = BuildReferenceSummary();
        Debug.Log(
            $"[CaveTutorialDiag][REFS][{reason}] {lastReferenceSummary}",
            this
        );
    }

    private string BuildReferenceSummary()
    {
        return
            $"Manager={NameOf(manager)} / PlayerMove={NameOf(playerMove)} / Rigidbody={NameOf(playerBody)} / " +
            $"WallClimb={NameOf(wallClimb)} / Prone={NameOf(prone)} / " +
            $"ItemBox={NameOf(tutorialItemBox)} / BoxInventory={NameOf(tutorialItemBoxInventory)} / " +
            $"InventoryToggle={NameOf(inventoryToggle)} / Equipment={NameOf(equipment)} / " +
            $"EquipmentVisual={NameOf(equipmentVisual)} / EnemyHealth={NameOf(tutorialEnemyHealth)} / " +
            $"Carry={NameOf(carry)} / TutorialNpc={NameOf(tutorialNpc)} / Elevator={NameOf(elevatorReceiver)} / " +
            $"Torch={NameOf(torch)} / Status={NameOf(statusConditions)}";
    }

    private static string NameOf(Object value)
    {
        return value != null ? value.name : "NULL";
    }

    private void Diag(string message)
    {
        if (!showDetailedDebugLogs)
        {
            return;
        }

        Debug.Log("[CaveTutorialDiag]" + message, this);
    }

    private void DiagOncePerInterval(string message)
    {
        if (!showDetailedDebugLogs)
        {
            return;
        }

        if (Time.unscaledTime < nextDiagnosticTime)
        {
            return;
        }

        Debug.LogWarning("[CaveTutorialDiag]" + message, this);
    }

    [ContextMenu("Log Tutorial Diagnostics Now")]
    private void LogTutorialDiagnosticsNow()
    {
        FindReferences();
        BindCurrentGun();
        LogReferenceSummary("ContextMenu");
        LogDetailedState("ContextMenu");
    }

    private void FixedUpdate()
    {
        if (manager == null ||
            wallClimb == null ||
            playerBody == null ||
            !wallClimb.IsWallClimbing)
        {
            previousWallPositionValid = false;
            return;
        }

        float y = playerBody.position.y;

        if (manager.CurrentStep == CaveTutorialStep2D.WallDescend &&
            previousWallPositionValid &&
            Input.GetKey(KeyCode.S) &&
            !Input.GetKey(KeyCode.W) &&
            y < previousWallY - minimumDownwardMove)
        {
            manager.NotifyWallDescended();
        }

        previousWallY = y;
        previousWallPositionValid = true;
    }
}
