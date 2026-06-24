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

    [Header("射撃設定")]
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float fireInterval = 0.15f;
    [SerializeField] private float bulletLifeTime = 3f;

    [Header("装弾数")]
    [Tooltip("この銃に入れられる最大弾数")]
    [SerializeField, Min(1)] private int magazineSize = 10;

    [Tooltip("現在マガジンに入っている弾数")]
    [SerializeField, Min(0)] private int currentAmmo = 10;

    [Tooltip("ゲーム開始時にマガジンを満タンにする")]
    [SerializeField] private bool fillMagazineOnStart = true;

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

    private float nextFireTime;
    private float nextEmptySoundTime;
    private bool isReloading;

    public int CurrentAmmo => currentAmmo;
    public int MagazineSize => magazineSize;
    public bool IsEmpty => currentAmmo <= 0;
    public bool IsReloading => isReloading;
    public bool IsGunEquipped => isGunEquipped;

    private bool IsInventoryOpen =>
        inventoryPanel != null && inventoryPanel.activeInHierarchy;

    private void Awake()
    {
        if (gunAudioSource == null)
        {
            gunAudioSource = GetComponent<AudioSource>();
        }

        if (fillMagazineOnStart)
        {
            currentAmmo = magazineSize;
        }
        else
        {
            currentAmmo = Mathf.Clamp(currentAmmo, 0, magazineSize);
        }
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

        // Rキーでリロード開始
        if (Keyboard.current != null &&
            Keyboard.current.rKey.wasPressedThisFrame)
        {
            StartReload();
        }

        // リロード中は発射不可
        if (isReloading)
        {
            return;
        }

        if (Mouse.current == null)
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
        if (isReloading || IsInventoryOpen)
        {
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

        nextFireTime = Time.time + fireInterval;
        currentAmmo--;

        if (gunAudioSource != null && shotSound != null)
        {
            gunAudioSource.PlayOneShot(shotSound, shotVolume);
        }

        GameObject bullet = Instantiate(
            bulletPrefab,
            muzzlePoint.position,
            muzzlePoint.rotation
        );

        Rigidbody2D bulletRb = bullet.GetComponent<Rigidbody2D>();

        if (bulletRb != null)
        {
            bulletRb.linearVelocity = muzzlePoint.right * bulletSpeed;
        }
        else
        {
            Debug.LogWarning("Bullet Prefab に Rigidbody2D が付いていません。");
        }

        Destroy(bullet, bulletLifeTime);
    }

    public void StartReload()
    {
        if (IsInventoryOpen)
        {
            return;
        }

        if (isReloading || currentAmmo >= magazineSize)
        {
            return;
        }

        StartCoroutine(ReloadRoutine());
    }

    private IEnumerator ReloadRoutine()
    {
        isReloading = true;

        if (gunAudioSource != null && reloadSound != null)
        {
            gunAudioSource.PlayOneShot(reloadSound, reloadVolume);
        }

        yield return new WaitForSeconds(reloadDuration);

        currentAmmo = magazineSize;
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

    public void SetGunEquipped(bool equipped)
    {
        isGunEquipped = equipped;
    }
}