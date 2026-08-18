using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Result SceneのUIへExpeditionResultSessionの内容を表示します。
/// </summary>
[DisallowMultipleComponent]
public class ExpeditionResultUI : MonoBehaviour
{
    [Header("結果表示")]
    [SerializeField] private TMP_Text rescuedNpcCountText;
    [SerializeField] private TMP_Text deadNpcCountText;
    [SerializeField] private TMP_Text recoveredItemBoxCountText;
    [SerializeField] private TMP_Text treasureListText;

    [Header("救出報酬")]
    [Tooltip("救出報酬の合計を表示するTextです。未設定でも報酬加算自体は行われます。")]
    [SerializeField] private TMP_Text rescueRewardText;

    [Tooltip("生存している救出者1人あたりの報酬です。")]
    [SerializeField, Min(0)] private int aliveRescueRewardPerPerson = 500;

    [Tooltip("死亡している救出者1人あたりの報酬です。")]
    [SerializeField, Min(0)] private int deadRescueRewardPerPerson = 50;

    [Tooltip("Result Sceneを開いた時に救出報酬を所持金へ自動加算します。")]
    [SerializeField] private bool grantRescueRewardOnStart = true;

    [Tooltip("救出報酬の表示形式です。{0}=合計報酬")]
    [SerializeField] private string rescueRewardFormat = "{0:N0} G";

    [Header("表示形式")]
    [SerializeField] private string rescuedNpcFormat = "{0} 人";
    [SerializeField] private string deadNpcFormat = "{0} 人";
    [SerializeField] private string recoveredItemBoxFormat = "{0} 個";

    [Tooltip("お宝が1つも無い時の表示です。")]
    [SerializeField] private string noTreasureMessage = "お宝なし";

    [Tooltip("お宝1種類ごとの表示形式です。{0}=名前、{1}=個数")]
    [SerializeField] private string treasureLineFormat = "{0} × {1}";

    [Header("墓場への死亡者記録")]
    [Tooltip(
        "ONならTown_Mainへ戻る直前に、今回の死亡NPC数を墓場の累計へ加算します。" +
        "通常はONで使用してください。"
    )]
    [SerializeField] private bool recordDeadNpcCountWhenReturningTown = true;

    [Header("町へ戻る")]
    [SerializeField] private Button returnTownButton;

    [Tooltip("町Scene名です。")]
    [SerializeField] private string townSceneName = "Town_Main";

    [Tooltip(
        "町へ移動する直前にResultの一時データを破棄します。" +
        "通常はONでOKです。"
    )]
    [SerializeField] private bool clearResultWhenReturningTown = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    // 同じExpeditionResultDataを使ってResult Sceneを再読込した場合に、
    // 報酬が二重加算されるのを防ぎます。
    private static ExpeditionResultData lastRewardedResult;

    public int CurrentRescueReward { get; private set; }
    public bool RescueRewardGranted { get; private set; }
    public bool DeadNpcCountRecordedToGraveyard { get; private set; }

    private bool returnToTownStarted;

    private void Awake()
    {
        SetupButton();
    }

    private void Start()
    {
        RefreshUI();

        if (grantRescueRewardOnStart)
        {
            TryGrantRescueReward();
        }
    }

    private void OnDestroy()
    {
        if (returnTownButton != null)
        {
            returnTownButton.onClick.RemoveListener(ReturnToTown);
        }
    }

    [ContextMenu("Refresh Result UI")]
    public void RefreshUI()
    {
        ExpeditionResultData result =
            ExpeditionResultSession.Current ??
            new ExpeditionResultData();

        SetText(
            rescuedNpcCountText,
            string.Format(
                rescuedNpcFormat,
                Mathf.Max(0, result.RescuedNpcCount)
            )
        );

        SetText(
            deadNpcCountText,
            string.Format(
                deadNpcFormat,
                Mathf.Max(0, result.DeadNpcCount)
            )
        );

        SetText(
            recoveredItemBoxCountText,
            string.Format(
                recoveredItemBoxFormat,
                Mathf.Max(0, result.RecoveredItemBoxCount)
            )
        );

        SetText(
            treasureListText,
            BuildTreasureText(result)
        );

        CurrentRescueReward = CalculateRescueReward(result);

        SetText(
            rescueRewardText,
            string.Format(
                rescueRewardFormat,
                CurrentRescueReward
            )
        );

        if (showDebugLogs)
        {
            Debug.Log(
                $"[ExpeditionResultUI] 表示更新: " +
                $"救出={result.RescuedNpcCount}, " +
                $"死亡={result.DeadNpcCount}, " +
                $"ItemBox={result.RecoveredItemBoxCount}, " +
                $"Treasure={result.TreasureItems?.Count ?? 0}, " +
                $"救出報酬={CurrentRescueReward:N0}G",
                this
            );
        }
    }

    /// <summary>
    /// 現在の探索結果から救出報酬を計算します。
    /// 生存者 × aliveRescueRewardPerPerson + 死亡者 × deadRescueRewardPerPerson。
    /// </summary>
    public int CalculateRescueReward(ExpeditionResultData result)
    {
        if (result == null)
        {
            return 0;
        }

        long aliveReward =
            (long)Mathf.Max(0, result.RescuedNpcCount) *
            Mathf.Max(0, aliveRescueRewardPerPerson);

        long deadReward =
            (long)Mathf.Max(0, result.DeadNpcCount) *
            Mathf.Max(0, deadRescueRewardPerPerson);

        long total = aliveReward + deadReward;

        return total > int.MaxValue
            ? int.MaxValue
            : (int)total;
    }

