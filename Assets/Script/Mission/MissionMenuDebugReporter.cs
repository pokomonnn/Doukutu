using System.Reflection;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// ミッション受注・Mメニュー表示の原因調査用ログ出力スクリプトです。
/// 探索シーンの任意のObject、またはMissionMenuUIと同じObjectへ付けてください。
///
/// Mキーを押した時、またはContext Menuから、
/// ・GameSessionManagerに保存されている受注ミッション
/// ・全MissionManager2DのScene/状態
/// ・MissionMenuUIが実際に参照しているMissionManager2D
/// ・MissionListContent内のMissionListItemUI
/// をConsoleへ出します。
/// </summary>
[DisallowMultipleComponent]
public class MissionMenuDebugReporter : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private MissionMenuUI missionMenuUI;
    [SerializeField] private MissionManager2D missionManager;

    [Header("ログタイミング")]
    [SerializeField] private bool logOnStart = true;
    [SerializeField] private bool logWhenPressMenuKey = true;
    [SerializeField] private KeyCode menuKey = KeyCode.M;

    [Tooltip("Mキー後、MissionMenuUIが一覧を作るのを待ってからログを出すための遅延フレーム数")]
    [SerializeField, Min(0)] private int framesAfterMenuKey = 2;

    [Header("デバッグ")]
    [SerializeField] private bool showDetailedMissionList = true;

    private int pendingFrameLogs;

    private void Awake()
    {
        FindReferences();
    }

    private void Start()
    {
        FindReferences();

        if (logOnStart)
        {
            DumpDebug("Start");
        }
    }

    private void Update()
    {
        if (logWhenPressMenuKey && Input.GetKeyDown(menuKey))
        {
            pendingFrameLogs = Mathf.Max(0, framesAfterMenuKey);

            if (pendingFrameLogs == 0)
            {
                DumpDebug($"{menuKey}押下直後");
            }
        }

        if (pendingFrameLogs > 0)
        {
            pendingFrameLogs--;

            if (pendingFrameLogs == 0)
            {
                DumpDebug($"{menuKey}押下後");
            }
        }
    }

    [ContextMenu("Dump Mission Menu Debug")]
    public void DumpDebugFromContextMenu()
    {
        DumpDebug("手動実行");
    }

    public void DumpDebug(string label)
    {
        FindReferences();

        Debug.Log($"[MissionMenuDebugReporter] ===== {label} / ActiveScene={SceneManager.GetActiveScene().name} =====", this);

        DumpGameSession();
        DumpAllMissionManagers();
        DumpMissionMenu();
    }

    private void DumpGameSession()
    {
        GameSessionManager session = GameSessionManager.Instance;

        if (session == null)
        {
            session = FindAnyObjectByType<GameSessionManager>(FindObjectsInactive.Include);
        }

        if (session == null)
        {
            Debug.LogWarning("[MissionMenuDebugReporter] GameSessionManagerが見つかりません。町で受注した情報が保持されていない可能性があります。", this);
            return;
        }

        Debug.Log($"[MissionMenuDebugReporter] GameSessionManager={GetObjectPath(session.gameObject)} / Scene={session.gameObject.scene.name} / {session.GetMissionSessionSummary()}", session);
    }

    private void DumpAllMissionManagers()
    {
        MissionManager2D[] managers = FindObjectsByType<MissionManager2D>(FindObjectsInactive.Include);

        Debug.Log($"[MissionMenuDebugReporter] MissionManager2D一覧: {managers.Length}件", this);

        for (int i = 0; i < managers.Length; i++)
        {
            MissionManager2D manager = managers[i];

            if (manager == null)
            {
                continue;
            }

            Debug.Log($"[MissionMenuDebugReporter] Manager[{i}] {GetObjectPath(manager.gameObject)} / Scene={manager.gameObject.scene.name} / Active={manager.gameObject.activeInHierarchy} / Enabled={manager.enabled} / MissionCount={manager.MissionCount} / HasTracked={manager.HasTrackedMission} / TrackedIndex={manager.TrackedMissionIndex}", manager);

            if (!showDetailedMissionList)
            {
                continue;
            }

            for (int m = 0; m < manager.MissionCount; m++)
            {
                MissionDefinition2D mission = manager.GetMissionDefinition(m);

                if (mission == null)
                {
                    Debug.Log($"[MissionMenuDebugReporter]   Mission[{m}] null", manager);
                    continue;
                }

                Debug.Log($"[MissionMenuDebugReporter]   Mission[{m}] {mission.DisplayName} / MissionId={mission.MissionId} / State={manager.GetMissionState(m)} / Progress={manager.GetMissionProgress(m)}/{manager.GetMissionRequiredAmount(m)} / Tracked={manager.IsTrackedMission(m)}", manager);
            }
        }
    }

    private void DumpMissionMenu()
    {
        if (missionMenuUI == null)
        {
            Debug.LogWarning("[MissionMenuDebugReporter] MissionMenuUIが見つかりません。MメニューのObjectにこのスクリプトを付けるか、MissionMenuUIを指定してください。", this);
            return;
        }

        MissionManager2D menuManager = GetPrivateField<MissionManager2D>(missionMenuUI, "missionManager");
        Transform missionListContent = GetPrivateField<Transform>(missionMenuUI, "missionListContent");

        Debug.Log($"[MissionMenuDebugReporter] MissionMenuUI={GetObjectPath(missionMenuUI.gameObject)} / Scene={missionMenuUI.gameObject.scene.name} / IsOpen={missionMenuUI.IsOpen} / SelectedIndex={missionMenuUI.SelectedMissionIndex}", missionMenuUI);

        if (menuManager == null)
        {
            Debug.LogWarning("[MissionMenuDebugReporter] MissionMenuUIのMissionManager参照がnullです。Inspectorで探索シーンのMissionManager2Dを直接入れてください。", missionMenuUI);
        }
        else
        {
            Debug.Log($"[MissionMenuDebugReporter] MissionMenuUIが参照中のManager={GetObjectPath(menuManager.gameObject)} / Scene={menuManager.gameObject.scene.name} / MissionCount={menuManager.MissionCount}", menuManager);
        }

        if (missionManager != null && menuManager != null && missionManager != menuManager)
        {
            Debug.LogWarning($"[MissionMenuDebugReporter] このDebugReporterのMissionManagerとMissionMenuUIのManagerが違います。Reporter={GetObjectPath(missionManager.gameObject)} / Menu={GetObjectPath(menuManager.gameObject)}", this);
        }

        if (missionListContent == null)
        {
            Debug.LogWarning("[MissionMenuDebugReporter] MissionMenuUIのMissionListContentがnullです。MissionScrollView > Viewport > Contentを設定してください。", missionMenuUI);
            return;
        }

        MissionListItemUI[] items = missionListContent.GetComponentsInChildren<MissionListItemUI>(true);

        Debug.Log($"[MissionMenuDebugReporter] MissionListContent={GetObjectPath(missionListContent.gameObject)} / ListItem={items.Length}件", missionListContent);

        for (int i = 0; i < items.Length; i++)
        {
            MissionListItemUI item = items[i];

            if (item == null)
            {
                continue;
            }

            string missionText = "Mission取得不可";
            string sessionText = "Session未確認";
            MissionProgressState2D state = MissionProgressState2D.Inactive;

            if (menuManager != null && item.MissionIndex >= 0)
            {
                MissionDefinition2D mission = menuManager.GetMissionDefinition(item.MissionIndex);

                if (mission != null)
                {
                    state = menuManager.GetMissionState(item.MissionIndex);
                    missionText = $"{mission.DisplayName} / MissionId={mission.MissionId} / ManagerState={state}";

                    GameSessionManager session = GameSessionManager.Instance;
                    if (session == null)
                    {
                        session = FindAnyObjectByType<GameSessionManager>(FindObjectsInactive.Include);
                    }

                    MissionSessionData data;
                    if (session != null && session.TryGetMissionSession(mission.MissionId, out data) && data != null)
                    {
                        sessionText = $"SessionState={data.State} / Progress={data.Progress}/{data.RequiredAmount}";
                    }
                    else
                    {
                        sessionText = session == null ? "GameSessionManagerなし" : "Session保存なし";
                    }
                }
            }

            Debug.Log($"[MissionMenuDebugReporter]   ListItem[{i}] {item.name} / Active={item.gameObject.activeSelf} / MissionIndex={item.MissionIndex} / {missionText} / {sessionText}", item);

            if (state == MissionProgressState2D.Inactive && sessionText.Contains("InProgress"))
            {
                Debug.LogWarning("[MissionMenuDebugReporter]   ↑ Sessionでは受注済みなのに、MissionMenuUIが見ているManagerではInactiveです。MissionSessionBridgeが別のMissionManager2Dへ復元しているか、MissionMenuUIのMissionManager参照が別Objectです。", item);
            }
        }
    }

    private void FindReferences()
    {
        if (missionMenuUI == null)
        {
            missionMenuUI = GetComponent<MissionMenuUI>();
        }

        if (missionMenuUI == null)
        {
            missionMenuUI = FindAnyObjectByType<MissionMenuUI>(FindObjectsInactive.Include);
        }

        if (missionManager == null)
        {
            missionManager = GetComponent<MissionManager2D>();
        }

        if (missionManager == null && missionMenuUI != null)
        {
            missionManager = GetPrivateField<MissionManager2D>(missionMenuUI, "missionManager");
        }

        if (missionManager == null)
        {
            MissionManager2D[] managers = FindObjectsByType<MissionManager2D>(FindObjectsInactive.Include);
            string sceneName = missionMenuUI != null ? missionMenuUI.gameObject.scene.name : gameObject.scene.name;

            foreach (MissionManager2D manager in managers)
            {
                if (manager != null && manager.gameObject.scene.name == sceneName)
                {
                    missionManager = manager;
                    break;
                }
            }

            if (missionManager == null && managers.Length > 0)
            {
                missionManager = managers[0];
            }
        }
    }

    private static T GetPrivateField<T>(object target, string fieldName) where T : class
    {
        if (target == null)
        {
            return null;
        }

        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);

        if (field == null)
        {
            return null;
        }

        return field.GetValue(target) as T;
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
