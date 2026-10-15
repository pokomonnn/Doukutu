using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 近接武器Prefabへ追加。既存WeaponAimの照準と操作ロックを共有します。
/// PlayerEquipmentVisualControllerのCurrentWeaponItemから、装備された
/// MeleeWeaponItemDataと耐久度を参照します。
/// ※ GunShooter は近接武器Prefabに付けません。
/// </summary>
[DisallowMultipleComponent]
public class PlayerMeleeWeapon2D : MonoBehaviour
{
    [Header("攻撃の起点と演出")]
    [Tooltip("未設定ならこのTransformを攻撃起点にします。通常は銃のMuzzleと同様の位置です。")]
    [SerializeField] private Transform attackOrigin;
    [Tooltip("回転して斬撃を表現する子Transform。WeaponAimが付いたRootを指定しないでください。")]
    [SerializeField] private Transform swingVisual;
    [SerializeField] private Animator swingAnimator;
    [SerializeField] private string animationTriggerName = "MeleeSwing";

    [Header("命中判定")]
    [Tooltip("敵に設定したLayerのみチェックすることを推奨。未設定(Everything)だと余分なColliderも検索します。")]
    [SerializeField] private LayerMask targetLayers = ~0;
    [Tooltip("救出NPCなどへの誤攻撃を防ぎます。ONならNPCも攻撃対象にできます。")]
    [SerializeField] private bool allowHitCarryableNpc = false;
    [SerializeField] private bool allowAttackInProne = true;

    [Header("効果音（武器Prefabで設定可能）")]
    [Tooltip("攻撃音を鳴らすAudioSource。未設定・未配置の場合は自動作成します。")]
    [SerializeField] private AudioSource audioSource;

    [Tooltip("空振りでも鳴る攻撃音。未設定ならMeleeWeaponItemDataのSwing Soundを使用します。")]
    [SerializeField] private AudioClip attackSoundOverride;
    [Tooltip("敵のHPが実際に減ったときに鳴る音。未設定ならItemDataのHit Soundを使用します。")]
    [SerializeField] private AudioClip hitSoundOverride;
    [Tooltip("武器の耐久度が0で攻撃できないときの音。未設定ならItemDataのBroken Soundを使用します。")]
    [SerializeField] private AudioClip brokenSoundOverride;

