# Changelog — MHS Download Manager

All notable releases, new features, security enhancements, and bug fixes for **MHS Download Manager** (`mdm.exe` + Manifest V3 Browser Extension + Windows x64 Setup Wizard) are documented here in **reverse chronological order (newest first)**.

> **Live GitHub Releases:** [All Releases (`v1.0.6` → `v1.0.0`)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases)

---

## [v1.0.6] — 2026-11-13

### Highlights & New Features
- **Resilient Cross-Layer Auto-Updater & Update Popups for All Users (`mdm.exe` + Browser Extension)**:
  - Added automatic **GitHub Web fallback** (`https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases/latest` + release page + `SHA256SUMS.txt` verification) in `ShihabAutoUpdater` whenever `api.github.com` hits HTTP `403` rate-limits or `429` throttling.
  - Enabled startup + 6-hour periodic background update checks in `--headless` daemon mode (`MehediProgramEntry`), plus automatic retry on `OnNetworkRestored` when startup occurred before network readiness.
  - Added `POST /shihab/open-updater` loopback bridge endpoint (`BridgeHttpServer`) so clicking **Update** inside the browser extension popup or page toast immediately raises `mdm.exe` and opens `ShihabAutoUpdateDialog` on top (`Activated` & centered).
  - Upgraded the MV3 browser extension (`shihab_background.js`, `mehedi_popup.js`, `mehedi_content.js`) with rate-limit-free GitHub Web release polling (`chrome.alarms`), a `NEW` toolbar badge indicator, an always-available `#shihabUpdateBanner` (even when `mdm.exe` is offline), and a compact dismissible dark-glass update notification toast on active tabs.
- **Zero-Duplicate Browser Native Save-As Suppression (Brave / Chrome / Edge / Opera / Firefox)**:
  - Multi-stage download capture across `chrome.downloads.onCreated`, `onDeterminingFilename` (cancelling & erasing BEFORE `suggest()`), and `onChanged`, completely eliminating simultaneous native browser Save-As popups and MDM Download File Info popups.
  - Added `webRequestBlocking` declaration to `extension/manifest.firefox.json` for reliable Firefox/Gecko header interception.
- **Dedicated Background Minimize Button (`mdm.exe`)**:
  - Added a top-right **Background Minimize** button next to standard Minimize on custom DWM chrome windows (`ShihabChromeTitleBar`), a **Minimize to Background** control on Download Progress dialogs (`ShihabProgressDialogs`), plus View menu / `Ctrl+Shift+B` shortcut to keep downloads and browser capture running quietly in System Tray.
- **New Glowing 3D Blue-Magenta Brand Identity**:
  - Updated `assets/mdm.ico` (`16×16`–`256×256` 32-bit frames), `assets/mdm.png`, desktop forms & tray icon, Setup & Uninstaller executables, `extension/images/shihab_logo{16,24,32,48,128}.png`, `shihab_captured.html` favicon, and inline `SHIHAB_MHS_ICON_DATA_URI` across injected page UI.
- **Modular Clean Architecture Refactor & Hardening**:
  - Split monolithic C# sources into cohesive domain modules (`Bridge/`, `Core/`, `Engine/`, `Models/`, `Muxer/`, `Network/`, `Process/`, `Qr/`, `Security/`, `Shared/`, `Storage/`, `Subsystems/`, `UI/`, `Updater/`) compiled cleanly under `.NET Framework 4.8` (`/langversion:5`, `/highentropyva+`) with centralized `ShihabProcessAndPathSafety` URL/SSRF/path-traversal guards, `DPAPI` secret protection, Windows Job Object child isolation, and smoother double-buffered WinForms painting.
- **Full Release History & Documentation**:
  - Added `CHANGELOG.md`, synchronized GitHub Releases (`v1.0.6` down to `v1.0.0`), and updated `README.md` with modular architecture layout and reverse-chronological release history.

### Bug Fixes
- Fixed simultaneous Brave/Chrome/Edge native Save-As file chooser popup race during intercepted downloads (`onDeterminingFilename` cancellation & erase before `suggest()`).
- Fixed silent desktop update check failures when unauthenticated GitHub REST API calls returned `403 Forbidden` rate-limit errors.
- Fixed browser extension popup (`mehedi_popup.js`) hiding `#shihabUpdateBanner` on early return whenever `mdm.exe` was offline or not yet running.
- Fixed `--headless` mode leaving `updateAvailable`/`latestVersion` unpopulated in `/shihab/ping` replies.
- Tightened origin checks for update release links opened from extension (`https://github.com/...`), redirect target validation during update downloads, and archive extraction traversal safety.

