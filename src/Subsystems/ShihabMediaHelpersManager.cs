using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
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

        private const int MaxRedirects = 5;
        private const int MaxTextResponseBytes = 4 * 1024 * 1024;
        private const long MaxHelperBinaryBytes = 512L * 1024 * 1024;
        private const long MaxFfmpegExtractedEntryBytes = 256L * 1024 * 1024;
        private static readonly TimeSpan HelperRegexTimeout = TimeSpan.FromMilliseconds(250);

        private static readonly Regex Sha256LineRegex = new Regex(
            @"^([0-9a-fA-F]{64})\s+\*?(.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            HelperRegexTimeout);
        private static readonly Regex Sha256HexOnlyRegex = new Regex(
            @"^[0-9a-fA-F]{64}$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            HelperRegexTimeout);
        private static readonly Regex YtDlpProgressRegex = new Regex(
            @"\[download\]\s+([0-9]+(?:\.[0-9]+)?)%\s+of\s+~?\s*([0-9]+(?:\.[0-9]+)?)([KMGTP]?i?B)(?:\s+at\s+([0-9]+(?:\.[0-9]+)?)([KMGTP]?i?B)/s)?",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            HelperRegexTimeout);
        private static readonly Regex YtDlpDestinationRegex = new Regex(
            @"(?:\[download\] Destination:\s+|\[Merger\] Merging formats into\s+""?|\[ExtractAudio\] Destination:\s+)(.+?)\""?$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            HelperRegexTimeout);
        private static readonly Regex HtmlTitleRegex = new Regex(
            @"<title[^>]*>(.*?)</title>",
            RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            HelperRegexTimeout);
        private static readonly Regex HtmlMediaAttrRegex = new Regex(
            @"(?:src|data-src|href|content)\s*=\s*[""']([^""']+\.(?:jpg|jpeg|png|webp|gif|avif|bmp|mp4)(?:\?[^""']*)?)[""']",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            HelperRegexTimeout);
        private static readonly Regex SafeAudioFormatRegex = new Regex(
            @"^[a-z0-9]{1,12}$",
            RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant,
            HelperRegexTimeout);

        public static string GetToolsDirectory(string baseDataDir)
        {
            string root = string.IsNullOrWhiteSpace(baseDataDir)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShihabMehediIDM")
                : baseDataDir;
            string dir = Path.Combine(root, "tools");
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
            string safeBase = ShihabProcessAndPathSafety.SanitizeFileName(toolName ?? "tool", "tool");
            string exeName = safeBase.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? safeBase : safeBase + ".exe";
            string localPath;
            if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(toolsDir, exeName, out localPath))
            {
                localPath = Path.Combine(toolsDir, "tool.exe");
            }
            if (File.Exists(localPath)) return localPath;

            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
            string[] dirs = pathEnv.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            for (int i = 0; i < dirs.Length; i++)
            {
                try
                {
                    string d = dirs[i].Trim().Trim('"');
                    if (string.IsNullOrEmpty(d) || !Path.IsPathRooted(d) || d.StartsWith("\\\\", StringComparison.Ordinal))
                    {
                        continue;
                    }
                    string cand;
                    if (ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(d, exeName, out cand) && File.Exists(cand))
                    {
                        return cand;
                    }
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
                    if (!IsValidSha256Hex(expectedSha))
                    {
                        throw new InvalidOperationException("Could not retrieve a valid SHA-256 checksum from yt-dlp SHA2-256SUMS.");
                    }
                    DownloadAndVerifyBinaryFile(YtDlpDownloadUrl, target, expectedSha);
                    if (onProgressStatus != null) onProgressStatus("yt-dlp.exe SHA-256 verified and installed at " + target);
                    return true;
                }
                if (key == "gallery-dl" || key == "gallerydl")
                {
                    if (onProgressStatus != null) onProgressStatus("Downloading gallery-dl.exe from official GitHub release...");
                    string target = Path.Combine(toolsDir, "gallery-dl.exe");
                    string expectedSha = FetchExpectedSha256FromGitHubReleaseApi(GalleryDlReleaseApiUrl, "gallery-dl.exe");
                    if (!IsValidSha256Hex(expectedSha))
                    {
                        throw new InvalidOperationException("Could not retrieve a valid SHA-256 digest for gallery-dl.exe from GitHub release metadata.");
                    }
                    DownloadAndVerifyBinaryFile(GalleryDlDownloadUrl, target, expectedSha);
                    if (onProgressStatus != null) onProgressStatus("gallery-dl.exe SHA-256 verified and installed at " + target);
                    return true;
                }
                if (key == "ffmpeg")
                {
                    if (onProgressStatus != null) onProgressStatus("Downloading ffmpeg-win64-gpl.zip + checksums.sha256...");
                    string zipTmp = Path.Combine(toolsDir, "ffmpeg_tmp.zip");
                    try
                    {
                        string expectedSha = FetchExpectedSha256FromSidecar(FfmpegChecksumsUrl, "ffmpeg-master-latest-win64-gpl.zip");
                        if (!IsValidSha256Hex(expectedSha))
                        {
                            throw new InvalidOperationException("Could not retrieve a valid SHA-256 checksum from FFmpeg checksums.sha256.");
                        }
                        DownloadAndVerifyBinaryFile(FfmpegDownloadUrl, zipTmp, expectedSha);
                        if (onProgressStatus != null) onProgressStatus("Extracting ffmpeg.exe and ffprobe.exe...");
                        using (ZipArchive za = ZipFile.OpenRead(zipTmp))
                        {
                            foreach (ZipArchiveEntry entry in za.Entries)
                            {
                                string entryLeaf = Path.GetFileName(entry.FullName ?? "");
                                if (!string.Equals(entryLeaf, "ffmpeg.exe", StringComparison.OrdinalIgnoreCase) &&
                                    !string.Equals(entryLeaf, "ffprobe.exe", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                                if (entry.Length < 0 || entry.Length > MaxFfmpegExtractedEntryBytes)
                                {
                                    throw new InvalidDataException("FFmpeg archive entry exceeds safe size limit: " + entryLeaf);
                                }
                                string dest;
                                if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(toolsDir, entryLeaf.ToLowerInvariant(), out dest))
                                {
                                    throw new InvalidOperationException("Invalid FFmpeg extraction destination.");
                                }
                                ExtractZipEntryAtomically(entry, dest, MaxFfmpegExtractedEntryBytes);
                            }
                        }
                    }
                    finally
                    {
                        try { if (File.Exists(zipTmp)) File.Delete(zipTmp); } catch { }
                    }
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

        private static bool IsValidSha256Hex(string candidate)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return false;
            try { return Sha256HexOnlyRegex.IsMatch(candidate.Trim()); }
            catch { return false; }
        }

        private static void ExtractZipEntryAtomically(ZipArchiveEntry entry, string destPath, long maxBytes)
        {
            string tmp = destPath + ".tmp";
            try
            {
                using (Stream inStream = entry.Open())
                using (FileStream outFs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buf = new byte[65536];
                    long total = 0;
                    int r;
                    while ((r = inStream.Read(buf, 0, buf.Length)) > 0)
                    {
                        total += r;
                        if (total > maxBytes)
                        {
                            throw new InvalidDataException("Archive entry decompressed past safety limit.");
                        }
                        outFs.Write(buf, 0, r);
                    }
                    outFs.Flush(true);
                }
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(tmp, destPath);
            }
            catch
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
                throw;
            }
        }

        private static HttpWebResponse GetValidatedHttpResponse(
            string url,
            bool httpsOnly,
            bool enforceSsrf,
            int timeoutMs,
            MehediAppConfiguration cfg,
            string userAgent,
            string acceptHeader)
        {
            string currentUrl = url;
            for (int hop = 0; hop <= MaxRedirects; hop++)
            {
                Uri parsed;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(currentUrl, httpsOnly, out parsed))
                {
                    throw new InvalidOperationException("Rejected invalid or insecure URL: " + currentUrl);
                }
                if (enforceSsrf && ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(parsed.Host, true))
                {
                    throw new InvalidOperationException("Blocked outbound request to local/internal host: " + parsed.Host);
                }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(parsed);
                req.Method = "GET";
                req.AllowAutoRedirect = false;
                req.Timeout = timeoutMs;
                req.ReadWriteTimeout = timeoutMs;
                req.UserAgent = string.IsNullOrEmpty(userAgent)
                    ? ("MHS-Download-Manager/" + ShihabAutoUpdater.GetCurrentAppVersion())
                    : userAgent;
                if (!string.IsNullOrEmpty(acceptHeader))
                {
                    req.Accept = acceptHeader;
                }
                if (cfg != null)
                {
                    ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, parsed.AbsoluteUri);
                }

                HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                int code = (int)resp.StatusCode;
                if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                {
                    string location = resp.Headers["Location"];
                    resp.Close();
                    if (string.IsNullOrWhiteSpace(location) || hop == MaxRedirects)
                    {
                        throw new WebException("Invalid or excessive HTTP redirects while fetching resource.");
                    }
                    Uri nextUri;
                    if (!Uri.TryCreate(parsed, location, out nextUri))
                    {
                        throw new InvalidOperationException("Malformed redirect target.");
                    }
                    currentUrl = nextUri.AbsoluteUri;
                    continue;
                }
                return resp;
            }
            throw new WebException("Exceeded maximum redirect count.");
        }

        private static string ReadBoundedUtf8Text(Stream stream, int maxBytes)
        {
            if (stream == null) return "";
            using (MemoryStream ms = new MemoryStream())
            {
                byte[] buf = new byte[16384];
                int r;
                while ((r = stream.Read(buf, 0, buf.Length)) > 0)
                {
                    if (ms.Length + r > maxBytes)
                    {
                        throw new InvalidOperationException("HTTP text response exceeded maximum safe size (" + maxBytes + " bytes).");
                    }
                    ms.Write(buf, 0, r);
                }
                return Encoding.UTF8.GetString(ms.ToArray());
            }
        }

        private static string FetchExpectedSha256FromSidecar(string sumsUrl, string assetName)
        {
            using (HttpWebResponse resp = GetValidatedHttpResponse(sumsUrl, true, true, 15000, null, null, null))
            using (Stream s = resp.GetResponseStream())
            {
                string body = ReadBoundedUtf8Text(s, MaxTextResponseBytes);
                string[] lines = body.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                for (int i = 0; i < lines.Length; i++)
                {
                    try
                    {
                        Match m = Sha256LineRegex.Match(lines[i].Trim('\uFEFF', ' ', '\t'));
                        if (m.Success && Path.GetFileName(m.Groups[2].Value.Trim()).Equals(assetName, StringComparison.OrdinalIgnoreCase))
                        {
                            return m.Groups[1].Value.ToLowerInvariant();
                        }
                    }
                    catch (RegexMatchTimeoutException) { }
                }
            }
            return "";
        }

        private static string FetchExpectedSha256FromGitHubReleaseApi(string releaseApiUrl, string assetName)
        {
            using (HttpWebResponse resp = GetValidatedHttpResponse(releaseApiUrl, true, true, 15000, null, null, "application/vnd.github+json"))
            using (Stream s = resp.GetResponseStream())
            {
                string json = ReadBoundedUtf8Text(s, MaxTextResponseBytes);
                Dictionary<string, object> root = ShihabJsonCodec.ParseObjectOrNull(json, 32);
                if (root == null) return "";
                List<object> assets = ShihabJsonCodec.GetArray(root, "assets");
                if (assets == null) return "";
                for (int i = 0; i < assets.Count; i++)
                {
                    Dictionary<string, object> asset = assets[i] as Dictionary<string, object>;
                    if (asset == null) continue;
                    string name = ShihabJsonCodec.GetString(asset, "name");
                    if (!string.Equals(name, assetName, StringComparison.OrdinalIgnoreCase)) continue;
                    string digest = ShihabJsonCodec.GetString(asset, "digest");
                    if (!string.IsNullOrEmpty(digest) && digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                    {
                        string hex = digest.Substring("sha256:".Length).Trim().ToLowerInvariant();
                        if (IsValidSha256Hex(hex)) return hex;
                    }
                }
            }
            return "";
        }

        private static void DownloadAndVerifyBinaryFile(string url, string destinationPath, string expectedSha256Hex)
        {
            if (!IsValidSha256Hex(expectedSha256Hex))
            {
                throw new InvalidOperationException("Refusing unverified helper binary download without a valid SHA-256 digest.");
            }
            string tmp = destinationPath + ".tmp";
            try
            {
                using (HttpWebResponse resp = GetValidatedHttpResponse(url, true, true, 45000, null, null, null))
                using (Stream inStream = resp.GetResponseStream())
                using (FileStream outFs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buf = new byte[65536];
                    long total = 0;
                    int r;
                    while ((r = inStream.Read(buf, 0, buf.Length)) > 0)
                    {
                        total += r;
                        if (total > MaxHelperBinaryBytes)
                        {
                            throw new InvalidDataException("Helper binary exceeded maximum safe download limit.");
                        }
                        outFs.Write(buf, 0, r);
                    }
                    outFs.Flush(true);
                }

                string actualHex = ShihabChecksumVerifier.ComputeFileChecksumHex(tmp, "SHA-256");
                if (!string.Equals(actualHex, expectedSha256Hex.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    try { File.Delete(tmp); } catch { }
                    throw new InvalidOperationException("Helper binary SHA-256 mismatch (expected " + expectedSha256Hex + ", got " + actualHex + ").");
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

        private static string StripControlCharacters(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            StringBuilder sb = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (c >= 0x20 && c != 0x7F) sb.Append(c);
            }
            return sb.ToString();
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

            List<object> fmtList = new List<object>();
            for (int i = 0; i < formats.Count; i++)
            {
                Dictionary<string, object> fd = new Dictionary<string, object>();
                fd["id"] = formats[i].FormatId ?? "";
                fd["ext"] = formats[i].Extension ?? "";
                fd["resolution"] = formats[i].ResolutionLabel ?? "";
                fd["sizeBytes"] = formats[i].FileSizeBytes;
                fd["hasVideo"] = formats[i].HasVideo;
                fd["hasAudio"] = formats[i].HasAudio;
                fmtList.Add(fd);
            }

            List<object> entryList = new List<object>();
            for (int i = 0; i < entries.Count; i++)
            {
                Dictionary<string, object> ed = new Dictionary<string, object>();
                ed["index"] = entries[i].Index;
                ed["title"] = entries[i].Title ?? "";
                ed["url"] = entries[i].Url ?? "";
                ed["duration"] = entries[i].DurationText ?? "";
                entryList.Add(ed);
            }

            Dictionary<string, object> summary = new Dictionary<string, object>();
            summary["title"] = mediaTitle ?? "";
            summary["isPlaylist"] = isPlaylist || entries.Count > 0;
            summary["formats"] = fmtList;
            summary["entries"] = entryList;
            summaryJson = ShihabJsonCodec.Serialize(summary);
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

            Uri parsedTarget;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(targetUrl, false, out parsedTarget))
            {
                errorMessage = "Empty or invalid HTTP/HTTPS media URL.";
                return false;
            }

            string ytdlpExe = GetToolExecutablePath(baseDataDir, "yt-dlp");
            if (!File.Exists(ytdlpExe))
            {
                errorMessage = "yt-dlp.exe is not installed yet (Options -> Media Helpers -> Install yt-dlp).";
                return false;
            }

            List<string> argv = new List<string>();
            argv.Add("--dump-single-json");
            argv.Add("--no-warnings");
            argv.Add("--ignore-config");
            argv.Add("--flat-playlist");
            string proxyArg = StripControlCharacters(ShihabLocalNetworkRelay.GetSubprocessProxyUrl(cfg));
            if (!string.IsNullOrEmpty(proxyArg))
            {
                argv.Add("--proxy");
                argv.Add(proxyArg);
            }
            argv.Add("--");
            argv.Add(parsedTarget.AbsoluteUri);

            string stdoutAll;
            string stderrAll;
            int exitCode = ShihabWindowsJobRunner.RunManagedSubprocess(
                ytdlpExe,
                ShihabProcessAndPathSafety.BuildWindowsArgumentString(argv.ToArray()),
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

            Dictionary<string, object> root = ShihabJsonCodec.ParseObjectOrNull(stdoutAll, 64);
            if (root != null)
            {
                mediaTitle = ShihabJsonCodec.GetString(root, "title");
                string rootType = ShihabJsonCodec.GetString(root, "_type");
                List<object> rawEntries = ShihabJsonCodec.GetArray(root, "entries");
                if (string.Equals(rootType, "playlist", StringComparison.OrdinalIgnoreCase) || rawEntries != null)
                {
                    if (rawEntries != null)
                    {
                        int idx = 1;
                        for (int i = 0; i < rawEntries.Count; i++)
                        {
                            Dictionary<string, object> entryObj = rawEntries[i] as Dictionary<string, object>;
                            if (entryObj == null) continue;
                            string eUrl = ShihabJsonCodec.GetString(entryObj, "url");
                            if (string.IsNullOrEmpty(eUrl)) eUrl = ShihabJsonCodec.GetString(entryObj, "webpage_url");
                            string eId = ShihabJsonCodec.GetString(entryObj, "id");
                            string eTitle = ShihabJsonCodec.GetString(entryObj, "title");
                            long durSec = ShihabJsonCodec.GetLong(entryObj, "duration");
                            if (string.IsNullOrEmpty(eUrl) && !string.IsNullOrEmpty(eId) &&
                                (parsedTarget.Host.EndsWith("youtube.com", StringComparison.OrdinalIgnoreCase) ||
                                 parsedTarget.Host.EndsWith("youtu.be", StringComparison.OrdinalIgnoreCase)))
                            {
                                eUrl = "https://www.youtube.com/watch?v=" + Uri.EscapeDataString(eId);
                            }
                            Uri eParsed;
                            if (ShihabProcessAndPathSafety.TryValidateHttpUrl(eUrl, false, out eParsed))
                            {
                                playlistEntries.Add(new ShihabPlaylistEntryInfo
                                {
                                    Index = idx++,
                                    Title = string.IsNullOrEmpty(eTitle) ? ("Video " + (idx - 1)) : eTitle,
                                    Url = eParsed.AbsoluteUri,
                                    DurationText = durSec > 0 ? TimeSpan.FromSeconds(durSec).ToString(@"mm\:ss") : "",
                                    Selected = true
                                });
                            }
                        }
                    }
                }

                List<object> rawFormats = ShihabJsonCodec.GetArray(root, "formats");
                if (rawFormats != null)
                {
                    for (int i = 0; i < rawFormats.Count; i++)
                    {
                        Dictionary<string, object> fmtObj = rawFormats[i] as Dictionary<string, object>;
                        if (fmtObj == null) continue;
                        string fid = ShihabJsonCodec.GetString(fmtObj, "format_id");
                        if (string.IsNullOrEmpty(fid)) continue;
                        string ext = ShihabJsonCodec.GetString(fmtObj, "ext");
                        string res = ShihabJsonCodec.GetString(fmtObj, "resolution");
                        string note = ShihabJsonCodec.GetString(fmtObj, "format_note");
                        string vcodec = ShihabJsonCodec.GetString(fmtObj, "vcodec");
                        string acodec = ShihabJsonCodec.GetString(fmtObj, "acodec");
                        long fsize = ShihabJsonCodec.GetLong(fmtObj, "filesize");
                        if (fsize <= 0) fsize = ShihabJsonCodec.GetLong(fmtObj, "filesize_approx");
                        formats.Add(new ShihabMediaFormatEntry
                        {
                            FormatId = fid,
                            Extension = string.IsNullOrEmpty(ext) ? "mp4" : ext,
                            ResolutionLabel = !string.IsNullOrEmpty(res) ? res : (!string.IsNullOrEmpty(note) ? note : fid),
                            FileSizeBytes = fsize,
                            DirectUrl = ShihabJsonCodec.GetString(fmtObj, "url"),
                            HasVideo = !string.IsNullOrEmpty(vcodec) && !string.Equals(vcodec, "none", StringComparison.OrdinalIgnoreCase),
                            HasAudio = !string.IsNullOrEmpty(acodec) && !string.Equals(acodec, "none", StringComparison.OrdinalIgnoreCase)
                        });
                    }
                }
            }

            if (string.IsNullOrEmpty(mediaTitle) && formats.Count == 0 && playlistEntries.Count == 0)
            {
                mediaTitle = ShihabDebridManager.ExtractJsonStringField(stdoutAll, "title");
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
            if (item == null)
            {
                errorMessage = "Missing download item.";
                return false;
            }
            Uri parsedTarget;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(item.MehediUrl, false, out parsedTarget))
            {
                errorMessage = "Invalid HTTP/HTTPS media URL.";
                return false;
            }

            string ytdlpExe = GetToolExecutablePath(baseDataDir, "yt-dlp");
            if (!File.Exists(ytdlpExe))
            {
                errorMessage = "yt-dlp.exe is required for this media stream. Install it from Tools / Options -> Media Helpers.";
                return false;
            }

            string saveDir = ShihabProcessAndPathSafety.NormalizeFullDirectoryPath(item.MehediSaveDirectory);
            if (string.IsNullOrEmpty(saveDir))
            {
                errorMessage = "Invalid save directory.";
                return false;
            }
            Directory.CreateDirectory(saveDir);

            string toolsDir = GetToolsDirectory(baseDataDir);
            string ffmpegExe = Path.Combine(toolsDir, "ffmpeg.exe");
            string baseStem = Path.GetFileNameWithoutExtension(ShihabFormatHelpers.SanitizeShihabFileName(item.ShihabFileName));
            if (string.IsNullOrWhiteSpace(baseStem) || baseStem == "download") baseStem = "%(title).120s";
            string outTemplate;
            if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(saveDir, baseStem + ".%(ext)s", out outTemplate))
            {
                outTemplate = Path.Combine(saveDir, "%(title).120s.%(ext)s");
            }

            List<string> argv = new List<string>();
            argv.Add("--newline");
            argv.Add("--no-warnings");
            argv.Add("--ignore-config");
            argv.Add("--continue");
            argv.Add("--no-playlist");
            if (File.Exists(ffmpegExe))
            {
                argv.Add("--ffmpeg-location");
                argv.Add(toolsDir);
            }

            string proxyUrl = StripControlCharacters(ShihabLocalNetworkRelay.GetSubprocessProxyUrl(cfg));
            if (!string.IsNullOrEmpty(proxyUrl))
            {
                argv.Add("--proxy");
                argv.Add(proxyUrl);
            }
            if (perTaskRateLimitBps > 0)
            {
                argv.Add("--limit-rate");
                argv.Add(perTaskRateLimitBps.ToString(CultureInfo.InvariantCulture));
            }

            if (!string.IsNullOrWhiteSpace(item.ShihabAudioOnlyExtractFormat))
            {
                string af = StripControlCharacters(item.ShihabAudioOnlyExtractFormat).Trim().ToLowerInvariant();
                if (SafeAudioFormatRegex.IsMatch(af))
                {
                    argv.Add("-x");
                    argv.Add("--audio-format");
                    argv.Add(af);
                    argv.Add("--audio-quality");
                    argv.Add("0");
                }
                else
                {
                    argv.Add("-f");
                    argv.Add("bv*+ba/b");
                    argv.Add("--merge-output-format");
                    argv.Add("mp4");
                }
            }
            else if (!string.IsNullOrWhiteSpace(item.ShihabYtDlpFormatSelector))
            {
                argv.Add("-f");
                argv.Add(StripControlCharacters(item.ShihabYtDlpFormatSelector));
            }
            else
            {
                argv.Add("-f");
                argv.Add("bv*+ba/b");
                argv.Add("--merge-output-format");
                argv.Add("mp4");
            }

            if (!string.IsNullOrWhiteSpace(item.ShihabSubtitleLanguages))
            {
                argv.Add("--write-subs");
                argv.Add("--write-auto-subs");
                argv.Add("--sub-langs");
                argv.Add(StripControlCharacters(item.ShihabSubtitleLanguages));
                if (item.ShihabEmbedSubtitles && File.Exists(ffmpegExe))
                {
                    argv.Add("--embed-subs");
                }
            }

            if (!string.IsNullOrWhiteSpace(item.MehediReferer))
            {
                argv.Add("--referer");
                argv.Add(StripControlCharacters(item.MehediReferer));
            }
            if (!string.IsNullOrWhiteSpace(item.MehediUserAgent))
            {
                argv.Add("--user-agent");
                argv.Add(StripControlCharacters(item.MehediUserAgent));
            }
            if (!string.IsNullOrWhiteSpace(item.ShihabCookies) && !item.MehediOmitCookies)
            {
                argv.Add("--add-header");
                argv.Add("Cookie:" + StripControlCharacters(item.ShihabCookies));
            }

            argv.Add("-o");
            argv.Add(outTemplate);
            argv.Add("--");
            argv.Add(parsedTarget.AbsoluteUri);

            string detectedOutFile = "";
            string stdoutAll;
            string stderrAll;
            int exitCode = ShihabWindowsJobRunner.RunManagedSubprocess(
                ytdlpExe,
                ShihabProcessAndPathSafety.BuildWindowsArgumentString(argv.ToArray()),
                saveDir,
                0,
                delegate(string line)
                {
                    if (string.IsNullOrWhiteSpace(line)) return;
                    try
                    {
                        Match dm = YtDlpDestinationRegex.Match(line.Trim());
                        if (dm.Success)
                        {
                            string p = dm.Groups[1].Value.Trim().Trim('"');
                            if (!string.IsNullOrEmpty(p))
                            {
                                string candidateLeaf = Path.GetFileName(p);
                                string resolved;
                                if (!string.IsNullOrEmpty(candidateLeaf) &&
                                    ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(saveDir, candidateLeaf, out resolved))
                                {
                                    detectedOutFile = resolved;
                                }
                            }
                        }
                        Match pm = YtDlpProgressRegex.Match(line);
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
                    }
                    catch (RegexMatchTimeoutException) { }
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
                string[] candFiles = Directory.GetFiles(saveDir);
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
            if (item == null)
            {
                errorMessage = "Missing download item.";
                return false;
            }
            Uri parsedTarget;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(item.MehediUrl, false, out parsedTarget))
            {
                errorMessage = "Invalid HTTP/HTTPS gallery URL.";
                return false;
            }

            string saveDir = ShihabProcessAndPathSafety.NormalizeFullDirectoryPath(item.MehediSaveDirectory);
            if (string.IsNullOrEmpty(saveDir))
            {
                errorMessage = "Invalid save directory.";
                return false;
            }
            string folderLeaf = Path.GetFileNameWithoutExtension(ShihabFormatHelpers.SanitizeShihabFileName(item.ShihabFileName));
            if (string.IsNullOrWhiteSpace(folderLeaf)) folderLeaf = "Image_Gallery";
            string targetFolder;
            if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(saveDir, folderLeaf, out targetFolder))
            {
                targetFolder = Path.Combine(saveDir, "Image_Gallery");
            }
            Directory.CreateDirectory(targetFolder);

            string gdlExe = GetToolExecutablePath(baseDataDir, "gallery-dl");
            if (File.Exists(gdlExe))
            {
                List<string> argv = new List<string>();
                argv.Add("--no-warnings");
                argv.Add("--ignore-config");
                argv.Add("--sleep");
                argv.Add("1");
                if (item.ShihabGalleryMaxItems > 0)
                {
                    argv.Add("--range");
                    argv.Add("1-" + item.ShihabGalleryMaxItems.ToString(CultureInfo.InvariantCulture));
                }
                string proxyUrl = StripControlCharacters(ShihabLocalNetworkRelay.GetSubprocessProxyUrl(cfg));
                if (!string.IsNullOrEmpty(proxyUrl))
                {
                    argv.Add("--proxy");
                    argv.Add(proxyUrl);
                }
                argv.Add("--directory");
                argv.Add(targetFolder);
                argv.Add("--");
                argv.Add(parsedTarget.AbsoluteUri);

                int count = 0;
                string stdoutAll, stderrAll;
                int code = ShihabWindowsJobRunner.RunManagedSubprocess(
                    gdlExe,
                    ShihabProcessAndPathSafety.BuildWindowsArgumentString(argv.ToArray()),
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
                    filesSavedCount = Directory.GetFiles(targetFolder, "*", SearchOption.AllDirectories).Length;
                    item.ShihabFileName = Path.GetFileName(targetFolder);
                    return true;
                }
                errorMessage = !string.IsNullOrWhiteSpace(stderrAll) ? stderrAll.Trim() : ("gallery-dl exited with code " + code);
                return false;
            }

            // Fallback to built-in gallery extractor when gallery-dl.exe is not installed
            string gTitle;
            List<string> urls = ExtractGalleryImageUrls(parsedTarget.AbsoluteUri, baseDataDir, out gTitle);
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
                    string safeLeaf = ShihabFormatHelpers.EnsureShihabUniqueFilePath(targetFolder, ShihabFormatHelpers.ExtractMehediFileNameFromUrl(urls[i]));
                    string dst;
                    if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(targetFolder, safeLeaf, out dst))
                    {
                        continue;
                    }
                    using (HttpWebResponse resp = GetValidatedHttpResponse(urls[i], false, true, 15000, cfg, null, null))
                    using (Stream s = resp.GetResponseStream())
                    using (FileStream fs = new FileStream(dst, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        byte[] buf = new byte[65536];
                        int r;
                        long singleFileBytes = 0;
                        const long maxGalleryItemBytes = 512L * 1024 * 1024;
                        while ((r = s.Read(buf, 0, buf.Length)) > 0)
                        {
                            singleFileBytes += r;
                            if (singleFileBytes > maxGalleryItemBytes) break;
                            fs.Write(buf, 0, r);
                        }
                        fs.Flush();
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
            Uri baseUri;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(pageUrl, false, out baseUri))
            {
                return results;
            }

            string gdlExe = GetToolExecutablePath(baseDataDir, "gallery-dl");
            if (File.Exists(gdlExe))
            {
                try
                {
                    string stdoutAll, stderrAll;
                    string argStr = ShihabProcessAndPathSafety.BuildWindowsArgumentString(
                        "--no-warnings",
                        "--ignore-config",
                        "--get-urls",
                        "--",
                        baseUri.AbsoluteUri);
                    int code = ShihabWindowsJobRunner.RunManagedSubprocess(
                        gdlExe,
                        argStr,
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
                            Uri candidate;
                            if (ShihabProcessAndPathSafety.TryValidateHttpUrl(lines[i], false, out candidate))
                            {
                                string abs = candidate.AbsoluteUri;
                                if (!results.Contains(abs)) results.Add(abs);
                            }
                        }
                        if (results.Count > 0) return results;
                    }
                }
                catch { }
            }

            try
            {
                galleryTitle = ShihabFormatHelpers.SanitizeShihabFileName(baseUri.Host + "_gallery");
                using (HttpWebResponse resp = GetValidatedHttpResponse(
                    baseUri.AbsoluteUri,
                    false,
                    true,
                    12000,
                    null,
                    "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/131.0.0.0 Safari/537.36",
                    null))
                using (Stream s = resp.GetResponseStream())
                {
                    string html = ReadBoundedUtf8Text(s, MaxTextResponseBytes);
                    try
                    {
                        Match tm = HtmlTitleRegex.Match(html);
                        if (tm.Success)
                        {
                            string t = WebUtility.HtmlDecode(tm.Groups[1].Value).Trim();
                            if (!string.IsNullOrEmpty(t)) galleryTitle = ShihabFormatHelpers.SanitizeShihabFileName(t);
                        }
                        MatchCollection matches = HtmlMediaAttrRegex.Matches(html);
                        for (int i = 0; i < matches.Count; i++)
                        {
                            string raw = WebUtility.HtmlDecode(matches[i].Groups[1].Value);
                            Uri abs;
                            if (Uri.TryCreate(baseUri, raw, out abs) &&
                                (abs.Scheme == Uri.UriSchemeHttp || abs.Scheme == Uri.UriSchemeHttps))
                            {
                                string u = abs.AbsoluteUri;
                                if (!results.Contains(u)) results.Add(u);
                            }
                        }
                    }
                    catch (RegexMatchTimeoutException) { }
                }
            }
            catch { }
            return results;
        }
    }
}

