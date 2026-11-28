using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using ShihabMehediShared;

namespace ShihabMehediIDM
{
    public class ShihabRemoteControlServer : IDisposable
    {
        private const int MaxHeaderBytes = 16384;
        private const int MaxJsonBodyBytes = 64 * 1024;
        private const int MaxTorrentUploadBodyBytes = 2 * 1024 * 1024;
        private const int MaxTrackedRateLimitIps = 2048;
        private const int MaxConcurrentClients = 32;
        private const int MaxStoredRemoteTorrents = 32;
        private const long LocalHostCacheTtlMs = 10000L;

        private static readonly ThreadLocal<SHA256> SharedSha256 = new ThreadLocal<SHA256>(delegate
        {
            return SHA256.Create();
        });
        private static readonly object LocalHostsCacheLock = new object();
        private static readonly Stopwatch RemoteMonotonicClock = Stopwatch.StartNew();
        private static HashSet<string> _cachedLocalHostNames;
        private static long _cachedLocalHostsTickMs = -LocalHostCacheTtlMs;

        private readonly Func<MehediAppConfiguration> _getConfig;
        private readonly Action _saveConfig;
        private readonly Func<List<ShihabDownloadItem>> _getDownloadsSnapshot;
        private readonly Action<string, string> _addDownloadUrl;
        private readonly Action<string, string> _performDownloadAction;
        private readonly object _rateLock = new object();
        private readonly object _devicesLock = new object();
        private readonly Dictionary<string, List<DateTime>> _failedPairAttemptsByIp = new Dictionary<string, List<DateTime>>(StringComparer.OrdinalIgnoreCase);
        private readonly SemaphoreSlim _clientSlots = new SemaphoreSlim(MaxConcurrentClients, MaxConcurrentClients);
        private TcpListener _listener;
        private volatile bool _running;
        private bool _disposed;
        public int Port { get; private set; }

        public ShihabRemoteControlServer(
            ShihabStateStore store,
            MehediAppConfiguration config,
            MehediDynamicSegmentEngine engine)
            : this(
                delegate { return config; },
                delegate { if (store != null && config != null) store.SaveMehediConfig(config); },
                delegate { return engine != null ? engine.GetShihabDownloadsSnapshot() : new List<ShihabDownloadItem>(); },
                delegate(string u, string n)
                {
                    if (engine != null && config != null && !string.IsNullOrWhiteSpace(u))
                    {
                        string customDir = store != null ? store.MehediBaseDirectory : null;
                        MehediBrowserBridgeServer.SendUrlToExistingShihabInstance(config.MehediLocalBridgePort, u, "", n, customDir);
                    }
                },
                delegate(string act, string id)
                {
                    if (engine == null || string.IsNullOrWhiteSpace(act)) return;
                    if (act == "start_queue") { engine.StartMehediQueue("Main Download Queue"); return; }
                    if (act == "stop_queue") { engine.StopShihabQueue("Main Download Queue"); return; }
                    if (string.IsNullOrWhiteSpace(id)) return;
                    bool targetAll = string.Equals(id, "all", StringComparison.OrdinalIgnoreCase);
                    if (act == "delete" && targetAll) return;
                    List<ShihabDownloadItem> snap = engine.GetShihabDownloadsSnapshot();
                    for (int i = 0; i < snap.Count; i++)
                    {
                        ShihabDownloadItem candidate = snap[i];
                        bool matches = targetAll ||
                            string.Equals(candidate.ShihabId, id, StringComparison.OrdinalIgnoreCase) ||
                            (id.Length >= 8 && candidate.ShihabId.StartsWith(id, StringComparison.OrdinalIgnoreCase));
                        if (!matches) continue;
                        if (act == "pause") engine.PauseShihabDownload(candidate);
                        else if (act == "resume" || act == "start") engine.StartOrResumeMehediDownload(candidate);
                        else if (act == "remove") engine.RemoveShihabDownload(candidate, false);
                        else if (act == "delete") engine.RemoveShihabDownload(candidate, true);
                        if (!targetAll) break;
                    }
                })
        {
            if (config != null && config.ShihabRemoteWebPort > 0)
            {
                Port = config.ShihabRemoteWebPort;
            }
        }

        public ShihabRemoteControlServer(
            Func<MehediAppConfiguration> getConfig,
            Action saveConfig,
            Func<List<ShihabDownloadItem>> getDownloadsSnapshot,
            Action<string, string> addDownloadUrl,
            Action<string, string> performDownloadAction)
        {
            _getConfig = getConfig;
            _saveConfig = saveConfig;
            _getDownloadsSnapshot = getDownloadsSnapshot;
            _addDownloadUrl = addDownloadUrl;
            _performDownloadAction = performDownloadAction;
            Port = 8463;
        }

        public void Start()
        {
            MehediAppConfiguration cfg = _getConfig != null ? _getConfig() : null;
            if (cfg != null) EnsureFreshPairingCode(cfg, _saveConfig, false);
            StartServer(cfg != null && cfg.ShihabRemoteWebPort > 0 ? cfg.ShihabRemoteWebPort : Port);
        }

        public void Stop()
        {
            StopServer();
        }

        public void Dispose()
        {
            StopServer();
            if (!_disposed)
            {
                _disposed = true;
                try { _clientSlots.Dispose(); } catch { }
            }
        }

        public static string GenerateEightDigitPairingCode()
        {
            return MehediAppConfiguration.GenerateRandomEightDigitCode();
        }

        public static string EnsureFreshPairingCode(MehediAppConfiguration cfg, Action saveCallback, bool forceRotate)
        {
            if (cfg == null) return GenerateEightDigitPairingCode();
            DateTime expUtc;
            bool expired = true;
            if (!forceRotate &&
                !string.IsNullOrWhiteSpace(cfg.ShihabRemotePairingCode) &&
                !string.IsNullOrWhiteSpace(cfg.ShihabRemotePairingExpiresUtc) &&
                DateTime.TryParse(cfg.ShihabRemotePairingExpiresUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out expUtc))
            {
                expired = DateTime.UtcNow > expUtc;
            }

            if (forceRotate || expired || string.IsNullOrWhiteSpace(cfg.ShihabRemotePairingCode))
            {
                cfg.ShihabRemotePairingCode = GenerateEightDigitPairingCode();
                cfg.ShihabRemotePairingExpiresUtc = DateTime.UtcNow.AddMinutes(10).ToString("o", CultureInfo.InvariantCulture);
                if (saveCallback != null)
                {
                    try { saveCallback(); } catch { }
                }
            }
            return cfg.ShihabRemotePairingCode;
        }

        public static string GetLocalLanDashboardUrl(int port)
        {
            int p = port > 0 ? port : 8463;
            return "http://" + GetPrimaryLanIpAddress() + ":" + p;
        }

