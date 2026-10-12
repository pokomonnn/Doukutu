using UnityEngine;

/// <summary>
/// 救出先を画面上の矢印で案内。ArrowImageは上向き（↑）の画像を使う。
/// CanvasはScreen Space - Overlay / Cameraの両方に対応。
/// </summary>
[DisallowMultipleComponent]
public sealed class CaveTutorialArrow2D : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Canvas canvas;
    [Tooltip("画面全体にストレッチした親RectTransform（Canvas直下の全画面パネル等）")]
    [SerializeField] private RectTransform fullScreenRect;
    [SerializeField] private RectTransform arrowImage;

    [Header("カメラ・表示位置")]
    [SerializeField] private Camera worldCamera;
    [SerializeField, Min(0f)] private float screenEdgePadding = 48f;

    private Transform destination;
    private bool shouldShow;

    private void Awake()
    {
        if (canvas == null) canvas = GetComponentInParent<Canvas>();
        if (worldCamera == null) worldCamera = Camera.main;
        if (arrowImage != null) arrowImage.gameObject.SetActive(false);
    }

    public void Initialize(Transform target)
    {
        destination = target;
        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        shouldShow = visible;
        if (arrowImage == null) return;
        bool actuallyVisible = shouldShow && destination != null;
        if (arrowImage.gameObject.activeSelf != actuallyVisible)
            arrowImage.gameObject.SetActive(actuallyVisible);
    }

    private void LateUpdate()
    {
        if (!shouldShow || destination == null || canvas == null ||
            fullScreenRect == null || arrowImage == null || worldCamera == null) return;

        Vector3 screen = worldCamera.WorldToScreenPoint(destination.position);
        Vector2 screenPoint = new Vector2(screen.x, screen.y);
        Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        if (screen.z < 0f) screenPoint = screenCenter * 2f - screenPoint;

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        Vector2 localPoint;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                fullScreenRect, screenPoint, uiCamera, out localPoint)) return;

        Rect rect = fullScreenRect.rect;
        Vector2 center = rect.center;
        Vector2 direction = localPoint - center;
        float maxX = Mathf.Max(0f, rect.width * 0.5f - screenEdgePadding);
        float maxY = Mathf.Max(0f, rect.height * 0.5f - screenEdgePadding);
        Vector2 clamped = new Vector2(
            Mathf.Clamp(direction.x, -maxX, maxX),
            Mathf.Clamp(direction.y, -maxY, maxY));

        // arrowImageは親RectTransformの中央アンカー・中央Pivotに設定する。
        arrowImage.anchoredPosition = clamped;
        if (direction.sqrMagnitude > 0.001f)
        {
            float degrees = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg - 90f;
            arrowImage.localRotation = Quaternion.Euler(0f, 0f, degrees);
        }
    }
}
