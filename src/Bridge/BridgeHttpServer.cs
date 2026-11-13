using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public partial class MehediBrowserBridgeServer : IDisposable
    {
        private const int MaxHttpHeaderBytes = 65536;
        private const int MaxJsonBodyBytes = 4 * 1024 * 1024;
        private const int MaxRelayBinaryBodyBytes = 32 * 1024 * 1024;

        private static readonly Regex ChromeOriginRegex = new Regex(
            @"^chrome-extension://([a-p]{32})$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex FirefoxOriginRegex = new Regex(
            @"^moz-extension://([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly ShihabStateStore _shihabStore;
        private readonly MehediAppConfiguration _shihabConfig;
        private readonly MehediDynamicSegmentEngine _mehediEngine;
        private TcpListener _shihabTcpListener;
        private CancellationTokenSource _mehediStopCts;
        private volatile bool _shihabIsRunning;

        private readonly object _pinnedOriginsLock = new object();
        private HashSet<string> _pinnedChromiumExtensionIds;
        private DateTime _pinnedIdsLastLoadedUtc = DateTime.MinValue;

        public event Action<ShihabCapturedLinkDto> OnShihabSingleCaptureReceived;
        public event Action<ShihabBatchCaptureDto> OnMehediBatchCaptureReceived;
        public event Action OnShihabStartMainQueueRequested;
        public event Action OnShihabShowMainWindowRequested;
        public event Action OnShihabOpenUpdaterRequested;
        public event Action OnShihabGracefulExitRequested;

        public MehediBrowserBridgeServer(MehediAppConfiguration shihabConfig, MehediDynamicSegmentEngine mehediEngine)
            : this(new ShihabStateStore(null), shihabConfig, mehediEngine)
        {
        }

        public MehediBrowserBridgeServer(ShihabStateStore shihabStore, MehediAppConfiguration shihabConfig, MehediDynamicSegmentEngine mehediEngine)
        {
            _shihabStore = shihabStore ?? new ShihabStateStore(null);
            _shihabConfig = shihabConfig;
            _mehediEngine = mehediEngine;
        }

        private string GetBridgeSecretToken()
        {
            return _shihabStore != null ? _shihabStore.LoadOrCreateBridgeToken() : "";
        }

        private static bool HasControlOrLineBreak(string value)
        {
            if (string.IsNullOrEmpty(value)) return false;
            for (int i = 0; i < value.Length; i++)
            {
                char ch = value[i];
                if (ch < 0x20 || ch == 0x7F) return true;
            }
            return false;
        }

        private static bool IsLoopbackHostHeader(string hostHeader, bool requirePresent)
        {
            if (string.IsNullOrWhiteSpace(hostHeader))
            {
                return !requirePresent;
            }
            string raw = hostHeader.Trim();
            if (HasControlOrLineBreak(raw)) return false;

            string hostOnly = raw;
            if (raw.StartsWith("[", StringComparison.Ordinal))
            {
                int closeIdx = raw.IndexOf(']');
                if (closeIdx <= 0) return false;
                hostOnly = raw.Substring(0, closeIdx + 1);
            }
            else
            {
                int firstColon = raw.IndexOf(':');
                int lastColon = raw.LastIndexOf(':');
                if (firstColon > 0 && firstColon == lastColon)
                {
                    hostOnly = raw.Substring(0, firstColon);
                }
            }

            return string.Equals(hostOnly, "127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(hostOnly, "localhost", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(hostOnly, "[::1]", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(hostOnly, "::1", StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryParseValidExtensionOrigin(
            string originHeader,
            out string normalizedOrigin,
            out bool isChromiumOrigin,
            out string chromiumExtensionId)
        {
            normalizedOrigin = "";
            isChromiumOrigin = false;
            chromiumExtensionId = "";
            if (string.IsNullOrWhiteSpace(originHeader)) return false;

            string trimmed = originHeader.Trim();
            if (HasControlOrLineBreak(trimmed)) return false;

            Match chromeMatch = ChromeOriginRegex.Match(trimmed);
            if (chromeMatch.Success)
            {
                isChromiumOrigin = true;
                chromiumExtensionId = chromeMatch.Groups[1].Value.ToLowerInvariant();
                normalizedOrigin = "chrome-extension://" + chromiumExtensionId;
                return true;
            }

            Match firefoxMatch = FirefoxOriginRegex.Match(trimmed);
            if (firefoxMatch.Success)
            {
                normalizedOrigin = "moz-extension://" + firefoxMatch.Groups[1].Value.ToLowerInvariant();
                return true;
            }

            return false;
        }

        private HashSet<string> GetPinnedChromiumExtensionIds()
        {
            lock (_pinnedOriginsLock)
            {
                if (_pinnedChromiumExtensionIds != null && (DateTime.UtcNow - _pinnedIdsLastLoadedUtc).TotalSeconds < 10.0)
                {
                    return _pinnedChromiumExtensionIds;
                }

                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    string exePath = Process.GetCurrentProcess().MainModule.FileName;
                    string exeDir = Path.GetDirectoryName(exePath);
                    if (!string.IsNullOrEmpty(exeDir))
                    {
                        string directExtDir = Path.Combine(exeDir, "extension");
                        if (Directory.Exists(directExtDir))
                        {
                            ids.Add(ShihabProcessAndPathSafety.ComputeChromiumUnpackedExtensionId(directExtDir));
                        }
                        string parentDir = Path.GetDirectoryName(exeDir);
                        if (!string.IsNullOrEmpty(parentDir))
                        {
                            string siblingExtDir = Path.Combine(parentDir, "extension");
                            if (Directory.Exists(siblingExtDir))
                            {
                                ids.Add(ShihabProcessAndPathSafety.ComputeChromiumUnpackedExtensionId(siblingExtDir));
                            }
                        }
                    }
                }
                catch { }

                TryCollectAllowedOriginIdsFromManifest(
                    Path.Combine(
                        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShihabMehediIDM"),
                        "com.shihab.mehedi.idm.json"),
                    ids);
                if (_shihabStore != null && !string.IsNullOrWhiteSpace(_shihabStore.MehediBaseDirectory))
                {
                    TryCollectAllowedOriginIdsFromManifest(
                        Path.Combine(_shihabStore.MehediBaseDirectory, "com.shihab.mehedi.idm.json"),
                        ids);
                }

                _pinnedChromiumExtensionIds = ids;
                _pinnedIdsLastLoadedUtc = DateTime.UtcNow;
                return ids;
            }
        }

        private static void TryCollectAllowedOriginIdsFromManifest(string manifestPath, HashSet<string> targetIds)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(manifestPath) || !File.Exists(manifestPath)) return;
                string raw = File.ReadAllText(manifestPath, Encoding.UTF8);
                Dictionary<string, object> obj = ShihabJsonCodec.ParseObjectOrNull(raw);
                if (obj == null) return;
                List<string> origins = ShihabJsonCodec.GetStringArray(obj, "allowed_origins");
                for (int i = 0; i < origins.Count; i++)
                {
                    string entry = (origins[i] ?? "").Trim().TrimEnd('/');
                    Match m = ChromeOriginRegex.Match(entry);
                    if (m.Success)
                    {
                        targetIds.Add(m.Groups[1].Value.ToLowerInvariant());
                    }
                }
            }
            catch { }
        }

        private bool IsValidBridgeToken(string providedToken)
        {
            if (string.IsNullOrWhiteSpace(providedToken) || HasControlOrLineBreak(providedToken)) return false;
            string expected = GetBridgeSecretToken();
            if (string.IsNullOrWhiteSpace(expected)) return false;
            string candidate = providedToken.Trim();
            if (candidate.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                candidate = candidate.Substring(7).Trim();
            }
            return ShihabProcessAndPathSafety.FixedTimeEqualsAscii(candidate, expected);
        }

        public bool StartShihabBridge()
        {
            try
            {
                int mehediPort = _shihabConfig.MehediLocalBridgePort > 0 ? _shihabConfig.MehediLocalBridgePort : 45779;
                _shihabTcpListener = new TcpListener(IPAddress.Loopback, mehediPort);
                _shihabTcpListener.Start();
                _shihabIsRunning = true;
                _mehediStopCts = new CancellationTokenSource();
                CancellationToken shihabTok = _mehediStopCts.Token;

                Task.Run(delegate
                {
                    AcceptMehediRequestsLoop(shihabTok);
                });
                return true;
            }
            catch
            {
                return false;
            }
        }

        public bool RestartShihabBridge()
        {
            try
            {
                _shihabIsRunning = false;
                if (_mehediStopCts != null)
                {
                    try { _mehediStopCts.Cancel(); } catch { }
                }
                if (_shihabTcpListener != null)
                {
                    try { _shihabTcpListener.Stop(); } catch { }
                    _shihabTcpListener = null;
                }
                Thread.Sleep(80);
            }
            catch
            {
            }
            return StartShihabBridge();
        }

        private void AcceptMehediRequestsLoop(CancellationToken shihabToken)
        {
            while (!shihabToken.IsCancellationRequested && _shihabIsRunning && _shihabTcpListener != null)
            {
                TcpClient mehediClient = null;
                try
                {
                    mehediClient = _shihabTcpListener.AcceptTcpClient();
                    TcpClient shihabCaptured = mehediClient;
                    Task.Run(delegate { HandleShihabHttpClient(shihabCaptured); });
                }
                catch
                {
                    try { if (mehediClient != null) mehediClient.Close(); } catch { }
                    if (!_shihabIsRunning || shihabToken.IsCancellationRequested) break;
                    Thread.Sleep(50);
                }
            }
        }

        private void HandleShihabHttpClient(TcpClient mehediClient)
        {
            using (mehediClient)
            {
                try
                {
                    mehediClient.ReceiveTimeout = 20000;
                    mehediClient.SendTimeout = 20000;
                    using (NetworkStream shihabStream = mehediClient.GetStream())
                    using (BufferedStream bufferedRead = new BufferedStream(shihabStream, 8192))
                    {
                        string mehediMethod;
                        string shihabPath;
                        string hostHeader;
                        string originHeader;
                        string bridgeTokenHeader;
                        int contentLength;

                        if (!ReadParsedHttpHeaders(
                            bufferedRead,
                            out mehediMethod,
                            out shihabPath,
                            out hostHeader,
                            out originHeader,
                            out bridgeTokenHeader,
                            out contentLength))
                        {
                            return;
                        }

                        bool hasOrigin = !string.IsNullOrWhiteSpace(originHeader);
                        if (!IsLoopbackHostHeader(hostHeader, hasOrigin))
                        {
                            WriteRawMehediHttpResponse(shihabStream, 403, "Forbidden", "{\"ok\":false,\"message\":\"Untrusted Host rejected\"}", "");
                            return;
                        }

                        string normalizedCorsOrigin = "";
                        bool isChromiumOrigin = false;
                        string chromiumExtId = "";
                        bool isValidExtensionOrigin = TryParseValidExtensionOrigin(
                            originHeader,
                            out normalizedCorsOrigin,
                            out isChromiumOrigin,
                            out chromiumExtId);

                        if (hasOrigin && !isValidExtensionOrigin)
                        {
                            WriteRawMehediHttpResponse(shihabStream, 403, "Forbidden", "{\"ok\":false,\"message\":\"Untrusted Origin rejected\"}", "");
                            return;
                        }

                        string allowedCorsOrigin = isValidExtensionOrigin ? normalizedCorsOrigin : "";

                        if (string.Equals(mehediMethod, "OPTIONS", StringComparison.OrdinalIgnoreCase))
                        {
                            WriteRawMehediHttpResponse(shihabStream, 204, "No Content", "", allowedCorsOrigin);
                            return;
                        }

                        string shihabCleanPath = shihabPath.Split('?')[0].ToLowerInvariant();
                        if (shihabCleanPath == "/shihab/ping" || shihabCleanPath == "/")
                        {
                            WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("MHS Download Manager Bridge Ready"), allowedCorsOrigin);
                            return;
                        }

                        bool isPinnedExtension = isChromiumOrigin && GetPinnedChromiumExtensionIds().Contains(chromiumExtId);
                        bool isAuthorizedCaller = IsValidBridgeToken(bridgeTokenHeader) || isPinnedExtension;
                        if (!isAuthorizedCaller)
                        {
                            WriteMehediJsonTcpResponse(
                                shihabStream,
                                401,
                                BuildShihabErrorReply("Unauthorized bridge request: valid X-MHS-Bridge-Token or pinned extension Origin required"),
                                allowedCorsOrigin);
                            return;
                        }

                        bool shihabIsBinaryRelay = shihabCleanPath == "/shihab/relay-chunk";
                        int maxBodyAllowed = shihabIsBinaryRelay ? MaxRelayBinaryBodyBytes : MaxJsonBodyBytes;
                        if (contentLength < 0 || contentLength > maxBodyAllowed)
                        {
                            WriteRawMehediHttpResponse(shihabStream, 413, "Payload Too Large", "{\"ok\":false,\"message\":\"Payload exceeds bridge limit\"}", allowedCorsOrigin);
                            return;
                        }

                        string mehediBody = "";
                        byte[] shihabRawBodyBytes = null;
                        int mehediRawBodyCount = 0;
                        if (contentLength > 0)
                        {
                            shihabRawBodyBytes = new byte[contentLength];
                            mehediRawBodyCount = ReadExactShihabBytes(bufferedRead, shihabRawBodyBytes, contentLength);
                            if (mehediRawBodyCount < contentLength)
                            {
                                WriteRawMehediHttpResponse(shihabStream, 400, "Bad Request", "{\"ok\":false,\"message\":\"Truncated request body\"}", allowedCorsOrigin);
                                return;
                            }
                            if (!shihabIsBinaryRelay)
                            {
                                mehediBody = Encoding.UTF8.GetString(shihabRawBodyBytes, 0, mehediRawBodyCount);
                            }
                        }

                        DispatchAuthorizedBridgeRequest(
                            shihabStream,
                            mehediMethod,
                            shihabPath,
                            shihabCleanPath,
                            mehediBody,
                            shihabRawBodyBytes,
                            mehediRawBodyCount,
                            allowedCorsOrigin);
                    }
                }
                catch
                {
                }
            }
        }

        private void DispatchAuthorizedBridgeRequest(
            NetworkStream shihabStream,
            string mehediMethod,
            string shihabPath,
            string shihabCleanPath,
            string mehediBody,
            byte[] shihabRawBodyBytes,
            int mehediRawBodyCount,
            string allowedCorsOrigin)
        {
            if (shihabCleanPath == "/shihab/show" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                if (OnShihabShowMainWindowRequested != null)
                {
                    try { OnShihabShowMainWindowRequested(); } catch { }
                }
                WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("Main window activated"), allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/open-updater" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                if (OnShihabOpenUpdaterRequested != null)
                {
                    try { OnShihabOpenUpdaterRequested(); } catch { }
                }
                else if (OnShihabShowMainWindowRequested != null)
                {
                    try { OnShihabShowMainWindowRequested(); } catch { }
                }
                WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("Updater dialog requested"), allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/exit" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                try { _mehediEngine.PersistShihabState(); } catch { }
                try { ShihabCrashMonitor.MarkCleanShutdown(); } catch { }
                WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true,\"message\":\"Shutting down cleanly\"}", allowedCorsOrigin);
                Task.Run(delegate
                {
                    Thread.Sleep(120);
                    if (OnShihabGracefulExitRequested != null)
                    {
                        try { OnShihabGracefulExitRequested(); return; } catch { }
                    }
                    try { System.Windows.Forms.Application.Exit(); } catch { }
                });
                return;
            }

            if (shihabCleanPath == "/shihab/api/status" && string.Equals(mehediMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                List<ShihabDownloadItem> items = _mehediEngine.GetShihabDownloadsSnapshot();
                StringBuilder jb = new StringBuilder("[");
                for (int i = 0; i < items.Count; i++)
                {
                    ShihabDownloadItem it = items[i];
                    if (i > 0) jb.Append(",");
                    jb.AppendFormat(CultureInfo.InvariantCulture,
                        "{{\"id\":\"{0}\",\"fileName\":\"{1}\",\"status\":\"{2}\",\"percent\":{3:0.0},\"totalBytes\":{4},\"downloadedBytes\":{5},\"speedBps\":{6:0},\"checksumStatus\":\"{7}\",\"url\":\"{8}\"}}",
                        ShihabJsonCodec.EscapeString(it.ShihabId),
                        ShihabJsonCodec.EscapeString(it.ShihabFileName),
                        ShihabJsonCodec.EscapeString(it.ShihabStatusText),
                        it.GetMehediPercent(),
                        it.ShihabTotalBytes,
                        it.MehediDownloadedBytes,
                        it.ShihabCurrentSpeedBps,
                        ShihabJsonCodec.EscapeString(it.ShihabChecksumStatus),
                        ShihabJsonCodec.EscapeString(it.MehediUrl));
                }
                jb.Append("]");
                WriteRawMehediHttpResponse(shihabStream, 200, "OK", jb.ToString(), allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/api/add" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, object> addObj = ShihabJsonCodec.ParseObjectOrNull(mehediBody);
                if (addObj == null)
                {
                    WriteRawMehediHttpResponse(shihabStream, 400, "Bad Request", "{\"ok\":false,\"error\":\"Invalid JSON body\"}", allowedCorsOrigin);
                    return;
                }
                string url = ShihabJsonCodec.GetString(addObj, "url", "").Trim();
                Uri validatedUrl;
                if (!ShihabProcessAndPathSafety.TryValidateExternalShellUri(url, true, out validatedUrl))
                {
                    WriteRawMehediHttpResponse(shihabStream, 400, "Bad Request", "{\"ok\":false,\"error\":\"Valid http/https/ftp/magnet url is required\"}", allowedCorsOrigin);
                    return;
                }

                string fileName = ShihabJsonCodec.GetString(addObj, "fileName", "");
                string checksum = ShihabJsonCodec.GetString(addObj, "checksum", "");
                string kind = ShihabJsonCodec.GetString(addObj, "kind", "");
                string format = ShihabJsonCodec.GetString(addObj, "format", "");
                string postBody = ShihabJsonCodec.GetString(addObj, "postBody", "");
                bool audioOnly = ShihabJsonCodec.GetBool(addObj, "audioOnly", false);
                bool incognito = ShihabJsonCodec.GetBool(addObj, "incognito", false);

                ShihabDownloadItem newItem = CreateItemFromApiOrCapture(url, fileName, checksum, kind, format, audioOnly, incognito, postBody);
                _mehediEngine.AddShihabDownloadItem(newItem, true);
                WriteRawMehediHttpResponse(shihabStream, 200, "OK",
                    "{\"ok\":true,\"id\":\"" + ShihabJsonCodec.EscapeString(newItem.ShihabId) + "\",\"fileName\":\"" + ShihabJsonCodec.EscapeString(newItem.ShihabFileName) + "\"}",
                    allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/api/action" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, object> actObj = ShihabJsonCodec.ParseObjectOrNull(mehediBody);
                if (actObj == null)
                {
                    WriteRawMehediHttpResponse(shihabStream, 400, "Bad Request", "{\"ok\":false,\"error\":\"Invalid JSON body\"}", allowedCorsOrigin);
                    return;
                }
                string act = ShihabJsonCodec.GetString(actObj, "action", "").Trim().ToLowerInvariant();
                string targetId = ShihabJsonCodec.GetString(actObj, "id", "").Trim();
                ExecuteBridgeActionOnEngine(act, targetId);
                WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true}", allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/relay-jobs" && string.Equals(mehediMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                ShihabRelayJobsReplyDto mehediRelayReply = new ShihabRelayJobsReplyDto
                {
                    ShihabOk = true,
                    MehediJobs = ShihabBrowserRelayHub.ClaimPendingShihabRelayJobs()
                };
                WriteMehediJsonTcpResponse(shihabStream, 200, mehediRelayReply, allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/relay-chunk" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                string shihabJobId = ParseMehediQueryParam(shihabPath, "jobId");
                long mehediOffset = -1L;
                long shihabTotal = -1L;
                long.TryParse(ParseMehediQueryParam(shihabPath, "offset"), out mehediOffset);
                long.TryParse(ParseMehediQueryParam(shihabPath, "total"), out shihabTotal);

                bool mehediAccepted = ShihabBrowserRelayHub.AppendMehediRelayChunk(
                    shihabJobId,
                    mehediOffset,
                    shihabTotal,
                    shihabRawBodyBytes,
                    mehediRawBodyCount);
                if (mehediAccepted)
                {
                    WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true}", allowedCorsOrigin);
                }
                else
                {
                    WriteRawMehediHttpResponse(shihabStream, 410, "Gone", "{\"ok\":false,\"message\":\"cancelled\"}", allowedCorsOrigin);
                }
                return;
            }

            if (shihabCleanPath == "/shihab/relay-done" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                string mehediJobId = ParseMehediQueryParam(shihabPath, "jobId");
                string shihabOkParam = ParseMehediQueryParam(shihabPath, "ok");
                string mehediErrParam = ParseMehediQueryParam(shihabPath, "error");
                bool shihabSuccess = shihabOkParam == "1" || string.Equals(shihabOkParam, "true", StringComparison.OrdinalIgnoreCase);
                ShihabBrowserRelayHub.CompleteShihabRelayJob(mehediJobId, shihabSuccess, mehediErrParam);
                WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true}", allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/toggle" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                _shihabConfig.MehediBrowserIntegrationEnabled = !_shihabConfig.MehediBrowserIntegrationEnabled;
                WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("Toggled integration"), allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/yt-streams" && string.Equals(mehediMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                string mehediYtId = ParseMehediQueryParam(shihabPath, "videoId");
                string shihabVisData = ParseMehediQueryParam(shihabPath, "visitorData");
                bool mehediForceYt = string.Equals(ParseMehediQueryParam(shihabPath, "force"), "1", StringComparison.OrdinalIgnoreCase);
                ShihabYouTubeStreamsReplyDto shihabYtReply = MehediYouTubeInnerTubeResolver.ResolveShihabYouTubeStreams(
                    mehediYtId,
                    shihabVisData,
                    mehediForceYt);
                WriteMehediJsonTcpResponse(shihabStream, shihabYtReply.ShihabOk ? 200 : 400, shihabYtReply, allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/capture" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                ShihabCapturedLinkDto shihabDto = DeserializeMehediJson<ShihabCapturedLinkDto>(mehediBody);
                Uri validCaptureUrl;
                if (shihabDto != null &&
                    !string.IsNullOrWhiteSpace(shihabDto.ShihabUrl) &&
                    ShihabProcessAndPathSafety.TryValidateExternalShellUri(shihabDto.ShihabUrl, true, out validCaptureUrl))
                {
                    shihabDto.ShihabSaveDirectory = NormalizeSafeCaptureSaveDirectory(shihabDto.ShihabSaveDirectory);
                    if (OnShihabSingleCaptureReceived != null)
                    {
                        OnShihabSingleCaptureReceived(shihabDto);
                    }
                    else
                    {
                        ShihabDownloadItem headlessItem = CreateItemFromCaptureDto(shihabDto);
                        _mehediEngine.AddShihabDownloadItem(headlessItem, !shihabDto.MehediAddToQueueOnly);
                    }
                    WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("Download transferred to MHS Download Manager"), allowedCorsOrigin);
                    return;
                }
                WriteMehediJsonTcpResponse(shihabStream, 400, BuildShihabErrorReply("Missing or unsupported URL"), allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/capture-batch" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                ShihabBatchCaptureDto mehediBatchDto = DeserializeMehediJson<ShihabBatchCaptureDto>(mehediBody);
                if (mehediBatchDto != null && mehediBatchDto.ShihabLinks != null && mehediBatchDto.ShihabLinks.Count > 0)
                {
                    List<MehediBatchLinkEntryDto> filteredLinks = new List<MehediBatchLinkEntryDto>();
                    for (int i = 0; i < mehediBatchDto.ShihabLinks.Count; i++)
                    {
                        MehediBatchLinkEntryDto linkEntry = mehediBatchDto.ShihabLinks[i];
                        Uri entryUri;
                        if (linkEntry != null &&
                            !string.IsNullOrWhiteSpace(linkEntry.ShihabUrl) &&
                            ShihabProcessAndPathSafety.TryValidateExternalShellUri(linkEntry.ShihabUrl, true, out entryUri))
                        {
                            filteredLinks.Add(linkEntry);
                        }
                    }
                    if (filteredLinks.Count > 0)
                    {
                        mehediBatchDto.ShihabLinks = filteredLinks;
                        if (OnMehediBatchCaptureReceived != null)
                        {
                            OnMehediBatchCaptureReceived(mehediBatchDto);
                        }
                        WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply(
                            string.Format("Transferred {0} links to MHS Download Manager", filteredLinks.Count)), allowedCorsOrigin);
                        return;
                    }
                }
                WriteMehediJsonTcpResponse(shihabStream, 400, BuildShihabErrorReply("No valid links supplied"), allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/start-queue" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                if (OnShihabStartMainQueueRequested != null)
                {
                    OnShihabStartMainQueueRequested();
                }
                else
                {
                    _mehediEngine.StartShihabQueue("Main Download Queue");
                }
                WriteMehediJsonTcpResponse(shihabStream, 200, BuildShihabStatusReply("Main Download Queue started"), allowedCorsOrigin);
                return;
            }

            WriteMehediJsonTcpResponse(shihabStream, 404, BuildShihabErrorReply("Unknown Shihab endpoint"), allowedCorsOrigin);
        }

        private static string NormalizeSafeCaptureSaveDirectory(string requestedDir)
        {
            if (string.IsNullOrWhiteSpace(requestedDir)) return "";
            string trimmed = requestedDir.Trim();
            if (HasControlOrLineBreak(trimmed) || trimmed.StartsWith(@"\\", StringComparison.Ordinal))
            {
                return "";
            }
            if (!Path.IsPathRooted(trimmed))
            {
                return "";
            }
            try
            {
                string full = Path.GetFullPath(trimmed).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                if (IsSensitiveSystemDirectory(full))
                {
                    return "";
                }
                return full;
            }
            catch
            {
                return "";
            }
        }

        private static bool IsSensitiveSystemDirectory(string fullPath)
        {
            Environment.SpecialFolder[] protectedFolders = new Environment.SpecialFolder[]
            {
                Environment.SpecialFolder.Windows,
                Environment.SpecialFolder.System,
                Environment.SpecialFolder.SystemX86,
                Environment.SpecialFolder.ProgramFiles,
                Environment.SpecialFolder.ProgramFilesX86
            };
            for (int i = 0; i < protectedFolders.Length; i++)
            {
                try
                {
                    string folder = Environment.GetFolderPath(protectedFolders[i]);
                    if (string.IsNullOrEmpty(folder)) continue;
                    string normFolder = Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                    if (string.Equals(fullPath, normFolder, StringComparison.OrdinalIgnoreCase) ||
                        fullPath.StartsWith(normFolder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
                catch { }
            }
            return false;
        }

        private ShihabDownloadItem CreateItemFromApiOrCapture(
            string url,
            string fileName,
            string checksum,
            string kind,
            string format,
            bool audioOnly,
            bool incognito,
            string postBody)
        {
            ShihabDownloadItem item = new ShihabDownloadItem();
            item.MehediUrl = url;
            item.ShihabFileName = string.IsNullOrWhiteSpace(fileName)
                ? ShihabFormatHelpers.ExtractMehediFileNameFromUrl(url)
                : ShihabFormatHelpers.SanitizeShihabFileName(fileName);
            item.ShihabExpectedChecksum = checksum ?? "";
            item.ShihabIsPrivateIncognito = incognito;
            item.ShihabPostBody = postBody ?? "";

            string k = (kind ?? "").Trim().ToLowerInvariant();
            if (k == "ytdlp" || k == "video" || k == "playlist")
            {
                item.ShihabIsYtDlpJob = true;
                item.ShihabYtDlpFormatSelector = format ?? "";
                item.ShihabAudioOnlyExtract = audioOnly;
            }
            else if (k == "gallery")
            {
                item.ShihabIsGalleryJob = true;
            }
            else if (k == "torrent" || url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase) || url.EndsWith(".torrent", StringComparison.OrdinalIgnoreCase))
            {
                item.ShihabIsTorrentOrMagnet = true;
                item.ShihabTorrentMagnetOrPath = url;
            }

            ShihabCategoryRule cat = _shihabConfig.ResolveMehediCategoryForFile(item.ShihabFileName);
            item.ShihabCategoryName = cat.ShihabName;
            item.MehediSaveDirectory = cat.ShihabDefaultFolder;
            item.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(item.MehediSaveDirectory, item.ShihabFileName);
            return item;
        }

        private ShihabDownloadItem CreateItemFromCaptureDto(ShihabCapturedLinkDto dto)
        {
            ShihabDownloadItem item = CreateItemFromApiOrCapture(
                dto.ShihabUrl,
                dto.ShihabFileName,
                dto.ShihabExpectedChecksum,
                dto.ShihabJobKind,
                dto.ShihabFormatSelector,
                dto.ShihabAudioOnly,
                dto.ShihabIncognito,
                dto.ShihabPostBody);
            item.MehediReferer = dto.MehediReferrer ?? "";
            item.ShihabCookies = dto.ShihabCookies ?? "";
            if (!string.IsNullOrWhiteSpace(dto.MehediUserAgent)) item.MehediUserAgent = dto.MehediUserAgent;
            string safeSaveDir = NormalizeSafeCaptureSaveDirectory(dto.ShihabSaveDirectory);
            if (!string.IsNullOrWhiteSpace(safeSaveDir)) item.MehediSaveDirectory = safeSaveDir;
            item.MehediOriginHeader = dto.ShihabOrigin ?? "";
            item.MehediOmitCookies = dto.MehediOmitCookies;
            item.MehediAudioCompanionUrl = dto.ShihabAudioUrl ?? "";
            item.ShihabAudioCompanionBytes = dto.MehediAudioSize;
            item.ShihabSourceTabId = dto.ShihabTabId;
            item.ShihabQualityLabel = dto.MehediQualityLabel ?? "";
            return item;
        }

        private void ExecuteBridgeActionOnEngine(string act, string targetId)
        {
            if (act == "start_queue")
            {
                _mehediEngine.StartShihabQueue("Main Download Queue");
                return;
            }
            if (act == "stop_queue")
            {
                _mehediEngine.StopShihabQueue("Main Download Queue");
                return;
            }
            if (string.IsNullOrWhiteSpace(targetId)) return;

            bool targetAll = string.Equals(targetId, "all", StringComparison.OrdinalIgnoreCase);
            List<ShihabDownloadItem> snap = _mehediEngine.GetShihabDownloadsSnapshot();
            for (int i = 0; i < snap.Count; i++)
            {
                ShihabDownloadItem it = snap[i];
                bool matches = targetAll || string.Equals(it.ShihabId, targetId, StringComparison.OrdinalIgnoreCase);
                if (!matches) continue;
                if (act == "pause") _mehediEngine.PauseShihabDownload(it);
                else if (act == "start" || act == "resume") _mehediEngine.StartOrResumeMehediDownload(it);
                else if (act == "remove") _mehediEngine.RemoveShihabDownload(it, false);
                else if (act == "delete") _mehediEngine.RemoveShihabDownload(it, true);
            }
        }

        private static string ParseMehediQueryParam(string shihabRawPath, string mehediParamName)
        {
            if (string.IsNullOrEmpty(shihabRawPath) || string.IsNullOrEmpty(mehediParamName)) return "";
            int shihabQPos = shihabRawPath.IndexOf('?');
            if (shihabQPos < 0 || shihabQPos + 1 >= shihabRawPath.Length) return "";
            string mehediQuery = shihabRawPath.Substring(shihabQPos + 1);
            string[] shihabPairs = mehediQuery.Split('&');
            for (int mehediI = 0; mehediI < shihabPairs.Length; mehediI++)
            {
                string shihabPair = shihabPairs[mehediI];
                int mehediEq = shihabPair.IndexOf('=');
                if (mehediEq > 0)
                {
                    try
                    {
                        string shihabK = Uri.UnescapeDataString(shihabPair.Substring(0, mehediEq));
                        if (string.Equals(shihabK, mehediParamName, StringComparison.OrdinalIgnoreCase))
                        {
                            return Uri.UnescapeDataString(shihabPair.Substring(mehediEq + 1).Replace('+', ' '));
                        }
                    }
                    catch
                    {
                    }
                }
            }
            return "";
        }

        private static bool ReadParsedHttpHeaders(
            Stream bufferedStream,
            out string shihabMethod,
            out string mehediPath,
            out string hostHeader,
            out string originHeader,
            out string bridgeTokenHeader,
            out int shihabContentLen)
        {
            shihabMethod = "";
            mehediPath = "/";
            hostHeader = "";
            originHeader = "";
            bridgeTokenHeader = "";
            shihabContentLen = 0;

            using (MemoryStream mehediHeaderBuf = new MemoryStream())
            {
                int shihabPrev3 = -1, mehediPrev2 = -1, shihabPrev1 = -1;
                while (mehediHeaderBuf.Length < MaxHttpHeaderBytes)
                {
                    int mehediByte = bufferedStream.ReadByte();
                    if (mehediByte < 0) break;
                    mehediHeaderBuf.WriteByte((byte)mehediByte);
                    if (shihabPrev3 == '\r' && mehediPrev2 == '\n' && shihabPrev1 == '\r' && mehediByte == '\n')
                    {
                        break;
                    }
                    shihabPrev3 = mehediPrev2;
                    mehediPrev2 = shihabPrev1;
                    shihabPrev1 = mehediByte;
                }

                string shihabHeaderText = Encoding.ASCII.GetString(mehediHeaderBuf.ToArray());
                if (string.IsNullOrWhiteSpace(shihabHeaderText)) return false;

                string[] mehediLines = shihabHeaderText.Split(new string[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries);
                if (mehediLines.Length == 0) return false;

                string[] shihabReqParts = mehediLines[0].Split(' ');
                if (shihabReqParts.Length < 2) return false;
                shihabMethod = shihabReqParts[0].Trim();
                mehediPath = shihabReqParts[1].Trim();

                for (int mehediI = 1; mehediI < mehediLines.Length; mehediI++)
                {
                    string shihabLine = mehediLines[mehediI];
                    if (shihabLine.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        int parsedLen;
                        if (!int.TryParse(shihabLine.Substring(15).Trim(), out parsedLen))
                        {
                            shihabContentLen = -1;
                        }
                        else
                        {
                            shihabContentLen = parsedLen;
                        }
                    }
                    else if (shihabLine.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                    {
                        hostHeader = shihabLine.Substring(5).Trim();
                    }
                    else if (shihabLine.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase))
                    {
                        originHeader = shihabLine.Substring(7).Trim();
                    }
                    else if (shihabLine.StartsWith("X-MHS-Bridge-Token:", StringComparison.OrdinalIgnoreCase))
                    {
                        bridgeTokenHeader = shihabLine.Substring(19).Trim();
                    }
                    else if (string.IsNullOrEmpty(bridgeTokenHeader) && shihabLine.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    {
                        bridgeTokenHeader = shihabLine.Substring(14).Trim();
                    }
                }
            }
            return true;
        }

        private static void WriteMehediJsonTcpResponse<T>(NetworkStream shihabStream, int mehediStatus, T shihabPayload, string allowedCorsOrigin)
        {
            string mehediJson = SerializeShihabJson(shihabPayload);
            string shihabReason = mehediStatus == 200 ? "OK"
                : (mehediStatus == 400 ? "Bad Request"
                : (mehediStatus == 401 ? "Unauthorized"
                : (mehediStatus == 403 ? "Forbidden"
                : (mehediStatus == 413 ? "Payload Too Large" : "Error"))));
            WriteRawMehediHttpResponse(shihabStream, mehediStatus, shihabReason, mehediJson, allowedCorsOrigin);
        }

        private static void WriteRawMehediHttpResponse(NetworkStream shihabStream, int mehediStatus, string shihabReason, string mehediJsonBody, string allowedCorsOrigin)
        {
            byte[] shihabBodyBytes = Encoding.UTF8.GetBytes(mehediJsonBody ?? "");
            StringBuilder hdr = new StringBuilder(384);
            hdr.AppendFormat("HTTP/1.1 {0} {1}\r\n", mehediStatus, shihabReason);
            hdr.Append("Content-Type: application/json; charset=utf-8\r\n");
            hdr.Append("X-Content-Type-Options: nosniff\r\n");
            if (!string.IsNullOrEmpty(allowedCorsOrigin) && !HasControlOrLineBreak(allowedCorsOrigin))
            {
                hdr.Append("Access-Control-Allow-Origin: ").Append(allowedCorsOrigin).Append("\r\n");
                hdr.Append("Vary: Origin\r\n");
                hdr.Append("Access-Control-Allow-Private-Network: true\r\n");
                hdr.Append("Access-Control-Allow-Methods: GET, POST, OPTIONS\r\n");
                hdr.Append("Access-Control-Allow-Headers: Content-Type, X-Shihab-Client, X-MHS-Bridge-Token, Authorization\r\n");
            }
            hdr.Append("Connection: close\r\n");
            hdr.AppendFormat("Content-Length: {0}\r\n\r\n", shihabBodyBytes.Length);
            byte[] shihabHdrBytes = Encoding.ASCII.GetBytes(hdr.ToString());
            shihabStream.Write(shihabHdrBytes, 0, shihabHdrBytes.Length);
            if (shihabBodyBytes.Length > 0)
            {
                shihabStream.Write(shihabBodyBytes, 0, shihabBodyBytes.Length);
            }
            shihabStream.Flush();
        }

        private MehediBridgeStatusReply BuildShihabStatusReply(string mehediMsg)
        {
            List<ShihabDownloadItem> shihabSnap = _mehediEngine.GetShihabDownloadsSnapshot();
            int mehediActive = 0;
            for (int shihabI = 0; shihabI < shihabSnap.Count; shihabI++)
            {
                if (_mehediEngine.IsMehediItemActive(shihabSnap[shihabI].ShihabId))
                {
                    mehediActive++;
                }
            }
            ShihabUpdateCheckResult cachedUpd = ShihabAutoUpdater.LastCachedUpdate;
            bool updAvail = cachedUpd != null && cachedUpd.UpdateAvailable;
            string curVer = ShihabAutoUpdater.GetCurrentAppVersion();
            return new MehediBridgeStatusReply
            {
                ShihabOk = true,
                MehediAppName = "MHS Download Manager",
                ShihabVersion = curVer,
                MehediIntegrationEnabled = _shihabConfig.MehediBrowserIntegrationEnabled,
                ShihabShowVideoPanel = _shihabConfig.ShihabShowVideoFloatingPanel,
                MehediPanelMiniMode = _shihabConfig.ShihabPanelMiniMode,
                ShihabPanelSkipAutoStart = _shihabConfig.MehediPanelSkipAutoStart,
                MehediMinVideoSizeKb = _shihabConfig.MehediMinVideoSizeKb,
                ShihabAutoCaptureExtensions = _shihabConfig.MehediAutoCaptureExtensions,
                MehediExcludedSites = _shihabConfig.ShihabExcludedSitesSpaceSeparated,
                ShihabExcludedAddresses = _shihabConfig.MehediExcludedAddressesSpaceSeparated ?? "",
                MehediPreventKey = string.IsNullOrWhiteSpace(_shihabConfig.MehediPreventKeyName) ? "Alt" : _shihabConfig.MehediPreventKeyName,
                ShihabForceKey = string.IsNullOrWhiteSpace(_shihabConfig.ShihabForceKeyName) ? "Ins" : _shihabConfig.ShihabForceKeyName,
                ShihabCaptureOpenedFiles = _shihabConfig.ShihabCaptureOpenedFiles,
                ShihabCaptureMinSizeKb = _shihabConfig.ShihabCaptureMinSizeKb,
                ShihabActiveDownloads = mehediActive,
                ShihabUpdateAvailable = updAvail,
                ShihabLatestVersion = updAvail ? cachedUpd.LatestVersion : curVer,
                ShihabLatestHtmlUrl = updAvail ? cachedUpd.HtmlUrl : "",
                MehediMessage = mehediMsg
            };
        }

        private static MehediBridgeStatusReply BuildShihabErrorReply(string mehediErr)
        {
            return new MehediBridgeStatusReply
            {
                ShihabOk = false,
                MehediAppName = "MHS Download Manager",
                ShihabVersion = ShihabAutoUpdater.GetCurrentAppVersion(),
                MehediMessage = mehediErr
            };
        }

        public static T DeserializeMehediJson<T>(string shihabJson) where T : class
        {
            if (string.IsNullOrWhiteSpace(shihabJson)) return null;
            string mehediClean = shihabJson.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            byte[] mehediBytes = Encoding.UTF8.GetBytes(mehediClean);
            using (MemoryStream shihabMs = new MemoryStream(mehediBytes))
            {
                DataContractJsonSerializer mehediSer = new DataContractJsonSerializer(typeof(T));
                return mehediSer.ReadObject(shihabMs) as T;
            }
        }

        public static string SerializeShihabJson<T>(T mehediObj)
        {
            using (MemoryStream shihabMs = new MemoryStream())
            {
                DataContractJsonSerializer mehediSer = new DataContractJsonSerializer(typeof(T));
                mehediSer.WriteObject(shihabMs, mehediObj);
                return Encoding.UTF8.GetString(shihabMs.ToArray());
            }
        }

        public static bool SendUrlToExistingShihabInstance(int shihabPort, string url, string saveDir, string fileName)
        {
            try
            {
                ShihabCapturedLinkDto shihabDto = new ShihabCapturedLinkDto
                {
                    ShihabUrl = url ?? "",
                    ShihabFileName = fileName ?? "",
                    ShihabSaveDirectory = saveDir ?? "",
                    ShihabSilentMode = true
                };
                string mehediJson = SerializeShihabJson(shihabDto);
                PostLocalShihabJson(shihabPort, "/shihab/capture", mehediJson, null);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool ForwardCliToRunningShihabInstance(ShihabCliParsedArguments mehediCli, int shihabPort)
        {
            try
            {
                string customDir = mehediCli != null ? mehediCli.MehediCustomConfigDir : null;
                if (mehediCli != null && !string.IsNullOrWhiteSpace(mehediCli.ShihabDirectUrl))
                {
                    ShihabCapturedLinkDto shihabDto = new ShihabCapturedLinkDto
                    {
                        ShihabUrl = mehediCli.ShihabDirectUrl,
                        ShihabFileName = mehediCli.ShihabLocalFileName ?? "",
                        ShihabSaveDirectory = mehediCli.MehediLocalSavePath ?? "",
                        ShihabSilentMode = mehediCli.MehediSilentNoPrompt,
                        MehediAddToQueueOnly = mehediCli.MehediAddToQueueOnly,
                        MehediExitWhenDone = mehediCli.ShihabExitOnDone
                    };
                    string mehediJson = SerializeShihabJson(shihabDto);
                    PostLocalShihabJson(shihabPort, "/shihab/capture", mehediJson, customDir);
                }
                if (mehediCli != null && mehediCli.ShihabStartMainQueue)
                {
                    PostLocalShihabJson(shihabPort, "/shihab/start-queue", "{}", customDir);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static string PostLocalShihabJson(int mehediPort, string shihabPath, string mehediJsonBody)
        {
            return PostLocalShihabJson(mehediPort, shihabPath, mehediJsonBody, null);
        }

        public static string PostLocalShihabJson(int mehediPort, string shihabPath, string mehediJsonBody, string customConfigDir)
        {
            HttpWebRequest shihabReq = (HttpWebRequest)WebRequest.Create(string.Format("http://127.0.0.1:{0}{1}", mehediPort, shihabPath));
            shihabReq.Method = "POST";
            shihabReq.Timeout = 3000;
            shihabReq.ReadWriteTimeout = 3000;
            shihabReq.Proxy = null;
            shihabReq.KeepAlive = false;
            shihabReq.ContentType = "application/json; charset=utf-8";
            try
            {
                ShihabStateStore tokenStore = new ShihabStateStore(customConfigDir);
                string token = tokenStore.LoadOrCreateBridgeToken();
                if (!string.IsNullOrEmpty(token))
                {
                    shihabReq.Headers["X-MHS-Bridge-Token"] = token;
                }
            }
            catch { }
            byte[] mehediData = Encoding.UTF8.GetBytes(mehediJsonBody ?? "{}");
            shihabReq.ContentLength = mehediData.Length;
            using (Stream shihabOut = shihabReq.GetRequestStream())
            {
                shihabOut.Write(mehediData, 0, mehediData.Length);
            }
            using (HttpWebResponse mehediResp = (HttpWebResponse)shihabReq.GetResponse())
            using (StreamReader shihabReader = new StreamReader(mehediResp.GetResponseStream(), Encoding.UTF8))
            {
                return shihabReader.ReadToEnd();
            }
        }

        public static string GetLocalMehediPing(int shihabPort)
        {
            HttpWebRequest mehediReq = (HttpWebRequest)WebRequest.Create(string.Format("http://127.0.0.1:{0}/shihab/ping", shihabPort));
            mehediReq.Method = "GET";
            mehediReq.Timeout = 2000;
            mehediReq.ReadWriteTimeout = 2000;
            mehediReq.Proxy = null;
            mehediReq.KeepAlive = false;
            using (HttpWebResponse shihabResp = (HttpWebResponse)mehediReq.GetResponse())
            using (StreamReader mehediReader = new StreamReader(shihabResp.GetResponseStream(), Encoding.UTF8))
            {
                return mehediReader.ReadToEnd();
            }
        }

        private static int ReadExactShihabBytes(Stream mehediStream, byte[] shihabBuffer, int mehediCount)
        {
            int shihabOffset = 0;
            while (shihabOffset < mehediCount)
            {
                int mehediGot = mehediStream.Read(shihabBuffer, shihabOffset, mehediCount - shihabOffset);
                if (mehediGot <= 0)
                {
                    return shihabOffset;
                }
                shihabOffset += mehediGot;
            }
            return shihabOffset;
        }

        public void Dispose()
        {
            try
            {
                _shihabIsRunning = false;
                if (_mehediStopCts != null) _mehediStopCts.Cancel();
                if (_shihabTcpListener != null)
                {
                    _shihabTcpListener.Stop();
                }
            }
            catch { }
        }
    }
}

