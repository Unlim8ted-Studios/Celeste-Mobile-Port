import http.server
import ssl

PORT = 4443
server_address = ("0.0.0.0", PORT)


class SharedArrayBufferHandler(http.server.SimpleHTTPRequestHandler):
    def end_headers(self):
        # Inject the mandatory COOP and COEP headers
        self.send_header("Cross-Origin-Opener-Policy", "same-origin")
        self.send_header("Cross-Origin-Embedder-Policy", "require-corp")

        # Optional: Prevent aggressive browser caching during development
        self.send_header("Cache-Control", "no-cache, no-store, must-revalidate")

        # Call the parent class to finalize the headers
        super().end_headers()


# Set up the server using our custom handler
httpd = http.server.HTTPServer(server_address, SharedArrayBufferHandler)


print(f"Serving COOP/COEP enabled HTTPS on https://localhost:{PORT}")
try:
    httpd.serve_forever()
except KeyboardInterrupt:
    print("\nShutting down server.")
    httpd.server_close()
