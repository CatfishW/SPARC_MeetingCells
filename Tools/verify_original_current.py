"""Frozen-source verification of the original-scene implementation, separate from browser route coverage."""
from pathlib import Path
from datetime import datetime,timezone
import json,subprocess,sys,xml.etree.ElementTree as ET
import argparse
from build_webgl_verified import input_manifest,fingerprint
from build_original_verified import digest,UNITY
ROOT=Path(__file__).resolve().parents[1]
def now():return datetime.now(timezone.utc).isoformat()
def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--reuse-verified-development',action='store_true',help='Reuse the exact existing dev bundle only when both its input fingerprint and all artifact hashes match');args=parser.parse_args()
    before=fingerprint(input_manifest());report={'status':'running','startedUtc':now(),'inputSha256':before};target=ROOT/'Reports/OriginalScene/verified-tests.json';target.write_text(json.dumps(report,indent=2))
    xml=ROOT/'Reports/OriginalScene/original-complete-tests.xml'
    command=[UNITY,'-batchmode','-force-metal','-projectPath',str(ROOT),'-runTests','-testPlatform','PlayMode','-testFilter','Sparc.Original.Tests','-testResults',str(xml),'-logFile',str(ROOT/'Reports/OriginalScene/original-complete-tests.log')]
    result=subprocess.run(command,cwd=ROOT,check=False)
    if not xml.exists():raise RuntimeError('Unity did not create test evidence')
    node=ET.parse(xml).getroot();after=fingerprint(input_manifest());report.update(finishedUtc=now(),exitCode=result.returncode,inputSha256After=after,tests={'file':str(xml.relative_to(ROOT)),'sha256':digest(xml),**node.attrib})
    report['status']='passed' if result.returncode==0 and before==after and node.attrib.get('result')=='Passed' and int(node.attrib.get('passed','0'))>=25 and node.attrib.get('failed')=='0' else 'failed'
    target.write_text(json.dumps(report,indent=2));print(json.dumps(report,indent=2),flush=True)
    if report['status']!='passed':raise SystemExit(1)
    for name,arguments in [('build_original_verified.py',[]),('build_original_verified.py',['--release']),('verify_original_fidelity.py',[]),('verify_original_player_security.py',[])]:
        if args.reuse_verified_development and name=='build_original_verified.py' and not arguments:
            previous=json.loads((ROOT/'Reports/OriginalScene/verified-webgl-build.json').read_text());outputs=previous.get('outputs',{});folder=ROOT/'Builds/WebGLOriginal'
            valid=previous.get('status')=='passed' and previous.get('inputSha256')==previous.get('inputSha256After')==after and bool(outputs) and all((folder/path).is_file() and digest(folder/path)==item['sha256'] for path,item in outputs.items())
            if not valid:raise RuntimeError('Existing development build failed fingerprint/content verification')
            print('REUSED_EXACT_VERIFIED_DEVELOPMENT',after,flush=True);continue

        print('RUN',name,*arguments,flush=True);subprocess.run([sys.executable,str(ROOT/'Tools'/name),*arguments],cwd=ROOT,check=True)
if __name__=='__main__':main()
