"""Audit actual deliverables; never infer a passing build from its existence alone."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib
import json
import re
import xml.etree.ElementTree as ET
from build_webgl_verified import input_manifest, fingerprint

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT.parent / 'Yanlai_Zlad_SPARC_Merge'
REPORTS = ROOT / 'Reports'

def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()

def load_json(path):
    return json.loads(path.read_text()) if path.is_file() else None

def latest_tests(prefix):
    evidence = []
    for path in REPORTS.glob(prefix + '*.xml'):
        try:
            node = ET.parse(path).getroot()
            evidence.append({'file': str(path.relative_to(ROOT)), 'sha256': sha(path), **{key: node.attrib.get(key) for key in ('result', 'total', 'passed', 'failed', 'skipped', 'start-time', 'end-time')}})
        except (ET.ParseError, OSError):
            continue
    return max(evidence, key=lambda item: item.get('end-time') or '') if evidence else {'available': False}

def build_summary(name):
    path = REPORTS / name
    if not path.is_file():
        return {'available': False}
    text = path.read_text()
    result = {'file': str(path.relative_to(ROOT)), 'sha256': sha(path)}
    for key in ('Result', 'Errors', 'Warnings', 'Bytes'):
        match = re.search(r'^' + key + r':\s*(.+)$', text, re.M)
        if match:
            result[key.lower()] = match.group(1)
    return result

def main():
    migration = load_json(REPORTS / 'migration.json')
    source_scene = SOURCE / 'Assets/GameMeetingCells/Scenes/MeetingCells_NewEnv.unity'
    sources = sorted((ROOT / 'Assets').rglob('*.cs'))
    original_hashes = {sha(path) for path in (SOURCE / 'Assets').rglob('*.cs') if path.is_file() and not path.is_symlink()}
    copies = [str(path.relative_to(ROOT)) for path in sources if sha(path) in original_hashes]
    misplaced = [str(path.relative_to(ROOT)) for path in sources if not str(path.relative_to(ROOT)).startswith('Assets/SPARC/')]
    current_input = fingerprint(input_manifest())
    provenance = {}
    for kind, filename in (('release','webgl-build-provenance.json'),('development','webgl-acceptance-provenance.json')):
        item = load_json(REPORTS / filename) or {}
        directory = item.get('outputDirectory', '')
        allowed = directory in ('Builds/WebGL', 'Builds/WebGLAcceptance')
        item['currentInputMatches'] = item.get('inputSha256') == item.get('inputSha256After') == current_input
        item['currentOutputMatches'] = allowed and bool(item.get('outputs')) and all((ROOT / directory / name).is_file() and sha(ROOT / directory / name) == details['sha256'] for name, details in item.get('outputs', {}).items())
        provenance[kind] = {key: value for key, value in item.items() if key not in ('inputs', 'outputs')}
    tests_provenance = load_json(REPORTS / 'test-provenance.json') or {}
    tests_provenance['currentInputMatches'] = tests_provenance.get('inputSha256') == tests_provenance.get('inputSha256After') == current_input
    tests_provenance['resultFilesMatch'] = bool(tests_provenance.get('tests')) and all((ROOT / result['file']).is_file() and sha(ROOT / result['file']) == result['sha256'] for result in tests_provenance.get('tests', {}).values())
    node_evidence = tests_provenance.get('nodeTests', {})
    tests_provenance['nodeResultMatches'] = bool(node_evidence) and (ROOT / node_evidence.get('file', '')).is_file() and sha(ROOT / node_evidence['file']) == node_evidence.get('sha256')
    browser = {}
    for path in sorted((REPORTS / 'BrowserAcceptance').glob('*/browser-acceptance.json')):
        item = load_json(path)
        hashes = item.get('buildHashes', {})
        directory = item.get('buildDirectory', 'Builds/WebGL')
        allowed = directory in ('Builds/WebGL', 'Builds/WebGLAcceptance')
        item['testedBuildStillMatches'] = allowed and bool(hashes) and all((ROOT / directory / name).is_file() and sha(ROOT / directory / name) == digest for name, digest in hashes.items())
        review = load_json(path.parent / 'visual-audit.json') or {}
        reviewed = review.get('screenshots', {})
        item['visualAuditMatches'] = review.get('status') == 'passed' and len(reviewed) >= 3 and all((path.parent / name).is_file() and sha(path.parent / name) == digest for name, digest in reviewed.items())
        item['visualAudit'] = review
        # Full screenshots and detailed frame observations remain in the source reports.
        browser[path.parent.name] = {key: value for key, value in item.items() if key not in ('snapshots', 'consoleMessages', 'lastState', 'buildHashes')}
    report = {
        'auditedUtc': datetime.now(timezone.utc).isoformat(),
        'sourceSceneSha256': sha(source_scene),
        'sourceSceneMatchesInitialAudit': sha(source_scene) == migration['sourceSha256'],
        'csharpFiles': len(sources),
        'currentInputSha256': current_input,
        'buildProvenance': provenance,
        'testProvenance': tests_provenance,
        'exactLegacySourceCopies': copies,
        'legacyCsharpOutsideNewNamespaceFolder': misplaced,
        'editMode': latest_tests('editmode'),
        'playMode': latest_tests('playmode'),
        'scene': load_json(REPORTS / 'scene-build.json'),
        'nativeBuild': build_summary('mac-build.txt'),
        'windowedNativeAcceptance': load_json(REPORTS / 'WindowedAcceptance/windowed-acceptance.json'),
        'webGLBuild': build_summary('webgl-build.txt'),
        'webGLProvenance': provenance['release'],
        'webGLFiles': {str(path.relative_to(ROOT / 'Builds/WebGL')): {'bytes': path.stat().st_size, 'sha256': sha(path)} for path in sorted((ROOT / 'Builds/WebGL').rglob('*')) if path.is_file()},
        'browsers': browser,
        'limits': {
            'liveAuthenticatedAppearance': 'Not deployed or verified with a real account. Isolated contract tests use synthetic credentials only.',
            'heartGame': 'External Heart game is not copied into this project; a handoff event and optional nextSceneName are provided.',
            'browserStoryCoverage': 'Read each browser report: smoke checks do not prove full story completion in that browser.',
            'sourceCopyAudit': 'Exact hash comparison proves no identical legacy C# files; it is not a semantic-equivalence proof.'
        }
    }
    failures = []
    if not report['sourceSceneMatchesInitialAudit']: failures.append('Original scene differs from migration baseline')
    if copies or misplaced: failures.append('Legacy C# source-copy audit failed')
    for key in ('editMode', 'playMode'):
        tests = report[key]
        if tests.get('result') != 'Passed' or int(tests.get('total') or 0) < 1: failures.append(key + ' tests are not proven passing')
    if report['webGLBuild'].get('result') != 'Succeeded' or report['webGLBuild'].get('errors') != '0': failures.append('WebGL build is not proven successful')
    if not report['webGLFiles']: failures.append('WebGL output files are missing')
    for kind, item in provenance.items():
        if item.get('status') != 'passed' or not item.get('currentInputMatches') or not item.get('currentOutputMatches'): failures.append(kind + ' build provenance is stale or incomplete')
    if tests_provenance.get('status') != 'passed' or not all(tests_provenance.get(key) for key in ('currentInputMatches','resultFilesMatch','nodeResultMatches')): failures.append('Unit/contract test provenance is stale or incomplete')
    if node_evidence.get('total') != 25 or node_evidence.get('failed') != 0: failures.append('25 Node contract tests are not proven passing')
    journeys = [item for item in browser.values() if item.get('status') == 'passed' and item.get('testedBuildStillMatches') and item.get('gameplayStateVerified')]
    if not any(set(item.get('chapters', [])) == set(range(12)) and len(set(item.get('completedCues', []))) == 7 and item.get('workbookAnswers') == 13 and item.get('workbookPages') == 10 and not item.get('errors') for item in journeys): failures.append('No complete real-input browser journey matches the current development build')
    for engine, delivery in (('chromium','http-gzip'),('firefox','http-gzip'),('chromium','fallback')):
        candidates = [item for item in browser.values() if item.get('scope') == 'release-smoke' and item.get('engine') == engine and item.get('httpDelivery') == delivery and item.get('testedBuildStillMatches')]
        if not any(item.get('status') == 'smoke-checks-passed-awaiting-visual-audit' and item.get('visualAuditMatches') and not item.get('runtimeErrors') and not item.get('failedRequests') for item in candidates): failures.append(engine + ' ' + delivery + ' release smoke/visual evidence is missing')
    report['auditFailures'] = failures
    report['status'] = 'passed-with-declared-limitations' if not failures else 'failed'
    (REPORTS / 'delivery-validation.json').write_text(json.dumps(report, indent=2))
    compact = {key: report[key] for key in ('status', 'auditedUtc', 'sourceSceneMatchesInitialAudit', 'csharpFiles', 'exactLegacySourceCopies', 'editMode', 'playMode', 'webGLBuild', 'auditFailures')}
    compact['browsers'] = {name: {'status': item.get('status'), 'testedBuildStillMatches': item['testedBuildStillMatches']} for name, item in browser.items()}
    print(json.dumps(compact, indent=2))
    if failures:
        raise SystemExit(1)

if __name__ == '__main__':
    main()
