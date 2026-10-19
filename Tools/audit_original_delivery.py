"""Join CURRENT original-scene evidence. Never accepts the rejected expedition audit or stale browser hashes."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib, json, math, re, sys
import argparse
import xml.etree.ElementTree as ET
from build_webgl_verified import input_manifest, fingerprint

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Reports/OriginalScene'

def now(): return datetime.now(timezone.utc).isoformat()
def load(path): return json.loads(path.read_text())
def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda: stream.read(1024*1024), b''): h.update(chunk)
    return h.hexdigest()
def safe(root, name):
    path=root/name
    if path.is_symlink() or not path.is_file() or root.resolve() not in path.resolve().parents: return None
    return path

TOUCH_CANCEL_INTERVENTION = 'Ignored attempt to cancel a touchcancel event with cancelable=false, for example because scrolling is in progress and cannot be interrupted.'
HYBRID_RUNNERS = ('original-journey.cjs','original-white-navigation.cjs','original-mobile-minigame.cjs','original-mobile-worksheet.cjs')
WORKSHEET_CHECKS = {'13-original-pages-and14-native-slots-through-real-touch','native-word-drag-and-tap-selection-both-verified','phone-portrait-and-landscape44-CSS-word-targets','original-textfields-and-submit-callback-retained','real-word-touchcancel-clears-selection-restores-bank-and-hit-testing'}
STICK_CHECKS = {'actual-authored-stick-touch-drives-virtual-axis-and-original-body','touch-end-returns-native-axis-to-zero','touchcancel-returns-native-axis-to-zero','unchanged-original-clock-continued-during-input-check'}
RELEASE_MOBILE_VIEWPORTS = {'portrait':{'width':390,'height':844},'landscape':{'width':844,'height':390}}
RELEASE_MOBILE_CHECKS = {'verified-release-loader-resolved','Android-multitouch-DPR2-environment','release-QA-telemetry-absent','canvas-CSS-and-scaled-backbuffer-recorded','real-touch-Start-and-post-intro-frames-captured'}
ORIGINAL_HEART_ENDING_SECONDS = 20.3166666667

def number(value): return isinstance(value,(int,float)) and not isinstance(value,bool) and math.isfinite(value)
def target_rect(rect, viewport):
    if not isinstance(rect,dict) or not isinstance(viewport,dict): return False
    if not all(number(rect.get(k)) for k in ('x','y','w','h')) or not all(number(viewport.get(k)) and viewport[k]>0 for k in ('width','height')): return False
    return rect['x']>=-.001 and rect['y']>=-.001 and rect['w']>0 and rect['h']>0 and rect['x']+rect['w']<=1.001 and rect['y']+rect['h']<=1.001 and rect['w']*viewport['width']>=43.5 and rect['h']*viewport['height']>=43.5

def css_target_rect(rect, viewport):
    if not isinstance(rect,dict) or not isinstance(viewport,dict):return False
    if not all(number(rect.get(key)) for key in ('x','y','width','height')) or not all(number(viewport.get(key)) and viewport[key]>0 for key in ('width','height')):return False
    return rect['width']>=44 and rect['height']>=44 and rect['x']>=0 and rect['y']>=0 and rect['x']+rect['width']<=viewport['width'] and rect['y']+rect['height']<=viewport['height']

def placed_word_aligned(interaction, viewport):
    placed=interaction.get('placedTileRect');slot=interaction.get('slotRect');tolerance=interaction.get('alignmentToleranceCssPixels')
    if tolerance!=1.5 or not target_rect(placed,viewport) or not target_rect(slot,viewport):return False
    return max(abs((placed['x']+placed['w']/2)-(slot['x']+slot['w']/2))*viewport['width'],abs((placed['y']+placed['h']/2)-(slot['y']+slot['h']/2))*viewport['height'],abs(placed['w']-slot['w'])*viewport['width'],abs(placed['h']-slot['h'])*viewport['height'])<=tolerance

def completed_handoff(state, viewport):
    handoff=state.get('handoff') or {};player=state.get('player') or {};card=handoff.get('cardRect');replay=handoff.get('replayRect');duration=handoff.get('duration')
    def inside(rect):return target_rect(rect,viewport) and rect['x']>=0 and rect['y']>=0 and rect['x']+rect['w']<=1 and rect['y']+rect['h']<=1
    progress=any(number(handoff.get(key)) and handoff[key]>=ORIGINAL_HEART_ENDING_SECONDS-.35 for key in ('peakTime','elapsed'))
    return handoff.get('started') is True and handoff.get('completed') is True and handoff.get('playing') is False and handoff.get('phase')=='complete' and handoff.get('starts')==1 and number(duration) and abs(duration-ORIGINAL_HEART_ENDING_SECONDS)<.03 and progress and handoff.get('cardVisible') is True and player.get('canControl') is False and 'nearestTalk' in state and state['nearestTalk'] is None and inside(card) and inside(replay) and replay['w']*viewport['width']>=44 and replay['h']*viewport['height']>=44

def validate_completed_handoff(journey):
    proof=journey.get('endingPlayback') or {};viewport=proof.get('viewport') or {};state=journey.get('lastState') or {};duration=proof.get('duration');samples=proof.get('handoffSamples') or []
    if not(number(duration) and abs(duration-ORIGINAL_HEART_ENDING_SECONDS)<.03 and proof.get('starts')==1 and proof.get('finishedPlaying') is True and number(proof.get('observedPeakTime')) and proof['observedPeakTime']>=duration-.35):raise ValueError('Original20.316-second ending has not completed without replay or seeking')
    if not completed_handoff(state,viewport) or proof.get('handoff')!=state.get('handoff'):raise ValueError('Native ending lacks its locked, bounded completion card and44px Replay target')
    if not number(proof.get('handoffHoldMilliseconds')) or proof['handoffHoldMilliseconds']<1500 or len({sample.get('frame') for sample in samples})<3:raise ValueError('Completed handoff lacks fresh observations during a1.5-second no-input hold')
    if not all(completed_handoff({'handoff':sample.get('handoff'),'player':{'canControl':sample.get('canControl')},'nearestTalk':sample.get('nearestTalk')},viewport) for sample in samples):raise ValueError('Completed handoff unlocked, lost its card or automatically replayed during observation')
    if 'native-ending-locked-completion-card-no-automatic-replay' not in journey.get('checks',[]):raise ValueError('Native completion handoff proof is not joined to the complete original route')
    return proof

def fresh_replay_start(state):
    handoff=state.get('handoff') or {}
    return state.get('started') is False and handoff.get('started') is False and handoff.get('completed') is False and handoff.get('starts')==0 and handoff.get('cardVisible') is False and state.get('waypoints')==0 and state.get('games')==[]

def validate_manual_replay(journey):
    validate_completed_handoff(journey)
    proof=journey.get('manualReplay') or {};terminal=journey.get('lastState') or {};viewport=(journey.get('endingPlayback') or {}).get('viewport');target=proof.get('targetRect');point=proof.get('touchCss') or {};picture=proof.get('screenshot') or {};snapshots=journey.get('snapshots') or []
    if proof.get('status')!='passed' or proof.get('method')!='real-touch-on-completion-Play-again':raise ValueError('Genuine manual Replay touch was not accepted')
    if not(number(proof.get('timeoutMilliseconds')) and proof['timeoutMilliseconds']==30000 and number(proof.get('waitMilliseconds')) and 0<=proof['waitMilliseconds']<=proof['timeoutMilliseconds'] and proof.get('idleHoldMilliseconds',0)>=500):raise ValueError('Manual Replay lacks bounded, idle fresh-start observation')
    if proof.get('viewport')!=viewport or target!=(terminal.get('handoff') or {}).get('replayRect') or not target_rect(target,viewport):raise ValueError('Manual Replay target differs from the accepted completion button')
    if not all(number(point.get(key)) for key in ('x','y')) or abs(point['x']-(target['x']+target['w']/2)*viewport['width'])>.001 or abs(point['y']-(target['y']+target['h']/2)*viewport['height'])>.001:raise ValueError('Manual Replay touch did not target the measured button center')
    if not isinstance(proof.get('beforeOrigin'),str) or not re.fullmatch(r'http://(?:127\.0\.0\.1|localhost):\d+',proof['beforeOrigin']) or proof.get('afterOrigin')!=proof['beforeOrigin']:raise ValueError('Manual Replay left the original browser origin')
    final=next((row for row in snapshots if row.get('name')=='final-state'),{})
    if proof.get('terminalFrame')!=terminal.get('frame') or proof.get('terminalSnapshot')!={'name':'final-state','sha256':final.get('sha256')} or not final or final.get('state')!=terminal:raise ValueError('Manual Replay replaced or lost the immutable first-playthrough endpoint')
    fresh=next((row for row in snapshots if row.get('name')=='manual-replay-fresh-start'),{})
    if not fresh_replay_start(proof.get('startState') or {}) or fresh.get('state')!=proof.get('startState') or picture!={'name':'manual-replay-fresh-start.png','sha256':fresh.get('sha256')} or not fresh:raise ValueError('Manual Replay lacks a separate, reset Start-state image and observation')
    if 'real-touch-manual-replay-returns-to-idle-start-without-automatic-replay' not in journey.get('checks',[]):raise ValueError('Manual Replay proof is not joined to the accepted original route')
    return proof

def check_browser_diagnostics(data, require, label):
    diagnostics=data.get('browserDiagnostics',[])
    require(isinstance(diagnostics,list),label+' browser diagnostics are malformed')
    if not isinstance(diagnostics,list): return
    require(all(isinstance(row,dict) and row.get('message')==TOUCH_CANCEL_INTERVENTION and row.get('classification')=='nonfatal-browser-intervention' and bool(row.get('reason')) for row in diagnostics),label+' contains an unrecognized browser diagnostic; application errors cannot be reclassified')

def check_mobile_look(mobile, require):
    """Join existing real-gesture measurements and later released-body poses."""
    geometry=mobile.get('controlGeometry') or []
    samples={row.get('name'):row.get('state') or {} for row in mobile.get('samples',[]) if isinstance(row,dict)}
    metrics={}
    for orientation in ('landscape','portrait'):
        gesture=mobile.get(orientation) or {}
        directions=[row.get('direction') for row in gesture.get('trials',[])]
        require(bool(directions) and all(direction in ('forward','right','left','back') and orientation+'-'+direction+'-all-up-clears-input' in mobile.get('checks',[]) for direction in directions),orientation+' actual gesture trials lack direction-specific all-up input release checks')
        shape=next((row for row in geometry if isinstance(row,dict) and row.get('label')==orientation),{})
        viewport=shape.get('viewport') or {};swipe=gesture.get('lookSwipeCssPixels');direction=gesture.get('direction')
        trial=next((row for row in gesture.get('trials',[]) if row.get('direction')==direction),{})
        names=[orientation+'-'+str(direction)+'-touch-start-ownership',orientation+'-'+str(direction)+'-gesture-result']
        before=(samples.get(names[0],{}).get('player') or {}).get('yaw')
        after=(samples.get(names[1],{}).get('player') or {}).get('yaw')
        observed=gesture.get('lookYawDegrees');trial_turn=trial.get('lookYawDegrees')
        valid=all(number(value) for value in (viewport.get('width'),viewport.get('height'),swipe,before,after,observed,trial_turn))
        valid=valid and min(viewport.get('width',0),viewport.get('height',0),swipe or 0)>0 and bool(direction)
        require(valid,orientation+' mobile look lacks viewport, swipe, drag and released-pose measurements')
        if not valid:continue
        # OriginalTouchPad.LookDegrees uses150 degrees per short viewport side,
        # with a320px minimum. DPR cancels between input pixels and Screen size.
        expected=swipe*150/max(320,min(viewport['width'],viewport['height']))
        released=abs(((after-before+180)%360)-180)
        require(abs(observed-trial_turn)<.1,orientation+' selected look measurement differs from its actual gesture trial')
        require(.65*expected<=observed<=1.4*expected,orientation+' actual mobile look lost or multiplied the authored swipe response')
        require(.65*expected<=released<=1.4*expected and abs(released-observed)<=5,orientation+' native body yaw did not persist after the touch gesture released')
        metrics[orientation]={'viewport':viewport,'swipeCssPixels':swipe,'expectedDegrees':expected,'observedDragDegrees':observed,'observedAfterReleaseDegrees':released,'persistenceDifferenceDegrees':abs(released-observed),'responseRatio':observed/expected,'baselineYaw':before,'releasedYaw':after,'sourceSamples':names}
    return metrics

def validate_release_mobile(folder, current, build_hashes, project_root=ROOT):
    """Read mandatory production phone evidence; shared by audit and packager."""
    def check(condition,message):
        if not condition:raise ValueError(message)
    folder=Path(folder);family=project_root/'Reports/OriginalScene/ReleaseBrowser/mobile'
    check(not folder.is_symlink() and family.resolve() in folder.resolve().parents,'Release-mobile run is outside its evidence family')
    raw_file=safe(folder,'acceptance.json');review_file=safe(folder,'visual-audit.json')
    check(raw_file and review_file,'Release-mobile acceptance and visual review are required')
    raw=load(raw_file);review=load(review_file)
    check(raw.get('status')=='smoke-passed-awaiting-visual-review','Release-mobile runtime smoke did not pass')
    check(raw.get('sourceInputSha256')==current,'Release-mobile source fingerprint is stale')
    check(len(build_hashes)==8 and raw.get('buildHashes')==build_hashes,'Release-mobile must bind all eight current production artifacts')
    check(not raw.get('errors') and not raw.get('externalRequests') and not raw.get('unexpectedRequestFailures'),'Release-mobile contains application, external or unexpected network errors')
    check(raw.get('physicalDeviceAcceptance') is False and bool(raw.get('scope')),'Release-mobile must retain its emulation scope')
    cases=raw.get('cases',[])
    check(isinstance(cases,list) and len(cases)==2 and {row.get('name') for row in cases}==set(RELEASE_MOBILE_VIEWPORTS),'Release-mobile must cover exactly both phone orientations')
    pictures={}
    for row in cases:
        label=row['name'];viewport=RELEASE_MOBILE_VIEWPORTS[label];environment=row.get('environment') or {};canvas=environment.get('canvas') or {}
        check(row.get('status')=='smoke-passed-awaiting-visual-review' and row.get('viewport')==viewport,label+' production phone case did not pass at the required viewport')
        check(not row.get('errors') and not row.get('externalRequests') and not row.get('unexpectedRequestFailures'),label+' production phone case contains application or network errors')
        check(RELEASE_MOBILE_CHECKS.issubset(set(row.get('checks',[]))),label+' production phone input/startup checks are incomplete')
        check('Android' in environment.get('userAgent','') and environment.get('coarsePointer') is True and environment.get('maxTouchPoints',0)>=2 and environment.get('devicePixelRatio')==2,label+' Android multitouch/DPR2 environment is missing')
        check(environment.get('qaTelemetry')=='undefined',label+' production phone exposes development telemetry')
        check(canvas.get('cssWidth')==viewport['width'] and canvas.get('cssHeight')==viewport['height'],label+' phone canvas CSS bounds differ from its viewport')
        scale=environment.get('renderToCssScale')
        check(number(scale) and scale>1 and all(number(canvas.get(k)) and canvas[k]>0 for k in ('width','height')),label+' production scaled backbuffer was not measured')
        check(abs(canvas['width']/viewport['width']-scale)<.01 and abs(canvas['height']/viewport['height']-scale)<.01,label+' production backbuffer measurements are inconsistent')
        expected={str(viewport['width'])+'x'+str(viewport['height'])+'-'+stage+'.png' for stage in ('start','intro','dialogue')}
        screenshots=row.get('screenshots',[])
        check(len(screenshots)==3 and {item.get('name') for item in screenshots}==expected,label+' Start, intro and dialogue captures are incomplete')
        for item in screenshots:
            name=item['name'];file=safe(folder,name)
            check(file and sha(file)==item.get('sha256'),'Release-mobile screenshot changed: '+name)
            pictures[name]=item['sha256']
        raf=row.get('browserRaf') or {}
        check(len(raf.get('samplesMilliseconds',[]))==60 and all(number(x) for x in raf['samplesMilliseconds']) and number(raf.get('medianMilliseconds')) and number(raf.get('p95Milliseconds')),label+' browser RAF diagnostics are incomplete')
    runner=raw.get('runner') or {};executed=safe(folder,runner.get('path',''));source=project_root/'Tools/browser/original-release-mobile-smoke.cjs'
    check(executed and source.is_file() and sha(executed)==runner.get('sha256')==sha(source) and bool(runner.get('nodeVersion')),'Exact production phone runner is absent or changed')
    check(review.get('status')=='passed' and review.get('acceptanceSha256')==sha(raw_file),'Release-mobile visual review is missing, failed or bound to a different report')
    check(review.get('screenshots')==pictures and len(pictures)==6,'Release-mobile visual review must bind all six phone captures')
    check(bool(review.get('method')) and bool(review.get('findings')),'Release-mobile visual review has no inspection method or findings')
    return {'status':raw['status'],'scope':raw['scope'],'sourceInputSha256':current,'report':str(raw_file.relative_to(project_root)),'sha256':sha(raw_file),'browserVersion':raw.get('browserVersion'),'runner':runner,'buildHashes':raw['buildHashes'],'cases':cases,'screenshots':[{'name':name,'sha256':digest} for name,digest in sorted(pictures.items())],'visualReview':{**review,'report':str(review_file.relative_to(project_root)),'sha256':sha(review_file)},'physicalDeviceAcceptance':False,'limitations':raw.get('limitations',[])}

def validate_journey_visual(folder, current, build_hashes, project_root=ROOT):
    """Require human review of the complete route's critical actual captures."""
    def check(condition,message):
        if not condition:raise ValueError(message)
    folder=Path(folder);family=project_root/'Reports/OriginalScene/BrowserJourney'
    check(not folder.is_symlink() and family.resolve() in folder.resolve().parents,'Journey visual evidence is outside its accepted family')
    raw_file=safe(folder,'acceptance.json');review_file=safe(folder,'visual-review.json')
    check(raw_file and review_file,'Full-journey visual-review.json is required')
    raw=load(raw_file);review=load(review_file)
    check(raw.get('status')=='passed-original-meetingcells-to-external-handoff' and not raw.get('errors') and not raw.get('boundaries') and not raw.get('externalRequests'),'Journey visual review must bind an accepted complete route')
    validate_completed_handoff(raw)
    validate_manual_replay(raw)
    runner=raw.get('runner') or {};main=(runner.get('files') or {}).get('Tools/browser/original-journey.cjs') or {}
    check(runner.get('sourceInputSha256')==current,'Journey visual-review source fingerprint is stale')
    check(len(build_hashes)==8 and raw.get('buildHashes')==build_hashes,'Journey visual review must bind all eight current development artifacts')
    executed=safe(folder,main.get('path',''));source=project_root/'Tools/browser/original-journey.cjs'
    check(executed and source.is_file() and sha(executed)==main.get('sha256')==sha(source),'Journey visual review uses a changed or missing executed runner')
    snapshots=raw.get('snapshots') or [];pictures={item['name']+'.png':item['sha256'] for item in snapshots}
    check(len(pictures)==len(snapshots),'Journey screenshot names are duplicated')
    cue=[]
    for item in snapshots:
        state=item.get('state') or {}
        if any(t.get('name')=='Storyline_introduce_yellow_circle' and t.get('active') and t.get('playing') for t in state.get('timelines',[])) and any(re.search(r'follow\s+cap',objective.get('description',''),re.I) for objective in state.get('reachObjectives',[])):cue.append(item['name']+'.png')
    check(len(cue)==1,'The actual Follow CAP cinematic cue capture is missing or ambiguous')
    required={f'mobile-workbook-{page:02}-filled.png' for page in range(1,14)}|{'mobile-workbook-02-field-entry.png','mobile-workbook-02-field-committed.png','mobile-oxygen-native-stick.png','mobile-white-native-stick.png','original-ending-midpoint.png','original-ending-complete.png','manual-replay-fresh-start.png',cue[0]}
    check(required.issubset(pictures),'Critical complete-route screenshots are missing')
    check(review.get('status')=='passed' and review.get('acceptanceSha256')==sha(raw_file),'Full-journey visual review is absent, needs fixes or binds a different accepted report')
    check(review.get('verifiedInputSha256')==current and review.get('buildHashes')==build_hashes and review.get('executedSourceSha256')==main['sha256'],'Full-journey visual review is not bound to current source, build and executed runner')
    reviewed=review.get('screenshots') or {}
    check(isinstance(reviewed,dict) and required.issubset(reviewed),'Full-journey visual review must cover all21 critical captures, including the manual Replay fresh Start')
    for name,digest in reviewed.items():
        image=safe(folder,name)
        check(pictures.get(name)==digest and image and sha(image)==digest,'Reviewed journey screenshot changed or is outside the accepted route: '+name)
    check(bool(review.get('method')) and bool(review.get('findings')),'Full-journey visual review needs its actual inspection method and findings')
    return {**review,'report':str(review_file.relative_to(project_root)),'sha256':sha(review_file),'requiredScreenshots':sorted(required),'followCapSnapshot':cue[0]}

