using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Celeste;
using Celeste.Mod;
using Celeste.Mod.CelesteNet.Client;
using Celeste.Mod.UI;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.MobileMultiplayer;

public sealed class MobileMultiplayerSettings : EverestModuleSettings {
    [SettingIgnore]
    public string Username { get; set; } = "Guest";

    [SettingIgnore]
    public string[] CustomServers { get; set; } = Array.Empty<string>();

    [SettingIgnore]
    public int HostPort { get; set; } = 17230;
}

public sealed class MobileMultiplayerModule : EverestModule {
    public static MobileMultiplayerModule Instance { get; private set; }
    public static MobileMultiplayerSettings Settings => (MobileMultiplayerSettings)Instance._Settings;
    public override Type SettingsType => typeof(MobileMultiplayerSettings);

    private static Action<OuiMainMenu> pendingMainMenuAction;
    private static TextMenu currentMenu;
    private static OuiMainMenu currentOwner;
    private static MenuKind currentMenuKind;

    private static readonly object HostLock = new();
    private static object hostedServer;
    private static Thread hostedServerThread;
    private static volatile bool hostRunning;
    private static volatile bool hostStarting;
    private static volatile bool hostJoinRequested;
    private static string hostStatus = "NOT HOSTING";

    private static readonly object DiscoveryLock = new();
    private static List<string> discoveredLanServers = new();
    private static volatile bool discoveryRunning;
    private static volatile bool discoveryRefreshPending;

    private enum MenuKind {
        None,
        Main,
        Host,
        Join,
        CustomServers
    }

    public MobileMultiplayerModule() {
        Instance = this;
    }

    public override void Load() {
        Everest.Events.MainMenu.OnCreateButtons += OnCreateMainMenuButtons;
        On.Celeste.OuiMainMenu.Update += OnMainMenuUpdate;
        On.Monocle.Engine.Update += OnEngineUpdate;
    }

    public override void Initialize() {
        base.Initialize();
        NormalizeSettings();
        if (string.IsNullOrWhiteSpace(Settings.Username) || Settings.Username == "Guest") {
            try {
                if (!string.IsNullOrWhiteSpace(CelesteNetClientModule.Settings?.Name))
                    Settings.Username = CelesteNetClientModule.Settings.Name;
            } catch {
            }
        }
    }

    public override void Unload() {
        Everest.Events.MainMenu.OnCreateButtons -= OnCreateMainMenuButtons;
        On.Celeste.OuiMainMenu.Update -= OnMainMenuUpdate;
        On.Monocle.Engine.Update -= OnEngineUpdate;
        StopHost();
        pendingMainMenuAction = null;
        currentMenu = null;
        currentOwner = null;
        currentMenuKind = MenuKind.None;
    }

    private static void NormalizeSettings() {
        Settings.Username = NormalizeUsername(Settings.Username);
        Settings.CustomServers ??= Array.Empty<string>();
        Settings.CustomServers = Settings.CustomServers
            .Select(NormalizeServer)
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Settings.HostPort = Math.Clamp(Settings.HostPort, 1024, 65535);
    }

    private static void SaveOurSettings() {
        NormalizeSettings();
        try { Instance.SaveSettings(); } catch { }
    }

    private static void OnCreateMainMenuButtons(OuiMainMenu menu, List<MenuButton> buttons) {
        Vector2 pos = Vector2.Zero;
        int index = Math.Max(0, buttons.Count - 1);
        buttons.Insert(index, new MainMenuSmallButton("MULTIPLAYER", "menu/options", menu, pos, pos, () => ShowMultiplayerMenu(menu)));
    }

    private static void OnMainMenuUpdate(On.Celeste.OuiMainMenu.orig_Update orig, OuiMainMenu menu) {
        orig(menu);
        if (pendingMainMenuAction == null || menu == null || !menu.Visible || !menu.Focused)
            return;

        Action<OuiMainMenu> action = pendingMainMenuAction;
        pendingMainMenuAction = null;
        Engine.Scene.OnEndOfFrame += () => action(menu);
    }

