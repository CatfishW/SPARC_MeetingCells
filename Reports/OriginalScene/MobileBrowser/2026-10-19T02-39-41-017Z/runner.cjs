'use strict';
// Isolated mobile emulation using real browser touches and read-only Unity observations.
// No keyboard, Unity setters, account login, chat submission, timer edits or teleportation.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const {chromium,devices}=require('playwright');
const root=path.resolve(__dirname,'../..'),build=path.join(root,'Builds/WebGLOriginal');
const out=path.join(root,'Reports/OriginalScene/MobileBrowser',new Date().toISOString().replace(/[:.]/g,'-'));fs.mkdirSync(out,{recursive:true});
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const files=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);
const report={status:'running',startedUtc:new Date().toISOString(),scope:'Chromium mobile emulation only; genuine multi-touch browser input, original controller/Settings, no gameplay setters',physicalDeviceAcceptance:false,limitations:['Emulation does not certify physical mobile devices, Safari, heat/battery behavior or real-device frame pacing.','Browser video is silent and is not audio acceptance.'],checks:[],errors:[],externalRequests:[],samples:[],screenshots:[],touchEvents:[],buildHashes:Object.fromEntries(files(build).map(file=>[path.relative(build,file),hash(file)]))};
fs.copyFileSync(__filename,path.join(out,'runner.cjs'));report.runner={path:'runner.cjs',sha256:hash(path.join(out,'runner.cjs')),nodeVersion:process.version};
const verified=path.join(root,'Reports/OriginalScene/verified-webgl-build.json');if(fs.existsSync(verified)){const b=JSON.parse(fs.readFileSync(verified));report.sourceInputSha256=b.inputSha256;}
let browser,context,page,cdp,video;const save=()=>fs.writeFileSync(path.join(out,'acceptance.json'),JSON.stringify(report,null,2));
const wait=ms=>page.waitForTimeout(ms);const state=()=>page.evaluate(()=>window.__SPARC_ORIGINAL_QA);
function distance(a,b){return Math.hypot(a.x-b.x,a.z-b.z);}
function yaw(a,b){return ((a-b+540)%360)-180;}
function magnitude(v){return Math.hypot(v?.x||0,v?.y||0);}
function center(rect){const v=page.viewportSize();return {x:(rect.x+rect.w/2)*v.width,y:(rect.y+rect.h/2)*v.height};}
function inside(rect){return rect&&rect.w>0&&rect.h>0&&rect.x>=-.001&&rect.y>=-.001&&rect.x+rect.w<=1.001&&rect.y+rect.h<=1.001;}
function pointer(id,p){return {id,x:p.x,y:p.y,radiusX:9,radiusY:9,force:1};}
function menuBack(s){return (s.buttons||[]).find(b=>b.name==='BackButton'&&/SettingUI\//.test(b.path));}
function story(s){return {block:s.block,lineId:s.lineId,completed:s.completed,games:(s.games||[]).map(g=>({white:g.white,Level:g.Level,Score:g.Score,Finished:g.Finished}))};}
async function touch(type,points){report.touchEvents.push({atMilliseconds:Date.now(),type,points:points.map(p=>({id:p.id,x:p.x,y:p.y}))});await cdp.send('Input.dispatchTouchEvent',{type,touchPoints:points});}
async function tap(point,id=61,held=[]){await touch('touchStart',[...held,pointer(id,point)]);await wait(130);await touch('touchEnd',[pointer(id,point)]);await wait(350);}
async function poll(predicate,timeout=10000){const end=Date.now()+timeout;while(Date.now()<end){const s=await state();if(s&&predicate(s))return s;await wait(120);}return null;}
async function capture(name){
 const file=path.join(out,name+'.png');let retries=0;
 for(;;){try{await page.screenshot({path:file,timeout:8000});break;}catch(error){report.screenshotRetries=report.screenshotRetries||[];report.screenshotRetries.push({name,attempt:retries+1,error:String(error)});if(++retries>=3)throw error;await wait(350);}}
 report.screenshots.push({name:name+'.png',sha256:hash(file),viewport:page.viewportSize(),retries});report.samples.push({name,state:await state()});save();
}
async function assertReleased(label){await wait(400);const s=await state();assert((s.touchPads||[]).every(p=>!p.held),label+': pad still owns a finger');assert(magnitude(s.player.touchMove)<.01,label+': movement input stuck');assert(!s.player.walking,label+': walking remained active');report.checks.push(label);return s;}
function activePads(s){const all=s.touchPads||[];return {move:all.find(p=>p.active&&!p.look),look:all.find(p=>p.active&&p.look)};}
function toolbarBounds(s,label){const v=page.viewportSize(),buttons=(s.buttons||[]).filter(b=>['SettingBtn','PerspectiveSwitchBtn','ViewResetBtn','MqttChatBtn'].includes(b.name));assert(buttons.some(b=>b.name==='SettingBtn'),label+': toolbar missing');for(const b of buttons){assert(inside(b.rect),label+': utility outside viewport '+b.path);assert(b.rect.w*v.width>=43.5&&b.rect.h*v.height>=43.5,label+': utility target under44 CSS pixels '+JSON.stringify(b));}return buttons;}
async function advanceOriginalRoute(){
 const v=page.viewportSize();await tap({x:v.width/2,y:v.height/2+36});const end=Date.now()+160000;let last='';
 while(Date.now()<end){const s=await state();if(s.lineId&&s.lineId!==last){last=s.lineId;report.samples.push({name:'original-route-line',lineId:s.lineId,block:s.block});}
  if(s.waypoints===4&&s.player?.canControl&&!s.speaking&&!s.timelines.some(t=>t.active&&t.playing)){report.checks.push('touch-start-and-original-dialogue-release-controls');return s;}
  if(s.speaking){const size=page.viewportSize();await tap({x:size.width-(size.width<700?128:147),y:size.height-63});}else await wait(250);
 }throw Error('Touch-only original intro/dialogue did not release controls');
}
function touchGeometry(s,label){
 const v=page.viewportSize(),pads=activePads(s),jump=(s.buttons||[]).find(b=>b.name==='JumpBtn');assert(pads.move&&pads.look&&jump,label+': native controls unavailable');
 const row={label,viewport:v,moveCss:{width:pads.move.rect.w*v.width,height:pads.move.rect.h*v.height},jumpCss:{width:jump.rect.w*v.width,height:jump.rect.h*v.height},moveRect:pads.move.rect,lookRect:pads.look.rect,jumpRect:jump.rect};
 (report.controlGeometry||(report.controlGeometry=[])).push(row);
 assert(row.moveCss.width>=127.5&&row.moveCss.height>=127.5,label+': thumbstick is less than128CSS pixels');assert(row.jumpCss.width>=75.5&&row.jumpCss.height>=75.5,label+': Jump is less than76CSS pixels');return row;
}
async function simultaneousMoveLook(label){
 let s=await state();assert(s.player.canControl,label+': player is not controllable');touchGeometry(s,label);const pads=activePads(s);assert(pads.move&&pads.look,label+': native touch surfaces missing');assert(inside(pads.move.rect)&&inside(pads.look.rect),label+': native pad outside viewport');
 const v=page.viewportSize(),m=center(pads.move.rect),l=center(pads.look.rect),diameter=pads.move.rect.w*v.width;
 l.x=Math.max(pads.look.rect.x*v.width+20,v.width*.54);const sweep=Math.min(Math.min(v.width,v.height)*.28,v.width-24-l.x),trials=[];
 let moving,successful=false;
 // A native ramp or random blocking cell may occupy a direction. Try other actual
 // thumb directions without changing the player, colliders, movement or thresholds.
 for(const direction of [{name:'forward',x:0,y:-1},{name:'right',x:1,y:0},{name:'left',x:-1,y:0},{name:'back',x:0,y:1}]){
  moving={x:m.x+direction.x*diameter*.45,y:m.y+direction.y*diameter*.45};const before=(await state()).player;
  await touch('touchStart',[pointer(11,m),pointer(22,l)]);const owned=await poll(a=>activePads(a).move?.held&&activePads(a).look?.held,2500);report.samples.push({name:label+'-'+direction.name+'-touch-start-ownership',state:owned||await state()});assert(owned,label+': both real fingers did not capture their native pads');
  for(let i=1;i<=18;i++){await touch('touchMove',[pointer(11,moving),pointer(22,{x:l.x+sweep*i/18,y:l.y+i*.25})]);await wait(75);if(i===2)report.samples.push({name:label+'-'+direction.name+'-early-analog-input',state:await state()});if(i===9)report.samples.push({name:label+'-'+direction.name+'-two-finger',state:await state()});}
  s=await state();const travelled=distance(s.player,before),turn=Math.abs(yaw(s.player.yaw,before.yaw));assert(activePads(s).move?.held&&activePads(s).look?.held,label+': independent fingers were not both retained');assert(magnitude(s.player.touchMove)>.1,label+': captured joystick never produced analog input');
  const trial={direction:direction.name,movedMeters:travelled,lookYawDegrees:turn,analog:s.player.touchMove};trials.push(trial);report[label]={trials,moveRect:pads.move.rect,lookRect:pads.look.rect,lookSwipeCssPixels:sweep};
  assert(turn>5,label+': concurrent look did not turn the camera ('+turn+')');
  const beforeRelease=s.player;await touch('touchEnd',[pointer(22,{x:l.x+sweep,y:l.y+4.5})]);await wait(450);s=await state();assert(activePads(s).move?.held&&!activePads(s).look?.held,label+': releasing look stole or retained the wrong pointer');assert(magnitude(s.player.touchMove)>.1,label+': releasing look cleared the other analog control');trial.afterLookReleaseMovedMeters=distance(s.player,beforeRelease);
  await touch('touchEnd',[]);await assertReleased(label+'-'+direction.name+'-all-up-clears-input');await capture(label+'-'+direction.name+'-gesture-result');
  if(travelled>1&&trial.afterLookReleaseMovedMeters>.2){successful=true;report[label].movedMeters=travelled;report[label].lookYawDegrees=turn;report[label].direction=direction.name;break;}
  trial.retryReason='Native geometry/crowd limited actual travel while analog input and independent ownership remained active';
 }
 assert(successful,label+': no actual thumb direction achieved>1m simultaneous movement plus>0.2m after releasing only look: '+JSON.stringify(trials));report.checks.push(label+'-simultaneous-movement-and-look',label+'-one-finger-release-keeps-other-active');
 await touch('touchStart',[pointer(11,m)]);await touch('touchMove',[pointer(11,moving)]);await wait(250);await touch('touchCancel',[]);await assertReleased(label+'-touchcancel-clears-input');
}
async function inspectSettingsWithHeldFinger(label){
 const before=await state(),button=(before.buttons||[]).find(b=>b.name==='SettingBtn');assert(button&&inside(button.rect),label+': native Settings launcher missing');const pads=activePads(before),m=center(pads.move.rect),held=pointer(11,{x:m.x,y:m.y-pads.move.rect.w*page.viewportSize().width*.4});
 await touch('touchStart',[pointer(11,m)]);await touch('touchMove',[held]);await wait(250);const moving=await state();assert(magnitude(moving.player.touchMove)>.05,label+': held movement was not active before menu');await tap(center(button.rect),32,[held]);const opened=await poll(s=>!!menuBack(s));assert(opened,label+': touch Settings did not open');assert(!opened.player.canControl,label+': menu did not gate gameplay');assert((opened.touchPads||[]).every(p=>!p.held),label+': menu left a finger captured');assert(magnitude(opened.player.touchMove)<.01,label+': menu retained analog movement');await capture(label+'-settings');
 const back=menuBack(opened);assert(inside(back.rect),label+': native Back target outside viewport');await tap(center(back.rect),33,[held]);const closed=await poll(s=>!menuBack(s)&&s.player.canControl);assert(closed,label+': touch Back did not restore gameplay');assert((closed.touchPads||[]).every(p=>!p.held),label+': old held finger rearmed after menu');assert(magnitude(closed.player.touchMove)<.01,label+': close resumed stale touch movement');
 await touch('touchEnd',[]);assert.deepEqual(story(closed),story(moving),label+': menu changed original story/activity');report.checks.push(label+'-settings-real-touch-open-back-and-held-input-clear');await capture(label+'-settings-closed');
}
async function verifyJump(){
 await wait(1000);let s=await state();const button=(s.buttons||[]).find(b=>b.name==='JumpBtn');assert(button&&inside(button.rect),'Native touch jump button unavailable');const floor=s.player.y;let peak=floor;await touch('touchStart',[pointer(45,center(button.rect))]);await wait(110);await touch('touchEnd',[]);
 for(let i=0;i<14;i++){await wait(65);s=await state();peak=Math.max(peak,s.player.y);if(i===2)await capture('landscape-touch-jump');}report.jumpHeight=peak-floor;assert(report.jumpHeight>.08,'Actual touch Jump failed to lift the original body');report.checks.push('native-touch-jump-lifts-original-body');
}
async function main(){
 const origin=process.env.SPARC_TEST_ORIGIN||'http://127.0.0.1:18095/';assert(/^http:\/\/127\.0\.0\.1:\d+\/$/.test(origin));report.origin=origin;
 browser=await chromium.launch({headless:true,args:['--use-angle=metal','--enable-webgl']});report.browserVersion=browser.version();context=await browser.newContext({viewport:{width:844,height:390},deviceScaleFactor:2,isMobile:true,hasTouch:true,userAgent:devices['Pixel 7'].userAgent,recordVideo:{dir:path.join(out,'recording'),size:{width:1280,height:800}}});
 await context.route(url=>/^https?:$/.test(url.protocol)&&url.origin!==new URL(origin).origin,route=>{report.externalRequests.push(route.request().url().split('?')[0]);return route.abort();});
 page=await context.newPage();cdp=await context.newCDPSession(page);await cdp.send('Emulation.setTouchEmulationEnabled',{enabled:true,maxTouchPoints:5});video=page.video();page.on('pageerror',e=>report.errors.push(String(e)));page.on('console',m=>{const message=m.text();if(message==='Ignored attempt to cancel a touchcancel event with cancelable=false, for example because scrolling is in progress and cannot be interrupted.'){(report.browserDiagnostics||(report.browserDiagnostics=[])).push({message,type:m.type(),location:m.location(),classification:'nonfatal-browser-intervention',reason:'This exact Chromium intervention concerns preventing the default of a noncancelable touchcancel event; native input release/cancel assertions remain mandatory.'});return;}if(m.type()==='error'||/NullReferenceException|MissingMethodException|RuntimeError:|OutOfMemory|Aborted\(/.test(message))report.errors.push(message);});
 const start=Date.now();save();await page.goto(origin,{waitUntil:'domcontentloaded'});await page.waitForFunction(()=>!!window.__SPARC_ORIGINAL_QA,null,{timeout:180000});report.startupSeconds=(Date.now()-start)/1000;report.inputEnvironment=await page.evaluate(()=>({userAgent:navigator.userAgent,maxTouchPoints:navigator.maxTouchPoints,coarsePointer:matchMedia('(pointer: coarse)').matches,devicePixelRatio:window.devicePixelRatio,renderToCssScale:window.SPARCBrowser.pixelScale()}));assert(/Android/.test(report.inputEnvironment.userAgent),'Android mobile user agent was not applied');assert(report.inputEnvironment.maxTouchPoints>=2&&report.inputEnvironment.coarsePointer,'Multi-touch/coarse mobile platform detection was not applied');await page.bringToFront();await page.locator('#unity-canvas').focus();await capture('01-mobile-start-landscape');await advanceOriginalRoute();await capture('02-mobile-hud-landscape');
 report.landscapeToolbar=toolbarBounds(await state(),'landscape');await simultaneousMoveLook('landscape');await verifyJump();await inspectSettingsWithHeldFinger('landscape');
 await page.setViewportSize({width:390,height:844});await wait(1200);await capture('portrait-hud');report.portraitToolbar=toolbarBounds(await state(),'portrait');await simultaneousMoveLook('portrait');await inspectSettingsWithHeldFinger('portrait');
 await page.setViewportSize({width:1024,height:768});await wait(1000);await capture('tablet-hud');report.tabletToolbar=toolbarBounds(await state(),'tablet');touchGeometry(await state(),'tablet');
 await page.setViewportSize({width:390,height:844});await wait(1200);
 report.dpr2=await page.evaluate(()=>({devicePixelRatio:window.devicePixelRatio,renderToCssScale:window.SPARCBrowser.pixelScale()}));assert(report.dpr2.devicePixelRatio>=1.9,'DPR2 emulation was not applied');assert(report.dpr2.renderToCssScale>1,'DPR2 run never exercised a scaled Unity backbuffer');report.dpr2.toolbar=toolbarBounds(await state(),'DPR2 portrait');await capture('dpr2-portrait-hud');report.checks.push('DPR2-emulation-keeps-thumb-targets-over44-CSS-pixels');
 report.performance=await page.evaluate(()=>new Promise(resolve=>{const samples=[];let last=performance.now();function sample(now){samples.push(now-last);last=now;if(samples.length<45)requestAnimationFrame(sample);else resolve({rafMedianMilliseconds:samples.slice().sort((a,b)=>a-b)[22],rafP95Milliseconds:samples.slice().sort((a,b)=>a-b)[42],devicePixelRatio:window.devicePixelRatio,canvasCss:{width:document.getElementById('unity-canvas').clientWidth,height:document.getElementById('unity-canvas').clientHeight}});}requestAnimationFrame(sample);}));
 const gameFrames=[];for(let i=0;i<12;i++){const s=await state();gameFrames.push({wallMilliseconds:Date.now(),frame:s.frame});await wait(120);}report.performance.unityFrameSamples=gameFrames;report.performance.unityObservedFramesPerSecond=(gameFrames.at(-1).frame-gameFrames[0].frame)/((gameFrames.at(-1).wallMilliseconds-gameFrames[0].wallMilliseconds)/1000);
 const final=await assertReleased('final-no-held-touch');report.lastState=final;assert.deepEqual(final.boundaries||[],[],'Unimplemented runtime boundary surfaced');assert.equal(report.errors.length,0,report.errors.join('\n'));assert.equal(report.externalRequests.length,0,'External request attempted');report.status='passed-mobile-emulation';
}
main().catch(async error=>{report.status='failed';report.failure=String(error.stack||error);if(page){report.lastState=await state().catch(()=>null);await page.screenshot({path:path.join(out,'failure.png'),timeout:5000}).catch(()=>{});}process.exitCode=1;}).finally(async()=>{report.finishedUtc=new Date().toISOString();save();if(context)await context.close().catch(()=>{});if(video)await video.saveAs(path.join(out,'mobile-controls.webm')).then(()=>report.video={path:'mobile-controls.webm',sha256:hash(path.join(out,'mobile-controls.webm'))}).catch(e=>report.videoFailure=String(e));if(browser)await browser.close().catch(()=>{});save();console.log(JSON.stringify({status:report.status,out,checks:report.checks,failure:report.failure},null,2));});
