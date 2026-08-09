using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Celeste;
using Celeste.Mod;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MouseUI;

public sealed class MouseUIModule : EverestModule {
    private static float scrollAccumulator;
    private static Vector2 desktopDragStart;
    private static bool desktopPotentialTap;
    private static bool desktopTapPending;
    private static bool ownsMInputDisabled;
    private static bool previousMInputDisabled;

    public override void Load() {
        Engine.Instance.IsMouseVisible = true;

        On.Celeste.TextMenu.Update += OnTextMenuUpdate;
        On.Celeste.OuiMainMenu.Update += OnMainMenuUpdate;
        On.Celeste.OuiFileSelect.Update += OnFileSelectUpdate;
        On.Celeste.OuiChapterSelect.Update += OnChapterSelectUpdate;
        On.Celeste.OuiChapterPanel.Update += OnChapterPanelUpdate;
        On.Celeste.OuiJournal.Update += OnJournalUpdate;
        On.Celeste.OuiTitleScreen.Update += OnTitleScreenUpdate;
        On.Celeste.OuiCredits.Update += OnCreditsUpdate;
        On.Monocle.Engine.Update += OnEngineUpdate;
        On.Monocle.MInput.Update += OnMInputUpdate;
        On.Celeste.ButtonUI.Render += OnButtonUIRender;
        On.Celeste.Overworld.ctor += OnOverworldCtor;
    }

    public override void Unload() {
        On.Celeste.TextMenu.Update -= OnTextMenuUpdate;
        On.Celeste.OuiMainMenu.Update -= OnMainMenuUpdate;
        On.Celeste.OuiFileSelect.Update -= OnFileSelectUpdate;
        On.Celeste.OuiChapterSelect.Update -= OnChapterSelectUpdate;
        On.Celeste.OuiChapterPanel.Update -= OnChapterPanelUpdate;
        On.Celeste.OuiJournal.Update -= OnJournalUpdate;
        On.Celeste.OuiTitleScreen.Update -= OnTitleScreenUpdate;
        On.Celeste.OuiCredits.Update -= OnCreditsUpdate;
        On.Monocle.Engine.Update -= OnEngineUpdate;
        On.Monocle.MInput.Update -= OnMInputUpdate;
        On.Celeste.ButtonUI.Render -= OnButtonUIRender;
        On.Celeste.Overworld.ctor -= OnOverworldCtor;

        if (ownsMInputDisabled) {
            MInput.Disabled = previousMInputDisabled;
            ownsMInputDisabled = false;
        }
    }

    private static bool UsingTouch => OptionalMobileBridge.TouchAvailable;

    private static void OnEngineUpdate(On.Monocle.Engine.orig_Update orig, Engine engine, GameTime gameTime) {
        desktopTapPending = false;
        orig(engine, gameTime);
    }

    private static void OnMInputUpdate(On.Monocle.MInput.orig_Update orig) {
        orig();

        if (!UsingTouch) {
            UpdateDesktopPointer();
        }

        bool shouldDisableMInputForTouch = UsingTouch &&
            (Engine.Scene is Overworld || IsAnyMenuOpen(Engine.Scene));

        if (shouldDisableMInputForTouch && !ownsMInputDisabled) {
            previousMInputDisabled = MInput.Disabled;
            MInput.Disabled = true;
            ownsMInputDisabled = true;
        } else if (!shouldDisableMInputForTouch && ownsMInputDisabled) {
            MInput.Disabled = previousMInputDisabled;
            ownsMInputDisabled = false;
        }
    }

    private static void UpdateDesktopPointer() {
        if (MInput.Mouse.PressedLeftButton) {
            desktopDragStart = MInput.Mouse.Position;
            desktopPotentialTap = true;
        }

        if (MInput.Mouse.CheckLeftButton && desktopPotentialTap &&
            Vector2.Distance(desktopDragStart, MInput.Mouse.Position) > 20f) {
            desktopPotentialTap = false;
        }

        if (MInput.Mouse.ReleasedLeftButton) {
            if (desktopPotentialTap) {
                desktopTapPending = true;
            }
            desktopPotentialTap = false;
        }
    }

