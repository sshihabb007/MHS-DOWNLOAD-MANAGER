# Changelog — MHS Download Manager

All notable releases, new features, security enhancements, and bug fixes for **MHS Download Manager** (`mdm.exe` + Manifest V3 Browser Extension + Windows x64 Setup Wizard) are documented here in **reverse chronological order (newest first)**.

> **Live GitHub Releases:** [All Releases (`v1.1.1` → `v1.0.0`)](https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases)

---

## [v1.1.1] — 2026-11-20

### Critical Post-Update & Browser Extension Fixes
- **Fixed Zombie Loopback Bridge (`127.0.0.1:45779`) After Silent Auto-Update (`ShihabAutoUpdater`, `ShihabSetupWizard`, `BridgeHttpServer`, `ShihabProcessAndPathSafety`)**:
  - Eliminated 3-generation Win32 socket-handle inheritance (`bInheritHandles = TRUE` via `UseShellExecute = false`) where launching `Setup.exe` from the running `mdm.exe` and silently relaunching the new `mdm.exe` caused the newly updated app (and its crash watchdog) to inherit the old listening TCP handle permanently, causing `WSAEADDRINUSE (10048)` while hanging every extension request on port `45779`.
  - Marked all local TCP/UDP listeners (`BridgeHttpServer`, `ShihabRemoteControlServer`, `ShihabClickNLoadServer`, `ShihabLocalNetworkRelay`, `ShihabTorrentSeedServer`) non-inheritable via `SetHandleInformation(..., HANDLE_FLAG_INHERIT, 0)`, switched updater/setup/native-host launches to `UseShellExecute = true`, added bounded bind retry (`25 × 200ms`) + UI self-heal recovery timer so upgrades from unpatched `v1.0.7`/`v1.1.0` cleanly reclaim `:45779`, and closed `ShihabAutoUpdateDialog` cleanly on launch.
- **Fixed Native Messaging Freeze, Detached Process Launch & Update Mutex Coordination (`NativeMessagingHost`, `ShihabSetupWizard`)**:
  - Stopped passive `/shihab/ping` checks from falling back to Native Messaging or auto-spawning `mdm.exe` (preventing 37–75 second extension hangs and mid-update app respawns).
  - Bounded explicit Native Messaging auto-launch waits to `<= 3.2s`, switched `mdm.exe /fromhost` launch to `UseShellExecute = true` (preventing Chrome stdio pipe / Job Object inheritance), respected `Local\ShihabMehediIDM_InstallerActive` during in-place upgrades, and passed `--shihab-config-dir` consistently into the Native Messaging loop.
- **Fixed Frozen `"Download Video"` Pill, Stuck Popup & Missed Browser Interceptions (`mehedi_content.js`, `mehedi_popup.js`, `shihab_background.js`)**:
  - Opened the floating **`Download Video`** dropdown immediately on click using cached/DOM streams while refreshing sniffed streams asynchronously with a bounded `2.5s` timeout, and added immediate toast feedback + `5.5s` dispatch timeout so the button never freezes even if `mdm.exe` is offline or busy.
  - Added bounded timeouts (`3.5s`) and `try/catch` guards in `mehedi_popup.js` so the toolbar popup never stalls forever on `"Checking mdm.exe bridge..."`.
  - Fixed `mehediAuthenticatedBridgeFetch` to avoid burning the `1.8s` HTTP timeout on cold Service Worker token lookups before trying the pinned-origin local bridge, serialized `mehediInstallContextMenus` to eliminate duplicate menu ID runtime errors, and scoped the 10-second browser-startup capture suppression strictly to `chrome.runtime.onStartup` (not extension reloads or SW wakeups).
- **Fixed v1.1.0 Extension & Setup Regressions**:
  - Fixed `shihabUpsertTabStreamRecord` missing `shihabScopedCacheKey` (`std:`/`inc:`) when looking up sniffed media `Origin`/`Referer`/`hadCookie` headers.
  - Added a storage hydration gate (`shihabServersReadyPromise`) so cold Service Worker starts cannot wipe paired remote servers; fixed Incognito cookie-store isolation in batch context-menu downloads; fixed `magnet:?` `preventDefault()` under non-passive click listeners and cleaned up leaked `chrome.storage.onChanged` listeners; raised `/shihab/video-info` timeout to `20s`.
  - Replaced destructive `Directory.Delete(extension, true)` during upgrades with atomic in-place file replacement (`ReplaceFileAtomicallyWithRenameFallback`), preserved custom `allowed_origins` in `com.shihab.mehedi.idm.json`, logged silent update errors to `crashes/update_error.log`, and added `ie4uinit.exe` to `AllowedSystem32Tools`.
  - Fixed PID-blind `session.running` false crash reports (`ExitCode: 0x00000000`) in `ShihabCrashMonitor`, guarded hidden/minimized crash recovery dialogs from blocking startup, and cached `bridge.token` in memory in `ShihabStateStore` to eliminate per-request NTFS ACL thrashing.

