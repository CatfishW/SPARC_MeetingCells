#!/usr/bin/env python3
"""Stage Builds/WebGLOriginalRelease for a plain static web server.

Unity's release bundle ships gzip-compressed *.unityweb files that need a
`Content-Encoding: gzip` header. Servers that cannot add it (for example the
ping.agaii.org nginx location for /game/gamemeetingcells/) get decompressed
copies instead, and index.html is pointed at them.

Usage: python3 Tools/stage_static_release.py OUTPUT_DIR
"""
import argparse
import gzip
import shutil
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ROOT / 'Builds/WebGLOriginalRelease'


def main():
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('output', type=Path)
    args = parser.parse_args()
    out = args.output.resolve()
    if out == SOURCE.resolve() or SOURCE.resolve() in out.parents:
        parser.error('output must be outside the release build')
    if out.exists():
        shutil.rmtree(out)
    shutil.copytree(SOURCE, out, ignore=shutil.ignore_patterns('*.unityweb'))
    for packed in (SOURCE / 'Build').glob('*.unityweb'):
        with gzip.open(packed, 'rb') as source, (out / 'Build' / packed.stem).open('wb') as target:
            shutil.copyfileobj(source, target)
    index = out / 'index.html'
    text = index.read_text()
    if '.unityweb' not in text:
        raise SystemExit('index.html has no .unityweb references; template changed?')
    index.write_text(text.replace('.unityweb', ''))
    for path in sorted(out.rglob('*')):
        if path.is_file():
            print(f'{path.stat().st_size:>12,}  {path.relative_to(out)}')


if __name__ == '__main__':
    main()
