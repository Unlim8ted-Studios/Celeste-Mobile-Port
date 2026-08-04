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
    private final AssetManager assets;
    private final String root;
    private final ExecutorService workers = Executors.newCachedThreadPool();
    private ServerSocket server;
    private Thread acceptThread;
    private volatile boolean running;

    LocalAssetServer(Context context, String root) {
        this.assets = context.getAssets();
        this.root = root;
    }

    void start() throws IOException {
        server = new ServerSocket(0, 64, InetAddress.getByName("127.0.0.1"));
        running = true;
        acceptThread = new Thread(this::acceptLoop, "celeste-asset-server");
        acceptThread.setDaemon(true);
        acceptThread.start();
    }

    String getRootUrl() {
        return "http://127.0.0.1:" + server.getLocalPort() + "/";
    }

    void stop() {
        running = false;
        closeQuietly(server);
        workers.shutdownNow();
        try {
            workers.awaitTermination(1, TimeUnit.SECONDS);
        } catch (InterruptedException ex) {
            Thread.currentThread().interrupt();
        }
    }

    private void acceptLoop() {
        while (running) {
            try {
                Socket socket = server.accept();
                workers.execute(() -> handle(socket));
            } catch (IOException ignored) {
                if (running) {
                    running = false;
                }
            }
        }
    }

    private void handle(Socket socket) {
        try (Socket s = socket;
             BufferedReader reader = new BufferedReader(new InputStreamReader(s.getInputStream(), StandardCharsets.US_ASCII));
             BufferedOutputStream out = new BufferedOutputStream(s.getOutputStream())) {
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
        } catch (IOException ignored) {
            Log.e(TAG, "request failed", ignored);
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
        String assetPath = root + "/" + path;
        try {
            AssetFileDescriptor afd = assets.openFd(assetPath);
            return new AssetResponse(new BufferedInputStream(afd.createInputStream()), afd.getLength(), mimeType(path));
        } catch (IOException ignored) {
            try {
                InputStream stream = assets.open(assetPath, AssetManager.ACCESS_STREAMING);
                return new AssetResponse(new BufferedInputStream(stream), -1, mimeType(path));
            } catch (IOException missing) {
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

        for (int index = 0; ; index++) {
            String chunkPath = root + "/" + path + index;
            try {
                AssetFileDescriptor afd = assets.openFd(chunkPath);
                streams.add(new BufferedInputStream(afd.createInputStream()));
                length += afd.getLength();
            } catch (IOException fdError) {
                try {
                    InputStream stream = assets.open(chunkPath, AssetManager.ACCESS_STREAMING);
                    streams.add(new BufferedInputStream(stream));
                    knownLength = false;
                } catch (IOException missing) {
                    if (index == 0) {
                        return null;
                    }
                    break;
                }
            }
        }

        return new AssetResponse(new SequenceInputStream(Collections.enumeration(streams)), knownLength ? length : -1, "application/wasm");
    }

    private String sanitizePath(String rawPath) {
        String path = rawPath;
        int query = path.indexOf('?');
        if (query >= 0) {
            path = path.substring(0, query);
        }
        path = Uri.decode(path);
        if (path == null || path.equals("/") || path.isEmpty()) {
            return "index.html";
        }
        while (path.startsWith("/")) {
            path = path.substring(1);
        }
        if (path.contains("..") || path.contains("\\") || path.startsWith(".")) {
            return "__invalid__";
        }
        if (path.endsWith("/")) {
            return path + "index.html";
        }
        return path;
    }

    private void drainHeaders(BufferedReader reader) throws IOException {
        String line;
        while ((line = reader.readLine()) != null && !line.isEmpty()) {
            // Drain request headers before writing the response.
        }
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
        byte[] buffer = new byte[128 * 1024];
        int read;
        while ((read = in.read(buffer)) != -1) {
            out.write(buffer, 0, read);
        }
        out.flush();
    }

    private String mimeType(String path) {
        String lower = path.toLowerCase(Locale.US);
        if (lower.endsWith(".html")) return "text/html; charset=utf-8";
        if (lower.endsWith(".js")) return "text/javascript; charset=utf-8";
        if (lower.endsWith(".mjs")) return "text/javascript; charset=utf-8";
        if (lower.endsWith(".css")) return "text/css; charset=utf-8";
        if (lower.endsWith(".json")) return "application/json; charset=utf-8";
        if (lower.endsWith(".wasm")) return "application/wasm";
        if (lower.endsWith(".png")) return "image/png";
        if (lower.endsWith(".jpg") || lower.endsWith(".jpeg")) return "image/jpeg";
        if (lower.endsWith(".svg")) return "image/svg+xml";
        if (lower.endsWith(".ico")) return "image/x-icon";
        if (lower.endsWith(".txt") || lower.endsWith(".md")) return "text/plain; charset=utf-8";
        return "application/octet-stream";
    }

    private void closeQuietly(Closeable closeable) {
        if (closeable == null) return;
        try {
            closeable.close();
        } catch (IOException ignored) {
        }
    }

    private static final class AssetResponse {
        final InputStream stream;
        final long length;
        final String mimeType;

        AssetResponse(InputStream stream, long length, String mimeType) {
            this.stream = stream;
            this.length = length;
            this.mimeType = mimeType;
        }
    }
}
