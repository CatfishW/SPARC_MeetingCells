"""Package only the currently audited ORIGINAL-scene release, safe server helpers and explicit evidence.
Private source manifests, secrets, legacy code, raw fonts and the rejected expedition are never included.
"""
from pathlib import Path
from datetime import datetime, timezone
import argparse
import base64
import gzip
import hashlib
import html
import json
import re
import shutil
import zipfile
from build_webgl_verified import input_manifest, fingerprint
from audit_original_delivery import validate_release_mobile, validate_journey_visual, validate_native_keyboard_dom, validate_manual_replay

ROOT=Path(__file__).resolve().parents[1]
REPORTS=ROOT/'Reports/OriginalScene'
OUT=ROOT/'Deliverables/OriginalScene'

def sha(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):h.update(block)
    return h.hexdigest()
def load(path):return json.loads(path.read_text())
def ensure_clean(data,patterns,label):
    if any(p in data for p in patterns):raise RuntimeError('Private source credential detected in '+label)
def checked_file(directory,name,digest,label):
    if not isinstance(name,str) or not name or Path(name).is_absolute() or '..' in Path(name).parts:raise RuntimeError('Unsafe '+label+' path')
    path=directory/name
    if path.is_symlink() or not path.is_file() or directory.resolve() not in path.resolve().parents or not re.fullmatch(r'[0-9a-f]{64}',str(digest or '')) or sha(path)!=digest:
        raise RuntimeError('Missing, changed or unsafe '+label+': '+str(name))
    return path
def figure_html(image,caption,mobile=False):
    data=image.read_bytes()
    portrait=data.startswith(b'\x89PNG\r\n\x1a\n') and len(data)>=24 and int.from_bytes(data[20:24],'big')>int.from_bytes(data[16:20],'big')
    css=' class="mobile-figure"' if mobile or portrait else ''
    return '<figure'+css+'><img alt="'+html.escape(caption,quote=True)+'" src="data:image/png;base64,'+base64.b64encode(data).decode()+'"><figcaption>'+html.escape(caption)+'</figcaption></figure>'
