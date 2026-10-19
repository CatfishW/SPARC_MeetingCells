"""Run the original-scene browser/server contracts and bind results to tested sources."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
REPORTS = ROOT / 'Reports/OriginalScene'


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def now():
    return datetime.now(timezone.utc).isoformat()


def main():
    target = REPORTS / 'verified-contracts.json'
    previous = json.loads(target.read_text())
    sources = {name: sha(ROOT / name) for name in dict.fromkeys([*previous['sources'], 'Tools/verify_original_contracts.py'])}
    report = {'status': 'running', 'startedUtc': now(), 'sources': sources, 'checks': []}
    target.write_text(json.dumps(report, indent=2))
    commands = [
        ('node', ['node', '--test', '--test-reporter=tap', 'Tools/tests/appearance-handoff.test.cjs',
                  'Tools/tests/browser-platform.test.cjs', 'Tools/tests/original-white-navigation.test.cjs'],
         'browser-contracts.tap'),
        ('python', [sys.executable, '-m', 'unittest', 'discover', '-s', 'Tools/tests',
                    '-p', 'test_original_*.py'], 'python-contracts.log'),
    ]
    for name, command, filename in commands:
        log = REPORTS / filename
        with log.open('w') as stream:
            result = subprocess.run(command, cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT)
        report['checks'].append({'name': name, 'exitCode': result.returncode,
                                 'log': str(log.relative_to(ROOT)), 'sha256': sha(log)})
    unchanged = all(sha(ROOT / name) == digest for name, digest in sources.items())
    report.update(finishedUtc=now(), status='passed' if unchanged and all(
        check['exitCode'] == 0 for check in report['checks']) else 'failed')
    target.write_text(json.dumps(report, indent=2))
    print(report['status'], report['checks'])
    if report['status'] != 'passed':
        raise SystemExit(1)


if __name__ == '__main__':
    main()
