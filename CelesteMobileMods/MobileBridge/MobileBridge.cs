using System;
using System.Runtime.InteropServices;
#if BROWSER
using System.Runtime.InteropServices.JavaScript;
#endif
using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MobileBridge;

public sealed class MobileBridgeSettings : EverestModuleSettings {
    private bool touchControls = true;
    private bool joystickMode = true;
    private bool joystickSnap8Way = true;
    private bool hapticFeedback = true;

    /// <summary>
    /// Controls the gameplay touch-control overlay in the wrapper. This is not
    /// MouseUI's menu-touch enable switch; MouseUI detects touch capability
    /// directly through MobileBridgeApi.
    /// </summary>
    public bool TouchControls {
        get => touchControls;
        set {
            touchControls = value;
            MobileBridgeApi.SetOption("touch_controls", value);
        }
    }

    public bool JoystickMode {
        get => joystickMode;
        set {
            joystickMode = value;
            MobileBridgeApi.SetOption("joystick_mode", value);
        }
    }

    public bool JoystickSnap8Way {
        get => joystickSnap8Way;
        set {
            joystickSnap8Way = value;
            MobileBridgeApi.SetOption("joystick_snap_8way", value);
        }
    }

    public bool HapticFeedback {
        get => hapticFeedback;
        set {
            hapticFeedback = value;
            MobileBridgeApi.SetOption("haptic_feedback", value);
        }
    }
}

public sealed class MobileBridgeModule : EverestModule {
    public static MobileBridgeModule Instance { get; private set; }

    public override Type SettingsType => typeof(MobileBridgeSettings);

    public static MobileBridgeSettings Settings =>
        (MobileBridgeSettings)Instance._Settings;

    public MobileBridgeModule() {
        Instance = this;
    }

    public override void Load() {
        On.Celeste.Input.Rumble += OnRumble;
        On.Celeste.Overworld.ReloadMenus += OnOverworldReloadMenus;
        Everest.Events.MainMenu.OnCreateButtons += OnCreateMainMenuButtons;
    }

    public override void Initialize() {
        SyncWrapperOptions();
    }

    public override void Unload() {
        On.Celeste.Input.Rumble -= OnRumble;
        On.Celeste.Overworld.ReloadMenus -= OnOverworldReloadMenus;
        Everest.Events.MainMenu.OnCreateButtons -= OnCreateMainMenuButtons;
    }

    public override void CreateModMenuSection(
        TextMenu menu,
        bool inGame,
        FMOD.Studio.EventInstance snapshot) {

        base.CreateModMenuSection(menu, inGame, snapshot);

        // Wrapper actions are intentionally ordinary TextMenu buttons. They
        // remain keyboard/controller accessible without MouseUI and become
        // mouse/touch clickable automatically when MouseUI is installed.
        menu.Add(new TextMenu.SubHeader("MOBILE TOOLS"));
        menu.Add(new TextMenu.Button("MOD BROWSER").Pressed(MobileBridgeApi.OpenModBrowser));
        menu.Add(new TextMenu.Button("SAVE DATA MANAGER").Pressed(MobileBridgeApi.OpenSaveData));
        menu.Add(new TextMenu.Button("FILE MANAGER").Pressed(MobileBridgeApi.OpenFileManager));
        menu.Add(new TextMenu.Button("CONTROL LAYOUT EDITOR").Pressed(MobileBridgeApi.OpenLayoutEditor));
        menu.Add(new TextMenu.Button("RESTART MOBILE WRAPPER").Pressed(MobileBridgeApi.ResetGame));
    }

    private static void OnRumble(
        On.Celeste.Input.orig_Rumble orig,
        RumbleStrength strength,
        RumbleLength length) {

        orig(strength, length);

        if (Settings.HapticFeedback) {
            MobileBridgeApi.Haptic(strength.ToString(), length.ToString());
        }
    }

    private static void OnOverworldReloadMenus(
        On.Celeste.Overworld.orig_ReloadMenus orig,
        Overworld overworld,
        Overworld.StartMode startMode) {

        orig(overworld, startMode);
        SyncWrapperOptions();
    }

