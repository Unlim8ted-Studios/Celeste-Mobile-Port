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
    private static TextMenu hoveredTextMenu;
    private static int hoveredTextMenuItem = -1;
    private static readonly Dictionary<TextMenu, float> textMenuScrollOffsets = new();
    private static readonly Dictionary<TextMenu, List<RenderedTextMenuItem>> renderedTextMenuItems = new();
    private static Vector2 desktopDragStart;
    private static bool desktopPotentialTap;
    private static bool desktopTapPending;
    private static bool ownsMInputDisabled;
    private static bool previousMInputDisabled;
    private static bool backPromptVisibleThisRender;
    private static bool backPromptVisibleForInput;
    private static BackButtonOverlay backButtonOverlay;

    public override void Load() {
        Engine.Instance.IsMouseVisible = true;

        On.Celeste.TextMenu.Update += OnTextMenuUpdate;
        On.Celeste.TextMenu.Render += OnTextMenuRender;
        On.Celeste.TextMenu.Item.Render += OnTextMenuItemRender;
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
    }

    public override void Unload() {
        On.Celeste.TextMenu.Update -= OnTextMenuUpdate;
        On.Celeste.TextMenu.Render -= OnTextMenuRender;
        On.Celeste.TextMenu.Item.Render -= OnTextMenuItemRender;
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

        if (ownsMInputDisabled) {
            MInput.Disabled = previousMInputDisabled;
            ownsMInputDisabled = false;
        }

        hoveredTextMenu = null;
        hoveredTextMenuItem = -1;
        textMenuScrollOffsets.Clear();
        renderedTextMenuItems.Clear();
        backButtonOverlay?.RemoveSelf();
        backButtonOverlay = null;
    }

    private static bool UsingTouch => OptionalMobileBridge.TouchAvailable;

    private static void OnEngineUpdate(On.Monocle.Engine.orig_Update orig, Engine engine, GameTime gameTime) {
        backPromptVisibleForInput = backPromptVisibleThisRender;
        backPromptVisibleThisRender = false;
        desktopTapPending = false;
        orig(engine, gameTime);
        EnsureBackButtonOverlay();
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

    private static void DrawBackButton() {
        const float x = 1680f;
        const float y = 1000f;
        const float w = 200f;
        const float h = 60f;

        Draw.Rect(
            x - 10f,
            y - 10f,
            w + 20f,
            h + 20f,
            Color.Black * 0.7f);

        Draw.Rect(
            x - 10f,
            y - 10f,
            w + 20f,
            2f,
            Color.White);

        ActiveFont.DrawOutline(
            "GO BACK",
            new Vector2(
                x + w * 0.5f,
                y + h * 0.5f),
            new Vector2(0.5f, 0.5f),
            Vector2.One * 0.8f,
            Color.White,
            2f,
            Color.Black);
    }

    private static bool IsBackButton(Vector2 pos) {
        return pos.X > 1620f &&
            pos.Y > 920f;
    }

    private static void EnsureBackButtonOverlay() {
        Scene scene = Engine.Scene;
        if (scene == null) {
            backButtonOverlay = null;
            return;
        }

        if (backButtonOverlay?.Scene == scene) {
            return;
        }

        backButtonOverlay?.RemoveSelf();
        backButtonOverlay = new BackButtonOverlay();
        scene.Add(backButtonOverlay);
    }

    private static bool ShouldShowBackButton() {
        Scene scene = Engine.Scene;

        return backPromptVisibleThisRender ||
            backPromptVisibleForInput ||
            scene?.Entities.Any(e => e is TextMenu menu && menu.Visible && menu.Focused) == true ||
            scene is Overworld overworld &&
            (overworld.IsCurrent<OuiMainMenu>() ||
             overworld.IsCurrent<OuiFileSelect>() ||
             overworld.IsCurrent<OuiChapterSelect>() ||
             overworld.IsCurrent<OuiChapterPanel>() ||
             overworld.IsCurrent<OuiJournal>() ||
             overworld.IsCurrent<OuiCredits>());
    }

    private sealed class BackButtonOverlay : Entity {
        public BackButtonOverlay() {
            Tag = Tags.HUD | Tags.PauseUpdate;
            Depth = -2100000000;
        }

        public override void Render() {
            if (ShouldShowBackButton()) {
                DrawBackButton();
            }
        }
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

        // MouseUI replaces the bottom-screen Confirm/Back controller hints.
        // A clickable GO BACK button exists only on frames where Celeste itself
        // attempts to render the normal MenuCancel / "Back X" prompt.
        if (ReferenceEquals(button, Input.MenuCancel)) {
            backPromptVisibleThisRender = true;
            return;
        }

        if (ReferenceEquals(button, Input.MenuConfirm)) {
            return;
        }

        orig(
            position,
            label,
            button,
            scale,
            justifyX,
            wiggle,
            alpha);
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

        MenuButton hovered = null;
        foreach (MenuButton button in menu.Buttons) {
            if (button != null && button.Visible && MainMenuHit(button, pointer.X, pointer.Y)) {
                hovered = button;
                break;
            }
        }

        foreach (MenuButton button in menu.Buttons) {
            if (button == null) {
                continue;
            }

            bool selected = ReferenceEquals(button, hovered);
            if (button.Selected != selected) {
                button.Selected = selected;
            }
        }

        if (IsBackButton(pointer)) {
            if (!ConsumePointerTap()) {
                return;
            }

            if (menu.Overworld.Next == null) {
                menu.Overworld.Goto<OuiTitleScreen>();
            }
            return;
        }

        if (!ConsumePointerTap()) {
            return;
        }

        if (hovered != null) {
            hovered.Confirm();
            return;
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
                        return;
                    }

                    Audio.Play("event:/ui/main/button_select");
                    Audio.Play("event:/ui/main/whoosh_savefile_out");
                    fileSelect.SelectSlot(reset: true);
                    return;
                }
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

        if (IsBackButton(pointer) && ConsumePointerTap()) {
            if (chapterSelect.Overworld.Next == null) {
                chapterSelect.Overworld.Goto<OuiMainMenu>();
            }
            return;
        }

        FieldInfo iconsField = chapterSelect.GetType().GetField(
            "icons",
            BindingFlags.NonPublic | BindingFlags.Instance);

        if (iconsField?.GetValue(chapterSelect) is not List<OuiChapterSelectIcon> icons) {
            return;
        }

        if (!ConsumePointerTap()) {
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

                if (SaveData.Instance.LastArea.ID != i) {
                    MoveChapterSelectionTo(chapterSelect, icons, i);
                    return;
                }

                SaveData.Instance.LastArea.Mode = AreaMode.Normal;
                Audio.Play("event:/ui/world_map/icon/select");
                chapterSelect.Overworld.Goto<OuiChapterPanel>();
                return;
            }
        }
    }

    private static void MoveChapterSelectionTo(
        OuiChapterSelect chapterSelect,
        List<OuiChapterSelectIcon> icons,
        int target) {

        int previous = SaveData.Instance.LastArea.ID;
        if (previous == target) {
            return;
        }

        int direction = Math.Sign(target - previous);
        SaveData.Instance.LastArea.ID = target;
        icons[target]?.Hovered(direction);

        chapterSelect.GetType()
            .GetMethod("EaseCamera", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.Invoke(chapterSelect, null);

        Audio.Play(direction > 0
            ? "event:/ui/world_map/icon/roll_right"
            : "event:/ui/world_map/icon/roll_left");
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

        if (panel == null || !panel.Focused) {
            return;
        }

        Vector2 pointer = PointerPosition();

        if (IsBackButton(pointer) && ConsumePointerTap()) {
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

        if (!ConsumePointerTap()) {
            return;
        }

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
                SetChapterPanelOption(
                    panel,
                    type,
                    optionProperty,
                    i,
                    Math.Sign(i - currentOption));
            }

            return;
        }
    }

    private static void SetChapterPanelOption(
        OuiChapterPanel panel,
        Type type,
        PropertyInfo optionProperty,
        int target,
        int direction) {

        optionProperty.SetValue(panel, target);
        Audio.Play(direction > 0
            ? "event:/ui/world_map/chapter/tab_roll_right"
            : "event:/ui/world_map/chapter/tab_roll_left");

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

        Vector2 pointer = PointerPosition();
        if (IsBackButton(pointer) && ConsumePointerTap()) {
            menu?.OnCancel?.Invoke();
            return;
        }

        if (menu == null || !menu.Focused || menu.Items == null || menu.Items.Count == 0) {
            if (ReferenceEquals(hoveredTextMenu, menu)) {
                hoveredTextMenu = null;
                hoveredTextMenuItem = -1;
            }
            return;
        }

        menu.RecalculateSize();

        Vector2 origin = menu.Position - menu.Justify * new Vector2(menu.Width, menu.Height);
        hoveredTextMenu = menu;
        hoveredTextMenuItem = FindHoveredTextMenuItem(menu, pointer, origin);

        float scroll = ConsumePointerScroll();
        if (UsingTouch) {
            if (Math.Abs(scroll) > 34f) {
                ScrollTextMenu(menu, scroll);
            }
        } else {
            if (Math.Abs(scroll) > 0f) {
                ScrollTextMenu(menu, scroll);
            }
        }

        if (!ConsumePointerTap()) {
            return;
        }

        if (hoveredTextMenuItem >= 0 &&
            hoveredTextMenuItem < menu.Items.Count) {

            TextMenu.Item item = menu.Items[hoveredTextMenuItem];
            if (item == null || !item.Visible || !item.Hoverable) {
                return;
            }

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
    }

    private static int FindHoveredTextMenuItem(
        TextMenu menu,
        Vector2 pointer,
        Vector2 origin) {

        if (renderedTextMenuItems.TryGetValue(menu, out List<RenderedTextMenuItem> rendered)) {
            for (int i = 0; i < rendered.Count; i++) {
                RenderedTextMenuItem hit = rendered[i];
                if (hit.Item == null || !hit.Item.Visible || !hit.Item.Hoverable) {
                    continue;
                }

                float centerY = hit.Position.Y + hit.Height * 0.5f;
                float hitHeight = Math.Max(hit.Height, 80f);

                if (pointer.X >= hit.Position.X - 100f &&
                    pointer.X <= hit.Position.X + menu.Width + 100f &&
                    pointer.Y >= centerY - hitHeight * 0.5f &&
                    pointer.Y <= centerY + hitHeight * 0.5f) {

                    return menu.Items.IndexOf(hit.Item);
                }
            }
        }

        textMenuScrollOffsets.TryGetValue(menu, out float offset);
        float itemY = origin.Y + offset;
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

                return i;
            }

            itemY += height + menu.ItemSpacing;
        }

        return -1;
    }

    private static void ScrollTextMenu(
        TextMenu menu,
        float delta) {

        if (menu == null) {
            return;
        }

        textMenuScrollOffsets.TryGetValue(menu, out float offset);
        offset += delta;
        textMenuScrollOffsets[menu] = ClampTextMenuScroll(menu, offset);
    }

    private static float ClampTextMenuScroll(
        TextMenu menu,
        float offset) {

        if (menu?.Items == null || menu.Items.Count == 0) {
            return 0f;
        }

        float contentHeight = 0f;
        int visible = 0;
        foreach (TextMenu.Item item in menu.Items) {
            if (item == null || !item.Visible) {
                continue;
            }

            if (visible > 0) {
                contentHeight += menu.ItemSpacing;
            }

            contentHeight += item.Height();
            visible++;
        }

        float viewportHeight = Engine.Height - 180f;
        if (contentHeight <= viewportHeight) {
            return 0f;
        }

        float min = viewportHeight - contentHeight;
        return Calc.Clamp(offset, min, 0f);
    }

    private static void OnTextMenuRender(
        On.Celeste.TextMenu.orig_Render orig,
        TextMenu menu) {

        if (menu != null) {
            renderedTextMenuItems[menu] = new List<RenderedTextMenuItem>();
        }

        if (menu == null) {
            orig(menu);
            return;
        }

        textMenuScrollOffsets.TryGetValue(menu, out float offset);
        Vector2 originalPosition = menu.Position;
        int originalSelection = menu.Selection;
        bool originalAutoScroll = menu.AutoScroll;

        if (Math.Abs(offset) > 0.01f) {
            menu.Position = originalPosition + new Vector2(0f, offset);
        }

        if (ReferenceEquals(menu, hoveredTextMenu) &&
            hoveredTextMenuItem >= 0 &&
            hoveredTextMenuItem < menu.Items.Count) {

            menu.Selection = hoveredTextMenuItem;
            menu.AutoScroll = false;
        }

        try {
            orig(menu);
        } finally {
            menu.Position = originalPosition;
            menu.Selection = originalSelection;
            menu.AutoScroll = originalAutoScroll;
        }
    }

    private static void OnTextMenuItemRender(
        On.Celeste.TextMenu.Item.orig_Render orig,
        TextMenu.Item item,
        Vector2 position,
        bool highlighted) {

        TextMenu menu = item?.Container;
        if (item?.Container != null &&
            ReferenceEquals(item.Container, hoveredTextMenu) &&
            item.Container.Items != null &&
            hoveredTextMenuItem >= 0 &&
            hoveredTextMenuItem < item.Container.Items.Count &&
            ReferenceEquals(item.Container.Items[hoveredTextMenuItem], item)) {

            highlighted = true;
        }

        if (menu != null && menu.Items != null) {
            if (!renderedTextMenuItems.TryGetValue(menu, out List<RenderedTextMenuItem> rendered)) {
                rendered = new List<RenderedTextMenuItem>();
                renderedTextMenuItems[menu] = rendered;
            }

            rendered.Add(new RenderedTextMenuItem(
                item,
                position,
                item.Height()));
        }

        orig(item, position, highlighted);
    }

    private readonly struct RenderedTextMenuItem {
        public readonly TextMenu.Item Item;
        public readonly Vector2 Position;
        public readonly float Height;

        public RenderedTextMenuItem(
            TextMenu.Item item,
            Vector2 position,
            float height) {

            Item = item;
            Position = position;
            Height = height;
        }
    }

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
