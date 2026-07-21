using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// TorchControllerの残量に応じて、Player周辺のLight 2Dの
/// 範囲・明るさを変化させます。
/// </summary>
[DisallowMultipleComponent]
public class TorchLight2DController : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private TorchController torchController;

    [Tooltip("Playerの子に置いたPoint Light 2Dを設定します")]
    [SerializeField] private Light2D torchLight;

    [Tooltip("必要な場合だけ設定します。通常はGlobal Lightを低い値で固定しても構いません")]
    [SerializeField] private Light2D globalLight;

    [Header("Point Light 2D：明るい時")]
    [SerializeField, Min(0f)] private float maximumOuterRadius = 7f;
    [SerializeField, Min(0f)] private float maximumInnerRadius = 2.2f;
    [SerializeField, Min(0f)] private float maximumIntensity = 1.1f;

    [Header("Point Light 2D：消灯時")]
    [SerializeField, Min(0f)] private float minimumOuterRadius = 0.65f;
    [SerializeField, Min(0f)] private float minimumInnerRadius = 0.05f;
    [SerializeField, Min(0f)] private float minimumIntensity = 0.03f;

    [Header("残量から明るさへの変換")]
    [Tooltip("横軸が松明残量、縦軸が視界の強さです")]
    [SerializeField]
    private AnimationCurve visibilityCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Tooltip("値が大きいほどLightの変化が速くなります")]
    [SerializeField, Min(0.01f)] private float transitionSpeed = 6f;

    [Header("低残量時の炎の揺れ")]
    [SerializeField] private bool enableLowTorchFlicker = true;

    [SerializeField, Range(0f, 1f)]
    private float flickerStartPercent = 0.25f;

    [SerializeField, Min(0f)] private float flickerIntensity = 0.12f;
    [SerializeField, Min(0.01f)] private float flickerSpeed = 7f;

    [Header("Global Light 2D（任意）")]
    [Tooltip("オンなら松明残量に合わせてシーン全体の暗さも変えます")]
    [SerializeField] private bool controlGlobalLight;

    [SerializeField, Min(0f)] private float maximumGlobalIntensity = 0.08f;
    [SerializeField, Min(0f)] private float minimumGlobalIntensity = 0.01f;

    private float torchPercent = 1f;
    private bool isSubscribed;
    private float flickerSeed;

    private void Awake()
    {
        FindReferences();
        flickerSeed = Random.Range(0f, 1000f);
        RefreshFromController(true);
    }

    private void OnEnable()
    {
        FindReferences();
        Subscribe();
        RefreshFromController(true);
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (torchLight == null)
        {
            return;
        }

        float curveValue = visibilityCurve != null
            ? Mathf.Clamp01(visibilityCurve.Evaluate(torchPercent))
            : torchPercent;

        float targetOuterRadius = Mathf.Lerp(
            minimumOuterRadius,
            maximumOuterRadius,
            curveValue
        );

        float targetInnerRadius = Mathf.Lerp(
            minimumInnerRadius,
            maximumInnerRadius,
            curveValue
        );

        float targetIntensity = Mathf.Lerp(
            minimumIntensity,
            maximumIntensity,
            curveValue
        );

        if (enableLowTorchFlicker &&
            torchPercent > 0f &&
            torchPercent <= flickerStartPercent)
        {
            float flickerStrength = Mathf.InverseLerp(
                flickerStartPercent,
                0f,
                torchPercent
            );

            float noise = Mathf.PerlinNoise(
                flickerSeed,
                Time.unscaledTime * flickerSpeed
            );

            float centeredNoise = noise * 2f - 1f;
            targetIntensity +=
                centeredNoise * flickerIntensity * flickerStrength;
        }

        float delta = transitionSpeed * Time.unscaledDeltaTime;

        torchLight.pointLightOuterRadius = Mathf.MoveTowards(
            torchLight.pointLightOuterRadius,
            targetOuterRadius,
            delta * Mathf.Max(1f, maximumOuterRadius)
        );

        torchLight.pointLightInnerRadius = Mathf.MoveTowards(
            torchLight.pointLightInnerRadius,
            Mathf.Min(targetInnerRadius, targetOuterRadius),
            delta * Mathf.Max(1f, maximumInnerRadius)
        );

        torchLight.intensity = Mathf.MoveTowards(
            torchLight.intensity,
            Mathf.Max(0f, targetIntensity),
            delta
        );

        if (controlGlobalLight && globalLight != null)
        {
            float targetGlobalIntensity = Mathf.Lerp(
                minimumGlobalIntensity,
                maximumGlobalIntensity,
                curveValue
            );

            globalLight.intensity = Mathf.MoveTowards(
                globalLight.intensity,
                targetGlobalIntensity,
                delta
            );
        }
    }

    private void HandleTorchChanged(float current, float maximum)
    {
        torchPercent = maximum <= 0f
            ? 0f
            : Mathf.Clamp01(current / maximum);
    }

    private void RefreshFromController(bool applyImmediately)
    {
        if (torchController == null)
        {
            return;
        }

        HandleTorchChanged(
            torchController.CurrentTorch,
            torchController.MaximumTorch
        );

        if (!applyImmediately || torchLight == null)
        {
            return;
        }

        float curveValue = visibilityCurve != null
            ? Mathf.Clamp01(visibilityCurve.Evaluate(torchPercent))
            : torchPercent;

        torchLight.pointLightOuterRadius = Mathf.Lerp(
            minimumOuterRadius,
            maximumOuterRadius,
            curveValue
        );

        torchLight.pointLightInnerRadius = Mathf.Min(
            torchLight.pointLightOuterRadius,
            Mathf.Lerp(
                minimumInnerRadius,
                maximumInnerRadius,
                curveValue
            )
        );

        torchLight.intensity = Mathf.Lerp(
            minimumIntensity,
            maximumIntensity,
            curveValue
        );

        if (controlGlobalLight && globalLight != null)
        {
            globalLight.intensity = Mathf.Lerp(
                minimumGlobalIntensity,
                maximumGlobalIntensity,
                curveValue
            );
        }
    }

    private void Subscribe()
    {
        if (isSubscribed || torchController == null)
        {
            return;
        }

        torchController.TorchChanged += HandleTorchChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || torchController == null)
        {
            return;
        }

        torchController.TorchChanged -= HandleTorchChanged;
        isSubscribed = false;
    }

    private void FindReferences()
    {
        if (torchController == null)
        {
            torchController = GetComponentInParent<TorchController>();
        }

        if (torchController == null)
        {
            torchController = FindAnyObjectByType<TorchController>();
        }

        if (torchLight == null)
        {
            torchLight = GetComponent<Light2D>();
        }

        if (torchLight == null)
        {
            Light2D[] lights = GetComponentsInChildren<Light2D>(true);

            foreach (Light2D light2D in lights)
            {
                if (light2D != null &&
                    light2D.lightType == Light2D.LightType.Point)
                {
                    torchLight = light2D;
                    break;
                }
            }
        }
    }

    private void OnValidate()
    {
        maximumOuterRadius = Mathf.Max(0f, maximumOuterRadius);
        minimumOuterRadius = Mathf.Clamp(
            minimumOuterRadius,
            0f,
            maximumOuterRadius
        );

        maximumInnerRadius = Mathf.Clamp(
            maximumInnerRadius,
            0f,
            maximumOuterRadius
        );

        minimumInnerRadius = Mathf.Clamp(
            minimumInnerRadius,
            0f,
            maximumInnerRadius
        );

        maximumIntensity = Mathf.Max(0f, maximumIntensity);
        minimumIntensity = Mathf.Clamp(
            minimumIntensity,
            0f,
            maximumIntensity
        );

        transitionSpeed = Mathf.Max(0.01f, transitionSpeed);
        flickerStartPercent = Mathf.Clamp01(flickerStartPercent);
        flickerIntensity = Mathf.Max(0f, flickerIntensity);
        flickerSpeed = Mathf.Max(0.01f, flickerSpeed);

        maximumGlobalIntensity = Mathf.Max(0f, maximumGlobalIntensity);
        minimumGlobalIntensity = Mathf.Clamp(
            minimumGlobalIntensity,
            0f,
            maximumGlobalIntensity
        );

        if (visibilityCurve == null)
        {
            visibilityCurve = AnimationCurve.EaseInOut(
                0f,
                0f,
                1f,
                1f
            );
        }
    }
}