def validate_native_keyboard_dom(project_root=ROOT):
    """Separate synthetic DOM evidence; never counts as the main JS contracts."""
    report_file=project_root/'Reports/OriginalScene/native-mobile-keyboard-dom-contract.json'
    data=load(report_file);html_file=project_root/'Assets/WebGLTemplates/SPARCOriginal/index.html';fixture_file=project_root/'Tools/verify_native_mobile_keyboard_dom.cjs'
    if data.get('status')!='passed' or data.get('checkCount')!=14 or len(set(data.get('checks',[])))!=14 or data.get('mainJsTestCountUnchanged') is not True:raise ValueError('Separate native keyboard DOM fixtures did not pass14 distinct checks')
    marker=re.search(rb'<script id="sparc-native-keyboard-marker">([\s\S]*?)</script>',html_file.read_bytes())
    if data.get('inputHtml')!='Assets/WebGLTemplates/SPARCOriginal/index.html' or sha(html_file)!=data.get('inputHtmlSha256') or sha(fixture_file)!=data.get('fixtureSha256') or not marker or hashlib.sha256(marker[1]).hexdigest()!=data.get('markerScriptSha256'):raise ValueError('Native keyboard DOM fixture source binding is stale')
    return {**data,'report':str(report_file.relative_to(project_root)),'sha256':sha(report_file),'fixture':'Tools/verify_native_mobile_keyboard_dom.cjs'}