    /// <summary>
    /// 救出報酬をGameSessionManagerの所持金へ1回だけ加算します。
    /// RefreshUI()を何度呼んでも追加加算はされません。
    /// </summary>
    public bool TryGrantRescueReward()
    {
        ExpeditionResultData result = ExpeditionResultSession.Current;

        if (result == null)
        {
            LogWarning("探索結果が無いため、救出報酬を加算できませんでした。");
            return false;
        }

        CurrentRescueReward = CalculateRescueReward(result);

        if (lastRewardedResult == result)
        {
            RescueRewardGranted = true;
            Log("この探索結果の救出報酬はすでに加算済みです。二重加算を防止しました。");
            return false;
        }

        if (CurrentRescueReward <= 0)
        {
            lastRewardedResult = result;
            RescueRewardGranted = true;
            Log("救出報酬は0Gです。所持金への加算はありません。");
            return true;
        }

        GameSessionManager session = GameSessionManager.Instance;

        if (session == null)
        {
            LogWarning(
                "GameSessionManager.Instanceが見つからないため、" +
                $"救出報酬 {CurrentRescueReward:N0}G を加算できませんでした。"
            );
            return false;
        }

        if (!session.AddMoney(CurrentRescueReward))
        {
            LogWarning(
                $"救出報酬 {CurrentRescueReward:N0}G の加算に失敗しました。"
            );
            return false;
        }

        lastRewardedResult = result;
        RescueRewardGranted = true;

        Log(
            $"救出報酬を加算しました。" +
            $"生存={Mathf.Max(0, result.RescuedNpcCount)}人 × {aliveRescueRewardPerPerson:N0}G / " +
            $"死亡={Mathf.Max(0, result.DeadNpcCount)}人 × {deadRescueRewardPerPerson:N0}G / " +
            $"合計={CurrentRescueReward:N0}G / " +
            $"現在所持金={session.CurrentMoney:N0}G"
        );

        return true;
    }

    /// <summary>
    /// 今回の探索で死亡状態のまま回収されたNPC数を、
    /// GameSessionManagerの墓場累計へ1回だけ加算します。
    /// </summary>
    public bool TryRecordDeadNpcCountToGraveyard()
    {
        if (DeadNpcCountRecordedToGraveyard)
        {
            return true;
        }

        ExpeditionResultData result = ExpeditionResultSession.Current;

        if (result == null)
        {
            LogWarning(
                "探索結果が無いため、墓場へ死亡NPC数を記録できませんでした。"
            );
            return false;
        }

        int deadNpcCount = Mathf.Max(0, result.DeadNpcCount);

        if (deadNpcCount <= 0)
        {
            DeadNpcCountRecordedToGraveyard = true;
            Log("今回の死亡NPCは0人のため、墓場の累計は変更しません。");
            return true;
        }

        GameSessionManager session = GameSessionManager.Instance;

        if (session == null)
        {
            LogWarning(
                "GameSessionManager.Instanceが見つからないため、" +
                $"死亡NPC {deadNpcCount:N0}人を墓場へ記録できませんでした。"
            );
            return false;
        }

        if (!session.AddDeadNpcCount(deadNpcCount))
        {
            LogWarning(
                $"死亡NPC {deadNpcCount:N0}人の墓場登録に失敗しました。"
            );
            return false;
        }

        DeadNpcCountRecordedToGraveyard = true;

        Log(
            $"墓場へ死亡NPCを記録しました。" +
            $"今回={deadNpcCount:N0}人 / " +
            $"累計={session.TotalDeadNpcCount:N0}人"
        );

        return true;
    }

    public void ReturnToTown()
    {
        if (returnToTownStarted)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(townSceneName))
        {
            Debug.LogWarning(
                "[ExpeditionResultUI] Town Scene Nameが空です。",
                this
            );
            return;
        }

        if (recordDeadNpcCountWhenReturningTown &&
            !TryRecordDeadNpcCountToGraveyard())
        {
            // 0人の場合や既に記録済みの場合も安全にTownへ戻れるよう、
            // 実際に未記録で失敗した時だけ警告を出し、遷移自体は止めません。
            LogWarning(
                "墓場への死亡NPC記録が完了していない可能性がありますが、Town_Mainへの移動は続行します。"
            );
        }

        returnToTownStarted = true;

        if (clearResultWhenReturningTown)
        {
            ExpeditionResultSession.Clear();
        }

        SceneManager.LoadScene(townSceneName);
    }

    private string BuildTreasureText(
        ExpeditionResultData result)
    {
        if (result == null ||
            result.TreasureItems == null ||
            result.TreasureItems.Count == 0)
        {
            return noTreasureMessage;
        }

        StringBuilder builder = new StringBuilder();

        foreach (ExpeditionTreasureResult treasure
                 in result.TreasureItems)
        {
            if (treasure == null ||
                treasure.ItemData == null ||
                treasure.Amount <= 0)
            {
                continue;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendFormat(
                treasureLineFormat,
                treasure.DisplayName,
                treasure.Amount
            );
        }

        return builder.Length > 0
            ? builder.ToString()
            : noTreasureMessage;
    }

    private void SetupButton()
    {
        if (returnTownButton == null)
        {
            return;
        }

        returnTownButton.onClick.RemoveListener(ReturnToTown);
        returnTownButton.onClick.AddListener(ReturnToTown);
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[ExpeditionResultUI] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning($"[ExpeditionResultUI] {message}", this);
    }

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }
    private void OnValidate()
    {
        aliveRescueRewardPerPerson = Mathf.Max(0, aliveRescueRewardPerPerson);
        deadRescueRewardPerPerson = Mathf.Max(0, deadRescueRewardPerPerson);

        if (string.IsNullOrWhiteSpace(rescueRewardFormat))
        {
            rescueRewardFormat = "{0:N0} G";
        }
    }

}
