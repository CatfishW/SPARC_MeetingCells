#!/usr/bin/env python3
"""Production peer-agent service for the published WebGL build. Binds loopback only;
the public web server forwards POST /api/sparc/agent here with the original Host header.
Usage: python3 Tools/agent_service.py --port 28093 --public-origin https://ping.agaii.org
       python3 Tools/agent_service.py --export-config OUT.json   (key-free settings from the private inventory)
Environment: SPARC_AGENT_API_KEY, SPARC_AGENT_BASE_URL (allow-listed), SPARC_AGENT_MODEL,
SPARC_AGENT_OPENROUTER_KEY (optional fallback), SPARC_AGENT_CONFIG (settings file).
Limits (defaults in brackets): SPARC_AGENT_RATE_PER_MINUTE [12], SPARC_AGENT_CLIENT_RATE_PER_MINUTE [12],
SPARC_AGENT_MAX_IN_FLIGHT [2], SPARC_AGENT_QUEUE_SECONDS [8, at most 8], SPARC_AGENT_MAX_SESSIONS [64].
"""
import argparse
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
from urllib.parse import urlsplit
from original_agent_proxy import build_agent, export_configuration, handle_agent_post

class Server(ThreadingHTTPServer):
    request_queue_size = 128  # the default backlog of 5 drops connections when a class sends at once

class Handler(BaseHTTPRequestHandler):
    agent = None
    origins = ()
    timeout = 30
    server_version = 'SparcAgent'
    sys_version = ''
    def do_POST(self):
        if not handle_agent_post(self, self.agent, self.origins): self.reply(404, {'ok': False, 'code': 'unknown_route'})
    def do_GET(self):
        # Health is loopback-only in practice: the public proxy forwards just the exact agent path.
        if urlsplit(self.path).path == '/healthz': self.reply(200, {'ok': True, **self.agent.metadata})
        else: self.reply(404, {'ok': False, 'code': 'unknown_route'})
    def reply(self, status, body):
        encoded = json.dumps(body).encode()
        self.send_response(status); self.send_header('Content-Type', 'application/json'); self.send_header('Cache-Control', 'no-store')
        self.send_header('Content-Length', str(len(encoded))); self.end_headers(); self.wfile.write(encoded)

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--port', type=int, default=28093)
    parser.add_argument('--public-origin', action='append', default=[], help='Page origin allowed through the reverse proxy; repeatable')
    parser.add_argument('--export-config', metavar='OUT', help='Write the key-free original settings and exit')
    args = parser.parse_args()
    if args.export_config:
        export_configuration(args.export_config); raise SystemExit(0)
    if not 1024 <= args.port <= 65535: parser.error('Use a port between 1024 and 65535.')
    for origin in args.public_origin:
        parts = urlsplit(origin)
        if parts.scheme != 'https' or not parts.netloc or parts.path or parts.query: parser.error(f'--public-origin must look like https://host, got {origin!r}')
    try: Handler.agent = build_agent()
    except ValueError as error: parser.error(str(error))
    Handler.origins = tuple(args.public_origin)
    print('Peer-agent service configuration:', Handler.agent.metadata, flush=True)
    if not Handler.agent.providers: print('No provider key configured; requests return service_not_configured.', flush=True)
    server = Server(('127.0.0.1', args.port), Handler)
    print(f'SPARC MeetingCells agent: http://127.0.0.1:{args.port}/api/sparc/agent', flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally: server.server_close()
