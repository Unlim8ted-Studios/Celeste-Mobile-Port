using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.UI;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;
using MonoMod.RuntimeDetour;

namespace Celeste.Mod.MobileMultiplayer;

public sealed class MobileMultiplayerSettings : EverestModuleSettings {
    [SettingIgnore]
    public string[] CustomServers { get; set; } =
        Array.Empty<string>();

    [SettingIgnore]
    public int HostPort { get; set; } =
        17230;
}

public sealed class MobileMultiplayerModule : EverestModule {
    public static MobileMultiplayerModule Instance { get; private set; }

    public static MobileMultiplayerSettings Settings =>
        (MobileMultiplayerSettings)Instance._Settings;

    public override Type SettingsType =>
        typeof(MobileMultiplayerSettings);

    private delegate void CreateModMenuSectionDelegate(
        EverestModule self,
        TextMenu menu,
        bool inGame,
        EventInstance snapshot);

    private delegate void CreateModMenuSectionHookDelegate(
        CreateModMenuSectionDelegate orig,
        EverestModule self,
        TextMenu menu,
        bool inGame,
        EventInstance snapshot);

    private static Hook celesteNetSettingsHook;

    private static TextMenu currentMenu;
    private static TextMenu optionsParent;
    private static OuiMainMenu mainMenuParent;
    private static bool rootInGame;
    private static EventInstance rootSnapshot;

    private static readonly object DiscoveryLock = new();
    private static List<string> discoveredLanServers = new();
    private static bool discoveryRunning;
    private static bool discoveryRefreshPending;

    private static bool joinWhenHostReady;

    private enum ScreenKind {
        None,
        Root,
        Host,
        Join,
        CustomServers
    }

    private enum ServerSource {
        Official,
        CelesteNet,
        Custom,
        Lan,
        Wrapper
    }

    private sealed record ServerEntry(
        string Address,
        ServerSource Source);

    private static ScreenKind currentScreen =
        ScreenKind.None;

    public MobileMultiplayerModule() {
        Instance = this;
    }

    public override void Load() {
        NormalizeSettings();
        InstallCelesteNetSettingsHook();

        Everest.Events.MainMenu.OnCreateButtons +=
            OnCreateMainMenuButtons;

        On.Monocle.Engine.Update +=
            OnEngineUpdate;
    }

    public override void Unload() {
        Everest.Events.MainMenu.OnCreateButtons -=
            OnCreateMainMenuButtons;

        On.Monocle.Engine.Update -=
            OnEngineUpdate;

        CloseCurrentMenu(
            restoreParent: false);

        celesteNetSettingsHook?.Dispose();
        celesteNetSettingsHook = null;
    }

    /// <summary>
    /// When MobileTweaks exists, MobileMultiplayer is represented in normal
    /// Options -> Multiplayer. Without MobileTweaks, preserve a standalone
    /// entry in Mod Options.
    /// </summary>
    public override void CreateModMenuSection(
        TextMenu menu,
        bool inGame,
        EventInstance snapshot) {

        if (IsMobileTweaksLoaded()) {
            return;
        }

        base.CreateModMenuSection(
            menu,
            inGame,
            snapshot);

        menu.Add(
            new TextMenu.Button(
                "OPEN MULTIPLAYER")
            .Pressed(() =>
                OpenOptionsMenu(
                    menu,
                    inGame,
                    snapshot)));
    }

    /// <summary>
    /// Entry point used by MobileTweaks' normal Options menu.
    /// </summary>
    public static void OpenOptionsMenu(
        TextMenu parent,
        bool inGame,
        EventInstance snapshot) {

        optionsParent = parent;
        mainMenuParent = null;
        rootInGame = inGame;
        rootSnapshot = snapshot;

        if (parent != null) {
            parent.Focused = false;
        }

        ShowRoot();
    }

    private static void OpenFromMainMenu(
        OuiMainMenu parent) {

        optionsParent = null;
        mainMenuParent = parent;
        rootInGame = false;
        rootSnapshot = default;

        if (parent != null) {
            parent.Focused = false;
        }

        ShowRoot();
    }

