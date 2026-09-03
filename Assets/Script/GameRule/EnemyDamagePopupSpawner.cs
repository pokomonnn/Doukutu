using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// EnemyのCharacterHealth.Damagedを監視し、命中位置へダメージ数字を生成します。
/// Damage Group IDが同じショットガンPelletは短時間だけ1つの数字へ合算できます。
/// </summary>
public class EnemyDamagePopupSpawner : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定ならこのGameObject/親から自動取得します。")]
    [SerializeField] private CharacterHealth characterHealth;

    [Tooltip("DamagePopupUIが付いたPrefabを設定します。")]
    [SerializeField] private DamagePopupUI popupPrefab;

    [Tooltip(
        "通常は未設定でOKです。未設定ならScene Rootへ生成するため、" +
        "Enemyの左右反転や死亡時Disableの影響を受けにくくなります。"
    )]
    [SerializeField] private Transform popupParent;

    [Header("生成位置")]
    [SerializeField] private Vector3 worldOffset =
        new Vector3(0f, 0.25f, 0f);

    [Tooltip("数字が完全に重ならないようにするランダム幅です。")]
    [SerializeField] private Vector2 randomOffset =
        new Vector2(0.12f, 0.08f);

    [Header("ショットガン表示")]
    [Tooltip("同じDamage GroupのPelletを1つのダメージ数字へ合算します。")]
    [SerializeField] private bool combineSameDamageGroup = true;

    [Tooltip("同じショットとして数字を合算する猶予時間です。")]
    [SerializeField, Min(0f)] private float combineWindow = 0.12f;

    private readonly Dictionary<int, DamageGroupPopup> groupPopups = new();
    private readonly List<int> cleanupKeys = new();

    private class DamageGroupPopup
    {
        public DamagePopupUI Popup;
        public int TotalDamage;
        public float LastHitTime;
    }

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
        groupPopups.Clear();
    }

    private void FindReferences()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (characterHealth == null)
        {
            characterHealth = GetComponentInParent<CharacterHealth>();
        }
    }

    private void Subscribe()
    {
        if (characterHealth == null)
        {
            return;
        }

        characterHealth.Damaged -= HandleDamaged;
        characterHealth.Damaged += HandleDamaged;
    }

    private void Unsubscribe()
    {
        if (characterHealth == null)
        {
            return;
        }

        characterHealth.Damaged -= HandleDamaged;
    }

    private void HandleDamaged(
        int actualDamage,
        int damageGroupId,
        Vector3 hitWorldPosition)
    {
        if (actualDamage <= 0 || popupPrefab == null)
        {
            return;
        }

        CleanupExpiredGroups();

        if (combineSameDamageGroup &&
            damageGroupId != 0 &&
            TryCombineDamage(
                actualDamage,
                damageGroupId
            ))
        {
            return;
        }

        Vector3 spawnPosition =
            hitWorldPosition +
            worldOffset +
            new Vector3(
                Random.Range(-Mathf.Abs(randomOffset.x), Mathf.Abs(randomOffset.x)),
                Random.Range(-Mathf.Abs(randomOffset.y), Mathf.Abs(randomOffset.y)),
                0f
            );

        DamagePopupUI popup = Instantiate(
            popupPrefab,
            spawnPosition,
            Quaternion.identity,
            popupParent
        );

        popup.Initialize(actualDamage);

        if (combineSameDamageGroup && damageGroupId != 0)
        {
            groupPopups[damageGroupId] = new DamageGroupPopup
            {
                Popup = popup,
                TotalDamage = actualDamage,
                LastHitTime = Time.time
            };
        }
    }

    private bool TryCombineDamage(
        int actualDamage,
        int damageGroupId)
    {
        if (!groupPopups.TryGetValue(
                damageGroupId,
                out DamageGroupPopup existing))
        {
            return false;
        }

        if (existing == null ||
            existing.Popup == null ||
            Time.time - existing.LastHitTime > combineWindow)
        {
            groupPopups.Remove(damageGroupId);
            return false;
        }

        existing.TotalDamage += actualDamage;
        existing.LastHitTime = Time.time;
        existing.Popup.SetDamage(existing.TotalDamage);
        return true;
    }

    private void CleanupExpiredGroups()
    {
        if (groupPopups.Count == 0)
        {
            return;
        }

        cleanupKeys.Clear();

        foreach (KeyValuePair<int, DamageGroupPopup> pair in groupPopups)
        {
            DamageGroupPopup entry = pair.Value;

            if (entry == null ||
                entry.Popup == null ||
                Time.time - entry.LastHitTime > combineWindow)
            {
                cleanupKeys.Add(pair.Key);
            }
        }

        foreach (int key in cleanupKeys)
        {
            groupPopups.Remove(key);
        }
    }

    private void OnValidate()
    {
        combineWindow = Mathf.Max(0f, combineWindow);
        randomOffset.x = Mathf.Abs(randomOffset.x);
        randomOffset.y = Mathf.Abs(randomOffset.y);
    }
}
