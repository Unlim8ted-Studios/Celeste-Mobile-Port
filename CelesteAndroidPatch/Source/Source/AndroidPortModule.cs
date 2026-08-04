using System;
using System.Linq;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.Core;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.AndroidPort;

public sealed class AndroidPortModule : EverestModule {
    public static AndroidPortModule Instance;
    private static bool skipNextTitleScreen = true;

    public override Type SettingsType => typeof(AndroidPortSettings);
    public static AndroidPortSettings Settings => (AndroidPortSettings) Instance._Settings;

    public AndroidPortModule() {
        Instance = this;
    }

    public override void Load() {
        CoreModule.Settings.LaunchWithoutIntro = true;
        On.Celeste.Level.Update += onLevelUpdate;
        On.Celeste.TextMenu.Update += onTextMenuUpdate;
        On.Celeste.OuiMainMenu.Update += onMainMenuUpdate;
        On.Celeste.OuiFileSelect.Update += onFileSelectUpdate;
        On.Celeste.OuiChapterSelect.Update += onChapterSelectUpdate;
        On.Celeste.Overworld.ReloadMenus += onOverworldReloadMenus;
        On.Celeste.MenuOptions.Create += onCreateOptionsMenu;
        Everest.Events.MainMenu.OnCreateButtons += onCreateMainMenuButtons;
        On.Celeste.Input.Rumble += onRumble;
    }

    public override void Initialize() {
        syncAndroidOptions();
    }

    public override void Unload() {
        On.Celeste.Level.Update -= onLevelUpdate;
        On.Celeste.TextMenu.Update -= onTextMenuUpdate;
        On.Celeste.OuiMainMenu.Update -= onMainMenuUpdate;
        On.Celeste.OuiFileSelect.Update -= onFileSelectUpdate;
        On.Celeste.OuiChapterSelect.Update -= onChapterSelectUpdate;
        On.Celeste.Overworld.ReloadMenus -= onOverworldReloadMenus;
        On.Celeste.MenuOptions.Create -= onCreateOptionsMenu;
        Everest.Events.MainMenu.OnCreateButtons -= onCreateMainMenuButtons;
        On.Celeste.Input.Rumble -= onRumble;
    }

    public override void CreateModMenuSection(TextMenu menu, bool inGame, FMOD.Studio.EventInstance snapshot) {
        base.CreateModMenuSection(menu, inGame, snapshot);
        menu.Add(new TextMenu.OnOff("Touch Controls", Settings.TouchControls).Change(value => {
            Settings.TouchControls = value;
            AndroidBridge.SetOption("touch_controls", value);
        }));
        menu.Add(new TextMenu.OnOff("Joystick Mode", Settings.JoystickMode).Change(value => {
            Settings.JoystickMode = value;
            AndroidBridge.SetOption("joystick_mode", value);
        }));
        menu.Add(new TextMenu.OnOff("8-way Joystick Snap", Settings.JoystickSnap8Way).Change(value => {
            Settings.JoystickSnap8Way = value;
            AndroidBridge.SetOption("joystick_snap_8way", value);
        }));
        menu.Add(new TextMenu.OnOff("Haptic Feedback", Settings.HapticFeedback).Change(value => {
            Settings.HapticFeedback = value;
            AndroidBridge.SetOption("haptic_feedback", value);
        }));
        menu.Add(new TextMenu.OnOff("Camera Centering", Settings.CameraCentering).Change(value => Settings.CameraCentering = value));
        menu.Add(new TextMenu.Button("Edit Touch Layout").Pressed(AndroidBridge.OpenLayoutEditor));
        menu.Add(new TextMenu.Button("Export Save Data").Pressed(AndroidBridge.OpenSaveData));
        menu.Add(new TextMenu.Button("File Manager").Pressed(AndroidBridge.OpenFileManager));
        menu.Add(new TextMenu.Button("Reset Game Data").Pressed(AndroidBridge.ResetGame));
        menu.Add(new TextMenu.Button("About the Port").Pressed(showAboutDialog));
        menu.Add(new TextMenu.Button("Mod Browser").Pressed(showModBrowser));
        menu.ItemSpacing = Math.Max(menu.ItemSpacing, 10f);
    }

    private static void syncAndroidOptions() {
        AndroidBridge.SetOption("touch_controls", Settings.TouchControls);
        AndroidBridge.SetOption("joystick_mode", Settings.JoystickMode);
        AndroidBridge.SetOption("joystick_snap_8way", Settings.JoystickSnap8Way);
        AndroidBridge.SetOption("haptic_feedback", Settings.HapticFeedback);
    }