---

## [v1.0.5] — 2026-11-12

### Highlights & New Features
- **Classic Top Menu Bar & Full-Width 11-Button Gradient Vector Ribbon**:
  - Added top menu bar (`Tasks`, `File`, `Downloads`, `View`, `Help`, `Registration`) paired with a responsive GDI+ 3-stop gradient toolbar (`Add URL`, `Resume`, `Stop`, `Stop All`, `Delete`, `Delete Completed`, `Options`, `Scheduler`, `Start Queue ▾`, `Stop Queue ▾`, `Tell a Friend`).
- **Scheduler, Queue & Utility Dialog Upgrades**:
  - Upgraded Queue Scheduler (`Sun..Sat` day-of-week schedules, periodic sync timer, queue ordering), added **Tell a Friend** QR sharing dialog, and added **Registration** status dialog.
- **UI & Build Polish**:
  - Updated toolbar & dialog layout responsiveness, dark-mode theming fidelity, and single-command CI release metadata.

### Bug Fixes
- Fixed toolbar button clipping and layout spacing between compact (`955px`) and wide (`1320px+`) main window widths.
- Fixed Queue Scheduler persistence edge cases and periodic queue sync synchronization issues.
- Fixed dialog icon/theme synchronization across secondary dialogs and Setup finish hints.

---

## [v1.0.4] — 2026-11-12

### Highlights & New Features
- **GitHub Releases Auto-Updater**:
  - Introduced `ShihabAutoUpdater` and `ShihabAutoUpdateDialog` with automatic startup/6h checks, System Tray balloon notifications, extension update banner, fail-closed `SHA256SUMS.txt` verification, and one-click silent update + restart for Setup and Portable editions.
- **1–128 Sparse Dynamic Segment Engine**:
  - Pre-allocated NTFS sparse `.mhs.part` zero-copy storage, `4096`-byte page-aligned halving, kept probe stream reuse, TTFB stall preemption, slow-connection shedding, RFC 9110 `ETag` + `64`-byte resume overlap validation, shared token-bucket speed limiter, multi-segment FTP/FTPS, and multi-algorithm checksum verification (`CRC32`, `MD5`, `SHA-1`, `SHA-256`, `SHA-512`).
- **BitTorrent P2P, Managed Media Helpers & Debrid Cloud**:
  - Added built-in BitTorrent & magnet engine (`BEP-3`, `BEP-9/10`, `BEP-15` UDP trackers, `BEP-19` WebSeeds, `.torrent` creator, IP blocklists), managed `yt-dlp`/`ffmpeg`/`gallery-dl` runner inside Windows Job Objects, Real-Debrid/AllDebrid/Premiumize + Google Drive/Docs unlockers, hardened archive auto-extractor, and background RSS auto-downloader.
- **Privacy, Network Security & Remote Control**:
  - Added `ShihabLocalNetworkRelay` (`SOCKS5`, WinHTTP PAC, DoH + plain DNS, strict VPN/adapter kill-switch), Windows `DPAPI` secret vault (`ShihabSecretsVault`), ISO/IEC 18004 QR code encoder, LAN Remote Web UI (`:8463`), token-authenticated local bridge (`X-MHS-Bridge-Token`), 14-browser Native Messaging (Chromium + Firefox MV3), EME DRM badge, 5 themes + custom `.toml`, `100%–200%` UI scaling, 28-language localization, out-of-process crash recovery watchdog, full CLI, and 15-tool MCP stdio server.

### Bug Fixes
- Eliminated resume corruption via RFC 9110 `ETag` + `64`-byte boundary overlap verification before appending resumed segments.
- Fixed slow/stalled worker tail latency via TTFB preemption and dynamic connection shedding across high segment counts (`1–128`).
- Hardened unauthenticated local RPC endpoints by requiring per-session cryptographic `X-MHS-Bridge-Token` authentication and hardened archive extraction paths.

---

## [v1.0.3] — 2026-11-02

### Highlights & New Features
- **Dark Transfers Dashboard UI**:
  - Redesigned main window with the `TRANSFERS` filter sidebar, live `Local Storage` & speed-limit card, rounded pill action bar + instant `Filter transfers...` search, 8-column progress grid, and 4-pill live socket/speed footer.
- **Outside-Corner `Download Video` Pill & `MHS STREAM SNIFFER` Dropdown**:
  - Refined translucent pill outside the top-right corner of `<video>` with quality selector rows (`MAX`, `1080p`, `720p`, `480p`, `AUDIO`, and sniffed streams).
