<div align="center">

# Celeste Android WASM Port

### Celeste + Everest on Android, powered by threaded .NET WebAssembly and GeckoView

An unofficial Android port focused on bringing a complete, mod-capable Celeste experience to mobile with customizable touch controls, haptics, save management, multiplayer support, and mobile-specific improvements.

**Developed by [Unlim8ted Studios](https://unlim8ted.com)**

</div>

---

> [!IMPORTANT]
> **Celeste game files are not distributed with this repository.**  
> Building a playable copy requires files from a legally obtained copy of Celeste.

> [!NOTE]
> **Project status: Active development.**  
> The Android host, threaded WASM runtime, touch interface, and supporting infrastructure are functional. Work is ongoing to complete compatibility with the modified Everest runtime and finish several mobile-specific features.

## Overview

Celeste Android WASM Port runs the threaded Celeste/Everest WebAssembly runtime inside an embedded **GeckoView** browser engine.

Rather than attempting to rewrite Celeste as native Android game code, the project keeps nearly all of the existing game and Everest runtime inside .NET WebAssembly. A relatively small Android host provides the platform-specific functionality needed for mobile, including:

- Touch controls
- Haptics
- Storage and save persistence
- File management
- Mod management
- Mobile UI integration
- Multiplayer hosting and joining
- Android lifecycle and fullscreen behavior

The web runtime is packaged under:

```text
CelesteAndroidApp/assets/www/
```

and served to GeckoView through a local HTTP server with the cross-origin isolation headers required for threaded WebAssembly and `SharedArrayBuffer`.

This architecture keeps the actual game runtime largely platform-independent while allowing Android-specific functionality to be built around it.

---

## Features

### 🎮 Touch Controls

- [x] Fully customizable touchscreen controls
- [x] Drag, resize, and reposition gameplay controls
- [x] Save and restore multiple control-layout presets
- [x] Built-in default mobile layout
- [x] Optional 8-way joystick snapping
- [x] Matching visual feedback for joystick snapping
- [x] Configure touch controls directly from Celeste's Options menu
- [x] Complete touch support across every game UI

### 📱 Mobile Improvements

- [x] Optional mobile camera-centering mode
- [x] Camera centering respects room boundaries
- [x] Camera centering automatically avoids interfering with cutscenes
- [x] Mobile-specific menu behavior
- [x] Android-specific quality-of-life improvements
- [x] Mobile-friendly startup behavior
- [x] Android-owned fullscreen handling

### 📳 Haptics

- [x] Android haptic feedback
- [x] Uses Celeste's existing rumble events
- [x] User-configurable haptic behavior
- [x] JavaScript-to-Android haptic bridge

### 💾 Saves & Files

- [x] Save persistence
- [x] IndexedDB-backed save snapshots
- [x] Save manager
- [x] File manager
- [x] Game-data reset functionality
- [x] Restore persistent data during runtime initialization

### 🧩 Mods & Everest

- [x] Everest mod support
- [x] Built-in mod browser
- [x] Mod installer
- [x] Mod persistence
- [x] Android-specific Everest integration
- [ ] Complete compatibility with the modified Everest runtime

### 🌐 Multiplayer

- [ ] Host multiplayer sessions directly from mobile
- [ ] Join multiplayer sessions directly from mobile
- [ ] Mobile-friendly multiplayer configuration
- [ ] Mobile-friendly multiplayer session UI

### 🗺️ Editing

- [ ] Improved in-game map editing
- [ ] Mobile-friendly map creation and modification tools

### ⚙️ Runtime

- [x] Threaded .NET WebAssembly
- [x] Embedded GeckoView runtime
- [x] Cross-origin-isolated local asset server
- [x] `SharedArrayBuffer` support
- [x] Split-WASM streaming
- [x] Explicit save and mod persistence
- [x] Android runtime diagnostics through logcat

---

## Architecture

At a high level:

```text
┌───────────────────────────────────────┐
│               Android                 │
│                                       │
│  MainActivity                         │
│       │                               │
│       ├────────► LocalAssetServer     │
│       │               │               │
│       │               ▼               │
│       │        assets/www/            │
│       │                               │
│       ▼                               │
│   GeckoView                           │
└───────┬───────────────────────────────┘
        │
        ▼
┌───────────────────────────────────────┐
│       Threaded .NET WASM Runtime      │
│                                       │
│          Celeste + Everest            │
│                                       │
│  MobileBridge / Everest integration   │
└───────────────────────────────────────┘
```

The APK does **not** run Celeste as Android-native game code.

Instead, Celeste and Everest remain inside the WebAssembly environment while Android provides the platform host around them.

This separation makes the runtime considerably more portable. The same core WASM environment can potentially be hosted on other platforms while replacing only the platform-specific services for things such as:

- Input
- Haptics
- Storage
- Window management
- Native UI
- File access
- Platform integrations

---

## Why GeckoView?

The threaded runtime requires browser functionality including:

```text
SharedArrayBuffer
WebAssembly threads
Cross-origin isolation
```

The Android application therefore embeds **Mozilla GeckoView** rather than relying on Android WebView.

GeckoView runs directly inside the application, so Celeste remains inside the Android process instead of opening in an external browser.

And yes, **we actually love Mozilla** — GeckoView is what makes the multithreaded runtime practical here. ❤️🦊

---

## Project Layout

```text
Celeste-Mobile-Port/
│
├── CelesteAndroidApp/
│   ├── app/
│   │   └── src/main/
│   │       ├── AndroidManifest.xml
│   │       ├── java/com/unlim8ted/celeste/
│   │       │   ├── MainActivity.java
│   │       │   └── LocalAssetServer.java
│   │       └── res/
│   │
│   ├── assets/
│   │   └── www/
│   │       ├── index.html
│   │       ├── bundle.js
│   │       ├── styles.css
│   │       ├── cfg.js
│   │       ├── _framework/
│   │       ├── celeste/
│   │       ├── assets/
│   │       ├── plugins/
│   │       └── Mods/
│   │
│   ├── gradle/
│   ├── gradlew
│   └── gradlew.bat
│
├── CelesteAndroidPatch/
│   ├── Source/
│   └── build/
│
└── scripts/
    ├── build-apk.ps1
    ├── package-android-port-mod.ps1
    └── postprocess-framework.sh
```

### Important Paths

| Path | Purpose |
|---|---|
| `CelesteAndroidApp/` | Android Gradle project and GeckoView host |
| `CelesteAndroidApp/app/src/main/` | Native Android application source |
| `CelesteAndroidApp/assets/www/` | Browser-facing Celeste/Everest runtime |
| `CelesteAndroidApp/assets/www/_framework/` | Threaded .NET WASM runtime |
| `CelesteAndroidApp/assets/www/celeste/` | Locally supplied game/runtime assemblies |
| `CelesteAndroidApp/assets/www/Mods/` | Everest mods bundled into the local build |
| `CelesteAndroidPatch/Source/` | Android-specific Everest integration source |
| `scripts/` | Build, packaging, and WASM post-processing scripts |

Generated builds, SDKs, signing keys, commercial game data, caches, and other machine-local files are intentionally excluded from version control.

---

# Android Host

## MainActivity

`MainActivity` provides the native Android shell.

It is responsible for:

- Creating a fullscreen immersive activity
- Enforcing landscape orientation
- Starting the local asset server
- Configuring GeckoView
- Creating the `GeckoRuntime`
- Creating the `GeckoSession`
- Attaching the session to `GeckoView`
- Loading the local runtime
- Managing Android-specific lifecycle behavior

The runtime is loaded from a loopback address similar to:

```text
http://127.0.0.1:<random-port>/
```

which resolves to the packaged:

```text
CelesteAndroidApp/assets/www/index.html
```

---

## Local Asset Server

`LocalAssetServer.java` is a lightweight HTTP server backed by Android packaged assets.

It:

- Binds exclusively to `127.0.0.1`
- Selects a local port at runtime
- Serves files from the packaged `www` runtime
- Supports `GET`
- Supports `HEAD`
- Sanitizes request paths
- Rejects directory traversal
- Maps required MIME types
- Streams large WASM binaries
- Provides Android-side diagnostics

### Cross-Origin Isolation

The server applies:

```http
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Resource-Policy: cross-origin
Permissions-Policy: cross-origin-isolated=*
```

These headers provide the isolated environment required by the threaded runtime.

### Runtime Logging

The diagnostic endpoint:

```text
/__android_port_log
```

allows JavaScript messages to appear in Android logcat under:

```text
CelesteAssetServer
```

---

# Web Runtime

`CelesteAndroidApp/assets/www/` contains the browser-facing portion of the application.

## Core Files

### `index.html`

Initial document and runtime bootstrap.

### `bundle.js`

Contains much of the mobile/browser integration layer:

- WASM loader behavior
- Mobile touch controls
- Layout editing
- Save management
- File management
- Mod browser
- Haptic bridge
- Android bridge functions
- Runtime persistence
- Mobile input behavior

### `styles.css`

Defines the web and mobile-control interface styling.

### `cfg.js`

Describes the locally supplied `data.data` payload.

### `_framework/`

Contains the threaded .NET WebAssembly runtime.

### `celeste/`

Contains locally supplied Celeste and Everest runtime assemblies.

### `Mods/`

Contains Everest mods included in the local build.

---

# Split-WASM Streaming

Some `dotnet.native.*.wasm` binaries are too large to be conveniently packaged as single Android assets.

The post-processing system divides them into numbered chunks:

```text
dotnet.native.example.wasm0
dotnet.native.example.wasm1
dotnet.native.example.wasm2
dotnet.native.example.wasm3
dotnet.native.example.wasm4
```

When GeckoView requests:

```text
dotnet.native.example.wasm
```

`LocalAssetServer` discovers the numbered parts and streams them sequentially as one:

```text
Content-Type: application/wasm
```

response.

To GeckoView and the .NET runtime, the result behaves like the original single WASM file.

---

# Persistence

The runtime intentionally keeps `/libsdl` memory-backed.

Persisting the complete runtime tree through OPFS caused GeckoView to hang during initialization, so persistent data is handled explicitly instead.

```text
Celeste runtime filesystem
          │
          ▼
   Selected save/mod data
          │
          ▼
      IndexedDB
          │
          ▼
   Application restart
          │
          ▼
Restore into runtime filesystem
```

This approach persists the data users actually need without forcing the entire game runtime into browser-backed persistent storage.

---

# Mobile / JavaScript Bridge

The web runtime exposes a set of:

```javascript
window.celesteAndroid...
```

functions that can be called by the Everest-side mobile integration.

The bridge provides access to services including:

- Haptic feedback
- Save manager
- File manager
- Mod browser
- Touch-layout editor
- URL handling
- Game-data reset
- Option synchronization
- Touch coordinates
- Pointer input
- Tap input
- Scroll input

Conceptually:

```text
Celeste / Everest
       │
       ▼
.NET JavaScript Interop
       │
       ▼
window.celesteAndroid...
       │
       ├──► Touch UI
       ├──► Haptics
       ├──► Storage
       ├──► Save Manager
       ├──► File Manager
       └──► Mod Browser
```

Bridge calls are designed to fail gracefully when the Android-specific JavaScript environment is unavailable.

---

# Modular Everest Components

The mobile functionality is being separated into smaller Everest modules instead of remaining one monolithic Android-specific mod.

## MouseUI

Generic mouse and pointer support for Celeste's UI.

Planned responsibilities include:

- Mouse-based menu navigation
- Clicking UI elements
- Pointer hover handling
- Scroll-wheel support
- Virtual pointer input for touchscreen platforms

`MouseUI` is intended to be useful independently of the Android port.

## BetterMapEditor

Improved map editing and creation tools.

The goal is to keep map-editing functionality independent from the mobile platform layer so it can also be useful on desktop installations.

## MobileMultiplayer

Mobile-specific multiplayer support.

Planned functionality includes:

- Hosting sessions from mobile
- Joining sessions from mobile
- Mobile-friendly connection configuration
- Mobile-oriented multiplayer UI

## MobileTweaks

Mobile-specific Celeste behavior and quality-of-life improvements.

Examples include:

- Camera centering
- Mobile menu adjustments
- Startup behavior
- Mobile option changes
- Other gameplay and UI tweaks

## MobileBridge

The low-level communication layer between Everest and the mobile/browser host.

Its responsibility is to expose platform capabilities rather than implement gameplay policy.

Examples include:

- Haptics
- Pointer information
- Touch events
- Platform detection
- Save manager requests
- File manager requests
- Mod-browser requests
- URL handling

The intended dependency model is roughly:

```text
                    MouseUI
                       ▲
                       │
                 MobileTweaks
                       │
                       ▼
                  MobileBridge
                       ▲
                       │
               MobileMultiplayer


              BetterMapEditor
                   independent
```

This keeps reusable features independent while allowing the Android port to combine them into a complete mobile experience.

---

# WASM Loader Rebuild

The threaded loader is based on Mercury Workshop's Celeste WASM work.

After publishing, Android-specific post-processing is performed by:

```text
scripts/postprocess-framework.sh
```

The post-processing stage:

- Transfers the main `.canvas` into the threaded runtime path
- Routes Emscripten main-thread assembly calls through `runMainThreadEmAsm`
- Raises the runtime ULEB limit
- Splits large `dotnet.native.*.wasm` binaries into 20 MB chunks
- Copies the resulting framework into:

```text
CelesteAndroidApp/assets/www/_framework/
```

---

# Build Pipeline

A complete local build roughly follows:

```text
Everest modules
      │
      ▼
Compile C#
      │
      ▼
Package mods
      │
      ▼
Build / prepare threaded WASM runtime
      │
      ▼
Post-process framework
      │
      ▼
Supply legally obtained Celeste game files
      │
      ▼
Gradle Android build
      │
      ▼
APK
```

---

# Prerequisites

Development may require:

- Android Studio
- Android SDK
- Compatible JDK
- Gradle 8.11.1 through the included wrapper
- .NET SDK
- Node.js
- WSL for rebuilding the WASM runtime
- A legally obtained copy of Celeste

The repository does not download or distribute commercial Celeste game files.

---

# Building

## 1. Build the Everest Integration

From the repository root:

```powershell
dotnet build .\CelesteAndroidPatch\Source\AndroidPort.csproj -c Debug
```

Package it with:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\package-android-port-mod.ps1
```

The generated mod package is copied into:

```text
CelesteAndroidApp\assets\www\Mods\
```

---

## 2. Rebuild the Threaded WASM Runtime

From a configured WSL environment inside the Celeste WASM source:

```bash
dotnet publish loader \
    -c Release \
    --nodereuse:false \
    -v minimal
```

Then run the Android post-processing stage from the repository root:

```powershell
wsl.exe -e bash -lc 'cd /mnt/o/celeste; bash scripts/postprocess-framework.sh'
```

The generated framework is placed under:

```text
CelesteAndroidApp/assets/www/_framework/
```

---

## 3. Build the Android APK

Enter the Android project:

```powershell
cd .\CelesteAndroidApp
```

Verify the Gradle wrapper:

```powershell
.\gradlew.bat --version
```

Expected version:

```text
Gradle 8.11.1
```

Build:

```powershell
.\gradlew.bat --no-daemon :app:assembleDebug
```

The debug APK is generated at:

```text
CelesteAndroidApp\app\build\outputs\apk\debug\app-debug.apk
```

Copy it to the repository root if desired:

```powershell
Copy-Item `
    .\app\build\outputs\apk\debug\app-debug.apk `
    ..\celeste-debug.apk `
    -Force
```

The complete build can also be run through:

```powershell
cd ..
powershell -ExecutionPolicy Bypass -File .\scripts\build-apk.ps1
```

---

# Testing

Install or update the development APK:

```powershell
adb install -r .\celeste-debug.apk
```

Launch it:

```powershell
adb shell am start -n com.unlim8ted.celeste/.MainActivity
```

## Logcat

Useful runtime logging:

```powershell
adb logcat CelesteAssetServer:D GeckoConsole:D GeckoRuntime:D AndroidRuntime:E *:S
```

For a clean test:

```powershell
adb logcat -c
adb shell am start -n com.unlim8ted.celeste/.MainActivity
adb logcat CelesteAssetServer:D GeckoConsole:D GeckoRuntime:D AndroidRuntime:E *:S
```

---

# Current Development Priorities

The main areas currently being worked on are:

1. **Everest compatibility**
   - Complete support for the modified threaded Everest runtime.

2. **Touch UI**
   - Extend pointer/touch interaction across all Celeste menus.

3. **Modularization**
   - Split the current Android-specific Everest functionality into `MouseUI`, `MobileBridge`, `MobileTweaks`, `MobileMultiplayer`, and related independent modules.

4. **Multiplayer**
   - Host sessions directly from Android.
   - Join sessions directly from Android.
   - Build a mobile-friendly session interface.

5. **Map editing**
   - Develop the standalone `BetterMapEditor` functionality.

6. **Mod ecosystem**
   - Continue improving browsing, installation, persistence, and management of Everest mods on mobile.

---

# Credits

This project would not exist without the work of several projects and communities.

### Unlim8ted Studios

Android port, mobile integration, UI, platform bridge, and project-specific development.

https://unlim8ted.com

### Mercury Workshop

Webleste / Celeste WASM, which provides the WebAssembly foundation used by this project.

https://github.com/MercuryWorkshop/celeste-wasm

### LucyYuih

Prior Android Celeste WASM work used as part of the Android-port foundation.

https://gamejolt.com/games/CelesteWASMAndroid/1043072


### Extremely OK Games

Creators and rights holders of Celeste.

---

# License & Attribution

This is an unofficial community project created by **Unlim8ted Studios**.

Original Android integration code and other original contributions by Unlim8ted Studios are shared publicly as part of this project. Contributions, testing, issue reports, and improvements are welcome.

This project builds upon or interfaces with third-party software including:

- Celeste WASM / Webleste
- Everest
- GeckoView
- Prior Android Celeste WASM work

Third-party components remain subject to their respective copyright notices and licenses.

**Celeste and its associated names, characters, artwork, audio, game code, and other intellectual property belong to their respective rights holders.**

Commercial Celeste game data, including `data.data`, is **not included** in this repository and must be supplied from a legally obtained copy of Celeste.

This project is not affiliated with, sponsored by, or endorsed by Extremely OK Games.

---

<div align="center">

### Made with ❤️,
**Unlim8ted Studios**

</div>