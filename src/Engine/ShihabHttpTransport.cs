using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public partial class MehediDynamicSegmentEngine
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern bool MoveFileExW(string lpExistingFileName, string lpNewFileName, int dwFlags);
        private const int MOVEFILE_REPLACE_EXISTING = 0x1;
        private const int MOVEFILE_WRITE_THROUGH = 0x8;
        private const int MaxHttpRedirects = 10;
        private const int MaxHostRateEntries = 1024;

        private static readonly TimeSpan TransportRegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex GdriveAnchorTitleRegex = new Regex(@"<a[^>]+href=""[^""]*""[^>]*>([^<]{2,180})</a>", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TransportRegexTimeout);
        private static readonly Regex ContentRangeTotalRegex = new Regex(@"/(\d+)", RegexOptions.Compiled | RegexOptions.CultureInvariant, TransportRegexTimeout);
        private static readonly Regex CdUtf8Regex = new Regex(@"filename\*\s*=\s*UTF-8''([^;]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TransportRegexTimeout);
        private static readonly Regex CdQuotedRegex = new Regex(@"filename\s*=\s*""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TransportRegexTimeout);
        private static readonly Regex CdUnquotedRegex = new Regex(@"filename\s*=\s*([^;]+)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, TransportRegexTimeout);

        private int GetAdaptiveEffectiveConnections(string hostName, ShihabDownloadItem item)
        {
            int rawRequested = item != null && item.MehediMaxConnections > 0
                ? item.MehediMaxConnections
                : _mehediConfig.ShihabDefaultMaxConnections;
            if (item != null && item.ShihabTransientMaxChunks > 0)
            {
                rawRequested = Math.Min(rawRequested, item.ShihabTransientMaxChunks);
            }
            int effective = _mehediConfig.GetEffectiveMaxConnectionsForHost(hostName, rawRequested);
            if (!string.IsNullOrWhiteSpace(hostName))
            {
                lock (_shihabEngineLock)
                {
                    int adaptiveCap;
                    if (_adaptiveHostConcurrencyCaps.TryGetValue(hostName, out adaptiveCap) && adaptiveCap > 0)
                    {
                        effective = Math.Min(effective, adaptiveCap);
                    }
                }
            }
            return Math.Max(1, effective);
        }

        private void RecordHostThrottleSignal(string hostName, HttpWebResponse resp, int currentDesired)
        {
            if (string.IsNullOrWhiteSpace(hostName)) return;
            int retrySec = 2;
            if (resp != null)
            {
                string ra = resp.Headers["Retry-After"];
                int parsedSec;
                DateTime parsedDt;
                if (!string.IsNullOrWhiteSpace(ra))
                {
                    if (int.TryParse(ra.Trim(), out parsedSec) && parsedSec > 0)
                    {
                        retrySec = Math.Min(120, parsedSec);
                    }
                    else if (DateTime.TryParse(ra.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsedDt))
                    {
                        double diff = (parsedDt - DateTime.UtcNow).TotalSeconds;
                        if (diff > 0) retrySec = (int)Math.Min(120.0, diff);
                    }
                }
            }
            lock (_shihabEngineLock)
            {
                if (_adaptiveHostConcurrencyCaps.Count >= MaxHostRateEntries && !_adaptiveHostConcurrencyCaps.ContainsKey(hostName))
                {
                    _adaptiveHostConcurrencyCaps.Clear();
                }
                if (_hostRetryCooldownUntilUtc.Count >= MaxHostRateEntries && !_hostRetryCooldownUntilUtc.ContainsKey(hostName))
                {
                    _hostRetryCooldownUntilUtc.Clear();
                }

                int prevCap;
                if (!_adaptiveHostConcurrencyCaps.TryGetValue(hostName, out prevCap) || prevCap <= 0)
                {
                    prevCap = Math.Max(1, currentDesired);
                }
                int nextCap = Math.Max(1, prevCap / 2);
                _adaptiveHostConcurrencyCaps[hostName] = nextCap;
                _hostRetryCooldownUntilUtc[hostName] = DateTime.UtcNow.AddSeconds(retrySec);
                EmitShihabLog(string.Format("[AdaptiveRate] Host {0} returned HTTP 429/503; capping concurrency -> {1} (cooldown {2}s)",
                    hostName, nextCap, retrySec));
            }
        }

        private void WaitForHostCooldownIfAny(string hostName, CancellationToken token)
        {
            if (string.IsNullOrWhiteSpace(hostName)) return;
            DateTime untilUtc = DateTime.MinValue;
            lock (_shihabEngineLock)
            {
                DateTime stored;
                if (_hostRetryCooldownUntilUtc.TryGetValue(hostName, out stored))
                {
                    untilUtc = stored;
                }
            }
            while (!token.IsCancellationRequested && DateTime.UtcNow < untilUtc)
            {
                long remMs = (long)Math.Max(10.0, Math.Min(200.0, (untilUtc - DateTime.UtcNow).TotalMilliseconds));
                WaitSleepOrCancel((int)remMs, token);
            }
        }

        private static bool TryAtomicMoveReplace(string sourcePath, string destPath)
        {
            try
            {
                string destDir = Path.GetDirectoryName(destPath);
                if (!string.IsNullOrEmpty(destDir)) Directory.CreateDirectory(destDir);
                if (MoveFileExW(sourcePath, destPath, MOVEFILE_REPLACE_EXISTING | MOVEFILE_WRITE_THROUGH))
                {
                    return true;
                }
            }
            catch { }
            try
            {
                if (File.Exists(destPath)) File.Delete(destPath);
                File.Move(sourcePath, destPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsShihabGoogleVideoUrl(string mehediUrl)
        {
            if (string.IsNullOrWhiteSpace(mehediUrl)) return false;
            Uri shihabUri;
            if (!Uri.TryCreate(mehediUrl, UriKind.Absolute, out shihabUri)) return false;
            string mehediHost = (shihabUri.Host ?? "").ToLowerInvariant();
            return mehediHost.EndsWith(".googlevideo.com", StringComparison.Ordinal) ||
                   mehediHost == "googlevideo.com" ||
                   shihabUri.AbsolutePath.IndexOf("/videoplayback", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        public static bool IsRealShihabGoogleVideoDomain(string mehediUrl)
        {
            if (string.IsNullOrWhiteSpace(mehediUrl)) return false;
            Uri shihabUri;
            if (!Uri.TryCreate(mehediUrl, UriKind.Absolute, out shihabUri)) return false;
            string mehediHost = (shihabUri.Host ?? "").ToLowerInvariant();
            return mehediHost.EndsWith(".googlevideo.com", StringComparison.Ordinal) ||
                   mehediHost == "googlevideo.com";
        }

        public static string GetShihabUrlQueryParam(string mehediUrl, string shihabParamKey)
        {
            if (string.IsNullOrWhiteSpace(mehediUrl) || string.IsNullOrWhiteSpace(shihabParamKey)) return "";
            Uri shihabUri;
            if (!Uri.TryCreate(mehediUrl, UriKind.Absolute, out shihabUri)) return "";
            string mehediQuery = shihabUri.Query;
            if (string.IsNullOrEmpty(mehediQuery) || mehediQuery == "?") return "";
            string[] shihabPairs = mehediQuery.TrimStart('?').Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            for (int mehediI = 0; mehediI < shihabPairs.Length; mehediI++)
            {
                int shihabEq = shihabPairs[mehediI].IndexOf('=');
                if (shihabEq <= 0) continue;
                string mehediKey = shihabPairs[mehediI].Substring(0, shihabEq);
                if (string.Equals(mehediKey, shihabParamKey, StringComparison.OrdinalIgnoreCase))
                {
                    try { return Uri.UnescapeDataString(shihabPairs[mehediI].Substring(shihabEq + 1)); }
                    catch { return shihabPairs[mehediI].Substring(shihabEq + 1); }
                }
            }
            return "";
        }

        public static bool IsDirectMobileShihabGoogleVideoUrl(string mehediUrl)
        {
            if (!IsRealShihabGoogleVideoDomain(mehediUrl)) return false;
            string shihabClient = GetShihabUrlQueryParam(mehediUrl, "c");
            if (string.Equals(shihabClient, "VISIONOS", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            if (string.Equals(shihabClient, "ANDROID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shihabClient, "IOS", StringComparison.OrdinalIgnoreCase))
            {
                if (!string.IsNullOrEmpty(GetShihabUrlQueryParam(mehediUrl, "bui")))
                {
                    double mehediDurSec;
                    if (double.TryParse(
                        GetShihabUrlQueryParam(mehediUrl, "dur"),
                        NumberStyles.Float,
                        CultureInfo.InvariantCulture,
                        out mehediDurSec) && mehediDurSec > 55.0)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        public static long GetShihabGoogleVideoBurstSliceBytes(string mehediUrl)
        {
            if (!IsRealShihabGoogleVideoDomain(mehediUrl))
            {
                return -1L;
            }
            string shihabMime = GetShihabUrlQueryParam(mehediUrl, "mime");
            if ((!string.IsNullOrEmpty(shihabMime) && shihabMime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase)) ||
                mehediUrl.IndexOf("mime=audio", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return 1048576L;
            }
            return 4194304L;
        }

        public static bool IsPoisonedWebShihabGoogleVideoUrl(string mehediUrl)
        {
            if (!IsRealShihabGoogleVideoDomain(mehediUrl)) return false;
            string shihabClient = GetShihabUrlQueryParam(mehediUrl, "c").ToUpperInvariant();
            if (shihabClient == "WEB" ||
                shihabClient == "MWEB" ||
                shihabClient == "WEB_REMIX" ||
                shihabClient == "WEB_CREATOR" ||
                shihabClient == "TVHTML5")
            {
                return true;
            }
            if (!string.IsNullOrEmpty(GetShihabUrlQueryParam(mehediUrl, "siu")) ||
                !string.IsNullOrEmpty(GetShihabUrlQueryParam(mehediUrl, "sabr")) ||
                !string.IsNullOrEmpty(GetShihabUrlQueryParam(mehediUrl, "ump")))
            {
                return true;
            }
            long mehediExpireUnix;
            if (long.TryParse(GetShihabUrlQueryParam(mehediUrl, "expire"), out mehediExpireUnix) && mehediExpireUnix > 0)
            {
                long shihabNowUnix = (long)(DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
                if (mehediExpireUnix <= shihabNowUnix + 30L)
                {
                    return true;
                }
            }
            return false;
        }

        public static string SanitizeMehediGoogleVideoUrl(string shihabRawUrl, out long mehediParsedClen)
        {
            mehediParsedClen = -1L;
            if (string.IsNullOrWhiteSpace(shihabRawUrl)) return shihabRawUrl;
            Uri shihabUri;
            if (!Uri.TryCreate(shihabRawUrl, UriKind.Absolute, out shihabUri)) return shihabRawUrl;

            string mehediQuery = shihabUri.Query;
            if (string.IsNullOrEmpty(mehediQuery) || mehediQuery == "?")
            {
                return shihabRawUrl;
            }

            string[] shihabPairs = mehediQuery.TrimStart('?').Split(new char[] { '&' }, StringSplitOptions.RemoveEmptyEntries);
            bool mehediRangeIsSigned = false;
            for (int shihabI = 0; shihabI < shihabPairs.Length; shihabI++)
            {
                int mehediEq = shihabPairs[shihabI].IndexOf('=');
                if (mehediEq <= 0) continue;
                string shihabKey = shihabPairs[shihabI].Substring(0, mehediEq);
                string mehediVal;
                try { mehediVal = Uri.UnescapeDataString(shihabPairs[shihabI].Substring(mehediEq + 1)); }
                catch { mehediVal = shihabPairs[shihabI].Substring(mehediEq + 1); }

                if (string.Equals(shihabKey, "clen", StringComparison.OrdinalIgnoreCase))
                {
                    long shihabClen;
                    if (long.TryParse(mehediVal, out shihabClen) && shihabClen > 0)
                    {
                        mehediParsedClen = shihabClen;
                    }
                }
                else if (string.Equals(shihabKey, "sparams", StringComparison.OrdinalIgnoreCase))
                {
                    string[] shihabSigned = mehediVal.Split(',');
                    for (int mehediS = 0; mehediS < shihabSigned.Length; mehediS++)
                    {
                        if (string.Equals(shihabSigned[mehediS].Trim(), "range", StringComparison.OrdinalIgnoreCase))
                        {
                            mehediRangeIsSigned = true;
                        }
                    }
                }
            }

            List<string> mehediKept = new List<string>(shihabPairs.Length);
            for (int shihabI = 0; shihabI < shihabPairs.Length; shihabI++)
            {
                int mehediEq = shihabPairs[shihabI].IndexOf('=');
                string shihabKey = mehediEq > 0 ? shihabPairs[shihabI].Substring(0, mehediEq) : shihabPairs[shihabI];
                if (string.Equals(shihabKey, "alr", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(shihabKey, "rn", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(shihabKey, "rbuf", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(shihabKey, "ump", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(shihabKey, "sabr", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                if (!mehediRangeIsSigned && string.Equals(shihabKey, "range", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
                mehediKept.Add(shihabPairs[shihabI]);
            }

            string shihabBase = shihabUri.GetLeftPart(UriPartial.Path);
            return mehediKept.Count > 0 ? (shihabBase + "?" + string.Join("&", mehediKept.ToArray())) : shihabBase;
        }

        private static string AppendShihabQueryRangeParam(string mehediCleanUrl, long shihabStart, long mehediEnd)
        {
            string shihabSep = mehediCleanUrl.IndexOf('?') >= 0 ? "&" : "?";
            return string.Format("{0}{1}range={2}-{3}", mehediCleanUrl, shihabSep, shihabStart, mehediEnd);
        }

        private static string GetEffectiveHttpUrl(ShihabDownloadItem item, string fallbackUrl)
        {
            if (item != null && !string.IsNullOrWhiteSpace(item.ShihabTransientUnlockedUrl))
            {
                return item.ShihabTransientUnlockedUrl;
            }
            return !string.IsNullOrWhiteSpace(fallbackUrl) ? fallbackUrl : (item != null ? item.MehediUrl : "");
        }

        private static bool IsSameHttpOrigin(Uri a, Uri b)
        {
            if (a == null || b == null) return false;
            return string.Equals(a.Scheme, b.Scheme, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(a.Host, b.Host, StringComparison.OrdinalIgnoreCase) &&
                   a.Port == b.Port;
        }

        private static bool IsAlwaysBlockedMetadataOrInternalHost(Uri targetUri)
        {
            if (targetUri == null) return true;
            string clean = (targetUri.Host ?? "").Trim().Trim('[', ']').TrimEnd('.');
            string lower = clean.ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(lower) ||
                lower == "metadata.google.internal" ||
                lower.EndsWith(".internal", StringComparison.Ordinal))
            {
                return true;
            }
            IPAddress literal;
            if (IPAddress.TryParse(clean, out literal))
            {
                if (literal.IsIPv4MappedToIPv6) literal = literal.MapToIPv4();
                if (literal.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    byte[] b = literal.GetAddressBytes();
                    // Always block link-local / cloud metadata (169.254.0.0/16) and unspecified (0.0.0.0/8)
                    if (b.Length == 4 && (b[0] == 0 || (b[0] == 169 && b[1] == 254))) return true;
                }
                else if (literal.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
                {
                    if (literal.IsIPv6LinkLocal || literal.Equals(IPAddress.IPv6Any) || literal.Equals(IPAddress.IPv6None)) return true;
                    byte[] v6 = literal.GetAddressBytes();
                    // Amazon / cloud IPv6 metadata fd00:ec2::254
                    if (v6.Length == 16 && v6[0] == 0xfd && v6[1] == 0x00 && v6[2] == 0x0e && v6[3] == 0xc2) return true;
                }
            }
            return false;
        }

        private static string StripUserInfoFromUri(Uri uri)
        {
            if (uri == null || string.IsNullOrEmpty(uri.UserInfo)) return uri != null ? uri.AbsoluteUri : "";
            UriBuilder ub = new UriBuilder(uri);
            ub.UserName = "";
            ub.Password = "";
            return ub.Uri.AbsoluteUri;
        }

        private HttpWebRequest CreateShihabHttpRequestCore(
            string mehediUrl,
            string shihabMethod,
            ShihabDownloadItem mehediContextItem,
            long shihabRangeStart,
            long mehediRangeEnd,
            bool stripCrossOriginCredentials,
            string ifRangeValidator)
        {
            Uri validatedUri;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(mehediUrl, false, out validatedUri))
            {
                throw new SecurityException("Blocked non-HTTP or malformed URL.");
            }
            if (IsAlwaysBlockedMetadataOrInternalHost(validatedUri))
            {
                throw new SecurityException("Blocked access to restricted internal or cloud metadata endpoint.");
            }

            HttpWebRequest shihabReq = (HttpWebRequest)WebRequest.Create(validatedUri);
            shihabReq.Method = shihabMethod;
            shihabReq.AllowAutoRedirect = false;
            shihabReq.Timeout = 25000;
            shihabReq.ReadWriteTimeout = 30000;
            shihabReq.KeepAlive = true;
            shihabReq.Accept = "*/*";
            shihabReq.AutomaticDecompression = DecompressionMethods.None;

            string shihabGvClient = IsRealShihabGoogleVideoDomain(mehediUrl) ? GetShihabUrlQueryParam(mehediUrl, "c") : "";
            bool mehediIsDirectInnerTubeGv =
                string.Equals(shihabGvClient, "VISIONOS", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shihabGvClient, "ANDROID", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(shihabGvClient, "IOS", StringComparison.OrdinalIgnoreCase);

            string mehediUa = (mehediContextItem != null && !string.IsNullOrWhiteSpace(mehediContextItem.MehediUserAgent))
                ? mehediContextItem.MehediUserAgent
                : _mehediConfig.ShihabDefaultUserAgent;
            if (mehediIsDirectInnerTubeGv)
            {
                if (string.Equals(shihabGvClient, "VISIONOS", StringComparison.OrdinalIgnoreCase))
                {
                    mehediUa = MehediYouTubeInnerTubeResolver.ShihabVisionOsUserAgent;
                }
                else if (string.Equals(shihabGvClient, "IOS", StringComparison.OrdinalIgnoreCase))
                {
                    mehediUa = MehediYouTubeInnerTubeResolver.MehediIosUserAgent;
                }
                else
                {
                    mehediUa = MehediYouTubeInnerTubeResolver.ShihabAndroidUserAgent;
                }
            }
            if (!string.IsNullOrWhiteSpace(mehediUa))
            {
                shihabReq.UserAgent = mehediUa;
            }
            if (!stripCrossOriginCredentials && !mehediIsDirectInnerTubeGv && mehediContextItem != null && !string.IsNullOrWhiteSpace(mehediContextItem.MehediReferer))
            {
                try { shihabReq.Referer = mehediContextItem.MehediReferer; } catch { }
            }
            if (!stripCrossOriginCredentials && !mehediIsDirectInnerTubeGv && mehediContextItem != null && !string.IsNullOrWhiteSpace(mehediContextItem.MehediOriginHeader))
            {
                try { shihabReq.Headers["Origin"] = mehediContextItem.MehediOriginHeader; } catch { }
            }
            if (!stripCrossOriginCredentials &&
                !mehediIsDirectInnerTubeGv &&
                mehediContextItem != null &&
                !mehediContextItem.MehediOmitCookies &&
                !string.IsNullOrWhiteSpace(mehediContextItem.ShihabCookies))
            {
                shihabReq.Headers[HttpRequestHeader.Cookie] = mehediContextItem.ShihabCookies;
            }

            if (!stripCrossOriginCredentials)
            {
                string shihabUser = mehediContextItem != null ? mehediContextItem.ShihabAuthUser : "";
                string mehediPass = mehediContextItem != null ? ShihabSecretsVault.UnprotectSecret(mehediContextItem.MehediAuthPassword) : "";
                if (string.IsNullOrWhiteSpace(shihabUser))
                {
                    ShihabSiteLoginCredential shihabSavedCred = _mehediConfig.FindShihabCredentialForUrl(mehediUrl);
                    if (shihabSavedCred != null)
                    {
                        shihabUser = shihabSavedCred.MehediUserName;
                        mehediPass = shihabSavedCred.ShihabPassword;
                    }
                }
                if (!string.IsNullOrWhiteSpace(shihabUser))
                {
                    string mehediBasic = Convert.ToBase64String(Encoding.UTF8.GetBytes(shihabUser + ":" + (mehediPass ?? "")));
                    shihabReq.Headers[HttpRequestHeader.Authorization] = "Basic " + mehediBasic;
                }
            }

            try
            {
                shihabReq.Headers["Want-Repr-Digest"] = "sha-256=9, md5=3";
            }
            catch { }

            ShihabDnsAndNetworkManager.ApplyAdapterAndProxyPolicy(shihabReq, mehediUrl, _mehediConfig);

            if (shihabRangeStart >= 0)
            {
                if (mehediRangeEnd >= shihabRangeStart)
                {
                    shihabReq.AddRange(shihabRangeStart, mehediRangeEnd);
                }
                else
                {
                    shihabReq.AddRange(shihabRangeStart);
                }
                if (!string.IsNullOrWhiteSpace(ifRangeValidator))
                {
                    try { shihabReq.Headers["If-Range"] = ifRangeValidator; } catch { }
                }
            }

            if (!stripCrossOriginCredentials &&
                mehediContextItem != null &&
                !string.IsNullOrEmpty(mehediContextItem.ShihabPostBody) &&
                shihabRangeStart <= 0 &&
                shihabMethod == "GET")
            {
                shihabReq.Method = "POST";
                shihabReq.ContentType = "application/x-www-form-urlencoded";
                byte[] postBytes = Encoding.UTF8.GetBytes(mehediContextItem.ShihabPostBody);
                shihabReq.ContentLength = postBytes.Length;
                using (Stream reqStream = shihabReq.GetRequestStream())
                {
                    reqStream.Write(postBytes, 0, postBytes.Length);
                }
            }

            return shihabReq;
        }

        private HttpWebResponse GetResponseWithSafeRedirects(
            string initialUrl,
            string httpMethod,
            ShihabDownloadItem ctxItem,
            long rangeStart,
            long rangeEnd,
            string ifRangeValidator,
            CancellationToken cancelToken,
            out HttpWebRequest finalActiveRequest)
        {
            finalActiveRequest = null;
            Uri initialUri;
            if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(initialUrl, false, out initialUri))
            {
                throw new IOException("Invalid or non-HTTP(S) target URL.");
            }
            if (IsAlwaysBlockedMetadataOrInternalHost(initialUri))
            {
                throw new SecurityException("Target URL points to a blocked internal/metadata endpoint.");
            }

            bool initialWasLocalOrPrivate = ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(initialUri.Host, true);
            string currentUrl = initialUri.AbsoluteUri;
            string currentMethod = httpMethod;
            bool stripCreds = false;

            for (int hop = 0; hop <= MaxHttpRedirects; hop++)
            {
                Uri hopUri;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(currentUrl, false, out hopUri))
                {
                    throw new IOException("Invalid or non-HTTP(S) hop URL.");
                }
                string hopHost = hopUri.Host ?? "";
                WaitForHostCooldownIfAny(hopHost, cancelToken);
                cancelToken.ThrowIfCancellationRequested();

                HttpWebRequest req = CreateShihabHttpRequestCore(
                    currentUrl,
                    currentMethod,
                    ctxItem,
                    rangeStart,
                    rangeEnd,
                    stripCreds,
                    ifRangeValidator);
                finalActiveRequest = req;

                HttpWebResponse resp = null;
                try
                {
                    using (cancelToken.Register(delegate { try { req.Abort(); } catch { } }))
                    {
                        resp = (HttpWebResponse)req.GetResponse();
                    }
                }
                catch (WebException wex)
                {
                    HttpWebResponse errResp = wex.Response as HttpWebResponse;
                    if (errResp != null)
                    {
                        try
                        {
                            int errCode = (int)errResp.StatusCode;
                            if (errCode == 429 || errCode == 503)
                            {
                                RecordHostThrottleSignal(hopHost, errResp, GetAdaptiveEffectiveConnections(hopHost, ctxItem));
                            }
                        }
                        finally
                        {
                            try { errResp.Close(); } catch { }
                        }
                    }
                    else if (wex.Response != null)
                    {
                        try { wex.Response.Close(); } catch { }
                    }
                    throw;
                }

                int code = (int)resp.StatusCode;
                if (code == 301 || code == 302 || code == 303 || code == 307 || code == 308)
                {
                    string loc = resp.Headers["Location"];
                    resp.Close();
                    if (string.IsNullOrWhiteSpace(loc))
                    {
                        throw new IOException("Redirect response missing Location header.");
                    }
                    Uri nextUri;
                    if (!Uri.TryCreate(hopUri, loc, out nextUri) ||
                        !ShihabProcessAndPathSafety.TryValidateHttpUrl(nextUri.AbsoluteUri, false, out nextUri))
                    {
                        throw new IOException("Invalid or unsafe redirect Location URI.");
                    }
                    if (IsAlwaysBlockedMetadataOrInternalHost(nextUri))
                    {
                        throw new SecurityException("Blocked redirect to internal or cloud metadata address.");
                    }
                    bool sameOriginWithInitial = IsSameHttpOrigin(initialUri, nextUri);
                    if (!sameOriginWithInitial)
                    {
                        stripCreds = true;
                        nextUri = new Uri(StripUserInfoFromUri(nextUri));
                        // Public/external targets must never redirect into loopback or LAN internal hosts.
                        if (!initialWasLocalOrPrivate && ShihabProcessAndPathSafety.IsHostDisallowedForOutboundSsrf(nextUri.Host, true))
                        {
                            throw new SecurityException("Blocked cross-origin redirect to internal/private network address.");
                        }
                    }
                    if ((code == 301 || code == 302 || code == 303) && string.Equals(currentMethod, "POST", StringComparison.OrdinalIgnoreCase))
                    {
                        currentMethod = "GET";
                    }
                    currentUrl = nextUri.AbsoluteUri;
                    continue;
                }
                return resp;
            }
            throw new IOException("Too many HTTP redirects (> " + MaxHttpRedirects + ").");
        }

        public ShihabProbeMetadata ProbeMehediUrl(string shihabUrl, ShihabDownloadItem mehediContextItem)
        {
            ShihabProbeMetadata shihabMeta = new ShihabProbeMetadata();
            shihabMeta.ShihabFinalUrl = shihabUrl;
            shihabMeta.ShihabContentLength = -1;
            shihabMeta.MehediSuggestedFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(shihabUrl);

            string gdriveResolvedUrl, gdriveHintName;
            if (ShihabDebridManager.TryResolveGoogleDriveOrDocsUrl(shihabUrl, _mehediConfig, out gdriveResolvedUrl, out gdriveHintName) &&
                !string.IsNullOrWhiteSpace(gdriveResolvedUrl))
            {
                shihabUrl = gdriveResolvedUrl;
                shihabMeta.ShihabFinalUrl = gdriveResolvedUrl;
                if (!string.IsNullOrWhiteSpace(gdriveHintName))
                {
                    shihabMeta.MehediSuggestedFileName = ShihabFormatHelpers.SanitizeShihabFileName(gdriveHintName);
                }
            }

            bool bypassDebrid = mehediContextItem != null && mehediContextItem.ShihabBypassDebrid;
            if (!bypassDebrid)
            {
                string linkPwd = mehediContextItem != null ? (mehediContextItem.ShihabArchivePassword ?? "") : "";
                string unrestrictedUrl, debridFileName, debridProvider;
                int debridMaxChunks;
                if (ShihabDebridManager.TryUnrestrictUrl(shihabUrl, linkPwd, _mehediConfig, out unrestrictedUrl, out debridFileName, out debridProvider, out debridMaxChunks) &&
                    !string.IsNullOrWhiteSpace(unrestrictedUrl))
                {
                    EmitShihabLog(string.Format("[Debrid] Unlocked via {0} (maxChunks={1}): {2}", debridProvider, debridMaxChunks, debridFileName));
                    shihabUrl = unrestrictedUrl;
                    shihabMeta.ShihabFinalUrl = unrestrictedUrl;
                    if (!string.IsNullOrWhiteSpace(debridFileName))
                    {
                        shihabMeta.MehediSuggestedFileName = ShihabFormatHelpers.SanitizeShihabFileName(debridFileName);
                    }
                    if (mehediContextItem != null)
                    {
                        mehediContextItem.ShihabTransientUnlockedUrl = unrestrictedUrl;
                        mehediContextItem.ShihabTransientMaxChunks = debridMaxChunks;
                    }
                }
            }

            Uri probeUri;
            if (Uri.TryCreate(shihabUrl, UriKind.Absolute, out probeUri) && !string.IsNullOrEmpty(probeUri.Host))
            {
                string countryCode;
                IPAddress ip = ShihabDnsAndNetworkManager.ResolveHostIpWithDohAndGeo(probeUri.Host, _mehediConfig, out countryCode);
                if (ip != null)
                {
                    shihabMeta.ShihabServerIp = ip.ToString();
                    shihabMeta.ShihabServerCountryCode = countryCode;
                    if (mehediContextItem != null)
                    {
                        mehediContextItem.ShihabServerIp = shihabMeta.ShihabServerIp;
                        mehediContextItem.ShihabServerCountryCode = shihabMeta.ShihabServerCountryCode;
                    }
                }

                if (probeUri.Scheme.Equals("ftp", StringComparison.OrdinalIgnoreCase) ||
                    probeUri.Scheme.Equals("ftps", StringComparison.OrdinalIgnoreCase) ||
                    probeUri.Scheme.Equals("ftpes", StringComparison.OrdinalIgnoreCase))
                {
                    return ProbeMehediFtpUrl(shihabUrl, mehediContextItem, shihabMeta);
                }
            }

            string mehediWatchVidId;
            if (MehediYouTubeInnerTubeResolver.TryExtractShihabYouTubeWatchVideoId(shihabUrl, out mehediWatchVidId))
            {
                ShihabYouTubeStreamsReplyDto shihabYtResolved = MehediYouTubeInnerTubeResolver.ResolveShihabYouTubeStreams(mehediWatchVidId, "");
                if (shihabYtResolved != null && shihabYtResolved.ShihabOk && shihabYtResolved.MehediStreams != null && shihabYtResolved.MehediStreams.Count > 0)
                {
                    MehediYouTubeResolvedStreamDto shihabBestVid;
                    MehediYouTubeResolvedStreamDto mehediBestAud;
                    MehediYouTubeInnerTubeResolver.SelectBestShihabStreamPair(
                        shihabYtResolved,
                        mehediContextItem != null ? mehediContextItem.ShihabQualityLabel : "",
                        false,
                        out shihabBestVid,
                        out mehediBestAud);
                    if (shihabBestVid != null)
                    {
                        shihabMeta.MehediSuccess = true;
                        shihabMeta.ShihabFinalUrl = shihabBestVid.ShihabUrl;
                        shihabMeta.ShihabContentType = shihabBestVid.MehediMimeType ?? "video/mp4";
                        shihabMeta.MehediAcceptsRanges = true;
                        long shihabCombinedSize = shihabBestVid.ShihabSizeBytes > 0
                            ? (shihabBestVid.ShihabSizeBytes + (mehediBestAud != null && mehediBestAud.ShihabSizeBytes > 0 ? mehediBestAud.ShihabSizeBytes : 0L))
                            : -1L;
                        shihabMeta.ShihabContentLength = shihabBestVid.ShihabSizeBytes > 0 ? shihabBestVid.ShihabSizeBytes : shihabCombinedSize;
                        string mehediTitleStem = ShihabFormatHelpers.SanitizeShihabFileName(
                            string.IsNullOrWhiteSpace(shihabYtResolved.ShihabTitle) ? ("YouTube_" + mehediWatchVidId) : shihabYtResolved.ShihabTitle);
                        string shihabExt = shihabBestVid.ShihabExt ?? "mp4";
                        string mehediQualPart = !string.IsNullOrWhiteSpace(shihabBestVid.ShihabResolution) ? (" [" + shihabBestVid.ShihabResolution + "]") : "";
                        shihabMeta.MehediSuggestedFileName = mehediTitleStem + mehediQualPart + "." + shihabExt;

                        if (mehediContextItem != null)
                        {
                            if (string.IsNullOrWhiteSpace(mehediContextItem.MehediReferer))
                            {
                                mehediContextItem.MehediReferer = shihabUrl;
                            }
                            mehediContextItem.MehediUrl = shihabBestVid.ShihabUrl;
                            mehediContextItem.MehediUserAgent = shihabBestVid.MehediUserAgent ?? MehediYouTubeInnerTubeResolver.ShihabVisionOsUserAgent;
                            mehediContextItem.MehediOmitCookies = true;
                            mehediContextItem.MehediOriginHeader = "";
                            if (!string.IsNullOrWhiteSpace(shihabBestVid.ShihabResolution))
                            {
                                mehediContextItem.ShihabQualityLabel = shihabBestVid.ShihabResolution;
                            }
                            if (mehediBestAud != null && !string.IsNullOrWhiteSpace(mehediBestAud.ShihabUrl))
                            {
                                mehediContextItem.MehediAudioCompanionUrl = mehediBestAud.ShihabUrl;
                                mehediContextItem.ShihabAudioCompanionBytes = mehediBestAud.ShihabSizeBytes;
                            }
                        }
                        return shihabMeta;
                    }
                }
            }

            if (IsShihabGoogleVideoUrl(shihabUrl))
            {
                long mehediGvClen;
                string shihabCleanGv = SanitizeMehediGoogleVideoUrl(shihabUrl, out mehediGvClen);
                shihabMeta.ShihabFinalUrl = shihabCleanGv;
                if (mehediGvClen > 0)
                {
                    shihabMeta.ShihabContentLength = mehediGvClen;
                    shihabMeta.MehediAcceptsRanges = true;
                }
                if (IsDirectMobileShihabGoogleVideoUrl(shihabCleanGv) && mehediGvClen > 0)
                {
                    shihabMeta.MehediSuccess = true;
                    string mehediMimeParam = GetShihabUrlQueryParam(shihabCleanGv, "mime");
                    shihabMeta.ShihabContentType = string.IsNullOrWhiteSpace(mehediMimeParam) ? "video/mp4" : mehediMimeParam;
                    return shihabMeta;
                }
                try
                {
                    string mehediMiniSliceUrl = AppendShihabQueryRangeParam(shihabCleanGv, 0, 511);
                    HttpWebRequest shihabGvProbe;
                    using (HttpWebResponse mehediGvResp = GetResponseWithSafeRedirects(mehediMiniSliceUrl, "GET", mehediContextItem, -1, -1, null, CancellationToken.None, out shihabGvProbe))
                    {
                        shihabMeta.ShihabContentType = mehediGvResp.ContentType ?? "";
                        if (shihabMeta.ShihabContentLength <= 0 && mehediGvResp.ContentLength > 512)
                        {
                            shihabMeta.ShihabContentLength = mehediGvResp.ContentLength;
                        }
                    }
                    shihabMeta.MehediSuccess = true;
                    return shihabMeta;
                }
                catch (Exception shihabGvEx)
                {
                    shihabMeta.MehediSuccess = mehediGvClen > 0;
                    shihabMeta.ShihabErrorMessage = shihabGvEx.Message;
                    return shihabMeta;
                }
            }

            try
            {
                HttpWebRequest mehediRangeProbe;
                using (HttpWebResponse shihabResp = GetResponseWithSafeRedirects(shihabUrl, "GET", mehediContextItem, 0, 0, null, CancellationToken.None, out mehediRangeProbe))
                {
                    PopulateShihabProbeFromResponse(shihabMeta, shihabResp);
                }
                if (shihabMeta.ShihabIsHtmlPage &&
                    (shihabUrl.IndexOf("drive.google.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     shihabUrl.IndexOf("drive.usercontent.google.com", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    TryFollowGoogleDriveConfirmationPage(shihabUrl, mehediContextItem, shihabMeta);
                }
                shihabMeta.MehediSuccess = true;
                return shihabMeta;
            }
            catch
            {
            }

            try
            {
                HttpWebRequest shihabHeadReq;
                using (HttpWebResponse mehediResp = GetResponseWithSafeRedirects(shihabUrl, "HEAD", mehediContextItem, -1, -1, null, CancellationToken.None, out shihabHeadReq))
                {
                    PopulateShihabProbeFromResponse(shihabMeta, mehediResp);
                }
                if (shihabMeta.ShihabIsHtmlPage &&
                    (shihabUrl.IndexOf("drive.google.com", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     shihabUrl.IndexOf("drive.usercontent.google.com", StringComparison.OrdinalIgnoreCase) >= 0))
                {
                    TryFollowGoogleDriveConfirmationPage(shihabUrl, mehediContextItem, shihabMeta);
                }
                shihabMeta.MehediSuccess = true;
                return shihabMeta;
            }
            catch (Exception shihabEx)
            {
                shihabMeta.MehediSuccess = false;
                shihabMeta.ShihabErrorMessage = shihabEx.Message;
                return shihabMeta;
            }
        }

        private void TryFollowGoogleDriveConfirmationPage(string gdriveUrl, ShihabDownloadItem ctxItem, ShihabProbeMetadata meta)
        {
            try
            {
                HttpWebRequest htmlReq;
                string html = "";
                string respBaseUrl = gdriveUrl;
                using (HttpWebResponse htmlResp = GetResponseWithSafeRedirects(gdriveUrl, "GET", ctxItem, -1, -1, null, CancellationToken.None, out htmlReq))
                using (Stream rs = htmlResp.GetResponseStream())
                {
                    if (htmlResp.ResponseUri != null) respBaseUrl = htmlResp.ResponseUri.AbsoluteUri;
                    if (rs != null)
                    {
                        byte[] cap = new byte[131072];
                        int total = 0;
                        while (total < cap.Length)
                        {
                            int r = rs.Read(cap, total, cap.Length - total);
                            if (r <= 0) break;
                            total += r;
                        }
                        html = Encoding.UTF8.GetString(cap, 0, total);
                    }
                }
                string confirmedUrl;
                Uri baseUriObj;
                if (!Uri.TryCreate(respBaseUrl, UriKind.Absolute, out baseUriObj))
                {
                    baseUriObj = new Uri("https://drive.usercontent.google.com/");
                }
                if (ShihabDebridManager.TryParseGoogleDriveConfirmationHtml(html, baseUriObj, out confirmedUrl) &&
                    !string.IsNullOrWhiteSpace(confirmedUrl))
                {
                    string confirmedName = "";
                    try
                    {
                        Match titleMatch = GdriveAnchorTitleRegex.Match(html);
                        if (titleMatch.Success)
                        {
                            confirmedName = WebUtility.HtmlDecode(titleMatch.Groups[1].Value.Trim());
                        }
                    }
                    catch { }
                    EmitShihabLog("[GoogleDrive] Bypassed virus-scan confirmation interstitial -> " + (!string.IsNullOrEmpty(confirmedName) ? confirmedName : confirmedUrl));
                    if (ctxItem != null) ctxItem.ShihabTransientUnlockedUrl = confirmedUrl;
                    if (!string.IsNullOrWhiteSpace(confirmedName))
                    {
                        meta.MehediSuggestedFileName = ShihabFormatHelpers.SanitizeShihabFileName(confirmedName);
                    }
                    HttpWebRequest confirmedProbe;
                    using (HttpWebResponse cResp = GetResponseWithSafeRedirects(confirmedUrl, "GET", ctxItem, 0, 0, null, CancellationToken.None, out confirmedProbe))
                    {
                        PopulateShihabProbeFromResponse(meta, cResp);
                        if (!string.IsNullOrWhiteSpace(confirmedName) &&
                            (string.IsNullOrWhiteSpace(meta.MehediSuggestedFileName) || meta.MehediSuggestedFileName == "uc" || meta.MehediSuggestedFileName == "download"))
                        {
                            meta.MehediSuggestedFileName = ShihabFormatHelpers.SanitizeShihabFileName(confirmedName);
                        }
                    }
                }
            }
            catch { }
        }

        private static bool TryNormalizeValidatedFtpUrl(string rawUrl, out string normalizedFtpUrl, out bool useTlsDefault)
        {
            normalizedFtpUrl = "";
            useTlsDefault = false;
            if (string.IsNullOrWhiteSpace(rawUrl) || rawUrl.IndexOf('\0') >= 0 || rawUrl.IndexOf('\r') >= 0 || rawUrl.IndexOf('\n') >= 0)
            {
                return false;
            }
            string trimmed = rawUrl.Trim();
            useTlsDefault = trimmed.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase) ||
                            trimmed.StartsWith("ftpes://", StringComparison.OrdinalIgnoreCase);
            if (!trimmed.StartsWith("ftp://", StringComparison.OrdinalIgnoreCase) && !useTlsDefault)
            {
                return false;
            }
            string replaced = trimmed.StartsWith("ftpes://", StringComparison.OrdinalIgnoreCase)
                ? "ftp://" + trimmed.Substring("ftpes://".Length)
                : (trimmed.StartsWith("ftps://", StringComparison.OrdinalIgnoreCase)
                    ? "ftp://" + trimmed.Substring("ftps://".Length)
                    : trimmed);
            Uri parsed;
            if (!Uri.TryCreate(replaced, UriKind.Absolute, out parsed) || string.IsNullOrWhiteSpace(parsed.Host))
            {
                return false;
            }
            if (IsAlwaysBlockedMetadataOrInternalHost(parsed))
            {
                return false;
            }
            normalizedFtpUrl = parsed.AbsoluteUri;
            return true;
        }

        private ShihabProbeMetadata ProbeMehediFtpUrl(string ftpUrl, ShihabDownloadItem ctxItem, ShihabProbeMetadata meta)
        {
            try
            {
                string normUrl;
                bool schemeTls;
                if (!TryNormalizeValidatedFtpUrl(ftpUrl, out normUrl, out schemeTls))
                {
                    throw new SecurityException("Blocked invalid or restricted FTP URL.");
                }
                bool useTls = _mehediConfig.ShihabFtpEnableTls || schemeTls;

                FtpWebRequest req = (FtpWebRequest)WebRequest.Create(normUrl);
                req.Method = WebRequestMethods.Ftp.GetFileSize;
                req.UseBinary = true;
                req.UsePassive = _mehediConfig.ShihabFtpUsePassiveMode;
                req.EnableSsl = useTls;
                req.Timeout = 15000;
                ApplyFtpCredentials(req, normUrl, ctxItem);
                using (FtpWebResponse resp = (FtpWebResponse)req.GetResponse())
                {
                    if (resp.ContentLength > 0)
                    {
                        meta.ShihabContentLength = resp.ContentLength;
                        meta.MehediAcceptsRanges = true;
                    }
                }

                try
                {
                    FtpWebRequest mdtmReq = (FtpWebRequest)WebRequest.Create(normUrl);
                    mdtmReq.Method = WebRequestMethods.Ftp.GetDateTimestamp;
                    mdtmReq.UseBinary = true;
                    mdtmReq.UsePassive = _mehediConfig.ShihabFtpUsePassiveMode;
                    mdtmReq.EnableSsl = useTls;
                    mdtmReq.Timeout = 10000;
                    ApplyFtpCredentials(mdtmReq, normUrl, ctxItem);
                    using (FtpWebResponse mdtmResp = (FtpWebResponse)mdtmReq.GetResponse())
                    {
                        if (mdtmResp.LastModified != DateTime.MinValue)
                        {
                            meta.MehediLastModifiedHeader = mdtmResp.LastModified.ToUniversalTime().ToString("R", CultureInfo.InvariantCulture);
                        }
                    }
                }
                catch { }

                meta.MehediSuccess = true;
            }
            catch (Exception ex)
            {
                meta.MehediSuccess = false;
                meta.ShihabErrorMessage = ex.Message;
            }
            return meta;
        }

        private void ApplyFtpCredentials(FtpWebRequest req, string url, ShihabDownloadItem ctxItem)
        {
            string user = ctxItem != null ? ctxItem.ShihabAuthUser : "";
            string pass = ctxItem != null ? ShihabSecretsVault.UnprotectSecret(ctxItem.MehediAuthPassword) : "";
            if (string.IsNullOrWhiteSpace(user))
            {
                ShihabSiteLoginCredential saved = _mehediConfig.FindShihabCredentialForUrl(url);
                if (saved != null)
                {
                    user = saved.MehediUserName;
                    pass = saved.ShihabPassword;
                }
            }
            if (!string.IsNullOrWhiteSpace(user))
            {
                req.Credentials = new NetworkCredential(user, pass ?? "");
            }
        }

        private static void PopulateShihabProbeFromResponse(ShihabProbeMetadata mehediMeta, HttpWebResponse shihabResp)
        {
            if (shihabResp.ResponseUri != null)
            {
                mehediMeta.ShihabFinalUrl = shihabResp.ResponseUri.AbsoluteUri;
                mehediMeta.MehediSuggestedFileName = ShihabFormatHelpers.ExtractMehediFileNameFromUrl(mehediMeta.ShihabFinalUrl);
            }
            mehediMeta.ShihabContentType = shihabResp.ContentType ?? "";
            mehediMeta.MehediLastModifiedHeader = shihabResp.Headers["Last-Modified"] ?? "";
            mehediMeta.ShihabETagHeader = shihabResp.Headers["ETag"] ?? "";
            mehediMeta.ShihabDigestSpec = ShihabChecksumVerifier.ExtractServerDigestSpec(
                shihabResp.Headers,
                mehediMeta.ShihabETagHeader,
                (int)shihabResp.StatusCode);

            string shihabContentDisp = shihabResp.Headers["Content-Disposition"] ?? "";
            string mehediParsedName = ParseShihabContentDispositionName(shihabContentDisp);
            if (!string.IsNullOrWhiteSpace(mehediParsedName))
            {
                mehediMeta.MehediSuggestedFileName = ShihabFormatHelpers.SanitizeShihabFileName(mehediParsedName);
            }

            string shihabContentRange = shihabResp.Headers["Content-Range"] ?? "";
            string mehediAcceptRanges = shihabResp.Headers["Accept-Ranges"] ?? "";
            if (shihabResp.StatusCode == HttpStatusCode.PartialContent && !string.IsNullOrEmpty(shihabContentRange))
            {
                mehediMeta.MehediAcceptsRanges = true;
                try
                {
                    Match shihabMatch = ContentRangeTotalRegex.Match(shihabContentRange);
                    long mehediTotalParsed;
                    if (shihabMatch.Success && long.TryParse(shihabMatch.Groups[1].Value, out mehediTotalParsed))
                    {
                        mehediMeta.ShihabContentLength = mehediTotalParsed;
                    }
                }
                catch { }
            }
            else
            {
                if (mehediAcceptRanges.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    mehediMeta.MehediAcceptsRanges = true;
                }
                if (shihabResp.ContentLength >= 0)
                {
                    mehediMeta.ShihabContentLength = shihabResp.ContentLength;
                }
            }

            string shihabCtLower = mehediMeta.ShihabContentType.ToLowerInvariant();
            if (shihabCtLower.Contains("text/html"))
            {
                mehediMeta.ShihabIsHtmlPage = true;
            }
            if (shihabCtLower.Contains("mpegurl") ||
                mehediMeta.ShihabFinalUrl.IndexOf(".m3u8", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                mehediMeta.MehediIsHlsPlaylist = true;
            }
        }

        private static string ParseShihabContentDispositionName(string mehediHeader)
        {
            if (string.IsNullOrWhiteSpace(mehediHeader))
            {
                return "";
            }
            try
            {
                Match shihabUtf8Match = CdUtf8Regex.Match(mehediHeader);
                if (shihabUtf8Match.Success)
                {
                    return Uri.UnescapeDataString(shihabUtf8Match.Groups[1].Value.Trim().Trim('"'));
                }
                Match mehediSimpleMatch = CdQuotedRegex.Match(mehediHeader);
                if (mehediSimpleMatch.Success)
                {
                    return mehediSimpleMatch.Groups[1].Value.Trim();
                }
                Match shihabUnquoted = CdUnquotedRegex.Match(mehediHeader);
                if (shihabUnquoted.Success)
                {
                    return shihabUnquoted.Groups[1].Value.Trim().Trim('"');
                }
            }
            catch { }
            return "";
        }
    }
}

