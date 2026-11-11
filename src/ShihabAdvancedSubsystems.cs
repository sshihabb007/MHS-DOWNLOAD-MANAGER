using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml;
using Microsoft.VisualBasic.FileIO;

namespace ShihabMehediIDM
{
    // =========================================================================
    // 1. OS & DISK SAFETY: NTFS SPARSE FILES, DISK PREFLIGHT, MOTW & SLEEP LOCK
    // =========================================================================
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
                EmptyWorkingSet(Process.GetCurrentProcess().Handle);
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
                        ulong neededWithMargin = (ulong)requiredBytes + (ulong)DiskSafetyMarginBytes;
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
            string clean = url.Replace("\r", "").Replace("\n", "").Trim();
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
                    string[] files = Directory.GetFiles(dirOrFilePath, "*", System.IO.SearchOption.AllDirectories);
                    int limit = Math.Min(files.Length, 2000);
                    for (int i = 0; i < limit; i++)
                    {
                        WriteMarkOfTheWeb(files[i], hostUrl, refererUrl);
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

    // =========================================================================
    // 2. MULTI-ALGORITHM CHECKSUM VERIFIER (CRC32, MD5, SHA-1, SHA-256, SHA-512)
    // =========================================================================
    public static class ShihabChecksumVerifier
    {
        private static readonly uint[] Crc32Table = BuildCrc32Table();

        private static uint[] BuildCrc32Table()
        {
            uint[] table = new uint[256];
            for (uint i = 0; i < 256; i++)
            {
                uint c = i;
                for (int k = 0; k < 8; k++)
                {
                    if ((c & 1) != 0)
                        c = 0xEDB88320u ^ (c >> 1);
                    else
                        c >>= 1;
                }
                table[i] = c;
            }
            return table;
        }

        public static bool ParseExpectedChecksumSpec(string rawInput, out string algorithm, out string expectedHex)
        {
            algorithm = "SHA-256";
            expectedHex = "";
            if (string.IsNullOrWhiteSpace(rawInput))
            {
                return false;
            }
            string s = rawInput.Trim();

            // Support BSD-style "SHA256 (file) = <hex>"
            Match bsdMatch = Regex.Match(s, @"^(CRC32|MD5|SHA-?1|SHA-?256|SHA-?512)\s*\([^)]+\)\s*=\s*([0-9A-Fa-f]{8,128})$", RegexOptions.IgnoreCase);
            if (bsdMatch.Success)
            {
                s = bsdMatch.Groups[1].Value + ":" + bsdMatch.Groups[2].Value;
            }

            int colon = s.IndexOf(':');
            if (colon <= 0) colon = s.IndexOf('=');
            if (colon > 0)
            {
                string prefix = s.Substring(0, colon).Trim().ToUpperInvariant().Replace("-", "");
                string rest = s.Substring(colon + 1).Trim().Trim('"', '\'');
                int wsInRest = rest.IndexOfAny(new char[] { ' ', '\t' });
                if (wsInRest > 0) rest = rest.Substring(0, wsInRest);
                if (prefix == "CRC32" || prefix == "CRC") algorithm = "CRC32";
                else if (prefix == "MD5") algorithm = "MD5";
                else if (prefix == "SHA1") algorithm = "SHA-1";
                else if (prefix == "SHA256") algorithm = "SHA-256";
                else if (prefix == "SHA512") algorithm = "SHA-512";
                else return false;
                expectedHex = NormalizeHexOrBase64ToHex(rest, algorithm);
                return !string.IsNullOrEmpty(expectedHex);
            }

            // Support GNU sidecar format "<hex>  *filename" or "<hex>  filename"
            string token = s.Trim('"', '\'');
            int wsIdx = token.IndexOfAny(new char[] { ' ', '\t' });
            if (wsIdx >= 8)
            {
                token = token.Substring(0, wsIdx);
            }

            string cleanHex = token.Replace(" ", "").ToLowerInvariant();
            for (int i = 0; i < cleanHex.Length; i++)
            {
                char c = cleanHex[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')))
                {
                    return false;
                }
            }
            if (cleanHex.Length == 8) { algorithm = "CRC32"; expectedHex = cleanHex; return true; }
            if (cleanHex.Length == 32) { algorithm = "MD5"; expectedHex = cleanHex; return true; }
            if (cleanHex.Length == 40) { algorithm = "SHA-1"; expectedHex = cleanHex; return true; }
            if (cleanHex.Length == 64) { algorithm = "SHA-256"; expectedHex = cleanHex; return true; }
            if (cleanHex.Length == 128) { algorithm = "SHA-512"; expectedHex = cleanHex; return true; }
            return false;
        }

        private static string NormalizeHexOrBase64ToHex(string val, string algo)
        {
            if (string.IsNullOrWhiteSpace(val)) return "";
            string clean = val.Trim().Trim(':');
            bool isHex = true;
            for (int i = 0; i < clean.Length; i++)
            {
                char c = clean[i];
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    isHex = false;
                    break;
                }
            }
            int expectedHexLen = 64;
            if (algo == "CRC32") expectedHexLen = 8;
            else if (algo == "MD5") expectedHexLen = 32;
            else if (algo == "SHA-1") expectedHexLen = 40;
            else if (algo == "SHA-512") expectedHexLen = 128;

            if (isHex && clean.Length == expectedHexLen)
            {
                return clean.ToLowerInvariant();
            }
            try
            {
                byte[] decoded = Convert.FromBase64String(clean);
                if (decoded.Length * 2 == expectedHexLen)
                {
                    StringBuilder sb = new StringBuilder(expectedHexLen);
                    for (int i = 0; i < decoded.Length; i++)
                    {
                        sb.Append(decoded[i].ToString("x2"));
                    }
                    return sb.ToString();
                }
            }
            catch
            {
            }
            return "";
        }

        public static string ExtractServerDigestSpec(WebHeaderCollection headers, string etag)
        {
            return ExtractServerDigestSpec(headers, etag, 200);
        }

        public static string ExtractServerDigestSpec(WebHeaderCollection headers, string etag, int httpStatusCode)
        {
            if (headers != null)
            {
                string reprDigest = headers["Repr-Digest"] ?? headers["Digest"];
                if (!string.IsNullOrWhiteSpace(reprDigest) && httpStatusCode != 206)
                {
                    string[] parts = reprDigest.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    string[] priority = new string[] { "sha-512", "sha-256", "sha", "sha-1", "md5", "crc32c", "crc32" };
                    for (int p = 0; p < priority.Length; p++)
                    {
                        for (int i = 0; i < parts.Length; i++)
                        {
                            string token = parts[i].Trim();
                            int eq = token.IndexOf('=');
                            if (eq > 0)
                            {
                                string k = token.Substring(0, eq).Trim().ToLowerInvariant();
                                string v = token.Substring(eq + 1).Trim().Trim(':', '"', '\'');
                                if (k == priority[p])
                                {
                                    string algoName = "SHA-256";
                                    if (k == "sha-512") algoName = "SHA-512";
                                    else if (k == "sha-256") algoName = "SHA-256";
                                    else if (k == "sha" || k == "sha-1") algoName = "SHA-1";
                                    else if (k == "md5") algoName = "MD5";
                                    else if (k == "crc32") algoName = "CRC32";
                                    else continue;
                                    string hex = NormalizeHexOrBase64ToHex(v, algoName);
                                    if (!string.IsNullOrEmpty(hex))
                                    {
                                        return algoName.ToLowerInvariant().Replace("-", "") + ":" + hex;
                                    }
                                }
                            }
                        }
                    }
                }

                string contentMd5 = headers["Content-MD5"];
                if (!string.IsNullOrWhiteSpace(contentMd5) && httpStatusCode != 206)
                {
                    string hex = NormalizeHexOrBase64ToHex(contentMd5.Trim(), "MD5");
                    if (!string.IsNullOrEmpty(hex))
                    {
                        return "md5:" + hex;
                    }
                }

                string googHash = headers["x-goog-hash"];
                if (!string.IsNullOrWhiteSpace(googHash))
                {
                    string[] parts = googHash.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < parts.Length; i++)
                    {
                        string t = parts[i].Trim();
                        if (t.StartsWith("md5=", StringComparison.OrdinalIgnoreCase))
                        {
                            string hex = NormalizeHexOrBase64ToHex(t.Substring(4).Trim(), "MD5");
                            if (!string.IsNullOrEmpty(hex)) return "md5:" + hex;
                        }
                    }
                }
            }

            if (httpStatusCode != 206 && !string.IsNullOrWhiteSpace(etag) && !etag.TrimStart().StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            {
                string cleanEtag = etag.Trim().Trim('"');
                if (cleanEtag.Length == 32 && cleanEtag.IndexOf('-') < 0)
                {
                    string algo;
                    string hex;
                    if (ParseExpectedChecksumSpec("md5:" + cleanEtag, out algo, out hex))
                    {
                        return "md5:" + hex;
                    }
                }
            }
            return "";
        }

        public static string ComputeStreamChecksumHex(Stream fs, string algorithm)
        {
            if (fs == null) return "";
            if (fs.CanSeek) fs.Seek(0, SeekOrigin.Begin);
            string algo = (algorithm ?? "SHA-256").Trim().ToUpperInvariant();
            if (algo == "CRC32" || algo == "CRC")
            {
                uint crc = 0xFFFFFFFFu;
                byte[] buf = new byte[65536];
                int read;
                while ((read = fs.Read(buf, 0, buf.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        crc = Crc32Table[(crc ^ buf[i]) & 0xFF] ^ (crc >> 8);
                    }
                }
                crc ^= 0xFFFFFFFFu;
                return crc.ToString("x8");
            }

            HashAlgorithm hasher = null;
            if (algo == "MD5") hasher = MD5.Create();
            else if (algo == "SHA-1" || algo == "SHA1") hasher = SHA1.Create();
            else if (algo == "SHA-512" || algo == "SHA512") hasher = SHA512.Create();
            else hasher = SHA256.Create();

            using (hasher)
            {
                byte[] hash = hasher.ComputeHash(fs);
                StringBuilder sb = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    sb.Append(hash[i].ToString("x2"));
                }
                return sb.ToString();
            }
        }

        public static string ComputeFileChecksumHex(string filePath, string algorithm)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            {
                return "";
            }
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 131072))
            {
                return ComputeStreamChecksumHex(fs, algorithm);
            }
        }

        public static bool VerifyExpectedChecksum(
            string filePath,
            string expectedSpec,
            out string algorithm,
            out string computedHex,
            out string statusSummary)
        {
            algorithm = "SHA-256";
            computedHex = "";
            statusSummary = "";
            if (string.IsNullOrWhiteSpace(expectedSpec))
            {
                computedHex = ComputeFileChecksumHex(filePath, "SHA-256");
                statusSummary = "SHA-256 computed";
                return true;
            }
            string expHex;
            if (!ParseExpectedChecksumSpec(expectedSpec, out algorithm, out expHex))
            {
                computedHex = ComputeFileChecksumHex(filePath, "SHA-256");
                statusSummary = "Invalid checksum specification (rejected fail-closed): " + expectedSpec.Trim();
                return false;
            }
            computedHex = ComputeFileChecksumHex(filePath, algorithm);
            bool match = string.Equals(computedHex, expHex, StringComparison.OrdinalIgnoreCase);
            statusSummary = match
                ? string.Format("{0} Verified", algorithm)
                : string.Format("{0} MISMATCH (expected {1}, got {2})", algorithm, expHex, computedHex);
            return match;
        }

        public static bool VerifyExpectedChecksum(
            string filePath,
            string expectedSpec,
            out string computedFormatted,
            out string statusSummary)
        {
            return VerifyFileAgainstSpec(filePath, expectedSpec, out computedFormatted, out statusSummary);
        }

        public static bool VerifyFileAgainstSpec(
            string filePath,
            string expectedSpec,
            out string computedFormatted,
            out string statusSummary)
        {
            computedFormatted = "";
            statusSummary = "";
            if (string.IsNullOrWhiteSpace(expectedSpec))
            {
                string shaHex = ComputeFileChecksumHex(filePath, "SHA-256");
                computedFormatted = string.IsNullOrEmpty(shaHex) ? "" : ("sha256:" + shaHex);
                statusSummary = "SHA-256 computed";
                return true;
            }
            string algo;
            string expectedHex;
            if (!ParseExpectedChecksumSpec(expectedSpec, out algo, out expectedHex))
            {
                computedFormatted = "";
                statusSummary = "Invalid checksum specification (rejected fail-closed): " + expectedSpec.Trim();
                return false;
            }
            string actualHex = ComputeFileChecksumHex(filePath, algo);
            computedFormatted = algo.ToLowerInvariant().Replace("-", "") + ":" + actualHex;
            bool match = string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase);
            statusSummary = match
                ? string.Format("{0} Verified", algo)
                : string.Format("{0} MISMATCH (expected {1}, got {2})", algo, expectedHex, actualHex);
            return match;
        }
    }

    // =========================================================================
    // 3. OFFLINE IP-TO-COUNTRY GEOLOCATION & FLAG RENDERER
    // =========================================================================
    public static class ShihabGeoIpLookup
    {
        private static readonly Dictionary<string, Bitmap> FlagBitmapCache = new Dictionary<string, Bitmap>(StringComparer.OrdinalIgnoreCase);

        public static string LookupCountryCodeForIp(IPAddress ip)
        {
            if (ip == null) return "UN";
            if (IPAddress.IsLoopback(ip)) return "LO";

            byte[] b = ip.GetAddressBytes();
            if (ip.AddressFamily == AddressFamily.InterNetwork && b.Length == 4)
            {
                byte b0 = b[0];
                byte b1 = b[1];
                if (b0 == 10 || (b0 == 172 && b1 >= 16 && b1 <= 31) || (b0 == 192 && b1 == 168) || b0 == 127)
                {
                    return "LO";
                }
                // Major CDN & RIR first-octet / prefix mapping
                if (b0 == 103)
                {
                    if (b1 >= 100 && b1 <= 125) return "BD";
                    if (b1 < 50) return "SG";
                    return "IN";
                }
                if (b0 == 114 || b0 == 116 || b0 == 118 || b0 == 120 || b0 == 220) return "CN";
                if (b0 == 126 || b0 == 133 || b0 == 153 || b0 == 210) return "JP";
                if (b0 == 112 || b0 == 175 || b0 == 211) return "KR";
                if (b0 == 49 || b0 == 106 || b0 == 117 || b0 == 122) return "IN";
                if (b0 == 1 || b0 == 14 || b0 == 27 || b0 == 58 || b0 == 61) return "AU";
                if (b0 == 2 || b0 == 51 || b0 == 80 || b0 == 82 || b0 == 90) return "FR";
                if (b0 == 5 || b0 == 31 || b0 == 37 || b0 == 46 || b0 == 78 || b0 == 85 || b0 == 88 || b0 == 95) return "DE";
                if (b0 == 77 || b0 == 79 || b0 == 81 || b0 == 86 || b0 == 87) return "GB";
                if (b0 == 91 || b0 == 92 || b0 == 93 || b0 == 94 || b0 == 109 || b0 == 145 || b0 == 185 || b0 == 193 || b0 == 194 || b0 == 195 || b0 == 212 || b0 == 213 || b0 == 217)
                {
                    if (b1 < 64) return "NL";
                    if (b1 < 128) return "DE";
                    if (b1 < 192) return "GB";
                    return "SE";
                }
                if (b0 == 176 || b0 == 178 || b0 == 188) return "NL";
                if (b0 == 177 || b0 == 179 || b0 == 186 || b0 == 187 || b0 == 189 || b0 == 191 || b0 == 200 || b0 == 201) return "BR";
                if (b0 == 196 || b0 == 197 || b0 == 41 || b0 == 102 || b0 == 105) return "ZA";
                if (b0 == 24 || b0 == 70 || b0 == 99 || b0 == 142) return "CA";
                // ARIN / North America & global CDNs (Cloudflare 104/172, Google 8/34/35/142, AWS 3/13/18/52/54, Akamai 23, Fastly 151, GitHub 140)
                return "US";
            }
            if (ip.AddressFamily == AddressFamily.InterNetworkV6 && b.Length == 16)
            {
                if (b[0] == 0x24) return "SG";
                if (b[0] == 0x2a) return "DE";
                if (b[0] == 0x28) return "BR";
                if (b[0] == 0x2c) return "ZA";
                return "US";
            }
            return "US";
        }

        public static string FormatCountryBadgeText(string countryCode, string ipStr)
        {
            if (string.IsNullOrWhiteSpace(countryCode) && string.IsNullOrWhiteSpace(ipStr))
            {
                return "";
            }
            string cc = string.IsNullOrWhiteSpace(countryCode) ? "UN" : countryCode.Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(ipStr))
            {
                return "[" + cc + "]";
            }
            return string.Format("[{0}] {1}", cc, ipStr);
        }

        public static Bitmap GetCountryFlagBitmap(string countryCode)
        {
            string cc = string.IsNullOrWhiteSpace(countryCode) ? "UN" : countryCode.Trim().ToUpperInvariant();
            lock (FlagBitmapCache)
            {
                Bitmap cached;
                if (FlagBitmapCache.TryGetValue(cc, out cached) && cached != null)
                {
                    return cached;
                }
                Bitmap bmp = new Bitmap(22, 14);
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.FromArgb(30, 41, 59));
                    if (cc == "BD")
                    {
                        g.Clear(Color.FromArgb(0, 106, 78));
                        using (Brush red = new SolidBrush(Color.FromArgb(244, 42, 65)))
                        {
                            g.FillEllipse(red, 6, 3, 8, 8);
                        }
                    }
                    else if (cc == "US")
                    {
                        g.Clear(Color.FromArgb(178, 34, 52));
                        using (Brush white = new SolidBrush(Color.White))
                        {
                            g.FillRectangle(white, 0, 2, 22, 2);
                            g.FillRectangle(white, 0, 6, 22, 2);
                            g.FillRectangle(white, 0, 10, 22, 2);
                        }
                        using (Brush navy = new SolidBrush(Color.FromArgb(60, 59, 110)))
                        {
                            g.FillRectangle(navy, 0, 0, 10, 8);
                        }
                    }
                    else if (cc == "DE")
                    {
                        using (Brush b1 = new SolidBrush(Color.Black)) g.FillRectangle(b1, 0, 0, 22, 5);
                        using (Brush b2 = new SolidBrush(Color.FromArgb(221, 0, 0))) g.FillRectangle(b2, 0, 5, 22, 4);
                        using (Brush b3 = new SolidBrush(Color.FromArgb(255, 206, 0))) g.FillRectangle(b3, 0, 9, 22, 5);
                    }
                    else if (cc == "NL")
                    {
                        using (Brush b1 = new SolidBrush(Color.FromArgb(174, 28, 40))) g.FillRectangle(b1, 0, 0, 22, 5);
                        using (Brush b2 = new SolidBrush(Color.White)) g.FillRectangle(b2, 0, 5, 22, 4);
                        using (Brush b3 = new SolidBrush(Color.FromArgb(33, 70, 139))) g.FillRectangle(b3, 0, 9, 22, 5);
                    }
                    else if (cc == "FR")
                    {
                        using (Brush b1 = new SolidBrush(Color.FromArgb(0, 35, 149))) g.FillRectangle(b1, 0, 0, 7, 14);
                        using (Brush b2 = new SolidBrush(Color.White)) g.FillRectangle(b2, 7, 0, 8, 14);
                        using (Brush b3 = new SolidBrush(Color.FromArgb(237, 41, 57))) g.FillRectangle(b3, 15, 0, 7, 14);
                    }
                    else if (cc == "JP")
                    {
                        g.Clear(Color.White);
                        using (Brush red = new SolidBrush(Color.FromArgb(188, 0, 45)))
                        {
                            g.FillEllipse(red, 7, 3, 8, 8);
                        }
                    }
                    else
                    {
                        using (Font f = new Font("Segoe UI", 6.5f, FontStyle.Bold))
                        using (Brush txt = new SolidBrush(Color.FromArgb(125, 211, 252)))
                        {
                            g.DrawString(cc.Length > 2 ? cc.Substring(0, 2) : cc, f, txt, 2, 1);
                        }
                    }
                    using (Pen border = new Pen(Color.FromArgb(71, 85, 105)))
                    {
                        g.DrawRectangle(border, 0, 0, 21, 13);
                    }
                }
                FlagBitmapCache[cc] = bmp;
                return bmp;
            }
        }
    }

    // =========================================================================
    // 4. CUSTOM DNS / DoH RESOLVER, NETWORK ADAPTER KILL-SWITCH & PAC PROXY
    // =========================================================================
    public class ShihabNetworkAdapterChoice
    {
        public string AdapterId { get; set; }
        public string AdapterName { get; set; }
        public string Description { get; set; }
        public string IpAddressText { get; set; }
        public bool IsUp { get; set; }

        public override string ToString()
        {
            if (string.IsNullOrEmpty(AdapterId))
            {
                return "Any / System Default";
            }
            return string.Format("{0} ({1}) [{2}]",
                AdapterName,
                string.IsNullOrEmpty(IpAddressText) ? "Disconnected" : IpAddressText,
                IsUp ? "UP" : "DOWN");
        }
    }

    public static class ShihabDnsAndNetworkManager
    {
        private class DnsCacheRecord
        {
            public IPAddress Address;
            public DateTime ExpiresUtc;
        }

        private static readonly Dictionary<string, DnsCacheRecord> DohCache = new Dictionary<string, DnsCacheRecord>(StringComparer.OrdinalIgnoreCase);
        private static bool _netwatchRegistered = false;
        public static event Action OnNetworkRestored;

        public static void EnsureNetworkWatcherStarted()
        {
            if (_netwatchRegistered) return;
            _netwatchRegistered = true;
            try
            {
                NetworkChange.NetworkAvailabilityChanged += delegate(object sender, NetworkAvailabilityEventArgs e)
                {
                    if (e != null && e.IsAvailable && OnNetworkRestored != null)
                    {
                        OnNetworkRestored();
                    }
                };
                NetworkChange.NetworkAddressChanged += delegate(object sender, EventArgs e)
                {
                    if (NetworkInterface.GetIsNetworkAvailable() && OnNetworkRestored != null)
                    {
                        OnNetworkRestored();
                    }
                };
            }
            catch
            {
            }
        }

        public static List<ShihabNetworkAdapterChoice> GetAvailableNetworkAdapters()
        {
            List<ShihabNetworkAdapterChoice> list = new List<ShihabNetworkAdapterChoice>();
            list.Add(new ShihabNetworkAdapterChoice
            {
                AdapterId = "",
                AdapterName = "Any / System Default",
                Description = "Route through default Windows routing table",
                IpAddressText = "",
                IsUp = true
            });
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface nic = nics[i];
                    if (nic == null || nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    string ipStr = "";
                    try
                    {
                        IPInterfaceProperties props = nic.GetIPProperties();
                        for (int u = 0; u < props.UnicastAddresses.Count; u++)
                        {
                            IPAddress addr = props.UnicastAddresses[u].Address;
                            if (addr != null && addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                            {
                                ipStr = addr.ToString();
                                break;
                            }
                        }
                    }
                    catch
                    {
                    }
                    list.Add(new ShihabNetworkAdapterChoice
                    {
                        AdapterId = nic.Name,
                        AdapterName = nic.Name,
                        Description = nic.Description,
                        IpAddressText = ipStr,
                        IsUp = nic.OperationalStatus == OperationalStatus.Up && !string.IsNullOrEmpty(ipStr)
                    });
                }
            }
            catch
            {
            }
            return list;
        }

        public static IPAddress GetBoundAdapterLocalIp(string adapterNameOrId)
        {
            if (string.IsNullOrWhiteSpace(adapterNameOrId)) return null;
            MehediAppConfiguration tmp = new MehediAppConfiguration();
            tmp.ShihabBoundNetworkAdapter = adapterNameOrId.Trim();
            IPAddress ip;
            string reason;
            if (TryGetBoundAdapterLocalIp(tmp, out ip, out reason))
            {
                return ip;
            }
            return null;
        }

        public static IPAddress ResolveHostIp(string host, MehediAppConfiguration cfg)
        {
            string cc;
            return ResolveHostIpWithDohAndGeo(host, cfg, out cc);
        }

        public static bool TryGetBoundAdapterLocalIp(MehediAppConfiguration cfg, out IPAddress boundIp, out string failReason)
        {
            boundIp = null;
            failReason = "";
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter))
            {
                return true;
            }
            string wanted = cfg.ShihabBoundNetworkAdapter.Trim();
            if (string.Equals(wanted, "Any", StringComparison.OrdinalIgnoreCase) ||
                wanted.StartsWith("Any ", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface nic = nics[i];
                    if (nic == null) continue;
                    if (string.Equals(nic.Name, wanted, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(nic.Id, wanted, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(nic.Description, wanted, StringComparison.OrdinalIgnoreCase))
                    {
                        if (nic.OperationalStatus != OperationalStatus.Up)
                        {
                            failReason = string.Format("VPN/Adapter Kill-Switch triggered: Bound network adapter '{0}' is currently DOWN.", nic.Name);
                            return false;
                        }
                        IPInterfaceProperties props = nic.GetIPProperties();
                        for (int u = 0; u < props.UnicastAddresses.Count; u++)
                        {
                            IPAddress addr = props.UnicastAddresses[u].Address;
                            if (addr != null && addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                            {
                                boundIp = addr;
                                return true;
                            }
                        }
                        failReason = string.Format("VPN/Adapter Kill-Switch triggered: Bound network adapter '{0}' has no active IPv4 address.", nic.Name);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                failReason = "Adapter lookup failed: " + ex.Message;
                return false;
            }
            failReason = string.Format("VPN/Adapter Kill-Switch triggered: Bound network adapter '{0}' was not found.", wanted);
            return false;
        }

        public static IPAddress ResolveHostIpWithDohAndGeo(
            string host,
            MehediAppConfiguration cfg,
            out string countryCode)
        {
            countryCode = "UN";
            if (string.IsNullOrWhiteSpace(host)) return null;

            IPAddress parsed;
            if (IPAddress.TryParse(host.Trim('[', ']'), out parsed))
            {
                countryCode = ShihabGeoIpLookup.LookupCountryCodeForIp(parsed);
                return parsed;
            }

            string cleanHost = host.Trim().ToLowerInvariant();
            if (cleanHost == "localhost" || cleanHost.EndsWith(".local", StringComparison.OrdinalIgnoreCase) || cleanHost.IndexOf('.') < 0)
            {
                countryCode = "LO";
                return IPAddress.Loopback;
            }

            string cacheKey = cleanHost + "|" + (cfg != null ? (cfg.ShihabDnsMode + ":" + cfg.ShihabDnsProvider + ":" + cfg.ShihabPlainDnsServer) : "sys");
            lock (DohCache)
            {
                DnsCacheRecord rec;
                if (DohCache.TryGetValue(cacheKey, out rec) && rec != null && rec.ExpiresUtc > DateTime.UtcNow)
                {
                    countryCode = ShihabGeoIpLookup.LookupCountryCodeForIp(rec.Address);
                    return rec.Address;
                }
            }

            IPAddress resolvedIp = null;
            string dnsMode = cfg != null ? (cfg.ShihabDnsMode ?? "System").Trim() : "System";
            if (dnsMode.StartsWith("Encrypted", StringComparison.OrdinalIgnoreCase))
            {
                resolvedIp = QueryDnsOverHttpsJson(cleanHost, cfg);
                if (resolvedIp == null && cfg != null && !cfg.ShihabDnsFallbackToSystem)
                {
                    return null;
                }
            }
            else if (string.Equals(dnsMode, "Plain", StringComparison.OrdinalIgnoreCase) &&
                     cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabPlainDnsServer))
            {
                resolvedIp = ShihabLocalNetworkRelay.QueryPlainUdpDnsA(cleanHost, cfg.ShihabPlainDnsServer.Trim());
                if (resolvedIp == null && !cfg.ShihabDnsFallbackToSystem)
                {
                    return null;
                }
            }

            if (resolvedIp == null)
            {
                try
                {
                    IPAddress[] addrs = Dns.GetHostAddresses(cleanHost);
                    if (addrs != null && addrs.Length > 0)
                    {
                        for (int i = 0; i < addrs.Length; i++)
                        {
                            if (addrs[i].AddressFamily == AddressFamily.InterNetwork)
                            {
                                resolvedIp = addrs[i];
                                break;
                            }
                        }
                        if (resolvedIp == null) resolvedIp = addrs[0];
                    }
                }
                catch
                {
                }
            }

            if (resolvedIp != null)
            {
                lock (DohCache)
                {
                    DohCache[cacheKey] = new DnsCacheRecord
                    {
                        Address = resolvedIp,
                        ExpiresUtc = DateTime.UtcNow.AddMinutes(5)
                    };
                }
                countryCode = ShihabGeoIpLookup.LookupCountryCodeForIp(resolvedIp);
            }
            return resolvedIp;
        }

        private static IPAddress QueryDnsOverHttpsJson(string host, MehediAppConfiguration cfg)
        {
            try
            {
                ShihabAutoUpdater.EnforceModernTls();
                string provider = (cfg != null && !string.IsNullOrEmpty(cfg.ShihabDnsProvider)) ? cfg.ShihabDnsProvider : "Cloudflare";
                string endpoint = "https://1.1.1.1/dns-query";
                if (string.Equals(provider, "Google", StringComparison.OrdinalIgnoreCase))
                    endpoint = "https://8.8.8.8/resolve";
                else if (string.Equals(provider, "Quad9", StringComparison.OrdinalIgnoreCase))
                    endpoint = "https://9.9.9.9:5053/dns-query";
                else if (string.Equals(provider, "AdGuard", StringComparison.OrdinalIgnoreCase))
                    endpoint = "https://94.140.14.14/resolve";
                else if (string.Equals(provider, "NextDNS", StringComparison.OrdinalIgnoreCase))
                {
                    string nextId = cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabNextDnsConfigId) ? cfg.ShihabNextDnsConfigId.Trim() : "";
                    endpoint = string.IsNullOrEmpty(nextId) ? "https://dns.nextdns.io/dns-query" : ("https://dns.nextdns.io/" + Uri.EscapeDataString(nextId));
                }
                else if (string.Equals(provider, "Custom", StringComparison.OrdinalIgnoreCase) && cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabCustomDohUrl))
                    endpoint = cfg.ShihabCustomDohUrl.Trim();

                string url = endpoint + (endpoint.IndexOf('?') >= 0 ? "&" : "?") + "name=" + Uri.EscapeDataString(host) + "&type=A";
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.Accept = "application/dns-json";
                req.UserAgent = "MHS-DoH-Resolver/1.1";
                req.Proxy = null;
                req.Timeout = 4500;
                req.ReadWriteTimeout = 4500;

                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream s = resp.GetResponseStream())
                using (StreamReader sr = new StreamReader(s, Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    MatchCollection matches = Regex.Matches(json, "\"data\"\\s*:\\s*\"([0-9]{1,3}(?:\\.[0-9]{1,3}){3})\"");
                    for (int i = 0; i < matches.Count; i++)
                    {
                        IPAddress ip;
                        if (IPAddress.TryParse(matches[i].Groups[1].Value, out ip))
                        {
                            return ip;
                        }
                    }
                }
            }
            catch
            {
            }
            return null;
        }

        private static bool IsLoopbackTargetUrl(string targetUrl)
        {
            Uri u;
            if (!Uri.TryCreate(targetUrl, UriKind.Absolute, out u)) return false;
            if (u.IsLoopback) return true;
            string h = (u.Host ?? "").Trim('[', ']').ToLowerInvariant();
            return h == "localhost" || h == "127.0.0.1" || h == "::1";
        }

        public static void ApplyNetworkBindingAndProxy(HttpWebRequest req, MehediAppConfiguration cfg, string targetUrl)
        {
            ApplyAdapterAndProxyPolicy(req, targetUrl, cfg);
        }

        public static void ApplyAdapterAndProxyPolicy(HttpWebRequest req, string targetUrl, MehediAppConfiguration cfg)
        {
            if (req == null) return;
            ShihabAutoUpdater.EnforceModernTls();
            if (cfg == null) return;

            // 1. Network Adapter / VPN Kill-Switch Binding
            if (!string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter) &&
                !cfg.ShihabBoundNetworkAdapter.StartsWith("Any", StringComparison.OrdinalIgnoreCase))
            {
                IPAddress boundLocalIp;
                string failReason;
                if (!TryGetBoundAdapterLocalIp(cfg, out boundLocalIp, out failReason) || boundLocalIp == null)
                {
                    throw new WebException(string.IsNullOrEmpty(failReason)
                        ? "Bound network adapter is offline (VPN Kill-Switch)."
                        : failReason);
                }
                if (!IsLoopbackTargetUrl(targetUrl))
                {
                    req.ServicePoint.BindIPEndPointDelegate = delegate(ServicePoint sp, IPEndPoint remoteEndPoint, int retryCount)
                    {
                        return new IPEndPoint(boundLocalIp, 0);
                    };
                }
            }

            if (IsLoopbackTargetUrl(targetUrl))
            {
                req.Proxy = null;
                return;
            }

            // 2. Route through authenticated loopback relay when SOCKS5, PAC, Custom DNS/DoH, or Adapter Binding is active
            if (ShihabLocalNetworkRelay.NeedsLocalRelay(cfg))
            {
                IWebProxy relayProxy = ShihabLocalNetworkRelay.GetOrStartConfiguredWebProxy(cfg);
                if (relayProxy != null)
                {
                    req.Proxy = relayProxy;
                    return;
                }
            }

            string pMode = (cfg.ShihabProxyMode ?? "").Trim();
            if (string.Equals(pMode, "None", StringComparison.OrdinalIgnoreCase) && !cfg.MehediUseHttpProxy)
            {
                req.Proxy = null;
                return;
            }

            if ((string.Equals(pMode, "Manual", StringComparison.OrdinalIgnoreCase) || cfg.MehediUseHttpProxy) &&
                !string.IsNullOrWhiteSpace(cfg.ShihabProxyAddress))
            {
                WebProxy wp = new WebProxy(cfg.ShihabProxyAddress.Trim(), cfg.MehediProxyPort > 0 ? cfg.MehediProxyPort : 8080);
                if (!string.IsNullOrWhiteSpace(cfg.ShihabProxyUser))
                {
                    wp.Credentials = new NetworkCredential(
                        cfg.ShihabProxyUser.Trim(),
                        ShihabSecretsVault.UnprotectSecret(cfg.MehediProxyPassword ?? ""));
                }
                req.Proxy = wp;
            }
        }
    }

    // =========================================================================
    // 5. HARDENED POST-DOWNLOAD ARCHIVE EXTRACTOR (.zip, .tar, .tar.gz, .7z, .rar)
    // =========================================================================
    public static class ShihabSafeArchiveExtractor
    {
        private const long MaxDecompressedBytes = 20L * 1024L * 1024L * 1024L; // 20 GiB cap
        private const long RatioGraceBytes = 10L * 1024L * 1024L; // 10 MiB grace
        private const long MaxCompressionRatio = 200L; // 200:1 bomb guard
        private const int MaxExtractedEntries = 100000;
        private const int MaxDirectoryDepth = 64;

        private static readonly HashSet<string> WindowsReservedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        public static bool IsSupportedArchiveFile(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)) return false;
            string lower = fileName.Trim().ToLowerInvariant();
            return lower.EndsWith(".zip") ||
                   lower.EndsWith(".tar") ||
                   lower.EndsWith(".tar.gz") ||
                   lower.EndsWith(".tgz") ||
                   lower.EndsWith(".gz") ||
                   lower.EndsWith(".7z") ||
                   lower.EndsWith(".rar") ||
                   lower.EndsWith(".tar.xz") ||
                   lower.EndsWith(".txz") ||
                   Regex.IsMatch(lower, @"\.(7z|zip)\.001$");
        }

        public static bool AreAllSplitArchiveVolumesReady(string archiveFilePath, out string waitingReason)
        {
            waitingReason = "";
            if (string.IsNullOrWhiteSpace(archiveFilePath) || !File.Exists(archiveFilePath))
            {
                waitingReason = "Archive file missing.";
                return false;
            }
            string dir = Path.GetDirectoryName(Path.GetFullPath(archiveFilePath));
            string name = Path.GetFileName(archiveFilePath);

            // Detect non-first split volumes (.part2.rar, .7z.002, .r00, etc.) -> skip auto-extracting secondary volumes directly
            Match rarPartMatch = Regex.Match(name, @"^(.+)\.part0*([0-9]+)\.rar$", RegexOptions.IgnoreCase);
            if (rarPartMatch.Success)
            {
                int partNum = 1;
                int.TryParse(rarPartMatch.Groups[2].Value, out partNum);
                if (partNum > 1)
                {
                    waitingReason = "Secondary split RAR volume (extracted via part 1).";
                    return false;
                }
                string prefix = rarPartMatch.Groups[1].Value;
                string[] inProg = Directory.GetFiles(dir, prefix + ".part*.rar.*part*");
                if (inProg.Length > 0)
                {
                    waitingReason = "Waiting for remaining split RAR volumes to finish downloading.";
                    return false;
                }
            }

            Match numSplitMatch = Regex.Match(name, @"^(.+\.(?:7z|zip))\.([0-9]{3})$", RegexOptions.IgnoreCase);
            if (numSplitMatch.Success)
            {
                if (numSplitMatch.Groups[2].Value != "001")
                {
                    waitingReason = "Secondary split volume (extracted via .001).";
                    return false;
                }
                string basePref = numSplitMatch.Groups[1].Value;
                string[] inProg = Directory.GetFiles(dir, basePref + ".*part*");
                if (inProg.Length > 0)
                {
                    waitingReason = "Waiting for remaining split volumes to finish downloading.";
                    return false;
                }
            }

            if (Regex.IsMatch(name, @"\.r[0-9]{2}$", RegexOptions.IgnoreCase))
            {
                waitingReason = "Secondary old-style RAR volume (extracted via .rar).";
                return false;
            }

            return true;
        }

        public static bool ExtractArchiveSafely(
            string archiveFilePath,
            string targetDirectoryOrMode,
            bool createSubfolder,
            string password,
            out string finalDestinationPath,
            out string errorMessage)
        {
            string mode = createSubfolder ? "Always" : "Never";
            if (string.Equals(targetDirectoryOrMode, "Smart", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetDirectoryOrMode, "Always", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(targetDirectoryOrMode, "Never", StringComparison.OrdinalIgnoreCase))
            {
                mode = targetDirectoryOrMode;
            }
            return ExtractArchiveSafely(
                archiveFilePath,
                mode,
                password,
                false,
                "",
                "",
                out finalDestinationPath,
                out errorMessage);
        }

        public static bool ExtractArchiveSafely(
            string archiveFilePath,
            string subfolderMode,
            string password,
            bool deleteArchiveOnSuccess,
            out string finalDestinationPath,
            out string errorMessage)
        {
            return ExtractArchiveSafely(
                archiveFilePath,
                subfolderMode,
                password,
                deleteArchiveOnSuccess,
                "",
                "",
                out finalDestinationPath,
                out errorMessage);
        }

        public static bool ExtractArchiveSafely(
            string archiveFilePath,
            string subfolderMode,
            string password,
            bool deleteArchiveOnSuccess,
            string motwHostUrl,
            string motwRefererUrl,
            out string finalDestinationPath,
            out string errorMessage)
        {
            finalDestinationPath = "";
            errorMessage = "";
            if (string.IsNullOrWhiteSpace(archiveFilePath) || !File.Exists(archiveFilePath))
            {
                errorMessage = "Archive file does not exist.";
                return false;
            }

            string splitWait;
            if (!AreAllSplitArchiveVolumesReady(archiveFilePath, out splitWait))
            {
                errorMessage = splitWait;
                return false;
            }

            string parentDir = Path.GetDirectoryName(Path.GetFullPath(archiveFilePath));
            string fileName = Path.GetFileName(archiveFilePath);
            string stem = GetArchiveStem(fileName);
            string stagingDir = Path.Combine(parentDir, ".mdm-extract-" + Guid.NewGuid().ToString("N").Substring(0, 8));

            long availDisk = ShihabOsAndDiskSafety.GetAvailableDiskBytes(parentDir);
            long diskBudgetCap = Math.Min(MaxDecompressedBytes, Math.Max(64L * 1024L * 1024L, availDisk - ShihabOsAndDiskSafety.DiskSafetyMarginBytes));

            try
            {
                Directory.CreateDirectory(stagingDir);
                long compressedBytes = new FileInfo(archiveFilePath).Length;
                string format = DetectArchiveFormat(archiveFilePath, fileName);

                if (format == "zip")
                {
                    try
                    {
                        ExtractZipToStaging(archiveFilePath, stagingDir, compressedBytes, diskBudgetCap);
                    }
                    catch (InvalidDataException)
                    {
                        // Encrypted / Deflate64 / Zip64 edge case -> try 7z.exe fallback
                        TryDeleteDirectory(stagingDir);
                        Directory.CreateDirectory(stagingDir);
                        if (!ExtractWithSevenZip(archiveFilePath, stagingDir, password, compressedBytes, diskBudgetCap, out errorMessage))
                        {
                            TryDeleteDirectory(stagingDir);
                            return false;
                        }
                    }
                }
                else if (format == "targz")
                {
                    using (FileStream fs = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
                    {
                        ExtractTarStreamToStaging(gz, stagingDir, compressedBytes, diskBudgetCap);
                    }
                }
                else if (format == "tar")
                {
                    using (FileStream fs = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        ExtractTarStreamToStaging(fs, stagingDir, compressedBytes, diskBudgetCap);
                    }
                }
                else if (format == "gz")
                {
                    string outLeaf = Path.GetFileNameWithoutExtension(fileName);
                    if (string.IsNullOrEmpty(outLeaf)) outLeaf = stem + ".extracted";
                    string safePath = ValidateSafeRelativeEntryPath(stagingDir, outLeaf);
                    using (FileStream fs = new FileStream(archiveFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (GZipStream gz = new GZipStream(fs, CompressionMode.Decompress))
                    using (FileStream outFs = new FileStream(safePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        CopyBoundedStream(gz, outFs, compressedBytes, 0, diskBudgetCap);
                    }
                }
                else
                {
                    if (!ExtractWithSevenZip(archiveFilePath, stagingDir, password, compressedBytes, diskBudgetCap, out errorMessage))
                    {
                        TryDeleteDirectory(stagingDir);
                        return false;
                    }
                }

                // Propagate Mark-of-the-Web to extracted files before moving into destination
                if (!string.IsNullOrWhiteSpace(motwHostUrl) || !string.IsNullOrWhiteSpace(motwRefererUrl))
                {
                    ShihabOsAndDiskSafety.PropagateMarkOfTheWebToDirectory(stagingDir, motwHostUrl, motwRefererUrl);
                }

                // Finalize subfolder placement ("Smart", "Always", "Never")
                string mode = string.IsNullOrWhiteSpace(subfolderMode) ? "Smart" : subfolderMode.Trim();
                string[] topDirs = Directory.GetDirectories(stagingDir);
                string[] topFiles = Directory.GetFiles(stagingDir);
                int topCount = topDirs.Length + topFiles.Length;

                if (string.Equals(mode, "Always", StringComparison.OrdinalIgnoreCase) ||
                    (string.Equals(mode, "Smart", StringComparison.OrdinalIgnoreCase) && topCount > 1))
                {
                    string targetSubfolder = GetUniqueDirectoryPath(parentDir, stem);
                    Directory.Move(stagingDir, targetSubfolder);
                    finalDestinationPath = targetSubfolder;
                }
                else if (topCount == 1 && !string.Equals(mode, "Never", StringComparison.OrdinalIgnoreCase))
                {
                    if (topDirs.Length == 1)
                    {
                        string dirLeaf = Path.GetFileName(topDirs[0]);
                        string destDir = GetUniqueDirectoryPath(parentDir, dirLeaf);
                        Directory.Move(topDirs[0], destDir);
                        finalDestinationPath = destDir;
                    }
                    else
                    {
                        string fileLeaf = Path.GetFileName(topFiles[0]);
                        string uniqueLeaf = ShihabFormatHelpers.EnsureShihabUniqueFilePath(parentDir, fileLeaf);
                        string destFile = Path.Combine(parentDir, uniqueLeaf);
                        File.Move(topFiles[0], destFile);
                        finalDestinationPath = destFile;
                    }
                    TryDeleteDirectory(stagingDir);
                }
                else
                {
                    for (int i = 0; i < topDirs.Length; i++)
                    {
                        string destDir = GetUniqueDirectoryPath(parentDir, Path.GetFileName(topDirs[i]));
                        Directory.Move(topDirs[i], destDir);
                    }
                    for (int i = 0; i < topFiles.Length; i++)
                    {
                        string uniqueLeaf = ShihabFormatHelpers.EnsureShihabUniqueFilePath(parentDir, Path.GetFileName(topFiles[i]));
                        File.Move(topFiles[i], Path.Combine(parentDir, uniqueLeaf));
                    }
                    finalDestinationPath = parentDir;
                    TryDeleteDirectory(stagingDir);
                }

                if (deleteArchiveOnSuccess)
                {
                    SendFileToRecycleBinSafe(archiveFilePath);
                }
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Archive extraction failed: " + ex.Message;
                TryDeleteDirectory(stagingDir);
                return false;
            }
        }

        public static void SendFileToRecycleBinSafe(string filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;
            try
            {
                FileSystem.DeleteFile(filePath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin);
                return;
            }
            catch { }
            try { File.Delete(filePath); } catch { }
        }

        private static string DetectArchiveFormat(string filePath, string fileName)
        {
            string lower = (fileName ?? "").ToLowerInvariant();
            try
            {
                byte[] magic = new byte[264];
                int read;
                using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    read = fs.Read(magic, 0, magic.Length);
                }
                if (read >= 4 && magic[0] == 0x50 && magic[1] == 0x4B && (magic[2] == 0x03 || magic[2] == 0x05 || magic[2] == 0x07))
                {
                    return "zip";
                }
                if (read >= 6 && magic[0] == 0x37 && magic[1] == 0x7A && magic[2] == 0xBC && magic[3] == 0xAF && magic[4] == 0x27 && magic[5] == 0x1C)
                {
                    return "7z";
                }
                if (read >= 6 && magic[0] == 0x52 && magic[1] == 0x61 && magic[2] == 0x72 && magic[3] == 0x21 && magic[4] == 0x1A && magic[5] == 0x07)
                {
                    return "rar";
                }
                if (read >= 262 && magic[257] == (byte)'u' && magic[258] == (byte)'s' && magic[259] == (byte)'t' && magic[260] == (byte)'a' && magic[261] == (byte)'r')
                {
                    return "tar";
                }
                if (read >= 2 && magic[0] == 0x1F && magic[1] == 0x8B)
                {
                    return (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz")) ? "targz" : "gz";
                }
            }
            catch { }

            if (lower.EndsWith(".zip")) return "zip";
            if (lower.EndsWith(".tar.gz") || lower.EndsWith(".tgz")) return "targz";
            if (lower.EndsWith(".tar")) return "tar";
            if (lower.EndsWith(".gz")) return "gz";
            return "7z";
        }

        private static void ExtractZipToStaging(string zipPath, string stagingDir, long compressedSize, long diskBudgetCap)
        {
            long totalExtracted = 0;
            int entriesSeen = 0;
            using (ZipArchive archive = ZipFile.OpenRead(zipPath))
            {
                foreach (ZipArchiveEntry entry in archive.Entries)
                {
                    if (string.IsNullOrEmpty(entry.FullName)) continue;
                    entriesSeen++;
                    if (entriesSeen > MaxExtractedEntries)
                    {
                        throw new InvalidOperationException("Archive exceeds maximum entry count limit (100,000).");
                    }
                    // Reject Unix symlink mode bits in ExternalAttributes (0120000 = 0xA000 << 16)
                    uint unixMode = ((uint)entry.ExternalAttributes >> 16) & 0xF000u;
                    if (unixMode == 0xA000u)
                    {
                        throw new InvalidOperationException("Blocked symbolic link entry in ZIP archive: " + entry.FullName);
                    }
                    string safePath = ValidateSafeRelativeEntryPath(stagingDir, entry.FullName);
                    if (entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\"))
                    {
                        Directory.CreateDirectory(safePath);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(safePath));
                    using (Stream inStream = entry.Open())
                    using (FileStream outFs = new FileStream(safePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        totalExtracted = CopyBoundedStream(inStream, outFs, compressedSize, totalExtracted, diskBudgetCap);
                    }
                }
            }
        }

        private static void ExtractTarStreamToStaging(Stream tarStream, string stagingDir, long compressedSize, long diskBudgetCap)
        {
            byte[] header = new byte[512];
            long totalExtracted = 0;
            int entriesSeen = 0;
            string pendingPaxOrGnuPath = "";

            while (true)
            {
                int read = ReadExactBytes(tarStream, header, 512);
                if (read == 0) break;
                bool allZero = true;
                for (int i = 0; i < 512; i++)
                {
                    if (header[i] != 0) { allZero = false; break; }
                }
                if (allZero) break;

                entriesSeen++;
                if (entriesSeen > MaxExtractedEntries)
                {
                    throw new InvalidOperationException("Archive exceeds maximum entry count limit (100,000).");
                }

                string name = ReadAsciiNullTerminated(header, 0, 100);
                string prefix = ReadAsciiNullTerminated(header, 345, 155);
                if (!string.IsNullOrEmpty(prefix))
                {
                    name = prefix + "/" + name;
                }
                if (!string.IsNullOrEmpty(pendingPaxOrGnuPath))
                {
                    name = pendingPaxOrGnuPath;
                    pendingPaxOrGnuPath = "";
                }

                string sizeOctal = ReadAsciiNullTerminated(header, 124, 12).Trim();
                long entrySize = 0;
                if (!string.IsNullOrEmpty(sizeOctal))
                {
                    try { entrySize = Convert.ToInt64(sizeOctal, 8); } catch { entrySize = 0; }
                }
                byte typeFlag = header[156];
                if (typeFlag == (byte)'2' || typeFlag == (byte)'1')
                {
                    throw new InvalidOperationException("Archive contains unsafe symbolic or hard links.");
                }
                if (typeFlag == (byte)'L' && entrySize > 0 && entrySize < 8192)
                {
                    byte[] longNameBuf = new byte[(int)entrySize];
                    ReadExactBytes(tarStream, longNameBuf, (int)entrySize);
                    pendingPaxOrGnuPath = Encoding.UTF8.GetString(longNameBuf).TrimEnd('\0', '\r', '\n');
                    long padL = (512 - (entrySize % 512)) % 512;
                    if (padL > 0) SkipStreamBytes(tarStream, padL);
                    continue;
                }

                string safePath = ValidateSafeRelativeEntryPath(stagingDir, name);
                if (typeFlag == (byte)'5' || name.EndsWith("/"))
                {
                    Directory.CreateDirectory(safePath);
                }
                else if (typeFlag == 0 || typeFlag == (byte)'0')
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(safePath));
                    using (FileStream outFs = new FileStream(safePath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        long remaining = entrySize;
                        byte[] buf = new byte[32768];
                        while (remaining > 0)
                        {
                            int toRead = (int)Math.Min((long)buf.Length, remaining);
                            int got = tarStream.Read(buf, 0, toRead);
                            if (got <= 0) break;
                            totalExtracted += got;
                            CheckDecompressionBombGuard(compressedSize, totalExtracted, diskBudgetCap);
                            outFs.Write(buf, 0, got);
                            remaining -= got;
                        }
                    }
                }
                else
                {
                    SkipStreamBytes(tarStream, entrySize);
                }

                long pad = (512 - (entrySize % 512)) % 512;
                if (pad > 0)
                {
                    SkipStreamBytes(tarStream, pad);
                }
            }
        }

        private static bool ExtractWithSevenZip(
            string archivePath,
            string stagingDir,
            string password,
            long compressedSize,
            long diskBudgetCap,
            out string errorMsg)
        {
            errorMsg = "";
            string sevenZipExe = FindSevenZipExecutable();
            if (string.IsNullOrEmpty(sevenZipExe))
            {
                errorMsg = "7-Zip (7z.exe) is required to extract .7z/.rar/encrypted archives. Install 7-Zip and retry.";
                return false;
            }
            StringBuilder args = new StringBuilder();
            args.Append("x -y -snl- ");
            if (!string.IsNullOrEmpty(password))
            {
                args.Append("\"-p").Append(password.Replace("\"", "\\\"")).Append("\" ");
            }
            else
            {
                args.Append("-p\"\" ");
            }
            args.Append("\"-o").Append(stagingDir).Append("\" -- \"").Append(archivePath).Append("\"");

            string stdoutAll;
            string stderrAll;
            int exitCode = ShihabWindowsJobRunner.RunManagedSubprocess(
                sevenZipExe,
                args.ToString(),
                stagingDir,
                600000,
                null,
                CancellationToken.None,
                out stdoutAll,
                out stderrAll);

            if (exitCode != 0)
            {
                errorMsg = !string.IsNullOrWhiteSpace(stderrAll)
                    ? stderrAll.Trim()
                    : string.Format("7z.exe exited with code {0}.", exitCode);
                return false;
            }

            AuditStagingDirectorySafety(stagingDir, compressedSize, diskBudgetCap);
            return true;
        }

        private static void AuditStagingDirectorySafety(string stagingDir, long compressedSize, long diskBudgetCap)
        {
            string fullRoot = Path.GetFullPath(stagingDir).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
            int entries = 0;
            long totalBytes = 0;
            Stack<DirectoryInfo> stack = new Stack<DirectoryInfo>();
            stack.Push(new DirectoryInfo(stagingDir));

            while (stack.Count > 0)
            {
                DirectoryInfo cur = stack.Pop();
                if ((cur.Attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new InvalidOperationException("Blocked symbolic link / junction extracted in staging folder: " + cur.Name);
                }

                string relDir = cur.FullName.Length >= fullRoot.Length ? cur.FullName.Substring(fullRoot.Length) : "";
                if (!string.IsNullOrEmpty(relDir))
                {
                    int depth = relDir.Split(Path.DirectorySeparatorChar).Length;
                    if (depth > MaxDirectoryDepth)
                    {
                        throw new InvalidOperationException("Extracted directory exceeds maximum nesting depth (64).");
                    }
                    EnsureNotReservedWindowsName(cur.Name);
                }

                FileInfo[] files = cur.GetFiles();
                for (int i = 0; i < files.Length; i++)
                {
                    entries++;
                    if (entries > MaxExtractedEntries)
                    {
                        throw new InvalidOperationException("Extracted archive exceeds 100,000 file limit.");
                    }
                    if ((files[i].Attributes & FileAttributes.ReparsePoint) != 0)
                    {
                        throw new InvalidOperationException("Blocked symbolic link file in archive: " + files[i].Name);
                    }
                    EnsureNotReservedWindowsName(files[i].Name);
                    totalBytes += files[i].Length;
                    CheckDecompressionBombGuard(compressedSize, totalBytes, diskBudgetCap);
                }

                DirectoryInfo[] subDirs = cur.GetDirectories();
                for (int i = 0; i < subDirs.Length; i++)
                {
                    entries++;
                    if (entries > MaxExtractedEntries)
                    {
                        throw new InvalidOperationException("Extracted archive exceeds 100,000 entry limit.");
                    }
                    stack.Push(subDirs[i]);
                }
            }
        }

        public static string FindSevenZipExecutable()
        {
            string[] candidates = new string[]
            {
                @"C:\Program Files\7-Zip\7z.exe",
                @"C:\Program Files (x86)\7-Zip\7z.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\7-Zip\7z.exe")
            };
            for (int i = 0; i < candidates.Length; i++)
            {
                if (File.Exists(candidates[i])) return candidates[i];
            }
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] dirs = pathEnv.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    string cand = Path.Combine(dirs[i].Trim(), "7z.exe");
                    if (File.Exists(cand)) return cand;
                }
                catch { }
            }
            return "";
        }

        private static void EnsureNotReservedWindowsName(string component)
        {
            if (string.IsNullOrEmpty(component)) return;
            string trimmed = component.TrimEnd(' ', '.');
            int dot = trimmed.IndexOf('.');
            string stem = dot >= 0 ? trimmed.Substring(0, dot) : trimmed;
            if (WindowsReservedDeviceNames.Contains(stem))
            {
                throw new InvalidOperationException("Blocked Windows reserved device filename in archive: " + component);
            }
        }

        private static string ValidateSafeRelativeEntryPath(string stagingRoot, string rawEntryName)
        {
            if (string.IsNullOrWhiteSpace(rawEntryName))
            {
                throw new InvalidOperationException("Archive entry has an empty path.");
            }
            if (rawEntryName.IndexOf(':') >= 0 || rawEntryName.IndexOf('\0') >= 0)
            {
                throw new InvalidOperationException("Blocked archive entry containing colon / NUL / ADS: " + rawEntryName);
            }
            string normalized = rawEntryName.Replace('/', Path.DirectorySeparatorChar).TrimStart(Path.DirectorySeparatorChar);
            string[] parts = normalized.Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length > MaxDirectoryDepth)
            {
                throw new InvalidOperationException("Archive entry exceeds maximum directory depth (64): " + rawEntryName);
            }
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "..")
                {
                    throw new InvalidOperationException("Blocked Zip-Slip path traversal in archive entry: " + rawEntryName);
                }
                EnsureNotReservedWindowsName(parts[i]);
            }
            string fullStaging = Path.GetFullPath(stagingRoot);
            if (!fullStaging.EndsWith(Path.DirectorySeparatorChar.ToString()))
            {
                fullStaging += Path.DirectorySeparatorChar;
            }
            string combined = Path.GetFullPath(Path.Combine(fullStaging, normalized));
            if (!combined.StartsWith(fullStaging, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Blocked path escape outside staging directory: " + rawEntryName);
            }
            return combined;
        }

        private static long CopyBoundedStream(Stream input, Stream output, long compressedSize, long currentTotal, long diskBudgetCap)
        {
            byte[] buf = new byte[32768];
            int read;
            while ((read = input.Read(buf, 0, buf.Length)) > 0)
            {
                currentTotal += read;
                CheckDecompressionBombGuard(compressedSize, currentTotal, diskBudgetCap);
                output.Write(buf, 0, read);
            }
            return currentTotal;
        }

        private static void CheckDecompressionBombGuard(long compressedSize, long totalExtracted, long diskBudgetCap)
        {
            long limit = Math.Min(MaxDecompressedBytes, diskBudgetCap);
            if (totalExtracted > limit)
            {
                throw new InvalidOperationException("Aborted extraction: archive exceeds disk or 20 GB safety limit.");
            }
            if (totalExtracted > RatioGraceBytes && compressedSize > 0)
            {
                long ratio = totalExtracted / Math.Max(1L, compressedSize);
                if (ratio > MaxCompressionRatio)
                {
                    throw new InvalidOperationException(string.Format("Aborted extraction: suspected decompression bomb ({0}:1 ratio).", ratio));
                }
            }
        }

        private static string GetArchiveStem(string fileName)
        {
            string lower = fileName.ToLowerInvariant();
            if (lower.EndsWith(".tar.gz") || lower.EndsWith(".tar.xz")) return fileName.Substring(0, fileName.Length - 7);
            Match m = Regex.Match(fileName, @"^(.+?)\.part0*1\.rar$", RegexOptions.IgnoreCase);
            if (m.Success) return m.Groups[1].Value;
            Match m2 = Regex.Match(fileName, @"^(.+?)\.(?:7z|zip)\.001$", RegexOptions.IgnoreCase);
            if (m2.Success) return m2.Groups[1].Value;
            return Path.GetFileNameWithoutExtension(fileName);
        }

        private static string GetUniqueDirectoryPath(string parentDir, string folderName)
        {
            string safe = ShihabFormatHelpers.SanitizeShihabFileName(folderName);
            string full = Path.Combine(parentDir, safe);
            if (!Directory.Exists(full) && !File.Exists(full)) return full;
            for (int i = 2; i < 1000; i++)
            {
                string cand = Path.Combine(parentDir, string.Format("{0}_{1}", safe, i));
                if (!Directory.Exists(cand) && !File.Exists(cand)) return cand;
            }
            return Path.Combine(parentDir, safe + "_" + Guid.NewGuid().ToString("N").Substring(0, 5));
        }

        private static int ReadExactBytes(Stream s, byte[] buf, int count)
        {
            int offset = 0;
            while (offset < count)
            {
                int r = s.Read(buf, offset, count - offset);
                if (r <= 0) break;
                offset += r;
            }
            return offset;
        }

        private static void SkipStreamBytes(Stream s, long count)
        {
            byte[] buf = new byte[4096];
            long rem = count;
            while (rem > 0)
            {
                int r = s.Read(buf, 0, (int)Math.Min((long)buf.Length, rem));
                if (r <= 0) break;
                rem -= r;
            }
        }

        private static string ReadAsciiNullTerminated(byte[] buf, int offset, int maxLen)
        {
            int len = 0;
            while (len < maxLen && buf[offset + len] != 0) len++;
            return Encoding.UTF8.GetString(buf, offset, len);
        }

        private static void TryDeleteDirectory(string dir)
        {
            try
            {
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
            }
            catch
            {
            }
        }
    }

    // =========================================================================
    // 6. MULTI-HOST DEBRID INTEGRATION & GOOGLE DRIVE / DOCS DIRECT RESOLVER
    // =========================================================================
    public static class ShihabDebridManager
    {
        private static readonly string[] SupportedPremiumHostKeywords = new string[]
        {
            "rapidgator.net", "rg.to", "uploaded.net", "ul.to", "nitroflare.com",
            "turbobit.net", "1fichier.com", "mega.nz", "mediafire.com", "katfile.com",
            "ddownload.com", "filerice.com", "filefactory.com", "uptobox.com",
            "keep2share.cc", "k2s.cc", "alfafile.net", "hitfile.net", "sendspace.com",
            "clicknupload.click", "drop.download", "hexupload.net", "usersdrive.com"
        };

        private static readonly object _dynamicHostCacheLock = new object();
        private static HashSet<string> _dynamicDebridDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private static DateTime _dynamicDomainsExpiresUtc = DateTime.MinValue;

        public static bool IsPotentialDebridHost(string url, MehediAppConfiguration cfg = null)
        {
            Uri u;
            if (!Uri.TryCreate(url, UriKind.Absolute, out u)) return false;
            string host = u.Host.ToLowerInvariant();
            for (int i = 0; i < SupportedPremiumHostKeywords.Length; i++)
            {
                if (host == SupportedPremiumHostKeywords[i] ||
                    host.EndsWith("." + SupportedPremiumHostKeywords[i], StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            if (cfg != null && cfg.ShihabDebridAccounts != null && cfg.ShihabDebridAccounts.Count > 0)
            {
                EnsureDynamicDomainsCached(cfg);
                lock (_dynamicHostCacheLock)
                {
                    if (_dynamicDebridDomains.Contains(host)) return true;
                    int firstDot = host.IndexOf('.');
                    if (firstDot > 0 && _dynamicDebridDomains.Contains(host.Substring(firstDot + 1))) return true;
                }
            }
            return false;
        }

        private static void EnsureDynamicDomainsCached(MehediAppConfiguration cfg)
        {
            lock (_dynamicHostCacheLock)
            {
                if (_dynamicDomainsExpiresUtc > DateTime.UtcNow) return;
                _dynamicDomainsExpiresUtc = DateTime.UtcNow.AddHours(6);
            }

            try
            {
                bool hasEnabled = false;
                for (int i = 0; i < cfg.ShihabDebridAccounts.Count; i++)
                {
                    if (cfg.ShihabDebridAccounts[i] != null && cfg.ShihabDebridAccounts[i].MehediEnabled)
                    {
                        hasEnabled = true;
                        break;
                    }
                }
                if (!hasEnabled) return;

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.real-debrid.com/rest/1.0/hosts/domains");
                req.Method = "GET";
                req.Timeout = 5000;
                ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, req.RequestUri.AbsoluteUri);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    MatchCollection matches = Regex.Matches(json, "\"([a-z0-9\\-]+\\.[a-z0-9\\.\\-]+)\"", RegexOptions.IgnoreCase);
                    lock (_dynamicHostCacheLock)
                    {
                        for (int i = 0; i < matches.Count; i++)
                        {
                            _dynamicDebridDomains.Add(matches[i].Groups[1].Value.ToLowerInvariant());
                        }
                    }
                }
            }
            catch { }
        }

        public static bool TryResolveGoogleDriveOrDocsUrl(
            string inputUrl,
            MehediAppConfiguration cfg,
            out string directUrl,
            out string suggestedFileName)
        {
            directUrl = inputUrl;
            suggestedFileName = "";
            Uri u;
            if (!Uri.TryCreate(inputUrl, UriKind.Absolute, out u)) return false;
            string host = u.Host.ToLowerInvariant();

            if (host == "docs.google.com")
            {
                Match docM = Regex.Match(u.AbsolutePath, @"/(document|spreadsheets|presentation)/d/([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
                if (docM.Success)
                {
                    string kind = docM.Groups[1].Value.ToLowerInvariant();
                    string id = docM.Groups[2].Value;
                    string fmt = kind == "spreadsheets" ? "xlsx" : (kind == "presentation" ? "pptx" : "docx");
                    directUrl = string.Format("https://docs.google.com/{0}/d/{1}/export?format={2}", kind, id, fmt);
                    suggestedFileName = "google_" + kind + "_" + id.Substring(0, Math.Min(8, id.Length)) + "." + fmt;
                    return true;
                }
            }

            if (host == "drive.google.com" || host == "drive.usercontent.google.com")
            {
                string fileId = "";
                Match mPath = Regex.Match(u.AbsolutePath, @"/file/d/([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
                if (mPath.Success) fileId = mPath.Groups[1].Value;
                if (string.IsNullOrEmpty(fileId))
                {
                    Match mQuery = Regex.Match(u.Query, @"[?&]id=([a-zA-Z0-9_\-]+)", RegexOptions.IgnoreCase);
                    if (mQuery.Success) fileId = mQuery.Groups[1].Value;
                }
                if (!string.IsNullOrEmpty(fileId))
                {
                    directUrl = "https://drive.usercontent.google.com/download?id=" + Uri.EscapeDataString(fileId) + "&export=download&confirm=t";
                    return true;
                }
            }
            return false;
        }

        public static bool TryParseGoogleDriveConfirmationHtml(string htmlBody, Uri baseUri, out string confirmedDownloadUrl)
        {
            confirmedDownloadUrl = "";
            if (string.IsNullOrEmpty(htmlBody)) return false;
            if (htmlBody.IndexOf("download-form", StringComparison.OrdinalIgnoreCase) < 0 &&
                htmlBody.IndexOf("uc-download-link", StringComparison.OrdinalIgnoreCase) < 0 &&
                htmlBody.IndexOf("confirm=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            Match formM = Regex.Match(htmlBody, @"<form[^>]*id=[""']download-form[""'][^>]*action=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (formM.Success)
            {
                string action = WebUtility.HtmlDecode(formM.Groups[1].Value);
                List<string> qParams = new List<string>();
                MatchCollection inputs = Regex.Matches(htmlBody, @"<input[^>]*type=[""']hidden[""'][^>]*>", RegexOptions.IgnoreCase);
                for (int i = 0; i < inputs.Count; i++)
                {
                    string tag = inputs[i].Value;
                    Match nm = Regex.Match(tag, @"name=[""']([^""']+)[""']", RegexOptions.IgnoreCase);
                    Match vl = Regex.Match(tag, @"value=[""']([^""']*)[""']", RegexOptions.IgnoreCase);
                    if (nm.Success)
                    {
                        string val = vl.Success ? WebUtility.HtmlDecode(vl.Groups[1].Value) : "";
                        qParams.Add(Uri.EscapeDataString(nm.Groups[1].Value) + "=" + Uri.EscapeDataString(val));
                    }
                }
                if (qParams.Count > 0)
                {
                    confirmedDownloadUrl = action + (action.IndexOf('?') >= 0 ? "&" : "?") + string.Join("&", qParams.ToArray());
                    return true;
                }
            }

            Match hrefM = Regex.Match(htmlBody, @"href=[""']([^""']*confirm=[^""']+)[""']", RegexOptions.IgnoreCase);
            if (hrefM.Success)
            {
                string rawHref = WebUtility.HtmlDecode(hrefM.Groups[1].Value);
                Uri resolved;
                if (Uri.TryCreate(baseUri, rawHref, out resolved))
                {
                    confirmedDownloadUrl = resolved.AbsoluteUri;
                    return true;
                }
            }
            return false;
        }

        public static bool TryUnrestrictUrl(
            string originalUrl,
            MehediAppConfiguration cfg,
            out string unrestrictedUrl,
            out string unlockedFileName,
            out string providerUsed)
        {
            int maxChunks;
            return TryUnrestrictUrl(originalUrl, "", cfg, out unrestrictedUrl, out unlockedFileName, out providerUsed, out maxChunks);
        }

        public static bool TryUnrestrictUrl(
            string originalUrl,
            string linkPassword,
            MehediAppConfiguration cfg,
            out string unrestrictedUrl,
            out string unlockedFileName,
            out string providerUsed,
            out int maxChunks)
        {
            unrestrictedUrl = originalUrl;
            unlockedFileName = "";
            providerUsed = "";
            maxChunks = 0;
            if (cfg == null || cfg.ShihabDebridAccounts == null || string.IsNullOrWhiteSpace(originalUrl))
            {
                return false;
            }
            if (!IsPotentialDebridHost(originalUrl, cfg))
            {
                return false;
            }

            for (int i = 0; i < cfg.ShihabDebridAccounts.Count; i++)
            {
                ShihabDebridAccount acct = cfg.ShihabDebridAccounts[i];
                if (acct == null || !acct.MehediEnabled) continue;
                string apiKey = ShihabSecretsVault.UnprotectSecret(acct.MehediEncryptedApiToken);
                if (string.IsNullOrWhiteSpace(apiKey)) continue;

                try
                {
                    if (string.Equals(acct.ShihabProvider, "Real-Debrid", StringComparison.OrdinalIgnoreCase))
                    {
                        if (UnrestrictWithRealDebrid(originalUrl, linkPassword, apiKey, cfg, out unrestrictedUrl, out unlockedFileName, out maxChunks))
                        {
                            providerUsed = "Real-Debrid";
                            return true;
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "AllDebrid", StringComparison.OrdinalIgnoreCase))
                    {
                        if (UnrestrictWithAllDebrid(originalUrl, linkPassword, apiKey, cfg, out unrestrictedUrl, out unlockedFileName, out maxChunks))
                        {
                            providerUsed = "AllDebrid";
                            return true;
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "Premiumize", StringComparison.OrdinalIgnoreCase))
                    {
                        if (UnrestrictWithPremiumize(originalUrl, apiKey, cfg, out unrestrictedUrl, out unlockedFileName))
                        {
                            providerUsed = "Premiumize";
                            return true;
                        }
                    }
                }
                catch
                {
                }
            }
            return false;
        }

        public static bool TryUnlockTorrentOrMagnetWithDebrid(
            string magnetOrTorrent,
            MehediAppConfiguration cfg,
            out string unlockedDirectUrl,
            out string unlockedFileName,
            out string providerUsed)
        {
            unlockedDirectUrl = "";
            unlockedFileName = "";
            providerUsed = "";
            if (cfg == null || cfg.ShihabDebridAccounts == null || string.IsNullOrWhiteSpace(magnetOrTorrent)) return false;
            if (!magnetOrTorrent.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase)) return false;

            for (int i = 0; i < cfg.ShihabDebridAccounts.Count; i++)
            {
                ShihabDebridAccount acct = cfg.ShihabDebridAccounts[i];
                if (acct == null || !acct.MehediEnabled) continue;
                string token = ShihabSecretsVault.UnprotectSecret(acct.MehediEncryptedApiToken);
                if (string.IsNullOrWhiteSpace(token)) continue;

                try
                {
                    if (string.Equals(acct.ShihabProvider, "Real-Debrid", StringComparison.OrdinalIgnoreCase))
                    {
                        HttpWebRequest addReq = (HttpWebRequest)WebRequest.Create("https://api.real-debrid.com/rest/1.0/torrents/addMagnet");
                        addReq.Method = "POST";
                        addReq.Headers["Authorization"] = "Bearer " + token.Trim();
                        addReq.ContentType = "application/x-www-form-urlencoded";
                        addReq.Timeout = 10000;
                        ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(addReq, cfg, addReq.RequestUri.AbsoluteUri);
                        byte[] addBody = Encoding.UTF8.GetBytes("magnet=" + Uri.EscapeDataString(magnetOrTorrent));
                        addReq.ContentLength = addBody.Length;
                        using (Stream s = addReq.GetRequestStream()) s.Write(addBody, 0, addBody.Length);
                        string torrentId = "";
                        using (HttpWebResponse r1 = (HttpWebResponse)addReq.GetResponse())
                        using (StreamReader sr1 = new StreamReader(r1.GetResponseStream(), Encoding.UTF8))
                        {
                            torrentId = ExtractJsonStringField(sr1.ReadToEnd(), "id");
                        }
                        if (!string.IsNullOrEmpty(torrentId))
                        {
                            HttpWebRequest selReq = (HttpWebRequest)WebRequest.Create("https://api.real-debrid.com/rest/1.0/torrents/selectFiles/" + Uri.EscapeDataString(torrentId));
                            selReq.Method = "POST";
                            selReq.Headers["Authorization"] = "Bearer " + token.Trim();
                            selReq.ContentType = "application/x-www-form-urlencoded";
                            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(selReq, cfg, selReq.RequestUri.AbsoluteUri);
                            byte[] selBody = Encoding.UTF8.GetBytes("files=all");
                            selReq.ContentLength = selBody.Length;
                            using (Stream s2 = selReq.GetRequestStream()) s2.Write(selBody, 0, selBody.Length);
                            using (HttpWebResponse r2 = (HttpWebResponse)selReq.GetResponse()) { }

                            HttpWebRequest infoReq = (HttpWebRequest)WebRequest.Create("https://api.real-debrid.com/rest/1.0/torrents/info/" + Uri.EscapeDataString(torrentId));
                            infoReq.Headers["Authorization"] = "Bearer " + token.Trim();
                            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(infoReq, cfg, infoReq.RequestUri.AbsoluteUri);
                            using (HttpWebResponse r3 = (HttpWebResponse)infoReq.GetResponse())
                            using (StreamReader sr3 = new StreamReader(r3.GetResponseStream(), Encoding.UTF8))
                            {
                                string infoJson = sr3.ReadToEnd();
                                Match linkM = Regex.Match(infoJson, "\"links\"\\s*:\\s*\\[\\s*\"((?:\\\\.|[^\"])+)\"");
                                if (linkM.Success)
                                {
                                    string hostLink = linkM.Groups[1].Value.Replace("\\/", "/");
                                    int chunks;
                                    if (UnrestrictWithRealDebrid(hostLink, "", token, cfg, out unlockedDirectUrl, out unlockedFileName, out chunks))
                                    {
                                        providerUsed = "Real-Debrid Cloud";
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "Premiumize", StringComparison.OrdinalIgnoreCase))
                    {
                        if (UnrestrictWithPremiumize(magnetOrTorrent, token, cfg, out unlockedDirectUrl, out unlockedFileName))
                        {
                            providerUsed = "Premiumize Cloud";
                            return true;
                        }
                    }
                }
                catch { }
            }
            return false;
        }

        private static bool UnrestrictWithRealDebrid(
            string link,
            string password,
            string token,
            MehediAppConfiguration cfg,
            out string directUrl,
            out string fileName,
            out int maxChunks)
        {
            directUrl = "";
            fileName = "";
            maxChunks = 0;
            string endpoint = "https://api.real-debrid.com/rest/1.0/unrestrict/link";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
            req.Method = "POST";
            req.Headers["Authorization"] = "Bearer " + token.Trim();
            req.ContentType = "application/x-www-form-urlencoded";
            req.Timeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, endpoint);
            string form = "link=" + Uri.EscapeDataString(link);
            if (!string.IsNullOrEmpty(password)) form += "&password=" + Uri.EscapeDataString(password);
            byte[] body = Encoding.UTF8.GetBytes(form);
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string json = sr.ReadToEnd();
                directUrl = ExtractJsonStringField(json, "download");
                fileName = ExtractJsonStringField(json, "filename");
                maxChunks = (int)ExtractJsonLongField(json, "chunks");
                return !string.IsNullOrWhiteSpace(directUrl);
            }
        }

        private static bool UnrestrictWithAllDebrid(
            string link,
            string password,
            string apiKey,
            MehediAppConfiguration cfg,
            out string directUrl,
            out string fileName,
            out int maxChunks)
        {
            directUrl = "";
            fileName = "";
            maxChunks = 0;
            string url = "https://api.alldebrid.com/v4/link/unlock?agent=MHS-Download-Manager&link=" + Uri.EscapeDataString(link);
            if (!string.IsNullOrEmpty(password)) url += "&password=" + Uri.EscapeDataString(password);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            req.Timeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, url);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string json = sr.ReadToEnd();
                directUrl = ExtractJsonStringField(json, "link");
                fileName = ExtractJsonStringField(json, "filename");
                maxChunks = (int)ExtractJsonLongField(json, "max_chunks");
                if (!string.IsNullOrWhiteSpace(directUrl)) return true;

                // Handle AllDebrid delayed unlock ID
                string delayedId = ExtractJsonStringField(json, "delayed");
                if (string.IsNullOrEmpty(delayedId))
                {
                    long delayedNum = ExtractJsonLongField(json, "delayed");
                    if (delayedNum > 0) delayedId = delayedNum.ToString(CultureInfo.InvariantCulture);
                }
                if (!string.IsNullOrEmpty(delayedId))
                {
                    for (int poll = 0; poll < 6; poll++)
                    {
                        Thread.Sleep(1500);
                        string pollUrl = "https://api.alldebrid.com/v4/link/delayed?agent=MHS-Download-Manager&id=" + Uri.EscapeDataString(delayedId);
                        HttpWebRequest pReq = (HttpWebRequest)WebRequest.Create(pollUrl);
                        pReq.Headers["Authorization"] = "Bearer " + apiKey.Trim();
                        ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(pReq, cfg, pollUrl);
                        using (HttpWebResponse pResp = (HttpWebResponse)pReq.GetResponse())
                        using (StreamReader pSr = new StreamReader(pResp.GetResponseStream(), Encoding.UTF8))
                        {
                            string pJson = pSr.ReadToEnd();
                            directUrl = ExtractJsonStringField(pJson, "link");
                            if (!string.IsNullOrWhiteSpace(directUrl)) return true;
                        }
                    }
                }
                return false;
            }
        }

        private static bool UnrestrictWithPremiumize(
            string link,
            string apiKey,
            MehediAppConfiguration cfg,
            out string directUrl,
            out string fileName)
        {
            directUrl = "";
            fileName = "";
            string endpoint = "https://www.premiumize.me/api/transfer/directdl";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
            req.Method = "POST";
            req.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            req.ContentType = "application/x-www-form-urlencoded";
            req.Timeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, endpoint);
            string post = "src=" + Uri.EscapeDataString(link);
            byte[] body = Encoding.UTF8.GetBytes(post);
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
            {
                string json = sr.ReadToEnd();
                directUrl = ExtractJsonStringField(json, "location");
                fileName = ExtractJsonStringField(json, "filename");
                return !string.IsNullOrWhiteSpace(directUrl);
            }
        }

        public static string CheckAccountStatus(string providerName, string plainToken)
        {
            ShihabDebridAccount acct = new ShihabDebridAccount
            {
                ShihabProvider = providerName ?? "Real-Debrid",
                MehediEncryptedApiToken = ShihabSecretsVault.ProtectSecret(plainToken ?? "")
            };
            string summary;
            VerifyAccountStatus(acct, out summary);
            return summary;
        }

        public static bool VerifyAccountStatus(ShihabDebridAccount acct, out string statusSummary)
        {
            statusSummary = "Not configured";
            if (acct == null) return false;
            string token = ShihabSecretsVault.UnprotectSecret(acct.MehediEncryptedApiToken);
            if (string.IsNullOrWhiteSpace(token))
            {
                acct.ShihabAccountStatus = "No API token set";
                statusSummary = acct.ShihabAccountStatus;
                return false;
            }
            try
            {
                ShihabAutoUpdater.EnforceModernTls();
                string url = "https://api.real-debrid.com/rest/1.0/user";
                if (string.Equals(acct.ShihabProvider, "AllDebrid", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://api.alldebrid.com/v4/user?agent=MHS-Download-Manager";
                }
                else if (string.Equals(acct.ShihabProvider, "Premiumize", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://www.premiumize.me/api/account/info";
                }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Headers["Authorization"] = "Bearer " + token.Trim();
                req.Timeout = 8000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    if (json.IndexOf("\"status\":\"error\"", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string errMsg = ExtractJsonStringField(json, "message");
                        acct.ShihabAccountStatus = "Error: " + (string.IsNullOrEmpty(errMsg) ? "Invalid token" : errMsg);
                        statusSummary = acct.ShihabAccountStatus;
                        return false;
                    }
                    string user = ExtractJsonStringField(json, "username");
                    if (string.IsNullOrEmpty(user)) user = ExtractJsonStringField(json, "customer_id");
                    string type = ExtractJsonStringField(json, "type");
                    string exp = ExtractJsonStringField(json, "expiration");
                    if (string.IsNullOrEmpty(exp))
                    {
                        long unixExp = ExtractJsonLongField(json, "premiumUntil");
                        if (unixExp <= 0) unixExp = ExtractJsonLongField(json, "premium_until");
                        if (unixExp > 0)
                        {
                            exp = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixExp).ToLocalTime().ToString("yyyy-MM-dd");
                        }
                    }
                    if (!string.IsNullOrEmpty(user)) acct.ShihabUsername = user;
                    if (!string.IsNullOrEmpty(exp)) acct.MehediExpiresAt = exp;
                    acct.ShihabAccountStatus = string.Format("Active ({0}{1}{2})",
                        string.IsNullOrEmpty(user) ? acct.ShihabProvider : user,
                        string.IsNullOrEmpty(type) ? "" : " - " + type,
                        string.IsNullOrEmpty(exp) ? "" : ", exp " + exp);
                    statusSummary = acct.ShihabAccountStatus;
                    return true;
                }
            }
            catch (Exception ex)
            {
                acct.ShihabAccountStatus = "Verification failed: " + ex.Message;
                statusSummary = acct.ShihabAccountStatus;
                return false;
            }
        }

        internal static string ExtractJsonStringField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName)) return "";
            Match m = Regex.Match(json, "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\"((?:\\\\.|[^\"])*)\"");
            if (m.Success)
            {
                return m.Groups[1].Value
                    .Replace("\\/", "/")
                    .Replace("\\\"", "\"")
                    .Replace("\\n", "\n")
                    .Replace("\\r", "")
                    .Replace("\\\\", "\\");
            }
            return "";
        }

        internal static long ExtractJsonLongField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName)) return 0;
            Match m = Regex.Match(json, "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*(-?[0-9]+)");
            long v = 0;
            if (m.Success) long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
            return v;
        }
    }

    // =========================================================================
    // 7. BITTORRENT & MAGNET P2P ENGINE (BEP-3, BEP-9/10, BEP-15, BEP-19, Blocklist)
    // =========================================================================
    public class ShihabTorrentFileEntry
    {
        public int Index { get; set; }
        public string RelativePath { get; set; }
        public long SizeBytes { get; set; }
        public long ByteOffsetInTorrent { get; set; }
        public bool IsPaddingFile { get; set; }
        public bool Selected { get; set; }

        public long Length
        {
            get { return SizeBytes; }
            set { SizeBytes = value; }
        }
    }

    public class ShihabTorrentMetadata
    {
        public string Name { get; set; }
        public string InfoHashHex { get; set; }
        public byte[] InfoHashBytes { get; set; }
        public long TotalSizeBytes { get; set; }
        public long PieceLength { get; set; }
        public int PieceCount { get; set; }
        public byte[] PiecesSha1Concat { get; set; }
        public bool IsMultiFile { get; set; }
        public string Comment { get; set; }
        public string ErrorMessage { get; set; }
        public List<string> Trackers { get; set; }
        public List<string> WebSeeds { get; set; }
        public List<ShihabTorrentFileEntry> Files { get; set; }

        public bool ParsedSuccessfully
        {
            get { return string.IsNullOrEmpty(ErrorMessage) && (Files.Count > 0 || !string.IsNullOrEmpty(InfoHashHex)); }
            set { }
        }

        public long TotalSize
        {
            get { return TotalSizeBytes; }
            set { TotalSizeBytes = value; }
        }

        public ShihabTorrentMetadata()
        {
            Name = "torrent_download";
            InfoHashHex = "";
            InfoHashBytes = new byte[0];
            PiecesSha1Concat = new byte[0];
            ErrorMessage = "";
            Trackers = new List<string>();
            WebSeeds = new List<string>();
            Files = new List<ShihabTorrentFileEntry>();
        }
    }

    public class ShihabBencodeNode
    {
        public string Kind; // "int", "bytes", "list", "dict"
        public long IntValue;
        public byte[] BytesValue;
        public List<ShihabBencodeNode> ListValue;
        public Dictionary<string, ShihabBencodeNode> DictValue;
        public int RawStartOffset;
        public int RawEndOffset;

        public string GetUtf8String()
        {
            return BytesValue != null ? Encoding.UTF8.GetString(BytesValue) : "";
        }
    }

    public static class ShihabTorrentManager
    {
        private struct IpRangeEntry : IComparable<IpRangeEntry>
        {
            public uint Start;
            public uint End;
            public int CompareTo(IpRangeEntry other) { return Start.CompareTo(other.Start); }
        }

        private static readonly object _blocklistLock = new object();
        private static string _loadedBlocklistSource = "";
        private static List<IpRangeEntry> _blockedRanges = new List<IpRangeEntry>();

        private static readonly string[] DefaultPublicTrackers = new string[]
        {
            "udp://tracker.opentrackr.org:1337/announce",
            "udp://open.stealth.si:80/announce",
            "udp://tracker.torrent.eu.org:451/announce",
            "udp://exodus.desync.com:6969/announce"
        };

        public static bool IsMagnetOrTorrentInput(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return false;
            string s = input.Trim();
            if (s.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase)) return true;
            Uri u;
            if (Uri.TryCreate(s, UriKind.Absolute, out u))
            {
                return u.AbsolutePath.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
            }
            return s.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase);
        }

        public static byte[] DecodeHexOrBase32Btih(string rawHash)
        {
            if (string.IsNullOrWhiteSpace(rawHash)) return new byte[0];
            string clean = rawHash.Trim();
            if (clean.Length == 40)
            {
                byte[] bytes = new byte[20];
                for (int i = 0; i < 20; i++)
                {
                    byte b;
                    if (!byte.TryParse(clean.Substring(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out b))
                        return new byte[0];
                    bytes[i] = b;
                }
                return bytes;
            }
            if (clean.Length == 32)
            {
                // RFC 4648 Base32 decode (A-Z = 0-25, 2-7 = 26-31)
                string upper = clean.ToUpperInvariant();
                int bitBuf = 0;
                int bitCount = 0;
                byte[] outBytes = new byte[20];
                int outIdx = 0;
                for (int i = 0; i < 32; i++)
                {
                    char c = upper[i];
                    int val;
                    if (c >= 'A' && c <= 'Z') val = c - 'A';
                    else if (c >= '2' && c <= '7') val = 26 + (c - '2');
                    else return new byte[0];
                    bitBuf = (bitBuf << 5) | val;
                    bitCount += 5;
                    if (bitCount >= 8 && outIdx < 20)
                    {
                        bitCount -= 8;
                        outBytes[outIdx++] = (byte)((bitBuf >> bitCount) & 0xFF);
                    }
                }
                return outIdx == 20 ? outBytes : new byte[0];
            }
            return new byte[0];
        }

        private static string BytesToHexLower(byte[] data)
        {
            if (data == null || data.Length == 0) return "";
            StringBuilder sb = new StringBuilder(data.Length * 2);
            for (int i = 0; i < data.Length; i++) sb.Append(data[i].ToString("x2"));
            return sb.ToString();
        }

        public static ShihabTorrentMetadata ParseMagnetUri(string magnetUri)
        {
            ShihabTorrentMetadata meta = new ShihabTorrentMetadata();
            if (string.IsNullOrWhiteSpace(magnetUri)) return meta;
            int q = magnetUri.IndexOf('?');
            string query = q >= 0 ? magnetUri.Substring(q + 1) : magnetUri;
            string[] parts = query.Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0) continue;
                string k = parts[i].Substring(0, eq).ToLowerInvariant();
                string v = Uri.UnescapeDataString(parts[i].Substring(eq + 1).Replace('+', ' '));
                if (k == "dn" && !string.IsNullOrWhiteSpace(v))
                {
                    meta.Name = ShihabFormatHelpers.SanitizeShihabFileName(v);
                }
                else if (k == "xt" && v.StartsWith("urn:btih:", StringComparison.OrdinalIgnoreCase))
                {
                    byte[] ih = DecodeHexOrBase32Btih(v.Substring(9).Trim());
                    if (ih.Length == 20)
                    {
                        meta.InfoHashBytes = ih;
                        meta.InfoHashHex = BytesToHexLower(ih);
                    }
                    else
                    {
                        meta.InfoHashHex = v.Substring(9).Trim().ToLowerInvariant();
                    }
                }
                else if (k == "tr" && !string.IsNullOrWhiteSpace(v))
                {
                    if (!meta.Trackers.Contains(v.Trim())) meta.Trackers.Add(v.Trim());
                }
                else if ((k == "ws" || k == "as") && !string.IsNullOrWhiteSpace(v))
                {
                    if (!meta.WebSeeds.Contains(v.Trim())) meta.WebSeeds.Add(v.Trim());
                }
                else if (k == "xl")
                {
                    long sz = 0;
                    if (long.TryParse(v, out sz) && sz > 0) meta.TotalSizeBytes = sz;
                }
            }
            for (int d = 0; d < DefaultPublicTrackers.Length; d++)
            {
                if (!meta.Trackers.Contains(DefaultPublicTrackers[d])) meta.Trackers.Add(DefaultPublicTrackers[d]);
            }
            if (meta.Name == "torrent_download" && !string.IsNullOrEmpty(meta.InfoHashHex))
            {
                meta.Name = "magnet_" + meta.InfoHashHex.Substring(0, Math.Min(12, meta.InfoHashHex.Length));
            }
            meta.Files.Add(new ShihabTorrentFileEntry
            {
                Index = 0,
                RelativePath = meta.Name,
                SizeBytes = meta.TotalSizeBytes,
                Selected = true
            });
            return meta;
        }

        public static ShihabTorrentMetadata InspectTorrentOrMagnet(string input, MehediAppConfiguration cfg = null)
        {
            if (string.IsNullOrWhiteSpace(input)) return new ShihabTorrentMetadata { ErrorMessage = "Empty torrent input." };
            string trimmed = input.Trim();
            if (trimmed.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                return ParseMagnetUri(trimmed);
            }
            if (File.Exists(trimmed))
            {
                return InspectTorrentFile(trimmed);
            }
            Uri remoteUri;
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out remoteUri) &&
                (remoteUri.Scheme == "http" || remoteUri.Scheme == "https"))
            {
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(trimmed);
                    req.UserAgent = "MHS-Download-Manager/" + ShihabAutoUpdater.GetCurrentAppVersion();
                    req.Timeout = 15000;
                    if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, trimmed);
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream s = resp.GetResponseStream())
                    using (MemoryStream ms = new MemoryStream())
                    {
                        byte[] buf = new byte[16384];
                        int r;
                        while ((r = s.Read(buf, 0, buf.Length)) > 0)
                        {
                            ms.Write(buf, 0, r);
                            if (ms.Length > 10 * 1024 * 1024) break;
                        }
                        return InspectTorrentBytes(ms.ToArray(), Path.GetFileNameWithoutExtension(remoteUri.AbsolutePath));
                    }
                }
                catch (Exception ex)
                {
                    return new ShihabTorrentMetadata { ErrorMessage = "Failed to download .torrent file: " + ex.Message };
                }
            }
            return new ShihabTorrentMetadata { ErrorMessage = "Torrent file not found: " + trimmed };
        }

        public static ShihabTorrentMetadata InspectTorrentFile(string torrentFilePath)
        {
            byte[] raw = File.ReadAllBytes(torrentFilePath);
            return InspectTorrentBytes(raw, Path.GetFileNameWithoutExtension(torrentFilePath));
        }

        public static ShihabTorrentMetadata InspectTorrentBytes(byte[] raw, string fallbackName)
        {
            ShihabTorrentMetadata meta = new ShihabTorrentMetadata();
            meta.Name = string.IsNullOrWhiteSpace(fallbackName) ? "torrent_download" : fallbackName;
            if (raw == null || raw.Length == 0)
            {
                meta.ErrorMessage = "Empty .torrent data.";
                return meta;
            }

            int idx = 0;
            ShihabBencodeNode root = ParseBencodeValue(raw, ref idx);
            if (root == null || root.DictValue == null)
            {
                meta.ErrorMessage = "Invalid bencoded .torrent dictionary.";
                return meta;
            }

            ShihabBencodeNode announceNode;
            if (root.DictValue.TryGetValue("announce", out announceNode) && announceNode != null)
            {
                string tr = announceNode.GetUtf8String();
                if (!string.IsNullOrWhiteSpace(tr)) meta.Trackers.Add(tr);
            }
            ShihabBencodeNode announceListNode;
            if (root.DictValue.TryGetValue("announce-list", out announceListNode) && announceListNode != null && announceListNode.ListValue != null)
            {
                for (int i = 0; i < announceListNode.ListValue.Count; i++)
                {
                    ShihabBencodeNode tier = announceListNode.ListValue[i];
                    if (tier != null && tier.ListValue != null)
                    {
                        for (int j = 0; j < tier.ListValue.Count; j++)
                        {
                            string tr = tier.ListValue[j].GetUtf8String();
                            if (!string.IsNullOrWhiteSpace(tr) && !meta.Trackers.Contains(tr))
                            {
                                meta.Trackers.Add(tr);
                            }
                        }
                    }
                }
            }

            ShihabBencodeNode urlListNode;
            if (root.DictValue.TryGetValue("url-list", out urlListNode) && urlListNode != null)
            {
                if (urlListNode.Kind == "bytes")
                {
                    string ws = urlListNode.GetUtf8String();
                    if (!string.IsNullOrWhiteSpace(ws)) meta.WebSeeds.Add(ws);
                }
                else if (urlListNode.ListValue != null)
                {
                    for (int i = 0; i < urlListNode.ListValue.Count; i++)
                    {
                        string ws = urlListNode.ListValue[i].GetUtf8String();
                        if (!string.IsNullOrWhiteSpace(ws)) meta.WebSeeds.Add(ws);
                    }
                }
            }

            ShihabBencodeNode commentNode;
            if (root.DictValue.TryGetValue("comment", out commentNode) && commentNode != null)
            {
                meta.Comment = commentNode.GetUtf8String();
            }

            ShihabBencodeNode infoNode;
            if (root.DictValue.TryGetValue("info", out infoNode) && infoNode != null && infoNode.DictValue != null)
            {
                PopulateMetadataFromInfoNode(meta, raw, infoNode);
            }
            return meta;
        }

        private static void PopulateMetadataFromInfoNode(ShihabTorrentMetadata meta, byte[] rawContainer, ShihabBencodeNode infoNode)
        {
            int infoLen = infoNode.RawEndOffset - infoNode.RawStartOffset;
            if (infoLen > 0 && infoNode.RawStartOffset >= 0 && infoNode.RawEndOffset <= rawContainer.Length)
            {
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] hash = sha1.ComputeHash(rawContainer, infoNode.RawStartOffset, infoLen);
                    meta.InfoHashBytes = hash;
                    meta.InfoHashHex = BytesToHexLower(hash);
                }
            }

            ShihabBencodeNode nameNode;
            if (infoNode.DictValue.TryGetValue("name.utf-8", out nameNode) && nameNode != null && !string.IsNullOrWhiteSpace(nameNode.GetUtf8String()))
            {
                meta.Name = ShihabFormatHelpers.SanitizeShihabFileName(nameNode.GetUtf8String());
            }
            else if (infoNode.DictValue.TryGetValue("name", out nameNode) && nameNode != null && !string.IsNullOrWhiteSpace(nameNode.GetUtf8String()))
            {
                meta.Name = ShihabFormatHelpers.SanitizeShihabFileName(nameNode.GetUtf8String());
            }

            ShihabBencodeNode plNode;
            if (infoNode.DictValue.TryGetValue("piece length", out plNode) && plNode != null)
            {
                meta.PieceLength = plNode.IntValue;
            }
            ShihabBencodeNode piecesNode;
            if (infoNode.DictValue.TryGetValue("pieces", out piecesNode) && piecesNode != null && piecesNode.BytesValue != null)
            {
                meta.PiecesSha1Concat = piecesNode.BytesValue;
                meta.PieceCount = piecesNode.BytesValue.Length / 20;
            }

            meta.Files.Clear();
            ShihabBencodeNode filesNode;
            if (infoNode.DictValue.TryGetValue("files", out filesNode) && filesNode != null && filesNode.ListValue != null)
            {
                meta.IsMultiFile = true;
                long runningOffset = 0;
                for (int i = 0; i < filesNode.ListValue.Count; i++)
                {
                    ShihabBencodeNode fDict = filesNode.ListValue[i];
                    if (fDict == null || fDict.DictValue == null) continue;
                    long fLen = 0;
                    ShihabBencodeNode lenNode;
                    if (fDict.DictValue.TryGetValue("length", out lenNode) && lenNode != null)
                    {
                        fLen = Math.Max(0, lenNode.IntValue);
                    }
                    string attrStr = "";
                    ShihabBencodeNode attrNode;
                    if (fDict.DictValue.TryGetValue("attr", out attrNode) && attrNode != null)
                    {
                        attrStr = attrNode.GetUtf8String();
                    }

                    string relPath = "file_" + i;
                    ShihabBencodeNode pathNode;
                    if (!fDict.DictValue.TryGetValue("path.utf-8", out pathNode) || pathNode == null)
                    {
                        fDict.DictValue.TryGetValue("path", out pathNode);
                    }
                    bool isPad = attrStr.IndexOf('p') >= 0;
                    if (pathNode != null && pathNode.ListValue != null)
                    {
                        List<string> segs = new List<string>();
                        for (int p = 0; p < pathNode.ListValue.Count; p++)
                        {
                            string seg = pathNode.ListValue[p].GetUtf8String();
                            if (p == 0 && seg == ".pad") isPad = true;
                            if (seg == ".." || seg == ".") continue;
                            segs.Add(ShihabFormatHelpers.SanitizeShihabFileName(seg));
                        }
                        if (segs.Count > 0) relPath = string.Join(Path.DirectorySeparatorChar.ToString(), segs.ToArray());
                    }

                    meta.Files.Add(new ShihabTorrentFileEntry
                    {
                        Index = i,
                        RelativePath = relPath,
                        SizeBytes = fLen,
                        ByteOffsetInTorrent = runningOffset,
                        IsPaddingFile = isPad,
                        Selected = !isPad
                    });
                    runningOffset += fLen;
                }
                meta.TotalSizeBytes = runningOffset;
            }
            else
            {
                meta.IsMultiFile = false;
                ShihabBencodeNode lenNode;
                long singleLen = 0;
                if (infoNode.DictValue.TryGetValue("length", out lenNode) && lenNode != null)
                {
                    singleLen = Math.Max(0, lenNode.IntValue);
                }
                meta.TotalSizeBytes = singleLen;
                meta.Files.Add(new ShihabTorrentFileEntry
                {
                    Index = 0,
                    RelativePath = meta.Name,
                    SizeBytes = singleLen,
                    ByteOffsetInTorrent = 0,
                    IsPaddingFile = false,
                    Selected = true
                });
            }
        }

        // ---------------------------------------------------------------------
        // IP Blocklist Loader & Binary Search (PeerGuardian + CIDR)
        // ---------------------------------------------------------------------
        public static void EnsurePeerBlocklistLoaded(MehediAppConfiguration cfg, string baseDataDir)
        {
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ShihabTorrentBlocklistUrl)) return;
            string src = cfg.ShihabTorrentBlocklistUrl.Trim();
            lock (_blocklistLock)
            {
                if (string.Equals(_loadedBlocklistSource, src, StringComparison.OrdinalIgnoreCase)) return;
                _loadedBlocklistSource = src;
                _blockedRanges.Clear();
            }

            try
            {
                string rawText = "";
                if (File.Exists(src))
                {
                    rawText = File.ReadAllText(src, Encoding.UTF8);
                }
                else
                {
                    string cacheFile = Path.Combine(baseDataDir ?? Path.GetTempPath(), "bt_blocklist.txt");
                    if (!File.Exists(cacheFile) || (DateTime.UtcNow - File.GetLastWriteTimeUtc(cacheFile)).TotalHours > 24)
                    {
                        HttpWebRequest req = (HttpWebRequest)WebRequest.Create(src);
                        req.Timeout = 10000;
                        ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, src);
                        using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                        using (Stream st = resp.GetResponseStream())
                        {
                            Stream decodeStream = st;
                            if (src.EndsWith(".gz", StringComparison.OrdinalIgnoreCase))
                            {
                                decodeStream = new GZipStream(st, CompressionMode.Decompress);
                            }
                            using (StreamReader sr = new StreamReader(decodeStream, Encoding.UTF8))
                            {
                                rawText = sr.ReadToEnd();
                            }
                        }
                        try { File.WriteAllText(cacheFile, rawText, Encoding.UTF8); } catch { }
                    }
                    else
                    {
                        rawText = File.ReadAllText(cacheFile, Encoding.UTF8);
                    }
                }

                List<IpRangeEntry> parsed = new List<IpRangeEntry>();
                string[] lines = rawText.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i].Trim();
                    if (ln.Length == 0 || ln[0] == '#' || ln.StartsWith("//", StringComparison.Ordinal)) continue;
                    int colon = ln.LastIndexOf(':');
                    string rangeSpec = colon >= 0 ? ln.Substring(colon + 1).Trim() : ln;
                    int dash = rangeSpec.IndexOf('-');
                    if (dash > 0)
                    {
                        uint a, b;
                        if (TryParseIpv4Uint(rangeSpec.Substring(0, dash), out a) &&
                            TryParseIpv4Uint(rangeSpec.Substring(dash + 1), out b))
                        {
                            parsed.Add(new IpRangeEntry { Start = Math.Min(a, b), End = Math.Max(a, b) });
                        }
                    }
                    else
                    {
                        int slash = rangeSpec.IndexOf('/');
                        int prefix;
                        uint ipVal;
                        if (slash > 0 &&
                            TryParseIpv4Uint(rangeSpec.Substring(0, slash), out ipVal) &&
                            int.TryParse(rangeSpec.Substring(slash + 1), out prefix) &&
                            prefix >= 0 && prefix <= 32)
                        {
                            uint mask = prefix == 0 ? 0u : (0xFFFFFFFFu << (32 - prefix));
                            uint start = ipVal & mask;
                            uint end = start | ~mask;
                            parsed.Add(new IpRangeEntry { Start = start, End = end });
                        }
                    }
                }
                parsed.Sort();
                lock (_blocklistLock) { _blockedRanges = parsed; }
            }
            catch { }
        }

        private static bool TryParseIpv4Uint(string text, out uint val)
        {
            val = 0;
            IPAddress ip;
            if (!IPAddress.TryParse((text ?? "").Trim(), out ip) || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] b = ip.GetAddressBytes();
            val = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
            return true;
        }

        public static bool IsPeerBlocked(IPAddress ip)
        {
            if (ip == null || ip.AddressFamily != AddressFamily.InterNetwork) return false;
            byte[] b = ip.GetAddressBytes();
            uint v = ((uint)b[0] << 24) | ((uint)b[1] << 16) | ((uint)b[2] << 8) | b[3];
            lock (_blocklistLock)
            {
                int lo = 0;
                int hi = _blockedRanges.Count - 1;
                while (lo <= hi)
                {
                    int mid = lo + ((hi - lo) >> 1);
                    IpRangeEntry r = _blockedRanges[mid];
                    if (v < r.Start) hi = mid - 1;
                    else if (v > r.End) lo = mid + 1;
                    else return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------------
        // HTTP/HTTPS & UDP (BEP-15) Tracker Announce + P2P Piece Downloader
        // ---------------------------------------------------------------------
        public static List<IPEndPoint> DiscoverPeersFromTrackers(
            ShihabTorrentMetadata meta,
            byte[] peerId20,
            int listenPort,
            MehediAppConfiguration cfg)
        {
            List<IPEndPoint> peers = new List<IPEndPoint>();
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (meta == null || meta.InfoHashBytes == null || meta.InfoHashBytes.Length != 20) return peers;

            List<string> trackers = new List<string>(meta.Trackers);
            for (int d = 0; d < DefaultPublicTrackers.Length; d++)
            {
                if (!trackers.Contains(DefaultPublicTrackers[d])) trackers.Add(DefaultPublicTrackers[d]);
            }

            for (int i = 0; i < trackers.Count && peers.Count < 80; i++)
            {
                string tr = trackers[i];
                if (string.IsNullOrWhiteSpace(tr)) continue;
                try
                {
                    List<IPEndPoint> batch = null;
                    if (tr.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                        tr.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        batch = AnnounceHttpTracker(tr, meta.InfoHashBytes, peerId20, listenPort, Math.Max(0, meta.TotalSizeBytes), cfg);
                    }
                    else if (tr.StartsWith("udp://", StringComparison.OrdinalIgnoreCase))
                    {
                        batch = AnnounceUdpTracker(tr, meta.InfoHashBytes, peerId20, listenPort, Math.Max(0, meta.TotalSizeBytes));
                    }
                    if (batch != null)
                    {
                        for (int p = 0; p < batch.Count; p++)
                        {
                            IPEndPoint ep = batch[p];
                            if (ep == null || ep.Port <= 0 || IsPeerBlocked(ep.Address)) continue;
                            string key = ep.ToString();
                            if (seen.Add(key)) peers.Add(ep);
                        }
                    }
                }
                catch { }
            }
            return peers;
        }

        private static string UrlEncodeRaw20Bytes(byte[] b20)
        {
            StringBuilder sb = new StringBuilder(60);
            for (int i = 0; i < b20.Length; i++)
            {
                sb.Append('%').Append(b20[i].ToString("X2"));
            }
            return sb.ToString();
        }

        private static List<IPEndPoint> AnnounceHttpTracker(
            string trackerUrl,
            byte[] infoHash20,
            byte[] peerId20,
            int port,
            long leftBytes,
            MehediAppConfiguration cfg)
        {
            List<IPEndPoint> list = new List<IPEndPoint>();
            string sep = trackerUrl.IndexOf('?') >= 0 ? "&" : "?";
            string full = string.Format(CultureInfo.InvariantCulture,
                "{0}{1}info_hash={2}&peer_id={3}&port={4}&uploaded=0&downloaded=0&left={5}&compact=1&numwant=50&event=started",
                trackerUrl, sep, UrlEncodeRaw20Bytes(infoHash20), UrlEncodeRaw20Bytes(peerId20), port, leftBytes);

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(full);
            req.UserAgent = "MHS-Download-Manager/1.0";
            req.Timeout = 6000;
            if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, full);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (MemoryStream ms = new MemoryStream())
            {
                s.CopyTo(ms);
                byte[] raw = ms.ToArray();
                int idx = 0;
                ShihabBencodeNode root = ParseBencodeValue(raw, ref idx);
                if (root != null && root.DictValue != null)
                {
                    ShihabBencodeNode peersNode;
                    if (root.DictValue.TryGetValue("peers", out peersNode) && peersNode != null)
                    {
                        if (peersNode.Kind == "bytes" && peersNode.BytesValue != null)
                        {
                            byte[] pb = peersNode.BytesValue;
                            for (int i = 0; i + 6 <= pb.Length; i += 6)
                            {
                                IPAddress ip = new IPAddress(new byte[] { pb[i], pb[i + 1], pb[i + 2], pb[i + 3] });
                                int pPort = (pb[i + 4] << 8) | pb[i + 5];
                                if (pPort > 0) list.Add(new IPEndPoint(ip, pPort));
                            }
                        }
                        else if (peersNode.ListValue != null)
                        {
                            for (int i = 0; i < peersNode.ListValue.Count; i++)
                            {
                                ShihabBencodeNode pd = peersNode.ListValue[i];
                                if (pd == null || pd.DictValue == null) continue;
                                ShihabBencodeNode ipN, ptN;
                                IPAddress ip;
                                if (pd.DictValue.TryGetValue("ip", out ipN) && pd.DictValue.TryGetValue("port", out ptN) &&
                                    ipN != null && ptN != null && IPAddress.TryParse(ipN.GetUtf8String(), out ip))
                                {
                                    list.Add(new IPEndPoint(ip, (int)ptN.IntValue));
                                }
                            }
                        }
                    }
                }
            }
            return list;
        }

        private static List<IPEndPoint> AnnounceUdpTracker(
            string udpUrl,
            byte[] infoHash20,
            byte[] peerId20,
            int port,
            long leftBytes)
        {
            List<IPEndPoint> list = new List<IPEndPoint>();
            Uri u;
            if (!Uri.TryCreate(udpUrl, UriKind.Absolute, out u)) return list;
            int trPort = u.Port > 0 ? u.Port : 80;
            IPAddress[] addrs = Dns.GetHostAddresses(u.Host);
            IPAddress targetIp = null;
            for (int i = 0; i < addrs.Length; i++)
            {
                if (addrs[i].AddressFamily == AddressFamily.InterNetwork) { targetIp = addrs[i]; break; }
            }
            if (targetIp == null) return list;

            using (UdpClient udp = new UdpClient(AddressFamily.InterNetwork))
            {
                udp.Client.ReceiveTimeout = 4000;
                udp.Client.SendTimeout = 4000;
                IPEndPoint remote = new IPEndPoint(targetIp, trPort);

                // BEP-15 Connect Request (protocol_id = 0x41727101980, action = 0)
                byte[] connReq = new byte[16];
                WriteBeUInt64(connReq, 0, 0x0000041727101980UL);
                WriteBeUInt32(connReq, 8, 0u);
                uint txId = (uint)(Environment.TickCount ^ 0x5A5AA5A5);
                WriteBeUInt32(connReq, 12, txId);
                udp.Send(connReq, connReq.Length, remote);

                IPEndPoint fromEp = new IPEndPoint(IPAddress.Any, 0);
                byte[] connResp = udp.Receive(ref fromEp);
                if (connResp == null || connResp.Length < 16) return list;
                if (ReadBeUInt32(connResp, 0) != 0u || ReadBeUInt32(connResp, 4) != txId) return list;
                ulong connectionId = ReadBeUInt64(connResp, 8);

                // BEP-15 Announce Request (98 bytes, action = 1)
                byte[] annReq = new byte[98];
                WriteBeUInt64(annReq, 0, connectionId);
                WriteBeUInt32(annReq, 8, 1u);
                uint txId2 = txId + 1u;
                WriteBeUInt32(annReq, 12, txId2);
                Buffer.BlockCopy(infoHash20, 0, annReq, 16, 20);
                Buffer.BlockCopy(peerId20, 0, annReq, 36, 20);
                WriteBeUInt64(annReq, 56, 0UL); // downloaded
                WriteBeUInt64(annReq, 64, (ulong)Math.Max(0L, leftBytes));
                WriteBeUInt64(annReq, 72, 0UL); // uploaded
                WriteBeUInt32(annReq, 80, 2u);  // event = started
                WriteBeUInt32(annReq, 84, 0u);  // IP default
                WriteBeUInt32(annReq, 88, txId ^ 0x12345678u); // key
                WriteBeUInt32(annReq, 92, 50u); // num_want
                annReq[96] = (byte)((port >> 8) & 0xFF);
                annReq[97] = (byte)(port & 0xFF);

                udp.Send(annReq, annReq.Length, remote);
                byte[] annResp = udp.Receive(ref fromEp);
                if (annResp == null || annResp.Length < 20) return list;
                if (ReadBeUInt32(annResp, 0) != 1u || ReadBeUInt32(annResp, 4) != txId2) return list;

                for (int i = 20; i + 6 <= annResp.Length; i += 6)
                {
                    IPAddress ip = new IPAddress(new byte[] { annResp[i], annResp[i + 1], annResp[i + 2], annResp[i + 3] });
                    int pPort = (annResp[i + 4] << 8) | annResp[i + 5];
                    if (pPort > 0) list.Add(new IPEndPoint(ip, pPort));
                }
            }
            return list;
        }

        public static bool ExecuteP2pTorrentSession(
            ShihabDownloadItem item,
            ShihabTorrentMetadata meta,
            MehediAppConfiguration cfg,
            string baseDataDir,
            Action onProgressChanged,
            CancellationToken cancelToken,
            out string errorMessage)
        {
            errorMessage = "";
            if (meta == null) { errorMessage = "Missing torrent metadata."; return false; }
            EnsurePeerBlocklistLoaded(cfg, baseDataDir);

            byte[] peerId = Encoding.ASCII.GetBytes("-MH1030-" + Guid.NewGuid().ToString("N").Substring(0, 12));
            int listenPort = 51413;

            // Step 1: Discover peers from trackers (HTTP/HTTPS + UDP BEP-15)
            item.ShihabStatusText = "Announcing to trackers...";
            if (onProgressChanged != null) onProgressChanged();
            List<IPEndPoint> peers = DiscoverPeersFromTrackers(meta, peerId, listenPort, cfg);

            // Step 2: If Magnet link lacks .torrent info dictionary (PieceCount == 0), fetch ut_metadata (BEP-9/10) from peers
            if (meta.PieceCount <= 0 && meta.InfoHashBytes != null && meta.InfoHashBytes.Length == 20)
            {
                item.ShihabStatusText = string.Format("Fetching magnet metadata ({0} peers)...", peers.Count);
                if (onProgressChanged != null) onProgressChanged();
                bool gotMeta = false;
                for (int p = 0; p < peers.Count && !cancelToken.IsCancellationRequested; p++)
                {
                    byte[] infoDictBytes = TryFetchMagnetMetadataFromPeer(peers[p], meta.InfoHashBytes, peerId, cfg, cancelToken);
                    if (infoDictBytes != null && infoDictBytes.Length > 0)
                    {
                        using (SHA1 sha1 = SHA1.Create())
                        {
                            byte[] check = sha1.ComputeHash(infoDictBytes);
                            if (BytesToHexLower(check) == meta.InfoHashHex)
                            {
                                int pos = 0;
                                ShihabBencodeNode infoNode = ParseBencodeValue(infoDictBytes, ref pos);
                                if (infoNode != null && infoNode.DictValue != null)
                                {
                                    PopulateMetadataFromInfoNode(meta, infoDictBytes, infoNode);
                                    gotMeta = meta.PieceCount > 0;
                                    if (gotMeta) break;
                                }
                            }
                        }
                    }
                }
                if (!gotMeta && meta.WebSeeds.Count == 0)
                {
                    errorMessage = peers.Count == 0
                        ? "No BitTorrent peers responded from trackers (check firewall/UDP trackers or configure a Debrid account)."
                        : "Connected peers did not supply BEP-9 magnet metadata.";
                    return false;
                }
            }

            if (meta.PieceCount <= 0 || meta.PieceLength <= 0 || meta.TotalSizeBytes <= 0)
            {
                errorMessage = "Torrent metadata has zero pieces or unknown total size.";
                return false;
            }

            // Apply per-item file selection filter
            HashSet<int> selectedIdx = null;
            if (item.ShihabSelectedTorrentFiles != null && item.ShihabSelectedTorrentFiles.Count > 0)
            {
                selectedIdx = new HashSet<int>(item.ShihabSelectedTorrentFiles);
            }
            for (int i = 0; i < meta.Files.Count; i++)
            {
                if (meta.Files[i].IsPaddingFile) meta.Files[i].Selected = false;
                else if (selectedIdx != null && selectedIdx.Count > 0) meta.Files[i].Selected = selectedIdx.Contains(meta.Files[i].Index);
            }

            // Compute needed pieces & total selected bytes
            bool[] pieceNeeded = new bool[meta.PieceCount];
            bool[] pieceDone = new bool[meta.PieceCount];
            bool[] pieceInFlight = new bool[meta.PieceCount];
            long selectedBytesTotal = 0;
            for (int f = 0; f < meta.Files.Count; f++)
            {
                ShihabTorrentFileEntry fe = meta.Files[f];
                if (!fe.Selected || fe.SizeBytes <= 0) continue;
                selectedBytesTotal += fe.SizeBytes;
                int firstP = (int)(fe.ByteOffsetInTorrent / meta.PieceLength);
                int lastP = (int)((fe.ByteOffsetInTorrent + fe.SizeBytes - 1) / meta.PieceLength);
                for (int p = Math.Max(0, firstP); p <= Math.Min(meta.PieceCount - 1, lastP); p++)
                {
                    pieceNeeded[p] = true;
                }
            }

            item.ShihabTotalBytes = selectedBytesTotal > 0 ? selectedBytesTotal : meta.TotalSizeBytes;
            string rootSaveDir = meta.IsMultiFile
                ? Path.Combine(item.MehediSaveDirectory, ShihabFormatHelpers.SanitizeShihabFileName(meta.Name))
                : item.MehediSaveDirectory;
            Directory.CreateDirectory(rootSaveDir);

            // Load persisted bitfield state if resuming
            string stateFile = Path.Combine(rootSaveDir, "." + meta.InfoHashHex.Substring(0, Math.Min(16, meta.InfoHashHex.Length)) + ".mhs.btstate");
            long verifiedBytesSoFar = 0;
            if (File.Exists(stateFile))
            {
                try
                {
                    string stateBits = File.ReadAllText(stateFile, Encoding.ASCII).Trim();
                    for (int p = 0; p < Math.Min(stateBits.Length, meta.PieceCount); p++)
                    {
                        if (stateBits[p] == '1' && pieceNeeded[p])
                        {
                            pieceDone[p] = true;
                            verifiedBytesSoFar += GetPieceSize(meta, p);
                        }
                    }
                }
                catch { }
            }
            item.MehediDownloadedBytes = Math.Min(item.ShihabTotalBytes, verifiedBytesSoFar);

            if (peers.Count == 0 && meta.WebSeeds.Count == 0)
            {
                errorMessage = "No active peers or WebSeeds found for this torrent.";
                return false;
            }

            object pieceLock = new object();
            object diskWriteLock = new object();
            long windowBytes = 0;
            Stopwatch speedSw = Stopwatch.StartNew();
            int activePeers = 0;
            bool sequential = item.ShihabTorrentSequential || (cfg != null && cfg.ShihabTorrentSequentialDefault);

            int workerCount = Math.Min(8, Math.Max(1, peers.Count + meta.WebSeeds.Count));
            List<Task> workers = new List<Task>();

            // WebSeed HTTP Range workers (BEP-19) when single-file WebSeed is present
            for (int w = 0; w < meta.WebSeeds.Count && !meta.IsMultiFile; w++)
            {
                string wsUrl = meta.WebSeeds[w];
                workers.Add(Task.Run(delegate
                {
                    DownloadPiecesFromWebSeed(wsUrl, meta, cfg, pieceNeeded, pieceDone, pieceInFlight, pieceLock, diskWriteLock, rootSaveDir, stateFile,
                        delegate(int gotLen)
                        {
                            Interlocked.Add(ref windowBytes, gotLen);
                            UpdateTorrentProgressUi(item, meta, pieceNeeded, pieceDone, pieceLock, ref windowBytes, speedSw, Math.Max(1, activePeers), onProgressChanged);
                        }, cancelToken);
                }));
            }

            // TCP Peer-Wire workers (BEP-3)
            for (int pIdx = 0; pIdx < Math.Min(peers.Count, 12); pIdx++)
            {
                IPEndPoint peerEp = peers[pIdx];
                workers.Add(Task.Run(delegate
                {
                    DownloadPiecesFromTcpPeer(peerEp, meta, peerId, cfg, sequential, pieceNeeded, pieceDone, pieceInFlight, pieceLock, diskWriteLock,
                        rootSaveDir, stateFile, ref activePeers,
                        delegate(int gotLen)
                        {
                            Interlocked.Add(ref windowBytes, gotLen);
                            UpdateTorrentProgressUi(item, meta, pieceNeeded, pieceDone, pieceLock, ref windowBytes, speedSw, activePeers, onProgressChanged);
                        }, cancelToken);
                }));
            }

            try { Task.WaitAll(workers.ToArray()); } catch { }
            cancelToken.ThrowIfCancellationRequested();

            bool allDone = true;
            lock (pieceLock)
            {
                for (int p = 0; p < meta.PieceCount; p++)
                {
                    if (pieceNeeded[p] && !pieceDone[p]) { allDone = false; break; }
                }
            }

            if (!allDone)
            {
                errorMessage = "Incomplete torrent transfer: all peers disconnected before remaining pieces finished (progress saved for resume).";
                return false;
            }

            try { if (File.Exists(stateFile)) File.Delete(stateFile); } catch { }
            item.MehediDownloadedBytes = item.ShihabTotalBytes;
            item.ShihabCurrentSpeedBps = 0;
            if (meta.IsMultiFile) item.ShihabFileName = ShihabFormatHelpers.SanitizeShihabFileName(meta.Name);
            else if (meta.Files.Count == 1) item.ShihabFileName = ShihabFormatHelpers.SanitizeShihabFileName(meta.Files[0].RelativePath);
            return true;
        }

        private static void UpdateTorrentProgressUi(
            ShihabDownloadItem item,
            ShihabTorrentMetadata meta,
            bool[] needed,
            bool[] done,
            object pieceLock,
            ref long windowBytes,
            Stopwatch sw,
            int activePeers,
            Action onProgress)
        {
            long elapsedMs = sw.ElapsedMilliseconds;
            if (elapsedMs < 350) return;
            long bytesDelta = Interlocked.Exchange(ref windowBytes, 0);
            sw.Restart();

            int doneCount = 0;
            int needCount = 0;
            long doneBytes = 0;
            lock (pieceLock)
            {
                for (int p = 0; p < meta.PieceCount; p++)
                {
                    if (!needed[p]) continue;
                    needCount++;
                    if (done[p])
                    {
                        doneCount++;
                        doneBytes += GetPieceSize(meta, p);
                    }
                }
            }
            item.MehediDownloadedBytes = Math.Min(item.ShihabTotalBytes, doneBytes);
            item.ShihabCurrentSpeedBps = elapsedMs > 0 ? (bytesDelta * 1000.0 / elapsedMs) : 0;
            item.ShihabStatusText = string.Format("Downloading ({0}/{1} pcs, {2} peers)", doneCount, needCount, activePeers);
            if (onProgress != null) onProgress();
        }

        private static int GetPieceSize(ShihabTorrentMetadata meta, int pieceIndex)
        {
            long start = (long)pieceIndex * meta.PieceLength;
            long rem = meta.TotalSizeBytes - start;
            return (int)Math.Max(0L, Math.Min(meta.PieceLength, rem));
        }

        private static bool VerifyAndWritePieceToDisk(
            ShihabTorrentMetadata meta,
            int pieceIndex,
            byte[] pieceData,
            string rootSaveDir,
            string stateFile,
            bool[] pieceDone,
            object pieceLock,
            object diskWriteLock)
        {
            if (pieceData == null) return false;
            if (meta.PiecesSha1Concat != null && (pieceIndex + 1) * 20 <= meta.PiecesSha1Concat.Length)
            {
                using (SHA1 sha1 = SHA1.Create())
                {
                    byte[] actual = sha1.ComputeHash(pieceData);
                    for (int b = 0; b < 20; b++)
                    {
                        if (actual[b] != meta.PiecesSha1Concat[pieceIndex * 20 + b])
                        {
                            return false;
                        }
                    }
                }
            }

            long pieceGlobalStart = (long)pieceIndex * meta.PieceLength;
            long pieceGlobalEnd = pieceGlobalStart + pieceData.Length;

            lock (diskWriteLock)
            {
                for (int f = 0; f < meta.Files.Count; f++)
                {
                    ShihabTorrentFileEntry fe = meta.Files[f];
                    if (!fe.Selected || fe.IsPaddingFile || fe.SizeBytes <= 0) continue;
                    long fStart = fe.ByteOffsetInTorrent;
                    long fEnd = fStart + fe.SizeBytes;
                    long ovStart = Math.Max(pieceGlobalStart, fStart);
                    long ovEnd = Math.Min(pieceGlobalEnd, fEnd);
                    if (ovEnd <= ovStart) continue;

                    string fullFilePath = Path.GetFullPath(Path.Combine(rootSaveDir, fe.RelativePath));
                    if (!fullFilePath.StartsWith(Path.GetFullPath(rootSaveDir), StringComparison.OrdinalIgnoreCase))
                    {
                        throw new InvalidOperationException("Blocked torrent path traversal: " + fe.RelativePath);
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(fullFilePath));
                    using (FileStream fs = new FileStream(fullFilePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.ReadWrite))
                    {
                        if (fs.Length == 0 && fe.SizeBytes > 65536)
                        {
                            ShihabOsAndDiskSafety.TryMarkFileSparse(fs);
                        }
                        fs.Seek(ovStart - fStart, SeekOrigin.Begin);
                        fs.Write(pieceData, (int)(ovStart - pieceGlobalStart), (int)(ovEnd - ovStart));
                    }
                }

                lock (pieceLock)
                {
                    pieceDone[pieceIndex] = true;
                    char[] chars = new char[meta.PieceCount];
                    for (int i = 0; i < meta.PieceCount; i++) chars[i] = pieceDone[i] ? '1' : '0';
                    try { File.WriteAllText(stateFile, new string(chars), Encoding.ASCII); } catch { }
                }
            }
            return true;
        }

        private static int ClaimNextPieceIndex(
            int pieceCount,
            bool[] needed,
            bool[] done,
            bool[] inFlight,
            bool[] peerHasPiece,
            bool sequential,
            int peerSeedHint,
            object pieceLock)
        {
            lock (pieceLock)
            {
                if (sequential)
                {
                    for (int p = 0; p < pieceCount; p++)
                    {
                        if (needed[p] && !done[p] && !inFlight[p] && (peerHasPiece == null || peerHasPiece[p]))
                        {
                            inFlight[p] = true;
                            return p;
                        }
                    }
                }
                else
                {
                    int start = Math.Abs(peerSeedHint) % Math.Max(1, pieceCount);
                    for (int k = 0; k < pieceCount; k++)
                    {
                        int p = (start + k) % pieceCount;
                        if (needed[p] && !done[p] && !inFlight[p] && (peerHasPiece == null || peerHasPiece[p]))
                        {
                            inFlight[p] = true;
                            return p;
                        }
                    }
                }
                // Endgame fallback: allow helping an in-flight piece if all needed pieces are already claimed
                for (int p = 0; p < pieceCount; p++)
                {
                    if (needed[p] && !done[p] && (peerHasPiece == null || peerHasPiece[p]))
                    {
                        return p;
                    }
                }
            }
            return -1;
        }

        private static void DownloadPiecesFromWebSeed(
            string wsUrl,
            ShihabTorrentMetadata meta,
            MehediAppConfiguration cfg,
            bool[] needed,
            bool[] done,
            bool[] inFlight,
            object pieceLock,
            object diskWriteLock,
            string rootSaveDir,
            string stateFile,
            Action<int> onBytesRead,
            CancellationToken cancelToken)
        {
            while (!cancelToken.IsCancellationRequested)
            {
                int pIdx = ClaimNextPieceIndex(meta.PieceCount, needed, done, inFlight, null, true, 0, pieceLock);
                if (pIdx < 0) break;
                int pSize = GetPieceSize(meta, pIdx);
                long byteStart = (long)pIdx * meta.PieceLength;
                long byteEnd = byteStart + pSize - 1;
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(wsUrl);
                    req.AddRange(byteStart, byteEnd);
                    req.Timeout = 15000;
                    if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, wsUrl);
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    {
                        if (resp.StatusCode != HttpStatusCode.PartialContent && pIdx > 0) break;
                        byte[] buf = new byte[pSize];
                        int off = 0;
                        using (Stream s = resp.GetResponseStream())
                        {
                            while (off < pSize)
                            {
                                cancelToken.ThrowIfCancellationRequested();
                                int r = s.Read(buf, off, pSize - off);
                                if (r <= 0) break;
                                off += r;
                                if (onBytesRead != null) onBytesRead(r);
                            }
                        }
                        if (off == pSize && VerifyAndWritePieceToDisk(meta, pIdx, buf, rootSaveDir, stateFile, done, pieceLock, diskWriteLock))
                        {
                            continue;
                        }
                    }
                }
                catch { }
                lock (pieceLock) { inFlight[pIdx] = false; }
                break;
            }
        }

        private static void DownloadPiecesFromTcpPeer(
            IPEndPoint peerEp,
            ShihabTorrentMetadata meta,
            byte[] peerId20,
            MehediAppConfiguration cfg,
            bool sequential,
            bool[] needed,
            bool[] done,
            bool[] inFlight,
            object pieceLock,
            object diskWriteLock,
            string rootSaveDir,
            string stateFile,
            ref int activePeersCounter,
            Action<int> onBytesRead,
            CancellationToken cancelToken)
        {
            if (IsPeerBlocked(peerEp.Address)) return;
            Socket sock = new Socket(peerEp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            bool countedActive = false;
            int claimedPiece = -1;
            try
            {
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter))
                {
                    IPAddress boundIp = ShihabDnsAndNetworkManager.GetBoundAdapterLocalIp(cfg.ShihabBoundNetworkAdapter);
                    if (boundIp == null) return;
                    sock.Bind(new IPEndPoint(boundIp, 0));
                }
                sock.ReceiveTimeout = 12000;
                sock.SendTimeout = 10000;
                IAsyncResult ar = sock.BeginConnect(peerEp, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(5000)) { sock.Close(); return; }
                sock.EndConnect(ar);

                using (NetworkStream ns = new NetworkStream(sock, true))
                using (cancelToken.Register(delegate { try { sock.Close(); } catch { } }))
                {
                    // BEP-3 68-byte Handshake
                    byte[] hs = BuildBitTorrentHandshake(meta.InfoHashBytes, peerId20, false);
                    ns.Write(hs, 0, 68);
                    byte[] peerHs = ReadExactStream(ns, 68);
                    if (peerHs[0] != 19) return;
                    for (int i = 0; i < 20; i++)
                    {
                        if (peerHs[28 + i] != meta.InfoHashBytes[i]) return;
                    }

                    Interlocked.Increment(ref activePeersCounter);
                    countedActive = true;

                    bool[] peerHas = new bool[meta.PieceCount];
                    bool peerChoking = true;

                    // Send Interested (id = 2)
                    ns.Write(new byte[] { 0, 0, 0, 1, 2 }, 0, 5);

                    byte[] currentPieceBuf = null;
                    int currentPieceSize = 0;
                    int currentPieceReceived = 0;
                    int nextRequestOffset = 0;
                    int outstandingBlocks = 0;

                    while (!cancelToken.IsCancellationRequested)
                    {
                        if (!peerChoking)
                        {
                            if (claimedPiece < 0)
                            {
                                claimedPiece = ClaimNextPieceIndex(meta.PieceCount, needed, done, inFlight, peerHas, sequential, peerEp.Port, pieceLock);
                                if (claimedPiece < 0) break;
                                currentPieceSize = GetPieceSize(meta, claimedPiece);
                                currentPieceBuf = new byte[currentPieceSize];
                                currentPieceReceived = 0;
                                nextRequestOffset = 0;
                                outstandingBlocks = 0;
                            }

                            while (outstandingBlocks < 8 && nextRequestOffset < currentPieceSize)
                            {
                                int blkLen = Math.Min(16384, currentPieceSize - nextRequestOffset);
                                byte[] reqMsg = new byte[17];
                                WriteBeUInt32(reqMsg, 0, 13u);
                                reqMsg[4] = 6; // request
                                WriteBeUInt32(reqMsg, 5, (uint)claimedPiece);
                                WriteBeUInt32(reqMsg, 9, (uint)nextRequestOffset);
                                WriteBeUInt32(reqMsg, 13, (uint)blkLen);
                                ns.Write(reqMsg, 0, 17);
                                nextRequestOffset += blkLen;
                                outstandingBlocks++;
                            }
                        }

                        byte[] lenBytes = ReadExactStream(ns, 4);
                        uint msgLen = ReadBeUInt32(lenBytes, 0);
                        if (msgLen == 0) continue; // keep-alive
                        if (msgLen > 2 * 1024 * 1024) break;

                        byte[] payload = ReadExactStream(ns, (int)msgLen);
                        byte msgId = payload[0];

                        if (msgId == 0) peerChoking = true;
                        else if (msgId == 1) peerChoking = false;
                        else if (msgId == 4 && payload.Length >= 5) // have
                        {
                            int p = (int)ReadBeUInt32(payload, 1);
                            if (p >= 0 && p < peerHas.Length) peerHas[p] = true;
                        }
                        else if (msgId == 5) // bitfield
                        {
                            for (int p = 0; p < peerHas.Length; p++)
                            {
                                int byteIdx = 1 + (p >> 3);
                                if (byteIdx < payload.Length && ((payload[byteIdx] >> (7 - (p & 7))) & 1) != 0)
                                {
                                    peerHas[p] = true;
                                }
                            }
                        }
                        else if (msgId == 7 && payload.Length > 9) // piece
                        {
                            int pIndex = (int)ReadBeUInt32(payload, 1);
                            int begin = (int)ReadBeUInt32(payload, 5);
                            int blkDataLen = payload.Length - 9;
                            if (outstandingBlocks > 0) outstandingBlocks--;
                            if (pIndex == claimedPiece && currentPieceBuf != null && begin >= 0 && begin + blkDataLen <= currentPieceSize)
                            {
                                Buffer.BlockCopy(payload, 9, currentPieceBuf, begin, blkDataLen);
                                currentPieceReceived += blkDataLen;
                                if (onBytesRead != null) onBytesRead(blkDataLen);

                                if (currentPieceReceived >= currentPieceSize)
                                {
                                    if (!VerifyAndWritePieceToDisk(meta, claimedPiece, currentPieceBuf, rootSaveDir, stateFile, done, pieceLock, diskWriteLock))
                                    {
                                        lock (pieceLock) { inFlight[claimedPiece] = false; }
                                    }
                                    claimedPiece = -1;
                                    currentPieceBuf = null;
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            finally
            {
                if (claimedPiece >= 0)
                {
                    lock (pieceLock) { if (!done[claimedPiece]) inFlight[claimedPiece] = false; }
                }
                if (countedActive) Interlocked.Decrement(ref activePeersCounter);
                try { sock.Close(); } catch { }
            }
        }

        private static byte[] TryFetchMagnetMetadataFromPeer(
            IPEndPoint peerEp,
            byte[] infoHash20,
            byte[] peerId20,
            MehediAppConfiguration cfg,
            CancellationToken cancelToken)
        {
            if (IsPeerBlocked(peerEp.Address)) return null;
            Socket sock = new Socket(peerEp.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            try
            {
                sock.ReceiveTimeout = 6000;
                sock.SendTimeout = 6000;
                IAsyncResult ar = sock.BeginConnect(peerEp, null, null);
                if (!ar.AsyncWaitHandle.WaitOne(3500)) { sock.Close(); return null; }
                sock.EndConnect(ar);

                using (NetworkStream ns = new NetworkStream(sock, true))
                {
                    byte[] hs = BuildBitTorrentHandshake(infoHash20, peerId20, true);
                    ns.Write(hs, 0, 68);
                    byte[] peerHs = ReadExactStream(ns, 68);
                    if (peerHs[0] != 19 || (peerHs[25] & 0x10) == 0) return null; // peer must support BEP-10 extension protocol

                    // Send BEP-10 extended handshake (msgId = 20, extId = 0): d1:md11:ut_metadatai2eee
                    byte[] extHsDict = Encoding.ASCII.GetBytes("d1:md11:ut_metadatai2eee");
                    byte[] extHsMsg = new byte[6 + extHsDict.Length];
                    WriteBeUInt32(extHsMsg, 0, (uint)(2 + extHsDict.Length));
                    extHsMsg[4] = 20;
                    extHsMsg[5] = 0;
                    Buffer.BlockCopy(extHsDict, 0, extHsMsg, 6, extHsDict.Length);
                    ns.Write(extHsMsg, 0, extHsMsg.Length);

                    int peerUtMetadataId = 0;
                    int metadataSize = 0;
                    byte[] assembled = null;
                    int nextMetaPiece = 0;
                    int totalMetaPieces = 0;

                    for (int step = 0; step < 64 && !cancelToken.IsCancellationRequested; step++)
                    {
                        byte[] lb = ReadExactStream(ns, 4);
                        uint mLen = ReadBeUInt32(lb, 0);
                        if (mLen == 0) continue;
                        if (mLen > 2 * 1024 * 1024) return null;
                        byte[] body = ReadExactStream(ns, (int)mLen);
                        if (body[0] != 20 || body.Length < 2) continue;

                        byte extId = body[1];
                        if (extId == 0)
                        {
                            int pos = 2;
                            ShihabBencodeNode d = ParseBencodeValue(body, ref pos);
                            if (d != null && d.DictValue != null)
                            {
                                ShihabBencodeNode mNode, szNode;
                                if (d.DictValue.TryGetValue("m", out mNode) && mNode != null && mNode.DictValue != null)
                                {
                                    ShihabBencodeNode utNode;
                                    if (mNode.DictValue.TryGetValue("ut_metadata", out utNode) && utNode != null)
                                        peerUtMetadataId = (int)utNode.IntValue;
                                }
                                if (d.DictValue.TryGetValue("metadata_size", out szNode) && szNode != null)
                                    metadataSize = (int)szNode.IntValue;
                            }
                            if (peerUtMetadataId > 0 && metadataSize > 0 && metadataSize <= 4 * 1024 * 1024)
                            {
                                assembled = new byte[metadataSize];
                                totalMetaPieces = (metadataSize + 16383) / 16384;
                                SendUtMetadataRequest(ns, (byte)peerUtMetadataId, 0);
                                nextMetaPiece = 1;
                            }
                        }
                        else if (extId == 2 && assembled != null)
                        {
                            int pos = 2;
                            ShihabBencodeNode hdr = ParseBencodeValue(body, ref pos);
                            if (hdr != null && hdr.DictValue != null)
                            {
                                ShihabBencodeNode mtNode, pcNode;
                                if (hdr.DictValue.TryGetValue("msg_type", out mtNode) && mtNode != null && mtNode.IntValue == 1 &&
                                    hdr.DictValue.TryGetValue("piece", out pcNode) && pcNode != null)
                                {
                                    int p = (int)pcNode.IntValue;
                                    int dataBytes = body.Length - pos;
                                    int dstOff = p * 16384;
                                    if (dataBytes > 0 && dstOff >= 0 && dstOff + dataBytes <= assembled.Length)
                                    {
                                        Buffer.BlockCopy(body, pos, assembled, dstOff, dataBytes);
                                    }
                                    if (nextMetaPiece < totalMetaPieces)
                                    {
                                        SendUtMetadataRequest(ns, (byte)peerUtMetadataId, nextMetaPiece++);
                                    }
                                    else
                                    {
                                        return assembled;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }
            finally { try { sock.Close(); } catch { } }
            return null;
        }

        private static void SendUtMetadataRequest(Stream ns, byte peerUtId, int pieceIdx)
        {
            byte[] dict = Encoding.ASCII.GetBytes(string.Format(CultureInfo.InvariantCulture, "d8:msg_typei0e5:piecei{0}ee", pieceIdx));
            byte[] pkt = new byte[6 + dict.Length];
            WriteBeUInt32(pkt, 0, (uint)(2 + dict.Length));
            pkt[4] = 20;
            pkt[5] = peerUtId;
            Buffer.BlockCopy(dict, 0, pkt, 6, dict.Length);
            ns.Write(pkt, 0, pkt.Length);
        }

        private static byte[] BuildBitTorrentHandshake(byte[] infoHash20, byte[] peerId20, bool advertiseExtendedBep10)
        {
            byte[] hs = new byte[68];
            hs[0] = 19;
            byte[] proto = Encoding.ASCII.GetBytes("BitTorrent protocol");
            Buffer.BlockCopy(proto, 0, hs, 1, 19);
            if (advertiseExtendedBep10) hs[25] = 0x10;
            Buffer.BlockCopy(infoHash20, 0, hs, 28, 20);
            Buffer.BlockCopy(peerId20, 0, hs, 48, 20);
            return hs;
        }

        private static byte[] ReadExactStream(Stream s, int count)
        {
            byte[] b = new byte[count];
            int off = 0;
            while (off < count)
            {
                int r = s.Read(b, off, count - off);
                if (r <= 0) throw new EndOfStreamException();
                off += r;
            }
            return b;
        }

        private static void WriteBeUInt32(byte[] buf, int off, uint v)
        {
            buf[off] = (byte)((v >> 24) & 0xFF);
            buf[off + 1] = (byte)((v >> 16) & 0xFF);
            buf[off + 2] = (byte)((v >> 8) & 0xFF);
            buf[off + 3] = (byte)(v & 0xFF);
        }

        private static void WriteBeUInt64(byte[] buf, int off, ulong v)
        {
            WriteBeUInt32(buf, off, (uint)(v >> 32));
            WriteBeUInt32(buf, off + 4, (uint)(v & 0xFFFFFFFFUL));
        }

        private static uint ReadBeUInt32(byte[] buf, int off)
        {
            return ((uint)buf[off] << 24) | ((uint)buf[off + 1] << 16) | ((uint)buf[off + 2] << 8) | buf[off + 3];
        }

        private static ulong ReadBeUInt64(byte[] buf, int off)
        {
            return ((ulong)ReadBeUInt32(buf, off) << 32) | ReadBeUInt32(buf, off + 4);
        }

        public static bool CreateTorrentFile(
            string sourceFileOrDir,
            string outputTorrentPath,
            List<string> trackers,
            List<string> webSeeds,
            bool isPrivate,
            int pieceLengthBytes,
            out string infoHashHex,
            out string errorMessage)
        {
            infoHashHex = "";
            errorMessage = "";
            try
            {
                CreateTorrentFile(sourceFileOrDir, outputTorrentPath, trackers, webSeeds, "Created by MHS Download Manager", pieceLengthBytes, isPrivate);
                ShihabTorrentMetadata meta = InspectTorrentFile(outputTorrentPath);
                infoHashHex = meta != null ? (meta.InfoHashHex ?? "") : "";
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool CreateTorrentFile(
            string sourceFileOrDir,
            string outputTorrentPath,
            List<string> trackers,
            List<string> webSeeds,
            string comment,
            int pieceLengthBytes,
            bool isPrivate,
            out string errorMessage)
        {
            errorMessage = "";
            try
            {
                CreateTorrentFile(sourceFileOrDir, outputTorrentPath, trackers, webSeeds, comment, pieceLengthBytes, isPrivate);
                return true;
            }
            catch (Exception ex)
            {
                errorMessage = ex.Message;
                return false;
            }
        }

        public static void CreateTorrentFile(
            string sourceFileOrDir,
            string outputTorrentPath,
            List<string> trackers,
            List<string> webSeeds,
            string comment,
            int pieceLengthBytes,
            bool isPrivate)
        {
            if (pieceLengthBytes < 16384) pieceLengthBytes = 262144; // 256 KiB default
            bool isDir = Directory.Exists(sourceFileOrDir);
            List<string> fullFiles = new List<string>();
            string baseRoot = sourceFileOrDir;
            string torrentName = Path.GetFileName(sourceFileOrDir.TrimEnd(Path.DirectorySeparatorChar));

            if (isDir)
            {
                string[] all = Directory.GetFiles(sourceFileOrDir, "*", System.IO.SearchOption.AllDirectories);
                Array.Sort(all, StringComparer.OrdinalIgnoreCase);
                fullFiles.AddRange(all);
            }
            else if (File.Exists(sourceFileOrDir))
            {
                fullFiles.Add(sourceFileOrDir);
                baseRoot = Path.GetDirectoryName(sourceFileOrDir);
            }
            else
            {
                throw new FileNotFoundException("Source path for torrent creation was not found.", sourceFileOrDir);
            }

            MemoryStream piecesHashes = new MemoryStream();
            byte[] pieceBuf = new byte[pieceLengthBytes];
            int pieceFill = 0;
            using (SHA1 sha1 = SHA1.Create())
            {
                for (int i = 0; i < fullFiles.Count; i++)
                {
                    using (FileStream fs = new FileStream(fullFiles[i], FileMode.Open, FileAccess.Read, FileShare.Read))
                    {
                        int read;
                        while ((read = fs.Read(pieceBuf, pieceFill, pieceLengthBytes - pieceFill)) > 0)
                        {
                            pieceFill += read;
                            if (pieceFill == pieceLengthBytes)
                            {
                                byte[] h = sha1.ComputeHash(pieceBuf, 0, pieceLengthBytes);
                                piecesHashes.Write(h, 0, 20);
                                pieceFill = 0;
                            }
                        }
                    }
                }
                if (pieceFill > 0)
                {
                    byte[] h = sha1.ComputeHash(pieceBuf, 0, pieceFill);
                    piecesHashes.Write(h, 0, 20);
                }
            }

            using (FileStream outFs = new FileStream(outputTorrentPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                WriteAscii(outFs, "d");
                if (trackers != null && trackers.Count > 0 && !string.IsNullOrWhiteSpace(trackers[0]))
                {
                    WriteBencodeKeyString(outFs, "announce", trackers[0].Trim());
                    WriteBencodeString(outFs, "announce-list");
                    WriteAscii(outFs, "l");
                    for (int i = 0; i < trackers.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(trackers[i])) continue;
                        WriteAscii(outFs, "l");
                        WriteBencodeString(outFs, trackers[i].Trim());
                        WriteAscii(outFs, "e");
                    }
                    WriteAscii(outFs, "e");
                }
                if (!string.IsNullOrWhiteSpace(comment))
                {
                    WriteBencodeKeyString(outFs, "comment", comment.Trim());
                }
                WriteBencodeKeyString(outFs, "created by", "MHS Download Manager v1.1");
                long unixNow = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                WriteBencodeString(outFs, "creation date");
                WriteBencodeInt(outFs, unixNow);

                // info dictionary (keys sorted lexicographically: files/length, name, piece length, pieces, private)
                WriteBencodeString(outFs, "info");
                WriteAscii(outFs, "d");
                if (isDir)
                {
                    WriteBencodeString(outFs, "files");
                    WriteAscii(outFs, "l");
                    for (int i = 0; i < fullFiles.Count; i++)
                    {
                        FileInfo fi = new FileInfo(fullFiles[i]);
                        WriteAscii(outFs, "d");
                        WriteBencodeString(outFs, "length");
                        WriteBencodeInt(outFs, fi.Length);
                        WriteBencodeString(outFs, "path");
                        WriteAscii(outFs, "l");
                        string rel = fullFiles[i].Substring(sourceFileOrDir.TrimEnd(Path.DirectorySeparatorChar).Length + 1);
                        string[] segs = rel.Split(new char[] { Path.DirectorySeparatorChar, '/' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int s = 0; s < segs.Length; s++)
                        {
                            WriteBencodeString(outFs, segs[s]);
                        }
                        WriteAscii(outFs, "ee");
                    }
                    WriteAscii(outFs, "e");
                }
                else
                {
                    FileInfo fi = new FileInfo(fullFiles[0]);
                    WriteBencodeString(outFs, "length");
                    WriteBencodeInt(outFs, fi.Length);
                }

                WriteBencodeKeyString(outFs, "name", torrentName);
                WriteBencodeString(outFs, "piece length");
                WriteBencodeInt(outFs, pieceLengthBytes);
                WriteBencodeString(outFs, "pieces");
                byte[] allHashes = piecesHashes.ToArray();
                WriteAscii(outFs, allHashes.Length.ToString(CultureInfo.InvariantCulture) + ":");
                outFs.Write(allHashes, 0, allHashes.Length);
                if (isPrivate)
                {
                    WriteBencodeString(outFs, "private");
                    WriteBencodeInt(outFs, 1);
                }
                WriteAscii(outFs, "e"); // end info

                if (webSeeds != null && webSeeds.Count > 0)
                {
                    WriteBencodeString(outFs, "url-list");
                    WriteAscii(outFs, "l");
                    for (int i = 0; i < webSeeds.Count; i++)
                    {
                        if (!string.IsNullOrWhiteSpace(webSeeds[i]))
                        {
                            WriteBencodeString(outFs, webSeeds[i].Trim());
                        }
                    }
                    WriteAscii(outFs, "e");
                }
                WriteAscii(outFs, "e"); // end root
            }
        }

        private static void WriteAscii(Stream s, string txt)
        {
            byte[] b = Encoding.ASCII.GetBytes(txt);
            s.Write(b, 0, b.Length);
        }

        private static void WriteBencodeString(Stream s, string str)
        {
            byte[] b = Encoding.UTF8.GetBytes(str ?? "");
            WriteAscii(s, b.Length.ToString(CultureInfo.InvariantCulture) + ":");
            s.Write(b, 0, b.Length);
        }

        private static void WriteBencodeKeyString(Stream s, string key, string val)
        {
            WriteBencodeString(s, key);
            WriteBencodeString(s, val);
        }

        private static void WriteBencodeInt(Stream s, long val)
        {
            WriteAscii(s, "i" + val.ToString(CultureInfo.InvariantCulture) + "e");
        }

        private static ShihabBencodeNode ParseBencodeValue(byte[] data, ref int pos)
        {
            if (data == null || pos >= data.Length) return null;
            int startOffset = pos;
            byte c = data[pos];
            if (c == (byte)'i')
            {
                pos++;
                int end = pos;
                while (end < data.Length && data[end] != (byte)'e') end++;
                string numStr = Encoding.ASCII.GetString(data, pos, end - pos);
                pos = end + 1;
                long v = 0;
                long.TryParse(numStr, out v);
                return new ShihabBencodeNode { Kind = "int", IntValue = v, RawStartOffset = startOffset, RawEndOffset = pos };
            }
            if (c == (byte)'l')
            {
                pos++;
                List<ShihabBencodeNode> list = new List<ShihabBencodeNode>();
                while (pos < data.Length && data[pos] != (byte)'e')
                {
                    ShihabBencodeNode child = ParseBencodeValue(data, ref pos);
                    if (child == null) break;
                    list.Add(child);
                }
                if (pos < data.Length && data[pos] == (byte)'e') pos++;
                return new ShihabBencodeNode { Kind = "list", ListValue = list, RawStartOffset = startOffset, RawEndOffset = pos };
            }
            if (c == (byte)'d')
            {
                pos++;
                Dictionary<string, ShihabBencodeNode> dict = new Dictionary<string, ShihabBencodeNode>(StringComparer.Ordinal);
                while (pos < data.Length && data[pos] != (byte)'e')
                {
                    ShihabBencodeNode kNode = ParseBencodeValue(data, ref pos);
                    if (kNode == null) break;
                    ShihabBencodeNode vNode = ParseBencodeValue(data, ref pos);
                    dict[kNode.GetUtf8String()] = vNode;
                }
                if (pos < data.Length && data[pos] == (byte)'e') pos++;
                return new ShihabBencodeNode { Kind = "dict", DictValue = dict, RawStartOffset = startOffset, RawEndOffset = pos };
            }
            if (c >= (byte)'0' && c <= (byte)'9')
            {
                int colon = pos;
                while (colon < data.Length && data[colon] != (byte)':') colon++;
                string lenStr = Encoding.ASCII.GetString(data, pos, colon - pos);
                int len = 0;
                int.TryParse(lenStr, out len);
                pos = colon + 1;
                if (len < 0 || pos + len > data.Length) len = Math.Max(0, data.Length - pos);
                byte[] bytes = new byte[len];
                Buffer.BlockCopy(data, pos, bytes, 0, len);
                pos += len;
                return new ShihabBencodeNode { Kind = "bytes", BytesValue = bytes, RawStartOffset = startOffset, RawEndOffset = pos };
            }
            pos++;
            return null;
        }
    }

    // =========================================================================
    // 8. MANAGED MEDIA HELPERS (yt-dlp, ffmpeg, gallery-dl) & SUBPROCESS PROXY
    // =========================================================================
    public class ShihabMediaFormatEntry
    {
        public string FormatId { get; set; }
        public string ResolutionLabel { get; set; }
        public string Extension { get; set; }
        public long FileSizeBytes { get; set; }
        public string DirectUrl { get; set; }
        public bool HasVideo { get; set; }
        public bool HasAudio { get; set; }

        public override string ToString()
        {
            return string.Format("{0} ({1}) - {2}",
                string.IsNullOrEmpty(ResolutionLabel) ? FormatId : ResolutionLabel,
                string.IsNullOrEmpty(Extension) ? "mp4" : Extension.ToUpperInvariant(),
                FileSizeBytes > 0 ? ShihabFormatHelpers.FormatMehediBytes(FileSizeBytes) : "Stream");
        }
    }

    public class ShihabPlaylistEntryInfo
    {
        public int Index { get; set; }
        public string Title { get; set; }
        public string Url { get; set; }
        public string DurationText { get; set; }
        public bool Selected { get; set; }
    }

    public static class ShihabMediaHelpersManager
    {
        public const string YtDlpDownloadUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe";
        public const string YtDlpSha256SumsUrl = "https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS";
        public const string GalleryDlDownloadUrl = "https://github.com/mikf/gallery-dl/releases/latest/download/gallery-dl.exe";
        public const string GalleryDlReleaseApiUrl = "https://api.github.com/repos/mikf/gallery-dl/releases/latest";
        public const string FfmpegDownloadUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/ffmpeg-master-latest-win64-gpl.zip";
        public const string FfmpegChecksumsUrl = "https://github.com/BtbN/FFmpeg-Builds/releases/download/latest/checksums.sha256";

        public static string GetToolsDirectory(string baseDataDir)
        {
            string dir = Path.Combine(baseDataDir, "tools");
            Directory.CreateDirectory(dir);
            return dir;
        }

        public static string GetHelperExecutablePath(string baseDataDir, string toolName)
        {
            return GetToolExecutablePath(baseDataDir, toolName);
        }

        public static void InstallOrUpdateHelperAsync(
            string baseDataDir,
            string toolKey,
            Action<string> onProgressStatus)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                string err;
                if (!InstallOrUpdateTool(baseDataDir, toolKey, onProgressStatus, out err) && onProgressStatus != null)
                {
                    onProgressStatus(err);
                }
            });
        }

        public static List<string> ExtractGalleryImageUrls(string pageUrl, string userAgent)
        {
            string title;
            string appData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShihabMehediIDM");
            return ExtractGalleryImageUrls(pageUrl, appData, out title);
        }

        public static string GetToolExecutablePath(string baseDataDir, string toolName)
        {
            string toolsDir = GetToolsDirectory(baseDataDir);
            string exeName = toolName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? toolName : toolName + ".exe";
            string localPath = Path.Combine(toolsDir, exeName);
            if (File.Exists(localPath)) return localPath;

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] dirs = pathEnv.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    string cand = Path.Combine(dirs[i].Trim(), exeName);
                    if (File.Exists(cand)) return cand;
                }
                catch { }
            }
            return localPath;
        }

        public static bool IsToolInstalled(string baseDataDir, string toolName, out string versionOrPath)
        {
            versionOrPath = "";
            string path = GetToolExecutablePath(baseDataDir, toolName);
            if (!File.Exists(path)) return false;
            versionOrPath = path;
            return true;
        }

        public static bool InstallOrUpdateTool(
            string baseDataDir,
            string toolKey,
            Action<string> onProgressStatus,
            out string errorMessage)
        {
            errorMessage = "";
            string toolsDir = GetToolsDirectory(baseDataDir);
            try
            {
                ShihabAutoUpdater.EnforceModernTls();
                string key = (toolKey ?? "").Trim().ToLowerInvariant();
                if (key == "yt-dlp" || key == "ytdlp")
                {
                    if (onProgressStatus != null) onProgressStatus("Downloading yt-dlp.exe + SHA2-256SUMS from official GitHub release...");
                    string target = Path.Combine(toolsDir, "yt-dlp.exe");
                    string expectedSha = FetchExpectedSha256FromSidecar(YtDlpSha256SumsUrl, "yt-dlp.exe");
                    DownloadAndVerifyBinaryFile(YtDlpDownloadUrl, target, expectedSha);
                    if (onProgressStatus != null) onProgressStatus("yt-dlp.exe SHA-256 verified and installed at " + target);
                    return true;
                }
                if (key == "gallery-dl" || key == "gallerydl")
                {
                    if (onProgressStatus != null) onProgressStatus("Downloading gallery-dl.exe from official GitHub release...");
                    string target = Path.Combine(toolsDir, "gallery-dl.exe");
                    string expectedSha = FetchExpectedSha256FromGitHubReleaseApi(GalleryDlReleaseApiUrl, "gallery-dl.exe");
                    DownloadAndVerifyBinaryFile(GalleryDlDownloadUrl, target, expectedSha);
                    if (onProgressStatus != null) onProgressStatus("gallery-dl.exe installed at " + target);
                    return true;
                }
                if (key == "ffmpeg")
                {
                    if (onProgressStatus != null) onProgressStatus("Downloading ffmpeg-win64-gpl.zip + checksums.sha256...");
                    string zipTmp = Path.Combine(toolsDir, "ffmpeg_tmp.zip");
                    string expectedSha = FetchExpectedSha256FromSidecar(FfmpegChecksumsUrl, "ffmpeg-master-latest-win64-gpl.zip");
                    DownloadAndVerifyBinaryFile(FfmpegDownloadUrl, zipTmp, expectedSha);
                    if (onProgressStatus != null) onProgressStatus("Extracting ffmpeg.exe and ffprobe.exe...");
                    using (ZipArchive za = ZipFile.OpenRead(zipTmp))
                    {
                        foreach (ZipArchiveEntry entry in za.Entries)
                        {
                            if (entry.Name.Equals("ffmpeg.exe", StringComparison.OrdinalIgnoreCase) ||
                                entry.Name.Equals("ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                            {
                                string dest = Path.Combine(toolsDir, entry.Name.ToLowerInvariant());
                                entry.ExtractToFile(dest, true);
                            }
                        }
                    }
                    try { File.Delete(zipTmp); } catch { }
                    if (onProgressStatus != null) onProgressStatus("ffmpeg.exe verified and installed in " + toolsDir);
                    return true;
                }
                errorMessage = "Unknown helper tool: " + toolKey;
                return false;
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to install " + toolKey + ": " + ex.Message;
                return false;
            }
        }

        private static string FetchExpectedSha256FromSidecar(string sumsUrl, string assetName)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(sumsUrl);
                req.UserAgent = "MHS-Download-Manager/" + ShihabAutoUpdater.GetCurrentAppVersion();
                req.Timeout = 15000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string body = sr.ReadToEnd();
                    string[] lines = body.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    for (int i = 0; i < lines.Length; i++)
                    {
                        Match m = Regex.Match(lines[i].Trim('\uFEFF', ' ', '\t'), @"^([0-9a-fA-F]{64})\s+\*?(.+)$");
                        if (m.Success && Path.GetFileName(m.Groups[2].Value.Trim()).Equals(assetName, StringComparison.OrdinalIgnoreCase))
                        {
                            return m.Groups[1].Value.ToLowerInvariant();
                        }
                    }
                }
            }
            catch { }
            return "";
        }

        private static string FetchExpectedSha256FromGitHubReleaseApi(string releaseApiUrl, string assetName)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(releaseApiUrl);
                req.UserAgent = "MHS-Download-Manager/" + ShihabAutoUpdater.GetCurrentAppVersion();
                req.Accept = "application/vnd.github+json";
                req.Timeout = 15000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string json = sr.ReadToEnd();
                    Match m = Regex.Match(json, "\"name\"\\s*:\\s*\"" + Regex.Escape(assetName) + "\"[\\s\\S]{0,600}?\"digest\"\\s*:\\s*\"sha256:([0-9a-fA-F]{64})\"", RegexOptions.IgnoreCase);
                    if (m.Success) return m.Groups[1].Value.ToLowerInvariant();
                }
            }
            catch { }
            return "";
        }

        private static void DownloadAndVerifyBinaryFile(string url, string destinationPath, string expectedSha256Hex)
        {
            string tmp = destinationPath + ".tmp";
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.UserAgent = "MHS-Download-Manager/" + ShihabAutoUpdater.GetCurrentAppVersion();
                req.AllowAutoRedirect = true;
                req.Timeout = 45000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream inStream = resp.GetResponseStream())
                using (FileStream outFs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buf = new byte[65536];
                    int r;
                    while ((r = inStream.Read(buf, 0, buf.Length)) > 0)
                    {
                        outFs.Write(buf, 0, r);
                    }
                    outFs.Flush(true);
                }

                if (!string.IsNullOrWhiteSpace(expectedSha256Hex))
                {
                    string actualHex = ShihabChecksumVerifier.ComputeFileChecksumHex(tmp, "SHA-256");
                    if (!string.Equals(actualHex, expectedSha256Hex.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(tmp); } catch { }
                        throw new InvalidOperationException("Helper binary SHA-256 mismatch (expected " + expectedSha256Hex + ", got " + actualHex + ").");
                    }
                }

                if (File.Exists(destinationPath)) File.Delete(destinationPath);
                File.Move(tmp, destinationPath);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }

        public static bool QueryVideoOrPlaylistInfo(
            string baseDataDir,
            string targetUrl,
            bool isPlaylist,
            out string summaryJson,
            out string errorMessage)
        {
            summaryJson = "";
            string mediaTitle;
            List<ShihabMediaFormatEntry> formats;
            List<ShihabPlaylistEntryInfo> entries;
            if (!QueryVideoOrPlaylistInfo(targetUrl, baseDataDir, null, out mediaTitle, out formats, out entries, out errorMessage))
            {
                return false;
            }
            StringBuilder sb = new StringBuilder();
            sb.Append("{\"title\":\"")
              .Append((mediaTitle ?? "").Replace("\\", "\\\\").Replace("\"", "\\\""))
              .Append("\",\"isPlaylist\":")
              .Append((isPlaylist || entries.Count > 0) ? "true" : "false")
              .Append(",\"formats\":[");
            for (int i = 0; i < formats.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{{\"id\":\"{0}\",\"ext\":\"{1}\",\"resolution\":\"{2}\",\"sizeBytes\":{3},\"hasVideo\":{4},\"hasAudio\":{5}}}",
                    (formats[i].FormatId ?? "").Replace("\"", "\\\""),
                    (formats[i].Extension ?? "").Replace("\"", "\\\""),
                    (formats[i].ResolutionLabel ?? "").Replace("\"", "\\\""),
                    formats[i].FileSizeBytes,
                    formats[i].HasVideo ? "true" : "false",
                    formats[i].HasAudio ? "true" : "false");
            }
            sb.Append("],\"entries\":[");
            for (int i = 0; i < entries.Count; i++)
            {
                if (i > 0) sb.Append(",");
                sb.AppendFormat(CultureInfo.InvariantCulture,
                    "{{\"index\":{0},\"title\":\"{1}\",\"url\":\"{2}\",\"duration\":\"{3}\"}}",
                    entries[i].Index,
                    (entries[i].Title ?? "").Replace("\\", "\\\\").Replace("\"", "\\\""),
                    (entries[i].Url ?? "").Replace("\\", "\\\\").Replace("\"", "\\\""),
                    (entries[i].DurationText ?? "").Replace("\"", "\\\""));
            }
            sb.Append("]}");
            summaryJson = sb.ToString();
            return true;
        }

        public static bool QueryVideoOrPlaylistInfo(
            string targetUrl,
            string baseDataDir,
            MehediAppConfiguration cfg,
            out string mediaTitle,
            out List<ShihabMediaFormatEntry> formats,
            out List<ShihabPlaylistEntryInfo> playlistEntries,
            out string errorMessage)
        {
            mediaTitle = "";
            formats = new List<ShihabMediaFormatEntry>();
            playlistEntries = new List<ShihabPlaylistEntryInfo>();
            errorMessage = "";

            if (string.IsNullOrWhiteSpace(targetUrl))
            {
                errorMessage = "Empty media URL.";
                return false;
            }

            string ytdlpExe = GetToolExecutablePath(baseDataDir, "yt-dlp");
            if (!File.Exists(ytdlpExe))
            {
                errorMessage = "yt-dlp.exe is not installed yet (Options -> Media Helpers -> Install yt-dlp).";
                return false;
            }

            StringBuilder args = new StringBuilder();
            args.Append("--dump-single-json --no-warnings --ignore-config --flat-playlist ");
            string proxyArg = ShihabLocalNetworkRelay.GetSubprocessProxyUrl(cfg);
            if (!string.IsNullOrEmpty(proxyArg))
            {
                args.Append("--proxy \"").Append(proxyArg.Replace("\"", "")).Append("\" ");
            }
            args.Append("-- \"").Append(targetUrl.Trim().Replace("\"", "")).Append("\"");

            string stdoutAll;
            string stderrAll;
            int exitCode = ShihabWindowsJobRunner.RunManagedSubprocess(
                ytdlpExe,
                args.ToString(),
                GetToolsDirectory(baseDataDir),
                35000,
                null,
                CancellationToken.None,
                out stdoutAll,
                out stderrAll);

            if (exitCode != 0 || string.IsNullOrWhiteSpace(stdoutAll))
            {
                errorMessage = !string.IsNullOrWhiteSpace(stderrAll) ? stderrAll.Trim() : "yt-dlp returned no media metadata.";
                return false;
            }

            mediaTitle = ShihabDebridManager.ExtractJsonStringField(stdoutAll, "title");
            string rootType = ShihabDebridManager.ExtractJsonStringField(stdoutAll, "_type");

            if (string.Equals(rootType, "playlist", StringComparison.OrdinalIgnoreCase) || stdoutAll.IndexOf("\"entries\"", StringComparison.Ordinal) >= 0)
            {
                MatchCollection entryMatches = Regex.Matches(stdoutAll, @"\{[^{}]*""(?:url|id)""\s*:\s*""[^""]+""[^{}]*\}");
                int idx = 1;
                for (int i = 0; i < entryMatches.Count; i++)
                {
                    string obj = entryMatches[i].Value;
                    string eUrl = ShihabDebridManager.ExtractJsonStringField(obj, "url");
                    string eId = ShihabDebridManager.ExtractJsonStringField(obj, "id");
                    string eTitle = ShihabDebridManager.ExtractJsonStringField(obj, "title");
                    long durSec = ShihabDebridManager.ExtractJsonLongField(obj, "duration");
                    if (string.IsNullOrEmpty(eUrl) && !string.IsNullOrEmpty(eId) && targetUrl.IndexOf("youtube.com", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        eUrl = "https://www.youtube.com/watch?v=" + eId;
                    }
                    if (!string.IsNullOrEmpty(eUrl) && eUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        playlistEntries.Add(new ShihabPlaylistEntryInfo
                        {
                            Index = idx++,
                            Title = string.IsNullOrEmpty(eTitle) ? ("Video " + (idx - 1)) : eTitle,
                            Url = eUrl,
                            DurationText = durSec > 0 ? TimeSpan.FromSeconds(durSec).ToString(@"mm\:ss") : "",
                            Selected = true
                        });
                    }
                }
            }

            MatchCollection fmtMatches = Regex.Matches(stdoutAll, @"\{[^{}]*""format_id""\s*:\s*""[^""]+""[^{}]*\}");
            for (int i = 0; i < fmtMatches.Count; i++)
            {
                string fObj = fmtMatches[i].Value;
                string fid = ShihabDebridManager.ExtractJsonStringField(fObj, "format_id");
                string ext = ShihabDebridManager.ExtractJsonStringField(fObj, "ext");
                string res = ShihabDebridManager.ExtractJsonStringField(fObj, "resolution");
                string note = ShihabDebridManager.ExtractJsonStringField(fObj, "format_note");
                string vcodec = ShihabDebridManager.ExtractJsonStringField(fObj, "vcodec");
                string acodec = ShihabDebridManager.ExtractJsonStringField(fObj, "acodec");
                long fsize = ShihabDebridManager.ExtractJsonLongField(fObj, "filesize");
                if (fsize <= 0) fsize = ShihabDebridManager.ExtractJsonLongField(fObj, "filesize_approx");
                if (string.IsNullOrEmpty(fid)) continue;
                formats.Add(new ShihabMediaFormatEntry
                {
                    FormatId = fid,
                    Extension = string.IsNullOrEmpty(ext) ? "mp4" : ext,
                    ResolutionLabel = !string.IsNullOrEmpty(res) ? res : (!string.IsNullOrEmpty(note) ? note : fid),
                    FileSizeBytes = fsize,
                    HasVideo = !string.IsNullOrEmpty(vcodec) && vcodec != "none",
                    HasAudio = !string.IsNullOrEmpty(acodec) && acodec != "none"
                });
            }

            return !string.IsNullOrEmpty(mediaTitle) || formats.Count > 0 || playlistEntries.Count > 0;
        }

        public static bool ExecuteYtDlpDownload(
            ShihabDownloadItem item,
            string baseDataDir,
            MehediAppConfiguration cfg,
            long perTaskRateLimitBps,
            Action onProgressChanged,
            CancellationToken cancelToken,
            out string finalFilePath,
            out string errorMessage)
        {
            finalFilePath = "";
            errorMessage = "";
            string ytdlpExe = GetToolExecutablePath(baseDataDir, "yt-dlp");
            if (!File.Exists(ytdlpExe))
            {
                errorMessage = "yt-dlp.exe is required for this media stream. Install it from Tools / Options -> Media Helpers.";
                return false;
            }

            Directory.CreateDirectory(item.MehediSaveDirectory);
            string toolsDir = GetToolsDirectory(baseDataDir);
            string ffmpegExe = Path.Combine(toolsDir, "ffmpeg.exe");
            string baseStem = Path.GetFileNameWithoutExtension(ShihabFormatHelpers.SanitizeShihabFileName(item.ShihabFileName));
            if (string.IsNullOrWhiteSpace(baseStem) || baseStem == "download") baseStem = "%(title).120s";
            string outTemplate = Path.Combine(item.MehediSaveDirectory, baseStem + ".%(ext)s");

            StringBuilder args = new StringBuilder();
            args.Append("--newline --no-warnings --ignore-config --continue --no-playlist ");
            if (File.Exists(ffmpegExe))
            {
                args.Append("--ffmpeg-location \"").Append(toolsDir).Append("\" ");
            }

            string proxyUrl = ShihabLocalNetworkRelay.GetSubprocessProxyUrl(cfg);
            if (!string.IsNullOrEmpty(proxyUrl))
            {
                args.Append("--proxy \"").Append(proxyUrl.Replace("\"", "")).Append("\" ");
            }
            if (perTaskRateLimitBps > 0)
            {
                args.Append("--limit-rate ").Append(perTaskRateLimitBps.ToString(CultureInfo.InvariantCulture)).Append(' ');
            }

            if (!string.IsNullOrWhiteSpace(item.ShihabAudioOnlyExtractFormat))
            {
                string af = item.ShihabAudioOnlyExtractFormat.Trim().ToLowerInvariant();
                args.Append("-x --audio-format ").Append(af).Append(" --audio-quality 0 ");
            }
            else if (!string.IsNullOrWhiteSpace(item.ShihabYtDlpFormatSelector))
            {
                args.Append("-f \"").Append(item.ShihabYtDlpFormatSelector.Replace("\"", "")).Append("\" ");
            }
            else
            {
                args.Append("-f \"bv*+ba/b\" --merge-output-format mp4 ");
            }

            if (!string.IsNullOrWhiteSpace(item.ShihabSubtitleLanguages))
            {
                args.Append("--write-subs --write-auto-subs --sub-langs \"")
                    .Append(item.ShihabSubtitleLanguages.Replace("\"", ""))
                    .Append("\" ");
                if (item.ShihabEmbedSubtitles && File.Exists(ffmpegExe)) args.Append("--embed-subs ");
            }

            if (!string.IsNullOrWhiteSpace(item.MehediReferer))
            {
                args.Append("--referer \"").Append(item.MehediReferer.Replace("\"", "")).Append("\" ");
            }
            if (!string.IsNullOrWhiteSpace(item.MehediUserAgent))
            {
                args.Append("--user-agent \"").Append(item.MehediUserAgent.Replace("\"", "")).Append("\" ");
            }
            if (!string.IsNullOrWhiteSpace(item.ShihabCookies) && !item.MehediOmitCookies)
            {
                args.Append("--add-header \"Cookie:").Append(item.ShihabCookies.Replace("\"", "")).Append("\" ");
            }

            args.Append("-o \"").Append(outTemplate.Replace("\"", "")).Append("\" ");
            args.Append("-- \"").Append(item.MehediUrl.Trim().Replace("\"", "")).Append("\"");

            string detectedOutFile = "";
            Regex progRx = new Regex(@"\[download\]\s+([0-9]+(?:\.[0-9]+)?)%\s+of\s+~?\s*([0-9]+(?:\.[0-9]+)?)([KMGTP]?i?B)(?:\s+at\s+([0-9]+(?:\.[0-9]+)?)([KMGTP]?i?B)/s)?", RegexOptions.IgnoreCase);
            Regex destRx = new Regex(@"(?:\[download\] Destination:\s+|\[Merger\] Merging formats into\s+""?|\[ExtractAudio\] Destination:\s+)(.+?)\""?$", RegexOptions.IgnoreCase);

            string stdoutAll;
            string stderrAll;
            int exitCode = ShihabWindowsJobRunner.RunManagedSubprocess(
                ytdlpExe,
                args.ToString(),
                item.MehediSaveDirectory,
                0,
                delegate(string line)
                {
                    if (string.IsNullOrWhiteSpace(line)) return;
                    Match dm = destRx.Match(line.Trim());
                    if (dm.Success)
                    {
                        string p = dm.Groups[1].Value.Trim().Trim('"');
                        if (!string.IsNullOrEmpty(p)) detectedOutFile = p;
                    }
                    Match pm = progRx.Match(line);
                    if (pm.Success)
                    {
                        double pct = 0;
                        double totalNum = 0;
                        double spdNum = 0;
                        double.TryParse(pm.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out pct);
                        double.TryParse(pm.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out totalNum);
                        long totalBytes = ParseUnitBytes(totalNum, pm.Groups[3].Value);
                        if (pm.Groups[4].Success && double.TryParse(pm.Groups[4].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out spdNum))
                        {
                            item.ShihabCurrentSpeedBps = ParseUnitBytes(spdNum, pm.Groups[5].Value);
                        }
                        if (totalBytes > 0)
                        {
                            item.ShihabTotalBytes = totalBytes;
                            item.MehediDownloadedBytes = (long)Math.Round(totalBytes * (pct / 100.0));
                        }
                        item.ShihabStatusText = string.Format("Downloading ({0:0.0}%)", pct);
                        if (onProgressChanged != null) onProgressChanged();
                    }
                },
                cancelToken,
                out stdoutAll,
                out stderrAll);

            cancelToken.ThrowIfCancellationRequested();
            if (exitCode != 0)
            {
                errorMessage = !string.IsNullOrWhiteSpace(stderrAll) ? stderrAll.Trim() : ("yt-dlp exited with code " + exitCode);
                return false;
            }

            if (!string.IsNullOrEmpty(detectedOutFile) && File.Exists(detectedOutFile))
            {
                finalFilePath = detectedOutFile;
            }
            else
            {
                // Locate newest matching file in save directory
                string[] candFiles = Directory.GetFiles(item.MehediSaveDirectory);
                DateTime newest = DateTime.MinValue;
                for (int i = 0; i < candFiles.Length; i++)
                {
                    FileInfo fi = new FileInfo(candFiles[i]);
                    if (fi.LastWriteTimeUtc > newest && !fi.Name.EndsWith(".part", StringComparison.OrdinalIgnoreCase))
                    {
                        newest = fi.LastWriteTimeUtc;
                        finalFilePath = fi.FullName;
                    }
                }
            }

            if (!string.IsNullOrEmpty(finalFilePath) && File.Exists(finalFilePath))
            {
                FileInfo fin = new FileInfo(finalFilePath);
                item.ShihabFileName = fin.Name;
                item.ShihabTotalBytes = fin.Length;
                item.MehediDownloadedBytes = fin.Length;
                return true;
            }
            return true;
        }

        public static bool ExecuteGalleryDlJob(
            ShihabDownloadItem item,
            string baseDataDir,
            MehediAppConfiguration cfg,
            Action onProgressChanged,
            CancellationToken cancelToken,
            out int filesSavedCount,
            out string errorMessage)
        {
            filesSavedCount = 0;
            errorMessage = "";
            string targetFolder = Path.Combine(item.MehediSaveDirectory, Path.GetFileNameWithoutExtension(ShihabFormatHelpers.SanitizeShihabFileName(item.ShihabFileName)));
            Directory.CreateDirectory(targetFolder);

            string gdlExe = GetToolExecutablePath(baseDataDir, "gallery-dl");
            if (File.Exists(gdlExe))
            {
                StringBuilder args = new StringBuilder();
                args.Append("--no-warnings --ignore-config --sleep 1 ");
                if (item.ShihabGalleryMaxItems > 0)
                {
                    args.Append("--range 1-").Append(item.ShihabGalleryMaxItems.ToString(CultureInfo.InvariantCulture)).Append(' ');
                }
                string proxyUrl = ShihabLocalNetworkRelay.GetSubprocessProxyUrl(cfg);
                if (!string.IsNullOrEmpty(proxyUrl))
                {
                    args.Append("--proxy \"").Append(proxyUrl.Replace("\"", "")).Append("\" ");
                }
                args.Append("--directory \"").Append(targetFolder.Replace("\"", "")).Append("\" ");
                args.Append("-- \"").Append(item.MehediUrl.Trim().Replace("\"", "")).Append("\"");

                int count = 0;
                string stdoutAll, stderrAll;
                int code = ShihabWindowsJobRunner.RunManagedSubprocess(
                    gdlExe,
                    args.ToString(),
                    targetFolder,
                    0,
                    delegate(string ln)
                    {
                        if (!string.IsNullOrWhiteSpace(ln))
                        {
                            count++;
                            item.ShihabStatusText = string.Format("Gallery ({0} files)", count);
                            if (onProgressChanged != null) onProgressChanged();
                        }
                    },
                    cancelToken,
                    out stdoutAll,
                    out stderrAll);

                cancelToken.ThrowIfCancellationRequested();
                if (code == 0)
                {
                    filesSavedCount = Directory.GetFiles(targetFolder, "*", System.IO.SearchOption.AllDirectories).Length;
                    item.ShihabFileName = Path.GetFileName(targetFolder);
                    return true;
                }
                errorMessage = !string.IsNullOrWhiteSpace(stderrAll) ? stderrAll.Trim() : ("gallery-dl exited with code " + code);
                return false;
            }

            // Fallback to built-in gallery extractor when gallery-dl.exe is not installed
            string gTitle;
            List<string> urls = ExtractGalleryImageUrls(item.MehediUrl, baseDataDir, out gTitle);
            if (urls.Count == 0)
            {
                errorMessage = "No gallery media URLs found (install gallery-dl helper for site-specific extractors).";
                return false;
            }
            int limit = item.ShihabGalleryMaxItems > 0 ? Math.Min(urls.Count, item.ShihabGalleryMaxItems) : urls.Count;
            long totalBytes = 0;
            for (int i = 0; i < limit; i++)
            {
                cancelToken.ThrowIfCancellationRequested();
                try
                {
                    HttpWebRequest req = (HttpWebRequest)WebRequest.Create(urls[i]);
                    req.Timeout = 15000;
                    if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, urls[i]);
                    string leaf = ShihabFormatHelpers.EnsureShihabUniqueFilePath(targetFolder, ShihabFormatHelpers.ExtractMehediFileNameFromUrl(urls[i]));
                    string dst = Path.Combine(targetFolder, leaf);
                    using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                    using (Stream s = resp.GetResponseStream())
                    using (FileStream fs = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        s.CopyTo(fs);
                        totalBytes += fs.Length;
                    }
                    filesSavedCount++;
                    item.MehediDownloadedBytes = totalBytes;
                    item.ShihabStatusText = string.Format("Gallery ({0}/{1})", filesSavedCount, limit);
                    if (onProgressChanged != null) onProgressChanged();
                }
                catch { }
            }
            item.ShihabTotalBytes = totalBytes;
            item.ShihabFileName = Path.GetFileName(targetFolder);
            return filesSavedCount > 0;
        }

        private static long ParseUnitBytes(double val, string unit)
        {
            string u = (unit ?? "").Trim().ToUpperInvariant();
            if (u.StartsWith("K")) return (long)(val * 1024.0);
            if (u.StartsWith("M")) return (long)(val * 1024.0 * 1024.0);
            if (u.StartsWith("G")) return (long)(val * 1024.0 * 1024.0 * 1024.0);
            if (u.StartsWith("T")) return (long)(val * 1024.0 * 1024.0 * 1024.0 * 1024.0);
            return (long)val;
        }

        public static List<string> ExtractGalleryImageUrls(string pageUrl, string baseDataDir, out string galleryTitle)
        {
            galleryTitle = "Image_Gallery";
            List<string> results = new List<string>();
            if (string.IsNullOrWhiteSpace(pageUrl)) return results;

            string gdlExe = GetToolExecutablePath(baseDataDir, "gallery-dl");
            if (File.Exists(gdlExe))
            {
                try
                {
                    string stdoutAll, stderrAll;
                    int code = ShihabWindowsJobRunner.RunManagedSubprocess(
                        gdlExe,
                        "--no-warnings --ignore-config --get-urls -- \"" + pageUrl.Replace("\"", "") + "\"",
                        GetToolsDirectory(baseDataDir),
                        20000,
                        null,
                        CancellationToken.None,
                        out stdoutAll,
                        out stderrAll);
                    if (code == 0 && !string.IsNullOrWhiteSpace(stdoutAll))
                    {
                        string[] lines = stdoutAll.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 0; i < lines.Length; i++)
                        {
                            string ln = lines[i].Trim();
                            if (ln.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                                ln.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                            {
                                if (!results.Contains(ln)) results.Add(ln);
                            }
                        }
                        if (results.Count > 0) return results;
                    }
                }
                catch { }
            }

            try
            {
                Uri baseUri = new Uri(pageUrl);
                galleryTitle = ShihabFormatHelpers.SanitizeShihabFileName(baseUri.Host + "_gallery");
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(pageUrl);
                req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36";
                req.Timeout = 12000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string html = sr.ReadToEnd();
                    Match tm = Regex.Match(html, @"<title[^>]*>(.*?)</title>", RegexOptions.IgnoreCase | RegexOptions.Singleline);
                    if (tm.Success)
                    {
                        string t = WebUtility.HtmlDecode(tm.Groups[1].Value).Trim();
                        if (!string.IsNullOrEmpty(t)) galleryTitle = ShihabFormatHelpers.SanitizeShihabFileName(t);
                    }
                    MatchCollection matches = Regex.Matches(
                        html,
                        @"(?:src|data-src|href|content)\s*=\s*[""']([^""']+\.(?:jpg|jpeg|png|webp|gif|avif|bmp|mp4)(?:\?[^""']*)?)[""']",
                        RegexOptions.IgnoreCase);
                    for (int i = 0; i < matches.Count; i++)
                    {
                        string raw = WebUtility.HtmlDecode(matches[i].Groups[1].Value);
                        Uri abs;
                        if (Uri.TryCreate(baseUri, raw, out abs))
                        {
                            string u = abs.AbsoluteUri;
                            if (!results.Contains(u)) results.Add(u);
                        }
                    }
                }
            }
            catch { }
            return results;
        }
    }

    // =========================================================================
    // 9. RSS & ATOM FEED READER & AUTO-DOWNLOADER (Torznab, Dedup & Poller)
    // =========================================================================
    public class ShihabRssEntryItem
    {
        public string GuidId { get; set; }
        public string Title { get; set; }
        public string DownloadUrl { get; set; }
        public string PublishedDate { get; set; }
        public long SizeBytes { get; set; }
    }

    public class ShihabRssItemEntry : ShihabRssEntryItem
    {
    }

    public static class ShihabRssFeedManager
    {
        private const int MaxFeedBytes = 5 * 1024 * 1024; // 5 MiB XML cap

        public static List<ShihabRssItemEntry> FetchFeedItems(string feedUrl, string userAgent)
        {
            List<ShihabRssItemEntry> res = new List<ShihabRssItemEntry>();
            try
            {
                string title;
                List<ShihabRssEntryItem> raw = FetchAndParseFeed(feedUrl, null, out title);
                for (int i = 0; i < raw.Count; i++)
                {
                    res.Add(new ShihabRssItemEntry
                    {
                        GuidId = raw[i].GuidId,
                        Title = raw[i].Title,
                        DownloadUrl = raw[i].DownloadUrl,
                        PublishedDate = raw[i].PublishedDate,
                        SizeBytes = raw[i].SizeBytes
                    });
                }
            }
            catch { }
            return res;
        }

        public static List<ShihabRssEntryItem> FetchAndParseFeed(string feedUrl, out string feedTitle)
        {
            return FetchAndParseFeed(feedUrl, null, out feedTitle);
        }

        public static List<ShihabRssEntryItem> FetchAndParseFeed(string feedUrl, MehediAppConfiguration cfg, out string feedTitle)
        {
            feedTitle = "RSS Feed";
            List<ShihabRssEntryItem> items = new List<ShihabRssEntryItem>();
            if (string.IsNullOrWhiteSpace(feedUrl)) return items;

            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(feedUrl.Trim());
            req.UserAgent = "MHS-Download-Manager-RSS/" + ShihabAutoUpdater.GetCurrentAppVersion();
            req.Timeout = 15000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            if (cfg != null) ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, feedUrl);

            byte[] xmlBytes;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            using (Stream s = resp.GetResponseStream())
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buf = new byte[16384];
                int r;
                while ((r = s.Read(buf, 0, buf.Length)) > 0)
                {
                    if (ms.Length + r > MaxFeedBytes)
                    {
                        throw new InvalidOperationException("RSS/Atom feed exceeds 5 MB safety limit.");
                    }
                    ms.Write(buf, 0, r);
                }
                xmlBytes = ms.ToArray();
            }

            XmlReaderSettings settings = new XmlReaderSettings();
            settings.DtdProcessing = DtdProcessing.Prohibit;
            settings.XmlResolver = null;
            settings.MaxCharactersFromEntities = 1024;

            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            using (MemoryStream xmlMs = new MemoryStream(xmlBytes))
            using (XmlReader xr = XmlReader.Create(xmlMs, settings))
            {
                doc.Load(xr);
            }

            XmlNode chTitle = doc.SelectSingleNode("//*[local-name()='channel']/*[local-name()='title']")
                           ?? doc.SelectSingleNode("//*[local-name()='feed']/*[local-name()='title']");
            if (chTitle != null && !string.IsNullOrWhiteSpace(chTitle.InnerText))
            {
                feedTitle = chTitle.InnerText.Trim();
            }

            // RSS 2.0 & RSS 1.0 (RDF) <item>
            XmlNodeList rssItems = doc.SelectNodes("//*[local-name()='item']");
            if (rssItems != null && rssItems.Count > 0)
            {
                for (int i = 0; i < rssItems.Count; i++)
                {
                    XmlNode node = rssItems[i];
                    string title = GetLocalChildText(node, "title");
                    string link = GetLocalChildText(node, "link");
                    string guid = GetLocalChildText(node, "guid");
                    string pubDate = GetLocalChildText(node, "pubDate");
                    if (string.IsNullOrEmpty(pubDate)) pubDate = GetLocalChildText(node, "date");
                    long size = -1;

                    // Torznab / Newznab / Torrent RSS extensions (<torrent:magnetURI>, <torznab:attr name="magneturl">)
                    string magnetChild = GetLocalChildText(node, "magnetURI");
                    if (!string.IsNullOrWhiteSpace(magnetChild) && magnetChild.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                    {
                        link = magnetChild;
                    }

                    XmlNodeList torznabAttrs = node.SelectNodes("*[local-name()='attr']");
                    if (torznabAttrs != null)
                    {
                        for (int a = 0; a < torznabAttrs.Count; a++)
                        {
                            XmlNode an = torznabAttrs[a];
                            if (an.Attributes == null) continue;
                            XmlAttribute nm = an.Attributes["name"];
                            XmlAttribute vl = an.Attributes["value"];
                            if (nm != null && vl != null)
                            {
                                if (string.Equals(nm.Value, "magneturl", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(vl.Value))
                                    link = vl.Value.Trim();
                                else if (string.Equals(nm.Value, "size", StringComparison.OrdinalIgnoreCase))
                                    long.TryParse(vl.Value, out size);
                            }
                        }
                    }

                    XmlNode enc = node.SelectSingleNode("*[local-name()='enclosure']");
                    if (enc != null && enc.Attributes != null)
                    {
                        XmlAttribute urlAttr = enc.Attributes["url"];
                        if (urlAttr != null && !string.IsNullOrWhiteSpace(urlAttr.Value) &&
                            !link.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
                        {
                            link = urlAttr.Value.Trim();
                        }
                        XmlAttribute lenAttr = enc.Attributes["length"];
                        if (lenAttr != null && size <= 0)
                        {
                            long.TryParse(lenAttr.Value, out size);
                        }
                    }

                    if (IsAllowedRssItemUrl(link))
                    {
                        items.Add(new ShihabRssEntryItem
                        {
                            GuidId = string.IsNullOrWhiteSpace(guid) ? link : guid,
                            Title = string.IsNullOrWhiteSpace(title) ? ShihabFormatHelpers.ExtractMehediFileNameFromUrl(link) : title,
                            DownloadUrl = link,
                            PublishedDate = pubDate,
                            SizeBytes = size
                        });
                    }
                }
                return items;
            }

            // Atom <entry>
            XmlNodeList atomEntries = doc.SelectNodes("//*[local-name()='entry']");
            if (atomEntries != null)
            {
                for (int i = 0; i < atomEntries.Count; i++)
                {
                    XmlNode node = atomEntries[i];
                    string title = GetLocalChildText(node, "title");
                    string id = GetLocalChildText(node, "id");
                    string updated = GetLocalChildText(node, "updated");
                    if (string.IsNullOrEmpty(updated)) updated = GetLocalChildText(node, "published");
                    string href = "";
                    long size = -1;

                    XmlNodeList links = node.SelectNodes("*[local-name()='link']");
                    if (links != null)
                    {
                        for (int l = 0; l < links.Count; l++)
                        {
                            XmlNode ln = links[l];
                            if (ln.Attributes == null) continue;
                            XmlAttribute hrefAttr = ln.Attributes["href"];
                            XmlAttribute relAttr = ln.Attributes["rel"];
                            XmlAttribute lenAttr = ln.Attributes["length"];
                            if (hrefAttr != null && !string.IsNullOrWhiteSpace(hrefAttr.Value))
                            {
                                if (string.IsNullOrEmpty(href) || (relAttr != null && relAttr.Value == "enclosure"))
                                {
                                    href = hrefAttr.Value.Trim();
                                    if (lenAttr != null) long.TryParse(lenAttr.Value, out size);
                                }
                            }
                        }
                    }

                    if (IsAllowedRssItemUrl(href))
                    {
                        items.Add(new ShihabRssEntryItem
                        {
                            GuidId = string.IsNullOrWhiteSpace(id) ? href : id,
                            Title = string.IsNullOrWhiteSpace(title) ? ShihabFormatHelpers.ExtractMehediFileNameFromUrl(href) : title,
                            DownloadUrl = href,
                            PublishedDate = updated,
                            SizeBytes = size
                        });
                    }
                }
            }
            return items;
        }

        private static bool IsAllowedRssItemUrl(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return false;
            string s = url.Trim();
            return s.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                   s.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                   s.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) ||
                   s.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase) ||
                   s.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase);
        }

        public static string ExtractEpisodeIdentityKey(string title)
        {
            if (string.IsNullOrWhiteSpace(title)) return "";
            Match se = Regex.Match(title, @"\bS([0-9]{1,2})E([0-9]{1,3})\b", RegexOptions.IgnoreCase);
            if (se.Success)
            {
                int sNum = 0, eNum = 0;
                int.TryParse(se.Groups[1].Value, out sNum);
                int.TryParse(se.Groups[2].Value, out eNum);
                string seriesPrefix = Regex.Replace(title.Substring(0, se.Index), @"[^a-zA-Z0-9]+", "_").Trim('_').ToLowerInvariant();
                return string.Format(CultureInfo.InvariantCulture, "{0}:s{1:00}e{2:00}", seriesPrefix, sNum, eNum);
            }
            Match nx = Regex.Match(title, @"\b([0-9]{1,2})x([0-9]{2,3})\b", RegexOptions.IgnoreCase);
            if (nx.Success)
            {
                int sNum = 0, eNum = 0;
                int.TryParse(nx.Groups[1].Value, out sNum);
                int.TryParse(nx.Groups[2].Value, out eNum);
                string seriesPrefix = Regex.Replace(title.Substring(0, nx.Index), @"[^a-zA-Z0-9]+", "_").Trim('_').ToLowerInvariant();
                return string.Format(CultureInfo.InvariantCulture, "{0}:s{1:00}e{2:00}", seriesPrefix, sNum, eNum);
            }
            return "";
        }

        public static bool MatchesRssRule(ShihabRssFeedConfig sub, ShihabRssEntryItem entry)
        {
            if (sub == null || entry == null) return false;
            string haystack = ((entry.Title ?? "") + " " + (entry.DownloadUrl ?? "")).Trim();
            string must = string.IsNullOrWhiteSpace(sub.MehediFilterPattern) ? "*" : sub.MehediFilterPattern.Trim();
            if (must != "*" && !ShihabFormatHelpers.WildcardMatchesMehediPattern(haystack, must))
            {
                try
                {
                    if (!Regex.IsMatch(haystack, must, RegexOptions.IgnoreCase)) return false;
                }
                catch { return false; }
            }

            if (!string.IsNullOrWhiteSpace(sub.ShihabExcludePattern))
            {
                string excl = sub.ShihabExcludePattern.Trim();
                if (ShihabFormatHelpers.WildcardMatchesMehediPattern(haystack, excl)) return false;
                try
                {
                    if (Regex.IsMatch(haystack, excl, RegexOptions.IgnoreCase)) return false;
                }
                catch { }
            }
            return true;
        }

        public static bool PollDueFeedsInBackground(
            MehediAppConfiguration cfg,
            Action<ShihabRssFeedConfig, ShihabRssEntryItem> onAutoEnqueueItem)
        {
            if (cfg == null || cfg.ShihabRssFeeds == null || cfg.ShihabRssFeeds.Count == 0)
            {
                return false;
            }

            bool configDirty = false;
            DateTime now = DateTime.Now;
            for (int i = 0; i < cfg.ShihabRssFeeds.Count; i++)
            {
                ShihabRssFeedConfig sub = cfg.ShihabRssFeeds[i];
                if (sub == null || string.IsNullOrWhiteSpace(sub.ShihabFeedUrl)) continue;

                int intervalMins = Math.Max(5, sub.MehediCheckIntervalMinutes);
                DateTime lastChecked;
                if (!string.IsNullOrWhiteSpace(sub.ShihabLastChecked) &&
                    DateTime.TryParse(sub.ShihabLastChecked, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out lastChecked))
                {
                    if ((now - lastChecked).TotalMinutes < intervalMins) continue;
                }

                try
                {
                    string title;
                    List<ShihabRssEntryItem> entries = FetchAndParseFeed(sub.ShihabFeedUrl, cfg, out title);
                    if (string.IsNullOrWhiteSpace(sub.ShihabFeedTitle) || sub.ShihabFeedTitle == "RSS Feed")
                    {
                        sub.ShihabFeedTitle = title;
                    }
                    sub.ShihabLastChecked = now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    if (sub.MehediSeenItemGuids == null) sub.MehediSeenItemGuids = new List<string>();
                    if (sub.ShihabDownloadedEpisodeKeys == null) sub.ShihabDownloadedEpisodeKeys = new List<string>();

                    // Seed initial backlog on first poll so historical items are not mass-enqueued unexpectedly
                    if (!sub.ShihabInitialBacklogSeeded)
                    {
                        for (int e = 0; e < entries.Count; e++)
                        {
                            if (!string.IsNullOrEmpty(entries[e].GuidId) && !sub.MehediSeenItemGuids.Contains(entries[e].GuidId))
                            {
                                sub.MehediSeenItemGuids.Add(entries[e].GuidId);
                            }
                        }
                        sub.ShihabInitialBacklogSeeded = true;
                        configDirty = true;
                        continue;
                    }

                    for (int e = 0; e < entries.Count; e++)
                    {
                        ShihabRssEntryItem item = entries[e];
                        if (string.IsNullOrEmpty(item.GuidId) || sub.MehediSeenItemGuids.Contains(item.GuidId)) continue;
                        sub.MehediSeenItemGuids.Add(item.GuidId);
                        while (sub.MehediSeenItemGuids.Count > 500) sub.MehediSeenItemGuids.RemoveAt(0);
                        configDirty = true;

                        if (!sub.ShihabAutoDownload || !MatchesRssRule(sub, item)) continue;

                        string epKey = ExtractEpisodeIdentityKey(item.Title);
                        if (!string.IsNullOrEmpty(epKey))
                        {
                            if (sub.ShihabDownloadedEpisodeKeys.Contains(epKey)) continue;
                            sub.ShihabDownloadedEpisodeKeys.Add(epKey);
                            while (sub.ShihabDownloadedEpisodeKeys.Count > 500) sub.ShihabDownloadedEpisodeKeys.RemoveAt(0);
                        }

                        if (onAutoEnqueueItem != null) onAutoEnqueueItem(sub, item);
                    }
                }
                catch { }
            }
            return configDirty;
        }

        private static string GetLocalChildText(XmlNode parent, string localName)
        {
            XmlNode n = parent.SelectSingleNode("*[local-name()='" + localName + "']");
            return n != null ? n.InnerText.Trim() : "";
        }
    }

    // =========================================================================
    // 10. LAN REMOTE CONTROL WEB UI (Port 8463), PAIRING & QR VISUAL RENDERER
    // =========================================================================
    public class ShihabRemoteControlServer : IDisposable
    {
        private readonly Func<MehediAppConfiguration> _getConfig;
        private readonly Action _saveConfig;
        private readonly Func<List<ShihabDownloadItem>> _getDownloadsSnapshot;
        private readonly Action<string, string> _addDownloadUrl;
        private readonly Action<string, string> _performDownloadAction;
        private readonly object _rateLock = new object();
        private readonly Dictionary<string, List<DateTime>> _failedPairAttemptsByIp = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
        private TcpListener _listener;
        private volatile bool _running;
        public int Port { get; private set; }

        public ShihabRemoteControlServer(
            ShihabStateStore store,
            MehediAppConfiguration config,
            MehediDynamicSegmentEngine engine)
            : this(
                delegate { return config; },
                delegate { if (store != null && config != null) store.SaveMehediConfig(config); },
                delegate { return engine != null ? engine.GetShihabDownloadsSnapshot() : new List<ShihabDownloadItem>(); },
                delegate(string u, string n)
                {
                    if (engine != null && config != null && !string.IsNullOrWhiteSpace(u))
                    {
                        MehediBrowserBridgeServer.SendUrlToExistingShihabInstance(config.MehediLocalBridgePort, u, "", n);
                    }
                },
                delegate(string act, string id)
                {
                    if (engine == null || string.IsNullOrWhiteSpace(act)) return;
                    if (act == "start_queue") { engine.StartMehediQueue("Main Queue"); return; }
                    if (act == "stop_queue") { engine.StopShihabQueue("Main Queue"); return; }
                    if (string.IsNullOrWhiteSpace(id)) return;
                    List<ShihabDownloadItem> snap = engine.GetShihabDownloadsSnapshot();
                    for (int i = 0; i < snap.Count; i++)
                    {
                        ShihabDownloadItem candidate = snap[i];
                        bool matches = string.Equals(id, "all", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(candidate.ShihabId, id, StringComparison.OrdinalIgnoreCase) ||
                            candidate.ShihabId.StartsWith(id, StringComparison.OrdinalIgnoreCase);
                        if (!matches) continue;
                        if (act == "pause") engine.PauseShihabDownload(candidate);
                        else if (act == "resume" || act == "start") engine.StartOrResumeMehediDownload(candidate);
                        else if (act == "remove") engine.RemoveShihabDownload(candidate, false);
                        else if (act == "delete") engine.RemoveShihabDownload(candidate, true);
                        if (!string.Equals(id, "all", StringComparison.OrdinalIgnoreCase)) break;
                    }
                })
        {
            if (config != null && config.ShihabRemoteWebPort > 0)
            {
                Port = config.ShihabRemoteWebPort;
            }
        }

        public ShihabRemoteControlServer(
            Func<MehediAppConfiguration> getConfig,
            Action saveConfig,
            Func<List<ShihabDownloadItem>> getDownloadsSnapshot,
            Action<string, string> addDownloadUrl,
            Action<string, string> performDownloadAction)
        {
            _getConfig = getConfig;
            _saveConfig = saveConfig;
            _getDownloadsSnapshot = getDownloadsSnapshot;
            _addDownloadUrl = addDownloadUrl;
            _performDownloadAction = performDownloadAction;
            Port = 8463;
        }

        public void Start()
        {
            MehediAppConfiguration cfg = _getConfig != null ? _getConfig() : null;
            if (cfg != null) EnsureFreshPairingCode(cfg, _saveConfig, false);
            StartServer(cfg != null && cfg.ShihabRemoteWebPort > 0 ? cfg.ShihabRemoteWebPort : Port);
        }

        public void Stop()
        {
            StopServer();
        }

        public void Dispose()
        {
            StopServer();
        }

        public static string GenerateEightDigitPairingCode()
        {
            return MehediAppConfiguration.GenerateRandomEightDigitCode();
        }

        public static string EnsureFreshPairingCode(MehediAppConfiguration cfg, Action saveCallback, bool forceRotate)
        {
            if (cfg == null) return GenerateEightDigitPairingCode();
            DateTime expUtc;
            bool expired = true;
            if (!forceRotate &&
                !string.IsNullOrWhiteSpace(cfg.ShihabRemotePairingCode) &&
                !string.IsNullOrWhiteSpace(cfg.ShihabRemotePairingExpiresUtc) &&
                DateTime.TryParse(cfg.ShihabRemotePairingExpiresUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out expUtc))
            {
                expired = DateTime.UtcNow > expUtc;
            }

            if (forceRotate || expired || string.IsNullOrWhiteSpace(cfg.ShihabRemotePairingCode))
            {
                cfg.ShihabRemotePairingCode = GenerateEightDigitPairingCode();
                cfg.ShihabRemotePairingExpiresUtc = DateTime.UtcNow.AddMinutes(10).ToString("o", CultureInfo.InvariantCulture);
                if (saveCallback != null)
                {
                    try { saveCallback(); } catch { }
                }
            }
            return cfg.ShihabRemotePairingCode;
        }

        public static string GetLocalLanDashboardUrl(int port)
        {
            int p = port > 0 ? port : 8463;
            return "http://" + GetPrimaryLanIpAddress() + ":" + p;
        }

        public static Bitmap RenderSimpleQrStyleBadge(string payload, int sizePx)
        {
            return ShihabQrCodeEncoder.RenderBitmap(
                string.IsNullOrWhiteSpace(payload) ? "http://127.0.0.1:8463" : payload,
                Math.Max(120, sizePx),
                Color.FromArgb(15, 23, 42),
                Color.White);
        }

        public static Bitmap RenderPairingMatrixBitmap(string url, string code, int sizePx)
        {
            string fullPayload = string.IsNullOrWhiteSpace(code)
                ? (url ?? "http://127.0.0.1:8463")
                : ((url ?? "http://127.0.0.1:8463").TrimEnd('/') + "/#pair=" + code.Trim());
            return ShihabQrCodeEncoder.RenderBitmap(
                fullPayload,
                Math.Max(120, sizePx),
                Color.FromArgb(15, 23, 42),
                Color.White);
        }

        public void StartServer(int port)
        {
            StopServer();
            Port = port > 0 ? port : 8463;
            try
            {
                _listener = new TcpListener(IPAddress.Any, Port);
                _listener.Start();
                _running = true;
                Thread t = new Thread(AcceptLoop);
                t.IsBackground = true;
                t.Name = "ShihabRemoteWebServer";
                t.Start();
            }
            catch
            {
                _running = false;
            }
        }

        public void StopServer()
        {
            _running = false;
            try
            {
                if (_listener != null) _listener.Stop();
            }
            catch
            {
            }
        }

        public static string GetPrimaryLanIpAddress()
        {
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface nic = nics[i];
                    if (nic.OperationalStatus != OperationalStatus.Up ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    IPInterfaceProperties props = nic.GetIPProperties();
                    for (int u = 0; u < props.UnicastAddresses.Count; u++)
                    {
                        IPAddress addr = props.UnicastAddresses[u].Address;
                        if (addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                        {
                            return addr.ToString();
                        }
                    }
                }
            }
            catch
            {
            }
            return "127.0.0.1";
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                try
                {
                    TcpClient client = _listener.AcceptTcpClient();
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        private bool IsIpRateLimited(string ipKey)
        {
            lock (_rateLock)
            {
                List<DateTime> hits;
                if (!_failedPairAttemptsByIp.TryGetValue(ipKey, out hits))
                {
                    return false;
                }
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-60);
                hits.RemoveAll(delegate(DateTime dt) { return dt < cutoff; });
                return hits.Count >= 5;
            }
        }

        private void RecordFailedPairAttempt(string ipKey)
        {
            lock (_rateLock)
            {
                List<DateTime> hits;
                if (!_failedPairAttemptsByIp.TryGetValue(ipKey, out hits))
                {
                    hits = new List<DateTime>();
                    _failedPairAttemptsByIp[ipKey] = hits;
                }
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-60);
                hits.RemoveAll(delegate(DateTime dt) { return dt < cutoff; });
                hits.Add(DateTime.UtcNow);
            }
        }

        private void HandleClient(object state)
        {
            TcpClient client = state as TcpClient;
            if (client == null) return;
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 6000;
                    client.SendTimeout = 6000;
                    IPEndPoint remoteEp = client.Client.RemoteEndPoint as IPEndPoint;
                    string remoteIp = remoteEp != null ? remoteEp.Address.ToString() : "unknown";

                    NetworkStream ns = client.GetStream();
                    string method;
                    string path;
                    string authHeader;
                    byte[] bodyBytes;
                    if (!TryReadHttpRequest(ns, out method, out path, out authHeader, out bodyBytes))
                    {
                        return;
                    }
                    string body = bodyBytes != null && bodyBytes.Length > 0 ? Encoding.UTF8.GetString(bodyBytes) : "";

                    if (path == "/" || path.StartsWith("/?"))
                    {
                        WriteHttpResponse(ns, 200, "text/html; charset=utf-8", BuildRemoteWebAppHtml());
                        return;
                    }

                    MehediAppConfiguration cfg = _getConfig();
                    if (path == "/api/pair" && method == "POST")
                    {
                        if (IsIpRateLimited(remoteIp))
                        {
                            WriteHttpResponse(ns, 429, "application/json", "{\"ok\":false,\"error\":\"Too many failed pairing attempts. Try again in 60 seconds.\"}");
                            return;
                        }

                        string submittedCode = ShihabDebridManager.ExtractJsonStringField(body, "code");
                        string devName = ShihabDebridManager.ExtractJsonStringField(body, "device");

                        DateTime expUtc;
                        bool codeExpired = false;
                        if (!string.IsNullOrWhiteSpace(cfg.ShihabRemotePairingExpiresUtc) &&
                            DateTime.TryParse(cfg.ShihabRemotePairingExpiresUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out expUtc))
                        {
                            codeExpired = DateTime.UtcNow > expUtc;
                        }

                        if (!codeExpired &&
                            !string.IsNullOrEmpty(submittedCode) &&
                            !string.IsNullOrEmpty(cfg.ShihabRemotePairingCode) &&
                            ConstantTimeEquals(submittedCode.Trim(), cfg.ShihabRemotePairingCode))
                        {
                            byte[] rnd = new byte[32];
                            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
                            {
                                rng.GetBytes(rnd);
                            }
                            StringBuilder rawSb = new StringBuilder(64);
                            for (int i = 0; i < rnd.Length; i++) rawSb.Append(rnd[i].ToString("x2"));
                            string rawToken = rawSb.ToString();
                            string tokenHash = ComputeSha256Hex(rawToken);
                            if (cfg.ShihabPairedRemoteDevices == null) cfg.ShihabPairedRemoteDevices = new List<ShihabPairedRemoteDevice>();
                            cfg.ShihabPairedRemoteDevices.Add(new ShihabPairedRemoteDevice
                            {
                                ShihabDeviceId = Guid.NewGuid().ToString("N").Substring(0, 8),
                                MehediDeviceName = string.IsNullOrEmpty(devName) ? "Web Remote" : devName,
                                ShihabTokenHash = tokenHash,
                                MehediPairedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                                ShihabLastSeenIp = remoteIp
                            });
                            // Single-use pairing code: rotate immediately after pairing
                            EnsureFreshPairingCode(cfg, _saveConfig, true);
                            if (_saveConfig != null) _saveConfig();
                            WriteHttpResponse(ns, 200, "application/json", "{\"ok\":true,\"token\":\"" + rawToken + "\"}");
                        }
                        else
                        {
                            RecordFailedPairAttempt(remoteIp);
                            string errMsg = codeExpired ? "Pairing code expired. Refresh Remote Control dialog." : "Invalid 8-digit pairing code";
                            WriteHttpResponse(ns, 403, "application/json", "{\"ok\":false,\"error\":\"" + errMsg + "\"}");
                        }
                        return;
                    }

                    ShihabPairedRemoteDevice authedDevice;
                    if (!IsAuthorizedBearer(authHeader, cfg, out authedDevice))
                    {
                        WriteHttpResponse(ns, 401, "application/json", "{\"ok\":false,\"error\":\"Unauthorized\"}");
                        return;
                    }
                    if (authedDevice != null)
                    {
                        authedDevice.ShihabLastSeenIp = remoteIp;
                    }

                    if (path == "/api/status" || path == "/api/list")
                    {
                        List<ShihabDownloadItem> items = _getDownloadsSnapshot != null ? _getDownloadsSnapshot() : new List<ShihabDownloadItem>();
                        StringBuilder sb = new StringBuilder();
                        sb.Append("{\"ok\":true,\"items\":[");
                        for (int i = 0; i < items.Count; i++)
                        {
                            ShihabDownloadItem it = items[i];
                            if (i > 0) sb.Append(",");
                            sb.AppendFormat(CultureInfo.InvariantCulture,
                                "{{\"id\":\"{0}\",\"name\":\"{1}\",\"size\":\"{2}\",\"pct\":{3:0.0},\"status\":\"{4}\",\"speed\":\"{5}\"}}",
                                EscapeJson(it.ShihabId),
                                EscapeJson(it.ShihabFileName),
                                EscapeJson(ShihabFormatHelpers.FormatMehediBytes(it.ShihabTotalBytes)),
                                it.GetMehediPercent(),
                                EscapeJson(it.ShihabStatusText),
                                EscapeJson(ShihabFormatHelpers.FormatShihabSpeed(it.ShihabCurrentSpeedBps)));
                        }
                        sb.Append("]}");
                        WriteHttpResponse(ns, 200, "application/json", sb.ToString());
                        return;
                    }

                    if ((path == "/api/add" || path == "/api/torrent") && method == "POST")
                    {
                        string url = ShihabDebridManager.ExtractJsonStringField(body, "url");
                        string name = ShihabDebridManager.ExtractJsonStringField(body, "name");
                        string b64Torrent = ShihabDebridManager.ExtractJsonStringField(body, "torrentBase64");
                        if (!string.IsNullOrWhiteSpace(b64Torrent))
                        {
                            try
                            {
                                byte[] tBytes = Convert.FromBase64String(b64Torrent);
                                string tmpTor = Path.Combine(Path.GetTempPath(), "mhs_remote_" + Guid.NewGuid().ToString("N").Substring(0, 8) + ".torrent");
                                File.WriteAllBytes(tmpTor, tBytes);
                                url = tmpTor;
                            }
                            catch { }
                        }
                        if (!string.IsNullOrWhiteSpace(url) && _addDownloadUrl != null)
                        {
                            _addDownloadUrl(url.Trim(), name);
                            WriteHttpResponse(ns, 200, "application/json", "{\"ok\":true}");
                        }
                        else
                        {
                            WriteHttpResponse(ns, 400, "application/json", "{\"ok\":false,\"error\":\"Missing URL\"}");
                        }
                        return;
                    }

                    if ((path == "/api/action" || path == "/api/act") && method == "POST")
                    {
                        string act = ShihabDebridManager.ExtractJsonStringField(body, "action");
                        string id = ShihabDebridManager.ExtractJsonStringField(body, "id");
                        if (!string.IsNullOrWhiteSpace(act) && _performDownloadAction != null)
                        {
                            _performDownloadAction(act.Trim().ToLowerInvariant(), id);
                            WriteHttpResponse(ns, 200, "application/json", "{\"ok\":true}");
                        }
                        else
                        {
                            WriteHttpResponse(ns, 400, "application/json", "{\"ok\":false}");
                        }
                        return;
                    }

                    if (path == "/api/queue" && method == "POST")
                    {
                        string mode = ShihabDebridManager.ExtractJsonStringField(body, "mode");
                        if (_performDownloadAction != null)
                        {
                            _performDownloadAction(string.Equals(mode, "stop", StringComparison.OrdinalIgnoreCase) ? "stop_queue" : "start_queue", "all");
                        }
                        WriteHttpResponse(ns, 200, "application/json", "{\"ok\":true}");
                        return;
                    }

                    if (path == "/api/forget" && method == "POST")
                    {
                        if (authedDevice != null && cfg.ShihabPairedRemoteDevices != null)
                        {
                            cfg.ShihabPairedRemoteDevices.Remove(authedDevice);
                            if (_saveConfig != null) _saveConfig();
                        }
                        WriteHttpResponse(ns, 200, "application/json", "{\"ok\":true}");
                        return;
                    }

                    WriteHttpResponse(ns, 404, "application/json", "{\"ok\":false,\"error\":\"Not found\"}");
                }
                catch
                {
                }
            }
        }

        private static bool TryReadHttpRequest(Stream ns, out string method, out string path, out string authHeader, out byte[] bodyBytes)
        {
            method = "GET";
            path = "/";
            authHeader = "";
            bodyBytes = new byte[0];

            MemoryStream hdrBuf = new MemoryStream();
            int prev3 = -1, prev2 = -1, prev1 = -1;
            while (hdrBuf.Length < 16384)
            {
                int b = ns.ReadByte();
                if (b < 0) break;
                hdrBuf.WriteByte((byte)b);
                if (prev3 == '\r' && prev2 == '\n' && prev1 == '\r' && b == '\n') break;
                prev3 = prev2;
                prev2 = prev1;
                prev1 = b;
            }
            if (hdrBuf.Length == 0) return false;
            string hdrText = Encoding.ASCII.GetString(hdrBuf.ToArray());
            string[] lines = hdrText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
            if (lines.Length == 0 || string.IsNullOrEmpty(lines[0])) return false;

            string[] firstParts = lines[0].Split(' ');
            method = firstParts.Length > 0 ? firstParts[0].ToUpperInvariant() : "GET";
            path = firstParts.Length > 1 ? firstParts[1] : "/";

            int contentLen = 0;
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i];
                if (string.IsNullOrEmpty(line)) break;
                if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                {
                    int.TryParse(line.Substring(15).Trim(), out contentLen);
                }
                else if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    authHeader = line.Substring(14).Trim();
                }
            }

            if (contentLen > 0 && contentLen <= 2 * 1024 * 1024)
            {
                bodyBytes = new byte[contentLen];
                int read = 0;
                while (read < contentLen)
                {
                    int r = ns.Read(bodyBytes, read, contentLen - read);
                    if (r <= 0) break;
                    read += r;
                }
            }
            return true;
        }

        public static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null || a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= (a[i] ^ b[i]);
            return diff == 0;
        }

        private static bool IsAuthorizedBearer(string authHeader, MehediAppConfiguration cfg, out ShihabPairedRemoteDevice matchedDevice)
        {
            matchedDevice = null;
            if (string.IsNullOrWhiteSpace(authHeader) || cfg == null || cfg.ShihabPairedRemoteDevices == null)
                return false;
            string token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring(7).Trim()
                : authHeader.Trim();
            if (string.IsNullOrEmpty(token)) return false;
            string hash = ComputeSha256Hex(token);
            for (int i = 0; i < cfg.ShihabPairedRemoteDevices.Count; i++)
            {
                if (ConstantTimeEquals(
                    (cfg.ShihabPairedRemoteDevices[i].ShihabTokenHash ?? "").ToLowerInvariant(),
                    hash.ToLowerInvariant()))
                {
                    matchedDevice = cfg.ShihabPairedRemoteDevices[i];
                    return true;
                }
            }
            return false;
        }

        private static string ComputeSha256Hex(string input)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] b = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
                StringBuilder sb = new StringBuilder(64);
                for (int i = 0; i < b.Length; i++) sb.Append(b[i].ToString("x2"));
                return sb.ToString();
            }
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "").Replace("\n", " ");
        }

        private static void WriteHttpResponse(NetworkStream ns, int statusCode, string contentType, string body)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body ?? "");
            string statusText = statusCode == 200 ? "OK" : (statusCode == 401 ? "Unauthorized" : (statusCode == 403 ? "Forbidden" : (statusCode == 429 ? "Too Many Requests" : "Error")));
            string header = string.Format(
                "HTTP/1.1 {0} {1}\r\nContent-Type: {2}\r\nContent-Length: {3}\r\nCache-Control: no-store\r\nX-Content-Type-Options: nosniff\r\nConnection: close\r\n\r\n",
                statusCode, statusText, contentType, payload.Length);
            byte[] hdrBytes = Encoding.ASCII.GetBytes(header);
            ns.Write(hdrBytes, 0, hdrBytes.Length);
            ns.Write(payload, 0, payload.Length);
        }

        private static string BuildRemoteWebAppHtml()
        {
            return @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<title>MHS Download Manager Remote</title>
<style>
body{background:#0b1120;color:#e2e8f0;font-family:system-ui,sans-serif;margin:0;padding:16px}
.card{background:#111827;border:1px solid #1e293b;border-radius:10px;padding:14px;margin-bottom:12px}
input,button{padding:9px 12px;border-radius:6px;border:1px solid #334155;background:#1e293b;color:#f8fafc;font-size:14px}
button{background:#0284c7;border:none;cursor:pointer;font-weight:600}
button.sec{background:#334155}
button.danger{background:#be123c}
.bar{height:8px;background:#1e293b;border-radius:4px;overflow:hidden;margin:6px 0}
.fill{height:100%;background:#38bdf8}
</style></head><body>
<h2 style='margin-top:0;color:#38bdf8'>MHS Download Manager — Remote Control</h2>
<div id='pairBox' class='card'>
  <b>Pair Device (Single-Use 8-Digit Code):</b><br><br>
  <input id='codeInp' placeholder='8-digit pairing code' style='width:180px'>
  <button onclick='pairNow()'>Pair</button>
</div>
<div id='mainBox' style='display:none'>
  <div class='card' style='display:flex;gap:8px;flex-wrap:wrap;align-items:center'>
    <input id='urlInp' placeholder='https://... or magnet:?xt=...' style='flex:1;min-width:200px'>
    <button onclick='addUrl()'>+ Add URL</button>
    <button class='sec' onclick=""qAct('start')"">Start Queue</button>
    <button class='sec' onclick=""qAct('stop')"">Stop Queue</button>
    <button class='danger' onclick='forgetDev()'>Unpair</button>
  </div>
  <div id='list'></div>
</div>
<script>
function esc(s){return String(s||'').replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/""/g,'&quot;').replace(/'/g,'&#39;');}
var tok = localStorage.getItem('mhs_remote_token') || '';
if(location.hash.indexOf('#pair=')===0){ document.getElementById('codeInp').value=decodeURIComponent(location.hash.substring(6)); }
if(tok){ document.getElementById('pairBox').style.display='none'; document.getElementById('mainBox').style.display='block'; refresh(); setInterval(refresh,2000); }
function pairNow(){
  var c=document.getElementById('codeInp').value.trim();
  fetch('/api/pair',{method:'POST',body:JSON.stringify({code:c,device:navigator.platform})}).then(function(r){return r.json();}).then(function(d){
    if(d.ok){ tok=d.token; localStorage.setItem('mhs_remote_token',tok); location.hash=''; location.reload(); } else alert(d.error||'Failed');
  });
}
function addUrl(){
  var u=document.getElementById('urlInp').value.trim(); if(!u)return;
  fetch('/api/add',{method:'POST',headers:{Authorization:'Bearer '+tok},body:JSON.stringify({url:u})}).then(function(){document.getElementById('urlInp').value='';refresh();});
}
function act(a,id){
  fetch('/api/action',{method:'POST',headers:{Authorization:'Bearer '+tok},body:JSON.stringify({action:a,id:id})}).then(function(){refresh();});
}
function qAct(m){
  fetch('/api/queue',{method:'POST',headers:{Authorization:'Bearer '+tok},body:JSON.stringify({mode:m})}).then(function(){refresh();});
}
function forgetDev(){
  fetch('/api/forget',{method:'POST',headers:{Authorization:'Bearer '+tok},body:'{}'}).then(function(){localStorage.removeItem('mhs_remote_token');location.reload();});
}
function refresh(){
  fetch('/api/status',{headers:{Authorization:'Bearer '+tok}}).then(function(r){if(r.status===401){localStorage.removeItem('mhs_remote_token');location.reload();}return r.json();}).then(function(d){
    if(!d||!d.items)return;
    var h='';
    for(var i=0;i<d.items.length;i++){
      var it=d.items[i];
      var pct=Math.max(0,Math.min(100,Number(it.pct)||0));
      var safeId=esc(it.id);
      h+=""<div class='card'><b>""+esc(it.name)+""</b> <span style='color:#94a3b8'>(""+esc(it.size)+"")</span><div class='bar'><div class='fill' style='width:""+pct+""%'></div></div><div style='display:flex;justify-content:space-between;align-items:center'><small>""+esc(it.status)+"" ""+esc(it.speed)+""</small><div><button class='sec' onclick=\""act('resume','""+safeId+""')\"">Start</button> <button class='sec' onclick=\""act('pause','""+safeId+""')\"">Pause</button> <button class='sec' onclick=\""act('remove','""+safeId+""')\"">Remove</button></div></div></div>"";
    }
    document.getElementById('list').innerHTML=h||'<div class=card>No downloads in list.</div>';
  });
}
</script></body></html>";
        }
    }

    // =========================================================================
    // 11. MODEL CONTEXT PROTOCOL (MCP) STDIO SERVER & HEADLESS CLI RUNNER
    // =========================================================================
    public static class ShihabMcpAndCliHost
    {
        public static bool TryCallLiveBridge(
            ShihabStateStore store,
            MehediAppConfiguration cfg,
            string httpMethod,
            string path,
            string jsonBody,
            out string responseBody)
        {
            responseBody = "";
            if (store == null || cfg == null || cfg.MehediLocalBridgePort <= 0) return false;
            try
            {
                string token = store.LoadOrCreateBridgeToken();
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(
                    "http://127.0.0.1:" + cfg.MehediLocalBridgePort + path);
                req.Method = string.IsNullOrWhiteSpace(httpMethod) ? "GET" : httpMethod;
                req.Timeout = 2500;
                req.ReadWriteTimeout = 2500;
                req.Proxy = null;
                req.Headers["X-MHS-Bridge-Token"] = token;
                if (req.Method == "POST")
                {
                    byte[] payload = Encoding.UTF8.GetBytes(jsonBody ?? "{}");
                    req.ContentType = "application/json; charset=utf-8";
                    req.ContentLength = payload.Length;
                    using (Stream s = req.GetRequestStream())
                    {
                        s.Write(payload, 0, payload.Length);
                    }
                }
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream rs = resp.GetResponseStream())
                {
                    if (rs != null)
                    {
                        using (StreamReader sr = new StreamReader(rs, Encoding.UTF8))
                        {
                            responseBody = sr.ReadToEnd();
                        }
                    }
                    return (int)resp.StatusCode >= 200 && (int)resp.StatusCode < 300;
                }
            }
            catch
            {
                return false;
            }
        }

        public static int RunMcpStdioServer(ShihabStateStore store)
        {
            MehediAppConfiguration cfg = store.LoadShihabConfig();
            string line;
            while ((line = Console.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line)) continue;
                string method = ShihabDebridManager.ExtractJsonStringField(line, "method");
                string idRaw = ExtractJsonIdRaw(line);

                if (method == "initialize")
                {
                    WriteJsonRpcResult(idRaw,
                        "{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{\"tools\":{}},\"serverInfo\":{\"name\":\"mhs-download-manager-mcp\",\"version\":\"" + ShihabAutoUpdater.GetCurrentAppVersion() + "\"}}");
                }
                else if (method == "notifications/initialized")
                {
                    // No response required for notifications
                }
                else if (method == "tools/list")
                {
                    string toolsJson = "{\"tools\":[" +
                        "{\"name\":\"list_downloads\",\"description\":\"List all downloads in MHS Download Manager with live status, size, progress, and checksum.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{}}}," +
                        "{\"name\":\"add_download\",\"description\":\"Add a new URL or Magnet link to MHS Download Manager.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"fileName\":{\"type\":\"string\"},\"checksum\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                        "{\"name\":\"pause_downloads\",\"description\":\"Pause a specific download by id or 'all'.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}}}}," +
                        "{\"name\":\"resume_downloads\",\"description\":\"Resume/start a specific download by id or 'all'.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"}}}}," +
                        "{\"name\":\"remove_downloads\",\"description\":\"Remove a download by id (optionally deleteFile=true).\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"id\":{\"type\":\"string\"},\"deleteFile\":{\"type\":\"boolean\"}},\"required\":[\"id\"]}}," +
                        "{\"name\":\"start_queue\",\"description\":\"Start the Main Download Queue.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{}}}," +
                        "{\"name\":\"probe_url\",\"description\":\"Probe a URL for final redirect target, Content-Length, filename, and HTTP Range support.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                        "{\"name\":\"video_info\",\"description\":\"Inspect media formats and title for a video URL via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                        "{\"name\":\"playlist_info\",\"description\":\"Inspect playlist items for a media playlist URL via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                        "{\"name\":\"torrent_info\",\"description\":\"Inspect a .torrent file, HTTP torrent URL, or magnet URI.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"target\":{\"type\":\"string\"}},\"required\":[\"target\"]}}," +
                        "{\"name\":\"start_video\",\"description\":\"Queue a media/video download job via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"},\"format\":{\"type\":\"string\"},\"audioOnly\":{\"type\":\"boolean\"}},\"required\":[\"url\"]}}," +
                        "{\"name\":\"start_playlist\",\"description\":\"Queue a playlist download job via yt-dlp.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                        "{\"name\":\"start_gallery\",\"description\":\"Queue an image gallery download job via gallery-dl.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"url\":{\"type\":\"string\"}},\"required\":[\"url\"]}}," +
                        "{\"name\":\"start_torrent\",\"description\":\"Queue a BitTorrent .torrent or magnet download job.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{\"target\":{\"type\":\"string\"}},\"required\":[\"target\"]}}," +
                        "{\"name\":\"check_updates\",\"description\":\"Check GitHub Releases for a newer version of MHS Download Manager.\",\"inputSchema\":{\"type\":\"object\",\"properties\":{}}}" +
                        "]}";
                    WriteJsonRpcResult(idRaw, toolsJson);
                }
                else if (method == "tools/call")
                {
                    cfg = store.LoadShihabConfig();
                    string toolName = ExtractToolCallName(line);
                    string toolText = ExecuteMcpToolCall(store, cfg, toolName, line);
                    string escaped = EscapeJsonString(toolText);
                    WriteJsonRpcResult(idRaw, "{\"content\":[{\"type\":\"text\",\"text\":\"" + escaped + "\"}]}");
                }
                else if (!string.IsNullOrEmpty(idRaw))
                {
                    WriteJsonRpcResult(idRaw, "{}");
                }
            }
            return 0;
        }

        private static string ExecuteMcpToolCall(ShihabStateStore store, MehediAppConfiguration cfg, string toolName, string rawJson)
        {
            string liveResp;
            if (toolName == "list_downloads")
            {
                if (TryCallLiveBridge(store, cfg, "GET", "/shihab/api/status", null, out liveResp) && !string.IsNullOrWhiteSpace(liveResp))
                {
                    return liveResp;
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                if (items.Count == 0) return "No downloads in list.";
                StringBuilder sb = new StringBuilder();
                for (int i = 0; i < items.Count; i++)
                {
                    ShihabDownloadItem it = items[i];
                    sb.AppendFormat("[{0}] {1} | {2:0.0}% of {3} | Status: {4} | URL: {5}\n",
                        it.ShihabId.Substring(0, Math.Min(8, it.ShihabId.Length)),
                        it.ShihabFileName,
                        it.GetMehediPercent(),
                        ShihabFormatHelpers.FormatMehediBytes(it.ShihabTotalBytes),
                        it.ShihabStatusText,
                        it.MehediUrl);
                }
                return sb.ToString().TrimEnd();
            }
            if (toolName == "add_download" || toolName == "start_video" || toolName == "start_playlist" || toolName == "start_gallery" || toolName == "start_torrent")
            {
                string url = ShihabDebridManager.ExtractJsonStringField(rawJson, "url");
                if (string.IsNullOrWhiteSpace(url)) url = ShihabDebridManager.ExtractJsonStringField(rawJson, "target");
                string fileName = ShihabDebridManager.ExtractJsonStringField(rawJson, "fileName");
                string checksum = ShihabDebridManager.ExtractJsonStringField(rawJson, "checksum");
                string fmt = ShihabDebridManager.ExtractJsonStringField(rawJson, "format");
                bool audioOnly = rawJson.IndexOf("\"audioOnly\":true", StringComparison.OrdinalIgnoreCase) >= 0;
                if (string.IsNullOrWhiteSpace(url)) return "Error: url/target is required.";

                string kind = toolName == "start_video" || toolName == "start_playlist" ? "ytdlp"
                    : (toolName == "start_gallery" ? "gallery" : (toolName == "start_torrent" ? "torrent" : "http"));
                QueueOrDispatchItem(store, cfg, url.Trim(), fileName, checksum, kind, fmt, audioOnly);
                return "Queued (" + kind + "): " + url.Trim();
            }
            if (toolName == "pause_downloads")
            {
                string id = ShihabDebridManager.ExtractJsonStringField(rawJson, "id");
                if (string.IsNullOrWhiteSpace(id)) id = "all";
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action",
                    "{\"action\":\"pause\",\"id\":\"" + EscapeJsonString(id) + "\"}", out liveResp))
                {
                    return "Paused download(s) via live engine: " + id;
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                int count = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (id == "all" || items[i].ShihabId.StartsWith(id, StringComparison.OrdinalIgnoreCase))
                    {
                        items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                        count++;
                    }
                }
                store.SaveShihabDownloads(items);
                return string.Format("Paused {0} download(s).", count);
            }
            if (toolName == "resume_downloads" || toolName == "start_queue")
            {
                string id = toolName == "start_queue" ? "all" : ShihabDebridManager.ExtractJsonStringField(rawJson, "id");
                if (string.IsNullOrWhiteSpace(id)) id = "all";
                string actName = toolName == "start_queue" ? "start_queue" : "resume";
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action",
                    "{\"action\":\"" + actName + "\",\"id\":\"" + EscapeJsonString(id) + "\"}", out liveResp))
                {
                    return "Started/resumed via live engine: " + id;
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                int count = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    if (items[i].GetShihabStatusEnum() != ShihabDownloadStatus.ShihabCompleted &&
                        (id == "all" || items[i].ShihabId.StartsWith(id, StringComparison.OrdinalIgnoreCase)))
                    {
                        items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                        count++;
                    }
                }
                store.SaveShihabDownloads(items);
                return string.Format("Queued {0} download(s) for resumption.", count);
            }
            if (toolName == "remove_downloads")
            {
                string id = ShihabDebridManager.ExtractJsonStringField(rawJson, "id");
                bool delFile = rawJson.IndexOf("\"deleteFile\":true", StringComparison.OrdinalIgnoreCase) >= 0;
                if (string.IsNullOrWhiteSpace(id)) return "Error: id is required.";
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action",
                    "{\"action\":\"" + (delFile ? "delete" : "remove") + "\",\"id\":\"" + EscapeJsonString(id) + "\"}", out liveResp))
                {
                    return (delFile ? "Deleted " : "Removed ") + id + " via live engine.";
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                int removed = 0;
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (items[i].ShihabId.StartsWith(id, StringComparison.OrdinalIgnoreCase))
                    {
                        if (delFile)
                        {
                            string fp = items[i].GetShihabFullTargetPath();
                            try { if (File.Exists(fp)) File.Delete(fp); } catch { }
                        }
                        items.RemoveAt(i);
                        removed++;
                    }
                }
                store.SaveShihabDownloads(items);
                return string.Format("Removed {0} download(s).", removed);
            }
            if (toolName == "probe_url")
            {
                string url = ShihabDebridManager.ExtractJsonStringField(rawJson, "url");
                if (string.IsNullOrWhiteSpace(url)) return "Error: url is required.";
                return ProbeSingleUrlSummary(url.Trim(), cfg);
            }
            if (toolName == "video_info" || toolName == "playlist_info")
            {
                string url = ShihabDebridManager.ExtractJsonStringField(rawJson, "url");
                if (string.IsNullOrWhiteSpace(url)) return "Error: url is required.";
                string infoJson, err;
                if (ShihabMediaHelpersManager.QueryVideoOrPlaylistInfo(store.MehediBaseDirectory, url.Trim(), toolName == "playlist_info", out infoJson, out err))
                {
                    return infoJson;
                }
                return "Error: " + err;
            }
            if (toolName == "torrent_info")
            {
                string target = ShihabDebridManager.ExtractJsonStringField(rawJson, "target");
                if (string.IsNullOrWhiteSpace(target)) target = ShihabDebridManager.ExtractJsonStringField(rawJson, "url");
                if (string.IsNullOrWhiteSpace(target)) return "Error: target is required.";
                try
                {
                    ShihabTorrentMetadata meta = ShihabTorrentManager.InspectTorrentOrMagnet(target.Trim());
                    return string.Format("Name: {0}\nInfoHash: {1}\nTotalSize: {2}\nFiles: {3}",
                        meta.Name, meta.InfoHashHex, ShihabFormatHelpers.FormatMehediBytes(meta.TotalSizeBytes), meta.Files.Count);
                }
                catch (Exception ex)
                {
                    return "Error: " + ex.Message;
                }
            }
            if (toolName == "check_updates")
            {
                ShihabUpdateCheckResult res = ShihabAutoUpdater.CheckForUpdates(cfg, true);
                if (!res.Ok) return "Update check failed: " + res.ErrorMessage;
                if (res.UpdateAvailable)
                {
                    string assetUrl = res.PrimaryAsset != null ? res.PrimaryAsset.BrowserDownloadUrl : res.HtmlUrl;
                    return string.Format("Update available: v{0} (installed: v{1}) | URL: {2}",
                        res.LatestVersion, res.CurrentVersion, assetUrl);
                }
                return string.Format("Up to date (v{0}).", res.CurrentVersion);
            }
            return "Unknown tool: " + toolName;
        }

        private static string ProbeSingleUrlSummary(string url, MehediAppConfiguration cfg)
        {
            try
            {
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.Method = "GET";
                req.UserAgent = (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabDefaultUserAgent)) ? cfg.ShihabDefaultUserAgent : "Mozilla/5.0";
                req.Timeout = 10000;
                req.AddRange(0, 0);
                ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, url);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    long total = resp.ContentLength;
                    string cr = resp.Headers["Content-Range"];
                    if (!string.IsNullOrEmpty(cr))
                    {
                        int slash = cr.LastIndexOf('/');
                        long parsedTotal;
                        if (slash >= 0 && long.TryParse(cr.Substring(slash + 1).Trim(), out parsedTotal))
                        {
                            total = parsedTotal;
                        }
                    }
                    bool resumable = resp.StatusCode == HttpStatusCode.PartialContent ||
                        string.Equals(resp.Headers["Accept-Ranges"], "bytes", StringComparison.OrdinalIgnoreCase);
                    string finalUrl = resp.ResponseUri != null ? resp.ResponseUri.ToString() : url;
                    string name = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(finalUrl);
                    return string.Format("FinalURL: {0}\nFileName: {1}\nSize: {2} ({3} bytes)\nResumable: {4}\nContentType: {5}",
                        finalUrl, name, ShihabFormatHelpers.FormatMehediBytes(total), total, resumable, resp.ContentType ?? "unknown");
                }
            }
            catch (Exception ex)
            {
                return "Probe error: " + ex.Message;
            }
        }

        private static void QueueOrDispatchItem(
            ShihabStateStore store,
            MehediAppConfiguration cfg,
            string targetUrl,
            string fileName,
            string checksum,
            string kind,
            string ytFormat,
            bool audioOnly)
        {
            string liveResp;
            string payload = string.Format(
                "{{\"url\":\"{0}\",\"fileName\":\"{1}\",\"checksum\":\"{2}\",\"kind\":\"{3}\",\"format\":\"{4}\",\"audioOnly\":{5}}}",
                EscapeJsonString(targetUrl),
                EscapeJsonString(fileName ?? ""),
                EscapeJsonString(checksum ?? ""),
                EscapeJsonString(kind ?? "http"),
                EscapeJsonString(ytFormat ?? ""),
                audioOnly ? "true" : "false");

            if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/add", payload, out liveResp))
            {
                return;
            }

            List<ShihabDownloadItem> items = store.LoadMehediDownloads();
            ShihabDownloadItem item = new ShihabDownloadItem();
            item.MehediUrl = targetUrl;
            item.ShihabFileName = string.IsNullOrWhiteSpace(fileName)
                ? ShihabFormatHelpers.ExtractMehediFileNameFromUrl(targetUrl)
                : ShihabFormatHelpers.SanitizeShihabFileName(fileName);
            item.ShihabExpectedChecksum = checksum ?? "";
            if (kind == "ytdlp")
            {
                item.ShihabIsYtDlpJob = true;
                item.ShihabYtDlpFormatSelector = ytFormat ?? "";
                item.ShihabAudioOnlyExtract = audioOnly;
            }
            else if (kind == "gallery")
            {
                item.ShihabIsGalleryJob = true;
            }
            else if (kind == "torrent" || targetUrl.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || targetUrl.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                item.ShihabIsTorrentOrMagnet = true;
            }
            ShihabCategoryRule cat = cfg.ResolveMehediCategoryForFile(item.ShihabFileName);
            item.ShihabCategoryName = cat.ShihabName;
            item.MehediSaveDirectory = cat.ShihabDefaultFolder;
            items.Insert(0, item);
            store.SaveShihabDownloads(items);
        }

        private static string EscapeJsonString(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return text
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }

        private static string ExtractToolCallName(string line)
        {
            Match m = Regex.Match(line, "\"params\"\\s*:\\s*\\{[^}]*\"name\"\\s*:\\s*\"([^\"]+)\"");
            return m.Success ? m.Groups[1].Value : "";
        }

        private static string ExtractJsonIdRaw(string line)
        {
            Match m = Regex.Match(line, "\"id\"\\s*:\\s*([0-9]+|\"[^\"]*\")");
            return m.Success ? m.Groups[1].Value : "1";
        }

        private static void WriteJsonRpcResult(string idRaw, string resultObjJson)
        {
            Console.WriteLine("{\"jsonrpc\":\"2.0\",\"id\":" + idRaw + ",\"result\":" + resultObjJson + "}");
            Console.Out.Flush();
        }

        public static int RunCliCommand(ShihabStateStore store, string[] args)
        {
            if (args == null || args.Length == 0) return 0;

            bool outputJson = false;
            List<string> cleaned = new List<string>();
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--json", StringComparison.OrdinalIgnoreCase))
                {
                    outputJson = true;
                    continue;
                }
                if (string.Equals(args[i], "--shihab-config-dir", StringComparison.OrdinalIgnoreCase))
                {
                    i++;
                    continue;
                }
                if (string.Equals(args[i], "--cli", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                cleaned.Add(args[i]);
            }

            if (cleaned.Count == 0)
            {
                PrintCliHelp();
                return 0;
            }

            string cmd = cleaned[0].ToLowerInvariant();
            MehediAppConfiguration cfg = store.LoadShihabConfig();

            if (cmd == "help" || cmd == "--help" || cmd == "-h")
            {
                PrintCliHelp();
                return 0;
            }
            if (cmd == "status" || cmd == "list")
            {
                string liveResp;
                if (TryCallLiveBridge(store, cfg, "GET", "/shihab/api/status", null, out liveResp) && !string.IsNullOrWhiteSpace(liveResp))
                {
                    if (outputJson)
                    {
                        Console.WriteLine(liveResp);
                        return 0;
                    }
                }
                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                if (outputJson)
                {
                    StringBuilder jb = new StringBuilder("[");
                    for (int i = 0; i < items.Count; i++)
                    {
                        ShihabDownloadItem it = items[i];
                        if (i > 0) jb.Append(",");
                        jb.AppendFormat(CultureInfo.InvariantCulture,
                            "{{\"id\":\"{0}\",\"fileName\":\"{1}\",\"status\":\"{2}\",\"percent\":{3:0.0},\"totalBytes\":{4},\"downloadedBytes\":{5},\"url\":\"{6}\"}}",
                            EscapeJsonString(it.ShihabId),
                            EscapeJsonString(it.ShihabFileName),
                            EscapeJsonString(it.ShihabStatusText),
                            it.GetMehediPercent(),
                            it.ShihabTotalBytes,
                            it.MehediDownloadedBytes,
                            EscapeJsonString(it.MehediUrl));
                    }
                    jb.Append("]");
                    Console.WriteLine(jb.ToString());
                    return 0;
                }
                Console.WriteLine("ID        | Status       | Progress | Size       | File Name");
                Console.WriteLine("----------+--------------+----------+------------+------------------------------");
                for (int i = 0; i < items.Count; i++)
                {
                    ShihabDownloadItem it = items[i];
                    string shortId = it.ShihabId.Substring(0, Math.Min(8, it.ShihabId.Length));
                    Console.WriteLine("{0,-9} | {1,-12} | {2,6:0.0}%  | {3,-10} | {4}",
                        shortId,
                        it.ShihabStatusText,
                        it.GetMehediPercent(),
                        ShihabFormatHelpers.FormatMehediBytes(it.ShihabTotalBytes),
                        it.ShihabFileName);
                }
                return 0;
            }
            if (cmd == "pair")
            {
                string code = ShihabRemoteControlServer.EnsureFreshPairingCode(cfg, delegate { store.SaveMehediConfig(cfg); }, false);
                string lanIp = ShihabRemoteControlServer.GetPrimaryLanIpAddress();
                string fullUrl = string.Format("http://{0}:{1}/#pair={2}", lanIp, cfg.ShihabRemoteWebPort, code);
                if (outputJson)
                {
                    Console.WriteLine("{{\"url\":\"{0}\",\"code\":\"{1}\",\"expiresUtc\":\"{2}\"}}",
                        EscapeJsonString(fullUrl), EscapeJsonString(code), EscapeJsonString(cfg.ShihabRemotePairingExpiresUtc));
                    return 0;
                }
                Console.WriteLine("Remote Web UI URL : {0}", fullUrl);
                Console.WriteLine("8-Digit Pair Code : {0} (single-use, expires in 10 min)", code);
                Console.WriteLine();
                Console.WriteLine(ShihabQrCodeEncoder.RenderTerminalUnicode(fullUrl));
                return 0;
            }
            if (cmd == "api")
            {
                if (cleaned.Count > 1)
                {
                    string mode = cleaned[1].ToLowerInvariant();
                    cfg.ShihabAutomationApiEnabled = (mode == "on" || mode == "true" || mode == "1");
                    store.SaveMehediConfig(cfg);
                }
                if (outputJson)
                {
                    Console.WriteLine("{{\"automationApiEnabled\":{0},\"port\":{1}}}",
                        cfg.ShihabAutomationApiEnabled ? "true" : "false", cfg.MehediLocalBridgePort);
                }
                else
                {
                    Console.WriteLine("Local Automation API: {0}", cfg.ShihabAutomationApiEnabled ? "ON" : "OFF");
                }
                return 0;
            }
            if (cmd == "check-update" || cmd == "check-updates")
            {
                ShihabUpdateCheckResult res = ShihabAutoUpdater.CheckForUpdates(cfg, true);
                cfg.ShihabLastUpdateCheckUtc = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);
                store.SaveMehediConfig(cfg);
                if (!res.Ok)
                {
                    if (outputJson)
                        Console.WriteLine("{{\"ok\":false,\"error\":\"{0}\"}}", EscapeJsonString(res.ErrorMessage));
                    else
                        Console.Error.WriteLine("Update check failed: " + res.ErrorMessage);
                    return 1;
                }
                if (outputJson)
                {
                    string assetUrl = res.PrimaryAsset != null ? res.PrimaryAsset.BrowserDownloadUrl : "";
                    string assetName = res.PrimaryAsset != null ? res.PrimaryAsset.Name : "";
                    Console.WriteLine(
                        "{{\"ok\":true,\"updateAvailable\":{0},\"currentVersion\":\"{1}\",\"latestVersion\":\"{2}\",\"installKind\":\"{3}\",\"assetName\":\"{4}\",\"assetUrl\":\"{5}\"}}",
                        res.UpdateAvailable ? "true" : "false",
                        EscapeJsonString(res.CurrentVersion),
                        EscapeJsonString(res.LatestVersion),
                        EscapeJsonString(res.InstallMode.ToString()),
                        EscapeJsonString(assetName),
                        EscapeJsonString(assetUrl));
                    return 0;
                }
                if (res.UpdateAvailable)
                {
                    string assetName = res.PrimaryAsset != null ? res.PrimaryAsset.Name : "N/A";
                    long assetSize = res.PrimaryAsset != null ? res.PrimaryAsset.Size : 0L;
                    Console.WriteLine("Update Available : v{0} (installed: v{1})", res.LatestVersion, res.CurrentVersion);
                    Console.WriteLine("Install Mode     : {0}", res.InstallMode);
                    Console.WriteLine("Package Asset    : {0} ({1})", assetName, ShihabFormatHelpers.FormatMehediBytes(assetSize));
                    Console.WriteLine("Release Page     : {0}", res.HtmlUrl);
                }
                else
                {
                    Console.WriteLine("MHS Download Manager v{0} is up to date.", res.CurrentVersion);
                }
                return 0;
            }
            if (cmd == "update")
            {
                bool verifyOnly = cleaned.Contains("--verify-only");
                ShihabUpdateCheckResult res = ShihabAutoUpdater.CheckForUpdates(cfg, true);
                if (!res.Ok)
                {
                    Console.Error.WriteLine("Update check failed: " + res.ErrorMessage);
                    return 1;
                }
                if (!res.UpdateAvailable)
                {
                    Console.WriteLine("Already running latest version v{0}.", res.CurrentVersion);
                    return 0;
                }
                string assetName = res.PrimaryAsset != null ? res.PrimaryAsset.Name : "update";
                Console.WriteLine("Downloading update v{0} ({1})...", res.LatestVersion, assetName);
                string verifiedPrimary, verifiedExtZip, verifyErr;
                bool verifiedOk = ShihabAutoUpdater.DownloadAndVerifyReleaseUpdate(
                    res,
                    cfg,
                    store.MehediBaseDirectory,
                    null,
                    CancellationToken.None,
                    out verifiedPrimary,
                    out verifiedExtZip,
                    out verifyErr);
                if (!verifiedOk)
                {
                    Console.Error.WriteLine("Update verification failed: " + verifyErr);
                    return 2;
                }
                Console.WriteLine("SHA-256 Verified : OK (ChecksumAsset={0})", res.Sha256SumsAsset != null ? res.Sha256SumsAsset.Name : "inline/none");
                Console.WriteLine("Staged Package   : {0}", verifiedPrimary);
                if (verifyOnly)
                {
                    return 0;
                }
                string applyErr;
                if (!ShihabAutoUpdater.ApplyVerifiedUpdateAndRestart(res, verifiedPrimary, verifiedExtZip, null, out applyErr))
                {
                    Console.Error.WriteLine("Apply update failed: " + applyErr);
                    return 3;
                }
                Console.WriteLine("Update installer launched successfully.");
                return 0;
            }
            if ((cmd == "video-info" || cmd == "playlist-info") && cleaned.Count > 1)
            {
                string infoJson, err;
                if (!ShihabMediaHelpersManager.QueryVideoOrPlaylistInfo(store.MehediBaseDirectory, cleaned[1], cmd == "playlist-info", out infoJson, out err))
                {
                    Console.Error.WriteLine(err);
                    return 1;
                }
                Console.WriteLine(infoJson);
                return 0;
            }
            if (cmd == "torrent-info" && cleaned.Count > 1)
            {
                string path = cleaned[1];
                ShihabTorrentMetadata meta = ShihabTorrentManager.InspectTorrentOrMagnet(path);
                if (outputJson)
                {
                    Console.WriteLine("{{\"name\":\"{0}\",\"infoHash\":\"{1}\",\"totalBytes\":{2},\"fileCount\":{3}}}",
                        EscapeJsonString(meta.Name), EscapeJsonString(meta.InfoHashHex), meta.TotalSizeBytes, meta.Files.Count);
                    return 0;
                }
                Console.WriteLine("Name      : {0}", meta.Name);
                Console.WriteLine("InfoHash  : {0}", meta.InfoHashHex);
                Console.WriteLine("TotalSize : {0}", ShihabFormatHelpers.FormatMehediBytes(meta.TotalSizeBytes));
                Console.WriteLine("Files     : {0}", meta.Files.Count);
                for (int i = 0; i < meta.Files.Count; i++)
                {
                    Console.WriteLine("  [{0}] {1} ({2})", meta.Files[i].Index, meta.Files[i].RelativePath, ShihabFormatHelpers.FormatMehediBytes(meta.Files[i].SizeBytes));
                }
                return 0;
            }
            if (cmd == "torrent-create" && cleaned.Count > 1)
            {
                string src = cleaned[1];
                string outFile = (cleaned.Count > 2) ? cleaned[2] : (src.TrimEnd('\\', '/') + ".torrent");
                ShihabTorrentManager.CreateTorrentFile(src, outFile, new List<string>(), new List<string>(), "Created by MHS Download Manager", 262144, false);
                Console.WriteLine("Created torrent: {0}", Path.GetFullPath(outFile));
                return 0;
            }
            if (cmd == "install-ytdlp" || cmd == "install-ffmpeg" || cmd == "install-gallery-dl")
            {
                string tool = cmd.Substring("install-".Length);
                string err;
                bool ok = ShihabMediaHelpersManager.InstallOrUpdateTool(store.MehediBaseDirectory, tool, Console.WriteLine, out err);
                if (!ok) { Console.Error.WriteLine(err); return 1; }
                return 0;
            }
            if ((cmd == "get" || cmd == "video-get" || cmd == "playlist-get" || cmd == "gallery-get" || cmd == "torrent-get") && cleaned.Count > 1)
            {
                string targetUrl = cleaned[1];
                string kind = (cmd == "video-get" || cmd == "playlist-get") ? "ytdlp"
                    : (cmd == "gallery-get" ? "gallery" : (cmd == "torrent-get" ? "torrent" : "http"));
                QueueOrDispatchItem(store, cfg, targetUrl, "", "", kind, "", false);
                Console.WriteLine("Queued ({0}): {1}", kind, targetUrl);
                return 0;
            }
            if ((cmd == "pause" || cmd == "start" || cmd == "resume" || cmd == "remove" || cmd == "delete") && cleaned.Count > 1)
            {
                string targetId = cleaned[1];
                string liveResp;
                if (TryCallLiveBridge(store, cfg, "POST", "/shihab/api/action",
                    "{\"action\":\"" + EscapeJsonString(cmd) + "\",\"id\":\"" + EscapeJsonString(targetId) + "\"}", out liveResp))
                {
                    Console.WriteLine("Completed live command '{0}' on '{1}'.", cmd, targetId);
                    return 0;
                }

                List<ShihabDownloadItem> items = store.LoadMehediDownloads();
                for (int i = items.Count - 1; i >= 0; i--)
                {
                    if (targetId == "all" || items[i].ShihabId.StartsWith(targetId, StringComparison.OrdinalIgnoreCase))
                    {
                        if (cmd == "pause") items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabPaused, "Paused");
                        else if (cmd == "start" || cmd == "resume") items[i].SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                        else if (cmd == "remove" || cmd == "delete")
                        {
                            if (cmd == "delete")
                            {
                                string fp = items[i].GetShihabFullTargetPath();
                                try { if (File.Exists(fp)) File.Delete(fp); } catch { }
                            }
                            items.RemoveAt(i);
                        }
                    }
                }
                store.SaveShihabDownloads(items);
                Console.WriteLine("Completed command '{0}' on '{1}'.", cmd, targetId);
                return 0;
            }

            PrintCliHelp();
            return 0;
        }

        private static void PrintCliHelp()
        {
            Console.WriteLine("MHS Download Manager CLI & Automation Commands:");
            Console.WriteLine("  mdm.exe --cli status [--json]                 List all downloads and live progress");
            Console.WriteLine("  mdm.exe --cli get <URL>                       Add a HTTP/HTTPS/FTP/Magnet download");
            Console.WriteLine("  mdm.exe --cli pause <id|all>                  Pause download(s)");
            Console.WriteLine("  mdm.exe --cli start <id|all>                  Resume/start download(s)");
            Console.WriteLine("  mdm.exe --cli remove <id|all>                 Remove download(s) from list");
            Console.WriteLine("  mdm.exe --cli delete <id|all>                 Remove download(s) and delete local file");
            Console.WriteLine("  mdm.exe --cli check-update [--json]           Check GitHub Releases for new version");
            Console.WriteLine("  mdm.exe --cli update [--verify-only]          Download, SHA-256 verify, and apply update");
            Console.WriteLine("  mdm.exe --cli pair [--json]                   Print LAN Remote Web UI URL, code & QR");
            Console.WriteLine("  mdm.exe --cli api <on|off>                    Enable or disable local automation API");
            Console.WriteLine("  mdm.exe --cli video-info <URL>                Query video formats via yt-dlp");
            Console.WriteLine("  mdm.exe --cli playlist-info <URL>             Query playlist entries via yt-dlp");
            Console.WriteLine("  mdm.exe --cli video-get <URL>                 Download video via yt-dlp");
            Console.WriteLine("  mdm.exe --cli gallery-get <URL>               Download image gallery via gallery-dl");
            Console.WriteLine("  mdm.exe --cli torrent-info <file|magnet|url>  Inspect .torrent or magnet metadata");
            Console.WriteLine("  mdm.exe --cli torrent-create <src> [out]      Create a .torrent file from file or folder");
            Console.WriteLine("  mdm.exe --cli install-ytdlp                   Install/update yt-dlp helper (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --cli install-ffmpeg                  Install/update ffmpeg helper (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --cli install-gallery-dl              Install/update gallery-dl helper (SHA-256 verified)");
            Console.WriteLine("  mdm.exe --mcp                                 Run Model Context Protocol stdio server");
            Console.WriteLine("  mdm.exe --headless                            Run background download daemon without GUI");
            Console.WriteLine("  mdm.exe --forget-sign-ins                     Wipe all saved credentials & tokens");
        }
    }

    // =========================================================================
    // 12. OUT-OF-PROCESS CRASH MONITOR & CUSTOM THEME / LOCALIZATION ENGINE
    // =========================================================================
    public static class ShihabCrashMonitor
    {
        private static System.Windows.Forms.Timer _heartbeatTimer;
        private static string _heartbeatFilePath;
        private static string _crashDirectory;
        private static string _activeSessionMarkerPath;

        public static void InitializeInMainProcess(string baseDataDir, bool enabled)
        {
            _crashDirectory = Path.Combine(baseDataDir, "crashes");
            Directory.CreateDirectory(_crashDirectory);
            _heartbeatFilePath = Path.Combine(_crashDirectory, "heartbeat.tick");
            _activeSessionMarkerPath = Path.Combine(_crashDirectory, "session.running");

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

            if (!enabled) return;

            try
            {
                File.WriteAllText(_activeSessionMarkerPath, Process.GetCurrentProcess().Id.ToString(CultureInfo.InvariantCulture));
            }
            catch { }

            TouchHeartbeatFile();
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
                if (_heartbeatTimer != null) _heartbeatTimer.Stop();
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
                    try { latestCrashLogPath = File.ReadAllText(dirtyMarker).Trim(); } catch { }
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
                psi.Arguments = string.Format("--crash-monitor {0} \"{1}\"", myPid, baseDataDir.TrimEnd('\\'));
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(psi);
            }
            catch
            {
            }
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

                Process parent = null;
                try { parent = Process.GetProcessById(parentPid); } catch { return 0; }
                if (parent == null) return 0;

                parent.WaitForExit();
                int exitCode = 0;
                try { exitCode = parent.ExitCode; } catch { }

                bool sessionStillMarkedRunning = File.Exists(sessionRunning);
                if (exitCode != 0 || sessionStillMarkedRunning)
                {
                    try { if (File.Exists(sessionRunning)) File.Delete(sessionRunning); } catch { }
                    string logFile = Path.Combine(crashDir, string.Format("crash_{0:yyyyMMdd_HHmmss}_watchdog.log", DateTime.Now));
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("MHS Download Manager Out-of-Process Crash Watchdog Report");
                    sb.AppendLine("Timestamp : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                    sb.AppendLine("OS        : " + Environment.OSVersion);
                    sb.AppendLine("ExitCode  : 0x" + exitCode.ToString("X8") + " (" + exitCode + ")");
                    sb.AppendLine("Reason    : Monitored main process exited abnormally without clean shutdown marker.");
                    File.WriteAllText(logFile, ScrubSensitivePaths(sb.ToString()), Encoding.UTF8);
                    File.WriteAllText(dirtyMarker, logFile, Encoding.UTF8);
                }
            }
            catch
            {
            }
            return 0;
        }

        private static void TouchHeartbeatFile()
        {
            try
            {
                File.WriteAllText(_heartbeatFilePath, DateTime.UtcNow.Ticks.ToString(CultureInfo.InvariantCulture));
            }
            catch
            {
            }
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
                    result = Regex.Replace(result, Regex.Escape(userProfile), "<USERPROFILE>", RegexOptions.IgnoreCase);
                }
                string userName = Environment.UserName;
                if (!string.IsNullOrEmpty(userName) && userName.Length >= 3)
                {
                    result = Regex.Replace(result, "\\\\Users\\\\" + Regex.Escape(userName), "\\Users\\<USER>", RegexOptions.IgnoreCase);
                }
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
                string file = Path.Combine(_crashDirectory, string.Format("crash_{0:yyyyMMdd_HHmmss}.log", DateTime.Now));
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("MHS Download Manager Diagnostic Crash Report");
                sb.AppendLine("Version   : " + ShihabAutoUpdater.GetCurrentAppVersion());
                sb.AppendLine("Timestamp : " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
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
            catch
            {
            }
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
            catch
            {
            }
            return list;
        }
    }

    public class ShihabCustomThemePreset
    {
        public string Name;
        public Color BackgroundMain;
        public Color BackgroundCard;
        public Color BackgroundSidebar;
        public Color AccentPrimary;
        public Color TextPrimary;
        public Color TextSecondary;
        public Color GridAlternate;
    }

    public static class ShihabThemeManager
    {
        public static readonly string[] BuiltInThemeNames = new string[]
        {
            "Obsidian Dark",
            "Midnight Nord",
            "Emerald Matrix",
            "Solarized Dark",
            "Classic Light"
        };

        public static readonly string[] SupportedLanguages = new string[]
        {
            "en - English",
            "bn - বাংলা (Bengali)",
            "es - Español",
            "fr - Français",
            "de - Deutsch",
            "ru - Русский",
            "zh - 中文 (Simplified)",
            "ja - 日本語",
            "ar - العربية",
            "pt - Português",
            "hi - हिन्दी",
            "tr - Türkçe",
            "ko - 한국어",
            "it - Italiano"
        };

        public static List<string> GetAllAvailableThemeNames(string baseDataDir)
        {
            List<string> names = new List<string>(BuiltInThemeNames);
            try
            {
                if (!string.IsNullOrWhiteSpace(baseDataDir))
                {
                    string themesDir = Path.Combine(baseDataDir, "themes");
                    if (Directory.Exists(themesDir))
                    {
                        string[] tomls = Directory.GetFiles(themesDir, "*.toml");
                        for (int i = 0; i < tomls.Length; i++)
                        {
                            string stem = Path.GetFileNameWithoutExtension(tomls[i]);
                            if (!string.IsNullOrWhiteSpace(stem) && !names.Contains(stem))
                            {
                                names.Add(stem);
                            }
                        }
                    }
                }
            }
            catch { }
            return names;
        }

        public static List<ShihabCustomThemePreset> GetBuiltInThemes()
        {
            List<ShihabCustomThemePreset> list = new List<ShihabCustomThemePreset>();
            for (int i = 0; i < BuiltInThemeNames.Length; i++)
            {
                list.Add(ResolveThemePalette(BuiltInThemeNames[i], ""));
            }
            return list;
        }

        public static bool IsDarkThemePreset(ShihabCustomThemePreset preset)
        {
            if (preset == null) return true;
            return ComputeRelativeLuminance(preset.BackgroundMain) < 0.5;
        }

        public static ShihabCustomThemePreset ResolveThemePalette(string themeName, string baseDataDir)
        {
            string t = (themeName ?? "Obsidian Dark").Trim();
            if (!string.IsNullOrWhiteSpace(baseDataDir))
            {
                ShihabCustomThemePreset custom = TryLoadCustomTomlTheme(t, baseDataDir);
                if (custom != null) return custom;
            }
            if (string.Equals(t, "Classic Light", StringComparison.OrdinalIgnoreCase))
            {
                return new ShihabCustomThemePreset
                {
                    Name = "Classic Light",
                    BackgroundMain = Color.FromArgb(241, 245, 249),
                    BackgroundCard = Color.FromArgb(255, 255, 255),
                    BackgroundSidebar = Color.FromArgb(226, 232, 240),
                    AccentPrimary = Color.FromArgb(2, 132, 199),
                    TextPrimary = Color.FromArgb(15, 23, 42),
                    TextSecondary = Color.FromArgb(71, 85, 105),
                    GridAlternate = Color.FromArgb(248, 250, 252)
                };
            }
            if (string.Equals(t, "Midnight Nord", StringComparison.OrdinalIgnoreCase))
            {
                return new ShihabCustomThemePreset
                {
                    Name = "Midnight Nord",
                    BackgroundMain = Color.FromArgb(46, 52, 64),
                    BackgroundCard = Color.FromArgb(59, 66, 82),
                    BackgroundSidebar = Color.FromArgb(36, 41, 51),
                    AccentPrimary = Color.FromArgb(136, 192, 208),
                    TextPrimary = Color.FromArgb(236, 239, 244),
                    TextSecondary = Color.FromArgb(216, 222, 233),
                    GridAlternate = Color.FromArgb(67, 76, 94)
                };
            }
            if (string.Equals(t, "Emerald Matrix", StringComparison.OrdinalIgnoreCase))
            {
                return new ShihabCustomThemePreset
                {
                    Name = "Emerald Matrix",
                    BackgroundMain = Color.FromArgb(6, 20, 14),
                    BackgroundCard = Color.FromArgb(11, 34, 24),
                    BackgroundSidebar = Color.FromArgb(4, 15, 10),
                    AccentPrimary = Color.FromArgb(16, 185, 129),
                    TextPrimary = Color.FromArgb(209, 250, 229),
                    TextSecondary = Color.FromArgb(110, 231, 183),
                    GridAlternate = Color.FromArgb(14, 42, 30)
                };
            }
            if (string.Equals(t, "Solarized Dark", StringComparison.OrdinalIgnoreCase))
            {
                return new ShihabCustomThemePreset
                {
                    Name = "Solarized Dark",
                    BackgroundMain = Color.FromArgb(0, 43, 54),
                    BackgroundCard = Color.FromArgb(7, 54, 66),
                    BackgroundSidebar = Color.FromArgb(0, 33, 43),
                    AccentPrimary = Color.FromArgb(38, 139, 210),
                    TextPrimary = Color.FromArgb(238, 232, 213),
                    TextSecondary = Color.FromArgb(147, 161, 161),
                    GridAlternate = Color.FromArgb(10, 62, 76)
                };
            }
            return new ShihabCustomThemePreset
            {
                Name = "Obsidian Dark",
                BackgroundMain = Color.FromArgb(14, 18, 28),
                BackgroundCard = Color.FromArgb(21, 27, 40),
                BackgroundSidebar = Color.FromArgb(17, 22, 33),
                AccentPrimary = Color.FromArgb(14, 165, 233),
                TextPrimary = Color.FromArgb(241, 245, 249),
                TextSecondary = Color.FromArgb(148, 163, 184),
                GridAlternate = Color.FromArgb(25, 32, 48)
            };
        }

        private static ShihabCustomThemePreset TryLoadCustomTomlTheme(string themeName, string baseDataDir)
        {
            try
            {
                string themesDir = Path.Combine(baseDataDir, "themes");
                if (!Directory.Exists(themesDir)) return null;
                string candidatePath = Path.Combine(themesDir, themeName + ".toml");
                if (!File.Exists(candidatePath)) return null;

                ShihabCustomThemePreset preset = ResolveThemePalette("Obsidian Dark", "");
                preset.Name = themeName;
                string[] lines = File.ReadAllLines(candidatePath, Encoding.UTF8);
                for (int i = 0; i < lines.Length; i++)
                {
                    string ln = lines[i].Trim();
                    if (ln.Length == 0 || ln.StartsWith("#")) continue;
                    int eq = ln.IndexOf('=');
                    if (eq <= 0) continue;
                    string key = ln.Substring(0, eq).Trim().ToLowerInvariant();
                    string val = ln.Substring(eq + 1).Trim().Trim('"', '\'');
                    Color parsed;
                    if (key == "name" && !string.IsNullOrWhiteSpace(val)) preset.Name = val;
                    else if ((key == "bg_main" || key == "background_main") && TryParseHexColor(val, out parsed)) preset.BackgroundMain = parsed;
                    else if ((key == "bg_card" || key == "background_card") && TryParseHexColor(val, out parsed)) preset.BackgroundCard = parsed;
                    else if ((key == "bg_sidebar" || key == "background_sidebar") && TryParseHexColor(val, out parsed)) preset.BackgroundSidebar = parsed;
                    else if ((key == "accent" || key == "accent_primary") && TryParseHexColor(val, out parsed)) preset.AccentPrimary = parsed;
                    else if (key == "text_primary" && TryParseHexColor(val, out parsed)) preset.TextPrimary = parsed;
                    else if (key == "text_secondary" && TryParseHexColor(val, out parsed)) preset.TextSecondary = parsed;
                    else if ((key == "grid_alt" || key == "grid_alternate") && TryParseHexColor(val, out parsed)) preset.GridAlternate = parsed;
                }

                // Enforce WCAG >= 4.5:1 contrast guard on primary text
                if (ComputeContrastRatio(preset.TextPrimary, preset.BackgroundMain) < 4.5)
                {
                    preset.TextPrimary = ComputeRelativeLuminance(preset.BackgroundMain) < 0.5
                        ? Color.FromArgb(241, 245, 249)
                        : Color.FromArgb(15, 23, 42);
                }
                if (ComputeContrastRatio(preset.TextSecondary, preset.BackgroundCard) < 3.0)
                {
                    preset.TextSecondary = ComputeRelativeLuminance(preset.BackgroundCard) < 0.5
                        ? Color.FromArgb(148, 163, 184)
                        : Color.FromArgb(71, 85, 105);
                }
                return preset;
            }
            catch
            {
                return null;
            }
        }

        private static bool TryParseHexColor(string hex, out Color color)
        {
            color = Color.Black;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            string s = hex.Trim().TrimStart('#');
            if (s.Length != 6) return false;
            int rgb;
            if (!int.TryParse(s, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out rgb)) return false;
            color = Color.FromArgb((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return true;
        }

        private static double ComputeRelativeLuminance(Color c)
        {
            double r = LinearizeChannel(c.R / 255.0);
            double g = LinearizeChannel(c.G / 255.0);
            double b = LinearizeChannel(c.B / 255.0);
            return 0.2126 * r + 0.7152 * g + 0.0722 * b;
        }

        private static double LinearizeChannel(double v)
        {
            return v <= 0.03928 ? v / 12.92 : Math.Pow((v + 0.055) / 1.055, 2.4);
        }

        private static double ComputeContrastRatio(Color a, Color b)
        {
            double l1 = ComputeRelativeLuminance(a);
            double l2 = ComputeRelativeLuminance(b);
            double lighter = Math.Max(l1, l2);
            double darker = Math.Min(l1, l2);
            return (lighter + 0.05) / (darker + 0.05);
        }

        public static string ExtractLanguageIsoCode(string languageSetting)
        {
            if (string.IsNullOrWhiteSpace(languageSetting)) return "en";
            int dash = languageSetting.IndexOf('-');
            string code = (dash > 0 ? languageSetting.Substring(0, dash) : languageSetting).Trim().ToLowerInvariant();
            return string.IsNullOrEmpty(code) ? "en" : code;
        }

        public static bool IsRightToLeftLanguage(string languageSetting)
        {
            string iso = ExtractLanguageIsoCode(languageSetting);
            return iso == "ar" || iso == "he" || iso == "fa" || iso == "ur";
        }

        public static string TranslateUiString(string languageSetting, string key, string fallbackEnglish)
        {
            string iso = ExtractLanguageIsoCode(languageSetting);
            if (iso == "en" || string.IsNullOrEmpty(key)) return fallbackEnglish;

            if (iso == "bn")
            {
                if (key == "menu_tasks") return "টাস্ক (&T)";
                if (key == "menu_file") return "ফাইল (&F)";
                if (key == "menu_downloads") return "ডাউনলোড (&D)";
                if (key == "menu_view") return "ভিউ (&V)";
                if (key == "menu_tools") return "টুলস (&L)";
                if (key == "menu_help") return "সাহায্য (&H)";
                if (key == "btn_add_url") return "URL যোগ করুন";
                if (key == "btn_resume") return "চালু করুন";
                if (key == "btn_stop") return "থামান";
                if (key == "btn_stop_all") return "সব থামান";
                if (key == "btn_delete") return "মুছুন";
                if (key == "btn_options") return "অপশন";
                if (key == "btn_scheduler") return "শিডিউলার";
                if (key == "btn_start_queue") return "কিউ শুরু";
                if (key == "btn_stop_queue") return "কিউ বন্ধ";
                if (key == "btn_grabber") return "গ্র্যাবার";
                if (key == "check_updates") return "আপডেট চেক করুন...";
            }
            else if (iso == "es")
            {
                if (key == "btn_add_url") return "Añadir URL";
                if (key == "btn_resume") return "Reanudar";
                if (key == "btn_stop") return "Pausar";
                if (key == "btn_stop_all") return "Pausar todo";
                if (key == "btn_delete") return "Eliminar";
                if (key == "btn_options") return "Opciones";
                if (key == "btn_scheduler") return "Programador";
                if (key == "check_updates") return "Buscar actualizaciones...";
            }
            else if (iso == "fr")
            {
                if (key == "btn_add_url") return "Ajouter URL";
                if (key == "btn_resume") return "Reprendre";
                if (key == "btn_stop") return "Pause";
                if (key == "btn_delete") return "Supprimer";
                if (key == "btn_options") return "Options";
                if (key == "check_updates") return "Vérifier les mises à jour...";
            }
            else if (iso == "de")
            {
                if (key == "btn_add_url") return "URL hinzufügen";
                if (key == "btn_resume") return "Fortsetzen";
                if (key == "btn_stop") return "Stopp";
                if (key == "btn_delete") return "Löschen";
                if (key == "btn_options") return "Optionen";
                if (key == "check_updates") return "Nach Updates suchen...";
            }
            else if (iso == "ar")
            {
                if (key == "btn_add_url") return "إضافة رابط";
                if (key == "btn_resume") return "استئناف";
                if (key == "btn_stop") return "إيقاف";
                if (key == "btn_stop_all") return "إيقاف الكل";
                if (key == "btn_delete") return "حذف";
                if (key == "btn_options") return "الخيارات";
                if (key == "btn_scheduler") return "الجدولة";
                if (key == "check_updates") return "التحقق من التحديثات...";
            }
            else if (iso == "ru")
            {
                if (key == "btn_add_url") return "Добавить URL";
                if (key == "btn_resume") return "Продолжить";
                if (key == "btn_stop") return "Пауза";
                if (key == "btn_delete") return "Удалить";
                if (key == "btn_options") return "Настройки";
                if (key == "check_updates") return "Проверить обновления...";
            }
            else if (iso == "zh")
            {
                if (key == "btn_add_url") return "新建任务";
                if (key == "btn_resume") return "继续";
                if (key == "btn_stop") return "暂停";
                if (key == "btn_delete") return "删除";
                if (key == "btn_options") return "选项";
                if (key == "check_updates") return "检查更新...";
            }
            else if (iso == "ja")
            {
                if (key == "btn_add_url") return "URL追加";
                if (key == "btn_resume") return "再開";
                if (key == "btn_stop") return "停止";
                if (key == "btn_delete") return "削除";
                if (key == "btn_options") return "設定";
                if (key == "check_updates") return "更新を確認...";
            }
            return fallbackEnglish;
        }
    }
}
