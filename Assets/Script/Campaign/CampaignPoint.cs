using System.Linq;
using UnityEngine;

public class CampaignPoint : MonoBehaviour
{
    public string stepId;
    CampaignStep step;
    CampaignDirector Director => CampaignDirector.Current;
    bool Near => Director.actor!=null && Vector2.Distance(Director.actor.transform.position,transform.position)<2.3f;
    string Offer => stepId=="deliverMedicine"?"S1S02/BALDO_REQUEST":
        stepId=="reportPermit"?(Director.Has("PlayerBanditTruce")?"S2S02/BALDO_S2_REQUEST":"S2S02/BALDO_S2_LOCKED"):
        stepId=="reportTrade"?"S3S02/BALDO_S3_FIRST_EARLY":null;
    void Start() {step=Director.Data.steps.First(s=>s.id==stepId);}
    void Update()
    {
        if(step==null || Director.BlocksControls) return;
        if(stepId=="finn" && Director.Has("step_finn") && !Director.Has("S1S01_Rescued") && Near)
            Director.actor.escort=transform;
        if(stepId=="rescue" && (Director.actor.escort==null || Vector2.Distance(Director.actor.escort.position,transform.position)>3)) return;
        if(Near && Director.Ready(step) && Input.GetKeyDown(KeyCode.E)) Director.Interact(step);
        else if(Near && !Director.Ready(step) && Offer!=null && Input.GetKeyDown(KeyCode.E)) Director.BrowseOffer(Offer);
        if(stepId=="finn" && Director.Has("S1S01_Rescued")) gameObject.SetActive(false);
    }
    void OnGUI()
    {
        if(step==null || Director.Talking || !Near || (!Director.Ready(step) && Offer==null)) return;
        Vector3 screen=Camera.main.WorldToScreenPoint(transform.position+Vector3.up*1.5f);
        var style=new GUIStyle(GUI.skin.box) {font=Director.japaneseFont,fontSize=16};
        GUI.Box(new Rect(screen.x-170,Screen.height-screen.y-40,340,60),step.title+"\n"+(Director.Ready(step)?"E：調べる / 話す":"E：依頼について聞く"),style);
    }
}
