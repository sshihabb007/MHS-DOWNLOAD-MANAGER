using System;
using System.Collections.Concurrent;
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
        private const int MaxConcurrentBridgeConnections = 32;
        private const long DriveFreeSpaceCacheTtlMs = 5000L;

        private sealed class CachedDriveFreeSpace
        {
            public long FreeBytes;
            public long UpdatedTickMs;
        }

        private sealed class PrependBufferStream : Stream
        {
            private readonly byte[] _prefix;
            private int _prefixOffset;
            private readonly Stream _inner;

            public PrependBufferStream(byte[] prefix, Stream inner)
            {
                _prefix = prefix;
                _prefixOffset = 0;
                _inner = inner;
            }

            public override bool CanRead { get { return true; } }
            public override bool CanSeek { get { return false; } }
            public override bool CanWrite { get { return false; } }
            public override long Length { get { throw new NotSupportedException(); } }
            public override long Position
            {
                get { throw new NotSupportedException(); }
                set { throw new NotSupportedException(); }
            }
            public override void Flush() { }
            public override long Seek(long offset, SeekOrigin origin) { throw new NotSupportedException(); }
            public override void SetLength(long value) { throw new NotSupportedException(); }
            public override void Write(byte[] buffer, int offset, int count) { throw new NotSupportedException(); }

            public override int Read(byte[] buffer, int offset, int count)
            {
                if (_prefix != null && _prefixOffset < _prefix.Length)
                {
                    int avail = _prefix.Length - _prefixOffset;
                    int take = Math.Min(avail, count);
                    Buffer.BlockCopy(_prefix, _prefixOffset, buffer, offset, take);
                    _prefixOffset += take;
                    return take;
                }
                return _inner.Read(buffer, offset, count);
            }
        }

        private static readonly ConcurrentDictionary<Type, DataContractJsonSerializer> JsonSerializerCache =
            new ConcurrentDictionary<Type, DataContractJsonSerializer>();
        private static readonly ConcurrentDictionary<string, CachedDriveFreeSpace> DriveFreeSpaceCache =
            new ConcurrentDictionary<string, CachedDriveFreeSpace>(StringComparer.OrdinalIgnoreCase);
        private static readonly Stopwatch BridgeMonotonicClock = Stopwatch.StartNew();

        private static readonly Regex ChromeOriginRegex = new Regex(
            @"^chrome-extension://([a-p]{32})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex FirefoxOriginRegex = new Regex(
            @"^moz-extension://([0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly ShihabStateStore _shihabStore;
        private readonly MehediAppConfiguration _shihabConfig;
        private readonly MehediDynamicSegmentEngine _mehediEngine;
        private readonly SemaphoreSlim _connectionGate = new SemaphoreSlim(MaxConcurrentBridgeConnections, MaxConcurrentBridgeConnections);
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

        private HashSet<string> GetPinnedChromiumExtensionIds(bool forceRefresh = false)
        {
            lock (_pinnedOriginsLock)
            {
                double minAgeSeconds = forceRefresh ? 3.0 : 60.0;
                if (_pinnedChromiumExtensionIds != null && (DateTime.UtcNow - _pinnedIdsLastLoadedUtc).TotalSeconds < minAgeSeconds)
                {
                    return _pinnedChromiumExtensionIds;
                }

                HashSet<string> ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                try
                {
                    string exePath = ShihabProcessAndPathSafety.CurrentExePath;
                    string exeDir = !string.IsNullOrEmpty(exePath) ? Path.GetDirectoryName(exePath) : null;
                    if (!string.IsNullOrEmpty(exeDir))
                    {
                        string directExtDir = Path.Combine(exeDir, "extension");
                        if (Directory.Exists(directExtDir))
                        {
                            ids.Add(ShihabProcessAndPathSafety.ComputeChromiumUnpackedExtensionId(directExtDir));
                        }
                        TryCollectAllowedOriginIdsFromManifest(
                            Path.Combine(exeDir, "com.shihab.mehedi.idm.json"),
                            ids);
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
            if (_shihabIsRunning && _shihabTcpListener != null)
            {
                return true;
            }
            int mehediPort = _shihabConfig.MehediLocalBridgePort > 0 ? _shihabConfig.MehediLocalBridgePort : 45779;
            // Retry briefly in case a previous process or legacy updater is still exiting and releasing the port.
            for (int attempt = 0; attempt < 25; attempt++)
            {
                TcpListener candidate = null;
                try
                {
                    candidate = new TcpListener(IPAddress.Loopback, mehediPort);
                    ShihabProcessAndPathSafety.DisableListenerInheritance(candidate);
                    candidate.Start();
                    ShihabProcessAndPathSafety.DisableListenerInheritance(candidate);
                    _shihabTcpListener = candidate;
                    _shihabIsRunning = true;
                    CancellationTokenSource newCts = new CancellationTokenSource();
                    CancellationTokenSource oldCts = Interlocked.Exchange(ref _mehediStopCts, newCts);
                    if (oldCts != null)
                    {
                        try { oldCts.Cancel(); } catch { }
                        try { oldCts.Dispose(); } catch { }
                    }
                    CancellationToken shihabTok = newCts.Token;

                    Thread acceptThread = new Thread(delegate()
                    {
                        AcceptMehediRequestsLoop(shihabTok);
                    });
                    acceptThread.IsBackground = true;
                    acceptThread.Name = "ShihabBridgeAccept";
                    acceptThread.Start();
                    return true;
                }
                catch
                {
                    try { if (candidate != null) candidate.Stop(); } catch { }
                    _shihabTcpListener = null;
                    _shihabIsRunning = false;
                    if (attempt < 24)
                    {
                        Thread.Sleep(200);
                    }
                }
            }
            return false;
        }

        public bool RestartShihabBridge()
        {
            try
            {
                _shihabIsRunning = false;
                CancellationTokenSource cts = Interlocked.Exchange(ref _mehediStopCts, null);
                if (cts != null)
                {
                    try { cts.Cancel(); } catch { }
                    try { cts.Dispose(); } catch { }
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
                    if (!_connectionGate.Wait(0))
                    {
                        try { mehediClient.Close(); } catch { }
                        continue;
                    }
                    TcpClient shihabCaptured = mehediClient;
                    try
                    {
                        Task.Run(delegate
                        {
                            try
                            {
                                HandleShihabHttpClient(shihabCaptured);
                            }
                            finally
                            {
                                try { _connectionGate.Release(); } catch { }
                            }
                        });
                    }
                    catch
                    {
                        try { _connectionGate.Release(); } catch { }
                        try { shihabCaptured.Close(); } catch { }
                    }
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
                    {
                        string mehediMethod;
                        string shihabPath;
                        string hostHeader;
                        string originHeader;
                        string bridgeTokenHeader;
                        int contentLength;
                        byte[] leftoverBodyBytes;

                        if (!ReadParsedHttpHeaders(
                            shihabStream,
                            out mehediMethod,
                            out shihabPath,
                            out hostHeader,
                            out originHeader,
                            out bridgeTokenHeader,
                            out contentLength,
                            out leftoverBodyBytes))
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

                        bool isAuthorizedCaller = IsValidBridgeToken(bridgeTokenHeader);

                        HashSet<string> pinnedIds = isChromiumOrigin ? GetPinnedChromiumExtensionIds(false) : null;
                        bool isPinnedExtension = isChromiumOrigin && pinnedIds != null && pinnedIds.Contains(chromiumExtId);
                        if (isChromiumOrigin && !isPinnedExtension && !isAuthorizedCaller && pinnedIds != null && pinnedIds.Count > 0)
                        {
                            pinnedIds = GetPinnedChromiumExtensionIds(true);
                            isPinnedExtension = pinnedIds != null && pinnedIds.Contains(chromiumExtId);
                        }
                        // Allow any valid extension Origin if it carries the valid Bridge Token (supports Portable/custom extension folders),
                        // or if it matches pinned Chromium IDs / Firefox extension Origin.
                        bool isAllowedExtensionOrigin = isValidExtensionOrigin &&
                            (isAuthorizedCaller || !isChromiumOrigin || isPinnedExtension || pinnedIds == null || pinnedIds.Count == 0);

                        if (hasOrigin && !isAllowedExtensionOrigin)
                        {
                            WriteRawMehediHttpResponse(shihabStream, 403, "Forbidden", "{\"ok\":false,\"message\":\"Untrusted Origin rejected\"}", "");
                            return;
                        }

                        string allowedCorsOrigin = isAllowedExtensionOrigin ? normalizedCorsOrigin : "";

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

                        if (!isAuthorizedCaller)
                        {
                            WriteMehediJsonTcpResponse(
                                shihabStream,
                                401,
                                BuildShihabErrorReply("Unauthorized bridge request: valid X-MHS-Bridge-Token required"),
                                allowedCorsOrigin);
                            return;
                        }

                        bool shihabIsBinaryRelay = shihabCleanPath == "/shihab/relay-chunk" &&
                            string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase);
                        int maxBodyAllowed = shihabIsBinaryRelay ? MaxRelayBinaryBodyBytes : MaxJsonBodyBytes;
                        if (contentLength < 0 || contentLength > maxBodyAllowed)
                        {
                            WriteRawMehediHttpResponse(shihabStream, 413, "Payload Too Large", "{\"ok\":false,\"message\":\"Payload exceeds bridge limit\"}", allowedCorsOrigin);
                            return;
                        }

                        Stream bodyStream = (leftoverBodyBytes != null && leftoverBodyBytes.Length > 0)
                            ? (Stream)new PrependBufferStream(leftoverBodyBytes, shihabStream)
                            : (Stream)shihabStream;

                        if (shihabIsBinaryRelay)
                        {
                            string shihabJobId = ParseMehediQueryParam(shihabPath, "jobId");
                            long mehediOffset = -1L;
                            long shihabTotal = -1L;
                            long.TryParse(ParseMehediQueryParam(shihabPath, "offset"), out mehediOffset);
                            long.TryParse(ParseMehediQueryParam(shihabPath, "total"), out shihabTotal);

                            bool bodyTruncated;
                            bool mehediAccepted = ShihabBrowserRelayHub.AppendMehediRelayChunkFromStream(
                                shihabJobId,
                                mehediOffset,
                                shihabTotal,
                                bodyStream,
                                contentLength,
                                out bodyTruncated);
                            if (bodyTruncated)
                            {
                                WriteRawMehediHttpResponse(shihabStream, 400, "Bad Request", "{\"ok\":false,\"message\":\"Truncated request body\"}", allowedCorsOrigin);
                            }
                            else if (mehediAccepted)
                            {
                                WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true}", allowedCorsOrigin);
                            }
                            else
                            {
                                WriteRawMehediHttpResponse(shihabStream, 410, "Gone", "{\"ok\":false,\"message\":\"cancelled\"}", allowedCorsOrigin);
                            }
                            return;
                        }

                        string mehediBody = "";
                        byte[] shihabRawBodyBytes = null;
                        int mehediRawBodyCount = 0;
                        if (contentLength > 0)
                        {
                            shihabRawBodyBytes = new byte[contentLength];
                            mehediRawBodyCount = ReadExactShihabBytes(bodyStream, shihabRawBodyBytes, contentLength);
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

            if (shihabCleanPath.StartsWith("/shihab/api/", StringComparison.OrdinalIgnoreCase) &&
                _shihabConfig != null && !_shihabConfig.ShihabAutomationApiEnabled)
            {
                WriteRawMehediHttpResponse(
                    shihabStream,
                    403,
                    "Forbidden",
                    "{\"ok\":false,\"error\":\"Local Automation API is disabled. Run 'mdm --cli api on' to re-enable.\"}",
                    allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/api/status" && string.Equals(mehediMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                string filterId = ParseMehediQueryParam(shihabPath, "id").Trim();
                string filterStatus = ParseMehediQueryParam(shihabPath, "status").Trim();
                string filterSearch = ParseMehediQueryParam(shihabPath, "search").Trim();
                string filterCat = ParseMehediQueryParam(shihabPath, "category").Trim();
                string filterQueue = ParseMehediQueryParam(shihabPath, "queue").Trim();
                int filterLimit = 0;
                int.TryParse(ParseMehediQueryParam(shihabPath, "limit"), out filterLimit);

                long defaultFreeBytes = TryGetFreeDiskBytesForPath(
                    (_shihabConfig != null && _shihabConfig.ShihabCategories != null && _shihabConfig.ShihabCategories.Count > 0)
                        ? _shihabConfig.ShihabCategories[0].ShihabDefaultFolder
                        : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

                List<ShihabDownloadItem> items = _mehediEngine.GetShihabDownloadsSnapshot();
                StringBuilder jb = new StringBuilder("[");
                int emitted = 0;
                for (int i = 0; i < items.Count; i++)
                {
                    ShihabDownloadItem it = items[i];
                    if (!string.IsNullOrEmpty(filterId) && !it.ShihabId.StartsWith(filterId, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrEmpty(filterStatus) && (it.ShihabStatusText ?? "").IndexOf(filterStatus, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    if (!string.IsNullOrEmpty(filterCat) && !string.Equals(it.ShihabCategoryName ?? "", filterCat, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrEmpty(filterQueue) && !string.Equals(it.MehediQueueName ?? "", filterQueue, StringComparison.OrdinalIgnoreCase)) continue;
                    if (!string.IsNullOrEmpty(filterSearch) &&
                        (it.ShihabFileName ?? "").IndexOf(filterSearch, StringComparison.OrdinalIgnoreCase) < 0 &&
                        (it.MehediUrl ?? "").IndexOf(filterSearch, StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        continue;
                    }
                    if (filterLimit > 0 && emitted >= filterLimit) break;
                    if (emitted > 0) jb.Append(",");
                    long itemFree = !string.IsNullOrWhiteSpace(it.MehediSaveDirectory)
                        ? TryGetFreeDiskBytesForPath(it.MehediSaveDirectory)
                        : defaultFreeBytes;
                    jb.AppendFormat(CultureInfo.InvariantCulture,
                        "{{\"id\":\"{0}\",\"fileName\":\"{1}\",\"status\":\"{2}\",\"percent\":{3:0.0},\"totalBytes\":{4},\"downloadedBytes\":{5},\"speedBps\":{6:0},\"checksumStatus\":\"{7}\",\"url\":\"{8}\",\"uploadedBytes\":{9},\"uploadSpeedBps\":{10:0},\"activePeers\":{11},\"listenPort\":{12},\"category\":\"{13}\",\"queue\":\"{14}\",\"saveDir\":\"{15}\",\"private\":{16},\"freeSpaceBytes\":{17}}}",
                        ShihabJsonCodec.EscapeString(it.ShihabId),
                        ShihabJsonCodec.EscapeString(it.ShihabFileName),
                        ShihabJsonCodec.EscapeString(it.ShihabStatusText),
                        it.GetMehediPercent(),
                        it.ShihabTotalBytes,
                        it.MehediDownloadedBytes,
                        it.ShihabCurrentSpeedBps,
                        ShihabJsonCodec.EscapeString(it.ShihabChecksumStatus),
                        ShihabJsonCodec.EscapeString(it.MehediUrl),
                        it.ShihabTorrentUploadedBytes,
                        it.ShihabTorrentUploadSpeedBps,
                        it.ShihabTorrentActivePeers,
                        it.ShihabTorrentListenPort,
                        ShihabJsonCodec.EscapeString(it.ShihabCategoryName ?? ""),
                        ShihabJsonCodec.EscapeString(it.MehediQueueName ?? ""),
                        ShihabJsonCodec.EscapeString(it.MehediSaveDirectory ?? ""),
                        it.ShihabIsPrivateIncognito ? "true" : "false",
                        itemFree);
                    emitted++;
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
                string validatedLocalTorrent;
                bool isLocalTorrent = ShihabProcessAndPathSafety.TryValidateLocalTorrentFilePath(url, out validatedLocalTorrent);
                if (!isLocalTorrent && !ShihabProcessAndPathSafety.TryValidateExternalShellUri(url, true, out validatedUrl))
                {
                    WriteRawMehediHttpResponse(shihabStream, 400, "Bad Request", "{\"ok\":false,\"error\":\"Valid http/https/ftp/magnet url or local .torrent file is required\"}", allowedCorsOrigin);
                    return;
                }
                if (isLocalTorrent) url = validatedLocalTorrent;

                string fileName = ShihabJsonCodec.GetString(addObj, "fileName", "");
                string checksum = ShihabJsonCodec.GetString(addObj, "checksum", "");
                string kind = ShihabJsonCodec.GetString(addObj, "kind", "");
                if (isLocalTorrent && string.IsNullOrWhiteSpace(kind)) kind = "torrent";
                string format = ShihabJsonCodec.GetString(addObj, "format", "");
                string postBody = ShihabJsonCodec.GetString(addObj, "postBody", "");
                bool audioOnly = ShihabJsonCodec.GetBool(addObj, "audioOnly", false);
                bool incognito = ShihabJsonCodec.GetBool(addObj, "incognito", false) || ShihabJsonCodec.GetBool(addObj, "private", false);
                string reqQueue = ShihabJsonCodec.GetString(addObj, "queue", "").Trim();
                string reqCategory = ShihabJsonCodec.GetString(addObj, "category", "").Trim();
                string reqSaveDir = NormalizeSafeCaptureSaveDirectory(ShihabJsonCodec.GetString(addObj, "saveDir", ""));
                string reqCookie = ShihabJsonCodec.GetString(addObj, "cookie", "");
                string reqReferer = ShihabJsonCodec.GetString(addObj, "referer", "");
                string reqUserAgent = ShihabJsonCodec.GetString(addObj, "userAgent", "");
                string reqDeferredRename = ShihabJsonCodec.GetString(addObj, "deferredRename", "").Trim();
                long reqLimitKb = ShihabJsonCodec.GetLong(addObj, "limitKb", 0);
                bool reqStartNow = ShihabJsonCodec.GetBool(addObj, "start", true);

                ShihabDownloadItem newItem = CreateItemFromApiOrCapture(url, fileName, checksum, kind, format, audioOnly, incognito, postBody);
                if (!string.IsNullOrEmpty(reqCategory)) newItem.ShihabCategoryName = reqCategory;
                if (!string.IsNullOrEmpty(reqQueue)) newItem.MehediQueueName = reqQueue;
                if (!string.IsNullOrEmpty(reqSaveDir))
                {
                    newItem.MehediSaveDirectory = reqSaveDir;
                    if (!newItem.ShihabIsTorrent)
                    {
                        newItem.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(reqSaveDir, newItem.ShihabFileName);
                    }
                }
                if (!string.IsNullOrEmpty(reqCookie)) newItem.ShihabCookies = reqCookie;
                if (!string.IsNullOrEmpty(reqReferer)) newItem.MehediReferer = reqReferer;
                if (!string.IsNullOrEmpty(reqUserAgent)) newItem.MehediUserAgent = reqUserAgent;
                if (!string.IsNullOrEmpty(reqDeferredRename)) newItem.ShihabDeferredRenameTarget = reqDeferredRename;
                if (reqLimitKb > 0)
                {
                    newItem.ShihabUseItemSpeedLimit = true;
                    newItem.MehediItemSpeedLimitKb = (int)Math.Min(1000000L, reqLimitKb);
                }
                if (!reqStartNow)
                {
                    if (string.IsNullOrEmpty(newItem.MehediQueueName)) newItem.MehediQueueName = "Main Download Queue";
                    newItem.SetMehediStatusEnum(ShihabDownloadStatus.ShihabQueued, "Queued");
                }
                if (newItem.ShihabIsTorrent)
                {
                    newItem.ShihabTorrentKeepSeeding = ShihabJsonCodec.GetBool(addObj, "keepSeeding", _shihabConfig.ShihabTorrentKeepSeedingDefault);
                    newItem.ShihabTorrentSequential = ShihabJsonCodec.GetBool(addObj, "sequential", _shihabConfig.ShihabTorrentSequentialDefault);
                    newItem.ShihabTorrentCreateSubfolder = ShihabJsonCodec.GetBool(addObj, "createSubfolder", true);
                    object selRaw;
                    if (addObj.TryGetValue("selectedFiles", out selRaw) && selRaw is List<object>)
                    {
                        List<object> selArr = (List<object>)selRaw;
                        List<int> selIdx = new List<int>();
                        for (int s = 0; s < selArr.Count; s++)
                        {
                            try { selIdx.Add(Convert.ToInt32(selArr[s], CultureInfo.InvariantCulture)); } catch { }
                        }
                        if (selIdx.Count > 0) newItem.ShihabSelectedTorrentFiles = selIdx;
                    }
                }
                _mehediEngine.AddShihabDownloadItem(newItem, reqStartNow);
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
                string targetQueue = ShihabJsonCodec.GetString(actObj, "queue", "").Trim();
                if (act == "delete" && string.Equals(targetId, "all", StringComparison.OrdinalIgnoreCase))
                {
                    WriteRawMehediHttpResponse(
                        shihabStream,
                        400,
                        "Bad Request",
                        "{\"ok\":false,\"error\":\"Explicit download IDs are required for destructive disk deletion; wildcard id 'all' is disallowed for action 'delete'.\"}",
                        allowedCorsOrigin);
                    return;
                }
                ExecuteBridgeActionOnEngine(act, targetId, targetQueue);
                WriteRawMehediHttpResponse(shihabStream, 200, "OK", "{\"ok\":true}", allowedCorsOrigin);
                return;
            }

            if (shihabCleanPath == "/shihab/relay-jobs" && string.Equals(mehediMethod, "GET", StringComparison.OrdinalIgnoreCase))
            {
                int waitMs = 0;
                int.TryParse(ParseMehediQueryParam(shihabPath, "wait"), out waitMs);
                if (waitMs < 0) waitMs = 0;
                if (waitMs > 25000) waitMs = 25000;
                CancellationToken stopToken = _mehediStopCts != null ? _mehediStopCts.Token : CancellationToken.None;
                ShihabRelayJobsReplyDto mehediRelayReply = new ShihabRelayJobsReplyDto
                {
                    ShihabOk = true,
                    MehediJobs = waitMs > 0
                        ? ShihabBrowserRelayHub.WaitAndClaimPendingShihabRelayJobs(waitMs, stopToken)
                        : ShihabBrowserRelayHub.ClaimPendingShihabRelayJobs()
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

            if (shihabCleanPath == "/shihab/video-info" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                Dictionary<string, object> reqObj = ShihabJsonCodec.ParseObjectOrNull(mehediBody);
                string reqUrl = reqObj != null ? ShihabJsonCodec.GetString(reqObj, "url", "").Trim() : "";
                bool isPl = reqObj != null && ShihabJsonCodec.GetBool(reqObj, "isPlaylist", false);
                Uri validatedInfoUri;
                if (!ShihabProcessAndPathSafety.TryValidateHttpUrl(reqUrl, false, out validatedInfoUri))
                {
                    WriteRawMehediHttpResponse(shihabStream, 400, "Bad Request", "{\"ok\":false,\"error\":\"Valid http/https url is required\"}", allowedCorsOrigin);
                    return;
                }
                string summaryJson;
                string infoErr;
                bool okInfo = ShihabMediaHelpersManager.QueryVideoOrPlaylistInfo(
                    _shihabStore.MehediBaseDirectory,
                    validatedInfoUri.AbsoluteUri,
                    isPl,
                    out summaryJson,
                    out infoErr,
                    _shihabConfig);
                if (okInfo && !string.IsNullOrWhiteSpace(summaryJson))
                {
                    WriteRawMehediHttpResponse(shihabStream, 200, "OK", summaryJson, allowedCorsOrigin);
                }
                else
                {
                    WriteRawMehediHttpResponse(shihabStream, 400, "Bad Request",
                        "{\"ok\":false,\"error\":\"" + ShihabJsonCodec.EscapeString(string.IsNullOrWhiteSpace(infoErr) ? "Unable to probe video metadata" : infoErr) + "\"}",
                        allowedCorsOrigin);
                }
                return;
            }

            if (shihabCleanPath == "/shihab/capture" && string.Equals(mehediMethod, "POST", StringComparison.OrdinalIgnoreCase))
            {
                ShihabCapturedLinkDto shihabDto = DeserializeMehediJson<ShihabCapturedLinkDto>(mehediBody);
                Uri validCaptureUrl;
                string validLocalTor = string.Empty;
                bool isLocalTorCapture = shihabDto != null && ShihabProcessAndPathSafety.TryValidateLocalTorrentFilePath(shihabDto.ShihabUrl, out validLocalTor);
                if (shihabDto != null &&
                    !string.IsNullOrWhiteSpace(shihabDto.ShihabUrl) &&
                    (isLocalTorCapture || ShihabProcessAndPathSafety.TryValidateExternalShellUri(shihabDto.ShihabUrl, true, out validCaptureUrl)))
                {
                    if (isLocalTorCapture) shihabDto.ShihabUrl = validLocalTor;
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
            return ShihabProcessAndPathSafety.NormalizeSafeCaptureSaveDirectory(requestedDir);
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
            else if (k == "torrent" || ShihabTorrentManager.IsMagnetOrTorrentInput(url))
            {
                item.ShihabIsTorrentOrMagnet = true;
                item.ShihabTorrentMagnetOrPath = url;
                if (string.IsNullOrWhiteSpace(fileName))
                {
                    if (File.Exists(url)) item.ShihabFileName = Path.GetFileName(url);
                    else if (url.StartsWith("magnet:", StringComparison.OrdinalIgnoreCase)) item.ShihabFileName = "magnet_download.torrent";
                }
            }

            ShihabCategoryRule cat = item.ShihabIsTorrent
                ? _shihabConfig.ResolveMehediCategoryForFile("download.torrent")
                : _shihabConfig.ResolveMehediCategoryForFile(item.ShihabFileName);
            item.ShihabCategoryName = cat.ShihabName;
            item.MehediSaveDirectory = cat.ShihabDefaultFolder;
            if (!item.ShihabIsTorrent)
            {
                item.ShihabFileName = ShihabFormatHelpers.EnsureShihabUniqueFilePath(item.MehediSaveDirectory, item.ShihabFileName);
            }
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
            if (!string.IsNullOrWhiteSpace(dto.MehediMimeType) &&
                dto.MehediMimeType.IndexOf("mpegurl", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                item.MehediIsHlsStream = true;
            }
            return item;
        }

        private static long TryGetFreeDiskBytesForPath(string folderPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(folderPath)) return -1L;
                string root = Path.GetPathRoot(Path.GetFullPath(folderPath));
                if (string.IsNullOrEmpty(root)) return -1L;
                long nowMs = BridgeMonotonicClock.ElapsedMilliseconds;
                CachedDriveFreeSpace cached;
                if (DriveFreeSpaceCache.TryGetValue(root, out cached) &&
                    cached != null &&
                    (nowMs - cached.UpdatedTickMs) < DriveFreeSpaceCacheTtlMs)
                {
                    return cached.FreeBytes;
                }
                DriveInfo di = new DriveInfo(root);
                long free = di.IsReady ? di.AvailableFreeSpace : -1L;
                DriveFreeSpaceCache[root] = new CachedDriveFreeSpace
                {
                    FreeBytes = free,
                    UpdatedTickMs = nowMs
                };
                return free;
            }
            catch
            {
                return -1L;
            }
        }

        private void ExecuteBridgeActionOnEngine(string act, string targetId, string targetQueue = "")
        {
            string qName = string.IsNullOrWhiteSpace(targetQueue) ? "Main Download Queue" : targetQueue.Trim();
            if (act == "start_queue")
            {
                _mehediEngine.StartShihabQueue(qName);
                return;
            }
            if (act == "stop_queue")
            {
                if (string.Equals(qName, "all", StringComparison.OrdinalIgnoreCase))
                {
                    _mehediEngine.StopAllShihabQueues();
                }
                else
                {
                    _mehediEngine.StopShihabQueue(qName);
                }
                return;
            }
            if (string.IsNullOrWhiteSpace(targetId)) return;

            bool targetAll = string.Equals(targetId, "all", StringComparison.OrdinalIgnoreCase);
            if (act == "delete" && targetAll)
            {
                return;
            }
            List<ShihabDownloadItem> snap = _mehediEngine.GetShihabDownloadsSnapshot();
            for (int i = 0; i < snap.Count; i++)
            {
                ShihabDownloadItem it = snap[i];
                bool matches = targetAll ||
                    string.Equals(it.ShihabId, targetId, StringComparison.OrdinalIgnoreCase) ||
                    (targetId.Length >= 6 && it.ShihabId.StartsWith(targetId, StringComparison.OrdinalIgnoreCase));
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
            Stream rawStream,
            out string shihabMethod,
            out string mehediPath,
            out string hostHeader,
            out string originHeader,
            out string bridgeTokenHeader,
            out int shihabContentLen,
            out byte[] leftoverBodyBytes)
        {
            shihabMethod = "";
            mehediPath = "/";
            hostHeader = "";
            originHeader = "";
            bridgeTokenHeader = "";
            shihabContentLen = 0;
            leftoverBodyBytes = null;

            byte[] readBuf = new byte[4096];
            using (MemoryStream mehediHeaderBuf = new MemoryStream(1024))
            {
                int headerEndIdx = -1;
                while (mehediHeaderBuf.Length < MaxHttpHeaderBytes)
                {
                    int toRead = (int)Math.Min(readBuf.Length, MaxHttpHeaderBytes - mehediHeaderBuf.Length);
                    int got = rawStream.Read(readBuf, 0, toRead);
                    if (got <= 0) break;

                    int prevLen = (int)mehediHeaderBuf.Length;
                    mehediHeaderBuf.Write(readBuf, 0, got);
                    byte[] accBuf = mehediHeaderBuf.GetBuffer();
                    int totalLen = (int)mehediHeaderBuf.Length;
                    int scanStart = Math.Max(0, prevLen - 3);
                    for (int idx = scanStart; idx <= totalLen - 4; idx++)
                    {
                        if (accBuf[idx] == '\r' && accBuf[idx + 1] == '\n' && accBuf[idx + 2] == '\r' && accBuf[idx + 3] == '\n')
                        {
                            headerEndIdx = idx + 4;
                            break;
                        }
                    }
                    if (headerEndIdx >= 0) break;
                }

                int accumulatedLen = (int)mehediHeaderBuf.Length;
                if (accumulatedLen == 0) return false;
                if (headerEndIdx < 0)
                {
                    if (accumulatedLen >= MaxHttpHeaderBytes) return false;
                    headerEndIdx = accumulatedLen;
                }

                byte[] rawBuf = mehediHeaderBuf.GetBuffer();
                int extraLen = accumulatedLen - headerEndIdx;
                if (extraLen > 0)
                {
                    leftoverBodyBytes = new byte[extraLen];
                    Buffer.BlockCopy(rawBuf, headerEndIdx, leftoverBodyBytes, 0, extraLen);
                }

                string shihabHeaderText = Encoding.ASCII.GetString(rawBuf, 0, headerEndIdx);
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
            StringBuilder hdr = new StringBuilder(512);
            hdr.AppendFormat("HTTP/1.1 {0} {1}\r\n", mehediStatus, shihabReason);
            hdr.Append("Content-Type: application/json; charset=utf-8\r\n");
            hdr.Append("X-Content-Type-Options: nosniff\r\n");
            hdr.Append("Cache-Control: no-store\r\n");
            hdr.Append("Referrer-Policy: no-referrer\r\n");
            hdr.Append("X-Frame-Options: DENY\r\n");
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
                ShihabContextMenuItems = _shihabConfig.MehediContextMenuItemsCsv ?? MehediAppConfiguration.DefaultMehediContextMenuCsv,
                ShihabActiveDownloads = mehediActive,
                ShihabPendingRelayCount = ShihabBrowserRelayHub.GetPendingUnclaimedJobCount(),
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

        private static DataContractJsonSerializer GetCachedJsonSerializer(Type type)
        {
            return JsonSerializerCache.GetOrAdd(type, delegate(Type t)
            {
                return new DataContractJsonSerializer(t);
            });
        }

        public static T DeserializeMehediJson<T>(string shihabJson) where T : class
        {
            if (string.IsNullOrWhiteSpace(shihabJson)) return null;
            string mehediClean = shihabJson.TrimStart('\uFEFF', ' ', '\t', '\r', '\n');
            byte[] mehediBytes = Encoding.UTF8.GetBytes(mehediClean);
            using (MemoryStream shihabMs = new MemoryStream(mehediBytes))
            {
                DataContractJsonSerializer mehediSer = GetCachedJsonSerializer(typeof(T));
                return mehediSer.ReadObject(shihabMs) as T;
            }
        }

        public static string SerializeShihabJson<T>(T mehediObj)
        {
            using (MemoryStream shihabMs = new MemoryStream())
            {
                DataContractJsonSerializer mehediSer = GetCachedJsonSerializer(typeof(T));
                mehediSer.WriteObject(shihabMs, mehediObj);
                return Encoding.UTF8.GetString(shihabMs.GetBuffer(), 0, (int)shihabMs.Length);
            }
        }

        public static bool SendUrlToExistingShihabInstance(int shihabPort, string url, string saveDir, string fileName, string customConfigDir = null)
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
                PostLocalShihabJson(shihabPort, "/shihab/capture", mehediJson, customConfigDir);
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
            return GetLocalMehediPing(shihabPort, 2000);
        }

        public static string GetLocalMehediPing(int shihabPort, int timeoutMs)
        {
            int effectiveTimeout = Math.Max(250, timeoutMs);
            HttpWebRequest mehediReq = (HttpWebRequest)WebRequest.Create(string.Format("http://127.0.0.1:{0}/shihab/ping", shihabPort));
            mehediReq.Method = "GET";
            mehediReq.Timeout = effectiveTimeout;
            mehediReq.ReadWriteTimeout = effectiveTimeout;
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
                CancellationTokenSource cts = Interlocked.Exchange(ref _mehediStopCts, null);
                if (cts != null)
                {
                    try { cts.Cancel(); } catch { }
                    try { cts.Dispose(); } catch { }
                }
                if (_shihabTcpListener != null)
                {
                    _shihabTcpListener.Stop();
                    _shihabTcpListener = null;
                }
            }
            catch { }
        }
    }
}

