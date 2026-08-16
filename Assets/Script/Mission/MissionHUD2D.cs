using TMPro;
using UnityEngine;

/// <summary>
/// 任意のミッション表示UIです。
/// Canvas内のTextMeshProUGUIへ、現在のミッション名・説明・進捗を表示します。
/// 使わなくてもミッションとコンパスは動作します。
/// </summary>
[DisallowMultipleComponent]
public class MissionHUD2D : MonoBehaviour
{
    [Header("参照")]
    [SerializeField] private MissionManager2D missionManager;

    [Tooltip("ミッション表示全体。空欄ならこのObjectを使います")]
    [SerializeField] private GameObject hudRoot;

    [SerializeField] private TMP_Text missionTitleText;
    [SerializeField] private TMP_Text missionDescriptionText;
    [SerializeField] private TMP_Text missionProgressText;

    [Header("表示文")]
    [SerializeField] private string collectFormat = "{0} {1} / {2}";
    [SerializeField] private string deliverFormat = "納品 {0}  {1} / {2}";
    [SerializeField] private string defeatFormat = "討伐 {0} / {1}";

    private bool isSubscribed;

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

    private void OnDisable()
    {
        UnsubscribeEvents();
    }

    public void RefreshUI()
    {
        if (missionManager == null ||
            !missionManager.HasActiveMission)
        {
            SetRootVisible(false);
            return;
        }

        MissionDefinition2D mission =
            missionManager.ActiveMission;

        SetRootVisible(true);

        if (missionTitleText != null)
        {
            missionTitleText.text = mission.DisplayName;
        }

        if (missionDescriptionText != null)
        {
            missionDescriptionText.text = mission.Description;
        }

        if (missionProgressText != null)
        {
            missionProgressText.text = BuildProgressText(
                mission,
                missionManager.ActiveProgress,
                missionManager.ActiveRequiredAmount
            );
        }
    }

    private string BuildProgressText(
        MissionDefinition2D mission,
        int progress,
        int required)
    {
        if (mission.ObjectiveType ==
            MissionObjectiveType2D.CollectItem)
        {
            string itemName = mission.RequiredItem != null
                ? mission.RequiredItem.DisplayName
                : "Item";

            return string.Format(
                collectFormat,
                itemName,
                progress,
                required
            );
        }

        if (mission.ObjectiveType ==
            MissionObjectiveType2D.DeliverItem)
        {
            string itemName = mission.RequiredItem != null
                ? mission.RequiredItem.DisplayName
                : "Item";

            return string.Format(
                deliverFormat,
                itemName,
                progress,
                required
            );
        }

        return string.Format(
            defeatFormat,
            progress,
            required
        );
    }

    private void HandleMissionStateChanged()
    {
        RefreshUI();
    }

    private void SubscribeEvents()
    {
        if (isSubscribed || missionManager == null)
        {
            return;
        }

        missionManager.MissionStateChanged +=
            HandleMissionStateChanged;

        isSubscribed = true;
    }

    private void UnsubscribeEvents()
    {
        if (!isSubscribed || missionManager == null)
        {
            return;
        }

        missionManager.MissionStateChanged -=
            HandleMissionStateChanged;

        isSubscribed = false;
    }

    private void SetRootVisible(bool visible)
    {
        // このコンポーネント自身のGameObjectを非表示にすると、
        // 次のミッション開始時に再表示するUpdate/Eventを受け取れなくなります。
        // その場合はTextだけを隠して、MissionHUD2D自体は有効のままにします。
        GameObject root = hudRoot != null
            ? hudRoot
            : gameObject;

        if (root != gameObject)
        {
            if (root.activeSelf != visible)
            {
                root.SetActive(visible);
            }

            return;
        }

        if (missionTitleText != null)
        {
            missionTitleText.enabled = visible;
        }

        if (missionDescriptionText != null)
        {
            missionDescriptionText.enabled = visible;
        }

        if (missionProgressText != null)
        {
            missionProgressText.enabled = visible;
        }
    }

    private void FindReferences()
    {
        if (missionManager == null)
        {
            missionManager =
                FindAnyObjectByType<MissionManager2D>();
        }

        if (hudRoot == null)
        {
            hudRoot = gameObject;
        }
    }
}