    private static void OnEngineUpdate(On.Monocle.Engine.orig_Update orig, Engine engine, GameTime gameTime) {
        orig(engine, gameTime);

        if (hostJoinRequested && hostRunning) {
            hostJoinRequested = false;
            Scene scene = Engine.Scene;
            if (scene != null)
                scene.OnEndOfFrame += () => ConnectToServer($"127.0.0.1:{Settings.HostPort}");
            else
                ConnectToServer($"127.0.0.1:{Settings.HostPort}");
        }

        if (discoveryRefreshPending) {
            discoveryRefreshPending = false;
            if (currentMenuKind == MenuKind.Join && currentOwner != null && currentMenu != null && currentMenu.Scene != null) {
                OuiMainMenu owner = currentOwner;
                Engine.Scene.OnEndOfFrame += () => ShowJoinMenu(owner, startDiscovery: false);
            }
        }
    }

    private static void ShowMultiplayerMenu(OuiMainMenu owner) {
        TextMenu menu = CreateMenu("MULTIPLAYER", owner, MenuKind.Main);
        CelesteNetClientSettings cn = CelesteNetClientModule.Settings;
        string connection = cn.Connected ? $"CONNECTED: {cn.EffectiveServer}" : "OFFLINE";
        menu.Add(new TextMenu.SubHeader(connection, false));

        menu.Add(new TextMenu.Button($"USERNAME: {Settings.Username}").Pressed(() => {
            CloseCurrentMenu();
            PromptString(owner, Settings.Username, 20, value => {
                Settings.Username = NormalizeUsername(value);
                SaveOurSettings();
                ApplyUsernameToCelesteNet();
                pendingMainMenuAction = ShowMultiplayerMenu;
            });
        }));

        menu.Add(new TextMenu.Button("HOST").Pressed(() => ShowHostMenu(owner)));
        menu.Add(new TextMenu.Button("JOIN").Pressed(() => ShowJoinMenu(owner, startDiscovery: true)));

        if (cn.Connected) {
            menu.Add(new TextMenu.Button("DISCONNECT").Pressed(() => {
                cn.Connected = false;
                RefreshCurrent(MenuKind.Main);
            }));
        }

        menu.Add(new TextMenu.Button("CELESTENET SETTINGS").Pressed(() => {
            CloseCurrentMenu();
            owner.Overworld.Goto<OuiModOptions>();
        }));

        menu.Add(new TextMenu.Button("CLOSE").Pressed(CloseCurrentMenu));
    }

    private static void ShowHostMenu(OuiMainMenu owner) {
        TextMenu menu = CreateMenu("HOST MULTIPLAYER", owner, MenuKind.Host);
        string status;
        lock (HostLock) status = hostStatus;
        menu.Add(new TextMenu.SubHeader(status, false));
        menu.Add(new TextMenu.SubHeader($"YOUR SERVER:  {GetBestLocalAddress()}:{Settings.HostPort}", false));

        menu.Add(new TextMenu.Button($"USERNAME: {Settings.Username}").Pressed(() => {
            CloseCurrentMenu();
            PromptString(owner, Settings.Username, 20, value => {
                Settings.Username = NormalizeUsername(value);
                SaveOurSettings();
                ApplyUsernameToCelesteNet();
                pendingMainMenuAction = ShowHostMenu;
            });
        }));

        menu.Add(new TextMenu.Button($"PORT: {Settings.HostPort}").Pressed(() => {
            if (hostRunning || hostStarting)
                return;
            CloseCurrentMenu();
            PromptString(owner, Settings.HostPort.ToString(), 5, value => {
                if (int.TryParse(value, out int port))
                    Settings.HostPort = Math.Clamp(port, 1024, 65535);
                SaveOurSettings();
                pendingMainMenuAction = ShowHostMenu;
            });
        }));

        if (!hostRunning && !hostStarting) {
            menu.Add(new TextMenu.Button("START HOST + JOIN").Pressed(() => {
                if (OperatingSystem.IsBrowser()) {
                    ShowInfo(owner,
                        "HOSTING NEEDS WRAPPER SUPPORT",
                        "CelesteNet.Server opens native TCP/UDP listening sockets. A browser/WASM build cannot provide that server socket directly. " +
                        "Joining still uses CelesteNet.Client; hosting works in the native build. The Android wrapper can later expose a native host service through MobileBridge without changing this menu.",
                        ShowHostMenu);
                    return;
                }
                StartHost(joinAfterStart: true);
                RefreshCurrent(MenuKind.Host);
            }));

            menu.Add(new TextMenu.Button("START HOST ONLY").Pressed(() => {
                if (OperatingSystem.IsBrowser()) {
                    ShowInfo(owner,
                        "HOSTING NEEDS WRAPPER SUPPORT",
                        "The browser/WASM runtime cannot listen as a CelesteNet TCP/UDP server. Use JOIN here, or expose native hosting through the Android wrapper.",
                        ShowHostMenu);
                    return;
                }
                StartHost(joinAfterStart: false);
                RefreshCurrent(MenuKind.Host);
            }));
        } else {
            menu.Add(new TextMenu.Button("STOP HOST").Pressed(() => {
                StopHost();
                RefreshCurrent(MenuKind.Host);
            }));
        }

        menu.Add(new TextMenu.Button("BACK").Pressed(() => ShowMultiplayerMenu(owner)));
    }