        public static Bitmap RenderSimpleQrStyleBadge(string payload, int sizePx)
        {
            return ShihabQrCodeEncoder.RenderBitmap(
                string.IsNullOrWhiteSpace(payload) ? "http://127.0.0.1:8463" : payload,
                Math.Max(120, sizePx),
                Color.FromArgb(15, 23, 42),
                Color.White);
        }

        public static Bitmap RenderPairingMatrixBitmap(string url, string code, int sizePx)
        {
            string fullPayload = string.IsNullOrWhiteSpace(code)
                ? (url ?? "http://127.0.0.1:8463")
                : ((url ?? "http://127.0.0.1:8463").TrimEnd('/') + "/#pair=" + code.Trim());
            return ShihabQrCodeEncoder.RenderBitmap(
                fullPayload,
                Math.Max(120, sizePx),
                Color.FromArgb(15, 23, 42),
                Color.White);
        }

        public void StartServer(int port)
        {
            StopServer();
            Port = port > 0 ? port : 8463;
            try
            {
                TcpListener listener = new TcpListener(IPAddress.Any, Port);
                ShihabMehediShared.ShihabProcessAndPathSafety.DisableListenerInheritance(listener);
                listener.Start();
                ShihabMehediShared.ShihabProcessAndPathSafety.DisableListenerInheritance(listener);
                _listener = listener;
                _running = true;
                Thread t = new Thread(AcceptLoop);
                t.IsBackground = true;
                t.Name = "ShihabRemoteWebServer";
                t.Start();
            }
            catch
            {
                _running = false;
            }
        }

        public void StopServer()
        {
            _running = false;
            TcpListener old = Interlocked.Exchange(ref _listener, null);
            try
            {
                if (old != null) old.Stop();
            }
            catch { }
        }

        public static string GetPrimaryLanIpAddress()
        {
            try
            {
                NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                for (int i = 0; i < nics.Length; i++)
                {
                    NetworkInterface nic = nics[i];
                    if (nic.OperationalStatus != OperationalStatus.Up ||
                        nic.NetworkInterfaceType == NetworkInterfaceType.Loopback)
                        continue;
                    IPInterfaceProperties props = nic.GetIPProperties();
                    for (int u = 0; u < props.UnicastAddresses.Count; u++)
                    {
                        IPAddress addr = props.UnicastAddresses[u].Address;
                        if (addr.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(addr))
                        {
                            return addr.ToString();
                        }
                    }
                }
            }
            catch { }
            return "127.0.0.1";
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpListener listener = _listener;
                if (listener == null) break;
                try
                {
                    TcpClient client = listener.AcceptTcpClient();
                    if (!_running)
                    {
                        try { client.Close(); } catch { }
                        break;
                    }
                    bool acquired = false;
                    try
                    {
                        acquired = _clientSlots.Wait(0);
                    }
                    catch (ObjectDisposedException)
                    {
                        try { client.Close(); } catch { }
                        break;
                    }
                    if (!acquired)
                    {
                        try { client.Close(); } catch { }
                        continue;
                    }
                    ThreadPool.QueueUserWorkItem(HandleClient, client);
                }
                catch
                {
                    if (!_running) break;
                }
            }
        }

        private bool IsIpRateLimited(string ipKey)
        {
            lock (_rateLock)
            {
                List<DateTime> hits;
                if (!_failedPairAttemptsByIp.TryGetValue(ipKey, out hits))
                {
                    return false;
                }
                DateTime cutoff = DateTime.UtcNow.AddSeconds(-60);
                hits.RemoveAll(delegate(DateTime dt) { return dt < cutoff; });
                if (hits.Count == 0)
                {
                    _failedPairAttemptsByIp.Remove(ipKey);
                    return false;
                }
                return hits.Count >= 5;
            }
        }

        private void RecordFailedPairAttempt(string ipKey)
        {
            lock (_rateLock)
            {
                DateTime now = DateTime.UtcNow;
                DateTime cutoff = now.AddSeconds(-60);
                if (_failedPairAttemptsByIp.Count > 256)
                {
                    List<string> staleKeys = new List<string>();
                    foreach (KeyValuePair<string, List<DateTime>> kv in _failedPairAttemptsByIp)
                    {
                        kv.Value.RemoveAll(delegate(DateTime dt) { return dt < cutoff; });
                        if (kv.Value.Count == 0) staleKeys.Add(kv.Key);
                    }
                    for (int i = 0; i < staleKeys.Count; i++) _failedPairAttemptsByIp.Remove(staleKeys[i]);
                    if (_failedPairAttemptsByIp.Count >= MaxTrackedRateLimitIps && !_failedPairAttemptsByIp.ContainsKey(ipKey))
                    {
                        string oldestKey = null;
                        DateTime oldestActivity = DateTime.MaxValue;
                        foreach (KeyValuePair<string, List<DateTime>> kv in _failedPairAttemptsByIp)
                        {
                            DateTime latest = kv.Value != null && kv.Value.Count > 0
                                ? kv.Value[kv.Value.Count - 1]
                                : DateTime.MinValue;
                            if (latest < oldestActivity)
                            {
                                oldestActivity = latest;
                                oldestKey = kv.Key;
                            }
                        }
                        if (oldestKey != null)
                        {
                            _failedPairAttemptsByIp.Remove(oldestKey);
                        }
                    }
                }
                List<DateTime> hits;
                if (!_failedPairAttemptsByIp.TryGetValue(ipKey, out hits))
                {
                    hits = new List<DateTime>();
                    _failedPairAttemptsByIp[ipKey] = hits;
                }
                hits.RemoveAll(delegate(DateTime dt) { return dt < cutoff; });
                hits.Add(now);
            }
        }

