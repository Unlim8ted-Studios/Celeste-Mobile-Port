package com.unlim8ted.celeste;

import android.app.Activity;
import android.graphics.Color;
import android.os.Bundle;
import android.view.View;
import android.view.Window;
import android.view.WindowManager;
import android.widget.FrameLayout;
import android.widget.TextView;

import java.io.File;
import java.io.FileWriter;
import java.io.IOException;

import org.mozilla.geckoview.GeckoRuntime;
import org.mozilla.geckoview.GeckoRuntimeSettings;
import org.mozilla.geckoview.GeckoSession;
import org.mozilla.geckoview.GeckoView;

public final class MainActivity extends Activity {
    private LocalAssetServer assetServer;
    private GeckoRuntime runtime;
    private GeckoSession session;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        configureWindow();

        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(Color.BLACK);
        setContentView(root);

        try {
            assetServer = new LocalAssetServer(this, "www");
            assetServer.start();
            File geckoConfig = writeGeckoConfig();

            GeckoRuntimeSettings settings = new GeckoRuntimeSettings.Builder()
                .aboutConfigEnabled(true)
                .consoleOutput(true)
                .debugLogging(true)
                .configFilePath(geckoConfig.getAbsolutePath())
                .build();
            runtime = GeckoRuntime.create(this, settings);

            session = new GeckoSession();
            session.open(runtime);

            GeckoView view = new GeckoView(this);
            view.setBackgroundColor(Color.BLACK);
            root.addView(view, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT,
                FrameLayout.LayoutParams.MATCH_PARENT
            ));
            view.setSession(session);

            session.loadUri(assetServer.getRootUrl());
        } catch (Exception ex) {
            TextView error = new TextView(this);
            error.setTextColor(Color.WHITE);
            error.setTextSize(14f);
            error.setPadding(32, 32, 32, 32);
            error.setText("Celeste failed to start.\n\n" + ex);
            root.addView(error);
        }
    }

    @Override
    protected void onDestroy() {
        if (session != null) {
            session.close();
            session = null;
        }
        if (assetServer != null) {
            assetServer.stop();
            assetServer = null;
        }
        super.onDestroy();
    }

    private void configureWindow() {
        requestWindowFeature(Window.FEATURE_NO_TITLE);
        Window window = getWindow();
        window.setFlags(WindowManager.LayoutParams.FLAG_FULLSCREEN, WindowManager.LayoutParams.FLAG_FULLSCREEN);
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        window.getDecorView().setSystemUiVisibility(
            View.SYSTEM_UI_FLAG_FULLSCREEN
                | View.SYSTEM_UI_FLAG_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_IMMERSIVE_STICKY
                | View.SYSTEM_UI_FLAG_LAYOUT_FULLSCREEN
                | View.SYSTEM_UI_FLAG_LAYOUT_HIDE_NAVIGATION
                | View.SYSTEM_UI_FLAG_LAYOUT_STABLE
        );
    }

    private File writeGeckoConfig() throws IOException {
        File config = new File(getFilesDir(), "geckoview-config.yaml");
        try (FileWriter writer = new FileWriter(config, false)) {
            writer.write("prefs:\n");
            writer.write("  javascript.options.shared_memory: true\n");
            writer.write("  javascript.options.wasm_threads: true\n");
            writer.write("  dom.workers.maxPerDomain: 64\n");
        }
        return config;
    }
}