    private static void onCreateMainMenuButtons(OuiMainMenu menu, System.Collections.Generic.List<MenuButton> buttons) {
        Vector2 position = Vector2.Zero;
        MenuButton button = new MainMenuSmallButton("ANDROID_PORT_ABOUT", "menu/options", menu, position, position, showAboutDialog);
        buttons.Insert(Math.Max(0, buttons.Count - 1), button);
    }

    private static void onOverworldReloadMenus(On.Celeste.Overworld.orig_ReloadMenus orig, Overworld overworld, Overworld.StartMode startMode) {
        if (skipNextTitleScreen && startMode == Overworld.StartMode.Titlescreen) {
            skipNextTitleScreen = false;
            startMode = Overworld.StartMode.MainMenu;
        }
        orig(overworld, startMode);
        syncAndroidOptions();
    }

    private static TextMenu onCreateOptionsMenu(On.Celeste.MenuOptions.orig_Create orig, bool inGame, FMOD.Studio.EventInstance snapshot) {
        TextMenu options = orig(inGame, snapshot);
        string fullscreenLabel = Dialog.Clean("OPTIONS_FULLSCREEN");
        TextMenu.Item fullscreen = options.Items.FirstOrDefault(item => item is TextMenu.OnOff onOff && onOff.Label == fullscreenLabel);
        if (fullscreen != null) {
            options.Remove(fullscreen);
        }
        global::Celeste.Settings.Instance.Fullscreen = false;
        options.ItemSpacing = Math.Max(options.ItemSpacing, 10f);
        return options;
    }

    private static void onMainMenuUpdate(On.Celeste.OuiMainMenu.orig_Update orig, OuiMainMenu menu) {
        orig(menu);

        if (menu == null || !menu.Visible || !menu.Focused || menu.Buttons == null || menu.Buttons.Count == 0) {
            return;
        }

        float scroll = AndroidBridge.ConsumeTouchScroll();
        if (Math.Abs(scroll) > 34f) {
            int steps = Math.Min(6, Math.Max(1, (int) (Math.Abs(scroll) / 72f)));
            int direction = scroll > 0f ? 1 : -1;
            for (int i = 0; i < steps; i++) {
                MenuButton selected = menu.Buttons.FirstOrDefault(button => button.Selected) ?? menu.Buttons.FirstOrDefault();
                MenuButton next = direction > 0 ? selected?.DownButton : selected?.UpButton;
                if (next != null) {
                    next.Selected = true;
                    Audio.Play(direction > 0 ? "event:/ui/main/rollover_down" : "event:/ui/main/rollover_up");
                }
            }
        }

        if (!AndroidBridge.ConsumeTouchTap()) {
            return;
        }

        AndroidBridge.Vector2Like tap = AndroidBridge.TouchPosition();
        foreach (MenuButton button in menu.Buttons) {
            if (button == null || button.Scene == null || !button.Visible) {
                continue;
            }

            if (mainMenuHit(button, tap.X, tap.Y)) {
                button.Selected = true;
                button.Confirm();
                return;
            }
        }
    }

    private static bool mainMenuHit(MenuButton button, float x, float y) {
        if (button is MainMenuClimb) {
            float width = 520f;
            float height = Math.Max(button.ButtonHeight, 230f);
            return x >= button.Position.X - width * 0.5f &&
                   x <= button.Position.X + width * 0.5f &&
                   y >= button.Position.Y - 40f &&
                   y <= button.Position.Y + height;
        }

        float hitHeight = Math.Max(button.ButtonHeight, 96f);
        return x >= button.Position.X - 72f &&
               x <= button.Position.X + 620f &&
               y >= button.Position.Y - hitHeight * 0.5f &&
               y <= button.Position.Y + hitHeight * 0.5f;
    }

