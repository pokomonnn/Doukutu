using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 救出人数Milestoneや特殊NPC救出でONになったStory Flagに応じて、
/// Town内のObjectを表示／非表示にします。
///
/// シーンをまたいだ際にTown側のGameObject参照が破棄された場合でも、
/// 保存しておいたScene名＋Hierarchy Pathを使ってTown再読込時に自動再接続します。
/// </summary>
[DisallowMultipleComponent]
public class TownRescueUnlockObject2D : MonoBehaviour
{
    [Header("解放条件")]
    [InspectorName("必要Story Flags")]
    [SerializeField]
    private List<string> requiredStoryFlags = new List<string>();

    [InspectorName("すべてのFlagが必要")]
    [SerializeField]
    private bool requireAllFlags = true;

    [Header("解放時のObject切替")]
    [InspectorName("解放時に表示")]
    [SerializeField]
    private GameObject[] objectsToEnable;

    [InspectorName("解放時に非表示")]
    [SerializeField]
    private GameObject[] objectsToDisable;

    [Header("シーン再読込対応")]
    [InspectorName("シーン読込時に参照を自動再接続")]
    [SerializeField]
    private bool rebindSceneObjectsOnSceneLoaded = true;

    [InspectorName("Session待機フレーム数")]
    [Min(1)]
    [SerializeField]
    private int sessionWaitFrames = 30;

    [Header("デバッグ")]
    [InspectorName("解放ログを表示")]
    [SerializeField]
    private bool showDebugLogs;

    // Inspectorで設定されたScene Objectの場所を保持します。
    // TownRescueUnlockObject2D自体がDontDestroyOnLoadで残った場合でも、
    // Townへ戻った時に同じHierarchy Pathから再取得できます。
    [SerializeField, HideInInspector]
    private string[] enableTargetSceneNames;

    [SerializeField, HideInInspector]
    private string[] enableTargetHierarchyPaths;

    [SerializeField, HideInInspector]
    private string[] disableTargetSceneNames;

    [SerializeField, HideInInspector]
    private string[] disableTargetHierarchyPaths;

    private GameSessionManager subscribedSession;
    private Coroutine refreshRoutine;

    public bool IsUnlocked => EvaluateUnlocked();

