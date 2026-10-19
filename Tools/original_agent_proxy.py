"""Server-only adapter for the original MeetingCells peer-agent contract.
No provider secret, provider URL, or system prompt is accepted from the browser.
Use build_agent() only in a loopback server or behind an authenticated same-origin deployment.
"""
from __future__ import annotations
from collections import deque
from dataclasses import asdict, dataclass
import hashlib
import ipaddress
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
ALLOWED={'https://subtoken.vip/v1':'apiKey','https://subtoken.shop/v1':'apiKey','https://openrouter.ai/api/v1':'openRouterApiKey'}
CONFIG_FIELDS=('systemPrompt','temperature','topP','maxTokens','modelName','apiBaseUrls','openRouterFallbackModels')
class AgentFailure(Exception):
    def __init__(self,code: str):self.code=code;super().__init__(code)
class NoRedirect(urllib.request.HTTPRedirectHandler):
    def redirect_request(self,req,fp,code,msg,headers,newurl):raise AgentFailure('provider_redirect_rejected')
@dataclass
class Provider:
    base: str
    key: str
    model: str
# Limit field -> (environment variable, smallest, largest). queue_seconds + 2 providers x 18 s stays under the game's 45 s timeout.
LIMIT_VARIABLES={'rate_per_minute':('SPARC_AGENT_RATE_PER_MINUTE',1,10000),'client_rate_per_minute':('SPARC_AGENT_CLIENT_RATE_PER_MINUTE',1,10000),'max_in_flight':('SPARC_AGENT_MAX_IN_FLIGHT',1,64),'queue_seconds':('SPARC_AGENT_QUEUE_SECONDS',0,8),'max_sessions':('SPARC_AGENT_MAX_SESSIONS',1,10000)}
@dataclass(frozen=True)
class Limits:
    rate_per_minute: int=12         # accepted requests per rolling minute, all clients
    client_rate_per_minute: int=12  # same, per client address (a school NAT is one client)
    max_in_flight: int=2            # concurrent provider calls
    queue_seconds: float=8          # wait for a free provider slot before answering busy
    max_sessions: int=64            # open conversations, each expiring after 30 idle minutes
    @classmethod
    def from_env(cls,environ=None)->Limits:
        environ=os.environ if environ is None else environ;values={}
        for field,(variable,low,high) in LIMIT_VARIABLES.items():
            raw=environ.get(variable,'').strip()
            if not raw:continue
            try:value=(float if field=='queue_seconds' else int)(raw)
            except ValueError:raise ValueError(f'{variable} must be a number, got {raw!r}') from None
            if not low<=value<=high:raise ValueError(f'{variable} must be between {low} and {high}, got {raw!r}')
            values[field]=value
        return cls(**values)

class OriginalAgent:
    def __init__(self,configuration: dict,providers: list[Provider],transport=None,limits:Limits|None=None):
        self.prompt=str(configuration.get('systemPrompt') or '')
        if not self.prompt:raise AgentFailure('original_prompt_missing')
        self.temperature=float(configuration.get('temperature',0.6));self.top_p=float(configuration.get('topP',0.9))
        self.max_tokens=max(1,min(512,int(configuration.get('maxTokens',128))))
        self.limits=limits or Limits.from_env()
        self.providers=providers;self.transport=transport or self._http;self.lock=threading.Lock();self.gate=threading.BoundedSemaphore(self.limits.max_in_flight)
        self.sessions={};self.recent=deque();self.clients={};self.attempts=0;self.cooldowns={}
    @property
    def metadata(self):return {'originalPromptSha256':hashlib.sha256(self.prompt.encode()).hexdigest(),'configuredProviders':len(self.providers),'providerRequests':self.attempts,'limits':asdict(self.limits)}
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
    def reply(self,body:dict,client:str='')->dict:
        if not isinstance(body,dict) or set(body)-{'text','session'}:raise AgentFailure('invalid_request')
        text=body.get('text');identity=body.get('session') or ''
        if not isinstance(text,str) or not text.strip() or len(text)>2000:raise AgentFailure('invalid_input')
        if not isinstance(identity,str) or len(identity)>100:raise AgentFailure('invalid_session')
        if not self.providers:raise AgentFailure('service_not_configured')
        limits=self.limits;now=time.monotonic()
        with self.lock:
            while self.recent and now-self.recent[0]>60:self.recent.popleft()
            for key in list(self.clients):
                calls=self.clients[key]
                while calls and now-calls[0]>60:calls.popleft()
                if not calls:self.clients.pop(key)
            if len(self.clients.get(client,()))>=limits.client_rate_per_minute:raise AgentFailure('client_rate_limited')
            if len(self.recent)>=limits.rate_per_minute:raise AgentFailure('rate_limited')
            self.recent.append(now);self.clients.setdefault(client,deque()).append(now)
            for key in list(self.sessions):
                if now-self.sessions[key]['updated']>1800:self.sessions.pop(key)
            fresh=not identity
            if identity:
                if identity not in self.sessions:raise AgentFailure('invalid_session')
            else:
                if len(self.sessions)>=limits.max_sessions:raise AgentFailure('too_many_sessions')
                identity=secrets.token_urlsafe(24);self.sessions[identity]={'history':[],'busy':False,'updated':now}
            state=self.sessions[identity]
            if state['busy']:raise AgentFailure('request_in_progress')
            state['busy']=True;history=list(state['history'][-10:])
        acquired=answered=False
        try:
            # A short wait for a provider slot rides out a class reaching the peer step together instead of failing at once.
            acquired=self.gate.acquire(timeout=limits.queue_seconds)
            if not acquired:raise AgentFailure('busy')
            failure='provider_unavailable'
            for provider in self.providers[:2]:
                if time.monotonic()<self.cooldowns.get(provider.base,0):failure='provider_rate_limited';continue
                payload={'model':provider.model,'messages':[{'role':'system','content':self.prompt},*history,{'role':'user','content':text.strip()}],'temperature':self.temperature,'top_p':self.top_p,'max_tokens':self.max_tokens,'stream':False}
                try:
                    self.attempts+=1;response=self.transport(provider,payload)
                    if not isinstance(response,str) or not response.strip() or len(response)>4096:raise AgentFailure('provider_response_invalid')
                    with self.lock:state['history']=(history+[{'role':'user','content':text.strip()},{'role':'assistant','content':response}])[-12:];state['updated']=time.monotonic()
                    answered=True;return {'ok':True,'session':identity,'text':response}
                except AgentFailure as error:
                    failure=error.code
                    if error.code=='provider_http_429':self.cooldowns[provider.base]=time.monotonic()+60
            raise AgentFailure(failure)
        finally:
            if acquired:self.gate.release()
            with self.lock:
                state['busy']=False
                # The browser never learned a new session whose first reply failed; free its slot instead of holding it 30 minutes.
                if fresh and not answered:self.sessions.pop(identity,None)

