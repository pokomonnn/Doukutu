using System.Collections;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 敵の死亡時に、停止・死亡アニメーション・死亡エフェクト・死亡SE・
/// 少し後ろへのスライド・アイテムドロップをまとめて処理します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class EnemyDeathHandler2D : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CharacterHealth health;
    [SerializeField] private Rigidbody2D enemyRigidbody;

    [Tooltip("未設定なら、この敵の全Collider2Dを自動取得します")]
    [SerializeField] private Collider2D[] collidersToDisable;

    [Tooltip("未設定なら子オブジェクトから自動取得します")]
    [SerializeField] private Animator animator;

    [Tooltip("未設定なら同じObjectから自動取得します")]
    [SerializeField] private EnemyChaser2D enemyChaser;

    [Tooltip("未設定なら同じObjectから自動取得します")]
    [SerializeField] private EnemyHitReaction2D hitReaction;

    [Tooltip("未設定なら同じObjectから自動取得します")]
    [SerializeField] private EnemyDropTable2D dropTable;

    [Tooltip("未設定なら同じObjectから自動取得します")]
    [SerializeField] private EnemyRangedAttack2D rangedAttack;

    [Tooltip("未設定なら同じObjectから自動取得します")]
    [SerializeField] private EnemySanityAura2D sanityAura;

    [Tooltip("EnemyChaser2D以外にも、死亡時に止めたいBehaviourがあれば設定します")]
    [SerializeField] private Behaviour[] additionalBehavioursToDisable;

    [Header("死亡アニメーション")]
    [Tooltip("Animatorに死亡用Triggerがある場合だけ設定します。例：Die")]
    [SerializeField] private string deathTriggerName = "";

    [Header("死亡時の少しの吹き飛び")]
    [Tooltip("オンなら、死亡時にプレイヤーと反対方向へ少しだけ滑ります。Colliderは即座に無効化するため、床や壁に引っかかりません")]
    [SerializeField] private bool enableDeathSlide = true;

    [SerializeField, Min(0f)] private float deathSlideDistance = 0.28f;

    [SerializeField, Min(0.01f)] private float deathSlideDuration = 0.14f;

    [Tooltip("死亡スライドの変化。左下から右上へ進むほど、最初が速く後半がゆっくりになります")]
    [SerializeField]
    private AnimationCurve deathSlideCurve =
        AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("死亡エフェクト")]
    [Tooltip("血しぶき・煙・消滅エフェクトなどのPrefab。不要なら空欄でOK")]
    [SerializeField] private GameObject deathEffectPrefab;

    [SerializeField] private Vector3 deathEffectOffset;

    [Tooltip("生成した死亡エフェクトを消すまでの秒数")]
    [SerializeField, Min(0.05f)] private float deathEffectDestroyDelay = 2f;

    [Header("死亡サウンド")]
    [SerializeField] private AudioClip deathSound;

    [SerializeField, Range(0f, 1f)] private float deathSoundVolume = 0.9f;

    [SerializeField, Range(0f, 1f)] private float deathSoundSpatialBlend = 0f;

    [Header("削除設定")]
    [Tooltip("オンなら死亡演出後に敵Objectを削除します")]
    [SerializeField] private bool destroyAfterDeath = true;

    [Tooltip("敵Objectを削除するまでの秒数。死亡スライド時間より短い場合は、自動でスライド完了後まで待ちます")]
    [SerializeField, Min(0f)] private float destroyDelay = 1.2f;

    [Header("イベント")]
    [SerializeField] private UnityEvent onEnemyDied;

    [Header("デバッグ")]
    [SerializeField] private bool showDeathLogs;

    public bool IsHandlingDeath => isHandlingDeath;

    private bool isHandlingDeath;
    private Coroutine deathSlideCoroutine;

    private void Awake()
    {
        FindReferences();
        CacheColliders();
    }

    private void OnEnable()
    {
        isHandlingDeath = false;
        FindReferences();
        CacheColliders();

        if (health != null)
        {
            health.Died -= HandleDeath;
            health.Died += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDeath;
        }

        if (deathSlideCoroutine != null)
        {
            StopCoroutine(deathSlideCoroutine);
            deathSlideCoroutine = null;
        }
    }

    private void HandleDeath()
    {
        if (isHandlingDeath)
        {
            return;
        }

        isHandlingDeath = true;

        DisableEnemyBehaviours();
        DisableEnemyColliders();

        // ドロップは敵Objectを消す前に生成します。
        dropTable?.SpawnDrops();

        PlayDeathAnimation();
        SpawnDeathEffect();
        PlayDeathSound();
        StartDeathSlide();

        onEnemyDied?.Invoke();

        if (showDeathLogs)
        {
            Debug.Log(
                $"[EnemyDeathHandler2D] {name}: 死亡処理を開始しました。",
                this
            );
        }

        if (destroyAfterDeath)
        {
            float finalDelay = enableDeathSlide
                ? Mathf.Max(destroyDelay, deathSlideDuration)
                : destroyDelay;

            Destroy(gameObject, finalDelay);
        }
    }

    private void DisableEnemyBehaviours()
    {
        if (enemyChaser != null)
        {
            enemyChaser.enabled = false;
        }

        if (hitReaction != null)
        {
            hitReaction.enabled = false;
        }

        if (rangedAttack != null)
        {
            rangedAttack.enabled = false;
        }

        if (sanityAura != null)
        {
            sanityAura.enabled = false;
        }

        foreach (Behaviour behaviour in additionalBehavioursToDisable)
        {
            if (behaviour != null && behaviour != this)
            {
                behaviour.enabled = false;
            }
        }
    }

    private void DisableEnemyColliders()
    {
        foreach (Collider2D targetCollider in collidersToDisable)
        {
            if (targetCollider != null)
            {
                targetCollider.enabled = false;
            }
        }
    }

    private void StartDeathSlide()
    {
        if (!enableDeathSlide ||
            deathSlideDistance <= 0.001f ||
            deathSlideDuration <= 0.001f)
        {
            StopPhysics();
            return;
        }

        if (deathSlideCoroutine != null)
        {
            StopCoroutine(deathSlideCoroutine);
        }

        deathSlideCoroutine = StartCoroutine(
            DeathSlideRoutine(GetDeathSlideDirection())
        );
    }

    private IEnumerator DeathSlideRoutine(float direction)
    {
        if (enemyRigidbody == null)
        {
            yield return SlideTransformRoutine(direction);
            deathSlideCoroutine = null;
            yield break;
        }

        Vector2 startPosition = enemyRigidbody.position;
        Vector2 endPosition = startPosition +
            Vector2.right * direction * deathSlideDistance;

        enemyRigidbody.simulated = true;
        enemyRigidbody.bodyType = RigidbodyType2D.Kinematic;
        enemyRigidbody.gravityScale = 0f;
        enemyRigidbody.linearVelocity = Vector2.zero;
        enemyRigidbody.angularVelocity = 0f;

        float elapsed = 0f;

        while (elapsed < deathSlideDuration)
        {
            elapsed += Time.fixedDeltaTime;

            float normalizedTime = Mathf.Clamp01(
                elapsed / deathSlideDuration
            );

            float curveValue = deathSlideCurve != null
                ? deathSlideCurve.Evaluate(normalizedTime)
                : normalizedTime;

            enemyRigidbody.MovePosition(
                Vector2.LerpUnclamped(
                    startPosition,
                    endPosition,
                    curveValue
                )
            );

            yield return new WaitForFixedUpdate();
        }

        enemyRigidbody.MovePosition(endPosition);
        StopPhysics();
        deathSlideCoroutine = null;
    }

    private IEnumerator SlideTransformRoutine(float direction)
    {
        Vector3 startPosition = transform.position;
        Vector3 endPosition = startPosition +
            Vector3.right * direction * deathSlideDistance;

        float elapsed = 0f;

        while (elapsed < deathSlideDuration)
        {
            elapsed += Time.deltaTime;

            float normalizedTime = Mathf.Clamp01(
                elapsed / deathSlideDuration
            );

            float curveValue = deathSlideCurve != null
                ? deathSlideCurve.Evaluate(normalizedTime)
                : normalizedTime;

            transform.position = Vector3.LerpUnclamped(
                startPosition,
                endPosition,
                curveValue
            );

            yield return null;
        }

        transform.position = endPosition;
    }

    private float GetDeathSlideDirection()
    {
        Transform playerTransform = enemyChaser != null
            ? enemyChaser.PlayerTransform
            : null;

        if (playerTransform == null)
        {
            PlayerMove playerMove = FindAnyObjectByType<PlayerMove>();
            playerTransform = playerMove != null
                ? playerMove.transform
                : null;
        }

        if (playerTransform != null)
        {
            float direction = transform.position.x -
                playerTransform.position.x;

            if (Mathf.Abs(direction) > 0.01f)
            {
                return Mathf.Sign(direction);
            }
        }

        return transform.localScale.x >= 0f ? 1f : -1f;
    }

    private void StopPhysics()
    {
        if (enemyRigidbody == null)
        {
            return;
        }

        enemyRigidbody.linearVelocity = Vector2.zero;
        enemyRigidbody.angularVelocity = 0f;
        enemyRigidbody.simulated = false;
    }

    private void PlayDeathAnimation()
    {
        if (animator != null &&
            !string.IsNullOrWhiteSpace(deathTriggerName))
        {
            animator.SetTrigger(deathTriggerName);
        }
    }

    private void SpawnDeathEffect()
    {
        if (deathEffectPrefab == null)
        {
            return;
        }

        GameObject effect = Instantiate(
            deathEffectPrefab,
            transform.position + deathEffectOffset,
            Quaternion.identity
        );

        Destroy(effect, deathEffectDestroyDelay);
    }

    private void PlayDeathSound()
    {
        if (deathSound == null)
        {
            return;
        }

        GameObject soundObject = new GameObject(
            $"OneShot_{deathSound.name}"
        );

        soundObject.transform.position = transform.position;

        AudioSource audioSource =
            soundObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.clip = deathSound;
        audioSource.volume = deathSoundVolume;
        audioSource.spatialBlend = deathSoundSpatialBlend;
        audioSource.Play();

        Destroy(
            soundObject,
            Mathf.Max(0.1f, deathSound.length)
        );
    }

    private void CacheColliders()
    {
        if (collidersToDisable == null ||
            collidersToDisable.Length == 0)
        {
            collidersToDisable =
                GetComponentsInChildren<Collider2D>(true);
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

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>(true);
        }

        if (enemyChaser == null)
        {
            enemyChaser = GetComponent<EnemyChaser2D>();
        }

        if (hitReaction == null)
        {
            hitReaction = GetComponent<EnemyHitReaction2D>();
        }

        if (dropTable == null)
        {
            dropTable = GetComponent<EnemyDropTable2D>();
        }

        if (rangedAttack == null)
        {
            rangedAttack = GetComponent<EnemyRangedAttack2D>();
        }

        if (sanityAura == null)
        {
            sanityAura = GetComponent<EnemySanityAura2D>();
        }
    }

    private void OnValidate()
    {
        deathSlideDistance = Mathf.Max(0f, deathSlideDistance);
        deathSlideDuration = Mathf.Max(0.01f, deathSlideDuration);
        deathEffectDestroyDelay = Mathf.Max(0.05f, deathEffectDestroyDelay);
        deathSoundVolume = Mathf.Clamp01(deathSoundVolume);
        deathSoundSpatialBlend = Mathf.Clamp01(deathSoundSpatialBlend);
        destroyDelay = Mathf.Max(0f, destroyDelay);

        if (deathSlideCurve == null)
        {
            deathSlideCurve = AnimationCurve.EaseInOut(
                0f,
                0f,
                1f,
                1f
            );
        }
    }
}