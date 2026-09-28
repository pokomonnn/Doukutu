using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Town_Mainのプレイヤー専用保管箱を開閉します。
/// 既存のItemBoxUIControllerをそのまま利用するため、
/// プレイヤーInventory ⇔ 保管箱のドラッグ＆ドロップも既存仕様を使えます。
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(ItemBoxInventory))]
[RequireComponent(typeof(TownPlayerItemBoxSessionBridge))]
public class TownPlayerItemBoxController : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("CanvasにあるItemBoxUIControllerです。未設定なら自動検索します。")]
    [SerializeField] private ItemBoxUIController itemBoxUIController;

    [Tooltip("このObjectのItemBoxInventoryです。")]
    [SerializeField] private ItemBoxInventory itemBoxInventory;

    [Tooltip("このObjectの保管箱セッションBridgeです。")]
    [SerializeField] private TownPlayerItemBoxSessionBridge sessionBridge;

    [Header("ボタン")]
    [Tooltip("町で保管箱を開くButtonです。未設定ならUnityEventからOpenBoxを直接呼んでもOKです。")]
    [SerializeField] private Button openButton;

    [Tooltip("保管箱画面を閉じるButtonです。未設定ならUnityEventからCloseBoxを直接呼んでもOKです。")]
    [SerializeField] private Button closeButton;

    [Header("動作")]
    [Tooltip("閉じる時に保管箱の最新状態をGameSessionManagerへ保存します。")]
    [SerializeField] private bool captureWhenClosing = true;

    [Header("診断ログ")]
    [SerializeField] private bool showDebugLogs;

    private void Awake()
    {
        FindReferences();
        SetupButtons();
    }

    private void OnEnable()
    {
        FindReferences();
        SetupButtons();
    }

    private void OnDisable()
    {
        RemoveButtonListeners();
    }

    public void OpenBox()
    {
        FindReferences();

        if (itemBoxUIController == null || itemBoxInventory == null)
        {
            Debug.LogWarning(
                "[TownPlayerItemBoxController] ItemBoxUIControllerまたはItemBoxInventoryが未設定です。",
                this
            );
            return;
        }

        sessionBridge?.EnsureSessionReady();
        itemBoxUIController.Open(itemBoxInventory);

        Log("プレイヤー保管箱を開きました。");
    }

    public void CloseBox()
    {
        FindReferences();

        if (captureWhenClosing)
        {
            sessionBridge?.CaptureToSession();
        }

        if (itemBoxUIController != null &&
            itemBoxUIController.CurrentItemBox == itemBoxInventory)
        {
            itemBoxUIController.Close();
        }

        Log("プレイヤー保管箱を閉じました。");
    }

    private void FindReferences()
    {
        if (itemBoxInventory == null)
        {
            itemBoxInventory = GetComponent<ItemBoxInventory>();
        }

        if (sessionBridge == null)
        {
            sessionBridge = GetComponent<TownPlayerItemBoxSessionBridge>();
        }

        if (itemBoxUIController == null)
        {
            itemBoxUIController = FindAnyObjectByType<ItemBoxUIController>(
                FindObjectsInactive.Include
            );
        }
    }

    private void SetupButtons()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenBox);
            openButton.onClick.AddListener(OpenBox);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseBox);
            closeButton.onClick.AddListener(CloseBox);
        }
    }

    private void RemoveButtonListeners()
    {
        if (openButton != null)
        {
            openButton.onClick.RemoveListener(OpenBox);
        }

        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(CloseBox);
        }
    }

    private void Log(string message)
    {
        if (showDebugLogs)
        {
            Debug.Log($"[TownPlayerItemBoxController] {message}", this);
        }
    }
}
