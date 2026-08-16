using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Result Sceneへ渡す、今回の探索結果1回分のデータです。
/// </summary>
[Serializable]
public class ExpeditionResultData
{
    public int RescuedNpcCount;
    public int DeadNpcCount;
    public int RecoveredItemBoxCount;

    public List<ExpeditionTreasureResult> TreasureItems =
        new List<ExpeditionTreasureResult>();
}

/// <summary>
/// お宝Item1種類分の集計結果です。
/// </summary>
[Serializable]
public class ExpeditionTreasureResult
{
    public ItemData ItemData;
    public int Amount;

    public string DisplayName =>
        ItemData != null
            ? ItemData.DisplayName
            : "不明なItem";
}

/// <summary>
/// Sceneをまたいで探索結果を保持する静的Sessionです。
/// 洞窟SceneでSetResultし、Result SceneでCurrentを読み取ります。
/// </summary>
public static class ExpeditionResultSession
{
    private static ExpeditionResultData current =
        new ExpeditionResultData();

    public static ExpeditionResultData Current => current;

    public static bool HasResult { get; private set; }

    public static void SetResult(ExpeditionResultData result)
    {
        current = result ?? new ExpeditionResultData();
        HasResult = true;
    }

    public static void Clear()
    {
        current = new ExpeditionResultData();
        HasResult = false;
    }
}
