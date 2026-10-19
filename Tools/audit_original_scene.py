"""Inventory the actual original scene and linked authored assets. Never opens its editor or writes there."""
from pathlib import Path
import collections, hashlib, json, re
from extract_story import documents

ROOT=Path(__file__).resolve().parents[1]
SOURCE=ROOT.parent/'Yanlai_Zlad_SPARC_Merge'
SCENE=SOURCE/'Assets/GameMeetingCells/Scenes/MeetingCells_NewEnv.unity'
OUT=ROOT/'Reports/OriginalScene'
GUID=re.compile(r'guid: ([a-f0-9]{32})')
TEXT={'.unity','.prefab','.asset','.mat','.controller','.overridecontroller','.anim','.playable','.signal','.shader','.shadergraph','.shadersubgraph','.hlsl','.cginc','.renderTexture'}
CODE={'.cs','.dll','.asmdef','.asmref','.pdb','.js','.jslib','.rsp','.so','.dylib','.bundle'}

def sha(path):return hashlib.sha256(path.read_bytes()).hexdigest()
def walk(value):
    if isinstance(value,dict):
        yield value
        for child in value.values():yield from walk(child)
    elif isinstance(value,list):
        for child in value:yield from walk(child)

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    assets={};scripts={};packages={}
    for meta in (SOURCE/'Assets').rglob('*.meta'):
        if meta.is_symlink():continue
        match=re.search(r'^guid: ([a-f0-9]{32})',meta.read_text(errors='replace'),re.M)
        if match:
            rel=str(meta.relative_to(SOURCE))[:-5];assets[match[1]]=rel
            if rel.endswith('.cs'):scripts[match[1]]=rel
    for meta in (SOURCE/'Library/PackageCache').glob('*/*'):
        pass
    for package in (SOURCE/'Library/PackageCache').iterdir():
        if not package.is_dir():continue
        for meta in package.rglob('*.cs.meta'):
            match=re.search(r'^guid: ([a-f0-9]{32})',meta.read_text(errors='replace'),re.M)
            if match:packages[match[1]]=str(meta.relative_to(package))[:-5]
    docs=documents(SCENE)
    def typename(d):
        guid=d.get('m_Script',{}).get('guid','');p=scripts.get(guid,packages.get(guid,''));return Path(p).stem or ('Unknown:'+guid)
    def name(fid):
        d=docs.get(fid,{}).get('data',{})
        return d.get('m_Name') or (name(d['m_GameObject']['fileID']) if d.get('m_GameObject',{}).get('fileID') else str(fid))
    references=set(GUID.findall(SCENE.read_text()));visited=set();queue=list(references);files={};skipped={};events=[];timelines=[];animators=[];types=collections.Counter();blocks=[]
    while queue:
        guid=queue.pop()
        if guid in visited:continue
        visited.add(guid);rel=assets.get(guid)
        if not rel:continue
        p=SOURCE/rel
        if not p.is_file() or p.is_symlink():continue
        if p.suffix.lower() in CODE:skipped[guid]=rel;continue
        files[guid]={'path':rel,'bytes':p.stat().st_size,'sha256':sha(p)}
        if p.suffix in TEXT:
            raw=p.read_text(errors='replace');queue.extend(GUID.findall(raw))
            if raw.startswith('%YAML') and p.suffix in {'.playable','.anim','.controller'}:
                records=documents(p)
                if p.suffix=='.playable':
                    tracks=[]
                    for fid,record in records.items():
                        d=record['data']
                        if 'm_Clips' in d:tracks.append({'id':str(fid),'name':d.get('m_Name'),'type':typename(d),'clips':d['m_Clips'],'muted':d.get('m_Muted',0)})
                    timelines.append({'guid':guid,**files[guid],'tracks':tracks})
                for fid,record in records.items():
                    for e in record['data'].get('m_Events',[]):events.append({'asset':rel,'clip':record['data'].get('m_Name'),'event':e})
    nodes=[];unity_events=[];directors=[]
    for fid,record in docs.items():
        d=record['data'];cls=record['class'];typ=typename(d) if cls==114 else str(cls)
        if cls==114:types[typ]+=1
        nodes.append({'id':str(fid),'class':cls,'type':typ,'name':name(fid),'data':d})
        if cls==95:animators.append({'id':str(fid),'name':name(fid),'controller':d.get('m_Controller'),'avatar':d.get('m_Avatar'),'enabled':d.get('m_Enabled')})
        if cls==320:directors.append({'id':str(fid),'name':name(fid),'asset':d.get('m_PlayableAsset'),'data':d})
        if typ=='Block':
            commands=[]
            for ref in d.get('commandList',[]):
                cid=ref.get('fileID');cd=docs.get(cid,{}).get('data',{})
                commands.append({'id':str(cid),'type':typename(cd),'data':cd})
            blocks.append({'id':str(fid),'name':d.get('blockName'),'event':d.get('eventHandler'),'commands':commands})
        for obj in walk(d):
            if 'm_PersistentCalls' in obj:
                for call in obj['m_PersistentCalls'].get('m_Calls',[]):unity_events.append({'ownerId':str(fid),'ownerName':name(fid),'ownerType':typ,**call})
    report={'sourceScene':str(SCENE),'sourceSha256':sha(SCENE),'assetMap':assets,'scriptMap':scripts,'packageScripts':packages,'dependencies':files,'skippedExecutableCode':skipped,'objects':nodes,'blocks':blocks,'directors':directors,'timelines':timelines,'animators':animators,'animationEvents':events,'unityEvents':unity_events}
    (OUT/'manifest.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    summary={'sourceSha256':report['sourceSha256'],'serializedRecords':len(docs),'gameObjects':sum(d['class']==1 for d in docs.values()),'sourceScriptTypes':dict(types),'storyBlocks':len(blocks),'storyCommands':sum(len(b['commands']) for b in blocks),'sayCommands':sum(c['type']=='Say' for b in blocks for c in b['commands']),'directors':len(directors),'timelineAssets':len(timelines),'animators':len(animators),'animationEvents':len(events),'dependencies':len(files),'dependencyBytes':sum(f['bytes'] for f in files.values()),'unityEventMethods':sorted(set(e.get('m_MethodName','') for e in unity_events)),'animationEventMethods':sorted(set(e['event'].get('functionName','') for e in events))}
    (OUT/'summary.json').write_text(json.dumps(summary,ensure_ascii=False,indent=2));print(json.dumps({k:v for k,v in summary.items() if k!='sourceScriptTypes'},indent=2))
    print('TIMELINES',[(x['path'],len(x['tracks']),sum(len(t['clips']) for t in x['tracks'])) for x in timelines])
    print('TIMELINE_TYPES',sorted(set(t['type'] for x in timelines for t in x['tracks'])))
if __name__=='__main__':main()
