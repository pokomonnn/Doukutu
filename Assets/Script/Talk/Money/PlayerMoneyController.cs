using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// 既存のPlayer側コードから所持金を扱うための窓口です。
/// 実際の所持金は、DontDestroyOnLoad の GameSessionManager が保持します。
/// Playerに付けたままで問題ありません。
/// </summary>
[DisallowMultipleComponent]
public class PlayerMoneyController : MonoBehaviour
{
    [Header("Game Session")]
    [Tooltip("未設定なら GameSessionManager.Instance を自動取得します")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Header("開始時の所持金（旧設定の引き継ぎ用）")]
    [Tooltip("最初の探索シーンで初回だけ GameSessionManager へ渡す所持金です。以前の Starting Money の値は自動的にここへ引き継がれます。")]
    [FormerlySerializedAs("startingMoney")]
    [SerializeField, Min(0)] private int initialMoney = 0;

    [Tooltip("オンなら、まだ所持金が初期化されていない時だけ Initial Money をGameSessionManagerへ渡します。")]
    [SerializeField] private bool useInitialMoneyForNewSession = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs;

    /// <summary>
    /// 現在の所持金です。GameSessionManager が無い時は0を返します。
    /// </summary>
    public int CurrentMoney
    {
        get
        {
            GameSessionManager session = GetSessionManager();

            if (session == null)
            {
                return 0;
            }

            session.EnsureMoneyInitialized();
            return session.CurrentMoney;
        }
    }

    /// <summary>
    /// 所持金が変化した時に、現在の所持金を通知します。
    /// 既存のスクリプトとの互換用イベントです。
    /// </summary>
    public event Action<int> MoneyChanged;

    private bool isSubscribed;
    private bool hasTriedInitialMoneyTransfer;

    private void Awake()
    {
        FindReferences();
        TryTransferInitialMoney();
    }

    private void OnEnable()
    {
        FindReferences();
        TryTransferInitialMoney();
        SubscribeEvents();
    }

    private void Start()
    {
        FindReferences();
        TryTransferInitialMoney();
        SubscribeEvents();
    }

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    /// <summary>
    /// 指定金額を所持金へ加算します。
    /// </summary>
    public bool AddMoney(int amount)
    {
        GameSessionManager session = GetSessionManager();

        if (session == null)
        {
            LogWarning("GameSessionManager が見つからないため、所持金を加算できません。");
            return false;
        }

        return session.AddMoney(amount);
    }

    /// <summary>
    /// 指定金額を支払います。所持金が足りない場合は減らしません。
    /// </summary>
    public bool TrySpendMoney(int amount)
    {
        GameSessionManager session = GetSessionManager();

        if (session == null)
        {
            LogWarning("GameSessionManager が見つからないため、支払いできません。");
            return false;
        }

        return session.TrySpendMoney(amount);
    }

    /// <summary>
    /// 指定金額を所持しているか確認します。
    /// </summary>
    public bool CanAfford(int amount)
    {
        GameSessionManager session = GetSessionManager();
        return session != null && session.CanAfford(amount);
    }

    /// <summary>
    /// 所持金を直接設定します。ロード・デバッグ用です。
    /// </summary>
    public void SetMoney(int amount)
    {
        GameSessionManager session = GetSessionManager();

        if (session == null)
        {
            LogWarning("GameSessionManager が見つからないため、所持金を設定できません。");
            return;
        }

        session.SetMoney(amount);
    }

    private void HandleSessionMoneyChanged(int currentMoney)
    {
        MoneyChanged?.Invoke(currentMoney);
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || gameSessionManager == null)
        {
            return;
        }

        gameSessionManager.MoneyChanged += HandleSessionMoneyChanged;
        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || gameSessionManager == null)
        {
            return;
        }

        gameSessionManager.MoneyChanged -= HandleSessionMoneyChanged;
        isSubscribed = false;
    }

    private void TryTransferInitialMoney()
    {
        if (hasTriedInitialMoneyTransfer)
        {
            return;
        }

        hasTriedInitialMoneyTransfer = true;

        if (!useInitialMoneyForNewSession ||
            gameSessionManager == null)
        {
            return;
        }

        // 初めてゲームを開始した時だけ、従来のPlayer側 Starting Money を
        // シーン共通の所持金へ移します。Town_Mainへ移動後のPlayer生成では
        // すでに初期化済みなので、現在の所持金は上書きされません。
        gameSessionManager.TrySetInitialMoney(initialMoney);
    }

    private GameSessionManager GetSessionManager()
    {
        FindReferences();
        return gameSessionManager;
    }

    private void FindReferences()
    {
        if (gameSessionManager != null)
        {
            return;
        }

        gameSessionManager = GameSessionManager.Instance;

        if (gameSessionManager == null)
        {
            gameSessionManager =
                FindAnyObjectByType<GameSessionManager>();
        }
    }

    private void LogWarning(string message)
    {
        if (!showDebugLogs)
        {
            return;
        }

        Debug.LogWarning($"[PlayerMoneyController] {message}", this);
    }

    private void OnValidate()
    {
        initialMoney = Mathf.Max(0, initialMoney);
    }
}
