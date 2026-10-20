using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>Existing tracked-mission HUD, extended with data-driven objectives. Never changes gameplay progress.</summary>
[DisallowMultipleComponent]
public class MissionHUD2D : MonoBehaviour
{
    [SerializeField] private MissionManager2D missionManager;
    [SerializeField] private GameObject hudRoot;
    [SerializeField] private TMP_Text missionTitleText, missionDescriptionText, missionProgressText;
    [SerializeField] private string collectFormat = "{0} {1} / {2}";
    [SerializeField] private string deliverFormat = "納品 {0} {1} / {2}";
    [SerializeField] private string defeatFormat = "討伐 {0} / {1}";
    [Header("目標更新の演出（任意）")]
    [SerializeField] private CanvasGroup objectiveGroup, completionGroup;
    [SerializeField] private TMP_Text completionText;
    [SerializeField, Min(.1f)] private float fadeSeconds = .3f;
    [SerializeField, Min(.1f)] private float completionHoldSeconds = 2.5f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip objectiveCompletedSound;
    [SerializeField, Range(0,1)] private float soundVolume = .25f;
    private readonly Queue<string> notices = new Queue<string>();
    private MissionDefinition2D observed;
    private bool[] previous;
    private int revision = -1;
    private bool initialized;
    private float nextRefresh, noticeElapsed;
    private string activeNotice, lastBody;
    private int readyFrame;
    private MissionHUDSafeArea2D layout;
    public int CompletionNoticeCount { get; private set; }
    public string CurrentObjectiveText => missionDescriptionText != null ? missionDescriptionText.text : "";

    void OnEnable()
    {
        layout=GetComponent<MissionHUDSafeArea2D>();
        if(missionManager==null)missionManager=FindFirstObjectByType<MissionManager2D>();
        if(missionManager!=null)missionManager.MissionStateChanged+=RefreshUI;
        initialized=false;readyFrame=Time.frameCount+3;RefreshUI();
        ClearNotices();
    }
    void OnDisable()
    {
        if(missionManager!=null)missionManager.MissionStateChanged-=RefreshUI;
        initialized=false;ClearNotices();
    }
    public void RefreshUI() { nextRefresh=0; }
    void LateUpdate()
    {
        if(Time.frameCount<readyFrame)return;
        // Keep completion notices queued while a conversation or mission menu covers the HUD.
        if(layout!=null && layout.IsObscured)return;
        if(Time.unscaledTime>=nextRefresh){RefreshPresentation();nextRefresh=Time.unscaledTime+.1f;}
        if(objectiveGroup!=null)objectiveGroup.alpha=Mathf.MoveTowards(objectiveGroup.alpha,1,Time.unscaledDeltaTime/Mathf.Max(.1f,fadeSeconds));
        if(activeNotice==null && notices.Count>0) {
            activeNotice=notices.Dequeue();noticeElapsed=0;
            if(completionText!=null)completionText.text=activeNotice;
            if(audioSource!=null && objectiveCompletedSound!=null)audioSource.PlayOneShot(objectiveCompletedSound,soundVolume);
        }
        if(activeNotice!=null) {
            noticeElapsed+=Time.unscaledDeltaTime;
            float duration=completionHoldSeconds+fadeSeconds*2;
            if(completionGroup!=null)completionGroup.alpha=Mathf.Clamp01(Mathf.Min(noticeElapsed/fadeSeconds,(duration-noticeElapsed)/fadeSeconds));
            if(noticeElapsed>=duration){activeNotice=null;if(completionGroup!=null)completionGroup.alpha=0;RefreshUI();}
        }
    }
    void ClearNotices()
    {
        notices.Clear();activeNotice=null;
        if(completionGroup!=null)completionGroup.alpha=0;
        if(completionText!=null)completionText.text="";
    }
    void ObserveCompletion(MissionDefinition2D mission,bool[] now)
    {
        if(previous==null || previous.Length!=now.Length)return;
        string tick=completionText!=null && completionText.font!=null && completionText.font.HasCharacter('✓')?"✓ ":"";
        bool changed=false;
        for(int i=0;i<now.Length;i++)if(now[i] && !previous[i]) {
            notices.Enqueue(tick+"完了："+mission.DisplayObjectives[i]?.text);CompletionNoticeCount++;changed=true;
        }
        if(changed && now.Length>0 && now.All(v=>v)) {
            notices.Enqueue("クエスト完了："+mission.DisplayName);CompletionNoticeCount++;
        }
    }
    void RefreshPresentation()
    {
        if(missionManager==null)missionManager=FindFirstObjectByType<MissionManager2D>();
        var current=missionManager!=null?missionManager.TrackedMission:null;
        int currentRevision=GameSessionManager.Instance!=null?GameSessionManager.Instance.SaveRestoreRevision:0;
        bool reset=!initialized || revision!=currentRevision;
        if(reset)ClearNotices();
        if(!reset && observed!=null && observed.HasDisplayObjectives) {
            var now=MissionObjectiveDisplayState.Snapshot(observed);
            // Switching to another unfinished quest is a silent baseline; finishing the old one can notify.
            if(current==observed || now.All(v=>v))ObserveCompletion(observed,now);
        }
        if(!reset && current!=observed && observed!=null && MissionObjectiveDisplayState.HasPending(observed))ClearNotices();
        observed=current;previous=MissionObjectiveDisplayState.Snapshot(current);revision=currentRevision;initialized=true;
        bool visible=current!=null || notices.Count>0 || activeNotice!=null;
        if(hudRoot!=null && hudRoot!=gameObject)hudRoot.SetActive(visible);
        if(missionTitleText!=null){missionTitleText.enabled=visible;missionTitleText.text=current!=null?"【"+current.DisplayName+"】":"";}
        string body=current==null?"":current.HasDisplayObjectives?"□ "+MissionObjectiveDisplayState.Current(current):current.Description;
        if(missionDescriptionText!=null){missionDescriptionText.enabled=visible;missionDescriptionText.text=body;}
        if(body!=lastBody && objectiveGroup!=null)objectiveGroup.alpha=reset?1:0;
        lastBody=body;
        if(missionProgressText!=null) {
            missionProgressText.enabled=visible;
            missionProgressText.text=current==null?"":current.HasDisplayObjectives?"":BuildProgressText(current,missionManager.TrackedProgress,missionManager.TrackedRequiredAmount);
        }
    }
    string BuildProgressText(MissionDefinition2D mission,int progress,int required)
    {
        string item=mission.RequiredItem!=null?mission.RequiredItem.DisplayName:"アイテム";
        if(mission.ObjectiveType==MissionObjectiveType2D.CollectItem)return string.Format(collectFormat,item,progress,required);
        if(mission.ObjectiveType==MissionObjectiveType2D.DeliverItem)return string.Format(deliverFormat,item,progress,required);
        if(mission.ObjectiveType==MissionObjectiveType2D.DefeatTargetEnemy)return string.Format(defeatFormat,progress,required);
        return (mission.ObjectiveType==MissionObjectiveType2D.RescueTargetNpc?"救出 ":"進捗 ")+progress+" / "+required;
    }
}
