using System;

/// <summary>
/// 酒場の利用進捗です。
/// GameSessionManagerがシーン間で保持し、SaveGameDataへ保存します。
/// </summary>
[Serializable]
public class TavernProgressData
{
    /// <summary>出撃開始回数。酒場のラウンド管理・デバッグ用です。</summary>
    public int RoundIndex;

    /// <summary>一人飲みの累計回数。</summary>
    public int PersonalDrinkTotal;

    /// <summary>現在ラウンドの一人飲み回数。</summary>
    public int PersonalDrinkThisRound;

    /// <summary>みんなにおごった累計回数。</summary>
    public int TreatEveryoneTotal;

    /// <summary>現在ラウンドでみんなにおごった回数。</summary>
    public int TreatEveryoneThisRound;

    /// <summary>一人飲み累計報酬を受け取り済みか。</summary>
    public bool PersonalDrinkRewardClaimed;

    /// <summary>みんなにおごる累計報酬を受け取り済みか。</summary>
    public bool TreatEveryoneRewardClaimed;
}