- **Single-Setup + Portable Distribution & Refreshed Branding**:
  - Streamlined releases to ship one official Setup `.exe` (bundling `<InstallDir>\extension`), one standalone Portable `.exe`, unpacked Extension `.zip`, and `SHA256SUMS.txt`, plus updated circular developer portrait About dialog and Dodger Blue popup accents.

### Bug Fixes
- Eliminated duplicate setup executable naming in GitHub Release asset staging (`build.yml`).
- Improved video container sniffing and accurate resolution/bitrate badge classification in dropdown rows.
- Fixed dark scrollbar and high-DPI scaling alignment glitches in main transfers table and About dialog.

---

## [v1.0.2] — 2026-10-31

### Highlights & New Features
- **Zero Unsolicited Startup & Background Download Popups**:
  - Added `chrome.storage.session` browser-startup & user-gesture download guard (`event.isTrusted === true` + per-tab / child-popup gesture propagation + historical download ID seeding) so launching Brave, Chrome, or Edge never emits unwanted popups without explicit user clicks.
- **Sequential Capture Modals & Duplicate Suppression**:
  - Serialized Download File Info dialogs into an ordered FIFO queue inside `mdm.exe`, suppressed duplicate capture bursts within `4s`, and seeded startup clipboard monitor state.

### Bug Fixes
- Fixed unsolicited `Download File Info` popups firing on Brave/Chrome/Edge startup due to browser session restore or existing `chrome.downloads` history items.
- Fixed overlapping capture dialogs stacking simultaneously when multiple redirects/links triggered within milliseconds.
- Fixed spurious clipboard capture popups immediately after launching `mdm.exe` when a URL was already present on the clipboard.

---

## [v1.0.1] — 2026-10-31

### Highlights & New Features
- **Outside Top-Right Corner Video Download Bar**:
  - Anchored floating video bar outside top-right edge of `<video>` elements (`5px` above top edge, or `5px` outside right edge on full-height Shorts/Reels/TikTok players) with `62%` translucent glass resting finish transitioning to `96%` on hover.
- **DOM-Coordinated Singleton Video Widget**:
  - Cross-instance DOM coordination (`shihab-mehedi-idm-supersede` custom event + owner liveness verification + background ping check) guarantees strictly one download bar per page across extension reloads and SPA navigation.
- **Setup Extension Verifier & Automated CI Release Pipeline**:
  - Setup Wizard verifies unpacked `<InstallDir>\extension` integrity during installation and exposes Finish **Copy Path** + **Open Folder** buttons; added CI extension syntax check and silent Setup smoke testing.

### Bug Fixes
- Fixed duplicate overlapping floating download icons appearing after extension updates/reloads or dynamic React/YouTube navigation.
- Prevented the floating bar from obscuring player controls on full-height vertical videos (YouTube Shorts, Instagram Reels, TikTok).
- Fixed stale unpacked extension scripts persisting in `<InstallDir>\extension` when upgrading over an older installation.

---

## [v1.0.0] — 2026-10-30

### Highlights & New Features
- **Initial Open-Source Release (`mdm.exe` + MV3 Extension + Windows x64 Setup Wizard)**:
  - **Obsidian-Violet Dark Theme**: Pitch-dark DWM window frames, native dark scrollbars, live search, `40px` borderless rows with inline progress meters, and full developer branding.
  - **High-Speed YouTube HD/4K & Companion Audio Acceleration**: Direct InnerTube stream resolution (`144p`–`2160p 4K` + `M4A`/`Opus` audio) with burst-sliced HTTP `206` range workers over warm `KeepAlive` TLS sockets.
  - **Zero-Dependency Lossless Media Muxer**: Built-in ISO-BMFF & Matroska muxer (`MP4/fMP4 + M4A -> .mp4`, `MP4 + Opus -> .mp4`, `WebM + WebM -> .webm`, `WebM + M4A -> .mkv`) respecting DRM boundaries.
  - **Download Management Suite**: Dynamic `1–32` in-flight segment splitting, Pause/Resume, Speed Limiter, Queues, Scheduler, Site Grabber, Batch Download, `.ef2`/`.txt` Import/Export, 9-Tab Options, System Tray, Browser Fetch Relay (`403` recovery), and Native Messaging host (`com.shihab.mehedi.idm`).

### Bug Fixes
- Resolved YouTube HTTP `403` throttling and playback restrictions on companion audio streams via InnerTube client selection and parallel burst slicing.
- Fixed cross-video metadata leakage on multi-video feeds (Facebook/Instagram/X/TikTok) with strict DOM video UID isolation and one-click Reloader (`↻`).