---

## [v1.1.0] — 2026-11-20

### Highlights & New Features
- **Complete BitTorrent P2P & Magnet Engine Overhaul (`ShihabTorrentManager`, `ShihabTorrentDhtAndSeeding`, `ShihabBencodeParser`)**:
  - Added full `BEP-5` Kademlia UDP DHT (trackerless magnets), `BEP-6` Fast extension (`Have All`/`Have None`/`Reject`), `BEP-7` IPv6 `peers6`, `BEP-9`/`BEP-10` parallel magnet metadata resolution, `BEP-11` `ut_pex`, `BEP-12` tracker tiers, `BEP-14` LSD multicast, `BEP-15` UDP trackers with cryptographic transaction IDs, single & multi-file `BEP-19` WebSeeds, strict `BEP-27` private-torrent isolation (zero DHT/PEX/LSD leaks), `BEP-53` `so=` selective file download, Base32+Hex BTIH, and `x.pe` direct peer hints.
  - Replaced placeholder seeding with a real TCP BitTorrent seed listener (`ShihabTorrentSeedServer`) serving `BEP-3` pieces & `BEP-9` metadata with live upload speed, share ratio, and time limits.
  - Added SHA-1 hash-verified resume & corrupt-piece auto-repair, atomic `.mhs.btstate` checkpoints, fail-closed path traversal / reserved device / case-collision / v2-only validation, merged overlapping IP blocklist ranges, no-outer-folder option, **3-Tab Torrent Inspector (`Files`/Recheck, `Peers`, `Trackers`, live pieces bitmap)**, and HKCU `magnet:` / `.torrent` OS file associations in `ShihabSetupWizard`.
- **Parallel HLS, Managed Deno JS Runtime & Media Suite Upgrades**:
  - Upgraded `MehediHlsDownloader` to a bounded **8-worker parallel segment fetcher** with shared speed limiting, retries, and exact sequential reassembly.
  - Added managed **Deno JS runtime** support (`--js-runtimes` + `--cli install-deno`), `bgutil` PO-Token server detection, optional **"Use my `yt-dlp` config file"** toggle, rich codec/FPS/HDR format labels (`1080p MP4 · AV1 · 60 fps · HDR10`), **"Most compatible (H.264 + AAC)"** preset (`video-get -q compatible`), `--embed-chapters`, `--concurrent-fragments`, and **Live Stream "Stop & Save MP4"** remuxing.
