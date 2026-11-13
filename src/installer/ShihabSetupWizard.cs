using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using ShihabMehediShared;

[assembly: AssemblyTitle("MHS Download Manager 1.0.7 Setup (Mehedi Hasan Shihab)")]
[assembly: AssemblyDescription("Official Windows x64 Installer & Uninstaller for MHS Download Manager 1.0.7 (Mehedi Hasan Shihab)")]
[assembly: AssemblyCompany("Mehedi Hasan Shihab")]
[assembly: AssemblyProduct("MHS Download Manager Setup")]
[assembly: AssemblyCopyright("Copyright (c) 2026 Mehedi Hasan Shihab")]
[assembly: AssemblyVersion("1.0.7.0")]
[assembly: AssemblyFileVersion("1.0.7.0")]

namespace ShihabMehediInstaller
{
    public sealed class ShihabInstallOptions
    {
        public string MehediInstallDirectory { get; set; }
        public bool ShihabCreateDesktopShortcut { get; set; }
        public bool MehediCreateStartMenuShortcuts { get; set; }
        public bool ShihabLaunchOnStartup { get; set; }
        public bool MehediRegisterNativeHost { get; set; }
        public bool ShihabSkipRegistryAndShortcuts { get; set; }
        public bool ShihabInPlaceUpdateMode { get; set; }
        public int ShihabWaitForPid { get; set; }
        public bool ShihabRelaunchAfterInstall { get; set; }
        public string ShihabUpdatedFromVersion { get; set; }

        public ShihabInstallOptions()
        {
            string shihabLocalAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            MehediInstallDirectory = Path.Combine(Path.Combine(shihabLocalAppData, "Programs"), "MHS Download Manager");
            ShihabCreateDesktopShortcut = true;
            MehediCreateStartMenuShortcuts = true;
            ShihabLaunchOnStartup = true;
            MehediRegisterNativeHost = true;
            ShihabSkipRegistryAndShortcuts = false;
            ShihabInPlaceUpdateMode = false;
            ShihabWaitForPid = 0;
            ShihabRelaunchAfterInstall = false;
            ShihabUpdatedFromVersion = "";
        }
    }

    public static class ShihabSetupTheme
    {
        public const string ShihabProductTitle = "MHS Download Manager 1.0.7 (Mehedi Hasan Shihab)";
        public const string MehediVersionText = "1.0.7";
        public const string ShihabUninstallRegKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MHSDownloadManager";
        public const string MehediRunRegKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        public const string ShihabRunValueName = "MHSDownloadManager";

        [DllImport("dwmapi.dll", EntryPoint = "DwmSetWindowAttribute")]
        private static extern int SetMehediDwmAttribute(IntPtr shihabHwnd, int mehediAttr, ref int shihabValue, int mehediSize);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
        private static extern void SHChangeNotify(int wEventId, uint uFlags, string dwItem1, string dwItem2);

        private static Icon _shihabCachedIcon;
        private static Bitmap _mehediCachedLogo;

        public static void RefreshWindowsShellIconCache(string[] updatedFilePaths, string[] updatedDirPaths)
        {
            const int SHCNE_UPDATEDIR = 0x00001000;
            const int SHCNE_UPDATEITEM = 0x00002000;
            const int SHCNE_ASSOCCHANGED = 0x08000000;
            const uint SHCNF_IDLIST = 0x0000u;
            const uint SHCNF_PATHW = 0x0005u;
            const uint SHCNF_FLUSH = 0x1000u;

            try
            {
                if (updatedFilePaths != null)
                {
                    for (int i = 0; i < updatedFilePaths.Length; i++)
                    {
                        string fp = updatedFilePaths[i];
                        if (!string.IsNullOrWhiteSpace(fp))
                        {
                            SHChangeNotify(SHCNE_UPDATEITEM, SHCNF_PATHW | SHCNF_FLUSH, fp, null);
                        }
                    }
                }
                if (updatedDirPaths != null)
                {
                    for (int i = 0; i < updatedDirPaths.Length; i++)
                    {
                        string dp = updatedDirPaths[i];
                        if (!string.IsNullOrWhiteSpace(dp))
                        {
                            SHChangeNotify(SHCNE_UPDATEDIR, SHCNF_PATHW | SHCNF_FLUSH, dp, null);
                        }
                    }
                }
                SHChangeNotify(SHCNE_ASSOCCHANGED, SHCNF_IDLIST | SHCNF_FLUSH, null, null);
            }
            catch { }

            try
            {
                string ie4uinit = ShihabProcessAndPathSafety.ResolveSystem32ToolPath("ie4uinit.exe");
                if (!string.IsNullOrEmpty(ie4uinit) && File.Exists(ie4uinit))
                {
                    ProcessStartInfo psi = new ProcessStartInfo(ie4uinit, "-show");
                    psi.UseShellExecute = false;
                    psi.CreateNoWindow = true;
                    using (Process p = Process.Start(psi))
                    {
                        if (p != null) p.WaitForExit(2500);
                    }
                }
            }
            catch { }
        }

        public static Color GetMehediWindowBg() { return Color.FromArgb(18, 19, 25); }
        public static Color GetShihabCardBg() { return Color.FromArgb(24, 26, 36); }
        public static Color GetMehediInputBg() { return Color.FromArgb(29, 32, 45); }
        public static Color GetShihabBorderCol() { return Color.FromArgb(44, 49, 68); }
        public static Color GetMehediPrimaryText() { return Color.FromArgb(236, 240, 250); }
        public static Color GetShihabMutedText() { return Color.FromArgb(148, 156, 182); }
        public static Color GetMehediAccentViolet() { return Color.FromArgb(124, 92, 255); }
        public static Color GetShihabAccentGreen() { return Color.FromArgb(16, 185, 129); }
        public static Color GetMehediAccentRed() { return Color.FromArgb(239, 68, 68); }

        public static void ApplyShihabDarkTitleBar(Form mehediForm)
        {
            if (mehediForm == null) return;
            EventHandler shihabHandler = delegate
            {
                try
                {
                    if (!mehediForm.IsHandleCreated) return;
                    int mehediDark = 1;
                    SetMehediDwmAttribute(mehediForm.Handle, 20, ref mehediDark, sizeof(int));
                    SetMehediDwmAttribute(mehediForm.Handle, 19, ref mehediDark, sizeof(int));
                    int roundPref = 2;
                    SetMehediDwmAttribute(mehediForm.Handle, 33, ref roundPref, sizeof(int));
                    int shihabBorderRef = (68 << 16) | (49 << 8) | 44;
                    SetMehediDwmAttribute(mehediForm.Handle, 34, ref shihabBorderRef, sizeof(int));
                    int shihabCaptionRef = (25 << 16) | (19 << 8) | 18;
                    SetMehediDwmAttribute(mehediForm.Handle, 35, ref shihabCaptionRef, sizeof(int));
                    int mehediTextRef = (250 << 16) | (240 << 8) | 236;
                    SetMehediDwmAttribute(mehediForm.Handle, 36, ref mehediTextRef, sizeof(int));
                    Icon ico = LoadShihabEmbeddedIcon();
                    if (ico != null)
                    {
                        SendMessage(mehediForm.Handle, 0x0080, IntPtr.Zero, ico.Handle);
                        SendMessage(mehediForm.Handle, 0x0080, new IntPtr(1), ico.Handle);
                    }
                }
                catch { }
            };
            if (mehediForm.IsHandleCreated) shihabHandler(mehediForm, EventArgs.Empty);
            mehediForm.HandleCreated += shihabHandler;
            mehediForm.Shown += shihabHandler;
        }

        public static Icon LoadShihabEmbeddedIcon()
        {
            if (_shihabCachedIcon != null) return _shihabCachedIcon;
            try
            {
                Stream mehediStream = typeof(ShihabSetupTheme).Assembly.GetManifestResourceStream("ShihabMehediInstaller.mdm_icon.ico");
                if (mehediStream != null)
                {
                    using (mehediStream)
                    {
                        _shihabCachedIcon = new Icon(mehediStream);
                        return _shihabCachedIcon;
                    }
                }
            }
            catch { }
            return SystemIcons.Application;
        }

        public static Bitmap LoadMehediLogoBitmap(int shihabSize)
        {
            if (_mehediCachedLogo == null)
            {
                try
                {
                    Stream shihabStream = typeof(ShihabSetupTheme).Assembly.GetManifestResourceStream("ShihabMehediInstaller.mdm_icon.png");
                    if (shihabStream != null)
                    {
                        using (shihabStream)
                        using (Image mehediImg = Image.FromStream(shihabStream))
                        {
                            _mehediCachedLogo = new Bitmap(mehediImg);
                        }
                    }
                }
                catch { }
            }
            int mehediDim = Math.Max(16, shihabSize);
            Bitmap shihabBmp = new Bitmap(mehediDim, mehediDim);
            using (Graphics mehediG = Graphics.FromImage(shihabBmp))
            {
                mehediG.SmoothingMode = SmoothingMode.HighQuality;
                mehediG.InterpolationMode = InterpolationMode.HighQualityBicubic;
                if (_mehediCachedLogo != null)
                {
                    mehediG.DrawImage(_mehediCachedLogo, new Rectangle(0, 0, mehediDim, mehediDim));
                }
                else
                {
                    mehediG.DrawIcon(LoadShihabEmbeddedIcon(), new Rectangle(0, 0, mehediDim, mehediDim));
                }
            }
            return shihabBmp;
        }

