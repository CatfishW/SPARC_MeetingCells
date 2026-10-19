'use strict';
// Desktop/laptop look check with real browser input: left-drag (trackpad style) and arrow keys
// must turn the explorer, and a vertical drag must orbit the third-person camera (pitch).
// Usage: SPARC_BUILD=Builds/WebGLOriginal SPARC_VIEWPORT=1280x620 SPARC_DPR=2 node desktop-look-check.cjs
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),assert=require('node:assert/strict');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'../..'),build=path.resolve(process.env.SPARC_BUILD||path.join(root,'Builds/WebGLOriginal'));
const [width,height]=(process.env.SPARC_VIEWPORT||'1280x620').split('x').map(Number),dpr=Number(process.env.SPARC_DPR||2);
const out=path.resolve(process.env.SPARC_OUT||path.join(root,'Logs/look-check'),new Date().toISOString().replace(/[:.]/g,'-'));fs.mkdirSync(out,{recursive:true});
const report={viewport:{width,height,dpr},checks:[],errors:[]};
function serve(){
 const server=http.createServer((req,res)=>{
  const url=new URL(req.url,'http://localhost');const p=path.resolve(build,'.'+(url.pathname==='/'?'/index.html':decodeURIComponent(url.pathname)));
  if(!p.startsWith(build+path.sep)||!fs.existsSync(p)||!fs.statSync(p).isFile()){res.writeHead(404);res.end();return;}
  const compressed=p.endsWith('.unityweb'),mime=/\.wasm(?:\.unityweb)?$/.test(p)?'application/wasm':/\.js(?:\.unityweb)?$/.test(p)?'application/javascript':/\.html$/.test(p)?'text/html; charset=utf-8':/\.mp4$/.test(p)?'video/mp4':'application/octet-stream';
  res.writeHead(200,{'Content-Type':mime,'Cache-Control':'no-store',...(compressed?{'Content-Encoding':'gzip'}:{})});fs.createReadStream(p).pipe(res);
 });
 return new Promise(resolve=>server.listen(0,'127.0.0.1',()=>resolve(server)));
}
(async()=>{
 const server=await serve(),browser=await chromium.launch({headless:true,args:['--use-angle=metal','--enable-webgl']});
 const page=await (await browser.newContext({viewport:{width,height},deviceScaleFactor:dpr})).newPage();page.on('pageerror',e=>report.errors.push(String(e)));
 const state=()=>page.evaluate(()=>window.__SPARC_ORIGINAL_QA||null),wait=ms=>page.waitForTimeout(ms);
 const shot=name=>page.screenshot({path:path.join(out,name+'.png')});
 const yawDelta=(a,b)=>((b-a+540)%360)-180;
 try{
  await page.goto('http://127.0.0.1:'+server.address().port+'/');await page.waitForFunction(()=>!!window.__SPARC_ORIGINAL_QA,null,{timeout:240000});
  await page.locator('#unity-canvas').focus();
  for(let i=0;i<30&&!(await state())?.started;i++){await page.keyboard.press('Enter');await wait(700);}
  let s;const end=Date.now()+180000;
  while(Date.now()<end){s=await state();if(s.waypoints===4&&!s.speaking&&!s.timelines.some(t=>t.active&&t.playing)&&s.player.canControl)break;if(s.speaking)await page.keyboard.press('Space');await wait(250);}
  assert(s.player.canControl,'Controls were not released after the opening route');
  if(!s.player.thirdPerson){await page.keyboard.press('KeyV');await wait(600);}s=await state();assert(s.player.thirdPerson,'V did not switch to third person');await shot('01-third-person');
  const cam=st=>st.cameras.find(c=>/Main Camera/i.test(c.name))||st.cameras[0];
  // Left-drag sideways on the world: trackpad users cannot hold a right button while dragging.
  let yaw=s.player.yaw;const x=width*.55,y=height*.42;await page.mouse.move(x,y);await page.mouse.down();await wait(120);
  for(let i=1;i<=24;i++){await page.mouse.move(x+i*14,y);await wait(30);}await page.mouse.up();await wait(400);s=await state();
  report.leftDragYaw=yawDelta(yaw,s.player.yaw);assert(Math.abs(report.leftDragYaw)>5,'Left-drag did not turn the explorer');report.checks.push('left-drag-yaw');await shot('02-left-drag-yaw');
  // Vertical drag in third person must orbit the camera, i.e. move it vertically around the explorer.
  let before=cam(s).position;await page.mouse.move(x,height*.3);await page.mouse.down();await wait(120);
  for(let i=1;i<=20;i++){await page.mouse.move(x,height*.3+i*10);await wait(30);}await page.mouse.up();await wait(700);s=await state();
  report.thirdPersonPitchCameraRise=cam(s).position.y-before.y;assert(Math.abs(report.thirdPersonPitchCameraRise)>.15,'Third-person vertical look did not move the camera');report.checks.push('third-person-pitch');await shot('03-third-person-pitch');
  // Arrow keys turn without a pointer at all.
  yaw=s.player.yaw;await page.keyboard.down('ArrowRight');await wait(700);await page.keyboard.up('ArrowRight');await wait(300);s=await state();
  report.arrowYaw=yawDelta(yaw,s.player.yaw);assert(Math.abs(report.arrowYaw)>20,'Arrow key did not turn the explorer');report.checks.push('arrow-key-yaw');await shot('04-arrow-yaw');
  // A drag that starts on a HUD button must not turn the view.
  const tool=s.buttons.find(b=>/TOPRightBtns/.test(b.path));if(tool){yaw=s.player.yaw;const bx=(tool.rect.x+tool.rect.w/2)*width,by=(tool.rect.y+tool.rect.h/2)*height;await page.mouse.move(bx,by);await page.mouse.down();for(let i=1;i<=12;i++){await page.mouse.move(bx-i*20,by+i*8);await wait(25);}await page.mouse.up();await wait(400);s=await state();
   report.hudDragYaw=yawDelta(yaw,s.player.yaw);assert(Math.abs(report.hudDragYaw)<1,'Dragging from a HUD button turned the view');report.checks.push('hud-drag-ignored');}
  await page.keyboard.press('KeyV');await wait(600);s=await state();assert(!s.player.thirdPerson);yaw=s.player.yaw;
  await page.mouse.move(x,y);await page.mouse.down();for(let i=1;i<=20;i++){await page.mouse.move(x-i*14,y);await wait(30);}await page.mouse.up();await wait(400);s=await state();
  report.firstPersonDragYaw=yawDelta(yaw,s.player.yaw);assert(Math.abs(report.firstPersonDragYaw)>5,'First-person drag did not turn');report.checks.push('first-person-drag-yaw');await shot('05-first-person');
  assert.equal(report.errors.length,0,report.errors.join('\n'));report.status='passed';
 }catch(e){report.status='failed';report.failure=String(e.stack||e);await shot('failure').catch(()=>{});process.exitCode=1;}
 finally{fs.writeFileSync(path.join(out,'look-check.json'),JSON.stringify(report,null,2));console.log('LOOK_CHECK',JSON.stringify(report));await browser.close();server.close();}
})();
