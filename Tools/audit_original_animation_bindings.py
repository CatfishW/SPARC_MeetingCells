"""Read authored animation bindings and asset hashes, no original writes."""
from pathlib import Path
import json,re
from extract_story import documents
from audit_original_scene import OUT,SOURCE

def main():
    source=json.loads((OUT/'manifest.json').read_text());custom=[];events=[];states=[]
    for guid,entry in source['dependencies'].items():
        path=SOURCE/entry['path']
        if path.suffix not in {'.anim','.controller','.playable'}:continue
        for fid,record in documents(path).items():
            data=record['data']
            if record['class']==74:
                for field in ('m_FloatCurves','m_PPtrCurves','m_EditorCurves','m_EulerEditorCurves'):
                    for curve in data.get(field,[]) or []:
                        script=curve.get('script',{}).get('guid')
                        if script in source['scriptMap']:custom.append({'asset':entry['path'],'clip':data.get('m_Name'),'list':field,'attribute':curve.get('attribute'),'path':curve.get('path'),'scriptGuid':script,'script':source['scriptMap'][script]})
                for event in data.get('m_Events',[]) or []:events.append({'asset':entry['path'],'clip':data.get('m_Name'),'event':event})
            if record['class']==1102:states.append({'controller':entry['path'],'state':data.get('m_Name'),'motion':data.get('m_Motion')})
    report={'customCurveBindings':custom,'events':events,'controllerStates':states};(OUT/'animation-contracts.json').write_text(json.dumps(report,indent=2))
    print(json.dumps({'customCurveBindings':custom,'events':events},indent=2));print('STATES',[(x['controller'],x['state']) for x in states if 'WBC.controller' in x['controller'] or 'Customized_Character' in x['controller']])
if __name__=='__main__':main()
