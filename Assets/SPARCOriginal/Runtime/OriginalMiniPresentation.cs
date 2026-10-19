using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Responsive activity skin and bounded cosmetic rewards; never changes scoring or timers.</summary>
    [DefaultExecutionOrder(2400)] public sealed class OriginalMiniPresentation:MonoBehaviour
    {
        public OriginalMiniGame game;OriginalBehaviour manager;Canvas canvas;RectTransform panel,area;
        TMP_Text title,tagline,flow,banner,helpLabel;Image timeTrack,timeFill;Button help;float bannerUntil;int streak,best;float streakUntil;bool wasTransitioning;
        readonly RectTransform[] particles=new RectTransform[20];readonly Vector2[] origins=new Vector2[20],velocities=new Vector2[20];readonly float[] births=new float[20];int particleCursor;
        public int Streak=>streak;public int BestStreak=>best;
        public bool InstructionsExpanded {get;private set;}
        static RectTransform Find(Transform root,string name)=>root.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name==name);
        public void Prepare(OriginalBehaviour source)
        {
            manager=source;canvas=GetComponent<Canvas>();panel=Find(transform,"InstructionPanel");area=Find(transform,"PlayArea");if(!panel||!area)return;bool white=source.sourceType=="WhiteBloodCellGameManager";
            var bg=Find(transform,"Background");if(bg){var image=bg.GetComponent<Image>();if(image){image.sprite=null;image.color=white?new Color(.035f,.075f,.10f):new Color(.12f,.045f,.075f);}}
            var arena=area.GetComponent<Image>();if(arena){arena.sprite=OriginalGameArt.Get(white?"defense-vessel-pixel-sketch":"oxygen-vessel-pixel-sketch");arena.overrideSprite=null;arena.color=Color.white;arena.material=null;arena.type=Image.Type.Simple;}
            foreach(var wall in transform.GetComponentsInChildren<RectTransform>(true).Where(t=>t.name=="OuterWall")){var image=wall.GetComponent<Image>();if(image){image.sprite=null;image.color=white?new Color(.15f,.42f,.45f):new Color(.65f,.25f,.32f);}}
            OriginalGameArt.Style(panel.GetComponent<Image>(),"paper");
            title=OriginalGameArt.Text("__SPARC_MINI_Title",panel,20,OriginalGameArt.Ink);title.fontStyle=FontStyles.Bold;title.text=white?"IMMUNE DEFENSE":"OXYGEN RUN";
            tagline=OriginalGameArt.Text("__SPARC_MINI_Tagline",panel,13,OriginalUiTheme.Muted);tagline.text=white?"Catch germs. Protect red cells.":"Catch oxygen. Carry it to the cells.";
            flow=OriginalGameArt.Text("__SPARC_MINI_Flow",panel,13,OriginalGameArt.Blue);
            if(!help)
            {
                var image=OriginalGameArt.Image("__SPARC_MINI_Help",panel,OriginalGameArt.Frame("secondary"),Color.white);image.type=Image.Type.Sliced;image.raycastTarget=true;help=image.gameObject.AddComponent<Button>();help.targetGraphic=image;
                helpLabel=OriginalGameArt.Text("__SPARC_MINI_HelpLabel",help.transform,15,OriginalGameArt.Ink);helpLabel.alignment=TextAlignmentOptions.Center;
                help.onClick.AddListener(()=>{InstructionsExpanded=!InstructionsExpanded;var runtime=OriginalRuntime.Instance;if(runtime)runtime.activityUi.RefreshResponsiveLayout(canvas);});
            }
            timeTrack=OriginalGameArt.Image("__SPARC_MINI_TimeTrack",panel,null,new Color(.78f,.83f,.80f));timeFill=OriginalGameArt.Image("__SPARC_MINI_TimeFill",timeTrack.transform,null,white?OriginalGameArt.Mint:new Color(.78f,.35f,.32f));
            OriginalGameArt.Entity(source.Object("player"),white?"white-cell-pixel-sketch":"red-cell-pixel-sketch",white);
            foreach(var zone in area.GetComponentsInChildren<RectTransform>(true).Where(t=>t.name=="OxygenDeposit"))
            {
                OriginalGameArt.Entity(zone.gameObject,"red-cell-pixel-sketch");
                float relative=canvas.transform.InverseTransformVector(zone.TransformVector(Vector3.right)).magnitude;
                var label=OriginalGameArt.Text("__SPARC_MINI_Delivery",zone,14*OriginalMobileControls.PixelScale/Mathf.Max(.01f,relative*canvas.scaleFactor),Color.white);label.alignment=TextAlignmentOptions.Bottom;label.fontStyle=FontStyles.Bold;label.text="DELIVER O2";label.textWrappingMode=TextWrappingModes.NoWrap;
                float preferred=label.GetPreferredValues(label.text,Mathf.Infinity,Mathf.Infinity).x;label.fontSize*=Mathf.Min(1,Mathf.Max(1,zone.rect.width-20)/Mathf.Max(1,preferred));OriginalGameArt.Stretch(label.rectTransform,10,0,10,0);
            }
            for(int i=0;i<4;i++)
            {
                var instruction=Find(panel,"Instruction"+i);if(!instruction)continue;
                var art=instruction.GetComponentsInChildren<Image>(true).FirstOrDefault();if(art){art.sprite=i==1?OriginalUiIcons.Get("joystick"):OriginalGameArt.Get(i==0?(white?"white-cell-pixel-sketch":"red-cell-pixel-sketch"):i==2?(white?"bacterium-pixel-sketch":"oxygen-pixel-sketch"):"red-cell-pixel-sketch");art.overrideSprite=null;art.color=i==1?OriginalGameArt.Ink:Color.white;art.material=null;}
            }
            var stick=Find(transform,"Joystick");if(stick){var image=stick.GetComponent<Image>();if(image){image.sprite=OriginalUiIcons.Get("joystick");image.overrideSprite=null;image.color=OriginalGameArt.Paper;image.preserveAspect=true;}}
            foreach(var button in GetComponentsInChildren<Button>(true))
            {OriginalGameArt.Style(button.targetGraphic as Image,button.name.Contains("Cancel")?"secondary":"primary");foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))text.color=button.name.Contains("Cancel")?OriginalGameArt.Ink:Color.white;}
            foreach(var overlay in new[]{"VictoryScreen","LoadingScreen"})
            {
                var root=Find(transform,overlay);if(!root)continue;var image=root.GetComponent<Image>();if(image){image.sprite=null;image.color=white?new Color(.035f,.075f,.10f,.98f):new Color(.12f,.045f,.075f,.98f);}
                var decoration=OriginalGameArt.Image("__SPARC_MINI_OverlayCharacter",root,OriginalGameArt.Get(white?"white-cell-pixel-sketch":"red-cell-pixel-sketch"),Color.white);decoration.preserveAspect=true;
                PositionOverlayArt(root,decoration);
            }
            var confirmation=Find(transform,"Box");if(confirmation)OriginalGameArt.Style(confirmation.GetComponent<Image>(),"dark");
            Layout();
        }
        void PositionOverlayArt(RectTransform root,Image decoration)
        {
            float unit=OriginalMobileControls.PixelScale/Mathf.Max(.01f,canvas.scaleFactor),top=float.NegativeInfinity;var corners=new Vector3[4];
            foreach(var text in root.GetComponentsInChildren<TMP_Text>(true).Where(t=>!t.GetComponentInParent<Button>(true)))
            {text.rectTransform.GetWorldCorners(corners);foreach(var corner in corners)top=Mathf.Max(top,root.InverseTransformPoint(corner).y);}
            if(float.IsNegativeInfinity(top))top=root.rect.center.y;
            float size=Mathf.Min(88*unit,Mathf.Max(0,root.rect.yMax-top-32*unit));decoration.enabled=size>=24*unit;
            var rect=decoration.rectTransform;rect.anchorMin=rect.anchorMax=new Vector2(.5f,.5f);rect.pivot=new Vector2(.5f,.5f);rect.sizeDelta=Vector2.one*size;rect.anchoredPosition=new Vector2(0,top-root.rect.center.y+12*unit+size/2);
        }
        public void Bind(OriginalMiniGame value){game=value;Prepare(game.source);}
        public void Token(GameObject root,string kind)
        {
            OriginalGameArt.Entity(root,kind=="oxygenPrefab"?"oxygen-pixel-sketch":kind=="bacteriaPrefab"?"bacterium-pixel-sketch":"red-cell-pixel-sketch");
            if(kind=="helpfulCellPrefab")
            {var oxygen=OriginalGameArt.Image("__SPARC_MINI_FriendlyOxygen",root.transform,OriginalGameArt.Get("oxygen-pixel-sketch"),Color.white);oxygen.preserveAspect=true;var r=oxygen.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,.5f);r.sizeDelta=new Vector2(55,45);r.anchoredPosition=new Vector2(0,5);}
        }
        void Layout()
        {
            if(!canvas||!panel||!title)return;float u=OriginalMobileControls.PixelScale/Mathf.Max(.01f,canvas.scaleFactor),w=panel.rect.width/u,h=panel.rect.height/u;
            OriginalGameArt.Box(title.rectTransform,14,12,w-28,27,u);title.fontSize=19*u;
            OriginalGameArt.Box(tagline.rectTransform,14,39,w-28,20,u);tagline.fontSize=12*u;
            OriginalGameArt.Box(timeTrack.rectTransform,14,65,w-28,4,u);
            OriginalGameArt.Box(flow.rectTransform,14,h-100,w-28,22,u);flow.fontSize=13*u;flow.alignment=TextAlignmentOptions.MidlineLeft;flow.gameObject.SetActive(!InstructionsExpanded);
            if(help){OriginalGameArt.Box(help.transform as RectTransform,12,h-68,112,56,u);OriginalGameArt.Style(help.targetGraphic as Image,"secondary");OriginalGameArt.Stretch(helpLabel.rectTransform,5*u,4*u,5*u,4*u);helpLabel.enableAutoSizing=false;helpLabel.margin=Vector4.zero;helpLabel.textWrappingMode=TextWrappingModes.NoWrap;helpLabel.fontSize=15*u;helpLabel.color=OriginalGameArt.Ink;helpLabel.text=InstructionsExpanded?"Hide help":"How to play";}
        }
        public void Pickup(Vector3 position)
        {if(game&&game.Player)game.Player.GetComponent<OriginalMiniSpriteMotion>()?.Pulse(true);Show("Oxygen collected!  Find a red cell",position,new Color(.35f,.85f,.94f),true);}
        public void Collected(OriginalMiniToken token,int value)
        {
            if(value>0){streak=Time.unscaledTime<=streakUntil?streak+1:1;streakUntil=Time.unscaledTime+10;best=Mathf.Max(best,streak);}else streak=0;
            string text=value<0?"That's a red cell — protect it!":game.IsWhite?"Germ cleared  +"+value:"Oxygen delivered  +"+value;
            if(streak>=3)text+="   ·   "+streak+" in a row!";
            if(game.Player)game.Player.GetComponent<OriginalMiniSpriteMotion>()?.Pulse(value>0);
            if(!game.IsWhite)foreach(var zone in area.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name=="OxygenDeposit"))if(Vector3.Distance(zone.TransformPoint(zone.rect.center),token.transform.position)<zone.rect.width*zone.lossyScale.x*.8f)zone.GetComponent<OriginalMiniSpriteMotion>()?.Pulse(true);
            Show(text,token.transform.position,value<0?new Color(1,.55f,.45f):new Color(.60f,.93f,.74f),true);
        }
        void Show(string message,Vector3 position,Color color,bool burst)
        {
            if(!area)return;
            if(!banner){banner=OriginalGameArt.Text("__SPARC_MINI_Reward",area,25,Color.white);banner.fontStyle=FontStyles.Bold;banner.alignment=TextAlignmentOptions.Center;var outline=banner.gameObject.AddComponent<UnityEngine.UI.Shadow>();outline.effectColor=new Color(.02f,.06f,.08f,.9f);outline.effectDistance=new Vector2(2,-2);}
            banner.text=message;banner.color=color;bannerUntil=Time.unscaledTime+1.5f;var r=banner.rectTransform;r.anchorMin=r.anchorMax=new Vector2(.5f,1);r.pivot=new Vector2(.5f,1);r.anchoredPosition=new Vector2(0,-50);r.sizeDelta=new Vector2(Mathf.Max(100,area.rect.width-100),70);banner.transform.SetAsLastSibling();
            if(!burst)return;
            Vector2 point=area.InverseTransformPoint(position);
            for(int j=0;j<8;j++)
            {
                int i=particleCursor++%particles.Length;
                if(!particles[i]){var image=OriginalGameArt.Image("__SPARC_MINI_Pixel"+i,area,null,color);particles[i]=image.rectTransform;particles[i].anchorMin=particles[i].anchorMax=new Vector2(.5f,.5f);particles[i].sizeDelta=new Vector2(8,8);}
                particles[i].GetComponent<Image>().color=color;particles[i].gameObject.SetActive(true);particles[i].SetAsLastSibling();origins[i]=point;float angle=j*Mathf.PI/4;velocities[i]=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*90;births[i]=Time.unscaledTime;
            }
        }
        void LateUpdate()
        {
            Layout();if(!game)return;
            if(wasTransitioning&&!game.Transitioning&&!game.Finished)Show("LEVEL "+game.Level+"  ·  New wave!",game.Player.transform.position,OriginalGameArt.Mint,true);wasTransitioning=game.Transitioning;
            if(timeFill){float fraction=Mathf.Clamp01(1-game.Elapsed/Mathf.Max(1,game.Duration));timeFill.rectTransform.anchorMin=Vector2.zero;timeFill.rectTransform.anchorMax=new Vector2(fraction,1);timeFill.rectTransform.offsetMin=timeFill.rectTransform.offsetMax=Vector2.zero;}
            if(flow){flow.gameObject.SetActive(!InstructionsExpanded);flow.text=streak>=3?streak+" in a row!":game.IsWhite?"Best clean streak: "+best:"Deliveries: "+Mathf.Max(0,game.Score);}
            if(banner){float remaining=bannerUntil-Time.unscaledTime;banner.gameObject.SetActive(remaining>0&&!game.Finished&&!game.Transitioning);var c=banner.color;c.a=Mathf.Clamp01(remaining*3);banner.color=c;}
            for(int i=0;i<particles.Length;i++)if(particles[i]&&particles[i].gameObject.activeSelf)
            {float age=Time.unscaledTime-births[i];if(age>.65f||game.Finished){particles[i].gameObject.SetActive(false);continue;}particles[i].anchoredPosition=origins[i]+velocities[i]*age+Vector2.down*(age*age*50);var c=particles[i].GetComponent<Image>().color;c.a=1-age/.65f;particles[i].GetComponent<Image>().color=c;}
        }
    }
}
