using UnityEngine;

/// <summary>敵対/中立の結果とその根拠。次のSTEP3で索敵フィルタとして使用。</summary>
public struct FactionDecision2D
{
    public bool IsHostile;
    public bool HasExplicitRule;
    public string Reason;

    public FactionDecision2D(bool hostile, bool explicitRule, string reason)
    {
        IsHostile = hostile;
        HasExplicitRule = explicitRule;
        Reason = reason;
    }
}

/// <summary>
/// STEP2: 派閥・プレイヤーカルマ・派閥友好度から敵対するかを返す純粋判定API。
/// AIの索敵・攻撃・ダメージ・カルマ自動増減は行わない。
/// Resources/FactionRules/DefaultFactionRules2D.asset があればそれを使用。
/// 未作成の場合はC#組み込みの初期ルールで動作する。
/// </summary>
public static class FactionHostility2D
{
    public const string DefaultRulesResourcePath = "FactionRules/DefaultFactionRules2D";

    private static FactionRules2D cachedDefaultRules;
    private static bool attemptedLoad;

    /// <summary>UnityのResourcesからルールアセットを探す。Play中は毎フレーム読み直さない。</summary>
    public static FactionRules2D LoadDefaultRules()
    {
        if (!attemptedLoad || (!Application.isPlaying && cachedDefaultRules == null))
        {
            cachedDefaultRules = Resources.Load<FactionRules2D>(DefaultRulesResourcePath);
            attemptedLoad = true;
        }
        return cachedDefaultRules;
    }

