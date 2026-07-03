using UnityEngine;

/// <summary>
/// プレイヤーの頭上へ表示する、現在のミッション目的地を指す矢印コンパスです。
/// Spriteが右向きで描かれている場合はRotation Offsetを0、
/// 上向きなら-90にします。
/// </summary>
[DisallowMultipleComponent]
public class MissionCompass2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定ならシーン内から自動取得します")]
    [SerializeField] private MissionManager2D missionManager;

    [Tooltip("未設定ならPlayerMoveを自動取得します")]
    [SerializeField] private Transform playerTransform;

    [Tooltip("実際に回転・表示するTransform。空欄ならこのObjectを使います")]
    [SerializeField] private Transform compassVisual;

    [Tooltip("矢印のSpriteRenderer。空欄ならCompass Visual自身か子から取得します")]
    [SerializeField] private SpriteRenderer compassRenderer;

    [Header("表示位置")]
    [Tooltip("プレイヤー頭上へのワールド座標オフセット")]
    [SerializeField]
    private Vector3 abovePlayerOffset =
        new Vector3(0f, 1.6f, 0f);

    [Header("方向")]
    [Tooltip("オンなら上下差を無視し、右・左の方向だけを示します")]
    [SerializeField] private bool horizontalOnly;

    [Tooltip("矢印Spriteが右向きなら0、上向きなら-90、左向きなら180にします")]
    [SerializeField] private float spriteRotationOffset;

    [Tooltip("矢印が目標方向へ回る速さ。0なら即座に向きを変えます")]
    [SerializeField, Min(0f)] private float rotationSpeed = 720f;

    [Header("表示設定")]
    [SerializeField] private bool hideWhenNoActiveMission = true;

    [Tooltip("目的地とほぼ同じ位置にいる時、矢印を非表示にします")]
    [SerializeField, Min(0f)] private float hideAtTargetDistance = 0.05f;

    private bool hasAppliedVisibility;
    private bool lastVisible;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        UpdateCompass(true);
    }

    private void LateUpdate()
    {
        FindReferences();
        UpdateCompass(false);
    }

    private void UpdateCompass(bool snapRotation)
    {
        if (playerTransform == null || compassVisual == null)
        {
            SetCompassVisible(false);
            return;
        }

        Transform target = missionManager != null
            ? missionManager.ActiveCompassTarget
            : null;

        if (target == null)
        {
            SetCompassVisible(!hideWhenNoActiveMission);
            return;
        }

        Vector3 position = playerTransform.position +
            abovePlayerOffset;

        compassVisual.position = position;

        Vector2 direction = target.position - position;

        if (horizontalOnly)
        {
            direction.y = 0f;
        }

        if (direction.sqrMagnitude <=
            hideAtTargetDistance * hideAtTargetDistance)
        {
            SetCompassVisible(false);
            return;
        }

        SetCompassVisible(true);

        float angle = Mathf.Atan2(
            direction.y,
            direction.x
        ) * Mathf.Rad2Deg + spriteRotationOffset;

        Quaternion targetRotation = Quaternion.Euler(
            0f,
            0f,
            angle
        );

        if (snapRotation || rotationSpeed <= 0f)
        {
            compassVisual.rotation = targetRotation;
            return;
        }

        compassVisual.rotation = Quaternion.RotateTowards(
            compassVisual.rotation,
            targetRotation,
            rotationSpeed * Time.deltaTime
        );
    }

    private void SetCompassVisible(bool visible)
    {
        if (hasAppliedVisibility && lastVisible == visible)
        {
            return;
        }

        hasAppliedVisibility = true;
        lastVisible = visible;

        if (compassRenderer != null)
        {
            compassRenderer.enabled = visible;
            return;
        }

        if (compassVisual != null &&
            compassVisual.gameObject.activeSelf != visible)
        {
            compassVisual.gameObject.SetActive(visible);
        }
    }

    private void FindReferences()
    {
        if (missionManager == null)
        {
            missionManager =
                FindAnyObjectByType<MissionManager2D>();
        }

        if (playerTransform == null)
        {
            PlayerMove playerMove =
                FindAnyObjectByType<PlayerMove>();

            if (playerMove != null)
            {
                playerTransform = playerMove.transform;
            }
        }

        if (compassVisual == null)
        {
            compassVisual = transform;
        }

        if (compassRenderer == null &&
            compassVisual != null)
        {
            compassRenderer =
                compassVisual.GetComponent<SpriteRenderer>();
        }

        if (compassRenderer == null &&
            compassVisual != null)
        {
            compassRenderer =
                compassVisual.GetComponentInChildren<SpriteRenderer>(
                    true
                );
        }
    }

    private void OnValidate()
    {
        rotationSpeed = Mathf.Max(0f, rotationSpeed);
        hideAtTargetDistance = Mathf.Max(
            0f,
            hideAtTargetDistance
        );
    }
}
