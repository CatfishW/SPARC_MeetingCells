#!/usr/bin/env python3
"""Loopback preview server for the packaged ORIGINAL MeetingCells release.
Run: python3 serve-preview.py --port 8092
Optional provider keys are read from SPARC_AGENT_API_KEY / SPARC_AGENT_OPENROUTER_KEY.
No keys are bundled. Without a configured service, the original service-unavailable branch remains available.
"""
from functools import partial
from http.server import ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit
import argparse
import json
import os
import sys

ROOT = Path(__file__).resolve().parent
sys.path.insert(0, str(ROOT / 'server'))
from original_agent_proxy import ALLOWED, OriginalAgent, Provider
from serve_webgl import Handler

class PreviewHandler(Handler):
    def send_head(self):
        name = unquote(urlsplit(self.path).path)
        if name == '/favicon.ico':
            self.send_response(204); self.end_headers(); return None
        allowed = name in ('/', '/index.html', '/appearance-handoff.js', '/browser-platform.js') or name.startswith(('/Build/', '/StreamingAssets/'))
        if not allowed or '..' in Path(name).parts or '\\' in name:
            self.send_error(404, 'Not a game resource'); return None
        return super().send_head()


def configured_agent(configuration):
    providers = []
    for base in configuration.get('apiBaseUrls', []):
        if base not in ALLOWED: continue
        field = ALLOWED[base]
        variable = 'SPARC_AGENT_OPENROUTER_KEY' if field == 'openRouterApiKey' else 'SPARC_AGENT_API_KEY'
        key = os.environ.get(variable, '').strip()
        if not key: continue
        model = str(configuration.get('modelName') or '')
        if field == 'openRouterApiKey' and configuration.get('openRouterFallbackModels'):
            model = str(configuration['openRouterFallbackModels'][0])
        providers.append(Provider(base, key, model))
    return OriginalAgent(configuration, providers)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=8092)
    parser.add_argument('--fallback', action='store_true', help='Exercise the tested JavaScript decompression fallback')
    args = parser.parse_args()
    if not 1024 <= args.port <= 65535: parser.error('Use a port between 1024 and 65535.')
    if not (ROOT / 'index.html').is_file(): parser.error('Extract the complete release ZIP before running this server.')
    config_file = ROOT / 'server/agent-config.json'
    if not config_file.is_file(): parser.error('The packaged server configuration is missing.')
    configuration = json.loads(config_file.read_text(encoding='utf-8'))
    PreviewHandler.agent = configured_agent(configuration)
    PreviewHandler.fallback = args.fallback
    try: server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(PreviewHandler, directory=str(ROOT)))
    except OSError as error: parser.error(f'Cannot open the local port: {error}')
    count = PreviewHandler.agent.metadata['configuredProviders']
    print(f'Meeting Cells: http://127.0.0.1:{args.port}/\nConfigured feedback providers: {count}\nPress Ctrl+C to stop.', flush=True)
    if not count:
        print('No feedback-service key was supplied. No external request will be made; the original unavailable-service fallback is used.', flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally: server.server_close()

if __name__ == '__main__':
    main()
