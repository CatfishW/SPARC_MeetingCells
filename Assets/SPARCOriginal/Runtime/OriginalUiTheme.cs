using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Complete native UI skin, including inactive canvases and newly spawned prefab children.
    /// Changes presentation only: native objects, text, raycasts, event callbacks and physics stay in place.</summary>
    [DefaultExecutionOrder(2000)]
    public sealed class OriginalUiTheme : MonoBehaviour
    {
        public static readonly Color Ink = new Color(.12f,.21f,.23f);
        public static readonly Color Teal = new Color(.20f,.40f,.42f);
        public static readonly Color Paper = new Color(.98f,.97f,.93f);
        public static readonly Color Muted = new Color(.40f,.48f,.47f);
        public static readonly Color Rust = new Color(.66f,.29f,.23f);
        const string Own = "__SPARC_UI_";
        readonly Dictionary<int,string> coverage = new();
        readonly Dictionary<int,Graphic> graphics = new();
        readonly Dictionary<int,Color> sourceColors = new();
        readonly Dictionary<int,string> sourceTextures = new();
        readonly Dictionary<int,string> roles = new();
        readonly HashSet<int> fontStyled = new();
        readonly Dictionary<int,float> subtitleFontSizes = new();
        readonly Dictionary<int,Vector2> cuePositions = new();
        readonly Dictionary<int,float> cueRestingY = new();
        readonly List<Texture2D> textures = new();
        // Each native graphic is styled once. Re-applying the whole skin on a timer fought the
        // HUD, launcher, interaction and menu owners (alternating colour, sprite and margins)
        // and rebuilt every path string, which read as flickering text and periodic hitches.
        readonly HashSet<int> styled = new(),styledButtons = new(),styledShadows = new(),styledFields = new();
        readonly List<TMP_Text> teachingCues = new(),sceneSubtitles = new();
        readonly List<Sprite> sprites = new();
        TMP_FontAsset font; Material fontMaterial,subtitleMaterial; Font legacyFont;
        Sprite panel,primary,tile,drop,line;
        float next;
        string hint; Rect hintAnchor;
        public int StyledGraphicCount => coverage.Count;

        void Start(){Initialize();Refresh();}
        void Initialize()
        {
            if(panel)return;
            panel=Frame("Paper",Paper,false);primary=Frame("Primary",Teal,false);
            tile=Frame("Word tile",new Color(.94f,.96f,.91f),false);
            // The authored tutorial explicitly calls these the blue spaces.
            drop=Frame("Answer slot",new Color(.80f,.91f,.97f),true);
            line=Frame("Rule",Color.white,false);
            var original=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if(original)
            {
                font=Instantiate(original);font.name="SPARC UI / readable type";
                font.fallbackFontAssetTable=FindObjectsByType<TMP_Text>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                    .Where(t=>t.gameObject.scene==gameObject.scene&&t.font&&t.font!=original).Select(t=>t.font).Distinct().ToList();
                fontMaterial=new Material(original.material){name="SPARC UI / clean ink type"};font.material=fontMaterial;
                subtitleMaterial=new Material(fontMaterial){name="SPARC UI / scene subtitle"};
                subtitleMaterial.EnableKeyword("OUTLINE_ON");
                subtitleMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth,.15f);
                subtitleMaterial.SetColor(ShaderUtilities.ID_OutlineColor,new Color(.05f,.07f,.08f,1));
            }
            legacyFont=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }
        Sprite Frame(string title,Color fill,bool dashed)
        {
            const int size=64;var texture=new Texture2D(size,size,TextureFormat.RGBA32,false){name="SPARC UI / "+title,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp};
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                float qx=Mathf.Max(Mathf.Abs(x-31.5f)-23.5f,0),qy=Mathf.Max(Mathf.Abs(y-31.5f)-23.5f,0);
                float d=Mathf.Sqrt(qx*qx+qy*qy)-7;
                float alpha=1-Mathf.Clamp01(d+.5f);
                bool border=d> -1.35f&&(!dashed||(x+y)%12<7);
                float grain=((x*17+y*31)%11)*.0006f;
                var c=border?new Color(Ink.r,Ink.g,Ink.b,.65f):new Color(fill.r-grain,fill.g-grain,fill.b-grain,1);
                c.a*=alpha;pixels[y*size+x]=c;
            }
            texture.SetPixels(pixels);texture.Apply(false,true);textures.Add(texture);
            var sprite=Sprite.Create(texture,new Rect(0,0,size,size),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,new Vector4(12,12,12,12));
            sprite.name="SPARC UI / "+title;sprites.Add(sprite);return sprite;
        }
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static bool Has(string value,params string[] words)=>words.Any(word=>value.Contains(word,StringComparison.OrdinalIgnoreCase));
        static bool TeachingCue(Transform target)=>Has(PathOf(target),"/StorylineRBC_PointAhead/Subtitle1_CAP/Content/","/Storyline_introduce_yellow_circle/Subtitle1_CAP/Content/","/Storyline_introduce_task_list_fold/TIP/Content/");
        static bool PendingCue(Transform target)=>Has(PathOf(target),"/WaitForResponse/P/");
        static bool WorldCue(Transform target)=>Has(PathOf(target),"/Teleport/Canvas/");
        static bool IsOwned(Transform t)=>t.name.StartsWith(Own,StringComparison.Ordinal);
        public static bool IsOriginalMiniGame(Transform target)=>Has(PathOf(target),"RedBloodCellGameCanvas","WhiteBloodCellGameCanvas","WordGameManager");
        string OriginalTexture(Graphic graphic)
        {
            int id=graphic.GetInstanceID();if(sourceTextures.TryGetValue(id,out var name))return name;
            name=graphic is Image image&&image.sprite?image.sprite.name:graphic is RawImage raw&&raw.texture?raw.texture.name:"";
            sourceTextures[id]=name;return name;
        }
        void Mark(Graphic graphic,string role)
        {int id=graphic.GetInstanceID();coverage[id]=role;graphics[id]=graphic;roles[id]=role;}
        public bool HasStyled(Graphic graphic)
        {
            if(!graphic)return false;if(coverage.ContainsKey(graphic.GetInstanceID()))return true;
            // TMP can create a fallback-font draw surface during Canvas rebuild,
            // after the theme pass. It uses its styled parent text's material;
            // record that inherited styling rather than leaving glyphs unclassified.
            if(graphic is TMP_SubMeshUI)
            {var parent=graphic.GetComponentInParent<TMP_Text>(true);if(parent&&coverage.ContainsKey(parent.GetInstanceID())){Mark(graphic,"styled text / fallback glyph surface");return true;}}
            return false;
        }
        public string RoleOf(Graphic graphic)=>graphic&&roles.TryGetValue(graphic.GetInstanceID(),out var role)?role:"unclassified";
        public object[] CoverageSnapshot()=>graphics.Values.Where(g=>g).Select(g=>new{path=PathOf(g.transform),type=g.GetType().Name,active=g.gameObject.activeInHierarchy,enabled=g.enabled,role=RoleOf(g),originalTexture=OriginalTexture(g),currentTexture=g is Image i&&i.sprite?i.sprite.name:g is RawImage r&&r.texture?r.texture.name:""}).Cast<object>().ToArray();
        public void Refresh()
        {
            Initialize();
            var all=FindObjectsByType<Graphic>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(g=>g.gameObject.scene==gameObject.scene&&!styled.Contains(g.GetInstanceID())).ToArray();
            foreach(var g in all){OriginalTexture(g);if(!sourceColors.ContainsKey(g.GetInstanceID()))sourceColors[g.GetInstanceID()]=g.color;}
            foreach(var effect in FindObjectsByType<Shadow>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(effect.gameObject.scene==gameObject.scene&&styledShadows.Add(effect.GetInstanceID())&&(!IsOriginalMiniGame(effect.transform)||effect.GetComponent<TMP_Text>()||effect.GetComponent<Text>()))effect.enabled=false;
            foreach(var button in FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(button.gameObject.scene==gameObject.scene&&styledButtons.Add(button.GetInstanceID()))StyleButton(button);
            foreach(var g in all)
            {
                if(!g)continue;
                if(g is TMP_Text tmp)StyleText(tmp);
                else if(g is Text text)StyleLegacyText(text);
                else if(g is Image image)StyleImage(image);
                else if(g is RawImage raw)StyleRaw(raw);
                else Mark(g,"native graphic / ink palette");
                styled.Add(g.GetInstanceID());
            }
            StyleWorldMarkers();
            foreach(var field in FindObjectsByType<TMP_InputField>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(field.gameObject.scene==gameObject.scene&&styledFields.Add(field.GetInstanceID()))
                {field.customCaretColor=true;field.caretColor=Ink;field.selectionColor=new Color(Teal.r,Teal.g,Teal.b,.22f);if(field.placeholder is TMP_Text placeholder){placeholder.color=Muted;placeholder.fontStyle=FontStyles.Italic;}}
            foreach(var field in FindObjectsByType<InputField>(FindObjectsInactive.Include,FindObjectsSortMode.None))
                if(field.gameObject.scene==gameObject.scene&&styledFields.Add(field.GetInstanceID()))
                {field.customCaretColor=true;field.caretColor=Ink;field.selectionColor=new Color(Teal.r,Teal.g,Teal.b,.22f);}
        }
        /// <summary>Captions animate under their Timelines; follow them every frame so the
        /// safe-area fit never lags the authored motion or the typewriter text.</summary>
        void LayoutCaptions()
        {
            foreach(var text in teachingCues)if(text&&text.isActiveAndEnabled)LayoutTeachingCue(text);
            foreach(var text in sceneSubtitles)if(text&&text.isActiveAndEnabled)LayoutSceneSubtitle(text);
        }
        public static string Semantic(Transform target)
        {
            string n=target.name.ToLowerInvariant(),path=PathOf(target).ToLowerInvariant();
            if(Has(n,"previous","prev","back","return"))return "back";
            if(Has(n,"next","continue","complete","submit"))return n.Contains("submit")?"submit":"next";
            if(Has(n,"noButton","cancel","close"))return "cancel";
            if(Has(n,"yesbutton","confirm","assign"))return "check";
            if(n.Contains("exit"))return Has(path,"victory","endgame")?"next":"exit";
            if(Has(n,"viewreset","resetpov","reset"))return "reset";
            if(Has(n,"perspective","pov","camera"))return "camera";
            if(Has(n,"chat","gpt","mqtt"))return "chat";
            if(Has(n,"setting"))return "settings";
            if(Has(n,"instruction","help"))return "help";
            if(Has(n,"knowledge","map"))return "map";
            if(Has(n,"custom","appearance","outfit"))return "avatar";
            if(Has(n,"send"))return "send";
            if(Has(n,"random"))return "shuffle";
            if(Has(n,"hair"))return "hair";if(Has(n,"brow"))return "brows";
            if(Has(n,"eyewear"))return "glasses";if(Has(n,"eyes"))return "eyes";
            if(Has(n,"mouth"))return "mouth";if(Has(n,"skin","color"))return "palette";
            if(Has(n,"bottom"))return "trousers";if(Has(n,"top"))return "shirt";
            if(Has(n,"shoe"))return "shoes";if(Has(n,"glove"))return "glove";if(Has(n,"mask"))return "mask";
            if(Has(n,"jump"))return "jump";if(Has(n,"time"))return "time";
            if(Has(n,"music"))return "music";if(Has(n,"sound","volume"))return "sound";
            if(Has(n,"objective","quest","task"))return "task";
            if(Has(n,"play","start"))return "play";
            if(Has(n,"option","button a","button b"))return "check";
            if(Has(n,"talk","interact"))return "talk";
            return "info";
        }
        static bool Primary(Button b)=>Has(b.name,"playgame","start","next","continue","send","submit","confirm","yesbutton","complete");
        static Color KeepOpacity(Graphic graphic,Color color){color.a=graphic.color.a;return color;}
        static bool Word(Transform t)=>t.GetComponent<OriginalWordTile>()||Has(t.name,"draggable");
        void StyleButton(Button button)
        {
            // The activities retain their own original art and visual themes.
            // Their existing responsive layout and callback repairs still apply.
            if(IsOriginalMiniGame(button.transform))
            {if(button.targetGraphic)Mark(button.targetGraphic,"original minigame control / retained artwork");return;}
            // Several source controls have no targetGraphic and rely entirely on
            // their old icon for raycasts. Replacing that icon must retain a hit
            // surface, rather than leaving a good-looking but unclickable button.
            if(!button.targetGraphic||!button.targetGraphic.raycastTarget)
            {
                var existing=button.GetComponentsInChildren<Graphic>(true).Where(g=>g.GetComponentInParent<Button>(true)==button).ToArray();
                bool visible=button.targetGraphic?button.targetGraphic.enabled:existing.Length==0||existing.Any(g=>g.enabled);
                var surface=new GameObject(Own+"Surface",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(LayoutElement));surface.layer=button.gameObject.layer;
                surface.transform.SetParent(button.transform,false);surface.transform.SetAsFirstSibling();
                var r=surface.GetComponent<RectTransform>();r.anchorMin=Vector2.zero;r.anchorMax=Vector2.one;r.offsetMin=r.offsetMax=Vector2.zero;
                surface.GetComponent<LayoutElement>().ignoreLayout=true;
                var target=surface.GetComponent<Image>();target.raycastTarget=true;target.enabled=visible;button.targetGraphic=target;
            }
            bool word=Word(button.transform);bool primaryButton=!word&&Primary(button);
            var labels=button.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.GetComponentInParent<Button>(true)==button&&!Has(PathOf(t.transform),"NotificationBadge")).ToArray();
            var legacy=button.GetComponentsInChildren<Text>(true).Where(t=>t.GetComponentInParent<Button>(true)==button&&!Has(PathOf(t.transform),"NotificationBadge")).ToArray();
            bool hasLabel=labels.Any(t=>!string.IsNullOrWhiteSpace(t.text))||legacy.Any(t=>!string.IsNullOrWhiteSpace(t.text));
            bool symbolic=!word&&labels.Cast<Graphic>().Concat(legacy).Any()&&labels.Select(t=>t.text.Trim()).Concat(legacy.Select(t=>t.text.Trim())).All(t=>t=="?"||t=="!"||t=="×"||t=="<"||t==">"||t=="X"||t=="");
            if(symbolic){hasLabel=false;foreach(var label in labels){label.enabled=false;Mark(label,"replaced symbolic icon label");}foreach(var label in legacy){label.enabled=false;Mark(label,"replaced symbolic icon label");}}
            if(button.targetGraphic is Image image){image.sprite=word?tile:primaryButton?primary:panel;image.type=Image.Type.Sliced;image.color=Color.white;image.material=null;Mark(image,word?"redesigned word tile":"redesigned button surface");}
            else if(button.targetGraphic is RawImage raw){raw.texture=(primaryButton?primary:panel).texture;raw.color=Color.white;raw.material=null;Mark(raw,"redesigned button surface");}
            var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.89f,.95f,.93f);colors.pressedColor=new Color(.73f,.85f,.83f);colors.selectedColor=new Color(.84f,.93f,.90f);colors.disabledColor=new Color(.65f,.65f,.65f,.5f);colors.fadeDuration=.1f;button.colors=colors;button.transition=Selectable.Transition.ColorTint;
            foreach(var g in button.GetComponentsInChildren<Graphic>(true))
            {
                if(g.GetComponentInParent<Button>(true)!=button||g==button.targetGraphic||g is TMP_Text||g is Text||IsOwned(g.transform))continue;
                if(Has(PathOf(g.transform),"NotificationBadge")&&g is Image badge)
                {badge.sprite=panel;badge.overrideSprite=null;badge.type=Image.Type.Sliced;badge.color=KeepOpacity(badge,Rust);badge.material=null;Mark(badge,"redesigned notification badge");continue;}
                g.enabled=false;Mark(g,"replaced original button ornament");
            }
            if(!word)
            {
                var icon=button.transform.Find(Own+"Icon");Image graphic;
                if(!icon){var obj=new GameObject(Own+"Icon",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image),typeof(LayoutElement));obj.layer=button.gameObject.layer;obj.transform.SetParent(button.transform,false);obj.GetComponent<LayoutElement>().ignoreLayout=true;graphic=obj.GetComponent<Image>();}
                else graphic=icon.GetComponent<Image>();
                graphic.sprite=OriginalUiIcons.Get(Semantic(button.transform));graphic.color=primaryButton?Color.white:Ink;graphic.raycastTarget=false;graphic.preserveAspect=true;
                graphic.enabled=button.targetGraphic==null||button.targetGraphic.enabled;
                var r=graphic.rectTransform;var size=((RectTransform)button.transform).rect.size;
                if(hasLabel&&size.x>145)
                {r.anchorMin=r.anchorMax=new Vector2(0,.5f);r.pivot=new Vector2(0,.5f);r.anchoredPosition=new Vector2(14,0);r.sizeDelta=new Vector2(24,24);foreach(var label in labels)label.margin=new Vector4(36,0,10,0);}
                else if(hasLabel&&labels.Any(t=>t.text.Trim().Length>1)&&size.x>=80)
                {r.anchorMin=r.anchorMax=new Vector2(0,.5f);r.pivot=new Vector2(0,.5f);r.anchoredPosition=new Vector2(8,0);r.sizeDelta=new Vector2(18,18);foreach(var label in labels)label.margin=new Vector4(27,0,6,0);}
                else if(!hasLabel){r.anchorMin=new Vector2(.17f,.17f);r.anchorMax=new Vector2(.83f,.83f);r.offsetMin=r.offsetMax=Vector2.zero;}
                else graphic.enabled=false;
                Mark(graphic,"new sketch icon / "+Semantic(button.transform));
                var hover=button.GetComponent<OriginalUiHint>()??button.gameObject.AddComponent<OriginalUiHint>();hover.theme=this;hover.label=hasLabel?"":HintLabel(button);
            }
        }
        static string HintLabel(Button b)
        {
            string key=Semantic(b.transform);
            return key switch{"avatar"=>"Character","chat"=>"Chat requires SPARC multiplayer","camera"=>"First / third person","reset"=>"Reset view","settings"=>"Settings","task"=>"Task details","map"=>"Knowledge map",_=>char.ToUpperInvariant(key[0])+key.Substring(1)};
        }
        void StyleText(TMP_Text text)
        {
            if(IsOriginalMiniGame(text.transform))
            {
                if(font&&fontStyled.Add(text.GetInstanceID())){text.font=font;text.fontSharedMaterial=fontMaterial;}
                Mark(text,"original minigame typography / clean readable font");return;
            }
            var button=text.GetComponentInParent<Button>(true);bool inButton=button;
            if(font&&fontStyled.Add(text.GetInstanceID()))
            {
                text.font=font;text.fontSharedMaterial=fontMaterial;text.extraPadding=false;
                bool title=Has(text.name,"title","heading","name","score","time","level","remaining");
                text.fontStyle=title||inButton?FontStyles.Bold:FontStyles.Normal;
                text.characterSpacing=title ? 0.4f : 0f;text.lineSpacing=4;
                if(inButton){text.enableAutoSizing=true;text.fontSizeMin=14;text.fontSizeMax=24;text.fontSize=22;}
                else if(!text.enableAutoSizing)text.fontSize=Mathf.Clamp(text.fontSize,16,32);
                text.richText=true;
            }
            text.color=KeepOpacity(text,inButton&&Primary(button)&&!Word(button.transform)?Color.white:Ink);
            if(Has(PathOf(text.transform),"NotificationBadge"))
            {text.enableAutoSizing=true;text.fontSizeMin=9;text.fontSizeMax=14;text.fontSize=12;text.margin=Vector4.zero;text.color=KeepOpacity(text,Color.white);}
            // Video subtitles sit over the scene rather than a paper panel.
            bool teaching=TeachingCue(text.transform);
            if(teaching||PendingCue(text.transform)||WorldCue(text.transform)||Has(PathOf(text.transform),"/Subtitles/","/Subtitle/"))
            {
                text.color=KeepOpacity(text,Paper);
                if(subtitleMaterial&&text.fontSharedMaterial!=subtitleMaterial)
                {text.fontSharedMaterial=subtitleMaterial;text.extraPadding=true;text.UpdateMeshPadding();}
                if(teaching){if(!teachingCues.Contains(text))teachingCues.Add(text);LayoutTeachingCue(text);}
                else{if(!sceneSubtitles.Contains(text))sceneSubtitles.Add(text);LayoutSceneSubtitle(text);}
            }
            Mark(text,inButton?"redesigned button typography":"redesigned clean typography");
        }
        static Vector2 PixelUnits(Transform target,Camera camera)
        {
            const float span=1024;var origin=RectTransformUtility.WorldToScreenPoint(camera,target.TransformPoint(Vector3.zero));
            return new Vector2((RectTransformUtility.WorldToScreenPoint(camera,target.TransformPoint(Vector3.right*span))-origin).magnitude/span,(RectTransformUtility.WorldToScreenPoint(camera,target.TransformPoint(Vector3.up*span))-origin).magnitude/span);
        }
        void LayoutTeachingCue(TMP_Text text)
        {
            var content=text.transform.parent as RectTransform;var canvas=text.GetComponentInParent<Canvas>();if(!content||content.name!="Content"||!canvas)return;
            var body=content.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.transform.parent==content&&t.name=="TMP_Text");
            var name=content.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.transform.parent==content&&t.name=="Name");if(!body)return;
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;float density=OriginalMobileControls.PixelScale;var parentUnits=PixelUnits(content,camera);
            if(parentUnits.x<.001f||parentUnits.y<.001f||canvas.pixelRect.width<1)return;
            var viewport=canvas.pixelRect;
            if((!camera||!camera.targetTexture)&&OriginalHudLayout.HasTouch)
            {
                var mobile=GetComponent<OriginalMobileControls>();var safe=mobile&&mobile.SafeScreenRect.width>0?mobile.SafeScreenRect:Screen.safeArea;
                var intersection=Rect.MinMaxRect(Mathf.Max(viewport.xMin,safe.xMin),Mathf.Max(viewport.yMin,safe.yMin),Mathf.Min(viewport.xMax,safe.xMax),Mathf.Min(viewport.yMax,safe.yMax));
                if(intersection.width>0&&intersection.height>0)viewport=intersection;
            }
            if(!cueRestingY.ContainsKey(content.GetInstanceID()))cueRestingY[content.GetInstanceID()]=content.anchoredPosition.y;
            float Width(TMP_Text label,float cssSize,float padding)
            {
                var units=PixelUnits(label.transform,camera);if(units.x<.001f||units.y<.001f)return 0;
                if(!cuePositions.ContainsKey(label.GetInstanceID()))cuePositions[label.GetInstanceID()]=label.rectTransform.anchoredPosition;
                label.enableAutoSizing=false;label.fontSize=cssSize*density/units.y;label.textWrappingMode=TextWrappingModes.Normal;
                float width=Mathf.Max(1,viewport.width-40*density)/units.x;label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
                float height=label.GetPreferredValues(label.text,width,Mathf.Infinity).y+padding*density/units.y;label.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,height);return height*units.y;
            }
            float bodyHeight=Width(body,20,8),nameHeight=name?Width(name,28,4):0;if(bodyHeight<=0)return;
            body.rectTransform.anchoredPosition=cuePositions[body.GetInstanceID()];
            // A landscape notch can make the safe viewport asymmetric. Center the
            // children within that viewport while the native Content keeps moving.
            float centerX=RectTransformUtility.WorldToScreenPoint(camera,body.transform.TransformPoint(body.rectTransform.rect.center)).x;
            float horizontalOffset=(viewport.center.x-centerX)/parentUnits.x;
            body.rectTransform.anchoredPosition+=Vector2.right*horizontalOffset;
            float nameOffset=(bodyHeight/2+10*density+nameHeight/2)/parentUnits.y;
            if(name)name.rectTransform.anchoredPosition=new Vector2(cuePositions[name.GetInstanceID()].x+horizontalOffset,body.rectTransform.anchoredPosition.y+nameOffset);
            var rule=content.Find("Whiteline") as RectTransform;
            if(rule)
            {
                if(!cuePositions.ContainsKey(rule.GetInstanceID()))cuePositions[rule.GetInstanceID()]=rule.anchoredPosition;
                var units=PixelUnits(rule,camera);if(units.x>.001f&&units.y>.001f){rule.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,Mathf.Max(1,viewport.width-40*density)/units.x);rule.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,density/units.y);}
                var p=cuePositions[rule.GetInstanceID()];p.x+=horizontalOffset;p.y=body.rectTransform.anchoredPosition.y+(bodyHeight/2+5*density)/parentUnits.y;rule.anchoredPosition=p;
            }
            // Preserve the parent's authored entrance animation. A constant offset of
            // its children fits the settled caption on short viewports without moving
            // the bound Content transform or forcing an early active/fade state.
            float restY=RectTransformUtility.WorldToScreenPoint(camera,body.transform.position).y+(cueRestingY[content.GetInstanceID()]-content.anchoredPosition.y)*parentUnits.y;
            float low=restY-bodyHeight/2,high=restY+bodyHeight/2+(name?10*density+nameHeight:0),offset=0;
            float bottom=viewport.yMin+16*density,top=viewport.yMax-16*density;
            if(low<bottom)offset=bottom-low;else if(high>top)offset=top-high;
            if(Mathf.Abs(offset)>.001f){float local=offset/parentUnits.y;body.rectTransform.anchoredPosition+=Vector2.up*local;if(name)name.rectTransform.anchoredPosition+=Vector2.up*local;if(rule)rule.anchoredPosition+=Vector2.up*local;}
            float horizontal=(viewport.center.x-RectTransformUtility.WorldToScreenPoint(camera,body.transform.position).x)/parentUnits.x;
            body.rectTransform.anchoredPosition+=Vector2.right*horizontal;if(name)name.rectTransform.anchoredPosition+=Vector2.right*horizontal;if(rule)rule.anchoredPosition+=Vector2.right*horizontal;
        }
        void LayoutSceneSubtitle(TMP_Text text)
        {
            var canvas=text.GetComponentInParent<Canvas>();var parent=text.rectTransform.parent as RectTransform;
            if(!canvas||!parent||!(canvas.name is "Subtitles" or "Subtitle")||canvas.pixelRect.width<1)return;
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            // Width and font size use the text's own local units. A wide span also
            // avoids subtracting nearly equal world positions on tiny camera canvases.
            const float span=1024;
            var origin=RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint(Vector3.zero));
            float xUnit=(RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint(Vector3.right*span))-origin).magnitude/span;
            float yUnit=(RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint(Vector3.up*span))-origin).magnitude/span;
            if(xUnit<.001f||yUnit<.001f)return; // Overlay Canvas may not be driven yet.
            float density=OriginalMobileControls.PixelScale,cssUnitY=density/yUnit;
            int id=text.GetInstanceID();if(!subtitleFontSizes.TryGetValue(id,out var baseline)){baseline=text.fontSize;subtitleFontSizes[id]=baseline;}
            text.enableAutoSizing=false;text.fontSize=Mathf.Max(baseline,18*cssUnitY);text.textWrappingMode=TextWrappingModes.Normal;
            // Work in the authored text's local coordinates. Do not move or reparent the
            // caption: original Timeline paths, anchored Y, text and alpha stay intact.
            float width=Mathf.Max(1,canvas.pixelRect.width-32*density)/xUnit;
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal,width);
            float height=text.GetPreferredValues(text.text,width,Mathf.Infinity).y+12*cssUnitY;
            text.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,Mathf.Max(50,height));
        }
        void StyleLegacyText(Text text)
        {
            if(IsOriginalMiniGame(text.transform)){if(legacyFont)text.font=legacyFont;Mark(text,"original minigame typography");return;}
            var button=text.GetComponentInParent<Button>(true);if(legacyFont)text.font=legacyFont;text.material=null;
            text.fontSize=Mathf.Clamp(text.fontSize,16,30);text.color=KeepOpacity(text,button&&Primary(button)?Color.white:Ink);
            text.fontStyle=button?FontStyle.Bold:FontStyle.Normal;Mark(text,"redesigned legacy typography");
        }
        string Subject(Graphic g)
        {
            string p=PathOf(g.transform).ToLowerInvariant(),n=g.name.ToLowerInvariant(),texture=OriginalTexture(g).ToLowerInvariant();
            if(Has(p,"playarea"))
            {
                if(Has(p,"bacteriaprefab")||Has(texture,"bacteria"))return "bacteria";
                if(Has(p,"helpfulcellprefab"))return "red-cell";
                if(Has(p,"obstacleprefab"))return "obstacle";
                if(Has(n,"oxygendeposit"))return "deposit";
                if(Has(p,"oxygenprefab")||Has(texture,"oxygen")&&!Has(texture,"redblood"))return "oxygen";
                if(Has(p,"playercharacter")||Has(texture,"redbloodcell","wbc"))return Has(p,"whiteblood")&&!Has(texture,"redblood")?"white-cell":"red-cell";
            }
            if(Has(p,"joystick"))return Has(n,"handle")?"knob":"joystick";
            if(Has(p,"questeffect","compassmarker","objectivebutton"))return "task";
            return null;
        }
        string OriginalIcon(Graphic g)
        {
            string value=OriginalTexture(g).ToLowerInvariant();
            if(Has(value,"outfitchange"))return "avatar";
            if(Has(value,"resetpov"))return "reset";
            if(Has(value,"3rdpov"))return "camera";
            if(Has(value,"icon_settings"))return "settings";
            if(Has(value,"icon_close","btn_red_close"))return "close";
            if(Has(value,"icon_check"))return "check";
            if(Has(value,"instructions"))return "help";
            if(Has(value,"knowledgemap")&&!Has(value,"background"))return "map";
            if(value=="chat"||value=="gpt")return "chat";
            if(Has(value,"jump"))return "jump";
            if(value=="hand"||Has(value,"handpointer","ui_arrow","touch"))return "point";
            if(Has(value,"icon_up2"))return "up";if(Has(value,"icon_down2"))return "down";
            if(Has(value,"notification_mainobj","circularprogress"))return "task";
            if(Has(value,"continueicon","btn_yellow_next"))return "next";
            if(Has(value,"speedicon"))return "time";
            if(Has(value,"joystick"))return "joystick";
            return null;
        }
        static bool StoryGraphic(Graphic g)=>Has(PathOf(g.transform),"BlackInBlackOut","BlackinTransitoin","BlackInOutImage","ScreenFade","CutoutCircle","Distortion")||
            g.material&&Has(g.material.name,"CutoutCircle","Distortion");
        bool Diagram(Graphic g)=>Has(OriginalTexture(g),"WGCell","KnowledgeMapBackground","KnowledgeMap_","CellIllustration");
        bool Retired(Graphic g)=>coverage.TryGetValue(g.GetInstanceID(),out var r)&&r=="replaced original button ornament";
        void StyleImage(Image image)
        {
            if(IsOriginalMiniGame(image.transform)){Mark(image,"original minigame asset / preserved");return;}
            if(IsOwned(image.transform)){if(!HasStyled(image))Mark(image,"owned runtime interface surface");return;}
            if(Retired(image)||image.GetComponentInParent<Button>(true))return;
            if(StoryGraphic(image)){Mark(image,"original story fade / cutout");return;}
            if(image.color.a<=.001f&&!image.sprite){Mark(image,"transparent input / layout receiver");return;}
            if(PendingCue(image.transform)){image.sprite=OriginalUiIcons.Get("time");image.overrideSprite=null;image.type=Image.Type.Simple;image.preserveAspect=true;image.color=KeepOpacity(image,Paper);image.material=null;Mark(image,"new light scene-overlay waiting icon");return;}
            string subject=Subject(image)??OriginalIcon(image);
            if(subject!=null){image.sprite=OriginalUiIcons.Get(subject);image.overrideSprite=null;image.type=Image.Type.Simple;image.preserveAspect=!Has(PathOf(image.transform),"PlayArea");image.color=KeepOpacity(image,Has(subject,"bacteria")?Rust:Has(subject,"oxygen","deposit")?Teal:Ink);image.material=null;Mark(image,"new sketch subject / "+subject);return;}
            if(Diagram(image)){image.material=null;image.color=KeepOpacity(image,Color.white);Mark(image,"educational illustration / preserved meaning");return;}
            int id=image.GetInstanceID();if(!sourceColors.ContainsKey(id))sourceColors[id]=image.color;
            string name=image.name.ToLowerInvariant(),path=PathOf(image.transform).ToLowerInvariant();
            bool narrow=image.rectTransform.rect.height<=5||image.rectTransform.rect.width<=5;
            bool fill=image.type==Image.Type.Filled||Has(name,"fill","handle","checkmark");
            if(Has(name,"checkmark")){image.sprite=OriginalUiIcons.Get("check");image.preserveAspect=true;image.color=Teal;image.material=null;Mark(image,"new toggle check icon");return;}
            if(Has(name,"handle")){image.sprite=OriginalUiIcons.Get("knob");image.overrideSprite=null;image.type=Image.Type.Simple;image.preserveAspect=true;image.color=KeepOpacity(image,Teal);image.material=null;Mark(image,"new slider / scrollbar knob");return;}
            bool slot=Has(name,"dropslot","dropzone","blank","slot")||image.GetComponent<OriginalWordSlot>();
            image.sprite=slot?drop:panel;image.overrideSprite=null;image.material=null;
            if(image.type!=Image.Type.Filled)image.type=Image.Type.Sliced;
            bool blackout=Has(name,"blackinblackout","fadeoverlay","blackout")||Has(path,"blackinblackout");
            if(blackout){image.sprite=line;image.color=sourceColors[id];Mark(image,"story transition overlay / original alpha");return;}
            if(narrow){image.sprite=line;image.color=new Color(Teal.r,Teal.g,Teal.b,.28f);Mark(image,"new ink divider");}
            else if(fill){image.color=KeepOpacity(image,Teal);Mark(image,"new progress / slider treatment");}
            else if(Has(name,"outerwall")){image.color=KeepOpacity(image,new Color(.55f,.69f,.66f));Mark(image,"new arena boundary");}
            else {image.color=KeepOpacity(image,Color.white);Mark(image,slot?"new answer drop slot":"new paper panel / decoration");}
        }
        void StyleRaw(RawImage raw)
        {
            if(IsOriginalMiniGame(raw.transform)){Mark(raw,"original minigame asset / preserved");return;}
            if(IsOwned(raw.transform)||Retired(raw)||raw.GetComponentInParent<Button>(true))return;
            if(StoryGraphic(raw)){Mark(raw,"original story fade / cutout");return;}
            if(raw.texture is RenderTexture||raw.GetComponentInParent<UnityEngine.Video.VideoPlayer>(true)||Has(PathOf(raw.transform),"VideoCanvas","VideoPlayer"))
            {Mark(raw,"original video / live preview content");return;}
            if(Diagram(raw)){raw.color=KeepOpacity(raw,Color.white);raw.material=null;Mark(raw,"educational illustration / preserved meaning");return;}
            string subject=Subject(raw)??OriginalIcon(raw);
            raw.texture=subject!=null?OriginalUiIcons.Get(subject).texture:panel.texture;
            raw.uvRect=new Rect(0,0,1,1);raw.color=KeepOpacity(raw,subject!=null?Ink:Color.white);raw.material=null;
            Mark(raw,subject!=null?"new sketch subject / "+subject:"new paper image surface");
        }
        void StyleWorldMarkers()
        {
            foreach(var mesh in FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(r=>r.gameObject.scene==gameObject.scene&&r.name=="Holo_Ring"&&Has(PathOf(r.transform),"QuestEffect")))
            {
                mesh.enabled=false;
                foreach(var label in mesh.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.text.Trim()=="?"))
                {label.enabled=false;if(label is Graphic graphic)Mark(graphic,"replaced world quest glyph");}
                if(mesh.transform.Find(Own+"QuestBadge"))continue;
                var badge=new GameObject(Own+"QuestBadge");badge.transform.SetParent(mesh.transform,false);
                float scale=Mathf.Max(.001f,mesh.transform.lossyScale.x);badge.transform.localScale=Vector3.one*(1.15f/scale);
                var back=badge.AddComponent<SpriteRenderer>();back.sprite=panel;back.color=Color.white;back.sortingOrder=30;
                var symbol=new GameObject(Own+"QuestGlyph");symbol.transform.SetParent(badge.transform,false);symbol.transform.localPosition=new Vector3(0,0,-.01f);symbol.transform.localScale=Vector3.one*.47f;
                var glyph=symbol.AddComponent<SpriteRenderer>();glyph.sprite=OriginalUiIcons.Get("task");glyph.color=Teal;glyph.sortingOrder=31;
                badge.AddComponent<OriginalUiWorldBadge>();
            }
        }
        void LateUpdate(){if(Time.unscaledTime>=next){next=Time.unscaledTime+.5f;Refresh();}LayoutCaptions();}
        public void Hint(string label,RectTransform anchor)
        {
            hint=label;if(string.IsNullOrEmpty(label)||!anchor)return;
            var corners=new Vector3[4];anchor.GetWorldCorners(corners);var canvas=anchor.GetComponentInParent<Canvas>();var camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var point=RectTransformUtility.WorldToScreenPoint(camera,corners[1]);hintAnchor=new Rect(point.x,Screen.height-point.y,anchor.rect.width,1);
        }
        void OnGUI()
        {
            if(string.IsNullOrEmpty(hint))return;
            var style=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleCenter};style.normal.textColor=Paper;
            float width=Mathf.Max(110,style.CalcSize(new GUIContent(hint)).x+28);var rect=new Rect(Mathf.Clamp(hintAnchor.x-width*.25f,8,Screen.width-width-8),Mathf.Max(8,hintAnchor.y-42),width,32);
            var old=GUI.color;GUI.color=Teal;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;GUI.Label(rect,hint,style);
        }
        void OnDestroy()
        {
            // The private definition borrows the imported atlas. TMP_FontAsset's
            // destructor destroys its atlas/material references, so release those
            // borrowed links before disposing the definition and our own material.
            if(font){font.atlasTextures=Array.Empty<Texture2D>();font.material=null;Destroy(font);}
            if(fontMaterial)Destroy(fontMaterial);if(subtitleMaterial)Destroy(subtitleMaterial);
            foreach(var sprite in sprites)if(sprite)Destroy(sprite);foreach(var texture in textures)if(texture)Destroy(texture);
            // Icon cache is shared with the IMGUI presenter and is owned by this scene.
            OriginalUiIcons.DisposeAll();
        }
    }
    public sealed class OriginalUiHint : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,ISelectHandler,IDeselectHandler
    {
        public OriginalUiTheme theme;public string label;
        public void OnPointerEnter(PointerEventData e){if(theme)theme.Hint(label,transform as RectTransform);}
        public void OnPointerExit(PointerEventData e){if(theme)theme.Hint(null,null);}
        public void OnSelect(BaseEventData e){if(theme)theme.Hint(label,transform as RectTransform);}
        public void OnDeselect(BaseEventData e){if(theme)theme.Hint(null,null);}
        void OnDisable(){if(theme)theme.Hint(null,null);}
    }
    public sealed class OriginalUiWorldBadge : MonoBehaviour
    {
        void LateUpdate(){var camera=Camera.main;if(camera)transform.rotation=camera.transform.rotation;}
    }
}