    public static void ClearDefaultRulesCache()
    {
        cachedDefaultRules = null;
        attemptedLoad = false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetCacheAtPlayStart() => ClearDefaultRulesCache();

    /// <summary>
    /// 仮のカルマ・友好度も渡せるため、エディターのシミュレーションにも使える。
    /// rules=nullならResourcesアセットを試し、無ければ内蔵初期ルールを適用。
    /// </summary>
    public static FactionDecision2D EvaluateToPlayer(string attackerFactionId, int karma,
        int attackerFactionReputation, FactionRules2D rules = null)
    {
        string id = FactionId2D.Normalize(attackerFactionId);
        if (string.IsNullOrEmpty(id) || id == FactionId2D.Player)
            return new FactionDecision2D(false, false, "所属派閥が未設定、またはプレイヤー自身です。");

        rules = rules != null ? rules : LoadDefaultRules();
        FactionPlayerAttitude2D mode;
        int threshold;
        FactionPlayerRule2D entry = null;
        bool explicitRule = rules != null && rules.TryGetPlayerRule(id, out entry);
        if (explicitRule)
        {
            mode = entry.Attitude;
            threshold = entry.Threshold;
        }
        else if (rules != null)
        {
            // ユーザーがアセットから削除したルールをC#の初期値で復活させない。
            mode = FactionPlayerAttitude2D.Neutral;
            threshold = 0;
        }
        else
        {
            switch (id)
            {
                case FactionId2D.Monsters:
                    mode = FactionPlayerAttitude2D.AlwaysHostile; threshold = 0; break;
                case FactionId2D.Bandits:
                    mode = FactionPlayerAttitude2D.HostileWhenReputationBelow; threshold = 40; break;
                case FactionId2D.Adventurers:
                    mode = FactionPlayerAttitude2D.HostileWhenKarmaAtOrBelow; threshold = -30; break;
                default:
                    mode = FactionPlayerAttitude2D.Neutral; threshold = 0; break;
            }
        }

        bool hostile;
        string reason;
        switch (mode)
        {
            case FactionPlayerAttitude2D.AlwaysHostile:
                hostile = true;
                reason = "常に敵対（カルマ・友好度は無視）";
                break;
            case FactionPlayerAttitude2D.HostileWhenReputationBelow:
                hostile = attackerFactionReputation < threshold;
                reason = "友好度 " + attackerFactionReputation +
                    (hostile ? " < " : " >= ") + threshold +
                    "（カルマは無視）";
                break;
            case FactionPlayerAttitude2D.HostileWhenKarmaAtOrBelow:
                hostile = karma <= threshold;
                reason = "カルマ " + karma + (hostile ? " <= " : " > ") + threshold;
                break;
            default:
                hostile = false;
                reason = "中立（積極的には攻撃しない）";
                break;
        }
        return new FactionDecision2D(hostile, explicitRule, reason);
    }

    /// <summary>NPC同士の非対称な攻撃判定。攻撃する側とされる側を逆にすると結果が変わる。</summary>
    public static FactionDecision2D EvaluateNpcToNpc(string attackerFactionId, string targetFactionId,
        FactionMonsterType2D targetMonsterType = FactionMonsterType2D.None, FactionRules2D rules = null)
    {
        string attackerId = FactionId2D.Normalize(attackerFactionId);
        string targetId = FactionId2D.Normalize(targetFactionId);
        if (string.IsNullOrEmpty(attackerId) || string.IsNullOrEmpty(targetId))
            return new FactionDecision2D(false, false, "派閥IDが未設定です。");
        if (attackerId == FactionId2D.Player || targetId == FactionId2D.Player)
            return new FactionDecision2D(false, false, "プレイヤー相手にはEvaluateToPlayerを使用してください。");
        if (attackerId == targetId)
            return new FactionDecision2D(false, false, "同じ派閥は攻撃しません（標準設定）。");

        rules = rules != null ? rules : LoadDefaultRules();
        if (rules != null && rules.TryGetNpcRule(attackerId, targetId, targetMonsterType, out FactionNpcRule2D match))
            return new FactionDecision2D(match.IsHostile, true, match.IsHostile
                ? "派閥ルールにより攻撃対象です。" : "派閥ルールにより非攻撃対象です。");

        // アセットが無い環境でも、指定の4派閥の標準ルールを適用する。
        bool hostile = (attackerId == FactionId2D.Monsters &&
                         (targetId == FactionId2D.Bandits || targetId == FactionId2D.Adventurers)) ||
                       (attackerId == FactionId2D.Dwarves && targetId == FactionId2D.Bandits) ||
                       (attackerId == FactionId2D.Dwarves && targetId == FactionId2D.Monsters &&
                         targetMonsterType == FactionMonsterType2D.Slime);
        // アセットが存在する場合は、未設定の組み合わせは中立。内蔵初期値を重ねない。
        if (rules != null) hostile = false;
        return new FactionDecision2D(hostile, false,
            rules == null ? "C#内蔵の初期ルール" : "未指定の組み合わせは中立");
    }

    public static FactionDecision2D EvaluateToPlayer(FactionMember2D attacker)
    {
        if (attacker == null)
            return new FactionDecision2D(false, false, "攻撃側のFactionMember2Dが見つかりません。");
        PlayerKarmaReputation2D state = GameSessionManager.Instance != null
            ? PlayerKarmaReputation2D.GetCurrent() : null;
        int karma = state != null ? state.Karma : 0;
        int rep = state != null ? state.GetReputation(attacker.FactionId) : 0;
        return EvaluateToPlayer(attacker.FactionId, karma, rep);
    }

    public static FactionDecision2D Evaluate(FactionMember2D attacker, FactionMember2D target)
    {
        if (attacker == null || target == null)
            return new FactionDecision2D(false, false, "FactionMember2Dが足りません。");
        if (attacker == target)
            return new FactionDecision2D(false, false, "自分自身は攻撃しません。");
        if (target.IsPlayer) return EvaluateToPlayer(attacker);
        return EvaluateNpcToNpc(attacker.FactionId, target.FactionId, target.MonsterType);
    }

    /// <summary>AI接続予定の便利メソッド。trueなら自発的な索敵・攻撃候補。</summary>
    public static bool IsHostile(FactionMember2D attacker, FactionMember2D target) =>
        Evaluate(attacker, target).IsHostile;

    public static bool IsHostileToPlayer(FactionMember2D attacker) =>
        EvaluateToPlayer(attacker).IsHostile;
}
