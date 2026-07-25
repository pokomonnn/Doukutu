using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAim : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer gunSpriteRenderer;

    [Tooltip("ほふく中の向き・照準角度を取得します。通常はPlayerの親階層から自動取得します")]
    [SerializeField] private PlayerProneController proneController;

    [Tooltip("通常はPlayerの親階層から自動取得します")]
    [SerializeField] private PlayerMove playerMove;

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
        FindReferences();

        if (gunSpriteRenderer != null)
        {
            gunBaseLocalScale =
                gunSpriteRenderer.transform.localScale;

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
        // 銃の回転・左右反転を現在の状態で固定する。
        if (IsInventoryOpen)
        {
            return;
        }

        FindReferences();

        if (targetCamera == null || Mouse.current == null)
        {
            return;
        }

        Vector2 mouseScreenPosition =
            Mouse.current.position.ReadValue();

        float distanceToWeapon = Mathf.Abs(
            targetCamera.transform.position.z -
            transform.position.z
        );

        Vector3 mouseWorldPosition =
            targetCamera.ScreenToWorldPoint(
                new Vector3(
                    mouseScreenPosition.x,
                    mouseScreenPosition.y,
                    distanceToWeapon
                )
            );

        Vector2 aimDirection =
            (Vector2)mouseWorldPosition -
            (Vector2)transform.position;

        if (aimDirection.sqrMagnitude <= 0.0001f)
        {
            return;
        }

        float angle =
            Mathf.Atan2(aimDirection.y, aimDirection.x) *
            Mathf.Rad2Deg;

        bool isAimingLeft = aimDirection.x < 0f;

        if (proneController != null &&
            proneController.IsProne)
        {
            bool facingRight =
                proneController.LockedFacingRight;

            float facingAngle = facingRight ? 0f : 180f;
            float halfAimAngle =
                proneController.ProneAimAngle * 0.5f;

            float angleFromFacing = Mathf.DeltaAngle(
                facingAngle,
                angle
            );

            angleFromFacing = Mathf.Clamp(
                angleFromFacing,
                -halfAimAngle,
                halfAimAngle
            );

            angle = facingAngle + angleFromFacing;

            // ほふく中の銃反転はマウス位置ではなく、
            // ほふく開始時に固定したPlayerの向きを使う。
            isAimingLeft = !facingRight;
        }

        transform.rotation = Quaternion.Euler(
            0f,
            0f,
            angle + rotationOffset
        );

        if (gunSpriteRenderer != null)
        {
            gunSpriteRenderer.transform.localScale =
                new Vector3(
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

    /// <summary>
    /// 武器生成側からPlayer参照を明示的に渡したい場合に使えます。
    /// 通常は親階層から自動取得されるため、呼ばなくても動作します。
    /// </summary>
    public void SetPlayerContext(
        PlayerMove move,
        PlayerProneController prone)
    {
        playerMove = move;
        proneController = prone;
    }

    private void FindReferences()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (playerMove == null)
        {
            playerMove = GetComponentInParent<PlayerMove>();
        }

        if (proneController == null)
        {
            proneController =
                GetComponentInParent<PlayerProneController>();
        }
    }
}
