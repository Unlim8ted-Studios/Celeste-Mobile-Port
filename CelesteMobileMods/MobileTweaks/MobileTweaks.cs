using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MobileTweaks;

public sealed class MobileTweaksSettings : EverestModuleSettings {
    [SettingIgnore]
    public bool CameraCentering { get; set; } = true;
}

public sealed class MobileTweaksModule : EverestModule {
    public static MobileTweaksModule Instance { get; private set; }

    public static MobileTweaksSettings Settings =>
        (MobileTweaksSettings)Instance._Settings;

    public override Type SettingsType =>
        typeof(MobileTweaksSettings);

    private bool skipNextTitleScreen = true;
    private string lastMenuSignature;

    public MobileTweaksModule() {
        Instance = this;
    }

    public override void Load() {
        ForceSkipIntro();

        On.Celeste.Level.Update += OnLevelUpdate;
        On.Celeste.Overworld.ReloadMenus += OnOverworldReloadMenus;
        On.Celeste.MenuOptions.Create += OnCreateOptionsMenu;
        On.Celeste.OuiMainMenu.Update += OnMainMenuUpdate;
    }

    public override void Initialize() {
        ForceSkipIntro();
    }

    public override void Unload() {
        On.Celeste.Level.Update -= OnLevelUpdate;
        On.Celeste.Overworld.ReloadMenus -= OnOverworldReloadMenus;
        On.Celeste.MenuOptions.Create -= OnCreateOptionsMenu;
        On.Celeste.OuiMainMenu.Update -= OnMainMenuUpdate;
    }

    /// <summary>
    /// MobileTweaks' own settings are intentionally moved into Celeste's normal
    /// Options screen while this module is installed.
    /// </summary>
    public override void CreateModMenuSection(
        TextMenu menu,
        bool inGame,
        EventInstance snapshot) {
    }

    private static bool IsBrowserRuntime() {
        try {
            return OperatingSystem.IsBrowser();
        } catch {
            return false;
        }
    }

    private static void ForceSkipIntro() {
        // Keep Everest's persistent runtime flag enabled.
        CoreModule.Settings.LaunchWithoutIntro = true;

        // Everest checks LaunchWithoutIntro in GameLoader.Begin(). A normal
        // code module can load after Begin has already happened, so also mark
        // the current loader as skipped when possible.
        try {
            if (Engine.Scene is GameLoader loader) {
                FieldInfo skipped = loader.GetType().GetField(
                    "skipped",
                    BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public);

                skipped?.SetValue(loader, true);
            }
        } catch (Exception e) {
            Logger.Log(
                LogLevel.Warn,
                "MobileTweaks",
                $"Could not force current GameLoader intro skip: {e.Message}");
        }
    }

    private void OnOverworldReloadMenus(
        On.Celeste.Overworld.orig_ReloadMenus orig,
        Overworld overworld,
        Overworld.StartMode startMode) {

        ForceSkipIntro();

        if (skipNextTitleScreen &&
            startMode == Overworld.StartMode.Titlescreen) {

            skipNextTitleScreen = false;
            startMode = Overworld.StartMode.MainMenu;
        }

        orig(overworld, startMode);
    }

    private static TextMenu OnCreateOptionsMenu(
        On.Celeste.MenuOptions.orig_Create orig,
        bool inGame,
        EventInstance snapshot) {

        TextMenu options = orig(inGame, snapshot);

        // In the embedded browser wrapper, fullscreen is not a user-facing
        // choice. Enforce windowed rendering and physically remove the vanilla
        // Fullscreen row.
        if (IsBrowserRuntime()) {
            string fullscreenLabel =
                Dialog.Clean("OPTIONS_FULLSCREEN");

            TextMenu.Item fullscreen =
                options.Items.FirstOrDefault(item =>
                    item is TextMenu.OnOff onOff &&
                    string.Equals(
                        onOff.Label,
                        fullscreenLabel,
                        StringComparison.OrdinalIgnoreCase));

            if (fullscreen != null) {
                options.Remove(fullscreen);
            }

            global::Celeste.Settings.Instance.Fullscreen = false;
        }

        options.ItemSpacing =
            Math.Max(options.ItemSpacing, 10f);

        options.Add(new TextMenu.SubHeader("MOBILE"));

        if (MobileBridgeProxy.Available) {
            options.Add(
                new TextMenu.OnOff(
                    "HAPTICS",
                    MobileBridgeProxy.HapticFeedback)
                .Change(value =>
                    MobileBridgeProxy.HapticFeedback = value));
        }

        options.Add(
            new TextMenu.OnOff(
                "CENTER CAMERA",
                Settings.CameraCentering)
            .Change(value =>
                Settings.CameraCentering = value));

        if (MobileBridgeProxy.Available) {
            options.Add(
                new TextMenu.Button("MOBILE CONTROLS")
                .Pressed(() =>
                    MobileBridgeProxy.OpenControlsMenu(options)));
        }

        if (MobileMultiplayerProxy.Available) {
            options.Add(
                new TextMenu.Button("MULTIPLAYER")
                .Pressed(() =>
                    MobileMultiplayerProxy.OpenOptionsMenu(
                        options,
                        inGame,
                        snapshot)));
        }

        if (!inGame &&
            MobileBridgeProxy.Available) {

            options.Add(new TextMenu.SubHeader("SAVES"));

            options.Add(
                new TextMenu.Button("EXPORT SAVE")
                .Pressed(MobileBridgeProxy.ExportSave));

            options.Add(
                new TextMenu.Button("LOAD SAVE")
                .Pressed(MobileBridgeProxy.LoadSave));
        }

        // Keep a route to the ordinary Everest Mod Options screen. The mobile
        // suite suppresses only its own sections; unrelated mods remain there.
        if (!inGame) {
            options.Add(new TextMenu.SubHeader("MODS"));

            options.Add(
                new TextMenu.Button("MOD OPTIONS")
                .Pressed(() => {
                    if (options.Scene is Overworld overworld) {
                        overworld.Goto<OuiModOptions>();
                    }
                }));
        }

        return options;
    }

