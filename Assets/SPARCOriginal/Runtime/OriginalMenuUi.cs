using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Runs the source SettingPanel's native show/hide and render-scale bindings.</summary>
    public sealed class OriginalMenuUi:MonoBehaviour
    {
        public OriginalRuntime runtime;
        OriginalBehaviour source;GameObject panel,objectives;Canvas canvas,playerCanvas;CanvasGroup group;
        Button settings,close;Slider renderScale;TMP_Text scaleValue;
        bool initialized,opened,previousInputLock,previousObjectivesVisible,changedRenderScale;int width,height;
        UniversalRenderPipelineAsset pipeline;float originalRenderScale;
        float appliedScale,appliedDensity;Rect appliedPixels;
        public bool IsOpen=>panel&&panel.activeInHierarchy&&group&&group.alpha>.5f;
        public GameObject NativePanel=>panel;
        void Start(){Initialize();}
        public void Initialize()
        {
            if(initialized||!runtime)return;source=runtime.First("SettingPanel");if(!source)return;
            panel=source.Object("panelRoot")??source.gameObject;canvas=panel.GetComponent<Canvas>();
            group=source.Component<CanvasGroup>("canvasGroup")??OriginalComponents.Ensure<CanvasGroup>(panel);
            settings=source.Component<Button>("settingButton")??FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(b=>b.gameObject.scene==gameObject.scene&&b.name=="SettingBtn");
            close=source.Component<Button>("closeButton");if(!settings||!close||!canvas)return;
            playerCanvas=settings.GetComponentInParent<Canvas>(true);objectives=runtime.First("MeetingCellsObjectiveHudTopLeftLock")?.gameObject;
            // Append the source script's dynamic handlers. Keep both authored SetActive
            // persistent calls, which switch SettingUI and PlayerCanvas, intact.
            settings.onClick.AddListener(ShowPanel);close.onClick.AddListener(HidePanel);
            renderScale=source.Component<Slider>("renderScaleSlider");scaleValue=source.Component<TMP_Text>("renderScaleValueText");
            pipeline=(QualitySettings.renderPipeline??GraphicsSettings.currentRenderPipeline) as UniversalRenderPipelineAsset;
            if(renderScale)
            {
                renderScale.minValue=source.Number("minRenderScale",.5f);renderScale.maxValue=source.Number("maxRenderScale",1.5f);
                if(pipeline)renderScale.SetValueWithoutNotify(Mathf.Clamp(pipeline.renderScale,renderScale.minValue,renderScale.maxValue));
                RenderScaleLabel(renderScale.value);renderScale.onValueChanged.AddListener(ChangeRenderScale);
            }
            initialized=true;HidePanel();
        }
        public void ShowPanel()
        {
            Initialize();if(!initialized||(runtime&&runtime.HandoffStarted))return;
            if(!opened)
            {
                if(runtime.player){previousInputLock=runtime.player.InputLocked;runtime.player.InputLocked=true;}
                if(objectives)previousObjectivesVisible=objectives.activeSelf;
            }
            opened=true;
            for(var parent=panel.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
            panel.SetActive(true);group.alpha=1;group.interactable=group.blocksRaycasts=true;
            if(playerCanvas)playerCanvas.gameObject.SetActive(false);if(objectives)objectives.SetActive(false);
            RepairLayout();
        }
        public void HidePanel()
        {
            if(!panel||!group)return;group.alpha=0;group.interactable=group.blocksRaycasts=false;panel.SetActive(false);
            if(opened)
            {
                if(playerCanvas)playerCanvas.gameObject.SetActive(true);if(objectives)objectives.SetActive(previousObjectivesVisible);
                if(runtime&&runtime.player)runtime.player.InputLocked=previousInputLock;
            }
            opened=false;
        }
        void RenderScaleLabel(float value){if(scaleValue)scaleValue.text=value.ToString("F2")+"x";}
        void ChangeRenderScale(float value)
        {
            float step=Mathf.Max(.01f,source.Number("renderScaleStep",.1f));float rounded=Mathf.Clamp(Mathf.Round(value/step)*step,renderScale.minValue,renderScale.maxValue);
            renderScale.SetValueWithoutNotify(rounded);RenderScaleLabel(rounded);
            if(pipeline&&source.Flag("applyRenderScaleImmediately",true))
            {if(!changedRenderScale){originalRenderScale=pipeline.renderScale;changedRenderScale=true;}pipeline.renderScale=rounded;}
        }
        static RectTransform Child(Transform parent,string name)=>parent.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(t=>t.name==name);
        static void Rect(RectTransform r,Vector2 anchor,Vector2 pivot,Vector2 position,Vector2 size)
        {if(!r)return;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.anchorMin=r.anchorMax=anchor;r.pivot=pivot;r.anchoredPosition=position;r.sizeDelta=size;}
        static void Stretch(RectTransform r,Vector2 min,Vector2 max)
        {if(!r)return;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.anchorMin=min;r.anchorMax=max;r.offsetMin=r.offsetMax=Vector2.zero;}
        public void RepairLayout()
        {
            if(!canvas)return;width=Screen.width;height=Screen.height;
            canvas.enabled=true;canvas.renderMode=RenderMode.ScreenSpaceOverlay;canvas.overrideSorting=true;canvas.sortingOrder=1500;
            var rect=(RectTransform)canvas.transform;rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
            var scaler=OriginalComponents.Ensure<CanvasScaler>(canvas.gameObject);scaler.uiScaleMode=CanvasScaler.ScaleMode.ScaleWithScreenSize;scaler.referenceResolution=new Vector2(1440,900);scaler.screenMatchMode=CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;scaler.matchWidthOrHeight=.5f;
            OriginalComponents.Ensure<GraphicRaycaster>(canvas.gameObject).enabled=true;
            Canvas.ForceUpdateCanvases();RefreshLayoutForViewport();
        }
        static void Readable(TMP_Text text,float size,bool bold=false)
        {if(!text)return;text.enableAutoSizing=false;text.fontSize=size;text.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;text.alignment=TextAlignmentOptions.MidlineLeft;text.color=OriginalUiTheme.Ink;text.margin=Vector4.zero;}
        public void RefreshLayoutForViewport()
        {
            if(!canvas)return;float density=OriginalMobileControls.PixelScale,layoutScale=canvas.scaleFactor,unit=density/Mathf.Max(.1f,layoutScale);var layoutPixels=canvas.pixelRect;
            float cssWidth=canvas.pixelRect.width/density,cssHeight=canvas.pixelRect.height/density;
            float margin=cssWidth<560?16:24,w=Mathf.Max(1,Mathf.Min(760,cssWidth-margin*2)),h=Mathf.Max(1,Mathf.Min(cssWidth<560?440:380,cssHeight-margin*2)),pad=cssWidth<560?22:28;
            var center=new Vector2(.5f,.5f);var background=Child(panel.transform,"Background (1)");Rect(background,center,center,Vector2.zero,new Vector2(w,h)*unit);
            if(background&&background.GetComponent<Image>())background.GetComponent<Image>().color=Color.white;
            var headings=Child(panel.transform,"Left Hand Toggle");Rect(headings,center,center,Vector2.zero,new Vector2(w,h)*unit);
            var title=Child(headings?headings:panel.transform,"Title");Rect(title,new Vector2(0,1),new Vector2(0,1),new Vector2(pad,-pad)*unit,new Vector2(w-pad*2-60,44)*unit);if(title)Readable(title.GetComponent<TMP_Text>(),22*unit,true);
            Rect(close.transform as RectTransform,center,new Vector2(1,1),new Vector2(w/2-pad,h/2-pad)*unit,new Vector2(44,44)*unit);
            float contentWidth=w-pad*2;var scale=Child(panel.transform,"Screen Scaling");Rect(scale,center,center,new Vector2(0,h/2-136)*unit,new Vector2(contentWidth,94)*unit);
            if(scale)
            {
                var label=Child(scale,"ScreenScaleText");Rect(label,new Vector2(0,1),new Vector2(0,1),Vector2.zero,new Vector2(contentWidth,28)*unit);if(label)Readable(label.GetComponent<TMP_Text>(),18*unit);
                if(renderScale)
                {
                    Rect(renderScale.transform as RectTransform,new Vector2(0,0),new Vector2(0,.5f),new Vector2(0,24)*unit,new Vector2(Mathf.Max(80,contentWidth-96),44)*unit);
                    var hit=Child(renderScale.transform,"__SPARC_MENU_SliderHit");
                    if(!hit){var obj=new GameObject("__SPARC_MENU_SliderHit",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));obj.layer=renderScale.gameObject.layer;obj.transform.SetParent(renderScale.transform,false);obj.transform.SetAsFirstSibling();hit=obj.GetComponent<RectTransform>();var image=obj.GetComponent<Image>();image.color=Color.clear;image.raycastTarget=true;}
                    Stretch(hit,Vector2.zero,Vector2.one);
                    if(renderScale.handleRect)renderScale.handleRect.sizeDelta=new Vector2(24,32)*unit;
                }
                if(scaleValue){Rect(scaleValue.rectTransform,new Vector2(1,0),new Vector2(1,.5f),new Vector2(0,24)*unit,new Vector2(80,44)*unit);Readable(scaleValue,17*unit);scaleValue.alignment=TextAlignmentOptions.MidlineRight;}
            }
            // Retain portal-only and placeholder source widgets without exposing inert actions.
            foreach(var name in new[]{"ExitGameBtn","Exit2NavigationBtn","Toggle_Switch-Group","TimeToggle","CharacterCustomizeBtn","Outfit Change Text"})
            {var item=Child(panel.transform,name);if(item)item.gameObject.SetActive(false);}
            var help=Child(panel.transform,"__SPARC_MENU_ControlHelp");
            if(!help)
            {
                var obj=new GameObject("__SPARC_MENU_ControlHelp",typeof(RectTransform),typeof(CanvasRenderer),typeof(TextMeshProUGUI));obj.layer=panel.layer;obj.transform.SetParent(panel.transform,false);help=obj.GetComponent<RectTransform>();var text=obj.GetComponent<TMP_Text>();text.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");text.raycastTarget=false;
            }
            Rect(help,center,center,new Vector2(0,-h/2+pad+68)*unit,new Vector2(contentWidth,136)*unit);var helpText=help.GetComponent<TMP_Text>();
            helpText.text=OriginalHudLayout.HasTouch?"<b>Touch controls</b>\nLeft stick: Move\nRight swipe: Look\nTap Jump / Talk nearby\nCamera icon: Switch view":"<b>Controls</b>\nWASD: Move\nRight-drag: Look\nE: Talk nearby    Space: Jump    V: Switch view";
            Readable(helpText,17*unit);helpText.alignment=TextAlignmentOptions.TopLeft;helpText.textWrappingMode=TextWrappingModes.Normal;helpText.lineSpacing=4;
            var reverse=Child(panel.transform,"ContronReverseToggle");var perspective=Child(panel.transform,"PerspectiveSwitchToggle");
            var reverseLabel=Child(panel.transform,"Left Handed txt");if(reverseLabel)reverseLabel.gameObject.SetActive(reverse&&reverse.gameObject.activeSelf);
            var perspectiveLabel=Child(panel.transform,"Perspective Switch txt");if(perspectiveLabel)perspectiveLabel.gameObject.SetActive(perspective&&perspective.gameObject.activeSelf);
            Canvas.ForceUpdateCanvases();appliedScale=layoutScale;appliedDensity=density;appliedPixels=layoutPixels;
        }
        /// <summary>Recover when CanvasScaler or browser density settles after ShowPanel.</summary>
        public bool RefreshViewportChanges()
        {
            if(!canvas||!IsOpen)return false;
            if(Mathf.Approximately(appliedScale,canvas.scaleFactor)&&Mathf.Approximately(appliedDensity,OriginalMobileControls.PixelScale)&&appliedPixels==canvas.pixelRect)return false;
            RefreshLayoutForViewport();return true;
        }
        void Update()
        {
            if(!initialized)return;
            if(runtime&&runtime.HandoffStarted){if(IsOpen)HidePanel();return;}
            if(IsOpen){if(Screen.width!=width||Screen.height!=height)RepairLayout();else RefreshViewportChanges();}
            if(Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame&&runtime&&!runtime.CinematicActive&&!runtime.activities.IsOpen)
            {if(IsOpen)HidePanel();else if(runtime.Started)ShowPanel();}
        }
        void OnDestroy()
        {
            if(settings)settings.onClick.RemoveListener(ShowPanel);if(close)close.onClick.RemoveListener(HidePanel);if(renderScale)renderScale.onValueChanged.RemoveListener(ChangeRenderScale);
            if(opened&&objectives)objectives.SetActive(previousObjectivesVisible);
            if(opened&&runtime&&runtime.player)runtime.player.InputLocked=previousInputLock;
            if(changedRenderScale&&pipeline)pipeline.renderScale=originalRenderScale;
        }
    }
}
