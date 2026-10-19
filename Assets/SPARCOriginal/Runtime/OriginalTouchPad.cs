using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Independent pointer ownership and responsive analog input on the original touch surfaces.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class OriginalTouchPad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler, IBeginDragHandler, IEndDragHandler, ICancelHandler
    {
        public OriginalPlayer player;
        public bool Look, Enabled = true;
        public float RadiusPixels = 58, DeadZone = .12f, Response = 24;
        public RectTransform Knob;
        const int NoPointer = int.MinValue;
        int pointer = NoPointer;
        Vector2 origin, target, filtered, knobRest;
        bool knobCached;
        public bool Held => pointer != NoPointer;
        public int HeldPointer => pointer;
        public Vector2 Value => filtered;
        bool Available => Enabled && isActiveAndEnabled && (!player || player.CanControl);

        public static Vector2 Analog(Vector2 delta, float radius, float deadZone = .12f)
        {
            float magnitude = delta.magnitude / Mathf.Max(1, radius);
            if (magnitude <= deadZone || float.IsNaN(magnitude) || float.IsInfinity(magnitude)) return Vector2.zero;
            float response = Mathf.Pow(Mathf.Clamp01((magnitude - deadZone) / Mathf.Max(.01f, 1 - deadZone)), 1.15f);
            return delta.normalized * response;
        }
        public static Vector2 LookDegrees(Vector2 pixels, float shortSide)
        {
            if (float.IsNaN(pixels.x) || float.IsNaN(pixels.y) || float.IsInfinity(pixels.x) || float.IsInfinity(pixels.y)) return Vector2.zero;
            // Equal fractions of a viewport turn equally across DPRs and orientations.
            return Vector2.ClampMagnitude(pixels, Mathf.Max(32, shortSide * .35f)) * (150 / Mathf.Max(320, shortSide));
        }
        public void Presence(bool active) { if (!active) Release(); }
        public void Release()
        {
            pointer = NoPointer; target = filtered = Vector2.zero;
            if (Knob && knobCached) Knob.anchoredPosition = knobRest;
            if (player) { if (Look) player.TouchLook = Vector2.zero; else player.TouchMove = Vector2.zero; }
        }
        public void OnPointerDown(PointerEventData e)
        {
            if (!Available || e.button != PointerEventData.InputButton.Left || Held) return;
            pointer = e.pointerId; origin = e.position; target = filtered = Vector2.zero;
            if (Knob && !knobCached) { knobRest = Knob.anchoredPosition; knobCached = true; }
        }
        public void OnBeginDrag(PointerEventData e) { if (!Held) OnPointerDown(e); }
        public void OnDrag(PointerEventData e)
        {
            if (!Available) { Release(); return; }
            if (!Held || e.pointerId != pointer) return;
            if (Look) { if (player) player.TouchLook += LookDegrees(e.delta, Mathf.Min(Screen.width, Screen.height)); }
            else target = Analog(e.position - origin, RadiusPixels, DeadZone);
        }
        public void OnPointerUp(PointerEventData e) { if (e.pointerId == pointer) Release(); }
        public void OnEndDrag(PointerEventData e) { if (e.pointerId == pointer) Release(); }
        public void OnCancel(BaseEventData e) { Release(); }
        void Update()
        {
            if (!Available) { if (Held || filtered.sqrMagnitude > 0) Release(); return; }
            if (Look || !Held) return;
            filtered = Vector2.Lerp(filtered, target, 1 - Mathf.Exp(-Response * Time.unscaledDeltaTime));
            if (player) player.TouchMove = filtered;
            if (Knob)
            {
                float scale = GetComponentInParent<Canvas>()?.scaleFactor ?? 1;
                Knob.anchoredPosition = knobRest + filtered * (RadiusPixels / Mathf.Max(.01f, scale));
            }
        }
        void OnApplicationFocus(bool focused) { if (!focused) Release(); }
        void OnApplicationPause(bool paused) { if (paused) Release(); }
        void OnDisable() { Release(); }
    }
}
