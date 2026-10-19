'use strict';
// Read-only observations plus genuine touches on the authored OnScreenStick.
const assert=require('node:assert/strict');const sessions=new WeakMap();
const pause=(page,ms)=>page.waitForTimeout(ms);
function axis(game){return Math.hypot(game?.input?.x||0,game?.input?.y||0);}
function gameFrom(s,white){return (s.games||[]).find(g=>g.white===white);}
async function observeRelease(page,state,white){const end=Date.now()+2500;while(Date.now()<end){const g=gameFrom(await state(),white);if(g&&axis(g)<.01)return g;await pause(page,120);}return null;}
async function verify({page,state,capture,report,game}){
 report.mobileMinigameTouch=report.mobileMinigameTouch||{scope:'Actual native minigame joystick touches during a hybrid keyboard/mouse original route; original timers and physics continue unchanged',physicalDeviceAcceptance:false};
 const name=game.white?'white':'oxygen';if(report.mobileMinigameTouch[name]?.passed)return;
 let cdp=sessions.get(page);if(!cdp){cdp=await page.context().newCDPSession(page);sessions.set(page,cdp);}
 const v=page.viewportSize(),r=game.joystick?.rect||game.joystick;assert(r&&r.w>0&&r.h>0,'Native '+name+' joystick bounds are absent');
 assert(r.w*v.width>=43.5&&r.h*v.height>=43.5,'Native '+name+' joystick is too small for touch');assert(r.x>=0&&r.y>=0&&r.x+r.w<=1.001&&r.y+r.h<=1.001,'Native joystick lies outside viewport');
 const center={x:(r.x+r.w/2)*v.width,y:(r.y+r.h/2)*v.height},contact=p=>({id:81,x:p.x,y:p.y,radiusX:8,radiusY:8,force:1});
 const evidence={joystick:r,viewport:v,attempts:[],events:[],passed:false};report.mobileMinigameTouch[name]=evidence;
 async function touch(type,points){evidence.events.push({type,points:points.map(p=>({id:p.id,x:p.x,y:p.y})),wallMilliseconds:Date.now()});await cdp.send('Input.dispatchTouchEvent',{type,touchPoints:points});}
 const directions=game.white?[[1,0],[0,1],[-1,0],[0,-1]]:[[1,0],[-1,0]];let good=null;
 for(const [dx,dy] of directions){
  const before=gameFrom(await state(),game.white);assert(before&&!before.Finished,'Native game ended before touch control check');const target={x:center.x+dx*r.w*v.width*.32,y:center.y+dy*r.h*v.height*.32};
  await touch('touchStart',[contact(center)]);await pause(page,100);await touch('touchMove',[contact(target)]);
  let strength=0,after=before;for(let i=0;i<6;i++){await pause(page,100);after=gameFrom(await state(),game.white);assert(after,'Native activity disappeared during joystick touch');strength=Math.max(strength,axis(after));}
  const pixels=Math.hypot((after.miniPlayer.x-before.miniPlayer.x)*v.width,(after.miniPlayer.y-before.miniPlayer.y)*v.height);
  evidence.attempts.push({direction:{x:dx,y:dy},axisMagnitude:strength,movedCssPixels:pixels,before:before.miniPlayer,after:after.miniPlayer,elapsedBefore:before.Elapsed,elapsedAfter:after.Elapsed});
  if(strength>.15&&pixels>4){await capture('mobile-'+name+'-native-stick');good={target,strength,pixels};}
  await touch('touchEnd',[contact(target)]);assert(await observeRelease(page,state,game.white),'Native '+name+' joystick did not release its virtual axis');
  if(good)break;
 }
 assert(good,'No real native '+name+' joystick direction produced a virtual axis and actual player motion: '+JSON.stringify(evidence.attempts));
 await touch('touchStart',[contact(center)]);await pause(page,100);await touch('touchMove',[contact(good.target)]);await pause(page,250);assert(axis(gameFrom(await state(),game.white))>.15,'Native joystick did not rearm before cancellation check');
 await touch('touchCancel',[]);assert(await observeRelease(page,state,game.white),'Native '+name+' touchCancel left the virtual joystick stuck');
 evidence.passed=true;evidence.checks=['actual-authored-stick-touch-drives-virtual-axis-and-original-body','touch-end-returns-native-axis-to-zero','touchcancel-returns-native-axis-to-zero','unchanged-original-clock-continued-during-input-check'];
 report.checks.push('mobile-'+name+'-native-OnScreenStick-real-touch-and-cancel');
}
module.exports={verify};
