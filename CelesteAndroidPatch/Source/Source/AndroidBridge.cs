using System;
using System.Runtime.InteropServices.JavaScript;

namespace Celeste.Mod.AndroidPort;

internal static partial class AndroidBridge {
    [JSImport("celesteAndroidHaptic", "android-port.js")]
    private static partial void jsHaptic(string strength, string length);

    [JSImport("celesteAndroidOpenUrl", "android-port.js")]
    private static partial void jsOpenUrl(string url);

    [JSImport("celesteAndroidOpenModBrowser", "android-port.js")]
    private static partial void jsOpenModBrowser();

    [JSImport("celesteAndroidOpenSaveData", "android-port.js")]
    private static partial void jsOpenSaveData();

    [JSImport("celesteAndroidOpenFileManager", "android-port.js")]
    private static partial void jsOpenFileManager();

    [JSImport("celesteAndroidOpenLayoutEditor", "android-port.js")]
    private static partial void jsOpenLayoutEditor();

    [JSImport("celesteAndroidResetGame", "android-port.js")]
    private static partial void jsResetGame();

    [JSImport("celesteAndroidSetOption", "android-port.js")]
    private static partial void jsSetOption(string key, string value);

    [JSImport("celesteAndroidConsumeTouchTap", "android-port.js")]
    private static partial bool jsConsumeTouchTap();

    [JSImport("celesteAndroidTouchX", "android-port.js")]
    private static partial double jsTouchX();

    [JSImport("celesteAndroidTouchY", "android-port.js")]
    private static partial double jsTouchY();

    [JSImport("celesteAndroidConsumeTouchScroll", "android-port.js")]
    private static partial double jsConsumeTouchScroll();

    public static void Haptic(string strength, string length) {
        invoke(() => jsHaptic(strength, length));
    }

    public static void OpenUrlPrompt(string url) {
        invoke(() => jsOpenUrl(url));
    }

    public static void OpenModBrowser() {
        invoke(jsOpenModBrowser);
    }

    public static void OpenSaveData() {
        invoke(jsOpenSaveData);
    }

    public static void OpenFileManager() {
        invoke(jsOpenFileManager);
    }

    public static void OpenLayoutEditor() {
        invoke(jsOpenLayoutEditor);
    }

    public static void ResetGame() {
        invoke(jsResetGame);
    }

    public static void SetOption(string key, bool enabled) {
        invoke(() => jsSetOption(key, enabled ? "true" : "false"));
    }

    public static bool ConsumeTouchTap() {
        try {
            return jsConsumeTouchTap();
        } catch (Exception) {
            return false;
        }
    }

    public static Vector2Like TouchPosition() {
        try {
            return new Vector2Like((float) jsTouchX(), (float) jsTouchY());
        } catch (Exception) {
            return new Vector2Like(-1f, -1f);
        }
    }

    public static float ConsumeTouchScroll() {
        try {
            return (float) jsConsumeTouchScroll();
        } catch (Exception) {
            return 0f;
        }
    }

    private static void invoke(Action action) {
        try {
            action();
        } catch (Exception) {
            // Desktop Everest and vanilla WASM builds do not expose the Android JS bridge.
        }
    }

    public readonly struct Vector2Like {
        public readonly float X;
        public readonly float Y;

        public Vector2Like(float x, float y) {
            X = x;
            Y = y;
        }
    }
}
