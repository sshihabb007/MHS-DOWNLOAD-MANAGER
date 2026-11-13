param(
    [string]$OutDir = ''
)
$ErrorActionPreference = 'Stop'
$shihabAppVersion = '1.0.6'
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

# 1. Compile x64 mdm.exe (MHS Download Manager) using .NET Framework 4.8 csc.exe with embedded Win32 & managed icons
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

& $shihabCsc /nologo /target:winexe /platform:x64 /optimize+ /highentropyva+ /langversion:5 `
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
    "/win32icon:$mehediIconIco" `
    "/resource:$mehediIconIco,ShihabMehediIDM.mdm_icon.ico" `
    "/resource:$shihabIconPng,ShihabMehediIDM.mdm_icon.png" `
    "/resource:$shihabAuthorJpg,ShihabMehediIDM.author_shihab.jpg" `
    "/out:$mehediOutExe" `
    @shihabSources

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed building mdm.exe with exit code $LASTEXITCODE"
}

$shihabHash = (Get-FileHash $mehediOutExe -Algorithm SHA256).Hash.ToLowerInvariant()
$mehediSize = (Get-Item $mehediOutExe).Length
Write-Output "BUILD SUCCESS [APP]: $mehediOutExe ($mehediSize bytes, SHA256=$shihabHash)"

# 2. Stage & Compress Embedded Installer Payload (mdm.exe + extension/ + README.md + LICENSE)
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

# Verify embedded payload zip includes mdm.exe and complete unpacked extension/ folder
$mehediPayloadInspect = [System.IO.Compression.ZipFile]::OpenRead($shihabPayloadZip)
try {
    $shihabZipNames = @($mehediPayloadInspect.Entries | ForEach-Object { $_.FullName -replace '\\', '/' })
    foreach ($mehediExpected in @(
        'mdm.exe',
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

# 3. Compile x64 Setup Wizard (MHS-Download-Manager-Setup-1.0.6-x64.exe)
Get-ChildItem -Path $mehediBinDir -Filter 'MHS-Download-Manager-Setup-*-x64.exe' -ErrorAction SilentlyContinue | Remove-Item -Force
$mehediSetupOutExe = Join-Path $mehediBinDir "MHS-Download-Manager-Setup-$shihabAppVersion-x64.exe"
$shihabSharedSafetySrc = Join-Path $shihabProjectRoot 'src\Shared\ShihabProcessAndPathSafety.cs'
$shihabInstallerSrc = Join-Path $shihabProjectRoot 'src\installer\ShihabSetupWizard.cs'

& $shihabCsc /nologo /target:winexe /platform:x64 /optimize+ /highentropyva+ /langversion:5 `
    /r:System.dll `
    /r:System.Core.dll `
    /r:System.Drawing.dll `
    /r:System.Windows.Forms.dll `
    /r:System.IO.Compression.dll `
    /r:System.IO.Compression.FileSystem.dll `
    "/win32icon:$mehediIconIco" `
    "/resource:$mehediIconIco,ShihabMehediInstaller.mdm_icon.ico" `
    "/resource:$shihabIconPng,ShihabMehediInstaller.mdm_icon.png" `
    "/resource:$shihabPayloadZip,ShihabMehediInstaller.payload.zip" `
    "/out:$mehediSetupOutExe" `
    $shihabSharedSafetySrc `
    $shihabInstallerSrc

if ($LASTEXITCODE -ne 0) {
    throw "csc.exe failed building Setup Installer with exit code $LASTEXITCODE"
}

$mehediSetupHash = (Get-FileHash $mehediSetupOutExe -Algorithm SHA256).Hash.ToLowerInvariant()
$shihabSetupSize = (Get-Item $mehediSetupOutExe).Length
Write-Output "BUILD SUCCESS [SETUP]: $mehediSetupOutExe ($shihabSetupSize bytes, SHA256=$mehediSetupHash)"
