using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Restores the source outline + native Talk/Observe menu contract, presented as an
    /// in-world field-note layer. Guides in view but out of reach carry a quiet name tag and a dot;
    /// in reach, a white silhouette, a name label with a caret, a hand-inked curve and brush-stroke
    /// action chips (E Talk / Q Observe) that remain the source's own Buttons.</summary>
    [DefaultExecutionOrder(2200)]
    public sealed class OriginalInteraction:MonoBehaviour
    {
        public OriginalRuntime runtime;
        sealed class Entry
        {
            public OriginalBehaviour controller,talk,outline;public GameObject menu;public Canvas canvas;
            public RectTransform card,buttonGroup;public CanvasGroup group;public LineRenderer line;
            public Button[] buttons;public Renderer[] renderers;public Material[][] originals;public Material highlight;
            public TMP_Text heading,headingShadow,kicker;public Image caret;public OriginalUiCurve[] curves;
            public Image[] glyphs,rings,taps;public TMP_Text[] keys,verbs,verbShadows;public OriginalChipHover[] hovers;public float[] sweep;
            public RectTransform tag;public TMP_Text tagName,tagShadow;public Image tagDot,tagHalo;public float tagAlpha;
            public bool outlined,explicitHold,talkUsed;public float progress,cooldown;
        }
        readonly List<Entry> entries=new();Entry selected;
        Canvas tagCanvas;TMP_Text hint,hintShadow;float hintShownAt=-1;
        void Start(){Initialize();}
        public bool MenuVisible=>selected!=null&&selected.menu&&selected.menu.activeInHierarchy&&selected.group&&selected.group.alpha>.95f&&selected.group.interactable;
        public bool HighlightVisible=>selected!=null&&selected.outlined;
        public int RestoredControllers=>entries.Count;
        public bool HasMenuFor(OriginalBehaviour talk)=>entries.Any(e=>e.talk==talk);
        public object Snapshot=>new {sourceId=selected?.controller.sourceId,npc=selected?.talk.name,highlight=HighlightVisible,menuVisible=MenuVisible,lineProgress=selected?.progress??0,nativeButtons=selected?.buttons.Length??0,controllers=entries.Count,tags=entries.Count(e=>e.tagAlpha>.05f)};
        const string Own="__SPARC_UI_";
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
                var shader=Resources.Load<Shader>("MeetingCellsHighlight");if(shader){e.highlight=new Material(shader){name="SPARC / nearby character silhouette"};e.highlight.SetColor("_OutlineColor",Color.white);}
                e.buttons=(c.Object("InteractionOptionButtonGroup")??menu).GetComponentsInChildren<Button>(true);
                e.canvas=menu.GetComponentInParent<Canvas>(true);if(!e.canvas)e.canvas=menu.AddComponent<Canvas>();
                e.canvas.renderMode=RenderMode.ScreenSpaceOverlay;e.canvas.overrideSorting=true;e.canvas.sortingOrder=260;
                OriginalComponents.Ensure<GraphicRaycaster>(e.canvas.gameObject);
                // One canvas unit is one CSS pixel; the whole layer is laid out in screen space.
                var scaler=OriginalComponents.Ensure<CanvasScaler>(e.canvas.gameObject);scaler.uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;scaler.scaleFactor=OriginalMobileControls.PixelScale;
                if(menu.GetComponent<Canvas>())e.card=OriginalGameArt.Rect(Own+"InteractionCard",menu.transform);
                else e.card=menu.transform as RectTransform;
                var background=OriginalComponents.Ensure<Image>(e.card.gameObject);background.enabled=false;background.raycastTarget=false;
                e.buttonGroup=c.Object("InteractionOptionButtonGroup")?.transform as RectTransform;if(e.buttonGroup&&e.buttonGroup!=e.card){e.buttonGroup.SetParent(e.card,false);}
                foreach(var layout in menu.GetComponentsInChildren<LayoutGroup>(true))layout.enabled=false;
                foreach(var fitter in menu.GetComponentsInChildren<ContentSizeFitter>(true))fitter.enabled=false;
                e.group=OriginalComponents.Ensure<CanvasGroup>(menu);e.group.alpha=0;e.group.interactable=e.group.blocksRaycasts=false;
                foreach(var graphic in menu.GetComponentsInChildren<Graphic>(true))if(graphic.transform!=e.card&&!(graphic is TMP_Text)&&!graphic.GetComponent<Button>())graphic.enabled=false;
                foreach(var text in menu.GetComponentsInChildren<TMP_Text>(true))text.enabled=false;
                e.curves=new[]{Curve(e.card,Own+"InteractionInk"),Curve(e.card,Own+"InteractionInkBranch")};
                e.headingShadow=Label(Own+"InteractionTitleShadow",e.card,24,true);e.heading=Label(Own+"InteractionTitle",e.card,24,true);
                e.kicker=Label(Own+"InteractionKicker",e.card,12,true);e.kicker.characterSpacing=12;
                e.caret=OriginalGameArt.Image(Own+"InteractionCaret",e.card,OriginalInteractionArt.Caret,Color.white);
                int n=e.buttons.Length;e.glyphs=new Image[n];e.rings=new Image[n];e.taps=new Image[n];e.keys=new TMP_Text[n];e.verbs=new TMP_Text[n];e.verbShadows=new TMP_Text[n];e.hovers=new OriginalChipHover[n];e.sweep=new float[n];
                for(int i=0;i<n;i++)
                {
                    var button=e.buttons[i];bool observe=i==1;
                    var image=OriginalComponents.Ensure<Image>(button.gameObject);image.enabled=true;button.targetGraphic=image;
                    foreach(var graphic in button.GetComponentsInChildren<Graphic>(true))if(graphic!=image)graphic.enabled=false;
                    e.verbShadows[i]=Label(Own+"ActionLabelShadow",button.transform,20,true);e.verbs[i]=Label(Own+"ActionLabel",button.transform,20,true);e.verbs[i].text=observe?"Observe":"Talk";
                    e.glyphs[i]=OriginalGameArt.Image(Own+"ActionGlyph",button.transform,OriginalInteractionArt.Disc,new Color(.07f,.12f,.15f,.92f));e.glyphs[i].raycastTarget=true;
                    e.rings[i]=OriginalGameArt.Image(Own+"ActionGlyphRing",e.glyphs[i].transform,OriginalInteractionArt.Ring,Color.white);
                    e.taps[i]=OriginalGameArt.Image(Own+"ActionGlyphTap",e.glyphs[i].transform,OriginalInteractionArt.Disc,Color.white);
                    e.keys[i]=Label(Own+"ActionKey",e.glyphs[i].transform,16,true);e.keys[i].alignment=TextAlignmentOptions.Center;e.keys[i].text=observe?"Q":"E";
                    e.hovers[i]=OriginalComponents.Ensure<OriginalChipHover>(button.gameObject);
                    var sourceAction=observe?"StartObserve":"StartTalk";bool already=Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Any(k=>button.onClick.GetPersistentMethodName(k)==sourceAction&&button.onClick.GetPersistentTarget(k)==talk);
                    if(!already)button.onClick.AddListener(()=>Use(e,observe));else button.onClick.AddListener(()=>Suppress(e,false));
                }
                if(e.line){e.line.enabled=false;e.line.positionCount=15;e.line.useWorldSpace=true;}
                menu.SetActive(false);entries.Add(e);
            }
            TagCanvas();foreach(var e in entries)BuildTag(e);
        }
        static TMP_Text Label(string name,Transform parent,float size,bool bold)
        {var t=OriginalGameArt.Text(name,parent,size,Color.white);t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Overflow;t.alignment=TextAlignmentOptions.MidlineLeft;t.margin=Vector4.zero;return t;}
        static OriginalUiCurve Curve(Transform parent,string name)
        {var rect=OriginalGameArt.Rect(name,parent);var curve=rect.GetComponent<OriginalUiCurve>()??rect.gameObject.AddComponent<OriginalUiCurve>();curve.raycastTarget=false;curve.color=new Color(1,1,1,.94f);OriginalGameArt.Stretch(rect);return curve;}
        void TagCanvas()
        {
            if(tagCanvas)return;
            var obj=new GameObject(Own+"InteractionTags",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler));obj.transform.SetParent(transform,false);
            tagCanvas=obj.GetComponent<Canvas>();tagCanvas.renderMode=RenderMode.ScreenSpaceOverlay;tagCanvas.overrideSorting=true;tagCanvas.sortingOrder=240;
            obj.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;
            hintShadow=Label(Own+"InteractionHintShadow",obj.transform,15,false);hint=Label(Own+"InteractionHint",obj.transform,15,false);
            foreach(var t in new[]{hint,hintShadow}){t.alignment=TextAlignmentOptions.Center;t.richText=true;t.color=new Color(1,1,1,0);}
        }
        void BuildTag(Entry e)
        {
            e.tag=OriginalGameArt.Rect(Own+"InteractionTag",tagCanvas.transform);
            e.tagShadow=Label(Own+"InteractionTagShadow",e.tag,15,true);e.tagName=Label(Own+"InteractionTagName",e.tag,15,true);
            e.tagHalo=OriginalGameArt.Image(Own+"InteractionTagHalo",e.tag,OriginalInteractionArt.Ring,Color.white);
            e.tagDot=OriginalGameArt.Image(Own+"InteractionTagDot",e.tag,OriginalInteractionArt.Disc,Color.white);
            e.tag.gameObject.SetActive(false);
        }
        public string DisplayName(OriginalBehaviour talk)
        {string block=talk?talk.Text("npcTalk_blockName"):"";return block.Contains("RBC",StringComparison.OrdinalIgnoreCase)?"Red":block.Contains("WBC",StringComparison.OrdinalIgnoreCase)?"White":"Cap";}
        static string Kicker(string name)=>name switch{"Red"=>"RED BLOOD CELL","White"=>"WHITE BLOOD CELL",_=>"YOUR GUIDE"};
        /// <summary>Each guide owns a hue: warm coral for Red, cool aqua for White, amber for Cap.</summary>
        static Color Hue(string name)=>name switch{"Red"=>new Color(.97f,.38f,.27f),"White"=>new Color(.11f,.70f,.68f),_=>new Color(.95f,.62f,.16f)};
        static readonly Color ObserveHue=new(.58f,.36f,.90f);
        void Use(Entry e,bool observe)
        {
            if(selected!=e||!MenuVisible||!Allowed(e))return;
            if(!observe&&e.talkUsed&&e.talk.Flag("ShouldDisableUsedButton"))return;
            if(!observe)e.talkUsed=true;Suppress(e,false);runtime.Call(e.talk,observe?"StartObserve":"StartTalk");
        }
        public bool TryTalk()
        {if(selected==null||!Allowed(selected))return false;selected.progress=1;selected.group.alpha=1;selected.group.interactable=true;Use(selected,false);return true;}
        public bool TryObserve()
        {if(selected==null||!Allowed(selected)||selected.buttons.Length<2)return false;selected.progress=1;selected.group.alpha=1;selected.group.interactable=true;Use(selected,true);return true;}
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
        bool Free=>runtime&&runtime.Started&&!runtime.ShuttingDown&&!runtime.HandoffStarted&&runtime.player&&runtime.player.CanControl&&!runtime.presentation.IsSpeaking&&!runtime.CinematicActive&&!runtime.activities.IsOpen&&!runtime.activities.IsWorksheetOpen&&!(runtime.activityUi&&runtime.activityUi.IsTaskPanelOpen);
        bool Allowed(Entry e)=>Free&&e.talk&&e.talk.isActiveAndEnabled&&runtime.NearestTalk==e.talk&&!e.explicitHold;
        void Outline(Entry e,bool visible)
        {
            if(e.outlined==visible)return;e.outlined=visible;
            for(int i=0;i<e.renderers.Length;i++)if(e.renderers[i])e.renderers[i].sharedMaterials=visible&&e.highlight?e.originals[i].Concat(new[]{e.highlight}).ToArray():e.originals[i];
        }
        void Hide(Entry e)
        {e.progress=0;for(int i=0;i<e.sweep.Length;i++)e.sweep[i]=0;if(e.group){e.group.alpha=0;e.group.interactable=e.group.blocksRaycasts=false;}if(e.menu)e.menu.SetActive(false);if(e.line)e.line.enabled=false;Outline(e,false);}
        Vector3 Anchor(Entry e){var anchor=e.controller.Component<Transform>("npcPosition");return anchor?anchor.position:e.talk.transform.position+Vector3.up*1.4f;}
        void LateUpdate()
        {
            Initialize();var next=entries.FirstOrDefault(Allowed);
            if(selected!=next){if(selected!=null)Hide(selected);selected=next;}
            foreach(var e in entries)if(e!=selected)Hide(e);
            var camera=Camera.main;Tags(camera);HintLine();
            if(selected==null)return;var item=selected;if(Time.unscaledTime<item.cooldown){Hide(item);return;}
            Outline(item,true);float density=OriginalMobileControls.PixelScale;
            if(item.highlight)item.highlight.SetFloat("_Width",(2.1f+Mathf.Sin(Time.unscaledTime*2.4f)*.25f)*density);
            item.progress=Mathf.MoveTowards(item.progress,1,Time.unscaledDeltaTime*Mathf.Max(.5f,item.controller.Number("drawSpeed",1))*2);
            if(!camera)return;var world=Anchor(item);var point=camera.WorldToScreenPoint(world);if(point.z<.1f){Hide(item);return;}
            if(hintShownAt<0)hintShownAt=Time.unscaledTime;
            item.canvas.GetComponent<CanvasScaler>().scaleFactor=density;item.canvas.transform.rotation=Quaternion.identity;OriginalComponents.MatchOverlayScale(item.canvas);
            if(!item.menu.activeSelf)item.menu.SetActive(true);item.group.alpha=Mathf.SmoothStep(0,1,item.progress);item.group.interactable=item.group.blocksRaycasts=item.progress>.98f;
            OriginalGameArt.Stretch(item.card);if(item.buttonGroup&&item.buttonGroup!=item.card)OriginalGameArt.Stretch(item.buttonGroup);
            float unit=density/Mathf.Max(.01f,item.canvas.scaleFactor);Layout(item,point,density,unit);
        }
        /// <summary>Screen-space composition in CSS pixels: caret + name beside the anchor, an ink
        /// curve that runs out and drops into the first choice, and a short stitch to the second.</summary>
        void Layout(Entry e,Vector3 point,float density,float unit)
        {
            var safe=runtime.GetComponent<OriginalMobileControls>()?.SafeScreenRect??Screen.safeArea;bool touch=OriginalHudLayout.HasTouch;
            float left=safe.xMin/density+12,right=safe.xMax/density-12,top=(Screen.height-safe.yMax)/density+12,bottom=(Screen.height-safe.yMin)/density-(touch?176:96);
            var anchor=new Vector2(point.x/density,(Screen.height-point.y)/density);
            string name=DisplayName(e.talk);Color hue=Hue(name);
            e.heading.text=e.headingShadow.text=name;e.kicker.text=Kicker(name);
            float nameW=e.heading.GetPreferredValues(name,9999,99).x/unit,kickW=e.kicker.GetPreferredValues(e.kicker.text,9999,99).x/unit,labelW=Mathf.Max(nameW,kickW);
            const float chip=212,glyph=36,rowGap=52;int n=e.buttons.Length;
            bool flip=anchor.x+34+labelW+64+22*(n-1)+chip>right;
            var label=new Vector2(flip?anchor.x-34-labelW:anchor.x+34,anchor.y-74);
            var g0=flip?new Vector2(label.x-28,label.y+92):new Vector2(label.x+labelW+64,label.y+92);
            // Keep the whole composition inside the safe area, nudging rather than reflowing.
            float minX=Mathf.Min(label.x-22,g0.x-glyph/2),maxX=Mathf.Max(label.x+labelW,g0.x+22*(n-1)+chip),minY=label.y-6,maxY=g0.y+rowGap*(n-1)+glyph/2;
            var shift=new Vector2(minX<left?left-minX:maxX>right?Mathf.Max(left-minX,right-maxX):0,minY<top?top-minY:maxY>bottom?Mathf.Max(top-minY,bottom-maxY):0);
            label+=shift;g0+=shift;
            float reveal=Mathf.Clamp01(e.progress*2.2f),ink=Mathf.Clamp01((e.progress-.2f)/.5f);
            float rise=(1-reveal)*8;
            Place(e.headingShadow,label.x+1.5f,label.y+rise+2,labelW+8,30,unit,new Color(0,0,0,.55f*reveal));
            Place(e.heading,label.x,label.y+rise,labelW+8,30,unit,new Color(1,1,1,reveal));
            Place(e.kicker,label.x,label.y+28+rise,labelW+8,16,unit,new Color(1,1,1,.72f*reveal));
            e.heading.fontSize=e.headingShadow.fontSize=24*unit;e.kicker.fontSize=11*unit;e.kicker.characterSpacing=14;
            // The caret points back at the guide; it flips with the composition.
            OriginalGameArt.Box(e.caret.rectTransform,flip?label.x+labelW+8:label.x-20,label.y+9+rise,12,14,unit);
            e.caret.rectTransform.localRotation=Quaternion.Euler(0,0,flip?180:0);e.caret.color=new Color(1,1,1,reveal);
            // Ink leaves from the name itself; the wider kicker sits below the stroke's run.
            var start=flip?new Vector2(label.x+labelW-nameW-26,label.y+15):new Vector2(label.x+nameW+12,label.y+15);
            var end=new Vector2(g0.x,g0.y-glyph/2-1);
            e.curves[0].Set(Local(e.curves[0],start,unit),Local(e.curves[0],new Vector2(start.x+(end.x-start.x)*.82f,start.y),unit),Local(e.curves[0],new Vector2(end.x,start.y+(end.y-start.y)*.3f),unit),Local(e.curves[0],end,unit),2*unit,ink,3*unit);
            var hover=0;
            for(int i=0;i<n;i++)
            {
                var button=e.buttons[i];button.gameObject.SetActive(true);bool observe=i==1;
                bool enabled=i!=0||!e.talkUsed||!e.talk.Flag("ShouldDisableUsedButton");button.interactable=enabled;
                var g=g0+new Vector2(22*i,rowGap*i);
                float target=Mathf.Clamp01((e.progress-.45f-.12f*i)/.4f);e.sweep[i]=Mathf.MoveTowards(e.sweep[i],target,Time.unscaledDeltaTime*4);
                float s=1-Mathf.Pow(1-e.sweep[i],3);bool over=e.hovers[i]&&e.hovers[i].Hover&&enabled;if(over)hover=i+1;
                float length=(chip+(over?14:0))*Mathf.Max(.02f,s);
                var image=button.targetGraphic as Image;if(image){image.sprite=OriginalInteractionArt.Streak;image.type=Image.Type.Simple;image.material=null;var tint=observe?ObserveHue:hue;image.color=new Color(tint.r,tint.g,tint.b,1);image.raycastTarget=true;}
                var colors=button.colors;var wanted=new ColorBlock{normalColor=Color.white,highlightedColor=Color.white,pressedColor=new Color(.78f,.78f,.78f,1),selectedColor=Color.white,disabledColor=new Color(.55f,.55f,.55f,.45f),colorMultiplier=1,fadeDuration=.08f};
                if(!colors.Equals(wanted)){button.colors=wanted;button.transition=Selectable.Transition.ColorTint;}
                OriginalGameArt.Box(button.transform as RectTransform,g.x,g.y-18,length,36,unit);
                var icon=button.transform.Find(Own+"Icon");if(icon)icon.gameObject.SetActive(false);
                OriginalGameArt.Box(e.glyphs[i].rectTransform,-glyph/2,(36-glyph)/2,glyph,glyph,unit);e.glyphs[i].transform.SetAsLastSibling();
                float pop=over?1.08f:1;e.glyphs[i].rectTransform.localScale=Vector3.one*pop;
                OriginalGameArt.Stretch(e.rings[i].rectTransform);e.rings[i].color=new Color(1,1,1,.95f);
                OriginalGameArt.Box(e.taps[i].rectTransform,glyph/2-5,glyph/2-5,10,10,unit);e.taps[i].gameObject.SetActive(touch);
                e.keys[i].gameObject.SetActive(!touch);Place(e.keys[i],0,0,glyph,glyph,unit,Color.white);e.keys[i].alignment=TextAlignmentOptions.Center;e.keys[i].characterSpacing=0;e.keys[i].fontSize=16*unit;e.keys[i].text=observe?"Q":"E";
                float textAlpha=Mathf.Clamp01((s-.35f)/.4f)*(enabled?1:.5f);
                Place(e.verbShadows[i],glyph/2+11.5f,2+1.5f,chip-glyph,34,unit,new Color(0,0,0,.35f*textAlpha));
                Place(e.verbs[i],glyph/2+10,2,chip-glyph,34,unit,new Color(1,1,1,textAlpha));
                e.verbs[i].fontSize=e.verbShadows[i].fontSize=20*unit;e.verbs[i].characterSpacing=e.verbShadows[i].characterSpacing=0;e.verbs[i].alignment=e.verbShadows[i].alignment=TextAlignmentOptions.MidlineLeft;e.verbs[i].text=e.verbShadows[i].text=observe?"Observe":"Talk";
            }
            // The branch stitches the first choice to the second, so both read as one decision.
            if(n>1){var a=g0+new Vector2(0,glyph/2+1);var b=g0+new Vector2(22-glyph/2-1,rowGap);e.curves[1].Set(Local(e.curves[1],a,unit),Local(e.curves[1],new Vector2(a.x,b.y-10),unit),Local(e.curves[1],new Vector2(b.x-12,b.y),unit),Local(e.curves[1],b,unit),1.6f*unit,Mathf.Clamp01((e.progress-.6f)/.3f),0);}
            else e.curves[1].Set(Vector2.zero,Vector2.zero,Vector2.zero,Vector2.zero,0,0,0);
        }
        /// <summary>The theme's one-time button pass gives labels auto-size and an icon inset;
        /// this layer owns its type, so those are cleared on every placement.</summary>
        static void Place(TMP_Text text,float x,float y,float w,float h,float unit,Color color)
        {OriginalGameArt.Box(text.rectTransform,x,y,w,h,unit);text.color=color;text.raycastTarget=false;text.enableAutoSizing=false;text.margin=Vector4.zero;text.textWrappingMode=TextWrappingModes.NoWrap;}
        static Vector2 Local(Graphic graphic,Vector2 css,float unit)
        {var r=graphic.rectTransform.rect;return new Vector2(css.x*unit+r.xMin,r.yMax-css.y*unit);}
        /// <summary>Guides in view but not yet in reach: a quiet name and a dot in their hue. The
        /// current objective's guide breathes a halo. Hidden behind walls and while busy.</summary>
        void Tags(Camera camera)
        {
            if(!tagCanvas)return;float density=OriginalMobileControls.PixelScale;tagCanvas.GetComponent<CanvasScaler>().scaleFactor=density;
            var goal=runtime&&runtime.objectives!=null?runtime.objectives.ActiveTalk?.Object("NPCTalkTo"):null;
            foreach(var e in entries)
            {
                float want=0;Vector3 point=default;
                if(camera&&Free&&e!=selected&&e.talk&&e.talk.isActiveAndEnabled&&runtime.TalkAvailable(e.talk))
                {
                    var world=Anchor(e);point=camera.WorldToScreenPoint(world);float distance=Vector3.Distance(runtime.player.transform.position,e.talk.transform.position);
                    bool onScreen=point.z>.3f&&point.x>0&&point.x<Screen.width&&point.y>0&&point.y<Screen.height;
                    if(onScreen&&distance<16&&!Occluded(camera,world,e))want=Mathf.Clamp01((16-distance)/5)*.92f;
                }
                e.tagAlpha=Mathf.MoveTowards(e.tagAlpha,want,Time.unscaledDeltaTime*3);
                bool show=e.tagAlpha>.01f;if(e.tag.gameObject.activeSelf!=show)e.tag.gameObject.SetActive(show);if(!show)continue;
                if(want>0){var css=new Vector2(point.x/density,(Screen.height-point.y)/density);OriginalGameArt.Box(e.tag,css.x-150,css.y-12,300,24,1);}
                string name=DisplayName(e.talk);float width=e.tagName.GetPreferredValues(name,999,99).x;
                // Name to the left, dot on the anchor: the dot is the thing to walk to.
                Place(e.tagShadow,150-14-width+1,1.5f,width+4,24,1,new Color(0,0,0,.5f*e.tagAlpha));Place(e.tagName,150-14-width,0,width+4,24,1,new Color(1,1,1,e.tagAlpha));
                e.tagName.text=e.tagShadow.text=name;e.tagName.fontSize=e.tagShadow.fontSize=15;
                var hue=Color.Lerp(Color.white,Hue(name),.35f);OriginalGameArt.Box(e.tagDot.rectTransform,150-5,7,10,10,1);e.tagDot.color=new Color(hue.r,hue.g,hue.b,e.tagAlpha);
                bool objective=goal&&e.talk.transform.IsChildOf(goal.transform)||goal==e.talk.gameObject;
                float pulse=objective?(Time.unscaledTime*.8f)%1:0;float halo=10+pulse*18;
                OriginalGameArt.Box(e.tagHalo.rectTransform,150-halo/2,12-halo/2,halo,halo,1);e.tagHalo.color=new Color(1,1,1,objective?(1-pulse)*.7f*e.tagAlpha:0);
            }
        }
        bool Occluded(Camera camera,Vector3 world,Entry e)
        {
            var from=camera.transform.position;var hits=Physics.RaycastAll(from,(world-from).normalized,Vector3.Distance(from,world)-.3f,~0,QueryTriggerInteraction.Ignore);
            foreach(var hit in hits){var t=hit.transform;if(t.IsChildOf(e.talk.transform)||(e.outline&&t.IsChildOf(e.outline.transform))||t.IsChildOf(runtime.player.transform)||t.GetComponentInParent<OriginalCrowdActor>())continue;return true;}
            return false;
        }
        /// <summary>First time a choice appears, a one-line field note teaches the verbs, then fades.</summary>
        void HintLine()
        {
            if(!hint)return;float age=hintShownAt<0?-1:Time.unscaledTime-hintShownAt;
            float alpha=age<0?0:Mathf.Clamp01(age/.5f)*Mathf.Clamp01((8-age)/1.2f);
            bool touch=OriginalHudLayout.HasTouch;string text=touch?"Tap a choice to talk or observe   ·   swipe to look around":"<b>E</b>  talk     ·     <b>Q</b>  observe     ·     <b>drag</b>  look around";
            float density=OriginalMobileControls.PixelScale,width=Screen.width/density;
            Place(hintShadow,width/2-300+1,98+1.5f,600,24,1,new Color(0,0,0,.5f*alpha));Place(hint,width/2-300,98,600,24,1,new Color(1,1,1,.92f*alpha));
            hint.text=hintShadow.text=text;hint.fontSize=hintShadow.fontSize=15;hint.alignment=hintShadow.alignment=TextAlignmentOptions.Center;
        }
        void OnDisable(){foreach(var e in entries){Hide(e);if(e.tag)e.tag.gameObject.SetActive(false);e.tagAlpha=0;}selected=null;}
        void OnDestroy(){foreach(var e in entries){Outline(e,false);if(e.highlight)Destroy(e.highlight);}}
    }

    /// <summary>Pointer hover for a choice chip; the chip leans out and its glyph lifts.</summary>
    public sealed class OriginalChipHover:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        public bool Hover {get;private set;}
        public void OnPointerEnter(PointerEventData eventData){Hover=true;}
        public void OnPointerExit(PointerEventData eventData){Hover=false;}
        void OnDisable(){Hover=false;}
    }

    /// <summary>A thin ink stroke along a cubic curve, drawn progressively, in a canvas.</summary>
    public sealed class OriginalUiCurve:MaskableGraphic
    {
        Vector2 a,b,c,d;float thickness,progress,dot;
        public void Set(Vector2 p0,Vector2 p1,Vector2 p2,Vector2 p3,float width,float drawn,float endDot)
        {
            if(p0==a&&p1==b&&p2==c&&p3==d&&Mathf.Approximately(width,thickness)&&Mathf.Approximately(drawn,progress)&&Mathf.Approximately(endDot,dot))return;
            a=p0;b=p1;c=p2;d=p3;thickness=width;progress=drawn;dot=endDot;SetVerticesDirty();
        }
        Vector2 At(float t){float u=1-t;return u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d;}
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();if(progress<=.001f||thickness<=0)return;const int segments=32;var tint=color;
            Vector2 previous=At(0);int steps=Mathf.Max(1,Mathf.CeilToInt(segments*progress));
            for(int i=1;i<=steps;i++)
            {
                var next=At(Mathf.Min(progress,(float)i/segments));var direction=(next-previous);if(direction.sqrMagnitude<1e-6f)continue;
                var normal=new Vector2(-direction.y,direction.x).normalized*thickness*.5f;int index=vh.currentVertCount;
                vh.AddVert(previous-normal,tint,Vector4.zero);vh.AddVert(previous+normal,tint,Vector4.zero);vh.AddVert(next+normal,tint,Vector4.zero);vh.AddVert(next-normal,tint,Vector4.zero);
                vh.AddTriangle(index,index+1,index+2);vh.AddTriangle(index,index+2,index+3);previous=next;
            }
            if(progress>=.999f&&dot>0)Disc(vh,At(1),dot,tint);
            Disc(vh,At(0),thickness*.5f,tint);
        }
        static void Disc(VertexHelper vh,Vector2 center,float radius,Color tint)
        {
            int index=vh.currentVertCount;vh.AddVert(center,tint,Vector4.zero);
            for(int i=0;i<=12;i++){float angle=i/12f*Mathf.PI*2;vh.AddVert(center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius,tint,Vector4.zero);}
            for(int i=1;i<=12;i++)vh.AddTriangle(index,index+i,index+i+1);
        }
    }

    /// <summary>Procedural art for the interaction layer: a brush streak, a disc, a ring and a caret.</summary>
    public static class OriginalInteractionArt
    {
        static Sprite streak,disc,ring,caret;
        static float Noise(float x,int seed)
        {int i=Mathf.FloorToInt(x);float f=x-i;float Hash(int n){n=(n<<13)^n^seed*7919;return 1f-((n*(n*n*15731+789221)+1376312589)&0x7fffffff)/1073741824f;}float u=f*f*(3-2*f);return Mathf.Lerp(Hash(i),Hash(i+1),u);}
        static Sprite Make(string name,int w,int h,Func<float,float,float> alpha)
        {
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name="SPARC interaction / "+name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};var pixels=new Color[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)pixels[y*w+x]=new Color(1,1,1,Mathf.Clamp01(alpha(x,y)));
            texture.SetPixels(pixels);texture.Apply(false,true);var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),100);sprite.name="SPARC interaction / "+name;return sprite;
        }
        /// <summary>A dry-brush stroke: solid behind the glyph, ragged top and bottom edges, a
        /// few bristle lines, fading out to the right.</summary>
        public static Sprite Streak=>streak?streak:streak=Make("streak",320,48,(x,y)=>
        {
            float u=x/319f,v=y/47f;
            float topEdge=.16f+.09f*Noise(x*.045f,3)+.03f*Noise(x*.3f,5),bottomEdge=.84f-.09f*Noise(x*.04f,11)-.03f*Noise(x*.28f,13);
            float body=Mathf.SmoothStep(0,1,(v-topEdge)/.05f)*Mathf.SmoothStep(0,1,(bottomEdge-v)/.05f);
            float fade=(1-Mathf.SmoothStep(.18f,1,u))*Mathf.SmoothStep(0,1,u/.03f);
            float bristles=1-.28f*Mathf.Clamp01(Noise(v*38,17)-.35f)*Mathf.SmoothStep(.3f,.9f,u);
            return body*fade*bristles*.96f;
        });
        public static Sprite Disc=>disc?disc:disc=Make("disc",64,64,(x,y)=>31.5f-Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f))+.5f);
        public static Sprite Ring=>ring?ring:ring=Make("ring",64,64,(x,y)=>{float r=Vector2.Distance(new Vector2(x,y),new Vector2(31.5f,31.5f));return Mathf.Min(31.5f-r+.5f,r-27.5f+.5f);});
        /// <summary>A small left-pointing caret, supersampled.</summary>
        public static Sprite Caret=>caret?caret:caret=Make("caret",32,32,(x,y)=>
        {
            int inside=0;for(int sy=0;sy<4;sy++)for(int sx=0;sx<4;sx++){float px=x+(sx+.5f)/4,py=y+(sy+.5f)/4;if(px>=6&&px<=27&&Mathf.Abs(py-16)<=(px-6)*.62f)inside++;}
            return inside/16f;
        });
    }
}
