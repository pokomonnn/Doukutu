using TMPro;
using UnityEngine;

// Uses the existing medical-kit completion callback and carrying component.
public sealed class StoryFinnDelivery : MonoBehaviour
{
    public Transform baldo;
    public TMP_FontAsset dialogueFont;
    public DialogueData2D treatedDialogue;
    public float deliveryDistance = 2.5f;
    public const string TreatedFlag = "S1S01_Treated";
    public const string DeliveredFlag = "S1S01_Delivered";
    public bool IsTreated => StoryCampaignRuntime.Has(TreatedFlag);
    bool hasBeenCarried;

    void Start()
    {
        if (StoryCampaignRuntime.Has(DeliveredFlag) || StoryCampaignRuntime.Has("S1S01_Completed")) {
            gameObject.SetActive(false);return;
        }
        if(IsTreated) ApplyCure();
        UpdateDestination();
    }
    void ApplyCure()
    {
        GetComponent<RescuePersonSymptoms2D>().CureSymptoms(StatusConditionType.Poison);
        var health=GetComponent<CharacterHealth>();health.Heal(health.MaxHealth);
        GetComponent<RescuePersonInfo2D>()?.SetCondition(RescuePersonCondition2D.Healthy);
        if(treatedDialogue!=null)GetComponent<DialogueActor2D>()?.SetDialogue(treatedDialogue);
        UpdateDestination();
    }
    void UpdateDestination()
    {
        var manager=StoryCampaignRuntime.FindMissionManager();
        if(manager==null)return;
        for(int i=0;i<manager.MissionCount;i++)
            if(manager.GetMissionDefinition(i)?.MissionId=="story_S1S01")
                manager.GetMissionEntry(i).SetCompassTarget(IsTreated?baldo:transform);
    }
    public void OnTreatmentSucceeded(StatusConditionType treated)
    {
        if ((treated & StatusConditionType.Poison)==0 || IsTreated) return;
        StoryCampaignRuntime.Set(TreatedFlag);ApplyCure();
        DialogueBattleBarkBubble2D.Show(transform,"ありがとう、助かったよ",dialogueFont,
            Vector3.up*2,4,100,5,Color.white,new Color(.08f,.09f,.12f,.95f),speechPriority:100);
    }
    void Update() { TryDeliver(); }
    public bool TryDeliver()
    {
        var cargo=GetComponent<CarryableObject2D>();
        if(cargo.IsCarried)hasBeenCarried=true;
        if(!hasBeenCarried || !IsTreated || baldo==null || StoryCampaignRuntime.Has(DeliveredFlag) ||
           StoryCampaignRuntime.Has("S1S01_Completed") ||
           Vector2.Distance(transform.position,baldo.position)>deliveryDistance) return false;
        var player=FindFirstObjectByType<PlayerCarryController2D>();
        if(player==null || Vector2.Distance(player.transform.position,baldo.position)>deliveryDistance+1) return false;
        // Beginning the mission also supports finding/treating Finn before accepting it.
        if(!StoryCampaignRuntime.TryExecute("accept/S1S01",out _)) return false;
        StoryCampaignRuntime.Set(DeliveredFlag);StoryCampaignRuntime.Sync();
        if(cargo.CurrentCarrier!=null)cargo.CurrentCarrier.NotifyCarryTargetUnavailable(cargo);
        DialogueBattleBarkBubble2D.Show(baldo,"フィンを連れてきてくれたか。礼を受け取ってくれ。",dialogueFont,
            Vector3.up*2,4,100,7,Color.white,new Color(.08f,.09f,.12f,.95f),speechPriority:100);
        gameObject.SetActive(false);return true;
    }
}

