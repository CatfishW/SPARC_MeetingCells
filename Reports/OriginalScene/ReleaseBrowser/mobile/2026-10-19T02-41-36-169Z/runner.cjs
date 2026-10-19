'use strict';
// Supplementary production-bundle mobile smoke. Real browser touch, no Unity probe/setters.
const fs=require('node:fs'),path=require('node:path'),http=require('node:http'),crypto=require('node:crypto'),assert=require('node:assert/strict');
const {chromium,devices}=require('playwright');
const root=path.resolve(__dirname,'../..'),build=path.join(root,'Builds/WebGLOriginalRelease');
const expected=process.env.SPARC_RELEASE_INPUT_SHA;assert(/^[a-f0-9]{64}$/.test(expected||''),'Set the exact verified SPARC_RELEASE_INPUT_SHA');
const hash=file=>crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');
const files=dir=>fs.readdirSync(dir,{withFileTypes:true}).flatMap(e=>e.isDirectory()?files(path.join(dir,e.name)):[path.join(dir,e.name)]);
const verified=JSON.parse(fs.readFileSync(path.join(root,'Reports/OriginalScene/verified-webgl-release.json'),'utf8'));
assert(verified.status==='passed'&&verified.inputSha256===expected&&verified.inputSha256After===expected,'Requested release is not verified');
const buildHashes=Object.fromEntries(files(build).map(file=>[path.relative(build,file),hash(file)]));
assert.deepEqual(buildHashes,Object.fromEntries(Object.entries(verified.outputs).map(([name,row])=>[name,row.sha256])),'Release artifacts differ from the verified bundle');
const out=path.join(root,'Reports/OriginalScene/ReleaseBrowser/mobile',new Date().toISOString().replace(/[:.]/g,'-'));fs.mkdirSync(out,{recursive:true});
fs.copyFileSync(__filename,path.join(out,'runner.cjs'));
const report={status:'running',sourceInputSha256:expected,startedUtc:new Date().toISOString(),scope:'Supplementary Android/touch/DPR2 production-release Start, authored intro and dialogue smoke at390x844 and844x390; no physical-device or full-route acceptance',physicalDeviceAcceptance:false,buildHashes,runner:{path:'runner.cjs',sha256:hash(path.join(out,'runner.cjs')),nodeVersion:process.version},cases:[],limitations:['Browser emulation and RAF timing do not certify physical-device speed, Unity frame rate, thermals, battery use, Safari or a phone soft keyboard.','This smoke does not cover the full story route, task HUD, settings, minigame controls or worksheet interactions.','Audio is not accepted by silent image evidence.']};
const save=()=>fs.writeFileSync(path.join(out,'acceptance.json'),JSON.stringify(report,null,2));
const server=http.createServer((req,res)=>{try{
 const url=new URL(req.url,'http://localhost');if(url.pathname==='/favicon.ico'){res.writeHead(204);res.end();return;}
 const file=path.resolve(build,'.'+(url.pathname==='/'?'/index.html':decodeURIComponent(url.pathname)));
 if(!file.startsWith(build+path.sep)||!fs.existsSync(file)||!fs.statSync(file).isFile()){res.writeHead(404);res.end();return;}
 const compressed=file.endsWith('.unityweb'),size=fs.statSync(file).size;let first=0,last=size-1,status=200;
 const headers={'Cache-Control':'no-store','Content-Type':/\.wasm(?:\.unityweb)?$/.test(file)?'application/wasm':/\.js(?:\.unityweb)?$/.test(file)?'application/javascript':/\.html$/.test(file)?'text/html; charset=utf-8':/\.mp4$/.test(file)?'video/mp4':'application/octet-stream',...(compressed?{'Content-Encoding':'gzip'}:{})};
 const range=/^bytes=(\d+)-(\d*)$/.exec(req.headers.range||'');if(range&&!compressed){first=Number(range[1]);last=range[2]?Math.min(last,Number(range[2])):last;if(first>last){res.writeHead(416);res.end();return;}status=206;headers['Accept-Ranges']='bytes';headers['Content-Range']=`bytes ${first}-${last}/${size}`;}
 res.writeHead(status,{...headers,'Content-Length':last-first+1});fs.createReadStream(file,{start:first,end:last}).pipe(res);
 }catch(_){res.writeHead(500);res.end();}});
