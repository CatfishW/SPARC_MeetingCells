using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Sparc.Original.Tests
{
    public sealed class OriginalMobileControlsLayoutTests
    {
        OriginalRuntime runtime;
        [UnitySetUp] public IEnumerator Setup()
        {
            yield return SceneManager.LoadSceneAsync("Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity");yield return null;
            runtime=UnityEngine.Object.FindFirstObjectByType<OriginalRuntime>();Assert.That(runtime,Is.Not.Null);
        }
        [UnityTearDown] public IEnumerator Cleanup()
        {
            if(runtime)runtime.Shutdown();var old=SceneManager.GetActiveScene();
            SceneManager.SetActiveScene(SceneManager.CreateScene("Mobile canvas cleanup "+Guid.NewGuid()));
            yield return SceneManager.UnloadSceneAsync(old);
        }
        static float ScreenWidth(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            return Mathf.Abs(RectTransformUtility.WorldToScreenPoint(null,corners[2]).x-RectTransformUtility.WorldToScreenPoint(null,corners[0]).x);
        }
        [UnityTest] public IEnumerator NativeThumbTargetsKeepScreenSizeWhenCanvasScalerSettlesAfterInstallation()
        {
            var controls=runtime.GetComponent<OriginalMobileControls>();controls.Initialize();
            var move=controls.MoveSurface;var jump=controls.JumpSurface;Assert.That(move,Is.Not.Null);Assert.That(jump,Is.Not.Null);
            var canvas=move.GetComponentInParent<Canvas>();Assert.That(canvas.renderMode,Is.EqualTo(RenderMode.ScreenSpaceOverlay));
            var scaler=canvas.GetComponent<CanvasScaler>();if(scaler)scaler.enabled=false;
            canvas.scaleFactor=.5f;controls.RepairLayout();Canvas.ForceUpdateCanvases();
            float diameter=OriginalMobileControls.CssPixel(Mathf.Clamp(Mathf.Min(Screen.width,Screen.height)/OriginalMobileControls.PixelScale*.29f,128,176));
            Assert.That(ScreenWidth(move),Is.EqualTo(diameter).Within(1));Assert.That(ScreenWidth(jump),Is.EqualTo(OriginalMobileControls.CssPixel(76)).Within(1));
            canvas.scaleFactor=.8f;Canvas.ForceUpdateCanvases();
            Assert.That(Mathf.Abs(ScreenWidth(move)-diameter),Is.GreaterThan(20),"Fixture must reproduce a changed effective target before layout can repair");
            yield return null;yield return null;Canvas.ForceUpdateCanvases();
            Assert.That(ScreenWidth(move),Is.EqualTo(diameter).Within(1),"Late CanvasScaler updates must preserve the physical thumbstick diameter");
            Assert.That(ScreenWidth(jump),Is.EqualTo(OriginalMobileControls.CssPixel(76)).Within(1),"Jump must keep its76CSS-pixel target without a screen resize");
        }
        [Test] public void OriginalDisableTouchPresenceResetsContactAndAllowsTheNextFinger()
        {
            var source=runtime.OfType("SimpleTouchController").First(s=>!s.name.Contains("Right"));var pad=source.GetComponent<OriginalTouchPad>();Assert.That(pad,Is.Not.Null);
            // Desktop PlayMode hides native touch objects; enable the same source hierarchy that a coarse-pointer browser uses.
            for(var t=pad.transform;t;t=t.parent)t.gameObject.SetActive(true);Assert.That(pad.isActiveAndEnabled,Is.True);
            var player=pad.player;pad.player=null;pad.Enabled=true;
            try
            {
                pad.OnPointerDown(new PointerEventData(null){pointerId=71,position=Vector2.zero,button=PointerEventData.InputButton.Left});Assert.That(pad.Held,Is.True);
                runtime.CallObject(source.gameObject,"DisableTouchPresence");Assert.That(pad.Held,Is.False);Assert.That(pad.Value,Is.EqualTo(Vector2.zero));
                Assert.That(pad.Enabled,Is.True,"The original method clears touchPresent, rather than disabling future contact");
                pad.OnPointerDown(new PointerEventData(null){pointerId=72,position=Vector2.one,button=PointerEventData.InputButton.Left});Assert.That(pad.HeldPointer,Is.EqualTo(72));
            }
            finally {pad.Release();pad.player=player;}
        }
    }
}
