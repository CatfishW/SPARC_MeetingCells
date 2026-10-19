"""Create compact, source-grounded implementation contracts without copying executable source."""
from pathlib import Path
import json,re
from extract_story import documents
from audit_original_scene import ROOT,SOURCE,OUT

def main():
    a=json.loads((OUT/'manifest.json').read_text());records={n['id']:n for n in a['objects']}
    interesting={'SayDialog','Writer','Teleport','ConversationTeleportHandler','ConversationPositionTeleport','AssignTargetGroupFollow','NPCMove2GivenDestination','OnDestroyHandler','NPC_Talk','NPCLookAtManager','WordGameManager','OnWordGameEndEvents','OnSubmitSceneTransition','ObjectiveTalkWithNPC','ObjectiveReachPoint','ObjectiveAnswerQuestions','EnableDelay','NPCMobile','WhiteCellLoadingScreen','WhiteCellEndGameScreen','SubtitleOnEnableTypeWriter','FungusBlockExecute','LoadHeart','RBCQuestion','OnVideoEnd'}
    contract={t:[] for t in interesting}
    for n in a['objects']:
        if n['type'] in interesting:
            contract[n['type']].append({k:n[k] for k in ('id','name','data')})
    package=a['packageScripts'];scripts=a['scriptMap'];prefab_types={}
    for guid,f in a['dependencies'].items():
        p=SOURCE/f['path']
        if p.suffix!='.prefab':continue
        for fid,d in documents(p).items():
            data=d['data'];sg=data.get('m_Script',{}).get('guid');typ=Path(scripts.get(sg,package.get(sg,''))).stem
            if not typ or sg not in scripts:continue
            prefab_types.setdefault(typ,[]).append({'asset':f['path'],'id':str(fid),'data':{k:v for k,v in data.items() if not k.startswith('m_')}})
    contract['prefabTypes']=prefab_types
    contract['missingDirectorRefs']=[{'id':d['id'],'name':d['name'],'asset':d['asset'],'bindingAssets':{g:a['assetMap'].get(g) for g in set(b['key'].get('guid','') for b in d['data'].get('m_SceneBindings',[])) if g}} for d in a['directors'] if not a['assetMap'].get(d['asset'].get('guid',''))]
    contract['invocations']=[{'block':b['name'],'id':c['id'],'data':c['data']} for b in a['blocks'] for c in b['commands'] if c['type']=='InvokeMethod']
    (OUT/'contracts.json').write_text(json.dumps(contract,ensure_ascii=False,indent=2))
    print('PREFAB_TYPES',sorted(prefab_types))
    print('MISSING_DIRECTORS',json.dumps(contract['missingDirectorRefs'],ensure_ascii=False))
    print('INVOKE',json.dumps(contract['invocations'],ensure_ascii=False))
    loaded=ROOT/'Reports/OriginalScene/imported-scene.json'
    if loaded.exists():
        imported=json.loads(loaded.read_text());print('ANIMATORS',[(n['path'],n['controller'],n['parameters']) for n in imported.get('animators',[]) if n['parameters']])
        print('CONFIG',json.dumps(imported.get('proxyConfiguration'),ensure_ascii=False)[:15000])
if __name__=='__main__':main()
