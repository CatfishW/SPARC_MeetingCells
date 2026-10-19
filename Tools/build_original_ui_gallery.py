"""Collect exact current UI renders for visual review; previews are not gameplay wins."""
from pathlib import Path
from datetime import datetime, timezone
import argparse, base64, hashlib, html, json
from PIL import Image, ImageDraw, ImageOps

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'Reports/OriginalScene/UiVisualAudit'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def family(name):
    if name.startswith('worksheet-') or 'workbook-' in name:return 'Worksheet — all original pages'
    if 'oxygen' in name or 'white-' in name:return 'Minigames — generated pixel sketch art'
    if any(s in name for s in ('PlayerInput','LAMInput','AnswerField','WaitForResponse','question-','peer-','quiz')):return 'Quiz and peer forms'
    if 'settings' in name or 'hud-' in name or 'objective' in name:return 'HUD and settings'
    if 'cue-' in name or 'caption-' in name or 'Follow_CAP' in name:return 'Story captions and teaching cues'
    if 'ending' in name or 'replay' in name:return 'Completion and replay'
    if 'interaction-' in name:return 'Character highlights and native interaction menus'
    if 'launcher' in name:return 'Activity launchers'
    return 'Start, dialogue, camera and controls'
def main():
    ap=argparse.ArgumentParser(description=__doc__)
    browser_keys=('journey','controls','mobile','release_mobile','release_chromium','release_firefox')
    for key in browser_keys:ap.add_argument('--'+key.replace('_','-'))
    args=ap.parse_args();tests=json.loads((ROOT/'Reports/OriginalScene/verified-tests.json').read_text())
    if tests.get('status')!='passed' or tests['inputSha256']!=tests['inputSha256After']:raise RuntimeError('Current native suite must pass before collecting its renders')
    since=datetime.fromisoformat(tests['startedUtc']).timestamp();items=[]
    for p in sorted((ROOT/'Reports/OriginalScene/LayoutFollowup').glob('*.png')):
        if p.stat().st_mtime>=since:items.append((p,'Native scene render; layout preview, not an input-driven game result'))
    for key in browser_keys:
        value=getattr(args,key)
        if not value:continue
        folder=(ROOT/value).resolve();raw=json.loads((folder/'acceptance.json').read_text())
        recorded=raw.get('sourceInputSha256') or raw.get('runner',{}).get('sourceInputSha256')
        if key=='controls':recorded=json.loads((folder/'runner-provenance.json').read_text()).get('inputSha256',recorded)
        if key.startswith('release_'):
            build=json.loads((ROOT/'Reports/OriginalScene/verified-webgl-release.json').read_text())
            expected={name:r['sha256'] for name,r in build['outputs'].items()}
            if build['status']!='passed' or build['inputSha256']!=tests['inputSha256'] or raw.get('buildHashes')!=expected:raise RuntimeError('Stale release browser artifacts: '+key)
            recorded=build['inputSha256']
        if not recorded:raise RuntimeError('Missing browser source provenance: '+key)
        if recorded and recorded!=tests['inputSha256']:raise RuntimeError('Stale browser run: '+key)
        accepted_images=raw.get('screenshots') or []
        if key=='journey':accepted_images=[{'name':r['name']+'.png','sha256':r['sha256']} for r in raw['snapshots']]
        if key=='release_mobile':accepted_images=[r for case in raw['cases'] for r in case['screenshots']]
        if not accepted_images:raise RuntimeError('No accepted browser images: '+key)
        for row in sorted(accepted_images,key=lambda r:r['name']):
            p=folder/row['name']
            if p.is_symlink() or folder not in p.resolve().parents or sha(p)!=row['sha256']:raise RuntimeError('Changed browser image: '+str(p))
            items.append((p,'Actual '+key.replace('_',' ')+' browser capture'))
    # Lead with the actual corrected screens; native alpha/timing fixtures remain below.
    featured=['06a-objective-expanded.png','mobile-workbook-04.png','visual-oxygen-play-960x940.png','visual-peer-input-960x940.png','visual-PlayGameRBC-launcher-960x940.png','visual-white-play-800x1100.png','390x844-dialogue.png']
    items.sort(key=lambda pair:(0,featured.index(pair[0].name)) if pair[0].name in featured else (0,1.5) if pair[0].name.startswith('interaction-') else (1 if pair[1].startswith('Actual ') else 2,str(pair[0])))
    OUT.mkdir(parents=True,exist_ok=True);rows=[]
    for p,scope in items:
        with Image.open(p) as im:w,h=im.size
        rows.append({'path':str(p.relative_to(ROOT)),'name':p.name,'family':family(p.name),'width':w,'height':h,'sha256':sha(p),'scope':scope})
    report={'status':'awaiting-visual-review','createdUtc':datetime.now(timezone.utc).isoformat(),'sourceInputSha256':tests['inputSha256'],'nativeTestXmlSha256':tests['tests']['sha256'],'nativeTests':tests['tests']['passed'],'images':rows,'limitations':['Native result/loading previews exercise retained UI presentation; they do not establish an input-driven victory.','Browser captures are separate from native fixtures. Physical phones, Safari and external multiplayer/PING services are outside this local audit.','Legacy LAM forms are retained inactive previews; the standalone feedback flow uses its original unified peer contract.']}
    (OUT/'gallery-manifest.json').write_text(json.dumps(report,indent=2)+'\n')
    figures=[]
    for i,(p,scope) in enumerate(items):
        r=rows[i];src='data:image/png;base64,'+base64.b64encode(p.read_bytes()).decode()
        figures.append('<figure data-family="'+html.escape(r['family'],quote=True)+'"><button class="zoom" aria-label="Enlarge '+html.escape(p.name,quote=True)+'"><img loading="lazy" src="'+src+'" alt="'+html.escape(p.name,quote=True)+'"></button><figcaption><strong>'+html.escape(p.name)+'</strong><br>'+html.escape(scope)+'</figcaption></figure>')
    options=''.join('<option>'+html.escape(s)+'</option>' for s in sorted({r['family'] for r in rows}))
    doc='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>Meeting Cells — UI visual audit</title><style>*{box-sizing:border-box}body{margin:0;background:#f3f0e6;color:#243d40;font:16px/1.5 system-ui}main{max-width:1440px;margin:auto;padding:28px}h1{font-size:32px;line-height:1.15}p{max-width:900px}select,button{font:inherit}select{padding:12px;background:#fffdf4;border:1px solid #78938b;max-width:100%}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:20px;margin-top:24px}figure{margin:0;padding:12px;background:#fffdf4;border:1px solid #acbbb1}figure[hidden]{display:none}.zoom{border:0;padding:0;background:transparent;width:100%;cursor:zoom-in}.zoom img{width:100%;height:260px;object-fit:contain}figcaption{font-size:13px;overflow-wrap:anywhere;padding-top:10px}dialog{padding:16px;max-width:98vw;max-height:98vh;border:1px solid #78938b;background:#fffdf4}dialog::backdrop{background:#000b}dialog img{display:block;max-width:92vw;max-height:84vh;object-fit:contain}dialog button{padding:10px 20px;cursor:pointer}code{overflow-wrap:anywhere;font-size:12px}@media(max-width:500px){main{padding:16px}.grid{grid-template-columns:1fr}}</style><main><h1>Meeting Cells UI visual audit</h1><p>Current native UI renders and actual browser captures. Approved generated pixel-and-sketch game art and the redesigned notebook are in use. Original story, questions and scoring remain. Select a screen family; click any image to inspect it at full size.</p><p>Result and loading previews show layout coverage. Actual gameplay acceptance is recorded separately. Inactive legacy LAM forms remain inactive in the standalone story.</p><p><strong>Source</strong> <code>'''+report['sourceInputSha256']+'''</code> · '''+str(len(rows))+''' captures</p><label>Screen family <select id="family"><option>All screens</option>'''+options+'''</select></label><div class="grid">'''+''.join(figures)+'''</div><dialog><button id="close">Close image</button><p id="caption"></p><img alt="Enlarged UI capture"></dialog></main><script>const d=document.querySelector('dialog');document.getElementById('family').onchange=e=>document.querySelectorAll('figure').forEach(f=>f.hidden=e.target.value!=='All screens'&&f.dataset.family!==e.target.value);document.querySelectorAll('.zoom').forEach(b=>b.onclick=()=>{d.querySelector('img').src=b.querySelector('img').src;document.getElementById('caption').textContent=b.closest('figure').querySelector('strong').textContent;d.showModal()});document.getElementById('close').onclick=()=>d.close();</script></html>'''
    (OUT/'index.html').write_text(doc)
    sheets=OUT/'ContactSheets';sheets.mkdir(exist_ok=True)
    for start in range(0,len(items),12):
        sheet=Image.new('RGB',(1440,1080),'#e8e5db');draw=ImageDraw.Draw(sheet)
        for j,(p,scope) in enumerate(items[start:start+12]):
            x=(j%4)*360;y=(j//4)*360
            with Image.open(p) as im:thumb=ImageOps.contain(im.convert('RGB'),(344,310));sheet.paste(thumb,(x+(360-thumb.width)//2,y+(310-thumb.height)//2))
            draw.text((x+8,y+314),str(start+j+1)+'. '+p.name[:42],fill='#203b3e')
        sheet.save(sheets/('sheet-'+str(start//12+1).zfill(2)+'.jpg'),quality=92)
    print(json.dumps({'status':report['status'],'images':len(rows),'gallery':str((OUT/'index.html').relative_to(ROOT)),'families':sorted({r['family'] for r in rows})},indent=2))
if __name__=='__main__':main()
