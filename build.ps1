param(
    [string]$OutDir = '',
    [string]$SignCertPfx = '',
    [string]$SignCertPassword = '',
    [string]$SignCertThumbprint = '',
    [string]$TimestampUrl = 'http://timestamp.digicert.com',
    [string]$SignToolPath = ''
)
$ErrorActionPreference = 'Stop'
$shihabAppVersion = '1.2.3'
$shihabProjectRoot = Split-Path -Parent $MyInvocation.MyCommand.Path
$mehediBinDir = if ([string]::IsNullOrWhiteSpace($OutDir)) { Join-Path $shihabProjectRoot 'bin' } else { $OutDir }
$shihabObjDir = Join-Path $shihabProjectRoot 'obj'
$shihabAssetsDir = Join-Path $shihabProjectRoot 'assets'
$mehediIconIco = Join-Path $shihabAssetsDir 'mdm.ico'
$shihabIconPng = Join-Path $shihabAssetsDir 'mdm.png'
$shihabAuthorJpg = Join-Path $shihabAssetsDir 'author_shihab.jpg'

New-Item -ItemType Directory -Force -Path $mehediBinDir, $shihabObjDir | Out-Null

if (-not (Test-Path $mehediIconIco) -or -not (Test-Path $shihabIconPng) -or -not (Test-Path $shihabAuthorJpg)) {
    throw "Required official icon or author portrait assets missing in $shihabAssetsDir"
}

# Win32 PE hygiene helpers: embed native RT_RCDATA into .rsrc (keeping executable .text clean)
# and stamp a valid PE CheckSum via imagehlp.dll!CheckSumMappedFile before optional Authenticode signing.
if (-not ('ShihabBuildPeTools' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.IO;
using System.Runtime.InteropServices;

public static class ShihabBuildPeTools
{
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr BeginUpdateResourceW(string pFileName, bool bDeleteExistingResources);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool UpdateResourceW(IntPtr hUpdate, IntPtr lpType, string lpName, ushort wLanguage, byte[] lpData, uint cbData);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool EndUpdateResourceW(IntPtr hUpdate, bool fDiscard);

    [DllImport("imagehlp.dll", SetLastError = true)]
    private static extern IntPtr CheckSumMappedFile(byte[] baseAddress, uint fileLength, out uint headerSum, out uint checkSum);

    public static void EmbedRcDataResource(string exePath, string resourceName, byte[] payloadBytes)
    {
        IntPtr hUpdate = BeginUpdateResourceW(exePath, false);
        if (hUpdate == IntPtr.Zero)
        {
            throw new InvalidOperationException("BeginUpdateResourceW failed with Win32 error " + Marshal.GetLastWin32Error());
        }
        bool ok = false;
        try
        {
            IntPtr rtRcData = new IntPtr(10);
            if (!UpdateResourceW(hUpdate, rtRcData, resourceName, 1033, payloadBytes, (uint)payloadBytes.Length))
            {
                throw new InvalidOperationException("UpdateResourceW failed with Win32 error " + Marshal.GetLastWin32Error());
            }
            ok = EndUpdateResourceW(hUpdate, false);
            if (!ok)
            {
                throw new InvalidOperationException("EndUpdateResourceW failed with Win32 error " + Marshal.GetLastWin32Error());
            }
        }
        finally
        {
            if (!ok)
            {
                try { EndUpdateResourceW(hUpdate, true); } catch { }
            }
        }
    }

    public static uint StampPeCheckSum(string exePath)
    {
        byte[] bytes = File.ReadAllBytes(exePath);
        if (bytes.Length < 0x100) return 0u;
        int peOff = BitConverter.ToInt32(bytes, 0x3C);
        if (peOff <= 0 || peOff + 24 + 68 > bytes.Length) return 0u;
        uint headerSum;
        uint checkSum;
        IntPtr ntHdr = CheckSumMappedFile(bytes, (uint)bytes.Length, out headerSum, out checkSum);
        if (ntHdr == IntPtr.Zero) return 0u;
        byte[] csBytes = BitConverter.GetBytes(checkSum);
        Buffer.BlockCopy(csBytes, 0, bytes, peOff + 24 + 64, 4);
        File.WriteAllBytes(exePath, bytes);
        return checkSum;
    }
}
'@
}

function Resolve-ShihabSignTool {
    param([string]$ExplicitPath)
    if (-not [string]::IsNullOrWhiteSpace($ExplicitPath) -and (Test-Path -LiteralPath $ExplicitPath)) {
        return $ExplicitPath
    }
    $cmd = Get-Command signtool.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $kitsRoot = 'C:\Program Files (x86)\Windows Kits\10\bin'
    if (Test-Path -LiteralPath $kitsRoot) {
        $found = Get-ChildItem -Path $kitsRoot -Recurse -Filter 'signtool.exe' -ErrorAction SilentlyContinue |
            Where-Object { $_.FullName -like '*\x64\signtool.exe' } |
            Sort-Object FullName -Descending |
            Select-Object -First 1
        if ($found) { return $found.FullName }
    }
    return ''
}

function Invoke-ShihabOptionalSign {
    param(
        [string]$FilePath,
        [string]$Description
    )
    $pfxPath = if (-not [string]::IsNullOrWhiteSpace($SignCertPfx)) { $SignCertPfx } else { $env:MHS_SIGN_PFX_PATH }
    $pfxPass = if (-not [string]::IsNullOrWhiteSpace($SignCertPassword)) { $SignCertPassword } else { $env:MHS_SIGN_PFX_PASSWORD }
    $thumb   = if (-not [string]::IsNullOrWhiteSpace($SignCertThumbprint)) { $SignCertThumbprint } else { $env:MHS_SIGN_THUMBPRINT }
    if ([string]::IsNullOrWhiteSpace($pfxPath) -and [string]::IsNullOrWhiteSpace($thumb)) {
        return
    }
    $st = Resolve-ShihabSignTool -ExplicitPath $SignToolPath
    if ([string]::IsNullOrWhiteSpace($st)) {
        throw "Authenticode signing was requested, but signtool.exe was not found."
    }
    $signArgs = @('sign', '/fd', 'SHA256', '/tr', $TimestampUrl, '/td', 'SHA256', '/d', $Description, '/du', 'https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER')
    if (-not [string]::IsNullOrWhiteSpace($pfxPath)) {
        $signArgs += @('/f', $pfxPath)
        if (-not [string]::IsNullOrWhiteSpace($pfxPass)) {
            $signArgs += @('/p', $pfxPass)
        }
    } else {
        $signArgs += @('/sha1', $thumb)
    }
    $signArgs += $FilePath
    & $st @signArgs
    if ($LASTEXITCODE -ne 0) {
        throw "signtool sign failed for $FilePath with exit code $LASTEXITCODE"
    }
    & $st verify /pa /v $FilePath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "signtool verify failed for $FilePath with exit code $LASTEXITCODE"
    }
    Write-Output "SIGNED & VERIFIED: $FilePath"
}

