using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CharacterHealth))]
public class PlayerDeathHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CharacterHealth health;
    [SerializeField] private Rigidbody2D rb;
    [SerializeField] private Animator animator;

    [Header("装備・銃操作")]
    [Tooltip("Playerに付いているPlayerEquipmentVisualController")]
    [SerializeField]
    private PlayerEquipmentVisualController equipmentVisualController;

    [Header("Death Settings")]
    [Tooltip("死亡時に止めたいスクリプトを入れる（移動など）")]
    [SerializeField] private Behaviour[] scriptsToDisableOnDeath;

    [Tooltip("死亡時にRigidbody2Dの物理演算を止める")]
    [SerializeField] private bool disablePhysicsOnDeath = true;

    [Tooltip("Animatorに死亡用Triggerがある時だけ名前を入れる。例：Die")]
    [SerializeField] private string deathTriggerName = "";

    [Header("Events")]
    [Tooltip("ここにGameManagerの死亡処理を登録する")]
    [SerializeField] private UnityEvent onPlayerDied;

    public bool IsDead { get; private set; }

    private readonly List<Behaviour> disabledScripts = new();
    private bool physicsWasSimulated;

    private void Awake()
    {
        if (health == null)
        {
            health = GetComponent<CharacterHealth>();
        }

        if (rb == null)
        {
            rb = GetComponent<Rigidbody2D>();
        }

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        if (equipmentVisualController == null)
        {
            equipmentVisualController =
                GetComponent<PlayerEquipmentVisualController>();
        }
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.Died += HandleDeath;
        }
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= HandleDeath;
        }
    }

    private void HandleDeath()
    {
        if (IsDead)
        {
            return;
        }

        IsDead = true;

        // WeaponHolder内に生成されている銃の
        // 射撃・リロード・照準操作を止める
        equipmentVisualController?.SetWeaponControlsEnabled(false);

        StopPlayerScripts();
        StopPhysics();
        PlayDeathAnimation();

        onPlayerDied?.Invoke();
    }

    private void StopPlayerScripts()
    {
        disabledScripts.Clear();

        foreach (Behaviour script in scriptsToDisableOnDeath)
        {
            if (script == null || script == this)
            {
                continue;
            }

            if (script.enabled)
            {
                script.enabled = false;
                disabledScripts.Add(script);
            }
        }
    }

    private void StopPhysics()
    {
        if (rb == null)
        {
            return;
        }

        rb.linearVelocity = Vector2.zero;
        rb.angularVelocity = 0f;

        physicsWasSimulated = rb.simulated;

        if (disablePhysicsOnDeath)
        {
            rb.simulated = false;
        }
    }

    private void PlayDeathAnimation()
    {
        if (animator != null &&
            !string.IsNullOrEmpty(deathTriggerName))
        {
            animator.SetTrigger(deathTriggerName);
        }
    }

    public void ReviveAt(Vector3 respawnPosition)
    {
        transform.position = respawnPosition;
        Revive();
    }

    public void Revive()
    {
        if (!IsDead)
        {
            return;
        }

        IsDead = false;

        if (rb != null)
        {
            rb.simulated = physicsWasSimulated;
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        foreach (Behaviour script in disabledScripts)
        {
            if (script != null)
            {
                script.enabled = true;
            }
        }

        disabledScripts.Clear();

        // 復活時、装備している銃だけ再び操作可能にする
        equipmentVisualController?.SetWeaponControlsEnabled(true);

        health.ResetHealth();
    }
}