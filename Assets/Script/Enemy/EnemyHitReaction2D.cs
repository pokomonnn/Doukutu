using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// CharacterHealthのHP減少を検知し、敵へノックバックと赤い被弾フラッシュを加えます。
/// DamageDealerが渡す弾の位置を使い、弾が来た方向とは反対へ押し出します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
[RequireComponent(typeof(Rigidbody2D))]
public class EnemyHitReaction2D : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CharacterHealth health;
    [SerializeField] private Rigidbody2D enemyRigidbody;
    [SerializeField] private EnemyChaser2D enemyChaser;

    [Tooltip("空欄なら子オブジェクトを含めた全SpriteRendererを自動取得します")]
    [SerializeField] private SpriteRenderer[] spriteRenderers;

    [Tooltip("DamageDealerから発射元が渡されなかった場合に、ノックバック方向の基準として使います")]
    [SerializeField] private Transform fallbackAttackerTransform;

    [Header("ノックバック")]
    [Tooltip("被弾時の横方向の押し出し速度")]
    [SerializeField, Min(0f)] private float knockbackSpeed = 4.5f;

    [Tooltip("被弾時に少しだけ浮かせる上向き速度。0なら横方向だけです")]
    [SerializeField, Min(0f)] private float upwardKnockbackSpeed = 0.35f;

    [Tooltip("ノックバック中にEnemyChaser2Dの追跡・攻撃を止める秒数")]
    [SerializeField, Min(0f)] private float hitStunDuration = 0.14f;

    [Header("被弾フラッシュ")]
    [SerializeField] private bool enableHitFlash = true;

    [Tooltip("フラッシュ全体の長さ")]
    [SerializeField, Min(0.01f)] private float flashDuration = 0.14f;

    [Tooltip("赤く点滅する回数")]
    [SerializeField, Min(1)] private int flashCount = 2;

    [SerializeField] private Color flashColor = new Color(1f, 0.1f, 0.1f, 1f);

    [Tooltip("0なら元の色、1なら指定したFlash Colorになります")]
    [SerializeField, Range(0f, 1f)] private float flashIntensity = 0.9f;

    [Header("デバッグ")]
    [SerializeField] private bool showHitLogs;

    private readonly Dictionary<SpriteRenderer, Color> originalColors =
        new Dictionary<SpriteRenderer, Color>();

    private int lastHealth;
    private bool hasCachedHealth;

    private bool hasPendingHitSource;
    private Vector2 pendingHitSourcePosition;
    private float pendingHitSourceExpireTime;

    private bool isFlashing;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        FindReferences();
        CacheSpriteRenderers();
    }

    private void OnEnable()
    {
        FindReferences();
        CacheSpriteRenderers();
        SubscribeHealth();
    }

    private void Start()
    {
        CacheCurrentHealth();
    }

    private void OnDisable()
    {
        UnsubscribeHealth();
        StopFlashAndRestore();
        hasPendingHitSource = false;
    }

    /// <summary>
    /// DamageDealerから、命中した弾などの位置を渡すために呼ばれます。
    /// このメソッドを直接呼ばなくても、HPが減ればフォールバック方向で反応します。
    /// </summary>
    public void NotifyHitSource(Vector2 sourcePosition)
    {
        hasPendingHitSource = true;
        pendingHitSourcePosition = sourcePosition;
        pendingHitSourceExpireTime = Time.time + 0.35f;
    }

    [ContextMenu("Test Hit Reaction")]
    public void TestHitReaction()
    {
        Vector2 sourcePosition = transform.position + Vector3.left;
        ApplyHitReaction(sourcePosition);
    }

    private void HandleHealthChanged(int currentHealth, int maxHealth)
    {
        if (!hasCachedHealth)
        {
            lastHealth = currentHealth;
            hasCachedHealth = true;
            return;
        }

        bool tookDamage = currentHealth < lastHealth;
        lastHealth = currentHealth;

        if (!tookDamage)
        {
            return;
        }

        ApplyHitReaction(GetHitSourcePosition());
    }

    private void ApplyHitReaction(Vector2 sourcePosition)
    {
        FindReferences();

        Vector2 knockbackDirection = GetKnockbackDirection(sourcePosition);

        if (enemyRigidbody != null)
        {
            Vector2 velocity = enemyRigidbody.linearVelocity;
            velocity.x = knockbackDirection.x * knockbackSpeed;

            if (upwardKnockbackSpeed > 0f)
            {
                velocity.y = Mathf.Max(
                    velocity.y,
                    upwardKnockbackSpeed
                );
            }

            enemyRigidbody.linearVelocity = velocity;
        }

        enemyChaser?.ApplyHitStun(hitStunDuration);

        if (enableHitFlash)
        {
            StartHitFlash();
        }

        if (showHitLogs)
        {
            Debug.Log(
                $"[EnemyHitReaction2D] {name}: 被弾。" +
                $"source={sourcePosition}, direction={knockbackDirection}",
                this
            );
        }
    }

    private Vector2 GetHitSourcePosition()
    {
        if (hasPendingHitSource &&
            Time.time <= pendingHitSourceExpireTime)
        {
            hasPendingHitSource = false;
            return pendingHitSourcePosition;
        }

        hasPendingHitSource = false;

        if (fallbackAttackerTransform != null)
        {
            return fallbackAttackerTransform.position;
        }

        return transform.position + Vector3.left;
    }

    private Vector2 GetKnockbackDirection(Vector2 sourcePosition)
    {
        float directionX = transform.position.x - sourcePosition.x;

        if (Mathf.Abs(directionX) <= 0.01f &&
            fallbackAttackerTransform != null)
        {
            directionX = transform.position.x -
                fallbackAttackerTransform.position.x;
        }

        if (Mathf.Abs(directionX) <= 0.01f)
        {
            directionX = transform.localScale.x >= 0f ? 1f : -1f;
        }

        return new Vector2(Mathf.Sign(directionX), 0f);
    }

    private void StartHitFlash()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
        }

        flashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private IEnumerator HitFlashRoutine()
    {
        isFlashing = true;

        float halfFlashDuration = Mathf.Max(
            0.01f,
            flashDuration / Mathf.Max(1, flashCount * 2)
        );

        for (int i = 0; i < flashCount; i++)
        {
            isFlashing = true;
            yield return new WaitForSeconds(halfFlashDuration);

            isFlashing = false;
            RestoreOriginalColorsPreserveAlpha();
            yield return new WaitForSeconds(halfFlashDuration);
        }

        isFlashing = false;
        RestoreOriginalColorsPreserveAlpha();
        flashCoroutine = null;
    }

    private void LateUpdate()
    {
        // WaterEnemyVisibilityControllerがUpdateで透明度を変える構成でも、
        // LateUpdateで最後に赤色を重ねるため被弾フラッシュが見えます。
        if (isFlashing)
        {
            ApplyFlashColorPreserveAlpha();
        }
    }

    private void ApplyFlashColorPreserveAlpha()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> pair in originalColors)
        {
            SpriteRenderer spriteRenderer = pair.Key;

            if (spriteRenderer == null)
            {
                continue;
            }

            Color baseColor = pair.Value;
            Color currentColor = spriteRenderer.color;
            Color flash = Color.Lerp(
                baseColor,
                flashColor,
                flashIntensity
            );

            flash.a = currentColor.a;
            spriteRenderer.color = flash;
        }
    }

    private void RestoreOriginalColorsPreserveAlpha()
    {
        foreach (KeyValuePair<SpriteRenderer, Color> pair in originalColors)
        {
            SpriteRenderer spriteRenderer = pair.Key;

            if (spriteRenderer == null)
            {
                continue;
            }

            Color restored = pair.Value;
            restored.a = spriteRenderer.color.a;
            spriteRenderer.color = restored;
        }
    }

    private void StopFlashAndRestore()
    {
        if (flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
        }

        isFlashing = false;
        RestoreOriginalColorsPreserveAlpha();
    }

    private void SubscribeHealth()
    {
        if (health == null)
        {
            return;
        }

        health.HealthChanged -= HandleHealthChanged;
        health.HealthChanged += HandleHealthChanged;
    }

    private void UnsubscribeHealth()
    {
        if (health != null)
        {
            health.HealthChanged -= HandleHealthChanged;
        }
    }

    private void CacheCurrentHealth()
    {
        if (health == null)
        {
            return;
        }

        lastHealth = health.CurrentHealth;
        hasCachedHealth = true;
    }

    private void CacheSpriteRenderers()
    {
        if (spriteRenderers == null || spriteRenderers.Length == 0)
        {
            spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        }

        foreach (SpriteRenderer spriteRenderer in spriteRenderers)
        {
            if (spriteRenderer == null ||
                originalColors.ContainsKey(spriteRenderer))
            {
                continue;
            }

            originalColors.Add(spriteRenderer, spriteRenderer.color);
        }
    }

    private void FindReferences()
    {
        if (health == null)
        {
            health = GetComponent<CharacterHealth>();
        }

        if (enemyRigidbody == null)
        {
            enemyRigidbody = GetComponent<Rigidbody2D>();
        }

        if (enemyChaser == null)
        {
            enemyChaser = GetComponent<EnemyChaser2D>();
        }

        if (fallbackAttackerTransform == null)
        {
            PlayerMove playerMove = FindAnyObjectByType<PlayerMove>();

            if (playerMove != null)
            {
                fallbackAttackerTransform = playerMove.transform;
            }
        }
    }

    private void OnValidate()
    {
        knockbackSpeed = Mathf.Max(0f, knockbackSpeed);
        upwardKnockbackSpeed = Mathf.Max(0f, upwardKnockbackSpeed);
        hitStunDuration = Mathf.Max(0f, hitStunDuration);
        flashDuration = Mathf.Max(0.01f, flashDuration);
        flashCount = Mathf.Max(1, flashCount);
        flashIntensity = Mathf.Clamp01(flashIntensity);
    }
}
