using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// UI Buttonから指定したシーンへ移動するためのコンポーネントです。
/// シーンを読む直前に、現在シーンのPlayerInventorySessionBridgeへ保存を要求します。
/// 質屋画面が開いている場合は、会計前のアイテムを先にプレイヤーへ戻します。
/// ButtonのOn ClickへLoadConfiguredSceneを登録して使います。
/// </summary>
[DisallowMultipleComponent]
public class SceneTransitionButton : MonoBehaviour
{
    [Header("移動先")]
    [Tooltip("Build Settingsに登録済みのシーン名を入力します。例：Town_Main")]
    [SerializeField] private string targetSceneName = "Town_Main";

    [Tooltip("通常はSingleのままでOKです。現在のシーンを閉じて移動します")]
    [SerializeField] private LoadSceneMode loadSceneMode = LoadSceneMode.Single;

    [Header("インベントリ引き継ぎ")]
    [Tooltip("オンならSceneManager.LoadSceneの直前に、現在のシーンのPlayerInventorySessionBridgeからインベントリを保存します。")]
    [SerializeField] private bool captureInventoryBeforeLoad = true;

    [Tooltip("オンなら、開いている質屋画面を閉じて売却予定アイテムを戻してからシーン移動します。")]
    [SerializeField] private bool closeOpenPawnShopBeforeLoad = true;

    [Header("ミッション引き継ぎ")]
    [Tooltip("オンならSceneManager.LoadSceneの直前に、現在のシーンのMissionSessionBridgeからミッション状態を保存します。")]
    [SerializeField] private bool captureMissionsBeforeLoad = true;

    [Header("ボタン設定")]
    [Tooltip("未設定なら同じGameObjectのButtonを自動取得します")]
    [SerializeField] private Button targetButton;

    [Tooltip("ロード開始後にボタンを押せないようにします")]
    [SerializeField] private bool disableButtonWhileLoading = true;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    private bool isLoading;

    public string TargetSceneName => targetSceneName;
    public bool IsLoading => isLoading;

    private void Awake()
    {
        FindButton();
    }

    private void OnEnable()
    {
        // シーンを戻った時などに、ボタン状態を確実に戻します。
        isLoading = false;
        SetButtonInteractable(true);
    }

    /// <summary>
    /// Inspectorで設定したTarget Scene Nameへ移動します。
    /// ButtonのOn Clickへこのメソッドを登録してください。
    /// </summary>
    public void LoadConfiguredScene()
    {
        LoadSceneByName(targetSceneName);
    }

    /// <summary>
    /// 任意のシーン名を指定して移動します。
    /// 他スクリプトや別のButtonイベントからも呼べます。
    /// </summary>
    public void LoadSceneByName(string sceneName)
    {
        if (isLoading)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning(
                "[SceneTransitionButton] 移動先のScene Nameが未設定です。",
                this
            );
            return;
        }

        if (!Application.CanStreamedLevelBeLoaded(sceneName))
        {
            Debug.LogWarning(
                $"[SceneTransitionButton] シーン『{sceneName}』を読み込めません。" +
                "File > Build Settings の Scenes In Build に追加されているか確認してください。",
                this
            );
            return;
        }

        // 質屋が開いている時は、カート内アイテムを戻せなければ移動を中止する。
        // 先にPlayerInventorySessionBridgeへ保存されると、カート側のアイテムが
        // セーブ対象から漏れてしまうため、必ず保存より前に行います。
        if (closeOpenPawnShopBeforeLoad &&
            !TryCloseOpenPawnShopBeforeSceneLoad())
        {
            return;
        }

        isLoading = true;

        if (disableButtonWhileLoading)
        {
            SetButtonInteractable(false);
        }

        if (showDebugLogs)
        {
            Debug.Log(
                $"[SceneTransitionButton] シーン移動開始：{SceneManager.GetActiveScene().name} → {sceneName}",
                this
            );
        }

        if (captureInventoryBeforeLoad)
        {
            CaptureInventoryBeforeSceneLoad();
        }
        else if (showDebugLogs)
        {
            Debug.LogWarning(
                "[SceneTransitionButton] Capture Inventory Before Load がOFFです。" +
                "OnDisableでの自動保存だけに依存するため、通常はONを推奨します。",
                this
            );
        }

        if (captureMissionsBeforeLoad)
        {
            CaptureMissionsBeforeSceneLoad();
        }

