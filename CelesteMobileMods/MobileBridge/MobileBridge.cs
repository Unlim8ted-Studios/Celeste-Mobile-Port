using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
#if BROWSER
using System.Runtime.InteropServices.JavaScript;
#endif
using Celeste;
using Celeste.Mod;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MobileBridge;

public sealed class MobileBridgeSettings : EverestModuleSettings {
    private bool touchControls = true;
    private bool joystickMode = true;
    private bool joystickSnap8Way = true;
    private bool hapticFeedback = true;

    public bool TouchControls {
        get => touchControls;
        set {
            touchControls = value;
            MobileBridgeApi.SetOption(
                "touch_controls",
                value);
        }
    }

    public bool JoystickMode {
        get => joystickMode;
        set {
            joystickMode = value;
            MobileBridgeApi.SetOption(
                "joystick_mode",
                value);
        }
    }

    public bool JoystickSnap8Way {
        get => joystickSnap8Way;
        set {
            joystickSnap8Way = value;
            MobileBridgeApi.SetOption(
                "joystick_snap_8way",
                value);
        }
    }

    public bool HapticFeedback {
        get => hapticFeedback;
        set {
            hapticFeedback = value;
            MobileBridgeApi.SetOption(
                "haptic_feedback",
                value);
        }
    }
}

public sealed class MobileBridgeModule : EverestModule {
    public static MobileBridgeModule Instance { get; private set; }

    public static MobileBridgeSettings Settings =>
        (MobileBridgeSettings)Instance._Settings;

