using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Sparc.Original
{
    /// <summary>Presentation only. Every spoken string, speaker and clip comes from its original Say record.</summary>
    public sealed class OriginalPresentation:MonoBehaviour
    {
        public OriginalRuntime runtime;
        public bool IsSpeaking {get;private set;}
        public string LineId {get;private set;}
        public string Speaker {get;private set;}
        public string Line {get;private set;}
        public int VisibleCharacters {get;private set;}
        public int PresentedLines {get;private set;}
        public AudioSource voice;
        bool advance,showDiagnostics;
        GUIStyle textStyle,titleStyle,heroStyle,compactHeroStyle,buttonStyle,panelStyle,kickerStyle,hintStyle,keyStyle,hitStyle;
        Texture2D paper,scrollTrack,scrollThumb;
        GUISkin sketchSkin;
        Vector2 dialogueScroll;
        static readonly Color Ink=new Color(0.14f,0.21f,0.23f);
        static readonly Color Muted=new Color(0.38f,0.43f,0.42f);
        static readonly Color Accent=new Color(0.23f,0.38f,0.39f);
        static readonly Color Rule=new Color(0.29f,0.39f,0.38f,0.24f);
        public const string CompletionTitle="Meeting Cells complete",CompletionBody="Next stop: the heart.",CompletionAction="Play again";
        static GUIStyle completionTitleStyle,completionBodyStyle;
        public readonly struct CompletionLayout
        {
            public readonly Rect Card,Title,Body,Replay;
            public CompletionLayout(Rect card,Rect title,Rect body,Rect replay){Card=card;Title=title;Body=body;Replay=replay;}
        }
        public static GUIStyle CompletionTitleStyle(){if(completionTitleStyle==null){completionTitleStyle=new GUIStyle{font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),fontSize=32,fontStyle=FontStyle.Bold,wordWrap=true,alignment=TextAnchor.MiddleLeft};completionTitleStyle.normal.textColor=Ink;}return completionTitleStyle;}
        public static GUIStyle CompletionBodyStyle(){if(completionBodyStyle==null){completionBodyStyle=new GUIStyle{font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"),fontSize=20,wordWrap=true,alignment=TextAnchor.MiddleLeft};completionBodyStyle.normal.textColor=Muted;}return completionBodyStyle;}
        public static CompletionLayout GetCompletionLayout(float width,float height)
        {
            float w=Mathf.Max(1,Mathf.Min(660,width-32)),pad=w<400?24:28,inner=Mathf.Max(1,w-pad*2);
            float title=CompletionTitleStyle().CalcHeight(new GUIContent(CompletionTitle),inner),body=CompletionBodyStyle().CalcHeight(new GUIContent(CompletionBody),inner);
            float h=pad*2+title+16+body+28+56;var card=new Rect((width-w)/2,(height-h)/2,w,h);
            var titleRect=new Rect(card.x+pad,card.y+pad,inner,title);var bodyRect=new Rect(card.x+pad,titleRect.yMax+16,inner,body);
            var replay=new Rect(card.center.x-Mathf.Min(260,inner)/2,bodyRect.yMax+28,Mathf.Min(260,inner),56);return new CompletionLayout(card,titleRect,bodyRect,replay);
        }
        public bool CompletionVisible=>runtime&&runtime.HandoffCompleted&&!runtime.ShuttingDown;
        void PresentationViewport(out float scale,out Rect safe)
        {
            scale=OriginalHudLayout.HasTouch?OriginalMobileControls.PixelScale:Mathf.Clamp(Mathf.Min(Screen.width/1440f,Screen.height/900f),1f,1.35f);safe=new Rect(0,0,Screen.width,Screen.height);
            if(OriginalHudLayout.HasTouch){var mobile=runtime?runtime.GetComponent<OriginalMobileControls>():null;var candidate=mobile?mobile.SafeScreenRect:Screen.safeArea;if(candidate.width>1&&candidate.height>1)safe=candidate;}
        }
        Rect CompletionViewportRect(bool replay)
        {
            if(!CompletionVisible)return Rect.zero;PresentationViewport(out var scale,out var safe);var layout=GetCompletionLayout(safe.width/scale,safe.height/scale);var r=replay?layout.Replay:layout.Card;
            return new Rect((safe.xMin+r.x*scale)/Screen.width,(Screen.height-safe.yMax+r.y*scale)/Screen.height,r.width*scale/Screen.width,r.height*scale/Screen.height);
        }
        public Rect CompletionCardViewportRect=>CompletionViewportRect(false);
        public Rect CompletionReplayViewportRect=>CompletionViewportRect(true);
        public void Advance(){advance=true;}
        public IEnumerator Say(OriginalBehaviour source)
        {
            while(IsSpeaking)yield return null;
            var character=source.Ref("character") as OriginalBehaviour;
            Speaker=character?character.Text("nameText",character.name):"";
            Line=source.Text("storyText");LineId=source.sourceId;PresentedLines++;IsSpeaking=true;advance=false;VisibleCharacters=0;dialogueScroll=Vector2.zero;
            var clip=source.Ref("voiceOverClip") as AudioClip;
            if(!voice){voice=gameObject.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;}
            if(clip){voice.clip=clip;voice.Play();}
            float shown=0;int count=Line.Length;
            while(VisibleCharacters<count)
            {
                if(advance){VisibleCharacters=count;advance=false;break;}
                shown+=Time.unscaledDeltaTime*36;VisibleCharacters=Mathf.Min(count,(int)shown);yield return null;
            }
            bool wait=source.Flag("waitForClick",true);
            if(wait){advance=false;while(!advance)yield return null;}
            else if(clip){while(voice.isPlaying)yield return null;}
            else yield return new WaitForSeconds(0.5f);
            if(voice)voice.Stop();IsSpeaking=false;advance=false;
        }
        public void CancelSpeech(){IsSpeaking=false;advance=false;if(voice)voice.Stop();}
        void Update()
        {
            var k=Keyboard.current;
            var menu=runtime?runtime.GetComponent<OriginalMenuUi>():null;if(menu&&menu.IsOpen)return;
            if(runtime&&!runtime.Started&&k?.enterKey.wasPressedThisFrame==true)runtime.Begin();
            if(IsSpeaking&&k!=null&&(k.spaceKey.wasPressedThisFrame||k.enterKey.wasPressedThisFrame))Advance();
            if(k!=null&&k.f8Key.wasPressedThisFrame)showDiagnostics=!showDiagnostics;
        }
        void Styles()
        {
            if(textStyle!=null)return;
            paper=new Texture2D(96,96,TextureFormat.RGBA32,false){name="Owned sketch paper",wrapMode=TextureWrapMode.Repeat};
            var grain=new System.Random(7041);var pixels=new Color[96*96];
            for(int i=0;i<pixels.Length;i++){float n=(float)grain.NextDouble()*0.009f;pixels[i]=new Color(0.975f-n,0.964f-n,0.930f-n,0.995f);}
            paper.SetPixels(pixels);paper.Apply(false,true);
            panelStyle=new GUIStyle(GUI.skin.box){border=new RectOffset(0,0,0,0)};panelStyle.normal.background=paper;
            textStyle=new GUIStyle(GUI.skin.label){wordWrap=true,fontSize=24,richText=true};textStyle.normal.textColor=Ink;
            titleStyle=new GUIStyle(textStyle){fontSize=25,fontStyle=FontStyle.Bold};
            heroStyle=new GUIStyle(titleStyle){fontSize=42};
            compactHeroStyle=new GUIStyle(heroStyle){fontSize=34};
            kickerStyle=new GUIStyle(textStyle){fontSize=13,fontStyle=FontStyle.Bold};kickerStyle.normal.textColor=Muted;
            hintStyle=new GUIStyle(textStyle){fontSize=17};hintStyle.normal.textColor=Muted;
            keyStyle=new GUIStyle(GUI.skin.label){fontSize=15,alignment=TextAnchor.MiddleCenter,fontStyle=FontStyle.Bold};keyStyle.normal.textColor=Ink;
            buttonStyle=new GUIStyle(GUI.skin.label){fontSize=20,alignment=TextAnchor.MiddleLeft,fontStyle=FontStyle.Bold};buttonStyle.normal.textColor=Color.white;
            hitStyle=new GUIStyle(GUIStyle.none){fontSize=20};hitStyle.normal.textColor=hitStyle.hover.textColor=hitStyle.active.textColor=hitStyle.focused.textColor=Color.clear;
            // Scrollbars belong to this presentation, too. Keep the long-line reader
            // in the same ink-and-paper palette without changing Unity's shared skin.
            scrollTrack=Solid("Sketch scroll track",new Color(0.86f,0.87f,0.81f));scrollThumb=Solid("Sketch scroll thumb",Accent);
            sketchSkin=Instantiate(GUI.skin);sketchSkin.name="Owned Meeting Cells sketch skin";
            sketchSkin.verticalScrollbar.fixedWidth=10;sketchSkin.verticalScrollbar.normal.background=sketchSkin.verticalScrollbar.hover.background=sketchSkin.verticalScrollbar.active.background=sketchSkin.verticalScrollbar.focused.background=scrollTrack;
            sketchSkin.verticalScrollbarThumb.normal.background=sketchSkin.verticalScrollbarThumb.hover.background=sketchSkin.verticalScrollbarThumb.active.background=sketchSkin.verticalScrollbarThumb.focused.background=scrollThumb;
            sketchSkin.verticalScrollbarUpButton.fixedHeight=sketchSkin.verticalScrollbarDownButton.fixedHeight=0;
            sketchSkin.horizontalScrollbar.fixedHeight=0;
        }
        static Texture2D Solid(string name,Color color)
        {var texture=new Texture2D(1,1,TextureFormat.RGBA32,false){name=name};texture.SetPixel(0,0,color);texture.Apply(false,true);return texture;}
        static void Stroke(Vector2 a,Vector2 b,Color color,float width=1)
        {
            var saved=GUI.matrix;var tint=GUI.color;GUI.color=color;
            // Compose in local GUI coordinates so a high-DPI parent matrix does
            // not apply the rotation pivot's scale twice.
            GUI.matrix=saved*Matrix4x4.TRS(new Vector3(a.x,a.y,0),Quaternion.Euler(0,0,Mathf.Atan2(b.y-a.y,b.x-a.x)*Mathf.Rad2Deg),Vector3.one);
            GUI.DrawTexture(new Rect(0,0,Vector2.Distance(a,b),width),Texture2D.whiteTexture);GUI.matrix=saved;GUI.color=tint;
        }
        static void Fill(Rect rect,Color color)
        {var saved=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=saved;}
        static void Icon(Rect rect,string key,Color color)
        {
            var sprite=OriginalUiIcons.Get(key);if(!sprite)return;
            var saved=GUI.color;GUI.color=color;
            var source=sprite.textureRect;GUI.DrawTextureWithTexCoords(rect,sprite.texture,new Rect(source.x/sprite.texture.width,source.y/sprite.texture.height,source.width/sprite.texture.width,source.height/sprite.texture.height));
            GUI.color=saved;
        }
        void Paper(Rect r,bool accent=false)
        {
            Fill(new Rect(r.x+5,r.y+7,r.width,r.height),new Color(0.10f,0.15f,0.15f,0.17f));
            GUI.Box(r,GUIContent.none,panelStyle);
            Stroke(new Vector2(r.x,r.y+1),new Vector2(r.xMax-1,r.y),Ink,1.3f);
            Stroke(new Vector2(r.xMax-1,r.y),new Vector2(r.xMax,r.yMax),Ink,1.2f);
            Stroke(new Vector2(r.xMax,r.yMax),new Vector2(r.x+1,r.yMax-1),Ink,1.3f);
            Stroke(new Vector2(r.x+1,r.yMax-1),new Vector2(r.x,r.y+1),Ink,1.1f);
            Stroke(new Vector2(r.x+6,r.y+5),new Vector2(r.xMax-8,r.y+4),Rule);
            if(accent)Fill(new Rect(r.x+1,r.y+1,5,r.height-2),Accent);
            Stroke(new Vector2(r.xMax-23,r.y+11),new Vector2(r.xMax-11,r.y+23),Rule);
        }
        bool Button(Rect r,string text,string icon)
        {
            bool hover=r.Contains(Event.current.mousePosition);
            Fill(new Rect(r.x+3,r.y+4,r.width,r.height),new Color(0.14f,0.21f,0.23f,0.18f));Fill(r,hover?Ink:Accent);
            Stroke(new Vector2(r.x+1,r.y+1),new Vector2(r.xMax-1,r.y),Ink);
            Stroke(new Vector2(r.xMax,r.y),new Vector2(r.xMax,r.yMax-1),Ink);
            Icon(new Rect(r.x+17,r.center.y-13,26,26),icon,Color.white);
            GUI.Label(new Rect(r.x+56,r.y,r.width-68,r.height),text,buttonStyle);
            return GUI.Button(r,new GUIContent(text),hitStyle);
        }
        void Key(Rect r,string text)
        {
            Fill(r,new Color(0.91f,0.91f,0.85f));Stroke(new Vector2(r.x,r.yMax),new Vector2(r.xMax,r.yMax),Rule);
            GUI.Label(r,text,keyStyle);
        }
        static string DisplaySpeaker(string speaker)
        {
            string value=(speaker??"").Trim();
            return value switch{"RedBloodCell" or "Red Blood Cell"=>"Red","WhiteBloodCell" or "White Blood Cell"=>"White","You"=>"Explorer","CAP"=>"Cap","TIP" or "Tip"=>"Field note",""=>"Meeting Cells",_=>value};
        }
        string TalkName()
        {
            var npc=runtime.NearestTalk;if(!npc)return "your guide";
            string block=npc.Text("npcTalk_blockName");
            if(block.StartsWith("RBC",StringComparison.Ordinal))return "Red";
            if(block.StartsWith("WBC",StringComparison.Ordinal))return "White";
            if(block.StartsWith("Mo",StringComparison.Ordinal)||block.StartsWith("CAP",StringComparison.Ordinal))return "Cap";
            return DisplaySpeaker(npc.gameObject.name);
        }
        void ControlHint(Rect rect,string icon,string key,string action)
        {
            Icon(new Rect(rect.x,rect.y+3,22,22),icon,Accent);
            GUI.Label(new Rect(rect.x+31,rect.y,rect.width-31,24),key,hintStyle);
            GUI.Label(new Rect(rect.x+31,rect.y+25,rect.width-31,24),action,hintStyle);
        }
        void StartCard(float w,float h)
        {
            bool touch=OriginalHudLayout.HasTouch;bool compact=h<520||w<570;float width=Mathf.Min(660,w-32),height=Mathf.Min(compact?360:468,h-32);
            var card=new Rect((w-width)/2,(h-height)/2,width,height);Paper(card,true);
            float left=card.x+28,inner=card.width-56;
            if(compact)
            {
                Icon(new Rect(left,card.y+22,25,25),"task",Accent);
                GUI.Label(new Rect(left+36,card.y+21,inner-36,28),"SPARC / MEETING CELLS",kickerStyle);
                compactHeroStyle.fontSize=Mathf.Clamp((int)(inner/7.7f),28,34);GUI.Label(new Rect(left,card.y+59,inner,48),"Meeting Cells",compactHeroStyle);
                float top=Mathf.Clamp(h/2+8,card.y+114,card.yMax-92);
                if(height>290)GUI.Label(new Rect(left,card.y+112,inner,48),"Meet Red and White. Learn by playing.",hintStyle);
                if(Button(new Rect(left,top,inner,56),"Start exploring","play"))runtime.Begin();
                GUI.Label(new Rect(left,top+69,inner,height>290?52:28),touch?(height>290?"Left stick: move / Right swipe: look\nTap Jump / Use Talk when nearby":"Left stick: move / Right swipe: look"):(height>290?"WASD move / Right-drag look\nSPACE jump / E talk / V view":"WASD move / Right-drag look / E talk"),hintStyle);
            }
            else
            {
                Icon(new Rect(left,card.y+26,30,30),"task",Accent);
                GUI.Label(new Rect(left+42,card.y+28,inner-42,28),"SPARC / MEETING CELLS",kickerStyle);
                GUI.Label(new Rect(left,card.y+77,inner,60),"Meeting Cells",heroStyle);
                GUI.Label(new Rect(left,card.y+139,inner,50),"A small adventure inside the bloodstream.",hintStyle);
                Stroke(new Vector2(left,card.y+193),new Vector2(card.xMax-28,card.y+192),Rule);
                GUI.Label(new Rect(left,card.y+210,inner,28),"EXPLORE  /  TALK  /  LEARN BY PLAYING",kickerStyle);
                // Preserve the existing real-input acceptance target at (720, 475).
                float top=h/2+8;if(Button(new Rect(left,top,inner,60),"Start exploring","play"))runtime.Begin();
                float third=inner/3;
                ControlHint(new Rect(left,top+85,third,54),"move",touch?"Left stick":"WASD","Move");
                ControlHint(new Rect(left+third,top+85,third,54),"look",touch?"Right swipe":"Right-drag","Look around");
                ControlHint(new Rect(left+third*2,top+85,third,54),"camera",touch?"View button":"V","Change view");
                GUI.Label(new Rect(left,card.yMax-38,inner,26),touch?"Tap Jump / Use Talk when nearby":"SPACE jump / E talk / ENTER start",kickerStyle);
            }
        }
        void DialogueCard(float w,float h)
        {
            bool compact=w<700;float width=Mathf.Min(1120,w-32),padding=compact?22:30,textWidth=width-padding*2-16;
            float fullHeight=textStyle.CalcHeight(new GUIContent(Line??""),textWidth);
            float availableHeight=Mathf.Max(180,h-32),minimumHeight=Mathf.Min(compact?246:220,availableHeight);
            float height=Mathf.Clamp(fullHeight+186,minimumHeight,Mathf.Min(availableHeight,Mathf.Max(minimumHeight,h*0.64f)));
            var card=new Rect((w-width)/2,h-height-16,width,height);Paper(card,true);
            Icon(new Rect(card.x+padding,card.y+22,34,34),Speaker is "WhiteBloodCell" or "White Blood Cell"?"shield":"chat",Accent);
            GUI.Label(new Rect(card.x+padding+48,card.y+14,card.width-padding*2-48,34),DisplaySpeaker(Speaker),titleStyle);
            GUI.Label(new Rect(card.x+padding+48,card.y+48,card.width-padding*2-48,23),"CONVERSATION",kickerStyle);
            Stroke(new Vector2(card.x+padding,card.y+81),new Vector2(card.xMax-padding,card.y+80),Rule);
            var content=new Rect(card.x+padding,card.y+98,card.width-padding*2,Mathf.Max(30,card.height-186));
            var oldSkin=GUI.skin;GUI.skin=sketchSkin;
            dialogueScroll=GUI.BeginScrollView(content,dialogueScroll,new Rect(0,0,textWidth,Mathf.Max(content.height,fullHeight)),false,false);
            GUI.Label(new Rect(0,0,textWidth,Mathf.Max(content.height,fullHeight)),(Line??"").Substring(0,Mathf.Min(VisibleCharacters,(Line??"").Length)),textStyle);GUI.EndScrollView();GUI.skin=oldSkin;
            bool revealing=VisibleCharacters<(Line??"").Length;
            float buttonWidth=compact?Mathf.Min(180,width-padding*2):202;
            var action=new Rect(card.xMax-padding-buttonWidth,card.yMax-73,buttonWidth,52);
            float hintWidth=action.x-(card.x+padding)-16;
            if(hintWidth>=135)
            {
                Key(new Rect(card.x+padding,card.yMax-66,132,32),OriginalHudLayout.HasTouch?"TAP CONTINUE":"SPACE / ENTER");
                if(hintWidth>360)GUI.Label(new Rect(card.x+padding+149,card.yMax-64,hintWidth-149,28),revealing?"Reveal the full line":"Continue when you're ready",hintStyle);
            }
            else Icon(new Rect(card.x+padding,card.yMax-62,27,27),"info",Muted);
            if(Button(action,revealing?"Show text":"Continue",revealing?"chat":"next"))Advance();
        }
        void OnGUI()
        {
            if(!runtime||runtime.ShuttingDown)return;var menu=runtime.GetComponent<OriginalMenuUi>();if(menu&&menu.IsOpen&&!runtime.HandoffStarted)return;Styles();
            // Small viewports keep readable type and touch targets; narrow cards reflow.
            PresentationViewport(out var scale,out var safe);var old=GUI.matrix;GUI.matrix=Matrix4x4.TRS(new Vector3(safe.xMin,Screen.height-safe.yMax,0),Quaternion.identity,Vector3.one*scale);float w=safe.width/scale,h=safe.height/scale;
            if(CompletionVisible)
            {
                var layout=GetCompletionLayout(w,h);Paper(layout.Card,true);GUI.Label(layout.Title,CompletionTitle,CompletionTitleStyle());GUI.Label(layout.Body,CompletionBody,CompletionBodyStyle());
                if(Button(layout.Replay,CompletionAction,"reset"))runtime.RestartIncludedScene();
            }
            else if(runtime.HandoffStarted){} // Native ending/fade keeps the whole screen until it finishes.
            else if(!runtime.Started)StartCard(w,h);
            else if(IsSpeaking)DialogueCard(w,h);
            else if(runtime.player&&runtime.player.CanControl&&!runtime.CinematicActive&&runtime.NearestTalk&&!runtime.activities.IsOpen&&(!runtime.interaction||!runtime.interaction.HasMenuFor(runtime.NearestTalk)))
            {
                float width=Mathf.Min(418,w-32);var card=new Rect((w-width)/2,h-110,width,82);Paper(card);
                if(Button(new Rect(card.x+10,card.y+10,card.width-20,62),"Talk to "+TalkName()+(OriginalHudLayout.HasTouch?"":"  /  E"),"talk"))runtime.Call(runtime.NearestTalk,"StartTalk",null);
            }
            if(showDiagnostics&&!runtime.HandoffStarted)
            {
                var card=new Rect(16,16,Mathf.Min(590,w-32),176);Paper(card);
                Icon(new Rect(card.x+20,card.y+18,24,24),"info",Accent);
                GUI.Label(new Rect(card.x+56,card.y+17,card.width-76,29),"DEVELOPER NOTES / F8 TO CLOSE",kickerStyle);
                Stroke(new Vector2(card.x+20,card.y+54),new Vector2(card.xMax-20,card.y+53),Rule);
                GUI.Label(new Rect(card.x+20,card.y+68,card.width-40,98),"Block: "+runtime.story.CurrentBlock+"\nLine: "+LineId+"\nUnimplemented boundaries: "+runtime.Unhandled.Count,hintStyle);
            }
            GUI.matrix=old;
        }
        void OnDestroy(){if(paper)Destroy(paper);if(scrollTrack)Destroy(scrollTrack);if(scrollThumb)Destroy(scrollThumb);if(sketchSkin)Destroy(sketchSkin);}
    }
}
