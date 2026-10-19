using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace Sparc.Original
{
    /// <summary>Notebook presentation of the original worksheet on every viewport; source questions, words, slots and submission stay original.</summary>
    [DefaultExecutionOrder(2600)]
    public sealed class OriginalMobileWorksheet : MonoBehaviour
    {
        public OriginalRuntime runtime;
        readonly Dictionary<GameObject,PageLayout> pages=new();
        readonly Dictionary<RectTransform,RectState> originals=new();
        readonly Dictionary<Behaviour,bool> layoutStates=new();
        readonly Dictionary<TMP_Text,TextState> originalSizes=new();
        sealed class TextState {public float size;public bool auto;public TextWrappingModes wrap;public Vector4 margin;public TextAlignmentOptions alignment;public TextState(TMP_Text t){size=t.fontSize;auto=t.enableAutoSizing;wrap=t.textWrappingMode;margin=t.margin;alignment=t.alignment;}public void Restore(TMP_Text t){t.fontSize=size;t.enableAutoSizing=auto;t.textWrappingMode=wrap;t.margin=margin;t.alignment=alignment;}}
        GameObject current;bool blocked;OriginalBehaviour manager;Canvas canvas;
        RectTransform next;Button previous;bool applied,previewOverride;float layoutDensity=1,layoutCanvasScale;Vector2Int layoutPixels;Rect layoutSafe;int placedSignature;
        public RectTransform PromptViewport=>current&&pages.TryGetValue(current,out var p)?p.promptScroll.viewport:null;
        public RectTransform WordBankViewport=>current&&pages.TryGetValue(current,out var p)?p.bankScroll.viewport:null;
        public object Snapshot
        {
            get { if(!current||!pages.TryGetValue(current,out var p))return null;return new {page=current.name,prompt=Bounds(p.promptScroll.viewport),bank=Bounds(p.bankScroll.viewport),selectedWord=OriginalWordTile.Selected?OriginalWordTile.Selected.Word:null}; }
        }
        object Bounds(RectTransform r)
        {
            var corners=new Vector3[4];r.GetWorldCorners(corners);var camera=canvas&&canvas.renderMode!=RenderMode.ScreenSpaceOverlay?canvas.worldCamera:null;
            var points=corners.Select(v=>RectTransformUtility.WorldToScreenPoint(camera,v)).ToArray();float x=points.Min(v=>v.x),y=points.Max(v=>v.y);
            float width=layoutPixels.x>0?layoutPixels.x:Screen.width,height=layoutPixels.y>0?layoutPixels.y:Screen.height;
            return new {x=x/width,y=1-y/height,w=(points.Max(v=>v.x)-x)/width,h=(y-points.Min(v=>v.y))/height};
        }
        sealed class PageLayout
        {
            public RectTransform root,prompt,bank,cue,promptCard,bankCard;public ScrollRect promptScroll,bankScroll;
            public OriginalWordTile[] tiles;public RectTransform[] paragraphs;public TMP_Text help,title,counter,status,bankTitle;public Image progress,illustration;
        }
        sealed class RectState
        {
            public Transform parent;public int sibling;public Vector2 min,max,pivot,size,position;public Vector3 scale;public Quaternion rotation;
            public RectState(RectTransform r){parent=r.parent;sibling=r.GetSiblingIndex();min=r.anchorMin;max=r.anchorMax;pivot=r.pivot;size=r.sizeDelta;position=r.anchoredPosition;scale=r.localScale;rotation=r.localRotation;}
            public void Restore(RectTransform r){if(!r)return;r.SetParent(parent,false);r.SetSiblingIndex(sibling);r.anchorMin=min;r.anchorMax=max;r.pivot=pivot;r.sizeDelta=size;r.anchoredPosition=position;r.localScale=scale;r.localRotation=rotation;}
        }
        void Remember(RectTransform rect){if(rect&&!originals.ContainsKey(rect))originals.Add(rect,new RectState(rect));}
        void DisableLayout(Behaviour layout){if(!layout)return;if(!layoutStates.ContainsKey(layout))layoutStates.Add(layout,layout.enabled);layout.enabled=false;}
        void SetType(TMP_Text text,float cssSize)
        {
            if(!text)return;if(!originalSizes.ContainsKey(text))originalSizes.Add(text,new TextState(text));
            text.enableAutoSizing=false;text.fontSize=cssSize*Unit;text.textWrappingMode=TextWrappingModes.Normal;text.color=OriginalGameArt.Ink;
        }
        float Unit=>layoutDensity/Mathf.Max(.01f,canvas?canvas.scaleFactor:1);
        static RectTransform Make(string name,Transform parent)
        {var obj=new GameObject(name,typeof(RectTransform));obj.layer=parent.gameObject.layer;obj.transform.SetParent(parent,false);return obj.GetComponent<RectTransform>();}
        static void Box(RectTransform r,float x,float y,float w,float h,float unit)
        {r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.localScale=Vector3.one;r.localRotation=Quaternion.identity;r.sizeDelta=new Vector2(w,h)*unit;r.anchoredPosition=new Vector2(x,-y)*unit;}
        ScrollRect Scroll(string name,RectTransform parent,out RectTransform content)
        {
            var view=Make(name,parent);var image=view.gameObject.AddComponent<Image>();image.color=Color.clear;image.raycastTarget=true;view.gameObject.AddComponent<RectMask2D>();
            var scroll=view.gameObject.AddComponent<ScrollRect>();scroll.horizontal=false;scroll.vertical=true;scroll.movementType=ScrollRect.MovementType.Clamped;scroll.decelerationRate=.12f;scroll.scrollSensitivity=25;
            content=Make("Content",view);scroll.viewport=view;scroll.content=content;
            var rail=Make("ScrollRail",view);rail.anchorMin=new Vector2(1,0);rail.anchorMax=Vector2.one;rail.pivot=new Vector2(1,.5f);rail.offsetMin=new Vector2(-44*Unit,0);rail.offsetMax=Vector2.zero;
            var railImage=rail.gameObject.AddComponent<Image>();railImage.color=Color.clear;
            var handle=Make("ScrollHandle",rail);handle.anchorMin=Vector2.zero;handle.anchorMax=Vector2.one;handle.offsetMin=new Vector2(3,3)*Unit;handle.offsetMax=new Vector2(-3,-3)*Unit;
            var transparent=handle.gameObject.AddComponent<Image>();transparent.color=Color.clear;
            var visual=Make("VisibleThumb",handle);visual.anchorMin=new Vector2(1,0);visual.anchorMax=Vector2.one;visual.offsetMin=new Vector2(-9,3)*Unit;visual.offsetMax=new Vector2(-3,-3)*Unit;
            var thumb=visual.gameObject.AddComponent<Image>();thumb.color=new Color(.40f,.58f,.60f,.8f);thumb.raycastTarget=false;
            var bar=rail.gameObject.AddComponent<Scrollbar>();bar.direction=Scrollbar.Direction.BottomToTop;bar.handleRect=handle;bar.targetGraphic=thumb;scroll.verticalScrollbar=bar;scroll.verticalScrollbarVisibility=ScrollRect.ScrollbarVisibility.AutoHide;
            return scroll;
        }
        PageLayout Prepare(GameObject page)
        {
            if(pages.TryGetValue(page,out var existing))return existing;
            var all=page.GetComponentsInChildren<RectTransform>(true);foreach(var rect in all)Remember(rect);
            var tiles=page.GetComponentsInChildren<OriginalWordTile>(true);
            var rows=page.GetComponentsInChildren<HorizontalLayoutGroup>(true).Where(r=>r.name.IndexOf("TextHold",StringComparison.OrdinalIgnoreCase)>=0).Select(r=>r.transform as RectTransform).ToArray();
            var textBlocks=page.GetComponentsInChildren<TMP_Text>(true).Where(t=>!t.GetComponentInParent<OriginalWordTile>()&&!t.GetComponentInParent<TMP_InputField>()&&!rows.Any(r=>t.transform.IsChildOf(r))).Select(t=>t.rectTransform);
            var blocks=rows.Concat(textBlocks).Distinct().OrderByDescending(r=>RectTransformUtility.WorldToScreenPoint(null,r.position).y).ThenBy(r=>r.GetSiblingIndex()).ToArray();
            foreach(var layout in page.GetComponentsInChildren<LayoutGroup>(true))DisableLayout(layout);
            foreach(var fitter in page.GetComponentsInChildren<ContentSizeFitter>(true))DisableLayout(fitter);
            var result=new PageLayout{tiles=tiles,paragraphs=blocks,cue=page.transform.Find("Image") as RectTransform};result.root=Make("MobileWorksheetLayout",page.transform);
            var paper=result.root.gameObject.AddComponent<Image>();paper.color=OriginalGameArt.Paper;paper.raycastTarget=true;
            result.promptCard=OriginalGameArt.Image("__SPARC_MINI_QuestionCard",result.root,OriginalGameArt.Frame("paper"),Color.white).rectTransform;
            result.bankCard=OriginalGameArt.Image("__SPARC_MINI_WordBankCard",result.root,OriginalGameArt.Frame("secondary"),Color.white).rectTransform;
            result.title=OriginalGameArt.Text("__SPARC_MINI_NotebookTitle",result.root,20,OriginalGameArt.Ink);result.title.fontStyle=FontStyles.Bold;result.title.text="CELL NOTEBOOK";
            result.counter=OriginalGameArt.Text("__SPARC_MINI_PageCount",result.root,15,OriginalUiTheme.Muted);result.counter.alignment=TextAlignmentOptions.MidlineRight;
            result.progress=OriginalGameArt.Image("__SPARC_MINI_PageProgress",result.root,null,OriginalGameArt.Blue);
            result.status=OriginalGameArt.Text("__SPARC_MINI_PlacedCount",result.root,13,OriginalGameArt.Blue);
            result.bankTitle=OriginalGameArt.Text("__SPARC_MINI_BankTitle",result.root,16,OriginalGameArt.Ink);result.bankTitle.fontStyle=FontStyles.Bold;result.bankTitle.text="Word bank";
            result.illustration=OriginalGameArt.Image("__SPARC_MINI_NotebookCells",result.root,OriginalGameArt.Get("worksheet-cells-pixel-sketch"),Color.white);result.illustration.preserveAspect=true;
            result.promptScroll=Scroll("PromptViewport",result.root,out result.prompt);result.bankScroll=Scroll("WordBankViewport",result.root,out result.bank);
            var help=Make("TouchWordHelp",result.root);result.help=help.gameObject.AddComponent<TextMeshProUGUI>();result.help.font=Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");result.help.color=OriginalUiTheme.Muted;result.help.raycastTarget=false;result.help.text="Tap a word, then a blue blank. You can also drag.";
            foreach(var block in blocks)block.SetParent(result.prompt,false);
            foreach(var tile in tiles){tile.SetTouchSelection(true);tile.transform.SetParent(result.bank,false);}
            pages.Add(page,result);return result;
        }
        public void ClearInteraction()
        {OriginalWordTile.ClearSelection();foreach(var p in pages.Values)foreach(var tile in p.tiles)if(tile)tile.CancelDrag();}
        public void Reflow(GameObject page)
        {
            var safe=GetComponent<OriginalMobileControls>()?.SafeScreenRect??Screen.safeArea;
            if(safe.width<=0||safe.height<=0)safe=Screen.safeArea;
            RefreshViewport(page,canvas,safe,new Vector2Int(Screen.width,Screen.height),OriginalMobileControls.PixelScale,false);
        }
        // The exact runtime layout path also supports a camera-backed viewport for native UI verification.
        public void ReflowForViewport(GameObject page,Canvas target,Rect safe,Vector2Int pixels,float density)
        {RefreshViewport(page,target,safe,pixels,density,true);}
        void RefreshViewport(GameObject page,Canvas target,Rect safe,Vector2Int pixels,float density,bool preview)
        {
            if(!page||!target)return;
            if(!applied||current!=page||canvas!=target||layoutPixels!=pixels||layoutSafe!=safe||
                Mathf.Abs(layoutDensity-Mathf.Max(.5f,density))>.001f||Mathf.Abs(layoutCanvasScale-Mathf.Max(.01f,target.scaleFactor))>.001f)
                ApplyViewport(page,target,safe,pixels,density,preview);
        }
        void ApplyViewport(GameObject page,Canvas target,Rect safe,Vector2Int pixels,float density,bool preview)
        {
            if(!page||!target)return;canvas=target;previewOverride=preview;layoutDensity=Mathf.Max(.5f,density);layoutPixels=pixels;layoutSafe=safe;layoutCanvasScale=Mathf.Max(.01f,target.scaleFactor);current=page;
            if(!manager&&runtime)manager=runtime.First("WordGameManager");
            if(manager)foreach(Transform child in manager.transform)Remember(child as RectTransform);
            ClearInteraction();var layout=Prepare(page);float unit=Unit;
            float w=safe.width/layoutDensity,h=safe.height/layoutDensity,left=safe.xMin/layoutDensity,top=(pixels.y-safe.yMax)/layoutDensity;
            const float margin=16,header=76,nav=72,gap=14;float bodyWidth=Mathf.Max(160,w-margin*2),bodyHeight=Mathf.Max(100,h-header-nav-16);
            var pageRect=page.transform as RectTransform;pageRect.anchorMin=Vector2.zero;pageRect.anchorMax=Vector2.one;pageRect.offsetMin=pageRect.offsetMax=Vector2.zero;pageRect.localScale=Vector3.one;pageRect.localRotation=Quaternion.identity;
            var frame=manager?manager.transform.Find("Frame"):null;if(frame){var graphic=frame.GetComponent<Graphic>();if(graphic)graphic.enabled=false;}
            page.transform.SetAsLastSibling();Box(layout.root,left,top,w,h,unit);
            int index=PageIndex(page);SetType(layout.title,19);SetType(layout.counter,14);SetType(layout.status,13);SetType(layout.bankTitle,16);SetType(layout.help,13);
            Box(layout.title.rectTransform,margin,18,bodyWidth-90,28,unit);Box(layout.counter.rectTransform,w-margin-88,18,88,28,unit);layout.counter.text=(index+1)+" / 13";
            Box(layout.progress.rectTransform,margin,58,bodyWidth*(index+1)/13f,4,unit);
            bool hasWords=layout.tiles.Length>0,columns=hasWords&&w>=740&&w/h>1.35f;
            layout.illustration.gameObject.SetActive(!hasWords);layout.bankCard.gameObject.SetActive(hasWords);layout.bankTitle.gameObject.SetActive(hasWords);layout.bankScroll.gameObject.SetActive(hasWords);layout.help.gameObject.SetActive(hasWords);layout.status.gameObject.SetActive(hasWords);
            float questionWidth=columns?(bodyWidth-gap)*.55f:bodyWidth;float bankWidth=columns?bodyWidth-questionWidth-gap:bodyWidth;
            float longest=100;
            foreach(var tile in layout.tiles){var label=tile.GetComponentInChildren<TMP_Text>();SetType(label,16);longest=Mathf.Max(longest,Mathf.Min(questionWidth-32,label.GetPreferredValues(tile.Word,Mathf.Infinity,Mathf.Infinity).x/unit+24));OriginalGameArt.Style(tile.GetComponent<Image>(),"paper");if(label)label.color=OriginalGameArt.Ink;}
            float promptWidth=questionWidth-32,y=LayoutPrompt(layout,promptWidth,longest,unit);
            float questionHeight=columns?bodyHeight:hasWords?Mathf.Clamp(y+32,120,Mathf.Max(120,bodyHeight*.49f)):Mathf.Min(bodyHeight,160);
            Box(layout.promptCard,margin,header,questionWidth,hasWords?questionHeight:bodyHeight,unit);
            Box(layout.promptScroll.viewport,margin+16,header+16,promptWidth,Mathf.Max(60,questionHeight-32),unit);
            if(y>questionHeight-32){promptWidth-=36;y=LayoutPrompt(layout,promptWidth,longest,unit);}
            Box(layout.prompt,0,0,promptWidth,Mathf.Max(questionHeight-32,y),unit);layout.promptScroll.verticalNormalizedPosition=1;
            float bankLeft=columns?margin+questionWidth+gap:margin,bankTop=columns?header:header+questionHeight+gap,bankHeight=columns?bodyHeight:bodyHeight-questionHeight-gap;
            if(hasWords)
            {
                Box(layout.bankCard,bankLeft,bankTop,bankWidth,bankHeight,unit);
                Box(layout.bankTitle.rectTransform,bankLeft+16,bankTop+12,bankWidth-32,25,unit);
                Box(layout.help.rectTransform,bankLeft+16,bankTop+42,bankWidth-32,34,unit);
                Box(layout.bankScroll.viewport,bankLeft+16,bankTop+84,bankWidth-32,Mathf.Max(44,bankHeight-100),unit);
                float bankContent=LayoutBank(layout,bankWidth-32,longest,unit);
                if(bankContent>bankHeight-100)bankContent=LayoutBank(layout,bankWidth-68,longest,unit);
                Box(layout.bank,0,0,bankContent>bankHeight-100?bankWidth-68:bankWidth-32,Mathf.Max(bankHeight-100,bankContent),unit);layout.bankScroll.verticalNormalizedPosition=1;
                Box(layout.status.rectTransform,margin+112,h-59,Mathf.Max(50,w-288),46,unit);
            }
            else
            {
                Box(layout.illustration.rectTransform,margin+32,header+Mathf.Min(150,bodyHeight*.30f),bodyWidth-64,Mathf.Max(80,bodyHeight-180),unit);
                float line=0;
                foreach(var block in layout.paragraphs)
                {
                    Box(block,0,line,promptWidth,42,unit);line+=48;
                    foreach(var text in block.GetComponentsInChildren<TMP_Text>(true)){text.alignment=TextAlignmentOptions.Center;SetType(text,index==12?28:24);if(text.rectTransform!=block)OriginalGameArt.Stretch(text.rectTransform);}
                }
            }
            foreach(var slot in page.GetComponentsInChildren<OriginalWordSlot>(true))
            {OriginalGameArt.Style(slot.GetComponent<Image>(),"slot");var hint=OriginalGameArt.Text("__SPARC_MINI_BlankHint",slot.transform,13*unit,OriginalGameArt.Blue);hint.text="choose word";hint.alignment=TextAlignmentOptions.Center;OriginalGameArt.Stretch(hint.rectTransform,5,3,5,3);hint.gameObject.SetActive(!slot.Current);}
            foreach(var field in page.GetComponentsInChildren<TMP_InputField>(true)){OriginalGameArt.Style(field.targetGraphic as Image,"red-input");foreach(var text in field.GetComponentsInChildren<TMP_Text>(true))text.color=OriginalGameArt.Ink;}
            if(layout.cue)layout.cue.gameObject.SetActive(false);
            var labelNext=manager.Component<TMP_Text>("nextButtonText");var button=labelNext?labelNext.GetComponentInParent<Button>():null;
            if(button)
            {
                next=button.transform as RectTransform;Remember(next);next.SetAsLastSibling();Box(next,left+w-margin-132,top+h-60,132,48,unit);OriginalGameArt.Style(button.targetGraphic as Image,"primary");
                foreach(var group in button.GetComponentsInChildren<LayoutGroup>(true))DisableLayout(group);
                foreach(var fitter in button.GetComponentsInChildren<ContentSizeFitter>(true))DisableLayout(fitter);
                foreach(var text in button.GetComponentsInChildren<TMP_Text>(true)){Remember(text.rectTransform);SetType(text,17);text.color=Color.white;text.margin=Vector4.zero;text.textWrappingMode=TextWrappingModes.NoWrap;text.alignment=TextAlignmentOptions.Center;OriginalGameArt.Stretch(text.rectTransform,10*unit,3*unit,10*unit,3*unit);}
                foreach(var art in button.GetComponentsInChildren<Graphic>(true).Where(g=>g.transform!=button.transform&&!(g is TMP_Text)))art.enabled=false;
            }
            if(!previous)
            {
                var image=OriginalGameArt.Image("__SPARC_MINI_PreviousPage",manager.transform,OriginalGameArt.Frame("secondary"),Color.white);image.raycastTarget=true;image.type=Image.Type.Sliced;previous=image.gameObject.AddComponent<Button>();previous.targetGraphic=image;previous.onClick.AddListener(()=>runtime.activities.Call(manager,"PrevPage",null));var text=OriginalGameArt.Text("__SPARC_MINI_BackLabel",previous.transform,17,OriginalGameArt.Ink);text.text="Back";text.alignment=TextAlignmentOptions.Center;OriginalGameArt.Stretch(text.rectTransform);
            }
            previous.gameObject.SetActive(index>0);previous.transform.SetAsLastSibling();Box(previous.transform as RectTransform,left+margin,top+h-60,96,48,unit);previous.GetComponentInChildren<TMP_Text>().fontSize=17*unit;
            Canvas.ForceUpdateCanvases();applied=true;placedSignature=Signature(layout);UpdateProgress(layout);
        }
        int PageIndex(GameObject page)
        {return manager?(manager.Data["wordGamePages"] as JArray??new JArray()).Select(t=>OriginalBehaviour.AsGameObject(manager.Resolve(t))).ToList().IndexOf(page):0;}
        int Signature(PageLayout page)
        {unchecked{int value=17;foreach(var tile in page.tiles)value=value*31+(tile.Slot?tile.Slot.GetInstanceID():0);return value;}}
        void UpdateProgress(PageLayout page)
        {
            if(!current)return;var slots=current.GetComponentsInChildren<OriginalWordSlot>(true);int count=slots.Count(s=>s.Current);
            page.status.text=count+" / "+slots.Length+" placed";
            foreach(var slot in slots){var hint=slot.transform.Find("__SPARC_MINI_BlankHint");if(hint)hint.gameObject.SetActive(!slot.Current);}
            if(page.tiles.Length==0)return;
            int signature=Signature(page);
            if(signature!=placedSignature&&!page.tiles.Any(t=>t.IsDragging))
            {placedSignature=signature;float width=page.bankScroll.viewport.rect.width/Unit;float height=LayoutBank(page,width,100,Unit);Box(page.bank,0,0,width,Mathf.Max(page.bankScroll.viewport.rect.height/Unit,height),Unit);}
        }
        void FitOriginalFrame(float left,float top,float width,float height,float unit)
        {
            var frame=manager?manager.transform.Find("Frame") as RectTransform:null;if(!frame)return;Remember(frame);
            // The source tablet sprite includes a wide left arm gutter. Fit its actual
            // writing area to the phone, keeping the original asset behind the controls.
            float w=(width-32)/.855f,h=(height-32)/.79f;
            Box(frame,left+16-w*.125f,top+16-h*.05f,w,h,unit);frame.SetSiblingIndex(1);
        }
        void RepositionTeachingCue(GameObject page,PageLayout layout,float unit)
        {
            var cue=layout.cue;
            if(!cue||cue.GetComponent<OriginalWordTile>()||!cue.GetComponent<Image>())return;
            var blank=page.GetComponentsInChildren<OriginalWordSlot>(true).FirstOrDefault();if(!blank)return;
            Remember(cue);var rotation=originals[cue].rotation;var corners=new Vector3[4];((RectTransform)blank.transform).GetWorldCorners(corners);
            var camera=canvas.renderMode==RenderMode.ScreenSpaceOverlay?null:canvas.worldCamera;
            var point=RectTransformUtility.WorldToScreenPoint(camera,(corners[0]+corners[3])*.5f);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(layout.root,point,camera,out var local);
            cue.SetParent(layout.root,false);cue.anchorMin=cue.anchorMax=new Vector2(0,1);cue.pivot=new Vector2(.5f,.5f);cue.localScale=Vector3.one;cue.localRotation=rotation;cue.sizeDelta=new Vector2(36,36)*unit;cue.localPosition=new Vector3(Mathf.Clamp(local.x,34*unit,Mathf.Max(34*unit,layout.root.rect.width-184*unit)),-layout.root.rect.height+34*unit,0);
        }
        float LayoutPrompt(PageLayout layout,float available,float longest,float unit)
        {float y=0;foreach(var block in layout.paragraphs){float height=LayoutParagraph(block,available,longest,unit);Box(block,0,y,available,height,unit);y+=height+12;}return y;}
        float LayoutParagraph(RectTransform block,float available,float longest,float unit)
        {
            var own=block.GetComponent<TMP_Text>();if(own){SetType(own,18);return Mathf.Max(28,own.GetPreferredValues(own.text,available*unit,Mathf.Infinity).y/unit+8);}
            float x=0,y=0,lineHeight=44;
            foreach(RectTransform cell in block)
            {
                var text=cell.GetComponent<TMP_Text>();var input=cell.GetComponent<TMP_InputField>();var slot=cell.GetComponent<OriginalWordSlot>();
                float width,height=44;
                if(text){SetType(text,18);width=Mathf.Min(available,Mathf.Max(8,text.GetPreferredValues(text.text,Mathf.Infinity,Mathf.Infinity).x/unit));height=Mathf.Max(28,text.GetPreferredValues(text.text,width*unit,Mathf.Infinity).y/unit+4);}
                else if(slot||input){width=Mathf.Min(available,Mathf.Max(input?140:100,longest));if(input)foreach(var label in input.GetComponentsInChildren<TMP_Text>(true)){SetType(label,16);label.textWrappingMode=TextWrappingModes.NoWrap;}}
                else continue;
                if(x>0&&x+width>available){x=0;y+=lineHeight+8;lineHeight=44;}
                Box(cell,x,y,width,height,unit);x+=width+6;lineHeight=Mathf.Max(lineHeight,height);
            }
            return y+lineHeight;
        }
        float LayoutBank(PageLayout p,float available,float longest,float unit)
        {
            float x=0,y=0,row=44;
            foreach(var tile in p.tiles)
            {
                var label=tile.GetComponentInChildren<TMP_Text>();SetType(label,16);float width=Mathf.Clamp(label.GetPreferredValues(tile.Word,Mathf.Infinity,Mathf.Infinity).x/unit+24,84,available);
                float height=Mathf.Max(44,label.GetPreferredValues(tile.Word,(width-16)*unit,Mathf.Infinity).y/unit+12);
                if(!tile.Slot&&x>0&&x+width>available){x=0;y+=row+8;row=44;}
                var r=tile.transform as RectTransform;var position=new Vector3(x*unit,-y*unit,0);
                // Anchored top-left local coordinates include the content's pivot offset.
                position+=new Vector3(-p.bank.rect.width*p.bank.pivot.x,p.bank.rect.height*(1-p.bank.pivot.y),0);
                tile.SetMobileHome(p.bank,position,new Vector2(width,height)*unit);
                label.rectTransform.anchorMin=Vector2.zero;label.rectTransform.anchorMax=Vector2.one;label.rectTransform.offsetMin=new Vector2(8,4)*unit;label.rectTransform.offsetMax=new Vector2(-8,-4)*unit;
                if(!tile.Slot){x+=width+8;row=Mathf.Max(row,height);}
            }
            return p.tiles.Length>0?y+row:0;
        }
        void Restore()
        {
            ClearInteraction();foreach(var pair in originals)if(pair.Key)pair.Value.Restore(pair.Key);
            foreach(var pair in originalSizes)if(pair.Key)pair.Value.Restore(pair.Key);
            foreach(var pair in layoutStates)if(pair.Key)pair.Key.enabled=pair.Value;
            foreach(var page in pages.Values){foreach(var tile in page.tiles)if(tile){tile.SetTouchSelection(false);var r=tile.transform as RectTransform;tile.SetMobileHome(r.parent as RectTransform,r.localPosition,r.sizeDelta);}if(page.root)Destroy(page.root.gameObject);}
            pages.Clear();originals.Clear();layoutStates.Clear();originalSizes.Clear();current=null;applied=false;
        }
        void LateUpdate()
        {
            if(previewOverride)return;
            if(!runtime)return;
            if(!manager)manager=runtime.First("WordGameManager");if(!manager||!manager.isActiveAndEnabled){if(current){ClearInteraction();current=null;}return;}
            if(!canvas)canvas=manager.GetComponentInParent<Canvas>();if(!canvas)return;
            var page=(manager.Data["wordGamePages"] as JArray??new JArray()).Select(t=>OriginalBehaviour.AsGameObject(manager.Resolve(t))).FirstOrDefault(g=>g&&g.activeInHierarchy);
            if(page){Reflow(page);if(pages.TryGetValue(page,out var activePage))UpdateProgress(activePage);}
            bool gate=runtime.ShuttingDown||runtime.CinematicActive||runtime.presentation.IsSpeaking||(runtime.GetComponent<OriginalMenuUi>()?.IsOpen??false);
            if(gate&&!blocked)ClearInteraction();blocked=gate;
            if(current&&pages.TryGetValue(current,out var active))foreach(var tile in active.tiles)if(tile)tile.SetTouchSelection(!gate);
            var selected=EventSystem.current?EventSystem.current.currentSelectedGameObject:null;
            if(selected&&selected.GetComponent<TMP_InputField>())OriginalWordTile.ClearSelection();
        }
        void OnDisable(){ClearInteraction();}
    }
}
