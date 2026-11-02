<div align="center">

<img src="assets/mdm.png" width="96" height="96" alt="MHS Download Manager Logo" />

# MHS Download Manager

**High-Speed Windows x64 Download Accelerator, Smart Browser Stream Sniffer & Lossless Media Muxer**

Developed by **[Mehedi Hasan Shihab](https://sshihabb007.github.io/)** (`@sshihabb007`)

[![Latest Release](https://img.shields.io/github/v/release/sshihabb007/MHS-DOWNLOAD-MANAGER?label=Release&color=0D74E7&style=for-the-badge&logo=github)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest)
[![Build & Release](https://img.shields.io/github/actions/workflow/status/sshihabb007/MHS-DOWNLOAD-MANAGER/build.yml?branch=main&label=Build&color=10B981&style=for-the-badge&logo=githubactions&logoColor=white)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/actions/workflows/build.yml)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-06B6D4?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest)
[![Zero Dependencies](https://img.shields.io/badge/.NET%204.8-Zero%20Dependencies-3B82F6?style=for-the-badge&logo=dotnet&logoColor=white)](#build-from-source)
[![License: MIT](https://img.shields.io/badge/License-MIT-F59E0B?style=for-the-badge)](LICENSE)

<br />

[![Download Setup Installer](https://img.shields.io/badge/%E2%AC%87%20Download%20Setup%20Installer%20(x64)-v1.0.3-0D74E7?style=for-the-badge&logo=windows11&logoColor=white)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Download-Manager-Setup-1.0.3-x64.exe)
[![Download Portable EXE](https://img.shields.io/badge/%E2%AC%87%20Download%20Portable%20EXE%20(x64)-v1.0.3-1E293B?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Download-Manager-Portable-1.0.3-x64.exe)

[**Installation Guide**](#installation) · [**Load Browser Extension**](#extension-setup) · [**Key Features**](#key-features) · [**Architecture**](#architecture) · [**CLI Reference**](#cli-reference) · [**Developer**](#developer)

</div>

---

## Overview

**MHS Download Manager (`mdm.exe` v1.0.3)** is a clean-room, open-source Windows x64 download accelerator and Manifest V3 Chromium browser extension suite crafted in pure C# and vanilla JavaScript by **Mehedi Hasan Shihab**.

Engineered for maximum throughput and zero bloat, the desktop application, custom lossless container muxer, and graphical Windows Setup Wizard compile directly with the built-in Windows `.NET Framework 4.8` compiler (`csc.exe`) — requiring **zero external NuGet packages, zero third-party DLLs, and no external `ffmpeg.exe` installation**.

---

<a id="installation"></a>
## 📦 Installation Guide

### Official Release Files

Every [GitHub Release](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest) ships a clean set of artifacts (one Setup installer, one Portable executable, the standalone Extension archive, and SHA-256 checksums):

| Release Asset | Recommended For | What It Contains |
| :--- | :--- | :--- |
| **`MHS-Download-Manager-Setup-1.0.3-x64.exe`** | **Standard Desktop Install *(Recommended)*** | Graphical Windows x64 Setup Wizard that installs `mdm.exe`, `Uninstall.exe`, Desktop & Start Menu shortcuts, Native Messaging host, and keeps the ready-to-load **`extension\`** folder directly inside `<InstallDir>\extension`. |
| **`MHS-Download-Manager-Portable-1.0.3-x64.exe`** | **Portable / USB / Zero-Install Users** | Standalone single-file `mdm.exe` executable. Run directly from any folder with no installer or registry changes. |
| **`MHS-Browser-Extension-1.0.3.zip`** | **Portable Users Only** | Unpacked Manifest V3 browser extension archive for Portable users *(Setup `.exe` users already get `<InstallDir>\extension` automatically)*. |
| **`SHA256SUMS.txt`** | **Integrity Verification** | Official SHA-256 hashes for verifying downloaded release binaries. |

---

### Option A — Install Using the Setup `.exe` *(Recommended)*

1. Download **[`MHS-Download-Manager-Setup-1.0.3-x64.exe`](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Download-Manager-Setup-1.0.3-x64.exe)** from [**GitHub Releases**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest).
2. Double-click `MHS-Download-Manager-Setup-1.0.3-x64.exe` to launch the Setup Wizard.
3. Choose your installation directory (or keep the default user-writable folder — no UAC prompt required):
   ```text
   %LOCALAPPDATA%\Programs\MHS Download Manager
   ```
   *(Expands to `C:\Users\<YourUsername>\AppData\Local\Programs\MHS Download Manager`)*
4. Click **Install Now**. Setup installs the desktop app and keeps the complete unpacked **`extension\`** folder inside your installation directory:
   ```text
   <InstallDir>\
   ├── mdm.exe                      # MHS Download Manager 1.0.3 (x64 Desktop App)
   ├── extension\                   # Unpacked Browser Extension (load this folder in browsers!)
   │   ├── manifest.json            # Manifest V3 configuration
   │   ├── mehedi_content.js        # Floating 'Download Video' pill & MHS Stream Sniffer
   │   ├── mehedi_page_hook.js      # Media & player stream detector
   │   ├── shihab_background.js     # User-gesture download interceptor & Fetch Relay
   │   ├── shihab_popup.html        # Toolbar popup UI
   │   └── images\                  # Extension & developer icons
   ├── Uninstall.exe                # Clean Windows Uninstaller
   ├── README.md                    # Documentation
   └── LICENSE                      # MIT License
   ```
5. Keep **"Launch MHS Download Manager 1.0.3 now"** checked on the final screen and click **Finish**.

### Option B — Run the Portable `.exe` *(No Installation)*

1. Download **[`MHS-Download-Manager-Portable-1.0.3-x64.exe`](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Download-Manager-Portable-1.0.3-x64.exe)** and place it in any permanent folder (for example, `D:\Tools\MHS-Download-Manager\`).
2. *(Optional — for browser integration)* Download **[`MHS-Browser-Extension-1.0.3.zip`](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Browser-Extension-1.0.3.zip)** and extract it into `D:\Tools\MHS-Download-Manager\extension\`.
3. Run `MHS-Download-Manager-Portable-1.0.3-x64.exe`. It automatically starts the local browser bridge on `http://127.0.0.1:45779`.

---

<a id="extension-setup"></a>
## 🌐 How to Load the Browser Extension from the Installation Directory

When you install MHS Download Manager via `MHS-Download-Manager-Setup-1.0.3-x64.exe`, the unpacked extension is already placed inside **`<InstallDir>\extension`** so you can load it directly into any Chromium browser.

### Step 1 — Locate Your `<InstallDir>\extension` Path

Use **any** of these 3 quick methods to get the exact folder path:

- **Method 1 (Setup Finish Screen):** On the final step of the Setup Wizard, `<InstallDir>\extension` is **copied to your clipboard automatically**, and you can click **Copy Path** or **Open Folder**.
- **Method 2 (From Inside MHS Download Manager):** Open MHS Download Manager, click the **`☰` Menu** button in the top bar, and choose **`Help -> Open Browser Extension Folder (Chrome / Brave)...`**.
- **Method 3 (Default Path / Start Menu):** Open the Windows Start Menu -> **MHS Download Manager -> MHS Browser Extension Folder**, or use the default path directly:
  ```text
  %LOCALAPPDATA%\Programs\MHS Download Manager\extension
  ```
  *(If you chose a custom installation folder such as `D:\Apps\MHS Download Manager`, use `D:\Apps\MHS Download Manager\extension`.)*

### Step 2 — Load Unpacked in Chrome, Brave, Edge, or Opera

1. Open your browser's Extensions management page by typing one of the following into the address bar and pressing **Enter**:
   - **Google Chrome:** `chrome://extensions`
   - **Brave Browser:** `brave://extensions`
   - **Microsoft Edge:** `edge://extensions`
   - **Opera:** `opera://extensions`
2. Turn **ON** the **Developer mode** toggle switch (located in the **top-right** corner in Chrome/Brave/Opera, or **left sidebar** in Edge).
3. Click the **Load unpacked** button (top-left).
4. In the folder selection dialog:
   - Click the address bar at the top, paste `%LOCALAPPDATA%\Programs\MHS Download Manager\extension` *(or browse to `<YourInstallDir>\extension`)*, and press **Enter**.
   - Make sure you are selecting the **`extension`** folder itself (the folder that directly contains `manifest.json`), then click **Select Folder**.
5. Click the puzzle-piece **Extensions** icon in your browser toolbar and **Pin** **MHS Download Manager Integration Module**.
6. Click the pinned **MHS** icon — when `mdm.exe` is running, the status dot turns green and displays **`Connected · mdm.exe v1.0.3`**.

> **Important Tips:**
> - **Do not delete `<InstallDir>\extension`:** Chromium loads unpacked extensions directly from that directory on disk.
> - **Updating after a new release:** When you install a newer `MHS-Download-Manager-Setup-*-x64.exe` over your existing folder, Setup automatically refreshes `<InstallDir>\extension`. Simply visit `chrome://extensions` or `brave://extensions` and click the **Reload (`↻`)** icon on the MHS Download Manager card.

---

<a id="key-features"></a>
## ✨ Key Features

| Capability | Details |
| :--- | :--- |
| **⚡ Dynamic Segment Acceleration (`1–32` Connections)** | Splits transfers across concurrent HTTP `206 Partial Content` `Range` workers and dynamically subdivides the largest remaining active chunk in flight whenever a worker finishes early. Includes checkpointed **Pause / Resume**, **Refresh Download Address**, **SHA-256** integrity verification, and per-item or global **Speed Limiters**. |
| **🖥️ Dark Transfers Dashboard UI (`v1.0.3`)** | Edge-to-edge dark obsidian workspace (`#121418` / `#16181d`) with native Windows 11 DWM dark titlebars, `TRANSFERS` filter pills (`All Downloads`, `Downloading`, `Completed`, `Paused`, `Scheduled`, and file categories), live `Local Storage` & clickable speed-limit card, instant `Filter transfers...` search, 8-column inline-progress grid, and 4-pill live socket/throughput footer. |
| **🎬 Outside-Corner `Download Video` Pill & Stream Sniffer** | Translucent dark stadium pill (`Download Video` + stream count badge) anchored **outside** the top-right corner of `<video>` so video playback is never covered, with a single-instance DOM coordinator (zero overlapping icons) and the **`MHS STREAM SNIFFER`** quality selector (`MAX`, `1080p Full HD`, `720p HD`, `480p Standard`, `Audio Only`, and sniffed streams). |
| **🚀 High-Speed YouTube HD/4K & Unthrottled Audio** | Built-in InnerTube stream resolver (`144p` up to `1080p`, `1440p`, and `2160p 4K` + original `M4A`/`Opus` audio) paired with parallel burst-sliced HTTP `206` range workers (`1 MiB` audio / `4 MiB` video slices over warm `KeepAlive` TLS sockets). |
| **🎞️ Zero-Dependency Lossless Media Muxer** | Pure C# ISO-BMFF & Matroska muxer (`MehediMediaMuxer`) losslessly combines separate adaptive DASH video + audio tracks (`MP4/fMP4 + M4A -> Fast-Start .mp4`, `MP4 + WebM Opus -> .mp4`, `WebM + WebM -> .webm`, `WebM + M4A -> .mkv`) without re-encoding. *(DRM/EME-protected streams are strictly respected and never bypassed.)* |
| **🛡️ Strict User-Gesture Download Interception** | Intercepts user-clicked downloads (`.mkv`, `.mp4`, `.zip`, `.rar`, `.exe`, `.iso`, `.pdf`, etc.) while blocking unsolicited browser-startup history replays and background auto-popups (`event.isTrusted` + `chrome.storage.session` guard), with `Prevent` (`Alt`) / `Force` (`Ins`) hotkeys and automatic **Browser Fetch Relay** recovery on session-bound `HTTP 403` links. |
| **🧰 Complete Desktop Toolkit** | Includes **Named Queues**, **Scheduler**, **Site Grabber** web crawler, **Batch Download** generator, `.ef2` / `.txt` **Import & Export**, **9-Tab Options** dialog, **System Tray** integration, and a native **Windows x64 Setup Wizard & Uninstaller**. |

---

<a id="architecture"></a>
## 🏗️ System Architecture

```mermaid
flowchart LR
    subgraph Browser["Chromium Browser (Chrome / Brave / Edge / Opera)"]
        PH["mehedi_page_hook.js - MAIN World Media & Player Hook"]
        CS["mehedi_content.js - Outside-Corner Pill & Stream Sniffer"]
        SW["shihab_background.js - MV3 Service Worker & Fetch Relay"]
        PU["shihab_popup.html - Toolbar Popup UI"]
        PH --> CS
        CS <--> SW
        PU <--> SW
    end

    subgraph Desktop["MHS Download Manager (mdm.exe x64)"]
        BR["MehediBrowserBridgeAndNativeHost - HTTP 45779 + Native Host"]
        UI["ShihabMainIdmWindow & Dialogs - Dark Transfers Dashboard UI"]
        YT["MehediYouTubeInnerTubeResolver - HD/4K & Audio Resolver"]
        DE["MehediDynamicSegmentEngine - 1-32 Segment & Burst-Slice Engine"]
        MX["MehediMediaMuxer - Lossless MP4 / WebM / MKV Muxer"]

        BR --> UI
        BR --> YT
        UI --> DE
        YT --> DE
        DE --> MX
    end

    SW <-->|"Loopback JSON & Native Messaging"| BR
```

### Repository Layout

```text
MHS-DOWNLOAD-MANAGER/
├── .github/workflows/
│   └── build.yml                           # Automated CI build, smoke test & GitHub Release publisher
├── assets/
│   ├── mdm.ico                             # Multi-resolution application & installer icon
│   ├── mdm.png                             # High-resolution brand logo
│   └── author_shihab.jpg                   # Developer portrait embedded in About dialog
├── extension/                              # Manifest V3 Browser Extension (bundled into Setup)
│   ├── manifest.json                       # MV3 manifest (v1.0.3)
│   ├── mehedi_page_hook.js                 # MSE/XHR/fetch & player stream hook
│   ├── mehedi_content.js                   # Floating Download Video pill & Stream Sniffer menu
│   ├── shihab_background.js                # User-gesture download guard & Browser Fetch Relay
│   ├── shihab_popup.html / mehedi_popup.js # Extension toolbar popup
│   └── images/                             # Extension icons & avatar
├── src/
│   ├── MehediProgramEntry.cs               # Entry point, CLI router & Native Host switch
│   ├── ShihabMainIdmWindow.cs              # Dark Transfers sidebar, pill header, grid & tray
│   ├── MehediDialogsAndProgress.cs         # Add URL, File Info, Live Segment Progress & About
│   ├── ShihabToolsWindows.cs               # 9-Tab Options, Scheduler, Queues & Site Grabber
│   ├── MehediDynamicSegmentEngine.cs       # Multi-segment engine, burst slicer & YT resolver
│   ├── MehediMediaMuxer.cs                 # Zero-dependency ISO-BMFF & Matroska muxer
│   ├── MehediBrowserBridgeAndNativeHost.cs # Loopback HTTP server & Native Messaging host
│   ├── ShihabDownloadModels.cs             # Persistence models, categories & configuration
│   └── installer/
│       └── ShihabSetupWizard.cs            # Windows x64 Setup Wizard & Uninstaller
├── build.ps1                               # Zero-dependency PowerShell build script
├── LICENSE                                 # MIT License
└── README.md                               # Project documentation
```

---

<a id="build-from-source"></a>
## 🛠️ Building from Source

No Visual Studio workload, NuGet restore, or external SDK is required — any standard **Windows 10 / 11** machine includes `.NET Framework 4.8` (`csc.exe`):

```powershell
# Clone the repository
git clone https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER.git
cd MHS-DOWNLOAD-MANAGER

# Compile both bin\mdm.exe and bin\MHS-Download-Manager-Setup-1.0.3-x64.exe
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Build outputs generated in `bin\`:
- `bin\mdm.exe` — Standalone x64 desktop application (also published as `MHS-Download-Manager-Portable-1.0.3-x64.exe` in Releases)
- `bin\MHS-Download-Manager-Setup-1.0.3-x64.exe` — Official Windows x64 Setup Installer (embeds `mdm.exe`, `extension\`, `README.md`, and `LICENSE`)

---

<a id="cli-reference"></a>
## ⌨️ Command-Line Reference

### Application CLI (`mdm.exe`)

```text
mdm.exe /d <URL> [/p <LocalPath>] [/f <LocalFileName>] [/n] [/q] [/a] [/s]
```

| Flag | Description |
| :--- | :--- |
| `/d <URL>` | Add and download the specified URL |
| `/p <LocalPath>` | Destination directory where the file will be saved |
| `/f <LocalFileName>` | Exact output filename |
| `/n` | Silent mode — start downloading immediately without confirmation dialogs |
| `/q` | Close `mdm.exe` automatically once the download finishes |
| `/a` | Add the file to the download queue without starting immediately |
| `/s` | Start the active download queue immediately |

### Setup Installer CLI (`MHS-Download-Manager-Setup-1.0.3-x64.exe`)

```text
MHS-Download-Manager-Setup-1.0.3-x64.exe [--shihab-silent] [--shihab-install-dir <Path>] [--shihab-no-registry]
```

| Flag | Description |
| :--- | :--- |
| `--shihab-silent` / `/S` | Perform an unattended silent installation (or silent uninstallation with `Uninstall.exe`) |
| `--shihab-install-dir <Path>` | Install into a custom target directory |
| `--shihab-no-registry` | Extract files without writing `HKCU` shortcuts, Startup, or Uninstall registry keys |
| `--shihab-uninstall` | Run uninstallation routine (invoked automatically by `<InstallDir>\Uninstall.exe`) |

---

<a id="developer"></a>
## 👨‍💻 About the Developer

**MHS Download Manager** is designed, engineered, and maintained by **Mehedi Hasan Shihab** — a **Software & Systems Engineer** focused on building fast, clean, user-centric desktop tools and full-stack web applications.

| Field | Details |
| :--- | :--- |
| **Developer** | **Mehedi Hasan Shihab** *(Software & Systems Engineer)* |
| **GitHub** | [@sshihabb007](https://github.com/sshihabb007) |
| **Portfolio** | [https://sshihabb007.github.io/](https://sshihabb007.github.io/) |
| **LinkedIn** | [linkedin.com/in/mehedi-hasan-shihab](https://www.linkedin.com/in/mehedi-hasan-shihab) |
| **Email** | [sshihabb007@gmail.com](mailto:sshihabb007@gmail.com) |
| **Project Repository** | [github.com/sshihabb007/MHS-DOWNLOAD-MANAGER](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER) |

<div align="center">

[![Portfolio](https://img.shields.io/badge/Portfolio-sshihabb007.github.io-0D74E7?style=for-the-badge&logo=googlechrome&logoColor=white)](https://sshihabb007.github.io/)
[![GitHub](https://img.shields.io/badge/GitHub-@sshihabb007-181717?style=for-the-badge&logo=github&logoColor=white)](https://github.com/sshihabb007)
[![LinkedIn](https://img.shields.io/badge/LinkedIn-Mehedi%20Hasan%20Shihab-0A66C2?style=for-the-badge&logo=linkedin&logoColor=white)](https://www.linkedin.com/in/mehedi-hasan-shihab)
[![Email](https://img.shields.io/badge/Email-sshihabb007%40gmail.com-EA4335?style=for-the-badge&logo=gmail&logoColor=white)](mailto:sshihabb007@gmail.com)

</div>

---

## 📄 License & Responsible Use

Released under the **[MIT License](LICENSE)** — Copyright © 2026 **Mehedi Hasan Shihab (`sshihabb007`)**.

> **Note:** MHS Download Manager accelerates standard HTTP/HTTPS file transfers and non-DRM media streams that your browser already has permission to access. Encrypted DRM / EME streams (`Widevine`, `PlayReady`, `FairPlay`, `SAMPLE-AES`) are strictly respected and never bypassed.
