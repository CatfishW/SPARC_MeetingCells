#!/usr/bin/env python3
"""Serve the original-scene rebuild locally with correct MIME/encoding. No directory listings.
Usage: python3 Tools/serve_webgl.py [--port 8092] [--development] [--fallback]
The default is the original-scene RELEASE. --development selects read-only test telemetry.
Only binds loopback; use your production web server for public deployment.
"""
import argparse
from functools import partial
from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
import urllib.parse
import re
from original_agent_proxy import build_agent, handle_agent_post

class Handler(SimpleHTTPRequestHandler):
    fallback = False
    agent = None
    def do_POST(self):
        if self.agent and handle_agent_post(self,self.agent):return
        self.send_error(404,'Unknown route')
    _remaining = None
    def send_head(self):
        self._remaining = None
        path = Path(self.translate_path(self.path)).resolve()
        root = Path(self.directory).resolve()
        if path != root and root not in path.parents:
            self.send_error(403, 'Path is outside the build')
            return None
        header = self.headers.get('Range')
        if header and path.is_file() and path.suffix.lower() in ('.mp4', '.webm', '.ogg'):
            size = path.stat().st_size
            match = re.fullmatch(r'bytes=(\d*)-(\d*)', header.strip())
            if not match or not any(match.groups()) or size == 0:
                self.send_error(416, 'Unsupported byte range')
                return None
            if not match.group(1):
                start, end = max(0, size-int(match.group(2))), size-1
            else:
                start = int(match.group(1)); end = min(size-1, int(match.group(2)) if match.group(2) else size-1)
            if start >= size or end < start:
                self.send_response(416); self.send_header('Content-Range', f'bytes */{size}'); self.send_header('Content-Length', '0'); self.end_headers()
                return None
            stream = path.open('rb'); stream.seek(start); self._remaining = end-start+1
            self.send_response(206); self.send_header('Content-Type', self.guess_type(str(path)))
            self.send_header('Accept-Ranges', 'bytes'); self.send_header('Content-Range', f'bytes {start}-{end}/{size}')
            self.send_header('Content-Length', str(self._remaining)); self.end_headers()
            return stream
        return super().send_head()
    def copyfile(self, source, outputfile):
        if self._remaining is None:
            return super().copyfile(source, outputfile)
        remaining = self._remaining
        while remaining > 0:
            data = source.read(min(1024*1024, remaining))
            if not data: break
            outputfile.write(data); remaining -= len(data)

    def list_directory(self, path):
        self.send_error(404, 'Directory listing is disabled')
        return None
    def guess_type(self, path):
        plain = path[:-9] if path.endswith('.unityweb') else path
        if plain.endswith('.wasm'): return 'application/wasm'
        if plain.endswith('.js'): return 'application/javascript'
        if plain.endswith('.data') or path.endswith('.unityweb'): return 'application/octet-stream'
        return super().guess_type(path)
    def end_headers(self):
        if urllib.parse.urlsplit(self.path).path.endswith('.unityweb') and not self.fallback:
            self.send_header('Content-Encoding', 'gzip')
        self.send_header('X-Content-Type-Options', 'nosniff')
        self.send_header('Cache-Control', 'no-store')
        super().end_headers()

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--port', type=int, default=8092)
    parser.add_argument('--use-original-agent', action='store_true', help='Use the original private provider credentials server-side for the unchanged peer-agent prompt; never expose them to the player')
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument('--development', action='store_true', help='Use the original-scene development build with read-only test observations')
    selection.add_argument('--rejected-expedition', action='store_true', help='Explicitly open the superseded, non-default redesign instead of the original-scene rebuild')
    parser.add_argument('--fallback', action='store_true', help='Exercise Unity JS decompression instead of HTTP gzip decoding')
    args = parser.parse_args()
    build_folder = 'Builds/WebGL' if args.rejected_expedition else ('Builds/WebGLOriginal' if args.development else 'Builds/WebGLOriginalRelease')
    root = Path(__file__).resolve().parents[1] / build_folder
    if not (root / 'index.html').is_file(): parser.error(f'{build_folder}/index.html is missing. Build the selected scene first.')
    if not 1024 <= args.port <= 65535: parser.error('Use a port between 1024 and 65535.')
    Handler.fallback = args.fallback
    Handler.agent = build_agent(args.use_original_agent)
    print('Original peer-agent server configuration:', Handler.agent.metadata, flush=True)
    server = ThreadingHTTPServer(('127.0.0.1', args.port), partial(Handler, directory=str(root)))
    print(f'SPARC MeetingCells: http://127.0.0.1:{args.port}/\nPress Ctrl+C to stop.', flush=True)
    try: server.serve_forever()
    except KeyboardInterrupt: pass
    finally: server.server_close()
