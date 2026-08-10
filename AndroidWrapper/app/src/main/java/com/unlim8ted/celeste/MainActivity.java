package com.unlim8ted.celeste;

import android.app.Activity;
import android.os.Bundle;
import android.view.View;
import android.view.Window;
import android.widget.FrameLayout;
import android.widget.TextView;
import java.io.File;
import java.io.FileWriter;
import java.io.IOException;
import org.mozilla.geckoview.GeckoRuntime;
import org.mozilla.geckoview.GeckoRuntimeSettings;
import org.mozilla.geckoview.GeckoSession;
import org.mozilla.geckoview.GeckoView;

/* JADX INFO: loaded from: /tmp/decompiler/3b2e22f821a54ff88b995449039e2e9c/classes3.dex */
public final class MainActivity extends Activity {
    private LocalAssetServer assetServer;
    private GeckoRuntime runtime;
    private GeckoSession session;

    @Override // android.app.Activity
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        configureWindow();
        FrameLayout root = new FrameLayout(this);
        root.setBackgroundColor(-16777216);
        setContentView(root);
        try {
            this.assetServer = new LocalAssetServer(this, "www");
            this.assetServer.start();
            File geckoConfig = writeGeckoConfig();
            GeckoRuntimeSettings settings = new GeckoRuntimeSettings.Builder().aboutConfigEnabled(true).consoleOutput(false).debugLogging(false).configFilePath(geckoConfig.getAbsolutePath()).build();
            this.runtime = GeckoRuntime.create(this, settings);
            this.session = new GeckoSession();
            this.session.open(this.runtime);
            GeckoView view = new GeckoView(this);
            view.setBackgroundColor(-16777216);
            root.addView((View) view, new FrameLayout.LayoutParams(-1, -1));
            view.setSession(this.session);
            this.session.loadUri(this.assetServer.getRootUrl());
        } catch (Exception ex) {
            TextView error = new TextView(this);
            error.setTextColor(-1);
            error.setTextSize(14.0f);
            error.setPadding(32, 32, 32, 32);
            error.setText("Celeste failed to start.\n\n" + ex);
            root.addView(error);
        }
    }

    @Override // android.app.Activity
    protected void onDestroy() {
        if (this.session != null) {
            this.session.close();
            this.session = null;
        }
        if (this.assetServer != null) {
            this.assetServer.stop();
            this.assetServer = null;
        }
        super.onDestroy();
    }

    private void configureWindow() {
        requestWindowFeature(1);
        Window window = getWindow();
        window.setFlags(1024, 1024);
        window.addFlags(128);
        window.getDecorView().setSystemUiVisibility(5894);
    }

    private File writeGeckoConfig() throws IOException {
        File config = new File(getFilesDir(), "geckoview-config.yaml");
        FileWriter writer = new FileWriter(config, false);
        try {
            writer.write("prefs:\n");
            writer.write("  javascript.options.shared_memory: true\n");
            writer.write("  javascript.options.wasm_threads: true\n");
            writer.write("  dom.workers.maxPerDomain: 8\n");
            writer.write("  gfx.webrender.program-binary-cache.enabled: false\n");
            writer.write("  webgl.program-binary-cache.enabled: false\n");
            writer.write("  gfx.webrender.program-binary: false\n");
            writer.write("  gfx.webrender.program-binary-disk: false\n");
            writer.close();
            return config;
        } catch (Throwable th) {
            try {
                writer.close();
            } catch (Throwable th2) {
                th.addSuppressed(th2);
            }
            throw th;
        }
    }
}
