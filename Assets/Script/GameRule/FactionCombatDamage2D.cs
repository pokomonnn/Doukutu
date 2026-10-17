using UnityEngine;

/// <summary>
/// NPCからNPCへの攻撃元を通知し、既存CharacterHealthへダメージを渡す。
/// プレイヤーの銃撃など別の攻撃系から呼ぶ場合も、このメソッドを利用できる。
/// </summary>
public static class FactionCombatDamage2D
{
    public static bool DealDamage(CharacterHealth victim, int amount, Transform attacker)
    {
        if (victim == null || victim.IsDead || amount <= 0) return false;
        if (attacker != null && (victim.transform == attacker ||
                                  attacker.IsChildOf(victim.transform))) return false;

        // 被害者が反撃できるAIを持っていれば攻撃者を教える。
        FactionEnemyTargeting2D seeker = victim.GetComponent<FactionEnemyTargeting2D>();
        if (seeker == null) seeker = victim.GetComponentInParent<FactionEnemyTargeting2D>();
        if (seeker != null && seeker.isActiveAndEnabled)
            seeker.NotifyAttackedBy(attacker);

        victim.TakeDamage(amount);
        return true;
    }
}
