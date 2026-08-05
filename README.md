# Celeste Android WASM Port

This workspace builds an Android APK that runs the threaded Celeste WASM/Everest runtime inside GeckoView. The Android application hosts the packaged web runtime from `CelesteAndroidApp/assets/www/` on a local HTTP server with the isolation headers required for `SharedArrayBuffer`.

## Credits

- Mobile port and Android integration: Unlim8ted Studios, https://unlim8ted.com
- WASM base: Mercury Workshop's Webleste / Celeste WASM work, https://github.com/MercuryWorkshop/celeste-wasm
  - GeckoView is used because it supports the threaded WASM and cross-origin-isolation requirements of the port.
- Android base attribution: LucyYuih, https://gamejolt.com/games/CelesteWASMAndroid/1043072
- Celeste is owned by Extremely OK Games, Ltd. This repository does not grant rights to the commercial game assets.

Commercial Celeste game files are not included in this repository. Building a playable APK requires files from a legally obtained copy of Celeste.

## Layout

- `CelesteAndroidApp/` - Android Gradle project for the GeckoView APK shell.
- `CelesteAndroidApp/app/src/main/` - Android application source, including `MainActivity`, `LocalAssetServer`, the manifest, and Android resources.
- `CelesteAndroidApp/assets/www/` - Browser-facing Celeste/Everest runtime packaged into the APK.
- `CelesteAndroidApp/assets/www/_framework/` - Threaded .NET WebAssembly framework output used by the game.
- `CelesteAndroidApp/assets/www/celeste/` - Packaged Celeste and Everest assemblies required by the runtime.
- `CelesteAndroidApp/assets/www/Mods/AndroidPort.zip` - Bundled Everest mod providing Android settings, touch hooks, haptics, and port-specific UI.
- `CelesteAndroidPatch/Source/` - C# source for the bundled Android Everest mod.
- `CelesteAndroidPatch/Source/Dialog/` - Everest dialog text used by the Android port mod.
- `CelesteAndroidPatch/build/` - Generated packaged mod contents, including `AndroidPort.dll`, dialog files, and metadata.
- `CelesteAndroidPatch/CelesteAndroidPatch.zip` - Packaged copy of the Android Everest mod.
- `scripts/` - Build, packaging, and WebAssembly post-processing scripts.

## Architecture

This port is a native Android shell around the Celeste/Everest WebAssembly runtime. The APK does not run Celeste as Android-native game code. Instead, it packages the threaded .NET WASM build, game data, web loader, and Android-specific Everest mod under `CelesteAndroidApp/assets/www/`, then serves those files to an embedded GeckoView instance from inside the application.

The primary motivation for this architecture is portability. By keeping nearly all game logic inside the shared WebAssembly runtime, the same Celeste/Everest build can be hosted on multiple platforms with a relatively small platform-specific shell. Android currently uses GeckoView as its host, while future ports can reuse the same runtime with platform-specific implementations for input, storage, haptics, and other operating-system services.

### Android APK Shell

The Android project in `CelesteAndroidApp/` builds a single-activity APK:

- `MainActivity` creates a fullscreen, immersive, landscape-only activity.
- `MainActivity` starts `LocalAssetServer` on `127.0.0.1` using a randomly selected local port.
- `MainActivity` writes a GeckoView configuration file enabling shared memory and WASM threads.
- A `GeckoRuntime`, `GeckoSession`, and `GeckoView` are created in-process.
- GeckoView loads the local server root URL, which resolves to `CelesteAndroidApp/assets/www/index.html`.

Android WebView is intentionally not used for gameplay because this runtime depends on the cross-origin-isolated environment required by threaded WASM and `SharedArrayBuffer`. GeckoView is embedded directly in the application, so gameplay stays inside the Android process rather than launching an external browser.

### Local Asset Server

`LocalAssetServer.java` is a small HTTP server backed by Android packaged assets:

- It binds only to `127.0.0.1`, so the packaged runtime is exposed only to the local device.
- It serves files from the `www` asset root that Gradle maps from `CelesteAndroidApp/assets/www/`.
- It supports `GET` and `HEAD` requests.
- It sanitizes request paths and rejects traversal attempts.
- It applies the isolation headers required by the threaded runtime:
  - `Cross-Origin-Opener-Policy: same-origin`
  - `Cross-Origin-Embedder-Policy: require-corp`
  - `Cross-Origin-Resource-Policy: cross-origin`
  - `Permissions-Policy: cross-origin-isolated=*`
- It maps common web, image, text, and WASM MIME types.
- It reassembles split `dotnet.native.*.wasm` files at request time by streaming numbered asset chunks as one `application/wasm` response.
- It exposes a diagnostic `/__android_port_log` endpoint that allows JavaScript messages to appear in Android logcat under `CelesteAssetServer`.

