using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem.OnScreen;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Thumb-safe original controls and lifecycle cancellation; no story, arena or physics changes.</summary>
    [DefaultExecutionOrder(2500)]
    public sealed class OriginalMobileControls : MonoBehaviour
    {
        public OriginalRuntime runtime;
        public Rect SafeScreenRect { get; private set; }
        public RectTransform MoveSurface => move;
        public RectTransform LookSurface => look;
        public RectTransform JumpSurface => jump;
        OriginalTouchPad[] pads = System.Array.Empty<OriginalTouchPad>();
        RectTransform move, look, jump;
        Canvas controlsCanvas;
        float layoutCanvasScale, layoutPixelScale;
        bool installed, available, prompt, unfocused, paused;
        readonly HashSet<OnScreenStick> suspendedSticks = new();
        int width, height, epoch;
        Rect safe;
#if UNITY_WEBGL && !UNITY_EDITOR
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern int SPARC_BrowserInputEpoch();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern float SPARC_BrowserPixelScale();
        [System.Runtime.InteropServices.DllImport("__Internal")] static extern float SPARC_BrowserSafeInset(int side);
#endif
        public static float PixelScale
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return Mathf.Max(.5f, SPARC_BrowserPixelScale());
#else
                return 1;
#endif
            }
        }
        public static float CssPixel(float pixels) => pixels * PixelScale;
        void Start() { Initialize(); }
        public void Initialize()
        {
            if (installed || !runtime || !runtime.player) return;
            // Source pads live on the original player canvas rather than under Runtime.
            pads = FindObjectsByType<OriginalTouchPad>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(p => p.gameObject.scene == gameObject.scene).ToArray();
            foreach (var pad in pads)
            {
                pad.player = runtime.player;
                var scroll = pad.GetComponent<ScrollRect>(); if (scroll) scroll.enabled = false;
                var legacyEvents = pad.GetComponent<EventTrigger>(); if (legacyEvents) legacyEvents.enabled = false;
                if (pad.Look) look = pad.transform as RectTransform;
                else
                {
                    move = pad.transform as RectTransform;
                    foreach (var ornament in pad.GetComponentsInChildren<Graphic>(true).Where(g => g.transform != move)) ornament.enabled = false;
                    var knob = new GameObject("MobileHandle",typeof(RectTransform),typeof(CanvasRenderer),typeof(Image));
                    knob.transform.SetParent(move,false);var image = knob.GetComponent<Image>();image.sprite=OriginalUiIcons.Get("knob");image.color=OriginalUiTheme.Teal;image.raycastTarget=false;
                    pad.Knob=knob.GetComponent<RectTransform>();
                }
            }
            var source = runtime.First("ControlManager");
            jump = source ? source.Component<RectTransform>("jumpButtonTransform") : null;
            if (!GetComponent<OriginalMobileWorksheet>()) { var words=gameObject.AddComponent<OriginalMobileWorksheet>();words.runtime=runtime; }
            installed = true; RepairLayout();
        }
        public void ReleaseAll()
        {
            foreach (var pad in pads) if (pad) pad.Release();
            if (runtime && runtime.player) runtime.player.ReleaseTouchInput();
            GetComponent<OriginalMobileWorksheet>()?.ClearInteraction();
            // The source minigame joystick is an actual OnScreenStick/virtual gamepad.
            // Disable/enable resets that control through Input System's own release path.
            foreach (var stick in FindObjectsByType<OnScreenStick>(FindObjectsSortMode.None).Where(s => s.gameObject.scene == gameObject.scene))
            {
                if (!stick.enabled) continue;
                if (!stick.useIsolatedInputActions) stick.OnPointerUp(null);
                stick.enabled = false;
                if (AnyTouch() || unfocused || paused || prompt) suspendedSticks.Add(stick); else stick.enabled = true;
            }
        }
        static bool AnyTouch() => Touchscreen.current != null && Touchscreen.current.touches.Any(t => t.press.isPressed);
        static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            if (!rect) return; rect.localScale = Vector3.one; rect.localRotation = Quaternion.identity;
            rect.anchorMin = rect.anchorMax = anchor; rect.pivot = pivot; rect.anchoredPosition = position; rect.sizeDelta = size;
        }
        public void RepairLayout()
        {
            width = Screen.width; height = Screen.height; safe = Screen.safeArea;
            if (!move || !look) return;
            controlsCanvas = controlsCanvas ? controlsCanvas : move.GetComponentInParent<Canvas>();
            float scale = Mathf.Max(.01f, controlsCanvas ? controlsCanvas.scaleFactor : 1);
            layoutCanvasScale = scale; layoutPixelScale = PixelScale;
            float left = safe.xMin, right = width - safe.xMax, bottom = safe.yMin, top = height - safe.yMax;
#if UNITY_WEBGL && !UNITY_EDITOR
            left = Mathf.Max(left, SPARC_BrowserSafeInset(0)); right = Mathf.Max(right, SPARC_BrowserSafeInset(1));
            top = Mathf.Max(top, SPARC_BrowserSafeInset(2)); bottom = Mathf.Max(bottom, SPARC_BrowserSafeInset(3));
#endif
            SafeScreenRect = new Rect(left,bottom,Mathf.Max(1,width-left-right),Mathf.Max(1,height-top-bottom));
            float diameter = CssPixel(Mathf.Clamp(Mathf.Min(width, height)/PixelScale * .29f, 128, 176)), margin = CssPixel(20);
            Place(move, Vector2.zero, new Vector2(.5f,.5f), new Vector2(left + margin + diameter/2, bottom + margin + diameter/2)/scale, Vector2.one * diameter/scale);
            foreach (var pad in pads) if (pad && !pad.Look)
            { pad.RadiusPixels = diameter * .36f; Place(pad.Knob,new Vector2(.5f,.5f),new Vector2(.5f,.5f),Vector2.zero,Vector2.one*CssPixel(58)/scale); }
            look.localScale = Vector3.one; look.localRotation = Quaternion.identity;
            look.anchorMin = new Vector2(.47f,0); look.anchorMax = Vector2.one;
            look.offsetMin = new Vector2(0,(bottom + CssPixel(18))/scale); look.offsetMax = new Vector2(-right/scale,-(top + CssPixel(94))/scale);
            // Transparent look receives unused space; native buttons remain above it.
            look.SetAsFirstSibling();
            if (jump) { Place(jump,new Vector2(1,0),new Vector2(1,0),new Vector2(-right-margin,bottom+CssPixel(30))/scale,Vector2.one*CssPixel(76)/scale); jump.SetAsLastSibling(); }
        }
        void LateUpdate()
        {
            if (!installed) Initialize(); if (!installed) return;
            // CanvasScaler can settle after installation without any viewport resize.
            // Recompute screen-sized targets and release old coordinates in that case too.
            float canvasScale = Mathf.Max(.01f, controlsCanvas ? controlsCanvas.scaleFactor : 1);
            if (width != Screen.width || height != Screen.height || safe != Screen.safeArea ||
                Mathf.Abs(layoutCanvasScale-canvasScale) > .001f || Mathf.Abs(layoutPixelScale-PixelScale) > .001f)
            { ReleaseAll(); RepairLayout(); }
            bool can = runtime && runtime.player && runtime.player.CanControl;
            bool nextPrompt = runtime && runtime.activities && runtime.OfType("ExitConfirmation").Any(c => c && c.isActiveAndEnabled && runtime.activities.IsExitPromptVisible(c));
            if (nextPrompt && !prompt) { prompt = true; ReleaseAll(); } else prompt = nextPrompt;
            if (available && !can) ReleaseAll(); available = can;
            if (!AnyTouch() && !prompt && !unfocused && !paused)
            { foreach (var stick in suspendedSticks) if (stick) stick.enabled = true; suspendedSticks.Clear(); }
#if UNITY_WEBGL && !UNITY_EDITOR
            int current = SPARC_BrowserInputEpoch(); if (epoch != current) { epoch = current; ReleaseAll(); RepairLayout(); }
#endif
        }
        void OnApplicationFocus(bool focused) { unfocused = !focused; if (!focused) ReleaseAll(); }
        void OnApplicationPause(bool state) { paused = state; if (state) ReleaseAll(); }
        void OnDisable() { ReleaseAll(); }
    }
}
