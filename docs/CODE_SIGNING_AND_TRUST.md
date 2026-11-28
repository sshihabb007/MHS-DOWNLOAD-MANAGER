# Code Signing, SmartScreen & Antivirus Trust Guide

## Why New-Device Downloads Show "Harmful / Malicious File" Warnings

When a Windows executable (`.exe`) is downloaded from GitHub Releases onto a brand-new Windows device, browsers attach a Mark of the Web (`Zone.Identifier`) and run **three independent trust checks**:

1. **Microsoft Defender Static ML & Behavioral Heuristics** — inspects PE structure, executable section entropy, embedded resources, API imports, and install/uninstall behavior.
2. **Windows Defender SmartScreen** — checks publisher Authenticode identity and historical download prevalence for that SHA-256 / publisher certificate.
3. **Google Chrome / Edge Safe Browsing** — checks Authenticode publisher reputation and download prevalence on `github.com` / `release-assets.githubusercontent.com`.

### What Is Already Fixed in the Installer & Build Pipeline

- **Clean executable `.text` section:** The installation payload is now stored in the non-executable Win32 `.rsrc` resource section (`RT_RCDATA`), dropping executable `.text` entropy from `~7.96` (packed/dropper-like) down to normal compiled IL (`~5.59` bits/byte).
- **Dedicated payload-free `Uninstall.exe`:** Setup no longer self-copies its own installer executable onto disk.
- **Standard `asInvoker` + UAC worker elevation:** Completely removed `OpenProcessToken` / `DuplicateTokenEx` / `CreateProcessWithTokenW` token manipulation and `IShellWindows` late-bound COM reflection.
- **Native Win32 APIs everywhere:** Replaced `cmd.exe /c ... ping 127.0.0.1 ... del /f /q` self-deletion with `MoveFileExW(..., MOVEFILE_DELAY_UNTIL_REBOOT)`, replaced `WScript.Shell` reflection with strongly-typed `[ComImport] IShellLinkW` + `IPersistFile`, removed hidden `ie4uinit.exe` spawns, removed unused `powershell.exe` / `rundll32.exe` / `cmd.exe` strings from Setup, and replaced `Process.MainModule` (`PROCESS_VM_READ`) with `QueryFullProcessImageNameW` (`PROCESS_QUERY_LIMITED_INFORMATION`).
- **Modern PE headers & provenance:** `/subsystemversion:6.00`, valid PE `CheckSum` stamped via `imagehlp.dll`, consistent Assembly metadata, and GitHub SLSA Build Provenance Attestations (`actions/attest-build-provenance`).

---

## Final Mandatory Step for Zero Warnings on Brand-New Devices: Authenticode Code Signing

Even a 100% clean PE executable will still trigger Chrome Safe Browsing *"Uncommon download"* or Windows SmartScreen *"Unknown publisher"* on a brand-new machine **until it is signed with a publicly trusted Authenticode certificate**.

### Option 1 (Recommended & Free for Public MIT Open Source): SignPath Foundation
[SignPath Foundation](https://signpath.org/) provides **free** HSM-backed Authenticode code signing for open-source projects under OSI-approved licenses (including MIT) hosted publicly on GitHub, available worldwide.

1. Apply at https://signpath.org/ for `sshihabb007/MHS-DOWNLOAD-MANAGER`.
2. Once approved, connect the GitHub Actions integration (`signpath/github-action-submit-signing-request`) so CI signs `mdm.exe` and `Uninstall.exe` first, packages Setup, and signs `MHS-Download-Manager-Setup-*-x64.exe` and `MHS-Download-Manager-Portable-*-x64.exe`.

### Option 2: Microsoft Azure Artifact Signing (Trusted Signing)
If you hold an eligible individual/organization identity in a supported region, [Azure Artifact Signing](https://learn.microsoft.com/en-us/azure/artifact-signing/) issues short-lived certificates directly recognized by Windows Defender SmartScreen.

### Option 3: Bring-Your-Own Authenticode Certificate in GitHub Actions (Already Wired!)
`.github/workflows/build.yml` and `build.ps1` already support automated Authenticode signing with RFC 3161 SHA-256 timestamping out of the box:

1. Add these GitHub Repository Secrets (**Settings → Secrets and variables → Actions**):
   - `MHS_CODESIGN_PFX_BASE64`: Base64-encoded `.pfx` code-signing certificate (`[Convert]::ToBase64String([IO.File]::ReadAllBytes("cert.pfx"))`).
   - `MHS_CODESIGN_PFX_PASSWORD`: Certificate export password.
2. Optional Repository Variable:
   - `MHS_CODESIGN_TIMESTAMP_URL` (defaults to `http://timestamp.digicert.com`).
3. Or sign locally with `build.ps1`:
   ```powershell
   .\build.ps1 -SignCertPfx "C:\path\to\cert.pfx" -SignCertPassword "..."
   ```
   `build.ps1` automatically signs inner `mdm.exe` and `Uninstall.exe` **before** packaging, then signs `MHS-Download-Manager-Setup-*-x64.exe`, and verifies every signature with `signtool verify /pa /v`.

---

## Clearing Any Residual False Positives After a Release

After publishing a tagged GitHub Release:

1. **Microsoft Security Intelligence (Defender / SmartScreen):** Submit the exact released `MHS-Download-Manager-Setup-*-x64.exe` and `MHS-Download-Manager-Portable-*-x64.exe` as **Software Developer** at https://www.microsoft.com/en-us/wdsi/filesubmission (select *Incorrectly detected as malware/PUA*).
2. **SmartScreen URL Feedback:** Report any flagged release download URL at https://feedback.smartscreen.microsoft.com/feedback.aspx.
3. **Google Safe Browsing False-Positive Report:** If Chrome flags the GitHub Release URL, submit at https://safebrowsing.google.com/safebrowsing/report_badware/.
4. **Avoid Recreating Published Releases:** Keep `force_recreate_release: false` so every release keeps a stable SHA-256 and builds cumulative SmartScreen / Safe Browsing reputation.
