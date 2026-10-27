<div align="center">

<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/banner.png" alt="AirFrameXR REMIX BarThi Banner" width="720" />

# 🚀 AirFrameXR REMIX BarThi `v0.3-ALPHA`

**Ultra-low latency Direct OpenXR & SteamVR PCVR streaming runtime for Meta Quest 3 / 3S / Pro / 2.**
Bypasses compositor overhead with hardware-tuned NVENC multi-slice encoding, unified SteamVR driver telemetry, 1-click ADB/APK deployment, automated microphone passthrough (`AirFrameXR-MIC`), and a modernized Dark Mode UI — now with a **native Rust OpenXR Quest client (v0.2, dual OpenGL / Vulkan builds)** and a unified **TOOLBOX OPENXR** launcher panel.

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

## ✨ What's New in `v0.3-ALPHA`

### 🦀 1. Native Rust OpenXR Quest Client — Dual GPU Backend Builds
* **Pure-Rust OpenXR client** (`aarch64-linux-android`, no JVM/ART) compiled in two separate signed APKs so you can A/B test graphics backends on the same headset:
  * `AirFrameXR-REMIX-BarThi-v0.2-ALPHA-OpenGL.apk` — OpenGL ES 3.2 backend (`XR_KHR_opengl_es_enable`), recommended for maximum stability.
  * `AirFrameXR-REMIX-BarThi-v0.2-ALPHA-VULKAN.apk` — Vulkan backend (`XR_KHR_vulkan_enable2`, `VK_KHR_multiview`), experimental lower-latency path.
* **Extreme refresh-rate unlock** — the client enumerates every refresh rate the headset driver exposes (`xrEnumerateDisplayRefreshRatesFB`) and requests the highest one with a safe fallback chain: **207 Hz → 144 Hz → 120 Hz** (frame budgets 4.83 / 6.94 / 8.33 ms).
* **Performance lock** — `XR_EXT_performance_settings` pins CPU & GPU to `Boost`/`SustainedHigh` for the whole session; `XR_FB_space_warp` support is prepared but off by default.
* **Compiler tuned for Snapdragon XR2 Gen 2** — `lto = "fat"`, `codegen-units = 1`, `panic = "abort"`, `strip = "symbols"`, plus `target-cpu=cortex-a78` and `+neon` so all encoding hot paths emit SIMD NEON instructions.

### 🛠️ 2. Fixed APK Packaging (Failure [-124] resolved)
* Quest-side installs previously failed with `Failure [-124: Failed parse during installPackageLI: Targeting R+ (version 30 and above) requires the resources.arsc of installed APKs to be stored uncompressed and aligned on a 4-byte boundary]`.
* Every shipped APK is now rebuilt with `resources.arsc` **STORED** (uncompressed) and 4-byte aligned, then signed **v1 + v2 + v3** (`apksigner`) — installs cleanly via ADB, SideQuest, and the built-in installer.

### 🎛️ 3. TOOLBOX OPENXR — Unified Launcher Panel
* The main `launcher.exe` now embeds a **TOOLBOX OPENXR** status panel: one glance shows whether your games will use AirFrameXR (Direct OpenXR) or SteamVR + the AirFrameXR driver, with the status message and all action buttons grouped at the bottom of the panel.
* **Universal ADB installer** (`tools\install_apk_quest.bat`): auto-cleans stale TCP connections (`adb disconnect`), detects the headset serial automatically, always installs with an explicit `-s <serial>`, handles `unauthorized` state with clear guidance, and supports multiple connected headsets with a numbered picker.

### 🔊 4. Audio, Telemetry & UI (carried over from v0.2)
* **AAudio ultra-low-latency path** — `PerformanceMode::LowLatency` + `SharingMode::Exclusive` on the Quest client.
* **Live 500 ms SteamVR telemetry** in `streamer.exe`: Server/Client FPS, bitrate, total latency, encode/network/decode/vsync breakdown, packet loss, headset battery %.
* **Dark Mode UI** across `launcher.exe`, `streamer.exe`, `configure.exe`, `performance.exe` with full **English & French** localization.

---

## 📸 Interface Showcase

<div align="center">

### 🎛️ 1. Main Launcher (`launcher.exe`) — TOOLBOX OPENXR Panel
*1-Click OpenXR / SteamVR Runtime Switcher, USB ADB Forwarder, Quest APK Installer & Language Selector*
<img src="https://raw.githubusercontent.com/BarthiGame/AirFrameXR-REMIX-BarThi/main/docs/images/en_launcher_toolbox_v03.png" alt="AirFrameXR Main Launcher — TOOLBOX OPENXR (English)" width="800" />

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
   * `AirFrameXR-REMIX-BarThi-v0.3-Release.zip` (Portable Windows x64 Runtime)
   * `AirFrameXR-REMIX-BarThi-v0.2-ALPHA-OpenGL.apk` (Meta Quest Client — OpenGL ES 3.2, Recommended)
   * `AirFrameXR-REMIX-BarThi-v0.2-ALPHA-VULKAN.apk` (Meta Quest Client — Vulkan, Experimental)
2. **Extract** `AirFrameXR-REMIX-BarThi-v0.3-Release.zip` to a folder on your PC.
3. **Launch** `AirFrameXR.exe` (or `bin\launcher.exe`):
   * Connect your Meta Quest via USB and click **Install APK (Quest)** — or sideload either APK via ADB/SideQuest (the universal installer handles serial selection and `unauthorized` state for you).
   * Click **Enable ADB (USB)** for wired ultra-high bitrate streaming, or connect wirelessly over Wi-Fi 6/6E/7.
   * Choose **Activate AirFrameXR** (Direct OpenXR) or **Enable SteamVR** depending on your game, then launch **Streamer**!

---

## 📜 License & Upstream Links

* **AirFrameXR**: [https://github.com/jfbobier/AirFrameXR](https://github.com/jfbobier/AirFrameXR)
* **ALVR (Air Light VR)**: [https://github.com/alvr-org/ALVR](https://github.com/alvr-org/ALVR)
* **REMIX Maintainer**: [BarthiGame](https://github.com/BarthiGame/AirFrameXR-REMIX-BarThi)
