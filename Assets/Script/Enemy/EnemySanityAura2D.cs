using System;
using UnityEngine;

/// <summary>
/// 特殊Enemy用のSAN干渉オーラです。
/// プレイヤーが範囲内にいる間、一定間隔でSANを減少させます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class EnemySanityAura2D : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private CharacterHealth ownHealth;
    [SerializeField] private EnemyChaser2D enemyChaser;
    [SerializeField] private PlayerSanityController playerSanity;

    [Header("SAN干渉")]
    [SerializeField, Min(0f)] private float effectRadius = 5f;
    [SerializeField, Min(0f)] private float sanityDrainPerSecond = 4f;
    [SerializeField, Min(0.05f)] private float pulseInterval = 0.5f;

    [Tooltip("オンならEnemyChaser2DがPlayerを発見している時だけ発動します")]
    [SerializeField] private bool requireDetectedPlayer;

    [Header("射線")]
    [SerializeField] private bool requireLineOfSight;
    [SerializeField] private LayerMask obstacleLayers;

    [Header("デバッグ")]
    [SerializeField] private bool showRangeGizmo = true;
    [SerializeField] private bool showLogs;

    public event Action SpecialEffectPerformed;

    private float nextPulseTime;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        nextPulseTime = Time.time + pulseInterval;
    }

    private void Update()
    {
        FindReferences();

        if (Time.time < nextPulseTime)
        {
            return;
        }

        nextPulseTime = Time.time + pulseInterval;

        if (!CanApplyAura())
        {
            return;
        }

        float requestedDrain = sanityDrainPerSecond * pulseInterval;
        float actualDrain = playerSanity.DrainSanity(requestedDrain);

        if (actualDrain > 0f)
        {
            SpecialEffectPerformed?.Invoke();

            if (showLogs)
            {
                Debug.Log(
                    $"[EnemySanityAura2D] {name}: SAN -{actualDrain:0.##}",
                    this
                );
            }
        }
    }

    private bool CanApplyAura()
    {
        if (ownHealth == null || ownHealth.IsDead || playerSanity == null)
        {
            return false;
        }

        if (requireDetectedPlayer &&
            (enemyChaser == null || !enemyChaser.HasDetectedPlayer))
        {
            return false;
        }

        float distance = Vector2.Distance(
            transform.position,
            playerSanity.transform.position
        );

        if (distance > effectRadius)
        {
            return false;
        }

        return !requireLineOfSight || HasLineOfSight();
    }

    private bool HasLineOfSight()
    {
        if (playerSanity == null || obstacleLayers.value == 0)
        {
            return true;
        }

        Vector2 origin = transform.position;
        Vector2 target = playerSanity.transform.position;
        Vector2 delta = target - origin;
        float distance = delta.magnitude;

        if (distance <= 0.001f)
        {
            return true;
        }

        RaycastHit2D hit = Physics2D.Raycast(
            origin,
            delta / distance,
            distance,
            obstacleLayers
        );

        return hit.collider == null;
    }

    private void FindReferences()
    {
        if (ownHealth == null)
        {
            ownHealth = GetComponent<CharacterHealth>();
        }

        if (enemyChaser == null)
        {
            enemyChaser = GetComponent<EnemyChaser2D>();
        }

        if (playerSanity == null)
        {
            playerSanity = FindAnyObjectByType<PlayerSanityController>();
        }
    }

    private void OnValidate()
    {
        effectRadius = Mathf.Max(0f, effectRadius);
        sanityDrainPerSecond = Mathf.Max(0f, sanityDrainPerSecond);
        pulseInterval = Mathf.Max(0.05f, pulseInterval);
    }

    private void OnDrawGizmosSelected()
    {
        if (!showRangeGizmo)
        {
            return;
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, effectRadius);
    }
}
