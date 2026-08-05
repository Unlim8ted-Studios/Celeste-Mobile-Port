# Celeste Android WASM Port


This workspace builds an Android APK that runs the threaded Celeste WASM/Everest runtime inside GeckoView. The Android app hosts the packaged web runtime from `assets/www` on a local HTTP server with the isolation headers required for `SharedArrayBuffer`.


## Credits


- Mobile port and Android integration: Unlim8ted Studios, https://unlim8ted.com
- WASM base: Mercury Workshop's Webleste / Celeste WASM work, https://github.com/MercuryWorkshop/celeste-wasm
  - We (Unlim8ted) actually love Mozilla though... it makes the multi threads work.
- Android base attribution: LucyYuih, https://gamejolt.com/games/CelesteWASMAndroid/1043072
- Celeste is owned by Extremely OK Games, Ltd. This repository does not grant rights to the commercial game assets.


## Layout

- "CelesteAndroidApp/" - Android Gradle project for the GeckoView APK shell.
- "CelesteAndroidApp/app/src/main/" - Android application source, including "MainActivity", "LocalAssetServer", the manifest, and Android resources.
- "CelesteAndroidApp/assets/www/" - Browser-facing Celeste/Everest runtime packaged into the APK.
- "CelesteAndroidApp/assets/www/_framework/" - Threaded .NET WebAssembly framework output used by the game.
- "CelesteAndroidApp/assets/www/celeste/" - Packaged Celeste and Everest assemblies required by the runtime.
- "CelesteAndroidApp/assets/www/Mods/AndroidPort.zip" - Bundled Everest mod providing Android settings, touch hooks, haptics, and port-specific UI.
- "CelesteAndroidPatch/Source/" - C# source for the bundled Android Everest mod.
- "CelesteAndroidPatch/Source/Dialog/" - Everest dialog text used by the Android port mod.
- "CelesteAndroidPatch/build/" - Generated packaged mod contents, including "AndroidPort.dll", dialog files, and metadata.
- "CelesteAndroidPatch/CelesteAndroidPatch.zip" - Packaged copy of the Android Everest mod.
- "reference/celeste-wasm/" - Mercury Workshop Celeste WASM source used to rebuild the threaded loader and runtime.
- "reference/working/" - Extracted files and APK from the last known working Android build, retained for comparison and recovery.
- "reference/probably not working/" - Older diagnostic and experimental APK builds.
- "scripts/" - Build, packaging, and WebAssembly post-processing scripts.
- "tools/" - Local Android SDK, Gradle, .NET, APK-signing, and other development tooling. This directory is machine-local and excluded from version control.
- Root-level Java and C# helper files - Utilities used for APK reconstruction, signing, verification, and Everest inspection.


## Architecture


This port is a native Android shell around the Celeste/Everest WebAssembly runtime. The APK does not run Celeste as Android-native game code. Instead, it packages the threaded .NET WASM build, game data, web loader, and Android-specific Everest mod under `assets/www`, then serves those files to an embedded GeckoView instance from inside the app.

The primary motivation for this is portability. By keeping nearly all game logic inside the shared WebAssembly runtime, the same Celeste/Everest build can be hosted on multiple platforms with only a relatively small platform-specific shell. Android currently uses GeckoView as its host, while future ports (such as iOS) can reuse the same runtime with platform-specific implementations for input, storage, haptics, and other operating system services.

### Android APK Shell


The Android project in `geckoview-wrapper/` builds a single-activity APK:


- `MainActivity` creates a fullscreen, immersive, landscape-only activity.
- `MainActivity` starts `LocalAssetServer` on `127.0.0.1` using a random local port.
- `MainActivity` writes a GeckoView config file enabling shared memory and WASM threads.
- A `GeckoRuntime`, `GeckoSession`, and `GeckoView` are created in-process.
- GeckoView loads the local server root URL, which resolves to `assets/www/index.html`.


