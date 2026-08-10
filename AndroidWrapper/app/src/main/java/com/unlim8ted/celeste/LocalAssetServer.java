package com.unlim8ted.celeste;

import android.content.Context;
import android.content.res.AssetFileDescriptor;
import android.content.res.AssetManager;
import android.net.Uri;
import android.util.Log;
import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.BufferedReader;
import java.io.Closeable;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.io.SequenceInputStream;
import java.net.InetAddress;
import java.net.ServerSocket;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.Locale;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

final class LocalAssetServer {
    private static final String TAG = "CelesteAssetServer";
    private Thread acceptThread;
    private final AssetManager assets;
    private final String root;
    private final NativeBridge nativeBridge;
    private volatile boolean running;
    private ServerSocket server;
    private final ExecutorService workers = Executors.newCachedThreadPool();

    LocalAssetServer(Context context, String root, NativeBridge nativeBridge) {
        this.assets = context.getAssets();
        this.root = root;
        this.nativeBridge = nativeBridge;
    }

    void start() throws IOException {
        this.server = new ServerSocket(8080, 64, InetAddress.getByName("127.0.0.1"));
        this.running = true;
        this.acceptThread = new Thread(() -> acceptLoop(), "celeste-asset-server");
        this.acceptThread.setDaemon(true);
        this.acceptThread.start();
    }

    String getRootUrl() {
        return "http://127.0.0.1:" + this.server.getLocalPort() + "/";
    }

    void stop() {
        this.running = false;
        closeQuietly(this.server);
        this.workers.shutdownNow();
        try {
            this.workers.awaitTermination(1L, TimeUnit.SECONDS);
        } catch (InterruptedException e) {
            Thread.currentThread().interrupt();
        }
    }

    private void acceptLoop() {
        while (this.running) {
            try {
                final Socket socket = this.server.accept();
                this.workers.execute(() -> handle(socket));
            } catch (IOException e) {
                if (this.running) {
                    this.running = false;
                }
            }
        }
    }

    private void handle(Socket socket) {
        try (BufferedReader reader = new BufferedReader(new InputStreamReader(socket.getInputStream(), StandardCharsets.US_ASCII));
             BufferedOutputStream out = new BufferedOutputStream(socket.getOutputStream())) {

            String request = reader.readLine();
            if (request == null || request.isEmpty()) {
                return;
            }

            String[] parts = request.split(" ");
            if (parts.length < 2) {
                sendText(out, 400, "Bad Request", "Bad Request");
                return;
            }

            String method = parts[0];
            String rawTarget = parts[1];

            drainHeaders(reader);

            if (!"GET".equals(method) && !"HEAD".equals(method)) {
                sendText(out, 405, "Method Not Allowed", "Method Not Allowed");
                return;
            }

            if (rawTarget.startsWith("/__android_port_log")) {
                Log.i(TAG, "JS " + Uri.decode(rawTarget));
                sendText(out, 204, "No Content", "");
                return;
            }

            if (rawTarget.startsWith("/__android_bridge/")) {
                String target = rawTarget.substring("/__android_bridge/".length());
                int queryStart = target.indexOf('?');
                String command = Uri.decode(queryStart >= 0 ? target.substring(0, queryStart) : target);
                String query = queryStart >= 0 ? target.substring(queryStart + 1) : "";
                String response = this.nativeBridge == null ? "" : this.nativeBridge.handle(command, query);
                sendText(out, 200, "OK", response == null ? "" : response);
                return;
            }

            String path = sanitizePath(rawTarget);
            AssetResponse asset = openAsset(path);

            if (asset == null) {
                Log.w(TAG, "404 " + method + " /" + path);
                sendText(out, 404, "Not Found", "Not Found");
                return;
            }

            try (InputStream in = asset.stream) {
                Log.i(TAG, "200 " + method + " /" + path + " " + asset.mimeType + " length=" + asset.length);
                writeHeaders(out, 200, "OK", asset.mimeType, asset.length);
                if (!"HEAD".equals(method)) {
                    copy(in, out);
                }
            }
        } catch (IOException e) {
            Log.e(TAG, "request failed", e);
        } finally {
            closeQuietly(socket);
        }
    }

    private AssetResponse openAsset(String path) throws IOException {
        if ("__invalid__".equals(path)) {
            return null;
        }
        AssetResponse chunkedWasm = openChunkedWasm(path);
        if (chunkedWasm != null) {
            return chunkedWasm;
        }
        String assetPath = this.root + "/" + path;
        
        try {
            AssetFileDescriptor afd = this.assets.openFd(assetPath);
            return new AssetResponse(new BufferedInputStream(afd.createInputStream()), afd.getLength(), mimeType(path));
        } catch (IOException e) {
            // Not found or compressed (though we disabled compression in Gradle)
            try {
                InputStream stream = this.assets.open(assetPath, AssetManager.ACCESS_STREAMING);
                return new AssetResponse(new BufferedInputStream(stream), -1L, mimeType(path));
            } catch (IOException e2) {
                return null;
            }
        }
    }