    private static void ShowJoinMenu(OuiMainMenu owner, bool startDiscovery) {
        TextMenu menu = CreateMenu("JOIN MULTIPLAYER", owner, MenuKind.Join);
        CelesteNetClientSettings cn = CelesteNetClientModule.Settings;
        menu.Add(new TextMenu.SubHeader(cn.Connected ? $"CONNECTED: {cn.EffectiveServer}" : "SELECT A SERVER", false));

        List<ServerEntry> servers = BuildServerList();
        foreach (ServerEntry server in servers) {
            ServerEntry captured = server;
            string prefix = captured.Source switch {
                ServerSource.Lan => "LAN  ",
                ServerSource.Custom => "CUSTOM  ",
                ServerSource.CelesteNet => "SAVED  ",
                _ => ""
            };
            menu.Add(new TextMenu.Button(prefix + captured.Address).Pressed(() => {
                ConnectToServer(captured.Address);
                RefreshCurrent(MenuKind.Join);
            }));
        }

        if (servers.Count == 0)
            menu.Add(new TextMenu.SubHeader("NO SERVERS FOUND", false));

        if (discoveryRunning)
            menu.Add(new TextMenu.SubHeader("SCANNING LOCAL NETWORK...", false));
        else
            menu.Add(new TextMenu.Button("SCAN LOCAL NETWORK").Pressed(() => {
                StartLanDiscovery();
                RefreshCurrent(MenuKind.Join);
            }));

        menu.Add(new TextMenu.Button("ADD CUSTOM SERVER").Pressed(() => {
            CloseCurrentMenu();
            PromptString(owner, "server.example.com:17230", 80, value => {
                string server = NormalizeServer(value);
                if (!string.IsNullOrWhiteSpace(server)) {
                    Settings.CustomServers = Settings.CustomServers
                        .Append(server)
                        .Distinct(StringComparer.OrdinalIgnoreCase)
                        .ToArray();
                    SaveOurSettings();
                }
                pendingMainMenuAction = main => ShowJoinMenu(main, startDiscovery: false);
            });
        }));

        menu.Add(new TextMenu.Button("MANAGE CUSTOM SERVERS").Pressed(() => ShowCustomServersMenu(owner)));
        if (cn.Connected)
            menu.Add(new TextMenu.Button("DISCONNECT").Pressed(() => {
                cn.Connected = false;
                RefreshCurrent(MenuKind.Join);
            }));
        menu.Add(new TextMenu.Button("BACK").Pressed(() => ShowMultiplayerMenu(owner)));

        if (startDiscovery && !OperatingSystem.IsBrowser())
            StartLanDiscovery();
    }

