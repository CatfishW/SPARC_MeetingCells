using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>One cohesive objective card and a responsive toolbar using the native controls.</summary>
    [DefaultExecutionOrder(2200)]
    public sealed class OriginalHudLayout:MonoBehaviour
    {
        public OriginalRuntime runtime;
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int SPARC_BrowserTouch();
#endif
        public static bool HasTouch
        {
            get{
#if UNITY_WEBGL && !UNITY_EDITOR
                return SPARC_BrowserTouch()!=0;
#else
                return Application.isMobilePlatform||Touchscreen.current!=null;
#endif
            }
        }
        int width,height;float next;
        Canvas objectives,playerCanvas;RectTransform tasks,panel,toolbar,card;TMP_Text progress;
        Image accent,rule,badge,taskIcon;int lastObjective,lastCompleted=-1;float attentionAt=-10;float ruleWidth;
        static readonly Color Ink=new Color(.14f,.21f,.23f);
        public RectTransform ObjectiveCard=>card;
        void Start(){Repair();}
        void LateUpdate()
        {
            RefreshEffects();
            if(!runtime||runtime.ShuttingDown||Time.unscaledTime<next)return;next=Time.unscaledTime+.25f;
            // Timeline retains activation and CanvasGroup fades. Its teaching
            // sequences also create task rows, which still need responsive geometry.
            // Before Begin only the toolbar needs the late CanvasScaler repair.
            if(!runtime.Started){LayoutToolbar(card?card.rect.width:0,card?card.rect.height:0);return;}
            if(width!=Screen.width||height!=Screen.height)Repair();else RefreshPresentation();
        }
        static void Rect(RectTransform r,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
        {if(!r)return;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=position;r.sizeDelta=size;}
        static void Inset(RectTransform r,float left,float right,float vertical)
        {if(!r)return;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=new Vector2(left,vertical);r.offsetMax=new Vector2(-right,-vertical);}
        static float Opacity(Transform row,Transform stop)
        {float alpha=1;for(var p=row;p&&p!=stop;p=p.parent){var group=p.GetComponent<CanvasGroup>();if(group)alpha*=group.alpha;}return alpha;}
        static float Scale(Canvas canvas)=>canvas?Mathf.Max(.1f,canvas.scaleFactor):1;
        static float Density=>OriginalMobileControls.PixelScale; // CSS-size targets on high-DPI browsers.
        static void CanvasLayout(Canvas canvas)
        {
            if(!canvas)return;canvas.renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=canvas.GetComponent<CanvasScaler>();if(scaler){scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;}
            canvas.transform.localRotation=Quaternion.identity;OriginalComponents.MatchOverlayScale(canvas);
        }
        public void Repair()
        {
            width=Screen.width;height=Screen.height;if(!runtime)return;
            var source=runtime.First("MeetingCellsObjectiveHudTopLeftLock");
            if(source)
            {
                objectives=source.GetComponent<Canvas>();CanvasLayout(objectives);if(objectives){objectives.overrideSorting=true;objectives.sortingOrder=90;}
                var rects=source.GetComponentsInChildren<RectTransform>(true);tasks=rects.FirstOrDefault(t=>t.name=="TasksTitle");panel=rects.FirstOrDefault(t=>t.name=="ObjectiveRect");
                if(tasks&&tasks.GetComponent<Image>())tasks.GetComponent<Image>().enabled=false;
                card=rects.FirstOrDefault(t=>t.name=="__SPARC_UI_ObjectiveCard");
                if(!card)
                {
                    var obj=new GameObject("__SPARC_UI_ObjectiveCard",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));obj.layer=source.gameObject.layer;obj.transform.SetParent(source.transform,false);obj.transform.SetAsFirstSibling();card=obj.GetComponent<RectTransform>();
                    var image=obj.GetComponent<Image>();image.raycastTarget=false;image.sprite=OriginalGameArt.Frame("paper");image.type=Image.Type.Sliced;image.color=Color.white;
                }
                if(!progress)
                {
                    var obj=new GameObject("__SPARC_UI_ObjectiveProgress",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));obj.layer=source.gameObject.layer;obj.transform.SetParent(source.transform,false);progress=obj.GetComponent<TMP_Text>();progress.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");progress.raycastTarget=false;
                }
                if(panel)
                {
                    // The source prefab's nested fitters and minimum sizes reserve
                    // empty rows. Keep those components and callbacks, but give the
                    // presentation one explicit content-size owner.
                    DisableDrivers(panel);
                    accent=OriginalGameArt.Image("__SPARC_UI_ObjectiveAccent",card,null,OriginalUiTheme.Teal);
                    rule=OriginalGameArt.Image("__SPARC_UI_ObjectiveRule",card,null,new Color(.2f,.4f,.42f,.18f));
                    badge=OriginalGameArt.Image("__SPARC_UI_ObjectiveBadge",card,OriginalGameArt.Frame("secondary"),Color.white);
                    taskIcon=OriginalGameArt.Image("__SPARC_UI_ObjectiveMark",tasks,OriginalUiIcons.Get("task"),OriginalUiTheme.Teal);
                }
            }
            playerCanvas=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).FirstOrDefault(c=>c.name=="PlayerCanvas");
            if(playerCanvas){CanvasLayout(playerCanvas);playerCanvas.sortingOrder=85;toolbar=playerCanvas.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name=="TOPRightBtns");}
            var control=runtime.First("ControlManager");if(control)foreach(var key in new[]{"joystickTransform","touchTransform","jumpButtonTransform"}){var item=control.Object(key);if(item)item.SetActive(HasTouch);}
            Canvas.ForceUpdateCanvases();RefreshPresentation();
        }
        static void Text(TMP_Text text,float size,bool bold=false)
        {if(!text)return;text.enableAutoSizing=false;text.fontSize=size;text.color=Ink;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;text.margin=Vector4.zero;text.textWrappingMode=TextWrappingModes.Normal;text.alignment=TextAlignmentOptions.MidlineLeft;}
        static void DisableDrivers(RectTransform section)
        {if(!section)return;var flow=section.GetComponent<LayoutGroup>();if(flow)flow.enabled=false;var fit=section.GetComponent<ContentSizeFitter>();if(fit)fit.enabled=false;}
        static void Section(RectTransform section,float top,float width,float height)
        {if(!section)return;DisableDrivers(section);Rect(section,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-top),new Vector2(width,height));var image=section.GetComponent<Image>();if(image)image.enabled=false;}
        void RefreshEffects()
        {
            // Only decorative ink animates: native buttons and text hit targets
            // remain still, and the source row CanvasGroup keeps its own fades.
            float pulse=Mathf.Clamp01(1-(Time.unscaledTime-attentionAt)/.8f);
            if(accent)accent.color=Color.Lerp(OriginalUiTheme.Teal,new Color(.45f,.77f,.65f),pulse);
            if(badge)badge.color=Color.Lerp(Color.white,new Color(.70f,.94f,.82f),pulse);
            if(rule){var size=rule.rectTransform.sizeDelta;size.x=ruleWidth*Mathf.SmoothStep(.18f,1,Mathf.Clamp01((Time.unscaledTime-attentionAt)/.38f));rule.rectTransform.sizeDelta=size;}
        }
        public void RefreshPresentation()
        {
            if(!runtime)return;float cardHeight=0,cardWidth=0;
            if(objectives&&tasks&&panel&&card)
            {
                float scale=Scale(objectives),unit=Density/scale,inset=Mathf.Max(24,16*unit);cardWidth=Mathf.Min(330*unit,((RectTransform)objectives.transform).rect.width-inset*2);
                float target=44*unit,header=48*unit;
                Rect(tasks,new Vector2(0,1),new Vector2(0,1),new Vector2(inset,-inset),new Vector2(cardWidth,header));
                foreach(var title in tasks.GetComponentsInChildren<TMP_Text>(true)){Text(title,11*unit,true);title.text="TASKS";title.characterSpacing=1;Inset(title.rectTransform,40*unit,target+124*unit,6*unit);}
                if(taskIcon)Rect(taskIcon.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(16*unit,-16*unit),new Vector2(18*unit,18*unit));
                var rows=runtime.OfType("ObjectiveToast").Where(t=>t.transform.parent==panel).OrderBy(t=>t.transform.GetSiblingIndex()).ToArray();
                float rowsHeight=0;int visible=0,first=0;
                for(int i=0;i<rows.Length;i++)
                {
                    var toast=rows[i];var rect=toast.transform as RectTransform;if(!rect)continue;rect.localScale=Vector3.one;
                    var title=toast.Component<TMP_Text>("TitleTextContent");var description=toast.Component<TMP_Text>("DescriptionTextContent");var counter=toast.Component<TMP_Text>("CounterTextContent");var subtitle=toast.Component<RectTransform>("SubTitleRect");
                    var button=toast.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name=="ObjectiveButton");var state=OriginalComponents.Ensure<OriginalObjectiveCard>(toast.gameObject);state.Bind(this,button);
                    bool hasDescription=description&&subtitle&&subtitle.gameObject.activeSelf;bool expanded=state.DetailsExpanded;bool counterVisible=counter&&!string.IsNullOrEmpty(counter.text);
                    Text(title,14*unit);if(title)title.color=OriginalUiTheme.Muted;Text(description,23*unit,true);Text(counter,12*unit);
                    if(title)title.enabled=!hasDescription||expanded;
                    float titleHeight=title&&title.enabled?Mathf.Max(22*unit,Mathf.Ceil(title.GetPreferredValues(title.text,cardWidth-40*unit,Mathf.Infinity).y)+4*unit):0;
                    if(i>0&&button&&expanded)titleHeight=Mathf.Max(titleHeight,target);
                    float right=(!expanded&&button&&i>0?target+24*unit:20*unit)+(counterVisible?52*unit:0);
                    float descriptionHeight=hasDescription?Mathf.Max(34*unit,Mathf.Ceil(description.GetPreferredValues(description.text,Mathf.Max(1,cardWidth-20*unit-right),Mathf.Infinity).y)+8*unit):0;
                    float rowHeight=titleHeight+descriptionHeight+12*unit;
                    DisableDrivers(rect);Rect(rect,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-rowsHeight),new Vector2(cardWidth,rowHeight));
                    Section(title?title.transform.parent as RectTransform:null,0,cardWidth,titleHeight);Section(subtitle,titleHeight,cardWidth,descriptionHeight);
                    if(title)Inset(title.rectTransform,20*unit,20*unit,2*unit);if(description)Inset(description.rectTransform,20*unit,right,4*unit);if(counter){counter.enabled=counterVisible;Inset(counter.rectTransform,cardWidth-64*unit,12*unit,4*unit);counter.alignment=TextAlignmentOptions.MidlineRight;}
                    if(button)
                    {
                        // Stay in the authored hierarchy; use the first row's native
                        // details button in the common paper card's header.
                        float y=i==0?header-2*unit:0;Rect(button.transform as RectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-4*unit,y),new Vector2(target,target));
                    }
                    // A completed row fades its own CanvasGroup out but stays active; it must not
                    // keep reserving an empty block above the current task.
                    if(toast.gameObject.activeSelf&&Opacity(rect,panel)>.05f){rowsHeight+=rowHeight+(visible++>0?4*unit:0);if(first==0)first=toast.GetInstanceID();}
                }
                Rect(panel,new Vector2(0,1),new Vector2(0,1),new Vector2(inset,-inset-header),new Vector2(cardWidth,Mathf.Max(1,rowsHeight)));
                int completed=runtime.objectives?runtime.objectives.CompletedCount:0;
                cardHeight=header+rowsHeight+4*unit;
                Rect(card,new Vector2(0,1),new Vector2(0,1),new Vector2(inset,-inset),new Vector2(cardWidth,cardHeight));
                if(accent)Rect(accent.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(0,-12*unit),new Vector2(3*unit,cardHeight-24*unit));
                ruleWidth=cardWidth-40*unit;if(rule)Rect(rule.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(20*unit,-header+5*unit),new Vector2(ruleWidth,unit));
                if(badge){badge.enabled=completed>0;Rect(badge.rectTransform,new Vector2(1,1),new Vector2(1,1),new Vector2(-target-10*unit,-12*unit),new Vector2(106*unit,24*unit));}
                if(progress)
                {
                    progress.enabled=completed>0;progress.text=completed+" completed";
                    Text(progress,11*unit,true);progress.alignment=TextAlignmentOptions.Center;Rect(progress.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(inset+cardWidth-target-116*unit,-inset-12*unit),new Vector2(106*unit,24*unit));
                }
                if(lastObjective!=first||lastCompleted!=completed){attentionAt=Time.unscaledTime;lastObjective=first;lastCompleted=completed;}RefreshEffects();
            }
            LayoutToolbar(cardWidth,cardHeight);
        }
        void LayoutToolbar(float objectiveWidth,float objectiveHeight)
        {
            if(!toolbar||!playerCanvas)return;float scale=Scale(playerCanvas),inset=Mathf.Max(24,16*Density/scale),size=Mathf.Max(52,44*Density/scale),gap=Mathf.Max(12,8*Density/scale);
            // The source single-row driver would otherwise overwrite our wrapped
            // positions during the next Canvas rebuild. Retain its component/IDs.
            var sourceFlow=toolbar.GetComponent<HorizontalOrVerticalLayoutGroup>();if(sourceFlow)sourceFlow.enabled=false;
            var buttons=toolbar.GetComponentsInChildren<Button>(true).Where(b=>b.transform.parent==toolbar).ToArray();int columns=Mathf.Max(1,buttons.Length);float canvasWidth=((RectTransform)playerCanvas.transform).rect.width;
            if(objectiveWidth+inset*2+columns*size+(columns-1)*gap+24>canvasWidth)columns=Mathf.Min(2,columns);
            float groupWidth=columns*size+(columns-1)*gap;int rows=Mathf.CeilToInt((float)buttons.Length/columns);float y=-inset;
            if(objectiveWidth+inset*2+groupWidth+24>canvasWidth)y-=objectiveHeight+12;
            Rect(toolbar,new Vector2(1,1),new Vector2(1,1),new Vector2(-inset,y),new Vector2(groupWidth,rows*size+(rows-1)*gap));
            for(int i=0;i<buttons.Length;i++)Rect(buttons[i].transform as RectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2((i%columns)*(size+gap),-(i/columns)*(size+gap)),new Vector2(size,size));
        }
    }

    /// <summary>Details never hide the actionable instruction or alter the source objective.</summary>
    [DefaultExecutionOrder(2210)]
    public sealed class OriginalObjectiveCard:MonoBehaviour
    {
        OriginalHudLayout hud;Button button;
        public bool DetailsExpanded {get;private set;}=true;
        public void Bind(OriginalHudLayout owner,Button nativeButton)
        {
            hud=owner;if(button==nativeButton)return;if(button)button.onClick.RemoveListener(ToggleDetails);button=nativeButton;if(button)button.onClick.AddListener(ToggleDetails);
        }
        public void ToggleDetails(){DetailsExpanded=!DetailsExpanded;if(hud)hud.RefreshPresentation();RefreshAffordance();}
        void LateUpdate(){RefreshAffordance();}
        void RefreshAffordance()
        {
            if(!button)return;
            // The source prefab spins this button's frame; at HUD size the turning square read
            // as a jittering icon. Hold the frame still after the Animator has written it.
            foreach(var part in button.GetComponentsInChildren<RectTransform>(true))if(part.name!="__SPARC_UI_Icon")part.localRotation=Quaternion.identity;
            var icon=button.transform.Find("__SPARC_UI_Icon");if(icon&&icon.GetComponent<Image>()){var graphic=icon.GetComponent<Image>();graphic.sprite=OriginalUiIcons.Get("chevron");graphic.rectTransform.localRotation=Quaternion.Euler(0,0,DetailsExpanded?180:0);}
            var hint=button.GetComponent<OriginalUiHint>();if(hint)hint.label=DetailsExpanded?"Hide task details":"Show task details";
        }
        void OnDestroy(){if(button)button.onClick.RemoveListener(ToggleDetails);}
    }
}
