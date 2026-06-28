using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SAN値が減るほど画面の周囲を赤く表示するUIです。
/// Canvas内の全面Imageに付けて使います。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class SanityVignetteUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private PlayerSanityController sanityController;

    [Tooltip("未設定なら、このGameObjectのImageを使います")]
    [SerializeField] private Image vignetteImage;

    [Header("SAN値が低い時の赤み")]
    [Tooltip("SAN値がこの割合以下になると、画面端が赤くなり始めます")]
    [SerializeField, Range(0f, 1f)]
    private float redStartSanityPercent = 0.7f;

    [Tooltip("SAN値が0の時の最大赤み")]
    [SerializeField, Range(0f, 1f)]
    private float maxRedAlpha = 0.7f;

    [Tooltip("赤みの切り替わる速さ")]
    [SerializeField, Min(0.01f)]
    private float fadeSpeed = 3f;

    [Header("ビネット見た目")]
    [SerializeField] private bool createVignetteSpriteAtRuntime = true;

    [Tooltip("中央の透明な範囲。大きいほど端だけが赤くなります")]
    [SerializeField, Range(0.1f, 0.95f)]
    private float innerClearArea = 0.5f;

    [Tooltip("赤くなる境目の柔らかさ")]
    [SerializeField, Range(0.1f, 4f)]
    private float gradientPower = 1.5f;

    [SerializeField, Min(32)] private int textureSize = 256;

    [SerializeField]
    private Color vignetteColor = new Color(1f, 0.05f, 0.05f, 1f);

    private float currentAlpha;
    private float targetAlpha;
    private bool isSubscribed;

    private Texture2D generatedTexture;
    private Sprite generatedSprite;

    private void Awake()
    {
        EnsureImage();
        CreateVignetteSprite();

        FindSanityController();
        RefreshFromSanity();
    }

    private void OnEnable()
    {
        FindSanityController();
        SubscribeEvents();
        RefreshFromSanity();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void OnDestroy()
    {
        DestroyGeneratedVisual();
    }

    private void Update()
    {
        currentAlpha = Mathf.MoveTowards(
            currentAlpha,
            targetAlpha,
            fadeSpeed * Time.unscaledDeltaTime
        );

        ApplyAlpha();
    }

    private void HandleSanityChanged(float currentSanity, float maxSanity)
    {
        SetTargetAlpha(currentSanity, maxSanity);
    }

    private void RefreshFromSanity()
    {
        if (sanityController == null)
        {
            targetAlpha = 0f;
            currentAlpha = 0f;
            ApplyAlpha();
            return;
        }

        SetTargetAlpha(
            sanityController.CurrentSanity,
            sanityController.MaxSanity
        );

        currentAlpha = targetAlpha;
        ApplyAlpha();
    }

    private void SetTargetAlpha(float currentSanity, float maxSanity)
    {
        float sanityPercent = maxSanity <= 0f
            ? 0f
            : Mathf.Clamp01(currentSanity / maxSanity);

        float redPercent;

        if (redStartSanityPercent <= 0.001f)
        {
            redPercent = sanityPercent <= 0.01f ? 1f : 0f;
        }
        else
        {
            redPercent = Mathf.Clamp01(
                (redStartSanityPercent - sanityPercent) /
                redStartSanityPercent
            );
        }

        targetAlpha = redPercent * maxRedAlpha;
    }

    private void ApplyAlpha()
    {
        if (vignetteImage == null)
        {
            return;
        }

        Color color = vignetteColor;
        color.a = currentAlpha;

        vignetteImage.color = color;
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || sanityController == null)
        {
            return;
        }

        sanityController.SanityChanged += HandleSanityChanged;
        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || sanityController == null)
        {
            return;
        }

        sanityController.SanityChanged -= HandleSanityChanged;
        isSubscribed = false;
    }

    private void FindSanityController()
    {
        if (sanityController != null)
        {
            return;
        }

        sanityController =
            FindAnyObjectByType<PlayerSanityController>();
    }

    private void EnsureImage()
    {
        if (vignetteImage == null)
        {
            vignetteImage = GetComponent<Image>();
        }

        if (vignetteImage == null)
        {
            vignetteImage = gameObject.AddComponent<Image>();
        }

        vignetteImage.raycastTarget = false;
        vignetteImage.type = Image.Type.Simple;

        RectTransform rectTransform = GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private void CreateVignetteSprite()
    {
        if (!createVignetteSpriteAtRuntime ||
            vignetteImage == null ||
            generatedSprite != null)
        {
            return;
        }

        int size = Mathf.Clamp(textureSize, 32, 512);

        generatedTexture = new Texture2D(
            size,
            size,
            TextureFormat.RGBA32,
            false
        );

        generatedTexture.name = "SanityVignette_RuntimeTexture";
        generatedTexture.filterMode = FilterMode.Bilinear;
        generatedTexture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float normalizedY = Mathf.Abs(
                (y / (float)(size - 1)) * 2f - 1f
            );

            for (int x = 0; x < size; x++)
            {
                float normalizedX = Mathf.Abs(
                    (x / (float)(size - 1)) * 2f - 1f
                );

                float edgeDistance = Mathf.Max(normalizedX, normalizedY);

                float alpha = Mathf.InverseLerp(
                    innerClearArea,
                    1f,
                    edgeDistance
                );

                alpha = Mathf.SmoothStep(0f, 1f, alpha);
                alpha = Mathf.Pow(alpha, gradientPower);

                pixels[y * size + x] =
                    new Color(1f, 1f, 1f, alpha);
            }
        }

        generatedTexture.SetPixels(pixels);
        generatedTexture.Apply();

        generatedSprite = Sprite.Create(
            generatedTexture,
            new Rect(0f, 0f, size, size),
            new Vector2(0.5f, 0.5f),
            100f,
            0,
            SpriteMeshType.FullRect
        );

        generatedSprite.name = "SanityVignette_RuntimeSprite";
        vignetteImage.sprite = generatedSprite;
    }

    private void DestroyGeneratedVisual()
    {
        if (vignetteImage != null &&
            vignetteImage.sprite == generatedSprite)
        {
            vignetteImage.sprite = null;
        }

        if (generatedSprite != null)
        {
            Destroy(generatedSprite);
            generatedSprite = null;
        }

        if (generatedTexture != null)
        {
            Destroy(generatedTexture);
            generatedTexture = null;
        }
    }

    private void OnValidate()
    {
        redStartSanityPercent = Mathf.Clamp01(redStartSanityPercent);
        maxRedAlpha = Mathf.Clamp01(maxRedAlpha);
        fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
        innerClearArea = Mathf.Clamp(innerClearArea, 0.1f, 0.95f);
        gradientPower = Mathf.Clamp(gradientPower, 0.1f, 4f);
        textureSize = Mathf.Clamp(textureSize, 32, 512);
    }
}
