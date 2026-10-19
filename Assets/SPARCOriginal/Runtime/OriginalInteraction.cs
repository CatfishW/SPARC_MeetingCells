using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Restores the source outline, curved connector and native Talk/Observe menu contract.</summary>
    [DefaultExecutionOrder(2200)]
    public sealed class OriginalInteraction:MonoBehaviour
    {
        public OriginalRuntime runtime;
        sealed class Entry
        {
            public OriginalBehaviour controller,talk,outline;public GameObject menu;public Canvas canvas;
            public RectTransform card,buttonGroup;public CanvasGroup group;public LineRenderer line;
            public Button[] buttons;public Renderer[] renderers;public Material[][] originals;public Material highlight;
            public TMP_Text heading;public bool outlined,explicitHold,talkUsed;public float progress,cooldown;
        }
        readonly List<Entry> entries=new();Entry selected;
        void Start(){Initialize();}
        public bool MenuVisible=>selected!=null&&selected.menu&&selected.menu.activeInHierarchy&&selected.group&&selected.group.alpha>.95f&&selected.group.interactable;
        public bool HighlightVisible=>selected!=null&&selected.outlined;
        public int RestoredControllers=>entries.Count;
        public bool HasMenuFor(OriginalBehaviour talk)=>entries.Any(e=>e.talk==talk);
        public object Snapshot=>new {sourceId=selected?.controller.sourceId,npc=selected?.talk.name,highlight=HighlightVisible,menuVisible=MenuVisible,lineProgress=selected?.progress??0,nativeButtons=selected?.buttons.Length??0,controllers=entries.Count};
        public void Initialize()
        {
            if(entries.Count>0||!runtime)return;
            foreach(var c in runtime.OfType("InteractionLineController"))
            {
                var talk=c.GetComponentsInParent<OriginalBehaviour>(true).FirstOrDefault(x=>x.sourceType=="NPC_Talk")??c.GetComponentsInChildren<OriginalBehaviour>(true).FirstOrDefault(x=>x.sourceType=="NPC_Talk");
                if(!talk)continue;var menu=c.Object("menuInstance");if(!menu)continue;
                var e=new Entry{controller=c,talk=talk,menu=menu,line=c.Component<LineRenderer>("lineRenderer"),outline=c.GetComponents<OriginalBehaviour>().FirstOrDefault(x=>x.sourceType=="OutlineAndInteractionHandler")};
                var root=e.outline?e.outline.transform:talk.transform;
                e.renderers=root.GetComponentsInChildren<Renderer>(true).Where(r=>r is MeshRenderer||r is SkinnedMeshRenderer).ToArray();e.originals=e.renderers.Select(r=>r.sharedMaterials.ToArray()).ToArray();
                var shader=Resources.Load<Shader>("MeetingCellsHighlight");if(shader)e.highlight=new Material(shader){name="SPARC / nearby character highlight"};
                e.buttons=(c.Object("InteractionOptionButtonGroup")??menu).GetComponentsInChildren<Button>(true);
                e.canvas=menu.GetComponentInParent<Canvas>(true);if(!e.canvas)e.canvas=menu.AddComponent<Canvas>();
                e.canvas.renderMode=RenderMode.ScreenSpaceOverlay;e.canvas.overrideSorting=true;e.canvas.sortingOrder=260;
                OriginalComponents.Ensure<GraphicRaycaster>(e.canvas.gameObject);
                var scaler=OriginalComponents.Ensure<CanvasScaler>(e.canvas.gameObject);scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.matchWidthOrHeight=.5f;
                if(menu.GetComponent<Canvas>())e.card=OriginalGameArt.Rect("__SPARC_UI_InteractionCard",menu.transform);
                else e.card=menu.transform as RectTransform;
                var background=OriginalComponents.Ensure<Image>(e.card.gameObject);background.sprite=OriginalGameArt.Frame("paper");background.type=Image.Type.Sliced;background.color=Color.white;background.raycastTarget=true;
                e.buttonGroup=c.Object("InteractionOptionButtonGroup")?.transform as RectTransform;if(e.buttonGroup&&e.buttonGroup!=e.card){e.buttonGroup.SetParent(e.card,false);OriginalGameArt.Stretch(e.buttonGroup);}
                foreach(var layout in menu.GetComponentsInChildren<LayoutGroup>(true))layout.enabled=false;
                foreach(var fitter in menu.GetComponentsInChildren<ContentSizeFitter>(true))fitter.enabled=false;
                e.group=OriginalComponents.Ensure<CanvasGroup>(menu);e.group.alpha=0;e.group.interactable=e.group.blocksRaycasts=false;
                foreach(var graphic in menu.GetComponentsInChildren<Graphic>(true))if(graphic.transform!=e.card&&!(graphic is TMP_Text)&&!graphic.GetComponent<Button>())graphic.enabled=false;
                e.heading=OriginalGameArt.Text("__SPARC_UI_InteractionTitle",e.card,18,OriginalUiTheme.Ink);e.heading.fontStyle=FontStyles.Bold;
                for(int i=0;i<e.buttons.Length;i++)
                {
                    var button=e.buttons[i];bool observe=i==1;
                    var image=OriginalComponents.Ensure<Image>(button.gameObject);image.enabled=true;image.sprite=OriginalGameArt.Frame(observe?"secondary":"primary");image.type=Image.Type.Sliced;image.color=Color.white;button.targetGraphic=image;
                    foreach(var graphic in button.GetComponentsInChildren<Graphic>(true))if(graphic!=image)graphic.enabled=false;
                    var label=OriginalGameArt.Text("__SPARC_UI_ActionLabel",button.transform,17,observe?OriginalUiTheme.Ink:Color.white);label.alignment=TextAlignmentOptions.Center;label.text=observe?"Observe":"Talk  /  E";
                    var sourceAction=observe?"StartObserve":"StartTalk";bool already=Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Any(n=>button.onClick.GetPersistentMethodName(n)==sourceAction&&button.onClick.GetPersistentTarget(n)==talk);
                    if(!already)button.onClick.AddListener(()=>Use(e,observe));else button.onClick.AddListener(()=>Suppress(e,false));
                }
                if(e.line){e.line.enabled=false;e.line.positionCount=15;e.line.useWorldSpace=true;e.line.startColor=new Color(.94f,.86f,.56f);e.line.endColor=new Color(.43f,.79f,.75f);}
                menu.SetActive(false);entries.Add(e);
            }
        }
        public string DisplayName(OriginalBehaviour talk)
        {string block=talk?talk.Text("npcTalk_blockName"):"";return block.Contains("RBC",StringComparison.OrdinalIgnoreCase)?"Red":block.Contains("WBC",StringComparison.OrdinalIgnoreCase)?"White":"Cap";}
        void Use(Entry e,bool observe)
        {
            if(selected!=e||!MenuVisible||!Allowed(e))return;
            if(!observe&&e.talkUsed&&e.talk.Flag("ShouldDisableUsedButton"))return;
            if(!observe)e.talkUsed=true;Suppress(e,false);runtime.Call(e.talk,observe?"StartObserve":"StartTalk");
        }
        public bool TryTalk()
        {if(selected==null||!Allowed(selected))return false;selected.progress=1;selected.group.alpha=1;selected.group.interactable=true;Use(selected,false);return true;}
        void Suppress(Entry e,bool hold)
        {e.explicitHold|=hold;e.cooldown=Time.unscaledTime+3;e.progress=0;if(e.menu)e.menu.SetActive(false);if(e.line)e.line.enabled=false;Outline(e,false);}
        public void OnInteraction(OriginalBehaviour talk,bool observing=false){foreach(var e in entries)if(e.talk==talk){if(!observing)e.talkUsed=true;Suppress(e,false);}}
        public void ResetTalk(OriginalBehaviour talk){foreach(var e in entries)if(e.talk==talk)e.talkUsed=false;}
        public bool Call(OriginalBehaviour source,string method,object arg)
        {
            var e=entries.FirstOrDefault(x=>x.controller==source||x.outline==source);if(e==null)return false;
            switch(method)
            {
                case "OnEitherButtonClicked":Suppress(e,false);break;
                case "SuppressInteractionUi":Suppress(e,arg==null||Convert.ToBoolean(arg));break;
                case "OnInteractionEnd":case "ReleaseInteractionUi":e.explicitHold=false;e.cooldown=0;break;
                case "EnableDrawing":e.explicitHold=false;break;
                case "DisableDrawing":Suppress(e,false);break;
                default:return false;
            }
            return true;
        }
        bool Allowed(Entry e)=>runtime&&runtime.Started&&!runtime.ShuttingDown&&!runtime.HandoffStarted&&runtime.player&&runtime.player.CanControl&&!runtime.presentation.IsSpeaking&&!runtime.CinematicActive&&!runtime.activities.IsOpen&&!runtime.activities.IsWorksheetOpen&&!(runtime.activityUi&&runtime.activityUi.IsTaskPanelOpen)&&e.talk&&e.talk.isActiveAndEnabled&&runtime.NearestTalk==e.talk&&!e.explicitHold;
        void Outline(Entry e,bool visible)
        {
            if(e.outlined==visible)return;e.outlined=visible;
            for(int i=0;i<e.renderers.Length;i++)if(e.renderers[i])e.renderers[i].sharedMaterials=visible&&e.highlight?e.originals[i].Concat(new[]{e.highlight}).ToArray():e.originals[i];
        }
        void Hide(Entry e)
        {e.progress=0;if(e.group){e.group.alpha=0;e.group.interactable=e.group.blocksRaycasts=false;}if(e.menu)e.menu.SetActive(false);if(e.line)e.line.enabled=false;Outline(e,false);}
        void LateUpdate()
        {
            Initialize();var next=entries.FirstOrDefault(Allowed);
            if(selected!=next){if(selected!=null)Hide(selected);selected=next;}
            foreach(var e in entries)if(e!=selected)Hide(e);
            if(selected==null)return;var item=selected;if(Time.unscaledTime<item.cooldown){Hide(item);return;}
            Outline(item,true);if(item.highlight)item.highlight.SetFloat("_Width",2.8f+Mathf.Sin(Time.unscaledTime*3.2f)*.3f);item.progress=Mathf.MoveTowards(item.progress,1,Time.unscaledDeltaTime*Mathf.Max(.5f,item.controller.Number("drawSpeed",1))*2);
            var camera=Camera.main;if(!camera)return;
            var anchor=item.controller.Component<Transform>("npcPosition");Vector3 world=anchor?anchor.position:item.talk.transform.position+Vector3.up*1.4f;
            var point=camera.WorldToScreenPoint(world);if(point.z<.1f){Hide(item);return;}
            var safe=runtime.GetComponent<OriginalMobileControls>()?.SafeScreenRect??Screen.safeArea;float density=OriginalMobileControls.PixelScale,unit=density/Mathf.Max(.01f,item.canvas.scaleFactor);
            float w=Mathf.Min(360,safe.width/density-32),h=126;float right=safe.xMax/density,bottom=(Screen.height-safe.yMin)/density-(OriginalHudLayout.HasTouch?176:22);
            float x=point.x/density+32;if(x+w>right-16)x=point.x/density-w-32;x=Mathf.Clamp(x,safe.xMin/density+16,right-w-16);
            float y=Mathf.Clamp((Screen.height-point.y)/density-20,(Screen.height-safe.yMax)/density+150,Mathf.Max(150,bottom-h));
            item.canvas.transform.rotation=Quaternion.identity;var scale=item.canvas.transform.parent?item.canvas.transform.parent.lossyScale:Vector3.one;item.canvas.transform.localScale=new Vector3(1/Mathf.Max(.0001f,Mathf.Abs(scale.x)),1/Mathf.Max(.0001f,Mathf.Abs(scale.y)),1/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
            if(!item.menu.activeSelf)item.menu.SetActive(true);item.group.alpha=Mathf.SmoothStep(0,1,item.progress);item.group.interactable=item.group.blocksRaycasts=item.progress>.98f;
            OriginalGameArt.Box(item.card,x,y,w,h,unit);OriginalGameArt.Box(item.heading.rectTransform,16,12,w-32,30,unit);item.heading.fontSize=18*unit;item.heading.text=DisplayName(item.talk)+"  ·  Nearby";
            float bw=(w-44)/2;
            for(int i=0;i<item.buttons.Length;i++)
                {var button=item.buttons[i];button.gameObject.SetActive(true);button.interactable=i!=0||!item.talkUsed||!item.talk.Flag("ShouldDisableUsedButton");OriginalGameArt.Style(button.targetGraphic as Image,i==0?"primary":"secondary");OriginalGameArt.Box(button.transform as RectTransform,16+i*(bw+12),58,bw,52,unit);var label=button.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="__SPARC_UI_ActionLabel");if(label){OriginalGameArt.Stretch(label.rectTransform,8*unit,4*unit,8*unit,4*unit);label.margin=Vector4.zero;label.color=i==0?Color.white:OriginalGameArt.Ink;label.fontSize=17*unit;if(i==0)label.text=OriginalHudLayout.HasTouch?"Talk":"Talk  /  E";}var icon=button.transform.Find("__SPARC_UI_Icon");if(icon)icon.gameObject.SetActive(false);}
            if(item.line)
            {
                item.line.enabled=true;float depth=Mathf.Max(.5f,point.z);Vector3 end=camera.ScreenToWorldPoint(new Vector3((x+(point.x/density<x?0:w))*density,Screen.height-(y+48)*density,depth));
                Vector3 p1=Vector3.Lerp(world,end,.38f)+Vector3.up*item.controller.Number("curveHeight",.7f),p2=Vector3.Lerp(world,end,.72f)-Vector3.up*item.controller.Number("secondCurveHeight",.8f);
                for(int i=0;i<15;i++){float t=i/14f*item.progress,u=1-t;item.line.SetPosition(i,u*u*u*world+3*u*u*t*p1+3*u*t*t*p2+t*t*t*end);}
                item.line.widthMultiplier=Mathf.Clamp(2*depth*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad/2)/Mathf.Max(1,Screen.height)*2.2f,.006f,.04f);
            }
        }
        void OnDisable(){foreach(var e in entries)Hide(e);selected=null;}
        void OnDestroy(){foreach(var e in entries){Outline(e,false);if(e.highlight)Destroy(e.highlight);}}
    }
}
