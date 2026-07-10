using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// MissionMenuUIの一覧で、未受注(Inactive)のミッションを非表示にします。
/// MissionManager2Dには受注候補を登録したまま、Mメニューには受注済みだけ出したい時に使います。
///
/// デバッグ強化版：
/// ・MissionMenuUIが参照しているMissionManager2Dを優先して使います。
/// ・Choice/List Content違い、別MissionManager2D参照、MissionId照合失敗をConsoleに出します。
/// </summary>
[DisallowMultipleComponent]
public class MissionAcceptedOnlyMenuFilter : MonoBehaviour
{
    [Header("参照")]
    [Tooltip("未設定なら同じObject、またはシーン内から自動取得します")]
    [SerializeField] private MissionMenuUI missionMenuUI;

    [Tooltip("未設定ならMissionMenuUIが参照しているMissionManager2Dを優先して使います")]
    [SerializeField] private MissionManager2D missionManager;

    [Tooltip("MissionScrollView > Viewport > Content を設定します。未設定ならMissionMenuUIの参照を反射で読み取ります")]
    [SerializeField] private Transform missionListContent;

    [Header("表示対象")]
    [Tooltip("進行中のミッションをMメニューへ表示します。通常はONです")]
    [SerializeField] private bool showInProgressMissions = true;

    [Tooltip("達成済みミッションもMメニューへ残します")]
    [SerializeField] private bool showCompletedMissions = true;

    [Tooltip("メニューが閉じている間もフィルターを実行します。通常はOFFでOKです")]
    [SerializeField] private bool filterEvenWhenMenuClosed;

    [Header("デバッグ")]
    [SerializeField] private bool showDebugLogs = true;

    [Tooltip("ONにすると、Mメニューを開いた時に各ミッションの表示判定を1件ずつ出します")]
    [SerializeField] private bool verboseMissionLogs = true;

    [Tooltip("同じログを毎フレーム出さないため、状態が変わった時だけ概要ログを出します")]
    [SerializeField] private bool logOnlyWhenCountsChanged = true;

    private int lastVisibleCount = -1;
    private int lastHiddenCount = -1;
    private int lastItemCount = -1;
    private bool lastMenuOpen;

    private void Awake()
    {
        FindReferences(true);
    }

    private void OnEnable()
    {
        FindReferences(true);
        ApplyFilter();
    }

    private void LateUpdate()
    {
        FindReferences(false);

        if (missionMenuUI == null || missionManager == null || missionListContent == null)
        {
            if (showDebugLogs)
            {
                WarnMissingReferencesOnce();
            }
            return;
        }

        bool isMenuOpen = missionMenuUI.IsOpen;

        if (!filterEvenWhenMenuClosed && !isMenuOpen)
        {
            lastMenuOpen = false;
            return;
        }

        bool openedThisFrame = isMenuOpen && !lastMenuOpen;
        lastMenuOpen = isMenuOpen;

        ApplyFilter(openedThisFrame);
    }

    [ContextMenu("Apply Mission Accepted Only Filter")]
    public void ApplyFilter()
    {
        ApplyFilter(true);
    }

    private void ApplyFilter(bool forceVerboseLog)
    {
        if (missionManager == null || missionListContent == null)
        {
            return;
        }

        MissionListItemUI[] items = missionListContent.GetComponentsInChildren<MissionListItemUI>(true);

        int visibleCount = 0;
        int hiddenCount = 0;

        if (showDebugLogs && forceVerboseLog)
        {
            Debug.Log($"[MissionAcceptedOnlyMenuFilter] --- フィルター開始 --- Menu={GetObjectPath(missionMenuUI != null ? missionMenuUI.gameObject : null)} / Manager={GetObjectPath(missionManager.gameObject)} / ManagerScene={missionManager.gameObject.scene.name} / Content={GetObjectPath(missionListContent.gameObject)} / ItemCount={items.Length}", this);
            DumpSessionSummary();
            DumpManagerSummary();
        }

        if (items.Length == 0)
        {
            if (showDebugLogs && forceVerboseLog)
            {
                Debug.LogWarning("[MissionAcceptedOnlyMenuFilter] MissionListContent内にMissionListItemUIが見つかりません。Contentの指定が違うか、MissionMenuUIがまだ一覧を生成していません。MissionScrollView > Viewport > Content を指定してください。", this);
            }
        }

        foreach (MissionListItemUI item in items)
        {
            if (item == null)
            {
                continue;
            }

            MissionDisplayDecision decision = GetMissionDisplayDecision(item.MissionIndex);

            if (item.gameObject.activeSelf != decision.ShouldShow)
            {
                item.gameObject.SetActive(decision.ShouldShow);
            }

            if (decision.ShouldShow)
            {
                visibleCount++;
            }
            else
            {
                hiddenCount++;
            }

            if (showDebugLogs && verboseMissionLogs && forceVerboseLog)
            {
                Debug.Log(
                    $"[MissionAcceptedOnlyMenuFilter] Item={item.name} / Index={item.MissionIndex} / Mission={decision.MissionName} / MissionId={decision.MissionId} / ManagerState={decision.ManagerState} / SessionState={decision.SessionStateText} / 表示={decision.ShouldShow} / 理由={decision.Reason}",
                    item
                );
            }
        }

        RectTransform contentRect = missionListContent as RectTransform;

        if (contentRect != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        }

        bool countsChanged = visibleCount != lastVisibleCount || hiddenCount != lastHiddenCount || items.Length != lastItemCount;

        if (showDebugLogs && (!logOnlyWhenCountsChanged || countsChanged || forceVerboseLog))
        {
            Debug.Log(
                $"[MissionAcceptedOnlyMenuFilter] 結果: 受注済み表示={visibleCount}件 / 非表示={hiddenCount}件 / ListItem={items.Length}件 / Manager={missionManager.name} / Scene={missionManager.gameObject.scene.name}",
                this
            );
        }

        lastVisibleCount = visibleCount;
        lastHiddenCount = hiddenCount;
        lastItemCount = items.Length;
    }

