using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Runtime.Serialization.Json;
using Microsoft.Win32;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public static class ShihabAutoUpdater
    {
        public const string DefaultRepoSlug = "sshihabb007/MHS-DOWNLOAD-MANAGER";
        private const string UninstallRegKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\MHSDownloadManager";
        private const int MaxRedirects = 5;
        private const long MaxJsonApiBytes = 5L * 1024L * 1024L;
        private const long MaxShaSumsBytes = 256L * 1024L;
        private const long MaxReleaseAssetBytes = 256L * 1024L * 1024L;

        private static readonly Regex ShaLineRx = new Regex(
            @"^([0-9a-fA-F]{64})\s+\*?(.+)$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant,
            TimeSpan.FromMilliseconds(250));

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
                ServicePointManager.CheckCertificateRevocationList = true;
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
            return "1.0.6";
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

        private static bool IsTrustedUpdateUri(string rawUrl, bool allowCustomOverrideHost, out Uri validatedUri)
        {
            validatedUri = null;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(rawUrl, true, out validatedUri))
            {
                return false;
            }
            if (!string.IsNullOrEmpty(validatedUri.UserInfo))
            {
                validatedUri = null;
                return false;
            }
            if (ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(validatedUri.Host, true))
            {
                validatedUri = null;
                return false;
            }
            if (!allowCustomOverrideHost && !ShihabProcessAndPathSafety.IsOfficialGitHubHttpsUri(validatedUri))
            {
                validatedUri = null;
                return false;
            }
            return true;
        }

        private static HttpWebRequest CreateSingleHopRequest(Uri uri, MehediAppConfiguration cfg)
        {
            EnforceModernTls();
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(uri);
            req.Method = "GET";
            req.AllowAutoRedirect = false;
            req.Timeout = 20000;
            req.ReadWriteTimeout = 30000;
            req.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            req.UserAgent = "MHS-Download-Manager/" + GetCurrentAppVersion() + " (+https://github.com/sshihabb007/MHS-DOWNLOAD-MANAGER)";
            req.Accept = "application/vnd.github+json, application/octet-stream, */*";
            if (cfg != null)
            {
                ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, uri.AbsoluteUri);
            }
            return req;
        }

        private static HttpWebResponse GetUpdateResponseWithSafeRedirects(
            string initialUrl,
            bool allowCustomOverrideHost,
            MehediAppConfiguration cfg,
            CancellationToken cancelToken)
        {
            Uri currentUri;
            if (!IsTrustedUpdateUri(initialUrl, allowCustomOverrideHost, out currentUri))
            {
                throw new InvalidOperationException("Refused untrusted or insecure update URL: " + initialUrl);
            }

            for (int hop = 0; hop <= MaxRedirects; hop++)
            {
                cancelToken.ThrowIfCancellationRequested();
                HttpWebRequest req = CreateSingleHopRequest(currentUri, cfg);
                using (cancelToken.Register(delegate { try { req.Abort(); } catch { } }))
                {
                    HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
                    int code = (int)resp.StatusCode;
                    if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                    {
                        string location = resp.Headers["Location"];
                        resp.Close();
                        if (string.IsNullOrWhiteSpace(location))
                        {
                            throw new WebException("Update server returned redirect without Location header.");
                        }
                        Uri resolvedNext;
                        if (!Uri.TryCreate(currentUri, location.Trim(), out resolvedNext))
                        {
                            throw new WebException("Update server returned malformed redirect URI.");
                        }
                        Uri trustedNext;
                        if (!IsTrustedUpdateUri(resolvedNext.AbsoluteUri, allowCustomOverrideHost, out trustedNext))
                        {
                            throw new InvalidOperationException("Refused insecure or off-origin update redirect: " + resolvedNext.AbsoluteUri);
                        }
                        currentUri = trustedNext;
                        continue;
                    }
                    return resp;
                }
            }
            throw new WebException("Too many update HTTP redirects.");
        }

        private static byte[] ReadBoundedResponseBytes(HttpWebResponse resp, long maxBytes, CancellationToken cancelToken)
        {
            using (Stream st = resp.GetResponseStream())
            using (MemoryStream ms = new MemoryStream())
            {
                if (st == null) return new byte[0];
                byte[] buf = new byte[16384];
                long total = 0;
                int r;
                while ((r = st.Read(buf, 0, buf.Length)) > 0)
                {
                    cancelToken.ThrowIfCancellationRequested();
                    total += r;
                    if (total > maxBytes)
                    {
                        throw new InvalidDataException("Update metadata response exceeds maximum allowed size.");
                    }
                    ms.Write(buf, 0, r);
                }
                return ms.ToArray();
            }
        }

        private static void ApplyResolvedReleaseToResult(
            ShihabGitHubReleaseDto chosen,
            string slug,
            string currentVer,
            MehediAppConfiguration cfg,
            bool ignoreSkippedVersion,
            bool hasCustomOverride,
            ShihabUpdateCheckResult res)
        {
            if (chosen == null)
            {
                res.Ok = true;
                res.UpdateAvailable = false;
                return;
            }

            string latestNorm = NormalizeVersionString(chosen.TagName);
            res.Ok = true;
            res.TagName = chosen.TagName;
            res.LatestVersion = latestNorm;
            res.ReleaseTitle = !string.IsNullOrWhiteSpace(chosen.Name) ? chosen.Name : ("MHS Download Manager " + chosen.TagName);
            res.ReleaseNotes = chosen.Body ?? "";

            string fallbackHtml = "https://github.com/" + slug + "/releases";
            Uri validatedHtml;
            if (!string.IsNullOrWhiteSpace(chosen.HtmlUrl) &&
                IsTrustedUpdateUri(chosen.HtmlUrl, hasCustomOverride, out validatedHtml))
            {
                res.HtmlUrl = validatedHtml.AbsoluteUri;
            }
            else
            {
                res.HtmlUrl = fallbackHtml;
            }

            res.PublishedAt = chosen.PublishedAt ?? "";
            res.IsPrerelease = chosen.Prerelease;

            SelectMatchingReleaseAssets(chosen, res.InstallMode, hasCustomOverride, res);

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
        }

        private static string DecodeBasicHtmlEntities(string rawText)
        {
            if (string.IsNullOrEmpty(rawText)) return "";
            string s = Regex.Replace(rawText, @"<script[\s\S]*?</script>", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<style[\s\S]*?</style>", "", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<br\s*/?>", "\r\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"</(?:p|div|li|tr|h[1-6])>", "\r\n", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<li[^>]*>", "• ", RegexOptions.IgnoreCase);
            s = Regex.Replace(s, @"<[^>]+>", "");
            s = s.Replace("&amp;", "&")
                 .Replace("&lt;", "<")
                 .Replace("&gt;", ">")
                 .Replace("&quot;", "\"")
                 .Replace("&#39;", "'")
                 .Replace("&apos;", "'")
                 .Replace("&nbsp;", " ")
                 .Replace("&mdash;", "\u2014")
                 .Replace("&ndash;", "\u2013");
            s = Regex.Replace(s, @"[ \t]{2,}", " ");
            s = Regex.Replace(s, @"(\r?\n){3,}", "\r\n\r\n");
            return s.Trim();
        }

        private static bool TryResolveLatestReleaseFromGitHubWeb(
            string slug,
            MehediAppConfiguration cfg,
            out ShihabGitHubReleaseDto resolvedDto,
            out bool webNotFound)
        {
            resolvedDto = null;
            webNotFound = false;
            try
            {
                string webLatestUrl = "https://github.com/" + slug + "/releases/latest";
                Uri finalUri = null;
                string html = "";

                using (HttpWebResponse resp = GetUpdateResponseWithSafeRedirects(webLatestUrl, false, cfg, CancellationToken.None))
                {
                    finalUri = resp.ResponseUri;
                    byte[] htmlBytes = ReadBoundedResponseBytes(resp, MaxJsonApiBytes, CancellationToken.None);
                    html = Encoding.UTF8.GetString(htmlBytes ?? new byte[0]);
                }

                string tag = "";
                if (finalUri != null && !string.IsNullOrWhiteSpace(finalUri.AbsolutePath))
                {
                    Match tagPathMatch = Regex.Match(
                        finalUri.AbsolutePath,
                        @"/releases/tag/([^/?#]+)",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (tagPathMatch.Success)
                    {
                        tag = Uri.UnescapeDataString(tagPathMatch.Groups[1].Value.Trim());
                    }
                }

                if (string.IsNullOrWhiteSpace(tag) && !string.IsNullOrEmpty(html))
                {
                    string escapedSlug = Regex.Escape(slug);
                    Match htmlTagMatch = Regex.Match(
                        html,
                        @"/" + escapedSlug + @"/releases/tag/(v?[0-9]+(?:\.[0-9]+)+(?:-[A-Za-z0-9.-]+)?)",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (htmlTagMatch.Success)
                    {
                        tag = Uri.UnescapeDataString(htmlTagMatch.Groups[1].Value.Trim());
                    }
                }

                string verNorm = NormalizeVersionString(tag);
                if (string.IsNullOrWhiteSpace(tag) || verNorm == "0.0.0")
                {
                    return false;
                }

                string releaseUrl = "https://github.com/" + slug + "/releases/tag/" + Uri.EscapeDataString(tag);
                string notesSummary = "Release " + tag + " is available on GitHub Releases.\r\n\r\nFull changelog: " + releaseUrl;
                if (!string.IsNullOrEmpty(html))
                {
                    Match bodyMatch = Regex.Match(
                        html,
                        @"<div[^>]*class=""[^""]*markdown-body[^""]*""[^>]*>([\s\S]{1,16384}?)</div>",
                        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                    if (bodyMatch.Success)
                    {
                        string extracted = DecodeBasicHtmlEntities(bodyMatch.Groups[1].Value);
                        if (!string.IsNullOrWhiteSpace(extracted))
                        {
                            notesSummary = extracted;
                        }
                    }
                }

                List<ShihabGitHubAssetDto> assets = new List<ShihabGitHubAssetDto>();
                string sumsUrl = "https://github.com/" + slug + "/releases/download/" + Uri.EscapeDataString(tag) + "/SHA256SUMS.txt";
                try
                {
                    using (HttpWebResponse sumsResp = GetUpdateResponseWithSafeRedirects(sumsUrl, false, cfg, CancellationToken.None))
                    {
                        byte[] sumsBytes = ReadBoundedResponseBytes(sumsResp, MaxShaSumsBytes, CancellationToken.None);
                        string sumsText = Encoding.UTF8.GetString(sumsBytes ?? new byte[0]).TrimStart('\uFEFF');
                        Dictionary<string, string> parsedSums = ParseSha256SumsText(sumsText);
                        if (parsedSums.Count > 0)
                        {
                            assets.Add(new ShihabGitHubAssetDto
                            {
                                Name = "SHA256SUMS.txt",
                                BrowserDownloadUrl = sumsUrl,
                                Size = sumsBytes != null ? sumsBytes.LongLength : 0
                            });
                            foreach (KeyValuePair<string, string> kv in parsedSums)
                            {
                                if (string.IsNullOrWhiteSpace(kv.Key)) continue;
                                assets.Add(new ShihabGitHubAssetDto
                                {
                                    Name = kv.Key,
                                    BrowserDownloadUrl = "https://github.com/" + slug + "/releases/download/" + Uri.EscapeDataString(tag) + "/" + Uri.EscapeDataString(kv.Key),
                                    Digest = "sha256:" + kv.Value
                                });
                            }
                        }
                    }
                }
                catch { }

                resolvedDto = new ShihabGitHubReleaseDto
                {
                    TagName = tag,
                    Name = "MHS Download Manager " + tag,
                    Body = notesSummary,
                    HtmlUrl = releaseUrl,
                    Prerelease = tag.IndexOf('-') >= 0,
                    Draft = false,
                    Assets = assets
                };
                return true;
            }
            catch (WebException wex)
            {
                try
                {
                    HttpWebResponse httpResp = wex.Response as HttpWebResponse;
                    if (httpResp != null && httpResp.StatusCode == HttpStatusCode.NotFound)
                    {
                        webNotFound = true;
                    }
                }
                finally
                {
                    try { if (wex.Response != null) wex.Response.Close(); } catch { }
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        public static ShihabUpdateCheckResult CheckForUpdates(MehediAppConfiguration cfg, bool ignoreSkippedVersion)
        {
            ShihabUpdateCheckResult res = new ShihabUpdateCheckResult();
            res.InstallMode = DetectCurrentInstallMode();
            string currentVer = GetCurrentAppVersion();
            res.CurrentVersion = currentVer;

            string slug = (cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabUpdateRepoSlug))
                ? cfg.ShihabUpdateRepoSlug.Trim().Trim('/')
                : DefaultRepoSlug;
            if (!ShihabProcessAndPathSafety.IsValidGitHubRepoSlug(slug))
            {
                res.Ok = false;
                res.ErrorMessage = "Invalid GitHub repository slug configuration.";
                return res;
            }

            bool includePre = cfg != null && cfg.ShihabUpdateIncludePrereleases;
            bool hasCustomOverride = cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabUpdateApiOverrideUrl);

            try
            {
                string apiUrl;
                if (hasCustomOverride)
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

                byte[] jsonBytes;
                using (HttpWebResponse resp = GetUpdateResponseWithSafeRedirects(apiUrl, hasCustomOverride, cfg, CancellationToken.None))
                {
                    jsonBytes = ReadBoundedResponseBytes(resp, MaxJsonApiBytes, CancellationToken.None);
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

                ApplyResolvedReleaseToResult(chosen, slug, currentVer, cfg, ignoreSkippedVersion, hasCustomOverride, res);
                return res;
            }
            catch (WebException wex)
            {
                bool apiNotFound = false;
                try
                {
                    HttpWebResponse httpResp = wex.Response as HttpWebResponse;
                    if (httpResp != null && httpResp.StatusCode == HttpStatusCode.NotFound)
                    {
                        apiNotFound = true;
                    }
                }
                finally
                {
                    try { if (wex.Response != null) wex.Response.Close(); } catch { }
                }

                // Fallback to github.com/<slug>/releases/latest + SHA256SUMS.txt when REST API hits 403 rate-limit or 429
                if (!hasCustomOverride)
                {
                    ShihabGitHubReleaseDto webDto;
                    bool webNotFound;
                    if (TryResolveLatestReleaseFromGitHubWeb(slug, cfg, out webDto, out webNotFound))
                    {
                        ApplyResolvedReleaseToResult(webDto, slug, currentVer, cfg, ignoreSkippedVersion, false, res);
                        return res;
                    }
                    if (apiNotFound || webNotFound)
                    {
                        res.Ok = true;
                        res.UpdateAvailable = false;
                        res.ErrorMessage = "No published GitHub releases yet.";
                        return res;
                    }
                }
                else if (apiNotFound)
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
                if (!hasCustomOverride)
                {
                    ShihabGitHubReleaseDto webDto;
                    bool webNotFound;
                    if (TryResolveLatestReleaseFromGitHubWeb(slug, cfg, out webDto, out webNotFound))
                    {
                        ApplyResolvedReleaseToResult(webDto, slug, currentVer, cfg, ignoreSkippedVersion, false, res);
                        return res;
                    }
                }
                res.Ok = false;
                res.ErrorMessage = "Update check failed: " + ex.Message;
                return res;
            }
        }

        private static void SelectMatchingReleaseAssets(
            ShihabGitHubReleaseDto release,
            ShihabAppInstallMode mode,
            bool allowCustomOverrideHost,
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
                Uri validUrl;
                if (!IsTrustedUpdateUri(a.BrowserDownloadUrl, allowCustomOverrideHost, out validUrl))
                {
                    continue;
                }
                if (a.Size > MaxReleaseAssetBytes) continue;

                string lower = a.Name.Trim().ToLowerInvariant();
                if (lower == "sha256sums.txt" || lower == "sha256sums" || lower.EndsWith(".sha256", StringComparison.Ordinal))
                {
                    if (sumsAsset == null || lower == "sha256sums.txt") sumsAsset = a;
                }
                else if (lower.EndsWith(".zip", StringComparison.Ordinal) && lower.Contains("extension"))
                {
                    extZipAsset = a;
                }
                else if (lower.EndsWith(".exe", StringComparison.Ordinal) && !lower.Contains("arm64") && !lower.Contains("x86"))
                {
                    if (lower.Contains("setup"))
                    {
                        setupAsset = a;
                    }
                    else if (lower.Contains("portable") || lower == "mdm.exe")
                    {
                        portableAsset = a;
                    }
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
            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim('\uFEFF', ' ', '\t');
                if (string.IsNullOrEmpty(line) || line.StartsWith("#", StringComparison.Ordinal)) continue;
                try
                {
                    Match m = ShaLineRx.Match(line);
                    if (m.Success)
                    {
                        string hash = m.Groups[1].Value.ToLowerInvariant();
                        string rawFile = m.Groups[2].Value.Trim();
                        string file = ShihabProcessAndPathSafety.SanitizeFileName(Path.GetFileName(rawFile), "");
                        if (!string.IsNullOrEmpty(file))
                        {
                            string existing;
                            if (map.TryGetValue(file, out existing) && !string.Equals(existing, hash, StringComparison.OrdinalIgnoreCase))
                            {
                                map.Remove(file);
                                continue;
                            }
                            map[file] = hash;
                        }
                    }
                }
                catch { }
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
                string sanitizedAsset = ShihabProcessAndPathSafety.SanitizeFileName(Path.GetFileName(asset.Name), "");
                string fromSums;
                if (!string.IsNullOrEmpty(sanitizedAsset) &&
                    parsedSums.TryGetValue(sanitizedAsset, out fromSums) &&
                    !string.IsNullOrEmpty(fromSums))
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

            string currentVer = GetCurrentAppVersion();
            if (!string.IsNullOrWhiteSpace(checkResult.LatestVersion) &&
                CompareSemVer(checkResult.LatestVersion, currentVer) < 0)
            {
                errorMessage = "Security check refused update: downgrade below current version " + currentVer + " is blocked.";
                return false;
            }

            try
            {
                string updatesDir = Path.Combine(dataDirectory, "updates");
                Directory.CreateDirectory(updatesDir);
                bool hasOverride = cfg != null && !string.IsNullOrWhiteSpace(cfg.ShihabUpdateApiOverrideUrl);

                Dictionary<string, string> sumsMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (checkResult.Sha256SumsAsset != null && !string.IsNullOrWhiteSpace(checkResult.Sha256SumsAsset.BrowserDownloadUrl))
                {
                    if (progressCallback != null) progressCallback(0, 100, "Fetching SHA256SUMS.txt cryptographic manifest...");
                    using (HttpWebResponse sumsResp = GetUpdateResponseWithSafeRedirects(checkResult.Sha256SumsAsset.BrowserDownloadUrl, hasOverride, cfg, cancelToken))
                    {
                        byte[] sumBytes = ReadBoundedResponseBytes(sumsResp, MaxShaSumsBytes, cancelToken);
                        sumsMap = ParseSha256SumsText(Encoding.UTF8.GetString(sumBytes));
                    }
                }

                string expectedPrimaryHash = ResolveExpectedSha256ForAsset(checkResult.PrimaryAsset, sumsMap);
                if (string.IsNullOrEmpty(expectedPrimaryHash))
                {
                    errorMessage = "Security check refused update: missing SHA-256 digest in SHA256SUMS.txt or release metadata for " + checkResult.PrimaryAsset.Name;
                    return false;
                }
                checkResult.ExpectedPrimarySha256 = expectedPrimaryHash;

                string safeAssetName = ShihabProcessAndPathSafety.SanitizeFileName(checkResult.PrimaryAsset.Name, "update.exe");
                string tempPart = Path.Combine(updatesDir, safeAssetName + ".download");
                string finalVerified = Path.Combine(updatesDir, safeAssetName);

                if (!DownloadStreamWithSha256Check(
                        checkResult.PrimaryAsset.BrowserDownloadUrl,
                        hasOverride,
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
                        checkResult.ExpectedExtensionSha256 = expectedExtHash;
                        string extName = ShihabProcessAndPathSafety.SanitizeFileName(checkResult.ExtensionZipAsset.Name, "extension.zip");
                        string extPart = Path.Combine(updatesDir, extName + ".download");
                        string extFinal = Path.Combine(updatesDir, extName);
                        string extErr;
                        if (DownloadStreamWithSha256Check(
                                checkResult.ExtensionZipAsset.BrowserDownloadUrl,
                                hasOverride,
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
            return DownloadStreamWithSha256Check(
                downloadUrl,
                false,
                tempDownloadPath,
                finalVerifiedPath,
                expectedSha256Hex,
                knownSizeBytes,
                cfg,
                progressCallback,
                cancelToken,
                out errorMessage);
        }

        private static bool DownloadStreamWithSha256Check(
            string downloadUrl,
            bool allowCustomOverrideHost,
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
            if (string.IsNullOrWhiteSpace(expectedSha256Hex) || expectedSha256Hex.Trim().Length != 64)
            {
                errorMessage = "Security check refused update: expected SHA-256 hash is required.";
                return false;
            }

            try
            {
                if (File.Exists(tempDownloadPath)) File.Delete(tempDownloadPath);
                using (HttpWebResponse resp = GetUpdateResponseWithSafeRedirects(downloadUrl, allowCustomOverrideHost, cfg, cancelToken))
                using (Stream net = resp.GetResponseStream())
                using (SHA256 sha = SHA256.Create())
                {
                    if (net == null)
                    {
                        errorMessage = "Empty HTTP response stream.";
                        return false;
                    }
                    long total = resp.ContentLength > 0 ? resp.ContentLength : knownSizeBytes;
                    if (total > MaxReleaseAssetBytes)
                    {
                        errorMessage = "Release asset exceeds maximum allowed 256 MiB package limit.";
                        return false;
                    }

                    long got = 0;
                    byte[] buf = new byte[65536];
                    using (FileStream fs = new FileStream(tempDownloadPath, FileMode.Create, FileAccess.Write, FileShare.None, 65536))
                    {
                        int r;
                        while ((r = net.Read(buf, 0, buf.Length)) > 0)
                        {
                            if (cancelToken.IsCancellationRequested)
                            {
                                throw new OperationCanceledException("Update download canceled.");
                            }
                            got += r;
                            if (got > MaxReleaseAssetBytes)
                            {
                                throw new InvalidDataException("Update download exceeded maximum permitted 256 MiB size.");
                            }
                            fs.Write(buf, 0, r);
                            sha.TransformBlock(buf, 0, r, null, 0);
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

                    if (knownSizeBytes > 0 && got != knownSizeBytes)
                    {
                        try { File.Delete(tempDownloadPath); } catch { }
                        errorMessage = string.Format("Downloaded size mismatch: expected {0} bytes, got {1} bytes.", knownSizeBytes, got);
                        return false;
                    }

                    string actualHex = BytesToLowerHex(sha.Hash);
                    if (!ShihabProcessAndPathSafety.FixedTimeEqualsAscii(actualHex, expectedSha256Hex.Trim().ToLowerInvariant()))
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

        private static string ComputeFileSha256Hex(string filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536))
            using (SHA256 sha = SHA256.Create())
            {
                return BytesToLowerHex(sha.ComputeHash(fs));
            }
        }

        private static string BytesToLowerHex(byte[] bytes)
        {
            if (bytes == null) return "";
            StringBuilder sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2"));
            return sb.ToString();
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

                if (checkResult != null && !string.IsNullOrWhiteSpace(checkResult.ExpectedPrimarySha256))
                {
                    string actualSha = ComputeFileSha256Hex(verifiedPrimaryPath);
                    if (!ShihabProcessAndPathSafety.FixedTimeEqualsAscii(actualSha, checkResult.ExpectedPrimarySha256.Trim().ToLowerInvariant()))
                    {
                        errorMessage = "Pre-launch SHA-256 integrity re-check failed for staged update binary.";
                        return false;
                    }
                }

                string currentExe = Process.GetCurrentProcess().MainModule.FileName;
                string currentDir = Path.GetFullPath(Path.GetDirectoryName(currentExe)).TrimEnd('\\');
                string currentVer = GetCurrentAppVersion();
                int currentPid = Process.GetCurrentProcess().Id;

                bool isSetupAsset = Path.GetFileName(verifiedPrimaryPath).IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0;
                if ((checkResult != null && checkResult.InstallMode == ShihabAppInstallMode.InstalledSetup) || isSetupAsset)
                {
                    string args = ShihabProcessAndPathSafety.BuildWindowsArgumentString(
                        "--shihab-silent",
                        "--shihab-update-mode",
                        "--shihab-wait-pid",
                        currentPid.ToString(CultureInfo.InvariantCulture),
                        "--shihab-relaunch",
                        "--shihab-updated-from",
                        currentVer,
                        "--shihab-install-dir",
                        currentDir);

                    ProcessStartInfo psi = new ProcessStartInfo(verifiedPrimaryPath, args);
                    psi.WorkingDirectory = Path.GetDirectoryName(verifiedPrimaryPath);
                    psi.UseShellExecute = false;
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
                        bool extHashOk = true;
                        if (checkResult != null && !string.IsNullOrWhiteSpace(checkResult.ExpectedExtensionSha256))
                        {
                            string extSha = ComputeFileSha256Hex(verifiedExtZipPath);
                            extHashOk = ShihabProcessAndPathSafety.FixedTimeEqualsAscii(
                                extSha,
                                checkResult.ExpectedExtensionSha256.Trim().ToLowerInvariant());
                        }
                        if (extHashOk)
                        {
                            string siblingExtDir = Path.Combine(currentDir, "extension");
                            if (Directory.Exists(siblingExtDir))
                            {
                                SafeUpdateSiblingExtensionFolder(verifiedExtZipPath, siblingExtDir);
                            }
                        }
                    }

                    if (cleanExitCallback != null) cleanExitCallback();

                    ProcessStartInfo relaunchPsi = new ProcessStartInfo(
                        currentExe,
                        ShihabProcessAndPathSafety.BuildWindowsArgumentString("--shihab-updated-from", currentVer));
                    relaunchPsi.WorkingDirectory = currentDir;
                    relaunchPsi.UseShellExecute = false;
                    Process.Start(relaunchPsi);
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
                string normRoot = ShihabProcessAndPathSafety.NormalizeFullDirectoryPath(targetExtDir);
                if (string.IsNullOrEmpty(normRoot)) return;
                const long maxBytes = 64L * 1024L * 1024L;
                long totalWritten = 0;
                long entryCount = 0;
                byte[] buf = new byte[65536];

                using (ZipArchive zip = ZipFile.OpenRead(extZipPath))
                {
                    foreach (ZipArchiveEntry e in zip.Entries)
                    {
                        entryCount++;
                        if (entryCount > 512) throw new InvalidDataException("Extension archive has too many entries.");
                        if (ShihabProcessAndPathSafety.IsZipEntrySymlinkOrReparse(e))
                        {
                            throw new InvalidDataException("Symlink rejected in extension archive.");
                        }

                        string raw = (e.FullName ?? "").Replace('\\', '/');
                        bool isDir = raw.EndsWith("/", StringComparison.Ordinal);
                        string trimmed = raw.TrimEnd('/');
                        if (trimmed.StartsWith("extension/", StringComparison.OrdinalIgnoreCase))
                        {
                            trimmed = trimmed.Substring("extension/".Length);
                        }
                        if (string.IsNullOrWhiteSpace(trimmed)) continue;

                        string dest;
                        if (!ShihabProcessAndPathSafety.ResolvePathStrictlyUnderRoot(normRoot, trimmed, out dest))
                        {
                            throw new InvalidDataException("Unsafe path rejected in extension archive: " + raw);
                        }
                        if (ShihabProcessAndPathSafety.HasReparsePointUnderRoot(normRoot, dest))
                        {
                            throw new InvalidDataException("Reparse point detected in extension target: " + raw);
                        }

                        if (isDir)
                        {
                            Directory.CreateDirectory(dest);
                            continue;
                        }

                        string parent = Path.GetDirectoryName(dest);
                        if (!string.IsNullOrEmpty(parent)) Directory.CreateDirectory(parent);
                        using (Stream sIn = e.Open())
                        using (FileStream sOut = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.Read, 65536))
                        {
                            int r;
                            while ((r = sIn.Read(buf, 0, buf.Length)) > 0)
                            {
                                totalWritten += r;
                                if (totalWritten > maxBytes)
                                {
                                    throw new InvalidDataException("Extension archive exceeds 64 MiB limit.");
                                }
                                sOut.Write(buf, 0, r);
                            }
                        }
                    }
                }
            }
            catch { }
        }
    }
}