    private void Awake()
    {
        // OnValidateが走っていないケースにも備えて、存在する参照だけ記録します。
        CacheSceneObjectMetadata(false);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        SceneManager.sceneLoaded += HandleSceneLoaded;

        Subscribe();
        RestartDelayedRefresh();
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
        Unsubscribe();

        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
            refreshRoutine = null;
        }
    }

    [ContextMenu("救出解放状態を更新")]
    public void Refresh()
    {
        // Townへ戻った直後など、参照がMissingになっている場合は先に復旧します。
        if (rebindSceneObjectsOnSceneLoaded)
        {
            RebindTargetsFromLoadedScenes();
        }

        bool unlocked = EvaluateUnlocked();

        ApplyActiveState(objectsToEnable, unlocked);
        ApplyActiveState(objectsToDisable, !unlocked);

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TownRescueUnlockObject2D] {name}: 解放={unlocked}",
                this
            );
        }
    }

    private static void ApplyActiveState(GameObject[] targets, bool active)
    {
        if (targets == null) return;

        foreach (GameObject target in targets)
        {
            if (target != null && target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }

    private bool EvaluateUnlocked()
    {
        GameSessionManager session = GetSession();

        if (session == null ||
            requiredStoryFlags == null ||
            requiredStoryFlags.Count == 0)
        {
            return false;
        }

        int validFlagCount = 0;
        int matchedCount = 0;

        foreach (string rawFlag in requiredStoryFlags)
        {
            if (string.IsNullOrWhiteSpace(rawFlag)) continue;

            validFlagCount++;

            if (session.HasStoryFlag(rawFlag.Trim()))
            {
                matchedCount++;
            }
        }

        if (validFlagCount <= 0) return false;

        return requireAllFlags
            ? matchedCount >= validFlagCount
            : matchedCount > 0;
    }

    private GameSessionManager GetSession()
    {
        if (GameSessionManager.Instance != null)
        {
            return GameSessionManager.Instance;
        }

        return FindAnyObjectByType<GameSessionManager>(
            FindObjectsInactive.Include
        );
    }

    private void Subscribe()
    {
        GameSessionManager current = GetSession();

        if (subscribedSession == current)
        {
            return;
        }

        if (subscribedSession != null)
        {
            subscribedSession.StorySessionChanged -= HandleStoryChanged;
        }

        subscribedSession = current;

        if (subscribedSession != null)
        {
            subscribedSession.StorySessionChanged -= HandleStoryChanged;
            subscribedSession.StorySessionChanged += HandleStoryChanged;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedSession != null)
        {
            subscribedSession.StorySessionChanged -= HandleStoryChanged;
            subscribedSession = null;
        }
    }

    private void HandleStoryChanged()
    {
        Refresh();
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (rebindSceneObjectsOnSceneLoaded)
        {
            RebindTargetsForScene(scene);
        }

        // Scene読込直後はGameSessionManager側の復元順序が後になる可能性があるため、
        // 数フレーム待ちながらSessionを探し直します。
        RestartDelayedRefresh();
    }

    private void RestartDelayedRefresh()
    {
        if (!isActiveAndEnabled) return;

        if (refreshRoutine != null)
        {
            StopCoroutine(refreshRoutine);
        }

        refreshRoutine = StartCoroutine(DelayedRefreshRoutine());
    }

    private IEnumerator DelayedRefreshRoutine()
    {
        int waitFrames = Mathf.Max(1, sessionWaitFrames);

        for (int i = 0; i < waitFrames; i++)
        {
            Subscribe();

            if (GetSession() != null)
            {
                // Scene内Objectが生成済みになるよう、さらに1フレーム待ってから適用します。
                yield return null;

                if (rebindSceneObjectsOnSceneLoaded)
                {
                    RebindTargetsFromLoadedScenes();
                }

                Subscribe();
                Refresh();
                refreshRoutine = null;
                yield break;
            }

            yield return null;
        }

        if (showDebugLogs)
        {
            Debug.LogWarning(
                $"[TownRescueUnlockObject2D] {name}: " +
                $"GameSessionManagerを{waitFrames}フレーム以内に取得できませんでした。",
                this
            );
        }

        // Sessionが見つからなくてもScene Objectの再接続だけは試します。
        if (rebindSceneObjectsOnSceneLoaded)
        {
            RebindTargetsFromLoadedScenes();
        }

        refreshRoutine = null;
    }

    private void CacheSceneObjectMetadata(bool overwriteEmptyMetadata)
    {
        ResizeMetadataArrays();

        CacheMetadataForTargets(
            objectsToEnable,
            enableTargetSceneNames,
            enableTargetHierarchyPaths,
            overwriteEmptyMetadata
        );

        CacheMetadataForTargets(
            objectsToDisable,
            disableTargetSceneNames,
            disableTargetHierarchyPaths,
            overwriteEmptyMetadata
        );
    }

    private void ResizeMetadataArrays()
    {
        int enableCount = objectsToEnable != null ? objectsToEnable.Length : 0;
        int disableCount = objectsToDisable != null ? objectsToDisable.Length : 0;

        enableTargetSceneNames = ResizeArray(enableTargetSceneNames, enableCount);
        enableTargetHierarchyPaths = ResizeArray(enableTargetHierarchyPaths, enableCount);
        disableTargetSceneNames = ResizeArray(disableTargetSceneNames, disableCount);
        disableTargetHierarchyPaths = ResizeArray(disableTargetHierarchyPaths, disableCount);
    }

    private static string[] ResizeArray(string[] source, int size)
    {
        if (source != null && source.Length == size)
        {
            return source;
        }

        string[] result = new string[size];

        if (source != null)
        {
            int copyCount = Mathf.Min(source.Length, size);

            for (int i = 0; i < copyCount; i++)
            {
                result[i] = source[i];
            }
        }

        return result;
    }

    private static void CacheMetadataForTargets(
        GameObject[] targets,
        string[] sceneNames,
        string[] hierarchyPaths,
        bool overwriteEmptyMetadata)
    {
        if (targets == null || sceneNames == null || hierarchyPaths == null)
        {
            return;
        }

        int count = Mathf.Min(
            targets.Length,
            Mathf.Min(sceneNames.Length, hierarchyPaths.Length)
        );

        for (int i = 0; i < count; i++)
        {
            GameObject target = targets[i];

            if (target != null)
            {
                Scene scene = target.scene;

                if (scene.IsValid())
                {
                    sceneNames[i] = scene.name;
                    hierarchyPaths[i] = GetHierarchyPath(target.transform);
                }
            }
            else if (overwriteEmptyMetadata)
            {
                sceneNames[i] = string.Empty;
                hierarchyPaths[i] = string.Empty;
            }
        }
    }

    private void RebindTargetsFromLoadedScenes()
    {
        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);

            if (scene.IsValid() && scene.isLoaded)
            {
                RebindTargetsForScene(scene);
            }
        }
    }

    private void RebindTargetsForScene(Scene scene)
    {
        if (!scene.IsValid() || !scene.isLoaded) return;

        ResizeMetadataArrays();

        RebindTargetArray(
            scene,
            objectsToEnable,
            enableTargetSceneNames,
            enableTargetHierarchyPaths,
            "解放時に表示"
        );

        RebindTargetArray(
            scene,
            objectsToDisable,
            disableTargetSceneNames,
            disableTargetHierarchyPaths,
            "解放時に非表示"
        );
    }

    private void RebindTargetArray(
        Scene scene,
        GameObject[] targets,
        string[] sceneNames,
        string[] hierarchyPaths,
        string label)
    {
        if (targets == null || sceneNames == null || hierarchyPaths == null)
        {
            return;
        }

        int count = Mathf.Min(
            targets.Length,
            Mathf.Min(sceneNames.Length, hierarchyPaths.Length)
        );

        for (int i = 0; i < count; i++)
        {
            // まだ有効な参照が残っている場合は再取得不要です。
            if (targets[i] != null) continue;

            string expectedScene = sceneNames[i];
            string path = hierarchyPaths[i];

            if (string.IsNullOrWhiteSpace(expectedScene) ||
                string.IsNullOrWhiteSpace(path) ||
                !string.Equals(scene.name, expectedScene, System.StringComparison.Ordinal))
            {
                continue;
            }

            GameObject found = FindByHierarchyPath(scene, path);

            if (found == null) continue;

            targets[i] = found;

            if (showDebugLogs)
            {
                Debug.Log(
                    $"[TownRescueUnlockObject2D] {name}: {label}[{i}]を再接続 " +
                    $"Scene={scene.name} / Path={path}",
                    this
                );
            }
        }
    }

    private static GameObject FindByHierarchyPath(Scene scene, string hierarchyPath)
    {
        if (!scene.IsValid() ||
            !scene.isLoaded ||
            string.IsNullOrWhiteSpace(hierarchyPath))
        {
            return null;
        }

        string[] parts = hierarchyPath.Split('/');
        if (parts.Length == 0) return null;

        GameObject[] roots = scene.GetRootGameObjects();

        for (int i = 0; i < roots.Length; i++)
        {
            GameObject root = roots[i];

            if (root == null || root.name != parts[0])
            {
                continue;
            }

            if (parts.Length == 1)
            {
                return root;
            }

            string childPath = string.Join("/", parts, 1, parts.Length - 1);
            Transform child = root.transform.Find(childPath);

            if (child != null)
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private static string GetHierarchyPath(Transform target)
    {
        if (target == null) return string.Empty;

        string path = target.name;
        Transform current = target.parent;

        while (current != null)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private void OnValidate()
    {
        if (requiredStoryFlags == null)
        {
            requiredStoryFlags = new List<string>();
        }

        for (int i = 0; i < requiredStoryFlags.Count; i++)
        {
            requiredStoryFlags[i] =
                requiredStoryFlags[i]?.Trim() ?? string.Empty;
        }

        sessionWaitFrames = Mathf.Max(1, sessionWaitFrames);

        // InspectorでObjectを割り当てた時点でScene名とHierarchy Pathを保存します。
        // nullになった要素の既存情報は消さず、シーン再接続用に保持します。
        CacheSceneObjectMetadata(false);
    }
}