        public static Button CreateShihabButton(string mehediText, Color shihabBg, Color mehediFg, int shihabWidth)
        {
            Button mehediBtn = new Button();
            mehediBtn.Text = mehediText;
            mehediBtn.Size = new Size(shihabWidth, 34);
            mehediBtn.FlatStyle = FlatStyle.Flat;
            mehediBtn.FlatAppearance.BorderSize = 1;
            mehediBtn.FlatAppearance.BorderColor = Color.FromArgb(56, 62, 86);
            mehediBtn.FlatAppearance.MouseOverBackColor = Color.FromArgb(
                Math.Min(255, shihabBg.R + 16),
                Math.Min(255, shihabBg.G + 16),
                Math.Min(255, shihabBg.B + 20));
            mehediBtn.BackColor = shihabBg;
            mehediBtn.ForeColor = mehediFg;
            mehediBtn.Font = new Font("Segoe UI Semibold", 9.2f);
            mehediBtn.Cursor = Cursors.Hand;
            return mehediBtn;
        }
    }

    public sealed class ShihabGradientProgressPanel : Panel
    {
        private readonly Font _shihabCaptionFont;
        public int MehediPercent { get; set; }
        public string ShihabCaption { get; set; }

        public ShihabGradientProgressPanel()
        {
            DoubleBuffered = true;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
            _shihabCaptionFont = new Font("Segoe UI Semibold", 8.8f);
            ShihabCaption = "0%";
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                try { _shihabCaptionFont.Dispose(); } catch { }
            }
            base.Dispose(disposing);
        }

