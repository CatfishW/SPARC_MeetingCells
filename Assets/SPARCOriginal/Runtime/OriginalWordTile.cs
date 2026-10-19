using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using System.Linq;
using UnityEngine.UI;

namespace Sparc.Original
{
    /// <summary>Original word objects support drag/drop and optional touch selection; no answer grading.</summary>
    public sealed class OriginalWordTile : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerClickHandler
    {
        public OriginalWordSlot Slot { get; private set; }
        public static OriginalWordTile Selected { get; private set; }
        public string Word { get { var label=GetComponentInChildren<TMP_Text>();return label?label.text:""; } }
        public bool TouchSelection { get; private set; }
        public bool IsDragging => dragging;
        RectTransform rect,homeParent;Vector3 home;Vector2 homeSize,anchoredHome;
        CanvasGroup group;Outline highlight;bool initialized,dragging,touchDrag;int owner;float suppressClickUntil;
        public float duration=.5f;float placedAt=-10;
        void Initialize()
        {
            if(initialized)return;initialized=true;rect=(RectTransform)transform;home=rect.localPosition;homeParent=rect.parent as RectTransform;homeSize=rect.sizeDelta;
            group=OriginalComponents.Ensure<CanvasGroup>(gameObject);
        }
        public static void ClearSelection()
        {if(Selected&&Selected.highlight)Selected.highlight.enabled=false;Selected=null;}
        public void SetTouchSelection(bool enabled)
        {
            TouchSelection=enabled;if(!enabled&&Selected==this)ClearSelection();
        }
        public void SetMobileHome(RectTransform parent,Vector3 position,Vector2 size)
        {
            Initialize();StopAllCoroutines();homeParent=parent;home=position;homeSize=size;
            // Bank content is measured before its final size/pivot is applied.
            // Top-left anchors survive that change; a saved localPosition does
            // not, and would shift every cancelled tile by the old pivot offset.
            anchoredHome=(Vector2)position-new Vector2(parent.rect.xMin,parent.rect.yMax);
            if(dragging)CancelDrag();
            if(!Slot){rect.SetParent(parent,false);rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.localScale=Vector3.one;rect.sizeDelta=size;rect.localPosition=position;}
            else if(TouchSelection){rect.SetParent(Slot.transform,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.localScale=Vector3.one;rect.sizeDelta=((RectTransform)Slot.transform).rect.size;rect.anchoredPosition=Vector2.zero;rect.position=SlotPosition(Slot);}
            else rect.position=Slot.transform.position;
        }
        public void CancelDrag()
        {
            if(!initialized)return;dragging=false;group.blocksRaycasts=true;StopAllCoroutines();
            if(Slot){if(TouchSelection)rect.SetParent(Slot.transform,false);rect.position=SlotPosition(Slot);}
            else {rect.SetParent(homeParent,false);rect.sizeDelta=homeSize;if(TouchSelection){rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.localScale=Vector3.one;rect.anchoredPosition=anchoredHome;}else rect.localPosition=home;}
            if(Selected==this)ClearSelection();
        }
        public void OnBeginDrag(PointerEventData e)
        {
            Initialize();if(dragging)return;ClearSelection();StopAllCoroutines();dragging=true;touchDrag=e is ExtendedPointerEventData extended&&extended.pointerType==UIPointerType.Touch;owner=e.pointerId;group.blocksRaycasts=false;
            if(Slot){Slot.Release(this);Slot=null;}
            if(TouchSelection){var canvas=GetComponentInParent<Canvas>();if(canvas)rect.SetParent(canvas.transform,true);rect.SetAsLastSibling();}
            Follow(e);
        }
        void Follow(PointerEventData e){if(RectTransformUtility.ScreenPointToWorldPointInRectangle((RectTransform)rect.parent,e.position,e.pressEventCamera,out var point))rect.position=point;}
        public void OnDrag(PointerEventData e){if(dragging&&e.pointerId==owner)Follow(e);}
        public void OnEndDrag(PointerEventData e)
        {if(!dragging||e.pointerId!=owner)return;dragging=false;suppressClickUntil=Time.unscaledTime+.15f;group.blocksRaycasts=true;if(!Slot)ReturnHome();}
        public void OnPointerClick(PointerEventData e)
        {
            if(dragging||Time.unscaledTime<suppressClickUntil)return;
            if(Slot){if(TouchSelection&&Selected&&Selected!=this)Slot.Accept(Selected);else ReturnHome();return;}if(!TouchSelection)return;
            if(Selected==this){ClearSelection();return;}ClearSelection();Selected=this;
            if(!highlight){highlight=gameObject.AddComponent<Outline>();highlight.effectColor=new Color(.12f,.66f,.82f,1);highlight.effectDistance=new Vector2(2,-2);}
            highlight.enabled=true;
        }
        public void Place(OriginalWordSlot slot)
        {
            Initialize();StopAllCoroutines();if(Slot&&Slot!=slot)Slot.Release(this);Slot=slot;ClearSelection();placedAt=Time.unscaledTime;
            if(TouchSelection){rect.SetParent(slot.transform,true);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);rect.localScale=Vector3.one;rect.sizeDelta=((RectTransform)slot.transform).rect.size;}
            StartCoroutine(MoveTo(SlotPosition(slot),false));
        }
        Vector3 SlotPosition(OriginalWordSlot slot)
        {
            // Mobile sentence reflow gives native blanks a top-left pivot.
            // The placed word has a center pivot, so use the actual blank center.
            var target=slot.transform as RectTransform;
            return TouchSelection&&target?target.TransformPoint(target.rect.center):slot.transform.position;
        }
        public void ReturnHome()
        {
            Initialize();StopAllCoroutines();if(Slot)Slot.Release(this);Slot=null;if(Selected==this)ClearSelection();
            if(TouchSelection){rect.SetParent(homeParent,true);rect.anchorMin=rect.anchorMax=new Vector2(0,1);rect.pivot=new Vector2(0,1);rect.localScale=Vector3.one;rect.sizeDelta=homeSize;}
            StartCoroutine(MoveTo(TouchSelection?(Vector3)anchoredHome:home,true));
        }
        IEnumerator MoveTo(Vector3 destination,bool local)
        {var start=local?(TouchSelection?(Vector3)rect.anchoredPosition:rect.localPosition):rect.position;float elapsed=0;while(rect&&elapsed<duration){elapsed+=Time.unscaledDeltaTime;float t=Mathf.SmoothStep(0,1,Mathf.Clamp01(elapsed/Mathf.Max(.01f,duration)));var point=Vector3.Lerp(start,destination,t);if(local){if(TouchSelection)rect.anchoredPosition=point;else rect.localPosition=point;}else rect.position=point;yield return null;}if(rect){if(local){if(TouchSelection)rect.anchoredPosition=destination;else rect.localPosition=destination;}else rect.position=destination;}}
        void LateUpdate()
        {
            // A browser touchCancel can reach the lifecycle bridge before the UI
            // module delivers its queued BeginDrag. Release that stale drag after
            // UI dispatch as well, without affecting mouse or held touch drags.
            if(dragging&&touchDrag&&(Touchscreen.current==null||!Touchscreen.current.touches.Any(t=>t.press.isPressed)))CancelDrag();
            var image=GetComponent<Image>();if(image&&TouchSelection)image.color=Color.Lerp(Color.white,new Color(.67f,.91f,.97f),Mathf.Clamp01(1-(Time.unscaledTime-placedAt)/.42f));
        }
        void OnDisable(){if(Selected==this)ClearSelection();if(initialized){dragging=false;group.blocksRaycasts=true;}}
    }
}
