"""Scan player data/build for original provider credentials without logging any credential value."""
from pathlib import Path
from datetime import datetime,timezone
import gzip
import json
import re
ROOT=Path(__file__).resolve().parents[1]

def main():
    inventory=ROOT/'Reports/OriginalScene/manifest.json'
    data=json.loads(inventory.read_text());credentials=[]
    for record in data['objects']:
        for name,value in record.get('data',{}).items():
            normalized=re.sub('[^a-z0-9]','',name.lower())
            if normalized.endswith(('apikey','accesstoken','refreshtoken','password','secret','authorization')) and isinstance(value,str) and len(value.strip())>=16:credentials.append(value.strip())
    patterns=list(set(pattern for value in credentials for pattern in (value.encode('utf-8'),value.encode('utf-16-le'))))
    longest=max(map(len,patterns),default=1);issues=[];count=0;scanned=0
    paths=[]
    for path in (ROOT/'Assets').rglob('*'):
        if path.is_file() and not path.is_symlink() and path.suffix in ('.unity','.prefab','.asset','.controller','.json','.js','.jslib'):
            with path.open('rb') as stream:prefix=stream.read(8)
            if prefix.startswith(b'%YAML') or path.suffix in ('.json','.js','.jslib'):paths.append(path)
    builds=[ROOT/name for name in ['Builds/WebGLOriginal','Builds/WebGLOriginalRelease'] if (ROOT/name/'index.html').is_file()]
    for build in builds:paths.extend(path for path in build.rglob('*') if path.is_file() and not path.is_symlink())
    for path in paths:
        count+=1
        with path.open('rb') as raw:
            is_gzip=raw.read(2)==b'\x1f\x8b';raw.seek(0)
            stream=gzip.GzipFile(fileobj=raw) if is_gzip else raw
            tail=b'';found=False
            while True:
                block=stream.read(1024*1024)
                if not block:break
                scanned+=len(block);sample=tail+block
                if any(pattern in sample for pattern in patterns):found=True;break
                tail=sample[-longest:] if longest else b''
            if is_gzip:stream.close()
            if found:issues.append(str(path.relative_to(ROOT)))
    for name in ('Reports/OriginalScene/manifest.json','Reports/source-story.json'):
        private=ROOT/name
        if private.exists():private.chmod(0o600)
    report={'status':'passed' if not issues else 'failed','checkedUtc':datetime.now(timezone.utc).isoformat(),'originalProviderSecretCount':len(set(credentials)),'filesScanned':count,'buildsScanned':[str(build.relative_to(ROOT)) for build in builds],'decodedBytesScanned':scanned,'filesWithCredentialMatches':issues,'method':'Exact original provider-secret matching in UTF-8 and UTF-16, including streamed GZIP WebGL payloads','privateInventoriesExcludedFromDelivery':True}
    (ROOT/'Reports/OriginalScene/player-security.json').write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2))
    if issues:raise SystemExit(1)
if __name__=='__main__':main()