    private static Vector2 PointerPosition() {
        if (UsingTouch) {
            return new Vector2(OptionalMobileBridge.TouchX(), OptionalMobileBridge.TouchY());
        }

        return MInput.Mouse.Position;
    }

    private static bool ConsumePointerTap() {
        if (UsingTouch) {
            return OptionalMobileBridge.ConsumeTouchTap();
        }

        if (!desktopTapPending) {
            return false;
        }

        desktopTapPending = false;
        return true;
    }

    private static float ConsumePointerScroll() {
        if (UsingTouch) {
            return OptionalMobileBridge.ConsumeTouchScroll();
        }

        return MInput.Mouse.WheelDelta;
    }

    private static void ConsumeGameMenuInputWhenUsingTouch() {
        if (!UsingTouch) {
            return;
        }

        Input.MenuConfirm.ConsumeBuffer();
        Input.MenuCancel.ConsumeBuffer();
        Input.MenuUp.ConsumeBuffer();
        Input.MenuDown.ConsumeBuffer();
        Input.MenuLeft.ConsumeBuffer();
        Input.MenuRight.ConsumeBuffer();
        Input.MenuJournal.ConsumeBuffer();
        Input.Pause.ConsumeBuffer();
        Input.ESC.ConsumeBuffer();
    }

    private static bool IsAnyMenuOpen(Scene scene) {
        return scene != null &&
            (scene.Entities.Any(e => e is TextMenu menu && menu.Visible && menu.Focused) ||
             scene is Overworld ov && (ov.IsCurrent<OuiJournal>() || ov.IsCurrent<OuiCredits>()));
    }

    private static TextMenu GetTopMenu(Scene scene) {
        return scene?.Entities.OfType<TextMenu>().LastOrDefault(m => m.Visible && m.Focused);
    }

    private static void OnOverworldCtor(
        On.Celeste.Overworld.orig_ctor orig,
        Overworld self,
        OverworldLoader loader) {

        orig(self, loader);

        Entity nav = new Entity {
            Tag = Tags.HUD,
            Depth = -1000000
        };
        nav.Add(new RenderComponent(DrawNavigationBar));
        self.Add(nav);
    }