The split-WASM behavior exists because very large WASM files are easier to package and serve reliably as multiple asset chunks.

### Web Runtime

`CelesteAndroidApp/assets/www/` contains the browser-facing runtime:

- `index.html`, `styles.css`, and `bundle.js` provide the loader, UI shell, Android overlay controls, save manager, file manager entry points, and bridge functions.
- `_framework/` contains the threaded .NET WASM output produced by the Celeste WASM loader build.
- `cfg.js` describes the packaged `data.data` payload.
- `celeste/`, `assets/`, and `plugins/` contain the game and runtime web assets.
- `Mods/AndroidPort.zip` is loaded by Everest as the bundled Android integration mod.

The loader keeps `/libsdl` memory-backed during startup. Saves and mods are persisted explicitly from the in-memory runtime filesystem into IndexedDB snapshots and restored during initialization. This avoids the GeckoView startup hangs that occurred when the complete runtime tree was persisted through OPFS.

### WASM Loader Rebuild and Post-Processing

The threaded loader is rebuilt from Mercury Workshop's Celeste WASM source. After publishing, `scripts/postprocess-framework.sh` copies the framework output and applies Android-specific runtime patches:

- Transfers the main `.canvas` to the threaded runtime path.
- Routes Emscripten main-thread assembly calls through `runMainThreadEmAsm`.
- Raises the runtime ULEB limit used by the loader.
- Splits large `dotnet.native.*.wasm` files into numbered 20 MB chunks.
- Copies the patched framework into `CelesteAndroidApp/assets/www/_framework/`.

### Everest Android Port Mod

`CelesteAndroidPatch/Source/` contains a C# Everest module packaged as `CelesteAndroidApp/assets/www/Mods/AndroidPort.zip`. It adds Android-specific behavior inside Celeste:

- Registers an Android Port options menu.
- Synchronizes touch control, joystick, snap, and haptic settings with JavaScript.
- Adds menu buttons for layout editing, save export, file management, reset, port information, and mod browser integration.
- Skips the initial title screen and launches into the main menu.
- Removes the fullscreen option from the in-game options menu because the Android shell owns fullscreen behavior.
- Maps touch taps and scroll gestures into the main menu, file select, chapter select, and text menus.
- Forwards Celeste rumble events to the JavaScript haptic bridge.
- Optionally recenters the camera around the player for mobile play.

`AndroidBridge.cs` uses .NET JavaScript imports to call functions defined by the web runtime. The bridge is defensive: calls are wrapped so the mod can still load in non-Android or desktop WASM environments where the Android JavaScript bridge is unavailable.

### JavaScript Bridge and Mobile UI

The web runtime exposes `window.celesteAndroid...` functions consumed by the Everest mod:

- Haptic feedback requests.
- URL prompts.
- Mod browser launch.
- Save manager launch.
- File manager launch.
- Touch layout editor launch.
- Game data reset.
- Option synchronization.
- Touch tap, touch position, and scroll consumption in Celeste canvas coordinates.

The same JavaScript layer provides the mobile control overlay, joystick mode, optional 8-way snapping, haptic setting persistence, layout presets, drag-and-resize editing, and reset behavior through `localStorage`.

### Build and Packaging Flow

The normal build flow is:

1. Build `CelesteAndroidPatch/Source/AndroidPort.csproj`.
2. Package `metadata.yaml`, `Dialog/`, and `bin/AndroidPort.dll` into `AndroidPort.zip`.
3. Copy `AndroidPort.zip` into `CelesteAndroidApp/assets/www/Mods/`.
4. Rebuild and post-process the threaded WASM framework when loader changes are needed.
5. Build the Android Gradle project in `CelesteAndroidApp/`.
6. Copy the debug APK to `celeste-fixed.apk`.

The helper scripts encode the common local steps:

- `scripts/package-android-port-mod.ps1` builds and packages the bundled Everest mod.
- `scripts/postprocess-framework.sh` patches and copies the WASM framework output.
- `scripts/build-apk.ps1` builds the Android APK and copies the result to `celeste-fixed.apk`.

## Features

- [x] Optional camera-centering mode designed specifically for touchscreen play while respecting room boundaries and cutscenes.
- [x] Uses Celeste's existing rumble events to drive Android haptic feedback.
- [ ] Touch support across the entire game UI.
- [x] Fully customizable touchscreen controls.
- [x] Drag, resize, and reposition every gameplay control.
- [x] Save and restore multiple control layout presets.
- [x] Built-in default layout preset.
- [x] Optional 8-way joystick snapping with matching visual feedback.
- [x] Touch controls configurable directly from the in-game Options menu.
- [x] Numerous additional mobile-specific quality-of-life improvements.
- [ ] Built-in map editing tools for creating and modifying Celeste maps.
- [x] Built-in mod browser and installer.
- [ ] actually get Everest to run with the changes.

