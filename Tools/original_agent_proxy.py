"""Server-only adapter for the original MeetingCells peer-agent contract.
No provider secret, provider URL, or system prompt is accepted from the browser.
Use build_agent() only in a loopback server or behind an authenticated same-origin deployment.
"""
from __future__ import annotations
from collections import deque
from dataclasses import dataclass
import hashlib
import json
import os
from pathlib import Path
import secrets
import threading
import time
import urllib.error
import urllib.request
from urllib.parse import urlsplit

ROOT=Path(__file__).resolve().parents[1]
ALLOWED={'https://subtoken.vip/v1':'apiKey','https://openrouter.ai/api/v1':'openRouterApiKey'}
class AgentFailure(Exception):
    def __init__(self,code: str):self.code=code;super().__init__(code)
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,req,fp,code,msg,headers,newurl):raise AgentFailure('provider_redirect_rejected')
@dataclass
class Provider:
    base: str
    key: str
    model: str

class OriginalAgent:
    def __init__(self,configuration: dict,providers: list[Provider],transport=None):
        self.prompt=str(configuration.get('systemPrompt') or '')
        if not self.prompt:raise AgentFailure('original_prompt_missing')
        self.temperature=float(configuration.get('temperature',0.6));self.top_p=float(configuration.get('topP',0.9))
        self.max_tokens=max(1,min(512,int(configuration.get('maxTokens',128))))
        self.providers=providers;self.transport=transport or self._http;self.lock=threading.Lock();self.gate=threading.BoundedSemaphore(2)
        self.sessions={};self.recent=deque();self.attempts=0;self.cooldowns={}
    @property
    def metadata(self):return {'originalPromptSha256':hashlib.sha256(self.prompt.encode()).hexdigest(),'configuredProviders':len(self.providers),'providerRequests':self.attempts}
    def _http(self,provider:Provider,payload:dict)->str:
        request=urllib.request.Request(provider.base.rstrip('/')+'/chat/completions',data=json.dumps(payload).encode(),headers={'Authorization':'Bearer '+provider.key,'Content-Type':'application/json','Accept':'application/json'},method='POST')
        try:
            with urllib.request.build_opener(NoRedirect()).open(request,timeout=18) as response:
                data=response.read(131073)
                if len(data)>131072:raise AgentFailure('provider_response_too_large')
                result=json.loads(data)
                text=result['choices'][0]['message']['content']
                if not isinstance(text,str) or not text.strip() or len(text)>4096:raise AgentFailure('provider_response_invalid')
                return text.strip()
        except AgentFailure:raise
        except urllib.error.HTTPError as error:raise AgentFailure('provider_http_'+str(error.code)) from None
        except Exception:raise AgentFailure('provider_unavailable') from None
    def reply(self,body:dict)->dict:
        if not isinstance(body,dict) or set(body)-{'text','session'}:raise AgentFailure('invalid_request')
        text=body.get('text');identity=body.get('session') or ''
        if not isinstance(text,str) or not text.strip() or len(text)>2000:raise AgentFailure('invalid_input')
        if not isinstance(identity,str) or len(identity)>100:raise AgentFailure('invalid_session')
        if not self.providers:raise AgentFailure('service_not_configured')
        now=time.monotonic()
        with self.lock:
            while self.recent and now-self.recent[0]>60:self.recent.popleft()
            if len(self.recent)>=12:raise AgentFailure('rate_limited')
            self.recent.append(now)
            for key in list(self.sessions):
                if now-self.sessions[key]['updated']>1800:self.sessions.pop(key)
            if identity:
                if identity not in self.sessions:raise AgentFailure('invalid_session')
            else:
                if len(self.sessions)>=64:raise AgentFailure('too_many_sessions')
                identity=secrets.token_urlsafe(24);self.sessions[identity]={'history':[],'busy':False,'updated':now}
            state=self.sessions[identity]
            if state['busy']:raise AgentFailure('request_in_progress')
            state['busy']=True;history=list(state['history'][-10:])
        if not self.gate.acquire(blocking=False):
            with self.lock:state['busy']=False
            raise AgentFailure('busy')
        try:
            failure='provider_unavailable'
            for provider in self.providers[:2]:
                if time.monotonic()<self.cooldowns.get(provider.base,0):failure='provider_rate_limited';continue
                payload={'model':provider.model,'messages':[{'role':'system','content':self.prompt},*history,{'role':'user','content':text.strip()}],'temperature':self.temperature,'top_p':self.top_p,'max_tokens':self.max_tokens,'stream':False}
                try:
                    self.attempts+=1;response=self.transport(provider,payload)
                    if not isinstance(response,str) or not response.strip() or len(response)>4096:raise AgentFailure('provider_response_invalid')
                    with self.lock:state['history']=(history+[{'role':'user','content':text.strip()},{'role':'assistant','content':response}])[-12:];state['updated']=time.monotonic()
                    return {'ok':True,'session':identity,'text':response}
                except AgentFailure as error:
                    failure=error.code
                    if error.code=='provider_http_429':self.cooldowns[provider.base]=time.monotonic()+60
            raise AgentFailure(failure)
        finally:
            self.gate.release()
            with self.lock:state['busy']=False

def build_agent(use_original_credentials:bool=False)->OriginalAgent:
    # Private migration inventory contains the source service settings. It is never part of the build/export.
    inventory=json.loads((ROOT/'Reports/OriginalScene/manifest.json').read_text())
    original=next(item['data'] for item in inventory['objects'] if item['type']=='LLM_Manager_Unified')
    providers=[]
    for base in original.get('apiBaseUrls',[]):
        if base not in ALLOWED:continue
        field=ALLOWED[base]
        variable='SPARC_AGENT_OPENROUTER_KEY' if field=='openRouterApiKey' else 'SPARC_AGENT_API_KEY'
        key=os.environ.get(variable) or (original.get(field) if use_original_credentials else '')
        if not isinstance(key,str) or not key.strip():continue
        model=str(original.get('modelName') or '')
        if field=='openRouterApiKey':
            choices=original.get('openRouterFallbackModels') or []
            if choices:model=str(choices[0])
        providers.append(Provider(base,key,model))
    return OriginalAgent(original,providers)

def handle_agent_post(handler,agent:OriginalAgent)->bool:
    if urlsplit(handler.path).path!='/api/sparc/agent':return False
    host=handler.headers.get('Host','');port=handler.server.server_port
    expected='http://'+host
    origin=handler.headers.get('Origin')
    if host not in {f'127.0.0.1:{port}',f'localhost:{port}'} or origin!=expected or handler.headers.get('Sec-Fetch-Site','same-origin') not in ('same-origin','none'):
        result,status={'ok':False,'code':'origin_rejected'},403
    elif handler.headers.get('Content-Type','').split(';')[0].strip()!='application/json':result,status={'ok':False,'code':'json_required'},415
    else:
        try:
            length=int(handler.headers.get('Content-Length','-1'))
            if length<1 or length>8192:raise AgentFailure('invalid_request_size')
            handler.connection.settimeout(20)
            data=handler.rfile.read(length)
            result=agent.reply(json.loads(data));status=200
        except AgentFailure as error:result,status={'ok':False,'code':error.code},200
        except Exception:result,status={'ok':False,'code':'invalid_request'},400
    encoded=json.dumps(result).encode();handler.send_response(status);handler.send_header('Content-Type','application/json');handler.send_header('Cache-Control','no-store');handler.send_header('Content-Length',str(len(encoded)));handler.end_headers();handler.wfile.write(encoded);return True