    private AssetResponse openChunkedWasm(String path) throws IOException {
        if (!path.endsWith(".wasm")) {
            return null;
        }
        List<InputStream> streams = new ArrayList<>();
        long length = 0;
        boolean knownLength = true;
        int index = 0;
        while (true) {
            String chunkPath = this.root + "/" + path + index;
            try {
                AssetFileDescriptor afd = this.assets.openFd(chunkPath);
                streams.add(new BufferedInputStream(afd.createInputStream()));
                length += afd.getLength();
            } catch (IOException e) {
                try {
                    InputStream stream = this.assets.open(chunkPath, AssetManager.ACCESS_STREAMING);
                    streams.add(new BufferedInputStream(stream));
                    knownLength = false;
                } catch (IOException e2) {
                    if (index == 0) {
                        return null;
                    }
                    return new AssetResponse(new SequenceInputStream(Collections.enumeration(streams)), knownLength ? length : -1L, "application/wasm");
                }
            }
            index++;
        }
    }

    interface NativeBridge {
        String handle(String command, String query);
    }

    private String sanitizePath(String rawPath) {
        String path = rawPath;
        int query = path.indexOf('?');
        if (query >= 0) {
            path = path.substring(0, query);
        }
        String path2 = Uri.decode(path);
        if (path2 == null || path2.equals("/") || path2.isEmpty()) {
            return "index.html";
        }
        while (path2.startsWith("/")) {
            path2 = path2.substring(1);
        }
        if (path2.contains("..") || path2.contains("\\") || path2.startsWith(".")) {
            return "__invalid__";
        }
        if (path2.endsWith("/")) {
            return path2 + "index.html";
        }
        return path2;
    }

    private void drainHeaders(BufferedReader reader) throws IOException {
        String line;
        do {
            line = reader.readLine();
            if (line == null) {
                return;
            }
        } while (!line.isEmpty());
    }

    private void sendText(OutputStream out, int status, String reason, String text) throws IOException {
        byte[] body = text.getBytes(StandardCharsets.UTF_8);
        writeHeaders(out, status, reason, "text/plain; charset=utf-8", body.length);
        out.write(body);
        out.flush();
    }

    private void writeHeaders(OutputStream out, int status, String reason, String contentType, long length) throws IOException {
        StringBuilder headers = new StringBuilder();
        headers.append("HTTP/1.1 ").append(status).append(' ').append(reason).append("\r\n");
        headers.append("Content-Type: ").append(contentType).append("\r\n");
        if (length >= 0) {
            headers.append("Content-Length: ").append(length).append("\r\n");
        }
        headers.append("Connection: close\r\n");
        headers.append("Cache-Control: no-cache\r\n");
        headers.append("Cross-Origin-Opener-Policy: same-origin\r\n");
        headers.append("Cross-Origin-Embedder-Policy: require-corp\r\n");
        headers.append("Cross-Origin-Resource-Policy: cross-origin\r\n");
        headers.append("Permissions-Policy: cross-origin-isolated=*\r\n");
        headers.append("\r\n");
        out.write(headers.toString().getBytes(StandardCharsets.US_ASCII));
    }

    private void copy(InputStream in, OutputStream out) throws IOException {
        byte[] buffer = new byte[131072];
        while (true) {
            int read = in.read(buffer);
            if (read != -1) {
                out.write(buffer, 0, read);
            } else {
                out.flush();
                return;
            }
        }
    }

    private String mimeType(String path) {
        String lower = path.toLowerCase(Locale.US);
        if (lower.endsWith(".html")) {
            return "text/html; charset=utf-8";
        }
        if (lower.endsWith(".js") || lower.endsWith(".mjs")) {
            return "text/javascript; charset=utf-8";
        }
        if (lower.endsWith(".css")) {
            return "text/css; charset=utf-8";
        }
        if (lower.endsWith(".json")) {
            return "application/json; charset=utf-8";
        }
        if (lower.endsWith(".wasm")) {
            return "application/wasm";
        }
        if (lower.endsWith(".png")) {
            return "image/png";
        }
        if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) {
            return "image/jpeg";
        }
        if (lower.endsWith(".svg")) {
            return "image/svg+xml";
        }
        if (lower.endsWith(".ico")) {
            return "image/x-icon";
        }
        if (lower.endsWith(".txt") || lower.endsWith(".md")) {
            return "text/plain; charset=utf-8";
        }
        return "application/octet-stream";
    }

    private void closeQuietly(Closeable closeable) {
        if (closeable == null) {
            return;
        }
        try {
            closeable.close();
        } catch (IOException e) {
        }
    }

    private static final class AssetResponse {
        final long length;
        final String mimeType;
        final InputStream stream;

        AssetResponse(InputStream stream, long length, String mimeType) {
            this.stream = stream;
            this.length = length;
            this.mimeType = mimeType;
        }
    }
}