    private void OnMainMenuUpdate(
        On.Celeste.OuiMainMenu.orig_Update orig,
        OuiMainMenu menu) {

        orig(menu);

        if (menu?.Buttons == null ||
            menu.Buttons.Count == 0) {
            return;
        }

        NormalizeMainMenu(menu);
    }

    private void NormalizeMainMenu(
        OuiMainMenu menu) {

        List<MenuButton> buttons = menu.Buttons;

        string signature =
            string.Join(
                "|",
                buttons.Select(GetButtonKey));

        if (signature == lastMenuSignature) {
            return;
        }

        MenuButton climb =
            buttons.FirstOrDefault(button =>
                button is MainMenuClimb);

        MenuButton multiplayer =
            FindButton(
                buttons,
                "MOBILEMULTIPLAYER_MAINMENU");

        MenuButton mapEditor =
            FindButton(
                buttons,
                "BETTERMAPEDITOR_MAINMENU");

        MenuButton modManager =
            FindButton(
                buttons,
                "MOBILEBRIDGE_MOD_BROWSER");

        MenuButton options =
            FindButton(
                buttons,
                "menu_options");

        MenuButton about =
            FindButton(
                buttons,
                "MOBILEBRIDGE_ABOUT_PORT");

        List<MenuButton> ordered = new();
        HashSet<MenuButton> used = new();

        void Add(MenuButton button) {
            if (button != null &&
                used.Add(button)) {

                ordered.Add(button);
            }
        }

        // Desired mobile main-menu order.
        Add(climb);
        Add(multiplayer);
        Add(mapEditor);
        Add(modManager);
        Add(options);
        Add(about);

        foreach (MenuButton button in buttons.ToArray()) {
            if (used.Contains(button)) {
                continue;
            }

            string key =
                GetButtonKey(button);

            // Move Everest's main-menu Mod Options button under normal
            // Options. Detect it by its public label key instead of referring
            // to Everest's internal MainMenuModOptionsButton class.
            if (string.Equals(
                key,
                "menu_modoptions",
                StringComparison.OrdinalIgnoreCase)) {

                button.RemoveSelf();
                continue;
            }

            // The mobile shell intentionally replaces these vanilla bottom
            // entries with its compact six-entry home screen.
            if (string.Equals(
                    key,
                    "menu_credits",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    key,
                    "menu_exit",
                    StringComparison.OrdinalIgnoreCase) ||
                string.Equals(
                    key,
                    "menu_debug",
                    StringComparison.OrdinalIgnoreCase)) {

                button.RemoveSelf();
                continue;
            }

            // Keep the mobile home screen within the layout MouseUI targets.
            // Unrelated mod entries remain available through Mod Options.
            button.RemoveSelf();
        }

        bool changed =
            ordered.Count != buttons.Count ||
            !ordered.SequenceEqual(buttons);

        if (changed) {
            buttons.Clear();
            buttons.AddRange(ordered);
        }

        string layoutMode = "";

        if (!string.Equals(
            CoreModule.Settings.MainMenuMode,
            layoutMode,
            StringComparison.Ordinal)) {

            CoreModule.Settings.MainMenuMode =
                layoutMode;
        }

        menu.UpdateLayout(layoutMode);

        lastMenuSignature =
            string.Join(
                "|",
                buttons.Select(GetButtonKey));
    }

    private static MenuButton FindButton(
        IEnumerable<MenuButton> buttons,
        string labelName) {

        return buttons
            .OfType<MainMenuSmallButton>()
            .FirstOrDefault(button =>
                string.Equals(
                    button.LabelName,
                    labelName,
                    StringComparison.OrdinalIgnoreCase));
    }

