from http.server import SimpleHTTPRequestHandler, ThreadingHTTPServer
from pathlib import Path
from urllib.parse import unquote, urlsplit
import webbrowser


ROOT = Path(__file__).resolve().parent
HOST = "127.0.0.1"


class UnityWebGLHandler(SimpleHTTPRequestHandler):
    def __init__(self, *args, **kwargs):
        super().__init__(*args, directory=str(ROOT), **kwargs)

    def guess_type(self, path):
        if path.endswith(".wasm.unityweb"):
            return "application/wasm"
        if path.endswith(".framework.js.unityweb"):
            return "application/javascript"
        if path.endswith(".data.unityweb"):
            return "application/octet-stream"
        return super().guess_type(path)

    def end_headers(self):
        if urlsplit(unquote(self.path)).path.endswith(".unityweb"):
            self.send_header("Content-Encoding", "gzip")
        super().end_headers()


if __name__ == "__main__":
    server = ThreadingHTTPServer((HOST, 0), UnityWebGLHandler)
    address = f"http://{HOST}:{server.server_port}/"
    print(f"Serving Rally Pioneers at {address}")
    print("Keep this window open while playing. Press Ctrl+C to stop.")
    webbrowser.open(address)
    server.serve_forever()