        SceneManager.LoadScene(sceneName, loadSceneMode);
    }

    private bool TryCloseOpenPawnShopBeforeSceneLoad()
    {
        PawnShopUIController pawnShop =
            FindAnyObjectByType<PawnShopUIController>();

        if (pawnShop == null || !pawnShop.IsOpen)
        {
            return true;
        }

        bool closed = pawnShop.TryClosePawnShop();

        if (!closed)
        {
            Debug.LogWarning(
                "[SceneTransitionButton] 質屋の売却予定アイテムをすべて戻せないため、" +
                "シーン移動を中止しました。プレイヤーインベントリの空きを作ってから、" +
                "質屋画面を閉じてください。",
                this
            );
        }

        return closed;
    }

    private void CaptureInventoryBeforeSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        PlayerInventorySessionBridge[] allBridges =
            FindObjectsByType<PlayerInventorySessionBridge>(
                FindObjectsInactive.Include
            );

        PlayerInventorySessionBridge selectedBridge = null;
        int bridgeCountInActiveScene = 0;

        foreach (PlayerInventorySessionBridge bridge in allBridges)
        {
            if (bridge == null || bridge.gameObject.scene.handle != activeScene.handle)
            {
                continue;
            }

            bridgeCountInActiveScene++;

            if (bridge.isActiveAndEnabled && selectedBridge == null)
            {
                selectedBridge = bridge;
            }
        }

        if (bridgeCountInActiveScene == 0)
        {
            Debug.LogWarning(
                "[SceneTransitionButton] インベントリ保存をスキップしました。" +
                $"現在のScene『{activeScene.name}』にPlayerInventorySessionBridgeがありません。" +
                "移動元のPlayerまたはTownPlayerInventoryへ追加してください。",
                this
            );
            return;
        }

        if (bridgeCountInActiveScene > 1)
        {
            Debug.LogWarning(
                "[SceneTransitionButton] 現在のSceneにPlayerInventorySessionBridgeが複数あります。" +
                $"最初に見つかった有効なBridge『{(selectedBridge != null ? selectedBridge.name : "なし")}』を保存元に使います。" +
                "Player用とTownPlayerInventory用が同一Sceneに重複していないか確認してください。",
                this
            );
        }

        if (selectedBridge == null)
        {
            Debug.LogWarning(
                "[SceneTransitionButton] PlayerInventorySessionBridgeは見つかりましたが、すべて無効です。" +
                "BridgeコンポーネントとGameObjectが有効か確認してください。",
                this
            );
            return;
        }

        bool saved = selectedBridge.CaptureToSession();

        if (showDebugLogs)
        {
            Debug.Log(
                saved
                    ? $"[SceneTransitionButton] インベントリ保存成功：{selectedBridge.name}"
                    : $"[SceneTransitionButton] インベントリ保存失敗：{selectedBridge.name}。直前のInventorySessionBridgeログを確認してください。",
                this
            );
        }
    }

    private void CaptureMissionsBeforeSceneLoad()
    {
        Scene activeScene = SceneManager.GetActiveScene();

        MissionSessionBridge[] allBridges =
            FindObjectsByType<MissionSessionBridge>(
                FindObjectsInactive.Include
            );

        MissionSessionBridge selectedBridge = null;
        int bridgeCountInActiveScene = 0;

        foreach (MissionSessionBridge bridge in allBridges)
        {
            if (bridge == null || bridge.gameObject.scene.handle != activeScene.handle)
            {
                continue;
            }

            bridgeCountInActiveScene++;

            if (bridge.isActiveAndEnabled && selectedBridge == null)
            {
                selectedBridge = bridge;
            }
        }

        if (bridgeCountInActiveScene == 0)
        {
            if (showDebugLogs)
            {
                Debug.Log(
                    "[SceneTransitionButton] ミッション保存をスキップしました。" +
                    $"現在のScene『{activeScene.name}』にMissionSessionBridgeがありません。" +
                    "町だけのSceneでは通常問題ありません。探索Sceneには追加してください。",
                    this
                );
            }

            return;
        }

        if (selectedBridge == null)
        {
            Debug.LogWarning(
                "[SceneTransitionButton] MissionSessionBridgeは見つかりましたが、すべて無効です。" +
                "BridgeコンポーネントとGameObjectが有効か確認してください。",
                this
            );
            return;
        }

        bool saved = selectedBridge.CaptureToSession();

        if (showDebugLogs)
        {
            Debug.Log(
                saved
                    ? $"[SceneTransitionButton] ミッション保存成功：{selectedBridge.name}"
                    : $"[SceneTransitionButton] ミッション保存失敗：{selectedBridge.name}。直前のMissionSessionBridgeログを確認してください。",
                this
            );
        }
    }

    private void FindButton()
    {
        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }
    }

    private void SetButtonInteractable(bool interactable)
    {
        FindButton();

        if (targetButton != null)
        {
            targetButton.interactable = interactable;
        }
    }

    private void OnValidate()
    {
        targetSceneName = targetSceneName?.Trim() ?? string.Empty;

        if (targetButton == null)
        {
            targetButton = GetComponent<Button>();
        }
    }
}
