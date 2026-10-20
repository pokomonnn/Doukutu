using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-200)]
public sealed class StoryConversationGuard : MonoBehaviour
{
    readonly List<Behaviour> suspended = new List<Behaviour>();
    DialogueActor2D locked;
    Transform player;
    CharacterHealth health;
    void Update()
    {
        var presenter=DialoguePresenter2D.Active;
        var actor=presenter!=null?presenter.CurrentActor:null;
        bool story=actor!=null && actor.GetComponent<StoryInteractionPoint>()!=null;
        if(story && locked==null) {
            var move=FindFirstObjectByType<PlayerMove>();
            if(move==null) return;
            player=move.transform;locked=actor;
            health=move.GetComponent<CharacterHealth>();if(health!=null)health.Damaged+=OnDamaged;
            Suspend(move);
            foreach(var gun in move.GetComponentsInChildren<GunShooter>(true)) Suspend(gun);
            Suspend(move.GetComponent<PlayerCarryController2D>());
            var body=move.GetComponent<Rigidbody2D>();if(body!=null)body.linearVelocity=new Vector2(0,body.linearVelocity.y);
        }
        if(locked!=null) {
            var hp=player!=null?player.GetComponent<CharacterHealth>():null;
            bool unsafeNow=player==null || hp!=null&&hp.IsDead || Vector2.Distance(player.position,locked.transform.position)>4;
            if(unsafeNow && presenter!=null) presenter.EndDialogue();
            if(!story || unsafeNow) Restore();
        }
    }
    void Suspend(Behaviour b){if(b!=null && b.enabled){suspended.Add(b);b.enabled=false;}}
    void OnDamaged(int damage,int remaining,Vector3 position) {
        if(DialoguePresenter2D.Active!=null)DialoguePresenter2D.Active.EndDialogue();
        Restore();
    }
    void Restore(){if(health!=null)health.Damaged-=OnDamaged;health=null;foreach(var b in suspended)if(b!=null)b.enabled=true;suspended.Clear();locked=null;}
    void OnDisable()=>Restore();
}
