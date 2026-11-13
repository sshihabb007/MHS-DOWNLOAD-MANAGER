using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.VisualBasic.FileIO;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public static class ShihabSafeArchiveExtractor
    {
        private const long MaxDecompressedBytes = 20L * 1024L * 1024L * 1024L; // 20 GiB cap
        private const long RatioGraceBytes = 10L * 1024L * 1024L; // 10 MiB grace
        private const long MaxCompressionRatio = 200L; // 200:1 bomb guard
        private const int MaxExtractedEntries = 100000;
        private const int MaxDirectoryDepth = 64;
        private const int MaxTarMetadataBlockBytes = 65536;

        private static readonly TimeSpan ArchiveRegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex SplitNumericExtRegex = new Regex(@"\.(7z|zip)\.001$", RegexOptions.Compiled | RegexOptions.CultureInvariant, ArchiveRegexTimeout);
        private static readonly Regex RarPartVolRegex = new Regex(@"^(.+)\.part0*([0-9]+)\.rar$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, ArchiveRegexTimeout);
        private static readonly Regex NumSplitVolRegex = new Regex(@"^(.+\.(?:7z|zip))\.([0-9]{3})$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, ArchiveRegexTimeout);
        private static readonly Regex OldRarVolRegex = new Regex(@"\.r[0-9]{2}$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, ArchiveRegexTimeout);
        private static readonly Regex RarStemRegex = new Regex(@"^(.+?)\.part0*1\.rar$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, ArchiveRegexTimeout);
        private static readonly Regex NumStemRegex = new Regex(@"^(.+?)\.(?:7z|zip)\.001$", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, ArchiveRegexTimeout);

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
                   SplitNumericExtRegex.IsMatch(lower);
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

            Match rarPartMatch = RarPartVolRegex.Match(name);
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

            Match numSplitMatch = NumSplitVolRegex.Match(name);
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

            if (OldRarVolRegex.IsMatch(name))
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

                if (!string.IsNullOrWhiteSpace(motwHostUrl) || !string.IsNullOrWhiteSpace(motwRefererUrl))
                {
                    ShihabOsAndDiskSafety.PropagateMarkOfTheWebToDirectory(stagingDir, motwHostUrl, motwRefererUrl);
                }

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
                    uint unixMode = ((uint)entry.ExternalAttributes >> 16) & 0xF000u;
                    if (unixMode == 0xA000u)
                    {
                        throw new InvalidOperationException("Blocked symbolic link entry in ZIP archive: " + entry.FullName);
                    }
                    bool isDir = entry.FullName.EndsWith("/") || entry.FullName.EndsWith("\\");
                    string safePath = ValidateSafeRelativeEntryPath(stagingDir, entry.FullName, isDir);
                    if (isDir)
                    {
                        if (!string.IsNullOrEmpty(safePath)) Directory.CreateDirectory(safePath);
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
                    try { entrySize = Convert.ToInt64(sizeOctal, 8); }
                    catch { throw new InvalidDataException("Invalid tar entry size header."); }
                }
                if (entrySize < 0 || entrySize > MaxDecompressedBytes || entrySize > diskBudgetCap)
                {
                    throw new InvalidOperationException("Tar entry size is invalid or exceeds safety limit.");
                }

                byte typeFlag = header[156];
                if (typeFlag == (byte)'2' || typeFlag == (byte)'1')
                {
                    throw new InvalidOperationException("Archive contains unsafe symbolic or hard links.");
                }
                if (typeFlag == (byte)'L' && entrySize > 0 && entrySize <= MaxTarMetadataBlockBytes)
                {
                    byte[] longNameBuf = new byte[(int)entrySize];
                    ReadExactBytes(tarStream, longNameBuf, (int)entrySize);
                    pendingPaxOrGnuPath = Encoding.UTF8.GetString(longNameBuf).TrimEnd('\0', '\r', '\n');
                    long padL = (512 - (entrySize % 512)) % 512;
                    if (padL > 0) SkipStreamBytes(tarStream, padL);
                    continue;
                }
                if (typeFlag == (byte)'x' && entrySize > 0 && entrySize <= MaxTarMetadataBlockBytes)
                {
                    byte[] paxBuf = new byte[(int)entrySize];
                    ReadExactBytes(tarStream, paxBuf, (int)entrySize);
                    string paxPath = ParsePaxPathAttribute(Encoding.UTF8.GetString(paxBuf));
                    if (!string.IsNullOrEmpty(paxPath))
                    {
                        pendingPaxOrGnuPath = paxPath;
                    }
                    long padX = (512 - (entrySize % 512)) % 512;
                    if (padX > 0) SkipStreamBytes(tarStream, padX);
                    continue;
                }

                bool isDir = typeFlag == (byte)'5' || name.EndsWith("/");
                string safePath = ValidateSafeRelativeEntryPath(stagingDir, name, isDir);
                if (isDir)
                {
                    if (!string.IsNullOrEmpty(safePath)) Directory.CreateDirectory(safePath);
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

        private static string ParsePaxPathAttribute(string paxData)
        {
            if (string.IsNullOrEmpty(paxData)) return "";
            string[] lines = paxData.Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i];
                int sp = line.IndexOf(' ');
                if (sp <= 0) continue;
                string kv = line.Substring(sp + 1);
                if (kv.StartsWith("path=", StringComparison.Ordinal))
                {
                    return kv.Substring(5).TrimEnd('\r', '\0');
                }
            }
            return "";
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

            List<string> argList = new List<string>();
            argList.Add("x");
            argList.Add("-y");
            argList.Add("-snl-");
            if (!string.IsNullOrEmpty(password))
            {
                string cleanPwd = password.Replace("\r", "").Replace("\n", "").Replace("\0", "");
                argList.Add("-p" + cleanPwd);
            }
            else
            {
                argList.Add("-p");
            }
            argList.Add("-o" + stagingDir);
            argList.Add("--");
            argList.Add(archivePath);

            string stdoutAll;
            string stderrAll;
            int exitCode = ShihabWindowsJobRunner.RunManagedSubprocess(
                sevenZipExe,
                ShihabProcessAndPathSafety.BuildWindowsArgumentString(argList.ToArray()),
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
                    string d = dirs[i].Trim().Trim('"');
                    if (string.IsNullOrEmpty(d) || !Path.IsPathRooted(d) || d.StartsWith(@"\\", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string cand = Path.Combine(d, "7z.exe");
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
            return ValidateSafeRelativeEntryPath(stagingRoot, rawEntryName, false);
        }

        private static string ValidateSafeRelativeEntryPath(string stagingRoot, string rawEntryName, bool isDirectoryEntry)
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
            string[] rawParts = normalized.Split(new char[] { Path.DirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries);
            List<string> cleanParts = new List<string>();
            for (int i = 0; i < rawParts.Length; i++)
            {
                if (rawParts[i] == ".") continue;
                if (rawParts[i] == "..")
                {
                    throw new InvalidOperationException("Blocked Zip-Slip path traversal in archive entry: " + rawEntryName);
                }
                EnsureNotReservedWindowsName(rawParts[i]);
                cleanParts.Add(rawParts[i]);
            }
            if (cleanParts.Count > MaxDirectoryDepth)
            {
                throw new InvalidOperationException("Archive entry exceeds maximum directory depth (64): " + rawEntryName);
            }
            if (cleanParts.Count == 0)
            {
                if (isDirectoryEntry) return "";
                throw new InvalidOperationException("Blocked root-resolving file entry in archive: " + rawEntryName);
            }

            string relPath = string.Join(Path.DirectorySeparatorChar.ToString(), cleanParts.ToArray());
            string resolved;
            if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(stagingRoot, relPath, out resolved))
            {
                throw new InvalidOperationException("Blocked path escape outside staging directory: " + rawEntryName);
            }
            return resolved;
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
            Match m = RarStemRegex.Match(fileName);
            if (m.Success) return m.Groups[1].Value;
            Match m2 = NumStemRegex.Match(fileName);
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
}

