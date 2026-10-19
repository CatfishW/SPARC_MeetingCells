"""Encode current accepted browser recordings without editing their frame sequence or speed."""
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import shutil
import subprocess

from build_webgl_verified import fingerprint, input_manifest

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / 'Deliverables/OriginalScene'


def sha(path):
    h = hashlib.sha256()
    with path.open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''):
            h.update(block)
    return h.hexdigest()


def probe(path):
    return json.loads(subprocess.check_output([
        '/opt/homebrew/bin/ffprobe', '-v', 'error', '-show_entries',
        'format=duration:stream=codec_name,codec_type,width,height,r_frame_rate',
        '-of', 'json', str(path)], text=True))


def main():
    audit = json.loads((ROOT / 'Reports/OriginalScene/delivery-audit.json').read_text())
    current = fingerprint(input_manifest())
    if audit.get('status') != 'passed-original-scene-with-declared-limitations' or audit.get('failures') or audit.get('sourceInputSha256') != current:
        raise RuntimeError('Current source must have a passing joined audit before encoding')
    result = {'status': 'running', 'createdUtc': datetime.now(timezone.utc).isoformat(),
              'sourceInputSha256': current, 'recordings': []}
    backup = OUT / 'recording-history' / datetime.now(timezone.utc).strftime('%Y-%m-%dT%H-%M-%SZ')
    for label, key in [('Runtime', 'journey'), ('Controls', 'browserControls'), ('Mobile', 'mobileBrowser')]:
        accepted = audit[key]
        report_path = ROOT / accepted['report']
        if sha(report_path) != accepted['sha256']:
            raise RuntimeError('Acceptance report changed: ' + key)
        video = accepted['video']
        source = report_path.parent / video['path']
        if sha(source) != video['sha256']:
            raise RuntimeError('Accepted browser video changed: ' + key)
        output = OUT / f'SPARC_MeetingCells_Original_{label}.mp4'
        if output.exists():
            backup.mkdir(parents=True, exist_ok=True)
            shutil.copy2(output, backup / output.name)
        subprocess.run(['/opt/homebrew/bin/ffmpeg', '-v', 'warning', '-y', '-i', str(source),
                        '-c:v', 'libx264', '-preset', 'veryfast', '-crf', '20', '-threads', '2',
                        '-movflags', '+faststart', '-an', str(output)], check=True)
        source_probe, output_probe = probe(source), probe(output)
        if abs(float(source_probe['format']['duration']) - float(output_probe['format']['duration'])) > .2:
            raise RuntimeError('Recording duration changed during encoding: ' + label)
        result['recordings'].append({
            'label': label, 'source': str(source.relative_to(ROOT)), 'sourceSha256': sha(source),
            'output': str(output.relative_to(ROOT)), 'outputSha256': sha(output),
            'bytes': output.stat().st_size, 'probe': output_probe, 'sourceProbe': source_probe,
            'acceptanceReport': accepted['report'], 'acceptanceSha256': accepted['sha256'],
            'editing': 'MP4 encoding only: no cuts, time changes, generated frames or synthetic gameplay.',
            'audio': 'Silent browser screen recording, not verification of runtime audio.'})
        print(label, str(output.relative_to(ROOT)), flush=True)
    if fingerprint(input_manifest()) != current:
        raise RuntimeError('Unity source changed during encoding')
    result['status'] = 'passed'
    (OUT / 'runtime-video-evidence.json').write_text(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()
