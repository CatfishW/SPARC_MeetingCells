"""Resolve serialized script GUIDs in the separate project, including assets with no GameObject."""
from pathlib import Path
import re,json
ROOT=Path(__file__).resolve().parents[1]
GUID=re.compile(r'^guid: ([0-9a-f]{32})',re.M)
SCRIPT=re.compile(r'm_Script:.*?guid: ([0-9a-f]{32})')
def main():
    known={}
    for base in (ROOT/'Assets',ROOT/'Library/PackageCache'):
        for p in base.rglob('*.meta'):
            if p.is_symlink():continue
            m=GUID.search(p.read_text(errors='replace'))
            if m:known[m[1]]=str(p.relative_to(ROOT))[:-5]
    original=json.loads((ROOT/'Reports/OriginalScene/manifest.json').read_text())
    missing=[]
    for p in (ROOT/'Assets').rglob('*'):
        if not p.is_file() or p.suffix not in {'.asset','.controller','.playable','.prefab','.unity'} or p.is_symlink():continue
        text=p.read_text(errors='replace')
        for guid in set(SCRIPT.findall(text)):
            if guid not in known:missing.append({'asset':str(p.relative_to(ROOT)),'guid':guid,'sourceScript':original['scriptMap'].get(guid),'sourcePackageScript':original['packageScripts'].get(guid)})
    (ROOT/'Reports/OriginalScene/missing-script-guids.json').write_text(json.dumps(missing,indent=2));print(json.dumps(missing[:35],indent=2));print('Unresolved script GUID references:',len(missing))
if __name__=='__main__':main()
