using System;
using System.Linq;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.Core;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MobileTweaks;

public sealed class MobileTweaksSettings : EverestModuleSettings {
    public bool CameraCentering { get; set; } = true;
    public bool SkipIntro { get; set; } = true;
    public bool SkipTitleScreen { get; set; } = true;
    public bool HideFullscreenOptionInBrowser { get; set; } = true;
    public bool IncreaseMenuSpacing { get; set; } = true;
}

public sealed class MobileTweaksModule : EverestModule {
    public static MobileTweaksModule Instance { get; private set; }

    private bool skipNextTitleScreen = true;
    private bool previousLaunchWithoutIntro;
    private bool capturedLaunchWithoutIntro;

    public override Type SettingsType => typeof(MobileTweaksSettings);

    public static MobileTweaksSettings Settings =>
        (MobileTweaksSettings)Instance._Settings;

    public MobileTweaksModule() {
        Instance = this;
    }

    public override void Load() {
        previousLaunchWithoutIntro = CoreModule.Settings.LaunchWithoutIntro;
        capturedLaunchWithoutIntro = true;
        ApplyIntroSetting();

        On.Celeste.Level.Update += OnLevelUpdate;
        On.Celeste.Overworld.ReloadMenus += OnOverworldReloadMenus;
        On.Celeste.MenuOptions.Create += OnCreateOptionsMenu;
    }

    public override void Initialize() {
        ApplyIntroSetting();
    }

    public override void Unload() {
        On.Celeste.Level.Update -= OnLevelUpdate;
        On.Celeste.Overworld.ReloadMenus -= OnOverworldReloadMenus;
        On.Celeste.MenuOptions.Create -= OnCreateOptionsMenu;

        if (capturedLaunchWithoutIntro) {
            CoreModule.Settings.LaunchWithoutIntro = previousLaunchWithoutIntro;
        }
    }

    private static bool IsBrowserRuntime() {
        try {
            return OperatingSystem.IsBrowser();
        } catch {
            return false;
        }
    }

    private static void ApplyIntroSetting() {
        if (Instance?._Settings is MobileTweaksSettings settings) {
            CoreModule.Settings.LaunchWithoutIntro = settings.SkipIntro;
        }
    }

    private void OnOverworldReloadMenus(
        On.Celeste.Overworld.orig_ReloadMenus orig,
        Overworld overworld,
        Overworld.StartMode startMode) {

        ApplyIntroSetting();

        if (Settings.SkipTitleScreen &&
            skipNextTitleScreen &&
            startMode == Overworld.StartMode.Titlescreen) {

            skipNextTitleScreen = false;
            startMode = Overworld.StartMode.MainMenu;
        }

        orig(overworld, startMode);
    }

    private static TextMenu OnCreateOptionsMenu(
        On.Celeste.MenuOptions.orig_Create orig,
        bool inGame,
        FMOD.Studio.EventInstance snapshot) {

        TextMenu options = orig(inGame, snapshot);

        if (Settings.HideFullscreenOptionInBrowser && IsBrowserRuntime()) {
            string fullscreenLabel = Dialog.Clean("OPTIONS_FULLSCREEN");
            TextMenu.Item fullscreen = options.Items.FirstOrDefault(item =>
                item is TextMenu.OnOff onOff && onOff.Label == fullscreenLabel);

            if (fullscreen != null) {
                options.Remove(fullscreen);
            }

            global::Celeste.Settings.Instance.Fullscreen = false;
        }

        if (Settings.IncreaseMenuSpacing) {
            options.ItemSpacing = Math.Max(options.ItemSpacing, 10f);
        }

        return options;
    }

    private static void OnLevelUpdate(
        On.Celeste.Level.orig_Update orig,
        Level level) {

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

        Player player = level.Tracker.GetEntity<Player>();
        if (player == null ||
            player.Dead ||
            !player.InControl ||
            player.StateMachine.State == Player.StDummy ||
            player.StateMachine.State == Player.StAttract) {
            return;
        }

        // Avoid fighting zoomed sequences, screen-padding effects, and most
        // special camera states. This keeps the behavior close to the original
        // AndroidPort implementation.
        if (level.Zoom != 1f || level.ScreenPadding != 0f) {
            return;
        }

        Rectangle bounds = level.Bounds;

        float maxX = Math.Max(bounds.Left, bounds.Right - 320f);
        float maxY = Math.Max(bounds.Top, bounds.Bottom - 180f);

        Vector2 target = new Vector2(
            Calc.Clamp(player.Center.X - 160f, bounds.Left, maxX),
            Calc.Clamp(player.Center.Y - 90f, bounds.Top, maxY));

        float amount = 1f - (float)Math.Pow(0.01, Engine.DeltaTime);
        level.Camera.Position = Vector2.Lerp(level.Camera.Position, target, amount);
    }
}
