using System.Collections;
using UnityEngine;

/// <summary>
/// Town_Mainにあるプレイヤー専用保管箱とGameSessionManagerを接続します。
/// 箱の中身が変化するたびに自動保存し、町へ戻った時に復元します。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemBoxInventory))]
public class TownPlayerItemBoxSessionBridge : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定ならGameSessionManager.Instanceから自動取得します。")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Tooltip("このObjectに付いているプレイヤー専用保管箱です。")]
    [SerializeField] private ItemBoxInventory itemBoxInventory;

    [Header("保存・復元")]
    [Tooltip("町シーン開始時に、保存済みの保管箱を自動復元します。")]
    [SerializeField] private bool restoreOnStart = true;

    [Tooltip("まだ保管箱データが無い場合、現在の箱の内容を初期状態として登録します。通常はONでOKです。")]
    [SerializeField] private bool initializeSessionWhenMissing = true;

    [Tooltip("箱の中身が変化するたびにGameSessionManagerへ即時保存します。")]
    [SerializeField] private bool captureWheneverInventoryChanges = true;

    [Tooltip("このObjectが無効化・破棄される時にも最後の状態を保存します。")]
    [SerializeField] private bool captureOnDisable = true;

    [Header("診断ログ")]
    [SerializeField] private bool showDebugLogs = true;

    public ItemBoxInventory ItemBoxInventory => itemBoxInventory;
    public bool HasRestoredForThisInstance => hasRestoredForThisInstance;

    private bool hasRestoredForThisInstance;
    private bool isRestoring;
    private bool isApplicationQuitting;
    private bool isSubscribed;

    private void Awake()
    {
        FindReferences();
    }

    private void OnEnable()
    {
        FindReferences();
        Subscribe();
    }

    private IEnumerator Start()
    {
        // ItemBoxInventory / TownPlayerInventory側のAwake・OnEnable完了を待つ。
        yield return null;
        EnsureSessionReady();
    }

    private void OnDisable()
    {
        if (captureOnDisable &&
            !isApplicationQuitting &&
            !isRestoring &&
            hasRestoredForThisInstance &&
            GameSessionManager.Instance != null &&
            itemBoxInventory != null)
        {
            CaptureToSession();
        }

        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void OnApplicationQuit()
    {
        isApplicationQuitting = true;
    }

    /// <summary>
    /// 初回アクセス時に、保存済みなら復元、無ければ現在状態を初期登録します。
    /// </summary>
    public void EnsureSessionReady()
    {
        if (hasRestoredForThisInstance) return;
        FindReferences();

        if (gameSessionManager == null || itemBoxInventory == null)
        {
            LogWarning("初期化できません。GameSessionManagerまたはItemBoxInventoryが見つかりません。");
            return;
        }

        if (restoreOnStart &&
            gameSessionManager.HasPlayerItemBoxSessionData)
        {
            RestoreFromSession();
            return;
        }

        if (initializeSessionWhenMissing &&
            !gameSessionManager.HasPlayerItemBoxSessionData)
        {
            hasRestoredForThisInstance = true;
            CaptureToSession();
        }
    }

    /// <summary>現在の保管箱をGameSessionManagerへ保存します。</summary>
    public bool CaptureToSession()
    {
        FindReferences();

        if (!hasRestoredForThisInstance && restoreOnStart &&
            gameSessionManager != null && gameSessionManager.HasPlayerItemBoxSessionData)
            return RestoreFromSession();

        if (gameSessionManager == null || itemBoxInventory == null)
        {
            LogWarning("保管箱を保存できません。参照を確認してください。");
            return false;
        }

        bool result = gameSessionManager.CapturePlayerItemBox(
            itemBoxInventory
        );

        if (result)
        {
            Log("保管箱をGameSessionManagerへ保存しました。");
        }

        if (result) hasRestoredForThisInstance = true;
        return result;
    }

    /// <summary>GameSessionManagerから保管箱を復元します。</summary>
    public bool RestoreFromSession()
    {
        FindReferences();

        if (gameSessionManager == null || itemBoxInventory == null)
        {
            LogWarning("保管箱を復元できません。参照を確認してください。");
            return false;
        }

        if (!gameSessionManager.HasPlayerItemBoxSessionData)
        {
            Log("復元データが無いため、現在の箱をそのまま使用します。");
            return false;
        }

        isRestoring = true;

        bool restored = gameSessionManager.RestorePlayerItemBox(
            itemBoxInventory,
            out string resultMessage
        );

        isRestoring = false;
        hasRestoredForThisInstance = true;

        if (restored)
        {
            Log(resultMessage);
        }
        else
        {
            LogWarning(resultMessage);
        }

        return restored;
    }

    [ContextMenu("保管箱をセッションへ保存")]
    private void CaptureFromContextMenu()
    {
        CaptureToSession();
    }

    [ContextMenu("保管箱をセッションから復元")]
    private void RestoreFromContextMenu()
    {
        RestoreFromSession();
    }

    [ContextMenu("保管箱セッション診断")]
    private void LogDiagnosticsFromContextMenu()
    {
        FindReferences();

        if (gameSessionManager == null)
        {
            LogWarning("GameSessionManagerが見つかりません。");
            return;
        }

        Log(gameSessionManager.GetPlayerItemBoxSessionSummary());
    }

    private void HandleInventoryChanged()
    {
        if (!captureWheneverInventoryChanges || isRestoring || !hasRestoredForThisInstance)
        {
            return;
        }

        CaptureToSession();
    }

    private void Subscribe()
    {
        if (isSubscribed || itemBoxInventory == null)
        {
            return;
        }

        itemBoxInventory.OnInventoryChanged += HandleInventoryChanged;
        isSubscribed = true;
    }

    private void Unsubscribe()
    {
        if (!isSubscribed || itemBoxInventory == null)
        {
            return;
        }

        itemBoxInventory.OnInventoryChanged -= HandleInventoryChanged;
        isSubscribed = false;
    }

    private void FindReferences()
    {
        if (GameSessionManager.Instance != null)
            gameSessionManager = GameSessionManager.Instance;
        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = FindAnyObjectByType<GameSessionManager>(
                FindObjectsInactive.Include
            );
        }

        if (itemBoxInventory == null)
        {
            itemBoxInventory = GetComponent<ItemBoxInventory>();
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[TownPlayerItemBoxSessionBridge] {message}", this);
        }
    }

    private void LogWarning(string message)
    {
        Debug.LogWarning(
            $"[TownPlayerItemBoxSessionBridge] {message}",
            this
        );
    }
}