def load_configuration()->dict:
    # SPARC_AGENT_CONFIG names a key-free settings file written by export_configuration().
    path=os.environ.get('SPARC_AGENT_CONFIG')
    if path:return json.loads(Path(path).read_text())
    # Private migration inventory contains the source service settings. It is never part of the build/export.
    inventory=json.loads((ROOT/'Reports/OriginalScene/manifest.json').read_text())
    return next(item['data'] for item in inventory['objects'] if item['type']=='LLM_Manager_Unified')

def export_configuration(path)->None:
    original=load_configuration()
    Path(path).write_text(json.dumps({field:original[field] for field in CONFIG_FIELDS if field in original},indent=2,ensure_ascii=False)+'\n')

def build_agent(use_original_credentials:bool=False)->OriginalAgent:
    original=load_configuration()
    sources=list(original.get('apiBaseUrls',[]));bases=sources
    override=os.environ.get('SPARC_AGENT_BASE_URL','').rstrip('/')
    if override:
        if ALLOWED.get(override)!='apiKey':raise AgentFailure('provider_not_allowed')
        bases=[override]+[base for base in sources if ALLOWED.get(base)!='apiKey']
    providers=[]
    for base in bases:
        if base not in ALLOWED:continue
        field=ALLOWED[base]
        variable='SPARC_AGENT_OPENROUTER_KEY' if field=='openRouterApiKey' else 'SPARC_AGENT_API_KEY'
        key=os.environ.get(variable) or (original.get(field) if use_original_credentials and base in sources else '')
        if not isinstance(key,str) or not key.strip():continue
        model=os.environ.get('SPARC_AGENT_MODEL') or str(original.get('modelName') or '')
        if field=='openRouterApiKey':
            choices=original.get('openRouterFallbackModels') or []
            if choices:model=str(choices[0])
        providers.append(Provider(base,key,model))
    return OriginalAgent(original,providers)

def client_key(handler)->str:
    # Key for the per-client limit. nginx's $proxy_add_x_forwarded_for appends the peer it saw after anything the browser sent,
    # so only the last entry is trusted, and only when the request itself comes from the loopback proxy.
    peer=handler.client_address[0]
    if peer!='127.0.0.1':return peer
    forwarded=handler.headers.get('X-Forwarded-For','').split(',')[-1].strip()
    try:return str(ipaddress.ip_address(forwarded))
    except ValueError:return peer

def handle_agent_post(handler,agent:OriginalAgent,origins=())->bool:
    # origins: public page origins (e.g. https://ping.agaii.org) whose reverse proxy forwards Host unchanged.
    if urlsplit(handler.path).path!='/api/sparc/agent':return False
    host=handler.headers.get('Host','');port=handler.server.server_port
    origin=handler.headers.get('Origin')
    loopback=host in {f'127.0.0.1:{port}',f'localhost:{port}'} and origin=='http://'+host
    public=origin in origins and host==urlsplit(origin).netloc
    if not (loopback or public) or handler.headers.get('Sec-Fetch-Site','same-origin') not in ('same-origin','none'):
        result,status={'ok':False,'code':'origin_rejected'},403
    elif handler.headers.get('Content-Type','').split(';')[0].strip()!='application/json':result,status={'ok':False,'code':'json_required'},415
    else:
        try:
            length=int(handler.headers.get('Content-Length','-1'))
            if length<1 or length>8192:raise AgentFailure('invalid_request_size')
            handler.connection.settimeout(20)
            data=handler.rfile.read(length)
            result=agent.reply(json.loads(data),client_key(handler));status=200
        except AgentFailure as error:result,status={'ok':False,'code':error.code},200
        except Exception:result,status={'ok':False,'code':'invalid_request'},400
    encoded=json.dumps(result).encode();handler.send_response(status);handler.send_header('Content-Type','application/json');handler.send_header('Cache-Control','no-store');handler.send_header('Content-Length',str(len(encoded)));handler.end_headers();handler.wfile.write(encoded);return True