    private static void ShowCustomServersMenu(OuiMainMenu owner) {
        TextMenu menu = CreateMenu("CUSTOM SERVERS", owner, MenuKind.CustomServers);
        string[] servers = Settings.CustomServers ?? Array.Empty<string>();

        if (servers.Length == 0)
            menu.Add(new TextMenu.SubHeader("NO CUSTOM SERVERS", false));

        foreach (string server in servers) {
            string captured = server;
            menu.Add(new TextMenu.Button("REMOVE  " + captured).Pressed(() => {
                Settings.CustomServers = Settings.CustomServers
                    .Where(s => !string.Equals(s, captured, StringComparison.OrdinalIgnoreCase))
                    .ToArray();
                SaveOurSettings();
                ShowCustomServersMenu(owner);
            }));
        }

        menu.Add(new TextMenu.Button("ADD SERVER").Pressed(() => {
            CloseCurrentMenu();
            PromptString(owner, "server.example.com:17230", 80, value => {
                string server = NormalizeServer(value);
                if (!string.IsNullOrWhiteSpace(server)) {
                    Settings.CustomServers = Settings.CustomServers.Append(server).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
                    SaveOurSettings();
                }
                pendingMainMenuAction = ShowCustomServersMenu;
            });
        }));
        menu.Add(new TextMenu.Button("BACK").Pressed(() => ShowJoinMenu(owner, startDiscovery: false)));
    }

    private static void ShowInfo(OuiMainMenu owner, string title, string text, Action<OuiMainMenu> onClose) {
        TextMenu menu = CreateMenu(title, owner, MenuKind.None);
        menu.Add(new WrappedTextItem(text, 920f));
        menu.Add(new TextMenu.Button("OK").Pressed(() => {
            CloseCurrentMenu();
            onClose?.Invoke(owner);
        }));
    }

    private static TextMenu CreateMenu(string title, OuiMainMenu owner, MenuKind kind) {
        CloseCurrentMenu();

        TextMenu menu = new() {
            Position = new Vector2(Engine.Width, Engine.Height) / 2f,
            Tag = Tags.HUD,
            ItemSpacing = 12f
        };
        menu.Add(new TextMenu.Header(title));
        menu.OnCancel = CloseCurrentMenu;

        ModalBackdrop backdrop = new(menu);
        OptionalPointerController pointer = new(menu);
        menu.OnClose += () => {
            backdrop.RemoveSelf();
            pointer.RemoveSelf();
            if (currentMenu == menu) {
                currentMenu = null;
                currentOwner = null;
                currentMenuKind = MenuKind.None;
            }
        };

        currentMenu = menu;
        currentOwner = owner;
        currentMenuKind = kind;
        Engine.Scene.Add(backdrop);
        Engine.Scene.Add(menu);
        Engine.Scene.Add(pointer);
        return menu;
    }

    private static void CloseCurrentMenu() {
        TextMenu menu = currentMenu;
        currentMenu = null;
        currentOwner = null;
        currentMenuKind = MenuKind.None;
        if (menu != null && menu.Scene != null)
            menu.Close();
    }

    private static void RefreshCurrent(MenuKind expectedKind) {
        if (currentOwner == null)
            return;
        OuiMainMenu owner = currentOwner;
        Engine.Scene.OnEndOfFrame += () => {
            if (expectedKind == MenuKind.Main) ShowMultiplayerMenu(owner);
            else if (expectedKind == MenuKind.Host) ShowHostMenu(owner);
            else if (expectedKind == MenuKind.Join) ShowJoinMenu(owner, startDiscovery: false);
            else if (expectedKind == MenuKind.CustomServers) ShowCustomServersMenu(owner);
        };
    }

    private static void PromptString(OuiMainMenu owner, string initial, int maxLength, Action<string> accepted) {
        if (owner?.Overworld == null)
            return;
        Audio.Play("event:/ui/main/savefile_rename_start");
        owner.Overworld.Goto<OuiModOptionString>().Init<OuiMainMenu>(
            initial ?? string.Empty,
            value => accepted?.Invoke(value ?? string.Empty),
            maxLength
        );
    }