    private static void onFileSelectUpdate(On.Celeste.OuiFileSelect.orig_Update orig, OuiFileSelect fileSelect) {
        orig(fileSelect);

        if (fileSelect == null || !fileSelect.Focused || fileSelect.SlotSelected || fileSelect.Slots == null || fileSelect.Slots.Length == 0) {
            return;
        }

        float scroll = AndroidBridge.ConsumeTouchScroll();
        if (Math.Abs(scroll) > 34f) {
            int oldIndex = fileSelect.SlotIndex;
            int steps = Math.Min(6, Math.Max(1, (int) (Math.Abs(scroll) / 72f)));
            int direction = scroll > 0f ? 1 : -1;
            fileSelect.SlotIndex = Calc.Clamp(fileSelect.SlotIndex + direction * steps, 0, fileSelect.Slots.Length - 1);
            if (fileSelect.SlotIndex != oldIndex) {
                Audio.Play(direction > 0f ? "event:/ui/main/savefile_rollover_down" : "event:/ui/main/savefile_rollover_up");
                scrollSaveSlots(fileSelect);
            }
        }

        if (!AndroidBridge.ConsumeTouchTap()) {
            return;
        }

        AndroidBridge.Vector2Like tap = AndroidBridge.TouchPosition();
        for (int i = 0; i < fileSelect.Slots.Length; i++) {
            OuiFileSelectSlot slot = fileSelect.Slots[i];
            if (slot == null || !slot.Visible) {
                continue;
            }

            if (tap.X >= slot.Position.X - 520f &&
                tap.X <= slot.Position.X + 520f &&
                tap.Y >= slot.Position.Y - 160f &&
                tap.Y <= slot.Position.Y + 160f) {
                fileSelect.SlotIndex = i;
                Audio.Play("event:/ui/main/button_select");
                Audio.Play("event:/ui/main/whoosh_savefile_out");
                fileSelect.SelectSlot(reset: true);
                return;
            }
        }
    }

    private static void scrollSaveSlots(OuiFileSelect fileSelect) {
        for (int i = 0; i < fileSelect.Slots.Length; i++) {
            OuiFileSelectSlot slot = fileSelect.Slots[i];
            if (slot != null) {
                slot.MoveTo(slot.IdlePosition.X, slot.IdlePosition.Y);
            }
        }
    }

    private static void onChapterSelectUpdate(On.Celeste.OuiChapterSelect.orig_Update orig, OuiChapterSelect chapterSelect) {
        orig(chapterSelect);

        if (chapterSelect == null || !chapterSelect.Focused || SaveData.Instance == null || AreaData.Areas == null || AreaData.Areas.Count == 0) {
            return;
        }

        float scroll = AndroidBridge.ConsumeTouchScroll();
        if (Math.Abs(scroll) > 34f) {
            int direction = scroll > 0f ? 1 : -1;
            moveChapterSelection(chapterSelect, direction);
        }

        if (!AndroidBridge.ConsumeTouchTap()) {
            return;
        }

        AndroidBridge.Vector2Like tap = AndroidBridge.TouchPosition();
        if (tap.X < 260f && tap.Y > 760f) {
            Audio.Play("event:/ui/world_map/journal/select");
            chapterSelect.Overworld.Goto<OuiJournal>();
            return;
        }

        if (tap.X < 640f) {
            moveChapterSelection(chapterSelect, -1);
        } else if (tap.X > 1280f) {
            moveChapterSelection(chapterSelect, 1);
        } else {
            Audio.Play("event:/ui/world_map/icon/select");
            SaveData.Instance.LastArea_Safe.Mode = AreaMode.Normal;
            chapterSelect.Overworld.Goto<OuiChapterPanel>();
        }
    }

    private static void moveChapterSelection(OuiChapterSelect chapterSelect, int direction) {
        int current = SaveData.Instance.LastArea_Safe.ID;
        int max = Math.Min(AreaData.Areas.Count - 1, Math.Max(0, SaveData.Instance.UnlockedAreas_Safe));
        int next = Calc.Clamp(current + Math.Sign(direction), 0, max);
        while (next >= 0 && next <= max && AreaData.Get(next) == null) {
            next += Math.Sign(direction);
        }
        if (next == current || next < 0 || next > max) {
            return;
        }

        SaveData.Instance.LastArea_Safe.ID = next;
        Audio.Play(direction > 0 ? "event:/ui/world_map/icon/roll_right" : "event:/ui/world_map/icon/roll_left");
        AreaData areaData = AreaData.Get(next);
        chapterSelect.Overworld.Mountain.EaseCamera(next, areaData.MountainIdle, null, nearTarget: true, areaData.Meta?.Mountain?.Rotate ?? (areaData.LevelSet == "Celeste" && next == 10));
        chapterSelect.Overworld.Maddy.Hide();
    }

