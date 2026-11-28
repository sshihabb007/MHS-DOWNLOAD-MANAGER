using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace ShihabMehediShared
{
    /// <summary>
    /// Shared fail-closed path, archive, URL/SSRF, process-quoting, and PID identity guards
    /// compiled into both mdm.exe and Setup.exe. Must stay C# 5 (.NET Framework 4.8) compatible.
    /// </summary>
    internal static class ShihabProcessAndPathSafety
    {
        private static readonly Regex GitHubSlugRegex = new Regex(
            @"^[A-Za-z0-9](?:[A-Za-z0-9-]{0,38})/[A-Za-z0-9._-]{1,100}$",
            RegexOptions.CultureInvariant);

        private static readonly object CurrentProcessCacheLock = new object();
        private static int _cachedCurrentProcessId;
        private static string _cachedCurrentExePath;

        internal static int CurrentProcessId
        {
            get
            {
                if (_cachedCurrentProcessId != 0) return _cachedCurrentProcessId;
                lock (CurrentProcessCacheLock)
                {
                    if (_cachedCurrentProcessId == 0)
                    {
                        using (Process cur = Process.GetCurrentProcess())
                        {
                            _cachedCurrentProcessId = cur.Id;
                            if (string.IsNullOrEmpty(_cachedCurrentExePath))
                            {
                                try
                                {
                                    ProcessModule mod = cur.MainModule;
                                    if (mod != null && !string.IsNullOrWhiteSpace(mod.FileName))
                                    {
                                        _cachedCurrentExePath = Path.GetFullPath(mod.FileName);
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    return _cachedCurrentProcessId;
                }
            }
        }

        internal static string CurrentExePath
        {
            get
            {
                if (!string.IsNullOrEmpty(_cachedCurrentExePath)) return _cachedCurrentExePath;
                lock (CurrentProcessCacheLock)
                {
                    if (string.IsNullOrEmpty(_cachedCurrentExePath))
                    {
                        using (Process cur = Process.GetCurrentProcess())
                        {
                            _cachedCurrentProcessId = cur.Id;
                            try
                            {
                                ProcessModule mod = cur.MainModule;
                                if (mod != null && !string.IsNullOrWhiteSpace(mod.FileName))
                                {
                                    _cachedCurrentExePath = Path.GetFullPath(mod.FileName);
                                }
                            }
                            catch { }
                        }
                        if (string.IsNullOrEmpty(_cachedCurrentExePath))
                        {
                            try
                            {
                                _cachedCurrentExePath = Path.GetFullPath(System.Reflection.Assembly.GetEntryAssembly().Location);
                            }
                            catch { _cachedCurrentExePath = string.Empty; }
                        }
                    }
                    return _cachedCurrentExePath;
                }
            }
        }

        private static readonly HashSet<string> WindowsReservedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "CLOCK$", "CONIN$", "CONOUT$",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "COM\u00B9", "COM\u00B2", "COM\u00B3",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
            "LPT\u00B9", "LPT\u00B2", "LPT\u00B3"
        };

#if !SHIHAB_INSTALLER
        private static readonly HashSet<string> AllowedSystem32Tools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cmd.exe",
            "explorer.exe",
            "shutdown.exe"
        };
#endif

        private const uint HANDLE_FLAG_INHERIT = 0x00000001u;
        private const int MOVEFILE_REPLACE_EXISTING = 0x1;
        private const int MOVEFILE_WRITE_THROUGH = 0x8;
        private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000u;

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetHandleInformation(IntPtr hObject, uint dwMask, uint dwFlags);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, int dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageNameW(IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref int lpdwSize);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        internal static void DisableHandleInheritance(IntPtr handle)
        {
            if (handle == IntPtr.Zero || handle == new IntPtr(-1)) return;
            try { SetHandleInformation(handle, HANDLE_FLAG_INHERIT, 0u); } catch { }
        }

        internal static void DisableListenerInheritance(TcpListener listener)
        {
            if (listener == null) return;
            try
            {
                Socket srv = listener.Server;
                if (srv != null)
                {
                    DisableHandleInheritance(srv.Handle);
                }
            }
            catch { }
        }

        internal static void ReplaceFileAtomicallyWithRenameFallback(string stagedTempFile, string destPath)
        {
            for (int attempt = 0; attempt < 3; attempt++)
            {
                if (MoveFileExW(stagedTempFile, destPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                {
                    return;
                }
                // If destination is a running PE or file allowing rename, move it aside first before placing the staged file.
                string backupOld = destPath + ".old";
                try { if (File.Exists(backupOld)) File.Delete(backupOld); } catch { }
                if (File.Exists(destPath))
                {
                    try { File.Move(destPath, backupOld); } catch { }
                }
                if (MoveFileExW(stagedTempFile, destPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                {
                    try { if (File.Exists(backupOld)) File.Delete(backupOld); } catch { }
                    return;
                }
                if (attempt < 2)
                {
                    System.Threading.Thread.Sleep(60);
                }
            }

            // Final fallback: when a browser holds a non-delete-shared read handle (FILE_SHARE_READ | FILE_SHARE_WRITE),
            // overwrite in place using shared write access so the upgrade succeeds cleanly without deleting the parent folder.
            try
            {
                using (FileStream src = new FileStream(stagedTempFile, FileMode.Open, FileAccess.Read, FileShare.Read))
                using (FileStream dst = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete))
                {
                    src.CopyTo(dst);
                    dst.Flush(true);
                }
                try { File.Delete(stagedTempFile); } catch { }
                return;
            }
            catch
            {
            }

            if (File.Exists(destPath))
            {
                File.Delete(destPath);
            }
            File.Move(stagedTempFile, destPath);
        }

        internal static bool IsWindowsReservedDeviceName(string leafName)
        {
            if (string.IsNullOrWhiteSpace(leafName))
            {
                return false;
            }
            string trimmed = leafName.Trim().TrimEnd('.', ' ');
            if (string.IsNullOrEmpty(trimmed))
            {
                return false;
            }
            if (WindowsReservedDeviceNames.Contains(trimmed))
            {
                return true;
            }
            int firstDot = trimmed.IndexOf('.');
            if (firstDot > 0)
            {
                string firstStem = trimmed.Substring(0, firstDot).TrimEnd(' ');
                if (WindowsReservedDeviceNames.Contains(firstStem))
                {
                    return true;
                }
            }
            int lastDot = trimmed.LastIndexOf('.');
            if (lastDot > 0 && lastDot != firstDot)
            {
                string lastStem = trimmed.Substring(0, lastDot).TrimEnd(' ');
                if (WindowsReservedDeviceNames.Contains(lastStem))
                {
                    return true;
                }
            }
            return false;
        }

        internal static string SanitizeFileName(string rawName, string fallback = "shihab_download.bin")
        {
            string safeFallback = string.IsNullOrWhiteSpace(fallback) ? "shihab_download.bin" : fallback;
            if (string.IsNullOrWhiteSpace(rawName))
            {
                return safeFallback;
            }

            string trimmed = rawName.Trim().Trim('"', '\'');
            char[] invalidChars = Path.GetInvalidFileNameChars();
            StringBuilder sb = new StringBuilder(trimmed.Length);

            for (int i = 0; i < trimmed.Length; i++)
            {
                char ch = trimmed[i];
                bool bad = ch < 0x20 || ch == 0x7F || ch == '/' || ch == '\\' || ch == ':';
                if (!bad)
                {
                    for (int j = 0; j < invalidChars.Length; j++)
                    {
                        if (ch == invalidChars[j])
                        {
                            bad = true;
                            break;
                        }
                    }
                }
                sb.Append(bad ? '_' : ch);
            }

            string cleaned = sb.ToString().Trim().TrimEnd('.', ' ');
            if (string.IsNullOrEmpty(cleaned) || cleaned == "." || cleaned == "..")
            {
                return safeFallback;
            }

            if (IsWindowsReservedDeviceName(cleaned))
            {
                cleaned = "_" + cleaned;
            }

            if (cleaned.Length > 160)
            {
                string ext = Path.GetExtension(cleaned);
                if (!string.IsNullOrEmpty(ext) && ext.Length > 24)
                {
                    ext = string.Empty;
                }
                string stem = string.IsNullOrEmpty(ext)
                    ? cleaned
                    : cleaned.Substring(0, cleaned.Length - ext.Length);
                int maxStemLen = Math.Max(16, 160 - (ext != null ? ext.Length : 0));
                if (stem.Length > maxStemLen)
                {
                    stem = stem.Substring(0, maxStemLen);
                }
                cleaned = (stem.TrimEnd('.', ' ') + (ext ?? string.Empty)).TrimEnd('.', ' ');
                if (string.IsNullOrEmpty(cleaned) || cleaned == "." || cleaned == "..")
                {
                    return safeFallback;
                }
                if (IsWindowsReservedDeviceName(cleaned))
                {
                    cleaned = "_" + cleaned;
                }
            }

            return cleaned;
        }

        internal static string NormalizeFullDirectoryPath(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || directoryPath.IndexOf('\0') >= 0)
            {
                return string.Empty;
            }
            try
            {
                string full = Path.GetFullPath(directoryPath.Trim());
                string root = Path.GetPathRoot(full);
                if (!string.IsNullOrEmpty(root) && string.Equals(full, root, StringComparison.OrdinalIgnoreCase))
                {
                    return full;
                }
                return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static bool TryValidateRelativePathSegments(string rawRelativePath, out string normalizedRelativePath)
        {
            normalizedRelativePath = string.Empty;
            if (string.IsNullOrWhiteSpace(rawRelativePath))
            {
                return false;
            }

            for (int i = 0; i < rawRelativePath.Length; i++)
            {
                char ch = rawRelativePath[i];
                if (ch < 0x20 || ch == 0x7F || ch == ':')
                {
                    return false;
                }
            }

            if (rawRelativePath.StartsWith("/") || rawRelativePath.StartsWith("\\"))
            {
                return false;
            }

            try
            {
                if (Path.IsPathRooted(rawRelativePath))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }

            string[] rawParts = rawRelativePath.Split(new[] { '/', '\\' }, StringSplitOptions.RemoveEmptyEntries);
            if (rawParts.Length == 0)
            {
                return false;
            }

            char[] invalidNameChars = Path.GetInvalidFileNameChars();
            List<string> safeParts = new List<string>(rawParts.Length);
            for (int i = 0; i < rawParts.Length; i++)
            {
                string seg = rawParts[i];
                if (string.IsNullOrWhiteSpace(seg) || seg == "." || seg == "..")
                {
                    return false;
                }
                if (seg.EndsWith(".") || seg.EndsWith(" "))
                {
                    return false;
                }
                if (seg.IndexOf(':') >= 0)
                {
                    return false;
                }
                for (int c = 0; c < seg.Length; c++)
                {
                    char ch = seg[c];
                    for (int k = 0; k < invalidNameChars.Length; k++)
                    {
                        if (ch == invalidNameChars[k])
                        {
                            return false;
                        }
                    }
                }
                if (IsWindowsReservedDeviceName(seg))
                {
                    return false;
                }
                safeParts.Add(seg);
            }

            normalizedRelativePath = string.Join(Path.DirectorySeparatorChar.ToString(), safeParts.ToArray());
            return normalizedRelativePath.Length > 0;
        }

        internal static bool ResolvePathStrictlyUnderRoot(
            string rootDirectory,
            string relativeOrUntrustedPath,
            out string resolvedFullPath)
        {
            resolvedFullPath = string.Empty;
            string normRoot = NormalizeFullDirectoryPath(rootDirectory);
            if (string.IsNullOrEmpty(normRoot))
            {
                return false;
            }

            string safeRelative;
            if (!TryValidateRelativePathSegments(relativeOrUntrustedPath, out safeRelative))
            {
                return false;
            }

            try
            {
                string combined = Path.GetFullPath(Path.Combine(normRoot, safeRelative));
                string prefix = normRoot.EndsWith(Path.DirectorySeparatorChar.ToString())
                    ? normRoot
                    : normRoot + Path.DirectorySeparatorChar;
                if (!combined.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
                resolvedFullPath = combined;
                return true;
            }
            catch
            {
                resolvedFullPath = string.Empty;
                return false;
            }
        }

        internal static bool HasAnyReparsePointInPath(string targetPath)
        {
            if (string.IsNullOrWhiteSpace(targetPath) || targetPath.IndexOf('\0') >= 0)
            {
                return true;
            }
            try
            {
                string full = Path.GetFullPath(targetPath.Trim()).TrimEnd('\\', '/');
                string root = Path.GetPathRoot(full);
                string cursor = full;
                while (!string.IsNullOrEmpty(cursor) &&
                       !string.Equals(cursor, root, StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(cursor) || File.Exists(cursor))
                    {
                        FileAttributes attr = File.GetAttributes(cursor);
                        if ((attr & FileAttributes.ReparsePoint) != 0)
                        {
                            return true;
                        }
                    }
                    string parent = Path.GetDirectoryName(cursor);
                    if (string.IsNullOrEmpty(parent) || string.Equals(parent, cursor, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    cursor = parent;
                }
                return false;
            }
            catch
            {
                return true;
            }
        }

        internal static void SafeDeleteDirectoryNoFollow(string dirPath)
        {
            if (string.IsNullOrWhiteSpace(dirPath) || !Directory.Exists(dirPath))
            {
                return;
            }
            FileAttributes attr = File.GetAttributes(dirPath);
            if ((attr & FileAttributes.ReparsePoint) != 0)
            {
                // Remove only the junction/symlink entry itself without descending into target
                Directory.Delete(dirPath, false);
                return;
            }
            string[] files = Directory.GetFiles(dirPath);
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    FileAttributes fAttr = File.GetAttributes(files[i]);
                    if ((fAttr & FileAttributes.ReadOnly) != 0)
                    {
                        File.SetAttributes(files[i], fAttr & ~FileAttributes.ReadOnly);
                    }
                }
                catch { }
                File.Delete(files[i]);
            }
            string[] subdirs = Directory.GetDirectories(dirPath);
            for (int i = 0; i < subdirs.Length; i++)
            {
                SafeDeleteDirectoryNoFollow(subdirs[i]);
            }
            Directory.Delete(dirPath, false);
        }

        internal static bool IsUncOrDevicePath(string rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return true;
            }
            string t = rawPath.Trim().Trim('"');
            if (t.StartsWith("\\\\", StringComparison.Ordinal) || t.StartsWith("//", StringComparison.Ordinal))
            {
                return true;
            }
            Uri u;
            if (Uri.TryCreate(t, UriKind.Absolute, out u) && u.IsUnc)
            {
                return true;
            }
            return false;
        }

        internal static string NormalizeSafeCaptureSaveDirectory(string rawDir)
        {
            if (string.IsNullOrWhiteSpace(rawDir) || ContainsControlOrNewline(rawDir) || IsUncOrDevicePath(rawDir))
            {
                return string.Empty;
            }
            try
            {
                string trimmed = rawDir.Trim().Trim('"');
                if (!Path.IsPathRooted(trimmed))
                {
                    return string.Empty;
                }
                string full = Path.GetFullPath(trimmed).TrimEnd('\\', '/');
                if (string.IsNullOrWhiteSpace(full) || full.Length < 4 || full[1] != ':')
                {
                    return string.Empty;
                }
                if (HasAnyReparsePointInPath(full))
                {
                    return string.Empty;
                }

                string appDataRoot = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "ShihabMehediIDM");
                string exeBaseDir = AppDomain.CurrentDomain.BaseDirectory;
                string[] blockedRoots = new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                    Environment.GetFolderPath(Environment.SpecialFolder.System),
                    Environment.GetFolderPath(Environment.SpecialFolder.SystemX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                    Environment.GetFolderPath(Environment.SpecialFolder.Startup),
                    Environment.GetFolderPath(Environment.SpecialFolder.CommonStartup),
                    appDataRoot
                };
                for (int i = 0; i < blockedRoots.Length; i++)
                {
                    string br = blockedRoots[i];
                    if (string.IsNullOrWhiteSpace(br)) continue;
                    string normBr = Path.GetFullPath(br).TrimEnd('\\', '/');
                    if (string.Equals(full, normBr, StringComparison.OrdinalIgnoreCase) ||
                        full.StartsWith(normBr + "\\", StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Empty;
                    }
                }
                // Block writing directly beside mdm.exe (DLL sideloading), while allowing subdirectories
                // in case a portable user placed mdm.exe inside a broad parent folder.
                if (!string.IsNullOrWhiteSpace(exeBaseDir))
                {
                    string normExeBase = Path.GetFullPath(exeBaseDir).TrimEnd('\\', '/');
                    if (string.Equals(full, normExeBase, StringComparison.OrdinalIgnoreCase))
                    {
                        return string.Empty;
                    }
                }
                return full;
            }
            catch
            {
                return string.Empty;
            }
        }

        internal static bool HasReparsePointUnderRoot(string rootDirectory, string targetFullPath)
        {
            string normRoot = NormalizeFullDirectoryPath(rootDirectory);
            if (string.IsNullOrEmpty(normRoot) || string.IsNullOrEmpty(targetFullPath))
            {
                return true;
            }
            if (HasAnyReparsePointInPath(normRoot))
            {
                return true;
            }

            try
            {
                string current = Path.GetFullPath(targetFullPath);
                while (!string.IsNullOrEmpty(current) &&
                       current.Length > normRoot.Length &&
                       current.StartsWith(normRoot, StringComparison.OrdinalIgnoreCase))
                {
                    if (Directory.Exists(current))
                    {
                        FileAttributes attrs = File.GetAttributes(current);
                        if ((attrs & FileAttributes.ReparsePoint) != 0)
                        {
                            return true;
                        }
                    }
                    else if (File.Exists(current))
                    {
                        FileAttributes attrs = File.GetAttributes(current);
                        if ((attrs & FileAttributes.ReparsePoint) != 0)
                        {
                            return true;
                        }
                    }
                    string parent = Path.GetDirectoryName(current);
                    if (string.IsNullOrEmpty(parent) || string.Equals(parent, current, StringComparison.OrdinalIgnoreCase))
                    {
                        break;
                    }
                    current = parent;
                }
            }
            catch
            {
                return true;
            }

            return false;
        }

        internal static bool IsZipEntrySymlinkOrReparse(ZipArchiveEntry entry)
        {
            if (entry == null)
            {
                return true;
            }
            int external = entry.ExternalAttributes;
            int unixFileType = (external >> 16) & 0xF000;
            if (unixFileType == 0xA000)
            {
                return true;
            }
            // Only apply Windows ReparsePoint bit check when Unix mode bits are absent.
            if (unixFileType == 0 && (external & 0x0400) != 0)
            {
                return true;
            }
            return false;
        }

        internal static void ExtractZipArchiveSafely(
            string zipFilePath,
            string destinationRoot,
            long maxEntries = 4096,
            long maxTotalBytes = 1024L * 1024L * 1024L,
            double maxCompressionRatio = 100.0)
        {
            using (FileStream fs = new FileStream(zipFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                ExtractZipArchiveSafely(fs, destinationRoot, maxEntries, maxTotalBytes, maxCompressionRatio);
            }
        }

        internal static void ExtractZipArchiveSafely(
            Stream zipStream,
            string destinationRoot,
            long maxEntries = 4096,
            long maxTotalBytes = 1024L * 1024L * 1024L,
            double maxCompressionRatio = 100.0)
        {
            long ignoredBytes;
            ExtractZipArchiveSafely(zipStream, destinationRoot, out ignoredBytes, null, maxEntries, maxTotalBytes, maxCompressionRatio);
        }

        internal static void ExtractZipArchiveSafely(
            Stream zipStream,
            string destinationRoot,
            out long totalBytesExtracted,
            Action<int, int, string> onEntryExtracted,
            long maxEntries = 4096,
            long maxTotalBytes = 1024L * 1024L * 1024L,
            double maxCompressionRatio = 100.0)
        {
            totalBytesExtracted = 0L;
            if (zipStream == null)
            {
                throw new ArgumentNullException("zipStream");
            }
            string normRoot = NormalizeFullDirectoryPath(destinationRoot);
            if (string.IsNullOrEmpty(normRoot))
            {
                throw new ArgumentException("Invalid extraction destination.", "destinationRoot");
            }

            Directory.CreateDirectory(normRoot);
            long totalWritten = 0;
            long entryCount = 0;
            byte[] buffer = new byte[65536];

            using (ZipArchive archive = new ZipArchive(zipStream, ZipArchiveMode.Read, true))
            {
                int totalEntryCount = Math.Max(1, archive.Entries.Count);
                for (int idx = 0; idx < archive.Entries.Count; idx++)
                {
                    ZipArchiveEntry entry = archive.Entries[idx];
                    entryCount++;
                    if (entryCount > maxEntries)
                    {
                        throw new InvalidDataException("Archive exceeds maximum permitted entry count.");
                    }

                    string rawName = entry.FullName ?? string.Empty;
                    bool isDir = rawName.EndsWith("/") || rawName.EndsWith("\\");
                    string trimmedForCheck = rawName.TrimEnd('/', '\\');
                    if (string.IsNullOrEmpty(trimmedForCheck))
                    {
                        continue;
                    }

                    if (IsZipEntrySymlinkOrReparse(entry))
                    {
                        throw new InvalidDataException("Symlink or reparse entry rejected: " + rawName);
                    }

                    string destPath;
                    if (!ResolvePathStrictlyUnderRoot(normRoot, trimmedForCheck, out destPath))
                    {
                        throw new InvalidDataException("Unsafe archive path rejected: " + rawName);
                    }

                    if (HasReparsePointUnderRoot(normRoot, destPath))
                    {
                        throw new InvalidDataException("Reparse point encountered in extraction target: " + rawName);
                    }

                    if (isDir)
                    {
                        Directory.CreateDirectory(destPath);
                        continue;
                    }

                    if (onEntryExtracted != null)
                    {
                        onEntryExtracted(idx + 1, totalEntryCount, trimmedForCheck);
                    }

                    long declaredLen = entry.Length;
                    long compLen = entry.CompressedLength;
                    if (declaredLen < 0 || declaredLen > maxTotalBytes - totalWritten)
                    {
                        throw new InvalidDataException("Archive entry exceeds total uncompressed byte budget: " + rawName);
                    }
                    if (declaredLen > 1048576L)
                    {
                        double ratio = compLen > 0 ? ((double)declaredLen / (double)compLen) : double.MaxValue;
                        if (ratio > maxCompressionRatio)
                        {
                            throw new InvalidDataException("Archive entry compression ratio exceeds safe bound: " + rawName);
                        }
                    }

                    string parentDir = Path.GetDirectoryName(destPath);
                    if (!string.IsNullOrEmpty(parentDir))
                    {
                        Directory.CreateDirectory(parentDir);
                    }
                    if (HasReparsePointUnderRoot(normRoot, destPath))
                    {
                        throw new InvalidDataException("Reparse point created during extraction: " + rawName);
                    }

                    long entryWritten = 0;
                    string stagedTempPath = destPath + ".shihab_stage_" + Guid.NewGuid().ToString("N") + ".tmp";
                    try
                    {
                        using (Stream entryStream = entry.Open())
                        using (FileStream outStream = new FileStream(stagedTempPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                        {
                            int read;
                            while ((read = entryStream.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                entryWritten += read;
                                totalWritten += read;
                                if (totalWritten > maxTotalBytes)
                                {
                                    throw new InvalidDataException("Archive total extracted payload exceeded size budget.");
                                }
                                if (entryWritten > 1048576L && compLen > 0)
                                {
                                    double actualRatio = (double)entryWritten / (double)compLen;
                                    if (actualRatio > maxCompressionRatio)
                                    {
                                        throw new InvalidDataException("Archive stream decompression bomb detected: " + rawName);
                                    }
                                }
                                else if (compLen == 0 && entryWritten > 1048576L)
                                {
                                    throw new InvalidDataException("Zero-compressed oversized archive stream rejected: " + rawName);
                                }
                                outStream.Write(buffer, 0, read);
                            }
                            outStream.Flush(true);
                        }
                        ReplaceFileAtomicallyWithRenameFallback(stagedTempPath, destPath);
                    }
                    finally
                    {
                        try { if (File.Exists(stagedTempPath)) File.Delete(stagedTempPath); } catch { }
                    }
                }
            }
            totalBytesExtracted = totalWritten;
        }

        internal static string QuoteWindowsArgument(string arg)
        {
            if (arg == null)
            {
                return "\"\"";
            }
            string cleaned = arg.Replace("\0", string.Empty);
            if (cleaned.Length == 0)
            {
                return "\"\"";
            }

            bool needsQuotes = false;
            for (int i = 0; i < cleaned.Length; i++)
            {
                char ch = cleaned[i];
                if (ch == ' ' || ch == '\t' || ch == '\n' || ch == '\r' || ch == '"')
                {
                    needsQuotes = true;
                    break;
                }
            }
            if (!needsQuotes)
            {
                return cleaned;
            }

            StringBuilder sb = new StringBuilder(cleaned.Length + 8);
            sb.Append('"');
            int backslashes = 0;
            for (int i = 0; i < cleaned.Length; i++)
            {
                char ch = cleaned[i];
                if (ch == '\\')
                {
                    backslashes++;
                }
                else if (ch == '"')
                {
                    sb.Append('\\', backslashes * 2 + 1);
                    sb.Append('"');
                    backslashes = 0;
                }
                else
                {
                    if (backslashes > 0)
                    {
                        sb.Append('\\', backslashes);
                        backslashes = 0;
                    }
                    sb.Append(ch);
                }
            }
            if (backslashes > 0)
            {
                sb.Append('\\', backslashes * 2);
            }
            sb.Append('"');
            return sb.ToString();
        }

        internal static string BuildWindowsArgumentString(params string[] args)
        {
            if (args == null || args.Length == 0)
            {
                return string.Empty;
            }
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < args.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(' ');
                }
                sb.Append(QuoteWindowsArgument(args[i]));
            }
            return sb.ToString();
        }

#if !SHIHAB_INSTALLER
        internal static string ResolveSystem32ToolPath(string toolName)
        {
            if (string.IsNullOrWhiteSpace(toolName) || !AllowedSystem32Tools.Contains(toolName.Trim()))
            {
                throw new ArgumentException("Unsupported System32 tool name.", "toolName");
            }
            string cleanName = toolName.Trim();
            string sysDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
            if (!string.IsNullOrEmpty(sysDir))
            {
                string candidate = Path.Combine(sysDir, cleanName);
                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            string winDir = Environment.GetEnvironmentVariable("SystemRoot");
            if (string.IsNullOrEmpty(winDir))
            {
                winDir = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            }
            if (!string.IsNullOrEmpty(winDir))
            {
                string fallback = Path.Combine(Path.Combine(winDir, "System32"), cleanName);
                if (File.Exists(fallback))
                {
                    return fallback;
                }
            }
            return Path.Combine(@"C:\Windows\System32", cleanName);
        }
#endif

        internal static string QueryProcessImagePathLimited(int pid)
        {
            if (pid <= 0)
            {
                return string.Empty;
            }
            IntPtr hProc = IntPtr.Zero;
            try
            {
                hProc = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, pid);
                if (hProc == IntPtr.Zero)
                {
                    return string.Empty;
                }
                int cap = 1024;
                StringBuilder sb = new StringBuilder(cap);
                if (QueryFullProcessImageNameW(hProc, 0u, sb, ref cap) && cap > 0)
                {
                    return Path.GetFullPath(sb.ToString(0, cap));
                }
                return string.Empty;
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                if (hProc != IntPtr.Zero)
                {
                    try { CloseHandle(hProc); } catch { }
                }
            }
        }

        internal static bool TryVerifyProcessIdentity(int pid, string expectedExeFullPath)
        {
            if (pid <= 0 || string.IsNullOrWhiteSpace(expectedExeFullPath))
            {
                return false;
            }
            try
            {
                string normalizedExpected = Path.GetFullPath(expectedExeFullPath);
                string actualPath = QueryProcessImagePathLimited(pid);
                if (string.IsNullOrWhiteSpace(actualPath))
                {
                    return false;
                }
                return string.Equals(actualPath, normalizedExpected, StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryValidateHttpUrl(string rawUrl, bool httpsOnly, out Uri validatedUri)
        {
            validatedUri = null;
            if (string.IsNullOrWhiteSpace(rawUrl) || rawUrl.IndexOf('\0') >= 0)
            {
                return false;
            }
            Uri parsed;
            if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out parsed))
            {
                return false;
            }
            if (httpsOnly)
            {
                if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            else
            {
                if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                    !string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            if (string.IsNullOrWhiteSpace(parsed.Host))
            {
                return false;
            }
            validatedUri = parsed;
            return true;
        }

        internal static bool TryValidateExternalShellUri(string rawUrl, bool allowDownloadSchemes, out Uri validatedUri)
        {
            validatedUri = null;
            if (string.IsNullOrWhiteSpace(rawUrl) || rawUrl.IndexOf('\0') >= 0)
            {
                return false;
            }
            string trimmed = rawUrl.Trim();
            for (int i = 0; i < trimmed.Length; i++)
            {
                if (trimmed[i] < 0x20 || trimmed[i] == 0x7F)
                {
                    return false;
                }
            }
            Uri parsed;
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out parsed))
            {
                return false;
            }
            string scheme = parsed.Scheme != null ? parsed.Scheme.ToLowerInvariant() : string.Empty;
            if (scheme == "http" || scheme == "https" || scheme == "mailto")
            {
                if (scheme != "mailto" && string.IsNullOrWhiteSpace(parsed.Host))
                {
                    return false;
                }
                validatedUri = parsed;
                return true;
            }
            if (allowDownloadSchemes && (scheme == "ftp" || scheme == "ftps" || scheme == "magnet"))
            {
                validatedUri = parsed;
                return true;
            }
            return false;
        }

        internal static bool TryValidateLocalTorrentFilePath(string rawPath, out string validatedFullPath)
        {
            validatedFullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(rawPath) || ContainsControlOrNewline(rawPath) || IsUncOrDevicePath(rawPath))
            {
                return false;
            }
            string trimmed = rawPath.Trim().Trim('"');
            if (!trimmed.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            try
            {
                if (!Path.IsPathRooted(trimmed))
                {
                    return false;
                }
                string full = Path.GetFullPath(trimmed);
                if (IsUncOrDevicePath(full) || HasAnyReparsePointInPath(full))
                {
                    return false;
                }
                if (!File.Exists(full))
                {
                    return false;
                }
                FileInfo fi = new FileInfo(full);
                if (fi.Length <= 2 || fi.Length > 32L * 1024L * 1024L)
                {
                    return false;
                }
                using (FileStream fs = new FileStream(full, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    if (fs.ReadByte() != (byte)'d')
                    {
                        return false;
                    }
                }
                validatedFullPath = full;
                return true;
            }
            catch
            {
                validatedFullPath = string.Empty;
                return false;
            }
        }

        internal static bool IsValidGitHubRepoSlug(string slug)
        {
            return !string.IsNullOrWhiteSpace(slug) && GitHubSlugRegex.IsMatch(slug.Trim());
        }

        internal static bool IsOfficialGitHubHttpsUri(Uri uri)
        {
            if (uri == null ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }
            string host = (uri.Host ?? string.Empty).ToLowerInvariant();
            if (host == "github.com")
            {
                string path = uri.AbsolutePath ?? string.Empty;
                return path.StartsWith("/sshihabb007/MHS-DOWNLOAD-MANAGER/", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(path, "/sshihabb007/MHS-DOWNLOAD-MANAGER", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/yt-dlp/", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/BtbN/", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/GyanD/", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/denoland/", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/mikf/", StringComparison.OrdinalIgnoreCase);
            }
            if (host == "api.github.com")
            {
                string path = uri.AbsolutePath ?? string.Empty;
                return path.StartsWith("/repos/sshihabb007/MHS-DOWNLOAD-MANAGER/", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/repos/yt-dlp/", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/repos/BtbN/", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/repos/denoland/", StringComparison.OrdinalIgnoreCase) ||
                       path.StartsWith("/repos/mikf/", StringComparison.OrdinalIgnoreCase);
            }
            return host == "objects.githubusercontent.com" ||
                   host == "release-assets.githubusercontent.com" ||
                   host == "github-releases.githubusercontent.com";
        }

        internal static bool IsOfficialMhsReleaseHttpsUri(Uri uri)
        {
            if (uri == null ||
                !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
                !string.IsNullOrEmpty(uri.UserInfo))
            {
                return false;
            }
            string host = (uri.Host ?? string.Empty).ToLowerInvariant();
            if (host == "github.com")
            {
                string path = uri.AbsolutePath ?? string.Empty;
                return path.StartsWith("/sshihabb007/MHS-DOWNLOAD-MANAGER/", StringComparison.OrdinalIgnoreCase) ||
                       string.Equals(path, "/sshihabb007/MHS-DOWNLOAD-MANAGER", StringComparison.OrdinalIgnoreCase);
            }
            if (host == "api.github.com")
            {
                string path = uri.AbsolutePath ?? string.Empty;
                return path.StartsWith("/repos/sshihabb007/MHS-DOWNLOAD-MANAGER/", StringComparison.OrdinalIgnoreCase);
            }
            return host == "objects.githubusercontent.com" ||
                   host == "release-assets.githubusercontent.com" ||
                   host == "github-releases.githubusercontent.com";
        }

        internal static bool IsDisallowedInternalIpAddress(IPAddress address)
        {
            if (address == null)
            {
                return true;
            }
            if (IPAddress.IsLoopback(address))
            {
                return true;
            }

            if (address.AddressFamily == AddressFamily.InterNetworkV6)
            {
                if (address.IsIPv4MappedToIPv6)
                {
                    return IsDisallowedInternalIpAddress(address.MapToIPv4());
                }
                if (address.Equals(IPAddress.IPv6Any) ||
                    address.Equals(IPAddress.IPv6None) ||
                    address.IsIPv6LinkLocal ||
                    address.IsIPv6SiteLocal ||
                    address.IsIPv6Multicast ||
                    address.IsIPv6Teredo)
                {
                    return true;
                }
                byte[] v6 = address.GetAddressBytes();
                if (v6.Length == 16)
                {
                    // Unique Local Addresses fc00::/7
                    if ((v6[0] & 0xFE) == 0xFC)
                    {
                        return true;
                    }
                    // IPv4-compatible ::w.x.y.z
                    bool firstZero = true;
                    for (int i = 0; i < 12; i++)
                    {
                        if (v6[i] != 0)
                        {
                            firstZero = false;
                            break;
                        }
                    }
                    if (firstZero)
                    {
                        byte[] mapped = new byte[4] { v6[12], v6[13], v6[14], v6[15] };
                        return IsDisallowedInternalIpAddress(new IPAddress(mapped));
                    }
                }
                return false;
            }

            if (address.AddressFamily == AddressFamily.InterNetwork)
            {
                byte[] b = address.GetAddressBytes();
                if (b.Length != 4)
                {
                    return true;
                }
                if (b[0] == 0) return true;                          // 0.0.0.0/8
                if (b[0] == 10) return true;                         // 10.0.0.0/8
                if (b[0] == 127) return true;                        // 127.0.0.0/8
                if (b[0] == 169 && b[1] == 254) return true;         // 169.254.0.0/16 link-local & cloud metadata
                if (b[0] == 172 && b[1] >= 16 && b[1] <= 31) return true; // 172.16.0.0/12
                if (b[0] == 192 && b[1] == 168) return true;         // 192.168.0.0/16
                if (b[0] == 100 && b[1] >= 64 && b[1] <= 127) return true; // 100.64.0.0/10 CGNAT
                if (b[0] == 192 && b[1] == 0 && (b[2] == 0 || b[2] == 2)) return true;
                if (b[0] == 198 && (b[1] == 18 || b[1] == 19)) return true; // benchmarking
                if (b[0] == 198 && b[1] == 51 && b[2] == 100) return true;
                if (b[0] == 203 && b[1] == 0 && b[2] == 113) return true;
                if (b[0] >= 224) return true;                      // multicast / reserved / 255.255.255.255
                return false;
            }

            return true;
        }

        internal static bool IsHostDisallowedForOutboundSsrf(string host, bool resolveDns = false)
        {
            if (string.IsNullOrWhiteSpace(host))
            {
                return true;
            }
            string clean = host.Trim().Trim('[', ']').TrimEnd('.');
            string lower = clean.ToLowerInvariant();
            if (lower == "localhost" ||
                lower.EndsWith(".localhost") ||
                lower.EndsWith(".local") ||
                lower.EndsWith(".internal") ||
                lower == "metadata.google.internal")
            {
                return true;
            }

            IPAddress literal;
            if (IPAddress.TryParse(clean, out literal))
            {
                return IsDisallowedInternalIpAddress(literal);
            }

            if (!resolveDns)
            {
                return false;
            }

            try
            {
                IPAddress[] addrs = Dns.GetHostAddresses(clean);
                if (addrs == null || addrs.Length == 0)
                {
                    return true;
                }
                for (int i = 0; i < addrs.Length; i++)
                {
                    if (IsDisallowedInternalIpAddress(addrs[i]))
                    {
                        return true;
                    }
                }
                return false;
            }
            catch
            {
                // Fail closed when DNS resolution cannot verify the host is public
                return true;
            }
        }

        internal static bool ContainsControlOrNewline(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return false;
            }
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch < 0x20 || ch == 0x7F || char.IsControl(ch))
                {
                    return true;
                }
            }
            return false;
        }

        internal static bool FixedTimeEqualsAscii(string a, string b)
        {
            if (a == null || b == null)
            {
                return false;
            }
            int maxLen = Math.Max(a.Length, b.Length);
            int diff = a.Length ^ b.Length;
            for (int i = 0; i < maxLen; i++)
            {
                char ca = i < a.Length ? a[i] : '\0';
                char cb = i < b.Length ? b[i] : '\0';
                diff |= (ca ^ cb);
            }
            return diff == 0 && a.Length > 0;
        }

        internal static string ComputeChromiumUnpackedExtensionId(string extensionDirPath)
        {
            string norm = Path.GetFullPath(extensionDirPath).TrimEnd('\\', '/');
            if (norm.Length >= 2 && norm[1] == ':')
            {
                norm = char.ToUpperInvariant(norm[0]) + norm.Substring(1);
            }
            byte[] utf16Bytes = Encoding.Unicode.GetBytes(norm);
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(utf16Bytes);
                char[] chars = new char[32];
                for (int i = 0; i < 16; i++)
                {
                    int b = hash[i];
                    chars[i * 2] = (char)('a' + ((b >> 4) & 0x0F));
                    chars[i * 2 + 1] = (char)('a' + (b & 0x0F));
                }
                return new string(chars);
            }
        }

        [DllImport("iphlpapi.dll", SetLastError = true)]
        private static extern uint GetExtendedTcpTable(
            IntPtr pTcpTable,
            ref int dwOutBufLen,
            bool sort,
            int ipVersion,
            int tblClass,
            uint reserved);

        internal static bool TryGetLoopbackListenerPid(int port, out int owningPid)
        {
            owningPid = 0;
            if (port <= 0 || port > 65535) return false;
            const int AF_INET = 2;
            const int TCP_TABLE_OWNER_PID_LISTENER = 3;
            const uint ERROR_INSUFFICIENT_BUFFER = 122u;
            for (int attempt = 0; attempt < 2; attempt++)
            {
                int bufSize = 0;
                uint res = GetExtendedTcpTable(IntPtr.Zero, ref bufSize, false, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0u);
                if (res != ERROR_INSUFFICIENT_BUFFER && res != 0u) return false;
                if (bufSize < 4 || bufSize > 16 * 1024 * 1024) return false;

                IntPtr buf = Marshal.AllocHGlobal(bufSize);
                try
                {
                    res = GetExtendedTcpTable(buf, ref bufSize, false, AF_INET, TCP_TABLE_OWNER_PID_LISTENER, 0u);
                    if (res == ERROR_INSUFFICIENT_BUFFER) continue;
                    if (res != 0u) return false;
                    int count = Marshal.ReadInt32(buf, 0);
                    int maxRows = (bufSize - 4) / 24;
                    if (count < 0) count = 0;
                    if (count > maxRows) count = maxRows;
                    for (int i = 0; i < count; i++)
                    {
                        int rowOff = 4 + i * 24;
                        uint localAddr = unchecked((uint)Marshal.ReadInt32(buf, rowOff + 4));
                        uint localPortRaw = unchecked((uint)Marshal.ReadInt32(buf, rowOff + 8));
                        int pid = Marshal.ReadInt32(buf, rowOff + 20);
                        int hostPort = (int)(((localPortRaw & 0xFFu) << 8) | ((localPortRaw >> 8) & 0xFFu));
                        if (hostPort == port && (localAddr == 0x0100007Fu || localAddr == 0u) && pid > 0)
                        {
                            owningPid = pid;
                            return true;
                        }
                    }
                    return false;
                }
                catch
                {
                    return false;
                }
                finally
                {
                    Marshal.FreeHGlobal(buf);
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Strongly-typed Win32 Shell COM (.lnk) helper using IShellLinkW + IPersistFile.
    /// Avoids dynamic WScript.Shell scripting host automation.
    /// </summary>
    internal static class ShihabShellLinkHelper
    {
        [ComImport]
        [Guid("00021401-0000-0000-C000-000000000046")]
        private class ShellLinkCoClass
        {
        }

        [ComImport]
        [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
        [Guid("000214F9-0000-0000-C000-000000000046")]
        private interface IShellLinkW
        {
            void GetPath([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszFile, int cchMaxPath, IntPtr pfd, uint fFlags);
            void GetIDList(out IntPtr ppidl);
            void SetIDList(IntPtr pidl);
            void GetDescription([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszName, int cchMaxName);
            void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
            void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszDir, int cchMaxPath);
            void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
            void GetArguments([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszArgs, int cchMaxPath);
            void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
            void GetHotkey(out short pwHotkey);
            void SetHotkey(short wHotkey);
            void GetShowCmd(out int piShowCmd);
            void SetShowCmd(int iShowCmd);
            void GetIconLocation([Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pszIconPath, int cchIconPath, out int piIcon);
            void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
            void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
            void Resolve(IntPtr hwnd, uint fFlags);
            void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
        }

        private const uint SLGP_RAWPATH = 0x0004u;
        private const int STGM_READ = 0x00000000;

        internal static string TryGetShortcutTargetPath(string lnkPath)
        {
            if (string.IsNullOrWhiteSpace(lnkPath) || !File.Exists(lnkPath)) return string.Empty;
            if (ShihabProcessAndPathSafety.HasAnyReparsePointInPath(lnkPath)) return string.Empty;

            object linkObj = null;
            try
            {
                linkObj = new ShellLinkCoClass();
                IPersistFile pf = (IPersistFile)linkObj;
                pf.Load(lnkPath, STGM_READ);
                IShellLinkW link = (IShellLinkW)linkObj;
                StringBuilder sb = new StringBuilder(1024);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, SLGP_RAWPATH);
                string raw = sb.ToString().Trim();
                if (string.IsNullOrEmpty(raw))
                {
                    sb.Length = 0;
                    link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0u);
                    raw = sb.ToString().Trim();
                }
                return string.IsNullOrEmpty(raw) ? string.Empty : Path.GetFullPath(raw);
            }
            catch
            {
                return string.Empty;
            }
            finally
            {
                if (linkObj != null && Marshal.IsComObject(linkObj))
                {
                    try { Marshal.FinalReleaseComObject(linkObj); } catch { }
                }
            }
        }

        internal static bool CreateOrUpdateShortcut(
            string lnkPath,
            string targetPath,
            string arguments,
            string workingDir,
            string description,
            string iconPath,
            int iconIndex)
        {
            if (string.IsNullOrWhiteSpace(lnkPath) || string.IsNullOrWhiteSpace(targetPath)) return false;
            if (ShihabProcessAndPathSafety.HasAnyReparsePointInPath(lnkPath)) return false;

            object linkObj = null;
            try
            {
                linkObj = new ShellLinkCoClass();
                IShellLinkW link = (IShellLinkW)linkObj;
                link.SetPath(targetPath);
                if (!string.IsNullOrEmpty(arguments))
                {
                    link.SetArguments(arguments);
                }
                if (!string.IsNullOrEmpty(workingDir))
                {
                    link.SetWorkingDirectory(workingDir);
                }
                if (!string.IsNullOrEmpty(description))
                {
                    link.SetDescription(description);
                }
                if (!string.IsNullOrEmpty(iconPath))
                {
                    link.SetIconLocation(iconPath, iconIndex);
                }
                IPersistFile pf = (IPersistFile)linkObj;
                pf.Save(lnkPath, true);
                try { File.SetLastWriteTimeUtc(lnkPath, DateTime.UtcNow); } catch { }
                return true;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (linkObj != null && Marshal.IsComObject(linkObj))
                {
                    try { Marshal.FinalReleaseComObject(linkObj); } catch { }
                }
            }
        }

        internal static bool RefreshShortcutIconLocation(string lnkPath, string targetExePath, int iconIndex)
        {
            if (string.IsNullOrWhiteSpace(lnkPath) || string.IsNullOrWhiteSpace(targetExePath) || !File.Exists(lnkPath)) return false;
            if (ShihabProcessAndPathSafety.HasAnyReparsePointInPath(lnkPath)) return false;

            object linkObj = null;
            try
            {
                string normExe = Path.GetFullPath(targetExePath);
                linkObj = new ShellLinkCoClass();
                IPersistFile pf = (IPersistFile)linkObj;
                pf.Load(lnkPath, STGM_READ);
                IShellLinkW link = (IShellLinkW)linkObj;
                StringBuilder sb = new StringBuilder(1024);
                link.GetPath(sb, sb.Capacity, IntPtr.Zero, 0u);
                string scTarget = sb.ToString().Trim();
                if (!string.IsNullOrEmpty(scTarget) &&
                    string.Equals(Path.GetFullPath(scTarget), normExe, StringComparison.OrdinalIgnoreCase))
                {
                    link.SetIconLocation(normExe, iconIndex);
                    pf.Save(lnkPath, true);
                    try { File.SetLastWriteTimeUtc(lnkPath, DateTime.UtcNow); } catch { }
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
            finally
            {
                if (linkObj != null && Marshal.IsComObject(linkObj))
                {
                    try { Marshal.FinalReleaseComObject(linkObj); } catch { }
                }
            }
        }
    }
}
