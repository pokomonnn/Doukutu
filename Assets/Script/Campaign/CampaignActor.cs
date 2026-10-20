using UnityEngine;

// A small isolated stage-playtest controller. Existing Player/weapon prefabs are untouched.
[RequireComponent(typeof(Rigidbody2D), typeof(BoxCollider2D))]
public class CampaignActor : MonoBehaviour
{
    public Camera followCamera;
    public int health = 5;
    Rigidbody2D body;
    float nextShot, nextDamage;
    public Transform escort;
    void Awake() { body=GetComponent<Rigidbody2D>(); }
    void Update()
    {
        if (CampaignDirector.Current.BlocksControls) {body.linearVelocity=new Vector2(0,body.linearVelocity.y); return;}
        float horizontal=Input.GetAxisRaw("Horizontal");
        body.linearVelocity=new Vector2(horizontal*6,body.linearVelocity.y);
        if(horizontal!=0) GetComponent<SpriteRenderer>().flipX=horizontal<0;
        if(Input.GetKeyDown(KeyCode.Space) && Physics2D.BoxCast((Vector2)transform.position+Vector2.down*.88f,new Vector2(.55f,.06f),0,Vector2.down,.13f,1<<6))
            body.linearVelocity=new Vector2(body.linearVelocity.x,11);
        if(Input.GetMouseButton(0) && Time.time>nextShot && Input.mousePosition.y < Screen.height*.87f) {
            nextShot=Time.time+.2f;
            Vector2 direction=followCamera.ScreenToWorldPoint(Input.mousePosition)-transform.position;
            var hit=Physics2D.Raycast((Vector2)transform.position+direction.normalized*.6f,direction.normalized,18,(1<<11)|(1<<6));
            if(hit.collider!=null) hit.collider.GetComponent<CampaignEnemy>()?.Hit();
            var tracer=new GameObject("Shot").AddComponent<LineRenderer>();
            tracer.material=new Material(Shader.Find("Sprites/Default"));
            tracer.startWidth=.05f; tracer.endWidth=.02f; tracer.positionCount=2;
            tracer.startColor=tracer.endColor=Color.yellow;
            tracer.SetPosition(0,transform.position); tracer.SetPosition(1,hit.collider!=null?(Vector3)hit.point:transform.position+(Vector3)direction.normalized*18);
            Destroy(tracer.material,.07f); Destroy(tracer.gameObject,.07f);
        }
        if(transform.position.y < -8) Damage(health);
        if(escort!=null && Vector2.Distance(escort.position,transform.position)>1.5f)
            escort.position=Vector3.MoveTowards(escort.position,transform.position+Vector3.left,4*Time.deltaTime);
    }
    void LateUpdate()
    {
        if(followCamera!=null) followCamera.transform.position=new Vector3(Mathf.Clamp(transform.position.x,10,70),4,-10);
    }
    public void Damage(int amount)
    {
        if(Time.time<nextDamage) return;
        nextDamage=Time.time+1; health-=amount;
        if(health>0) return;
        health=5; transform.position=new Vector3(3,2,0); body.linearVelocity=Vector2.zero;
        if(escort!=null) {escort.position=new Vector3(16,1,0);escort=null;}
    }
    void OnGUI()
    {
        if(!CampaignDirector.Current.Talking) GUI.Label(new Rect(20,Screen.height-65,300,35),"体力 "+new string('●',health));
    }
}
