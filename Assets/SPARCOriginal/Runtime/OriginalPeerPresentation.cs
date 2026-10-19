using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Motion and field-note dressing for the peer-explanation step: the player's note card,
    /// Cap's thinking bubble and Cap's streamed reply. OriginalActivityUi lays the native widgets out
    /// every frame; this runs after it and after the theme, and only adds owned graphics, alpha, scale,
    /// and the reply's visible-character count. Native text, callbacks and the agent contract stay as they are.</summary>
    [DefaultExecutionOrder(2100)]
    public sealed class OriginalPeerPresentation:MonoBehaviour
    {
        public OriginalRuntime runtime;
        const string Own="__SPARC_UI_";
        /// <summary>Cap's hue matches the interaction layer's name chip.</summary>
        static readonly Color CapHue=new(.95f,.62f,.16f),ChipHue=new(.88f,.5f,.1f),Coral=new(.97f,.38f,.27f),Mint=new(.43f,.77f,.68f);
        static readonly string[] Thoughts={"is thinking","is reading your note","is connecting the dots","is writing back"},ShortThoughts={"is thinking","is reading","is pondering","is writing"};
        Canvas[] canvases=Array.Empty<Canvas>();
        readonly Dictionary<int,Shown> shown=new();
        float scrim;
        public bool Revealing=>reply!=null&&reply.active&&!reply.done;
        public int RevealedCharacters=>reply?.count??0;

        sealed class Shown {public bool active;public float at;}
        sealed class Chip {public Image streak;public TMP_Text label,shadow,kicker;public float end;}
        sealed class Note
        {
            public RectTransform shadow;public Image scrim,pencil,surface,focus;public TMP_Text counter;public float focused,pop;public bool ready;
        }
        sealed class Thinking
        {
            public RectTransform root,shadow,track,segment;public Image card,scrim;public OriginalPeerTail tail;public Chip chip;public TMP_Text status;public Image[] dots;
        }
        sealed class Reply
        {
            public Canvas canvas;public RectTransform panel,shadow,stamp;public Image scrim,accent,caret,stampFrame;public OriginalPeerTail tail;public Chip chip;public OriginalUiCurve rule;public TMP_Text stampLabel;
            public TMP_Text body;public string text;public int count,total;public float carry,hold,doneAt,started;public bool active,done,instant,celebrated,stampOnTop;public Mood mood;
            public readonly List<Confetto> confetti=new();
        }
        sealed class Confetto {public Image image;public Vector2 position,velocity,size;public float spin,angle,life,age;}
        enum Mood {Asks,Success,ChoseA,Error,Fried}
        readonly Dictionary<int,Note> notes=new();Thinking thinking;Reply reply;

        public void Initialize()
        {
            canvases=FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None)
                .Where(c=>c.gameObject.scene==runtime.gameObject.scene&&c.name is "PlayerInputField" or "LAMInputField" or "WaitForResponse" or "AnswerField" or "LAMAnswerField").ToArray();
        }
        void Start(){if(canvases.Length==0)Initialize();}

        void LateUpdate()
        {
            if(!runtime||!runtime.Started||runtime.ShuttingDown)return;
            bool any=false;
            foreach(var canvas in canvases)
            {
                if(!canvas)continue;bool active=canvas.isActiveAndEnabled;any|=active;
                int id=canvas.GetInstanceID();if(!shown.TryGetValue(id,out var state))shown[id]=state=new Shown();
                if(active&&!state.active)state.at=Time.unscaledTime;state.active=active;
                if(!active)continue;float age=Time.unscaledTime-state.at,unit=Unit(canvas);
                if(canvas.name is "PlayerInputField" or "LAMInputField")NoteCard(canvas,age,unit);
                else if(canvas.name=="WaitForResponse")ThinkingBubble(canvas,age,unit);
                else ReplyBubble(canvas,age,unit,state.at);
            }
            // One shared level, so handing over between the three canvases never flashes the scrim.
            scrim=Mathf.MoveTowards(scrim,any?1:0,Time.unscaledDeltaTime*3);
            if(reply!=null&&reply.active&&!(reply.canvas&&reply.canvas.isActiveAndEnabled))reply.active=false;
        }
        static float Unit(Canvas canvas)=>OriginalMobileControls.PixelScale/Mathf.Max(.1f,canvas.scaleFactor);
        static float Out(float t){t=Mathf.Clamp01(t);return 1-(1-t)*(1-t)*(1-t);}
        static float Back(float t){t=Mathf.Clamp01(t);const float c=1.6f;return 1+(c+1)*Mathf.Pow(t-1,3)+c*Mathf.Pow(t-1,2);}
        /// <summary>Entrance: fade with a small spring in scale. Scale only, around the card's centre, so
        /// a docked card never leaves the safe viewport while it settles.</summary>
        static float Pop(float age)=>Mathf.Lerp(.9f,1,Back(age/.42f));
        static float Fade(float age)=>Mathf.Lerp(.02f,1,Out(age/.24f));
        /// <summary>Box() places widgets with a top-left pivot. This multiplies a widget's scale by a factor
        /// about any world point (its own centre, or the card's for a group), whatever its parent.</summary>
        static void ScaleAbout(RectTransform r,Vector3 center,float factor)
        {
            if(!r||Mathf.Approximately(factor,1))return;var s=r.localScale;r.localScale=new Vector3(s.x*factor,s.y*factor,s.z);r.position=center+(r.position-center)*factor;
        }
        static Vector3 Center(RectTransform r)=>r.TransformPoint(r.rect.center);
        /// <summary>Box() with a centred pivot, for widgets that rotate or scale about themselves.</summary>
        static void BoxCentered(RectTransform r,float x,float y,float w,float h,float unit)
        {OriginalGameArt.Box(r,x,y,w,h,unit);r.pivot=new Vector2(.5f,.5f);r.anchoredPosition+=new Vector2(w/2,-h/2)*unit;}
        static void Follow(RectTransform target,RectTransform source,float inflate,float down,float unit)
        {
            target.anchorMin=source.anchorMin;target.anchorMax=source.anchorMax;target.pivot=new Vector2(0,1);target.localRotation=Quaternion.identity;target.localScale=Vector3.one;
            var size=source.rect.size;var topLeft=source.anchoredPosition+new Vector2(-size.x*source.pivot.x,size.y*(1-source.pivot.y));
            target.sizeDelta=size+Vector2.one*inflate*2*unit;target.anchoredPosition=topLeft+new Vector2(-inflate,inflate-down)*unit;
        }
        static RectTransform Before(string name,RectTransform sibling)
        {
            var parent=sibling.parent;var r=OriginalGameArt.Rect(name,parent);int index=sibling.GetSiblingIndex();
            if(r.GetSiblingIndex()!=index-1)r.SetSiblingIndex(r.GetSiblingIndex()<index?index-1:index);return r;
        }
        static Image Scrim(Canvas canvas,float level,float age)
        {
            var image=OriginalGameArt.Image(Own+"PeerScrim",canvas.transform,OriginalPeerArt.Scrim,Color.black);image.type=Image.Type.Simple;
            if(image.transform.GetSiblingIndex()!=0)image.transform.SetAsFirstSibling();
            var r=image.rectTransform;r.anchorMin=Vector2.zero;r.anchorMax=new Vector2(1,.62f);r.pivot=new Vector2(.5f,0);r.offsetMin=r.offsetMax=Vector2.zero;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;
            image.color=new Color(0,0,0,.5f*Mathf.Max(level,Out(age/.3f)));return image;
        }
        static Image Shadow(RectTransform shadow,RectTransform card,float alpha,float unit)
        {
            var image=shadow.GetComponent<Image>()??shadow.gameObject.AddComponent<Image>();image.sprite=OriginalPeerArt.Shadow;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=1/Mathf.Max(.1f,unit);image.raycastTarget=false;
            image.color=new Color(0,0,0,.34f*alpha);Follow(shadow,card,14,-7,unit);return image;
        }
        static TMP_Text Label(string name,Transform parent,float size,bool bold,Color color)
        {
            var t=OriginalGameArt.Text(name,parent,size,color);t.fontStyle=bold?FontStyles.Bold:FontStyles.Normal;t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;
            t.alignment=TextAlignmentOptions.MidlineLeft;t.margin=Vector4.zero;t.richText=false;t.characterSpacing=0;t.enableAutoSizing=false;t.raycastTarget=false;return t;
        }
        static void Place(TMP_Text t,float x,float y,float w,float h,float size,float unit,Color color)
        {OriginalGameArt.Box(t.rectTransform,x,y,w,h,unit);t.fontSize=size*unit;t.color=color;t.enableAutoSizing=false;t.margin=Vector4.zero;}
        static Chip MakeChip(Transform parent)
        {
            var chip=new Chip{streak=OriginalGameArt.Image(Own+"PeerChipStreak",parent,OriginalPeerArt.Brush,ChipHue)};chip.streak.type=Image.Type.Simple;
            chip.shadow=Label(Own+"PeerChipNameShadow",parent,20,true,new Color(0,0,0,.4f));chip.label=Label(Own+"PeerChipName",parent,20,true,Color.white);
            chip.kicker=Label(Own+"PeerChipKicker",parent,15,false,OriginalUiTheme.Muted);return chip;
        }
        /// <summary>The guide's name on an amber dry-brush stroke that paints in from the left, as the
        /// world's Talk chip does, with a kicker after it that says what Cap is doing. Returns nothing;
        /// chip.end is where the stroke stops, in the parent's CSS pixels.</summary>
        static void LayoutChip(Chip chip,string name,string kicker,Color kickerColor,float x,float y,float right,float age,float unit,float breathe)
        {
            chip.label.text=chip.shadow.text=name;chip.label.fontSize=20*unit;float labelW=chip.label.GetPreferredValues(name,9999,99).x/unit;
            float sweep=Out((age-.08f)/.38f),text=Mathf.Clamp01((age-.22f)/.25f),full=labelW+48,length=Mathf.Max(2,full*sweep+breathe);
            OriginalGameArt.Box(chip.streak.rectTransform,x-4,y,length,40,unit);chip.streak.color=ChipHue;chip.end=x-4+full;
            chip.label.overflowMode=chip.shadow.overflowMode=TextOverflowModes.Overflow;
            Place(chip.shadow,x+11.5f,y+6,labelW+30,30,20,unit,new Color(.35f,.16f,0,.55f*text));Place(chip.label,x+10,y+4.5f,labelW+30,30,20,unit,new Color(1,1,1,text));
            chip.kicker.text=kicker;chip.kicker.fontStyle=FontStyles.Italic;Place(chip.kicker,chip.end+6,y+6,Mathf.Max(40,right-chip.end-6),28,15,unit,new Color(kickerColor.r,kickerColor.g,kickerColor.b,text));
        }

        // ---- The player's note card -------------------------------------------------------------
        void NoteCard(Canvas canvas,float age,float unit)
        {
            var field=canvas.GetComponentInChildren<TMP_InputField>(true);var card=canvas.transform.Find(Own+"PeerCard") as RectTransform;if(!field||!card)return;
            int id=canvas.GetInstanceID();if(!notes.TryGetValue(id,out var note))notes[id]=note=new Note();
            note.scrim=Scrim(canvas,scrim,age);
            var cardImage=card.GetComponent<Image>();if(cardImage){cardImage.sprite=OriginalPeerArt.Card;cardImage.type=Image.Type.Sliced;cardImage.pixelsPerUnitMultiplier=1/Mathf.Max(.1f,unit);}
            note.shadow=Before(Own+"PeerCardShadow",card);Shadow(note.shadow,card,Fade(age),unit);
            note.pencil=OriginalGameArt.Image(Own+"PeerPencil",card,OriginalUiIcons.Get("pencil"),new Color(OriginalUiTheme.Ink.r,OriginalUiTheme.Ink.g,OriginalUiTheme.Ink.b,.8f));note.pencil.type=Image.Type.Simple;note.pencil.preserveAspect=true;
            OriginalGameArt.Box(note.pencil.rectTransform,17,17,22,22,unit);
            // The field's inset well; its teal underline grows while the field has focus.
            var corners=new Vector3[4];((RectTransform)field.transform).GetWorldCorners(corners);
            Vector2 low=card.InverseTransformPoint(corners[0]),high=card.InverseTransformPoint(corners[2]);
            var fieldSize=new Vector2(high.x-low.x,high.y-low.y)/unit;
            note.surface=field.targetGraphic as Image??field.GetComponent<Image>();
            if(note.surface){note.surface.sprite=OriginalPeerArt.Card;note.surface.overrideSprite=null;note.surface.type=Image.Type.Sliced;note.surface.pixelsPerUnitMultiplier=1/Mathf.Max(.1f,unit);note.surface.color=new Color(.945f,.94f,.9f);}
            note.focused=Mathf.MoveTowards(note.focused,field.isFocused?1:0,Time.unscaledDeltaTime*5);
            note.focus=OriginalGameArt.Image(Own+"PeerFieldFocus",field.transform,null,OriginalUiTheme.Teal);float reach=Out(note.focused);
            if(note.focus.transform.GetSiblingIndex()!=field.transform.childCount-1)note.focus.transform.SetAsLastSibling();
            var bar=note.focus.rectTransform;bar.anchorMin=bar.anchorMax=bar.pivot=new Vector2(0,0);bar.localScale=Vector3.one;bar.localRotation=Quaternion.identity;
            bar.sizeDelta=new Vector2(Mathf.Max(0,(fieldSize.x-24)*reach),2.5f)*unit;bar.anchoredPosition=new Vector2(12+(fieldSize.x-24)*(1-reach)/2,3)*unit;
            note.focus.color=new Color(OriginalUiTheme.Teal.r,OriginalUiTheme.Teal.g,OriginalUiTheme.Teal.b,reach);
            note.counter=Label(Own+"PeerCounter",card,13,false,OriginalUiTheme.Muted);note.counter.alignment=TextAlignmentOptions.MidlineRight;
            int length=field.text?.Length??0,limit=field.characterLimit>0?field.characterLimit:2000;note.counter.text=length+" / "+limit;
            var counterColor=length>=limit*.9f?OriginalUiTheme.Rust:OriginalUiTheme.Muted;
            Place(note.counter,card.rect.width/unit-176,16,160,24,13,unit,new Color(counterColor.r,counterColor.g,counterColor.b,length>0?1:.7f));
            // Send wakes with a small pop the moment there is something to send.
            var send=canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b=>b.name is "Send" or Own+"PeerSend");
            if(send)
            {
                bool ready=send.interactable;if(ready&&!note.ready)note.pop=1;note.ready=ready;note.pop=Mathf.MoveTowards(note.pop,0,Time.unscaledDeltaTime*4);
                var press=OriginalComponents.Ensure<OriginalPeerPress>(send.gameObject);var r=(RectTransform)send.transform;
                float scale=(1+.09f*Mathf.Sin(note.pop*Mathf.PI))*press.Scale(ready);ScaleAbout(r,Center(r),scale);
            }
            // Entrance: everything the layout placed on this canvas springs in together.
            var group=OriginalComponents.Ensure<CanvasGroup>(canvas.gameObject);group.alpha=Fade(age);
            float pop=Pop(age);var middle=Center(card);foreach(var r in new[]{card,note.shadow,(RectTransform)field.transform,canvas.transform.Find(Own+"PeerHeading") as RectTransform,send?(RectTransform)send.transform:null})ScaleAbout(r,middle,pop);
        }

        // ---- Cap thinking ----------------------------------------------------------------------
        void ThinkingBubble(Canvas canvas,float age,float unit)
        {
            var p=canvas.transform.Find("P") as RectTransform;if(!p)return;
            // The native cue keeps its place and text for the source contract; the bubble draws over it.
            foreach(var g in p.GetComponentsInChildren<Graphic>(true))if(g.enabled)g.enabled=false;
            if(thinking==null||!thinking.root)
            {
                var t=thinking=new Thinking();t.root=OriginalGameArt.Rect(Own+"PeerThinking",canvas.transform);
                t.shadow=OriginalGameArt.Rect(Own+"PeerThinkingShadow",t.root);
                t.card=OriginalGameArt.Image(Own+"PeerThinkingCard",t.root,OriginalPeerArt.Card,OriginalUiTheme.Paper);t.card.type=Image.Type.Sliced;
                t.tail=OriginalPeerTail.Make(Own+"PeerThinkingTail",t.root);
                t.chip=MakeChip(t.root);t.status=Label(Own+"PeerThinkingStatus",t.root,17,false,OriginalUiTheme.Ink);
                t.dots=Enumerable.Range(0,3).Select(i=>OriginalGameArt.Image(Own+"PeerThinkingDot"+i,t.root,OriginalInteractionArt.Disc,OriginalUiTheme.Teal)).ToArray();
                var track=OriginalGameArt.Image(Own+"PeerThinkingTrack",t.root,null,new Color(OriginalUiTheme.Ink.r,OriginalUiTheme.Ink.g,OriginalUiTheme.Ink.b,.1f));t.track=track.rectTransform;OriginalComponents.Ensure<RectMask2D>(track.gameObject);
                t.segment=OriginalGameArt.Image(Own+"PeerThinkingSweep",t.track,null,CapHue).rectTransform;
            }
            var th=thinking;th.scrim=Scrim(canvas,scrim,age);
            Follow(th.root,p,0,0,unit);float w=th.root.rect.width/unit,h=th.root.rect.height/unit;
            OriginalGameArt.Box(th.card.rectTransform,0,0,w,h,unit);th.card.pixelsPerUnitMultiplier=1/Mathf.Max(.1f,unit);th.card.color=OriginalUiTheme.Paper;
            Shadow(th.shadow,th.card.rectTransform,Fade(age),unit);
            th.tail.Layout(w/2,0,30,14,unit,OriginalUiTheme.Paper);
            float breathe=4*Mathf.Sin(Time.unscaledTime*2.2f);
            string name=GuideName(p.GetComponentInChildren<TMP_Text>(true)?.text,"Cap");
            LayoutChip(th.chip,name,"",OriginalUiTheme.Muted,18,14,w-18,age,unit,breathe);
            // Status phrases take turns; each one fades through its slot.
            float statusX=th.chip.end+8,statusW=Mathf.Max(40,w-statusX-58);var thoughts=statusW<190?ShortThoughts:Thoughts;
            string phrase=age>14?"is thinking hard":thoughts[Mathf.FloorToInt(age/2.6f)%thoughts.Length];float slot=age>14?1:(age%2.6f)/2.6f;
            float phraseAlpha=Mathf.Clamp01((age-.3f)/.25f)*(age>14?1:Mathf.Clamp01(slot/.1f)*Mathf.Clamp01((1-slot)/.1f));
            th.status.text=phrase;Place(th.status,statusX,14,statusW,40,17,unit,new Color(OriginalUiTheme.Ink.r,OriginalUiTheme.Ink.g,OriginalUiTheme.Ink.b,phraseAlpha));
            float textW=Mathf.Min(statusW,th.status.GetPreferredValues(phrase,9999,99).x/unit);
            for(int i=0;i<th.dots.Length;i++)
            {
                float wave=Mathf.Max(0,Mathf.Sin(Time.unscaledTime*6.5f-i*.7f));float lift=wave*5;
                OriginalGameArt.Box(th.dots[i].rectTransform,statusX+textW+8+i*12,30-lift,7,7,unit);
                var c=Color.Lerp(OriginalUiTheme.Teal,CapHue,wave);th.dots[i].color=new Color(c.r,c.g,c.b,(.45f+.55f*wave)*Mathf.Clamp01((age-.35f)/.2f));
            }
            // An unhurried indeterminate sweep along the bottom edge.
            OriginalGameArt.Box(th.track,18,h-15,w-36,4,unit);float run=(w-36)*.34f,phase=(age*.7f)%1,x=Mathf.Lerp(-run,w-36,Mathf.SmoothStep(0,1,phase));
            OriginalGameArt.Box(th.segment,x,0,run,4,unit);th.segment.GetComponent<Image>().color=Color.Lerp(OriginalUiTheme.Teal,ChipHue,Mathf.PingPong(age*.6f,1));
            var group=OriginalComponents.Ensure<CanvasGroup>(canvas.gameObject);group.alpha=Fade(age);
            ScaleAbout(th.root,Center(th.card.rectTransform),Pop(age));
        }
        static string GuideName(string source,string fallback)
        {
            var word=(source??"").Trim().Split(' ').FirstOrDefault(s=>s.Length>0);if(string.IsNullOrEmpty(word))return fallback;
            return char.ToUpperInvariant(word[0])+word.Substring(1).ToLowerInvariant();
        }

        // ---- Cap's reply -------------------------------------------------------------------------
        void ReplyBubble(Canvas canvas,float age,float unit,float shownAt)
        {
            var panel=canvas.transform.Find("GameObject") as RectTransform;if(!panel)return;
            var body=panel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="AF");if(!body)return;
            if(reply==null||reply.panel!=panel)reply=BuildReply(canvas,panel);
            var r=reply;r.canvas=canvas;r.body=body;r.scrim=Scrim(canvas,scrim,age);
            var native=panel.GetComponentsInChildren<TMP_Text>(true).FirstOrDefault(t=>t.name=="Name");if(native&&native.enabled)native.enabled=false;
            var line=panel.Find("GradientWhiteline");if(line)foreach(var g in line.GetComponentsInChildren<Graphic>(true))if(g.enabled)g.enabled=false;
            var card=panel.Find(Own+"FeedbackCard")?.GetComponent<Image>();if(card){card.sprite=OriginalPeerArt.Card;card.type=Image.Type.Sliced;card.pixelsPerUnitMultiplier=1/Mathf.Max(.1f,unit);}
            // A new reply (or the panel reopening) restarts the stream.
            if(!r.active||r.text!=body.text||r.started!=shownAt)Restart(r,body,shownAt);
            r.active=true;
            Stream(r);
            float w=panel.rect.width/unit,h=panel.rect.height/unit;
            r.shadow=Before(Own+"FeedbackShadow",panel);Shadow(r.shadow,panel,Fade(age),unit);
            r.tail.Layout(w/2,0,30,14,unit,OriginalUiTheme.Paper);if(r.tail.transform.GetSiblingIndex()!=panel.childCount-1)r.tail.transform.SetAsLastSibling();
            var (kicker,tone,accent)=Tone(r.mood);
            // OriginalActivityUi sets the header height (shorter on short screens); the body viewport starts there.
            var viewport=panel.Find(Own+"FeedbackScroll") as RectTransform;float header=viewport?-viewport.anchoredPosition.y/unit:74,ruleY=header-10,chipY=Mathf.Max(6,(ruleY-40)/2);
            r.stampOnTop=w>=560;
            LayoutChip(r.chip,GuideName(native?native.text:"","Cap"),kicker,tone,18,chipY,r.mood==Mood.Success&&r.stampOnTop?w-190:w-20,age,unit,r.mood==Mood.Success&&r.done?3*Mathf.Sin(Time.unscaledTime*4):0);
            // A short stripe of the reply's tone runs down the card's left edge.
            float stripe=Out((age-.12f)/.4f);OriginalGameArt.Box(r.accent.rectTransform,1.5f,16+(h-32)*(1-stripe)/2,4,(h-32)*stripe,unit);r.accent.color=new Color(accent.r,accent.g,accent.b,.9f);
            float rule=Out((age-.1f)/.55f);r.rule.color=new Color(OriginalUiTheme.Ink.r,OriginalUiTheme.Ink.g,OriginalUiTheme.Ink.b,.22f);
            OriginalGameArt.Stretch(r.rule.rectTransform);var rr=r.rule.rectTransform.rect;
            Vector2 L(float x,float y)=>new(rr.xMin+x*unit,rr.yMax-y*unit);
            r.rule.Set(L(20,ruleY),L(w*.4f,ruleY+.6f),L(w*.7f,ruleY-.6f),L(w-20,ruleY),1.2f*unit,rule,0);
            Caret(r,unit);Buttons(r,panel,unit);Stamp(r,w,h,unit);Confetti(r,unit);
            // Entrance: the card springs up; an error arrives with a short shake instead.
            var group=OriginalComponents.Ensure<CanvasGroup>(canvas.gameObject);group.alpha=Fade(age);
            var center=Center(panel);ScaleAbout(panel,center,Pop(age));ScaleAbout(r.shadow,center,Pop(age));
            if(r.mood is Mood.Error or Mood.Fried&&age<.55f){float shake=Mathf.Sin(age*48)*7*(1-age/.55f)*unit;panel.anchoredPosition+=Vector2.right*shake;}
            // Tap anywhere on the card to finish the line straight away.
            OriginalComponents.Ensure<OriginalPeerSkip>(panel.gameObject).owner=this;
        }
        Reply BuildReply(Canvas canvas,RectTransform panel)
        {
            var r=new Reply{canvas=canvas,panel=panel};
            r.accent=OriginalGameArt.Image(Own+"FeedbackAccent",panel,null,CapHue);
            var rule=OriginalGameArt.Rect(Own+"FeedbackRule",panel);r.rule=rule.GetComponent<OriginalUiCurve>()??rule.gameObject.AddComponent<OriginalUiCurve>();r.rule.raycastTarget=false;
            r.chip=MakeChip(panel);r.tail=OriginalPeerTail.Make(Own+"FeedbackTail",panel);
            r.stamp=OriginalGameArt.Rect(Own+"FeedbackStamp",panel);r.stampFrame=OriginalGameArt.Image(Own+"FeedbackStampFrame",r.stamp,OriginalPeerArt.Stamp,OriginalUiTheme.Teal);r.stampFrame.type=Image.Type.Sliced;
            r.stampLabel=Label(Own+"FeedbackStampLabel",r.stamp,20,true,OriginalUiTheme.Teal);r.stampLabel.alignment=TextAlignmentOptions.Center;r.stampLabel.text="GOT IT!";
            r.stamp.gameObject.SetActive(false);return r;
        }
        void Restart(Reply r,TMP_Text body,float shownAt)
        {
            r.text=body.text;r.total=r.text?.Length??0;r.count=0;r.carry=r.hold=0;r.done=false;r.celebrated=false;r.started=shownAt;r.doneAt=0;
            var agent=runtime.peerAgent;bool primary=agent&&body.GetComponentInParent<Canvas>(true)?.name=="AnswerField";
            string outcome=primary?agent.Outcome:"";bool unavailable=primary&&agent.ServiceState=="unavailable";
            bool fried=(r.text??"").IndexOf("brain is fried",StringComparison.OrdinalIgnoreCase)>=0;
            r.mood=fried?Mood.Fried:outcome=="B"?Mood.Success:outcome=="A"?Mood.ChoseA:unavailable?Mood.Error:Mood.Asks;
            // Service errors are notices, not speech: they appear whole.
            r.instant=r.mood is Mood.Error or Mood.Fried;if(r.instant){r.count=r.total;}
            foreach(var c in r.confetti)if(c.image)c.image.gameObject.SetActive(false);
            if(r.stamp)r.stamp.gameObject.SetActive(false);
        }
        static (string kicker,Color tone,Color accent) Tone(Mood mood)=>mood switch
        {
            Mood.Success=>("gets it now!",OriginalUiTheme.Teal,OriginalUiTheme.Teal),
            Mood.ChoseA=>("went with A",OriginalUiTheme.Muted,CapHue),
            Mood.Error=>("lost the signal",OriginalUiTheme.Rust,OriginalUiTheme.Rust),
            Mood.Fried=>("is out of ideas",OriginalUiTheme.Rust,OriginalUiTheme.Rust),
            _=>("wants to hear more",OriginalUiTheme.Muted,CapHue)
        };
        /// <summary>Streams the reply at a talking pace (about 60 characters a second, faster for long
        /// replies so none takes more than about two seconds), with short beats after punctuation.</summary>
        void Stream(Reply r)
        {
            if(!r.done)
            {
                float speed=Mathf.Max(60,r.total/2f);
                if(r.hold>0)r.hold-=Time.unscaledDeltaTime;
                else
                {
                    r.carry+=Time.unscaledDeltaTime*speed;
                    while(r.carry>=1&&r.count<r.total)
                    {
                        r.count++;r.carry-=1;char c=r.text[r.count-1];
                        if(c is '.' or '!' or '?' or '\n'&&r.count<r.total){r.hold=.12f;r.carry=0;break;}
                        if(c is ',' or ';' or ':'){r.hold=.05f;r.carry=0;break;}
                    }
                }
                if(r.count>=r.total){r.count=r.total;r.done=true;r.doneAt=Time.unscaledTime;}
            }
            if(!r.done)r.body.maxVisibleCharacters=r.count;
        }
        public void Skip(){if(reply!=null&&reply.active&&!reply.done){reply.count=reply.total;reply.hold=reply.carry=0;}}
        void Caret(Reply r,float unit)
        {
            if(!r.caret){r.caret=OriginalGameArt.Image(Own+"FeedbackCaret",r.body.transform,null,CapHue);}
            var caret=r.caret;if(caret.transform.parent!=r.body.transform)caret.transform.SetParent(r.body.transform,false);
            float fade=r.done?1-Mathf.Clamp01((Time.unscaledTime-r.doneAt)/.35f):1;bool show=!r.instant&&fade>0&&r.total>0;caret.gameObject.SetActive(show);if(!show)return;
            r.body.ForceMeshUpdate();var info=r.body.textInfo;if(info.characterCount==0){caret.gameObject.SetActive(false);return;}
            var ch=info.characterInfo[Mathf.Clamp((r.done?r.total:Mathf.Max(1,r.count))-1,0,info.characterCount-1)];var lineInfo=info.lineInfo[Mathf.Clamp(ch.lineNumber,0,info.lineCount-1)];
            float x=ch.character=='\n'?r.body.rectTransform.rect.xMin:ch.xAdvance;float top=lineInfo.ascender,bottom=lineInfo.descender;
            var c=caret.rectTransform;c.anchorMin=c.anchorMax=new Vector2(.5f,.5f);c.pivot=new Vector2(0,0);c.localScale=Vector3.one;c.localRotation=Quaternion.identity;
            c.sizeDelta=new Vector2(3*unit,top-bottom);c.anchoredPosition=new Vector2(x+2*unit,bottom)-r.body.rectTransform.rect.center;
            // Follow the head of the line when a long reply scrolls.
            var scroll=r.body.GetComponentInParent<ScrollRect>();
            if(scroll&&!r.done)
            {
                float below=r.body.rectTransform.rect.yMax-bottom,view=scroll.viewport.rect.height,content=r.body.rectTransform.rect.height;
                if(content>view+1)scroll.verticalNormalizedPosition=1-Mathf.Clamp01((below-view+8*unit)/(content-view));
            }
            float blink=r.done?fade:.65f+.35f*Mathf.Sin(Time.unscaledTime*14);caret.color=new Color(CapHue.r,CapHue.g,CapHue.b,blink);
        }
        /// <summary>Choices wait for the line, then slide in; until then they are invisible and clicks pass to the card.</summary>
        void Buttons(Reply r,RectTransform panel,float unit)
        {
            float t=r.done?Mathf.Clamp01((Time.unscaledTime-r.doneAt)/.3f):0;
            foreach(var button in panel.GetComponentsInChildren<Button>(true))
            {
                // Hidden and click-through while the line streams; staying interactable avoids a grey disabled-tint flash on arrival.
                var group=OriginalComponents.Ensure<CanvasGroup>(button.gameObject);group.alpha=r.done?Out(t):0;group.interactable=true;group.blocksRaycasts=r.done;
                var rect=(RectTransform)button.transform;rect.anchoredPosition+=Vector2.right*(1-Out(t))*16*unit;
                var press=OriginalComponents.Ensure<OriginalPeerPress>(button.gameObject);ScaleAbout(rect,Center(rect),press.Scale(r.done)*(r.mood==Mood.Success&&r.done?1+.025f*Mathf.Max(0,Mathf.Sin((Time.unscaledTime-r.doneAt)*3)):1));
            }
        }
        /// <summary>A success earns a rubber stamp that thumps onto the card's corner.</summary>
        void Stamp(Reply r,float w,float h,float unit)
        {
            bool show=r.mood==Mood.Success&&r.done;if(r.stamp.gameObject.activeSelf!=show)r.stamp.gameObject.SetActive(show);if(!show)return;
            float t=Time.unscaledTime-r.doneAt-.05f,land=Mathf.Clamp01(t/.2f);
            // Wide cards take the stamp on their top-right corner; narrow ones in the free footer corner.
            if(r.stampOnTop)BoxCentered(r.stamp,w-172,10,148,48,unit);else BoxCentered(r.stamp,20,h-62,Mathf.Min(140,w-226),46,unit);
            float scale=t<.2f?Mathf.Lerp(1.9f,1,land*land):1+.06f*Mathf.Exp(-(t-.2f)*9)*Mathf.Sin((t-.2f)*26);
            r.stamp.localScale=new Vector3(scale,scale,1);r.stamp.localRotation=Quaternion.Euler(0,0,-8);
            OriginalGameArt.Stretch(r.stampFrame.rectTransform);r.stampFrame.pixelsPerUnitMultiplier=1/Mathf.Max(.1f,unit);
            float alpha=Mathf.Clamp01(t/.12f);r.stampFrame.color=new Color(OriginalUiTheme.Teal.r,OriginalUiTheme.Teal.g,OriginalUiTheme.Teal.b,.92f*alpha);
            OriginalGameArt.Stretch(r.stampLabel.rectTransform);r.stampLabel.alignment=TextAlignmentOptions.CenterGeoAligned;r.stampLabel.fontSize=(r.stampOnTop?20:17)*unit;r.stampLabel.fontStyle=FontStyles.Bold;r.stampLabel.characterSpacing=8;r.stampLabel.color=new Color(OriginalUiTheme.Teal.r,OriginalUiTheme.Teal.g,OriginalUiTheme.Teal.b,alpha);
            if(!r.celebrated&&t>=.2f){r.celebrated=true;Burst(r,r.stampOnTop?new Vector2(w-98,30):new Vector2(90,h-40),unit);}
        }
        void Burst(Reply r,Vector2 origin,float unit)
        {
            var palette=new[]{CapHue,OriginalUiTheme.Teal,Coral,Mint,new Color(.99f,.93f,.62f)};
            while(r.confetti.Count<34)
            {
                int i=r.confetti.Count;bool round=i%3==0;
                var image=OriginalGameArt.Image(Own+"FeedbackConfetti"+i,r.panel,round?OriginalInteractionArt.Disc:null,Color.white);image.type=Image.Type.Simple;r.confetti.Add(new Confetto{image=image});
            }
            for(int i=0;i<r.confetti.Count;i++)
            {
                var c=r.confetti[i];float angle=UnityEngine.Random.Range(200f,340f)*Mathf.Deg2Rad,speed=UnityEngine.Random.Range(240f,560f);
                c.position=origin+new Vector2(UnityEngine.Random.Range(-40f,40f),UnityEngine.Random.Range(-6f,6f));c.velocity=new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*speed;
                c.spin=UnityEngine.Random.Range(-720f,720f);c.angle=UnityEngine.Random.Range(0,360f);c.life=UnityEngine.Random.Range(1.1f,1.8f);c.age=0;
                var tint=palette[i%palette.Length];c.image.color=tint;c.image.gameObject.SetActive(true);c.image.transform.SetAsLastSibling();
                bool round=i%3==0;c.size=round?new Vector2(7,7):new Vector2(5,11);
            }
        }
        void Confetti(Reply r,float unit)
        {
            foreach(var c in r.confetti)
            {
                if(!c.image||!c.image.gameObject.activeSelf)continue;float dt=Time.unscaledDeltaTime;c.age+=dt;
                if(c.age>=c.life){c.image.gameObject.SetActive(false);continue;}
                c.velocity+=new Vector2(-c.velocity.x*1.2f*dt,900*dt);c.velocity.y=Mathf.Min(c.velocity.y,420);c.position+=c.velocity*dt;c.angle+=c.spin*dt;
                var rect=c.image.rectTransform;BoxCentered(rect,c.position.x-c.size.x/2,c.position.y-c.size.y/2,c.size.x,c.size.y,unit);
                rect.localRotation=Quaternion.Euler(0,0,c.angle);rect.localScale=new Vector3(Mathf.Cos(c.age*9+c.spin),1,1);
                var color=c.image.color;color.a=1-Mathf.Clamp01((c.age-c.life*.6f)/(c.life*.4f));c.image.color=color;
            }
        }
    }

    /// <summary>Hover lift and press squash for the peer step's buttons.</summary>
    public sealed class OriginalPeerPress:MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IPointerDownHandler,IPointerUpHandler
    {
        bool over,down;float value=1;
        public void OnPointerEnter(PointerEventData e){over=true;}
        public void OnPointerExit(PointerEventData e){over=false;down=false;}
        public void OnPointerDown(PointerEventData e){down=true;}
        public void OnPointerUp(PointerEventData e){down=false;}
        void OnDisable(){over=down=false;value=1;}
        public float Scale(bool enabled)
        {float target=!enabled?1:down?.96f:over?1.045f:1;value=Mathf.MoveTowards(value,target,Time.unscaledDeltaTime*1.2f);return value;}
    }

    /// <summary>Clicking or tapping the reply card finishes the streamed line.</summary>
    public sealed class OriginalPeerSkip:MonoBehaviour,IPointerClickHandler
    {
        public OriginalPeerPresentation owner;
        public void OnPointerClick(PointerEventData e){if(owner)owner.Skip();}
    }

    /// <summary>A speech-bubble tail: a paper triangle above the card's top edge with two ink sides.
    /// It overlaps the edge by a few pixels so the card's own border is covered where they join.</summary>
    public sealed class OriginalPeerTail:MaskableGraphic
    {
        float stroke;Color fill=Color.white,edge=Color.black;
        public static OriginalPeerTail Make(string name,Transform parent)
        {var r=OriginalGameArt.Rect(name,parent);var tail=r.GetComponent<OriginalPeerTail>()??r.gameObject.AddComponent<OriginalPeerTail>();tail.raycastTarget=false;return tail;}
        public void Layout(float centerX,float cardTop,float width,float height,float unit,Color paper)
        {
            OriginalGameArt.Box(rectTransform,centerX-width/2,cardTop-height,width,height+3,unit);
            var ink=OriginalUiTheme.Ink*paper;ink.a=1;var border=Color.Lerp(paper,ink,.55f);border.a=1;
            if(fill!=paper||edge!=border||!Mathf.Approximately(stroke,1.4f*unit)){fill=paper;edge=border;stroke=1.4f*unit;SetVerticesDirty();}
        }
        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();var r=rectTransform.rect;if(r.width<=0||r.height<=0)return;float joint=r.yMin+3*(r.height/Mathf.Max(1,r.height))*stroke/1.4f;
            var apex=new Vector2(r.center.x,r.yMax-stroke*.5f);var left=new Vector2(r.xMin,joint);var right=new Vector2(r.xMax,joint);
            int i=vh.currentVertCount;vh.AddVert(new Vector2(r.xMin-1,r.yMin),fill,Vector4.zero);vh.AddVert(new Vector2(r.xMax+1,r.yMin),fill,Vector4.zero);vh.AddVert(apex,fill,Vector4.zero);vh.AddTriangle(i,i+1,i+2);
            Side(vh,left,apex);Side(vh,apex,right);
        }
        void Side(VertexHelper vh,Vector2 a,Vector2 b)
        {
            var n=new Vector2(-(b-a).y,(b-a).x).normalized*stroke*.5f;int i=vh.currentVertCount;
            vh.AddVert(a-n,edge,Vector4.zero);vh.AddVert(a+n,edge,Vector4.zero);vh.AddVert(b+n,edge,Vector4.zero);vh.AddVert(b-n,edge,Vector4.zero);vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }

    /// <summary>Procedural sprites for the peer step: a rounded paper card with an ink edge, its soft
    /// shadow, the name-tag brush, an outlined rubber stamp and the bottom scrim.</summary>
    public static class OriginalPeerArt
    {
        static Sprite card,shadow,stamp,scrim,brush;
        static float Noise(float x,int seed)
        {int i=Mathf.FloorToInt(x);float f=x-i;float Hash(int n){n=(n<<13)^n^seed*7919;return 1f-((n*(n*n*15731+789221)+1376312589)&0x7fffffff)/1073741824f;}float u=f*f*(3-2*f);return Mathf.Lerp(Hash(i),Hash(i+1),u);}
        /// <summary>A name-tag brush stroke: solid for most of its length with ragged top and bottom
        /// edges, then a short dry-brush tail, so the name always sits on full colour.</summary>
        public static Sprite Brush=>brush?brush:brush=Make("brush",256,48,(x,y)=>
        {
            float u=x/255f,v=y/47f;
            float top=.14f+.08f*Noise(x*.05f,3)+.03f*Noise(x*.3f,5),bottom=.86f-.08f*Noise(x*.045f,11)-.03f*Noise(x*.28f,13);
            float body=Mathf.SmoothStep(0,1,(v-top)/.05f)*Mathf.SmoothStep(0,1,(bottom-v)/.05f);
            // Mathf.SmoothStep interpolates from-to; edges are written as SmoothStep(0,1,(u-start)/width).
            float fade=(1-Mathf.SmoothStep(0,1,(u-.74f)/.26f))*Mathf.SmoothStep(0,1,u/.025f);
            float bristles=1-.35f*Mathf.Clamp01(Noise(v*40,17)-.3f)*Mathf.SmoothStep(0,1,(u-.6f)/.35f);
            return new Color(1,1,1,body*fade*bristles);
        },Vector4.zero);
        static Sprite Make(string name,int w,int h,Func<float,float,Color> pixel,Vector4 border)
        {
            var texture=new Texture2D(w,h,TextureFormat.RGBA32,false){name="SPARC peer / "+name,wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear};var pixels=new Color[w*h];
            for(int y=0;y<h;y++)for(int x=0;x<w;x++)pixels[y*w+x]=pixel(x,y);
            texture.SetPixels(pixels);texture.Apply(false,true);var sprite=Sprite.Create(texture,new Rect(0,0,w,h),new Vector2(.5f,.5f),100,0,SpriteMeshType.FullRect,border);sprite.name="SPARC peer / "+name;return sprite;
        }
        /// <summary>Signed distance to a rounded box centred in a 64-pixel square.</summary>
        static float Box(float x,float y,float half,float radius)
        {
            float qx=Mathf.Abs(x-31.5f)-(half-radius),qy=Mathf.Abs(y-31.5f)-(half-radius);
            return Mathf.Sqrt(Mathf.Max(qx,0)*Mathf.Max(qx,0)+Mathf.Max(qy,0)*Mathf.Max(qy,0))+Mathf.Min(Mathf.Max(qx,qy),0)-radius;
        }
        public static Sprite Card=>card?card:card=Make("card",64,64,(x,y)=>
        {
            float d=Box(x,y,30.5f,13),alpha=Mathf.Clamp01(.5f-d),edge=Mathf.Clamp01(d+2.1f);float grain=((int)(x*17+y*31)%11)*.0012f;
            var paper=new Color(1-grain,1-grain,1-grain,1);var ink=Color.Lerp(paper,new Color(.12f,.21f,.23f),.55f);var c=Color.Lerp(paper,ink,edge);c.a=alpha;return c;
        },new Vector4(20,20,20,20));
        public static Sprite Shadow=>shadow?shadow:shadow=Make("shadow",64,64,(x,y)=>
        {float d=Box(x,y,12,6);float a=d<=0?1:Mathf.Pow(1-Mathf.Clamp01(d/19),2.2f);return new Color(1,1,1,a);},new Vector4(26,26,26,26));
        /// <summary>A rubber-stamp outline: a thick rounded ring whose ink is a little uneven.</summary>
        public static Sprite Stamp=>stamp?stamp:stamp=Make("stamp",64,64,(x,y)=>
        {
            float d=Box(x,y,29.5f,10),ring=Mathf.Clamp01(.5f-d)*Mathf.Clamp01(d+3.6f+.5f);float worn=.78f+.22f*Mathf.PerlinNoise(x*.21f,y*.21f);return new Color(1,1,1,ring*worn);
        },new Vector4(16,16,16,16));
        public static Sprite Scrim=>scrim?scrim:scrim=Make("scrim",4,128,(x,y)=>{float v=y/127f;return new Color(1,1,1,Mathf.Pow(1-v,1.7f));},Vector4.zero);
    }
}
