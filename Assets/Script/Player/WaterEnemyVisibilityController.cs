using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class WaterEnemyVisibilityController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら同じPlayerから自動取得します")]
    [SerializeField]
    private PlayerSurvivalController survivalController;

    [Header("対象レイヤー")]
    [Tooltip("Project Settings > Tags and Layers で作成した Enemy レイヤーを設定")]
    [SerializeField]
    private LayerMask enemyLayer;

    [Header("水分不足時の視認距離")]
    [Tooltip("水分が低下状態（既定では30％以下）になった時の視認距離")]
    [SerializeField, Min(0f)]
    private float lowWaterVisibilityDistance = 14f;

    [Tooltip("水分が危険状態（既定では10％以下）になった時の視認距離")]
    [SerializeField, Min(0f)]
    private float criticalWaterVisibilityDistance = 8f;

    [Tooltip("水分が0になった時の視認距離")]
    [SerializeField, Min(0f)]
    private float emptyWaterVisibilityDistance = 4f;

    [Header("フェード設定")]
    [Tooltip("視認距離の境界から、この距離だけ近づく間に透明から完全表示へ変化します")]
    [SerializeField, Min(0f)]
    private float fadeInDistance = 4f;

    [Tooltip("透明度が目標値へ近づく速さ。大きいほど素早く変化します")]
    [SerializeField, Min(0.01f)]
    private float fadeSpeed = 5f;

    [Header("探索設定")]
    [Tooltip("新しく出現した敵を探し直す間隔。小さくしすぎる必要はありません")]
    [SerializeField, Min(0.05f)]
    private float refreshInterval = 0.5f;

    [Header("デバッグ表示")]
    [SerializeField] private bool showVisibilityRangeGizmo = true;

    private readonly Dictionary<Transform, EnemyVisualTarget>
        enemyTargets = new Dictionary<Transform, EnemyVisualTarget>();

    private readonly List<Transform> targetsToRemove =
        new List<Transform>();

    private float nextRefreshTime;

    private void Awake()
    {
        FindSurvivalController();
    }

    private void OnEnable()
    {
        FindSurvivalController();
        RefreshEnemyCache();
        UpdateEnemyVisibility(0f);
    }

    private void Update()
    {
        FindSurvivalController();

        if (Time.time >= nextRefreshTime)
        {
            RefreshEnemyCache();
            nextRefreshTime = Time.time + refreshInterval;
        }

        UpdateEnemyVisibility(Time.deltaTime);
    }

    private void OnDisable()
    {
        RestoreAllEnemyRenderers();
    }

    private void OnDestroy()
    {
        RestoreAllEnemyRenderers();
    }

    [ContextMenu("Refresh Enemy Cache")]
    public void RefreshEnemyCache()
    {
        Transform[] allTransforms =
            FindObjectsByType<Transform>(FindObjectsInactive.Exclude);

        HashSet<Transform> foundRoots = new HashSet<Transform>();

        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null ||
                !IsOnEnemyLayer(candidate.gameObject))
            {
                continue;
            }

            Transform enemyRoot = GetEnemyRoot(candidate);

            if (enemyRoot == null || !foundRoots.Add(enemyRoot))
            {
                continue;
            }

            if (!enemyTargets.TryGetValue(
                    enemyRoot,
                    out EnemyVisualTarget target))
            {
                target = new EnemyVisualTarget(enemyRoot);
                enemyTargets.Add(enemyRoot, target);
            }

            target.RefreshSprites();
        }

        targetsToRemove.Clear();

        foreach (KeyValuePair<Transform, EnemyVisualTarget> pair
                 in enemyTargets)
        {
            if (pair.Key == null || !foundRoots.Contains(pair.Key))
            {
                pair.Value.Restore();
                targetsToRemove.Add(pair.Key);
            }
        }

        foreach (Transform targetRoot in targetsToRemove)
        {
            enemyTargets.Remove(targetRoot);
        }
    }

    private void UpdateEnemyVisibility(float deltaTime)
    {
        float visibilityDistance = GetVisibilityDistance();

        foreach (EnemyVisualTarget target in enemyTargets.Values)
        {
            if (target == null || target.Root == null)
            {
                continue;
            }

            float targetVisibility = GetTargetVisibility(
                target.Root.position,
                visibilityDistance
            );

            target.SetVisibility(
                targetVisibility,
                fadeSpeed,
                deltaTime
            );
        }
    }

    private float GetTargetVisibility(
        Vector3 targetPosition,
        float visibilityDistance)
    {
        // 水分が十分な時は距離制限なしで完全表示
        if (float.IsPositiveInfinity(visibilityDistance))
        {
            return 1f;
        }

        if (visibilityDistance <= 0f)
        {
            return 0f;
        }

        float distance = Vector2.Distance(
            transform.position,
            targetPosition
        );

        // 視認距離の外は完全透明
        if (distance >= visibilityDistance)
        {
            return 0f;
        }

        // fadeInDistance を 0 にした場合は、従来どおり即時表示
        if (fadeInDistance <= 0f)
        {
            return 1f;
        }

        float fullyVisibleDistance = Mathf.Max(
            0f,
            visibilityDistance - fadeInDistance
        );

        // 例：視認距離14、フェード距離4なら、
        // 14で透明 → 10で完全表示になる
        return Mathf.InverseLerp(
            visibilityDistance,
            fullyVisibleDistance,
            distance
        );
    }

    private float GetVisibilityDistance()
    {
        if (survivalController == null)
        {
            return float.PositiveInfinity;
        }

        switch (survivalController.WaterState)
        {
            case SurvivalNeedState.Low:
                return lowWaterVisibilityDistance;

            case SurvivalNeedState.Critical:
                return criticalWaterVisibilityDistance;

            case SurvivalNeedState.Empty:
                return emptyWaterVisibilityDistance;

            // Normal と Warning は通常どおり全距離で見える
            default:
                return float.PositiveInfinity;
        }
    }

    private Transform GetEnemyRoot(Transform source)
    {
        Transform root = source;
        Transform parent = source.parent;

        // 親もEnemyレイヤーなら、最も上のEnemyレイヤーの親を
        // その敵の基準位置として扱う
        while (parent != null && IsOnEnemyLayer(parent.gameObject))
        {
            root = parent;
            parent = parent.parent;
        }

        return root;
    }

    private bool IsOnEnemyLayer(GameObject target)
    {
        return target != null &&
            (enemyLayer.value & (1 << target.layer)) != 0;
    }

    private bool FindSurvivalController()
    {
        if (survivalController != null)
        {
            return true;
        }

        survivalController =
            GetComponent<PlayerSurvivalController>();

        return survivalController != null;
    }

    private void RestoreAllEnemyRenderers()
    {
        foreach (EnemyVisualTarget target in enemyTargets.Values)
        {
            target?.Restore();
        }

        enemyTargets.Clear();
        targetsToRemove.Clear();
    }

    private void OnValidate()
    {
        lowWaterVisibilityDistance = Mathf.Max(
            0f,
            lowWaterVisibilityDistance
        );

        criticalWaterVisibilityDistance = Mathf.Max(
            0f,
            criticalWaterVisibilityDistance
        );

        emptyWaterVisibilityDistance = Mathf.Max(
            0f,
            emptyWaterVisibilityDistance
        );

        fadeInDistance = Mathf.Max(0f, fadeInDistance);
        fadeSpeed = Mathf.Max(0.01f, fadeSpeed);
        refreshInterval = Mathf.Max(0.05f, refreshInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showVisibilityRangeGizmo || !Application.isPlaying)
        {
            return;
        }

        float visibilityDistance = GetVisibilityDistance();

        if (float.IsPositiveInfinity(visibilityDistance))
        {
            return;
        }

        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, visibilityDistance);

        float fullyVisibleDistance = Mathf.Max(
            0f,
            visibilityDistance - fadeInDistance
        );

        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(
            transform.position,
            fullyVisibleDistance
        );
    }

    private sealed class EnemyVisualTarget
    {
        public Transform Root { get; }

        private readonly Dictionary<SpriteRenderer, SpriteState>
            originalStates =
                new Dictionary<SpriteRenderer, SpriteState>();

        private float currentVisibility = 1f;

        public EnemyVisualTarget(Transform root)
        {
            Root = root;
            RefreshSprites();
        }

        public void RefreshSprites()
        {
            if (Root == null)
            {
                return;
            }

            SpriteRenderer[] sprites =
                Root.GetComponentsInChildren<SpriteRenderer>(true);

            foreach (SpriteRenderer sprite in sprites)
            {
                if (sprite == null || originalStates.ContainsKey(sprite))
                {
                    continue;
                }

                originalStates.Add(
                    sprite,
                    new SpriteState(sprite.enabled, sprite.color)
                );
            }
        }

        public void SetVisibility(
            float targetVisibility,
            float fadeSpeed,
            float deltaTime)
        {
            targetVisibility = Mathf.Clamp01(targetVisibility);

            // OnEnable直後など deltaTime が0の時は、
            // 見た目をただちに正しい状態へ合わせる
            if (deltaTime <= 0f)
            {
                currentVisibility = targetVisibility;
            }
            else
            {
                currentVisibility = Mathf.MoveTowards(
                    currentVisibility,
                    targetVisibility,
                    fadeSpeed * deltaTime
                );
            }

            foreach (KeyValuePair<SpriteRenderer, SpriteState> pair
                     in originalStates)
            {
                SpriteRenderer sprite = pair.Key;

                if (sprite == null)
                {
                    continue;
                }

                SpriteState original = pair.Value;

                // 元から非表示のSpriteRendererは表示しない
                if (!original.enabled)
                {
                    sprite.enabled = false;
                    continue;
                }

                Color color = original.color;
                color.a *= currentVisibility;

                sprite.color = color;
                sprite.enabled = currentVisibility > 0.001f;
            }
        }

        public void Restore()
        {
            currentVisibility = 1f;

            foreach (KeyValuePair<SpriteRenderer, SpriteState> pair
                     in originalStates)
            {
                if (pair.Key == null)
                {
                    continue;
                }

                pair.Key.enabled = pair.Value.enabled;
                pair.Key.color = pair.Value.color;
            }
        }

        private readonly struct SpriteState
        {
            public readonly bool enabled;
            public readonly Color color;

            public SpriteState(bool enabled, Color color)
            {
                this.enabled = enabled;
                this.color = color;
            }
        }
    }
}
