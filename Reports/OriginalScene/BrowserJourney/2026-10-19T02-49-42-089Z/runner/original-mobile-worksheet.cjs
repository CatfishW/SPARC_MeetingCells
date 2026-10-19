'use strict';
// Optional hybrid-route worksheet proof. All placement uses genuine browser touch events.
const assert=require('node:assert/strict');
const sessions=new WeakMap();
const pause=(page,ms)=>page.waitForTimeout(ms);
function point(rect,page){const v=page.viewportSize();return {x:(rect.x+rect.w/2)*v.width,y:(rect.y+rect.h/2)*v.height};}
function visible(rect,clip){return rect&&clip&&rect.w>0&&rect.h>0&&rect.x>=clip.x-.001&&rect.y>=clip.y-.001&&rect.x+rect.w<=clip.x+clip.w+.001&&rect.y+rect.h<=clip.y+clip.h+.001;}
function enough(rect,page,label){const v=page.viewportSize();assert(rect.h*v.height>=43.5,label+' is less than44 CSSpx high');assert(rect.w*v.width>=43.5,label+' is less than44 CSSpx wide');}
async function poll(state,predicate,page,timeout=5000){const end=Date.now()+timeout;while(Date.now()<end){const s=await state();if(predicate(s))return s;await pause(page,150);}return null;}
async function tap(page,p){await page.touchscreen.tap(p.x,p.y);await pause(page,350);}
async function drag(page,from,to){let cdp=sessions.get(page);if(!cdp){cdp=await page.context().newCDPSession(page);sessions.set(page,cdp);}const contact=p=>({id:71,x:p.x,y:p.y,radiusX:8,radiusY:8,force:1});await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[contact(from)]});await pause(page,120);for(let i=1;i<=18;i++){await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[contact({x:from.x+(to.x-from.x)*i/18,y:from.y+(to.y-from.y)*i/18})]});await pause(page,40);}await cdp.send('Input.dispatchTouchEvent',{type:'touchEnd',touchPoints:[contact(to)]});await pause(page,650);}
function sameBlank(a,b){return a.name===b.name&&Math.abs(a.rect.x-b.rect.x)<.005&&Math.abs(a.rect.y-b.rect.y)<.005;}
function placedGeometry(tile,slot,page){
 if(!tile||!slot)return false;const v=page.viewportSize(),a=tile.rect,b=slot.rect;
 return Math.abs((a.x+a.w/2-b.x-b.w/2)*v.width)<=1.5&&Math.abs((a.y+a.h/2-b.y-b.h/2)*v.height)<=1.5&&Math.abs((a.w-b.w)*v.width)<=1.5&&Math.abs((a.h-b.h)*v.height)<=1.5;
}
async function cancelWordGesture({page,state,report,tile}){
 const beforeTiles=(await state()).wordTiles.map(t=>({name:t.name,word:t.word,rect:{...t.rect}}));
 let cdp=sessions.get(page);if(!cdp){cdp=await page.context().newCDPSession(page);sessions.set(page,cdp);}const home=tile.rect,from=point(home,page),contact=p=>({id:72,x:p.x,y:p.y,radiusX:8,radiusY:8,force:1});
 await tap(page,from);assert(await poll(state,s=>s.mobileWorksheet.selectedWord===tile.word,page),'Native word did not select before cancellation');
 await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[contact(from)]});await pause(page,100);await cdp.send('Input.dispatchTouchEvent',{type:'touchCancel',touchPoints:[]});
 assert(await poll(state,s=>s.mobileWorksheet.selectedWord===null,page),'Word selection survived real browser touchCancel');
 await cdp.send('Input.dispatchTouchEvent',{type:'touchStart',touchPoints:[contact(from)]});await pause(page,100);await cdp.send('Input.dispatchTouchEvent',{type:'touchMove',touchPoints:[contact({x:from.x+32,y:from.y-18})]});await pause(page,180);await cdp.send('Input.dispatchTouchEvent',{type:'touchCancel',touchPoints:[]});
 const reset=await poll(state,s=>{const t=(s.wordTiles||[]).find(t=>t.name===tile.name&&t.word===tile.word);return t&&Math.abs(t.rect.x-home.x)<.004&&Math.abs(t.rect.y-home.y)<.004&&s.mobileWorksheet.selectedWord===null&&(s.wordSlots||[]).every(slot=>!slot.value);},page);
 if(!reset){const after=await state();report.mobileWorksheet.cancelFailure={home,beforeTiles,after:{frame:after.frame,mobileWorksheet:after.mobileWorksheet,wordTiles:after.wordTiles,wordSlots:after.wordSlots}};}
 assert(reset,'Cancelled native word drag did not return to its bank without assigning a blank');
 const allWords=beforeTiles.map(before=>{const after=reset.wordTiles.find(t=>t.name===before.name&&t.word===before.word);assert(after,'Cancellation lost a source word');assert(['x','y','w','h'].every(k=>Math.abs(after.rect[k]-before.rect[k])<.004),'Cancellation shifted the rendered bank home for '+before.word);return {name:before.name,word:before.word,home:before.rect,returned:after.rect};});
 await tap(page,from);assert(await poll(state,s=>s.mobileWorksheet.selectedWord===tile.word,page),'Cancelled word did not regain actual touch hit testing');
 report.mobileWorksheet.cancelVerified={word:tile.word,home,returned:reset.wordTiles.find(t=>t.name===tile.name&&t.word===tile.word).rect,assignedSlots:reset.wordSlots.filter(slot=>slot.value).length,allWords};
 report.mobileWorksheet.checks.push('real-word-touchcancel-clears-selection-restores-bank-and-hit-testing');
}
async function completePage({page,state,capture,report,pageNumber}){
 report.mobileWorksheet=report.mobileWorksheet||{scope:'Hybrid original route: real keyboard/mouse navigation and unchanged native120/250-second games, followed by real mobile worksheet touch; not a pure-touch full journey',physicalDeviceAcceptance:false,pages:[],checks:[]};
 // Use both phone orientations while keeping the source page sequence unchanged.
 const viewport=pageNumber===4||pageNumber===8?{width:844,height:390}:{width:390,height:844};
 if(page.viewportSize().width!==viewport.width||page.viewportSize().height!==viewport.height){await page.setViewportSize(viewport);await pause(page,1000);}
 let s=await state();assert.equal(s.player.canControl,false,'Original worksheet must gate player locomotion');await capture('mobile-workbook-'+String(pageNumber).padStart(2,'0'));
 assert(s.mobileWorksheet,'Read-only mobile worksheet viewport observation is missing');
 const row={page:pageNumber,viewport,layout:s.mobileWorksheet,interactions:[],tiles:(s.wordTiles||[]).map(t=>({name:t.name,word:t.word,rect:t.rect})),slots:s.wordSlots||[],fields:s.fields||[]};
 if(!report.mobileWorksheet.cancelVerified&&(s.wordTiles||[]).length){const tile=s.wordTiles.find(t=>visible(t.rect,s.mobileWorksheet.bank));assert(tile,'No visible native word for cancel check');await cancelWordGesture({page,state,report,tile});}
 const used=new Set();
 for(let slotIndex=0;slotIndex<(s.wordSlots||[]).length;slotIndex++){
  let now=await state(),blank=now.wordSlots[slotIndex];if(blank.value)continue;
  enough(blank.rect,page,'Native blank '+blank.name);
  for(let attempt=0;attempt<5&&!visible(blank.rect,now.mobileWorksheet.prompt);attempt++){
   const clip=now.mobileWorksheet.prompt,v=page.viewportSize(),below=blank.rect.y+blank.rect.h>clip.y+clip.h;
   await tap(page,{x:(clip.x+clip.w)*v.width-22,y:(below?clip.y+clip.h-.03:clip.y+.03)*v.height});
   now=await state();blank=now.wordSlots[slotIndex];
  }
  assert(visible(blank.rect,now.mobileWorksheet.prompt),'Native blank could not be scrolled fully into its prompt viewport');
  let tile=(now.wordTiles||[]).find(t=>!used.has(t.name+'\n'+t.word)&&visible(t.rect,now.mobileWorksheet.bank));
  if(!tile){const clip=now.mobileWorksheet.bank,v=page.viewportSize();await tap(page,{x:(clip.x+clip.w)*v.width-22,y:(clip.y+clip.h-.03)*v.height});now=await state();tile=(now.wordTiles||[]).find(t=>!used.has(t.name+'\n'+t.word)&&visible(t.rect,now.mobileWorksheet.bank));}
  assert(tile,'No visible native word remained in the mobile bank');enough(tile.rect,page,'Native word '+tile.word);used.add(tile.name+'\n'+tile.word);
  const mode=!report.mobileWorksheet.dragVerified?'drag':'tap';
  if(mode==='drag')await drag(page,point(tile.rect,page),point(blank.rect,page));
  else {await tap(page,point(tile.rect,page));const selected=await poll(state,a=>a.mobileWorksheet.selectedWord===tile.word,page);assert(selected,'Actual touch did not select original word');await tap(page,point(blank.rect,page));}
  now=await poll(state,after=>{const slot=after.wordSlots[slotIndex],placed=(after.wordTiles||[]).find(t=>t.name===tile.name&&t.word===tile.word);return slot?.value===tile.word&&placedGeometry(placed,slot,page);},page,7000);
  assert(now,'Real '+mode+' did not place and visually align original word '+JSON.stringify(tile.word)+' in '+blank.name);
  if(mode==='drag')report.mobileWorksheet.dragVerified=true;
  const placedTile=now.wordTiles.find(t=>t.name===tile.name&&t.word===tile.word),placedSlot=now.wordSlots[slotIndex];
  row.interactions.push({mode,word:tile.word,tileRect:tile.rect,placedTileRect:placedTile.rect,slot:blank.name,slotRect:placedSlot.rect,alignmentToleranceCssPixels:1.5});
 }
 for(const field of s.fields||[]){
  enough(field.rect,page,'Native text field');await tap(page,point(field.rect,page));
  // The original Tutorial field accepts at most16 alphanumeric characters.
  // Use real keys within that contract, then commit through Unity's actual HTML OK button.
  const text='RedDeliversO2';
  await page.waitForFunction(()=>document.activeElement instanceof HTMLInputElement||document.activeElement instanceof HTMLTextAreaElement,null,{timeout:5000});
  const htmlInput=await page.evaluate(()=>{const e=document.activeElement;return {tag:e.tagName,type:e.type,maxLength:e.maxLength,readOnly:e.readOnly,disabled:e.disabled};});
  assert(!htmlInput.readOnly&&!htmlInput.disabled,'Native mobile HTML input is not editable');assert(htmlInput.maxLength<0||htmlInput.maxLength>=text.length,'Chosen real input exceeds the original field character limit');
  const entry={page:pageNumber,name:field.name,beforeNativeText:field.text,expectedText:text,htmlInput,method:'real-native-field-tap-and-keyboard-typing'};
  (report.mobileWorksheet.fieldInputs||(report.mobileWorksheet.fieldInputs=[])).push(entry);
  await page.keyboard.type(text,{delay:35});
  const typed=await poll(state,a=>(a.fields||[]).some(f=>f.name===field.name&&f.text===text),page);assert(typed,'Real mobile field typing did not reach the native original input');
  entry.typedNativeText=typed.fields.find(f=>f.name===field.name).text;await capture('mobile-workbook-'+String(pageNumber).padStart(2,'0')+'-field-entry');
  const done=page.getByRole('button',{name:'OK',exact:true});await done.waitFor({state:'visible',timeout:5000});entry.commit={method:'real-touch-on-Unity-HTML-OK',rect:await done.boundingBox(),minimumTargetCssPixels:44};
  const okRect=entry.commit.rect,okViewport=page.viewportSize();
  assert(okRect&&okRect.width>=44&&okRect.height>=44,'Native Unity HTML OK button must be at least44×44 CSSpx');
  assert(okRect.x>=0&&okRect.y>=0&&okRect.x+okRect.width<=okViewport.width&&okRect.y+okRect.height<=okViewport.height,'Native Unity HTML OK button must be fully inside the viewport');
  await done.tap();await done.waitFor({state:'hidden',timeout:5000});
  const committed=await poll(state,a=>(a.fields||[]).some(f=>f.name===field.name&&f.text===text),page);assert(committed,'Native text changed after the actual mobile keyboard commit');
  entry.committedNativeText=committed.fields.find(f=>f.name===field.name).text;entry.keyboardDismissed=true;
  row.fieldEntries=row.fieldEntries||[];row.fieldEntries.push(entry);await capture('mobile-workbook-'+String(pageNumber).padStart(2,'0')+'-field-committed');
 }
 const checked=await state();assert.equal(checked.player.canControl,false,'Worksheet input re-enabled player locomotion');row.filledSlots=(checked.wordSlots||[]).filter(slot=>slot.value).length;row.totalSlots=(checked.wordSlots||[]).length;assert.equal(row.filledSlots,row.totalSlots,'Mobile page still has unfilled native blanks');
 const next=(checked.buttons||[]).find(b=>b.name==='NextButton');assert(next,'Native Next/Submit control missing');enough(next.rect,page,'Native Next/Submit');assert(next.rect.x>=0&&next.rect.y>=0&&next.rect.x+next.rect.w<=1.001&&next.rect.y+next.rect.h<=1.001,'Native Next outside viewport');
 row.next={text:next.text,rect:next.rect};report.mobileWorksheet.pages.push(row);await capture('mobile-workbook-'+String(pageNumber).padStart(2,'0')+'-filled');
 await tap(page,point(next.rect,page));await pause(page,550);
 if(/submit/i.test(next.text)){report.mobileWorksheet.totalFilledSlots=report.mobileWorksheet.pages.reduce((sum,p)=>sum+p.filledSlots,0);assert.equal(report.mobileWorksheet.pages.length,13,'All original worksheet pages must be visited');assert.equal(report.mobileWorksheet.totalFilledSlots,14,'All14 native slot references must be populated');assert(report.mobileWorksheet.dragVerified,'At least one native mobile drag must pass alongside tap placement');report.mobileWorksheet.checks.push('13-original-pages-and14-native-slots-through-real-touch','native-word-drag-and-tap-selection-both-verified','phone-portrait-and-landscape44-CSS-word-targets','original-textfields-and-submit-callback-retained');}
 return {submitted:/submit/i.test(next.text),filledSlots:row.filledSlots,totalSlots:row.totalSlots};
}
module.exports={completePage};
