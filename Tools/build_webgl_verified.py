#!/usr/bin/env python3
"""Build WebGL with content hashes before and after Unity; fail on concurrent asset edits."""
import argparse
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess

ROOT = Path(__file__).resolve().parents[1]

def sha(path):
    digest = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()

def input_manifest():
    roots = ('Assets', 'Packages', 'ProjectSettings')
    result = {}
    for folder in roots:
        for path in sorted((ROOT / folder).rglob('*')):
            if path.is_file() and not path.is_symlink() and path.name != '.DS_Store':
                result[str(path.relative_to(ROOT))] = sha(path)
    return result

def fingerprint(items):
    return hashlib.sha256(json.dumps(items, sort_keys=True, separators=(',', ':')).encode()).hexdigest()

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--acceptance', action='store_true')
    parser.add_argument('--unity', default='/Applications/Unity/Hub/Editor/6000.3.0b10/Unity.app/Contents/MacOS/Unity')
    args = parser.parse_args()
    if not Path(args.unity).is_file(): parser.error('Unity executable does not exist')
    stem = 'webgl-acceptance' if args.acceptance else 'webgl-build'
    output = ROOT / ('Builds/WebGLAcceptance' if args.acceptance else 'Builds/WebGL')
    report_path = ROOT / 'Reports' / (stem + '-provenance.json')
    log_path = ROOT / 'Reports' / ('unity-' + stem + '-verified.log')
    before = input_manifest()
    evidence = {'status': 'running', 'startedUtc': datetime.now(timezone.utc).isoformat(), 'kind': 'development-read-only-acceptance' if args.acceptance else 'release', 'inputSha256': fingerprint(before), 'inputs': before, 'outputDirectory': str(output.relative_to(ROOT)), 'log': str(log_path.relative_to(ROOT))}
    report_path.write_text(json.dumps(evidence, indent=2))
    command = [args.unity, '-batchmode', '-nographics', '-quit', '-buildTarget', 'WebGL', '-projectPath', str(ROOT), '-executeMethod', 'Sparc.MeetingCells.Editor.ProjectBuilder.' + ('BuildWebGLAcceptance' if args.acceptance else 'BuildWebGL'), '-logFile', str(log_path)]
    print('BUILD', evidence['kind'], evidence['inputSha256'], flush=True)
    result = subprocess.run(command, cwd=ROOT, check=False)
    after = input_manifest()
    evidence.update(finishedUtc=datetime.now(timezone.utc).isoformat(), exitCode=result.returncode, inputSha256After=fingerprint(after), changedInputs=sorted(name for name in set(before) | set(after) if before.get(name) != after.get(name)))
    evidence['outputs'] = {str(path.relative_to(output)): {'sha256': sha(path), 'bytes': path.stat().st_size} for path in sorted(output.rglob('*')) if path.is_file()}
    summary = ROOT / 'Reports' / ('webgl-acceptance-build.txt' if args.acceptance else 'webgl-build.txt')
    evidence['buildSummary'] = summary.read_text() if summary.exists() else ''
    evidence['status'] = 'passed' if result.returncode == 0 and not evidence['changedInputs'] and 'Result: Succeeded' in evidence['buildSummary'] and 'Errors: 0' in evidence['buildSummary'] and (output / 'index.html').exists() else 'failed'
    report_path.write_text(json.dumps(evidence, indent=2))
    print(json.dumps({key: evidence[key] for key in ('status', 'kind', 'inputSha256', 'inputSha256After', 'changedInputs', 'exitCode', 'buildSummary')}, indent=2), flush=True)
    if evidence['status'] != 'passed': raise SystemExit(result.returncode or 1)

if __name__ == '__main__':
    main()
