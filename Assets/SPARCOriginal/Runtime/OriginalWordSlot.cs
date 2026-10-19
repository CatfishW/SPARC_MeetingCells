using UnityEngine;
using UnityEngine.EventSystems;
namespace Sparc.Original
{
    public sealed class OriginalWordSlot : MonoBehaviour, IDropHandler, IPointerClickHandler
    {
        public OriginalWordTile Current { get; private set; }
        public void Accept(OriginalWordTile tile)
        {if(!tile)return;if(Current&&Current!=tile)Current.ReturnHome();Current=tile;tile.Place(this);}
        public void OnDrop(PointerEventData eventData)
        {Accept(eventData.pointerDrag?eventData.pointerDrag.GetComponent<OriginalWordTile>():null);}
        public void OnPointerClick(PointerEventData e)
        {var tile=OriginalWordTile.Selected;if(tile&&tile.TouchSelection&&tile.gameObject.activeInHierarchy)Accept(tile);}
        public void Release(OriginalWordTile tile){if(Current==tile)Current=null;}
    }
}