Android WebView is intentionally not used for gameplay because the runtime depends on the cross-origin isolated path required by threaded WASM and `SharedArrayBuffer`. GeckoView is embedded directly in the app, so gameplay stays in the Android process rather than launching an external browser.


### Local Asset Server


`LocalAssetServer.java` is a small HTTP server backed by Android packaged assets:


- It binds only to `127.0.0.1`, so the packaged runtime is exposed only to the local device.
- It serves files from the `www` asset root that Gradle maps from `assets/www`.
- It supports `GET` and `HEAD`.
- It sanitizes request paths and rejects traversal attempts.
- It applies the isolation headers required by the threaded runtime:
  - `Cross-Origin-Opener-Policy: same-origin`
  - `Cross-Origin-Embedder-Policy: require-corp`
  - `Cross-Origin-Resource-Policy: cross-origin`
  - `Permissions-Policy: cross-origin-isolated=*`
- It maps common web, image, text, and WASM MIME types.
- It reassembles split `dotnet.native.*.wasm` files at request time by streaming numbered asset chunks as one `application/wasm` response.
- It exposes a diagnostic `/__android_port_log` endpoint that lets JavaScript messages show up in Android logcat under `CelesteAssetServer`.


The split-WASM behavior exists because very large WASM files are easier to package and serve reliably as multiple asset chunks.


### Web Runtime


`assets/www` contains the browser-facing runtime:


- `index.html`, `styles.css`, and `bundle.js` provide the loader, UI shell, Android overlay controls, save manager, file manager entry points, and bridge functions.
- `_framework/` contains the threaded .NET WASM output produced from `upstream-celeste-wasm/loader`.
- `cfg.js` describes the packaged `data.data` payload.
- `celeste/`, `assets/`, and `plugins/` hold the game/runtime web assets.
- `Mods/AndroidPort.zip` is loaded by Everest as the bundled Android integration mod.


The loader keeps `/libsdl` memory-backed during startup. Saves and mods are persisted explicitly from the in-memory runtime filesystem into IndexedDB snapshots and restored during initialization. This avoids the GeckoView startup hangs that occurred when the full runtime tree was persisted through OPFS.


### WASM Loader Rebuild and Post-Processing


The threaded loader is rebuilt from `upstream-celeste-wasm/`. After publishing, `scripts/postprocess-framework.sh` copies the framework output and applies Android-specific runtime patches:


- Transfers the main `.canvas` to the threaded runtime path.
- Routes Emscripten main-thread assembly calls through `runMainThreadEmAsm`.
- Raises the runtime ULEB limit used by the loader.
- Splits large `dotnet.native.*.wasm` files into 20 MB numbered chunks.
- Copies the patched framework into `assets/www/_framework`.


### Everest Android Port Mod


`EverestAndroidPort/` is a C# Everest module packaged as `assets/www/Mods/AndroidPort.zip`. It adds Android-specific behavior inside Celeste:


- Registers an Android Port options menu.
- Syncs touch control, joystick, snap, and haptic settings into JavaScript.
- Adds menu buttons for layout editing, save export, file manager, reset, port info, and mod browser integration.
- Skips the initial title screen and launches into the main menu.
- Removes the fullscreen option from the in-game options menu because the Android shell owns fullscreen behavior.
- Maps touch taps and scroll gestures into the main menu, file select, chapter select, and text menus.
- Forwards Celeste rumble events to the JavaScript haptic bridge.
- Optionally recenters the camera around the player for mobile play.


`AndroidBridge.cs` uses .NET JavaScript imports to call functions defined by the web runtime. The bridge is defensive: calls are wrapped so the mod can still load in non-Android or desktop WASM environments where the Android JavaScript bridge is not present.


### JavaScript Bridge and Mobile UI


The web runtime exposes `window.celesteAndroid...` functions consumed by the Everest mod:


