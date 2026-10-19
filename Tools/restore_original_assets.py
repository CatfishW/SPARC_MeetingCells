"""Restore actual scene/art/Timelines/animations, replacing only first-party executable components.
The source is always read-only. Differing destination files are backed up before restoration.
All original scene GameObjects, transforms, native components and Timeline contents are retained.
"""
from pathlib import Path
from datetime import datetime,timezone
import collections, hashlib, json, re, shutil
import yaml
from extract_story import documents,HEADER
from audit_original_scene import ROOT,SOURCE,SCENE,OUT,sha,walk

BEHAVIOUR='f20507b741684cb7802c656cb4b1b101'
DATA='f20507b741684cb7802c656cb4b1b102'
STATE='f20507b741684cb7802c656cb4b1b103'
SCENE_OUT=ROOT/'Assets/SPARCOriginal/Scenes/MeetingCells_NewEnv.unity'
TEXT={'.prefab','.asset','.controller','.overridecontroller'}
SKIP_KEYS={'m_ObjectHideFlags','m_CorrespondingSourceObject','m_PrefabInstance','m_PrefabAsset','m_GameObject','m_Enabled','m_EditorHideFlags','m_Script','m_Name','m_EditorClassIdentifier'}

def main():
    audit=json.loads((OUT/'manifest.json').read_text())
    if sha(SCENE)!=audit['sourceSha256']:raise RuntimeError('The source scene changed after its inventory.')
    backup=ROOT/'Revisions/rejected-expedition-before-original-restoration'
    scripts=audit['scriptMap'];converted=collections.Counter();replacements=[]
    def adapt(raw,scope,suffix):
        matches=list(HEADER.finditer(raw))
        if not matches:return raw
        pieces=[]
        for i,m in enumerate(matches):
            segment=raw[m.start():matches[i+1].start() if i+1<len(matches) else len(raw)]
            if int(m[1])!=114 or ' stripped\n' in segment.splitlines(keepends=True)[0]:pieces.append(segment);continue
            script=re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]{32})',segment)
            if not script or script[1] not in scripts:pieces.append(segment);continue
            d=next(iter(yaml.load(segment[segment.index('\n')+1:],Loader=yaml.CSafeLoader).values()))
            old_type=Path(scripts[script[1]]).stem
            config={k:v for k,v in d.items() if k not in SKIP_KEYS}
            # Credentials are not story/art data and are never included in the new player.
            for obj in walk(config):
                for key in list(obj):
                    normalized=re.sub(r'[^a-z0-9]','',key.lower())
                    if normalized.endswith(('apikey','accesstoken','refreshtoken','password','secret','authorization')) and isinstance(obj[key],str):obj[key]=''
            refs={}
            for obj in walk(config):
                if 'fileID' in obj and obj.get('fileID'):
                    key=str(obj.get('guid',''))+':'+str(obj['fileID']);refs[key]={'key':key,'target':dict(obj)}
            guid=BEHAVIOUR if d.get('m_GameObject',{}).get('fileID') else STATE if suffix=='.controller' else DATA
            new={k:v for k,v in d.items() if k in SKIP_KEYS}
            new['m_Script']={'fileID':11500000,'guid':guid,'type':3};new['m_EditorClassIdentifier']=''
            new.update(sourceType=old_type,sourceId=str(m[2]),sourceScope=scope,configJson=json.dumps(config,ensure_ascii=False,separators=(',',':')),sourceReferences=list(refs.values()))
            pieces.append(segment.split('\n',1)[0]+'\n'+yaml.safe_dump({'MonoBehaviour':new},allow_unicode=True,sort_keys=False,width=1000000))
            converted[old_type]+=1;replacements.append({'scope':scope,'id':str(m[2]),'type':old_type,'dataSha256':hashlib.sha256(json.dumps(config,sort_keys=True,ensure_ascii=False).encode()).hexdigest()})
        return raw[:matches[0].start()]+''.join(pieces)
    changed=[];copied=[]
    def write(rel,data):
        target=ROOT/rel
        if target.is_file():
            old=target.read_bytes()
            if old==data:return
            saved=backup/rel
            if not saved.exists():saved.parent.mkdir(parents=True,exist_ok=True);saved.write_bytes(old)
            changed.append(rel)
        else:copied.append(rel)
        target.parent.mkdir(parents=True,exist_ok=True);target.write_bytes(data)
    for guid,item in audit['dependencies'].items():
        rel=item['path'];src=SOURCE/rel
        if src.suffix in ('.unity',):continue
        data=src.read_bytes()
        if src.suffix in TEXT and data.startswith(b'%YAML'):
            data=adapt(data.decode('utf-8-sig'),guid,src.suffix).encode('utf-8')
        write(rel,data)
        if (SOURCE/(rel+'.meta')).is_file():write(rel+'.meta',(SOURCE/(rel+'.meta')).read_bytes())
    raw=SCENE.read_text(encoding='utf-8-sig');migrated=adapt(raw,'scene',SCENE.suffix)
    write(str(SCENE_OUT.relative_to(ROOT)),migrated.encode('utf-8'))
    report={'status':'restored-not-yet-runtime-accepted','restoredUtc':datetime.now(timezone.utc).isoformat(),'sourceSha256':audit['sourceSha256'],'sourceUnchanged':sha(SCENE)==audit['sourceSha256'],'scene':str(SCENE_OUT.relative_to(ROOT)),'newFiles':copied,'restoredFiles':changed,'preimageDirectory':str(backup.relative_to(ROOT)),'rewrittenComponentTypes':dict(converted),'componentRecords':replacements,'sourceCSharpCopied':0,'timelineHashes':{t['path']:sha(ROOT/t['path']) for t in audit['timelines']},'allTimelineAssetsByteIdentical':all(sha(ROOT/t['path'])==t['sha256'] for t in audit['timelines'])}
    before=documents(SCENE);after=documents(SCENE_OUT)
    native=[fid for fid,d in before.items() if d['class']!=114]
    report['nativeRecordsPreserved']=sum(before[fid]==after.get(fid) for fid in native)
    report['nativeRecordsExpected']=len(native)
    report['originalGameObjectsPreserved']=all(before[fid]==after.get(fid) for fid,d in before.items() if d['class']==1)
    report['originalTransformsPreserved']=all(before[fid]==after.get(fid) for fid,d in before.items() if d['class'] in (4,224))
    if report['nativeRecordsPreserved']!=len(native) or not report['allTimelineAssetsByteIdentical']:raise RuntimeError('Original native scene or Timeline parity failed.')
    (OUT/'restoration.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(json.dumps({k:v for k,v in report.items() if k not in ('newFiles','restoredFiles','componentRecords','timelineHashes','rewrittenComponentTypes')},indent=2));print('ASSETS',len(copied),'RESTORED',len(changed),'NEW_IMPLEMENTATION_COMPONENTS',sum(converted.values()))
if __name__=='__main__':main()
