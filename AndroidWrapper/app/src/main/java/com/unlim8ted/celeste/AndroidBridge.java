package com.unlim8ted.celeste;

import android.app.Activity;
import android.content.Intent;
import android.net.Uri;
import android.os.Build;
import android.os.VibrationEffect;
import android.os.Vibrator;
import android.util.Log;
import java.io.File;
import java.io.IOException;
import java.util.Locale;

final class AndroidBridge implements LocalAssetServer.NativeBridge {
    private static final String TAG = "CelesteAndroidBridge";
    private final Activity activity;
    private Process hostProcess;

    AndroidBridge(Activity activity) {
        this.activity = activity;
    }

    @Override
    public synchronized String handle(String command, String query) {
        try {
            switch (command) {
                case "haptic":
                    vibrate(get(query, "strength"), get(query, "length"));
                    return "1";
                case "openUrl":
                    openUrl(get(query, "url"));
                    return "1";
                case "startHost":
                    return startHost(parsePort(get(query, "port"))) ? "1" : "0";
                case "stopHost":
                    stopHost();
                    return "1";
                case "isHostRunning":
                    return isHostRunning() ? "1" : "0";
                case "getServers":
                    return isHostRunning() ? "127.0.0.1" : "";
                default:
                    Log.w(TAG, "Unknown bridge command: " + command);
                    return "";
            }
        } catch (Exception ex) {
            Log.e(TAG, "Bridge command failed: " + command, ex);
            return "";
        }
    }

    synchronized void stop() {
        stopHost();
    }

    private void vibrate(String strengthText, String lengthText) {
        long length = clamp(parseLong(lengthText, 35), 10, 500);
        int amplitude = (int)clamp(parseStrength(strengthText), 1, 255);
        Vibrator vibrator = (Vibrator)activity.getSystemService(Activity.VIBRATOR_SERVICE);
        if (vibrator == null || !vibrator.hasVibrator()) {
            return;
        }
        if (Build.VERSION.SDK_INT >= 26) {
            vibrator.vibrate(VibrationEffect.createOneShot(length, amplitude));
        } else {
            vibrator.vibrate(length);
        }
    }

    private void openUrl(String url) {
        if (url == null || url.trim().isEmpty()) {
            return;
        }
        Uri uri = Uri.parse(url);
        Intent intent = new Intent(Intent.ACTION_VIEW, uri);
        intent.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
        activity.startActivity(intent);
    }

    private boolean startHost(int port) {
        if (isHostRunning()) {
            return true;
        }

        File executable = new File(activity.getFilesDir(), "CelesteNetServer/CelesteNet.Server");
        if (!executable.isFile()) {
            executable = new File(activity.getFilesDir(), "CelesteNetServer/CelesteNet.Server.dll");
        }
        if (!executable.isFile()) {
            Log.w(TAG, "CelesteNet host executable is not packaged. Expected " + executable.getAbsolutePath());
            return false;
        }

        File workingDir = executable.getParentFile();
        File config = new File(workingDir, "celestenet-config.yaml");
        writeMinimalConfig(config, port);

        ProcessBuilder builder;
        if (executable.getName().endsWith(".dll")) {
            builder = new ProcessBuilder("dotnet", executable.getAbsolutePath(), "--nolog");
        } else {
            executable.setExecutable(true, false);
            builder = new ProcessBuilder(executable.getAbsolutePath(), "--nolog");
        }
        builder.directory(workingDir);
        builder.redirectErrorStream(true);

        try {
            hostProcess = builder.start();
            Log.i(TAG, "Started CelesteNet host on port " + port);
            return true;
        } catch (IOException ex) {
            Log.e(TAG, "Failed to start CelesteNet host", ex);
            hostProcess = null;
            return false;
        }
    }

    private void stopHost() {
        if (hostProcess != null) {
            hostProcess.destroy();
            hostProcess = null;
        }
    }

    private boolean isHostRunning() {
        return hostProcess != null && hostProcess.isAlive();
    }

    private static void writeMinimalConfig(File config, int port) {
        try {
            File parent = config.getParentFile();
            if (parent != null) {
                parent.mkdirs();
            }
            java.io.FileWriter writer = new java.io.FileWriter(config, false);
            try {
                writer.write("MainPort: " + port + "\n");
                writer.write("ModuleRoot: Modules\n");
                writer.write("ModuleConfigRoot: ModuleConfigs\n");
                writer.write("UserDataRoot: UserData\n");
                writer.write("TCPRecvUseEPoll: false\n");
            } finally {
                writer.close();
            }
        } catch (IOException ex) {
            Log.w(TAG, "Failed to write CelesteNet config", ex);
        }
    }

    private static int parsePort(String value) {
        long port = parseLong(value, 17230);
        return (int)clamp(port, 1, 65535);
    }

    private static long parseStrength(String value) {
        if (value == null) {
            return 128;
        }
        String lower = value.toLowerCase(Locale.US);
        if (lower.contains("strong") || lower.contains("heavy")) {
            return 255;
        }
        if (lower.contains("weak") || lower.contains("light")) {
            return 80;
        }
        return clamp(parseLong(value, 128), 1, 255);
    }

    private static long parseLong(String value, long fallback) {
        if (value == null) {
            return fallback;
        }
        try {
            return Long.parseLong(value.trim());
        } catch (NumberFormatException ex) {
            return fallback;
        }
    }

    private static long clamp(long value, long min, long max) {
        return Math.max(min, Math.min(max, value));
    }

    private static String get(String query, String key) {
        if (query == null) {
            return "";
        }
        for (String part : query.split("&")) {
            int equals = part.indexOf('=');
            String rawKey = equals >= 0 ? part.substring(0, equals) : part;
            if (!key.equals(Uri.decode(rawKey))) {
                continue;
            }
            return Uri.decode(equals >= 0 ? part.substring(equals + 1) : "");
        }
        return "";
    }
}
