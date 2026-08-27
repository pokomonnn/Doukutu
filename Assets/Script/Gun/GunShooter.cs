using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(AudioSource))]
public class GunShooter : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Transform muzzlePoint;
    [SerializeField] private GameObject bulletPrefab;

    [Header("UI参照")]
    [Tooltip("Tabで表示・非表示にしているインベントリの親Panelを設定")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("回復中の射撃制限")]
    [Tooltip("未設定ならPlayerから自動取得します")]
    [SerializeField]
    private PlayerWeightController playerWeightController;

    [Header("SAN値による射撃精度")]
    [Tooltip("未設定ならPlayerから自動取得します")]
    [SerializeField]
    private PlayerSanityController playerSanityController;

    [Tooltip("このSAN割合を下回ると、弾がランダムにブレ始めます")]
    [SerializeField, Range(0f, 1f)]
    private float accuracyLossStartSanityPercent = 0.7f;

    [Tooltip("SAN値が0の時に、左右どちらかへ最大何度ブレるか")]
    [SerializeField, Min(0f)]
    private float maxSpreadAngleAtZeroSanity = 12f;

    [Tooltip("オフならSAN値に関係なく通常どおり真っすぐ飛びます")]
    [SerializeField]
    private bool useSanityAccuracyPenalty = true;

    [Header("弾薬連携")]
    [Tooltip("装備時にPlayerEquipmentVisualControllerから設定されます。Prefab側では未設定でOKです。")]
    [SerializeField] private WeaponItemData weaponItemData;

    [Tooltip("装備時にPlayerEquipmentVisualControllerから設定されます。")]
    [SerializeField] private InventoryController inventoryController;

    [Header("弾種切替")]
    [Tooltip("オンならBキーで、所持中の互換弾薬を順番に選択できます。")]
    [SerializeField] private bool enableAmmoSwitchWithBKey = true;

    [Header("射撃設定")]
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float bulletLifeTime = 3f;

    [Header("装弾数")]
    [Tooltip("この銃に入れられる最大弾数")]
    [SerializeField, Min(1)] private int magazineSize = 10;

    [Tooltip("現在マガジンに入っている弾数")]
    [SerializeField, Min(0)] private int currentAmmo = 0;

    [Tooltip("単体テスト用です。通常の装備武器ではオフにしてください。")]
    [SerializeField] private bool fillMagazineOnStart = false;

    [Header("リロード設定")]
    [Tooltip("リロードにかかる秒数")]
    [SerializeField, Min(0f)] private float reloadDuration = 1.5f;

    [Header("サウンド")]
    [SerializeField] private AudioSource gunAudioSource;

    [Tooltip("発射時の音")]
    [SerializeField] private AudioClip shotSound;

    [Tooltip("リロード開始時の音")]
    [SerializeField] private AudioClip reloadSound;

    [Tooltip("弾切れ時に鳴らす音")]
    [SerializeField] private AudioClip emptySound;

    [Range(0f, 1f)]
    [SerializeField] private float shotVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float reloadVolume = 1f;

    [Range(0f, 1f)]
    [SerializeField] private float emptyVolume = 1f;

    [Tooltip("弾切れ音が連続で鳴りすぎないための間隔")]
    [SerializeField] private float emptySoundInterval = 0.4f;

    [Header("耐久度")]
    [Tooltip("装備時にWeaponItemDataとInventoryItemから設定されます。Prefab側では通常触りません。")]
    [SerializeField, Min(0f)] private float currentDurability = 100f;

    [SerializeField, Min(1f)] private float maxDurability = 100f;
    [SerializeField, Min(0f)] private float durabilityLossPerShot = 0.1f;

    [Tooltip("耐久度0の銃で射撃しようとした時の音。未設定ならEmpty Soundを使用します。")]
    [SerializeField] private AudioClip brokenSound;

    [Header("低耐久・ジャム音")]
    [Tooltip("ジャムが発生した瞬間の音。未設定ならEmpty Soundを使用します。")]
    [SerializeField] private AudioClip jamSound;

    [Tooltip("Rキーでジャム解除を開始した時の操作音。")]
    [SerializeField] private AudioClip jamClearSound;

    [SerializeField, Range(0f, 1f)] private float jamVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float jamClearVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float brokenVolume = 1f;

    [Header("状態")]
    [SerializeField] private bool isGunEquipped = true;

    [Header("薬きょう")]
    [SerializeField] private ParticleSystem casingParticleSystem;

    private float nextFireTime;
    private float nextEmptySoundTime;
    private bool isReloading;
    private int reloadToken;
    private bool isJammed;
    private bool isClearingJam;
    private int jamClearToken;

    private AmmoItemData loadedAmmoData;
    private AmmoItemData selectedAmmoData;

    public event Action<int> OnMagazineAmmoChanged;
    public event Action<AmmoItemData> OnLoadedAmmoChanged;
    public event Action<AmmoItemData> OnSelectedAmmoChanged;
    public event Action<float, float> OnDurabilityChanged;
    public event Action<bool> OnJamStateChanged;

    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;
    public bool IsEmpty => currentAmmo <= 0;
    public bool IsReloading => isReloading;
    public bool IsGunEquipped => isGunEquipped;
    public float CurrentDurability => Mathf.Max(0f, currentDurability);
    public float MaxDurability => Mathf.Max(1f, maxDurability);
    public float DurabilityPercent => Mathf.Clamp01(CurrentDurability / MaxDurability);
    public bool IsBroken => CurrentDurability <= 0f;
    public bool IsJammed => isJammed;
    public bool IsClearingJam => isClearingJam;

    /// <summary>
    /// 現在装備している武器の射撃方式です。
    /// WeaponItemData未設定時は安全側でSemiAutoとして扱います。
    /// </summary>
    public WeaponFireMode FireMode =>
        weaponItemData != null
            ? weaponItemData.FireMode
            : WeaponFireMode.SemiAuto;

    /// <summary>
    /// 現在装備中のWeaponItemDataから射撃間隔を取得します。
    /// WeaponItemData未設定時のみ互換用として0.15秒を使用します。
    /// </summary>
    public float CurrentFireInterval =>
        weaponItemData != null
            ? weaponItemData.FireInterval
            : 0.15f;

    public float CurrentReloadDuration
    {
        get
        {
            float multiplier = weaponItemData != null
                ? weaponItemData.GetReloadDurationMultiplier(DurabilityPercent)
                : 1f;

            float skillMultiplier =
                SkillCardEffectUtility.GetMultiplier(
                    SkillEffectType.ReloadDuration
                );

            return Mathf.Max(
                0f,
                reloadDuration * multiplier * skillMultiplier
            );
        }
    }

    /// <summary>既存UI互換用。武器データの優先弾薬を返します。</summary>
    public AmmoItemData CompatibleAmmo =>
        weaponItemData != null
            ? weaponItemData.PreferredAmmo
            : null;

    public AmmoItemData LoadedAmmo => loadedAmmoData;
    public AmmoItemData SelectedAmmo => selectedAmmoData;

    public int ReserveAmmoCount
    {
        get
        {
            if (currentAmmo > 0 && loadedAmmoData != null)
            {
                return GetReserveAmount(loadedAmmoData);
            }

            if (selectedAmmoData != null)
            {
                return GetReserveAmount(selectedAmmoData);
            }

            return GetTotalCompatibleReserveAmmo();
        }
    }

    public int TotalCompatibleReserveAmmoCount =>
        GetTotalCompatibleReserveAmmo();

    public int SelectedReserveAmmoCount =>
        selectedAmmoData != null && inventoryController != null
            ? inventoryController.GetTotalAmount(selectedAmmoData)
            : 0;

    public bool HasReserveAmmo => ReserveAmmoCount > 0;

    private bool IsInventoryOpen =>
        inventoryPanel != null && inventoryPanel.activeInHierarchy;

    private bool IsUsingConsumable
    {
        get
        {
            FindPlayerWeightController();

            return playerWeightController != null &&
                   playerWeightController.IsUsingConsumable;
        }
    }

    private void Awake()
    {
        if (gunAudioSource == null)
        {
            gunAudioSource = GetComponent<AudioSource>();
        }

        currentAmmo = fillMagazineOnStart
            ? magazineSize
            : Mathf.Clamp(currentAmmo, 0, magazineSize);
    }

    private void Start()
    {
        EnsureSelectedAmmo();

        if (fillMagazineOnStart && currentAmmo > 0 && loadedAmmoData == null)
        {
            loadedAmmoData = selectedAmmoData ?? CompatibleAmmo;
        }
    }

    private void OnDisable()
    {
        CancelReload();
        CancelJamClear();
    }

    private void Update()
    {
        if (!isGunEquipped)
        {
            return;
        }

        if (IsInventoryOpen || IsUsingConsumable)
        {
            return;
        }

        if (Keyboard.current != null)
        {
            if (Keyboard.current.rKey.wasPressedThisFrame)
            {
                if (isJammed)
                {
                    StartJamClear();
                }
                else
                {
                    StartReload();
                }
            }

            if (enableAmmoSwitchWithBKey &&
                Keyboard.current.bKey.wasPressedThisFrame)
            {
                CycleSelectedAmmoType();
            }
        }

        if (isReloading || isClearingJam || Mouse.current == null)
        {
            return;
        }

        if (ShouldFireFromInput() &&
            Time.time >= nextFireTime)
        {
            Shoot();
        }
    }

    /// <summary>
    /// WeaponItemDataのFire Modeに応じて射撃入力を切り替えます。
    /// SemiAutoは押した瞬間だけ、FullAutoは押している間ずっと有効です。
    /// </summary>
    private bool ShouldFireFromInput()
    {
        if (Mouse.current == null)
        {
            return false;
        }

        switch (FireMode)
        {
            case WeaponFireMode.FullAuto:
                return Mouse.current.leftButton.isPressed;

            case WeaponFireMode.SemiAuto:
            default:
                return Mouse.current.leftButton.wasPressedThisFrame;
        }
    }

    private void Shoot()
    {
        if (!isGunEquipped ||
            isReloading ||
            isClearingJam ||
            IsInventoryOpen ||
            IsUsingConsumable)
        {
            return;
        }

        if (IsBroken)
        {
            PlayBrokenSound();
            return;
        }

        if (isJammed)
        {
            PlayJamSound();
            return;
        }

        if (currentAmmo <= 0)
        {
            PlayEmptySound();
            return;
        }

        if (muzzlePoint == null || bulletPrefab == null)
        {
            Debug.LogWarning(
                "GunShooter：Muzzle Point または Bullet Prefab が設定されていません。"
            );
            return;
        }

        if (loadedAmmoData == null)
        {
            loadedAmmoData = ResolveFallbackLoadedAmmo();
            OnLoadedAmmoChanged?.Invoke(loadedAmmoData);
        }

        nextFireTime = Time.time + CurrentFireInterval;

        if (TryTriggerJam())
        {
            return;
        }

        SetCurrentAmmo(currentAmmo - 1);

        if (gunAudioSource != null && shotSound != null)
        {
            gunAudioSource.PlayOneShot(shotSound, shotVolume);
        }

        if (casingParticleSystem != null)
        {
            casingParticleSystem.Emit(1);
        }

        // SAN値・低耐久による照準ブレは1回の射撃につき1回決める。
        // ショットガンの各Pelletは、この中心方向からさらに散弾角度を加える。
        Vector2 shotCenterDirection = GetShotDirection();

        int pelletCount = weaponItemData != null
            ? weaponItemData.PelletCount
            : 1;

        float pelletSpreadAngle = weaponItemData != null
            ? weaponItemData.PelletSpreadAngle
            : 0f;

        float skillDamageMultiplier =
            SkillCardEffectUtility.GetMultiplier(
                SkillEffectType.WeaponDamage
            );

        for (int pelletIndex = 0;
             pelletIndex < pelletCount;
             pelletIndex++)
        {
            Vector2 pelletDirection = GetPelletDirection(
                shotCenterDirection,
                pelletSpreadAngle
            );

            SpawnBullet(
                pelletDirection,
                skillDamageMultiplier
            );
        }

        // 散弾数に関係なく、1回の射撃につき耐久は1回だけ減らす。
        ApplyDurabilityLossForShot();
    }

    /// <summary>
    /// ショットガン用の散弾方向を作ります。
    /// Spread Angleは散弾全体の角度なので、左右へ半分ずつ広がります。
    /// WeaponSpreadスキルの倍率も散弾の広がりへ反映します。
    /// </summary>
    private Vector2 GetPelletDirection(
        Vector2 centerDirection,
        float spreadAngle)
    {
        float safeSpread = Mathf.Max(0f, spreadAngle);

        if (safeSpread <= 0.0001f)
        {
            return centerDirection.normalized;
        }

        float spreadSkillMultiplier =
            SkillCardEffectUtility.GetMultiplier(
                SkillEffectType.WeaponSpread
            );

        safeSpread *= Mathf.Max(0f, spreadSkillMultiplier);

        float halfSpread = safeSpread * 0.5f;
        float randomAngle = UnityEngine.Random.Range(
            -halfSpread,
            halfSpread
        );

        Vector3 rotatedDirection =
            Quaternion.Euler(0f, 0f, randomAngle) *
            new Vector3(
                centerDirection.x,
                centerDirection.y,
                0f
            );

        return new Vector2(
            rotatedDirection.x,
            rotatedDirection.y
        ).normalized;
    }

    /// <summary>
    /// 1個のBulletを生成し、Ammo・Skill Damage・速度を設定します。
    /// ショットガンではこの処理をPellet Count回呼びます。
    /// </summary>
    private void SpawnBullet(
        Vector2 shotDirection,
        float skillDamageMultiplier)
    {
        float shotAngle = Mathf.Atan2(
            shotDirection.y,
            shotDirection.x
        ) * Mathf.Rad2Deg;

        GameObject bullet = Instantiate(
            bulletPrefab,
            muzzlePoint.position,
            Quaternion.Euler(0f, 0f, shotAngle)
        );

        DamageDealer[] damageDealers =
            bullet.GetComponentsInChildren<DamageDealer>(true);

        foreach (DamageDealer damageDealer in damageDealers)
        {
            if (damageDealer == null)
            {
                continue;
            }

            damageDealer.ConfigureAmmo(loadedAmmoData);
            damageDealer.ConfigureSkillDamageMultiplier(
                skillDamageMultiplier
            );
        }

        Rigidbody2D bulletRb =
            bullet.GetComponent<Rigidbody2D>();

        if (bulletRb != null)
        {
            bulletRb.linearVelocity =
                shotDirection * bulletSpeed;
        }
        else
        {
            Debug.LogWarning(
                "Bullet Prefab に Rigidbody2D が付いていません。"
            );
        }

        Destroy(bullet, bulletLifeTime);
    }

    private Vector2 GetShotDirection()
    {
        Vector2 baseDirection = muzzlePoint.right.normalized;
        float totalAngleOffset = 0f;

        // SAN値によるブレ
        if (useSanityAccuracyPenalty &&
            maxSpreadAngleAtZeroSanity > 0f &&
            FindPlayerSanityController())
        {
            float sanityPercent = playerSanityController.SanityPercent;

            if (sanityPercent < accuracyLossStartSanityPercent)
            {
                float spreadStrength = Mathf.InverseLerp(
                    accuracyLossStartSanityPercent,
                    0f,
                    sanityPercent
                );

                totalAngleOffset += UnityEngine.Random.Range(
                    -maxSpreadAngleAtZeroSanity,
                    maxSpreadAngleAtZeroSanity
                ) * spreadStrength;
            }
        }

        // 武器の低耐久によるブレ。SANのブレと加算されます。
        if (weaponItemData != null)
        {
            float durabilitySpread =
                weaponItemData.GetDurabilitySpreadAngle(
                    DurabilityPercent
                );

            if (durabilitySpread > 0f)
            {
                totalAngleOffset += UnityEngine.Random.Range(
                    -durabilitySpread,
                    durabilitySpread
                );
            }
        }

        totalAngleOffset *=
            SkillCardEffectUtility.GetMultiplier(
                SkillEffectType.WeaponSpread
            );

        if (Mathf.Abs(totalAngleOffset) <= 0.0001f)
        {
            return baseDirection;
        }

        Vector3 rotatedDirection =
            Quaternion.Euler(0f, 0f, totalAngleOffset) *
            new Vector3(baseDirection.x, baseDirection.y, 0f);

        return new Vector2(
            rotatedDirection.x,
            rotatedDirection.y
        ).normalized;
    }

    public void StartReload()
    {
        if (!isGunEquipped ||
            IsInventoryOpen ||
            IsUsingConsumable ||
            isReloading ||
            isClearingJam ||
            isJammed ||
            IsBroken ||
            currentAmmo >= magazineSize)
        {
            return;
        }

        if (!TryGetInventory(out InventoryController inventory))
        {
            Debug.LogWarning(
                "GunShooter：InventoryControllerが設定されていません。",
                this
            );
            PlayEmptySound();
            return;
        }

        AmmoItemData requiredAmmo = ResolveAmmoForReload();

        if (requiredAmmo == null)
        {
            PlayEmptySound();
            return;
        }

        if (inventory.GetTotalAmount(requiredAmmo) <= 0)
        {
            PlayEmptySound();
            return;
        }

        int token = ++reloadToken;
        StartCoroutine(ReloadRoutine(requiredAmmo, inventory, token));
    }

    private IEnumerator ReloadRoutine(
        AmmoItemData requiredAmmo,
        InventoryController inventory,
        int token)
    {
        isReloading = true;

        if (gunAudioSource != null && reloadSound != null)
        {
            gunAudioSource.PlayOneShot(reloadSound, reloadVolume);
        }

        yield return new WaitForSeconds(CurrentReloadDuration);

        if (token != reloadToken || !isGunEquipped)
        {
            yield break;
        }

        int neededAmmo = magazineSize - currentAmmo;

        int loadedAmmo = inventory.RemoveAmountByItemData(
            requiredAmmo,
            neededAmmo
        );

        if (loadedAmmo > 0)
        {
            bool magazineWasEmpty = currentAmmo <= 0;

            if (magazineWasEmpty || loadedAmmoData == null)
            {
                SetLoadedAmmo(requiredAmmo);
            }

            SetCurrentAmmo(currentAmmo + loadedAmmo);
            SetSelectedAmmo(requiredAmmo);
        }

        isReloading = false;
    }

    public void ConfigureAmmoSystem(
        WeaponItemData weaponData,
        InventoryController controller)
    {
        weaponItemData = weaponData;
        inventoryController = controller;

        if (loadedAmmoData != null &&
            !IsCompatibleAmmo(loadedAmmoData))
        {
            loadedAmmoData = null;
        }

        EnsureSelectedAmmo();
    }

    public void ConfigureDurabilitySystem(
        WeaponItemData weaponData,
        float durability,
        bool hasStoredDurability)
    {
        ConfigureDurabilitySystem(
            weaponData,
            durability,
            hasStoredDurability,
            false
        );
    }

    public void ConfigureDurabilitySystem(
        WeaponItemData weaponData,
        float durability,
        bool hasStoredDurability,
        bool storedJammed)
    {
        if (weaponData == null)
        {
            maxDurability = 100f;
            durabilityLossPerShot = 0f;
            currentDurability = maxDurability;
            SetJammedState(false);
            OnDurabilityChanged?.Invoke(currentDurability, maxDurability);
            return;
        }

        weaponItemData = weaponData;
        maxDurability = weaponData.MaxDurability;
        durabilityLossPerShot = weaponData.DurabilityLossPerShot;

        currentDurability = hasStoredDurability
            ? Mathf.Clamp(durability, 0f, maxDurability)
            : maxDurability;

        SetJammedState(storedJammed && currentDurability > 0f);
        OnDurabilityChanged?.Invoke(currentDurability, maxDurability);
    }

    public void SetJammedState(bool jammed)
    {
        bool finalJammed = jammed && !IsBroken;

        if (isJammed == finalJammed)
        {
            return;
        }

        isJammed = finalJammed;
        OnJamStateChanged?.Invoke(isJammed);
    }

    public void SetCurrentDurability(float durability)
    {
        float clamped = Mathf.Clamp(
            durability,
            0f,
            Mathf.Max(1f, maxDurability)
        );

        if (Mathf.Approximately(currentDurability, clamped))
        {
            return;
        }

        currentDurability = clamped;

        if (IsBroken && isJammed)
        {
            SetJammedState(false);
        }

        OnDurabilityChanged?.Invoke(currentDurability, maxDurability);
    }

    private void ApplyDurabilityLossForShot()
    {
        if (durabilityLossPerShot <= 0f || IsBroken)
        {
            return;
        }

        float skillMultiplier =
            SkillCardEffectUtility.GetMultiplier(
                SkillEffectType.WeaponDurabilityLoss
            );

        SetCurrentDurability(
            currentDurability -
            (durabilityLossPerShot * skillMultiplier)
        );
    }

    private bool TryTriggerJam()
    {
        if (weaponItemData == null || IsBroken || isJammed)
        {
            return false;
        }

        float jamChance = weaponItemData.GetJamChance(
            DurabilityPercent
        ) * SkillCardEffectUtility.GetMultiplier(
            SkillEffectType.JamChance
        );

        jamChance = Mathf.Clamp01(jamChance);

        if (jamChance <= 0f || UnityEngine.Random.value >= jamChance)
        {
            return false;
        }

        SetJammedState(true);
        PlayJamSound();
        return true;
    }

    public void StartJamClear()
    {
        if (!isGunEquipped ||
            !isJammed ||
            isClearingJam ||
            isReloading ||
            IsInventoryOpen ||
            IsUsingConsumable)
        {
            return;
        }

        int token = ++jamClearToken;
        StartCoroutine(ClearJamRoutine(token));
    }

    private IEnumerator ClearJamRoutine(int token)
    {
        isClearingJam = true;

        if (gunAudioSource != null && jamClearSound != null)
        {
            gunAudioSource.PlayOneShot(
                jamClearSound,
                Mathf.Clamp01(jamClearVolume)
            );
        }

        float duration = weaponItemData != null
            ? weaponItemData.JamClearDuration
            : 1.25f;

        yield return new WaitForSeconds(Mathf.Max(0f, duration));

        if (token != jamClearToken || !isGunEquipped)
        {
            yield break;
        }

        isClearingJam = false;
        SetJammedState(false);
    }

    public bool SelectAmmoType(AmmoItemData ammoData)
    {
        if (ammoData == null || !IsCompatibleAmmo(ammoData))
        {
            return false;
        }

        SetSelectedAmmo(ammoData);
        return true;
    }

    /// <summary>
    /// BキーまたはUIから呼び、所持中の互換弾薬を順番に選択します。
    /// マガジンに弾が残っている場合、そのマガジンの中身は変わらず、
    /// 空になった後の次回リロードから選択弾が使われます。
    /// </summary>
    public void CycleSelectedAmmoType()
    {
        if (!TryGetInventory(out _))
        {
            return;
        }

        List<AmmoItemData> available = GetAvailableCompatibleAmmoTypes();

        if (available.Count <= 0)
        {
            SetSelectedAmmo(null);
            return;
        }

        int currentIndex = available.IndexOf(selectedAmmoData);
        int nextIndex = currentIndex < 0
            ? 0
            : (currentIndex + 1) % available.Count;

        SetSelectedAmmo(available[nextIndex]);

        Debug.Log(
            $"[GunShooter] 次回装填弾を {selectedAmmoData.DisplayName} " +
            $"({selectedAmmoData.CaliberId}/{selectedAmmoData.Variant}) に切り替えました。",
            this
        );
    }

    public void SetGunEquipped(bool equipped)
    {
        isGunEquipped = equipped;

        if (!equipped)
        {
            CancelReload();
            CancelJamClear();
        }
    }

    public void SetInventoryPanel(GameObject panel)
    {
        inventoryPanel = panel;
    }

    public void SetCurrentAmmo(int ammo)
    {
        int clampedAmmo = Mathf.Clamp(ammo, 0, magazineSize);

        if (currentAmmo == clampedAmmo)
        {
            return;
        }

        currentAmmo = clampedAmmo;
        OnMagazineAmmoChanged?.Invoke(currentAmmo);
    }

    /// <summary>
    /// 残弾数と装填中の弾種を同時に復元します。
    /// 古いセーブで弾種が無い場合はPreferred Ammoを補完に使用します。
    /// </summary>
    public void SetMagazineState(
        int ammo,
        AmmoItemData ammoType)
    {
        AmmoItemData safeAmmoType = ammoType;

        if (safeAmmoType != null && !IsCompatibleAmmo(safeAmmoType))
        {
            safeAmmoType = null;
        }

        if (safeAmmoType == null && ammo > 0)
        {
            safeAmmoType = ResolveFallbackLoadedAmmo();
        }

        SetLoadedAmmo(safeAmmoType);
        SetCurrentAmmo(ammo);

        if (selectedAmmoData == null)
        {
            SetSelectedAmmo(safeAmmoType);
        }
    }

    private AmmoItemData ResolveAmmoForReload()
    {
        if (currentAmmo > 0)
        {
            // マガジン途中では異なる弾種を混ぜない。
            AmmoItemData existingType = loadedAmmoData;

            if (existingType == null)
            {
                existingType = ResolveFallbackLoadedAmmo();
            }

            return existingType != null && IsCompatibleAmmo(existingType)
                ? existingType
                : null;
        }

        EnsureSelectedAmmo();

        if (selectedAmmoData != null &&
            IsCompatibleAmmo(selectedAmmoData) &&
            GetReserveAmount(selectedAmmoData) > 0)
        {
            return selectedAmmoData;
        }

        return FindFirstAvailableCompatibleAmmo();
    }

    private AmmoItemData ResolveFallbackLoadedAmmo()
    {
        if (selectedAmmoData != null && IsCompatibleAmmo(selectedAmmoData))
        {
            return selectedAmmoData;
        }

        AmmoItemData preferred = CompatibleAmmo;

        return preferred != null && IsCompatibleAmmo(preferred)
            ? preferred
            : null;
    }

    private void EnsureSelectedAmmo()
    {
        if (selectedAmmoData != null &&
            IsCompatibleAmmo(selectedAmmoData) &&
            GetReserveAmount(selectedAmmoData) > 0)
        {
            return;
        }

        AmmoItemData preferred = CompatibleAmmo;

        if (preferred != null &&
            IsCompatibleAmmo(preferred) &&
            GetReserveAmount(preferred) > 0)
        {
            SetSelectedAmmo(preferred);
            return;
        }

        SetSelectedAmmo(FindFirstAvailableCompatibleAmmo());
    }

    private AmmoItemData FindFirstAvailableCompatibleAmmo()
    {
        List<AmmoItemData> available = GetAvailableCompatibleAmmoTypes();
        return available.Count > 0 ? available[0] : null;
    }

    private List<AmmoItemData> GetAvailableCompatibleAmmoTypes()
    {
        List<AmmoItemData> result = new List<AmmoItemData>();

        if (!TryGetInventory(out InventoryController inventory) ||
            inventory.Grid == null)
        {
            return result;
        }

        foreach (InventoryItem item in inventory.Grid.Items)
        {
            AmmoItemData ammo = item?.ItemData as AmmoItemData;

            if (ammo == null ||
                item.Amount <= 0 ||
                !IsCompatibleAmmo(ammo) ||
                result.Contains(ammo))
            {
                continue;
            }

            result.Add(ammo);
        }

        return result;
    }

    private int GetTotalCompatibleReserveAmmo()
    {
        if (!TryGetInventory(out InventoryController inventory) ||
            inventory.Grid == null)
        {
            return 0;
        }

        int total = 0;

        foreach (InventoryItem item in inventory.Grid.Items)
        {
            AmmoItemData ammo = item?.ItemData as AmmoItemData;

            if (ammo != null && IsCompatibleAmmo(ammo))
            {
                total += Mathf.Max(0, item.Amount);
            }
        }

        return total;
    }

    private int GetReserveAmount(AmmoItemData ammoData)
    {
        if (ammoData == null ||
            !TryGetInventory(out InventoryController inventory))
        {
            return 0;
        }

        return inventory.GetTotalAmount(ammoData);
    }

    private bool IsCompatibleAmmo(AmmoItemData ammoData)
    {
        return weaponItemData != null &&
               weaponItemData.IsAmmoCompatible(ammoData);
    }

    private void SetLoadedAmmo(AmmoItemData ammoData)
    {
        if (loadedAmmoData == ammoData)
        {
            return;
        }

        loadedAmmoData = ammoData;
        OnLoadedAmmoChanged?.Invoke(loadedAmmoData);
    }

    private void SetSelectedAmmo(AmmoItemData ammoData)
    {
        if (selectedAmmoData == ammoData)
        {
            return;
        }

        selectedAmmoData = ammoData;
        OnSelectedAmmoChanged?.Invoke(selectedAmmoData);
    }

    private bool TryGetInventory(out InventoryController inventory)
    {
        inventory = inventoryController;

        if (inventory == null)
        {
            inventory = FindAnyObjectByType<InventoryController>();
            inventoryController = inventory;
        }

        return inventory != null;
    }

    private bool FindPlayerWeightController()
    {
        if (playerWeightController != null)
        {
            return true;
        }

        playerWeightController =
            GetComponentInParent<PlayerWeightController>();

        if (playerWeightController != null)
        {
            return true;
        }

        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerWeightController =
                player.GetComponent<PlayerWeightController>();
        }

        if (playerWeightController == null)
        {
            playerWeightController =
                FindAnyObjectByType<PlayerWeightController>();
        }

        return playerWeightController != null;
    }

    private bool FindPlayerSanityController()
    {
        if (playerSanityController != null)
        {
            return true;
        }

        playerSanityController =
            GetComponentInParent<PlayerSanityController>();

        if (playerSanityController != null)
        {
            return true;
        }

        GameObject player =
            GameObject.FindGameObjectWithTag("Player");

        if (player != null)
        {
            playerSanityController =
                player.GetComponent<PlayerSanityController>();
        }

        if (playerSanityController == null)
        {
            playerSanityController =
                FindAnyObjectByType<PlayerSanityController>();
        }

        return playerSanityController != null;
    }

    private void CancelReload()
    {
        reloadToken++;
        isReloading = false;
    }

    private void CancelJamClear()
    {
        jamClearToken++;
        isClearingJam = false;
    }

    private void PlayJamSound()
    {
        if (Time.time < nextEmptySoundTime)
        {
            return;
        }

        nextEmptySoundTime = Time.time + emptySoundInterval;

        AudioClip clip = jamSound != null
            ? jamSound
            : emptySound;

        if (gunAudioSource != null && clip != null)
        {
            gunAudioSource.PlayOneShot(
                clip,
                Mathf.Clamp01(jamVolume)
            );
        }
    }

    private void PlayBrokenSound()
    {
        if (Time.time < nextEmptySoundTime)
        {
            return;
        }

        nextEmptySoundTime = Time.time + emptySoundInterval;

        AudioClip clip = brokenSound != null
            ? brokenSound
            : emptySound;

        if (gunAudioSource != null && clip != null)
        {
            gunAudioSource.PlayOneShot(
                clip,
                Mathf.Clamp01(brokenVolume)
            );
        }
    }

    private void PlayEmptySound()
    {
        if (Time.time < nextEmptySoundTime)
        {
            return;
        }

        nextEmptySoundTime = Time.time + emptySoundInterval;

        if (gunAudioSource != null && emptySound != null)
        {
            gunAudioSource.PlayOneShot(emptySound, emptyVolume);
        }
    }

    private void OnValidate()
    {
        magazineSize = Mathf.Max(1, magazineSize);
        currentAmmo = Mathf.Clamp(currentAmmo, 0, magazineSize);
        reloadDuration = Mathf.Max(0f, reloadDuration);
        emptySoundInterval = Mathf.Max(0f, emptySoundInterval);

        accuracyLossStartSanityPercent =
            Mathf.Clamp01(accuracyLossStartSanityPercent);

        maxSpreadAngleAtZeroSanity =
            Mathf.Max(0f, maxSpreadAngleAtZeroSanity);

        maxDurability = Mathf.Max(1f, maxDurability);
        currentDurability = Mathf.Clamp(
            currentDurability,
            0f,
            maxDurability
        );
        durabilityLossPerShot = Mathf.Max(0f, durabilityLossPerShot);
        jamVolume = Mathf.Clamp01(jamVolume);
        jamClearVolume = Mathf.Clamp01(jamClearVolume);
        brokenVolume = Mathf.Clamp01(brokenVolume);
    }
}