def accepted_bundle(record,family,label):
    if not isinstance(record,dict) or not record.get('report'):raise RuntimeError('Missing accepted '+label+' evidence')
    report=checked_file(ROOT,record['report'],record.get('sha256'),label+' report')
    if (REPORTS/family).resolve() not in report.resolve().parents:raise RuntimeError(label+' report is outside its accepted evidence family')
    directory=report.parent;assets=[('acceptance.json',report,record['sha256'])]
    screenshots=record.get('screenshots',[])
    if not screenshots:raise RuntimeError('Missing '+label+' screenshots')
    for item in screenshots:assets.append((item['name'],checked_file(directory,item['name'],item.get('sha256'),label+' screenshot'),item['sha256']))
    video=record.get('video',{})
    if not video.get('path'):raise RuntimeError('Missing '+label+' recording')
    assets.append((video['path'],checked_file(directory,video['path'],video.get('sha256'),label+' recording'),video['sha256']))
    return directory,assets
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--stage-only',action='store_true');parser.add_argument('--release-mobile',required=True,help='Exact production phone run directory already accepted by the delivery audit');args=parser.parse_args()
    audit=load(REPORTS/'delivery-audit.json')
    if audit['status']!='passed-original-scene-with-declared-limitations' or audit['failures']:raise RuntimeError('Current original-scene audit is not passing')
    if fingerprint(input_manifest())!=audit['sourceInputSha256']:raise RuntimeError('Unity source changed after the delivery audit')
    # Validate every new evidence dependency before creating or replacing deliverables.
    keyboard_dom=validate_native_keyboard_dom()
    if keyboard_dom!=audit.get('nativeMobileKeyboardDom'):raise RuntimeError('Separate native keyboard DOM evidence differs from the accepted audit')
    keyboard_dom_assets=[('contract.json',checked_file(ROOT,keyboard_dom['report'],keyboard_dom['sha256'],'native keyboard DOM contract'),keyboard_dom['sha256']),('Tools/verify_native_mobile_keyboard_dom.cjs',checked_file(ROOT,keyboard_dom['fixture'],keyboard_dom['fixtureSha256'],'native keyboard DOM fixture'),keyboard_dom['fixtureSha256']),('Assets/WebGLTemplates/SPARCOriginal/index.html',checked_file(ROOT,keyboard_dom['inputHtml'],keyboard_dom['inputHtmlSha256'],'native keyboard HTML source'),keyboard_dom['inputHtmlSha256'])]
    accepted=load(REPORTS/'verified-webgl-release.json');build=ROOT/'Builds/WebGLOriginalRelease'
    if accepted.get('status')!='passed' or accepted.get('inputSha256')!=accepted.get('inputSha256After') or accepted.get('inputSha256')!=audit['sourceInputSha256'] or accepted.get('buildSummary',{}).get('errors')!=0:raise RuntimeError('Release provenance does not match the accepted source')
    build_hashes={name:item['sha256'] for name,item in accepted.get('outputs',{}).items()}
    actual_hashes={str(path.relative_to(build)):sha(path) for path in build.rglob('*') if path.is_file()}
    if len(build_hashes)!=8 or actual_hashes!=build_hashes:raise RuntimeError('Current production artifacts differ from the audited eight-file release')
    release_mobile=audit.get('releaseMobile') or {}
    release_mobile_report=checked_file(ROOT,release_mobile.get('report',''),release_mobile.get('sha256'),'release-mobile report')
    release_mobile_directory=release_mobile_report.parent
    if (ROOT/args.release_mobile).resolve()!=release_mobile_directory.resolve():raise RuntimeError('Requested production phone run differs from the accepted audit')
    if validate_release_mobile(release_mobile_directory,audit['sourceInputSha256'],build_hashes)!=release_mobile:raise RuntimeError('Production phone evidence differs from the accepted audit')
    release_mobile_assets=[('acceptance.json',release_mobile_report,release_mobile['sha256'])]
    for item in release_mobile['screenshots']:release_mobile_assets.append((item['name'],checked_file(release_mobile_directory,item['name'],item['sha256'],'production phone screenshot'),item['sha256']))
    release_runner=release_mobile['runner'];release_mobile_assets.append((release_runner['path'],checked_file(release_mobile_directory,release_runner['path'],release_runner['sha256'],'production phone runner'),release_runner['sha256']))
    release_review=release_mobile['visualReview'];release_mobile_assets.append(('visual-audit.json',checked_file(ROOT,release_review['report'],release_review['sha256'],'production phone visual review'),release_review['sha256']))
    mobile=audit.get('mobileBrowser');mobile_directory,mobile_assets=accepted_bundle(mobile,'MobileBrowser','mobile touch')
    mobile_raw=load(mobile_directory/'acceptance.json')
    if mobile_raw.get('status')!='passed-mobile-emulation' or mobile_raw.get('sourceInputSha256')!=audit['sourceInputSha256']:raise RuntimeError('Mobile acceptance does not match this audited source')
    gesture_figures={}
    for orientation in ('landscape','portrait'):
        direction=mobile_raw.get(orientation,{}).get('direction')
        if direction not in ('forward','right','left','back'):raise RuntimeError('Missing accepted mobile gesture direction: '+orientation)
        gesture_figures[orientation+'-'+direction+'-gesture-result.png']=orientation.capitalize()+' phone emulation after the accepted '+direction+' movement/look gesture. This screenshot follows all-up; the recording and held-pointer state samples prove simultaneous multi-touch.'
    mobile_figures=set(gesture_figures)|{'portrait-settings.png','tablet-hud.png','dpr2-portrait-hud.png'}
    if not mobile_figures.issubset({s['name'] for s in mobile['screenshots']}):raise RuntimeError('Accepted mobile orientation/tablet/DPR2 figures are incomplete')
    runner=mobile.get('runner',{});runner_path=checked_file(mobile_directory,runner.get('path',''),runner.get('sha256'),'mobile runner');mobile_assets.append((runner['path'],runner_path,runner['sha256']))
    mobile_review=mobile_directory/'visual-review.json'
    if mobile_review.exists():
        if mobile_review.is_symlink() or not mobile_review.is_file():raise RuntimeError('Unsafe mobile visual review')
        review=load(mobile_review);source_images={item['name']:item['sha256'] for item in mobile['screenshots']}
        if review.get('status')!='passed' or review.get('acceptanceSha256')!=mobile['sha256'] or review.get('verifiedInputSha256')!=audit['sourceInputSha256'] or review.get('executedSourceSha256')!=runner['sha256']:raise RuntimeError('Mobile visual review is not bound to accepted source/report')
        if not source_images or review.get('screenshots')!=source_images:raise RuntimeError('Mobile visual review must bind all accepted screenshot hashes')
        for name,digest in review['screenshots'].items():checked_file(mobile_directory,name,digest,'reviewed mobile screenshot')
        mobile_assets.append(('visual-review.json',mobile_review,sha(mobile_review)))
    controls=audit['browserControls'];controls_directory,controls_assets=accepted_bundle(controls,'ControlsBrowser','desktop controls')
    controls_provenance=controls.get('runnerProvenance',{});provenance_file=checked_file(ROOT,controls_provenance.get('report',''),controls_provenance.get('sha256'),'controls provenance')
    if provenance_file.parent.resolve()!=controls_directory.resolve():raise RuntimeError('Controls provenance is outside the accepted run')
    controls_assets.append(('runner-provenance.json',provenance_file,controls_provenance['sha256']))
    executed=Path(controls_provenance.get('exactExecutedSourcePath',''))
    if executed.parent.resolve()!=controls_directory.resolve():raise RuntimeError('Executed controls source is outside the accepted run')
    controls_assets.append((executed.name,checked_file(controls_directory,executed.name,controls_provenance.get('sourceSha256'),'executed controls source'),controls_provenance['sourceSha256']))
    controls_review=controls_directory/'visual-review.json'
    if controls_review.exists():
        if controls_review.is_symlink() or not controls_review.is_file():raise RuntimeError('Unsafe desktop visual review')
        review=load(controls_review);source_images={item['name']:item['sha256'] for item in controls['screenshots']}
        if review.get('status')!='passed' or review.get('acceptanceSha256')!=controls['sha256'] or review.get('verifiedInputSha256')!=audit['sourceInputSha256'] or review.get('executedSourceSha256')!=controls_provenance['sourceSha256']:raise RuntimeError('Desktop visual review is not bound to accepted source/report')
        if not review.get('screenshots') or any(source_images.get(name)!=digest for name,digest in review['screenshots'].items()):raise RuntimeError('Desktop visual review images are not in the accepted report')
        for name,digest in review['screenshots'].items():checked_file(controls_directory,name,digest,'reviewed desktop screenshot')
        controls_assets.append(('visual-review.json',controls_review,sha(controls_review)))
    journey=audit['journey'];directory,journey_assets=accepted_bundle(journey,'BrowserJourney','original hybrid journey')
    development=load(REPORTS/'verified-webgl-build.json');development_folder=ROOT/'Builds/WebGLOriginal';development_hashes={name:item['sha256'] for name,item in development.get('outputs',{}).items()}
    if development.get('status')!='passed' or development.get('inputSha256')!=development.get('inputSha256After') or development.get('inputSha256')!=audit['sourceInputSha256'] or len(development_hashes)!=8:raise RuntimeError('Development provenance does not match the audited full journey')
    if {str(path.relative_to(development_folder)):sha(path) for path in development_folder.rglob('*') if path.is_file()}!=development_hashes:raise RuntimeError('Current development artifacts differ from the reviewed full journey')
    journey_review=validate_journey_visual(directory,audit['sourceInputSha256'],development_hashes)
    if journey_review!=journey.get('visualReview'):raise RuntimeError('Full-journey visual review differs from the accepted audit')
    if validate_manual_replay(load(directory/'acceptance.json'))!=journey.get('manualReplay'):raise RuntimeError('Genuine manual Replay differs from the accepted audit')
    journey_assets.append(('visual-review.json',checked_file(ROOT,journey_review['report'],journey_review['sha256'],'full-journey visual review'),journey_review['sha256']))
    if not journey.get('mobileEvidenceValidated'):raise RuntimeError('Hybrid native minigame and worksheet touch evidence was not accepted')
    hybrid_figures={'mobile-oxygen-native-stick.png','mobile-white-native-stick.png','mobile-workbook-01-filled.png','mobile-workbook-04-filled.png','mobile-workbook-13-filled.png','manual-replay-fresh-start.png'}
    if not hybrid_figures.issubset({s['name'] for s in journey['screenshots']}):raise RuntimeError('Accepted hybrid activity/worksheet figures are incomplete')
    journey_runner=journey.get('runner',{})
    if journey_runner.get('sourceInputSha256')!=audit['sourceInputSha256'] or not journey_runner.get('files'):raise RuntimeError('Hybrid journey runner provenance is absent or stale')
    for source,item in journey_runner['files'].items():
        if not source.startswith('Tools/browser/') or not source.endswith('.cjs'):raise RuntimeError('Unexpected hybrid runner source: '+source)
        checked_file(ROOT,source,item.get('sha256'),'current hybrid runner source')
        journey_assets.append((item['path'],checked_file(directory,item['path'],item.get('sha256'),'executed hybrid runner source'),item['sha256']))
    ui_directory=REPORTS/'UiVisualAudit';ui_manifest=load(ui_directory/'gallery-manifest.json');native_review=load(ui_directory/'native-review.json')
    if ui_manifest.get('status')!='passed-scoped-ui-visual-audit' or ui_manifest.get('sourceInputSha256')!=audit['sourceInputSha256']:raise RuntimeError('UI gallery lacks a current visual review')
    if native_review.get('status')!='passed-native-layout-review' or native_review.get('sourceInputSha256')!=audit['sourceInputSha256'] or native_review.get('nativeTestXmlSha256')!=ui_manifest.get('nativeTestXmlSha256'):raise RuntimeError('Native UI review is stale')
    ui_images={row['path']:row['sha256'] for row in ui_manifest['images']}
    if len(ui_images)!=len(ui_manifest['images']) or not native_review.get('images') or any(ui_images.get(name)!=digest for name,digest in native_review['images'].items()):raise RuntimeError('UI gallery does not contain its reviewed native images')
    for name,digest in ui_images.items():checked_file(ROOT,name,digest,'reviewed UI image')
    ui_review=ui_manifest.get('review',{})
    if ui_review.get('nativeReviewSha256')!=sha(ui_directory/'native-review.json') or not ui_review.get('actualBrowserReviews'):raise RuntimeError('UI gallery review provenance is absent')
    for name,digest in ui_review['actualBrowserReviews'].items():checked_file(ROOT,name,digest,'UI browser review')
    ui_html=ui_directory/'index.html'
    if ui_html.is_symlink() or not ui_html.is_file() or ui_html.read_text().count('data:image/png;base64,')!=len(ui_images):raise RuntimeError('Self-contained UI gallery images are incomplete')
    mobile_provenance={'scope':'Chromium mobile emulation with genuine browser touch events; not physical-device acceptance','sourceInputSha256':audit['sourceInputSha256'],'acceptance':{'path':'acceptance.json','sha256':mobile['sha256']},'runner':runner,'buildHashes':mobile_raw.get('buildHashes',{}),'inputEnvironment':mobile_raw.get('inputEnvironment'),'physicalDeviceAcceptance':mobile.get('physicalDeviceAcceptance',False),'limitations':mobile.get('limitations',[])}
    video_evidence_path=OUT/'runtime-video-evidence.json'
    if video_evidence_path.is_symlink() or not video_evidence_path.is_file():raise RuntimeError('Missing current runtime-video-evidence.json')
    video_evidence=load(video_evidence_path)
    if video_evidence.get('status')!='passed' or video_evidence.get('sourceInputSha256')!=audit['sourceInputSha256']:raise RuntimeError('Recording encodings do not match this audited source')
    recordings=video_evidence.get('recordings',[]);accepted_recordings={'Runtime':journey,'Controls':controls,'Mobile':mobile};mp4_artifacts=[]
    if len(recordings)!=3 or {r.get('label') for r in recordings}!=set(accepted_recordings):raise RuntimeError('Runtime, Controls and Mobile recording encodings are all required')
    for item in recordings:
        accepted_record=accepted_recordings[item['label']];acceptance=checked_file(ROOT,item.get('acceptanceReport',''),item.get('acceptanceSha256'),'recording acceptance')
        if item['acceptanceReport']!=accepted_record['report'] or item['acceptanceSha256']!=accepted_record['sha256']:raise RuntimeError('Recording acceptance is stale: '+item['label'])
        source=checked_file(ROOT,item.get('source',''),item.get('sourceSha256'),'recording source')
        if source.resolve()!=(acceptance.parent/accepted_record['video']['path']).resolve() or item['sourceSha256']!=accepted_record['video']['sha256']:raise RuntimeError('Recording source is not its accepted browser video')
        output=checked_file(ROOT,item.get('output',''),item.get('outputSha256'),'recording MP4')
        if output.parent.resolve()!=OUT.resolve() or output.suffix.lower()!='.mp4' or output.stat().st_size!=item.get('bytes'):raise RuntimeError('Unsafe or changed MP4 delivery artifact')
        probe=item.get('probe',{});source_probe=item.get('sourceProbe',{})
        if not any(s.get('codec_type')=='video' and s.get('width',0)>0 and s.get('height',0)>0 for s in probe.get('streams',[])):raise RuntimeError('Missing encoded video probe: '+item['label'])
        source_duration=float(source_probe.get('format',{}).get('duration',0));output_duration=float(probe.get('format',{}).get('duration',0))
        if source_duration<=0 or output_duration<=0 or abs(output_duration-source_duration)>.2:raise RuntimeError('Recording encoding changed source duration')
        mp4_artifacts.append(output)
    OUT.mkdir(parents=True,exist_ok=True);stage=OUT/'release-package';stage.mkdir(exist_ok=True)
    inventory=load(REPORTS/'manifest.json');secrets=[]
    for record in inventory['objects']:
        for key,value in record.get('data',{}).items():
            if re.sub('[^a-z0-9]','',key.lower()).endswith(('apikey','accesstoken','refreshtoken','password','secret','authorization')) and isinstance(value,str) and len(value.strip())>=16:secrets.append(value.strip())
    patterns=list({p for value in secrets for p in (value.encode(),value.encode('utf-16-le'))})
    ui_output=OUT/'SPARC_MeetingCells_Original_UI_Audit.html';ui_data=ui_html.read_bytes();ensure_clean(ui_data,patterns,'UI gallery');ui_output.write_bytes(ui_data)
    original_agent=next(record['data'] for record in inventory['objects'] if record['type']=='LLM_Manager_Unified')
    config_keys=('systemPrompt','temperature','topP','maxTokens','modelName','apiBaseUrls','openRouterFallbackModels')
    safe_config={key:original_agent[key] for key in config_keys if key in original_agent}
    config_data=json.dumps(safe_config,ensure_ascii=False,indent=2).encode();ensure_clean(config_data,patterns,'non-secret original agent configuration')
    if accepted.get('inputSha256')!=accepted.get('inputSha256After') or accepted.get('inputSha256')!=audit['sourceInputSha256'] or accepted.get('status')!='passed':raise RuntimeError('Release provenance does not match the accepted source')
    manifest={};expected_entries=set()
    for name,item in accepted['outputs'].items():
        source=build/name
        if source.is_symlink() or not source.is_file() or sha(source)!=item['sha256']:raise RuntimeError('Release changed after build: '+name)
        if source.suffix.lower() in {'.ttf','.otf','.woff','.woff2'}:raise RuntimeError('Raw font export is not permitted')
        destination=stage/name;destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,destination);expected_entries.add(name);manifest[name]={'sha256':item['sha256'],'bytes':source.stat().st_size,'kind':'verified-release'}
    extras={'serve-preview.py':(ROOT/'Tools/serve_original_package.py').read_bytes(),'server/serve_webgl.py':(ROOT/'Tools/serve_webgl.py').read_bytes(),'server/original_agent_proxy.py':(ROOT/'Tools/original_agent_proxy.py').read_bytes(),'server/agent-config.json':config_data}
    readme='''SPARC_MeetingCells — ORIGINAL SCENE WEBGL RELEASE

This is the rebuild using MeetingCells_NewEnv's original environment, objects,
character rigs, animation controllers, Timeline assets and unchanged story data.
It is NOT the rejected replacement expedition.

The surrounding interface uses the new paper-and-ink theme and owned icons.
The oxygen and defense games use approved generated pixel-and-sketch art,
compact HUDs, native Help and streak feedback. The notebook retains its source
questions, words, blanks and submission callbacks with redesigned controls. Lighting
uses softer shadows and neutral ambient fill. Settings provides the native
render-scale control and Back action; unavailable cross-module actions are hidden.

1. Extract the complete ZIP into its own folder.
2. Run: python3 serve-preview.py --port 8092
3. Open: http://127.0.0.1:8092/

The helper needs only Python 3 and binds your computer's loopback interface.
It serves correct gzip/WebAssembly headers and byte ranges for the original video.
Do not open index.html using file://. Press Ctrl+C to stop the local server.

WASD: move. Right-drag: look. Tab: pointer capture/release. V: first/third person.
Space: jump or advance spoken dialogue. E: interact. Original native UI buttons,
questionnaire choices and worksheet drag/drop objects remain in use.

The source peer-agent prompt is included without any private credentials.
No feedback-service request is made unless you supply an environment key:
SPARC_AGENT_API_KEY or SPARC_AGENT_OPENROUTER_KEY. Keys stay on the local server;
they are never sent into Unity. The provider may be unavailable or rate-limited.
Without a working provider the original service-unavailable fallback runs; this
package does NOT substitute local guessed grading or a different story.

Actual original worksheet: 13 pages, 14 draggable answer slots. The oxygen game
uses its 120-second timer. White uses the source scene's 250-second limit, not a
newly invented 60-second game. Read the audit for the route's exact outcomes.

LIMITS: The external Heart game is not included. Live signed-in PING appearance
synchronization still requires the original PING origin and a real-account check.
The retained multiplayer chat launcher has no local panel in this isolated module;
its external multiplayer integration is not accepted.
Physical mobile/Safari/gamepad acceptance and all alternative story branches are
not claimed. Original artwork retains its existing ownership and licenses.
The release contains no development test telemetry.
'''
    extras['README-ORIGINAL.txt']=readme.encode()
    for name,data in extras.items():
        ensure_clean(data,patterns,name);dest=stage/name;dest.parent.mkdir(parents=True,exist_ok=True);dest.write_bytes(data);expected_entries.add(name);manifest[name]={'sha256':hashlib.sha256(data).hexdigest(),'bytes':len(data),'kind':'safe-preview-support'}
    manifest_data=json.dumps({'inputSha256':audit['sourceInputSha256'],'files':manifest},indent=2).encode();(stage/'release-manifest.json').write_bytes(manifest_data);expected_entries.add('release-manifest.json')
    # Ignore Python's local cache, but fail on any other unplanned staged artifact.
    extras_present=[str(p.relative_to(stage)) for p in stage.rglob('*') if p.is_file() and '__pycache__' not in p.parts and str(p.relative_to(stage)) not in expected_entries]
    if extras_present:raise RuntimeError('Unplanned staged files: '+str(extras_present))
    result={'status':'staged','createdUtc':datetime.now(timezone.utc).isoformat(),'sourceInputSha256':audit['sourceInputSha256'],'stage':str(stage.relative_to(ROOT)),'releaseBytes':sum(item['bytes'] for item in accepted['outputs'].values()),'sourceSecretValuesExported':0,'entries':sorted(expected_entries),'files':manifest}
    if args.stage_only:
        (OUT/'package-stage.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k not in ('files','entries')},indent=2));return
    release=OUT/'SPARC_MeetingCells_Original_WebGL.zip'
    with zipfile.ZipFile(release,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
        for name in sorted(expected_entries):archive.write(stage/name,name)
    with zipfile.ZipFile(release) as archive:
        if archive.testzip() is not None:raise RuntimeError('Release ZIP CRC validation failed')
        if set(archive.namelist())!=expected_entries:raise RuntimeError('Release archive entries differ from whitelist')
        for name in sorted(expected_entries):
            data=archive.read(name)
            if name in manifest and hashlib.sha256(data).hexdigest()!=manifest[name]['sha256']:raise RuntimeError('Release archive checksum mismatch: '+name)
            if data.startswith(b'\x1f\x8b'):data=gzip.decompress(data)
            ensure_clean(data,patterns,'decoded release entry '+name)
    E=html.escape;f=audit['fidelity'];journey=audit['journey'];test_total=int(audit['unityTests']['passed'])+audit['contracts']['browser']+audit['contracts']['server'];directory=ROOT/Path(journey['report']).parent
    choices=[(REPORTS/'ReleaseBrowser/chromium/02-original-timeline.png','Release: the original introductory Timeline and original environment.'),(REPORTS/'ReleaseBrowser/chromium/03-original-dialogue.png','Release: CAP, the original line and the redesigned paper dialogue panel.'),(directory/'11-oxygen-activity.png','Recorded development run: original falling-oxygen objects and native activity UI.'),(directory/'28-white-activity.png','Recorded development run: original White arena, not replacement click-to-score gameplay.'),(directory/'original-ending-midpoint.png','Original Heart-transition ending during its authored closing fade/black transition. The external Heart scene and its imagery are not included.'),(directory/'final-state.png','After the full original ending, the dedicated module shows its completion card and locks gameplay input. This first-playthrough endpoint was saved before touching Play again.')]
    # Snapshot numbering can vary; select by suffix and require hash-bound evidence.
    image_hashes={str(directory/s['name']):s['sha256'] for s in journey['screenshots']}
    release_evidence=load(REPORTS/'ReleaseBrowser/chromium/acceptance.json');image_hashes.update({str(REPORTS/'ReleaseBrowser/chromium'/s['name']):s['sha256'] for s in release_evidence['screenshots']})
    figures=[]
    for image,caption in choices:
        if not image.exists() and image.parent==directory:
            suffix='oxygen-activity.png' if 'oxygen-activity' in image.name else 'white-activity.png' if 'white-activity' in image.name else image.name
            candidates=[p for p in directory.glob('*'+suffix) if str(p) in image_hashes];image=candidates[-1] if candidates else image
        if not image.is_file() or sha(image)!=image_hashes.get(str(image)):raise RuntimeError('Image does not belong to accepted current run: '+image.name)
        figures.append(figure_html(image,caption))
    for item in controls['screenshots']:
        if item['name'] not in ('02-original-third-person.png','03-original-walking-rig.png','04-original-jump.png','05-original-first-person.png','06a-objective-expanded.png','06b-objective-folded.png','06c-objective-reopened.png','07-native-settings-desktop.png','08-native-settings-960x600.png'):continue
        image=controls_directory/item['name']
        if sha(image)!=item['sha256']:raise RuntimeError('Controls screenshot changed after audit')
        captions={'02-original-third-person.png':'Third-person mode: the native camera blend and wall collision remain active; the avatar render layer is suppressed whenever the output camera is too close to its body.','04-original-jump.png':'A real jump with native world collision. Close-camera avatar culling prevents the view from exposing internal hair or body surfaces.','03-original-walking-rig.png':'Original locomotion and the redesigned main HUD in the actual browser.','05-original-first-person.png':'Original first-person camera after a real V key press.','06a-objective-expanded.png':'One paper objective card with a prominent current instruction.','06b-objective-folded.png':'A real native details click folds the context while retaining the actionable instruction.','06c-objective-reopened.png':'A second real native details click restores context without changing story or activity state.','07-native-settings-desktop.png':'Compact native Settings, opened through the actual top-right toolbar button; the objective HUD is hidden while modal.','08-native-settings-960x600.png':'The same native Settings panel at 960 by 600, before closing through its original Back control.'}
        caption='Actual browser controls test: '+captions[item['name']]
        figures.append(figure_html(image,caption))
    mobile_captions={**gesture_figures,'portrait-settings.png':'Native Settings opened by touch while held movement is cleared.','tablet-hud.png':'Tablet viewport: original HUD and reachable touch controls.','dpr2-portrait-hud.png':'DPR2 emulation: CSS hit targets checked against the actual scaled Unity backbuffer.'}
    for name,caption in mobile_captions.items():
        item=next((s for s in mobile['screenshots'] if s['name']==name),None)
        if not item:raise RuntimeError('Missing accepted mobile figure: '+name)
        image=checked_file(mobile_directory,name,item['sha256'],'mobile figure')
        figures.append(figure_html(image,caption,True))
    release_mobile_captions={'390x844-start.png':'Production release, phone portrait: the actual Start card and surrounding native UI.','390x844-intro.png':'Production release, phone portrait: the authored intro and its mobile caption.','390x844-dialogue.png':'Production release, phone portrait: source CAP dialogue and reachable Continue control.','844x390-start.png':'Production release, phone landscape: the actual Start card.','844x390-intro.png':'Production release, phone landscape: original environment and authored intro caption.','844x390-dialogue.png':'Production release, phone landscape: original dialogue inside the responsive paper panel.'}
    for item in release_mobile['screenshots']:
        image=checked_file(release_mobile_directory,item['name'],item['sha256'],'production phone figure');caption=release_mobile_captions[item['name']]
        figures.append(figure_html(image,caption,True))
    hybrid_captions={'mobile-oxygen-native-stick.png':'Hybrid route: genuine touch on the original oxygen OnScreenStick drives its original physics body.','mobile-white-native-stick.png':'Hybrid route: genuine touch controls the original White defense game.','mobile-workbook-01-filled.png':'Hybrid route: original worksheet word tiles and blanks filled through mobile touch.','mobile-workbook-04-filled.png':'The redesigned notebook in phone landscape, retaining original questions, words and source page sequence.','mobile-workbook-13-filled.png':'The final original worksheet page before its native Submit callback.','manual-replay-fresh-start.png':'After the first playthrough endpoint was recorded, a genuine touch on Play again reloaded the same scene to its idle Start card. The original endpoint evidence remains separate.'}
    for item in journey['screenshots']:
        if item['name'] not in hybrid_captions:continue
        image=checked_file(directory,item['name'],item['sha256'],'hybrid figure');caption=hybrid_captions[item['name']]
        figures.append(figure_html(image,caption,True))
    ending=audit.get('endingPlayback')
    if ending:
        same_run=ending.get('report')==journey['report']
        ending_text=('The same complete route' if same_run else 'A separate input-driven run')+' observed the full '+format(ending['duration'],'.3f')+'-second original ending without seeking; its start count remained one.'
        ending_text+=' The midpoint retains the source-authored closing fade/black transition; it does not show local Heart-game imagery. After the native ending completed, gameplay input stayed locked and a paper completion card appeared. The card remained stable without automatic replay. Only after saving that endpoint did a genuine touch on Play again reload the same scene to its idle Start card; the reset observation and screenshot are recorded separately.'
        if not same_run and (ending.get('oxygenAssisted') or ending.get('whiteAssisted')):ending_text+=' That separate run used the original activity exit/confirmation controls.'
    else:ending_text='The main route verifies the original ending begins once. Full endpoint playback is not included in this audit.'
    peer=journey['peerAgent'];white=journey['white'];white_outcome='original WBC_LOSE branch' if journey['whiteLossBranch'] else 'original WBC_WIN branch' if journey['whiteWinBranch'] else 'recorded original continuation; see JSON'
    white_evidence=('This actual browser run completed all three native levels and reached their original win branch.' if journey['whiteWinBranch'] else 'This browser run verifies the original loss continuation.')+' A separate Unity collision fixture also checks all three source levels and their original win branch.'
    note='No feedback-provider credentials were supplied to this isolated run. It exercised the original service-unavailable response and branch after three failures; no locally invented grading was used. A separate earlier live-provider check returned rate-limit/unavailable responses.' if peer.get('ServiceState')=='unavailable' else 'Peer service state: '+str(peer.get('ServiceState'))+'. See the exact report for the observed response and branch.'
    summary_rows=[('Imported original scene',str(f['nativeRecordsExact'])+' / '+str(f['nativeRecordsExpected'])+' native records intact','Source objects and transforms retained.'),('Narrative data',str(f['sourceGraphBlocks'])+' blocks / '+str(f['sourceGraphCommands'])+' commands / '+str(f['dialogueRecords'])+' Say records','Original dialogue, voice references and branch records checked.'),('Authored cinematic assets',str(f['timelineAssets'])+' Timeline assets / '+str(f['directors'])+' scene directors','Original playable assets, clip timing and bindings retained.'),('Animation assets',str(f['standaloneClips'])+' standalone clips / '+str(f['animatorControllers'])+' controllers','Original clips and native animator state records preserved.'),('Unity PlayMode',str(audit['unityTests']['passed'])+' passed / 0 failed','Includes actual original bone motion, locomotion clips, outfit rig preservation and native activity controls.'),('Browser/server contracts',str(audit['contracts']['browser'])+' JavaScript + '+str(audit['contracts']['server'])+' Python tests','Appearance isolation, pointer lifetime, original agent boundary and video byte ranges.'),('Current builds','Development + release: 0 errors, 0 warnings','Source fingerprint and artifact checksums verified after testing.')]
    crowd=audit['gameplayCrowd'];worksheet=journey['mobileWorksheet'];native_touch=journey['mobileMinigameTouch']
    summary_rows.extend([('Original blocking-cell crowd',str(crowd['spawned'])+' spawned / '+str(crowd['expired'])+' expired / '+str(crowd['maximumActive'])+' maximum active','Source spawner and native prefab assets; navigation, expiry and replenishment observed.'),('Phone/tablet touch controls',str(len(mobile['checks']))+' Chromium-emulation checks','Independent fingers, release/cancel, native Jump and Settings; utility hit regions at least 44 CSS pixels in portrait, landscape and DPR2.'),('Activity artwork and feedback','Seven generated pixel-and-sketch assets; source rules retained','Wider delivery targets, animated subjects, bounded reward effects, native Help, and all13 notebook pages.'),('Ending and manual Replay','Authored ending retained; completion card and real-touch restart verified','Closing fade is original; Heart-game imagery is outside this module. The saved first-playthrough endpoint remains separate from the reset Start state.')])
    mobile_section='<h2>Mobile input and responsive layouts</h2><p>The dedicated mobile run uses real browser touch events with an Android user agent, coarse pointer and multiple independent fingers. It checks concurrent movement/look, releasing one finger while the other stays active, touch cancellation, native Jump, Settings open/Back and high-DPR CSS sizing. Utility hit regions are at least 44 CSS pixels; the report includes the measured thumbstick and Jump sizes.</p><p>A separate mandatory production-release phone smoke checks the actual Start card, authored intro caption and first dialogue at390 ×844 and844 ×390 under Android/touch/DPR2 emulation. All six captures have a report-bound visual review; the release exposes no development telemetry. Console/network logs, canvas backbuffer measurements and browser RAF samples are retained. RAF samples do not establish Unity FPS or physical-phone performance.</p><p>The complete story recording is a <strong>hybrid route</strong>: keyboard/mouse navigation and real touches on both original minigame joysticks, followed by the native worksheet on phone viewports. Both authored OnScreenStick controls moved their original physics bodies and returned their virtual axes to zero on release/cancel. The worksheet touched all '+str(len(worksheet['pages']))+' source pages and filled '+str(worksheet['totalFilledSlots'])+' native blanks using drag and tap placement; cancellation, text entry and the original Submit callback are recorded.</p><p>Approved generated pixel-and-sketch sprites and backgrounds replace the timed-game artwork. Source questions, words, activity physics, scoring and story rules are retained. The main HUD uses a content-sized paper card, compact real completion badge and a brief decorative task-change pulse; folding details keeps its action visible. Nearby actors use the restored outline/curved connector and original Talk/Observe buttons. Utility buttons sit at the top-right, and modal Settings hides the objective until Back restores it.</p><div class="boundary"><strong>Emulation scope</strong><p>These checks are Chromium browser emulation, not physical-phone or Safari acceptance. Recorded frame samples do not certify real-device frame pacing, thermals, battery use or the operating-system soft keyboard. The hybrid route is not a pure-touch completion of every story stage.</p></div>'
    rows=''.join('<tr><th scope="row">'+E(a)+'</th><td>'+E(b)+'</td><td>'+E(c)+'</td></tr>' for a,b,c in summary_rows)
    browser_rows=''.join('<tr><td>'+E(name)+'</td><td>'+E(item['browserVersion'])+'</td><td>'+E(item['networkStatus'])+'</td><td>'+str(len(item['errors']))+' app errors / '+str(len(item['warnings']))+' logged warnings</td></tr>' for name,item in audit['releaseBrowsers'].items())
    html_doc='''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>SPARC MeetingCells — original-scene rework audit</title><style>*{box-sizing:border-box}body{margin:0;background:#f1eee6;color:#25373b;font:16px/1.65 system-ui,-apple-system,"Segoe UI",sans-serif}main{max-width:1100px;margin:auto;padding:42px 24px}header{background:#fffdf4;padding:34px;border:1px solid #5e7372;outline:1px solid #b7c0b2;outline-offset:5px;margin-bottom:38px}h1{font-size:clamp(28px,5vw,46px);line-height:1.12;margin:12px 0 18px;letter-spacing:-.025em}h2{font-size:25px;margin:36px 0 10px}.kicker{font-size:12px;font-weight:750;letter-spacing:.13em;color:#466763}.status{display:inline-block;background:#dce8df;padding:7px 13px;font-weight:700}.boundary{padding:20px 25px;background:#fff0d8;border-left:5px solid #b99248;margin:25px 0}.scroll{overflow-x:auto}table{border-collapse:collapse;width:100%;font-size:14px;background:#fffdf6}td,th{vertical-align:top;padding:14px;text-align:left;border-bottom:1px solid #d1d8ce}thead{background:#e2e9e1}pre{background:#dfe7e1;padding:20px;overflow-x:auto}code{font:13px/1.6 ui-monospace,SFMono-Regular,monospace;overflow-wrap:anywhere}figure{padding:10px;border:1px solid #c2cbbd;margin:28px 0;background:#fffdf6}img{display:block;width:100%;height:auto}.mobile-figure img{width:auto;max-width:100%;max-height:900px;margin:0 auto;object-fit:contain}figcaption{padding:10px 6px;font-size:14px;color:#465f5d}.metric{font-size:20px;font-weight:700}footer{font-size:13px;color:#536963;border-top:1px solid #b8c5b9;margin-top:35px;padding-top:22px}details{background:#e5ebe2;padding:18px;margin-top:24px}summary{cursor:pointer;font-weight:650}</style><main><header><div class="kicker">SPARC / ORIGINAL SCENE / ACTUAL RUNTIME EVIDENCE</div><h1>Original objects.<br>Original Timelines.<br>Unchanged story data.</h1><p>This audit covers the standalone rebuild of <code>MeetingCells_NewEnv.unity</code>, its current source snapshot, the tested original story route and its service boundaries.</p><span class="status">Verified original-scene build — scoped acceptance</span></header><h2>Interface and lighting improvements</h2><p>The main interface, icons, task cards, dialogue and Settings use a consistent paper-and-ink design. The oxygen and White defense games use newly generated pixel-and-sketch sprites and backgrounds, compact HUDs, expandable Help and streak feedback. The worksheet uses an opaque notebook, clear word bank, blue blanks, page progress and Back/Next controls. Source questions, native physics, scoring and timers remain. Softer shadows, neutral ambient fill and reduced bloom make the original environment and characters easier to read. Original animation clips remain in use, with verified locomotion and bone motion. The native third-person camera retains its original lens, blend and environment collision. Its obstacle checks exclude the player capsule, and a close-camera visibility guard hides only the avatar render layer while keeping its rig active.</p><h2>What is preserved and tested</h2><div class="scroll"><table><thead><tr><th>Area</th><th>Result</th><th>Meaning</th></tr></thead><tbody>'''+rows+'''</tbody></table></div><h2>What happened in the recorded browser run</h2><p>The complete route combines real mouse/keyboard navigation with genuine native minigame and worksheet touch input. Its development-only observer has no scene setters, teleportation, score mutation, timeline seeking or timer acceleration. It visited all four authored CAP waypoints and displayed '''+str(len(journey['originalDialogueIds']))+''' original dialogue records, each checked against the original source text.</p><p class="metric">Oxygen: '''+str(journey['oxygen']['Score'])+''' collected • '''+format(journey['oxygen']['Elapsed'],'.2f')+''' game seconds</p><p>The full original 120-second oxygen activity was exercised, not skipped. White ran to '''+format(white['Elapsed'],'.2f')+''' seconds with score '''+str(white['Score'])+''' on level '''+str(white['Level'])+'''; this run followed the '''+E(white_outcome)+'''. '''+E(white_evidence)+'''</p><p>The original worksheet has '''+str(journey['workbookPages'])+''' source pages. The run filled all '''+str(sum(p['totalSlots'] for p in journey['workbookInteractions']))+''' original drag/drop slots, exercised typed input and submitted once. The original Heart transition started once. '''+E(ending_text)+'''</p><div class="boundary"><strong>External services and scope</strong><p>'''+E(note)+''' Provider credentials are not present in the player or this package.</p><p>The external Heart game is not included. A real signed-in, same-origin PING outfit session is not verified by the 120-preset rig tests. The retained multiplayer chat launcher has no local panel in this isolated module; its external integration is not accepted. Physical mobile devices, Safari, physical gamepads, every optional story branch and human difficulty/cinematography review remain separate acceptance work.</p></div><h2>Release browser checks</h2><div class="scroll"><table><thead><tr><th>Engine</th><th>Version</th><th>Network observation</th><th>Runtime logs</th></tr></thead><tbody>'''+browser_rows+'''</tbody></table></div><p>The release contains no development observation interface. Both engines exercised its actual Start button, original intro, dialogue input and viewport resizing. The final original scene/subtitles and dialogue at 1440 × 900 and 960 × 600 were visually checked. Chromium's narrowly identified loader cancellations are retained rather than called successful downloads.</p>'''+mobile_section+'''<h2>Run this release</h2><pre><code>Extract SPARC_MeetingCells_Original_WebGL.zip
cd /path/to/extracted/folder
python3 serve-preview.py --port 8092
# Open http://127.0.0.1:8092/</code></pre><p>Project checkout: <code>/Users/zlad/Development/SPARC_MeetingCells</code><br>Playable Unity scene: <code>Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity</code><br>Local preview: <code>python3 Tools/serve_webgl.py --port 8092</code>. The default is now the original-scene release. <code>--development</code> explicitly selects its test build.</p><h2>Actual captures from the tested builds</h2>'''+''.join(figures)+'''<h2>Runtime recording</h2><p><code>SPARC_MeetingCells_Original_Runtime.mp4</code> is a transcode of the complete input-driven browser screen recording: no cuts, generated frames or speed changes. It is silent because the browser recorder captures frames, not the game's audio output. The original voice clips remain in the game; this recording is not an audio acceptance claim.</p><p>The separate desktop controls recording verifies keyboard movement, original locomotion, jumping, both perspectives, right-drag look, objective folding and native Settings. The Evidence ZIP also includes the unedited mobile touch recording and hybrid journey recording, exact executed runner sources, hash-bound reports and original screenshots. These browser recordings are silent; they do not verify audio output.</p><details><summary>Integrity and reproducibility</summary><p>Frozen Unity inputs: <code>'''+E(audit['sourceInputSha256'])+'''</code></p><p>Original source-scene checksum: <code>'''+E(f['sourceSceneSha256'])+'''</code></p><p>New first-party implementation files: '''+str(f['newImplementationFiles'])+'''. Exact old C# copies: '''+str(len(f['exactLegacyCSharpCopies']))+'''. Tests covered in this audit: '''+str(test_total)+'''. Build files and supplied images were checked against accepted hashes before packaging. The original project/open editor were not changed.</p></details><footer>Audit generated '''+E(audit['auditedUtc'])+'''. The scope and integration limits are stated above. No public deployment or repository push was performed. Original artwork retains its existing ownership and license conditions.</footer></main></html>'''
    html_path=OUT/'SPARC_MeetingCells_Original_Audit.html';html_path.write_text(html_doc,encoding='utf-8');ensure_clean(html_doc.encode(),patterns,'illustrated audit')
    evidence_names=['delivery-audit.json','fidelity-validation.json','verified-tests.json','verified-contracts.json','verified-webgl-build.json','verified-webgl-release.json','original-complete-tests.xml','browser-contracts.tap','python-contracts.log','player-security.json']
    evidence=OUT/'SPARC_MeetingCells_Original_Evidence.zip'
    with zipfile.ZipFile(evidence,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
        archive.write(html_path,html_path.name)
        archive.write(ui_output,ui_output.name)
        for name in ('gallery-manifest.json','native-review.json'):
            data=(ui_directory/name).read_bytes();ensure_clean(data,patterns,'UI gallery '+name);archive.writestr('evidence/ui-visual-audit/'+name,data)
        for name,digest in native_review['images'].items():
            path=checked_file(ROOT,name,digest,'native UI image');data=path.read_bytes();ensure_clean(data,patterns,'native UI '+name);archive.writestr('evidence/ui-visual-audit/native/'+path.name,data)
        for name in ('assets.json','prompt-set.json'):
            data=(REPORTS/'GeneratedArt'/name).read_bytes();ensure_clean(data,patterns,'generated art '+name);archive.writestr('evidence/generated-art/'+name,data)
        for asset in audit['uiRedesign']['generatedArt']['assets']:
            path=checked_file(ROOT,asset['path'],asset['sha256'],'generated art asset');archive.write(path,'evidence/generated-art/png/'+path.name)
        for name in evidence_names:
            path=REPORTS/name;data=path.read_bytes();ensure_clean(data,patterns,'evidence/'+name);archive.writestr('evidence/'+name,data)
        for name in ('ui-inventory.json','ui-inventory-map.md','UiRedesign/DESIGN_SCOPE.md','UiRedesign/playmode-native-scene-coverage.json','UiRedesign/playmode-dynamic-prefabs-coverage.json'):
            data=(REPORTS/name).read_bytes();ensure_clean(data,patterns,'UI evidence/'+name);archive.writestr('evidence/'+name,data)
        for name in ('chromium','firefox'):
            for filename in ('acceptance.json','visual-audit.json'):
                path=REPORTS/'ReleaseBrowser'/name/filename;data=path.read_bytes();ensure_clean(data,patterns,'release '+name+' '+filename);archive.writestr('evidence/release-'+name+'/'+filename,data)
        path=ROOT/journey['report'];data=path.read_bytes();ensure_clean(data,patterns,'journey report');archive.writestr('evidence/original-journey.json',data)
        data=(ROOT/controls['report']).read_bytes();ensure_clean(data,patterns,'controls report');archive.writestr('evidence/original-controls.json',data)
        for filename in ('runner-provenance.json','original-controls-executed-source.cjs'):
            data=(controls_directory/filename).read_bytes();ensure_clean(data,patterns,'controls runner '+filename);archive.writestr('evidence/controls-runner/'+filename,data)
        for family,assets in (('desktop-controls',controls_assets),('mobile-browser',mobile_assets),('hybrid-journey',journey_assets),('release-mobile',release_mobile_assets),('native-mobile-keyboard-dom',keyboard_dom_assets)):
            for name,path,digest in assets:
                if sha(path)!=digest:raise RuntimeError('Accepted evidence changed during packaging: '+str(path))
                data=path.read_bytes();ensure_clean(data,patterns,family+'/'+name);archive.writestr('evidence/'+family+'/'+name,data)
        for name,provenance in (('mobile-browser',mobile_provenance),('hybrid-journey',journey_runner)):
            data=json.dumps(provenance,ensure_ascii=False,indent=2).encode();ensure_clean(data,patterns,name+' provenance');archive.writestr('evidence/'+name+'/provenance.json',data)
        data=video_evidence_path.read_bytes();ensure_clean(data,patterns,'recording encoding evidence');archive.writestr('evidence/runtime-video-evidence.json',data)
        data=(REPORTS/'package-helper-tests.json').read_bytes();ensure_clean(data,patterns,'package server report');archive.writestr('evidence/package-helper-tests.json',data)
        if ending:
            data=(ROOT/ending['report']).read_bytes();ensure_clean(data,patterns,'full ending report');archive.writestr('evidence/original-ending.json',data)
    with zipfile.ZipFile(evidence) as archive:
        if archive.testzip() is not None:raise RuntimeError('Evidence ZIP CRC verification failed')
    result.update(status='packaged-and-hash-verified',package=release.name,archiveContentChecksumsVerified=True,archiveSecretsExcluded=True,uiVisualAudit={'captures':len(ui_images),'manifestSha256':sha(ui_directory/'gallery-manifest.json'),'nativeReviewSha256':sha(ui_directory/'native-review.json')},artifacts=[{'path':str(p.relative_to(ROOT)),'bytes':p.stat().st_size,'sha256':sha(p)} for p in (release,html_path,ui_output,evidence,video_evidence_path,*mp4_artifacts)])
    (OUT/'delivery-manifest.json').write_text(json.dumps(result,indent=2));print(json.dumps({k:v for k,v in result.items() if k not in ('files','entries')},indent=2))
if __name__=='__main__':main()
