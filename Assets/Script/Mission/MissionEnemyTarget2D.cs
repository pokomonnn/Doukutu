using System;
using UnityEngine;

/// <summary>
/// 「指定された敵を倒す」ミッションの対象Enemyへ付けます。
/// CharacterHealth.Diedを監視し、死亡時に全MissionManager2Dへ通知します。
///
/// 新方式：
/// MissionTargetIdentity2Dを同じPrefab（親/子でも可）へ付け、
/// Target Type = Enemy、Target Id = enemy_xxx のように設定します。
///
/// 旧方式：
/// MissionManager2DのMission EntryへTarget Enemyを直接割り当てる方式も互換維持します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class MissionEnemyTarget2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら同じObjectのCharacterHealthを使います")]
    [SerializeField] private CharacterHealth characterHealth;

    [Tooltip(
        "EnemyのミッションIDを持つMissionTargetIdentity2Dです。" +
        "未設定なら自分・子・親から自動取得します。"
    )]
    [SerializeField] private MissionTargetIdentity2D targetIdentity;

    [Tooltip("コンパスが指す位置。空欄ならこのEnemyの位置を使います")]
    [SerializeField] private Transform compassAnchor;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public bool IsDefeated => isDefeated;

    public MissionTargetIdentity2D TargetIdentity => targetIdentity;

    public string TargetId =>
        targetIdentity != null &&
        targetIdentity.TargetType == MissionTargetType2D.Enemy
            ? targetIdentity.TargetId
            : string.Empty;

    public string TargetDisplayName =>
        targetIdentity != null
            ? targetIdentity.DisplayName
            : gameObject.name;

    public Transform CompassAnchor => compassAnchor != null
        ? compassAnchor
        : transform;

    /// <summary>
    /// 旧方式との互換用。特定インスタンスを直接監視する外部コードでも利用できます。
    /// </summary>
    public event Action<MissionEnemyTarget2D> Defeated;

    /// <summary>
    /// Scene内のMissionEnemyTarget2Dが倒された時に共通で発火します。
    /// ランダム生成EnemyでもMissionManager2DがIDで判定できます。
    /// </summary>
    public static event Action<MissionEnemyTarget2D> AnyDefeated;

    private bool isDefeated;
    private bool isSubscribed;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeEvents();

        if (characterHealth != null && characterHealth.IsDead)
        {
            HandleDied();
        }
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || characterHealth == null)
        {
            return;
        }

        characterHealth.Died += HandleDied;
        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || characterHealth == null)
        {
            return;
        }

        characterHealth.Died -= HandleDied;
        isSubscribed = false;
    }

    private void HandleDied()
    {
        if (isDefeated)
        {
            return;
        }

        isDefeated = true;

        if (showDebugLogs)
        {
            string id = string.IsNullOrWhiteSpace(TargetId)
                ? "ID未設定（旧Target Enemy直接参照のみ）"
                : TargetId;

            Debug.Log(
                $"[MissionEnemyTarget2D] {TargetDisplayName}: 討伐されました。TargetId={id}",
                this
            );
        }

        Defeated?.Invoke(this);
        AnyDefeated?.Invoke(this);
    }

    private void FindReferences()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }

        if (targetIdentity == null)
        {
            targetIdentity = GetComponent<MissionTargetIdentity2D>();
        }

        if (targetIdentity == null)
        {
            targetIdentity =
                GetComponentInChildren<MissionTargetIdentity2D>(true);
        }

        if (targetIdentity == null)
        {
            targetIdentity =
                GetComponentInParent<MissionTargetIdentity2D>();
        }
    }

    private void OnValidate()
    {
        FindReferences();
    }
}
