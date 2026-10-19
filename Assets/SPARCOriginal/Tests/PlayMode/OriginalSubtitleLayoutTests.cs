using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sparc.Original.Tests
{
    public sealed class OriginalSubtitleLayoutTests
    {
        OriginalRuntime runtime;RenderTexture target;
        [UnitySetUp] public IEnumerator Setup()
        {yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);}
        [UnityTearDown] public IEnumerator Cleanup()
        {if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Subtitle cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);if(target){target.Release();UnityEngine.Object.Destroy(target);}}
        [UnityTest] public IEnumerator OriginalLongIntroCaptionWrapsWithinPortraitPhone(){yield return Caption(390,844,.63f,true);}
        [UnityTest] public IEnumerator OriginalIntroCaptionKeepsDesktopFontPositionAndTimelineIdentity(){yield return Caption(1440,900,.79f,false);}
        IEnumerator Caption(int width,int height,float scale,bool portrait)
        {
            var canvas=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).Single(c=>c.name=="Subtitles");
            var captions=canvas.GetComponentsInChildren<TMP_Text>(true);Assert.That(captions.Length,Is.EqualTo(3));var text=captions.OrderByDescending(t=>t.text.Length).First();Assert.That(text.text,Does.Contain("many cells are working tirelessly"));
            var originals=captions.Select(t=>new{item=t,id=t.GetInstanceID(),parent=t.transform.parent,sibling=t.transform.GetSiblingIndex(),content=t.text,position=t.rectTransform.anchoredPosition}).ToArray();
            for(var parent=canvas.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
            foreach(var caption in captions)caption.gameObject.SetActive(caption==text);var sourceColor=text.color;sourceColor.a=.65f;text.color=sourceColor;
            var camera=new GameObject("Original intro caption viewport").AddComponent<Camera>();camera.transform.position=new Vector3(1000,1000,-1000);camera.cullingMask=~0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.08f,.08f);
            target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;canvas.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=scale;Canvas.ForceUpdateCanvases();
            // ScreenSpaceCamera roots are driven at preCull. Prime the actual target
            // before measuring parent units, then exercise the normal periodic reflow.
            camera.Render();yield return null;Canvas.ForceUpdateCanvases();runtime.GetComponent<OriginalUiTheme>().Refresh();Canvas.ForceUpdateCanvases();camera.Render();yield return new WaitForSecondsRealtime(.35f);yield return null;Canvas.ForceUpdateCanvases();camera.Render();text.ForceMeshUpdate(true,true);
            Assert.That(canvas.pixelRect.width,Is.EqualTo(width).Within(.1f));Assert.That(canvas.pixelRect.height,Is.EqualTo(height).Within(.1f));
            var points=new Vector3[4];text.rectTransform.GetWorldCorners(points);var pixels=points.Select(p=>camera.WorldToScreenPoint(p)).ToArray();
            var captionParent=text.transform.parent;var parentOrigin=RectTransformUtility.WorldToScreenPoint(camera,captionParent.TransformPoint(Vector3.zero));var textOrigin=RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint(Vector3.zero));
            float parentOne=(RectTransformUtility.WorldToScreenPoint(camera,captionParent.TransformPoint(Vector3.right))-parentOrigin).magnitude;
            float parentWide=(RectTransformUtility.WorldToScreenPoint(camera,captionParent.TransformPoint(Vector3.right*1024))-parentOrigin).magnitude/1024;
            float textWide=(RectTransformUtility.WorldToScreenPoint(camera,text.transform.TransformPoint(Vector3.right*1024))-textOrigin).magnitude/1024;
            var rootCorners=new Vector3[4];((RectTransform)canvas.transform).GetWorldCorners(rootCorners);var rootPixels=rootCorners.Select(p=>camera.WorldToScreenPoint(p)).ToArray();
            TestContext.WriteLine($"Native caption projection: parentOne={parentOne:R}, parentWide={parentWide:R}, textWide={textWide:R}, textLocalScale={text.transform.localScale}, parentLossyScale={captionParent.lossyScale}, canvasScale={canvas.scaleFactor:R}, rootX={rootPixels.Min(p=>p.x):R}..{rootPixels.Max(p=>p.x):R}, textX={pixels.Min(p=>p.x):R}..{pixels.Max(p=>p.x):R}, localWidth={text.rectTransform.rect.width:R}");
            Capture(camera,width,height);
            Assert.That(pixels.Min(p=>p.x),Is.GreaterThanOrEqualTo(15));Assert.That(pixels.Max(p=>p.x),Is.LessThanOrEqualTo(width-15));Assert.That(pixels.Min(p=>p.y),Is.GreaterThanOrEqualTo(0));Assert.That(pixels.Max(p=>p.y),Is.LessThanOrEqualTo(height));
            Assert.That(text.textWrappingMode,Is.EqualTo(TextWrappingModes.Normal));Assert.That(text.fontSize*scale,Is.GreaterThanOrEqualTo(17.9f));Assert.That(text.rectTransform.rect.height,Is.GreaterThanOrEqualTo(text.GetPreferredValues(text.text,text.rectTransform.rect.width,Mathf.Infinity).y));
            if(portrait)Assert.That(text.textInfo.lineCount,Is.GreaterThanOrEqualTo(2),"The actual long source caption must wrap instead of overflowing the phone edges");else Assert.That(text.fontSize,Is.EqualTo(32).Within(.1f),"Desktop retains the established logical font size");
            foreach(var glyph in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible))
            {
                foreach(var corner in new[]{glyph.bottomLeft,glyph.topLeft,glyph.topRight,glyph.bottomRight})
                {var point=camera.WorldToScreenPoint(text.transform.TransformPoint(corner));Assert.That(point.x,Is.InRange(15f,width-15f),"Actual native caption glyphs must stay inside the portrait safe width");Assert.That(point.y,Is.InRange(0f,(float)height));}
            }
            foreach(var original in originals){Assert.That(original.item.GetInstanceID(),Is.EqualTo(original.id));Assert.That(original.item.transform.parent,Is.SameAs(original.parent));Assert.That(original.item.transform.GetSiblingIndex(),Is.EqualTo(original.sibling));Assert.That(original.item.text,Is.EqualTo(original.content));Assert.That(original.item.rectTransform.anchoredPosition,Is.EqualTo(original.position));Assert.That(original.item.gameObject.activeSelf,Is.EqualTo(original.item==text));}
            Assert.That(text.color.a,Is.EqualTo(.65f).Within(.001f),"Native caption fade alpha must remain controlled by the source");Assert.That(runtime.story.Trace,Is.Empty);
        }
        void Capture(Camera camera,int width,int height)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(width,height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/OriginalScene/LayoutFollowup"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"native-intro-caption-"+width+"x"+height+".png"),image.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.Destroy(image);
        }
    }
}