    private MissionDisplayDecision GetMissionDisplayDecision(int missionIndex)
    {
        MissionDisplayDecision decision = new MissionDisplayDecision
        {
            MissionName = "未取得",
            MissionId = "未取得",
            SessionStateText = "未確認",
            ManagerState = MissionProgressState2D.Inactive,
            ShouldShow = false,
            Reason = "初期値"
        };

        if (missionIndex < 0 || missionManager == null)
        {
            decision.Reason = "MissionIndexが不正、またはMissionManager2Dがnull";
            return decision;
        }

        MissionDefinition2D mission = missionManager.GetMissionDefinition(missionIndex);

        if (mission == null)
        {
            decision.Reason = "MissionDefinitionがnull";
            return decision;
        }

        decision.MissionName = mission.DisplayName;
        decision.MissionId = mission.MissionId;
        decision.ManagerState = missionManager.GetMissionState(missionIndex);

        GameSessionManager session = GameSessionManager.Instance;
        if (session == null)
        {
            session = FindAnyObjectByType<GameSessionManager>(FindObjectsInactive.Include);
        }

        MissionSessionData sessionData = null;
        bool hasSessionData = session != null && session.TryGetMissionSession(mission.MissionId, out sessionData);

        if (hasSessionData && sessionData != null)
        {
            decision.SessionStateText = sessionData.State.ToString();
        }
        else
        {
            decision.SessionStateText = session == null ? "GameSessionManagerなし" : "保存データなし";
        }

        switch (decision.ManagerState)
        {
            case MissionProgressState2D.InProgress:
                decision.ShouldShow = showInProgressMissions;
                decision.Reason = decision.ShouldShow
                    ? "MissionManager2D上でInProgress"
                    : "InProgress表示がOFF";
                return decision;

            case MissionProgressState2D.Completed:
                decision.ShouldShow = showCompletedMissions;
                decision.Reason = decision.ShouldShow
                    ? "MissionManager2D上でCompleted"
                    : "Completed表示がOFF";
                return decision;

            default:
                decision.ShouldShow = false;
                if (hasSessionData && sessionData != null && sessionData.State == MissionSessionState.InProgress)
                {
                    decision.Reason = "Sessionでは受注済みだが、このMissionManager2DではInactive。MissionSessionBridgeがこのManagerへ復元していない可能性があります";
                }
                else
                {
                    decision.Reason = "未受注Inactiveのため非表示";
                }
                return decision;
        }
    }

    private void FindReferences(bool log)
    {
        if (missionMenuUI == null)
        {
            missionMenuUI = GetComponent<MissionMenuUI>();
        }

        if (missionMenuUI == null)
        {
            missionMenuUI = FindAnyObjectByType<MissionMenuUI>(FindObjectsInactive.Include);
        }

        MissionManager2D menuManager = GetPrivateField<MissionManager2D>(missionMenuUI, "missionManager");

        // MissionMenuUIが実際に使っているManagerを最優先する。
        if (menuManager != null && missionManager != menuManager)
        {
            if (log && showDebugLogs && missionManager != null)
            {
                Debug.LogWarning($"[MissionAcceptedOnlyMenuFilter] FilterのMissionManagerとMissionMenuUIのMissionManagerが違うため、Menu側に合わせます。Filter={GetObjectPath(missionManager.gameObject)} / Menu={GetObjectPath(menuManager.gameObject)}", this);
            }

            missionManager = menuManager;
        }

        if (missionManager == null)
        {
            missionManager = FindBestMissionManagerForMenuScene(log);
        }

        Transform menuContent = GetPrivateField<Transform>(missionMenuUI, "missionListContent");

        if (menuContent != null && missionListContent != menuContent)
        {
            if (log && showDebugLogs && missionListContent != null)
            {
                Debug.LogWarning($"[MissionAcceptedOnlyMenuFilter] FilterのMissionListContentとMissionMenuUIのContentが違うため、Menu側に合わせます。Filter={GetObjectPath(missionListContent.gameObject)} / Menu={GetObjectPath(menuContent.gameObject)}", this);
            }

            missionListContent = menuContent;
        }

        if (missionListContent == null && missionMenuUI != null)
        {
            Transform scrollView = FindChildRecursive(missionMenuUI.transform, "MissionScrollView");

            if (scrollView != null)
            {
                Transform content = scrollView.Find("Viewport/Content");

                if (content != null)
                {
                    missionListContent = content;
                }
            }
        }
    }