    private static void SyncWrapperOptions() {
        if (Instance?._Settings is not MobileBridgeSettings settings) {
            return;
        }

        MobileBridgeApi.SetOption("touch_controls", settings.TouchControls);
        MobileBridgeApi.SetOption("joystick_mode", settings.JoystickMode);
        MobileBridgeApi.SetOption("joystick_snap_8way", settings.JoystickSnap8Way);
        MobileBridgeApi.SetOption("haptic_feedback", settings.HapticFeedback);
    }

    private static void OnCreateMainMenuButtons(
        OuiMainMenu menu,
        System.Collections.Generic.List<MenuButton> buttons) {

        Vector2 position = Vector2.Zero;
        buttons.Insert(
            Math.Max(0, buttons.Count - 1),
            new MainMenuSmallButton(
                "ANDROID_PORT_ABOUT",
                "menu/options",
                menu,
                position,
                position,
                ShowAboutDialog));
    }

    private static void ShowAboutDialog() {
        TextMenu popup = new TextMenu {
            Position = new Vector2(Engine.Width, Engine.Height) / 2f,
            Tag = Tags.HUD
        };

        popup.Add(new TextMenu.Header(Dialog.Clean("ANDROID_PORT_ABOUT")));
        popup.Add(new MultilineText(Dialog.Clean("ANDROID_PORT_ABOUT_BODY"), 700f));
        popup.Add(new TextMenu.Button(Dialog.Clean("ANDROID_PORT_VISIT"))
            .Pressed(() => MobileBridgeApi.OpenUrlPrompt("https://unlim8ted.com")));
        popup.Add(new TextMenu.Button("CLOSE").Pressed(popup.Close));

        Entity background = new Entity {
            Depth = popup.Depth + 1,
            Tag = Tags.HUD
        };

        background.Add(new RenderComponent(() => {
            popup.RecalculateSize();
            float width = popup.Width + 60f;
            float height = popup.Height + 40f;
            float left = popup.Position.X - width * 0.5f;
            float top = popup.Position.Y - height * 0.5f;

            Draw.Rect(left, top, width, height, Color.Black * 0.95f);
            Draw.Rect(left, top, width, 4f, Color.White);
            Draw.Rect(left, top + height - 4f, width, 4f, Color.White);
            Draw.Rect(left, top, 4f, height, Color.White);
            Draw.Rect(left + width - 4f, top, 4f, height, Color.White);
        }));

        Engine.Scene.Add(popup);
        Engine.Scene.Add(background);
        popup.OnClose += background.RemoveSelf;
    }

    private sealed class RenderComponent : Component {
        private readonly Action onRender;

        public RenderComponent(Action onRender) : base(true, true) {
            this.onRender = onRender;
        }

        public override void Render() {
            onRender?.Invoke();
        }
    }

    private sealed class MultilineText : TextMenu.Item {
        private readonly FancyText.Text group;

        public MultilineText(string text, float width) {
            Selectable = false;
            group = FancyText.Parse(text, (int)width, 100);
        }

        public override float Height() {
            return group.Lines * ActiveFont.LineHeight * 0.6f + 20f;
        }

        public override float LeftWidth() {
            return 600f;
        }

        public override void Render(Vector2 position, bool highlighted) {
            group.Draw(
                position + new Vector2(Container.Width * 0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                Vector2.One * 0.6f,
                Container.Alpha);
        }
    }
}

/// <summary>
/// Public wrapper API. MouseUI binds to the touch subset of this class at
/// runtime through reflection, so MouseUI does not need a MobileBridge DLL
/// reference and still runs independently when this mod is absent.
/// </summary>
public static partial class MobileBridgeApi {
#if BROWSER
    [JSImport("celesteAndroidHaptic", "android-port.js")]
    private static partial void JsHaptic(string strength, string length);

    [JSImport("celesteAndroidOpenUrl", "android-port.js")]
    private static partial void JsOpenUrl(string url);

