using UnityEngine;

public class CampaignEnemy : MonoBehaviour
{
    int health=3;
    float origin;
    void Start() {origin=transform.position.x;}
    void Update()
    {
        if(CampaignDirector.Current.Talking) return;
        transform.position=new Vector3(origin+Mathf.Sin(Time.time)*2,transform.position.y,0);
        var actor=CampaignDirector.Current.actor;
        if(actor!=null && Vector2.Distance(actor.transform.position,transform.position)<1.3f) actor.Damage(1);
    }
    public void Hit() {if(--health<=0) Destroy(gameObject);}
}