    private MissionManager2D FindBestMissionManagerForMenuScene(bool log)
    {
        MissionManager2D[] managers = FindObjectsByType<MissionManager2D>(FindObjectsInactive.Include);

        if (managers == null || managers.Length == 0)
        {
            return null;
        }

        string menuSceneName = missionMenuUI != null ? missionMenuUI.gameObject.scene.name : string.Empty;
        MissionManager2D sameSceneManager = null;
        MissionManager2D activeManager = null;

        foreach (MissionManager2D manager in managers)
        {
            if (manager == null)
            {
                continue;
            }

            if (sameSceneManager == null && manager.gameObject.scene.name == menuSceneName)
            {
                sameSceneManager = manager;
            }

            if (activeManager == null && manager.gameObject.activeInHierarchy && manager.enabled)
            {
                activeManager = manager;
            }
        }

        MissionManager2D selected = sameSceneManager != null
            ? sameSceneManager
            : activeManager != null
                ? activeManager
                : managers[0];

        if (log && showDebugLogs)
        {
            Debug.Log($"[MissionAcceptedOnlyMenuFilter] MissionManager2D自動選択: 候補={managers.Length}件 / MenuScene={menuSceneName} / 選択={GetObjectPath(selected.gameObject)} / 選択Scene={selected.gameObject.scene.name}", this);
        }

        return selected;
    }

    private void WarnMissingReferencesOnce()
    {
        if (missionMenuUI == null)
        {
            Debug.LogWarning("[MissionAcceptedOnlyMenuFilter] MissionMenuUIが見つかりません。MissionMenuUIと同じObjectへ付けるのがおすすめです。", this);
        }

        if (missionManager == null)
        {
            Debug.LogWarning("[MissionAcceptedOnlyMenuFilter] MissionManager2Dが見つかりません。探索シーンのMissionManager2DをMissionMenuUIへ設定してください。", this);
        }

        if (missionListContent == null)
        {
            Debug.LogWarning("[MissionAcceptedOnlyMenuFilter] MissionListContentが見つかりません。MissionScrollView > Viewport > Contentを指定してください。", this);
        }
    }

    private void DumpSessionSummary()
    {
        GameSessionManager session = GameSessionManager.Instance;
        if (session == null)
        {
            session = FindAnyObjectByType<GameSessionManager>(FindObjectsInactive.Include);
        }

        if (session == null)
        {
            Debug.LogWarning("[MissionAcceptedOnlyMenuFilter] GameSessionManagerが見つかりません。町で受注した情報を確認できません。", this);
            return;
        }

        Debug.Log($"[MissionAcceptedOnlyMenuFilter] {session.GetMissionSessionSummary()}", session);
    }

    private void DumpManagerSummary()
    {
        if (missionManager == null)
        {
            return;
        }

        Debug.Log($"[MissionAcceptedOnlyMenuFilter] MissionManager状態: {GetObjectPath(missionManager.gameObject)} / Scene={missionManager.gameObject.scene.name} / MissionCount={missionManager.MissionCount} / HasTracked={missionManager.HasTrackedMission} / TrackedIndex={missionManager.TrackedMissionIndex}", missionManager);

        for (int i = 0; i < missionManager.MissionCount; i++)
        {
            MissionDefinition2D mission = missionManager.GetMissionDefinition(i);
            if (mission == null)
            {
                Debug.Log($"[MissionAcceptedOnlyMenuFilter] ManagerMission[{i}] null", missionManager);
                continue;
            }

            Debug.Log($"[MissionAcceptedOnlyMenuFilter] ManagerMission[{i}] {mission.DisplayName} / MissionId={mission.MissionId} / State={missionManager.GetMissionState(i)} / Tracked={missionManager.IsTrackedMission(i)}", missionManager);
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

    private static Transform FindChildRecursive(Transform root, string objectName)
    {
        if (root == null)
        {
            return null;
        }

        for (int i = 0; i < root.childCount; i++)
        {
            Transform child = root.GetChild(i);

            if (child.name == objectName)
            {
                return child;
            }

            Transform found = FindChildRecursive(child, objectName);

            if (found != null)
            {
                return found;
            }
        }

        return null;
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

    private struct MissionDisplayDecision
    {
        public string MissionName;
        public string MissionId;
        public MissionProgressState2D ManagerState;
        public string SessionStateText;
        public bool ShouldShow;
        public string Reason;
    }
}
