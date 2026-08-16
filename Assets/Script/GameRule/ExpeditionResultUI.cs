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

    [Header("表示形式")]
    [SerializeField] private string rescuedNpcFormat = "{0} 人";
    [SerializeField] private string deadNpcFormat = "{0} 人";
    [SerializeField] private string recoveredItemBoxFormat = "{0} 個";

    [Tooltip("お宝が1つも無い時の表示です。")]
    [SerializeField] private string noTreasureMessage = "お宝なし";

    [Tooltip("お宝1種類ごとの表示形式です。{0}=名前、{1}=個数")]
    [SerializeField] private string treasureLineFormat = "{0} × {1}";

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

    private void Awake()
    {
        SetupButton();
    }

    private void Start()
    {
        RefreshUI();
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

        if (showDebugLogs)
        {
            Debug.Log(
                $"[ExpeditionResultUI] 表示更新: " +
                $"救出={result.RescuedNpcCount}, " +
                $"死亡={result.DeadNpcCount}, " +
                $"ItemBox={result.RecoveredItemBoxCount}, " +
                $"Treasure={result.TreasureItems?.Count ?? 0}",
                this
            );
        }
    }

    public void ReturnToTown()
    {
        if (string.IsNullOrWhiteSpace(townSceneName))
        {
            Debug.LogWarning(
                "[ExpeditionResultUI] Town Scene Nameが空です。",
                this
            );
            return;
        }

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

    private static void SetText(TMP_Text text, string value)
    {
        if (text != null)
        {
            text.text = value ?? string.Empty;
        }
    }
}
