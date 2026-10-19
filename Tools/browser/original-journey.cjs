'use strict';
// Acceptance uses genuine browser input (including touch in hybrid mode) and bounded, read-only Unity observations.
// No SendMessage, scene setters, teleport, score edits, director seeking or timer acceleration.
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const {chromium,devices}=require('playwright');
const root=path.resolve(__dirname,'../..'),build=path.resolve(process.env.SPARC_BUILD||path.join(root,'Builds/WebGLOriginal'));
// Laptop/desktop profiles: SPARC_VIEWPORT=1280x620 SPARC_DPR=2 (defaults keep the 1440x900 baseline).
const [viewportWidth,viewportHeight]=(process.env.SPARC_VIEWPORT||'1440x900').split('x').map(Number),deviceScale=Number(process.env.SPARC_DPR||1);
const mobileWorksheet=process.env.SPARC_MOBILE_WORKSHEET==='1';
const liveAgent=process.env.SPARC_LIVE_AGENT||'',stopAfterPeer=process.env.SPARC_STOP_AFTER_PEER==='1',requireWhiteWin=process.env.SPARC_REQUIRE_WHITE_WIN==='1',stopAfterWhite=process.env.SPARC_STOP_AFTER_WHITE==='1';
const stubScript=(process.env.SPARC_STUB_AGENT_SCRIPT||'success').split(',').map(s=>s.trim()).filter(Boolean),stubDelay=Number(process.env.SPARC_STUB_AGENT_DELAY_MS||0);let stubTurn=0;
const stubReplies={success:{ok:true,session:'stub',text:'Nice explanation! Losing the nucleus leaves room for hemoglobin, so B is correct.'},ask:{ok:true,session:'stub',text:'Ooh bestie, that is a start, no cap! But why would losing a nucleus help me carry more oxygen? Spill the deets, I am listening fr fr.'},error:{ok:false,code:'busy'}};
const whiteVictoryHoldMilliseconds=Number(process.env.SPARC_WHITE_VICTORY_HOLD_MS||6200);
assert(!requireWhiteWin||process.env.SPARC_WHITE_ASSIST!=='1','White win acceptance cannot use the activity exit assist');
assert(Number.isFinite(whiteVictoryHoldMilliseconds)&&whiteVictoryHoldMilliseconds>=0,'Invalid White victory hold interval');
const whiteNavigation=require('./original-white-navigation.cjs');
const stamp=new Date().toISOString().replace(/[:.]/g,'-');
const out=path.join(process.env.SPARC_OUT?path.resolve(process.env.SPARC_OUT):path.join(root,'Reports/OriginalScene/BrowserJourney'),stamp);fs.mkdirSync(out,{recursive:true});
const report={status:'running',startedUtc:new Date().toISOString(),scope:'Original scene, authored route, original dialogue and original native activities with real input',errors:[],warnings:[],snapshots:[],lines:[],externalRequests:[],checks:[],buildHashes:{}};
if(mobileWorksheet)report.scope+='; hybrid route with real mobile touch worksheet, not a pure-touch full journey';
if(requireWhiteWin||stopAfterWhite)report.requestedWhiteCoverage={requireWin:requireWhiteWin,stopAfterContinuation:stopAfterWhite,victoryHoldMilliseconds:whiteVictoryHoldMilliseconds};
report.whiteNavigation={levels:[],samples:[],stallEpisodes:[],maximumStationarySeconds:0};
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(p)).digest('hex');
const files=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);
for(const file of files(build))report.buildHashes[path.relative(build,file)]=hash(file);
// Bind the exact runner and imported decision/input helpers used by this attempt.
// Copies are evidence only: the running process still uses its normal local modules.
const runnerDirectory=path.join(out,'runner');fs.mkdirSync(runnerDirectory,{recursive:true});
report.runner={nodeVersion:process.version,sourceInputSha256:null,files:{}};
const verifiedBuildFile=path.join(root,'Reports/OriginalScene/verified-webgl-build.json');
if(fs.existsSync(verifiedBuildFile)){const verified=JSON.parse(fs.readFileSync(verifiedBuildFile,'utf8'));report.runner.sourceInputSha256=verified.inputSha256;}
for(const name of ['original-journey.cjs','original-white-navigation.cjs','original-mobile-minigame.cjs','original-mobile-worksheet.cjs']){
 const source=path.join(__dirname,name),copy=path.join(runnerDirectory,name);fs.copyFileSync(source,copy);
 report.runner.files[path.relative(root,source)]={path:path.relative(out,copy),sha256:hash(copy)};
}
const save=()=>{fs.writeFileSync(path.join(out,'acceptance.json'),JSON.stringify(report,null,2));fs.writeFileSync(path.join(path.dirname(out),'latest-browser-journey.json'),JSON.stringify({directory:path.relative(root,out),status:report.status,lastState:report.lastState,checks:report.checks,errors:report.errors,failure:report.failure},null,2));};
let browser,context,page,video,server;
const wait=ms=>page.waitForTimeout(ms);
const crowdOrigins=new Map();
async function state(){
 const s=await page.evaluate(()=>window.__SPARC_ORIGINAL_QA),c=s?.crowd;
 if(c){
  const proof=report.crowd||(report.crowd={maximumActive:0,spawned:0,expired:0,contacts:0,maximumSpawnedMovement:0,navReadySamples:0,spawners:[]});
  proof.maximumActive=Math.max(proof.maximumActive,c.active);proof.spawned=Math.max(proof.spawned,c.spawned);proof.expired=Math.max(proof.expired,c.expired);proof.contacts=Math.max(proof.contacts,c.contacts);
  proof.spawners=c.spawners;
  for(const actor of c.actors||[])if(actor.spawned){
   if(!crowdOrigins.has(actor.instanceId))crowdOrigins.set(actor.instanceId,actor.position);
   const first=crowdOrigins.get(actor.instanceId),p=actor.position;
   proof.maximumSpawnedMovement=Math.max(proof.maximumSpawnedMovement,Math.hypot(p.x-first.x,p.z-first.z));
   if(actor.navReady&&actor.hasPath)proof.navReadySamples++;
  }
 }
 return s;
}
async function key(name,ms=130){await page.keyboard.down(name);await wait(ms);await page.keyboard.up(name);await wait(200);}
async function clickButton(button){const r=button.rect,v=page.viewportSize();assert(r.w>0&&r.h>0);assert(r.x>=0&&r.y>=0&&r.x+r.w<=1.001&&r.y+r.h<=1.001,'Native button outside viewport: '+JSON.stringify(button));await page.mouse.move((r.x+r.w/2)*v.width,(r.y+r.h/2)*v.height);await page.mouse.down();await wait(160);await page.mouse.up();await wait(320);}
// SPARC_PEER_FRAMES=1: timed frames of Cap thinking and of the reply streaming in after each Send.
async function peerFrames(n){
 const sent=Date.now(),shot=name=>page.screenshot({path:path.join(out,name+'.png'),timeout:8000}).catch(()=>{});
 for(const at of [250,1400]){await wait(Math.max(0,at-(Date.now()-sent)));if((await state()).peerAgent?.Pending)await shot('peer-'+n+'-thinking-'+at);}
 while((await state()).peerAgent?.Pending&&Date.now()-sent<60000)await wait(50);
 const arrived=Date.now();for(const at of [60,350,800,1500,2600])
 {await wait(Math.max(0,at-(Date.now()-arrived)));await shot('peer-'+n+'-reply-'+at);}
 const action=(await state()).buttons.find(b=>/ContinueButton|CompleteBButton|CompleteAButton/.test(b.name)),v=page.viewportSize();
 if(action){await page.mouse.move((action.rect.x+action.rect.w/2)*v.width,(action.rect.y+action.rect.h/2)*v.height,{steps:6});await wait(220);await shot('peer-'+n+'-hover');await page.mouse.move(8,8);}
 if(process.env.SPARC_PEER_PHONE==='1'){const initial=page.viewportSize();for(const viewport of [{width:390,height:844},{width:844,height:390}]){await page.setViewportSize(viewport);await wait(900);await shot('peer-'+n+'-reply-'+viewport.width+'x'+viewport.height);}await page.setViewportSize(initial);await wait(700);}
}
async function auditUiProfiles(family){
 if(process.env.SPARC_VISUAL_UI_AUDIT!=='1')return;
 const done=report.visualUiAudit||(report.visualUiAudit={scope:'Real browser input/state captures; no scene or score mutation',families:[]});if(done.families.includes(family))return;done.families.push(family);const initial=page.viewportSize();
 for(const viewport of [{width:960,height:940},{width:800,height:1100}]){await page.setViewportSize(viewport);await wait(650);await capture('visual-'+family+'-'+viewport.width+'x'+viewport.height);}
 await page.setViewportSize(initial);await wait(650);save();
}
async function auditGameExit(family){
 if(process.env.SPARC_VISUAL_UI_AUDIT!=='1')return;const key=family+'-exit';const done=report.visualUiAudit;if(done?.families.includes(key))return;
 const exit=(await state()).buttons.find(b=>b.name==='ExitButton'&&b.path.includes('InstructionPanel'));assert(exit,'Actual minigame Exit action is missing');await clickButton(exit);await wait(300);await capture('visual-'+key);const cancel=(await state()).buttons.find(b=>b.path.includes('ExitConfirmation')&&/cancel|no/i.test(b.name+' '+b.text));assert(cancel,'Actual exit confirmation Cancel action is missing');await clickButton(cancel);done.families.push(key);save();
}
async function auditGameHelp(family){
 if(process.env.SPARC_VISUAL_UI_AUDIT!=='1')return;const label=family+'-help';const done=report.visualUiAudit;if(done?.families.includes(label))return;
 const before=await state(),game=before.games.find(g=>!g.Finished),help=before.buttons.find(b=>b.name==='__SPARC_MINI_Help');assert(help&&game,'Native game help missing');await clickButton(help);await capture('visual-'+label);await auditUiProfiles(label);const after=await state();assert.equal(after.games[0].Duration,game.Duration,'Help changed the authored duration');assert(after.games[0].Elapsed>=game.Elapsed,'Help rewound the clock');const close=after.buttons.find(b=>b.name==='__SPARC_MINI_Help');assert(close,'Help close control missing');await clickButton(close);done.families.push(label);save();
}
async function interactionBeforeTalk(s){
 if(!s.nearestTalk)return false;
 // The read-only probe publishes before the presentation's LateUpdate. Wait for
 // the newly approached actor instead of treating the preceding frame as final.
 const npc=s.nearestTalk,end=Date.now()+4000;
 while(Date.now()<end){s=await state();if(s.interaction?.npc===npc&&s.interaction.menuVisible)break;await wait(100);}
 if(s.interaction?.npc!==npc||!s.interaction.menuVisible)return false;
 const rows=report.restoredInteractions||(report.restoredInteractions=[]),id=s.interaction.sourceId;
 if(!rows.some(r=>r.sourceId===id)){
  assert(s.interaction.highlight&&s.interaction.nativeButtons===2&&s.interaction.controllers===3,'Source highlight/options contract missing');
  await capture('interaction-'+id);await auditUiProfiles('interaction-'+id);s=await state();rows.push({sourceId:id,npc:s.nearestTalk,highlight:s.interaction.highlight,menuVisible:s.interaction.menuVisible,nativeButtons:s.interaction.nativeButtons,method:'real approach; native Talk button pointer; no setters'});save();
 }
 const talk=s.buttons.find(b=>b.name==='OptionButton1'&&/talk/i.test(b.text));assert(talk,'Visible original Talk option missing');await clickButton(talk);return true;
}
function peerRectInside(rect){return rect&&['x','y','w','h'].every(key=>Number.isFinite(rect[key]))&&rect.w>0&&rect.h>0&&rect.x>=0&&rect.y>=0&&rect.x+rect.w<=1&&rect.y+rect.h<=1;}
function peerRectMotion(a,b,viewport){return Math.max(Math.abs(a.x-b.x)*viewport.width,Math.abs(a.y-b.y)*viewport.height,Math.abs(a.x+a.w-b.x-b.w)*viewport.width,Math.abs(a.y+a.h-b.y-b.h)*viewport.height);}
async function readyPeerField(initial,initialFrame){
 const begin=Date.now(),viewport=page.viewportSize(),proof={name:initial.name,initialRect:{...initial.rect},initialFrame,viewport,timeoutMilliseconds:5000,minimumSampleSpacingMilliseconds:150,maximumStableMotionCssPixels:1,status:'waiting',samples:[]};
 (report.peerFieldReadiness||(report.peerFieldReadiness=[])).push(proof);let previous=null,lastFrame=null;
 while(Date.now()-begin<=5000){
  const current=await state(),matches=(current?.fields||[]).filter(field=>field.name===initial.name),field=matches.length===1?matches[0]:null,now=Date.now();
  if(Number.isFinite(current?.frame)&&current.frame!==lastFrame){
   lastFrame=current.frame;const inside=field&&peerRectInside(field.rect);proof.samples.push({frame:current.frame,waitMilliseconds:now-begin,matchingFields:matches.length,inside:!!inside,rect:field?{...field.rect}:null});
   if(inside){
    const motion=previous?peerRectMotion(previous.rect,field.rect,viewport):null;
    if(previous&&now-previous.observedAt>=150&&motion<=1){proof.status='ready';proof.finalRect={...field.rect};proof.finalFrame=current.frame;proof.waitMilliseconds=now-begin;proof.stableMotionCssPixels=motion;save();return field;}
    previous={rect:{...field.rect},observedAt:now};
   }else previous=null;
  }
  const remaining=5000-(Date.now()-begin);if(remaining<=0)break;await wait(Math.min(150,remaining));
 }
 proof.status='timed-out';proof.waitMilliseconds=Date.now()-begin;save();throw Error('Native peer field did not settle fully inside the viewport within5 seconds: '+JSON.stringify(proof));
}
async function capture(name,{updateLastState=true}={}){
 let s=await state();
 const cue=s.timelines?.find(t=>t.name==='Storyline_introduce_yellow_circle'&&t.active&&t.playing);
 if(cue&&cue.time<2.5){
  // Review the settled native caption rather than its off-screen entrance frame.
  // This waits for the authored clock: no seeking, alpha edits or scene mutation.
  await page.waitForFunction(()=>{const t=window.__SPARC_ORIGINAL_QA?.timelines?.find(t=>t.name==='Storyline_introduce_yellow_circle');return t&&t.playing&&t.time>=2.5;},null,{timeout:6000});s=await state();
  report.followCapCaptionCapture={method:'wait for original Timeline caption entrance to settle',observedTime:s.timelines.find(t=>t.name==='Storyline_introduce_yellow_circle').time,noSeeking:true};
 }
 if(updateLastState)report.lastState=s;const target=path.join(out,name+'.png');
 for(let attempt=1;;attempt++){
  try{await page.screenshot({path:target,timeout:8000});break;}
  catch(error){report.screenshotRetries=report.screenshotRetries||[];report.screenshotRetries.push({name,attempt,error:String(error)});save();if(attempt>=3)throw error;}
 }
 report.snapshots.push({name,state:s,sha256:hash(target)});save();console.log('FRAME',name,s.block,s.objective,s.waypoints,s.games);return s;
}
function normalize(deg){return ((deg+540)%360)-180;}
const originalHeartEndingSeconds=20.3166666667;
function completedHandoff(s,viewport){
 const h=s?.handoff,r=h?.replayRect;
 return h?.started===true&&h.completed===true&&h.playing===false&&h.phase==='complete'&&h.starts===1&&Number.isFinite(h.duration)&&Math.abs(h.duration-originalHeartEndingSeconds)<.03&&((Number.isFinite(h.peakTime)&&h.peakTime>=h.duration-.35)||(Number.isFinite(h.elapsed)&&h.elapsed>=h.duration-.35))&&h.cardVisible===true&&s.player?.canControl===false&&s.nearestTalk===null&&peerRectInside(h.cardRect)&&peerRectInside(r)&&r.w*viewport.width>=44&&r.h*viewport.height>=44;
}
function freshReplayStart(s){
 const h=s?.handoff;
 return s?.started===false&&h?.started===false&&h.completed===false&&h.starts===0&&h.cardVisible===false&&s.waypoints===0&&Array.isArray(s.games)&&s.games.length===0;
}
async function verifyManualReplay(terminal){
 const viewport=page.viewportSize(),r=terminal.handoff.replayRect,origin=new URL(page.url()).origin;
 assert(completedHandoff(terminal,viewport),'Replay must follow the locked, completed first playthrough');
 const snapshot=report.snapshots.find(row=>row.name==='final-state');assert(snapshot&&snapshot.state===terminal,'Replay needs immutable first-playthrough final-state evidence');
 const proof=report.manualReplay={status:'running',method:'real-touch-on-completion-Play-again',terminalSnapshot:{name:snapshot.name,sha256:snapshot.sha256},terminalFrame:terminal.frame,beforeOrigin:origin,viewport,targetRect:{...r},touchCss:{x:(r.x+r.w/2)*viewport.width,y:(r.y+r.h/2)*viewport.height},timeoutMilliseconds:30000};save();
 const begin=Date.now();await page.touchscreen.tap(proof.touchCss.x,proof.touchCss.y);let s;
 while(Date.now()-begin<=30000){s=await state();if(freshReplayStart(s))break;await wait(150);}
 assert(freshReplayStart(s),'Manual Play again did not return to the idle, reset start state');
 proof.waitMilliseconds=Date.now()-begin;await wait(500);
 assert.equal(new URL(page.url()).origin,origin,'Manual Replay navigated away from the original browser origin');
 s=await capture('manual-replay-fresh-start',{updateLastState:false});assert(freshReplayStart(s),'Fresh Start state changed before its Replay evidence capture');
 const picture=report.snapshots.find(row=>row.name==='manual-replay-fresh-start');
 Object.assign(proof,{status:'passed',afterOrigin:new URL(page.url()).origin,idleHoldMilliseconds:500,startState:s,screenshot:{name:picture.name+'.png',sha256:picture.sha256}});
 assert.strictEqual(report.lastState,terminal,'Manual Replay overwrote the first-playthrough terminal snapshot');
 report.checks.push('real-touch-manual-replay-returns-to-idle-start-without-automatic-replay');save();
}
let stalled=0,lastPosition=null;
const talkApproaches=new Map();
let whiteStarted=0,whitePrevious=null,whiteStationarySince=0,whiteLastSample=0,whiteLastSummary='',whiteVictoryChecked=false;
function observeWhite(game,intent=null){
 const now=Date.now(),diagnostics=report.whiteNavigation,p=game.miniPlayer;
 if(!whiteStarted)whiteStarted=now;
 const level=diagnostics.levels.find(row=>row.level===game.Level);
 if(!level)diagnostics.levels.push({level:game.Level,firstObservedElapsed:game.Elapsed,initialHostiles:game.remaining,initialScore:game.Score,obstacles:game.board?.obstacles||[],playerRadius:game.board?.playerRadius,boardRect:game.board?.rect});
 const summary=[game.Level,game.Score,game.remaining,game.Transitioning,game.Finished].join('/');
 if(p&&whitePrevious&&game.Level===whitePrevious.level&&!game.Transitioning&&!game.Finished){
  const moved=Math.hypot((p.x-whitePrevious.position.x)*1440,(p.y-whitePrevious.position.y)*900);
  if(moved>4){if(whiteStationarySince&&now-whiteStationarySince>2000)diagnostics.stallEpisodes.push({level:game.Level,elapsed:game.Elapsed,seconds:(now-whiteStationarySince)/1000,position:p});whiteStationarySince=0;}
  else if(!whiteStationarySince)whiteStationarySince=now;
 }else whiteStationarySince=0;
 if(whiteStationarySince)diagnostics.maximumStationarySeconds=Math.max(diagnostics.maximumStationarySeconds,(now-whiteStationarySince)/1000);
 if(p)whitePrevious={level:game.Level,position:{...p}};
 if(summary!==whiteLastSummary||now-whiteLastSample>=500){
  diagnostics.samples.push({wallSeconds:(now-whiteStarted)/1000,elapsed:game.Elapsed,level:game.Level,score:game.Score,remaining:game.remaining,transitioning:game.Transitioning,finished:game.Finished,position:p,tokens:game.tokens,stationarySeconds:whiteStationarySince?(now-whiteStationarySince)/1000:0,intent:intent?{keys:intent.keys,targetId:intent.targetId,holdMilliseconds:intent.holdMilliseconds,pathLength:intent.path.length,cost:intent.cost,unreachableDistance:intent.unreachableDistance,reachable:intent.reachable,candidates:intent.candidates}:null});
  whiteLastSample=now;
 }else if(intent&&diagnostics.samples.length){const latest=diagnostics.samples[diagnostics.samples.length-1];if(latest.elapsed===game.Elapsed)latest.intent={keys:intent.keys,targetId:intent.targetId,holdMilliseconds:intent.holdMilliseconds,pathLength:intent.path.length,cost:intent.cost,unreachableDistance:intent.unreachableDistance,reachable:intent.reachable,candidates:intent.candidates};}
 if(summary!==whiteLastSummary){console.log('WHITE_PROGRESS',JSON.stringify({elapsed:game.Elapsed,level:game.Level,score:game.Score,remaining:game.remaining,transitioning:game.Transitioning,finished:game.Finished,position:p}));whiteLastSummary=summary;save();}
}
function originalWhiteBranch(s,name){return s.block===name||(s.completed||[]).includes(name)||(s.trace||[]).some(row=>row.startsWith(name+'/'));}
async function moveTowards(s,target){
 const p=s.player,dx=target.x-p.x,dz=target.z-p.z,distance=Math.hypot(dx,dz),yaw=Math.atan2(dx,dz)*180/Math.PI;
 let error=normalize(yaw-p.yaw);
 if(Math.abs(error)>7){
  const delta=Math.max(-370,Math.min(370,error/0.138));await page.mouse.move(720,310);await wait(60);await page.mouse.down({button:'right'});await wait(70);await page.mouse.move(720+delta,310,{steps:5});await wait(100);await page.mouse.up({button:'right'});await wait(120);
  return;
 }
 if(lastPosition&&Math.hypot(p.x-lastPosition.x,p.z-lastPosition.z)<0.04)stalled++;else stalled=0;lastPosition=p;
 if(stalled>4){await key('Space',120);await key(stalled%2?'KeyA':'KeyD',350);stalled=0;}
 await key('KeyW',Math.min(650,Math.max(120,distance/4*750)));
}
async function verifyEnding(){
 if(process.env.SPARC_VERIFY_ENDING!=='1')return;
 let s=await state(),ending=s.timelines.find(t=>t.name==='Storyline_MeetingCellsTransition2Heart');assert(ending&&ending.starts===1&&ending.duration>0&&ending.duration<120,'Original ending is not running');
 assert(Math.abs(ending.duration-originalHeartEndingSeconds)<.03,'Original20.316-second ending duration changed');
 const duration=ending.duration,begin=Date.now(),samples=[];let peak=ending.time;
 while(Date.now()-begin<(duration+4)*1000){s=await state();ending=s.timelines.find(t=>t.name==='Storyline_MeetingCellsTransition2Heart');assert(ending&&ending.starts===1,'Original ending replayed');peak=Math.max(peak,ending.time);samples.push({wallSeconds:(Date.now()-begin)/1000,time:ending.time,playing:ending.playing,active:ending.active});if(samples.length===25)await capture('original-ending-midpoint');if(peak>=duration-0.35&&!ending.playing)break;await wait(150);}
 assert(peak>=duration-0.35,'Ending did not reach its authored endpoint');assert.equal(ending.starts,1);assert(!ending.playing,'Original ending is still playing');assert.equal(report.errors.length,0,'Errors during original ending');
 const viewport=page.viewportSize(),readyDeadline=Date.now()+5000;
 while(!completedHandoff(s,viewport)&&Date.now()<readyDeadline){await wait(150);s=await state();}
 assert(completedHandoff(s,viewport),'Native ending lacks a locked, visible completion handoff: '+JSON.stringify({handoff:s.handoff,player:s.player,nearestTalk:s.nearestTalk}));
 const holdBegin=Date.now(),handoffSamples=[];
 while(Date.now()-holdBegin<1500){
  s=await state();ending=s.timelines.find(t=>t.name==='Storyline_MeetingCellsTransition2Heart');
  assert(completedHandoff(s,viewport)&&ending?.starts===1&&!ending.playing,'Completed handoff unlocked input, lost its card or automatically replayed');
  handoffSamples.push({frame:s.frame,milliseconds:Date.now()-holdBegin,handoff:s.handoff,canControl:s.player.canControl,nearestTalk:s.nearestTalk});await wait(150);
 }
 assert(new Set(handoffSamples.map(row=>row.frame)).size>=3,'Handoff stability needs fresh native observations');
 s=await capture('original-ending-complete');assert(completedHandoff(s,viewport),'Completion card disappeared after the no-input hold');
 report.endingPlayback={duration,observedPeakTime:peak,starts:ending.starts,finishedPlaying:!ending.playing,samples,viewport,handoff:s.handoff,handoffHoldMilliseconds:Date.now()-holdBegin,handoffSamples};report.checks.push('full-authored-ending-playback-no-seek-no-replay','native-ending-locked-completion-card-no-automatic-replay');save();
}
async function main(){
 server=http.createServer((req,res)=>{
  try{
   const url=new URL(req.url,'http://localhost');if(url.pathname==='/favicon.ico'){res.writeHead(204);res.end();return;}
   // Layout-only runs without provider credentials: SPARC_STUB_AGENT=1 answers the peer contract locally.
   // SPARC_STUB_AGENT_SCRIPT=ask,error,success plays those replies in turn (the last repeats); SPARC_STUB_AGENT_DELAY_MS holds each one.
   if(process.env.SPARC_STUB_AGENT==='1'&&url.pathname==='/api/sparc/agent'&&req.method==='POST'){req.resume();const kind=stubScript[Math.min(stubTurn++,stubScript.length-1)];setTimeout(()=>{res.writeHead(200,{'Content-Type':'application/json'});res.end(JSON.stringify(stubReplies[kind]||stubReplies.success));},stubDelay);report.stubAgent=true;(report.stubReplies||(report.stubReplies=[])).push(kind);return;}
   // SPARC_LIVE_AGENT=https://ping.agaii.org forwards the game's peer POST to the deployed service as that page origin would send it.
   if(liveAgent&&url.pathname==='/api/sparc/agent'&&req.method==='POST'){const chunks=[];req.on('data',c=>chunks.push(c));req.on('end',async()=>{try{const answer=await fetch(liveAgent+'/api/sparc/agent',{method:'POST',headers:{'Content-Type':'application/json',Origin:liveAgent,'Sec-Fetch-Site':'same-origin'},body:Buffer.concat(chunks)});const body=await answer.text();let parsed=null;try{parsed=JSON.parse(body);}catch{}(report.liveAgentResponses||(report.liveAgentResponses=[])).push({status:answer.status,ok:parsed?.ok===true,code:parsed?.code,text:parsed?.text});save();res.writeHead(answer.status,{'Content-Type':'application/json','Cache-Control':'no-store'});res.end(body);}catch(e){(report.liveAgentResponses||(report.liveAgentResponses=[])).push({error:String(e)});res.writeHead(502);res.end();}});return;}
   const p=path.resolve(build,'.'+(url.pathname==='/'?'/index.html':decodeURIComponent(url.pathname)));
   if(!p.startsWith(build+path.sep)||!fs.existsSync(p)||!fs.statSync(p).isFile()){res.writeHead(404);res.end();return;}
   const size=fs.statSync(p).size,compressed=p.endsWith('.unityweb');const mime=/\.wasm(?:\.unityweb)?$/.test(p)?'application/wasm':/\.js(?:\.unityweb)?$/.test(p)?'application/javascript':/\.html$/.test(p)?'text/html; charset=utf-8':/\.mp4$/.test(p)?'video/mp4':/\.webm$/.test(p)?'video/webm':'application/octet-stream';
   const headers={'Content-Type':mime,'Cache-Control':'no-store',...(compressed?{'Content-Encoding':'gzip'}:{})};const range=/^bytes=(\d+)-(\d*)$/.exec(req.headers.range||'');
   if(range&&!compressed){const start=Number(range[1]),end=Math.min(size-1,range[2]?Number(range[2]):size-1);if(start>=size||start>end){res.writeHead(416,{'Content-Range':'bytes */'+size});res.end();return;}res.writeHead(206,{...headers,'Accept-Ranges':'bytes','Content-Range':`bytes ${start}-${end}/${size}`,'Content-Length':end-start+1});fs.createReadStream(p,{start,end}).pipe(res);}
   else{res.writeHead(200,{...headers,'Content-Length':size});fs.createReadStream(p).pipe(res);}
  }catch(e){res.writeHead(500);res.end();}
 });await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));
 const origin=process.env.SPARC_TEST_ORIGIN||'http://127.0.0.1:'+server.address().port+'/';if(!/^http:\/\/(127\.0\.0\.1|localhost):\d+\/$/.test(origin))throw Error('External test origin must be loopback');
 if(liveAgent){if(!/^https:\/\/[^/]+$/.test(liveAgent))throw Error('SPARC_LIVE_AGENT must be an https origin such as https://ping.agaii.org');if(process.env.SPARC_STUB_AGENT==='1')throw Error('SPARC_LIVE_AGENT exercises the deployed agent; unset SPARC_STUB_AGENT');report.liveAgent=liveAgent;}
 browser=await chromium.launch({headless:true,args:['--use-angle=metal','--enable-webgl']});report.browserVersion=browser.version();
 context=await browser.newContext({viewport:{width:viewportWidth,height:viewportHeight},deviceScaleFactor:deviceScale,...(mobileWorksheet?{isMobile:true,hasTouch:true,userAgent:devices['Pixel 7'].userAgent}:{}),recordVideo:{dir:path.join(out,'video'),size:{width:1280,height:800}}});page=await context.newPage();video=page.video();
 await context.route('**/*',r=>{const u=r.request().url();if(u.startsWith(origin)||/^(blob:|data:)/.test(u))return r.continue();report.externalRequests.push(u.split('?')[0]);return r.abort();});
 page.on('pageerror',e=>{report.errors.push(String(e));save();});
 page.on('console',m=>{const text=m.text();if(text==='Ignored attempt to cancel a touchcancel event with cancelable=false, for example because scrolling is in progress and cannot be interrupted.'){(report.browserDiagnostics||(report.browserDiagnostics=[])).push({message:text,type:m.type(),location:m.location(),classification:'nonfatal-browser-intervention',reason:'This exact Chromium intervention concerns preventing the default of a noncancelable touchcancel event; native input release/cancel assertions remain mandatory.'});save();return;}if(m.type()==='error'||/NullReferenceException|MissingMethodException|RuntimeError:|OutOfMemory|Aborted\(/.test(text)){report.errors.push(text);save();console.error('RUNTIME_ERROR',text);}else if(m.type()==='warning'&&report.warnings.length<200)report.warnings.push(text);});
 save();await page.goto(origin,{waitUntil:'domcontentloaded'});await page.waitForFunction(()=>!!window.__SPARC_ORIGINAL_QA,null,{timeout:180000});await page.locator('#unity-canvas').focus();await capture('01-start');if(mobileWorksheet)report.mobileInputEnvironment=await page.evaluate(()=>({userAgent:navigator.userAgent,maxTouchPoints:navigator.maxTouchPoints,coarsePointer:matchMedia('(pointer: coarse)').matches,devicePixelRatio:window.devicePixelRatio}));
 await page.keyboard.press('Enter');await page.waitForFunction(()=>window.__SPARC_ORIGINAL_QA.started,null,{timeout:10000});
 // Physical NPC approaches and original moving blockers need a navigation budget;
 // this changes only the audit deadline, never a Unity timer or gameplay clock.
 const begun=Date.now(),deadline=begun+Number(process.env.SPARC_JOURNEY_SECONDS||900)*1000;
 let lastLine='',lastObjective='',lastStage='',oxygenStarted=0,oxygenSeen=false,whiteSeen=false,questionAnswered=false,workbookPages=0,workbookSubmitted=false;let lastSnapshot=0,lastSave=0;
 while(Date.now()<deadline){
  let s=await state();report.lastState=s;
  if(!s)throw Error('Read-only runtime observation disappeared');
  if(Date.now()-lastSave>3000){lastSave=Date.now();save();}
  if(report.errors.length)throw Error('Runtime errors recorded');
  if(whiteSeen&&originalWhiteBranch(s,'WBC_LOSE')){report.whiteContinuation='WBC_LOSE';if(requireWhiteWin)throw Error('Original White loss branch was reached; a browser win was required');}
  if(whiteSeen&&originalWhiteBranch(s,'WBC_WIN')&&report.whiteContinuation!=='WBC_WIN'){report.whiteContinuation='WBC_WIN';if(requireWhiteWin){assert(report.whiteFinal?.Finished&&report.whiteFinal.Level===3&&report.whiteFinal.remaining===0,'Win branch must follow actual collection through all three original levels');assert(!report.whiteAssisted,'White activity must not be exited for win acceptance');assert(report.whiteKeyboardNavigation,'White win must include actual browser keyboard movement');report.checks.push('three-original-white-levels-with-real-keyboard-input','native-white-victory-control-enters-original-win-branch');}if(stopAfterWhite){await capture('white-win-original-continuation');report.status='passed-original-white-win-route';break;}}
  if(whiteSeen&&stopAfterWhite&&report.whiteContinuation==='WBC_LOSE'){report.status='passed-original-white-loss-route';break;}
  if(s.lineId!==lastLine&&s.lineId){lastLine=s.lineId;report.lines.push({id:s.lineId,block:s.block,speaker:s.speaker,text:s.line});console.log('LINE',s.block,s.lineId,s.line.slice(0,110));}
  const stage=s.games?.length?(s.games[0].white?'white-activity':'oxygen-activity'):(s.speaking?s.block:(s.reachObjectives?.map(o=>o.description).join('|')||s.objective||'exploration'));
  if(stage!==lastStage&&Date.now()-lastSnapshot>900){lastStage=stage;lastSnapshot=Date.now();await capture(String(report.snapshots.length+1).padStart(2,'0')+'-'+String(stage).replace(/[^a-z0-9-]/gi,'_').slice(0,48));}
  if(s.speaking){await key('Space');continue;}
  if(s.games?.length){
   const game=s.games[0];
   if(game.white)whiteSeen=true;
   if(game.white){report.whiteFinal=game;observeWhite(game);}
   if(!game.white&&!oxygenSeen){oxygenSeen=true;oxygenStarted=Date.now();}
   if(process.env.SPARC_VISUAL_UI_AUDIT==='1'&&!report.visualUiAudit?.families.includes(game.white?'white-play':'oxygen-play')){await auditUiProfiles(game.white?'white-play':'oxygen-play');await auditGameHelp(game.white?'white':'oxygen');await auditGameExit(game.white?'white':'oxygen');continue;}
   if(process.env.SPARC_VISUAL_UI_AUDIT==='1'&&game.white&&game.Transitioning&&game.Level>1&&!report.visualUiAudit?.families.includes('white-loading')){await capture('visual-white-loading');report.visualUiAudit.families.push('white-loading');}
   if(mobileWorksheet&&!game.Finished&&game.Elapsed>1&&!report.mobileMinigameTouch?.[game.white?'white':'oxygen']?.passed){await require('./original-mobile-minigame.cjs').verify({page,state,capture,report,game});continue;}
   if(!game.Finished&&((!game.white&&process.env.SPARC_RBC_ASSIST==='1')||(game.white&&process.env.SPARC_WHITE_ASSIST==='1'))&&game.Elapsed>5){if(game.white)report.whiteAssisted=true;else report.oxygenAssisted=true;const exit=s.buttons.find(b=>/ExitButton/.test(b.name));if(exit)await clickButton(exit);else await key('Escape');await wait(250);const prompt=await state();const confirm=prompt.buttons.find(b=>/confirm|yes/i.test(b.name+' '+b.text));if(confirm)await clickButton(confirm);else await key('Enter');continue;}

   if(game.Finished){
    if(game.white&&process.env.SPARC_VISUAL_UI_AUDIT==='1'&&!report.whiteResultCaptured){await capture('visual-white-result');report.whiteResultCaptured=true;}
    if(!game.white){report.oxygenActualWallSeconds=(Date.now()-oxygenStarted)/1000;report.oxygenFinal=game;if(!report.oxygenEndCaptured){report.oxygenEndCaptured=true;await capture('oxygen-full-timer-finished');}const exits=s.buttons.filter(b=>/ExitGameBtn|Exit.*Button|ExitButton/.test(b.name));const exit=exits.find(b=>/VictoryScreen|EndGame/i.test(b.path))||exits[0];if(exit)await clickButton(exit);else await key('Escape');await wait(400);const prompt=await state();const confirm=prompt.buttons.find(b=>/confirm|yes/i.test(b.name+' '+b.text));if(confirm)await clickButton(confirm);else await key('Enter');}
    else if(requireWhiteWin||(game.Level===3&&game.remaining===0)){
     assert(game.Level===3&&game.remaining===0,'White timer ended before all three levels were collected: '+JSON.stringify({level:game.Level,remaining:game.remaining,score:game.Score,elapsed:game.Elapsed}));
     if(requireWhiteWin&&!whiteVictoryChecked){await capture('white-three-level-victory-before-native-continue');await wait(whiteVictoryHoldMilliseconds);const held=await state(),active=held.games?.find(g=>g.white);assert(active?.Finished&&active.Level===3&&active.remaining===0,'Original White victory must wait for its native Continue control');report.whiteVictoryHeldSeconds=whiteVictoryHoldMilliseconds/1000;whiteVictoryChecked=true;report.checks.push('white-victory-remains-until-native-continue');}
     const current=await state();const exit=current.buttons.find(b=>/VictoryScreen/.test(b.path)&&!/GameLost/i.test(b.path)&&/ExitButton|Continue|Next/i.test(b.name+' '+b.text));assert(exit,'Original White victory Continue control was not visible');report.whiteVictoryControl={name:exit.name,path:exit.path,text:exit.text};await clickButton(exit);
    }else await wait(500);
    continue;
   }
   if(game.white&&game.board&&game.miniPlayer){
    const intent=whiteNavigation.chooseMovement(game);observeWhite(game,intent);if(intent&&intent.keys.length){for(const k of intent.keys)await page.keyboard.down(k);await wait(intent.holdMilliseconds);for(const k of intent.keys)await page.keyboard.up(k);await wait(90);report.whiteKeyboardNavigation=true;continue;}
   }
   if(game.miniPlayer&&game.tokens?.length){
    const p=game.miniPlayer;const candidates=!game.white&&game.tokens.some(t=>t.attached)&&(game.deposits||[]).length?game.deposits:game.tokens.filter(t=>game.white?t.hostile:!t.attached);const target=candidates.sort((a,b)=>Math.hypot(a.x-p.x,a.y-p.y)-Math.hypot(b.x-p.x,b.y-p.y))[0];
    if(target){let keys=[];if(Math.abs(target.x-p.x)>0.01)keys.push(target.x>p.x?'KeyD':'KeyA');if(game.white&&Math.abs(target.y-p.y)>0.01)keys.push(target.y<p.y?'KeyW':'KeyS');for(const k of keys)await page.keyboard.down(k);await wait(180);for(const k of keys)await page.keyboard.up(k);await wait(60);continue;}
   }
   const cycle=Math.floor((Date.now()-begun)/1700)%2;await key(cycle?'KeyA':'KeyD',450);continue;
  }
  if(s.fields?.length&&!s.buttons.some(b=>b.name==='NextButton'))
  {
   if(s.peerAgent?.Pending){await wait(250);continue;}
   const completion=s.buttons.find(b=>b.name==='CompleteBButton'||b.name==='CompleteAButton');if(completion){await capture('original-peer-feedback');if(stopAfterPeer){report.status='passed-original-peer-feedback';break;}await clickButton(completion);await wait(400);continue;}
   const again=s.buttons.find(b=>b.name==='ContinueButton');if(again){await clickButton(again);await wait(250);continue;}
   await auditUiProfiles('peer-input');s=await state();const initialField=s.fields.find(f=>/InputField/.test(f.name))||s.fields[0],field=await readyPeerField(initialField,s.frame);await capture('peer-field-ready-'+String((report.peerSubmissions||0)+1));await clickButton(field);await page.keyboard.type('Red blood cells do not contain a nucleus. Losing the nucleus leaves more room for hemoglobin to carry oxygen, and helps them bend through narrow capillaries. That is why B is correct.',{delay:8});
   await wait(350);if(process.env.SPARC_PEER_FRAMES==='1')await page.screenshot({path:path.join(out,'peer-'+((report.peerSubmissions||0)+1)+'-typed.png'),timeout:8000}).catch(()=>{});const send=(await state()).buttons.find(b=>b.name==='Send'||b.name==='__SPARC_UI_PeerSend');assert(send,'A visible, enabled Send action must be available after typing');await clickButton(send);report.peerSendMethod='real-native-Send-button';report.peerSubmissions=(report.peerSubmissions||0)+1;assert(report.peerSubmissions<=7,'Peer input did not converge or send');if(process.env.SPARC_PEER_FRAMES==='1')await peerFrames(report.peerSubmissions);else await wait(600);continue;
  }
  const retryPeer=s.buttons.find(b=>b.name==='ContinueButton');if(retryPeer){await auditUiProfiles('peer-feedback');await capture('original-peer-retry-'+(s.peerAgent?.Failures||0));await clickButton((await state()).buttons.find(b=>b.name==='ContinueButton'));await wait(250);continue;}
  const completion=s.buttons.find(b=>b.name==='CompleteBButton'||b.name==='CompleteAButton');if(completion){await capture('original-peer-feedback');if(stopAfterPeer){report.status='passed-original-peer-feedback';break;}await clickButton(completion);await wait(300);continue;}
  const workbookNext=s.buttons.find(b=>b.name==='NextButton');
  if(workbookNext){
   assert(workbookPages<13,'The workbook repeated a page or failed to submit');
   if(mobileWorksheet){const result=await require('./original-mobile-worksheet.cjs').completePage({page,state,capture,report,pageNumber:workbookPages+1});report.workbookInteractions=report.workbookInteractions||[];report.workbookInteractions.push({page:workbookPages+1,filledSlots:result.filledSlots,totalSlots:result.totalSlots});workbookSubmitted=result.submitted;workbookPages++;continue;}
   await capture('workbook-'+String(workbookPages+1).padStart(2,'0'));
   // Functional drag/drop coverage, not a claim that the user's answers were graded correct.
   const available=[...(s.wordTiles||[])];
   for(const slot of (s.wordSlots||[]).filter(slot=>!slot.value)){
    const tile=available.shift();if(!tile)break;const from=tile.rect,to=slot.rect;
    assert(from.x>=0&&from.x+from.w<=1.01&&to.x>=0&&to.x+to.w<=1.01,'Original workbook widget outside the viewport');
    const view=page.viewportSize();await page.mouse.move((from.x+from.w/2)*view.width,(from.y+from.h/2)*view.height);await page.mouse.down();await wait(120);
    await page.mouse.move((to.x+to.w/2)*view.width,(to.y+to.h/2)*view.height,{steps:18});await wait(130);await page.mouse.up();await wait(650);
   }
   // The original Tutorial TextField1 accepts alphanumerics and at most16 characters.
   for(const field of s.fields||[]){await clickButton(field);await page.keyboard.type('RedDeliversO2',{delay:8});}
   const checked=await state();report.workbookInteractions=report.workbookInteractions||[];report.workbookInteractions.push({page:workbookPages+1,filledSlots:checked.wordSlots?.filter(slot=>slot.value).length||0,totalSlots:checked.wordSlots?.length||0});
   const next=checked.buttons.find(b=>b.name==='NextButton');assert(next,'The original workbook lost its Next/Submit control');
   workbookSubmitted=/submit/i.test(next.text);await clickButton(next);workbookPages++;await wait(550);continue;
  }
  if(workbookSubmitted&&s.timelines.some(t=>t.name==='Storyline_MeetingCellsTransition2Heart'&&t.starts===1)){report.status='passed-original-meetingcells-to-external-handoff';await wait(2500);await verifyEnding();break;}
  // Authored questionnaire animation may remain playing while its native UI expects input.
  // Modal controls are therefore handled before waiting for a camera-only sequence.
  const play=s.buttons.find(b=>/PlayGameRBC|PlayGameWBC/.test(b.name));if(play){await auditUiProfiles(play.name+'-launcher');await clickButton((await state()).buttons.find(b=>b.name===play.name));await wait(800);continue;}
  const answer=s.buttons.find(b=>/^(CompleteBButton|Button B)$/.test(b.name)||/^B(?:[.\s:)]|$)/i.test(b.text)||/no nucleus|without a nucleus/i.test(b.text));
  if(answer&&!questionAnswered){await auditUiProfiles('quiz');await capture('quiz-before-answer');await clickButton((await state()).buttons.find(b=>b.name===answer.name));await wait(500);const after=await state();questionAnswered=after.speaking||after.completed?.includes('AfterQuestionCanvas')||!after.buttons.some(b=>b.name==='Button B');if(!questionAnswered){console.log('QUESTION_CLICK_NOT_RECEIVED',after.pointerTargets);await key('Space');}continue;}
  if(s.timelines.some(t=>t.active&&t.playing)){await wait(200);continue;}
  if(s.completed?.includes('AfterRBCMinigame')&&questionAnswered&&process.env.SPARC_STOP_AFTER_RBC==='1'){report.status='passed-original-rbc-route';break;}
  // Source WBC_After ends Say728318886 with Wait728318360 (3 seconds), then
  // activates the original worksheet. Its nearby NPC can still offer Talk during
  // that pause; wait for the authored modal instead of replaying WBC_New with E.
  if(whiteSeen&&workbookPages===0&&(s.lineId==='728318886'||s.trace?.includes('WBC_After/728318360/Wait'))){
   report.whiteWorksheetTransitionWait=report.whiteWorksheetTransitionWait||{finalSay:'728318886',waitCommand:'728318360',sourceSeconds:3,observations:0};
   report.whiteWorksheetTransitionWait.observations++;await wait(200);continue;
  }
  const targets=(s.reachObjectives||[]).flatMap(o=>(o.targets||[]).map(t=>({...t,label:o.description}))).sort((a,b)=>Math.hypot(a.x-s.player.x,a.z-s.player.z)-Math.hypot(b.x-s.player.x,b.z-s.player.z));
  if(targets.length){if(targets[0].label!==lastObjective){lastObjective=targets[0].label;console.log('FOLLOW_ORIGINAL_OBJECTIVE',lastObjective,targets[0]);}await moveTowards(s,targets[0]);continue;}
  if(s.talkObjective){
   const target=s.talkObjective,point=target.position;
   assert(point&&Number.isFinite(point.x)&&Number.isFinite(point.z),'Original talk objective lost its observed world position');
   const phase=target.name+'/'+s.completedObjectives;
   if(!talkApproaches.has(phase))talkApproaches.set(phase,{name:target.name,initialDistance:Math.hypot(point.x-s.player.x,point.z-s.player.z),initialTravel:s.player.distance});
   if(s.nearestTalk!==target.name){await moveTowards(s,point);continue;}
   const approach=talkApproaches.get(phase);
   report.talkApproaches=report.talkApproaches||[];
   report.talkApproaches.push({name:target.name,initialDistance:approach.initialDistance,arrivedDistance:Math.hypot(point.x-s.player.x,point.z-s.player.z),walkedMeters:s.player.distance-approach.initialTravel,nearestTalk:s.nearestTalk,player:{x:s.player.x,y:s.player.y,z:s.player.z},target:{...point}});
   console.log('TALK_AFTER_PHYSICAL_APPROACH',JSON.stringify(report.talkApproaches[report.talkApproaches.length-1]));
   if(!await interactionBeforeTalk(s))await key('KeyE',160);await wait(400);continue;
  }
  if(s.nearestTalk){await key('KeyE',160);await wait(400);}else await wait(250);
  if(workbookSubmitted&&s.timelines.some(t=>t.name==='Storyline_MeetingCellsTransition2Heart'&&t.starts>0)){report.status='passed-original-meetingcells-to-external-handoff';await verifyEnding();break;}
 }
 const s=await capture('final-state');report.oxygenSeen=oxygenSeen;report.whiteSeen=whiteSeen;report.questionAnswered=questionAnswered;report.workbookPages=workbookPages;report.workbookSubmitted=workbookSubmitted;report.boundaries=s.boundaries;
 const intro=s.timelines.find(t=>t.name==='StorylineIntro');assert(intro&&intro.starts===1,'Original intro must not replay');
 assert(s.waypoints===4,'CAP must traverse all four original destinations');
 report.checks.push('single-original-intro','four-original-cap-waypoints','no-scene-or-score-mutation');
 if(requireWhiteWin)assert.equal(report.whiteContinuation,'WBC_WIN','Required original White win continuation was not observed');
 if(whiteStarted)report.whiteActualWallSeconds=(Date.now()-whiteStarted)/1000;
 if(oxygenSeen&&report.oxygenActualWallSeconds>=115)report.checks.push('full-real-time-original-oxygen-activity');
 if(report.status==='running'){report.status='incomplete';throw Error('Journey deadline reached before required original-route completion');}
 if(report.status==='passed-original-meetingcells-to-external-handoff'){
  const c=report.crowd;assert(c&&c.spawned>5&&c.expired>0,'Original blocking cells did not continuously spawn and replenish');
  assert(c.maximumActive<=5&&c.maximumActive>0,'Original enabled spawner must retain its five-cell cap');
  assert(c.maximumSpawnedMovement>.5&&c.navReadySamples>0,'Spawned original cells did not actually navigate');
  assert(c.spawners.some(s=>s.type==='NPC_spawn'&&s.enabled&&s.cap===5)&&c.spawners.some(s=>s.type==='RBC_SPAWN_MeetingCells'&&!s.enabled),'Source spawner enabled states changed');
  report.checks.push('original-rbc-blockers-continuously-spawn-move-expire-and-replenish','original-five-cell-cap-and-disabled-alternative-retained');
  if(c.contacts>0)report.checks.push('original-world-collision-observed-from-real-player-input');
 }
 assert(s.boundaries.length===0,'Unimplemented boundary surfaced: '+s.boundaries.join('; '));
 if(report.status==='passed-original-meetingcells-to-external-handoff'&&mobileWorksheet&&process.env.SPARC_VERIFY_ENDING==='1')await verifyManualReplay(s);
}
main().catch(async e=>{if(report.status!=='incomplete')report.status='failed';report.failure=String(e.stack||e);process.exitCode=1;if(page){await page.screenshot({path:path.join(out,'failure.png'),timeout:8000}).catch(()=>{});const failedState=await state().catch(()=>report.lastState);if(report.manualReplay){report.manualReplay.status='failed';report.manualReplay.failureState=failedState;}else report.lastState=failedState;}}).finally(async()=>{
 report.finishedUtc=new Date().toISOString();save();if(context)await context.close().catch(()=>{});
 if(video)await video.saveAs(path.join(out,'original-journey.webm')).then(()=>{report.video={path:'original-journey.webm',sha256:hash(path.join(out,'original-journey.webm'))};}).catch(e=>{report.videoError=String(e);});
 if(browser)await browser.close().catch(()=>{});if(server)await new Promise(resolve=>server.close(resolve));save();console.log(JSON.stringify({status:report.status,checks:report.checks,errors:report.errors,boundaries:report.boundaries,failure:report.failure,out},null,2));
});
