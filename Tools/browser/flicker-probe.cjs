'use strict';
// Flicker probe: rapid screenshot bursts over the opening route at a desktop/laptop viewport.
// Counts pixels inside paper-coloured UI cards that change between consecutive frames, so a
// card whose text or layout alternates shows up even while the 3D scene animates behind it.
// Usage: SPARC_BUILD=Builds/WebGLOriginal SPARC_VIEWPORT=1280x620 SPARC_DPR=2 node flicker-probe.cjs
const fs=require('node:fs'),path=require('node:path'),http=require('node:http');
const {chromium}=require('playwright');
const root=path.resolve(__dirname,'../..'),build=path.resolve(process.env.SPARC_BUILD||path.join(root,'Builds/WebGLOriginal'));
const [width,height]=(process.env.SPARC_VIEWPORT||'1280x620').split('x').map(Number),dpr=Number(process.env.SPARC_DPR||2);
const seconds=Number(process.env.SPARC_PROBE_SECONDS||75);
const out=path.resolve(process.env.SPARC_OUT||path.join(root,'Logs/flicker'),new Date().toISOString().replace(/[:.]/g,'-'));fs.mkdirSync(out,{recursive:true});
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
 const server=await serve(),origin='http://127.0.0.1:'+server.address().port+'/';
 const browser=await chromium.launch({headless:true,args:['--use-angle=metal','--enable-webgl']});
 const context=await browser.newContext({viewport:{width,height},deviceScaleFactor:dpr});const page=await context.newPage();
 const analysis=await context.newPage();await analysis.setContent('<canvas></canvas>');
 const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(origin);await page.waitForFunction(()=>document.getElementById('loading')?.style.display==='none',null,{timeout:240000});
 await page.locator('#unity-canvas').focus();
 for(let i=0;i<30&&!(await page.evaluate(()=>window.__SPARC_ORIGINAL_QA?.started));i++){await page.keyboard.press('Enter');await page.waitForTimeout(700);}
 await page.waitForTimeout(1500);
 const report={viewport:{width,height,dpr},bursts:[],errors};const end=Date.now()+seconds*1000;let index=0;
 while(Date.now()<end){
  const s=await page.evaluate(()=>window.__SPARC_ORIGINAL_QA||null);
  // Reveal the whole line first, so the typewriter is not mistaken for flicker; advance after the burst.
  if(s?.speaking){await page.keyboard.press('Space');await page.waitForTimeout(350);}
  const frames=[];for(let i=0;i<6;i++){frames.push((await page.screenshot()).toString('base64'));await page.waitForTimeout(40);}
  const diffs=await analysis.evaluate(async frames=>{
   const load=src=>new Promise(r=>{const i=new Image();i.onload=()=>r(i);i.src='data:image/png;base64,'+src;});
   const images=await Promise.all(frames.map(load)),c=document.querySelector('canvas');c.width=images[0].width;c.height=images[0].height;const g=c.getContext('2d',{willReadFrequently:true});
   const data=images.map(i=>{g.drawImage(i,0,0);return g.getImageData(0,0,c.width,c.height).data;});
   const paper=(d,o)=>d[o]>228&&d[o+1]>222&&d[o+2]>198&&d[o]-d[o+2]>4&&d[o]-d[o+2]<40;
   // Card regions: paper bounding box within the top-left HUD quadrant, and within the bottom
   // band (dialogue / talk / launcher). Every pixel inside a card box is compared.
   const W=c.width,H=c.height,box=(d,x0,y0,x1,y1)=>{let a=[1e9,1e9,-1,-1],n=0;for(let y=y0;y<y1;y+=2)for(let x=x0;x<x1;x+=2){const o=(y*W+x)*4;if(paper(d,o)){n++;a=[Math.min(a[0],x),Math.min(a[1],y),Math.max(a[2],x),Math.max(a[3],y)];}}return n>400?a:null;};
   const regions=[box(data[0],0,0,W*.4|0,H*.45|0),box(data[0],W*.12|0,H*.55|0,W*.88|0,H)].filter(Boolean);
   const result=[];
   for(let f=1;f<data.length;f++){let changed=0,minX=1e9,minY=1e9,maxX=-1,maxY=-1;const a=data[f-1],b=data[f];
    for(const r of regions)for(let y=r[1];y<=r[3];y++)for(let x=r[0];x<=r[2];x++){const o=(y*W+x)*4;if(Math.abs(a[o]-b[o])+Math.abs(a[o+1]-b[o+1])+Math.abs(a[o+2]-b[o+2])>90){changed++;minX=Math.min(minX,x);maxX=Math.max(maxX,x);minY=Math.min(minY,y);maxY=Math.max(maxY,y);}}
    result.push({changed,regions,box:changed?[minX,minY,maxX,maxY]:null});}
   return result;
  },frames);
  const worst=diffs.reduce((m,d,i)=>d.changed>m.changed?{...d,i}:m,{changed:0});
  report.bursts.push({t:Math.round((seconds*1000-(end-Date.now()))/1000),speaking:!!s?.speaking,line:s?.lineId,worst});
  if(index%5===0)fs.writeFileSync(path.join(out,`sample-${index}.png`),Buffer.from(frames[0],'base64'));
  if(worst.changed>150){fs.writeFileSync(path.join(out,`burst-${index}-a.png`),Buffer.from(frames[worst.i],'base64'));fs.writeFileSync(path.join(out,`burst-${index}-b.png`),Buffer.from(frames[worst.i+1],'base64'));}
  console.log('BURST',index,worst.changed,JSON.stringify(diffs[0].regions),JSON.stringify(worst.box),s?.speaking?'speaking':'');index++;
  if(s?.speaking){await page.keyboard.press('Space');}
  await page.waitForTimeout(700);
 }
 fs.writeFileSync(path.join(out,'flicker.json'),JSON.stringify(report,null,2));
 console.log('FLICKER_SUMMARY',JSON.stringify({bursts:report.bursts.length,over150:report.bursts.filter(b=>b.worst.changed>150).length,errors:errors.length,out}));
 await browser.close();server.close();
})().catch(e=>{console.error(e);process.exit(1);});
