using System.Collections;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace Sparc.Original.Tests
{
    public sealed class OriginalMobileWorksheetTests
    {
        GameObject root;RectTransform bank;OriginalWordSlot slot;
        [SetUp] public void Setup()
        {
            root=new GameObject("Native worksheet test",typeof(RectTransform),typeof(Canvas));
            bank=new GameObject("Native word bank",typeof(RectTransform)).GetComponent<RectTransform>();bank.SetParent(root.transform,false);bank.pivot=new Vector2(0,1);
            var blank=new GameObject("Native blue blank",typeof(RectTransform),typeof(Image));blank.transform.SetParent(root.transform,false);((RectTransform)blank.transform).sizeDelta=new Vector2(130,44);slot=blank.AddComponent<OriginalWordSlot>();
        }
        [TearDown] public void Cleanup(){OriginalWordTile.ClearSelection();Object.DestroyImmediate(root);}
        OriginalWordTile Tile(string word,Vector3 home)
        {
            var obj=new GameObject("Original word "+word,typeof(RectTransform),typeof(Image));obj.transform.SetParent(bank,false);
            var label=new GameObject("Original text",typeof(RectTransform),typeof(TextMeshProUGUI));label.transform.SetParent(obj.transform,false);label.GetComponent<TMP_Text>().text=word;
            var tile=obj.AddComponent<OriginalWordTile>();tile.duration=.01f;tile.SetTouchSelection(true);tile.SetMobileHome(bank,home,new Vector2(100,44));return tile;
        }
        [UnityTest] public IEnumerator TapSelectionUsesTheSameOriginalSlotAndWordWithoutGrading()
        {
            var red=Tile("Red",Vector3.zero);var white=Tile("White",new Vector3(110,0,0));var click=new PointerEventData(null);
            red.OnPointerClick(click);Assert.That(OriginalWordTile.Selected,Is.SameAs(red));slot.OnPointerClick(click);yield return new WaitForSecondsRealtime(.04f);
            Assert.That(slot.Current,Is.SameAs(red));Assert.That(red.Slot,Is.SameAs(slot));Assert.That(red.Word,Is.EqualTo("Red"));Assert.That(OriginalWordTile.Selected,Is.Null);
            // Real pointer routing hits the placed word child, not its slot parent.
            white.OnPointerClick(click);red.OnPointerClick(click);yield return new WaitForSecondsRealtime(.04f);
            Assert.That(slot.Current,Is.SameAs(white));Assert.That(red.Slot,Is.Null);Assert.That(red.transform.parent,Is.SameAs(bank));Assert.That(white.Word,Is.EqualTo("White"));
        }
        [UnityTest] public IEnumerator OrientationReflowUpdatesReturnHomeWithoutLosingThePlacedAnswer()
        {
            var tile=Tile("Oxygen",Vector3.zero);slot.Accept(tile);yield return new WaitForSecondsRealtime(.04f);
            var updated=new Vector3(25,-80,0);tile.SetMobileHome(bank,updated,new Vector2(140,48));
            Assert.That(slot.Current,Is.SameAs(tile));Assert.That(tile.Slot,Is.SameAs(slot));tile.ReturnHome();yield return new WaitForSecondsRealtime(.04f);
            Assert.That(slot.Current,Is.Null);Assert.That(tile.transform.parent,Is.SameAs(bank));Assert.That(Vector3.Distance(tile.transform.localPosition,updated),Is.LessThan(.1f));
            Assert.That(((RectTransform)tile.transform).rect.height,Is.EqualTo(48).Within(.1f));
        }
        [Test] public void CancellingDragRestoresRaycastsAndSelectionCanClearOnPageDisable()
        {
            var tile=Tile("DNA",Vector3.zero);var pointer=new PointerEventData(null){pointerId=9,position=Vector2.zero};
            tile.OnPointerClick(pointer);Assert.That(OriginalWordTile.Selected,Is.SameAs(tile));tile.OnBeginDrag(pointer);
            Assert.That(tile.GetComponent<CanvasGroup>().blocksRaycasts,Is.False);tile.CancelDrag();Assert.That(tile.GetComponent<CanvasGroup>().blocksRaycasts,Is.True);Assert.That(tile.IsDragging,Is.False);
            tile.OnPointerClick(pointer);tile.gameObject.SetActive(false);Assert.That(OriginalWordTile.Selected,Is.Null);
        }
        [UnityTest] public IEnumerator CancelledTouchCannotLeaveAQueuedNativeWordDragFloating()
        {
            var touch=InputSystem.AddDevice<Touchscreen>();
            try
            {
                var home=new Vector3(42,-30,0);var tile=Tile("Important",home);
                var pointer=new ExtendedPointerEventData(null){pointerId=72,pointerType=UIPointerType.Touch,position=new Vector2(90,90)};
                tile.OnBeginDrag(pointer);Assert.That(tile.IsDragging,Is.True);yield return null;yield return null;
                Assert.That(tile.IsDragging,Is.False);Assert.That(tile.transform.parent,Is.SameAs(bank));Assert.That(Vector3.Distance(tile.transform.localPosition,home),Is.LessThan(.1f));
                Assert.That(tile.GetComponent<CanvasGroup>().blocksRaycasts,Is.True);Assert.That(tile.Slot,Is.Null);Assert.That(slot.Current,Is.Null);Assert.That(OriginalWordTile.Selected,Is.Null);
            }
            finally{InputSystem.RemoveDevice(touch);}
        }
        [UnityTest] public IEnumerator BankPivotAndSizeChangesKeepTheSameAnchoredReturnPosition()
        {
            bank.pivot=new Vector2(.5f,.5f);bank.sizeDelta=new Vector2(100,100);
            var tile=Tile("Red",new Vector3(-50,50,0));var rect=(RectTransform)tile.transform;
            bank.pivot=new Vector2(0,1);bank.sizeDelta=new Vector2(326,324);var expected=rect.position;
            Assert.That(rect.anchoredPosition,Is.EqualTo(Vector2.zero));tile.CancelDrag();
            Assert.That(Vector3.Distance(rect.position,expected),Is.LessThan(.01f));Assert.That(rect.anchoredPosition,Is.EqualTo(Vector2.zero));
            tile.transform.position+=Vector3.right*30;tile.ReturnHome();yield return new WaitForSecondsRealtime(.04f);
            Assert.That(Vector3.Distance(rect.position,expected),Is.LessThan(.01f));Assert.That(tile.Slot,Is.Null);Assert.That(slot.Current,Is.Null);
        }
    }
}
