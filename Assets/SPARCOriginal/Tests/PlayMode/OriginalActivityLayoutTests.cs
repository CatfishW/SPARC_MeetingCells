using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sparc.Original.Tests
{
    public sealed class OriginalActivityLayoutTests
    {
        OriginalRuntime runtime;RenderTexture target;Camera camera;Touchscreen touch;
        [UnitySetUp] public IEnumerator Setup()
        {
            touch=InputSystem.AddDevice<Touchscreen>();
            yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;
            runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);
            camera=new GameObject("Native mobile activity viewport").AddComponent<Camera>();camera.transform.position=new Vector3(1000,1000,-1000);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.25f,.12f,.16f);camera.cullingMask=~0;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Activity layout cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);
            if(target){target.Release();UnityEngine.Object.Destroy(target);}if(touch!=null&&touch.added)InputSystem.RemoveDevice(touch);
        }
        void Viewport(Canvas canvas,int width,int height)
        {
            if(target){camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);}
            target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            canvas.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=1;Canvas.ForceUpdateCanvases();
        }
        static void Reveal(Transform transform){for(var parent=transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);}
        [UnityTest] public IEnumerator OriginalWhiteArenaAndControlsFitPortraitPhone(){yield return Board(true,390,844);}
        [UnityTest] public IEnumerator OriginalWhiteArenaAndControlsFitLandscapePhone(){yield return Board(true,844,390);}
        [UnityTest] public IEnumerator OriginalOxygenArenaAndControlsFitPortraitPhone(){yield return Board(false,390,844);}
        [UnityTest] public IEnumerator OriginalOxygenArenaAndControlsFitLandscapePhone(){yield return Board(false,844,390);}
        [UnityTest] public IEnumerator OxygenFitsSquareDesktop(){yield return DesktopBoard(false,960,940);}
        [UnityTest] public IEnumerator WhiteFitsSquareDesktop(){yield return DesktopBoard(true,960,940);}
        [UnityTest] public IEnumerator OxygenFitsTallDesktop(){yield return DesktopBoard(false,800,1100);}
        [UnityTest] public IEnumerator WhiteFitsTallDesktop(){yield return DesktopBoard(true,800,1100);}
        [UnityTest] public IEnumerator OxygenFitsWideDesktop(){yield return DesktopBoard(false,1440,900);}
        [UnityTest] public IEnumerator WhiteFitsWideDesktop(){yield return DesktopBoard(true,1440,900);}
        IEnumerator DesktopBoard(bool white,int width,int height){InputSystem.RemoveDevice(touch);touch=null;yield return Board(white,width,height);}
        [UnityTest] public IEnumerator OriginalOxygenTouchStickFitsLargeViewportAfterCanvasScaleSettles(){yield return SettledScale(false,1440,900,128);}
        [UnityTest] public IEnumerator OriginalWhitePhoneLayoutRecoversAfterCanvasScaleSettles(){yield return SettledScale(true,390,844,104);}
        IEnumerator SettledScale(bool white,int width,int height,float stickSize)
        {
            var manager=runtime.First(white?"WhiteBloodCellGameManager":"RedBloodCellGameManager");Reveal(manager.transform);runtime.activities.Activated(manager);
            var game=runtime.activities.Games.Single(g=>g.IsWhite==white);var canvas=manager.GetComponentInParent<Canvas>();
            foreach(var body in game.Area.GetComponentsInChildren<Rigidbody2D>(true))body.simulated=false;
            Viewport(canvas,width,height);runtime.activityUi.RefreshResponsiveLayout(canvas);Canvas.ForceUpdateCanvases();yield return null;
            var stick=canvas.GetComponentsInChildren<OnScreenStick>(true).Single();var rect=(RectTransform)stick.transform;var art=stick.GetComponent<Image>().sprite;string controlPath=stick.controlPath;
            var playerSize=((RectTransform)game.Player.transform).rect.size;int score=game.Score,level=game.Level;float duration=game.Duration;int screenWidth=Screen.width,screenHeight=Screen.height;var pixels=canvas.pixelRect;
            var initial=Pixels(rect);Assert.That(initial.width,Is.EqualTo(stickSize).Within(.2f));Inside(initial,width,height,"native touch stick");Assert.That(initial.xMin,Is.EqualTo(16).Within(.2f));Assert.That(initial.yMin,Is.EqualTo(16).Within(.2f));
            // Reproduce CanvasScaler's later Update without changing browser dimensions.
            canvas.scaleFactor=.63f;Canvas.ForceUpdateCanvases();Assert.That(Pixels(rect).width,Is.LessThan(stickSize*.7f));
            Assert.That(runtime.activityUi.RefreshViewportChanges(canvas),Is.True,"A scale-only change must trigger the runtime's normal viewport repair");Canvas.ForceUpdateCanvases();
            var settled=Pixels(rect);Assert.That(settled.width,Is.EqualTo(stickSize).Within(.2f));Assert.That(settled.height,Is.EqualTo(stickSize).Within(.2f));Assert.That(settled.xMin,Is.EqualTo(16).Within(.2f));Assert.That(settled.yMin,Is.EqualTo(16).Within(.2f));Inside(settled,width,height,"settled native touch stick");
            Assert.That(Screen.width,Is.EqualTo(screenWidth));Assert.That(Screen.height,Is.EqualTo(screenHeight));Assert.That(canvas.pixelRect,Is.EqualTo(pixels));Assert.That(runtime.activityUi.RefreshViewportChanges(canvas),Is.False,"A settled viewport must not repeatedly reset native controls");
            Assert.That(stick.GetComponent<Image>().sprite,Is.SameAs(art));Assert.That(stick.controlPath,Is.EqualTo(controlPath));Assert.That(((RectTransform)game.Player.transform).rect.size,Is.EqualTo(playerSize));Assert.That(game.Score,Is.EqualTo(score));Assert.That(game.Level,Is.EqualTo(level));Assert.That(game.Duration,Is.EqualTo(duration));
            if(white)
            {
                var instruction=canvas.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Instruction0");Assert.That(instruction.fontSize*canvas.scaleFactor,Is.EqualTo(16).Within(.1f));
                foreach(var wall in canvas.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name=="OuterWall"))Inside(Pixels(wall),width,height,"settled native arena boundary");
            }
            stick.OnPointerUp(new PointerEventData(EventSystem.current));Assert.That(rect.anchoredPosition,Is.EqualTo(Vector2.zero));
            var confirmation=canvas.GetComponentsInChildren<OriginalBehaviour>(true).Single(c=>c.sourceType=="ExitConfirmation");runtime.activities.Call(confirmation,"OnCancelButton",null);Capture((white?"white":"oxygen")+"-settled-"+width+"x"+height);
        }
        IEnumerator Board(bool white,int width,int height)
        {
            var manager=runtime.First(white?"WhiteBloodCellGameManager":"RedBloodCellGameManager");Assert.That(manager,Is.Not.Null);Reveal(manager.transform);runtime.activities.Activated(manager);
            var game=runtime.activities.Games.Single(g=>g.IsWhite==white);var canvas=manager.GetComponentInParent<Canvas>();var area=game.Area;
            // Geometry/art fixture only. Existing gameplay physics has independent contact
            // tests; freeze these real bodies so the layout screenshot is deterministic.
            foreach(var body in area.GetComponentsInChildren<Rigidbody2D>(true))body.simulated=false;
            Viewport(canvas,1440,900);OriginalMiniGame.MatchColliders(canvas.gameObject);
            var art=canvas.GetComponentsInChildren<Image>(true).ToDictionary(i=>i.GetInstanceID(),i=>i.sprite);
            var player=game.Player.transform as RectTransform;var playerSize=player.rect.size;var colliders=area.GetComponentsInChildren<CircleCollider2D>(true).ToDictionary(c=>c.GetInstanceID(),c=>c.radius);
            int score=game.Score,level=game.Level,hostiles=game.RemainingHostiles;float duration=game.Duration;
            Viewport(canvas,width,height);runtime.activityUi.RefreshResponsiveLayout(canvas);Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(area.rect.width,Is.EqualTo(964.8f).Within(.1f));Assert.That(area.rect.height,Is.EqualTo(751.5f).Within(.1f));Assert.That(area.pivot,Is.EqualTo(new Vector2(.5f,.5f)));Assert.That(area.localScale.x,Is.EqualTo(area.localScale.y).Within(.0001f));
            Assert.That(player.rect.size,Is.EqualTo(playerSize),"Uniform board presentation must keep the original player shape");
            foreach(var c in area.GetComponentsInChildren<CircleCollider2D>(true))if(colliders.TryGetValue(c.GetInstanceID(),out var radius))Assert.That(c.radius,Is.EqualTo(radius),"Original logical collection/cell collider radius must remain");
            foreach(var image in canvas.GetComponentsInChildren<Image>(true))if(art.TryGetValue(image.GetInstanceID(),out var sprite))Assert.That(image.sprite,Is.SameAs(sprite),"Original minigame artwork must be retained: "+image.name);
            Inside(Pixels(area),width,height,"original arena");
            if(!white)foreach(var label in area.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.name=="__SPARC_MINI_Delivery"))
            {Assert.That(label.textWrappingMode,Is.EqualTo(TextWrappingModes.NoWrap));Assert.That(label.GetPreferredValues(label.text,Mathf.Infinity,Mathf.Infinity).x,Is.LessThanOrEqualTo(label.rectTransform.rect.width+.5f),"Delivery caption must fit in one line in both overlay and camera-backed canvases");Assert.That(label.fontSize*Pixels(label.rectTransform).width/label.rectTransform.rect.width,Is.GreaterThanOrEqualTo(10),"Delivery caption must remain readable in CSS pixels");}
            foreach(var wall in canvas.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name=="OuterWall"))
            {Assert.That(wall.parent==area||wall.parent==area.parent,Is.True,"Every native boundary must belong to the uniformly transformed arena");Inside(Pixels(wall),width,height,"native arena boundary");}var panel=canvas.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="InstructionPanel");Inside(Pixels(panel),width,height,"native instructions");
            Assert.That(Pixels(area).Overlaps(Pixels(panel)),Is.False,"Readable status/instructions must not cover the arena");
            var instructions=panel.GetComponentsInChildren<ScrollRect>(true).Single(s=>s.name=="__SPARC_UI_Instructions");Assert.That(instructions.vertical,Is.True);Assert.That(instructions.viewport.rect.height,Is.GreaterThanOrEqualTo(44));
            for(int i=0;i<4;i++)
            {
                var text=panel.GetComponentsInChildren<TMP_Text>(true).Single(t=>t.name=="Instruction"+i);Assert.That(text.fontSize*canvas.scaleFactor,Is.GreaterThanOrEqualTo(15.9f));
                Assert.That(text.rectTransform.rect.height,Is.GreaterThanOrEqualTo(text.GetPreferredValues(text.text,text.rectTransform.rect.width,Mathf.Infinity).y-.5f));
            }
            var exit=panel.GetComponentsInChildren<Button>(true).Single(b=>b.name=="ExitButton");Inside(Pixels((RectTransform)exit.transform),width,height,"native Exit");Assert.That(Pixels((RectTransform)exit.transform).height,Is.GreaterThanOrEqualTo(43.9f));
            var stick=canvas.GetComponentsInChildren<OnScreenStick>(true).Single();if(OriginalHudLayout.HasTouch){Inside(Pixels((RectTransform)stick.transform),width,height,"native joystick");Assert.That(Pixels((RectTransform)stick.transform).width,Is.GreaterThanOrEqualTo(100));Assert.That(Pixels((RectTransform)stick.transform).Overlaps(Pixels(area)),Is.False,"The joystick must not hide the original arena");}else Assert.That(stick.gameObject.activeSelf,Is.False);
            stick.OnPointerUp(new PointerEventData(EventSystem.current));Assert.That(((RectTransform)stick.transform).anchoredPosition,Is.EqualTo(Vector2.zero),"Responsive wrapper must preserve native OnScreenStick's neutral origin");
            Assert.That(game.Score,Is.EqualTo(score));Assert.That(game.Level,Is.EqualTo(level));Assert.That(game.RemainingHostiles,Is.EqualTo(hostiles));Assert.That(game.Duration,Is.EqualTo(duration));
            var confirmation=canvas.GetComponentsInChildren<OriginalBehaviour>(true).Single(c=>c.sourceType=="ExitConfirmation");runtime.activities.Call(confirmation,"OnCancelButton",null);
            Capture((white?"white":"oxygen")+"-"+width+"x"+height);
            var help=canvas.GetComponentsInChildren<Button>(true).Single(b=>b.name=="__SPARC_MINI_Help");help.onClick.Invoke();Assert.That(game.Visuals.InstructionsExpanded,Is.True);Assert.That(instructions.viewport.gameObject.activeSelf,Is.True);Inside(Pixels(panel),width,height,"expanded game help");Capture((white?"white":"oxygen")+"-help-"+width+"x"+height);help.onClick.Invoke();Assert.That(game.Visuals.InstructionsExpanded,Is.False);Assert.That(game.Score,Is.EqualTo(score));Assert.That(game.Duration,Is.EqualTo(duration));
            exit.onClick.Invoke();Assert.That(runtime.activities.IsExitPromptVisible(confirmation),Is.True,"Keep the native Exit callback and confirmation flow");
            var box=canvas.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="Box"&&r.parent.name=="ExitConfirmationBox");Inside(Pixels(box),width,height,"native confirmation");Capture((white?"white":"oxygen")+"-exit-"+width+"x"+height);runtime.activities.Call(confirmation,"OnCancelButton",null);
            var loading=canvas.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="LoadingScreen");
            if(loading){loading.gameObject.SetActive(true);runtime.activityUi.RefreshResponsiveLayout(canvas);AssertOverlayIllustration(loading,width,height);Capture((white?"white":"oxygen")+"-loading-preview-"+width+"x"+height);loading.gameObject.SetActive(false);}
            var victory=canvas.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name=="VictoryScreen");
            if(victory){victory.gameObject.SetActive(true);runtime.activityUi.RefreshResponsiveLayout(canvas);AssertOverlayIllustration(victory,width,height);Capture((white?"white":"oxygen")+"-result-preview-"+width+"x"+height);victory.gameObject.SetActive(false);}
        }
        void AssertOverlayIllustration(RectTransform overlay,int width,int height)
        {
            Canvas.ForceUpdateCanvases();var art=overlay.Find("__SPARC_MINI_OverlayCharacter") as RectTransform;if(!art||!art.GetComponent<Image>().enabled)return;
            var bounds=Pixels(art);Inside(bounds,width,height,"generated result/loading illustration");
            foreach(var text in overlay.GetComponentsInChildren<TMP_Text>(true).Where(t=>!t.GetComponentInParent<Button>(true)))Assert.That(bounds.Overlaps(Pixels(text.rectTransform)),Is.False,"Decorative result art must fit above native text, including short landscape views");
        }
        [UnityTest] public IEnumerator SevenGeneratedAssetsLoadWithPixelFilteringAndKeepNativeGameplay()
        {
            foreach(var name in new[]{"red-cell-pixel-sketch","white-cell-pixel-sketch","bacterium-pixel-sketch","oxygen-pixel-sketch","oxygen-vessel-pixel-sketch","defense-vessel-pixel-sketch","worksheet-cells-pixel-sketch"})
            {var sprite=OriginalGameArt.Get(name);Assert.That(sprite,Is.Not.Null,name);Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Point));Assert.That(sprite.texture.mipmapCount,Is.EqualTo(1));}
            yield return null;
        }
        [UnityTest] public IEnumerator DeliveryTargetsHaveTheRequestedWiderGapAndMatchingHitboxCenters()
        {
            var source=runtime.First("RedBloodCellGameManager");Reveal(source.transform);runtime.activities.Activated(source);var game=runtime.activities.Games.Single(g=>!g.IsWhite);
            var zones=game.Area.GetComponentsInChildren<RectTransform>(true).Where(r=>r.name=="OxygenDeposit").OrderBy(r=>game.Area.InverseTransformPoint(r.TransformPoint(r.rect.center)).x).ToArray();Assert.That(zones.Length,Is.EqualTo(2));
            var corners=new Vector3[4];zones[0].GetWorldCorners(corners);float leftEnd=corners.Max(p=>game.Area.InverseTransformPoint(p).x);zones[1].GetWorldCorners(corners);float rightStart=corners.Min(p=>game.Area.InverseTransformPoint(p).x);Assert.That(rightStart-leftEnd,Is.GreaterThanOrEqualTo(430),"Visible receiver gap should be about432 native units, increased from224");
            foreach(var zone in zones){var circle=zone.GetComponent<CircleCollider2D>();if(circle)Assert.That(Vector2.Distance(circle.offset,zone.rect.center),Is.LessThan(.001f),"Target hitbox center must follow its left/right pivot");}
            yield return null;
        }
        [UnityTest] public IEnumerator SpriteAnimationAndImpactNeverMoveOrResizeTheNativePhysicsBody()
        {
            var source=runtime.First("RedBloodCellGameManager");Reveal(source.transform);runtime.activities.Activated(source);var game=runtime.activities.Games.Single(g=>!g.IsWhite);
            foreach(var body in game.Area.GetComponentsInChildren<Rigidbody2D>(true))body.simulated=false;
            var rect=(RectTransform)game.Player.transform;var position=rect.position;var size=rect.rect.size;var motion=game.Player.GetComponent<OriginalMiniSpriteMotion>();Assert.That(motion&&motion.Visual,Is.True);
            motion.Pulse(true);yield return new WaitForSecondsRealtime(.12f);Assert.That(Vector3.Distance(rect.position,position),Is.LessThan(.01f));Assert.That(rect.rect.size,Is.EqualTo(size));Assert.That(motion.Visual.localScale,Is.Not.EqualTo(Vector3.one));Assert.That(game.Score,Is.EqualTo(0));Assert.That(game.Duration,Is.EqualTo(120));
        }
        [UnityTest] public IEnumerator CleanStreakRewardsNeverMultiplySourceScoreAndPenaltyResetsTheStreak()
        {
            var source=runtime.First("WhiteBloodCellGameManager");Reveal(source.transform);runtime.activities.Activated(source);var game=runtime.activities.Games.Single(g=>g.IsWhite);
            foreach(var body in game.Area.GetComponentsInChildren<Rigidbody2D>(true))body.simulated=false;
            var tokens=game.Area.GetComponentsInChildren<OriginalMiniToken>();var hostile=tokens.Where(t=>t.Hostile).Take(3).ToArray();Assert.That(hostile.Length,Is.EqualTo(3));
            foreach(var token in hostile)game.Collect(token,(int)token.source.Number("scoreForDestroy",1));
            Assert.That(game.Score,Is.EqualTo(3));Assert.That(game.Visuals.Streak,Is.EqualTo(3));Assert.That(game.Visuals.BestStreak,Is.EqualTo(3));Assert.That(game.RemainingHostiles,Is.EqualTo(2));Assert.That(game.Level,Is.EqualTo(1));Assert.That(game.Duration,Is.EqualTo(250));
            var friendly=tokens.Single(t=>!t.Hostile);int penalty=(int)friendly.source.Number("scoreForDestroy",-1);Assert.That(penalty,Is.LessThan(0));game.Collect(friendly,penalty);Assert.That(game.Score,Is.EqualTo(3+penalty));Assert.That(game.Visuals.Streak,Is.EqualTo(0));Assert.That(game.Visuals.BestStreak,Is.EqualTo(3));yield return null;
        }
        [UnityTest] public IEnumerator OriginalQuestionAndPeerPanelsFitPortraitPhone(){yield return Panels(390,844);}
        [UnityTest] public IEnumerator OriginalQuestionAndPeerPanelsFitLandscapePhone(){yield return Panels(844,390);}
        [UnityTest] public IEnumerator QuestionAndPeerPanelsFitSquareDesktop(){InputSystem.RemoveDevice(touch);touch=null;yield return Panels(960,940);}
        [UnityTest] public IEnumerator QuestionAndPeerPanelsFitTallDesktop(){InputSystem.RemoveDevice(touch);touch=null;yield return Panels(800,1100);}
        [UnityTest] public IEnumerator QuestionAndPeerPanelsFitWideDesktop(){InputSystem.RemoveDevice(touch);touch=null;yield return Panels(1440,900);}
        IEnumerator Panels(int width,int height)
        {
            var canvases=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).ToArray();
            var question=canvases.Single(c=>c.name=="QuestionCanvas");Reveal(question.transform);runtime.activityUi.PreparePanel(question);Viewport(question,width,height);runtime.activityUi.RefreshResponsiveLayout(question);yield return null;
            var choices=question.GetComponentsInChildren<Button>(true).Where(b=>b.name is "Button A" or "Button B").ToArray();Assert.That(choices.Length,Is.EqualTo(2));
            foreach(var button in choices){Inside(Pixels((RectTransform)button.transform),width,height,button.name);Assert.That(Pixels((RectTransform)button.transform).height,Is.GreaterThanOrEqualTo(43.9f));Assert.That(button.onClick.GetPersistentEventCount(),Is.GreaterThan(0));}
            Inside(Pixels(question.GetComponentsInChildren<RectTransform>(true).Single(r=>r.name=="Content")),width,height,"native question");
            Capture("question-"+width+"x"+height);question.gameObject.SetActive(false);
            foreach(var name in new[]{"PlayerInputField","AnswerField","WaitForResponse","LAMInputField","LAMAnswerField"})
            {
                var canvas=canvases.Single(c=>c.name==name);Reveal(canvas.transform);runtime.activityUi.PreparePanel(canvas);Viewport(canvas,width,height);runtime.activityUi.RefreshResponsiveLayout(canvas);Canvas.ForceUpdateCanvases();yield return null;
                if(name=="PlayerInputField"||name=="LAMInputField")
                {var field=canvas.GetComponentInChildren<TMP_InputField>();Assert.That(field,Is.Not.Null);Inside(Pixels((RectTransform)field.transform),width,height,name);Assert.That(field.characterLimit,Is.EqualTo(2000));Assert.That(field.textComponent.fontSize*canvas.scaleFactor,Is.GreaterThanOrEqualTo(17.9f));runtime.GetComponent<OriginalUiTheme>().Refresh();runtime.activityUi.RefreshResponsiveLayout(canvas);Capture(name+"-empty-"+width+"x"+height);field.text="Red blood cells have more room for hemoglobin because they do not have a nucleus.";runtime.activityUi.RefreshResponsiveLayout(canvas);var send=canvas.GetComponentsInChildren<Button>().Single(b=>b.name=="__SPARC_UI_PeerSend"||b.name=="Send");Inside(Pixels((RectTransform)send.transform),width,height,"Send");Assert.That(send.interactable,Is.EqualTo(name=="PlayerInputField"));Assert.That(Pixels((RectTransform)send.transform).height,Is.GreaterThanOrEqualTo(44));Assert.That(Pixels((RectTransform)send.transform).Overlaps(Pixels((RectTransform)field.transform)),Is.False);}
                else if(name=="AnswerField")
                {var box=canvas.transform.Find("GameObject") as RectTransform;Inside(Pixels(box),width,height,name);var scroll=box.GetComponentInChildren<ScrollRect>(true);Assert.That(scroll,Is.Not.Null,"Long original feedback remains readable in its own scroll viewport");foreach(var button in box.GetComponentsInChildren<Button>().Where(b=>b.gameObject.activeInHierarchy)){Inside(Pixels((RectTransform)button.transform),width,height,button.name);Assert.That(Pixels((RectTransform)button.transform).height,Is.GreaterThanOrEqualTo(43.9f));}}
                if(name=="AnswerField")
                {var body=canvas.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="AF");body.text="[Error] Feedback service unavailable. Please try again.";foreach(var b in canvas.GetComponentsInChildren<Button>(true))b.gameObject.SetActive(b.name=="ContinueButton");var card=canvas.GetComponentsInChildren<Image>(true).Single(i=>i.name=="__SPARC_UI_FeedbackCard");Assert.That(card.enabled&&card.color.a>.95f,Is.True,"Feedback needs an opaque paper surface over the 3D world");}
                runtime.GetComponent<OriginalUiTheme>().Refresh();runtime.activityUi.RefreshResponsiveLayout(canvas);Capture(name+"-"+width+"x"+height);canvas.gameObject.SetActive(false);
                if(name=="AnswerField")
                {canvas.gameObject.SetActive(true);var body=canvas.GetComponentsInChildren<TMP_Text>(true).First(t=>t.name=="AF");body.text=string.Join("\n\n",Enumerable.Repeat("Red blood cells carry oxygen. Explain how their shape and missing nucleus help with this job.",10));runtime.activityUi.RefreshResponsiveLayout(canvas);Capture(name+"-long-"+width+"x"+height);canvas.gameObject.SetActive(false);}
            }
            Assert.That(runtime.story.Trace,Is.Empty,"Responsive native panels must not advance or replace the authored story");
        }
        Rect Pixels(RectTransform rect){var corners=new Vector3[4];rect.GetWorldCorners(corners);var p=corners.Select(c=>(Vector2)camera.WorldToScreenPoint(c)).ToArray();return Rect.MinMaxRect(p.Min(c=>c.x),p.Min(c=>c.y),p.Max(c=>c.x),p.Max(c=>c.y));}
        static void Inside(Rect rect,int width,int height,string name)
        {Assert.That(rect.xMin,Is.GreaterThanOrEqualTo(-1),name);Assert.That(rect.yMin,Is.GreaterThanOrEqualTo(-1),name);Assert.That(rect.xMax,Is.LessThanOrEqualTo(width+1),name);Assert.That(rect.yMax,Is.LessThanOrEqualTo(height+1),name);}
        void Capture(string name)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,target.width,target.height),0,0);pixels.Apply();
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/OriginalScene/LayoutFollowup"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"native-mobile-"+name+".png"),pixels.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);
        }
    }
}
