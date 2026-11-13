using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public static class ShihabCrashMonitor
    {
        private static readonly TimeSpan CrashRegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex BearerRedactRegex = new Regex(
            @"(Authorization\s*:\s*Bearer\s+)[^\r\n\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            CrashRegexTimeout);
        private static readonly Regex BridgeTokenRedactRegex = new Regex(
            @"(X-MHS-Bridge-Token\s*:\s*)[^\r\n\s]+",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            CrashRegexTimeout);
        private static readonly Regex UrlCredsRedactRegex = new Regex(
            @"(https?|ftps?)://([^:/@\s]+):([^@/\s]+)@",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            CrashRegexTimeout);

        private static System.Windows.Forms.Timer _heartbeatTimer;
        private static string _heartbeatFilePath;
        private static string _crashDirectory;
        private static string _activeSessionMarkerPath;
        private static int _hooksRegistered;

        public static void InitializeInMainProcess(string baseDataDir, bool enabled)
        {
            _crashDirectory = Path.Combine(baseDataDir, "crashes");
            Directory.CreateDirectory(_crashDirectory);
            _heartbeatFilePath = Path.Combine(_crashDirectory, "heartbeat.tick");
            _activeSessionMarkerPath = Path.Combine(_crashDirectory, "session.running");

            if (Interlocked.Exchange(ref _hooksRegistered, 1) == 0)
            {
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    WriteCrashReport("UnhandledAppDomainException", e.ExceptionObject as Exception);
                };
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    WriteCrashReport("UIThreadException", e.Exception);
                };
                Application.ApplicationExit += delegate
                {
                    MarkCleanShutdown();
                };
            }

            if (!enabled) return;

            try
            {
                File.WriteAllText(_activeSessionMarkerPath, Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
            }
            catch { }

            TouchHeartbeatFile();
            System.Windows.Forms.Timer prevTimer = _heartbeatTimer;
            if (prevTimer != null)
            {
                try { prevTimer.Stop(); prevTimer.Dispose(); } catch { }
            }
            _heartbeatTimer = new System.Windows.Forms.Timer();
            _heartbeatTimer.Interval = 2000;
            _heartbeatTimer.Tick += delegate { TouchHeartbeatFile(); };
            _heartbeatTimer.Start();

            SpawnOutOfProcessWatchdog(baseDataDir);
        }

        public static void MarkCleanShutdown()
        {
            try
            {
                System.Windows.Forms.Timer timer = Interlocked.Exchange(ref _heartbeatTimer, null);
                if (timer != null)
                {
                    timer.Stop();
                    timer.Dispose();
                }
                if (!string.IsNullOrEmpty(_activeSessionMarkerPath) && File.Exists(_activeSessionMarkerPath))
                {
                    File.Delete(_activeSessionMarkerPath);
                }
            }
            catch { }
        }

        public static bool CheckAndClearUncleanShutdownMarker(string baseDataDir, out string latestCrashLogPath)
        {
            latestCrashLogPath = "";
            try
            {
                string crashDir = Path.Combine(baseDataDir, "crashes");
                string dirtyMarker = Path.Combine(crashDir, "dirty_shutdown.marker");
                bool hadUnclean = false;
                if (File.Exists(dirtyMarker))
                {
                    hadUnclean = true;
                    try
                    {
                        string rawLog = File.ReadAllText(dirtyMarker).Trim();
                        string leaf = Path.GetFileName(rawLog);
                        string resolved;
                        if (!string.IsNullOrEmpty(leaf) &&
                            ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(crashDir, leaf, out resolved))
                        {
                            latestCrashLogPath = resolved;
                        }
                    }
                    catch { }
                    try { File.Delete(dirtyMarker); } catch { }
                }
                if (!hadUnclean) return false;
                if (string.IsNullOrEmpty(latestCrashLogPath) || !File.Exists(latestCrashLogPath))
                {
                    List<string> logs = GetRecentCrashLogs(baseDataDir);
                    if (logs.Count > 0) latestCrashLogPath = logs[0];
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void SpawnOutOfProcessWatchdog(string baseDataDir)
        {
            try
            {
                string exePath = Application.ExecutablePath;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;
                int myPid = Process.GetCurrentProcess().Id;
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = exePath;
                psi.Arguments = ShihabProcessAndPathSafety.BuildWindowsArgumentString(
                    "--crash-monitor",
                    myPid.ToString(CultureInfo.InvariantCulture),
                    baseDataDir);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                using (Process p = Process.Start(psi))
                {
                }
            }
            catch { }
        }

        public static int RunOutOfProcessWatchdog(int parentPid, string baseDataDir)
        {
            if (parentPid <= 0 || string.IsNullOrWhiteSpace(baseDataDir)) return 0;
            try
            {
                string crashDir = Path.Combine(baseDataDir, "crashes");
                Directory.CreateDirectory(crashDir);
                string sessionRunning = Path.Combine(crashDir, "session.running");
                string dirtyMarker = Path.Combine(crashDir, "dirty_shutdown.marker");

                using (Process parent = Process.GetProcessById(parentPid))
                {
                    parent.WaitForExit();
                    int exitCode = 0;
                    try { exitCode = parent.ExitCode; } catch { }

                    bool sessionStillMarkedRunning = File.Exists(sessionRunning);
                    if (exitCode != 0 || sessionStillMarkedRunning)
                    {
                        try { if (File.Exists(sessionRunning)) File.Delete(sessionRunning); } catch { }
                        string logFile = Path.Combine(
                            crashDir,
                            string.Format(CultureInfo.InvariantCulture, "crash_{0:yyyyMMdd_HHmmss}_watchdog.log", DateTime.Now));
                        StringBuilder sb = new StringBuilder();
                        sb.AppendLine("MHS Download Manager Out-of-Process Crash Watchdog Report");
                        sb.AppendLine("Timestamp : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                        sb.AppendLine("OS        : " + Environment.OSVersion);
                        sb.AppendLine("ExitCode  : 0x" + exitCode.ToString("X8", CultureInfo.InvariantCulture) + " (" + exitCode.ToString(CultureInfo.InvariantCulture) + ")");
                        sb.AppendLine("Reason    : Monitored main process exited abnormally without clean shutdown marker.");
                        File.WriteAllText(logFile, ScrubSensitivePaths(sb.ToString()), Encoding.UTF8);
                        File.WriteAllText(dirtyMarker, logFile, Encoding.UTF8);
                    }
                }
            }
            catch { }
            return 0;
        }

        private static void TouchHeartbeatFile()
        {
            try
            {
                if (!string.IsNullOrEmpty(_heartbeatFilePath))
                {
                    File.WriteAllText(_heartbeatFilePath, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
                }
            }
            catch { }
        }

        public static string ScrubSensitivePaths(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            string result = raw;
            try
            {
                string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(userProfile))
                {
                    result = Regex.Replace(result, Regex.Escape(userProfile), "<USERPROFILE>", RegexOptions.IgnoreCase, CrashRegexTimeout);
                }
                string userName = Environment.UserName;
                if (!string.IsNullOrEmpty(userName) && userName.Length >= 3)
                {
                    result = Regex.Replace(result, "\\\\Users\\\\" + Regex.Escape(userName), "\\Users\\<USER>", RegexOptions.IgnoreCase, CrashRegexTimeout);
                }
                result = BearerRedactRegex.Replace(result, "$1<REDACTED>");
                result = BridgeTokenRedactRegex.Replace(result, "$1<REDACTED>");
                result = UrlCredsRedactRegex.Replace(result, "$1://$2:<REDACTED>@");
            }
            catch { }
            return result;
        }

        public static void WriteCrashReport(string kind, Exception ex)
        {
            try
            {
                if (string.IsNullOrEmpty(_crashDirectory)) return;
                Directory.CreateDirectory(_crashDirectory);
                string file = Path.Combine(
                    _crashDirectory,
                    string.Format(CultureInfo.InvariantCulture, "crash_{0:yyyyMMdd_HHmmss}.log", DateTime.Now));
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("MHS Download Manager Diagnostic Crash Report");
                sb.AppendLine("Version   : " + ShihabAutoUpdater.GetCurrentAppVersion());
                sb.AppendLine("Timestamp : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                sb.AppendLine("OS        : " + Environment.OSVersion);
                sb.AppendLine("CLR       : " + Environment.Version);
                sb.AppendLine("Kind      : " + kind);
                if (ex != null)
                {
                    sb.AppendLine("Message   : " + ex.Message);
                    sb.AppendLine("Stack     :");
                    sb.AppendLine(ex.ToString());
                }
                File.WriteAllText(file, ScrubSensitivePaths(sb.ToString()), Encoding.UTF8);
                File.WriteAllText(Path.Combine(_crashDirectory, "dirty_shutdown.marker"), file, Encoding.UTF8);
            }
            catch { }
        }

        public static List<string> GetRecentCrashLogs(string baseDataDir)
        {
            List<string> list = new List<string>();
            try
            {
                string dir = Path.Combine(baseDataDir, "crashes");
                if (Directory.Exists(dir))
                {
                    string[] files = Directory.GetFiles(dir, "crash_*.log");
                    Array.Sort(files, StringComparer.OrdinalIgnoreCase);
                    Array.Reverse(files);
                    list.AddRange(files);
                }
            }
            catch { }
            return list;
        }
    }
}

