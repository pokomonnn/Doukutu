using TMPro;
using UnityEngine;

/// <summary>
/// GameSessionManager の所持金をTextMeshProへ表示します。
/// 探索シーン・Town_MainのどちらのCanvasにも同じスクリプトを付けられます。
/// </summary>
[DisallowMultipleComponent]
public class MoneyUI : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら GameSessionManager.Instance を自動取得します")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Tooltip("旧構成との互換用です。Game Session Managerが見つからない時だけ参照します")]
    [SerializeField] private PlayerMoneyController playerMoneyController;

    [Tooltip("未設定なら、このObjectまたは子のTMP_Textを自動取得します")]
    [SerializeField] private TMP_Text moneyText;

    [Header("表示")]
    [Tooltip("{0} に所持金が入ります。例：所持金 ¥{0:N0}")]
    [SerializeField] private string displayFormat = "所持金 ¥{0:N0}";

    private bool isSubscribed;
    private bool isSubscribedToGameSession;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        SubscribeEvents();
        RefreshUI();
    }

    private void Start()
    {
        FindReferences();
        SubscribeEvents();
        RefreshUI();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    /// <summary>
    /// 現在の所持金表示をすぐに更新します。
    /// </summary>
    public void RefreshUI()
    {
        if (moneyText == null)
        {
            return;
        }

        moneyText.text = string.Format(
            GetSafeDisplayFormat(),
            Mathf.Max(0, GetCurrentMoney())
        );
    }

    private int GetCurrentMoney()
    {
        if (gameSessionManager != null)
        {
            gameSessionManager.EnsureMoneyInitialized();
            return gameSessionManager.CurrentMoney;
        }

        return playerMoneyController != null
            ? playerMoneyController.CurrentMoney
            : 0;
    }

    private void HandleMoneyChanged(int currentMoney)
    {
        if (moneyText == null)
        {
            return;
        }

        moneyText.text = string.Format(
            GetSafeDisplayFormat(),
            Mathf.Max(0, currentMoney)
        );
    }

    private void SubscribeEvents()
    {
        if (isSubscribed)
        {
            return;
        }

        if (gameSessionManager != null)
        {
            gameSessionManager.MoneyChanged += HandleMoneyChanged;
            isSubscribed = true;
            isSubscribedToGameSession = true;
            return;
        }

        if (playerMoneyController != null)
        {
            playerMoneyController.MoneyChanged += HandleMoneyChanged;
            isSubscribed = true;
            isSubscribedToGameSession = false;
        }
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed)
        {
            return;
        }

        if (isSubscribedToGameSession)
        {
            if (gameSessionManager != null)
            {
                gameSessionManager.MoneyChanged -= HandleMoneyChanged;
            }
        }
        else if (playerMoneyController != null)
        {
            playerMoneyController.MoneyChanged -= HandleMoneyChanged;
        }

        isSubscribed = false;
        isSubscribedToGameSession = false;
    }

    private void FindReferences()
    {
        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>();
        }

        if (playerMoneyController == null)
        {
            playerMoneyController =
                FindAnyObjectByType<PlayerMoneyController>();
        }

        if (moneyText == null)
        {
            moneyText = GetComponent<TMP_Text>();
        }

        if (moneyText == null)
        {
            moneyText = GetComponentInChildren<TMP_Text>(true);
        }
    }

    private string GetSafeDisplayFormat()
    {
        return string.IsNullOrWhiteSpace(displayFormat)
            ? "所持金 ¥{0:N0}"
            : displayFormat;
    }

    private void OnValidate()
    {
        if (string.IsNullOrWhiteSpace(displayFormat))
        {
            displayFormat = "所持金 ¥{0:N0}";
        }
    }
}
