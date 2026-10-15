/// <summary>
/// 洞窟退出時に精算済みの現金額を、その探索結果にひも付けてResult Sceneへ渡します。
/// 所持金への実際の加算はExpeditionResultCollector2Dが退出時に1回だけ行います。
/// </summary>
public static class ExpeditionRecoveredMoneySession
{
    private static ExpeditionResultData recordedResult;
    private static int recoveredMoney;

    public static void Record(ExpeditionResultData result, int money)
    {
        recordedResult = result;
        recoveredMoney = money < 0 ? 0 : money;
    }

    public static int GetAmount(ExpeditionResultData result)
    {
        return result != null && object.ReferenceEquals(recordedResult, result)
            ? recoveredMoney
            : 0;
    }

    public static void Clear()
    {
        recordedResult = null;
        recoveredMoney = 0;
    }
}