    private static void OnCreateMainMenuButtons(
        OuiMainMenu menu,
        List<MenuButton> buttons) {

        Vector2 position =
            Vector2.Zero;

        int climbIndex =
            buttons.FindIndex(button =>
                button is MainMenuClimb);

        int insertIndex =
            climbIndex >= 0
                ? climbIndex + 1
                : 0;

        buttons.Insert(
            Math.Clamp(
                insertIndex,
                0,
                buttons.Count),
            new MainMenuSmallButton(
                "MOBILEMULTIPLAYER_MAINMENU",
                "menu/options",
                menu,
                position,
                position,
                () =>
                    OpenFromMainMenu(menu)));
    }

    /// <summary>
    /// Everest builds Mod Options by asking each loaded EverestModule to add
    /// its own section. Detour that single common method and suppress only the
    /// CelesteNet.Client section. Other mods pass directly through.
    /// </summary>
    private static void InstallCelesteNetSettingsHook() {
        try {
            MethodInfo target =
                typeof(EverestModule).GetMethod(
                    "CreateModMenuSection",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic,
                    binder: null,
                    types: new[] {
                        typeof(TextMenu),
                        typeof(bool),
                        typeof(EventInstance)
                    },
                    modifiers: null);

            if (target == null) {
                Logger.Log(
                    LogLevel.Warn,
                    "MobileMultiplayer",
                    "Could not locate EverestModule.CreateModMenuSection; CelesteNet settings cannot be relocated.");
                return;
            }

            celesteNetSettingsHook =
                new Hook(
                    target,
                    (CreateModMenuSectionHookDelegate)
                        DetourCreateModMenuSection);
        } catch (Exception e) {
            Logger.Log(
                LogLevel.Error,
                "MobileMultiplayer",
                $"Could not install CelesteNet settings relocation hook: {e}");
        }
    }

    private static void DetourCreateModMenuSection(
        CreateModMenuSectionDelegate orig,
        EverestModule self,
        TextMenu menu,
        bool inGame,
        EventInstance snapshot) {

        if (string.Equals(
            self?.Metadata?.Name,
            "CelesteNet.Client",
            StringComparison.OrdinalIgnoreCase)) {

            // CelesteNet's section is recreated under Options -> Multiplayer.
            return;
        }

        orig?.Invoke(
            self,
            menu,
            inGame,
            snapshot);
    }

    private static EverestModule GetCelesteNetModule() {
        return Everest.Modules
            .FirstOrDefault(module =>
                string.Equals(
                    module?.Metadata?.Name,
                    "CelesteNet.Client",
                    StringComparison.OrdinalIgnoreCase));
    }

    private static object GetCelesteNetSettings() {
        EverestModule module =
            GetCelesteNetModule();

        if (module == null) {
            return null;
        }

