using UnityEngine;

/// <summary>
/// DamageDealerの実ダメージ適用後から呼び出すヘルパー。
/// Playerが発射した弾のMarkerがある時だけ達成通知を送る。
/// </summary>
public static class CaveTutorialDamageHook2D
{
    public static void Report(DamageDealer dealer, CharacterHealth victim,
                              int damage, bool wasInvincible)
    {
        CaveTutorialManager2D tutorial = CaveTutorialManager2D.Instance;
        if (tutorial == null || dealer == null || victim == null || damage <= 0 || wasInvincible)
            return;
        if (dealer.GetComponentInParent<CaveTutorialPlayerProjectile2D>() == null)
            return;
        tutorial.NotifyEnemyHit(victim.gameObject);
    }
}
