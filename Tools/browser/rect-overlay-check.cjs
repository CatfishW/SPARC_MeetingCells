'use strict';
// Draws the read-only probe's native button rects over a real screenshot, so a mismatch between
// Unity's layout (what input hits) and the rendered pixels (what players see) is visible.
// Usage: SPARC_BUILD=Builds/WebGLOriginal SPARC_VIEWPORT=1280x620 SPARC_DPR=2 node rect-overlay-check.cjs
const fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'../..'),build=path.resolve(process.env.SPARC_BUILD||path.join(root,'Builds/WebGLOriginal'));
const [width,height]=(process.env.SPARC_VIEWPORT||'1280x620').split('x').map(Number),dpr=Number(process.env.SPARC_DPR||2);
const out=path.resolve(process.env.SPARC_OUT||path.join(root,'Logs/rect-overlay'));fs.mkdirSync(out,{recursive:true});
const server=http.createServer((req,res)=>{const url=new URL(req.url,'http://localhost');const p=path.resolve(build,'.'+(url.pathname==='/'?'/index.html':decodeURIComponent(url.pathname)));
 if(!p.startsWith(build+path.sep)||!fs.existsSync(p)||!fs.statSync(p).isFile()){res.writeHead(404);res.end();return;}
 const compressed=p.endsWith('.unityweb'),mime=/\.wasm(?:\.unityweb)?$/.test(p)?'application/wasm':/\.js(?:\.unityweb)?$/.test(p)?'application/javascript':/\.html$/.test(p)?'text/html':'application/octet-stream';
 res.writeHead(200,{'Content-Type':mime,...(compressed?{'Content-Encoding':'gzip'}:{})});fs.createReadStream(p).pipe(res);});
(async()=>{
 await new Promise(r=>server.listen(0,'127.0.0.1',r));
 const browser=await chromium.launch({headless:true,args:['--use-angle=metal','--enable-webgl']});const context=await browser.newContext({viewport:{width,height},deviceScaleFactor:dpr});const page=await context.newPage();
 await page.goto('http://127.0.0.1:'+server.address().port+'/');await page.waitForFunction(()=>!!window.__SPARC_ORIGINAL_QA,null,{timeout:240000});await page.locator('#unity-canvas').focus();
 for(let i=0;i<30&&!(await page.evaluate(()=>window.__SPARC_ORIGINAL_QA?.started));i++){await page.keyboard.press('Enter');await page.waitForTimeout(700);}
 const label=process.env.SPARC_LABEL||`${width}x${height}@${dpr}`;const samples=[];
 for(const at of [6000,30000]){
  await page.waitForTimeout(at-(samples.length?6000:0));
  const s=await page.evaluate(()=>window.__SPARC_ORIGINAL_QA);const canvas=await page.evaluate(()=>{const c=document.getElementById('unity-canvas');return{width:c.width,height:c.height,clientWidth:c.clientWidth,clientHeight:c.clientHeight,dpr:devicePixelRatio};});
  const shot=(await page.screenshot()).toString('base64');const rects=s.buttons.filter(b=>b.rect&&b.rect.w>0).map(b=>({name:b.name,rect:b.rect}));
  const png=await page.evaluate(async([shot,rects])=>{const i=new Image();await new Promise(r=>{i.onload=r;i.src='data:image/png;base64,'+shot;});const c=document.createElement('canvas');c.width=i.width;c.height=i.height;const g=c.getContext('2d');g.drawImage(i,0,0);g.lineWidth=4;g.strokeStyle='#00ff66';g.font='24px sans-serif';g.fillStyle='#00ff66';
   for(const b of rects){g.strokeRect(b.rect.x*c.width,b.rect.y*c.height,b.rect.w*c.width,b.rect.h*c.height);g.fillText(b.name,b.rect.x*c.width,b.rect.y*c.height-4);}return c.toDataURL('image/png').split(',')[1];},[shot,rects]);
  const file=path.join(out,`${label}-${at}.png`);fs.writeFileSync(file,Buffer.from(png,'base64'));samples.push({at,canvas,file,buttons:rects.length});
 }
 console.log('RECT_OVERLAY',JSON.stringify(samples));await browser.close();server.close();
})().catch(e=>{console.error(e);process.exit(1);});
