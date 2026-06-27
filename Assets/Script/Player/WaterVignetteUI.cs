using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(RectTransform))]
public class WaterVignetteUI : MonoBehaviour
{
    [Header("参照")]
    [SerializeField]
    private PlayerSurvivalController survivalController;

    [Tooltip("未設定なら、このオブジェクトのImageを使います")]
    [SerializeField]
    private Image vignetteImage;

    [Header("水分不足で暗くなる設定")]
    [Tooltip("水分がこの割合以下になると、画面端が暗くなり始めます")]
    [SerializeField, Range(0f, 1f)]
    private float darkenStartWaterPercent = 0.3f;

    [Tooltip("水分が0の時の最大暗さ")]
    [SerializeField, Range(0f, 1f)]
    private float maxDarknessAlpha = 0.55f;

    [Tooltip("暗さの切り替わる速さ")]
    [SerializeField, Min(0.01f)]
    private float fadeSpeed = 3f;

    [Header("ビネット見た目")]
    [SerializeField]
    private bool createVignetteSpriteAtRuntime = true;

    [Tooltip("中央の明るい範囲。大きいほど端だけが暗くなります")]
    [SerializeField, Range(0.1f, 0.95f)]
    private float innerClearArea = 0.5f;

    [Tooltip("暗くなる境目の柔らかさ")]
    [SerializeField, Range(0.1f, 4f)]
    private float gradientPower = 1.5f;

    [SerializeField, Min(32)]
    private int textureSize = 256;

    [SerializeField]
    private Color vignetteColor = Color.black;

    private float currentAlpha;
    private float targetAlpha;
    private bool isSubscribed;

    private Texture2D generatedTexture;
    private Sprite generatedSprite;

    private void Awake()
    {
        EnsureImage();
        CreateVignetteSprite();

        FindSurvivalController();
        RefreshFromWater();
    }

    private void OnEnable()
    {
        FindSurvivalController();
        SubscribeEvents();
        RefreshFromWater();
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

    private void HandleWaterChanged(
        float currentWater,
        float maxWater)
    {
        SetTargetAlpha(currentWater, maxWater);
    }

    private void RefreshFromWater()
    {
        if (survivalController == null)
        {
            targetAlpha = 0f;
            currentAlpha = 0f;
            ApplyAlpha();
            return;
        }

        SetTargetAlpha(
            survivalController.CurrentWater,
            survivalController.MaxWater
        );

        currentAlpha = targetAlpha;
        ApplyAlpha();
    }

    private void SetTargetAlpha(
        float currentWater,
        float maxWater)
    {
        float waterPercent = maxWater <= 0f
            ? 0f
            : Mathf.Clamp01(currentWater / maxWater);

        float darknessPercent;

        if (darkenStartWaterPercent <= 0.001f)
        {
            darknessPercent = waterPercent <= 0.01f
                ? 1f
                : 0f;
        }
        else
        {
            darknessPercent = Mathf.Clamp01(
                (darkenStartWaterPercent - waterPercent) /
                darkenStartWaterPercent
            );
        }

        targetAlpha =
            darknessPercent * maxDarknessAlpha;
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
        if (isSubscribed || survivalController == null)
        {
            return;
        }

        survivalController.WaterChanged +=
            HandleWaterChanged;

        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || survivalController == null)
        {
            return;
        }

        survivalController.WaterChanged -=
            HandleWaterChanged;

        isSubscribed = false;
    }

    private void FindSurvivalController()
    {
        if (survivalController != null)
        {
            return;
        }

        survivalController =
            FindAnyObjectByType<PlayerSurvivalController>();
    }

    private void EnsureImage()
    {
        if (vignetteImage == null)
        {
            vignetteImage = GetComponent<Image>();
        }

        if (vignetteImage == null)
        {
            vignetteImage =
                gameObject.AddComponent<Image>();
        }

        vignetteImage.raycastTarget = false;
        vignetteImage.type = Image.Type.Simple;

        RectTransform rectTransform =
            GetComponent<RectTransform>();

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

        generatedTexture.name =
            "WaterVignette_RuntimeTexture";

        generatedTexture.filterMode = FilterMode.Bilinear;
        generatedTexture.wrapMode = TextureWrapMode.Clamp;

        Color[] pixels = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            float normalizedY =
                Mathf.Abs(
                    (y / (float)(size - 1)) * 2f - 1f
                );

            for (int x = 0; x < size; x++)
            {
                float normalizedX =
                    Mathf.Abs(
                        (x / (float)(size - 1)) * 2f - 1f
                    );

                float edgeDistance = Mathf.Max(
                    normalizedX,
                    normalizedY
                );

                float alpha = Mathf.InverseLerp(
                    innerClearArea,
                    1f,
                    edgeDistance
                );

                alpha = Mathf.SmoothStep(
                    0f,
                    1f,
                    alpha
                );

                alpha = Mathf.Pow(
                    alpha,
                    gradientPower
                );

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

        generatedSprite.name =
            "WaterVignette_RuntimeSprite";

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
        darkenStartWaterPercent = Mathf.Clamp01(
            darkenStartWaterPercent
        );

        maxDarknessAlpha = Mathf.Clamp01(
            maxDarknessAlpha
        );

        fadeSpeed = Mathf.Max(0.01f, fadeSpeed);

        innerClearArea = Mathf.Clamp(
            innerClearArea,
            0.1f,
            0.95f
        );

        gradientPower = Mathf.Clamp(
            gradientPower,
            0.1f,
            4f
        );

        textureSize = Mathf.Clamp(
            textureSize,
            32,
            512
        );
    }
}