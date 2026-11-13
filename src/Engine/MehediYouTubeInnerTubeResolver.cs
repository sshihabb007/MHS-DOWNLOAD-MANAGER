using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public static class MehediYouTubeInnerTubeResolver
    {
        public const string ShihabVisionOsClientVersion = "1.02";
        public const string ShihabVisionOsUserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 15_7_3) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/26.0 Safari/605.1.15";
        public const string ShihabAndroidClientVersion = "20.10.38";
        public const string ShihabAndroidUserAgent = "com.google.android.youtube/20.10.38 (Linux; U; Android 14) gzip";
        public const string MehediIosClientVersion = "20.10.4";
        public const string MehediIosUserAgent = "com.google.ios.youtube/20.10.4 (iPhone16,2; U; CPU iOS 18_3_2 like Mac OS X;)";

        private const int MaxStreamCacheEntries = 256;
        private const int MaxTitleCacheEntries = 512;
        private const int MaxHttpResponseBytes = 8 * 1024 * 1024;
        private const string ChromeDesktopUserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36";

        private static readonly TimeSpan RegexTimeout = TimeSpan.FromMilliseconds(250);
        private static readonly Regex VisitorDataJsonRegex = new Regex(@"""VISITOR_DATA""\s*:\s*""([^""]+)""", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex VisitorDataTokenRegex = new Regex(@"(Cgt[A-Za-z0-9_\-%=]{16,})", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex WatchPathRegex = new Regex(@"^/(?:shorts|embed|live|v|reels)/([A-Za-z0-9_-]{11})(?:[/?#]|$)", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex DescVideoIdRegex = new Regex(@"(?:v=|yt:|youtu\.be/)([A-Za-z0-9_-]{11})", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex BracketSuffixRegex = new Regex(@"\s*\[[^\]]+\]\s*(?:_\d+)?$", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex TrailingIndexRegex = new Regex(@"_\d+$", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex SearchVideoRendererRegex = new Regex(@"""videoRenderer""\s*:\s*\{\s*""videoId""\s*:\s*""([A-Za-z0-9_-]{11})""", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex SearchFallbackVideoIdRegex = new Regex(@"""videoId""\s*:\s*""([A-Za-z0-9_-]{11})""", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex MimeCodecsRegex = new Regex(@"codecs=""([^""]+)""", RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex QualityLabelPRegex = new Regex(@"^(\d{3,4}p)", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);
        private static readonly Regex ResolutionNumRegex = new Regex(@"(\d{3,4})", RegexOptions.Compiled | RegexOptions.CultureInvariant, RegexTimeout);

        private static readonly object _shihabCacheLock = new object();
        private static readonly Dictionary<string, ShihabCachedYtEntry> _mehediStreamCache = new Dictionary<string, ShihabCachedYtEntry>(StringComparer.Ordinal);
        private static readonly Queue<string> _mehediStreamCacheOrder = new Queue<string>();
        private static readonly Dictionary<string, string> _shihabTitleToVideoIdCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        private static readonly Queue<string> _shihabTitleCacheOrder = new Queue<string>();
        private static string _mehediCachedVisitorData = "";
        private static DateTime _shihabVisitorDataCachedAtUtc = DateTime.MinValue;

        private static string ReadBoundedUtf8Stream(Stream stream, int maxBytes)
        {
            if (stream == null) return "";
            byte[] buf = new byte[16384];
            using (MemoryStream ms = new MemoryStream())
            {
                int total = 0;
                while (total < maxBytes)
                {
                    int toRead = Math.Min(buf.Length, maxBytes - total);
                    int r = stream.Read(buf, 0, toRead);
                    if (r <= 0) break;
                    ms.Write(buf, 0, r);
                    total += r;
                }
                return Encoding.UTF8.GetString(ms.GetBuffer(), 0, (int)ms.Length);
            }
        }

        private static void StoreBoundedStreamCacheLocked(string videoId, ShihabYouTubeStreamsReplyDto reply)
        {
            DateTime now = DateTime.UtcNow;
            List<string> expired = new List<string>();
            foreach (KeyValuePair<string, ShihabCachedYtEntry> kv in _mehediStreamCache)
            {
                if (kv.Value == null || (now - kv.Value.MehediCachedAtUtc).TotalMinutes >= 15.0)
                {
                    expired.Add(kv.Key);
                }
            }
            for (int i = 0; i < expired.Count; i++)
            {
                _mehediStreamCache.Remove(expired[i]);
            }

            if (!_mehediStreamCache.ContainsKey(videoId))
            {
                while (_mehediStreamCache.Count >= MaxStreamCacheEntries && _mehediStreamCacheOrder.Count > 0)
                {
                    string oldest = _mehediStreamCacheOrder.Dequeue();
                    _mehediStreamCache.Remove(oldest);
                }
                _mehediStreamCacheOrder.Enqueue(videoId);
            }

            _mehediStreamCache[videoId] = new ShihabCachedYtEntry
            {
                MehediCachedAtUtc = now,
                ShihabReply = reply
            };
        }

        private static void StoreBoundedTitleCacheLocked(string query, string videoId)
        {
            if (!_shihabTitleToVideoIdCache.ContainsKey(query))
            {
                while (_shihabTitleToVideoIdCache.Count >= MaxTitleCacheEntries && _shihabTitleCacheOrder.Count > 0)
                {
                    string oldest = _shihabTitleCacheOrder.Dequeue();
                    _shihabTitleToVideoIdCache.Remove(oldest);
                }
                _shihabTitleCacheOrder.Enqueue(query);
            }
            _shihabTitleToVideoIdCache[query] = videoId;
        }

        public static string NormalizeShihabVisitorData(string mehediRawVisitorData)
        {
            if (string.IsNullOrWhiteSpace(mehediRawVisitorData)) return "";
            string shihabCleaned = mehediRawVisitorData.Trim();
            for (int mehediPass = 0; mehediPass < 2 && shihabCleaned.IndexOf('%') >= 0; mehediPass++)
            {
                try
                {
                    shihabCleaned = Uri.UnescapeDataString(shihabCleaned);
                }
                catch
                {
                    break;
                }
            }
            return shihabCleaned.Trim();
        }

        public static string ObtainFreshShihabVisitorData(string mehediVideoId, bool shihabForceRefresh)
        {
            if (!shihabForceRefresh)
            {
                lock (_shihabCacheLock)
                {
                    if (!string.IsNullOrWhiteSpace(_mehediCachedVisitorData) &&
                        (DateTime.UtcNow - _shihabVisitorDataCachedAtUtc).TotalMinutes < 10.0)
                    {
                        return _mehediCachedVisitorData;
                    }
                }
            }

            string[] shihabProbeUrls = IsValidShihabYouTubeVideoId(mehediVideoId)
                ? new string[] { "https://www.youtube.com/watch?v=" + mehediVideoId, "https://www.youtube.com/sw.js_data" }
                : new string[] { "https://www.youtube.com/sw.js_data" };

            for (int mehediU = 0; mehediU < shihabProbeUrls.Length; mehediU++)
            {
                try
                {
                    Uri validatedUri;
                    if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(shihabProbeUrls[mehediU], true, out validatedUri))
                    {
                        continue;
                    }
                    HttpWebRequest shihabVisReq = (HttpWebRequest)WebRequest.Create(validatedUri);
                    shihabVisReq.Method = "GET";
                    shihabVisReq.Timeout = 10000;
                    shihabVisReq.ReadWriteTimeout = 10000;
                    shihabVisReq.AllowAutoRedirect = false;
                    shihabVisReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                    shihabVisReq.UserAgent = ChromeDesktopUserAgent;
                    shihabVisReq.Headers["Accept-Language"] = "en-US,en;q=0.9";

                    using (HttpWebResponse mehediVisResp = (HttpWebResponse)shihabVisReq.GetResponse())
                    using (Stream shihabVisStream = mehediVisResp.GetResponseStream())
                    {
                        string shihabBody = ReadBoundedUtf8Stream(shihabVisStream, MaxHttpResponseBytes);
                        Match mehediVisMatch = VisitorDataJsonRegex.Match(shihabBody);
                        if (!mehediVisMatch.Success)
                        {
                            mehediVisMatch = VisitorDataTokenRegex.Match(shihabBody);
                        }
                        if (mehediVisMatch.Success)
                        {
                            string shihabNormalized = NormalizeShihabVisitorData(mehediVisMatch.Groups[1].Value);
                            if (!string.IsNullOrWhiteSpace(shihabNormalized))
                            {
                                lock (_shihabCacheLock)
                                {
                                    _mehediCachedVisitorData = shihabNormalized;
                                    _shihabVisitorDataCachedAtUtc = DateTime.UtcNow;
                                }
                                return shihabNormalized;
                            }
                        }
                    }
                }
                catch
                {
                }
            }
            return "";
        }

        public static bool IsValidShihabYouTubeVideoId(string mehediCandidate)
        {
            if (string.IsNullOrWhiteSpace(mehediCandidate) || mehediCandidate.Length != 11)
            {
                return false;
            }
            for (int shihabI = 0; shihabI < mehediCandidate.Length; shihabI++)
            {
                char mehediCh = mehediCandidate[shihabI];
                bool shihabOk = (mehediCh >= 'A' && mehediCh <= 'Z') ||
                                (mehediCh >= 'a' && mehediCh <= 'z') ||
                                (mehediCh >= '0' && mehediCh <= '9') ||
                                mehediCh == '_' ||
                                mehediCh == '-';
                if (!shihabOk) return false;
            }
            return true;
        }

        public static bool TryExtractShihabYouTubeWatchVideoId(string mehediRawUrl, out string shihabVideoId)
        {
            shihabVideoId = "";
            if (string.IsNullOrWhiteSpace(mehediRawUrl)) return false;
            Uri shihabUri;
            if (!Uri.TryCreate(mehediRawUrl.Trim(), UriKind.Absolute, out shihabUri)) return false;

            string mehediHost = (shihabUri.Host ?? "").ToLowerInvariant();
            if (mehediHost == "youtu.be" || mehediHost.EndsWith(".youtu.be", StringComparison.Ordinal))
            {
                string shihabSeg = shihabUri.AbsolutePath.Trim('/');
                int mehediSlash = shihabSeg.IndexOf('/');
                if (mehediSlash > 0) shihabSeg = shihabSeg.Substring(0, mehediSlash);
                if (IsValidShihabYouTubeVideoId(shihabSeg))
                {
                    shihabVideoId = shihabSeg;
                    return true;
                }
                return false;
            }

            if (mehediHost == "youtube.com" ||
                mehediHost.EndsWith(".youtube.com", StringComparison.Ordinal) ||
                mehediHost == "youtube-nocookie.com" ||
                mehediHost.EndsWith(".youtube-nocookie.com", StringComparison.Ordinal))
            {
                string shihabVParam = MehediDynamicSegmentEngine.GetShihabUrlQueryParam(mehediRawUrl, "v");
                if (IsValidShihabYouTubeVideoId(shihabVParam))
                {
                    shihabVideoId = shihabVParam;
                    return true;
                }

                Match mehediPathMatch = WatchPathRegex.Match(shihabUri.AbsolutePath ?? "");
                if (mehediPathMatch.Success && IsValidShihabYouTubeVideoId(mehediPathMatch.Groups[1].Value))
                {
                    shihabVideoId = mehediPathMatch.Groups[1].Value;
                    return true;
                }
            }
            return false;
        }

        public static string ExtractShihabVideoIdFromItem(ShihabDownloadItem shihabItem)
        {
            if (shihabItem == null) return "";

            string mehediVid;
            if (TryExtractShihabYouTubeWatchVideoId(shihabItem.MehediUrl, out mehediVid))
            {
                return mehediVid;
            }

            if (MehediDynamicSegmentEngine.IsRealShihabGoogleVideoDomain(shihabItem.MehediUrl))
            {
                string shihabDocId = MehediDynamicSegmentEngine.GetShihabUrlQueryParam(shihabItem.MehediUrl, "docid");
                if (IsValidShihabYouTubeVideoId(shihabDocId))
                {
                    return shihabDocId;
                }
            }

            if (TryExtractShihabYouTubeWatchVideoId(shihabItem.MehediReferer, out mehediVid))
            {
                return mehediVid;
            }

            if (!string.IsNullOrWhiteSpace(shihabItem.ShihabDescription))
            {
                if (TryExtractShihabYouTubeWatchVideoId(shihabItem.ShihabDescription, out mehediVid))
                {
                    return mehediVid;
                }
                try
                {
                    Match shihabDescMatch = DescVideoIdRegex.Match(shihabItem.ShihabDescription);
                    if (shihabDescMatch.Success && IsValidShihabYouTubeVideoId(shihabDescMatch.Groups[1].Value))
                    {
                        return shihabDescMatch.Groups[1].Value;
                    }
                }
                catch { }
            }

            if (MehediDynamicSegmentEngine.IsRealShihabGoogleVideoDomain(shihabItem.MehediUrl) &&
                !string.IsNullOrWhiteSpace(shihabItem.ShihabFileName))
            {
                string mehediStem = Path.GetFileNameWithoutExtension(shihabItem.ShihabFileName);
                try
                {
                    mehediStem = BracketSuffixRegex.Replace(mehediStem, "").Trim();
                    mehediStem = TrailingIndexRegex.Replace(mehediStem, "").Trim();
                }
                catch { mehediStem = mehediStem.Trim(); }

                if (mehediStem.Length >= 6 &&
                    !mehediStem.StartsWith("videoplayback", StringComparison.OrdinalIgnoreCase) &&
                    !mehediStem.StartsWith("download", StringComparison.OrdinalIgnoreCase))
                {
                    string shihabSearchedId = SearchShihabYouTubeVideoIdByTitle(mehediStem);
                    if (IsValidShihabYouTubeVideoId(shihabSearchedId))
                    {
                        return shihabSearchedId;
                    }
                }
            }

            return "";
        }

        public static string SearchShihabYouTubeVideoIdByTitle(string mehediTitleQuery)
        {
            if (string.IsNullOrWhiteSpace(mehediTitleQuery)) return "";
            lock (_shihabCacheLock)
            {
                string shihabCached;
                if (_shihabTitleToVideoIdCache.TryGetValue(mehediTitleQuery, out shihabCached))
                {
                    return shihabCached;
                }
            }

            try
            {
                string mehediSearchUrl = "https://www.youtube.com/results?search_query=" + Uri.EscapeDataString(mehediTitleQuery) + "&sp=EgIQAQ%253D%253D";
                Uri validatedUri;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(mehediSearchUrl, true, out validatedUri))
                {
                    return "";
                }
                HttpWebRequest shihabReq = (HttpWebRequest)WebRequest.Create(validatedUri);
                shihabReq.Method = "GET";
                shihabReq.Timeout = 12000;
                shihabReq.ReadWriteTimeout = 12000;
                shihabReq.AllowAutoRedirect = false;
                shihabReq.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                shihabReq.UserAgent = ChromeDesktopUserAgent;
                shihabReq.Headers["Accept-Language"] = "en-US,en;q=0.9";

                using (HttpWebResponse mehediResp = (HttpWebResponse)shihabReq.GetResponse())
                using (Stream shihabStream = mehediResp.GetResponseStream())
                {
                    string shihabHtml = ReadBoundedUtf8Stream(shihabStream, MaxHttpResponseBytes);
                    Match mehediMatch = SearchVideoRendererRegex.Match(shihabHtml);
                    if (!mehediMatch.Success)
                    {
                        mehediMatch = SearchFallbackVideoIdRegex.Match(shihabHtml);
                    }
                    if (mehediMatch.Success && IsValidShihabYouTubeVideoId(mehediMatch.Groups[1].Value))
                    {
                        string shihabFound = mehediMatch.Groups[1].Value;
                        lock (_shihabCacheLock)
                        {
                            StoreBoundedTitleCacheLocked(mehediTitleQuery, shihabFound);
                        }
                        return shihabFound;
                    }
                }
            }
            catch
            {
            }
            return "";
        }

        public static string InferShihabResolutionFromItag(string mehediItagText)
        {
            int shihabItag;
            if (!int.TryParse(mehediItagText, out shihabItag)) return "";
            switch (shihabItag)
            {
                case 313:
                case 315:
                case 401:
                case 305:
                    return "2160p";
                case 271:
                case 308:
                case 400:
                case 304:
                    return "1440p";
                case 137:
                case 299:
                case 248:
                case 303:
                case 399:
                    return "1080p";
                case 22:
                case 136:
                case 298:
                case 247:
                case 302:
                case 398:
                    return "720p";
                case 135:
                case 244:
                case 397:
                    return "480p";
                case 18:
                case 134:
                case 243:
                case 396:
                    return "360p";
                case 133:
                case 242:
                case 395:
                    return "240p";
                case 160:
                case 278:
                case 394:
                    return "144p";
                default:
                    return "";
            }
        }

        public static ShihabYouTubeStreamsReplyDto ResolveShihabYouTubeStreams(string mehediVideoId, string shihabVisitorData)
        {
            return ResolveShihabYouTubeStreams(mehediVideoId, shihabVisitorData, false);
        }

        public static ShihabYouTubeStreamsReplyDto ResolveShihabYouTubeStreams(
            string mehediVideoId,
            string shihabVisitorData,
            bool mehediForceRefresh)
        {
            ShihabYouTubeStreamsReplyDto shihabFail = new ShihabYouTubeStreamsReplyDto
            {
                ShihabOk = false,
                MehediVideoId = mehediVideoId ?? "",
                ShihabTitle = "",
                MehediStreams = new List<MehediYouTubeResolvedStreamDto>()
            };

            if (!IsValidShihabYouTubeVideoId(mehediVideoId))
            {
                shihabFail.MehediError = "Invalid YouTube videoId.";
                return shihabFail;
            }

            if (!mehediForceRefresh)
            {
                lock (_shihabCacheLock)
                {
                    ShihabCachedYtEntry mehediHit;
                    if (_mehediStreamCache.TryGetValue(mehediVideoId, out mehediHit) &&
                        mehediHit != null &&
                        mehediHit.ShihabReply != null &&
                        (DateTime.UtcNow - mehediHit.MehediCachedAtUtc).TotalMinutes < 15.0)
                    {
                        return mehediHit.ShihabReply;
                    }
                }
            }

            string mehediEffectiveVis = NormalizeShihabVisitorData(shihabVisitorData);
            if (string.IsNullOrWhiteSpace(mehediEffectiveVis))
            {
                mehediEffectiveVis = ObtainFreshShihabVisitorData(mehediVideoId, mehediForceRefresh);
            }

            string shihabLastErr = "";
            string[] mehediClientsOrder = new string[] { "VISIONOS", "ANDROID", "IOS" };
            for (int shihabC = 0; shihabC < mehediClientsOrder.Length; shihabC++)
            {
                string mehediClientKey = mehediClientsOrder[shihabC];
                string shihabAttemptErr;
                ShihabYouTubeStreamsReplyDto mehediAttempt = QuerySingleShihabInnerTubeClient(
                    mehediVideoId,
                    mehediClientKey,
                    mehediEffectiveVis,
                    out shihabAttemptErr);

                if ((mehediAttempt == null || !mehediAttempt.ShihabOk) &&
                    string.Equals(mehediClientKey, "VISIONOS", StringComparison.OrdinalIgnoreCase))
                {
                    string shihabRefreshedVis = ObtainFreshShihabVisitorData(mehediVideoId, true);
                    if (!string.IsNullOrWhiteSpace(shihabRefreshedVis) &&
                        !string.Equals(shihabRefreshedVis, mehediEffectiveVis, StringComparison.Ordinal))
                    {
                        mehediEffectiveVis = shihabRefreshedVis;
                        mehediAttempt = QuerySingleShihabInnerTubeClient(
                            mehediVideoId,
                            mehediClientKey,
                            mehediEffectiveVis,
                            out shihabAttemptErr);
                    }
                }

                if (mehediAttempt != null && mehediAttempt.ShihabOk && mehediAttempt.MehediStreams != null && mehediAttempt.MehediStreams.Count > 0)
                {
                    lock (_shihabCacheLock)
                    {
                        StoreBoundedStreamCacheLocked(mehediVideoId, mehediAttempt);
                    }
                    return mehediAttempt;
                }
                if (!string.IsNullOrWhiteSpace(shihabAttemptErr))
                {
                    shihabLastErr = shihabAttemptErr;
                }
            }

            shihabFail.MehediError = string.IsNullOrWhiteSpace(shihabLastErr)
                ? "No unencrypted direct YouTube streams returned."
                : shihabLastErr;
            return shihabFail;
        }

        private static ShihabYouTubeStreamsReplyDto QuerySingleShihabInnerTubeClient(
            string mehediVideoId,
            string shihabClientKind,
            string mehediVisitorData,
            out string shihabErrorOut)
        {
            shihabErrorOut = "";
            try
            {
                string shihabCleanVis = NormalizeShihabVisitorData(mehediVisitorData);
                bool shihabIsVisionOs = string.Equals(shihabClientKind, "VISIONOS", StringComparison.OrdinalIgnoreCase);
                bool mehediIsIos = string.Equals(shihabClientKind, "IOS", StringComparison.OrdinalIgnoreCase);

                string shihabUa = shihabIsVisionOs
                    ? ShihabVisionOsUserAgent
                    : (mehediIsIos ? MehediIosUserAgent : ShihabAndroidUserAgent);
                string mehediClientVer = shihabIsVisionOs
                    ? ShihabVisionOsClientVersion
                    : (mehediIsIos ? MehediIosClientVersion : ShihabAndroidClientVersion);
                string shihabClientNameHeader = shihabIsVisionOs
                    ? "101"
                    : (mehediIsIos ? "5" : "3");

                ShihabYtClientDto mehediClientObj;
                if (shihabIsVisionOs)
                {
                    mehediClientObj = new ShihabYtClientDto
                    {
                        ShihabClientName = "VISIONOS",
                        MehediClientVersion = mehediClientVer,
                        MehediDeviceMake = "Apple",
                        ShihabDeviceModel = "RealityDevice17,1",
                        MehediOsName = "visionOS",
                        ShihabOsVersion = "26.5.23O471",
                        MehediUserAgent = shihabUa,
                        ShihabHl = "en",
                        ShihabUtcOffsetMinutes = 0,
                        MehediVisitorData = string.IsNullOrWhiteSpace(shihabCleanVis) ? null : shihabCleanVis
                    };
                }
                else if (mehediIsIos)
                {
                    mehediClientObj = new ShihabYtClientDto
                    {
                        ShihabClientName = "IOS",
                        MehediClientVersion = mehediClientVer,
                        MehediDeviceMake = "Apple",
                        ShihabDeviceModel = "iPhone16,2",
                        MehediOsName = "iPhone",
                        ShihabOsVersion = "18.3.2.22D82",
                        MehediUserAgent = shihabUa,
                        ShihabHl = "en",
                        ShihabUtcOffsetMinutes = 0,
                        MehediVisitorData = string.IsNullOrWhiteSpace(shihabCleanVis) ? null : shihabCleanVis
                    };
                }
                else
                {
                    mehediClientObj = new ShihabYtClientDto
                    {
                        ShihabClientName = "ANDROID",
                        MehediClientVersion = mehediClientVer,
                        ShihabAndroidSdkVersion = 34,
                        MehediOsName = "Android",
                        ShihabOsVersion = "14",
                        MehediUserAgent = shihabUa,
                        ShihabHl = "en",
                        ShihabUtcOffsetMinutes = 0,
                        MehediVisitorData = string.IsNullOrWhiteSpace(shihabCleanVis) ? null : shihabCleanVis
                    };
                }

                ShihabYtPlayerRequestDto shihabPayload = new ShihabYtPlayerRequestDto
                {
                    ShihabVideoId = mehediVideoId,
                    MehediRacyCheckOk = true,
                    ShihabContentCheckOk = true,
                    MehediContext = new MehediYtContextDto { ShihabClient = mehediClientObj },
                    ShihabPlaybackContext = new MehediYtPlaybackContextDto
                    {
                        MehediContentPlaybackContext = new ShihabYtContentPlaybackContextDto
                        {
                            ShihabHtml5Preference = "HTML5_PREF_WANTS"
                        }
                    }
                };

                byte[] mehediBodyBytes;
                using (MemoryStream shihabMs = new MemoryStream())
                {
                    DataContractJsonSerializer mehediReqSer = new DataContractJsonSerializer(typeof(ShihabYtPlayerRequestDto));
                    mehediReqSer.WriteObject(shihabMs, shihabPayload);
                    mehediBodyBytes = shihabMs.ToArray();
                }

                HttpWebRequest shihabHttp = (HttpWebRequest)WebRequest.Create("https://www.youtube.com/youtubei/v1/player?prettyPrint=false");
                shihabHttp.Method = "POST";
                shihabHttp.Timeout = 15000;
                shihabHttp.ReadWriteTimeout = 15000;
                shihabHttp.AllowAutoRedirect = false;
                shihabHttp.ContentType = "application/json";
                shihabHttp.Accept = "application/json";
                shihabHttp.UserAgent = shihabUa;
                shihabHttp.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
                shihabHttp.Headers["X-YouTube-Client-Name"] = shihabClientNameHeader;
                shihabHttp.Headers["X-YouTube-Client-Version"] = mehediClientVer;
                shihabHttp.Headers["X-Goog-Api-Format-Version"] = "2";
                if (!string.IsNullOrWhiteSpace(shihabCleanVis))
                {
                    shihabHttp.Headers["X-Goog-Visitor-Id"] = shihabCleanVis;
                }
                shihabHttp.ContentLength = mehediBodyBytes.Length;

                using (Stream mehediReqStream = shihabHttp.GetRequestStream())
                {
                    mehediReqStream.Write(mehediBodyBytes, 0, mehediBodyBytes.Length);
                }

                MehediYtPlayerResponseDto mehediParsedResp;
                using (HttpWebResponse shihabResp = (HttpWebResponse)shihabHttp.GetResponse())
                using (Stream mehediRespStream = shihabResp.GetResponseStream())
                {
                    DataContractJsonSerializer shihabRespSer = new DataContractJsonSerializer(typeof(MehediYtPlayerResponseDto));
                    mehediParsedResp = shihabRespSer.ReadObject(mehediRespStream) as MehediYtPlayerResponseDto;
                }

                if (mehediParsedResp == null)
                {
                    shihabErrorOut = shihabClientKind + " player returned empty JSON.";
                    return null;
                }

                string shihabPlayStatus = mehediParsedResp.ShihabPlayabilityStatus != null
                    ? (mehediParsedResp.ShihabPlayabilityStatus.ShihabStatus ?? "")
                    : "";
                if (!string.Equals(shihabPlayStatus, "OK", StringComparison.OrdinalIgnoreCase))
                {
                    string mehediReason = mehediParsedResp.ShihabPlayabilityStatus != null
                        ? (mehediParsedResp.ShihabPlayabilityStatus.MehediReason ?? shihabPlayStatus)
                        : "Unplayable";
                    shihabErrorOut = shihabClientKind + ": " + mehediReason;
                    return null;
                }

                if (mehediParsedResp.ShihabStreamingData == null)
                {
                    shihabErrorOut = shihabClientKind + ": missing streamingData.";
                    return null;
                }

                List<MehediYtFormatDto> shihabRawFormats = new List<MehediYtFormatDto>();
                if (mehediParsedResp.ShihabStreamingData.ShihabFormats != null)
                {
                    shihabRawFormats.AddRange(mehediParsedResp.ShihabStreamingData.ShihabFormats);
                }
                if (mehediParsedResp.ShihabStreamingData.MehediAdaptiveFormats != null)
                {
                    shihabRawFormats.AddRange(mehediParsedResp.ShihabStreamingData.MehediAdaptiveFormats);
                }

                bool mehediHasExplicitDefaultAudio = false;
                for (int shihabI = 0; shihabI < shihabRawFormats.Count; shihabI++)
                {
                    MehediYtFormatDto mehediF = shihabRawFormats[shihabI];
                    if (mehediF != null && mehediF.MehediAudioTrack != null && mehediF.MehediAudioTrack.ShihabAudioIsDefault)
                    {
                        mehediHasExplicitDefaultAudio = true;
                        break;
                    }
                }

                int shihabDurationSec = 0;
                if (mehediParsedResp.MehediVideoDetails != null && !string.IsNullOrWhiteSpace(mehediParsedResp.MehediVideoDetails.ShihabLengthSeconds))
                {
                    int.TryParse(mehediParsedResp.MehediVideoDetails.ShihabLengthSeconds, out shihabDurationSec);
                }

                List<MehediYouTubeResolvedStreamDto> mehediCandidateStreams = new List<MehediYouTubeResolvedStreamDto>();
                for (int shihabI = 0; shihabI < shihabRawFormats.Count; shihabI++)
                {
                    MehediYtFormatDto mehediFmt = shihabRawFormats[shihabI];
                    if (mehediFmt == null) continue;
                    if (mehediFmt.ShihabDrmFamilies != null && mehediFmt.ShihabDrmFamilies.Count > 0) continue;
                    if (string.IsNullOrWhiteSpace(mehediFmt.ShihabUrl)) continue;
                    if (!string.IsNullOrWhiteSpace(mehediFmt.MehediSignatureCipher) || !string.IsNullOrWhiteSpace(mehediFmt.ShihabCipher)) continue;
                    Uri formatUri;
                    if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(mehediFmt.ShihabUrl, false, out formatUri)) continue;
                    if (MehediDynamicSegmentEngine.IsPoisonedWebShihabGoogleVideoUrl(mehediFmt.ShihabUrl)) continue;

                    if (mehediHasExplicitDefaultAudio && mehediFmt.MehediAudioTrack != null && !mehediFmt.MehediAudioTrack.ShihabAudioIsDefault)
                    {
                        continue;
                    }

                    string shihabMimeRaw = mehediFmt.MehediMimeType ?? "";
                    string mehediBaseMime = shihabMimeRaw.Split(';')[0].Trim().ToLowerInvariant();
                    string shihabCodecs = "";
                    Match mehediCodecMatch = MimeCodecsRegex.Match(shihabMimeRaw);
                    if (mehediCodecMatch.Success)
                    {
                        shihabCodecs = mehediCodecMatch.Groups[1].Value;
                    }

                    bool mehediIsAudioMime = mehediBaseMime.StartsWith("audio/", StringComparison.Ordinal);
                    bool shihabIsVideoMime = mehediBaseMime.StartsWith("video/", StringComparison.Ordinal);
                    if (!mehediIsAudioMime && !shihabIsVideoMime) continue;

                    bool shihabHasVideo = shihabIsVideoMime;
                    bool mehediHasAudio = mehediIsAudioMime ||
                                          mehediFmt.ShihabAudioChannels > 0 ||
                                          !string.IsNullOrWhiteSpace(mehediFmt.MehediAudioSampleRate) ||
                                          shihabCodecs.IndexOf("mp4a", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          shihabCodecs.IndexOf("opus", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          shihabCodecs.IndexOf("vorbis", StringComparison.OrdinalIgnoreCase) >= 0;

                    string shihabExt = "mp4";
                    if (mehediBaseMime.IndexOf("webm", StringComparison.Ordinal) >= 0)
                    {
                        shihabExt = "webm";
                    }
                    else if (mehediIsAudioMime)
                    {
                        shihabExt = "m4a";
                    }
                    else if (mehediBaseMime.IndexOf("3gpp", StringComparison.Ordinal) >= 0)
                    {
                        continue;
                    }

                    long mehediClen = -1L;
                    if (!string.IsNullOrWhiteSpace(mehediFmt.ShihabContentLength))
                    {
                        long.TryParse(mehediFmt.ShihabContentLength, out mehediClen);
                    }
                    long mehediQueryClen;
                    string shihabCleanUrl = MehediDynamicSegmentEngine.SanitizeMehediGoogleVideoUrl(mehediFmt.ShihabUrl, out mehediQueryClen);
                    if (mehediClen <= 0 && mehediQueryClen > 0)
                    {
                        mehediClen = mehediQueryClen;
                    }
                    long shihabBitrate = mehediFmt.MehediAverageBitrate > 0 ? mehediFmt.MehediAverageBitrate : mehediFmt.ShihabBitrate;
                    if (mehediClen <= 0 && shihabBitrate > 0 && shihabDurationSec > 0)
                    {
                        mehediClen = (shihabBitrate * (long)shihabDurationSec) / 8L;
                    }

                    string mehediRes = "";
                    if (shihabHasVideo)
                    {
                        if (!string.IsNullOrWhiteSpace(mehediFmt.MehediQualityLabel))
                        {
                            Match shihabQlMatch = QualityLabelPRegex.Match(mehediFmt.MehediQualityLabel.Trim());
                            mehediRes = shihabQlMatch.Success ? shihabQlMatch.Groups[1].Value : mehediFmt.MehediQualityLabel.Trim();
                        }
                        else if (mehediFmt.MehediHeight > 0)
                        {
                            int shihabShortSide = mehediFmt.ShihabWidth > 0 ? Math.Min(mehediFmt.ShihabWidth, mehediFmt.MehediHeight) : mehediFmt.MehediHeight;
                            mehediRes = shihabShortSide + "p";
                        }
                    }

                    bool shihabIsOrigAud = false;
                    if (mehediFmt.MehediAudioTrack != null)
                    {
                        string mehediDisp = mehediFmt.MehediAudioTrack.ShihabDisplayName ?? "";
                        shihabIsOrigAud = mehediFmt.MehediAudioTrack.ShihabAudioIsDefault ||
                                          mehediDisp.IndexOf("original", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    else if (mehediIsAudioMime && !mehediHasExplicitDefaultAudio)
                    {
                        shihabIsOrigAud = true;
                    }

                    string shihabXtags = MehediDynamicSegmentEngine.GetShihabUrlQueryParam(shihabCleanUrl, "xtags");
                    string mehediTrackIdStr = mehediFmt.MehediAudioTrack != null ? (mehediFmt.MehediAudioTrack.MehediId ?? "") : "";
                    bool shihabIsDrc =
                        (!string.IsNullOrEmpty(shihabXtags) && shihabXtags.IndexOf("drc=1", StringComparison.OrdinalIgnoreCase) >= 0) ||
                        (!string.IsNullOrEmpty(mehediTrackIdStr) && mehediTrackIdStr.IndexOf("drc", StringComparison.OrdinalIgnoreCase) >= 0);

                    bool mehediIsHdr = false;
                    if (mehediFmt.ShihabColorInfo != null && !string.IsNullOrWhiteSpace(mehediFmt.ShihabColorInfo.ShihabTransferCharacteristics))
                    {
                        string shihabTc = mehediFmt.ShihabColorInfo.ShihabTransferCharacteristics;
                        mehediIsHdr = shihabTc.IndexOf("SMPTEST2084", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                      shihabTc.IndexOf("ARIB_STD_B67", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    if (!string.IsNullOrWhiteSpace(mehediFmt.MehediQualityLabel) &&
                        mehediFmt.MehediQualityLabel.IndexOf("HDR", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        mehediIsHdr = true;
                    }

                    string shihabRole = (shihabHasVideo && mehediHasAudio)
                        ? "progressive"
                        : (shihabHasVideo ? "video_only" : "audio");

                    mehediCandidateStreams.Add(new MehediYouTubeResolvedStreamDto
                    {
                        ShihabItag = mehediFmt.ShihabItag,
                        ShihabUrl = shihabCleanUrl,
                        MehediMimeType = mehediBaseMime,
                        ShihabCodecs = shihabCodecs,
                        ShihabExt = shihabExt,
                        MehediWidth = mehediFmt.ShihabWidth,
                        ShihabHeight = mehediFmt.MehediHeight,
                        MehediFps = mehediFmt.ShihabFps,
                        ShihabResolution = mehediRes,
                        MehediTrackRole = shihabRole,
                        ShihabHasVideo = shihabHasVideo,
                        MehediHasAudio = mehediHasAudio,
                        ShihabSizeBytes = mehediClen,
                        MehediBitrate = shihabBitrate,
                        ShihabIsOriginalAudio = shihabIsOrigAud,
                        MehediIsDrcAudio = shihabIsDrc,
                        MehediIsHdr = mehediIsHdr,
                        MehediUserAgent = shihabUa,
                        MehediAudioCompanionUrl = "",
                        ShihabAudioCompanionBytes = -1L
                    });
                }

                if (mehediCandidateStreams.Count == 0)
                {
                    shihabErrorOut = shihabClientKind + ": zero direct formats available.";
                    return null;
                }

                MehediYouTubeResolvedStreamDto shihabBestM4aAudio = SelectBestShihabAudioByExt(mehediCandidateStreams, "m4a");
                MehediYouTubeResolvedStreamDto mehediBestWebmAudio = SelectBestShihabAudioByExt(mehediCandidateStreams, "webm");

                Dictionary<string, MehediYouTubeResolvedStreamDto> shihabBestVideoByRes = new Dictionary<string, MehediYouTubeResolvedStreamDto>(StringComparer.OrdinalIgnoreCase);
                for (int mehediI = 0; mehediI < mehediCandidateStreams.Count; mehediI++)
                {
                    MehediYouTubeResolvedStreamDto shihabCand = mehediCandidateStreams[mehediI];
                    if (!shihabCand.ShihabHasVideo) continue;
                    string mehediKey = string.IsNullOrWhiteSpace(shihabCand.ShihabResolution)
                        ? ("itag_" + shihabCand.ShihabItag)
                        : shihabCand.ShihabResolution;

                    if (!shihabCand.MehediHasAudio)
                    {
                        MehediYouTubeResolvedStreamDto shihabAudMatch = string.Equals(shihabCand.ShihabExt, "mp4", StringComparison.OrdinalIgnoreCase)
                            ? (shihabBestM4aAudio ?? mehediBestWebmAudio)
                            : (mehediBestWebmAudio ?? shihabBestM4aAudio);
                        if (shihabAudMatch != null)
                        {
                            shihabCand.MehediAudioCompanionUrl = shihabAudMatch.ShihabUrl;
                            shihabCand.ShihabAudioCompanionBytes = shihabAudMatch.ShihabSizeBytes;
                        }
                        else
                        {
                            continue;
                        }
                    }

                    MehediYouTubeResolvedStreamDto mehediCurrentBest;
                    if (!shihabBestVideoByRes.TryGetValue(mehediKey, out mehediCurrentBest) ||
                        CompareShihabVideoFormatPreference(shihabCand, mehediCurrentBest) > 0)
                    {
                        shihabBestVideoByRes[mehediKey] = shihabCand;
                    }
                }

                List<MehediYouTubeResolvedStreamDto> mehediFinalStreams = new List<MehediYouTubeResolvedStreamDto>(shihabBestVideoByRes.Values);
                mehediFinalStreams.Sort(delegate(MehediYouTubeResolvedStreamDto shihabA, MehediYouTubeResolvedStreamDto mehediB)
                {
                    int shihabHa = ParseMehediResolutionNumber(shihabA.ShihabResolution);
                    int mehediHb = ParseMehediResolutionNumber(mehediB.ShihabResolution);
                    if (shihabHa != mehediHb) return mehediHb.CompareTo(shihabHa);
                    return mehediB.MehediBitrate.CompareTo(shihabA.MehediBitrate);
                });

                if (shihabBestM4aAudio != null)
                {
                    mehediFinalStreams.Add(shihabBestM4aAudio);
                }
                else if (mehediBestWebmAudio != null)
                {
                    mehediFinalStreams.Add(mehediBestWebmAudio);
                }

                string shihabCleanTitle = mehediParsedResp.MehediVideoDetails != null
                    ? (mehediParsedResp.MehediVideoDetails.MehediTitle ?? "")
                    : "";

                return new ShihabYouTubeStreamsReplyDto
                {
                    ShihabOk = mehediFinalStreams.Count > 0,
                    MehediVideoId = mehediVideoId,
                    ShihabTitle = shihabCleanTitle,
                    MehediDurationSec = shihabDurationSec,
                    ShihabClientUsed = shihabIsVisionOs ? "VISIONOS" : (mehediIsIos ? "IOS" : "ANDROID"),
                    MehediError = "",
                    MehediStreams = mehediFinalStreams
                };
            }
            catch (WebException wex)
            {
                if (wex.Response != null)
                {
                    try { wex.Response.Close(); } catch { }
                }
                shihabErrorOut = shihabClientKind + " exception: " + wex.Message;
                return null;
            }
            catch (Exception shihabEx)
            {
                shihabErrorOut = shihabClientKind + " exception: " + shihabEx.Message;
                return null;
            }
        }

        private static MehediYouTubeResolvedStreamDto SelectBestShihabAudioByExt(
            List<MehediYouTubeResolvedStreamDto> shihabAll,
            string mehediTargetExt)
        {
            MehediYouTubeResolvedStreamDto shihabBest = null;
            for (int mehediI = 0; mehediI < shihabAll.Count; mehediI++)
            {
                MehediYouTubeResolvedStreamDto shihabCand = shihabAll[mehediI];
                if (shihabCand.ShihabHasVideo || !shihabCand.MehediHasAudio) continue;
                if (!string.Equals(shihabCand.ShihabExt, mehediTargetExt, StringComparison.OrdinalIgnoreCase)) continue;
                if (shihabBest == null)
                {
                    shihabBest = shihabCand;
                    continue;
                }
                if (shihabCand.ShihabIsOriginalAudio != shihabBest.ShihabIsOriginalAudio)
                {
                    if (shihabCand.ShihabIsOriginalAudio) shihabBest = shihabCand;
                    continue;
                }
                if (shihabCand.ShihabItag == 140 && shihabBest.ShihabItag != 140)
                {
                    shihabBest = shihabCand;
                    continue;
                }
                if (shihabBest.ShihabItag == 140 && shihabCand.ShihabItag != 140)
                {
                    continue;
                }
                if (shihabCand.ShihabItag == 251 && shihabBest.ShihabItag != 251)
                {
                    shihabBest = shihabCand;
                    continue;
                }
                if (shihabBest.ShihabItag == 251 && shihabCand.ShihabItag != 251)
                {
                    continue;
                }
                if (shihabCand.MehediIsDrcAudio != shihabBest.MehediIsDrcAudio)
                {
                    if (!shihabCand.MehediIsDrcAudio) shihabBest = shihabCand;
                    continue;
                }
                if (shihabCand.MehediBitrate > shihabBest.MehediBitrate)
                {
                    shihabBest = shihabCand;
                }
            }
            return shihabBest;
        }

        private static int CompareShihabVideoFormatPreference(
            MehediYouTubeResolvedStreamDto shihabCand,
            MehediYouTubeResolvedStreamDto mehediExisting)
        {
            if (shihabCand.MehediIsHdr != mehediExisting.MehediIsHdr)
            {
                return shihabCand.MehediIsHdr ? -1 : 1;
            }

            bool shihabCandMp4 = string.Equals(shihabCand.ShihabExt, "mp4", StringComparison.OrdinalIgnoreCase);
            bool mehediExistMp4 = string.Equals(mehediExisting.ShihabExt, "mp4", StringComparison.OrdinalIgnoreCase);
            if (shihabCandMp4 != mehediExistMp4)
            {
                return shihabCandMp4 ? 1 : -1;
            }

            bool shihabCandAvc = (shihabCand.ShihabCodecs ?? "").IndexOf("avc1", StringComparison.OrdinalIgnoreCase) >= 0;
            bool mehediExistAvc = (mehediExisting.ShihabCodecs ?? "").IndexOf("avc1", StringComparison.OrdinalIgnoreCase) >= 0;
            if (shihabCandAvc != mehediExistAvc)
            {
                return shihabCandAvc ? 1 : -1;
            }

            if (shihabCand.ShihabItag != 18 && mehediExisting.ShihabItag == 18 && !string.IsNullOrWhiteSpace(shihabCand.MehediAudioCompanionUrl))
            {
                return 1;
            }
            if (shihabCand.ShihabItag == 18 && mehediExisting.ShihabItag != 18 && !string.IsNullOrWhiteSpace(mehediExisting.MehediAudioCompanionUrl))
            {
                return -1;
            }

            if (shihabCand.MehediFps != mehediExisting.MehediFps)
            {
                return shihabCand.MehediFps.CompareTo(mehediExisting.MehediFps);
            }
            return shihabCand.MehediBitrate.CompareTo(mehediExisting.MehediBitrate);
        }

        public static int ParseMehediResolutionNumber(string shihabRes)
        {
            if (string.IsNullOrWhiteSpace(shihabRes)) return 0;
            Match mehediMatch = ResolutionNumRegex.Match(shihabRes);
            int shihabVal;
            if (mehediMatch.Success && int.TryParse(mehediMatch.Groups[1].Value, out shihabVal))
            {
                return shihabVal;
            }
            return 0;
        }

        public static void SelectBestShihabStreamPair(
            ShihabYouTubeStreamsReplyDto mehediReply,
            string shihabDesiredQuality,
            bool mehediWantsAudioOnly,
            out MehediYouTubeResolvedStreamDto shihabSelectedPrimary,
            out MehediYouTubeResolvedStreamDto mehediSelectedCompanionAudio)
        {
            shihabSelectedPrimary = null;
            mehediSelectedCompanionAudio = null;
            if (mehediReply == null || mehediReply.MehediStreams == null || mehediReply.MehediStreams.Count == 0)
            {
                return;
            }

            MehediYouTubeResolvedStreamDto shihabAudioDto = null;
            for (int mehediI = 0; mehediI < mehediReply.MehediStreams.Count; mehediI++)
            {
                MehediYouTubeResolvedStreamDto shihabS = mehediReply.MehediStreams[mehediI];
                if (!shihabS.ShihabHasVideo && shihabS.MehediHasAudio)
                {
                    shihabAudioDto = shihabS;
                    break;
                }
            }

            if (mehediWantsAudioOnly)
            {
                shihabSelectedPrimary = shihabAudioDto;
                return;
            }

            int mehediDesiredHeight = ParseMehediResolutionNumber(shihabDesiredQuality);
            MehediYouTubeResolvedStreamDto shihabBestVideo = null;

            for (int mehediI = 0; mehediI < mehediReply.MehediStreams.Count; mehediI++)
            {
                MehediYouTubeResolvedStreamDto shihabVid = mehediReply.MehediStreams[mehediI];
                if (!shihabVid.ShihabHasVideo) continue;

                if (mehediDesiredHeight > 0)
                {
                    int shihabH = ParseMehediResolutionNumber(shihabVid.ShihabResolution);
                    if (shihabH == mehediDesiredHeight)
                    {
                        shihabBestVideo = shihabVid;
                        break;
                    }
                }
                else
                {
                    int shihabH = ParseMehediResolutionNumber(shihabVid.ShihabResolution);
                    if (shihabBestVideo == null)
                    {
                        shihabBestVideo = shihabVid;
                    }
                    if (shihabH == 1080)
                    {
                        shihabBestVideo = shihabVid;
                        break;
                    }
                }
            }

            if (shihabBestVideo == null)
            {
                for (int mehediI = 0; mehediI < mehediReply.MehediStreams.Count; mehediI++)
                {
                    if (mehediReply.MehediStreams[mehediI].ShihabHasVideo)
                    {
                        shihabBestVideo = mehediReply.MehediStreams[mehediI];
                        break;
                    }
                }
            }

            shihabSelectedPrimary = shihabBestVideo;
            if (shihabBestVideo != null && !shihabBestVideo.MehediHasAudio)
            {
                if (!string.IsNullOrWhiteSpace(shihabBestVideo.MehediAudioCompanionUrl))
                {
                    mehediSelectedCompanionAudio = new MehediYouTubeResolvedStreamDto
                    {
                        ShihabUrl = shihabBestVideo.MehediAudioCompanionUrl,
                        ShihabSizeBytes = shihabBestVideo.ShihabAudioCompanionBytes,
                        MehediHasAudio = true,
                        ShihabHasVideo = false,
                        MehediUserAgent = shihabBestVideo.MehediUserAgent
                    };
                }
                else
                {
                    mehediSelectedCompanionAudio = shihabAudioDto;
                }
            }
        }
    }
}