    private static void ApplyUsernameToCelesteNet() {
        CelesteNetClientSettings cn = CelesteNetClientModule.Settings;
        if (cn == null)
            return;
        cn.LoginMode = CelesteNetClientSettings.LoginModeType.Guest;
        cn.Name = Settings.Username;
        try { CelesteNetClientModule.Instance.SaveSettings(); } catch { }
    }

    private static void ConnectToServer(string address) {
        address = NormalizeServer(address);
        if (string.IsNullOrWhiteSpace(address))
            return;

        try {
            CelesteNetClientSettings cn = CelesteNetClientModule.Settings;
            if (cn.Connected)
                cn.Connected = false;
            cn.ServerOverride = string.Empty;
            cn.Server = address;
            cn.LoginMode = CelesteNetClientSettings.LoginModeType.Guest;
            cn.Name = Settings.Username;
            try { CelesteNetClientModule.Instance.SaveSettings(); } catch { }
            cn.Connected = true;
        } catch (Exception e) {
            Logger.Log(LogLevel.Error, "MobileMultiplayer", $"Failed to connect to {address}: {e}");
        }
    }

    private enum ServerSource {
        Default,
        CelesteNet,
        Custom,
        Lan
    }

    private sealed record ServerEntry(string Address, ServerSource Source);

