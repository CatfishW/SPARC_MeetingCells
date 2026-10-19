using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sparc.Original.Tests
{
    /// <summary>Audits the retained scene and real spawned assets, including UI hidden by the story.</summary>
    public sealed class OriginalUiThemeTests
    {
        OriginalRuntime runtime;
        OriginalUiTheme theme;
        Graphic[] native;
        MeshRenderer[] questRings;
        const string ScenePath="Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity";

        [UnitySetUp] public IEnumerator Setup()
        {
            // sceneLoaded runs after Awake/OnEnable and before Start. Capture the actual
            // source components before the theme creates glyphs or replaces their artwork.
            SceneManager.sceneLoaded+=Loaded;
            try { yield return SceneManager.LoadSceneAsync(ScenePath); }
            finally { SceneManager.sceneLoaded-=Loaded; }
            Assert.That(runtime,Is.Not.Null);Assert.That(theme,Is.Not.Null);
            yield return null;
        }
        void Loaded(Scene scene,LoadSceneMode mode)
        {
            if(scene.path!=ScenePath)return;
            runtime=InScene<OriginalRuntime>(scene).Single();theme=runtime.GetComponent<OriginalUiTheme>();
            Assert.That(theme,Is.Not.Null);theme.enabled=false;
            native=InScene<Graphic>(scene);
            questRings=InScene<MeshRenderer>(scene).Where(r=>r.sharedMaterials.Any(m=>m&&m.name=="HoloLoading_01_Mat")).ToArray();
            Assert.That(native.All(g=>!g.name.StartsWith("__SPARC_UI_",StringComparison.Ordinal)),Is.True,"Fixture must capture original UI before generated icons exist");
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            SceneManager.sceneLoaded-=Loaded;
            if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("UI theme cleanup "+Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
        }
        static T[] InScene<T>(Scene scene) where T:Component=>scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<T>(true)).ToArray();
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        static bool NativeMinigame(Transform t)=>PathOf(t).Split('/').Any(name=>name=="WordGameManager"||name=="WordGameManager(Clone)"||name=="RedBloodCellGameCanvas"||name=="WhiteBloodCellGameCanvas"||name=="RedBloodCellGameCanvas(Clone)"||name=="WhiteBloodCellGameCanvas(Clone)");
        static Texture Artwork(Graphic g)=>g is Image image?(image.overrideSprite?image.overrideSprite.texture:image.sprite?image.sprite.texture:null):g is RawImage raw?raw.texture:null;
        static string ArtworkName(Graphic g)=>g is Image image?(image.overrideSprite?image.overrideSprite.name:image.sprite?image.sprite.name:""):g is RawImage raw&&raw.texture?raw.texture.name:"";
        static bool NewArtwork(Graphic g)=>ArtworkName(g).StartsWith("SPARC UI / ",StringComparison.Ordinal)||ArtworkName(g).StartsWith("SPARC pixel UI / ",StringComparison.Ordinal)||ArtworkName(g).StartsWith("SPARC generated / ",StringComparison.Ordinal)||ArtworkName(g).StartsWith("Sketch / ",StringComparison.Ordinal)||ArtworkName(g).StartsWith("Owned UI icon / ",StringComparison.Ordinal);
        static bool OriginalMedia(Graphic g)
        {
            // This independent allowlist describes source content, not theme role strings.
            string name=ArtworkName(g),path=PathOf(g.transform);
            return name=="WGCell"||name.StartsWith("KnowledgeMapBackground",StringComparison.Ordinal)||
                Artwork(g) is RenderTexture||path.Contains("VideoCanvas_Loading/Video Player",StringComparison.Ordinal)||
                (g.material&&(g.material.name.Contains("CutoutCircle",StringComparison.Ordinal)||g.material.name.Contains("Distortion",StringComparison.Ordinal)))||
                path.EndsWith("BlackinTransitoin/BlackInOutImage",StringComparison.Ordinal);
        }

        [UnityTest] public IEnumerator EveryNativeSurfaceIsClassifiedWhileOriginalControlsPhysicsAndMinigameArtArePreserved()
        {
            Assert.That(native.Length,Is.GreaterThan(400),"Audit must include the real scene and nested prefab UI");
            Assert.That(native.Count(g=>!g.gameObject.activeInHierarchy),Is.GreaterThan(100),"Inactive settings, workbook pages, results and confirmations must be audited");
            var graphics=native.Select(g=>new GraphicRecord(g)).ToArray();
            var buttons=InScene<Button>(runtime.gameObject.scene);var callbacks=buttons.Select(b=>new ButtonRecord(b)).ToArray();
            var colliders=PhysicsRecords();
            var text=InScene<TMP_Text>(runtime.gameObject.scene).ToDictionary(t=>t.GetInstanceID(),t=>t.text);
            var oldText=InScene<Text>(runtime.gameObject.scene).ToDictionary(t=>t.GetInstanceID(),t=>t.text);
            theme.Refresh();
            var subtitles=InScene<TMP_Text>(runtime.gameObject.scene).Where(t=>PathOf(t.transform).Contains("StorylineIntro/Subtitles/",StringComparison.Ordinal)).ToArray();
            Assert.That(subtitles.Length,Is.EqualTo(3),"Numeric native caption names must be classified by their parent hierarchy");
            foreach(var caption in subtitles)
            {
                Assert.That(caption.color.r,Is.EqualTo(OriginalUiTheme.Paper.r).Within(.001f));
                Assert.That(caption.color.g,Is.EqualTo(OriginalUiTheme.Paper.g).Within(.001f));
                Assert.That(caption.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth),Is.GreaterThan(.1f));
                var panelLabel=InScene<TMP_Text>(runtime.gameObject.scene).First(t=>!NativeMinigame(t.transform)&&!PathOf(t.transform).Contains("Subtitles",StringComparison.Ordinal)&&t.font==caption.font);
                Assert.That(caption.fontSharedMaterial,Is.Not.SameAs(panelLabel.fontSharedMaterial),"Subtitle edges must not change paper-panel typography");
            }
            AssertCoverage(graphics);AssertButtons(callbacks);AssertPhysics(colliders);
            CollectionAssert.AreEquivalent(buttons.Select(b=>b.GetInstanceID()),InScene<Button>(runtime.gameObject.scene).Select(b=>b.GetInstanceID()),"Presentation must preserve every native Button object");
            foreach(var label in InScene<TMP_Text>(runtime.gameObject.scene))if(text.TryGetValue(label.GetInstanceID(),out var value))Assert.That(label.text,Is.EqualTo(value),PathOf(label.transform));
            foreach(var label in InScene<Text>(runtime.gameObject.scene))if(oldText.TryGetValue(label.GetInstanceID(),out var value))Assert.That(label.text,Is.EqualTo(value),PathOf(label.transform));
            Assert.That(questRings.Length,Is.GreaterThanOrEqualTo(2),"Both authored CAP and WBC navigation markers must be checked");
            foreach(var ring in questRings)
            {
                Assert.That(ring.enabled,Is.False,"Old hologram mesh must retire: "+PathOf(ring.transform));
                foreach(var label in ring.GetComponentsInChildren<TMP_Text>(true).Where(t=>t.text.Trim()=="?"))
                    Assert.That(label.enabled,Is.False,"Old question-mark glyph must not overlap its replacement badge");
                var replacements=ring.GetComponentsInChildren<SpriteRenderer>(true);
                Assert.That(replacements.Length,Is.GreaterThanOrEqualTo(2),"World marker needs a new badge and semantic glyph");
                Assert.That(replacements.Any(s=>s.sprite&&s.sprite.name=="Sketch / task"),Is.True,"World-space navigation must get the new task icon");
                Assert.That(replacements.All(s=>s.sprite&&(s.sprite.name.StartsWith("Sketch / ",StringComparison.Ordinal)||s.sprite.name.StartsWith("SPARC UI / ",StringComparison.Ordinal))),Is.True);
            }
            // Repeated refresh must preserve the original controls and avoid stacking icons.
            int generated=InScene<Graphic>(runtime.gameObject.scene).Count(g=>g.name.StartsWith("__SPARC_UI_",StringComparison.Ordinal));
            theme.Refresh();Assert.That(InScene<Graphic>(runtime.gameObject.scene).Count(g=>g.name.StartsWith("__SPARC_UI_",StringComparison.Ordinal)),Is.EqualTo(generated));
            AssertCoverage(graphics);AssertButtons(callbacks);AssertPhysics(colliders);
            WriteReport("native-scene",graphics,callbacks.Length,colliders.Length);
            yield return null;
        }

        [UnityTest] public IEnumerator ReloadingTheThemedSceneRetainsTheImportedFontAtlasAndMaterial()
        {
            var original=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            Assert.That(original,Is.Not.Null);var atlases=original.atlasTextures.ToArray();var material=original.material;
            Assert.That(atlases,Is.Not.Empty);theme.Refresh();runtime.Shutdown();
            SceneManager.sceneLoaded+=Loaded;
            try{yield return SceneManager.LoadSceneAsync(ScenePath);}
            finally{SceneManager.sceneLoaded-=Loaded;}
            yield return null;yield return null;theme.Refresh();
            Assert.That(original.material,Is.SameAs(material));Assert.That(material,Is.Not.Null);
            CollectionAssert.AreEqual(atlases,original.atlasTextures);
            Assert.That(atlases.All(texture=>texture),Is.True,"Scene cleanup must never destroy the imported shared font textures");
            Assert.That(original.HasCharacter('A'),Is.True);
        }

        [UnityTest] public IEnumerator LaterObjectivesAreRedesignedWhileOriginalMinigameAssetsArePreserved()
        {
            theme.Refresh();
            var manager=runtime.First("ObjectiveHUDManager");Assert.That(manager,Is.Not.Null);
            var panel=manager.Component<RectTransform>("ObjectivePanel");var prefab=manager.Object("PrimaryObjectivePrefab");
            Assert.That(panel&&prefab,Is.True);
            var row=UnityEngine.Object.Instantiate(prefab,panel);row.SetActive(true);
            foreach(var source in row.GetComponentsInChildren<OriginalBehaviour>(true))runtime.Register(source);
            var toast=row.GetComponents<OriginalBehaviour>().Single(c=>c.sourceType=="ObjectiveToast");
            var objective=runtime.components.First(c=>c.Text("Description")=="- Follow CAP");
            toast.Component<TMP_Text>("TitleTextContent").text=objective.Text("Title");
            toast.Component<TMP_Text>("DescriptionTextContent").text=objective.Text("Description");
            toast.Component<TMP_Text>("CounterTextContent").text="";
            // Invoke the real activation path, which runs PrepareGame and MiniGame.Begin.
            // Keeping the story idle isolates presentation from timers and collision scoring.
            var sourceManager=runtime.First("WhiteBloodCellGameManager");Assert.That(sourceManager,Is.Not.Null);
            for(var parent=sourceManager.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
            runtime.activities.Activated(sourceManager);
            var game=runtime.activities.Games.Single(g=>g.IsWhite);Assert.That(game.Begun,Is.True);
            Assert.That(game.RemainingHostiles,Is.EqualTo(5),"The original first level must still spawn five bacteria");
            var spawned=InScene<OriginalMiniToken>(runtime.gameObject.scene).Where(t=>t.game==game).ToArray();
            Assert.That(spawned.Length,Is.GreaterThanOrEqualTo(6),"Audit must include bacteria and the authored friendly red cell");
            var later=InScene<Graphic>(runtime.gameObject.scene).Where(g=>!theme.HasStyled(g)).Select(g=>new GraphicRecord(g)).ToArray();
            Assert.That(later.Length,Is.GreaterThan(10),"This must inspect real dynamically instantiated graphics");
            Assert.That(later.Any(g=>g.Graphic is RawImage),Is.True,"Nested objective RawImage ornaments must be included");
            var callbacks=row.GetComponentsInChildren<Button>(true).Concat(game.GetComponentInParent<Canvas>().GetComponentsInChildren<Button>(true)).Select(b=>new ButtonRecord(b)).ToArray();
            var spawnedArt=spawned.SelectMany(token=>token.GetComponentsInChildren<Image>(true)).Select(image=>new GraphicRecord(image)).ToDictionary(record=>record.Id);
            var colliders=PhysicsRecords();
            theme.Refresh();AssertCoverage(later);AssertButtons(callbacks);AssertPhysics(colliders);
            foreach(var token in spawned)
            {
                var images=token.GetComponentsInChildren<Image>(true);Assert.That(images,Is.Not.Empty);
                foreach(var image in images)AssertOriginalMinigameArt(spawnedArt[image.GetInstanceID()]);
            }
            Assert.That(toast.Component<TMP_Text>("TitleTextContent").text,Is.EqualTo(objective.Text("Title")));
            Assert.That(toast.Component<TMP_Text>("DescriptionTextContent").text,Is.EqualTo(objective.Text("Description")));
            var descriptionColor=toast.Component<TMP_Text>("DescriptionTextContent").color;
            Assert.That(descriptionColor.r,Is.EqualTo(OriginalUiTheme.Ink.r).Within(.001f),"Native BackgroundImageSubTitle is a paper objective, not a scene caption");
            Assert.That(descriptionColor.g,Is.EqualTo(OriginalUiTheme.Ink.g).Within(.001f));
            Assert.That(descriptionColor.b,Is.EqualTo(OriginalUiTheme.Ink.b).Within(.001f));
            Assert.That(game.Score,Is.EqualTo(0));Assert.That(game.Level,Is.EqualTo(1));Assert.That(game.RemainingHostiles,Is.EqualTo(5));
            WriteReport("dynamic-prefabs",later,callbacks.Length,colliders.Length);
            var canvas=game.GetComponentInParent<Canvas>();
            var confirmation=canvas.GetComponentsInChildren<OriginalBehaviour>(true).Single(c=>c.sourceType=="ExitConfirmation");
            runtime.activities.Call(confirmation,"OnCancelButton",null);
            Assert.That(runtime.activities.IsExitPromptVisible(confirmation),Is.False);
            var joystick=canvas.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Joystick");joystick.gameObject.SetActive(false);
            CaptureNativeActivity(canvas);
            yield return null;
        }
        static void CaptureNativeActivity(Canvas canvas)
        {
            // Layout preview only, separate from the real-input browser recording.
            // Render the actual themed native widgets at their reference resolution.
            var obj=new GameObject("Native UI layout preview");var camera=obj.AddComponent<Camera>();
            camera.transform.position=new Vector3(1000,1000,-1000);camera.orthographic=true;camera.orthographicSize=5;
            camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=OriginalUiTheme.Paper;
            var target=new RenderTexture(1440,900,24);target.Create();camera.targetTexture=target;
            var scaler=canvas.GetComponent<CanvasScaler>();if(scaler)scaler.enabled=false;
            canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=1;
            Canvas.ForceUpdateCanvases();foreach(var text in canvas.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;
            var pixels=new Texture2D(1440,900,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,1440,900),0,0);pixels.Apply();
            var path=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/OriginalScene/UiRedesign/native-white-layout-preview.png"));File.WriteAllBytes(path,pixels.EncodeToPNG());
            RenderTexture.active=previous;camera.targetTexture=null;target.Release();UnityEngine.Object.Destroy(target);UnityEngine.Object.Destroy(pixels);UnityEngine.Object.Destroy(obj);
        }

        sealed class GraphicRecord
        {
            public readonly Graphic Graphic;public readonly int Id;public readonly string Path,OriginalArtwork,OriginalGeometry,ImageSettings;public readonly Texture Texture;public readonly Sprite SourceSprite,SourceOverride;public readonly Material SourceMaterial;public readonly Color SourceColor;public readonly float InitialAlpha;public readonly bool Media,RaycastTarget,InvisibleHelper,IsMini;
            public GraphicRecord(Graphic graphic){Graphic=graphic;Id=graphic.GetInstanceID();Path=PathOf(graphic.transform);OriginalArtwork=ArtworkName(graphic);Texture=Artwork(graphic);SourceSprite=(graphic as Image)?.sprite;SourceOverride=(graphic as Image)?.overrideSprite;SourceMaterial=graphic.material;SourceColor=graphic.color;OriginalGeometry=Geometry(graphic.rectTransform);ImageSettings=Settings(graphic);IsMini=NativeMinigame(graphic.transform);InitialAlpha=graphic.color.a;InvisibleHelper=graphic is Image&&!Texture&&InitialAlpha<=.001f&&!graphic.GetComponentInParent<Button>(true);Media=OriginalMedia(graphic);RaycastTarget=graphic.raycastTarget;}
        }
        void AssertCoverage(IEnumerable<GraphicRecord> records)
        {
            foreach(var original in records)
            {
                var g=original.Graphic;Assert.That(g&&g.GetInstanceID()==original.Id,Is.True,"Native Graphic identity lost: "+original.Path);
                Assert.That(theme.HasStyled(g),Is.True,"Unclassified native Graphic: "+original.Path);
                Assert.That(theme.RoleOf(g),Is.Not.EqualTo("unclassified"),original.Path);
                Assert.That(g.raycastTarget,Is.EqualTo(original.RaycastTarget),"Theme must preserve native hit testing: "+original.Path);
                if(g is TMP_Text||g is Text)continue;
                if(original.IsMini){AssertOriginalMinigameArt(original);continue;}
                if(original.Media){Assert.That(Artwork(g),Is.SameAs(original.Texture),"Educational/video/story content must survive: "+original.Path);continue;}
                if(original.InvisibleHelper)
                {
                    Assert.That(g.color.a,Is.EqualTo(original.InitialAlpha).Within(.0001f),"Invisible source input/layout helper became visible: "+original.Path);
                    Assert.That(Artwork(g),Is.Null,"Invisible source receiver acquired visible artwork: "+original.Path);
                    continue;
                }
                if(g.enabled)
                {
                    Assert.That(NewArtwork(g),Is.True,"Old visible Image or RawImage artwork survived: "+original.Path+" / "+ArtworkName(g));
                    if(original.Texture)Assert.That(Artwork(g),Is.Not.SameAs(original.Texture),"Source decorative texture was only recolored: "+original.Path);
                }
                else if(original.Texture&&Artwork(g)==original.Texture)
                {
                    Assert.That(g.GetComponentInParent<Button>(true)||original.Path.Contains("DynamicInteractionUI",StringComparison.Ordinal),Is.True,"Retired old artwork needs its replaced native control or restored interaction card: "+original.Path);
                }
            }
        }
        static string Geometry(RectTransform rect)=>JsonConvert.SerializeObject(new{anchors=new[]{V(rect.anchorMin),V(rect.anchorMax)},pivot=V(rect.pivot),offsets=new[]{V(rect.offsetMin),V(rect.offsetMax)},size=V(rect.rect.size),scale=V(rect.localScale),rotation=new[]{rect.localRotation.x,rect.localRotation.y,rect.localRotation.z,rect.localRotation.w}});
        static string Settings(Graphic graphic)=>graphic is Image image?JsonConvert.SerializeObject(new{type=image.type.ToString(),image.preserveAspect,image.fillCenter,image.fillAmount,fillMethod=image.fillMethod.ToString(),image.fillOrigin,image.fillClockwise}):graphic is RawImage raw?JsonConvert.SerializeObject(new{raw.uvRect.x,raw.uvRect.y,raw.uvRect.width,raw.uvRect.height}):"";
        static void AssertOriginalMinigameArt(GraphicRecord original)
        {
            var g=original.Graphic;Assert.That(original.IsMini,Is.True,"Expected an original native minigame canvas: "+original.Path);
            Assert.That(Artwork(g),Is.SameAs(original.Texture),"Keep original minigame texture: "+original.Path);
            if(g is Image image)
            {
                Assert.That(image.sprite,Is.SameAs(original.SourceSprite),"Keep the original imported sprite: "+original.Path);
                Assert.That(image.overrideSprite,Is.SameAs(original.SourceOverride),"Keep original minigame sprite state: "+original.Path);
            }
            Assert.That(g.material,Is.SameAs(original.SourceMaterial),"Keep original minigame material: "+original.Path);
            Assert.That(g.color,Is.EqualTo(original.SourceColor),"Keep original minigame art palette: "+original.Path);
            Assert.That(Geometry(g.rectTransform),Is.EqualTo(original.OriginalGeometry),"Do not reshape original minigame artwork: "+original.Path);
            Assert.That(Settings(g),Is.EqualTo(original.ImageSettings),"Preserve minigame fill, crop and image settings: "+original.Path);
        }
        sealed class ButtonRecord
        {
            public readonly Button Button;public readonly int Id;public readonly string Path;public readonly string[] Calls;
            public ButtonRecord(Button button){Button=button;Id=button.GetInstanceID();Path=PathOf(button.transform);Calls=PersistentCalls(button);}
        }
        static string[] PersistentCalls(Button b)=>Enumerable.Range(0,b.onClick.GetPersistentEventCount()).Select(i=>
            (b.onClick.GetPersistentTarget(i)?b.onClick.GetPersistentTarget(i).GetInstanceID():0)+"|"+b.onClick.GetPersistentMethodName(i)+"|"+b.onClick.GetPersistentListenerState(i)).ToArray();
        void AssertButtons(IEnumerable<ButtonRecord> buttons)
        {
            foreach(var original in buttons)
            {
                var b=original.Button;Assert.That(b&&b.GetInstanceID()==original.Id,Is.True,original.Path);CollectionAssert.AreEqual(original.Calls,PersistentCalls(b),"Authored callbacks changed: "+original.Path);
                bool word=b.GetComponents<OriginalBehaviour>().Any(c=>c.sourceType=="DraggableUIElement");
                if(NativeMinigame(b.transform))
                {
                    Assert.That(b.GetComponentsInChildren<Image>(true).Where(i=>i.name.StartsWith("__SPARC_UI_",StringComparison.Ordinal)&&i.GetComponentInParent<Button>(true)==b),Is.Empty,"Preserve original minigame controls without replacing their icons: "+original.Path);
                    continue;
                }
                if(word||b.GetComponent<TMP_InputField>()||b.GetComponent<InputField>())continue;
                Assert.That(b.targetGraphic&&b.targetGraphic.raycastTarget,Is.True,"Redesigned control needs an actual pointer hit surface: "+original.Path);
                var glyphs=b.GetComponentsInChildren<Image>(true).Where(i=>i.name=="__SPARC_UI_Icon"&&i.GetComponentInParent<Button>(true)==b).ToArray();
                Assert.That(glyphs.Length,Is.EqualTo(1),"Every native non-word control needs exactly one new semantic glyph: "+original.Path);
                Assert.That(glyphs[0].sprite&&glyphs[0].sprite.name.StartsWith("Sketch / ",StringComparison.Ordinal),Is.True,original.Path);
                Assert.That(glyphs[0].raycastTarget,Is.False,"New icon must not intercept the original button");
            }
        }
        sealed class PhysicsRecord
        {
            public Component Collider;public string Shape;
        }
        PhysicsRecord[] PhysicsRecords()=>InScene<Collider>(runtime.gameObject.scene).Select(c=>new PhysicsRecord{Collider=c,Shape=Shape(c)}).Concat(InScene<Collider2D>(runtime.gameObject.scene).Select(c=>new PhysicsRecord{Collider=c,Shape=Shape(c)})).ToArray();
        static float[] V(Vector2 v)=>new[]{v.x,v.y};static float[] V(Vector3 v)=>new[]{v.x,v.y,v.z};
        static string Shape(Component component)
        {
            var data=new Dictionary<string,object>{{"type",component.GetType().Name},{"layer",component.gameObject.layer},{"scale",V(component.transform.localScale)}};
            if(component is Collider c){data["trigger"]=c.isTrigger;data["enabled"]=c.enabled;}
            if(component is Collider2D c2){data["trigger"]=c2.isTrigger;data["enabled"]=c2.enabled;data["offset"]=V(c2.offset);}
            switch(component)
            {
                case BoxCollider box:data["center"]=V(box.center);data["size"]=V(box.size);break;
                case SphereCollider sphere:data["center"]=V(sphere.center);data["radius"]=sphere.radius;break;
                case CapsuleCollider capsule:data["center"]=V(capsule.center);data["radius"]=capsule.radius;data["height"]=capsule.height;data["direction"]=capsule.direction;break;
                case MeshCollider mesh:data["mesh"]=mesh.sharedMesh?mesh.sharedMesh.GetInstanceID():0;data["convex"]=mesh.convex;break;
                case BoxCollider2D box2d:data["size"]=V(box2d.size);data["edgeRadius"]=box2d.edgeRadius;break;
                case CircleCollider2D circle:data["radius"]=circle.radius;break;
                case CapsuleCollider2D capsule2d:data["size"]=V(capsule2d.size);data["direction"]=capsule2d.direction.ToString();break;
                case PolygonCollider2D polygon:data["paths"]=Enumerable.Range(0,polygon.pathCount).Select(i=>polygon.GetPath(i).Select(V).ToArray()).ToArray();break;
                case EdgeCollider2D edge:data["points"]=edge.points.Select(V).ToArray();data["radius"]=edge.edgeRadius;break;
            }
            return JsonConvert.SerializeObject(data);
        }
        static void AssertPhysics(IEnumerable<PhysicsRecord> records)
        {foreach(var record in records){Assert.That(record.Collider,Is.Not.Null);Assert.That(Shape(record.Collider),Is.EqualTo(record.Shape),"UI redesign changed authored collider geometry: "+PathOf(record.Collider.transform));}}
        void WriteReport(string kind,IEnumerable<GraphicRecord> records,int buttons,int colliders)
        {
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/OriginalScene/UiRedesign"));Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder,"playmode-"+kind+"-coverage.json"),JsonConvert.SerializeObject(new{
                status="passed",scene=ScenePath,kind,checkedButtons=buttons,checkedColliders=colliders,
                nativeGraphics=records.Select(g=>new{path=g.Path,id=g.Id,type=g.Graphic.GetType().Name,active=g.Graphic.gameObject.activeInHierarchy,enabled=g.Graphic.enabled,sourceArtwork=g.OriginalArtwork,currentArtwork=ArtworkName(g.Graphic),originalMinigameAssetsPreserved=g.IsMini,sourceMediaPreserved=g.Media,sourceAlpha=g.InitialAlpha,invisibleInputHelper=g.InvisibleHelper,role=theme.RoleOf(g.Graphic)}),
                allClassifiedGraphics=theme.CoverageSnapshot()
            },Formatting.Indented));
        }
    }
}
