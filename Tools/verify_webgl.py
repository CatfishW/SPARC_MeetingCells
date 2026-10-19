#!/usr/bin/env python3
"""Execute the frozen-source desktop WebGL acceptance pipeline. No publication or live accounts."""
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
from build_webgl_verified import input_manifest, fingerprint, sha

ROOT = Path(__file__).resolve().parents[1]
UNITY = '/Applications/Unity/Hub/Editor/6000.3.0b10/Unity.app/Contents/MacOS/Unity'

def now(): return datetime.now(timezone.utc).isoformat()

def main():
    os.chdir(ROOT)
    before = input_manifest()
    evidence = {'status': 'running', 'startedUtc': now(), 'inputSha256': fingerprint(before), 'steps': []}
    output = ROOT / 'Reports/webgl-pipeline.json'
    def save(): output.write_text(json.dumps(evidence, indent=2))
    def run(name, command, env=None):
        step = {'name': name, 'startedUtc': now(), 'status': 'running'}
        evidence['steps'].append(step); save(); print('RUN', name, flush=True)
        result = subprocess.run(command, cwd=ROOT, env=env, check=False)
        step.update(finishedUtc=now(), exitCode=result.returncode, status='passed' if result.returncode == 0 else 'failed'); save()
        if result.returncode: raise RuntimeError(name + ' exited with ' + str(result.returncode))
    try:
        tests = {'startedUtc': now(), 'inputSha256': evidence['inputSha256'], 'tests': {}}
        for mode, minimum in [('EditMode', 40), ('PlayMode', 13)]:
            xml = Path('Reports') / (mode.lower() + '-frozen-final.xml')
            log = Path('Reports') / ('unity-' + mode.lower() + '-frozen-final.log')
            run(mode, [UNITY, '-batchmode', '-nographics', '-projectPath', str(ROOT), '-runTests', '-testPlatform', mode, '-testResults', str(xml), '-logFile', str(log)])
            result = ET.parse(xml).getroot()
            if result.attrib.get('result') != 'Passed' or int(result.attrib.get('total', 0)) < minimum or int(result.attrib.get('failed', -1)) != 0:
                raise RuntimeError(mode + ' XML does not prove a complete pass')
            tests['tests'][mode] = {'file': str(xml), 'sha256': sha(xml), **result.attrib}
        node = subprocess.run(['node', '--test', '--test-reporter=tap', 'Tools/tests/appearance-handoff.test.cjs', 'Tools/tests/browser-platform.test.cjs'], capture_output=True, text=True, check=False)
        tap = Path('Reports/browser-contract-final.tap'); tap.write_text(node.stdout + node.stderr)
        if node.returncode or not re.search(r'# tests 25\b', node.stdout) or not re.search(r'# fail 0\b', node.stdout): raise RuntimeError('25 Node contract tests did not pass')
        tests.update(status='passed', finishedUtc=now(), inputSha256After=fingerprint(input_manifest()), nodeTests={'file': str(tap), 'sha256': sha(tap), 'total': 25, 'passed': 25, 'failed': 0})
        if tests['inputSha256After'] != tests['inputSha256']: raise RuntimeError('Input assets changed during tests')
        Path('Reports/test-provenance.json').write_text(json.dumps(tests, indent=2))
        run('development WebGL build + provenance', [sys.executable, 'Tools/build_webgl_verified.py', '--acceptance'])
        run('release WebGL build + provenance', [sys.executable, 'Tools/build_webgl_verified.py'])
        run('real-input complete browser journey', ['node', 'Tools/browser/webgl-journey.cjs'])
        run('Chromium release HTTP gzip smoke', ['node', 'Tools/browser/webgl-smoke.cjs'])
        run('Firefox release HTTP gzip smoke', ['node', 'Tools/browser/webgl-smoke.cjs'], {**os.environ, 'SPARC_BROWSER': 'firefox'})
        run('Chromium release JS decompression fallback smoke', ['node', 'Tools/browser/webgl-smoke.cjs'], {**os.environ, 'SPARC_HTTP_GZIP': '0'})
        evidence['inputSha256After'] = fingerprint(input_manifest())
        if evidence['inputSha256After'] != evidence['inputSha256']: raise RuntimeError('Source assets changed during acceptance')
        evidence['status'] = 'passed-awaiting-separate-visual-audit'
    except Exception as error:
        evidence['status'] = 'failed'; evidence['failure'] = str(error); raise
    finally:
        evidence['finishedUtc'] = now(); save(); print(json.dumps({k: v for k, v in evidence.items() if k != 'steps'}, indent=2), flush=True)

if __name__ == '__main__':
    main()