        private static HashSet<string> GetAllowedLocalHostNames()
        {
            long nowMs = RemoteMonotonicClock.ElapsedMilliseconds;
            lock (LocalHostsCacheLock)
            {
                if (_cachedLocalHostNames != null && (nowMs - _cachedLocalHostsTickMs) < LocalHostCacheTtlMs)
                {
                    return _cachedLocalHostNames;
                }
                HashSet<string> hosts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                hosts.Add("127.0.0.1");
                hosts.Add("localhost");
                hosts.Add("::1");
                try
                {
                    string machine = Environment.MachineName;
                    if (!string.IsNullOrWhiteSpace(machine))
                    {
                        hosts.Add(machine.Trim());
                        hosts.Add(machine.Trim() + ".local");
                    }
                }
                catch { }
                try
                {
                    string dnsHost = Dns.GetHostName();
                    if (!string.IsNullOrWhiteSpace(dnsHost))
                    {
                        hosts.Add(dnsHost.Trim());
                        hosts.Add(dnsHost.Trim() + ".local");
                    }
                }
                catch { }
                try
                {
                    NetworkInterface[] nics = NetworkInterface.GetAllNetworkInterfaces();
                    for (int i = 0; i < nics.Length; i++)
                    {
                        NetworkInterface nic = nics[i];
                        if (nic == null || nic.OperationalStatus != OperationalStatus.Up) continue;
                        IPInterfaceProperties props = nic.GetIPProperties();
                        if (props == null) continue;
                        for (int u = 0; u < props.UnicastAddresses.Count; u++)
                        {
                            IPAddress addr = props.UnicastAddresses[u].Address;
                            if (addr != null)
                            {
                                string ipText = addr.ToString();
                                int pct = ipText.IndexOf('%');
                                if (pct > 0) ipText = ipText.Substring(0, pct);
                                hosts.Add(ipText);
                            }
                        }
                    }
                }
                catch { }
                _cachedLocalHostNames = hosts;
                _cachedLocalHostsTickMs = nowMs;
                return hosts;
            }
        }

