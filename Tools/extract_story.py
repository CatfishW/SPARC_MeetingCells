"""Read-only source audit. Export serialized narrative DATA, never legacy code."""
from pathlib import Path
import argparse, collections, hashlib, json, re
import yaml

HEADER = re.compile(r'^--- !u!(\d+) &(-?\d+)(?: stripped)?\s*$', re.M)
LOADER = getattr(yaml, 'CSafeLoader', yaml.SafeLoader)
def documents(path):
    text = path.read_text(encoding='utf-8-sig')
    matches = list(HEADER.finditer(text))
    output = {}
    for i, match in enumerate(matches):
        raw = text[match.end(): matches[i+1].start() if i+1<len(matches) else len(text)]
        payload = yaml.load(raw, Loader=LOADER) or {}
        output[int(match[2])] = {'class':int(match[1]), 'data':next(iter(payload.values()), {})}
    return output

def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--source', required=True)
    parser.add_argument('--output', required=True)
    args = parser.parse_args()
    source, output = Path(args.source).resolve(), Path(args.output).resolve()
    if source == output or source in output.parents:
        raise ValueError('Audit output must be outside the source project')
    output.mkdir(parents=True, exist_ok=True)
    scene = source/'Assets/GameMeetingCells/Scenes/MeetingCells_NewEnv.unity'
    docs = documents(scene)
    assets, scripts = {}, {}
    for meta in (source/'Assets').rglob('*.meta'):
        if meta.is_symlink(): continue
        match = re.search(r'^guid: (\w+)', meta.read_text(errors='replace'), re.M)
        if match:
            path = str(meta.relative_to(source))[:-5]
            assets[match[1]] = path
            if path.endswith('.cs'): scripts[match[1]] = path
    def name(fid):
        doc = docs.get(fid, {}).get('data', {})
        if 'm_Name' in doc and doc['m_Name']: return str(doc['m_Name'])
        return name(doc['m_GameObject']['fileID']) if doc.get('m_GameObject',{}).get('fileID') else str(fid)
    def kind(doc):
        d = doc['data']
        return Path(scripts.get(d.get('m_Script',{}).get('guid',''), 'Package')).stem
    def clean(d):
        return {k:v for k,v in d.items() if k not in ('m_ObjectHideFlags','m_CorrespondingSourceObject','m_PrefabInstance','m_PrefabAsset','m_EditorHideFlags','m_Script','m_EditorClassIdentifier')}
    blocks, directors, behaviours = [], [], []
    for fid, doc in docs.items():
        d, k = doc['data'], kind(doc)
        if k == 'Block':
            commands=[]
            for ref in d.get('commandList',[]):
                cfid=ref.get('fileID',0); cdoc=docs.get(cfid)
                if cdoc: commands.append({'id':cfid,'type':kind(cdoc),'data':clean(cdoc['data'])})
            blocks.append({'id':fid,'name':d.get('blockName',name(fid)), 'event':d.get('eventHandler'), 'commands':commands})
        if doc['class']==320:
            directors.append({'id':fid,'name':name(fid),'asset':assets.get(d.get('m_PlayableAsset',{}).get('guid',''),''),'data':clean(d)})
        if doc['class']==114 and k not in ('Package','Block','Say','SetActive','CallMethod','Wait','Call','InvokeMethod'):
            behaviours.append({'id':fid,'type':k,'name':name(fid),'data':clean(d)})
    audit={'sourceScene':str(scene),'sha256':hashlib.sha256(scene.read_bytes()).hexdigest(),'blocks':blocks,'directors':directors,'behaviours':behaviours,'names':{str(fid):name(fid) for fid in docs},'scriptMap':scripts,'assetMap':assets}
    (output/'source-story.json').write_text(json.dumps(audit,ensure_ascii=False,indent=2))
    counts=collections.Counter(c['type'] for b in blocks for c in b['commands'])
    print('SCENE',audit['sha256'],'OBJECTS',len(docs))
    print('BLOCKS',[(b['name'],len(b['commands'])) for b in blocks])
    print('COMMAND_TYPES',dict(counts))
    print('DIRECTORS',[(x['name'],x['asset'],x['data'].get('m_InitialState')) for x in directors])
    print('BEHAVIOURS',dict(collections.Counter(x['type'] for x in behaviours)))

if __name__=='__main__': main()
