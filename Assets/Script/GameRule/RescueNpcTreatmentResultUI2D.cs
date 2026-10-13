using TMPro;
using UnityEngine;

/// <summary>
/// 探索で実際に行ったNPCの応急処置をResult画面に表示します。
/// 止血・解毒・骨折固定の成功実績から治療報酬を計算し、
/// GameSessionManagerの所持金にResult画面で1回だけ加算します。
/// エレベーター回収は治療実績・治療報酬の条件ではありません。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class RescueNpcTreatmentResultUI2D : MonoBehaviour
{
    [Header("表示先")]
    [SerializeField] private TMP_Text resultText;

    [Header("NPC治療報酬（症状1種類の処置成功につき）")]
    [Tooltip("包帯による止血を1回成功した際の報酬（G）。")]
    [SerializeField, Min(0)] private int bleedingTreatmentReward = 100;

    [Tooltip("解毒剤による解毒を1回成功した際の報酬（G）。")]
    [SerializeField, Min(0)] private int poisonTreatmentReward = 100;

    [Tooltip("副木による骨折固定を1回成功した際の報酬（G）。")]
    [SerializeField, Min(0)] private int fractureTreatmentReward = 100;

    [Tooltip("Result画面が開いたとき治療報酬を所持金へ自動加算する。通常はON。")]
    [SerializeField] private bool grantTreatmentRewardOnStart = true;

    [Header("診断")]
    [SerializeField] private bool showDebugLogs = true;

    public int CurrentTreatmentReward { get; private set; }
    public bool TreatmentRewardGranted { get; private set; }

    private void Awake()
    {
        if (resultText == null) resultText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        // OnEnableでは表示更新のみ。支払いはStartで行う。
        Refresh();
    }

    private void Start()
    {
        if (grantTreatmentRewardOnStart)
        {
            TryGrantTreatmentReward();
        }
        Refresh();
    }

    private void OnValidate()
    {
        bleedingTreatmentReward = Mathf.Max(0, bleedingTreatmentReward);
        poisonTreatmentReward = Mathf.Max(0, poisonTreatmentReward);
        fractureTreatmentReward = Mathf.Max(0, fractureTreatmentReward);
    }

    /// <summary>
    /// 新規の応急処置の種類×報酬額。NPCを回収したかどうかには依存しません。
    /// 一人の同じ症状は記録側で1回までに制限されています。
    /// </summary>
    public int CalculateTreatmentReward(RescueNpcTreatmentResultSession2D.Report data)
    {
        if (data == null) return 0;

        long total = (long)Mathf.Max(0, data.BleedingTreated) * Mathf.Max(0, bleedingTreatmentReward)
                   + (long)Mathf.Max(0, data.PoisonTreated) * Mathf.Max(0, poisonTreatmentReward)
                   + (long)Mathf.Max(0, data.FractureTreated) * Mathf.Max(0, fractureTreatmentReward);

        return (int)System.Math.Min(total, int.MaxValue);
    }

    /// <summary>
    /// Result画面の治療報酬を1回だけ受け取る。
    /// Refresh()は支払いをしません。GameSessionManagerが見つからない場合は
    /// 支払い済みにせず、手動で再試行できます。
    /// </summary>
    [ContextMenu("NPC治療報酬を受け取る・未受取なら再試行")]
    private void GrantRewardFromContextMenu()
    {
        TryGrantTreatmentReward();
    }

    public bool TryGrantTreatmentReward()
    {
        ExpeditionResultData currentResult = ExpeditionResultSession.Current;
        RescueNpcTreatmentResultSession2D.Report data =
            RescueNpcTreatmentResultSession2D.Current;

        if (currentResult == null || data == null)
        {
            Debug.LogWarning(
                "[RescueTreatment][報酬失敗] 探索結果／治療集計がありません。" +
                "Collectorから治療結果がResultに渡っているか確認してください。", this);
            return false;
        }

        if (data.TreatmentRewardPaid)
        {
            TreatmentRewardGranted = true;
            CurrentTreatmentReward = data.TreatmentRewardPaidAmount;
            if (showDebugLogs)
                Debug.Log("[RescueTreatment][報酬重複防止] この探索の治療報酬は受取済み: " +
                          CurrentTreatmentReward + "G", this);
            Refresh();
            return true;
        }

        CurrentTreatmentReward = CalculateTreatmentReward(data);

        if (CurrentTreatmentReward == 0)
        {
            // 対象NPCゼロや、全単価0Gの場合も同じ探索で再支払いしない。
            data.TreatmentRewardPaid = true;
            data.TreatmentRewardPaidAmount = 0;
            TreatmentRewardGranted = true;
            if (showDebugLogs)
                Debug.Log("[RescueTreatment][治療報酬] 0Gのため加算なし（支払い完了扱い）", this);
            Refresh();
            return true;
        }

        GameSessionManager session = GameSessionManager.Instance;
        if (session == null)
        {
            Debug.LogWarning("[RescueTreatment][報酬失敗] GameSessionManager.Instanceがありません。" +
                             "Resultシーン内のManagerを確認してください。治療報酬=" +
                             CurrentTreatmentReward + "G（未加算）", this);
            Refresh();
            return false;
        }

        int previousMoney = session.CurrentMoney;
        if (!session.AddMoney(CurrentTreatmentReward))
        {
            Debug.LogWarning("[RescueTreatment][報酬失敗] AddMoneyがfalseです。" +
                             "治療報酬=" + CurrentTreatmentReward + "G（未加算）", this);
            Refresh();
            return false;
        }

        // 所持金の加算が成功した後にだけ確定する。
        data.TreatmentRewardPaid = true;
        data.TreatmentRewardPaidAmount = CurrentTreatmentReward;
        TreatmentRewardGranted = true;

        Debug.Log("[RescueTreatment][報酬加算成功 v3.7] 止血=" + data.BleedingTreated +
                  "×" + bleedingTreatmentReward + "G / 解毒=" + data.PoisonTreated +
                  "×" + poisonTreatmentReward + "G / 骨折固定=" + data.FractureTreated +
                  "×" + fractureTreatmentReward + "G / 治療報酬=" +
                  CurrentTreatmentReward + "G / 所持金=" + previousMoney + "→" +
                  session.CurrentMoney + "G", this);

        Refresh();
        return true;
    }

    [ContextMenu("Refresh NPC Treatment Result")]
    public void Refresh()
    {
        if (resultText == null) resultText = GetComponent<TMP_Text>();
        if (resultText == null) return;

        RescueNpcTreatmentResultSession2D.Report data =
            RescueNpcTreatmentResultSession2D.Current;
        if (data == null)
        {
            resultText.text = "NPC応急処置結果\n治療記録がありません";
            if (showDebugLogs)
                Debug.LogWarning("[RescueTreatment][ResultUI] 治療集計がありません。" +
                                 "洞窟のCollectorへ接続済みか確認してください。", this);
            return;
        }

        CurrentTreatmentReward = data.TreatmentRewardPaid
            ? data.TreatmentRewardPaidAmount
            : CalculateTreatmentReward(data);
        TreatmentRewardGranted = data.TreatmentRewardPaid;

        string rewardStatus = data.TreatmentRewardPaid ? "受取済み" : "未受取";

        resultText.text =
            "NPC応急処置結果\n" +
            "治療したNPC：" + data.TreatedNpcCount + " 人\n" +
            "止血：" + data.BleedingTreated + " 人\n" +
            "解毒：" + data.PoisonTreated + " 人\n" +
            "骨折固定：" + data.FractureTreated + " 人\n" +
            "治療して生存救出：" + data.TreatedAndRescued + " 人\n" +
            "治療後未回収：" + data.TreatedNotRecovered + " 人\n" +
            "治療後死亡・回収：" + data.TreatedButDeadRecovered + " 人\n" +
            "未処置症状が残る生存者：" + data.UnstabilizedRescued + " 人\n" +
            "治療報酬：" + CurrentTreatmentReward.ToString("N0") + " G（" + rewardStatus + "）";

        if (showDebugLogs)
            Debug.Log("[RescueTreatment][ResultUI表示 v3.7] 治療NPC=" + data.TreatedNpcCount +
                      " / 未回収=" + data.TreatedNotRecovered +
                      " / 報酬=" + CurrentTreatmentReward + "G / " + rewardStatus, this);
    }
}