## Runtime Notes

- Android WebView is not used for gameplay because this build requires a cross-origin-isolated threaded WASM environment.
- GeckoView is used in-process rather than as an external browser window.
- `/libsdl` is intentionally memory-backed in the rebuilt loader. Persisting the complete runtime tree through OPFS caused GeckoView to hang during initialization.
- Saves and mods are persisted explicitly from JavaScript into IndexedDB snapshots and restored during initialization.
- Large `dotnet.native.*.wasm` files are stored as numbered chunks and streamed as a single WASM response by `LocalAssetServer`.
- Commercial Celeste game files must be supplied locally from a legally obtained copy of the game.

## Build

Run all PowerShell commands from:

```text
O:\celeste
```

### Build and Package the Everest Mod

Build the Android Everest mod:

```powershell
dotnet build .\CelesteAndroidPatch\Source\AndroidPort.csproj -c Debug
```

Package the mod with the provided script:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-android-port-mod.ps1
```

The resulting mod package should be copied to:

```text
CelesteAndroidApp\assets\www\Mods\AndroidPort.zip
```

The package contains:

- `CelesteAndroidPatch\Source\metadata.yaml`
- `CelesteAndroidPatch\Source\Dialog\`
- `CelesteAndroidPatch\Source\bin\AndroidPort.dll`

### Rebuild the Threaded WASM Loader

Run the following inside WSL from the root of a local Celeste WASM source checkout:

```bash
export PATH=/home/unlim8ted/.dotnet10:/home/unlim8ted/node16/bin:$PATH
export DOTNET_ROOT=/home/unlim8ted/.dotnet10
export DOTNET_ROLL_FORWARD=Major
export TMPDIR=/mnt/o/celeste/.tmp-wsl
export DOTNET_CLI_HOME=/mnt/o/celeste/.dotnet-cli-home
export NUGET_PACKAGES="$PWD/nuget"

dotnet publish loader \
    -c Release \
    --nodereuse:false \
    -v minimal
```

After publishing, run the post-processing script from the project root:

```powershell
wsl.exe -e bash -lc 'cd /mnt/o/celeste; bash scripts/postprocess-framework.sh'
```

The post-processed runtime is copied into:

```text
CelesteAndroidApp\assets\www\_framework
```

### Build the Android APK

Enter the Android Gradle project:

```powershell
cd O:\celeste\CelesteAndroidApp
```

Confirm that the Gradle wrapper is using Gradle 8.11.1:

```powershell
.\gradlew.bat --version
```

Build the debug APK:

```powershell
.\gradlew.bat --no-daemon :app:assembleDebug
```

Copy the APK to the workspace root:

```powershell
Copy-Item `
    .\app\build\outputs\apk\debug\app-debug.apk `
    ..\celeste-fixed.apk `
    -Force
```

The final APK will be located at:

```text
O:\celeste\celeste-fixed.apk
```

The complete build can also be run through the helper script:

```powershell
cd O:\celeste
powershell -ExecutionPolicy Bypass -File .\scripts\build-apk.ps1
```

## Testing

Install or update the APK:

```powershell
cd O:\celeste
adb.exe install -r .\celeste-fixed.apk
```

Launch the application:

```powershell
adb.exe shell am start -n com.unlim8ted.celeste/.MainActivity
```

View the most useful log output:

```powershell
adb.exe logcat CelesteAssetServer:D GeckoConsole:D GeckoRuntime:D AndroidRuntime:E *:S
```

To clear existing logs before launching:

```powershell
adb.exe logcat -c
adb.exe shell am start -n lucyyuih.celeste.wasm/com.unlim8ted.celeste.MainActivity
adb.exe logcat CelesteAssetServer:D GeckoConsole:D GeckoRuntime:D AndroidRuntime:E *:S
```

License and Attribution

This is an unofficial community project created by Unlim8ted Studios.

Original Android integration code and other original contributions by Unlim8ted Studios are shared as part of this project. Contributions, issue reports, and improvements are welcome.

This project builds upon or interfaces with Celeste WASM/Webleste, Everest, GeckoView, and other third-party software. Those components remain subject to their respective licenses and copyright notices.

Celeste and its associated intellectual property belong to their respective rights holders. Commercial Celeste game data, including "data.data", is not included and must be supplied from a legally obtained copy of the game.

This project is not affiliated with or endorsed by Extremely OK Games.