        private static bool TryExtractHostAndPort(string hostHeader, out string hostOnly, out int port)
        {
            hostOnly = "";
            port = -1;
            if (string.IsNullOrWhiteSpace(hostHeader) || ShihabProcessAndPathSafety.ContainsControlOrNewline(hostHeader))
            {
                return false;
            }
            string raw = hostHeader.Trim();
            if (raw.StartsWith("[", StringComparison.Ordinal))
            {
                int closeIdx = raw.IndexOf(']');
                if (closeIdx <= 1) return false;
                hostOnly = raw.Substring(1, closeIdx - 1);
                if (closeIdx + 1 < raw.Length)
                {
                    if (raw[closeIdx + 1] != ':') return false;
                    if (!int.TryParse(raw.Substring(closeIdx + 2), NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port <= 0 || port > 65535)
                    {
                        return false;
                    }
                }
                return !string.IsNullOrWhiteSpace(hostOnly);
            }
            int firstColon = raw.IndexOf(':');
            int lastColon = raw.LastIndexOf(':');
            if (firstColon > 0 && firstColon == lastColon)
            {
                hostOnly = raw.Substring(0, firstColon).Trim();
                if (!int.TryParse(raw.Substring(firstColon + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out port) || port <= 0 || port > 65535)
                {
                    return false;
                }
            }
            else if (firstColon < 0)
            {
                hostOnly = raw;
            }
            else
            {
                // Bare IPv6 without brackets
                hostOnly = raw;
            }
            return !string.IsNullOrWhiteSpace(hostOnly);
        }

        private bool IsValidRemoteHostHeader(string hostHeader)
        {
            string hostOnly;
            int hostPort;
            if (!TryExtractHostAndPort(hostHeader, out hostOnly, out hostPort))
            {
                return false;
            }
            if (hostPort > 0 && hostPort != Port)
            {
                return false;
            }
            return GetAllowedLocalHostNames().Contains(hostOnly);
        }

        private bool IsAllowedOriginHeader(string originHeader, string hostHeader)
        {
            if (string.IsNullOrWhiteSpace(originHeader))
            {
                return true;
            }
            string trimmed = originHeader.Trim();
            if (ShihabProcessAndPathSafety.ContainsControlOrNewline(trimmed))
            {
                return false;
            }
            if (trimmed.StartsWith("chrome-extension://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("moz-extension://", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            Uri originUri;
            if (!Uri.TryCreate(trimmed, UriKind.Absolute, out originUri))
            {
                return false;
            }
            if (!string.Equals(originUri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(originUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            string reqHostOnly;
            int reqHostPort;
            if (!TryExtractHostAndPort(hostHeader, out reqHostOnly, out reqHostPort))
            {
                return false;
            }
            string originHost = (originUri.Host ?? "").Trim('[', ']');
            if (!string.Equals(originHost, reqHostOnly, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
            int expectedPort = reqHostPort > 0 ? reqHostPort : Port;
            return originUri.Port == expectedPort;
        }

        private static bool IsJsonContentType(string contentTypeHeader)
        {
            if (string.IsNullOrWhiteSpace(contentTypeHeader)) return false;
            string mediaType = contentTypeHeader.Trim();
            int semi = mediaType.IndexOf(';');
            if (semi >= 0) mediaType = mediaType.Substring(0, semi).Trim();
            return string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase);
        }

        private static string GetAndCleanRemoteTorrentsDirectory()
        {
            string dir = Path.Combine(
                Path.Combine(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ShihabMehediIDM"),
                    "temp"),
                "remote_torrents");
            Directory.CreateDirectory(dir);
            ShihabSecretsVault.ApplyRestrictedUserDirectoryAcl(dir);
            try
            {
                string[] files = Directory.GetFiles(dir, "*.torrent");
                if (files.Length > 0)
                {
                    DateTime cutoff = DateTime.UtcNow.AddHours(-1);
                    List<FileInfo> remaining = new List<FileInfo>(files.Length);
                    for (int i = 0; i < files.Length; i++)
                    {
                        try
                        {
                            FileInfo fi = new FileInfo(files[i]);
                            if (fi.LastWriteTimeUtc < cutoff)
                            {
                                fi.Delete();
                            }
                            else
                            {
                                remaining.Add(fi);
                            }
                        }
                        catch { }
                    }
                    if (remaining.Count >= MaxStoredRemoteTorrents)
                    {
                        remaining.Sort(delegate(FileInfo a, FileInfo b)
                        {
                            return a.LastWriteTimeUtc.CompareTo(b.LastWriteTimeUtc);
                        });
                        int toRemove = remaining.Count - (MaxStoredRemoteTorrents - 1);
                        for (int i = 0; i < toRemove && i < remaining.Count; i++)
                        {
                            try { remaining[i].Delete(); } catch { }
                        }
                    }
                }
            }
            catch { }
            return dir;
        }

        private void HandleClient(object state)
        {
            TcpClient client = state as TcpClient;
            if (client == null) return;
            try
            {
                using (client)
                {
                    client.ReceiveTimeout = 6000;
                    client.SendTimeout = 6000;
                    IPEndPoint remoteEp = client.Client.RemoteEndPoint as IPEndPoint;
                    string remoteIp = remoteEp != null ? remoteEp.Address.ToString() : "unknown";

                    using (NetworkStream ns = client.GetStream())
                    {
                        string method;
                        string path;
                        string hostHeader;
                        string originHeader;
                        string contentTypeHeader;
                        string authHeader;
                        long contentLength;
                        byte[] leftover;
                        int leftoverLen;
                        if (!TryReadHttpHeaders(
                            ns,
                            out method,
                            out path,
                            out hostHeader,
                            out originHeader,
                            out contentTypeHeader,
                            out authHeader,
                            out contentLength,
                            out leftover,
                            out leftoverLen))
                        {
                            return;
                        }

                        if (!IsValidRemoteHostHeader(hostHeader))
                        {
                            WriteJsonResponse(ns, 403, false, "Untrusted Host header rejected.");
                            return;
                        }

                        if (!IsAllowedOriginHeader(originHeader, hostHeader))
                        {
                            WriteJsonResponse(ns, 403, false, "Cross-origin request rejected.");
                            return;
                        }

                        if (path == "/" || path.StartsWith("/?", StringComparison.Ordinal))
                        {
                            string scriptNonce = GenerateCryptographicHex(16);
                            string csp = "default-src 'none'; script-src 'nonce-" + scriptNonce +
                                "'; style-src 'unsafe-inline'; connect-src 'self'; img-src 'self'; base-uri 'none'; frame-ancestors 'none'";
                            WriteHttpResponse(ns, 200, "text/html; charset=utf-8", BuildRemoteWebAppHtml(scriptNonce), csp);
                            return;
                        }

                        if (method == "POST" && path.StartsWith("/api/", StringComparison.Ordinal) && !IsJsonContentType(contentTypeHeader))
                        {
                            WriteJsonResponse(ns, 415, false, "Content-Type: application/json is required.");
                            return;
                        }

                        MehediAppConfiguration cfg = _getConfig != null ? _getConfig() : null;
                        if (cfg == null)
                        {
                            WriteJsonResponse(ns, 500, false, "Server configuration unavailable.");
                            return;
                        }

                        if (path == "/api/pair" && method == "POST")
                        {
                            if (IsIpRateLimited(remoteIp))
                            {
                                WriteJsonResponse(ns, 429, false, "Too many failed pairing attempts. Try again in 60 seconds.");
                                return;
                            }

                            byte[] pairBodyBytes;
                            bool pairTooLarge;
                            if (!TryReadHttpBody(ns, contentLength, MaxJsonBodyBytes, leftover, leftoverLen, out pairBodyBytes, out pairTooLarge))
                            {
                                WriteJsonResponse(ns, pairTooLarge ? 413 : 400, false, pairTooLarge ? "Request body too large." : "Invalid request body.");
                                return;
                            }

                            string pairBody = pairBodyBytes.Length > 0 ? Encoding.UTF8.GetString(pairBodyBytes) : "";
                            Dictionary<string, object> pairJson = !string.IsNullOrWhiteSpace(pairBody)
                                ? ShihabJsonCodec.ParseObjectOrNull(pairBody, 16)
                                : null;

                            string submittedCode = pairJson != null
                                ? ShihabJsonCodec.GetString(pairJson, "code")
                                : ShihabDebridManager.ExtractJsonStringField(pairBody, "code");
                            string devName = SanitizeRemoteLabel(pairJson != null
                                ? ShihabJsonCodec.GetString(pairJson, "device")
                                : ShihabDebridManager.ExtractJsonStringField(pairBody, "device"));

                            bool pairedOk = false;
                            bool codeExpired = false;
                            string rawToken = "";
                            lock (_devicesLock)
                            {
                                DateTime expUtc;
                                if (!string.IsNullOrWhiteSpace(cfg.ShihabRemotePairingExpiresUtc) &&
                                    DateTime.TryParse(cfg.ShihabRemotePairingExpiresUtc, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out expUtc))
                                {
                                    codeExpired = DateTime.UtcNow > expUtc;
                                }

                                if (!codeExpired &&
                                    !string.IsNullOrEmpty(submittedCode) &&
                                    !string.IsNullOrEmpty(cfg.ShihabRemotePairingCode) &&
                                    ConstantTimeEquals(submittedCode.Trim(), cfg.ShihabRemotePairingCode))
                                {
                                    rawToken = GenerateCryptographicHex(32);
                                    string tokenHash = ComputeSha256Hex(rawToken);
                                    if (cfg.ShihabPairedRemoteDevices == null)
                                    {
                                        cfg.ShihabPairedRemoteDevices = new List<ShihabPairedRemoteDevice>();
                                    }
                                    cfg.ShihabPairedRemoteDevices.Add(new ShihabPairedRemoteDevice
                                    {
                                        ShihabDeviceId = Guid.NewGuid().ToString("N").Substring(0, 8),
                                        MehediDeviceName = string.IsNullOrEmpty(devName) ? "Web Remote" : devName,
                                        ShihabTokenHash = tokenHash,
                                        MehediPairedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                                        ShihabLastSeenIp = remoteIp
                                    });
                                    EnsureFreshPairingCode(cfg, null, true);
                                    pairedOk = true;
                                }
                            }

                            if (pairedOk)
                            {
                                if (_saveConfig != null) _saveConfig();

                                Dictionary<string, object> okObj = new Dictionary<string, object>();
                                okObj["ok"] = true;
                                okObj["token"] = rawToken;
                                WriteHttpResponse(ns, 200, "application/json; charset=utf-8", ShihabJsonCodec.Serialize(okObj), null);
                            }
                            else
                            {
                                RecordFailedPairAttempt(remoteIp);
                                string errMsg = codeExpired
                                    ? "Pairing code expired. Refresh Remote Control dialog."
                                    : "Invalid 8-digit pairing code";
                                WriteJsonResponse(ns, 403, false, errMsg);
                            }
                            return;
                        }

                        // Validate bearer token and route BEFORE allocating any request body bytes.
                        ShihabPairedRemoteDevice authedDevice;
                        if (!IsAuthorizedBearer(authHeader, cfg, out authedDevice))
                        {
                            WriteJsonResponse(ns, 401, false, "Unauthorized");
                            return;
                        }
                        if (authedDevice != null)
                        {
                            authedDevice.ShihabLastSeenIp = remoteIp;
                        }

                        if (path == "/api/status" || path == "/api/list")
                        {
                            List<ShihabDownloadItem> items = _getDownloadsSnapshot != null ? _getDownloadsSnapshot() : new List<ShihabDownloadItem>();
                            List<object> arr = new List<object>(items.Count);
                            for (int i = 0; i < items.Count; i++)
                            {
                                ShihabDownloadItem it = items[i];
                                if (it == null || it.ShihabIsPrivateIncognito) continue;
                                Dictionary<string, object> row = new Dictionary<string, object>();
                                row["id"] = it.ShihabId ?? "";
                                row["name"] = it.ShihabFileName ?? "";
                                row["size"] = ShihabFormatHelpers.FormatMehediBytes(it.ShihabTotalBytes);
                                row["pct"] = Math.Round(it.GetMehediPercent(), 1);
                                row["status"] = it.ShihabStatusText ?? "";
                                row["speed"] = ShihabFormatHelpers.FormatShihabSpeed(
                                    it.ShihabCurrentSpeedBps,
                                    ShihabFormatHelpers.IsBitsSpeedUnit(cfg.ShihabDefaultSpeedUnit),
                                    false);
                                arr.Add(row);
                            }
                            Dictionary<string, object> payload = new Dictionary<string, object>();
                            payload["ok"] = true;
                            payload["items"] = arr;
                            payload["speedLimitEnabled"] = cfg.MehediGlobalSpeedLimiterEnabled;
                            payload["speedLimitKb"] = cfg.ShihabGlobalSpeedLimitKb;
                            WriteHttpResponse(ns, 200, "application/json; charset=utf-8", ShihabJsonCodec.Serialize(payload), null);
                            return;
                        }

                        bool isKnownPostRoute = method == "POST" && (
                            path == "/api/speedlimit" ||
                            path == "/api/add" ||
                            path == "/api/torrent" ||
                            path == "/api/action" ||
                            path == "/api/act" ||
                            path == "/api/queue" ||
                            path == "/api/forget");
                        if (!isKnownPostRoute)
                        {
                            WriteJsonResponse(ns, 404, false, "Not found");
                            return;
                        }

                        int routeMaxBody = string.Equals(path, "/api/torrent", StringComparison.Ordinal)
                            ? MaxTorrentUploadBodyBytes
                            : MaxJsonBodyBytes;
                        byte[] bodyBytes;
                        bool payloadTooLarge;
                        if (!TryReadHttpBody(ns, contentLength, routeMaxBody, leftover, leftoverLen, out bodyBytes, out payloadTooLarge))
                        {
                            WriteJsonResponse(ns, payloadTooLarge ? 413 : 400, false, payloadTooLarge ? "Request body exceeds maximum allowed size." : "Invalid request body.");
                            return;
                        }

                        string body = bodyBytes != null && bodyBytes.Length > 0 ? Encoding.UTF8.GetString(bodyBytes) : "";
                        Dictionary<string, object> reqJson = !string.IsNullOrWhiteSpace(body)
                            ? ShihabJsonCodec.ParseObjectOrNull(body, 16)
                            : null;

                        if (path == "/api/speedlimit" && method == "POST")
                        {
                            if (reqJson != null)
                            {
                                if (reqJson.ContainsKey("enabled"))
                                {
                                    cfg.MehediGlobalSpeedLimiterEnabled = ShihabJsonCodec.GetBool(reqJson, "enabled", cfg.MehediGlobalSpeedLimiterEnabled);
                                }
                                else
                                {
                                    cfg.MehediGlobalSpeedLimiterEnabled = !cfg.MehediGlobalSpeedLimiterEnabled;
                                }
                                int reqKb = ShihabJsonCodec.GetInt(reqJson, "limitKb", 0);
                                if (reqKb > 0) cfg.ShihabGlobalSpeedLimitKb = Math.Max(10, Math.Min(1000000, reqKb));
                            }
                            else
                            {
                                cfg.MehediGlobalSpeedLimiterEnabled = !cfg.MehediGlobalSpeedLimiterEnabled;
                            }
                            if (_saveConfig != null) _saveConfig();
                            Dictionary<string, object> respObj = new Dictionary<string, object>();
                            respObj["ok"] = true;
                            respObj["speedLimitEnabled"] = cfg.MehediGlobalSpeedLimiterEnabled;
                            respObj["speedLimitKb"] = cfg.ShihabGlobalSpeedLimitKb;
                            WriteHttpResponse(ns, 200, "application/json; charset=utf-8", ShihabJsonCodec.Serialize(respObj), null);
                            return;
                        }

                        if ((path == "/api/add" || path == "/api/torrent") && method == "POST")
                        {
                            string url = reqJson != null
                                ? ShihabJsonCodec.GetString(reqJson, "url")
                                : ShihabDebridManager.ExtractJsonStringField(body, "url");
                            string name = SanitizeRemoteLabel(reqJson != null
                                ? ShihabJsonCodec.GetString(reqJson, "name")
                                : ShihabDebridManager.ExtractJsonStringField(body, "name"));
                            string b64Torrent = reqJson != null
                                ? ShihabJsonCodec.GetString(reqJson, "torrentBase64")
                                : ShihabDebridManager.ExtractJsonStringField(body, "torrentBase64");

                            bool isValidatedLocalTorrent = false;
                            if (path == "/api/torrent" && !string.IsNullOrWhiteSpace(b64Torrent))
                            {
                                try
                                {
                                    byte[] tBytes = Convert.FromBase64String(b64Torrent);
                                    if (tBytes.Length > 0 && tBytes.Length <= MaxTorrentUploadBodyBytes && tBytes[0] == (byte)'d')
                                    {
                                        ShihabTorrentMetadata meta = ShihabTorrentManager.InspectTorrentBytes(tBytes, "remote.torrent");
                                        if (meta != null && meta.ParsedSuccessfully)
                                        {
                                            string torDir = GetAndCleanRemoteTorrentsDirectory();
                                            string tmpTor = Path.Combine(
                                                torDir,
                                                "mhs_remote_" + GenerateCryptographicHex(8) + ".torrent");
                                            File.WriteAllBytes(tmpTor, tBytes);
                                            ShihabSecretsVault.ApplyRestrictedUserFileAcl(tmpTor);
                                            url = tmpTor;
                                            isValidatedLocalTorrent = true;
                                        }
                                    }
                                }
                                catch { }
                            }

                            if (!isValidatedLocalTorrent && !IsSafeRemoteSubmittedUrl(url))
                            {
                                WriteJsonResponse(ns, 400, false, "Invalid or unsupported download URL.");
                                return;
                            }

                            if (!string.IsNullOrWhiteSpace(url) && _addDownloadUrl != null)
                            {
                                _addDownloadUrl(url.Trim(), name);
                                WriteJsonResponse(ns, 200, true, null);
                            }
                            else
                            {
                                WriteJsonResponse(ns, 400, false, "Missing URL");
                            }
                            return;
                        }

                        if ((path == "/api/action" || path == "/api/act") && method == "POST")
                        {
                            string act = reqJson != null
                                ? ShihabJsonCodec.GetString(reqJson, "action")
                                : ShihabDebridManager.ExtractJsonStringField(body, "action");
                            string id = reqJson != null
                                ? ShihabJsonCodec.GetString(reqJson, "id")
                                : ShihabDebridManager.ExtractJsonStringField(body, "id");
                            if (!string.IsNullOrWhiteSpace(act) && _performDownloadAction != null)
                            {
                                _performDownloadAction(act.Trim().ToLowerInvariant(), id ?? "");
                                WriteJsonResponse(ns, 200, true, null);
                            }
                            else
                            {
                                WriteJsonResponse(ns, 400, false, "Missing action");
                            }
                            return;
                        }

                        if (path == "/api/queue" && method == "POST")
                        {
                            string mode = reqJson != null
                                ? ShihabJsonCodec.GetString(reqJson, "mode")
                                : ShihabDebridManager.ExtractJsonStringField(body, "mode");
                            if (_performDownloadAction != null)
                            {
                                _performDownloadAction(
                                    string.Equals(mode, "stop", StringComparison.OrdinalIgnoreCase) ? "stop_queue" : "start_queue",
                                    "all");
                            }
                            WriteJsonResponse(ns, 200, true, null);
                            return;
                        }

                        if (path == "/api/forget" && method == "POST")
                        {
                            if (authedDevice != null)
                            {
                                lock (_devicesLock)
                                {
                                    if (cfg.ShihabPairedRemoteDevices != null)
                                    {
                                        cfg.ShihabPairedRemoteDevices.Remove(authedDevice);
                                    }
                                }
                                if (_saveConfig != null) _saveConfig();
                            }
                            WriteJsonResponse(ns, 200, true, null);
                            return;
                        }

                        WriteJsonResponse(ns, 404, false, "Not found");
                    }
                }
            }
            catch { }
            finally
            {
                try { _clientSlots.Release(); } catch { }
            }
        }

        private static bool IsSafeRemoteSubmittedUrl(string rawUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl)) return false;
            string s = rawUrl.Trim();
            if (s.StartsWith("magnet:?", StringComparison.OrdinalIgnoreCase))
            {
                return s.IndexOf("xt=urn:btih:", StringComparison.OrdinalIgnoreCase) >= 0;
            }
            Uri parsed;
            if (!Uri.TryCreate(s, UriKind.Absolute, out parsed)) return false;
            return parsed.Scheme == Uri.UriSchemeHttp ||
                   parsed.Scheme == Uri.UriSchemeHttps ||
                   string.Equals(parsed.Scheme, "ftp", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(parsed.Scheme, "ftps", StringComparison.OrdinalIgnoreCase);
        }

        private static string SanitizeRemoteLabel(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return "";
            StringBuilder sb = new StringBuilder(Math.Min(raw.Length, 128));
            for (int i = 0; i < raw.Length && sb.Length < 128; i++)
            {
                char ch = raw[i];
                if (ch >= 0x20 && ch != 0x7F) sb.Append(ch);
            }
            return sb.ToString().Trim();
        }

        private static string GenerateCryptographicHex(int byteCount)
        {
            byte[] rnd = new byte[byteCount];
            using (RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(rnd);
            }
            StringBuilder sb = new StringBuilder(byteCount * 2);
            for (int i = 0; i < rnd.Length; i++) sb.Append(rnd[i].ToString("x2"));
            return sb.ToString();
        }

        private static bool TryReadHttpHeaders(
            Stream ns,
            out string method,
            out string path,
            out string hostHeader,
            out string originHeader,
            out string contentTypeHeader,
            out string authHeader,
            out long contentLength,
            out byte[] leftover,
            out int leftoverLen)
        {
            method = "GET";
            path = "/";
            hostHeader = "";
            originHeader = "";
            contentTypeHeader = "";
            authHeader = "";
            contentLength = 0;
            leftover = null;
            leftoverLen = 0;

            using (MemoryStream hdrBuf = new MemoryStream(1024))
            {
                byte[] chunk = new byte[4096];
                int headerEndOffset = -1;

                while (hdrBuf.Length < MaxHeaderBytes)
                {
                    int read = ns.Read(chunk, 0, Math.Min(chunk.Length, MaxHeaderBytes - (int)hdrBuf.Length));
                    if (read <= 0) break;
                    hdrBuf.Write(chunk, 0, read);
                    byte[] acc = hdrBuf.GetBuffer();
                    int len = (int)hdrBuf.Length;
                    for (int i = Math.Max(3, len - read); i < len; i++)
                    {
                        if (acc[i - 3] == '\r' && acc[i - 2] == '\n' && acc[i - 1] == '\r' && acc[i] == '\n')
                        {
                            headerEndOffset = i + 1;
                            leftoverLen = len - headerEndOffset;
                            if (leftoverLen > 0)
                            {
                                leftover = new byte[leftoverLen];
                                Buffer.BlockCopy(acc, headerEndOffset, leftover, 0, leftoverLen);
                            }
                            break;
                        }
                    }
                    if (headerEndOffset >= 0) break;
                }

                if (headerEndOffset <= 0) return false;
                string hdrText = Encoding.ASCII.GetString(hdrBuf.GetBuffer(), 0, headerEndOffset);
                string[] lines = hdrText.Split(new string[] { "\r\n" }, StringSplitOptions.None);
                if (lines.Length == 0 || string.IsNullOrEmpty(lines[0])) return false;

                string[] firstParts = lines[0].Split(' ');
                method = firstParts.Length > 0 ? firstParts[0].ToUpperInvariant() : "GET";
                path = firstParts.Length > 1 ? firstParts[1] : "/";

                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (string.IsNullOrEmpty(line)) break;
                    if (line.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
                    {
                        long parsedLen;
                        if (!long.TryParse(line.Substring(15).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out parsedLen))
                        {
                            contentLength = -1;
                        }
                        else
                        {
                            contentLength = parsedLen;
                        }
                    }
                    else if (line.StartsWith("Host:", StringComparison.OrdinalIgnoreCase))
                    {
                        hostHeader = line.Substring(5).Trim();
                    }
                    else if (line.StartsWith("Origin:", StringComparison.OrdinalIgnoreCase))
                    {
                        originHeader = line.Substring(7).Trim();
                    }
                    else if (line.StartsWith("Content-Type:", StringComparison.OrdinalIgnoreCase))
                    {
                        contentTypeHeader = line.Substring(13).Trim();
                    }
                    else if (line.StartsWith("Authorization:", StringComparison.OrdinalIgnoreCase))
                    {
                        authHeader = line.Substring(14).Trim();
                    }
                }
                return true;
            }
        }

        private static bool TryReadHttpBody(
            Stream ns,
            long contentLength,
            int maxBodyBytes,
            byte[] leftover,
            int leftoverLen,
            out byte[] bodyBytes,
            out bool payloadTooLarge)
        {
            bodyBytes = new byte[0];
            payloadTooLarge = false;
            if (contentLength < 0 || contentLength > maxBodyBytes)
            {
                payloadTooLarge = contentLength > maxBodyBytes;
                return false;
            }
            if (contentLength == 0)
            {
                return true;
            }

            int expected = (int)contentLength;
            bodyBytes = new byte[expected];
            int totalRead = 0;
            if (leftover != null && leftoverLen > 0)
            {
                int copyLen = Math.Min(leftoverLen, expected);
                Buffer.BlockCopy(leftover, 0, bodyBytes, 0, copyLen);
                totalRead = copyLen;
            }
            while (totalRead < expected)
            {
                int r = ns.Read(bodyBytes, totalRead, expected - totalRead);
                if (r <= 0) break;
                totalRead += r;
            }
            if (totalRead < expected)
            {
                byte[] trimmed = new byte[totalRead];
                Buffer.BlockCopy(bodyBytes, 0, trimmed, 0, totalRead);
                bodyBytes = trimmed;
            }
            return true;
        }

        public static bool ConstantTimeEquals(string a, string b)
        {
            if (a == null || b == null) return false;
            int maxLen = Math.Max(a.Length, b.Length);
            int diff = a.Length ^ b.Length;
            for (int i = 0; i < maxLen; i++)
            {
                char ca = i < a.Length ? a[i] : '\0';
                char cb = i < b.Length ? b[i] : '\0';
                diff |= (ca ^ cb);
            }
            return diff == 0;
        }

        private bool IsAuthorizedBearer(string authHeader, MehediAppConfiguration cfg, out ShihabPairedRemoteDevice matchedDevice)
        {
            matchedDevice = null;
            if (string.IsNullOrWhiteSpace(authHeader) || cfg == null) return false;
            string token = authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
                ? authHeader.Substring(7).Trim()
                : authHeader.Trim();
            if (string.IsNullOrEmpty(token)) return false;
            string hash = ComputeSha256Hex(token).ToLowerInvariant();
            lock (_devicesLock)
            {
                if (cfg.ShihabPairedRemoteDevices == null) return false;
                for (int i = 0; i < cfg.ShihabPairedRemoteDevices.Count; i++)
                {
                    string devHash = (cfg.ShihabPairedRemoteDevices[i].ShihabTokenHash ?? "").ToLowerInvariant();
                    if (devHash.Length == 64 && ConstantTimeEquals(devHash, hash))
                    {
                        matchedDevice = cfg.ShihabPairedRemoteDevices[i];
                        return true;
                    }
                }
            }
            return false;
        }

        private static string ComputeSha256Hex(string input)
        {
            SHA256 sha = SharedSha256.Value;
            byte[] b = sha.ComputeHash(Encoding.UTF8.GetBytes(input ?? ""));
            const string digits = "0123456789abcdef";
            char[] hex = new char[b.Length * 2];
            for (int i = 0; i < b.Length; i++)
            {
                hex[i * 2] = digits[(b[i] >> 4) & 0x0F];
                hex[i * 2 + 1] = digits[b[i] & 0x0F];
            }
            return new string(hex);
        }

        private static void WriteJsonResponse(NetworkStream ns, int statusCode, bool ok, string error)
        {
            Dictionary<string, object> obj = new Dictionary<string, object>();
            obj["ok"] = ok;
            if (!string.IsNullOrEmpty(error)) obj["error"] = error;
            WriteHttpResponse(ns, statusCode, "application/json; charset=utf-8", ShihabJsonCodec.Serialize(obj), null);
        }

        private static void WriteHttpResponse(NetworkStream ns, int statusCode, string contentType, string body, string cspHeader)
        {
            byte[] payload = Encoding.UTF8.GetBytes(body ?? "");
            string statusText;
            switch (statusCode)
            {
                case 200: statusText = "OK"; break;
                case 400: statusText = "Bad Request"; break;
                case 401: statusText = "Unauthorized"; break;
                case 403: statusText = "Forbidden"; break;
                case 404: statusText = "Not Found"; break;
                case 413: statusText = "Payload Too Large"; break;
                case 415: statusText = "Unsupported Media Type"; break;
                case 429: statusText = "Too Many Requests"; break;
                default: statusText = "Error"; break;
            }
            StringBuilder hdr = new StringBuilder();
            hdr.AppendFormat(CultureInfo.InvariantCulture, "HTTP/1.1 {0} {1}\r\n", statusCode, statusText);
            hdr.AppendFormat(CultureInfo.InvariantCulture, "Content-Type: {0}\r\n", contentType);
            hdr.AppendFormat(CultureInfo.InvariantCulture, "Content-Length: {0}\r\n", payload.Length);
            hdr.Append("Cache-Control: no-store\r\n");
            hdr.Append("X-Content-Type-Options: nosniff\r\n");
            hdr.Append("X-Frame-Options: DENY\r\n");
            hdr.Append("Referrer-Policy: no-referrer\r\n");
            if (!string.IsNullOrEmpty(cspHeader))
            {
                hdr.Append("Content-Security-Policy: ").Append(cspHeader).Append("\r\n");
            }
            hdr.Append("Connection: close\r\n\r\n");
            byte[] hdrBytes = Encoding.ASCII.GetBytes(hdr.ToString());
            ns.Write(hdrBytes, 0, hdrBytes.Length);
            ns.Write(payload, 0, payload.Length);
        }

        private static string BuildRemoteWebAppHtml(string scriptNonce)
        {
            string header = @"<!DOCTYPE html>
<html><head><meta charset='utf-8'><meta name='viewport' content='width=device-width,initial-scale=1'>
<title>MHS Download Manager Remote</title>
<style>
body{background:#0b1120;color:#e2e8f0;font-family:system-ui,sans-serif;margin:0;padding:16px}
.card{background:#111827;border:1px solid #1e293b;border-radius:10px;padding:14px;margin-bottom:12px}
input,button{padding:9px 12px;border-radius:6px;border:1px solid #334155;background:#1e293b;color:#f8fafc;font-size:14px}
button{background:#0284c7;border:none;cursor:pointer;font-weight:600}
button.sec{background:#334155}
button.danger{background:#be123c}
.bar{height:8px;background:#1e293b;border-radius:4px;overflow:hidden;margin:6px 0}
.fill{height:100%;background:#38bdf8}
</style></head><body>
<h2 style='margin-top:0;color:#38bdf8'>MHS Download Manager — Remote Control</h2>
<div id='pairBox' class='card'>
  <b>Pair Device (Single-Use 8-Digit Code):</b><br><br>
  <input id='codeInp' placeholder='8-digit pairing code' style='width:180px'>
  <button id='pairBtn'>Pair</button>
</div>
<div id='mainBox' style='display:none'>
  <div class='card' style='display:flex;gap:8px;flex-wrap:wrap;align-items:center'>
    <input id='urlInp' placeholder='https://... or magnet:?xt=...' style='flex:1;min-width:200px'>
    <button id='addBtn'>+ Add URL</button>
    <input id='torInp' type='file' accept='.torrent' style='display:none'>
    <button class='sec' id='torBtn'>+ .torrent</button>
    <button class='sec' id='qStartBtn'>Start Queue</button>
    <button class='sec' id='qStopBtn'>Stop Queue</button>
    <button class='sec' id='spdBtn'>Speed Limit: Off</button>
    <button class='danger' id='forgetBtn'>Unpair</button>
  </div>
  <div id='list'></div>
</div>
<script nonce='" + scriptNonce + @"'>";

            string scriptBody = @"
var tok = localStorage.getItem('mhs_remote_token') || '';
if (location.hash.indexOf('#pair=') === 0) {
  document.getElementById('codeInp').value = decodeURIComponent(location.hash.substring(6));
} else if (location.search.indexOf('code=') >= 0) {
  var m = /[?&]code=([0-9]{8})/.exec(location.search);
  if (m) document.getElementById('codeInp').value = m[1];
}
document.getElementById('pairBtn').addEventListener('click', pairNow);
document.getElementById('addBtn').addEventListener('click', addUrl);
document.getElementById('torBtn').addEventListener('click', function(){ document.getElementById('torInp').click(); });
document.getElementById('torInp').addEventListener('change', uploadTorrentFile);
document.getElementById('qStartBtn').addEventListener('click', function(){ qAct('start'); });
document.getElementById('qStopBtn').addEventListener('click', function(){ qAct('stop'); });
document.getElementById('spdBtn').addEventListener('click', toggleSpeedLimit);
document.getElementById('forgetBtn').addEventListener('click', forgetDev);
document.getElementById('list').addEventListener('click', function(e) {
  var btn = e.target;
  if (!btn || btn.tagName !== 'BUTTON') return;
  var a = btn.getAttribute('data-act');
  var id = btn.getAttribute('data-id');
  if (a && id) act(a, id);
});
if (tok) {
  document.getElementById('pairBox').style.display = 'none';
  document.getElementById('mainBox').style.display = 'block';
  refresh();
  setInterval(refresh, 2000);
}
function pairNow() {
  var c = document.getElementById('codeInp').value.trim();
  fetch('/api/pair', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify({ code: c, device: navigator.platform }) })
    .then(function(r) { return r.json(); })
    .then(function(d) {
      if (d.ok) {
        tok = d.token;
        localStorage.setItem('mhs_remote_token', tok);
        location.hash = '';
        location.reload();
      } else {
        alert(d.error || 'Failed');
      }
    });
}
function addUrl() {
  var u = document.getElementById('urlInp').value.trim();
  if (!u) return;
  fetch('/api/add', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + tok }, body: JSON.stringify({ url: u }) })
    .then(function() { document.getElementById('urlInp').value = ''; refresh(); });
}
function uploadTorrentFile(ev) {
  var f = ev.target.files && ev.target.files[0];
  if (!f) return;
  var rd = new FileReader();
  rd.onload = function() {
    var dataUrl = String(rd.result || '');
    var comma = dataUrl.indexOf(',');
    var b64 = comma >= 0 ? dataUrl.substring(comma + 1) : dataUrl;
    fetch('/api/torrent', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + tok }, body: JSON.stringify({ name: f.name, torrentBase64: b64 }) })
      .then(function() { ev.target.value = ''; refresh(); });
  };
  rd.readAsDataURL(f);
}
function toggleSpeedLimit() {
  fetch('/api/speedlimit', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + tok }, body: '{}' })
    .then(function() { refresh(); });
}
function act(a, id) {
  fetch('/api/action', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + tok }, body: JSON.stringify({ action: a, id: id }) })
    .then(function() { refresh(); });
}
function qAct(m) {
  fetch('/api/queue', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + tok }, body: JSON.stringify({ mode: m }) })
    .then(function() { refresh(); });
}
function forgetDev() {
  fetch('/api/forget', { method: 'POST', headers: { 'Content-Type': 'application/json', Authorization: 'Bearer ' + tok }, body: '{}' })
    .then(function() { localStorage.removeItem('mhs_remote_token'); location.reload(); });
}
function makeBtn(cls, label, actName, itemId) {
  var b = document.createElement('button');
  b.className = cls;
  b.textContent = label;
  b.setAttribute('data-act', actName);
  b.setAttribute('data-id', itemId);
  return b;
}
function refresh() {
  fetch('/api/status', { headers: { Authorization: 'Bearer ' + tok } })
    .then(function(r) {
      if (r.status === 401) { localStorage.removeItem('mhs_remote_token'); location.reload(); return null; }
      return r.json();
    })
    .then(function(d) {
      if (!d || !d.items) return;
      var spdBtn = document.getElementById('spdBtn');
      if (spdBtn) {
        spdBtn.textContent = d.speedLimitEnabled ? ('Speed Limit: ' + (d.speedLimitKb || 1024) + ' KB/s') : 'Speed Limit: Off';
      }
      var listEl = document.getElementById('list');
      listEl.textContent = '';
      if (d.items.length === 0) {
        var empty = document.createElement('div');
        empty.className = 'card';
        empty.textContent = 'No downloads in list.';
        listEl.appendChild(empty);
        return;
      }
      for (var i = 0; i < d.items.length; i++) {
        var it = d.items[i];
        var pct = Math.max(0, Math.min(100, Number(it.pct) || 0));
        var card = document.createElement('div');
        card.className = 'card';
        var title = document.createElement('b');
        title.textContent = it.name || '';
        card.appendChild(title);
        card.appendChild(document.createTextNode(' '));
        var sz = document.createElement('span');
        sz.style.color = '#94a3b8';
        sz.textContent = '(' + (it.size || '') + ')';
        card.appendChild(sz);
        var bar = document.createElement('div');
        bar.className = 'bar';
        var fill = document.createElement('div');
        fill.className = 'fill';
        fill.style.width = pct + '%';
        bar.appendChild(fill);
        card.appendChild(bar);
        var row = document.createElement('div');
        row.style.display = 'flex';
        row.style.justifyContent = 'space-between';
        row.style.alignItems = 'center';
        var st = document.createElement('small');
        st.textContent = (it.status || '') + ' ' + (it.speed || '');
        row.appendChild(st);
        var btns = document.createElement('div');
        var itemId = String(it.id || '');
        btns.appendChild(makeBtn('sec', 'Start', 'resume', itemId));
        btns.appendChild(document.createTextNode(' '));
        btns.appendChild(makeBtn('sec', 'Pause', 'pause', itemId));
        btns.appendChild(document.createTextNode(' '));
        btns.appendChild(makeBtn('sec', 'Remove', 'remove', itemId));
        row.appendChild(btns);
        card.appendChild(row);
        listEl.appendChild(card);
      }
    });
}
</script></body></html>";

            return header + scriptBody;
        }
    }
}