def check_hybrid_mobile(journey, expected_pages, expected_slots, expected_fields, require):
    worksheet=journey.get('mobileWorksheet') or {};pages=worksheet.get('pages',[])
    require(worksheet.get('physicalDeviceAcceptance') is False and 'hybrid' in worksheet.get('scope','').lower(),'Mobile worksheet scope must disclose hybrid input and emulation')
    require(WORKSHEET_CHECKS.issubset(set(worksheet.get('checks',[]))),'Actual mobile worksheet drag/tap/cancel/typing/submit checks are incomplete')
    require(isinstance(pages,list) and len(pages)==expected_pages and [p.get('page') for p in pages]==list(range(1,expected_pages+1)),'Mobile worksheet must visit every original page exactly once and in order')
    filled=sum(p.get('filledSlots',0) for p in pages);total=sum(p.get('totalSlots',0) for p in pages)
    require(worksheet.get('totalFilledSlots')==filled==total==expected_slots and all(p.get('filledSlots')==p.get('totalSlots') for p in pages),'Mobile worksheet does not prove all original native slots were populated')
    interactions=[(p,row) for p in pages for row in p.get('interactions',[])]
    require(len(interactions)==expected_slots and {row.get('mode') for _,row in interactions}=={'drag','tap'} and worksheet.get('dragVerified') is True,'Mobile worksheet needs actual drag and tap placements, not only a completion flag')
    require(all(row.get('word') and row.get('slot') and target_rect(row.get('tileRect'),p.get('viewport')) and target_rect(row.get('slotRect'),p.get('viewport')) for p,row in interactions),'Mobile worksheet interaction bounds are missing, clipped or smaller than44 CSS pixels')
    require(all(placed_word_aligned(row,p.get('viewport')) for p,row in interactions),'Mobile worksheet placed word tiles do not align with the native slot center and size within1.5 CSS pixels')
    require(all(target_rect(p.get('next',{}).get('rect'),p.get('viewport')) for p in pages) and bool(pages) and 'submit' in pages[-1].get('next',{}).get('text','').lower(),'Mobile worksheet native Next/Submit targets were not retained and reachable')
    orientations={(p.get('viewport',{}).get('width',0)>p.get('viewport',{}).get('height',0)) for p in pages}
    require(orientations=={True,False},'Mobile worksheet did not cover both phone orientations')
    entries=[entry for p in pages for entry in p.get('fieldEntries',[])]
    require(len(entries)==expected_fields and all(len(p.get('fieldEntries',[]))==len(p.get('fields',[])) and {e.get('name') for e in p.get('fieldEntries',[])}=={f.get('name') for f in p.get('fields',[])} for p in pages),'Mobile worksheet native text-field typing is incomplete or targets a different field')
    # The canonical Tutorial field is ASCII alphanumeric, with a16-character limit.
    require(all(isinstance(e.get('expectedText'),str) and re.fullmatch(r'[A-Za-z0-9]{1,16}',e['expectedText']) and e.get('typedNativeText')==e['expectedText']==e.get('committedNativeText') and e.get('keyboardDismissed') is True and e.get('method')=='real-native-field-tap-and-keyboard-typing' and e.get('commit',{}).get('method')=='real-touch-on-Unity-HTML-OK' for e in entries),'Mobile worksheet input does not prove valid native text entry and real keyboard commit/dismissal')
    require(all(css_target_rect(e.get('commit',{}).get('rect'),p.get('viewport')) for p in pages for e in p.get('fieldEntries',[])),'Actual native HTML keyboard OK target is clipped or smaller than44 by44 CSS pixels')
    require(worksheet.get('fieldInputs')==entries,'Mobile worksheet field-entry/commit telemetry is not joined to its actual page entries')
    cancel=worksheet.get('cancelVerified') or {};home=cancel.get('home') or {};returned=cancel.get('returned') or {}
    require(cancel.get('word') and cancel.get('assignedSlots')==0 and all(number(home.get(k)) and number(returned.get(k)) and abs(home[k]-returned[k])<.004 for k in ('x','y','w','h')),'Actual word touchCancel did not prove unassigned bank restoration')
    all_words=cancel.get('allWords') or []
    require(len(all_words)>=3 and all(all(number(row.get('home',{}).get(k)) and number(row.get('returned',{}).get(k)) and abs(row['home'][k]-row['returned'][k])<.004 for k in ('x','y','w','h')) for row in all_words),'Actual word cancellation did not preserve every rendered bank home')
    environment=journey.get('mobileInputEnvironment') or {}
    require(environment.get('coarsePointer') is True and environment.get('maxTouchPoints',0)>=1 and environment.get('devicePixelRatio')==1 and 'Android' in environment.get('userAgent',''),'Hybrid worksheet input environment is missing or inaccurately scoped')
    touch=journey.get('mobileMinigameTouch') or {}
    require(touch.get('physicalDeviceAcceptance') is False and 'hybrid' in touch.get('scope','').lower(),'Native minigame touch evidence must disclose hybrid/emulated scope')
    for name in ('oxygen','white'):
        evidence=touch.get(name) or {};attempts=evidence.get('attempts',[]);events=evidence.get('events',[])
        require(evidence.get('passed') is True and STICK_CHECKS.issubset(set(evidence.get('checks',[]))),name+' native joystick touch/end/cancel checks did not pass')
        require(target_rect(evidence.get('joystick'),evidence.get('viewport')),name+' native joystick target is missing, clipped or under44 CSS pixels')
        require(any(number(a.get('axisMagnitude')) and a['axisMagnitude']>.15 and number(a.get('movedCssPixels')) and a['movedCssPixels']>4 and number(a.get('elapsedBefore')) and number(a.get('elapsedAfter')) and a['elapsedAfter']>a['elapsedBefore'] for a in attempts),name+' touch evidence lacks actual virtual-axis, body-motion and advancing-clock measurements')
        require({'touchStart','touchMove','touchEnd','touchCancel'}.issubset({e.get('type') for e in events}),name+' native touch events do not include actual end and cancellation')
        require('mobile-'+name+'-native-OnScreenStick-real-touch-and-cancel' in journey.get('checks',[]),name+' native touch verification was not joined to the accepted original route')
    return {'worksheet':worksheet,'minigames':touch,'environment':environment,'expectedSlots':expected_slots,'expectedTypedFields':expected_fields}

