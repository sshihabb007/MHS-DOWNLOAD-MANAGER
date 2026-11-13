using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Sockets;
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
            RegexOptions.Compiled | RegexOptions.CultureInvariant);

        private static readonly HashSet<string> WindowsReservedDeviceNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CON", "PRN", "AUX", "NUL", "CLOCK$",
            "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
            "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
        };

        private static readonly HashSet<string> AllowedSystem32Tools = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "cmd.exe",
            "explorer.exe",
            "rundll32.exe",
            "powershell.exe",
            "conhost.exe"
        };

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

        internal static bool HasReparsePointUnderRoot(string rootDirectory, string targetFullPath)
        {
            string normRoot = NormalizeFullDirectoryPath(rootDirectory);
            if (string.IsNullOrEmpty(normRoot) || string.IsNullOrEmpty(targetFullPath))
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
                    using (Stream entryStream = entry.Open())
                    using (FileStream outStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
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

        internal static bool TryVerifyProcessIdentity(int pid, string expectedExeFullPath)
        {
            if (pid <= 0 || string.IsNullOrWhiteSpace(expectedExeFullPath))
            {
                return false;
            }
            try
            {
                string normalizedExpected = Path.GetFullPath(expectedExeFullPath);
                using (Process proc = Process.GetProcessById(pid))
                {
                    if (proc.HasExited)
                    {
                        return false;
                    }
                    ProcessModule mod = proc.MainModule;
                    if (mod == null || string.IsNullOrWhiteSpace(mod.FileName))
                    {
                        return false;
                    }
                    string actualPath = Path.GetFullPath(mod.FileName);
                    return string.Equals(actualPath, normalizedExpected, StringComparison.OrdinalIgnoreCase);
                }
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
            return host == "github.com" ||
                   host == "api.github.com" ||
                   host == "objects.githubusercontent.com" ||
                   host == "release-assets.githubusercontent.com" ||
                   host == "github-releases.githubusercontent.com" ||
                   host.EndsWith(".githubusercontent.com");
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
                return false;
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
    }
}
