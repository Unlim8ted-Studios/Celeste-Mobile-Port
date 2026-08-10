using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Monocle;

namespace Celeste.Mod.BetterMapEditor;

public sealed class BetterMapEditorModule : EverestModule {
    public static BetterMapEditorModule Instance { get; private set; }

    private const string MetadataFileName = ".better-map-editor.json";
    private static readonly JsonSerializerOptions JsonOptions = new() {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private static Action<OuiMainMenu> pendingMainMenuAction;
    private static EditorProject activeProject;
    private static string activeProjectDirectory;

    public BetterMapEditorModule() {
        Instance = this;
    }

    public override void Load() {
        Everest.Events.MainMenu.OnCreateButtons += OnCreateMainMenuButtons;
        On.Celeste.OuiMainMenu.Update += OnMainMenuUpdate;
    }

    public override void Unload() {
        Everest.Events.MainMenu.OnCreateButtons -= OnCreateMainMenuButtons;
        On.Celeste.OuiMainMenu.Update -= OnMainMenuUpdate;
        pendingMainMenuAction = null;
        activeProject = null;
        activeProjectDirectory = null;
    }

    private static void OnCreateMainMenuButtons(OuiMainMenu menu, List<MenuButton> buttons) {
        Vector2 pos = Vector2.Zero;

        int multiplayerIndex = buttons.FindIndex(button =>
            button is MainMenuSmallButton small &&
            string.Equals(
                small.LabelName,
                "MOBILEMULTIPLAYER_MAINMENU",
                StringComparison.OrdinalIgnoreCase));

        int climbIndex = buttons.FindIndex(button =>
            button is MainMenuClimb);

        int index =
            multiplayerIndex >= 0
                ? multiplayerIndex + 1
                : climbIndex >= 0
                    ? climbIndex + 1
                    : 0;

        buttons.Insert(
            Math.Clamp(index, 0, buttons.Count),
            new MainMenuSmallButton(
                "BETTERMAPEDITOR_MAINMENU",
                "menu/options",
                menu,
                pos,
                pos,
                () => ShowBrowser(menu)));
    }

    private static void OnMainMenuUpdate(On.Celeste.OuiMainMenu.orig_Update orig, OuiMainMenu menu) {
        orig(menu);

        if (pendingMainMenuAction == null || menu == null || !menu.Visible || !menu.Focused)
            return;

        Action<OuiMainMenu> action = pendingMainMenuAction;
        pendingMainMenuAction = null;
        Engine.Scene.OnEndOfFrame += () => action(menu);
    }

    private static void ShowBrowser(OuiMainMenu owner) {
        CloseOurOverlays();

        TextMenu menu = CreateMenu("MAP EDITOR + BROWSER", owner);
        menu.Add(new TextMenu.Button("CREATE NEW MAP MOD").Pressed(() => {
            CloseMenu(menu);
            PromptString(owner, "NewMapMod", 40, value => {
                string displayName = CleanDisplayName(value, "New Map Mod");
                string slug = MakeUniqueModSlug(Slugify(displayName));
                string dir = Path.Combine(GetModsDirectory(), slug);

                EditorProject project = new() {
                    Name = displayName,
                    ModName = slug,
                    Chapters = new List<EditorChapter>()
                };

                Directory.CreateDirectory(dir);
                SaveProject(project, dir);
                activeProject = project;
                activeProjectDirectory = dir;
                pendingMainMenuAction = main => ShowProject(main, project, dir);
            });
        }));

        List<ProjectEntry> projects = ScanProjects();
        if (projects.Count > 0)
            menu.Add(new TextMenu.SubHeader("EDITABLE PROJECTS", false));

        foreach (ProjectEntry entry in projects) {
            ProjectEntry captured = entry;
            menu.Add(new TextMenu.Button(captured.Project.Name).Pressed(() => {
                activeProject = captured.Project;
                activeProjectDirectory = captured.Directory;
                CloseMenu(menu);
                ShowProject(owner, captured.Project, captured.Directory);
            }));
        }

        List<InstalledMapMod> installed = ScanInstalledMapMods(projects.Select(p => p.Directory));
        if (installed.Count > 0) {
            menu.Add(new TextMenu.SubHeader("OTHER INSTALLED MAP MODS", false));
            foreach (InstalledMapMod mod in installed) {
                InstalledMapMod captured = mod;
                menu.Add(new TextMenu.Button($"{captured.Name}  ({captured.MapCount} MAPS)").Pressed(() => {
                    CloseMenu(menu);
                    ShowInfo(owner,
                        captured.Name,
                        "This map mod was not created by BetterMapEditor, so it is listed read-only. " +
                        "BetterMapEditor never overwrites an unknown map binary.",
                        main => ShowBrowser(main));
                }));
            }
        }

        if (projects.Count == 0 && installed.Count == 0)
            menu.Add(new TextMenu.SubHeader("NO MAP MODS FOUND", false));

        menu.Add(new TextMenu.Button("CLOSE").Pressed(() => CloseMenu(menu)));
    }

    private static void ShowProject(OuiMainMenu owner, EditorProject project, string projectDirectory) {
        activeProject = project;
        activeProjectDirectory = projectDirectory;
        SaveProject(project, projectDirectory);

        TextMenu menu = CreateMenu(project.Name.ToUpperInvariant(), owner);
        menu.Add(new TextMenu.SubHeader($"MOD ID: {project.ModName}", false));
        menu.Add(new TextMenu.Button("ADD CHAPTER").Pressed(() => {
            CloseMenu(menu);
            PromptString(owner, $"Chapter {project.Chapters.Count + 1}", 40, value => {
                string name = CleanDisplayName(value, $"Chapter {project.Chapters.Count + 1}");
                string baseSlug = Slugify(name);
                string slug = MakeUniqueChapterSlug(project, baseSlug);
                EditorChapter chapter = new() {
                    Name = name,
                    Slug = slug,
                    Rooms = new List<EditorRoom> { EditorRoom.CreateDefault("room_1") }
                };
                project.Chapters.Add(chapter);
                SaveProject(project, projectDirectory);
                pendingMainMenuAction = main => ShowChapter(main, project, projectDirectory, chapter);
            });
        }));

        if (project.Chapters.Count > 0)
            menu.Add(new TextMenu.SubHeader("CHAPTERS", false));

        for (int i = 0; i < project.Chapters.Count; i++) {
            EditorChapter chapter = project.Chapters[i];
            int number = i + 1;
            menu.Add(new TextMenu.Button($"{number:00}  {chapter.Name}  ({chapter.Rooms.Count} ROOMS)").Pressed(() => {
                CloseMenu(menu);
                ShowChapter(owner, project, projectDirectory, chapter);
            }));
        }

        menu.Add(new TextMenu.Button("BUILD / SAVE ALL MAPS").Pressed(() => {
            BuildAllMaps(project, projectDirectory);
            SaveProject(project, projectDirectory);
            CloseMenu(menu);
            ShowInfo(owner,
                "MAPS WRITTEN",
                $"Saved {project.Chapters.Count} chapter map(s) into Mods/{project.ModName}/Maps/{project.ModName}. " +
                "Restart Celeste before playing a newly-created map mod so Everest can discover it.",
                main => ShowProject(main, project, projectDirectory));
        }));

        menu.Add(new TextMenu.Button("RENAME MAP MOD").Pressed(() => {
            CloseMenu(menu);
            PromptString(owner, project.Name, 40, value => {
                project.Name = CleanDisplayName(value, project.Name);
                SaveProject(project, projectDirectory);
                pendingMainMenuAction = main => ShowProject(main, project, projectDirectory);
            });
        }));

        menu.Add(new TextMenu.Button("BACK TO BROWSER").Pressed(() => {
            CloseMenu(menu);
            ShowBrowser(owner);
        }));
    }

    private static void ShowChapter(OuiMainMenu owner, EditorProject project, string projectDirectory, EditorChapter chapter) {
        TextMenu menu = CreateMenu(chapter.Name.ToUpperInvariant(), owner);
        int chapterIndex = project.Chapters.IndexOf(chapter);
        menu.Add(new TextMenu.SubHeader($"CHAPTER {chapterIndex + 1:00}  /  SID {project.ModName}/{chapter.Slug}", false));

        menu.Add(new TextMenu.Button("ADD ROOM").Pressed(() => {
            string name = MakeUniqueRoomName(chapter, $"room_{chapter.Rooms.Count + 1}");
            EditorRoom room = EditorRoom.CreateDefault(name);
            chapter.Rooms.Add(room);
            SaveProject(project, projectDirectory);
            CloseMenu(menu);
            ShowRoomEditor(owner, project, projectDirectory, chapter, room);
        }));

        menu.Add(new TextMenu.Button("RENAME CHAPTER").Pressed(() => {
            CloseMenu(menu);
            PromptString(owner, chapter.Name, 40, value => {
                chapter.Name = CleanDisplayName(value, chapter.Name);
                SaveProject(project, projectDirectory);
                pendingMainMenuAction = main => ShowChapter(main, project, projectDirectory, chapter);
            });
        }));

        menu.Add(new TextMenu.Button("BUILD THIS CHAPTER").Pressed(() => {
            BuildChapter(project, chapter, projectDirectory);
            SaveProject(project, projectDirectory);
            CloseMenu(menu);
            ShowInfo(owner,
                "CHAPTER WRITTEN",
                $"Saved {project.ModName}/{chapter.Slug}.bin with {chapter.Rooms.Count} room(s).",
                main => ShowChapter(main, project, projectDirectory, chapter));
        }));

        if (chapter.Rooms.Count > 0)
            menu.Add(new TextMenu.SubHeader("ROOMS", false));

        for (int i = 0; i < chapter.Rooms.Count; i++) {
            EditorRoom room = chapter.Rooms[i];
            menu.Add(new TextMenu.Button($"{i + 1:00}  {room.Name}  {room.WidthTiles}x{room.HeightTiles}").Pressed(() => {
                CloseMenu(menu);
                ShowRoomEditor(owner, project, projectDirectory, chapter, room);
            }));
        }

        menu.Add(new TextMenu.Button("BACK").Pressed(() => {
            CloseMenu(menu);
            ShowProject(owner, project, projectDirectory);
        }));
    }

    private static void ShowRoomEditor(OuiMainMenu owner, EditorProject project, string projectDirectory, EditorChapter chapter, EditorRoom room) {
        CloseOurOverlays();
        RoomEditorOverlay overlay = new(owner, project, projectDirectory, chapter, room);
        Engine.Scene.Add(overlay);
    }

    private static void ShowInfo(OuiMainMenu owner, string title, string text, Action<OuiMainMenu> onClose) {
        TextMenu menu = CreateMenu(title, owner);
        menu.Add(new WrappedTextItem(text, 900f));
        menu.Add(new TextMenu.Button("OK").Pressed(() => {
            CloseMenu(menu);
            onClose?.Invoke(owner);
        }));
    }

    private static TextMenu CreateMenu(string title, OuiMainMenu owner) {
        CloseOurOverlays();

        TextMenu menu = new() {
            Position = new Vector2(Engine.Width, Engine.Height) / 2f,
            Tag = Tags.HUD,
            ItemSpacing = 12f
        };
        menu.Depth = -2000000;
        menu.Add(new TextMenu.Header(title));

        ModalBackdrop backdrop = new(menu);
        OptionalPointerController pointer = new(menu);
        menu.OnCancel = () => CloseMenu(menu);
        menu.OnClose += () => {
            backdrop.RemoveSelf();
            pointer.RemoveSelf();
        };

        Engine.Scene.Add(backdrop);
        Engine.Scene.Add(menu);
        Engine.Scene.Add(pointer);
        return menu;
    }

    private static void CloseMenu(TextMenu menu) {
        if (menu != null && menu.Scene != null)
            menu.Close();
    }

    private static void CloseOurOverlays() {
        Scene scene = Engine.Scene;
        if (scene == null)
            return;

        foreach (RoomEditorOverlay overlay in scene.Entities.OfType<RoomEditorOverlay>().ToArray())
            overlay.RemoveSelf();
        foreach (OptionalPointerController pointer in scene.Entities.OfType<OptionalPointerController>().ToArray())
            pointer.RemoveSelf();
        foreach (ModalBackdrop backdrop in scene.Entities.OfType<ModalBackdrop>().ToArray())
            backdrop.RemoveSelf();
    }

    private static void PromptString(OuiMainMenu owner, string initialValue, int maxLength, Action<string> accepted) {
        if (owner?.Overworld == null)
            return;

        Audio.Play("event:/ui/main/savefile_rename_start");
        owner.Overworld.Goto<OuiModOptionString>().Init<OuiMainMenu>(
            initialValue ?? string.Empty,
            value => accepted?.Invoke(value ?? string.Empty),
            maxLength
        );
    }

    private static string GetModsDirectory() {
        string gamePath = null;
        try {
            PropertyInfo property = typeof(Everest).GetProperty("PathGame", BindingFlags.Public | BindingFlags.Static);
            gamePath = property?.GetValue(null) as string;
        } catch {
        }

        if (string.IsNullOrWhiteSpace(gamePath))
            gamePath = AppContext.BaseDirectory;

        string mods = Path.Combine(gamePath, "Mods");
        Directory.CreateDirectory(mods);
        return mods;
    }

    private static List<ProjectEntry> ScanProjects() {
        List<ProjectEntry> result = new();
        string mods = GetModsDirectory();

        foreach (string dir in Directory.EnumerateDirectories(mods)) {
            string metadata = Path.Combine(dir, MetadataFileName);
            if (!File.Exists(metadata))
                continue;

            try {
                EditorProject project = JsonSerializer.Deserialize<EditorProject>(File.ReadAllText(metadata), JsonOptions);
                if (project != null) {
                    project.Normalize();
                    result.Add(new ProjectEntry(project, dir));
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "BetterMapEditor", $"Could not load {metadata}: {e.Message}");
            }
        }

        return result.OrderBy(p => p.Project.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static List<InstalledMapMod> ScanInstalledMapMods(IEnumerable<string> editableDirectories) {
        HashSet<string> editable = new(editableDirectories.Select(Path.GetFullPath), StringComparer.OrdinalIgnoreCase);
        List<InstalledMapMod> result = new();

        foreach (string dir in Directory.EnumerateDirectories(GetModsDirectory())) {
            string full = Path.GetFullPath(dir);
            if (editable.Contains(full))
                continue;

            string maps = Path.Combine(dir, "Maps");
            if (!Directory.Exists(maps))
                continue;

            int count;
            try {
                count = Directory.EnumerateFiles(maps, "*.bin", SearchOption.AllDirectories).Count();
            } catch {
                continue;
            }

            if (count > 0)
                result.Add(new InstalledMapMod(Path.GetFileName(dir), count));
        }

        return result.OrderBy(m => m.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static void SaveProject(EditorProject project, string projectDirectory) {
        project.Normalize();
        Directory.CreateDirectory(projectDirectory);
        File.WriteAllText(Path.Combine(projectDirectory, MetadataFileName), JsonSerializer.Serialize(project, JsonOptions));
        WriteGeneratedEverestYaml(project, projectDirectory);
    }

    private static void WriteGeneratedEverestYaml(EditorProject project, string projectDirectory) {
        string yaml =
            $"- Name: {YamlScalar(project.ModName)}\n" +
            "  Version: 1.0.0\n" +
            "  Dependencies:\n" +
            "    - Name: Everest\n" +
            "      Version: 1.6418.0\n";
        File.WriteAllText(Path.Combine(projectDirectory, "everest.yaml"), yaml, new UTF8Encoding(false));
    }

    private static string YamlScalar(string value) {
        value ??= string.Empty;
        return "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }

    private static void BuildAllMaps(EditorProject project, string projectDirectory) {
        foreach (EditorChapter chapter in project.Chapters)
            BuildChapter(project, chapter, projectDirectory);
    }

    private static void BuildChapter(EditorProject project, EditorChapter chapter, string projectDirectory) {
        project.Normalize();
        string mapDir = Path.Combine(projectDirectory, "Maps", project.ModName);
        Directory.CreateDirectory(mapDir);
        string output = Path.Combine(mapDir, chapter.Slug + ".bin");
        string package = project.ModName + "/" + chapter.Slug;

        MapElement map = BuildMapElement(chapter);
        CelesteMapBinary.Write(output, package, map);
    }

    private static MapElement BuildMapElement(EditorChapter chapter) {
        MapElement levels = new("levels");
        int roomX = 0;
        int entityId = 0;

        foreach (EditorRoom room in chapter.Rooms) {
            room.Normalize();
            MapElement level = new MapElement("level")
                .Attr("name", room.Name)
                .Attr("x", roomX)
                .Attr("y", 0)
                .Attr("width", room.WidthTiles * 8)
                .Attr("height", room.HeightTiles * 8)
                .Attr("c", 0)
                .Attr("musicLayer1", true)
                .Attr("musicLayer2", true)
                .Attr("musicLayer3", true)
                .Attr("musicLayer4", true)
                .Attr("musicProgress", "")
                .Attr("ambienceProgress", "")
                .Attr("delayAltMusicFade", false)
                .Attr("dark", false)
                .Attr("space", false)
                .Attr("underwater", false)
                .Attr("whisper", false)
                .Attr("music", "music_oldsite_awake")
                .Attr("altMusic", "")
                .Attr("disableDownTransition", false)
                .Attr("windPattern", "None")
                .Attr("cameraOffsetX", 0f)
                .Attr("cameraOffsetY", 0f);

            string solids = room.GetSolidsText();
            string emptyTiles = room.GetEmptyTilesText();
            string objTiles = room.GetObjectTilesText();

            level.Child(new MapElement("solids").Attr("innerText", solids));
            level.Child(new MapElement("bg").Attr("innerText", emptyTiles));
            level.Child(new MapElement("objtiles").Attr("innerText", objTiles));
            level.Child(new MapElement("fgtiles").Attr("tileset", "Scenery"));
            level.Child(new MapElement("bgtiles").Attr("tileset", "Scenery"));

            MapElement entities = new("entities");

            if (room.HasSpawn) {
                entities.Child(
                    new MapElement("player")
                    .Attr("id", entityId++)
                    .Attr("x", room.SpawnTileX * 8 + 4)
                    .Attr("y", room.SpawnTileY * 8 + 8));
            }

            foreach (EditorEntity entity in room.Entities) {
                MapElement element =
                    new MapElement(entity.Type)
                    .Attr("id", entityId++)
                    .Attr("x", entity.TileX * 8 + 4)
                    .Attr("y", entity.TileY * 8 + 8);

                if (string.Equals(
                    entity.Type,
                    "spikesUp",
                    StringComparison.OrdinalIgnoreCase)) {

                    element
                        .Attr("width", 8)
                        .Attr("type", "default");
                }

                entities.Child(element);
            }

            level.Child(entities);
            level.Child(new MapElement("triggers"));
            level.Child(new MapElement("fgdecals").Attr("tileset", "Scenery"));
            level.Child(new MapElement("bgdecals").Attr("tileset", "Scenery"));

            levels.Child(level);
            roomX += room.WidthTiles * 8;
        }

        MapElement style = new MapElement("Style")
            .Child(new MapElement("Foregrounds"))
            .Child(new MapElement("Backgrounds"));

        return new MapElement("Map")
            .Child(levels)
            .Child(style)
            .Child(new MapElement("Filler"));
    }

    private static string CleanDisplayName(string value, string fallback) {
        string cleaned = (value ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    private static string Slugify(string value) {
        StringBuilder b = new();
        bool underscore = false;
        foreach (char c in value ?? string.Empty) {
            if (char.IsLetterOrDigit(c)) {
                b.Append(c);
                underscore = false;
            } else if (!underscore && b.Length > 0) {
                b.Append('_');
                underscore = true;
            }
        }

        string result = b.ToString().Trim('_');
        if (string.IsNullOrWhiteSpace(result))
            result = "MapMod";
        if (char.IsDigit(result[0]))
            result = "Map_" + result;
        return result;
    }

    private static string MakeUniqueModSlug(string baseSlug) {
        string mods = GetModsDirectory();
        string slug = baseSlug;
        int n = 2;
        while (Directory.Exists(Path.Combine(mods, slug)))
            slug = baseSlug + "_" + n++;
        return slug;
    }

    private static string MakeUniqueChapterSlug(EditorProject project, string baseSlug) {
        string slug = baseSlug;
        int n = 2;
        while (project.Chapters.Any(c => string.Equals(c.Slug, slug, StringComparison.OrdinalIgnoreCase)))
            slug = baseSlug + "_" + n++;
        return slug;
    }

    private static string MakeUniqueRoomName(EditorChapter chapter, string baseName) {
        string name = baseName;
        int n = 2;
        while (chapter.Rooms.Any(r => string.Equals(r.Name, name, StringComparison.OrdinalIgnoreCase)))
            name = baseName + "_" + n++;
        return name;
    }

    private sealed record ProjectEntry(EditorProject Project, string Directory);
    private sealed record InstalledMapMod(string Name, int MapCount);

    public sealed class EditorProject {
        public string Name { get; set; } = "New Map Mod";
        public string ModName { get; set; } = "NewMapMod";
        public List<EditorChapter> Chapters { get; set; } = new();

        public void Normalize() {
            Name = CleanDisplayName(Name, "New Map Mod");
            ModName = Slugify(ModName);
            Chapters ??= new List<EditorChapter>();
            foreach (EditorChapter chapter in Chapters)
                chapter.Normalize();
        }
    }

    public sealed class EditorChapter {
        public string Name { get; set; } = "Chapter 1";
        public string Slug { get; set; } = "Chapter_1";
        public List<EditorRoom> Rooms { get; set; } = new();

        public void Normalize() {
            Name = CleanDisplayName(Name, "Chapter");
            Slug = Slugify(Slug);
            Rooms ??= new List<EditorRoom>();
            foreach (EditorRoom room in Rooms)
                room.Normalize();
        }
    }

    public sealed class EditorRoom {
        public string Name { get; set; } = "room_1";
        public int WidthTiles { get; set; } = 40;
        public int HeightTiles { get; set; } = 23;
        public List<string> SolidRows { get; set; } = new();
        public bool HasSpawn { get; set; } = true;
        public int SpawnTileX { get; set; } = 3;
        public int SpawnTileY { get; set; } = 20;
        public List<EditorEntity> Entities { get; set; } = new();

        public static EditorRoom CreateDefault(string name) {
            EditorRoom room = new() {
                Name = name
            };

            room.Normalize();

            char[] floor =
                room.SolidRows[
                    room.HeightTiles - 1]
                .ToCharArray();

            for (int x = 0; x < floor.Length; x++) {
                floor[x] = '1';
            }

            room.SolidRows[
                room.HeightTiles - 1] =
                new string(floor);

            room.SpawnTileX = 3;
            room.SpawnTileY =
                Math.Max(
                    1,
                    room.HeightTiles - 3);

            return room;
        }

        public void Normalize() {
            Name =
                Slugify(Name)
                .ToLowerInvariant();

            WidthTiles =
                Math.Clamp(
                    WidthTiles,
                    10,
                    160);

            HeightTiles =
                Math.Clamp(
                    HeightTiles,
                    8,
                    90);

            SolidRows ??=
                new List<string>();

            Entities ??=
                new List<EditorEntity>();

            while (SolidRows.Count < HeightTiles) {
                SolidRows.Add(
                    new string(
                        '0',
                        WidthTiles));
            }

            if (SolidRows.Count > HeightTiles) {
                SolidRows.RemoveRange(
                    HeightTiles,
                    SolidRows.Count - HeightTiles);
            }

            for (int y = 0;
                y < SolidRows.Count;
                y++) {

                string row =
                    SolidRows[y] ??
                    string.Empty;

                if (row.Length < WidthTiles) {
                    row +=
                        new string(
                            '0',
                            WidthTiles - row.Length);
                }

                if (row.Length > WidthTiles) {
                    row =
                        row.Substring(
                            0,
                            WidthTiles);
                }

                char[] chars =
                    row.ToCharArray();

                for (int x = 0;
                    x < chars.Length;
                    x++) {

                    chars[x] =
                        chars[x] == '0'
                            ? '0'
                            : '1';
                }

                SolidRows[y] =
                    new string(chars);
            }

            SpawnTileX =
                Math.Clamp(
                    SpawnTileX,
                    0,
                    WidthTiles - 1);

            SpawnTileY =
                Math.Clamp(
                    SpawnTileY,
                    0,
                    HeightTiles - 1);

            foreach (EditorEntity entity in
                Entities) {

                entity.Normalize(
                    WidthTiles,
                    HeightTiles);
            }
        }

        public bool IsSolid(
            int x,
            int y) {

            if (x < 0 ||
                y < 0 ||
                x >= WidthTiles ||
                y >= HeightTiles) {

                return false;
            }

            return SolidRows[y][x] != '0';
        }

        public void SetSolid(
            int x,
            int y,
            bool solid) {

            if (x < 0 ||
                y < 0 ||
                x >= WidthTiles ||
                y >= HeightTiles) {

                return;
            }

            char[] row =
                SolidRows[y]
                .ToCharArray();

            row[x] =
                solid
                    ? '1'
                    : '0';

            SolidRows[y] =
                new string(row);
        }

        public string GetSolidsText() {
            return string.Join(
                "\n",
                SolidRows);
        }

        public string GetEmptyTilesText() {
            return string.Join(
                "\n",
                Enumerable.Repeat(
                    new string(
                        '0',
                        WidthTiles),
                    HeightTiles));
        }

        public string GetObjectTilesText() {
            return string.Join(
                "\n",
                Enumerable.Repeat(
                    string.Join(
                        ",",
                        Enumerable.Repeat(
                            "-1",
                            WidthTiles)),
                    HeightTiles));
        }
    }

    public sealed class EditorEntity {
        public string Type { get; set; } =
            "strawberry";

        public int TileX { get; set; }
        public int TileY { get; set; }

        public EditorEntity Clone() {
            return new EditorEntity {
                Type = Type,
                TileX = TileX,
                TileY = TileY
            };
        }

        public void Normalize(
            int roomWidth,
            int roomHeight) {

            if (Type != "strawberry" &&
                Type != "spring" &&
                Type != "spikesUp") {

                Type = "strawberry";
            }

            TileX =
                Math.Clamp(
                    TileX,
                    0,
                    Math.Max(
                        0,
                        roomWidth - 1));

            TileY =
                Math.Clamp(
                    TileY,
                    0,
                    Math.Max(
                        0,
                        roomHeight - 1));
        }
    }

    private enum EditorTool {
        Select,
        Solid,
        Erase,
        Spawn,
        Strawberry,
        Spring,
        SpikesUp,
        Pan
    }

    private sealed class RoomEditorOverlay : Entity {
        private readonly OuiMainMenu owner;
        private readonly EditorProject project;
        private readonly string projectDirectory;
        private readonly EditorChapter chapter;

        private EditorRoom room;
        private EditorTool tool =
            EditorTool.Select;

        private int selectedEntity = -1;

        private readonly Stack<string> undo =
            new();

        private readonly Stack<string> redo =
            new();

        private bool desktopPainting;
        private bool desktopPaintValue;
        private bool desktopPanning;
        private Vector2 lastPanPointer;

        private float zoom = 1f;
        private Vector2 pan;

        private const float SidebarX = 20f;
        private const float SidebarY = 125f;
        private const float SidebarWidth = 265f;
        private const float SidebarHeight = 735f;

        private const float CanvasX = 315f;
        private const float CanvasY = 125f;
        private const float CanvasWidth = 1580f;
        private const float CanvasHeight = 735f;

        private readonly ToolbarButton[] toolbar;

        public RoomEditorOverlay(
            OuiMainMenu owner,
            EditorProject project,
            string projectDirectory,
            EditorChapter chapter,
            EditorRoom room)
            : base(Vector2.Zero) {

            this.owner = owner;
            this.project = project;
            this.projectDirectory =
                projectDirectory;
            this.chapter = chapter;
            this.room = room;

            this.room.Normalize();

            Tag =
                Tags.HUD |
                Tags.PauseUpdate;

            Depth = -2000000;

            toolbar = new[] {
                new ToolbarButton(
                    "SELECT",
                    315,
                    885,
                    175,
                    62,
                    () => SetTool(EditorTool.Select)),
                new ToolbarButton(
                    "SOLID",
                    500,
                    885,
                    155,
                    62,
                    () => SetTool(EditorTool.Solid)),
                new ToolbarButton(
                    "ERASE",
                    665,
                    885,
                    155,
                    62,
                    () => SetTool(EditorTool.Erase)),
                new ToolbarButton(
                    "SPAWN",
                    830,
                    885,
                    155,
                    62,
                    () => SetTool(EditorTool.Spawn)),
                new ToolbarButton(
                    "BERRY",
                    995,
                    885,
                    155,
                    62,
                    () => SetTool(EditorTool.Strawberry)),
                new ToolbarButton(
                    "SPRING",
                    1160,
                    885,
                    155,
                    62,
                    () => SetTool(EditorTool.Spring)),
                new ToolbarButton(
                    "SPIKES",
                    1325,
                    885,
                    155,
                    62,
                    () => SetTool(EditorTool.SpikesUp)),
                new ToolbarButton(
                    "PAN",
                    1490,
                    885,
                    155,
                    62,
                    () => SetTool(EditorTool.Pan)),
                new ToolbarButton(
                    "UNDO",
                    315,
                    960,
                    155,
                    62,
                    Undo),
                new ToolbarButton(
                    "REDO",
                    480,
                    960,
                    155,
                    62,
                    Redo),
                new ToolbarButton(
                    "ZOOM -",
                    645,
                    960,
                    155,
                    62,
                    () => ChangeZoom(-0.15f)),
                new ToolbarButton(
                    "ZOOM +",
                    810,
                    960,
                    155,
                    62,
                    () => ChangeZoom(0.15f)),
                new ToolbarButton(
                    "- W",
                    975,
                    960,
                    120,
                    62,
                    () => ResizeRoom(-1, 0)),
                new ToolbarButton(
                    "+ W",
                    1105,
                    960,
                    120,
                    62,
                    () => ResizeRoom(1, 0)),
                new ToolbarButton(
                    "- H",
                    1235,
                    960,
                    120,
                    62,
                    () => ResizeRoom(0, -1)),
                new ToolbarButton(
                    "+ H",
                    1365,
                    960,
                    120,
                    62,
                    () => ResizeRoom(0, 1)),
                new ToolbarButton(
                    "SAVE",
                    1495,
                    960,
                    175,
                    62,
                    Save),
                new ToolbarButton(
                    "BACK",
                    1680,
                    960,
                    195,
                    62,
                    Back)
            };
        }

        public override void Update() {
            base.Update();

            if (Input.MenuCancel.Pressed ||
                MInput.Keyboard.Pressed(
                    Keys.Escape)) {

                Back();
                return;
            }

            HandleKeyboardShortcuts();

            Vector2 pointer =
                new(
                    MInput.Mouse.X,
                    MInput.Mouse.Y);

            float wheel =
                MInput.Mouse.WheelDelta;

            if (Math.Abs(wheel) >= 120f &&
                IsInsideCanvas(pointer)) {

                ChangeZoom(
                    wheel > 0f
                        ? 0.12f
                        : -0.12f);
            }

            bool desktopPress =
                MInput.Mouse.PressedLeftButton;

            bool desktopHeld =
                MInput.Mouse.CheckLeftButton;

            bool desktopRelease =
                MInput.Mouse.ReleasedLeftButton;

            bool panModifier =
                MInput.Keyboard.Check(
                    Keys.Space);

            if (desktopPress) {
                if (TryToolbar(pointer)) {
                    return;
                }

                if (TrySidebar(pointer)) {
                    return;
                }

                if ((tool == EditorTool.Pan ||
                     panModifier) &&
                    IsInsideCanvas(pointer)) {

                    desktopPanning = true;
                    lastPanPointer = pointer;
                    return;
                }

                HandleCanvasPress(
                    pointer,
                    beginContinuousEdit: true);
            } else if (desktopHeld) {
                if (desktopPanning) {
                    Vector2 delta =
                        pointer -
                        lastPanPointer;

                    pan += delta;
                    lastPanPointer =
                        pointer;
                } else if (desktopPainting &&
                    TryGetCell(
                        pointer,
                        out int x,
                        out int y)) {

                    room.SetSolid(
                        x,
                        y,
                        desktopPaintValue);
                }
            }

            if (desktopRelease) {
                desktopPainting = false;
                desktopPanning = false;
            }

            if (OptionalMobileBridge.TouchAvailable) {
                float touchScroll =
                    OptionalMobileBridge
                        .ConsumeTouchScroll();

                if (Math.Abs(touchScroll) > 12f) {
                    // MobileBridge swipe scrolling pans the editor instead of
                    // changing the selected tool / room.
                    pan.Y +=
                        Math.Sign(touchScroll) *
                        70f;
                }

                if (OptionalMobileBridge
                    .ConsumeTouchTap()) {

                    Vector2 touch =
                        OptionalMobileBridge
                            .TouchPosition;

                    if (TryToolbar(touch) ||
                        TrySidebar(touch)) {

                        return;
                    }

                    HandleCanvasPress(
                        touch,
                        beginContinuousEdit: false);
                }
            }
        }

        public override void Render() {
            Draw.Rect(
                0f,
                0f,
                1920f,
                1080f,
                Color.Black * 0.94f);

            ActiveFont.DrawOutline(
                $"{project.Name}  /  {chapter.Name}",
                new Vector2(
                    960f,
                    40f),
                new Vector2(
                    0.5f,
                    0.5f),
                Vector2.One * 0.72f,
                Color.White,
                2f,
                Color.Black);

            ActiveFont.DrawOutline(
                $"{room.Name}    {room.WidthTiles * 8} x {room.HeightTiles * 8}px    ZOOM {zoom:0.00}x",
                new Vector2(
                    960f,
                    88f),
                new Vector2(
                    0.5f,
                    0.5f),
                Vector2.One * 0.42f,
                Color.LightGray,
                2f,
                Color.Black);

            RenderSidebar();
            RenderCanvas();

            foreach (ToolbarButton button in
                toolbar) {

                button.Render(tool);
            }

            ActiveFont.DrawOutline(
                "CTRL+S SAVE   CTRL+Z/Y UNDO/REDO   SPACE+DRAG PAN   WHEEL ZOOM   DELETE ENTITY",
                new Vector2(
                    960f,
                    1048f),
                new Vector2(
                    0.5f,
                    0.5f),
                Vector2.One * 0.33f,
                Color.Gray,
                2f,
                Color.Black);
        }

        private void HandleKeyboardShortcuts() {
            bool ctrl =
                MInput.Keyboard.Check(
                    Keys.LeftControl) ||
                MInput.Keyboard.Check(
                    Keys.RightControl);

            if (ctrl &&
                MInput.Keyboard.Pressed(
                    Keys.S)) {

                Save();
            }

            if (ctrl &&
                MInput.Keyboard.Pressed(
                    Keys.Z)) {

                Undo();
            }

            if (ctrl &&
                MInput.Keyboard.Pressed(
                    Keys.Y)) {

                Redo();
            }

            if (MInput.Keyboard.Pressed(
                Keys.Delete)) {

                DeleteSelectedEntity();
            }

            if (MInput.Keyboard.Pressed(Keys.D1)) {
                SetTool(EditorTool.Select);
            }

            if (MInput.Keyboard.Pressed(Keys.D2)) {
                SetTool(EditorTool.Solid);
            }

            if (MInput.Keyboard.Pressed(Keys.D3)) {
                SetTool(EditorTool.Erase);
            }

            if (MInput.Keyboard.Pressed(Keys.D4)) {
                SetTool(EditorTool.Spawn);
            }
        }

        private void SetTool(
            EditorTool newTool) {

            tool = newTool;

            if (tool != EditorTool.Select) {
                selectedEntity = -1;
            }
        }

        private void HandleCanvasPress(
            Vector2 pointer,
            bool beginContinuousEdit) {

            if (!TryGetCell(
                pointer,
                out int x,
                out int y)) {

                if (tool == EditorTool.Select) {
                    selectedEntity = -1;
                }

                return;
            }

            switch (tool) {
                case EditorTool.Solid:
                    PushUndo();
                    room.SetSolid(
                        x,
                        y,
                        true);

                    if (beginContinuousEdit) {
                        desktopPainting = true;
                        desktopPaintValue = true;
                    }
                    break;

                case EditorTool.Erase:
                    PushUndo();

                    int entityIndex =
                        FindEntityAt(
                            x,
                            y);

                    if (entityIndex >= 0) {
                        room.Entities.RemoveAt(
                            entityIndex);

                        selectedEntity = -1;
                    } else if (
                        room.HasSpawn &&
                        room.SpawnTileX == x &&
                        room.SpawnTileY == y) {

                        room.HasSpawn = false;
                    } else {
                        room.SetSolid(
                            x,
                            y,
                            false);

                        if (beginContinuousEdit) {
                            desktopPainting = true;
                            desktopPaintValue = false;
                        }
                    }
                    break;

                case EditorTool.Spawn:
                    PushUndo();
                    room.HasSpawn = true;
                    room.SpawnTileX = x;
                    room.SpawnTileY = y;
                    selectedEntity = -1;
                    break;

                case EditorTool.Strawberry:
                    PlaceEntity(
                        "strawberry",
                        x,
                        y);
                    break;

                case EditorTool.Spring:
                    PlaceEntity(
                        "spring",
                        x,
                        y);
                    break;

                case EditorTool.SpikesUp:
                    PlaceEntity(
                        "spikesUp",
                        x,
                        y);
                    break;

                case EditorTool.Select:
                    int hit =
                        FindEntityAt(
                            x,
                            y);

                    if (hit >= 0) {
                        selectedEntity = hit;
                    } else if (selectedEntity >= 0 &&
                        selectedEntity < room.Entities.Count) {

                        PushUndo();

                        room.Entities[
                            selectedEntity]
                            .TileX = x;

                        room.Entities[
                            selectedEntity]
                            .TileY = y;
                    } else {
                        selectedEntity = -1;
                    }
                    break;

                case EditorTool.Pan:
                    break;
            }
        }

        private void PlaceEntity(
            string type,
            int x,
            int y) {

            PushUndo();

            room.Entities.Add(
                new EditorEntity {
                    Type = type,
                    TileX = x,
                    TileY = y
                });

            selectedEntity =
                room.Entities.Count - 1;

            tool =
                EditorTool.Select;
        }

        private int FindEntityAt(
            int x,
            int y) {

            for (int i =
                    room.Entities.Count - 1;
                i >= 0;
                i--) {

                EditorEntity entity =
                    room.Entities[i];

                if (entity.TileX == x &&
                    entity.TileY == y) {

                    return i;
                }
            }

            return -1;
        }

        private void DeleteSelectedEntity() {
            if (selectedEntity < 0 ||
                selectedEntity >=
                    room.Entities.Count) {

                return;
            }

            PushUndo();

            room.Entities.RemoveAt(
                selectedEntity);

            selectedEntity = -1;
        }

        private void RenderSidebar() {
            Draw.Rect(
                SidebarX,
                SidebarY,
                SidebarWidth,
                SidebarHeight,
                new Color(
                    21,
                    24,
                    33));

            Draw.HollowRect(
                SidebarX,
                SidebarY,
                SidebarWidth,
                SidebarHeight,
                Color.White * 0.6f);

            ActiveFont.DrawOutline(
                "ROOMS",
                new Vector2(
                    SidebarX +
                        SidebarWidth * 0.5f,
                    SidebarY + 28f),
                new Vector2(
                    0.5f,
                    0.5f),
                Vector2.One * 0.45f,
                Color.White,
                2f,
                Color.Black);

            float y =
                SidebarY + 65f;

            const float rowHeight =
                52f;

            int maxRows =
                Math.Max(
                    1,
                    (int)(
                        (SidebarHeight - 150f) /
                        rowHeight));

            int currentIndex =
                Math.Max(
                    0,
                    chapter.Rooms.IndexOf(room));

            int first =
                Math.Clamp(
                    currentIndex -
                    maxRows / 2,
                    0,
                    Math.Max(
                        0,
                        chapter.Rooms.Count -
                        maxRows));

            for (int i = first;
                i < chapter.Rooms.Count &&
                i < first + maxRows;
                i++) {

                EditorRoom candidate =
                    chapter.Rooms[i];

                Rectangle row =
                    GetSidebarRoomRect(
                        i - first);

                bool active =
                    ReferenceEquals(
                        candidate,
                        room);

                Draw.Rect(
                    row.X,
                    row.Y,
                    row.Width,
                    row.Height,
                    active
                        ? Color.White * 0.22f
                        : Color.White * 0.06f);

                ActiveFont.DrawOutline(
                    candidate.Name,
                    new Vector2(
                        row.X + 12f,
                        row.Center.Y),
                    new Vector2(
                        0f,
                        0.5f),
                    Vector2.One * 0.36f,
                    Color.White,
                    2f,
                    Color.Black);
            }

            Rectangle delete =
                GetDeleteRoomRect();

            Draw.Rect(
                delete.X,
                delete.Y,
                delete.Width,
                delete.Height,
                Color.Red * 0.10f);

            Draw.HollowRect(
                delete.X,
                delete.Y,
                delete.Width,
                delete.Height,
                Color.White * 0.40f);

            ActiveFont.DrawOutline(
                "- DELETE ROOM",
                new Vector2(
                    delete.Center.X,
                    delete.Center.Y),
                new Vector2(
                    0.5f,
                    0.5f),
                Vector2.One * 0.34f,
                chapter.Rooms.Count > 1
                    ? Color.White
                    : Color.Gray,
                2f,
                Color.Black);

            Rectangle add =
                GetAddRoomRect();

            Draw.Rect(
                add.X,
                add.Y,
                add.Width,
                add.Height,
                Color.White * 0.10f);

            Draw.HollowRect(
                add.X,
                add.Y,
                add.Width,
                add.Height,
                Color.White * 0.55f);

            ActiveFont.DrawOutline(
                "+ NEW ROOM",
                new Vector2(
                    add.Center.X,
                    add.Center.Y),
                new Vector2(
                    0.5f,
                    0.5f),
                Vector2.One * 0.38f,
                Color.White,
                2f,
                Color.Black);
        }

        private bool TrySidebar(
            Vector2 pointer) {

            if (pointer.X < SidebarX ||
                pointer.X >
                    SidebarX +
                    SidebarWidth ||
                pointer.Y < SidebarY ||
                pointer.Y >
                    SidebarY +
                    SidebarHeight) {

                return false;
            }

            Rectangle delete =
                GetDeleteRoomRect();

            if (delete.Contains(
                (int)pointer.X,
                (int)pointer.Y)) {

                DeleteCurrentRoom();
                return true;
            }

            Rectangle add =
                GetAddRoomRect();

            if (add.Contains(
                (int)pointer.X,
                (int)pointer.Y)) {

                AddRoom();
                return true;
            }

            const float rowHeight =
                52f;

            int maxRows =
                Math.Max(
                    1,
                    (int)(
                        (SidebarHeight - 150f) /
                        rowHeight));

            int currentIndex =
                Math.Max(
                    0,
                    chapter.Rooms.IndexOf(room));

            int first =
                Math.Clamp(
                    currentIndex -
                    maxRows / 2,
                    0,
                    Math.Max(
                        0,
                        chapter.Rooms.Count -
                        maxRows));

            for (int local = 0;
                local < maxRows;
                local++) {

                int index =
                    first + local;

                if (index >=
                    chapter.Rooms.Count) {

                    break;
                }

                Rectangle row =
                    GetSidebarRoomRect(
                        local);

                if (row.Contains(
                    (int)pointer.X,
                    (int)pointer.Y)) {

                    SwitchRoom(
                        chapter.Rooms[index]);

                    return true;
                }
            }

            return true;
        }

        private static Rectangle GetSidebarRoomRect(
            int localIndex) {

            return new Rectangle(
                (int)SidebarX + 10,
                (int)SidebarY +
                    58 +
                    localIndex * 52,
                (int)SidebarWidth - 20,
                46);
        }

        private static Rectangle GetDeleteRoomRect() {
            return new Rectangle(
                (int)SidebarX + 10,
                (int)(
                    SidebarY +
                    SidebarHeight -
                    124f),
                (int)SidebarWidth - 20,
                50);
        }

        private static Rectangle GetAddRoomRect() {
            return new Rectangle(
                (int)SidebarX + 10,
                (int)(
                    SidebarY +
                    SidebarHeight -
                    66f),
                (int)SidebarWidth - 20,
                52);
        }

        private void DeleteCurrentRoom() {
            if (chapter.Rooms.Count <= 1) {
                Audio.Play(
                    "event:/ui/main/button_invalid");
                return;
            }

            int index =
                chapter.Rooms.IndexOf(room);

            if (index < 0) {
                return;
            }

            chapter.Rooms.RemoveAt(index);

            room =
                chapter.Rooms[
                    Math.Clamp(
                        index,
                        0,
                        chapter.Rooms.Count - 1)];

            room.Normalize();
            selectedEntity = -1;
            ResetViewport();
            ClearHistory();
            SaveCurrentProjectOnly();

            Audio.Play(
                "event:/ui/main/button_select");
        }

        private void AddRoom() {
            SaveCurrentProjectOnly();

            string name =
                MakeUniqueRoomName(
                    chapter,
                    $"room_{chapter.Rooms.Count + 1}");

            EditorRoom created =
                EditorRoom.CreateDefault(
                    name);

            chapter.Rooms.Add(
                created);

            room = created;
            selectedEntity = -1;
            ResetViewport();
            ClearHistory();
            SaveCurrentProjectOnly();

            Audio.Play(
                "event:/ui/main/button_select");
        }

        private void SwitchRoom(
            EditorRoom target) {

            if (target == null ||
                ReferenceEquals(
                    target,
                    room)) {

                return;
            }

            SaveCurrentProjectOnly();

            room = target;
            room.Normalize();
            selectedEntity = -1;
            ResetViewport();
            ClearHistory();

            Audio.Play(
                "event:/ui/main/rollover_down");
        }

        private void RenderCanvas() {
            Draw.Rect(
                CanvasX,
                CanvasY,
                CanvasWidth,
                CanvasHeight,
                new Color(
                    15,
                    17,
                    24));

            Draw.HollowRect(
                CanvasX,
                CanvasY,
                CanvasWidth,
                CanvasHeight,
                Color.White * 0.45f);

            GetCanvasTransform(
                out float cell,
                out float originX,
                out float originY);

            float roomWidth =
                room.WidthTiles * cell;

            float roomHeight =
                room.HeightTiles * cell;

            Draw.Rect(
                originX - 4f,
                originY - 4f,
                roomWidth + 8f,
                roomHeight + 8f,
                Color.White * 0.8f);

            Draw.Rect(
                originX,
                originY,
                roomWidth,
                roomHeight,
                new Color(
                    34,
                    38,
                    50));

            for (int y = 0;
                y < room.HeightTiles;
                y++) {

                for (int x = 0;
                    x < room.WidthTiles;
                    x++) {

                    float px =
                        originX +
                        x * cell;

                    float py =
                        originY +
                        y * cell;

                    if (room.IsSolid(
                        x,
                        y)) {

                        Draw.Rect(
                            px + 1f,
                            py + 1f,
                            Math.Max(
                                1f,
                                cell - 2f),
                            Math.Max(
                                1f,
                                cell - 2f),
                            new Color(
                                120,
                                104,
                                92));
                    }

                    if (cell >= 7f) {
                        Draw.Line(
                            new Vector2(
                                px,
                                py),
                            new Vector2(
                                px + cell,
                                py),
                            Color.White * 0.07f);

                        Draw.Line(
                            new Vector2(
                                px,
                                py),
                            new Vector2(
                                px,
                                py + cell),
                            Color.White * 0.07f);
                    }
                }
            }

            if (room.HasSpawn) {
                DrawEntityMarker(
                    room.SpawnTileX,
                    room.SpawnTileY,
                    cell,
                    originX,
                    originY,
                    Color.Cyan,
                    "P",
                    false);
            }

            for (int i = 0;
                i < room.Entities.Count;
                i++) {

                EditorEntity entity =
                    room.Entities[i];

                Color color =
                    entity.Type switch {
                        "strawberry" =>
                            Color.Red,
                        "spring" =>
                            Color.LimeGreen,
                        "spikesUp" =>
                            Color.LightGray,
                        _ =>
                            Color.Magenta
                    };

                string marker =
                    entity.Type switch {
                        "strawberry" =>
                            "B",
                        "spring" =>
                            "S",
                        "spikesUp" =>
                            "^",
                        _ =>
                            "?"
                    };

                DrawEntityMarker(
                    entity.TileX,
                    entity.TileY,
                    cell,
                    originX,
                    originY,
                    color,
                    marker,
                    i == selectedEntity);
            }
        }

        private static void DrawEntityMarker(
            int tileX,
            int tileY,
            float cell,
            float originX,
            float originY,
            Color color,
            string marker,
            bool selected) {

            float x =
                originX +
                tileX * cell +
                cell * 0.5f;

            float y =
                originY +
                tileY * cell +
                cell * 0.5f;

            float radius =
                Math.Max(
                    5f,
                    Math.Min(
                        15f,
                        cell * 0.34f));

            Draw.Circle(
                new Vector2(x, y),
                radius,
                color,
                16);

            if (selected) {
                Draw.Circle(
                    new Vector2(x, y),
                    radius + 5f,
                    Color.Yellow,
                    16);
            }

            if (cell >= 15f) {
                ActiveFont.DrawOutline(
                    marker,
                    new Vector2(x, y),
                    new Vector2(
                        0.5f,
                        0.5f),
                    Vector2.One *
                        Math.Min(
                            0.32f,
                            cell / 90f),
                    Color.Black,
                    1f,
                    Color.White);
            }
        }

        private bool IsInsideCanvas(
            Vector2 pointer) {

            return pointer.X >= CanvasX &&
                pointer.X <=
                    CanvasX + CanvasWidth &&
                pointer.Y >= CanvasY &&
                pointer.Y <=
                    CanvasY + CanvasHeight;
        }

        private void GetCanvasTransform(
            out float cell,
            out float originX,
            out float originY) {

            float fit =
                Math.Min(
                    CanvasWidth /
                        room.WidthTiles,
                    CanvasHeight /
                        room.HeightTiles);

            cell =
                Math.Max(
                    2f,
                    fit * zoom);

            float width =
                room.WidthTiles *
                cell;

            float height =
                room.HeightTiles *
                cell;

            originX =
                CanvasX +
                (CanvasWidth - width) *
                    0.5f +
                pan.X;

            originY =
                CanvasY +
                (CanvasHeight - height) *
                    0.5f +
                pan.Y;
        }

        private bool TryGetCell(
            Vector2 pointer,
            out int x,
            out int y) {

            GetCanvasTransform(
                out float cell,
                out float originX,
                out float originY);

            float width =
                room.WidthTiles *
                cell;

            float height =
                room.HeightTiles *
                cell;

            if (pointer.X < originX ||
                pointer.Y < originY ||
                pointer.X >=
                    originX + width ||
                pointer.Y >=
                    originY + height) {

                x = -1;
                y = -1;
                return false;
            }

            x =
                Math.Clamp(
                    (int)(
                        (pointer.X -
                         originX) /
                        cell),
                    0,
                    room.WidthTiles - 1);

            y =
                Math.Clamp(
                    (int)(
                        (pointer.Y -
                         originY) /
                        cell),
                    0,
                    room.HeightTiles - 1);

            return true;
        }

        private bool TryToolbar(
            Vector2 pointer) {

            foreach (ToolbarButton button in
                toolbar) {

                if (button.Contains(pointer)) {
                    Audio.Play(
                        "event:/ui/main/button_select");

                    button.Action();
                    return true;
                }
            }

            return false;
        }

        private void ResizeRoom(
            int deltaWidth,
            int deltaHeight) {

            PushUndo();

            room.WidthTiles =
                Math.Clamp(
                    room.WidthTiles +
                        deltaWidth,
                    10,
                    160);

            room.HeightTiles =
                Math.Clamp(
                    room.HeightTiles +
                        deltaHeight,
                    8,
                    90);

            room.Normalize();
        }

        private void ChangeZoom(
            float delta) {

            zoom =
                Math.Clamp(
                    zoom + delta,
                    0.35f,
                    4f);
        }

        private void ResetViewport() {
            zoom = 1f;
            pan = Vector2.Zero;
        }

        private void PushUndo() {
            undo.Push(
                SerializeRoom(room));

            while (undo.Count > 80) {
                string[] items =
                    undo.ToArray();

                undo.Clear();

                for (int i =
                        items.Length - 2;
                    i >= 0;
                    i--) {

                    undo.Push(
                        items[i]);
                }
            }

            redo.Clear();
        }

        private void Undo() {
            if (undo.Count == 0) {
                return;
            }

            redo.Push(
                SerializeRoom(room));

            RestoreRoom(
                undo.Pop());
        }

        private void Redo() {
            if (redo.Count == 0) {
                return;
            }

            undo.Push(
                SerializeRoom(room));

            RestoreRoom(
                redo.Pop());
        }

        private void ClearHistory() {
            undo.Clear();
            redo.Clear();
        }

        private static string SerializeRoom(
            EditorRoom source) {

            return JsonSerializer.Serialize(
                source,
                JsonOptions);
        }

        private void RestoreRoom(
            string json) {

            EditorRoom restored =
                JsonSerializer.Deserialize<
                    EditorRoom>(
                    json,
                    JsonOptions);

            if (restored == null) {
                return;
            }

            restored.Normalize();

            int index =
                chapter.Rooms.IndexOf(
                    room);

            if (index >= 0) {
                chapter.Rooms[index] =
                    restored;
            }

            room = restored;
            selectedEntity = -1;
        }

        private void SaveCurrentProjectOnly() {
            room.Normalize();

            SaveProject(
                project,
                projectDirectory);
        }

        private void Save() {
            room.Normalize();

            SaveProject(
                project,
                projectDirectory);

            BuildChapter(
                project,
                chapter,
                projectDirectory);

            Audio.Play(
                "event:/ui/main/button_select");
        }

        private void Back() {
            SaveCurrentProjectOnly();

            RemoveSelf();

            Scene scene =
                Engine.Scene;

            if (scene != null) {
                scene.OnEndOfFrame += () =>
                    ShowChapter(
                        owner,
                        project,
                        projectDirectory,
                        chapter);
            }
        }

        private sealed class ToolbarButton {
            public readonly string Label;
            public readonly Rectangle Rect;
            public readonly Action Action;

            public ToolbarButton(
                string label,
                int x,
                int y,
                int width,
                int height,
                Action action) {

                Label = label;

                Rect =
                    new Rectangle(
                        x,
                        y,
                        width,
                        height);

                Action = action;
            }

            public bool Contains(
                Vector2 point) {

                return Rect.Contains(
                    (int)point.X,
                    (int)point.Y);
            }

            public void Render(
                EditorTool currentTool) {

                bool selected =
                    (Label == "SELECT" &&
                     currentTool ==
                        EditorTool.Select) ||
                    (Label == "SOLID" &&
                     currentTool ==
                        EditorTool.Solid) ||
                    (Label == "ERASE" &&
                     currentTool ==
                        EditorTool.Erase) ||
                    (Label == "SPAWN" &&
                     currentTool ==
                        EditorTool.Spawn) ||
                    (Label == "BERRY" &&
                     currentTool ==
                        EditorTool.Strawberry) ||
                    (Label == "SPRING" &&
                     currentTool ==
                        EditorTool.Spring) ||
                    (Label == "SPIKES" &&
                     currentTool ==
                        EditorTool.SpikesUp) ||
                    (Label == "PAN" &&
                     currentTool ==
                        EditorTool.Pan);

                Color fill =
                    selected
                        ? Color.White * 0.28f
                        : Color.White * 0.12f;

                Draw.Rect(
                    Rect.X,
                    Rect.Y,
                    Rect.Width,
                    Rect.Height,
                    fill);

                Draw.HollowRect(
                    Rect.X,
                    Rect.Y,
                    Rect.Width,
                    Rect.Height,
                    Color.White * 0.8f);

                ActiveFont.DrawOutline(
                    Label,
                    new Vector2(
                        Rect.Center.X,
                        Rect.Center.Y),
                    new Vector2(
                        0.5f,
                        0.5f),
                    Vector2.One * 0.38f,
                    Color.White,
                    2f,
                    Color.Black);
            }
        }
    }

    private sealed class ModalBackdrop : Entity {
        private readonly TextMenu menu;

        public ModalBackdrop(TextMenu menu) {
            this.menu = menu;
            Tag = Tags.HUD | Tags.PauseUpdate;
            Depth = menu.Depth + 1;
        }

        public override void Render() {
            Draw.Rect(0f, 0f, 1920f, 1080f, Color.Black * 0.78f);

            if (menu?.Scene == null)
                return;

            menu.RecalculateSize();

            float width = Math.Min(1500f, menu.Width + 100f);
            float height = Math.Min(980f, menu.Height + 80f);
            Vector2 position = menu.Position;

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

    private sealed class OptionalPointerController : Entity {
        private readonly TextMenu menu;
        private Vector2 dragStart;
        private bool potentialTap;
        private float scrollAccumulator;

        public OptionalPointerController(TextMenu menu) {
            this.menu = menu;
            Tag = Tags.HUD | Tags.PauseUpdate;
        }

        public override void Update() {
            base.Update();

            if (menu?.Scene == null || !menu.Focused || menu.Items == null || menu.Items.Count == 0)
                return;

            menu.RecalculateSize();

            Vector2 pointer = MInput.Mouse.Position;
            Vector2 origin = menu.Position - menu.Justify * new Vector2(menu.Width, menu.Height);

            if (MInput.Mouse.PressedLeftButton) {
                dragStart = pointer;
                potentialTap = true;
            }

            if (MInput.Mouse.CheckLeftButton &&
                potentialTap &&
                Vector2.Distance(dragStart, pointer) > 20f) {

                potentialTap = false;
            }

            float scroll = MInput.Mouse.WheelDelta;
            scrollAccumulator += scroll;
            if (Math.Abs(scrollAccumulator) >= 120f) {
                menu.MoveSelection(scrollAccumulator > 0f ? -1 : 1, true);
                scrollAccumulator = 0f;
            }

            if (!MInput.Mouse.ReleasedLeftButton || !potentialTap) {
                if (MInput.Mouse.ReleasedLeftButton)
                    potentialTap = false;
                return;
            }

            potentialTap = false;

            float itemY = origin.Y;
            for (int i = 0; i < menu.Items.Count; i++) {
                TextMenu.Item item = menu.Items[i];
                if (item == null || !item.Visible)
                    continue;

                float height = item.Height();
                float centerY = itemY + height * 0.5f;
                float hitHeight = Math.Max(height, 80f);

                if (item.Hoverable &&
                    pointer.X >= origin.X - 100f &&
                    pointer.X <= origin.X + menu.Width + 100f &&
                    pointer.Y >= centerY - hitHeight * 0.5f &&
                    pointer.Y <= centerY + hitHeight * 0.5f) {

                    if (menu.Selection != i) {
                        menu.Current?.OnLeave?.Invoke();
                        menu.Selection = i;
                        item.OnEnter?.Invoke();
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

                itemY += height + menu.ItemSpacing;
            }
        }
    }

    private sealed class WrappedTextItem : TextMenu.Item {
        private readonly FancyText.Text text;

        public WrappedTextItem(string value, float width) {
            Selectable = false;
            text = FancyText.Parse(value ?? "", (int)width, 100);
        }

        public override float Height() {
            return text.Lines * ActiveFont.LineHeight * 0.55f + 30f;
        }

        public override float LeftWidth() {
            return 920f;
        }

        public override void Render(Vector2 position, bool highlighted) {
            text.Draw(
                position + new Vector2(Container.Width * 0.5f, 0f),
                new Vector2(0.5f, 0.5f),
                Vector2.One * 0.55f,
                Container.Alpha);
        }
    }

    private static class OptionalMobileBridge {
        private static bool resolved;
        private static PropertyInfo touchAvailable;
        private static MethodInfo consumeTap;
        private static MethodInfo touchX;
        private static MethodInfo touchY;
        private static MethodInfo consumeScroll;

        public static bool TouchAvailable {
            get {
                Resolve();
                try { return touchAvailable != null && (bool)touchAvailable.GetValue(null); }
                catch { return false; }
            }
        }

        public static Vector2 TouchPosition {
            get {
                Resolve();
                try {
                    float x = Convert.ToSingle(touchX?.Invoke(null, null) ?? -1f);
                    float y = Convert.ToSingle(touchY?.Invoke(null, null) ?? -1f);
                    return new Vector2(x, y);
                } catch {
                    return new Vector2(-1, -1);
                }
            }
        }

        public static bool ConsumeTouchTap() {
            Resolve();
            try { return consumeTap != null && (bool)consumeTap.Invoke(null, null); }
            catch { return false; }
        }

        public static float ConsumeTouchScroll() {
            Resolve();
            try { return Convert.ToSingle(consumeScroll?.Invoke(null, null) ?? 0f); }
            catch { return 0f; }
        }

        private static void Resolve() {
            if (resolved)
                return;
            resolved = true;

            Type api = AppDomain.CurrentDomain.GetAssemblies()
                .Select(a => a.GetType("Celeste.Mod.MobileBridge.MobileBridgeApi", false))
                .FirstOrDefault(t => t != null);
            if (api == null)
                return;

            touchAvailable = api.GetProperty("TouchAvailable", BindingFlags.Public | BindingFlags.Static);
            consumeTap = api.GetMethod("ConsumeTouchTap", BindingFlags.Public | BindingFlags.Static);
            touchX = api.GetMethod("TouchX", BindingFlags.Public | BindingFlags.Static);
            touchY = api.GetMethod("TouchY", BindingFlags.Public | BindingFlags.Static);
            consumeScroll = api.GetMethod("ConsumeTouchScroll", BindingFlags.Public | BindingFlags.Static);
        }
    }

    private sealed class MapElement {
        public string Name { get; }
        public Dictionary<string, object> Attributes { get; } = new(StringComparer.Ordinal);
        public List<MapElement> Children { get; } = new();

        public MapElement(string name) {
            Name = name;
        }

        public MapElement Attr(string key, object value) {
            Attributes[key] = value;
            return this;
        }

        public MapElement Child(MapElement child) {
            Children.Add(child);
            return this;
        }
    }

    private static class CelesteMapBinary {
        private const string Header = "CELESTE MAP";

        public static void Write(string path, string package, MapElement root) {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            List<string> lookup = new();
            Dictionary<string, ushort> indices = new(StringComparer.Ordinal);
            CollectLookup(root, lookup, indices);

            if (lookup.Count > short.MaxValue)
                throw new InvalidDataException("Map lookup table is too large.");

            using FileStream stream = new(path, FileMode.Create, FileAccess.Write, FileShare.Read);
            using BinaryWriter writer = new(stream, Encoding.UTF8, leaveOpen: false);
            WriteString(writer, Header);
            WriteString(writer, package);
            writer.Write((short)lookup.Count);
            foreach (string value in lookup)
                WriteString(writer, value);
            WriteElement(writer, root, indices);
        }

        private static void CollectLookup(MapElement element, List<string> lookup, Dictionary<string, ushort> indices) {
            AddLookup(element.Name, lookup, indices);
            foreach ((string key, object value) in element.Attributes) {
                AddLookup(key, lookup, indices);
                if (value is string str && key != "innerText")
                    AddLookup(str, lookup, indices);
            }
            foreach (MapElement child in element.Children)
                CollectLookup(child, lookup, indices);
        }

        private static void AddLookup(string value, List<string> lookup, Dictionary<string, ushort> indices) {
            value ??= string.Empty;
            if (indices.ContainsKey(value))
                return;
            if (lookup.Count >= ushort.MaxValue)
                throw new InvalidDataException("Map lookup table overflow.");
            indices[value] = (ushort)lookup.Count;
            lookup.Add(value);
        }

        private static void WriteElement(BinaryWriter writer, MapElement element, Dictionary<string, ushort> lookup) {
            writer.Write(lookup[element.Name]);
            if (element.Attributes.Count > byte.MaxValue)
                throw new InvalidDataException($"Element {element.Name} has too many attributes.");
            writer.Write((byte)element.Attributes.Count);

            foreach ((string key, object value) in element.Attributes) {
                writer.Write(lookup[key]);
                WriteValue(writer, key, value, lookup);
            }

            if (element.Children.Count > ushort.MaxValue)
                throw new InvalidDataException($"Element {element.Name} has too many children.");
            writer.Write((ushort)element.Children.Count);
            foreach (MapElement child in element.Children)
                WriteElement(writer, child, lookup);
        }

        private static void WriteValue(BinaryWriter writer, string key, object value, Dictionary<string, ushort> lookup) {
            switch (value) {
                case bool b:
                    writer.Write((byte)0);
                    writer.Write(b);
                    return;
                case byte b8:
                    writer.Write((byte)1);
                    writer.Write(b8);
                    return;
                case sbyte sb:
                    WriteInteger(writer, sb);
                    return;
                case short s16:
                    WriteInteger(writer, s16);
                    return;
                case ushort u16:
                    WriteInteger(writer, u16);
                    return;
                case int i32:
                    WriteInteger(writer, i32);
                    return;
                case long i64 when i64 >= int.MinValue && i64 <= int.MaxValue:
                    WriteInteger(writer, (int)i64);
                    return;
                case float f:
                    writer.Write((byte)4);
                    writer.Write(f);
                    return;
                case double d:
                    writer.Write((byte)4);
                    writer.Write((float)d);
                    return;
                case string str:
                    if (key != "innerText" && lookup.TryGetValue(str, out ushort index)) {
                        writer.Write((byte)5);
                        writer.Write(index);
                        return;
                    }
                    WriteStringValue(writer, str ?? string.Empty);
                    return;
                default:
                    throw new InvalidDataException($"Unsupported map attribute type {value?.GetType().FullName ?? "null"} for {key}.");
            }
        }

        private static void WriteInteger(BinaryWriter writer, int value) {
            if (value >= byte.MinValue && value <= byte.MaxValue) {
                writer.Write((byte)1);
                writer.Write((byte)value);
            } else if (value >= short.MinValue && value <= short.MaxValue) {
                writer.Write((byte)2);
                writer.Write((short)value);
            } else {
                writer.Write((byte)3);
                writer.Write(value);
            }
        }

        private static void WriteStringValue(BinaryWriter writer, string value) {
            byte[] utf8 = Encoding.UTF8.GetBytes(value);
            byte[] rle = TryRle(value);
            if (rle != null && rle.Length < utf8.Length && rle.Length <= short.MaxValue) {
                writer.Write((byte)7);
                writer.Write((short)rle.Length);
                writer.Write(rle);
            } else {
                writer.Write((byte)6);
                WriteString(writer, value);
            }
        }

        private static byte[] TryRle(string value) {
            if (string.IsNullOrEmpty(value))
                return null;
            foreach (char c in value)
                if (c > byte.MaxValue)
                    return null;

            List<byte> result = new();
            char current = value[0];
            int count = 1;
            for (int i = 1; i < value.Length; i++) {
                char c = value[i];
                if (c != current || count == 255) {
                    result.Add((byte)count);
                    result.Add((byte)current);
                    current = c;
                    count = 1;
                } else {
                    count++;
                }
            }
            result.Add((byte)count);
            result.Add((byte)current);
            return result.ToArray();
        }

        private static void WriteString(BinaryWriter writer, string value) {
            byte[] bytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
            WriteVarInt(writer, bytes.Length);
            writer.Write(bytes);
        }

        private static void WriteVarInt(BinaryWriter writer, int value) {
            uint remaining = (uint)value;
            while (remaining > 127) {
                writer.Write((byte)((remaining & 0x7F) | 0x80));
                remaining >>= 7;
            }
            writer.Write((byte)remaining);
        }
    }
}