def worksheet_field_count(inventory):
    objects={n['id']:n for n in inventory['objects']};transforms={str(n['data']['m_GameObject']['fileID']):n['data'] for n in inventory['objects'] if n['class'] in (4,224) and 'm_GameObject' in n['data']}
    root=next(str(n['data']['m_GameObject']['fileID']) for n in inventory['objects'] if n['type']=='WordGameManager')
    def inside(gid):
        seen=set()
        while gid and gid not in seen:
            if gid==root:return True
            seen.add(gid);parent=str(transforms.get(gid,{}).get('m_Father',{}).get('fileID'));gid=str(objects.get(parent,{}).get('data',{}).get('m_GameObject',{}).get('fileID',''))
        return False
    return sum(n['type'] in ('TMP_InputField','InputField') and inside(str(n['data'].get('m_GameObject',{}).get('fileID'))) for n in inventory['objects'])

def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--journey',help='Exact original BrowserJourney directory, project-relative');parser.add_argument('--ending',help='Optional separate assisted-input full-ending run, project-relative');parser.add_argument('--controls',required=True,help='Exact current real-input original-controls browser directory');parser.add_argument('--mobile',required=True,help='Exact current real-touch mobile-emulation directory');parser.add_argument('--release-mobile',required=True,help='Exact current production phone Start/intro/dialogue run directory');args=parser.parse_args()
    failures=[];current=fingerprint(input_manifest())
    report={'status':'running','auditedUtc':now(),'sourceInputSha256':current,'scope':'Original MeetingCells scene, original story route and source animations; not the rejected expedition'}
    def require(condition, message):
        if not condition: failures.append(message)
    builds={};build_outputs={}
    for kind, filename, folder in [('development','verified-webgl-build.json','Builds/WebGLOriginal'),('release','verified-webgl-release.json','Builds/WebGLOriginalRelease')]:
        evidence=load(OUT/filename)
        matching=evidence.get('inputSha256')==evidence.get('inputSha256After')==current
        outputs=evidence.get('outputs',{})
        build_outputs[kind]={name:item['sha256'] for name,item in outputs.items()}
        verified=bool(outputs) and all((p:=safe(ROOT/folder,name)) and sha(p)==item['sha256'] for name,item in outputs.items())
        require(evidence.get('status')=='passed' and matching and verified, kind+' build does not match current source and output')
        require(evidence.get('buildSummary',{}).get('errors')==0, kind+' build contains errors')
        builds[kind]={**{k:v for k,v in evidence.items() if k!='outputs'},'artifactHashesVerified':bool(verified),'sourceMatches':matching,'fileCount':len(outputs),'bytes':sum(i['bytes'] for i in outputs.values())}
    report['builds']=builds
    tests=load(OUT/'verified-tests.json');item=tests['tests'];xml=ROOT/item['file'];test_xml=ET.parse(xml).getroot()
    require(tests.get('status')=='passed' and tests.get('inputSha256')==tests.get('inputSha256After')==current and sha(xml)==item['sha256'],'Unity test evidence is stale')
    require(test_xml.attrib.get('result')=='Passed' and int(test_xml.attrib.get('total',0))>=25 and int(test_xml.attrib.get('failed',-1))==0,'Original Unity tests did not pass completely')
    report['unityTests']={**item,'caseNames':[n.attrib['name'] for n in test_xml.iter('test-case')]}
    contracts=load(OUT/'verified-contracts.json')
    require(contracts.get('status')=='passed' and all((p:=safe(ROOT,name)) and sha(p)==digest for name,digest in contracts.get('sources',{}).items()),'Browser/server contract sources changed after testing')
    for check in contracts.get('checks',[]):
        if 'log' in check:require(sha(ROOT/check['log'])==check['sha256'] and check.get('exitCode')==0,'Contract result changed: '+check['name'])
    tap=(OUT/'browser-contracts.tap').read_text();python=(OUT/'python-contracts.log').read_text()
    node_total=re.search(r'^# tests (\d+)$',tap,re.M);node_failed=re.search(r'^# fail (\d+)$',tap,re.M);python_total=re.search(r'Ran (\d+) tests',python)
    require(node_total and int(node_total[1])==37 and node_failed and int(node_failed[1])==0,'37 browser contract tests are not proven passing')
    require(python_total and int(python_total[1])==14 and re.search(r'^OK$',python,re.M),'14 server tests are not proven passing')
    report['contracts']={'browser':37,'server':14,'failed':0,'evidence':'Reports/OriginalScene/verified-contracts.json','sha256':sha(OUT/'verified-contracts.json')}
    try:report['nativeMobileKeyboardDom']=validate_native_keyboard_dom()
    except (OSError,ValueError,TypeError,KeyError,AttributeError) as error:
        require(False,'Separate native keyboard DOM contract failed: '+str(error));report['nativeMobileKeyboardDom']={'status':'failed','failure':str(error)}
    fidelity=load(OUT/'fidelity-validation.json')
    require(fidelity.get('status')=='passed-source-fidelity-only' and not fidelity.get('errors') and fidelity.get('sourceSceneUnchanged'),'Original fidelity audit did not pass')
    original=ROOT.parent/'Yanlai_Zlad_SPARC_Merge/Assets/GameMeetingCells/Scenes/MeetingCells_NewEnv.unity'
    require(sha(original)==fidelity.get('sourceSceneSha256'),'Original source scene differs from the fidelity audit')
    for item in fidelity['timelines']+fidelity['animationClips']:
        require(sha(ROOT/item['path'])==item['sourceSha256'],'Source Timeline/clip changed: '+item['path'])
    report['fidelity']={key:fidelity[key] for key in ('sourceSceneSha256','sourceSceneUnchanged','nativeRecordsExact','nativeRecordsExpected','sourceGraphBlocks','sourceGraphCommands','newImplementationFiles','exactLegacyCSharpCopies')}
    report['fidelity'].update(dialogueRecords=len(fidelity['dialogueAndVoiceRecords']),timelineAssets=len(fidelity['timelines']),standaloneClips=len(fidelity['animationClips']),animatorControllers=len(fidelity['animatorControllers']),directors=len(fidelity['originalDirectors']))
    pointer=load(OUT/'latest-browser-journey.json');folder=ROOT/(args.journey or pointer['directory'])
    require((OUT/'BrowserJourney').resolve() in folder.resolve().parents,'Journey is not an original-scene report')
    journey=load(folder/'acceptance.json');hashes=journey.get('buildHashes',{})
    require(hashes==build_outputs['development'],'Browser journey must bind every current development artifact')
    require(hashes and all((p:=safe(ROOT/'Builds/WebGLOriginal',name)) and sha(p)==digest for name,digest in hashes.items()),'Browser journey does not match the current development build')
    require(journey.get('status')=='passed-original-meetingcells-to-external-handoff' and not journey.get('errors') and not journey.get('boundaries') and not journey.get('externalRequests'),'Full original MeetingCells browser route is incomplete, has errors or attempted external access')
    state=journey.get('lastState') or {};timelines=state.get('timelines',[])
    intro=next((t for t in timelines if t['name']=='StorylineIntro'),{})
    ending=next((t for t in timelines if t['name']=='Storyline_MeetingCellsTransition2Heart'),{})
    require(intro.get('starts')==1 and abs(intro.get('duration',0)-24.1)<0.01,'Original 24.1-second intro replayed or differs')
    require(ending.get('starts')==1,'Original ending must begin exactly once')
    require(state.get('waypoints')==4,'Original CAP route did not visit four authored waypoints')
    require(journey.get('oxygenSeen') and not journey.get('oxygenAssisted') and (journey.get('oxygenActualWallSeconds') or 0)>=115 and journey.get('oxygenFinal',{}).get('Finished'),'Full real-time original oxygen activity was not completed')
    require(journey.get('whiteSeen') and journey.get('questionAnswered') and journey.get('workbookSubmitted'),'Required original activity, quiz or worksheet was skipped')
    original_inventory=load(OUT/'manifest.json');source_words=next(n['data'] for n in original_inventory['objects'] if n['type']=='WordGameManager');expected_pages=len(source_words['wordGamePages'])
    expected_slots=sum(n['type']=='DropSlotUIElement' for n in original_inventory['objects']);expected_fields=worksheet_field_count(original_inventory)
    require(journey.get('workbookPages')==expected_pages,'Worksheet page count does not match the original source')
    report['fidelity']['originalWorksheetPages']=expected_pages
    interactions=journey.get('workbookInteractions',[]);require(len(interactions)==expected_pages and all(row['filledSlots']==row['totalSlots'] for row in interactions),'Original worksheet drag/drop interactions are incomplete')
    require(sum(row.get('filledSlots',0) for row in interactions)==expected_slots,'Original worksheet slot total does not match the source')
    mobile_failures_before=len(failures)
    hybrid=check_hybrid_mobile(journey,expected_pages,expected_slots,expected_fields,require)
    check_browser_diagnostics(journey,require,'Hybrid original journey')
    runner=journey.get('runner') or {};runner_files=runner.get('files',{})
    require(runner.get('sourceInputSha256')==current and bool(runner.get('nodeVersion')) and set(runner_files)=={'Tools/browser/'+name for name in HYBRID_RUNNERS},'Exact hybrid route/helper runner provenance is absent or stale')
    for name in HYBRID_RUNNERS:
        source_name='Tools/browser/'+name;row=runner_files.get(source_name,{})
        copy=safe(folder,row.get('path',''));source=safe(ROOT,source_name)
        require(copy and source and sha(copy)==row.get('sha256')==sha(source),'Executed hybrid route/helper source changed: '+source_name)
    mobile_validated=bool(len(failures)==mobile_failures_before and journey.get('status')=='passed-original-meetingcells-to-external-handoff' and not journey.get('errors') and not journey.get('boundaries') and not journey.get('externalRequests') and hashes==build_outputs['development'] and runner.get('sourceInputSha256')==current)
    require(mobile_validated,'Hybrid mobile evidence is not joined to an accepted, current, provenance-bound full original route')
    for snap in journey.get('snapshots',[]):require(sha(folder/(snap['name']+'.png'))==snap['sha256'],'Journey screenshot changed: '+snap['name'])
    known={d['id']:d['text'] for d in fidelity['dialogueAndVoiceRecords']}
    mismatches=[line['id'] for line in journey.get('lines',[]) if known.get(line['id'])!=line['text']]
    require(not mismatches,'Browser displayed dialogue different from original Say data')
    poses={}
    for snapshot in journey.get('snapshots',[]):
        for actor in snapshot.get('state',{}).get('actors',[]):
            name=actor['path'];entry=poses.setdefault(name,{'path':name,'controller':actor.get('controller'),'movedBoneSamples':0,'clips':set()})
            entry['movedBoneSamples']=max(entry['movedBoneSamples'],actor.get('accumulatedMovedBones',0));entry['clips'].update(c['name'] for c in actor.get('clips',[]))
    poses=[{**entry,'clips':sorted(entry['clips'])} for entry in poses.values() if entry['movedBoneSamples']>0]
    require(bool(poses),'No actual original skeleton motion was observed in the browser')
    require(journey.get('video') and sha(folder/journey['video']['path'])==journey['video']['sha256'],'Browser recording is missing or changed')
    oxygen=journey.get('oxygenFinal') or {};white=journey.get('whiteFinal') or {};peer=state.get('peerAgent') or {}
    source_white=next(n['data'] for n in original_inventory['objects'] if n['type']=='WhiteBloodCellGameManager')
    require(oxygen.get('Duration')==120 and white.get('Duration')==source_white['time_limit'],'Original activity durations differ from the original source')
    white_win=any(line.get('block')=='WBC_WIN' for line in journey.get('lines',[]));white_loss=any(line.get('block')=='WBC_LOSE' for line in journey.get('lines',[]))
    require(not journey.get('whiteAssisted') and white.get('Finished') and ((white_win and white.get('Level')==source_white['maxLevels'] and white.get('remaining')==0) or (white_loss and white.get('Elapsed',0)>=source_white['time_limit']-.25)),'Original White activity lacks an unassisted native win or full-timer loss continuation')
    report['journey']={'report':str((folder/'acceptance.json').relative_to(ROOT)),'sha256':sha(folder/'acceptance.json'),'startedUtc':journey.get('startedUtc'),'finishedUtc':journey.get('finishedUtc'),'status':journey.get('status'),'errors':journey.get('errors'),'boundaries':journey.get('boundaries'),'checks':journey.get('checks'),'originalDialogueIds':list(dict.fromkeys(line['id'] for line in journey.get('lines',[]))),'dialogueMatchesOriginal':not mismatches,'oxygen':{k:oxygen.get(k) for k in ('Score','Elapsed','Duration','Finished')},'oxygenWallSeconds':journey.get('oxygenActualWallSeconds'),'white':{k:white.get(k) for k in ('Score','Level','Elapsed','Duration','Finished','remaining')},'whiteWinBranch':any(line.get('block')=='WBC_WIN' for line in journey.get('lines',[])),'whiteLossBranch':any(line.get('block')=='WBC_LOSE' for line in journey.get('lines',[])),'workbookPages':journey.get('workbookPages'),'workbookInteractions':journey.get('workbookInteractions'),'originalEndingStarts':ending.get('starts'),'peerAgent':peer,'actorMotion':poses,'video':{**journey.get('video',{}),'directory':str(folder.relative_to(ROOT))},'screenshots':[{'name':s['name']+'.png','sha256':s['sha256']} for s in journey.get('snapshots',[])]}
    restored=journey.get('restoredInteractions',[])
    require(len(restored)>=2 and all(row.get('highlight') and row.get('menuVisible') and row.get('nativeButtons')==2 and 'native Talk button pointer' in row.get('method','') for row in restored),'Restored Red/White native Talk menus lack actual highlighted-character pointer acceptance')
    report['restoredInteraction']={'nativeControllers':3,'actualPointerActors':restored,'scope':'Native controller/Observe/callback preservation in Unity; real highlighted Red and White approach and Talk pointer in this browser route'}
    report['journey'].update(scope=journey.get('scope'),warnings=journey.get('warnings',[]),browserDiagnostics=journey.get('browserDiagnostics',[]),runner=runner,mobileWorksheet=hybrid['worksheet'],mobileMinigameTouch=hybrid['minigames'],mobileInputEnvironment=hybrid['environment'],mobileEvidenceValidated=mobile_validated,originalWorksheetSlots=expected_slots,originalWorksheetTypedFields=expected_fields,endingPlayback=journey.get('endingPlayback'),manualReplay=journey.get('manualReplay'))
    try:validate_completed_handoff(journey)
    except (ValueError,TypeError,KeyError,AttributeError) as error:require(False,'Current original ending handoff failed: '+str(error))
    try:validate_manual_replay(journey)
    except (ValueError,TypeError,KeyError,AttributeError) as error:require(False,'Current genuine manual Replay failed: '+str(error))
    try:report['journey']['visualReview']=validate_journey_visual(folder,current,build_outputs['development'])
    except (OSError,ValueError,TypeError,KeyError,AttributeError) as error:
        require(False,'Current full-journey visual acceptance failed: '+str(error));report['journey']['visualReview']={'status':'failed','failure':str(error)}
    if args.ending:
        ending_folder=ROOT/args.ending;require((OUT/'BrowserJourney').resolve() in ending_folder.resolve().parents,'Ending report is outside original browser evidence')
        ending_run=load(ending_folder/'acceptance.json');proof=ending_run.get('endingPlayback',{});hashes=ending_run.get('buildHashes',{})
        require(hashes==build_outputs['development'],'Ending browser run must bind every current development artifact')
        require(hashes and all((p:=safe(ROOT/'Builds/WebGLOriginal',name)) and sha(p)==digest for name,digest in hashes.items()),'Ending test build is stale')
        require(ending_run.get('status')=='passed-original-meetingcells-to-external-handoff' and not ending_run.get('errors') and not ending_run.get('boundaries') and proof.get('starts')==1 and proof.get('observedPeakTime',0)>=proof.get('duration',10000)-0.35 and proof.get('finishedPlaying'),'Full ending playback has not passed')
        report['endingPlayback']={'report':str((ending_folder/'acceptance.json').relative_to(ROOT)),'sha256':sha(ending_folder/'acceptance.json'),'oxygenAssisted':ending_run.get('oxygenAssisted'),'whiteAssisted':ending_run.get('whiteAssisted'),**proof}
    controls_folder=ROOT/args.controls;require((OUT/'ControlsBrowser').resolve() in controls_folder.resolve().parents,'Control evidence is outside the original-controls directory')
    controls=load(controls_folder/'acceptance.json');hashes=controls.get('buildHashes',{})
    require(hashes==build_outputs['development'],'Control browser run must bind every current development artifact')
    require(hashes and all((p:=safe(ROOT/'Builds/WebGLOriginal',name)) and sha(p)==digest for name,digest in hashes.items()),'Control/animation test build is stale')
    require(controls.get('status')=='passed' and not controls.get('errors') and not controls.get('externalRequests') and controls.get('movedMeters',0)>1 and any(re.search('walk|run',name,re.I) for name in controls.get('walkClips',[])),'Actual browser movement did not drive the original locomotion clip')
    required_controls={'actual-player-movement-over-one-meter','original-locomotion-clip-and-bone-motion','actual-original-body-jump','real-first-person-switch','real-third-person-switch','right-drag-original-camera-look'}
    require(required_controls.issubset(set(controls.get('checks',[]))),'Original browser controls are incompletely tested')
    require(controls.get('video') and sha(controls_folder/controls['video']['path'])==controls['video']['sha256'],'Original controls recording is absent or changed')
    for image in controls.get('screenshots',[]):require(sha(controls_folder/image['name'])==image['sha256'],'Original controls image changed')
    report['browserControls']={key:controls.get(key) for key in ('status','browserVersion','checks','movedMeters','walkClips','jumpHeight','lookYawChange','errors','finishedUtc','screenshots','video')};report['browserControls'].update(report=str((controls_folder/'acceptance.json').relative_to(ROOT)),sha256=sha(controls_folder/'acceptance.json'))
    menu_checks={'native-settings-opens-through-actual-pointer','native-settings-visible-at-960x600','native-settings-back-button-restores-gameplay-ui','native-settings-preserves-story-and-activity-state'}
    require(menu_checks.issubset(set(controls.get('checks',[]))),'Redesigned Settings has not passed real pointer open/resize/close acceptance')
    report['browserControls']['menus']=controls.get('uiMenus',{})
    runner_file=controls_folder/'runner-provenance.json';runner=load(runner_file);executed=Path(runner.get('exactExecutedSourcePath',''))
    require(executed.is_file() and executed.resolve().parent==controls_folder.resolve() and sha(executed)==runner.get('sourceSha256')==sha(ROOT/'Tools/browser/original-controls.cjs') and runner.get('inputSha256')==current,'Exact real-input controls runner provenance is missing or stale')
    report['browserControls']['runnerProvenance']={**runner,'report':str(runner_file.relative_to(ROOT)),'sha256':sha(runner_file)}
    mobile_folder=ROOT/args.mobile
    require((OUT/'MobileBrowser').resolve() in mobile_folder.resolve().parents,'Mobile evidence is outside the mobile browser directory')
    mobile=load(mobile_folder/'acceptance.json');hashes=mobile.get('buildHashes',{})
    require(hashes==build_outputs['development'],'Mobile browser run must bind every current development artifact')
    require(hashes and all((p:=safe(ROOT/'Builds/WebGLOriginal',name)) and sha(p)==digest for name,digest in hashes.items()),'Mobile touch evidence build is stale')
    require(mobile.get('status')=='passed-mobile-emulation' and not mobile.get('errors') and not mobile.get('externalRequests') and mobile.get('sourceInputSha256')==current,'Real multi-touch emulation acceptance is incomplete or stale')
    required_mobile={'touch-start-and-original-dialogue-release-controls','landscape-simultaneous-movement-and-look','landscape-one-finger-release-keeps-other-active','landscape-touchcancel-clears-input','portrait-simultaneous-movement-and-look','portrait-one-finger-release-keeps-other-active','portrait-touchcancel-clears-input','native-touch-jump-lifts-original-body','landscape-settings-real-touch-open-back-and-held-input-clear','portrait-settings-real-touch-open-back-and-held-input-clear','DPR2-emulation-keeps-thumb-targets-over44-CSS-pixels'}
    require(required_mobile.issubset(set(mobile.get('checks',[]))),'Mobile control gestures, cancellation or responsive targets are not fully proven')
    check_browser_diagnostics(mobile,require,'Mobile controls')
    mobile_look=check_mobile_look(mobile,require)
    for item in mobile.get('screenshots',[]):require(sha(mobile_folder/item['name'])==item['sha256'],'Mobile screenshot changed: '+item['name'])
    require(mobile.get('video') and sha(mobile_folder/mobile['video']['path'])==mobile['video']['sha256'],'Mobile recording is missing or changed')
    runner=mobile.get('runner',{});runner_path=safe(mobile_folder,runner.get('path',''))
    require(runner_path and sha(runner_path)==runner.get('sha256')==sha(ROOT/'Tools/browser/original-mobile-controls.cjs'),'Exact mobile touch runner is missing or changed')
    report['mobileBrowser']={key:mobile.get(key) for key in ('status','scope','sourceInputSha256','browserVersion','checks','errors','externalRequests','landscape','portrait','jumpHeight','dpr2','performance','physicalDeviceAcceptance','limitations','screenshots','video','runner','browserDiagnostics','inputEnvironment','touchEvents','geometry','controlGeometry','landscapeToolbar','portraitToolbar','tabletToolbar','screenshotRetries')}
    report['mobileBrowser']['browserDiagnostics']=mobile.get('browserDiagnostics',[])
    report['mobileBrowser']['lookInputValidation']=mobile_look
    report['mobileBrowser'].update(report=str((mobile_folder/'acceptance.json').relative_to(ROOT)),sha256=sha(mobile_folder/'acceptance.json'))
    crowd=journey.get('crowd',{})
    require(crowd.get('spawned',0)>5 and crowd.get('expired',0)>0 and 0<crowd.get('maximumActive',0)<=5 and crowd.get('maximumSpawnedMovement',0)>.5 and crowd.get('navReadySamples',0)>0,'Original blocking cells were not observed spawning, navigating, expiring and replenishing')
    require(any(s.get('type')=='NPC_spawn' and s.get('enabled') is True and s.get('cap')==5 and s.get('interval')==1 for s in crowd.get('spawners',[])) and any(s.get('type')=='RBC_SPAWN_MeetingCells' and s.get('enabled') is False for s in crowd.get('spawners',[])),'Original active five-cell spawner or disabled alternative state was not retained')
    require({'original-rbc-blockers-continuously-spawn-move-expire-and-replenish','original-five-cell-cap-and-disabled-alternative-retained'}.issubset(set(journey.get('checks',[]))),'Crowd proof was not joined to the accepted complete original route')
    report['gameplayCrowd']=crowd
    report['journey']['talkApproaches']=journey.get('talkApproaches',[])
    ui_reports={}
    for name in ('native-scene','dynamic-prefabs'):
        path=OUT/'UiRedesign'/('playmode-'+name+'-coverage.json');ui=load(path)
        require(ui.get('status')=='passed' and bool(ui.get('nativeGraphics')),'Complete native/dynamic UI coverage did not pass: '+name)
        ui_reports[name]={'report':str(path.relative_to(ROOT)),'sha256':sha(path),'graphics':len(ui.get('nativeGraphics',[])),'buttons':ui.get('checkedButtons'),'colliders':ui.get('checkedColliders'),'selectedMinigameGraphicsProtectedFromGlobalTheme':sum(1 for row in ui.get('nativeGraphics',[]) if row.get('originalMinigameAssetsPreserved'))}
    case_names=[node.attrib.get('name','') for node in test_xml.iter('test-case')]
    require('ReloadingTheThemedSceneRetainsTheImportedFontAtlasAndMaterial' in case_names,'Theme font ownership reload regression was not executed')
    require({'OriginalSceneTemplateSpawnsMovesCapsAndReplenishesWithoutGrowingSceneCaches','ActualBlockingCellContactPushesTheOriginalPlayerWithoutLockingInput','OriginalBurstUsesItsAuthoredSpeedAndExpiresAfterFiveSeconds','FarObjectiveCannotOfferTalkButNearTriggerAndAuthoredSignalsStillWork'}.issubset(set(case_names)),'Source crowd physics/lifecycle or actual nearby-talk regression cases were not executed')
    art_file=OUT/'GeneratedArt/assets.json';generated_art=load(art_file);art_rows=generated_art.get('assets',[])
    require(generated_art.get('generationMode')=='built-in image_gen' and len(art_rows)==7,'Seven approved built-in generated assets are required')
    for row in art_rows:
        asset=safe(ROOT,row.get('path',''))
        require(asset and str(row['path']).startswith('Assets/Resources/MeetingCellsArt/') and sha(asset)==row.get('sha256'),'Generated minigame asset changed: '+str(row.get('name')))
    require({'ThreeOriginalOptionGroupsAndReferencesAreRestored','ApproachingShowsHighlightConnectorAndOptionsAndLeavingRestoresMaterials','OriginalObserveAndTalkButtonsEnterTheirSourceBlocksAndSuppressSelection','DeliveryTargetsHaveTheRequestedWiderGapAndMatchingHitboxCenters','SpriteAnimationAndImpactNeverMoveOrResizeTheNativePhysicsBody','CleanStreakRewardsNeverMultiplySourceScoreAndPenaltyResetsTheStreak','BankPivotAndSizeChangesKeepTheSameAnchoredReturnPosition','NativeBankCancellationKeepsEveryWordAtItsRenderedHomeAfterFirstLayout','NewTaskRowsKeepTheirLayoutDuringOriginalCinematicsWithoutChangingFadeAlpha'}.issubset(set(case_names)),'Restored interaction, wider delivery gap or sprite-animation regression was not executed')
    report['uiRedesign']={'scope':'Original story and native activity rules; approved generated pixel/sketch game art, notebook UI and restored native character interaction system','coverage':ui_reports,'inventory':'Reports/OriginalScene/ui-inventory.json','inventorySha256':sha(OUT/'ui-inventory.json'),'generatedArt':{'report':str(art_file.relative_to(ROOT)),'sha256':sha(art_file),'assets':art_rows},'prompts':{'report':'Reports/OriginalScene/GeneratedArt/prompt-set.json','sha256':sha(OUT/'GeneratedArt/prompt-set.json')}}
    browsers={}
    for engine in ('chromium','firefox'):
        directory=OUT/'ReleaseBrowser'/engine;browser=load(directory/'acceptance.json');review_file=directory/'visual-audit.json';review=load(review_file) if review_file.is_file() else {}
        matches=bool(browser.get('buildHashes')) and all((p:=safe(ROOT/'Builds/WebGLOriginalRelease',name)) and sha(p)==digest for name,digest in browser.get('buildHashes',{}).items())
        require(browser.get('buildHashes')==build_outputs['release'],engine+' smoke must bind every current release artifact')
        require(matches and browser.get('status')=='smoke-passed-awaiting-visual-review' and not browser.get('errors') and not browser.get('unexpectedRequestFailures') and not browser.get('externalRequests'),engine+' current release smoke did not pass')
        require(review.get('status')=='passed' and review.get('acceptanceSha256')==sha(directory/'acceptance.json') and len(review.get('screenshots',{}))>=3 and all(sha(directory/name)==digest for name,digest in review.get('screenshots',{}).items()),engine+' current release visual review is missing')
        browsers[engine]={key:browser.get(key) for key in ('status','browserVersion','startupSeconds','checks','errors','warnings','failedRequests','recoveredRequests','nonfatalLoaderCancellations','unexpectedRequestFailures','networkStatus','finishedUtc')}
        browsers[engine].update(visualReview=review,currentBuildHashMatch=matches)
    report['releaseBrowsers']=browsers
    try:report['releaseMobile']=validate_release_mobile(ROOT/args.release_mobile,current,build_outputs['release'])
    except (OSError,ValueError,TypeError,KeyError,AttributeError) as error:
        require(False,'Current production phone acceptance failed: '+str(error));report['releaseMobile']={'status':'failed','failure':str(error)}
    security=load(OUT/'player-security.json');require(security.get('status')=='passed' and not security.get('filesWithCredentialMatches'),'Original provider secrets were not proven excluded')
    report['security']=security
    helper=load(OUT/'package-helper-tests.json');require(helper.get('status')=='passed' and all(sha(ROOT/name)==digest for name,digest in helper.get('sourceHashes',{}).items()),'Standalone package server checks are stale');report['packageHelper']=helper

    report['limitations']=['The retained multiplayer chat launcher has no local chat panel in the isolated module; its external multiplayer integration is not accepted.','Preserves original story data and native gameplay with approved generated minigame art; this is not acceptance of every optional/alternate branch or every external service.','The recorded run reaches the original MeetingCells-to-Heart handoff, not a playable external Heart scene.','Live same-origin, signed-in PING outfit synchronization is not verified; canonical outfit and account-boundary tests are separate.','Peer-agent service status is recorded above. An unavailable/rate-limited service follows the original fallback; no local invented grading is used.','Original White win progression also has a separate physics-focused Unity test. That fixture is not evidence of a browser-input win unless this run actually records one.','The full route is hybrid: keyboard/mouse world navigation and original timed activities, actual native joystick touch/end/cancel checks, then phone worksheet touch and browser-keyboard typing. This is not a pure-touch full journey or physical soft-keyboard acceptance.','Mobile touch input, portrait/landscape layouts and high-DPR targets are checked in Chromium emulation; physical devices, Safari, heat/battery behavior, physical gamepads and human difficulty review remain outside this acceptance scope.','Warnings, narrowly recognized touchcancel browser interventions and known Chromium loader cancellations remain visible in the reports. They are not erased or called successful network requests.']
    report['failures']=failures;report['status']='passed-original-scene-with-declared-limitations' if not failures else 'failed'
    (OUT/'delivery-audit.json').write_text(json.dumps(report,indent=2,ensure_ascii=False))
    print(json.dumps({k:report[k] for k in ('status','auditedUtc','sourceInputSha256','fidelity','failures')},indent=2))
    print('Journey:',report['journey']['status'],'Oxygen:',report['journey']['oxygen'],'White:',report['journey']['white'],'Peer:',peer)
    if failures:raise SystemExit(1)
if __name__=='__main__':main()
