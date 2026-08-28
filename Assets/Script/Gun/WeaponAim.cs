using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class WeaponAim : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private SpriteRenderer gunSpriteRenderer;

    [Tooltip(
        "Player本体のSpriteRenderer。" +
        "銃をPlayerの前/後ろへ切り替える基準に使います。" +
        "未設定ならPlayer階層から自動検索します。"
    )]
    [SerializeField] private SpriteRenderer playerSpriteRenderer;

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

    [Header("左右切り替え")]
    [Tooltip(
        "Player中央付近で左右WeaponHolderが細かく切り替わるのを防ぐ範囲です。" +
        "マウスのX差がこの値以内なら、直前の左右状態を維持します。"
    )]
    [SerializeField, Min(0f)]
    private float aimSideSwitchDeadZone = 0.50f;

    [Header("銃の描画順")]
    [Tooltip(
        "オンの場合、PlayerのSorting LayerとSorting Orderを基準にして、" +
        "右向きは前、左向きは後ろへ自動切り替えします。"
    )]
    [SerializeField] private bool changeSortingOrderByAimDirection = true;

    [Tooltip(
        "右側へ構えた時、PlayerのSorting Orderへ加算する値。" +
        "1ならPlayerより1つ前に表示されます。"
    )]
    [SerializeField] private int rightAimSortingOrderOffset = 1;

    [Tooltip(
        "左側へ構えた時、PlayerのSorting Orderへ加算する値。" +
        "-1ならPlayerより1つ後ろに表示されます。"
    )]
    [SerializeField] private int leftAimSortingOrderOffset = -1;

    [Tooltip(
        "オンの場合、銃のSorting LayerもPlayerと同じSorting Layerへ合わせます。"
    )]
    [SerializeField] private bool syncSortingLayerWithPlayer = true;

    private Vector3 gunBaseLocalScale;

    private bool currentIsAimingLeft;
    private bool hasAimDirectionState;

    /// <summary>
    /// 照準がPlayerの左右をまたいだ時だけ通知します。
    /// true = 左向き / false = 右向き
    /// </summary>
    public event Action<bool> AimDirectionChanged;

    public bool IsAimingLeft => currentIsAimingLeft;

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

        // 初期状態は右向きとして描画順を設定。
        ApplyWeaponSorting(false);
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

        bool isAimingLeft =
            ResolveAimingLeft(aimDirection.x);

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

        ApplyWeaponSorting(isAimingLeft);
        UpdateAimDirectionState(isAimingLeft);
    }

    /// <summary>
    /// Player中央付近では直前の左右状態を維持し、
    /// マウスがDead Zoneを十分に抜けた時だけ左右を切り替えます。
    ///
    /// 右向き中：
    /// Xが -DeadZone より左へ行くまで右を維持。
    ///
    /// 左向き中：
    /// Xが +DeadZone より右へ行くまで左を維持。
    ///
    /// これにより中央付近でのHolder高速切り替えを防ぎます。
    /// </summary>
    private bool ResolveAimingLeft(float horizontalAimDelta)
    {
        float deadZone =
            Mathf.Max(0f, aimSideSwitchDeadZone);

        if (!hasAimDirectionState)
        {
            if (horizontalAimDelta < -deadZone)
            {
                return true;
            }

            if (horizontalAimDelta > deadZone)
            {
                return false;
            }

            // 初期状態で中央付近にいる場合は右向きから開始。
            return false;
        }

        if (currentIsAimingLeft)
        {
            // 左向き中は、右側Dead Zoneを抜けるまで左を維持。
            if (horizontalAimDelta > deadZone)
            {
                return false;
            }

            return true;
        }

        // 右向き中は、左側Dead Zoneを抜けるまで右を維持。
        if (horizontalAimDelta < -deadZone)
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// 左右が実際に切り替わった時だけイベントを発行します。
    /// 毎フレームHolderを付け替えないための通知です。
    /// </summary>
    private void UpdateAimDirectionState(bool isAimingLeft)
    {
        if (hasAimDirectionState &&
            currentIsAimingLeft == isAimingLeft)
        {
            return;
        }

        currentIsAimingLeft = isAimingLeft;
        hasAimDirectionState = true;

        AimDirectionChanged?.Invoke(currentIsAimingLeft);
    }

    /// <summary>
    /// マウス/照準方向に応じて銃の描画順を切り替えます。
    ///
    /// 右向き：
    /// Playerより前へ
    ///
    /// 左向き：
    /// Playerより後ろへ
    /// </summary>
    private void ApplyWeaponSorting(bool isAimingLeft)
    {
        if (!changeSortingOrderByAimDirection ||
            gunSpriteRenderer == null)
        {
            return;
        }

        FindReferences();

        if (playerSpriteRenderer == null)
        {
            return;
        }

        if (syncSortingLayerWithPlayer)
        {
            gunSpriteRenderer.sortingLayerID =
                playerSpriteRenderer.sortingLayerID;
        }

        int offset = isAimingLeft
            ? leftAimSortingOrderOffset
            : rightAimSortingOrderOffset;

        gunSpriteRenderer.sortingOrder =
            playerSpriteRenderer.sortingOrder + offset;
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
    /// Player本体のSpriteRendererを外部から明示的に設定できます。
    /// 自動検索で違うSpriteRendererを拾う場合に使用します。
    /// </summary>
    public void SetPlayerSpriteRenderer(
        SpriteRenderer renderer)
    {
        playerSpriteRenderer = renderer;
    }

    /// <summary>
    /// 現在の左右状態を購読側へ即時通知します。
    /// Weapon生成直後のHolder同期に使用できます。
    /// </summary>
    public void NotifyCurrentAimDirection()
    {
        AimDirectionChanged?.Invoke(currentIsAimingLeft);
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

    private void OnValidate()
    {
        aimSideSwitchDeadZone =
            Mathf.Max(0f, aimSideSwitchDeadZone);
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

        if (playerSpriteRenderer == null &&
            playerMove != null)
        {
            SpriteRenderer[] playerRenderers =
                playerMove.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer renderer in playerRenderers)
            {
                if (renderer == null ||
                    renderer == gunSpriteRenderer ||
                    renderer.transform.IsChildOf(transform))
                {
                    continue;
                }

                playerSpriteRenderer = renderer;
                break;
            }
        }

        if (proneController == null)
        {
            proneController =
                GetComponentInParent<PlayerProneController>();
        }
    }
}
