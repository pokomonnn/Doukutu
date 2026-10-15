using UnityEngine;

/// <summary>
/// 近接武器の設定。WeaponItemDataを継承するため、既存PrimaryWeapon装備枠、
/// InventoryItemの耐久保存、シーン間セーブをそのまま利用できます。
/// Weapon PrefabにはWeaponAimとPlayerMeleeWeapon2Dを付けてください。
/// </summary>
[CreateAssetMenu(fileName = "NewMeleeWeaponItemData", menuName = "Inventory/Items/Melee Weapon Item Data")]
public class MeleeWeaponItemData : WeaponItemData
{
    [Header("近接攻撃")]
    [SerializeField, Min(1)] private int attackDamage = 18;
    [SerializeField, Min(0.2f)] private float attackReach = 1.7f;
    [SerializeField, Range(10f, 180f)] private float attackArcDegrees = 95f;
    [Tooltip("1回の攻撃動作に要する秒数")]
    [SerializeField, Min(0.08f)] private float swingDuration = 0.35f;
    [Tooltip("クリックから最初の当たり判定までの時間。Swing Durationより短くしてください。")]
    [SerializeField, Min(0f)] private float hitStartDelay = 0.11f;
    [Tooltip("当たり判定を継続する時間。敵1体につき1回のみダメージ")]
    [SerializeField, Min(0.01f)] private float hitWindowSeconds = 0.13f;
    [Tooltip("次の攻撃開始までの最短秒数")]
    [SerializeField, Min(0.1f)] private float attackInterval = 0.50f;

    [Header("運搬・耐久")]
    [Tooltip("片手武器ならON。OFFの場合はNPC運搬中に攻撃できません。")]
    [SerializeField] private bool usableWhileCarryingNpc = true;
    [SerializeField, Min(0f)] private float durabilityLossPerAttack = 0.15f;

    [Header("斬撃演出")]
    [Tooltip("攻撃中に武器画像を振る角度。Rootではなく子のSwingVisualを回します。")]
    [SerializeField, Range(0f, 175f)] private float visualSwingArc = 85f;
    [SerializeField] private AudioClip swingSound;
    [SerializeField] private AudioClip hitSound;
    [SerializeField] private AudioClip brokenSound;

    public int AttackDamage => Mathf.Max(1, attackDamage);
    public float AttackReach => Mathf.Max(0.2f, attackReach);
    public float AttackArcDegrees => Mathf.Clamp(attackArcDegrees, 10f, 180f);
    public float SwingDuration => Mathf.Max(0.08f, swingDuration);
    public float HitStartDelay => Mathf.Clamp(hitStartDelay, 0f, SwingDuration);
    public float HitWindowSeconds => Mathf.Max(0.01f, hitWindowSeconds);
    public float AttackInterval => Mathf.Max(0.1f, attackInterval);
    public bool UsableWhileCarryingNpc => usableWhileCarryingNpc;
    public float DurabilityLossPerAttack => Mathf.Max(0f, durabilityLossPerAttack);
    public float VisualSwingArc => Mathf.Clamp(visualSwingArc, 0f, 175f);
    public AudioClip SwingSound => swingSound;
    public AudioClip HitSound => hitSound;
    public AudioClip BrokenSound => brokenSound;

    protected override void OnValidate()
    {
        base.OnValidate();
        swingDuration = Mathf.Max(0.08f, swingDuration);
        hitStartDelay = Mathf.Clamp(hitStartDelay, 0f, swingDuration);
        hitWindowSeconds = Mathf.Max(0.01f, hitWindowSeconds);
        attackInterval = Mathf.Max(attackInterval, swingDuration);
    }
}