    public override Type SettingsType =>
        typeof(MobileBridgeSettings);

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
        EventInstance snapshot) {

        // MobileTweaks moves this module's settings into normal Options.
        // Without MobileTweaks, keep the ordinary Everest-generated settings
        // so MobileBridge remains useful as a standalone mod.
        if (IsMobileTweaksLoaded()) {
            return;
        }

        base.CreateModMenuSection(
            menu,
            inGame,
            snapshot);

        menu.Add(
            new TextMenu.SubHeader(
                "MOBILE TOOLS"));

        menu.Add(
            new TextMenu.Button(
                "SAVE DATA MANAGER")
            .Pressed(
                MobileBridgeApi.OpenSaveData));

        menu.Add(
            new TextMenu.Button(
                "FILE MANAGER")
            .Pressed(
                MobileBridgeApi.OpenFileManager));

        menu.Add(
            new TextMenu.Button(
                "RESIZE / MOVE CONTROLS")
            .Pressed(
                MobileBridgeApi.OpenLayoutEditor));
    }

    public static bool GetHapticFeedback() {
        return Instance?._Settings is MobileBridgeSettings settings
            ? settings.HapticFeedback
            : true;
    }

    public static void SetHapticFeedback(
        bool value) {

        if (Instance?._Settings is MobileBridgeSettings settings) {
            settings.HapticFeedback = value;
        }
    }

    public static void ExportSave() {
        MobileBridgeApi.ExportSave();
    }

    public static void LoadSave() {
        MobileBridgeApi.LoadSave();
    }

    public static void OpenControlsMenu(
        TextMenu parent) {

        if (parent?.Scene == null) {
            return;
        }

        parent.Focused = false;

        TextMenu menu = new() {
            Position =
                new Vector2(
                    Engine.Width,
                    Engine.Height) / 2f,
            Tag =
                Tags.HUD |
                Tags.PauseUpdate,
            ItemSpacing = 12f
        };

        menu.Add(
            new TextMenu.Header(
                "MOBILE CONTROLS"));

        TextMenu.OnOff snap = new(
            "8-WAY SNAP",
            Settings.JoystickSnap8Way);

        TextMenu.Slider movement = new(
            "MOVEMENT",
            index =>
                index == 0
                    ? "JOYSTICK"
                    : "ARROWS",
            0,
            1,
            Settings.JoystickMode
                ? 0
                : 1);

        movement.Change(index => {
            Settings.JoystickMode =
                index == 0;

            snap.Disabled =
                !Settings.JoystickMode;
        });

        menu.Add(movement);

        snap.Disabled =
            !Settings.JoystickMode;

        snap.Change(value =>
            Settings.JoystickSnap8Way =
                value);

        menu.Add(snap);

        menu.Add(
            new TextMenu.Button(
                "RESIZE / MOVE CONTROLS")
            .Pressed(
                MobileBridgeApi.OpenLayoutEditor));

        menu.Add(
            new TextMenu.Button("BACK")
            .Pressed(menu.Close));

        menu.OnCancel =
            menu.Close;

        ModalBackdrop backdrop =
            new(menu);

        menu.OnClose += () => {
            backdrop.RemoveSelf();

            if (parent.Scene != null) {
                parent.Focused = true;
            }
        };

        Engine.Scene.Add(backdrop);
        Engine.Scene.Add(menu);
    }

    private static bool IsMobileTweaksLoaded() {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Any(assembly =>
                assembly.GetType(
                    "Celeste.Mod.MobileTweaks.MobileTweaksModule",
                    throwOnError: false) != null);
    }

    private static void OnRumble(
        On.Celeste.Input.orig_Rumble orig,
        RumbleStrength strength,
        RumbleLength length) {

        orig(strength, length);

        if (Settings.HapticFeedback) {
            MobileBridgeApi.Haptic(
                strength.ToString(),
                length.ToString());
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

        MobileBridgeApi.SetOption(
            "touch_controls",
            settings.TouchControls);

        MobileBridgeApi.SetOption(
            "joystick_mode",
            settings.JoystickMode);

        MobileBridgeApi.SetOption(
            "joystick_snap_8way",
            settings.JoystickSnap8Way);

        MobileBridgeApi.SetOption(
            "haptic_feedback",
            settings.HapticFeedback);
    }

    private static void OnCreateMainMenuButtons(
        OuiMainMenu menu,
        List<MenuButton> buttons) {

        Vector2 position =
            Vector2.Zero;

        int optionsIndex =
            FindButtonIndex(
                buttons,
                "menu_options");

        int modManagerIndex =
            optionsIndex >= 0
                ? optionsIndex
                : buttons.Count;

        buttons.Insert(
            Math.Clamp(
                modManagerIndex,
                0,
                buttons.Count),
            new MainMenuSmallButton(
                "MOBILEBRIDGE_MOD_BROWSER",
                "menu/options",
                menu,
                position,
                position,
                MobileBridgeApi.OpenModBrowser));

        optionsIndex =
            FindButtonIndex(
                buttons,
                "menu_options");

        int aboutIndex =
            optionsIndex >= 0
                ? optionsIndex + 1
                : buttons.Count;

        buttons.Insert(
            Math.Clamp(
                aboutIndex,
                0,
                buttons.Count),
            new MainMenuSmallButton(
                "MOBILEBRIDGE_ABOUT_PORT",
                "menu/options",
                menu,
                position,
                position,
                () =>
                    ShowAboutDialog(menu)));
    }

    private static int FindButtonIndex(
        List<MenuButton> buttons,
        string labelName) {

        return buttons.FindIndex(button =>
            button is MainMenuSmallButton small &&
            string.Equals(
                small.LabelName,
                labelName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static void ShowAboutDialog(
        OuiMainMenu owner) {

        if (owner?.Scene == null) {
            return;
        }

        owner.Focused = false;

        TextMenu popup = new() {
            Position =
                new Vector2(
                    Engine.Width,
                    Engine.Height) / 2f,
            Tag =
                Tags.HUD |
                Tags.PauseUpdate,
            ItemSpacing = 12f
        };

        popup.Add(
            new TextMenu.Header(
                Dialog.Clean(
                    "MOBILEBRIDGE_ABOUT_PORT")));

        popup.Add(
            new MultilineText(
                Dialog.Clean(
                    "MOBILEBRIDGE_ABOUT_BODY"),
                900f));

        popup.Add(
            new TextMenu.Button(
                Dialog.Clean(
                    "MOBILEBRIDGE_VISIT"))
            .Pressed(() =>
                MobileBridgeApi.OpenUrlPrompt(
                    "https://unlim8ted.com")));

        popup.Add(
            new TextMenu.Button("CLOSE")
            .Pressed(popup.Close));

        popup.OnCancel =
            popup.Close;

        ModalBackdrop backdrop =
            new(popup);

        popup.OnClose += () => {
            backdrop.RemoveSelf();

            if (owner.Scene != null) {
                owner.Focused = true;
            }
        };

        Engine.Scene.Add(backdrop);
        Engine.Scene.Add(popup);
    }

    private sealed class ModalBackdrop : Entity {
        private readonly TextMenu menu;

        public ModalBackdrop(
            TextMenu menu) {

            this.menu = menu;
            Tag =
                Tags.HUD |
                Tags.PauseUpdate;
            Depth =
                menu.Depth + 1;
        }

        public override void Render() {
            Draw.Rect(
                0f,
                0f,
                1920f,
                1080f,
                Color.Black * 0.78f);

            if (menu?.Scene == null) {
                return;
            }

            menu.RecalculateSize();

            float width =
                Math.Min(
                    1500f,
                    menu.Width + 100f);

            float height =
                Math.Min(
                    980f,
                    menu.Height + 80f);

            Vector2 position =
                menu.Position;

            Draw.Rect(
                position.X - width * 0.5f,
                position.Y - height * 0.5f,
                width,
                height,
                Color.Black * 0.94f);

            Draw.HollowRect(
                position.X - width * 0.5f,
                position.Y - height * 0.5f,
                width,
                height,
                Color.White * 0.9f);
        }
    }

    private sealed class MultilineText : TextMenu.Item {
        private readonly FancyText.Text text;

        public MultilineText(
            string value,
            float width) {

            Selectable = false;

            text =
                FancyText.Parse(
                    value ?? "",
                    (int)width,
                    100);
        }

        public override float Height() {
            return text.Lines *
                ActiveFont.LineHeight *
                0.6f +
                20f;
        }

        public override float LeftWidth() {
            return 800f;
        }

        public override void Render(
            Vector2 position,
            bool highlighted) {

            text.Draw(
                position +
                new Vector2(
                    Container.Width * 0.5f,
                    0f),
                new Vector2(
                    0.5f,
                    0.5f),
                Vector2.One * 0.6f,
                Container.Alpha);
        }
    }
}

public static partial class MobileBridgeApi {
#if BROWSER
    [JSImport("celesteAndroidHaptic", "android-port.js")]
    private static partial void JsHaptic(
        string strength,
        string length);

    [JSImport("celesteAndroidOpenUrl", "android-port.js")]
    private static partial void JsOpenUrl(
        string url);

    [JSImport("celesteAndroidOpenModBrowser", "android-port.js")]
    private static partial void JsOpenModBrowser();

    [JSImport("celesteAndroidOpenSaveData", "android-port.js")]
    private static partial void JsOpenSaveData();

    [JSImport("celesteAndroidExportSave", "android-port.js")]
    private static partial void JsExportSave();

    [JSImport("celesteAndroidLoadSave", "android-port.js")]
    private static partial void JsLoadSave();

    [JSImport("celesteAndroidOpenFileManager", "android-port.js")]
    private static partial void JsOpenFileManager();

    [JSImport("celesteAndroidOpenLayoutEditor", "android-port.js")]
    private static partial void JsOpenLayoutEditor();

    [JSImport("celesteAndroidSetOption", "android-port.js")]
    private static partial void JsSetOption(
        string key,
        string value);

    [JSImport("celesteAndroidConsumeTouchTap", "android-port.js")]
    private static partial bool JsConsumeTouchTap();

    [JSImport("celesteAndroidTouchX", "android-port.js")]
    private static partial double JsTouchX();

    [JSImport("celesteAndroidTouchY", "android-port.js")]
    private static partial double JsTouchY();

    [JSImport("celesteAndroidConsumeTouchScroll", "android-port.js")]
    private static partial double JsConsumeTouchScroll();

    [JSImport("celesteAndroidStartCelesteNetHost", "android-port.js")]
    private static partial bool JsStartCelesteNetHost(
        int port);

    [JSImport("celesteAndroidStopCelesteNetHost", "android-port.js")]
    private static partial void JsStopCelesteNetHost();

    [JSImport("celesteAndroidIsCelesteNetHostRunning", "android-port.js")]
    private static partial bool JsIsCelesteNetHostRunning();

    [JSImport("celesteAndroidGetCelesteNetServers", "android-port.js")]
    private static partial string JsGetCelesteNetServers();
#else
    private static void JsHaptic(
        string strength,
        string length) {
    }

    private static void JsOpenUrl(
        string url) {
    }

    private static void JsOpenModBrowser() {
    }

    private static void JsOpenSaveData() {
    }

    private static void JsExportSave() {
    }

    private static void JsLoadSave() {
    }

    private static void JsOpenFileManager() {
    }

    private static void JsOpenLayoutEditor() {
    }

    private static void JsSetOption(
        string key,
        string value) {
    }

    private static bool JsConsumeTouchTap() =>
        false;

    private static double JsTouchX() =>
        -1d;

    private static double JsTouchY() =>
        -1d;

    private static double JsConsumeTouchScroll() =>
        0d;

    private static bool JsStartCelesteNetHost(
        int port) =>
        false;

    private static void JsStopCelesteNetHost() {
    }

    private static bool JsIsCelesteNetHostRunning() =>
        false;

    private static string JsGetCelesteNetServers() =>
        "";
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

    public static bool TouchAvailable =>
        IsBrowser;

    public static void Haptic(
        string strength,
        string length) {

        Invoke(() =>
            JsHaptic(
                strength,
                length));
    }

    public static void OpenUrlPrompt(
        string url) {

        Invoke(() =>
            JsOpenUrl(url));
    }

    public static void OpenModBrowser() {
        Invoke(
            JsOpenModBrowser);
    }

    public static void OpenSaveData() {
        Invoke(
            JsOpenSaveData);
    }

    public static void ExportSave() {
        if (!IsBrowser) {
            return;
        }

        try {
            JsExportSave();
        } catch {
            Invoke(
                JsOpenSaveData);
        }
    }

    public static void LoadSave() {
        if (!IsBrowser) {
            return;
        }

        try {
            JsLoadSave();
        } catch {
            Invoke(
                JsOpenSaveData);
        }
    }

    public static void OpenFileManager() {
        Invoke(
            JsOpenFileManager);
    }

    public static void OpenLayoutEditor() {
        Invoke(
            JsOpenLayoutEditor);
    }

    public static void SetOption(
        string key,
        bool enabled) {

        Invoke(() =>
            JsSetOption(
                key,
                enabled
                    ? "true"
                    : "false"));
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

    public static bool StartCelesteNetHost(
        int port) {

        if (!IsBrowser) {
            return false;
        }

        try {
            return JsStartCelesteNetHost(port);
        } catch {
            return false;
        }
    }

    public static void StopCelesteNetHost() {
        if (!IsBrowser) {
            return;
        }

        try {
            JsStopCelesteNetHost();
        } catch {
        }
    }

    public static bool IsCelesteNetHostRunning {
        get {
            if (!IsBrowser) {
                return false;
            }

            try {
                return JsIsCelesteNetHostRunning();
            } catch {
                return false;
            }
        }
    }

    public static string[] GetCelesteNetServers() {
        if (!IsBrowser) {
            return Array.Empty<string>();
        }

        try {
            string raw =
                JsGetCelesteNetServers() ??
                "";

            return raw
                .Split(
                    new[] {
                        '\n',
                        '\r',
                        ',',
                        ';'
                    },
                    StringSplitOptions.RemoveEmptyEntries)
                .Select(value =>
                    value.Trim())
                .Where(value =>
                    value.Length > 0)
                .Distinct(
                    StringComparer.OrdinalIgnoreCase)
                .ToArray();
        } catch {
            return Array.Empty<string>();
        }
    }

    private static void Invoke(
        Action action) {

        if (!IsBrowser) {
            return;
        }

        try {
            action();
        } catch {
        }
    }
}
