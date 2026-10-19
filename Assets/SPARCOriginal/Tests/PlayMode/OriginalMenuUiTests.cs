using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sparc.Original.Tests
{
    public sealed class OriginalMenuUiTests
    {
        OriginalRuntime runtime;OriginalMenuUi menu;RenderTexture target;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;
            runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);
            menu=runtime.GetComponent<OriginalMenuUi>();Assert.That(menu,Is.Not.Null,"Native settings bridge must be installed by the original runtime");menu.Initialize();
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();SceneManager.SetActiveScene(SceneManager.CreateScene("Menu cleanup "+Guid.NewGuid()));yield return SceneManager.UnloadSceneAsync(old);
            if(target){target.Release();UnityEngine.Object.Destroy(target);}
        }
        [UnityTest] public IEnumerator OriginalSettingAndBackCallbacksRevealUsableMenuAt1440By900(){yield return OpenAndClose(1440,900,false);}
        [UnityTest] public IEnumerator OriginalSettingAndBackCallbacksRevealUsableMenuAt960By600(){yield return OpenAndClose(960,600,true);}
        static string[] Calls(Button button)=>Enumerable.Range(0,button.onClick.GetPersistentEventCount()).Select(i=>(button.onClick.GetPersistentTarget(i)?button.onClick.GetPersistentTarget(i).GetInstanceID():0)+"|"+button.onClick.GetPersistentMethodName(i)+"|"+button.onClick.GetPersistentListenerState(i)).ToArray();
        IEnumerator OpenAndClose(int width,int height,bool previousLock)
        {
            var source=runtime.First("SettingPanel");var panel=source.Object("panelRoot");var canvas=panel.GetComponent<Canvas>();var group=source.Component<CanvasGroup>("canvasGroup");var back=source.Component<Button>("closeButton");
            var settings=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsInactive.Include,FindObjectsSortMode.None).Single(b=>b.gameObject.scene==runtime.gameObject.scene&&b.name=="SettingBtn");var hud=settings.GetComponentInParent<Canvas>(true);
            var settingsCalls=Calls(settings);var backCalls=Calls(back);Assert.That(settingsCalls.Length,Is.EqualTo(2));Assert.That(backCalls.Length,Is.EqualTo(2));
            for(var parent=hud.transform;parent;parent=parent.parent)parent.gameObject.SetActive(true);
            var objectives=runtime.First("MeetingCellsObjectiveHudTopLeftLock").gameObject;bool previousObjectives=!previousLock;objectives.SetActive(previousObjectives);
            runtime.player.InputLocked=previousLock;
            settings.onClick.Invoke();
            Assert.That(menu.IsOpen,Is.True,"Source SetActive alone leaves alpha zero; the source ShowPanel bridge must reveal the same native panel");
            Assert.That(panel.activeInHierarchy,Is.True);Assert.That(group.alpha,Is.EqualTo(1));Assert.That(group.interactable&&group.blocksRaycasts,Is.True);Assert.That(hud.gameObject.activeSelf,Is.False);Assert.That(runtime.player.InputLocked,Is.True);Assert.That(objectives.activeSelf,Is.False,"Objective HUD must not show through a modal Settings card");
            // Use a real camera viewport so an Editor GameView cannot silently replace
            // the requested dimensions. The same original menu and callbacks are used.
            var camera=new GameObject("Settings viewport camera").AddComponent<Camera>();camera.transform.position=new Vector3(1000,1000,-1000);camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=OriginalUiTheme.Paper;
            target=new RenderTexture(width,height,24);target.Create();camera.targetTexture=target;
            canvas.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=width/1440f;
            menu.RefreshLayoutForViewport();runtime.GetComponent<OriginalUiTheme>().Refresh();Canvas.ForceUpdateCanvases();yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(canvas.pixelRect.width,Is.EqualTo(width).Within(1));Assert.That(canvas.pixelRect.height,Is.EqualTo(height).Within(1));
            var bounds=Pixels((RectTransform)back.transform,camera);Assert.That(bounds.width,Is.GreaterThanOrEqualTo(43.9f));Assert.That(bounds.height,Is.GreaterThanOrEqualTo(43.9f));
            var paper=panel.GetComponentsInChildren<RectTransform>(true).Single(t=>t.name=="Background (1)");var paperBounds=Pixels(paper,camera);Assert.That(paperBounds.width,Is.LessThanOrEqualTo(761));Assert.That(paperBounds.height,Is.LessThanOrEqualTo(441));
            var scaleLabel=panel.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t=>t.name=="ScreenScaleText");Assert.That(scaleLabel.fontSize*canvas.scaleFactor,Is.GreaterThanOrEqualTo(17.9f));
            var slider=source.Component<Slider>("renderScaleSlider");Assert.That(Pixels((RectTransform)slider.transform,camera).height,Is.GreaterThanOrEqualTo(43.9f));
            Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(0));Assert.That(bounds.yMin,Is.GreaterThanOrEqualTo(0));Assert.That(bounds.xMax,Is.LessThanOrEqualTo(width));Assert.That(bounds.yMax,Is.LessThanOrEqualTo(height));
            var visibleButtons=panel.GetComponentsInChildren<Button>().Where(button=>button.isActiveAndEnabled).ToArray();
            CollectionAssert.AreEquivalent(new[]{"BackButton"},visibleButtons.Select(button=>button.name),"Standalone settings should expose working controls; retain portal-only and unimplemented source widgets inactive");
            var pointer=new PointerEventData(EventSystem.current){position=bounds.center};var hits=new List<RaycastResult>();canvas.GetComponent<GraphicRaycaster>().Raycast(pointer,hits);
            Assert.That(hits.Any(hit=>hit.gameObject.GetComponentInParent<Button>()==back),Is.True,"The actual native BackButton must be pointer-reachable at this viewport");
            Capture(camera,width,height);
            back.onClick.Invoke();Assert.That(menu.IsOpen,Is.False);Assert.That(panel.activeSelf,Is.False);Assert.That(group.alpha,Is.EqualTo(0));Assert.That(group.interactable||group.blocksRaycasts,Is.False);Assert.That(hud.gameObject.activeInHierarchy,Is.True);Assert.That(runtime.player.InputLocked,Is.EqualTo(previousLock),"Closing settings must restore a prior cinematic lock rather than unconditionally unlock the player");
            Assert.That(objectives.activeSelf,Is.EqualTo(previousObjectives),"Closing settings must restore the source objective HUD visibility");
            CollectionAssert.AreEqual(settingsCalls,Calls(settings));CollectionAssert.AreEqual(backCalls,Calls(back));
            Assert.That(runtime.story.Trace,Is.Empty,"Settings must not execute or replace the authored story pipeline");
        }
        static Rect Pixels(RectTransform rect,Camera camera)
        {var corners=new Vector3[4];rect.GetWorldCorners(corners);var points=corners.Select(p=>(Vector2)camera.WorldToScreenPoint(p)).ToArray();return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));}
        void Capture(Camera camera,int width,int height)
        {
            camera.Render();var previous=RenderTexture.active;RenderTexture.active=target;var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
            var folder=Path.GetFullPath(Path.Combine(Application.dataPath,"../Reports/OriginalScene/LayoutFollowup"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"native-settings-"+width+"x"+height+".png"),pixels.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.Destroy(pixels);
        }
        [UnityTest] public IEnumerator NativeRenderScaleSliderUsesSourceRoundingAndRestoresPipelineOnUnload()
        {
            var source=runtime.First("SettingPanel");var slider=source.Component<Slider>("renderScaleSlider");var label=source.Component<TMPro.TMP_Text>("renderScaleValueText");
            var pipeline=QualitySettings.renderPipeline??GraphicsSettings.currentRenderPipeline;Assert.That(pipeline,Is.Not.Null);var property=pipeline.GetType().GetProperty("renderScale");Assert.That(property,Is.Not.Null);float initial=Convert.ToSingle(property.GetValue(pipeline));
            slider.value=.86f;Assert.That(slider.value,Is.EqualTo(.9f).Within(.001));Assert.That(Convert.ToSingle(property.GetValue(pipeline)),Is.EqualTo(.9f).Within(.001));Assert.That(label.text,Is.EqualTo("0.90x"));
            UnityEngine.Object.Destroy(menu);yield return null;Assert.That(Convert.ToSingle(property.GetValue(pipeline)),Is.EqualTo(initial).Within(.001));
        }
        [UnityTest] public IEnumerator NativeSettingsRepairsCanvasScaleOnlySettlingWithoutResizing()
        {
            var source=runtime.First("SettingPanel");menu.ShowPanel();var canvas=menu.NativePanel.GetComponent<Canvas>();var back=source.Component<Button>("closeButton");var calls=Calls(back);
            var camera=new GameObject("Settled Settings viewport").AddComponent<Camera>();camera.transform.position=new Vector3(1000,1000,-1000);target=new RenderTexture(390,844,24);target.Create();camera.targetTexture=target;
            canvas.GetComponent<CanvasScaler>().enabled=false;canvas.renderMode=RenderMode.ScreenSpaceCamera;canvas.worldCamera=camera;canvas.planeDistance=1;canvas.scaleFactor=1;Canvas.ForceUpdateCanvases();menu.RefreshLayoutForViewport();
            int screenWidth=Screen.width,screenHeight=Screen.height;var pixels=canvas.pixelRect;
            Assert.That(Pixels((RectTransform)back.transform,camera).width,Is.EqualTo(44).Within(.1f));canvas.scaleFactor=.63f;Canvas.ForceUpdateCanvases();Assert.That(Pixels((RectTransform)back.transform,camera).width,Is.LessThan(30));
            Assert.That(menu.RefreshViewportChanges(),Is.True,"Open Settings must detect CanvasScaler settling without a Screen resize");Canvas.ForceUpdateCanvases();
            var bounds=Pixels((RectTransform)back.transform,camera);Assert.That(bounds.width,Is.EqualTo(44).Within(.1f));Assert.That(bounds.height,Is.EqualTo(44).Within(.1f));Assert.That(bounds.xMin,Is.GreaterThanOrEqualTo(0));Assert.That(bounds.xMax,Is.LessThanOrEqualTo(390));
            var label=menu.NativePanel.GetComponentsInChildren<TMPro.TMP_Text>(true).Single(t=>t.name=="ScreenScaleText");Assert.That(label.fontSize*canvas.scaleFactor,Is.EqualTo(18).Within(.1f));
            Assert.That(Screen.width,Is.EqualTo(screenWidth));Assert.That(Screen.height,Is.EqualTo(screenHeight));Assert.That(canvas.pixelRect,Is.EqualTo(pixels));Assert.That(menu.RefreshViewportChanges(),Is.False);
            back.onClick.Invoke();Assert.That(menu.IsOpen,Is.False);CollectionAssert.AreEqual(calls,Calls(back));Assert.That(runtime.story.Trace,Is.Empty);yield return null;
        }
    }
}
