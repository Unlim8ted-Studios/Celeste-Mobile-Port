<div align="center">

<img src="readme-icons/mountain.svg" width="88" alt="Mountain illustration" />

<h1>Celeste Mobile Port</h1>

<p><strong>Celeste + Everest on mobile</strong></p>

<p>An unofficial port of Celeste for Android, with customizable touch controls, mobile-friendly menus, and mod support.</p>

<p>Developed by <a href="https://unlim8ted.com">Unlim8ted Studios</a></p>

<p><strong><a href="https://github.com/Unlim8ted-Studios/Celeste-Mobile-Port/releases">Download for Android</a></strong> · <a href="https://github.com/Unlim8ted-Studios/Celeste-Mobile-Port/issues">Report an issue</a></p>

</div>

---

> [!IMPORTANT]
> **You need a legally obtained copy of Celeste.** The Android release contains encrypted game data that is unlocked with `Gameplay0.data` from your own desktop installation. Commercial game files are not distributed in this repository.

## <img src="readme-icons/install.svg" width="22" height="22" alt="" /> Getting Started

1. Download the latest APK from [Releases](https://github.com/Unlim8ted-Studios/Celeste-Mobile-Port/releases) and install it on your Android device.
2. From your Celeste installation on a computer, copy `Content/Graphics/Atlases/Gameplay0.data` to your phone.
3. Open the app, choose **Unlock with my game file**, and select `Gameplay0.data`.

You don't need to transfer the rest of your game installation.

---

## <img src="readme-icons/features.svg" width="22" height="22" alt="" /> Features

### <img src="readme-icons/touch.svg" width="19" height="19" alt="" /> Touch Controls

- Move, resize, and reposition on-screen buttons.
- Switch between joystick and directional arrows, with optional 8-way snapping.
- Save your layout; controls adjust between gameplay, menus, and cutscenes.

### <img src="readme-icons/mobile-ui.svg" width="19" height="19" alt="" /> Mobile UI

- Touch-enabled Celeste menus, including tapping and finger scrolling.
- Mobile settings integrated into the normal **Options** menu.
- Optional centered camera and other mobile-specific adjustments.

### <img src="readme-icons/mods.svg" width="19" height="19" alt="" /> Everest & Mods

- Everest support with in-app mod management and persistent mod storage.
- Mobile-specific Everest modules: `MobileBridge`, `MobileTweaks`, and `MouseUI`.
- Mod compatibility is still being improved.

### <img src="readme-icons/saves.svg" width="19" height="19" alt="" /> Saves & Haptics

- Persistent saves with import and export tools.
- Optional haptic feedback based on Celeste's rumble events.

---

## <img src="readme-icons/status.svg" width="22" height="22" alt="" /> Project Status

**Android:** Preview APKs are available. Development and physical-device testing are ongoing, so expect bugs and some mod compatibility issues.

**iOS:** The repository includes an experimental iOS wrapper and build instructions, but I can't compile or test it without the required Mac hardware. There is no iOS release yet. See [iOS build instructions](IOSWrapper/README.md).

**Experimental extras:** `MobileMultiplayer` (CelesteNet integration) and `BetterMapEditor` (in-game map editing) are present in the source but **aren't bundled in the public Android APK**.

---

## <img src="readme-icons/building.svg" width="22" height="22" alt="" /> Building

The main project directories are:

```text
AndroidWrapper/     Android app and native integrations
IOSWrapper/         Experimental iOS wrapper
CelesteRuntime/     Shared WebAssembly runtime
CelesteMobileMods/  Mobile Everest modules
```

A playable build requires the Android SDK/JDK, the appropriate .NET tooling, a prepared Celeste WebAssembly runtime, and files from an owned copy of Celeste. A fresh clone alone isn't enough.

**Build the Android wrapper (Windows):**

```bat
BuildAndroidWrapper.bat
```

**Build/deploy Everest modules for desktop testing:**

```powershell
powershell -ExecutionPolicy Bypass -File .\CelesteMobileMods\BuildDeployMods.ps1
```

---

## <img src="readme-icons/credits.svg" width="22" height="22" alt="" /> Credits

- [Mercury Workshop / Celeste WASM](https://github.com/MercuryWorkshop/celeste-wasm) — WebAssembly foundation.
- [Everest](https://everestapi.github.io/) — Celeste mod loader.
- [CelesteNet](https://github.com/0x0ade/CelesteNet) — multiplayer framework used by the optional module.
- [Mozilla GeckoView](https://mozilla.github.io/geckoview/) — Android embedded browser runtime.
- [LucyYuih](https://gamejolt.com/games/CelesteWASMAndroid/1043072) — previous Android Celeste WASM porting work.
- **Extremely OK Games** — creators of Celeste.

Celeste and its assets belong to their respective rights holders. This is an unofficial community project, not affiliated with or endorsed by Extremely OK Games. Third-party components remain subject to their respective licenses.

---

<div align="center">

Made with ❤️ by

<strong><a href="https://unlim8ted.com">Unlim8ted Studios</a></strong>

</div>
