"""Remove provider credentials from cloned Unity DATA, never from the source project.
Reports contain field names/counts only. Raw source inventory remains private and must not be packaged.
"""
from pathlib import Path
import json,re,yaml
ROOT=Path(__file__).resolve().parents[1]
ENDS=('apikey','accesstoken','refreshtoken','password','secret','authorization')
def redact(obj):
    fields=[]
    if isinstance(obj,dict):
        for key,value in list(obj.items()):
            normalized=re.sub('[^a-z0-9]','',key.lower())
            if normalized.endswith(ENDS) and isinstance(value,str):
                if value:fields.append(key)
                obj[key]=''
            else:fields.extend(redact(value))
    elif isinstance(obj,list):
        for item in obj:fields.extend(redact(item))
    return fields

def main():
    changes=[]
    for path in (ROOT/'Assets').rglob('*'):
        if not path.is_file() or path.is_symlink() or path.suffix not in ('.unity','.prefab','.asset','.controller'):continue
        encoded=path.read_bytes()
        if not encoded.startswith(b'%YAML'):continue
        raw=encoded.decode('utf-8-sig');edits=[]
        for match in re.finditer(r'^  configJson: [\s\S]*?(?=^  [A-Za-z_][A-Za-z0-9_]*:|^---|\Z)',raw,re.M):
            try:entry=yaml.safe_load(match[0])
            except yaml.YAMLError:raise RuntimeError('Cannot parse configuration in '+str(path.relative_to(ROOT))) from None
            value=entry.get('configJson')
            if not isinstance(value,str):continue
            try:obj=json.loads(value)
            except json.JSONDecodeError:continue
            fields=redact(obj)
            if fields:
                serialized=yaml.safe_dump({'configJson':json.dumps(obj,ensure_ascii=False,separators=(',',':'))},allow_unicode=True,sort_keys=False,width=1000000)
                replacement=''.join('  '+line+'\n' for line in serialized.splitlines())
                edits.append((match.start(),match.end(),replacement));changes.append({'path':str(path.relative_to(ROOT)),'redactedFieldNames':fields})
        for start,end,replacement in reversed(edits):raw=raw[:start]+replacement+raw[end:]
        if edits:path.write_text(raw)
    result={'status':'credentials-removed-from-player-data','changedRecords':len(changes),'changes':changes,'notes':['No credential values are included in this report.','Historical private builds must be replaced before sharing. Raw source inventory is never a deliverable.','Source project files and credentials were not modified.']}
    target=ROOT/'Reports/OriginalScene/player-credential-redaction.json';target.write_text(json.dumps(result,indent=2));print(json.dumps(result,indent=2))
if __name__=='__main__':main()
