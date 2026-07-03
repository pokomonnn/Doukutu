using System;
using UnityEngine;

/// <summary>
/// 「指定された敵を倒す」ミッションの対象に付けます。
/// CharacterHealth.Diedを監視し、MissionManager2Dへ討伐完了を知らせます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(CharacterHealth))]
public class MissionEnemyTarget2D : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら同じObjectのCharacterHealthを使います")]
    [SerializeField] private CharacterHealth characterHealth;

    [Tooltip("コンパスが指す位置。空欄ならこのEnemyの位置を使います")]
    [SerializeField] private Transform compassAnchor;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    public bool IsDefeated => isDefeated;

    public Transform CompassAnchor => compassAnchor != null
        ? compassAnchor
        : transform;

    public event Action<MissionEnemyTarget2D> Defeated;

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
            Debug.Log(
                $"[MissionEnemyTarget2D] {name}: 討伐対象が倒されました。",
                this
            );
        }

        Defeated?.Invoke(this);
    }

    private void FindReferences()
    {
        if (characterHealth == null)
        {
            characterHealth = GetComponent<CharacterHealth>();
        }
    }
}
