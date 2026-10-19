"""Build the original-scene WebGL player against an immutable source snapshot and verify its output.
Never use the rejected expedition builder. Changes during the build invalidate this evidence.
"""
from pathlib import Path
from datetime import datetime,timezone
import hashlib,json,subprocess,sys,argparse
from build_webgl_verified import input_manifest,fingerprint
ROOT=Path(__file__).resolve().parents[1]
UNITY='/Applications/Unity/Hub/Editor/6000.3.0b10/Unity.app/Contents/MacOS/Unity'
def digest(path):
    h=hashlib.sha256()
    with path.open('rb') as stream:
        for chunk in iter(lambda:stream.read(1024*1024),b''):h.update(chunk)
    return h.hexdigest()
def now():return datetime.now(timezone.utc).isoformat()
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--release',action='store_true');args=parser.parse_args()
    report={'status':'running','startedUtc':now(),'project':'SPARC_MeetingCells original-scene rework','kind':'release' if args.release else 'development-read-only-observation'}
    target=ROOT/('Reports/OriginalScene/verified-webgl-release.json' if args.release else 'Reports/OriginalScene/verified-webgl-build.json');before=input_manifest();report['inputSha256']=fingerprint(before);target.write_text(json.dumps(report,indent=2))
    result=subprocess.run([UNITY,'-batchmode','-force-metal','-quit','-buildTarget','WebGL','-projectPath',str(ROOT),'-executeMethod','Sparc.Original.Editor.OriginalProjectBuilder.'+('BuildWebGLRelease' if args.release else 'BuildWebGL'),'-logFile',str(ROOT/('Reports/OriginalScene/unity-webgl-release-verified.log' if args.release else 'Reports/OriginalScene/unity-webgl-original-verified.log'))],cwd=ROOT,check=False)
    after=input_manifest();summary=json.loads((ROOT/('Reports/OriginalScene/webgl-release-build.json' if args.release else 'Reports/OriginalScene/webgl-build.json')).read_text());report.update(finishedUtc=now(),exitCode=result.returncode,inputSha256After=fingerprint(after),changedInputs=sorted(key for key in set(before)|set(after) if before.get(key)!=after.get(key)),buildSummary=summary)
    build=ROOT/('Builds/WebGLOriginalRelease' if args.release else 'Builds/WebGLOriginal');report['outputDirectory']=str(build.relative_to(ROOT));report['outputs']={str(p.relative_to(build)):{'sha256':digest(p),'bytes':p.stat().st_size} for p in sorted(build.rglob('*')) if p.is_file()}
    report['status']='passed' if result.returncode==0 and not report['changedInputs'] and summary['result']=='Succeeded' and summary['errors']==0 else 'failed'
    target.write_text(json.dumps(report,indent=2));print(json.dumps({k:v for k,v in report.items() if k!='outputs'},indent=2),flush=True)
    if report['status']!='passed':raise SystemExit(1)
if __name__=='__main__':main()
