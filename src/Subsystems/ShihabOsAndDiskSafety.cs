using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace ShihabMehediIDM
{
    public static class ShihabOsAndDiskSafety
    {
        private const uint FSCTL_SET_SPARSE = 0x000900C4;
        private const uint ES_CONTINUOUS = 0x80000000;
        private const uint ES_SYSTEM_REQUIRED = 0x00000001;
        public const long DiskSafetyMarginBytes = 128L * 1024L * 1024L; // 128 MiB
        public const long Fat32MaxFileSizeBytes = 4294967295L; // 4 GiB - 1

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            Microsoft.Win32.SafeHandles.SafeFileHandle hDevice,
            uint dwIoControlCode,
            IntPtr InBuffer,
            uint nInBufferSize,
            IntPtr OutBuffer,
            uint nOutBufferSize,
            out uint pBytesReturned,
            IntPtr lpOverlapped);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetDiskFreeSpaceEx(
            string lpDirectoryName,
            out ulong lpFreeBytesAvailable,
            out ulong lpTotalNumberOfBytes,
            out ulong lpTotalNumberOfFreeBytes);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern bool GetVolumeInformation(
            string lpRootPathName,
            StringBuilder lpVolumeNameBuffer,
            int nVolumeNameSize,
            out uint lpVolumeSerialNumber,
            out uint lpMaximumComponentLength,
            out uint lpFileSystemFlags,
            StringBuilder lpFileSystemNameBuffer,
            int nFileSystemNameSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("psapi.dll", SetLastError = true)]
        private static extern bool EmptyWorkingSet(IntPtr hwProc);

        [DllImport("powrprof.dll", SetLastError = true)]
        private static extern bool SetSuspendState(bool Hibernate, bool ForceCritical, bool DisableWakeEvent);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern IntPtr CreateWaitableTimerW(IntPtr lpTimerAttributes, bool bManualReset, string lpTimerName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetWaitableTimer(IntPtr hTimer, [In] ref long pDueTime, int lPeriod, IntPtr pfnCompletionRoutine, IntPtr lpArgToCompletionRoutine, bool fResume);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFileW(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        private static IntPtr _activeWakeTimerHandle = IntPtr.Zero;
        private static readonly object _wakeTimerLock = new object();

        public static long GetAvailableDiskBytes(string directoryPath)
        {
            try
            {
                string dir = string.IsNullOrWhiteSpace(directoryPath) ? Environment.CurrentDirectory : directoryPath;
                if (!Directory.Exists(dir)) dir = Path.GetPathRoot(Path.GetFullPath(dir));
                ulong freeAvail = 0, total = 0, totalFree = 0;
                if (GetDiskFreeSpaceEx(dir, out freeAvail, out total, out totalFree))
                {
                    return (long)Math.Min((ulong)long.MaxValue, freeAvail);
                }
            }
            catch { }
            return long.MaxValue;
        }

        public static void TrimWorkingSetMemory()
        {
            try
            {
                GC.Collect(2, GCCollectionMode.Optimized, false);
                using (Process current = Process.GetCurrentProcess())
                {
                    EmptyWorkingSet(current.Handle);
                }
            }
            catch { }
        }

        public static bool EnterSystemSleepOrHibernate(bool hibernate)
        {
            try
            {
                return SetSuspendState(hibernate, false, false);
            }
            catch
            {
                return false;
            }
        }

        public static void ScheduleWakeTimer(DateTime wakeLocalTime)
        {
            lock (_wakeTimerLock)
            {
                if (_activeWakeTimerHandle != IntPtr.Zero)
                {
                    try { CloseHandle(_activeWakeTimerHandle); } catch { }
                    _activeWakeTimerHandle = IntPtr.Zero;
                }
                if (wakeLocalTime <= DateTime.Now) return;
                try
                {
                    IntPtr hTimer = CreateWaitableTimerW(IntPtr.Zero, true, null);
                    if (hTimer != IntPtr.Zero)
                    {
                        long dueFileTime = wakeLocalTime.ToFileTimeUtc();
                        if (SetWaitableTimer(hTimer, ref dueFileTime, 0, IntPtr.Zero, IntPtr.Zero, true))
                        {
                            _activeWakeTimerHandle = hTimer;
                        }
                        else
                        {
                            CloseHandle(hTimer);
                        }
                    }
                }
                catch { }
            }
        }

        public static bool TryMarkFileSparse(FileStream fs)
        {
            if (fs == null || fs.SafeFileHandle == null || fs.SafeFileHandle.IsInvalid)
            {
                return false;
            }
            try
            {
                uint bytesReturned = 0;
                return DeviceIoControl(
                    fs.SafeFileHandle,
                    FSCTL_SET_SPARSE,
                    IntPtr.Zero,
                    0,
                    IntPtr.Zero,
                    0,
                    out bytesReturned,
                    IntPtr.Zero);
            }
            catch
            {
                return false;
            }
        }

        public static bool VerifyDiskPreflight(string targetDirectory, long requiredBytes, out string errorMessage)
        {
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(targetDirectory))
            {
                return true;
            }
            try
            {
                Directory.CreateDirectory(targetDirectory);
                string root = Path.GetPathRoot(Path.GetFullPath(targetDirectory));
                if (!string.IsNullOrEmpty(root) && requiredBytes > Fat32MaxFileSizeBytes)
                {
                    StringBuilder volName = new StringBuilder(261);
                    StringBuilder fsName = new StringBuilder(261);
                    uint serial = 0, maxComp = 0, flags = 0;
                    if (GetVolumeInformation(root, volName, volName.Capacity, out serial, out maxComp, out flags, fsName, fsName.Capacity))
                    {
                        string fs = fsName.ToString().Trim().ToUpperInvariant();
                        if (fs == "FAT32" || fs == "FAT" || fs == "FAT16")
                        {
                            errorMessage = string.Format("File size ({0}) exceeds the 4 GB maximum file size limit of the {1} volume ({2}).",
                                ShihabFormatHelpers.FormatMehediBytes(requiredBytes), fs, root);
                            return false;
                        }
                    }
                }

                if (requiredBytes > 0)
                {
                    ulong freeAvail = 0, totalBytes = 0, totalFree = 0;
                    if (GetDiskFreeSpaceEx(targetDirectory, out freeAvail, out totalBytes, out totalFree))
                    {
                        ulong reqUnsigned = (ulong)requiredBytes;
                        ulong marginUnsigned = (ulong)DiskSafetyMarginBytes;
                        ulong neededWithMargin = reqUnsigned > (ulong.MaxValue - marginUnsigned)
                            ? ulong.MaxValue
                            : (reqUnsigned + marginUnsigned);
                        if (freeAvail < neededWithMargin)
                        {
                            errorMessage = string.Format("Insufficient disk space on {0}: {1} available, {2} required (including 128 MB safety margin).",
                                string.IsNullOrEmpty(root) ? targetDirectory : root,
                                ShihabFormatHelpers.FormatMehediBytes((long)Math.Min((ulong)long.MaxValue, freeAvail)),
                                ShihabFormatHelpers.FormatMehediBytes((long)Math.Min((ulong)long.MaxValue, neededWithMargin)));
                            return false;
                        }
                    }
                }
            }
            catch
            {
            }
            return true;
        }

        public static void WriteMarkOfTheWeb(string filePath, string hostUrl, string refererUrl)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return;
            }
            try
            {
                FileAttributes attrs = File.GetAttributes(filePath);
                if ((attrs & FileAttributes.ReparsePoint) != 0)
                {
                    return;
                }

                string adsPath = filePath + ":Zone.Identifier";
                const uint GENERIC_WRITE = 0x40000000;
                const uint FILE_SHARE_READ = 0x00000001;
                const uint CREATE_ALWAYS = 2;
                const uint FILE_ATTRIBUTE_NORMAL = 0x00000080;

                using (Microsoft.Win32.SafeHandles.SafeFileHandle handle = CreateFileW(
                    adsPath, GENERIC_WRITE, FILE_SHARE_READ, IntPtr.Zero, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero))
                {
                    if (handle != null && !handle.IsInvalid)
                    {
                        using (FileStream fs = new FileStream(handle, FileAccess.Write))
                        {
                            StringBuilder sb = new StringBuilder();
                            sb.Append("[ZoneTransfer]\r\nZoneId=3\r\n");
                            if (!string.IsNullOrWhiteSpace(refererUrl))
                            {
                                sb.Append("ReferrerUrl=").Append(SanitizeZoneUrl(refererUrl)).Append("\r\n");
                            }
                            if (!string.IsNullOrWhiteSpace(hostUrl))
                            {
                                sb.Append("HostUrl=").Append(SanitizeZoneUrl(hostUrl)).Append("\r\n");
                            }
                            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());
                            fs.Write(bytes, 0, bytes.Length);
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static string SanitizeZoneUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return "";
            string clean = url.Replace("\r", "").Replace("\n", "").Replace("\0", "").Trim();
            Uri u;
            if (Uri.TryCreate(clean, UriKind.Absolute, out u) && !string.IsNullOrEmpty(u.UserInfo))
            {
                return u.GetComponents(UriComponents.HttpRequestUrl, UriFormat.UriEscaped);
            }
            return clean;
        }

        public static void PropagateMarkOfTheWebToDirectory(string dirOrFilePath, string hostUrl, string refererUrl)
        {
            if (string.IsNullOrWhiteSpace(dirOrFilePath)) return;
            try
            {
                if (File.Exists(dirOrFilePath))
                {
                    WriteMarkOfTheWeb(dirOrFilePath, hostUrl, refererUrl);
                    return;
                }
                if (Directory.Exists(dirOrFilePath))
                {
                    Queue<string> pendingDirs = new Queue<string>();
                    pendingDirs.Enqueue(dirOrFilePath);
                    int visitedDirs = 0;
                    const int maxVisitedDirs = 10000;
                    while (pendingDirs.Count > 0 && visitedDirs < maxVisitedDirs)
                    {
                        string currentDir = pendingDirs.Dequeue();
                        visitedDirs++;
                        try
                        {
                            FileAttributes dirAttrs = File.GetAttributes(currentDir);
                            if ((dirAttrs & FileAttributes.ReparsePoint) != 0)
                            {
                                continue;
                            }
                        }
                        catch
                        {
                            continue;
                        }

                        try
                        {
                            foreach (string file in Directory.EnumerateFiles(currentDir))
                            {
                                WriteMarkOfTheWeb(file, hostUrl, refererUrl);
                            }
                            foreach (string subDir in Directory.EnumerateDirectories(currentDir))
                            {
                                pendingDirs.Enqueue(subDir);
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
        }

        public static void UpdateSystemSleepPrevention(bool activeTransfersRunning, bool preventSleepEnabled)
        {
            try
            {
                Action apply = delegate
                {
                    try
                    {
                        if (activeTransfersRunning && preventSleepEnabled)
                        {
                            SetThreadExecutionState(ES_CONTINUOUS | ES_SYSTEM_REQUIRED);
                        }
                        else
                        {
                            SetThreadExecutionState(ES_CONTINUOUS);
                        }
                    }
                    catch { }
                };

                if (Application.OpenForms.Count > 0)
                {
                    Form mainForm = Application.OpenForms[0];
                    if (mainForm != null && !mainForm.IsDisposed && mainForm.IsHandleCreated)
                    {
                        if (mainForm.InvokeRequired)
                        {
                            mainForm.BeginInvoke(apply);
                            return;
                        }
                    }
                }
                apply();
            }
            catch
            {
            }
        }
    }
}

