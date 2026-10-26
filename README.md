<div align="center">

<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/banner.png" alt="AirFrameXR REMIX BarThi Banner" width="720" />

# 🚀 AirFrameXR REMIX BarThi `v0.2-ALPHA`

**Ultra-low latency Direct OpenXR & SteamVR PCVR streaming runtime for Meta Quest 3 / 3S / Pro / 2.**  
Bypasses compositor overhead with hardware-tuned NVENC multi-slice encoding, unified SteamVR driver telemetry, 1-click ADB/APK deployment, automated microphone passthrough (`AirFrameXR-MIC`), and a modernized Dark Mode UI.

[![Latest Release](https://img.shields.io/github/v/release/BarthiGame/AirFrameXR-REMIX-BarThi?include_prereleases&style=for-the-badge&color=0e62b2)](https://github.com/BarthiGame/AirFrameXR-REMIX-BarThi/releases)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%2F11%20x64-0078D6?style=for-the-badge&logo=windows)](https://github.com/BarthiGame/AirFrameXR-REMIX-BarThi/releases)
[![Headsets](https://img.shields.io/badge/Headsets-Meta%20Quest%203%20%7C%203S%20%7C%20Pro%20%7C%202-1877F2?style=for-the-badge&logo=meta)](https://github.com/BarthiGame/AirFrameXR-REMIX-BarThi/releases)

</div>

---

## 🙏 Official Credits & Acknowledgments

This **REMIX** edition is built upon the extraordinary engineering of the open-source VR community. Full credit and special thanks go to the upstream creators:

* 🌟 **[AirFrameXR by jfbobier](https://github.com/jfbobier/AirFrameXR)** — Original Direct OpenXR runtime architecture, VDXR integration, hardware-tuned NVENC split-frame encoder pipeline, and low-latency frame pacing foundation.
* 🌟 **[ALVR — Air Light VR (alvr-org)](https://github.com/alvr-org/ALVR)** — The foundational open-source PC-to-headset VR streaming engine, OpenVR driver core, network protocol, and Android OpenXR client ecosystem.

---

## ✨ What's New in `v0.2-ALPHA`

### 🔗 1. Unified `session.json` & Seamless SteamVR Coexistence
* **Single Unified Configuration (`config\session.json`)**: Both the Direct OpenXR host (`airframexr_host.exe`) and the official ALVR OpenVR driver (`driver_airframexr.dll`) now share a single configuration file (`config\session.json`) and unified log directory (`logs\`). Any setting changed in `configure.exe` or `streamer.exe` immediately applies to both Direct OpenXR and SteamVR modes.
* **Zero Port 8082 / 9943 / 9944 Conflicts**:
  * Automatic port fallback (`8082..8086`) eliminates the `ALVR panicked: error binding to 0.0.0.0:8082 (os error 10048)` crash dialog.
  * Resilient UDP `9943` discovery socket retry loop ensures instant handover when switching between AirFrameXR Direct OpenXR and SteamVR (`vrserver.exe`).
* **Real-Time SteamVR Telemetry in `streamer.exe`**:
  * When SteamVR is active, `streamer.exe` automatically yields UDP ports to `driver_airframexr.dll` and displays live 500ms telemetry directly in the dashboard tiles: **Server/Client FPS, Bitrate (Mbps), Total Latency, Encode / Network / Decode / VSync breakdown, Packet Loss, and Headset Battery %**.

### 🎮 2. Fixed Direct OpenXR & Oculus (`LibOVRRT64_1`) Game Detection
* Synchronized shared memory and named event IPC prefixes (`AirFrameXR_*`) across `openxr_airframexr.dll`, `LibOVRRT64_1.dll`, `mirrordesktop.exe`, and `airframexr_host.exe` so native OpenXR and Oculus games are immediately detected on launch.

### 🛠️ 3. 1-Click Runtime Switcher, ADB USB & Quest APK Installer
* **Instant Runtime Switching**: Switch between **AirFrameXR (Direct OpenXR)** and **SteamVR + AirFrameXR Driver** (for Anti-Cheat / BattlEye titles) with a single click from both the Launcher and Streamer windows.
* **1-Click ADB (USB) & APK Installer**: Built-in `ADBForwarder` toggle for wired USB streaming at up to 500+ Mbps and 1-click Quest APK installation (`install_apk_quest.bat`).
* **Automated Virtual Microphone (`AirFrameXR-MIC`)**: 1-click setup and automatic Windows registry endpoint naming for zero-latency Quest microphone passthrough.

### 🎨 4. Redesigned Dark Mode UI & Multilingual Support (FR / EN)
* Complete Dark Theme overhaul across all tools (`launcher.exe`, `streamer.exe`, `configure.exe`, `performance.exe`) with refined DPI scaling, non-cropped Haptics/Controller layouts, and full French & English localization.

---

## 📸 Interface Preview

<div align="center">

### 🎛️ Main Launcher (`launcher.exe`)
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/screen_launcher.png" alt="AirFrameXR Launcher" width="780" />

### 📡 Live Streamer & Real-Time Telemetry (`streamer.exe`)
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/screen_streamer.png" alt="AirFrameXR Streamer" width="780" />

### ⚙️ Unlocked Settings & Audio/NVENC Tuning (`configure.exe`)
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/screen_configure.png" alt="AirFrameXR Configuration" width="780" />

### 📊 Performance & Session Analysis (`performance.exe`)
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/screen_performance.png" alt="AirFrameXR Performance" width="780" />

</div>

---

## 📦 Installation & Quick Start

1. **Download the Release Assets** from the [**Releases Page**](https://github.com/BarthiGame/AirFrameXR-REMIX-BarThi/releases):
   * `AirFrameXR-REMIX-BarThi-v0.2-Release.zip` (Portable Windows x64 Runtime)
   * `AirFrameXR-REMIX-BarThi.apk` (Meta Quest Client APK)
2. **Extract** `AirFrameXR-REMIX-BarThi-v0.2-Release.zip` to a folder on your PC.
3. **Launch** `AirFrameXR.exe` (or `bin\launcher.exe`):
   * Connect your Meta Quest via USB and click **Install APK (Quest)** (or sideload `AirFrameXR-REMIX-BarThi.apk` via ADB/SideQuest).
   * Click **Enable ADB (USB)** for wired ultra-high bitrate streaming, or connect wirelessly over Wi-Fi 6/6E/7.
   * Choose **Activate AirFrameXR** (Direct OpenXR) or **Activate SteamVR** depending on your game, then launch **Streamer**!

---

## 📜 License & Upstream Links

* **AirFrameXR**: [https://github.com/jfbobier/AirFrameXR](https://github.com/jfbobier/AirFrameXR)
* **ALVR (Air Light VR)**: [https://github.com/alvr-org/ALVR](https://github.com/alvr-org/ALVR)
* **REMIX Maintainer**: [BarthiGame](https://github.com/BarthiGame/AirFrameXR-REMIX-BarThi)