let browser,currentPage;
async function main(){
 await new Promise(resolve=>server.listen(0,'127.0.0.1',resolve));const origin='http://127.0.0.1:'+server.address().port+'/';
 browser=await chromium.launch({headless:true,args:['--use-angle=metal','--enable-webgl']});report.browserVersion=browser.version();
 for(const viewport of [{width:390,height:844},{width:844,height:390}]){
  const name=viewport.width<viewport.height?'portrait':'landscape',row={name,viewport,errors:[],warnings:[],externalRequests:[],failedRequests:[],screenshots:[],checks:[]};report.cases.push(row);save();
  const context=await browser.newContext({viewport,deviceScaleFactor:2,isMobile:true,hasTouch:true,userAgent:devices['Pixel 7'].userAgent});
  try{
   await context.route(url=>/^https?:$/.test(url.protocol)&&url.origin!==new URL(origin).origin,route=>{row.externalRequests.push(route.request().url().split('?')[0]);return route.abort();});
   const page=await context.newPage();currentPage=page;const cdp=await context.newCDPSession(page);await cdp.send('Emulation.setTouchEmulationEnabled',{enabled:true,maxTouchPoints:5});
   page.on('pageerror',error=>{row.errors.push(String(error));save();});page.on('requestfailed',request=>row.failedRequests.push({url:request.url(),error:request.failure()?.errorText}));
   page.on('console',message=>{const value=message.text();if(message.type()==='error'||/NullReferenceException|MissingMethodException|RuntimeError:|OutOfMemory|Aborted\(/.test(value)){row.errors.push(value);save();}else if(message.type()==='warning')row.warnings.push(value);});
   async function capture(stage){
    const name=viewport.width+'x'+viewport.height+'-'+stage+'.png',file=path.join(out,name);await page.screenshot({path:file,timeout:10000});row.screenshots.push({name,sha256:hash(file)});save();console.log('MOBILE_RELEASE_CAPTURE',name);
   }
   const began=Date.now();await page.goto(origin,{waitUntil:'domcontentloaded'});await page.waitForFunction(()=>document.getElementById('loading').style.display==='none',null,{timeout:180000});row.startupSeconds=(Date.now()-began)/1000;
   row.environment=await page.evaluate(()=>{const canvas=document.getElementById('unity-canvas'),rect=canvas.getBoundingClientRect();return {userAgent:navigator.userAgent,maxTouchPoints:navigator.maxTouchPoints,coarsePointer:matchMedia('(pointer: coarse)').matches,devicePixelRatio:window.devicePixelRatio,qaTelemetry:typeof window.__SPARC_ORIGINAL_QA,renderToCssScale:window.SPARCBrowser.pixelScale(),canvas:{width:canvas.width,height:canvas.height,cssWidth:rect.width,cssHeight:rect.height},visibility:document.visibilityState};});
   assert(/Android/.test(row.environment.userAgent)&&row.environment.maxTouchPoints>=2&&row.environment.coarsePointer,'Android multi-touch environment is absent');assert.equal(row.environment.devicePixelRatio,2);assert.equal(row.environment.qaTelemetry,'undefined');assert.equal(row.environment.canvas.cssWidth,viewport.width);assert.equal(row.environment.canvas.cssHeight,viewport.height);assert(row.environment.renderToCssScale>1,'Scaled Unity backbuffer was not exercised');
   row.checks.push('verified-release-loader-resolved','Android-multitouch-DPR2-environment','release-QA-telemetry-absent','canvas-CSS-and-scaled-backbuffer-recorded');
   // Loader resolution precedes Unity's own splash. Let that authored startup
   // finish before photographing and touching the visible Start card.
   await page.bringToFront();await page.locator('#unity-canvas').focus();await page.waitForTimeout(4000);await capture('start');await page.touchscreen.tap(viewport.width/2,viewport.height/2+36);await page.waitForTimeout(10000);await capture('intro');await page.waitForTimeout(18000);await capture('dialogue');
   assert.notEqual(row.screenshots[0].sha256,row.screenshots[1].sha256,'Start/intro frames did not change after the actual touch');row.checks.push('real-touch-Start-and-post-intro-frames-captured');
   row.browserRaf=await page.evaluate(()=>new Promise((resolve,reject)=>{const samples=[];let previous=performance.now();const timeout=setTimeout(()=>reject(Error('Browser RAF sampling timed out')),10000);function frame(now){samples.push(now-previous);previous=now;if(samples.length<60)requestAnimationFrame(frame);else{clearTimeout(timeout);const sorted=[...samples].sort((a,b)=>a-b);resolve({samplesMilliseconds:samples,medianMilliseconds:sorted[30],p95Milliseconds:sorted[57],scope:'Browser RAF timing only, not Unity FPS or physical-device performance'});}}requestAnimationFrame(frame);}));
   row.nonfatalLoaderCancellations=row.failedRequests.filter(request=>request.error==='net::ERR_ABORTED'&&/^\/Build\/WebGLOriginalRelease\.(framework\.js|wasm)\.unityweb$/.test(new URL(request.url).pathname));
   row.unexpectedRequestFailures=row.failedRequests.filter(request=>!row.nonfatalLoaderCancellations.includes(request));assert.equal(row.errors.length,0,row.errors.join('\n'));assert.equal(row.externalRequests.length,0);assert.equal(row.unexpectedRequestFailures.length,0,'Unexpected release request failure');row.status='smoke-passed-awaiting-visual-review';
  }finally{await context.close();currentPage=null;save();}
 }
 report.status='smoke-passed-awaiting-visual-review';
}
main().catch(async error=>{report.status='failed';report.failure=String(error.stack||error);if(currentPage)await currentPage.screenshot({path:path.join(out,'failure.png'),timeout:5000}).catch(()=>{});process.exitCode=1;}).finally(async()=>{report.finishedUtc=new Date().toISOString();save();if(browser)await browser.close().catch(()=>{});await new Promise(resolve=>server.close(resolve));console.log(JSON.stringify({status:report.status,sourceInputSha256:expected,out,failure:report.failure,cases:report.cases.map(row=>({name:row.name,status:row.status,errors:row.errors,checks:row.checks}))},null,2));});