    private static string GetButtonKey(
        MenuButton button) {

        if (button is MainMenuClimb) {
            return "$CLIMB";
        }

        if (button is MainMenuSmallButton small) {
            return small.LabelName ??
                small.GetType().FullName ??
                "$BUTTON";
        }

        return button?.GetType().FullName ??
            "$NULL";
    }

    private static void OnLevelUpdate(
        On.Celeste.Level.orig_Update orig,
        Level level) {

        // Let vanilla update the camera first.
        orig(level);

        if (!Settings.CameraCentering ||
            level == null ||
            level.FrozenOrPaused ||
            level.InCutscene ||
            level.SkippingCutscene ||
            level.Transitioning ||
            level.Wipe != null) {
            return;
        }

        Player player =
            level.Tracker.GetEntity<Player>();

        if (player == null ||
            player.Dead ||
            player.StateMachine.State == Player.StDummy ||
            player.StateMachine.State == Player.StAttract) {
            return;
        }

        Rectangle bounds = level.Bounds;

        float maxX =
            Math.Max(
                bounds.Left,
                bounds.Right - 320f);

        float maxY =
            Math.Max(
                bounds.Top,
                bounds.Bottom - 180f);

        Vector2 target = new(
            Calc.Clamp(
                player.Center.X - 160f,
                bounds.Left,
                maxX),
            Calc.Clamp(
                player.Center.Y - 90f,
                bounds.Top,
                maxY));

        // Write after vanilla Level.Update so this is the position actually
        // rendered. The old InControl/Zoom/ScreenPadding gates prevented the
        // feature from engaging in many ordinary rooms.
        level.Camera.Position = target;
    }

    private static class MobileBridgeProxy {
        private static Type moduleType;
        private static bool resolved;

        private static void Resolve() {
            if (resolved) {
                return;
            }

            resolved = true;

            moduleType =
                AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly =>
                    assembly.GetType(
                        "Celeste.Mod.MobileBridge.MobileBridgeModule",
                        throwOnError: false))
                .FirstOrDefault(type =>
                    type != null);
        }

        public static bool Available {
            get {
                Resolve();
                return moduleType != null;
            }
        }

        public static bool HapticFeedback {
            get =>
                Invoke(
                    "GetHapticFeedback",
                    true);
            set =>
                InvokeVoid(
                    "SetHapticFeedback",
                    value);
        }

        public static void OpenControlsMenu(
            TextMenu parent) {

            InvokeVoid(
                "OpenControlsMenu",
                parent);
        }

        public static void ExportSave() {
            InvokeVoid("ExportSave");
        }

        public static void LoadSave() {
            InvokeVoid("LoadSave");
        }

        private static T Invoke<T>(
            string methodName,
            T fallback,
            params object[] args) {

            Resolve();

            if (moduleType == null) {
                return fallback;
            }

            try {
                MethodInfo method =
                    moduleType.GetMethod(
                        methodName,
                        BindingFlags.Public |
                        BindingFlags.Static);

                if (method == null) {
                    return fallback;
                }

                object result =
                    method.Invoke(
                        null,
                        args);

                return result is T typed
                    ? typed
                    : fallback;
            } catch {
                return fallback;
            }
        }

        private static void InvokeVoid(
            string methodName,
            params object[] args) {

            Resolve();

            if (moduleType == null) {
                return;
            }

            try {
                moduleType
                    .GetMethod(
                        methodName,
                        BindingFlags.Public |
                        BindingFlags.Static)
                    ?.Invoke(
                        null,
                        args);
            } catch {
            }
        }
    }

    private static class MobileMultiplayerProxy {
        private static Type moduleType;
        private static bool resolved;

        private static void Resolve() {
            if (resolved) {
                return;
            }

            resolved = true;

            moduleType =
                AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly =>
                    assembly.GetType(
                        "Celeste.Mod.MobileMultiplayer.MobileMultiplayerModule",
                        throwOnError: false))
                .FirstOrDefault(type =>
                    type != null);
        }

        public static bool Available {
            get {
                Resolve();
                return moduleType != null;
            }
        }

        public static void OpenOptionsMenu(
            TextMenu parent,
            bool inGame,
            EventInstance snapshot) {

            Resolve();

            if (moduleType == null) {
                return;
            }

            try {
                moduleType
                    .GetMethod(
                        "OpenOptionsMenu",
                        BindingFlags.Public |
                        BindingFlags.Static)
                    ?.Invoke(
                        null,
                        new object[] {
                            parent,
                            inGame,
                            snapshot
                        });
            } catch (Exception e) {
                Logger.Log(
                    LogLevel.Warn,
                    "MobileTweaks",
                    $"Could not open Multiplayer options: {e.Message}");
            }
        }
    }
}
