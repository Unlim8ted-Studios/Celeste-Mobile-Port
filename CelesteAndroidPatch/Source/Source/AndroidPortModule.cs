using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
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
    private static float scrollAccumulator = 0f;

    public override Type SettingsType => typeof(AndroidPortSettings);
    public static AndroidPortSettings Settings => (AndroidPortSettings) Instance._Settings;

    public AndroidPortModule() => Instance = this;

    public override void Load() {
        if (!AndroidBridge.IsBrowser) {
            Logger.Log(LogLevel.Info, "AndroidPort", "Running in Desktop Test mode (Touch simulated by Mouse)");
            Engine.Instance.IsMouseVisible = true;
        }

        CoreModule.Settings.LaunchWithoutIntro = true;

        On.Celeste.Level.Update += onLevelUpdate;
        On.Celeste.TextMenu.Update += onTextMenuUpdate;
        On.Celeste.OuiMainMenu.Update += onMainMenuUpdate;
        On.Celeste.OuiFileSelect.Update += onFileSelectUpdate;
        On.Celeste.OuiChapterSelect.Update += onChapterSelectUpdate;
        On.Celeste.OuiChapterPanel.Update += onChapterPanelUpdate;
        On.Celeste.OuiJournal.Update += onJournalUpdate;
        On.Celeste.OuiTitleScreen.Update += onTitleScreenUpdate;
        On.Celeste.OuiCredits.Update += onCreditsUpdate;
        On.Celeste.Overworld.ReloadMenus += onOverworldReloadMenus;
        On.Celeste.MenuOptions.Create += onCreateOptionsMenu;
        Everest.Events.MainMenu.OnCreateButtons += onCreateMainMenuButtons;
        On.Celeste.Input.Rumble += onRumble;
        On.Monocle.Engine.Update += onEngineUpdate;
        On.Monocle.MInput.Update += onMInputUpdate;
        On.Celeste.ButtonUI.Render += onButtonUIRender;
        On.Celeste.Overworld.ctor += onOverworldCtor;
    }

    public override void Initialize() {
        if (!AndroidBridge.IsBrowser) Settings.TouchControls = true;
        syncAndroidOptions();
    }

    public override void Unload() {
        On.Celeste.Level.Update -= onLevelUpdate;
        On.Celeste.TextMenu.Update -= onTextMenuUpdate;
        On.Celeste.OuiMainMenu.Update -= onMainMenuUpdate;
        On.Celeste.OuiFileSelect.Update -= onFileSelectUpdate;
        On.Celeste.OuiChapterSelect.Update -= onChapterSelectUpdate;
        On.Celeste.OuiChapterPanel.Update -= onChapterPanelUpdate;
        On.Celeste.OuiJournal.Update -= onJournalUpdate;
        On.Celeste.OuiTitleScreen.Update -= onTitleScreenUpdate;
        On.Celeste.OuiCredits.Update -= onCreditsUpdate;
        On.Celeste.Overworld.ReloadMenus -= onOverworldReloadMenus;
        On.Celeste.MenuOptions.Create -= onCreateOptionsMenu;
        Everest.Events.MainMenu.OnCreateButtons -= onCreateMainMenuButtons;
        On.Celeste.Input.Rumble -= onRumble;
        On.Monocle.Engine.Update -= onEngineUpdate;
        On.Monocle.MInput.Update -= onMInputUpdate;
        On.Celeste.ButtonUI.Render -= onButtonUIRender;
        On.Celeste.Overworld.ctor -= onOverworldCtor;
    }

    private static void onOverworldCtor(On.Celeste.Overworld.orig_ctor orig, Overworld self, OverworldLoader loader) {
        orig(self, loader);
        Entity nav = new Entity { Tag = Tags.HUD, Depth = -1000000 };
        nav.Add(new RenderComponent(drawNavigationBar));
        self.Add(nav);
    }

    private static void onMInputUpdate(On.Monocle.MInput.orig_Update orig) {
        orig();
        if (Settings.TouchControls) {
            AndroidBridge.UpdateDesktopInput();
            if (AndroidBridge.IsBrowser) {
                if (Engine.Scene is Overworld || isAnyMenuOpen(Engine.Scene)) MInput.Disabled = true;
                else MInput.Disabled = false;
            }
        }
    }

    private static void onButtonUIRender(On.Celeste.ButtonUI.orig_Render orig, Vector2 position, string label, VirtualButton button, float scale, float justifyX, float wiggle, float alpha) {
        if (Settings.TouchControls) return;
        orig(position, label, button, scale, justifyX, wiggle, alpha);
    }

    private static void consumeDesktopInput() {
        if (AndroidBridge.IsBrowser && Settings.TouchControls) {
            Input.MenuConfirm.ConsumeBuffer(); Input.MenuCancel.ConsumeBuffer();
            Input.MenuUp.ConsumeBuffer(); Input.MenuDown.ConsumeBuffer();
            Input.MenuLeft.ConsumeBuffer(); Input.MenuRight.ConsumeBuffer();
            Input.MenuJournal.ConsumeBuffer(); Input.Pause.ConsumeBuffer(); Input.ESC.ConsumeBuffer();
        }
    }

    private static bool isAnyMenuOpen(Scene scene) => scene != null && (scene.Entities.Any(e => e is TextMenu menu && menu.Visible && menu.Focused) || scene is Overworld ov && (ov.IsCurrent<OuiJournal>() || ov.IsCurrent<OuiCredits>()));
    private static TextMenu getTopMenu(Scene scene) => scene?.Entities.OfType<TextMenu>().LastOrDefault(m => m.Visible && m.Focused);

    private static void drawNavigationBar() {
        if (!Settings.TouchControls) return;
        var scene = Engine.Scene as Overworld;
        if (scene == null) return;

        // Show Back button in most UI states
        bool shouldDraw = scene.IsCurrent<OuiMainMenu>() || scene.IsCurrent<OuiFileSelect>() || scene.IsCurrent<OuiChapterSelect>() || scene.IsCurrent<OuiChapterPanel>() || scene.IsCurrent<OuiJournal>() || scene.IsCurrent<OuiCredits>();

        // Hide if a real sub-menu (TextMenu) is open, unless it's the Journal which has its own overlay
        if (isAnyMenuOpen(scene) && getTopMenu(scene)?.Tag != (Tags.PauseUpdate | Tags.HUD) && !scene.IsCurrent<OuiJournal>()) shouldDraw = false;

        if (!shouldDraw) return;

        float x = 1920f - 240f, y = 1080f - 80f, w = 200f, h = 60f;
        Draw.Rect(x - 10f, y - 10f, w + 20f, h + 20f, Color.Black * 0.7f);
        Draw.Rect(x - 10f, y - 10f, w + 20f, 2f, Color.White);
        ActiveFont.DrawOutline("GO BACK", new Vector2(x + w * 0.5f, y + h * 0.5f), new Vector2(0.5f, 0.5f), Vector2.One * 0.8f, Color.White, 2f, Color.Black);
    }

    private static void onEngineUpdate(On.Monocle.Engine.orig_Update orig, Engine engine, GameTime gameTime) {
        AndroidBridge.ResetDesktopTap();
        orig(engine, gameTime);
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
        if (AndroidBridge.IsBrowser) {
            string fullscreenLabel = Dialog.Clean("OPTIONS_FULLSCREEN");
            TextMenu.Item fullscreen = options.Items.FirstOrDefault(item => item is TextMenu.OnOff onOff && onOff.Label == fullscreenLabel);
            if (fullscreen != null) options.Remove(fullscreen);
            global::Celeste.Settings.Instance.Fullscreen = false;
        }
        options.ItemSpacing = Math.Max(options.ItemSpacing, 10f);
        return options;
    }

    private static void onTitleScreenUpdate(On.Celeste.OuiTitleScreen.orig_Update orig, OuiTitleScreen title) {
        if (title.Focused && title.Visible && AndroidBridge.ConsumeTouchTap()) {
            Audio.Play("event:/ui/main/button_select");
            title.Overworld.Goto<OuiMainMenu>();
            return;
        }
        orig(title);
    }

    private static void onMainMenuUpdate(On.Celeste.OuiMainMenu.orig_Update orig, OuiMainMenu menu) {
        consumeDesktopInput();
        if (isAnyMenuOpen(menu.Scene)) { orig(menu); return; }
        orig(menu);
        if (menu == null || !menu.Visible || !menu.Focused) return;
        AndroidBridge.Vector2Like mousePos = AndroidBridge.TouchPosition();
        foreach (MenuButton button in menu.Buttons) {
            if (button != null && button.Visible && mainMenuHit(button, mousePos.X, mousePos.Y)) {
                if (!button.Selected) {
                    foreach (MenuButton other in menu.Buttons) if (other != null) other.Selected = (other == button);
                    Audio.Play("event:/ui/main/rollover_down");
                }
                break;
            }
        }
        if (AndroidBridge.ConsumeTouchTap()) {
            if (mousePos.X > 1650f && mousePos.Y > 950f) { if (menu.Overworld.Next == null) menu.Overworld.Goto<OuiTitleScreen>(); return; }
            foreach (MenuButton button in menu.Buttons) {
                if (button != null && button.Visible && mainMenuHit(button, mousePos.X, mousePos.Y)) { button.Confirm(); return; }
            }
        }
    }

    private static bool mainMenuHit(MenuButton button, float x, float y) {
        if (button is MainMenuClimb) return x >= button.Position.X - 260f && x <= button.Position.X + 260f && y >= button.Position.Y - 40f && y <= button.Position.Y + 230f;
        return x >= button.Position.X - 40f && x <= button.Position.X + 480f && y >= button.Position.Y - 48f && y <= button.Position.Y + 48f;
    }

    private static void onFileSelectUpdate(On.Celeste.OuiFileSelect.orig_Update orig, OuiFileSelect fileSelect) {
        consumeDesktopInput();
        if (isAnyMenuOpen(fileSelect.Scene)) { orig(fileSelect); return; }
        orig(fileSelect);
        if (fileSelect == null || !fileSelect.Focused) return;
        AndroidBridge.Vector2Like mousePos = AndroidBridge.TouchPosition();
        if (!fileSelect.SlotSelected) {
            for (int i = 0; i < fileSelect.Slots.Length; i++) {
                OuiFileSelectSlot slot = fileSelect.Slots[i];
                if (slot == null || !slot.Visible) continue;
                if (mousePos.X >= slot.Position.X - 520f && mousePos.X <= slot.Position.X + 520f && mousePos.Y >= slot.Position.Y - 160f && mousePos.Y <= slot.Position.Y + 160f) {
                    if (fileSelect.SlotIndex != i) { fileSelect.SlotIndex = i; Audio.Play("event:/ui/main/savefile_rollover_down"); scrollSaveSlots(fileSelect); }
                }
            }
        }
        if (AndroidBridge.ConsumeTouchTap()) {
            if (mousePos.X > 1650f && mousePos.Y > 950f) { if (fileSelect.Overworld.Next == null) { if (fileSelect.SlotSelected) fileSelect.UnselectHighlighted(); else fileSelect.Overworld.Goto<OuiMainMenu>(); } return; }
            if (!fileSelect.SlotSelected) {
                OuiFileSelectSlot slot = fileSelect.Slots[fileSelect.SlotIndex];
                if (slot != null && slot.Visible && mousePos.X >= slot.Position.X - 520f && mousePos.X <= slot.Position.X + 520f && mousePos.Y >= slot.Position.Y - 160f && mousePos.Y <= slot.Position.Y + 160f) {
                    Audio.Play("event:/ui/main/button_select"); Audio.Play("event:/ui/main/whoosh_savefile_out"); fileSelect.SelectSlot(reset: true);
                }
            } else {
                OuiFileSelectSlot slot = fileSelect.Slots[fileSelect.SlotIndex];
                var buttons = (List<object>)slot.GetType().GetField("buttons", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(slot);
                var buttonIndex = (int)slot.GetType().GetField("buttonIndex", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(slot);
                float itemY = slot.Position.Y - 150f + 350f * (float)slot.GetType().GetField("selectedEase", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(slot);
                for (int i = 0; i < buttons.Count; i++) {
                    float h = ActiveFont.LineHeight * (float)buttons[i].GetType().GetField("Scale").GetValue(buttons[i]);
                    if (mousePos.X >= slot.Position.X - 300f && mousePos.X <= slot.Position.X + 300f && mousePos.Y >= itemY && mousePos.Y <= itemY + h) {
                        if (buttonIndex != i) { slot.GetType().GetField("buttonIndex", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(slot, i); Audio.Play("event:/ui/main/rollover_down"); }
                        else ((Action)buttons[i].GetType().GetField("Action").GetValue(buttons[i]))();
                        return;
                    }
                    itemY += h + 15f;
                }
            }
        }
    }

    private static void scrollSaveSlots(OuiFileSelect fileSelect) {
        for (int i = 0; i < fileSelect.Slots.Length; i++) if (fileSelect.Slots[i] != null) fileSelect.Slots[i].MoveTo(fileSelect.Slots[i].IdlePosition.X, fileSelect.Slots[i].IdlePosition.Y);
    }

    private static void onChapterSelectUpdate(On.Celeste.OuiChapterSelect.orig_Update orig, OuiChapterSelect chapterSelect) {
        consumeDesktopInput();
        if (isAnyMenuOpen(chapterSelect.Scene)) { orig(chapterSelect); return; }
        orig(chapterSelect);
        if (chapterSelect == null || !chapterSelect.Focused) return;
        AndroidBridge.Vector2Like mousePos = AndroidBridge.TouchPosition();
        Vector2 mouseVec = new Vector2(mousePos.X, mousePos.Y);
        var icons = (List<OuiChapterSelectIcon>)chapterSelect.GetType().GetField("icons", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(chapterSelect);
        for (int i = 0; i < icons.Count; i++) {
            if (icons[i].Area <= SaveData.Instance.UnlockedAreas && Vector2.Distance(mouseVec, icons[i].Position) < 120f) {
                if (SaveData.Instance.LastArea.ID != i) {
                    SaveData.Instance.LastArea.ID = i; icons[i].Hovered(Math.Sign(i - SaveData.Instance.LastArea.ID));
                    chapterSelect.GetType().GetMethod("EaseCamera", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(chapterSelect, null);
                    Audio.Play(i > SaveData.Instance.LastArea.ID ? "event:/ui/world_map/icon/roll_right" : "event:/ui/world_map/icon/roll_left");
                }
            }
        }
        if (AndroidBridge.ConsumeTouchTap()) {
            if (mousePos.X > 1650f && mousePos.Y > 950f) { if (chapterSelect.Overworld.Next == null) chapterSelect.Overworld.Goto<OuiMainMenu>(); return; }
            if (mousePos.X < 260f && mousePos.Y > 760f) { Audio.Play("event:/ui/world_map/journal/select"); chapterSelect.Overworld.Goto<OuiJournal>(); return; }
            for (int i = 0; i < icons.Count; i++) {
                if (icons[i].Area <= SaveData.Instance.UnlockedAreas && Vector2.Distance(mouseVec, icons[i].Position) < 120f) {
                    Audio.Play("event:/ui/world_map/icon/select"); SaveData.Instance.LastArea.Mode = AreaMode.Normal; chapterSelect.Overworld.Goto<OuiChapterPanel>(); return;
                }
            }
        }
    }

    private static void moveChapterSelection(OuiChapterSelect chapterSelect, int direction) {
        int current = SaveData.Instance.LastArea.ID, max = Math.Min(AreaData.Areas.Count - 1, Math.Max(0, SaveData.Instance.UnlockedAreas)), next = Calc.Clamp(current + Math.Sign(direction), 0, max);
        while (next >= 0 && next <= max && AreaData.Get(next) == null) next += Math.Sign(direction);
        if (next == current || next < 0 || next > max) return;
        SaveData.Instance.LastArea.ID = next; Audio.Play(direction > 0 ? "event:/ui/world_map/icon/roll_right" : "event:/ui/world_map/icon/roll_left");
        chapterSelect.GetType().GetMethod("EaseCamera", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(chapterSelect, null);
    }

    private static void onChapterPanelUpdate(On.Celeste.OuiChapterPanel.orig_Update orig, OuiChapterPanel panel) {
        consumeDesktopInput();
        if (isAnyMenuOpen(panel.Scene)) { orig(panel); return; }
        orig(panel);
        if (panel == null || !panel.Focused) return;
        AndroidBridge.Vector2Like mousePos = AndroidBridge.TouchPosition();
        if (AndroidBridge.ConsumeTouchTap()) {
            if (mousePos.X > 1650f && mousePos.Y > 950f) { Audio.Play("event:/ui/world_map/chapter/back"); panel.Overworld.Goto<OuiChapterSelect>(); return; }

            var center = (Vector2)panel.GetType().GetProperty("OptionsRenderPosition", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
            var options = (IList)panel.GetType().GetProperty("options", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
            int currentOpt = (int)panel.GetType().GetProperty("option", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);

            for (int i = 0; i < options.Count; i++) {
                var pos = (Vector2)options[i].GetType().GetMethod("GetRenderPosition").Invoke(options[i], new object[] { center });
                if (Vector2.Distance(new Vector2(mousePos.X, mousePos.Y), pos) < 80f) {
                    if (currentOpt == i) {
                        bool selectingMode = (bool)panel.GetType().GetField("selectingMode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel);
                        if (selectingMode) {
                            if (!SaveData.Instance.FoundAnyCheckpoints(panel.Area)) panel.Start(null);
                            else { Audio.Play("event:/ui/world_map/chapter/level_select"); panel.GetType().GetMethod("Swap", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, null); }
                        } else {
                            panel.Start((string)options[i].GetType().GetField("CheckpointLevelName").GetValue(options[i]));
                        }
                    } else {
                        panel.GetType().GetProperty("option", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(panel, i);
                        Audio.Play("event:/ui/world_map/chapter/tab_roll_right");
                        panel.GetType().GetField("wiggler", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel).GetType().GetMethod("Start", new Type[0]).Invoke(panel.GetType().GetField("wiggler", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel), null);
                        if ((bool)panel.GetType().GetField("selectingMode", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(panel))
                            panel.GetType().GetMethod("UpdateStats", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(panel, new object[] { true, null, null, null });
                    }
                    return;
                }
            }
        }
    }

    private static void onJournalUpdate(On.Celeste.OuiJournal.orig_Update orig, OuiJournal journal) {
        consumeDesktopInput();
        orig(journal);
        if (journal == null || !journal.Focused) return;
        AndroidBridge.Vector2Like mousePos = AndroidBridge.TouchPosition();
        if (AndroidBridge.ConsumeTouchTap()) {
            if (mousePos.X > 1650f && mousePos.Y > 950f) { journal.GetType().GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(journal, null); return; }
            if (mousePos.Y > 800f) {
                if (mousePos.X < 960f && journal.PageIndex > 0) journal.Add(new Coroutine(journal.TurnPage(-1), true));
                else if (mousePos.X > 960f && journal.PageIndex < journal.Pages.Count - 1) journal.Add(new Coroutine(journal.TurnPage(1), true));
            }
        }
    }

    private static void onCreditsUpdate(On.Celeste.OuiCredits.orig_Update orig, OuiCredits credits) {
        consumeDesktopInput();
        if (credits.Focused && AndroidBridge.ConsumeTouchTap() && AndroidBridge.TouchPosition().X > 1650f && AndroidBridge.TouchPosition().Y > 950f) {
            credits.Overworld.Goto<OuiMainMenu>(); return;
        }
        orig(credits);
    }

    private static void onTextMenuUpdate(On.Celeste.TextMenu.orig_Update orig, TextMenu menu) {
        consumeDesktopInput();
        TextMenu top = getTopMenu(menu.Scene);
        if (menu != top) { orig(menu); return; }
        orig(menu);
        if (menu == null || !menu.Focused || menu.Items == null || menu.Items.Count == 0) return;

        menu.RecalculateSize();
        AndroidBridge.Vector2Like mousePos = AndroidBridge.TouchPosition();
        Vector2 origin = menu.Position - menu.Justify * new Vector2(menu.Width, menu.Height);

        float scroll = AndroidBridge.ConsumeTouchScroll();
        if (AndroidBridge.IsBrowser) {
            if (Math.Abs(scroll) > 34f) { menu.MoveSelection(scroll > 0f ? -1 : 1, true); }
        } else {
            scrollAccumulator += scroll;
            if (Math.Abs(scrollAccumulator) >= 120f) {
                menu.MoveSelection(scrollAccumulator > 0 ? -1 : 1, true);
                scrollAccumulator = 0f;
            }
        }

        float itemY = origin.Y;
        for (int i = 0; i < menu.Items.Count; i++) {
            TextMenu.Item item = menu.Items[i];
            if (item == null || !item.Visible) continue;
            float h = item.Height(), centerY = itemY + h * 0.5f, hitH = Math.Max(h, 80f);
            if (item.Hoverable && mousePos.X >= origin.X - 100f && mousePos.X <= origin.X + menu.Width + 100f && mousePos.Y >= centerY - hitH * 0.5f && mousePos.Y <= centerY + hitH * 0.5f) {
                if (menu.Current != item) {
                    menu.Current?.OnLeave?.Invoke();
                    menu.Selection = i;
                    item.OnEnter?.Invoke();
                    item.SelectWiggler?.Start();
                    Audio.Play("event:/ui/main/rollover_down");
                }
                break;
            }
            itemY += h + menu.ItemSpacing;
        }

        if (AndroidBridge.ConsumeTouchTap()) {
            if (mousePos.X > 1650f && mousePos.Y > 950f) { menu.OnCancel?.Invoke(); return; }
            itemY = origin.Y;
            for (int i = 0; i < menu.Items.Count; i++) {
                TextMenu.Item item = menu.Items[i];
                if (item == null || !item.Visible) continue;
                float h = item.Height(), centerY = itemY + h * 0.5f, hitH = Math.Max(h, 80f);
                if (item.Hoverable && mousePos.X >= origin.X - 100f && mousePos.X <= origin.X + menu.Width + 100f && mousePos.Y >= centerY - hitH * 0.5f && mousePos.Y <= centerY + hitH * 0.5f) {
                    item.ConfirmPressed(); item.OnPressed?.Invoke();
                    if (mousePos.X > origin.X + menu.Width - 160f) item.RightPressed();
                    else if (mousePos.X > origin.X + menu.Width - 320f && mousePos.X < origin.X + menu.Width - 160f) item.LeftPressed();
                    return;
                }
                itemY += h + menu.ItemSpacing;
            }
        }
    }

    private static void onRumble(On.Celeste.Input.orig_Rumble orig, RumbleStrength strength, RumbleLength length) {
        orig(strength, length);
        if (Settings.HapticFeedback) AndroidBridge.Haptic(strength.ToString(), length.ToString());
    }

    private static void onLevelUpdate(On.Celeste.Level.orig_Update orig, Level level) {
        orig(level);
        if (!Settings.CameraCentering || level == null || level.FrozenOrPaused || level.InCutscene || level.SkippingCutscene || level.Transitioning || level.Wipe != null) return;
        Player player = level.Tracker.GetEntity<Player>();
        if (player == null || player.Dead || !player.InControl || player.StateMachine.State == Player.StDummy || player.StateMachine.State == Player.StAttract) return;
        // The game camera lock doesn't always set CameraLockMode. Relaxing checks.
        if (level.Zoom != 1f || level.ScreenPadding != 0f) return;
        Rectangle b = level.Bounds;
        Vector2 target = new Vector2(Calc.Clamp(player.Center.X - 160f, b.Left, b.Right - 320f), Calc.Clamp(player.Center.Y - 90f, (float)b.Top, (float)b.Bottom - 180f));
        level.Camera.Position = Vector2.Lerp(level.Camera.Position, target, 1f - (float)Math.Pow(0.01, Engine.DeltaTime));
    }

    private class RenderComponent : Component {
        public Action OnRender;
        public RenderComponent(Action onRender) : base(true, true) => OnRender = onRender;
        public override void Render() => OnRender?.Invoke();
    }

    private class MultilineText : TextMenu.Item {
        private FancyText.Text group;
        public MultilineText(string text, float width) { Selectable = false; group = FancyText.Parse(text, (int)width, 100); }
        public override float Height() => group.Lines * ActiveFont.LineHeight * 0.6f + 20f;
        public override float LeftWidth() => 600f;
        public override void Render(Vector2 position, bool highlighted) => group.Draw(position + new Vector2(Container.Width * 0.5f, 0f), new Vector2(0.5f, 0.5f), Vector2.One * 0.6f, Container.Alpha);
    }

    private static void showAboutDialog() {
        TextMenu p = new TextMenu { Position = new Vector2(Engine.Width, Engine.Height) / 2f, Tag = Tags.HUD };
        p.Add(new TextMenu.Header(Dialog.Clean("ANDROID_PORT_ABOUT")));
        p.Add(new MultilineText(Dialog.Clean("ANDROID_PORT_ABOUT_BODY"), 700f));
        p.Add(new TextMenu.Button(Dialog.Clean("ANDROID_PORT_VISIT")).Pressed(() => AndroidBridge.OpenUrlPrompt("https://unlim8ted.com")));
        p.Add(new TextMenu.Button("CLOSE").Pressed(p.Close));
        Entity bg = new Entity { Depth = p.Depth + 1, Tag = Tags.HUD };
        bg.Add(new RenderComponent(() => {
            p.RecalculateSize();
            float w = p.Width + 60f, h = p.Height + 40f;
            Draw.Rect(p.Position.X - w * 0.5f, p.Position.Y - h * 0.5f, w, h, Color.Black * 0.95f);
            Draw.Rect(p.Position.X - w * 0.5f, p.Position.Y - h * 0.5f, w, 4f, Color.White);
            Draw.Rect(p.Position.X - w * 0.5f, p.Position.Y + h * 0.5f - 4f, w, 4f, Color.White);
            Draw.Rect(p.Position.X - w * 0.5f, p.Position.Y - h * 0.5f, 4f, h, Color.White);
            Draw.Rect(p.Position.X + w * 0.5f - 4f, p.Position.Y - h * 0.5f, 4f, h, Color.White);
        }));
        Engine.Scene.Add(p); Engine.Scene.Add(bg);
        p.OnClose += bg.RemoveSelf;
    }

    private static void showModBrowser() => AndroidBridge.OpenModBrowser();
    private static void syncAndroidOptions() {
        AndroidBridge.SetOption("touch_controls", Settings.TouchControls);
        AndroidBridge.SetOption("joystick_mode", Settings.JoystickMode);
        AndroidBridge.SetOption("joystick_snap_8way", Settings.JoystickSnap8Way);
        AndroidBridge.SetOption("haptic_feedback", Settings.HapticFeedback);
    }
    private static void onCreateMainMenuButtons(OuiMainMenu menu, System.Collections.Generic.List<MenuButton> buttons) {
        Vector2 pos = Vector2.Zero;
        buttons.Insert(Math.Max(0, buttons.Count - 1), new MainMenuSmallButton("ANDROID_PORT_ABOUT", "menu/options", menu, pos, pos, showAboutDialog));
    }
}
