'use strict';
// Real keyboard/mouse controller and original-animation acceptance. State observations are read-only.
const fs=require('node:fs'),path=require('node:path'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const uiMenus=process.env.SPARC_UI_MENUS==='1';
const {chromium}=require('playwright');const root=path.resolve(__dirname,'../..'),build=path.join(root,'Builds/WebGLOriginal');
const out=path.join(root,'Reports/OriginalScene/ControlsBrowser',new Date().toISOString().replace(/[:.]/g,'-'));fs.mkdirSync(out,{recursive:true});
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const all=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?all(path.join(dir,e.name)):[path.join(dir,e.name)]);
const exactSource=path.join(out,'original-controls-executed-source.cjs');fs.copyFileSync(__filename,exactSource);
const verified=JSON.parse(fs.readFileSync(path.join(root,'Reports/OriginalScene/verified-webgl-build.json'),'utf8'));
const provenance={execution:'Direct node Tools/browser/original-controls.cjs; no in-memory source modification.',originalRunnerPath:__filename,exactExecutedSourcePath:exactSource,sourceSha256:hash(exactSource),environment:{SPARC_TEST_ORIGIN:process.env.SPARC_TEST_ORIGIN||'http://127.0.0.1:18095/',SPARC_UI_MENUS:process.env.SPARC_UI_MENUS||'0'},inputSha256:verified.inputSha256,verifiedDevelopmentStatus:verified.status,timing:'Source runner includes real canvas refocus/frame settling before unchanged RMB drag. Objective and menu checks use real pointer clicks; no app setters or lowered controller assertions.'};
fs.writeFileSync(path.join(out,'runner-provenance.json'),JSON.stringify(provenance,null,2));
const report={status:'running',startedUtc:new Date().toISOString(),scope:'Original player controller and actual original rig; real input, no setters',checks:[],errors:[],externalRequests:[],samples:[],screenshots:[],buildHashes:Object.fromEntries(all(build).map(p=>[path.relative(build,p),hash(p)]))};
report.runnerProvenance={path:'runner-provenance.json',sourceSha256:provenance.sourceSha256,inputSha256:provenance.inputSha256,verifiedDevelopmentStatus:provenance.verifiedDevelopmentStatus};
const save=()=>fs.writeFileSync(path.join(out,'acceptance.json'),JSON.stringify(report,null,2));let browser,context,page,video;
async function state(){return page.evaluate(()=>window.__SPARC_ORIGINAL_QA);}
async function key(k,ms=140){await page.keyboard.down(k);await page.waitForTimeout(ms);await page.keyboard.up(k);await page.waitForTimeout(230);}
async function capture(name){const p=path.join(out,name+'.png');await page.screenshot({path:p});report.screenshots.push({name:name+'.png',sha256:hash(p)});report.samples.push({name,state:await state()});save();}
function nativeButtons(s){return s?.buttons||[];}
function settingsBack(s){return nativeButtons(s).find(b=>b.name==='BackButton'&&/(?:^|\/)SettingUI\//.test(b.path));}
async function waitObservation(predicate,timeout=10000){const deadline=Date.now()+timeout;while(Date.now()<deadline){const s=await state();if(s&&predicate(s))return s;await page.waitForTimeout(250);}return null;}
async function clickNative(button){
 assert(button?.rect,'Native control observation is missing');const r=button.rect,viewport=page.viewportSize();
 assert(r.w>0&&r.h>0&&r.x>=0&&r.y>=0&&r.x+r.w<=1.001&&r.y+r.h<=1.001,'Native control outside viewport: '+JSON.stringify(button));
 await page.mouse.move((r.x+r.w/2)*viewport.width,(r.y+r.h/2)*viewport.height);await page.waitForTimeout(270);
 const before=await state();report.uiMenus.pointerClicks.push({name:button.name,path:button.path,rect:r,viewport,pointerTargets:before?.pointerTargets||[]});save();
 await page.mouse.down();await page.waitForTimeout(170);await page.mouse.up();await page.waitForTimeout(450);
}
function uiBoundaryDelta(before,after){const known=new Set(before.boundaries||[]);return (after.boundaries||[]).filter(item=>!known.has(item));}
function storyActivities(s){return {block:s.block,lineId:s.lineId,completed:s.completed,games:(s.games||[]).map(g=>({white:g.white,Level:g.Level,Score:g.Score,Finished:g.Finished}))};}
function inspectToolbar(s){
 const viewport=page.viewportSize(),buttons=nativeButtons(s).filter(b=>/\/TOPRightBtns\//.test(b.path));assert(buttons.length>=3,'Native utility toolbar is incomplete');
 const measured=buttons.map(b=>({name:b.name,path:b.path,x:b.rect.x*viewport.width,y:b.rect.y*viewport.height,width:b.rect.w*viewport.width,height:b.rect.h*viewport.height}));
 for(const b of measured){assert(b.width>=43.9&&b.height>=43.9,'Toolbar target is below 44 CSS pixels: '+JSON.stringify(b));assert(b.y>=0&&b.y+b.height<viewport.height*.3,'Utility control is not in the top safe region: '+JSON.stringify(b));}
 return {viewport,buttons:measured};
}
async function inspectObjectiveCard(){
 const before=await state(),button=nativeButtons(before).find(b=>b.name==='ObjectiveButton');assert(button,'Native objective details control is absent');
 report.uiMenus.objective={before:storyActivities(before),button,toolbarDesktop:inspectToolbar(before)};await capture('06a-objective-expanded');
 await clickNative(button);const folded=await state();assert(nativeButtons(folded).some(b=>b.path===button.path),'Objective control disappeared after folding');
 assert.deepEqual(storyActivities(folded),storyActivities(before),'Folding the objective changed source story/activity state');report.uiMenus.objective.folded=storyActivities(folded);await page.mouse.move(720,450);await page.waitForTimeout(300);await capture('06b-objective-folded');
 await clickNative(nativeButtons(folded).find(b=>b.path===button.path));await page.mouse.move(720,450);await page.waitForTimeout(300);const reopened=await state();
 assert.deepEqual(storyActivities(reopened),storyActivities(before),'Reopening objective details changed source story/activity state');report.uiMenus.objective.reopened=storyActivities(reopened);await capture('06c-objective-reopened');report.checks.push('native-objective-fold-and-reopen-use-real-pointer','objective-details-preserve-story-and-activity-state','desktop-toolbar-at-top-with-44-css-pixel-targets');
}
async function inspectNativeMenus(){
 const before=await state();report.uiMenus={scope:'Real pointer interaction with existing Settings and optional local chat; no text, sending, account login or Unity command interface',pointerClicks:[],limitations:[],before:storyActivities(before)};
 await inspectObjectiveCard();
 const launcher=nativeButtons(before).find(b=>b.name==='SettingBtn');assert(launcher,'Native SettingBtn was not observed after controller checks');report.uiMenus.settings={launcher};
 await clickNative(launcher);let opened=await waitObservation(s=>!!settingsBack(s));assert(opened,'Clicking native SettingBtn did not reveal SettingUI/BackButton');
 assert(!nativeButtons(opened).some(b=>b.name==='ObjectiveButton'),'Objective HUD remained visible through modal Settings');
 assert.deepEqual(uiBoundaryDelta(before,opened),[],'Opening native Settings introduced an unimplemented runtime boundary');
 report.uiMenus.settings.desktopBack=settingsBack(opened);await capture('07-native-settings-desktop');report.checks.push('native-settings-opens-through-actual-pointer');
 await page.setViewportSize({width:960,height:600});await page.waitForTimeout(900);let resized=await state();const back=settingsBack(resized);assert(back,'Native Settings BackButton disappeared after resize');
 report.uiMenus.settings.resizedBack=back;await capture('08-native-settings-960x600');report.checks.push('native-settings-visible-at-960x600');
 await clickNative(back);const closed=await waitObservation(s=>!settingsBack(s)&&nativeButtons(s).some(b=>b.name==='SettingBtn'));assert(closed,'Native Settings BackButton did not restore the original toolbar');
 assert.deepEqual(uiBoundaryDelta(before,closed),[],'Settings close introduced an unimplemented runtime boundary');
 assert.deepEqual(storyActivities(closed),storyActivities(before),'Opening/closing Settings changed story or native activity state');report.checks.push('native-settings-back-button-restores-gameplay-ui','native-settings-preserves-story-and-activity-state');
 report.uiMenus.settings.resizedToolbar=inspectToolbar(closed);assert(nativeButtons(closed).some(b=>b.name==='ObjectiveButton'),'Settings close did not restore the objective card');report.checks.push('settings-modal-hides-and-restores-objective-card','resized-toolbar-keeps-44-css-pixel-targets');
 await page.setViewportSize({width:1440,height:900});await page.waitForTimeout(900);await capture('09-native-settings-closed');
 const chatBefore=await state(),chatLauncher=nativeButtons(chatBefore).find(b=>b.name==='MqttChatBtn');report.uiMenus.externalRequestsBeforeChat=report.externalRequests.length;
 if(!chatLauncher){report.uiMenus.chat={status:'not-available'};report.uiMenus.limitations.push('Native MqttChatBtn was not present in this isolated player; local chat panel acceptance is incomplete.');save();return;}
 const knownReturns=new Set(nativeButtons(chatBefore).filter(b=>b.name==='returnBtn').map(b=>b.path));report.uiMenus.chat={status:'checking-native-local-panel',launcher:chatLauncher};await clickNative(chatLauncher);
 const chatOpened=await waitObservation(s=>nativeButtons(s).some(b=>b.name==='returnBtn'&&!knownReturns.has(b.path)&&/(?:ChatCanvas|ChatGPT|inputchat)\//.test(b.path)),3500);
 if(!chatOpened){report.uiMenus.chat.status='native-launcher-without-local-panel';report.uiMenus.limitations.push('Actual MqttChatBtn click did not expose a local panel/returnBtn. Its account or network integration is outside this isolated UI check.');await capture('10-native-chat-unavailable');}
 else{
  report.uiMenus.chat.status='local-panel-opened';report.uiMenus.chat.openedButtons=nativeButtons(chatOpened).filter(b=>/(?:ChatCanvas|ChatGPT|inputchat)\//.test(b.path));
  await capture('10-native-chat-local-panel');const chatBack=report.uiMenus.chat.openedButtons.find(b=>b.name==='returnBtn'&&!knownReturns.has(b.path));
  if(chatBack){report.uiMenus.chat.returnButton=chatBack;await clickNative(chatBack);const chatClosed=await waitObservation(s=>!nativeButtons(s).some(b=>b.path===chatBack.path));assert(chatClosed,'Native chat returnBtn did not close its local panel');report.uiMenus.chat.status='local-panel-opened-and-closed';report.checks.push('native-local-chat-panel-pointer-open-and-return');await capture('11-native-chat-closed');}
  else report.uiMenus.limitations.push('Local chat panel was observed but no native returnBtn was available; no substitute close command was used.');
 }
 const after=await state();report.uiMenus.chat.runtimeBoundaries=uiBoundaryDelta(chatBefore,after);report.uiMenus.chat.blockedExternalRequestAttempts=report.externalRequests.slice(report.uiMenus.externalRequestsBeforeChat);
 if(report.uiMenus.chat.runtimeBoundaries.length)report.uiMenus.limitations.push('Original optional chat surfaced runtime integration boundaries, retained in chat.runtimeBoundaries.');
 if(report.uiMenus.chat.blockedExternalRequestAttempts.length)report.uiMenus.limitations.push('Optional chat attempted external requests; the isolated browser route blocked each attempt before transmission.');
 report.uiMenus.after=storyActivities(after);report.checks.push('ui-menu-checks-use-read-only-observations-and-real-pointer','chat-no-typing-sending-or-login');save();
}
async function main(){
 const origin=process.env.SPARC_TEST_ORIGIN||'http://127.0.0.1:18095/';assert(/^http:\/\/127\.0\.0\.1:\d+\/$/.test(origin));
 browser=await chromium.launch({headless:true,args:['--use-angle=metal','--enable-webgl']});report.browserVersion=browser.version();context=await browser.newContext({viewport:{width:1440,height:900},deviceScaleFactor:1,recordVideo:{dir:path.join(out,'recording'),size:{width:1280,height:800}}});
 await context.route(url=>/^https?:$/.test(url.protocol)&&url.origin!==new URL(origin).origin,route=>{report.externalRequests.push(route.request().url().split('?')[0]);return route.abort();});
 page=await context.newPage();video=page.video();page.on('pageerror',e=>report.errors.push(String(e)));page.on('console',m=>{if(m.type()==='error'||/NullReferenceException|MissingMethodException|RuntimeError:|OutOfMemory|Aborted\(/.test(m.text()))report.errors.push(m.text());});
 await page.goto(origin,{waitUntil:'domcontentloaded'});await page.waitForFunction(()=>!!window.__SPARC_ORIGINAL_QA,null,{timeout:120000});await page.locator('#unity-canvas').focus();await page.bringToFront();
 await page.mouse.move(720,475);await page.mouse.down();await page.waitForTimeout(180);await page.mouse.up();
 let s;const end=Date.now()+150000;
 while(Date.now()<end){s=await state();if(s.waypoints===4&&!s.speaking&&!s.timelines.some(t=>t.active&&t.playing))break;if(s.speaking)await key('Space');else await page.waitForTimeout(180);}
 assert(s.waypoints===4&&!s.speaking,'Original route did not release controls');await capture('01-original-camera');
 if(!s.player.thirdPerson)await key('KeyV');await page.waitForTimeout(400);s=await state();assert(s.player.thirdPerson);report.checks.push('real-third-person-switch');await capture('02-original-third-person');
 const before=s.player;const moves=[];await page.keyboard.down('KeyW');
 for(let i=0;i<10;i++){await page.waitForTimeout(120);moves.push(await state());if(i===5)await capture('03-original-walking-rig');}
 await page.keyboard.up('KeyW');await page.waitForTimeout(300);s=await state();report.movedMeters=Math.hypot(s.player.x-before.x,s.player.z-before.z);
 const playerActors=moves.flatMap(s=>s.actors.filter(a=>a.controller==='Customized_Character_Anime'));
 report.walkClips=[...new Set(playerActors.flatMap(a=>a.clips.map(c=>c.name)))];
 assert(report.movedMeters>1,'Keyboard input did not move the original body');assert(playerActors.some(a=>a.clips.some(c=>/Run|Walk/i.test(c.name))),'Original rig never selected its locomotion clip');assert(playerActors.some(a=>a.movedBones>2),'Walking clip did not move the original bones');report.checks.push('actual-player-movement-over-one-meter','original-locomotion-clip-and-bone-motion');
 const floor=s.player.y;let peak=floor;await page.keyboard.down('Space');await page.waitForTimeout(130);await page.keyboard.up('Space');
 for(let i=0;i<10;i++){await page.waitForTimeout(110);s=await state();peak=Math.max(peak,s.player.y);if(i===2)await capture('04-original-jump');}report.jumpHeight=peak-floor;assert(report.jumpHeight>0.08,'Original jump did not lift the body');report.checks.push('actual-original-body-jump');
 await key('KeyV');s=await state();assert(!s.player.thirdPerson);await capture('05-original-first-person');report.checks.push('real-first-person-switch');
 await page.locator('#unity-canvas').focus();await page.bringToFront();await page.waitForTimeout(900);s=await state();report.inputTimingAdjustment="Real canvas refocus and 900ms frame settling before RMB drag; 250ms down settling; 50ms between real pointer moves.";const yaw=s.player.yaw;await page.mouse.move(620,300);await page.mouse.down({button:'right'});await page.waitForTimeout(250);for(let step=1;step<=24;step++){await page.mouse.move(620+step*15,300+step);await page.waitForTimeout(50);}await page.waitForTimeout(200);await page.mouse.up({button:'right'});await page.waitForTimeout(250);s=await state();report.lookYawChange=((s.player.yaw-yaw+540)%360)-180;assert(Math.abs(report.lookYawChange)>5);report.checks.push('right-drag-original-camera-look');await capture('06-original-look');
 if(uiMenus)await inspectNativeMenus();
 assert.equal(report.errors.length,0,report.errors.join('\n'));if(uiMenus)assert.equal(report.uiMenus.externalRequestsBeforeChat??report.externalRequests.length,0,'External request occurred before optional chat UI inspection');else assert.equal(report.externalRequests.length,0);report.status='passed';report.finishedUtc=new Date().toISOString();
}
main().catch(async error=>{report.status='failed';report.failure=String(error.stack||error);if(page){report.lastState=await state().catch(()=>null);await page.screenshot({path:path.join(out,'failure.png'),timeout:5000}).catch(()=>{});}process.exitCode=1;}).finally(async()=>{report.finishedUtc=new Date().toISOString();save();console.log('CHECKS_COMPLETE',report.status);if(page&&!page.isClosed())await page.goto('about:blank',{waitUntil:'domcontentloaded',timeout:5000}).catch(()=>{});console.log('CLOSING_ISOLATED_CONTEXT');if(context)await context.close().catch(()=>{});console.log('FINALIZING_RECORDING');if(video)await video.saveAs(path.join(out,'original-controls.webm')).then(()=>report.video={path:'original-controls.webm',sha256:hash(path.join(out,'original-controls.webm'))}).catch(e=>report.videoFailure=String(e));if(browser)await browser.close().catch(()=>{});save();console.log(JSON.stringify({status:report.status,out,checks:report.checks,movedMeters:report.movedMeters,walkClips:report.walkClips,jumpHeight:report.jumpHeight,failure:report.failure},null,2));});
