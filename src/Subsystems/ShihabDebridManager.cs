using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public static class ShihabDebridManager
    {
        private const int MaxDebridResponseBytes = 4 * 1024 * 1024;
        private static readonly TimeSpan DebridRegexTimeout = TimeSpan.FromMilliseconds(250);

        private static readonly Regex DomainTokenRegex = new Regex(
            "\"([a-z0-9\\-]+\\.[a-z0-9\\.\\-]+)\"",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex DocsPathRegex = new Regex(
            @"/(document|spreadsheets|presentation)/d/([a-zA-Z0-9_\-]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex DrivePathIdRegex = new Regex(
            @"/file/d/([a-zA-Z0-9_\-]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex DriveFolderPathRegex = new Regex(
            @"/drive/(?:u/\d+/)?folders/([a-zA-Z0-9_\-]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex DriveQueryIdRegex = new Regex(
            @"[?&](?:id|docid)=([a-zA-Z0-9_\-]+)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex GdriveFormRegex = new Regex(
            @"<form[^>]*id=[""']download-form[""'][^>]*action=[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex GdriveHiddenInputRegex = new Regex(
            @"<input[^>]*type=[""']hidden[""'][^>]*>",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex GdriveAttrNameRegex = new Regex(
            @"name=[""']([^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex GdriveAttrValueRegex = new Regex(
            @"value=[""']([^""']*)[""']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex GdriveConfirmHrefRegex = new Regex(
            @"href=[""']([^""']*confirm=[^""']+)[""']",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
            DebridRegexTimeout);
        private static readonly Regex RdLinksRegex = new Regex(
            "\"links\"\\s*:\\s*\\[\\s*\"([^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"",
            RegexOptions.CultureInvariant,
            DebridRegexTimeout);

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

        private static string ReadBoundedResponseUtf8(HttpWebResponse resp)
        {
            if (resp == null) return "";
            using (Stream s = resp.GetResponseStream())
            {
                if (s == null) return "";
                byte[] buf = new byte[16384];
                using (MemoryStream ms = new MemoryStream())
                {
                    int total = 0;
                    int read;
                    while (total < MaxDebridResponseBytes && (read = s.Read(buf, 0, Math.Min(buf.Length, MaxDebridResponseBytes - total))) > 0)
                    {
                        ms.Write(buf, 0, read);
                        total += read;
                    }
                    return Encoding.UTF8.GetString(ms.ToArray());
                }
            }
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
                bool hasRealDebridEnabled = false;
                for (int i = 0; i < cfg.ShihabDebridAccounts.Count; i++)
                {
                    if (cfg.ShihabDebridAccounts[i] != null &&
                        cfg.ShihabDebridAccounts[i].MehediEnabled &&
                        string.Equals(cfg.ShihabDebridAccounts[i].ShihabProvider, "Real-Debrid", StringComparison.OrdinalIgnoreCase))
                    {
                        hasRealDebridEnabled = true;
                        break;
                    }
                }
                if (!hasRealDebridEnabled) return;

                ShihabAutoUpdater.EnforceModernTls();
                HttpWebRequest req = (HttpWebRequest)WebRequest.Create("https://api.real-debrid.com/rest/1.0/hosts/domains");
                req.Method = "GET";
                req.AllowAutoRedirect = false;
                req.Timeout = 5000;
                req.ReadWriteTimeout = 5000;
                ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, req.RequestUri.AbsoluteUri);
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    string json = ReadBoundedResponseUtf8(resp);
                    MatchCollection matches = DomainTokenRegex.Matches(json);
                    lock (_dynamicHostCacheLock)
                    {
                        _dynamicDebridDomains.Clear();
                        for (int i = 0; i < matches.Count && i < 4096; i++)
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
                Match docM = DocsPathRegex.Match(u.AbsolutePath);
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
                Match mPath = DrivePathIdRegex.Match(u.AbsolutePath);
                if (mPath.Success) fileId = mPath.Groups[1].Value;
                if (string.IsNullOrEmpty(fileId))
                {
                    Match mQuery = DriveQueryIdRegex.Match(u.Query);
                    if (mQuery.Success) fileId = mQuery.Groups[1].Value;
                }
                if (string.IsNullOrEmpty(fileId) && !string.IsNullOrEmpty(u.Fragment))
                {
                    Match mFrag = DrivePathIdRegex.Match(u.Fragment);
                    if (mFrag.Success) fileId = mFrag.Groups[1].Value;
                    if (string.IsNullOrEmpty(fileId))
                    {
                        Match mFragQ = DriveQueryIdRegex.Match(u.Fragment);
                        if (mFragQ.Success) fileId = mFragQ.Groups[1].Value;
                    }
                }
                if (!string.IsNullOrEmpty(fileId))
                {
                    directUrl = "https://drive.usercontent.google.com/download?id=" + Uri.EscapeDataString(fileId) + "&export=download&confirm=t";
                    return true;
                }
                // Shared folder preview URLs carrying an embedded file segment (e.g. /drive/folders/<folderId>?... or /folders/<folderId>/file/<fileId>)
                Match mFolder = DriveFolderPathRegex.Match(u.AbsolutePath);
                if (mFolder.Success && u.AbsolutePath.IndexOf("/file/", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    int idx = u.AbsolutePath.IndexOf("/file/", StringComparison.OrdinalIgnoreCase) + 6;
                    string tail = u.AbsolutePath.Substring(idx).Trim('/');
                    int slash = tail.IndexOf('/');
                    string candidateId = slash > 0 ? tail.Substring(0, slash) : tail;
                    if (candidateId.Length >= 10)
                    {
                        directUrl = "https://drive.usercontent.google.com/download?id=" + Uri.EscapeDataString(candidateId) + "&export=download&confirm=t";
                        return true;
                    }
                }
            }
            return false;
        }

        private static bool IsSafeResolvedExternalUrl(Uri baseUri, string rawUrl, out string normalizedUrl)
        {
            normalizedUrl = "";
            Uri resolved;
            if (baseUri != null)
            {
                if (!Uri.TryCreate(baseUri, rawUrl, out resolved)) return false;
            }
            else
            {
                if (!Uri.TryCreate(rawUrl, UriKind.Absolute, out resolved)) return false;
            }
            Uri validated;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(resolved.AbsoluteUri, false, out validated))
            {
                return false;
            }
            if (ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(validated.Host, true))
            {
                return false;
            }
            normalizedUrl = validated.AbsoluteUri;
            return true;
        }

        private static bool IsAllowedGoogleDriveDownloadUri(Uri baseUri, string rawUrl, out string normalizedUrl)
        {
            normalizedUrl = "";
            string safeUrl;
            if (!IsSafeResolvedExternalUrl(baseUri, rawUrl, out safeUrl))
            {
                return false;
            }
            Uri parsed;
            if (!Uri.TryCreate(safeUrl, UriKind.Absolute, out parsed))
            {
                return false;
            }
            if (!string.Equals(parsed.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string host = (parsed.Host ?? "").ToLowerInvariant();
            if (host != "drive.google.com" &&
                host != "drive.usercontent.google.com" &&
                host != "docs.google.com")
            {
                return false;
            }
            normalizedUrl = parsed.AbsoluteUri;
            return true;
        }

        public static bool TryParseGoogleDriveConfirmationHtml(string htmlBody, Uri baseUri, out string confirmedDownloadUrl)
        {
            confirmedDownloadUrl = "";
            if (string.IsNullOrEmpty(htmlBody)) return false;
            if (htmlBody.Length > 2 * 1024 * 1024)
            {
                htmlBody = htmlBody.Substring(0, 2 * 1024 * 1024);
            }
            if (htmlBody.IndexOf("download-form", StringComparison.OrdinalIgnoreCase) < 0 &&
                htmlBody.IndexOf("uc-download-link", StringComparison.OrdinalIgnoreCase) < 0 &&
                htmlBody.IndexOf("confirm=", StringComparison.OrdinalIgnoreCase) < 0)
            {
                return false;
            }

            try
            {
                Match formM = GdriveFormRegex.Match(htmlBody);
                if (formM.Success)
                {
                    string action = WebUtility.HtmlDecode(formM.Groups[1].Value);
                    List<string> qParams = new List<string>();
                    MatchCollection inputs = GdriveHiddenInputRegex.Matches(htmlBody);
                    for (int i = 0; i < inputs.Count && i < 64; i++)
                    {
                        string tag = inputs[i].Value;
                        Match nm = GdriveAttrNameRegex.Match(tag);
                        Match vl = GdriveAttrValueRegex.Match(tag);
                        if (nm.Success)
                        {
                            string val = vl.Success ? WebUtility.HtmlDecode(vl.Groups[1].Value) : "";
                            qParams.Add(Uri.EscapeDataString(nm.Groups[1].Value) + "=" + Uri.EscapeDataString(val));
                        }
                    }
                    if (qParams.Count > 0)
                    {
                        string cand = action + (action.IndexOf('?') >= 0 ? "&" : "?") + string.Join("&", qParams.ToArray());
                        string safeUrl;
                        if (IsAllowedGoogleDriveDownloadUri(baseUri, cand, out safeUrl))
                        {
                            confirmedDownloadUrl = safeUrl;
                            return true;
                        }
                    }
                }

                Match hrefM = GdriveConfirmHrefRegex.Match(htmlBody);
                if (hrefM.Success)
                {
                    string rawHref = WebUtility.HtmlDecode(hrefM.Groups[1].Value);
                    string safeUrl;
                    if (IsAllowedGoogleDriveDownloadUri(baseUri, rawHref, out safeUrl))
                    {
                        confirmedDownloadUrl = safeUrl;
                        return true;
                    }
                }
            }
            catch { }
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
            return TryUnrestrictUrl(originalUrl, linkPassword, cfg, CancellationToken.None, out unrestrictedUrl, out unlockedFileName, out providerUsed, out maxChunks);
        }

        public static bool TryUnrestrictUrl(
            string originalUrl,
            string linkPassword,
            MehediAppConfiguration cfg,
            CancellationToken cancelToken,
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
                if (cancelToken.IsCancellationRequested) return false;
                ShihabDebridAccount acct = cfg.ShihabDebridAccounts[i];
                if (acct == null || !acct.MehediEnabled) continue;
                string apiKey = ShihabSecretsVault.UnprotectSecret(acct.MehediEncryptedApiToken);
                if (string.IsNullOrWhiteSpace(apiKey)) continue;

                try
                {
                    string candUrl;
                    string candName;
                    int candChunks;
                    if (string.Equals(acct.ShihabProvider, "Real-Debrid", StringComparison.OrdinalIgnoreCase))
                    {
                        if (UnrestrictWithRealDebrid(originalUrl, linkPassword, apiKey, cfg, out candUrl, out candName, out candChunks) &&
                            IsSafeResolvedExternalUrl(null, candUrl, out unrestrictedUrl))
                        {
                            unlockedFileName = candName;
                            maxChunks = candChunks;
                            providerUsed = "Real-Debrid";
                            return true;
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "AllDebrid", StringComparison.OrdinalIgnoreCase))
                    {
                        if (UnrestrictWithAllDebrid(originalUrl, linkPassword, apiKey, cfg, cancelToken, out candUrl, out candName, out candChunks) &&
                            IsSafeResolvedExternalUrl(null, candUrl, out unrestrictedUrl))
                        {
                            unlockedFileName = candName;
                            maxChunks = candChunks;
                            providerUsed = "AllDebrid";
                            return true;
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "Premiumize", StringComparison.OrdinalIgnoreCase))
                    {
                        if (UnrestrictWithPremiumize(originalUrl, apiKey, cfg, out candUrl, out candName) &&
                            IsSafeResolvedExternalUrl(null, candUrl, out unrestrictedUrl))
                        {
                            unlockedFileName = candName;
                            providerUsed = "Premiumize";
                            return true;
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "TorBox", StringComparison.OrdinalIgnoreCase))
                    {
                        if (UnrestrictWithTorBox(originalUrl, linkPassword, apiKey, cfg, out candUrl, out candName) &&
                            IsSafeResolvedExternalUrl(null, candUrl, out unrestrictedUrl))
                        {
                            unlockedFileName = candName;
                            providerUsed = "TorBox";
                            return true;
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "Mega-Debrid", StringComparison.OrdinalIgnoreCase))
                    {
                        if (UnrestrictWithMegaDebrid(originalUrl, linkPassword, apiKey, cfg, out candUrl, out candName) &&
                            IsSafeResolvedExternalUrl(null, candUrl, out unrestrictedUrl))
                        {
                            unlockedFileName = candName;
                            providerUsed = "Mega-Debrid";
                            return true;
                        }
                    }
                }
                catch
                {
                }
            }
            unrestrictedUrl = originalUrl;
            return false;
        }

        public static bool TryUnlockTorrentOrMagnetWithDebrid(
            string magnetOrTorrent,
            MehediAppConfiguration cfg,
            out string unlockedDirectUrl,
            out string unlockedFileName,
            out string providerUsed)
        {
            return TryUnlockTorrentOrMagnetWithDebrid(magnetOrTorrent, cfg, CancellationToken.None, out unlockedDirectUrl, out unlockedFileName, out providerUsed);
        }

        public static bool TryUnlockTorrentOrMagnetWithDebrid(
            string magnetOrTorrent,
            MehediAppConfiguration cfg,
            CancellationToken cancelToken,
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
                if (cancelToken.IsCancellationRequested) return false;
                ShihabDebridAccount acct = cfg.ShihabDebridAccounts[i];
                if (acct == null || !acct.MehediEnabled) continue;
                string token = ShihabSecretsVault.UnprotectSecret(acct.MehediEncryptedApiToken);
                if (string.IsNullOrWhiteSpace(token)) continue;

                try
                {
                    ShihabAutoUpdater.EnforceModernTls();
                    if (string.Equals(acct.ShihabProvider, "Real-Debrid", StringComparison.OrdinalIgnoreCase))
                    {
                        HttpWebRequest addReq = (HttpWebRequest)WebRequest.Create("https://api.real-debrid.com/rest/1.0/torrents/addMagnet");
                        addReq.Method = "POST";
                        addReq.AllowAutoRedirect = false;
                        addReq.Headers["Authorization"] = "Bearer " + token.Trim();
                        addReq.ContentType = "application/x-www-form-urlencoded";
                        addReq.Timeout = 10000;
                        addReq.ReadWriteTimeout = 10000;
                        ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(addReq, cfg, addReq.RequestUri.AbsoluteUri);
                        byte[] addBody = Encoding.UTF8.GetBytes("magnet=" + Uri.EscapeDataString(magnetOrTorrent));
                        addReq.ContentLength = addBody.Length;
                        using (Stream s = addReq.GetRequestStream()) s.Write(addBody, 0, addBody.Length);
                        string torrentId = "";
                        using (HttpWebResponse r1 = (HttpWebResponse)addReq.GetResponse())
                        {
                            torrentId = ExtractJsonStringField(ReadBoundedResponseUtf8(r1), "id");
                        }
                        if (!string.IsNullOrEmpty(torrentId))
                        {
                            HttpWebRequest selReq = (HttpWebRequest)WebRequest.Create("https://api.real-debrid.com/rest/1.0/torrents/selectFiles/" + Uri.EscapeDataString(torrentId));
                            selReq.Method = "POST";
                            selReq.AllowAutoRedirect = false;
                            selReq.Headers["Authorization"] = "Bearer " + token.Trim();
                            selReq.ContentType = "application/x-www-form-urlencoded";
                            selReq.Timeout = 10000;
                            selReq.ReadWriteTimeout = 10000;
                            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(selReq, cfg, selReq.RequestUri.AbsoluteUri);
                            byte[] selBody = Encoding.UTF8.GetBytes("files=all");
                            selReq.ContentLength = selBody.Length;
                            using (Stream s2 = selReq.GetRequestStream()) s2.Write(selBody, 0, selBody.Length);
                            using (HttpWebResponse r2 = (HttpWebResponse)selReq.GetResponse()) { }

                            for (int rdPoll = 0; rdPoll < 5; rdPoll++)
                            {
                                if (rdPoll > 0 && cancelToken.WaitHandle.WaitOne(1500)) return false;
                                HttpWebRequest infoReq = (HttpWebRequest)WebRequest.Create("https://api.real-debrid.com/rest/1.0/torrents/info/" + Uri.EscapeDataString(torrentId));
                                infoReq.AllowAutoRedirect = false;
                                infoReq.Headers["Authorization"] = "Bearer " + token.Trim();
                                infoReq.Timeout = 10000;
                                infoReq.ReadWriteTimeout = 10000;
                                ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(infoReq, cfg, infoReq.RequestUri.AbsoluteUri);
                                using (HttpWebResponse r3 = (HttpWebResponse)infoReq.GetResponse())
                                {
                                    string infoJson = ReadBoundedResponseUtf8(r3);
                                    Match linkM = RdLinksRegex.Match(infoJson);
                                    if (linkM.Success)
                                    {
                                        string hostLink = UnescapeJsonStringFragment(linkM.Groups[1].Value);
                                        int chunks;
                                        string candUrl;
                                        string candName;
                                        if (UnrestrictWithRealDebrid(hostLink, "", token, cfg, out candUrl, out candName, out chunks) &&
                                            IsSafeResolvedExternalUrl(null, candUrl, out unlockedDirectUrl))
                                        {
                                            unlockedFileName = candName;
                                            providerUsed = "Real-Debrid Cloud";
                                            return true;
                                        }
                                    }
                                    string statusStr = ExtractJsonStringField(infoJson, "status");
                                    if (string.Equals(statusStr, "error", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(statusStr, "dead", StringComparison.OrdinalIgnoreCase) ||
                                        string.Equals(statusStr, "magnet_error", StringComparison.OrdinalIgnoreCase))
                                    {
                                        break;
                                    }
                                }
                            }
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "AllDebrid", StringComparison.OrdinalIgnoreCase))
                    {
                        string candUrl;
                        string candName;
                        if (UnlockMagnetWithAllDebrid(magnetOrTorrent, token, cfg, cancelToken, out candUrl, out candName) &&
                            IsSafeResolvedExternalUrl(null, candUrl, out unlockedDirectUrl))
                        {
                            unlockedFileName = candName;
                            providerUsed = "AllDebrid Cloud";
                            return true;
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "TorBox", StringComparison.OrdinalIgnoreCase))
                    {
                        string candUrl;
                        string candName;
                        if (UnlockMagnetWithTorBox(magnetOrTorrent, token, cfg, cancelToken, out candUrl, out candName) &&
                            IsSafeResolvedExternalUrl(null, candUrl, out unlockedDirectUrl))
                        {
                            unlockedFileName = candName;
                            providerUsed = "TorBox Cloud";
                            return true;
                        }
                    }
                    else if (string.Equals(acct.ShihabProvider, "Premiumize", StringComparison.OrdinalIgnoreCase))
                    {
                        string candUrl;
                        string candName;
                        if (UnrestrictWithPremiumize(magnetOrTorrent, token, cfg, out candUrl, out candName) &&
                            IsSafeResolvedExternalUrl(null, candUrl, out unlockedDirectUrl))
                        {
                            unlockedFileName = candName;
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
            ShihabAutoUpdater.EnforceModernTls();
            string endpoint = "https://api.real-debrid.com/rest/1.0/unrestrict/link";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
            req.Method = "POST";
            req.AllowAutoRedirect = false;
            req.Headers["Authorization"] = "Bearer " + token.Trim();
            req.ContentType = "application/x-www-form-urlencoded";
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, endpoint);
            string form = "link=" + Uri.EscapeDataString(link);
            if (!string.IsNullOrEmpty(password)) form += "&password=" + Uri.EscapeDataString(password);
            byte[] body = Encoding.UTF8.GetBytes(form);
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                string json = ReadBoundedResponseUtf8(resp);
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
            CancellationToken cancelToken,
            out string directUrl,
            out string fileName,
            out int maxChunks)
        {
            directUrl = "";
            fileName = "";
            maxChunks = 0;
            ShihabAutoUpdater.EnforceModernTls();
            string url = "https://api.alldebrid.com/v4/link/unlock?agent=MHS-Download-Manager&link=" + Uri.EscapeDataString(link);
            if (!string.IsNullOrEmpty(password)) url += "&password=" + Uri.EscapeDataString(password);
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
            req.Method = "GET";
            req.AllowAutoRedirect = false;
            req.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, url);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                string json = ReadBoundedResponseUtf8(resp);
                directUrl = ExtractJsonStringField(json, "link");
                fileName = ExtractJsonStringField(json, "filename");
                maxChunks = (int)ExtractJsonLongField(json, "max_chunks");
                if (!string.IsNullOrWhiteSpace(directUrl)) return true;

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
                        if (cancelToken.WaitHandle.WaitOne(1500)) return false;
                        string pollUrl = "https://api.alldebrid.com/v4/link/delayed?agent=MHS-Download-Manager&id=" + Uri.EscapeDataString(delayedId);
                        HttpWebRequest pReq = (HttpWebRequest)WebRequest.Create(pollUrl);
                        pReq.AllowAutoRedirect = false;
                        pReq.Headers["Authorization"] = "Bearer " + apiKey.Trim();
                        pReq.Timeout = 10000;
                        pReq.ReadWriteTimeout = 10000;
                        ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(pReq, cfg, pollUrl);
                        using (HttpWebResponse pResp = (HttpWebResponse)pReq.GetResponse())
                        {
                            string pJson = ReadBoundedResponseUtf8(pResp);
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
            ShihabAutoUpdater.EnforceModernTls();
            string endpoint = "https://www.premiumize.me/api/transfer/directdl";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
            req.Method = "POST";
            req.AllowAutoRedirect = false;
            req.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            req.ContentType = "application/x-www-form-urlencoded";
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, endpoint);
            string post = "src=" + Uri.EscapeDataString(link);
            byte[] body = Encoding.UTF8.GetBytes(post);
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                string json = ReadBoundedResponseUtf8(resp);
                directUrl = ExtractJsonStringField(json, "location");
                fileName = ExtractJsonStringField(json, "filename");
                return !string.IsNullOrWhiteSpace(directUrl);
            }
        }

        private static bool UnrestrictWithTorBox(
            string link,
            string password,
            string apiKey,
            MehediAppConfiguration cfg,
            out string directUrl,
            out string fileName)
        {
            directUrl = "";
            fileName = "";
            ShihabAutoUpdater.EnforceModernTls();
            string createEp = "https://api.torbox.app/v1/api/webdl/createwebdownload";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(createEp);
            req.Method = "POST";
            req.AllowAutoRedirect = false;
            req.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            req.ContentType = "application/x-www-form-urlencoded";
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, createEp);
            string form = "link=" + Uri.EscapeDataString(link);
            if (!string.IsNullOrEmpty(password)) form += "&password=" + Uri.EscapeDataString(password);
            byte[] body = Encoding.UTF8.GetBytes(form);
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            long webId = 0;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                string json = ReadBoundedResponseUtf8(resp);
                webId = ExtractJsonLongField(json, "webdownload_id");
                if (webId <= 0) webId = ExtractJsonLongField(json, "id");
                fileName = ExtractJsonStringField(json, "name");
            }
            if (webId <= 0) return false;

            string dlEp = "https://api.torbox.app/v1/api/webdl/requestdl?web_id=" +
                webId.ToString(CultureInfo.InvariantCulture) + "&zip_link=false";
            HttpWebRequest dlReq = (HttpWebRequest)WebRequest.Create(dlEp);
            dlReq.Method = "GET";
            dlReq.AllowAutoRedirect = false;
            dlReq.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            dlReq.Timeout = 12000;
            dlReq.ReadWriteTimeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(dlReq, cfg, dlEp);
            using (HttpWebResponse dlResp = (HttpWebResponse)dlReq.GetResponse())
            {
                string dlJson = ReadBoundedResponseUtf8(dlResp);
                directUrl = ExtractJsonStringField(dlJson, "data");
                if (string.IsNullOrWhiteSpace(directUrl)) directUrl = ExtractJsonStringField(dlJson, "link");
                return !string.IsNullOrWhiteSpace(directUrl);
            }
        }

        private static bool UnlockMagnetWithTorBox(
            string magnet,
            string apiKey,
            MehediAppConfiguration cfg,
            CancellationToken cancelToken,
            out string directUrl,
            out string fileName)
        {
            directUrl = "";
            fileName = "";
            ShihabAutoUpdater.EnforceModernTls();
            string createEp = "https://api.torbox.app/v1/api/torrents/createtorrent";
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(createEp);
            req.Method = "POST";
            req.AllowAutoRedirect = false;
            req.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            req.ContentType = "application/x-www-form-urlencoded";
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, createEp);
            byte[] body = Encoding.UTF8.GetBytes("magnet=" + Uri.EscapeDataString(magnet));
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            long torId = 0;
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                string json = ReadBoundedResponseUtf8(resp);
                torId = ExtractJsonLongField(json, "torrent_id");
                if (torId <= 0) torId = ExtractJsonLongField(json, "id");
                fileName = ExtractJsonStringField(json, "name");
            }
            if (torId <= 0) return false;

            for (int poll = 0; poll < 5; poll++)
            {
                if (poll > 0 && cancelToken.WaitHandle.WaitOne(1500)) return false;
                string dlEp = "https://api.torbox.app/v1/api/torrents/requestdl?torrent_id=" +
                    torId.ToString(CultureInfo.InvariantCulture) + "&zip_link=true";
                HttpWebRequest dlReq = (HttpWebRequest)WebRequest.Create(dlEp);
                dlReq.Method = "GET";
                dlReq.AllowAutoRedirect = false;
                dlReq.Headers["Authorization"] = "Bearer " + apiKey.Trim();
                dlReq.Timeout = 10000;
                dlReq.ReadWriteTimeout = 10000;
                ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(dlReq, cfg, dlEp);
                try
                {
                    using (HttpWebResponse dlResp = (HttpWebResponse)dlReq.GetResponse())
                    {
                        string dlJson = ReadBoundedResponseUtf8(dlResp);
                        directUrl = ExtractJsonStringField(dlJson, "data");
                        if (!string.IsNullOrWhiteSpace(directUrl)) return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private static bool UnlockMagnetWithAllDebrid(
            string magnet,
            string apiKey,
            MehediAppConfiguration cfg,
            CancellationToken cancelToken,
            out string directUrl,
            out string fileName)
        {
            directUrl = "";
            fileName = "";
            ShihabAutoUpdater.EnforceModernTls();
            string upEp = "https://api.alldebrid.com/v4/magnet/upload?agent=MHS-Download-Manager";
            HttpWebRequest upReq = (HttpWebRequest)WebRequest.Create(upEp);
            upReq.Method = "POST";
            upReq.AllowAutoRedirect = false;
            upReq.Headers["Authorization"] = "Bearer " + apiKey.Trim();
            upReq.ContentType = "application/x-www-form-urlencoded";
            upReq.Timeout = 12000;
            upReq.ReadWriteTimeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(upReq, cfg, upEp);
            byte[] body = Encoding.UTF8.GetBytes("magnets[]=" + Uri.EscapeDataString(magnet));
            upReq.ContentLength = body.Length;
            using (Stream s = upReq.GetRequestStream()) s.Write(body, 0, body.Length);
            long magId = 0;
            using (HttpWebResponse resp = (HttpWebResponse)upReq.GetResponse())
            {
                string json = ReadBoundedResponseUtf8(resp);
                magId = ExtractJsonLongField(json, "id");
                fileName = ExtractJsonStringField(json, "name");
            }
            if (magId <= 0) return false;

            for (int poll = 0; poll < 5; poll++)
            {
                if (poll > 0 && cancelToken.WaitHandle.WaitOne(1500)) return false;
                string stUrl = "https://api.alldebrid.com/v4/magnet/status?agent=MHS-Download-Manager&id=" + magId.ToString(CultureInfo.InvariantCulture);
                HttpWebRequest stReq = (HttpWebRequest)WebRequest.Create(stUrl);
                stReq.AllowAutoRedirect = false;
                stReq.Headers["Authorization"] = "Bearer " + apiKey.Trim();
                stReq.Timeout = 10000;
                stReq.ReadWriteTimeout = 10000;
                ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(stReq, cfg, stUrl);
                using (HttpWebResponse stResp = (HttpWebResponse)stReq.GetResponse())
                {
                    string stJson = ReadBoundedResponseUtf8(stResp);
                    string hostLink = ExtractJsonStringField(stJson, "link");
                    if (!string.IsNullOrWhiteSpace(hostLink) && hostLink.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        int chunks;
                        string candUrl;
                        string candName;
                        if (UnrestrictWithAllDebrid(hostLink, "", apiKey, cfg, cancelToken, out candUrl, out candName, out chunks))
                        {
                            directUrl = candUrl;
                            if (!string.IsNullOrWhiteSpace(candName)) fileName = candName;
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        private static bool UnrestrictWithMegaDebrid(
            string link,
            string password,
            string token,
            MehediAppConfiguration cfg,
            out string directUrl,
            out string fileName)
        {
            directUrl = "";
            fileName = "";
            ShihabAutoUpdater.EnforceModernTls();
            string endpoint = "https://www.mega-debrid.eu/api.php?action=getLink&token=" + Uri.EscapeDataString(token.Trim());
            HttpWebRequest req = (HttpWebRequest)WebRequest.Create(endpoint);
            req.Method = "POST";
            req.AllowAutoRedirect = false;
            req.ContentType = "application/x-www-form-urlencoded";
            req.Timeout = 12000;
            req.ReadWriteTimeout = 12000;
            ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, cfg, endpoint);
            string form = "link=" + Uri.EscapeDataString(link);
            if (!string.IsNullOrEmpty(password)) form += "&password=" + Uri.EscapeDataString(password);
            byte[] body = Encoding.UTF8.GetBytes(form);
            req.ContentLength = body.Length;
            using (Stream s = req.GetRequestStream()) s.Write(body, 0, body.Length);
            using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
            {
                string json = ReadBoundedResponseUtf8(resp);
                directUrl = ExtractJsonStringField(json, "debridLink");
                if (string.IsNullOrWhiteSpace(directUrl)) directUrl = ExtractJsonStringField(json, "link");
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
                bool sendBearerHeader = true;
                if (string.Equals(acct.ShihabProvider, "AllDebrid", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://api.alldebrid.com/v4/user?agent=MHS-Download-Manager";
                }
                else if (string.Equals(acct.ShihabProvider, "Premiumize", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://www.premiumize.me/api/account/info";
                }
                else if (string.Equals(acct.ShihabProvider, "TorBox", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://api.torbox.app/v1/api/user/me";
                }
                else if (string.Equals(acct.ShihabProvider, "Mega-Debrid", StringComparison.OrdinalIgnoreCase))
                {
                    url = "https://www.mega-debrid.eu/api.php?action=getUserHistory&token=" + Uri.EscapeDataString(token.Trim());
                    sendBearerHeader = false;
                }

                HttpWebRequest req = (HttpWebRequest)WebRequest.Create(url);
                req.AllowAutoRedirect = false;
                if (sendBearerHeader)
                {
                    req.Headers["Authorization"] = "Bearer " + token.Trim();
                }
                req.Timeout = 8000;
                req.ReadWriteTimeout = 8000;
                MehediAppConfiguration activeCfg = ShihabLocalNetworkRelay.ActiveConfig;
                if (activeCfg != null)
                {
                    ShihabDnsAndNetworkManager.ApplyNetworkBindingAndProxy(req, activeCfg, url);
                }
                using (HttpWebResponse resp = (HttpWebResponse)req.GetResponse())
                {
                    string json = ReadBoundedResponseUtf8(resp);
                    if (json.IndexOf("\"status\":\"error\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        json.IndexOf("\"response_code\":\"error\"", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        json.IndexOf("\"success\":false", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string errMsg = ExtractJsonStringField(json, "message");
                        if (string.IsNullOrEmpty(errMsg)) errMsg = ExtractJsonStringField(json, "detail");
                        if (string.IsNullOrEmpty(errMsg)) errMsg = ExtractJsonStringField(json, "response_text");
                        acct.ShihabAccountStatus = "Error: " + (string.IsNullOrEmpty(errMsg) ? "Invalid token" : errMsg);
                        statusSummary = acct.ShihabAccountStatus;
                        return false;
                    }
                    string user = ExtractJsonStringField(json, "username");
                    if (string.IsNullOrEmpty(user)) user = ExtractJsonStringField(json, "email");
                    if (string.IsNullOrEmpty(user)) user = ExtractJsonStringField(json, "customer_id");
                    string type = ExtractJsonStringField(json, "type");
                    string exp = ExtractJsonStringField(json, "expiration");
                    if (string.IsNullOrEmpty(exp)) exp = ExtractJsonStringField(json, "premium_expires_at");
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

        private static string UnescapeJsonStringFragment(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return "";
            StringBuilder sb = new StringBuilder(raw.Length);
            for (int i = 0; i < raw.Length; i++)
            {
                char ch = raw[i];
                if (ch == '\\' && i + 1 < raw.Length)
                {
                    char next = raw[++i];
                    if (next == 'n') sb.Append('\n');
                    else if (next == 'r') { }
                    else if (next == 't') sb.Append('\t');
                    else sb.Append(next);
                }
                else
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        internal static string ExtractJsonStringField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName)) return "";
            try
            {
                Match m = Regex.Match(
                    json,
                    "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*\"([^\"\\\\]*(?:\\\\.[^\"\\\\]*)*)\"",
                    RegexOptions.CultureInvariant,
                    DebridRegexTimeout);
                if (m.Success)
                {
                    return UnescapeJsonStringFragment(m.Groups[1].Value);
                }
            }
            catch { }
            return "";
        }

        internal static long ExtractJsonLongField(string json, string fieldName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(fieldName)) return 0;
            try
            {
                Match m = Regex.Match(
                    json,
                    "\"" + Regex.Escape(fieldName) + "\"\\s*:\\s*(-?[0-9]+)",
                    RegexOptions.CultureInvariant,
                    DebridRegexTimeout);
                long v = 0;
                if (m.Success) long.TryParse(m.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out v);
                return v;
            }
            catch { }
            return 0;
        }
    }
}