    private static void DrawNavigationBar() {
        if (Engine.Scene is not Overworld scene) {
            return;
        }

        bool shouldDraw =
            scene.IsCurrent<OuiMainMenu>() ||
            scene.IsCurrent<OuiFileSelect>() ||
            scene.IsCurrent<OuiChapterSelect>() ||
            scene.IsCurrent<OuiChapterPanel>() ||
            scene.IsCurrent<OuiJournal>() ||
            scene.IsCurrent<OuiCredits>();

        if (IsAnyMenuOpen(scene) &&
            GetTopMenu(scene)?.Tag != (Tags.PauseUpdate | Tags.HUD) &&
            !scene.IsCurrent<OuiJournal>()) {
            shouldDraw = false;
        }

        if (!shouldDraw) {
            return;
        }

        const float x = 1680f;
        const float y = 1000f;
        const float w = 200f;
        const float h = 60f;

        Draw.Rect(x - 10f, y - 10f, w + 20f, h + 20f, Color.Black * 0.7f);
        Draw.Rect(x - 10f, y - 10f, w + 20f, 2f, Color.White);
        ActiveFont.DrawOutline(
            "GO BACK",
            new Vector2(x + w * 0.5f, y + h * 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.One * 0.8f,
            Color.White,
            2f,
            Color.Black);
    }

    private static bool IsBackButton(Vector2 pos) {
        return pos.X > 1650f && pos.Y > 950f;
    }

    private static void OnButtonUIRender(
        On.Celeste.ButtonUI.orig_Render orig,
        Vector2 position,
        string label,
        VirtualButton button,
        float scale,
        float justifyX,
        float wiggle,
        float alpha) {

        // Keep normal keyboard/gamepad prompts when MouseUI is being used only
        // with a desktop mouse. Touch mode has its own clickable navigation UI.
        if (UsingTouch) {
            return;
        }

        orig(position, label, button, scale, justifyX, wiggle, alpha);
    }

    private static void OnTitleScreenUpdate(
        On.Celeste.OuiTitleScreen.orig_Update orig,
        OuiTitleScreen title) {

        if (title.Focused && title.Visible && ConsumePointerTap()) {
            Audio.Play("event:/ui/main/button_select");
            title.Overworld.Goto<OuiMainMenu>();
            return;
        }

        orig(title);
    }

    private static void OnMainMenuUpdate(
        On.Celeste.OuiMainMenu.orig_Update orig,
        OuiMainMenu menu) {

        ConsumeGameMenuInputWhenUsingTouch();

        if (IsAnyMenuOpen(menu.Scene)) {
            orig(menu);
            return;
        }

        orig(menu);

        if (menu == null || !menu.Visible || !menu.Focused) {
            return;
        }

        Vector2 pointer = PointerPosition();

        foreach (MenuButton button in menu.Buttons) {
            if (button == null || !button.Visible || !MainMenuHit(button, pointer.X, pointer.Y)) {
                continue;
            }

            if (!button.Selected) {
                foreach (MenuButton other in menu.Buttons) {
                    if (other != null) {
                        other.Selected = other == button;
                    }
                }
                Audio.Play("event:/ui/main/rollover_down");
            }
            break;
        }

        if (!ConsumePointerTap()) {
            return;
        }

        if (IsBackButton(pointer)) {
            if (menu.Overworld.Next == null) {
                menu.Overworld.Goto<OuiTitleScreen>();
            }
            return;
        }

        foreach (MenuButton button in menu.Buttons) {
            if (button != null && button.Visible && MainMenuHit(button, pointer.X, pointer.Y)) {
                button.Confirm();
                return;
            }
        }
    }

    private static bool MainMenuHit(MenuButton button, float x, float y) {
        if (button is MainMenuClimb) {
            return x >= button.Position.X - 260f &&
                   x <= button.Position.X + 260f &&
                   y >= button.Position.Y - 40f &&
                   y <= button.Position.Y + 230f;
        }

        return x >= button.Position.X - 40f &&
               x <= button.Position.X + 480f &&
               y >= button.Position.Y - 48f &&
               y <= button.Position.Y + 48f;
    }

    private static void OnFileSelectUpdate(
        On.Celeste.OuiFileSelect.orig_Update orig,
        OuiFileSelect fileSelect) {

        ConsumeGameMenuInputWhenUsingTouch();

        if (IsAnyMenuOpen(fileSelect.Scene)) {
            orig(fileSelect);
            return;
        }

        orig(fileSelect);

        if (fileSelect == null || !fileSelect.Focused) {
            return;
        }

        Vector2 pointer = PointerPosition();

        if (!fileSelect.SlotSelected) {
            for (int i = 0; i < fileSelect.Slots.Length; i++) {
                OuiFileSelectSlot slot = fileSelect.Slots[i];
                if (slot == null || !slot.Visible) {
                    continue;
                }

                if (pointer.X >= slot.Position.X - 520f &&
                    pointer.X <= slot.Position.X + 520f &&
                    pointer.Y >= slot.Position.Y - 160f &&
                    pointer.Y <= slot.Position.Y + 160f) {

                    if (fileSelect.SlotIndex != i) {
                        fileSelect.SlotIndex = i;
                        Audio.Play("event:/ui/main/savefile_rollover_down");
                        ResetSaveSlotPositions(fileSelect);
                    }
                }
            }
        }

        if (!ConsumePointerTap()) {
            return;
        }

        if (IsBackButton(pointer)) {
            if (fileSelect.Overworld.Next == null) {
                if (fileSelect.SlotSelected) {
                    fileSelect.UnselectHighlighted();
                } else {
                    fileSelect.Overworld.Goto<OuiMainMenu>();
                }
            }
            return;
        }

        if (!fileSelect.SlotSelected) {
            OuiFileSelectSlot slot = fileSelect.Slots[fileSelect.SlotIndex];
            if (slot != null && slot.Visible &&
                pointer.X >= slot.Position.X - 520f &&
                pointer.X <= slot.Position.X + 520f &&
                pointer.Y >= slot.Position.Y - 160f &&
                pointer.Y <= slot.Position.Y + 160f) {

                Audio.Play("event:/ui/main/button_select");
                Audio.Play("event:/ui/main/whoosh_savefile_out");
                fileSelect.SelectSlot(reset: true);
            }
            return;
        }

        OuiFileSelectSlot selectedSlot = fileSelect.Slots[fileSelect.SlotIndex];
        if (selectedSlot == null) {
            return;
        }

        FieldInfo buttonsField = selectedSlot.GetType().GetField(
            "buttons",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo buttonIndexField = selectedSlot.GetType().GetField(
            "buttonIndex",
            BindingFlags.NonPublic | BindingFlags.Instance);
        FieldInfo selectedEaseField = selectedSlot.GetType().GetField(
            "selectedEase",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (buttonsField?.GetValue(selectedSlot) is not IList buttons ||
            buttonIndexField == null ||
            selectedEaseField == null) {
            return;
        }

        int buttonIndex = (int)buttonIndexField.GetValue(selectedSlot);
        float selectedEase = (float)selectedEaseField.GetValue(selectedSlot);
        float itemY = selectedSlot.Position.Y - 150f + 350f * selectedEase;

        for (int i = 0; i < buttons.Count; i++) {
            object button = buttons[i];
            if (button == null) {
                continue;
            }

            FieldInfo scaleField = button.GetType().GetField("Scale");
            FieldInfo actionField = button.GetType().GetField("Action");
            if (scaleField == null || actionField == null) {
                continue;
            }

            float height = ActiveFont.LineHeight * (float)scaleField.GetValue(button);

            if (pointer.X >= selectedSlot.Position.X - 300f &&
                pointer.X <= selectedSlot.Position.X + 300f &&
                pointer.Y >= itemY &&
                pointer.Y <= itemY + height) {

                if (buttonIndex != i) {
                    buttonIndexField.SetValue(selectedSlot, i);
                    Audio.Play("event:/ui/main/rollover_down");
                } else if (actionField.GetValue(button) is Action action) {
                    action();
                }
                return;
            }

            itemY += height + 15f;
        }
    }

    private static void ResetSaveSlotPositions(OuiFileSelect fileSelect) {
        for (int i = 0; i < fileSelect.Slots.Length; i++) {
            OuiFileSelectSlot slot = fileSelect.Slots[i];
            if (slot != null) {
                slot.MoveTo(slot.IdlePosition.X, slot.IdlePosition.Y);
            }
        }
    }

    private static void OnChapterSelectUpdate(
        On.Celeste.OuiChapterSelect.orig_Update orig,
        OuiChapterSelect chapterSelect) {

        ConsumeGameMenuInputWhenUsingTouch();

        if (IsAnyMenuOpen(chapterSelect.Scene)) {
            orig(chapterSelect);
            return;
        }

        orig(chapterSelect);

        if (chapterSelect == null || !chapterSelect.Focused || SaveData.Instance == null) {
            return;
        }

        Vector2 pointer = PointerPosition();
        FieldInfo iconsField = chapterSelect.GetType().GetField(
            "icons",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (iconsField?.GetValue(chapterSelect) is not List<OuiChapterSelectIcon> icons) {
            return;
        }

        for (int i = 0; i < icons.Count; i++) {
            OuiChapterSelectIcon icon = icons[i];
            if (icon == null ||
                icon.Area > SaveData.Instance.UnlockedAreas ||
                Vector2.Distance(pointer, icon.Position) >= 120f) {
                continue;
            }

            int previous = SaveData.Instance.LastArea.ID;
            if (previous != i) {
                int direction = Math.Sign(i - previous);
                SaveData.Instance.LastArea.ID = i;
                icon.Hovered(direction);

                chapterSelect.GetType()
                    .GetMethod("EaseCamera", BindingFlags.NonPublic | BindingFlags.Instance)
                    ?.Invoke(chapterSelect, null);

                Audio.Play(direction > 0
                    ? "event:/ui/world_map/icon/roll_right"
                    : "event:/ui/world_map/icon/roll_left");
            }
            break;
        }

        if (!ConsumePointerTap()) {
            return;
        }

        if (IsBackButton(pointer)) {
            if (chapterSelect.Overworld.Next == null) {
                chapterSelect.Overworld.Goto<OuiMainMenu>();
            }
            return;
        }

        if (pointer.X < 260f && pointer.Y > 760f) {
            Audio.Play("event:/ui/world_map/journal/select");
            chapterSelect.Overworld.Goto<OuiJournal>();
            return;
        }

        for (int i = 0; i < icons.Count; i++) {
            OuiChapterSelectIcon icon = icons[i];
            if (icon != null &&
                icon.Area <= SaveData.Instance.UnlockedAreas &&
                Vector2.Distance(pointer, icon.Position) < 120f) {

                Audio.Play("event:/ui/world_map/icon/select");
                SaveData.Instance.LastArea.ID = i;
                SaveData.Instance.LastArea.Mode = AreaMode.Normal;
                chapterSelect.Overworld.Goto<OuiChapterPanel>();
                return;
            }
        }
    }

    private static void OnChapterPanelUpdate(
        On.Celeste.OuiChapterPanel.orig_Update orig,
        OuiChapterPanel panel) {

        ConsumeGameMenuInputWhenUsingTouch();

        if (IsAnyMenuOpen(panel.Scene)) {
            orig(panel);
            return;
        }

        orig(panel);

        if (panel == null || !panel.Focused || !ConsumePointerTap()) {
            return;
        }

        Vector2 pointer = PointerPosition();

        if (IsBackButton(pointer)) {
            Audio.Play("event:/ui/world_map/chapter/back");
            panel.Overworld.Goto<OuiChapterSelect>();
            return;
        }

        Type type = panel.GetType();
        PropertyInfo renderPositionProperty = type.GetProperty(
            "OptionsRenderPosition",
            BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo optionsProperty = type.GetProperty(
            "options",
            BindingFlags.NonPublic | BindingFlags.Instance);
        PropertyInfo optionProperty = type.GetProperty(
            "option",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (renderPositionProperty == null ||
            optionsProperty?.GetValue(panel) is not IList options ||
            optionProperty == null) {
            return;
        }

        Vector2 center = (Vector2)renderPositionProperty.GetValue(panel);
        int currentOption = (int)optionProperty.GetValue(panel);

        for (int i = 0; i < options.Count; i++) {
            object option = options[i];
            if (option == null) {
                continue;
            }

            MethodInfo getRenderPosition = option.GetType().GetMethod("GetRenderPosition");
            if (getRenderPosition == null) {
                continue;
            }

            Vector2 position = (Vector2)getRenderPosition.Invoke(option, new object[] { center });
            if (Vector2.Distance(pointer, position) >= 80f) {
                continue;
            }

            if (currentOption == i) {
                FieldInfo selectingModeField = type.GetField(
                    "selectingMode",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                bool selectingMode = selectingModeField != null &&
                    (bool)selectingModeField.GetValue(panel);

                if (selectingMode) {
                    if (!SaveData.Instance.FoundAnyCheckpoints(panel.Area)) {
                        panel.Start(null);
                    } else {
                        Audio.Play("event:/ui/world_map/chapter/level_select");
                        type.GetMethod("Swap", BindingFlags.NonPublic | BindingFlags.Instance)
                            ?.Invoke(panel, null);
                    }
                } else {
                    FieldInfo checkpointField = option.GetType().GetField("CheckpointLevelName");
                    panel.Start((string)checkpointField?.GetValue(option));
                }
            } else {
                optionProperty.SetValue(panel, i);
                Audio.Play("event:/ui/world_map/chapter/tab_roll_right");

                FieldInfo wigglerField = type.GetField(
                    "wiggler",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                object wiggler = wigglerField?.GetValue(panel);
                wiggler?.GetType().GetMethod("Start", Type.EmptyTypes)?.Invoke(wiggler, null);

                FieldInfo selectingModeField = type.GetField(
                    "selectingMode",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (selectingModeField != null && (bool)selectingModeField.GetValue(panel)) {
                    type.GetMethod("UpdateStats", BindingFlags.NonPublic | BindingFlags.Instance)
                        ?.Invoke(panel, new object[] { true, null, null, null });
                }
            }

            return;
        }
    }

    private static void OnJournalUpdate(
        On.Celeste.OuiJournal.orig_Update orig,
        OuiJournal journal) {

        ConsumeGameMenuInputWhenUsingTouch();
        orig(journal);

        if (journal == null || !journal.Focused) {
            return;
        }

        Vector2 pointer = PointerPosition();
        if (!ConsumePointerTap()) {
            return;
        }

        if (IsBackButton(pointer)) {
            journal.GetType()
                .GetMethod("Close", BindingFlags.NonPublic | BindingFlags.Instance)
                ?.Invoke(journal, null);
            return;
        }

        if (pointer.Y > 800f) {
            if (pointer.X < 960f && journal.PageIndex > 0) {
                journal.Add(new Coroutine(journal.TurnPage(-1), true));
            } else if (pointer.X > 960f && journal.PageIndex < journal.Pages.Count - 1) {
                journal.Add(new Coroutine(journal.TurnPage(1), true));
            }
        }
    }

    private static void OnCreditsUpdate(
        On.Celeste.OuiCredits.orig_Update orig,
        OuiCredits credits) {

        ConsumeGameMenuInputWhenUsingTouch();

        if (credits.Focused) {
            Vector2 pointer = PointerPosition();
            if (IsBackButton(pointer) && ConsumePointerTap()) {
                credits.Overworld.Goto<OuiMainMenu>();
                return;
            }
        }

        orig(credits);
    }

    private static void OnTextMenuUpdate(
        On.Celeste.TextMenu.orig_Update orig,
        TextMenu menu) {

        ConsumeGameMenuInputWhenUsingTouch();

        TextMenu top = GetTopMenu(menu.Scene);
        if (menu != top) {
            orig(menu);
            return;
        }

        orig(menu);

        if (menu == null || !menu.Focused || menu.Items == null || menu.Items.Count == 0) {
            return;
        }

        menu.RecalculateSize();

        Vector2 pointer = PointerPosition();
        Vector2 origin = menu.Position - menu.Justify * new Vector2(menu.Width, menu.Height);

        float scroll = ConsumePointerScroll();
        if (UsingTouch) {
            if (Math.Abs(scroll) > 34f) {
                menu.MoveSelection(scroll > 0f ? -1 : 1, true);
            }
        } else {
            scrollAccumulator += scroll;
            if (Math.Abs(scrollAccumulator) >= 120f) {
                menu.MoveSelection(scrollAccumulator > 0f ? -1 : 1, true);
                scrollAccumulator = 0f;
            }
        }

        float itemY = origin.Y;
        for (int i = 0; i < menu.Items.Count; i++) {
            TextMenu.Item item = menu.Items[i];
            if (item == null || !item.Visible) {
                continue;
            }

            float height = item.Height();
            float centerY = itemY + height * 0.5f;
            float hitHeight = Math.Max(height, 80f);

            if (item.Hoverable &&
                pointer.X >= origin.X - 100f &&
                pointer.X <= origin.X + menu.Width + 100f &&
                pointer.Y >= centerY - hitHeight * 0.5f &&
                pointer.Y <= centerY + hitHeight * 0.5f) {

                if (menu.Current != item) {
                    menu.Current?.OnLeave?.Invoke();
                    menu.Selection = i;
                    item.OnEnter?.Invoke();
                    item.SelectWiggler?.Start();
                    Audio.Play("event:/ui/main/rollover_down");
                }
                break;
            }

            itemY += height + menu.ItemSpacing;
        }

        if (!ConsumePointerTap()) {
            return;
        }

        if (IsBackButton(pointer)) {
            menu.OnCancel?.Invoke();
            return;
        }

        itemY = origin.Y;
        for (int i = 0; i < menu.Items.Count; i++) {
            TextMenu.Item item = menu.Items[i];
            if (item == null || !item.Visible) {
                continue;
            }

            float height = item.Height();
            float centerY = itemY + height * 0.5f;
            float hitHeight = Math.Max(height, 80f);

            if (item.Hoverable &&
                pointer.X >= origin.X - 100f &&
                pointer.X <= origin.X + menu.Width + 100f &&
                pointer.Y >= centerY - hitHeight * 0.5f &&
                pointer.Y <= centerY + hitHeight * 0.5f) {

                item.ConfirmPressed();
                item.OnPressed?.Invoke();

                if (pointer.X > origin.X + menu.Width - 160f) {
                    item.RightPressed();
                } else if (pointer.X > origin.X + menu.Width - 320f &&
                           pointer.X < origin.X + menu.Width - 160f) {
                    item.LeftPressed();
                }
                return;
            }

            itemY += height + menu.ItemSpacing;
        }
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

    /// <summary>
    /// Optional runtime binding to MobileBridge. There is deliberately no
    /// MobileBridge assembly reference in MouseUI.csproj.
    /// </summary>
    private static class OptionalMobileBridge {
        private const string ApiTypeName = "Celeste.Mod.MobileBridge.MobileBridgeApi";

        private static Type apiType;
        private static PropertyInfo touchAvailableProperty;
        private static MethodInfo consumeTouchTapMethod;
        private static MethodInfo touchXMethod;
        private static MethodInfo touchYMethod;
        private static MethodInfo consumeTouchScrollMethod;
        private static int failedResolveFrames;

        public static bool TouchAvailable {
            get {
                EnsureResolved();
                if (touchAvailableProperty == null) {
                    return false;
                }

                try {
                    return (bool)touchAvailableProperty.GetValue(null);
                } catch {
                    ClearBinding();
                    return false;
                }
            }
        }

        public static bool ConsumeTouchTap() {
            EnsureResolved();
            try {
                return consumeTouchTapMethod != null &&
                    (bool)consumeTouchTapMethod.Invoke(null, null);
            } catch {
                ClearBinding();
                return false;
            }
        }

        public static float TouchX() {
            EnsureResolved();
            try {
                return touchXMethod == null ? -1f :
                    Convert.ToSingle(touchXMethod.Invoke(null, null));
            } catch {
                ClearBinding();
                return -1f;
            }
        }

        public static float TouchY() {
            EnsureResolved();
            try {
                return touchYMethod == null ? -1f :
                    Convert.ToSingle(touchYMethod.Invoke(null, null));
            } catch {
                ClearBinding();
                return -1f;
            }
        }

        public static float ConsumeTouchScroll() {
            EnsureResolved();
            try {
                return consumeTouchScrollMethod == null ? 0f :
                    Convert.ToSingle(consumeTouchScrollMethod.Invoke(null, null));
            } catch {
                ClearBinding();
                return 0f;
            }
        }

        private static void EnsureResolved() {
            if (apiType != null) {
                return;
            }

            // Avoid scanning every loaded assembly every frame when the bridge
            // simply is not installed. Retry periodically so hot-loading or a
            // later module load still works.
            if (failedResolveFrames > 0) {
                failedResolveFrames--;
                return;
            }

            Type found = Type.GetType(ApiTypeName + ", MobileBridge", false);

            if (found == null) {
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies()) {
                    found = assembly.GetType(ApiTypeName, false);
                    if (found != null) {
                        break;
                    }
                }
            }

            if (found == null) {
                failedResolveFrames = 60;
                return;
            }

            apiType = found;
            touchAvailableProperty = apiType.GetProperty(
                "TouchAvailable",
                BindingFlags.Public | BindingFlags.Static);
            consumeTouchTapMethod = apiType.GetMethod(
                "ConsumeTouchTap",
                BindingFlags.Public | BindingFlags.Static);
            touchXMethod = apiType.GetMethod(
                "TouchX",
                BindingFlags.Public | BindingFlags.Static);
            touchYMethod = apiType.GetMethod(
                "TouchY",
                BindingFlags.Public | BindingFlags.Static);
            consumeTouchScrollMethod = apiType.GetMethod(
                "ConsumeTouchScroll",
                BindingFlags.Public | BindingFlags.Static);

            if (touchAvailableProperty == null ||
                consumeTouchTapMethod == null ||
                touchXMethod == null ||
                touchYMethod == null ||
                consumeTouchScrollMethod == null) {
                ClearBinding();
                failedResolveFrames = 60;
            }
        }

        private static void ClearBinding() {
            apiType = null;
            touchAvailableProperty = null;
            consumeTouchTapMethod = null;
            touchXMethod = null;
            touchYMethod = null;
            consumeTouchScrollMethod = null;
        }
    }
}
