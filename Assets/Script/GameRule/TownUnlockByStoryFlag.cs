using UnityEngine;

/// <summary>
/// Story Flagに応じて町のGameObjectを表示／非表示します。
/// RescueProgressionControllerが作る
/// rescue_total_3 / rescue_total_6 / ... や
/// 特殊NPCの解放Flagを、そのまま町の機能解放に使えます。
///
/// このComponentは「自分自身をOFFにするObject」ではなく、
/// 常時ActiveなManager/Canvas配下に置くのがおすすめです。
/// </summary>
[DisallowMultipleComponent]
public class TownUnlockByStoryFlag : MonoBehaviour
{
    [Header("解放条件")]
    [Tooltip("ONになったら解放するStory Flag IDです。")]
    [InspectorName("必要Story Flag ID")]
    [SerializeField] private string requiredStoryFlagId =
        string.Empty;

    [Header("解放時")]
    [Tooltip("FlagがONならActiveにするObjectです。")]
    [InspectorName("解放時に表示するObject")]
    [SerializeField] private GameObject[] objectsToEnable;

    [Tooltip("FlagがONなら非ActiveにするObjectです。")]
    [InspectorName("解放時に非表示にするObject")]
    [SerializeField] private GameObject[] objectsToDisable;

    [Header("デバッグ")]
    [InspectorName("解放ログを表示")]
    [SerializeField] private bool showDebugLogs;

    private GameSessionManager subscribedSession;

    private void OnEnable()
    {
        Subscribe();
        RefreshState();
    }

    private void Start()
    {
        Subscribe();
        RefreshState();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    [ContextMenu("解放状態を更新")]
    public void RefreshState()
    {
        GameSessionManager session =
            FindSessionManager();

        bool unlocked =
            session != null &&
            !string.IsNullOrWhiteSpace(
                requiredStoryFlagId) &&
            session.HasStoryFlag(
                requiredStoryFlagId
            );

        SetObjectsActive(
            objectsToEnable,
            unlocked
        );

        SetObjectsActive(
            objectsToDisable,
            !unlocked
        );

        if (showDebugLogs)
        {
            Debug.Log(
                $"[TownUnlockByStoryFlag] {name}: " +
                $"Flag={requiredStoryFlagId} / " +
                $"Unlocked={unlocked}",
                this
            );
        }
    }

    private void Subscribe()
    {
        GameSessionManager session =
            FindSessionManager();

        if (subscribedSession == session)
        {
            return;
        }

        Unsubscribe();

        subscribedSession =
            session;

        if (subscribedSession != null)
        {
            subscribedSession
                .StorySessionChanged +=
                HandleStoryChanged;
        }
    }

    private void Unsubscribe()
    {
        if (subscribedSession != null)
        {
            subscribedSession
                .StorySessionChanged -=
                HandleStoryChanged;
        }

        subscribedSession = null;
    }

    private void HandleStoryChanged()
    {
        RefreshState();
    }

    private static void SetObjectsActive(
        GameObject[] targets,
        bool active)
    {
        if (targets == null)
        {
            return;
        }

        foreach (GameObject target in
                 targets)
        {
            if (target != null &&
                target.activeSelf != active)
            {
                target.SetActive(active);
            }
        }
    }

    private static GameSessionManager FindSessionManager()
    {
        GameSessionManager session =
            GameSessionManager.Instance;

        if (session == null)
        {
            session =
                FindAnyObjectByType<
                    GameSessionManager
                >(
                    FindObjectsInactive.Include
                );
        }

        return session;
    }

    private void OnValidate()
    {
        requiredStoryFlagId =
            requiredStoryFlagId?.Trim() ??
            string.Empty;
    }
}