        try {
            PropertyInfo staticSettings =
                module.GetType().GetProperty(
                    "Settings",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (staticSettings != null) {
                return staticSettings.GetValue(null);
            }

            FieldInfo field =
                typeof(EverestModule).GetField(
                    "_Settings",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            return field?.GetValue(module);
        } catch {
            return null;
        }
    }

    /// <summary>
    /// Use Everest's original generic settings generator on the actual loaded
    /// CelesteNet module, so future CelesteNet settings continue to appear.
    /// Then remove only its connection/server selector controls because Host
    /// and Join replace that part of the interface.
    /// </summary>
    private static void AddCelesteNetSettings(
        TextMenu menu,
        bool inGame,
        EventInstance snapshot) {

        EverestModule module =
            GetCelesteNetModule();

        object settings =
            GetCelesteNetSettings();

        if (module == null ||
            settings == null ||
            celesteNetSettingsHook == null) {

            menu.Add(
                new TextMenu.SubHeader(
                    "CELESTENET IS NOT LOADED",
                    false));

            return;
        }

        int firstGeneratedItem =
            menu.Items.Count;

        bool wasApplied =
            celesteNetSettingsHook.IsApplied;

        try {
            if (wasApplied) {
                celesteNetSettingsHook.Undo();
            }

            module.CreateModMenuSection(
                menu,
                inGame,
                snapshot);
        } finally {
            if (wasApplied) {
                celesteNetSettingsHook.Apply();
            }
        }

        string[] generatedConnectionProperties = {
            "EnabledEntry",
            "ConnectDefaultButton",
            "ConnectDefaultButtonHint",
            "ServerEntry",
            "ExtraServersEntry",
            "ConnectLocallyButton",
            "NameEntry"
        };

        foreach (string propertyName in
            generatedConnectionProperties) {

            RemoveReferencedMenuItem(
                menu,
                settings,
                propertyName);
        }

        // CelesteNet's "reload extra servers" button is generated separately
        // and is not exposed through a Settings property.
        string reloadLabel =
            Dialog.Clean(
                "modoptions_celestenetclient_extraservers_reload");

        foreach (TextMenu.Item item in
            menu.Items
                .Skip(firstGeneratedItem)
                .ToArray()) {

            if (item is TextMenu.Button button &&
                string.Equals(
                    button.Label,
                    reloadLabel,
                    StringComparison.OrdinalIgnoreCase)) {

                menu.Remove(item);
            }
        }
    }

    private static void RemoveReferencedMenuItem(
        TextMenu menu,
        object settings,
        string propertyName) {

        try {
            PropertyInfo property =
                settings.GetType().GetProperty(
                    propertyName,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (property?.GetValue(settings)
                is TextMenu.Item item &&
                menu.Items.Contains(item)) {

                menu.Remove(item);
            }
        } catch {
        }
    }

    private static void ShowRoot() {
        TextMenu menu =
            CreateOverlay(
                "MULTIPLAYER",
                ScreenKind.Root);

        bool connected =
            GetCelesteNetBool(
                "Connected");

        string effectiveServer =
            GetCelesteNetString(
                "EffectiveServer",
                "");

        menu.Add(
            new TextMenu.SubHeader(
                connected
                    ? $"CONNECTED: {effectiveServer}"
                    : "OFFLINE",
                false));

        menu.Add(
            new TextMenu.Button("HOST")
            .Pressed(ShowHost));

        menu.Add(
            new TextMenu.Button("JOIN")
            .Pressed(() =>
                ShowJoin(
                    startDiscovery: true)));

        if (connected) {
            menu.Add(
                new TextMenu.Button(
                    "DISCONNECT")
                .Pressed(() => {
                    SetCelesteNetProperty(
                        "Connected",
                        false);

                    ShowRoot();
                }));
        }

        menu.Add(
            new TextMenu.SubHeader(
                "CELESTENET SETTINGS"));

        string username =
            GetCelesteNetString(
                "Name",
                "Guest");

        menu.Add(
            new TextMenu.Button(
                $"USERNAME: {username}")
            .Pressed(() =>
                PromptString(
                    username,
                    20,
                    value => {
                        string cleaned =
                            (value ?? "")
                            .Trim();

                        if (cleaned.Length == 0) {
                            cleaned = "Guest";
                        }

                        if (cleaned.Length > 20) {
                            cleaned =
                                cleaned.Substring(
                                    0,
                                    20);
                        }

                        SetCelesteNetProperty(
                            "Name",
                            cleaned);

                        SaveCelesteNetSettings();
                    })));

        AddCelesteNetSettings(
            menu,
            rootInGame,
            rootSnapshot);

        menu.Add(
            new TextMenu.Button("BACK")
            .Pressed(CloseRoot));
    }

    private static void ShowHost() {
        TextMenu menu =
            CreateOverlay(
                "HOST",
                ScreenKind.Host);

        bool running =
            MobileBridgeProxy.HostRunning;

        menu.Add(
            new TextMenu.SubHeader(
                running
                    ? $"HOSTING ON PORT {Settings.HostPort}"
                    : "NOT HOSTING",
                false));

        menu.Add(
            new TextMenu.Button(
                $"PORT: {Settings.HostPort}")
            .Pressed(() =>
                PromptString(
                    Settings.HostPort.ToString(),
                    5,
                    value => {
                        if (int.TryParse(
                            value,
                            out int port)) {

                            Settings.HostPort =
                                Math.Clamp(
                                    port,
                                    1024,
                                    65535);

                            SaveOurSettings();
                        }
                    })));

        if (!running) {
            menu.Add(
                new TextMenu.Button(
                    "START HOST + JOIN")
                .Pressed(() => {
                    joinWhenHostReady = true;

                    if (!MobileBridgeProxy.StartHost(
                        Settings.HostPort)) {

                        joinWhenHostReady = false;

                        ShowInfo(
                            "HOST UNAVAILABLE",
                            "Hosting requires MobileBridge plus a native host implementation in AndroidWrapper or IOSWrapper.");

                        return;
                    }

                    ShowHost();
                }));

            menu.Add(
                new TextMenu.Button(
                    "START HOST ONLY")
                .Pressed(() => {
                    joinWhenHostReady = false;

                    if (!MobileBridgeProxy.StartHost(
                        Settings.HostPort)) {

                        ShowInfo(
                            "HOST UNAVAILABLE",
                            "Hosting requires MobileBridge plus a native host implementation in AndroidWrapper or IOSWrapper.");

                        return;
                    }

                    ShowHost();
                }));
        } else {
            menu.Add(
                new TextMenu.Button(
                    "STOP HOST")
                .Pressed(() => {
                    joinWhenHostReady = false;
                    MobileBridgeProxy.StopHost();
                    ShowHost();
                }));
        }

        menu.Add(
            new TextMenu.Button("BACK")
            .Pressed(ShowRoot));
    }

    private static void ShowJoin(
        bool startDiscovery) {

        TextMenu menu =
            CreateOverlay(
                "JOIN",
                ScreenKind.Join);

        bool connected =
            GetCelesteNetBool(
                "Connected");

        string effectiveServer =
            GetCelesteNetString(
                "EffectiveServer",
                "");

        menu.Add(
            new TextMenu.SubHeader(
                connected
                    ? $"CONNECTED: {effectiveServer}"
                    : "ACTIVE / SAVED SERVERS",
                false));

        List<ServerEntry> servers =
            BuildServerList();

        foreach (ServerEntry server in
            servers) {

            ServerEntry captured =
                server;

            string prefix =
                captured.Source switch {
                    ServerSource.Official =>
                        "OFFICIAL  ",
                    ServerSource.Wrapper =>
                        "LOCAL  ",
                    ServerSource.Lan =>
                        "LAN  ",
                    ServerSource.Custom =>
                        "CUSTOM  ",
                    ServerSource.CelesteNet =>
                        "SAVED  ",
                    _ =>
                        ""
                };

            menu.Add(
                new TextMenu.Button(
                    prefix +
                    captured.Address)
                .Pressed(() => {
                    ConnectToServer(
                        captured.Address);

                    ShowJoin(
                        startDiscovery: false);
                }));
        }

        if (servers.Count == 0) {
            menu.Add(
                new TextMenu.SubHeader(
                    "NO SERVERS FOUND",
                    false));
        }

        if (discoveryRunning) {
            menu.Add(
                new TextMenu.SubHeader(
                    "SCANNING LOCAL NETWORK...",
                    false));
        } else {
            menu.Add(
                new TextMenu.Button(
                    "SCAN LOCAL NETWORK")
                .Pressed(() => {
                    StartLanDiscovery();

                    ShowJoin(
                        startDiscovery: false);
                }));
        }

        menu.Add(
            new TextMenu.Button(
                "ADD CUSTOM SERVER")
            .Pressed(() =>
                PromptString(
                    "server.example.com:17230",
                    80,
                    AddCustomServer)));

        menu.Add(
            new TextMenu.Button(
                "MANAGE CUSTOM SERVERS")
            .Pressed(
                ShowCustomServers));

        if (connected) {
            menu.Add(
                new TextMenu.Button(
                    "DISCONNECT")
                .Pressed(() => {
                    SetCelesteNetProperty(
                        "Connected",
                        false);

                    ShowJoin(
                        startDiscovery: false);
                }));
        }

        menu.Add(
            new TextMenu.Button("BACK")
            .Pressed(ShowRoot));

        if (startDiscovery) {
            StartLanDiscovery();
        }
    }

    private static void ShowCustomServers() {
        TextMenu menu =
            CreateOverlay(
                "CUSTOM SERVERS",
                ScreenKind.CustomServers);

        string[] custom =
            Settings.CustomServers ??
            Array.Empty<string>();

        if (custom.Length == 0) {
            menu.Add(
                new TextMenu.SubHeader(
                    "NO CUSTOM SERVERS",
                    false));
        }

        foreach (string server in
            custom) {

            string captured =
                server;

            menu.Add(
                new TextMenu.Button(
                    "REMOVE  " +
                    captured)
                .Pressed(() => {
                    Settings.CustomServers =
                        (Settings.CustomServers ??
                         Array.Empty<string>())
                        .Where(value =>
                            !string.Equals(
                                value,
                                captured,
                                StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    SaveOurSettings();
                    ShowCustomServers();
                }));
        }

        menu.Add(
            new TextMenu.Button(
                "ADD SERVER")
            .Pressed(() =>
                PromptString(
                    "server.example.com:17230",
                    80,
                    AddCustomServer)));

        menu.Add(
            new TextMenu.Button("BACK")
            .Pressed(() =>
                ShowJoin(
                    startDiscovery: false)));
    }

    private static void AddCustomServer(
        string value) {

        string normalized =
            NormalizeServer(value);

        if (normalized.Length == 0) {
            return;
        }

        Settings.CustomServers =
            (Settings.CustomServers ??
             Array.Empty<string>())
            .Append(normalized)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        SaveOurSettings();
    }

    private static TextMenu CreateOverlay(
        string title,
        ScreenKind screen) {

        CloseCurrentMenu(
            restoreParent: false);

        currentScreen =
            screen;

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
            new TextMenu.Header(title));

        ModalBackdrop backdrop =
            new(menu);

        menu.OnCancel = () => {
            if (screen == ScreenKind.Root) {
                CloseRoot();
            } else {
                ShowRoot();
            }
        };

        menu.OnClose += () => {
            backdrop.RemoveSelf();

            if (ReferenceEquals(
                currentMenu,
                menu)) {

                currentMenu = null;
            }
        };

        currentMenu =
            menu;

        Engine.Scene.Add(backdrop);
        Engine.Scene.Add(menu);

        return menu;
    }

    private static void CloseCurrentMenu(
        bool restoreParent) {

        TextMenu menu =
            currentMenu;

        currentMenu = null;
        currentScreen = ScreenKind.None;

        if (menu?.Scene != null) {
            menu.Close();
        }

        if (restoreParent) {
            RestoreParent();
        }
    }

    private static void CloseRoot() {
        CloseCurrentMenu(
            restoreParent: true);
    }

    private static void RestoreParent() {
        if (optionsParent?.Scene != null) {
            optionsParent.Focused = true;
        }

        if (mainMenuParent?.Scene != null) {
            mainMenuParent.Focused = true;
        }

        optionsParent = null;
        mainMenuParent = null;
    }

    private static void ShowInfo(
        string title,
        string message) {

        TextMenu menu =
            CreateOverlay(
                title,
                currentScreen);

        menu.Add(
            new WrappedTextItem(
                message,
                900f));

        menu.Add(
            new TextMenu.Button("OK")
            .Pressed(ShowRoot));
    }

    private static void PromptString(
        string initial,
        int maxLength,
        Action<string> accepted) {

        if (Engine.Scene is not Overworld overworld) {
            return;
        }

        bool returnToMainMenu =
            mainMenuParent != null;

        CloseCurrentMenu(
            restoreParent: false);

        Audio.Play(
            "event:/ui/main/savefile_rename_start");

        OuiModOptionString entry =
            overworld.Goto<OuiModOptionString>();

        entry.Init(
            initial ?? "",
            value =>
                accepted?.Invoke(
                    value ?? ""),
            confirmed => {
                if (returnToMainMenu) {
                    overworld.Goto<OuiMainMenu>();
                } else {
                    overworld.Goto<OuiOptions>();
                }
            },
            maxLength,
            1);
    }

    private static void ConnectToServer(
        string address) {

        address =
            NormalizeServer(address);

        if (address.Length == 0) {
            return;
        }

        try {
            SetCelesteNetProperty(
                "Connected",
                false);

            SetCelesteNetProperty(
                "ServerOverride",
                "");

            SetCelesteNetProperty(
                "Server",
                address);

            SaveCelesteNetSettings();

            SetCelesteNetProperty(
                "Connected",
                true);
        } catch (Exception e) {
            Logger.Log(
                LogLevel.Error,
                "MobileMultiplayer",
                $"Could not connect to '{address}': {e}");
        }
    }

    private static List<ServerEntry> BuildServerList() {
        Dictionary<string, ServerSource> values =
            new(
                StringComparer.OrdinalIgnoreCase);

        void Add(
            string address,
            ServerSource source) {

            address =
                NormalizeServer(address);

            if (address.Length == 0) {
                return;
            }

            if (!values.ContainsKey(address) ||
                source == ServerSource.Lan ||
                source == ServerSource.Wrapper) {

                values[address] =
                    source;
            }
        }

        Add(
            GetCelesteNetDefaultServer(),
            ServerSource.Official);

        Add(
            GetCelesteNetString(
                "Server",
                ""),
            ServerSource.CelesteNet);

        foreach (string server in
            GetCelesteNetStringArray(
                "ExtraServers")) {

            Add(
                server,
                ServerSource.CelesteNet);
        }

        foreach (string server in
            Settings.CustomServers ??
            Array.Empty<string>()) {

            Add(
                server,
                ServerSource.Custom);
        }

        lock (DiscoveryLock) {
            foreach (string server in
                discoveredLanServers) {

                Add(
                    server,
                    ServerSource.Lan);
            }
        }

        foreach (string server in
            MobileBridgeProxy.DiscoveredServers) {

            Add(
                server,
                ServerSource.Wrapper);
        }

        return values
            .Select(pair =>
                new ServerEntry(
                    pair.Key,
                    pair.Value))
            .OrderBy(entry =>
                entry.Source switch {
                    ServerSource.Official => 0,
                    ServerSource.Wrapper => 1,
                    ServerSource.Lan => 2,
                    ServerSource.Custom => 3,
                    _ => 4
                })
            .ThenBy(
                entry => entry.Address,
                StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void StartLanDiscovery() {
        if (discoveryRunning) {
            return;
        }

        // Browser/WASM networking cannot perform a raw local subnet TCP scan.
        // The native wrapper can supply discovered server endpoints through
        // MobileBridge.GetCelesteNetServers().
        if (OperatingSystem.IsBrowser()) {
            discoveryRefreshPending = true;
            return;
        }

        discoveryRunning = true;

        Task.Run(async () => {
            List<string> found =
                new();

            try {
                IPAddress local =
                    GetLocalIPv4();

                if (local != null) {
                    byte[] bytes =
                        local.GetAddressBytes();

                    int[] ports =
                        Settings.HostPort == 17230
                            ? new[] {
                                17230
                            }
                            : new[] {
                                17230,
                                Settings.HostPort
                            };

                    using SemaphoreSlim gate =
                        new(32);

                    List<Task> tasks =
                        new();

                    for (int host = 1;
                        host <= 254;
                        host++) {

                        if (host == bytes[3]) {
                            continue;
                        }

                        foreach (int port in
                            ports.Distinct()) {

                            string ip =
                                $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{host}";

                            int capturedPort =
                                port;

                            tasks.Add(
                                Task.Run(
                                    async () => {
                                        await gate
                                            .WaitAsync()
                                            .ConfigureAwait(false);

                                        try {
                                            using TcpClient client =
                                                new(
                                                    AddressFamily.InterNetwork);

                                            using CancellationTokenSource timeout =
                                                new(
                                                    TimeSpan.FromMilliseconds(
                                                        220));

                                            try {
                                                await client
                                                    .ConnectAsync(
                                                        ip,
                                                        capturedPort,
                                                        timeout.Token)
                                                    .ConfigureAwait(false);

                                                if (client.Connected) {
                                                    lock (found) {
                                                        found.Add(
                                                            capturedPort == 17230
                                                                ? ip
                                                                : $"{ip}:{capturedPort}");
                                                    }
                                                }
                                            } catch {
                                            }
                                        } finally {
                                            gate.Release();
                                        }
                                    }));
                        }
                    }

                    await Task
                        .WhenAll(tasks)
                        .ConfigureAwait(false);
                }
            } catch (Exception e) {
                Logger.Log(
                    LogLevel.Warn,
                    "MobileMultiplayer",
                    $"LAN discovery failed: {e.Message}");
            } finally {
                lock (DiscoveryLock) {
                    discoveredLanServers =
                        found
                        .Distinct(
                            StringComparer.OrdinalIgnoreCase)
                        .OrderBy(value =>
                            value)
                        .ToList();
                }

                discoveryRunning = false;
                discoveryRefreshPending = true;
            }
        });
    }

    private static IPAddress GetLocalIPv4() {
        try {
            return Dns
                .GetHostEntry(
                    Dns.GetHostName())
                .AddressList
                .FirstOrDefault(address =>
                    address.AddressFamily ==
                        AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(
                        address));
        } catch {
            return null;
        }
    }

    private static void OnEngineUpdate(
        On.Monocle.Engine.orig_Update orig,
        Engine engine,
        GameTime gameTime) {

        orig(
            engine,
            gameTime);

        if (joinWhenHostReady &&
            MobileBridgeProxy.HostRunning) {

            joinWhenHostReady = false;

            Scene scene =
                Engine.Scene;

            if (scene != null) {
                scene.OnEndOfFrame += () =>
                    ConnectToServer(
                        $"127.0.0.1:{Settings.HostPort}");
            }
        }

        if (discoveryRefreshPending) {
            discoveryRefreshPending = false;

            if (currentScreen == ScreenKind.Join &&
                currentMenu?.Scene != null) {

                Scene scene =
                    Engine.Scene;

                if (scene != null) {
                    scene.OnEndOfFrame += () =>
                        ShowJoin(
                            startDiscovery: false);
                }
            }
        }
    }

    private static bool IsMobileTweaksLoaded() {
        return AppDomain.CurrentDomain
            .GetAssemblies()
            .Any(assembly =>
                assembly.GetType(
                    "Celeste.Mod.MobileTweaks.MobileTweaksModule",
                    throwOnError: false) != null);
    }

    private static void NormalizeSettings() {
        Settings.CustomServers ??=
            Array.Empty<string>();

        Settings.CustomServers =
            Settings.CustomServers
            .Select(NormalizeServer)
            .Where(value =>
                value.Length > 0)
            .Distinct(
                StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Settings.HostPort =
            Math.Clamp(
                Settings.HostPort,
                1024,
                65535);
    }

    private static void SaveOurSettings() {
        NormalizeSettings();

        try {
            Instance.SaveSettings();
        } catch {
        }
    }

    private static string NormalizeServer(
        string value) {

        string server =
            (value ?? "")
            .Trim();

        if (server.StartsWith(
            "http://",
            StringComparison.OrdinalIgnoreCase)) {

            server =
                server.Substring(7);
        }

        if (server.StartsWith(
            "https://",
            StringComparison.OrdinalIgnoreCase)) {

            server =
                server.Substring(8);
        }

        return server
            .Trim()
            .TrimEnd('/');
    }

    private static object GetCelesteNetProperty(
        string name) {

        object settings =
            GetCelesteNetSettings();

        if (settings == null) {
            return null;
        }

        try {
            return settings
                .GetType()
                .GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic)
                ?.GetValue(settings);
        } catch {
            return null;
        }
    }

    private static void SetCelesteNetProperty(
        string name,
        object value) {

        object settings =
            GetCelesteNetSettings();

        if (settings == null) {
            return;
        }

        try {
            PropertyInfo property =
                settings
                .GetType()
                .GetProperty(
                    name,
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            if (property?.CanWrite == true) {
                property.SetValue(
                    settings,
                    value);
            }
        } catch (Exception e) {
            Logger.Log(
                LogLevel.Warn,
                "MobileMultiplayer",
                $"Could not set CelesteNet setting '{name}': {e.Message}");
        }
    }

    private static bool GetCelesteNetBool(
        string name) {

        object value =
            GetCelesteNetProperty(name);

        return value is bool boolean &&
            boolean;
    }

    private static string GetCelesteNetString(
        string name,
        string fallback) {

        return GetCelesteNetProperty(name)
            as string ??
            fallback;
    }

    private static string[] GetCelesteNetStringArray(
        string name) {

        return GetCelesteNetProperty(name)
            as string[] ??
            Array.Empty<string>();
    }

    private static string GetCelesteNetDefaultServer() {
        object settings =
            GetCelesteNetSettings();

        if (settings == null) {
            return "celeste.0x0a.de";
        }

        try {
            FieldInfo field =
                settings
                .GetType()
                .GetField(
                    "DefaultServer",
                    BindingFlags.Static |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            return field?.GetValue(null)
                as string ??
                "celeste.0x0a.de";
        } catch {
            return "celeste.0x0a.de";
        }
    }

    private static void SaveCelesteNetSettings() {
        EverestModule module =
            GetCelesteNetModule();

        if (module == null) {
            return;
        }

        try {
            MethodInfo method =
                module.GetType().GetMethod(
                    "SaveSettings",
                    BindingFlags.Instance |
                    BindingFlags.Public |
                    BindingFlags.NonPublic);

            method?.Invoke(
                module,
                null);
        } catch {
        }
    }

    private sealed class ModalBackdrop : Entity {
        private readonly TextMenu menu;

        public ModalBackdrop(
            TextMenu menu) {

            this.menu =
                menu;

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

    private sealed class WrappedTextItem : TextMenu.Item {
        private readonly FancyText.Text text;

        public WrappedTextItem(
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
                0.55f +
                30f;
        }

        public override float LeftWidth() {
            return 920f;
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
                Vector2.One * 0.55f,
                Container.Alpha);
        }
    }

    private static class MobileBridgeProxy {
        private static Type apiType;
        private static bool resolved;

        private static void Resolve() {
            if (resolved) {
                return;
            }

            resolved = true;

            apiType =
                AppDomain.CurrentDomain
                .GetAssemblies()
                .Select(assembly =>
                    assembly.GetType(
                        "Celeste.Mod.MobileBridge.MobileBridgeApi",
                        throwOnError: false))
                .FirstOrDefault(type =>
                    type != null);
        }

        public static bool HostRunning {
            get {
                Resolve();

                if (apiType == null) {
                    return false;
                }

                try {
                    PropertyInfo property =
                        apiType.GetProperty(
                            "IsCelesteNetHostRunning",
                            BindingFlags.Static |
                            BindingFlags.Public);

                    return property != null &&
                        property.GetValue(null)
                        is bool running &&
                        running;
                } catch {
                    return false;
                }
            }
        }

        public static string[] DiscoveredServers {
            get {
                return Invoke(
                    "GetCelesteNetServers",
                    Array.Empty<string>());
            }
        }

        public static bool StartHost(
            int port) {

            return Invoke(
                "StartCelesteNetHost",
                false,
                port);
        }

        public static void StopHost() {
            Resolve();

            if (apiType == null) {
                return;
            }

            try {
                apiType
                    .GetMethod(
                        "StopCelesteNetHost",
                        BindingFlags.Static |
                        BindingFlags.Public)
                    ?.Invoke(
                        null,
                        null);
            } catch {
            }
        }

        private static T Invoke<T>(
            string methodName,
            T fallback,
            params object[] args) {

            Resolve();

            if (apiType == null) {
                return fallback;
            }

            try {
                MethodInfo method =
                    apiType.GetMethod(
                        methodName,
                        BindingFlags.Static |
                        BindingFlags.Public);

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
    }
}
