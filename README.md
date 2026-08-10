<div align="center">

<h1>Celeste Mobile Port</h1>

<p><strong>Celeste + Everest on mobile, powered by threaded .NET WebAssembly and platform-specific wrappers</strong></p>

<p>
An unofficial mobile port focused on bringing a complete, mod-capable Celeste experience to phones and tablets with customizable touch controls, haptics, save management, multiplayer, map creation, and mobile-specific UI improvements.
</p>

<p>
Developed by <a href="https://unlim8ted.com">Unlim8ted Studios</a>
</p>

</div>

---

> [!IMPORTANT]
> Celeste game files are not distributed with this repository.
> Building a playable copy requires files from a legally obtained copy of Celeste.

> [!NOTE]
> **Project status: Active development.**
> The threaded Celeste/Everest WebAssembly runtime, Android GeckoView host, touch-control system, save/mod persistence, and modular mobile integration are functional. Multiplayer hosting, the simplified in-game map editor, iOS support, and some modified-Everest compatibility work are still under active development.

## Table of Contents

- [Overview](#overview)
- [Features](#features)
  - [Touch Controls](#-touch-controls)
  - [Mobile Improvements](#-mobile-improvements)
  - [Haptics](#-haptics)
  - [Saves & Files](#-saves--files)
  - [Mods & Everest](#-mods--everest)
  - [Multiplayer](#-multiplayer)
  - [Map Editing](#️-map-editing)
  - [Runtime](#️-runtime)
- [User Interface](#user-interface)
  - [Main Menu](#main-menu)
  - [Options](#options)
- [Architecture](#architecture)
- [Repository Layout](#repository-layout)
- [Platform Wrappers](#platform-wrappers)
  - [Android Wrapper](#android-wrapper)
  - [iOS Wrapper](#ios-wrapper)
- [Web Runtime](#web-runtime)
  - [Core Files](#core-files)
  - [Local Asset Server](#local-asset-server)
  - [Cross-Origin Isolation](#cross-origin-isolation)
  - [Split-WASM Streaming](#split-wasm-streaming)
- [Persistence](#persistence)
- [Modular Everest Components](#modular-everest-components)
  - [MobileBridge](#mobilebridge)
  - [MouseUI](#mouseui)
  - [MobileTweaks](#mobiletweaks)
  - [MobileMultiplayer](#mobilemultiplayer)
  - [BetterMapEditor](#bettermapeditor)
  - [Module Relationships](#module-relationships)
- [Multiplayer Architecture](#multiplayer-architecture)
- [Map Editor Architecture](#map-editor-architecture)
- [WASM Loader Rebuild](#wasm-loader-rebuild)
- [Building Mobile Mods](#building-mobile-mods)
  - [Automatic Mod Detection](#automatic-mod-detection)
  - [CelesteNet](#celestenet)
  - [Mod Package Layout](#mod-package-layout)
- [Build Pipeline](#build-pipeline)
- [Prerequisites](#prerequisites)
- [Building the Everest Mods](#building-the-everest-mods)
- [Building the Android Runtime](#building-the-android-runtime)
- [Testing](#testing)
  - [Desktop Everest Testing](#desktop-everest-testing)
  - [Android Testing](#android-testing)
- [Current Development Priorities](#current-development-priorities)
- [Credits](#credits)
- [License & Attribution](#license--attribution)

---

## Overview

Celeste Mobile Port runs Celeste and Everest inside a threaded .NET WebAssembly environment and surrounds that runtime with a comparatively small platform-specific host.

The Android version currently uses Mozilla GeckoView as its embedded browser engine.

Rather than attempting to rewrite Celeste as native Android or iOS game code, the project keeps nearly all game logic, Everest, mods, and FNA-compatible runtime behavior inside the shared WASM environment.

Platform wrappers provide the functionality that actually needs to be native, including:

- Touch controls
- Haptics
- Save import/export
- File access
- Mod management
- Multiplayer host services
- Platform lifecycle handling
- Native browser/runtime integration
- Platform-specific fullscreen and display behavior

The architecture is intentionally split into four major parts:

```text
Celeste-Mobile-Port/
│
├── AndroidWrapper/
├── IOSWrapper/
├── CelesteRuntime/
└── CelesteMobileMods/
```

This keeps the game runtime largely platform-independent.

The goal is for Android and iOS to share the same Celeste/Everest runtime and mobile mods while replacing only the native wrapper layer.

---

## Features

### 🎮 Touch Controls

- [x] Fully customizable touchscreen controls
- [x] Drag, resize, and reposition gameplay controls
- [x] Save and restore control layouts
- [x] Built-in default mobile layout
- [x] Joystick movement mode
- [x] Arrow-button movement mode
- [x] Optional 8-way joystick snapping
- [x] Matching joystick snapping behavior
- [x] In-game control layout editor
- [x] Touch interaction with Celeste menus through `MouseUI`
- [x] Mouse support independent of the mobile platform
- [x] Touch support automatically enabled when `MobileBridge` is available

### 📱 Mobile Improvements

- [x] Optional player-centered camera mode
- [x] Camera centering respects room boundaries
- [x] Camera centering avoids cutscenes and transitions
- [x] Mobile-specific menu organization
- [x] Mobile-friendly main menu
- [x] Mobile-friendly Options integration
- [x] Skip the normal introductory startup flow
- [x] Automatically enter the main menu
- [x] Hide browser-inappropriate settings such as Fullscreen
- [x] Move mobile settings out of Everest Mod Options and into normal Celeste Options
- [x] Preserve normal Mod Options for unrelated third-party mods
- [x] Move overflowing main-menu additions into columns to the right

### 📳 Haptics

- [x] Native mobile haptic feedback
- [x] Uses Celeste's existing rumble events
- [x] User-configurable haptics
- [x] JavaScript/native bridge
- [x] Configurable directly from Celeste's normal Options menu

### 💾 Saves & Files

- [x] Persistent saves
- [x] IndexedDB-backed runtime persistence
- [x] Save manager
- [x] Save export
- [x] Save import
- [x] File manager
- [x] Mod persistence
- [x] Restore persistent data during runtime initialization

### 🧩 Mods & Everest

- [x] Everest mod support
- [x] Built-in mod browser
- [x] Mod installation
- [x] Mod persistence
- [x] Mobile-specific Everest integration
- [x] Modular mobile Everest components
- [x] Third-party Mod Options remain available normally
- [ ] Complete compatibility with every modified Everest/runtime feature

### 🌐 Multiplayer

Multiplayer is built around CelesteNet rather than implementing a separate networking protocol.

- [x] Dedicated Multiplayer entry on the main menu
- [x] CelesteNet settings integrated into `Options → Multiplayer`
- [x] CelesteNet's duplicate Mod Options section hidden while `MobileMultiplayer` is active
- [x] Configurable username
- [x] Official CelesteNet server support
- [x] Saved/custom server support
- [x] Add custom servers
- [x] Remove custom servers
- [x] LAN server discovery on supported platforms
- [x] Wrapper-provided server discovery
- [x] Join sessions from the mobile-friendly UI
- [x] Disconnect from the current server
- [ ] Native mobile CelesteNet host service
- [ ] Final Android multiplayer-host integration
- [ ] Final iOS multiplayer-host integration

### 🗺️ Map Editing

`BetterMapEditor` provides a simplified in-game map-development environment inspired by Lönn and Ahorn.

- [x] Map Editor entry on the main menu
- [x] Create new Everest map mods
- [x] Multiple chapters per project
- [x] Multiple rooms per chapter
- [x] Room sidebar
- [x] Add rooms
- [x] Delete rooms
- [x] Switch rooms directly inside the editor
- [x] Place/remove solid tiles
- [x] Place player spawns
- [x] Basic entity placement
- [x] Strawberry placement
- [x] Spring placement
- [x] Spike placement
- [x] Entity selection
- [x] Entity movement
- [x] Entity deletion
- [x] Pan
- [x] Zoom
- [x] Undo/redo
- [x] Resize rooms
- [x] Save projects
- [x] Build real Celeste map binaries
- [x] Mouse input
- [x] Optional `MobileBridge` touch input
- [ ] Expanded entity catalog
- [ ] Trigger editing
- [ ] Decal editing
- [ ] Styleground editing
- [ ] Advanced room metadata
- [ ] More Lönn/Ahorn-style editing tools

### ⚙️ Runtime

- [x] Threaded .NET WebAssembly
- [x] Embedded GeckoView runtime on Android
- [x] Cross-origin-isolated local asset server
- [x] `SharedArrayBuffer`
- [x] WebAssembly threads
- [x] Split-WASM streaming
- [x] Explicit save persistence
- [x] Explicit mod persistence
- [x] Android runtime diagnostics through logcat
- [ ] iOS wrapper completion

---

## User Interface

The mobile integration reorganizes Celeste's menus rather than adding a large collection of unrelated Everest Mod Options.

### Main Menu

The intended mobile home screen is:

```text
Climb
Multiplayer
Map Editor
Mod Manager
Options
About the Port
```

Additional third-party mods are still allowed to add main-menu entries.

If those additions overflow the available vertical space, the menu switches to a multi-column layout and places additional items to the right.

### Options

Mobile settings are integrated into Celeste's normal Options screen.

Conceptually:

```text
Options
│
├── [normal Celeste settings]
│
├── Mobile
│   ├── Haptics
│   ├── Center Camera
│   │
│   └── Mobile Controls
│       ├── Movement
│       │   ├── Joystick
│       │   └── Arrows
│       ├── 8-Way Snap
│       └── Resize / Move Controls
│
├── Multiplayer
│   ├── Host
│   ├── Join
│   ├── Username
│   └── CelesteNet settings
│
├── Saves
│   ├── Export Save
│   └── Load Save
│
└── Mods
    └── Mod Options
```

The normal Mod Options menu is intentionally preserved.

Only settings belonging to the mobile suite are relocated.

Third-party Everest mods continue to expose their settings through Mod Options normally.

---

## Architecture

At a high level:

```text
┌────────────────────────────────────────────┐
│              Platform Wrapper              │
│                                            │
│   AndroidWrapper         IOSWrapper        │
│         │                     │            │
│         └──────────┬──────────┘            │
│                    │                       │
│        Platform-specific services          │
│                    │                       │
└────────────────────┼───────────────────────┘
                     │
                     ▼
┌────────────────────────────────────────────┐
│                 CelesteRuntime/                   │
│                                            │
│       Threaded .NET WASM Runtime           │
│                                            │
│           Celeste + Everest                │
│                                            │
│        Web / JavaScript runtime            │
└────────────────────┬───────────────────────┘
                     │
                     ▼
┌────────────────────────────────────────────┐
│            CelesteMobileMods/              │
│                                            │
│  MobileBridge                              │
│  MobileTweaks                              │
│  MouseUI                                   │
│  MobileMultiplayer                         │
│  BetterMapEditor                           │
│  ...third-party Everest mods               │
└────────────────────────────────────────────┘
```

Celeste is not being rewritten as native mobile game code.

The shared runtime remains inside WebAssembly.

Platform wrappers provide services around it.

This makes the architecture significantly more portable than tying the game runtime directly to Android APIs.

---

## Repository Layout

```text
Celeste-Mobile-Port/
│
├── AndroidWrapper/
│   ├── Android application
│   ├── GeckoView host
│   ├── local asset server
│   ├── Android lifecycle integration
│   ├── native haptics
│   ├── platform storage
│   └── Android-specific bridge services
│
├── IOSWrapper/
│   ├── iOS application
│   ├── embedded web-runtime host
│   ├── native iOS platform services
│   └── iOS-specific bridge implementation
│
├── CelesteRuntime/
│   ├── index.html
│   ├── bundle.js
│   ├── styles.css
│   ├── cfg.js
│   ├── _framework/
│   ├── celeste/
│   ├── assets/
│   ├── plugins/
│   └── Mods/
│
└── CelesteMobileMods/
    │
    ├── MobileBridge/
    │   ├── MobileBridge.cs
    │   ├── MobileBridge.csproj
    │   ├── everest.yaml
    │   └── Dialog/
    │
    ├── MobileTweaks/
    │   ├── MobileTweaks.cs
    │   ├── MobileTweaks.csproj
    │   └── everest.yaml
    │
    ├── MouseUI/
    │   ├── MouseUI.cs
    │   ├── MouseUI.csproj
    │   └── everest.yaml
    │
    ├── MobileMultiplayer/
    │   ├── MobileMultiplayer.cs
    │   ├── MobileMultiplayer.csproj
    │   ├── everest.yaml
    │   └── Dialog/
    │
    ├── BetterMapEditor/
    │   ├── BetterMapEditor.cs
    │   ├── BetterMapEditor.csproj
    │   ├── everest.yaml
    │   └── Dialog/
    │
    └── BuildDeployMods.ps1
```

Generated builds, signing keys, commercial game data, SDKs, caches, local toolchains, and other machine-specific files are intentionally excluded from version control.

---

## Platform Wrappers

### Android Wrapper

Android currently provides the primary mobile host.

#### GeckoView

The Android application embeds Mozilla GeckoView.

The threaded runtime requires:

- `SharedArrayBuffer`
- WebAssembly threads
- Cross-origin isolation

GeckoView provides the browser/runtime capabilities needed to run the threaded Celeste WASM environment while remaining embedded directly inside the Android application.

Celeste therefore runs inside the application rather than opening in an external browser.

And yes, we actually love Mozilla — GeckoView is what makes the multithreaded runtime practical here. ❤️🦊

#### Native Responsibilities

The Android wrapper is responsible for services such as:

- Creating the application window
- Landscape orientation
- Immersive/fullscreen Android behavior
- Creating GeckoView
- Creating the Gecko runtime/session
- Hosting the local runtime
- Native lifecycle handling
- Native haptics
- Storage integration
- Save import/export
- File access
- Mod-management support
- Multiplayer host services
- Native bridge functionality

### iOS Wrapper

`IOSWrapper/` provides the corresponding platform layer for iOS.

The goal is not to maintain a separate iOS Celeste implementation.

Instead, iOS should host the same:

```text
CelesteRuntime/
CelesteMobileMods/
```

runtime used by Android while implementing equivalent native platform services.

Areas of work include:

- Embedded browser/runtime hosting
- Touch integration
- Storage
- Haptics
- Save import/export
- File access
- Mod persistence
- Multiplayer host services
- Lifecycle handling

iOS support is currently less complete than Android.

---

## Web Runtime

`CelesteRuntime/` contains the browser-facing runtime shared by the platform wrappers.

### Core Files

#### `index.html`

Initial document and runtime bootstrap.

#### `bundle.js`

Contains browser/mobile integration including:

- WASM loader behavior
- Touch controls
- Layout editing
- Save management
- File management
- Mod browser
- Haptic bridge
- Platform bridge functions
- Runtime persistence
- Mobile input behavior

#### `styles.css`

Defines web and touch-control interface styling.

#### `cfg.js`

Describes the locally supplied game-data payload.

#### `_framework/`

Contains the threaded .NET WebAssembly runtime.

#### `celeste/`

Contains locally supplied Celeste/Everest runtime assemblies.

#### `Mods/`

Contains Everest mods available to the runtime.

### Local Asset Server

The Android wrapper includes a lightweight HTTP server backed by packaged/runtime assets.

It:

- Binds only to loopback
- Selects a local port dynamically
- Serves the `CelesteRuntime/` runtime
- Supports `GET`
- Supports `HEAD`
- Sanitizes request paths
- Rejects directory traversal
- Maps required MIME types
- Streams large WASM binaries
- Provides platform-side diagnostics

The runtime is loaded from an address similar to:

```text
http://127.0.0.1:<random-port>/
```

### Cross-Origin Isolation

The local server provides headers such as:

```http
Cross-Origin-Opener-Policy: same-origin
Cross-Origin-Embedder-Policy: require-corp
Cross-Origin-Resource-Policy: cross-origin
Permissions-Policy: cross-origin-isolated=*
```

These provide the cross-origin isolation required by the threaded WebAssembly runtime and `SharedArrayBuffer`.

### Split-WASM Streaming

Some `dotnet.native.*.wasm` binaries are too large to package conveniently as single platform assets.

The post-processing system can split them into numbered chunks:

```text
dotnet.native.example.wasm0
dotnet.native.example.wasm1
dotnet.native.example.wasm2
dotnet.native.example.wasm3
dotnet.native.example.wasm4
```

When the browser requests:

```text
dotnet.native.example.wasm
```

the local host streams the numbered pieces sequentially as one response with:

```http
Content-Type: application/wasm
```

To GeckoView and the .NET runtime, it behaves like the original single WASM binary.

---

## Persistence

The runtime intentionally avoids persisting the entire virtual runtime filesystem.

Persisting the whole runtime through browser-backed storage caused initialization problems, so only user-relevant data is persisted explicitly.

Conceptually:

```text
Celeste runtime filesystem
          │
          ▼
   Selected persistent data
          │
          ├── Saves
          ├── Everest settings
          ├── Mods
          └── Mobile data
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

This keeps persistence focused on data the user actually needs.

---

## Modular Everest Components

Mobile functionality is split into independent Everest modules instead of one larger Android-specific mod.

### MobileBridge

`MobileBridge` is the low-level communication layer between Everest and the platform/browser host.

It is intentionally separated from higher-level gameplay/UI policy.

Examples of bridge services include:

- Haptics
- Platform detection
- Touch coordinates
- Tap events
- Swipe/scroll events
- Save manager requests
- Save export
- Save import
- File manager requests
- Mod browser requests
- Touch-layout editor requests
- URL handling
- Gameplay-control configuration
- Multiplayer server discovery
- Multiplayer host-service requests

Conceptually:

```text
Celeste / Everest
       │
       ▼
MobileBridge
       │
       ▼
.NET ↔ JavaScript interop
       │
       ▼
Platform wrapper
       │
       ├── Haptics
       ├── Storage
       ├── Save Manager
       ├── File Manager
       ├── Mod Manager
       ├── Touch Controls
       └── Multiplayer Services
```

Bridge calls are designed to fail gracefully when the corresponding platform implementation is unavailable.

### MouseUI

`MouseUI` adds generic mouse-based interaction to Celeste's UI.

Responsibilities include:

- Mouse menu navigation
- Clicking UI elements
- Scroll-wheel support
- Pointer-based file selection
- Pointer-based chapter selection
- Pointer-based journal navigation
- Pointer-based `TextMenu` interaction
- Replacing controller-only bottom-screen navigation hints
- Clickable `GO BACK` behavior
- Optional touch input supplied through `MobileBridge`

`MouseUI` does not require `MobileBridge` for desktop mouse functionality.

If `MobileBridge` is present, touch is automatically treated as an additional pointer source.

### MobileTweaks

`MobileTweaks` handles mobile-specific Celeste behavior and UI policy.

Responsibilities include:

- Camera centering
- Intro/startup skipping
- Main-menu organization
- Mobile Options integration
- Browser Fullscreen suppression
- Right-column menu overflow
- Relocating mobile-suite settings
- Preserving third-party Mod Options

`MobileTweaks` does not provide low-level platform communication.

### MobileMultiplayer

`MobileMultiplayer` builds a mobile-friendly multiplayer experience around CelesteNet.

It does not replace CelesteNet's networking implementation.

Instead, it:

- Adds Multiplayer to the main menu
- Hides CelesteNet's standalone Mod Options section
- Recreates CelesteNet settings under `Options → Multiplayer`
- Replaces the normal connection/server picker with Host and Join flows
- Preserves the rest of CelesteNet's settings
- Provides username configuration
- Lists official/saved/custom/discovered servers
- Supports custom server management
- Requests native hosting through `MobileBridge`

CelesteNet remains an external dependency.

The mobile build script does not rebuild CelesteNet.

### BetterMapEditor

`BetterMapEditor` provides a standalone map-editing environment.

Its design goal is approximately:

> A smaller, easier in-game editor inspired by Lönn/Ahorn rather than a replacement for every advanced desktop mapping feature.

Responsibilities include:

- Project creation
- Everest map-mod creation
- Multiple chapters
- Multiple rooms
- Graphical room editing
- Tile editing
- Basic entities
- Spawn placement
- Pan/zoom
- Undo/redo
- Map-binary generation
- Mouse support
- Optional `MobileBridge` touch support

### Module Relationships

The modules intentionally avoid unnecessary hard dependencies.

Conceptually:

```text
                 MobileTweaks
                /      |      \
               /       |       \
              ▼        ▼        ▼
      MobileBridge  MouseUI  MobileMultiplayer
           │           │            │
           │           │            ▼
           │           │        CelesteNet
           │           │
           └──── touch ─┘


              BetterMapEditor
                    │
            optional touch via
              MobileBridge
```

Important points:

- `MouseUI` works with mouse without `MobileBridge`.
- `BetterMapEditor` works with mouse without `MobileBridge`.
- `MobileMultiplayer` uses installed CelesteNet rather than rebuilding it.
- `MobileBridge` owns native/platform communication.
- `MobileTweaks` owns the unified mobile menu experience.
- Unrelated third-party mods continue using Everest normally.

---

## Multiplayer Architecture

CelesteNet remains responsible for actual multiplayer synchronization.

`MobileMultiplayer` changes how users interact with it.

Instead of:

```text
Mod Options
└── CelesteNet
    └── Connect to...
```

the mobile interface becomes:

```text
Options
└── Multiplayer
    ├── Host
    ├── Join
    ├── Username
    └── CelesteNet settings
```

### Join

The Join screen can expose:

- Official CelesteNet server
- Existing CelesteNet server setting
- CelesteNet saved servers
- Custom servers
- Locally discovered servers
- Wrapper-discovered servers

Custom servers can be added and removed directly from the UI.

### Host

Hosting requires functionality that cannot be implemented entirely inside a browser/WASM client.

The intended route is:

```text
MobileMultiplayer
       │
       ▼
MobileBridge
       │
       ▼
AndroidWrapper / IOSWrapper
       │
       ▼
Native CelesteNet server host
```

The platform wrapper therefore handles the actual listening server process/service while the Everest mod provides the user interface and session control.

---

## Map Editor Architecture

`BetterMapEditor` stores editable project state and emits real Celeste map binaries.

A project can contain:

```text
Map Project
│
├── Chapter 1
│   ├── Room 1
│   ├── Room 2
│   └── Room 3
│
├── Chapter 2
│   ├── Room 1
│   └── Room 2
│
└── ...
```

The editor workspace is organized around:

```text
┌──────────────┬───────────────────────────────────┐
│              │                                   │
│ Room list    │           Room canvas             │
│              │                                   │
│ Room 1       │   Tiles / entities / spawn        │
│ Room 2       │                                   │
│ Room 3       │                                   │
│              │                                   │
│ + New Room   │                                   │
│ - Delete     │                                   │
│              │                                   │
├──────────────┴───────────────────────────────────┤
│ Select | Solid | Erase | Spawn | Berry | Spring │
│ Spikes | Pan | Undo | Redo | Zoom | Save        │
└──────────────────────────────────────────────────┘
```

It is intentionally simpler than Lönn or Ahorn but follows the same general room/canvas/tool workflow.

---

## WASM Loader Rebuild

The threaded loader is based on Mercury Workshop's Celeste WASM work.

After publishing, platform-specific post-processing can:

- Transfer the main canvas into the threaded runtime path
- Route Emscripten main-thread assembly calls correctly
- Adjust runtime limits
- Split large `dotnet.native.*.wasm` binaries
- Copy the processed runtime into `CelesteRuntime/_framework/`

The exact scripts and paths may vary as the repository is reorganized.

---

## Building Mobile Mods

The mobile mods live under:

```text
CelesteMobileMods/
```

Each code mod contains:

```text
SomeMod/
├── SomeMod.cs
├── SomeMod.csproj
├── everest.yaml
└── optional content folders
```

Examples of optional content folders include:

```text
Dialog/
Graphics/
Maps/
Loenn/
Audio/
```

### Automatic Mod Detection

`BuildDeployMods.ps1` does not maintain a hard-coded list of mobile mods.

A directory is automatically detected as a buildable mod when it contains both:

```text
everest.yaml
*.csproj
```

For example:

```text
CelesteMobileMods/
│
├── MobileBridge/
│   ├── MobileBridge.csproj
│   └── everest.yaml
│
├── MouseUI/
│   ├── MouseUI.csproj
│   └── everest.yaml
│
└── SomeFutureMod/
    ├── SomeFutureMod.csproj
    └── everest.yaml
```

`SomeFutureMod` will automatically be built on the next run.

### CelesteNet

CelesteNet is deliberately not managed by the mobile-mod build script.

The script does not:

- Build CelesteNet
- Repackage CelesteNet
- Delete CelesteNet
- Replace CelesteNet
- Deploy CelesteNet

CelesteNet should already exist in the target Everest installation.

### Mod Package Layout

Mobile mods use a flat code-mod package:

```text
MobileBridge.zip
├── everest.yaml
├── MobileBridge.dll
└── Dialog/
```

The corresponding manifest contains:

```yaml
DLL: MobileBridge.dll
```

The DLL is not placed under `/bin` inside the Everest ZIP.

The source project's normal build output can still live under:

```text
bin/
```

because that is simply where `dotnet build` writes the compiled file.

The deployment script copies the resulting DLL out of that build directory and places it at the root of the Everest package.

---

## Build Pipeline

At a high level:

```text
CelesteMobileMods/
        │
        ▼
Auto-detect mod projects
        │
        ▼
Compile C#
        │
        ▼
Package Everest mods
        │
        ▼
CelesteRuntime/Mods
        │
        ▼
Prepare threaded WASM runtime
        │
        ▼
Post-process framework
        │
        ▼
Supply legally obtained Celeste files
        │
        ▼
Platform wrapper build
        │
        ├── Android APK
        └── iOS application
```

---

## Prerequisites

Development may require:

- Android Studio (optional)
- Android SDK
- Compatible JDK
- Gradle through the included Android wrapper
- .NET SDK
- Node.js
- WSL or Linux environment for rebuilding/post-processing the WASM runtime
- A legally obtained copy of Celeste

iOS development additionally requires the appropriate Apple development environment.

The repository does not download or distribute commercial Celeste game files.

---

## Building the Everest Mods

From the repository root:

```powershell
powershell -ExecutionPolicy Bypass -File .\CelesteMobileMods\BuildDeployMods.ps1
```

The script:

1. Detects all buildable mod directories automatically.
2. Ignores CelesteNet.
3. Builds each detected `.csproj`.
4. Reads the mod's existing `everest.yaml`.
5. Copies the compiled DLL to the ZIP root.
6. Includes Everest content folders such as `Dialog/` or `Graphics/`.
7. Verifies the package.
8. Deploys it to the configured desktop Celeste installation for testing.

This is primarily a development/test deployment path.

---

## Building the Android Runtime

The exact Android build commands depend on the contents of `AndroidWrapper/`, but the general process is:

```text
CelesteRuntime/
       │
       ▼
AndroidWrapper packages runtime
       │
       ▼
Gradle build
       │
       ▼
APK
```

A typical Android wrapper build is:

```powershell
.\BuildAndroidWrapper.bat
```

`BuildAndroidWrapper.bat` stages the shared `CelesteRuntime/` folder into
`AndroidWrapper/app/src/main/assets/CelesteRuntime/` before running Gradle.

The generated APK can then be installed with:

```powershell
adb install -r <path-to-apk>
```

---

## Testing

### Desktop Everest Testing

The individual modules are designed so they can be tested on normal desktop Celeste where practical.

Examples:

- `MouseUI` works as a normal desktop mouse-navigation mod.
- `BetterMapEditor` works with mouse input.
- `MobileTweaks` can be tested against desktop Everest behavior.
- `MobileMultiplayer` can use an installed CelesteNet client.
- `MobileBridge` safely no-ops platform calls when the mobile host is absent.

### Android Testing

Launch the Android application using ADB as appropriate for the configured application ID.

Useful logcat categories include:

```bash
adb logcat CelesteAssetServer:D GeckoConsole:D GeckoRuntime:D AndroidRuntime:E *:S
```

For a clean test:

```bash
adb logcat -c
```

Then launch the application and begin log capture.

For example, if the application ID and activity are configured as `com.unlim8ted.celeste/.MainActivity`:

```bash
adb shell am start -n com.unlim8ted.celeste/.MainActivity
adb logcat CelesteAssetServer:D GeckoConsole:D GeckoRuntime:D AndroidRuntime:E *:S
```

---

## Current Development Priorities

The main areas currently being worked on are:

1. **Mobile UI integration**
   - Finish consistent pointer/touch support across every Celeste/Everest menu.
   - Continue refining the unified mobile Options layout.

2. **Multiplayer**
   - Finish Android-native CelesteNet hosting.
   - Add equivalent iOS host support.
   - Improve discovery and session-management UX.

3. **BetterMapEditor**
   - Expand the entity catalog.
   - Add triggers.
   - Add decals.
   - Add stylegrounds.
   - Improve selection and room-management tools.
   - Continue moving toward a lightweight Lönn-style experience.

4. **Everest compatibility**
   - Continue testing third-party mods against the threaded WASM runtime.
   - Fix assumptions that depend on desktop-only APIs or behavior.

5. **iOS**
   - Complete the iOS wrapper.
   - Implement equivalent storage, haptic, file, and multiplayer services.

6. **Mod ecosystem**
   - Continue improving browsing, installation, persistence, updating, and management of Everest mods on mobile.

---

## Credits

This project builds on work from several projects and communities.

### Unlim8ted Studios

Mobile port architecture, Android integration, mobile UI, platform bridge, modular Everest integration, multiplayer UI, map editor, and project-specific development.

https://unlim8ted.com

### Mercury Workshop

Webleste / Celeste WASM, providing the WebAssembly foundation used by this project.

https://github.com/MercuryWorkshop/celeste-wasm

### Everest

The Celeste mod loader and API used by the mobile mod ecosystem.

### CelesteNet

Multiplayer infrastructure used by `MobileMultiplayer`.

### Mozilla

GeckoView provides the embedded Android browser engine required for the current threaded WASM runtime.

### LucyYuih

Prior Android Celeste WASM work used as part of the Android-port foundation.

https://gamejolt.com/games/CelesteWASMAndroid/1043072

### Extremely OK Games

Creators and rights holders of Celeste.

---

## License & Attribution

This is an unofficial community project created by Unlim8ted Studios.

Original mobile integration code and other original contributions by Unlim8ted Studios are shared publicly as part of this project.

Contributions, testing, issue reports, and improvements are welcome.

This project builds upon or interfaces with third-party software including:

- Celeste WASM / Webleste
- Everest
- CelesteNet
- GeckoView
- Prior Android Celeste WASM work

Third-party components remain subject to their respective licenses and copyright notices.

Celeste and its associated names, characters, artwork, audio, game code, commercial assets, and other intellectual property belong to their respective rights holders.

Commercial Celeste game data, including `data.data`, is not included in this repository and must be supplied from a legally obtained copy of Celeste.

This project is not affiliated with, sponsored by, or endorsed by Extremely OK Games.

---

<div align="center">

Made with ❤️ by

<strong>Unlim8ted Studios</strong>

</div>
