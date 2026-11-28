using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public static class ShihabCrashMonitor
    {
        private const int HeartbeatMmfSizeBytes = 4096;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMinimumWorkingSetSize, IntPtr dwMaximumWorkingSetSize);

        private static readonly TimeSpan CrashRegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex BearerRedactRegex = new Regex(
            @"(Authorization\s*:\s*Bearer\s+)[^\r\n\s]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            CrashRegexTimeout);
        private static readonly Regex BridgeTokenRedactRegex = new Regex(
            @"(X-MHS-Bridge-Token\s*:\s*)[^\r\n\s]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            CrashRegexTimeout);
        private static readonly Regex UrlCredsRedactRegex = new Regex(
            @"(https?|ftps?)://([^:/@\s]+):([^@/\s]+)@",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            CrashRegexTimeout);
        private static readonly Regex QueryTokenRedactRegex = new Regex(
            @"([?&](?:token|api_key|apikey|auth|sig|password)=)[^&\s]+",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            CrashRegexTimeout);

        private static readonly object _heartbeatMmfLock = new object();
        private static MemoryMappedFile _heartbeatMmf;
        private static MemoryMappedViewAccessor _heartbeatAccessor;
        private static System.Windows.Forms.Timer _heartbeatTimer;
        private static string _heartbeatFilePath;
        private static string _crashDirectory;
        private static string _activeSessionMarkerPath;
        private static int _hooksRegistered;

        private static string BuildHeartbeatMmfName(int pid)
        {
            return "Local\\ShihabMehediIDM_HB_" + pid.ToString(CultureInfo.InvariantCulture);
        }

        private static void InitializeHeartbeatMmf(int myPid)
        {
            lock (_heartbeatMmfLock)
            {
                DisposeHeartbeatMmfLocked();
                try
                {
                    string mapName = BuildHeartbeatMmfName(myPid);
                    _heartbeatMmf = MemoryMappedFile.CreateOrOpen(mapName, HeartbeatMmfSizeBytes, MemoryMappedFileAccess.ReadWrite);
                    _heartbeatAccessor = _heartbeatMmf.CreateViewAccessor(0, 8, MemoryMappedFileAccess.ReadWrite);
                }
                catch
                {
                    DisposeHeartbeatMmfLocked();
                }
            }
        }

        private static void DisposeHeartbeatMmfLocked()
        {
            if (_heartbeatAccessor != null)
            {
                try { _heartbeatAccessor.Dispose(); } catch { }
                _heartbeatAccessor = null;
            }
            if (_heartbeatMmf != null)
            {
                try { _heartbeatMmf.Dispose(); } catch { }
                _heartbeatMmf = null;
            }
        }

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

            int myPid = ShihabProcessAndPathSafety.CurrentProcessId;
            try
            {
                File.WriteAllText(_activeSessionMarkerPath, myPid.ToString(CultureInfo.InvariantCulture));
            }
            catch { }

            InitializeHeartbeatMmf(myPid);
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
            // Delete our own session marker FIRST so cross-thread timer disposal can never skip it.
            try
            {
                if (!string.IsNullOrEmpty(_activeSessionMarkerPath) && File.Exists(_activeSessionMarkerPath))
                {
                    string myPidStr = ShihabProcessAndPathSafety.CurrentProcessId.ToString(CultureInfo.InvariantCulture);
                    string markerVal = "";
                    try { markerVal = (File.ReadAllText(_activeSessionMarkerPath, Encoding.UTF8) ?? "").Trim(); } catch { }
                    if (string.IsNullOrEmpty(markerVal) || string.Equals(markerVal, myPidStr, StringComparison.Ordinal))
                    {
                        File.Delete(_activeSessionMarkerPath);
                    }
                }
            }
            catch { }

            try
            {
                System.Windows.Forms.Timer timer = Interlocked.Exchange(ref _heartbeatTimer, null);
                if (timer != null)
                {
                    timer.Stop();
                    timer.Dispose();
                }
            }
            catch { }

            lock (_heartbeatMmfLock)
            {
                DisposeHeartbeatMmfLocked();
            }
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
                string exePath = ShihabProcessAndPathSafety.CurrentExePath;
                if (string.IsNullOrEmpty(exePath)) exePath = Application.ExecutablePath;
                if (string.IsNullOrEmpty(exePath) || !File.Exists(exePath)) return;
                int myPid = ShihabProcessAndPathSafety.CurrentProcessId;
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
            MemoryMappedFile hbMmf = null;
            MemoryMappedViewAccessor hbAccessor = null;
            try
            {
                // Trim idle watchdog working set RAM (~2-4 MB) once at startup.
                try
                {
                    using (Process cur = Process.GetCurrentProcess())
                    {
                        SetProcessWorkingSetSize(cur.Handle, (IntPtr)(-1), (IntPtr)(-1));
                    }
                }
                catch { }

                string crashDir = Path.Combine(baseDataDir, "crashes");
                Directory.CreateDirectory(crashDir);
                string sessionRunning = Path.Combine(crashDir, "session.running");
                string dirtyMarker = Path.Combine(crashDir, "dirty_shutdown.marker");
                string heartbeatFile = Path.Combine(crashDir, "heartbeat.tick");
                string mmfName = BuildHeartbeatMmfName(parentPid);
                DateTime startedMonitoringUtc = DateTime.UtcNow;
                bool freezeReportedForCurrentStall = false;
                long lastSeenHeartbeatTicks = 0;

                try
                {
                    hbMmf = MemoryMappedFile.OpenExisting(mmfName, MemoryMappedFileRights.Read);
                    hbAccessor = hbMmf.CreateViewAccessor(0, 8, MemoryMappedFileAccess.Read);
                }
                catch
                {
                    if (hbAccessor != null) { try { hbAccessor.Dispose(); } catch { } hbAccessor = null; }
                    if (hbMmf != null) { try { hbMmf.Dispose(); } catch { } hbMmf = null; }
                }

                using (Process parent = Process.GetProcessById(parentPid))
                {
                    while (!parent.WaitForExit(5000))
                    {
                        try
                        {
                            if ((DateTime.UtcNow - startedMonitoringUtc).TotalSeconds < 15.0) continue;
                            long ticks = 0;
                            if (hbAccessor == null)
                            {
                                try
                                {
                                    hbMmf = MemoryMappedFile.OpenExisting(mmfName, MemoryMappedFileRights.Read);
                                    hbAccessor = hbMmf.CreateViewAccessor(0, 8, MemoryMappedFileAccess.Read);
                                }
                                catch
                                {
                                    if (hbAccessor != null) { try { hbAccessor.Dispose(); } catch { } hbAccessor = null; }
                                    if (hbMmf != null) { try { hbMmf.Dispose(); } catch { } hbMmf = null; }
                                }
                            }
                            if (hbAccessor != null)
                            {
                                ticks = hbAccessor.ReadInt64(0);
                            }
                            else if (File.Exists(heartbeatFile))
                            {
                                string tickStr = File.ReadAllText(heartbeatFile, Encoding.UTF8).Trim();
                                long.TryParse(tickStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks);
                            }

                            if (ticks > 0)
                            {
                                if (ticks != lastSeenHeartbeatTicks)
                                {
                                    lastSeenHeartbeatTicks = ticks;
                                    freezeReportedForCurrentStall = false;
                                }
                                DateTime hbUtc = new DateTime(ticks, DateTimeKind.Utc);
                                double stallSec = (DateTime.UtcNow - hbUtc).TotalSeconds;
                                if (stallSec >= 30.0 && !freezeReportedForCurrentStall)
                                {
                                    freezeReportedForCurrentStall = true;
                                    string freezeFile = Path.Combine(
                                        crashDir,
                                        string.Format(CultureInfo.InvariantCulture, "freeze_{0:yyyyMMdd_HHmmss}_watchdog.log", DateTime.Now));
                                    StringBuilder fsb = new StringBuilder();
                                    fsb.AppendLine("MHS Download Manager Out-of-Process Freeze Watchdog Report");
                                    fsb.AppendLine("Timestamp : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
                                    fsb.AppendLine("OS        : " + Environment.OSVersion);
                                    fsb.AppendLine("PID       : " + parentPid.ToString(CultureInfo.InvariantCulture));
                                    fsb.AppendLine(string.Format(CultureInfo.InvariantCulture, "StallSec  : {0:0.0}s without UI heartbeat", stallSec));
                                    fsb.AppendLine("Reason    : Monitored UI thread heartbeat stopped advancing.");
                                    File.WriteAllText(freezeFile, ScrubSensitivePaths(fsb.ToString()), Encoding.UTF8);
                                }
                            }
                        }
                        catch { }
                    }
                    int exitCode = 0;
                    try { exitCode = parent.ExitCode; } catch { }

                    bool sessionBelongsToExitedParent = false;
                    if (File.Exists(sessionRunning))
                    {
                        try
                        {
                            string markerPid = (File.ReadAllText(sessionRunning, Encoding.UTF8) ?? "").Trim();
                            if (string.IsNullOrEmpty(markerPid) ||
                                string.Equals(markerPid, parentPid.ToString(CultureInfo.InvariantCulture), StringComparison.Ordinal))
                            {
                                sessionBelongsToExitedParent = true;
                            }
                        }
                        catch { }
                    }
                    if (exitCode != 0 || sessionBelongsToExitedParent)
                    {
                        if (sessionBelongsToExitedParent)
                        {
                            try { if (File.Exists(sessionRunning)) File.Delete(sessionRunning); } catch { }
                        }
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
            finally
            {
                if (hbAccessor != null) { try { hbAccessor.Dispose(); } catch { } }
                if (hbMmf != null) { try { hbMmf.Dispose(); } catch { } }
            }
            return 0;
        }

        private static void TouchHeartbeatFile()
        {
            long nowTicks = DateTime.UtcNow.Ticks;
            lock (_heartbeatMmfLock)
            {
                if (_heartbeatAccessor != null)
                {
                    try
                    {
                        _heartbeatAccessor.Write(0, nowTicks);
                        return;
                    }
                    catch { }
                }
            }
            try
            {
                if (!string.IsNullOrEmpty(_heartbeatFilePath))
                {
                    File.WriteAllText(_heartbeatFilePath, nowTicks.ToString(CultureInfo.InvariantCulture));
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
                result = QueryTokenRedactRegex.Replace(result, "$1<REDACTED>");
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

