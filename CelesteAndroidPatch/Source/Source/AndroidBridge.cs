using System;
using System.Runtime.InteropServices;
#if BROWSER
using System.Runtime.InteropServices.JavaScript;
#endif
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.AndroidPort;

internal static partial class AndroidBridge {
#if BROWSER
    public static bool IsBrowser => true;
#else
    public static bool IsBrowser => OperatingSystem.IsBrowser();
#endif

#if BROWSER
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
#else
    private static void jsHaptic(string strength, string length) {}
    private static void jsOpenUrl(string url) {}
    private static void jsOpenModBrowser() {}
    private static void jsOpenSaveData() {}
    private static void jsOpenFileManager() {}
    private static void jsOpenLayoutEditor() {}
    private static void jsResetGame() {}
    private static void jsSetOption(string key, string value) {}
    private static bool jsConsumeTouchTap() => false;
    private static double jsTouchX() => -1;
    private static double jsTouchY() => -1;
    private static double jsConsumeTouchScroll() => 0;
#endif

    public static void Haptic(string strength, string length) => invoke(() => jsHaptic(strength, length));
    public static void OpenUrlPrompt(string url) => invoke(() => jsOpenUrl(url));
    public static void OpenModBrowser() => invoke(jsOpenModBrowser);
    public static void OpenSaveData() => invoke(jsOpenSaveData);
    public static void OpenFileManager() => invoke(jsOpenFileManager);
    public static void OpenLayoutEditor() => invoke(jsOpenLayoutEditor);
    public static void ResetGame() => invoke(jsResetGame);
    public static void SetOption(string key, bool enabled) => invoke(() => jsSetOption(key, enabled ? "true" : "false"));

    private static bool desktopTapConsumed = false;
    private static Vector2 dragStartPos;
    private static bool potentialTap = false;

    public static bool TapPressed => IsBrowser ? jsConsumeTouchTap() : (MInput.Mouse.ReleasedLeftButton && potentialTap && !desktopTapConsumed);

    public static void UpdateDesktopInput() {
        if (IsBrowser) return;
        if (MInput.Mouse.PressedLeftButton) {
            dragStartPos = MInput.Mouse.Position;
            potentialTap = true;
            desktopTapConsumed = false;
        }
        if (MInput.Mouse.CheckLeftButton) {
            if (Vector2.Distance(dragStartPos, MInput.Mouse.Position) > 20f) potentialTap = false;
        }
    }

    public static bool ConsumeTouchTap() {
        if (IsBrowser) return jsConsumeTouchTap();
        if (MInput.Mouse.ReleasedLeftButton && potentialTap && !desktopTapConsumed) {
            desktopTapConsumed = true;
            potentialTap = false;
            return true;
        }
        return false;
    }

    public static void ResetDesktopTap() {
        desktopTapConsumed = false;
    }

    public static Vector2Like TouchPosition() {
        if (!IsBrowser) return new Vector2Like(MInput.Mouse.X, MInput.Mouse.Y);
        try { return new Vector2Like((float) jsTouchX(), (float) jsTouchY()); }
        catch { return new Vector2Like(-1f, -1f); }
    }

    public static float ConsumeTouchScroll() {
        if (!IsBrowser) return MInput.Mouse.WheelDelta;
        try { return (float) jsConsumeTouchScroll(); }
        catch { return 0f; }
    }

    private static void invoke(Action action) {
        if (!IsBrowser) return;
        try { action(); } catch { }
    }

    public readonly struct Vector2Like {
        public readonly float X;
        public readonly float Y;
        public Vector2Like(float x, float y) { X = x; Y = y; }
    }
}
