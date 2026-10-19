using System;
using System.Collections;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sparc.Original.Tests
{
    public sealed class NativeWorksheetLayoutTests
    {
        OriginalRuntime runtime;OriginalBehaviour manager;OriginalMobileWorksheet layout;Canvas canvas;Camera camera;RenderTexture target;
        GameObject[] pages;Image[] nativeArt;Sprite[] sprites;Material[] materials;string[] words;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;
            runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);
            manager=runtime.First("WordGameManager");Assert.That(manager,Is.Not.Null);
            for(var p=manager.transform;p;p=p.parent)p.gameObject.SetActive(true);
            runtime.activities.Activated(manager);yield return null;
            layout=runtime.GetComponent<OriginalMobileWorksheet>()??runtime.gameObject.AddComponent<OriginalMobileWorksheet>();layout.runtime=runtime;
            canvas=manager.GetComponentInParent<Canvas>();Assert.That(canvas,Is.Not.Null);
            pages=((JArray)manager.Data["wordGamePages"]).Select(t=>OriginalBehaviour.AsGameObject(manager.Resolve(t))).ToArray();
            Assert.That(pages.Length,Is.EqualTo(13));Assert.That(manager.GetComponentsInChildren<OriginalWordSlot>(true).Length,Is.EqualTo(14));
            nativeArt=manager.GetComponentsInChildren<Image>(true);sprites=nativeArt.Select(i=>i.sprite).ToArray();materials=nativeArt.Select(i=>i.material).ToArray();
            words=manager.GetComponentsInChildren<OriginalWordTile>(true).Select(t=>t.Word).OrderBy(w=>w).ToArray();
            foreach(var other in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))if(other&&!other.transform.IsChildOf(manager.transform))other.enabled=false;
            camera=new GameObject("Native mobile worksheet viewport").AddComponent<Camera>();camera.cullingMask=~0;camera.transform.position=new Vector3(1000,1000,-1000);camera.orthographic=true;camera.orthographicSize=5;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.1f,.12f);
            var scaler=canvas.GetComponent<CanvasScaler>();if(scaler)scaler.enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=1;
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            OriginalWordTile.ClearSelection();if(runtime)runtime.Shutdown();if(camera)camera.targetTexture=null;
            if(target){target.Release();UnityEngine.Object.Destroy(target);}if(camera)UnityEngine.Object.Destroy(camera.gameObject);
            var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Worksheet layout cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);
        }
        [UnityTest] public IEnumerator NativeWorksheetTargetsAndArtAt390By844(){yield return Verify(390,844);}
        [UnityTest] public IEnumerator NativeWorksheetTargetsAndArtAt844By390(){yield return Verify(844,390);}
        [UnityTest] public IEnumerator NotebookTargetsAndSourceWordsAt1440By900(){yield return Verify(1440,900);}
        [UnityTest] public IEnumerator NotebookTargetsAndSourceWordsAt960By940(){yield return Verify(960,940);}
        [UnityTest] public IEnumerator NativeBankCancellationKeepsEveryWordAtItsRenderedHomeAfterFirstLayout()
        {
            target=new RenderTexture(390,844,24);target.Create();camera.targetTexture=target;runtime.activities.Call(manager,"NextPage",null);var page=pages[1];
            layout.ReflowForViewport(page,canvas,new Rect(0,0,390,844),new Vector2Int(390,844),1);yield return null;Canvas.ForceUpdateCanvases();
            var tiles=page.GetComponentsInChildren<OriginalWordTile>(true);Assert.That(tiles.Length,Is.EqualTo(3));var before=tiles.Select(t=>Bounds((RectTransform)t.transform)).ToArray();
            tiles[0].OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));layout.ClearInteraction();Canvas.ForceUpdateCanvases();
            for(int i=0;i<tiles.Length;i++){var after=Bounds((RectTransform)tiles[i].transform);Assert.That(Vector2.Distance(after.position,before[i].position),Is.LessThan(.1f),"Cancellation must not shift the bank by its pre-layout pivot offset");Assert.That(after.size,Is.EqualTo(before[i].size));Assert.That(tiles[i].Slot,Is.Null);Assert.That(tiles[i].GetComponent<CanvasGroup>().blocksRaycasts,Is.True);}
            Assert.That(OriginalWordTile.Selected,Is.Null);Assert.That(runtime.story.Trace,Is.Empty);
        }
        [UnityTest] public IEnumerator NativeWorksheetReflowsScaleAndSafeAreaWithoutViewportResizeAndKeepsAnswers()
        {
            const int width=1055,height=488;target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            for(int i=0;i<3;i++)runtime.activities.Call(manager,"NextPage",null);
            var page=pages[3];var pixels=new Vector2Int(width,height);canvas.scaleFactor=1;Canvas.ForceUpdateCanvases();
            layout.ReflowForViewport(page,canvas,new Rect(0,0,width,height),pixels,1);yield return null;Canvas.ForceUpdateCanvases();
            var tiles=page.GetComponentsInChildren<OriginalWordTile>(true);var blanks=page.GetComponentsInChildren<OriginalWordSlot>(true);
            Assert.That(tiles.Length,Is.EqualTo(12));Assert.That(blanks.Length,Is.EqualTo(3));blanks[0].Accept(tiles[0]);yield return new WaitForSecondsRealtime(tiles[0].duration+.04f);AssertAssignedGeometry(tiles[0],blanks[0]);
            // Actual native drop order assigns the blank while the drag still owns
            // the word. Cancellation must keep that accepted word centered.
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current){pointerId=73,position=Bounds(blanks[1].transform as RectTransform).center,pointerDrag=tiles[1].gameObject,pointerPressRaycast=new UnityEngine.EventSystems.RaycastResult{module=canvas.GetComponent<GraphicRaycaster>()}};
            tiles[1].OnBeginDrag(pointer);blanks[1].OnDrop(pointer);yield return new WaitForSecondsRealtime(.04f);tiles[1].CancelDrag();AssertAssignedGeometry(tiles[1],blanks[1]);Assert.That(tiles[1].IsDragging,Is.False);Assert.That(tiles[1].GetComponent<CanvasGroup>().blocksRaycasts,Is.True);
            // Rotate/reflow before this original placement animation finishes. Its
            // old world destination must not overwrite the new viewport center.
            blanks[2].Accept(tiles[2]);tiles[3].ReturnHome();yield return new WaitForSecondsRealtime(.04f);
            tiles[4].OnPointerClick(new UnityEngine.EventSystems.PointerEventData(null));Assert.That(OriginalWordTile.Selected,Is.SameAs(tiles[4]));
            canvas.scaleFactor=.7f;Canvas.ForceUpdateCanvases();const float density=1.25f;var safe=new Rect(20,10,width-40,height-20);
            layout.ReflowForViewport(page,canvas,safe,pixels,density);yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(blanks[0].Current,Is.SameAs(tiles[0]),"Canvas/DPR/safe-area reflow must keep the original assigned slot reference");Assert.That(tiles[0].Slot,Is.SameAs(blanks[0]));Assert.That(OriginalWordTile.Selected,Is.Null,"Viewport changes cancel stale selected contacts");
            var unassignedNewHome=tiles[3].transform.localPosition;
            yield return new WaitForSecondsRealtime(Mathf.Max(tiles[2].duration,tiles[3].duration)+.04f);Canvas.ForceUpdateCanvases();for(int i=0;i<3;i++)AssertAssignedGeometry(tiles[i],blanks[i]);
            Assert.That(Vector3.Distance(tiles[3].transform.localPosition,unassignedNewHome),Is.LessThan(.01f),"An interrupted ReturnHome must not overwrite the new mobile bank geometry");
            foreach(var tile in tiles)Assert.That(Bounds(tile.transform as RectTransform).height/density,Is.GreaterThanOrEqualTo(43.5f));
            foreach(var blank in blanks)Assert.That(Bounds(blank.transform as RectTransform).height/density,Is.GreaterThanOrEqualTo(43.5f));
            var next=manager.Component<TMP_Text>("nextButtonText").GetComponentInParent<Button>();var navigation=Bounds(next.transform as RectTransform);
            Assert.That(navigation.height/density,Is.GreaterThanOrEqualTo(43.5f));Assert.That(navigation.xMax,Is.LessThanOrEqualTo(safe.xMax+1));Assert.That(navigation.yMin,Is.GreaterThanOrEqualTo(safe.yMin-1));
            // Safe-area-only changes must also be observed when density and canvas scale stay fixed.
            var promptBefore=Bounds(layout.PromptViewport);safe.x+=12;safe.width-=12;layout.ReflowForViewport(page,canvas,safe,pixels,density);Canvas.ForceUpdateCanvases();
            Assert.That(Bounds(layout.PromptViewport).xMin-promptBefore.xMin,Is.EqualTo(12).Within(1));Assert.That(blanks[0].Current,Is.SameAs(tiles[0]));for(int i=0;i<3;i++)AssertAssignedGeometry(tiles[i],blanks[i]);
        }
        IEnumerator Verify(int width,int height)
        {
            target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            int currentIndex=0;
            foreach(int index in new[]{1,3,12})
            {
                while(currentIndex<index){runtime.activities.Call(manager,"NextPage",null);currentIndex++;}
                Assert.That(pages[index].activeInHierarchy,Is.True,"Use the actual original page navigation state");
                Canvas.ForceUpdateCanvases();layout.ReflowForViewport(pages[index],canvas,new Rect(0,0,width,height),new Vector2Int(width,height),1);
                yield return null;Canvas.ForceUpdateCanvases();
                var tiles=pages[index].GetComponentsInChildren<OriginalWordTile>(true);var blanks=pages[index].GetComponentsInChildren<OriginalWordSlot>(true);
                if(index==3){Assert.That(tiles.Length,Is.EqualTo(12));Assert.That(blanks.Length,Is.EqualTo(3));}
                foreach(var tile in tiles){var bounds=Bounds(tile.transform as RectTransform);Assert.That(bounds.height,Is.GreaterThanOrEqualTo(43.5f),"Visible word target height: "+tile.Word);Assert.That(bounds.width,Is.GreaterThanOrEqualTo(43.5f));}
                var boxes=tiles.Select(t=>Bounds(t.transform as RectTransform)).ToArray();for(int i=0;i<boxes.Length;i++)for(int j=i+1;j<boxes.Length;j++)Assert.That(boxes[i].Overlaps(boxes[j]),Is.False,"Actual native word tiles overlap");
                foreach(var blank in blanks)Assert.That(Bounds(blank.transform as RectTransform).height,Is.GreaterThanOrEqualTo(43.5f),"Native blank needs a real44px target");
                foreach(var input in pages[index].GetComponentsInChildren<TMP_InputField>(true))Assert.That(Bounds(input.transform as RectTransform).height,Is.GreaterThanOrEqualTo(43.5f));
                var next=manager.Component<TMP_Text>("nextButtonText").GetComponentInParent<Button>();var navigation=Bounds(next.transform as RectTransform);
                var nextText=manager.Component<TMP_Text>("nextButtonText");Assert.That(nextText.GetPreferredValues(nextText.text,Mathf.Infinity,Mathf.Infinity).x,Is.LessThanOrEqualTo(nextText.rectTransform.rect.width+.5f),"Native Next/Submit text must fit on one readable line");
                Assert.That(nextText.textWrappingMode,Is.EqualTo(TextWrappingModes.NoWrap));
                var frame=manager.transform.Find("Frame");Assert.That(pages[index].transform.GetSiblingIndex(),Is.GreaterThan(frame.GetSiblingIndex()),"Original tablet frame must stay behind the native controls");Assert.That(next.transform.GetSiblingIndex(),Is.GreaterThan(pages[index].transform.GetSiblingIndex()),"Next must remain above all worksheet content");
                var cue=pages[index].GetComponentsInChildren<Image>(true).FirstOrDefault(i=>i.name=="Image"&&i.transform.parent.name=="MobileWorksheetLayout");if(cue)Assert.That(boxes.Any(box=>box.Overlaps(Bounds(cue.rectTransform))),Is.False,"Native teaching cue must not cover a word target");
                Assert.That(navigation.height,Is.GreaterThanOrEqualTo(43.5f));Assert.That(navigation.xMin,Is.GreaterThanOrEqualTo(-1));Assert.That(navigation.yMin,Is.GreaterThanOrEqualTo(-1));Assert.That(navigation.xMax,Is.LessThanOrEqualTo(width+1));Assert.That(navigation.yMax,Is.LessThanOrEqualTo(height+1));
                CheckScroll(layout.PromptViewport);if(tiles.Length>0)CheckScroll(layout.WordBankViewport);
                Assert.That(manager.GetComponentsInChildren<OriginalWordSlot>(true).Length,Is.EqualTo(14));CollectionAssert.AreEqual(words,manager.GetComponentsInChildren<OriginalWordTile>(true).Select(t=>t.Word).OrderBy(w=>w).ToArray());
                for(int i=0;i<nativeArt.Length;i++)
                {
                    var image=nativeArt[i];bool redesigned=image.GetComponent<OriginalWordTile>()||image.GetComponent<OriginalWordSlot>()||image.GetComponentInParent<TMP_InputField>(true)||image.GetComponent<Button>();
                    if(redesigned&&image.sprite!=sprites[i])Assert.That(image.sprite&&image.sprite.name.StartsWith("SPARC pixel UI / ",StringComparison.Ordinal),Is.True,"Activated notebook controls use the new pixel frame; unvisited pages retain their source art: "+image.name);
                    else {Assert.That(image.sprite,Is.SameAs(sprites[i]),"Source educational artwork must survive");Assert.That(image.material,Is.SameAs(materials[i]),"Source educational material must survive");}
                }
                Capture(width,height,index+1);
            }
        }
        void AssertAssignedGeometry(OriginalWordTile tile,OriginalWordSlot slot)
        {
            Assert.That(slot.Current,Is.SameAs(tile));Assert.That(tile.Slot,Is.SameAs(slot));
            var word=tile.transform as RectTransform;var blank=slot.transform as RectTransform;
            var actual=new Vector3[4];var expected=new Vector3[4];word.GetWorldCorners(actual);blank.GetWorldCorners(expected);
            Assert.That(Vector3.Distance(word.TransformPoint(word.rect.center),blank.TransformPoint(blank.rect.center)),Is.LessThan(.001f),"The native placed word must share the blank's visible center, independent of its pivot");
            for(int i=0;i<4;i++)Assert.That(Vector3.Distance(actual[i],expected[i]),Is.LessThan(.001f),"Native placed word corner "+i+" must align with the assigned blank");
        }
        void CheckScroll(RectTransform viewport)
        {
            Assert.That(viewport,Is.Not.Null);var scroll=viewport.GetComponent<ScrollRect>();Assert.That(scroll,Is.Not.Null);
            if(scroll.content.rect.height>viewport.rect.height+.5f){Assert.That(scroll.vertical,Is.True);Assert.That(scroll.verticalScrollbar,Is.Not.Null);Assert.That(scroll.verticalScrollbar.gameObject.activeSelf,Is.True,"Overflow must expose a real scroll control");}
        }
        Rect Bounds(RectTransform r)
        {var corners=new Vector3[4];r.GetWorldCorners(corners);var p=corners.Select(v=>RectTransformUtility.WorldToScreenPoint(camera,v)).ToArray();return Rect.MinMaxRect(p.Min(v=>v.x),p.Min(v=>v.y),p.Max(v=>v.x),p.Max(v=>v.y));}
        void Capture(int width,int height,int page)
        {
            foreach(var text in manager.GetComponentsInChildren<TMP_Text>())text.ForceMeshUpdate();camera.Render();var old=RenderTexture.active;RenderTexture.active=target;
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
            var directory=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/OriginalScene/LayoutFollowup"));Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,$"worksheet-{width}x{height}-page{page:00}.png"),pixels.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.Destroy(pixels);
        }
    }
}
