using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sparc.Original.Tests
{
    public sealed class OriginalHudLayoutTests
    {
        OriginalRuntime runtime;RenderTexture target;
        [UnitySetUp] public IEnumerator Setup()
        {yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);}
        [UnityTearDown] public IEnumerator Cleanup()
        {if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("HUD cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);if(target){target.Release();UnityEngine.Object.Destroy(target);}}
        [UnityTest] public IEnumerator CohesiveNativeObjectiveAndToolbarWorkAt1440By900(){yield return MeasureNativeRow(1440,900);}
        [UnityTest] public IEnumerator CohesiveNativeObjectiveAndToolbarWorkAt960By600(){yield return MeasureNativeRow(960,600);}
        [UnityTest] public IEnumerator PortraitToolbarFitsBeforeBeginAndAfterCanvasScaleSettles()
        {
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                Assert.That(runtime.Started,Is.False);runtime.hud.Repair();
                var canvas=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).Single(c=>c.name=="PlayerCanvas");
                for(var parent=canvas.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
                var buttons=canvas.GetComponentsInChildren<Button>(true).Where(b=>b.transform.parent.name=="TOPRightBtns").ToArray();Assert.That(buttons.Length,Is.EqualTo(4));var callbacks=buttons.Select(Calls).ToArray();
                var groups=canvas.GetComponentsInChildren<CanvasGroup>(true).ToDictionary(g=>g,g=>g.alpha);
                var camera=new GameObject("Pre-Begin phone HUD viewport").AddComponent<Camera>();camera.transform.position=new Vector3(1000,1000,-1000);camera.cullingMask=~0;camera.clearFlags=CameraClearFlags.SolidColor;
                target=new RenderTexture(390,844,24);target.Create();camera.targetTexture=target;canvas.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=1;
                foreach(float scale in new[]{1f,.63f})
                {
                    canvas.scaleFactor=scale;Canvas.ForceUpdateCanvases();camera.Render();yield return null;Canvas.ForceUpdateCanvases();yield return new WaitForSecondsRealtime(.35f);yield return null;Canvas.ForceUpdateCanvases();camera.Render();
                    Assert.That(canvas.pixelRect.width,Is.EqualTo(390).Within(.1f));Assert.That(canvas.pixelRect.height,Is.EqualTo(844).Within(.1f));Assert.That(buttons[0].transform.parent.GetComponent<HorizontalLayoutGroup>().enabled,Is.False,"The old single-row driver must not undo wrapped toolbar positions on the next Canvas rebuild");
                    var bounds=buttons.Select(b=>PixelBounds((RectTransform)b.transform,camera)).ToArray();
                    foreach(var rect in bounds){Assert.That(rect.xMin,Is.GreaterThanOrEqualTo(15));Assert.That(rect.xMax,Is.LessThanOrEqualTo(375));Assert.That(rect.yMin,Is.GreaterThan(844*.65f));Assert.That(rect.yMax,Is.LessThanOrEqualTo(829));Assert.That(rect.width,Is.GreaterThanOrEqualTo(43.9f));Assert.That(rect.height,Is.GreaterThanOrEqualTo(43.9f));}
                    for(int i=0;i<bounds.Length;i++)for(int j=i+1;j<bounds.Length;j++)Assert.That(bounds[i].Overlaps(bounds[j]),Is.False);
                    Assert.That(runtime.Started,Is.False,"The pre-start toolbar must recover before Begin");
                }
                foreach(var group in groups)Assert.That(group.Key.alpha,Is.EqualTo(group.Value),"Toolbar layout must leave native visibility and Timeline fades alone");
                for(int i=0;i<buttons.Length;i++)CollectionAssert.AreEqual(callbacks[i],Calls(buttons[i]));Assert.That(runtime.story.Trace,Is.Empty);
                Capture(camera,390,844,"before-begin");
            }
            finally{if(touch.added)InputSystem.RemoveDevice(touch);}
        }
        static string[] Calls(Button button)=>Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Select(i=>(button.onClick.GetPersistentTarget(i)?button.onClick.GetPersistentTarget(i).GetInstanceID():0)+"|"+button.onClick.GetPersistentMethodName(i)+"|"+button.onClick.GetPersistentListenerState(i)).ToArray();
        IEnumerator MeasureNativeRow(int width,int height)
        {
            var manager=runtime.First("ObjectiveHUDManager");var panel=manager.Component<RectTransform>("ObjectivePanel");
            var instance=UnityEngine.Object.Instantiate(manager.Object("PrimaryObjectivePrefab"),panel);instance.SetActive(true);
            var toast=instance.GetComponents<OriginalBehaviour>().Single(c=>c.sourceType=="ObjectiveToast");runtime.Register(toast);
            var objective=runtime.components.First(c=>c.Text("Description")=="- Follow CAP");
            var title=toast.Component<TMP_Text>("TitleTextContent");var description=toast.Component<TMP_Text>("DescriptionTextContent");
            title.text=objective.Text("Title");description.text=objective.Text("Description");toast.Component<TMP_Text>("CounterTextContent").text="";toast.Object("SubTitleRect").SetActive(true);
            runtime.hud.Repair();var fold=instance.GetComponentsInChildren<Button>(true).Single(b=>b.name=="ObjectiveButton");int foldId=fold.GetInstanceID();var callbacks=Calls(fold);
            var objectiveCanvas=panel.GetComponentInParent<Canvas>();var playerCanvas=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).Single(c=>c.name=="PlayerCanvas");
            var camera=new GameObject("HUD viewport camera").AddComponent<Camera>();camera.transform.position=new Vector3(1000,1000,-1000);camera.cullingMask=~0;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.86f,.88f,.85f);
            target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            foreach(var canvas in new[]{objectiveCanvas,playerCanvas})
            {
                for(var parent=canvas.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
                canvas.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=width/1440f;
            }
            runtime.GetComponent<OriginalUiTheme>().Refresh();runtime.hud.RefreshPresentation();Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(objectiveCanvas.pixelRect.width,Is.EqualTo(width).Within(1));Assert.That(objectiveCanvas.pixelRect.height,Is.EqualTo(height).Within(1));
            AssertCard(title,description,runtime.hud.ObjectiveCard,camera);
            Assert.That(description.fontSize,Is.GreaterThan(title.fontSize),"The source action instruction must have the strongest hierarchy");
            Assert.That(title.transform.parent.GetComponent<Image>().enabled,Is.False,"Context and action should share one card rather than disconnected native bars");
            Assert.That(toast.Component<RectTransform>("SubTitleRect").GetComponent<Image>().enabled,Is.False);
            var foldRect=PixelBounds((RectTransform)fold.transform,camera);Assert.That(foldRect.width,Is.GreaterThanOrEqualTo(43.9f));Assert.That(foldRect.height,Is.GreaterThanOrEqualTo(43.9f));
            var toolbar=playerCanvas.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="TOPRightBtns");var buttons=toolbar.GetComponentsInChildren<Button>(true);
            Assert.That(buttons.Length,Is.GreaterThanOrEqualTo(4));var bounds=buttons.Select(b=>PixelBounds((RectTransform)b.transform,camera)).ToArray();
            foreach(var rect in bounds)
            {
                Assert.That(rect.width,Is.GreaterThanOrEqualTo(43.9f));Assert.That(rect.height,Is.GreaterThanOrEqualTo(43.9f));Assert.That(rect.yMin,Is.GreaterThan(height*.75f),"Utility controls belong in the top safe region");
                Assert.That(rect.xMin,Is.GreaterThan(0));Assert.That(rect.xMax,Is.LessThan(width));Assert.That(rect.yMax,Is.LessThan(height));
                Assert.That(rect.Overlaps(PixelBounds(runtime.hud.ObjectiveCard,camera)),Is.False,"Toolbar must not overlap the objective card");
            }
            for(int i=0;i<bounds.Length;i++)for(int j=i+1;j<bounds.Length;j++)Assert.That(bounds[i].Overlaps(bounds[j]),Is.False);
            Assert.That(PixelBounds(runtime.hud.ObjectiveCard,camera).height,Is.LessThan(155),"A short task must not reserve an empty worksheet-sized card");
            Capture(camera,width,height,"expanded");
            float expandedHeight=runtime.hud.ObjectiveCard.rect.height;var text=description.text;
            fold.onClick.Invoke();runtime.hud.RefreshPresentation();Canvas.ForceUpdateCanvases();yield return null;
            Assert.That(instance.GetComponent<OriginalObjectiveCard>().DetailsExpanded,Is.False);Assert.That(description.enabled&&description.gameObject.activeInHierarchy,Is.True,"Folding details must leave the current action visible");
            Assert.That(description.text,Is.EqualTo(text));Assert.That(title.enabled,Is.False);Assert.That(runtime.hud.ObjectiveCard.rect.height,Is.LessThan(expandedHeight));
            Assert.That(fold.GetInstanceID(),Is.EqualTo(foldId));CollectionAssert.AreEqual(callbacks,Calls(fold));
            Capture(camera,width,height,"folded");
            fold.onClick.Invoke();runtime.hud.RefreshPresentation();Canvas.ForceUpdateCanvases();
            Assert.That(title.enabled,Is.True);Assert.That(title.text,Is.EqualTo(objective.Text("Title")));
            var longest=runtime.components.Where(c=>c.sourceType!=null&&c.sourceType.StartsWith("Objective",StringComparison.Ordinal)).OrderByDescending(c=>c.Text("Description").Length).First();
            description.text=longest.Text("Description");runtime.hud.RefreshPresentation();Canvas.ForceUpdateCanvases();yield return null;AssertCard(title,description,runtime.hud.ObjectiveCard,camera);
            Assert.That(runtime.story.Trace,Is.Empty,"Layout and folding must not alter the original story pipeline");
        }
        [UnityTest] public IEnumerator CompletionIndicatorReflectsTheOriginalObjectiveCompletionCount()
        {
            var objective=runtime.components.First(c=>c.Text("Description")=="- Follow CAP");runtime.objectives.Activated(objective);Canvas.ForceUpdateCanvases();
            Assert.That(runtime.hud.ObjectiveCard.rect.height*runtime.hud.ObjectiveCard.GetComponentInParent<Canvas>().scaleFactor,Is.GreaterThan(100),"A newly registered source task must receive its compact layout before its first fade-in frame");
            runtime.objectives.Reached(objective);yield return null;runtime.hud.RefreshPresentation();
            Assert.That(runtime.objectives.CompletedCount,Is.EqualTo(1));
            var progress=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMP_Text>(true)).Single(t=>t.name=="__SPARC_UI_ObjectiveProgress");
            Assert.That(progress.enabled,Is.True);Assert.That(progress.text,Is.EqualTo("1 completed"),"Use the real source completion counter rather than an invented percentage or stage index");
        }
        [UnityTest] public IEnumerator NewTaskRowsKeepTheirLayoutDuringOriginalCinematicsWithoutChangingFadeAlpha()
        {
            runtime.Begin();Assert.That(runtime.CinematicActive,Is.True);var manager=runtime.First("ObjectiveHUDManager");var panel=manager.Component<RectTransform>("ObjectivePanel");
            for(var parent=panel.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
            var instance=UnityEngine.Object.Instantiate(manager.Object("PrimaryObjectivePrefab"),panel);instance.SetActive(true);foreach(var source in instance.GetComponentsInChildren<OriginalBehaviour>(true))runtime.Register(source);
            var toast=instance.GetComponents<OriginalBehaviour>().Single(t=>t.sourceType=="ObjectiveToast");var title=toast.Component<TMP_Text>("TitleTextContent");var description=toast.Component<TMP_Text>("DescriptionTextContent");
            title.text="Meeting Cells";description.text="- Follow CAP";toast.Component<TMP_Text>("CounterTextContent").text="";toast.Object("SubTitleRect").SetActive(true);
            var group=toast.Component<CanvasGroup>("CanvasGroup");group.alpha=.37f;yield return new WaitForSecondsRealtime(.4f);Canvas.ForceUpdateCanvases();
            Assert.That(runtime.CinematicActive,Is.True);AssertCard(title,description,runtime.hud.ObjectiveCard,null);
            var pixels=runtime.hud.ObjectiveCard.rect.height*runtime.hud.ObjectiveCard.GetComponentInParent<Canvas>(true).scaleFactor;Assert.That(pixels,Is.InRange(100,155));Assert.That(group.alpha,Is.EqualTo(.37f).Within(.001f),"Task layout must not take ownership of original cinematic or toast fades");
            Assert.That(description.fontSize,Is.GreaterThan(title.fontSize));
        }
        [UnityTest] public IEnumerator CompletedTasksUseAHeaderBadgeWithoutGrowingTheCurrentTaskCard()
        {
            var follow=runtime.components.First(c=>c.Text("Description")=="- Follow CAP");runtime.objectives.Activated(follow);runtime.hud.Repair();runtime.objectives.Reached(follow);
            yield return new WaitForSecondsRealtime(2.8f);runtime.hud.RefreshPresentation();Canvas.ForceUpdateCanvases();
            var card=runtime.hud.ObjectiveCard;var progress=runtime.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMP_Text>(true)).Single(t=>t.name=="__SPARC_UI_ObjectiveProgress");
            Assert.That(progress.enabled,Is.True);Assert.That(progress.text,Is.EqualTo("1 completed"));
            Assert.That(card.rect.height*card.GetComponentInParent<Canvas>().scaleFactor,Is.LessThan(155),"Expired source toasts must not leave an empty card");
            Assert.That(card.GetComponentInParent<Canvas>().transform.InverseTransformPoint(progress.rectTransform.position).y,Is.GreaterThan(card.anchoredPosition.y-48/card.GetComponentInParent<Canvas>().scaleFactor),"Real progress belongs in the compact header");
            Assert.That(runtime.story.Trace,Is.Empty);
        }
        static Rect PixelBounds(RectTransform rect,Camera camera)
        {var points=new Vector3[4];rect.GetWorldCorners(points);var min=new Vector2(float.PositiveInfinity,float.PositiveInfinity);var max=new Vector2(float.NegativeInfinity,float.NegativeInfinity);foreach(var point in points){var pixel=RectTransformUtility.WorldToScreenPoint(camera,point);min=Vector2.Min(min,pixel);max=Vector2.Max(max,pixel);}return Rect.MinMaxRect(min.x,min.y,max.x,max.y);}
        static void AssertCard(TMP_Text title,TMP_Text description,RectTransform card,Camera camera)
        {
            var t=PixelBounds(title.rectTransform,camera);var d=PixelBounds(description.rectTransform,camera);var paper=PixelBounds(card,camera);
            Assert.That(t.Overlaps(d),Is.False,"Context and actionable instruction must not overlap");
            Assert.That(paper.Contains(t.center)&&paper.Contains(d.center),Is.True,"All source objective text belongs inside the common card");
            Assert.That(title.rectTransform.rect.height,Is.GreaterThanOrEqualTo(title.GetPreferredValues(title.text,title.rectTransform.rect.width,Mathf.Infinity).y-.5f));
            Assert.That(description.rectTransform.rect.height,Is.GreaterThanOrEqualTo(description.GetPreferredValues(description.text,description.rectTransform.rect.width,Mathf.Infinity).y-.5f));
        }
        void Capture(Camera camera,int width,int height,string state)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/OriginalScene/LayoutFollowup"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"native-hud-"+width+"x"+height+"-"+state+".png"),pixels.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);
        }
    }
}
