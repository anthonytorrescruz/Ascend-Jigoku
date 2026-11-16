using UnityEngine;

[RequireComponent(typeof(EnemyHealth),typeof(CombatAttack))]
public sealed class GrimDeathSpirit : MonoBehaviour
{
    private GrimDeathBoss boss;
    private EnemyHealth health;
    private CombatAttack attack;
    private SpriteRenderer art;
    private Vector2 dashVelocity;
    private float age,cycle,dyingAt=-1;
    private bool lunging;
    public void Initialize(GrimDeathBoss owner){
        boss=owner;health=GetComponent<EnemyHealth>();attack=GetComponent<CombatAttack>();art=GetComponent<SpriteRenderer>();
        health.InitializeEncounterHealth(1,boss.Settings.spiritDeath.Length/boss.Settings.framesPerSecond+.05f);
        var shape=GetComponent<BoxCollider2D>();
        if(shape!=null){shape.size=Vector2.Max(boss.Settings.spiritHurtboxSize,Vector2.one*.1f);shape.offset=boss.Settings.spiritHurtboxOffset;}
        health.HealthChanged+=Changed;
    }
    private void Changed(int hp,int max){if(hp<=0 && dyingAt<0){dyingAt=Time.time;attack.CancelAll();boss?.Play(boss.Settings.spiritDeathSound,.6f);}}
    private void Update(){
        if(boss==null){Destroy(gameObject);return;}
        if(!boss.IsAlive || boss.Player==null || boss.Player.CurrentHealth<=0){if(dyingAt<0)Vanish();if(dyingAt>=0)Frame(boss.Settings.spiritDeath,Time.time-dyingAt,false);return;}
        float dt=Time.deltaTime;if(dt<=0)return;age+=dt;cycle+=dt;
        var s=boss.Settings;
        if(dyingAt>=0){Frame(s.spiritDeath,Time.time-dyingAt,false);return;}
        if(age<s.spiritAppear.Length/s.framesPerSecond){Frame(s.spiritAppear,age,false);return;}
        Frame(s.spiritIdle,age,true);
        if(age>s.spiritLifetime){Vanish();return;}
        float warning=Mathf.Max(.2f,s.spiritWarning);
        if(!lunging && cycle>=warning+.6f){
            var shape=boss.Player.GetComponent<Collider2D>();Vector2 target=shape!=null?shape.bounds.center:boss.Player.transform.position;
            dashVelocity=(target-(Vector2)transform.position).normalized*s.spiritSpeed;
            lunging=true;cycle=0;
            attack.BeginAttack(dashVelocity.x<0?CombatDirection.Left:CombatDirection.Right,new Vector2(0,.65f),new Vector2(.7f,1.25f),1,40,~0,Time.time);
        }
        if(lunging){
            if(attack.CurrentSwing==null || attack.CurrentSwing.Cancelled || cycle>.6f){attack.CancelAll();lunging=false;cycle=0;}
            else transform.position+=(Vector3)(dashVelocity*dt);
        }else{
            // Long tell and a locked lunge vector make summoned spirits threatening but dodgeable.
            float brightness=cycle>warning?1:.55f;art.color=new Color(1,brightness,brightness,1);
            Vector3 p=transform.position;float floor=boss.Arena!=null?boss.Arena.bounds.max.y:boss.transform.position.y;
            p.y=Mathf.MoveTowards(p.y,floor+s.spiritHoverHeight,dt*3);transform.position=p;
        }
        if(attack.CurrentSwing!=null && attack.CurrentSwing.Parried){Vanish();return;}
        if(boss.Arena!=null){var p=transform.position;p.x=Mathf.Clamp(p.x,boss.Arena.bounds.min.x+.6f,boss.Arena.bounds.max.x-.6f);p.y=Mathf.Clamp(p.y,boss.Arena.bounds.max.y+.3f,boss.Arena.bounds.max.y+8);transform.position=p;}
    }
    private void Frame(Sprite[] frames,float t,bool loop){if(frames==null || frames.Length==0)return;int i=Mathf.FloorToInt(t*boss.Settings.framesPerSecond);art.sprite=frames[loop?i%frames.Length:Mathf.Min(i,frames.Length-1)];}
    public void Vanish(){if(health!=null && health.CurrentHealth>0)health.TakeDamage(999);else if(health==null)Destroy(gameObject);}
    private void OnDestroy(){if(health!=null)health.HealthChanged-=Changed;}
}
