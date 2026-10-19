#!/usr/bin/env python3
"""Package only an audited current release and its evidence; never includes raw font files or secrets."""
from pathlib import Path
from datetime import datetime, timezone
import base64
import hashlib
import html
import json
import zipfile

ROOT = Path(__file__).resolve().parents[1]

def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): h.update(block)
    return h.hexdigest()

def main():
    report = json.loads((ROOT / 'Reports/delivery-validation.json').read_text())
    if report.get('status') != 'passed-with-declared-limitations' or report.get('auditFailures'): raise SystemExit('Current delivery audit is not passing.')
    out = ROOT / 'Deliverables'; out.mkdir(exist_ok=True)
    build = ROOT / 'Builds/WebGL'
    files = report['webGLFiles']
    for name, item in files.items():
        path = build / name
        if path.is_symlink() or not path.is_file() or sha(path) != item['sha256']: raise SystemExit('Release changed after audit: ' + name)
    readme = '''SPARC_MeetingCells — browser release

Extract this archive into its own folder. Serve that folder over HTTP, not file://:

    python3 -m http.server 8092 --bind 127.0.0.1

Then open http://127.0.0.1:8092/ in a browser. This basic server uses the tested
JavaScript decompression fallback. A production server can serve .unityweb files
with Content-Encoding: gzip and the decoded JavaScript/WebAssembly MIME types.

WASD move; Shift sprint; Space jump; E interact; V first/third person; J journal.
Tab explicitly captures/releases the pointer. Right-drag also looks around.
Escape opens options or can be held to skip a shot. Left-click operates the UI.

This is the release build, without development telemetry. It was tested with a
full real-input journey in a matching development build, plus separate release
smoke/visual checks in Chromium and Firefox. Read the audit for precise coverage.
The oxygen activity used its supported assisted continuation after collection;
the defense activity was won through three waves with actual browser input.

The existing PING origin is required for real saved-account outfit synchronization.
No live website was changed and no real signed-in account acceptance is claimed.
The external Heart game and old multiplayer/analytics/AI services are not bundled.
Original art retains its existing ownership and license restrictions.
'''
    release_zip = out / 'SPARC_MeetingCells_WebGL.zip'
    with zipfile.ZipFile(release_zip, 'w', compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for name in sorted(files): archive.write(build / name, name)
        archive.writestr('README-WEBGL.txt', readme)
        archive.writestr('build-manifest.json', json.dumps(files, indent=2))
    with zipfile.ZipFile(release_zip) as archive:
        if archive.testzip() is not None: raise SystemExit('WebGL archive CRC validation failed')
        for name, item in files.items():
            if hashlib.sha256(archive.read(name)).hexdigest() != item['sha256']: raise SystemExit('WebGL archive content mismatch: ' + name)
    esc = html.escape
    rows = [
        ('Unity EditMode', report['editMode']['passed'] + ' passed / ' + report['editMode']['failed'] + ' failed', 'State rules, appearance validation, cue receipts and activity models.'),
        ('Unity PlayMode', report['playMode']['passed'] + ' passed / ' + report['playMode']['failed'] + ' failed', 'Actual scene, traversal, modular avatar, UI callbacks, alternate route and cancellation.'),
        ('Browser boundary contracts', '25 passed / 0 failed', 'Origin/account isolation, stale response races, logout, size limits and pointer lifetime.'),
        ('Real-input browser journey', '12 chapters / 7 unique cues / 13 answers', 'Mouse/keyboard movement, jump, FP/TP, wardrobe, activities, quiz, reflection, workbook and reload.'),
        ('Release browser matrix', 'Chromium + Firefox + decompression fallback', 'Actual shipped bundle; startup, input, views, journal, resize, pointer behavior and error collection.'),
        ('Migration integrity', '0 missing scene scripts or materials', 'Original source-scene checksum preserved; no exact legacy C# file copies in the new project.')
    ]
    screenshot_choices = [
        ('release-chromium-metal-http-gzip','03-third-person.png','Release: the redesigned bloodstream expedition and third-person explorer.'),
        ('release-chromium-metal-http-gzip','05-first-person.png','Release: first-person view and story interaction prompt.'),
        ('release-chromium-metal-http-gzip','07-small-viewport.png','Release: the field journal at a 960 × 540 viewport.'),
        ('chromium-metal-journey','09-oxygen-activity.png','Development acceptance: the live oxygen collection board.'),
        ('chromium-metal-journey','11-defense-victory.png','Development acceptance: three defense waves completed through actual pointer input.'),
        ('chromium-metal-journey','12-workbook-page-2.png','Development acceptance: real typed workbook answers.'),
        ('chromium-metal-journey','13-complete.png','Development acceptance: the end of the twelve-chapter route.')
    ]
    figures = []
    for folder, name, caption in screenshot_choices:
        source = ROOT / 'Reports/BrowserAcceptance' / folder
        evidence = json.loads((source / 'browser-acceptance.json').read_text())
        expected = next((s['sha256'] for s in evidence['screenshots'] if s['name'] == name), None)
        image = source / name
        if not expected or sha(image) != expected: raise SystemExit('Screenshot is not part of the accepted run: ' + name)
        encoded = base64.b64encode(image.read_bytes()).decode('ascii')
        figures.append('<figure><img alt="' + esc(caption, quote=True) + '" src="data:image/png;base64,' + encoded + '"><figcaption>' + esc(caption) + '</figcaption></figure>')
    browser_rows = []
    for name, item in report['browsers'].items():
        if not item.get('testedBuildStillMatches'): continue
        if item.get('status') != 'passed' and not item.get('visualAuditMatches'): continue
        scope = 'Full route (development)' if item.get('gameplayStateVerified') else 'Release smoke + visual audit'
        errors = len(item.get('runtimeErrors', item.get('errors', [])))
        warnings = len(item.get('consoleWarnings', item.get('warnings', [])))
        browser_rows.append('<tr><td>' + esc(name) + '</td><td>' + esc(str(item.get('browserVersion', ''))) + '</td><td>' + scope + '</td><td>' + str(errors) + ' errors / ' + str(warnings) + ' logged warnings</td></tr>')
    total_bytes = sum(item['bytes'] for item in files.values())
    table_rows = ''.join('<tr><th scope="row">' + esc(a) + '</th><td>' + esc(b) + '</td><td>' + esc(c) + '</td></tr>' for a, b, c in rows)
    document = '''<!doctype html><html lang="en"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>SPARC MeetingCells — WebGL audit</title>
<style>:root{color-scheme:light}*{box-sizing:border-box}body{margin:0;background:#f4f1e8;color:#213640;font:16px/1.65 system-ui,-apple-system,BlinkMacSystemFont,"Segoe UI",sans-serif}main{max-width:1120px;margin:auto;padding:56px 28px}header{border:1px solid #657878;outline:1px solid #b7b8ac;outline-offset:5px;padding:34px;background:#fffdf4;margin-bottom:40px}.eyebrow{letter-spacing:.14em;font-size:12px;font-weight:750;color:#477276}h1{font-size:clamp(30px,5vw,56px);line-height:1.1;letter-spacing:-.03em;margin:14px 0}h2{margin-top:40px;font-size:25px}h3{font-size:18px}p{max-width:88ch}.status{display:inline-block;background:#dcebdd;color:#294f3b;padding:7px 14px;font-weight:700;margin-top:12px}.stats{display:flex;gap:14px;flex-wrap:wrap;margin-top:24px}.stats div{background:#e9eee8;padding:14px 18px;min-width:170px;flex:1}.stats strong{display:block;font-size:26px}table{width:100%;border-collapse:collapse;background:#fffdf6;margin:20px 0;font-size:14px}td,th{text-align:left;vertical-align:top;border-bottom:1px solid #d5d9cc;padding:14px}thead{background:#e0e9e4}.scroll{overflow-x:auto}.limit{border-left:5px solid #c69e4d;background:#fcf0d8;padding:18px 24px;margin:24px 0}figure{margin:30px 0;background:#fffdf6;border:1px solid #bec7ba;padding:10px}img{display:block;width:100%;height:auto}figcaption{padding:12px 8px;font-size:14px;color:#486065}code{font:13px/1.6 ui-monospace,SFMono-Regular,monospace;overflow-wrap:anywhere}pre{background:#e5ebe4;padding:20px;overflow:auto}details{margin:20px 0;padding:18px;background:#e9eee6}summary{cursor:pointer;font-weight:650}footer{margin-top:48px;border-top:1px solid #b9c5b7;padding-top:20px;color:#486065;font-size:13px}a{color:#245b63}</style><main>
<header><div class="eyebrow">SPARC / MEETING CELLS / WEBGL ACCEPTANCE</div><h1>Small world.<br>Verified journey.</h1><p>A separate Unity rebuild with a newly authored single-player story system, sketch interface, explorer controls, browser outfit adapter and camera choreography.</p><div class="status">PASS — WITH DECLARED LIMITATIONS</div><div class="stats"><div><strong>12</strong>story chapters traversed</div><div><strong>7</strong>unique cutscene receipts</div><div><strong>78</strong>Unity + browser contract tests</div><div><strong>''' + f'{total_bytes / 2**20:.1f}' + ''' MiB</strong>release build files</div></div></header>
<h2>What was actually tested</h2><p>The complete journey used browser mouse and keyboard input. Read-only state observations in the development build supplied numerical checks; there were no teleport, timer, chapter or debug-command shortcuts. The shipped release was then tested separately, including image inspection.</p><div class="scroll"><table><thead><tr><th>Layer</th><th>Result</th><th>Scope</th></tr></thead><tbody>''' + table_rows + '''</tbody></table></div>
<h2>Defects corrected during the audit</h2><p>Flat cylinder platforms had oversized capsule colliders; they now use matching beveled collision meshes. The controller no longer drops tiny movement steps at high refresh rates. Browser pointer capture is explicit, trusted-event driven and cancellation-safe; desktop browsers no longer show synthetic touch controls. The wardrobe pedestal is render-only, avoiding an IL2CPP-stripped collider dependency. Current story characters take priority over nearby optional notes. Journal titles are readable rather than internal block identifiers.</p>
<div class="limit"><h3>Important boundaries</h3><p>The oxygen activity was exercised with real collection and pause/resume, then its supported assisted continuation. Defense was won through all three waves. A complete two-minute oxygen run, a physical mobile/gamepad matrix and human difficulty review are not claimed.</p><p>Authenticated outfit inheritance still needs a real signed-in, same-origin PING acceptance run. No live website, account or service was changed. The external Heart game, old multiplayer, MQTT analytics and AI/peer-agent services are not bundled. All source narrative data are retained, but this is not one-to-one parity with every legacy event graph.</p></div>
<h2>Browser evidence</h2><div class="scroll"><table><thead><tr><th>Run</th><th>Browser</th><th>Coverage</th><th>Console</th></tr></thead><tbody>''' + ''.join(browser_rows) + '''</tbody></table></div><p>Warnings are retained, not erased. Unity build summaries record ''' + esc(str(report['webGLBuild'].get('warnings','?'))) + ''' warning(s) and ''' + esc(str(report['webGLBuild'].get('errors','?'))) + ''' errors. Internal render-pipeline/driver warnings in the browser are distinct from application exceptions; inspect the machine-readable reports for the exact messages. Startup timings are local-run observations, not general performance claims.</p>
<h2>Run the release</h2><pre><code>Extract SPARC_MeetingCells_WebGL.zip into its own folder.
cd /path/to/extracted/folder
python3 -m http.server 8092 --bind 127.0.0.1
# Open http://127.0.0.1:8092/</code></pre><p>For the project checkout, use <code>python3 Tools/serve_webgl.py --port 8092</code>. Do not open the game's HTML through <code>file://</code>. Tab captures/releases the pointer; right-drag also looks around. The release contains no development telemetry.</p>
<h2>Captured from the tested builds</h2>''' + ''.join(figures) + '''
<details><summary>Integrity and reproducibility</summary><p>Unity version: <code>''' + esc(report['scene']['unityVersion']) + '''</code></p><p>Input fingerprint: <code>''' + esc(report['currentInputSha256']) + '''</code></p><p>Original source-scene checksum still matches: <strong>''' + str(report['sourceSceneMatchesInitialAudit']) + '''</strong>. Exact legacy C# copies: <strong>''' + str(len(report['exactLegacySourceCopies'])) + '''</strong>. New C# files: <strong>''' + str(report['csharpFiles']) + '''</strong>.</p><p>The release and development builds share that frozen source fingerprint. Build artifact hashes, test XML hashes, browser observations and screenshot review hashes are included in the companion JSON evidence. Re-run with <code>python3 Tools/verify_webgl.py</code>, inspect the resulting images, then run <code>python3 Tools/validate_delivery.py</code>.</p></details>
<footer>Audited ''' + esc(report['auditedUtc']) + '''. Generated from verified project artifacts. Original artwork retains its existing ownership and licensing. The full project remains at <code>/Users/zlad/Development/SPARC_MeetingCells</code>.</footer></main></html>'''
    html_path = out / 'SPARC_MeetingCells_Audit.html'; html_path.write_text(document)
    evidence_files = ['Reports/delivery-validation.json','Reports/test-provenance.json','Reports/webgl-build-provenance.json','Reports/webgl-acceptance-provenance.json','Reports/webgl-pipeline.json','Reports/editmode-frozen-final.xml','Reports/playmode-frozen-final.xml','Reports/browser-contract-final.tap','Reports/scene-build.json','Reports/webgl-build.txt','Reports/webgl-acceptance-build.txt','README.md','Docs/FeatureCoverage.md','Docs/AppearanceIntegration.md']
    for folder, item in report['browsers'].items():
        if item.get('testedBuildStillMatches') and (item.get('status') == 'passed' or item.get('visualAuditMatches')):
            evidence_files.append('Reports/BrowserAcceptance/' + folder + '/browser-acceptance.json')
            visual = 'Reports/BrowserAcceptance/' + folder + '/visual-audit.json'
            if (ROOT / visual).exists(): evidence_files.append(visual)
    audit_zip = out / 'SPARC_MeetingCells_Audit.zip'
    with zipfile.ZipFile(audit_zip,'w',compression=zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
        archive.write(html_path,html_path.name)
        for name in evidence_files: archive.write(ROOT / name,name)
    with zipfile.ZipFile(audit_zip) as archive:
        if archive.testzip() is not None: raise SystemExit('Audit archive CRC validation failed')
    result = {'createdUtc': datetime.now(timezone.utc).isoformat(), 'auditInputSha256': report['currentInputSha256'], 'artifacts': [{'path': str(path.relative_to(ROOT)), 'bytes': path.stat().st_size, 'sha256': sha(path)} for path in (release_zip,html_path,audit_zip)], 'webGLFileCount': len(files), 'archiveContentHashesVerified': True}
    (out / 'delivery-manifest.json').write_text(json.dumps(result,indent=2)); print(json.dumps(result,indent=2))

if __name__ == '__main__':
    main()
