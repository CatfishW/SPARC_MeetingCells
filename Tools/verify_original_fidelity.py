"""Verify source fidelity for the original-scene rework. This does not certify runtime behavior."""
from pathlib import Path
from datetime import datetime, timezone
import hashlib,json
from extract_story import documents
from audit_original_scene import ROOT,SOURCE,OUT,SCENE

def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as f:
        for block in iter(lambda:f.read(1024*1024),b''):h.update(block)
    return h.hexdigest()

def main():
    reference=json.loads((OUT/'manifest.json').read_text())
    old=documents(SCENE);imported=documents(ROOT/'Assets/SPARCOriginal/Scenes/MeetingCells_NewEnv.unity');built=documents(ROOT/'Assets/SPARCOriginal/Scenes/SPARC_MeetingCells.unity')
    originals={n['id']:n for n in reference['objects']}
    baseline=[fid for fid,item in old.items() if item['class']!=114]
    native_exact=[fid for fid in baseline if old[fid]==imported.get(fid)]
    dialogs=[];commands=[];blocks=[];errors=[]
    prepared={str(fid):item['data'] for fid,item in built.items() if item['class']==114 and item['data'].get('sourceScope')=='scene'}
    for ident,record in originals.items():
        if record['type'] not in {'Block','Say','Wait','SetActive','CallMethod','Call','InvokeMethod'}:continue
        copy=prepared.get(ident)
        if not copy:
            errors.append('Missing original graph record '+ident);continue
        source={key:value for key,value in record['data'].items() if key not in {'m_ObjectHideFlags','m_CorrespondingSourceObject','m_PrefabInstance','m_PrefabAsset','m_GameObject','m_Enabled','m_EditorHideFlags','m_Script','m_Name','m_EditorClassIdentifier'}}
        config=json.loads(copy['configJson'])
        if source!=config:errors.append('Changed original graph configuration '+ident+' '+record['type'])
        if record['type']=='Say':dialogs.append({'id':ident,'text':source.get('storyText'),'voice':source.get('voiceOverClip'),'unchanged':source==config})
        elif record['type']=='Block':blocks.append(ident)
        else:commands.append(ident)
    timeline=[];clips=[];controllers=[]
    for guid,item in reference['dependencies'].items():
        path=ROOT/item['path'];suffix=path.suffix
        if suffix not in {'.playable','.anim','.controller','.overridecontroller'}:continue
        same=path.is_file() and digest(path)==item['sha256']
        row={'path':item['path'],'sourceSha256':item['sha256'],'currentSha256':digest(path) if path.is_file() else None,'byteIdentical':same}
        if suffix=='.playable':timeline.append(row)
        elif suffix=='.anim':clips.append(row)
        else:
            original_asset=documents(SOURCE/item['path']);new_asset=documents(path)
            native_ids=[fid for fid,d in original_asset.items() if d['class']!=114]
            row['nativeAnimationStateRecordsUnchanged']=all(original_asset[fid]==new_asset.get(fid) for fid in native_ids)
            row['nativeRecordCount']=len(native_ids);controllers.append(row)
    director_proof=[]
    for fid,item in old.items():
        if item['class']!=320:continue
        copy=built.get(fid,{}).get('data',{});original=item['data']
        fields=('m_PlayableAsset','m_SceneBindings','m_ExposedReferences','m_WrapMode','m_InitialTime','m_DirectorUpdateMode')
        changed=[key for key in fields if original.get(key)!=copy.get(key)]
        director_proof.append({'id':str(fid),'originalAssetAndBindingsUnchanged':not changed,'changedFields':changed})
    if len(native_exact)!=len(baseline):errors.append('Imported original native records differ')
    if len(dialogs)!=107 or len(blocks)!=29 or len(commands)+len(dialogs)!=416:errors.append('Original narrative graph coverage is incomplete')
    if any(not x['byteIdentical'] for x in timeline+clips):errors.append('Original Timeline or clip bytes changed')
    if any(not x['nativeAnimationStateRecordsUnchanged'] for x in controllers):errors.append('Original animator graph changed')
    if any(not x['originalAssetAndBindingsUnchanged'] for x in director_proof):errors.append('Original director binding or timing changed')
    sha=digest(SCENE)
    if sha!=reference['sourceSha256']:errors.append('Original source scene changed')
    source_hashes={digest(p) for p in (SOURCE/'Assets').rglob('*.cs') if p.is_file() and not p.is_symlink()}
    new_code=list((ROOT/'Assets/SPARCOriginal').rglob('*.cs'));copies=[str(p.relative_to(ROOT)) for p in new_code if digest(p) in source_hashes]
    if copies:errors.append('Exact legacy C# copies found')
    report={'status':'passed-source-fidelity-only' if not errors else 'failed','checkedUtc':datetime.now(timezone.utc).isoformat(),'sourceSceneSha256':sha,'sourceSceneUnchanged':sha==reference['sourceSha256'],'nativeRecordsExact':len(native_exact),'nativeRecordsExpected':len(baseline),'sourceGraphBlocks':len(blocks),'sourceGraphCommands':len(commands)+len(dialogs),'dialogueAndVoiceRecords':dialogs,'timelines':timeline,'animationClips':clips,'animatorControllers':controllers,'originalDirectors':director_proof,'newImplementationFiles':len(new_code),'exactLegacyCSharpCopies':copies,'errors':errors,'limits':['This audit checks asset/graph fidelity. It does not prove every source behavior, legacy external service or branch has completed runtime acceptance.','The prepared scene additionally contains input/runtime compatibility adapters, WebGL-safe streaming video URLs, staggered reflection probes and rebaked navigation using the original geometry.','Original scene GameObjects and transforms are verified at the imported baseline; the prepared scene contains documented compatibility components.']}
    (OUT/'fidelity-validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2))
    print(json.dumps({key:value for key,value in report.items() if key not in {'dialogueAndVoiceRecords','timelines','animationClips','animatorControllers','originalDirectors','limits'}},indent=2))
    print('Exact original Timelines',len(timeline),'clips',len(clips),'controllers',len(controllers),'directors',len(director_proof))
    if errors:raise SystemExit(1)
if __name__=='__main__':main()