    [JSImport("celesteAndroidOpenModBrowser", "android-port.js")]
    private static partial void JsOpenModBrowser();

    [JSImport("celesteAndroidOpenSaveData", "android-port.js")]
    private static partial void JsOpenSaveData();

    [JSImport("celesteAndroidOpenFileManager", "android-port.js")]
    private static partial void JsOpenFileManager();

    [JSImport("celesteAndroidOpenLayoutEditor", "android-port.js")]
    private static partial void JsOpenLayoutEditor();

    [JSImport("celesteAndroidResetGame", "android-port.js")]
    private static partial void JsResetGame();

    [JSImport("celesteAndroidSetOption", "android-port.js")]
    private static partial void JsSetOption(string key, string value);

    [JSImport("celesteAndroidConsumeTouchTap", "android-port.js")]
    private static partial bool JsConsumeTouchTap();

    [JSImport("celesteAndroidTouchX", "android-port.js")]
    private static partial double JsTouchX();

    [JSImport("celesteAndroidTouchY", "android-port.js")]
    private static partial double JsTouchY();

    [JSImport("celesteAndroidConsumeTouchScroll", "android-port.js")]
    private static partial double JsConsumeTouchScroll();
#else
    private static void JsHaptic(string strength, string length) { }
    private static void JsOpenUrl(string url) { }
    private static void JsOpenModBrowser() { }
    private static void JsOpenSaveData() { }
    private static void JsOpenFileManager() { }
    private static void JsOpenLayoutEditor() { }
    private static void JsResetGame() { }
    private static void JsSetOption(string key, string value) { }
    private static bool JsConsumeTouchTap() => false;
    private static double JsTouchX() => -1d;
    private static double JsTouchY() => -1d;
    private static double JsConsumeTouchScroll() => 0d;
#endif

    public static bool IsBrowser {
        get {
            try {
                return OperatingSystem.IsBrowser();
            } catch {
                return false;
            }
        }
    }

    /// <summary>
    /// True only when MobileBridge is running in the browser/WASM runtime that
    /// can supply touch events to MouseUI.
    /// </summary>
    public static bool TouchAvailable => IsBrowser;

    public static void Haptic(string strength, string length) {
        Invoke(() => JsHaptic(strength, length));
    }

    public static void OpenUrlPrompt(string url) {
        Invoke(() => JsOpenUrl(url));
    }

    public static void OpenModBrowser() {
        Invoke(JsOpenModBrowser);
    }

    public static void OpenSaveData() {
        Invoke(JsOpenSaveData);
    }

    public static void OpenFileManager() {
        Invoke(JsOpenFileManager);
    }

    public static void OpenLayoutEditor() {
        Invoke(JsOpenLayoutEditor);
    }

    public static void ResetGame() {
        Invoke(JsResetGame);
    }

    public static void SetOption(string key, bool enabled) {
        Invoke(() => JsSetOption(key, enabled ? "true" : "false"));
    }

    public static bool ConsumeTouchTap() {
        if (!TouchAvailable) {
            return false;
        }

        try {
            return JsConsumeTouchTap();
        } catch {
            return false;
        }
    }

    public static float TouchX() {
        if (!TouchAvailable) {
            return -1f;
        }

        try {
            return (float)JsTouchX();
        } catch {
            return -1f;
        }
    }

    public static float TouchY() {
        if (!TouchAvailable) {
            return -1f;
        }

        try {
            return (float)JsTouchY();
        } catch {
            return -1f;
        }
    }

    public static float ConsumeTouchScroll() {
        if (!TouchAvailable) {
            return 0f;
        }

        try {
            return (float)JsConsumeTouchScroll();
        } catch {
            return 0f;
        }
    }

    private static void Invoke(Action action) {
        if (!IsBrowser) {
            return;
        }

        try {
            action();
        } catch {
            // The bridge must never take the game down if the host wrapper is
            // missing an optional function or is still initializing.
        }
    }
}
