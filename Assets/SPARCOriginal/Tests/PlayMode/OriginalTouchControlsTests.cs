using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace Sparc.Original.Tests
{
    public sealed class OriginalTouchControlsTests
    {
        static PointerEventData Finger(int id,Vector2 position) => new PointerEventData(null) {pointerId=id,position=position,button=PointerEventData.InputButton.Left};
        [Test] public void AnalogDeadzoneKeepsDriftStillAndFullDiagonalMagnitudeBounded()
        {
            Assert.That(OriginalTouchPad.Analog(new Vector2(4,0),60),Is.EqualTo(Vector2.zero));
            var half=OriginalTouchPad.Analog(new Vector2(36,0),60);Assert.That(half.x,Is.InRange(.4f,.6f));
            Assert.That(OriginalTouchPad.Analog(new Vector2(100,100),60).magnitude,Is.EqualTo(1).Within(.001f));
        }
        [Test] public void LookUsesViewportFractionsAndRejectsInvalidOrExtremeEvents()
        {
            var small=OriginalTouchPad.LookDegrees(new Vector2(39,0),390);var large=OriginalTouchPad.LookDegrees(new Vector2(78,0),780);
            Assert.That(small.x,Is.EqualTo(large.x).Within(.001f));Assert.That(small.x,Is.EqualTo(15).Within(.001f));
            Assert.That(OriginalTouchPad.LookDegrees(new Vector2(float.NaN,0),390),Is.EqualTo(Vector2.zero));
            Assert.That(OriginalTouchPad.LookDegrees(new Vector2(10000,0),390).magnitude,Is.LessThan(53));
        }
        [UnityTest] public IEnumerator SecondFingerCannotStealOrReleaseTheMovementOwner()
        {
            var obj=new GameObject("Test native movement pad",typeof(RectTransform));var pad=obj.AddComponent<OriginalTouchPad>();
            try
            {
                pad.RadiusPixels=60;var first=Finger(11,new Vector2(100,100));pad.OnPointerDown(first);
                pad.OnPointerDown(Finger(22,new Vector2(900,900)));Assert.That(pad.HeldPointer,Is.EqualTo(11));
                // BeginDrag must preserve the original down position, including its initial travel.
                first.position=new Vector2(160,100);pad.OnBeginDrag(first);pad.OnDrag(first);yield return new WaitForSecondsRealtime(.12f);
                Assert.That(pad.Value.x,Is.GreaterThan(.1f));pad.OnPointerUp(Finger(22,Vector2.zero));Assert.That(pad.Held,Is.True);
                pad.OnPointerUp(first);Assert.That(pad.Held,Is.False);Assert.That(pad.Value,Is.EqualTo(Vector2.zero));
            }
            finally {Object.Destroy(obj);}
        }
        [Test] public void SeparateLookFingerAndCancelDoNotStealMovementOwnership()
        {
            var move=new GameObject("Move touch surface").AddComponent<OriginalTouchPad>();var look=new GameObject("Look touch surface").AddComponent<OriginalTouchPad>();look.Look=true;
            try
            {
                move.OnPointerDown(Finger(31,Vector2.zero));look.OnPointerDown(Finger(42,Vector2.one));
                look.OnPointerUp(Finger(31,Vector2.zero));Assert.That(move.HeldPointer,Is.EqualTo(31));Assert.That(look.HeldPointer,Is.EqualTo(42));
                look.OnCancel(new BaseEventData(null));Assert.That(look.Held,Is.False);Assert.That(move.Held,Is.True);
                move.Presence(false);Assert.That(move.Held,Is.False);Assert.That(move.Value,Is.EqualTo(Vector2.zero));
            }
            finally {Object.DestroyImmediate(move.gameObject);Object.DestroyImmediate(look.gameObject);}
        }
        [Test] public void DisabledPadRejectsNewContactAndReleasesHeldPointer()
        {
            var obj=new GameObject("Disabled touch surface");var pad=obj.AddComponent<OriginalTouchPad>();
            try
            {
                pad.OnPointerDown(Finger(7,Vector2.zero));Assert.That(pad.Held,Is.True);pad.enabled=false;Assert.That(pad.Held,Is.False);
                pad.OnPointerDown(Finger(8,Vector2.zero));Assert.That(pad.Held,Is.False);
            }
            finally {Object.DestroyImmediate(obj);}
        }
    }
}