# 1. Compile x64 mdm.exe (MHS Download Manager) using .NET Framework 4.8 csc.exe
$shihabCsc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
$mehediOutExe = Join-Path $mehediBinDir 'mdm.exe'
$shihabSrcRoot = Join-Path $shihabProjectRoot 'src'
$shihabInstallerDir = Join-Path $shihabSrcRoot 'installer'
$shihabSources = @(
    Get-ChildItem -Path $shihabSrcRoot -Filter '*.cs' -Recurse -File |
    Where-Object { -not $_.FullName.StartsWith($shihabInstallerDir + [System.IO.Path]::DirectorySeparatorChar, [System.StringComparison]::OrdinalIgnoreCase) } |
    Sort-Object FullName |
    ForEach-Object { $_.FullName }
)
if ($shihabSources.Count -lt 10) {
    throw "Expected modular C# sources under $shihabSrcRoot, found $($shihabSources.Count)"
}

$shihabAppManifest = Join-Path $shihabSrcRoot 'mdm.manifest'
if (-not (Test-Path $shihabAppManifest)) {
    throw "Required Win32 application manifest missing: $shihabAppManifest"
}

& $shihabCsc /nologo /target:winexe /platform:x64 /optimize+ /highentropyva+ /subsystemversion:6.00 /langversion:5 `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.Runtime.Serialization.dll `
    /r:System.Security.dll `
    /r:System.IO.Compression.dll `
    /r:System.IO.Compression.FileSystem.dll `
    /r:System.Xml.dll `
    /r:Microsoft.VisualBasic.dll `
    "/win32manifest:$shihabAppManifest" `
    "/win32icon:$mehediIconIco" `
    "/resource:$mehediIconIco,ShihabMehediIDM.mdm_icon.ico" `
    "/resource:$shihabIconPng,ShihabMehediIDM.mdm_icon.png" `
    "/resource:$shihabAuthorJpg,ShihabMehediIDM.author_shihab.jpg" `
    "/out:$mehediOutExe" `
    @shihabSources

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed building mdm.exe with exit code $LASTEXITCODE"
}

[void][ShihabBuildPeTools]::StampPeCheckSum($mehediOutExe)
Invoke-ShihabOptionalSign -FilePath $mehediOutExe -Description "MHS Download Manager $shihabAppVersion"

$shihabHash = (Get-FileHash $mehediOutExe -Algorithm SHA256).Hash.ToLowerInvariant()
$mehediSize = (Get-Item $mehediOutExe).Length
Write-Output "BUILD SUCCESS [APP]: $mehediOutExe ($mehediSize bytes, SHA256=$shihabHash)"

# 2. Compile dedicated, payload-free x64 Uninstall.exe (eliminates Setup self-copying heuristic)
$shihabSharedSafetySrc = Join-Path $shihabProjectRoot 'src\Shared\ShihabProcessAndPathSafety.cs'
$shihabInstallerSrc = Join-Path $shihabProjectRoot 'src\installer\ShihabSetupWizard.cs'
$shihabSetupManifest = Join-Path $shihabProjectRoot 'src\installer\setup.manifest'
if (-not (Test-Path $shihabSetupManifest)) {
    throw "Required Setup manifest missing: $shihabSetupManifest"
}

$mehediUninstallOutExe = Join-Path $shihabObjDir 'Uninstall.exe'
if (Test-Path -LiteralPath $mehediUninstallOutExe) {
    Remove-Item -LiteralPath $mehediUninstallOutExe -Force
}

& $shihabCsc /nologo /target:winexe /platform:x64 /optimize+ /highentropyva+ /subsystemversion:6.00 /langversion:5 `
    "/define:SHIHAB_INSTALLER;SHIHAB_UNINSTALLER_ONLY" `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.IO.Compression.dll `
    /r:System.IO.Compression.FileSystem.dll `
    "/win32manifest:$shihabSetupManifest" `
    "/win32icon:$mehediIconIco" `
    "/out:$mehediUninstallOutExe" `
    $shihabSharedSafetySrc `
    $shihabInstallerSrc

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed building Uninstall.exe with exit code $LASTEXITCODE"
}

[void][ShihabBuildPeTools]::StampPeCheckSum($mehediUninstallOutExe)
Invoke-ShihabOptionalSign -FilePath $mehediUninstallOutExe -Description "MHS Download Manager $shihabAppVersion Uninstaller"
Write-Output "BUILD SUCCESS [UNINSTALLER]: $mehediUninstallOutExe ($((Get-Item $mehediUninstallOutExe).Length) bytes)"

# 3. Stage & Package Installer Payload (signed mdm.exe + signed Uninstall.exe + extension/ + README.md + LICENSE)
Add-Type -AssemblyName System.IO.Compression
Add-Type -AssemblyName System.IO.Compression.FileSystem

$mehediPayloadStage = Join-Path $shihabObjDir 'installer_stage'
$shihabPayloadZip = Join-Path $shihabObjDir 'shihab_installer_payload.zip'
if (Test-Path $mehediPayloadStage) {
    Remove-Item -LiteralPath $mehediPayloadStage -Recurse -Force
}
if (Test-Path $shihabPayloadZip) {
    Remove-Item -LiteralPath $shihabPayloadZip -Force
}

New-Item -ItemType Directory -Force -Path $mehediPayloadStage | Out-Null
Copy-Item -LiteralPath $mehediOutExe -Destination (Join-Path $mehediPayloadStage 'mdm.exe') -Force
Copy-Item -LiteralPath $mehediUninstallOutExe -Destination (Join-Path $mehediPayloadStage 'Uninstall.exe') -Force
Copy-Item -LiteralPath (Join-Path $shihabProjectRoot 'extension') -Destination (Join-Path $mehediPayloadStage 'extension') -Recurse -Force
if (Test-Path (Join-Path $shihabProjectRoot 'README.md')) {
    Copy-Item -LiteralPath (Join-Path $shihabProjectRoot 'README.md') -Destination (Join-Path $mehediPayloadStage 'README.md') -Force
}
if (Test-Path (Join-Path $shihabProjectRoot 'LICENSE')) {
    Copy-Item -LiteralPath (Join-Path $shihabProjectRoot 'LICENSE') -Destination (Join-Path $mehediPayloadStage 'LICENSE') -Force
}

[System.IO.Compression.ZipFile]::CreateFromDirectory(
    $mehediPayloadStage,
    $shihabPayloadZip,
    [System.IO.Compression.CompressionLevel]::Optimal,
    $false)
Remove-Item -LiteralPath $mehediPayloadStage -Recurse -Force -ErrorAction SilentlyContinue

# Verify embedded payload zip includes mdm.exe, Uninstall.exe, and complete unpacked extension/ folder
$mehediPayloadInspect = [System.IO.Compression.ZipFile]::OpenRead($shihabPayloadZip)
try {
    $shihabZipNames = @($mehediPayloadInspect.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
    foreach ($mehediExpected in @(
        'mdm.exe',
        'Uninstall.exe',
        'extension/manifest.json',
        'extension/manifest.firefox.json',
        'extension/mehedi_content.js',
        'extension/mehedi_page_hook.js',
        'extension/shihab_background.js',
        'extension/shihab_popup.html',
        'extension/shihab_popup.css',
        'extension/mehedi_popup.js',
        'extension/shihab_captured.html',
        'extension/mehedi_captured.js',
        'extension/images/shihab_logo16.png',
        'extension/images/shihab_logo24.png',
        'extension/images/shihab_logo32.png',
        'extension/images/shihab_logo48.png',
        'extension/images/shihab_logo128.png',
        'extension/images/author_shihab.jpg'
    )) {
        if ($shihabZipNames -notcontains $mehediExpected) {
            throw "Installer payload zip missing required entry: $mehediExpected"
        }
    }
} finally {
    $mehediPayloadInspect.Dispose()
}

# 4. Compile x64 Setup Wizard (MHS-Download-Manager-Setup-1.2.3-x64.exe)
# Executable .text contains strictly compiled IL code (zero high-entropy compressed managed blobs);
# the install payload is embedded into the non-executable Win32 .rsrc section as RT_RCDATA ("MHS_INSTALL_PAYLOAD").
Get-ChildItem -Path $mehediBinDir -Filter 'MHS-Download-Manager-Setup-*-x64.exe' -ErrorAction SilentlyContinue | Remove-Item -Force
$mehediSetupOutExe = Join-Path $mehediBinDir "MHS-Download-Manager-Setup-$shihabAppVersion-x64.exe"

& $shihabCsc /nologo /target:winexe /platform:x64 /optimize+ /highentropyva+ /subsystemversion:6.00 /langversion:5 `
    /define:SHIHAB_INSTALLER `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.IO.Compression.dll `
    /r:System.IO.Compression.FileSystem.dll `
    "/win32manifest:$shihabSetupManifest" `
    "/win32icon:$mehediIconIco" `
    "/out:$mehediSetupOutExe" `
    $shihabSharedSafetySrc `
    $shihabInstallerSrc

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed building Setup Installer with exit code $LASTEXITCODE"
}

$payloadBytes = [System.IO.File]::ReadAllBytes($shihabPayloadZip)
[ShihabBuildPeTools]::EmbedRcDataResource($mehediSetupOutExe, 'MHS_INSTALL_PAYLOAD', $payloadBytes)
[void][ShihabBuildPeTools]::StampPeCheckSum($mehediSetupOutExe)
Invoke-ShihabOptionalSign -FilePath $mehediSetupOutExe -Description "MHS Download Manager $shihabAppVersion Setup"

$mehediSetupHash = (Get-FileHash $mehediSetupOutExe -Algorithm SHA256).Hash.ToLowerInvariant()
$shihabSetupSize = (Get-Item $mehediSetupOutExe).Length
Write-Output "BUILD SUCCESS [SETUP]: $mehediSetupOutExe ($shihabSetupSize bytes, SHA256=$mehediSetupHash)"