- Haptic feedback requests.
- URL prompts.
- Mod browser launch point.
- Save manager launch.
- File manager launch.
- Touch layout editor launch.
- Game data reset.
- Option synchronization.
- Touch tap, touch position, and scroll consumption in Celeste canvas coordinates.


The same JavaScript layer provides the mobile control overlay, joystick mode, 8-way snap, haptic setting persistence, layout presets, drag-and-resize editing, and reset behavior through `localStorage`.


### Build and Packaging Flow


The normal build flow is:


1. Build `EverestAndroidPort/AndroidPort.csproj`.
2. Package `metadata.yaml`, `Dialog/`, and `bin/AndroidPort.dll` into `AndroidPort.zip`.
3. Copy `AndroidPort.zip` into `assets/www/Mods/`.
4. Rebuild and post-process the threaded WASM framework when loader changes are needed.
5. Build the Android Gradle project.
6. Copy the debug APK to `celeste-fixed.apk`.


The helper scripts encode the common local steps:


- `scripts/package-android-port-mod.ps1` builds and packages the bundled Everest mod.
- `scripts/postprocess-framework.sh` patches and copies the WASM framework output.
- `scripts/build-apk.ps1` configures local Android/Gradle paths and builds `celeste-fixed.apk`.


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


## Runtime Notes


- Android WebView is not used for gameplay because it does not reliably provide the cross-origin isolated threaded WASM path this build requires.
- GeckoView is used in-app, not as an external browser window.
- `/libsdl` is intentionally memory-backed in the rebuilt loader. Persisting the entire runtime tree through OPFS caused GeckoView to hang during initialization.
- Saves and mods are persisted explicitly from JavaScript into IndexedDB snapshots and restored during initialization.


## Build


From `O:\celeste`:


```powershell
dotnet build EverestAndroidPort\AndroidPort.csproj -c Debug
```


Then rebuild `assets\www\Mods\AndroidPort.zip` from:


- `EverestAndroidPort\metadata.yaml`
- `EverestAndroidPort\Dialog`
- `EverestAndroidPort\bin\AndroidPort.dll`


To rebuild the threaded WASM loader from WSL:


```powershell
wsl.exe -e bash -lc 'cd /mnt/o/celeste/upstream-celeste-wasm; export PATH=/home/unlim8ted/.dotnet10:/home/unlim8ted/node16/bin:$PATH; export DOTNET_ROOT=/home/unlim8ted/.dotnet10; export DOTNET_ROLL_FORWARD=Major; export TMPDIR=/mnt/o/celeste/.tmp-wsl; export DOTNET_CLI_HOME=/mnt/o/celeste/.dotnet-cli-home; export NUGET_PACKAGES=/mnt/o/celeste/upstream-celeste-wasm/nuget; dotnet publish loader -c Release --nodereuse:false -v minimal'
wsl.exe -e bash -lc 'cd /mnt/o/celeste; bash scripts/postprocess-framework.sh'
```


To build the APK:


```powershell
$env:ANDROID_SDK_ROOT=(Resolve-Path '.android-sdk').Path
$env:ANDROID_HOME=$env:ANDROID_SDK_ROOT
$env:GRADLE_USER_HOME=(Resolve-Path '.gradle-user-home').Path
& '.gradle-home\gradle-8.11.1\bin\gradle.bat' -p geckoview-wrapper --no-daemon :app:assembleDebug
Copy-Item geckoview-wrapper\app\build\outputs\apk\debug\app-debug.apk celeste-fixed.apk -Force
```


## Testing


Install and launch:


```powershell adb.exe install -r celeste-fixed.apk
adb.exe shell am start -n lucyyuih.celeste.wasm/com.unlim8ted.celeste.MainActivity
```


Useful log filters:


```powershell
adb.exe logcat CelesteAssetServer:D GeckoConsole:D GeckoRuntime:D AndroidRuntime:E *:S
```