- **Click'n'Load 2 Local Server (`:9666`) & 5-Provider Debrid Cloud**:
  - Added opt-in `ShihabClickNLoadServer` (`/jdcheck.js`, `/crossdomain.xml`, `/flash/add`, `/flash/addcrypted2` pure-C# AES-128-CBC decrypt, CORS + Private Network Access, DNS-rebinding `403` guard, SSRF filter, and 10 req/min rate limiting) across GUI, `--headless`, and Options.
  - Expanded Debrid cloud to **5 providers** (added **TorBox** and **Mega-Debrid** alongside Real-Debrid, AllDebrid with magnet/torrent unlock, and Premiumize) and added **Google Drive shared-folder video** resolution.
- **10 Built-In Themes, Desktop Power UX, Archive Manager & Extension Upgrades**:
  - Expanded to **10 built-in themes** (`Obsidian Dark`, `Midnight Nord`, `Emerald Matrix`, `Solarized Dark`, `Classic Light`, `Luna XP`, `Aero Glass`, `Adwaita`, `Breeze`, `High Contrast`) with dynamic `View → Theme`, luminance-derived dark/light mode, custom `.toml` WCAG contrast guard, and `90%–200%` UI scaling.
  - Added **Deferred Rename** while downloading (GUI, Bridge, CLI `--deferred-rename`, MCP), **Drag finished files OUT** of the main list, **Global Show/Hide Hotkey** (Win32 `RegisterHotKey`), **Batch "Downloads Complete" coalescing + Snooze (`30m`/`1h`/`8h`/Until restart)**, Download File Info duplicate overwrite prompt, Batch/Grabber `Save to` override, regex clipboard batch extraction, dynamic tray speed tooltip, and stealth `/fromhost` handoff.
  - Added **DPAPI Saved Archive Passwords** (MRU auto-try + Options modal), split-volume archive re-trigger, AES ZIP `7z` fallback, and **HatchZip** in-place extraction.
  - Upgraded the MV3 Browser Extension with **strict Incognito cookie-store isolation** (`inc:`/`std:` keys), **Multi-Server Remote Pairing & Switcher**, dynamic context-menu sync, **"Hide floating video button in fullscreen"** toggle, Google Drive shared-folder sniffing, and magnet/torrent link interception.
- **Expanded CLI & 16-Tool MCP Server**:
  - Added CLI `add` (with `-o`, `-f`, `-c`, `-q`, `-l`, `--no-start`, `--private`, `--checksum`, `--cookie`, `--referer`, `--user-agent`, `--deferred-rename`), `start-queue`, `stop-queue`, `install-deno`, filtered live non-JSON `status`, and expanded `--mcp` to **16 tools** (adding `stop_queue`, filtered `list_downloads` with `freeSpaceBytes`, and full `add_download` options).

### Bug Fixes & Hardening
- Fixed `bridge.token` rotation/desync between startup and local bridge authentication (`ShihabStateStore` & `MehediProgramEntry`).
- Fixed `PlainDNS` vs `"Plain"` config mismatch, Manual HTTP proxy `Proxy-Authorization` & plain-HTTP forwarding inside `ShihabLocalNetworkRelay`, and VPN/proxy bypasses across FTP, YouTube InnerTube, DoH/PAC, Debrid, and media helpers.
- Fixed RFC 5987 (`UTF-8'en'...`), RFC 2047, and `%XX` `Content-Disposition` filename decoding, `archive.org` cluster cookie redirects, own-UA `403`/`412` bot-check fallback, HTML-instead-of-file error guards, and resume overlap mismatch clean auto-restart.
- Fixed LAN Remote QR `#pair=` auto-pairing, "New Code" expiry refresh, `/api/speedlimit`, `.torrent` upload, and strict private-item exclusion from Remote Web UI & `.txt` exports.
- Fixed Options `Forget All Sign-Ins` leaving stale in-memory credentials, dead `View → Theme` menu entries, `ShihabAutomationApiEnabled` HTTP `403` enforcement, queue scheduler active-days/wake re-arm & per-queue quota, VPN kill-switch auto-resume, `--headless` queue power actions, out-of-process freeze watchdog heartbeat, and offline CLI queueing as `ShihabQueued`.

---

## [v1.0.7] — 2026-11-13

### Highlights & New Features
- **Rock-Solid Dark & Light Custom Window Chrome (`ShihabChromeForm`)**:
  - Updated `ShihabChromeForm` non-client message handling (`WM_NCCALCSIZE`, `WM_NCPAINT`, `WM_NCACTIVATE`, `WM_SETTEXT`, `WM_SETICON`, `WM_DWMCOMPOSITIONCHANGED`, `WM_THEMECHANGED`, `WM_SETTINGCHANGE`, `WM_DPICHANGED`) to prevent Windows classic steel-blue caption bars and thick outer borders from bleeding through after focus changes, tray restore, or theme switches.
  - Synchronized active/inactive border colors (`DWMWA_BORDER_COLOR` / `DWMWA_CAPTION_COLOR` / `DWMWA_TEXT_COLOR`) across `ShihabMainIdmWindow`, `ShihabDownloadProgressForm`, `MehediDownloadFileInfoForm`, and all modal/utility dialogs.
  - Added working-area `WM_GETMINMAXINFO` clipping and `WM_WINDOWPOSCHANGED` / `SetBoundsCore` restore stabilization so chromeless `WS_THICKFRAME` windows never grow by `+14×14px` across repeated minimize/maximize/restore cycles.
- **Automatic Desktop & Taskbar Icon Refresh Across Upgrades**:
  - Updated `ShihabSetupWizard` (including `--shihab-update-mode`) and `ShihabAutoUpdater` (Portable in-place updates) to refresh Desktop `.lnk` shortcuts, Start Menu shortcuts, and matching pinned Taskbar shortcuts (`User Pinned\TaskBar\*.lnk`), explicitly clear icon resource location (`IconLocation = "<exe>,0"`), and broadcast `SHChangeNotify` (`SHCNE_UPDATEITEM`, `SHCNE_UPDATEDIR`, `SHCNE_ASSOCCHANGED`) + `ie4uinit.exe -show`.
  - Fixed `ShihabThemePalette.CreateShihabAppIcon()` to return cloned `Icon` instances and explicitly apply `SM_CXSMICON` (`WM_SETICON` `ICON_SMALL`) + `SM_CXICON` (`WM_SETICON` `ICON_BIG`) window HICONs across forms so dialogs cannot dispose a shared process icon.

### Bug Fixes
- Fixed classic Windows caption `[□] [×] [?]` and mismatched thick outer border appearing over the custom dark UI after updates, deactivation (`WM_NCACTIVATE`), or window restore (`SC_RESTORE`).
- Fixed `+14×14px` cumulative window growth caused by `Form.RestoreWindowBoundsIfNecessary` re-adding `WS_THICKFRAME` insets on restore.
- Fixed stale Desktop and Taskbar icons remaining cached from earlier versions (`v1.0.5` -> `v1.0.6`) after silent or GUI upgrades.
- Fixed shared `Icon` disposal bug when closing `ShihabAutoUpdateDialog` that could revert secondary window/taskbar icons.

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
