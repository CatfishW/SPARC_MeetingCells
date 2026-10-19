"""Migrate serialized art/narrative data only; never import legacy executable code."""
from pathlib import Path
import argparse, collections, hashlib, json, re, shutil, zipfile
from extract_story import documents, HEADER

CODE={'.cs','.dll','.pdb','.js','.jslib','.asmdef','.asmref','.rsp','.so','.dylib','.bundle','.shader','.shadergraph','.compute','.hlsl','.cginc'}
TEXT={'.unity','.prefab','.asset','.mat','.controller','.overridecontroller','.anim','.playable','.signal','.renderTexture'}
GUID=re.compile(r'guid: ([0-9a-f]{32})')

def main():
    ap=argparse.ArgumentParser(); ap.add_argument('--source',required=True); args=ap.parse_args()
    source=Path(args.source).resolve(); dest=Path(__file__).resolve().parents[1]
    assert source!=dest and source not in dest.parents
    audit=json.loads((dest/'Reports/source-story.json').read_text())
    assetmap=audit['assetMap']; names=audit['names']; scene=Path(audit['sourceScene'])
    if hashlib.sha256(scene.read_bytes()).hexdigest()!=audit['sha256']: raise RuntimeError('Source scene changed since audit')
    summary=[]; dialogue=[]; operations=[]
    character_names={str(b['id']):b['data'].get('nameText') or b['name'] for b in audit['behaviours'] if b['type']=='Character'}
    for b in audit['blocks']:
        summary.append('\n## '+b['name']); lines=[]
        for c in b['commands']:
            d=c['data']; t=c['type']; text=''
            if t=='Say':
                voice=d.get('voiceOverClip',{}).get('guid',''); char=str(d.get('character',{}).get('fileID',0))
                line={'id':str(c['id']),'text':d.get('storyText') or '', 'speaker':character_names.get(char,names.get(char,'')), 'voice':assetmap.get(voice,'')}
                lines.append(line); text='SAY '+line['speaker']+': '+line['text']
            elif t=='SetActive':
                obj=d.get('_targetGameObject',{}).get('gameObjectVal',{}).get('fileID',0)
                text='ACTIVE '+names.get(str(obj),str(obj))+' = '+str(d.get('activeState',{}).get('booleanVal',0))
            elif t=='CallMethod': text='METHOD '+names.get(str(d.get('targetObject',{}).get('fileID',0)),'')+'.'+str(d.get('methodName'))
            elif t=='Call': text='CALL '+next((x['name'] for x in audit['blocks'] if x['id']==d.get('targetBlock',{}).get('fileID')), '(none)')
            else: text=t+' '+json.dumps(d,ensure_ascii=False)
            summary.append(text); operations.append({'block':b['name'],'id':c['id'],'action':text})
        dialogue.append({'name':b['name'],'lines':lines})
    (dest/'Reports/story-map.md').write_text('\n'.join(summary))
    resource=dest/'Assets/SPARC/Resources'; resource.mkdir(parents=True,exist_ok=True)
    (resource/'Narrative.json').write_text(json.dumps({'blocks':dialogue},ensure_ascii=False,indent=2))
    (dest/'Reports/legacy-operation-map.json').write_text(json.dumps(operations,ensure_ascii=False,indent=2))
    copied=set(); skipped=set(); queue=[]
    def eligible(rel):
        p=Path(rel); actual=p.with_suffix('') if p.suffix=='.meta' else p
        return str(p).startswith('Assets/') and '..' not in p.parts and actual.suffix.lower() not in CODE
    def copy(rel):
        if rel in copied or rel in skipped:return
        if not eligible(rel): skipped.add(rel);return
        src=source/rel
        if not src.is_file() or src.is_symlink(): skipped.add(rel);return
        target=dest/rel;target.parent.mkdir(parents=True,exist_ok=True)
        if target.exists() and not rel.endswith('.meta'): return
        shutil.copy2(src,target);copied.add(rel)
        meta=source/(rel+'.meta')
        if meta.is_file(): shutil.copy2(meta,dest/(rel+'.meta'));copied.add(rel+'.meta')
        if src.suffix in TEXT:queue.append(target)
    with zipfile.ZipFile(source/'MeetingCells_NewEnv_ArtAssets.zip') as z:
        for item in z.infolist():
            rel=item.filename.replace('\\','/')
            if not item.is_dir() and eligible(rel): copy(rel)
    # Traverse actual scene dependencies, not just the old art ZIP's possibly stale list.
    pending=list(GUID.findall(scene.read_text()))
    seen=set()
    while pending or queue:
        while pending:
            g=pending.pop()
            if g in seen:continue
            seen.add(g)
            if g in assetmap:copy(assetmap[g])
        while queue:
            p=queue.pop();pending.extend(GUID.findall(p.read_text(errors='replace')))
    srcdocs=documents(scene)
    def sanitize(path, text):
        matches=list(HEADER.finditer(text)); parts=[]; removed=[]
        for i,m in enumerate(matches):
            raw=text[m.start():matches[i+1].start() if i+1<len(matches) else len(text)]
            sg=re.search(r'm_Script: \{fileID: \d+, guid: ([0-9a-f]+)',raw)
            if int(m[1])==114 and sg and sg[1] in audit['scriptMap']:
                removed.append(int(m[2]));continue
            if int(m[1])==320:raw=re.sub(r'm_InitialState: \d+','m_InitialState: 0',raw)
            if int(m[1])==74:raw=re.sub(r'(?ms)^  m_Events:.*?(?=^  [A-Za-z_]|\Z)','  m_Events: []\n',raw)
            parts.append(raw)
        if not matches:return text
        body=text[:matches[0].start()]+''.join(parts)
        for fid in removed:
            body=re.sub(r'^  - component: \{fileID: '+str(fid)+r'\}\s*\n','',body,flags=re.M)
            body=body.replace('{fileID: '+str(fid)+'}', '{fileID: 0}')
        return body
    sceneout=dest/'Assets/SPARC/Scenes/MeetingCells.unity';sceneout.parent.mkdir(parents=True,exist_ok=True)
    sceneout.write_text(sanitize(sceneout,scene.read_text()))
    for rel in copied:
        p=dest/rel
        if p.suffix in TEXT:
            text=p.read_text(errors='replace')
            if text.startswith('%YAML'):p.write_text(sanitize(p,text))
    settings=dest/'ProjectSettings';settings.mkdir(exist_ok=True)
    for name in ('TagManager.asset','Physics2DSettings.asset','DynamicsManager.asset','TimeManager.asset','InputManager.asset'):
        if (source/'ProjectSettings'/name).exists():shutil.copy2(source/'ProjectSettings'/name,settings/name)
    report={'copiedFiles':len(copied),'skippedCodeAssets':len(skipped),'dialogueLines':sum(len(b['lines']) for b in dialogue),'storyBlocks':len(dialogue),'sourceSha256':audit['sha256'],'legacyCsFilesCopied':0,'sourceUnchanged':hashlib.sha256(scene.read_bytes()).hexdigest()==audit['sha256']}
    (dest/'Reports/migration.json').write_text(json.dumps(report,indent=2));print(json.dumps(report))
if __name__=='__main__':main()
