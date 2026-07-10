using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 現在シーンのMissionManager2Dと、GameSessionManagerに保存したミッション受注状態を接続します。
/// 探索シーンのMissionManager2Dと同じObject、または同じシーン内の管理Objectへ付けてください。
///
/// デバッグ強化版：
/// ・MissionManager2Dが複数ある時、同じシーン内のものを優先して選びます。
/// ・どのMissionManager2Dへ復元したか、登録MissionId、復元前後の状態をConsoleへ出します。
/// </summary>
[DisallowMultipleComponent]
public class MissionSessionBridge : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定ならGameSessionManager.Instance、またはシーン内から探します")]
    [SerializeField] private GameSessionManager gameSessionManager;

    [Tooltip("未設定なら同じObject、または同じScene内のMissionManager2Dを優先して探します")]
    [SerializeField] private MissionManager2D missionManager;

    [Header("動作")]
    [Tooltip("Start時に、GameSessionManagerの受注済みミッションをMissionManager2Dへ反映します")]
    [SerializeField] private bool restoreMissionsOnStart = true;

    [Tooltip("このObjectが無効化・破棄される時に、MissionManager2Dの進行状態をGameSessionManagerへ保存します")]
    [SerializeField] private bool captureMissionsOnDisable = true;

    [Tooltip("Start直後に1フレーム待ってから復元します。MissionManager2DやUIの初期化順を安定させます")]
    [SerializeField] private bool waitOneFrameBeforeRestore = true;

    [Header("デバッグ")]
    [SerializeField] private bool alwaysLogSessionTransfer = true;

    [Tooltip("MissionManager2Dの候補一覧、登録MissionId、復元前後の状態を詳しく出します")]
    [SerializeField] private bool verboseDebugLogs = true;

    private bool hasRestored;
    private bool hasCapturedOnDisable;

    private void Awake()
    {
        FindReferences(true);
    }

    private IEnumerator Start()
    {
        FindReferences(true);

        if (waitOneFrameBeforeRestore)
        {
            yield return null;
        }

        if (restoreMissionsOnStart)
        {
            RestoreFromSession();
        }
    }

    private void OnDisable()
    {
        if (captureMissionsOnDisable)
        {
            CaptureToSession();
        }
    }

    private void OnDestroy()
    {
        if (captureMissionsOnDisable && !hasCapturedOnDisable)
        {
            CaptureToSession();
        }
    }

    /// <summary>
    /// SceneTransitionButtonからシーン移動直前に呼べます。
    /// </summary>
    public bool CaptureToSession()
    {
        FindReferences(false);
        hasCapturedOnDisable = true;

        if (gameSessionManager == null)
        {
            LogWarning("保存失敗: GameSessionManagerが見つかりません。");
            return false;
        }

        if (missionManager == null)
        {
            LogWarning("保存失敗: MissionManager2Dが見つかりません。");
            DumpAllMissionManagers("保存失敗時");
            return false;
        }

        if (verboseDebugLogs)
        {
            Log($"保存先GameSessionManager={GetObjectPath(gameSessionManager.gameObject)} / Scene={gameSessionManager.gameObject.scene.name}");
            Log($"保存元MissionManager2D={GetObjectPath(missionManager.gameObject)} / Scene={missionManager.gameObject.scene.name}");
            DumpManagerMissions(missionManager, "保存前MissionManager状態");
        }

        bool captured = gameSessionManager.CaptureMissionsFromManager(missionManager);

        if (captured)
        {
            Log("ミッション状態をGameSessionManagerへ保存しました。");
            LogSessionSummary();
        }

        return captured;
    }

    /// <summary>
    /// GameSessionManagerに保存されている受注状態をMissionManager2Dへ反映します。
    /// </summary>
    public bool RestoreFromSession()
    {
        if (hasRestored)
        {
            Log("復元はすでに1回実行済みのためスキップしました。");
            return true;
        }

        FindReferences(true);

        if (gameSessionManager == null)
        {
            LogWarning("復元失敗: GameSessionManagerが見つかりません。Town_Mainを単体再生していないか、開始シーンにGameSessionManagerがあるか確認してください。");
            return false;
        }

        if (missionManager == null)
        {
            LogWarning("復元失敗: MissionManager2Dが見つかりません。探索シーンのMissionManager2DにMissionSessionBridgeを付けてください。");
            DumpAllMissionManagers("復元失敗時");
            return false;
        }

        if (verboseDebugLogs)
        {
            Log($"復元元GameSessionManager={GetObjectPath(gameSessionManager.gameObject)} / Scene={gameSessionManager.gameObject.scene.name}");
            Log($"復元先MissionManager2D={GetObjectPath(missionManager.gameObject)} / Scene={missionManager.gameObject.scene.name}");
            LogSessionSummary();
            DumpAllMissionManagers("復元前の全MissionManager2D");
            DumpManagerMissions(missionManager, "復元前MissionManager状態");
        }

        bool restored = gameSessionManager.RestoreMissionsToManager(missionManager);
        hasRestored = true;

        if (restored)
        {
            Log("保存済みミッションをMissionManager2Dへ反映しました。");
        }
        else
        {
            LogWarning("RestoreMissionsToManagerは実行されましたが、反映件数が0の可能性があります。MissionId未登録・保存データなしを確認してください。");
        }

        if (verboseDebugLogs)
        {
            DumpManagerMissions(missionManager, "復元後MissionManager状態");
        }

        return restored;
    }

    [ContextMenu("Capture Missions To Session")]
    private void CaptureToSessionFromContextMenu()
    {
        CaptureToSession();
    }

    [ContextMenu("Restore Missions From Session")]
    private void RestoreFromSessionFromContextMenu()
    {
        hasRestored = false;
        RestoreFromSession();
    }

    [ContextMenu("Dump Mission Session Debug")]
    private void DumpMissionSessionDebugFromContextMenu()
    {
        FindReferences(true);
        LogSessionSummary();
        DumpAllMissionManagers("手動デバッグ");
        if (missionManager != null)
        {
            DumpManagerMissions(missionManager, "現在選択中のMissionManager状態");
        }
    }

    private void FindReferences(bool logCandidates)
    {
        if (gameSessionManager == null)
        {
            gameSessionManager = GameSessionManager.Instance;
        }

        if (gameSessionManager == null)
        {
            gameSessionManager = FindAnyObjectByType<GameSessionManager>(FindObjectsInactive.Include);
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MissionManager2D>();
        }

        if (missionManager == null)
        {
            missionManager = FindBestMissionManagerForThisScene(logCandidates);
        }
    }

    private MissionManager2D FindBestMissionManagerForThisScene(bool logCandidates)
    {
        MissionManager2D[] managers = FindObjectsByType<MissionManager2D>(FindObjectsInactive.Include);

        if (managers == null || managers.Length == 0)
        {
            return null;
        }

        Scene myScene = gameObject.scene;
        MissionManager2D firstActiveInSameScene = null;
        MissionManager2D firstInSameScene = null;
        MissionManager2D firstActive = null;

        foreach (MissionManager2D manager in managers)
        {
            if (manager == null)
            {
                continue;
            }

            bool sameScene = manager.gameObject.scene == myScene;
            bool active = manager.gameObject.activeInHierarchy && manager.enabled;

            if (sameScene && active && firstActiveInSameScene == null)
            {
                firstActiveInSameScene = manager;
            }

            if (sameScene && firstInSameScene == null)
            {
                firstInSameScene = manager;
            }

            if (active && firstActive == null)
            {
                firstActive = manager;
            }
        }

        MissionManager2D selected = firstActiveInSameScene != null
            ? firstActiveInSameScene
            : firstInSameScene != null
                ? firstInSameScene
                : firstActive != null
                    ? firstActive
                    : managers[0];

        if (logCandidates && verboseDebugLogs)
        {
            Log($"MissionManager2D候補={managers.Length}件 / BridgeScene={myScene.name} / 選択={GetObjectPath(selected.gameObject)} / 選択Scene={selected.gameObject.scene.name}");
            DumpAllMissionManagers("候補確認");
        }

        return selected;
    }

    private void DumpAllMissionManagers(string label)
    {
        if (!verboseDebugLogs)
        {
            return;
        }

        MissionManager2D[] managers = FindObjectsByType<MissionManager2D>(FindObjectsInactive.Include);
        Log($"--- MissionManager2D一覧: {label} / 件数={managers.Length} ---");

        for (int i = 0; i < managers.Length; i++)
        {
            MissionManager2D manager = managers[i];
            if (manager == null)
            {
                continue;
            }

            Log($"[{i}] {GetObjectPath(manager.gameObject)} / Scene={manager.gameObject.scene.name} / Active={manager.gameObject.activeInHierarchy} / Enabled={manager.enabled} / MissionCount={manager.MissionCount} / HasTracked={manager.HasTrackedMission} / TrackedIndex={manager.TrackedMissionIndex}");
        }
    }

    private void DumpManagerMissions(MissionManager2D manager, string label)
    {
        if (!verboseDebugLogs || manager == null)
        {
            return;
        }

        Log($"--- {label}: {GetObjectPath(manager.gameObject)} / Scene={manager.gameObject.scene.name} ---");

        if (manager.MissionCount <= 0)
        {
            Log("MissionManager2DのMissionsが0件です。InspectorのMissionsにMissionDefinition2Dを登録してください。");
            return;
        }

        for (int i = 0; i < manager.MissionCount; i++)
        {
            MissionDefinition2D mission = manager.GetMissionDefinition(i);
            if (mission == null)
            {
                Log($"Mission[{i}] = null");
                continue;
            }

            Log($"Mission[{i}] {mission.DisplayName} / MissionId={mission.MissionId} / State={manager.GetMissionState(i)} / Progress={manager.GetMissionProgress(i)}/{manager.GetMissionRequiredAmount(i)} / Tracked={manager.IsTrackedMission(i)}");
        }
    }

    private void LogSessionSummary()
    {
        if (!verboseDebugLogs)
        {
            return;
        }

        if (gameSessionManager == null)
        {
            LogWarning("GameSessionManagerがnullのため、セッション概要を表示できません。");
            return;
        }

        Log(gameSessionManager.GetMissionSessionSummary());
    }

    private void Log(string message)
    {
        if (!alwaysLogSessionTransfer && !verboseDebugLogs)
        {
            return;
        }

        Debug.Log($"[MissionSessionBridge] {message}", this);
    }

    private void LogWarning(string message)
    {
        if (!alwaysLogSessionTransfer && !verboseDebugLogs)
        {
            return;
        }

        Debug.LogWarning($"[MissionSessionBridge] {message}", this);
    }

    private static string GetObjectPath(GameObject target)
    {
        if (target == null)
        {
            return "null";
        }

        string path = target.name;
        Transform current = target.transform.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }
}
