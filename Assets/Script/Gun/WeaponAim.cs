using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAim : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer gunSpriteRenderer;

    [Header("UI参照")]
    [Tooltip("Tabで表示・非表示にしているインベントリの親Panelを設定")]
    [SerializeField] private GameObject inventoryPanel;

    [Header("設定")]
    [SerializeField] private bool isGunEquipped = true;

    [Tooltip("銃画像が最初から右向きなら 0 のままでOK")]
    [SerializeField] private float rotationOffset = 0f;

    private Vector3 gunBaseLocalScale;

    private bool IsInventoryOpen =>
        inventoryPanel != null && inventoryPanel.activeInHierarchy;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (gunSpriteRenderer != null)
        {
            gunBaseLocalScale = gunSpriteRenderer.transform.localScale;
            gunSpriteRenderer.flipY = false;
        }
    }

    private void Update()
    {
        if (!isGunEquipped)
        {
            return;
        }

        // インベントリを開いている間は、
        // 銃の回転・左右反転を現在の状態で固定する
        if (IsInventoryOpen)
        {
            return;
        }

        if (targetCamera == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseScreenPosition = Mouse.current.position.ReadValue();

        float distanceToWeapon =
            Mathf.Abs(targetCamera.transform.position.z - transform.position.z);

        Vector3 mouseWorldPosition = targetCamera.ScreenToWorldPoint(
            new Vector3(
                mouseScreenPosition.x,
                mouseScreenPosition.y,
                distanceToWeapon
            )
        );

        Vector2 aimDirection =
            (Vector2)mouseWorldPosition - (Vector2)transform.position;

        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle =
            Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;

        transform.rotation =
            Quaternion.Euler(0f, 0f, angle + rotationOffset);

        if (gunSpriteRenderer != null)
        {
            bool isAimingLeft = aimDirection.x < 0f;

            gunSpriteRenderer.transform.localScale = new Vector3(
                gunBaseLocalScale.x,
                Mathf.Abs(gunBaseLocalScale.y) *
                    (isAimingLeft ? -1f : 1f),
                gunBaseLocalScale.z
            );
        }
    }

    public void SetGunEquipped(bool equipped)
    {
        isGunEquipped = equipped;
    }

    public void SetInventoryPanel(GameObject panel)
    {
        inventoryPanel = panel;
    }
}