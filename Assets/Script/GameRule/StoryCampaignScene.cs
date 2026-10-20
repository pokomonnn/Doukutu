using UnityEngine;

public sealed class StoryCampaignScene : MonoBehaviour
{
    public int stage;
    public DialogueActor2D gardenActor;
    public DialogueData2D gardenIntroduction, gardenChoices, endingA, endingB, endingC;
    void Start(){StoryCampaignRuntime.Sync();}
    void Update()
    {
        if(gardenActor==null || (DialoguePresenter2D.Active!=null && DialoguePresenter2D.Active.IsPlaying)) return;
        var data=!StoryCampaignRuntime.Has("S4M01_StorySeen")?gardenIntroduction:
            !StoryCampaignRuntime.Has("EndingCommitted")?gardenChoices:
            StoryCampaignRuntime.Has("EndingType_A")?endingA:StoryCampaignRuntime.Has("EndingType_B")?endingB:endingC;
        gardenActor.SetDialogue(data);
    }
}