    [Tooltip("攻撃音の大きさ（0=無音、1=最大）。")]
    [SerializeField, Range(0f, 1f)] private float attackSoundVolume = 1f;
    [Tooltip("命中音の大きさ（0=無音、1=最大）。")]
    [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 1f;
    [Tooltip("破損音の大きさ（0=無音、1=最大）。")]
    [SerializeField, Range(0f, 1f)] private float brokenSoundVolume = 1f;

    [Tooltip("ON：一振りで複数の敵に命中してもヒット音を一度だけ鳴らします。")]
    [SerializeField] private bool hitSoundOncePerSwing = true;
    [Tooltip("横スクロールゲーム向け。ONならカメラ位置に左右されない2D音声として再生します。")]
    [SerializeField] private bool use2DAudio = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = false;

    private WeaponAim weaponAim;
    private PlayerEquipmentVisualController equipmentVisual;
    private PlayerWeightController playerWeight;
    private PlayerCarryController2D carryController;
    private Transform playerRoot;
    private float nextAttackTime;
    private bool attacking;
    private bool hitSoundPlayedThisSwing;
    private float visualBaseZ;
    private readonly HashSet<CharacterHealth> hitThisSwing = new HashSet<CharacterHealth>();

    public bool IsAttacking => attacking;

    private void Awake()
    {
        weaponAim = GetComponent<WeaponAim>();
        if (weaponAim == null)
            weaponAim = GetComponentInParent<WeaponAim>();
        EnsureAudioSource();
        if (attackOrigin == null)
            attackOrigin = transform;
        if (swingVisual != null)
            visualBaseZ = swingVisual.localEulerAngles.z;
        FindContext();
    }

    private void Start()
    {
        FindContext();
        if (weaponAim == null)
            Debug.LogWarning("[MeleeWeapon] WeaponAimがありません。近接武器PrefabのRootに追加してください。", this);
    }

    private void Update()
    {
        if (!TryGetCurrentEquipment(out MeleeWeaponItemData data, out InventoryItem item))
            return;

        if (Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Time.time >= nextAttackTime && !attacking &&
            CanAttack(data, item))
        {
            nextAttackTime = Time.time + Mathf.Max(data.AttackInterval, data.SwingDuration);
            StartCoroutine(SwingRoutine(data, item));
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        attacking = false;
        hitSoundPlayedThisSwing = false;
        hitThisSwing.Clear();
        SetSwingAngle(0f);
    }

    private bool CanAttack(MeleeWeaponItemData data, InventoryItem item)
    {
        if (data == null || item == null ||
            weaponAim == null || !weaponAim.CanUseEquippedWeapon)
            return false;

        if (playerWeight != null && playerWeight.IsUsingConsumable)
            return false;

        if (carryController != null && carryController.IsCarrying &&
            !data.UsableWhileCarryingNpc)
            return false;

        if (!allowAttackInProne && playerRoot != null)
        {
            PlayerProneController prone = playerRoot.GetComponent<PlayerProneController>();
            if (prone != null && prone.IsProne)
                return false;
        }

        if (item.StoredWeaponDurability <= 0f)
        {
            Play(SelectSound(brokenSoundOverride, data.BrokenSound), brokenSoundVolume);
            Log("武器が破損しているため攻撃できません。");
            return false;
        }
        return true;
    }

    private IEnumerator SwingRoutine(MeleeWeaponItemData data, InventoryItem equippedItem)
    {
        attacking = true;
        hitThisSwing.Clear();
        hitSoundPlayedThisSwing = false;
        Play(SelectSound(attackSoundOverride, data.SwingSound), attackSoundVolume);
        if (swingAnimator != null && !string.IsNullOrWhiteSpace(animationTriggerName))
            swingAnimator.SetTrigger(animationTriggerName);

        float duration = data.SwingDuration;
        float startedAt = Time.time;
        // 操作方向を攻撃開始時に固定する。武器画像の向きはWeaponAimが追従する。
        Vector2 attackDirection = transform.right;
        if (attackDirection.sqrMagnitude < 0.0001f)
            attackDirection = Vector2.right;
        attackDirection.Normalize();
        float endHit = Mathf.Min(duration, data.HitStartDelay + data.HitWindowSeconds);
        bool damagedAny = false;

        while (Time.time - startedAt < duration)
        {
            float elapsed = Time.time - startedAt;
            float normalizedTime = Mathf.Clamp01(elapsed / duration);
            // S字のなめらかな振り。アニメーションがある場合はSwing Visualを未設定にしてよい。
            float swung = Mathf.SmoothStep(-0.5f, 0.5f, normalizedTime) * data.VisualSwingArc;
            if (weaponAim != null && weaponAim.IsAimingLeft)
                swung = -swung;
            SetSwingAngle(swung);

            if (elapsed >= data.HitStartDelay && elapsed <= endHit &&
                equippedItem != null &&
                equipmentVisual != null && equipmentVisual.CurrentWeaponItem == equippedItem &&
                weaponAim != null && weaponAim.CanUseEquippedWeapon)
            {
                if (ApplyHitFrame(data, attackDirection))
                    damagedAny = true;
            }
            yield return null;
        }

        SetSwingAngle(0f);
        if (equippedItem != null && equipmentVisual != null &&
            equipmentVisual.CurrentWeaponItem == equippedItem &&
            damagedAny)
        {
            // 1回の攻撃で命中が何体あっても耐久消耗は1回。
            float durabilityBefore = equippedItem.StoredWeaponDurability;
            equippedItem.SetStoredWeaponDurability(
                durabilityBefore - data.DurabilityLossPerAttack);
            if (durabilityBefore > 0f && equippedItem.StoredWeaponDurability <= 0f)
            {
                GameEventLogManager2D.WeaponBroken(data.DisplayName);
            }
            Log($"耐久度 {equippedItem.StoredWeaponDurability:0.0}/{data.MaxDurability:0.0}");
        }
        attacking = false;
    }

    private bool ApplyHitFrame(MeleeWeaponItemData data, Vector2 direction)
    {
        Vector2 origin = attackOrigin != null ? (Vector2)attackOrigin.position : (Vector2)transform.position;
        Collider2D[] colliders = Physics2D.OverlapCircleAll(origin, data.AttackReach, targetLayers);
        bool landed = false;
        float threshold = Mathf.Cos(data.AttackArcDegrees * 0.5f * Mathf.Deg2Rad);

        foreach (Collider2D hit in colliders)
        {
            if (hit == null) continue;
            CharacterHealth health = hit.GetComponentInParent<CharacterHealth>();
            if (health == null || health.IsDead || hitThisSwing.Contains(health))
                continue;
            if (health.gameObject.CompareTag("Player") ||
                (playerRoot != null && health.transform.IsChildOf(playerRoot)))
                continue;
            if (!allowHitCarryableNpc && health.GetComponentInParent<CarryableObject2D>() != null)
                continue;

            Vector2 nearest = hit.ClosestPoint(origin);
            Vector2 toTarget = nearest - origin;
            if (toTarget.sqrMagnitude < 0.0001f)
                toTarget = (Vector2)hit.bounds.center - origin;
            if (toTarget.sqrMagnitude < 0.0001f ||
                Vector2.Dot(toTarget.normalized, direction) < threshold)
                continue;

            // 同一スイング中は1体につき1回。対象の無敵時間はCharacterHealth側が管理。
            hitThisSwing.Add(health);
            if (health.IsInvincible)
                continue;

            EnemyHitReaction2D reaction = health.GetComponent<EnemyHitReaction2D>();
            if (reaction != null)
                reaction.NotifyHitSource(origin);
            int damage = Mathf.Max(1, Mathf.RoundToInt(
                data.AttackDamage * SkillCardEffectUtility.GetMultiplier(SkillEffectType.WeaponDamage)));
            health.TakeDamage(damage, 0, nearest);
            landed = true;
            Log($"命中: {health.name} / Damage={damage}");
        }
        if (landed && (!hitSoundOncePerSwing || !hitSoundPlayedThisSwing))
        {
            Play(SelectSound(hitSoundOverride, data.HitSound), hitSoundVolume);
            hitSoundPlayedThisSwing = true;
        }
        return landed;
    }

    private void SetSwingAngle(float angle)
    {
        if (swingVisual != null)
            swingVisual.localRotation = Quaternion.Euler(0f, 0f, visualBaseZ + angle);
    }

    /// <summary>Prefabで音を指定していなければ武器ItemDataの音を使います。</summary>
    private static AudioClip SelectSound(AudioClip prefabClip, AudioClip dataClip)
    {
        return prefabClip != null ? prefabClip : dataClip;
    }

    /// <summary>
    /// 以前からあるPrefabにも対応：AudioSourceが無い場合は自動で追加します。
    /// </summary>
    private void EnsureAudioSource()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        if (use2DAudio)
            audioSource.spatialBlend = 0f;
    }

    private void Play(AudioClip clip, float volume)
    {
        if (clip == null || volume <= 0f)
            return;
        if (audioSource == null)
            EnsureAudioSource();
        if (audioSource != null && audioSource.isActiveAndEnabled)
            audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }

    private bool TryGetCurrentEquipment(out MeleeWeaponItemData data, out InventoryItem item)
    {
        data = null;
        item = null;
        if (equipmentVisual == null) FindContext();
        if (equipmentVisual == null || equipmentVisual.CurrentWeaponAim != weaponAim)
            return false;
        item = equipmentVisual.CurrentWeaponItem;
        data = item != null ? item.ItemData as MeleeWeaponItemData : null;
        return data != null;
    }

    private void FindContext()
    {
        if (equipmentVisual == null)
            equipmentVisual = GetComponentInParent<PlayerEquipmentVisualController>();
        if (equipmentVisual == null)
            return;
        playerRoot = equipmentVisual.transform;
        if (playerWeight == null)
            playerWeight = equipmentVisual.GetComponent<PlayerWeightController>();
        if (carryController == null)
            carryController = equipmentVisual.GetComponent<PlayerCarryController2D>();
    }

    private void Log(string text)
    {
        if (showDebugLogs)
            Debug.Log("[MeleeWeapon] " + text, this);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 origin = attackOrigin != null ? attackOrigin.position : transform.position;
        Gizmos.color = Color.yellow;
        float reach = 1.7f;
        Gizmos.DrawWireSphere(origin, reach);
    }
}
