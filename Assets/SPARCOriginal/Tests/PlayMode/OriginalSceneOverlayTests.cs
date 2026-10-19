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
    public sealed class OriginalSceneOverlayTests
    {
        OriginalRuntime runtime;RenderTexture target;Camera camera;
        static string PathOf(Transform t)=>t.parent?PathOf(t.parent)+"/"+t.name:t.name;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);
            camera=new GameObject("Native teaching cue viewport").AddComponent<Camera>();camera.transform.position=new Vector3(1000,1000,-1000);camera.cullingMask=~0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.35f,.08f,.08f);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Scene overlay cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);if(target){target.Release();UnityEngine.Object.Destroy(target);}}
        [UnityTest] public IEnumerator NativeTeachingCuesAreReadableAtPortraitPhone(){yield return Cues(390,844,.63f);}
        [UnityTest] public IEnumerator NativeTeachingCuesAreReadableAtLandscapePhone(){yield return Cues(844,390,.63f);}
        [UnityTest] public IEnumerator NativeTeachingCuesAreReadableAtDesktop(){yield return Cues(1440,900,.79f);}
        IEnumerator Cues(int width,int height,float scale)
        {
            target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            var roots=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).Where(c=>PathOf(c.transform).EndsWith("/StorylineRBC_PointAhead/Subtitle1_CAP",StringComparison.Ordinal)||PathOf(c.transform).EndsWith("/Storyline_introduce_yellow_circle/Subtitle1_CAP",StringComparison.Ordinal)||PathOf(c.transform).EndsWith("/Storyline_introduce_task_list_fold/TIP",StringComparison.Ordinal)).ToArray();Assert.That(roots.Length,Is.EqualTo(3));
            foreach(var canvas in roots)
            {
                for(var ancestor=canvas.transform;ancestor;ancestor=ancestor.parent)ancestor.gameObject.SetActive(true);
                var content=canvas.transform.Find("Content") as RectTransform;Assert.That(content,Is.Not.Null);var texts=content.GetComponentsInChildren<TMP_Text>(true);var body=texts.Single(t=>t.name=="TMP_Text");var animator=content.GetComponent<Animator>();
                canvas.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=scale;Canvas.ForceUpdateCanvases();camera.Render();yield return null;yield return new WaitForSecondsRealtime(.55f);yield return null;Canvas.ForceUpdateCanvases();camera.Render();
                // The native Content entrance and reveal may legitimately advance on
                // activation. Compare styling against the settled state in this frame.
                var contentPosition=content.anchoredPosition;var contentParent=content.parent;var contentScale=content.localScale;bool animatorEnabled=animator&&animator.enabled;
                var records=texts.Select(t=>new{item=t,id=t.GetInstanceID(),text=t.text,parent=t.transform.parent,scale=t.transform.localScale,active=t.gameObject.activeSelf,enabled=t.enabled}).ToArray();
                foreach(var text in texts){var color=text.color;color.a=.65f;text.color=color;text.maxVisibleCharacters=4;}
                var groups=canvas.GetComponentsInChildren<CanvasGroup>(true).ToDictionary(g=>g,g=>g.alpha);
                runtime.GetComponent<OriginalUiTheme>().Refresh();Canvas.ForceUpdateCanvases();camera.Render();
                Assert.That(canvas.pixelRect.width,Is.EqualTo(width).Within(.1f));Assert.That(canvas.pixelRect.height,Is.EqualTo(height).Within(.1f));
                foreach(var text in texts)
                {
                    Assert.That(text.color.r,Is.EqualTo(OriginalUiTheme.Paper.r).Within(.001f));Assert.That(text.color.g,Is.EqualTo(OriginalUiTheme.Paper.g).Within(.001f));Assert.That(text.color.a,Is.EqualTo(.65f).Within(.001f));Assert.That(text.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth),Is.GreaterThan(0));Assert.That(text.maxVisibleCharacters,Is.EqualTo(4),"Styling must leave the native typewriter reveal untouched");
                    text.maxVisibleCharacters=int.MaxValue;text.ForceMeshUpdate(true,true);
                }
                Canvas.ForceUpdateCanvases();camera.Render();
                foreach(var record in records)
                {
                    var text=record.item;text.ForceMeshUpdate(true,true);Assert.That(text.GetInstanceID(),Is.EqualTo(record.id));Assert.That(text.text,Is.EqualTo(record.text));Assert.That(text.transform.parent,Is.SameAs(record.parent));Assert.That(text.transform.localScale,Is.EqualTo(record.scale));Assert.That(text.gameObject.activeSelf,Is.EqualTo(record.active));Assert.That(text.enabled,Is.EqualTo(record.enabled));
                    foreach(var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible))foreach(var corner in new[]{character.bottomLeft,character.topLeft,character.topRight,character.bottomRight})
                    {var p=camera.WorldToScreenPoint(text.transform.TransformPoint(corner));Assert.That(p.x,Is.InRange(15f,width-15f),PathOf(text.transform));Assert.That(p.y,Is.InRange(15f,height-15f),PathOf(text.transform));}
                    var origin=camera.WorldToScreenPoint(text.transform.TransformPoint(Vector3.zero));float unit=(camera.WorldToScreenPoint(text.transform.TransformPoint(Vector3.up*1024))-origin).magnitude/1024;
                    Assert.That(text.fontSize*unit,Is.EqualTo(text==body?20:28).Within(.15f),"Native child scale must be included in readable CSS font size");
                }
                foreach(var group in groups)Assert.That(group.Key.alpha,Is.EqualTo(group.Value));Assert.That(content.anchoredPosition,Is.EqualTo(contentPosition),"The Timeline-bound Content animation position must be untouched");Assert.That(content.parent,Is.SameAs(contentParent));Assert.That(content.localScale,Is.EqualTo(contentScale));Assert.That(animator&&animator.enabled,Is.EqualTo(animatorEnabled));
                Capture(canvas.transform.parent.name+"-"+width+"x"+height);canvas.gameObject.SetActive(false);
            }
            Assert.That(runtime.story.Trace,Is.Empty);
        }
        [UnityTest] public IEnumerator PendingAndWorldOverlayKeepNativeAlphaAndGeometry()
        {
            var all=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMP_Text>(true));var waiting=all.Single(t=>PathOf(t.transform).Contains("/WaitForResponse/P/",StringComparison.Ordinal));var portal=all.Single(t=>PathOf(t.transform).EndsWith("/Teleport/Canvas/Text (TMP)",StringComparison.Ordinal));
            var spinner=waiting.GetComponentInParent<Canvas>(true).GetComponentsInChildren<Image>(true).Single(i=>PathOf(i.transform).EndsWith("/WaitForResponse/P/Image",StringComparison.Ordinal));
            var objects=new Graphic[]{waiting,portal,spinner};var records=objects.Select(g=>new{item=g,text=(g as TMP_Text)?.text,parent=g.transform.parent,position=g.rectTransform.anchoredPosition,size=g.rectTransform.sizeDelta,scale=g.transform.localScale,rotation=g.transform.localRotation,active=g.gameObject.activeSelf,enabled=g.enabled,alpha=g.color.a}).ToArray();
            runtime.GetComponent<OriginalUiTheme>().Refresh();
            foreach(var record in records)
            {var item=record.item;Assert.That(item.color.a,Is.EqualTo(record.alpha));Assert.That(item.color.r,Is.EqualTo(OriginalUiTheme.Paper.r).Within(.001f));Assert.That(item.color.g,Is.EqualTo(OriginalUiTheme.Paper.g).Within(.001f));Assert.That(item.transform.parent,Is.SameAs(record.parent));Assert.That(item.rectTransform.anchoredPosition,Is.EqualTo(record.position));Assert.That(item.rectTransform.sizeDelta,Is.EqualTo(record.size));Assert.That(item.transform.localScale,Is.EqualTo(record.scale));Assert.That(item.transform.localRotation,Is.EqualTo(record.rotation));Assert.That(item.gameObject.activeSelf,Is.EqualTo(record.active));Assert.That(item.enabled,Is.EqualTo(record.enabled));if(item is TMP_Text text){Assert.That(text.text,Is.EqualTo(record.text));Assert.That(text.fontSharedMaterial.GetFloat(ShaderUtilities.ID_OutlineWidth),Is.GreaterThan(0));}}
            Assert.That(spinner.sprite,Is.SameAs(OriginalUiIcons.Get("time")));Assert.That(runtime.story.Trace,Is.Empty);yield return null;
        }
        void Capture(string name)
        {
            camera.Render();var old=RenderTexture.active;RenderTexture.active=target;var image=new Texture2D(target.width,target.height,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,target.width,target.height),0,0);image.Apply();
            var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/OriginalScene/LayoutFollowup"));Directory.CreateDirectory(dir);File.WriteAllBytes(Path.Combine(dir,"native-cue-"+name+".png"),image.EncodeToPNG());RenderTexture.active=old;UnityEngine.Object.Destroy(image);
        }
    }
}
