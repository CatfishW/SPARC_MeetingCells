using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>New implementation of the source scene's runtime-created UI bridge.
    /// Repairs the actual original buttons and canvases; never substitutes a story transition.</summary>
    public sealed class OriginalActivityUi:MonoBehaviour
    {
        public OriginalRuntime runtime;
        Button[] launchers=Array.Empty<Button>();Canvas[] canvases=Array.Empty<Canvas>();
        readonly HashSet<int> wired=new();readonly HashSet<int> startedLaunchers=new();
        readonly Dictionary<Graphic,bool> phoneDecorations=new();
        readonly Dictionary<int,Viewport> appliedViewports=new();
        readonly Dictionary<int,string> controlHints=new();
        public bool IsTaskPanelOpen=>canvases.Any(c=>c&&c.isActiveAndEnabled&&(c.name is "QuestionCanvas" or "PlayerInputField" or "LAMInputField" or "AnswerField" or "LAMAnswerField" or "WaitForResponse"));
        float next,pixelScale;int width,height;
        static RectTransform Child(Transform parent,string name)=>parent?parent.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name==name):null;
        static void Stretch(RectTransform r,Vector2 min,Vector2 max,Vector2 low=default,Vector2 high=default)
        {if(!r)return;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.anchorMin=min;r.anchorMax=max;r.pivot=new Vector2(0.5f,0.5f);r.offsetMin=low;r.offsetMax=high;var p=r.localPosition;p.z=0;r.localPosition=p;}
        static void Fixed(RectTransform r,Vector2 anchor,Vector2 pivot,Vector2 size,Vector2 position=default)
        {if(!r)return;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=position;}
        public void Initialize()
        {
            launchers=FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(b=>b.gameObject.scene==runtime.gameObject.scene&&(b.name is "PlayGameRBC" or "PlayGameWBC")).ToArray();
            canvases=FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None).Where(c=>c.gameObject.scene==runtime.gameObject.scene).ToArray();
            foreach(var button in launchers)if(wired.Add(button.GetInstanceID()))
            {var original=button;button.onClick.AddListener(()=>{startedLaunchers.Add(original.GetInstanceID());original.gameObject.SetActive(false);});}
        }
        void Start(){Initialize();}
        public int LauncherCount=>launchers.Length;
        static void CanvasReady(Canvas canvas,int order)
        {
            if(!canvas)return;canvas.enabled=true;canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=Mathf.Max(canvas.sortingOrder,order);
            // Root overlay canvas dimensions are driven by Unity. Zeroing its size here
            // produces a zero-width rectangle until the next WebGL canvas rebuild.
            canvas.transform.rotation=Quaternion.identity;
            OriginalComponents.MatchOverlayScale(canvas);
            var scale=OriginalComponents.Ensure<CanvasScaler>(canvas.gameObject);scale.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scale.referenceResolution=new Vector2(1440,900);scale.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scale.matchWidthOrHeight=0.5f;
            OriginalComponents.Ensure<GraphicRaycaster>(canvas.gameObject).enabled=true;
            var group=canvas.GetComponent<CanvasGroup>();if(group){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
        }
        public void HideLegacyDialogueGraphics(OriginalBehaviour dialog)
        {
            // The original PlayGameRBC is a child of the original SayDialog prefab.
            // Hiding the entire prefab would repeatedly disable that authored launch button.
            // Hide only the old dialogue graphics, retaining its canvas and native controls.
            foreach(var graphic in dialog.GetComponentsInChildren<Graphic>(true))
            {
                bool launcher=false;for(var p=graphic.transform;p&&p!=dialog.transform;p=p.parent)if(p.name is "PlayGameRBC" or "PlayGameWBC"){launcher=true;break;}
                if(!launcher)graphic.enabled=false;
            }
        }
        /// <summary>The launcher's own overlay, in CSS pixels. The authored button sits beneath
        /// animated dialogue/world HUD parents whose transforms moved it off-screen on short
        /// desktop viewports; one stable owner keeps it bottom-centre at every size.</summary>
        Canvas launcherCanvas;
        public const float LauncherHeight=56,LauncherBottom=28;
        public bool LauncherVisible=>launchers.Any(b=>b&&b.gameObject.activeInHierarchy&&!startedLaunchers.Contains(b.GetInstanceID())&&b.interactable&&(b.GetComponent<CanvasGroup>()?.alpha??1)>.5f);
        Canvas LauncherCanvas()
        {
            if(launcherCanvas)return launcherCanvas;
            var obj=new GameObject("__SPARC_UI_LauncherCanvas",typeof(RectTransform),typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));
            obj.layer=LayerMask.NameToLayer("UI")<0?0:LayerMask.NameToLayer("UI");obj.transform.SetParent(runtime.transform,false);
            launcherCanvas=obj.GetComponent<Canvas>();launcherCanvas.renderMode=RenderMode.ScreenSpaceOverlay;launcherCanvas.overrideSorting=true;launcherCanvas.sortingOrder=150;
            obj.GetComponent<CanvasScaler>().uiScaleMode=CanvasScaler.ScaleMode.ConstantPixelSize;return launcherCanvas;
        }
        public void RepairLauncher(Button button)
        {
            if(!button||!button.gameObject.activeSelf||startedLaunchers.Contains(button.GetInstanceID()))return;
            var canvas=LauncherCanvas();canvas.GetComponent<CanvasScaler>().scaleFactor=OriginalMobileControls.PixelScale;
            if(button.transform.parent!=canvas.transform){button.transform.SetParent(canvas.transform,false);}
            var visibility=OriginalComponents.Ensure<CanvasGroup>(button.gameObject);bool ready=!runtime.presentation.IsSpeaking&&!runtime.activities.IsOpen;
            visibility.alpha=ready?1:0;visibility.interactable=visibility.blocksRaycasts=ready;
            OriginalComponents.Ensure<LayoutElement>(button.gameObject).ignoreLayout=true;
            var v=View(canvas);var area=((RectTransform)canvas.transform).rect;float bottom=(OriginalHudLayout.HasTouch?180:LauncherBottom)+Mathf.Max(0,area.height-v.top-v.height);
            Fixed(button.transform as RectTransform,new Vector2(.5f,0),new Vector2(.5f,0),new Vector2(Mathf.Min(340,v.width-32),LauncherHeight),new Vector2(v.left+v.width/2-area.width/2,bottom));
            button.enabled=true;button.interactable=true;var graphic=button.targetGraphic;if(graphic){graphic.raycastTarget=true;var c=graphic.color;c.a=1;graphic.color=c;}
            foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)){NativeText(text,19,true);text.alignment=TextAlignmentOptions.Center;text.margin=new Vector4(36,0,10,0);}
        }
        public void PrepareGame(OriginalBehaviour manager,bool reset)
        {
            var canvas=manager.GetComponentInParent<Canvas>(true);if(!canvas)return;CanvasReady(canvas,1200);
            var root=canvas.transform;var area=Child(root,"PlayArea");var panel=Child(root,"InstructionPanel");RestoreArena(area);RestoreInstructions(panel);bool portrait=Screen.height>Screen.width;
            // Some authored Exit buttons only call GameObject.SetActive. The original
            // confirmation script keeps its own GameObject active and hides a CanvasGroup,
            // so activating that object cannot reveal the panel. Retain its existing callback
            // and restore the original OnExitButton behavior after that callback.
            var confirmation=canvas.GetComponentsInChildren<OriginalBehaviour>(true).FirstOrDefault(c=>c.sourceType=="ExitConfirmation");
            if(confirmation)
                foreach(var exit in canvas.GetComponentsInChildren<Button>(true).Where(b=>b.name=="ExitButton"&&b.transform.parent.name=="InstructionPanel"))
                    if(wired.Add(exit.GetInstanceID())){var originalConfirmation=confirmation;exit.onClick.AddListener(()=>runtime.activities.Call(originalConfirmation,"OnExitButton",null));}

            foreach(var name in new[]{"Background","VictoryScreen","LoadingScreen","ExitConfirmationBox"})Stretch(Child(root,name),Vector2.zero,Vector2.one);
            Stretch(area,portrait?new Vector2(0.04f,0.29f):new Vector2(0.055f,0.09f),portrait?new Vector2(0.96f,0.88f):new Vector2(0.725f,0.925f));
            Stretch(panel,portrait?new Vector2(0.05f,0.04f):new Vector2(0.755f,0.09f),portrait?new Vector2(0.95f,0.25f):new Vector2(0.965f,0.925f));
            if(area)OriginalComponents.Ensure<RectMask2D>(area.gameObject);
            bool white=manager.sourceType=="WhiteBloodCellGameManager";var player=manager.Object("player");
            var playerRect=player?player.transform as RectTransform:null;
            if(reset)Fixed(playerRect,white?new Vector2(0.5f,0.5f):new Vector2(0.5f,0.15f),new Vector2(0.5f,0.5f),white?new Vector2(132,132):new Vector2(205,90));
            Fixed(Child(playerRect,"PlayerCollectionHitbox"),new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),new Vector2(205,94));
            Fixed(Child(area,"PlayerSpawnPoint"),new Vector2(0.5f,0.15f),new Vector2(0.5f,0.5f),new Vector2(32,32));
            Stretch(Child(area,"SpawnZone"),new Vector2(0.08f,0.84f),new Vector2(0.92f,0.98f));
            Stretch(Child(area,"KillPlane"),Vector2.zero,new Vector2(1,0),Vector2.zero,new Vector2(0,70));
            if(area)
            {
                int deposit=0;
                foreach(var r in area.GetComponentsInChildren<RectTransform>(true).Where(r=>r.parent==area))
                {
                    if(r.name=="OxygenDeposit"){bool left=deposit++%2==0;Fixed(r,new Vector2(left?0:1,0),new Vector2(left?0:1,0),new Vector2(208,208),new Vector2(left?58:-58,40));}
                    if(r.name=="OuterWall")
                    {bool vertical=Mathf.Approximately(r.anchorMin.x,r.anchorMax.x);bool upper=vertical?r.anchorMin.x>0.5f||r.anchoredPosition.x>0:r.anchorMin.y>0.5f||r.anchoredPosition.y>0;
                    if(vertical)Stretch(r,new Vector2(upper?1:0,0),new Vector2(upper?1:0,1),new Vector2(upper?-52:0,0),new Vector2(upper?0:52,0));
                    else Stretch(r,new Vector2(0,upper?1:0),new Vector2(1,upper?1:0),new Vector2(0,upper?-52:0),new Vector2(0,upper?0:52));}
                }
            }
            var joystick=Child(root,"Joystick");if(joystick){StickWrapper(joystick);RestoreStick(joystick);joystick.gameObject.SetActive(OriginalHudLayout.HasTouch);}
            string[] labels=white?new[]{"TimeText","ScoreText","LevelText","RemText","Instruction0","Instruction1","Instruction2","Instruction3","ExitButton"}:new[]{"TimeText","ScoreText","Instruction0","Instruction1","Instruction2","Instruction3","ExitButton"};
            float[] lows=white?new[]{0.895f,0.805f,0.725f,0.645f,0.53f,0.395f,0.26f,0.13f,0.025f}:new[]{0.84f,0.73f,0.58f,0.43f,0.28f,0.15f,0.04f};
            float[] highs=white?new[]{0.975f,0.885f,0.795f,0.715f,0.63f,0.52f,0.385f,0.25f,0.11f}:new[]{0.94f,0.83f,0.72f,0.57f,0.42f,0.27f,0.13f};
            for(int i=0;i<labels.Length;i++)Stretch(Child(panel,labels[i]),new Vector2(0.08f,lows[i]),new Vector2(0.92f,highs[i]));
            if(panel)
            {
                var paper=panel.GetComponent<Image>();if(paper)paper.color=new Color(0.965f,0.95f,0.90f,0.985f);
                foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true)){text.enableAutoSizing=true;text.fontSizeMin=16;text.fontSizeMax=28;text.margin=Vector4.zero;text.textWrappingMode=TextWrappingModes.Normal;text.color=new Color(0.12f,0.20f,0.22f);}
            }
            Canvas.ForceUpdateCanvases();var applied=View(canvas);CompactGame(manager,canvas);LayoutGameStick(canvas);if(area)LayoutRebuilder.ForceRebuildLayoutImmediate(area);OriginalMiniGame.MatchColliders(canvas.gameObject);appliedViewports[canvas.GetInstanceID()]=applied;
            OriginalComponents.Ensure<OriginalMiniPresentation>(canvas.gameObject).Prepare(manager);
        }
        void Question(Canvas canvas)
        {
            CanvasReady(canvas,1300);
            // The authored questionnaire is nested under a rotated/scaled Timeline object.
            // Overlay UI must cancel that world transform rather than compress its choices.
            canvas.transform.rotation=Quaternion.identity;
            OriginalComponents.MatchOverlayScale(canvas);
            var content=Child(canvas.transform,"Content");
            if(content)
            {
                Fixed(content,new Vector2(0.5f,0.5f),new Vector2(0.5f,0.5f),new Vector2(1060,500));
                Stretch(content.Find("BGImage") as RectTransform,Vector2.zero,Vector2.one);
                var question=content.Find("Question") as RectTransform;Stretch(question,new Vector2(0.06f,0.72f),new Vector2(0.94f,0.95f));
                var title=question?question.GetComponent<TMP_Text>():null;if(title){title.enableAutoSizing=true;title.fontSizeMin=22;title.fontSizeMax=36;title.alignment=TextAlignmentOptions.Center;title.margin=Vector4.zero;}
                for(int i=1;i<=2;i++)
                {
                    var option=content.Find("Option_"+i) as RectTransform;if(!option)continue;
                    Stretch(option,new Vector2(i==1?0.06f:0.54f,0.10f),new Vector2(i==1?0.46f:0.94f,0.66f));
                    var button=option.GetComponentInChildren<Button>(true);if(button)Fixed((RectTransform)button.transform,new Vector2(0.5f,0.71f),new Vector2(0.5f,0.5f),new Vector2(170,64));
                    var description=option.Find("Option_1_Content") as RectTransform;Stretch(description,new Vector2(0,0.02f),new Vector2(1,0.48f));
                    var text=description?description.GetComponent<TMP_Text>():null;if(text){text.enableAutoSizing=true;text.fontSizeMin=21;text.fontSizeMax=30;text.alignment=TextAlignmentOptions.Center;text.margin=Vector4.zero;}
                }
                var separator=content.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="GWL");if(separator)Fixed(separator,new Vector2(0.5f,0.69f),new Vector2(0.5f,0.5f),new Vector2(930,2));
            }

            RestoreDecorations(canvas);CompactQuestion(canvas);
            foreach(var group in canvas.GetComponentsInChildren<CanvasGroup>(true)){group.alpha=1;group.interactable=true;group.blocksRaycasts=true;}
            foreach(var button in canvas.GetComponentsInChildren<Button>(true))button.interactable=true;
            foreach(var field in canvas.GetComponentsInChildren<TMP_InputField>(true)){field.interactable=true;field.shouldHideMobileInput=false;field.shouldHideSoftKeyboard=false;}
            Canvas.ForceUpdateCanvases();
        }
        void PeerPanel(Canvas canvas)
        {
            CanvasReady(canvas,1400);canvas.transform.rotation=Quaternion.identity;
            OriginalComponents.MatchOverlayScale(canvas);
            if(canvas.name is "PlayerInputField" or "LAMInputField")
            {
                var field=canvas.GetComponentInChildren<TMP_InputField>(true);if(!field)return;
                var fitter=field.GetComponent<ContentSizeFitter>();if(fitter)fitter.enabled=false;
                Fixed((RectTransform)field.transform,new Vector2(0.5f,0.18f),new Vector2(0.5f,0.5f),new Vector2(920,185));
                field.lineType=TMP_InputField.LineType.MultiLineSubmit;field.characterLimit=2000;field.interactable=true;
                Stretch(field.textViewport,Vector2.zero,Vector2.one,new Vector2(22,22),new Vector2(-22,-22));
                foreach(var text in field.GetComponentsInChildren<TMP_Text>(true)){text.enableAutoSizing=false;text.fontSize=26;text.margin=Vector4.zero;}
            }
            else if(canvas.name is "AnswerField" or "LAMAnswerField")
            {
                var panel=canvas.transform.Find("GameObject") as RectTransform;if(!panel)return;
                Fixed(panel,new Vector2(0.5f,0.24f),new Vector2(0.5f,0.5f),new Vector2(980,270));
                Stretch(panel.Find("BG") as RectTransform,Vector2.zero,Vector2.one);
                Stretch(panel.Find("AF") as RectTransform,new Vector2(0.04f,0.29f),new Vector2(0.96f,0.80f));
                Stretch(panel.Find("Name") as RectTransform,new Vector2(0.04f,0.80f),new Vector2(0.90f,0.98f));
                foreach(var button in panel.GetComponentsInChildren<Button>(true))Fixed((RectTransform)button.transform,new Vector2(0.82f,0.15f),new Vector2(0.5f,0.5f),new Vector2(245,52));
                foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true)){text.enableAutoSizing=true;text.fontSizeMin=20;text.fontSizeMax=28;text.margin=Vector4.zero;}
                var line=panel.Find("GradientWhiteline") as RectTransform;if(line)Fixed(line,new Vector2(0.5f,0.78f),new Vector2(0.5f,0.5f),new Vector2(910,2));
            }
            else if(canvas.name=="WaitForResponse")
            {
                var panel=canvas.transform.Find("P") as RectTransform;if(panel)Fixed(panel,new Vector2(0.5f,0.25f),new Vector2(0.5f,0.5f),new Vector2(520,90));
            }
            CompactPeer(canvas);
        }
        struct Viewport
        {
            public float width,height,unit,left,top;public bool touch;
            public bool Matches(Viewport other)=>Mathf.Approximately(width,other.width)&&Mathf.Approximately(height,other.height)&&Mathf.Approximately(unit,other.unit)&&Mathf.Approximately(left,other.left)&&Mathf.Approximately(top,other.top)&&touch==other.touch;
        }
        Viewport View(Canvas canvas)
        {
            float density=OriginalMobileControls.PixelScale;var pixels=canvas.pixelRect;var safe=pixels;
            var mobile=runtime?runtime.GetComponent<OriginalMobileControls>():null;
            if(canvas.renderMode==RenderMode.ScreenSpaceOverlay&&OriginalHudLayout.HasTouch&&mobile&&mobile.SafeScreenRect.width>0)
            {var candidate=mobile.SafeScreenRect;if(candidate.xMin>=pixels.xMin&&candidate.yMin>=pixels.yMin&&candidate.xMax<=pixels.xMax+1&&candidate.yMax<=pixels.yMax+1)safe=candidate;}
            return new Viewport{width=safe.width/density,height=safe.height/density,left=(safe.xMin-pixels.xMin)/density,top=(pixels.yMax-safe.yMax)/density,unit=density/Mathf.Max(.1f,canvas.scaleFactor),touch=OriginalHudLayout.HasTouch};
        }
        bool Compact(Canvas canvas){var v=View(canvas);return v.width<1100||v.height<650;}
        static void NativeText(TMP_Text text,float size,bool bold=false)
        {if(!text)return;text.rectTransform.localScale=Vector3.one;text.rectTransform.localRotation=Quaternion.identity;text.enableAutoSizing=false;text.enableVertexGradient=false;text.fontSize=size;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;text.margin=Vector4.zero;text.textWrappingMode=TextWrappingModes.Normal;if(!text.GetComponentInParent<TMP_InputField>(true)){text.firstVisibleCharacter=0;text.maxVisibleCharacters=text.maxVisibleWords=text.maxVisibleLines=int.MaxValue;}}
        static void Box(RectTransform r,float x,float y,float w,float h,float unit)
        {Fixed(r,new Vector2(0,1),new Vector2(0,1),new Vector2(w,h)*unit,new Vector2(x,-y)*unit);}
        sealed class StickLayout
        {
            public RectTransform wrapper;public Vector2 min,max,pivot,size,position;public Vector3 scale;public Quaternion rotation;
        }
        readonly Dictionary<int,StickLayout> stickLayouts=new();
        sealed class ArenaChild
        {
            public RectTransform rect,parent;public Vector2 min,max,pivot,size,position;public Vector3 scale;public Quaternion rotation;
            public ArenaChild(RectTransform r){rect=r;parent=r.parent as RectTransform;min=r.anchorMin;max=r.anchorMax;pivot=r.pivot;size=r.sizeDelta;position=r.anchoredPosition;scale=r.localScale;rotation=r.localRotation;}
            public void Place(Transform parent){rect.SetParent(parent,false);rect.anchorMin=min;rect.anchorMax=max;rect.pivot=pivot;rect.sizeDelta=size;rect.anchoredPosition=position;rect.localScale=scale;rect.localRotation=rotation;}
        }
        sealed class ArenaLayout {public RectTransform wrapper;public ArenaChild[] children;}
        readonly Dictionary<int,ArenaLayout> arenaLayouts=new();
        void RestoreArena(RectTransform area)
        {if(!area||!arenaLayouts.TryGetValue(area.GetInstanceID(),out var layout))return;foreach(var child in layout.children)child.Place(child.parent);layout.wrapper.gameObject.SetActive(false);}
        RectTransform ArenaWrapper(RectTransform area)
        {
            if(arenaLayouts.TryGetValue(area.GetInstanceID(),out var existing))
            {existing.wrapper.gameObject.SetActive(true);foreach(var child in existing.children)child.Place(existing.wrapper);return existing.wrapper;}
            var parent=area.parent;var children=parent.GetComponentsInChildren<RectTransform>(true).Where(r=>r==area||(r.parent==parent&&r.name=="OuterWall")).Select(r=>new ArenaChild(r)).ToArray();
            var obj=new GameObject("__SPARC_NATIVE_Arena",typeof(RectTransform));obj.layer=area.gameObject.layer;var wrapper=obj.GetComponent<RectTransform>();wrapper.SetParent(parent,false);wrapper.SetSiblingIndex(area.GetSiblingIndex());wrapper.sizeDelta=new Vector2(1440,900);
            var layout=new ArenaLayout{wrapper=wrapper,children=children};arenaLayouts[area.GetInstanceID()]=layout;foreach(var child in children)child.Place(wrapper);return wrapper;
        }
        RectTransform StickWrapper(RectTransform stick)
        {
            if(!stick)return null;int id=stick.GetInstanceID();if(stickLayouts.TryGetValue(id,out var state))return state.wrapper;
            var obj=new GameObject("__SPARC_NATIVE_GameJoystick",typeof(RectTransform));obj.layer=stick.gameObject.layer;var wrapper=obj.GetComponent<RectTransform>();wrapper.SetParent(stick.parent,false);wrapper.SetSiblingIndex(stick.GetSiblingIndex());
            state=new StickLayout{wrapper=wrapper,min=stick.anchorMin,max=stick.anchorMax,pivot=stick.pivot,size=stick.sizeDelta,position=stick.anchoredPosition,scale=stick.localScale,rotation=stick.localRotation};stickLayouts[id]=state;
            wrapper.anchorMin=state.min;wrapper.anchorMax=state.max;wrapper.pivot=state.pivot;wrapper.sizeDelta=state.size;wrapper.anchoredPosition=state.position;wrapper.localScale=state.scale;wrapper.localRotation=state.rotation;
            stick.SetParent(wrapper,false);Fixed(stick,new Vector2(.5f,.5f),new Vector2(.5f,.5f),state.size);
            return wrapper;
        }
        void RestoreStick(RectTransform stick)
        {
            if(!stick||!stickLayouts.TryGetValue(stick.GetInstanceID(),out var state))return;var r=state.wrapper;
            r.anchorMin=state.min;r.anchorMax=state.max;r.pivot=state.pivot;r.sizeDelta=state.size;r.anchoredPosition=state.position;r.localScale=state.scale;r.localRotation=state.rotation;
            Fixed(stick,new Vector2(.5f,.5f),new Vector2(.5f,.5f),state.size);
        }
        sealed class InstructionsLayout {public RectTransform viewport,content;public ScrollRect scroll;public bool positioned;}
        readonly Dictionary<int,InstructionsLayout> instructionLayouts=new();
        void RestoreInstructions(RectTransform panel)
        {
            if(!panel||!instructionLayouts.TryGetValue(panel.GetInstanceID(),out var layout))return;
            for(int i=0;i<4;i++){var text=Child(layout.content,"Instruction"+i);if(text)text.SetParent(panel,false);}
            layout.viewport.gameObject.SetActive(false);
        }
        InstructionsLayout InstructionViewport(RectTransform panel)
        {
            if(instructionLayouts.TryGetValue(panel.GetInstanceID(),out var existing)){existing.viewport.gameObject.SetActive(true);return existing;}
            var obj=new GameObject("__SPARC_UI_Instructions",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));obj.layer=panel.gameObject.layer;obj.transform.SetParent(panel,false);var image=obj.GetComponent<Image>();image.color=Color.clear;image.raycastTarget=true;
            var content=new GameObject("__SPARC_UI_InstructionContent",typeof(RectTransform));content.layer=panel.gameObject.layer;content.transform.SetParent(obj.transform,false);
            var layout=new InstructionsLayout{viewport=obj.GetComponent<RectTransform>(),content=content.GetComponent<RectTransform>(),scroll=obj.GetComponent<ScrollRect>()};layout.scroll.viewport=layout.viewport;layout.scroll.content=layout.content;layout.scroll.horizontal=false;layout.scroll.vertical=true;layout.scroll.inertia=false;layout.scroll.movementType=ScrollRect.MovementType.Clamped;
            instructionLayouts[panel.GetInstanceID()]=layout;return layout;
        }
        void CompactGame(OriginalBehaviour manager,Canvas canvas)
        {
            var v=View(canvas);var root=canvas.transform;var area=Child(root,"PlayArea");var panel=Child(root,"InstructionPanel");if(!area||!panel)return;
            bool white=manager.sourceType=="WhiteBloodCellGameManager",columns=v.width>=740&&v.width/v.height>=1.35f;
            // The same canonical desktop board, pivot, child positions, assets and
            // collider dimensions are retained. Only its parent scale/placement changes.
            float margin=16;
            var arena=ArenaWrapper(area);Fixed(arena,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(1440,900));Stretch(area,new Vector2(.055f,.09f),new Vector2(.725f,.925f));
            // Fit the actual source visual union, including sibling WBC borders.
            // This reserves a consistent inset without changing any wall geometry.
            Vector2 low=new Vector2(float.PositiveInfinity,float.PositiveInfinity),high=new Vector2(float.NegativeInfinity,float.NegativeInfinity);var corners=new Vector3[4];
            foreach(var r in arena.GetComponentsInChildren<RectTransform>(true).Where(r=>r==area||r.name=="OuterWall"))
            {r.GetWorldCorners(corners);foreach(var point in corners){Vector2 local=arena.InverseTransformPoint(point);low=Vector2.Min(low,local);high=Vector2.Max(high,local);}}
            var bounds=Rect.MinMaxRect(low.x,low.y,high.x,high.y);
            float stick=OriginalHudLayout.HasTouch?(Compact(canvas)?104:128):0;
            float rail=columns&&stick>0?stick+16:0,reserve=!columns&&stick>0?stick+32:0;
            float panelWidth=columns?Mathf.Clamp(v.width*.26f,272,330):Mathf.Min(760,v.width-margin*2);
            string[] metrics=white?new[]{"TimeText","ScoreText","LevelText","RemText"}:new[]{"TimeText","ScoreText"};
            float inner=panelWidth-24,statusRows=2,y=72+statusRows*30+8;var heights=new float[4];
            for(int i=0;i<4;i++)
            {
                var text=Child(panel,"Instruction"+i)?.GetComponent<TMP_Text>();
                if(text)
                {
                    int id=text.GetInstanceID();if(!controlHints.ContainsKey(id))controlHints[id]=text.text;
                    string original=controlHints[id];text.text=!OriginalHudLayout.HasTouch&&original.IndexOf("joystick",StringComparison.OrdinalIgnoreCase)>=0?"Move with WASD or the arrow keys.":original;
                }
                NativeText(text,16*v.unit);
                if(text)
                {
                    text.margin=new Vector4(44,0,0,0)*v.unit;
                    foreach(var art in text.GetComponentsInChildren<Image>(true)){Fixed(art.rectTransform,new Vector2(0,1),new Vector2(0,1),new Vector2(32,32)*v.unit,new Vector2(0,-2)*v.unit);art.preserveAspect=true;art.raycastTarget=false;}
                }
                heights[i]=text?Mathf.Max(40,Mathf.Ceil(text.GetPreferredValues(text.text,Mathf.Max(1,inner-44)*v.unit,Mathf.Infinity).y/v.unit)):0;
            }
            var skin=OriginalComponents.Ensure<OriginalMiniPresentation>(canvas.gameObject);bool expanded=skin.InstructionsExpanded;
            float required=expanded?y+heights.Sum()+8*3+12+56+40:y+112;
            float available=columns?v.height-margin*2:Mathf.Min(360,Mathf.Max(180,(v.height-reserve-margin*2)*.48f));
            // Short square windows may give less than half the height to Help.
            // Reserve one readable scroll row plus the native footer before fitting the board.
            if(!columns)available=Mathf.Max(available,Mathf.Min(expanded?y+44+108:y+112,Mathf.Max(180,v.height-reserve-100)));
            float panelHeight=Mathf.Min(available,required);
            float availableWidth=columns?v.width-margin*2-panelWidth-16-rail:v.width-margin*2;
            float availableHeight=columns?v.height-margin*2:v.height-margin*2-reserve-panelHeight-12;
            float cssScale=Mathf.Max(.01f,Mathf.Min(1.1f,availableWidth/bounds.width,availableHeight/bounds.height));
            float boardWidth=bounds.width*cssScale,boardHeight=bounds.height*cssScale;
            float boardLeft=columns?v.left+margin+rail+(availableWidth-boardWidth)/2:v.left+(v.width-boardWidth)/2;
            float boardTop=columns?v.top+(v.height-boardHeight)/2:v.top+margin+Mathf.Max(0,(v.height-reserve-margin*2-boardHeight-12-panelHeight)/2);
            float panelLeft=columns?v.left+v.width-margin-panelWidth:v.left+(v.width-panelWidth)/2;
            float panelTop=columns?v.top+Mathf.Clamp(boardTop-v.top,margin,v.height-margin-panelHeight):boardTop+boardHeight+12;
            float boardScale=cssScale*v.unit;var desiredCenter=new Vector2(boardLeft+boardWidth/2,-boardTop-boardHeight/2)*v.unit;
            arena.anchorMin=arena.anchorMax=new Vector2(0,1);arena.anchoredPosition=desiredCenter-(Vector2)bounds.center*boardScale;arena.localScale=Vector3.one*boardScale;
            Box(panel,panelLeft,panelTop,panelWidth,panelHeight,v.unit);
            for(int i=0;i<metrics.Length;i++)
            {var r=Child(panel,metrics[i]);Box(r,12+(i%2)*(inner+12)/2,72+(i/2)*30,(inner-12)/2,28,v.unit);if(r)NativeText(r.GetComponent<TMP_Text>(),16*v.unit,true);}
            if(!white)
            {
                var timerCaption=Child(panel,"TimeText");Box(timerCaption,12,70,(inner-12)/2,22,v.unit);if(timerCaption)NativeText(timerCaption.GetComponent<TMP_Text>(),14*v.unit);
                var timer=manager.Component<TMP_Text>("timeText");if(timer){Box(timer.rectTransform,12,94,(inner-12)/2,28,v.unit);NativeText(timer,18*v.unit,true);}
                var score=manager.Component<TMP_Text>("scoreText");if(score){Box(score.rectTransform,12+(inner+12)/2,72,(inner-12)/2,50,v.unit);NativeText(score,18*v.unit,true);}
            }
            var instructions=InstructionViewport(panel);float normalized=instructions.positioned?instructions.scroll.verticalNormalizedPosition:1;instructions.positioned=true;
            Box(instructions.viewport,12,y,inner,Mathf.Max(44,panelHeight-y-(expanded?108:80)),v.unit);
            Fixed(instructions.content,new Vector2(0,1),new Vector2(0,1),new Vector2(inner,heights.Sum()+24)*v.unit);
            float instructionY=0;
                for(int i=0;i<4;i++){var text=Child(panel,"Instruction"+i);if(text){text.SetParent(instructions.content,false);Box(text,0,instructionY,inner,heights[i],v.unit);var label=text.GetComponent<TMP_Text>();if(label)label.ForceMeshUpdate(true,true);}instructionY+=heights[i]+8;}
            instructions.scroll.verticalNormalizedPosition=float.IsNaN(normalized)?1:Mathf.Clamp01(normalized);instructions.viewport.gameObject.SetActive(expanded);
            var exit=Child(panel,"ExitButton");Box(exit,panelWidth-140,panelHeight-68,128,56,v.unit);if(exit)foreach(var text in exit.GetComponentsInChildren<TMP_Text>(true))NativeText(text,17*v.unit,true);
            var hint=panel.Find("__SPARC_UI_ScrollHint");
            if(!hint){var obj=new GameObject("__SPARC_UI_ScrollHint",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));obj.layer=panel.gameObject.layer;obj.transform.SetParent(panel,false);hint=obj.transform;var label=obj.GetComponent<TMP_Text>();label.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");label.raycastTarget=false;label.color=OriginalUiTheme.Ink;}
            var scrollHint=hint.GetComponent<TMP_Text>();NativeText(scrollHint,12*v.unit);scrollHint.text="Scroll for more";scrollHint.gameObject.SetActive(expanded&&instructions.content.rect.height>instructions.viewport.rect.height+1);Box(scrollHint.rectTransform,12,panelHeight-96,panelWidth-24,20,v.unit);scrollHint.alignment=TextAlignmentOptions.MidlineLeft;
            CompactGameOverlays(canvas,v);
            // The authored oxygen gravity uses the board's scale at Start. Keep that
            // same formula after an orientation change, without changing its rule.
            foreach(var token in area.GetComponentsInChildren<OriginalMiniToken>(true))if(token.Oxygen&&token.source)
            {var body=token.GetComponent<Rigidbody2D>();if(body)body.gravityScale=token.source.Number("gravityMultiplier",50)*Mathf.Max(.001f,area.lossyScale.x);}
            OriginalMiniGame.MatchColliders(canvas.gameObject);Physics2D.SyncTransforms();
        }
        void LayoutGameStick(Canvas canvas)
        {
            var joystick=Child(canvas.transform,"Joystick");if(!joystick)return;
            if(!OriginalHudLayout.HasTouch){RestoreStick(joystick);joystick.gameObject.SetActive(false);return;}
            var v=View(canvas);float size=Compact(canvas)?104:128;var wrapper=StickWrapper(joystick);
            // Keep the authored stick/device and its cached neutral point. Only its
            // wrapper moves, including on tablets where the board stays desktop-sized.
            Box(wrapper,v.left+16,v.top+v.height-16-size,size,size,v.unit);Fixed(joystick,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(size,size)*v.unit);joystick.gameObject.SetActive(true);
        }
        void CompactGameOverlays(Canvas canvas,Viewport v)
        {
            float w=Mathf.Min(600,v.width-32),h=Mathf.Min(330,v.height-40);var root=canvas.transform;
            var confirmation=Child(root,"ExitConfirmationBox");var box=confirmation?Child(confirmation,"Box"):null;
            if(box)
            {
                Fixed(box,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(w,h)*v.unit);
                foreach(var text in box.GetComponentsInChildren<TMP_Text>(true).Where(t=>!t.GetComponentInParent<Button>(true)))
                {Box(text.rectTransform,24,26,w-48,h-120,v.unit);NativeText(text,18*v.unit);}
                var buttons=box.GetComponentsInChildren<Button>(true);float bw=(w-60)/2;
                for(int i=0;i<buttons.Length;i++){Box(buttons[i].transform as RectTransform,24+i*(bw+12),h-72,bw,48,v.unit);foreach(var text in buttons[i].GetComponentsInChildren<TMP_Text>(true))NativeText(text,16*v.unit,true);}
            }
            var loading=Child(root,"LoadingScreen");
            if(loading)
            {
                var heading=Child(loading,"ThisTextSaysLoading");Fixed(heading,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(w,48)*v.unit,new Vector2(0,100)*v.unit);if(heading)NativeText(heading.GetComponent<TMP_Text>(),24*v.unit,true);
                Fixed(Child(loading,"LoadingBar"),new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(w-32,26)*v.unit,new Vector2(0,40)*v.unit);
                var hint=Child(loading,"LoadingHint");Fixed(hint,new Vector2(.5f,.5f),new Vector2(.5f,.5f),new Vector2(w-32,140)*v.unit,new Vector2(0,-70)*v.unit);if(hint)NativeText(hint.GetComponent<TMP_Text>(),17*v.unit);
            }
            var victory=Child(root,"VictoryScreen");
            if(victory)
            {
                var labels=victory.GetComponentsInChildren<TMP_Text>(true).Where(t=>!t.GetComponentInParent<Button>(true)).OrderBy(t=>t.name=="EndGameBlurb"?0:t.name=="FinalScore"?1:2).ToArray();float total=0;
                foreach(var text in labels){NativeText(text,18*v.unit);total+=Mathf.Max(30,text.GetPreferredValues(text.text,(w-32)*v.unit,Mathf.Infinity).y/v.unit)+12;}
                float y=(v.height-total-64)/2;
                foreach(var text in labels){float height=Mathf.Max(30,text.GetPreferredValues(text.text,(w-32)*v.unit,Mathf.Infinity).y/v.unit);Box(text.rectTransform,v.left+(v.width-w)/2+16,v.top+y,w-32,height,v.unit);y+=height+12;}
                foreach(var button in victory.GetComponentsInChildren<Button>(true)){Box(button.transform as RectTransform,v.left+(v.width-180)/2,v.top+y+4,180,48,v.unit);foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))NativeText(text,17*v.unit,true);}
            }
        }
        void RestoreDecorations(Canvas canvas)
        {foreach(var item in phoneDecorations.Where(p=>p.Key&&p.Key.GetComponentInParent<Canvas>(true)==canvas).ToArray()){item.Key.enabled=item.Value;phoneDecorations.Remove(item.Key);}}
        void CompactQuestion(Canvas canvas)
        {
            var v=View(canvas);var content=Child(canvas.transform,"Content");if(!content)return;bool portrait=v.height>v.width;float w=Mathf.Min(1000,v.width-32);
            var question=content.Find("Question") as RectTransform;var questionText=question?question.GetComponent<TMP_Text>():null;NativeText(questionText,20*v.unit,true);
            float heading=questionText?Mathf.Clamp(questionText.GetPreferredValues(questionText.text,(w-40)*v.unit,Mathf.Infinity).y/v.unit+12,56,96):56;
            float ow=portrait?w-40:(w-60)/2;var heights=new float[2];
            for(int i=1;i<=2;i++)
            {var option=content.Find("Option_"+i);var description=option?option.Find("Option_1_Content"):null;var text=description?description.GetComponent<TMP_Text>():null;NativeText(text,17*v.unit);heights[i-1]=64+(text?Mathf.Max(28,text.GetPreferredValues(text.text,(ow-16)*v.unit,Mathf.Infinity).y/v.unit):28);}
            float top=16+heading+12,h=Mathf.Min(v.height-32,top+(portrait?heights.Sum()+12:heights.Max())+16);
            Fixed(content,new Vector2(0,1),new Vector2(.5f,.5f),new Vector2(w,h)*v.unit,new Vector2(v.left+v.width/2,-v.top-v.height/2)*v.unit);
            Box(question,20,16,w-40,heading,v.unit);
            for(int i=1;i<=2;i++)
            {
                var option=content.Find("Option_"+i) as RectTransform;if(!option)continue;float oh=heights[i-1];Box(option,20+(portrait?0:(i-1)*(ow+20)),top+(portrait&&i==2?heights[0]+12:0),ow,oh,v.unit);
                var button=option.GetComponentInChildren<Button>(true);if(button){Box(button.transform as RectTransform,0,0,ow,44,v.unit);foreach(var text in button.GetComponentsInChildren<TMP_Text>(true))NativeText(text,18*v.unit,true);}
                var description=option.Find("Option_1_Content") as RectTransform;Box(description,8,54,ow-16,Mathf.Max(28,oh-58),v.unit);if(description&&description.GetComponent<TMP_Text>())description.GetComponent<TMP_Text>().alignment=TextAlignmentOptions.TopLeft;
            }
            foreach(var name in new[]{"OPTION1","OPTION2","ConnectingLine1","ConnectingLine2","GWL"})
            {var decoration=content.Find(name);if(decoration)foreach(var graphic in decoration.GetComponentsInChildren<Graphic>(true)){if(!phoneDecorations.ContainsKey(graphic))phoneDecorations[graphic]=graphic.enabled;graphic.enabled=false;}}
        }
        sealed class FeedbackLayout {public RectTransform viewport,body,originalParent;public ScrollRect scroll;public string text;}
        readonly Dictionary<int,FeedbackLayout> feedbackLayouts=new();
        void RestoreFeedback(Canvas canvas)
        {
            foreach(var layout in feedbackLayouts.Values.Where(l=>l.viewport&&l.viewport.GetComponentInParent<Canvas>(true)==canvas))
            {if(layout.body&&layout.originalParent)layout.body.SetParent(layout.originalParent,false);layout.viewport.gameObject.SetActive(false);}
        }
        void FeedbackViewport(RectTransform panel,RectTransform body,float width,float height,float unit)
        {
            if(!body||!body.GetComponent<TMP_Text>())return;int id=body.GetInstanceID();
            if(!feedbackLayouts.TryGetValue(id,out var layout))
            {
                var obj=new GameObject("__SPARC_UI_FeedbackScroll",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(RectMask2D),typeof(ScrollRect));obj.layer=panel.gameObject.layer;obj.transform.SetParent(panel,false);var image=obj.GetComponent<Image>();image.color=Color.clear;image.raycastTarget=true;
                layout=new FeedbackLayout{viewport=obj.GetComponent<RectTransform>(),body=body,originalParent=body.parent as RectTransform,scroll=obj.GetComponent<ScrollRect>()};layout.scroll.viewport=layout.viewport;layout.scroll.content=body;layout.scroll.horizontal=false;layout.scroll.vertical=true;layout.scroll.inertia=false;layout.scroll.movementType=ScrollRect.MovementType.Clamped;feedbackLayouts[id]=layout;
            }
            layout.viewport.gameObject.SetActive(true);var text=body.GetComponent<TMP_Text>();float normalized=layout.text==text.text?layout.scroll.verticalNormalizedPosition:1;layout.text=text.text;
            Box(layout.viewport,20,52,width,height,unit);body.SetParent(layout.viewport,false);float contentHeight=Mathf.Max(height,text.GetPreferredValues(text.text,width*unit,Mathf.Infinity).y/unit);
            Fixed(body,new Vector2(0,1),new Vector2(0,1),new Vector2(width,contentHeight)*unit);layout.scroll.verticalNormalizedPosition=float.IsNaN(normalized)?1:Mathf.Clamp01(normalized);
        }
        void CompactPeer(Canvas canvas)
        {
            var v=View(canvas);float w=Mathf.Min(720,v.width-32),inputHeight=Mathf.Min(232,v.height*.42f);
            if(canvas.name=="PlayerInputField"||canvas.name=="LAMInputField")
            {
                var field=canvas.GetComponentInChildren<TMP_InputField>(true);if(!field)return;
                foreach(var decoration in canvas.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name.StartsWith("ConnectionBubble",StringComparison.Ordinal)))foreach(var graphic in decoration.GetComponentsInChildren<Graphic>(true))graphic.enabled=false;
                float top=v.top+v.height-16-inputHeight;bool shortView=v.height<500;
                var card=PeerSurface(canvas.transform,"__SPARC_UI_PeerCard",OriginalUiTheme.Paper);Box(card,v.left+(v.width-w)/2,top,w,inputHeight,v.unit);
                var title=PeerHeading(canvas.transform);Box(title.rectTransform,v.left+(v.width-w)/2+16,top+12,w-32,28,v.unit);NativeText(title,20*v.unit,true);title.text="Your explanation";
                float fieldWidth=shortView?w-160:w-32,fieldHeight=shortView?inputHeight-60:inputHeight-116;
                Box(field.transform as RectTransform,v.left+(v.width-w)/2+16,top+48,fieldWidth,fieldHeight,v.unit);
                Stretch(field.textViewport,Vector2.zero,Vector2.one,new Vector2(12,10)*v.unit,new Vector2(-12,-10)*v.unit);
                foreach(var text in field.GetComponentsInChildren<TMP_Text>(true)){NativeText(text,18*v.unit);text.alignment=TextAlignmentOptions.TopLeft;}
                var send=canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name is "Send" or "__SPARC_UI_PeerSend");
                if(!send)
                {
                    var obj=new GameObject("__SPARC_UI_PeerSend",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(Button));obj.layer=canvas.gameObject.layer;obj.transform.SetParent(canvas.transform,false);obj.GetComponent<Image>().color=OriginalUiTheme.Teal;send=obj.GetComponent<Button>();send.targetGraphic=obj.GetComponent<Image>();send.onClick.AddListener(()=>runtime.peerAgent.Submit());
                    var label=new GameObject("Label",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));label.layer=obj.layer;label.transform.SetParent(obj.transform,false);Stretch(label.GetComponent<RectTransform>(),Vector2.zero,Vector2.one);var caption=label.GetComponent<TMP_Text>();caption.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");caption.text="Send";caption.color=Color.white;caption.raycastTarget=false;
                }
                Box(send.transform as RectTransform,v.left+(v.width-w)/2+w-136,top+inputHeight-60,120,48,v.unit);
                foreach(var text in send.GetComponentsInChildren<TMP_Text>(true)){NativeText(text,17*v.unit,true);text.alignment=TextAlignmentOptions.Center;}
                var primaryInput=runtime.First("LLM_Manager_Unified")?.Component<TMP_InputField>("UserInputField");
                send.interactable=field==primaryInput&&runtime.peerAgent&&!runtime.peerAgent.Pending&&runtime.peerAgent.Outcome.Length==0&&!string.IsNullOrWhiteSpace(field.text);
            }
            else if(canvas.name=="AnswerField"||canvas.name=="LAMAnswerField")
            {
                var panel=canvas.transform.Find("GameObject") as RectTransform;if(!panel)return;
                bool inputVisible=canvases.Any(c=>c&&c.isActiveAndEnabled&&(c.name is "PlayerInputField" or "LAMInputField"));
                var answer=Child(panel,"AF")?.GetComponent<TMP_Text>();NativeText(answer,18*v.unit);
                float answerHeight=answer?answer.GetPreferredValues(answer.text,(w-40)*v.unit,Mathf.Infinity).y/v.unit:44;
                float available=Mathf.Max(160,v.height-(inputVisible?inputHeight:0)-48),h=Mathf.Min(Mathf.Clamp(answerHeight+132,172,330),available),top=v.top+16+Mathf.Max(0,(available-h)/2);
                Box(panel,v.left+(v.width-w)/2,top,w,h,v.unit);Stretch(panel.Find("BG") as RectTransform,Vector2.zero,Vector2.one);
                var feedbackCard=PeerSurface(panel,"__SPARC_UI_FeedbackCard",OriginalUiTheme.Paper);Stretch(feedbackCard,Vector2.zero,Vector2.one);
                Box(panel.Find("Name") as RectTransform,20,12,w-40,30,v.unit);
                foreach(var text in panel.GetComponentsInChildren<TMP_Text>(true))NativeText(text,18*v.unit);
                FeedbackViewport(panel,Child(panel,"AF"),w-40,Mathf.Max(44,h-132),v.unit);
                foreach(var button in panel.GetComponentsInChildren<Button>(true)){Box(button.transform as RectTransform,w-180,h-60,160,48,v.unit);foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)){text.text=button.name=="ContinueButton"?"Try again":"Continue";text.enabled=true;NativeText(text,17*v.unit,true);text.alignment=TextAlignmentOptions.Center;}}
                Box(panel.Find("GradientWhiteline") as RectTransform,20,46,w-40,1,v.unit);
            }
            else if(canvas.name=="WaitForResponse")
            {
                bool inputVisible=canvases.Any(c=>c&&c.isActiveAndEnabled&&(c.name is "PlayerInputField" or "LAMInputField"));
                float h=92,top=v.top+16+Mathf.Max(0,(v.height-(inputVisible?inputHeight:0)-48-h)/2);var p=canvas.transform.Find("P") as RectTransform;Box(p,v.left+(v.width-w)/2,top,w,h,v.unit);
                if(p){var background=PeerSurface(p,"__SPARC_UI_WaitCard",OriginalUiTheme.Teal);Stretch(background,Vector2.zero,Vector2.one);var image=Child(p,"Image");Box(image,16,22,48,48,v.unit);foreach(var text in p.GetComponentsInChildren<TMP_Text>(true)){Box(text.rectTransform,80,18,w-100,56,v.unit);NativeText(text,18*v.unit);text.color=Color.white;text.alignment=TextAlignmentOptions.MidlineLeft;}}
            }
        }
        static RectTransform PeerSurface(Transform parent,string name,Color color)
        {
            var found=parent.Find(name) as RectTransform;if(found)return found;
            var obj=new GameObject(name,typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));obj.layer=parent.gameObject.layer;obj.transform.SetParent(parent,false);obj.transform.SetAsFirstSibling();var image=obj.GetComponent<Image>();image.color=color;image.raycastTarget=false;return obj.GetComponent<RectTransform>();
        }
        static TMP_Text PeerHeading(Transform parent)
        {
            var found=parent.Find("__SPARC_UI_PeerHeading");if(found)return found.GetComponent<TMP_Text>();
            var obj=new GameObject("__SPARC_UI_PeerHeading",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));obj.layer=parent.gameObject.layer;obj.transform.SetParent(parent,false);var text=obj.GetComponent<TMP_Text>();text.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");text.raycastTarget=false;text.color=OriginalUiTheme.Ink;return text;
        }
        public void PreparePanel(Canvas canvas)
        {if(!canvas)return;if(canvas.name=="QuestionCanvas")Question(canvas);else PeerPanel(canvas);}
        /// <summary>Reflows native widgets against the current Canvas viewport, including test cameras.</summary>
        public void RefreshResponsiveLayout(Canvas canvas)
        {
            if(!canvas)return;var applied=View(canvas);
            var manager=canvas.GetComponentsInChildren<OriginalBehaviour>(true).FirstOrDefault(c=>c.sourceType is "RedBloodCellGameManager" or "WhiteBloodCellGameManager");
            if(manager){CompactGame(manager,canvas);LayoutGameStick(canvas);OriginalComponents.Ensure<OriginalMiniPresentation>(canvas.gameObject).Prepare(manager);}else {if(canvas.name=="QuestionCanvas")CompactQuestion(canvas);else CompactPeer(canvas);}
            Canvas.ForceUpdateCanvases();appliedViewports[canvas.GetInstanceID()]=applied;
        }
        /// <summary>CanvasScaler may settle after activation without a browser resize.</summary>
        public bool RefreshViewportChanges(Canvas canvas)
        {
            if(!canvas)return false;var view=View(canvas);if(appliedViewports.TryGetValue(canvas.GetInstanceID(),out var before)&&before.Matches(view))return false;
            RefreshResponsiveLayout(canvas);return true;
        }
        void LateUpdate()
        {
            if(!runtime||!runtime.Started||runtime.ShuttingDown)return;
            foreach(var canvas in canvases)if(canvas&&canvas.isActiveAndEnabled)
            {if(canvas.name=="QuestionCanvas")Question(canvas);else if(canvas.name is "PlayerInputField" or "LAMInputField" or "AnswerField" or "LAMAnswerField" or "WaitForResponse")PeerPanel(canvas);}
        }
        void Update()
        {
            if(!runtime||!runtime.Started||runtime.ShuttingDown||Time.unscaledTime<next)return;next=Time.unscaledTime+0.2f;
            foreach(var button in launchers)if(button&&button.gameObject.activeSelf)
            {if(startedLaunchers.Contains(button.GetInstanceID()))button.gameObject.SetActive(false);else if(!runtime.activities.IsOpen)RepairLauncher(button);}
            bool resized=width!=Screen.width||height!=Screen.height||!Mathf.Approximately(pixelScale,OriginalMobileControls.PixelScale);width=Screen.width;height=Screen.height;pixelScale=OriginalMobileControls.PixelScale;
            foreach(var game in runtime.activities.Games)if(game&&game.source.isActiveAndEnabled&&!game.Continued)
            {if(resized)PrepareGame(game.source,false);else RefreshViewportChanges(game.GetComponentInParent<Canvas>());}
        }
    }
}