        protected override void OnPaint(PaintEventArgs shihabE)
        {
            Graphics mehediG = shihabE.Graphics;
            mehediG.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle shihabRect = ClientRectangle;
            using (SolidBrush mehediBg = new SolidBrush(ShihabSetupTheme.GetMehediInputBg()))
            {
                mehediG.FillRectangle(mehediBg, shihabRect);
            }

            int mehediClamped = Math.Max(0, Math.Min(100, MehediPercent));
            int shihabFillW = (shihabRect.Width * mehediClamped) / 100;
            if (shihabFillW > 1)
            {
                using (LinearGradientBrush mehediGrad = new LinearGradientBrush(
                    new Rectangle(0, 0, Math.Max(4, shihabRect.Width), Math.Max(4, shihabRect.Height)),
                    Color.FromArgb(56, 189, 248),
                    Color.FromArgb(124, 92, 255),
                    0f))
                {
                    mehediG.FillRectangle(mehediGrad, 0, 0, shihabFillW, shihabRect.Height);
                }
            }

            using (Pen shihabBorder = new Pen(ShihabSetupTheme.GetShihabBorderCol(), 1f))
            {
                mehediG.DrawRectangle(shihabBorder, 0, 0, shihabRect.Width - 1, shihabRect.Height - 1);
            }

            TextRenderer.DrawText(
                mehediG,
                ShihabCaption ?? (mehediClamped + "%"),
                _shihabCaptionFont,
                shihabRect,
                Color.White,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);
        }
    }

    public static class MehediInstallerEngine
    {
        public static bool IsShihabProcessElevated()
        {
            try
            {
                WindowsIdentity mehediId = WindowsIdentity.GetCurrent();
                WindowsPrincipal shihabPrincipal = new WindowsPrincipal(mehediId);
                return shihabPrincipal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        public static bool CanWriteShihabTargetDirectory(string mehediTargetDir)
        {
            try
            {
                string shihabFull = Path.GetFullPath(mehediTargetDir);
                string mehediProbeDir = shihabFull;
                while (!string.IsNullOrEmpty(mehediProbeDir) && !Directory.Exists(mehediProbeDir))
                {
                    mehediProbeDir = Path.GetDirectoryName(mehediProbeDir);
                }
                if (string.IsNullOrEmpty(mehediProbeDir)) return false;

                Directory.CreateDirectory(shihabFull);
                string shihabProbeFile = Path.Combine(shihabFull, ".shihab_write_probe_" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(shihabProbeFile, "ok");
                File.Delete(shihabProbeFile);
                return true;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static string ComputeShihabChromiumUnpackedExtensionId(string mehediExtensionDirPath)
        {
            return ShihabProcessAndPathSafety.ComputeChromiumUnpackedExtensionId(mehediExtensionDirPath);
        }

        private static int ResolveConfiguredLocalBridgePort()
        {
            int port = 45779;
            try
            {
                string dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShihabMehediIDM");
                string cfgPath = Path.Combine(dataDir, "shihab_config.json");
                if (File.Exists(cfgPath))
                {
                    string json = File.ReadAllText(cfgPath, Encoding.UTF8);
                    Match m = Regex.Match(json, "\"MehediLocalBridgePort\"\\s*:\\s*(\\d+)", RegexOptions.CultureInvariant);
                    int parsed;
                    if (m.Success && int.TryParse(m.Groups[1].Value, out parsed) && parsed >= 1024 && parsed <= 65535)
                    {
                        port = parsed;
                    }
                }
            }
            catch { }
            return port;
        }

        private static void TryRequestGracefulBridgeExit()
        {
            try
            {
                string dataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShihabMehediIDM");
                string token = "";
                string tokenPath = Path.Combine(dataDir, "bridge.token");
                if (File.Exists(tokenPath))
                {
                    token = (File.ReadAllText(tokenPath, Encoding.UTF8) ?? "").Trim();
                }
                int port = ResolveConfiguredLocalBridgePort();
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:" + port + "/shihab/exit");
                req.Method = "POST";
                req.Timeout = 1500;
                req.ReadWriteTimeout = 1500;
                req.Proxy = null;
                req.KeepAlive = false;
                req.ContentLength = 0;
                if (!string.IsNullOrEmpty(token))
                {
                    req.Headers["X-MHS-Bridge-Token"] = token;
                }
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse()) { }
            }
            catch { }
        }

        public static void StopOwnedShihabAppInDirectory(string mehediInstallDir, Action<string> shihabLog)
        {
            string mehediTargetExe = Path.GetFullPath(Path.Combine(mehediInstallDir, "mdm.exe"));
            Process[] shihabInitial = Process.GetProcessesByName("mdm");
            bool anyMatch = false;
            for (int i = 0; i < shihabInitial.Length; i++)
            {
                try
                {
                    string pPath = shihabInitial[i].MainModule != null ? Path.GetFullPath(shihabInitial[i].MainModule.FileName) : "";
                    if (string.Equals(pPath, mehediTargetExe, StringComparison.OrdinalIgnoreCase))
                    {
                        anyMatch = true;
                    }
                }
                catch { }
                finally
                {
                    shihabInitial[i].Dispose();
                }
            }

            if (anyMatch)
            {
                if (shihabLog != null) shihabLog("Requesting graceful shutdown of running mdm.exe...");
                TryRequestGracefulBridgeExit();
                Stopwatch sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < 3500)
                {
                    bool stillRunning = false;
                    Process[] checkProcs = Process.GetProcessesByName("mdm");
                    for (int c = 0; c < checkProcs.Length; c++)
                    {
                        try
                        {
                            string cp = checkProcs[c].MainModule != null ? Path.GetFullPath(checkProcs[c].MainModule.FileName) : "";
                            if (string.Equals(cp, mehediTargetExe, StringComparison.OrdinalIgnoreCase) && !checkProcs[c].HasExited)
                            {
                                stillRunning = true;
                            }
                        }
                        catch { }
                        finally { checkProcs[c].Dispose(); }
                    }
                    if (!stillRunning) return;
                    Thread.Sleep(150);
                }
            }

            Process[] shihabProcs = Process.GetProcessesByName("mdm");
            for (int mehediI = 0; mehediI < shihabProcs.Length; mehediI++)
            {
                Process shihabP = shihabProcs[mehediI];
                try
                {
                    string mehediProcPath = shihabP.MainModule != null ? Path.GetFullPath(shihabP.MainModule.FileName) : "";
                    if (string.Equals(mehediProcPath, mehediTargetExe, StringComparison.OrdinalIgnoreCase))
                    {
                        if (shihabLog != null) shihabLog("Stopping running mdm.exe instance (PID " + shihabP.Id + ")...");
                        try { shihabP.CloseMainWindow(); } catch { }
                        if (!shihabP.WaitForExit(1500))
                        {
                            shihabP.Kill();
                            shihabP.WaitForExit(4000);
                        }
                    }
                }
                catch { }
                finally
                {
                    shihabP.Dispose();
                }
            }
        }

        public static void ExecuteShihabInstallation(
            ShihabInstallOptions mehediOpts,
            Action<int, string> shihabProgressCallback)
        {
            string shihabInstallRoot = Path.GetFullPath(mehediOpts.MehediInstallDirectory.Trim());
            string shihabInstalledExe = Path.Combine(shihabInstallRoot, "mdm.exe");

            Action<int, string> mehediReport = delegate(int shihabPct, string mehediMsg)
            {
                if (shihabProgressCallback != null)
                {
                    shihabProgressCallback(shihabPct, mehediMsg);
                }
            };

            mehediReport(5, "Preparing installation directory: " + shihabInstallRoot);
            Directory.CreateDirectory(shihabInstallRoot);

            if (mehediOpts.ShihabWaitForPid > 0)
            {
                if (ShihabProcessAndPathSafety.TryVerifyProcessIdentity(mehediOpts.ShihabWaitForPid, shihabInstalledExe))
                {
                    try
                    {
                        using (Process shihabWaitProc = Process.GetProcessById(mehediOpts.ShihabWaitForPid))
                        {
                            mehediReport(8, "Waiting for previous mdm.exe instance (PID " + mehediOpts.ShihabWaitForPid + ") to exit...");
                            if (!shihabWaitProc.WaitForExit(12000))
                            {
                                if (ShihabProcessAndPathSafety.TryVerifyProcessIdentity(mehediOpts.ShihabWaitForPid, shihabInstalledExe))
                                {
                                    try { shihabWaitProc.Kill(); shihabWaitProc.WaitForExit(3000); } catch { }
                                }
                            }
                        }
                    }
                    catch { }
                }
            }

            StopOwnedShihabAppInDirectory(shihabInstallRoot, delegate(string shihabMsg) { mehediReport(10, shihabMsg); });

            string mehediExtensionDir = Path.Combine(shihabInstallRoot, "extension");
            if (Directory.Exists(mehediExtensionDir))
            {
                try
                {
                    mehediReport(12, "Refreshing unpacked Browser Extension folder: " + mehediExtensionDir);
                    Directory.Delete(mehediExtensionDir, true);
                }
                catch { }
            }

            Stream shihabPayloadStream = typeof(MehediInstallerEngine).Assembly.GetManifestResourceStream("ShihabMehediInstaller.payload.zip");
            if (shihabPayloadStream == null)
            {
                throw new InvalidOperationException("Embedded installer payload (ShihabMehediInstaller.payload.zip) is missing.");
            }

            long mehediTotalExtractedBytes = 0L;
            using (shihabPayloadStream)
            {
                ShihabProcessAndPathSafety.ExtractZipArchiveSafely(
                    shihabPayloadStream,
                    shihabInstallRoot,
                    out mehediTotalExtractedBytes,
                    delegate(int mehediIdx, int shihabTotalEntries, string mehediRelPath)
                    {
                        int shihabPct = 15 + (mehediIdx * 55) / Math.Max(1, shihabTotalEntries);
                        mehediReport(shihabPct, "Extracting: " + mehediRelPath);
                    });
            }

            string shihabSelfExe = Process.GetCurrentProcess().MainModule.FileName;
            string mehediUninstallerPath = Path.Combine(shihabInstallRoot, "Uninstall.exe");
            mehediReport(75, "Creating uninstaller: Uninstall.exe");
            if (!string.Equals(Path.GetFullPath(shihabSelfExe), Path.GetFullPath(mehediUninstallerPath), StringComparison.OrdinalIgnoreCase))
            {
                File.Copy(shihabSelfExe, mehediUninstallerPath, true);
            }

            string[] shihabRequiredExtFiles = new string[]
            {
                "manifest.json",
                "mehedi_content.js",
                "mehedi_page_hook.js",
                "shihab_background.js",
                "shihab_popup.html"
            };
            for (int mehediReqIdx = 0; mehediReqIdx < shihabRequiredExtFiles.Length; mehediReqIdx++)
            {
                string shihabReqFile = Path.Combine(mehediExtensionDir, shihabRequiredExtFiles[mehediReqIdx]);
                if (!File.Exists(shihabReqFile))
                {
                    throw new InvalidOperationException("Unpacked browser extension file missing in installation directory: " + shihabReqFile);
                }
            }

            List<string> updatedShellFiles = new List<string>();
            List<string> updatedShellDirs = new List<string>();
            updatedShellFiles.Add(shihabInstalledExe);
            updatedShellFiles.Add(mehediUninstallerPath);
            updatedShellDirs.Add(shihabInstallRoot);

            if (!mehediOpts.ShihabSkipRegistryAndShortcuts)
            {
                BackupExistingShihabRegistryState();

                string shihabDesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shihabDesktopLnk = Path.Combine(shihabDesktopDir, "MHS Download Manager.lnk");
                bool refreshDesktopShortcut = !mehediOpts.ShihabInPlaceUpdateMode
                    ? mehediOpts.ShihabCreateDesktopShortcut
                    : File.Exists(shihabDesktopLnk);
                if (refreshDesktopShortcut)
                {
                    mehediReport(82, mehediOpts.ShihabInPlaceUpdateMode ? "Refreshing Desktop shortcut icon..." : "Creating Desktop shortcut...");
                    if (mehediOpts.ShihabInPlaceUpdateMode)
                    {
                        try { if (File.Exists(shihabDesktopLnk)) File.Delete(shihabDesktopLnk); } catch { }
                    }
                    CreateShihabShellShortcut(
                        shihabDesktopLnk,
                        shihabInstalledExe,
                        "",
                        shihabInstallRoot,
                        ShihabSetupTheme.ShihabProductTitle,
                        shihabInstalledExe + ",0");
                    updatedShellFiles.Add(shihabDesktopLnk);
                    updatedShellDirs.Add(shihabDesktopDir);
                }

                // Also refresh an existing OneDrive Desktop shortcut if present and targeting this exe
                try
                {
                    string userProfileDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                    string oneDriveDesktopLnk = Path.Combine(Path.Combine(userProfileDir, "OneDrive\\Desktop"), "MHS Download Manager.lnk");
                    if (!string.Equals(oneDriveDesktopLnk, shihabDesktopLnk, StringComparison.OrdinalIgnoreCase) &&
                        File.Exists(oneDriveDesktopLnk))
                    {
                        string existingTarget = TryGetShortcutTargetPath(oneDriveDesktopLnk);
                        if (string.Equals(existingTarget, shihabInstalledExe, StringComparison.OrdinalIgnoreCase))
                        {
                            try { File.Delete(oneDriveDesktopLnk); } catch { }
                            CreateShihabShellShortcut(
                                oneDriveDesktopLnk,
                                shihabInstalledExe,
                                "",
                                shihabInstallRoot,
                                ShihabSetupTheme.ShihabProductTitle,
                                shihabInstalledExe + ",0");
                            updatedShellFiles.Add(oneDriveDesktopLnk);
                            updatedShellDirs.Add(Path.GetDirectoryName(oneDriveDesktopLnk));
                        }
                    }
                }
                catch { }

                if (mehediOpts.MehediCreateStartMenuShortcuts)
                {
                    mehediReport(87, "Creating Start Menu shortcuts (App + Extension Folder + Uninstaller)...");
                    string mehediStartPrograms = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                        "MHS Download Manager");
                    Directory.CreateDirectory(mehediStartPrograms);

                    string startAppLnk = Path.Combine(mehediStartPrograms, "MHS Download Manager.lnk");
                    string startExtLnk = Path.Combine(mehediStartPrograms, "MHS Browser Extension Folder.lnk");
                    string startUninstLnk = Path.Combine(mehediStartPrograms, "Uninstall MHS Download Manager.lnk");

                    if (mehediOpts.ShihabInPlaceUpdateMode)
                    {
                        try { if (File.Exists(startAppLnk)) File.Delete(startAppLnk); } catch { }
                        try { if (File.Exists(startExtLnk)) File.Delete(startExtLnk); } catch { }
                        try { if (File.Exists(startUninstLnk)) File.Delete(startUninstLnk); } catch { }
                    }

                    CreateShihabShellShortcut(
                        startAppLnk,
                        shihabInstalledExe,
                        "",
                        shihabInstallRoot,
                        ShihabSetupTheme.ShihabProductTitle,
                        shihabInstalledExe + ",0");

                    CreateShihabShellShortcut(
                        startExtLnk,
                        ShihabProcessAndPathSafety.ResolveSystem32ToolPath("explorer.exe"),
                        ShihabProcessAndPathSafety.QuoteWindowsArgument(mehediExtensionDir),
                        mehediExtensionDir,
                        "Open unpacked Chrome / Brave / Edge extension folder",
                        shihabInstalledExe + ",0");

                    CreateShihabShellShortcut(
                        startUninstLnk,
                        mehediUninstallerPath,
                        "--shihab-uninstall",
                        shihabInstallRoot,
                        "Uninstall MHS Download Manager",
                        mehediUninstallerPath + ",0");

                    updatedShellFiles.Add(startAppLnk);
                    updatedShellFiles.Add(startExtLnk);
                    updatedShellFiles.Add(startUninstLnk);
                    updatedShellDirs.Add(mehediStartPrograms);
                }

                mehediReport(89, "Refreshing pinned Taskbar shortcut icons (if pinned)...");
                RefreshPinnedTaskbarShortcuts(shihabInstalledExe, updatedShellFiles, updatedShellDirs);

                if (mehediOpts.ShihabLaunchOnStartup && !mehediOpts.ShihabInPlaceUpdateMode)
                {
                    mehediReport(91, "Configuring Windows startup entry...");
                    using (RegistryKey shihabRunKey = Registry.CurrentUser.CreateSubKey(ShihabSetupTheme.MehediRunRegKey))
                    {
                        if (shihabRunKey != null)
                        {
                            try { shihabRunKey.DeleteValue("MHS_Download_Manager", false); } catch { }
                            shihabRunKey.SetValue(
                                ShihabSetupTheme.ShihabRunValueName,
                                ShihabProcessAndPathSafety.QuoteWindowsArgument(shihabInstalledExe) + " --shihab-minimized",
                                RegistryValueKind.String);
                        }
                    }
                }

                if (mehediOpts.MehediRegisterNativeHost && Directory.Exists(mehediExtensionDir))
                {
                    mehediReport(95, "Registering 14-Browser Native Messaging host...");
                    RegisterShihabNativeHostForInstalledExtension(shihabInstalledExe, mehediExtensionDir);
                }

                mehediReport(98, "Registering Windows Installed Apps entry...");
                RegisterMehediWindowsUninstaller(shihabInstallRoot, shihabInstalledExe, mehediUninstallerPath, mehediTotalExtractedBytes);

                mehediReport(99, "Refreshing Windows Desktop & Taskbar icon cache...");
                ShihabSetupTheme.RefreshWindowsShellIconCache(updatedShellFiles.ToArray(), updatedShellDirs.ToArray());
            }

            mehediReport(100, "Installation complete! Browser extension ready at: " + mehediExtensionDir);
        }

        private static void BackupExistingShihabRegistryState()
        {
            try
            {
                string shihabBackupDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShihabMehediIDM");
                Directory.CreateDirectory(shihabBackupDir);
                string mehediBackupFile = Path.Combine(shihabBackupDir, "shihab_installer_registry_backup.txt");
                if (File.Exists(mehediBackupFile)) return;

                List<string> shihabLines = new List<string>();
                shihabLines.Add("# MHS Download Manager Installer Additive Registry Backup (" + DateTime.UtcNow.ToString("o") + ")");
                using (RegistryKey mehediRun = Registry.CurrentUser.OpenSubKey(ShihabSetupTheme.MehediRunRegKey, false))
                {
                    if (mehediRun != null)
                    {
                        object shihabExistingRun = mehediRun.GetValue(ShihabSetupTheme.ShihabRunValueName);
                        shihabLines.Add("HKCU_RUN=" + (shihabExistingRun != null ? shihabExistingRun.ToString() : "<none>"));
                    }
                }
                File.WriteAllLines(mehediBackupFile, shihabLines.ToArray(), Encoding.UTF8);
            }
            catch { }
        }

        private static readonly string[] ChromiumNativeHostRegistryKeys = new string[]
        {
            @"Software\Google\Chrome\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Chromium\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Microsoft\Edge\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\BraveSoftware\Brave-Browser\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Vivaldi\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Opera Software\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Helium\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Thorium\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Arc\NativeMessagingHosts\com.shihab.mehedi.idm"
        };

        private static readonly string[] FirefoxNativeHostRegistryKeys = new string[]
        {
            @"Software\Mozilla\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Zen\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\LibreWolf\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Floorp\NativeMessagingHosts\com.shihab.mehedi.idm",
            @"Software\Waterfox\NativeMessagingHosts\com.shihab.mehedi.idm"
        };

        private static void WriteNativeHostRegistryKeys(string[] keys, string manifestPath)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                using (RegistryKey mehediKey = Registry.CurrentUser.CreateSubKey(keys[i]))
                {
                    if (mehediKey != null)
                    {
                        mehediKey.SetValue("", manifestPath, RegistryValueKind.String);
                    }
                }
            }
        }

        private static void RemoveNativeHostRegistryKeys(string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                try { Registry.CurrentUser.DeleteSubKeyTree(keys[i], false); } catch { }
            }
        }

        private static string EscapeJsonLiteral(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            StringBuilder sb = new StringBuilder(value.Length + 8);
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch == '\\') sb.Append("\\\\");
                else if (ch == '"') sb.Append("\\\"");
                else if (ch == '\r' || ch == '\n') continue;
                else sb.Append(ch);
            }
            return sb.ToString();
        }

        private static void RegisterShihabNativeHostForInstalledExtension(string mehediExePath, string shihabExtensionDir)
        {
            try
            {
                string mehediAppDataDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShihabMehediIDM");
                Directory.CreateDirectory(mehediAppDataDir);

                string shihabLauncherCmd = Path.Combine(mehediAppDataDir, "shihab_native_host_launcher.cmd");
                File.WriteAllText(
                    shihabLauncherCmd,
                    "@echo off\r\n" + ShihabProcessAndPathSafety.QuoteWindowsArgument(mehediExePath) + " --shihab-native-messaging-host\r\n",
                    Encoding.ASCII);

                string mehediComputedExtId = ComputeShihabChromiumUnpackedExtensionId(shihabExtensionDir);
                string shihabManifestPath = Path.Combine(mehediAppDataDir, "com.shihab.mehedi.idm.json");
                string mehediEscapedCmd = EscapeJsonLiteral(shihabLauncherCmd);
                string shihabManifestJson =
                    "{\r\n" +
                    "  \"name\": \"com.shihab.mehedi.idm\",\r\n" +
                    "  \"description\": \"MHS Download Manager Native Messaging Host (mdm.exe)\",\r\n" +
                    "  \"path\": \"" + mehediEscapedCmd + "\",\r\n" +
                    "  \"type\": \"stdio\",\r\n" +
                    "  \"allowed_origins\": [\r\n" +
                    "    \"chrome-extension://" + EscapeJsonLiteral(mehediComputedExtId) + "/\"\r\n" +
                    "  ]\r\n" +
                    "}\r\n";
                File.WriteAllText(shihabManifestPath, shihabManifestJson, new UTF8Encoding(false));
                WriteNativeHostRegistryKeys(ChromiumNativeHostRegistryKeys, shihabManifestPath);

                string shihabFirefoxManifestPath = Path.Combine(mehediAppDataDir, "com.shihab.mehedi.idm.firefox.json");
                string shihabFirefoxManifestJson =
                    "{\r\n" +
                    "  \"name\": \"com.shihab.mehedi.idm\",\r\n" +
                    "  \"description\": \"MHS Download Manager Native Messaging Host (mdm.exe)\",\r\n" +
                    "  \"path\": \"" + mehediEscapedCmd + "\",\r\n" +
                    "  \"type\": \"stdio\",\r\n" +
                    "  \"allowed_extensions\": [\r\n" +
                    "    \"idm@shihab.mehedi\"\r\n" +
                    "  ]\r\n" +
                    "}\r\n";
                File.WriteAllText(shihabFirefoxManifestPath, shihabFirefoxManifestJson, new UTF8Encoding(false));
                WriteNativeHostRegistryKeys(FirefoxNativeHostRegistryKeys, shihabFirefoxManifestPath);
            }
            catch { }
        }

        private static void RegisterMehediWindowsUninstaller(
            string shihabInstallDir,
            string mehediExePath,
            string shihabUninstallExePath,
            long mehediSizeBytes)
        {
            try
            {
                using (RegistryKey shihabUninst = Registry.CurrentUser.CreateSubKey(ShihabSetupTheme.ShihabUninstallRegKey))
                {
                    if (shihabUninst == null) return;
                    shihabUninst.SetValue("DisplayName", ShihabSetupTheme.ShihabProductTitle, RegistryValueKind.String);
                    shihabUninst.SetValue("DisplayVersion", ShihabSetupTheme.MehediVersionText, RegistryValueKind.String);
                    shihabUninst.SetValue("Publisher", "Mehedi Hasan Shihab", RegistryValueKind.String);
                    shihabUninst.SetValue("DisplayIcon", ShihabProcessAndPathSafety.QuoteWindowsArgument(mehediExePath) + ",0", RegistryValueKind.String);
                    shihabUninst.SetValue("InstallLocation", shihabInstallDir, RegistryValueKind.String);
                    shihabUninst.SetValue("UninstallString", ShihabProcessAndPathSafety.QuoteWindowsArgument(shihabUninstallExePath) + " --shihab-uninstall", RegistryValueKind.String);
                    shihabUninst.SetValue("QuietUninstallString", ShihabProcessAndPathSafety.QuoteWindowsArgument(shihabUninstallExePath) + " --shihab-uninstall --shihab-silent", RegistryValueKind.String);
                    shihabUninst.SetValue("URLInfoAbout", "https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER", RegistryValueKind.String);
                    shihabUninst.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    shihabUninst.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    int mehediSizeKb = (int)Math.Max(512L, mehediSizeBytes / 1024L);
                    shihabUninst.SetValue("EstimatedSize", mehediSizeKb, RegistryValueKind.DWord);
                }
            }
            catch { }
        }

        public static string TryGetShortcutTargetPath(string lnkPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(lnkPath) || !File.Exists(lnkPath)) return "";
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType == null) return "";
                object shell = Activator.CreateInstance(shellType);
                object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { lnkPath });
                object targetObj = shortcut.GetType().InvokeMember("TargetPath", BindingFlags.GetProperty, null, shortcut, null);
                string raw = targetObj != null ? targetObj.ToString().Trim() : "";
                return string.IsNullOrEmpty(raw) ? "" : Path.GetFullPath(raw);
            }
            catch
            {
                return "";
            }
        }

        private static void RefreshPinnedTaskbarShortcuts(
            string targetExePath,
            List<string> updatedShellFiles,
            List<string> updatedShellDirs)
        {
            try
            {
                string taskBarDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Internet Explorer\Quick Launch\User Pinned\TaskBar");
                if (!Directory.Exists(taskBarDir)) return;

                string normExe = Path.GetFullPath(targetExePath);
                string[] lnks = Directory.GetFiles(taskBarDir, "*.lnk", SearchOption.TopDirectoryOnly);
                bool touchedDir = false;
                for (int i = 0; i < lnks.Length; i++)
                {
                    string lnk = lnks[i];
                    string existingTarget = TryGetShortcutTargetPath(lnk);
                    if (string.Equals(existingTarget, normExe, StringComparison.OrdinalIgnoreCase))
                    {
                        CreateShihabShellShortcut(
                            lnk,
                            normExe,
                            "",
                            Path.GetDirectoryName(normExe),
                            ShihabSetupTheme.ShihabProductTitle,
                            normExe + ",0");
                        try { File.SetLastWriteTimeUtc(lnk, DateTime.UtcNow); } catch { }
                        if (updatedShellFiles != null) updatedShellFiles.Add(lnk);
                        touchedDir = true;
                    }
                }
                if (touchedDir && updatedShellDirs != null)
                {
                    updatedShellDirs.Add(taskBarDir);
                }
            }
            catch { }
        }

        public static void CreateShihabShellShortcut(
            string mehediLnkPath,
            string shihabTargetPath,
            string mehediArguments,
            string shihabWorkingDir,
            string mehediDescription,
            string shihabIconLocation)
        {
            try
            {
                Type mehediShellType = Type.GetTypeFromProgID("WScript.Shell");
                if (mehediShellType == null) return;
                object shihabShell = Activator.CreateInstance(mehediShellType);
                object mehediShortcut = mehediShellType.InvokeMember(
                    "CreateShortcut",
                    BindingFlags.InvokeMethod,
                    null,
                    shihabShell,
                    new object[] { mehediLnkPath });
                Type shihabScType = mehediShortcut.GetType();
                shihabScType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, mehediShortcut, new object[] { shihabTargetPath });
                if (!string.IsNullOrEmpty(mehediArguments))
                {
                    shihabScType.InvokeMember("Arguments", BindingFlags.SetProperty, null, mehediShortcut, new object[] { mehediArguments });
                }
                if (!string.IsNullOrEmpty(shihabWorkingDir))
                {
                    shihabScType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, mehediShortcut, new object[] { shihabWorkingDir });
                }
                if (!string.IsNullOrEmpty(mehediDescription))
                {
                    shihabScType.InvokeMember("Description", BindingFlags.SetProperty, null, mehediShortcut, new object[] { mehediDescription });
                }
                if (!string.IsNullOrEmpty(shihabIconLocation))
                {
                    shihabScType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, mehediShortcut, new object[] { shihabIconLocation });
                }
                shihabScType.InvokeMember("Save", BindingFlags.InvokeMethod, null, mehediShortcut, null);
                try { File.SetLastWriteTimeUtc(mehediLnkPath, DateTime.UtcNow); } catch { }
            }
            catch { }
        }

        public static string ResolveMehediExistingInstallDirForUninstall(string shihabExplicitDir)
        {
            if (!string.IsNullOrWhiteSpace(shihabExplicitDir))
            {
                return Path.GetFullPath(shihabExplicitDir);
            }
            string mehediSelfDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
            if (File.Exists(Path.Combine(mehediSelfDir, "mdm.exe")))
            {
                return mehediSelfDir;
            }
            try
            {
                using (RegistryKey shihabUninst = Registry.CurrentUser.OpenSubKey(ShihabSetupTheme.ShihabUninstallRegKey, false))
                {
                    if (shihabUninst != null)
                    {
                        object mehediLoc = shihabUninst.GetValue("InstallLocation");
                        if (mehediLoc != null && !string.IsNullOrWhiteSpace(mehediLoc.ToString()))
                        {
                            return Path.GetFullPath(mehediLoc.ToString());
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        public static void ExecuteMehediUninstall(string shihabInstallDir, bool mehediSkipRegistryAndShortcuts)
        {
            if (string.IsNullOrWhiteSpace(shihabInstallDir) || !Directory.Exists(shihabInstallDir))
            {
                return;
            }
            string mehediFullDir = Path.GetFullPath(shihabInstallDir);
            StopOwnedShihabAppInDirectory(mehediFullDir, null);

            string[] shihabKnownFiles = new string[] { "mdm.exe", "README.md", "LICENSE" };
            for (int mehediI = 0; mehediI < shihabKnownFiles.Length; mehediI++)
            {
                string shihabFile = Path.Combine(mehediFullDir, shihabKnownFiles[mehediI]);
                try { if (File.Exists(shihabFile)) File.Delete(shihabFile); } catch { }
            }

            string mehediExtDir = Path.Combine(mehediFullDir, "extension");
            try { if (Directory.Exists(mehediExtDir)) Directory.Delete(mehediExtDir, true); } catch { }

            if (!mehediSkipRegistryAndShortcuts)
            {
                string deletedDesktopLnk = "";
                try
                {
                    string shihabDesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    string shihabDesktopLnk = Path.Combine(shihabDesktopDir, "MHS Download Manager.lnk");
                    if (File.Exists(shihabDesktopLnk))
                    {
                        File.Delete(shihabDesktopLnk);
                        deletedDesktopLnk = shihabDesktopLnk;
                    }
                }
                catch { }

                try
                {
                    string shihabDesktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                    ShihabSetupTheme.RefreshWindowsShellIconCache(
                        string.IsNullOrEmpty(deletedDesktopLnk) ? null : new string[] { deletedDesktopLnk },
                        new string[] { shihabDesktopDir });
                }
                catch { }

                try
                {
                    string mehediStartMenuDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MHS Download Manager");
                    if (Directory.Exists(mehediStartMenuDir)) Directory.Delete(mehediStartMenuDir, true);
                }
                catch { }

                try
                {
                    using (RegistryKey shihabRun = Registry.CurrentUser.OpenSubKey(ShihabSetupTheme.MehediRunRegKey, true))
                    {
                        if (shihabRun != null)
                        {
                            shihabRun.DeleteValue(ShihabSetupTheme.ShihabRunValueName, false);
                            shihabRun.DeleteValue("MHS_Download_Manager", false);
                        }
                    }
                }
                catch { }

                try { Registry.CurrentUser.DeleteSubKeyTree(ShihabSetupTheme.ShihabUninstallRegKey, false); } catch { }

                RemoveNativeHostRegistryKeys(ChromiumNativeHostRegistryKeys);
                RemoveNativeHostRegistryKeys(FirefoxNativeHostRegistryKeys);
                try
                {
                    string mehediAppDataDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "ShihabMehediIDM");
                    string[] hostArtifacts = new string[]
                    {
                        "shihab_native_host_launcher.cmd",
                        "com.shihab.mehedi.idm.json",
                        "com.shihab.mehedi.idm.firefox.json"
                    };
                    for (int idx = 0; idx < hostArtifacts.Length; idx++)
                    {
                        string artPath = Path.Combine(mehediAppDataDir, hostArtifacts[idx]);
                        if (File.Exists(artPath)) File.Delete(artPath);
                    }
                }
                catch { }
            }

            string shihabSelfExe = Path.GetFullPath(Process.GetCurrentProcess().MainModule.FileName);
            string mehediUninstExe = Path.Combine(mehediFullDir, "Uninstall.exe");
            if (string.Equals(shihabSelfExe, mehediUninstExe, StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    string cmdExePath = ShihabProcessAndPathSafety.ResolveSystem32ToolPath("cmd.exe");
                    string cleanupArgs = "/c ping 127.0.0.1 -n 2 > nul & del /f /q " +
                        ShihabProcessAndPathSafety.QuoteWindowsArgument(mehediUninstExe) +
                        " & rmdir " +
                        ShihabProcessAndPathSafety.QuoteWindowsArgument(mehediFullDir);
                    ProcessStartInfo shihabCleanupPsi = new ProcessStartInfo(cmdExePath, cleanupArgs);
                    shihabCleanupPsi.CreateNoWindow = true;
                    shihabCleanupPsi.UseShellExecute = false;
                    Process.Start(shihabCleanupPsi);
                }
                catch { }
            }
            else
            {
                try { if (File.Exists(mehediUninstExe)) File.Delete(mehediUninstExe); } catch { }
                try
                {
                    if (Directory.GetFiles(mehediFullDir).Length == 0 && Directory.GetDirectories(mehediFullDir).Length == 0)
                    {
                        Directory.Delete(mehediFullDir, false);
                    }
                }
                catch { }
            }
        }
    }

    public sealed class ShihabSetupWizardForm : Form
    {
        private readonly ShihabInstallOptions _shihabOptions;
        private int _mehediCurrentStep = 0;
        private bool _shihabInstallingBusy = false;

        private Panel _mehediStepWelcome;
        private Panel _shihabStepLocation;
        private Panel _mehediStepProgress;
        private Panel _shihabStepFinish;

        private TextBox _mehediInstallDirBox;
        private Label _shihabExtPreviewLabel;
        private CheckBox _mehediDesktopShortcutCheck;
        private CheckBox _shihabStartMenuCheck;
        private CheckBox _mehediStartupCheck;
        private CheckBox _shihabNativeHostCheck;

        private ShihabGradientProgressPanel _mehediProgressBar;
        private TextBox _shihabInstallLogBox;

        private TextBox _mehediFinalExtPathBox;
        private CheckBox _shihabLaunchAppCheck;
        private CheckBox _mehediOpenExtFolderCheck;

        private Button _shihabBackBtn;
        private Button _mehediNextBtn;
        private Button _shihabCancelBtn;

        public ShihabSetupWizardForm(ShihabInstallOptions mehediInitialOpts)
        {
            _shihabOptions = mehediInitialOpts ?? new ShihabInstallOptions();

            Text = ShihabSetupTheme.ShihabProductTitle + " — Setup";
            Size = new Size(650, 480);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = ShihabSetupTheme.GetMehediWindowBg();
            ForeColor = ShihabSetupTheme.GetMehediPrimaryText();
            Font = new Font("Segoe UI", 9.2f);
            Icon = ShihabSetupTheme.LoadShihabEmbeddedIcon();
            ShihabSetupTheme.ApplyShihabDarkTitleBar(this);

            BuildMehediHeaderBanner();
            BuildShihabFooterBar();
            BuildMehediStepPanels();
            ShowShihabWizardStep(0);
        }

        private void BuildMehediHeaderBanner()
        {
            Panel shihabHeader = new Panel
            {
                Dock = DockStyle.Top,
                Height = 68,
                BackColor = ShihabSetupTheme.GetShihabCardBg()
            };
            PictureBox mehediLogo = new PictureBox
            {
                Image = ShihabSetupTheme.LoadMehediLogoBitmap(42),
                Location = new Point(18, 13),
                Size = new Size(42, 42),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            Label shihabTitle = new Label
            {
                Text = ShihabSetupTheme.ShihabProductTitle,
                Location = new Point(72, 13),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11.5f),
                ForeColor = ShihabSetupTheme.GetMehediPrimaryText()
            };
            Label mehediSub = new Label
            {
                Text = "Windows x64 Desktop Application + Unpacked Chrome / Brave / Edge Extension",
                Location = new Point(73, 38),
                AutoSize = true,
                Font = new Font("Segoe UI", 8.8f),
                ForeColor = ShihabSetupTheme.GetShihabMutedText()
            };
            Panel shihabAccentLine = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 1,
                BackColor = ShihabSetupTheme.GetShihabBorderCol()
            };
            shihabHeader.Controls.AddRange(new Control[] { mehediLogo, shihabTitle, mehediSub, shihabAccentLine });
            Controls.Add(shihabHeader);
        }

        private void BuildShihabFooterBar()
        {
            Panel mehediFooter = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 56,
                BackColor = ShihabSetupTheme.GetShihabCardBg()
            };
            Panel shihabTopBorder = new Panel
            {
                Dock = DockStyle.Top,
                Height = 1,
                BackColor = ShihabSetupTheme.GetShihabBorderCol()
            };

            _shihabBackBtn = ShihabSetupTheme.CreateShihabButton("< Back", ShihabSetupTheme.GetMehediInputBg(), ForeColor, 96);
            _shihabBackBtn.Location = new Point(308, 11);
            _shihabBackBtn.Click += delegate { if (_mehediCurrentStep == 1) ShowShihabWizardStep(0); };

            _mehediNextBtn = ShihabSetupTheme.CreateShihabButton("Next >", ShihabSetupTheme.GetMehediAccentViolet(), Color.White, 108);
            _mehediNextBtn.Location = new Point(412, 11);
            _mehediNextBtn.Click += delegate { HandleMehediPrimaryButtonClick(); };

            _shihabCancelBtn = ShihabSetupTheme.CreateShihabButton("Cancel", ShihabSetupTheme.GetMehediInputBg(), ForeColor, 96);
            _shihabCancelBtn.Location = new Point(528, 11);
            _shihabCancelBtn.Click += delegate
            {
                if (!_shihabInstallingBusy)
                {
                    Close();
                }
            };

            mehediFooter.Controls.AddRange(new Control[] { shihabTopBorder, _shihabBackBtn, _mehediNextBtn, _shihabCancelBtn });
            Controls.Add(mehediFooter);
        }

        private void BuildMehediStepPanels()
        {
            Rectangle shihabBodyBounds = new Rectangle(0, 68, 634, 318);

            // Step 0: Welcome
            _mehediStepWelcome = new Panel { Bounds = shihabBodyBounds, BackColor = BackColor };
            Label shihabWelHead = new Label
            {
                Text = "Welcome to MHS Download Manager 1.0.7 Setup",
                Location = new Point(22, 16),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11f)
            };
            Label mehediWelDesc = new Label
            {
                Text = "This wizard will install MHS Download Manager 1.0.7 (Mehedi Hasan Shihab) on your computer,\n" +
                       "keeping the unpacked 'extension' folder inside your installation directory ready to load in Chrome, Brave, Edge, and Firefox.",
                Location = new Point(22, 44),
                Size = new Size(595, 42),
                ForeColor = ShihabSetupTheme.GetShihabMutedText()
            };
            TextBox shihabPlanBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(22, 94),
                Size = new Size(592, 198),
                BackColor = ShihabSetupTheme.GetShihabCardBg(),
                ForeColor = ForeColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.8f),
                Text =
                    "Planned Installation Components (<InstallDir>):\r\n" +
                    "  • mdm.exe          — x64 Download Accelerator (1-128 Sparse Dynamic Segments)\r\n" +
                    "  • extension\\       — Ready-to-load Unpacked MV3 Extension (Chromium & Firefox)\r\n" +
                    "  • Uninstall.exe    — Clean Windows Uninstaller\r\n\r\n" +
                    "Included v1.0.7 Capabilities:\r\n" +
                    "  • Rock-Solid DWM Custom Dark/Light Chrome + Auto Desktop & Taskbar Icon Cache Refresh\r\n" +
                    "  • Modular C# Core + Zero-Duplicate Brave/Chrome/Edge/Firefox Popup Suppression\r\n" +
                    "  • Background Minimize Button (Top-Right Tray Action) & Glowing 3D App Branding\r\n" +
                    "  • Resilient Auto-Updater (REST + Web Fallback + Browser Popup) with SHA-256 Verify\r\n" +
                    "  • Top Menu Bar & 11-Button Gradient Ribbon + 1-128 Sparse Dynamic Segments\r\n" +
                    "  • BitTorrent & Magnet Client + Managed yt-dlp / ffmpeg / gallery-dl + Debrid Cloud\r\n" +
                    "  • Encrypted DNS (DoH), SOCKS5/PAC Relay, VPN Kill-Switch, DPAPI Vault, LAN QR & MCP\r\n" +
                    "  • Outside-corner 'Download Video' pill + 14-browser integration + lossless MP4/WebM/MKV muxer\r\n\r\n" +
                    "License: MIT License — Copyright (c) 2026 Mehedi Hasan Shihab"
            };
            _mehediStepWelcome.Controls.AddRange(new Control[] { shihabWelHead, mehediWelDesc, shihabPlanBox });

            // Step 1: Install Location & Options
            _shihabStepLocation = new Panel { Bounds = shihabBodyBounds, BackColor = BackColor };
            Label mehediLocTitle = new Label
            {
                Text = "Choose Installation Location & Integration Options",
                Location = new Point(22, 16),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11f)
            };
            Label shihabDirLbl = new Label
            {
                Text = "Destination Folder:",
                Location = new Point(22, 48),
                AutoSize = true,
                ForeColor = ShihabSetupTheme.GetShihabMutedText()
            };
            _mehediInstallDirBox = new TextBox
            {
                Location = new Point(22, 70),
                Size = new Size(492, 26),
                BackColor = ShihabSetupTheme.GetMehediInputBg(),
                ForeColor = ForeColor,
                BorderStyle = BorderStyle.FixedSingle,
                Text = _shihabOptions.MehediInstallDirectory
            };
            Button shihabBrowseBtn = ShihabSetupTheme.CreateShihabButton("Browse...", ShihabSetupTheme.GetMehediInputBg(), ForeColor, 90);
            shihabBrowseBtn.Height = 27;
            shihabBrowseBtn.Location = new Point(524, 68);
            shihabBrowseBtn.Click += delegate
            {
                using (FolderBrowserDialog mehediFbd = new FolderBrowserDialog())
                {
                    mehediFbd.Description = "Select folder where MHS Download Manager will be installed:";
                    mehediFbd.SelectedPath = _mehediInstallDirBox.Text;
                    if (mehediFbd.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(mehediFbd.SelectedPath))
                    {
                        string shihabPicked = mehediFbd.SelectedPath.Trim();
                        if (!shihabPicked.EndsWith("MHS Download Manager", StringComparison.OrdinalIgnoreCase))
                        {
                            shihabPicked = Path.Combine(shihabPicked, "MHS Download Manager");
                        }
                        _mehediInstallDirBox.Text = shihabPicked;
                    }
                }
            };

            _shihabExtPreviewLabel = new Label
            {
                Location = new Point(22, 103),
                Size = new Size(592, 20),
                AutoEllipsis = true,
                Font = new Font("Segoe UI Semibold", 8.8f),
                ForeColor = Color.FromArgb(56, 189, 248)
            };
            Action mehediRefreshExtPreview = delegate
            {
                string shihabBase = (_mehediInstallDirBox.Text ?? "").Trim();
                _shihabExtPreviewLabel.Text = "Browser Extension Folder will be created at:  " +
                    (string.IsNullOrEmpty(shihabBase) ? "" : Path.Combine(shihabBase, "extension"));
            };
            _mehediInstallDirBox.TextChanged += delegate { mehediRefreshExtPreview(); };
            mehediRefreshExtPreview();

            _mehediDesktopShortcutCheck = new CheckBox
            {
                Text = "Create Desktop shortcut (MHS Download Manager)",
                Location = new Point(24, 140),
                AutoSize = true,
                Checked = _shihabOptions.ShihabCreateDesktopShortcut
            };
            _shihabStartMenuCheck = new CheckBox
            {
                Text = "Create Start Menu shortcuts (App + Extension Folder + Uninstaller)",
                Location = new Point(24, 172),
                AutoSize = true,
                Checked = _shihabOptions.MehediCreateStartMenuShortcuts
            };
            _mehediStartupCheck = new CheckBox
            {
                Text = "Launch MHS Download Manager automatically when Windows starts",
                Location = new Point(24, 204),
                AutoSize = true,
                Checked = _shihabOptions.ShihabLaunchOnStartup
            };
            _shihabNativeHostCheck = new CheckBox
            {
                Text = "Register Chrome, Brave & Edge Native Messaging integration (com.shihab.mehedi.idm)",
                Location = new Point(24, 236),
                AutoSize = true,
                Checked = _shihabOptions.MehediRegisterNativeHost
            };
            _shihabStepLocation.Controls.AddRange(new Control[]
            {
                mehediLocTitle, shihabDirLbl, _mehediInstallDirBox, shihabBrowseBtn, _shihabExtPreviewLabel,
                _mehediDesktopShortcutCheck, _shihabStartMenuCheck, _mehediStartupCheck, _shihabNativeHostCheck
            });

            // Step 2: Installing Progress
            _mehediStepProgress = new Panel { Bounds = shihabBodyBounds, BackColor = BackColor };
            Label shihabProgTitle = new Label
            {
                Text = "Installing MHS Download Manager 1.0.7...",
                Location = new Point(22, 16),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11f)
            };
            _mehediProgressBar = new ShihabGradientProgressPanel
            {
                Location = new Point(22, 50),
                Size = new Size(592, 26)
            };
            _shihabInstallLogBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(22, 88),
                Size = new Size(592, 204),
                BackColor = ShihabSetupTheme.GetShihabCardBg(),
                ForeColor = ForeColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 8.6f)
            };
            _mehediStepProgress.Controls.AddRange(new Control[] { shihabProgTitle, _mehediProgressBar, _shihabInstallLogBox });

            // Step 3: Finish + Extension Instructions
            _shihabStepFinish = new Panel { Bounds = shihabBodyBounds, BackColor = BackColor };
            Label mehediFinTitle = new Label
            {
                Text = "Installation Complete!",
                Location = new Point(22, 14),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11.5f),
                ForeColor = ShihabSetupTheme.GetShihabAccentGreen()
            };
            Label shihabExtGuideLbl = new Label
            {
                Text = "Load the installed Browser Extension from your installation directory (Chrome / Brave / Edge):\n" +
                       "1. Open chrome://extensions or brave://extensions   2. Turn ON Developer mode   3. Click 'Load unpacked' and select:",
                Location = new Point(22, 44),
                Size = new Size(596, 40),
                ForeColor = ShihabSetupTheme.GetShihabMutedText()
            };
            _mehediFinalExtPathBox = new TextBox
            {
                ReadOnly = true,
                Location = new Point(22, 90),
                Size = new Size(376, 26),
                BackColor = ShihabSetupTheme.GetMehediInputBg(),
                ForeColor = Color.FromArgb(56, 189, 248),
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9.0f)
            };
            Button shihabCopyExtBtn = ShihabSetupTheme.CreateShihabButton("Copy Path", ShihabSetupTheme.GetMehediAccentViolet(), Color.White, 96);
            shihabCopyExtBtn.Height = 27;
            shihabCopyExtBtn.Location = new Point(406, 88);
            shihabCopyExtBtn.Click += delegate
            {
                try
                {
                    Clipboard.SetText(_mehediFinalExtPathBox.Text);
                    shihabCopyExtBtn.Text = "Copied!";
                }
                catch { }
            };
            Button mehediOpenExtBtn = ShihabSetupTheme.CreateShihabButton("Open Folder", ShihabSetupTheme.GetMehediInputBg(), ForeColor, 104);
            mehediOpenExtBtn.Height = 27;
            mehediOpenExtBtn.Location = new Point(510, 88);
            mehediOpenExtBtn.Click += delegate
            {
                try
                {
                    if (!string.IsNullOrWhiteSpace(_mehediFinalExtPathBox.Text) && Directory.Exists(_mehediFinalExtPathBox.Text))
                    {
                        ProcessStartInfo expPsi = new ProcessStartInfo(
                            ShihabProcessAndPathSafety.ResolveSystem32ToolPath("explorer.exe"),
                            ShihabProcessAndPathSafety.QuoteWindowsArgument(_mehediFinalExtPathBox.Text));
                        expPsi.UseShellExecute = false;
                        Process.Start(expPsi);
                    }
                }
                catch { }
            };
            Label shihabExtKeptNote = new Label
            {
                Text = "Kept in <InstallDir>\\extension — also accessible anytime via Help -> Open Browser Extension Folder.",
                Location = new Point(22, 122),
                Size = new Size(592, 18),
                ForeColor = ShihabSetupTheme.GetShihabMutedText(),
                Font = new Font("Segoe UI", 8.5f)
            };

            _shihabLaunchAppCheck = new CheckBox
            {
                Text = "Launch MHS Download Manager 1.0.7 now",
                Location = new Point(24, 152),
                AutoSize = true,
                Checked = true,
                Font = new Font("Segoe UI Semibold", 9.5f)
            };
            _mehediOpenExtFolderCheck = new CheckBox
            {
                Text = "Open the installed 'extension' folder in File Explorer on Finish (for Chrome / Brave Load unpacked)",
                Location = new Point(24, 184),
                AutoSize = true,
                Checked = true
            };
            _shihabStepFinish.Controls.AddRange(new Control[]
            {
                mehediFinTitle, shihabExtGuideLbl, _mehediFinalExtPathBox, shihabCopyExtBtn, mehediOpenExtBtn, shihabExtKeptNote,
                _shihabLaunchAppCheck, _mehediOpenExtFolderCheck
            });

            Controls.AddRange(new Control[] { _mehediStepWelcome, _shihabStepLocation, _mehediStepProgress, _shihabStepFinish });
        }

        private void ShowShihabWizardStep(int mehediStep)
        {
            _mehediCurrentStep = mehediStep;
            _mehediStepWelcome.Visible = (mehediStep == 0);
            _shihabStepLocation.Visible = (mehediStep == 1);
            _mehediStepProgress.Visible = (mehediStep == 2);
            _shihabStepFinish.Visible = (mehediStep == 3);

            _shihabBackBtn.Visible = (mehediStep == 1);
            _shihabCancelBtn.Enabled = (mehediStep < 2);
            _shihabCancelBtn.Visible = (mehediStep < 3);

            if (mehediStep == 0)
            {
                _mehediNextBtn.Text = "Next >";
                _mehediNextBtn.BackColor = ShihabSetupTheme.GetMehediAccentViolet();
            }
            else if (mehediStep == 1)
            {
                _mehediNextBtn.Text = "Install";
                _mehediNextBtn.BackColor = ShihabSetupTheme.GetShihabAccentGreen();
            }
            else if (mehediStep == 2)
            {
                _mehediNextBtn.Enabled = false;
                _mehediNextBtn.Text = "Installing...";
            }
            else if (mehediStep == 3)
            {
                _mehediNextBtn.Enabled = true;
                _mehediNextBtn.Location = new Point(516, 11);
                _mehediNextBtn.Text = "Finish";
                _mehediNextBtn.BackColor = ShihabSetupTheme.GetMehediAccentViolet();
            }
        }

        private void HandleMehediPrimaryButtonClick()
        {
            if (_mehediCurrentStep == 0)
            {
                ShowShihabWizardStep(1);
                return;
            }
            if (_mehediCurrentStep == 1)
            {
                string shihabChosenDir = (_mehediInstallDirBox.Text ?? "").Trim();
                if (string.IsNullOrWhiteSpace(shihabChosenDir))
                {
                    MessageBox.Show(this, "Please choose a valid installation folder.", "MHS Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                _shihabOptions.MehediInstallDirectory = Path.GetFullPath(shihabChosenDir);
                _shihabOptions.ShihabCreateDesktopShortcut = _mehediDesktopShortcutCheck.Checked;
                _shihabOptions.MehediCreateStartMenuShortcuts = _shihabStartMenuCheck.Checked;
                _shihabOptions.ShihabLaunchOnStartup = _mehediStartupCheck.Checked;
                _shihabOptions.MehediRegisterNativeHost = _shihabNativeHostCheck.Checked;

                if (!MehediInstallerEngine.CanWriteShihabTargetDirectory(_shihabOptions.MehediInstallDirectory) &&
                    !MehediInstallerEngine.IsShihabProcessElevated())
                {
                    DialogResult mehediElevAsk = MessageBox.Show(
                        this,
                        "Installing into \"" + _shihabOptions.MehediInstallDirectory + "\" requires Administrator permission.\n\n" +
                        "Click OK to continue with Administrator permission, or Cancel to pick another folder.",
                        "MHS Setup — Administrator Permission",
                        MessageBoxButtons.OKCancel,
                        MessageBoxIcon.Information);
                    if (mehediElevAsk == DialogResult.OK && TryRelaunchShihabSetupElevated())
                    {
                        Close();
                    }
                    return;
                }

                StartAsyncShihabInstallation();
                return;
            }
            if (_mehediCurrentStep == 3)
            {
                string mehediInstDir = _shihabOptions.MehediInstallDirectory;
                string shihabExtDir = Path.Combine(mehediInstDir, "extension");
                string mehediExePath = Path.Combine(mehediInstDir, "mdm.exe");

                if (_mehediOpenExtFolderCheck.Checked && Directory.Exists(shihabExtDir))
                {
                    try
                    {
                        ProcessStartInfo expPsi = new ProcessStartInfo(
                            ShihabProcessAndPathSafety.ResolveSystem32ToolPath("explorer.exe"),
                            ShihabProcessAndPathSafety.QuoteWindowsArgument(shihabExtDir));
                        expPsi.UseShellExecute = false;
                        Process.Start(expPsi);
                    }
                    catch { }
                }
                if (_shihabLaunchAppCheck.Checked && File.Exists(mehediExePath))
                {
                    try
                    {
                        ProcessStartInfo shihabPsi = new ProcessStartInfo(mehediExePath);
                        shihabPsi.WorkingDirectory = mehediInstDir;
                        shihabPsi.UseShellExecute = true;
                        Process.Start(shihabPsi);
                    }
                    catch { }
                }
                Close();
            }
        }

        private bool TryRelaunchShihabSetupElevated()
        {
            try
            {
                string shihabSelf = Process.GetCurrentProcess().MainModule.FileName;
                List<string> mehediArgs = new List<string>();
                mehediArgs.Add("--shihab-install-dir");
                mehediArgs.Add(_shihabOptions.MehediInstallDirectory ?? "");
                if (!_shihabOptions.ShihabCreateDesktopShortcut) mehediArgs.Add("--mehedi-no-desktop");
                if (!_shihabOptions.MehediCreateStartMenuShortcuts) mehediArgs.Add("--mehedi-no-startmenu");
                if (!_shihabOptions.ShihabLaunchOnStartup) mehediArgs.Add("--mehedi-no-startup");
                if (!_shihabOptions.MehediRegisterNativeHost) mehediArgs.Add("--mehedi-no-nativehost");
                mehediArgs.Add("--shihab-auto-install");

                ProcessStartInfo shihabPsi = new ProcessStartInfo(
                    shihabSelf,
                    ShihabProcessAndPathSafety.BuildWindowsArgumentString(mehediArgs.ToArray()));
                shihabPsi.UseShellExecute = true;
                shihabPsi.Verb = "runas";
                Process.Start(shihabPsi);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public void BeginShihabAutoInstallImmediately()
        {
            Shown += delegate { StartAsyncShihabInstallation(); };
        }

        private void StartAsyncShihabInstallation()
        {
            _shihabInstallingBusy = true;
            ShowShihabWizardStep(2);

            Task.Run(delegate
            {
                try
                {
                    MehediInstallerEngine.ExecuteShihabInstallation(_shihabOptions, delegate(int shihabPct, string mehediLine)
                    {
                        if (IsDisposed) return;
                        try
                        {
                            BeginInvoke((Action)delegate
                            {
                                if (IsDisposed) return;
                                _mehediProgressBar.MehediPercent = shihabPct;
                                _mehediProgressBar.ShihabCaption = shihabPct + "%";
                                _mehediProgressBar.Invalidate();
                                _shihabInstallLogBox.AppendText(mehediLine + "\r\n");
                            });
                        }
                        catch { }
                    });

                    if (IsDisposed) return;
                    BeginInvoke((Action)delegate
                    {
                        if (IsDisposed) return;
                        _shihabInstallingBusy = false;
                        string mehediExtPath = Path.Combine(_shihabOptions.MehediInstallDirectory, "extension");
                        _mehediFinalExtPathBox.Text = mehediExtPath;
                        try { Clipboard.SetText(mehediExtPath); } catch { }
                        ShowShihabWizardStep(3);
                    });
                }
                catch (Exception shihabEx)
                {
                    if (IsDisposed) return;
                    BeginInvoke((Action)delegate
                    {
                        _shihabInstallingBusy = false;
                        MessageBox.Show(
                            this,
                            "Installation failed:\n" + shihabEx.Message,
                            "MHS Setup Error",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Error);
                        ShowShihabWizardStep(1);
                    });
                }
            });
        }
    }

    public static class MehediSetupEntryPoint
    {
        [DllImport("user32.dll", EntryPoint = "SetProcessDPIAware")]
        private static extern bool EnableShihabDpiAwareness();

        [STAThread]
        public static int Main(string[] shihabArgs)
        {
            try { EnableShihabDpiAwareness(); } catch { }
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            ShihabInstallOptions mehediOpts = new ShihabInstallOptions();
            bool shihabSilent = false;
            bool mehediAutoInstall = false;
            bool shihabExplicitInstallDir = false;
            string shihabSelfLeaf = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName) ?? "";
            bool mehediUninstallMode = shihabSelfLeaf.StartsWith("Uninstall", StringComparison.OrdinalIgnoreCase);

            try
            {
                using (RegistryKey existingUninst = Registry.CurrentUser.OpenSubKey(ShihabSetupTheme.ShihabUninstallRegKey, false))
                {
                    if (existingUninst != null)
                    {
                        object existingLoc = existingUninst.GetValue("InstallLocation");
                        if (existingLoc != null && !string.IsNullOrWhiteSpace(existingLoc.ToString()))
                        {
                            mehediOpts.MehediInstallDirectory = Path.GetFullPath(existingLoc.ToString().Trim());
                        }
                    }
                }
            }
            catch { }

            if (shihabArgs != null)
            {
                for (int shihabI = 0; shihabI < shihabArgs.Length; shihabI++)
                {
                    string mehediArg = shihabArgs[shihabI] ?? "";
                    if (string.Equals(mehediArg, "--shihab-uninstall", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(mehediArg, "/uninstall", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediUninstallMode = true;
                    }
                    else if (string.Equals(mehediArg, "--shihab-silent", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(mehediArg, "--silent", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(mehediArg, "/silent", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(mehediArg, "/verysilent", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(mehediArg, "/S", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(mehediArg, "/quiet", StringComparison.OrdinalIgnoreCase))
                    {
                        shihabSilent = true;
                    }
                    else if (string.Equals(mehediArg, "--shihab-auto-install", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediAutoInstall = true;
                    }
                    else if (string.Equals(mehediArg, "--shihab-no-registry", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediOpts.ShihabSkipRegistryAndShortcuts = true;
                    }
                    else if (string.Equals(mehediArg, "--mehedi-no-desktop", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediOpts.ShihabCreateDesktopShortcut = false;
                    }
                    else if (string.Equals(mehediArg, "--mehedi-no-startmenu", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediOpts.MehediCreateStartMenuShortcuts = false;
                    }
                    else if (string.Equals(mehediArg, "--mehedi-no-startup", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediOpts.ShihabLaunchOnStartup = false;
                    }
                    else if (string.Equals(mehediArg, "--mehedi-no-nativehost", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediOpts.MehediRegisterNativeHost = false;
                    }
                    else if ((string.Equals(mehediArg, "--shihab-install-dir", StringComparison.OrdinalIgnoreCase) ||
                              string.Equals(mehediArg, "/D", StringComparison.OrdinalIgnoreCase)) &&
                             shihabI + 1 < shihabArgs.Length)
                    {
                        shihabI++;
                        mehediOpts.MehediInstallDirectory = shihabArgs[shihabI];
                        shihabExplicitInstallDir = true;
                    }
                    else if (string.Equals(mehediArg, "--shihab-update-mode", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(mehediArg, "--update", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediOpts.ShihabInPlaceUpdateMode = true;
                    }
                    else if (string.Equals(mehediArg, "--shihab-relaunch", StringComparison.OrdinalIgnoreCase) ||
                             string.Equals(mehediArg, "--relaunch", StringComparison.OrdinalIgnoreCase))
                    {
                        mehediOpts.ShihabRelaunchAfterInstall = true;
                    }
                    else if (string.Equals(mehediArg, "--shihab-wait-pid", StringComparison.OrdinalIgnoreCase) &&
                             shihabI + 1 < shihabArgs.Length)
                    {
                        shihabI++;
                        int parsedPid;
                        if (int.TryParse(shihabArgs[shihabI], out parsedPid) && parsedPid > 0)
                        {
                            mehediOpts.ShihabWaitForPid = parsedPid;
                        }
                    }
                    else if (string.Equals(mehediArg, "--shihab-updated-from", StringComparison.OrdinalIgnoreCase) &&
                             shihabI + 1 < shihabArgs.Length)
                    {
                        shihabI++;
                        mehediOpts.ShihabUpdatedFromVersion = shihabArgs[shihabI] ?? "";
                    }
                }
            }

            if (mehediUninstallMode)
            {
                string shihabTargetDir = MehediInstallerEngine.ResolveMehediExistingInstallDirForUninstall(
                    shihabExplicitInstallDir ? mehediOpts.MehediInstallDirectory : "");
                if (string.IsNullOrWhiteSpace(shihabTargetDir) || !Directory.Exists(shihabTargetDir))
                {
                    if (!shihabSilent)
                    {
                        MessageBox.Show("MHS Download Manager installation directory was not found.", "MHS Uninstaller", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    return 0;
                }

                if (!shihabSilent)
                {
                    DialogResult mehediConfirm = MessageBox.Show(
                        "Are you sure you want to uninstall " + ShihabSetupTheme.ShihabProductTitle + " from:\n\n" +
                        shihabTargetDir + "\n\n(Your downloaded files in your Downloads folders will NOT be deleted.)",
                        "Uninstall — " + ShihabSetupTheme.ShihabProductTitle,
                        MessageBoxButtons.YesNo,
                        MessageBoxIcon.Question);
                    if (mehediConfirm != DialogResult.Yes)
                    {
                        return 0;
                    }
                }

                MehediInstallerEngine.ExecuteMehediUninstall(shihabTargetDir, mehediOpts.ShihabSkipRegistryAndShortcuts);
                if (!shihabSilent)
                {
                    MessageBox.Show(
                        ShihabSetupTheme.ShihabProductTitle + " has been uninstalled cleanly.",
                        "Uninstall Complete",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                }
                return 0;
            }

            if (shihabSilent)
            {
                try
                {
                    MehediInstallerEngine.ExecuteShihabInstallation(mehediOpts, null);
                    if (mehediOpts.ShihabRelaunchAfterInstall)
                    {
                        try
                        {
                            string shihabInstalledExe = Path.Combine(Path.GetFullPath(mehediOpts.MehediInstallDirectory.Trim()), "mdm.exe");
                            if (File.Exists(shihabInstalledExe))
                            {
                                ProcessStartInfo shihabRelaunchPsi = new ProcessStartInfo(shihabInstalledExe);
                                if (!string.IsNullOrWhiteSpace(mehediOpts.ShihabUpdatedFromVersion))
                                {
                                    shihabRelaunchPsi.Arguments = ShihabProcessAndPathSafety.BuildWindowsArgumentString(
                                        "--shihab-updated-from",
                                        mehediOpts.ShihabUpdatedFromVersion);
                                }
                                shihabRelaunchPsi.WorkingDirectory = Path.GetDirectoryName(shihabInstalledExe);
                                shihabRelaunchPsi.UseShellExecute = false;
                                Process.Start(shihabRelaunchPsi);
                            }
                        }
                        catch { }
                    }
                    return 0;
                }
                catch
                {
                    return 1;
                }
            }

            ShihabSetupWizardForm mehediWizard = new ShihabSetupWizardForm(mehediOpts);
            if (mehediAutoInstall)
            {
                mehediWizard.BeginShihabAutoInstallImmediately();
            }
            Application.Run(mehediWizard);
            return 0;
        }
    }
}
