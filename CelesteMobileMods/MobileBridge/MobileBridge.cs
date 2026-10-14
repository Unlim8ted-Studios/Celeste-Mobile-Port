using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
    private bool controlsAlwaysOn;

    private bool keyboardControls;
    public bool KeyboardControls {
        get => keyboardControls;
        set { keyboardControls = value; MobileBridgeApi.SetOption("keyboard_controls", value); }
    }

    public bool ControlsAlwaysOn {
        get => controlsAlwaysOn;
        set { controlsAlwaysOn = value; MobileBridgeApi.SetOption("controls_always_on", value); }
    }

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
        On.Monocle.Engine.Update += OnEngineUpdate;
        On.Celeste.Input.Rumble += OnRumble;
        On.Celeste.Overworld.ReloadMenus += OnOverworldReloadMenus;
        On.Celeste.OuiFileNaming.Update += OnFileNamingUpdate;
        On.Celeste.OuiFileNaming.Render += OnFileNamingRender;
        Everest.Events.MainMenu.OnCreateButtons += OnCreateMainMenuButtons;
    }

    public override void Initialize() {
        SyncWrapperOptions();
    }

    public override void Unload() {
        On.Monocle.Engine.Update -= OnEngineUpdate;
        On.Celeste.Input.Rumble -= OnRumble;
        On.Celeste.Overworld.ReloadMenus -= OnOverworldReloadMenus;
        On.Celeste.OuiFileNaming.Update -= OnFileNamingUpdate;
        On.Celeste.OuiFileNaming.Render -= OnFileNamingRender;
        Everest.Events.MainMenu.OnCreateButtons -= OnCreateMainMenuButtons;
    }

    private bool? lastGameplay;
    private bool? lastPauseOnly;
    private bool crouchDashBound;
    private int optionPoll;
    private OuiFileNaming activeNaming;
    private string promptName;
    private static readonly MethodInfo FinishNaming = typeof(OuiFileNaming).GetMethod("Finish", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly MethodInfo CancelNaming = typeof(OuiFileNaming).GetMethod("Cancel", BindingFlags.Instance | BindingFlags.NonPublic);
    private static readonly FieldInfo NamingEase = typeof(OuiFileNaming).GetField("ease", BindingFlags.Instance | BindingFlags.NonPublic);
    private void OnFileNamingRender(On.Celeste.OuiFileNaming.orig_Render orig, OuiFileNaming naming) {
        if (!MobileBridgeApi.IsBrowser || activeNaming != naming || NamingEase == null) {
            orig(naming);
            return;
        }

        // The browser dialog replaces the game's picker; only its dimmed
        // background is needed. The save card is rendered separately.
        float ease = (float)NamingEase.GetValue(naming);
        Draw.Rect(-10f, -10f, 1940f, 1100f, Color.Black * (0.8f * ease));
    }
    private void OnFileNamingUpdate(On.Celeste.OuiFileNaming.orig_Update orig, OuiFileNaming naming) {
        bool browserPromptOpen = MobileBridgeApi.IsBrowser && activeNaming == naming;
        bool inputDisabled = MInput.Disabled;
        if (browserPromptOpen) MInput.Disabled = true;
        try { orig(naming); }
        finally { MInput.Disabled = inputDisabled; }
        if (browserPromptOpen && promptName != null) naming.Name = promptName;
        if (!MobileBridgeApi.IsBrowser || !naming.Selected || !naming.Focused) return;
        if (activeNaming != naming) {
            activeNaming = naming;
            promptName = naming.Name ?? "";
            MobileBridgeApi.OpenNamePrompt(promptName);
        }
        string response = MobileBridgeApi.ConsumeNamePrompt();
        if (response.Length == 0) return;
        if (response[0] == '2') {
            string preview = response.Substring(1);
            promptName = preview.Length <= 12 ? preview : preview.Substring(0, 12);
            naming.Name = promptName;
            return;
        }
        activeNaming = null;
        promptName = null;
        if (response[0] == '0') {
            CancelNaming?.Invoke(naming, null);
        } else if (response[0] == '1') {
            string name = response.Substring(1).Trim();
            if (name.Length == 0) return;
            naming.Name = name.Length <= 12 ? name : name.Substring(0, 12);
            FinishNaming?.Invoke(naming, null);
        }
    }
    private void OnEngineUpdate(On.Monocle.Engine.orig_Update orig, Engine engine, GameTime time) {
        orig(engine, time);
        if (!MobileBridgeApi.IsBrowser) return;
        if (++optionPoll >= 15) {
            optionPoll = 0;
            string changes = MobileBridgeApi.ConsumeOptions();
            foreach (string change in changes.Split('|')) {
                string[] pair = change.Split('=');
                if (pair.Length != 2 || !bool.TryParse(pair[1], out bool value)) continue;
                switch (pair[0]) {
                    case "touch_controls": Settings.TouchControls = value; break;
                    case "controls_always_on": Settings.ControlsAlwaysOn = value; break;
                    case "joystick_mode": Settings.JoystickMode = value; break;
                    case "joystick_snap_8way": Settings.JoystickSnap8Way = value; break;
                }
            }
            if (changes.Length > 0) SaveSettings();
        }
        if (!crouchDashBound && global::Celeste.Settings.Instance?.DemoDash != null) {
            global::Celeste.Settings.Instance.DemoDash.Add(Microsoft.Xna.Framework.Input.Keys.V);
            crouchDashBound = true;
        }
        bool postcard = Engine.Scene?.Entities.Any(entity => entity.Visible && entity is Postcard) == true;
        bool interactiveTutorial = Engine.Scene?.Entities.OfType<BirdTutorialGui>().Any(gui => gui.Visible && gui.Open) == true;
        bool pauseOnly = postcard || Engine.Scene is IntroVignette ||
            (Engine.Scene is Level cinematic && cinematic.InCutscene && !interactiveTutorial && !cinematic.Paused);
        if (lastPauseOnly != pauseOnly) {
            lastPauseOnly = pauseOnly;
            MobileBridgeApi.SetOption("pause_only", pauseOnly);
        }
        bool gameplay = !pauseOnly && Engine.Scene is Level level && !level.Paused &&
            !level.Entities.OfType<TextMenu>().Any(menu => menu.Visible);
        if (lastGameplay != gameplay) {
            lastGameplay = gameplay;
            MobileBridgeApi.SetGameplay(gameplay);
        }
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
        menu.Depth = parent.Depth - 2;

        menu.Add(
            new TextMenu.Header(
                "MOBILE CONTROLS"));
        menu.Add(new TextMenu.OnOff("SHOW CONTROLS", Settings.TouchControls)
            .Change(value => Settings.TouchControls = value));
        TextMenu.Button keyboardConfig = new("CONFIGURE KEYBOARD") {
            Visible = Settings.KeyboardControls
        };
        keyboardConfig.Pressed(() => {
            if (menu.Scene == null) return;
            menu.Focused = false;
            KeyboardConfigUI config = new();
            config.OnClose = () => {
                if (menu.Scene != null) menu.Focused = true;
            };
            Engine.Scene.Add(config);
        });
        menu.Add(new TextMenu.OnOff("KEYBOARD CONTROLS", Settings.KeyboardControls)
            .Change(value => {
                Settings.KeyboardControls = value;
                keyboardConfig.Visible = value;
            }));
        menu.Add(keyboardConfig);
        menu.Add(new TextMenu.OnOff("ON-SCREEN CONTROLS ALWAYS ON", Settings.ControlsAlwaysOn)
            .Change(value => Settings.ControlsAlwaysOn = value));

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
        MobileBridgeApi.SetOption("controls_always_on", settings.ControlsAlwaysOn);
        MobileBridgeApi.SetOption("keyboard_controls", settings.KeyboardControls);

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
        popup.Depth = -2000000;

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
    [JSImport("celesteAndroidConsumeOptions", "android-port.js")]
    private static partial string JsConsumeOptions();
    [JSImport("celesteAndroidSetGameplay", "android-port.js")]
    private static partial void JsSetGameplay(bool gameplay);
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

    [JSImport("celesteAndroidTouchDown", "android-port.js")]
    private static partial bool JsTouchDown();

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
    [JSImport("celesteAndroidOpenNamePrompt", "android-port.js")]
    private static partial void JsOpenNamePrompt(string currentName);
    [JSImport("celesteAndroidConsumeNamePrompt", "android-port.js")]
    private static partial string JsConsumeNamePrompt();
    [JSImport("celesteAndroidPhysicalKeyboardSeen", "android-port.js")]
    private static partial bool JsPhysicalKeyboardSeen();
#else
    private static string JsConsumeOptions() => "";
    private static void JsSetGameplay(bool gameplay) { }
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

    private static bool JsTouchDown() =>
        false;

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
    private static void JsOpenNamePrompt(string currentName) { }
    private static string JsConsumeNamePrompt() => "";
    private static bool JsPhysicalKeyboardSeen() => false;
#endif

    public static bool IsBrowser {
        get {
#if BROWSER
            return true;
#else
            return false;
#endif
        }
    }

    public static bool TouchAvailable =>
        IsBrowser && !(MobileBridgeModule.Instance?._Settings is MobileBridgeSettings settings && settings.ControlsAlwaysOn);
    public static bool PhysicalKeyboardSeen {
        get {
            try {
                return IsBrowser &&
                    MobileBridgeModule.Instance?._Settings is MobileBridgeSettings settings &&
                    settings.KeyboardControls && JsPhysicalKeyboardSeen();
            } catch { return false; }
        }
    }

    public static void SetGameplay(bool gameplay) => Invoke(() => JsSetGameplay(gameplay));
    public static void OpenNamePrompt(string currentName) => Invoke(() => JsOpenNamePrompt(currentName));
    public static string ConsumeNamePrompt() {
        try { return IsBrowser ? JsConsumeNamePrompt() ?? "" : ""; } catch { return ""; }
    }
    public static string ConsumeOptions() {
        try { return IsBrowser ? JsConsumeOptions() ?? "" : ""; } catch { return ""; }
    }

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

    public static bool TouchDown() {
        if (!TouchAvailable) {
            return false;
        }

        try {
            return JsTouchDown();
        } catch {
            return false;
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
        } catch (Exception error) {
            if (!reportedBridgeError) { reportedBridgeError = true; Logger.Log("MobileBridge", "Browser bridge failed: " + error); }
        }
    }
    private static bool reportedBridgeError;
}