    private static List<ServerEntry> BuildServerList() {
        Dictionary<string, ServerSource> servers = new(StringComparer.OrdinalIgnoreCase);
        void Add(string address, ServerSource source) {
            address = NormalizeServer(address);
            if (string.IsNullOrWhiteSpace(address))
                return;
            if (!servers.ContainsKey(address) || source == ServerSource.Lan)
                servers[address] = source;
        }

        Add(CelesteNetClientSettings.DefaultServer, ServerSource.Default);
        try {
            CelesteNetClientSettings cn = CelesteNetClientModule.Settings;
            Add(cn.Server, ServerSource.CelesteNet);
            foreach (string extra in cn.ExtraServers ?? Array.Empty<string>())
                Add(extra, ServerSource.CelesteNet);
        } catch {
        }
        foreach (string custom in Settings.CustomServers ?? Array.Empty<string>())
            Add(custom, ServerSource.Custom);
        lock (DiscoveryLock) {
            foreach (string lan in discoveredLanServers)
                Add(lan, ServerSource.Lan);
        }

        return servers.Select(kv => new ServerEntry(kv.Key, kv.Value))
            .OrderByDescending(s => s.Source == ServerSource.Lan)
            .ThenBy(s => s.Source)
            .ThenBy(s => s.Address, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static void StartLanDiscovery() {
        if (OperatingSystem.IsBrowser() || discoveryRunning)
            return;

        discoveryRunning = true;
        Task.Run(async () => {
            List<string> found = new();
            try {
                IPAddress local = GetLocalIPv4();
                if (local != null) {
                    byte[] bytes = local.GetAddressBytes();
                    int[] ports = Settings.HostPort == 17230 ? new[] { 17230 } : new[] { 17230, Settings.HostPort };
                    using SemaphoreSlim gate = new(32);
                    List<Task> tasks = new();
                    for (int host = 1; host <= 254; host++) {
                        if (host == bytes[3])
                            continue;
                        foreach (int port in ports.Distinct()) {
                            string ip = $"{bytes[0]}.{bytes[1]}.{bytes[2]}.{host}";
                            int capturedPort = port;
                            tasks.Add(Task.Run(async () => {
                                await gate.WaitAsync().ConfigureAwait(false);
                                try {
                                    using TcpClient client = new(AddressFamily.InterNetwork);
                                    using CancellationTokenSource timeout = new(TimeSpan.FromMilliseconds(220));
                                    try {
                                        await client.ConnectAsync(ip, capturedPort, timeout.Token).ConfigureAwait(false);
                                        if (client.Connected) {
                                            lock (found)
                                                found.Add(capturedPort == 17230 ? ip : $"{ip}:{capturedPort}");
                                        }
                                    } catch {
                                    }
                                } finally {
                                    gate.Release();
                                }
                            }));
                        }
                    }
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
            } catch (Exception e) {
                Logger.Log(LogLevel.Warn, "MobileMultiplayer", $"LAN discovery failed: {e.Message}");
            } finally {
                lock (DiscoveryLock) {
                    discoveredLanServers = found.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(s => s).ToList();
                }
                discoveryRunning = false;
                discoveryRefreshPending = true;
            }
        });
    }

    private static IPAddress GetLocalIPv4() {
        try {
            return Dns.GetHostEntry(Dns.GetHostName()).AddressList
                .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
        } catch {
            return null;
        }
    }

    private static string GetBestLocalAddress() => GetLocalIPv4()?.ToString() ?? "127.0.0.1";

    private static void StartHost(bool joinAfterStart) {
        lock (HostLock) {
            if (hostRunning || hostStarting)
                return;
            hostStarting = true;
            hostJoinRequested = joinAfterStart;
            hostStatus = "STARTING SERVER...";
        }

        hostedServerThread = new Thread(() => {
            object server = null;
            try {
                EmbeddedServerRuntime.InstallResolver();
                Assembly assembly = EmbeddedServerRuntime.LoadAssembly("CelesteNet.Server");
                if (assembly == null)
                    throw new FileNotFoundException("CelesteNet.Server.dll was not embedded. Build CelesteNet.Server before building MobileMultiplayer.");

                Type settingsType = assembly.GetType("Celeste.Mod.CelesteNet.Server.CelesteNetServerSettings", throwOnError: true);
                Type serverType = assembly.GetType("Celeste.Mod.CelesteNet.Server.CelesteNetServer", throwOnError: true);
                object serverSettings = Activator.CreateInstance(settingsType);

                string root = GetServerDataDirectory();
                string modules = Path.Combine(root, "Modules");
                string configs = Path.Combine(root, "ModuleConfigs");
                string users = Path.Combine(root, "UserData");
                string packets = Path.Combine(root, "packetDump");
                Directory.CreateDirectory(modules);
                Directory.CreateDirectory(configs);
                Directory.CreateDirectory(users);
                Directory.CreateDirectory(packets);

                SetProperty(settingsType, serverSettings, "ModuleRoot", modules);
                SetProperty(settingsType, serverSettings, "ModuleConfigRoot", configs);
                SetProperty(settingsType, serverSettings, "UserDataRoot", users);
                SetProperty(settingsType, serverSettings, "PacketDumperDirectory", packets);
                SetProperty(settingsType, serverSettings, "MainPort", Settings.HostPort);

                server = Activator.CreateInstance(serverType, new object[] { serverSettings });
                lock (HostLock)
                    hostedServer = server;

                serverType.GetMethod("Start", BindingFlags.Public | BindingFlags.Instance)!.Invoke(server, null);
                lock (HostLock) {
                    hostStarting = false;
                    hostRunning = true;
                    hostStatus = $"HOSTING ON PORT {Settings.HostPort}";
                }

                try {
                    serverType.GetMethod("Wait", BindingFlags.Public | BindingFlags.Instance)!.Invoke(server, null);
                } catch (TargetInvocationException e) when (e.InnerException is OperationCanceledException) {
                } catch (OperationCanceledException) {
                }
            } catch (Exception e) {
                Exception actual = e is TargetInvocationException tie && tie.InnerException != null ? tie.InnerException : e;
                Logger.Log(LogLevel.Error, "MobileMultiplayer", $"Host failed: {actual}");
                lock (HostLock) {
                    hostStatus = "HOST FAILED: " + actual.Message;
                    hostJoinRequested = false;
                }
            } finally {
                if (server is IDisposable disposable) {
                    try { disposable.Dispose(); } catch { }
                }
                lock (HostLock) {
                    if (ReferenceEquals(hostedServer, server))
                        hostedServer = null;
                    hostStarting = false;
                    hostRunning = false;
                    if (!hostStatus.StartsWith("HOST FAILED", StringComparison.Ordinal))
                        hostStatus = "NOT HOSTING";
                }
            }
        }) {
            IsBackground = true,
            Name = "MobileMultiplayer CelesteNet Host"
        };
        hostedServerThread.Start();
    }

    private static void StopHost() {
        object server;
        lock (HostLock) {
            server = hostedServer;
            hostJoinRequested = false;
            if (server == null) {
                hostStarting = false;
                hostRunning = false;
                hostStatus = "NOT HOSTING";
                return;
            }
            hostStatus = "STOPPING SERVER...";
        }

        try {
            if (server is IDisposable disposable)
                disposable.Dispose();
            else
                server.GetType().GetMethod("Dispose", BindingFlags.Public | BindingFlags.Instance)?.Invoke(server, null);
        } catch (Exception e) {
            Logger.Log(LogLevel.Warn, "MobileMultiplayer", $"Error stopping host: {e.Message}");
        }
    }

    private static void SetProperty(Type type, object instance, string propertyName, object value) {
        PropertyInfo property = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (property == null || !property.CanWrite)
            throw new MissingMemberException(type.FullName, propertyName);
        property.SetValue(instance, value);
    }

    private static string GetServerDataDirectory() {
        string gamePath = null;
        try {
            gamePath = typeof(Everest).GetProperty("PathGame", BindingFlags.Public | BindingFlags.Static)?.GetValue(null) as string;
        } catch {
        }
        if (string.IsNullOrWhiteSpace(gamePath))
            gamePath = AppContext.BaseDirectory;
        string dir = Path.Combine(gamePath, "Saves", "MobileMultiplayerServer");
        Directory.CreateDirectory(dir);
        return dir;
    }

    private static string NormalizeUsername(string value) {
        string result = (value ?? string.Empty).Trim();
        if (result.Length > 20)
            result = result.Substring(0, 20);
        return string.IsNullOrWhiteSpace(result) ? "Guest" : result;
    }

    private static string NormalizeServer(string value) {
        string server = (value ?? string.Empty).Trim();
        if (server.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            server = server.Substring(7);
        if (server.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            server = server.Substring(8);
        server = server.Trim().TrimEnd('/');
        return server;
    }

    private sealed class ModalBackdrop : Entity {
        private readonly TextMenu menu;
        public ModalBackdrop(TextMenu menu) {
            this.menu = menu;
            Tag = Tags.HUD | Tags.PauseUpdate;
            Depth = menu.Depth + 1;
        }
        public override void Render() {
            Draw.Rect(0, 0, 1920, 1080, Color.Black * 0.78f);
            if (menu?.Scene == null)
                return;
            menu.RecalculateSize();
            float w = Math.Min(1500f, menu.Width + 100f);
            float h = Math.Min(980f, menu.Height + 80f);
            Vector2 p = menu.Position;
            Draw.Rect(p.X - w * 0.5f, p.Y - h * 0.5f, w, h, Color.Black * 0.94f);
            Draw.HollowRect(p.X - w * 0.5f, p.Y - h * 0.5f, w, h, Color.White * 0.9f);
        }
    }

    private sealed class OptionalPointerController : Entity {
        private readonly TextMenu menu;
        public OptionalPointerController(TextMenu menu) {
            this.menu = menu;
            Tag = Tags.HUD | Tags.PauseUpdate;
            Depth = -2000001;
        }
        public override void Update() {
            base.Update();
            if (menu == null || menu.Scene == null || !menu.Visible || !menu.Focused || menu.Items == null || menu.Items.Count == 0)
                return;

            if (!OptionalMobileBridge.TouchAvailable)
                return;

            Vector2 pointer = OptionalMobileBridge.TouchPosition;
            bool pressed = OptionalMobileBridge.ConsumeTouchTap();
            float scroll = OptionalMobileBridge.ConsumeTouchScroll();
            menu.RecalculateSize();
            Vector2 origin = menu.Position - menu.Justify * new Vector2(menu.Width, menu.Height);

            if (Math.Abs(scroll) > 34f)
                menu.MoveSelection(scroll > 0 ? -1 : 1, true);

            float itemY = origin.Y;
            for (int i = 0; i < menu.Items.Count; i++) {
                TextMenu.Item item = menu.Items[i];
                if (item == null || !item.Visible)
                    continue;
                float h = item.Height();
                float centerY = itemY + h * 0.5f;
                float hitH = Math.Max(h, 80f);
                if (item.Hoverable && pointer.X >= origin.X - 100f && pointer.X <= origin.X + menu.Width + 100f && pointer.Y >= centerY - hitH * 0.5f && pointer.Y <= centerY + hitH * 0.5f) {
                    if (menu.Current != item) {
                        menu.Current?.OnLeave?.Invoke();
                        menu.Selection = i;
                        item.OnEnter?.Invoke();
                        item.SelectWiggler?.Start();
                    }
                    if (pressed) {
                        item.ConfirmPressed();
                        item.OnPressed?.Invoke();
                    }
                    return;
                }
                itemY += h + menu.ItemSpacing;
            }
        }
    }

    private sealed class WrappedTextItem : TextMenu.Item {
        private readonly FancyText.Text text;
        public WrappedTextItem(string value, float width) {
            Selectable = false;
            text = FancyText.Parse(value ?? string.Empty, (int)width, 100);
        }
        public override float Height() => text.Lines * ActiveFont.LineHeight * 0.55f + 30f;
        public override float LeftWidth() => 920f;
        public override void Render(Vector2 position, bool highlighted) {
            text.Draw(position + new Vector2(Container.Width * 0.5f, 0), new Vector2(0.5f, 0.5f), Vector2.One * 0.55f, Container.Alpha);
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
                    return new Vector2(
                        Convert.ToSingle(touchX?.Invoke(null, null) ?? -1f),
                        Convert.ToSingle(touchY?.Invoke(null, null) ?? -1f));
                } catch { return new Vector2(-1, -1); }
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

    private static class EmbeddedServerRuntime {
        private const string ResourcePrefix = "MobileMultiplayer.EmbeddedServer.";
        private static readonly object Sync = new();
        private static readonly Dictionary<string, Assembly> Loaded = new(StringComparer.OrdinalIgnoreCase);
        private static bool resolverInstalled;

        public static void InstallResolver() {
            lock (Sync) {
                if (resolverInstalled)
                    return;
                resolverInstalled = true;
                AppDomain.CurrentDomain.AssemblyResolve += Resolve;
            }
        }

        public static Assembly LoadAssembly(string simpleName) {
            Assembly existing = AppDomain.CurrentDomain.GetAssemblies()
                .FirstOrDefault(a => string.Equals(a.GetName().Name, simpleName, StringComparison.OrdinalIgnoreCase));
            if (existing != null)
                return existing;

            lock (Sync) {
                if (Loaded.TryGetValue(simpleName, out Assembly cached))
                    return cached;
                string resource = typeof(MobileMultiplayerModule).Assembly.GetManifestResourceNames()
                    .FirstOrDefault(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal) && n.EndsWith(simpleName + ".dll", StringComparison.OrdinalIgnoreCase));
                if (resource == null)
                    return null;
                using Stream stream = typeof(MobileMultiplayerModule).Assembly.GetManifestResourceStream(resource);
                if (stream == null)
                    return null;
                byte[] bytes = new byte[stream.Length];
                int offset = 0;
                while (offset < bytes.Length) {
                    int read = stream.Read(bytes, offset, bytes.Length - offset);
                    if (read <= 0)
                        break;
                    offset += read;
                }
                Assembly loaded = Assembly.Load(bytes);
                Loaded[simpleName] = loaded;
                return loaded;
            }
        }

        private static Assembly Resolve(object sender, ResolveEventArgs args) {
            string name;
            try { name = new AssemblyName(args.Name).Name; }
            catch { return null; }
            return string.IsNullOrWhiteSpace(name) ? null : LoadAssembly(name);
        }
    }
}
