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
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

namespace ShihabMehediIDM
{
    public enum ShihabAppInstallMode
    {
        Portable = 0,
        InstalledSetup = 1
    }

    [DataContract]
    public sealed class ShihabGitHubAssetDto
    {
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "size")] public long Size { get; set; }
        [DataMember(Name = "browser_download_url")] public string BrowserDownloadUrl { get; set; }
        [DataMember(Name = "content_type")] public string ContentType { get; set; }
        [DataMember(Name = "digest")] public string Digest { get; set; }
    }

    [DataContract]
    public sealed class ShihabGitHubReleaseDto
    {
        [DataMember(Name = "tag_name")] public string TagName { get; set; }
        [DataMember(Name = "name")] public string Name { get; set; }
        [DataMember(Name = "body")] public string Body { get; set; }
        [DataMember(Name = "html_url")] public string HtmlUrl { get; set; }
        [DataMember(Name = "published_at")] public string PublishedAt { get; set; }
        [DataMember(Name = "draft")] public bool Draft { get; set; }
        [DataMember(Name = "prerelease")] public bool Prerelease { get; set; }
        [DataMember(Name = "assets")] public List<ShihabGitHubAssetDto> Assets { get; set; }
    }

    public sealed class ShihabUpdateCheckResult
    {
        public bool Ok { get; set; }
        public bool UpdateAvailable { get; set; }
        public string CurrentVersion { get; set; }
        public string LatestVersion { get; set; }
        public string TagName { get; set; }
        public string ReleaseTitle { get; set; }
        public string ReleaseNotes { get; set; }
        public string HtmlUrl { get; set; }
        public string PublishedAt { get; set; }
        public bool IsPrerelease { get; set; }
        public ShihabAppInstallMode InstallMode { get; set; }
        public ShihabGitHubAssetDto PrimaryAsset { get; set; }
        public ShihabGitHubAssetDto ExtensionZipAsset { get; set; }
        public ShihabGitHubAssetDto Sha256SumsAsset { get; set; }
        public string ErrorMessage { get; set; }

        public ShihabUpdateCheckResult()
        {
            CurrentVersion = ShihabAutoUpdater.GetCurrentAppVersion();
            LatestVersion = CurrentVersion;
            TagName = "";
            ReleaseTitle = "";
            ReleaseNotes = "";
            HtmlUrl = "https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER/releases";
            PublishedAt = "";
            ErrorMessage = "";
        }
    }

    public static class ShihabAutoUpdater
    {
        public const string DefaultRepoSlug = "sshihabb007/MHS-DOWNLOAD-MANAGER";
        private const string UninstallRegKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MHSDownloadManager";
        private static readonly object _updateLock = new object();
        private static ShihabUpdateCheckResult _lastCachedUpdate;

        public static ShihabUpdateCheckResult LastCachedUpdate
        {
            get { lock (_updateLock) { return _lastCachedUpdate; } }
        }

        public static void EnforceModernTls()
        {
            try
            {
                SecurityProtocolType tls12 = SecurityProtocolType.Tls12;
                SecurityProtocolType tls13 = (SecurityProtocolType)12288;
                try
                {
                    ServicePointManager.SecurityProtocol = tls12 | tls13;
                }
                catch
                {
                    ServicePointManager.SecurityProtocol = tls12;
                }
            }
            catch { }
        }

        public static string GetCurrentAppVersion()
        {
            try
            {
                Version v = Assembly.GetExecutingAssembly().GetName().Version;
                if (v != null)
                {
                    if (v.Revision > 0)
                    {
                        return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}.{3}", v.Major, v.Minor, Math.Max(0, v.Build), v.Revision);
                    }
                    return string.Format(CultureInfo.InvariantCulture, "{0}.{1}.{2}", v.Major, v.Minor, Math.Max(0, v.Build));
                }
            }
            catch { }
            return "1.0.4";
        }

        public static string NormalizeVersionString(string rawTagOrVer)
        {
            if (string.IsNullOrWhiteSpace(rawTagOrVer)) return "0.0.0";
            string s = rawTagOrVer.Trim();
            while (s.Length > 0 && (s[0] == 'v' || s[0] == 'V'))
            {
                s = s.Substring(1).Trim();
            }
            int plusIdx = s.IndexOf('+');
            if (plusIdx >= 0) s = s.Substring(0, plusIdx);
            return string.IsNullOrWhiteSpace(s) ? "0.0.0" : s;
        }

        public static int CompareSemVer(string versionA, string versionB)
        {
            string normA = NormalizeVersionString(versionA);
            string normB = NormalizeVersionString(versionB);

            string coreA = normA;
            string preA = "";
            int dashA = normA.IndexOf('-');
            if (dashA >= 0)
            {
                coreA = normA.Substring(0, dashA);
                preA = normA.Substring(dashA + 1);
            }

            string coreB = normB;
            string preB = "";
            int dashB = normB.IndexOf('-');
            if (dashB >= 0)
            {
                coreB = normB.Substring(0, dashB);
                preB = normB.Substring(dashB + 1);
            }

            string[] partsA = coreA.Split('.');
            string[] partsB = coreB.Split('.');
            int maxLen = Math.Max(3, Math.Max(partsA.Length, partsB.Length));
            for (int i = 0; i < maxLen; i++)
            {
                long numA = 0;
                long numB = 0;
                if (i < partsA.Length) long.TryParse(partsA[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numA);
                if (i < partsB.Length) long.TryParse(partsB[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out numB);
                if (numA != numB) return numA > numB ? 1 : -1;
            }

            bool hasPreA = !string.IsNullOrEmpty(preA);
            bool hasPreB = !string.IsNullOrEmpty(preB);
            if (!hasPreA && !hasPreB) return 0;
            if (!hasPreA && hasPreB) return 1;
            if (hasPreA && !hasPreB) return -1;

            string[] preSegA = preA.Split('.');
            string[] preSegB = preB.Split('.');
            int minPre = Math.Min(preSegA.Length, preSegB.Length);
            for (int i = 0; i < minPre; i++)
            {
                long pNumA;
                long pNumB;
                bool isNumA = long.TryParse(preSegA[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out pNumA);
                bool isNumB = long.TryParse(preSegB[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out pNumB);
                if (isNumA && isNumB)
                {
                    if (pNumA != pNumB) return pNumA > pNumB ? 1 : -1;
                }
                else if (isNumA != isNumB)
                {
                    return isNumA ? -1 : 1;
                }
                else
                {
                    int cmp = string.Compare(preSegA[i], preSegB[i], StringComparison.OrdinalIgnoreCase);
                    if (cmp != 0) return cmp > 0 ? 1 : -1;
                }
            }
            return preSegA.Length.CompareTo(preSegB.Length);
        }

        public static bool IsNewerVersion(string candidateTag, string currentVersion)
        {
            return CompareSemVer(candidateTag, currentVersion) > 0;
        }

        public static ShihabAppInstallMode DetectCurrentInstallMode()
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule.FileName;
                string exeDir = Path.GetFullPath(Path.GetDirectoryName(exePath)).TrimEnd('\\', '/');
                if (File.Exists(Path.Combine(exeDir, "Uninstall.exe")))
                {
                    return ShihabAppInstallMode.InstalledSetup;
                }
                using (RegistryKey rk = Registry.CurrentUser.OpenSubKey(UninstallRegKeyPath, false))
                {
                    if (rk != null)
                    {
                        object locObj = rk.GetValue("InstallLocation");
                        if (locObj != null && !string.IsNullOrWhiteSpace(locObj.ToString()))
                        {
                            string regDir = Path.GetFullPath(locObj.ToString().Trim()).TrimEnd('\\', '/');
                            if (string.Equals(exeDir, regDir, StringComparison.OrdinalIgnoreCase))
                            {
                                return ShihabAppInstallMode.InstalledSetup;
                            }
                        }
                    }
                }
            }
            catch { }
            return ShihabAppInstallMode.Portable;
        }

        public static void TidyOldUpdateArtifacts(string dataDirectory)
        {
            try
            {
                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string oldBackup = currentExe + ".old";
                if (File.Exists(oldBackup))
                {
                    try { File.Delete(oldBackup); } catch { }
                }
                string exeDir = Path.GetDirectoryName(currentExe);
                if (!string.IsNullOrEmpty(exeDir) && Directory.Exists(exeDir))
                {
                    string[] oldFiles = Directory.GetFiles(exeDir, "*.old");
                    for (int i = 0; i < oldFiles.Length; i++)
                    {
                        try { File.Delete(oldFiles[i]); } catch { }
                    }
                }
            }
            catch { }

            try
            {
                if (!string.IsNullOrWhiteSpace(dataDirectory))
                {
                    string updatesDir = Path.Combine(dataDirectory, "updates");
                    if (Directory.Exists(updatesDir))
                    {
                        string[] partials = Directory.GetFiles(updatesDir, "*.download");
                        for (int i = 0; i < partials.Length; i++)
                        {
                            try { File.Delete(partials[i]); } catch { }
                        }
                        string[] files = Directory.GetFiles(updatesDir);
                        DateTime cutoff = DateTime.UtcNow.AddDays(-7);
                        for (int i = 0; i < files.Length; i++)
                        {
                            try
                            {
                                if (File.GetLastWriteTimeUtc(files[i]) < cutoff)
                                {
                                    File.Delete(files[i]);
                                }
                            }
                            catch { }
                        }
                    }
                }
            }
            catch { }
        }

        private static HttpWebRequest CreateUpdateRequest(string url, MehediAppConfiguration cfg)
        {
            EnforceModernTls();
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.Timeout = 20000;
            req.ReadWriteTimeout = 30000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.UserAgent = "MHS-Download-Manager/" + GetCurrentAppVersion() + " (+https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER)";
            req.Accept = "application/vnd.github+json, application/octet-stream, */*";
            if (cfg != null)
            {
                ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, url);
            }
            return req;
        }

        public static ShihabUpdateCheckResult CheckForUpdates(MehediAppConfiguration cfg, bool ignoreSkippedVersion)
        {
            ShihabUpdateCheckResult res = new ShihabUpdateCheckResult();
            res.InstallMode = DetectCurrentInstallMode();
            string currentVer = GetCurrentAppVersion();
            res.CurrentVersion = currentVer;

            try
            {
                string slug = (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabUpdateRepoSlug))
                    ? cfg.ShihabUpdateRepoSlug.Trim().Trim('/')
                    : DefaultRepoSlug;
                bool includePre = cfg != null && cfg.ShihabUpdateIncludePrereleases;

                string apiUrl;
                if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabUpdateApiOverrideUrl))
                {
                    apiUrl = cfg.ShihabUpdateApiOverrideUrl.Trim();
                }
                else if (includePre)
                {
                    apiUrl = "https://api.github.com/repos/" + slug + "/releases?per_page=10";
                }
                else
                {
                    apiUrl = "https://api.github.com/repos/" + slug + "/releases/latest";
                }

                HttpWebRequest req = CreateUpdateRequest(apiUrl, cfg);
                byte[] jsonBytes;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream st = resp.GetResponseStream())
                using (MemoryStream ms = new MemoryStream())
                {
                    byte[] buf = new byte[16384];
                    int r;
                    while ((r = st.Read(buf, 0, buf.Length)) > 0)
                    {
                        ms.Write(buf, 0, r);
                        if (ms.Length > 5 * 1024 * 1024) break;
                    }
                    jsonBytes = ms.ToArray();
                }

                if (jsonBytes.Length >= 3 && jsonBytes[0] == 0xEF && jsonBytes[1] == 0xBB && jsonBytes[2] == 0xBF)
                {
                    byte[] noBom = new byte[jsonBytes.Length - 3];
                    Buffer.BlockCopy(jsonBytes, 3, noBom, 0, noBom.Length);
                    jsonBytes = noBom;
                }

                ShihabGitHubReleaseDto chosen = null;
                string trimmedStart = Encoding.UTF8.GetString(jsonBytes).TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
                if (trimmedStart.StartsWith("[", StringComparison.Ordinal))
                {
                    using (MemoryStream ms = new MemoryStream(jsonBytes))
                    {
                        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(List<ShihabGitHubReleaseDto>));
                        List<ShihabGitHubReleaseDto> list = ser.ReadObject(ms) as List<ShihabGitHubReleaseDto>;
                        if (list != null)
                        {
                            for (int i = 0; i < list.Count; i++)
                            {
                                ShihabGitHubReleaseDto cand = list[i];
                                if (cand == null || cand.Draft || string.IsNullOrWhiteSpace(cand.TagName)) continue;
                                if (!includePre && cand.Prerelease) continue;
                                if (chosen == null || CompareSemVer(cand.TagName, chosen.TagName) > 0)
                                {
                                    chosen = cand;
                                }
                            }
                        }
                    }
                }
                else
                {
                    using (MemoryStream ms = new MemoryStream(jsonBytes))
                    {
                        DataContractJsonSerializer ser = new DataContractJsonSerializer(typeof(ShihabGitHubReleaseDto));
                        ShihabGitHubReleaseDto single = ser.ReadObject(ms) as ShihabGitHubReleaseDto;
                        if (single != null && !single.Draft && !string.IsNullOrWhiteSpace(single.TagName))
                        {
                            if (includePre || !single.Prerelease)
                            {
                                chosen = single;
                            }
                        }
                    }
                }

                if (chosen == null)
                {
                    res.Ok = true;
                    res.UpdateAvailable = false;
                    return res;
                }

                string latestNorm = NormalizeVersionString(chosen.TagName);
                res.Ok = true;
                res.TagName = chosen.TagName;
                res.LatestVersion = latestNorm;
                res.ReleaseTitle = !string.IsNullOrWhiteSpace(chosen.Name) ? chosen.Name : ("MHS Download Manager " + chosen.TagName);
                res.ReleaseNotes = chosen.Body ?? "";
                res.HtmlUrl = !string.IsNullOrWhiteSpace(chosen.HtmlUrl)
                    ? chosen.HtmlUrl
                    : ("https://github.com/" + slug + "/releases/tag/" + chosen.TagName);
                res.PublishedAt = chosen.PublishedAt ?? "";
                res.IsPrerelease = chosen.Prerelease;

                SelectMatchingReleaseAssets(chosen, res.InstallMode, res);

                bool newer = IsNewerVersion(latestNorm, currentVer);
                if (newer && !ignoreSkippedVersion && cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabSkippedUpdateVersion))
                {
                    if (CompareSemVer(latestNorm, cfg.ShihabSkippedUpdateVersion) == 0)
                    {
                        newer = false;
                    }
                }
                res.UpdateAvailable = newer;

                if (cfg != null)
                {
                    cfg.ShihabLastUpdateCheckUtc = DateTime.UtcNow.ToString("o");
                }
                lock (_updateLock)
                {
                    _lastCachedUpdate = res;
                }
                return res;
            }
            catch (WebException wex)
            {
                HttpWebResponse httpResp = wex.Response as HttpWebResponse;
                if (httpResp != null && httpResp.StatusCode == HttpStatusCode.NotFound)
                {
                    res.Ok = true;
                    res.UpdateAvailable = false;
                    res.ErrorMessage = "No published GitHub releases yet.";
                    return res;
                }
                res.Ok = false;
                res.ErrorMessage = "Update check failed: " + wex.Message;
                return res;
            }
            catch (Exception ex)
            {
                res.Ok = false;
                res.ErrorMessage = "Update check failed: " + ex.Message;
                return res;
            }
        }

        private static void SelectMatchingReleaseAssets(
            ShihabGitHubReleaseDto release,
            ShihabAppInstallMode mode,
            ShihabUpdateCheckResult result)
        {
            if (release == null || release.Assets == null) return;
            ShihabGitHubAssetDto setupAsset = null;
            ShihabGitHubAssetDto portableAsset = null;
            ShihabGitHubAssetDto extZipAsset = null;
            ShihabGitHubAssetDto sumsAsset = null;

            for (int i = 0; i < release.Assets.Count; i++)
            {
                ShihabGitHubAssetDto a = release.Assets[i];
                if (a == null || string.IsNullOrWhiteSpace(a.Name) || string.IsNullOrWhiteSpace(a.BrowserDownloadUrl)) continue;
                string lower = a.Name.Trim().ToLowerInvariant();
                if (lower == "sha256sums.txt" || lower == "sha256sums" || lower.EndsWith(".sha256", StringComparison.Ordinal))
                {
                    if (sumsAsset == null || lower == "sha256sums.txt") sumsAsset = a;
                }
                else if (lower.EndsWith(".zip", StringComparison.Ordinal) && lower.Contains("extension"))
                {
                    extZipAsset = a;
                }
                else if (lower.EndsWith(".exe", StringComparison.Ordinal) && lower.Contains("setup"))
                {
                    setupAsset = a;
                }
                else if (lower.EndsWith(".exe", StringComparison.Ordinal) && (lower.Contains("portable") || lower == "mdm.exe"))
                {
                    portableAsset = a;
                }
            }

            result.Sha256SumsAsset = sumsAsset;
            result.ExtensionZipAsset = extZipAsset;
            if (mode == ShihabAppInstallMode.InstalledSetup)
            {
                result.PrimaryAsset = setupAsset ?? portableAsset;
            }
            else
            {
                result.PrimaryAsset = portableAsset ?? setupAsset;
            }
        }

        public static Dictionary<string, string> ParseSha256SumsText(string sumsContent)
        {
            Dictionary<string, string> map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrWhiteSpace(sumsContent)) return map;
            string[] lines = sumsContent.Split(new char[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            Regex rx = new Regex(@"^([0-9a-fA-F]{64})\s+\*?(.+)$");
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\uFEFF', ' ', '\t');
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;
                Match m = rx.Match(line);
                if (m.Success)
                {
                    string hash = m.Groups[1].Value.ToLowerInvariant();
                    string file = Path.GetFileName(m.Groups[2].Value.Trim());
                    if (!string.IsNullOrEmpty(file))
                    {
                        map[file] = hash;
                    }
                }
            }
            return map;
        }

        private static string ResolveExpectedSha256ForAsset(
            ShihabGitHubAssetDto asset,
            Dictionary<string, string> parsedSums)
        {
            if (asset == null) return "";
            if (parsedSums != null && !string.IsNullOrEmpty(asset.Name))
            {
                string fromSums;
                if (parsedSums.TryGetValue(asset.Name, out fromSums) && !string.IsNullOrEmpty(fromSums))
                {
                    return fromSums.ToLowerInvariant();
                }
            }
            if (!string.IsNullOrWhiteSpace(asset.Digest))
            {
                string d = asset.Digest.Trim();
                if (d.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                {
                    string hex = d.Substring(7).Trim().ToLowerInvariant();
                    if (hex.Length == 64) return hex;
                }
            }
            return "";
        }

        public static bool DownloadAndVerifyReleaseUpdate(
            ShihabUpdateCheckResult checkResult,
            MehediAppConfiguration cfg,
            string dataDirectory,
            Action<long, long, string> progressCallback,
            CancellationToken cancelToken,
            out string verifiedPrimaryPath,
            out string verifiedExtZipPath,
            out string errorMessage)
        {
            verifiedPrimaryPath = "";
            verifiedExtZipPath = "";
            errorMessage = "";

            if (checkResult == null || checkResult.PrimaryAsset == null || string.IsNullOrWhiteSpace(checkResult.PrimaryAsset.BrowserDownloadUrl))
            {
                errorMessage = "Release does not contain a compatible Windows x64 binary asset.";
                return false;
            }

            try
            {
                string updatesDir = Path.Combine(dataDirectory, "updates");
                Directory.CreateDirectory(updatesDir);

                Dictionary<string, string> sumsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (checkResult.Sha256SumsAsset != null && !string.IsNullOrWhiteSpace(checkResult.Sha256SumsAsset.BrowserDownloadUrl))
                {
                    if (progressCallback != null) progressCallback(0, 100, "Fetching SHA256SUMS.txt cryptographic manifest...");
                    HttpWebRequest sumsReq = CreateUpdateRequest(checkResult.Sha256SumsAsset.BrowserDownloadUrl, cfg);
                    using (HttpWebResponse sumsResp = (HttpWebResponse)sumsReq.GetResponse())
                    using (StreamReader sr = new StreamReader(sumsResp.GetResponseStream(), Encoding.UTF8))
                    {
                        sumsMap = ParseSha256SumsText(sr.ReadToEnd());
                    }
                }

                string expectedPrimaryHash = ResolveExpectedSha256ForAsset(checkResult.PrimaryAsset, sumsMap);
                if (string.IsNullOrEmpty(expectedPrimaryHash))
                {
                    errorMessage = "Security check refused update: missing SHA-256 digest in SHA256SUMS.txt or release metadata for " + checkResult.PrimaryAsset.Name;
                    return false;
                }

                string safeAssetName = ShihabFormatHelpers.SanitizeShihabFileName(checkResult.PrimaryAsset.Name);
                string tempPart = Path.Combine(updatesDir, safeAssetName + ".download");
                string finalVerified = Path.Combine(updatesDir, safeAssetName);

                if (!DownloadStreamWithSha256Check(
                        checkResult.PrimaryAsset.BrowserDownloadUrl,
                        tempPart,
                        finalVerified,
                        expectedPrimaryHash,
                        checkResult.PrimaryAsset.Size,
                        cfg,
                        progressCallback,
                        cancelToken,
                        out errorMessage))
                {
                    return false;
                }

                verifiedPrimaryPath = finalVerified;

                if (checkResult.InstallMode == ShihabAppInstallMode.Portable &&
                    checkResult.ExtensionZipAsset != null &&
                    !string.IsNullOrWhiteSpace(checkResult.ExtensionZipAsset.BrowserDownloadUrl))
                {
                    string expectedExtHash = ResolveExpectedSha256ForAsset(checkResult.ExtensionZipAsset, sumsMap);
                    if (!string.IsNullOrEmpty(expectedExtHash))
                    {
                        string extName = ShihabFormatHelpers.SanitizeShihabFileName(checkResult.ExtensionZipAsset.Name);
                        string extPart = Path.Combine(updatesDir, extName + ".download");
                        string extFinal = Path.Combine(updatesDir, extName);
                        string extErr;
                        if (DownloadStreamWithSha256Check(
                                checkResult.ExtensionZipAsset.BrowserDownloadUrl,
                                extPart,
                                extFinal,
                                expectedExtHash,
                                checkResult.ExtensionZipAsset.Size,
                                cfg,
                                progressCallback,
                                cancelToken,
                                out extErr))
                        {
                            verifiedExtZipPath = extFinal;
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                errorMessage = "Update download failed: " + ex.Message;
                return false;
            }
        }

        public static bool DownloadStreamWithSha256Check(
            string downloadUrl,
            string tempDownloadPath,
            string finalVerifiedPath,
            string expectedSha256Hex,
            long knownSizeBytes,
            MehediAppConfiguration cfg,
            Action<long, long, string> progressCallback,
            CancellationToken cancelToken,
            out string errorMessage)
        {
            errorMessage = "";
            try
            {
                if (File.Exists(tempDownloadPath)) File.Delete(tempDownloadPath);
                HttpWebRequest req = CreateUpdateRequest(downloadUrl, cfg);
                using (cancelToken.Register(delegate { try { req.Abort(); } catch { } }))
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (Stream net = resp.GetResponseStream())
                using (SHA256 sha = SHA256.Create())
                {
                    long total = resp.ContentLength > 0 ? resp.ContentLength : knownSizeBytes;
                    long got = 0;
                    byte[] buf = new byte[65536];
                    using (FileStream fs = new FileStream(tempDownloadPath, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        int r;
                        while ((r = net.Read(buf, 0, buf.Length)) > 0)
                        {
                            if (cancelToken.IsCancellationRequested)
                            {
                                throw new OperationCanceledException("Update download canceled.");
                            }
                            fs.Write(buf, 0, r);
                            sha.TransformBlock(buf, 0, r, null, 0);
                            got += r;
                            if (progressCallback != null)
                            {
                                progressCallback(got, total, string.Format("Downloading update ({0} / {1})...",
                                    ShihabFormatHelpers.FormatMehediBytes(got),
                                    total > 0 ? ShihabFormatHelpers.FormatMehediBytes(total) : "Unknown"));
                            }
                        }
                        sha.TransformFinalBlock(new byte[0], 0, 0);
                        fs.Flush(true);
                    }

                    StringBuilder sb = new StringBuilder(64);
                    byte[] hashBytes = sha.Hash;
                    for (int i = 0; i < hashBytes.Length; i++) sb.Append(hashBytes[i].ToString("x2"));
                    string actualHex = sb.ToString();

                    if (!string.IsNullOrWhiteSpace(expectedSha256Hex) &&
                        !string.Equals(actualHex, expectedSha256Hex.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        try { File.Delete(tempDownloadPath); } catch { }
                        errorMessage = string.Format("SHA-256 verification FAILED! Expected {0}, got {1}. Corrupted or tampered file was deleted.",
                            expectedSha256Hex, actualHex);
                        return false;
                    }
                }

                if (File.Exists(finalVerifiedPath)) File.Delete(finalVerifiedPath);
                File.Move(tempDownloadPath, finalVerifiedPath);
                if (progressCallback != null) progressCallback(100, 100, "SHA-256 verified! Ready to install.");
                return true;
            }
            catch (OperationCanceledException)
            {
                try { if (File.Exists(tempDownloadPath)) File.Delete(tempDownloadPath); } catch { }
                errorMessage = "Update canceled by user.";
                return false;
            }
            catch (Exception ex)
            {
                try { if (File.Exists(tempDownloadPath)) File.Delete(tempDownloadPath); } catch { }
                errorMessage = ex.Message;
                return false;
            }
        }

        public static bool ApplyVerifiedUpdateAndRestart(
            ShihabUpdateCheckResult checkResult,
            string verifiedPrimaryPath,
            string verifiedExtZipPath,
            Action cleanExitCallback,
            out string errorMessage)
        {
            errorMessage = "";
            try
            {
                if (string.IsNullOrWhiteSpace(verifiedPrimaryPath) || !File.Exists(verifiedPrimaryPath))
                {
                    errorMessage = "Verified update package does not exist on disk.";
                    return false;
                }

                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string currentDir = Path.GetFullPath(Path.GetDirectoryName(currentExe));
                string currentVer = GetCurrentAppVersion();
                int currentPid = Process.GetCurrentProcess().Id;

                bool isSetupAsset = Path.GetFileName(verifiedPrimaryPath).IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0;
                if (checkResult.InstallMode == ShihabAppInstallMode.InstalledSetup || isSetupAsset)
                {
                    string args = string.Format(
                        CultureInfo.InvariantCulture,
                        "--shihab-silent --shihab-update-mode --shihab-wait-pid {0} --shihab-relaunch --shihab-updated-from \"{1}\" --shihab-install-dir \"{2}\"",
                        currentPid,
                        currentVer.Replace("\"", ""),
                        currentDir.TrimEnd('\\'));

                    ProcessStartInfo psi = new ProcessStartInfo(verifiedPrimaryPath, args);
                    psi.WorkingDirectory = Path.GetDirectoryName(verifiedPrimaryPath);
                    psi.UseShellExecute = true;
                    Process.Start(psi);

                    if (cleanExitCallback != null) cleanExitCallback();
                    return true;
                }
                else
                {
                    string backupOld = currentExe + ".old";
                    try { if (File.Exists(backupOld)) File.Delete(backupOld); } catch { }
                    File.Move(currentExe, backupOld);
                    try
                    {
                        File.Copy(verifiedPrimaryPath, currentExe, true);
                    }
                    catch
                    {
                        try { if (!File.Exists(currentExe) && File.Exists(backupOld)) File.Move(backupOld, currentExe); } catch { }
                        throw;
                    }

                    if (!string.IsNullOrWhiteSpace(verifiedExtZipPath) && File.Exists(verifiedExtZipPath))
                    {
                        string siblingExtDir = Path.Combine(currentDir, "extension");
                        if (Directory.Exists(siblingExtDir))
                        {
                            SafeUpdateSiblingExtensionFolder(verifiedExtZipPath, siblingExtDir);
                        }
                    }

                    ProcessStartInfo relaunchPsi = new ProcessStartInfo(currentExe, "--shihab-updated-from \"" + currentVer.Replace("\"", "") + "\"");
                    relaunchPsi.WorkingDirectory = currentDir;
                    relaunchPsi.UseShellExecute = false;
                    Process.Start(relaunchPsi);

                    if (cleanExitCallback != null) cleanExitCallback();
                    return true;
                }
            }
            catch (Exception ex)
            {
                errorMessage = "Failed to apply update: " + ex.Message;
                return false;
            }
        }

        private static void SafeUpdateSiblingExtensionFolder(string extZipPath, string targetExtDir)
        {
            try
            {
                string fullRoot = Path.GetFullPath(targetExtDir);
                string rootWithSep = fullRoot.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                    ? fullRoot
                    : (fullRoot + Path.DirectorySeparatorChar);

                using (ZipArchive zip = ZipFile.OpenRead(extZipPath))
                {
                    for (int i = 0; i < zip.Entries.Count; i++)
                    {
                        ZipArchiveEntry e = zip.Entries[i];
                        string rel = e.FullName.Replace('/', Path.DirectorySeparatorChar);
                        if (rel.StartsWith("extension" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                        {
                            rel = rel.Substring(("extension" + Path.DirectorySeparatorChar).Length);
                        }
                        if (string.IsNullOrWhiteSpace(rel)) continue;
                        string dest = Path.GetFullPath(Path.Combine(fullRoot, rel));
                        if (!dest.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase)) continue;
                        if (e.FullName.EndsWith("/", StringComparison.Ordinal) || e.FullName.EndsWith("\\", StringComparison.Ordinal))
                        {
                            Directory.CreateDirectory(dest);
                            continue;
                        }
                        string parent = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                        using (Stream sIn = e.Open())
                        using (FileStream sOut = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.Read))
                        {
                            sIn.CopyTo(sOut);
                        }
                    }
                }
            }
            catch { }
        }
    }

    public sealed class ShihabAutoUpdateDialog : Form
    {
        private readonly ShihabUpdateCheckResult _updateInfo;
        private readonly MehediAppConfiguration _config;
        private readonly ShihabStateStore _store;
        private readonly Action _cleanExitCallback;
        private CancellationTokenSource _downloadCts;
        private bool _isDownloading;

        private readonly Panel _progressFillPanel;
        private readonly Panel _progressTrackPanel;
        private readonly Label _statusLabel;
        private readonly Button _updateNowBtn;
        private readonly Button _laterBtn;
        private readonly Button _skipBtn;

        public ShihabAutoUpdateDialog(
            ShihabUpdateCheckResult updateInfo,
            MehediAppConfiguration config,
            ShihabStateStore store,
            Action cleanExitCallback)
        {
            _updateInfo = updateInfo;
            _config = config;
            _store = store;
            _cleanExitCallback = cleanExitCallback;

            bool dark = config == null || config.ShihabDarkMode;
            Text = "Software Update Available — MHS Download Manager";
            ClientSize = new Size(620, 445);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            TopMost = true;
            BackColor = ShihabThemePalette.GetMehediWindowBg(dark);
            ForeColor = ShihabThemePalette.GetShihabPrimaryText(dark);
            Font = new Font("Segoe UI", 9.2f);
            Icon = ShihabThemePalette.CreateShihabAppIcon();
            ShihabThemePalette.ApplyMehediNativeDarkFrame(this, dark);

            Panel header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 76,
                BackColor = ShihabThemePalette.GetShihabSidebarBg(dark)
            };
            PictureBox iconBox = new PictureBox
            {
                Image = ShihabThemePalette.CreateMehediLogoBitmap(46),
                Location = new Point(18, 15),
                Size = new Size(46, 46),
                SizeMode = PictureBoxSizeMode.StretchImage
            };
            Label titleLbl = new Label
            {
                Text = "A New Version of MHS Download Manager is Ready!",
                Location = new Point(76, 13),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 11.5f),
                ForeColor = ShihabThemePalette.GetMehediAccentGreen()
            };
            string modeBadge = updateInfo.InstallMode == ShihabAppInstallMode.InstalledSetup ? "Installed Setup" : "Portable x64";
            string sizeBadge = (updateInfo.PrimaryAsset != null && updateInfo.PrimaryAsset.Size > 0)
                ? ("  •  " + ShihabFormatHelpers.FormatMehediBytes(updateInfo.PrimaryAsset.Size))
                : "";
            Label verSubLbl = new Label
            {
                Text = string.Format("Installed: v{0}   \u2192   Latest: v{1}   ({2}{3})",
                    updateInfo.CurrentVersion, updateInfo.LatestVersion, modeBadge, sizeBadge),
                Location = new Point(77, 41),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9f),
                ForeColor = ShihabThemePalette.GetMehediMutedText(dark)
            };
            header.Controls.AddRange(new Control[] { iconBox, titleLbl, verSubLbl });

            Label notesHdr = new Label
            {
                Text = "What's New in " + (string.IsNullOrWhiteSpace(updateInfo.ReleaseTitle) ? ("v" + updateInfo.LatestVersion) : updateInfo.ReleaseTitle) + ":",
                Location = new Point(18, 88),
                AutoSize = true,
                Font = new Font("Segoe UI Semibold", 9.2f)
            };
            LinkLabel ghLink = new LinkLabel
            {
                Text = "View Release on GitHub \u2197",
                Location = new Point(455, 88),
                AutoSize = true,
                LinkColor = ShihabThemePalette.GetMehediAccentCyan()
            };
            ghLink.LinkClicked += delegate
            {
                try { Process.Start(_updateInfo.HtmlUrl); } catch { }
            };

            TextBox notesBox = new TextBox
            {
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                Location = new Point(18, 114),
                Size = new Size(584, 200),
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(dark),
                ForeColor = ForeColor,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Consolas", 9f),
                Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes)
                    ? "Performance improvements, bug fixes, and verified SHA-256 GitHub release package."
                    : updateInfo.ReleaseNotes.Replace("\r\n", "\n").Replace("\n", "\r\n")
            };

            _progressTrackPanel = new Panel
            {
                Location = new Point(18, 326),
                Size = new Size(584, 18),
                BackColor = ShihabThemePalette.GetShihabSurfaceBg(dark),
                BorderStyle = BorderStyle.FixedSingle
            };
            _progressFillPanel = new Panel
            {
                Location = new Point(0, 0),
                Size = new Size(0, 18),
                BackColor = ShihabThemePalette.GetShihabAccentBlue()
            };
            _progressTrackPanel.Controls.Add(_progressFillPanel);

            _statusLabel = new Label
            {
                Text = updateInfo.Sha256SumsAsset != null
                    ? "Cryptographic SHA-256 verification (SHA256SUMS.txt) will run automatically before installing."
                    : "Ready to download and install automatically.",
                Location = new Point(18, 350),
                Size = new Size(584, 22),
                AutoEllipsis = true,
                ForeColor = ShihabThemePalette.GetMehediMutedText(dark)
            };

            _skipBtn = ShihabThemePalette.CreateMehediStyledButton("Skip This Version", ShihabThemePalette.GetShihabSurfaceBg(dark), ForeColor, 138);
            _skipBtn.Location = new Point(18, 388);
            _skipBtn.Click += delegate
            {
                if (_isDownloading) return;
                if (_config != null && _store != null)
                {
                    _config.ShihabSkippedUpdateVersion = _updateInfo.LatestVersion;
                    _store.SaveMehediConfig(_config);
                }
                DialogResult = DialogResult.Ignore;
                Close();
            };

            _laterBtn = ShihabThemePalette.CreateMehediStyledButton("Remind Me Later", ShihabThemePalette.GetShihabSurfaceBg(dark), ForeColor, 138);
            _laterBtn.Location = new Point(268, 388);
            _laterBtn.Click += delegate
            {
                if (_isDownloading && _downloadCts != null)
                {
                    _downloadCts.Cancel();
                    return;
                }
                DialogResult = DialogResult.Cancel;
                Close();
            };

            _updateNowBtn = ShihabThemePalette.CreateMehediStyledButton("Update & Restart Now", ShihabThemePalette.GetMehediAccentGreen(), Color.White, 186);
            _updateNowBtn.Location = new Point(416, 388);
            _updateNowBtn.Click += delegate { StartOneClickUpdate(); };

            Controls.AddRange(new Control[]
            {
                header, notesHdr, ghLink, notesBox, _progressTrackPanel, _statusLabel,
                _skipBtn, _laterBtn, _updateNowBtn
            });
        }

        private void StartOneClickUpdate()
        {
            if (_isDownloading) return;
            _isDownloading = true;
            _updateNowBtn.Enabled = false;
            _skipBtn.Enabled = false;
            _laterBtn.Text = "Cancel";
            _downloadCts = new CancellationTokenSource();
            CancellationToken token = _downloadCts.Token;
            string dataDir = _store != null
                ? _store.MehediBaseDirectory
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShihabMehediIDM");

            Task.Run(delegate
            {
                string primaryFile;
                string extZip;
                string err;
                bool ok = ShihabAutoUpdater.DownloadAndVerifyReleaseUpdate(
                    _updateInfo,
                    _config,
                    dataDir,
                    delegate(long got, long total, string msg)
                    {
                        if (IsDisposed) return;
                        try
                        {
                            BeginInvoke((Action)delegate
                            {
                                if (IsDisposed) return;
                                int pct = total > 0 ? (int)Math.Min(100L, (got * 100L) / total) : 50;
                                _progressFillPanel.Width = (_progressTrackPanel.ClientSize.Width * pct) / 100;
                                _statusLabel.Text = msg;
                            });
                        }
                        catch { }
                    },
                    token,
                    out primaryFile,
                    out extZip,
                    out err);

                if (IsDisposed) return;
                BeginInvoke((Action)delegate
                {
                    if (IsDisposed) return;
                    _isDownloading = false;
                    if (!ok)
                    {
                        _updateNowBtn.Enabled = true;
                        _skipBtn.Enabled = true;
                        _laterBtn.Text = "Remind Me Later";
                        _statusLabel.ForeColor = ShihabThemePalette.GetMehediAccentRed();
                        _statusLabel.Text = err;
                        MessageBox.Show(this, err, "MHS Auto-Updater", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return;
                    }

                    _progressFillPanel.Width = _progressTrackPanel.ClientSize.Width;
                    _statusLabel.ForeColor = ShihabThemePalette.GetMehediAccentGreen();
                    _statusLabel.Text = "SHA-256 verified! Applying update and restarting MHS Download Manager...";

                    string applyErr;
                    if (!ShihabAutoUpdater.ApplyVerifiedUpdateAndRestart(_updateInfo, primaryFile, extZip, _cleanExitCallback, out applyErr))
                    {
                        _updateNowBtn.Enabled = true;
                        _skipBtn.Enabled = true;
                        _laterBtn.Text = "Remind Me Later";
                        _statusLabel.ForeColor = ShihabThemePalette.GetMehediAccentRed();
                        _statusLabel.Text = applyErr;
                        MessageBox.Show(this, applyErr, "MHS Auto-Updater", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                });
            });
        }
    }

    // =========================================================================
    // Real ISO/IEC 18004 Scannable QR Code Encoder (Byte Mode, ECC Level M/L)
    // =========================================================================
    public static class ShihabQrCodeEncoder
    {
        // Version 1..10 Total Codewords, EC Codewords Per Block (ECC-L), Num Blocks (ECC-L)
        private static readonly int[] TotalCodewordsByVer = new int[] { 0, 26, 44, 70, 100, 134, 172, 196, 242, 292, 346 };
        private static readonly int[] EccCodewordsPerBlockL = new int[] { 0, 7, 10, 15, 20, 26, 18, 20, 24, 30, 18 };
        private static readonly int[] Group1BlocksL = new int[] { 0, 1, 1, 1, 1, 1, 2, 2, 2, 2, 2 };
        private static readonly int[] Group2BlocksL = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 2 };

        private static readonly int[][] AlignmentCenters = new int[][]
        {
            new int[0],
            new int[0],
            new int[] { 6, 18 },
            new int[] { 6, 22 },
            new int[] { 6, 26 },
            new int[] { 6, 30 },
            new int[] { 6, 34 },
            new int[] { 6, 22, 38 },
            new int[] { 6, 24, 42 },
            new int[] { 6, 26, 46 },
            new int[] { 6, 28, 50 }
        };

        public static bool[,] EncodeText(string text)
        {
            byte[] payload = Encoding.UTF8.GetBytes(text ?? "");
            int version = 1;
            int dataCapacityBytes = 0;
            for (int v = 1; v <= 10; v++)
            {
                int totalBlocks = Group1BlocksL[v] + Group2BlocksL[v];
                int totalDataCw = TotalCodewordsByVer[v] - (EccCodewordsPerBlockL[v] * totalBlocks);
                int headerBytes = v <= 9 ? 2 : 3; // 4-bit mode + 8/16-bit len + 4-bit terminator
                if (payload.Length <= totalDataCw - headerBytes)
                {
                    version = v;
                    dataCapacityBytes = totalDataCw;
                    break;
                }
            }
            if (dataCapacityBytes == 0)
            {
                version = 10;
                int totalBlocks = Group1BlocksL[10] + Group2BlocksL[10];
                dataCapacityBytes = TotalCodewordsByVer[10] - (EccCodewordsPerBlockL[10] * totalBlocks);
                if (payload.Length > dataCapacityBytes - 3)
                {
                    byte[] truncated = new byte[dataCapacityBytes - 3];
                    Buffer.BlockCopy(payload, 0, truncated, 0, truncated.Length);
                    payload = truncated;
                }
            }

            List<int> bits = new List<int>(dataCapacityBytes * 8);
            AppendBits(bits, 0x4, 4); // Byte mode
            AppendBits(bits, payload.Length, version <= 9 ? 8 : 16);
            for (int i = 0; i < payload.Length; i++) AppendBits(bits, payload[i], 8);

            int maxDataBits = dataCapacityBytes * 8;
            AppendBits(bits, 0, Math.Min(4, maxDataBits - bits.Count));
            while ((bits.Count & 7) != 0) bits.Add(0);

            byte[] dataCw = new byte[dataCapacityBytes];
            int byteCount = bits.Count / 8;
            for (int i = 0; i < byteCount; i++)
            {
                int b = 0;
                for (int j = 0; j < 8; j++) b = (b << 1) | bits[i * 8 + j];
                dataCw[i] = (byte)b;
            }
            for (int i = byteCount; i < dataCapacityBytes; i++)
            {
                dataCw[i] = ((i - byteCount) % 2 == 0) ? (byte)0xEC : (byte)0x11;
            }

            byte[] allCodewords = BuildInterleavedCodewords(version, dataCw);
            int size = version * 4 + 17;
            bool[,] modules = new bool[size, size];
            bool[,] isFunction = new bool[size, size];

            DrawFunctionPatterns(version, size, modules, isFunction);
            PlaceDataCodewords(size, allCodewords, modules, isFunction);

            int bestMask = 0;
            int bestPenalty = int.MaxValue;
            for (int mask = 0; mask < 8; mask++)
            {
                bool[,] trial = (bool[,])modules.Clone();
                ApplyDataMask(size, mask, trial, isFunction);
                DrawFormatBits(size, mask, trial, isFunction);
                int pen = ComputeMaskPenalty(size, trial);
                if (pen < bestPenalty)
                {
                    bestPenalty = pen;
                    bestMask = mask;
                }
            }

            ApplyDataMask(size, bestMask, modules, isFunction);
            DrawFormatBits(size, bestMask, modules, isFunction);
            return modules;
        }

        public static Bitmap RenderBitmap(string text, int pixelSize)
        {
            return RenderBitmap(text, pixelSize, Color.FromArgb(14, 18, 28), Color.White);
        }

        public static Bitmap RenderBitmap(string text, int pixelsPerModule, int quietZoneModules)
        {
            bool[,] matrix = EncodeText(text);
            int modules = matrix.GetLength(0);
            int border = Math.Max(1, quietZoneModules);
            int scale = Math.Max(2, pixelsPerModule);
            int totalMods = modules + border * 2;
            int finalDim = totalMods * scale;

            Bitmap bmp = new Bitmap(finalDim, finalDim);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.White);
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                using (SolidBrush darkBrush = new SolidBrush(Color.FromArgb(14, 18, 28)))
                {
                    for (int y = 0; y < modules; y++)
                    {
                        for (int x = 0; x < modules; x++)
                        {
                            if (matrix[y, x])
                            {
                                g.FillRectangle(darkBrush, (x + border) * scale, (y + border) * scale, scale, scale);
                            }
                        }
                    }
                }
            }
            return bmp;
        }

        public static Bitmap RenderBitmap(string text, int pixelSize, Color darkColor, Color lightColor)
        {
            bool[,] matrix = EncodeText(text);
            int modules = matrix.GetLength(0);
            int border = 4;
            int totalMods = modules + border * 2;
            int scale = Math.Max(3, pixelSize / totalMods);
            int finalDim = totalMods * scale;

            Bitmap bmp = new Bitmap(finalDim, finalDim);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(lightColor);
                g.SmoothingMode = SmoothingMode.None;
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                using (SolidBrush darkBrush = new SolidBrush(darkColor))
                {
                    for (int y = 0; y < modules; y++)
                    {
                        for (int x = 0; x < modules; x++)
                        {
                            if (matrix[y, x])
                            {
                                g.FillRectangle(darkBrush, (x + border) * scale, (y + border) * scale, scale, scale);
                            }
                        }
                    }
                }
            }
            return bmp;
        }

        public static string RenderTerminalUnicode(string text)
        {
            bool[,] matrix = EncodeText(text);
            int n = matrix.GetLength(0);
            int border = 2;
            int total = n + border * 2;
            StringBuilder sb = new StringBuilder();
            for (int y = 0; y < total; y += 2)
            {
                for (int x = 0; x < total; x++)
                {
                    bool topDark = (x >= border && x < border + n && y >= border && y < border + n) && matrix[y - border, x - border];
                    bool botDark = (x >= border && x < border + n && (y + 1) >= border && (y + 1) < border + n) && matrix[(y + 1) - border, x - border];
                    if (!topDark && !botDark) sb.Append('\u2588');
                    else if (topDark && botDark) sb.Append(' ');
                    else if (!topDark && botDark) sb.Append('\u2580');
                    else sb.Append('\u2584');
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        private static void AppendBits(List<int> bits, int val, int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                bits.Add((val >> i) & 1);
            }
        }

        private static byte[] BuildInterleavedCodewords(int version, byte[] dataCw)
        {
            int g1 = Group1BlocksL[version];
            int g2 = Group2BlocksL[version];
            int totalBlocks = g1 + g2;
            int eccLen = EccCodewordsPerBlockL[version];
            int shortDataLen = dataCw.Length / totalBlocks;
            byte[] rsDiv = ComputeReedSolomonDivisor(eccLen);

            List<byte[]> dataBlocks = new List<byte[]>();
            List<byte[]> eccBlocks = new List<byte[]>();
            int offset = 0;
            for (int b = 0; b < totalBlocks; b++)
            {
                int dLen = shortDataLen + (b < g1 ? 0 : 1);
                byte[] dBlock = new byte[dLen];
                Buffer.BlockCopy(dataCw, offset, dBlock, 0, dLen);
                offset += dLen;
                dataBlocks.Add(dBlock);
                eccBlocks.Add(ComputeReedSolomonRemainder(dBlock, rsDiv));
            }

            List<byte> result = new List<byte>(TotalCodewordsByVer[version]);
            int maxDataLen = shortDataLen + (g2 > 0 ? 1 : 0);
            for (int i = 0; i < maxDataLen; i++)
            {
                for (int b = 0; b < totalBlocks; b++)
                {
                    if (i < dataBlocks[b].Length) result.Add(dataBlocks[b][i]);
                }
            }
            for (int i = 0; i < eccLen; i++)
            {
                for (int b = 0; b < totalBlocks; b++)
                {
                    result.Add(eccBlocks[b][i]);
                }
            }
            return result.ToArray();
        }

        private static byte GfMultiply(byte x, byte y)
        {
            int z = 0;
            for (int i = 7; i >= 0; i--)
            {
                z = (z << 1) ^ ((z >> 7) * 0x11D);
                z ^= ((y >> i) & 1) * x;
            }
            return (byte)z;
        }

        private static byte[] ComputeReedSolomonDivisor(int degree)
        {
            byte[] result = new byte[degree];
            result[degree - 1] = 1;
            byte root = 1;
            for (int i = 0; i < degree; i++)
            {
                for (int j = 0; j < degree; j++)
                {
                    result[j] = GfMultiply(result[j], root);
                    if (j + 1 < degree) result[j] ^= result[j + 1];
                }
                root = GfMultiply(root, 0x02);
            }
            return result;
        }

        private static byte[] ComputeReedSolomonRemainder(byte[] data, byte[] divisor)
        {
            byte[] result = new byte[divisor.Length];
            for (int i = 0; i < data.Length; i++)
            {
                byte factor = (byte)(data[i] ^ result[0]);
                Buffer.BlockCopy(result, 1, result, 0, result.Length - 1);
                result[result.Length - 1] = 0;
                for (int j = 0; j < divisor.Length; j++)
                {
                    result[j] ^= GfMultiply(divisor[j], factor);
                }
            }
            return result;
        }

        private static void SetFuncModule(int size, int x, int y, bool dark, bool[,] modules, bool[,] isFunc)
        {
            if (x < 0 || y < 0 || x >= size || y >= size) return;
            modules[y, x] = dark;
            isFunc[y, x] = true;
        }

        private static void DrawFinderPattern(int size, int cx, int cy, bool[,] modules, bool[,] isFunc)
        {
            for (int dy = -4; dy <= 4; dy++)
            {
                for (int dx = -4; dx <= 4; dx++)
                {
                    int xx = cx + dx;
                    int yy = cy + dy;
                    if (xx < 0 || yy < 0 || xx >= size || yy >= size) continue;
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    SetFuncModule(size, xx, yy, dist != 2 && dist != 4, modules, isFunc);
                }
            }
        }

        private static void DrawAlignmentPattern(int size, int cx, int cy, bool[,] modules, bool[,] isFunc)
        {
            for (int dy = -2; dy <= 2; dy++)
            {
                for (int dx = -2; dx <= 2; dx++)
                {
                    int dist = Math.Max(Math.Abs(dx), Math.Abs(dy));
                    SetFuncModule(size, cx + dx, cy + dy, dist != 1, modules, isFunc);
                }
            }
        }

        private static void DrawFunctionPatterns(int version, int size, bool[,] modules, bool[,] isFunc)
        {
            for (int i = 0; i < size; i++)
            {
                SetFuncModule(size, 6, i, i % 2 == 0, modules, isFunc);
                SetFuncModule(size, i, 6, i % 2 == 0, modules, isFunc);
            }
            DrawFinderPattern(size, 3, 3, modules, isFunc);
            DrawFinderPattern(size, size - 4, 3, modules, isFunc);
            DrawFinderPattern(size, 3, size - 4, modules, isFunc);

            int[] align = AlignmentCenters[version];
            for (int i = 0; i < align.Length; i++)
            {
                for (int j = 0; j < align.Length; j++)
                {
                    if ((i == 0 && j == 0) || (i == 0 && j == align.Length - 1) || (i == align.Length - 1 && j == 0))
                    {
                        continue;
                    }
                    DrawAlignmentPattern(size, align[j], align[i], modules, isFunc);
                }
            }

            DrawFormatBits(size, 0, modules, isFunc);
            if (version >= 7)
            {
                int rem = version;
                for (int i = 0; i < 12; i++) rem = (rem << 1) ^ ((rem >> 11) * 0x1F25);
                int verBits = (version << 12) | rem;
                for (int i = 0; i < 18; i++)
                {
                    bool bit = ((verBits >> i) & 1) != 0;
                    int a = size - 11 + (i % 3);
                    int b = i / 3;
                    SetFuncModule(size, a, b, bit, modules, isFunc);
                    SetFuncModule(size, b, a, bit, modules, isFunc);
                }
            }
        }

        private static void DrawFormatBits(int size, int mask, bool[,] modules, bool[,] isFunc)
        {
            int data = (0x01 << 3) | mask; // ECC Level L = 01
            int rem = data;
            for (int i = 0; i < 10; i++) rem = (rem << 1) ^ ((rem >> 9) * 0x537);
            int bits = ((data << 10) | rem) ^ 0x5412;

            for (int i = 0; i <= 5; i++) SetFuncModule(size, 8, i, ((bits >> i) & 1) != 0, modules, isFunc);
            SetFuncModule(size, 8, 7, ((bits >> 6) & 1) != 0, modules, isFunc);
            SetFuncModule(size, 8, 8, ((bits >> 7) & 1) != 0, modules, isFunc);
            SetFuncModule(size, 7, 8, ((bits >> 8) & 1) != 0, modules, isFunc);
            for (int i = 9; i < 15; i++) SetFuncModule(size, 14 - i, 8, ((bits >> i) & 1) != 0, modules, isFunc);

            for (int i = 0; i < 8; i++) SetFuncModule(size, size - 1 - i, 8, ((bits >> i) & 1) != 0, modules, isFunc);
            for (int i = 8; i < 15; i++) SetFuncModule(size, 8, size - 15 + i, ((bits >> i) & 1) != 0, modules, isFunc);
            SetFuncModule(size, 8, size - 8, true, modules, isFunc);
        }

        private static void PlaceDataCodewords(int size, byte[] data, bool[,] modules, bool[,] isFunc)
        {
            int bitIdx = 0;
            int totalBits = data.Length * 8;
            for (int right = size - 1; right >= 1; right -= 2)
            {
                if (right == 6) right = 5;
                for (int vert = 0; vert < size; vert++)
                {
                    for (int j = 0; j < 2; j++)
                    {
                        int x = right - j;
                        bool upward = ((right + 1) & 2) == 0;
                        int y = upward ? (size - 1 - vert) : vert;
                        if (!isFunc[y, x] && bitIdx < totalBits)
                        {
                            modules[y, x] = ((data[bitIdx >> 3] >> (7 - (bitIdx & 7))) & 1) != 0;
                            bitIdx++;
                        }
                    }
                }
            }
        }

        private static void ApplyDataMask(int size, int mask, bool[,] modules, bool[,] isFunc)
        {
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    if (isFunc[y, x]) continue;
                    bool invert = false;
                    switch (mask)
                    {
                        case 0: invert = (x + y) % 2 == 0; break;
                        case 1: invert = y % 2 == 0; break;
                        case 2: invert = x % 3 == 0; break;
                        case 3: invert = (x + y) % 3 == 0; break;
                        case 4: invert = ((x / 3) + (y / 2)) % 2 == 0; break;
                        case 5: invert = (x * y) % 2 + (x * y) % 3 == 0; break;
                        case 6: invert = ((x * y) % 2 + (x * y) % 3) % 2 == 0; break;
                        case 7: invert = ((x + y) % 2 + (x * y) % 3) % 2 == 0; break;
                    }
                    if (invert) modules[y, x] = !modules[y, x];
                }
            }
        }

        private static int ComputeMaskPenalty(int size, bool[,] m)
        {
            int penalty = 0;
            for (int y = 0; y < size; y++)
            {
                int run = 1;
                for (int x = 1; x < size; x++)
                {
                    if (m[y, x] == m[y, x - 1])
                    {
                        run++;
                        if (run == 5) penalty += 3;
                        else if (run > 5) penalty++;
                    }
                    else run = 1;
                }
            }
            for (int x = 0; x < size; x++)
            {
                int run = 1;
                for (int y = 1; y < size; y++)
                {
                    if (m[y, x] == m[y - 1, x])
                    {
                        run++;
                        if (run == 5) penalty += 3;
                        else if (run > 5) penalty++;
                    }
                    else run = 1;
                }
            }
            for (int y = 0; y < size - 1; y++)
            {
                for (int x = 0; x < size - 1; x++)
                {
                    bool c = m[y, x];
                    if (c == m[y, x + 1] && c == m[y + 1, x] && c == m[y + 1, x + 1]) penalty += 3;
                }
            }
            return penalty;
        }
    }

    // =========================================================================
    // Loopback HTTP + CONNECT Relay for SOCKS5, PAC, Custom DNS/DoH & VPN Binding
    // =========================================================================
    public static class ShihabLocalNetworkRelay
    {
        private static readonly object _relayLock = new object();
        private static TcpListener _listener;
        private static int _boundPort;
        private static readonly string _proxyUser = "mhsrelay";
        private static readonly string _proxyPass = Guid.NewGuid().ToString("N");
        private static MehediAppConfiguration _activeConfig;
        private static string _cachedPacUrl = "";
        private static string _cachedPacScript = "";
        private static DateTime _cachedPacAtUtc = DateTime.MinValue;

        public static event Action<string> OnVpnKillSwitchTripped;

        static ShihabLocalNetworkRelay()
        {
            try
            {
                NetworkChange.NetworkAddressChanged += HandleNetworkStateChanged;
                NetworkChange.NetworkAvailabilityChanged += delegate { HandleNetworkStateChanged(null, EventArgs.Empty); };
            }
            catch { }
        }

        private static void HandleNetworkStateChanged(object sender, EventArgs e)
        {
            MehediAppConfiguration cfg = _activeConfig;
            if (cfg == null || string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter)) return;
            IPAddress ip = ShihabDnsAndNetworkManager.GetBoundAdapterLocalIp(cfg.ShihabBoundNetworkAdapter);
            if (ip == null)
            {
                Action<string> handler = OnVpnKillSwitchTripped;
                if (handler != null) handler(cfg.ShihabBoundNetworkAdapter);
            }
        }

        public static bool NeedsLocalRelay(MehediAppConfiguration cfg)
        {
            if (cfg == null) return false;
            string pMode = (cfg.ShihabProxyMode ?? "").Trim();
            if (string.Equals(pMode, "SOCKS5", StringComparison.OrdinalIgnoreCase) || cfg.ShihabUseSocksProxy)
            {
                if (!string.IsNullOrWhiteSpace(cfg.MehediSocksHost) || !string.IsNullOrWhiteSpace(cfg.ShihabProxyAddress))
                    return true;
            }
            if (string.Equals(pMode, "PAC", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cfg.ShihabPacScriptUrl))
            {
                return true;
            }
            if (!string.IsNullOrWhiteSpace(cfg.ShihabDnsMode) &&
                !string.Equals(cfg.ShihabDnsMode, "System", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (!string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter))
            {
                return true;
            }
            return false;
        }

        public static IWebProxy GetOrStartConfiguredWebProxy(MehediAppConfiguration cfg)
        {
            _activeConfig = cfg;
            int port = EnsureStarted(cfg);
            if (port <= 0) return null;
            WebProxy wp = new WebProxy("http://127.0.0.1:" + port);
            wp.Credentials = new NetworkCredential(_proxyUser, _proxyPass);
            return wp;
        }

        public static string GetSubprocessProxyUrl(MehediAppConfiguration cfg)
        {
            if (!NeedsLocalRelay(cfg))
            {
                if (cfg != null && (string.Equals(cfg.ShihabProxyMode, "Manual", StringComparison.OrdinalIgnoreCase) || cfg.MehediUseHttpProxy) &&
                    !string.IsNullOrWhiteSpace(cfg.ShihabProxyAddress))
                {
                    return string.Format(CultureInfo.InvariantCulture, "http://{0}:{1}", cfg.ShihabProxyAddress.Trim(), cfg.MehediProxyPort > 0 ? cfg.MehediProxyPort : 8080);
                }
                return "";
            }
            int port = EnsureStarted(cfg);
            if (port <= 0) return "";
            return string.Format(CultureInfo.InvariantCulture, "http://{0}:{1}@127.0.0.1:{2}", _proxyUser, _proxyPass, port);
        }

        public static void Stop()
        {
            lock (_relayLock)
            {
                if (_listener != null)
                {
                    try { _listener.Stop(); } catch { }
                    _listener = null;
                }
                _boundPort = 0;
            }
        }

        public static int EnsureStarted(MehediAppConfiguration cfg)
        {
            _activeConfig = cfg;
            lock (_relayLock)
            {
                if (_listener != null && _boundPort > 0) return _boundPort;
                try
                {
                    _listener = new TcpListener(IPAddress.Loopback, 0);
                    _listener.Start();
                    _boundPort = ((IPEndPoint)_listener.LocalEndpoint).Port;
                    Thread acceptThread = new Thread(AcceptLoop);
                    acceptThread.IsBackground = true;
                    acceptThread.Name = "MHS-LocalNetworkRelay";
                    acceptThread.Start();
                    return _boundPort;
                }
                catch
                {
                    _listener = null;
                    _boundPort = 0;
                    return 0;
                }
            }
        }

        private static void AcceptLoop()
        {
            while (true)
            {
                TcpClient client = null;
                try
                {
                    TcpListener l = _listener;
                    if (l == null) break;
                    client = l.AcceptTcpClient();
                    TcpClient captured = client;
                    ThreadPool.QueueUserWorkItem(delegate { HandleClient(captured); });
                }
                catch
                {
                    if (client != null) { try { client.Close(); } catch { } }
                    break;
                }
            }
        }

        private static void HandleClient(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 30000;
                    client.SendTimeout = 30000;
                    NetworkStream clientStream = client.GetStream();

                    List<string> headerLines = new List<string>();
                    string firstLine = ReadAsciiLine(clientStream);
                    if (string.IsNullOrEmpty(firstLine)) return;
                    while (true)
                    {
                        string h = ReadAsciiLine(clientStream);
                        if (string.IsNullOrEmpty(h)) break;
                        headerLines.Add(h);
                        if (headerLines.Count > 120) return;
                    }

                    if (!ValidateRelayAuth(headerLines))
                    {
                        byte[] unauth = Encoding.ASCII.GetBytes("HTTP/1.1 407 Proxy Authentication Required\r\nProxy-Authenticate: Basic realm=\"MHS\"\r\nConnection: close\r\n\r\n");
                        clientStream.Write(unauth, 0, unauth.Length);
                        return;
                    }

                    string[] parts = firstLine.Split(' ');
                    if (parts.Length < 2) return;
                    string method = parts[0].ToUpperInvariant();
                    string rawTarget = parts[1];

                    string targetHost = "";
                    int targetPort = 80;
                    string fullUrl = rawTarget;

                    if (method == "CONNECT")
                    {
                        int colon = rawTarget.LastIndexOf(':');
                        if (colon > 0)
                        {
                            targetHost = rawTarget.Substring(0, colon).Trim('[', ']');
                            int.TryParse(rawTarget.Substring(colon + 1), out targetPort);
                        }
                        else
                        {
                            targetHost = rawTarget;
                            targetPort = 443;
                        }
                        fullUrl = "https://" + targetHost + ":" + targetPort + "/";
                    }
                    else
                    {
                        Uri parsed;
                        if (!Uri.TryCreate(rawTarget, UriKind.Absolute, out parsed)) return;
                        targetHost = parsed.Host;
                        targetPort = parsed.Port > 0 ? parsed.Port : (parsed.Scheme == "https" ? 443 : 80);
                    }

                    using (Socket upstream = ConnectUpstreamSocket(targetHost, targetPort, fullUrl, _activeConfig))
                    using (NetworkStream upStream = new NetworkStream(upstream, false))
                    {
                        if (method == "CONNECT")
                        {
                            byte[] okBytes = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
                            clientStream.Write(okBytes, 0, okBytes.Length);
                            clientStream.Flush();
                            PumpBidirectional(clientStream, upStream);
                        }
                        else
                        {
                            Uri u = new Uri(rawTarget);
                            StringBuilder reqSb = new StringBuilder();
                            reqSb.Append(method).Append(' ').Append(u.PathAndQuery).Append(" HTTP/1.1\r\n");
                            for (int i = 0; i < headerLines.Count; i++)
                            {
                                string hl = headerLines[i];
                                if (hl.StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase) ||
                                    hl.StartsWith("Proxy-Connection:", StringComparison.OrdinalIgnoreCase))
                                {
                                    continue;
                                }
                                reqSb.Append(hl).Append("\r\n");
                            }
                            reqSb.Append("Connection: close\r\n\r\n");
                            byte[] outHdr = Encoding.ASCII.GetBytes(reqSb.ToString());
                            upStream.Write(outHdr, 0, outHdr.Length);

                            long contentLen = 0;
                            for (int i = 0; i < headerLines.Count; i++)
                            {
                                if (headerLines[i].StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                                {
                                    long.TryParse(headerLines[i].Substring(15).Trim(), out contentLen);
                                }
                            }
                            if (contentLen > 0)
                            {
                                byte[] bBuf = new byte[16384];
                                long rem = contentLen;
                                while (rem > 0)
                                {
                                    int r = clientStream.Read(bBuf, 0, (int)Math.Min(bBuf.Length, rem));
                                    if (r <= 0) break;
                                    upStream.Write(bBuf, 0, r);
                                    rem -= r;
                                }
                            }
                            upStream.Flush();
                            upStream.CopyTo(clientStream);
                        }
                    }
                }
                catch (Exception ex)
                {
                    try
                    {
                        byte[] err = Encoding.ASCII.GetBytes("HTTP/1.1 502 Bad Gateway\r\nContent-Type: text/plain\r\nConnection: close\r\n\r\nMHS Relay Error: " + ex.Message);
                        client.GetStream().Write(err, 0, err.Length);
                    }
                    catch { }
                }
            }
        }

        private static bool ValidateRelayAuth(List<string> headers)
        {
            string expected = "Basic " + Convert.ToBase64String(Encoding.ASCII.GetBytes(_proxyUser + ":" + _proxyPass));
            for (int i = 0; i < headers.Count; i++)
            {
                if (headers[i].StartsWith("Proxy-Authorization:", StringComparison.OrdinalIgnoreCase))
                {
                    string v = headers[i].Substring(20).Trim();
                    if (string.Equals(v, expected, StringComparison.Ordinal)) return true;
                }
            }
            return false;
        }

        private static string ReadAsciiLine(Stream s)
        {
            MemoryStream ms = new MemoryStream();
            while (ms.Length < 8192)
            {
                int b = s.ReadByte();
                if (b < 0) break;
                if (b == '\n') break;
                if (b != '\r') ms.WriteByte((byte)b);
            }
            if (ms.Length == 0) return "";
            return Encoding.ASCII.GetString(ms.ToArray());
        }

        private static void PumpBidirectional(Stream a, Stream b)
        {
            ManualResetEvent done = new ManualResetEvent(false);
            ThreadPool.QueueUserWorkItem(delegate
            {
                try { a.CopyTo(b); } catch { }
                try { b.Close(); } catch { }
                done.Set();
            });
            try { b.CopyTo(a); } catch { }
            try { a.Close(); } catch { }
            done.WaitOne(5000);
        }

        private static Socket ConnectUpstreamSocket(string targetHost, int targetPort, string fullUrl, MehediAppConfiguration cfg)
        {
            IPAddress boundLocalIp = null;
            if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabBoundNetworkAdapter))
            {
                boundLocalIp = ShihabDnsAndNetworkManager.GetBoundAdapterLocalIp(cfg.ShihabBoundNetworkAdapter);
                if (boundLocalIp == null)
                {
                    throw new WebException("VPN / Network Adapter Kill-Switch blocked connection: '" + cfg.ShihabBoundNetworkAdapter + "' is disconnected.");
                }
            }

            string routeSpec = ResolveEffectiveUpstreamProxySpec(fullUrl, targetHost, cfg);
            if (routeSpec.StartsWith("SOCKS", StringComparison.OrdinalIgnoreCase))
            {
                string[] sp = routeSpec.Split(' ');
                string hostPort = sp.Length > 1 ? sp[1] : "";
                string sHost = cfg != null && !string.IsNullOrWhiteSpace(cfg.MehediSocksHost) ? cfg.MehediSocksHost : (cfg != null ? cfg.ShihabProxyAddress : "");
                int sPort = cfg != null && cfg.ShihabSocksPort > 0 ? cfg.ShihabSocksPort : 1080;
                if (!string.IsNullOrEmpty(hostPort) && hostPort.IndexOf(':') > 0)
                {
                    int cIdx = hostPort.LastIndexOf(':');
                    sHost = hostPort.Substring(0, cIdx);
                    int.TryParse(hostPort.Substring(cIdx + 1), out sPort);
                }
                return ConnectViaSocks5(
                    sHost,
                    sPort,
                    cfg != null ? cfg.ShihabProxyUser : "",
                    cfg != null ? ShihabSecretsVault.UnprotectSecret(cfg.MehediProxyPassword) : "",
                    targetHost,
                    targetPort,
                    boundLocalIp);
            }

            if (routeSpec.StartsWith("PROXY ", StringComparison.OrdinalIgnoreCase))
            {
                string hp = routeSpec.Substring(6).Trim();
                int cIdx = hp.LastIndexOf(':');
                if (cIdx > 0)
                {
                    string pHost = hp.Substring(0, cIdx);
                    int pPort = 8080;
                    int.TryParse(hp.Substring(cIdx + 1), out pPort);
                    Socket proxySock = DialDirectHappyEyeballs(pHost, pPort, cfg, boundLocalIp);
                    NetworkStream ns = new NetworkStream(proxySock, false);
                    string connectCmd = string.Format(CultureInfo.InvariantCulture, "CONNECT {0}:{1} HTTP/1.1\r\nHost: {0}:{1}\r\n\r\n", targetHost, targetPort);
                    byte[] cmdBytes = Encoding.ASCII.GetBytes(connectCmd);
                    ns.Write(cmdBytes, 0, cmdBytes.Length);
                    string statusLine = ReadAsciiLine(ns);
                    while (!string.IsNullOrEmpty(ReadAsciiLine(ns))) { }
                    if (statusLine.IndexOf(" 200 ", StringComparison.Ordinal) < 0)
                    {
                        proxySock.Close();
                        throw new IOException("Upstream HTTP proxy refused CONNECT: " + statusLine);
                    }
                    return proxySock;
                }
            }

            return DialDirectHappyEyeballs(targetHost, targetPort, cfg, boundLocalIp);
        }

        private static string ResolveEffectiveUpstreamProxySpec(string fullUrl, string host, MehediAppConfiguration cfg)
        {
            if (cfg == null) return "DIRECT";
            string mode = (cfg.ShihabProxyMode ?? "").Trim();
            if (string.Equals(mode, "SOCKS5", StringComparison.OrdinalIgnoreCase) || cfg.ShihabUseSocksProxy)
            {
                string sHost = !string.IsNullOrWhiteSpace(cfg.MehediSocksHost) ? cfg.MehediSocksHost.Trim() : (cfg.ShihabProxyAddress ?? "").Trim();
                int sPort = cfg.ShihabSocksPort > 0 ? cfg.ShihabSocksPort : 1080;
                if (!string.IsNullOrEmpty(sHost)) return "SOCKS5 " + sHost + ":" + sPort;
            }
            if (string.Equals(mode, "PAC", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(cfg.ShihabPacScriptUrl))
            {
                string script = FetchPacScript(cfg.ShihabPacScriptUrl.Trim());
                if (!string.IsNullOrEmpty(script))
                {
                    return EvaluatePacFindProxyForUrl(script, fullUrl, host);
                }
            }
            return "DIRECT";
        }

        private static string FetchPacScript(string pacUrl)
        {
            lock (_relayLock)
            {
                if (string.Equals(_cachedPacUrl, pacUrl, StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrEmpty(_cachedPacScript) &&
                    (DateTime.UtcNow - _cachedPacAtUtc).TotalMinutes < 10.0)
                {
                    return _cachedPacScript;
                }
            }
            try
            {
                if (File.Exists(pacUrl))
                {
                    string disk = File.ReadAllText(pacUrl, Encoding.UTF8);
                    lock (_relayLock) { _cachedPacUrl = pacUrl; _cachedPacScript = disk; _cachedPacAtUtc = DateTime.UtcNow; }
                    return disk;
                }
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(pacUrl);
                req.Proxy = null;
                req.Timeout = 6000;
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                using (StreamReader sr = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                {
                    string body = sr.ReadToEnd();
                    lock (_relayLock) { _cachedPacUrl = pacUrl; _cachedPacScript = body; _cachedPacAtUtc = DateTime.UtcNow; }
                    return body;
                }
            }
            catch { return ""; }
        }

        public static string EvaluatePacFindProxyForUrl(string pacScript, string url, string host)
        {
            if (string.IsNullOrWhiteSpace(pacScript)) return "DIRECT";
            string lowerHost = (host ?? "").ToLowerInvariant();
            string lowerUrl = (url ?? "").ToLowerInvariant();

            // Evaluate conditional rules (`if (dnsDomainIs(...) || shExpMatch(...) || isPlainHostName(...)) return "..."`)
            Regex ruleRx = new Regex(@"if\s*\(([^\{;]+)\)\s*\{?\s*return\s+[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            MatchCollection ruleMatches = ruleRx.Matches(pacScript);
            for (int i = 0; i < ruleMatches.Count; i++)
            {
                string cond = ruleMatches[i].Groups[1].Value;
                string retSpec = SelectPrimaryPacToken(ruleMatches[i].Groups[2].Value);
                if (EvaluatePacCondition(cond, lowerUrl, lowerHost))
                {
                    return retSpec;
                }
            }

            MatchCollection allRets = Regex.Matches(pacScript, @"return\s+[""']([^""']+)[""']", RegexOptions.IgnoreCase);
            if (allRets.Count > 0)
            {
                return SelectPrimaryPacToken(allRets[allRets.Count - 1].Groups[1].Value);
            }
            return "DIRECT";
        }

        private static string SelectPrimaryPacToken(string pacReturnVal)
        {
            if (string.IsNullOrWhiteSpace(pacReturnVal)) return "DIRECT";
            string[] parts = pacReturnVal.Split(';');
            for (int i = 0; i < parts.Length; i++)
            {
                string p = parts[i].Trim();
                if (!string.IsNullOrEmpty(p)) return p;
            }
            return "DIRECT";
        }

        private static bool EvaluatePacCondition(string cond, string url, string host)
        {
            if (cond.IndexOf("isPlainHostName", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                bool plain = host.IndexOf('.') < 0;
                return cond.TrimStart().StartsWith("!", StringComparison.Ordinal) ? !plain : plain;
            }
            Match domM = Regex.Match(cond, @"dnsDomainIs\s*\(\s*host\s*,\s*[""']([^""']+)[""']\s*\)", RegexOptions.IgnoreCase);
            if (domM.Success)
            {
                string dom = domM.Groups[1].Value.ToLowerInvariant();
                return host == dom.TrimStart('.') || host.EndsWith(dom.StartsWith(".", StringComparison.Ordinal) ? dom : ("." + dom), StringComparison.Ordinal);
            }
            Match expM = Regex.Match(cond, @"shExpMatch\s*\(\s*(host|url)\s*,\s*[""']([^""']+)[""']\s*\)", RegexOptions.IgnoreCase);
            if (expM.Success)
            {
                string subject = string.Equals(expM.Groups[1].Value, "url", StringComparison.OrdinalIgnoreCase) ? url : host;
                return ShihabFormatHelpers.WildcardMatchesMehediPattern(subject, expM.Groups[2].Value.ToLowerInvariant());
            }
            return false;
        }

        private static Socket ConnectViaSocks5(
            string socksHost,
            int socksPort,
            string username,
            string password,
            string targetHost,
            int targetPort,
            IPAddress boundLocalIp)
        {
            Socket sock = DialDirectHappyEyeballs(socksHost, socksPort, null, boundLocalIp);
            try
            {
                NetworkStream ns = new NetworkStream(sock, false);
                bool hasAuth = !string.IsNullOrEmpty(username);
                byte[] greeting = hasAuth ? new byte[] { 0x05, 0x02, 0x00, 0x02 } : new byte[] { 0x05, 0x01, 0x00 };
                ns.Write(greeting, 0, greeting.Length);

                byte[] choice = ReadExactBytes(ns, 2);
                if (choice[0] != 0x05 || choice[1] == 0xFF)
                {
                    throw new IOException("SOCKS5 proxy rejected authentication methods.");
                }
                if (choice[1] == 0x02)
                {
                    byte[] uBytes = Encoding.UTF8.GetBytes(username ?? "");
                    byte[] pBytes = Encoding.UTF8.GetBytes(password ?? "");
                    byte[] authPkt = new byte[3 + uBytes.Length + pBytes.Length];
                    authPkt[0] = 0x01;
                    authPkt[1] = (byte)uBytes.Length;
                    Buffer.BlockCopy(uBytes, 0, authPkt, 2, uBytes.Length);
                    authPkt[2 + uBytes.Length] = (byte)pBytes.Length;
                    Buffer.BlockCopy(pBytes, 0, authPkt, 3 + uBytes.Length, pBytes.Length);
                    ns.Write(authPkt, 0, authPkt.Length);
                    byte[] authResp = ReadExactBytes(ns, 2);
                    if (authResp[1] != 0x00) throw new IOException("SOCKS5 username/password authentication failed.");
                }

                byte[] hostBytes = Encoding.ASCII.GetBytes(targetHost);
                byte[] reqPkt = new byte[4 + 1 + hostBytes.Length + 2];
                reqPkt[0] = 0x05;
                reqPkt[1] = 0x01; // CONNECT
                reqPkt[2] = 0x00;
                reqPkt[3] = 0x03; // Domain name (no local DNS leak)
                reqPkt[4] = (byte)hostBytes.Length;
                Buffer.BlockCopy(hostBytes, 0, reqPkt, 5, hostBytes.Length);
                reqPkt[5 + hostBytes.Length] = (byte)((targetPort >> 8) & 0xFF);
                reqPkt[6 + hostBytes.Length] = (byte)(targetPort & 0xFF);
                ns.Write(reqPkt, 0, reqPkt.Length);

                byte[] hdr = ReadExactBytes(ns, 4);
                if (hdr[1] != 0x00) throw new IOException("SOCKS5 CONNECT failed with code " + hdr[1]);
                int skip = 0;
                if (hdr[3] == 0x01) skip = 4 + 2;
                else if (hdr[3] == 0x04) skip = 16 + 2;
                else if (hdr[3] == 0x03)
                {
                    byte[] dLen = ReadExactBytes(ns, 1);
                    skip = dLen[0] + 2;
                }
                if (skip > 0) ReadExactBytes(ns, skip);
                return sock;
            }
            catch
            {
                try { sock.Close(); } catch { }
                throw;
            }
        }

        private static byte[] ReadExactBytes(Stream s, int count)
        {
            byte[] buf = new byte[count];
            int off = 0;
            while (off < count)
            {
                int r = s.Read(buf, off, count - off);
                if (r <= 0) throw new EndOfStreamException("Unexpected EOF during proxy handshake.");
                off += r;
            }
            return buf;
        }

        private static Socket DialDirectHappyEyeballs(
            string host,
            int port,
            MehediAppConfiguration cfg,
            IPAddress boundLocalIp)
        {
            List<IPAddress> candidates = ResolveHostCandidates(host, cfg);
            if (candidates.Count == 0)
            {
                throw new SocketException((int)SocketError.HostNotFound);
            }

            Exception lastEx = null;
            for (int i = 0; i < candidates.Count; i++)
            {
                IPAddress ip = candidates[i];
                if (boundLocalIp != null && boundLocalIp.AddressFamily != ip.AddressFamily) continue;
                Socket sock = new Socket(ip.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    sock.NoDelay = true;
                    if (boundLocalIp != null)
                    {
                        sock.Bind(new IPEndPoint(boundLocalIp, 0));
                    }
                    IAsyncResult ar = sock.BeginConnect(new IPEndPoint(ip, port), null, null);
                    if (!ar.AsyncWaitHandle.WaitOne(8000))
                    {
                        sock.Close();
                        throw new TimeoutException("Connect timed out to " + ip);
                    }
                    sock.EndConnect(ar);
                    return sock;
                }
                catch (Exception ex)
                {
                    lastEx = ex;
                    try { sock.Close(); } catch { }
                }
            }
            if (lastEx != null) throw lastEx;
            throw new SocketException((int)SocketError.NetworkUnreachable);
        }

        public static List<IPAddress> ResolveHostCandidates(string host, MehediAppConfiguration cfg)
        {
            List<IPAddress> list = new List<IPAddress>();
            IPAddress literal;
            if (IPAddress.TryParse((host ?? "").Trim('[', ']'), out literal))
            {
                list.Add(literal);
                return list;
            }

            if (cfg != null && string.Equals(cfg.ShihabDnsMode, "Plain", StringComparison.OrdinalIgnoreCase) &&
                !string.IsNullOrWhiteSpace(cfg.ShihabPlainDnsServer))
            {
                IPAddress resolvedPlain = QueryPlainUdpDnsA(host, cfg.ShihabPlainDnsServer.Trim());
                if (resolvedPlain != null)
                {
                    list.Add(resolvedPlain);
                    return list;
                }
                if (!cfg.ShihabDnsFallbackToSystem) return list;
            }
            else if (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabDnsMode) &&
                     cfg.ShihabDnsMode.StartsWith("Encrypted", StringComparison.OrdinalIgnoreCase))
            {
                IPAddress dohIp = ShihabDnsAndNetworkManager.ResolveHostIp(host, cfg);
                if (dohIp != null)
                {
                    list.Add(dohIp);
                    return list;
                }
                if (!cfg.ShihabDnsFallbackToSystem) return list;
            }

            try
            {
                IPAddress[] sys = Dns.GetHostAddresses(host);
                // Interleave IPv6 + IPv4 per RFC 8305 Happy Eyeballs
                for (int i = 0; i < sys.Length; i++)
                {
                    if (sys[i].AddressFamily == AddressFamily.InterNetworkV6) list.Add(sys[i]);
                }
                for (int i = 0; i < sys.Length; i++)
                {
                    if (sys[i].AddressFamily == AddressFamily.InterNetwork) list.Add(sys[i]);
                }
            }
            catch { }
            return list;
        }

        public static IPAddress QueryPlainUdpDnsA(string domain, string dnsServerIp)
        {
            try
            {
                IPAddress serverAddr;
                if (!IPAddress.TryParse(dnsServerIp, out serverAddr)) return null;

                MemoryStream q = new MemoryStream();
                ushort txId = (ushort)(Environment.TickCount & 0xFFFF);
                q.WriteByte((byte)(txId >> 8)); q.WriteByte((byte)(txId & 0xFF));
                q.Write(new byte[] { 0x01, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }, 0, 10);
                string[] labels = domain.Trim('.').Split('.');
                for (int i = 0; i < labels.Length; i++)
                {
                    byte[] lb = Encoding.ASCII.GetBytes(labels[i]);
                    q.WriteByte((byte)lb.Length);
                    q.Write(lb, 0, lb.Length);
                }
                q.Write(new byte[] { 0x00, 0x00, 0x01, 0x00, 0x01 }, 0, 5); // Type A, Class IN
                byte[] pkt = q.ToArray();

                using (UdpClient udp = new UdpClient(serverAddr.AddressFamily))
                {
                    udp.Client.ReceiveTimeout = 3500;
                    udp.Client.SendTimeout = 3500;
                    IPEndPoint ep = new IPEndPoint(serverAddr, 53);
                    udp.Send(pkt, pkt.Length, ep);
                    IPEndPoint remote = new IPEndPoint(IPAddress.Any, 0);
                    byte[] resp = udp.Receive(ref remote);
                    if (resp == null || resp.Length < 12) return null;
                    int anCount = (resp[6] << 8) | resp[7];
                    if (anCount <= 0) return null;

                    int idx = 12;
                    while (idx < resp.Length && resp[idx] != 0)
                    {
                        if ((resp[idx] & 0xC0) == 0xC0) { idx += 2; goto AfterQName; }
                        idx += 1 + resp[idx];
                    }
                    idx++;
                AfterQName:
                    idx += 4;
                    for (int a = 0; a < anCount && idx + 10 <= resp.Length; a++)
                    {
                        if ((resp[idx] & 0xC0) == 0xC0) idx += 2;
                        else { while (idx < resp.Length && resp[idx] != 0) idx += 1 + resp[idx]; idx++; }
                        if (idx + 10 > resp.Length) break;
                        int rType = (resp[idx] << 8) | resp[idx + 1];
                        int rdLen = (resp[idx + 8] << 8) | resp[idx + 9];
                        idx += 10;
                        if (rType == 1 && rdLen == 4 && idx + 4 <= resp.Length)
                        {
                            return new IPAddress(new byte[] { resp[idx], resp[idx + 1], resp[idx + 2], resp[idx + 3] });
                        }
                        idx += rdLen;
                    }
                }
            }
            catch { }
            return null;
        }
    }

    // =========================================================================
    // Win32 Job Object Subprocess Runner (Kill-On-Close + Deadlock-Free Pipes)
    // =========================================================================
    public static class ShihabWindowsJobRunner
    {
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateJobObjectW(IntPtr lpJobAttributes, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool SetInformationJobObject(IntPtr hJob, int JobObjectInfoClass, IntPtr lpJobObjectInfo, uint cbJobObjectInfoLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool AssignProcessToJobObject(IntPtr hJob, IntPtr hProcess);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_BASIC_LIMIT_INFORMATION
        {
            public long PerProcessUserTimeLimit;
            public long PerJobUserTimeLimit;
            public uint LimitFlags;
            public UIntPtr MinimumWorkingSetSize;
            public UIntPtr MaximumWorkingSetSize;
            public uint ActiveProcessLimit;
            public UIntPtr Affinity;
            public uint PriorityClass;
            public uint SchedulingClass;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IO_COUNTERS
        {
            public ulong ReadOperationCount;
            public ulong WriteOperationCount;
            public ulong OtherOperationCount;
            public ulong ReadTransferCount;
            public ulong WriteTransferCount;
            public ulong OtherTransferCount;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct JOBOBJECT_EXTENDED_LIMIT_INFORMATION
        {
            public JOBOBJECT_BASIC_LIMIT_INFORMATION BasicLimitInformation;
            public IO_COUNTERS IoInfo;
            public UIntPtr ProcessMemoryLimit;
            public UIntPtr JobMemoryLimit;
            public UIntPtr PeakProcessMemoryUsed;
            public UIntPtr PeakJobMemoryUsed;
        }

        private static readonly IntPtr _sharedKillOnCloseJob = CreateKillOnCloseJob();

        private static IntPtr CreateKillOnCloseJob()
        {
            try
            {
                IntPtr job = CreateJobObjectW(IntPtr.Zero, null);
                if (job == IntPtr.Zero) return IntPtr.Zero;
                JOBOBJECT_EXTENDED_LIMIT_INFORMATION info = new JOBOBJECT_EXTENDED_LIMIT_INFORMATION();
                info.BasicLimitInformation.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
                int len = Marshal.SizeOf(typeof(JOBOBJECT_EXTENDED_LIMIT_INFORMATION));
                IntPtr ptr = Marshal.AllocHGlobal(len);
                try
                {
                    Marshal.StructureToPtr(info, ptr, false);
                    SetInformationJobObject(job, 9, ptr, (uint)len);
                }
                finally
                {
                    Marshal.FreeHGlobal(ptr);
                }
                return job;
            }
            catch
            {
                return IntPtr.Zero;
            }
        }

        public static void AttachProcessToKillOnCloseJob(Process proc)
        {
            if (proc == null || _sharedKillOnCloseJob == IntPtr.Zero) return;
            try { AssignProcessToJobObject(_sharedKillOnCloseJob, proc.Handle); } catch { }
        }

        public static int RunManagedSubprocess(
            string exePath,
            string arguments,
            string workingDirectory,
            int timeoutMs,
            Action<string> onStdoutLine,
            CancellationToken cancelToken,
            out string stdoutAll,
            out string stderrAll)
        {
            StringBuilder outSb = new StringBuilder();
            StringBuilder errSb = new StringBuilder();
            object sbLock = new object();

            ProcessStartInfo psi = new ProcessStartInfo(exePath, arguments);
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;
            psi.RedirectStandardOutput = true;
            psi.RedirectStandardError = true;
            psi.StandardOutputEncoding = Encoding.UTF8;
            psi.StandardErrorEncoding = Encoding.UTF8;
            if (!string.IsNullOrWhiteSpace(workingDirectory) && Directory.Exists(workingDirectory))
            {
                psi.WorkingDirectory = workingDirectory;
            }

            using (Process p = new Process())
            {
                p.StartInfo = psi;
                p.OutputDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (sbLock) { if (outSb.Length < 16 * 1024 * 1024) outSb.AppendLine(e.Data); }
                    if (onStdoutLine != null) { try { onStdoutLine(e.Data); } catch { } }
                };
                p.ErrorDataReceived += delegate(object s, DataReceivedEventArgs e)
                {
                    if (e.Data == null) return;
                    lock (sbLock) { if (errSb.Length < 2 * 1024 * 1024) errSb.AppendLine(e.Data); }
                };

                p.Start();
                AttachProcessToKillOnCloseJob(p);
                p.BeginOutputReadLine();
                p.BeginErrorReadLine();

                using (cancelToken.Register(delegate { try { if (!p.HasExited) p.Kill(); } catch { } }))
                {
                    bool exited = timeoutMs > 0 ? p.WaitForExit(timeoutMs) : p.WaitForExit(int.MaxValue);
                    if (!exited)
                    {
                        try { p.Kill(); } catch { }
                        stdoutAll = outSb.ToString();
                        stderrAll = "Subprocess timed out: " + errSb.ToString();
                        return -1;
                    }
                    p.WaitForExit();
                }

                stdoutAll = outSb.ToString();
                stderrAll = errSb.ToString();
                return p.ExitCode;
            }
        }
    }
}
