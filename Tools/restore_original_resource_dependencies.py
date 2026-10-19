"""Restore original globally-loaded TMP resources and original White's wave clip.
These are source-owned assets missed by a scene-only dependency walk. Never copies source code.
"""
import json,re,shutil,hashlib
from pathlib import Path
from audit_original_scene import ROOT,SOURCE,OUT,GUID,TEXT,CODE

def main():
    audit=json.loads((OUT/'manifest.json').read_text());mapping=audit['assetMap'];reverse={p:g for g,p in mapping.items()}
    roots=['Assets/TextMesh Pro/Resources/TMP Settings.asset','Assets/Resource/SM_Updated_WBC/ANIMATIONS/UPDATED_WBC_Wave/Updated Wave.anim']
    pending=[reverse[p] for p in roots];seen=set();changes=[];backup=ROOT/'Revisions/rejected-expedition-before-original-restoration'
    while pending:
        guid=pending.pop()
        if guid in seen:continue
        seen.add(guid);rel=mapping.get(guid)
        if not rel:continue
        path=SOURCE/rel
        if not path.is_file() or path.suffix.lower() in CODE or path.is_symlink():continue
        data=path.read_bytes()
        for source,destination in ((path,ROOT/rel),(Path(str(path)+'.meta'),ROOT/(rel+'.meta'))):
            if not source.exists():continue
            content=source.read_bytes()
            if destination.exists() and destination.read_bytes()==content:continue
            if destination.exists():
                saved=backup/destination.relative_to(ROOT)
                if not saved.exists():saved.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(destination,saved)
            destination.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(source,destination)
        changes.append({'path':rel,'sha256':hashlib.sha256(data).hexdigest()})
        if path.suffix in TEXT:pending.extend(GUID.findall(data.decode('utf-8',errors='replace')))
    (OUT/'additional-original-resources.json').write_text(json.dumps(changes,indent=2));print('Original resource assets verified/copied:',len(changes))
if __name__=='__main__':main()