    private static void onTextMenuUpdate(On.Celeste.TextMenu.orig_Update orig, TextMenu menu) {
        orig(menu);

        if (menu == null || !menu.Focused || menu.Items == null || menu.Items.Count == 0) {
            return;
        }

        float scroll = AndroidBridge.ConsumeTouchScroll();
        if (Math.Abs(scroll) > 34f) {
            int steps = Math.Min(6, Math.Max(1, (int) (Math.Abs(scroll) / 72f)));
            int direction = scroll > 0f ? 1 : -1;
            for (int i = 0; i < steps; i++) {
                menu.MoveSelection(direction, true);
            }
        }

        if (!AndroidBridge.ConsumeTouchTap()) {
            return;
        }

        AndroidBridge.Vector2Like tap = AndroidBridge.TouchPosition();
        Vector2 origin = menu.Position - menu.Justify * new Vector2(menu.Width, menu.Height);
        float y = origin.Y;
        for (int i = 0; i < menu.Items.Count; i++) {
            TextMenu.Item item = menu.Items[i];
            if (item == null || !item.Visible) {
                continue;
            }

            float height = item.Height();
            float centerY = y + height * 0.5f;
            float hitHeight = Math.Max(height, 88f);
            bool hitX = tap.X >= origin.X - 72f && tap.X <= origin.X + menu.Width + 72f;
            bool hitY = tap.Y >= centerY - hitHeight * 0.5f && tap.Y <= centerY + hitHeight * 0.5f;

            if (item.Hoverable && hitX && hitY) {
                selectMenuItem(menu, item, i);
                if (tap.X > origin.X + menu.Width - 128f) {
                    item.RightPressed();
                } else if (tap.X > origin.X + menu.Width - 300f && tap.X < origin.X + menu.Width - 128f) {
                    item.LeftPressed();
                } else {
                    item.ConfirmPressed();
                    item.OnPressed?.Invoke();
                }
                return;
            }

            y += height + menu.ItemSpacing;
        }
    }

    private static void selectMenuItem(TextMenu menu, TextMenu.Item item, int index) {
        if (menu.Current == item) {
            return;
        }

        menu.Current?.OnLeave?.Invoke();
        menu.Selection = index;
        item.OnEnter?.Invoke();
        item.SelectWiggler?.Start();
    }

    private static void onRumble(On.Celeste.Input.orig_Rumble orig, RumbleStrength strength, RumbleLength length) {
        orig(strength, length);
        if (!Settings.HapticFeedback) {
            return;
        }
        AndroidBridge.Haptic(strength.ToString(), length.ToString());
    }

    private static void onLevelUpdate(On.Celeste.Level.orig_Update orig, Level level) {
        orig(level);

        if (!Settings.CameraCentering || level == null || level.FrozenOrPaused || level.InCutscene || level.SkippingCutscene || level.Transitioning || level.Wipe != null) {
            return;
        }

        Player player = level.Tracker.GetEntity<Player>();
        if (player == null || player.Dead || !player.InControl || player.IsIntroState || player.StateMachine.State == Player.StDummy || player.StateMachine.State == Player.StAttract) {
            return;
        }

        if (level.CameraLockMode != Level.CameraLockModes.None || level.Zoom != 1f || level.ZoomTarget != 1f || level.ScreenPadding != 0f) {
            return;
        }

        Rectangle bounds = level.Bounds;
        Vector2 target = player.Center - new Vector2(160f, 90f);
        target.X = Calc.Clamp(target.X, bounds.Left, bounds.Right - 320f);
        target.Y = Calc.Clamp(target.Y, bounds.Top, bounds.Bottom - 180f);
        level.Camera.Position = Vector2.Lerp(level.Camera.Position, target, 0.18f);
    }

    private static void showAboutDialog() {
        TextMenu popup = new TextMenu {
            Position = new Vector2(Engine.Width, Engine.Height) / 2f
        };
        popup.Add(new TextMenu.Header(Dialog.Clean("ANDROID_PORT_ABOUT")));
        popup.Add(new TextMenu.SubHeader(Dialog.Clean("ANDROID_PORT_ABOUT_BODY")));
        popup.Add(new TextMenu.Button(Dialog.Clean("ANDROID_PORT_VISIT")).Pressed(() => AndroidBridge.OpenUrlPrompt("https://unlim8ted.com")));
        popup.Add(new TextMenu.Button(Dialog.Clean("ANDROID_PORT_CLOSE")).Pressed(popup.RemoveSelf));
        popup.OnCancel = popup.RemoveSelf;
        popup.OnESC = popup.RemoveSelf;
        Engine.Scene.Add(popup);
    }

    private static void showModBrowser() {
        AndroidBridge.OpenModBrowser();
    }

}
