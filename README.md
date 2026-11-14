<div align="center">

<img src="assets/mdm.png" width="96" height="96" alt="MHS Download Manager Logo" />

# MHS Download Manager

**High-Speed Windows x64 Download Accelerator, Smart Browser Stream Sniffer & Lossless Media Muxer**

Developed by **[Mehedi Hasan Shihab](https://sshihabb007.github.io/)** (`@sshihabb007`)

[![Latest Release](https://img.shields.io/github/v/release/sshihabb007/MHS-DOWNLOAD-MANAGER?label=Release&color=0D74E7&style=for-the-badge&logo=github)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases)
[![All Releases](https://img.shields.io/badge/Releases-v1.0.8%20%E2%86%92%20v1.0.0-7C3AED?style=for-the-badge&logo=github)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases)
[![Build & Release](https://img.shields.io/github/actions/workflow/status/sshihabb007/MHS-DOWNLOAD-MANAGER/build.yml?branch=main&label=Build&color=10B981&style=for-the-badge&logo=githubactions&logoColor=white)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/actions/workflows/build.yml)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011%20(x64)-06B6D4?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases)
[![Zero Dependencies](https://img.shields.io/badge/.NET%204.8-Zero%20Dependencies-3B82F6?style=for-the-badge&logo=dotnet&logoColor=white)](#build-from-source)
[![License: MIT](https://img.shields.io/badge/License-MIT-F59E0B?style=for-the-badge)](LICENSE)

<br />

[![Download Setup Installer](https://img.shields.io/badge/%E2%AC%87%20Download%20Setup%20Installer%20(x64)-v1.0.8-0D74E7?style=for-the-badge&logo=windows11&logoColor=white)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Download-Manager-Setup-1.0.8-x64.exe)
[![Download Portable EXE](https://img.shields.io/badge/%E2%AC%87%20Download%20Portable%20EXE%20(x64)-v1.0.8-1E293B?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Download-Manager-Portable-1.0.8-x64.exe)

[**Installation Guide**](#installation) · [**Load Browser Extension**](#extension-setup) · [**Key Features**](#key-features) · [**Release History**](#release-history) · [**Architecture**](#architecture) · [**CLI Reference**](#cli-reference) · [**Developer**](#developer)

</div>

---

## Overview

**MHS Download Manager (`mdm.exe` v1.0.8)** is a clean-room, open-source Windows x64 download accelerator and Manifest V3 Chromium & Firefox browser extension suite crafted in pure C# and vanilla JavaScript by **Mehedi Hasan Shihab**.

Engineered for maximum throughput and zero bloat, the desktop application, custom lossless container muxer, and graphical Windows Setup Wizard compile directly with the built-in Windows `.NET Framework 4.8` compiler (`csc.exe`) — requiring **zero external NuGet packages, zero third-party DLLs, and no external `ffmpeg.exe` installation**.

---

<a id="installation"></a>
## 📦 Installation Guide

### Official Release Files

Every [GitHub Release](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases) ships a clean set of artifacts (one Setup installer, one Portable executable, the standalone Extension archive, and SHA-256 checksums):

| Release Asset | Recommended For | What It Contains |
| :--- | :--- | :--- |
| **`MHS-Download-Manager-Setup-1.0.8-x64.exe`** | **Standard Desktop Install *(Recommended)*** | Graphical Windows x64 Setup Wizard that installs `mdm.exe`, `Uninstall.exe`, Desktop & Start Menu shortcuts, Native Messaging host, and keeps the ready-to-load **`extension\`** folder directly inside `<InstallDir>\extension`. |
| **`MHS-Download-Manager-Portable-1.0.8-x64.exe`** | **Portable / USB / Zero-Install Users** | Standalone single-file `mdm.exe` executable. Run directly from any folder with no installer or registry changes. |
| **`MHS-Browser-Extension-1.0.8.zip`** | **Portable Users Only** | Unpacked Manifest V3 browser extension archive for Portable users *(Setup `.exe` users already get `<InstallDir>\extension` automatically)*. |
| **`SHA256SUMS.txt`** | **Integrity Verification** | Official SHA-256 hashes for verifying downloaded release binaries. |

---

### Option A — Install Using the Setup `.exe` *(Recommended)*

1. Download **[`MHS-Download-Manager-Setup-1.0.8-x64.exe`](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Download-Manager-Setup-1.0.8-x64.exe)** from [**GitHub Releases**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases).
2. Double-click `MHS-Download-Manager-Setup-1.0.8-x64.exe` to launch the Setup Wizard.
3. Choose your installation directory (or keep the default user-writable folder — no UAC prompt required):
   ```text
   %LOCALAPPDATA%\Programs\MHS Download Manager
   ```
   *(Expands to `C:\Users\<YourUsername>\AppData\Local\Programs\MHS Download Manager`)*
4. Click **Install Now**. Setup installs the desktop app and keeps the complete unpacked **`extension\`** folder inside your installation directory:
   ```text
   <InstallDir>\
   ├── mdm.exe                      # MHS Download Manager 1.0.8 (x64 Desktop App)
   ├── extension\                   # Unpacked Browser Extension (load this folder in browsers!)
   │   ├── manifest.json            # Manifest V3 configuration
   │   ├── manifest.firefox.json    # Firefox / Gecko MV3 configuration
   │   ├── mehedi_content.js        # Floating 'Download Video' pill, sniffer & update toast
   │   ├── mehedi_page_hook.js      # Media & player stream detector
   │   ├── shihab_background.js     # User-gesture & Brave/Chrome popup guard, Relay & updater
   │   ├── shihab_popup.html        # Toolbar popup UI & update banner
   │   └── images\                  # 3D Extension & developer icons
   ├── Uninstall.exe                # Clean Windows Uninstaller
   ├── README.md                    # Documentation
   └── LICENSE                      # MIT License
   ```
5. Keep **"Launch MHS Download Manager 1.0.8 now"** checked on the final screen and click **Finish**.

### Option B — Run the Portable `.exe` *(No Installation)*

1. Download **[`MHS-Download-Manager-Portable-1.0.8-x64.exe`](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Download-Manager-Portable-1.0.8-x64.exe)** and place it in any permanent folder (for example, `D:\Tools\MHS-Download-Manager\`).
2. *(Optional — for browser integration)* Download **[`MHS-Browser-Extension-1.0.8.zip`](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest/download/MHS-Browser-Extension-1.0.8.zip)** and extract it into `D:\Tools\MHS-Download-Manager\extension\`.
3. Run `MHS-Download-Manager-Portable-1.0.8-x64.exe`. It automatically starts the local browser bridge on `http://127.0.0.1:45779`.

---

<a id="extension-setup"></a>
## 🌐 How to Load the Browser Extension from the Installation Directory

When you install MHS Download Manager via `MHS-Download-Manager-Setup-1.0.8-x64.exe`, the unpacked extension is already placed inside **`<InstallDir>\extension`** so you can load it directly into any Chromium browser.

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
6. Click the pinned **MHS** icon — when `mdm.exe` is running, the status dot turns green and displays **`Connected · mdm.exe v1.0.8`**.

> **Important Tips:**
> - **Do not delete `<InstallDir>\extension`:** Chromium loads unpacked extensions directly from that directory on disk.
> - **Updating after a new release:** When you install a newer `MHS-Download-Manager-Setup-*-x64.exe` over your existing folder, Setup automatically refreshes `<InstallDir>\extension`. Simply visit `chrome://extensions` or `brave://extensions` and click the **Reload (`↻`)** icon on the MHS Download Manager card.

---

<a id="key-features"></a>
## ✨ Key Features

| Capability | Details |
| :--- | :--- |
| **🎛️ Top Menu Bar & 11-Button Gradient Ribbon** | Full-width top menu bar (`Tasks`, `File`, `Downloads`, `View`, `Help`, `Registration`) and GDI+ 3-stop gradient vector icon toolbar (`Add URL`, `Resume`, `Stop`, `Stop All`, `Delete`, `Delete Completed`, `Options`, `Scheduler`, `Start Queue ▾`, `Stop Queue ▾`, `Tell a Friend`) with live selection state sync, `Sun..Sat` queue scheduler, periodic sync, and QR sharing. |
| **⚡ 1–128 Sparse Dynamic Segment Engine** | Pre-allocates a unified NTFS sparse `.mhs.part` file (`FSCTL_SET_SPARSE`) with `4096`-byte page-aligned dynamic halving, Connection 1 kept-stream reuse, TTFB stall preemption, slow-connection shedding (`< 1/4` peak speed), RFC 9110 `ETag` + `64`-byte resume overlap verification, multi-segment `FTP`/`FTPS` (`REST`), and `CRC32`/`MD5`/`SHA-1`/`SHA-256`/`SHA-512` checksum verification. |
| **🔔 GitHub Releases Auto-Updater & Popup** | Automatically checks GitHub Releases (`5s` after launch + every `6h`, or `Ctrl+U`), notifies via Tray + themed Update Popup + browser extension banner, verifies `SHA256SUMS.txt` (fail-closed), and performs one-click silent update + restart for both Setup and Portable modes. |
| **🧲 BitTorrent, Media Suite & Debrid Cloud** | Full BitTorrent & Magnet client (`BEP-3`, `BEP-9/10` magnet metadata, `BEP-15` UDP trackers, `BEP-19` WebSeeds, `.torrent` creator, IP blocklists), managed `yt-dlp` / `ffmpeg` / `gallery-dl` video/playlist/gallery downloaders, Real-Debrid / AllDebrid / Premiumize unlocking, Google Drive/Docs direct export, and hardened Zip-Slip/bomb-safe archive extraction. |
| **🎬 14-Browser Capture & Lossless Muxer** | Native Messaging auto-registration across 9 Chromium + 5 Firefox browsers (`manifest.json` + `manifest.firefox.json`), outside-corner `Download Video` pill & `MHS STREAM SNIFFER`, DNR inline capture, POST form capture, EME DRM detection badge, InnerTube HD/4K resolver, and pure C# ISO-BMFF/Matroska lossless muxer. |
| **🛡️ Privacy, Network Kill-Switch & DPAPI Vault** | Encrypted DNS (`DoH` via Cloudflare, Google, Quad9, AdGuard, NextDNS, Custom), authenticated local `SOCKS5` (`RFC 1928/1929`) & WinHTTP `PAC` relay, strict Network Adapter / VPN Kill-Switch with `netwatch` auto-resume, Windows `DPAPI` credential vault, Mark-of-the-Web (`Zone.Identifier`), and offline IP-to-Country flag badges. |
| **📱 LAN Remote QR Web UI, CLI & MCP Server** | Mobile-responsive LAN Remote Control Web Dashboard (`:8463`) with 8-digit one-time pairing & native ISO/IEC 18004 QR encoder, RSS 2.0/Atom auto-downloader, rolling bandwidth quota, 5 themes + `.toml` custom themes, `100%–200%` scaling, `28` languages, out-of-process `--crash-monitor`, `--headless` daemon, full `--cli`, and 15-tool `--mcp` JSON-RPC server. |

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
        DE["MehediDynamicSegmentEngine - 1-128 Sparse Segment Engine"]
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
├── extension/                              # Manifest V3 Browser Extension (Chromium + Firefox)
│   ├── manifest.json                       # Chromium MV3 manifest (v1.0.8)
│   ├── manifest.firefox.json               # Firefox / Gecko MV3 manifest (v1.0.8)
│   ├── mehedi_page_hook.js                 # MSE/XHR/fetch & EME DRM stream hook
│   ├── mehedi_content.js                   # Floating Download Video pill, sniffer menu & update toast
│   ├── shihab_background.js                # User-gesture & popup guard, DNR, POST, Relay & update check
│   ├── shihab_popup.html / css / mehedi_popup.js # Toolbar popup & online/offline update banner
│   ├── shihab_captured.html / mehedi_captured.js # Captured download confirmation page
│   └── images/                             # 3D blue-magenta extension icons & author avatar
├── src/
│   ├── MehediProgramEntry.cs               # Entry point, CLI/MCP/Headless (with auto-update)/Watchdog router
│   ├── Bridge/                             # Authenticated loopback HTTP bridge (:45779), relay & 14-browser Native Host
│   ├── Core/                               # Zero-dependency JSON serializer & core codecs
│   ├── Engine/                             # 1-128 sparse HTTP/FTP/HLS segment engine, rate controller & YouTube resolver
│   ├── Models/                             # Persistent configuration, transfer state & download models
│   ├── Muxer/                              # Zero-dependency ISO-BMFF & EBML/Matroska muxer
│   ├── Network/                            # Local SOCKS5/PAC proxy relay & outbound network policies
│   ├── Process/                            # Windows Job Object isolated child-process runner
│   ├── Qr/                                 # ISO/IEC 18004 QR code encoder
│   ├── Security/                           # Windows DPAPI credential & secret vault
│   ├── Shared/                             # Centralized URL/SSRF, archive & path safety validation
│   ├── Storage/                            # Atomic config/downloads state store & formatting helpers
│   ├── Subsystems/                         # DoH, GeoIP, Checksum, BitTorrent, Debrid, Media, Remote, RSS, Themes & MCP
│   ├── UI/                                 # Modular DWM dark chrome, 11-button ribbon, options, progress & tool dialogs
│   ├── Updater/                            # Resilient GitHub REST + Web fallback auto-updater & TopMost update dialog
│   └── installer/
│       └── ShihabSetupWizard.cs            # Windows x64 Setup Wizard, Silent Updater & Uninstaller
├── build.ps1                               # Zero-dependency PowerShell build & optional Authenticode signing script
├── CHANGELOG.md                            # Complete reverse-chronological changelog (v1.0.8 -> v1.0.0)
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

# Compile both bin\mdm.exe and bin\MHS-Download-Manager-Setup-1.0.8-x64.exe
powershell -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

Build outputs generated in `bin\`:
- `bin\mdm.exe` — Standalone x64 desktop application (also published as `MHS-Download-Manager-Portable-1.0.8-x64.exe` in Releases)
- `bin\MHS-Download-Manager-Setup-1.0.8-x64.exe` — Official Windows x64 Setup Installer (embeds `mdm.exe`, `extension\`, `README.md`, and `LICENSE`)

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
| `--cli status [--json]` | List all downloads and live progress |
| `--cli get <URL>` | Queue/start an HTTP, HTTPS, FTP, FTPS, Magnet, or `.torrent` download |
| `--cli check-update [--json]` | Check GitHub Releases for a newer version |
| `--cli update [--verify-only]` | Download, verify `SHA256SUMS.txt`, and apply the latest GitHub Release |
| `--cli pair [--json]` | Generate an 8-digit LAN Remote Control pairing code & terminal QR code |
| `--cli torrent-info <target>` / `torrent-create <src>` | Inspect `.torrent`/magnet metadata or create a `.torrent` file |
| `--cli video-get <URL>` / `gallery-get <URL>` | Download media or galleries via managed `yt-dlp` / `gallery-dl` |
| `--mcp` | Run the Model Context Protocol (`MCP`) JSON-RPC 2.0 stdio server (`15` tools) |
| `--headless` | Run background download engine, local bridge, and LAN Remote UI without GUI |
| `--forget-sign-ins` | Purge all DPAPI-encrypted credentials and tokens from the local vault |

### Setup Installer CLI (`MHS-Download-Manager-Setup-1.0.8-x64.exe`)

```text
MHS-Download-Manager-Setup-1.0.8-x64.exe [--shihab-silent] [--shihab-install-dir <Path>] [--shihab-no-registry]
```

| Flag | Description |
| :--- | :--- |
| `--shihab-silent` / `/S` | Perform an unattended silent installation (or silent uninstallation with `Uninstall.exe`) |
| `--shihab-install-dir <Path>` | Install into a custom target directory |
| `--shihab-no-registry` | Extract files without writing `HKCU` shortcuts, Startup, or Uninstall registry keys |
| `--shihab-uninstall` | Run uninstallation routine (invoked automatically by `<InstallDir>\Uninstall.exe`) |

---

<a id="release-history"></a>
## 📜 Release History (Reverse Chronological — Latest First)

Browse all full release downloads on [**GitHub Releases (`v1.0.8` → `v1.0.0`)**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases) or read the detailed version log in [**CHANGELOG.md**](CHANGELOG.md).

| Version | Date | Key Highlights | Key Bug Fixes & Hardening |
| :--- | :--- | :--- | :--- |
| [**`v1.0.8`** *(Latest)*](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/tag/v1.0.8) | 2026-11-14 | Win32 SxS application manifests (`mdm.manifest` & `setup.manifest`), typed `IShellLinkW` + `IPersistFile` COM shortcuts (removing `WScript.Shell`), direct `mdm.exe` Native Messaging host (removing `.cmd` launcher), optional Authenticode signing pipeline (`build.ps1` + CI), and `oxlint` AST scope gate | Fixed `chrome://extensions` `ReferenceError: shihabHVal is not defined` on HTTP `206` `Content-Range` responses; serialized MV3 context-menu rebuilds & `runtime.lastError` handling; guarded closed-tab badge Promises; preserved YouTube custom `User-Agent` & HDR/codec metadata; fixed duration ternary precedence & `shihab_logo128.png` resource entry |
| [**`v1.0.7`**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/tag/v1.0.7) | 2026-11-13 | Rock-solid dark/light DWM custom chrome (`ShihabChromeForm`), restore-size stability, automatic Desktop & Taskbar shortcut icon cache refresh (`SHChangeNotify` + `ie4uinit.exe -show`) during updates, and explicit `WM_SETICON` small/big HICON assignment | Fixed classic caption `[□] [×] [?]` & thick light outer border regression on `WM_NCACTIVATE` / restore; fixed `+14×14px` restore size growth; fixed stale Desktop/Taskbar icon cache across upgrades; fixed shared `Icon` disposal bug |
| [**`v1.0.6`**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/tag/v1.0.6) | 2026-11-13 | Modular C# architecture refactor (`src/<Domain>/`), resilient GitHub REST + Web fallback auto-updater with `/shihab/open-updater`, browser update banner (online & offline) + dismissible page toast, top-right **Background Minimize** button, Firefox `webRequestBlocking`, and new glowing 3D blue-magenta icon across app, setup & extension | Fixed dual Brave/Chrome/Edge native Save-As + MDM popups via `onDeterminingFilename` early cancel & erase; fixed silent `403` rate-limit updater failures; fixed offline popup hiding update banner; enabled `--headless` update cache population; tightened update & archive origin checks |
| [**`v1.0.5`**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/tag/v1.0.5) | 2026-11-12 | Top Menu Bar (`Tasks`, `File`, `Downloads`, `View`, `Help`, `Registration`), full-width 11-button GDI+ gradient ribbon toolbar, upgraded `Sun..Sat` Queue Scheduler, `Tell a Friend` QR dialog, and `Registration` dialog | Fixed compact/wide toolbar responsive layout (`955px–1320px+`), Queue Scheduler ordering & periodic sync reliability, and secondary dialog theme synchronization |
| [**`v1.0.4`**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/tag/v1.0.4) | 2026-11-12 | GitHub Releases Auto-Updater + `SHA256SUMS.txt` verification, `1–128` NTFS sparse `.mhs.part` segment engine, BitTorrent & magnet P2P engine, `yt-dlp`/`ffmpeg`/`gallery-dl` helpers, Debrid unlockers, `DoH`/`SOCKS5`/`PAC`/VPN Kill-Switch, `DPAPI` vault, LAN QR Web UI (`:8463`), 14-browser Native Messaging (Chromium + Firefox), 5 themes + 28 languages, crash watchdog, and 15-tool MCP server | Eliminated resume corruption via RFC 9110 `ETag` + `64`-byte overlap verification; reduced worker tail stalls via TTFB preemption & slow-connection shedding; added `X-MHS-Bridge-Token` local RPC authentication |
| [**`v1.0.3`**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/tag/v1.0.3) | 2026-11-02 | Dark Transfers Dashboard (`TRANSFERS` sidebar, `Local Storage` card, instant filter search, 8-column grid, 4-pill footer), outside-corner `Download Video` pill + `MHS STREAM SNIFFER` dropdown, circular developer portrait About dialog, and streamlined Setup + Portable + ExtZip releases | Removed duplicate Setup installer aliases from release staging; fixed container probe labeling and dark scrollbar/DPI alignment |
| [**`v1.0.2`**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/tag/v1.0.2) | 2026-10-31 | `chrome.storage.session` browser-startup & trusted user-gesture guard (`event.isTrusted === true` + per-tab/child-popup propagation + download ID seeding), plus sequential FIFO capture modal queue with `4s` duplicate-burst suppression | Eliminated spurious startup/restore/background download popups in Brave, Chrome, and Edge; prevented stacked capture dialogs and false clipboard popups at launch |
| [**`v1.0.1`**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/tag/v1.0.1) | 2026-10-31 | Outside top-right corner `Download Video` bar (`62%–96%` glass styling), cross-instance DOM singleton coordination, `<InstallDir>\extension` Setup verifier with Finish **Copy Path**/**Open Folder**, and GitHub Actions CI release pipeline | Eliminated overlapping floating video bars across reloads/SPAs; kept player controls uncovered on full-height Shorts/Reels/TikTok players; refreshed stale unpacked extension files during upgrades |
| [**`v1.0.0`**](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/tag/v1.0.0) | 2026-10-30 | Initial open-source release: Obsidian-Violet dark UI, InnerTube HD/4K (`2160p`) & parallel burst-sliced companion audio, zero-dependency lossless MP4/WebM/MKV muxer, `1–32` segment engine, Browser Fetch Relay, Queues, Site Grabber, Native Messaging, and x64 Setup Wizard | Fixed YouTube HTTP `403` companion audio throttling (`KeepAlive` burst slicing & InnerTube client resolution); isolated per-video captions/metadata on multi-video feeds |

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
