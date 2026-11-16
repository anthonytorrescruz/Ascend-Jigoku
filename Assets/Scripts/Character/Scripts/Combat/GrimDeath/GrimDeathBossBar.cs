using UnityEngine;
using UnityEngine.UI;
using AscendJigoku.UI;

// Encounter-local, runtime-only UI. Black is missing health, red is live health;
// the pale chip briefly records the last hit, then drains away.
public sealed class GrimDeathBossBar : MonoBehaviour
{
    private GrimDeathBoss boss;
    private CanvasGroup group;
    private RectTransform fill,chip;
    private Text nameText,tell;
    private float previous=1,damageChip=1,chipWait;
    private bool ended;
    public float DisplayedFraction {get;private set;}
    public void Initialize(GrimDeathBoss owner){
        boss=owner;var canvas=MenuStyle.CreateCanvas("Grim Death Boss HUD",6,transform);
        Destroy(canvas.GetComponent<GraphicRaycaster>());
        group=canvas.gameObject.AddComponent<CanvasGroup>();group.blocksRaycasts=false;group.interactable=false;group.alpha=0;
        var root=new GameObject("Boss bar",typeof(RectTransform)).GetComponent<RectTransform>();root.SetParent(canvas,false);
        root.anchorMin=new Vector2(.16f,0);root.anchorMax=new Vector2(.84f,0);root.pivot=new Vector2(.5f,0);root.anchoredPosition=new Vector2(0,156);root.sizeDelta=new Vector2(0,80);
        nameText=MenuStyle.Label(root,"Boss name",boss.Settings.bossName,new Vector2(0,25),new Vector2(750,32),25,TextAnchor.MiddleLeft,new Color(.9f,.86f,.79f));
        nameText.font=MenuStyle.TitleFont;
        var nr=nameText.rectTransform;nr.anchorMin=new Vector2(0,.5f);nr.anchorMax=new Vector2(1,.5f);nr.pivot=new Vector2(.5f,.5f);nr.sizeDelta=new Vector2(0,32);nr.anchoredPosition=new Vector2(0,25);nameText.resizeTextForBestFit=true;nameText.resizeTextMinSize=14;nameText.resizeTextMaxSize=25;
        var frame=Box(root,"Weathered gold frame",new Color(.4f,.33f,.24f));frame.anchorMin=new Vector2(0,.35f);frame.anchorMax=new Vector2(1,.35f);frame.sizeDelta=new Vector2(0,16);
        var black=Box(frame,"Missing health (black)",new Color(.012f,.008f,.016f));Inset(black,3,3);
        chip=Box(black,"Recent damage",new Color(.52f,.34f,.34f));StretchFill(chip);
        fill=Box(black,"Live health",new Color(.56f,.035f,.065f));StretchFill(fill);
        var shine=Box(fill,"Highlight",new Color(1,.3f,.3f,.2f));shine.anchorMin=new Vector2(0,.6f);shine.anchorMax=Vector2.one;shine.sizeDelta=Vector2.zero;
        tell=MenuStyle.Label(root,"Attack warning","",new Vector2(0,-38),new Vector2(900,26),17,TextAnchor.MiddleCenter,new Color(.78f,.67f,.9f));
        tell.rectTransform.anchorMin=new Vector2(0,.5f);tell.rectTransform.anchorMax=new Vector2(1,.5f);tell.rectTransform.sizeDelta=new Vector2(0,26);tell.resizeTextForBestFit=true;tell.resizeTextMinSize=11;tell.resizeTextMaxSize=17;
    }
    public void End()=>ended=true;
    private void Update(){
        if(boss==null || group==null)return;
        bool visible=!ended && boss.Player!=null && boss.Player.CurrentHealth>0 && Time.timeScale>0;
        group.alpha=Time.timeScale<=0?0:Mathf.MoveTowards(group.alpha,visible?1:0,Time.unscaledDeltaTime*4);
        float fraction=Mathf.Clamp01((float)boss.Health.CurrentHealth/boss.Health.MaxHealth);DisplayedFraction=fraction;
        if(fraction<previous){damageChip=Mathf.Max(previous,damageChip);chipWait=Time.unscaledTime+.22f;}
        if(Time.unscaledTime>=chipWait)damageChip=Mathf.MoveTowards(damageChip,fraction,Time.unscaledDeltaTime*.75f);
        damageChip=Mathf.Max(fraction,damageChip);previous=fraction;
        fill.anchorMax=new Vector2(fraction,1);chip.anchorMax=new Vector2(damageChip,1);tell.text=boss.Telegraph;
    }
    private static RectTransform Box(Transform parent,string name,Color color){var go=new GameObject(name,typeof(RectTransform),typeof(Image));go.transform.SetParent(parent,false);var image=go.GetComponent<Image>();image.color=color;image.raycastTarget=false;return (RectTransform)go.transform;}
    private static void Inset(RectTransform r,float x,float y){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(x,y);r.offsetMax=new Vector2(-x,-y);}
    private static void StretchFill(RectTransform r){r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.pivot=new Vector2(0,.5f);r.sizeDelta=Vector2.zero;r.anchoredPosition=Vector2.zero;}
}
