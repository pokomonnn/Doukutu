using System;
using System.Collections;
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

    [Header("射撃設定")]
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float fireInterval = 0.15f;
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

    [Header("状態")]
    [SerializeField] private bool isGunEquipped = true;

    [Header("薬きょう")]
    [SerializeField] private ParticleSystem casingParticleSystem;

    private float nextFireTime;
    private float nextEmptySoundTime;
    private bool isReloading;
    private int reloadToken;

    // 残弾が変化した瞬間に、装備しているInventoryItemへ保存するためのイベント
    public event Action<int> OnMagazineAmmoChanged;

    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;
    public bool IsEmpty => currentAmmo <= 0;
    public bool IsReloading => isReloading;
    public bool IsGunEquipped => isGunEquipped;

    public AmmoItemData CompatibleAmmo =>
        weaponItemData != null
            ? weaponItemData.CompatibleAmmo
            : null;

    public int ReserveAmmoCount
    {
        get
        {
            if (inventoryController == null ||
                CompatibleAmmo == null)
            {
                return 0;
            }

            return inventoryController.GetTotalAmount(CompatibleAmmo);
        }
    }

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

    private void OnDisable()
    {
        CancelReload();
    }

    private void Update()
    {
        if (!isGunEquipped)
        {
            return;
        }

        // インベントリ表示中は射撃・リロード開始をさせない
        if (IsInventoryOpen)
        {
            return;
        }

        if (IsInventoryOpen || IsUsingConsumable)
        {
            return;
        }

        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartReload();
        }

        if (isReloading || Mouse.current == null)
        {
            return;
        }

        // 回復中は射撃禁止
        if (IsUsingConsumable)
        {
            return;
        }

        if (Mouse.current.leftButton.isPressed &&
            Time.time >= nextFireTime)
        {
            Shoot();
        }
    }

    private void Shoot()
    {
        if (!isGunEquipped ||
            isReloading ||
            IsInventoryOpen ||
            IsUsingConsumable)
        {
            return;
        }

        // マガジンが空なら、予備弾が残っていても発射しない。
        // Rでリロードして初めて撃てるようになる。
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

        nextFireTime = Time.time + fireInterval;
        SetCurrentAmmo(currentAmmo - 1);

        if (gunAudioSource != null && shotSound != null)
        {
            gunAudioSource.PlayOneShot(shotSound, shotVolume);
        }

        if (casingParticleSystem != null)
        {
            casingParticleSystem.Emit(1);
        }

        // SAN値が低いほど、Muzzle Pointの正面方向からランダムにブレます。
        Vector2 shotDirection = GetShotDirection();
        float shotAngle = Mathf.Atan2(
            shotDirection.y,
            shotDirection.x
        ) * Mathf.Rad2Deg;

        GameObject bullet = Instantiate(
            bulletPrefab,
            muzzlePoint.position,
            Quaternion.Euler(0f, 0f, shotAngle)
        );

        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();

        if (bulletRb != null)
        {
            bulletRb.linearVelocity = shotDirection * bulletSpeed;
        }
        else
        {
            Debug.LogWarning("Bullet Prefab に Rigidbody2D が付いていません。");
        }

        Destroy(bullet, bulletLifeTime);
    }

    private Vector2 GetShotDirection()
    {
        Vector2 baseDirection = muzzlePoint.right.normalized;

        if (!useSanityAccuracyPenalty ||
            maxSpreadAngleAtZeroSanity <= 0f ||
            !FindPlayerSanityController())
        {
            return baseDirection;
        }

        float sanityPercent = playerSanityController.SanityPercent;

        if (sanityPercent >= accuracyLossStartSanityPercent)
        {
            return baseDirection;
        }

        // 例：開始値70%、SAN 35%ならブレは最大値の半分。
        float spreadStrength = Mathf.InverseLerp(
            accuracyLossStartSanityPercent,
            0f,
            sanityPercent
        );

        float angleOffset = UnityEngine.Random.Range(
            -maxSpreadAngleAtZeroSanity,
            maxSpreadAngleAtZeroSanity
        ) * spreadStrength;

        Vector3 rotatedDirection =
            Quaternion.Euler(0f, 0f, angleOffset) *
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
            currentAmmo >= magazineSize)
        {
            return;
        }

        if (!TryGetAmmoContext(
                out AmmoItemData requiredAmmo,
                out InventoryController inventory))
        {
            Debug.LogWarning(
                "GunShooter：WeaponItemDataのCompatible Ammo、またはInventoryControllerが設定されていません。",
                this
            );

            PlayEmptySound();
            return;
        }

        // 予備弾が0ならリロードを開始しない
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

        yield return new WaitForSeconds(reloadDuration);

        // 死亡・武器を外すなどでリロードが中断された時は消費しない
        if (token != reloadToken || !isGunEquipped)
        {
            yield break;
        }

        int neededAmmo = magazineSize - currentAmmo;

        // ここで必要な分だけ、複数スタックもまたいで消費する。
        // 13発しかなければ13発だけ装填される。
        int loadedAmmo = inventory.RemoveAmountByItemData(
            requiredAmmo,
            neededAmmo
        );

        if (loadedAmmo > 0)
        {
            SetCurrentAmmo(currentAmmo + loadedAmmo);
        }

        isReloading = false;
    }

    public void ConfigureAmmoSystem(
        WeaponItemData weaponData,
        InventoryController controller)
    {
        weaponItemData = weaponData;
        inventoryController = controller;
    }

    public void SetGunEquipped(bool equipped)
    {
        isGunEquipped = equipped;

        if (!equipped)
        {
            CancelReload();
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

    private bool TryGetAmmoContext(
        out AmmoItemData requiredAmmo,
        out InventoryController inventory)
    {
        requiredAmmo = CompatibleAmmo;
        inventory = inventoryController;

        if (inventory == null)
        {
            inventory = FindAnyObjectByType<InventoryController>();
            inventoryController = inventory;
        }

        return requiredAmmo != null && inventory != null;
    }

    private void CancelReload()
    {
        reloadToken++;
        isReloading = false;
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
    }
}
