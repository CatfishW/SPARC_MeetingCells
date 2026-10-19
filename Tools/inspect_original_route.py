"""Summarize source runtime bindings without interpreting or replacing the source storyline."""
from pathlib import Path
import json
from extract_story import documents
from audit_original_scene import SOURCE,OUT

def main():
    a=json.loads((OUT/'manifest.json').read_text());objects={n['id']:n for n in a['objects']};asset=a['assetMap'];script=a['scriptMap'];package=a['packageScripts']
    def desc(ref):
        if not isinstance(ref,dict):return ref
        fid=str(ref.get('fileID',''));g=ref.get('guid')
        if g:return {'asset':asset.get(g,package.get(g,g)),'id':fid}
        obj=objects.get(fid)
        if not obj:return ref
        d=obj['data'];go=objects.get(str(d.get('m_GameObject',{}).get('fileID')))
        return {'id':fid,'name':obj['name'],'type':obj['type'],'class':obj['class'],'gameObject':go['name'] if go else None,'active':go['data'].get('m_IsActive') if go else d.get('m_IsActive')}
    out={}
    for director in a['directors']:
        if director['name'] not in ['StorylineIntro','StorylineCap','Storyline_RBC_Raise_Question','Storyline_MeetingCellsTransition2Heart']:continue
        info={'director':director['data'],'bindings':[{ 'key':desc(x['key']),'value':desc(x['value'])} for x in director['data'].get('m_SceneBindings',[])],'timelineRecords':[]}
        p=asset.get(director['asset'].get('guid',''))
        if p:
            for fid,r in documents(SOURCE/p).items():
                d=r['data']
                if r['class']==74:continue
                typ=Path(script.get(d.get('m_Script',{}).get('guid',''),package.get(d.get('m_Script',{}).get('guid',''),''))).stem
                info['timelineRecords'].append({'id':str(fid),'type':typ,'data':d})
        out[director['name']]=info
    out['SignalsAndEnables']=[{'id':n['id'],'name':n['name'],'data':n['data'],'targets':[desc(r) for r in n['data'].get('sourceReferences',[])]} for n in a['objects'] if n['type'] in ['SignalReceiver','OnEnableEvent']]
    out['prefabActivities']={}
    contracts=json.loads((OUT/'contracts.json').read_text())
    for typ in ['RedBloodCellGameManager','RedBloodCellEndGameScreen','PlayerController2D','OxygenController','ExitManager','ExitConfirmation','MatchColliderSizeToUI']:
        out['prefabActivities'][typ]=contracts['prefabTypes'].get(typ,[])
    (OUT/'route-bindings.json').write_text(json.dumps(out,indent=2));print('Wrote route-bindings.json with',len(out),'sections')
if __name__=='__main__':main()
