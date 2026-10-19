"""Restore the source rendering asset dependency graph and navigation settings as data, never original C#."""
import json,re,shutil,hashlib
from pathlib import Path
from audit_original_scene import ROOT,SOURCE,OUT,GUID,TEXT,CODE

def main():
    audit=json.loads((OUT/'manifest.json').read_text());mapping=audit['assetMap'];pending=['6cbb334173594044d902c240207198f2'];seen=set();records=[];skipped=[]
    backup=ROOT/'Revisions/rejected-expedition-before-original-restoration'
    def copy(source,target):
        if target.exists() and source.read_bytes()==target.read_bytes():return
        if target.exists():
            saved=backup/target.relative_to(ROOT)
            if not saved.exists():saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(target,saved)
        target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,target)
    while pending:
        guid=pending.pop()
        if guid in seen:continue
        seen.add(guid);rel=mapping.get(guid)
        if not rel:continue
        p=SOURCE/rel
        if not p.is_file():continue
        if p.suffix.lower() in CODE:skipped.append(rel);continue
        data=p.read_bytes();copy(p,ROOT/rel)
        if Path(str(p)+'.meta').exists():copy(Path(str(p)+'.meta'),ROOT/(rel+'.meta'))
        records.append({'path':rel,'sha256':hashlib.sha256(data).hexdigest()})
        if p.suffix in TEXT:pending.extend(GUID.findall(data.decode('utf-8',errors='replace')))
    navigation=Path('ProjectSettings/NavMeshAreas.asset');copy(SOURCE/navigation,ROOT/navigation)
    records.append({'path':str(navigation),'sha256':hashlib.sha256((ROOT/navigation).read_bytes()).hexdigest()})
    (OUT/'original-render-settings.json').write_text(json.dumps({'restored':records,'firstPartyScriptsNotCopied':skipped},indent=2))
    print('Rendering/navigation data restored:',len(records),'first-party script dependencies:',skipped)
if __name__=='__main__':main()
