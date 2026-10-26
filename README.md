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

### 🎨 4. Redesigned Dark Mode UI & Multilingual Support (EN / FR)
* Complete Dark Theme overhaul across all tools (`launcher.exe`, `streamer.exe`, `configure.exe`, `performance.exe`) with refined DPI scaling, non-cropped Haptics/Controller layouts, interactive `session.json` key inspector, and full English & French localization.

---

## 📸 Interface Showcase

<div align="center">

### 🎛️ 1. Main Launcher (`launcher.exe`)
*1-Click OpenXR / SteamVR Runtime Switcher, USB ADB Forwarder, Quest APK Installer & Language Selector*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_launcher.png" alt="AirFrameXR Main Launcher (English)" width="800" />

---

### 📡 2. Live Streamer & Real-Time Telemetry (`streamer.exe`)
*Live 120 FPS / 465 Mbps Streaming, Latency Breakdown (Game / Compositor / Encode / Network / Decode / VSync), SSW Frame Generation & Live Image Controls*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/screen_streamer_steamvr.png" alt="AirFrameXR Live Streamer & Real-Time Telemetry" width="800" />

---

### 🥽 3. Settings — Network & Headset Pairing (`configure.exe`)
*mDNS / UDP 9943 Discovery, Auto-Pairing, Packet Size & DSCP QoS Priority*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_tab0_network.png" alt="Settings - Network & Pairing" width="800" />

---

### 🎮 4. Settings — Controllers, Hand Tracking & Haptics (`configure.exe`)
*Controller Emulation, Skeletal Hand Tracking, Full Body Tracking (FBT), Eye/Face OSC, VMC Protocol & Position/Rotation Offsets*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_tab1_controllers.png" alt="Settings - Controllers & Headset" width="800" />

---

### ⚡ 5. Settings — Video & NVENC Hardware Encoding (`configure.exe`)
*H.264 / HEVC / AV1, Multi-Slice & Split-Frame NVENC, Client-Side Foveation & Headset Super-Sampling*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_tab2_video.png" alt="Settings - Video & NVENC Encoding" width="800" />

---

### 🎨 6. Settings — Image Quality, Clarity & Chroma-Key Passthrough (`configure.exe`)
*Snapdragon Game Super Resolution, Color Vibrance, Sharpening, Gamma & Mixed-Reality Chroma-Key Overlay*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_tab3_image.png" alt="Settings - Image Quality & Clarity" width="800" />

---

### ⏱️ 7. Settings — Frame Pacing, Phase Lock & Timing (`configure.exe`)
*Phase Lock, Running Start Margin, Host Intake Gate & Decoder Queue Drain*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_tab4_pacing.png" alt="Settings - Frame Pacing & Tracking" width="800" />

---

### 🔊 8. Settings — Audio Loopback & Virtual Microphone `AirFrameXR-MIC` (`configure.exe`)
*Universal Audio Loopback, 1-Click `AirFrameXR-MIC` Virtual Driver Setup & Buffer Tuning*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_tab5_audio.png" alt="Settings - Audio & Microphone" width="800" />

---

### 🖥️ 9. Settings — Windows System & SteamVR Lifecycle (`configure.exe`)
*Automatic Game Power Plan Switching (`GameTurbo` / High Performance) & SteamVR Auto-Start/Close*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_tab6_system.png" alt="Settings - Windows System & SteamVR" width="800" />

---

### 🧪 10. Built-in Network, Quality & NVENC Benchmarks (`configure.exe`)
*Automated Bitrate Sweep (100–500+ Mbps), 3D Visual Quality Benchmark & Hardware Encoder Profiling*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_tab7_bench.png" alt="Settings - Benchmarks & Tests" width="800" />

---

### 📊 11. Recorded Game Sessions & Performance Analyzer (`performance.exe`)
*Per-Game Session History, Slowest 10% FPS Decile, Repeated Frame Periods & End-to-End Latency Breakdown*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_performance.png" alt="Game Performance & Stats Overlay" width="800" />

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
   * Choose **Activate AirFrameXR** (Direct OpenXR) or **Enable SteamVR** depending on your game, then launch **Streamer**!

---

## 📜 License & Upstream Links

* **AirFrameXR**: [https://github.com/jfbobier/AirFrameXR](https://github.com/jfbobier/AirFrameXR)
* **ALVR (Air Light VR)**: [https://github.com/alvr-org/ALVR](https://github.com/alvr-org/ALVR)
* **REMIX Maintainer**: [BarthiGame](https://github.com/BarthiGame/AirFrameXR-REMIX-BarThi)
