using System;
using System.Collections.Generic;
using UnityEngine;

// Full-width final arena. The ceiling locks the next checkpoint before entry; side
// seals close only after the player lands. Neither streaming nor leaving view resets boss HP.
public sealed class GrimDeathEncounter : MonoBehaviour
{
    private GrimDeathSettings settings;
    private Collider2D floor;
    private PlayerController player;
    private Action cleared;
    private GameObject ceiling,left,right;
    private GrimDeathBossBar bar;
    private AudioSource music;
    private float musicFade;
    private bool stopping;
    private readonly Dictionary<AudioSource,float> backgrounds=new Dictionary<AudioSource,float>();
    public GrimDeathBoss Boss {get;private set;}
    public bool Started {get;private set;}
    public bool Defeated {get;private set;}
    public float CombatWidth=>floor!=null?floor.bounds.size.x:0;
    public bool MusicPlaying=>music!=null && music.isPlaying;
    public void Initialize(GrimDeathSettings config,Collider2D platform,PlayerController actor,Action onCleared){
        settings=config;floor=platform;player=actor;cleared=onCleared;
        // The generator just scaled/moved this platform; cached physics bounds can still be one unit wide.
        Physics2D.SyncTransforms();
        float width=floor.bounds.size.x;
        ceiling=Barrier("Grim Death checkpoint seal",new Vector2(0,config.ceilingHeight),new Vector2(width+2,.35f));
        left=Barrier("Grim Death left seal",new Vector2(-width*.5f,config.ceilingHeight*.5f),new Vector2(.35f,config.ceilingHeight));
        right=Barrier("Grim Death right seal",new Vector2(width*.5f,config.ceilingHeight*.5f),new Vector2(.35f,config.ceilingHeight));left.SetActive(false);right.SetActive(false);
        float spawnX=Mathf.Clamp(player.transform.position.x<transform.position.x?8:-8,-width*.5f+3,width*.5f-3);
        var go=Instantiate(config.bossPrefab,new Vector3(transform.position.x+spawnX,floor.bounds.max.y+.06f,0),Quaternion.identity,transform);
        Boss=go.GetComponent<GrimDeathBoss>();Boss.Initialize(floor,player);Boss.Died+=OnDefeated;
        music=gameObject.AddComponent<AudioSource>();music.playOnAwake=false;music.loop=true;music.spatialBlend=0;music.clip=config.music;music.volume=0;
    }
    private GameObject Barrier(string name,Vector2 position,Vector2 size){
        var go=new GameObject(name);go.transform.SetParent(transform,false);go.transform.localPosition=position;go.layer=3;go.AddComponent<BoxCollider2D>().size=size;
        var art=go.AddComponent<SpriteRenderer>();art.sprite=GrimDeathHazard.Square;art.transform.localScale=Vector3.one;art.drawMode=SpriteDrawMode.Sliced;art.size=size;art.color=new Color(.35f,.13f,.65f,.45f);art.sortingOrder=6;
        return go;
    }
    private void Update(){
        if(player==null || floor==null)return;
        var hp=player.GetComponent<PlayerHealth>();
        if(hp.CurrentHealth<=0){if(Boss!=null){Boss.CancelMove();Boss.ClearSpells();}StopMusic();if(bar!=null)bar.End();return;}
        if(!Started && !Defeated && Time.timeScale>0){
            var bounds=player.GetComponent<Collider2D>().bounds;
            if(player.IsGrounded && bounds.center.x>floor.bounds.min.x+.6f && bounds.center.x<floor.bounds.max.x-.6f
                && bounds.min.y>=floor.bounds.max.y-.08f && bounds.min.y<floor.bounds.max.y+.3f)BeginFight();
        }
        if(music!=null && music.isPlaying){
            musicFade=Mathf.MoveTowards(musicFade,stopping?0:1,Time.unscaledDeltaTime/1.1f);
            music.volume=settings.musicVolume*musicFade*AscendJigoku.Settings.GameSettings.MusicVolume;
            if(stopping && musicFade<=0)StopMusic();
        }
    }
    public void BeginFight(){
        if(Started || Defeated || Boss==null)return;Started=true;left.SetActive(true);right.SetActive(true);Boss.Activate();
        bar=gameObject.AddComponent<GrimDeathBossBar>();bar.Initialize(Boss);
        foreach(var source in FindObjectsByType<AudioSource>())if(source!=music && source.isPlaying && source.loop && source.spatialBlend<.01f && source.clip!=null){backgrounds[source]=source.volume;source.volume*=.08f;}
        if(music.clip!=null)music.Play();
    }
    private void OnDefeated(){
        if(Defeated)return;Defeated=true;ceiling.SetActive(false);left.SetActive(false);right.SetActive(false);stopping=true;
        if(bar!=null)bar.End();cleared?.Invoke();
        AscendJigoku.UI.GameplayHud.Announce("GRIM DEATH VANQUISHED — the next checkpoint is open.");
    }
    private void StopMusic(){if(music!=null)music.Stop();foreach(var pair in backgrounds)if(pair.Key!=null)pair.Key.volume=pair.Value;backgrounds.Clear();}
    private void OnDisable(){StopMusic();if(Boss!=null){Boss.CancelMove();Boss.ClearSpells();}if(bar!=null)bar.End();}
    private void OnDestroy(){if(Boss!=null)Boss.Died-=OnDefeated;StopMusic();}
}
