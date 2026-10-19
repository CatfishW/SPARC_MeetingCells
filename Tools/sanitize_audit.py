"""Remove credential-like serialized fields from this project's generated audit only."""
from pathlib import Path
import json,re
root=Path(__file__).resolve().parents[1]
path=root/'Reports/source-story.json'
data=json.loads(path.read_text())
pattern=re.compile(r'(?:password|secret|api[_-]?key|access[_-]?token|refresh[_-]?token|authorization|credential)',re.I)
count=0
def clean(value):
    global count
    if isinstance(value,dict):
        result={}
        for key,item in value.items():
            if pattern.search(str(key)) and isinstance(item,(str,int,float)) and item not in ('',0,None):
                result[key]='[REDACTED]';count+=1
            else:result[key]=clean(item)
        return result
    if isinstance(value,list):return [clean(v) for v in value]
    return value
cleaned=clean(data)
path.write_text(json.dumps(cleaned,ensure_ascii=False,indent=2))
(root/'Reports/audit-sanitization.json').write_text(json.dumps({'generatedReport':'source-story.json','redactedFields':count,'sourceProjectModified':False},indent=2))
print('Generated audit sanitized; redacted field count:',count)
